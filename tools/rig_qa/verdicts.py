"""YOK-finish-qa: PASS / FIX per body and clip from a rank.py summary, as Markdown table rows.

Thresholds (the game's pose, cm): sink > 4 on a standing frame (air clips skipped; knockdown/get-up: the lying body),
float > 4 on a standing clip, planted-foot drift > 10 (walks, the back dash and lying clips skipped: the sim moves the body),
a clip whose core-capsule intersection is >= 8 cm deep on >= 4 frames and >= 12 vertices (shallower ones are
sleeves, fur and belly volume pressing, not a limb through a body).
Usage: python tools/rig_qa/verdicts.py <dir> [--before <dir>]
"""
import json
import sys
from pathlib import Path

AIR_ONLY = ("jump-kick", "jump-punch")


def problems(r):
    out = []
    clip = r["clip"]
    if not any(a in clip for a in AIR_ONLY) and r["sink_cm"] > 4:
        out.append(f"sinks {r['sink_cm']} cm (f{r['sink_f']})")
    if not r["air"] and not r["lying"] and r["float_cm"] > 4:
        out.append(f"floats {r['float_cm']} cm (f{r['float_f']}, {r['float_frames']} frames)")
    if not r["walk"] and not r["lying"] and "dash" not in clip and not r["air"] and max(r["drift_cm"]) > 10:
        out.append(f"planted foot slides {max(r['drift_cm'])} cm (L/R {r['drift_cm'][0]}/{r['drift_cm'][1]})")
    worst = [(k, v) for k, v in r["inter"].items() if v["depth"] >= 8 and v["frames"] >= 4 and v["verts"] >= 12]
    worst.sort(key=lambda kv: -kv[1]["depth"])
    for k, v in worst[:2]:
        a, b = k.split(">")
        out.append(f"{a} into {b} {v['depth']} cm ({v['frames']} frames, worst f{v['worst_f']})")
    return out


def fixes(r, probs):
    """Proposed fix letters (finish-qa.md "Fixes proposed"): A foot lock, B per-body hip offset, C body-proportion
    retarget, D per-body lying clip, E model change (tail / shell-fit, needs approval)."""
    out = []
    for p in probs:
        if p.startswith("sinks") or p.startswith("floats"):
            out.append("D" if r["lying"] else ("B" if r["body"] in ("oni", "kappa", "tanuki") else "C (hips offset per clip)"))
        elif p.startswith("planted"):
            out.append("A done, rest C" if r["clip"] in LOCKED else ("move data (forward_travel)" if "charge" in r["clip"] else "A"))
        else:
            out.append(("E1 or D" if r["lying"] else "E1 or C") if ("tail" in p and r["body"] == "tanuki") else ("E2 or C" if "shell" in p else "C"))
    return ", ".join(dict.fromkeys(out)) or "-"


# clip ids whose file went through tools/blender/foot_lock.py (tools/rig_qa/foot_lock_batch.sh)
LOCKED = {"ryo-heavy-kick", "ryo-foxfire-cast", "ryo-fox-mirage", "ryo-generic-throw", "ryo-river-grab", "ryo-light-kick",
          "ryo-medium-kick", "ryo-heavy-punch", "ryo-iron-club-charge", "ryo-oni-quake", "ryo-shell-spin", "ryo-hit-high",
          "ryo-hit-low", "kitsune-foxfire", "kitsune-fox-mirage", "kitsune-heavy-kick", "kappa-heavy-kick", "kitsune-throw",
          "kappa-throw", "kappa-river-grab", "kitsune-medium-kick", "kappa-medium-kick", "kitsune-hit-high", "kitsune-hit-low",
          "kitsune-heavy-punch", "kappa-heavy-punch", "kappa-shell-spin", "oni-heavy-punch", "oni-quake", "oni-medium-punch",
          "oni-medium-kick"}


def main():
    d = Path(sys.argv[1])
    rows = json.loads((d / "summary.json").read_text())
    before = {}
    if "--before" in sys.argv:
        b = Path(sys.argv[sys.argv.index("--before") + 1])
        before = {(r["body"], r["clip"]): r for r in json.loads((b / "summary.json").read_text())}
    counts = {}
    for body in ["ryo", "tanuki", "kitsune", "oni", "kappa"]:
        print(f"\n### {body}\n")
        print("| Clip | Verdict | Problem (game pose) | Proposed fix | Was (before the foot lock) |")
        print("|---|---|---|---|---|")
        for r in sorted([r for r in rows if r["body"] == body], key=lambda r: r["clip"]):
            p = problems(r)
            v = "FIX" if p else "PASS"
            counts.setdefault(body, {"PASS": 0, "FIX": 0})[v] += 1
            was = ""
            if (body, r["clip"]) in before:
                pb = problems(before[(body, r["clip"])])
                if pb != p:
                    was = "; ".join(pb) or "PASS"
            print(f"| {r['clip']} | {v} | {'; '.join(p) or '-'} | {fixes(r, p)} | {was} |")
    print("\n" + json.dumps(counts))


if __name__ == "__main__":
    main()
