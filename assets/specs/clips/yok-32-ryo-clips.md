# YOK-32 Ryo clip picks for the YOK-39 demo (spec step, nothing run)

Ticket YOK-32. Rules: F2, F5, F6, C2-C8, E4, E10, E11, V8. **Status: RUN (23 takes, 69 credits, designer-approved; see Takes). Balance after: 818.**
Rig: Ryo `01a10e47-857e-73a0-9f7a-cd74795198fb`. Retarget gate closed, Ryo GO (`docs/assets/retarget_gate.md`). Open-handed strikes (no finger bones). Demo ships silent: no sound cues in this pass.

## How the ids were found (and how much to trust them)
The MCP cannot list the library, so ids come from Meshy's public table (docs.meshy.ai/en/api/animation-library, fetched 2026-10-06, 656 rows). Each preset also has a public preview GIF at `cdn.meshy.ai/webapp-assets/feature-demo/animation/preview/biped/<Name>.gif`. I pulled the GIFs for every candidate and read the **duration and key poses** from them (free). This is the check that was missing for take 389. Sanity check: the GIFs for 194 (1.04 s) and 259 (4.6 s) match what we measured after generation (63 and 285 ticks). Previews are low-res and front-on, so every "contact" tick below is an **estimate (plus or minus 5 ticks)**, confirmed only at measurement. Ticks are 60/s. "Contact" = the visible hit pose (full extension / release).

`meshy_animate` takes only an action id, so there is no prompt generation here. The library has `_inplace` variants for some walks and runs (ids 607-696); strike clips have none, so root motion is stripped at measurement as in YOK-31 (hips drift was under 1 cm on 194).

## Already exist (0 credits)
| Move ID | Clip | Notes |
|---|---|---|
| walk-fwd | free rig walk `game/assets/generated/characters/ryo/ryo-walk.glb` (`walking_man`, 64 ticks loop) | PASS in YOK-31. Neutral walk, not guarded. About 0.023 m per tick |
| N03 heavy-punch | `game/assets/generated/clips/ryo-heavy.glb` (194 `Right_Uppercut_from_Guard`) | Head-trim 28 ticks; contact about tick 10-14 of the trimmed 35-tick clip. C8 10/4/20 |
| T01 generic-throw | `game/assets/generated/clips/ryo-grab-2.glb` (259 `Step_Forward_and_Push`) | Head-trim from tick 30 (decided). Contact about tick 6 after the trim |
| dash-fwd | free rig run `ryo-run.glb` | Trim 18 ticks from the run start (C2 18 frames); distance 300 units is data (E10). A gait, not a lunge: WARN, acceptable for the demo |
| walk-back | free rig walk played at speed -1 | Reads as the same gait reversed. WARN. Upgrade option U2 below |
| jump-fwd, jump-back | reuse the jump-neutral clip | Drift of 8 units per frame is data (E10, clip_needs R05). Upgrade option U3 for a proper back jump |
| throw-break (T02) | reuse the hit-high (R09, 174) clip on both fighters | clip_needs fallback: both play R09 pushback. Not paired |
| burst (R16) | hold pose of block-high + shockwave VFX | clip_needs fallback. 20 invulnerable frames are data (C6) |
| idle (R01, not in the YOK-32 list) | static guard pose = frame 0 of `ryo-heavy.glb` | Placeholder only; see flag 5 |

## Alignment with the Kitsune spec (YOK-42, PR #27)
One consistent list: every move both fighters share uses the **same preset id** (F6). Where my first draft differed I adopted the YOK-42 pick. Changes from my draft: LP 191 to **192**; MP 192 to **210**; LK 209 to **211**; MK 206 to **209**; HK 215 to **207** (fallback 213 `Leg_Sweep`); block-low substitute replaced by **146** `Block10`; hit-high 179 to **174**; knockdown 187 to **190** (fallback 187); get-up 347 to **344** (fallback 347); crouch 319 to **258** (alt 319); **Foxfire 136** added on Ryo (fallback 132). Ryo-only rows (no Kitsune equivalent): dash-back, S01, S02, jump-in normals. Each preset is one `meshy_animate` call **per rig**, so Ryo's 3 credits per shared move are counted here and Kitsune's in YOK-42.

