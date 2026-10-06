# YOK-42: Kitsune demo clips (Foxfire + normals), Foxfire level preset, bamboo-grove dusk stage preset

Status: **TAKE 1 RUN AND MEASURED (2026-10-06): 18 takes, 54 credits, no retakes. See `## Takes`.** The spec below is unchanged except where the takes log says otherwise.
Context: YOK-39 demo, retarget gate closed (`docs/assets/retarget_gate.md`): Kitsune GO, wide-sleeve flare on big arm swings accepted; hakama and hair held up. Strikes are open-handed (no finger bones). Rules: F2, F5 (cap 3 takes per move), F6 (in place, one preset shared by every rig), C4 (generic grab, no paired throw), A5/V8 (data-only level presets), V5 (foxfire-orange sparks), V9 (stage). The demo ships silent: no sound cues.

Kitsune rig: `01a10e47-91f6-709b-bd34-8884f8cbabbb`. Ryo rig: `01a10e47-857e-73a0-9f7a-cd74795198fb`.

## How the picks were made

The full public library table (docs.meshy.ai/en/api/animation-library, 656 rows) was fetched and every candidate preview GIF (`cdn.meshy.ai/.../animation/preview/biped/<Name>.gif`) was inspected as a six-frame contact sheet. Picks are therefore judged from the preview, not from the name alone, but frame numbers below are GIF-preview estimates only. Real timing is measured on the exported GLB after the take (F2).

Selection rules: (1) prefer clips whose arms stay low or close to the body, because of the accepted sleeve flare; (2) in-place clips; (3) no clip that needs finger bones; (4) free rig clips where they exist; (5) the same preset id on Ryo's rig wherever the move is the same (F6).

## Approval table

Frames are 60-tick targets after trimming (trim is Movesmith/measure data, not hand-keyed). "Window" = where the hit visibly begins/ends, taken from `docs/moves/clip_needs.md` and rules.md first-pass targets; the measured take overrides it. "Shared" = same preset id should be used for Ryo in YOK-32; each rig still needs its own `meshy_animate` call (3 credits), so Ryo's cost is not in this table.

