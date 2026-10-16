# Oni rig

Ticket: YOK-112 ("Rig trial on Oni: generator auto-rig vs AccuRIG 2"), following YOK-102. Owner: rig-specialist. Rules: F5, F6, G4, T0, E15, AM5.
Model spec and acceptance: `assets/specs/oni.md` (asset-smith, O3 and O5 ACCEPT). Shared skeleton: `assets/specs/rigs/skeleton.md`.

**Verdict: ACCEPT (rig and weights, 2026-10-16).** The club is the open item: **G4 NO-GO for the club in the shared walk and the heavy's recovery** (see "G4 gate" below).

## Status 2026-10-16 (repair, props, scene, G4)
This section supersedes the take-1 verdict and the work list further down.

**Build chain.** All free and scripted. `oni-rigged.glb` is now the repaired 2.2 m game file, rebuilt from the raw take-1 rig (at `d7b0999`) by:
1. `scale_rig.py --factor 1.1`;
2. `repair_weights.py --fighter oni`.

Two runs give the same bytes, and the Ryo and Kitsune repair outputs are unchanged (`38b14f62…`, `0375fefb…`). The old `oni-rigged.glb (2.2 m)` has been removed. Logs: `logs/oni-scale-220.json`, `logs/oni-repair.json`, `logs/oni-rig-check.json`.

**Repair (R4).** Ryo's pelvis rules made the Oni worse, so they are off for him. The `oni` entry instead:
- makes the whole torso between the seat and the armpits follow the trunk by height (new step T6, full depth);
- fades the leg weight out over 1.04-1.42 m (T2 over the whole torso);
- drops the armpit line 1.6 m per metre past |x| 0.12 m.

The baggy trousers are cloth (`cloth/oni.md`, z 0.62-1.10).

| Check | Raw take 1 | Repaired |
|---|---|---|
| skeleton, rest, scale 2.2 m, axes, origin | PASS | PASS |
| weights | WARN, 189 arm-and-leg bleed | **PASS**, 0 bleed, 0 cross-side |
| kick / crouch / arm raise / twist / grab | FAIL 81 / FAIL 158 / WARN 11 / WARN 9 / WARN 8 | **WARN 12 / WARN 24 / WARN 6 / PASS 0 / WARN 8** (limit 63, no collapse) |

Stills: `stills/oni-repaired-{rest,high-kick,deep-crouch,arm-raise,torso-twist,grab}-{front,side}.png`, `stills/oni-cloth-mask-{front,side}.png`.

**Club (designer: always in hand).**
- `prep_prop.py --mode club --axis-bones RightForeArm,RightHand --tilt-deg 75 --tilt-toward 0,0,1`. The club runs on from the forearm, tilted 75 degrees forward, so with the arm down it points ahead at hip height. Before, it stuck out flat above his head.
- 1.20 m in rig space, 1.32 m on screen, grip at the hand centre. Log: `logs/oni-club-grip.json`.
- 0 club verts go into the body in any stress pose or arms-down. Its lowest point is 0.73 m up with the arms down. Clearance log: `logs/oni-club-clearance.json`.
- Stills: `stills/oni-club-{rest,high-kick,deep-crouch,arm-raise,torso-twist,grab,arms-down}-{front,side,back}.png`.

**Scene.** `game/scenes/fighters/oni.tscn`, same structure as `kitsune.tscn`:
- `FighterModel` with Fighter `oni`, and the rig as `Model`;
- `Club`, a BoneAttachment3D on `RightHand` with the external skeleton → `ClubPivot`, transform from the log → `club-grip.glb`.

Godot 4 headless loads it with 24 bones and the club in the hand. **Not yet wired into the game:** `FighterAnimSet` has no `Oni` entry and `For()` only knows `kitsune`. That is a C# change for gameplay-programmer.

