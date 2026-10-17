"""Tests for tools/data_merge_check.py (YOK-34). Run: python3 -m unittest tools.test_data_merge_check

Each case copies data/, tools/ and rules.md into a temp checkout (game/ is a symlink to the real one), lays a
sample change over its data/ and runs the check on it. The schema step needs jsonschema in this interpreter;
the harness cases need Godot (GODOT or godot on PATH) and a built, imported game/ (they run with --no-build)."""
import io
import json
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import data_merge_check as dmc  # noqa: E402

ROOT = Path(__file__).resolve().parent.parent
MERGE_FIX = ROOT / "tools" / "tests" / "merge_fixtures"
RULES_FIX = ROOT / "tools" / "tests" / "rules_fixtures"

try:
    import jsonschema  # noqa: F401
    HAVE_SCHEMA = True
except ImportError:
    HAVE_SCHEMA = False
GODOT = dmc.find_godot(None)
HAVE_GODOT = GODOT is not None and (ROOT / "game" / ".godot").is_dir()


class Checkout:
    """A temp copy of this repo's content with `overlay` (a folder shaped like data/) laid over data/."""

    def __init__(self, overlay: Path | None):
        self.tmp = tempfile.TemporaryDirectory(prefix="yok34-root-")
        self.root = Path(self.tmp.name)
        (self.root / "docs" / "design").mkdir(parents=True)
        shutil.copy(ROOT / "docs" / "design" / "rules.md", self.root / "docs" / "design" / "rules.md")
        shutil.copytree(ROOT / "data", self.root / "data")
        shutil.copytree(ROOT / "tools", self.root / "tools", ignore=shutil.ignore_patterns("tests", "runs", "__pycache__"))
        (self.root / "game").symlink_to(ROOT / "game")
        if overlay:
            for f in overlay.rglob("*.json"):
                dest = self.root / "data" / f.relative_to(overlay)
                dest.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy(f, dest)

    def check(self, *args):
        out = io.StringIO()
        code = dmc.main(["--root", str(self.root), "--no-build", "--runs", "10", "--json", *args], out=out)
        return code, json.loads(out.getvalue())

    def __enter__(self):
        return self

    def __exit__(self, *exc):
        self.tmp.cleanup()


def status(rep):
    return {s["step"]: s["status"] for s in rep["steps"]}


