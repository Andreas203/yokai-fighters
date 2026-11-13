"""YOK-finish-qa: rank the per-frame metrics written by game/tests/captures/FinishQaCapture.cs.

Reads <dir>/metrics.json, prints one line per body x clip and writes <dir>/summary.json:
sink (cm below the floor, lowest foot), float (cm, lowest foot above the floor on a grounded frame),
slip (cm of planted-sole travel summed over the clip; worst single frame), intersections (pair, frames, max depth cm).
Usage: python tools/rig_qa/rank.py <dir> [--top N]
"""
import json
import sys
from pathlib import Path

AIR = ("jump",)  # air clips: hips capped at rest, the sim draws the arc; floating there is expected
LYING = ("knockdown", "get-up", "getup")
WALK = ("walk",)

def summarise(body, clip, c):
    fr = c["frames"]
    air = any(a in clip for a in AIR)
    lying = any(a in clip for a in LYING)
    walk = any(a in clip for a in WALK)
    low = [min(f["fl"], f["fr"]) for f in fr]
    bodymin = [f["min"] for f in fr]
    sink = max(0.0, -min(bodymin))
    sink_f = fr[bodymin.index(min(bodymin))]["f"]
    flt, flt_f, float_frames = 0.0, None, 0
    if not air:
        ref = bodymin if lying else low
        for f, v in zip(fr, ref):
            if v > 2.0:
                float_frames += 1
                if v > flt:
                    flt, flt_f = v, f["f"]
    slipL = sum(f["sl"] for f in fr); slipR = sum(f["sr"] for f in fr)
    slip_max = max(max(f["sl"], f["sr"]) for f in fr)
    slip_f = max(fr, key=lambda f: max(f["sl"], f["sr"]))["f"]
    drift = []
    for key, pos in (("fl", "pl"), ("fr", "pr")):
        best, run0 = 0.0, None
        for f in fr:
            if f[key] < 1.5:
                if run0 is None:
                    run0 = f[pos]
                dx, dz = f[pos][0] - run0[0], f[pos][1] - run0[1]
                best = max(best, (dx * dx + dz * dz) ** 0.5)
            else:
                run0 = None
        drift.append(round(best, 1))
    inter = {}
    for f in fr:
        for p, t, n, d in f["hits"]:
            k = f"{p}>{t}"
            e = inter.setdefault(k, {"frames": 0, "depth": 0.0, "worst_f": None, "verts": 0})
            e["frames"] += 1
            if d > e["depth"]:
                e["depth"], e["worst_f"], e["verts"] = d, f["f"], n
    score = sink * 2 + (flt if flt > 3 else 0) + (0 if walk else max(0, max(drift) - 3)) + sum(
        min(e["depth"], 15) * min(1, e["frames"] / 4) * min(1, e["verts"] / 8) for e in inter.values())
    return {
        "body": body, "clip": clip, "uses": c["uses"], "frames": c["frames_total"], "hit": c["hit"], "picks": c["picks"],
        "sink_cm": round(sink, 1), "sink_f": sink_f, "float_cm": round(flt, 1), "float_f": flt_f, "float_frames": float_frames,
        "slip_total_cm": [round(slipL, 1), round(slipR, 1)], "drift_cm": drift, "slip_max_cm": round(slip_max, 2), "slip_f": slip_f,
        "inter": inter, "score": round(score, 1), "air": air, "lying": lying, "walk": walk,
    }

def main():
    d = Path(sys.argv[1])
    top = int(sys.argv[sys.argv.index("--top") + 1]) if "--top" in sys.argv else 0
    m = {}
    for f in sorted(d.glob("metrics*.json")):
        m.update(json.loads(f.read_text()))
    rows = [summarise(b, clip, c) for b, bd in m.items() for clip, c in bd["clips"].items()]
    rows.sort(key=lambda r: -r["score"])
    (d / "summary.json").write_text(json.dumps(rows, indent=1))
    for r in rows[: top or None]:
        it = "; ".join(f"{k} {v['frames']}fr {v['depth']}cm@f{v['worst_f']}({v['verts']}v)" for k, v in sorted(r["inter"].items(), key=lambda kv: -kv[1]["depth"]))
        print(f"{r['score']:6.1f} {r['body']:8s} {r['clip']:28s} n{r['frames']:3d} sink {r['sink_cm']:4.1f}@f{r['sink_f']} "
              f"float {r['float_cm']:4.1f}@f{r['float_f']}({r['float_frames']}) drift {r['drift_cm']} slip {r['slip_total_cm']} max {r['slip_max_cm']}@f{r['slip_f']} | {it}")

if __name__ == "__main__":
    main()
