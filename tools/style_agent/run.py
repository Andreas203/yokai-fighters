#!/usr/bin/env python3
"""Yokai Fighters style guide agent: Generator -> Evaluator (SCORE + REASON) -> Refiner, until the card scores 10/10.

    brief --> Generator --> text --> code measures it --> Evaluator: SCORE X/10, REASON
                                        ^                              | below target
                                        +---------- Refiner <----------+

No human steps in once a run starts: the Evaluator finds the violations, the
Refiner fixes them from the Evaluator's REASON, and the loop stops at 10/10
with no code flag, or after --max-rounds evaluations.

Usage (from anywhere):
    python tools/style_agent/run.py demo                 # the three before/after examples (cases.py)
    python tools/style_agent/run.py demo --only example-2-vocabulary-lore
    python tools/style_agent/run.py audit                # score the story cards already in data/story/
    python tools/style_agent/run.py guide                # print the style guide the agents read
    python tools/style_agent/run.py demo --replay        # rebuild a recorded run without calling a model

Every run writes runs/<name>/trace.json and trace.md plus llm-calls.json for --replay.
Standard library only; live runs need the `claude` CLI, logged in.
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
sys.path.insert(1, str(HERE.parent / "content_pipeline"))

from agents import (EVALUATOR_SYSTEM, GENERATOR_SYSTEM, REFINER_SYSTEM, clean_card, evaluator_prompt,  # noqa: E402
                    generator_prompt, measure, parse_evaluation, refiner_prompt)
from cases import CASES, SLOTS  # noqa: E402
from llm import LLM  # noqa: E402  (content_pipeline/llm.py: recorded claude -p calls)
from style_guide import render_guide  # noqa: E402

ROOT = HERE.parent.parent
RUNS = HERE / "runs"
STORY = ROOT / "data" / "story"


def evaluate(llm, args, slot: dict, text: str) -> dict:
    m = measure(text, slot)
    reply = llm.complete("evaluator", args.evaluator_model, EVALUATOR_SYSTEM, evaluator_prompt(slot, text, m))
    ev = parse_evaluation(reply)
    ev.update({"reply": reply.strip(), "measurements": {k: v for k, v in m.items() if k != "flags"}, "flags": m["flags"]})
    return ev


def run_case(llm, args, case: dict) -> dict:
    slot = SLOTS[case["slot"]]
    text = clean_card(llm.complete("generator", args.generator_model, GENERATOR_SYSTEM, generator_prompt(slot, case["brief"])))
    rounds = []
    for n in range(1, args.max_rounds + 1):
        ev = evaluate(llm, args, slot, text)
        reason = ev["reason"]
        if ev["flags"]:
            # A limit the code counted and the Evaluator let through still goes to the Refiner.
            missed = [f for f in ev["flags"] if f.split(":")[0] not in reason]
            if missed:
                reason += "\nAlso counted by code: " + "; ".join(missed) + "."
        rounds.append({"round": n, "text": text, "score": ev["score"], "reason": ev["reason"],
                       "flags": ev["flags"], "measurements": ev["measurements"], "evaluator_reply": ev["reply"]})
        print(f"    round {n}: {ev['score']}/10, {len(ev['flags'])} code flag(s)")
        if ev["score"] >= args.target and not ev["flags"]:
            break
        if n == args.max_rounds:
            break
        text = clean_card(llm.complete("refiner", args.refiner_model, REFINER_SYSTEM,
                                       refiner_prompt(slot, text, ev["score"], reason, measure(text, slot))))
    last = rounds[-1]
    return {**case, "slot_info": slot, "rounds": rounds, "before": rounds[0]["text"], "after": last["text"],
            "before_score": rounds[0]["score"], "after_score": last["score"],
            "passed": last["score"] >= args.target and not last["flags"]}


def audit_cards() -> list[dict]:
    """The narrated story cards in data/story/, each with a slot built from its own trigger and speaker.

    HUD labels and first-meeting callouts are skipped: they state a fight rule by design (Y3), and the guide is for story cards.
    """
    cards = []
    for path in sorted(STORY.glob("*.json")):
        card = json.loads(path.read_text(encoding="utf-8"))
        if card.get("kind") != "story_card" or card.get("speaker") == "HUD" or card["id"].endswith("-callout"):
            continue
        intro = card["id"].endswith("-intro")
        cards.append({"id": card["id"], "status": card.get("status", ""), "text": card["text"], "slot": {
            "card": "Elder intro story card" if intro else "Story card",
            "trigger": card.get("trigger", ""), "speaker": card.get("speaker", ""),
            "max_sentences": 1 if intro else 2, "max_chars": 140 if intro else 280,
            "data_file": f"data/story/{path.name}"}})
    return cards


# ---------- trace rendering ----------

def _cell(text) -> str:
    return str(text).replace("|", "\\|").replace("\n", "<br>")


def _quote(text: str) -> str:
    return "\n".join("> " + line for line in text.splitlines()) or ">"


def render_demo(meta: dict, results: list[dict]) -> str:
    lines = [f"# Trace: {meta['run']}", "",
             f"Command: `{meta['command']}`. Generator: {meta['generator_model']}. Evaluator: {meta['evaluator_model']}. "
             f"Refiner: {meta['refiner_model']}. Target: {meta['target']}/10, at most {meta['max_rounds']} evaluations.", "",
             "| Example | Violation class | Card slot | Before | After | Evaluations |", "|---|---|---|---|---|---|"]
    for r in results:
        lines.append(f"| {r['id']} | {r['violation_class']} | {r['slot_info']['card']} | {r['before_score']}/10 | "
                     f"{r['after_score']}/10{'' if r['passed'] else ' (not passed)'} | {len(r['rounds'])} |")
    for r in results:
        lines += ["", f"## {r['id']}: {r['violation_class']}", "",
                  f"**Card slot** (all the Evaluator and Refiner are told): {r['slot_info']['card']}. {r['slot_info']['trigger']}.", "",
                  f"**Brief given to the Generator** (built to break this class): {r['brief']}", ""]
        for x in r["rounds"]:
            who = "Generator" if x["round"] == 1 else "Refiner"
            lines += [f"### Round {x['round']}: {who}'s text, {x['score']}/10", "", _quote(x["text"]), "",
                      f"Measured by code: {x['measurements']['characters']} characters, {x['measurements']['sentences']} "
                      f"sentence(s), flags: {'; '.join(x['flags']) or 'none'}.", "",
                      "Evaluator's reply:", "", "```", x["evaluator_reply"], "```", ""]
        lines += [f"**Result:** {r['before_score']}/10 → {r['after_score']}/10"
                  + (" after " + str(len(r["rounds"]) - 1) + " refinement(s)." if len(r["rounds"]) > 1 else ".")]
    return "\n".join(lines) + "\n"


def render_audit(meta: dict, results: list[dict]) -> str:
    lines = [f"# Trace: {meta['run']}", "",
             f"Command: `{meta['command']}`. Evaluator: {meta['evaluator_model']}. Scores every narrated story card in "
             "`data/story/` (HUD labels and first-meeting callouts skipped: they state a rule by design, Y3). "
             "Nothing is rewritten.", "",
             "| Card | Status | Score | Text | Reason |", "|---|---|---|---|---|"]
    for r in results:
        lines.append(f"| `{r['id']}` | {r['status']} | {r['score']}/10 | {_cell(r['text'])} | {_cell(r['reason'])} |")
    return "\n".join(lines) + "\n"


# ---------- commands ----------

def cmd_visual(args) -> int:
    # Imported here so the story commands keep to the standard library; the visual loop needs Pillow.
    from visual_agents import Astra, VisionLLM, reference_image, seed_first_takes
    from visual_cases import CASES as VISUAL_CASES
    from visual_loop import render_visual, run_visual_case

    args.run = args.run or "visual"
    run_dir = RUNS / args.run
    run_dir.mkdir(parents=True, exist_ok=True)
    cases = [c for c in VISUAL_CASES if not args.only or c["id"] in args.only]
    if not cases:
        raise SystemExit("Nothing matched --only. Use an example id from visual_cases.py.")
    if args.seed_from and not args.replay:
        n = seed_first_takes(RUNS / args.seed_from, run_dir, [c["id"] for c in cases])
        print(f"  seeded {n} take 1 image(s) from runs/{args.seed_from}")
    vllm = VisionLLM(run_dir / "llm-calls.json", replay=args.replay)
    astra = Astra(run_dir, replay=args.replay, budget=args.max_images, model=args.image_model)
    reference = reference_image(run_dir)
    meta = {"run": args.run, "command": "visual", "image_model": args.image_model, "evaluator_model": args.evaluator_model,
            "refiner_model": args.refiner_model, "target": args.target, "max_rounds": args.max_rounds,
            "max_images": args.max_images, "refiner": "v1, no keep rule" if args.refiner_v1 else "v2, with the keep rule",
            "seeded_from": args.seed_from}
    results = []
    for case in cases:
        print(f"  {case['id']}")
        results.append(run_visual_case(vllm, astra, reference, args, case))
    (run_dir / "trace.json").write_text(json.dumps({"meta": meta, "results": results}, indent=2, ensure_ascii=False),
                                        encoding="utf-8")
    (run_dir / "trace.md").write_text(render_visual(meta, results), encoding="utf-8")
    print("\n" + ", ".join(f"{r['id']} {r['before_score']}→{r['after_score']}" for r in results)
          + f"; {astra.live} live Astra take(s), {vllm.live_calls} live model call(s), {vllm.replayed_calls} replayed.")
    print(f"Trace: {run_dir / 'trace.md'}")
    return 0 if all(r["passed"] for r in results) else 1


def main() -> int:
    for stream in (sys.stdout, sys.stderr):
        stream.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest="command", required=True)
    for name in ("demo", "audit"):
        p = sub.add_parser(name)
        p.add_argument("--run", help="run folder name under runs/ (default: the command's name)")
        p.add_argument("--only", nargs="*", help="example ids (demo) or card ids (audit)")
        p.add_argument("--generator-model", default="sonnet")
        p.add_argument("--evaluator-model", default="opus")
        p.add_argument("--refiner-model", default="sonnet")
        p.add_argument("--target", type=int, default=10, help="score that ends the loop")
        p.add_argument("--max-rounds", type=int, default=4, help="evaluations per example, including the first")
        p.add_argument("--replay", action="store_true", help="use the run's recorded model calls; call nothing")
    p = sub.add_parser("visual", help="the stage-backdrop loop: Astra takes scored by a vision Evaluator (needs Pillow)")
    p.add_argument("--run", help="run folder name under runs/ (default: visual)")
    p.add_argument("--only", nargs="*", help="example ids from visual_cases.py")
    p.add_argument("--image-model", default="gpt-6-astra")
    p.add_argument("--evaluator-model", default="opus")
    p.add_argument("--refiner-model", default="sonnet")
    p.add_argument("--target", type=int, default=10)
    p.add_argument("--max-rounds", type=int, default=3, help="takes per asset, including the first")
    p.add_argument("--max-images", type=int, default=9, help="live Astra takes allowed for the whole run (the approved budget)")
    p.add_argument("--refiner-v1", action="store_true",
                   help="the Refiner prompt without the keep rule, as runs/visual was recorded (to replay it)")
    p.add_argument("--seed-from", help="reuse each example's take 1 and its evaluation from this run folder (no new take)")
    p.add_argument("--replay", action="store_true", help="use the run's recorded takes and model calls; call nothing")
    sub.add_parser("guide")
    sub.add_parser("visual-guide")
    args = parser.parse_args()
    if args.command == "guide":
        print(render_guide())
        return 0
    if args.command == "visual-guide":
        from visual_guide import render_guide as render_visual_guide
        print(render_visual_guide())
        return 0
    if args.command == "visual":
        return cmd_visual(args)

    args.run = args.run or args.command
    run_dir = RUNS / args.run
    run_dir.mkdir(parents=True, exist_ok=True)
    llm = LLM(run_dir / "llm-calls.json", replay=args.replay)
    meta = {"run": args.run, "command": args.command, "generator_model": args.generator_model,
            "evaluator_model": args.evaluator_model, "refiner_model": args.refiner_model,
            "target": args.target, "max_rounds": args.max_rounds}

    if args.command == "demo":
        cases = [c for c in CASES if not args.only or c["id"] in args.only]
        if not cases:
            raise SystemExit("Nothing matched --only. Use an example id from cases.py.")
        results = []
        for case in cases:
            print(f"  {case['id']}")
            results.append(run_case(llm, args, case))
        md = render_demo(meta, results)
        ok = all(r["passed"] for r in results)
        summary = ", ".join(f"{r['id']} {r['before_score']}→{r['after_score']}" for r in results)
    else:
        results = []
        for card in audit_cards():
            if args.only and card["id"] not in args.only:
                continue
            ev = evaluate(llm, args, card["slot"], card["text"])
            print(f"  {card['id']}: {ev['score']}/10")
            results.append({**card, "score": ev["score"], "reason": ev["reason"], "flags": ev["flags"]})
        md = render_audit(meta, results)
        ok = True
        summary = f"{len(results)} cards scored"

    (run_dir / "trace.json").write_text(json.dumps({"meta": meta, "results": results}, indent=2, ensure_ascii=False),
                                        encoding="utf-8")
    (run_dir / "trace.md").write_text(md, encoding="utf-8")
    print(f"\n{summary}; {llm.live_calls} live model call(s), {llm.replayed_calls} replayed.")
    print(f"Trace: {run_dir / 'trace.md'}")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
