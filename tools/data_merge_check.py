#!/usr/bin/env python3
"""Data merge check: the auto-merge path for content changes in data/ (YOK-34).

Usage:
    python3 tools/data_merge_check.py [--base REF] [--root DIR] [--runs N] [--seed N]
                                      [--no-build] [--only STEPS] [--godot PATH]
                                      [--report FILE] [--json]

Runs, in order, stopping at the first step that fails:

  1 schema   tools/validate_data.py: JSON Schema + per-file consistency (YOK-12)
  2 rules    tools/check_rules.py: the written rules over the whole content set (YOK-35);
             its "harness must confirm" items are listed, never gating
  3 harness  the whole-run harness (YOK-26, G1/R5): the newcomer Kihon bot plays N seeded
             runs headless on ROOT/data; it must finish every run and write its results.
             Balance numbers are printed for the Sparring Partner, never gating.
  4 suite    the full headless Godot test suite (data PRs need it green, as code does)

Verdicts (exit code):
  0 MERGEABLE   every step passed on the full 200-run batch after a fresh build, and the change touches
                only content in data/
  1 FAIL        a step failed; the report names it and why
  2 SETUP       the check could not run (no Godot, no jsonschema, build failed, bad --base), or, with
                --base, the tree has uncommitted changes outside data/ content
  3 REVIEW      every step passed but the change also touches code (anything but a .json file in data/,
                or data/schema/; a rename counts both paths; a symlink or submodule is code): it goes to code review
  4 INCOMPLETE  every step that ran passed, but --only skipped some, --runs was under 200 or --no-build
                was given: not a merge verdict
  5 NOTHING TO MERGE  --base shows no changed file: no verdict
Designer decisions 2026-10-17: empty diff, dirty tree and full batch + build (docs/pipeline.md).

MERGEABLE is a verdict only. Nothing here merges: the designer still reviews and merges every
data PR until they say otherwise (CLAUDE.md, Pipeline).

--base REF   compare HEAD with REF (git diff REF...HEAD in ROOT) to decide data-only vs code.
             Without it the scope is not checked and the best verdict is MERGEABLE for the tree.
--root DIR   the checkout to check (default: this repo). Its data/ and game/ are used, and its tools/
             unless --tools names another.
--tools DIR  where validate_data.py and check_rules.py come from (CI: the base branch's tools/, extracted
             outside the checkout so the tree stays clean). The validator is pointed at ROOT through
             YOK_CONTENT_ROOT.
--only       comma list of steps to run (schema,rules,harness,suite), for local iteration.
--no-build   skip `dotnet build` and the Godot import before the Godot steps.
"""
from __future__ import annotations

import argparse
import json
import os
import shutil
import stat
import subprocess
import sys
import tempfile
import time
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
STEPS = ("schema", "rules", "harness", "suite")
MERGEABLE, FAIL, SETUP, REVIEW, INCOMPLETE, NOTHING = 0, 1, 2, 3, 4, 5
VERDICT = {MERGEABLE: "MERGEABLE", FAIL: "FAIL", SETUP: "SETUP", REVIEW: "REVIEW", INCOMPLETE: "INCOMPLETE",
           NOTHING: "NOTHING TO MERGE"}
FULL_RUNS = 200   # designer 2026-10-17: MERGEABLE needs the full 200-run batch and a fresh build
MAX_LINES = 25          # lines of a failing step's output kept in the report
HARNESS_OUTCOMES = {"won", "lost"}


class Step:
    def __init__(self, name: str, title: str):
        self.name, self.title = name, title
        self.status = "not run"     # pass | fail | setup | skipped | not run
        self.summary = ""
        self.details: list[str] = []
        self.notes: list[str] = []  # informational, never gating
        self.seconds = 0.0

    def as_dict(self) -> dict:
        return {"step": self.name, "title": self.title, "status": self.status, "summary": self.summary,
                "details": self.details, "notes": self.notes, "seconds": round(self.seconds, 1)}


def tail(text: str, n: int = MAX_LINES) -> list[str]:
    lines = [ln.rstrip() for ln in text.splitlines() if ln.strip()]
    return lines[-n:]


def run_split(cmd: list[str], cwd: Path | None = None, timeout: int = 1800, env: dict | None = None) -> tuple[int, str, str]:
    """(exit code, stdout, stderr)."""
    try:
        p = subprocess.run(cmd, cwd=cwd, capture_output=True, text=True, timeout=timeout, env=env)
    except FileNotFoundError as e:
        return 127, "", f"{e}"
    except subprocess.TimeoutExpired:
        return 124, "", f"timed out after {timeout} s: {' '.join(cmd)}"
    return p.returncode, p.stdout or "", p.stderr or ""


