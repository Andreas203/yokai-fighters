#!/usr/bin/env python3
"""Goal-oriented coding agent for Yokai Fighters.

The goal: every feature the design requires is found in the codebase.

  read the design -> scan the code -> find the gaps -> score them -> write the top one
        ^                                                                  |
        +---------- re-scan after every write, stop when none is left -----+

Usage (from the repo root):
  python tools/goal_agent/run.py gaps                      what is missing and what comes next (no model calls)
  python tools/goal_agent/run.py build --run NAME          write the top-ranked feature, then re-scan and repeat
  python tools/goal_agent/run.py build --max-features 3    ...for up to three features
  python tools/goal_agent/run.py critic-test --run NAME    break accepted code on purpose and see who notices

`build` needs the `claude` CLI (logged in), `dotnet` and `godot` on PATH, and a branch
other than main: the agent writes to the working tree and never commits, pushes or merges.
"""
from __future__ import annotations

import argparse
import datetime
import json
import re
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent.parent
sys.path.insert(0, str(HERE))
sys.path.insert(0, str(HERE.parent / "content_pipeline"))

import critics  # noqa: E402
import gdd  # noqa: E402
import priority  # noqa: E402
import scan as scanner  # noqa: E402
import verify  # noqa: E402
import writer  # noqa: E402
from critics import Issue  # noqa: E402
from llm import LLM  # noqa: E402

RUNS = HERE / "runs"
MAX_ATTEMPTS = 4   # the GDD budgets about four revisions per code ticket (10.3)


class Trace:
    """Everything the agent decides, written as it happens to runs/<name>/trace.md and the console."""

    def __init__(self, path: Path | None):
        self.path = path
        self.lines: list[str] = []

    def add(self, text: str = "", echo: bool = True) -> None:
        self.lines.append(text)
        if echo:
            print(text, flush=True)
        if self.path:
            self.path.write_text("\n".join(self.lines) + "\n", encoding="utf-8")


def git(*args: str) -> str:
    return subprocess.run(["git", *args], cwd=ROOT, capture_output=True, text=True).stdout.strip()


def current_week(design, override: int | None) -> tuple[int, str]:
    """The build week. Week 1 starts at the repository's first commit."""
    if override:
        return override, "given with --week"
    first = git("log", "--reverse", "--format=%as").split("\n")[0]
    start = datetime.date.fromisoformat(first)
    week = (datetime.date.today() - start).days // 7 + 1
    return max(1, min(week, design.last_week)), f"first commit {first}, today {datetime.date.today()}"


def load(week_override: int | None):
    design = gdd.load(ROOT)
    manifest = scanner.load_features(ROOT)
    problems = scanner.validate_manifest(manifest, design, ROOT)
    if problems:
        raise SystemExit("tools/goal_agent/features.json does not match the design:\n  " + "\n  ".join(problems))
    week, why = current_week(design, week_override)
    return design, manifest["features"], week, why


def gap_table(gaps: dict, ranked: list) -> list[str]:
    present = [g for g in gaps.values() if g.status == "present"]
    lines = [f"Design requires {len(gaps)} features: {len(present)} present, {len(gaps) - len(present)} not.", ""]
    if not ranked:
        return lines
    lines += ["| # | Feature | Status | Score | Can the agent write it now? |", "|---|---|---|---|---|"]
    for n, s in enumerate(ranked, 1):
        g = gaps[s.feature_id]
        if s.eligible:
            can = "yes"
        elif s.blocked_by:
            can = "blocked by " + ", ".join(s.blocked_by)
        else:
            can = "no: " + s.route
        lines.append(f"| {n} | `{s.feature_id}` {g.feature['title']} ({', '.join(g.feature['rules'])}) | {g.status} | {s.total} | {can} |")
    return lines


def score_lines(s) -> list[str]:
    out = ["| Factor | Points | Why |", "|---|---|---|"]
    out += [f"| {factor} | {points:+d} | {why} |" for factor, points, why in s.parts]
    out.append(f"| **total** | **{s.total}** | |")
    return out


