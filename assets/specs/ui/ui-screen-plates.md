# Screen kit plates — YOK-astra-screen-kit

## Pause feedback — diamond move icons (2026-10-11)

Designer approved four built-in imagegen jobs. Saved under `game/assets/generated/ui/`: `ability-icon-spirit-wave.png`, `ability-icon-rising-talisman.png`, `ability-icon-foxfire.png`, `ability-icon-fallback.png`. Each source is 1254×1254, resized with Godot Image Lanczos to 256×256, preserving alpha; no nine-slice. All four were visually inspected: text-free, no characters, blank talisman, transparent exterior. The sibling manifest `game/ui/ability_art.tres` maps ability ids to textures, with a generic pine energy-ring fallback. Icons also supply faint right-hand motifs. Existing paper-card and blossom plates are reused; no fonts added.

Exact executed prompts:

### spirit-wave

Create one standalone square game UI diamond move icon, tight framing with small transparent padding, genuine transparent alpha outside diamond. Thin rough ink #1D1B21 diamond border, flat matte ukiyo-e cel ink-brush illustration, rice paper #F1E8D4 motif. Bold readable silhouette at 90px. No lettering, numbers, real kanji, watermark, people, weapons, masks, Tanuki or scenery. Palette only indigo #2D3A5E, ink #1D1B21, rice #F1E8D4, pine #34483B, shrine red #B5332B, persimmon #D8632C. Indigo diamond containing a curling rice-white spirit wave.

### rising-talisman

Create one standalone square game UI diamond move icon, tight framing with small transparent padding, genuine transparent alpha outside diamond. Thin rough ink #1D1B21 diamond border, flat matte ukiyo-e cel ink-brush illustration, rice paper #F1E8D4 motif. Bold readable silhouette at 90px. No lettering, numbers, real kanji, watermark, people, weapons, masks, Tanuki or scenery. Palette only indigo #2D3A5E, ink #1D1B21, rice #F1E8D4, pine #34483B, shrine red #B5332B, persimmon #D8632C. Persimmon diamond containing one blank rice-paper rectangular talisman rising with two sweeping trails. Talisman is completely unmarked.

### foxfire

Create one standalone square game UI diamond move icon, tight framing with small transparent padding, genuine transparent alpha outside diamond. Thin rough ink #1D1B21 diamond border, flat matte ukiyo-e cel ink-brush illustration, rice paper #F1E8D4 motif. Bold readable silhouette at 90px. No lettering, numbers, real kanji, watermark, people, weapons, masks, Tanuki or scenery. Palette only indigo #2D3A5E, ink #1D1B21, rice #F1E8D4, pine #34483B, shrine red #B5332B, persimmon #D8632C. Shrine-red diamond containing a rice-white three-tongued supernatural flame.

### fallback

Create one standalone square game UI diamond move icon, tight framing with small transparent padding, genuine transparent alpha outside diamond. Thin rough ink #1D1B21 diamond border, flat matte ukiyo-e cel ink-brush illustration, rice paper #F1E8D4 motif. Bold readable silhouette at 90px. No lettering, numbers, real kanji, watermark, people, weapons, masks, Tanuki or scenery. Palette only indigo #2D3A5E, ink #1D1B21, rice #F1E8D4, pine #34483B, shrine red #B5332B, persimmon #D8632C. Pine diamond containing a rice-white abstract swirling energy ring, neutral generic special ability.


Designer authorization: this run explicitly approves built-in image generation and cropping the screen concepts. Built-in imagegen was used, not Meshy or an API fallback. Every reference below is in `docs/design/screens/` unless stated otherwise. All plates are under `game/assets/generated/ui/`. No existing asset was removed. `red-seal.png` is not used by this kit.

The only lettering exception is `title-wordmark.png`, containing **YOKAI FIGHTERS**. Everything else is text-free. The three reward illustration crops contain paper and illustration only; their names, source labels, tags, descriptions, and values remain live controls. They are presentation examples, not changes to move data.

## Inventory

Margins are source pixels, left/top/right/bottom. None means ordinary aspect-preserving art, not nine-sliced. All generated alpha is preserved. Cropping/resizing used Godot Image with Lanczos filtering. No fonts were added.

