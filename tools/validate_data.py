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
}
# Folder under data/ -> the kinds its files may declare. Other folders (samples/) accept any kind.
DIR_KIND = {
    "moves": ("special", "normal", "throw"),
    "modifiers": ("modifier",),
    "cancels": ("cancel_rule",),
    "profiles": ("profile",),
    "clips": ("clip",),
}
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


def semantic(doc: dict, path: Path, rule_ids: set[str], clips: dict[str, dict]) -> list[tuple[str, str]]:
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
    elif kind == "throw":  # C4: throwboxes inside the active window, clip timing as for strikes (F2)
        fd = doc.get("frame_data")
        check_frame_data(fd, "frame_data", clip, clip_id, errs)
        if isinstance(fd, dict) and is_int(fd.get("startup")) and is_int(fd.get("active")):
            s = fd["startup"]
            check_ranges(fd.get("throwboxes"), s + 1, s + fd["active"], "frame_data.throwboxes", "throwbox", errs)
    elif kind == "special":
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
    elif kind == "clip":
        hs, he, ft = doc.get("hit_start"), doc.get("hit_end"), doc.get("frames_total")
        if (hs is None) != (he is None):
            errs.append(("hit_end", "hit_start and hit_end must both be set or both be null"))
        elif is_int(hs) and is_int(he) and is_int(ft) and not hs <= he <= ft:
            errs.append(("hit_end", f"need hit_start <= hit_end <= frames_total, got {hs}/{he}/{ft}"))
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
            errs.extend(semantic(doc, f, rule_ids, clips))
        if errs:
            failures[f] = errs

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