Differences and risks the designer should see:
- **Crouch 258** has no down/up transition in the preview (5.6 s of a held squat, arms near the knees). 319 `air_squat` has real transitions but holds the arms forward. 258 was adopted for consistency and sleeve safety; down/up becomes a data blend. Swap to 319 on both rigs if it reads badly.
- **Knockdown 190**: I only have its duration (3.2 s), not a close look. I saw 187 (a back flip, legs overhead).
- **HK 207** is partly airborne in the preview (leaves the ground around 1.2 s). On either rig it may fail as a ground kick; the 213 fallback applies to both.
- Walk back and dash forward use the free rig clips in both specs. The Kitsune spec has no back dash (clip_needs: not required); Ryo gets 543 (row 13).

## Takes to approve (3 credits each, Ryo rig)
"Raw contact" is the estimate from the preview GIF. "Trim / speed" is applied in Godot (animation range and `speed_scale`: data, not hand keying). Where the raw clip is longer than the window, Movesmith keeps the clip's truth (F2), cuts the tail with a blend and flags the delta. "Shared" = same id in YOK-42.

| # | Move ID | Preset (id, name) | Shared | Why | Raw length / raw contact | Trim / speed | Target window | Credits |
|---|---|---|---|---|---|---|---|---|
| 1 | N01 light-punch | **192** `Right_Jab_from_Guard` | yes | Quick straight jab from a guard | 1.96 s (118 ticks) / ticks 31-41 | head-trim 26, 1.5x: startup about 4, active 2; tail cut | 4/2/7 (13) | 3 |
| 2 | N02 medium-punch | **210** `Boxing_Guard_Prep_Straight_Punch` | yes | Cross punch with a hip turn | 3.88 s (233) / cross ticks 106-125 | head-trim 100, 1.0x: startup about 6, active 3 | 6/3/12 (21) | 3 |
| 3 | N04 light-kick | **211** `Boxing_Guard_Step_Knee_Strike` | yes | Quick strike close to the body; a knee for a shin kick is visual only | 2.44 s (146) / knee peak tick 53 | head-trim 44, 1.5x: startup about 6 | 5/2/9 (16) | 3 |
| 4 | N05 medium-kick | **209** `Boxing_Guard_Right_Straight_Kick` | yes | Long straight front kick; knee lifts at 0.48 s | 1.40 s (84) / ticks 36-53 | head-trim 28, 1.2x: startup about 7 | 7/3/14 (24) | 3 |
| 5 | N06 heavy-kick | **207** `Roundhouse_Kick` | yes | Big round kick; knockdown is data. WARN: partly airborne in the preview | 2.64 s (158) / about ticks 80-95 | head-trim 65, 1.2x: startup about 12. Fallback 213 | 12/4/22 (38) | 3 |
| 6 | R07 block-high | **139** `Block2` | yes | Hands up at the head, steady hold | 1.60 s / guard up by 0.44 s | head-trim about 20, hold last pose | about 10 + hold | 3 |
| 7 | R08 block-low | **146** `Block10` | yes | Ends in a crouched guard (0.6 s) | 0.60 s (36) | hold the final pose | about 10 + hold | 3 |
| 8 | R09 hit-high | **174** `Face_Punch_Reaction` | yes | Head/torso snap-back. Also the T02 throw-break clip | 2.76 s (166) / snap by about 0.5 s | trim to about 16 ticks, hold recoil pose | about 16 | 3 |
| 9 | R10 hit-low | **171** `Hit_Reaction_to_Waist` | yes | Doubles over at the waist | 1.64 s | trim to about 16, hold | about 16 | 3 |
| 10 | R11 knockdown | **190** `Knock_Down_1` | yes | Natural fall, ends flat. Also the generic throw victim (C4). Fallback 187 | 3.2 s (192) | trim to the E4 40 ticks, end flat | E4: 40 | 3 |
| 11 | R12 get-up | **344** `Stand_Up1` | yes | Rises from flat to a stand. Fallback 347 `Stand_Up4` | 8.0 s (480, long) | head-trim to the rise, cut to about 25-50 ticks; 4-tick blend from the knockdown | about 25 (rough) | 3 |
| 12 | R06 crouch | **258** `CrouchLookAroundBow` | yes | Held squat, arms near the knees; no transition in the preview (see above). Alt 319 | 5.64 s | down/up as a 6-tick blend, hold a mid-clip frame | 6 in / hold / 6 out | 3 |
| 13 | dash-back | **543** `Step_Back` | Ryo only | Short backstep, 0.92 s, in place, no props | 0.92 s (55) | 3x to 18 ticks | 18 (back dash 240 units, E10) | 3 |
| 14 | R05 jump (neutral, fwd, back) | **466** `Regular_Jump` | yes | Crouch, takeoff 0.3 s, apex 0.7 s, landing about 1.3 s | 1.88 s (113) | span 0.25-1.3 s, about 1.0x; E10 says no pre-jump frames, so trim the crouch to 0-2 ticks | 40 + 4 | 3 |
| 15 | S01 Spirit Wave Lv 1 | **133** `mage_soell_cast_4` | Ryo only | Lunge with a two-handed forward thrust at 0.8-1.0 s | 2.20 s (132) / release ticks 48-60 | head-trim 36: release about tick 12-13; tail cut | 13/-/30 (43) | 3 |
| 16 | S02 Rising Talisman Lv 1 | **196** `Left_Uppercut_from_Guard` | Ryo only | Quick uppercut from guard; other arm to the heavy. Airborne feet are data | 1.36 s (82) / ticks 22-29 | head-trim 17: startup 5, active about 8 | 5/8/28 (41) | 3 |
| 17 | S03 Foxfire (Ryo drafts it in slot C; Tanuki copy, T0) | **136** `mage_soell_cast_7` | yes (Kitsune reference) | One-arm forward cast with a held extension. Fallback 132 `mage_soell_cast_3`; if it fails on either rig both retake together | 2.64 s (158) | trim with release about tick 15, no root move | 15/4/30 (49) | 3 |
| 18 | jump-in punches (YOK-55, E11) | **457** `Jumping_Punch` | Ryo only | Punch at the apex; L/M/H share it at different speeds | 2.64 s (158); airborne punch 0.7-1.4 s | use only the airborne span (about 40 ticks) | set by YOK-55 data | 3 |
| 19 | jump-in kicks (YOK-55, E11) | **422** `Rising_Flying_Kick` | Ryo only | Airborne kick, no props. WARN: it flips | 1.48 s (89) | as is | set by YOK-55 data | 3 |

