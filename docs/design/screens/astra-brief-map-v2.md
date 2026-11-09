# Astra brief: map v2, the handscroll pass (YOK-109)

**Status: take 2 run 2026-11-09 from the designer's review of take 1.** The designer asked for an Astra pass on the map (2026-11-09): "make the scroll like the scroll on the start screen, have sketches of the paths with mist for the upcoming routes, make the fight selections more stylised (masks instead of just squares and a wooden stand for the shop), make the routes less linear and more windy". This replaces [M1](astra-brief-map-hud-twist.md#m1--map-style-rewardpng), which predates AM18 (the handscroll and the reveal). It is a concept target for the screen, not a game-rule change.

## The designer's pointers and how the prompt carries them

| Pointer | In the concept |
|---|---|
| Scroll like the start screen | The paper is the title screen's reason scroll (`game/assets/generated/ui/title-reason-scroll.png`): plain rice paper, thin ink outline, a wooden roller at each end with indigo end caps and brass rings. No pine silk mount, no sheet seams. Left roller holds the road behind Ryo, right roller the road ahead. |
| Sketches of the paths with mist for upcoming routes | Past the choosable row, faint pencil-and-thin-ink sketch lines carry on and dissolve into layered mist bands. No nodes, names or icons in the mist. |
| Masks instead of squares | Each yokai node is its mask, drawn as on the title screen (red horned Oni, white-and-red fox Kitsune, green beaked Kappa with the water dish), with a small paper name tag beneath. |
| Wooden stand for the shop | The merchant node is a small wooden market stand: plank counter, two posts, a short cloth noren and a hanging lantern. |
| Routes less linear, more windy | Nodes sit at staggered heights, never on one line. Routes meander in S-curves and switchbacks around small sketched landmarks (pines, rocks, a stream with a footbridge, a little torii). |

## Rule check

- **AM18 / R2 (reveal).** AM18 says later rows "stay blank paper". The mist sketches are the designer's request and change that wording, so the concept keeps them **uninformative**: no node, name, type or twist shows beyond row 5, and the sketched lines must not be read as the real route graph. The designer chose the true topology after take 1: [AM19](../gdd-amendments.md#am19).
- **R2.** Every shown node names its yokai. **Y3.** Elder nodes carry their twist seal (shield = armour, opposing arrows = reflect).
- **T6.** The copy-target chip stays on the map.
- **Tanuki never drawn.** Row 9 is hidden in this state, so the concept has no boss node.
- **Kitsune.** Fox masks only; no face.
- **Apprentice (row 1)** is a human rival, not a yokai. The concept gives him a plain unpainted wooden practice mask. Designer to confirm.
- **Elders** reuse their yokai's mask, larger, with a twisted straw rope and white paper streamers across the top (shimenawa and shide) plus the red twist seal. Nine-Tails = the fox mask with brass trim.

## State shown

Mid-run, the same state as `docs/screenshots/current/map_midrun_walked.png`: rows 1–4 cleared along Apprentice → Oni → Kappa → Merchant (here), row 5 choosable (Elder Oni, Nine-Tails selected). Chip, status, focus panel, legend and footer keep the live game's wording.

## References attached

| Image | Why |
|---|---|
| `docs/screenshots/current/map_midrun_walked.png` | Layout, chrome, wording |
| `game/assets/generated/ui/title-reason-scroll.png` | The scroll |
| `docs/design/screens/title.png` | Mask designs, wooden stands, finished art hand |
| `docs/design/screens/map.png` | Previous map concept: chip, status, legend, brushed walked route |

## Exact Astra prompt (MAP2)

