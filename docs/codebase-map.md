# Codebase map

Maintained by `gameplay-programmer` (and `ui-designer` for UI scenes). Agents read this instead of crawling the project, then pull only the files their ticket touches. Update it whenever a system is added or moved.

## Godot project (`game/`)

Godot 4.7 .NET, C# (`YokaiFighters.csproj`, net10.0). Boot: `scenes/main.tscn` (`scripts/Main.cs`) changes to `scenes/fight.tscn`. Its C# loader will adopt the schemas below (YOK-40).

### Simulation core (`game/scripts/Sim/`, namespace `YokaiFighters.Sim`, YOK-15)
Pure C#, **no Godot types**, integers only, so it runs headless for tests and the harness bot and replays exactly from per-tick inputs.

| File | Purpose |
|---|---|
| `SimConfig.cs` | Fight constants (record with defaults; a data loader fills it later). Positions in **centi-units** (`Scale` = 100 per gameplay unit). Stage half-width, view width, body (push-box) width, max separation (screen walls), walk speed (C2), max health (C1), KO slow-down (V4). |
| `FighterInput.cs` | `InputBits` flags + `FighterInput`: one fighter's **raw** input for one tick — absolute Left/Right/Up/Down, six attack buttons (LP MP HP LK MK HK) and Kihon's `Special` (K1/K2). Device mapping, the AI (P6) and replays all produce these; the input layer below turns them into commands. `Debug*` bits are placeholders until moves land. |
| `Fighter.cs` | `Fighter` state (X/Y on the 2D plane, Facing ±1, health, KO, previous input for edge detection, queued damage); `Fnv` state hashing; `SimRng` seeded xorshift32 (the only RNG gameplay may use). |
| `Match.cs` | One round. `Step(in1, in2)` = exactly one 1/60 s tick. Phases `Fighting → KoSlowMo → Over`. World step order: inputs/movement → `ResolvePositions` (screen walls, push boxes, stage corners) → `UpdateFacing` (always face each other) → `ApplyDamage` (simultaneous; double KO = draw). `QueueDamage()` is the hook for hits (YOK-16). KO: inputs ignored, world advances every 2nd tick for 30 world frames (V4), then `Over` until `Reset()`. `StateHash()` for determinism checks. Events `KnockOut`, `RoundOver`. |
| `FixedStepClock.cs` | Integer-microsecond fixed-step clock: `Advance(nowUsec)` returns the ticks due so exactly 60 ticks run per second at any render rate; caps catch-up at 8 and drops the rest on a hitch. |
| `FightCamera.cs` | Camera centre X = fighters' midpoint clamped to the stage; `BothInView()` for tests. |

### Input layer (`game/scripts/Sim/Input/`, namespace `YokaiFighters.Sim`, YOK-17)
Pure C#, deterministic, shared by Kata and Kihon (K3): only the `ICommandParser` differs. Per fighter: `InputReader.Update(raw, facing)` once per tick, then the move system calls `TryConsume(out InputCommand)`.

| File | Purpose |
|---|---|
| `InputConfig.cs` | Timing in ticks (all **proposed**, GDD gives none): `MotionWindow` 16 (first direction → press), `CommandBuffer` 5 (command waits to be consumed), `ChargeTicks` 40, `ChargeGrace` 8, `HistoryTicks` 128; `MotionPriority` tie-break (623 > 236 > 214 > 22); `ButtonPriority` (heavy > medium > light, punch > kick). |
| `Numpad.cs` | Absolute bits ↔ numpad direction relative to facing (6 = toward opponent). SOCD: left+right and up+down cancel. |
| `InputBuffer.cs` | Ring buffer of raw `InputBits` (age 0 = now): `Dir(age, facing)`, `PressedAt(age)` (button edges), `HeldTicks(bits)` (hold-to-charge buttons). |
| `MotionParser.cs` | `Motion` enum (236, 623, 214, 22 for K1 slots A–D, plus `[4]6` / `[2]8` charge motions not in any slot) and `Matches` / `FinalStepAge`: backwards greedy subsequence match inside the window, directions read with the facing at the press (motions flip when sides switch). Lenience: 623 accepts 1 for its down, 22 any down/non-down. |
| `InputCommand.cs` | `InputCommand` (Kind None/Normal/Special, `SpecialSlot` A–D, Motion, chosen Button, full Pressed mask for two-button throws, Direction, `Precision` = Kata motion +10%, K1) and `ICommandParser`. |
| `KataParser.cs` | Kata (K1): on an attack-button press, the most recently completed K1 motion in the window → Special in its slot (ties by `MotionPriority`), else a Normal with its direction. Ignores `Special`. Kihon (YOK-23) adds a sibling parser: `Special` + direction → slot. |
| `InputReader.cs` | Per-fighter owner of buffer + parser + command buffer. A normal never replaces a waiting special; a newer special does. Not yet wired into `Match`/`FightScene` (move system, YOK-16). |

