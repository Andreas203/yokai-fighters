# Ability icons, batch 1: five specials (YOK-80)

Status: SPEC ONLY. No job run, no credits spent. Waits on designer approval of the job list and cost.

Extends the four existing diamond icons in `assets/specs/ui-screen-plates.md` (spirit-wave, rising-talisman, foxfire, fallback). Prompts use the same template, word for word, with only the last sentence (diamond colour and motif) changing. Source rules: A8 (motif comes only from the owning yokai), V1 (toon look), V5 (source colours).

## Tool and cost

- Tool: Meshy MCP `meshy_text_to_image` (AM1). Not the built-in imagegen the first four used, so the look must be checked against them (see Acceptance).
- Model: `nano-banana-2` (designer to confirm; alternatives below).
- Settings: `aspect_ratio: 1:1`, `remove_background: true` (needed for real alpha), `generate_multi_view: false`, `response_format: json`.
- Post step (local, free): resize the PNG to 256x256 with Lanczos, preserving alpha, as for the first four. Save to `game/assets/generated/ui/ability-icon-<id>.png`.

| Model | Credits per job | 5 icons, 1 take each | Worst case, 3 takes each |
|---|---|---|---|
| nano-banana-2 (proposed) | 6 | 30 | 90 |
| nano-banana-2-lite | 3 | 15 | 45 |
| nano-banana / nano-banana-2-1 | 3 / 4 | 15 / 20 | 45 / 60 |
| nano-banana-pro | 9 | 45 | 135 |
| gpt-image-2 | 9 | 45 | 135 |

Proposed approval: 5 jobs at 6 credits = **30 credits**. Retakes (up to 2 more per icon, 60 credits, 90 total ceiling) each need their own approval.

## Diamond colours (five distinct at 90 px, and from the four existing)

Existing diamonds: indigo (spirit-wave), persimmon (rising-talisman), shrine red (foxfire), pine (fallback).

| Source | Diamond | Motif colour | Why |
|---|---|---|---|
| Kitsune | shrine red #B5332B (designer's choice) | rice #F1E8D4 | Shares red with foxfire. Told apart by motif (three fading copies, not one flame). Flag if it reads as a duplicate at 90 px. |
| Oni | ink #1D1B21, ink border still drawn | rice with persimmon accents | Only dark diamond. Oni red (V5) would collide with Kitsune red, so ink is proposed. Designer may prefer another. |
| Kappa | rice #F1E8D4 with ink border | indigo #2D3A5E with pine accents | Only light diamond. Teal is outside the allowed palette and pine is taken by the fallback. |

Within a source the two icons share the diamond colour and differ by motif and silhouette (below). If that is not distinct enough at 90 px, the fix is a motif change, not a new colour.

## Shared prompt template

Same as the four existing icons, with "characters, animals, faces" added to the negatives per the ticket:

> Create one standalone square game UI diamond move icon, tight framing with small transparent padding, genuine transparent alpha outside diamond. Thin rough ink #1D1B21 diamond border, flat matte ukiyo-e cel ink-brush illustration, [MOTIF COLOUR] motif. Bold readable silhouette at 90px. No lettering, numbers, real kanji, watermark, people, characters, animals, faces, weapons, masks, Tanuki or scenery. Palette only indigo #2D3A5E, ink #1D1B21, rice paper #F1E8D4, pine #34483B, shrine red #B5332B, persimmon #D8632C. [DIAMOND + MOTIF SENTENCE]

## Jobs

### 1. fox-mirage (Kitsune, rare, teleport behind)

Output: `game/assets/generated/ui/ability-icon-fox-mirage.png`

Motif: the Kitsune dissolves and reappears behind the opponent, leaving fading copies (Kitsune sheet Fox Mirage study). Three identical rice-white teardrop flames in a row, each fainter and more broken into dry-brush flecks. No fox, no mask.

Prompt: `Create one standalone square game UI diamond move icon, tight framing with small transparent padding, genuine transparent alpha outside diamond. Thin rough ink #1D1B21 diamond border, flat matte ukiyo-e cel ink-brush illustration, rice paper #F1E8D4 motif. Bold readable silhouette at 90px. No lettering, numbers, real kanji, watermark, people, characters, animals, faces, weapons, masks, Tanuki or scenery. Palette only indigo #2D3A5E, ink #1D1B21, rice paper #F1E8D4, pine #34483B, shrine red #B5332B, persimmon #D8632C. Shrine-red diamond containing a row of three identical rice-white teardrop flames, left to right, the left one solid and each next one fainter and breaking into dry-brush flecks, as if dissolving and reappearing.`

### 2. iron-club-charge (Oni, common, forward charge, 1-hit armour)

Output: `game/assets/generated/ui/ability-icon-iron-club-charge.png`

Motif: the Oni's forward rush with armour. No club or weapon may be drawn (ticket rule), so the club is told through round iron studs on a bold rice-white forward chevron, three speed streaks behind, one persimmon impact ring at the tip.

Prompt: `Create one standalone square game UI diamond move icon, tight framing with small transparent padding, genuine transparent alpha outside diamond. Thin rough ink #1D1B21 diamond border, flat matte ukiyo-e cel ink-brush illustration, rice paper #F1E8D4 and persimmon motif. Bold readable silhouette at 90px. No lettering, numbers, real kanji, watermark, people, characters, animals, faces, weapons, masks, Tanuki or scenery. Palette only indigo #2D3A5E, ink #1D1B21, rice paper #F1E8D4, pine #34483B, shrine red #B5332B, persimmon #D8632C. Near-black ink diamond containing a bold rice-white arrowhead chevron pointing right, a row of small round studs along its edge, three rice-white speed streaks trailing behind it and a persimmon impact ring at the tip.`

