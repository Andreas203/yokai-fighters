# YOK-154 balance gate: whole-run harness, batch 2 (2026-10-18)

Source: `harness/results/whole-run/newcomer-kihon-bot-n200-s1.json` (200 runs, base seed 1, bot `newcomer-kihon-bot`; re-run is byte-identical to the committed batch). Code read: `game/scripts/Harness/*.cs`, `game/scripts/Ai/ProfileAi.cs`, `game/scripts/Sim/Match.cs` (guard), `game/scripts/Fight/FightScene.*.cs`. Config: `data/harness/newcomer-kihon-bot.json`, `data/map/node-scenes.json`, `data/profiles/`, `data/moves/`. All numbers below come from scripts run on the raw runs. No outside playtest notes exist (`production/playtests/` is absent), so the bot has never been calibrated (R5).

## 1. Summary

| Target | Target value | Measured (n) | Verdict |
|---|---|---|---|
| R5 reach Tanuki | 50–70% | 0% (0/200). Runs ended on row 1: 146, row 2: 53, row 3: 1 | FAIL. The cause is mostly the bot and harness (section 2) |
| R5 beat Tanuki | 40–60% of reached | no data (0 reached) | Not measurable |
| R4 arrival, row 4 / row 7 / Tanuki | ≈520 (heal@4 + rest@7) | n=0 / 0 / 0 | Not measurable |
| Y4 apprentice, damage taken per duel | 110 | mean 935, median 1000, sd 151 (n=200). Wins only: mean 758, sd 188 (n=54) | FAIL by 8.5x |
| Y4 yokai (rows 2, 3, 6) | 220 | mean 287, median 221 (n=55) | Looks close, but only because a fight stops when Ryo's health runs out. Ryo arrives with 287 on average and wins 1 of 55 |
| Y4 elder / Tanuki | 280 / 350 | n=0 / 0 | Not measurable |
| R9 run length | 25–40 min | mean 1.9 min, p90 2.7 (n=200; the runs are cut short) | Not measurable. Projection below |
| R8 coins at row 4 | ≈75 | no run reached row 4 (coins at end: mean 6.9) | Not measurable |
| P5 copy rule, elder twists, P4, X3, R7 | | copy_locked 0, 0 elder duels, Kata not run, trial skipped | Not measurable |

The batch can test one fight pairing: the Kihon bot against `kitsune-aggressive`. That profile is used in **255 of 255 fights**, including the apprentice. 37 of the 55 later fights are Oni or Kappa nodes played by a Kitsune stand-in.

## 2. Findings, ranked by impact on a first-time player

**F1. Neither side can block a strike on reaction. This is the main cause, and it comes from the bot and the AI model, not the data.**
- Every strike normal hits within 15 frames: startup 4–11, last active frame 6–15 (Ryo and Kitsune).
- The bot sees the screen 24 ticks late (`reaction_frames` 24) and puts its guard up only when it sees a threat (`NewcomerBot.Next`). By the time it sees a normal, the normal has already hit or whiffed.
- So `block_pct` 50 only works against Foxfire and against moves that follow. The only real guard comes by chance, when a walk-back step happens to hold back (`Match.cs:133`).
- The AI has the same structure (`ProfileAi` "block" on `opponent_attacking`, 22–30f), so the fight becomes an unguarded trade.
- Measured, as damage dealt / damage taken per fight:
  - Apprentice: median 0.81 (p25 0.60, p75 1.03).
  - Yokai: median 0.81 (p25 0.56, p75 1.36).
- Y4 assumes about **4.5:1**: the newcomer takes 220 while dealing 1000. **0 of 255 fights reached 4.5; the best was 3.93.** No single data number closes a 5x gap. It is a difference in how the agents are modelled.

**F2. The reaction-frame tiers have no measurable effect, so the Y4 curve does not exist in the sim yet.** Win chance at full health:
- Apprentice at 30f: about 27% (n=200).
- Yokai at 22f: about 45% (n=55, ±13 points).

If anything, the slower apprentice is harder. This follows from F1: when no one can react to a strike, reacting faster changes little. A fix to the agent model comes before any tuning of the tiers.

