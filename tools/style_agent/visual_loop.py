"""The visual loop: Astra take -> pixel measurements -> vision Evaluator (SCORE + REASON) -> Refiner -> Astra take ...

Stops at the target score with no code flag, after --max-rounds takes per asset, or when the
image budget (--max-images live Astra takes for the whole run) is spent. Nobody steps in.
"""
from __future__ import annotations

from pathlib import Path

from agents import clean_card, parse_evaluation
from image_tools import measure_image
from visual_agents import (EVALUATOR_SYSTEM, GENERATOR_NOTE, REFINER_SYSTEM, BudgetSpent, evaluator_prompt,
                           refiner_prompt, wrap_prompt)
from visual_cases import SLOTS


def run_visual_case(vllm, astra, reference: Path, args, case: dict) -> dict:
    slot = SLOTS[case["slot"]]
    prompt = f"{GENERATOR_NOTE} {slot['subject']} {case['brief']}"
    scene = prompt   # what the Refiner rewrites: the whole first prompt, then its own scene descriptions
    take = astra.render(prompt, f"{case['id']}-take1")
    rounds, stopped = [], ""
    best = None   # (score, round) of the best take so far; the Refiner always works from it
    for n in range(1, args.max_rounds + 1):
        m = measure_image(take)
        reply = vllm.complete_with_images("evaluator", args.evaluator_model, EVALUATOR_SYSTEM, evaluator_prompt(slot, m),
                                          [reference, take])
        ev = parse_evaluation(reply)
        reason = ev["reason"]
        missed = [f for f in m["flags"] if f.split(":")[0] not in reason]
        if missed:
            reason += "\nAlso measured by code: " + "; ".join(missed) + "."
        rounds.append({"round": n, "prompt": prompt, "image": take.name, "score": ev["score"], "reason": ev["reason"],
                       "flags": m["flags"], "measurements": {k: v for k, v in m.items() if k != "flags"},
                       "evaluator_reply": reply.strip()})
        print(f"    take {n}: {ev['score']}/10, {len(m['flags'])} code flag(s)")
        rank = ev["score"] - (10 if m["flags"] else 0)   # a flagged take never beats a clean one
        if best is None or rank > best[0]:
            best = (rank, n, scene, ev["score"], reason, m)
        if ev["score"] >= args.target and not m["flags"]:
            break
        if n == args.max_rounds:
            stopped = "take cap"
            break
        # A rewrite can overshoot and score lower; the next one starts again from the best take, not the last.
        _, _, base_scene, base_score, base_reason, base_m = best
        scene = clean_card(vllm.complete("refiner", args.refiner_model, REFINER_SYSTEM,
                                         refiner_prompt(slot, base_scene, base_score, base_reason, base_m,
                                                        keep=not getattr(args, "refiner_v1", False))))
        prompt = wrap_prompt(scene)
        try:
            take = astra.render(prompt, f"{case['id']}-take{n + 1}")
        except BudgetSpent as e:
            stopped = str(e)
            break
    kept = rounds[best[1] - 1]   # the loop's output is its best take, not its last
    return {**case, "slot_info": slot, "rounds": rounds, "before_score": rounds[0]["score"], "after_score": kept["score"],
            "best_take": kept["round"], "passed": kept["score"] >= args.target and not kept["flags"], "stopped": stopped}


def _cell(text) -> str:
    return str(text).replace("|", "\\|").replace("\n", "<br>")


def render_visual(meta: dict, results: list[dict]) -> str:
    lines = [f"# Trace: {meta['run']}", "",
             f"Command: `{meta['command']}`. Generator: {meta['image_model']} (via `codex exec`). Evaluator: "
             f"{meta['evaluator_model']} (vision). Refiner: {meta['refiner_model']}. Target {meta['target']}/10, at most "
             f"{meta['max_rounds']} takes per asset and {meta['max_images']} live takes in all. Refiner prompt: "
             f"{meta.get('refiner', 'v1, no keep rule')}."
             + (f" Take 1 of each example is reused from `runs/{meta['seeded_from']}` (same brief, same picture; not paid "
                "for again)." if meta.get("seeded_from") else ""), "",
             "The Evaluator sees two images: the accepted bamboo grove backdrop as REFERENCE "
             "([`images/reference-bamboo-grove.png`](images/reference-bamboo-grove.png)), then the take.", "",
             "| Example | Violation class | Asset slot | Before | After (best take) | Takes |", "|---|---|---|---|---|---|"]
    for r in results:
        after = (f"{r['after_score']}/10, take {r['best_take']}"
                 + ("" if r["passed"] else f" (target not reached: {r['stopped'] or 'below target'})"))
        lines.append(f"| {r['id']} | {r['violation_class']} | {r['slot_info']['asset']} | {r['before_score']}/10 | {after} | {len(r['rounds'])} |")
    for r in results:
        lines += ["", f"## {r['id']}: {r['violation_class']}", "",
                  f"**Asset slot** (all the Evaluator and Refiner are told): {r['slot_info']['asset']}. {r['slot_info']['subject']}", "",
                  f"**Brief given to Astra** (built to break this class): {r['brief']}", ""]
        for x in r["rounds"]:
            who = "Astra from the sabotaged brief" if x["round"] == 1 else "Astra from the Refiner's prompt"
            m = x["measurements"]
            lines += [f"### Take {x['round']}: {who}, {x['score']}/10", "",
                      f"![{r['id']} take {x['round']}](images/{x['image']})", "",
                      f"Prompt: {x['prompt']}", "",
                      f"Measured by code: {m['width']}x{m['height']}, on-palette {m['on_palette_share']:.0%}, flatness "
                      f"{m['flatness']:.0%}, lower third edge {m['lower_third_edge']} vs upper {m['upper_edge']}, neon "
                      f"{m['neon_share']:.1%}. Flags: {'; '.join(x['flags']) or 'none'}.", "",
                      "Evaluator's reply:", "", "```", x["evaluator_reply"], "```", ""]
        lines.append(f"**Result:** {r['before_score']}/10 → {r['after_score']}/10 (take {r['best_take']}, the best of "
                     f"{len(r['rounds'])}).")
    return "\n".join(lines) + "\n"