| # | Move | Preset id and name | Why | Trim notes | Target window (60 fps) | Credits | Shared with Ryo |
|---|---|---|---|---|---|---|---|
| 1 | Walk fwd / back (R02/R03) | Free rig walk (`kitsune-walk.glb`) | Already exists; WARN accepted at the gate | Back = same clip played in reverse in data | 64-tick loop | 0 | Yes (Ryo has `ryo-walk.glb`) |
| 2 | Dash (R04, Fox Step) | Free rig run (`kitsune-run.glb`) | Free, forward lunge read, small arm swing | Take first 18 ticks; movement is data | 18 | 0 | Yes (Ryo rig run) |
| 3 | Jump (R05) | **466** `Regular_Jump` (BodyMovements/Jumping) | Plain neutral jump, arms stay low; crouch, rise, apex, land visible | Strip any root rise (height is data, E10 no pre-jump: trim the crouch wind-up to 0-2 ticks) | ~40 airborne + ~4 land | 3 | Yes |
| 4 | Crouch (R06) | **258** `CrouchLookAroundBow` (DailyActions/Idle) | Deep squat with the hands near the knees (sleeves low); hold pose is stable | Down transition = first ~6 ticks, hold = a clean mid-clip frame, stand-up = same 6 ticks reversed; ignore the head-look | 6 in / hold / 6 out | 3 | Yes |
| 5 | Block high (R07) | **139** `Block2` (Fighting/Blocking) | Guard from a fighting stance, a short impact flex | Take ticks around the flex, hold the last pose | ~10, hold | 3 | Yes |
| 6 | Block low (R08) | **146** `Block10` (Fighting/Blocking) | Only blocking preset that ends in a low crouched guard (15 frames) | Hold the final pose | ~10, hold | 3 | Yes |
| 7 | Hit high (R09) | **174** `Face_Punch_Reaction` (Fighting/GettingHit) | Clear head/torso snap-back | Trim to ~16 ticks; hold or loop the recoil frame | ~16 | 3 | Yes |
| 8 | Hit low (R10) | **171** `Hit_Reaction_to_Waist` (Fighting/Transitioning) | Doubles at the waist, reads as a low hit | Trim to ~16 ticks | ~16 | 3 | Yes |
| 9 | Knockdown (R11, also the generic-throw victim, C4) | **190** `Knock_Down_1` (Fighting/Dying) | Natural fall to the ground in one piece. Fallback: 187 `Knock_Down` (launch variant) | Trim to the 40-tick standard knockdown (E4); must end on a clean flat pose | 40 | 3 | Yes |
| 10 | Get-up (R12) | **344** `Stand_Up1` (DailyActions/Transitioning) | Rises from lying flat to a neutral stand; the knockdown ends flat, so the join is short. Fallback: 347 `Stand_Up4` (faster roll-and-rise, begins on the side) | The clip is long (about 200 preview frames): head-trim to the rise and cut to ~25 ticks. A 4-tick blend into the knockdown is a data setting | ~25 | 3 | Yes |
| 11 | LP (N01) | **192** `Right_Jab_from_Guard` (Fighting/Punching) | Fast straight jab from guard, short arm travel | Trim to 13 ticks around the jab; the guard frame before it is also the idle stance (see notes) | 13: 4/2/7 | 3 | Yes |
| 12 | MP (N02) | **210** `Boxing_Guard_Prep_Straight_Punch` (Fighting/Punching) | Cross punch with a hip turn | Long clip (~97 preview frames): head-trim to the cross, ~21 ticks | 21: 6/3/12 | 3 | Yes |
| 13 | HP (N03) | **194** `Right_Uppercut_from_Guard` (already generated, `game/assets/generated/clips/kitsune-heavy.glb`) | Gate clip, sleeve flare accepted | 28-tick head-trim (Movesmith) | 35: 10/4/20 | 0 | Yes (Ryo has `ryo-heavy.glb`) |
| 14 | LK (N04) | **211** `Boxing_Guard_Step_Knee_Strike` (Fighting/Punching) | Quick low strike close to the body: smallest hakama stretch and no sleeve use. A knee for a "low shin kick" is a visual change only | Trim to 16 ticks | 16: 5/2/9 | 3 | Yes |
| 15 | MK (N05) | **209** `Boxing_Guard_Right_Straight_Kick` (Fighting/Punching) | Long straight front kick, upright, 35 preview frames | Trim to 24 ticks | 24: 7/3/14 | 3 | Yes |
| 16 | HK (N06) | **207** `Roundhouse_Kick` (Fighting/Punching) | Big round kick for the knockdown. Highest hakama/tail risk, arms swing wide. Fallback: 213 `Leg_Sweep` | Trim to 38 ticks | 38: 12/4/22 | 3 | Yes |
| 17 | Throw (T01, C4) | **259** `Step_Forward_and_Push` (DailyActions/Pushing), same as Ryo's chosen grab | Fixed by the gate. One generic grab | Head-trim from tick 30; step is move data | ~30: 5 startup | 3 | Yes (Ryo take exists: `ryo-grab-2.glb`) |
| 18 | Foxfire (S03) | **136** `mage_soell_cast_7` (Fighting/CastingSpell) | A one-arm forward cast from a standing guard with a held extension: the flame release reads at the extended hand, arm travel is forward, not overhead. Fallback: 132 `mage_soell_cast_3` (two-hand push, lunging stance) | Trim to 49 ticks with release at about tick 15; no root movement | 49: 15/4/30; hit_start about 15 | 3 | **Yes. Must read on Ryo's rig (slot C reward, T0 copy), see below** |

