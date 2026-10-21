#!/usr/bin/env python3
"""Validate Yokai Fighters content files against data/schema/ (YOK-12).

Usage:
    python3 tools/validate_data.py [paths...]

With no paths, validates every *.json under data/ except data/schema/ and any
directory named `invalid/` (deliberately malformed samples). Files passed
explicitly are always validated. Exit code 0 = all valid, 1 = errors, 2 = setup
problem. Each error prints as `<file>: <field.path>: <message>`.

Each file declares its type in a top-level "kind" field. Beyond the JSON Schema,
the validator checks rules that need more than one field or file: id matches the
file name, rule IDs exist in docs/design/rules.md, clip references resolve and
match the frame data (F2), hitboxes sit inside the active window, and a few
rules.md consistency checks. It also does what the game's ability loader does
(YOK-103): every special is built at every level and EX, and every modifier is
applied to every special it claims, so a file the game would refuse to load
fails here first. The game suite stays the last word: before a content PR is
reviewed, run it too (godot --headless --path game res://tests/test_runner.tscn);
ModifierEffectTests builds the real ability pool from data/.

Requires the `jsonschema` package (see tools/requirements.txt).
"""
from __future__ import annotations

import copy
import json
import os
import re
import sys
from pathlib import Path

try:
    from jsonschema import Draft202012Validator
    from referencing import Registry, Resource
except ImportError:  # pragma: no cover
    sys.stderr.write("validate_data: needs jsonschema>=4.18 (pip install -r tools/requirements.txt)\n")
    sys.exit(2)

ROOT = Path(__file__).resolve().parent.parent
if os.environ.get("YOK_CONTENT_ROOT"):
    # YOK-34: the data merge check runs the base branch's validator on a PR's checkout, named here.
    ROOT = Path(os.environ["YOK_CONTENT_ROOT"]).resolve()
DATA = ROOT / "data"
SCHEMA_DIR = DATA / "schema"
RULES_MD = ROOT / "docs" / "design" / "rules.md"

KIND_SCHEMA = {
    "special": "special.schema.json",
    "normal": "normal.schema.json",
    "throw": "throw.schema.json",
    "modifier": "modifier.schema.json",
    "cancel_rule": "cancel-rule.schema.json",
    "profile": "profile.schema.json",
    "clip": "clip.schema.json",
    "story_card": "story-card.schema.json",
    "merchant_shop_lines": "merchant-shop-lines.schema.json",
    "run_map": "run-map.schema.json",
    "node_scenes": "node-scenes.schema.json",
    "elder_twist": "elder-twist.schema.json",
    "trial": "trial.schema.json",
    "fighter": "fighter.schema.json",
    "harness_bot": "harness-bot.schema.json",
    "harness_bot_levels": "harness-bot-levels.schema.json",
}
# Folder under data/ -> the kinds its files may declare. Other folders (samples/) accept any kind.
DIR_KIND = {
    "moves": ("special", "normal", "throw"),
    "modifiers": ("modifier",),
    "cancels": ("cancel_rule",),
    "profiles": ("profile",),
    "clips": ("clip",),
    "story": ("story_card", "merchant_shop_lines"),
    "map": ("run_map", "node_scenes"),
    "twists": ("elder_twist",),
    "trials": ("trial",),
    "fighters": ("fighter",),
    "harness": ("harness_bot", "harness_bot_levels"),
}
# Step keys that only Kata can produce (K1); a trial must be Kihon-completable (R7, K2). Same list as TrialLoader.KataOnlyKeys.
TRIAL_KATA_ONLY = ("motion", "precision", "kata")
ELDER_SOURCE = {"nine-tailed-kitsune": "kitsune", "elder-oni": "oni", "elder-kappa": "kappa"}


def rel(path: Path) -> str:
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return path.as_posix()


def load_validators() -> dict[str, Draft202012Validator]:
    schemas = [json.loads(p.read_text(encoding="utf-8")) for p in sorted(SCHEMA_DIR.glob("*.schema.json"))]
    registry = Registry().with_resources((s["$id"], Resource.from_contents(s)) for s in schemas)
    by_name = {s["$id"].rsplit("/", 1)[-1]: s for s in schemas}
    out = {}
    for kind, name in KIND_SCHEMA.items():
        Draft202012Validator.check_schema(by_name[name])
        out[kind] = Draft202012Validator(by_name[name], registry=registry)
    return out


def load_rule_ids() -> set[str]:
    text = RULES_MD.read_text(encoding="utf-8")
    return set(re.findall(r"^- \*\*([A-Z][0-9]+)\*\*", text, flags=re.M))


def collect(paths: list[str]) -> list[Path]:
    if not paths:
        paths = [str(DATA)]
    files: list[Path] = []
    for raw in paths:
        p = Path(raw)
        if p.is_dir():
            for f in sorted(p.rglob("*.json")):
                # Skip invalid/ and schema/ below the given folder (pass them explicitly to check them).
                below = f.relative_to(p).parts[:-1]
                if "invalid" in below or SCHEMA_DIR.resolve() in f.resolve().parents:
                    continue
                files.append(f)
        else:
            files.append(p)
    return files


def field(path) -> str:
    return ".".join(str(x) for x in path) or "(root)"


def is_int(v) -> bool:
    return isinstance(v, int) and not isinstance(v, bool)


def check_ranges(items, lo, hi, where, what, errs):
    for i, item in enumerate(items or []):
        fr = item.get("frames") if isinstance(item, dict) else None
        if not (isinstance(fr, list) and len(fr) == 2 and all(whole(x) is not None for x in fr)):
            continue
        a, b = whole(fr[0]), whole(fr[1])
        if a > b:
            errs.append((f"{where}.{i}.frames", f"first frame {a} is after last frame {b}"))
        elif a < lo or b > hi:
            errs.append((f"{where}.{i}.frames", f"{what} frames {a}-{b} fall outside {lo}-{hi}"))


