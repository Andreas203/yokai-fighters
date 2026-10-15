#!/usr/bin/env python3
"""Static rules check over Yokai Fighters content (YOK-35).

Usage:
    python -I tools/check_rules.py [--json] [ROOT ...]

Complements tools/validate_data.py (shape: JSON Schema + per-file consistency) and
the rules-lawyer agent (judgment, card text). This script checks the written rules
that need the whole content set at once, citing the rule ID in docs/design/rules.md
and the GDD section it comes from:

  slots      A1   four special slots A-D, two starters in their own slots; a run's
                  slots (save-file shape, `run.slots` or top-level `slots`) hold at
                  most four specials, each once (A3)
  modifiers  A2   one modifier per special (in a run's slots, or on a special file)
  cancel-cap X2 / A11 / X4   at most two cancel rules held in a run; at most three in data
  cancels    X3   every rule is once per combo; a loop in the cancel graph over the moves
                  (from/into classes of every rule in data) fails only when a rule on it is
                  not once per combo, otherwise it is left for the harness
  levels     A3 / A4 / A5 / E21 / P1   every special has exactly Lv 1-3; Lv 2 one
                  tuning step that is not damage alone; Lv 3 an evolution with a preset
  meter      C5 / E16   only a special's EX (1 bar = 100) or the burst (C6, not in data)
                  spends meter; no normal, throw, level or modifier spends it, except an
                  effect on the EX cost spelt with an `ex.` target (`ex.meter_cost`)
  schemes    K1 / K2 / E18  every special has a Kata motion and a Kihon direction,
                  on the same slot, matching that slot's K1 motion and K2 direction
  trials     R7 / K2   each trial's special and every step's move exist, are in Ryo's
                  kit and have a Kihon input; completability within 3 attempts is
                  printed as "harness must confirm"

ROOT is a data-like folder (default: data/). Several ROOTs merge in order, a later
file replacing an earlier one with the same kind and id, so a folder of broken
files can be laid over data/ (see tools/tests/rules_fixtures/). data/schema/,
data/samples/ and any `invalid/` folder are skipped. Files the schema would refuse
are still read: this check never needs them to be schema-valid.

Exit code 0 = no violation (harness notes do not fail), 1 = violations, 2 = setup problem.
Stdlib only, so it runs with `python -I`.
"""
from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DATA = ROOT / "data"
SKIP_DIRS = {"schema", "samples", "invalid"}

# Rule ID -> where it comes from in the GDD (docs/design/gdd-extended.md). E rules are
# designer-approved engine defaults with no GDD section of their own.
GDD = {
    "A1": "GDD 4.1 Slots, levels and offers",
    "A2": "GDD 4.1 Slots, levels and offers",
    "A3": "GDD 4.1 Slots, levels and offers",
    "A4": "GDD 4.1 Slots, levels and offers; 4.2 Specials",
    "A5": "GDD 4.1 Slots, levels and offers; 4.2 Specials",
    "A11": "GDD 4 Abilities (4.1 Locks; 4.4 Cancel rules)",
    "X2": "GDD 4.1 Slots, levels and offers",
    "X3": "GDD 3.5 Cancels",
    "X4": "GDD 4.4 Cancel rules",
    "C5": "GDD 3.2 Fight basics (Meter)",
    "K1": "GDD 3.3 Control schemes",
    "K2": "GDD 3.3 Control schemes",
    "K3": "GDD 3.3 Control schemes",
    "R7": "GDD 6.3 Rest stops (Dojo)",
    "C4": "GDD 3.2 Fight basics (Throws); command grabs are an open question in rules.md",
    "P1": "GDD 1.3 Design pillars",
    "E16": "rules.md E16 (designer default, no GDD section)",
    "E21": "rules.md E21 (designer decision YOK-41, no GDD section)",
}

SLOTS = ("A", "B", "C", "D")                                     # A1
KATA_MOTION = {"A": "236", "B": "623", "C": "214", "D": "22"}    # K1
KIHON_DIRECTION = {"A": "neutral", "B": "forward", "C": "back", "D": "down"}  # K2
EX_COST = 100           # C5: an EX special costs 1 bar of 100
MAX_HELD_CANCELS = 2    # X2
MAX_CANCEL_RULES = 3    # A11 / X4: Fox Step, Oni Chain, Undertow
STARTERS = 2            # A1
KATA_ONLY_KEYS = ("motion", "precision", "kata")  # same list as validate_data.TRIAL_KATA_ONLY
HEAVY_BUTTONS = ("HP", "HK")  # Match.Specials: CancelFrom.HeavyNormals = HP or HK normal


