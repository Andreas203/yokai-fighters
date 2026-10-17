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

    def test_control_good_batch_small_no_build_is_incomplete(self):
        # designer 2026-10-17: fewer than 200 runs or --no-build is INCOMPLETE, never MERGEABLE
        code, text = self.run_harness(self.writes(self.good()))
        self.assertEqual(code, dmc.INCOMPLETE, text)

    def test_control_200_runs_with_build_is_mergeable(self):
        saved = dmc.prepare_godot
        dmc.prepare_godot = lambda *a, **k: True      # the build itself is stubbed; the flag is what is tested
        try:
            tmp, r = fake_root()
            with tmp, tempfile.TemporaryDirectory() as gd:
                g = fake_godot(gd, self.writes(self.good(200)))
                code, text = run_main("--root", str(r), "--godot", g)
        finally:
            dmc.prepare_godot = saved
        self.assertEqual(code, dmc.MERGEABLE, text)

    def test_199_runs_or_no_build_with_a_full_batch_is_incomplete(self):
        saved = dmc.prepare_godot
        dmc.prepare_godot = lambda *a, **k: True
        try:
            for extra, n in ((["--runs", "199"], 199), (["--no-build"], 200)):
                with self.subTest(extra=extra):
                    tmp, r = fake_root()
                    with tmp, tempfile.TemporaryDirectory() as gd:
                        g = fake_godot(gd, self.writes(self.good(n)))
                        code, text = run_main("--root", str(r), "--godot", g, *(["--runs", "200"] if n == 200 else []), *extra)
                    self.assertEqual(code, dmc.INCOMPLETE, text)
        finally:
            dmc.prepare_godot = saved

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


class Round3Probes(Stubbed, unittest.TestCase):
    def test_exit_5_is_not_mergeable_and_says_so(self):
        tmp, r = repo()
        with tmp:
            code, text = self.verdict(r)
        self.assertEqual(code, 5, text)
        self.assertNotIn(code, (dmc.MERGEABLE, dmc.REVIEW, dmc.INCOMPLETE))
        self.assertNotIn("Mergeable", text)
        self.assertNotIn("MERGEABLE", text)

    def test_exit_codes_are_distinct(self):
        codes = [dmc.MERGEABLE, dmc.FAIL, dmc.SETUP, dmc.REVIEW, dmc.INCOMPLETE, dmc.NOTHING]
        self.assertEqual(len(set(codes)), 6)

    def test_nothing_to_merge_with_only_subset_is_still_5_or_incomplete_never_0(self):
        tmp, r = repo()
        with tmp:
            code, text = self.verdict(r, "--only", "schema")
        self.assertNotEqual(code, dmc.MERGEABLE, text)

    def test_ignored_untracked_files_do_not_trigger_setup(self):
        tmp, r = repo()
        with tmp:
            (r / ".git/info/exclude").write_text("game/bin/\n*.log\n")
            write(r, "data/moves/b.json")
            commit(r)
            write(r, "game/bin/x.dll", "bin")
            write(r, "run.log", "log")
            code, text = self.verdict(r, "--runs", "200")
        self.assertEqual(code, dmc.MERGEABLE, text)

    def test_untracked_non_ignored_game_file_is_setup(self):
        tmp, r = repo()
        with tmp:
            write(r, "data/moves/b.json")
            commit(r)
            write(r, "game/new.cs", "x")
            code, text = self.verdict(r)
        self.assertEqual(code, dmc.SETUP, text)

    def test_staged_but_uncommitted_game_change_is_setup(self):
        tmp, r = repo()
        with tmp:
            write(r, "data/moves/b.json")
            commit(r)
            write(r, "game/tests/T.cs", "staged")
            git(r, "add", "game/tests/T.cs")
            code, text = self.verdict(r)
        self.assertEqual(code, dmc.SETUP, text)

    def test_unstaged_deletion_of_game_file_is_setup(self):
        tmp, r = repo()
        with tmp:
            write(r, "data/moves/b.json")
            commit(r)
            (r / "game/tests/T.cs").unlink()
            code, text = self.verdict(r)
        self.assertEqual(code, dmc.SETUP, text)

    def test_staged_rename_out_of_game_is_setup(self):
        tmp, r = repo()
        with tmp:
            write(r, "data/moves/b.json")
            commit(r)
            git(r, "mv", "game/tests/T.cs", "data/moves/T.json")
            code, text = self.verdict(r)
        self.assertEqual(code, dmc.SETUP, text)

    def test_odd_named_dirty_game_file_is_setup(self):
        tmp, r = repo()
        with tmp:
            write(r, "data/moves/b.json")
            commit(r)
            write(r, "game/we ird\nname.cs", "x")
            code, text = self.verdict(r)
        self.assertEqual(code, dmc.SETUP, text)

    def test_uncommitted_symlink_named_json_in_data_is_not_mergeable(self):
        # the dirty-tree rule lets uncommitted data/*.json through; a symlink there is code (YOK-34 scope)
        tmp, r = repo()
        with tmp:
            write(r, "data/moves/b.json")
            commit(r)
            os.symlink("../../game/tests/T.cs", r / "data/moves/evil.json")
            code, text = self.verdict(r)
        self.assertNotEqual(code, dmc.MERGEABLE, text)

    def test_dirty_schema_file_is_setup(self):
        tmp, r = repo()
        with tmp:
            write(r, "data/moves/b.json")
            commit(r)
            write(r, "data/schema/x.json")
            code, text = self.verdict(r)
        self.assertEqual(code, dmc.SETUP, text)

    def test_dirty_check_runs_before_nothing_to_merge(self):
        tmp, r = repo()
        with tmp:
            write(r, "game/new.cs", "x")
            code, text = self.verdict(r)
        self.assertEqual(code, dmc.SETUP, text)


