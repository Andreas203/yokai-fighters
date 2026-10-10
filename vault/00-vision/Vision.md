---
title: Vision
status: draft
source: GDD §1.1, §1.3, §1.4, §10.2, §10.4
updated: 2026-10-10
---

# Vision

## Pitch

Yokai Fighters is a single-player 2.5D roguelite fighting game for PC. Ryo, an apprentice exorcist, binds yokai instead of destroying them, and each binding lends him one of three abilities drawn from that yokai's power. The yokai you choose to fight decide the fighter you become. At the end of every run the Tanuki, a shapeshifter, copies the move you have invested in most. A first-time player finishes a run in 25–40 minutes.

## Design pillars

Each pillar has a test a ticket or a piece of content can be checked against.

| # | Pillar | GDD example | Test |
|---|---|---|---|
| 1 | Every upgrade changes how you fight | Piercing Foxfire cuts through the Kitsune's own projectiles, which changes how you win that matchup | Does it change frame data, a property or a combo route, and not damage alone? |
| 2 | Your fighter is yours | Chase Kappa and you finish a grappler; chase Kitsune and you finish a zoner | Can you tell which yokai the player hunted by watching one fight? |
| 3 | Every choice costs something | A replaced move loses its levels; dojos make you choose between healing and training | What does the player give up by taking this? |
| 4 | Easy to start, deep to master | Kihon and a guided apprentice fight let newcomers win in their first run | Can a Kihon newcomer use it, and does the one-button version never beat the manual one? |
| 5 | The game answers your habits | The Nine-Tailed Kitsune reflects any special used twice in a row | Does it stop one safe pattern from winning on autopilot? |
| 6 | Readable, fair fights | Yokai react only to what is on screen and harder yokai simply react faster | Is it telegraphed, and does a loss trace back to a decision the player made? |

## Audience and inspirations

| Audience | Who | What exists for them |
|---|---|---|
| Primary | Roguelite players curious about fighting games | Kihon controls, the guided first fight, readable yokai habits, the rest-stop forecast |
| Secondary | Fighting-game fans wanting deep solo content | Kata precision bonus, cancel-rule depth, the frame-data toggle, practice mode |

Inspirations: Hades (run structure and a story told across runs), Slay the Spire (drafting and branching maps), Street Fighter 6 (2.5D presentation and two control schemes).

## Success

The 5-week build is the capstone's assessed deliverable: a complete, polished one-act game, not a retail release.

| Measure | Target |
|---|---|
| A first-time player finishes a full run | 25–40 minutes |
| The copy rule | Works in every build |
| 200 simulated first runs by a newcomer-profile Kihon bot reach the Tanuki | 50–70% |
| Of the runs that reach the Tanuki, the share that beat it | 40–60% |

## Scope guardrails

| Gate | When | Cut order |
|---|---|---|
| Scope gate | End of week 1, on actual velocity | Kata, then the meta-unlock layer (carry-over and story stay) |
| Balance gate | Mid week 3, on harness and playtest data | Modifiers 14 to 8, then dojo trials, then colour-only presets |

Never cut: the copy rule, the merchant, Kihon, the harness and the story cards. The designer makes every cut call.

Details live in [[Run-Loop]], [[Abilities]], [[Tanuki]] and [[Meta-Progression]].