def cmd_gaps(args) -> int:
    design, features, week, why = load(args.week)
    s = scanner.scan(ROOT)
    gaps = scanner.detect_gaps(features, s)
    ranked = priority.rank(gaps, design, week)
    if args.json:
        print(json.dumps({
            "week": week,
            "scan": s.summary(),
            "gaps": [{"id": sc.feature_id, "title": gaps[sc.feature_id].feature["title"],
                      "rules": gaps[sc.feature_id].feature["rules"], "owner": gaps[sc.feature_id].feature["owner"],
                      "status": gaps[sc.feature_id].status, "score": sc.total, "eligible": sc.eligible,
                      "blocked_by": sc.blocked_by, "route": sc.route, "unmet": gaps[sc.feature_id].unmet,
                      "why": [{"factor": f, "points": p, "reason": w} for f, p, w in sc.parts]} for sc in ranked],
        }, indent=2, ensure_ascii=False))
        return 0
    print(f"Design: {len(design.rules)} rules, {len(design.amendments)} amendments. Never cut (G1): {', '.join(design.never_cut)}.")
    print(f"Cut order (G2, G3): {' > '.join(design.cut_order)}.")
    print(f"Build week {week} of {design.last_week} ({why}).")
    sm = s.summary()
    print(f"Scan: {sm['cs_files']} C# files, {sm['types']} types, {sm['tests']} tests, {sm['data_files']} data files.\n")
    print("\n".join(gap_table(gaps, ranked)))
    top = next((sc for sc in ranked if sc.eligible), None)
    if top:
        print(f"\nNext: `{top.feature_id}`\n" + "\n".join(score_lines(top)))
    return 0


# ---------------------------------------------------------------- the build loop

def run_adversaries(llm, args, design, feature, change, attempt, standing, disputed, baseline, trace):
    """Both model critics on a change that already builds and passes. Returns (issues, probe text shown to the writer)."""
    issues: list[Issue] = []
    found, dropped = critics.code_critic(llm, args.critic_model, ROOT, feature, design, change)
    trace.add(f"- code-critic: {len(found)} finding(s) kept, {len(dropped)} thrown away as unverifiable")
    for d in dropped:
        trace.add(f"  - thrown away: {d}")
    issues += found

    for name, why in change.disputes.items():
        if re.search(r"\b[A-Z]\d+\b", why):
            disputed[name] = why
            trace.add(f"- writer disputes probe `{name}`: {why}")
        else:
            trace.add(f"- writer's dispute of `{name}` names no rule ID and is ignored")

    class_name = f"{critics.PROBE_CLASS}{attempt}"
    source, notes = critics.write_probes(llm, args.breaker_model, ROOT, feature, design, change, class_name,
                                         lambda m: trace.add(f"- {m}"))
    if notes and notes.lower() != "none":
        issues.append(Issue("test-breaker", False, "author tests", "tests", notes))
    carried = []
    for cls, src, failing in standing:
        still = failing - set(disputed)
        if still:
            (ROOT / critics.probe_path(cls)).write_text(src, encoding="utf-8")
            carried.append((cls, src, still))
    errors = verify.build(ROOT)
    for cls, src, still in list(carried):
        if any(cls in e for e in errors):   # the rewrite changed an API an old probe used
            (ROOT / critics.probe_path(cls)).unlink()
            carried.remove((cls, src, still))
            trace.add(f"- {cls}: earlier probes no longer compile against the new attempt and are dropped")
    run = verify.run_tests(ROOT)
    probe_text = ""
    new_standing = []
    if source:
        failing = critics.judge_probes(run, class_name, source)
        n = len(critics.probe_rules(source))
        trace.add(f"- test-breaker: {n} probe(s) compiled and ran, {len(failing)} failed")
        issues += failing
        if failing:
            new_standing.append((class_name, source, {i.where.split()[-1] for i in failing}))
            probe_text += f"// {critics.probe_path(class_name)}\n{source}\n"
    for cls, src, still in carried:
        failing = critics.judge_probes(run, cls, src, only=still)
        trace.add(f"- {cls}: {len(still)} earlier failing probe(s) re-run, {len(failing)} still fail")
        issues += failing
        if failing:
            new_standing.append((cls, src, {i.where.split()[-1] for i in failing}))
            probe_text += f"// {critics.probe_path(cls)}\n{src}\n"
    critics.remove_probes(ROOT)
    standing[:] = new_standing
    return issues, probe_text