| File | Source | Saved size | Nine-slice margins | Consumer |
|---|---|---|---|---|
| `night-backdrop.png` | Generated, `story-card.png`, prompt N | 1672×941 | None | `kit_backdrop.tscn` default `Art` |
| `title-shrine.png` | Generated, `title.png`, prompt T | 1672×941 | None | Title proof, backdrop `Art` override |
| `rice-sheet.png` | Generated, `settings.png`, prompt P | 960×640 | 32/32/32/32 | `paper_sheet_style.tres`, PaperSheet / PaperDialog; HUD study |
| `rice-card.png` | Generated, `reward.png`, prompt C | 384×576 | 28/28/28/28 | `paper_card_style.tres`, PaperCard |
| `red-brush.png` | Generated, `pause.png`, prompt R | 960×200 | None (stretch) | MenuRow / RedButton; live reward tags |
| `ink-underline.png` | Generated, `settings.png`, prompt U | 1024×40 | None (stretch) | KitHeading; title proof divider |
| `title-wordmark.png` | Generated, `title.png`, prompt L | 800×450 | None | Title proof; **lettering exception** |
| `fox-mask.png` | Generated, `settings.png`, prompt F | 384×358 | None | Reward corner and Kitsune HUD portrait; swappable TextureRect |
| `ryo-portrait.png` | Generated, `hud.png` style + `docs/design/characters/ryo/ryo-meshy-front.png` identity, prompt Y | 480×320 | None | Ryo HUD portrait; unarmed, high indigo collar |
| `burst-blank.png` | Generated, `hud.png`, prompt B | 192×192 | None | HUD burst ornament, separate live glyph |
| `selection-frame.png` | Generated, `reward.png`, prompt S | 384×576 | 32/32/32/32, centre disabled | KitCard `Stroke` NinePatchRect |
| `blossom-corner.png` | Generated, `story-card.png`, prompt O | 320×260 | None | CornerOrnament default `Art`, mirrored by corner |
| `reward-foxfire.png` | Cropped `reward.png`, rectangle (277,464,350,194) | 350×194 | None | Reward proof card illustration |
| `reward-spirit-wave.png` | Cropped `reward.png`, rectangle (681,474,329,181) | 329×181 | None | Reward proof card illustration |
| `reward-will-o-wisp.png` | Cropped `reward.png`, rectangle (1067,474,333,184) | 333×184 | None | Reward proof card illustration |

The key chip keeps its StyleBox frame: generating a plate would not improve its simple outline. The gallery HUD study uses the shared rice sheet; the live HUD now uses only portraits and the blank seal, with no paper backing. Kitsune's portrait reuses the full-face mask ornament. Selection uses a nine-slice border with `draw_center=false`, so the generated inner tint cannot obscure card contents.

## Exact executed prompts

### N — night-backdrop

Edit this reference into a clean 16:9 game menu backdrop, same composition and same ink-brush flat toon cel colour matte hand. Remove the entire central paper UI rectangle and all text, seals and UI; seamlessly continue the bamboo valley, forest, waterfall and paths behind it. Keep moon upper right, red torii, tiny lanterns, deep indigo #2D3A5E, ink #1D1B21, pine #34483B, rice #F1E8D4, shrine red #B5332B, persimmon #D8632C. No lettering, numbers, kanji, watermark, people, masks or Tanuki. Output only the scenery.

### T — title-shrine

Edit the reference into a text-free 16:9 title shrine illustration for a game. Precisely retain the three masks on the right (white red Kitsune full face mask, red Oni, green Kappa), shrine table, bamboo, red sun and indigo mountains, same hand and flat matte toon ink-brush style. Remove ALL lettering and UI: title logo, Start Continue Practice Settings Quit, underline, Choose your path, ENTER Confirm, their brush highlight. Reconstruct clean rice-paper coloured negative space on the left for live menu controls. No text, numbers, kanji, watermark, Tanuki or people. Palette indigo #2D3A5E ink #1D1B21 rice #F1E8D4 pine #34483B shrine red #B5332B persimmon #D8632C.