def check_frame_data(fd, where, clip, clip_name, errs):
    if not isinstance(fd, dict):
        return
    s, a, r = whole(fd.get("startup")), fd.get("active"), whole(fd.get("recovery"))
    a = None if a is None else whole(a)
    if s is None or r is None or (fd.get("active") is not None and a is None):
        return
    total = s + (a or 0) + r
    props = fd.get("properties") or {}
    if a is None:
        if "projectile" not in props:
            errs.append((f"{where}.active", "active is null ('—') but properties.projectile is missing"))
        if fd.get("hitboxes"):
            errs.append((f"{where}.hitboxes", "active is null ('—'), so the body has no hitboxes; put the hit on the projectile"))
    else:
        check_ranges(fd.get("hitboxes"), s + 1, s + a, f"{where}.hitboxes", "hitbox", errs)
    check_ranges(fd.get("hurtboxes"), 1, total, f"{where}.hurtboxes", "hurtbox", errs)
    check_ranges(fd.get("cancel_windows"), 1, total, f"{where}.cancel_windows", "cancel window", errs)
    proj = props.get("projectile")
    if isinstance(proj, dict) and whole(proj.get("spawn_frame")) is not None and not 1 <= whole(proj["spawn_frame"]) <= total:
        errs.append((f"{where}.properties.projectile.spawn_frame", f"spawn frame {proj['spawn_frame']} is outside the move (1-{total})"))
    if isinstance(clip, dict):  # F2: frame data must match the clip's real timing
        ft, hs, he = clip.get("frames_total"), clip.get("hit_start"), clip.get("hit_end")
        if is_int(ft) and ft != total:
            errs.append((where, f"startup+active+recovery = {total} but clip '{clip_name}' has frames_total {ft} (F2)"))
        if is_int(hs) and hs != s + 1:
            errs.append((f"{where}.startup", f"startup {s} means first hit on frame {s + 1}, but clip '{clip_name}' hit_start is {hs} (F2)"))
        if is_int(he) and he != s + (a or 1):
            errs.append((f"{where}.active", f"active window ends on frame {s + (a or 1)}, but clip '{clip_name}' hit_end is {he} (F2)"))


def check_run_map(doc: dict) -> list[tuple[str, str]]:
    """YOK-76: the graph checks of the game's loader (MapGraph), plus R1's row layout when the map lists R1.
    Node types stay an open id here: the game's node-type registry rejects an unregistered one at load."""
    errs: list[tuple[str, str]] = []
    nodes = [n for n in doc.get("nodes") or [] if isinstance(n, dict) and isinstance(n.get("id"), str) and is_int(n.get("row"))]
    by_id: dict[str, dict] = {}
    for n in nodes:
        if n["id"] in by_id:
            errs.append(("nodes", f"node id '{n['id']}' is defined twice"))
        else:
            by_id[n["id"]] = n
    rows = sorted({n["row"] for n in by_id.values()})
    nxt: dict[str, list[str]] = {i: [] for i in by_id}
    for r in doc.get("routes") or []:
        if not isinstance(r, dict):
            continue
        a, b = r.get("from"), r.get("to")
        missing = [x for x in (a, b) if x not in by_id]
        for x in missing:
            errs.append(("routes", f"route {a} -> {b}: unknown node '{x}'"))
        if missing:
            continue
        ra, rb = by_id[a]["row"], by_id[b]["row"]
        if rb <= ra:
            errs.append(("routes", f"route {a} -> {b} goes {'sideways' if rb == ra else 'backwards'} (row {ra} to row {rb})"))
        elif rows[rows.index(ra) + 1] != rb:
            errs.append(("routes", f"route {a} -> {b} skips a row (row {ra} to row {rb})"))
        elif b in nxt[a]:
            errs.append(("routes", f"route {a} -> {b} is listed twice"))
        else:
            nxt[a].append(b)
    if not by_id:
        return errs
    reached = {i for i, n in by_id.items() if n["row"] == rows[0]}
    todo = list(reached)
    while todo:
        for b in nxt[todo.pop()]:
            if b not in reached:
                reached.add(b)
                todo.append(b)
    for i, n in by_id.items():
        if i not in reached:
            errs.append(("nodes", f"node '{i}' is unreachable: no route leads to it from a start node"))
        if not nxt[i] and n["row"] != rows[-1]:
            errs.append(("nodes", f"node '{i}' is a dead end: no route out before the last row ({rows[-1]})"))

    if "R1" in (doc.get("rules") or []):
        if rows != list(range(1, 10)):
            errs.append(("nodes", f"R1: rows must be 1-8 plus the boss in row 9, got {rows}"))
        allowed = {1: ("duel",), 2: ("duel",), 3: ("duel",), 4: ("merchant",), 5: ("elder",), 6: ("duel",),
                   7: ("merchant", "dojo"), 8: ("elder",), 9: ("boss",)}
        for i, n in by_id.items():
            want = allowed.get(n["row"])
            if want and n.get("type") not in want:
                errs.append(("nodes", f"R1: node '{i}': row {n['row']} must be {' or '.join(want)}, got '{n.get('type')}'"))
            if n["row"] == 1 and n.get("yokai") != "apprentice":
                errs.append(("nodes", f"R1: node '{i}': row 1 is the apprentice, got '{n.get('yokai')}'"))
        boss = [n for n in by_id.values() if n["row"] == 9]
        if len(boss) != 1 or boss[0].get("yokai") != "tanuki":
            errs.append(("nodes", "R1: row 9 must be exactly one boss node with yokai 'tanuki'"))
    return errs


# \Z and fullmatch: '$' would also accept a trailing newline. The schema's $defs/stage can only say '$', so this
# check and NodeSceneTable.StagePattern (which ends in \z) are the strict ones.
STAGE_PATTERN = re.compile(r"^res://scenes/stage/(?:[A-Za-z0-9_-]+/)*[A-Za-z0-9_-]+\.tscn\Z", re.ASCII)


