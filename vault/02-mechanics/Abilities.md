---
title: Abilities
status: draft
source: GDD §3.5, §4.1–§4.5, §10.2
updated: 2026-10-10
---

# Abilities

## Slots, levels and offers

- Ryo has four special slots; two start filled with his starters, Spirit Wave and Rising Talisman (A1).
- One modifier per special (A2). Up to two cancel rules (X2).
- Levels belong to the move. Anything newly drafted starts at Lv 1; drafting a special Ryo already owns levels it up; at Lv 3 it evolves (A3).
- Lv 2 gives a special one tuning step chosen per move, such as 4 fewer recovery frames (A4).
- A replaced or sold move loses its levels (A6).
- After each duel the player is offered three cards from the yokai just beaten. One is always an upgrade to a move Ryo owns, starters included (A7).
- A yokai only offers abilities sourced from itself: a Kappa never offers a projectile (A8).
- Rare means the cancel rules and each yokai's second special, offered only by elders. Everything else is common (A9).

## What ships

| Item | Count |
|---|---|
| Specials | 8 (2 starters + 2 per yokai) |
| Evolutions | 8 (one per special, data-only with a visual preset, never locked) |
| Modifiers | 14 (data-only) |
| Cancel rules | 3 (from elders only) |
| Abilities in total | 33 |
| Start locked | 12: 3 specials, 8 modifiers, 1 cancel rule |

## Specials

A ◆ marks an ability that starts locked.

| Special | Source | Startup / active / recovery | Damage | Notes | Lv 3 evolution |
|---|---|---|---|---|---|
| Spirit Wave | Ryo starter | 13 / — / 30 | 60 | Projectile | Great Wave: absorbs one projectile |
| Rising Talisman | Ryo starter | 5 / 8 / 28 | 90 | Anti-air; invulnerable to air attacks on frames 1–5 | Heaven Seal: launches for a follow-up |
| Foxfire | Kitsune, common | 15 / 4 / 30 | 70 | Slow projectile | Piercing Foxfire: passes through projectiles |
| Fox Mirage ◆ | Kitsune, rare | 18 / — / 10 | 0 | Teleport behind the opponent | Mirage Feint: leaves a decoy that absorbs one hit |
| Iron-Club Charge | Oni, common | 16 / 6 / 24 | 110 | Forward charge with 1-hit armour | Crushing Charge: wall bounce |
| Oni Quake ◆ | Oni, rare | 22 / 5 / 26 | 90 | Ground pound; hits low | Aftershock: a second quake 30 frames later |
| River Grab | Kappa, common | 6 / 2 / 30 | 130 | Command grab (generic throw) | Whirlpool Grab: +50% range |
| Shell Spin ◆ | Kappa, rare | 10 / 18 / 20 | 80 | Spins through projectiles | Torrent Spin: travels forward, multi-hit |

## Kitsune modifiers

A modifier attaches to one special and changes how it plays without new animation (A10). The Kitsune offers these five.

| Modifier | Effect on the special it is attached to | Locked |
|---|---|---|
| Will-o'-wisp | Projectiles travel 30% faster | no |
| Fox's Patience | Builds 25% more meter on block | no |
| Illusion Cloak | The first use each round can't be counterhit | ◆ |
| Trickster's Angle | Projectiles can be angled upward by holding up | ◆ |
| Ninefold Spark | The EX version costs half a bar | ◆ |

## Oni modifiers

A modifier attaches to one special and changes how it plays without new animation (A10). The Oni offers these five.

| Modifier | Effect on the special it is attached to | Locked |
|---|---|---|
| Oni's Hide | Armour on frames 1–5 | no |
| Iron Will | Can't be interrupted by projectiles | no |
| Rage Ember | +15% damage while Ryo is below 30% health | ◆ |
| Club Momentum | Pushes the opponent 50% further toward the corner | ◆ |
| Brute Recovery | 4 fewer recovery frames on block | ◆ |

## Kappa modifiers

A modifier attaches to one special and changes how it plays without new animation (A10). The Kappa offers these four.

| Modifier | Effect on the special it is attached to | Locked |
|---|---|---|
| Slippery Skin | Throw-invulnerable during startup | no |
| River Pull | Pulls the opponent one step closer on hit | no |
| Cucumber Gift | Heals 2% of max health on hit | ◆ |
| Sumo Stance | Hold the button to charge for +20% damage | ◆ |

## Cancel rules

Ryo starts every run with no cancels. Cancel rules are rare and offered only by elders (X1). Each move can be cancelled into once per combo, so stacked rules never loop (X3).

| Cancel rule | Offered by | Path | Example | Locked |
|---|---|---|---|---|
| Fox Step | Nine-Tailed Kitsune | Specials cancel into a dash | Foxfire → dash → light punch as the flame connects | no |
| Oni Chain | Elder Oni | Heavy normals cancel into specials | Heavy punch → Iron-Club Charge, a 190-damage punish | no |
| Undertow | Elder Kappa | Successful throws cancel into a special | River Grab → Shell Spin while the opponent is still rising | ◆ |

## The reward screen

Three cards slide up after each win. Each card shows the yokai's crest, the ability's name, its source and rarity, one plain-language line, and a tag: NEW, UPGRADE or MODIFIER. Plain language comes first; frame data sits behind a toggle (A12).

| Card in the GDD mockup | Line under the name | Plain text | Tag |
|---|---|---|---|
| Foxfire | Kitsune · common | "Launch a slow fox flame." | NEW · Lv 1 |
| Rising Talisman | Upgrade · starter | "Lv 1 → Lv 2: recovers 4 frames faster." | UPGRADE |
| Oni's Hide | Oni · modifier | "Your uppercut can't be interrupted early on." | MODIFIER |

## Balance-gate cuts

If the balance gate cuts modifiers from 14 to 8, the six unlocked ones stay (Will-o'-wisp, Fox's Patience, Oni's Hide, Iron Will, Slippery Skin, River Pull) and the locked ones drop to 2.

## Open questions

- The exact Lv 2 tuning step for each special other than Spirit Wave (recovers 4 frames faster) and Rising Talisman (recovers 6 frames faster).
- Which two locked modifiers survive a balance-gate cut.