class ToolsOptionProbes(unittest.TestCase):
    def test_roots_own_tools_cannot_override_tools_option(self):
        # a PR's tools/ that always passes must not win over --tools (the base's)
        tmp, r = fake_root()   # root/tools passes
        with tmp, tempfile.TemporaryDirectory() as bt:
            (Path(bt) / "validate_data.py").write_text("import sys;print('bad');sys.exit(1)")
            (Path(bt) / "check_rules.py").write_text((r / "tools/check_rules.py").read_text())
            code, text = run_main("--root", str(r), "--tools", bt, "--only", "schema,rules")
        self.assertEqual(code, dmc.FAIL, text)

    def test_validator_from_tools_gets_the_checked_roots_name(self):
        tmp, r = fake_root(validate="import os,sys;sys.exit(0 if os.environ.get('YOK_CONTENT_ROOT')==os.path.realpath(os.getcwd()) else 1)")
        with tmp:
            code, text = run_main("--root", str(r), "--only", "schema")
        self.assertEqual(code, dmc.INCOMPLETE, text)   # passes the step, skipped steps remain

    def test_inherited_content_root_env_is_overridden(self):
        tmp, r = fake_root(validate="import os,sys;sys.exit(0 if os.environ.get('YOK_CONTENT_ROOT')==os.path.realpath(os.getcwd()) else 1)")
        old = os.environ.get("YOK_CONTENT_ROOT")
        os.environ["YOK_CONTENT_ROOT"] = "/nonexistent-elsewhere"
        try:
            with tmp:
                code, text = run_main("--root", str(r), "--only", "schema")
        finally:
            if old is None:
                del os.environ["YOK_CONTENT_ROOT"]
            else:
                os.environ["YOK_CONTENT_ROOT"] = old
        self.assertEqual(code, dmc.INCOMPLETE, text)

    def test_missing_tools_dir_is_not_a_pass(self):
        tmp, r = fake_root()
        with tmp:
            code, text = run_main("--root", str(r), "--tools", str(r / "nope"), "--only", "schema,rules")
        self.assertIn(code, (dmc.FAIL, dmc.SETUP), text)


HERE = Path(__file__).resolve().parent


