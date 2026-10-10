---
title: Art-and-Audio
status: draft
source: GDD §2.1, §3.1, §3.7, §5.1; AM1; assets/specs/style.md
updated: 2026-10-10
---

# Art and audio

## Look

Toon-shaded 3D with ink outlines and a muted ukiyo-e palette of persimmon, indigo, rice paper and pine (V1). UI elements are paper talismans, brush strokes and red seals. Visual assets are generated through the Meshy MCP and unified by one toon shader and outline pass; sound and music are sourced from bought packs (AM1).

| Colour | Hex | Used on |
|---|---|---|
| Indigo | `#2D3A5E` | Ryo's coat, night sky |
| Ink | `#1D1B21` | Outlines, Ryo's energy |
| Persimmon | `#D8632C` | Ryo's lining, dusk glow |
| Rice paper | `#F1E8D4` | Talismans, UI paper |
| Pine | `#34483B` | Bamboo, stage mid-tones |
| Foxfire | `#F2A23A` | Kitsune accents, fire |
| Shrine red | `#B5332B` | Seals, trim |

Reference images: `assets/specs/style.md`, `docs/design/characters/`.

## Camera and 2.5D

Fighters and stages are 3D models, but every rule happens on a flat 2D plane. A perspective camera with a narrow field of view, about 25°, keeps spacing easy to read. The camera stays steady except for a slow push-in on the Tanuki's reveal and on the round-ending blow (V7).

## Stages

| Stage | Yokai | Image |
|---|---|---|
| Bamboo grove at dusk | Kitsune | Tall bamboo, low orange light |
| Snowbound mountain shrine gate | Oni | A torii in snow |
| Riverbank of floating lanterns | Kappa | Lanterns drifting on dark water |
| The Tanuki's tea house | Tanuki | Paper screens that flicker between seasons |

Reference image: `game/assets/generated/stage/bamboo-grove/backdrop.png`.

## Music

Shamisen, shakuhachi and taiko, layered so intensity rises row by row. The Tanuki's theme borrows the merchant's cheerful melody and slowly detunes it (V9).

## Game feel

| Lever | Rule |
|---|---|
| Hitstop | Light 6 frames, medium 9, heavy 12; counterhits add 4. Both fighters freeze. |
| Screen shake | Heavy hits and EX specials only; 2–4 pixels for 6 frames. Never on light hits. |
| Round-ending blow | 30 frames at half speed, then the binding: an ink stroke seals the yokai into a paper talisman. |
| Hit sparks | Coloured by source: foxfire orange, oni red, kappa teal, Ryo's own ink black. |
| Sound | Every hit layers an impact thud with a short yokai sting, timed to the impact frame. |

## The build shows on screen

Moves change colour and effects as they level, so the player's build is visible in the fight (V8). Each special has a visual preset per level and one for its evolution. Evolutions and modifiers are data-only: no new animation.

## Elder twists on screen

Each elder's twist appears as an icon on its map node, a one-line intro card and a short callout the first time a player meets it. The fight HUD keeps a one-line reminder, for example "Elder twist: specials used twice are reflected".

## The copy target on screen

The talisman of the special the Tanuki will copy glows in the HUD and on the map all run.

## Open questions

- If the balance gate reaches its third cut, level presets become colour-only.
