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

## Take 2 (YOK-30 rebuild, 2026-10-05, designer-approved, one take, 44 credits; tail untouched)
Supersedes take 1 body files (first-pass task ids above stay in history). New reference: `docs/design/characters/kitsune/references/kitsune-ref-d.png` (stylised, dynamic stance, wide hakama, flowing hair; tail-less; from PR #19). Nine tails remain the separate unrigged `tail.glb`.

Turnaround prompt (nano-banana-pro, `generate_multi_view: true`):
> Character turnaround of the same fox-spirit fighter, no tails: front, side and back views, same height and scale, T-pose (arms straight out), neutral standing pose with legs close together, plain white background. Keep this art style: anime, thick black ink outline, 3-tone cel shading. Full white fox mask hiding face, fox ears, long white hair tipped orange falling behind the shoulders, white haori with wide sleeves, indigo top, red hakama, bell on obi, black socks, wooden geta. No text, no logos, no kanji.

| Job | Task id | Credits |
|---|---|---|
| K1 turnaround | 01a10e45-1710-7131-b05d-fd6bcfa89887 | 9 |
| K2 mesh (meshy-7.1, t-pose, 15k tri, 2K, no PBR) | 01a10e46-2481-75d3-8391-5268b4bbb83a | 30 |
| K3 rig, 1.65 m | 01a10e47-91f6-709b-bd34-8884f8cbabbb | 5 |

Total 44. Files replaced in place: `characters/kitsune/kitsune-{mesh,rigged,walk,run}.glb`, `kitsune-mesh_base_color.png`, `kitsune-turnaround_{0,1,2}.png` (0=front, 1=side, 2=back; front used). `tail.glb`, `tail_base_color.png`, `tail-ref.png` kept.

### Acceptance (take 2)
Verdict: **PASS (provisional; no in-engine toon render yet)**. The take-1 grey-smudge defect is not present in the new turnaround front view.
- Look: mask with orange markings and eye glints, fox ears, white hair tipped orange hanging behind shoulders (consistent in all three views, no ponytail), white haori with red trim and orange crest, indigo top, bell, black socks, geta, red pleated hakama. Neutral stance achieved (legs close). Bold outline kept. Hakama is a wide full-length skirt-trouser hiding the legs down to the socks (longer than the sheet's knee length).
- Rig (glTF parsed): 15,153 tris, 1 mesh, 1 material, 2K base colour, no PBR, 24 joints, rigged height 1.65 m (unrigged mesh bbox 1.70 m), origin at feet, Y-up, T-pose span +-0.72 m. Sizes 3.9-4.6 MB each.
- G4 risk (high, record for clip-matcher/YOK-31): (1) hakama is wide and long with legs inside one shared volume; Meshy skinning will likely weight both legs onto the same cloth, so walk may show the skirt stretching or leg poking through, and heavy/throw wide stances will tear or pinch the front pleats; (2) hair is long to hip level behind the back, baked rigid to head/spine, will clip into the hakama back and into the tail mounts in kick/crouch; (3) wide haori sleeves clip in heavy/throw as before. Mitigation if the gate fails: shorten hakama or tie hair up (new spec, 44 credits), or folklore human guise fallback.
- Licence: plan tier unknown, flagged.

## Take 3: Tripo redo (YOK-147, SPEC ONLY, awaiting designer approval)

Ticket: YOK-147 "Tripo redo: Kitsune". Status: **DRAFT. No job has been run and no credits spent.** The designer runs Tripo by hand (no Tripo MCP is installed) and drops the files in `game/assets/generated/characters/kitsune/` (raw files in a `tripo-raw/` subfolder with a `.gdignore`; a raw Tripo rig is never imported as is, see Rig notes). It supersedes the Meshy take 2 body only when accepted AND G4 passes; until then the Meshy files stay (same rule as Ryo, `rigs/ryo-tripo.md`).

Rules: V1, F5, F6, G4, T0, E15; AM1, AM5. Decisions kept: nine tails as one mesh instanced, not rigged by the generator; full mask stays on.

**Open gate, stated plainly:** YOK-146's G4 (retarget gate) for the Tripo set is **still OPEN**. Ryo's Tripo rig is ACCEPT for the test branch only (provisional); the clips are Meshy clips retargeted by script, and the real Tripo/mocap clips do not exist yet (`rigs/ryo-tripo.md`, "G4 status"). Open flags from that ticket that touch this job: 54k triangles against a 20k budget, painted-on props across the hip crease failing deep-crouch volume, and a groin pleat tearing in floor clips. This spec is written to avoid them (triangle cap, no painted-on props), but spending before G4 closes is a bet that the Tripo skeleton survives. Designer's call: wait, or approve and accept the risk.

### The export preset and skeleton from YOK-146
`assets/specs/rigs/tripo-export.md`, named in the ticket, does **not exist** on main. The settings below come from what YOK-146 recorded in `assets/specs/rigs/ryo-tripo.md` and `skeleton.md`: Tripo auto-rig with the **Mixamo preset** (65 joints, `mixamorig:` prefix, 40 finger joints), exported as GLB. The Tripo version, task id and credits of the Ryo run were not recorded; the designer should write them in the Takes table this time. If a `tripo-export.md` was intended, rig-specialist should write it from these lines.

Target skeleton: the shared 25 bones in `skeleton.md` (no fingers, no prefix, metres). `tools/blender/canon_tripo_rig.py` builds it from the raw export, so the export only has to carry the standard Mixamo names.

### Inputs
| Use | File | Note |
|---|---|---|
| Body, front (and side, back) | a **tail-less** version of `docs/design/characters/kitsune/kitsune-astra-turnaround.png` | The Astra sheet is settled (T-pose, mask on, hair, bell, geta), but its nine tails hide the legs and back and Tripo would fuse them into the body (the G4 failure mode). Needs an Astra rerun, brief below |
| Tail | crop of one tail from the same Astra sheet (lowest left tail, front view) | One tail, isolated on white |
| Fallback body ref | `docs/design/characters/kitsune/kitsune-meshy-front-notails.png` | Older, A-pose, flat. Only if Astra is not rerun |

Astra brief addition (`astra-brief.md`, designer runs): "The same turnaround of the same character, same pose, outfit, hair, mask and art style, with all nine tails removed so the legs and the back of the hakama are visible. T-pose, feet shoulder width apart, arms straight out, plain white background, no effects."

### Tripo settings (designer runs)
Tripo version, plan and per-job credits are **not verifiable from the repo**. The figures are working estimates from Tripo's published tiers as I know them; the designer confirms each against the figure the Tripo UI shows before running (AM5 open question YOK-115).

| # | Step | Tripo setting | Est. credits |
|---|---|---|---|
| T0 | Prepare images | Tail-less turnaround (Astra, designer) and tail crop. Not a Tripo job | 0 |
| T1 | Body: image (or multiview) to model | Latest standard model; multiview with front, side, back if offered, else the front only; T-pose; texture ON, **PBR OFF**; face limit about **20,000 triangles** (Ryo came back at 54k); triangles, not quad; no stylize preset; not A-pose | ~30-40 |
| T2 | Body: auto-rig | Tripo auto-rig, humanoid, **Mixamo preset** (as Ryo), fingers on (stripped later by script), export GLB with rig | ~25 |
| T3 | Tail: image to model | Single tail crop; standard model; texture ON, PBR OFF; face limit about 4,000; no rig | ~25-30 |

Per-take total (UNVERIFIED estimate): **about 80-95 Tripo credits**. Take cap 3, for the whole chain or any subset. Cheapest option: keep the accepted Meshy `tail.glb` and run T1+T2 only (about 55-65). Designer to choose.

Texture note: Ryo's Tripo base colour had folds and shading painted in and needed an Astra four-view paint-over (`assets/specs/texture/ryo-tripo/README.md`). White fur and haori show this first; budget the same designer-run pass if T1 shows it. No Tripo credits.

### Prompt (only if Tripo takes a text hint with the image)
Style block first (`assets/specs/style.md`), then:
> Same fox-spirit fighter as the image, no tails: full white fox mask with red markings, eye slits only, fox ears, long white hair tipped orange hanging down the back, cropped white haori with wide sleeves and red trim over an indigo high-neck top, orange sash with indigo cord and a small bell, shrine-red pleated hakama, black split-toe socks, wooden geta. T-pose, feet shoulder width apart, flat cel colours. Palette: fur white #FBF6EC, foxfire #F2A23A, shrine red #B5332B, indigo #2D3A5E. Avoid: [negative prompt].

Tail:
> A single fluffy fox tail on its own, white fur with a foxfire-orange tip, gentle S-curve, flat cel colours. Avoid: [negative prompt], body, hands, fire.

### Rig notes for rig-specialist
Target: shared 25-bone skeleton, 1.65 m (the Meshy Kitsune height), origin at feet, Y-up, +Z facing, metres: `canon_tripo_rig.py`, then a Kitsune weight repair (`repair_weights.py`, as for Oni and Kappa). Expect the two known Tripo faults (joints without transforms; inverse binds in a Z-up frame).

| Part | Mesh or body | Plan |
|---|---|---|
| Nine tails | **Separate mesh** (T3 or the Meshy tail), instanced 9x | `BoneAttachment3D` on `Hips`, or `X_Tail_<n>` bones added by script under `Hips` (`skeleton.md`, "Attachment points"). Tick-driven sway, no physics. Re-pivot to the tail root and scale to about 0.5 m (the Meshy tail pivots at its centre, bbox about +-0.95 m) |
| Hakama | Part of the body (wide pleated skirt-trouser) | Highest G4 risk: one volume hides both legs. Ask T1 for a visible gap between the legs at the hem. Pelvis and knee repair steps are ready if it tears |
| Hair | Part of the body, behind the shoulders | Clips the hakama back and tail mounts in kick and crouch. Optional `X_Hair_0..2` chain bones by script |
| Haori sleeves | Part of the body | Clip in heavy and throw; risk accepted in take 2 |
| Mask, ears | Part of the head | Rigid |
| Bell, cord, tassels | Part of the body | Small, baked. Check they are not painted flat across the hip crease (the YOK-146 deep-crouch fault) |

### Acceptance criteria (checked on import)
- **Look (V1):** mask covers the face fully (hard fail if a face appears); orange-tipped white hair; fox ears; palette within tolerance; flat tones, no baked shading; reads as a fox at 720p with and without the tails.
- **Consistency:** 1.65 m; proportions and outline weight match the accepted Tripo Ryo.
- **Mesh:** at or under 20,000 triangles (flag above 25,000); one material; clean T-pose, arms within 10 degrees of horizontal; legs not fused; origin at the feet; tail is one clean piece with an orange tip.
- **Rig:** Mixamo names present; after `canon_tripo_rig.py` it matches `skeleton.md` and passes `rig_check.py`; stress poses no worse than Ryo's Tripo rig (55 edges; volume FAIL only for painted props).
- **G4 readiness:** hakama, hair, sleeve and tail clearance recorded for walk, heavy, throw. This take cannot close the open Tripo G4.
- **Licence:** Tripo plan and commercial terms of the output UNKNOWN (YOK-115); flag.

### Fallback after 3 failed takes
Shorten the hakama or tie the hair up (new brief), or the folklore human guise (new spec). Not run without approval.

### Takes (Tripo, designer fills in)
| Job | Tripo task id | Version and settings used | Credits (est/actual) |
|---|---|---|---|
| T1 body | | | ~30-40 / |
| T2 rig | | | ~25 / |
| T3 tail | | | ~25-30 / |