def check_node_scenes(doc: dict, path: Path) -> list[tuple[str, str]]:
    """YOK-77: every tier named is defined, and every node type used by a run map in the same folder has an
    entry (the game's resolver refuses a node whose type has no Y4 tier)."""
    errs: list[tuple[str, str]] = []
    tiers = doc.get("tiers") if isinstance(doc.get("tiers"), dict) else {}
    types = doc.get("types") if isinstance(doc.get("types"), dict) else {}
    for section in ("types", "nodes", "opponents"):
        entries = doc.get(section) if isinstance(doc.get(section), dict) else {}
        for name, entry in entries.items():
            tier = entry.get("tier") if isinstance(entry, dict) else None
            if isinstance(tier, str) and tier not in tiers:
                errs.append((f"{section}.{name}.tier", f"tier '{tier}' is not defined in 'tiers' (Y4)"))
    # YOK-144: every stage named is a scene under scenes/stage/ that exists (res:// = game/). STAGE_PATTERN is the
    # schema's $defs/stage pattern and NodeSceneTable.StagePattern; it also stops '..' from escaping the folder here.
    stages = [("default_stage", doc.get("default_stage"))]
    for section in ("types", "nodes"):
        entries = doc.get(section) if isinstance(doc.get(section), dict) else {}
        stages += [(f"{section}.{name}.stage", e.get("stage")) for name, e in entries.items() if isinstance(e, dict)]
    for where, stage in stages:
        if not isinstance(stage, str):
            continue
        if not STAGE_PATTERN.fullmatch(stage):
            errs.append((where, f"stage '{stage}' is not a scene path under res://scenes/stage/ (no '..', '//' or '\\')"))
        elif not (ROOT / "game" / stage[len("res://"):]).is_file():
            errs.append((where, f"stage scene '{stage}' does not exist under game/"))
    for other in sorted(path.parent.glob("*.json")):
        try:
            m = json.loads(other.read_text(encoding="utf-8"))
        except (OSError, ValueError):
            continue
        if not isinstance(m, dict) or m.get("kind") != "run_map":
            continue
        used = {n.get("type") for n in m.get("nodes") or [] if isinstance(n, dict) and isinstance(n.get("type"), str)}
        for t in sorted(used - set(types)):
            errs.append(("types", f"node type '{t}' is used by {other.name} but has no entry"))
    return errs


# --- YOK-103: the ability loader's own checks, mirrored -------------------------------------------
# A schema can say which effect targets exist, not whether an effect still leaves a legal move on the
# special it lands on. PR #92 passed the schema and broke every fight, so this section does what the C#
# loader does (game/scripts/Sim/SpecialData.cs: Effects.Apply, SpecialData.Build / Legal; MoveLoader):
# it builds every level and EX version of each special, and every modifier on every special it applies
# to, and reports what the game would refuse to load. Both sides run the shared case list
# game/tests/fixtures/modifier-gate/cases.json (tools/test_validate_data.py, ModifierEffectTests), which
# is what keeps the two copies in step. The loader itself is still the last word: the game suite builds
# the real pool from data/ (ModifierEffectTests.EveryDataModifierLoadsOnEverySpecialItAppliesTo).

FRAME_FIELDS = {"startup", "active", "recovery", "damage", "hitstun", "blockstun", "pushback", "strength",
                "meter_gain", "hitboxes", "hurtboxes", "cancel_windows", "properties"}
INT_TARGETS = {"startup", "active", "recovery", "damage", "hitstun", "blockstun", "pushback.on_hit", "pushback.on_block",
               "meter_gain.on_hit", "meter_gain.on_block", "properties.projectile.speed", "properties.projectile.hits",
               "properties.projectile.absorbs_projectiles", "properties.projectile.max_active",
               "properties.projectile.lifetime", "properties.pull_on_hit", "properties.heal_pct_max_health",
               "properties.aftershock_delay"}
# SimConfig defaults a modifier scales when the move omits the value (C5 meter gains, E5 pushback).
EFFECT_DEFAULTS = {"meter_gain.on_hit": 6, "meter_gain.on_block": 3, "pushback.on_hit": 40, "pushback.on_block": 50,
                   "properties.pull_on_hit": 0, "properties.heal_pct_max_health": 0}
START_CONDITIONS = ("first_use_each_round", "ryo_health_below_30pct", "holding_up")
# Folders whose specials the game's ability pool can hold (AbilityPool.LoadRepo / LoadFixtures).
SPECIAL_DIRS = (DATA / "moves", ROOT / "game" / "tests" / "fixtures" / "specials", ROOT / "game" / "tests" / "fixtures" / "rewards")


SAMPLES_DIR = (DATA / "samples").resolve()


class Unbuildable(Exception):
    """The loader would throw FormatException here."""


INT32 = (-2**31, 2**31 - 1)


def whole(v):
    """A whole number in any JSON notation (2, 2.0, 1e1) inside int32, as an int, else None: the loader's
    Effects.Whole / WholeInt. Frame data, effect values and results all follow this one rule."""
    if isinstance(v, bool):
        return None
    if isinstance(v, float):
        if not v.is_integer():
            return None
        v = int(v)
    if isinstance(v, int) and INT32[0] <= v <= INT32[1]:
        return v
    return None


def combine(op: str, n: int, v: int, target: str) -> int:
    """add / mul_pct as Effects.Combine: a result outside int32 is refused, never wrapped."""
    r = n + v if op == "add" else mul_pct(n, v)
    if not INT32[0] <= r <= INT32[1]:
        raise Unbuildable(f"effect '{op}' on {target} gives {r}, outside the whole numbers the fight holds")
    return r


def mul_pct(n: int, pct: int) -> int:
    q = n * pct  # nearest, half away from zero (E20)
    return (q + 50) // 100 if q >= 0 else -((-q + 50) // 100)


def resolve_target(target: str) -> list[str]:
    parts = target.split(".")
    return parts if parts[0] in FRAME_FIELDS else ["properties"] + parts


def read_armour(v):
    if not isinstance(v, dict) or set(v) - {"frames", "hits"}:
        return None
    fr = v.get("frames")
    if not (isinstance(fr, list) and len(fr) == 2):
        return None
    first, last, hits = whole(fr[0]), whole(fr[1]), whole(v.get("hits", 1))
    if first is None or last is None or hits is None or first < 1 or last < first or hits < 1:
        return None
    return {"frames": [first, last], "hits": hits}


