# YOK-167 balance report: whole-run harness, batch 3 (2026-10-21)

**Sources.** Five 200-run batches in `harness/results/whole-run/` (main 566ee05, hit log on; index in that folder's README):

| Key | File |
|---|---|
| NC | `newcomer-kihon-bot-n200-s1.json` |
| NC201 | `newcomer-kihon-bot-n200-s201.json` |
| MOD | `newcomer-kihon-bot-modifier-first-n200-s1.json` |
| INT | `intermediate-kihon-bot-n200-s1.json` |
| SK | `skilled-kihon-bot-n200-s1.json` |

**Code and data read.** `ProfileAi.cs`, `BehaviourProfile.cs`, `RunHarness.cs`, `RunSession.cs`, `RewardDraft.cs`, `NewcomerBot.cs`, `data/profiles/*`, `data/moves/foxfire.json`, `data/map/*`, `data/harness/*`.

**Method.** Every number below comes from scripts run on the raw runs. 95% intervals are Wilson intervals.

**Counterfactuals.** These are a Monte Carlo replay of the recorded hit logs:
- Each fight's hits are replayed in tick order, with Ryo's health carried across the map (bind +50, merchant +250, dojo +300).
- Damage is scaled where a scenario says so.
- An undecided fight is extrapolated at its measured damage rates, and it is lost at the 10800-tick cap.
- Fights are drawn from the pool for the same tier and profile, 4000 runs each.
- Check at scale 1.0: the replay gives 0.3% reach for the newcomer (measured 1 in 400) and 3.5% for the skilled bot (measured 5%, CI 2.7–9). The row-by-row funnel matches within about 5 points.

**Calibration.** `production/playtests/` still does not exist, so the bot has never been calibrated (R5).

**Known gaps, treated as missing data.**
- These habits never roll in any batch: apprentice `at_close_range`, Kappa-aggressive `blocked_two_hits_in_row` and Tanuki `opponent_blocking`. F3 explains why: two are a code bug and one is the AI never blocking.
- The Oni and Kappa fight in the Kitsune body, and the Kappa has no strikes.

## 1. Summary

| Target | Target | Measured | Verdict |
|---|---|---|---|
| R5 reach Tanuki | 50–70% | NC 0.5% (1/200, CI 0.1–2.8), NC201 0% (0/200), MOD 0%. INT 1%, SK 5% (10/200) | **FAIL** by two orders of magnitude |
| R5 beat Tanuki, of those who reach | 40–60% | NC 0/1. SK 3/10 (30%, CI 11–60) | Not measurable for the newcomer |
| R4 heal@4 + rest@7, at Tanuki | ≈520 | NC 85 (n=1). INT 248 (n=2). SK mean 397, median 334 (n=10) | FAIL (SK −120 to −190). The other three R4 lines are untested: the bot always heals and rests |
| R4 implied row-4 arrival | ≈600 (1000 −110 −220 −220 +3×50) | NC survivors 389 (n=38, sd 249). NC201 370 (n=30) | FAIL. These are survivors only, so the true figure is lower |
| Y4 newcomer: apprentice | 110 | 326 (median 312, sd 134, n=200) | FAIL ×3.0 |
| Y4 newcomer: yokai | 220 | All fights 487 (n=280). Wins 315 (n=115). NC201: 511 / 318 | FAIL ×2.2 (×1.4 in wins only) |
| Y4 newcomer: elder | 280 | All 597 (n=40). Wins 620 (n=6) | FAIL ×2.1 |
| Y4 newcomer: Tanuki | 350 | n=1 | Not measurable |
| Y4 average (INT used as proxy) | 80 / 160 / 200 / 250 | 314 / 436 (wins 324) / 601 / 248 (n=2) | FAIL ×2–4 |
| R9 run length | 25–40 min | Runs that reach the Tanuki: NC 13 min (n=1), SK 11.4 (n=10, range 10.2–13.1). This includes the provisional 45 s per node | FAIL ×2–3 (projection below) |
| R8 coins | 75 at row 4, +75 by row 7 | 75 / 150 in every surviving run (sd 0) | PASS by construction. Shop spending is untested (the bot heals) |
| P5 copy rule (T2/T3) | correct target | 13/13 locks correct across all batches (checked against the pick order) | PASS (small n) |
| P5 single-move dominance | no autopilot build | Spirit Wave is 32% (NC) to 40% (SK) of Ryo's damage in won fights. One special >50% in 26% (NC) to 40% (SK) of non-apprentice wins | WATCH. The bot picks specials at random, so this measures move efficiency, not player habit |
| P5 elder twists | break their habit | **Not applied in the harness** (F7) | Not measurable |
| P6/Y2 one punishable habit per yokai | every profile | Fires and is punished in 3 of 8 profiles: Kappa-patient 92–97%, Kitsune both 67–92%. Oni both: 0–13 rolls in 43–84 fights. 3 profiles never | FAIL for 5 of 8 profiles (F3) |
| P4, X3, R7 | | No Kata bot, no cancel probe, the bot never takes the trial | Not measurable |

**Since batch 2:**
- Exchange ratio (dealt ÷ taken): 0.81 → 1.88 (NC).
- Apprentice wins: 27% → 100%.
- Damage the newcomer takes from the apprentice: 935 → 326.
- The idle guard and half damage worked. The failure has moved from row 1 to rows 2, 3 and 5.

**Stability.** NC and NC201 share no run:
- Duel win rate: 61.6% vs 60.3%.
- Exchange ratio: 1.88 vs 1.83.
- Damage taken from the apprentice: 326 vs 325.
- Runs lost on row 2: 125 vs 130.

The batches are stable. Differences between profiles of under about 10 points are inside noise.

## 2. Findings, ranked by impact on a first-time player

**F1. The gap is global: no minimal tuning reaches R5 with this bot.**
- Y4's numbers assume a newcomer takes about 220 while dealing 1000, an exchange ratio of about 4.5.
- Measured exchange ratios:
  - Newcomer: 1.88.
  - Intermediate: 2.08.
  - Skilled: 2.44.
- **Even the skilled bot is about half of what Y4 assumes for a newcomer.**
- Per-duel win rates after row 1:
  - Yokai: 41% (NC), 57% (SK).
  - Elder: 15% (NC), 50% (SK).
- R5 needs about 0.9 per duel (0.9^5 ≈ 0.6).
- Counterfactual, with all yokai and elder damage scaled by s:

| Bot | s = 1.0 | 0.8 | 0.6 | 0.5 | 0.4 | 0.3 |
|---|---|---|---|---|---|---|
| Newcomer | 0.3% | 1.1% | 3.4% | 7.0% | 14.6% | 29.6% |
| Skilled | 3.5% | 8.9% | 21.3% | 34.9% | 47.8% | 56.8% |

- All of the per-profile fixes below, applied together, move the newcomer replay from 0.3% to about 2% (skilled 3% to 12%).
- So one of two things is true:
  1. The bot is about 3× weaker than a real newcomer.
  2. The yokai are about 3× stronger than Y4 intends.

  **Only a playtest can tell which** (R5 calibration). This is the main input for G3: balance is not converging through data tuning yet.

**F2. The Kitsune is a wall in both temperaments.**
- The Kitsune ends 40–46% of all runs in every newcomer batch.
- At row 2 (NC and NC201 pooled, arrival health about 740):

| Profile | Win rate (n) | CI |
|---|---|---|
| Kitsune-patient | 3% (2/77) | 0.7–9 |
| Kitsune-aggressive | 11% (8/70) | 6–21 |
| Oni, either temperament | 36–38% | |
| Kappa-aggressive | 51% | |
| Kappa-patient | 100% (52/52) | |

- **Patient: Ryo cannot damage her.**
  - Ryo deals 56 damage per 1000 ticks, against 231 vs the aggressive Kitsune and 425 vs the aggressive Oni.
  - Her `projectile_on_screen → block` (60) blocks 53–60% of Ryo's Spirit Waves, his main damage source.
  - Her walk-back (C3 guard) and her spacing to full screen keep his normals out of range.
  - **Even at zero damage taken, the replay wins only 24% of her fights inside the cap** (NC 19/80, SK 8/33).
  - The failure moves from KO losses (NC: 1 timeout in 28) to timeouts as the bot improves:
    - INT: 13 timeouts in 30 fights.
    - SK: 20 in 33, and the median fight is the full 10800 ticks.
  - Foxfire is 82–88% of the damage she deals: about 9 unblocked flames per newcomer fight, 70 each.
- **Aggressive: the damage comes from Foxfire.** Foxfire is 57% of her damage (mid-range weight 40).
  - Ryo's damage rate against her is normal.
  - She deals 713 per fight.

**F3. Five of eight profiles have no working habit (P6, Y2).**
- **Code bug (gameplay):**
  - `ProfileAi.cs:125` fires the habit only on `rose[h.When]`.
  - `Events()` (`ProfileAi.cs:425–458`) raises edges only for event observations.
  - `at_close_range` and `opponent_blocking` are standing observations (`BehaviourProfile.IsEvent`, `BehaviourProfile.cs:99–100`), so **the apprentice's teaching habit (Y6) and the Tanuki's AM4 throw habit can never fire, in the game as well as the harness.**
  - This is not a harness counting gap.
- **Kappa-aggressive: the AI never takes the habit.**
  - It blocks 0.4–1.0% of Ryo's hits. Its only block is a reactive `opponent_attacking` 30, which comes too late for strikes (batch-2 F1).
  - Its constant walking also resets `_blockedInRow`.
- **Oni (both temperaments): the habit almost never fires.**
  - `blocked_projectile` rolled in 0–2 of 43–84 fights per batch (13 in MOD).
  - The Oni blocks 0–5% of Ryo's hits. Only a `projectile_on_screen` reaction blocks a projectile in time (compare the patient Kitsune at 60%), and no Oni profile has one.
- **Where a habit does fire, it is punished 67–97% of the time.**
  - The 3-hit window counts either side's hits, so this is close to "Ryo landed anything soon after".
  - It is generous, not proof that the counter was learned.

**F4. The Kappa (no strikes, so throws are all he has).**
- **Patient: nearly free.**
  - Wins 92–95% (NC 55/58).
  - Ryo takes 108–124 per fight, about one throw (target 220).
- **Aggressive: throws are 94–97% of his damage.**
  - About 4 throws per fight at 120 each.
  - Ryo broke 3 of 654 Kappa throws across NC and SK. The bot has no throw break (`NewcomerBot.cs:74`), which is a **bot artefact**: Y6 teaches the break.
  - Win rate at 740 health: 57% (NC), 63% (SK).
  - Replay with throw damage ×0.67: 76% / 82%.

**F5. The apprentice: 100% wins (CI 98–100), but Ryo takes 326 against a target of 110 (×3.0).**
- Damage split:
  - Medium punch: 33%.
  - Light punch: 25%.
  - Throw: 20%.
- Its habit never fires (F3). The harness plays the raw profile, not the Y6 guided fight.
- Replay at 110 taken: reach 0.3% → 0.5% (NC), 3.2% → 5.0% (SK).
- So the extra 215 damage matters little next to F1–F2.

**F6. Timeouts grow with skill: NC 1, INT 15, SK 25, all against the patient Kitsune.**
- When the cap hit, Ryo was ahead on health in 14 of SK's 25 timeouts.
- The harness counts a timeout as a loss, but rules.md has **no round timer**.
- Without the cap, the skilled replay reaches 4.7% at s=1.0 and 48% at s=0.5 (35% with the cap).
- This is partly a harness rule the designer has not set, and partly a real P6 problem: playing better makes this fight longer.

**F7. Coverage gaps and bugs (not balance):**
- **The draft offers the stand-in body's cards (A7 bug).** `RunSession.cs:225` passes `node.Fighter` (the Kitsune body) to `RewardDraft.Build` instead of the yokai beaten.
  - In all five batches every offer at an Oni, Kappa or elder node was a Kitsune card.
  - Only 2 of the 13 modifier files (`fox-patience`, `will-o-wisp`) can ever be drafted.
  - The bug is in the game too.
- **Elder twists are off in the harness.** `RunHarness.FightNode` (`RunHarness.cs:154`) never calls `Twists.Apply`; only `FightScene.Flow.cs:175` does.
  - Every elder result is "faster reactions + 100% jump punish", with no twist.
  - Elder Oni's armour would be inert anyway: the Oni has no specials.
- **Draft choice has no measurable effect.** MOD and NC are paired on the same 200 seeds and meet identical opponents (254 later fights, every profile the same):
  - Fights won: 105 vs 106.
  - Runs that got further: 32 vs 34, with 134 equal.
  - Exchange ratio: 1.87 vs 1.88.
  - Modifier-first picks almost no upgrades (41 against 318), so neither the two modifiers nor the Lv2/Lv3 steps move outcomes for this bot.
- No shop runs, no trial runs, no Kata bot.

**F8. Run length (R9).**
- Winning fights take 38 s (apprentice), 43 s (yokai), 58 s (elder) and about 33 s (Tanuki, SK).
- A full run is about 5.3 min of fighting plus 9 × 45 s of menus: **about 12 min**, against 25–40.
- Fights run at about 1000 health in about 40 s.
- Human fights are likely longer than the bot's, and the 45 s per node is provisional (YOK-36). This needs the playtest before any tuning.

## 3. Recommendations, in priority order

Rows 1–3 are bugs or coverage gaps and come before any tuning. They change what the next batch can measure.

| # | Change | Expected effect | Verify in batch 4 | Owner | Rule |
|---|---|---|---|---|---|
| 1 | **Bug:** make the habit fire on a standing trigger. Raise an edge in `ProfileAi.Events` when `at_close_range` or `opponent_blocking` becomes true, or have the schema reject standing triggers (designer picks) | Apprentice and Tanuki habits fire. The apprentice fight may get easier: a slow heavy punch replaces some of her jabs | `habits.apprentice.rolls` > 0 and `tanuki-boss.rolls` > 0 | gameplay-programmer | Y2, AM4, Y6, P6 |
| 2 | **Bug:** `RunSession.cs:225` drafts from the yokai beaten (`plays_as`), not `node.Fighter` | Oni and Kappa nodes offer Oni and Kappa modifiers (`iron-will`, `onis-hide`, `river-pull`, `slippery-skin`) | Offers by node yokai match the yokai | gameplay-programmer | A7, A8 |
| 3 | **Coverage:** `RunHarness.FightNode` applies `Twists.Apply(m, node)` as `FightScene` does, and logs the twist's triggers | Elders get harder (Nine-Tails reflects the bot's repeated specials) | Elder win rate and a twist-fired count per elder | gameplay-programmer | P5, Y3 |
| 4 | `data/profiles/kitsune-patient.json`, behaviour `projectile_on_screen → block`: weight **60 → 30** | Spirit Waves blocked 55–60% → about 35%. Ryo's damage rate against her +25–40%. Replay proxy: NC 1% → 6%, SK 24% → 33%. Fewer INT/SK timeouts. She still holds full screen (Y2) | Ryo's Spirit Wave blocked % about 35. Ryo damage per 1000 ticks > 75. SK timeouts < 10 of about 33. If SK still wins < 30%, the next single step is `neutral → walk_back` 20 → 0 | habit-writer | Y4, P6 |
| 5 | `data/profiles/kitsune-aggressive.json`, `at_mid_range → special:foxfire`: weight **40 → 20** | Foxfire's share of her mid-range neutral picks 39% → 22%. Foxfire damage about 430 → about 240 per fight. Replay proxy (Foxfire ×0.56): row-2 win at 740 health 11% → 21% (NC), 41% → 65% (SK) | Foxfire < 40% of her damage. NC win rate ≥ 20% (n ≥ 60) | habit-writer | Y4, P6 |
| 6 | `data/profiles/oni-aggressive.json` and `oni-patient.json`: add `{ "when": "projectile_on_screen", "do": "block", "weight": 30 }` | The Oni can block a Spirit Wave, so `blocked_projectile` (his habit) rolls in most fights. Ryo loses about 30% of the Spirit Wave damage he deals the Oni (about −65 per fight) but gains a punishable dash | `habits.oni-*.rolls / fights` > 0.5, with a punish % | habit-writer | Y2, P6 |
| 7 | `data/profiles/kappa-aggressive.json`: add `guard_pct` (absent = 0) **→ 30** (the YOK-165 pre-emptive guard while settled at close range) | He can block two in a row, so his habit can fire. He may get slightly tougher | `habits.kappa-aggressive.rolls` > 0 and his win % within 10 points of now | habit-writer | Y2, P6 |
| 8 | Bot: a `throw_break_pct` field in `data/harness/newcomer-kihon-bot.json` (proposed 15 for newcomer, 30 / 45 for the other levels in `bot-levels.json`). A newcomer who did the Y6 lesson breaks some throws. The value is a guess until the playtest | Kappa-aggressive wins about 57% → about 70% (replay at throw ×0.67 gives 76%). **Do this instead of** cutting `kappa-aggressive` `at_close_range → throw` 45 → 30; that is the data fallback only if the playtest shows newcomers do not break | Ryo breaks > 10% of Kappa throws | gameplay-programmer | R5, C4, Y6 |
| 9 | Designer: rule on fight time. Either a round timer with a health decision, or no cap (the harness then needs a much higher `max_fight_ticks` and must report it) | Removes the timeout artefact (F6). SK replay 3.5% → 4.7% now, 35% → 48% if the damage scale changes | Timeouts reported apart from losses | designer | R5, R9 |
| 10 | Designer: **the playtest calibration (R5)**. Log damage taken per duel on rows 1–3 and Kitsune fight length for one or two outside players | Decides F1: either the bot is retuned (gameplay-programmer, `bot-levels.json`) or the fights are. The only global data lever is `data/map/node-scenes.json` `tiers.yokai` / `tiers.elder` `damage_dealt_pct` (absent = 100). Setting it would be a Y4 design change; **not recommended before the playtest** | | designer | R5, Y4 |