Moves needing no clip of their own: Spirit Wave Lv 2, Rising Talisman Lv 2 and Foxfire Lv 2 (one tuning step, A4, data only). Hitstun, knockdown and get-up are rows 8-11. The YOK-55 air-normal slot count is not fixed yet; two shared clips are proposed.

### Credits
- **First pass: 19 takes x 3 = 57 credits** (balance 941 to 884). Foxfire on Ryo (+3) is included, as YOK-42 asked.
- **Worst case, 3 takes each (F5 cap): 19 x 3 x 3 = 171 credits.** Every retake is a separate approval.
- Optional upgrades, not in the totals, 3 credits each, each needs its own approval: U1 guarded walk forward **689** `Walk_Fight_Forward_inplace`; U2 guarded walk back **688** `Walk_Fight_Back_inplace` (replaces the reversed walk); U3 back jump **468** `Back_Jump`. All three = 9.
- Combined with YOK-42 (45 first pass, 135 worst case, excluding the Ryo Foxfire take): both specs 102 first pass, 306 worst case.

## Flags (no plausible preset, or weak picks)
1. **Crouch block (R08)** now has a real preset (146); no substitute needed.
2. **Burst (R16).** No burst-like preset. Substitute: block-high hold + shockwave VFX.
3. **Heavy kick (N06)** is the weakest pick (207 may be airborne; startup estimate uncertain). After 3 failed takes F5 applies: propose a mechanically equivalent move on an approved clip (the N05 clip at slower speed, knockdown in data).
4. **Knockdown (R11)** is also the throw victim: check 190 from the throw angle at measurement.
5. **Looping idle** is missing from the YOK-32 list and is not priced. Library idles are relaxed stands (`Idle` 0, `Idle_02` 11); `Combat_Stance` 89 is weapon-style. Raise with the designer.
6. KO (R13) and win pose (R14) are out of scope. If wanted: 8 `Dead` (3.0 s) and 403 `Victory_Fist_Pump` (1.52 s), 3 credits each.

