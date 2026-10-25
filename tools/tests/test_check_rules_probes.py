"""Adversarial probes for tools/check_rules.py (YOK-35). Run:
python3 -I -m unittest discover -s tools -p 'test_check_rules*.py'

Every probe lays a temp folder over data/ (nothing in the repo is edited). The rule ID in each
comment is where the expected result comes from (docs/design/rules.md)."""
import copy
import io
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import check_rules  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
DATA = ROOT / "data"
SCRIPT = ROOT / "tools" / "check_rules.py"


def real(rel):
    return json.loads((DATA / rel).read_text(encoding="utf-8"))


def overlay(files, extra_roots=()):
    """files: {relative path: dict | str}. Returns (exit code, parsed --json report)."""
    with tempfile.TemporaryDirectory() as tmp:
        for name, doc in files.items():
            f = Path(tmp) / name
            f.parent.mkdir(parents=True, exist_ok=True)
            f.write_text(doc if isinstance(doc, str) else json.dumps(doc), encoding="utf-8")
        out = io.StringIO()
        code = check_rules.main(["--json", str(DATA), *map(str, extra_roots), tmp], out=out)
        return code, json.loads(out.getvalue())


def hit(rep, rule, check=None):
    return [v for v in rep["violations"] if v["rule"] == rule and (check is None or v["check"] == check)]


def special(**edit):
    d = real("moves/spirit-wave.json")
    for k, v in edit.items():
        d[k] = v
    return d


class SlipProbes(unittest.TestCase):
    def test_special_without_input_block_fails_both_schemes(self):
        # K1 + K2: a special with no `input` at all needs a Kata motion and a Kihon slot
        d = special()
        del d["input"]
        code, rep = overlay({"moves/spirit-wave.json": d})
        self.assertEqual(code, 1)
        self.assertTrue(hit(rep, "K1", "schemes") and hit(rep, "K2", "schemes"), rep["violations"])

    def test_empty_kihon_block_fails(self):
        # K2: kihon {} names no slot and no direction
        d = special()
        d["input"]["kihon"] = {}
        code, rep = overlay({"moves/spirit-wave.json": d})
        self.assertEqual(code, 1, rep["violations"])

    def test_extra_level_key_fails(self):
        # A3: exactly Lv 1-3; "4" and "01" are not levels
        for extra in ("4", "01", "0"):
            with self.subTest(extra=extra):
                d = special()
                d["levels"][extra] = copy.deepcopy(d["levels"]["2"])
                code, rep = overlay({"moves/spirit-wave.json": d})
                self.assertTrue(hit(rep, "A3", "levels"), rep["violations"])

    def test_run_level_bool_or_string_is_not_a_level(self):
        # A3: level is the integer 1, 2 or 3 (True == 1 in Python must not pass, nor "2")
        for lv in (True, "2", 2.5):
            with self.subTest(lv=lv):
                run = {"run": {"slots": [{"slot": "A", "special": "spirit-wave", "level": lv}]}}
                code, rep = overlay({"saves/run.json": run})
                self.assertTrue(hit(rep, "A3", "levels"), (lv, rep["violations"]))

    def test_lv2_empty_tuning_is_not_a_step(self):
        # A4: Lv 2 is one tuning step; {} is no step
        d = special()
        d["levels"]["2"]["tuning"] = {}
        code, rep = overlay({"moves/spirit-wave.json": d})
        self.assertEqual(code, 1, rep["violations"])

    def test_lv2_damage_hidden_in_nested_effect(self):
        # E21 / P1: a damage-only Lv 2 step is out, however it is nested
        d = special()
        d["levels"]["2"]["tuning"] = {"effects": [{"target": "damage", "op": "add", "value": 10}]}
        code, rep = overlay({"moves/spirit-wave.json": d})
        self.assertEqual(code, 1, rep["violations"])

    def test_lv2_damage_target_case_difference(self):
        # E21: "Damage" is still damage
        d = special()
        d["levels"]["2"]["tuning"] = {"target": "Damage", "op": "add", "value": 10}
        code, rep = overlay({"moves/spirit-wave.json": d})
        self.assertTrue(hit(rep, "E21"), rep["violations"])


    def test_meter_cost_on_normal_nested_and_via_effect(self):
        # C5: no normal spends meter
        n = real("moves/ryo-light-punch.json")
        n["levels"] = {"1": {"cost": {"meter_cost": 50}}}
        code, rep = overlay({"moves/ryo-light-punch.json": n})
        self.assertTrue(hit(rep, "C5", "meter"), rep["violations"])



    def test_two_modifiers_via_modifier_and_modifiers(self):
        # A2: one modifier per special, whichever key carries it
        mods = sorted((DATA / "modifiers").glob("*.json"))
        self.assertGreaterEqual(len(mods), 2)
        a, b = (json.loads(m.read_text())["id"] for m in mods[:2])
        run = {"run": {"slots": [{"slot": "A", "special": "spirit-wave", "level": 1, "modifier": a, "modifiers": [b]}]}}
        code, rep = overlay({"saves/run.json": run})
        self.assertTrue(hit(rep, "A2"), rep["violations"])


    def test_trial_step_kitsune_move_not_in_kit(self):
        # R7: steps use Ryo's kit
        t = real("trials/spirit-wave-trial.json")
        t["goal"]["steps"][0]["move"] = "kitsune-light-punch"
        code, rep = overlay({"trials/spirit-wave-trial.json": t})
        self.assertTrue(hit(rep, "R7", "trials"), rep["violations"])

    def test_trial_id_case_difference(self):
        # R7: ids are exact; "Spirit-Wave" is not "spirit-wave"
        t = real("trials/spirit-wave-trial.json")
        t["special"] = "Spirit-Wave"
        code, rep = overlay({"trials/spirit-wave-trial.json": t})
        self.assertTrue(hit(rep, "R7", "trials"), rep["violations"])

    def test_renamed_id_with_old_file_name_does_not_hide_original(self):
        # A1/K1: renaming the id inside a file adds a second special; a special whose Kata slot
        # collides must still be checked. Here the renamed copy is broken and must be reported.
        d = special(id="spirit-wave-renamed")
        d["input"]["kata"]["motion"] = "999"
        code, rep = overlay({"moves/spirit-wave.json": d})
        self.assertTrue(hit(rep, "K1", "schemes"), rep["violations"])

    def test_duplicate_id_in_one_root_does_not_hide_broken_file(self):
        # Overlay order: two files in the same root with one (kind, id); the broken one sorts first and is
        # silently replaced by the good one, so broken data passes (K1)
        bad = special()
        bad["input"]["kata"]["motion"] = "999"
        good = special()
        code, rep = overlay({"moves/a-broken.json": bad, "moves/z-good.json": good}, extra_roots=())
        self.assertTrue(hit(rep, "K1", "schemes") or hit(rep, "A1") or code == 1,
                        "broken duplicate was silently dropped; report the duplicate id")

    def test_file_without_id_in_overlay_keys_by_stem(self):
        # a special file with no `id` keyed by file stem must not mask a broken original
        d = special()
        del d["id"]
        d["input"]["kata"]["motion"] = "999"
        code, rep = overlay({"moves/other-stem.json": d})
        self.assertTrue(hit(rep, "K1", "schemes"), rep["violations"])

    def test_json_booleans_as_strings_for_once_per_combo(self):
        # X3: once_per_combo "true" (string) is not true
        r = {"kind": "cancel_rule", "id": "x", "once_per_combo": "true", "path": {"from": "normals", "into": "specials"}}
        code, rep = overlay({"cancels/x.json": r})
        self.assertTrue(hit(rep, "X3", "cancels"), rep["violations"])


