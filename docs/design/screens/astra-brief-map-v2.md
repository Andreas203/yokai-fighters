# Astra brief: map v2, the handscroll pass (YOK-109)

**Status: run 2026-11-09 from the designer's pointers.** The designer asked for an Astra pass on the map (2026-11-09): "make the scroll like the scroll on the start screen, have sketches of the paths with mist for the upcoming routes, make the fight selections more stylised (masks instead of just squares and a wooden stand for the shop), make the routes less linear and more windy". This replaces [M1](astra-brief-map-hud-twist.md#m1--map-style-rewardpng), which predates AM18 (the handscroll and the reveal). It is a concept target for the screen, not a game-rule change.

## The designer's pointers and how the prompt carries them

| Pointer | In the concept |
|---|---|
| Scroll like the start screen | The paper is the title screen's reason scroll (`game/assets/generated/ui/title-reason-scroll.png`): plain rice paper, thin ink outline, a wooden roller at each end with indigo end caps and brass rings. No pine silk mount, no sheet seams. Left roller holds the road behind Ryo, right roller the road ahead. |
| Sketches of the paths with mist for upcoming routes | Past the choosable row, faint pencil-and-thin-ink sketch lines carry on and dissolve into layered mist bands. No nodes, names or icons in the mist. |
| Masks instead of squares | Each yokai node is its mask, drawn as on the title screen (red horned Oni, white-and-red fox Kitsune, green beaked Kappa with the water dish), with a small paper name tag beneath. |
| Wooden stand for the shop | The merchant node is a small wooden market stand: plank counter, two posts, a short cloth noren and a hanging lantern. |
| Routes less linear, more windy | Nodes sit at staggered heights, never on one line. Routes meander in S-curves and switchbacks around small sketched landmarks (pines, rocks, a stream with a footbridge, a little torii). |

## Rule check

- **AM18 / R2 (reveal).** AM18 says later rows "stay blank paper". The mist sketches are the designer's request and change that wording, so the concept keeps them **uninformative**: no node, name, type or twist shows beyond row 5, and the sketched lines must not be read as the real route graph. Whether the built sketches follow the true topology (which would leak the route shape) is open; if the designer wants that, it needs a line on AM18.
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
