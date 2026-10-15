# YOK-102 approval job list (Oni, Kappa, Tanuki concept art and models)

Status: DRAFT. Nothing run, nothing approved. Costs from the Meshy published table used in `yok-29-job-list.md` (nano-banana-pro 9, nano-banana 3, meshy-7.1 image-to-3d with texture 30, meshy-6-lite image-to-3d with texture 15, rig 5). Take cap 3 per chain.

Balance: `meshy_check_balance` was NOT callable in this session (the Meshy MCP tools were not connected to the spec run). Last recorded: 941 on 2026-10-06 after the gate, then 123 approved for clips (clips may not have used Meshy credits; check). Run `meshy_check_balance` before any approved job; stop if it does not cover the approved amount.

| ID | Job | Tool | Model | Settings (short) | Credits | Spec |
|---|---|---|---|---|---|---|
| O1 | Oni concept front | text_to_image | nano-banana-pro | 2:3, no weapon | 9 | oni.md |
| O2 | Oni turnaround | image_to_image | nano-banana-pro | multi-view, ref O1, T-pose | 9 | oni.md |
| K1 | Kappa concept front | text_to_image | nano-banana-pro | 2:3 | 9 | kappa.md |
| K2 | Kappa turnaround | image_to_image | nano-banana-pro | multi-view, ref K1, T-pose | 9 | kappa.md |
| T1 | Tanuki merchant concept | text_to_image | nano-banana-pro | 2:3 | 9 | tanuki.md |
| T2 | Tanuki true-form concept | text_to_image | nano-banana-pro | 2:3 | 9 | tanuki.md |
| O3 | Oni mesh + texture | image_to_3d | meshy-7.1 | 15k tri, 2K tex, no PBR, t-pose, ~2.0 m | 30 | oni.md |
| O4 | Club image | text_to_image | nano-banana | 1:1 | 3 | oni.md |
| O5 | Club mesh (separate, rigid) | image_to_3d | meshy-6-lite | 4k tri, 2K tex | 15 | oni.md |
| K3 | Kappa mesh + texture | image_to_3d | meshy-7.1 | 15k tri, 2K tex, no PBR, t-pose, ~1.5 m | 30 | kappa.md |
| K4 | Shell image (recommended, designer chooses) | text_to_image | nano-banana | 1:1 | 3 | kappa.md |
| K5 | Shell mesh (separate, rigid) | image_to_3d | meshy-6-lite | 3k tri, 2K tex | 15 | kappa.md |
| T3 | Tanuki true-form turnaround (OPTIONAL) | image_to_image | nano-banana-pro | multi-view, ref T2 | 9 | tanuki.md |
| T4 | Tanuki true-form mesh (OPTIONAL) | image_to_3d | meshy-7.1 | 15k tri, 2K tex, no PBR, t-pose | 30 | tanuki.md |
| G1 | Oni human-guise fallback chain (CONTINGENCY) | 2D + image_to_3d | as above | only if G4 gate fails | 48 | oni.md |
| G2 | Kappa human-guise fallback chain (CONTINGENCY) | 2D + image_to_3d | as above | only if G4 gate fails | 48 | kappa.md |

Mesh lines use the front view of the accepted O2/K2 turnaround (or the Astra turnaround, see conflict below).

## Totals

| | Oni (O1-O5) | Kappa (K1-K5) | Tanuki (T1-T2) | Required total | With optional T3-T4 |
|---|---|---|---|---|---|
| First pass | 66 | 66 | 18 | **150** | **189** |
| Worst case, 3 takes of every job | 198 | 198 | 54 | **450** | **567** |

- Concept lines only (O1, O2, K1, K2, T1, T2): 54 first pass, 162 worst case.
- Mesh and prop lines only (O3-O5, K3-K5): 96 first pass, 288 worst case.
- If the separate shell is dropped (K4, K5): -18 first pass, -54 worst case.
- Contingency, not in totals: G1 48 and G2 48 per take; clips (animator); any take beyond 3. Retakes are usually cheaper than a whole chain, so the worst case is a ceiling.

Next, NOT in this approval: rigging (`meshy_rig`, 5 each, 2.0 m Oni, 1.5 m Kappa), owned by `rig-specialist` with its own spec and approval; clips, G4 gate.

## Open conflict: who makes the turnarounds (designer to decide)

`docs/decision-log.md` (2026-10-11) records that Astra (the designer's own Codex run) makes the concept art and turnarounds that feed Meshy. AM1, rule F6 and the `asset-smith` definition say 2D comes from the Meshy 2D image models; the log lists "an amendment so AM1, F6 and asset-smith.md allow Astra turnarounds" as still pending. Not decided here.

The prompts in each spec are tool-neutral, so they double as the Astra brief (concept front, turnaround, prop image). Options:

| Option | What changes | Cost |
|---|---|---|
| A. Meshy 2D (as listed) | O1, O2, K1, K2, T1, T2 run on Meshy. Allowed today by F6 | 54 first pass |
| B. Astra for concept + turnaround | Drop those six lines; Astra delivers front/side/back T-pose images using the prompts; Meshy runs mesh lines only. Needs the pending amendment first | 96 first pass (150 minus 54), plus Astra's cost |
| C. Mixed | Astra for the concept fronts (O1, K1, T1, T2); Meshy turnaround (O2, K2) from them | 150 minus 36 = 114 |

Flags for any Astra art: T-pose, white background, no weapon or held items, same height and scale across views, flat cel look per `style.md` (a painterly Astra render raises the risk of baked shading in the 3D texture, a RETAKE reason).

## Decisions the designer must make

1. Turnaround source: A, B or C above (and ratify the amendment if B or C).
2. Approve which lines, at which credit amount (first pass 150, or a subset); retakes need new approval.
3. Palette additions to `style.md`: oni red skin (proposed `#A8372C`), kappa teal skin (`#3E8C84`), shell ochre (`#8A6A3A`), Tanuki brown (`#6E5A48`).
4. Oni and Kappa personalities and silhouettes (the vault marks the one-liners as proposed); heights (Oni ~2.0 m, Kappa ~1.5 m) vs the shared skeleton.
5. Tanuki: 2D concept art only (recommended, T0) or also a 3D true form for the reveal (T3-T4, +39)?
6. Kappa shell: baked (default) or separate mesh (K4-K5, +18)?
7. Whether human-guise fallbacks (G1, G2) are pre-approved or only after a failed G4 gate.
8. Licence: Meshy plan tier and commercial terms of output still unknown (rules.md open question).
