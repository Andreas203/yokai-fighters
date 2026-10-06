"""Tests for tools/validate_data.py. Run: python3 -m unittest discover -s tools -p 'test_*.py'"""
import io
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import validate_data  # noqa: E402

ROOT = Path(__file__).resolve().parent.parent
SAMPLES = ROOT / "data" / "samples"
FIXTURE_NORMALS = ROOT / "game" / "tests" / "fixtures" / "ryo-normals"
FIXTURE_THROWS = ROOT / "game" / "tests" / "fixtures" / "throws"


def run(*paths):
    out = io.StringIO()
    code = validate_data.validate([str(p) for p in paths], out=out)
    return code, out.getvalue()


class ValidateDataTest(unittest.TestCase):
    def test_default_run_over_data_passes(self):
        code, out = run()
        self.assertEqual(code, 0, out)
        self.assertNotIn("invalid/", out)

    def test_each_valid_sample_type_present_and_passes(self):
        code, out = run(SAMPLES)
        self.assertEqual(code, 0, out)
        self.assertIn("7 valid, 0 invalid", out)

    def test_malformed_special_fails_with_clear_messages(self):
        code, out = run(SAMPLES / "ryo-spirit-wave.json", SAMPLES / "invalid" / "broken-wave.json")
        self.assertEqual(code, 1)
        prefix = "data/samples/invalid/broken-wave.json: "
        self.assertIn(prefix + "input: 'kihon' is a required property", out)
        self.assertIn(prefix + "levels.1.frame_data.damage: 'sixty' is not of type 'integer'", out)
        self.assertIn(prefix + "rules.1: rule ID 'Z9' does not exist in docs/design/rules.md", out)
        self.assertIn(prefix + "levels.1.frame_data.hitboxes.0.frames: hitbox frames 20-22 fall outside 14-15", out)
        self.assertIn("1 valid, 1 invalid", out)

    def test_profile_reading_buttons_fails(self):
        code, out = run(SAMPLES / "invalid" / "oni-reads-buttons.json")
        self.assertEqual(code, 1)
        self.assertIn("behaviours.0.when: 'opponent_pressed_punch' is not one of", out)
        self.assertIn("temperament: 'boss' temperament is for the Tanuki only", out)

    def test_malformed_normal_fails_with_clear_messages(self):
        code, out = run(SAMPLES / "sample-light-punch-clip.json", SAMPLES / "invalid" / "broken-normal.json")
        self.assertEqual(code, 1)
        prefix = "data/samples/invalid/broken-normal.json: "
        self.assertIn(prefix + "input.button: 'XP' is not one of", out)
        self.assertIn(prefix + "input.directions.0: 0 is less than the minimum of 1", out)
        self.assertIn(prefix + "frame_data.strength: 'huge' is not one of", out)  # V2 (YOK-20)
        self.assertIn(prefix + "frame_data.hitboxes.0.frames: hitbox frames 8-9 fall outside 5-6", out)
        self.assertIn(prefix + "frame_data: startup+active+recovery = 14 but clip 'sample-light-punch-clip' has frames_total 13 (F2)", out)

    def test_normal_kind_allowed_in_moves_folder(self):
        self.assertIn("normal", validate_data.DIR_KIND["moves"])
        self.assertIn("special", validate_data.DIR_KIND["moves"])

    def test_ryo_normal_test_fixtures_match_schema_except_clip(self):
        # YOK-18 TEST FIXTURES: C8 numbers without a clip yet (F2). Everything but the clip must be valid,
        # so the C# loader reads the same format movesmith will write.
        code, out = run(FIXTURE_NORMALS)
        lines = [l for l in out.splitlines() if not l.startswith("validate_data:")]
        self.assertEqual(len(lines), 6, out)
        for line in lines:
            self.assertTrue(line.endswith("(root): 'clip' is a required property"), line)

    def test_throw_test_fixture_matches_schema_except_clip(self):
        # YOK-19 TEST FIXTURE: C4 numbers, grab clip still being retaken (YOK-31).
        self.assertIn("throw", validate_data.DIR_KIND["moves"])
        code, out = run(FIXTURE_THROWS)
        lines = [l for l in out.splitlines() if not l.startswith("validate_data:")]
        self.assertEqual(len(lines), 1, out)
        self.assertTrue(lines[0].endswith("(root): 'clip' is a required property"), lines[0])

    def test_throw_semantic_checks(self):
        import json
        doc = json.loads((FIXTURE_THROWS / "test-ryo-throw.json").read_text(encoding="utf-8"))
        doc["frame_data"]["throwboxes"][0]["frames"] = [4, 9]
        doc["input"]["buttons"] = ["LP", "LP"]
        errs = validate_data.semantic(doc, FIXTURE_THROWS / "test-ryo-throw.json", validate_data.load_rule_ids(), {})
        self.assertIn(("frame_data.throwboxes.0.frames", "throwbox frames 4-9 fall outside 6-7"), errs)
        v = validate_data.load_validators()["throw"]
        self.assertTrue(any("non-unique" in e.message for e in v.iter_errors(doc)))

    def test_explicit_invalid_folder_is_checked(self):
        code, out = run(SAMPLES / "invalid")
        self.assertEqual(code, 1)
        self.assertIn("3 file(s), 0 valid, 3 invalid", out)


if __name__ == "__main__":
    unittest.main()