**Not recommended now:**
- Kappa-patient. Ryo takes 108 against a target of 220; leave it until reach is in band. A later candidate is `opponent_in_recovery → throw` 20 → 35.
- The apprentice's weights. Fix #1 first, since it changes her moves.
- Foxfire move data. One file serves Ryo, the Kitsune and the Tanuki copy (K3), and 70 damage is the A-table value.
- Any Oni or Kappa body tuning while they use the Kitsune body.

## 4. Balance-gate input (G3, mid week 3)

**The data cannot show that modifiers hurt balance, and it cannot show that they help. The cut is a schedule call.**

**Modifiers 14 → 8 (six unlocked stay, the locked ones drop to 2):**
- **For the cut:**
  - MOD, paired on the same seeds and opponents as NC, shows modifiers have no measurable effect: fights won 105 vs 106, reach 0 vs 0.5%.
  - The balance gap (F1, F2) is entirely in the fight layer, and the hours a modifier costs are better spent there.
  - The harness can validate only 2 of the 13 modifier files before the gate. The 7 locked ones are never offered: they are locked, and the draft bug hides the Oni and Kappa pools.
  - Cutting the locked ones removes content that no batch has tested.
- **Against the cut:**
  - The silence is caused by a bug (F7 draft) and a weak bot. It is not evidence that the modifiers are weak.
  - The six unlocked ones that stay include four (Oni and Kappa) that have **never** been drafted.
  - Cutting the locked ones to 2 also shrinks the M2 unlock pool ("12 locked abilities, 4–6 runs"). That pacing is untested too.
- **If the designer wants data before deciding:** fix #2, then rerun MOD (one batch). That gives the 6 unlocked modifiers some exposure, and the result can arrive before the cut lands.

**Dojo trials:**
- No data. The bot never takes the trial (YOK-98), so R7 and R4's train@7 line (≈370) are untested.
- Row 7 is reached by 0–1% (newcomer) to 7.5% (SK) of runs.
- At current balance, cutting trials affects almost no simulated run. That is weak support for the cut, and it says more about F1 than about trials.

**Colour-only presets:** the harness has no bearing on this (visual only).
