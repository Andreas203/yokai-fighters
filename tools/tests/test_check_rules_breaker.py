"""Second breaker pass for tools/check_rules.py (YOK-35). Run: python3 -I -m unittest discover -s tools -p 'test_check_rules*.py'
Each probe overlays a temp folder on data/. The rule ID in each comment is where the expectation comes from."""
import copy, io, json, os, sys, tempfile, unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import check_rules  # noqa: E402

DATA = Path(__file__).resolve().parents[2] / "data"


def real(rel):
    return json.loads((DATA / rel).read_text(encoding="utf-8"))


def run(files, roots=None, setup=None):
    with tempfile.TemporaryDirectory() as tmp:
        for name, doc in files.items():
            f = Path(tmp) / name
            f.parent.mkdir(parents=True, exist_ok=True)
            if isinstance(doc, bytes):
                f.write_bytes(doc)
            else:
                f.write_text(doc if isinstance(doc, str) else json.dumps(doc), encoding="utf-8")
        if setup:
            setup(Path(tmp))
        out = io.StringIO()
        rs = [str(r) for r in (roots if roots is not None else [DATA])] + [tmp]
        code = check_rules.main(["--json", *rs], out=out)
        return code, json.loads(out.getvalue()) if out.getvalue().strip() else None


def hit(rep, rule, check=None):
    return [v for v in rep["violations"] if v["rule"] == rule and (check is None or v["check"] == check)]


def sp(**kw):
    d = real("moves/spirit-wave.json")
    d.update(kw)
    return d


def rule(i, frm, into, once=True):
    return {"kind": "cancel_rule", "id": i, "name": i, "source": "ryo", "rarity": "rare", "locked": False,
            "rules": ["X3"], "path": {"from": frm, "into": into, "on": ["hit"]}, "once_per_combo": once, "card": {}}


class CancelProbes(unittest.TestCase):
    def loop3(self, last_once):
        # X3: a 3-rule loop normals -> heavy_normals -> specials -> normals
        return {"cancels/a.json": rule("a", "normals", "heavy_normals"),
                "cancels/b.json": rule("b", "heavy_normals", "specials"),
                "cancels/c.json": rule("c", "specials", "normals", last_once)}

    def test_three_rule_cycle_with_one_rule_unbounded_fails_x3(self):
        code, rep = run(self.loop3(False))
        self.assertTrue([v for v in rep["violations"] if v["check"] == "cancels" and v["rule"] == "X3"], rep["violations"])

    def test_three_rule_cycle_all_once_is_left_to_harness(self):
        code, rep = run(self.loop3(True))
        self.assertFalse(hit(rep, "X3", "cancels"), rep["violations"])
        self.assertTrue([h for h in rep["harness_must_confirm"] if "cycle" in h["message"]])

    def test_once_per_combo_string_true_is_not_true(self):
        # X3: "true" is not the boolean
        code, rep = run({"cancels/a.json": rule("a", "specials", "dash", "true")})
        self.assertTrue(hit(rep, "X3", "cancels"))

    def test_four_cancel_rules_over_cap_across_files(self):
        # A11/X4: three cancel rules exist in the game; four fail
        files = {f"cancels/{n}/r.json": rule(n, "specials", "dash") for n in "abcd"}
        code, rep = run(files)
        self.assertTrue(hit(rep, "A11"))


class SchemeProbes(unittest.TestCase):
    def test_kata_only_special_fails_k2(self):
        d = sp(); del d["input"]["kihon"]
        code, rep = run({"moves/spirit-wave.json": d})
        self.assertTrue(hit(rep, "K2", "schemes"))

    def test_kihon_only_special_fails_k1(self):
        d = sp(); del d["input"]["kata"]
        code, rep = run({"moves/spirit-wave.json": d})
        self.assertTrue(hit(rep, "K1", "schemes"))

    def test_kata_slot_b_with_slot_a_motion_fails(self):
        # K1: slot B is 623
        d = sp(); d["input"]["kata"] = {"slot": "B", "motion": "236"}; d["input"]["kihon"] = {"slot": "B", "direction": "forward"}
        code, rep = run({"moves/spirit-wave.json": d})
        self.assertTrue(hit(rep, "K1", "schemes"))