def build_feature(llm, args, design, feature, gap, baseline, trace) -> dict:
    """The code-writing step for one feature: write, verify, attack, repair. Returns what happened."""
    system = writer.system_prompt(ROOT)
    base = writer.user_prompt(ROOT, feature, design, gap.unmet, design.open_questions)
    prompt = base
    standing: list = []          # failing probes carried to the next attempt
    disputed: dict[str, str] = {}
    outcome = {"feature": feature["id"], "accepted": False, "attempts": [], "disputed": disputed, "advisory": [], "files": []}

    for attempt in range(1, args.attempts + 1):
        trace.add(f"\n#### Attempt {attempt}")
        reply = llm.complete("writer", args.writer_model, system, prompt)
        change = writer.parse(reply)
        trace.add(f"- writer returned {len(change.files)} new file(s), {len(change.edits)} edited: {', '.join(change.paths) or 'nothing'}")
        if change.notes and change.notes.lower() != "none":
            trace.add(f"- writer's notes: {' '.join(change.notes.split())}")
        issues = [Issue("format", True, "", "", p) for p in writer.check_paths(ROOT, change, feature)]
        probe_text, snapshot = "", None
        if not issues:
            snapshot = writer.apply(ROOT, change, feature)
            issues += critics.code_checks(ROOT, change)
            errors = verify.build(ROOT)
            if errors:
                issues += [Issue("build", True, "", "", e) for e in errors[:12]]
                trace.add(f"- build: {len(errors)} compiler error(s)")
            else:
                run = verify.run_tests(ROOT)
                new_failures = {t: m for t, m in run.failed.items() if t not in baseline.failed}
                trace.add(f"- build ok; tests: {run.summary or 'the runner did not finish'}"
                          + (f" ({len(new_failures)} not failing before)" if new_failures else ""))
                if not run.ran:
                    issues.append(Issue("tests", True, "", "", "the test runner did not finish (crash or timeout)"))
                issues += [Issue("tests", True, t, "", f"fails: {m}") for t, m in sorted(new_failures.items())]
                rescan = scanner.detect_gaps([feature], scanner.scan(ROOT))[feature["id"]]
                trace.add(f"- re-scan: `{feature['id']}` is {rescan.status}")
                issues += [Issue("scan", True, "", "", f"the scan still cannot find {u}") for u in rescan.unmet]
            if not issues:
                found, probe_text = run_adversaries(llm, args, design, feature, change, attempt, standing, disputed, baseline, trace)
                issues += found
        blocking = [i for i in issues if i.blocking]
        advisory = [i for i in issues if not i.blocking]
        for i in issues:
            trace.add(f"  - {i.render()}")
        outcome["attempts"].append({"attempt": attempt, "files": change.paths,
                                    "blocking": [i.render() for i in blocking], "advisory": [i.render() for i in advisory]})
        if not blocking:
            outcome.update(accepted=True, files=change.paths, advisory=[i.render() for i in advisory],
                           notes=change.notes, map_entry=change.map_entry)
            trace.add(f"- **accepted** on attempt {attempt}: no blocking finding left")
            return outcome
        if snapshot is not None:
            writer.revert(ROOT, snapshot)
        critics.remove_probes(ROOT)
        trace.add(f"- rejected: {len(blocking)} blocking finding(s); the attempt is removed from the repo")
        prompt = writer.repair_prompt(base, reply, blocking + advisory, probe_text, attempt + 1)  # advisory: take or leave
    trace.add(f"- **gave up** after {args.attempts} attempts; nothing of this feature is left in the repo")
    return outcome


