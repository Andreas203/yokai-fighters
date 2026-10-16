# Oni club clips (YOK-102 follow-up to the G4 gate)

Status: **SPEC, nothing run (2026-10-16).** Designer approved the spend; the main session runs the jobs. Rules: F2-F6, G4, AM5, T0 (n/a: the Tanuki copies with Ryo's model, so these clips are Oni-only).
Rig: `game/assets/generated/characters/oni/oni-rigged.glb` (2.2 m), Meshy rig task `01a144ad-b98d-7520-8376-c776c414a476`. Club: `club-grip.glb` on `RightHand`, 1.32 m on screen, always in hand.
Balance at the check: 697 credits (497 monthly, 200 permanent).

## Why
The shared bare-handed clips put the club through the near shin on every walk back-swing (ticks 29-59, up to 15 cm) and through the back of the head in the heavy's recovery (ticks 55-61, up to 11 cm). No mount angle fixes it (`retarget_gate.md`, `rigs/oni.md`). The fix is clips whose right arm does not swing back or lift behind the head.

## How the picks were made
The public library table (docs.meshy.ai/en/api/animation-library, 657 rows) was fetched and the candidate preview GIFs inspected as eight-frame contact sheets. `meshy_list_animations` was not reachable from this session, so ids come from that table; ids are exact, timing is a preview estimate only (F2: measure after the take). The previews show a generic mannequin, mostly without props, so arm paths are judged, not clearance: the club check after the take decides.

Selection rules: (1) the right forearm stays forward or out, no back-swing past the hip; (2) in place; (3) no finger bones needed (the club is rigid on the hand); (4) a one-handed club in the right hand must read even if the preset is two-handed (see mount impact).

## Clips needed and picks

| # | Clip | Preset | Why | Mount impact |
|---|---|---|---|---|
| 1 | Idle (R01) | **335** `Axe_Breathe_and_Look_Around` (DailyActions/LookingAround, 272 preview frames) | Wide, low, hunched weapon-ready stance; both arms hang forward and still, only a breathing sway. Trim a quiet loop and ignore the look-around | Right forearm points down-forward, so the club (75 deg forward of the forearm) points ahead at hip height. Fine. Fallback 89 `Combat_Stance` (52 frames): a sword-and-shield guard, but the right forearm crosses the body, so the club would point across the belly; second choice |
| 2 | Walk forward (R02) | **669** `Slow_Orc_Walk_inplace` (WalkAndRun, in place, 166 preview frames) | Heavy hunched stomp; the arms stay bent in front of the chest and do not swing back, so no back-swing to cut the leg. Reads as an ogre | Right forearm held forward and up, so the club points forward-up, not along the thigh. Check at the legs' crossing ticks. Fallback 689 `Walk_Fight_Forward_inplace` (guard walk, close arms) |
| 3 | Walk back (R03) | **687** `Walk_Backward_with_Sword_inplace` (preview of 546, 34 frames) | Armed walk back, arms held out at the sides with a small swing | Right arm out from the body, so the club leans outward, clear of the leg. Fallback 688 `Walk_Fight_Back_inplace` |
| 4 | Heavy (N03) | **128** `Heavy_Hammer_Swing` (Fighting/AttackingwithWeapon, 45 preview frames) | Overhead strike: wind-up through a lean, both arms up over the head, slam down, then a hunched recovery with the arms low in front. The recovery stays low, so the club does not lift behind the head the way the uppercut does | Two-handed preset on a one-handed mount: the left hand grips nothing, and the right hand must carry the arc. The club tilt means that at the overhead peak it points forward-up; check the head and horns at the peak and the wind-up. Fallback 237 `Charged_Axe_Chop` (185 frames, starts crouched; head-trim) |

Heavy alternative at zero credits: trim the shared `ryo-heavy-punch` before tick 55 (hit 38-41 is clean per the gate) and blend the recovery in data. It keeps a bare-handed uppercut look with a club in the fist, so the designer may prefer 128.

## What stays shared (per the gate report)
The gate played only walk, heavy and throw with the club.
- **Throw (`ryo-generic-throw`, ticks 30-59, hit 35-41): clean with the club. Stays shared** (C4, one generic grab).
- Heavy hit frames (28-61, hit 38-41) are clean, but the recovery fails, so the heavy is replaced (row 4) unless the free trim is chosen.
- **Not yet checked with the club:** jump, crouch, blocks, hit reactions, knockdown, get-up, dash/run, light and medium normals. They stay shared by default (F6), but each needs a free `prop_clearance.py --clip` run before it is called clean. Any that fails goes back to the designer with a proposed id; do not assume clean. The knockdown and get-up are the likeliest to bury the club in the floor or body.

## Job table (first pass, one take each, 3-take cap per clip, F5)

| Clip | Tool | Action id or prompt | Credits |
|---|---|---|---|
| Idle | `meshy_animate`, rig `01a144ad-b98d-7520-8376-c776c414a476` | 335 | 3 |
| Walk forward | same | 669 | 3 |
| Walk back | same | 687 | 3 |
| Heavy | same | 128 | 3 |
| **First pass total** | | | **12** |

Fallbacks, each a new approval (retake): idle 89, walk fwd 689, walk back 688, heavy 237, 3 credits each; worst case 3 takes x 4 clips x 3 = 36 credits, so 24 beyond the first pass.
No preset is missing, so no `text_to_motion` is proposed. If every heavy preset fails the club check, propose text_to_motion (swift, 3 credits): "A huge ogre raises a heavy club one-handed in his right hand high over his head, pauses, then slams it straight down in front of him, and holds the follow-through with the club low in front; feet planted, in place, no travel". Needs its own approval.
The Meshy action calls also return the walk and run for free at rigging time; those are the bare shared ones and are not wanted here.

## Per-clip plan after the take
Every step is scripted (F5). Log each task id and credits under `## Takes`, every operation and setting under `## Cleanup`.

1. **Download** with `meshy_download_model` (task_type animation) into `game/assets/generated/clips/oni/` as `oni-<name>.glb`; strip the mesh with `tools/strip_clip.py` as for the Kitsune.
2. **Cleanup** (`tools/blender/clean_clip.py`): strip root motion (in-place presets, so only drift), resample to 60 fps, trim, Butterworth only on jittery channels and never across the hit frames.
3. **Measure** (`tools/blender/measure_clip.py` / `tools/measure_clip.py`): frames at 60 fps, planted-foot slide (reject above 3 cm), jitter (30 deg per tick limit), hit-frame stills into `assets/specs/clips/stills/oni/`. Walk: loop pop under 8 deg, foot speed matched to the walk speed in move data. Heavy: the frames where the club visibly begins and ends the strike.
4. **Club clearance** (the gate's check): `Blender -b --python tools/blender/prop_clearance.py -- --rig game/assets/generated/characters/oni/oni-rigged.glb --prop <club-grip.glb> --log assets/specs/rigs/logs/oni-club-grip.json --out assets/specs/clips/stills/oni --tag oni-club --ignore-radius 0.12 --fps 60 --clip <cleaned clip.glb> --frames <every 3rd tick>`. Pass = 0 new club verts inside the body or head in the whole loop (walk, idle) and in all heavy ticks; floor contact is also read (club tip must not go below the feet at the slam, a ground hit at the end of the strike is intended). Reject on any pass above 2 cm and say which ticks.
5. **Skin and feet** with `skin_clip_check.py --fps 60 --rotations-only 1` (Oni baseline 9/24/45 failing edges) and the toe-height check against his rest contact (sinks 2-5 cm are known and accepted on the shared clips; the YOK-130 hip scale 1.188 still applies).
6. **Retiming (F2, AM5):** a curve per clip, each segment 0.5x-2x, a held pose at most 4 frames.
   - **Heavy:** target about 40 game frames, 10 / 4 / 26 startup / active / recovery. The preview's wind-up is slow, so play the lean and rise at 1.5-2x to reach the overhead peak by frame 9, hold the slam pose through the 4 active frames, then ease the hunched recovery at 0.6-0.8x. `hit_start`/`hit_end` are reported as retimed. Reach (hands about 40% further and 30% higher than Ryo's) and the club tip are move data for movesmith.
   - **Walks:** a plain loop, speed_scale only. Forward about 64-96 ticks, back about 64 ticks from the 34-frame preview, speed set so the planted foot matches the move data's walk speed (tune `speed_scale`, no curve).
   - **Idle:** loop, no curve.
   - Until `ClipCatalog` reads curves, only `trim_start`, `trim_end` and `speed_scale` reach the game; the curve goes in this spec and is flagged for the Producer.
7. **Data:** only after measuring, `data/clips/oni-idle.json`, `oni-walk-fwd.json`, `oni-walk-back.json`, `oni-heavy-punch.json` (schema `data/schema/clip.schema.json`; walks and idle have `hit_start`/`hit_end` null). No sound cues for the idle or the walks; the heavy gets a whoosh at startup and thud plus yokai sting at the retimed hit frame, in `data/sound/oni-heavy-punch.json`.
8. **Gate:** on a failed take say exactly what to change (preset, trim); after 3 rejected takes propose a mechanically equivalent move on an approved clip (F5), never hand-keyed. Walk, heavy and throw rerun the G4 gate (rig-specialist) with the club on; the verdict goes in `retarget_gate.md`.

## Risks
- **Two-handed heavy on a one-handed mount** (128): the left hand holds air and the swing is built around two hands, so the right wrist may bend oddly at the peak.
- **Preview limits:** the previews show no weapon; the real arm path, and so the club clearance, is only known after the take. The orc walk's raised forearm is the likeliest to put the club tip near his head and horns.
- **Idle 335** carries a look-around; the loop may need a trim, and the pose is wide-legged (feet may sit past his rest contact).
- **Foot sink:** the clips carry a mannequin's leg proportions; his known 2-5 cm sink stays.
- **Shared clips with the club** are unchecked beyond walk, heavy and throw (see above); the knockdown, get-up and crouch may need Oni-specific takes (3 credits each, new approval).
- **Heavy timing:** the preview is long, retiming past 2x is not allowed, so if the swing cannot reach about 40 frames the move gets a longer startup (movesmith).

## Takes
(none yet)

## Cleanup
(none yet)