### P — rice-sheet

Extract/re-create ONLY the blank wide rice-paper sheet from this concept as a standalone transparent game UI plate. Landscape 3:2 sheet, nearly rectangular with subtle fine torn fibrous edges, pale warm rice #F1E8D4 softly mottled flat matte paper matching reference. Empty uniform centre, 9-slice-safe straight edges with all edge irregularity within outer 24 pixels, no large tears, no border or shadow. Sheet fills image with small transparent padding. Remove ALL text, lines, controls, mask, scenery, ornaments and seals. No lettering, numbers, kanji or watermark. Genuine alpha outside sheet.

### C — rice-card

Create one blank small portrait reward card plate from this reference, only the rice-paper card and fine ink border. Flat matte pale rice #F1E8D4 paper, subtle texture, straight nearly rectangular finely torn edges, thin irregular ink #1D1B21 brush outline inset slightly. Empty centre. 9-slice-safe: all border within outer 24 pixels. Tight framing with small genuine transparent alpha padding outside paper. Remove every letter, number, icon, illustration, seal, selection glow, scenery and UI. No kanji or watermark. Match reference hand, no chunky polygonal edges.

### R — red-brush

Extract and recreate ONLY the broad horizontal red dry-brush selection stroke behind Resume, with the icon and lettering removed. One standalone horizontal stroke, about 6:1 aspect ratio, uneven bristled ends, dense opaque centre for readable live text, small dry flecks along edges. Matte shrine red #B5332B, same ink-brush hand as reference. Tight transparent canvas, true alpha around brush and in tiny dry gaps. No lettering, numbers, symbols, kanji, paper, scenery, border, shadow or watermark.

### U — ink-underline

Extract/recreate ONLY a long thin black ink underline like the horizontal divider below Settings. One horizontal dry-brush line, broad at left then gently tapers to right, around 30:1 aspect ratio within transparent landscape canvas. Colour ink #1D1B21; same matte ink brush hand. Genuine alpha outside and in dry gaps. No text, letters, numbers, kanji, symbols, paper, scenery, shadow or watermark.

### L — title-wordmark

Extract and recreate ONLY the YOKAI FIGHTERS two-line title wordmark as a single transparent lettering plate. Exact text YOKAI on top in ink #1D1B21, FIGHTERS below in shrine red #B5332B. Match reference bold angular dry ink brush hand, energy, slant, stacked composition. Tight framing with breathing room, no clipping. Genuine alpha background. NO sun, paper, scenery, masks, UI, extra text, kanji or watermark. This is the title logo lettering only.

### F — fox-mask

Extract ONLY the upper-right fox mask ornament as a standalone transparent UI ornament. The entire white Kitsune face mask, black outline, red markings and red hanging cords/tassels, angled as in reference, no person beneath it. Match the exact reference hand: flat matte toon cel colour, ink-brush edges. Mask entirely covers face. Tight portrait composition but no clipping. True transparent alpha surrounding it. No paper, black swash, scenery, UI, text, numbers, kanji, watermark, Tanuki or other characters.

### Y — ryo-portrait

Create a standalone transparent bust portrait for Ryo's left HUD, facing right in three-quarter profile. Image 1 is ONLY the flat matte cel colour and ink-brush illustration STYLE reference; image 2 defines Ryo's IDENTITY and clothing. Black undercut hair with a long lock, sharp eyes, vermilion mark under left eye, high indigo #2D3A5E collar covering mouth to nose, indigo kimono-cut coat with small spiral brass button, plain persimmon ofuda earring with NO lettering. Unarmed, no weapon, no sword, no headband, no white gi. Head and shoulders only, rough ink lower cutout edge, genuine alpha background. Rice #F1E8D4, ink #1D1B21, shrine red #B5332B, persimmon #D8632C. No UI, text, numbers, kanji, watermark, other characters or Tanuki.

### B — burst-blank

