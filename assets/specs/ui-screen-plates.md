# Screen kit plates — YOK-astra-screen-kit

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

The key chip keeps its StyleBox frame: generating a plate would not improve its simple outline. The HUD uses the shared rice sheet, portrait and blank seal instead of a redundant dedicated paper texture. Kitsune's portrait reuses the full-face mask ornament. Selection uses a nine-slice border with `draw_center=false`, so the generated inner tint cannot obscure card contents.

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
