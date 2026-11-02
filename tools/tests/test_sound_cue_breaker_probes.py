"""YOK-93 Test Breaker probes: the validator and HitCueLibrary must agree on the shared case list
(game/tests/fixtures/sound-probe/cases.json; the C# side is HitSoundCueBreakerProbes.LoaderAndCaseList_*).
Each case's expectation comes from the sound-cue schema and V6, not from either implementation."""
import io
import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import validate_data  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
CASES = ROOT / "game" / "tests" / "fixtures" / "sound-probe" / "cases.json"


def run(*paths):
    out = io.StringIO()
    code = validate_data.validate([str(p) for p in paths], out=out)
    return code, out.getvalue()


class SoundCueCaseListTest(unittest.TestCase):
    def test_validator_agrees_with_the_case_list(self):
        cases = json.loads(CASES.read_text(encoding="utf-8"))["cases"]
        self.assertGreaterEqual(len(cases), 90)
        wrong = []
        for c in cases:
            with tempfile.TemporaryDirectory() as tmp:
                paths = []
                for stem, body in c["files"].items():
                    p = Path(tmp) / f"{stem}.json"
                    p.write_text(body if isinstance(body, str) else json.dumps(body), encoding="utf-8")
                    paths.append(p)
                code, out = run(*paths)
                if (code == 0) != c["ok"]:
                    wrong.append(f"{c['name']}: expected {'accept' if c['ok'] else 'refuse'} ({c['why']}), validator exit {code}")
        self.assertEqual(wrong, [], "\n" + "\n".join(wrong))


if __name__ == "__main__":
    unittest.main()