class RealValidatorRootProbes(unittest.TestCase):
    """validate_data.py with YOK_CONTENT_ROOT: the base's script must check the PR's data, not its own."""

    def _run(self, root, env_root):
        env = {k: v for k, v in os.environ.items() if k != "YOK_CONTENT_ROOT"}
        if env_root is not None:
            env["YOK_CONTENT_ROOT"] = str(env_root)
        return subprocess.run([sys.executable, str(HERE / "validate_data.py")], cwd=str(root), env=env,
                              capture_output=True, text=True)

    def setUp(self):
        try:
            import jsonschema  # noqa: F401
        except ImportError:
            self.skipTest("jsonschema not installed")
        self.tmp = tempfile.TemporaryDirectory(prefix="yok34b-vroot-")
        self.pr = Path(self.tmp.name)

    def tearDown(self):
        self.tmp.cleanup()

    def test_pr_with_broken_json_fails_even_though_script_dir_data_is_fine(self):
        import shutil
        shutil.copytree(HERE.parent / "data", self.pr / "data")
        (self.pr / "docs/design").mkdir(parents=True)
        shutil.copy(HERE.parent / "docs/design/rules.md", self.pr / "docs/design/rules.md")
        victim = next((self.pr / "data/moves").glob("*.json"))
        victim.write_text("{ not json")
        p = self._run(self.pr, self.pr)
        self.assertEqual(p.returncode, 1, p.stdout + p.stderr)

    def test_root_without_data_is_not_a_clean_pass(self):
        # YOK_CONTENT_ROOT naming a directory with no data/ must not exit 0 (it would validate nothing)
        p = self._run(self.pr, self.pr)
        self.assertNotEqual(p.returncode, 0, p.stdout + p.stderr)

    def test_nonexistent_content_root_is_not_a_pass(self):
        p = self._run(self.pr, self.pr / "gone")
        self.assertNotEqual(p.returncode, 0, p.stdout + p.stderr)


WORKFLOW = HERE.parent / ".github/workflows/data-merge-check.yml"


class WorkflowProbes(unittest.TestCase):
    def setUp(self):
        self.text = WORKFLOW.read_text()

    def run_blocks(self):
        """text of every `run:` step body (inline or block)."""
        out, lines, i = [], self.text.splitlines(), 0
        while i < len(lines):
            ln = lines[i]
            if ln.strip().startswith("run:"):
                ind = len(ln) - len(ln.lstrip())
                body = [ln.split("run:", 1)[1]]
                i += 1
                while i < len(lines) and (not lines[i].strip() or len(lines[i]) - len(lines[i].lstrip()) > ind):
                    body.append(lines[i])
                    i += 1
                out.append("\n".join(body))
                continue
            i += 1
        return out

    def test_no_attacker_controlled_expression_inside_a_run_block(self):
        import re
        bad = re.compile(r"\$\{\{\s*github\.(head_ref|event\.|actor|ref_name|base_ref)")
        for b in self.run_blocks():
            self.assertIsNone(bad.search(b), b)

    def test_pull_request_not_pull_request_target(self):
        self.assertNotIn("pull_request_target", self.text)

    def test_permissions_read_only(self):
        self.assertIn("contents: read", self.text)
        self.assertNotRegex(self.text, r"(?m)^\s*(contents|pull-requests|id-token|actions):\s*write")

    def test_every_action_pinned_to_a_sha(self):
        import re
        for m in re.finditer(r"uses:\s*(\S+)", self.text):
            self.assertRegex(m.group(1), r"@[0-9a-f]{40}$", m.group(1))

    def test_base_tools_are_extracted_outside_the_workspace_and_run_with_tools_flag(self):
        self.assertIn("git archive", self.text)
        self.assertIn("--tools", self.text)
        self.assertNotIn("cp -R .base", self.text)
        self.assertIn("RUNNER_TEMP/base", self.text)

    def test_archive_extract_cannot_escape_runner_temp_or_write_the_workspace(self):
        import re
        for b in self.run_blocks():
            for m in re.finditer(r"tar\s+[^\n|]*-C\s+(\S+)", b):
                self.assertTrue(m.group(1).strip('"').startswith("$RUNNER_TEMP/"), m.group(0))

    def test_code_exit_handling_never_maps_incomplete_or_setup_to_success(self):
        import re
        m = re.search(r"case \$code in(.*?)esac", self.text, re.S)
        self.assertIsNotNone(m)
        arms = m.group(1)
        for ok in re.findall(r"(?m)^\s*([0-9|]+)\)\s*echo", arms):
            self.assertTrue(set(ok.split("|")) <= {"0", "3", "5"}, ok)

    def test_pr_checker_cannot_report_mergeable(self):
        self.assertRegex(self.text, r'CHECKER" = pr \]\s*&&\s*\[ "\$code" = 0 \]')

    def test_base_ref_used_via_env_not_expression_in_git_commands(self):
        for b in self.run_blocks():
            if "git archive" in b:
                self.assertNotIn("${{", b)


