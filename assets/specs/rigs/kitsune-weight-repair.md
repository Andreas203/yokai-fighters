# Kitsune weight repair (free, scripted)

Ticket: YOK-117. Owner: rig-specialist. Rules: F5, F6, G4, T0, AM5. Follows the cloth exemption (YOK-110, `assets/specs/rigs/cloth/kitsune.md`) and Ryo's pelvis repair (YOK-116, `ryo-weight-repair.md`).
The designer approved a free repair. **No Meshy, Tripo or AccuRIG job was run. 0 credits spent. `game/` is untouched.**

**Verdict: ACCEPT (all five stress poses reach WARN, weights PASS). Swapping the repaired rig into the game is the designer's call, and it has not been done.**

## The fault
- Meshy's auto-skin hangs Kitsune's long back hair on `LeftArm` **and** `RightArm`. The hair is one sheet from the head down to the waist, 0.15 to 0.21 m behind the spine.
- The hair tips and the back of the coat also carry `Hips` plus both `UpLeg` bones.
- So the hair crumpled or lifted whenever an arm or leg moved:
  - The weights check counted 886 verts with arm-and-leg bleed.
  - The waist tore in the kick, crouch, arm raise and grab.
  - `Hips` owned only 68 verts, and those collapsed: volume 0.515 in the crouch, 0.698 in the kick.
- The sash bell carried about 0.25 of each `UpLeg`. Ryo's midline leg split would have sheared it (the first take below did).

## What was done
Script: `tools/blender/repair_weights.py --fighter kitsune`. It runs headless, logs every step and gives the same result every run.
- **Settings stay per fighter.** `FIGHTERS["kitsune"]` turns on the torso steps T1–T5. `--fighter ryo` is the default and runs Ryo's steps alone. Ryo's output is still byte-identical, sha256 `38b14f62…` before and after this change.
- **Input:** the raw `game/assets/generated/characters/kitsune/kitsune-rigged.glb` (sha256 `fb3bcb8a…`). It is never written.
- **Only the weights change.** The output is a copy where only the `JOINTS_0`/`WEIGHTS_0` bytes differ: all 115,374 changed bytes fall inside those two buffer views. Mesh, UVs, skeleton, inverse binds and the bundled clip are byte-identical.
- **Output:** sha256 `0375fefb…`. Two runs give the same hash.
- **Verts touched:** 7,391 of 17,198. Every other vert keeps its exact original slots.

Axes: Blender world, so +Z is up, the character faces −Y and her left is +X. Depth `y` is measured from the `Hips` head, with + behind her.

| Step | What it does | Settings |
|---|---|---|
| T1 arm strip | In the torso column and the hair layer behind the back, arm weight goes to the spine chain (`Hips`/`Spine02`/`Spine01`/`Spine`/`neck`), split by height between the joint heads. All edges are smoothsteps. | Torso column: \|x\| 0.12–0.18 m, below z 1.00–1.10. Hair layer: y > 0.05–0.09, \|x\| 0.10–0.40, below z 1.24–1.34. 3,315 verts. |
| T2 leg strip | Leg weight in the hair layer above the seat goes to the trunk. | z 0.86–0.93. 1,943 verts. |
| T3 smooth | 60 Laplacian iterations over the T1/T2 verts on [the arm bones, the trunk total]. The trunk total goes back to each vert's own original trunk weights first, and any gain follows the height split. | 6,605 verts in the region |
| 1–3 pelvis | Ryo's steps with Ryo's settings: smooth Hips/UpLeg, split each side of the crotch to its own leg (±5 cm blend), fade UpLeg out above the hip joint. | band z 0.667–1.099 |
| T4 bleed | A vert that still has both arm and leg weight loses the smaller group to the trunk. | 2,337 verts checked |
| T5 rigid | Every bell vert gets the bell's mean weight, so the bell moves as one piece. | \|x\| < 4.5 cm, y < −0.11, z 0.87–0.97. 366 verts. |
| tidy | Drop weights under 0.01, keep at most 4 influences, normalise. | |

Net change, as summed vert weight per bone:

| Bone | Change |
|---|---|
| Hips | +1,830 |
| Spine02 | +425 |
| Spine01 | +367 |
| Spine | +210 |
| LeftArm | −442 |
| RightArm | −356 |
| Both UpLeg bones | about −1,786 |

Takes:
1. **Bell bound to `Hips` alone.** The bell's seam to the hakama tore in the crouch, which went to FAIL with 66 failing edges.
2. **Bell given its own mean weight.** This is the version kept.