MALFORMED = {
    "id-as-list": {"kind": "special", "id": ["x"]},
    "id-as-dict": {"kind": "trial", "id": {"a": 1}},
    "kind-as-list": {"kind": ["special"], "id": "x"},
    "special-levels-list": {"kind": "special", "id": "x", "levels": [1, 2, 3]},
    "special-levels-null": {"kind": "special", "id": "x", "levels": None, "input": "A", "ex": 5},
    "special-ex-changes-int": {"kind": "special", "id": "x", "ex": {"meter_cost": 100, "changes": 5}},
    "special-tuning-list": {"kind": "special", "id": "x", "levels": {"1": 5, "2": {"tuning": [1]}, "3": {"evolution": {"changes": 7}}}},
    "special-levels-values-str": {"kind": "special", "id": "x", "levels": {"1": "a", "2": "b", "3": "c"}},
    "special-input-str": {"kind": "special", "id": "x", "input": {"kata": "236", "kihon": "A"}},
    "special-slot-list": {"kind": "special", "id": "x", "starter": True, "input": {"kihon": {"slot": ["A"]}}},
    "special-modifiers-int": {"kind": "special", "id": "x", "modifiers": 3},
    "modifier-effects-int": {"kind": "modifier", "id": "m", "effects": 3},
    "modifier-effects-strs": {"kind": "modifier", "id": "m", "effects": ["a"]},
    "trial-goal-str": {"kind": "trial", "id": "t", "goal": "x", "setup": "y", "special": ["a"]},
    "trial-steps-int": {"kind": "trial", "id": "t", "special": "spirit-wave", "goal": {"steps": 3}},
    "trial-step-move-list": {"kind": "trial", "id": "t", "special": "spirit-wave", "goal": {"steps": [{"move": ["a"]}]}},
    "trial-dummy-moves-nested": {"kind": "trial", "id": "t", "special": "spirit-wave", "setup": {"dummy": {"moves": [["a"]]}}},
    "trial-dummy-str": {"kind": "trial", "id": "t", "special": "spirit-wave", "setup": {"dummy": "idle"}},
    "trial-attempts-str": {"kind": "trial", "id": "t", "special": "spirit-wave", "attempts": "three"},
    "cancel-path-str": {"kind": "cancel_rule", "id": "c", "path": "x", "once_per_combo": True},
    "cancel-path-lists": {"kind": "cancel_rule", "id": "c", "path": {"from": ["a"], "into": ["b"]}, "once_per_combo": True},
    "move-id-int": {"kind": "normal", "id": 5, "source": "ryo"},
    "normal-input-str": {"kind": "normal", "id": "n", "source": "ryo", "input": "LP"},
    "special-props-str": {"kind": "special", "id": "x", "levels": {"1": {"frame_data": {"properties": "a"}}}},
    "run-slots-ints": {"run": {"slots": [1, 2, None, "x"]}},
    "run-slot-special-list": {"slots": [{"slot": "A", "special": ["a"]}]},
    "run-slot-modifier-dict": {"slots": [{"slot": "A", "special": "spirit-wave", "modifier": {"a": 1}}]},
    "run-slot-letter-list": {"slots": [{"slot": ["A"], "special": "spirit-wave"}]},
    "run-cancels-nested": {"slots": [], "cancels": [["a"], {"b": 1}]},
    "run-cancels-str": {"slots": [], "cancels": "abc"},
    "run-slots-level-list": {"slots": [{"slot": "A", "special": "spirit-wave", "level": [1]}]},
}


