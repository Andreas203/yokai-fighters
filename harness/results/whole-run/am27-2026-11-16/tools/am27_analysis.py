import json, sys, math, statistics as st, collections, re

import os
BR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))  # this results folder (AM27 branch)
MN = os.path.join(BR, "main-b25858cb")  # the same batches on main before AM27

COMMONS = {
    "kitsune": ["foxfire", "fox-patience", "will-o-wisp"],
    "oni": ["iron-club-charge", "iron-will", "onis-hide"],
    "kappa": ["river-grab", "river-pull", "slippery-skin"],
}
BINDS = {"apprentice": "kitsune", "kitsune": "kitsune", "oni": "oni", "kappa": "kappa",
         "nine-tailed-kitsune": "kitsune", "elder-oni": "oni", "elder-kappa": "kappa"}


def wilson(k, n, z=1.96):
    if n == 0:
        return (None, None)
    p = k / n
    d = 1 + z * z / n
    c = (p + z * z / (2 * n)) / d
    h = z * math.sqrt(p * (1 - p) / n + z * z / (4 * n * n)) / d
    return (round(100 * (c - h), 1), round(100 * (c + h), 1))


def desc(xs):
    if not xs:
        return "n=0"
    s = f"n={len(xs)} mean {st.mean(xs):.0f} median {st.median(xs):.0f}"
    if len(xs) > 1:
        s += f" sd {st.stdev(xs):.0f}"
    return s + f" range {min(xs)}-{max(xs)}"


def load(p):
    return json.load(open(p))["runs"]


def analyse(tag, runs):
    out = {}
    print(f"\n===== {tag} (runs={len(runs)}) =====")
    reach = sum(r["reached_tanuki"] for r in runs)
    beat = sum(r["beat_tanuki"] for r in runs)
    print(f"reach {reach}/{len(runs)} {100*reach/len(runs):.1f}% CI {wilson(reach,len(runs))}; beat {beat}/{reach}")
    # per-row fights
    rowf = collections.defaultdict(lambda: [0, 0])
    tier_taken = collections.defaultdict(list)
    prof = collections.defaultdict(lambda: [0, 0])
    arr = collections.defaultdict(list)
    stops = collections.Counter()
    bought_items = collections.Counter()
    nbuy = collections.defaultdict(collections.Counter)
    spent = collections.defaultdict(list)
    coins_arr = collections.defaultdict(list)
    empty_main = collections.Counter()
    nobuy_reason = collections.Counter()
    copyok = [0, 0]
    for r in runs:
        bound = set()
        held = set()
        # held from cards
        for c in r["cards_picked"]:
            m = re.match(r"NEW (\S+)", c)
            if m:
                held.add(m.group(1))
        for n in r["nodes"]:
            arr[n["row"] if n["type"] != "boss" else "tanuki"].append(n["arrival_health"])
            if "fight" in n:
                f = n["fight"]
                key = n["row"]
                rowf[key][1] += 1
                rowf[key][0] += f["result"] == "won"
                tier_taken[n["tier"]].append(f["damage_taken"])
                prof[(n["row"], f["profile"])][1] += 1
                prof[(n["row"], f["profile"])][0] += f["result"] == "won"
                if f["result"] == "won" and n["yokai"] in BINDS:
                    bound.add(BINDS[n["yokai"]])
            if n["type"] in ("merchant", "dojo"):
                row = n["row"]
                stops[(row, n["type"], n["action"])] += 1
                b = n.get("bought", [])
                for i in b:
                    bought_items[(row, i)] += 1
                    held.add(i)
                if n["action"] == "shop":
                    nbuy[row][len(b)] += 1
                    spent[row].append(50 * len(b))
                    coins_arr[row].append(n["arrival_coins"])
                    # main-rule shelf: unbound yokai commons not held
                    unb = [i for y, items in COMMONS.items() if y not in bound for i in items if i not in held or i in b]
                    if not unb:
                        empty_main[row] += 1
                    if not b:
                        if not unb and tag.startswith("main"):
                            nobuy_reason["empty shelf (main rule)"] += 1
                        elif n["arrival_coins"] < 50:
                            nobuy_reason["coins < 50"] += 1
                        else:
                            nobuy_reason["stock but nothing fits/affordable"] += 1
            if n["type"] == "boss" and r.get("copy_locked"):
                copyok[1] += 1
        if r.get("copy_locked"):
            # T2/T3 check: highest level special in a slot, ties -> most recently levelled
            lv = {"spirit-wave": 1, "rising-talisman": 1}
            last = {"spirit-wave": -2, "rising-talisman": -1}
            t = 0
            for n in r["nodes"]:
                for i in n.get("bought", []):
                    if i in ("foxfire", "iron-club-charge", "river-grab"):
                        lv[i] = 1; last[i] = t; t += 1
                c = n.get("card")
                if c and c["badge"] in ("UPGRADE", "NEW"):
                    lv[c["ability"]] = c["level"]; last[c["ability"]] = t; t += 1
            best = max(lv, key=lambda k: (lv[k], last[k]))
            ok = best == r["copy_locked"]["special"] and lv[best] == r["copy_locked"]["level"]
            copyok[0] += ok
            if not ok:
                print("COPY MISMATCH", r["seed"], lv, r["copy_locked"])
    print("row win rates:", {k: f"{v[0]}/{v[1]} {100*v[0]/v[1]:.0f}%" for k, v in sorted(rowf.items())})
    print("arrival health:")
    for k in [2, 3, 4, 5, 6, 7, 8, "tanuki"]:
        print(f"  {k}: {desc(arr[k])}")
    print("Y4 taken by tier:", {k: f"{st.mean(v):.0f} (n={len(v)})" for k, v in tier_taken.items()})
    print("stops:", dict(sorted(stops.items())))
    for row in (4, 7):
        if nbuy[row]:
            tot = sum(nbuy[row].values())
            print(f"row {row} shop stops {tot}: items bought dist {dict(sorted(nbuy[row].items()))}; "
                  f"mean items {sum(k*v for k,v in nbuy[row].items())/tot:.2f}; coins at arrival {desc(coins_arr[row])}; "
                  f"spent {desc(spent[row])}; main-rule empty shelf {empty_main[row]}/{tot}")
    print("no-buy reasons:", dict(nobuy_reason))
    print("items bought:", dict(sorted(bought_items.items())))
    print(f"copy rule check: {copyok[0]}/{copyok[1]} correct")
    # run length
    m_all = [r["estimated_minutes"] for r in runs]
    m_t = [r["estimated_minutes"] for r in runs if r["reached_tanuki"]]
    print(f"R9 minutes all: mean {st.mean(m_all):.1f}; reaching tanuki: {m_t}")
    # end coins
    endc = [r["coins"] for r in runs]
    print(f"coins at run end: {desc(endc)}")
    # coins at row 7 arrival (any action)
    c7 = [n["arrival_coins"] for r in runs for n in r["nodes"] if n["row"] == 7]
    c4 = [n["arrival_coins"] for r in runs for n in r["nodes"] if n["row"] == 4]
    print(f"R8 coins on arrival row4 {desc(c4)}; row7 {desc(c7)}")
    return dict(runs=runs, rowf=rowf, prof=prof, arr=arr)


