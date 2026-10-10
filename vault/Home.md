---
title: Home
status: draft
source: GDD §1.1
updated: 2026-10-10
---

# Yokai Fighters design vault

Yokai Fighters is a single-player 2.5D roguelite fighting game for PC. You play Ryo, an earnest, stubborn apprentice exorcist who binds yokai rather than destroying them, apologising to each one he seals into a talisman. Every yokai he binds lends him one of three abilities, so the yokai you choose to fight decide the fighter you become. At the end of every run waits the Tanuki, a shapeshifter that copies the move you have invested in most.

This vault is the design written as notes: one topic per note, for people and for the dev agents. It also serves as the retrieval store for the content pipeline in `tools/content_pipeline/`. How notes are written: [_conventions](_conventions.md).

## 00 Vision

| Note | What it answers |
|---|---|
| [Vision](00-vision/Vision.md) | Pitch, six pillars with a test each, audience, success, scope guardrails |

## 01 Vibe

| Note | What it answers |
|---|---|
| [Tone](01-vibe/Tone.md) | The tone line, voice rules per speaker, do and don't examples |
| [Story](01-vibe/Story.md) | The illusion loop, the merchant's disguise, the card schedule, sample cards |
| [Characters](01-vibe/Characters.md) | Ryo, Master Sōen, the merchant, the Tanuki, Kitsune, Oni, Kappa |
| [Art-and-Audio](01-vibe/Art-and-Audio.md) | Look, palette, stages, music, game feel, level presets |

## 02 Mechanics

| Note | What it answers |
|---|---|
| [Combat](02-mechanics/Combat.md) | Fight basics, Ryo's kit, meter, burst, throws, frame data |
| [Controls](02-mechanics/Controls.md) | Kata and Kihon |
| [Abilities](02-mechanics/Abilities.md) | Slots, levels, offers, the 33 abilities, cancel rules, reward cards |
| [Run-Loop](02-mechanics/Run-Loop.md) | The 8-row map, the core loop, win and loss |
| [Yokai](02-mechanics/Yokai.md) | Archetypes, temperaments, habits, elder twists, reaction frames |
| [Tanuki](02-mechanics/Tanuki.md) | The copy rule and how the boss fights |
| [Economy](02-mechanics/Economy.md) | Health, duel costs, routes, coins, merchant, dojo, forecast |
| [Meta-Progression](02-mechanics/Meta-Progression.md) | Unlocks by depth, the Lv 2 carry-over, story save |

## Where the vault sits

The GDD PDF plus `docs/design/gdd-amendments.md` stay the source of truth. `docs/design/rules.md` holds the same rules with citable IDs for the Rules Lawyer. A vault note wins over the GDD only once its status is `locked`.