Recreate ONLY the circular red burst seal from the HUD as a standalone transparent UI plate, removing the B completely. Solid blank shrine red #B5332B disk with irregular dry-brush circumference and a broken rice-paper #F1E8D4 circular ring near the edge. Flat matte ink-brush hand identical to reference. Tight square framing, true alpha outside circle. No glyphs, lettering, numbers, kanji, UI, paper rectangle, scenery, people, watermark or Tanuki.

### S — selection-frame

Extract/recreate ONLY the selected reward card's red brush border and subtle persimmon outer rim as a standalone transparent portrait rectangular frame. Straight edges, small irregular dry-brush flecks, shrine red #B5332B main thin stroke with persimmon #D8632C outer stroke. 9-slice-safe all marks within outer 24 pixels at 512x768. Centre entirely transparent; outside entirely transparent. No paper fill, art, letters, numbers, symbols, kanji, watermark or scenery. Match reference ink brush matte hand. No luminous neon glow.

### O — blossom-corner

Extract/recreate ONLY the small top-left decorative black ink cloud and red plum blossom twig from the paper corner of this reference. Standalone corner ornament with black horizontal cloud streaks along the top and a delicate red blossom branch descending the left side, centre/right kept empty. Flat matte toon ink-brush, ink #1D1B21 shrine red #B5332B pine #34483B. True transparent alpha background; no rice paper, rectangle, lettering, kanji, symbols, numbers, watermark, scenery or people.

## Processing provenance

Generated source canvases: N/T 1672×941; P 1536×1024; C/S 1024×1536; R/U 2172×724; L 1671×941; F 1299×1211; Y 1536×1024; B 1254×1254; O 1672×941. Crops before resizing: P (32,32,1472,960); C (24,32,976,1472); R (28,130,2144,430); U (24,300,2148,128); S (32,32,960,1472); O (0,0,1160,941). Other plates resized from the full canvas. Reward crops are unscaled. No full-screen concept is shipped as UI.

## Review limits

All 11 concepts and both existing 1080p gallery pages were opened before changes; generated outputs were visually reviewed. Acceptance of final rendered integration requires working windowed Godot captures. The system serif is still a stand-in for the brush headings; OS fallback changes metrics across machines. The designer must choose licensed distributable body and brush fonts in later work. No purchase or font download was made.


## Designer-feedback revision — horizontal title reason scroll

Designer explicitly approved generation for this run. Built-in imagegen, one job; no fonts added. New `title-reason-scroll.png`: transparent text-free rolled-end handscroll, saved 960×256. Source 2172×724, alpha-used crop (15,9,2143,705), Godot Image Lanczos resize. Horizontal three-slice margins 72/0/72/0; title NinePatchRect at (64,854), 1380×204. Live 28px ink copy occupies the central paper. All unavailable entry reasons are listed while any exist, so keyboard/pad users need not focus disabled rows; scroll hides when all entries are available. Hover tooltips and existing focus/input semantics remain.

Exact prompt:

Create a standalone game UI plate: one blank horizontal Japanese handscroll unrolled left to right, rolled vertical paper ends at both left and right. Wide landscape 6:1 scroll, tightly framed with small transparent padding. Flat matte ukiyo-e ink-brush cel illustration. Warm pale rice paper #F1E8D4, fine ink #1D1B21 outlines, tiny indigo #2D3A5E end details. Uniform opaque blank centre for live text. Horizontal three-slice safe: rolled ends entirely in outer 12% on each side, straight parallel top/bottom across centre, no perspective, no sag, no centre decoration. Genuine transparent alpha outside the scroll. No lettering, numbers, kanji, symbols, seals, watermark, people, weapons, masks, Tanuki or scenery. Deliver only the text-free scroll plate.

Generated plate visually inspected: blank opaque paper, rolled ends, alpha exterior, no lettering or characters. The scroll replaces the concept’s underline/footer copy with router-owned availability reasons. Integration pixel review remains with the orchestrator. HUD feedback adds no art and removes both rice-paper panels entirely.

## YOK-159 shop plates (Astra), acceptance 2026-10-20