def run(cmd: list[str], cwd: Path | None = None, timeout: int = 1800, env: dict | None = None) -> tuple[int, str]:
    """(exit code, stdout + stderr), for output that is shown, never parsed."""
    code, out, err = run_split(cmd, cwd, timeout, env)
    return code, out + err


# --- scope: data-only or code ----------------------------------------------------------------

def is_content(path: str) -> bool:
    """Content the data path may carry: a JSON file under data/, outside data/schema/ (the C# loader's
    contract). Anything else under data/ (scripts, binaries, other text) goes to review like code.
    Paths are git's (always `/`): a backslash is part of a file name, never a separator, and a path with an
    empty, `.` or `..` segment is never content."""
    parts = path.split("/")
    if any(seg in ("", ".", "..") for seg in parts):
        return False
    return len(parts) > 1 and parts[0] == "data" and parts[1] != "schema" and path.endswith(".json")


def classify(paths: list[str], not_regular: set[str] = frozenset()) -> tuple[list[str], list[str]]:
    """(content, code). `not_regular`: paths git records as something other than a plain file (a symlink,
    a submodule); they are code whatever their name, since a symlink can make the loader read a code file."""
    content = sorted(p for p in paths if is_content(p) and p not in not_regular)
    code = sorted(p for p in paths if p not in content)
    return content, code


REGULAR_MODES = {"100644", "100755", "000000"}   # plain files, and the missing side of an add or delete


def changed_entries(root: Path, base: str) -> list[tuple[str, str, str]]:
    """(path, old mode, new mode) for every file changed in REF...HEAD.
    --no-renames: a rename is a delete plus an add, so both paths are classified (a test moved out of
    game/ into data/ is a code change). --raw: the file modes, so a symlink (120000) or a submodule (160000)
    is seen. -z: paths come raw, never quoted."""
    code, out, err = run_split(["git", "-C", str(root), "diff", "--raw", "--no-renames", "--no-abbrev", "-z",
                                f"{base}...HEAD"])
    if code != 0:
        raise RuntimeError(f"git diff {base}...HEAD failed: {(err or out).strip()[:300]}")
    fields = out.split("\0")
    entries = []
    i = 0
    while i + 1 < len(fields):
        meta, path = fields[i], fields[i + 1]
        if not meta.startswith(":"):
            raise RuntimeError(f"unexpected git diff --raw output near {meta[:80]!r}")
        old_mode, new_mode = meta[1:].split(" ")[:2]
        entries.append((path, old_mode, new_mode))
        i += 2
    return entries


def uncommitted_outside_data(root: Path) -> list[str]:
    """Paths with uncommitted changes (staged, unstaged or untracked, not ignored) that are not content in
    data/. Uncommitted content files are allowed for local runs; anything else is refused (SETUP)."""
    code, out, err = run_split(["git", "-C", str(root), "status", "--porcelain=v1", "-z", "--untracked-files=all"])
    if code != 0:
        raise RuntimeError(f"git status failed: {(err or out).strip()[:300]}")
    fields, paths, i = out.split("\0"), [], 0
    while i < len(fields):
        f = fields[i]
        if len(f) > 3:
            paths.append(f[3:])
            if f[0] in "RC":           # a rename or copy carries its old path in the next field
                i += 1
                if i < len(fields) and fields[i]:
                    paths.append(fields[i])
        i += 1
    def plain_content(p: str) -> bool:
        """Content by name, and on disk a regular file or gone (a deletion). A symlink, a folder or a nested
        repository named like a JSON file is code, as it is in a commit (scope_of)."""
        if not is_content(p):
            return False
        try:
            st = os.lstat(root / p)
        except FileNotFoundError:
            return True
        return stat.S_ISREG(st.st_mode)

    return sorted(p for p in set(paths) if not plain_content(p))


def changed_files(root: Path, base: str) -> list[str]:
    return [p for p, _, _ in changed_entries(root, base)]


def scope_of(entries: list[tuple[str, str, str]]) -> tuple[list[str], list[str]]:
    odd = {p for p, a, b in entries if a not in REGULAR_MODES or b not in REGULAR_MODES}
    return classify([p for p, _, _ in entries], odd)


# --- steps -------------------------------------------------------------------------------------

