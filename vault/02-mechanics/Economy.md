---
title: Economy
status: draft
source: GDD §5.2, §6.2, §6.3, §6.4
updated: 2026-10-10
---

# Economy

## Health

| Rule | Value |
|---|---|
| Ryo's health | 1,000, carried across the whole run |
| Binding a yokai | Restores 50 |
| Merchant's free heal | 25% |
| Dojo rest | 30% |
| Dojo training trial | 15%, whether or not the trial is landed |

## Duel costs

Health a duel is expected to cost, by opponent.

| Opponent | Newcomer | Average player |
|---|---|---|
| Apprentice (row 1) | 110 | 80 |
| Yokai (rows 2, 3, 6) | 220 | 160 |
| Elder (rows 5, 8) | 280 | 200 |
| Tanuki (boss) | 350 | 250 |

## Routes to the boss

| Route | Health on arrival |
|---|---|
| Newcomer: heal at row 4, rest at row 7 | about 520 |
| Newcomer: heal at row 4, train at row 7 | about 370 |
| Newcomer: shop at row 4, rest at row 7 | about 270, short of the boss's cost |
| Average player: shop at row 4, train at row 7 | about 490 |

The values are tuned by the harness; the targets are fixed.

## Coins

| Source | Coins |
|---|---|
| Each duel on rows 1–6 | 25 |
| The row-5 elder | 50 |
| The row-8 elder | None: it pays a rare ability instead |

That is about 75 coins at row 4 and 75 more by row 7, so every coin can be spent.

## The merchant

The merchant is at row 4 always and at row 7 sometimes. The player chooses one: a free heal of 25%, or shopping.

| Shop item | Price | Rule |
|---|---|---|
| A common ability from a yokai not bound this run | 50 coins | Appears in grey "borrowed ink" and never carries over |
| Release a binding to free a slot | 40 coins | The merchant won't release Ryo's last special |

The merchant never sells rares or cancel rules.

## The dojo

The dojo can appear at row 7. The player chooses one: rest (heal 30%), or a training trial.

- A trial is a hand-authored, Kihon-completable sequence for one of Ryo's specials.
- It heals 15% either way and levels the special if landed within 3 attempts.

| Sample trial | Sequence |
|---|---|
| Spirit Wave | Hit the dummy at mid range, then anti-air its jump with Rising Talisman |
| Iron-Club Charge | Block the dummy's string, then punish with the charge before it recovers |
| River Grab | Grab the dummy as it lands from a jump |

Balance-gate cut candidate: dojo trials are the second cut at the balance gate.

## The Tanuki forecast

At each rest stop a forecast shows the Tanuki's copied move, its level and its expected cost from the player's own fights.

## Open questions

- Whether practice mode unlocks trials for every special or only the ones the player owns.
