"""Tests for tools/check_docs.py and tools/changelog.py (YOK-188).
Run: python3 -I -m unittest discover -s tools -p 'test_check_docs.py'"""
import io
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import changelog  # noqa: E402
import check_docs  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]

AMENDMENTS = """# Amendments

## Index

| AM | Approved | Change |
|---|---|---|
| [AM1](#am1) | 2026-10-05 | One |
| [AM2](#am2) | 2026-10-06 | Two |

<a id="am1"></a>

## AM1 — One (2026-10-05)
"""


class Repo:
    """A throwaway git checkout holding the given files, all tracked."""

    def __init__(self, files: dict[str, str]):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)
        for path, text in files.items():
            p = self.root / path
            p.parent.mkdir(parents=True, exist_ok=True)
            p.write_text(text, encoding="utf-8")
        subprocess.run(["git", "init", "-q"], cwd=self.root, check=True)
        subprocess.run(["git", "add", "-A"], cwd=self.root, check=True)

    def check(self):
        out = io.StringIO()
        code = check_docs.main(["--root", str(self.root)], out=out)
        return code, out.getvalue()

    def __enter__(self):
        return self

    def __exit__(self, *exc):
        self.tmp.cleanup()


class CheckDocsTest(unittest.TestCase):
    def test_this_checkout_is_clean(self):
        out = io.StringIO()
        self.assertEqual(check_docs.main(["--root", str(ROOT)], out=out), 0, out.getvalue())

    def test_links_resolve_relative_to_the_file(self):
        files = {"docs/README.md": "[ok](process/a.md) [up](../README.md) [dir](design/) [web](https://x.y) [anchor](#top)\n",
                 "docs/process/a.md": "x", "docs/design/b.md": "x", "README.md": "x"}
        with Repo(files) as r:
            self.assertEqual(r.check()[0], 0)

    def test_broken_link_is_reported_with_file_and_target(self):
        with Repo({"docs/README.md": "see [gone](process/gone.md#part)\n"}) as r:
            code, out = r.check()
        self.assertEqual(code, 1)
        self.assertIn("links: docs/README.md: 'process/gone.md#part' does not resolve", out)

    def test_links_in_code_are_not_links(self):
        text = "```\n[x](nowhere.md)\n```\nand `[y](nowhere.md)` inline\n"
        with Repo({"docs/README.md": text}) as r:
            self.assertEqual(r.check()[0], 0)

    def test_image_links_are_checked(self):
        with Repo({"docs/README.md": "![shot](screenshots/none.png)\n"}) as r:
            self.assertIn("screenshots/none.png", r.check()[1])

    def test_markdown_outside_a_home_is_a_finding(self):
        with Repo({"docs/random-notes.md": "x", "game/scripts/Sim/NOTES.md": "x"}) as r:
            code, out = r.check()
        self.assertEqual(code, 1)
        self.assertIn("placement: docs/random-notes.md", out)
        self.assertIn("placement: game/scripts/Sim/NOTES.md", out)

    def test_proposal_files_are_refused_even_in_a_home(self):
        with Repo({"docs/design/lv2-steps-proposal.md": "x", "docs/process/TODO.md": "x"}) as r:
            code, out = r.check()
        self.assertEqual(code, 1)
        self.assertIn("proposals: docs/design/lv2-steps-proposal.md", out)
        self.assertIn("proposals: docs/process/TODO.md", out)

    def test_amendment_index_and_headings_must_agree(self):
        with Repo({"docs/design/gdd-amendments.md": AMENDMENTS}) as r:
            code, out = r.check()
        self.assertEqual(code, 1)
        self.assertIn("AM2 has an index row but no heading", out)
        self.assertNotIn("AM1", out)
        with Repo({"docs/design/gdd-amendments.md": AMENDMENTS + "\n## AM2 — Two\n\n## AM3 — Three\n"}) as r:
            self.assertIn("AM3 has a heading but no index row", r.check()[1])

    def test_untracked_files_are_ignored(self):
        with Repo({"README.md": "x"}) as r:
            (r.root / "docs").mkdir()
            (r.root / "docs" / "stray.md").write_text("[x](nope.md)", encoding="utf-8")
            self.assertEqual(r.check()[0], 0)

    MAP_INDEX = "# Codebase map\n\n| Area | Folder |\n|---|---|\n| [Sim](sim.md) | `game/scripts/Sim/` |\n"

    def test_map_every_script_has_exactly_one_row(self):
        files = {"game/scripts/Sim/A.cs": "", "game/scripts/Sim/Run/B.cs": "", "game/scripts/Top.cs": "",
                 "docs/codebase-map/README.md": self.MAP_INDEX + "| [Run](run.md#top) | `game/scripts/Sim/Run/` |\n",
                 "docs/codebase-map/sim.md": "| File | What |\n|---|---|\n"
                                            "| `Sim/A.cs`, `game/scripts/Top.cs` | uses `Sim/Run/B.cs` |\n"
                                            "| (cont.) | more on `Sim/Run/B.cs` |\n"
                                            "\nProse naming `Sim/Run/B.cs` is not a row.\n",
                 "docs/codebase-map/run.md": "| `Sim/Run/B.cs` | the run |\n"}
        with Repo(files) as r:
            self.assertEqual(r.check(), (0, "docs check: 0 finding(s)\n"))

    def test_map_reports_missing_doubled_and_stale_rows(self):
        files = {"game/scripts/Sim/A.cs": "", "game/scripts/Sim/B.cs": "", "game/scripts/Sim/C.cs": "",
                 "docs/codebase-map/README.md": self.MAP_INDEX,
                 "docs/codebase-map/sim.md": "| `Sim/A.cs` | a |\n| `Sim/Gone.cs` | removed |\n",
                 "docs/codebase-map/other.md": "| `Sim/A.cs` | a again |\n| `Sim/C.cs` | c |\n"}
        with Repo(files) as r:
            (r.root / "game/scripts/Sim/C.cs").unlink()  # deleted in the working tree: no longer a script
            code, out = r.check()
        self.assertEqual(code, 1)
        self.assertIn("map: game/scripts/Sim/B.cs: not in any area file", out)
        self.assertIn("map: game/scripts/Sim/A.cs: has 2 rows (docs/codebase-map/other.md, docs/codebase-map/sim.md)", out)
        self.assertIn("map: docs/codebase-map/sim.md: row for game/scripts/Sim/Gone.cs, which is not a tracked file", out)
        self.assertIn("map: docs/codebase-map/other.md: row for game/scripts/Sim/C.cs, which is deleted; drop the row", out)
        self.assertNotIn("Sim/C.cs: not in any area file", out)

    def test_map_a_deleted_script_needs_no_row(self):
        files = {"game/scripts/Sim/A.cs": "", "game/scripts/Sim/B.cs": "",
                 "docs/codebase-map/README.md": self.MAP_INDEX, "docs/codebase-map/sim.md": "| `Sim/A.cs` | a |\n"}
        with Repo(files) as r:
            (r.root / "game/scripts/Sim/B.cs").unlink()
            self.assertEqual(r.check()[0], 0)

    def test_map_first_cells_name_only_scripts(self):
        files = {"game/scripts/Sim/A.cs": "", "game/tests/FooTests.cs": "",
                 "docs/codebase-map/README.md": self.MAP_INDEX,
                 "docs/codebase-map/sim.md": "| `Sim/A.cs` | a |\n| `tests/FooTests.cs` | relative test |\n"
                                            "| `game/tests/FooTests.cs` | full test path |\n"
                                            "| `tests/run.tscn` | `tests/FooTests.cs` in a later cell is fine |\n"}
        with Repo(files) as r:
            code, out = r.check()
        self.assertEqual(code, 1)
        self.assertIn("map: docs/codebase-map/sim.md: first cell names `tests/FooTests.cs`, which is not under game/scripts/", out)
        self.assertIn("map: docs/codebase-map/sim.md: first cell names `game/tests/FooTests.cs`, which is not under game/scripts/", out)
        self.assertNotIn("game/scripts/tests/", out)
        self.assertEqual(out.count("map:"), 2)

    def test_map_area_files_must_be_linked_from_the_index(self):
        files = {"game/scripts/Sim/A.cs": "", "game/scripts/Ui/B.cs": "",
                 "docs/codebase-map/README.md": self.MAP_INDEX,
                 "docs/codebase-map/sim.md": "| `Sim/A.cs` | a |\n", "docs/codebase-map/ui.md": "| `Ui/B.cs` | b |\n"}
        with Repo(files) as r:
            code, out = r.check()
        self.assertEqual(code, 1)
        self.assertIn("map: docs/codebase-map/ui.md: not linked from docs/codebase-map/README.md", out)
        self.assertNotIn("sim.md: not linked", out)

    def test_map_untracked_scripts_are_ignored_and_a_missing_index_is_reported(self):
        with Repo({"game/scripts/Sim/A.cs": "", "docs/codebase-map/sim.md": "| `Sim/A.cs` | a |\n"}) as r:
            (r.root / "game/scripts/Sim/New.cs").write_text("", encoding="utf-8")
            code, out = r.check()
        self.assertEqual(code, 1)
        self.assertNotIn("New.cs", out)
        self.assertIn("map: docs/codebase-map/README.md: missing", out)

    def test_map_sizes(self):
        long_row = "| `Sim/A.cs` | " + "x" * 600 + " |\n"
        files = {"game/scripts/Sim/A.cs": "",
                 "docs/codebase-map/README.md": self.MAP_INDEX + "y" * check_docs.MAP_INDEX_MAX,
                 "docs/codebase-map/sim.md": long_row + "| (cont.) | " + "z" * 580 + " |\n",
                 "docs/codebase-map/big.md": "w" * (check_docs.MAP_AREA_MAX + 1)}
        with Repo(files) as r:
            code, out = r.check()
        self.assertEqual(code, 1)
        self.assertIn("map: docs/codebase-map/sim.md:1: row is 617 characters, at most 600", out)
        self.assertNotIn("sim.md:2", out)
        self.assertIn("map: docs/codebase-map/big.md: 40961 bytes", out)
        self.assertIn("map: docs/codebase-map/README.md:", out)
        self.assertIn("the index must stay under 8192", out)

    def test_not_a_checkout_is_exit_2(self):
        with tempfile.TemporaryDirectory() as d:
            out = io.StringIO()
            self.assertEqual(check_docs.main(["--root", d], out=out), 2)


