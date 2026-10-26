"""Test Breaker probes for YOK-212 (validate_data.py and the later_loss schema). Run: python3 -m unittest discover -s tools -p 'test_*.py'
Expected verdicts: validate_data.py's header ("a file the game would refuse to load fails here first"), the schema text of
later_loss.yokai ("a yokai id, never an elder's") and S3. LaterLossBreakerProbes.cs holds the game side."""
import io
import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import validate_data  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]


def verdict(later_loss_json: str, extra: str = ""):
    """Run the validator on a story card whose later_loss is the raw JSON text given (floats and exponents kept as written)."""
    doc = {"kind": "story_card", "id": "fixture-probe", "status": "proposed", "trigger": "TEST FIXTURE", "text": "{yokai} stands.",
           "placeholders": ["yokai"], "speaker": "narrator", "rules": ["S3"]}
    text = json.dumps(doc)[:-1] + ', "later_loss": ' + later_loss_json + extra + "}"
    with tempfile.TemporaryDirectory() as tmp:
        path = Path(tmp) / "data" / "story" / "fixture-probe.json"
        path.parent.mkdir(parents=True)
        path.write_text(text, encoding="utf-8")
        out = io.StringIO()
        code = validate_data.validate([str(path)], out=out)
    return code, out.getvalue()


class LaterLossValidatorProbes(unittest.TestCase):
    def test_numbers_the_game_cannot_read_are_refused(self):
        # C# StoryLibrary.ParseLaterLoss uses TryGetInt32: 4.0, 1e1 and 3000000000 all throw FormatException and take the whole library down.
        for what, ll in {
            "row_min 4.0": '{"row_min": 4.0, "row_max": 6}',
            "row_max 1e1": '{"row_min": 1, "row_max": 1e1}',
            "row_max over Int32": '{"row_min": 1, "row_max": 3000000000}',
        }.items():
            with self.subTest(what):
                code, out = verdict(ll)
                self.assertEqual(code, 1, f"{what}: validate_data passes it, the game cannot load it\n{out}")

    def test_row_order_is_checked_for_floats_too(self):
        # S3 + the author's own rule "row_min <= row_max": the isinstance(int) test skips 9.0 > 3, so it passes.
        code, out = verdict('{"row_min": 9.0, "row_max": 3}')
        self.assertEqual(code, 1, out)

    def test_a_yokai_id_is_never_an_elders(self):
        # Schema text: "a yokai id, never an elder's" and the ticket: the winner is always the plain yokai, so these cards can never be shown.
        for yokai in ("elder-oni", "elder-kappa"):
            with self.subTest(yokai):
                code, out = verdict('{"yokai": "%s", "row_min": 1, "row_max": 3}' % yokai)
                self.assertEqual(code, 1, f"{yokai}: validate_data passes a card that can never match\n{out}")

    def test_controls_the_validator_and_loader_agree_on(self):
        # Agreement, not findings: these must keep the verdicts the C# loader also gives (LaterLossBreakerProbes / LoseCardTests).
        for ll, ok in (('{"row_min": 1, "row_max": 1}', True), ('{"yokai": "oni", "row_min": 2, "row_max": 9}', True),
                       ('{"row_min": "4", "row_max": 6}', False), ('{"row_min": true, "row_max": 6}', False),
                       ('{"yokai": null, "row_min": 1, "row_max": 6}', False), ('{"yokai": "", "row_min": 1, "row_max": 6}', False),
                       ('{"row_min": 0, "row_max": 6}', False), ('{"row_min": 7, "row_max": 6}', False), ('null', False), ('[1, 3]', False)):
            with self.subTest(ll):
                code, out = verdict(ll)
                self.assertEqual(code == 0, ok, out)


if __name__ == "__main__":
    unittest.main()