First pass (this ticket): 15 generated takes (#3-#12, #14-#18, 15 rows with credits) at 3 credits = **45 credits**. Rows 1, 2 and 13 are free/existing.
Worst case at the F5 cap of 3 takes per move: 15 moves x 3 takes x 3 = **135 credits** (90 beyond the first pass).
If YOK-32 does not already apply 136 to Ryo's rig: +3 for the Ryo Foxfire take (+9 worst case), counted in YOK-32 rather than here.

## Per-move spec details

For every move: source is a library preset (F6), in place, humanoid skeleton, take cap 3 (F5), applied to the Kitsune rig, with the same preset applied to Ryo's rig under YOK-32 where marked. Verdict, measured `frames_total`, `hit_start`, `hit_end` and `data/clips/<move-id>.json` follow after the take is run. Do not write `data/clips/*.json` before measuring.

- **Acceptance (all moves):** no root motion that cannot be stripped; planted-foot drift within 3 cm; sleeve flare allowed only on contact frames (accepted at the gate), but a smeared or inside-out sleeve on the hit frame is a reject; hakama must not poke through the leg; tails (proxy fan) clear of legs on the pose.
- **Where the hit lands:** LP/MP the open hand at full extension in front of the torso; HP uppercut peak; LK knee at its highest forward point; MK/HK the foot at full extension; Foxfire the hand at full forward extension (projectile spawn point); throw the two palms meeting the opponent after the step.
- **Knockdown/get-up pairing:** the knockdown must end flat and the get-up must start flat so a short blend suffices. Tails on a back landing are a clipping risk (clip_needs flag 7): if it clips, retake with 187 or prefer a side/front collapse.
- **Habit "often jumps after a knockdown" (Y2):** a readable get-up into a jump is two clips played in sequence in data: row 10 (get-up, about 25 ticks), then row 3 (jump) with no pre-jump frames (E10). The get-up must end in a clear standing pose before the first jump tick so the player can read "she is up and will jump". No new clip is needed, and no combined clip is generated.
- **Idle (R01) is not in this scope.** Proposed free stand-in for the demo: hold the guard pose from the first frames of the LP clip (row 11). The designer should confirm, or add an idle take (3 credits, e.g. a fighting-stance idle) to a later ticket. KO/win clips are also out of scope for this ticket.
- **Sleeves low:** picks favour clips with guard-hand or low-arm poses (jab, knee, front kick, short block). The remaining wide-arm risks are the roundhouse (row 16), the Foxfire cast (row 18) and the high block (row 5); if the measured sleeve stretch looks worse than the accepted heavy at its hit frame, that take is rejected and retaken with the listed fallback.

### Foxfire on Ryo's rig (T0, slot C reward)

The Tanuki copies specials with Ryo's model, and Ryo can draft Foxfire into slot C, so the same preset 136 must also be applied to Ryo's rig (`01a10e47-857e-73a0-9f7a-cd74795198fb`) and checked. Ryo has tight sleeves, so the risk is lower than on the Kitsune. Her clip is the acceptance reference; if 136 fails on either rig, both retake together with 132.

## Foxfire level preset (V8, A5): spec section

`data/presets/` and a preset schema do not exist yet (no `data/schema/preset.schema.json`), so this is a spec for the gameplay-programmer/Movesmith to turn into data once the schema exists. Do not write `data/presets/foxfire.json` until then. The sound field is omitted because the demo is silent. Spark colour follows V5: foxfire orange (the same colour whether Ryo or Kitsune casts it, because the spark colour is by source = Kitsune special).

| Level | Projectile / hand VFX | Hit spark | Colour-only fallback (G3) |
|---|---|---|---|
| Lv 1 base | Small orange-persimmon teardrop flame, warm yellow core | Orange spark, small | Same: base orange |
| Lv 2 colour shift | Same mesh/VFX, hue shifted to a deeper red-orange with a brighter core | Same spark, deeper hue | Hue shift only |
| Lv 3 Piercing Foxfire (E03) | Elongated white-hot flame with an orange edge and a short trail (passes through projectiles, A-section) | Spark with a white core | **Required:** white-hot colour plus an orange rim on the Lv 1 projectile (no new texture) |

Rules for the data: every level reuses the base Foxfire clip (A5, F6); no new clip. Any new VFX texture (flame sprite, trail) is requested from asset-smith through the Producer (generated, F6); if it is not approved, the colour-only fallback in the right-hand column is used. Per-level keys to define in the schema: `move_id: foxfire`, `level`, `projectile_tint`, `core_tint`, `spark_tint`, `trail_enabled`, `vfx_texture` (nullable), `fallback_tint`. Frame offsets: spawn at the clip's measured `hit_start`.

## Bamboo grove at dusk: stage preset (V9): spec section

The stage assets are generated per `assets/specs/bamboo-grove-dusk.md` (YOK-29/30). This preset is the Godot-side assembly and mood, to be written as data once the schema exists (no preset or stage schema exists yet). It adds no generation cost.

| Item | Value |
|---|---|
| Stage id | `bamboo-grove-dusk` (Kitsune's stage, V9) |
| Background | Generated backdrop on a quad behind the fight plane |
| Props | Bamboo cluster x5-8 at varied depth/scale/rotation; stone lantern x1-2 at the edges, outside the fighter band |
| Floor | Tiled ground strip |
| Dusk light | One low warm directional light (persimmon `#D8632C`, low angle, no baked shadows), an indigo (`#2D3A5E`) ambient fill, and a pine (`#34483B`) tint on the midground |
| Fog | Faint indigo depth fog behind the props so fighters read against the lower third |
| Contrast | Fighters keep the toon + outline pass; verify Kitsune's persimmon-ish costume against the orange sky by screenshot (acceptance in the stage spec) |
| Hit spark colour on this stage | Unchanged (V5): foxfire orange is the Kitsune source colour, so a spark against the orange sky needs a dark outline ring: verify by screenshot, tint is data |
| Music | Not part of this ticket (demo silent) |

## Takes

Take 1, 2026-10-06. Designer-approved this session: Kitsune rig `01a10e47-91f6-709b-bd34-8884f8cbabbb` only, 18 takes x 3 = **54 credits**, first take only, no retakes. Spent **54**. Balance at the check before the run 941; after the run 818, which includes the Ryo agent's parallel YOK-32 spend (69), so mine is 941 - 54 = 887 before theirs. Three rows were added to the 15 first-pass rows: guarded walk forward 689, guarded walk back 688, one guard idle (250 `Idle_10`; no Ryo id was pushed yet, so chosen from the public table's previews: a looping fighting stance, arms close to the body, 89-frame preview. If YOK-32 picks another idle, the Kitsune idle is the one to keep in sync).

GLBs: `game/assets/generated/clips/kitsune/kitsune-<name>.glb` are animation-only copies (skeleton, skin and animation kept, the mesh replaced by one invisible triangle, no textures; made by `tools/strip_clip.py`; poses verified identical to the full files in Godot 4.6.3, max joint-matrix difference 0). 18 files, 2 MB in total. The full 4.6 MB files are not committed: re-download them for free from the task ids below with `meshy_download_model` (task_type animation). Stills: `docs/assets/clips/kitsune/<name>.png` (front on the top row, side below, with the 9-capsule proxy tail fan on the hips; seven to eight raw ticks per sheet across the usable window). Measured with `tools/measure_clip.py` (numpy, FK plus linear-blend skinning, 60 ticks) and Godot 4.6.3 stills.

### Verdicts

Criteria: PASS = nothing to flag. WARN = usable, a named cost the designer can accept (sleeve flare in the accepted heavy class, treadmill walk, data blend needed). RETAKE = a spec criterion fails: more than 35 cm of body travel in the usable window, a required motion absent or 5x longer than needed, a tube-deformed hakama, or a hit-frame sleeve stretch above 14x (the accepted heavy peaks at 9.6x on its hit frames, 11.7x over the clip). "Ticks" are raw 60 fps ticks at 30 fps keys.

| # | Move | Preset | Task id | Raw ticks | Trim (raw) | Frames / hit window | Verdict |
|---|---|---|---|---|---|---|---|
| 3 | jump | 466 | `01a1100b-3de4-7225-8eaf-8a047bfe2257` | 114 | 30-78 | 49; airborne 3-37 | WARN (arms swing wide, sleeves like wings at the apex, 11.3x) |
| 4 | crouch | 258 | `01a1100b-3f90-750c-9cc6-387b5c496768` | 352 | hold 108-137 | 30 | WARN (**no stand-to-crouch transition in the clip**; hold only; down/up is a data blend; deep squat) |
| 5 | block high | 139 | `01a1100b-411d-73c3-b9a1-93acbc8fa464` | 98 | 14-50 | 37; flex 19-25, hold from 27 | WARN (sleeves pile at the head, lining visible, 11.4x) |
| 6 | block low | 146 | `01a1100b-42d4-7488-8182-7a76ef612eac` | 34 | 0-34 | 35; hold from 29 | WARN (cuffs open to the camera 12.7x; right foot drifts 7.3 cm) |
| 7 | hit high | 174 | `01a1100b-4478-710f-b827-d5a3bc27c8ae` | 172 | 28-60 | 33; snap 3-17, hold after | WARN (hips 13 cm back; tail proxy 1 cm under floor on one tick) |
| 8 | hit low | 171 | `01a1100b-45dd-724a-afb2-327d778f1f87` | 100 | 10-46 | 37; snap 3-13, hold after | WARN (tail proxy 1.7 cm under floor for 18 ticks) |
| 9 | knockdown | 190 | `01a1100b-7228-76d3-9c4c-5d8988209547` | 198 | 52-100 | 49; flat from 44 | WARN (a backward somersault, hakama flips over the torso at raw 70-90, 14.9x; 44 vs 40 ticks) |
| 10 | get-up | 344 | `01a1100b-738a-7041-839a-dbb2a8ccf02b` | 498 | 104-364 | 261 of continuous motion | **RETAKE** (about 4.4 s from lying to standing; target 25 ticks; cannot be trimmed, a 10x speed-up smears) |
| 11 | LP | 192 | `01a1100b-74f7-749d-89a7-ca797e1ce8a3` | 122 | 26-67 | 42; hit 9-22 | **RETAKE** (hit-frame sleeve stretch 16.6x, 336 stretched sleeve edges per tick vs the heavy's 172; lining visible on both sleeves) |
| 12 | MP | 210 | `01a1100b-764a-75b4-b229-55e9f0974eb8` | 240 | 103-167 | 65; hit 10-19 | WARN (long sleeve flare at full reach 7.7x; guard frames fold sleeves over the face) |
| 13 | HP (existing) | 194 | `01a10e5b-4763-7086-95f6-9b05c342b132` | 63 | 28-62 | 35; hit 11-14 | WARN (gate, accepted) |
| 14 | LK | 211 | `01a1100b-77c2-716a-aa3b-b824c890837c` | 152 | 42-114 | 73 | **RETAKE** (the hips advance 41 cm in the usable window, 76 cm in the clip; stripped, both planted feet slide 13.5 cm) |
| 15 | MK | 209 | `01a1100b-7944-77f4-ae8b-d192ac8fa326` | 84 | 29-63 | 35; hit 11-25 | WARN (a head-high kick, foot 107 cm; hakama drawn into a tube around the leg; 11.9x sleeves) |
| 16 | HK | 207 | `01a1100b-7ad5-7627-be19-120b28f86a52` | 162 | 69-114 | 46; hit 15-21 | **RETAKE** (a spinning kick: 65 cm sideways root travel, left foot slides 22.5 cm stripped; the hakama is dragged into a tube around the leg at 83-89) |
| 17 | throw | 259 | `01a1100b-7c57-73cf-a0d0-82566171bd64` | 284 | 30-60 | 31; hit 4-20 | WARN (28 cm hip advance must be replayed from move data, 30 cm slide if stripped; the accepted shove) |
| 18 | Foxfire | 136 | `01a1100b-7dd0-77bc-bf29-391d4c8fcd25` | 162 | 35-129 | 95; release 19, extended 30, hold to 66 | WARN (34 cm hip advance, right foot shuffles 33 cm: spawn point is the hand; startup 18 vs 15) |
| +1 | walk guarded fwd | 689 | `01a1100b-7f34-7143-8c35-78c8784825ad` | 104 | 0-103 | 104 loop | WARN (hakama stretch at the free-walk level; stance foot 0.64-0.72 m/s) |
| +2 | walk guarded back | 688 | `01a1100b-8968-7618-8ced-06214e02cfd3` | 104 | 0-103 | 104 loop | WARN (loop pop 7.4 deg; stance foot 0.57-0.61 m/s) |
| +3 | idle (guard) | 250 | `01a1100b-8ac2-7638-8dca-a0a81de6ab39` | 222 | 0-221 | 222 loop | **PASS** (loop closes at 0.3 deg, root 0, feet planted 0 cm) |
| 1 | walk (free rig) | `walking_man` | n/a | 64 | 0-63 | 64 loop | WARN (YOK-31, unchanged) |

Dash (row 2, free rig run) was not part of this run's measurement. All takes: no jitter (max joint rotation per tick 3-19 deg, limit 30), no inside-out sleeve in the stills of the WARN clips beyond the accepted-heavy class, tails (proxy fan) clear of legs and hair in every pose; the proxy dips under the floor only in the two hit reactions (1-1.7 cm) and in the supine get-up. The proxy tail test against the hakama is not discriminating (the proxy root sits inside the skirt in the bind pose), so the stills decide.

### Retakes proposed (designer approves; nothing run)
| Move | Why | Proposed preset | Credits |
|---|---|---|---|
| get-up (R12) | 344 is a 4.4 s rise | 347 `Stand_Up4` (spec fallback, a quick roll-and-rise; unseen, verify on the preview) | 3 |
| LP (N01) | sleeve inversion on the jab at the hit frames | 191 `Left_Jab_from_Guard` (the mirrored jab; same family, may share the flare, so it is not a sure fix) | 3 |
| LK (N04) | 41 cm lunge | 103 `Simple_Kick` (Fighting; unseen, check the preview first); 215 `High_Kick` is the other candidate | 3 |
| HK (N06) | spinning kick, 65 cm sideways travel | 213 `Leg_Sweep` (spec fallback) | 3 |

Total if all four are approved: 12 credits (take 2 of 3 for each move, F5). If a retake is not approved, the move substitution rule applies (F5): LP could reuse MP (210) trimmed, LK could reuse MK (209). The Ryo rig takes the same ids, so a shared retake is 3 credits per rig, counted in YOK-32.

### Data written
`data/clips/kitsune-*.json` for every PASS and WARN take (16 files, schema-valid): idle, walk-fwd, walk-guard-fwd, walk-guard-back, crouch, jump, block-high, block-low, hit-high, hit-low, knockdown, medium-punch, medium-kick, heavy-punch, throw, foxfire. None for the RETAKE rows. `hit_start`/`hit_end` for a hit reaction are the visible snap; for blocks, jump, crouch, walks, idle and knockdown they are null. The Foxfire (V8) and bamboo-grove presets stay in this spec because `data/schema/` has no preset schema.

## Needed from the designer

1. Approve the 15-take first pass (45 credits), or strike rows. Worst case 135.
2. Confirm the guard-pose-from-LP stand-in for idle (or add an idle take).
3. Confirm no Ryo Foxfire take is run here (YOK-32 owns Ryo's rig).
4. Ryo's picks: no `YOK-32-ryo-clips` branch exists on origin yet, so the flags above show which Kitsune picks should be adopted by Ryo: rows 3-12 and 14-18 all use the same preset id.

## Retake run (YOK-32-clip-retakes, 2026-10-06): take 2 of 3, designer-approved, Kitsune rig, 3 credits each
Stills: `docs/assets/clips/kitsune/hk-take2.png`, `lk-take2.png`, `getup-take2.png`.

| Move | Preset | Task id | Verdict | Data |
|---|---|---|---|---|
| N06 heavy kick | 215 High_Kick | 01a1101d-91e6-706a-aea6-a3195cdc82a3 | WARN | `kitsune-heavy-kick.json`, 38 frames, hit 12-15, trim raw 46-93 at 1.25x. Head-high kick, hakama drawn into a tube around the leg at raw 62-72, stretch 13.6x (limit 14), left foot slide 35 cm if stripped (about 45 cm hip travel is move data) |
| R12 get-up | 347 Stand_Up4 | 01a1101d-951b-7220-afc2-2b54408d8fe4 | WARN | `kitsune-get-up.json`, 33 frames, raw 22-102 at 2.5x. Hair covers the face and the hakama bunches on all fours, sleeve stretch 12.0x. Get-up end to jump start: hips 91 vs 88 cm, mean joint difference 22.7 deg, a 6-tick blend is enough. Face-down start does not join the face-up knockdown end |
| N04 light kick | 103 Simple_Kick | 01a1101d-97fe-760b-b89c-0ed4c59f5f96 | FAIL (not committed) | Walking kick: hips +145 cm over the clip, 42 cm in the kick window, left foot slide 37 cm stripped, stretch 13.9x. The 211 file stays |

Take 3 proposal: do not spend; F5 substitute LK = the MK clip (209) trimmed and sped up. Kitsune LP is now the F5 substitute `kitsune-light-punch.json` (MP clip 210 at 1.5x, trim raw 106-124, 13 frames, hit 5-6). Free re-timing of MP, MK, throw, HP and Foxfire is in each data file's notes.