## After approval
Run only approved lines on the Ryo rig (check balance first; log each task id under `## Takes`), download to `game/assets/generated/clips/ryo-<move>.glb`, measure each (frames, contact ticks, root drift, planted-foot shift, jitter), then write `data/clips/<move-id>.json` per the clip schema for passing takes only. None are written now: nothing has been generated.

## Level presets (V8) for Spirit Wave and Rising Talisman
`data/presets/` is not written: `special.schema.json` points at `data/presets/<id>.json` but there is no preset schema in `data/schema/`, and defining one is the gameplay-programmer's call. Spec instead. Lv 1 base, Lv 2 colour shift, Lv 3 (evolution) belongs to the evolution tickets. Every preset keeps a **colour-only fallback** (G3). Ryo hit spark is ink black (V5). New VFX textures go to asset-smith via the Producer.

| Preset id | Level | Colour (proposal) | VFX | Colour-only fallback |
|---|---|---|---|---|
| spirit-wave-lv1 | 1 | wave body ink indigo `#2B2F5E`, rim rice paper `#F2EBDD` | thin ink-brush trail on the projectile, ink black spark | same colours, no trail |
| spirit-wave-lv2 | 2 | shift to persimmon `#D9602B` core, indigo rim | slightly wider trail | persimmon recolour only |
| rising-talisman-lv1 | 1 | talisman paper `#F2EBDD` with red seal `#B3262B`, ink black spark | short paper-streak arc along the uppercut | colours only |
| rising-talisman-lv2 | 2 | seal shifts to gold `#C9A227` | same arc, brighter | gold recolour only |

Colours are palette proposals from V1 for the designer to adjust. Lv 3 (Great Wave, Heaven Seal) presets are out of scope for the demo set.

## Takes
Run 2026-10-06 on the Ryo rig `01a10e47-857e-73a0-9f7a-cd74795198fb`, designer-approved: 23 takes x 3 = **69 credits**, first take only, no retakes. Balance before 941 (shared with the Kitsune run of 54), after 818, so this run spent exactly 69. Full GLBs (4.3 MB each, 95 MB for 23) are not committed. The committed `game/assets/generated/clips/ryo/ryo-<move>.glb` are **animation-only** copies (skeleton, skin and animation kept, mesh replaced by one hidden triangle, no textures; 70-120 KB each). Sampled Godot 4.7.2 poses of the stripped and the full file are identical. The full files can be re-downloaded from the task ids below with `meshy_download_model` (no credits).

Measured as in the YOK-31 gate: skeleton FK and linear-blend skinning at every 60-fps tick (clips come as 30 fps keys), stills rendered in Godot 4.7.2 (camera follows the hips, tick in each label) under `docs/assets/clips/ryo/ryo-<move>.png`. "Raw ticks" count 60 per second from the clip start. Hits are the visible contact or release window; frames are 1-based after the trim (`data/clips/ryo-*.json`).