class MeterProbes(unittest.TestCase):
    def test_ex_cost_50_and_200_fail(self):
        for cost in (0, 50, 99, 101, 200):
            d = sp(); d["ex"] = {"meter_cost": cost, "changes": []}
            code, rep = run({"moves/spirit-wave.json": d})
            self.assertTrue(hit(rep, "C5", "meter"), cost)

    def test_ex_cost_100_passes_and_changing_damage_in_ex_is_fine(self):
        # C5: EX costs 100; EX may change other things
        d = sp(); d["ex"] = {"meter_cost": 100, "changes": [{"target": "damage", "op": "mul_pct", "value": 150}]}
        code, rep = run({"moves/spirit-wave.json": d})
        self.assertFalse(hit(rep, "C5", "meter"), rep["violations"])

    def test_ex_block_on_normal_with_cost_fails(self):
        d = real("moves/ryo-light-punch.json"); d["ex"] = {"meter_cost": 100}
        code, rep = run({"moves/ryo-light-punch.json": d})
        self.assertTrue(hit(rep, "C5", "meter"))


    def test_ex_changes_nested_two_deep_meter_target(self):
        d = sp(); d["ex"] = {"meter_cost": 100, "changes": [{"group": {"inner": [{"target": "meter_cost", "op": "set", "value": 0}]}}]}
        code, rep = run({"moves/spirit-wave.json": d})
        self.assertTrue(hit(rep, "C5", "meter"))

    def test_modifier_spending_meter_fails(self):
        m = real("modifiers/fox-patience.json"); m["effects"] = [{"target": "meter_cost", "op": "set", "value": 0}]
        code, rep = run({"modifiers/fox-patience.json": m})
        self.assertTrue(hit(rep, "C5", "meter"))

    def test_throw_with_meter_cost_fails(self):
        d = real("moves/ryo-generic-throw.json"); d["meter_cost"] = 50
        code, rep = run({"moves/ryo-generic-throw.json": d})
        self.assertTrue(hit(rep, "C5", "meter"))


class LevelProbes(unittest.TestCase):
    def levels(self, keys):
        d = sp(); base = d["levels"]
        d["levels"] = {k: copy.deepcopy(base.get(k, base["3"])) for k in keys}
        return d

    def test_fourth_level_fails(self):
        code, rep = run({"moves/spirit-wave.json": self.levels(["1", "2", "3", "4"])})
        self.assertTrue(hit(rep, "A3", "levels"))

    def test_level_skip_1_3_fails(self):
        code, rep = run({"moves/spirit-wave.json": self.levels(["1", "3"])})
        self.assertTrue(hit(rep, "A3", "levels"))

    def test_level_zero_fails(self):
        code, rep = run({"moves/spirit-wave.json": self.levels(["0", "1", "2", "3"])})
        self.assertTrue(hit(rep, "A3", "levels"))

    def test_run_slot_level_4_and_0_fail(self):
        for lv in (0, 4, 2.0, True):
            run_doc = {"slots": [{"slot": "A", "special": "spirit-wave", "level": lv}]}
            code, rep = run({"run.json": run_doc})
            self.assertTrue(hit(rep, "A3", "levels"), lv)

    def test_lv2_tuning_multi_target_damage_and_nested_damage(self):
        d = sp(); d["levels"]["2"]["tuning"] = {"target": "projectile.damage", "op": "mul_pct", "value": 130}
        code, rep = run({"moves/spirit-wave.json": d})
        self.assertTrue(hit(rep, "E21"))   # nested damage-only step is still damage alone

    def test_lv2_damage_suffix_lookalike_not_damage(self):
        # E21 is about damage; `damage_taken` / `self.damage_reduction` are not damage-only steps
        d = sp(); d["levels"]["2"]["tuning"] = {"target": "self.damage_taken", "op": "mul_pct", "value": 80}
        code, rep = run({"moves/spirit-wave.json": d})
        self.assertFalse(hit(rep, "E21"), rep["violations"])

    def test_lv3_nested_changes_damage_only_fails(self):
        d = sp(); d["levels"]["3"]["evolution"]["changes"] = [{"all": [{"target": "damage", "op": "add", "value": 10}]}]
        code, rep = run({"moves/spirit-wave.json": d})
        self.assertTrue(hit(rep, "P1"))

    def test_lv3_changes_mixed_damage_and_other_passes(self):
        d = sp(); d["levels"]["3"]["evolution"]["changes"] = [{"target": "damage", "op": "add", "value": 10}, {"target": "speed", "op": "add", "value": 1}]
        code, rep = run({"moves/spirit-wave.json": d})
        self.assertFalse(hit(rep, "P1"), rep["violations"])