res = {}
for lvl in ["newcomer-kihon-bot", "skilled-kihon-bot", "newcomer-kihon-bot-shop", "skilled-kihon-bot-shop"]:
    for side, d in (("main", MN), ("am27", BR)):
        res[(side, lvl)] = analyse(f"{side} {lvl}", load(os.path.join(d, f"{lvl}-n200-s1.json")))

# paired divergence for shop bots
for lvl in ["newcomer-kihon-bot-shop", "skilled-kihon-bot-shop"]:
    a = res[("main", lvl)]["runs"]; b = res[("am27", lvl)]["runs"]
    same_r4 = sum(1 for x, y in zip(a, b) if [n["arrival_health"] for n in x["nodes"] if n["row"] <= 4] == [n["arrival_health"] for n in y["nodes"] if n["row"] <= 4] and x["route"][:4] == y["route"][:4])
    went = collections.Counter()
    for x, y in zip(a, b):
        went[(x["row_reached"] < y["row_reached"]) - (x["row_reached"] > y["row_reached"])] += 1
    print(f"\n{lvl}: runs identical through row 4: {same_r4}/200; AM27 deeper/same/shallower: {went[1]}/{went[0]}/{went[-1]}")
    # row 5+ win rates for runs that reached row 5, with what was bought at row 4
    for side, rr in (("main", a), ("am27", b)):
        g = collections.defaultdict(lambda: [0, 0])
        for r in rr:
            r4 = next((n for n in r["nodes"] if n["row"] == 4), None)
            if not r4:
                continue
            k = "bought" if r4.get("bought") else "nothing"
            for n in r["nodes"]:
                if n["row"] in (5, 6) and "fight" in n:
                    g[(k, n["row"])][1] += 1
                    g[(k, n["row"])][0] += n["fight"]["result"] == "won"
        print(f"  {side} row5/6 by row-4 purchase:", {k: f"{v[0]}/{v[1]}" for k, v in sorted(g.items())})
