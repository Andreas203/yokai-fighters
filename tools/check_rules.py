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
TRIAL_ATTEMPTS = 3      # R7: a trial levels its special if landed within 3 attempts
STARTERS = 2            # A1
KATA_ONLY_KEYS = ("motion", "precision", "kata")  # same list as validate_data.TRIAL_KATA_ONLY
HEAVY_BUTTONS = ("HP", "HK")  # Match.Specials: CancelFrom.HeavyNormals = HP or HK normal



KINDS = ("special", "normal", "throw", "modifier", "cancel_rule", "profile", "clip", "story_card",
         "run_map", "node_scenes", "elder_twist", "trial", "fighter", "harness_bot", "merchant_shop_lines")
# validate_data.KIND_SCHEMA (no rule here reads harness_bot (YOK-26), elder_twist (YOK-90), fighter (YOK-136) or merchant_shop_lines (YOK-159))


# Shape guards: this check reads files the schema would refuse, so every value read from a document goes
# through one of these and a wrong type reads as absent instead of raising.
def D(x) -> dict:
    return x if isinstance(x, dict) else {}


def L(x) -> list:
    return x if isinstance(x, list) else []


def S(x):
    """A string, or None (also keeps unhashable values out of sets and dict keys)."""
    return x if isinstance(x, str) else None


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


def load(roots: list[Path], rep: "Report") -> dict:
    """(kind group, id) -> (path, doc). A later root replaces an earlier entry; two files with one kind and id
    inside the same root are a violation (neither may hide the other). Run-state docs key by path."""
    docs: dict = {}
    for root in roots:
        files = [root] if root.is_file() else sorted(root.rglob("*.json"))
        this_root: dict = {}
        for f in files:
            below = f.relative_to(root).parts[:-1] if root.is_dir() else ()
            if SKIP_DIRS.intersection(below):
                continue
            try:
                doc = json.loads(f.read_text(encoding="utf-8"))
            except (OSError, ValueError, RecursionError) as e:  # UnicodeDecodeError is a ValueError
                rep.fail("read", "", f, f"cannot read JSON: {type(e).__name__}: {str(e)[:200]}")
                continue
            if not isinstance(doc, dict):
                continue
            if "kind" in doc:
                kind = doc["kind"]
                if S(kind) not in KINDS:
                    rep.fail("read", "", f, f"unknown kind {kind!r}; expected one of {list(KINDS)}")
                    continue
                key = (group(kind), str(doc["id"]) if "id" in doc else f.stem)
                if key in this_root:
                    rep.fail("read", "", f, f"duplicate id: {key[0]} '{key[1]}' is also defined in {rel(this_root[key])}")
                this_root[key] = f
                docs[key] = (f, doc)
            elif "slots" in run_of(doc):   # a run state; a non-list `slots` is still one, and fails A1
                docs[("run", str(f))] = (f, doc)
    return docs


def run_of(doc: dict) -> dict:
    """A run state: the save file's `run` object (SaveStore v3) or the document itself."""
    return doc["run"] if isinstance(doc.get("run"), dict) else doc


def run_slots(doc: dict):
    slots = run_of(doc).get("slots")
    return slots if isinstance(slots, list) else None


def of_kind(docs, *kinds):
    return [(p, d) for (_, _), (p, d) in sorted(docs.items(), key=lambda kv: str(kv[1][0])) if d.get("kind") in kinds]


def is_ryo_move(d: dict) -> bool:
    """Ryo's kit: his own normals and throw, and every special (any source can be drafted, A7)."""
    return d.get("kind") == "special" or d.get("source") == "ryo"


def lv1_props(d: dict) -> dict:
    return D(D(D(D(d.get("levels")).get("1")).get("frame_data")).get("properties"))


# --- A1 slot limits, A2 one modifier per special, X2 cancel cap ---------------------------------

