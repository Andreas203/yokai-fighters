# Ryo Tripo texture paint-over (YOK-146), phase A

Phase A makes the paint-over inputs and checks the diagnosis. No generation job was run and no credits were spent. Astra is run by the designer in Codex from the section "Ryo texture paint-over (YOK-146)" in `docs/design/characters/astra-brief.md`. Phase B (project the paintings back onto the existing UVs and bake) comes after.

## Files

| File | What |
|---|---|
| `ryo-albedo-{front,left,right,back}.png` | Unlit albedo, 2048 px square, pure white background: the paint-over base |
| `ryo-mask-{front,left,right,back}.png` | Silhouette, pure black on pure white, 2048 px, no anti-aliasing |
| `views.json` | Camera parameters shared by the four views (ortho scale 2.97 m, 1.45 mm per pixel, target, per-view position and matrix, input sha256) |
| `ryo-paint-{front,left,right,back}.png` | Astra's outputs (not yet made) |
| `diagnosis/basecolor-1024.png`, `diagnosis/uv-layout-1024.png` | The base colour (8192 px, shown at 1024) and the UV layout |

Made by `tools/blender/texture_views.py` (Blender 4.1.1, headless, Workbench flat lighting, Standard view transform, anti-aliasing off). Re-run: `blender -b --python tools/blender/texture_views.py`. The run was clean: eight renders and the JSON, no warnings. Views: front (he faces the camera, his left is image-right), left (his left side, he faces image-left), right (faces image-right), back. Same ortho scale, camera distance and centre for all four. Notes: the glTF importer adds a bone-shape "Icosphere" helper that is not in the file; the script hides it. The albedo is the raw base-colour image, so the 0.8 `baseColorFactor` is not in it.

## Diagnosis of `ryo-tripo-rigged.glb`: confirmed

1. **Shading painted into the texture: confirmed.** The 8192 px JPEG (7.4 MB) carries cloth folds as dark creases on the coat, hakama and sleeves, soft gradients on the orange lining, ink-line hatching on the wraps and dark smudges (`diagnosis/basecolor-1024.png`, and the albedo renders: the folds read as drawn shadow even with no light). In `stills/ryo-tripo/clips/ryo-get-up_f030_tripo_side.png` the folds stay on the cloth while the mesh bends, and the toon shader's own light bands and specular blobs sit on top (the shiny patches on the sleeve and hakama knee). Double shading.
2. **Muddy island edges: confirmed.** The UVs have 315 islands, 189 of them tiny (under 0.05% of the sheet), packed around the large cloth panels (`diagnosis/uv-layout-1024.png`). The cloth panels are long single islands with sparse triangles; the hair, hands and small props are many small blobs. The texture around them (top rows, sides) is smeared, with dark bleed and blown-out white gaps. Where the groin and knee stretch, the triangles are large and the texture pulls across them.
3. **Darkening: confirmed.** The material has `baseColorFactor` 0.8 on all channels (the whole sheet renders at 80%), plus `KHR_materials_specular` 0.8 and roughness 0.5. Godot multiplies the factor in, so every colour is darker than the concept. The knockdown still shows it: the pelvis looks black. `ToonLook.cs` copies only the diffuse mode, specular mode and roughness from `toon.tres` onto the material; it keeps the albedo colour (factor included) and texture.

## What the paint-over fixes and what it can't

| Fixed by the paint-over | Not fixed |
|---|---|
| Painted folds and shadows (cause 1): flat colour has nothing to stretch, and the toon shader shades it once | Skin-weight stretching and the jagged hakama pleat at the groin (knockdown f089 pelvis still): that is geometry and weights, see `ryo-tripo.md` open item 3 (groin and knee repair step) |
| Muddy bleed and halos inside islands (cause 2, partly): flat fills have no texture noise to smear | The fragmented UV islands themselves: the bake reuses the existing UVs, so seams stay. Flat colour makes seams invisible except where two islands get different colours; only a re-unwrap (a new Tripo take or a Blender unwrap, then a new rig pass) fixes the layout |
| Darkening (cause 3), but only if the bake is paired with a factor of 1.0 | `baseColorFactor` is a material setting, not texture: set it to 1.0 in the bake step (phase B) or `ToonLook` |
| 8192 px for 54k triangles: the bake can be 2048 px | Areas hidden from the four views (under the arms, inside the sleeves, soles, crotch, under the chin and hair): phase B needs a fill rule, for example the nearest painted colour |
| | Charm and pouch painted across the hip crease (the deep-crouch volume flag): unchanged |
| | Ink outline weight: comes from `ink_outline.tres`, not the texture |

Side views see only half-depth surfaces cleanly; steep surfaces (shoulders, tops of feet, sleeve undersides) project badly from four views, so phase B should blend by surface-normal angle and fill the rest by colour.

# Phase B result: baked texture