| Move | Preset | Task id | Credits | Raw ticks | Trim / speed | Frames | Hit | Verdict |
|---|---|---|---|---|---|---|---|---|
| N01 light punch | 192 Right_Jab_from_Guard | 01a1100b-a09e-777e-b304-a4ea9bb17af0 | 3 | 123 | 30-66, 1.5x | 25 | 5-6 | PASS |
| N02 medium punch | 210 Boxing_Guard_Prep_Straight_Punch | 01a1100b-a271-754d-9833-81d94939c09b | 3 | 241 | 106-150, 1.0x | 45 | 7-9 | WARN |
| N04 light kick | 211 Boxing_Guard_Step_Knee_Strike | 01a1100b-a402-765a-8d8d-29284d6a3161 | 3 | 153 | 46-100, 1.5x | 37 | 6-7 | WARN |
| N05 medium kick | 209 Boxing_Guard_Right_Straight_Kick | 01a1100b-a57c-71e7-8cbf-99916ac3aada | 3 | 85 | 33-70, 1.2x | 32 | 8-10 | WARN |
| N06 heavy kick | 207 Roundhouse_Kick | 01a1100b-a710-75cf-b687-8b2adafacfaa | 3 | 163 | none | none | none | **RETAKE** |
| R07 block high | 139 Block2 | 01a1100b-a8cb-747b-a769-b3db687ac12c | 3 | 99 | 20-30, hold | 11 | none | PASS |
| R08 block low | 146 Block10 | 01a1100b-aa32-77b6-9211-30179c1e069f | 3 | 35 | 0-34, hold | 35 | none | PASS |
| R09 hit high | 174 Face_Punch_Reaction | 01a1100b-abad-7540-9b76-d13be5d7c6d7 | 3 | 173 | 28-70, 2.5x | 18 | none | PASS |
| R10 hit low | 171 Hit_Reaction_to_Waist | 01a1100b-ad15-751e-b145-9fec4694b545 | 3 | 101 | 8-50, 2.6x | 17 | none | PASS |
| R11 knockdown | 190 Knock_Down_1 | 01a1100b-ae90-75f8-8e53-6c75728f4c53 | 3 | 199 | 50-130, 2.0x | 41 | none | WARN |
| R12 get-up | 344 Stand_Up1 | 01a1100b-b874-7129-a557-0ae49d5a5aba | 3 | 499 | 100-370, 4.0x | 69 | none | WARN |
| R06 crouch | 258 CrouchLookAroundBow | 01a1100b-b9f9-7773-a18d-d0ebc717b339 | 3 | 353 | hold raw 100 | 1 | none | WARN |
| dash back | 543 Step_Back | 01a1100b-bb66-7261-83e1-2fb15cb5cd52 | 3 | 55 | 0-55, 3.0x | 19 | none | PASS |
| R05 jump | 466 Regular_Jump | 01a1100b-bce7-727a-8ab1-2c4474d00010 | 3 | 115 | 32-76, 0.85x | 53 | none | PASS |
| S01 Spirit Wave | 133 mage_soell_cast_4 | 01a1100b-be4d-7666-8a24-144f0d06edbc | 3 | 135 | 18-75, 1.0x | 58 | 14-16 | WARN |
| S02 Rising Talisman | 196 Left_Uppercut_from_Guard | 01a1100b-bfb5-776a-95e7-4d2986c2a48c | 3 | 83 | 17-60, 1.0x | 44 | 6-13 | WARN |
| S03 Foxfire | 136 mage_soell_cast_7 | 01a1100b-c129-7727-8ad9-cdc35fb05ff9 | 3 | 163 | 39-130, 1.0x | 92 | 16-19 | WARN |
| jump-in punch | 457 Jumping_Punch | 01a1100b-c292-74e1-944e-1ea34687d474 | 3 | 163 | none | none | none | **RETAKE** |
| jump-in kick | 422 Rising_Flying_Kick | 01a1100b-c3f6-7505-b271-e44951bd8675 | 3 | 93 | 4-66, 1.0x | 63 | 25-34 | WARN |
| U1 guarded walk fwd | 689 Walk_Fight_Forward_inplace | 01a1100b-c587-72dd-b1c0-afee2b13bbbf | 3 | 105 | loop 104 | 104 | none | PASS |
| U2 guarded walk back | 688 Walk_Fight_Back_inplace | 01a1100b-cf61-7394-82d1-36faa648c4e4 | 3 | 105 | loop 104 | 104 | none | PASS |
| U3 back jump | 468 Back_Jump | 01a1100b-d0ef-76a8-83b6-0d92b252e5c7 | 3 | 59 | 10-50, 1.0x | 41 | none | WARN |
| looping guard idle | 250 Idle_10 | 01a1100b-d24d-718f-873d-31d57646e812 | 3 | 223 | loop 222 | 222 | none | PASS |

Already existing, matches written in `data/clips/`: heavy punch (194, task 01a10e5b-45a5-7037-98b1-83de6aa9f9e9, head-trim 28, 35 frames, hit 11-14), generic throw (259, task 01a10e69-98a9-7088-a761-e49d556113e6, head-trim 30, tail cut at raw 90: 61 frames, hit 6-12) and the free rig walk (64-frame loop).