def check_slots(docs, rep: Report):
    specials = of_kind(docs, "special")
    starters: dict[str, list] = {}
    for p, d in specials:
        inp = D(d.get("input"))
        raw = D(inp.get("kihon")).get("slot") or D(inp.get("kata")).get("slot")
        if raw is not None and S(raw) not in SLOTS:
            rep.fail("slots", "A1", p, f"slot {raw!r} is not one of the four special slots A-D")
        if d.get("starter") is True:
            starters.setdefault(str(raw), []).append(d.get("id"))
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

    known_specials = {S(d.get("id")) for _, d in specials}
    known_mods = {S(d.get("id")) for _, d in of_kind(docs, "modifier")}
    known_cancels = {S(d.get("id")) for _, d in cancels}
    for (g, _), (p, doc) in sorted(docs.items(), key=lambda kv: str(kv[1][0])):
        if g != "run":
            continue
        raw_slots = run_of(doc).get("slots")
        if raw_slots is not None and not isinstance(raw_slots, list):
            rep.fail("slots", "A1", p, f"run slots is a {type(raw_slots).__name__}, not a list of at most four slotted specials")
        slots = run_slots(doc) or []
        if len(slots) > len(SLOTS):
            rep.fail("slots", "A1", p, f"{len(slots)} specials slotted; there are four slots")
        seen_slot, seen_special = {}, {}
        for i, s in enumerate(slots):
            where = f"slots[{i}]"
            if not isinstance(s, dict):
                rep.fail("slots", "A1", p, f"{where}: {s!r} is not a slot")
                continue
            letter, sp = S(s.get("slot")), S(s.get("special"))
            if letter not in SLOTS:
                rep.fail("slots", "A1", p, f"{where}: slot {s.get('slot')!r} is not one of A-D")
            elif letter in seen_slot:
                rep.fail("slots", "A1", p, f"{where}: slot {letter} is filled twice (also slots[{seen_slot[letter]}])")
            else:
                seen_slot[letter] = i
            if sp is None:
                rep.fail("slots", "A1", p, f"{where}: special {s.get('special')!r} is not a special id")
            elif sp in seen_special:
                rep.fail("slots", "A3", p, f"{where}: '{sp}' is slotted twice (also slots[{seen_special[sp]}]); drafting an owned special levels it up")
            else:
                seen_special[sp] = i
                if known_specials and sp not in known_specials:
                    rep.fail("slots", "A1", p, f"{where}: '{sp}' is not a special in data/moves/")
            lv = s.get("level")
            if lv is not None and (type(lv) is not int or lv not in (1, 2, 3)):
                rep.fail("levels", "A3", p, f"{where}: level {lv!r}; a special is Lv 1, 2 or 3")
            mod, mods = s.get("modifier"), s.get("modifiers")
            listed = ([mod] if mod is not None and not isinstance(mod, list) else L(mod)) + \
                     ([mods] if mods is not None and not isinstance(mods, list) else L(mods))
            listed = [m for m in listed if m]
            if len(listed) > 1:
                rep.fail("modifiers", "A2", p, f"{where}: '{sp}' carries {len(listed)} modifiers {listed}; one modifier per special")
            for m in listed:
                if S(m) is None:
                    rep.fail("modifiers", "A2", p, f"{where}: modifier {m!r} is not a modifier id")
                elif known_mods and m not in known_mods:
                    rep.fail("modifiers", "A2", p, f"{where}: modifier '{m}' is not in data/modifiers/")
        held = run_of(doc).get("cancels")
        if held is None:
            continue
        if not isinstance(held, list):
            rep.fail("cancel-cap", "X2", p, f"cancels {held!r} is not a list of cancel rule ids")
            continue
        if len(held) > MAX_HELD_CANCELS:
            rep.fail("cancel-cap", "X2", p, f"{len(held)} cancel rules held {held}; up to {MAX_HELD_CANCELS} at once")
        if len({json.dumps(c, sort_keys=True) for c in held}) != len(held):
            rep.fail("cancel-cap", "X2", p, f"a cancel rule is held twice: {held}")
        for c in held:
            if S(c) is None:
                rep.fail("cancel-cap", "X2", p, f"held cancel rule {c!r} is not a cancel rule id")
            elif known_cancels and c not in known_cancels:
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
        inp = D(d.get("input"))
        btn = S(inp.get("button"))
        if btn in HEAVY_BUTTONS or (not btn and any(S(b) in HEAVY_BUTTONS for b in L(inp.get("buttons")))):
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
        path = D(r.get("path"))
        src, dst = S(path.get("from")), S(path.get("into"))
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
    if any(D(d.get("path")).get("from") == "successful_throws" for _, d in rules):
        for p, d in of_kind(docs, "special"):
            if lv1_props(d).get("throw"):
                rep.confirm("cancels", "C4", p,
                            f"'{d.get('id')}' is a command grab: the game does not count it as a successful throw "
                            f"for cancels (open question in rules.md, C4)")
    rep.confirm("cancels", "X3", None,
                f"with the {len(rules)} cancel rule(s) in data, held up to {MAX_HELD_CANCELS} at once (X2), "
                f"no move is cancelled into twice in one combo (runtime once-per-combo enforcement)")