def cmd_build(args) -> int:
    design, features, week, why = load(args.week)
    branch = git("branch", "--show-current")
    if branch in ("main", "master"):
        raise SystemExit("The goal agent does not write on main. Make a branch first (YOK-<number>-<brief-name>).")
    run_dir = RUNS / args.run
    run_dir.mkdir(parents=True, exist_ok=True)
    llm = LLM(run_dir / "llm-calls.json", timeout=1500)
    trace = Trace(run_dir / "trace.md")
    trace.add(f"# Goal agent run `{args.run}`\n")
    trace.add(f"Branch `{branch}` at `{git('rev-parse', '--short', 'HEAD')}`. Build week {week} of {design.last_week} ({why}).")
    trace.add(f"Models: writer `{args.writer_model}`, code-critic `{args.critic_model}`, test-breaker `{args.breaker_model}`. "
              f"Up to {args.max_features} feature(s), {args.attempts} attempts each.")
    trace.add(f"\nRead the design: {len(design.rules)} rules, {len(design.amendments)} amendments. "
              f"Never cut (G1): {', '.join(design.never_cut)}. Cut order (G2, G3): {' > '.join(design.cut_order)}.")

    critics.remove_probes(ROOT)
    errors = verify.build(ROOT)
    if errors:
        raise SystemExit("The game does not build before the agent has written anything:\n  " + "\n  ".join(errors[:10]))
    baseline = verify.run_tests(ROOT)
    if not baseline.ran:
        raise SystemExit("The game's test runner did not finish. Try: godot --headless --path game --import")
    trace.add(f"Before any write: build ok, {baseline.summary}"
              + (f". Already failing, not held against the agent: {', '.join(sorted(baseline.failed))}." if baseline.failed else "."))

    outcomes, gave_up = [], set()
    iteration, stop = 0, ""
    while True:
        iteration += 1
        s = scanner.scan(ROOT)
        gaps = scanner.detect_gaps(features, s)
        ranked = priority.rank(gaps, design, week)
        (run_dir / f"scan-{iteration}.json").write_text(s.to_json(), encoding="utf-8")
        sm = s.summary()
        trace.add(f"\n## Iteration {iteration}\n")
        trace.add(f"Scan: {sm['cs_files']} C# files, {sm['types']} types, {sm['tests']} tests, {sm['data_files']} data files.")
        trace.add("\n".join(gap_table(gaps, ranked)))
        candidates = [sc for sc in ranked if sc.eligible and sc.feature_id not in gave_up
                      and (not args.only or sc.feature_id in args.only)]
        if not ranked:
            stop = "every feature the design requires is in the codebase"
        elif not candidates:
            waiting = [sc for sc in ranked if not sc.eligible]
            stop = (f"nothing is left that the agent can write: {len(waiting)} gap(s) wait on another agent, "
                    f"a build brief or a blocked prerequisite" + (f", {len(gave_up)} it gave up on" if gave_up else ""))
        elif len(outcomes) >= args.max_features:
            stop = f"the budget of {args.max_features} feature(s) for this run is spent"
        if stop:
            break
        pick = candidates[0]
        feature = gaps[pick.feature_id].feature
        trace.add(f"\n### Chosen: `{pick.feature_id}` — {feature['title']}\n")
        trace.add("\n".join(score_lines(pick)))
        if len(candidates) > 1:
            trace.add("\nRunners-up: " + ", ".join(f"`{c.feature_id}` {c.total}" for c in candidates[1:4]) + ".")
        outcome = build_feature(llm, args, design, feature, gaps[pick.feature_id], baseline, trace)
        outcome["score"] = {"total": pick.total, "parts": pick.parts}
        outcomes.append(outcome)
        if not outcome["accepted"]:
            gave_up.add(pick.feature_id)
        (run_dir / "outcomes.json").write_text(json.dumps(outcomes, indent=2, ensure_ascii=False), encoding="utf-8")

    trace.add(f"\n## Stopped\n\nThe loop stopped because {stop}.\n")
    for o in outcomes:
        state = f"accepted on attempt {len(o['attempts'])}" if o["accepted"] else f"given up after {len(o['attempts'])} attempts"
        trace.add(f"- `{o['feature']}`: {state}" + (f"; files: {', '.join(o['files'])}" if o["files"] else ""))
        for a in o["advisory"]:
            trace.add(f"  - suggested change, not blocking: {a}")
        for name, why_ in o["disputed"].items():
            trace.add(f"  - for the designer: the writer disputed probe `{name}`: {why_}")
    trace.add(f"\nModel calls: {llm.live_calls} live, {llm.replayed_calls} read back from this run's record. "
              "Nothing was committed: review the working tree, then the designer decides the merge.")
    return 0 if all(o["accepted"] for o in outcomes) else 1


# ---------------------------------------------------------------- critic test: break accepted code on purpose

