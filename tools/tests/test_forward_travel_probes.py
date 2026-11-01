"""Test Breaker probes for YOK-221 (properties.forward_travel in tools/validate_data.py).
The same value list runs through the C# loader in game/tests/probes/ForwardTravelBreakerProbes.cs
(Loader_AgreesWithTheValidatorOnForwardTravelValues): the two must give the same verdict on every row.
Run: python3 -m unittest discover -s tools -p 'test_*.py'"""
import io
import json
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
