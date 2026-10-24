"""The code-writing step: ask a model for one feature, then put its answer in the repo.

The model has no tools and no view of the repo. It sees the rule text read from the
design, the files the feature's brief names, and the checks its answer has to pass. It
answers in marked blocks, and this module is the only thing that touches the disk:

  * a new file may only be one of the files the feature names, and must not exist yet;
  * an edit may only change an existing file the feature names, by replacing text that
    occurs exactly once;
  * existing tests cannot be edited at all, so the writer cannot go green by weakening one.

Every write is snapshotted first, so a rejected attempt is undone completely.
"""
from __future__ import annotations

import re
from dataclasses import dataclass, field
from pathlib import Path

MAP_FILE = "docs/codebase-map/goal-agent.md"
MAP_HEADING = "## Built by the goal agent (`tools/goal_agent/`)"
MAP_INTRO = ("Features written by the goal agent and accepted by its gates (build, tests, re-scan, code-critic, "
             "test-breaker). Each still needs the designer's review before it merges.")
MAX_CONTEXT_LINES = 420

_MARK = re.compile(r"^@@@ (FILE|EDIT|SEARCH|REPLACE|MAP|NOTES|DISPUTE|PROBES|END)\b ?(.*)$")


@dataclass
class Change:
    files: dict[str, str] = field(default_factory=dict)                       # new file -> content
    edits: dict[str, list[tuple[str, str]]] = field(default_factory=dict)     # file -> [(search, replace)]
    map_entry: str = ""
    notes: str = ""
    disputes: dict[str, str] = field(default_factory=dict)                    # probe name -> reason
    probes: str = ""                                                          # used by the test breaker's reply
    problems: list[str] = field(default_factory=list)                         # format errors found while parsing

    @property
    def paths(self) -> list[str]:
        return sorted(set(self.files) | set(self.edits))

    def new_text(self, path: str) -> str:
        """The text this change adds to a file (whole new files, or the replacement side of edits)."""
        return self.files.get(path) or "\n".join(r for _, r in self.edits.get(path, []))


def parse(reply: str) -> Change:
    """Reads the marked blocks out of a model reply."""
    c = Change()
    kind, arg, buf, search = None, "", [], None

    def close():
        nonlocal search
        text = "\n".join(buf).strip("\n")
        if kind == "FILE":
            c.files[arg] = text + "\n"
        elif kind == "REPLACE":
            if search is None:
                c.problems.append(f"EDIT {arg}: REPLACE without SEARCH")
            else:
                c.edits.setdefault(arg, []).append((search, text))
            search = None
        elif kind == "SEARCH":
            search = text
        elif kind == "MAP":
            c.map_entry = " ".join(text.split())
        elif kind == "NOTES":
            c.notes = text.strip()
        elif kind == "DISPUTE":
            c.disputes[arg.strip()] = " ".join(text.split())
        elif kind == "PROBES":
            c.probes = text + "\n"

    for line in reply.replace("\r\n", "\n").split("\n"):
        m = _MARK.match(line)
        if not m:
            if kind:
                buf.append(line)
            continue
        tag, rest = m.group(1), m.group(2).strip()
        if tag == "END":
            close()
            kind, buf = None, []
        elif tag in ("SEARCH", "REPLACE"):
            if kind in ("SEARCH", "REPLACE"):
                close()
            elif kind != "EDIT":
                c.problems.append(f"{tag} outside an EDIT block")
                continue
            kind, buf = tag, []
        else:
            if kind:
                close()
            kind, arg, buf = tag, rest, []
            if tag == "EDIT":
                c.edits.setdefault(rest, [])
    if kind:
        c.problems.append(f"block {kind} {arg} has no '@@@ END'")
    for path, pairs in c.edits.items():
        if not pairs:
            c.problems.append(f"EDIT {path}: no SEARCH/REPLACE pair")
    return c