Astra's four paintings were accepted (silhouette IoU 0.973 to 0.983 against the masks; 8 generations used, no more to run). No credits spent in phase B.

## What was made

| Item | Path |
|---|---|
| Baked base colour, 2048 px sRGB PNG, on the mesh's own UVs | `game/assets/generated/characters/ryo/ryo-tripo-basecolor.png` (sha256 `a1b5ff76...`) |
| Projection script | `tools/blender/texture_project.py` (log: `projection.json`, coverage map: `diagnosis/projection-coverage.png`: white = painted by a view, red = filled, grey = island padding) |
| Texture-into-GLB script | `tools/blender/apply_texture.py` (plain Python, no Blender) |
| Helpers for the checks | `tools/blender/texture_compare.py` (contact sheets), `tools/blender/texture_closeup.py` (seam close-ups) |
| `ryo-tripo-rigged.glb` with the new texture applied | sha256 `82825610...`, 4.98 MB (was 9.39 MB with the 8192 px JPEG) |

Projection settings: weight = (normal . direction to camera)^3, a view counts only above cos 0.25, per-view z-buffer occlusion (so the back view never paints the front), paint sampled inside the view's mask eroded 3 px, near-white (background leak) rejected. 1,879,968 of 3,479,921 island texels (54%) are painted by at least one view; the other 46% are filled with the per-channel median colour of the 15 nearest painted texels in 3D (a thin rope or wrap beside cloth cannot leak in, which the nearest-single-texel fill did). Islands are padded 16 px by dilation; texels outside islands take the mean colour.

## Re-applying after the weight repair

rig-specialist's groin repair will overwrite `ryo-tripo-rigged.glb` (UVs and the mesh must stay unchanged; if the repair re-unwraps or decimates, rerun the projection first). From the repo root:

```
python tools/blender/apply_texture.py --glb game/assets/generated/characters/ryo/ryo-tripo-rigged.glb --png game/assets/generated/characters/ryo/ryo-tripo-basecolor.png
```

Add `--out <other.glb>` to write a copy. It replaces the image, sets `baseColorFactor` to 1.0 and rebuilds only the BIN layout; every other bufferView (mesh, UVs, joints, weights, inverse bind matrices) is copied byte for byte. If the mesh or UVs did change: `blender -b --python tools/blender/texture_project.py -- --in <new.glb>`, then apply as above. The projection takes about a minute.

## Checks

All paths under `assets/specs/texture/ryo-tripo/compare/` unless noted. Blender Workbench renders (same renderer as the old rig stills), so lighting differs from Godot's toon shader and the 0.8 factor does not show in them; the Godot captures show it.

- `views-old-paint-new.png`: four rows (front, left, right, back); columns old albedo, Astra paint, the new texture on the mesh. The new texture follows the paint closely: indigo coat, persimmon lining, rope, wraps, boots, face, buttons, charm. Painted folds are gone.
- `deformed-old-new.png`: get-up f030 (front, side), knockdown f089 (front, pelvis), jump f076 (front, pelvis), heavy kick active f063 (front, side); old then new for each. The new cloth has no folds to stretch, so the groin and knee pose no longer looks doubled; the jagged pleat that remains is geometry.
- `seams-closeup-old-new.png`: rest-pose waist and hip close-ups, front and back (old, new, old, new). No visible seam lines on the cloth.
- Godot, windowed capture in the grove (`tests/model_capture.tscn only=ryo`): `docs/screenshots/current/fighters/ryo-tripo-texture/before/` and `after/` (idle, jab, heavy punch, heavy kick). Brighter and closer to the concept indigo `#2D3A5E`; the 0.8 darkening is gone.

## What still looks wrong

- **Seams on the existing UV layout.** 315 islands stay. On flat colour the seams are invisible except where two islands were filled differently: a few faint dark threads on the back of the hakama and thin orange streaks (the lining) in the side views, where lining and coat islands meet.
- **Filled areas (46% of island texels).** Unseen texels (sleeve insides, armpits, under the hakama layers, soles, inside the collar, under the hair) take median colours of their neighbours. Under motion this shows as orange lining patches on the outside of the hakama in the kick (the lining is real geometry that the pose exposes; the same patches show in the old texture) and a different shade at the inner sleeve.
- **Rope and charm.** The rope tassels and the charm are thin geometry; the belt reads, but the tassels carry some white scraps on the thigh and hip in the Godot idle capture. The charm and pouch are still painted across the hip crease (the deep-crouch volume flag is unchanged).
- **Detail lost from the paint.** The bandage lines are simplified, the face is plainer (fine at fighting distance), and there is a slight two-tone residue in the cloth. The hand skin is a little paler than the concept.
- **Not texture:** the groin pleat and skin-weight stretch in knockdown, get-up and jump (rig-specialist's repair), the 54k triangle count and the Workbench-versus-toon shading difference.
