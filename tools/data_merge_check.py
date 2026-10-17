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
  0 MERGEABLE   every step passed and the change touches only content in data/
  1 FAIL        a step failed; the report names it and why
  2 SETUP       the check could not run (no Godot, no jsonschema, build failed, bad --base)
  3 REVIEW      every step passed but the change also touches code (anything but a .json file in data/,
                or data/schema/; a rename counts both paths): it goes to code review (Code Critic, Test Breaker, designer)
  4 INCOMPLETE  every step that ran passed, but --only skipped some: not a merge verdict

MERGEABLE is a verdict only. Nothing here merges: the designer still reviews and merges every
data PR until they say otherwise (CLAUDE.md, Pipeline).

--base REF   compare HEAD with REF (git diff REF...HEAD in ROOT) to decide data-only vs code.
             Without it the scope is not checked and the best verdict is MERGEABLE for the tree.
--root DIR   the checkout to check (default: this repo). Its tools/ and data/ are used.
--only       comma list of steps to run (schema,rules,harness,suite), for local iteration.
--no-build   skip `dotnet build` and the Godot import before the Godot steps.
"""
from __future__ import annotations

import argparse
import json
import os
import shutil
import subprocess
import sys
import tempfile
import time
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
STEPS = ("schema", "rules", "harness", "suite")
MERGEABLE, FAIL, SETUP, REVIEW, INCOMPLETE = 0, 1, 2, 3, 4
VERDICT = {MERGEABLE: "MERGEABLE", FAIL: "FAIL", SETUP: "SETUP", REVIEW: "REVIEW", INCOMPLETE: "INCOMPLETE"}
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


def run_split(cmd: list[str], cwd: Path | None = None, timeout: int = 1800) -> tuple[int, str, str]:
    """(exit code, stdout, stderr)."""
    try:
        p = subprocess.run(cmd, cwd=cwd, capture_output=True, text=True, timeout=timeout)
    except FileNotFoundError as e:
        return 127, "", f"{e}"
    except subprocess.TimeoutExpired:
        return 124, "", f"timed out after {timeout} s: {' '.join(cmd)}"
    return p.returncode, p.stdout or "", p.stderr or ""


def run(cmd: list[str], cwd: Path | None = None, timeout: int = 1800) -> tuple[int, str]:
    """(exit code, stdout + stderr), for output that is shown, never parsed."""
    code, out, err = run_split(cmd, cwd, timeout)
    return code, out + err


# --- scope: data-only or code ----------------------------------------------------------------

def is_content(path: str) -> bool:
    """Content the data path may carry: a JSON file under data/, outside data/schema/ (the C# loader's
    contract). Anything else under data/ (scripts, binaries, other text) goes to review like code."""
    p = path.replace("\\", "/")
    return p.startswith("data/") and not p.startswith("data/schema/") and p.endswith(".json")


def classify(paths: list[str]) -> tuple[list[str], list[str]]:
    content = sorted(p for p in paths if is_content(p))
    code = sorted(p for p in paths if not is_content(p))
    return content, code


def changed_files(root: Path, base: str) -> list[str]:
    # --no-renames: a rename is a delete plus an add, so both paths are classified (a test moved out of
    # game/ into data/ is a code change). -z: paths come raw, never quoted.
    code, out, err = run_split(["git", "-C", str(root), "diff", "--no-renames", "--name-only", "-z", f"{base}...HEAD"])
    if code != 0:
        raise RuntimeError(f"git diff {base}...HEAD failed: {(err or out).strip()[:300]}")
    return [p for p in out.split("\0") if p]


# --- steps -------------------------------------------------------------------------------------

def step_schema(root: Path, st: Step, py: str) -> None:
    code, out = run([py, str(root / "tools" / "validate_data.py")], cwd=root)
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


def step_rules(root: Path, st: Step, py: str) -> None:
    code, out, err = run_split([py, "-I", str(root / "tools" / "check_rules.py"), "--json", str(root / "data")], cwd=root)
    try:
        rep = json.loads(out)   # stdout only: --json prints one object there; stderr is shown, never parsed
    except ValueError:
        st.status, st.summary, st.details = "setup", f"check_rules gave no JSON on stdout (exit {code})", tail(out + err)
        return
    vs, confirm = rep.get("violations", []), rep.get("harness_must_confirm", [])

    def line(v):
        return f"{v.get('file') or '(data)'}: {v.get('rule') or '-'} [{v.get('check')}] {v.get('message')}"

    st.summary = f"{len(vs)} violation(s), {len(confirm)} item(s) the harness must confirm"
    # The whole-run harness plays runs, not dojo trials or cancel chains: these stay runtime items for the
    # game suite and playtests, listed so the reviewer sees them (YOK-35 "harness must confirm").
    st.notes = ["runtime item, not gating and not played by the whole-run harness: " + line(v) for v in confirm]
    if code == 0:
        st.status = "pass"
    elif code == 1:
        st.status, st.details = "fail", [line(v) for v in vs][:MAX_LINES]
    else:
        st.status, st.details = "setup", [line(v) for v in vs] or tail(out)


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
        try:
            batch = json.loads(files[0].read_text(encoding="utf-8"))
        except ValueError as e:
            st.status, st.summary = "fail", f"harness results are not JSON: {e}"
            return
    played = batch.get("runs_played")
    bad = [r.get("seed") for r in batch.get("runs", []) if r.get("outcome") not in HARNESS_OUTCOMES]
    problems = []
    if played != runs or len(batch.get("runs", [])) != runs:
        problems.append(f"{played!r} runs played, {len(batch.get('runs', []))} recorded; asked for {runs}")
    if bad:
        problems.append(f"runs without a won/lost outcome, seeds {bad[:10]}")
    s = batch.get("summary", {})
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
    if scope.get("checked"):
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
        INCOMPLETE: "Some steps were skipped (--only): no merge verdict.",
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
    scope = {"checked": False, "content": [], "code": []}
    verdict = None
    if a.base:
        try:
            content, code = classify(changed_files(root, a.base))
            scope.update(checked=True, content=content, code=code)
        except RuntimeError as e:
            steps[0].status, steps[0].summary = "setup", str(e)
            verdict = SETUP

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
                step_schema(root, st, a.python)
            elif st.name == "rules":
                step_rules(root, st, a.python)
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
        if any(s.status == "skipped" for s in steps):
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
