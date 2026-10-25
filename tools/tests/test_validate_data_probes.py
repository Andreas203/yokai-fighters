"""Test Breaker probes for YOK-105 (trial validator). Run: python3 -m unittest discover -s tools -p 'test_*.py'
Tests named KnownOpen_ pin behaviour the loader (game/scripts/Sim/Run/TrialData.cs) disagrees with: they pass today and
must be flipped when the validator is fixed."""
import io
import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import validate_data  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
FIXTURE_TRIAL = ROOT / "game" / "tests" / "fixtures" / "trials" / "test-spirit-wave-trial.json"
FIXTURE_THROW = ROOT / "game" / "tests" / "fixtures" / "throws" / "test-ryo-throw.json"
MOVES = ROOT / "data" / "moves"


def run_text(path, *extra):
    out = io.StringIO()
    code = validate_data.validate([str(path), *map(str, extra)], out=out)
    return code, "\n".join(l for l in out.getvalue().splitlines() if Path(path).name in l)


def write(tmp, text, name="test-spirit-wave-trial", folder=None):
    d = Path(tmp) / folder if folder else Path(tmp)
    d.mkdir(parents=True, exist_ok=True)
    p = d / f"{name}.json"
    p.write_text(text, encoding="utf-8")
    return p


def fixture_doc():
    return json.loads(FIXTURE_TRIAL.read_text(encoding="utf-8"))


class TrialValidatorProbes(unittest.TestCase):
    def test_KnownOpen_duplicate_key_validator_keeps_last_loader_refuses(self):
        # TrialProbes.KnownOpen_DuplicateKey_LoaderRefuses is the other half: the loader refuses all of these.
        text = FIXTURE_TRIAL.read_text(encoding="utf-8")
        cases = {
            "root attempts": text.replace('"attempts": 3,', '"attempts": 4, "attempts": 3,'),
            "setup distance": text.replace('"distance": 600,', '"distance": 5, "distance": 600,'),
            "dummy action": text.replace('"action": "jump"', '"action": "idle", "action": "jump"'),
            "step event": text.replace('{ "event": "hit", "move": "test-rising-talisman"', '{ "event": "block", "event": "hit", "move": "test-rising-talisman"'),
            "distance min": text.replace('"min": 400', '"min": 9999, "min": 400'),
        }
        for what, t in cases.items():
            with self.subTest(what), tempfile.TemporaryDirectory() as tmp:
                self.assertNotEqual(t, text, what)
                code, lines = run_text(write(tmp, t))
                self.assertEqual(code, 0, f"{what}: the validator reads the last duplicate and accepts (loader refuses): {lines}")

    def test_special_may_name_a_move_that_is_not_a_special(self):
        # Critic finding confirmed. R7: a trial is "for one of your specials". The schema only checks the id's shape.
        # The Python call passes the normal and throw files in the same run, as the validator has no other way to know kinds.
        for mv in ("ryo-generic-throw", "ryo-light-punch"):
            with self.subTest(mv), tempfile.TemporaryDirectory() as tmp:
                d = fixture_doc()
                d["special"] = mv
                d["goal"]["steps"][0]["move"] = mv if mv != "ryo-generic-throw" else "test-spirit-wave"
                if mv == "ryo-generic-throw":
                    d["goal"]["steps"][1] = {"event": "throw", "move": mv, "dummy_state": "grounded"}
                else:
                    d["goal"]["steps"][0]["move"] = mv
                p = write(tmp, json.dumps(d))
                code, lines = run_text(p, MOVES / f"{mv}.json")
                self.assertEqual(code, 0, f"KnownOpen: accepted although '{mv}' is not a special: {lines}")

    def test_KnownOpen_idle_dummy_with_airborne_hit_validates(self):
        # Reachable only by launching the idle dummy first; neither side rejects it. Pinned, see the report.
        d = fixture_doc()
        d["setup"]["dummy"] = {"action": "idle"}
        with tempfile.TemporaryDirectory() as tmp:
            code, lines = run_text(write(tmp, json.dumps(d)))
            self.assertEqual(code, 0, lines)

    def test_throw_step_naming_a_special_is_refused_when_the_kit_is_in_the_run(self):
        # The validator's half of the GDD River Grab question: a kind "special" move in a throw step is "not a throw".
        d = fixture_doc()
        d["goal"]["steps"][1] = {"event": "throw", "move": "spirit-wave", "dummy_state": "grounded"}
        with tempfile.TemporaryDirectory() as tmp:
            code, lines = run_text(write(tmp, json.dumps(d)), MOVES / "spirit-wave.json", ROOT / "data" / "clips" / "ryo-spirit-wave.json")
            self.assertIn("is not a throw", lines)

    def test_non_trial_placed_in_trials_folder_and_trial_placed_in_any_typed_folder_are_refused(self):
        for folder in ("moves", "modifiers", "cancels", "profiles", "clips", "story", "map"):
            with self.subTest(folder), tempfile.TemporaryDirectory() as tmp:
                code, lines = run_text(write(tmp, FIXTURE_TRIAL.read_text(encoding="utf-8"), folder=f"data/{folder}"))
                self.assertEqual(code, 1, f"trial in data/{folder}/ should be refused")
                self.assertIn("must be kind", lines)
        with tempfile.TemporaryDirectory() as tmp:
            d = fixture_doc()
            d["kind"] = "profile"
            code, lines = run_text(write(tmp, json.dumps(d), folder="data/trials"))
            self.assertEqual(code, 1)
            self.assertIn("files in data/trials/ must be kind 'trial'", lines)

    def test_every_existing_data_file_still_validates(self):
        out = io.StringIO()
        self.assertEqual(validate_data.validate([], out=out), 0, out.getvalue())
        # The file count grows with content (96 when written, already more on main): every file valid is the point.
        self.assertRegex(out.getvalue(), r"(\d+) file\(s\), \1 valid, 0 invalid")

    def test_whole_number_spellings_accepted_like_the_loader(self):
        text = FIXTURE_TRIAL.read_text(encoding="utf-8")
        for old, new in [('"distance": 600,', '"distance": 600.0,'), ('"attempt_frames": 600', '"attempt_frames": 6e2'),
                         ('"attempts": 3', '"attempts": 3.0'), ('"every": 90', '"every": 9E1'), ('"min": 400', '"min": 400.0')]:
            with self.subTest(new), tempfile.TemporaryDirectory() as tmp:
                code, lines = run_text(write(tmp, text.replace(old, new)))
                self.assertEqual(code, 0, lines)


if __name__ == "__main__":
    unittest.main()