# --- A3-A5 three levels per special -----------------------------------------------------------

def _targets(node):
    """Every effect `target` inside a tuning step, however nested."""
    if isinstance(node, dict):
        if "target" in node:
            yield node["target"]
        for k, v in node.items():
            if k != "target":
                yield from _targets(v)
    elif isinstance(node, list):
        for v in node:
            yield from _targets(v)


def _is_damage(t) -> bool:
    t = t.strip().lower() if isinstance(t, str) else ""
    return t == "damage" or t.endswith(".damage")   # `damage`, or a nested one like `projectile.damage`


def check_levels(docs, rep: Report):
    for p, d in of_kind(docs, "special"):
        raw = d.get("levels")
        if not isinstance(raw, dict):
            rep.fail("levels", "A3", p, f"levels {type(raw).__name__}; every special has exactly Lv 1, 2 and 3")
            continue
        levels = raw
        keys = sorted(levels)
        if keys != ["1", "2", "3"]:
            rep.fail("levels", "A3", p, f"levels {keys}; every special has exactly Lv 1, 2 and 3")
        if "1" in levels and not isinstance(D(levels.get("1")).get("frame_data"), dict):
            rep.fail("levels", "A3", p, "Lv 1 has no frame data")
        if "2" in levels:
            lv2 = D(levels.get("2"))
            t = lv2.get("tuning")
            targets = list(_targets(t))
            if not isinstance(t, dict) or len(targets) != 1 or not all(S(x) for x in targets):
                rep.fail("levels", "A4", p, f"Lv 2 must be one tuning step (exactly one effect `target` in `tuning`, got {len(targets)})")
            if targets and all(_is_damage(x) for x in targets):
                rep.fail("levels", "E21", p, "Lv 2 step changes damage alone; damage-only steps are out (P1, E21)")
            if "evolution" in lv2:
                rep.fail("levels", "A4", p, "Lv 2 carries an evolution; the evolution is Lv 3")
        if "3" in levels:
            ev = D(levels.get("3")).get("evolution")
            if not isinstance(ev, dict):
                rep.fail("levels", "A4", p, "Lv 3 must be an evolution")
            else:
                preset = ev.get("preset")
                if not (isinstance(preset, str) and preset.strip()):
                    rep.fail("levels", "A5", p, f"Lv 3 evolution preset {preset!r}: no visual preset (an evolution names its preset as a string)")
                if ev.get("locked") is True:
                    rep.fail("levels", "A5", p, "evolutions come with their special and are never locked")
                targets = list(_targets(ev.get("changes")))
                if targets and all(_is_damage(x) for x in targets):
                    rep.fail("levels", "P1", p, "Lv 3 evolution changes damage alone; upgrades change how you fight")


