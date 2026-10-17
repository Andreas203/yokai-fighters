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
rules.md consistency checks.

Requires the `jsonschema` package (see tools/requirements.txt).
"""
from __future__ import annotations

import json
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
    "run_map": "run-map.schema.json",
    "node_scenes": "node-scenes.schema.json",
    "elder_twist": "elder-twist.schema.json",
    "trial": "trial.schema.json",
}
# Folder under data/ -> the kinds its files may declare. Other folders (samples/) accept any kind.
DIR_KIND = {
    "moves": ("special", "normal", "throw"),
    "modifiers": ("modifier",),
    "cancels": ("cancel_rule",),
    "profiles": ("profile",),
    "clips": ("clip",),
    "story": ("story_card",),
    "map": ("run_map", "node_scenes"),
    "twists": ("elder_twist",),
    "trials": ("trial",),
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
        if not (isinstance(fr, list) and len(fr) == 2 and all(is_int(x) for x in fr)):
            continue
        a, b = fr
        if a > b:
            errs.append((f"{where}.{i}.frames", f"first frame {a} is after last frame {b}"))
        elif a < lo or b > hi:
            errs.append((f"{where}.{i}.frames", f"{what} frames {a}-{b} fall outside {lo}-{hi}"))


def check_frame_data(fd, where, clip, clip_name, errs):
    if not isinstance(fd, dict):
        return
    s, a, r = fd.get("startup"), fd.get("active"), fd.get("recovery")
    if not (is_int(s) and is_int(r) and (a is None or is_int(a))):
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
    if isinstance(proj, dict) and is_int(proj.get("spawn_frame")) and not 1 <= proj["spawn_frame"] <= total:
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


def whole(v):
    """A JSON Schema integer as a number: 3, 3.0 and 3e0 are all 3 (as the game's TrialLoader reads them)."""
    if isinstance(v, bool) or not isinstance(v, (int, float)):
        return None
    if isinstance(v, float) and (v != v or v in (float("inf"), float("-inf")) or v != int(v)):
        return None
    return int(v)


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
            errs.extend(semantic(doc, f, rule_ids, clips, move_kinds))
        if errs:
            failures[f] = errs

    # YOK-90 (Y2): one twist per elder across the files checked together.
    twist_of: dict[str, Path] = {}
    for f, doc in docs.items():
        if isinstance(doc, dict) and doc.get("kind") == "elder_twist" and isinstance(doc.get("elder"), str):
            first = twist_of.setdefault(doc["elder"], f)
            if first != f:
                failures.setdefault(f, []).append(("elder", f"'{doc['elder']}' already has a twist in {rel(first)} (Y2: one per elder)"))

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
