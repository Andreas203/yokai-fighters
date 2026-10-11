# Reward and target card art - spec (demo moves only)

Status: PROPOSED, awaiting designer approval. **No Meshy job has been run and no generating tool was called.** Prices from earlier specs (`nano-banana` 3), not re-verified; balance not checked.

Targets: `docs/design/screens/reward.png` (landscape illustration inside each tall talisman card), `modifier-target.png` and `pause.png` (white glyph on a coloured diamond). Names, tags, badges (F, S, W), numbers and card text are live controls. The demo has four moves in play: **Foxfire** (Kitsune special), **Spirit Wave** and **Rising Talisman** (Ryo's starters), **Will-o'-wisp** (Kitsune modifier). Move ids in `data/moves/`. No other move gets art in this pass; every other move uses the fallback glyph.

## Rules for every image
- No readable text, no real kanji or letters (the Rising Talisman strip carries abstract unreadable brush marks only).
- No characters, hands, faces or masks in card art: the art is the effect, so Kitsune's mask question never arises and no Tanuki can appear.
- Flat cel in 2-3 tone steps with an ink outline; the concept's painterly noise is not a target (V1).
- Source colours (V5): Foxfire in foxfire #F2A23A with persimmon #D8632C edges; Spirit Wave in Ryo's ink and indigo #2D3A5E with pine #34483B shadow and paper-white crests; Rising Talisman in rice paper #F1E8D4 with a shrine red #B5332B seal and ink swirl; Will-o'-wisp in indigo #2D3A5E with a lighter indigo tint and paper-white cores (the concept's bright blue is outside the palette, flagged below).
- Delivered on **plain pure white** so the engine can use a `multiply` blend over the paper card (white vanishes, no alpha cut needed); a cut-to-alpha copy is made only if a card is dimmed.

## Jobs
All `meshy_text_to_image`, `ai_model: nano-banana`, 3 credits per take, take cap 3, retake needs fresh approval. Prompt = style block (`assets/specs/style.md`) + line below + "Avoid:" the shared negative plus "readable text, real kanji or letters, characters, hands, faces, masks, 3D render, gloss, photographic, glow halo, drop shadow".

| # | Asset | Aspect | Prompt line | Output | Credits/take |
|---|---|---|---|---|---|
| C1 | Foxfire illustration | 3:2 | One slow drifting ball of foxfire flame in foxfire #F2A23A with persimmon #D8632C edges and a paper-white core, three smaller fading copies trailing behind it to the left, a few ember flecks, two flat ink-wash cloud bands behind in ink and indigo, centred, plain pure white background | `game/assets/generated/ui/cards/card-foxfire.png` | 3 |
| C2 | Spirit Wave illustration | 3:2 | One large curling ink-brush wave in indigo #2D3A5E and pine #34483B with paper-white foam crests, rolling to the right, thick brush outline, two flat ink-wash cloud bands behind, centred, plain pure white background | `.../cards/card-spirit-wave.png` | 3 |
| C3 | Will-o'-wisp illustration | 3:2 | Three small floating wisp flames, each a teardrop flame with a paper-white core, indigo #2D3A5E body and a lighter indigo tint at the tips, different sizes, drifting upward, thin ink wisp trails between them, two flat ink-wash cloud bands behind, plain pure white background | `.../cards/card-will-o-wisp.png` | 3 |
| C4 | Rising Talisman illustration | 3:2 | One upright rice-paper #F1E8D4 talisman strip with a shrine red #B5332B stamp mark and abstract meaningless ink brush marks (no letters), flying upward, with a spiralling ink-black energy swirl rising around it and a few small paper scraps, two flat ink-wash cloud bands behind, centred, plain pure white background | `.../cards/card-rising-talisman.png` | 3 |
| C5 | Glyph sheet (4 move glyphs plus fallback) | 3:2 | A tidy grid of six equal cells in two rows of three on plain white, five of them filled, each a single solid ink-black #1D1B21 silhouette glyph with no inner detail lines: (1) a flame, (2) a curling wave, (3) three small teardrop flames, (4) an upright paper talisman strip with a spiral above it, (5) a plain blank talisman strip with a small round dot. Sixth cell empty. No frames, no text, no labels | `.../cards/glyphs-sheet.png` (split into `glyph-foxfire.png`, `glyph-spirit-wave.png`, `glyph-will-o-wisp.png`, `glyph-rising-talisman.png`, `glyph-fallback.png`) | 3 |

C5 turns to white-on-alpha after cutting the cells; the engine tints and sets them on the diamond tiles (diamond is a Godot polygon or StyleBox, not generated: indigo for Ryo's moves as in the concept, persimmon for talisman, red for Kitsune's flame, and a pale grey diamond for the fallback). If the sheet comes back with unequal cells or merged glyphs, the retake is five single glyph calls (15 credits per take; needs fresh approval).

**Fallback glyph** (C5 cell 5): blank talisman strip with a dot, used by any move lacking art (every other move, and any move added later).

## Slicing
Not 9-slice. C1-C4 display at about 370x250 px in a card, aspect 3:2, stretched to fit width, cropped vertically, `multiply` over K2/paper. Glyphs display at 64-96 px on a diamond.

## Rig plan
Not applicable. 2D UI textures.

## Acceptance criteria
1. Look (V1): flat tones, ink outline, source-colour palette above, matte, no glow haloes, no painterly noise or smoke noise; each reads at 370 px and at 50% size.
2. Distinct silhouettes: flame vs wave vs three wisps vs upright strip, and clearly different from each other when greyed out (the Modifier target screen greys cards).
3. No readable text, no real kanji; the talisman marks are abstract.
4. No characters, hands, faces, masks or creatures in any image.
5. Consistency: C1-C4 share outline weight, cloud-band motif and paper-white core treatment; set matches K1/K2 paper tone when multiplied.
6. Clean white background: multiply result leaves no visible box edge on cream paper.
7. C5: five glyphs, each a single solid shape, separable by a simple grid cut; reads at 64 px.
8. Licence UNKNOWN (plan terms not verifiable from the MCP). Designer to confirm before shipping.

## Fallback after 3 failed takes per job
- C1-C4: show the glyph (C5) large in the card art area on a flat ink-wash cloud shape (StyleBox and the K4 stroke), no illustration.
- C5: the blank-talisman glyph drawn as a Godot polygon, other glyphs as Unicode-free simple `Polygon2D` shapes (flame, wave, spiral) kept to one colour.
- Use `ui-paper-demo.md` U2 (`brush-bar.png`) thresholded as the cloud band.

## Credit estimate
First pass C1-C5 = 15 credits; worst case at 3 takes each = 45. Minimum useful set C5 alone = 3 (worst 9): the modifier-target and pause screens work from glyphs; the reward screen is the only one needing C1-C4.

## Takes
None. Nothing run, 0 credits spent.

| Job | Task id | Credits (est/actual) |
|---|---|---|
| C1 Foxfire | not run | 3/0 |
| C2 Spirit Wave | not run | 3/0 |
| C3 Will-o'-wisp | not run | 3/0 |
| C4 Rising Talisman | not run | 3/0 |
| C5 glyph sheet | not run | 3/0 |

## Acceptance
Pending (no outputs yet).

## Designer questions
1. Approve C1-C5 (15), or C5 only (3) with the reward cards using glyph plus ink cloud?
2. Will-o'-wisp colour: the concept uses a bright blue outside the palette. Keep palette indigo (spec), or add one "wisp blue" to `style.md`? Foxfire is orange, so the pair must stay clearly different.
3. Rising Talisman art is a strip flying upward with an ink swirl. Does that match the move's clip (`ryo-rising-talisman-strike`), or should it show Ryo's strike (would need a character; not proposed)?