**Provenance.** Designer-approved 2026-10-20. Run by the main session through `codex exec -m gpt-6-astra`, one image per run; Codex does not report cost, so spend is unrecorded. No image job was run in this pass. Raw plates are in `docs/design/screens/astra/`. S4 is take 2 **repaired by script** (`tools/ui/repair_slip.py`): background removal had eaten the flat centre in both takes, so the script fills the inside of the inner ink border with flat paper, makes the paper opaque (Astra left alpha at 247-252) and clears coloured fringe. This is a repair, not a clean Astra output; the raw take is `ledger-slip-raw-take2.png`. Licence of Astra output: UNKNOWN, designer to confirm before shipping.

**Method.** `tools/ui/import_shop_plates.py` (Pillow 12.3, Lanczos; Pillow premultiplies RGBA on resize so no matte bleeds). Before resizing, alpha >= 246 is snapped to 255 on S1, S2, S3, S5 (Astra leaves the paper and wood body at alpha 253-254, effectively but not truly opaque; S4 was already fixed by the repair). Crops only keep the brief's aspect ratio. No existing plate was overwritten. Margins below were checked by a nine-slice stretch test at four sizes per plate.

**Checks common to all.** No text, glyph, fake glyph, number or kanji on any plate (each viewed in full). Partial-alpha edge pixels are dark ink or paper colour; a scan for white, yellow, green or magenta matte in partial alpha found 0 to 0.05% of partial pixels on S1-S3 (no fringe). S5 has about 470 yellow or green partial pixels out of 977k (negligible; hat straw and bundle edge). No cast shadow or halo on any plate. Palette sampled from opaque pixels: S1 wood #503729/#5E412E/#815B3F with straw #C3AA7F (brief: wood #4A3425/#7A5535, straw #C9B58C; slightly dark and desaturated, within tolerance); S2/S3/S4 paper #EBE0CB to #F0E2C8 vs rice paper #F1E8D4 (a little warmer and darker, fine); S4 pin shrine red; S5 kimono #685342 (brown #6E5A48), sash #A93E31 (shrine red #B5332B), skin #EABE95, hat #967149; B1 indigo #2C3551/#1C2844 with persimmon lanterns and an off-white moon.

