"""YOK-finish-qa: before/after table of two rank.py summaries. Usage: python tools/rig_qa/compare.py <before dir> <after dir> [clip-substring ...]"""
import json
import sys
from pathlib import Path

def load(d):
    return {(r["body"], r["clip"]): r for r in json.loads((Path(d) / "summary.json").read_text())}

def inter(r):
    return round(sum(min(e["depth"], 15) * min(1, e["frames"] / 4) * min(1, e["verts"] / 8) for e in r["inter"].values()), 1)

b, a = load(sys.argv[1]), load(sys.argv[2])
subs = sys.argv[3:]
for k in sorted(b):
    if subs and not any(s in k[1] for s in subs):
        continue
    x, y = b[k], a.get(k)
    if not y:
        continue
    if x == y or (x["drift_cm"] == y["drift_cm"] and x["sink_cm"] == y["sink_cm"] and inter(x) == inter(y)):
        continue
    print(f"{k[0]:8s} {k[1]:28s} drift {x['drift_cm']} -> {y['drift_cm']}  sink {x['sink_cm']} -> {y['sink_cm']}  "
          f"float {x['float_cm']} -> {y['float_cm']}  inter {inter(x)} -> {inter(y)}  score {x['score']} -> {y['score']}")