def cmd_critic_test(args) -> int:
    design, features, _, _ = load(None)
    faults = json.loads((HERE / "seeded_faults.json").read_text(encoding="utf-8"))["faults"]
    by_id = {f["id"]: f for f in features}
    run_dir = RUNS / args.run
    run_dir.mkdir(parents=True, exist_ok=True)
    llm = LLM(run_dir / "llm-calls.json", replay=args.replay, timeout=1500)
    trace = Trace(run_dir / "trace.md")
    trace.add(f"# Critic test `{args.run}`\n")
    trace.add("Each fault below is put into code the agent had accepted, the gates are run as in a real attempt, "
              "and the fault is taken out again. A fault counts as caught when a blocking finding names it.\n")
    critics.remove_probes(ROOT)
    baseline = verify.run_tests(ROOT) if not verify.build(ROOT) else None
    if baseline is None or not baseline.ran:
        raise SystemExit("The game has to build and its tests have to run before faults are seeded.")
    rows, caught_all = [], True
    for n, fault in enumerate(faults, 1):
        feature = by_id[fault["feature"]]
        path = ROOT / fault["file"]
        original = path.read_text(encoding="utf-8")
        trace.add(f"## Fault {n}: {fault['what']}\n\n`{fault['file']}`, breaks {fault['rule']}.\n")
        if original.count(fault["search"]) != 1:
            trace.add("- the text this fault replaces is no longer in the file exactly once; fault skipped")
            rows.append((fault["what"], "skipped", "", "", ""))
            continue
        # The critics get the feature's files whole, as in a real attempt, and no hint of where the fault is.
        change = writer.Change(edits={p: [] for p in feature["build"]["files"] if (ROOT / p).is_file()},
                               notes="none", map_entry="seeded fault")
        by = {"code-check": 0, "tests": 0, "code-critic": 0, "test-breaker": 0}
        try:
            path.write_text(original.replace(fault["search"], fault["replace"], 1), encoding="utf-8")
            issues = critics.code_checks(ROOT, change)
            errors = verify.build(ROOT)
            if errors:
                trace.add(f"- the fault does not compile: {errors[0]}")
                rows.append((fault["what"], "does not compile", "", "", ""))
                continue
            run = verify.run_tests(ROOT)
            issues += [Issue("tests", True, t, "", f"fails: {m}") for t, m in sorted(run.failed.items()) if t not in baseline.failed]
            standing: list = []
            found, _ = run_adversaries(llm, args, design, feature, change, n, standing, {}, baseline, trace)
            issues += found
            for i in issues:
                if i.blocking:
                    by[i.source] = by.get(i.source, 0) + 1
                trace.add(f"  - {i.render()}")
        finally:
            path.write_text(original, encoding="utf-8")
            critics.remove_probes(ROOT)
        caught = by["code-critic"] + by["test-breaker"] + by["code-check"] > 0
        caught_all &= caught
        rows.append((fault["what"], f"{by['tests']} failing", f"{by['code-critic']} finding(s)",
                     f"{by['test-breaker']} failing probe(s)", "caught" if caught else "MISSED by the critics"))
    trace.add("\n## Result\n\n| Seeded fault | Author's tests | code-critic | test-breaker | Critics |\n|---|---|---|---|---|")
    for r in rows:
        trace.add("| " + " | ".join(r) + " |")
    trace.add(f"\nModel calls: {llm.live_calls} live, {llm.replayed_calls} read back. Every fault was removed again.")
    verify.build(ROOT)
    return 0 if caught_all else 1


def main() -> int:
    for stream in (sys.stdout, sys.stderr):
        stream.reconfigure(encoding="utf-8")
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)

    g = sub.add_parser("gaps", help="scan, find gaps, rank them (no model calls)")
    g.add_argument("--week", type=int, help="build week to rank for (default: from the first commit's date)")
    g.add_argument("--json", action="store_true", help="machine-readable, for the producer")
    g.set_defaults(fn=cmd_gaps)

    def models(p):
        p.add_argument("--writer-model", default="opus")
        p.add_argument("--critic-model", default="opus")
        p.add_argument("--breaker-model", default="sonnet")

    b = sub.add_parser("build", help="write the top-ranked feature, re-scan, repeat")
    b.add_argument("--run", required=True, help="name of the run folder under tools/goal_agent/runs/")
    b.add_argument("--max-features", type=int, default=1, help="features to attempt before stopping (default 1)")
    b.add_argument("--attempts", type=int, default=MAX_ATTEMPTS, help=f"attempts per feature (default {MAX_ATTEMPTS})")
    b.add_argument("--only", nargs="*", help="limit the choice to these feature ids")
    b.add_argument("--week", type=int)
    models(b)
    b.set_defaults(fn=cmd_build)

    c = sub.add_parser("critic-test", help="seed faults into accepted code and see which critic catches them")
    c.add_argument("--run", default="critic-seeded-faults")
    c.add_argument("--replay", action="store_true", help="use the recorded model replies (probes are still compiled and run)")
    models(c)
    c.set_defaults(fn=cmd_critic_test)

    args = ap.parse_args()
    return args.fn(args)


if __name__ == "__main__":
    sys.exit(main())