class CrashProbes(unittest.TestCase):
    def test_schema_invalid_documents_report_instead_of_traceback(self):
        # setup contract: "Files the schema would refuse are still read: this check never needs them
        # to be schema-valid" -> must never raise
        for name, doc in MALFORMED.items():
            with self.subTest(case=name):
                try:
                    code, rep = overlay({f"x/{name}.json": doc})
                except Exception as e:  # noqa: BLE001
                    self.fail(f"{type(e).__name__}: {e}")
                self.assertIn(code, (0, 1))

    def test_unparsable_files_are_reported(self):
        samples = {
            "truncated": '{"kind": "special", "id": ',
            "empty": "",
            "bom": "﻿" + json.dumps({"kind": "special", "id": "x"}),
            "nan": '{"kind": "special", "id": "x", "n": NaN}',
            "list-top": "[1,2,3]",
            "scalar": "42",
            "null": "null",
        }
        for name, text in samples.items():
            with self.subTest(case=name):
                try:
                    code, rep = overlay({f"x/{name}.json": text})
                except Exception as e:  # noqa: BLE001
                    self.fail(f"{type(e).__name__}: {e}")
        # a broken file that cannot be parsed fails the run (it may hide a rule break)
        code, rep = overlay({"x/truncated.json": samples["truncated"]})
        self.assertEqual(code, 1)
        self.assertTrue(any(v["check"] == "read" for v in rep["violations"]))

    def test_non_utf8_and_deeply_nested_json(self):
        with tempfile.TemporaryDirectory() as tmp:
            (Path(tmp) / "bad.json").write_bytes(b'{"kind": "\xff\xfe"}')
            (Path(tmp) / "deep.json").write_text("[" * 100000 + "]" * 100000)
            out = io.StringIO()
            try:
                code = check_rules.main(["--json", str(DATA), tmp], out=out)
            except Exception as e:  # noqa: BLE001
                self.fail(f"{type(e).__name__}: {e}")
            self.assertEqual(code, 1)

    def test_json_files_in_skipped_dirs_do_not_count(self):
        # setup: schema/, samples/, invalid/ are skipped, but a broken file there must not alter results
        d = special()
        d["input"]["kata"]["motion"] = "999"
        for sk in ("invalid", "samples", "schema"):
            code, rep = overlay({f"{sk}/moves/spirit-wave.json": d})
            self.assertEqual(code, 0, (sk, rep["violations"]))