def apply_effect(fd: dict, effect: dict, defaults: bool) -> None:
    """Effects.Apply: one unconditional effect on a frame_data dict (already schema-valid)."""
    target, op, value = effect["target"], effect["op"], effect["value"]
    path = resolve_target(target)
    key = ".".join(path)
    if key == "pushback":
        for side in ("on_hit", "on_block"):
            apply_effect(fd, {"target": f"{target}.{side}", "op": op, "value": value}, defaults)
        return
    node = fd
    for seg in path[:-1]:
        if not isinstance(node.get(seg), dict):
            if op != "set" and not defaults:
                raise Unbuildable(f"effect target {target} doesn't exist for '{op}'")
            node[seg] = {}
        node = node[seg]
    leaf = path[-1]
    if defaults and len(path) == 2 and path[0] in ("meter_gain", "pushback"):
        for side in ("on_hit", "on_block"):
            if node.get(side) is None:
                node[side] = EFFECT_DEFAULTS[f"{path[0]}.{side}"]
    if op == "set":
        if key in INT_TARGETS:
            value = whole(value)
        elif key == "properties.armour":
            value = read_armour(value)
            if value is None:
                raise Unbuildable("armour needs frames [first, last] with first <= last and hits >= 1")
        node[leaf] = copy.deepcopy(value)
        return
    cur, v = whole(node.get(leaf)), whole(value)
    if cur is None and defaults:
        cur = EFFECT_DEFAULTS.get(key)
    if cur is None or v is None:
        raise Unbuildable(f"effect '{op}' needs an integer at {target}")
    node[leaf] = combine(op, cur, v, target)


def built_move_errors(fd: dict) -> list[str]:
    """What MoveLoader and SpecialData.Legal refuse in a special as built (after level, EX and modifier effects)."""
    out: list[str] = []

    def num(obj, name, default=None):
        v = obj.get(name, default) if isinstance(obj, dict) else default
        return default if v is None else whole(v)

    def bad_numbers(o, where):  # MoveLoader reads every number in frame data with WholeInt
        if isinstance(o, dict):
            for k, x in o.items():
                yield from bad_numbers(x, f"{where}.{k}" if where else k)
        elif isinstance(o, list):
            for i, x in enumerate(o):
                yield from bad_numbers(x, f"{where}.{i}")
        elif isinstance(o, (int, float)) and not isinstance(o, bool) and whole(o) is None:
            yield f"{where} = {o} is not a whole number inside int32"

    out.extend(bad_numbers(fd, ""))
    if out:
        return out
    s, a, r = num(fd, "startup"), num(fd, "active", 0), num(fd, "recovery")
    if s is None or a is None or r is None or num(fd, "damage") is None:
        return ["startup, active, recovery and damage must be whole numbers"]
    for name, value, lo in (("startup", s, 1), ("active", a, 0), ("recovery", r, 0), ("damage", num(fd, "damage"), 0),
                            ("hitstun", num(fd, "hitstun", 0), 0), ("blockstun", num(fd, "blockstun", 0), 0),
                            ("pushback.on_hit", num(fd.get("pushback"), "on_hit", 0), 0),
                            ("pushback.on_block", num(fd.get("pushback"), "on_block", 0), 0),
                            ("meter_gain.on_hit", num(fd.get("meter_gain"), "on_hit", 0), 0),
                            ("meter_gain.on_block", num(fd.get("meter_gain"), "on_block", 0), 0)):
        if value is None or value < lo:
            out.append(f"{name} is {value} after its effects; it must be at least {lo}")
    total = s + a + r
    stun_given = all(isinstance(fd.get(k), (int, float)) and not isinstance(fd.get(k), bool) for k in ("hitstun", "blockstun"))
    props0 = fd.get("properties") if isinstance(fd.get("properties"), dict) else {}
    if isinstance(props0.get("projectile"), dict) and num(fd, "damage") > 0 and not stun_given:
        out.append("a damaging projectile needs hitstun and blockstun")
    if fd.get("hitboxes") and not stun_given:
        out.append("a move with hitboxes needs hitstun and blockstun")
    for i, hb in enumerate(fd.get("hitboxes") or []):
        fr = hb.get("frames") if isinstance(hb, dict) else None
        if isinstance(fr, list) and len(fr) == 2 and all(whole(x) is not None for x in fr) \
                and not (a > 0 and s + 1 <= whole(fr[0]) and whole(fr[1]) <= s + a):
            out.append(f"hitbox frames {fr[0]}-{fr[1]} outside the active window")
    props = fd.get("properties") if isinstance(fd.get("properties"), dict) else {}
    proj = props.get("projectile")
    if isinstance(proj, dict):
        missing = [k for k in ("spawn_frame", "speed", "hitbox", "hits") if k not in proj]
        if missing:
            out.append(f"properties.projectile.{missing[0]} is required")
        else:
            if num(proj, "spawn_frame", 0) > total:
                out.append(f"projectile spawn_frame {proj['spawn_frame']} is after the move ends")
            if num(proj, "hits", 0) < 1 or num(proj, "max_active", 1) < 1:
                out.append("projectile hits and max_active must be >= 1")
            for name in ("speed", "lifetime", "absorbs_projectiles"):
                if num(proj, name, 0) < 0:
                    out.append(f"projectile.{name} is {proj[name]} after its effects; it must be at least 0")
    if "armour" in props:
        arm = read_armour(props["armour"])
        if arm is None or arm["frames"][1] > total:
            out.append(f"properties.armour needs frames [first, last] inside the move (1-{total})")
    for name in ("pull_on_hit", "heal_pct_max_health"):
        if name in props and (whole(props[name]) is None or whole(props[name]) < 0):
            out.append(f"properties.{name} must be a whole number >= 0")
    if props.get("aftershock_delay") is not None:  # YOK-136: MoveData.ReadAftershock
        if whole(props["aftershock_delay"]) is None or whole(props["aftershock_delay"]) < 1:
            out.append("properties.aftershock_delay must be a whole number >= 1")
        elif not fd.get("hitboxes"):
            out.append("properties.aftershock_delay needs the move's own hitboxes to replay")
    if "throw_invuln" in props and props["throw_invuln"] != "startup":
        out.append('properties.throw_invuln must be "startup"')
    return out