class ModifierProbes(unittest.TestCase):

    def test_two_modifiers_on_special_file(self):
        code, rep = run({"moves/spirit-wave.json": sp(modifiers=["fox-patience", "will-o-wisp"])})
        self.assertTrue(hit(rep, "A2"))

    def test_same_modifier_listed_twice_on_special_file(self):
        code, rep = run({"moves/spirit-wave.json": sp(modifiers=["fox-patience", "fox-patience"])})
        self.assertTrue(hit(rep, "A2"))

    def test_run_slot_modifier_string_plus_list(self):
        run_doc = {"slots": [{"slot": "A", "special": "spirit-wave", "modifier": "fox-patience", "modifiers": ["will-o-wisp"]}]}
        code, rep = run({"run.json": run_doc})
        self.assertTrue(hit(rep, "A2"))

    def test_one_modifier_in_run_passes(self):
        run_doc = {"slots": [{"slot": "A", "special": "spirit-wave", "modifier": "fox-patience"}]}
        code, rep = run({"run.json": run_doc})
        self.assertFalse(hit(rep, "A2"), rep["violations"])

    def test_same_special_slotted_in_two_run_files_each_with_a_modifier_is_ok(self):
        run_a = {"slots": [{"slot": "A", "special": "spirit-wave", "modifier": "fox-patience"}]}
        run_b = {"slots": [{"slot": "A", "special": "spirit-wave", "modifier": "will-o-wisp"}]}
        code, rep = run({"a/run.json": run_a, "b/run.json": run_b})
        self.assertFalse(hit(rep, "A2"), rep["violations"])  # two different runs


class SlotProbes(unittest.TestCase):
    def test_five_slots_and_duplicate_slot_letter(self):
        run_doc = {"slots": [{"slot": "ABCD"[i % 4], "special": "spirit-wave"} for i in range(5)]}
        code, rep = run({"run.json": run_doc})
        self.assertTrue(hit(rep, "A1"))

    def test_saved_run_with_empty_slots_list_passes(self):
        code, rep = run({"run.json": {"run": {"slots": []}}})
        self.assertFalse(hit(rep, "A1"), rep["violations"])

    def test_three_starters_fail(self):
        d = sp(); d["id"] = "extra-starter"; d["starter"] = True
        d["input"]["kata"]["slot"] = "D"; d["input"]["kata"]["motion"] = "22"
        d["input"]["kihon"] = {"slot": "D", "direction": "down"}
        code, rep = run({"moves/extra-starter.json": d})
        self.assertTrue(hit(rep, "A1"))

    def test_slot_e_fails(self):
        d = sp(); d["input"]["kata"]["slot"] = "E"; d["input"]["kihon"]["slot"] = "E"
        code, rep = run({"moves/spirit-wave.json": d})
        self.assertTrue(hit(rep, "A1"))


class TrialProbes(unittest.TestCase):
    def trial(self, step):
        t = real("trials/spirit-wave-trial.json"); t["goal"]["steps"] = [step]
        return {"trials/spirit-wave-trial.json": t}

    def test_step_demands_kata_motion(self):
        code, rep = run(self.trial({"event": "hit", "move": "spirit-wave", "motion": "236"}))
        self.assertTrue(hit(rep, "K2", "trials"))

    def test_step_kata_motion_nested_under_other_key(self):
        # K2: nested still demands a Kata input
        code, rep = run(self.trial({"event": "hit", "move": "spirit-wave", "require": {"motion": "236"}}))
        self.assertTrue(hit(rep, "K2", "trials"), rep["violations"])


    def test_trial_special_kata_only(self):
        d = sp(); del d["input"]["kihon"]
        files = {"moves/spirit-wave.json": d}
        code, rep = run(files)
        self.assertTrue(hit(rep, "K2", "trials"))   # spirit-wave-trial now needs a Kihon input it does not have

    def test_empty_steps_list(self):
        t = real("trials/spirit-wave-trial.json"); t["goal"]["steps"] = []
        code, rep = run({"trials/spirit-wave-trial.json": t})
        self.assertTrue(hit(rep, "R7"))

    def test_attempts_two_and_four_fail(self):
        for n in (2, 4, 0):
            t = real("trials/spirit-wave-trial.json"); t["attempts"] = n
            code, rep = run({"trials/spirit-wave-trial.json": t})
            self.assertTrue(hit(rep, "R7"), n)


