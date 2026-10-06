# YOK-29 approval job list (demo slice: Ryo vs Kitsune, bamboo grove)

Balance checked 2026-10-05 (free call): 1300 credits. Nothing run. Approve line by line. Costs from the Meshy MCP published table. Take cap 3 per chain.

| ID | Job | Tool | Model | Settings (short) | Credits | Spec |
|---|---|---|---|---|---|---|
| R1 | Ryo turnaround | image_to_image | nano-banana-pro | multi-view, ref ryo front | 9 | ryo.md |
| R2 | Ryo mesh + texture | image_to_3d | meshy-7.1 | 15k tri, 2K tex, no PBR, t-pose | 30 | ryo.md |
| R3 | Ryo rig (+walk/run) | rig | Meshy auto-rig | 1.75 m | 5 | ryo.md |
| K1 | Kitsune turnaround (no tails) | image_to_image | nano-banana-pro | multi-view, ref no-tails front | 9 | kitsune.md |
| K2 | Kitsune mesh + texture | image_to_3d | meshy-7.1 | 15k tri, 2K tex, no PBR, t-pose | 30 | kitsune.md |
| K3 | Kitsune rig | rig | Meshy auto-rig | 1.65 m | 5 | kitsune.md |
| K4 | Tail image | image_to_image | nano-banana | ref tails front | 3 | kitsune.md |
| K5 | Tail mesh (instanced 9x, not rigged) | image_to_3d | meshy-6-lite | 4k tri, 2K tex | 15 | kitsune.md |
| S1 | Grove backdrop 16:9 | text_to_image | nano-banana-pro | 16:9 | 9 | bamboo-grove-dusk.md |
| S2 | Bamboo cluster image | text_to_image | nano-banana | 1:1 | 3 | bamboo-grove-dusk.md |
| S3 | Bamboo cluster mesh | image_to_3d | meshy-6-lite | 3k tri, 2K tex | 15 | bamboo-grove-dusk.md |
| S4 | Lantern image | text_to_image | nano-banana | 1:1 | 3 | bamboo-grove-dusk.md |
| S5 | Lantern mesh | image_to_3d | meshy-6-lite | 3k tri, 2K tex | 15 | bamboo-grove-dusk.md |
| S6 | Ground strip | text_to_image | nano-banana | 16:9 | 3 | bamboo-grove-dusk.md |
| U1-U3 | Paper, brush bar, red seal (OPTIONAL) | text_to_image | nano-banana | 1:1 | 9 | ui-paper-demo.md |

## Totals

| | Ryo | Kitsune | Stage | Required | With optional UI |
|---|---|---|---|---|---|
| First pass | 44 | 62 | 48 | **154** | **163** |
| Worst case, 3 takes of every job | 132 | 186 | 144 | **462** | **489** |

Not included: Kitsune human-guise fallback (G4), 44 per take, only if clip-matcher's gate fails; clips (clip-matcher); any take beyond 3. Retakes are usually cheaper than a whole chain (e.g. R2+R3 = 35), so the worst case is a conservative ceiling.

## Designer decisions (2026-10-05)

1. Nine tails, one mesh instanced 9x, not rigged (rigid hip attachments, tick-driven sway).
2. T-pose for all meshes.
3. Optional UI textures U1-U3 approved.
4. **Approved spend: first pass only, R1-R3, K1-K5, S1-S6, U1-U3 = 163 credits.** Retakes are not approved; each needs a new approval.

## Open items (original, for reference)

1. Kitsune tail count for the demo: 3 (sheet default, recommended) or 9 (ticket wording).
2. A-pose vs T-pose: references are A-pose; Meshy advises T-pose for rigging. If R3 or K3 fails, retake the mesh with `pose_mode: t-pose` (30 credits each). Approve this contingency or not.
3. Licence: plan tier is an open question (rules.md). Commercial terms of generated output are UNKNOWN and must be confirmed on the designer's plan before any asset counts as shippable; treat the demo as non-final until then.
4. Optional UI textures: generate, or use Godot-drawn placeholders.

## Results (YOK-30, 2026-10-05)

Balance 1300 before, 1137 after: **163 credits spent, equal to the approved 163**. No retakes run. Prompts were condensed to fit the tool's 600-character limit.

| Job | Task id | Credits | Verdict |
|---|---|---|---|
| R1 | 01a10e15-7ddd-721f-8a9a-6f01c06b18de | 9 | PASS |
| R2 | 01a10e18-621a-73e8-9ecd-193c46618713 | 30 | PASS (provisional) |
| R3 | 01a10e19-f774-707d-9b31-3e94ac0acdcd | 5 | PASS |
| K1 | 01a10e15-8089-70cf-a57b-7b72c5850df8 | 9 | PASS WITH DEFECT (grey smudges) |
| K2 | 01a10e18-660f-771b-b8de-f7f71792ece1 | 30 | PASS WITH DEFECT |
| K3 | 01a10e1a-063d-7300-8ecd-5b42ad89b086 | 5 | PASS |
| K4 | 01a10e15-82bd-747d-b7b1-3e3390a91729 | 3 | PASS |
| K5 | 01a10e18-6a0b-72fa-b646-ad75b3210f3b | 15 | PASS |
| S1 | 01a10e15-8773-723d-b136-1d23063c0abb | 9 | PASS (crop border) |
| S2 | 01a10e15-8892-71a5-ba90-3fce2857b258 | 3 | PASS |
| S3 | 01a10e18-6da0-7768-890d-83095f28f92b | 15 | PASS |
| S4 | 01a10e15-8e05-75b8-abda-39423e71b9bd | 3 | PASS |
| S5 | 01a10e18-7297-74ea-99c7-564eeba84286 | 15 | PASS |
| S6 | 01a10e15-91b3-7043-817f-24196649546d | 3 | RETAKE recommended (3) |
| U1 | 01a10e15-94b8-73fa-b49d-83c9547aca85 | 3 | PASS (crop) |
| U2 | 01a10e15-9826-72ea-8e29-07d0f6cb9c72 | 3 | PASS (alpha cleanup) |
| U3 | 01a10e15-9bc3-75ce-b033-a11886b07533 | 3 | RETAKE (3) |

Retakes awaiting approval: U3 (3), S6 (3), optional Kitsune K1-K3 (44).
