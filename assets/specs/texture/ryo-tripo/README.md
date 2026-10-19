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
