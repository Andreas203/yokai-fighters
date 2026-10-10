# Night backdrop for non-fight screens - spec

Status: PROPOSED, awaiting designer approval. **No Meshy job has been run and no generating tool was called.** Prices from earlier specs (`nano-banana-pro` 9, `nano-banana` 3), not re-verified; balance not checked.

Used behind: Control select, Reward, Modifier target, Story card (and Settings, if it sits on a backdrop). Concepts: `control-select.png`, `reward.png`, `modifier-target.png`, `story-card.png`. Pause and Defeat sit over the fight stage; **dimming is done in engine** (a `ColorRect` of ink #1D1B21 at about 55% alpha), not generated. The Title uses its own plate in `ui-title-masks.md`.

## Job
| # | Asset | Tool | Settings | Credits/take |
|---|---|---|---|---|
| N1 | Night backdrop | `meshy_text_to_image` | `ai_model: nano-banana-pro`, aspect 16:9, highest resolution offered, no reference | 9 |

Cheaper alternative the designer may pick: `nano-banana` (3 credits, lower detail; the screen is mostly covered by a paper sheet, so this may be enough). Say which at approval.

Prompt: style block from `assets/specs/style.md`, then:

> Wide night scene of a bamboo grove, matte flat cel layers. Tall pine #34483B bamboo stalks and dark bamboo-leaf silhouettes frame the far left and far right edges. Upper centre: a large pale full moon in rice paper #FBF6EC with a few flat indigo cloud bands. Upper right on a rocky rise: a vermilion #B5332B torii gate and one stone lantern with a warm persimmon #D8632C glow. Deep indigo #2D3A5E misty hills and layered pine forest behind, a small waterfall and an arched wooden bridge in the far middle, two or three distant glowing paper lanterns. A few drifting red maple leaves. Lower edge: dark mossy stone slabs and ink-black ferns. The whole centre band is calm, low detail and mid-dark so a paper sheet can sit on it. No characters, no animals, no masks, no text, no signs.

Avoid: the shared negative plus "readable text, real kanji or letters, characters, people, fox, Tanuki, gloss, photographic rendering, neon, bright centre".

## Layout constraints (for the prompt check)
- Moon and torii stay clear of the centre-left, where the paper sheet's heading sits; the concepts put the moon at x about 62%, torii at x about 85%.
- Left and right 15% are heavy dark bamboo, so the sheet and the mask corner ornament read against them.
- Ink and indigo dominate; no colour brighter than the moon and the lantern glow.

## Rig plan
Not applicable. Static 2D. Engine may add a slow leaf-drift or lantern flicker driven by the tick count (deterministic); not part of this job.

## Acceptance criteria
1. Look (V1): flat cel layers, even ink-brush edges, palette within tolerance (indigo, pine, shrine red, persimmon, paper), no painterly noise, no film grain, no photographic depth-of-field.
2. Composition: calm mid-dark centre; moon, torii, lantern present; no text or lettering anywhere (including signs and torii plaques); no figures.
3. Consistency: same night mood, moon and torii as the fight stage (`bamboo-grove-dusk.md`) and the concept set; reads as one world with the in-fight stage when the Pause/Defeat dimming is applied.
4. Size: delivered at least 1920x1080 (upscale if the tool returns less; note it), crisp at 1080p, no banding in the sky.
5. Licence: UNKNOWN (plan terms not verifiable from the MCP). Designer to confirm before shipping.

## Fallback after 3 failed takes
Reuse the `bamboo-grove-dusk` stage render with a flat indigo `ColorRect` dim, or layer a plain indigo gradient with the K1 sheet and the M1 mask only (no scenery).

## Credit estimate
First pass 9 (or 3 on `nano-banana`). Worst case at 3 takes: 27 (or 9).

## Takes
None. Nothing run, 0 credits spent.

| Job | Task id | Credits (est/actual) |
|---|---|---|
| N1 | not run | 9/0 |

## Acceptance
Pending (no output yet).

## Designer questions
1. `nano-banana-pro` (9) or `nano-banana` (3)?
2. One backdrop for all non-fight screens, or a second variant later for variety? (Not proposed.)
