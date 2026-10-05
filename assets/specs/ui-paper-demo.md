# Minimal UI textures for the demo - OPTIONAL spec

Ticket: YOK-29. Status: APPROVED (YOK-30, U1-U3, 9 credits). Was optional: needed only if ui-designer will not use flat Godot-drawn placeholders for the YOK-39 HUD (health bars, timer, round-win seals). Designer decides.

All `meshy_text_to_image`, `nano-banana`, 1:1, 3 credits each, then cleaned into tileable or alpha textures.

| # | Texture | Prompt (after style block) |
|---|---|---|
| U1 | Rice-paper panel | Plain rice-paper texture, warm off-white #F1E8D4, subtle fibres, flat, tileable, no text |
| U2 | Brush stroke bar | One bold horizontal black ink brush stroke on white background, rough dry-brush ends, no text |
| U3 | Red seal | A round vermilion #B5332B hanko seal stamp with an abstract unreadable mark, on white, no real characters |

First pass 9. Take cap 3. Acceptance: tileable or clean alpha, palette match, no readable text. Hit sparks and level presets are not part of this demo.

## Takes
Take 1, 2026-10-05 (approved).

| Job | Task id | Credits (est/actual) |
|---|---|---|
| U1 paper | 01a10e15-94b8-73fa-b49d-83c9547aca85 | 3/3 |
| U2 brush bar | 01a10e15-9826-72ea-8e29-07d0f6cb9c72 | 3/3 |
| U3 red seal | 01a10e15-9bc3-75ce-b033-a11886b07533 | 3/3 |

Total 9/9. Outputs: `game/assets/generated/ui/paper-panel.png`, `brush-bar.png`, `red-seal.png`.

## Acceptance
- U1 paper: **PASS after crop** (warm off-white fibre texture inside a pale grey border; crop the inner square; tileability unverified).
- U2 brush bar: **PASS after cleanup**: real-looking ink stroke, but photographic on white paper, no alpha. Threshold to alpha; style mismatch is minor.
- U3 red seal: **RETAKE (3 credits, not approved)**. Glossy 3D red button with a real readable kanji, violating "no real characters" and the matte flat style. Suggested prompt: "flat 2D red circular seal imprint, abstract meaningless brush marks, matte, top-down, white background, no 3D, no letters".
- Files under `game/assets/generated/`. Licence: Meshy plan tier and commercial terms of generated output are NOT verifiable from the MCP (balance call only); UNKNOWN, designer to confirm on the plan before shipping.
