"""Test Breaker probes for tools/data_merge_check.py (YOK-34). Run: python3 -I -m unittest tools.test_data_merge_check_breaker
Real temp git repos decide the scope; the four steps are stubbed unless a probe says otherwise."""
import io
import json
import os
import stat
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import data_merge_check as dmc  # noqa: E402

GIT = ["git", "-c", "user.name=t", "-c", "user.email=t@example.invalid", "-c", "commit.gpgsign=false"]


def git(r, *a, check=True):
    return subprocess.run(GIT + ["-C", str(r), *a], check=check, capture_output=True, text=True)


def write(r, rel, text="{}\n"):
    p = Path(r) / rel
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_text(text)


def repo(base_files=("data/moves/a.json", "game/tests/T.cs")):
    tmp = tempfile.TemporaryDirectory(prefix="yok34b-")
    r = Path(tmp.name)
    git(r, "init", "-q", "-b", "main")
    for f in base_files:
        write(r, f)
    git(r, "add", "-A")
    git(r, "commit", "-q", "-m", "base")
    git(r, "switch", "-q", "-c", "pr")
    return tmp, r


def commit(r, msg="pr"):
    git(r, "add", "-A")
    git(r, "commit", "-q", "-m", msg)


class Stubbed:
    """Steps stubbed to pass; changed_files is the real one."""
    steps = ("step_schema", "step_rules", "step_harness", "step_suite", "find_godot", "prepare_godot")

    def setUp(self):
        self._saved = {n: getattr(dmc, n) for n in self.steps}
        ok = lambda root, st, *a: setattr(st, "status", "pass")
        dmc.step_schema = dmc.step_rules = ok
        dmc.step_harness = dmc.step_suite = ok
        dmc.find_godot = lambda arg: "godot"
        dmc.prepare_godot = lambda *a, **k: True

    def tearDown(self):
        for n, v in self._saved.items():
            setattr(dmc, n, v)

    def verdict(self, r, *args, base="main"):
        out = io.StringIO()
        argv = ["--root", str(r), *(["--base", base] if base else []), *args]
        return dmc.main(argv, out=out), out.getvalue()