def step_schema(root: Path, st: Step, py: str, tools: Path | None = None) -> None:
    # YOK_CONTENT_ROOT: the validator checks ROOT's data/ even when it comes from another tools/ (CI: the base's)
    code, out = run([py, str((tools or root / "tools") / "validate_data.py")], cwd=root,
                    env={**os.environ, "YOK_CONTENT_ROOT": str(root)})
    last = tail(out, 1)
    st.summary = last[0] if last else f"exit {code}"
    if code == 0:
        st.status = "pass"
    elif code == 1:
        st.status = "fail"
        st.details = [ln for ln in tail(out, 400) if not ln.startswith("validate_data:")][:MAX_LINES]
    else:
        st.status = "setup"
        st.details = tail(out)


def step_rules(root: Path, st: Step, py: str, tools: Path | None = None) -> None:
    code, out, err = run_split([py, "-I", str((tools or root / "tools") / "check_rules.py"), "--json", str(root / "data")],
                               cwd=root)
    try:
        rep = json.loads(out)   # stdout only: --json prints one object there; stderr is shown, never parsed
    except ValueError:
        st.status, st.summary, st.details = "setup", f"check_rules gave no JSON on stdout (exit {code})", tail(out + err)
        return
    vs, confirm = (rep.get("violations"), rep.get("harness_must_confirm", [])) if isinstance(rep, dict) else (None, None)
    if not isinstance(vs, list) or not isinstance(confirm, list):
        st.status, st.summary = "setup", f"check_rules gave JSON that is not its report (exit {code})"
        st.details = tail(out + err)
        return

    def line(v):
        v = v if isinstance(v, dict) else {"message": repr(v)}
        return f"{v.get('file') or '(data)'}: {v.get('rule') or '-'} [{v.get('check')}] {v.get('message')}"

    st.summary = f"{len(vs)} violation(s), {len(confirm)} item(s) the harness must confirm"
    # The whole-run harness plays runs, not dojo trials or cancel chains: these stay runtime items for the
    # game suite and playtests, listed so the reviewer sees them (YOK-35 "harness must confirm").
    st.notes = ["runtime item, not gating and not played by the whole-run harness: " + line(v) for v in confirm]
    if code == 0 and not vs:
        st.status = "pass"
    elif code in (0, 1):   # a listed violation fails the step even if the exit code says otherwise
        st.status, st.details = "fail", [line(v) for v in vs][:MAX_LINES] or [f"check_rules exit {code} with no violation listed"]
    else:
        st.status, st.details = "setup", [line(v) for v in vs] or tail(out + err)


def prepare_godot(game: Path, godot: str, st: Step, build: bool) -> bool:
    if not build:
        return True
    code, out = run(["dotnet", "build"], cwd=game)
    if code != 0:
        st.status, st.summary, st.details = "setup", "dotnet build failed in game/", tail(out)
        return False
    code, out = run([godot, "--headless", "--path", str(game), "--import"], cwd=game)
    if code != 0:
        st.status, st.summary, st.details = "setup", "Godot import failed", tail(out)
        return False
    return True


def step_harness(root: Path, st: Step, godot: str, runs: int, seed: int) -> None:
    game = root / "game"
    with tempfile.TemporaryDirectory(prefix="yok34-harness-") as out_dir:
        code, out = run([godot, "--headless", "--path", str(game), "res://tests/run_harness.tscn", "--",
                         "--runs", str(runs), "--seed", str(seed), "--out", out_dir, "--data", str(root / "data")],
                        cwd=game)
        files = sorted(Path(out_dir).glob("*.json"))
        if code != 0 or not files:
            st.status = "fail"
            st.summary = f"the harness did not finish (exit {code}, {len(files)} result file(s))"
            st.details = [ln for ln in tail(out, 400) if "RunHarness" in ln or "Exception" in ln or "rror" in ln][:MAX_LINES] or tail(out)
            return
        if len(files) > 1:   # one batch writes one file; an extra one must never hide behind the first
            st.status = "fail"
            st.summary = f"the harness wrote {len(files)} result files; one batch writes exactly one"
            st.details = [f.name for f in files][:MAX_LINES]
            return
        try:
            batch = json.loads(files[0].read_text(encoding="utf-8"))
        except ValueError as e:
            st.status, st.summary = "fail", f"harness results are not JSON: {e}"
            return
    if not isinstance(batch, dict) or not isinstance(batch.get("runs"), list):
        st.status, st.summary = "fail", "harness results are not a batch (an object with a list of runs)"
        return
    recorded = batch["runs"]
    played = batch.get("runs_played")
    problems = []
    if any(not isinstance(r, dict) for r in recorded):
        problems.append(f"run entries that are not objects at {[i for i, r in enumerate(recorded) if not isinstance(r, dict)][:10]}")
    bad = [r.get("seed") for r in recorded if isinstance(r, dict) and r.get("outcome") not in HARNESS_OUTCOMES]
    if played != runs or len(recorded) != runs:
        problems.append(f"{played!r} runs played, {len(recorded)} recorded; asked for {runs}")
    if bad:
        problems.append(f"runs without a won/lost outcome, seeds {bad[:10]}")
    s = batch.get("summary") if isinstance(batch.get("summary"), dict) else {}
    st.notes = [f"for the Sparring Partner, not gating: reached Tanuki {s.get('reach_tanuki_pct')}% "
                f"(R5 target {s.get('R5_reach_target_pct')}), beat {s.get('beat_tanuki_pct_of_reached')}% of those "
                f"(R5 target {s.get('R5_beat_target_pct')}), runs ended by row {s.get('runs_ended_by_row')}, "
                f"fights timed out {s.get('fights_timed_out')}"]
    if problems:
        st.status, st.summary, st.details = "fail", "harness results are incomplete", problems
    else:
        st.status, st.summary = "pass", f"{runs} whole runs from seed {seed} finished with '{batch.get('bot')}'"