def check_paths(root: Path, change: Change, feature: dict) -> list[str]:
    """Why this change may not be written, if it may not."""
    allowed = feature["build"]["files"]
    problems = list(change.problems)
    if not change.paths:
        problems.append("the reply contains no FILE or EDIT block")
    for path in change.paths:
        if path not in allowed:
            problems.append(f"{path}: not one of this feature's files ({', '.join(allowed)}). "
                            "If the feature cannot be built without it, say so under NOTES.")
    for path in change.files:
        if (root / path).exists():
            problems.append(f"{path}: FILE is for new files and this one exists; use EDIT")
        if path in change.edits:
            problems.append(f"{path}: both FILE and EDIT")
    for path, pairs in change.edits.items():
        target = root / path
        if not target.is_file():
            problems.append(f"{path}: EDIT is for existing files and this one does not exist; use FILE")
            continue
        if path.startswith("game/tests/"):
            problems.append(f"{path}: existing tests cannot be edited; add a new test file")
            continue
        text = target.read_text(encoding="utf-8")
        for search, _ in pairs:
            n = text.count(search)
            if n != 1:
                problems.append(f"{path}: SEARCH text occurs {n} times, must occur exactly once: {search[:80]!r}")
    for path in change.files:
        if "\t" not in change.files[path] and "\n    " in change.files[path]:
            problems.append(f"{path}: indented with spaces; this codebase uses tabs")
    if not change.map_entry:
        problems.append("no MAP block: the codebase map needs one entry for the feature")
    return problems


def apply(root: Path, change: Change, feature: dict) -> dict[str, str | None]:
    """Writes the change. Returns the snapshot `revert` needs (path -> text before, None = did not exist)."""
    snapshot: dict[str, str | None] = {}
    for path, content in change.files.items():
        snapshot[path] = None
        (root / path).parent.mkdir(parents=True, exist_ok=True)
        (root / path).write_text(content, encoding="utf-8")
    for path, pairs in change.edits.items():
        text = (root / path).read_text(encoding="utf-8")
        snapshot[path] = text
        for search, replace in pairs:
            text = text.replace(search, replace, 1)
        (root / path).write_text(text, encoding="utf-8")
    map_path = root / MAP_FILE
    text = map_path.read_text(encoding="utf-8")
    snapshot[MAP_FILE] = text
    if MAP_HEADING not in text:
        text = text.rstrip("\n") + f"\n\n{MAP_HEADING}\n{MAP_INTRO}\n"
    entry = f"- **`{feature['id']}`** ({', '.join(feature['rules'])}): {change.map_entry}"
    map_path.write_text(text.rstrip("\n") + f"\n{entry}\n", encoding="utf-8")
    return snapshot


def revert(root: Path, snapshot: dict[str, str | None]) -> None:
    for path, before in snapshot.items():
        target = root / path
        if before is None:
            target.unlink(missing_ok=True)
            Path(str(target) + ".uid").unlink(missing_ok=True)  # Godot's id file for a new script
        else:
            target.write_text(before, encoding="utf-8")


def agent_section(root: Path, slug: str, heading: str) -> str:
    """One `## heading` section of a crew role file, so prompts and the crew share one source."""
    text = (root / ".claude" / "agents" / f"{slug}.md").read_text(encoding="utf-8")
    m = re.search(rf"^## {re.escape(heading)}[^\n]*\n(.*?)(?=^## |\Z)", text, re.S | re.M)
    if not m:
        raise SystemExit(f".claude/agents/{slug}.md has no '## {heading}' section; the goal agent reads it")
    return m.group(1).strip()


def read_context(root: Path, paths: list[str]) -> str:
    out = []
    for path in paths:
        lines = (root / path).read_text(encoding="utf-8").split("\n")
        cut = len(lines) > MAX_CONTEXT_LINES
        body = "\n".join(lines[:MAX_CONTEXT_LINES])
        out.append(f"===== {path}" + (f" (first {MAX_CONTEXT_LINES} of {len(lines)} lines)" if cut else "") + f" =====\n{body}")
    return "\n\n".join(out)


FORMAT = """Answer with marked blocks only. Every marker starts at the beginning of a line.

A new file, complete:
@@@ FILE game/scripts/Sim/Run/Example.cs
<the whole file>
@@@ END

A change to an existing file. Each SEARCH text must occur exactly once in the file as it is now; keep it short but unique:
@@@ EDIT game/scripts/Sim/Run/Existing.cs
@@@ SEARCH
<exact text now in the file>
@@@ REPLACE
<the text that replaces it>
@@@ SEARCH
<a second place, if needed>
@@@ REPLACE
<its replacement>
@@@ END

One entry for the codebase map, a single paragraph in the map's style (what the types are, what they do, which rule IDs):
@@@ MAP
<the paragraph>
@@@ END

Anything the designer must know: a reading of a rule you had to choose, something the feature needs that you were not allowed to write:
@@@ NOTES
<plain text, or "none">
@@@ END"""

