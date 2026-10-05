# Kitsune (fighter) - generation spec

Ticket: YOK-29 (feeds YOK-39 slice). Status: APPROVED first pass (YOK-30). DESIGNER DECISIONS (2026-10-05): first pass approved (163 credits total across all specs); T-pose, not A-pose; nine tails, not rigged; retakes need new approval.
References: `docs/design/characters/kitsune/kitsune-meshy-front-notails.png` (body) and `kitsune-meshy-front.png` (tails fanned, for the tail image). Sheet: `docs/design/characters/kitsune/README.md`.

## Decision (designer): nine tails

The ticket asks for a nine-tail plan. The sheet says three tails for the common Kitsune (the bamboo-grove fight) and nine only for the Nine-Tailed elder (sheet open question 1). This spec builds the tail as one reusable mesh instanced N times, so 3 or 9 is a data change with no extra generation. DECIDED: the demo uses nine tails, one mesh instanced 9x, NOT rigged: rigid hip attachments with tick-driven sway.

## Prompts (after the style block)

Body turnaround (image-to-image, reference = kitsune-meshy-front-notails.png):
> Character turnaround sheet of the same fox-spirit fighter with NO tails: front, side and back views, same height and scale, T-pose (arms straight out), plain white background. Full white fox mask with red markings covering the whole face (face never visible, eye slits only), fox ears, long white hair tipped orange hanging straight down the back, cropped white haori with wide sleeves and red trim over an indigo high-neck top, orange sash with indigo cord and a small bell, shrine-red knee-length hakama, black split-toe socks and tall wooden sandals. Palette: fur white #FBF6EC, foxfire #F2A23A, shrine red #B5332B, indigo #2D3A5E. No fire, no hand sign, no tails. Avoid: [negative prompt].

Tail (image-to-image, reference = kitsune-meshy-front.png):
> A single fluffy fox tail on its own, side view, white fur with foxfire-orange tip, gentle S-curve, plain white background, flat cel shading. Avoid: [negative prompt], body, hands, fire.

## Job chain

| # | Tool | Settings | Credits |
|---|---|---|---|
| K1 | `meshy_image_to_image` | `nano-banana-pro`, reference notails front, `generate_multi_view: true` | 9 |
| K2 | `meshy_image_to_3d` | shared settings (style.md); front view of K1; `target_polycount: 15000`, `pose_mode: t-pose`, `texture_resolution: 2k`, `auto_size`, `origin_at: bottom` (~1.65 m) | 30 |
| K3 | `meshy_rig` | `input_task_id`: K2, `height_meters: 1.65` | 5 |
| K4 | `meshy_image_to_image` | `nano-banana`, tail prompt, reference = tails front image | 3 |
| K5 | `meshy_image_to_3d` | `ai_model: meshy-6-lite`, K4 image, `target_polycount: 4000`, `should_remesh: true`, `texture_resolution: 2k`, `enable_pbr: false` (rigid prop; cheap model is enough) | 15 |

Per-take total: 62 (body K1-K3 = 44, tail K4-K5 = 18). Take cap 3 per chain.

## Rig plan

Meshy's rig is humanoid only: it will not rig tails or loose sleeves.

| Part | Driven by | Method |
|---|---|---|
| Body, limbs, head | Meshy humanoid skeleton | shared clips |
| Tails (9, one mesh instanced) | Godot, not Meshy | the single K5 mesh is instanced N times on `BoneAttachment3D` nodes at the hip. Each tail gets a fixed fan angle plus procedural sway `angle = base + A * sin(tick * w + phase_i)`, amplitude scaled by clip speed. Tick-driven, so deterministic; no physics, no hand-keyed animation. Each tail is baked rigid (no per-tail bones) |
| Ponytail/hair | skeleton | baked, hangs straight down |
| Haori sleeves, hakama | skeleton | baked into the body; clipping risk in heavy/throw (G4) |
| Fox ears, mask | skeleton | baked to head |
| Suzu bell, obi cord | skeleton | baked |

Tails are kept off the body mesh because tails fused to the body get stretched by the auto-rig and clip the legs (the G4 failure mode).

## G4 fallback: folklore human guise (flag)

If generated walk, heavy or throw clips clip the sleeves, hakama or tails and clip-matcher's gate test fails, Kitsune falls back to her folklore human guise: a tail-less human woman in plain kimono, with the full mask kept on (designer decision: the face stays hidden). That is a new spec and a new approval. Contingency estimate, NOT in the totals: 9 + 30 + 5 = 44 per take.

## Acceptance criteria

- Mask fully covers the face (hard fail if a face appears); orange-tipped white hair; fox ears; palette matches.
- No tails on the body mesh; tail mesh is one clean piece, orange tip, pivot at the root.
- Rig per Meshy humanoid, 1.65 m, origin at feet; tails attach at the hip with no gap.
- G4 readiness: sleeve, hakama and tail clearance recorded for clip-matcher's walk, heavy, throw test.
- Silhouette says fox at 720p (ears plus tail fan).
- Licence: plan tier unknown, flagged.

## Takes
Take 1, 2026-10-05 (approved first pass, T-pose, nine tails not rigged).

| Job | Task id | Credits (est/actual) |
|---|---|---|
| K1 turnaround | 01a10e15-8089-70cf-a57b-7b72c5850df8 | 9/9 |
| K2 mesh | 01a10e18-660f-771b-b8de-f7f71792ece1 | 30/30 |
| K3 rig | 01a10e1a-063d-7300-8ecd-5b42ad89b086 | 5/5 |
| K4 tail image | 01a10e15-82bd-747d-b7b1-3e3390a91729 | 3/3 |
| K5 tail mesh | 01a10e18-6a0b-72fa-b646-ad75b3210f3b | 15/15 |

Total 62/62. Outputs: `characters/kitsune/kitsune-turnaround_{0,1,2}.png` (0=front, 1=side, 2=back; front used), `kitsune-mesh.glb`, `kitsune-rigged.glb`, `kitsune-walk.glb`, `kitsune-run.glb`, `tail-ref.png`, `tail.glb`.

## Acceptance
Verdict: body **PASS WITH DEFECT (designer to look; optional RETAKE K1+K2+K3 = 44 credits, not approved)**, tail **PASS**.
- Body look: full mask on with eye slits, fox ears, white hair tipped orange, white haori with red trim, indigo top, orange sash, red hakama, wooden sandals; no tails on the body. DEFECT: the front reference has grey smudge artefacts across the haori and hands, baked into the texture as grey patches. The side view shows a high ponytail while front/back show hair hanging; only the front was used.
- Rig: 15,120 tris, 24-joint humanoid, 1.65 m, origin at feet, Y-up, 2K base colour, no PBR. T-pose span +-0.64 m. Walk/run GLBs included.
- Tail: 3,161 tris, clean white-to-orange single piece, one 2K texture. Pivot is at the mesh centre (bbox ~+-0.95 m, unscaled), not the root; rescale to ~0.5 m and offset the root in Godot. Nine instances, tick-driven sway, not rigged, per designer.
- G4: wide sleeves and knee-length hakama are a clipping risk in heavy and throw; hair hangs down the back and may clip the tail attachments. Record for clip-matcher.
- Files under `game/assets/generated/`. Licence: Meshy plan tier and commercial terms of generated output are NOT verifiable from the MCP (balance call only); UNKNOWN, designer to confirm on the plan before shipping.
