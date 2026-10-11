"""Tests for the goal agent's reasoning layer. No model calls, no build.
Run: python3 -m unittest discover -s tools/goal_agent -p 'test_*.py'
"""
import sys
import tempfile
import unittest
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent.parent
sys.path.insert(0, str(HERE))
sys.path.insert(0, str(HERE.parent / "content_pipeline"))

import critics  # noqa: E402
import gdd  # noqa: E402
import priority  # noqa: E402
import scan  # noqa: E402
import writer  # noqa: E402


def feature(fid, terms, milestone, needs=(), size=1, kind="code", build=True, evidence=None):
    f = {"id": fid, "title": fid, "rules": ["T2"], "gdd": "5.3", "terms": list(terms), "milestone": milestone,
         "kind": kind, "owner": "gameplay-programmer", "size": size, "needs": list(needs),
         "evidence": evidence or [{"symbol": f"class {fid}\\b", "in": "game/scripts/*"}]}
    if build:
        f["build"] = {"files": [f"game/scripts/Sim/Run/{fid}.cs", f"game/tests/{fid}Tests.cs"], "context": [], "brief": ""}
    return f


def gaps_of(features, code=None, tests=()):
    s = scan.Scan({"game/tests/T.cs": {"tests": list(tests)}}, code or {})
    return scan.detect_gaps(features, s)