```text
Stylised toon-shaded game art, flat cel colour in 2-3 tone steps, clean confident ink-brush outline of even weight, muted ukiyo-e colour palette, matte surfaces, soft even lighting with no cast shadows, no photographic or painterly texture, no gloss, no baked ambient occlusion, simple readable shapes. Use case: ui-mockup. One full-bleed 1672 x 941 landscape screen concept (nominal 16:9), opaque PNG. Match the attached title screen's art hand and the existing paper-talisman screen kit: bold serif body text, brush headings only, flat matte paper, restrained ink outlines. Palette: indigo #2D3A5E, ink #1D1B21, persimmon #D8632C, rice paper #F1E8D4, pine #34483B, fur white #FBF6EC, foxfire #F2A23A, shrine red #B5332B, brass #B08A48, wood dark #4A3425, wood mid #7A5535. Positions are percentages of the full canvas. Exact English lettering is required; no filler text. Tanuki is NEVER drawn, including portrait, silhouette, animal, mask or disguise. Kitsune appears only as her white-and-red fox mask; no face. Avoid photorealism, PBR, gloss, painterly noise, grain, neon, cast shadows, particles, extra UI, real kanji, watermark, logo, signature, franchise or artist references.

Screen: the run map of a roguelite, mid-run. Night bamboo valley backdrop (pale moon, red torii, bamboo) shows only above and below the scroll.

TOP BAR y3-13: top-left heading "Choose your path" in brush lettering on the night backdrop. Top-centre rice-paper chip with double persimmon border and a small blue wave icon: "COPY TARGET" above "SPIRIT WAVE", "Lv 3" on the right. Top-right rice-paper status: "RYO · 840 / 1000", red health bar 84% full, "75 coins", "row 4 of 8 done".

THE SCROLL y15-80, x1-99: one long horizontal handscroll drawn exactly like the attached rolled scroll: plain cream rice paper with a thin black ink outline and faint edge lines, a round wooden roller standing at each end (left roller x1-3, right roller x97-99) with indigo end caps and thin brass rings, paper wound around them. No silk border, no green mount, no seams. Across the bottom third of the paper, very soft grey sumi-wash hills and a few sketched pines, low contrast. Small brush row labels along the top edge of the paper: "ROW 1" x10, "ROW 2" x26, "ROW 3" x42, "ROW 4" x57, "ROW 5" x71.

NODES are illustrated objects sitting on the paper, each about 9% wide, with a small torn rice-paper name tag under it (bold serif name, tiny type letter at the tag's left). Heights are staggered, never in a line:
- Row 1 x10 y58: plain unpainted pale-wood practice mask with simple ink brows; tag "D  Apprentice". Cleared.
- Row 2 x26 y36: red horned Oni mask exactly as on the title screen; tag "D  Oni". Cleared.
- Row 3 x42 y62: green beaked Kappa mask with the water dish on its head, as on the title screen; tag "D  Kappa". Cleared.
- Row 4 x57 y40: small wooden market stand: plank counter on two posts, little shingled roof, short indigo noren cloth, a paper lantern hanging from the roof corner, a few wrapped goods on the counter; tag "M  Merchant" and under it persimmon "YOU ARE HERE". Ringed by a loose persimmon brush circle.
- Row 5 x71 y28: larger red Oni mask crowned with a twisted straw rope and white zigzag paper streamers; small round red seal with a white shield at its upper right; tag "E  Elder Oni". Reachable: thin indigo brush ring.
- Row 5 x74 y62: larger white-and-red fox mask with brass trim, crowned with the same straw rope and paper streamers; small round red seal with white opposing arrows at its upper right; tag "E  Nine-Tails". Selected: glowing foxfire-and-brass brush halo and a small persimmon triangle pointing at it from the left.
Cleared masks are drained to grey ink wash on the paper, with a small red tick stamp; still readable.

ROUTES are winding brush paths, never straight: they curve in S-bends and switchbacks, loop around a cluster of pines, dip under a tiny arched footbridge over a thin stream between rows 2 and 3, and pass a small sketched torii near row 4. The walked route Apprentice -> Oni -> Kappa -> Merchant is one thick persimmon dry-brush stroke with tapered ends and bristle streaks. From the Merchant stand two thinner black sumi brush paths wind to the two row-5 masks.

MIST from x80 to the right roller: beyond row 5 the paths continue only as faint pencil-and-thin-ink sketch lines, a few branching wandering trails, broken and unfinished, fading out into soft layered white-grey mist bands that drift across the paper. Nothing else in the mist: no masks, no tags, no icons, no labels, no row labels.

BOTTOM y82-93 left x3-60: rice-paper focus panel, red seal with white opposing arrows at the left: "NINE-TAILS · Elder duel · row 5"; red "Twist: Reflects a special used twice in a row"; "Pays 50 coins. Bind it and draft an ability (a rare can appear)."; "Habit to punish: often jumps after a knockdown."
Bottom right x62-97 y82-93: dark indigo legend: small grey mask "cleared", persimmon ring "here", indigo ring "reachable", foxfire halo "selected"; red shield seal "Armour on every special"; red opposing-arrows seal "Reflects a special used twice in a row"; "D duel · E elder duel · M merchant".
FOOTER y95-99 key chips: "ARROWS / D-PAD" "Choose route", "ENTER / A" "Go", "WHEEL / R-STICK" "Pan scroll", "Q E / LB RB" "Page".
No square cards anywhere, no extra nodes, no boss, no prices, no prototype annotations.
```