def pr(n, title, merged, body=""):
    return {"number": n, "title": title, "body": body, "mergedAt": merged, "url": f"https://github.com/o/r/pull/{n}"}


class ChangelogTest(unittest.TestCase):
    def test_line_comes_from_the_changelog_section_else_the_title(self):
        body = "## Ticket\nYOK-1\n\n## Changelog\n<!-- one line -->\nThe merchant has a portrait.\n"
        self.assertEqual(changelog.entry(pr(208, "YOK-159-rest-stop-merchant-portrait", "2026-10-20T21:00:00Z", body)),
                         "- 2026-10-20 **YOK-159** The merchant has a portrait. ([#208](https://github.com/o/r/pull/208))")
        self.assertEqual(changelog.entry(pr(9, "YOK-repo-tidy", "2026-10-06T10:00:00Z")),
                         "- 2026-10-06 repo tidy ([#9](https://github.com/o/r/pull/9))")

    def test_empty_section_falls_back_and_none_leaves_the_pr_out(self):
        empty = "## Changelog\n<!-- One line, or none -->\n\n## Other\nx"
        self.assertIn("generic throws", changelog.entry(pr(3, "YOK-19-generic-throws", "2026-10-06T00:00:00Z", empty)))
        self.assertIsNone(changelog.entry(pr(4, "YOK-1-x", "2026-10-06T00:00:00Z", "## Changelog\nNone.\n")))

    def test_build_weeks(self):
        self.assertEqual(changelog.week_of(changelog.dt.date(2026, 10, 4)), "Week 1 (to 2026-10-10)")
        self.assertEqual(changelog.week_of(changelog.dt.date(2026, 10, 17)), "Week 2 (2026-10-11 to 2026-10-17)")
        self.assertEqual(changelog.week_of(changelog.dt.date(2026, 10, 18)), "Week 3 (2026-10-18 to 2026-10-24)")
        self.assertEqual(changelog.week_of(changelog.dt.date(2026, 11, 8)), "After the build")

    def test_render_groups_newest_week_first(self):
        text = changelog.render([pr(1, "YOK-1-a", "2026-10-05T00:00:00Z"), pr(3, "YOK-3-c", "2026-10-20T00:00:00Z"),
                                 pr(2, "YOK-2-b", "2026-10-20T00:00:00Z")])
        self.assertLess(text.index("## Week 3"), text.index("## Week 1"))
        self.assertLess(text.index("#3"), text.index("#2"))
        self.assertTrue(text.endswith(")\n"))

    def test_check_reports_out_of_date_without_writing(self):
        with tempfile.TemporaryDirectory() as d:
            src, out_file = Path(d) / "prs.json", Path(d) / "CHANGELOG.md"
            src.write_text('[{"number": 1, "title": "YOK-1-a", "body": null, "mergedAt": "2026-10-05T00:00:00Z", '
                           '"url": "u"}]', encoding="utf-8")
            args = ["--from-json", str(src), "--out", str(out_file)]
            self.assertEqual(changelog.main(args + ["--check"], out=io.StringIO()), 1)
            self.assertFalse(out_file.exists())
            self.assertEqual(changelog.main(args, out=io.StringIO()), 0)
            self.assertEqual(changelog.main(args + ["--check"], out=io.StringIO()), 0)


if __name__ == "__main__":
    unittest.main()
