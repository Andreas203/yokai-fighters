# HUD ornaments - spec

Status: PROPOSED, awaiting designer approval. **No Meshy job has been run and no generating tool was called.** Prices from earlier specs (`ryo.md` R1: `nano-banana-pro` image-to-image 9; `ui-paper-demo.md`: `nano-banana` 3), not re-verified; balance not checked.

Target: `docs/design/screens/hud.png`. Bars, meter segments, "B" letter, name and numbers are live controls on a K1/K3-style paper plate (`ui-screen-kit.md`).

## Concept corrections (do not copy the concept)
The HUD concept drew Ryo with a **sword**, a red headband and a white open jacket. The character sheet (`docs/design/characters/ryo/README.md`) has no sword and no headband: Ryo is **unarmed**; he fights with ofuda and ink energy. Keep from the sheet: black undercut hair with one lock over the right eye, high indigo collar to the nose, ofuda earring, vermilion binding mark under the left eye, indigo coat with brass spiral buttons, shimenawa rope over the shoulder. The portrait must follow the sheet, not the concept.

## Jobs
| # | Asset | Tool and settings | Credits/take |
|---|---|---|---|
| H1 | Ryo HUD portrait | `meshy_image_to_image`, `ai_model: nano-banana-pro`, `reference_file_paths: [docs/design/characters/ryo/ryo-meshy-front.png]`, strong reference (keep face, hair, coat and palette from the sheet), `generate_multi_view: false`, aspect 1:1 | 9 |
| H2 | Round blank burst seal | `meshy_text_to_image`, `ai_model: nano-banana`, aspect 1:1 | 3 |
| - | Kitsune corner ornament | **Reuse mask M1** from `ui-title-masks.md` (no new job). | 0 |

### H1 prompt
Style block from `assets/specs/style.md`, then:

> Head-and-shoulders bust portrait of the same young man as the reference, UNARMED, no weapon, no hands visible, turned three-quarters to face the viewer's right, calm focused eyes, black undercut hair with one lock over his right eye, high indigo #2D3A5E collar zipped up to the nose, vermilion #B5332B mark under his left eye, small ofuda paper earring, indigo coat shoulder with a brass #B08A48 spiral button and a persimmon #D8632C lining edge, shimenawa rope and paper streamer over the shoulder, flat cel colour, bold ink outline, cropped through the chest, plain pure white background, no frame.

Avoid: the shared negative plus "sword, katana, blade, hilt, weapon, headband, hands, text, glow, particles, gloss, shadow".

### H2 prompt
> One flat round vermilion #B5332B ink-stamp seal, slightly uneven brushy edge with small ink bleed, a plain empty centre (no mark, no letters, no symbol), matte, top-down, plain pure white background, no 3D, no shadow.

Avoid: the shared negative plus "readable text, real kanji or letters, 3D render, glossy, button, bevel, emboss". The "B" is a live Label over it. H2 **supersedes the unapproved U3 retake** in `ui-paper-demo.md` (same cost, better scoped); K6 in `ui-screen-kit.md` is the square tomoe seal variant, if ever wanted.

### Burst seal decision
`red-seal.png` (U3) does not cover the burst seal: glossy 3D disc with a real kanji. H2 is needed unless the designer accepts a flat red `StyleBoxFlat` circle (StyleBox-only, free; the concept's seal is a hand-stamped disc with a brush "B", so a StyleBox circle with a ring border is a weaker but acceptable placeholder).

### Kitsune corner ornament
The concept shows a fox mask tilted in the plate's top-right corner with red tassels. Reuse M1 (`mask-kitsune.png`), rotated about 10-15 degrees and scaled to about 220 px tall in engine, which also serves the Controls, Reward, Modifier target and Defeat screens. This only works if M1 is generated **with the tassel cords and bell** (see the title-mask extension in `ui-title-masks.md`); otherwise add tassels as a Godot `Line2D` (poor). Kitsune's full mask stays on in every image. The ornament is the mask only, never a face.

## Delivery and alpha
Both downloaded on white; cut to alpha (threshold). H1 is delivered at 1024 px, used at about 180-260 px. Place on the left of the plate so its shoulders overlap the torn plate edge (Control node outside the plate clip). Output paths: `game/assets/generated/ui/hud/portrait-ryo.png`, `.../hud/seal-burst.png`.

## Rig plan
Not applicable. 2D UI textures.

## Acceptance criteria
1. Look (V1): flat cel, even ink outline, palette matches the sheet and `style.md`, matte, no glow, no painterly noise. Reads at 200 px.
2. **Unarmed**: no sword, hilt, blade, or any held item. Fail on any weapon.
3. Likeness to the sheet: undercut with the lock over the right eye, collar to the nose, vermilion mark under the left eye, earring, indigo coat (not white), no headband. Consistent with the accepted Ryo model colours (`assets/specs/ryo.md`).
4. Alpha: clean cut, no white halo, no crop of the head.
5. H2: flat 2D, matte, no real character, no 3D bevel, empty centre, reads as a stamp at 64 px.
6. Licence: UNKNOWN (plan terms not verifiable from the MCP). Designer to confirm before shipping.

## Fallback after 3 failed takes
- H1: render a Godot `SubViewport` bust from the accepted Ryo model (head camera, toon shader), which is deterministic and always on-model.
- H2: `StyleBoxFlat` red circle with a thin ink ring.
- Kitsune ornament: reuse M1 without tassels, add a red `Line2D` cord.

## Credit estimate
First pass H1 + H2 = 12; worst case at 3 takes each = 36.

## Takes
None. Nothing run, 0 credits spent.

| Job | Task id | Credits (est/actual) |
|---|---|---|
| H1 | not run | 9/0 |
| H2 | not run | 3/0 |

## Acceptance
Pending (no outputs yet).

## Designer questions
1. H1 as `nano-banana-pro` (9) or try the 3-credit `nano-banana` with the same reference first? (Likeness risk is higher.)
2. Open sheet question 1 (collar over the mouth) is still undecided; H1 follows the current sheet (collar to the nose). Confirm.
3. H2, or StyleBox circle for the burst seal?