class ScopeProbes(Stubbed, unittest.TestCase):
    def test_pure_delete_of_game_test_is_review(self):
        # ticket/CLAUDE.md: only data/*.json is data-only; removing a test is a code change
        tmp, r = repo()
        with tmp:
            (r / "game/tests/T.cs").unlink()
            commit(r)
            code, text = self.verdict(r)
        self.assertEqual(code, dmc.REVIEW, text)
        self.assertIn("game/tests/T.cs", text)

    def test_delete_of_data_json_alone_is_mergeable(self):
        tmp, r = repo()
        with tmp:
            (r / "data/moves/a.json").unlink()
            commit(r)
            code, text = self.verdict(r)
        self.assertEqual(code, dmc.MERGEABLE, text)

    def test_odd_filenames_under_data_are_content_and_game_ones_are_code(self):
        names = ["data/moves/sp ace.json", "data/moves/new\nline.json", "data/moves/妖怪.json", 'data/moves/q"uote.json']
        tmp, r = repo()
        with tmp:
            for n in names:
                write(r, n)
            commit(r)
            code, text = self.verdict(r)
            self.assertEqual(code, dmc.MERGEABLE, text)
            write(r, "game/we ird\nnameé.cs", "x")
            commit(r)
            code, text = self.verdict(r)
        self.assertEqual(code, dmc.REVIEW, text)

    def test_odd_game_filename_listed_whole(self):
        tmp, r = repo()
        with tmp:
            write(r, "game/we ird\nnameé.cs", "x")
            commit(r)
            self.assertEqual(dmc.changed_files(r, "main"), ["game/we ird\nnameé.cs"])

    def test_traversal_path_is_not_content(self):
        # a path that climbs out of data/ is not content under data/ (ticket: JSON file under data/)
        content, code = dmc.classify(["data/../game/x.json", "data/moves/../../tools/x.json"])
        self.assertEqual(content, [], content)

    def test_backslash_name_at_repo_root_is_not_content(self):
        # a file literally named 'data\\evil.json' at the repo root lives outside data/ on POSIX
        tmp, r = repo()
        with tmp:
            write(r, "data\\evil.json")
            commit(r)
            code, text = self.verdict(r)
        self.assertEqual(code, dmc.REVIEW, text)

    def test_symlink_named_json_under_data_pointing_into_game_is_review(self):
        # a symlink is not a JSON file with content: it makes the loader read a code file as content
        tmp, r = repo()
        with tmp:
            os.symlink("../../game/tests/T.cs", r / "data/moves/evil.json")
            commit(r)
            code, text = self.verdict(r)
        self.assertEqual(code, dmc.REVIEW, text)

    def test_symlinked_directory_under_data_is_review(self):
        tmp, r = repo()
        with tmp:
            os.symlink("../game", r / "data/link")
            commit(r)
            code, text = self.verdict(r)
        self.assertEqual(code, dmc.REVIEW, text)

    def test_schema_dir_file_is_review_even_json(self):
        tmp, r = repo()
        with tmp:
            write(r, "data/schema/new.schema.json")
            commit(r)
            code, text = self.verdict(r)
        self.assertEqual(code, dmc.REVIEW, text)

    def test_uppercase_json_extension_is_review(self):
        tmp, r = repo()
        with tmp:
            write(r, "data/moves/B.JSON")
            commit(r)
            code, text = self.verdict(r)
        self.assertEqual(code, dmc.REVIEW, text)

    def test_data_change_plus_whitespace_only_game_change_is_review(self):
        tmp, r = repo()
        with tmp:
            write(r, "data/moves/b.json")
            write(r, "game/tests/T.cs", "{}\n\n   \n")
            commit(r)
            code, text = self.verdict(r)
        self.assertEqual(code, dmc.REVIEW, text)

    def test_mode_only_change_in_game_is_review(self):
        tmp, r = repo()
        with tmp:
            p = r / "game/tests/T.cs"
            p.chmod(p.stat().st_mode | stat.S_IXUSR)
            write(r, "data/moves/b.json")
            commit(r)
            code, text = self.verdict(r)
        self.assertEqual(code, dmc.REVIEW, text)

    def test_merge_commit_bringing_main_game_change_is_not_blamed_on_pr(self):
        tmp, r = repo()
        with tmp:
            write(r, "data/moves/b.json")
            commit(r)
            git(r, "switch", "-q", "main")
            write(r, "game/tests/T.cs", "changed on main")
            commit(r, "main moves")
            git(r, "switch", "-q", "pr")
            git(r, "merge", "-q", "--no-ff", "-m", "merge main", "main")
            code, text = self.verdict(r)
        self.assertEqual(code, dmc.MERGEABLE, text)

    def test_merge_commit_with_evil_resolution_in_game_is_review(self):
        tmp, r = repo()
        with tmp:
            write(r, "data/moves/b.json")
            commit(r)
            git(r, "switch", "-q", "main")
            write(r, "data/moves/c.json")
            commit(r, "main moves")
            git(r, "switch", "-q", "pr")
            git(r, "merge", "-q", "--no-ff", "--no-commit", "main")
            write(r, "game/tests/T.cs", "sneaked into the merge")
            git(r, "add", "-A")
            git(r, "commit", "-q", "-m", "merge main")
            code, text = self.verdict(r)
        self.assertEqual(code, dmc.REVIEW, text)

    def test_missing_ref_is_setup(self):
        tmp, r = repo()
        with tmp:
            code, text = self.verdict(r, base="nope")
        self.assertEqual(code, dmc.SETUP, text)

    def test_unrelated_history_is_setup(self):
        tmp, r = repo()
        with tmp:
            git(r, "switch", "-q", "--orphan", "other")
            git(r, "rm", "-rqf", ".", check=False)
            write(r, "data/moves/z.json")
            commit(r, "orphan")
            code, text = self.verdict(r)
        self.assertEqual(code, dmc.SETUP, text)

    def test_empty_diff_is_not_a_merge_verdict(self):
        # DESIGNER QUESTION: ticket silent on an empty PR. Reading: nothing to merge is not MERGEABLE.
        tmp, r = repo()
        with tmp:
            code, text = self.verdict(r)
        self.assertNotEqual(code, dmc.MERGEABLE, text)

    def test_uncommitted_game_change_does_not_pass_as_data_only(self):
        # the steps run on the working tree but the scope reads HEAD: a dirty game/ file is unseen.
        # DESIGNER QUESTION (CI is a clean checkout); expected REVIEW or SETUP, never MERGEABLE.
        tmp, r = repo()
        with tmp:
            write(r, "data/moves/b.json")
            commit(r)
            write(r, "game/tests/T.cs", "dirty, uncommitted")
            code, text = self.verdict(r)
        self.assertNotEqual(code, dmc.MERGEABLE, text)