class Round4Probes(Stubbed, unittest.TestCase):
    def dirty_verdict(self, setup):
        tmp, r = repo(("data/moves/a.json", "data/moves/real.json", "game/tests/T.cs"))
        with tmp:
            write(r, "data/moves/b.json")
            commit(r)
            setup(r)
            return self.verdict(r)

    def test_uncommitted_symlink_to_regular_data_file_is_setup(self):
        # a symlink is never content, even when it points at a real data file
        code, text = self.dirty_verdict(lambda r: os.symlink("real.json", r / "data/moves/link.json"))
        self.assertEqual(code, dmc.SETUP, text)

    def test_uncommitted_dangling_symlink_is_setup(self):
        code, text = self.dirty_verdict(lambda r: os.symlink("/nonexistent", r / "data/moves/dangling.json"))
        self.assertEqual(code, dmc.SETUP, text)

    def test_uncommitted_directory_named_json_is_setup(self):
        code, text = self.dirty_verdict(lambda r: write(r, "data/moves/dir.json/inner.cs", "x"))
        self.assertEqual(code, dmc.SETUP, text)

    def test_tracked_file_replaced_by_symlink_is_setup(self):
        def s(r):
            (r / "data/moves/real.json").unlink()
            os.symlink("../../game/tests/T.cs", r / "data/moves/real.json")
        code, text = self.dirty_verdict(s)
        self.assertEqual(code, dmc.SETUP, text)

    def test_dirty_content_edit_and_delete_stay_allowed(self):
        def s(r):
            write(r, "data/moves/real.json", '{"x": 1}')
            (r / "data/moves/a.json").unlink()
        code, text = self.dirty_verdict(s)
        self.assertNotEqual(code, dmc.SETUP, text)


class SummaryForgeryProbes(unittest.TestCase):
    def setUp(self):
        self.text = WORKFLOW.read_text()

    def check_step(self):
        i = self.text.index("data_merge_check.py\" --root")
        return self.text[max(0, i - 600):i + 600]

    def test_game_running_step_loses_every_runner_file_command(self):
        # the PR's game code runs in this step: GITHUB_ENV / GITHUB_PATH / GITHUB_OUTPUT / GITHUB_STATE let it
        # poison later steps exactly as GITHUB_STEP_SUMMARY lets it forge the summary
        block = self.check_step()
        for var in ("GITHUB_STEP_SUMMARY", "GITHUB_ENV", "GITHUB_PATH", "GITHUB_OUTPUT", "GITHUB_STATE"):
            self.assertIn(f"-u {var}", block, f"{var} still reachable by the PR's game code")

    def test_summary_step_does_not_trust_env_written_by_the_game_step(self):
        # OUT_DIR comes from $GITHUB_ENV, which the game step could rewrite (see above); the summary step
        # must not read a path from env the game step can change
        i = self.text.index("Job summary from the report")
        block = self.text[i:i + 700]
        self.assertNotIn("$OUT_DIR", block.replace("RUNNER_TEMP", ""), "summary reads OUT_DIR from GITHUB_ENV")

    def test_summary_step_always_runs_and_reads_only_the_report(self):
        i = self.text.index("Job summary from the report")
        block = self.text[i:i + 700]
        self.assertIn("always()", block)


if __name__ == "__main__":
    unittest.main()
