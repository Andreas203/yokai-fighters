"""Test Breaker probes for YOK-221 (properties.forward_travel in tools/validate_data.py).
The same value list runs through the C# loader in game/tests/probes/ForwardTravelBreakerProbes.cs
(Loader_AgreesWithTheValidatorOnForwardTravelValues): the two must give the same verdict on every row.
Run: python3 -m unittest discover -s tools -p 'test_*.py'"""
import io
import json
import os
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import validate_data  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
NORMAL = ROOT / "game" / "tests" / "fixtures" / "ryo-normals" / "test-ryo-light-punch.json"

# (raw JSON text of the value, loader accepts it). The loader's rule: a whole number in int32 written any way JSON
# allows (Effects.Whole), >= 0.
VALUES = [
    ("300", True), ("300.0", True), ("3e2", True), ("0", True), ("0.0", True), ("-0", True), ("-0.0", True),
    ("2147483647", True), ("2147483648", False), ("-1", False), ("2.5", False), ('"300"', False), ("true", False),
    ("[]", False), ("{}", False), ("1e400", False),
    ("300.0000000000000000001", False),  # not a whole number: Python's float() rounds it to 300.0, C# decimal keeps it
]


def validate_text(path):
    out = io.StringIO()
    code = validate_data.validate([str(path)], out=out)
    return code, out.getvalue()


def normal_with(raw_value, active=None):
    doc = json.loads(NORMAL.read_text(encoding="utf-8"))
    doc["clip"] = "ryo-light-punch"
    if active is not None:
        doc["frame_data"]["active"] = active
        doc["frame_data"]["hitboxes"] = []
    text = json.dumps(doc)
    return text.replace('"frame_data": {', '"frame_data": {"properties": {"forward_travel": %s},' % raw_value, 1)


class ForwardTravelValidatorProbes(unittest.TestCase):
    def travel_errors(self, text):
        with tempfile.TemporaryDirectory() as t:
            p = Path(t) / "test-ryo-light-punch.json"
            p.write_text(text, encoding="utf-8")
            return [l for l in validate_text(p)[1].splitlines() if "forward_travel" in l]

    def test_validator_agrees_with_the_loader_on_every_value(self):
        # YOK-221 / F3: one whole-number rule for the loader and the validator (Effects.Whole == validate_data.whole).
        wrong = []
        for raw, loader_accepts in VALUES:
            if bool(self.travel_errors(normal_with(raw))) == loader_accepts:
                wrong.append(f"forward_travel {raw}: loader {'accepts' if loader_accepts else 'refuses'}")
        self.assertEqual([], wrong, "the validator and the loader disagree")

    def test_validator_refuses_travel_with_no_active_frames_on_a_normal(self):
        # YOK-221: MoveData.ReadTravel refuses forward_travel > 0 when active <= 0, for every move kind it loads.
        self.assertTrue(self.travel_errors(normal_with("300", active=0)),
                        "a normal with forward_travel 300 and 0 active frames loads nowhere; the validator says nothing")


# Round 2 (after ee46383: JsonFloat, exact Decimal judgement). (raw JSON text, is a whole number in int32 and >= 0, judged mathematically).
EXACT_VALUES = [
    ("300.00000000000000000000000000000000", True),   # 32 zeros: whole
    ("300.00000000000000000000000000000001", False),  # past C# decimal's 28 digits, still a fraction
    ("3.0000000000000000000000000000001e2", False),   # = 300.00000000000000000000000000001
    ("0.0000000000000000000000000000000001", False),  # 1e-34: a fraction, not 0
    ("-0.0000000000000000000000000000000000001", False),
    ("1e-400", False), ("0e999999999", True),  # 1e999999999 has its own test: it hangs whole()
    ("2147483647.0", True), ("2147483647.5", False), ("2147483648.0", False),
    ("2147483647.0000000000000000000000000000000001", False),
    ("-0.0e5", True), ("1E2", True), ("1e+2", True), ("0.3e3", True), ("3000e-1", True),
]


