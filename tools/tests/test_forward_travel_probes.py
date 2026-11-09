"""Test Breaker probes for YOK-221 (properties.forward_travel in tools/validate_data.py).
The same value list runs through the C# loader in game/tests/probes/ForwardTravelBreakerProbes.cs
(Loader_AgreesWithTheValidatorOnForwardTravelValues): the two must give the same verdict on every row.
Run: python3 -m unittest discover -s tools -p 'test_*.py'"""
import io
import json
import re
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


# Round 2. Designer ruling 2026-11-02: the validator matches the loader, and a "whole number" is what .NET decimal
# (TryGetDecimal) reads: it rounds past ~28 digits and tiny exponents become 0. So each row is the LOADER'S verdict
# (True = accepted), pinned by ForwardTravelExactBreakerProbes.Loader_AndValidatorAgreeOnLongAndExtremeNumbers in C#,
# which carries the same rows. 1e999999999 is only in the C# list: here it has its own (hang) test.
DECIMAL_VALUES = [
    ("300.00000000000000000000000000000000", True),
    ("300.00000000000000000000000000000001", True),   # past decimal's digits: rounds to 300
    ("3.0000000000000000000000000000001e2", True),
    ("0.0000000000000000000000000000000001", True),   # 1e-34 reads as 0
    ("-0.0000000000000000000000000000000000001", True),
    ("1e-400", True), ("0e999999999", True),
    ("2147483647.0", True), ("2147483647.5", False), ("2147483648.0", False),
    ("2147483647.0000000000000000000000000000000001", True),
    ("-0.0e5", True), ("1E2", True), ("1e+2", True), ("0.3e3", True), ("3000e-1", True),
    ("300.0000000000000000001", False),               # 22 digits: decimal keeps the fraction
    ("-0.5", False), ("1e400", False),
]


class ForwardTravelExactnessProbes(unittest.TestCase):
    def travel_errors(self, raw):
        return ForwardTravelValidatorProbes.travel_errors(None, normal_with(raw))

    def test_validator_and_loader_agree_on_long_and_extreme_numbers(self):
        wrong = [f"{raw}: loader {'accepts' if ok else 'refuses'}, validator {'refuses' if self.travel_errors(raw) else 'accepts'}"
                 for raw, ok in DECIMAL_VALUES if bool(self.travel_errors(raw)) == ok]
        self.assertEqual([], wrong, "validator verdict differs from the loader's")

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
        # Every JSON under data/ and the C# fixtures keeps the verdict (valid or not) the pre-YOK-221 validator gave it.
        # Verdicts, not message text: later tickets reword messages (YOK-224's "(or properties.teleport)") and add kinds
        # (YOK-93's sound_cue), and neither is a forward_travel change.
        import subprocess
        old = subprocess.run(["git", "show", "c2f48d9:tools/validate_data.py"], cwd=ROOT, capture_output=True)
        self.assertEqual(0, old.returncode, old.stderr)  # bytes: the source holds non-ASCII text
        tmp = ROOT / "tools" / "_validate_data_main_probe.py"
        tmp.write_bytes(old.stdout)
        # Features added after c2f48d9 that the old validator can't read: YOK-93's sound_cue kind, and YOK-225's command-grab
        # fixture (a grab has no hitstun), which every requires-[] modifier is built onto.
        later = re.compile(r"unknown kind '(sound_cue)'|: on test-river-grab Lv ")
        try:
            paths = [str(ROOT / "data"), str(ROOT / "game" / "tests" / "fixtures")]
            def run_with(mod):
                r = subprocess.run([sys.executable, str(mod)] + paths, capture_output=True, text=True, encoding="utf-8", errors="replace", cwd=ROOT, timeout=900,
                                   env={**os.environ, "PYTHONIOENCODING": "utf-8"})
                lines = [l for l in (r.stdout + r.stderr).splitlines() if ": " in l and not l.startswith("validate_data: ")]
                return r.returncode, {l.split(": ", 1)[0] for l in lines}, {l.split(": ", 1)[0] for l in lines if later.search(l)}
            old_code, old_bad, old_later = run_with(tmp)
            new_code, new_bad, _ = run_with(ROOT / "tools" / "validate_data.py")
        finally:
            tmp.unlink()
        self.assertEqual(old_code, new_code, "exit code changed")
        self.assertEqual(sorted(old_bad - old_later), sorted(new_bad - old_later), "the set of files the validator refuses changed")


class DotnetDecimalProbes(unittest.TestCase):
    """Round 3: validate_data.dotnet_decimal / whole() / loader_view against the values .NET's own
    JsonElement.TryGetDecimal gave for 2046 texts (game/tests/probes/ForwardTravelDecimalGolden.json, kept honest by the
    C# probe ForwardTravelDecimalBreakerProbes.Golden_IsWhatDotNetReadsForEveryCase)."""
    GOLDEN = json.loads((ROOT / "game" / "tests" / "probes" / "ForwardTravelDecimalGolden.json").read_text(encoding="utf-8"))

    def test_dotnet_decimal_matches_the_loader_golden(self):
        import decimal
        wrong = []
        for text, want in self.GOLDEN:
            got = validate_data.dotnet_decimal(text)
            if (got is None) != (want is None) or (got is not None and got != decimal.Decimal(want)):
                wrong.append(f"{text}: .NET {want}, dotnet_decimal {got}")
        self.assertEqual([], wrong[:12], f"{len(wrong)} of {len(self.GOLDEN)} differ")

    def test_whole_matches_the_loader_golden(self):
        # The loader's WholeInt: TryGetInt32, else a decimal that is integral and inside int32.
        import decimal
        wrong = []
        for text, want in self.GOLDEN:
            d = None if want is None else decimal.Decimal(want)
            expect = int(d) if d is not None and d == d.to_integral_value() and -2**31 <= d <= 2**31 - 1 else None
            if "." in text or "e" in text.lower():
                got = validate_data.whole(validate_data.JsonFloat(text))
            else:
                got = validate_data.whole(int(text))
                expect = int(text) if -2**31 <= int(text) <= 2**31 - 1 else None
            if got != expect:
                wrong.append(f"{text}: loader {expect}, whole() {got}")
        self.assertEqual([], wrong[:12], f"{len(wrong)} of {len(self.GOLDEN)} differ")

    def test_loader_view_only_turns_loader_whole_numbers_into_ints(self):
        import decimal
        wrong = []
        for text, want in self.GOLDEN:
            if not ("." in text or "e" in text.lower()):
                continue
            seen = validate_data.loader_view(validate_data.JsonFloat(text))
            d = None if want is None else decimal.Decimal(want)
            whole_dec = d is not None and d == d.to_integral_value()
            if whole_dec and not (isinstance(seen, int) and seen == int(d)):
                wrong.append(f"{text}: .NET reads whole {d}, the schema sees {seen!r}")
            if not whole_dec and isinstance(seen, int):
                wrong.append(f"{text}: .NET does not read it as whole, the schema sees int {seen}")
        self.assertEqual([], wrong[:12], f"{len(wrong)} differ")
