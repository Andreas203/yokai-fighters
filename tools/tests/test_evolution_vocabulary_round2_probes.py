"""Test Breaker round 2 for YOK-220 (after 232d26f: the loader mirror runs when only another field fails the schema).
Run: python -m unittest discover -s tools -p 'test_*.py'. Expected values: the loader refuses these (MoveLoader / SpecialData.Build),
the validator mirrors it (YOK-103), and a report line says each problem once."""
import copy
import io
import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import validate_data  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
FIX = ROOT / "game" / "tests" / "fixtures"
NORMAL = FIX / "ryo-normals" / "test-ryo-heavy-kick.json"
THROW = FIX / "throws" / "test-ryo-throw.json"
WAVE = FIX / "specials" / "test-spirit-wave.json"
TALISMAN = FIX / "specials" / "test-rising-talisman.json"
OUTSIDE = 3000000000


def check(doc, name):
    """(exit code, report lines about this file) for the doc written as <name>.json, the fixture's missing clip filtered out."""
    with tempfile.TemporaryDirectory() as tmp:
        f = Path(tmp) / f"{name}.json"
        f.write_text(json.dumps(doc), encoding="utf-8")
        out = io.StringIO()
        code = validate_data.validate([str(f)], out=out)
        lines = [l.split(".json: ", 1)[1] for l in out.getvalue().splitlines() if ".json: " in l]
    return code, [l for l in lines if "clip" not in l]


def load(p):
    return json.loads(p.read_text(encoding="utf-8"))


class RoundTwoProbes(unittest.TestCase):
    def test_partly_invalid_normal_reports_every_problem_once(self):
        d = load(NORMAL)
        d["id"] = "p2-partly-invalid"
        d["rules"] = ["Z9"]
        d["frame_data"]["damage"] = OUTSIDE
        code, lines = check(d, "p2-partly-invalid")
        self.assertEqual(code, 1)
        self.assertTrue(any("Z9" in l for l in lines), lines)
        self.assertTrue(any("damage" in l and "int32" in l for l in lines), lines)
        self.assertEqual(len(lines), len(set(lines)), f"a line reported twice: {lines}")

    def test_one_bad_pending_field_is_one_report_line(self):
        # Duplicate error reports: an out-of-int32 pending value is named by bad_numbers AND by the pending-shape check.
        cases = {"decoy": OUTSIDE, "forward_travel": OUTSIDE, "wall_bounce": 1,
                 "multi_hit": {"hits": OUTSIDE, "interval": 1}, "throw": {"range": OUTSIDE, "breakable": True}}
        for key, value in cases.items():
            with self.subTest(key):
                d = load(NORMAL)
                d["id"] = "p2-one-line"
                d["frame_data"]["properties"] = {key: value}
                code, lines = check(d, "p2-one-line")
                mine = [l for l in lines if key in l]
                self.assertEqual(code, 1)
                self.assertEqual(len(mine), 1, f"{key} reported {len(mine)} times: {mine}")

    def test_schema_failed_frame_data_does_not_crash_the_mirror(self):
        for key, value in (("properties", []), ("properties", "x"), ("properties", None), ("hitboxes", "x"), ("damage", "sixty"),
                           ("cancel_windows", [{"frames": [1], "on": ["hit"]}])):
            with self.subTest(key, value=value):
                d = load(NORMAL)
                d["id"] = "p2-crash"
                d["frame_data"][key] = value
                code, lines = check(d, "p2-crash")  # an exception here is the failure
                self.assertEqual(code, 1)

    def test_special_with_a_bad_field_outside_its_moves_still_builds_every_level(self):
        # status is not part of the move; the Lv 3 / Lv 2 / EX pending problems must still be named.
        for where, mutate, label in (
            ("evolution", lambda d: d["levels"]["3"]["evolution"].update(changes=[{"target": "forward_travel", "op": "set", "value": -1}]), "Lv 3"),
            ("tuning", lambda d: d["levels"]["2"].update(tuning={"target": "decoy", "op": "set", "value": 0}), "Lv 2"),
            ("ex-decoy-zero", lambda d: d["ex"].update(changes=[{"target": "decoy", "op": "set", "value": 0}]), "EX"),
        ):
            with self.subTest(where):
                d = load(WAVE)
                d["id"] = "p2-special"
                d["status"] = "not-a-status"  # a schema error outside every move field
                mutate(d)
                code, lines = check(d, "p2-special")
                self.assertTrue(any(label in l for l in lines), f"{where}: {lines}")
                self.assertEqual(len(lines), len(set(lines)), f"a line reported twice: {lines}")

    def test_special_ex_that_passes_the_schema_but_fails_the_loader(self):
        # The clip is the only schema error: every level and the EX version must be run by the mirror.
        accept = {"decoy": 1, "wall_bounce": True}
        for target, value in (("decoy", 0), ("forward_travel", -1), ("multi_hit", {"hits": 1, "interval": 1}), ("wall_bounce", 1)):
            with self.subTest(target):
                d = load(WAVE)
                d["id"] = "p2-ex"
                d["ex"]["changes"] = [{"target": target, "op": "set", "value": value}]
                code, lines = check(d, "p2-ex")
                self.assertEqual(code, 1)
                self.assertTrue(lines, "the loader refuses it; the validator named nothing besides the clip")
        for target, value in accept.items():
            with self.subTest("ok-" + target):
                d = load(WAVE)
                d["id"] = "p2-ex-ok"
                d["ex"]["changes"] = [{"target": target, "op": "set", "value": value}]
                code, lines = check(d, "p2-ex-ok")
                self.assertEqual(lines, [])

    def test_throw_loader_mirror_with_the_clip_missing(self):
        # A throw's frame_data is throw.schema.json: no properties. The mirror still catches int32 in the new cancel_windows.
        d = load(THROW)
        d["id"] = "test-ryo-throw"
        d["frame_data"]["cancel_windows"] = [{"frames": [1, OUTSIDE], "on": ["hit"]}]
        code, lines = check(d, "test-ryo-throw")
        self.assertEqual(code, 1)
        self.assertTrue(any("int32" in l or "outside" in l for l in lines), lines)

    def test_throw_may_not_carry_pending_properties(self):
        d = load(THROW)
        d["frame_data"]["properties"] = {"decoy": 1}
        code, lines = check(d, "test-ryo-throw")
        self.assertTrue(any("properties" in l for l in lines), lines)


if __name__ == "__main__":
    unittest.main()