@unittest.skipUnless(HAVE_SCHEMA, "jsonschema not installed (pip install -r tools/requirements.txt)")
class SampleChangesTest(unittest.TestCase):
    def assertFailsAt(self, overlay, step, fragment):
        with Checkout(overlay) as c:
            code, rep = c.check("--only", "schema,rules,harness" if HAVE_GODOT else "schema,rules")
        st = status(rep)
        self.assertEqual(code, dmc.FAIL, rep)
        self.assertEqual(st[step], "fail", st)
        later = list(dmc.STEPS)[list(dmc.STEPS).index(step) + 1:]
        self.assertTrue(all(st[s] in ("not run", "skipped") for s in later), st)   # stops at the first failure
        failed = next(s for s in rep["steps"] if s["step"] == step)
        self.assertTrue(any(fragment in d for d in failed["details"]), failed["details"])
        return rep

    def test_valid_sample_change_passes_every_step_that_runs(self):
        only = "schema,rules,harness" if HAVE_GODOT else "schema,rules"
        with Checkout(MERGE_FIX / "valid-story-edit") as c:
            code, rep = c.check("--only", only)
        st = status(rep)
        self.assertTrue(all(st[s] == "pass" for s in only.split(",")), rep)
        self.assertEqual(code, dmc.INCOMPLETE, "a run with skipped steps is never a merge verdict")

    def test_schema_break_fails_at_schema(self):
        self.assertFailsAt(MERGE_FIX / "schema-wrong-type", "schema", "ryo-light-punch.json")

    def test_cross_file_rule_break_fails_at_rules(self):
        # schema-valid on its own, but the trial asks for a Kitsune normal Ryo does not have (R7)
        rep = self.assertFailsAt(MERGE_FIX / "rules-trial-foreign-move", "rules", "R7")
        self.assertEqual(status(rep)["schema"], "pass")

    def test_cancel_loop_is_caught_before_the_harness(self):
        # A rule that is not once per combo is refused by the schema already (once_per_combo: const true);
        # check_rules finds the loop itself when read without the schema (tools/test_check_rules.py).
        with Checkout(RULES_FIX / "cancel-loop") as c:
            code, rep = c.check("--only", "schema,rules")
        self.assertEqual(code, dmc.FAIL)
        self.assertEqual(status(rep)["schema"], "fail")
        self.assertNotEqual(status(rep)["harness"], "pass")

    def test_missing_kihon_slot_is_caught_before_the_harness(self):
        # The schema already requires input.kihon, so it fails there; check_rules catches it too (K2) when
        # read without the schema (tools/test_check_rules.py).
        with Checkout(RULES_FIX / "missing-kihon") as c:
            code, rep = c.check("--only", "schema,rules")
        st = status(rep)
        self.assertEqual(code, dmc.FAIL)
        self.assertTrue("fail" in (st["schema"], st["rules"]), st)
        self.assertNotEqual(st["harness"], "pass")

    @unittest.skipUnless(HAVE_GODOT, "Godot or an imported game/ not found")
    def test_harness_only_break_fails_at_harness(self):
        rep = self.assertFailsAt(MERGE_FIX / "harness-bot-range", "harness", "preferred_min is above preferred_max")
        self.assertEqual((status(rep)["schema"], status(rep)["rules"]), ("pass", "pass"))

    def test_report_names_the_failing_step(self):
        with Checkout(MERGE_FIX / "rules-trial-foreign-move") as c:
            out = io.StringIO()
            code = dmc.main(["--root", str(c.root), "--no-build", "--only", "schema,rules"], out=out)
        text = out.getvalue()
        self.assertEqual(code, dmc.FAIL)
        self.assertTrue(text.startswith("DATA MERGE CHECK: FAIL at step 2 (rules"), text)
        self.assertIn("fix step 2 (rules)", text)