def special_builds(doc: dict):
    """(label, frame_data) for every level and EX version the loader builds from a special, or raises Unbuildable."""
    levels = doc.get("levels") or {}
    base = (levels.get("1") or {}).get("frame_data")
    if not isinstance(base, dict):
        return
    tuning = (levels.get("2") or {}).get("tuning")
    changes = ((levels.get("3") or {}).get("evolution") or {}).get("changes")
    top = 3 if tuning and changes else 2 if tuning else 1
    ex = doc.get("ex") or {}
    for level in range(1, top + 1):
        fd = copy.deepcopy(base)
        steps = ([tuning] if level >= 2 else []) + (list(changes) if level >= 3 else [])
        for e in steps:
            apply_effect(fd, e, defaults=False)
        yield f"Lv {level}", fd
        if whole(ex.get("meter_cost")) and whole(ex.get("meter_cost")) > 0:
            exfd = copy.deepcopy(fd)
            for e in ex.get("changes") or []:
                apply_effect(exfd, e, defaults=False)
            yield f"Lv {level} EX", exfd


def has_property(special: dict, name: str) -> bool:
    fd = ((special.get("levels") or {}).get("1") or {}).get("frame_data") or {}
    if fd.get(name) is True:
        return True
    p = (fd.get("properties") or {}).get(name)
    return p is not None and p is not False


def check_special_builds(doc: dict, errs) -> None:
    try:
        for label, fd in special_builds(doc):
            for msg in built_move_errors(fd):
                errs.append(("levels", f"{label}: {msg}"))
    except Unbuildable as e:
        errs.append(("levels", f"{e}"))


def check_modifier(doc: dict, specials: dict[str, dict], errs) -> None:
    """ModifierLoader.Parse rules a schema can't state, then SpecialData.Build on every special it applies to."""
    effects = [e for e in doc.get("effects") or [] if isinstance(e, dict)]
    requires = (doc.get("applies_to") or {}).get("requires") or []
    frame_effects = []
    before = len(errs)
    for i, e in enumerate(effects):
        target = e.get("target")
        if not isinstance(target, str) or target == "ex.meter_cost":
            continue
        path = resolve_target(target)
        if path[:2] == ["properties", "projectile"] and "projectile" not in requires:
            errs.append((f"effects.{i}.target", f"'{target}' changes a projectile, so applies_to.requires must list \"projectile\""))
        if ".".join(path) == "properties.armour" and e.get("op") == "set" and read_armour(e.get("value")) is None:
            errs.append((f"effects.{i}.value", "armour needs frames [first, last] with first <= last and hits >= 1"))
        frame_effects.append(e)
    if len(errs) > before:
        return
    ex_effects = [e for e in effects if e.get("target") == "ex.meter_cost"]
    for sid in sorted(specials):  # ModifierData.ExCostFor on every special with an EX it applies to
        special = specials[sid]
        cost = whole((special.get("ex") or {}).get("meter_cost"))
        if not ex_effects or not cost or not all(has_property(special, r) for r in requires):
            continue
        try:
            for e in ex_effects:
                v = whole(e["value"])
                cost = v if e["op"] == "set" else combine(e["op"], cost, v, "ex.meter_cost")
        except Unbuildable as ex:
            errs.append(("effects", f"on {sid}: {ex}"))
            return
    used = [c for c in START_CONDITIONS if any(e.get("when") == c for e in frame_effects)]
    subsets = [[c for j, c in enumerate(used) if mask >> j & 1] for mask in range(1 << len(used))]
    seen: set[str] = set()
    for sid in sorted(specials):
        special = specials[sid]
        if not all(has_property(special, r) for r in requires):
            continue
        try:
            builds = list(special_builds(special))
        except Unbuildable:
            continue  # the special's own problem, reported on its own file
        for label, base in builds:
            for held in subsets:
                try:
                    fd = copy.deepcopy(base)
                    for e in frame_effects:
                        if e.get("when", "always") == "always" or e.get("when") in held:
                            apply_effect(fd, e, defaults=True)
                    found = [("", m) for m in built_move_errors(fd)]
                    for outcome in ("on_hit", "on_block"):
                        if any(e.get("when") == outcome for e in frame_effects):
                            o = copy.deepcopy(fd)
                            for e in frame_effects:
                                if e.get("when") == outcome:
                                    apply_effect(o, e, defaults=True)
                            found += [(f" {outcome}", m) for m in built_move_errors(o)]
                except Unbuildable as e:
                    found = [("", str(e))]
                for where, msg in found:
                    line = f"on {sid} {label}{where}{' when ' + '+'.join(held) if held else ''}: {msg}"
                    if msg not in seen:  # one line per distinct problem is enough to fix it
                        seen.add(msg)
                        errs.append(("effects", line))


def load_specials(docs: dict) -> dict[str, dict]:
    """Every special the game's pool can hold: the SPECIAL_DIRS folders plus specials in this run."""
    found: dict[str, dict] = {}
    for d in SPECIAL_DIRS:
        for f in sorted(d.glob("*.json")) if d.is_dir() else []:
            try:
                doc = json.loads(f.read_text(encoding="utf-8"))
            except (OSError, json.JSONDecodeError):
                continue
            if isinstance(doc, dict) and doc.get("kind") == "special":
                found[doc.get("id", f.stem)] = doc
    for path, doc in docs.items():
        if SAMPLES_DIR in path.resolve().parents:
            continue  # samples are not in the game's pool
        if isinstance(doc, dict) and doc.get("kind") == "special" and isinstance(doc.get("id"), str):
            found[doc["id"]] = doc
    return found