**G4 gate** (full report in `assets/specs/clips/retarget_gate.md`, run on a throwaway merge with YOK-130):
- Body: **GO**. Skin worst failing edges per frame are 9 / 24 / 45 (walk / heavy / throw).
- Feet: with the hip fix (scale 1.188) they sit 1.6-4.9 cm below his rest contact.
- Club: **NO-GO** in the walk (back-swing through the near shin, up to 15 cm) and the heavy's recovery (ticks 55-61, through the back of the head). The throw and the heavy's hit frames are clean.
- Reach: the heavy reaches 0.57 m forward at 1.65 m, against Ryo's 0.40 m at 1.29 m. The throw push lands at 1.41 m.
## Designer decisions (2026-10-16, relayed by the main session)
- **Rigger:** the YOK-112 trial went with **Meshy** ("try meshy"). The designer approved R1 directly to the main session, which ran it. AccuRIG 2 was not run and stays the fallback if the Meshy rig cannot be repaired to WARN.
- **Size: 2.2 m.** A uniform x1.1 on the 2.0 m rig, by script, with no re-rig and no credits (below).
- **Club: always in hand,** on a `RightHand` BoneAttachment3D in every clip. No stow or idle-on-back variant.
- **Human guise (G1): not used.** If G4 fails, the fixes are weight repair, a simpler part, or AccuRIG 2.

## Rig (take 1)
| Field | Value |
|---|---|
| Rigger | Meshy auto-rig (`meshy_rig`, `height_meters: 2.0`), input O3 `01a144a2-bfe8-72a2-a861-d9eb1b3979dc` |
| Rig task id | `01a144ad-b98d-7520-8376-c776c414a476` (SUCCEEDED, 5 credits, run by the main session) |
| Raw file | `game/assets/generated/characters/oni/oni-rigged.glb`, 2.0 m, sha256 `e504c26ba236…`. Free walk and run: `oni-walk.glb`, `oni-run.glb` |
| Game file | `game/assets/generated/characters/oni/oni-rigged.glb (2.2 m)`: the raw rig with the `Armature` node scale 0.01 → 0.011 (`tools/blender/scale_rig.py --factor 1.1`). JSON chunk only; mesh, skin and texture are byte-identical. Log: `assets/specs/rigs/logs/oni-scale-220.json` |
| Import | `godot --headless --path game --import` runs clean for every Oni file |
| Skeleton | The shared 24 bones: names, parents and skin order identical (`rig_check` skeleton PASS), no bone map. Godot sees 24 bones |
| Height, frame | 2.200 m after scaling (`rig_check --height 2.2` PASS), Y-up, +Z facing, feet at 0, feet centre 0.3 / 6.3 cm off the origin. Bones stay in cm with their own lengths: `Hips` rest Y is 112.8 cm in bone space (Ryo 94.9), which is 1.24 m in the world |
| Rest | T-pose, rest == bind. Arms 8.9 / 9.8 deg below level; legs 15.0 / 12.3 deg apart (limit 15) |
| Mesh | `char1`, 18,330 verts, one material |
| Weights | Max 4 influences, all normalised, 0 unweighted, 0 cross-side. **189 verts with arm-and-leg bleed** at z 1.32-1.41 m (2.0 m frame), where the back of the mane and the upper back carry `UpLeg` and arm weight together (WARN) |
| Fingers | Five modelled fingers and no finger bones: they ride `LeftHand` / `RightHand` rigid (E15) |

## Input
| Field | Value |
|---|---|
| Body | `game/assets/generated/characters/oni/oni-mesh.glb`, O3 task `01a144a2-bfe8-72a2-a861-d9eb1b3979dc`. 2.000 m, arm span 1.91 m, 15,623 tris, one 2K texture |
| Club | `game/assets/generated/characters/oni/club.glb`, O5 task `01a144a2-cb1d-7742-b9e3-e01c176e257a`. 1.90 m long and upright, origin at its centre. Head at the top (radius 0.17 m), handle below (0.03-0.06 m), knob at the bottom |

Measured on the mesh (Blender axes, +Y = behind, 2.0 m frame):
- The mane fills the neck and upper shoulders, 0.23-0.29 m behind the centre line at z 1.60-1.90.
- The trousers are baggy from z 0.55 to 1.00, 0.84 m across both legs at z 0.70. The legs share volume up to the crotch.

## Check run (2026-10-16, raw take 1)
Command: `Blender -b --python tools/blender/rig_check.py -- --rig <rig> --skeleton assets/specs/rigs/skeleton.md --height <2.0|2.2> --out <dir>`.
- The 2.0 m raw rig and the 2.2 m game file give identical pose figures, as a uniform scale should.
- There is no `--cloth` file yet, so the trouser folds are checked as body.
- An edge fails above 3x stretch or below 0.25x squash. A pose FAILs above 63 failing edges (0.2% of 31,639) or on a collapsed bone.