# --- C5 / E16 meter for EX only ---------------------------------------------------------------

def _meter_keys(node, path=""):
    if isinstance(node, dict):
        for k, v in node.items():
            here = f"{path}.{k}" if path else str(k)
            if k == "meter_cost":
                yield here
            yield from _meter_keys(v, here)
    elif isinstance(node, list):
        for i, v in enumerate(node):
            yield from _meter_keys(v, f"{path}.{i}")


def _spends_meter(target) -> bool:
    return isinstance(target, str) and bool(re.search(r"(^|\.)(meter_cost|meter_spend|meter\.cost)$", target.strip().lower()))


def check_meter(docs, rep: Report):
    for p, d in of_kind(docs, "special", "normal", "throw", "modifier"):
        kind = d.get("kind")
        for where in _meter_keys(d):
            if not (kind == "special" and where == "ex.meter_cost"):
                rep.fail("meter", "C5", p, f"{where}: only a special's EX (C5) or the burst (C6) spends meter")
        ex = d.get("ex")
        if kind == "special" and ex is not None:
            if not isinstance(ex, dict):
                rep.fail("meter", "C5", p, f"ex {ex!r} is not an EX block with a meter_cost")
            else:
                cost = ex.get("meter_cost")
                if type(cost) is not int or cost != EX_COST:
                    rep.fail("meter", "C5", p, f"ex.meter_cost {cost!r}; an EX special costs 1 bar = {EX_COST} (C5, E16)")
                for t in _targets(ex.get("changes")):   # a list, a single dict or nested: every target counts
                    if _spends_meter(t):
                        rep.fail("meter", "C5", p, f"ex.changes: target '{t}' changes the EX's own cost; the cost is ex.meter_cost (E16)")
        effects = []
        if kind == "modifier":
            effects = [("effects", e) for e in L(d.get("effects"))]
        elif kind == "special":
            lv = D(d.get("levels"))
            effects = [("levels.2.tuning", t) for t in [D(lv.get("2")).get("tuning")]]
            effects += [("levels.3.evolution.changes", c) for c in L(D(D(lv.get("3")).get("evolution")).get("changes"))]
        for where, e in effects:
            for t in _targets(e):
                if _spends_meter(t) and not str(t).strip().lower().startswith("ex."):
                    rep.fail("meter", "C5", p, f"{where}: target '{t}' spends meter outside the EX version")


# --- K1/K2 every special in both schemes ------------------------------------------------------

def check_schemes(docs, rep: Report):
    for p, d in of_kind(docs, "special"):
        inp = D(d.get("input"))
        kata, kihon = inp.get("kata"), inp.get("kihon")
        ks, hs = S(D(kata).get("slot")), S(D(kihon).get("slot"))
        if not isinstance(kata, dict) or ks not in SLOTS:
            rep.fail("schemes", "K1", p, "no Kata input on a slot A-D: every special needs its slot's motion")
        if not isinstance(kihon, dict) or hs not in SLOTS:
            rep.fail("schemes", "K2", p, "no Kihon input: every special needs a Special + direction slot (Kihon is never cut, K4)")
        if ks in KATA_MOTION and kata.get("motion") != KATA_MOTION[ks]:
            rep.fail("schemes", "K1", p, f"Kata slot {ks} is motion {KATA_MOTION[ks]}, got {kata.get('motion')!r}")
        if hs in KIHON_DIRECTION and kihon.get("direction") != KIHON_DIRECTION[hs]:
            rep.fail("schemes", "K2", p, f"Kihon slot {hs} is direction {KIHON_DIRECTION[hs]}, got {kihon.get('direction')!r}")
        if ks in SLOTS and hs in SLOTS and ks != hs:
            rep.fail("schemes", "K1", p, f"Kata slot {ks} and Kihon slot {hs} differ; "
                     f"K1 and K2 (E18) name the same four slots, so a special sits in one slot in both schemes")


# --- R7 trials completable with Kihon inputs --------------------------------------------------

