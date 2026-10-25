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