DISPUTE_FORMAT = """
If a failing probe expects something the written rule does not say, do not change the code for it. Dispute it instead, naming the rule ID; the designer decides disputed probes:
@@@ DISPUTE <ProbeMethodName>
<why the rule does not require what the probe expects>
@@@ END"""


def system_prompt(root: Path) -> str:
    constraints = agent_section(root, "gameplay-programmer", "Engine constraints")
    return f"""You are the code-writing step of the Yokai Fighters goal agent. Yokai Fighters is a deterministic 2.5D fighting roguelite in Godot 4 (.NET) with C#. You write one feature at a time.

You have no tools and cannot see the repository. Everything you know about the project is in the message you are given: the written rules, the files next to the feature, and the checks your answer has to pass. Do not assume a type or member exists unless it appears in those files.

Engine constraints (from the gameplay programmer's role, non-negotiable):
{constraints}

How to work:
- Build exactly the feature described and nothing else. Later features are named so you can leave room for them, not so you build them.
- The rule text is the specification. Where it is silent, take the narrowest reading and say which one under NOTES. Never answer an entry under "Open questions".
- You may only write the files listed under "Files you may write". Existing tests cannot be edited and must stay green, so do not change the behaviour or the state hash of anything the feature does not need to change.
- Tests: a new public static class in namespace YokaiFighters.Tests, one `[Test] public static void` method per case, using `Assert.True(cond, msg)` and `Assert.Equal(expected, actual, msg)` from the runner shown to you. Test the rule, clause by clause, including its tie-breaks and its "never". Use test fixtures only (`AbilityPool.LoadFixtures`), never `data/`.
- Match the code you are shown: tabs for indentation, file-scoped namespaces, XML doc comments that say what and cite rule IDs, a named constant with its rule ID for any number the rules give.
- Code under game/scripts/Sim/ is pure C#: no Godot types, integers only, no clock, `SimRng` as the only random source, nothing that depends on Dictionary or HashSet enumeration order.

Two adversarial reviewers read what you write. One checks every clause of the cited rules against your code. The other writes probe tests that try to make your code give a wrong answer, and they are run.

{FORMAT}"""


def user_prompt(root: Path, feature: dict, design, unmet: list[str], open_questions: list[str]) -> str:
    b = feature["build"]
    context = [p for p in b["context"] if (root / p).is_file()]
    for extra in ("game/tests/TestRunner.cs",):
        if extra not in context:
            context.append(extra)
    parts = [
        f"# Feature: {feature['title']}  (`{feature['id']}`)",
        "## The rules it implements (docs/design/rules.md)\n" + design.rule_text(feature["rules"]),
        f"## The GDD section behind them ({feature['gdd']})\n" + design.gdd_sections[feature["gdd"]],
        "## Brief\n" + b["brief"],
        "## Files you may write\n" + "\n".join(
            f"- {p} ({'exists: EDIT' if (root / p).exists() else 'new: FILE'})" for p in b["files"]),
        "## Done when\nThe project builds, every existing test still passes, your new tests pass, and a scan of the code finds:\n"
        + "\n".join(f"- {u}" for u in unmet),
        "## Open questions (the designer decides these; do not answer them in code)\n" + "\n".join(f"- {q}" for q in open_questions),
        "## The code next to this feature\n" + read_context(root, context),
    ]
    return "\n\n".join(parts)


def repair_prompt(base: str, previous_reply: str, issues: list, probes: str, attempt: int) -> str:
    listed = "\n".join(f"{n}. {i.render()}" for n, i in enumerate(issues, 1))
    parts = [base,
             f"# Attempt {attempt}: your previous answer was rejected\nIt has been removed from the repository, so answer again with the complete set of blocks, not a patch on top of it.",
             "## Your previous answer\n" + previous_reply.strip(),
             "## Why it was rejected\nEvery finding must be fixed, except those marked advisory: take those where they are right.\n" + listed]
    if probes:
        parts.append("## The probe tests that were run against it\n" + probes.strip() + "\n" + DISPUTE_FORMAT)
    return "\n\n".join(parts)
