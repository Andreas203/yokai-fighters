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
