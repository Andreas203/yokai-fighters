---
title: Yokai
status: draft
source: GDD §1.3, §5.1, §5.2
updated: 2026-10-10
---

# Yokai

## How yokai fight

In-game yokai are a deterministic C# state machine reading behaviour profiles (Y1). Each yokai has two temperaments, so repeat fights play differently, and exactly one learnable habit that an attentive player can punish (Y2). Yokai telegraph their attacks and react only to what is on screen, never to button presses. Harder yokai simply react faster (P6).

## Kitsune

The Kitsune is the zoner. It offers Foxfire, Fox Mirage and the Kitsune modifiers.

| | |
|---|---|
| Aggressive temperament | Throws foxfire at mid range while advancing |
| Patient temperament | Holds full screen and anti-airs jumps |
| Habit to punish | Often jumps after a knockdown |
| Elder | Nine-Tailed Kitsune |
| Elder twist | Reflects any special used twice in a row |
| Habit the twist breaks | Repeating one special, so the player learns to mix specials |
| Elder offers | The cancel rule Fox Step |

## Oni

The Oni is the rushdown yokai. Its relentless pressure teaches the player to block. It offers Iron-Club Charge, Oni Quake and the Oni modifiers.

| | |
|---|---|
| Aggressive temperament | Dashes in constantly |
| Patient temperament | Walks in and punishes whiffs with its charge |
| Habit to punish | Dashes in after blocking a projectile |
| Elder | Elder Oni |
| Elder twist | Armour on every special |
| Habit the twist breaks | Mashing jabs to interrupt it stops working |
| Elder offers | The cancel rule Oni Chain |

## Kappa

The Kappa is the grappler. It offers River Grab, Shell Spin and the Kappa modifiers.

| | |
|---|---|
| Aggressive temperament | Walks straight into throw range |
| Patient temperament | Blocks, then throws after two blocked hits |
| Habit to punish | Throws after blocking two hits in a row |
| Elder | Elder Kappa |
| Elder twist | Its throws can't be broken |
| Habit the twist breaks | Relying on the throw break, so the player has to stay out of throw range |
| Elder offers | The cancel rule Undertow |

## Elders

Rows 5 and 8 always hold elders. Each elder adds one rule twist that breaks a common habit, so a strong build never wins on autopilot (P5). The twist appears as an icon on the map node, a one-line intro card and a short callout the first time the player meets it (Y3). The row-5 elder pays 50 coins; the row-8 elder pays a rare ability instead.

## Difficulty curve

| Opponent | Reaction time | Damage dealt per duel (newcomer / average) |
|---|---|---|
| Apprentice (row 1) | 30 frames | 110 / 80 (half damage) |
| Yokai (rows 2, 3, 6) | 22 frames | 220 / 160 |
| Elder (rows 5, 8) | 18 frames | 280 / 200 |
| Tanuki (boss) | 15 frames | 350 / 250 |

## The illusion grows stronger

Across runs, each Tanuki win, up to three, makes yokai react 1 frame faster and pushes them toward their aggressive temperament (Y5).

## Open questions

- "Habit the twist breaks" is stated in the GDD for the Elder Oni and the Nine-Tailed Kitsune. The Elder Kappa's row is inferred from its twist and needs the designer's wording.
