"""Test Breaker probes (YOK-179): the harness step accepts a 'stalled' outcome and still rejects anything else."""
import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import data_merge_check as dmc  # noqa: E402


def run_step(outcomes):
    runs = [{"seed": i, "outcome": o} for i, o in enumerate(outcomes)]
    batch = {"bot": "b", "runs_played": len(runs), "runs": runs, "summary": {"fights_stalled": 1}}
    saved = dmc.run

    def fake(cmd, cwd=None):
        out = Path(cmd[cmd.index("--out") + 1])
        (out / "b.json").write_text(json.dumps(batch), encoding="utf-8")
        return 0, ""
    dmc.run = fake
    try:
        st = dmc.Step("harness", "h")
        with tempfile.TemporaryDirectory() as d:
            dmc.step_harness(Path(d), st, "godot", len(runs), 1)
    finally:
        dmc.run = saved
    return st


class StalledOutcome(unittest.TestCase):
    def test_stalled_accepted(self):
        st = run_step(["won", "lost", "stalled"])
        self.assertEqual(st.status, "pass", st.details)
        self.assertIn("fights stalled 1", st.notes[0])

    def test_bogus_outcomes_rejected(self):
        for bad in ("timeout", "Stalled", "", None, "stall", "won "):
            with self.subTest(bad=bad):
                st = run_step(["won", bad])
                self.assertEqual(st.status, "fail")
                self.assertIn("won/lost/stalled", " ".join(st.details))


if __name__ == "__main__":
    unittest.main()