### Idle pick
Previews of the library's idle and fighting-stance rows (GIFs, free): `Combat_Stance` 89 holds a sword and shield, `Idle_5` 245 is a loose one-hand pose, `Idle` 0, `Idle_02` 11 and `Idle_03` to `Idle_15` are relaxed or fidgeting stands. **Idle_10 (250)** is a two-fist guard with bent knees and a small bob, 3.56 s preview, 3.70 s on the rig, loops closed (0 deg). Hips sit at 75 cm against 85-87 cm in the strike clips, so every attack should blend in over 8-10 ticks. Open hands (E15).

### Verdict notes
- **Root motion.** Most strikes keep net drift under 11 cm; do not strip pelvis sway. Clips that travel (knee 211: 81 cm step, Foxfire 136: 55 cm step, jump kick 422: 1.4 m, back jump 468: 1.85 m, dash back 543: 45 cm, get-up 344: 66 cm) need the travel stripped and carried by move data; the foot slide that appears after stripping is listed in each `data/clips` note.
- **Orientation.** Every Ryo guard clip starts with the hips yawed about -45 to -50 deg from +Z (side-on stance). Strikes rotate toward -4 deg at contact. The model node needs one fixed yaw so the strike axis lies along the fight axis; this is data.
- **Jitter.** Max joint rate is 5-19 deg per raw tick for all clips; the speed-ups push the knockdown (2.0x, about 38) and get-up (4.0x, about 35) past the 30 limit.
- **Clipping.** No coat, sleeve or leg clipping visible in any still. Edge stretch at the crotch (RightUpLeg) reaches 6-7x on kicks, flips and the knockdown but not in the stills; the YOK-31 gate had 3.8x on the heavy.
- **Sound.** None (the demo ships silent).

### Retakes proposed (designer approves, 3 credits each, take 2 of 3)
| Move | Why it failed | Proposed fallback | Cost |
|---|---|---|---|
| N06 heavy kick (207) | A spinning leap: the body pitches forward about 90 deg, the hips travel 1.67 m, feet reach 0.47 m off the ground at raw 80, the kick lands at raw 83-89, and edges stretch 6.6x. Not a ground kick | **215 High_Kick** (2.04 s preview, standing guard to a high kick, no spin; a ground move) first, 213 Leg_Sweep (2.44 s, a turning low sweep) second | 3 |
| jump-in punch (457) | Not a jump-in punch: it launches 1.7 m high with the arms up, travels 2.9 m, the punch is a swing at raw 80, and it ends in an all-fours landing at raw 120. Hip height 1.71 m, leg-vs-leg axis 0.4 cm | No good library candidate seen (Flying_Fist_Kick 94 is 4.7 s and rolls). **Free option (F5):** jump-in L/M/H reuse the jump-in kick take (ryo-jump-kick) with different data, or the neutral jump | 0 (or 3 for 94, low odds) |
| R12 get-up (344), optional | Usable at 4x (69 frames) but long | 347 Stand_Up4 (1.64 s, rises from prone, so it starts face-down) | 3 |

### Shared moves with the Kitsune run (PR #33), judged on Ryo's own rig
Ryo's clothes are fitted (no hakama or sleeve flare), so cloth issues do not apply; motion issues do, and they match hers. Idle: both rigs use 250 `Idle_10`.

| Move | Ryo result | Same retake for both rigs? |
|---|---|---|
| Get-up 344 | WARN: about 4.4 s of real rise (raw 100-370), 69 frames at 4x | Yes, 347 `Stand_Up4` for both (motion problem) |
| Light kick 211 | WARN: 81 cm step then knee; 40 cm hip travel in the trim window, 12 cm slide if stripped | Yes: if retaken, 103 `Simple_Kick` or 215 `High_Kick` serves both; on Ryo it is usable as WARN if the designer prefers no spend |
| Heavy kick 207 | RETAKE: spin, 1.67 m travel (hers 65 cm sideways), airborne | Yes, same retake for both. She proposed 213 `Leg_Sweep`; I saw both previews and 215 `High_Kick` (2.04 s, no spin) looks the safer ground kick. Designer picks one id for both (F6) |
| Crouch 258 | WARN: starts already squatting, no transition; also turned -59 deg | Same for both: data blend from the idle guard; swap both to 319 if it reads badly |
| Others (192, 210, 209, 139, 146, 174, 171, 190, 136) | PASS/WARN as in the table; 190 flips and 136 steps 55 cm | No retake needed on Ryo; see her notes for cloth-specific WARNs |
