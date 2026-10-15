# Ryo weight repair (free, scripted)

Ticket: YOK-116. Owner: rig-specialist. Rules: F5, F6, G4, T0, AM5. Diagnosis: `assets/specs/ryo-regen.md` §1–2.
Designer's choice: try a free Blender weight repair on the current rig before any regeneration spend. **No Meshy, Tripo or AccuRIG job was run. 0 credits spent.**

**Verdict: ACCEPT (repair reaches WARN). Swapping it into the game is the designer's call, and it has not been done.** The regeneration in `ryo-regen.md` stays available if the designer wants the outfit fixed as well.

## What was done
- Script: `tools/blender/repair_weights.py`. It runs headless and logs every step. Input is the raw `game/assets/generated/characters/ryo/ryo-rigged.glb` (rig task `01a10e47-857e-73a0-9f7a-cd74795198fb`, sha256 `6cd871a4…`), which is never written.
- It changes only the `JOINTS_0`/`WEIGHTS_0` bytes in a copy of the GLB. Mesh, UVs, normals, texture, skeleton, skin order, inverse bind matrices and the bundled clip are byte-identical: all 48,045 changed bytes fall inside those two buffer views.
- It only works in the pelvis band, from z 0.668 to 1.089 m (3,273 of 17,714 verts). Every other vert keeps its exact original slots.
- Steps:
  1. Weld the UV-seam duplicates.
  2. Laplacian-smooth the Hips/UpLeg columns for 20 iterations, keeping their per-vert total.
  3. Separate the two sides, so each half of the fused crotch follows its own leg. The two sides blend over ±5 cm at the midline.
  4. Fade UpLeg weight out above the hip joint and give it to Hips. At the front it fades from 0 to 8 cm above the joint, at the back from 3 to 12 cm.
  5. Prune weights under 0.01, keep at most 4 influences, and normalise.
- Net change: Hips +758, LeftUpLeg −456, RightUpLeg −302 (summed vert weights).
- Deterministic: two runs give the same output, sha256 `38b14f62…`.
- Tried and dropped:
  - A 1.2 cm side band tore the crotch in the kick (10x stretch).
  - Giving the crotch midline to Hips tore the crouch (113 edges).
  - Smoothing again after the fade re-spread the UpLeg weight and tore the hip crease.

Reproduce:
```
Blender -b --python tools/blender/repair_weights.py -- \
  --in game/assets/generated/characters/ryo/ryo-rigged.glb --out <dir>/ryo-rigged-repaired.glb --log <dir>/repair_log.json
```

## Rig check, before and after
`rig_check.py` (YOK-110 branch) with `skeleton.md` (YOK-111 branch), height 1.75 m. Failing edge = stretch >3x or squash <0.25x; a pose FAILs above 62 failing edges (0.2% of 31,130) or on a collapsed bone.

| Pose | Before | After |
|---|---|---|
| deep crouch | **FAIL**: 68 failing edges, max 6.67x, `Hips` collapsed (mean 0.686, p5 0.28) | **WARN**: 27 failing edges, max 4.12x, no collapse. `Hips` mean 0.964, p5 0.80 |
| high kick | WARN: 35 failing edges, max 5.42x | WARN: 9 failing edges, max 3.79x |
| arm raise | WARN: 4, max 3.15x | WARN: 4, max 3.15x (unchanged, outside the band) |
| grab | WARN: 1, max 2.03x | WARN: 1, max 2.03x (unchanged) |
| torso twist | PASS | PASS |
| skeleton, rest, scale, up, origin, weights | PASS | PASS (max 4 influences, normalised, 0 cross-side) |

- In the crouch, the gate target was a `Hips` mean of 0.85 or more. The set of verts mostly weighted to `Hips` grows from 320 to 1,638 after the repair, because the belly band now belongs to `Hips`. On the original 320 verts the repaired mean is 0.980 (p5 0.93), so the gain does not come from the changed set.
- The trade-off: edges stretched over 1.8x (not tears) go up in the kick, from 156 to 313, and stay level in the crouch, 255 to 244. The stretch moved from tears at the crotch and belly to an even crease at the hip.

## Ryo's real clips on the repaired skin
Run with `tools/blender/skin_clip_check.py`. Each clip's own action plays on both skins (same rest, 0.0 m bone offset), every frame is measured, and stills are rendered at the worst frames.