Tried in the numpy model of the check (`rig_check`'s skinning, matched to 4e-7 m) and dropped:
- **Narrow hair band (\|x\| < 0.16–0.22).** The hair locks tore at the band edge, and the arm raise went to FAIL with 206 failing edges.
- **Smoothing each trunk column separately.** It spread `Hips` weight up the belly, and the crouch went to FAIL with 64.
- **Removing the far arm's weight past the midline.** No gain, so it was left out.

Reproduce:
```
Blender -b --python tools/blender/repair_weights.py -- --fighter kitsune \
  --in game/assets/generated/characters/kitsune/kitsune-rigged.glb \
  --out <dir>/kitsune-rigged-repaired.glb --log <dir>/repair_log.json
```

## Rig check, before and after
Tools:
- `rig_check.py --cloth` (YOK-110 branch)
- `skeleton.md` (YOK-111), height 1.65 m
- cloth regions `cloth/kitsune.md`

Rules:
- An edge fails when it stretches past 3x or squashes under 0.25x.
- A pose FAILs above 60 failing body edges (0.2% of 30,427) or when a bone collapses.

| Pose | Before | After |
|---|---|---|
| high kick | **FAIL**: 115 failing edges, max 8.36x. `Hips` collapsed (0.698, p5 0.481), `RightUpLeg` collapsed (0.741). | **WARN**: 32, max 7.02x, no collapse |
| deep crouch | **FAIL**: 324, max 11.9x. `Hips` collapsed (0.515, p5 0.168). | **WARN**: 48, max 10.9x, no collapse |
| arm raise | **FAIL**: 146, max 8.34x | **WARN**: 33, max 7.71x |
| torso twist | WARN: 38, max 7.89x | WARN: 19, max 7.89x |
| grab | **FAIL**: 120, max 9.00x | **WARN**: 16, max 4.32x |
| weights | WARN: 886 arm-and-leg bleed | **PASS**: 0 bleed, 0 cross-side, max 4 influences, normalised, 0 seam mismatch |
| skeleton, rest, scale, up axis, origin | PASS | PASS |

Edges over 1.8x (stretched, not torn):

| Pose | Before | After |
|---|---|---|
| kick | 479 | 235 |
| crouch | 791 | 525 |
| arm raise | 525 | 208 |
| twist | 192 | 139 |
| grab | 425 | 148 |

Volume (mean ratio of posed to rest distance from the bone):
- **`Hips`.** It owns 2,941 verts after the repair, against 68 before, so the measured vert set changes. Both measures are given:

  | Vert set | Kick | Crouch |
  |---|---|---|
  | Verts `Hips` owns after the repair (2,941) | 0.986 | 0.931 (p5 0.58) |
  | The original 68 verts | 0.993 | 1.005 (p5 0.95) |

- **`RightUpLeg` in the kick:** 0.795 on the verts it still owns.

Cloth (exempt, info only):
- Hakama tears are unchanged in the kick: 205 before, 206 after.
- Sleeve tears rise: arm raise 12 to 19, grab 1 to 12. This is the sleeve flare, which stays exempt.
- The sleeve verts drop from 1,474 to 1,175. The side hair locks no longer count as sleeve, so they are now checked like the body.

## Kitsune's real clips (`skin_clip_check.py`)
- Each clip's own action plays on both skins and every frame is measured.
- All edges count here, with no cloth exemption, so these numbers are much higher than the rig check's.
- The gate clips are walk, heavy and throw.

| Clip | Worst-frame failing edges | Max stretch | Mean edges over 1.8x |
|---|---|---|---|
| kitsune-hk | 487 → 348 | 13.55 → 13.07 | 707 → 544 |
| kitsune-crouch | 393 → 154 | 11.51 → 12.13 | 1147 → 825 |
| kitsune-throw (gate) | 245 → 171 | 11.68 → 15.19 | 720 → 476 |
| kitsune-heavy (gate) | 239 → 120 | 11.68 → 14.61 | 640 → 535 |
| kitsune-walk (gate) | 157 → 100 | 7.29 → 7.29 | 392 → 205 |
| kitsune-mk | 452 → 306 | 12.09 → 14.52 | 629 → 413 |
| kitsune-mp | 258 → 181 | 14.57 → 12.28 | 770 → 618 |
| kitsune-block-high | 130 → 107 | 11.37 → 11.90 | 559 → 553 |
| kitsune-block-low | 319 → 258 | 12.67 → 15.70 | 874 → 749 |
| kitsune-foxfire | 115 → 67 | 8.09 → 8.65 | 448 → 244 |
| kitsune-getup | 443 → 303 | 12.01 → 24.96 | 1093 → 924 |
| kitsune-guard-walk-back | 135 → 85 | 6.44 → 12.18 | 474 → 306 |
| kitsune-guard-walk-fwd | 164 → 98 | 7.09 → 12.65 | 552 → 327 |
| kitsune-hit-high | 142 → 53 | 8.20 → 11.64 | 337 → 211 |
| kitsune-hit-low | 158 → 60 | 8.85 → 8.56 | 508 → 315 |
| kitsune-idle | 285 → 164 | 8.76 → 8.76 | 943 → 596 |
| kitsune-jump | 326 → 149 | 11.23 → 21.00 | 784 → 470 |
| kitsune-knockdown | 475 → 202 | 14.85 → 14.67 | 762 → 465 |

What the stills show:
- **Better.**
  - Before, the back hair was dragged by the arms: hiked into a crumpled shield in the arm raise and the grab, and pulled under the arms to the chest in the throw push (f101). It was also torn into spikes down the back in the deep crouch.
  - Now it hangs as one sheet from the head and shoulders and stays on her back in every clip.
  - The bell hangs round on the sash in the jump, throw and crouch.
- **Worse in every clip: failing edges go down, but one value goes up.** The single worst edge (max stretch) rises in several clips: get-up 25x, jump 21x, guard walks about 12x.
  - Those edges sit where the side hair locks join the sleeve top at the shoulder. The locks are fused to the sleeve, so they keep their arm weight and the sleeve flare keeps working, while the hair next to them now follows the trunk.
  - In the guard walk and the get-up bow, the locks part from the back sheet there, and a little red sleeve lining shows through in the grab and kick stills.
  - It is a handful of edges, and smaller than the old tears at the waist.
- **Unchanged:**
  - The leg and foot tears (`LeftLeg`/`RightLeg`). They are hakama cloth, outside this repair.
  - The tails. They are nine `tail.glb` copies on a `Hips` attachment, swayed in code, and the `Hips` bone and the attachment are untouched. The hair behind the waist now moves with `Hips`/`Spine02`, so it stays in step with the tail root instead of swinging with the arms.

Stills and reports are kept outside the repo:
- Stress-pose sheets: `/private/tmp/claude-501/-Users-whash-yokai-fighters/da2b5d58-deeb-4eb6-a182-300c22b991dc/scratchpad/kitsune-repair/compare/pose_*.png`. Before is on top, after below. Views are back, three-quarter back, side and front.
- Clip sheets: `…/kitsune-repair/compare/kitsune-*_compare.png`, with before on top, after below, in front, side and pelvis views.
- Rig check: `…/kitsune-repair/before/report.json` and `…/kitsune-repair/after/report.json`, with their stills.
- Clip report: `…/kitsune-repair/clips/clip_report.json`.
- Repaired GLB: `…/kitsune-repair/after/kitsune-rigged-repaired.glb`. Its repair log is next to it.

On acceptance, copy the pose sheets to `assets/specs/rigs/stills/kitsune-repair-*.png`.

## Godot check
The repaired GLB was imported headless in Godot 4.7.2 mono, in a scratch project. It loads with:
- 1 `Skeleton3D` of 24 bones
- 1 skinned `MeshInstance3D` with 17,198 verts
- the bundled animation

## Swap plan (designer's call, not done)
1. Regenerate the GLB with the command above, straight to `game/assets/generated/characters/kitsune/kitsune-rigged.glb`. For a way back, rely on git history, or keep the raw file outside `game/` as `kitsune-rigged.raw.glb`.
2. Keep the filename and the `.import` file, with uid `uid://ck00c37ebhrtl`. Then neither `game/scenes/fighters/kitsune.tscn` nor `FighterAnimSet.cs` needs an edit.
3. Clips need no rework. The skeleton, rest pose and bone heads are identical, so the `data/clips/kitsune-*.json` matches, the frame data and the hurtboxes stand. The clip GLBs carry their own copy of the old skin, but the game draws Kitsune from `kitsune-rigged.glb`.
4. Re-check once in engine:
   - Capture the crouch, high kick, throw and guard walk for `docs/screenshots/current/`. Look at the tails against the back hair, and at the side locks at the shoulder.
   - Run the full game suite.
5. Re-run G4 for Kitsune (walk, heavy, throw) in `assets/specs/clips/retarget-gate.md` on the repaired rig. The skin numbers above already cover those three clips.

## Open points for the designer
- Approve or decline the swap into `game/`.
- The shoulder seam: the side hair locks stay on the arms because they are fused to the sleeve top. Two options, both new work:
  - **Free:** a later script pass could feather the locks' arm weight down their length.
  - **New spend:** a regeneration with the hair modelled apart from the sleeves would remove the seam. That needs its own spec and approval.
- The crouch has the least margin: 48 failing edges against the limit of 60.
- The `Hips` volume metric depends on which verts `Hips` owns, the same caveat as Ryo's. Both measures are given above.
