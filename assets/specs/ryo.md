# Ryo (fighter) - generation spec

Ticket: YOK-29 (feeds YOK-39 slice). Status: APPROVED first pass (YOK-30). DESIGNER DECISIONS (2026-10-05): first pass approved (163 credits total across all specs); T-pose, not A-pose; retakes need new approval.
Reference: `docs/design/characters/ryo/ryo-meshy-front.png` (1024x2112, A-pose reference; generate T-pose). Sheet: `docs/design/characters/ryo/README.md`.

## Prompt (after the style block in `assets/specs/style.md`)

Turnaround (image-to-image, reference = ryo-meshy-front.png):
> Character turnaround sheet of the same young exorcist: front view, side view and back view, same height and scale, T-pose (arms straight out), plain white background. Indigo coat with spiral brass buttons closed left over right, long coat tail with persimmon lining, high indigo collar over the lower face, black undercut hair with one lock over the right eye, shimenawa rope sash with paper shide and a small pouch, indigo trousers, rice-paper shin wraps, black split-toe shoes, vermilion mark under the left eye, four rice-paper talismans wrapped on the right forearm. Palette: indigo #2D3A5E, ink #1D1B21, persimmon #D8632C, rice paper #F1E8D4, brass #B08A48. No held items, no energy effects, no extra talisman strips. Avoid: [negative prompt from style.md].

Image-to-3D: the selected front view only. No texture_prompt.

## Job chain

| # | Tool | Settings | Credits |
|---|---|---|---|
| R1 | `meshy_image_to_image` | `ai_model: nano-banana-pro`, `reference_file_paths: [ryo-meshy-front.png]`, `generate_multi_view: true` (front/side/back in one call; if views are inconsistent, retake as 3 single calls at 9 each) | 9 |
| R2 | `meshy_image_to_3d` | `ai_model: meshy-7.1`, `file_path`: chosen front view, `model_type: standard`, `pose_mode: t-pose`, `should_remesh: true`, `topology: triangle`, `target_polycount: 15000`, `should_texture: true`, `texture_resolution: 2k`, `enable_pbr: false`, `image_enhancement: false`, `origin_at: bottom`, `auto_size: true` (~1.75 m), `target_formats: ["glb"]` | 30 |
| R3 | `meshy_rig` | `input_task_id`: R2, `height_meters: 1.75`. Includes free walk + run clips | 5 |

Per-take total: 44. Take cap 3 (whole chain, or any subset the designer approves, e.g. R2+R3 = 35). Download with `meshy_download_model` (glb) into `game/`.

## Rig plan (Meshy humanoid skeleton)

| Part | Driven by | Method |
|---|---|---|
| Body, head, arms, legs | Meshy humanoid skeleton | shared clips |
| Coat tail (long side) | skeleton | baked rigid to hips/spine. If it clips legs in walk, heavy or throw, add 2 extra bones with sway computed from the tick count (deterministic, no physics) |
| Shimenawa rope, shide, pouch | skeleton | baked rigid to hips |
| Forearm ofuda wraps | skeleton | baked; no per-slot glow in the demo (sheet open question 2) |
| Collar | skeleton | baked; mouth stays covered (sheet open question 1) |

Hair is baked to the head. No secondary-motion physics anywhere (F3, determinism).

## Acceptance criteria

- Palette matches; coat tail, collar, binding mark and four forearm ofuda legible; no stray talisman strip or held ofuda.
- Silhouette reads lean and wide-stance at 720p; hands separate from the torso.
- <= ~15k triangles; one 2K texture; no PBR maps.
- Rig imports in Godot 4, skeleton intact, 1.75 m, up-axis Y, origin at feet.
- G4 readiness: coat-tail clearance for walk, heavy and throw recorded for clip-matcher.
- Licence: plan tier unknown, flagged.

## Takes
Take 1, 2026-10-05 (approved first pass, T-pose). Prompt shortened to fit the 600-char tool limit (style block condensed).

| Job | Task id | Credits (est/actual) |
|---|---|---|
| R1 turnaround | 01a10e15-7ddd-721f-8a9a-6f01c06b18de | 9/9 |
| R2 mesh | 01a10e18-621a-73e8-9ecd-193c46618713 | 30/30 |
| R3 rig | 01a10e19-f774-707d-9b31-3e94ac0acdcd | 5/5 |

Total 44/44. Outputs: `characters/ryo/ryo-turnaround_{0,1,2}.png` (0=back, 1=front, 2=side; front used), `ryo-mesh.glb`, `ryo-rigged.glb`, `ryo-walk.glb`, `ryo-run.glb`.

## Acceptance
Verdict: **PASS (provisional; no in-engine render yet)**.
- Look: front view matches palette (indigo coat, brass buttons, persimmon lining/rope, wraps, split-toe shoes), flat, outlined. Ink outline pass and toon shader are not in the repo yet, so the shaded look is unchecked.
- Rig (parsed from glTF): 15,161 tris, 1 mesh, 1 material, 2K base colour only (no PBR), 24-joint Meshy humanoid skeleton, height 1.75 m, bbox min Y = 0 (origin at feet), Y-up. Rig GLB carries one baked clip; separate walk and run GLBs included. T-pose span is +-0.80 m, so do not use it as a rest hurtbox.
- Deviations from the sheet: ofuda wraps read as bandage with red dots, not four distinct strips; coat tail is a short asymmetric panel, not long. Minor; flag to designer.
- G4: the coat panel is short and sits close to the thighs, low clipping risk; clip-matcher to confirm.
- Texture atlas is fragmented islands (normal UV layout); needs a Godot import check for seams.
- Files under `game/assets/generated/`. Licence: Meshy plan tier and commercial terms of generated output are NOT verifiable from the MCP (balance call only); UNKNOWN, designer to confirm on the plan before shipping.
