# Astra brief: map v2 plates (YOK-238)

**Status: approved 2026-11-09.** The designer approved an Astra plate pass for the map rebuild ("Astra pass, ~8 takes"), a **wooden dojo gate** for the dojo node and the **tea house with its screens shut** for the Tanuki node. The concept is [map.png](map.png) (take 3 of [the map v2 brief](astra-brief-map-v2.md)). These plates are the cut-out art the game draws; seals, name tags, routes, mist and the scroll stay drawn in code or come from existing plates.

## Plates

Six takes. Sheets are cut into single sprites by script (`tools/ui/import_map_plates.py`): transparent background, items not touching, each cut to its alpha bounds and saved into `game/assets/generated/ui/map/`. Raw takes stay in `docs/design/screens/astra/`.

| ID | Take | Cut into | References attached |
|---|---|---|---|
| MP1 | Mask sheet: Apprentice, Oni, Kitsune, Kappa | `mask-apprentice.png`, `mask-oni.png`, `mask-kitsune.png`, `mask-kappa.png` | title.png, map.png |
| MP2 | Elder mask sheet: Elder Oni, Nine-Tails, Elder Kappa (MP1's masks plus rope and streamers) | `mask-elder-oni.png`, `mask-nine-tails.png`, `mask-elder-kappa.png` | MP1 take, map.png |
| MP3 | Merchant stand | `node-merchant.png` | map.png, title.png |
| MP4 | Dojo gate | `node-dojo.png` | map.png, title.png |
| MP5 | Tea house, screens shut | `node-tanuki.png` | map.png, title.png |
| MP6 | Landmark sheet: pines, footbridge, torii, rocks | `landmark-*.png` | map.png |

Already in the project, not regenerated: the scroll rollers (`title-reason-scroll.png`, three-sliced), the twist seals (`TwistSeal`, code), the walked-route brush (`MapRoutes`, code).

## Rules every plate keeps

- **Tanuki never drawn**: no tanuki, raccoon dog, animal, statue, silhouette or figure anywhere, the tea house included (no tanuki statue at its door, no shadow behind its screens).
- **Kitsune** only as her white-and-red fox mask.
- No lettering, no real kanji, no seals (the game draws the twist seals).
- Same art hand as the title screen masks.

## Shared style block

```text
Stylised toon-shaded game art matching the attached title screen's masks: heavy tapered black ink outlines, flat cel colour in 2-3 tone steps, black dry-brush hatching for shadows, muted ukiyo-e palette, matte surfaces, soft even light, no cast shadows, no gloss, no photographic or painterly texture. Use case: game UI sprite on a cream rice-paper map. Fully transparent background (real alpha): no backdrop, no ground, no ground shadow, no frame, no border, no paper. No text, no lettering, no real kanji, no seals, no watermark, no logo, no signature. Palette: indigo #2D3A5E, ink #1D1B21, persimmon #D8632C, rice paper #F1E8D4, pine #34483B, fur white #FBF6EC, foxfire #F2A23A, shrine red #B5332B, brass #B08A48, wood dark #4A3425, wood mid #7A5535. Never draw a tanuki or raccoon dog, as an animal, statue, figure, silhouette or mask.
```

## Prompts (appended to the style block)

### MP1 — masks

```text
One landscape 3:2 sprite sheet: four masks in one row, evenly spaced, all the same height, front view, each fully separate with wide transparent gaps, nothing touching or overlapping. Left to right: (1) the Apprentice's plain practice mask: unpainted pale wood oval, two simple eye holes, a few light ink brow strokes, a thin cord; calm and plain. (2) the red horned Oni mask exactly as on the title screen: red face, cream horns, gold eyes, tusks, black hair at the sides, small red tassels. (3) the white fox mask exactly as on the title screen: white with red markings, pointed ears with red insides, black nose, red cord and tassels. (4) the green beaked Kappa mask exactly as on the title screen: teal-green face, yellow beak, leaf-like green hair, the round water dish with a brass rim on top, rope cord and teal tassels.
```

### MP2 — elder masks

```text
One landscape 3:2 sprite sheet: three masks in one row, evenly spaced, same height, front view, fully separate with wide transparent gaps. They are exactly the attached Oni, fox and Kappa masks (same design, colours and proportions), each with only this added: a thick twisted straw shimenawa rope draped across the top of the mask (in front of the Oni's horns, between the fox's ears, around the base of the Kappa's dish) and white zigzag shide paper streamers hanging from the rope at both sides of the face. Left to right: Elder Oni, Nine-Tails (fox), Elder Kappa. No seals, no extra trim, nothing else changed.
```

### MP3 — merchant stand

```text
One square sprite: a small wooden market stand seen from the front, slightly from above: plank counter on two posts, a little shingled roof, a short indigo noren cloth hanging under the roof edge, a round paper lantern hanging from one roof corner, a few cloth-wrapped goods and a small box on the counter. Matches the merchant stand in the attached map concept. No person, no animal, no lettering on the noren or lantern.
```

### MP4 — dojo gate

```text
One square sprite: a small wooden dojo gateway seen from the front: two square wooden posts, a heavy crossbeam with a short tiled roof on top, a blank wooden plaque hanging from the beam on two cords (plain wood, no lettering), a low wooden step at the foot. Same wood and drawing as the merchant stand in the attached map concept. No person, no animal, no lettering.
```

### MP5 — Tanuki's tea house

```text
One square sprite: a small rural tea house seen from the front, slightly from above: thatched roof, dark wood frame, a row of white paper shoji screens all slid shut, a faint warm glow behind the paper, two flat stepping stones at the door, a single small red paper lantern hanging unlit by the eaves. Quiet and closed. Nothing behind or on the screens: no shadow, no silhouette, no figure. No statue, no animal, no tanuki or raccoon dog anywhere, no lettering.
```

### MP6 — landmarks

```text
One landscape 3:2 sprite sheet of small map landmarks, each fully separate with wide transparent gaps, nothing touching: two pine clusters (one wide, one tall) in pine green with ink outlines; one small arched wooden footbridge in persimmon-red lacquer, side view; one small red torii gate; one cluster of three grey rocks. Simple and low-detail, drawn to sit quietly on paper behind the masks.
```

## What the plates must not change

- No new nodes, characters or creatures. The masks are the title screen's designs.
- The elder masks differ from the plain masks only by the rope and the streamers.

## Run record (2026-11-09)

Six takes, one each, `gpt-6-astra` via `codex exec` (MP1, MP3–MP6 in parallel, then MP2 with MP1's take attached). Every take has real alpha with no halo on rice paper. Cut by `tools/ui/import_map_plates.py`: masks 332 px tall, buildings 372 px, landmarks at half the sheet's scale, 6 px transparent margin.

| ID | Raw take | Verdict | Notes |
|---|---|---|---|
| MP1 | [map-masks.png](astra/map-masks.png) | Accept | Four masks match the title screen's designs; the eye holes are transparent. |
| MP2 | [map-elder-masks.png](astra/map-elder-masks.png) | Accept | The same masks plus rope and streamers only. The Nine-Tails' eye holes are grey rather than transparent. |
| MP3 | [map-merchant-stand.png](astra/map-merchant-stand.png) | Accept | Shingled roof, indigo noren, lantern, wrapped goods; no lettering. |
| MP4 | [map-dojo-gate.png](astra/map-dojo-gate.png) | Accept | Indigo tiled roof, blank hanging plaque. |
| MP5 | [map-tea-house.png](astra/map-tea-house.png) | Accept | Screens shut, warm glow, nothing behind the paper, no statue or animal. |
| MP6 | [map-landmarks.png](astra/map-landmarks.png) | Accept | Two pine clusters, footbridge, torii, rocks. |
