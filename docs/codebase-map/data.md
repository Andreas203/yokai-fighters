# Data: content files and schemas

`data/` (content) and `data/schema/` (JSON Schema draft 2020-12). Every content file is one JSON object whose top-level `kind` picks its schema; the C# loader for each kind is named below. Checked by `tools/validate_data.py` and `tools/check_rules.py` ([tools.md](tools.md)). Index: [README.md](README.md).

| `kind` | Schema | Folder | Covers, and who reads it |
|---|---|---|---|
| `normal` | `normal.schema.json` | `data/moves/` | Source, `clip`, `rules`, `input.button` (or `input.buttons` for an air normal, E19), optional `input.directions`, top-level `frame_data` (C8); optional `air` (E11) and `landing_recovery` (needs `air`). `MoveLoader`. |
| `throw` | `throw.schema.json` | `data/moves/` | C4: grab `clip`, `input.buttons` (two different), `frame_data`, `break_window`, `pushback.on_hit`/`on_break`, `throwboxes` inside the active window. `MoveLoader`. |
| `special` | `special.schema.json` | `data/moves/` | Source, starter, rarity, locked, `clip`, Kata + Kihon input, card, optional EX, levels 1–3 (`levels.1.frame_data` full; `levels.2` one `tuning` effect + `status`; `levels.3.evolution` needs a `preset`). `SpecialLoader`. |
| (block) | `frame-data.schema.json` | inside moves | Startup/active/recovery, damage, hitstun/blockstun, pushback, `strength` (V2), `meter_gain`, hitboxes, hurtboxes, cancel windows, `properties` (`projectile.max_active`, `aftershock_delay`, modifier properties). |
| `modifier` | `modifier.schema.json` | `data/modifiers/` | Source, always common, locked, `applies_to`, effects (`modifierEffect`: targets and `when` limited to what the loader reads), card. `ModifierLoader`. |
| `cancel_rule` | `cancel-rule.schema.json` | `data/cancels/` | Source, offering elder, always rare, path from/into/on, `once_per_combo: true` (X3). `CancelRuleLoader` (`ParseData` for the elder draft, YOK-186). |
| `profile` | `profile.schema.json` | `data/profiles/` | Yokai, temperament, reaction tier + frames, aggression, preferred range, the habit, weighted behaviours, `tell_cover_pct`, `ex_use`, `burst_use`, `guard_pct`. `when` values are on-screen observations only (P6). `BehaviourProfile`. |
| `clip` | `clip.schema.json` | `data/clips/` | Source, clip, frames_total, hit_start, hit_end, retarget notes, `trim_start`/`trim_end`, `speed_scale`, `file` (GLB under `game/`), `yaw_offset`, `retime` curve. `ClipCatalog`. |
| `story_card` | `story-card.schema.json` | `data/story/` | Id, status, trigger (S3), title, text (S2, max 280), `placeholders` (S4), speaker. `StoryLibrary`. |
| `merchant_shop_lines` | `merchant-shop-lines.schema.json` | `data/story/shop/` | Merchant event lines and item lead-ins (S1, S2). `MerchantShopLines`. |
| `run_map` | `run-map.schema.json` | `data/map/` | R1, R2, Y3: `schema_version` 1, `nodes` (id, row, open `type`, `yokai`, `elder`, `label`, `payload`), `routes`. `MapLoader`. |
| `node_scenes` | `node-scenes.schema.json` | `data/map/` | R2, R3, A7, Y4: `default_scene`, `default_stage`, `fallback_fighter`, `tiers` (`reaction_frames`, `damage_dealt_pct`, `jump_punish_pct`), `types`, `nodes`, `rows`, `opponents` (`fighter`, `kit`, `plays_as`, `tier`, `profile`), `coins`. `NodeSceneTable`. |
| `trial` | `trial.schema.json` | `data/trials/` | R7, K2: id, `special`, `prompt`, `attempts` 3, `setup` (distance, attempt frames, dummy), `goal.steps` 1–4. No Kata-only keys. `TrialLoader`. |
| `fighter` | `fighter.schema.json` | `data/fighters/` | `push_width` (1–1,060; absent = `SimConfig.BodyWidth`). `FighterData`. |
| `elder_twist` | `elder-twist.schema.json` | `data/twists/` | Y3: elder, effect, repeat/hits, label key, icon, intro and callout cards. `ElderTwistLoader`. |
| `level_preset` | `level-preset.schema.json` | `data/presets/` | V8, A5: `special`, `level`, `colour`, optional `spark` (colour, scale, frames, textures, sheet columns, tint). One per special + level. `LevelPresetLibrary`. |
| `harness_bot` | `harness-bot.schema.json` | `data/harness/` | The newcomer Kihon bot (all values proposed). `BotConfig`. |
| `harness_bot_levels` | `harness-bot-levels.schema.json` | `data/harness/` | Bot skill levels merged onto the base bot. `BotLevels`. |
| — | `common.schema.json` | — | Shared `$defs`: id, yokai, rarity, status, ruleIds, card, frameRange, rect, timedRect, condition, effect, `effectTarget`/`effectShape` (= `Effects.Vocabulary`), `modifierCondition`, `stage`. |

## Conventions

- **ids** are lowercase kebab-case and equal the file name. References (`clip`, `preset`) are ids resolved in their folder.
- **Integers only** for gameplay numbers (F3): frames, units, percent. `mul_pct: 130` = ×1.3. Whole floats (2.0, 1e1) read as integers on both sides.
- **Frames** are 1-based; active frames are `startup+1 .. startup+active`. `"active": null` = rules.md "—" (projectile carries the hit).
- **Rects** are gameplay units (200 per metre) from the character's feet, x toward the opponent, y up.
- **Effects** `{target, op, value, when?}` are the one change vocabulary for Lv 2, Lv 3, EX and modifiers (A5, A10). `op` = `add | mul_pct | set`; `when` is a game-state condition, never a button.
- **`status: "proposed"`** marks values awaiting the designer. **`rules`** lists rules.md IDs (validator rejects unknown ones).
- `data/samples/` holds one valid file per kind; `data/samples/invalid/` holds malformed files the validator tests expect to fail. The game never loads samples.