def _kata_only_keys(node, path: str):
    """Every Kata-only key (KATA_ONLY_KEYS) anywhere inside a trial step, with its dotted path."""
    if isinstance(node, dict):
        for k, v in node.items():
            here = f"{path}.{k}"
            if k in KATA_ONLY_KEYS:
                yield here
            yield from _kata_only_keys(v, here)
    elif isinstance(node, list):
        for j, v in enumerate(node):
            yield from _kata_only_keys(v, f"{path}.{j}")


def has_kihon(d: dict) -> bool:
    """Normals and throws are buttons in both schemes (K2, E12); a special needs its Kihon slot."""
    if d.get("kind") != "special":
        return True
    return S(D(D(d.get("input")).get("kihon")).get("slot")) in SLOTS


def check_trials(docs, rep: Report):
    moves = {S(d.get("id")): d for _, d in of_kind(docs, "special", "normal", "throw") if S(d.get("id"))}
    for p, t in of_kind(docs, "trial"):
        sp = t.get("special")
        sd = moves.get(S(sp))
        if sd is None or sd.get("kind") != "special":
            rep.fail("trials", "R7", p, f"trial special {sp!r} is not a special in data/moves/")
        elif not has_kihon(sd):
            rep.fail("trials", "K2", p, f"trial special '{sp}' has no Kihon input; trials must be Kihon-completable")
        steps = D(t.get("goal")).get("steps")
        if not isinstance(steps, list) or not steps:
            rep.fail("trials", "R7", p, f"goal.steps {steps!r}: a trial needs a list of steps")
        for i, st in enumerate(L(steps)):
            if not isinstance(st, dict):
                rep.fail("trials", "R7", p, f"goal.steps.{i}: {st!r} is not a step")
                continue
            for where in _kata_only_keys(st, f"goal.steps.{i}"):   # nested too, e.g. require.motion
                rep.fail("trials", "K2", p, f"{where}: needs a Kata input; trials must be Kihon-completable")
            mid = st.get("move")
            if mid is None:
                continue
            md = moves.get(S(mid))
            if md is None:
                rep.fail("trials", "R7", p, f"goal.steps.{i}: move {mid!r} does not exist in data/moves/")
            elif not is_ryo_move(md):
                rep.fail("trials", "R7", p, f"goal.steps.{i}: '{mid}' is a {md.get('source')} move, not in Ryo's kit")
            elif not has_kihon(md):
                rep.fail("trials", "K2", p, f"goal.steps.{i}: '{mid}' has no Kihon input")
        for j, mid in enumerate(L(D(D(t.get("setup")).get("dummy")).get("moves"))):
            if S(mid) not in moves:
                rep.fail("trials", "R7", p, f"setup.dummy.moves.{j}: {mid!r} does not exist in data/moves/")
        attempts = t.get("attempts", TRIAL_ATTEMPTS)
        if type(attempts) is not int or attempts != TRIAL_ATTEMPTS:
            rep.fail("trials", "R7", p, f"attempts {attempts!r}: the special levels only if landed within {TRIAL_ATTEMPTS} attempts")
            attempts = TRIAL_ATTEMPTS
        rep.confirm("trials", "R7", p, f"'{t.get('id')}' can be landed with Kihon inputs within {attempts!r} attempts")


CHECKS = (check_slots, check_cancels, check_levels, check_meter, check_schemes, check_trials)


def check(roots: list[Path]) -> tuple[Report, int]:
    rep = Report()
    docs = load(roots, rep)
    for fn in CHECKS:
        try:
            fn(docs, rep)
        except Exception as e:  # noqa: BLE001 - backstop: a malformed file must be reported, never a traceback
            rep.fail("internal", "", None, f"{fn.__name__} stopped on malformed data: {type(e).__name__}: {e}")
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
    if n == 0 and not rep.violations:
        print(f"check_rules: no documents found under {', '.join(map(str, roots))}", file=sys.stderr)
        return 2
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
