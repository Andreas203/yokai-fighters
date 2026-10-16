# Oni rig

Ticket: YOK-112 ("Rig trial on Oni: generator auto-rig vs AccuRIG 2"), following YOK-102. Owner: rig-specialist. Rules: F5, F6, G4, T0, E15, AM5.
Model spec and acceptance: `assets/specs/oni.md` (asset-smith, O3 and O5 ACCEPT). Shared skeleton: `assets/specs/rigs/skeleton.md`.

**Status: SPEC, awaiting the designer's approval. No rig job has been run and no credits have been spent.** Meshy balance was 707 on 2026-10-16 (`meshy_check_balance`). The O3 task `01a144a2-bfe8-72a2-a861-d9eb1b3979dc` is SUCCEEDED (multi-image-to-3d, textured), so `meshy_rig` can use it as `input_task_id`.

Note, 2026-10-16: the coordinating agent relayed "try meshy" from the designer, as approval for R1 and K-R1 (10 credits). An agent's message is not the designer's approval under the crew rules, so nothing was run. The designer can confirm directly, and then R1 runs as written below.

## Input
| Field | Value |
|---|---|
| Body | `game/assets/generated/characters/oni/oni-mesh.glb`, O3 task `01a144a2-bfe8-72a2-a861-d9eb1b3979dc` |
| Size | 2.000 m tall, arm span 1.91 m (T-pose, arms level at about 1.50 m), Y-up, feet at 0, centred. 15,623 tris, one 2K texture |
| Hands | Five separate open fingers (no finger bones on the shared skeleton: they ride the hand bone as a rigid mitt, E15) |
| Club | `game/assets/generated/characters/oni/club.glb`, O5 task `01a144a2-cb1d-7742-b9e3-e01c176e257a`. 1.90 m long along Z, origin at its centre, head at +Z (radius up to 0.17 m), handle at -Z (radius 0.03-0.06 m), knob at -0.86 m |

Measured on the mesh (Blender read-only import, 2026-10-16; Blender axes, +Y = behind):
- The mane fills the neck and upper shoulders: at z 1.60-1.90 it runs 0.23-0.29 m behind the centre line, against 0.17-0.22 m for the chest and back below. It is fused to the head and the tops of the shoulders.
- The trousers are baggy from z 0.55 to 1.00: 0.84 m wide across both legs at z 0.70, front to back -0.29 to +0.24 m. The left and right legs share volume up to the crotch (asset-smith: no gap for a clean split).

## Rig job options
| Option | Rigger | Settings | Cost | Who runs it |
|---|---|---|---|---|
| A | Meshy auto-rig (the generator that made the model) | `meshy_rig`, `input_task_id: 01a144a2-bfe8-72a2-a861-d9eb1b3979dc`, `height_meters: 2.0` | 5 credits (includes the free walk and run GLBs) | rig-specialist, after `meshy_check_balance` |
| B | AccuRIG 2 (desktop, free) | Settings below | 0 credits, about 30-45 min of designer time | Designer runs it and puts the FBX in `game/assets/generated/characters/oni/` |

Tripo's auto-rig is not an option here: the model came from Meshy, so the generator's own rig is Meshy's.

### Option B: AccuRIG 2 settings
1. **Input.** AccuRIG takes FBX or OBJ. A scripted step exports `oni-mesh.glb` to `oni-mesh-accurig.fbx`, with no change to the geometry: T-pose, Y-up, metres, base-colour texture embedded. That step is the first part of `tools/blender/bone_map_accurig.py` (to write; see the bone map below). The designer opens the FBX in AccuRIG 2.
2. **Rig Body.** Use the auto marker placement, then check:
   - Chin marker at the jaw line, below the beard and tusks. If the mane pulls it too high, the mane gets neck weight.
   - Wrist and elbow markers at the joints, not at the wrap edges.
   - Knee markers at the knee, not at the trouser cuff: the cuff sits about 0.05 m above it.
   - Crotch marker at the real crotch inside the trousers, about 0.55-0.60 m up. AccuRIG tends to place it at the trouser hem.
3. **Rig Hand.** 5 fingers, auto markers. The finger bones stay at rest after the bone map (rule 4 in `skeleton.md`).
4. **Pose check.** In AccuRIG's preview, try a high arm raise and a wide stance. Redo the markers if the mane follows the arm or the trousers tear at the crotch.
5. **Export.** Export > FBX, target **Blender**, skeleton **CC3/CC4 standard** (`CC_Base_` names), **T-pose** as the bind pose, mesh and skin on, no motion, embed textures. File name: `oni-accurig.fbx`.