## What Astra must not change

- No node, label or icon beyond row 5. The mist carries no information.
- The walked route stays persimmon brushed ink (Q16, 2026-10-25); open routes black sumi.
- Twist seals keep their meaning: shield = armour, opposing arrows = reflect.
- No Tanuki. No Kitsune face. No square node cards.

## Astra run record (2026-11-09)

One take, `gpt-6-astra` via `codex exec`, the four references attached in the order above. 1672×941, opaque, no annotations. Codex does not report the credit cost.

| ID | Output | Verdict | Notes |
|---|---|---|---|
| MAP2 | [map-v2-take1.png](astra/map-v2-take1.png) | Accept as the target, pending the designer | Scroll matches the title scroll (rollers, indigo caps). Masks match the title art; elders carry the rope, streamers and the right seals; the merchant stand, YOU ARE HERE ring and the Nine-Tails halo are right; the walked route winds past a footbridge and a torii; sketched trails fade into mist with nothing in it. Lettering is clean. Misses: the cleared Oni and Kappa masks keep full colour (only the red tick marks them), and the mist is a narrow band (x83–95) rather than a third of the paper. Once accepted it replaces `map.png`. |

## Take 2 (2026-11-09)

The designer's review of take 1: the mist sketches follow the real routes (AM19); the Apprentice's plain mask is fine; retake with no row headings, mist covering the routes ahead, a different heading font top left, and elder masks built on the normal masks. The heading font is now the kit's bold serif (the in-game heading's face). Nodes move left (rows 1–5 at x8–62) so the mist and the sketched rows 6–8 fit on screen.

### Exact Astra prompt (MAP2, take 2)