class OnlyProbes(Stubbed, unittest.TestCase):
    def test_only_subsets_are_never_mergeable(self):
        tmp, r = repo()
        with tmp:
            write(r, "data/moves/b.json")
            commit(r)
            for only in ("schema", "schema,rules", "rules,harness,suite", "", "schema,schema,rules,harness"):
                with self.subTest(only=only):
                    code, text = self.verdict(r, "--only", only)
                    self.assertEqual(code, dmc.INCOMPLETE, text)

    def test_only_subset_with_code_change_is_still_not_mergeable(self):
        tmp, r = repo()
        with tmp:
            write(r, "game/x.cs", "x")
            commit(r)
            code, text = self.verdict(r, "--only", "schema")
        self.assertNotEqual(code, dmc.MERGEABLE, text)

    def test_one_run_batch_is_not_a_mergeable_verdict(self):
        # DESIGNER QUESTION: ticket says a 200-run batch; --runs 1 --no-build still reaches MERGEABLE.
        tmp, r = repo()
        with tmp:
            write(r, "data/moves/b.json")
            commit(r)
            code, text = self.verdict(r, "--runs", "1", "--no-build")
        self.assertNotEqual(code, dmc.MERGEABLE, text)


def fake_root(validate="print('ok')", rules=None):
    tmp = tempfile.TemporaryDirectory(prefix="yok34b-root-")
    r = Path(tmp.name)
    (r / "tools").mkdir()
    (r / "data").mkdir()
    (r / "game").mkdir()
    (r / "tools/validate_data.py").write_text(validate)
    (r / "tools/check_rules.py").write_text(rules if rules is not None else
                                            "import json;print(json.dumps({'violations':[],'harness_must_confirm':[]}))")
    return tmp, r


def run_main(*argv):
    out = io.StringIO()
    try:
        return dmc.main(list(argv), out=out), out.getvalue()
    except Exception as e:  # an unhandled crash is reported as code -1, never a verdict
        return -1, f"CRASH {type(e).__name__}: {e}"