| Check | Result |
|---|---|
| skeleton, rest pose, scale, up axis, origin | PASS |
| weights | WARN: 189 verts with arm-and-leg bleed (mane back, upper back) |
| high kick | **FAIL**: 81 failing edges, max 7.08x. `RightUpLeg` 34, `LeftUpLeg` 18, `Spine02` 9, mixed 101. No collapse |
| deep crouch | **FAIL**: 158, max 8.98x. `Spine02` 45, `UpLeg` 24, `Leg` 11, mixed 236. No collapse |
| arm raise | WARN: 11, max 5.03x |
| torso twist | WARN: 9, max 4.04x |
| grab | WARN: 8, max 3.38x (shoulders and arms) |

Baseline: raw Ryo had crouch FAIL at 68 failing edges and the kick at 35. Raw Kitsune had crouch 324, kick 115, arm raise 146 and grab 120. Both reached WARN with the free `repair_weights.py`.

What the stills show:
- **Crouch:** the back and seat skin is dragged down into a sheet behind the trousers. The trouser fronts balloon round the knees.
- **Kick:** the crease tears at the crotch and seat on the kicking side.
- **Mane:** it stays with the head in the arm raise and the grab. Unlike Kitsune's hair, it is not hung on the arms.
- **Shoulders, fingers, horns:** the shoulders hold, and the fingers and horns stay rigid.

