"""Tests for tools/check_rules.py (YOK-35). Run: python3 -I -m unittest discover -s tools -p 'test_check_rules.py'

Each folder in tools/tests/rules_fixtures/ is laid over data/ (later files replace earlier ones with the
same kind and id), so every broken sample is caught on top of otherwise real content."""
import io
import json
import subprocess
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import check_rules  # noqa: E402

ROOT = Path(__file__).resolve().parent.parent
DATA = ROOT / "data"
FIX = ROOT / "tools" / "tests" / "rules_fixtures"


def run(*roots, as_json=False):
    out = io.StringIO()
    argv = (["--json"] if as_json else []) + [str(r) for r in roots]
    code = check_rules.main(argv, out=out)
    return code, out.getvalue()


def violations(*roots):
    code, out = run(*roots, as_json=True)
    return code, json.loads(out)


def rules_hit(fixture):
    code, rep = violations(DATA, FIX / fixture)
    return code, [(v["rule"], v["check"], v["message"]) for v in rep["violations"]], rep


class CurrentDataTest(unittest.TestCase):
    def test_data_on_main_passes(self):
        code, out = run()
        self.assertEqual(code, 0, out)
        self.assertIn(" 0 violation(s)", out)

    def test_every_trial_is_left_for_the_harness(self):
        code, rep = violations(DATA)
        trials = sorted(p.stem for p in (DATA / "trials").glob("*.json"))
        confirmed = sorted(h["message"].split("'")[1] for h in rep["harness_must_confirm"] if h["check"] == "trials")
        self.assertEqual(trials, confirmed)
        self.assertTrue(rep["ok"])

    def test_text_output_names_file_rule_and_gdd_section(self):
        code, out = run(DATA, FIX / "missing-kihon")
        self.assertEqual(code, 1)
        self.assertIn("tools/tests/rules_fixtures/missing-kihon/moves/spirit-wave.json: K2 [schemes] no Kihon input", out)
        self.assertIn("(GDD 3.3 Control schemes)", out)

    def test_runs_isolated_from_the_command_line(self):
        ok = subprocess.run([sys.executable, "-I", str(ROOT / "tools" / "check_rules.py")], capture_output=True, text=True)
        self.assertEqual(ok.returncode, 0, ok.stdout + ok.stderr)
        bad = subprocess.run([sys.executable, "-I", str(ROOT / "tools" / "check_rules.py"), "--json",
                              str(DATA), str(FIX / "cancel-loop")], capture_output=True, text=True)
        self.assertEqual(bad.returncode, 1)
        self.assertFalse(json.loads(bad.stdout)["ok"])

    def test_missing_root_is_a_setup_error(self):
        self.assertEqual(check_rules.main([str(ROOT / "no-such-folder")], out=io.StringIO()), 2)

    def test_every_cited_rule_exists_and_has_a_source(self):
        text = (ROOT / "docs" / "design" / "rules.md").read_text(encoding="utf-8")
        for rid, where in check_rules.GDD.items():
            self.assertIn(f"- **{rid}**", text, rid)
            self.assertTrue(where, rid)