### 3. oni-quake (Oni, rare, ground pound, hits low)

Output: `game/assets/generated/ui/ability-icon-oni-quake.png`

Motif: a ground pound that hits low. A ground line across the lower third, a jagged rice-white crack splitting upward, a wide persimmon shockwave arc, four rubble chips. Low and wide, versus the Charge's pointed right-facing silhouette.

Prompt: `Create one standalone square game UI diamond move icon, tight framing with small transparent padding, genuine transparent alpha outside diamond. Thin rough ink #1D1B21 diamond border, flat matte ukiyo-e cel ink-brush illustration, rice paper #F1E8D4 and persimmon motif. Bold readable silhouette at 90px. No lettering, numbers, real kanji, watermark, people, characters, animals, faces, weapons, masks, Tanuki or scenery. Palette only indigo #2D3A5E, ink #1D1B21, rice paper #F1E8D4, pine #34483B, shrine red #B5332B, persimmon #D8632C. Near-black ink diamond containing a horizontal rice-white ground line across the lower third with a jagged rice-white crack splitting upward from its centre, a wide persimmon shockwave arc above it and four small rice-white rubble chips.`

### 4. river-grab (Kappa, common, command grab)

Output: `game/assets/generated/ui/ability-icon-river-grab.png`

Motif: a river current that seizes. Two thick indigo curling currents from left and right closing around a small pine pebble, a few foam flecks. No hand, claw or creature.

Prompt: `Create one standalone square game UI diamond move icon, tight framing with small transparent padding, genuine transparent alpha outside diamond. Thin rough ink #1D1B21 diamond border, flat matte ukiyo-e cel ink-brush illustration, indigo #2D3A5E motif with pine accent. Bold readable silhouette at 90px. No lettering, numbers, real kanji, watermark, people, characters, animals, faces, weapons, masks, Tanuki or scenery. Palette only indigo #2D3A5E, ink #1D1B21, rice paper #F1E8D4, pine #34483B, shrine red #B5332B, persimmon #D8632C. Rice-paper diamond containing two thick indigo curling water currents entering from left and right and closing together around one small pine-green round pebble at the centre, with a few ink foam flecks.`

### 5. shell-spin (Kappa, rare, spins through projectiles)

Output: `game/assets/generated/ui/ability-icon-shell-spin.png`

Motif: the Kappa's shell, spinning. A round indigo shell disc from above with a pine hexagon scute pattern, ringed by two broken circular spin arcs. Round and closed, versus River Grab's open currents.

Prompt: `Create one standalone square game UI diamond move icon, tight framing with small transparent padding, genuine transparent alpha outside diamond. Thin rough ink #1D1B21 diamond border, flat matte ukiyo-e cel ink-brush illustration, indigo #2D3A5E motif with pine accent. Bold readable silhouette at 90px. No lettering, numbers, real kanji, watermark, people, characters, animals, faces, weapons, masks, Tanuki or scenery. Palette only indigo #2D3A5E, ink #1D1B21, rice paper #F1E8D4, pine #34483B, shrine red #B5332B, persimmon #D8632C. Rice-paper diamond containing a round indigo turtle-shell disc seen from above with a pine hexagon scute pattern and ink outlines, surrounded by two broken circular ink spin arcs.`

## Job list for approval

| # | Id | Tool call | Model | Credits |
|---|---|---|---|---|
| 1 | fox-mirage | meshy_text_to_image, 1:1, remove_background true | nano-banana-2 | 6 |
| 2 | iron-club-charge | same | nano-banana-2 | 6 |
| 3 | oni-quake | same | nano-banana-2 | 6 |
| 4 | river-grab | same | nano-banana-2 | 6 |
| 5 | shell-spin | same | nano-banana-2 | 6 |
| | Total, one take each | | | 30 |
| | Ceiling with 3 takes each | | | 90 |

Run order after approval: `meshy_check_balance` (stop if under 30), run the five jobs, poll `meshy_get_task_status`, `meshy_download_model` (images save their own files), resize to 256x256, move to the output paths, log under Takes.

## Acceptance checks (per icon, after each take)

1. Text-free, no characters: no letters, numbers, kanji, watermark, people, animals, faces, masks or weapons. Inspect at full size.
2. Real alpha: pixels outside the diamond have alpha 0 (checked by script, not by eye). Small padding, diamond not clipped. A baked checkerboard or white fill is a RETAKE.
3. Readable at 90 px: downscale to 90 px; the motif silhouette and diamond colour still read.
4. Distinct from the other seven (three non-fallback existing icons plus the other four new): view all eight at 90 px. No confusable pair. Known risks: Kitsune red vs foxfire; Kappa's two and Oni's two must differ by silhouette.
5. Style matches the existing icons: thin rough ink border, flat matte cel fill, no gradient, gloss, glow or painterly noise, palette within the six hexes, outline weight like spirit-wave and foxfire.
6. 256x256 PNG at the correct path and id.

Licence: Meshy output terms on the designer's plan are not confirmed; UNKNOWN until the designer confirms commercial use.

Retake rule: 3 takes per icon. Each retake states the exact prompt change and its credit cost (6 at nano-banana-2) and needs designer approval. After 3 failed takes, use `ability-icon-fallback.png` (generic pine energy ring) for that ability through `game/ui/ability_art.tres` and stop.

## Takes

None. Nothing has been run.

## Acceptance

Pending first takes.