class InputProbes(unittest.TestCase):
    def test_empty_data_dir_exits_2(self):
        with tempfile.TemporaryDirectory() as tmp:
            self.assertEqual(check_rules.main([tmp], out=io.StringIO()), 2)

    def test_only_skipped_dirs_exits_2(self):
        with tempfile.TemporaryDirectory() as tmp:
            (Path(tmp) / "schema").mkdir(); (Path(tmp) / "schema" / "x.json").write_text('{"kind":"special"}')
            self.assertEqual(check_rules.main([tmp], out=io.StringIO()), 2)

    def test_empty_json_object_files_only_exits_2(self):
        with tempfile.TemporaryDirectory() as tmp:
            (Path(tmp) / "a.json").write_text("{}")
            self.assertEqual(check_rules.main([tmp], out=io.StringIO()), 2)

    def test_non_utf8_fails_and_does_not_crash(self):
        code, rep = run({"moves/bad.json": b'{"kind":"special","id":"\xff\xfe"}'})
        self.assertEqual(code, 1)
        self.assertTrue([v for v in rep["violations"] if v["check"] == "read"])

    def test_invalid_json_and_trailing_comma(self):
        for body in ("{", '{"kind": "special",}', ""):
            code, rep = run({"moves/bad.json": body})
            self.assertEqual(code, 1, body)


    def test_duplicate_id_across_folders_same_root(self):
        a = sp(); b = sp()
        code, rep = run({"moves/a/spirit-wave.json": a, "moves/b/spirit-wave.json": b})
        self.assertTrue([v for v in rep["violations"] if "duplicate id" in v["message"]])

    def test_symlink_loop_terminates(self):
        def setup(tmp):
            (tmp / "loop").mkdir(); os.symlink(tmp, tmp / "loop" / "back")
        code, rep = run({"moves/x.json": sp()}, setup=setup)   # must return, not hang or traceback
        self.assertIn(code, (0, 1))

    def test_symlink_to_file_outside_root_duplicates_id(self):
        def setup(tmp):
            os.symlink(DATA / "moves" / "spirit-wave.json", tmp / "link.json")
        code, rep = run({}, setup=setup) if False else run({"moves/spirit-wave.json": sp()}, setup=setup)
        self.assertTrue([v for v in rep["violations"] if "duplicate id" in v["message"]], rep["violations"])

    def test_broken_symlink_is_reported_not_crash(self):
        def setup(tmp):
            os.symlink(tmp / "nowhere.json", tmp / "dead.json")
        code, rep = run({"moves/x.json": sp()}, setup=setup)
        self.assertTrue([v for v in rep["violations"] if v["check"] == "read"], rep["violations"])


class RecursiveKataKeyProbes(unittest.TestCase):
    """Round 4: the recursive Kata-only key search (K2) must not open holes or false positives."""
    def trial(self, step):
        t = real("trials/spirit-wave-trial.json"); t["goal"]["steps"] = [step]
        return {"trials/spirit-wave-trial.json": t}

    def test_motion_word_in_text_values_is_allowed(self):
        # K2 forbids Kata inputs, not the word: strings and a `notes` text are not keys
        code, rep = run(self.trial({"event": "hit", "move": "spirit-wave", "hint": "motion 236 is Kata only"}))
        self.assertFalse(hit(rep, "K2", "trials"), rep["violations"])

    def test_motion_inside_list_of_dicts_is_found(self):
        code, rep = run(self.trial({"event": "hit", "any_of": [{"move": "spirit-wave"}, {"motion": "236"}]}))
        self.assertTrue(hit(rep, "K2", "trials"), rep["violations"])

    def test_very_deep_nesting_reports_instead_of_passing(self):
        deep = cur = {}
        for _ in range(400):
            cur["x"] = {}; cur = cur["x"]
        cur["motion"] = "236"
        code, rep = run(self.trial({"event": "hit", "move": "spirit-wave", "a": deep}))
        self.assertTrue(hit(rep, "K2", "trials") or code == 1, (code, rep["violations"]))


if __name__ == "__main__":
    unittest.main()