| Clip | Worst-frame failing edges, before → after | Max stretch, before → after |
|---|---|---|
| `ryo-crouch` (CrouchLookAroundBow) | 97 → 43 | 6.08 → 4.87 |
| `ryo-hk` (High_Kick) | 61 → 52 | 5.91 → 5.91 |
| `ryo-grab-2` (throw T01, Step_Forward_and_Push) | 67 → 61 | 5.70 → 5.70 |
| `ryo-mk` | 42 → 39 | 6.31 → 4.70 |
| `ryo-jump-kick` | 84 → 45 | 5.71 → 5.19 |
| `ryo-knockdown` | 81 → 75 | 7.09 → 4.71 |
| `ryo-get-up` | 149 → 85 | 7.88 → 4.63 |
| `ryo-block-low` | 44 → **52** | 4.66 → 4.66 |
| `ryo-walk-guard-fwd` | 19 → 18 | 3.60 → 2.87 |

What the stills show:
- **Better.** The rope sash, pouch and paper stay a level belt at the waist. Before, the thighs dragged them down and into the crotch in the crouch, kick and get-up, and the belly no longer caves in.
- **New, minor:**
  1. In the high kick the take-2 front coat panel no longer follows the raised thigh, so a small fold sits at the hip crease.
  2. In the deep crouch, seen from the front, a strip of sash shows between the raised thighs. This is correct depth order, not clipping.
  3. In `ryo-block-low` the worst frame has 8 more failing edges (20 on `Hips`, at the hip crease), with no visible tear at 720p framing.
  4. In `ryo-grab-2` frame 40 the sash sits slightly lower at the front and half covers the paper strip.
- No new tears at the wrists, shoulders or head. Those weights are untouched.

Stills and raw reports are kept outside the repo. Each `*_compare.png` sheet has before on top, after below, in front, side and pelvis close-up views.
- Compare sheets: `/private/tmp/claude-501/-Users-whash-yokai-fighters/da2b5d58-deeb-4eb6-a182-300c22b991dc/scratchpad/ryo-repair/compare/`
- Rig check reports: `…/ryo-repair/before/report.json`, `…/ryo-repair/after/report.json`
- Clip report: `…/ryo-repair/clips/clip_report.json`
- Repaired GLB: `…/ryo-repair/after/ryo-rigged-repaired.glb`

On acceptance, copy the sheets to `assets/specs/rigs/stills/ryo-repair-*.png`.

## Godot check
The repaired GLB was imported headless in Godot 4.7.2 mono, in a scratch project. It loads with 1 `Skeleton3D` of 24 bones, 1 skinned `MeshInstance3D` with 17,714 verts, and the bundled animation.

## How it would replace the game asset (designer's call, not done)
1. Regenerate the GLB with the command above, straight to `game/assets/generated/characters/ryo/ryo-rigged.glb`. Keep the old file as `ryo-rigged.take2-raw.glb` outside `game/`, or rely on git history.
2. Keep the filename and the `.import` file, with uid `uid://dnphawusyrft0`. Then `game/scenes/fighters/ryo.tscn` and `FighterAnimSet.cs` (`ModelPath`) need no edit, and Godot re-imports on the next open.
3. Clips need no rework. The skeleton, rest pose and bone heads are identical, so all 26 `data/clips/ryo-*.json` matches, the frame data and the hurtboxes stand. The clip GLBs carry their own copies of the old skin, but the game draws Ryo from `ryo-rigged.glb`.
4. Re-check once in engine: a capture of the crouch, kick and throw for `docs/screenshots/current/`, and the game suite.
5. T0: the Tanuki fights in this rig, so it gets the fix too.
6. Update `assets/specs/rigs/ryo.md` (YOK-111 branch). Put the repaired file in its Rig table, replace "Stress poses (pending)" with this check, and add a row to Takes: "weight repair (script), 0 credits".

## Open points for the designer
- Approve or decline the swap into `game/`.
- Whether to still spend on the regeneration in `ryo-regen.md`. The repair fixes the rig gate, not the outfit (loose trouser folds, hip-height coat panel).
- The arm raise and grab WARNs at the shoulders are outside this repair. They are the same as before.
- The `rig_check.py` volume metric depends on which verts are mostly weighted to `Hips`. A weight repair changes that set, so both numbers are given above. The rule could fix the vert set at rest in a later YOK-110 revision.