### Presentation (`game/scripts/Fight/`, namespace `YokaiFighters.Fight`)
| File | Purpose |
|---|---|
| `FightScene.cs` (`scenes/fight.tscn`) | Owns `Match` + `FixedStepClock`; each `_Process` runs the due ticks then `Render()`s. Builds the placeholder stage (floor, backdrop, corner posts), capsule fighters (mirrored by `Scale.X = Facing`, "Nose" marks facing; KO'd fighter tips over across the 30 slow frames), Camera3D at FOV 25° (F4), 200 units per metre, fixed Z = 0. `ExternalDrive` + `Step()` let tests/harness drive it without the clock or keyboard. Debug keys: P1 A/D, F strike, G cross-up; P2 ←/→, L, K; R resets after KO. |
| `InputDevices.cs` | Minimal device → `FighterInput` mapping (K2 keyboard and pad; rebinding/scheme select is YOK-25). P1 keys WASD + U/I/O punches, J/K/L kicks, Space Special; P2 arrows + numpad 4/5/6, 1/2/3, 0. Pad: d-pad/left stick (0.5 deadzone), X/Y/RB punches, A/B/RT kicks, LB Special. `Read(player)` ORs keyboard and pad `player`. Not yet wired into `FightScene` (still on debug keys). |
| `FightHud.cs` | Placeholder `CanvasLayer`: `P1Health`/`P2Health` bars and the `Banner` label (K.O., winner). ui-designer replaces it; reads `Match` only. |

### Tests (`game/tests/`)
Headless runner: `tests/test_runner.tscn` (`TestRunner.cs`) runs every `[Test]` public static method in the assembly (optionally taking the runner `Node`), prints `PASS/FAIL`, exits 0/1. Add tests as new static classes; no NuGet needed.
```
cd game
dotnet build
<godot-console-exe> --headless --path . --import          # first run only
<godot-console-exe> --headless --path . res://tests/test_runner.tscn
```
`FightLoopTests.cs` (YOK-15): clock at 24–1000 Hz and jittered frames, hitch, facing under 5,000 random ticks (plus bounds, push boxes, camera), side switch, corner, screen walls, walk speed, KO slow-down and freeze, reset, double KO, determinism by hash, scene smoke test through KO and reset.
`InputParserTests.cs` (YOK-17): recorded numpad sequences (`"5 2 3 6+LP"`, `*N` repeats) for every K1 motion in both facings, window edges, side-switch flips, button and motion priority, lenience, charge, hold, command buffer expiry/consume, SOCD, determinism, device mapping.

## Content data (`data/`) and schemas (`data/schema/`, YOK-12)

JSON Schema draft 2020-12. Every content file is one JSON object with a top-level `kind` that picks its schema.

| `kind` | Schema | Folder | Covers |
|---|---|---|---|
| `special` | `special.schema.json` | `data/moves/` | Source, starter, rarity, locked, `clip`, `rules`, Kata + Kihon input, card, optional EX, levels 1–3 |
| (block) | `frame-data.schema.json` | inside a special's `levels.1.frame_data` | Startup/active/recovery, damage, hitstun/blockstun, meter gain, hitboxes, hurtboxes, cancel windows, properties |
| `modifier` | `modifier.schema.json` | `data/modifiers/` | Source, always common, locked, `applies_to`, effects, card |
| `cancel_rule` | `cancel-rule.schema.json` | `data/cancels/` | Source, offering elder, always rare, path from/into/on, `once_per_combo: true` |
| `profile` | `profile.schema.json` | `data/profiles/` | Yokai, temperament, reaction tier + frames, aggression bias, preferred range, the one habit, weighted behaviours, elder twist |
| `clip` | `clip.schema.json` | `data/clips/` | Source, clip, frames_total, hit_start, hit_end, retarget_notes |
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