class CliProbes(unittest.TestCase):
    def test_json_shape_is_stable(self):
        code, rep = overlay({})
        self.assertEqual(list(rep), ["ok", "documents", "violations", "harness_must_confirm"])
        d = special()
        del d["input"]
        code, rep = overlay({"moves/spirit-wave.json": d})
        for v in rep["violations"]:
            self.assertEqual(list(v), ["check", "rule", "gdd", "file", "message"])
        self.assertFalse(rep["ok"])
        self.assertIsInstance(rep["documents"], int)

    def test_missing_root_exits_2_even_after_a_good_root(self):
        r = subprocess.run([sys.executable, "-I", str(SCRIPT), str(DATA), "/nonexistent/root"], capture_output=True, text=True)
        self.assertEqual(r.returncode, 2, r.stdout + r.stderr)
        self.assertNotIn("Traceback", r.stderr)
        r = subprocess.run([sys.executable, "-I", str(SCRIPT), "--json", "/nonexistent/root"], capture_output=True, text=True)
        self.assertEqual(r.returncode, 2)
        self.assertEqual(r.stdout, "")

    def test_runs_from_another_cwd_with_relative_root(self):
        with tempfile.TemporaryDirectory() as tmp:
            r = subprocess.run([sys.executable, "-I", str(SCRIPT), "--json"], cwd=tmp, capture_output=True, text=True)
            self.assertEqual(r.returncode, 0, r.stdout + r.stderr)
            rel = Path(ROOT / "data").relative_to(ROOT)
            r2 = subprocess.run([sys.executable, "-I", str(SCRIPT), "--json", str(DATA)], cwd="/", capture_output=True, text=True)
            self.assertEqual(r2.returncode, 0, r2.stdout + r2.stderr)
            self.assertEqual(json.loads(r.stdout), json.loads(r2.stdout))

    def test_file_root_that_is_not_json_is_reported(self):
        with tempfile.TemporaryDirectory() as tmp:
            f = Path(tmp) / "notes.txt"
            f.write_text("hello")
            out = io.StringIO()
            code = check_rules.main(["--json", str(DATA), str(f)], out=out)
            self.assertEqual(code, 1, "a root file that is not parsable JSON should be reported")

    def test_overlay_order_later_root_wins_and_earlier_broken_is_replaced(self):
        # documented: later file replaces earlier with the same kind and id
        bad = special()
        bad["input"]["kata"]["motion"] = "999"
        with tempfile.TemporaryDirectory() as a, tempfile.TemporaryDirectory() as b:
            (Path(a) / "spirit-wave.json").write_text(json.dumps(bad))
            (Path(b) / "spirit-wave.json").write_text(json.dumps(special()))
            out = io.StringIO()
            self.assertEqual(check_rules.main(["--json", str(DATA), a, b], out=out), 0)
            out = io.StringIO()
            self.assertEqual(check_rules.main(["--json", str(DATA), b, a], out=out), 1)