class StepProbes(unittest.TestCase):
    def test_validator_crash_is_fail_or_setup(self):
        tmp, r = fake_root(validate="raise SystemExit(Exception('boom'))")
        with tmp:
            code, text = run_main("--root", str(r), "--only", "schema,rules,harness,suite")
        self.assertIn(code, (dmc.FAIL, dmc.SETUP), text)

    def test_validator_killed_by_signal_is_fail_or_setup(self):
        tmp, r = fake_root(validate="import os,signal;os.kill(os.getpid(), signal.SIGKILL)")
        with tmp:
            code, text = run_main("--root", str(r), "--only", "schema")
        self.assertIn(code, (dmc.FAIL, dmc.SETUP), text)

    def test_check_rules_junk_on_stdout_is_not_a_pass(self):
        tmp, r = fake_root(rules="print('hello'); print('{')")
        with tmp:
            code, text = run_main("--root", str(r), "--only", "schema,rules")
        self.assertIn(code, (dmc.FAIL, dmc.SETUP), text)

    def test_check_rules_junk_before_json_is_not_a_pass(self):
        tmp, r = fake_root(rules="import json;print('warning: x');print(json.dumps({'violations':[]}))")
        with tmp:
            code, text = run_main("--root", str(r), "--only", "schema,rules")
        self.assertIn(code, (dmc.FAIL, dmc.SETUP), text)

    def test_check_rules_non_object_json_does_not_crash_the_checker(self):
        for body in ("[]", "null", "3"):
            with self.subTest(body=body):
                tmp, r = fake_root(rules=f"print({body!r})")
                with tmp:
                    code, text = run_main("--root", str(r), "--only", "schema,rules")
                self.assertIn(code, (dmc.FAIL, dmc.SETUP), text)

    def test_check_rules_exit_zero_with_violations_is_not_a_pass(self):
        # YOK-35: violations are failures; exit 0 with a violation listed contradicts itself
        rules = ("import json;print(json.dumps({'violations':[{'file':'a','rule':'R7','check':'c','message':'m'}],"
                 "'harness_must_confirm':[]}))")
        tmp, r = fake_root(rules=rules)
        with tmp:
            code, text = run_main("--root", str(r), "--only", "schema,rules", "--json")
        self.assertEqual({s["step"]: s["status"] for s in json.loads(text)["steps"]}["rules"], "fail", text)

    def test_check_rules_exit_one_with_no_violations_is_still_fail(self):
        tmp, r = fake_root(rules="import json,sys;print(json.dumps({'violations':[]}));sys.exit(1)")
        with tmp:
            code, text = run_main("--root", str(r), "--only", "schema,rules")
        self.assertEqual(code, dmc.FAIL, text)


FAKE_GODOT = """#!/bin/sh
case "$*" in
*run_harness*)
  while [ $# -gt 0 ]; do [ "$1" = "--out" ] && OUT="$2"; shift; done
  %s ;;
*) echo "TESTS 5 passed, 0 failed"; exit 0 ;;
esac
"""


def fake_godot(tmp, harness_sh):
    p = Path(tmp) / "godot"
    p.write_text(FAKE_GODOT % harness_sh)
    p.chmod(0o755)
    return str(p)


