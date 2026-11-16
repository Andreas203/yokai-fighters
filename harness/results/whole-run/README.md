# Whole-run harness results

Raw `harness_results` v1 files from `game/tests/run_harness.tscn` (see `docs/codebase-map/harness.md`). The Sparring Partner reads these; reports go to `harness/reports/`.

## Balance gate batch 3 (YOK-167, 2026-10-21)

Every file below was built from main at **566ee05** (merge of #211, YOK-165), `dotnet build` in `game/` (0 errors), and run with Godot 4.7.2-stable mono (console exe, headless), 200 runs each, with `--hit-log`. Game code and `data/` are unchanged from that commit.

| File | Bot | Base seed (runs) | Command args after `--` |
|---|---|---|---|
| `newcomer-kihon-bot-n200-s1.json` | newcomer (bot-levels.json) | 1 (seeds 1-200) | `--runs 200 --seed 1 --level all --hit-log` |
| `intermediate-kihon-bot-n200-s1.json` | intermediate | 1 (seeds 1-200) | same run as above |
| `skilled-kihon-bot-n200-s1.json` | skilled | 1 (seeds 1-200) | same run as above |
| `newcomer-kihon-bot-modifier-first-n200-s1.json` | newcomer, `reward_order` modifier-first | 1 (seeds 1-200) | `--runs 200 --seed 1 --bot <copy> --hit-log` |
| `newcomer-kihon-bot-n200-s201.json` | newcomer | 201 (seeds 201-400) | `--runs 200 --seed 201 --level newcomer --hit-log` |

- **Modifier-first:** `--bot` played a scratch copy of `data/harness/newcomer-kihon-bot.json` with exactly two values changed: `id` = `newcomer-kihon-bot-modifier-first` (so the file name differs) and `run.reward_order` = `["modifier", "upgrade", "new"]` (default `["upgrade", "new", "modifier"]`). A `--bot` batch has `level` null (`summary.levels` key `none`). The copy was not committed; `data/` is unchanged.
- **Second seed:** a batch's runs use seeds base..base+n-1, so `--seed 2` would share 199 of its 200 runs with `--seed 1`. The stability check uses base 201 instead (disjoint runs).
- `newcomer-kihon-bot-n200-s1.json` replaces the YOK-154 (batch 2) file of the same name.
- The `caveats` text still says Oni fights are throw-only; since #210 the Oni throws his own strikes (see `moves_by_profile`, `oni-*`). The caveat string is stale; the numbers are not affected.

## Calibration batch (YOK-158, 2026-11-02)

Built from main at **97d43f9** (merge of #256, YOK-186), `dotnet build` in `game/` (0 errors), Godot 4.7.2-stable mono, headless, 200 runs, `--hit-log`.

| File | Bot | Base seed (runs) | Command args after `--` |
|---|---|---|---|
| `newcomer-kihon-bot-calibration-n200-s1.json` | `data/harness/newcomer-kihon-bot-calibration.json` | 1 (seeds 1-200) | `--runs 200 --seed 1 --bot data/harness/newcomer-kihon-bot-calibration.json --hit-log` |

- **Bot:** the newcomer file with `fight.block_pct` 100 (was 50) and `fight.mistake_pct` 0 (was 15); `idle_guard_pct` stays 50 (YOK-155), every other value is the newcomer's. It is played only through `--bot`; no default or level loads it. A `--bot` batch has `level` null (`summary.levels` key `none`).
- **Question (Sparring Partner rec 4 of YOK-154; R5, Y4):** can any version of this bot reach Y4 pace, about 4.5:1 exchange ratio (unblocked hits landed / taken, decision 2026-10-21), against this AI?
- **Baseline, not committed:** the default newcomer on the same build, `--runs 200 --seed 1 --level newcomer --hit-log`. `newcomer-kihon-bot-n200-s1.json` above stays the YOK-167 file (built at 566ee05), so its numbers differ from this baseline. The baseline is reproducible from 97d43f9 with that command.

| Same build, seed 1, 200 runs | Calibration | Baseline (newcomer) |
|---|---|---|
| Exchange ratio per duel, median (p25 / p75) | 1.18 (0.82 / 2.00) | 1.40 (0.88 / 2.29) |
| Exchange ratio pooled over every duel (`levels.exchange_ratio`) | 1.31 | 1.43 |
| Duels at or above 4.5:1 | 64 / 504 | 68 / 514 |
| Damage ratio (dealt / taken) | 1.82 | 1.87 |
| Best single duel | seed 183 `r3-kappa` (kappa-patient), 17:0 clean hits, 1000:0 damage | seed 94 `r2-kappa` (kappa-patient), 17:0, 1000:0 |
| Row-1 win rate | 200 / 200 | 199 / 200 |
| Reached Tanuki | 1 (0.5%) | 0 (0%) |
| Beat Tanuki | 0 | 0 |
| Duel win rate | 60.3% | 61.1% |
| Stalls (runs / fights) | 0 / 0 | 0 / 0 |

Per-duel ratios count a duel where the opponent landed no clean hit as infinite (22 calibration, 17 baseline duels); the medians and quartiles are order statistics, so they still hold. Blocks rose from 4,772 to 12,841 and mistakes fell from 7,701 to 0, yet the exchange ratio fell: no setting of these two knobs gets near 4.5:1 against this AI.

## YOK-253 smoke batches: spam, punish, crouch guard (2026-11-15)

Folder `yok-253-smoke/`. Built from branch `YOK-253-harness-spam-bot` (from main at **b25858cb**), `dotnet build` in `game/` (0 errors), Godot 4.7.2-stable mono (console exe, headless, `--audio-driver Dummy`), 20 runs each, seed 1 (seeds 1-20), `--hit-log`. **Smoke only:** they show each new bot field working in whole runs. They are not a balance verdict (YOK-256), and 20 runs are too few for one.

| File | Bot | Command args after `--` |
|---|---|---|
| `newcomer-kihon-bot-spam-charge-n20-s1.json` | `data/harness/newcomer-kihon-bot-spam-charge.json` | `--runs 20 --seed 1 --bot data/harness/newcomer-kihon-bot-spam-charge.json --hit-log --out harness/results/whole-run/yok-253-smoke` |
| `skilled-kihon-bot-spam-charge-n20-s1.json` | scratch copy, not committed (see below) | `--runs 20 --seed 1 --bot <copy> --hit-log --out harness/results/whole-run/yok-253-smoke` |
| `newcomer-kihon-bot-punish-n20-s1.json` | `data/harness/newcomer-kihon-bot-punish.json` | same, `--bot data/harness/newcomer-kihon-bot-punish.json` |
| `newcomer-kihon-bot-crouch-guard-n20-s1.json` | `data/harness/newcomer-kihon-bot-crouch-guard.json` | same, `--bot data/harness/newcomer-kihon-bot-crouch-guard.json` |

- **The new fields** (`data/schema/harness-bot.schema.json`; all opt-in; absent = off, with no RNG draw):
  - `fight.special_slot_only` (A-D). While that slot is filled, its special is the bot's only attack. The normal and throw weights count as 0, and mistakes, anti-airs and punishes press that slot. While the slot is empty, the bot plays as usual. With this field set, rewards level that slot first. No NEW card ever replaces the special in that slot, except `prefer_special`'s own card, which may replace it only to put the preferred special there. The one case the bot cannot avoid: A7 makes a pick compulsory, so if every card on offer could only go on that slot, it takes one (A7 also guarantees an upgrade card, so real offers never do this).
  - `run.prefer_special` (a special id). The bot takes that special's NEW card whenever it is offered. With `special_slot_only` also set, that card goes on the spam slot, no other special ever fills that slot, and the special's own slot must be the spam slot: a file that pairs them otherwise, or names a special not in the pool, is rejected when the harness loads it.
  - `fight.punish_after_block_pct`. Each time Ryo enters blockstun on the bot's delayed screen, the bot rolls once. On a success, on Ryo's first free tick, it presses his fastest move that reaches the opponent, but only while the screen still shows the blocked move in recovery. It never reads the opponent's frame data. A punish is armed for one blocked move: if the screen shows another move, or a new use of the same one, before Ryo is free, the punish is dropped.
  - `fight.crouch_guard_pct`. Inside a guard the bot already holds, it rolls once for each low it sees, and for each shot that flies over a crouching body (Foxfire). On a success, it holds down-back.
- **Defaults unchanged (R5).** `newcomer-kihon-bot.json` and `bot-levels.json` set none of these fields. With the new code, `--runs 200 --seed 1 --level newcomer|skilled --hit-log` reproduces `finish-2026-11-12-tuned/` byte for byte: the git blob ids match (`b37f7fd5`, `278ef524`).
- **Skilled spam copy.** This is `newcomer-kihon-bot-spam-charge.json` with the `skilled` level's fight overrides from `bot-levels.json` applied (reaction 14, decide 4, mistake 3, block 80, idle guard 70, throw break 45, anti-air 60, weights jump 4 and wait 6), and `id` = `skilled-kihon-bot-spam-charge`.
- **Baseline** for comparison: runs 1-20 of `finish-2026-11-12-tuned/newcomer-kihon-bot-n200-s1.json`. A run's seeds depend only on its own seed, so those runs are the same 20 runs.

| Smoke (20 runs) | Result |
|---|---|
| Spam, newcomer | It reaches the Oni and drafts Iron-Club Charge in 1 run of 20. It loses most runs in row 2 first, with the slot still empty. In the 2 fights with slot C filled: 27 specials, 0 normals, 0 throws. The charge connected 9 times (hit or blocked). |
| Spam, skilled copy | It drafts the charge 8 times and upgrades it 5 times. In the 22 fights with slot C filled: 337 specials, 0 normals, 0 throws, and 16 of 22 fights won. It reaches the Tanuki 3 times and beats it twice. In seed 18 the Tanuki copies Iron-Club Charge Lv3 (slot C, T2) and lands it 3 times, and Ryo loses. The other two runs locked Spirit Wave, because the charge was not drafted in those runs. |
| Punish | 52 punish rolls (one per blockstun seen), 52 armed (at 100 every roll succeeds), 35 punishes pressed, 0 out of reach. The other 17 blocked moves had ended before Ryo was free (24-tick reaction). Each punish press also counts as a normal or a special in `bot` (35 more presses than before this count was added; the fights are unchanged). Ryo's next clean hit within 60 ticks of blocking an opponent move: 22 of 53, against 8 of 64 in the baseline. |
| Crouch guard | 96 crouch guards, all against Foxfire (no Oni Quake appears in these 20 runs; the tests cover it). Foxfire blocked: 2, against 10 in the baseline (the guard ducks under it). Foxfire hits: 35, against 38. Those hits come when no guard is up, and the field adds no guards. |