class BrokenSamplesTest(unittest.TestCase):
    def assertCaught(self, fixture, rule, check, fragment):
        code, hits, rep = rules_hit(fixture)
        self.assertEqual(code, 1, rep)
        self.assertTrue(any(r == rule and c == check and fragment in m for r, c, m in hits),
                        f"{fixture}: expected {rule} [{check}] '{fragment}', got {hits}")
        for v in rep["violations"]:
            self.assertTrue(v["gdd"], v)

    def test_cancel_loop_without_once_per_combo(self):
        self.assertCaught("cancel-loop", "X3", "cancels", "cancel loop via ['spirit-chain']")
        self.assertCaught("cancel-loop", "X3", "cancels", "['spirit-chain'] not once per combo")
        _, hits, _ = rules_hit("cancel-loop")
        self.assertFalse(any("fox-step" in m for _, _, m in hits), "Fox Step alone ends in a dash and cannot loop")

    def test_rule_not_once_per_combo(self):
        self.assertCaught("cancel-not-once", "X3", "cancels", "once_per_combo must be true")

    def test_cancel_caps(self):
        self.assertCaught("cancel-cap", "A11", "cancel-cap", "4 cancel rules in data")
        self.assertCaught("cancel-cap", "X2", "cancel-cap", "3 cancel rules held")

    def test_the_three_x4_rules_together_do_not_loop(self):
        _, hits, _ = rules_hit("cancel-cap")
        self.assertFalse([h for h in hits if h[0] == "X3"], hits)

    def test_missing_kihon_slot(self):
        self.assertCaught("missing-kihon", "K2", "schemes", "no Kihon input")
        self.assertCaught("missing-kihon", "K2", "trials", "trial special 'spirit-wave' has no Kihon input")

    def test_wrong_motion_and_split_slots(self):
        self.assertCaught("wrong-scheme", "K1", "schemes", "Kata slot B is motion 623")
        self.assertCaught("wrong-scheme", "K1", "schemes", "Kata slot B and Kihon slot C differ")

    def test_two_modifiers_on_one_special(self):
        self.assertCaught("two-modifiers", "A2", "modifiers", "slots[0]: 'spirit-wave' carries 2 modifiers")
        self.assertCaught("two-modifiers", "A2", "modifiers", "special carries 2 modifiers")

    def test_slot_limits(self):
        self.assertCaught("slot-overflow", "A1", "slots", "5 specials slotted")
        self.assertCaught("slot-overflow", "A1", "slots", "slot 'E' is not one of A-D")
        self.assertCaught("slot-overflow", "A3", "slots", "'foxfire' is slotted twice")
        self.assertCaught("slot-overflow", "A3", "levels", "level 4")
        self.assertCaught("slot-overflow", "A1", "slots", "3 starter special(s)")
        self.assertCaught("slot-overflow", "A1", "slots", "share slot A")

    def test_three_levels(self):
        self.assertCaught("levels", "A3", "levels", "levels ['1', '2']")
        self.assertCaught("levels", "E21", "levels", "changes damage alone")
        self.assertCaught("levels", "A5", "levels", "no visual preset")

    def test_meter_for_ex_only(self):
        self.assertCaught("meter", "C5", "meter", "meter_cost: only a special's EX (C5) or the burst (C6) spends meter")
        self.assertCaught("meter", "C5", "meter", "ex.meter_cost 200")
        self.assertCaught("meter", "C5", "meter", "target 'meter_cost' spends meter outside the EX")

    def test_trial_not_kihon_completable(self):
        self.assertCaught("trial-broken", "K2", "trials", "goal.steps.0.motion")
        self.assertCaught("trial-broken", "R7", "trials", "'kitsune-light-punch' is a kitsune move")
        self.assertCaught("trial-broken", "R7", "trials", "move 'ghost-palm' does not exist")
        self.assertCaught("trial-broken", "R7", "trials", "trial special 'fox-mirage' is not a special")

    def test_every_fixture_is_tested(self):
        broken = {"cancel-loop", "cancel-not-once", "cancel-cap", "missing-kihon", "wrong-scheme",
                  "two-modifiers", "slot-overflow", "levels", "meter", "trial-broken"}
        self.assertEqual(broken | PASSING, {p.name for p in FIX.iterdir() if p.is_dir()})
        for name in broken:
            self.assertEqual(rules_hit(name)[0], 1, name)
        for name in PASSING:
            self.assertEqual(rules_hit(name)[0], 0, name)


PASSING = {"cancel-cycle-bounded", "river-grab-x4", "ninefold-spark"}


class PassingSamplesTest(unittest.TestCase):
    def test_cycle_bounded_by_once_per_combo_is_left_for_the_harness(self):
        code, hits, rep = rules_hit("cancel-cycle-bounded")
        self.assertEqual(code, 0, hits)
        self.assertTrue(any(h["rule"] == "X3" and "cancel cycle via ['spirit-chain']" in h["message"]
                            for h in rep["harness_must_confirm"]), rep["harness_must_confirm"])

    def test_river_grab_with_the_three_x4_rules_is_no_loop(self):
        # The game's SuccessfulThrows is IsThrow (throw input only), so a command grab is just a special (C4 open).
        code, hits, rep = rules_hit("river-grab-x4")
        self.assertEqual(code, 0, hits)
        self.assertTrue(any(h["rule"] == "C4" and "'river-grab' is a command grab" in h["message"]
                            for h in rep["harness_must_confirm"]))
        self.assertFalse(any("cancel cycle" in h["message"] for h in rep["harness_must_confirm"]))

    def test_ninefold_spark_style_ex_cost_modifier_passes(self):
        code, hits, _ = rules_hit("ninefold-spark")
        self.assertEqual(code, 0, hits)


class GraphTest(unittest.TestCase):
    def test_find_loops_two_hop_and_self(self):
        g = {"a": {"b": ["r1"]}, "b": {"a": ["r2"], "dash": ["r3"]}, "c": {"c": ["r4"]}, "dash": {}}
        loops = check_rules.find_loops(g)
        self.assertEqual(loops, [(["a", "b"], ["a", "b"], ["r1", "r2"]), (["c"], ["c"], ["r4"])])

    def test_acyclic_graph_has_no_loop(self):
        g = {"hp": {"wave": ["oni-chain"]}, "wave": {"dash": ["fox-step"]}, "throw": {"wave": ["undertow"]}, "dash": {}}
        self.assertEqual(check_rules.find_loops(g), [])

    def test_heavy_normals_are_hp_and_hk_only(self):
        self.assertIn("heavy_normals", check_rules.move_classes({"kind": "normal", "source": "ryo", "input": {"button": "HK"}}))
        self.assertNotIn("heavy_normals", check_rules.move_classes({"kind": "normal", "source": "ryo", "input": {"button": "MP"}}))
        self.assertEqual(check_rules.move_classes({"kind": "normal", "source": "kitsune", "input": {"button": "HP"}}), set())


if __name__ == "__main__":
    unittest.main()
