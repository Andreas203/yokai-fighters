# Ryo rig: Tripo pilot (YOK-146)

Ticket: YOK-146 "Tripo redo: Ryo (pilot)". Owner: rig-specialist. Rules: F3, F5, F6, G4, T0, E15; AM1, AM5.
Designer decisions (2026-10-19, on YOK-146): **Option A**, so Tripo's skeleton becomes the shared skeleton (`skeleton.md` rewritten). The heavy punch and heavy kick come from video mocap (animator's spec). A test branch with the new Ryo playable is wanted soon.

**Verdict: ACCEPT for the test branch (provisional).** The rig is structurally clean and plays every clip, but one scripted stress-pose metric still FAILs. The cause is understood and does not show in the stills; see "Open for the designer". **No generation job was run and no credits were spent.** The old Meshy files (`ryo-rigged.glb`, `clips/ryo/`) are untouched and stay until the Tripo set passes G4.

## Source and jobs
| Item | Value |
|---|---|
| Model and rig | Tripo, Mixamo-preset auto-rig, `game/assets/generated/characters/ryo/Ryo.glb` (dropped by the designer 2026-10-19; untracked in the main checkout and **not committed** here; sha256 `c46dfd32561184d3dab61455f1dcd218b660ee9964c15aafe7dd444ec614dfc7`, 9,840,428 bytes). Task id and credits are not recorded in the file; the designer ran the job |
| Mesh | 53,785 triangles, 31,156 vertices, one material (Tripo base colour JPEG). **About 2.7x the 20k budget** in `assets/specs/characters/ryo-regen.md`. Not decimated (designer's call, see below) |
| Rig as delivered | 65 joints (`mixamorig:` prefix, 40 finger joints), no twist bones, no animation, about 1.0 m tall. **Unusable raw**: no joint node has a transform (the bind pose is only in the inverse bind matrices), and those matrices are in a Z-up frame while the mesh is Y-up. The body flies apart in Blender and in Godot (`stills/ryo-tripo/raw_import_front.png`, `raw_import_side.png`; Godot 4.7.2 reads `mixamorig_Hips` rest as (0, 0, 0)) |
| Canonical rig | `game/assets/generated/characters/ryo/ryo-tripo-rigged.glb`, sha256 `034b20cfbf19f9bb…` |

## How the canonical rig was made (scripted, F5)
1. `tools/blender/canon_tripo_rig.py --in Ryo.glb --out <canon.glb>` (log `logs/ryo-tripo-canon.json`):
   - Fits the bind-skeleton frame to the mesh by the weights: mesh = s·R·skel + t, with R = axis swap (mesh X = skel Y, Y = Z, Z = X), t = (−0.002, 0.497, 0.005) m and s = 1.013. Mean vertex-to-own-bone distance is 4.6 cm; the next-best rotation is 6.6 cm; finger bones land within 0.6 to 1.5 cm of their own vertices.
   - Rebuilds the armature at the fitted bind joints and keeps Tripo's weights. Rest == bind (offset 0.0 m).
   - Strips the `mixamorig:` prefix and merges the 40 finger joints into `LeftHand` / `RightHand` (3,115 vertex influences moved; max influences stay 4). 25 joints remain.
   - Scales ×1.7509 to 1.75 m, puts the feet at Y = 0 and centres on the ankles. Y-up, +Z facing, metre bones, no animation.
2. `tools/blender/repair_weights.py --fighter ryo-tripo --in <canon.glb> --out ryo-tripo-rigged.glb` (log `logs/ryo-tripo-repair.json`). This is the YOK-116 pelvis repair with the thigh falloff band moved down to Tripo's anatomical hip socket (front −10 to +6 cm, back −8 to +10 cm around the `UpLeg` head, which Tripo puts level with `Hips`). Net weight moved: `Hips` +3,250, `LeftUpLeg` −2,538, `RightUpLeg` −711. Only `JOINTS_0`/`WEIGHTS_0` change. The script is deterministic (same hash on two runs).

Why the repair, and why this band (per-clip skin tears over the 8-clip test set, then all 26):

| Weights | Deep crouch (stress) | Clip tears, 8-clip set | Notes |
|---|---|---|---|
| Tripo as delivered (rebuilt only) | FAIL: 212 edges, `Hips` and `LeftUpLeg` collapse | n/a (26 clips: 5,203) | get-up 2,220, jump-punch 808, knockdown 402: `LeftUpLeg` owns about 3.7k vertices against 0.9k on the right, including belly, sash and coat |
| YOK-116 "ryo" profile | FAIL: 83, `LeftUpLeg` p5 0.34 | 1,425 (26 clips: 3,070) | hard `Hips`/thigh seam at the pelvis top: idle guard 157 |
| no smoothing / no side step / falloff only | FAIL: 81 / 85 / 81 | 1,419 / 1,552 / 1,513 | |
| band raised (−4..+16) | FAIL: 255 | 1,131 | |
| band low and wide (−14..+10) | FAIL: 171, and the high kick FAILs | 930 | |
| **band low (−10..+6 front, −8..+10 back): `ryo-tripo`** | **FAIL: 55 edges; volume only, `LeftUpLeg` p5 0.29** | **933** | chosen |
| `prop_bind=1` (charm and pouch rigid to `Hips`) | FAIL: 985 edges | | the props are painted on the body across the hip crease, as on the Meshy Ryo |

## Rig check (`tools/blender/rig_check.py`, Blender 4.1.1)
Report: `logs/ryo-tripo-rig-check.json`. Stills: `stills/ryo-tripo/` (rest, high kick, deep crouch, arm raise, torso twist and grab, front and side; raw-import stills). The tear limit is 0.2% of 84,653 edges, or 169 edges.

| Check | Result |
|---|---|
| Skeleton | **PASS**: 25 bones; names, parents and skin order match `skeleton.md` |
| Rest pose | **PASS**: T-pose. Upper arm to hand 7.7° (left) / 7.6° (right) below horizontal; hip to ankle 9.1° / 9.5° from vertical; rest == bind |
| Scale / up / origin | **PASS**: 1.750 m; Y-up (hips to head 3.8° off vertical); facing glTF +Z; feet on Y = 0, centred |
| Weights | **PASS**: max 4 influences, all normalised, no unweighted vertex, 0 cross-side, 0 arm-leg bleed |
| High kick | WARN: 44 edges (max 8.5x), mostly the long coat panel on the right thigh lifting with the leg |
| Deep crouch | **FAIL**: 55 edges (under the 169-edge limit), but `LeftUpLeg` volume mean 0.749 (limit 0.75) and p5 0.29 (limit 0.40). The squashed vertices sit 16 cm in front of the upper left thigh: the painted paper charm and pouch, which swing in toward the thigh bone when it flexes 110°. Same props, same cause as YOK-110/116 on the Meshy Ryo. The stills read clean |
| Arm raise / torso twist / grab | WARN: 2 / 7 / 8 edges |

Raw `Ryo.glb` for comparison (`logs/ryo-tripo-raw-rig-check.json`): its names map cleanly through the prefix normaliser (no missing bones, no wrong parents), but rest, scale, origin and every pose FAIL (thousands of torn edges).

Godot 4.7.2 import (throwaway project, headless): `ryo-tripo-rigged.glb` imports as `Armature/Skeleton3D` with 25 bones, `Hips` rest (0.003, 0.998, 0.055), `RightHand` global rest (−0.545, 1.366, −0.012), mesh 1.75 m tall, no animation.

## Decisions taken in this ticket
- **Prefix: stripped.** Godot 4.7.2 imports `mixamorig:Hips` as `mixamorig_Hips` (verified), because `:` separates subnames in node paths. Bare names keep `FindBone("Hips")` in `FighterModel.cs` and `RightHand` in `oni.tscn` working. The Blender scripts normalise either prefix on input (`clip_common.normalise_bone_name`).
- **Fingers: removed, with their weights merged into the hand** (E15 mitts). Nothing can curl them, and there are 40 fewer joints.
- **Rest: Tripo's bind pose kept** (arms 7.7° low, legs splayed 9°). The retarget aligns rest directions, so clips are unaffected. Re-posing would mean re-binding the mesh.
- **Units: metres** under an unscaled `Armature` node (Meshy used cm under 0.01).

## Stopgap clips (test branch only)
`game/assets/generated/clips/ryo-tripo/` holds 26 clips with the same names as the Meshy files (README there). Each was made with `tools/blender/retarget_bvh.py --clip` from the Meshy clip: bone map by chain position, rest directions aligned, `Hips` in place (vertical only, ×1.052 hip ratio), a proxy mesh, and the same frame numbers, so every `data/clips` trim holds. Measured over each `data/clips` trim:
- with `measure_clip.py --as-stripped`, which is what the game shows. Slide is the worst planted-foot run in cm per game frame; the limits are 1.0 (warn) and 2.0 (fail).
- with `skin_clip_check.py --rotations-only 1`, the most failing skin edges in any frame. The 0.2% limit is 62 on Meshy (31,130 edges) and 169 on Tripo (84,653 edges).

Logs: `logs/ryo-tripo-clips.json`, `logs/ryo-tripo-retarget/`.

| Clip file | Clip id | Measure (Meshy -> Tripo) | Slide cm/f | Skin tears | Max stretch | Tripo worst bones |
|---|---|---|---|---|---|---|
| `ryo-block-high` | ryo-block-high | PASS | 0.47 -> 0.76 | 6 -> 15 | 3.3 -> 7.0 | LeftForeArm 4, LeftArm 3 |
| `ryo-block-low` | ryo-block-low | PASS | 0.47 -> 0.60 | 52 -> **169** (at the limit) | 4.7 -> 11.2 | Hips 262, LeftUpLeg 23 |
| `ryo-crouch` | ryo-crouch | n/a (1-frame hold; `measure_clip` cannot measure it on either rig) | - | 31 -> 83 | 4.4 -> 8.1 | Hips 90, RightUpLeg 33 |
| `ryo-dash-back` | ryo-dash-back | FAIL (movement clip) | 3.06 -> 3.63 | 16 -> 43 | 3.3 -> 6.3 | Hips 35, LeftUpLeg 12 |
| `ryo-foxfire` | ryo-foxfire-cast | PASS -> WARN | 0.95 -> 1.12 | 14 -> 16 | 3.3 -> 7.0 | RightArm 10, RightUpLeg 6 |
| `ryo-get-up` | ryo-get-up | FAIL (movement clip) | 3.20 -> 3.12 | 84 -> **224** | 4.6 -> 12.4 | Hips 311, RightUpLeg 26 |
| `ryo-hit-high` | ryo-hit-high | WARN -> FAIL (movement clip) | 1.88 -> 2.03 | 18 -> 26 | 4.1 -> 6.2 | RightUpLeg 11, LeftArm 8 |
| `ryo-hit-low` | ryo-hit-low | FAIL (movement clip) | 2.31 -> 3.54 | 36 -> 163 | 3.3 -> 7.0 | Hips 256, LeftUpLeg 37 |
| `ryo-hk` | ryo-heavy-kick | WARN | 1.44 -> 1.87 | 57 -> 132 | 5.9 -> 9.4 | RightUpLeg 80, Hips 64 |
| `ryo-idle-guard` | ryo-idle-guard | PASS | 0.26 -> 0.33 | 34 -> 111 | 3.7 -> 6.2 | Hips 130, RightUpLeg 28 |
| `ryo-jump-back` | ryo-jump-back | FAIL -> PASS (no planted run on Tripo) | 2.40 -> 0.00 | 63 -> 90 | 4.2 -> 12.3 | RightUpLeg 111, LeftUpLeg 24 |
| `ryo-jump-kick` | ryo-jump-kick (also ryo-jump-punch) | FAIL | 2.18 -> 2.75 | 45 -> 155 | 5.2 -> 9.7 | Hips 251, RightUpLeg 18 |
| `ryo-jump-punch` | (unused file) | FAIL -> PASS (no planted run) | 4.99 -> 0.00 | 60 -> **177** | 4.6 -> 11.7 | Hips 288, LeftUpLeg 20 |
| `ryo-jump` | ryo-jump | PASS | 0.50 -> 0.63 | 55 -> **170** | 4.6 -> 7.5 | Hips 278, LeftUpLeg 32 |
| `ryo-knockdown` | ryo-knockdown | WARN -> FAIL (movement clip) | 1.35 -> 2.05 | 36 -> **210** | 4.7 -> 9.8 | Hips 345, LeftUpLeg 18 |
| `ryo-lk` | ryo-light-kick | WARN -> FAIL | 1.82 -> 2.44 | 28 -> 53 | 3.0 -> 11.5 | RightUpLeg 46, Hips 21 |
| `ryo-lp` | ryo-light-punch | PASS | 0.40 -> 0.52 | 14 -> 32 | 4.7 -> 8.7 | Hips 16, RightUpLeg 14 |
| `ryo-mk` | ryo-medium-kick | WARN | 1.18 -> 1.59 | 42 -> 87 | 4.7 -> 11.7 | RightUpLeg 90, Head 16 |
| `ryo-mp` | ryo-medium-punch | PASS | 0.48 -> 0.62 | 20 -> 58 | 3.9 -> 6.4 | Hips 72, LeftUpLeg 17 |
| `ryo-rising-talisman` | ryo-rising-talisman-strike | WARN | 1.20 -> 1.42 | 14 -> 26 | 3.5 -> 6.3 | LeftArm 11, LeftForeArm 8 |
| `ryo-spirit-wave` | ryo-spirit-wave-cast | WARN -> FAIL | 1.59 -> 2.00 | 27 -> 110 | 4.4 -> 6.8 | Hips 181, LeftUpLeg 10 |
| `ryo-walk-guard-back` | ryo-walk-guard-back | WARN | 1.04 -> 1.36 | 14 -> 13 | 2.6 -> 4.8 | LeftShoulder 4, LeftArm 3 |
| `ryo-walk-guard-fwd` | ryo-walk-guard-fwd | WARN | 1.80 -> 1.79 | 18 -> 21 | 2.9 -> 5.6 | Hips 15, LeftUpLeg 7 |
| `ryo-heavy` | ryo-heavy-punch | PASS | 0.94 -> 0.97 | 18 -> 30 | 4.7 -> 7.7 | Hips 19, LeftUpLeg 10 |
| `ryo-grab-2` | ryo-generic-throw | WARN | 1.11 -> 1.31 | 16 -> 20 | 3.3 -> 7.2 | RightUpLeg 12, Hips 4 |
| `ryo-walk` | ryo-walk-fwd | FAIL (movement clip) | 2.28 -> 2.79 | 12 -> 21 | 3.8 -> 5.5 | Hips 31, LeftArm 4 |

Reading the table:
- **Foot slide** rises a little on most clips, by 0.1 to 1.2 cm/frame. The Tripo stance is wider (ankles ±0.225 m against ±0.13 m), and the hips are pinned in place while the legs keep Meshy's rotations. Four clips cross a limit: `ryo-lk` (2.44), `ryo-spirit-wave` (2.00), `ryo-hit-high` (2.03) and `ryo-knockdown` (2.05). Hit, knockdown, get-up, dash and walk are movement clips, where slide is reported but not judged.
- **Joint acceleration** is unchanged in kind; the retarget is per frame with no smoothing.
- **Skin tears** are close to the Meshy rig per edge on standing clips (idle 0.13% against 0.11%; walk, heavy and throw are well under the limit). Four used clips reach or pass 0.2%: `ryo-block-low`, `ryo-jump`, `ryo-knockdown` and `ryo-get-up`. All are in the `Hips` band: a jagged hakama pleat at the groin when the thighs fold past about 90° (`stills/ryo-tripo/clips/ryo-knockdown_f089_tripo_pelvis.png`). The get-up reads clean at full body (`ryo-get-up_f030_tripo_side.png`).
- **No candy-wrapping**: forearm and hand tear counts stay in single digits on every clip (no twist bones on either rig). **No broken poses** in the stills.
- **One clip, one retarget fault:** at the jump landing (`ryo-jump_f076_tripo_pelvis.png`) the left hand sinks into the thigh and sash, because of the wider Tripo hips. It's a stopgap issue; the real Tripo/mocap clips replace it.

## G4 (retarget gate) status
See `assets/specs/clips/retarget_gate.md`, section YOK-146. **Recommendation: GO for the test branch on the stopgap clips; the formal G4 for the Tripo set stays OPEN** until the Tripo exports and mocap takes exist.

## Open for the designer
1. **Triangle budget:** 53,785 triangles against the 20k budget in `characters/ryo-regen.md`. Not decimated. Recommendation: decimate by script to about 20k (Blender collapse on the body, keeping the face, hands and UV seams) in a follow-up ticket, before the Tripo clips are measured on it. Re-run `canon_tripo_rig.py` and the `ryo-tripo` repair first, so the weights carry over through the decimation. Alternatively accept 54k: two fighters on screen is about 108k triangles. Your call.
2. **Deep-crouch volume flag** (charm and pouch painted on the left hip): accept it as known, as on the Meshy Ryo, or ask Asset Smith for a Tripo take with the charm and pouch as separate meshes, which can then be bound rigid to `Hips`.
3. **Groin pleat in the floor clips** (knockdown, get-up, jump, block-low): it stays on the stopgap clips. Recheck it on the real Tripo/mocap clips before G4; if it persists, a knee/groin repair step (`repair_weights.py` steps C and K, already scripted for Oni and Kappa) is the next free fix.
4. **Raw `Ryo.glb` is not committed** (9.8 MB, untracked in the main checkout, and Godot would import it broken). Keep it outside `game/` or commit it under a `.gdignore`d folder for reproducibility: your call.
5. **Expect the same two faults** (no joint transforms, IBMs in a Z-up frame) on Kitsune, Oni and Kappa from Tripo (YOK-147/148/149) and on Tripo clip exports. `canon_tripo_rig.py` handles rigs. Tripo clip exports have not been seen yet, so the first one needs a check.