```text
Stylised toon-shaded game art, flat cel colour in 2-3 tone steps, clean confident ink-brush outline of even weight, muted ukiyo-e colour palette, matte surfaces, soft even lighting with no cast shadows, no photographic or painterly texture, no gloss, no baked ambient occlusion, simple readable shapes. Use case: ui-mockup. One full-bleed 1672 x 941 landscape screen concept (nominal 16:9), opaque PNG. Match the attached title screen's art hand and the existing paper-talisman screen kit: bold serif body text, brush headings only, flat matte paper, restrained ink outlines. Palette: indigo #2D3A5E, ink #1D1B21, persimmon #D8632C, rice paper #F1E8D4, pine #34483B, fur white #FBF6EC, foxfire #F2A23A, shrine red #B5332B, brass #B08A48, wood dark #4A3425, wood mid #7A5535. Positions are percentages of the full canvas. Exact English lettering is required; no filler text. Tanuki is NEVER drawn, including portrait, silhouette, animal, mask or disguise. Kitsune appears only as her white-and-red fox mask; no face. Avoid photorealism, PBR, gloss, painterly noise, grain, neon, cast shadows, particles, extra UI, real kanji, watermark, logo, signature, franchise or artist references.

Screen: the run map of a roguelite, mid-run. Night bamboo valley backdrop (pale moon, red torii, bamboo) shows only above and below the scroll.

TOP BAR y3-13: top-left heading "Choose your path" set large in the same bold serif typeface as the body text, rice-paper colour, on the night backdrop; not brush script, not handwritten. Top-centre rice-paper chip with double persimmon border and a small blue wave icon: "COPY TARGET" above "SPIRIT WAVE", "Lv 3" on the right. Top-right rice-paper status: "RYO · 840 / 1000", red health bar 84% full, "75 coins", "row 4 of 8 done".

THE SCROLL y15-80, x1-99: one long horizontal handscroll drawn exactly like the attached rolled scroll: plain cream rice paper with a thin black ink outline and faint edge lines, a round wooden roller standing at each end (left roller x1-3, right roller x97-99) with indigo end caps and thin brass rings, paper wound around them. No silk border, no green mount, no seams. Across the bottom third of the paper, very soft grey sumi-wash hills and a few sketched pines, low contrast. No row labels or headings anywhere on the paper.

NODES are illustrated objects sitting on the paper, each about 9% wide, every mask the same size, with a small torn rice-paper name tag under it (bold serif name, tiny type letter at the tag's left). Heights are staggered, never in a line:
- Row 1 x8 y58: plain unpainted pale-wood practice mask with simple ink brows; tag "D  Apprentice". Cleared.
- Row 2 x21 y36: red horned Oni mask exactly as on the title screen; tag "D  Oni". Cleared.
- Row 3 x34 y62: green beaked Kappa mask with the water dish on its head, as on the title screen; tag "D  Kappa". Cleared.
- Row 4 x47 y40: small wooden market stand: plank counter on two posts, little shingled roof, short indigo noren cloth, a paper lantern hanging from the roof corner, a few wrapped goods on the counter; tag "M  Merchant" and under it persimmon "YOU ARE HERE". Ringed by a loose persimmon brush circle.
- Row 5 x60 y28: the very same red horned Oni mask as row 2, identical design, colours and size, with only a twisted straw rope and white zigzag paper streamers draped across its top added; small round red seal with a white shield at its upper right; tag "E  Elder Oni". Reachable: thin indigo brush ring.
- Row 5 x62 y62: the white-and-red fox mask exactly as on the title screen, same size as the other masks, no brass trim, with only the same straw rope and white zigzag paper streamers added; small round red seal with white opposing arrows at its upper right; tag "E  Nine-Tails". Selected: glowing foxfire-and-brass brush halo and a small persimmon triangle pointing at it from the left.
Cleared masks are drained to grey ink wash on the paper, with a small red tick stamp; still readable.

ROUTES are winding brush paths, never straight: they curve in S-bends and switchbacks, loop around a cluster of pines, dip under a tiny arched footbridge over a thin stream between rows 2 and 3, and pass a small sketched torii near row 4. The walked route Apprentice -> Oni -> Kappa -> Merchant is one thick persimmon dry-brush stroke with tapered ends and bristle streaks. From the Merchant stand two thinner black sumi brush paths wind to the two row-5 masks. The elder masks differ from ordinary masks only by the rope, streamers and seal.

MIST covers the paper from x68 to the right roller: a broad bank of soft layered white-grey mist drifting over all the routes ahead. Under the mist the routes continue as faint pencil-and-thin-ink sketch lines, half hidden: from each row-5 mask two lines lead on and cross to two unmarked meeting points, each of those splits to two more meeting points, each of those to two more, and the last two lines join into a single line that runs on into the mist toward the right roller. Meeting points are just where lines join: no masks, dots, circles, tags, icons or labels anywhere in the mist.

BOTTOM y82-93 left x3-60: rice-paper focus panel, red seal with white opposing arrows at the left: "NINE-TAILS · Elder duel · row 5"; red "Twist: Reflects a special used twice in a row"; "Pays 50 coins. Bind it and draft an ability (a rare can appear)."; "Habit to punish: often jumps after a knockdown."
Bottom right x62-97 y82-93: dark indigo legend: small grey mask "cleared", persimmon ring "here", indigo ring "reachable", foxfire halo "selected"; red shield seal "Armour on every special"; red opposing-arrows seal "Reflects a special used twice in a row"; "D duel · E elder duel · M merchant".
FOOTER y95-99 key chips: "ARROWS / D-PAD" "Choose route", "ENTER / A" "Go", "WHEEL / R-STICK" "Pan scroll", "Q E / LB RB" "Page".
No square cards anywhere, no extra nodes, no boss, no prices, no prototype annotations.
```