def step_suite(root: Path, st: Step, godot: str, log: Path) -> None:
    game = root / "game"
    code, out = run([godot, "--headless", "--path", str(game), "res://tests/test_runner.tscn"], cwd=game)
    log.write_text(out, encoding="utf-8")
    totals = [ln for ln in out.splitlines() if ln.startswith("TESTS ")]
    fails = [ln for ln in out.splitlines() if ln.startswith("FAIL ")]
    st.summary = (totals[-1] if totals else f"the test runner did not finish (exit {code})") + f" (log {log})"
    if code == 0 and totals and not fails:
        st.status = "pass"
    else:
        st.status, st.details = "fail", fails[:MAX_LINES] or tail(out)


# --- report ------------------------------------------------------------------------------------

def render(verdict: int, steps: list[Step], scope: dict) -> str:
    failed = next((s for s in steps if s.status in ("fail", "setup")), None)
    head = f"DATA MERGE CHECK: {VERDICT[verdict]}"
    if failed:
        head += f" at step {steps.index(failed) + 1} ({failed.name}: {failed.title})"
    lines = [head, ""]
    if scope.get("dirty"):
        lines.append(f"Uncommitted changes outside data/ ({len(scope['dirty'])}): commit, stash or remove them first; "
                     "the scope is read from commits, so they would not be classified")
        for p in scope["dirty"][:MAX_LINES]:
            lines.append(f"    uncommitted: {p}")
    elif scope.get("checked"):
        lines.append(f"Change: {len(scope['content'])} content file(s) in data/, {len(scope['code'])} other file(s)")
        for p in scope["code"][:MAX_LINES]:
            lines.append(f"    code (goes to review): {p}")
    else:
        lines.append("Change: scope not checked (no --base); the working tree was checked as it is")
    lines.append("")
    for i, s in enumerate(steps, 1):
        lines.append(f"  {i} {s.name:<8} {s.status.upper():<8} {s.summary}")
        for d in s.details:
            lines.append(f"      {d}")
        for n in s.notes:
            lines.append(f"      note: {n}")
    lines.append("")
    lines.append({
        MERGEABLE: ("Mergeable: every step passed on a data-only change." if scope.get("checked") else
                    "Mergeable: every step passed on this tree (scope not checked: pass --base for a PR).")
                   + " This is a verdict only; the designer reviews and merges.",
        FAIL: f"Not mergeable: fix step {steps.index(failed) + 1 if failed else '?'} ({failed.name if failed else '?'}) and run again.",
        SETUP: "The check could not run; this says nothing about the change. Fix the setup and run again.",
        REVIEW: "Checks passed, but the change touches code: it goes to code review (Code Critic, Test Breaker, designer).",
        INCOMPLETE: "No merge verdict: " + "; ".join(scope.get("incomplete") or ["steps were skipped"]) + ".",
        NOTHING: f"Nothing to merge: no file changed since {scope.get('base')}. No verdict.",
    }[verdict])
    return "\n".join(lines) + "\n"


def find_godot(arg: str | None) -> str | None:
    for c in (arg, os.environ.get("GODOT"), shutil.which("godot")):
        if c and (Path(c).is_file() or shutil.which(c)):
            return c
    return None