def check_trial(doc: dict, move_kinds: dict[str, str] | None = None) -> list[tuple[str, str]]:
    """YOK-105 (R7): the checks of the game's TrialLoader the schema cannot express. Kihon-completable steps,
    one step lands the trial's own special, distance min <= max, block / counterhit steps only against an
    attacking dummy, `every` no longer than the attempt, and (for moves found in data/moves/ or this run)
    throws only in throw steps."""
    errs: list[tuple[str, str]] = []
    goal = doc.get("goal")
    steps = goal.get("steps") if isinstance(goal, dict) else None
    steps = steps if isinstance(steps, list) else []
    setup = doc.get("setup") if isinstance(doc.get("setup"), dict) else {}
    dummy = setup.get("dummy") if isinstance(setup.get("dummy"), dict) else {}
    attacks = dummy.get("action") == "attack"
    every, frames = whole(dummy.get("every")), whole(setup.get("attempt_frames"))
    if every is not None and frames is not None and every > frames:
        errs.append(("setup.dummy.every", f"{every} is longer than the attempt ({frames} ticks)"))
    for i, st in enumerate(steps):
        if not isinstance(st, dict):
            continue
        for key in TRIAL_KATA_ONLY:
            if key in st:
                errs.append((f"goal.steps.{i}.{key}", f"'{key}' needs a Kata input; trials must be Kihon-completable (R7, K2)"))
        d = st.get("distance")
        if isinstance(d, dict):
            lo, hi = whole(d.get("min")), whole(d.get("max"))
            if lo is not None and hi is not None and lo > hi:
                errs.append((f"goal.steps.{i}.distance", f"min {lo} is greater than max {hi}"))
        if not attacks:
            if st.get("event") == "block":
                errs.append((f"goal.steps.{i}.event", "block needs a dummy with action 'attack' (nothing to block otherwise)"))
            if st.get("counter") is True:
                errs.append((f"goal.steps.{i}.counter", "a counterhit needs a dummy with action 'attack' (C7: the dummy must be in a move)"))
        mk = (move_kinds or {}).get(st.get("move")) if isinstance(st.get("move"), str) else None
        if mk == "throw" and st.get("event") != "throw":
            errs.append((f"goal.steps.{i}.move", f"'{st['move']}' is a throw; throws only meet a throw step"))
        elif mk is not None and mk != "throw" and st.get("event") == "throw":
            errs.append((f"goal.steps.{i}.move", f"'{st['move']}' is not a throw"))
    special = doc.get("special")
    if isinstance(special, str) and steps and not any(isinstance(st, dict) and st.get("move") == special for st in steps):
        errs.append(("goal.steps", f"no step names the trial's special '{special}' as its move (R7: the trial is for that special)"))
    return errs


