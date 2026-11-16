import json, statistics as st, collections

import os
BR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))  # this results folder (AM27 branch)
MN = os.path.join(BR, "main-b25858cb")  # the same batches on main before AM27
L = lambda d, l: json.load(open(f"{d}\\{l}-n200-s1.json"))["runs"]

def node(r, row):
    return next((n for n in r["nodes"] if n["row"] == row), None)

for lvl in ["skilled-kihon-bot-shop", "newcomer-kihon-bot-shop"]:
    a, b = L(MN, lvl), L(BR, lvl)
    print(f"\n== {lvl} paired ==")
    conv4 = [(x, y) for x, y in zip(a, b) if node(x, 4) and not node(x, 4).get("bought")]
    print("row-4 empty on main:", len(conv4), "AM27 bought:", collections.Counter(tuple(node(y, 4).get("bought", [])) for _, y in conv4))
    for row in (5, 6, 8, 9):
        wm = sum(1 for x, _ in conv4 if node(x, row) and "fight" in node(x, row) and node(x, row)["fight"]["result"] == "won")
        fm = sum(1 for x, _ in conv4 if node(x, row) and "fight" in node(x, row))
        wa = sum(1 for _, y in conv4 if node(y, row) and "fight" in node(y, row) and node(y, row)["fight"]["result"] == "won")
        fa = sum(1 for _, y in conv4 if node(y, row) and "fight" in node(y, row))
        print(f"  converted subset row {row}: main {wm}/{fm}  am27 {wa}/{fa}")
    # row 5 damage dealt / taken in converted subset
    for tag, idx in (("main", 0), ("am27", 1)):
        dd = [p[idx] for p in conv4]
        dealt = [node(r, 5)["fight"]["damage_dealt"] for r in dd if node(r, 5)]
        taken = [node(r, 5)["fight"]["damage_taken"] for r in dd if node(r, 5)]
        print(f"  converted subset row 5 {tag}: dealt mean {st.mean(dealt):.0f}, taken mean {st.mean(taken):.0f} (n={len(dealt)})")
    conv7 = [(x, y) for x, y in zip(a, b) if node(x, 7) and node(y, 7)]
    print("row-7 reached both sides:", len(conv7))
    for x, y in conv7:
        print("  seed", x["seed"], "main bought", node(x, 7).get("bought"), "->", x["row_reached"], x["outcome"],
              "| am27 bought", node(y, 7).get("bought"), "->", y["row_reached"], y["outcome"],
              "| r8 arrival", node(y, 8)["arrival_health"], "r8 taken main/am27",
              node(x, 8)["fight"]["damage_taken"], node(y, 8)["fight"]["damage_taken"])
    diff = [(x, y) for x, y in zip(a, b) if x["row_reached"] != y["row_reached"]]
    for x, y in diff:
        print("  DIFF seed", x["seed"], x["route"], x["row_reached"], "->", y["route"], y["row_reached"],
              "main r4", node(x, 4).get("bought"), "am27 r4", node(y, 4).get("bought"))

# Shop route vs heal route (skilled), am27
heal = L(BR, "skilled-kihon-bot"); shop = L(BR, "skilled-kihon-bot-shop")
print("\n== skilled heal route vs shop route (AM27) ==")
for tag, rr in (("heal", heal), ("shop", shop)):
    for row in (5, 6, 7, 8):
        arr = [node(r, row)["arrival_health"] for r in rr if node(r, row)]
        print(f"  {tag} row {row} arrival mean {st.mean(arr):.0f} median {st.median(arr):.0f} n={len(arr)}")
    t = [node(r, 9)["arrival_health"] for r in rr if node(r, 9)]
    print(f"  {tag} tanuki arrival {sorted(t)}")
    # row-8 elder: damage taken among winners
    w8 = [node(r, 8)["fight"]["damage_taken"] for r in rr if node(r, 8) and node(r, 8)["fight"]["result"] == "won"]
    print(f"  {tag} row-8 winners damage taken {sorted(w8)}")
# Counterfactual shop@4 + rest@7: row-7 arrival + 300 (cap 1000), then this run's own row-8 damage
# taken if it won (lower bound for those who died: they had less health)
est = []
for r in shop:
    n7, n8 = node(r, 7), node(r, 8)
    if not n7:
        continue
    h8 = min(1000, n7["arrival_health"] + 300)
    taken = n8["fight"]["damage_taken"]
    won = n8["fight"]["result"] == "won"
    est.append((n7["arrival_health"], h8, taken, won, h8 - taken + 50 if h8 - taken > 0 else 0))
print("shop@4+rest@7 counterfactual (row7 arr, row8 arr, row8 taken as played, won, est tanuki arr):")
for e in est:
    print("  ", e)
print("survive estimate:", sum(1 for e in est if e[4] > 0), "/", len(est), "mean est tanuki arr of survivors",
      st.mean([e[4] for e in est if e[4] > 0]))

# P5 share of bought specials in won fights rows 5-8, AM27 skilled shop
print("\n== P5 single-move share, won fights rows 2-8, AM27 vs heal ==")
for tag, rr in (("heal", heal), ("shop", shop)):
    top = collections.Counter(); n = 0; over50 = 0
    for r in rr:
        for nd in r["nodes"]:
            f = nd.get("fight")
            if not f or nd["row"] < 2 or nd["row"] > 8 or f["result"] != "won":
                continue
            mv = f["log"]["moves"]["ryo"]
            tot = sum(v["damage"] for v in mv.values())
            sp = {k: v["damage"] for k, v in mv.items() if k in ("spirit-wave", "rising-talisman", "foxfire", "iron-club-charge", "river-grab")}
            if not tot or not sp:
                continue
            k = max(sp, key=sp.get); n += 1
            if sp[k] / tot > 0.5:
                over50 += 1; top[k] += 1
    print(f"  {tag}: won fights {n}, one special >50% in {over50} ({100*over50/n:.0f}%): {dict(top)}")