def main(argv=None, out=sys.stdout) -> int:
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--base")
    ap.add_argument("--root", type=Path, default=REPO)
    ap.add_argument("--tools", type=Path, help="where validate_data.py and check_rules.py come from (default ROOT/tools; "
                                               "CI passes the base branch's, kept outside the checkout)")
    ap.add_argument("--runs", type=int, default=200)
    ap.add_argument("--seed", type=int, default=1)
    ap.add_argument("--no-build", action="store_true")
    ap.add_argument("--only", default=",".join(STEPS))
    ap.add_argument("--godot")
    ap.add_argument("--python", default=sys.executable, help="interpreter with jsonschema for the schema step")
    ap.add_argument("--report", type=Path)
    ap.add_argument("--suite-log", type=Path)
    ap.add_argument("--json", action="store_true")
    a = ap.parse_args(argv)
    root = a.root.resolve()
    tools = a.tools.resolve() if a.tools else None
    only = [s.strip() for s in a.only.split(",") if s.strip()]
    unknown = [s for s in only if s not in STEPS]
    if unknown or a.runs < 1:
        out.write(f"data_merge_check: unknown step(s) {unknown} (steps: {', '.join(STEPS)})\n" if unknown
                  else "data_merge_check: --runs must be at least 1\n")
        return SETUP

    steps = [Step("schema", "JSON Schema, tools/validate_data.py"),
             Step("rules", "written rules, tools/check_rules.py"),
             Step("harness", f"whole-run harness, {a.runs} runs"),
             Step("suite", "headless Godot test suite")]
    scope = {"checked": False, "content": [], "code": [], "base": a.base}
    verdict = None
    if a.base:
        try:
            dirty = uncommitted_outside_data(root)
            entries = changed_entries(root, a.base)
        except RuntimeError as e:
            steps[0].status, steps[0].summary = "setup", str(e)
            verdict = SETUP
        else:
            if dirty:                      # designer 2026-10-17: refuse, never classify a dirty tree
                scope["dirty"] = dirty
                verdict = SETUP
            elif not entries:              # designer 2026-10-17: an empty diff is nothing to merge
                verdict = NOTHING
            else:
                content, code = scope_of(entries)
                scope.update(checked=True, content=content, code=code)

    godot = find_godot(a.godot)
    prepared = False
    for st in steps:
        if verdict is not None:
            break
        if st.name not in only:
            st.status = "skipped"
            continue
        t0 = time.monotonic()
        if st.name in ("harness", "suite"):
            if godot is None:
                st.status, st.summary = "setup", "Godot not found (pass --godot, set GODOT or put godot on PATH)"
            elif not prepared:
                prepared = prepare_godot(root / "game", godot, st, build=not a.no_build)
        if st.status == "not run":
            if st.name == "schema":
                step_schema(root, st, a.python, tools)
            elif st.name == "rules":
                step_rules(root, st, a.python, tools)
            elif st.name == "harness":
                step_harness(root, st, godot, a.runs, a.seed)
            else:
                log = a.suite_log or Path(tempfile.gettempdir()) / f"yok34-suite-{os.getpid()}-{int(time.time())}.log"
                step_suite(root, st, godot, log)
        st.seconds = time.monotonic() - t0
        if st.status == "fail":
            verdict = FAIL
        elif st.status == "setup":
            verdict = SETUP
    if verdict is None:
        reasons = []
        if any(s.status == "skipped" for s in steps):
            reasons.append("steps were skipped (--only)")
        if a.runs < FULL_RUNS:
            reasons.append(f"the harness played {a.runs} runs, not the full {FULL_RUNS}")
        if a.no_build:
            reasons.append("--no-build: the game was not freshly built")
        scope["incomplete"] = reasons
        if reasons:
            verdict = INCOMPLETE
        elif scope["checked"] and scope["code"]:
            verdict = REVIEW
        else:
            verdict = MERGEABLE

    text = render(verdict, steps, scope)
    if a.json:
        out.write(json.dumps({"verdict": VERDICT[verdict], "exit": verdict, "scope": scope,
                              "steps": [s.as_dict() for s in steps]}, indent=2) + "\n")
    else:
        out.write(text)
    if a.report:
        a.report.write_text(text, encoding="utf-8")
    summary = os.environ.get("GITHUB_STEP_SUMMARY")
    if summary:
        with open(summary, "a", encoding="utf-8") as f:
            f.write("```\n" + text + "```\n")
    return verdict


if __name__ == "__main__":
    sys.exit(main())