class HarnessProbes(unittest.TestCase):
    def run_harness(self, sh, runs=3):
        tmp, r = fake_root()
        with tmp, tempfile.TemporaryDirectory() as gd:
            g = fake_godot(gd, sh)
            return run_main("--root", str(r), "--no-build", "--godot", g, "--runs", str(runs),
                            "--only", "schema,rules,harness,suite")

    @staticmethod
    def writes(obj, exit_code=0):
        return f"echo '{json.dumps(obj)}' > \"$OUT/b.json\"; exit {exit_code}"

    def good(self, n=3, **over):
        b = {"bot": "kihon", "runs_played": n, "runs": [{"seed": i, "outcome": "won"} for i in range(n)], "summary": {}}
        b.update(over)
        return b

    def test_control_good_batch_is_mergeable_with_all_steps(self):
        code, text = self.run_harness(self.writes(self.good()))
        self.assertEqual(code, dmc.MERGEABLE, text)

    def test_run_with_no_result_fails(self):
        b = self.good()
        del b["runs"][1]["outcome"]
        code, text = self.run_harness(self.writes(b))
        self.assertEqual(code, dmc.FAIL, text)

    def test_run_with_null_outcome_or_unknown_outcome_fails(self):
        for o in (None, "crashed", "", "WON"):
            with self.subTest(o=o):
                b = self.good()
                b["runs"][0]["outcome"] = o
                code, text = self.run_harness(self.writes(b))
                self.assertEqual(code, dmc.FAIL, text)

    def test_run_entry_not_an_object_does_not_crash_or_pass(self):
        b = self.good()
        b["runs"][0] = None
        code, text = self.run_harness(self.writes(b))
        self.assertIn(code, (dmc.FAIL, dmc.SETUP), text)

    def test_batch_that_is_a_json_list_does_not_crash_or_pass(self):
        code, text = self.run_harness(self.writes([1, 2]))
        self.assertIn(code, (dmc.FAIL, dmc.SETUP), text)

    def test_fewer_runs_than_asked_fails(self):
        code, text = self.run_harness(self.writes(self.good(2)), runs=3)
        self.assertEqual(code, dmc.FAIL, text)

    def test_runs_played_lies_about_recorded_runs(self):
        b = self.good(3, runs_played=3)
        b["runs"] = b["runs"][:2] + [{"seed": 9, "outcome": "won"}] * 0
        code, text = self.run_harness(self.writes(b))
        self.assertEqual(code, dmc.FAIL, text)

    def test_harness_exit_nonzero_with_complete_results_fails(self):
        code, text = self.run_harness(self.writes(self.good(), exit_code=1))
        self.assertEqual(code, dmc.FAIL, text)

    def test_harness_exit_zero_with_no_file_fails(self):
        code, text = self.run_harness("exit 0")
        self.assertEqual(code, dmc.FAIL, text)

    def test_harness_empty_file_fails(self):
        code, text = self.run_harness(': > "$OUT/b.json"; exit 0')
        self.assertEqual(code, dmc.FAIL, text)

    def test_second_result_file_with_a_lost_data_run_is_not_ignored(self):
        # first file sorted wins; an extra result file must not hide a bad one. DESIGNER QUESTION on
        # whether the harness may write several files; expected: the check does not pass on a bad extra.
        good, bad = self.good(), self.good()
        bad["runs"][0]["outcome"] = None
        sh = (f"echo '{json.dumps(good)}' > \"$OUT/a.json\"; echo '{json.dumps(bad)}' > \"$OUT/b.json\"; exit 0")
        code, text = self.run_harness(sh)
        self.assertNotEqual(code, dmc.MERGEABLE, text)

    def test_suite_failure_line_fails_even_with_exit_zero(self):
        tmp, r = fake_root()
        with tmp, tempfile.TemporaryDirectory() as gd:
            p = Path(gd) / "godot"
            p.write_text("#!/bin/sh\ncase \"$*\" in *run_harness*) while [ $# -gt 0 ]; do [ \"$1\" = --out ] && O=\"$2\"; shift; done;"
                         f" echo '{json.dumps(self.good())}' > \"$O/b.json\";; *) echo 'FAIL X: no'; echo 'TESTS 4 passed, 1 failed';; esac\n")
            p.chmod(0o755)
            code, text = run_main("--root", str(r), "--no-build", "--godot", str(p), "--runs", "3")
        self.assertEqual(code, dmc.FAIL, text)

    def test_suite_with_no_totals_line_fails(self):
        tmp, r = fake_root()
        with tmp, tempfile.TemporaryDirectory() as gd:
            p = Path(gd) / "godot"
            p.write_text("#!/bin/sh\ncase \"$*\" in *run_harness*) while [ $# -gt 0 ]; do [ \"$1\" = --out ] && O=\"$2\"; shift; done;"
                         f" echo '{json.dumps(self.good())}' > \"$O/b.json\";; *) echo hi;; esac\n")
            p.chmod(0o755)
            code, text = run_main("--root", str(r), "--no-build", "--godot", str(p), "--runs", "3")
        self.assertEqual(code, dmc.FAIL, text)


if __name__ == "__main__":
    unittest.main()