def rel(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return path.as_posix()


class Report:
    def __init__(self):
        self.violations: list[dict] = []
        self.harness: list[dict] = []

    def _item(self, check, rule, path, msg):
        return {"check": check, "rule": rule, "gdd": GDD.get(rule, ""), "file": rel(path) if path else "(data)", "message": msg}

    def fail(self, check, rule, path, msg):
        self.violations.append(self._item(check, rule, path, msg))

    def confirm(self, check, rule, path, msg):
        self.harness.append(self._item(check, rule, path, msg))


def group(kind) -> str:
    return "move" if kind in ("special", "normal", "throw") else str(kind)


def load(roots: list[Path]) -> tuple[dict, list[tuple[Path, str]]]:
    """(kind group, id) -> (path, doc); later roots replace earlier entries. Run-state docs key by path."""
    docs: dict = {}
    unreadable = []
    for root in roots:
        files = [root] if root.is_file() else sorted(root.rglob("*.json"))
        for f in files:
            below = f.relative_to(root).parts[:-1] if root.is_dir() else ()
            if SKIP_DIRS.intersection(below):
                continue
            try:
                doc = json.loads(f.read_text(encoding="utf-8"))
            except (OSError, ValueError) as e:
                unreadable.append((f, str(e)))
                continue
            if not isinstance(doc, dict):
                continue
            if "kind" in doc:
                docs[(group(doc["kind"]), doc.get("id", f.stem))] = (f, doc)
            elif run_slots(doc) is not None:
                docs[("run", f.name)] = (f, doc)
    return docs, unreadable


def run_slots(doc: dict):
    """Slots of a run state: the save file's `run.slots` (SaveStore v3) or a top-level `slots`."""
    run = doc.get("run") if isinstance(doc.get("run"), dict) else doc
    slots = run.get("slots")
    return slots if isinstance(slots, list) else None


def of_kind(docs, *kinds):
    return [(p, d) for (_, _), (p, d) in sorted(docs.items(), key=lambda kv: str(kv[1][0])) if d.get("kind") in kinds]


def is_ryo_move(d: dict) -> bool:
    """Ryo's kit: his own normals and throw, and every special (any source can be drafted, A7)."""
    return d.get("kind") == "special" or d.get("source") == "ryo"


# --- A1 slot limits, A2 one modifier per special, X2 cancel cap ---------------------------------

def check_slots(docs, rep: Report):
    specials = of_kind(docs, "special")
    starters: dict[str, list[str]] = {}
    for p, d in specials:
        slot = ((d.get("input") or {}).get("kihon") or {}).get("slot") or ((d.get("input") or {}).get("kata") or {}).get("slot")
        if slot is not None and slot not in SLOTS:
            rep.fail("slots", "A1", p, f"slot '{slot}' is not one of the four special slots A-D")
        if d.get("starter") is True:
            starters.setdefault(str(slot), []).append(d.get("id"))
        mods = d.get("modifiers")
        if isinstance(mods, list) and len(mods) > 1:
            rep.fail("modifiers", "A2", p, f"special carries {len(mods)} modifiers {mods}; one modifier per special")
    n = sum(len(v) for v in starters.values())
    if specials and n != STARTERS:
        rep.fail("slots", "A1", None, f"{n} starter special(s) in data; Ryo starts with exactly {STARTERS} slots filled")
    for slot, ids in sorted(starters.items()):
        if len(ids) > 1:
            rep.fail("slots", "A1", None, f"starters {ids} share slot {slot}; each starter fills its own slot")

    cancels = of_kind(docs, "cancel_rule")
    if len(cancels) > MAX_CANCEL_RULES:
        rep.fail("cancel-cap", "A11", cancels[-1][0], f"{len(cancels)} cancel rules in data; the game has {MAX_CANCEL_RULES} (A11, X4)")

    known_specials = {d.get("id") for _, d in specials}
    known_mods = {d.get("id") for _, d in of_kind(docs, "modifier")}
    known_cancels = {d.get("id") for _, d in cancels}
    for (g, _), (p, doc) in sorted(docs.items(), key=lambda kv: str(kv[1][0])):
        if g != "run":
            continue
        slots = run_slots(doc) or []
        if len(slots) > len(SLOTS):
            rep.fail("slots", "A1", p, f"{len(slots)} specials slotted; there are four slots")
        seen_slot, seen_special = {}, {}
        for i, s in enumerate(slots):
            if not isinstance(s, dict):
                continue
            letter, sp = s.get("slot"), s.get("special")
            where = f"slots[{i}]"
            if letter not in SLOTS:
                rep.fail("slots", "A1", p, f"{where}: slot '{letter}' is not one of A-D")
            elif letter in seen_slot:
                rep.fail("slots", "A1", p, f"{where}: slot {letter} is filled twice (also slots[{seen_slot[letter]}])")
            else:
                seen_slot[letter] = i
            if sp in seen_special:
                rep.fail("slots", "A3", p, f"{where}: '{sp}' is slotted twice (also slots[{seen_special[sp]}]); drafting an owned special levels it up")
            else:
                seen_special[sp] = i
            if known_specials and sp not in known_specials:
                rep.fail("slots", "A1", p, f"{where}: '{sp}' is not a special in data/moves/")
            lv = s.get("level")
            if lv is not None and lv not in (1, 2, 3):
                rep.fail("levels", "A3", p, f"{where}: level {lv!r}; a special is Lv 1, 2 or 3")
            mod = s.get("modifier")
            mods = s.get("modifiers")
            listed = [m for m in ([mod] if isinstance(mod, str) else (mod if isinstance(mod, list) else []))
                      + (mods if isinstance(mods, list) else []) if m]
            if len(listed) > 1:
                rep.fail("modifiers", "A2", p, f"{where}: '{sp}' carries {len(listed)} modifiers {listed}; one modifier per special")
            for m in listed:
                if known_mods and m not in known_mods:
                    rep.fail("modifiers", "A2", p, f"{where}: modifier '{m}' is not in data/modifiers/")
        held = (doc.get("run") if isinstance(doc.get("run"), dict) else doc).get("cancels")
        if isinstance(held, list):
            if len(held) > MAX_HELD_CANCELS:
                rep.fail("cancel-cap", "X2", p, f"{len(held)} cancel rules held {held}; up to {MAX_HELD_CANCELS} at once")
            if len(set(map(str, held))) != len(held):
                rep.fail("cancel-cap", "X2", p, f"a cancel rule is held twice: {held}")
            for c in held:
                if known_cancels and c not in known_cancels:
                    rep.fail("cancel-cap", "X2", p, f"held cancel rule '{c}' is not in data/cancels/")


# --- X3 once per combo and the cancel graph ---------------------------------------------------

def move_classes(d: dict) -> set[str]:
    """The cancel path classes (cancel-rule.schema.json `path.from` / `path.into`) a move belongs to."""
    k = d.get("kind")
    out = set()
    if k == "special":
        # Specials are never in successful_throws: the game's CancelFrom.SuccessfulThrows is m.IsThrow, which
        # only the throw input sets (Match.Specials.cs, MoveData.cs). Whether a command grab such as River Grab
        # counts as a throw is an open question (C4); check_cancels flags it for the harness instead.
        out |= {"specials", "one_special"}
    elif k == "normal" and d.get("source") == "ryo":
        out.add("normals")
        btn = (d.get("input") or {}).get("button")
        btns = (d.get("input") or {}).get("buttons") or []
        if btn in HEAVY_BUTTONS or (not btn and any(b in HEAVY_BUTTONS for b in btns)):
            out.add("heavy_normals")
    elif k == "throw" and d.get("source") == "ryo":
        out.add("successful_throws")
    return out


def cancel_graph(moves: list[dict], rules: list[dict]) -> dict[str, dict[str, list[str]]]:
    """move id -> {target id: [rule ids]}; 'dash' is a node with no way out (no rule starts from a dash)."""
    classes = {m["id"]: move_classes(m) for m in moves if isinstance(m.get("id"), str)}
    graph: dict[str, dict[str, list[str]]] = {mid: {} for mid in classes}
    graph["dash"] = {}
    for r in rules:
        path = r.get("path") or {}
        src, dst = path.get("from"), path.get("into")
        targets = ["dash"] if dst == "dash" else [mid for mid, c in classes.items() if dst in c]
        for mid, c in classes.items():
            if src in c:
                for t in targets:
                    graph[mid].setdefault(t, []).append(str(r.get("id")))
    return graph


def find_loops(graph: dict[str, dict[str, list[str]]]) -> list[tuple[list[str], list[str], list[str]]]:
    """One entry per group of moves that can cancel round in a loop (a strongly connected component with
    a cycle): (sorted members, the shortest example loop from the smallest member, rule ids on the group's
    internal edges). Graphs here are a few dozen nodes, so a reachability pass per node is fine."""
    def reach(a):
        seen, todo = set(), [a]
        while todo:
            for b in graph.get(todo.pop(), {}):
                if b not in seen:
                    seen.add(b)
                    todo.append(b)
        return seen

    r = {n: reach(n) for n in graph}
    out, done = [], set()
    for n in sorted(graph):
        if n in done or n not in r[n]:
            continue
        members = sorted(m for m in graph if m == n or (m in r[n] and n in r[m]))
        done.update(members)
        # Shortest loop n -> ... -> n by breadth-first search inside the group.
        prev, frontier, example = {}, [n], [n]
        while frontier and n not in prev:
            nxt = []
            for a in frontier:
                for b in sorted(graph[a]):
                    if b in members and b not in prev:
                        prev[b] = a
                        nxt.append(b)
            frontier = nxt
        if n in prev:
            path, cur = [], prev[n]
            while cur != n:
                path.append(cur)
                cur = prev[cur]
            example = [n] + path[::-1]
        rules = sorted({rid for a in members for b, rids in graph[a].items() if b in members for rid in rids})
        out.append((members, example, rules))
    return out


def check_cancels(docs, rep: Report):
    rules = of_kind(docs, "cancel_rule")
    for p, d in rules:
        if d.get("once_per_combo") is not True:
            rep.fail("cancels", "X3", p, "once_per_combo must be true: each move can be cancelled into once per combo")
    if not rules:
        return
    moves = [d for _, d in of_kind(docs, "special", "normal", "throw")]
    graph = cancel_graph(moves, [d for _, d in rules])
    by_id = {str(d.get("id")): (p, d) for p, d in rules}
    # X3: once per combo is what stops stacked rules looping. A cycle is a violation only when a rule on it
    # lacks once_per_combo: true; otherwise the runtime bound has to hold, which the harness confirms.
    for members, example, used in find_loops(graph):
        chain = " -> ".join(example + [example[0]])
        unbounded = [r for r in used if (by_id.get(r) or (None, {}))[1].get("once_per_combo") is not True]
        first = (by_id.get(used[0]) or (None,))[0] if used else None
        if unbounded:
            rep.fail("cancels", "X3", (by_id.get(unbounded[0]) or (None,))[0],
                     f"cancel loop via {used} among {members} (e.g. {chain}) and {unbounded} not once per combo; "
                     f"stacked rules would loop")
        else:
            rep.confirm("cancels", "X3", first,
                        f"cancel cycle via {used} among {members} (e.g. {chain}) stops only by once per combo")
    if any(isinstance(d.get("path"), dict) and d["path"].get("from") == "successful_throws" for _, d in rules):
        for p, d in of_kind(docs, "special"):
            props = (((d.get("levels") or {}).get("1") or {}).get("frame_data") or {}).get("properties") or {}
            if props.get("throw"):
                rep.confirm("cancels", "C4", p,
                            f"'{d.get('id')}' is a command grab: the game does not count it as a successful throw "
                            f"for cancels (open question in rules.md, C4)")
    rep.confirm("cancels", "X3", None,
                f"with the {len(rules)} cancel rule(s) in data, held up to {MAX_HELD_CANCELS} at once (X2), "
                f"no move is cancelled into twice in one combo (runtime once-per-combo enforcement)")


# --- A3-A5 three levels per special -----------------------------------------------------------

def check_levels(docs, rep: Report):
    for p, d in of_kind(docs, "special"):
        levels = d.get("levels") if isinstance(d.get("levels"), dict) else {}
        keys = sorted(levels)
        if keys != ["1", "2", "3"]:
            rep.fail("levels", "A3", p, f"levels {keys}; every special has exactly Lv 1, 2 and 3")
        lv1 = levels.get("1") or {}
        if "1" in levels and not isinstance(lv1.get("frame_data"), dict):
            rep.fail("levels", "A3", p, "Lv 1 has no frame data")
        lv2 = levels.get("2") or {}
        if "2" in levels:
            t = lv2.get("tuning")
            if not isinstance(t, dict):
                rep.fail("levels", "A4", p, "Lv 2 must be one tuning step (a single effect in `tuning`)")
            elif t.get("target") == "damage":
                rep.fail("levels", "E21", p, "Lv 2 step changes damage alone; damage-only steps are out (P1, E21)")
            if "evolution" in lv2:
                rep.fail("levels", "A4", p, "Lv 2 carries an evolution; the evolution is Lv 3")
        lv3 = levels.get("3") or {}
        if "3" in levels:
            ev = lv3.get("evolution")
            if not isinstance(ev, dict):
                rep.fail("levels", "A4", p, "Lv 3 must be an evolution")
            else:
                if not ev.get("preset"):
                    rep.fail("levels", "A5", p, "Lv 3 evolution has no visual preset")
                if ev.get("locked") is True:
                    rep.fail("levels", "A5", p, "evolutions come with their special and are never locked")
                ch = [c for c in ev.get("changes") or [] if isinstance(c, dict)]
                if ch and all(c.get("target") == "damage" for c in ch):
                    rep.fail("levels", "P1", p, "Lv 3 evolution changes damage alone; upgrades change how you fight")


# --- C5 / E16 meter for EX only ---------------------------------------------------------------

def _meter_keys(node, path=""):
    if isinstance(node, dict):
        for k, v in node.items():
            here = f"{path}.{k}" if path else k
            if k == "meter_cost":
                yield here
            yield from _meter_keys(v, here)
    elif isinstance(node, list):
        for i, v in enumerate(node):
            yield from _meter_keys(v, f"{path}.{i}")


def _spends_meter(target) -> bool:
    return isinstance(target, str) and bool(re.search(r"(^|\.)(meter_cost|meter_spend|meter\.cost)$", target))


def check_meter(docs, rep: Report):
    for p, d in of_kind(docs, "special", "normal", "throw", "modifier"):
        kind = d.get("kind")
        for where in _meter_keys(d):
            if not (kind == "special" and where == "ex.meter_cost"):
                rep.fail("meter", "C5", p, f"{where}: only a special's EX (C5) or the burst (C6) spends meter")
        if kind == "special" and isinstance(d.get("ex"), dict):
            cost = d["ex"].get("meter_cost")
            if cost != EX_COST:
                rep.fail("meter", "C5", p, f"ex.meter_cost {cost!r}; an EX special costs 1 bar = {EX_COST} (C5, E16)")
            for i, c in enumerate(d["ex"].get("changes") or []):
                if isinstance(c, dict) and _spends_meter(c.get("target")):
                    rep.fail("meter", "C5", p, f"ex.changes.{i}: the EX changes its own cost; the cost is ex.meter_cost (E16)")
        effects = []
        if kind == "modifier":
            effects = [("effects", e) for e in d.get("effects") or []]
        elif kind == "special":
            lv = d.get("levels") or {}
            effects = [("levels.2.tuning", (lv.get("2") or {}).get("tuning"))]
            effects += [("levels.3.evolution.changes", c) for c in ((lv.get("3") or {}).get("evolution") or {}).get("changes") or []]
        for where, e in effects:
            if isinstance(e, dict) and _spends_meter(e.get("target")) and not str(e.get("target")).startswith("ex."):
                rep.fail("meter", "C5", p, f"{where}: target '{e.get('target')}' spends meter outside the EX version")


# --- K1-K3 every special in both schemes ------------------------------------------------------

def check_schemes(docs, rep: Report):
    for p, d in of_kind(docs, "special"):
        inp = d.get("input") if isinstance(d.get("input"), dict) else {}
        kata, kihon = inp.get("kata"), inp.get("kihon")
        if not isinstance(kata, dict):
            rep.fail("schemes", "K1", p, "no Kata input: every special needs its slot's motion")
        if not isinstance(kihon, dict):
            rep.fail("schemes", "K2", p, "no Kihon input: every special needs a Special + direction slot (Kihon is never cut, K4)")
        if isinstance(kata, dict):
            s = kata.get("slot")
            if s in KATA_MOTION and kata.get("motion") != KATA_MOTION[s]:
                rep.fail("schemes", "K1", p, f"Kata slot {s} is motion {KATA_MOTION[s]}, got {kata.get('motion')!r}")
        if isinstance(kihon, dict):
            s = kihon.get("slot")
            if s in KIHON_DIRECTION and kihon.get("direction") != KIHON_DIRECTION[s]:
                rep.fail("schemes", "K2", p, f"Kihon slot {s} is direction {KIHON_DIRECTION[s]}, got {kihon.get('direction')!r}")
        if isinstance(kata, dict) and isinstance(kihon, dict) and kata.get("slot") != kihon.get("slot"):
            rep.fail("schemes", "K1", p, f"Kata slot {kata.get('slot')} and Kihon slot {kihon.get('slot')} differ; "
                     f"K1 and K2 (E18) name the same four slots, so a special sits in one slot in both schemes")


# --- R7 trials completable with Kihon inputs --------------------------------------------------

def has_kihon(d: dict) -> bool:
    """Normals and throws are buttons in both schemes (K2, E12); a special needs its Kihon slot."""
    if d.get("kind") != "special":
        return True
    k = (d.get("input") or {}).get("kihon")
    return isinstance(k, dict) and k.get("slot") in SLOTS


def check_trials(docs, rep: Report):
    moves = {d.get("id"): d for _, d in of_kind(docs, "special", "normal", "throw")}
    for p, t in of_kind(docs, "trial"):
        sp = t.get("special")
        sd = moves.get(sp)
        if sd is None or sd.get("kind") != "special":
            rep.fail("trials", "R7", p, f"trial special '{sp}' is not a special in data/moves/")
        elif not has_kihon(sd):
            rep.fail("trials", "K2", p, f"trial special '{sp}' has no Kihon input; trials must be Kihon-completable")
        steps = ((t.get("goal") or {}).get("steps") or []) if isinstance(t.get("goal"), dict) else []
        for i, st in enumerate(steps):
            if not isinstance(st, dict):
                continue
            for key in KATA_ONLY_KEYS:
                if key in st:
                    rep.fail("trials", "K2", p, f"goal.steps.{i}.{key}: needs a Kata input; trials must be Kihon-completable")
            mid = st.get("move")
            if mid is None:
                continue
            md = moves.get(mid)
            if md is None:
                rep.fail("trials", "R7", p, f"goal.steps.{i}: move '{mid}' does not exist in data/moves/")
            elif not is_ryo_move(md):
                rep.fail("trials", "R7", p, f"goal.steps.{i}: '{mid}' is a {md.get('source')} move, not in Ryo's kit")
            elif not has_kihon(md):
                rep.fail("trials", "K2", p, f"goal.steps.{i}: '{mid}' has no Kihon input")
        dummy = (t.get("setup") or {}).get("dummy") if isinstance(t.get("setup"), dict) else None
        for j, mid in enumerate((dummy or {}).get("moves") or []):
            if mid not in moves:
                rep.fail("trials", "R7", p, f"setup.dummy.moves.{j}: '{mid}' does not exist in data/moves/")
        rep.confirm("trials", "R7", p, f"'{t.get('id')}' can be landed with Kihon inputs within {t.get('attempts', 3)} attempts")


CHECKS = (check_slots, check_cancels, check_levels, check_meter, check_schemes, check_trials)


def check(roots: list[Path]) -> tuple[Report, int]:
    docs, unreadable = load(roots)
    rep = Report()
    for f, err in unreadable:
        rep.fail("read", "", f, f"cannot read JSON: {err}")
    for fn in CHECKS:
        fn(docs, rep)
    return rep, len(docs)


def main(argv=None, out=sys.stdout) -> int:
    ap = argparse.ArgumentParser(description="Static rules check over data/ (rules.md IDs, GDD sections).")
    ap.add_argument("roots", nargs="*", help="data-like folders or files, merged in order (default: data/)")
    ap.add_argument("--json", action="store_true", help="print one JSON object instead of text")
    args = ap.parse_args(argv)
    roots = [Path(r) for r in args.roots] or [DATA]
    for r in roots:
        if not r.exists():
            print(f"check_rules: {r} does not exist", file=sys.stderr)
            return 2
    rep, n = check(roots)
    if args.json:
        json.dump({"ok": not rep.violations, "documents": n, "violations": rep.violations,
                   "harness_must_confirm": rep.harness}, out, indent=2, ensure_ascii=False)
        out.write("\n")
    else:
        for v in rep.violations:
            print(f"{v['file']}: {v['rule']} [{v['check']}] {v['message']} ({v['gdd']})", file=out)
        for h in rep.harness:
            print(f"harness must confirm: {h['file']}: {h['rule']} [{h['check']}] {h['message']}", file=out)
        print(f"check_rules: {n} document(s), {len(rep.violations)} violation(s), "
              f"{len(rep.harness)} item(s) the harness must confirm", file=out)
    return 1 if rep.violations else 0


if __name__ == "__main__":
    sys.exit(main())
