# Codebase map

Maintained by `gameplay-programmer` (and `ui-designer` for UI scenes). Agents read this instead of crawling the project, then pull only the files their ticket touches. Update it whenever a system is added or moved.

_No Godot code yet. The Godot project will live in `game/`; its C# loader adopts the schemas below (YOK-40)._

## Content data (`data/`) and schemas (`data/schema/`, YOK-12)

JSON Schema draft 2020-12. Every content file is one JSON object with a top-level `kind` that picks its schema.

| `kind` | Schema | Folder | Covers |
|---|---|---|---|
| `special` | `special.schema.json` | `data/moves/` | Source, starter, rarity, locked, `clip`, `rules`, Kata + Kihon input, card, optional EX, levels 1–3 |
| (block) | `frame-data.schema.json` | inside a special's `levels.1.frame_data` | Startup/active/recovery, damage, hitstun/blockstun, meter gain, hitboxes, hurtboxes, cancel windows, properties |
| `modifier` | `modifier.schema.json` | `data/modifiers/` | Source, always common, locked, `applies_to`, effects, card |
| `cancel_rule` | `cancel-rule.schema.json` | `data/cancels/` | Source, offering elder, always rare, path from/into/on, `once_per_combo: true` |
| `profile` | `profile.schema.json` | `data/profiles/` | Yokai, temperament, reaction tier + frames, aggression bias, preferred range, the one habit, weighted behaviours, elder twist |
| `clip` | `clip.schema.json` | `data/clips/` | Pack, clip, frames_total, hit_start, hit_end, retarget_notes |
| — | `common.schema.json` | — | Shared `$defs`: id, yokai, rarity, status, ruleIds, card, frameRange, rect, timedRect, condition, effect |

Not covered yet (out of YOK-12 scope): map, economy, story, presets, sound, trials.

### Conventions
- **ids** are lowercase kebab-case and equal the file name: `data/moves/spirit-wave.json` has `"id": "spirit-wave"`. References (`clip`, `preset`) are ids, resolved as `data/clips/<id>.json` and `data/presets/<id>.json`.
- **Integers only** for gameplay numbers (determinism, F3): frames, units, percent. `mul_pct: 130` means ×1.3.
- **Frames** are 1-based from the move's first frame. Active frames are `startup+1 .. startup+active`. `"active": null` is the rules.md "—" (projectile carries the hit; needs `properties.projectile`, no body hitboxes).
- **Rects** are in gameplay units (1 unit = 1 px at 1920×1080) relative to the character's feet, x toward the opponent, y up.
- **Effects** (`{target, op, value, when?}`) are the single data-only change vocabulary for Lv 2 tuning, Lv 3 evolutions, EX versions and modifiers. `target` is a dotted path into frame data (`recovery`, `projectile.speed`, `properties.armour`); `op` is `add | mul_pct | set`; `when` is an on-screen/game-state condition, never a button.
- **Inputs**: each special stores `input.kata` (`{slot, motion}`) and `input.kihon` (`{slot, direction}`); allowed pairs are fixed by K1/K2 and both slots must match.
- **Levels**: `levels.1.frame_data` is the full Lv 1 data; `levels.2` is exactly one `tuning` effect plus a required `status`; `levels.3.evolution` needs a `preset`.
- **`status: "proposed"`** marks values awaiting the designer (e.g. every Lv 2 step, an open question). Allowed on any file and on Lv 2 / evolutions; the validator counts them.
- **`rules`** lists rules.md IDs; the validator rejects IDs that don't exist.
- Profiles' `when` values come from a fixed list of on-screen observations (P6); a button-press condition fails the schema.

### Validator (`tools/validate_data.py`)
```
pip install -r tools/requirements.txt        # jsonschema==4.26.0 (already in the dev container)
python3 tools/validate_data.py               # all of data/, skipping data/schema/ and any invalid/ folder
python3 tools/validate_data.py data/moves data/clips/foo.json
python3 -m unittest discover -s tools -p 'test_*.py'
```
Exit 0 = valid, 1 = errors, 2 = missing dependency. Errors print as `<file>: <field.path>: <message>`.
Beyond the schema it checks: id = file name; files in `data/<folder>/` have that folder's kind; rule IDs exist; a special's clip exists and matches its frame data (total = frames_total, hit_start = startup+1, hit_end = last active frame; F2); hitboxes inside the active window; hurtbox/cancel ranges inside the move; Kata/Kihon slots match; starters are Ryo + common; cancel `offered_by` elder matches `source`; `boss` temperament and `tanuki` tier only for the Tanuki; clip hit_start ≤ hit_end ≤ frames_total.

### Samples
`data/samples/` holds one valid file per kind (Spirit Wave + its clip, Will-o'-wisp, Fox Step, Kitsune patient). `data/samples/invalid/` holds deliberately malformed files; the default run skips them and `tools/test_validate_data.py` asserts they fail with the expected messages.