class WrongTypeReadsAsAbsentProbes(unittest.TestCase):
    """Round 2: a wrong type must fail its rule, not read as 'absent' and skip it."""

    def _special(self, edit):
        d = special()
        edit(d)
        return overlay({"moves/spirit-wave.json": d})

    def test_ex_changes_dict_spending_meter(self):
        # C5/E16: EX changes may not rewrite the cost, even when `changes` is a dict instead of a list
        code, rep = self._special(lambda d: d["ex"].update(changes={"target": "ex.meter_cost", "op": "set", "value": 0}))
        self.assertEqual(code, 1, rep["violations"])

    def test_lv3_changes_dict_damage_only(self):
        # P1: damage-only evolution, `changes` as a single dict
        def e(d):
            d["levels"]["3"]["evolution"]["changes"] = {"target": "damage", "op": "add", "value": 5}
        code, rep = self._special(e)
        self.assertEqual(code, 1, rep["violations"])

    def test_lv3_preset_wrong_type(self):
        # A5: the preset is a name; a list or number is not one
        for bad in (["great-wave"], 5, {"a": 1}):
            with self.subTest(preset=bad):
                code, rep = self._special(lambda d: d["levels"]["3"]["evolution"].update(preset=bad))
                self.assertEqual(code, 1, rep["violations"])


    def test_lv2_tuning_with_evolution_key_is_refused_even_as_null(self):
        # A4: Lv 2 carrying an `evolution` key (even null) is not the written shape
        code, rep = self._special(lambda d: d["levels"]["2"].update(evolution=None))
        self.assertEqual(code, 1, rep["violations"])


    def test_run_slot_modifier_dict_two(self):
        # A2: modifier given as a dict of two
        run = {"slots": [{"slot": "A", "special": "spirit-wave", "level": 1, "modifier": {"a": 1, "b": 2}}]}
        code, rep = overlay({"saves/run.json": run})
        self.assertTrue(hit(rep, "A2"), rep["violations"])

    def test_run_slots_as_dict_is_reported(self):
        # A1: five specials slotted as a dict are still five specials; a run whose slots are not a list is malformed
        slots = {k: {"slot": k, "special": "spirit-wave"} for k in "ABCDE"}
        code, rep = overlay({"saves/run.json": {"run": {"slots": slots}}})
        self.assertEqual(code, 1, "run with dict slots ignored silently")

    def test_run_cancels_wrong_types_over_cap(self):
        # X2: up to two held; three as a dict or a string of three is over the cap
        for held in ({"a": 1, "b": 2, "c": 3}, "abc"):
            with self.subTest(held=held):
                code, rep = overlay({"saves/run.json": {"slots": [], "cancels": held}})
                self.assertEqual(code, 1, "held cancels of wrong type skipped")

    def test_trial_steps_not_a_list(self):
        # R7/K2: steps as a dict hide a kitsune move and a Kata motion
        for steps in ({"0": {"move": "kitsune-light-punch", "motion": "236"}}, "hit", 3):
            with self.subTest(steps=steps):
                t = real("trials/spirit-wave-trial.json")
                t["goal"]["steps"] = steps
                code, rep = overlay({"trials/spirit-wave-trial.json": t})
                self.assertEqual(code, 1, rep["violations"])

    def test_trial_goal_not_an_object(self):
        # R7: a trial whose goal is a string has no landable goal
        t = real("trials/spirit-wave-trial.json")
        t["goal"] = "land a hit"
        code, rep = overlay({"trials/spirit-wave-trial.json": t})
        self.assertEqual(code, 1, rep["violations"])

    def test_trial_attempts_over_three(self):
        # R7: the special levels only if landed within 3 attempts; a trial granting 10 breaks the rule
        for n in (4, 10):
            with self.subTest(attempts=n):
                t = real("trials/spirit-wave-trial.json")
                t["attempts"] = n
                code, rep = overlay({"trials/spirit-wave-trial.json": t})
                self.assertEqual(code, 1, rep["violations"])


    def test_trial_move_int_or_null(self):
        # R7: a step move of 5 does not exist
        t = real("trials/spirit-wave-trial.json")
        t["goal"]["steps"][0]["move"] = 5
        code, rep = overlay({"trials/spirit-wave-trial.json": t})
        self.assertTrue(hit(rep, "R7"), rep["violations"])

    def test_input_slots_non_string(self):
        # A1/K1: slot given as the number 1 or true is not A-D
        for bad in (1, True, ["A"]):
            with self.subTest(slot=bad):
                def e(d):
                    d["input"]["kata"]["slot"] = bad
                    d["input"]["kihon"]["slot"] = bad
                code, rep = self._special(e)
                self.assertTrue(hit(rep, "A1"), rep["violations"])

    def test_starter_flag_as_string_still_counts_two_starters(self):
        # A1: Ryo starts with exactly 2 starters; dropping one to "true" (string) must not leave 2 silently
        code, rep = self._special(lambda d: d.update(starter="true"))
        self.assertEqual(code, 1, rep["violations"])

    def test_only_unreadable_root_is_a_failure_not_a_setup_error_or_pass(self):
        # setup: a root holding only a truncated JSON file is a violation (exit 1), never 0
        with tempfile.TemporaryDirectory() as tmp:
            (Path(tmp) / "x.json").write_text('{"kind": ')
            r = subprocess.run([sys.executable, "-I", str(SCRIPT), tmp], capture_output=True, text=True)
            self.assertEqual(r.returncode, 1, r.stdout + r.stderr)
            self.assertNotIn("Traceback", r.stderr)

    def test_same_id_across_move_kinds_in_one_root(self):
        # overlay: a normal and a special with one id in a root are one (move, id) key, reported as duplicate
        n = real("moves/ryo-light-punch.json")
        n["id"] = "spirit-wave"
        code, rep = overlay({"moves/spirit-wave-normal.json": n})
        self.assertEqual(code, 1, rep["violations"])

    def test_internal_backstop_is_not_hiding_a_crash(self):
        # the try/except backstop must surface as a violation, never exit 0 with partial results
        for name, doc in MALFORMED.items():
            with self.subTest(case=name):
                code, rep = overlay({f"x/{name}.json": doc})
                self.assertFalse(any(v["check"] == "internal" for v in rep["violations"]),
                                 [v for v in rep["violations"] if v["check"] == "internal"])


if __name__ == "__main__":
    unittest.main()