### Recommended order for the YOK-112 trial
1. **A first** (5 credits). It is already the shared skeleton, so it needs no bone map and the gate clips play on it unchanged.
2. **B on the same Oni mesh** (free).
3. **Both through the same checks.** Run `rig_check.py` (B after the bone map) and `skin_clip_check.py --rig meshy=... --rig accurig=...` on the walk, heavy and throw clips.
4. **The winner** is the rig with fewer failing edges and no collapse at the mane, shoulders, crotch and wrists. A tie goes to Meshy, because it needs no bone map and no twist-bone merge.
5. **The Kappa uses only the winner** (see `rigs/kappa.md`).
6. The verdict and the reason are written under "Trial result" below, so the next fighter uses one rigger.

## Extra parts
| Part | Treatment |
|---|---|
| Mane | Baked; weighted to `Head` / `neck` / `Spine` |
| Horns, tusks, beard | Baked to `Head`. Rigid by weight (check that `Head` is near 1.0 on the horns) |
| Trousers (baggy) | Baked into the body skin. A cloth region file `assets/specs/rigs/cloth/oni.md` (same format as `cloth/kitsune.md`) exempts the outer folds from the tear check, with `UpLeg`, `min_radius_m` about 0.14, z 0.55-0.95. The crotch and waist band stay checked |
| Rope belt, forearm and shin wraps | Baked, no sway |
| Kanabo club | **Separate rigid mesh** on a hand attachment (plan below). No extra bones |

No `X_` extra bones are needed.

## Club attachment plan
1. **Rescale and re-pivot by script.** `tools/blender/prep_prop.py` (to write; prop prep, a mesh edit, not animation):
   - Import `club.glb` and scale it uniformly by 1.2 / 1.90 = **0.632**, which gives 1.20 m long and a head radius of about 0.11 m.
   - Find the grip. It is the thinnest section of the lower half, measured as a radius profile along Z (handle 0.03-0.05 m before scaling). Put the origin at the grip centre, about 0.12 m above the knob after scaling.
   - Point the club along +Y from the grip, so its local up is the club axis.
   - Write `club-grip.glb`. The original file is never written.
   - Log the scale, the grip offset and the sha256.