class DesignTest(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.design = gdd.load(ROOT)

    def test_scope_facts_are_read_from_section_g(self):
        d = self.design
        self.assertEqual(d.never_cut, ["copy rule", "merchant", "Kihon", "harness", "story cards"])
        self.assertEqual(d.cut_order[:2], ["Kata", "meta-unlock layer"])
        self.assertEqual(d.cut_order[-1], "colour-only presets")
        self.assertEqual(d.week_of("combat core"), 1)
        self.assertEqual(d.week_of("copy rule"), 2)
        self.assertEqual(d.week_of("story cards"), 3)
        self.assertIsNone(d.week_of("supers"))

    def test_rule_text_keeps_its_table(self):
        self.assertIn("Undertow", self.design.rules["X4"])
        self.assertIn("most recently levelled", self.design.rules["T3"])

    def test_open_questions_are_not_rules(self):
        self.assertTrue(self.design.open_questions)
        self.assertFalse(any(q in self.design.rules.values() for q in self.design.open_questions))

    def test_manifest_matches_the_design(self):
        self.assertEqual(scan.validate_manifest(scan.load_features(ROOT), self.design, ROOT), [])

    def test_manifest_problems_are_reported(self):
        m = {"not_tracked": {}, "features": [feature("X", ["x"], "supers") | {"rules": ["Z9"], "needs": ["nope"]}]}
        text = "\n".join(scan.validate_manifest(m, self.design, ROOT))
        for expected in ("rule Z9 is not in rules.md", "milestone 'supers'", "unknown feature 'nope'", "rule T2 is neither"):
            self.assertIn(expected, text)


class GapTest(unittest.TestCase):
    def test_present_partial_missing(self):
        f = feature("Thing", ["x"], "map", evidence=[{"symbol": "class Thing\\b", "in": "game/scripts/*"},
                                                      {"test": "^ThingTests\\.", "min": 2}])
        self.assertEqual(gaps_of([f])["Thing"].status, "missing")
        code = {"game/scripts/Thing.cs": "public class Thing {}"}
        self.assertEqual(gaps_of([f], code)["Thing"].status, "partial")
        self.assertEqual(gaps_of([f], code, ["ThingTests.A"])["Thing"].status, "partial")
        self.assertEqual(gaps_of([f], code, ["ThingTests.A", "ThingTests.B"])["Thing"].status, "present")

    def test_a_comment_is_not_evidence(self):
        src = scan.strip_comments("// class CopyRule is not built here\n/* class CopyRule */\nclass Other {}")
        f = feature("CopyRule", ["x"], "map")
        self.assertEqual(gaps_of([f], {"game/scripts/a.cs": src})["CopyRule"].status, "missing")

    def test_the_real_scan_finds_the_combat_core(self):
        g = scan.detect_gaps(scan.load_features(ROOT)["features"], scan.scan(ROOT))
        for fid in ("fight-loop", "throws", "kihon", "run-flow"):
            self.assertEqual(g[fid].status, "present", g[fid].unmet)


class PriorityTest(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.design = gdd.load(ROOT)

    def rank(self, features, week=2, code=None):
        return priority.rank(gaps_of(features, code), self.design, week)

    def test_never_cut_outranks_everything_else_that_is_ready(self):
        r = self.rank([feature("A", ["copy rule"], "screens/HUD", size=3), feature("B", ["x"], "combat core")])
        self.assertEqual(r[0].feature_id, "A")

    def test_blocked_features_are_not_chosen_and_raise_what_they_wait_on(self):
        hub, leaf = feature("Hub", ["x"], "map"), feature("Leaf", ["copy rule"], "copy rule", needs=["Hub"])
        r = self.rank([hub, leaf, feature("Solo", ["x"], "map")])
        self.assertEqual([s.feature_id for s in r if s.eligible], ["Hub", "Solo"])
        self.assertEqual(r[-1].blocked_by, ["Hub"])
        self.assertEqual(r[0].total - r[1].total, priority.WEIGHTS["unblocks"])

    def test_waiting_is_counted_through_chains(self):
        fs = [feature("A", ["x"], "map"), feature("B", ["x"], "map", needs=["A"]), feature("C", ["x"], "map", needs=["B"])]
        self.assertEqual(priority.waiting_on("A", gaps_of(fs)), ["B", "C"])

    def test_work_due_sooner_scores_higher(self):
        self.assertEqual([priority.urgency(w, 2) for w in (1, 2, 3, 4, 5)], [3, 3, 2, 1, 0])

    def test_first_cut_is_built_last(self):
        r = self.rank([feature("K", ["Kata"], "map"), feature("D", ["dojo trials"], "map"), feature("N", ["x"], "map")])
        self.assertEqual([s.feature_id for s in r], ["N", "D", "K"])

    def test_content_and_briefless_features_are_routed_not_written(self):
        r = self.rank([feature("C", ["copy rule"], "map", kind="content"), feature("NoBrief", ["x"], "map", build=False)])
        self.assertFalse(any(s.eligible for s in r))
        self.assertIn("producer", r[0].route)

    def test_a_built_feature_leaves_the_ranking(self):
        f = feature("Done", ["x"], "map")
        self.assertEqual(self.rank([f], code={"game/scripts/Done.cs": "class Done {}"}), [])


REPLY = """Here you go.
@@@ FILE game/scripts/Sim/Run/Thing.cs
namespace YokaiFighters.Sim;
public static class Thing
{
	public const int Heal = 25; // R6
}
@@@ END
@@@ EDIT game/scripts/Sim/Run/Old.cs
@@@ SEARCH
int a = 1;
@@@ REPLACE
int a = 2;
@@@ SEARCH
int b = 1;
@@@ REPLACE
int b = 3;
@@@ END
@@@ MAP
`Thing`: the
heal (R6).
@@@ END
@@@ NOTES
none
@@@ END
@@@ DISPUTE Probe_X
T3 does not say that.
@@@ END
"""


class WriterTest(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)
        (self.root / "game/scripts/Sim/Run").mkdir(parents=True)
        (self.root / "game/tests").mkdir(parents=True)
        (self.root / "docs").mkdir()
        (self.root / "game/scripts/Sim/Run/Old.cs").write_text("int a = 1;\nint b = 1;\n")
        (self.root / "game/tests/OldTests.cs").write_text("// tests\n")
        (self.root / "docs/codebase-map.md").write_text("# Codebase map\n")
        self.feature = {"id": "thing", "rules": ["R6"], "build": {"files": [
            "game/scripts/Sim/Run/Thing.cs", "game/scripts/Sim/Run/Old.cs", "game/tests/OldTests.cs"]}}

    def tearDown(self):
        self.tmp.cleanup()

    def test_parse(self):
        c = writer.parse(REPLY)
        self.assertEqual(c.paths, ["game/scripts/Sim/Run/Old.cs", "game/scripts/Sim/Run/Thing.cs"])
        self.assertEqual(c.edits["game/scripts/Sim/Run/Old.cs"], [("int a = 1;", "int a = 2;"), ("int b = 1;", "int b = 3;")])
        self.assertEqual(c.map_entry, "`Thing`: the heal (R6).")
        self.assertEqual(c.disputes, {"Probe_X": "T3 does not say that."})
        self.assertEqual(c.problems, [])

    def test_apply_then_revert_leaves_nothing(self):
        c = writer.parse(REPLY)
        self.assertEqual(writer.check_paths(self.root, c, self.feature), [])
        snap = writer.apply(self.root, c, self.feature)
        self.assertEqual((self.root / "game/scripts/Sim/Run/Old.cs").read_text(), "int a = 2;\nint b = 3;\n")
        self.assertIn("**`thing`** (R6): `Thing`: the heal (R6).", (self.root / "docs/codebase-map.md").read_text())
        writer.revert(self.root, snap)
        self.assertFalse((self.root / "game/scripts/Sim/Run/Thing.cs").exists())
        self.assertEqual((self.root / "game/scripts/Sim/Run/Old.cs").read_text(), "int a = 1;\nint b = 1;\n")
        self.assertEqual((self.root / "docs/codebase-map.md").read_text(), "# Codebase map\n")

    def test_refuses_what_it_must_not_write(self):
        cases = {
            "@@@ FILE tools/x.py\nx\n@@@ END\n@@@ MAP\nm\n@@@ END": "not one of this feature's files",
            "@@@ FILE game/scripts/Sim/Run/Old.cs\nx\n@@@ END\n@@@ MAP\nm\n@@@ END": "this one exists",
            "@@@ EDIT game/tests/OldTests.cs\n@@@ SEARCH\n// tests\n@@@ REPLACE\n\n@@@ END\n@@@ MAP\nm\n@@@ END": "existing tests cannot be edited",
            "@@@ EDIT game/scripts/Sim/Run/Old.cs\n@@@ SEARCH\nint\n@@@ REPLACE\nlong\n@@@ END\n@@@ MAP\nm\n@@@ END": "occurs 2 times",
            "@@@ FILE game/scripts/Sim/Run/Thing.cs\nx\n@@@ END": "no MAP block",
            "@@@ FILE game/scripts/Sim/Run/Thing.cs\nx": "has no '@@@ END'",
            "nothing": "no FILE or EDIT block",
        }
        for reply, expected in cases.items():
            problems = "\n".join(writer.check_paths(self.root, writer.parse(reply), self.feature))
            self.assertIn(expected, problems, reply)

    def test_role_file_sections_are_the_prompt_source(self):
        self.assertIn("Determinism", writer.agent_section(ROOT, "code-critic", "What to attack"))
        self.assertIn("Ties and ordering", writer.agent_section(ROOT, "test-breaker", "What to attack"))
        self.assertIn("60-tick", writer.agent_section(ROOT, "gameplay-programmer", "Engine constraints"))


class CriticTest(unittest.TestCase):
    def test_code_checks(self):
        c = writer.Change(files={
            "game/scripts/Sim/Run/Bad.cs": 'using Godot;\nclass Bad { float x; string y = "kitsune"; } // TODO in a comment is fine\n',
            "game/tests/BadTests.cs": 'class BadTests { void A() { AbilityPool.LoadRepo(root); } }\n',
            "game/scripts/Ui/Fine.cs": "using Godot;\nclass Fine { float x; }\n",
        })
        found = [(i.where, i.rule) for i in critics.code_checks(ROOT, c)]
        self.assertEqual(sorted(found), [
            ("game/scripts/Sim/Run/Bad.cs", "data-driven"), ("game/scripts/Sim/Run/Bad.cs", "determinism"),
            ("game/scripts/Sim/Run/Bad.cs", "sim-purity"), ("game/tests/BadTests.cs", "tests")])

    def test_findings_need_a_real_line_and_a_real_rule(self):
        design = gdd.load(ROOT)
        path = "tools/goal_agent/priority.py"
        raw = [
            {"severity": "blocking", "file": path, "quote": "UNBLOCKS_CAP = 5", "rule": "T3", "problem": "kept"},
            {"severity": "blocking", "file": path, "quote": "this line is invented", "rule": "T3", "problem": "no such line"},
            {"severity": "blocking", "file": path, "quote": "UNBLOCKS_CAP = 5", "rule": "Q7", "problem": "no such rule"},
            {"severity": "advisory", "file": "README.md", "quote": "Yokai", "rule": "T3", "problem": "not in the change"},
        ]
        kept, dropped = critics.verify_findings(ROOT, raw, design, [path])
        self.assertEqual([(i.problem, i.blocking) for i in kept], [("kept", True)])
        self.assertTrue(kept[0].where.startswith(path + ":"))
        self.assertEqual(len(dropped), 3)

    def test_probe_rule_comments(self):
        src = "\t// T3: ties go to the most recently levelled\n\t[Test]\n\tpublic static void Tie_Recent() {}\n"
        self.assertEqual(critics.probe_rules(src), {"Tie_Recent": "T3: ties go to the most recently levelled"})


if __name__ == "__main__":
    unittest.main()