class ForwardTravelExactnessProbes(unittest.TestCase):
    def travel_errors(self, raw):
        return ForwardTravelValidatorProbes.travel_errors(None, normal_with(raw))

    def test_validator_verdict_is_the_mathematical_one_on_exact_edge_values(self):
        wrong = [raw for raw, ok in EXACT_VALUES if bool(self.travel_errors(raw)) == ok]
        self.assertEqual([], wrong, "validator verdict differs from the whole-number rule")

    def test_nan_and_infinity_literals_are_refused_not_crashed(self):
        for raw in ("NaN", "Infinity", "-Infinity"):
            self.assertTrue(self.travel_errors(raw), f"forward_travel {raw} must be reported")

    def test_a_huge_exponent_does_not_hang_the_validator(self):
        # int(Decimal("1e999999999")) would build a 10**999999999 integer; the loader just refuses it.
        import subprocess
        code = ("import sys; sys.path.insert(0, %r); import validate_data as v;"
                "print(v.whole(v.JsonFloat('1e9999999')), v.whole(v.JsonFloat('1e999999999')))"
                % str(ROOT / "tools"))
        try:
            r = subprocess.run([sys.executable, "-c", code], capture_output=True, text=True, timeout=15)
        except subprocess.TimeoutExpired:
            self.fail("whole(JsonFloat('1e9999999')) did not finish in 15 s")
        self.assertEqual("None None", r.stdout.strip(), r.stderr[-300:])

    def test_one_report_per_problem(self):
        # A bad forward_travel is reported once per distinct message, not once by the schema and again by travel_errors
        # under the same words (a duplicate line is noise for the designer).
        for raw in ("-1", "2.5", "true", '"3"'):
            lines = self.travel_errors(raw)
            msgs = [l.split(": ", 1)[1] for l in lines]
            self.assertEqual(len(msgs), len(set(msgs)), f"{raw}: duplicate report {lines}")
            self.assertLessEqual(len(lines), 1, f"{raw}: more than one forward_travel line: {lines}")

    def test_jsonfloat_survives_deepcopy_and_dumps(self):
        import copy
        v = validate_data.loads('{"a": 300.0000000000000000001, "b": [1e2]}')
        c = copy.deepcopy(v)
        self.assertIsNone(validate_data.whole(c["a"]))
        self.assertEqual(100, validate_data.whole(c["b"][0]))
        self.assertEqual(300.0, json.loads(json.dumps(v))["a"])

    def test_no_existing_data_file_changes_verdict(self):
        # Every JSON under data/ and the C# fixtures: the validator's output text matches the pre-YOK-221 validator's.
        import subprocess
        old = subprocess.run(["git", "show", "c2f48d9:tools/validate_data.py"], cwd=ROOT, capture_output=True)
        self.assertEqual(0, old.returncode, old.stderr)  # bytes: the source holds non-ASCII text
        tmp = ROOT / "tools" / "_validate_data_main_probe.py"
        tmp.write_bytes(old.stdout)
        try:
            paths = [str(ROOT / "data"), str(ROOT / "game" / "tests" / "fixtures")]
            def run_with(mod):
                r = subprocess.run([sys.executable, str(mod)] + paths, capture_output=True, text=True, encoding="utf-8", errors="replace", cwd=ROOT, timeout=900,
                                   env={**os.environ, "PYTHONIOENCODING": "utf-8"})
                return r.returncode, sorted(l for l in (r.stdout + r.stderr).splitlines())
            old_code, old_lines = run_with(tmp)
            new_code, new_lines = run_with(ROOT / "tools" / "validate_data.py")
        finally:
            tmp.unlink()
        self.assertEqual(old_code, new_code, "exit code changed")
        self.assertEqual(old_lines, new_lines, "validator output on existing files changed")