| ID | Plate | Verdict | Saved (in `game/assets/generated/ui/`) | Nine-slice L/T/R/B |
|---|---|---|---|---|
| S1 | shop cabinet | ACCEPT with notes | `shop-cabinet.png` 800x704 | none |
| S2 | price tag | ACCEPT | `price-tag.png` 392x140 | 108/16/24/16 (brief's 76 cuts the string hole) |
| S3a | bubble body | ACCEPT | `speech-bubble.png` 640x200 (brief 640x320; the art is 3.3:1, so height follows the art) | 56/56/56/56 |
| S3b | bubble tail | ACCEPT | `speech-bubble-tail.png` 128x160 | none |
| S4 | ledger slip | ACCEPT (script-repaired) | `ledger-slip.png` 400x560 | 52/72/72/48 (brief's 28 leaves the pin in the stretch zone) |
| S5 | merchant portrait | ACCEPT (take 3, designer) | `merchant-portrait.png` 432x768 | none |
| B1 | shop backdrop | ACCEPT | `shop-backdrop.png` 1672x941 (opaque RGB, unchanged) | none |

**S1 cabinet** (raw 1341x1173, cropped x 4..1337 to the exact 800:704 ratio, scale 0.6002). Body alpha 255, exterior 0; alpha bbox at saved size x 6..793, y 32..665. Measured at saved size, to place the 9 cells (a cell is the straw back panel above a board; items stand on the board top):
- Opening, straw panels: x 97 to 703 (side-wall shading runs x 66..97 and 703..735; shelf boards span x 68..733).
- Row 1 cavity y 101 to 235 (board top y 236, board 236..284). Row 2 cavity y 285 to 416 (board top 417, board 417..467). Row 3 cavity y 468 to 574 (board top 575, board 575 to about 625, base below to 665).
- Board tops (item stand line): y 236, 417, 575. Column thirds across x 97..703 are 202 wide, centres x 198, 400, 602.
- Cell heights 134, 131, 107: **row 3 is about 20% shorter than rows 1 and 2.** The brief asks for equal rows, so this is a strict miss. Place per row from the offsets above. Retake only if the designer wants equal rows.
- The back panel carries a flat darker band under each cornice and board (two tone steps) and shaded side walls. That is cel shading, not a gradient, and reads as shelf depth; flagged against "no baked shading" for the designer. Plank seams are fine ink lines, no glyphs.
- Cabinet outline is thinner and more rendered than the paper plates; acceptable beside them.

**S2 tag** (raw 2073x758, crop y 9..749, scale 0.189). Alpha bbox at saved size x 8..383, y 15..124; string hole at about x 71..87; the pointed left end runs to about x 106; string loop x 8..70. Stretch test at 520x140, 300x140 and 392x180 is clean for width stretch; a height stretch squashes the hole, so keep height 140. Paper has faint fibre flecks (flat colour, no noise); string is grey-black.

**S3** (raw sheet 2161x728, split at the empty columns 1771..1848). Body bbox 5..634 x 5..194 at 640x200, ink border 10 to 15 px with a rough hand edge inside the 56 margin; stretch tests at 640x200, 900x200, 500x260 and 420x150 are clean. Tail bbox 9..119 x 5..154: its top edge has no ink line (open to merge into the body), so draw the tail behind the body so the body's bottom border covers the seam, overlapping about 10 px.

**S4 slip** (repaired; whole 1060x1484 canvas, scale 0.377). Paper bbox 29..373 x 29..533; inner ink border at x 41/361, y 42/520; pin x 334..360, y 40..65. Paper alpha 255 after the repair, centre flat (255 verified), exterior 0; the 1.1% partial pixels are torn-edge anti-aliasing in paper or ink colour. The pin sits in the top-right, so right >= 66 and top >= 68; the saved art carries 29 px of transparent padding, so the brief's 28 would leave the pin in the stretch zone. Stretch test at 400x560, 260x360, 300x500 and 480x700 is clean with 52/72/72/48. Honest record: the flat centre, opaque paper and fringe clearing are the script's work, not Astra's; the torn edge, inner border and pin are Astra's.

**S5 portrait** (raw 941x1672, whole canvas, scale 0.459). Fully human (straw hat, brown kimono, red sash, bundle), no ear, tail, whisker, leaf or fur tell; costume matches the brief. His near hand holds the bundle rope instead of being raised in welcome: a variation, not a retake. A rough dark brush cut runs along the hem, as the "rough ink lower cut" intends. About 10% transparent padding at the top and 8% at the bottom. Rendering is a little softer (hat and kimono carry two-tone shading) than `ryo-portrait.png`; acceptable.

**S5 retakes (designer, 2026-10-20).** The designer asked for the merchant to match Ryo's style. Take 2 (style image `ryo-portrait.png` plus take 1) was flatter but still soft. Take 3 (style image `ryo-portrait.png` plus take 2, with no costume sheet) was ACCEPTED by the designer: black dry-brush hatching for shadows, a flat hat with bold strokes, spiky black hair, heavy tapered outlines and a ragged lower edge, as in Ryo's portrait. It is still fully human, with the same costume and pose. Raw 941x1672, re-imported to 432x768 with the same method (alpha >= 246 snapped). Edge scan: 42k partial-alpha pixels, nearly all intended dry-brush edges; 981 coloured partial pixels, at take 1's level and not visible at 432x768. All three raw takes are kept, and the prompt is in the brief under "S5 take 3".

**B1 backdrop** (1672x941, opaque). Lantern stall at left (blank paper lanterns, blank noren cloth, gourds, bundles), bamboo framing both sides, torii and waterfall at far right, moon at x 65-76%, y 2-17% (upper right, not centre-left, so the sheet heading is clear). Centre band (x 30-65%) is mid-dark forest and mist with low-contrast detail; the brightest lights (lanterns) sit in the left edge band x 4-23%, mostly under the scroll sheet's left edge. No characters, animals, masks or readable signs. The calm-centre check is a visual judgement, not a measured contrast.

**Concerns for the UI Designer.** Place cells from the S1 offsets, not thirds of the plate; row 3 is shorter. Use the corrected margins above for S2 and S4. Draw the bubble tail behind the body. The bubble body is 200 high, not 320.
