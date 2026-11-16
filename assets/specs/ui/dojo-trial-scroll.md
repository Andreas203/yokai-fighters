# Dojo trial scroll (YOK-98 follow-up)

**Status: designer asked for it 2026-11-15** ("use astra to come up with a more fitting scroll"), after trying the code-drawn trial scroll on branch `YOK-dojo-trial-scroll`. Batch cap: **3 takes** of the one prompt below; no retakes with a changed prompt without the designer's yes.

## What it replaces

The dojo trial's quest is shown twice. Both places draw their scroll in code today:
- The opening card reuses the elder twist's hanging scroll (`TwistScroll`).
- During the attempts, a strip under the HUD (`TrialScrollBanner`) has rods at the top and bottom.

A wide banner with rods at the top and bottom reads as a wall scroll laid on its side. A **handscroll (makimono) unrolled sideways, with its rollers at the left and right ends**, fits a wide strip. One nine-slice plate serves both sizes: the 1180 x 210 banner and the opening card.

## Plate

| ID | Plate | Generate | Saved at | Alpha | Nine-slice (saved px, L/T/R/B) | Raw | Imports to |
|---|---|---|---|---|---|---|---|
| T1 | Unrolled handscroll, blank paper | wide, about 3:1 | 1200 x 400 (Lanczos) | transparent outside the scroll | measured at import: rollers and caps in L/R, deckled edges in T/B | `docs/design/screens/astra/dojo-trial-scroll-take<n>.png` | `game/assets/generated/ui/dojo-trial-scroll.png` |

References attached: `game/assets/generated/ui/rice-card.png`, `rice-sheet.png` (kit paper and ink).

## Prompt (copy-paste)

Stylised toon-shaded game art, flat cel colour in 2-3 tone steps, clean confident ink-brush outline of even weight, muted ukiyo-e colour palette, matte surfaces, soft even lighting with no cast shadows, no photographic or painterly texture, no gloss, no baked ambient occlusion, simple readable shapes. One standalone unrolled Japanese handscroll (makimono) for a game UI banner, seen straight from the front, flat, perfectly horizontal and symmetrical, a very wide landscape image of about 3:1, tight framing with small transparent padding, genuine transparent alpha outside the scroll. At the left and right ends, a thick vertical wooden roller in dark wood #4A3425 with rounded end caps in shrine red #B5332B lacquer that project a little above and below the paper. Beside each roller, a narrow vertical band of indigo #2D3A5E silk mounting edged with a thin brass #B08A48 line. Between them, a wide sheet of warm rice paper #F1E8D4: perfectly blank, one uniform flat colour, evenly lit, its top and bottom edges slightly rough and deckled, with a thin ink #1D1B21 outline. The paper's middle is plain so live text can be written on it by the game. Avoid: photorealism, PBR detail, metallic sheen, painterly brush noise, film grain, text, lettering, numbers, kanji, seals, stamps, writing or drawings on the paper, watermark, logo, signature, cords or tassels, glowing particles, cast shadows, drop shadow, background scenery, any resemblance to an existing franchise, artist or character.

## Acceptance

- No glyphs or marks on the paper. Paper alpha is 255 across the centre; a centre eaten by background removal is repaired by script (`tools/ui/repair_slip.py` method) and the raw take is kept.
- The rollers and caps sit entirely inside the left and right nine-slice margins, and the paper centre is uniform.
- At 1180 x 190 in the fight, the kicker and quest read in ink on the paper over the bamboo-grove stage.

## Results

2026-11-15, 3 takes run in parallel (`codex exec -m gpt-6-astra`, refs rice-card and rice-sheet). Codex does not report the credit cost.

| Take | Raw | Verdict |
|---|---|---|
| 1 | `docs/design/screens/astra/dojo-trial-scroll-take1.png` (2172 x 724) | **Accepted.** Blank paper, alpha 253-255 across the centre, round red caps. Trimmed and saved at 1200 x 391 as `game/assets/generated/ui/dojo-trial-scroll.png`; paper spans x 108-1091, y 39-352; nine-slice 112/60/112/60. |
| 2 | `.../dojo-trial-scroll-take2.png` | Rejected: background removal ate the paper's centre (alpha about 3), leaving a dark smear. |
| 3 | `.../dojo-trial-scroll-take3.png` | Usable alternative (clean alpha, flatter caps); not used. |

Built: `TrialScrollBanner` draws the plate at half scale (1180 x 190 under the HUD); the opening card draws it at 1:1 across `TwistScroll.PlateRect`. Captures `docs/screenshots/current/trial_open.png`, `trial_attempt.png`.
