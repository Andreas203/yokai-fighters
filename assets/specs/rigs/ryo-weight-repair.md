# Ryo weight repair (free, scripted)

Ticket: YOK-116. Owner: rig-specialist. Rules: F5, F6, G4, T0, AM5. Diagnosis: `assets/specs/characters/ryo-regen.md` §1–2.
Designer's choice: try a free Blender weight repair on the current rig before any regeneration spend. **No Meshy, Tripo or AccuRIG job was run. 0 credits spent.**

**Verdict: ACCEPT (repair reaches WARN). Swapped in on 2026-10-15 with the designer's approval, and there will be no regeneration.** `characters/ryo-regen.md` is kept on file as not needed. `game/assets/generated/characters/ryo/ryo-rigged.glb` is now the repaired file (sha256 `38b14f62…`). The raw take-2 rig is in git history at `fe5d423`.

## What was done
- Script: `tools/blender/repair_weights.py`. It runs headless and logs every step. Input is the raw take-2 rig, `ryo-rigged.glb` as of `fe5d423` (rig task `01a10e47-857e-73a0-9f7a-cd74795198fb`, sha256 `6cd871a4…`). The script never writes its input.
- It changes only the `JOINTS_0`/`WEIGHTS_0` bytes in a copy of the GLB. Mesh, UVs, normals, texture, skeleton, skin order, inverse bind matrices and the bundled clip are byte-identical: all 48,045 changed bytes fall inside those two buffer views.
- It only works in the pelvis band, from z 0.668 to 1.089 m (3,273 of 17,714 verts). Every other vert keeps its exact original slots.
- Steps:
  1. Weld the UV-seam duplicates.
  2. Laplacian-smooth the Hips/UpLeg columns for 20 iterations, keeping their per-vert total.
  3. Separate the two sides, so each half of the fused crotch follows its own leg. The two sides blend over ±5 cm at the midline.
  4. Fade UpLeg weight out above the hip joint and give it to Hips. At the front it fades from 0 to 8 cm above the joint, at the back from 3 to 12 cm.
  5. Optional prop step, off by default (see "Charm, pouch and sash").
  6. Prune weights under 0.01, keep at most 4 influences, and normalise.
- Net change: Hips +758, LeftUpLeg −456, RightUpLeg −302 (summed vert weights).
- Deterministic: two runs give the same output, sha256 `38b14f62…`.
- Tried and dropped:
  - A 1.2 cm side band tore the crotch in the kick (10x stretch).
  - Giving the crotch midline to Hips tore the crouch (113 edges).
  - Smoothing again after the fade re-spread the UpLeg weight and tore the hip crease.

Reproduce from the raw rig (the script refuses to overwrite its input):
```
git show fe5d423:game/assets/generated/characters/ryo/ryo-rigged.glb > <dir>/ryo-rigged.take2-raw.glb
Blender -b --python tools/blender/repair_weights.py -- \
  --in <dir>/ryo-rigged.take2-raw.glb --out game/assets/generated/characters/ryo/ryo-rigged.glb --log <dir>/repair_log.json
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

Stills are in `assets/specs/rigs/stills/`. Each sheet has before on top and after below, in front, side and pelvis close-up views:
- `ryo-repair-deep-crouch.png` and `ryo-repair-high-kick.png`: the stress poses.
- `ryo-repair-clip-crouch-f107.png`, `-hk-f026`, `-grab-2-f040`, `-get-up-f019` and `-block-low-f013`: Ryo's clips.

## Charm, pouch and sash (YOK-110 cloth pass re-check)
The cloth pass (`assets/specs/rigs/cloth/ryo.md`, YOK-110 branch) found the lightning paper charm and the pouch at the front of the hip weighted to the thighs. The rig plan wants them rigid on `Hips`. They are not separate parts: they are painted onto the one body mesh, which has only 3 islands, and they sit across the hip crease (z 0.84–0.96 m; the hip joint is at 0.862). I found them by base colour in the pelvis band:
- Light charm and pouch: 275 verts.
- Persimmon rope sash: 250 verts.

| Set | Take-2 raw rig | Accepted repair (in game) |
|---|---|---|
| Charm + pouch (275 verts) | `Hips` mean 0.27; UpLeg mean 0.69; 266 verts thigh-dominant | `Hips` mean 0.67; UpLeg mean 0.29; 95 verts thigh-dominant |
| Rope sash (250 verts) | `Hips` mean 0.32; UpLeg mean 0.61; 151 thigh-dominant | `Hips` mean 0.70; UpLeg mean 0.23; 62 thigh-dominant |

The upper parts of the charm, pouch and sash are now on `Hips`. **They are not rigid.** The parts below about 5 cm above the hip joint still follow the thighs.

A full rigid bind was tried as step 4 of the script (`--set prop_bind=1`). It sets UpLeg to 0 on each colour patch of 20 or more verts and blends into the body over 3–16 edge rings. Every variant FAILED the deep crouch with 87–198 failing edges and up to 19x stretch, and the kick rose to 31–67 edges. Binding only the parts above the joint failed as well (107–198). A painted patch that crosses the crease cannot stay rigid while the cloth under it bends with the thigh: it either tears or clips.

What's left:
- The lower charm, pouch and rope ends still move with the thighs. In the stills this reads as the charm and pouch tilting with the leg, with no tear.
- A true rigid bind needs the props as separate geometry on `Hips`, with the body painted plain under them. That is a mesh edit, so an asset-smith or modelling task.
- It is listed below as an open point and is not done here.

## Godot check
The repaired GLB was imported headless in Godot 4.7.2 mono, in a scratch project. It loads with 1 `Skeleton3D` of 24 bones, 1 skinned `MeshInstance3D` with 17,714 verts, and the bundled animation.

## Swap (done 2026-10-15)
1. The repaired GLB was regenerated from the raw rig straight onto `game/assets/generated/characters/ryo/ryo-rigged.glb` (sha256 `38b14f62…`, the same as the reviewed file). The filename and the `.import` file (uid `uid://dnphawusyrft0`) are unchanged, so `ryo.tscn` and `FighterAnimSet.cs` need no edit.
2. Re-imported with `godot --headless --path game --import` (Godot 4.7.2 mono), exit 0, `ryo-rigged.glb` re-imported. A headless load of `res://assets/generated/characters/ryo/ryo-rigged.glb` gives 1 `Skeleton3D` with 24 bones, 1 skinned mesh with 17,714 verts, and its animation. The import log's theme errors (`rice-sheet.png`, `rice-card.png`) come from the fresh `.godot` cache and have nothing to do with this file.
3. Clips, frame data and hurtboxes carry over: the skeleton, rest pose and bone heads are identical. T0: the Tanuki fights in this rig and gets the fix too.
4. Still to do: run the game suite (coordinator) and take an in-engine capture for `docs/screenshots/current/`. `assets/specs/rigs/ryo.md` (YOK-111 branch) gets the repaired file and a Takes row, "weight repair (script), 0 credits", when the two branches meet.

## Open points for the designer
- Charm, pouch and sash: only partly freed from the thighs (see above). A rigid bind needs a separate-mesh prop edit.
- The arm raise and grab WARNs at the shoulders are outside this repair. They are the same as before.
- The `rig_check.py` volume metric depends on which verts are mostly weighted to `Hips`. A weight repair changes that set, so both numbers are given above. The rule could fix the vert set at rest in a later YOK-110 revision.