def semantic(doc: dict, path: Path, rule_ids: set[str], clips: dict[str, dict],
             specials: dict[str, dict] | None = None, schema_ok: bool = True,
             move_kinds: dict[str, str] | None = None) -> list[tuple[str, str]]:
    errs: list[tuple[str, str]] = []
    kind = doc.get("kind")
    if doc.get("id") != path.stem:
        errs.append(("id", f"id '{doc.get('id')}' must equal the file name '{path.stem}'"))
    parts = path.resolve().parts
    if "data" in parts:
        idx = len(parts) - 1 - parts[::-1].index("data")
        folder = parts[idx + 1] if idx + 1 < len(parts) - 1 else None
        if folder in DIR_KIND and kind not in DIR_KIND[folder]:
            allowed = " or ".join(f"'{k}'" for k in DIR_KIND[folder])
            errs.append(("kind", f"files in data/{folder}/ must be kind {allowed}, got '{kind}'"))
    for i, rid in enumerate(doc.get("rules") or []):
        if isinstance(rid, str) and rid not in rule_ids:
            errs.append((f"rules.{i}", f"rule ID '{rid}' does not exist in docs/design/rules.md"))

    if kind in ("special", "normal", "throw"):
        clip_id = doc.get("clip")
        clip = clips.get(clip_id) if isinstance(clip_id, str) else None
        if isinstance(clip_id, str) and clip is None:
            errs.append(("clip", f"clip '{clip_id}' not found (expected data/clips/{clip_id}.json) (F2)"))
    if kind in ("normal", "throw") and schema_ok and isinstance(doc.get("frame_data"), dict):
        for msg in built_move_errors(copy.deepcopy(doc["frame_data"])):  # YOK-103: what MoveLoader refuses
            errs.append(("frame_data", msg))
    if kind == "normal":
        check_frame_data(doc.get("frame_data"), "frame_data", clip, clip_id, errs)
        if "landing_recovery" in doc and doc.get("air") is not True:  # E11: only jump-ins land mid-move
            errs.append(("landing_recovery", "landing_recovery is only for air normals (set \"air\": true) (E11)"))
        if "buttons" in (doc.get("input") or {}) and doc.get("air") is not True:  # E19: several buttons only mid-jump
            errs.append(("input.buttons", "input.buttons is only for air normals (set \"air\": true); ground normals use input.button (E19)"))
    elif kind == "throw":  # C4: throwboxes inside the active window, clip timing as for strikes (F2)
        fd = doc.get("frame_data")
        check_frame_data(fd, "frame_data", clip, clip_id, errs)
        if isinstance(fd, dict) and is_int(fd.get("startup")) and is_int(fd.get("active")):
            s = fd["startup"]
            check_ranges(fd.get("throwboxes"), s + 1, s + fd["active"], "frame_data.throwboxes", "throwbox", errs)
    elif kind == "special":
        if doc.get("id") == "copied":  # YOK-94: profiles' special:copied names the Tanuki's copy (T0)
            errs.append(("id", "'copied' is reserved: profiles use special:copied for the Tanuki's copied special (T0)"))
        inp = doc.get("input") or {}
        ks, hs = (inp.get("kata") or {}).get("slot"), (inp.get("kihon") or {}).get("slot")
        if ks and hs and ks != hs:
            errs.append(("input", f"Kata slot {ks} and Kihon slot {hs} must match (K3)"))
        if doc.get("starter") and (doc.get("source") != "ryo" or doc.get("rarity") != "common"):
            errs.append(("starter", "starters must have source 'ryo' and rarity 'common' (A1, A9)"))
        lv1 = ((doc.get("levels") or {}).get("1") or {}).get("frame_data")
        check_frame_data(lv1, "levels.1.frame_data", clip, clip_id, errs)
        if schema_ok:  # YOK-103: every level and EX version must still be a move the loader runs
            check_special_builds(doc, errs)
    elif kind == "modifier":
        if schema_ok:  # YOK-103: what the C# loader does with it (see the mirror above)
            check_modifier(doc, specials or {}, errs)
    elif kind == "cancel_rule":
        ob, src = doc.get("offered_by"), doc.get("source")
        if ob in ELDER_SOURCE and src and ELDER_SOURCE[ob] != src:
            errs.append(("offered_by", f"'{ob}' is a {ELDER_SOURCE[ob]} elder but source is '{src}' (A8)"))
    elif kind == "profile":
        tanuki = doc.get("yokai") == "tanuki"
        if tanuki != (doc.get("temperament") == "boss"):
            errs.append(("temperament", "'boss' temperament is for the Tanuki only, and the Tanuki must use it (Y1)"))
        tier = (doc.get("reaction") or {}).get("tier")
        if tier and tanuki != (tier == "tanuki"):
            errs.append(("reaction.tier", "the 'tanuki' reaction tier is for the Tanuki only, and the Tanuki must use it (Y4)"))
    elif kind == "story_card":  # S4: the game fills exactly the listed run names
        listed = {x for x in doc.get("placeholders") or [] if isinstance(x, str)}
        used: set[str] = set()
        for key in ("title", "text"):
            if isinstance(doc.get(key), str):
                used |= set(re.findall(r"\{([^{}]*)\}", doc[key]))
        for name in sorted(used - listed):
            errs.append(("placeholders", f"{{{name}}} is used but not listed in placeholders (S4)"))
        for name in sorted(listed - used):
            errs.append(("placeholders", f"'{name}' is listed but {{{name}}} is not used in title or text"))
    elif kind == "elder_twist":  # YOK-90 (Y3): the intro card and callout are story cards
        for key in ("intro_card", "callout_card"):
            card = doc.get(key)
            if isinstance(card, str) and not (DATA / "story" / f"{card}.json").is_file():
                errs.append((key, f"story card '{card}' not found (expected data/story/{card}.json) (Y3)"))
    elif kind == "run_map":
        errs.extend(check_run_map(doc))
    elif kind == "node_scenes":
        errs.extend(check_node_scenes(doc, path))
    elif kind == "trial":
        errs.extend(check_trial(doc, move_kinds))
    elif kind == "clip":
        hs, he, ft = doc.get("hit_start"), doc.get("hit_end"), doc.get("frames_total")
        if (hs is None) != (he is None):
            errs.append(("hit_end", "hit_start and hit_end must both be set or both be null"))
        elif is_int(hs) and is_int(he) and is_int(ft) and not hs <= he <= ft:
            errs.append(("hit_end", f"need hit_start <= hit_end <= frames_total, got {hs}/{he}/{ft}"))
        # YOK-53: structured trim/speed/file (ClipCatalog reads these before the notes).
        ts, te, sp = doc.get("trim_start"), doc.get("trim_end"), doc.get("speed_scale", 1)
        if (ts is None) != (te is None):
            errs.append(("trim_end", "trim_start and trim_end must both be set or both be absent"))
        elif is_int(ts) and is_int(te):
            if ts > te:
                errs.append(("trim_end", f"need trim_start <= trim_end, got {ts}-{te}"))
            elif "retime" in doc:
                pass  # YOK-113: the curve, not speed_scale, sets frames_total (checked below)
            elif isinstance(sp, (int, float)) and not isinstance(sp, bool) and sp > 0 and is_int(ft):
                frames = int((te - ts) / sp + 1e-9) + 1
                if abs(frames - ft) > 1:
                    errs.append(("frames_total", f"trim {ts}-{te} at {sp}x gives {frames} frames but frames_total is {ft} (F2)"))
        f = doc.get("file")
        if isinstance(f, str) and f.endswith(".glb") and not (ROOT / "game" / f).is_file():
            errs.append(("file", f"game/{f} does not exist"))
        if "retime" in doc:
            errs.extend(check_retime(doc.get("retime"), ft, ts if is_int(ts) and is_int(te) else None, te))
    return errs


# YOK-113 (F2, AM5) first-pass retiming limits; the designer may change them. Keep in step with ClipInfo (C#).
RETIME_MIN_SPEED = (1, 2)   # 0.5x as a ratio, so the check is exact integer math
RETIME_MAX_SPEED = (2, 1)   # 2x
RETIME_MAX_HOLD_FRAMES = 4  # game frames one held tick may show on


def check_retime(segs, frames_total, trim_start=None, trim_end=None) -> list[tuple[str, str]]:
    """Retiming curve: contiguous segments from frame 1 to frames_total, each 0.5x-2x or a hold of <= 4 frames."""
    errs: list[tuple[str, str]] = []
    if not isinstance(segs, list) or not segs:
        return [("retime", "retime must be a non-empty list of segments")]
    parsed = []
    for i, s in enumerate(segs):
        fr, tk = (s.get("frames"), s.get("ticks")) if isinstance(s, dict) else (None, None)
        if not (isinstance(fr, list) and isinstance(tk, list) and len(fr) == 2 and len(tk) == 2
                and all(is_int(x) for x in fr + tk)):
            errs.append((f"retime.{i}", "segment needs frames [a, b] and ticks [ta, tb] as integers"))
            return errs
        parsed.append((i, fr[0], fr[1], tk[0], tk[1]))
    if is_int(frames_total):
        if parsed[0][1] != 1:
            errs.append(("retime.0.frames", f"curve must start on game frame 1, starts on {parsed[0][1]}"))
        if parsed[-1][2] != frames_total:
            errs.append((f"retime.{len(parsed) - 1}.frames",
                         f"curve must end on frames_total {frames_total}, ends on {parsed[-1][2]} (F2)"))
    prev = None
    hold_from = None  # first frame of the current run of held segments
    for i, a, b, ta, tb in parsed:
        where = f"retime.{i}"
        if b <= a:
            errs.append((f"{where}.frames", f"need last frame > first frame, got {a}-{b}"))
            prev, hold_from = (b, tb), None
            continue
        if prev is not None:
            if a != prev[0]:
                errs.append((f"{where}.frames", f"segments must be contiguous: starts on frame {a}, previous ends on {prev[0]}"))
            if ta != prev[1]:
                errs.append((f"{where}.ticks", f"segments must be contiguous: starts on tick {ta}, previous ends on {prev[1]}"))
        df, dt = b - a, tb - ta
        if dt == 0:
            hold_from = a if hold_from is None else hold_from
            shown = b - hold_from + 1
            if shown > RETIME_MAX_HOLD_FRAMES:
                errs.append((f"{where}.frames", f"held tick {ta} shows on {shown} frames ({hold_from}-{b}); a hold lasts at most {RETIME_MAX_HOLD_FRAMES} (F2)"))
        else:
            hold_from = None
            lo_n, lo_d = RETIME_MIN_SPEED
            hi_n, hi_d = RETIME_MAX_SPEED
            if dt * lo_d < lo_n * df or dt * hi_d > hi_n * df:
                errs.append((f"{where}.ticks", f"ticks {ta}-{tb} over frames {a}-{b} play at {dt / df:g}x; a segment plays between 0.5x and 2x (F2)"))
        if trim_start is not None and is_int(trim_end) and not (trim_start <= min(ta, tb) and max(ta, tb) <= trim_end):
            errs.append((f"{where}.ticks", f"ticks {ta}-{tb} fall outside trim {trim_start}-{trim_end}"))
        prev = (b, tb)
    return errs