class ScopeAndVerdictTest(unittest.TestCase):
    def test_classify(self):
        content, code = dmc.classify(["data/moves/a.json", "data/schema/special.schema.json", "game/x.cs",
                                      "docs/pipeline.md", "data/story/b.json", "data/attic/GuardTests.cs",
                                      "data/tools/run.py", "data/moves/README.md"])
        self.assertEqual(content, ["data/moves/a.json", "data/story/b.json"])
        self.assertEqual(code, ["data/attic/GuardTests.cs", "data/moves/README.md", "data/schema/special.schema.json",
                                "data/tools/run.py", "docs/pipeline.md", "game/x.cs"])

    def test_scope_of_treats_symlinks_submodules_and_odd_paths_as_code(self):
        content, code = dmc.scope_of([("data/moves/a.json", "100644", "100644"),
                                      ("data/moves/new.json", "000000", "100644"),
                                      ("data/moves/evil.json", "000000", "120000"),
                                      ("data/moves/sub.json", "160000", "160000"),
                                      ("data\\evil.json", "000000", "100644"),
                                      ("data/../game/x.json", "000000", "100644")])
        self.assertEqual(content, ["data/moves/a.json", "data/moves/new.json"])
        self.assertEqual(len(code), 4, code)

    def _repo_with_rename(self, src, dest):
        """A temp git repo whose branch `pr` moves `src` to `dest` (git mv) off `main`."""
        tmp = tempfile.TemporaryDirectory(prefix="yok34-git-")
        r = Path(tmp.name)
        git = ["git", "-C", str(r), "-c", "user.name=t", "-c", "user.email=t@example.invalid", "-c", "commit.gpgsign=false"]
        subprocess.run(git + ["init", "-q", "-b", "main"], check=True)
        for rel in (src, "data/moves/a.json"):
            (r / rel).parent.mkdir(parents=True, exist_ok=True)
            (r / rel).write_text('{"kind": "x"}\n' if rel.endswith(".json") else "public static class GuardTests {}\n")
        subprocess.run(git + ["add", "-A"], check=True)
        subprocess.run(git + ["commit", "-q", "-m", "base"], check=True)
        subprocess.run(git + ["switch", "-q", "-c", "pr"], check=True)
        (r / dest).parent.mkdir(parents=True, exist_ok=True)
        subprocess.run(git + ["mv", src, dest], check=True)
        subprocess.run(git + ["commit", "-q", "-m", "move"], check=True)
        return tmp, r

    def test_rename_out_of_game_into_data_goes_to_review(self):
        # A rename lists both paths (--no-renames): removing a test by moving it into data/ is a code change.
        for src, dest in (("game/tests/GuardTests.cs", "data/attic/GuardTests.cs"),
                          ("game/tests/fixtures/guard.json", "data/moves/guard.json")):
            with self.subTest(src=src, dest=dest):
                tmp, r = self._repo_with_rename(src, dest)
                with tmp:
                    changed = dmc.changed_files(r, "main")
                self.assertIn(src, changed)
                self.assertIn(dest, changed)
                code, text = self._verdict(changed)
                self.assertEqual(code, dmc.REVIEW, text)
                self.assertIn(f"code (goes to review): {src}", text)

    def _verdict(self, changed):
        """Every step passes (stubbed); only the change scope decides."""
        saved = (dmc.changed_entries, dmc.step_schema, dmc.step_rules, dmc.step_harness, dmc.step_suite,
                 dmc.find_godot, dmc.prepare_godot)

        def ok(*a):
            a[1].status = "pass"
        try:
            dmc.changed_entries = lambda root, base: [(p, "100644", "100644") for p in changed]
            dmc.step_schema = dmc.step_rules = ok
            dmc.step_harness = lambda root, st, *a: setattr(st, "status", "pass")
            dmc.step_suite = lambda root, st, *a: setattr(st, "status", "pass")
            dmc.find_godot = lambda arg: "godot"
            dmc.prepare_godot = lambda *a, **k: True
            out = io.StringIO()
            return dmc.main(["--base", "origin/main"], out=out), out.getvalue()
        finally:
            (dmc.changed_entries, dmc.step_schema, dmc.step_rules, dmc.step_harness, dmc.step_suite,
             dmc.find_godot, dmc.prepare_godot) = saved

    def test_data_only_change_is_mergeable(self):
        code, text = self._verdict(["data/moves/spirit-wave.json"])
        self.assertEqual(code, dmc.MERGEABLE, text)
        self.assertIn("verdict only; the designer reviews and merges", text)

    def test_code_change_goes_to_review(self):
        for changed in (["data/moves/x.json", "game/scripts/Sim/Match.cs"], ["data/schema/special.schema.json"]):
            with self.subTest(changed=changed):
                code, text = self._verdict(changed)
                self.assertEqual(code, dmc.REVIEW, text)
                self.assertIn("code (goes to review)", text)

    def test_missing_godot_is_setup_not_pass(self):
        saved = dmc.find_godot
        try:
            dmc.find_godot = lambda arg: None
            out = io.StringIO()
            code = dmc.main(["--only", "harness", "--no-build"], out=out)
        finally:
            dmc.find_godot = saved
        self.assertEqual(code, dmc.SETUP, out.getvalue())
        self.assertIn("Godot not found", out.getvalue())

    def test_unknown_step_is_setup(self):
        self.assertEqual(dmc.main(["--only", "lint"], out=io.StringIO()), dmc.SETUP)


if __name__ == "__main__":
    unittest.main()