### Take 2 run record (2026-11-09)

One take, `gpt-6-astra` via `codex exec`, with take 1, the title scroll and the title screen attached. 1672×941, opaque.

| ID | Output | Verdict | Notes |
|---|---|---|---|
| MAP2 take 2 | [map-v2-take2.png](astra/map-v2-take2.png) | Superseded by take 3 (the designer preferred take 1's mist) | No row headings. The heading is in the kit's bold serif. Elder Oni is the row-2 Oni mask plus the rope, streamers and shield seal; Nine-Tails is the title fox mask plus the same rope and the reflect seal. A wide mist bank (x70–97) covers the routes ahead, with sketched lines branching and crossing under it and nothing else in it. Miss: the sketched lines only suggest branching and do not show the real 2→2→2→1 shape exactly; the build draws them from the route data (AM19), so this does not reach the game. Cleared masks still keep their colour, marked by the red tick. |

## Take 3 (2026-11-09)

The designer's review of take 2: keep take 1's trailing mist (wisps at the right edge, not a wide bank), and drop the row count ("row 4 of 8 done") from Ryo's status panel. Everything else as take 2.

### Exact Astra prompt (MAP2, take 3)

```text
Stylised toon-shaded game art, flat cel colour in 2-3 tone steps, clean confident ink-brush outline of even weight, muted ukiyo-e colour palette, matte surfaces, soft even lighting with no cast shadows, no photographic or painterly texture, no gloss, no baked ambient occlusion, simple readable shapes. Use case: ui-mockup. One full-bleed 1672 x 941 landscape screen concept (nominal 16:9), opaque PNG. Match the attached title screen's art hand and the existing paper-talisman screen kit: bold serif body text, brush headings only, flat matte paper, restrained ink outlines. Palette: indigo #2D3A5E, ink #1D1B21, persimmon #D8632C, rice paper #F1E8D4, pine #34483B, fur white #FBF6EC, foxfire #F2A23A, shrine red #B5332B, brass #B08A48, wood dark #4A3425, wood mid #7A5535. Positions are percentages of the full canvas. Exact English lettering is required; no filler text. Tanuki is NEVER drawn, including portrait, silhouette, animal, mask or disguise. Kitsune appears only as her white-and-red fox mask; no face. Avoid photorealism, PBR, gloss, painterly noise, grain, neon, cast shadows, particles, extra UI, real kanji, watermark, logo, signature, franchise or artist references.

Screen: the run map of a roguelite, mid-run. Night bamboo valley backdrop (pale moon, red torii, bamboo) shows only above and below the scroll.

TOP BAR y3-13: top-left heading "Choose your path" set large in the same bold serif typeface as the body text, rice-paper colour, on the night backdrop; not brush script, not handwritten. Top-centre rice-paper chip with double persimmon border and a small blue wave icon: "COPY TARGET" above "SPIRIT WAVE", "Lv 3" on the right. Top-right rice-paper status: "RYO · 840 / 1000", red health bar 84% full, "75 coins". Nothing else in that panel: no row count, no progress text.

THE SCROLL y15-80, x1-99: one long horizontal handscroll drawn exactly like the attached rolled scroll: plain cream rice paper with a thin black ink outline and faint edge lines, a round wooden roller standing at each end (left roller x1-3, right roller x97-99) with indigo end caps and thin brass rings, paper wound around them. No silk border, no green mount, no seams. Across the bottom third of the paper, very soft grey sumi-wash hills and a few sketched pines, low contrast. No row labels or headings anywhere on the paper.

NODES are illustrated objects sitting on the paper, each about 9% wide, every mask the same size, with a small torn rice-paper name tag under it (bold serif name, tiny type letter at the tag's left). Heights are staggered, never in a line:
- Row 1 x8 y58: plain unpainted pale-wood practice mask with simple ink brows; tag "D  Apprentice". Cleared.
- Row 2 x21 y36: red horned Oni mask exactly as on the title screen; tag "D  Oni". Cleared.
- Row 3 x34 y62: green beaked Kappa mask with the water dish on its head, as on the title screen; tag "D  Kappa". Cleared.
- Row 4 x47 y40: small wooden market stand: plank counter on two posts, little shingled roof, short indigo noren cloth, a paper lantern hanging from the roof corner, a few wrapped goods on the counter; tag "M  Merchant" and under it persimmon "YOU ARE HERE". Ringed by a loose persimmon brush circle.
- Row 5 x60 y28: the very same red horned Oni mask as row 2, identical design, colours and size, with only a twisted straw rope and white zigzag paper streamers draped across its top added; small round red seal with a white shield at its upper right; tag "E  Elder Oni". Reachable: thin indigo brush ring.
- Row 5 x62 y62: the white-and-red fox mask exactly as on the title screen, same size as the other masks, no brass trim, with only the same straw rope and white zigzag paper streamers added; small round red seal with white opposing arrows at its upper right; tag "E  Nine-Tails". Selected: glowing foxfire-and-brass brush halo and a small persimmon triangle pointing at it from the left.
Cleared masks are drained to grey ink wash on the paper, with a small red tick stamp; still readable.

ROUTES are winding brush paths, never straight: they curve in S-bends and switchbacks, loop around a cluster of pines, dip under a tiny arched footbridge over a thin stream between rows 2 and 3, and pass a small sketched torii near row 4. The walked route Apprentice -> Oni -> Kappa -> Merchant is one thick persimmon dry-brush stroke with tapered ends and bristle streaks. From the Merchant stand two thinner black sumi brush paths wind to the two row-5 masks. The elder masks differ from ordinary masks only by the rope, streamers and seal.

TRAILING MIST at the right end, exactly as in the second attached image (the earlier take): from each row-5 mask the routes carry on to the right as faint thin-ink and pencil sketch lines, a few branching and crossing trails, broken and unfinished, that drift into soft trailing wisps of white-grey mist along the right edge of the paper (about x80 to the right roller) and fade out there. The paper between the row-5 masks and the mist stays plain and light. Nothing in the mist or on the trails: no masks, dots, circles, tags, icons or labels.

BOTTOM y82-93 left x3-60: rice-paper focus panel, red seal with white opposing arrows at the left: "NINE-TAILS · Elder duel · row 5"; red "Twist: Reflects a special used twice in a row"; "Pays 50 coins. Bind it and draft an ability (a rare can appear)."; "Habit to punish: often jumps after a knockdown."
Bottom right x62-97 y82-93: dark indigo legend: small grey mask "cleared", persimmon ring "here", indigo ring "reachable", foxfire halo "selected"; red shield seal "Armour on every special"; red opposing-arrows seal "Reflects a special used twice in a row"; "D duel · E elder duel · M merchant".
FOOTER y95-99 key chips: "ARROWS / D-PAD" "Choose route", "ENTER / A" "Go", "WHEEL / R-STICK" "Pan scroll", "Q E / LB RB" "Page".
No square cards anywhere, no extra nodes, no boss, no prices, no prototype annotations.
```

### Take 3 run record (2026-11-09)

One take, `gpt-6-astra` via `codex exec`, with take 2 (composition), take 1 (mist) and the title screen attached. 1672×941, opaque.

| ID | Output | Verdict | Notes |
|---|---|---|---|
| MAP2 take 3 | [map-v2-take3.png](astra/map-v2-take3.png), copied to [map.png](map.png) | Accept as the map target; **mist reference is take 1** | Ryo's panel now reads only "RYO · 840 / 1000", the health bar and "75 coins". Everything else matches take 2. Miss: the mist is lighter but still almost as wide as take 2's bank, not take 1's narrow trailing wisps. The game draws the mist in code (`MapScroll`), so the build follows [take 1](astra/map-v2-take1.png) for the mist: wisps along the right edge of the paper (about x83–97 of the screen), with the sketched trails drawn from the real routes (AM19) fading into them. |
