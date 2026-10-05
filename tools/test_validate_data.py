"""Tests for tools/validate_data.py. Run: python3 -m unittest discover -s tools -p 'test_*.py'"""
import io
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import validate_data  # noqa: E402

ROOT = Path(__file__).resolve().parent.parent
SAMPLES = ROOT / "data" / "samples"


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
        self.assertIn("5 valid, 0 invalid", out)

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

    def test_explicit_invalid_folder_is_checked(self):
        code, out = run(SAMPLES / "invalid")
        self.assertEqual(code, 1)
        self.assertIn("2 file(s), 0 valid, 2 invalid", out)


if __name__ == "__main__":
    unittest.main()