**F3. The harness apprentice is not the game's apprentice. It is a harness and game gap, not data.**
- The harness plays row 1 as a full `kitsune-aggressive` at 30f.
- In the game, the first-run apprentice is the guided fight (Y6, `TutorialCoach` overrides the AI during lessons).
- Y4's half damage is not applied **anywhere**: it is not in the harness, `NodeFight` or the sim. `node-scenes.json` notes say "not applied here". This is a missing game feature that the harness reproduces faithfully, not a harness bug.
- Counterfactual: applying half damage at the measured per-fight damage rates gives a row-1 win rate of about **85% (170/200)**, up from 27%. Mean damage taken by the winners would still be about 587, against a target of 110.
- Ryo would then reach row 2 with about 460 health and win there about 15–25% of the time, so reach would stay near 0%.
- Half damage is necessary but not sufficient.

**F4. The bot never punishes a habit.**
- `anti_airs` = 0 across all 255 fights. Kitsune-aggressive jumps only on its habit, after a knockdown.
- The anti-air check fires only on the single tick a jump first appears, and only if the bot is free to act on that tick (`jumpShown && interruptible`). The bot is often mid-move at that moment.
- A Y4/Y2 newcomer is assumed to learn the habit. The bot cannot, so the habit's punish rate (P6/Y2) is untestable.
- This is a bot artefact.

**F5. The bot's specials are random.** About 17 specials per apprentice fight, on a random slot at any range. Rising Talisman (recovery 28) thrown in neutral feeds the AI's `opponent_whiffed` punish (weight 50). This probably costs damage, but the results do not attribute damage to moves, so it can't be quantified. The harness needs per-hit logging.

**F6. Run length (R9) projection, low confidence.**
- A full fight (1000 damage dealt) takes about 3000 ticks, or 50 s.
- 7 fights plus 9 nodes at 45 s menu time comes to about **13–15 min**, short of 25–40.
- Fight time with a human, and the provisional 45 s per node (YOK-36), both need a playtest before this means anything.

**F7. Coverage gaps (G3 relevance):**
- Temperament never varies, in the harness or the game: the first profile is used unless a map node names one, and no node does.
- All 55 cards picked were upgrades (`reward_order` puts upgrade first). **No modifier was ever drafted.**

## 3. Recommendations (smallest change each)

| # | Change | Expected effect | Owner | Rule |
|---|---|---|---|---|
| 1 | Bot: add one config number, a standing guard held while the bot is waiting or walking in range (`idle_guard_pct`, start 50), applied to the existing `wait`/`walk` plans. This replaces relying on reactive blocks that cannot fire against strikes | Exchange ratio rises from about 0.8 toward Y4's 4.5. This is the first lever to measure. Re-run the batch and report the ratio before touching data | gameplay-programmer | R5, Y4, P6 |
| 2 | Sim: apply Y4's apprentice half damage (multiply apprentice-tier P2 damage by 0.5). The game and the harness both pick it up through `NodeFight` | Row-1 wins about 27% → 85%, taken about 935 → 590 (counterfactual, n=200). Alone it does not move reach | gameplay-programmer | Y4 |
| 3 | Harness: per-hit log (attacker move, blocked or not, throw or break) and per-fight AI habit triggers against bot punishes. Make the anti-air check fire on the first tick the bot is free while the opponent is airborne, not only on the jump's first tick | Damage can be attributed to moves (F5, P5 single-move dominance). Habit punish becomes measurable (F4) | gameplay-programmer | P5, P6, Y2 |
| 4 | Harness: a calibration batch with the same seed, `block_pct` 100 and `mistake_pct` 0 (config only, no code) | Shows whether any version of this bot can reach Y4 pace against this AI. If not, the AI model is the problem, not the newcomer settings | gameplay-programmer | R5 |
| 5 | Designer: one outside playtest or the YOK-69 hand play-through, logging damage taken per duel for rows 1–3 | The only thing that can calibrate the bot (R5). Without it, any data tuning is a guess | designer | R5 |
| 6 | Data: **no tuning yet.** Only if the exchange ratio is still below about 2 after #1–#3: lower `kitsune-aggressive` `opponent_whiffed` weight from 50 to 30 | Fewer punishes on the bot's whiffed specials | habit-writer | Y2, Y4 |

Not recommended now:
- Changing Y4 reaction frames or move damage. F2 shows the tiers are inert under the current agents.
- Any Oni or Kappa tuning. Those fights are Kitsune stand-ins until an `oni`/`kappa` entry lands in `node-scenes.json` (waits on YOK-91).

## 4. Balance-gate input (G3: modifiers 14 → 8)

**The data does not support a call either way.** No run drafted a modifier, no run passed row 3, and no elder or Tanuki fight was played. The harness gives no evidence that modifiers cost balance or time. If the gate cuts, the reason should be schedule, not this harness. Re-run after recommendations #1–#3 and set `reward_order` to put modifiers first for one batch before the harness can inform G3.