**R4, the proposed repair (free, needs the designer's go-ahead):**
- A `FIGHTERS["oni"]` entry in `repair_weights.py`.
- Ryo's pelvis steps 1-3 (smooth, ±5 cm side split, UpLeg falloff), with the band scaled to the Oni's hips (`Hips` 1.128 m in the 2.0 m frame).
- Kitsune's T2 leg strip and T4 bleed steps, for the 189 mane and back verts.
- `cloth/oni.md` for the trouser folds (`UpLeg`, `min_radius_m` about 0.14, z 0.55-0.95).
- Target: WARN on every pose.
- Order of the build: raw → `repair_weights.py` → `scale_rig.py --factor 1.1` → `oni-rigged.glb (2.2 m)`.

Stills, raw take 1: `assets/specs/rigs/stills/oni-raw-{rest,high-kick,deep-crouch,arm-raise,torso-twist,grab}-{front,side}.png`.

## Club (done, 2026-10-16)
**Prep.** `tools/blender/prep_prop.py --mode club --length 1.2 --grip-frac 0.13 --axis 0,0,1 --rig oni-rigged.glb --bone RightHand` writes `game/assets/generated/characters/oni/club-grip.glb` (sha256 `195c41710fd2…`; log `assets/specs/rigs/logs/oni-club-grip.json`).
- Scale 0.632: from 1.90 m to 1.20 m in rig space.
- The head end is picked by its outer radius (0.17 against 0.08 m at the knob).
- The pivot is the grip, 0.156 m above the knob end.
- Handle to head points forward (glTF +Z). With the arm down, the club points ahead out of the fist.

**Mount (for the fighter scene, owned by gameplay-programmer).** `BoneAttachment3D` on `RightHand` → child `Node3D` → `club-grip.glb`, with this child transform (from the 2.0 m rest):
```
Transform3D(-19.636593, 82.52919, 52.946537, -96.107491, -5.496515, -27.076351, -19.435675, -56.202465, 80.396095, 2.943158, 13.373433, 3.974909)
```
- The grip sits at the hand centre: the mean of the 583 verts weighted mostly to `RightHand`.
- At rest, the club is upright in the world.
- Do not recompute the transform on the scaled file. It lives in bone space, so the 1.1 on `Armature` scales the club with him.

**Checked in Godot 4** (headless, `oni-rigged.glb (2.2 m)`): the club's global basis is 1.1 x identity, its grip is at (-0.949, 1.668, -0.010) m, and it is **1.32 m** long.

**Clearance.** `tools/blender/prop_clearance.py --ignore-radius 0.12` ran the five stress poses plus arms-down. **0 club verts inside the body in every pose.** Stills: `assets/specs/rigs/stills/oni-club-{rest-front,arms-down-side,high-kick-side,grab-front,deep-crouch-front,torso-twist-front}.png`.
- The hand is an open mitt with no finger bones (E15), so the handle passes through open fingers rather than a fist. This reads as holding at 720p, and it is the accepted rig limit.

## Attachment bones
- Club on `RightHand`.
- Hit sparks on `Spine01`.
- Head VFX on `Head` / `headfront`.
- Foot contact on `LeftToeBase` / `RightToeBase`.

No new bones.

## G4 gate plan (not run; the rig is not ready yet)
- **Clips:** `characters/ryo/ryo-walk.glb`, `clips/ryo-heavy.glb` (preset 194) and `clips/ryo-grab-2.glb` (preset 259, head-trimmed from tick 30, E15).
- **Tools:** `skin_clip_check.py` with `--rig oni=oni-rigged.glb (2.2 m) --rig ryo=...`, plus `prop_clearance.py` with the club. Stills go to `assets/specs/clips/stills/retarget-gate/oni-*.png`, and the verdict to `retarget_gate.md`.
- **Hip height: a code fix, needed before G4.**
  - `FighterModel.BuildLibrary` (`game/scripts/Fight/Presentation/FighterModel.cs`) drops per-bone translation, so the bones keep their lengths, and pins `Hips` X/Z. It **keeps the clip's `Hips` Y as it is**: Ryo's 94.9 cm in bone space.
  - The Oni's rest is 112.8 cm in bone space, so he sinks 17.9 cm in bone units, **about 20 cm on screen at 2.2 m**, with his feet through the floor.
  - Fix: scale the `Hips` Y keys by `rest.Y / clipRest.Y` = 112.8 / 94.9 = **1.19**. The 1.1 rides on the `Armature` node, so it must not be applied twice.
  - This belongs to gameplay-programmer and the designer's code review. It applies to the Kappa too.
- **Reach.** The clips are rotations, so the Oni's hands land in proportion to his size: about **26% further and higher** than Ryo's (2.2 / 1.75). On the heavy and the throw, measure the hand, and the club tip, at contact against a Ryo-sized target. The throw push should meet the chest, not the head. Any gap is fixed in move data (range, the throw's step, hitboxes, E15), not on the mesh.
- **Bulk and club.** Arms against the torso in the throw and the guard, and the club against the head, horns and thigh at the hit frames. Record the overlap.
- **NO-GO fallback** (designer's choice): weight repair, a simpler part (a slimmer mane, shorter trousers), or AccuRIG 2. The human guise is out.

## AccuRIG 2 (fallback; not run)
1. A scripted export of `oni-mesh.glb` to FBX: T-pose, Y-up, metres, texture embedded.
2. Rig Body markers:
   - chin below the beard;
   - wrists and elbows at the joints;
   - knees at the knee, not at the trouser cuff;
   - crotch at the real crotch, about 0.55-0.60 m.
3. Rig Hand: 5 fingers.
4. Preview an arm raise and a wide stance.
5. Export an FBX for Blender with CC3/CC4 names, T-pose bind and no motion.
6. Bone map onto the shared skeleton:
   - spine by chain position (`Waist` / `Spine01` / `Spine02` → `Spine02` / `Spine01` / `Spine`);
   - twist bones merged into their parents;
   - fingers held at rest;
   - `head_end` and `headfront` added by script;
   - the rest re-posed to the T-pose.

   The mapping script `bone_map_accurig.py` is still to write. Cost 0; the designer runs the app.

## Work list
| # | Item | Tool | Credits | State |
|---|---|---|---|---|
| R1 | Meshy auto-rig, 2.0 m | `meshy_rig` | 5 | **Done** (main session) |
| R2 | AccuRIG 2 | desktop | 0 | Fallback, not run |
| R3 | Club rescale, re-pivot, mount transform, clearance | `prep_prop.py`, `prop_clearance.py` | 0 | **Done** |
| R4 | Weight repair to WARN | `repair_weights.py --fighter oni` (new entry), `cloth/oni.md` | 0 | **Proposed, needs go-ahead** |
| R5 | Scale to 2.2 m | `scale_rig.py --factor 1.1` | 0 | **Done** on the raw rig; rerun after R4 |
| R6 | `Hips` Y scaling in `FighterModel.BuildLibrary` | code (gameplay-programmer) | 0 | Needed before G4 |
| R7 | Fighter scene: model, club mount | `oni.tscn` (gameplay-programmer) | 0 | After R4 |
| R8 | G4 gate | `skin_clip_check.py`, `prop_clearance.py` | 0 | After R4 and R6 |

Spent on the Oni rig: 5 credits (R1). Worst case if R4 fails and Meshy is retried: 2 more takes, 10 credits. Then AccuRIG 2 (free).

## Takes
| # | Date | Job | Task id | Credits |
|---|---|---|---|---|
| 1 | 2026-10-16 | `meshy_rig`, 2.0 m (run by the main session on the designer's approval) | `01a144ad-b98d-7520-8376-c776c414a476` | 5 |