2. **Mount in Godot.** Add a `BoneAttachment3D` "Club" on `RightHand` in `game/scenes/fighters/oni.tscn`. Its child `ClubPivot` has scale 100, because the bone space is in cm, as on the Kitsune tails. The grip offset in hand space comes from the rig: the palm centre is about 8-10 cm past the `RightHand` head along the hand, rotated so the club stands up out of the fist. The offset is data on the node, read from the rig in Blender (the hand's mean vert position), never hand-tuned per clip.
3. **Clearance.** Check the club in the stress poses and gate clips: no passing through the head, horns or thigh in the heavy and throw.
4. **Visibility: not decided here.** The shared clips are bare-handed, and an open-hand push (E15 throw) holding a club reads badly. The options:
   - (a) always visible;
   - (b) visible only in the club moves (Iron-Club Charge and its normals), with a show/hide flag in move data stepped on the tick;
   - (c) on the back (a `BoneAttachment3D` on `Spine`) when idle, in the hand in club moves.

   This is **the animator's or the designer's call**. The rig works for all three.

## Attachment bones
- Club on `RightHand`.
- Hit sparks on `Spine01`.
- Head VFX on `Head` / `headfront`.
- Foot contact on `LeftToeBase` / `RightToeBase`.

No new bones.

## Blender checks after rigging
Run them on the raw rig GLB first. A repaired copy is only made if a check fails (weights only, as `repair_weights.py` does).
1. **`rig_check.py`**: `Blender -b --python tools/blender/rig_check.py -- --rig game/assets/generated/characters/oni/oni-rigged.glb --skeleton assets/specs/rigs/skeleton.md --height 2.0 --cloth assets/specs/rigs/cloth/oni.md --out <scratch>/oni-meshy`. Add `--bone-map accurig` for option B. It checks:
   - names, parents and skin order against the 24 shared bones;
   - T-pose rest, Y-up, +Z facing, origin at the feet;
   - at most 4 influences, normalised weights, no verts weighted to the wrong side;
   - the five stress poses: high kick, deep crouch, full arm raise, torso twist, grab.
2. **Watch-list**, judged from the stills:
   - **Mane**: must not follow `LeftArm` / `RightArm` in the arm raise. Kitsune's Meshy skin hung her hair on both arms (`kitsune-weight-repair.md`), and the same fault is likely here, because the mane rests on the shoulders. Fix: the Kitsune T1 arm strip, as a new `FIGHTERS["oni"]` entry in `repair_weights.py`.
   - **Trousers and thighs**: crotch tear in the high kick and deep crouch. The legs share volume, so expect the crossed-side bleed Ryo had (YOK-116). Fix: Ryo's pelvis steps 1-3 (smooth, ±5 cm side split, UpLeg falloff), with the band scaled to the Oni's hips.
   - **Shoulders**: very big deltoids and pecs. Check for collapse in the arm raise and the grab, through the `LeftArm` / `RightArm` volume ratio.
   - **Wrists and fingers**: fingers rigid on the hand and no candy-wrap in the torso twist or the grab. The Meshy rig has no twist bones. On option B, the CC twist bones are merged into their parents (bone map rule 3).
   - **Horns and beard**: rigid on `Head`, with no `neck` stretch in the high kick.
3. **Stills**: `assets/specs/rigs/stills/oni-{rest,high-kick,deep-crouch,arm-raise,torso-twist,grab}-{front,side}.png`. For the trial, prefix the files `oni-meshy-` or `oni-accurig-`.
4. **Export check**: the glTF imports into Godot 4 with `Model/Armature/Skeleton3D` and the 24 bones intact.

## Bone map
- **Option A (Meshy):** the shared skeleton itself. No map: the 24 names, order and parents are expected to be identical, and `rig_check.py` verifies them.
- **Option B (AccuRIG 2):** fill `### Bone map: AccuRIG 2 (oni)` in `skeleton.md` from the first file, starting from the CC column already there:
  - `CC_Base_Hip` → `Hips`, with `CC_Base_Pelvis` kept at rest.
  - `Waist` / `Spine01` / `Spine02` → `Spine02` / `Spine01` / `Spine` (the spine-order trap).
  - `NeckTwist01` → `neck`, and `Head` → `Head`.
  - Clavicle, upper arm, forearm and hand map to the matching arm bones.
  - Thigh, calf, foot and toe base map to the matching leg bones.
  - Twist bones are merged into their parent by script.
  - Fingers stay at rest.
  - `head_end` and `headfront` are added by script, at the skull top and 0.15 m in front of `Head`.
  - The CC root is removed.
  - The A-pose or roll delta is re-posed to the shared T-pose and the rest is re-applied by script, so the delta is zero.

  The script is `tools/blender/bone_map_accurig.py` (to write). It must play the walk, heavy and throw clips before any new clip is made (AM5).

## G4 gate plan (after the rig passes; not run)
- **Clips:** `characters/ryo/ryo-walk.glb`, `clips/ryo-heavy.glb` (preset 194) and `clips/ryo-grab-2.glb` (preset 259, head-trimmed from tick 30, E15).
- **Tools:** `skin_clip_check.py` with `--rig oni=... --rig ryo=...`, plus stills at the hit frames into `assets/specs/clips/stills/retarget-gate/oni-*.png`. The verdict goes in `assets/specs/clips/retarget_gate.md`.
- **Height:** the clips were authored on 1.65-1.75 m rigs, and the Oni is 2.0 m (x1.14 Ryo):
  - **Reach.** Rotations carry across proportions, so the Oni's hands land about 14% further and higher. On the heavy and the throw, measure where the hand ends up against a Ryo-sized target. The throw push should meet the chest, not the head.
  - **Hips height.** If the clip keeps a `Hips` translation track in absolute cm, ground contact needs checking. Ryo's hips sit at about 0.95 m, and the Oni's will be about 1.05-1.10 m, so an absolute track sinks his feet about 0.1-0.15 m into the floor. Check the toe height against Y=0 in each clip (`measure_clip.py` planted feet). The fix is scaling the `Hips` position track by the hip-height ratio at import or in clip data, not a mesh change. This never showed up before, because Ryo's and Kitsune's hips differ by only 1 cm.
  - **Bulk.** Arms against the torso in the throw and the guard. Record the overlap at the hit frames.
- **NO-GO fallback** (designer's choice): an extra bone or a slimmer mane retake, or the folklore human guise (G1, 48 credits per take, in `yok-102-job-list.md`).

## Approval table (designer)
| # | Job | Tool | Settings | Credits | Who runs it |
|---|---|---|---|---|---|
| R1 | Oni auto-rig, trial arm A | `meshy_rig` | `input_task_id 01a144a2-bfe8-72a2-a861-d9eb1b3979dc`, `height_meters 2.0` | 5 | rig-specialist |
| R2 | Oni auto-rig, trial arm B | AccuRIG 2 desktop | Settings above; FBX in from the scripted export, CC FBX out | 0 | designer |
| R3 | Club rescale and re-pivot | Blender script `prep_prop.py` | scale 0.632, origin at the grip | 0 | rig-specialist |
| R4 | Weight repair, if a check fails | `repair_weights.py --fighter oni` | mane arm strip, pelvis steps | 0 | rig-specialist |

- **First pass: 5 credits.**
- **Worst case, 3 takes: 15 credits** (R1 x3; AccuRIG and the scripts are free).
- A Meshy retake changes its input, not just a re-roll: for example `model_url` set to a scripted copy with the mane thinned off the shoulders, still 5 credits.
- After 3 failed takes: the other rigger, a shorter mane (asset-smith retake), or the human guise.

## Trial result
Pending: neither rig has been run.

## Check run
Pending.

## Takes
| # | Date | Job | Task id | Credits |
|---|---|---|---|---|
| - | - | none run | - | 0 |