def validate(paths: list[str], out=sys.stdout) -> int:
    validators = load_validators()
    rule_ids = load_rule_ids()
    files = collect(paths)
    docs: dict[Path, object] = {}
    failures: dict[Path, list[tuple[str, str]]] = {}
    for f in files:
        try:
            docs[f] = json.loads(f.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError) as e:
            failures[f] = [("(file)", f"cannot read JSON: {e}")]
    # Clips referenced by specials and normals: everything in data/clips/ plus clips in this run.
    clips: dict[str, dict] = {}
    for c in sorted((DATA / "clips").glob("*.json")) if (DATA / "clips").is_dir() else []:
        try:
            d = json.loads(c.read_text(encoding="utf-8"))
            clips[d.get("id", c.stem)] = d
        except (OSError, json.JSONDecodeError, AttributeError):
            pass
    for d in docs.values():
        if isinstance(d, dict) and d.get("kind") == "clip" and isinstance(d.get("id"), str):
            clips[d["id"]] = d

    specials = load_specials(docs)  # YOK-103: modifiers are built on these, as the game's pool does

    # Move kinds a trial's steps are checked against: data/moves/ plus moves in this run.
    move_kinds: dict[str, str] = {}
    move_docs = []
    for mf in sorted((DATA / "moves").glob("*.json")) if (DATA / "moves").is_dir() else []:
        try:
            move_docs.append(json.loads(mf.read_text(encoding="utf-8")))
        except (OSError, json.JSONDecodeError):
            pass
    for d in move_docs + list(docs.values()):
        if isinstance(d, dict) and d.get("kind") in ("special", "normal", "throw") and isinstance(d.get("id"), str):
            move_kinds[d["id"]] = d["kind"]

    for f, doc in docs.items():
        errs: list[tuple[str, str]] = []
        if not isinstance(doc, dict):
            errs.append(("(root)", "top level must be a JSON object"))
        elif doc.get("kind") not in validators:
            errs.append(("kind", f"unknown kind {doc.get('kind')!r}; expected one of {sorted(validators)}"))
        else:
            v = validators[doc["kind"]]
            for e in sorted(v.iter_errors(doc), key=lambda e: list(map(str, e.absolute_path))):
                errs.append((field(e.absolute_path), e.message))
            # data/samples/ is documentation the game never loads: no loader mirror there (YOK-103).
            game_loads = SAMPLES_DIR not in f.resolve().parents
            errs.extend(semantic(doc, f, rule_ids, clips, specials, schema_ok=not errs and game_loads, move_kinds=move_kinds))
        if errs:
            failures[f] = errs

    # YOK-90 (Y2): one twist per elder across the files checked together.
    twist_of: dict[str, Path] = {}
    for f, doc in docs.items():
        if isinstance(doc, dict) and doc.get("kind") == "elder_twist" and isinstance(doc.get("elder"), str):
            first = twist_of.setdefault(doc["elder"], f)
            if first != f:
                failures.setdefault(f, []).append(("elder", f"'{doc['elder']}' already has a twist in {rel(first)} (Y2: one per elder)"))

    # YOK-153 (A11, M1): an unlock is saved by ability id alone, so one id must name one ability across the kinds
    # checked together: specials, their evolutions (A5), modifiers and cancel rules. data/samples/ is documentation
    # and may reuse real ids.
    ability_of: dict[str, tuple[Path, str]] = {}
    for f, doc in docs.items():
        if not isinstance(doc, dict) or SAMPLES_DIR in f.resolve().parents:
            continue
        kind = doc.get("kind")
        named: list[tuple[str, str, object]] = []
        if kind in ("special", "modifier", "cancel_rule"):
            named.append(("id", kind, doc.get("id")))
        if kind == "special":
            evo = ((doc.get("levels") or {}).get("3") or {}).get("evolution") if isinstance(doc.get("levels"), dict) else None
            if isinstance(evo, dict):
                named.append(("levels.3.evolution.id", "evolution", evo.get("id")))
        for where, what, aid in named:
            if not isinstance(aid, str) or not aid:
                continue
            first = ability_of.setdefault(aid, (f, what))
            if first != (f, what):
                failures.setdefault(f, []).append(
                    (where, f"ability id '{aid}' is already the {first[1]} in {rel(first[0])} (A11: one id per ability, so an unlock names one)"))

    proposed = sum(1 for d in docs.values() if isinstance(d, dict) and '"proposed"' in json.dumps(d))
    for f in files:
        for where, msg in failures.get(f, []):
            print(f"{rel(f)}: {where}: {msg}", file=out)
    bad = len(failures)
    print(f"validate_data: {len(files)} file(s), {len(files) - bad} valid, {bad} invalid"
          f" ({proposed} with 'proposed' values awaiting the designer)", file=out)
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(validate(sys.argv[1:]))
