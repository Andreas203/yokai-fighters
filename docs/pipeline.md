# Data merge check (YOK-34)

The auto-merge path for content in `data/`. One command decides whether a data change is
**mergeable**; the designer still reviews and merges every PR until they say otherwise. Nothing in
this path merges, pushes or approves on its own.

## Run it

```
pip install -r tools/requirements.txt          # once: jsonschema, for step 1
python3 tools/data_merge_check.py --base origin/main
```

Godot is found from `--godot`, then `$GODOT`, then `godot` on `PATH`. The check runs `dotnet build`
and a headless Godot import in `game/` before the Godot steps; `--no-build` skips both when the
game is already built and imported, but then the verdict is INCOMPLETE (designer 2026-10-17).

| Option | Use |
|---|---|
| `--base REF` | Diff `REF...HEAD` to tell a data-only change from one that touches code. Without it the tree is checked as it is. |
| `--runs N`, `--seed N` | Harness batch (default 200 runs from seed 1, the Sparring Partner's batch). Fewer than 200 runs makes the verdict INCOMPLETE, for quick local runs only. |
| `--only schema,rules` | Run some steps while iterating. Skipped steps make the verdict INCOMPLETE, never MERGEABLE. |
| `--report FILE` | Also write the text report to a file. |
| `--suite-log FILE` | Where the test suite's full output goes (default: a uniquely named temp file). |
| `--json` | The report as one JSON object (verdict, scope, each step's status, details and notes). |
| `--root DIR` | Check another checkout (its `data/` and `game/`, and its `tools/` unless `--tools` is given). |
| `--tools DIR` | Take `validate_data.py` and `check_rules.py` from another folder (CI: the base branch's), still checking `--root`'s data. |

## The steps, in order

The check stops at the first step that fails, so a later step never runs on content an earlier one refused.

| # | Step | What it checks | Fails when |
|---|---|---|---|
| 1 | schema | `tools/validate_data.py`: every file against `data/schema/`, ids, rule IDs, clip references, hitboxes in the active window (YOK-12) | any file is invalid |
| 2 | rules | `tools/check_rules.py`: the written rules over the whole content set, each citing its rules.md ID and GDD section (YOK-35): slots (A1), one modifier per special (A2), cancel-rule caps (X2, A11/X4), once per combo and cancel loops (X3), three levels (A3-A5, E21, P1), meter for EX only (C5, E16), Kata and Kihon on every special (K1, K2, E18), trials with Kihon inputs (R7, K2) | any violation |
| 3 | harness | The whole-run harness (YOK-26): the newcomer Kihon bot plays the batch headless on this `data/` | the harness errors, writes no file or more than one, the file is not a batch, or a run is missing, not an object or has no won/lost outcome |
| 4 | suite | The full headless Godot test suite (`tests/test_runner.tscn`) | any test fails or the runner does not finish |

Not gating, but listed in the report for the reviewer:

- **Runtime items** from step 2 ("harness must confirm"): each trial landed within 3 attempts with Kihon
  inputs, and any cancel cycle bounded only by once per combo. The whole-run harness does not play dojo
  trials or cancel chains, so these are left to the game suite and playtests.
- **Balance numbers** from step 3 (reach and beat rates against R5, rows where runs ended, timeouts). They
  are for the Sparring Partner; a data change is never refused for them.

Many broken samples are refused at step 1 already, because the schema encodes some rules too (for
example `once_per_combo` must be `true`, and every special needs `input.kihon`). Step 2 catches what one
file cannot show: a trial asking for a move Ryo does not have, two starters in one slot, a fourth cancel
rule, a loop through rules held at once.

## Verdicts

| Exit | Verdict | Meaning |
|---|---|---|
| 0 | MERGEABLE | Every step passed on the full 200-run batch after a fresh build, and the change touches only content in `data/`. Ready for the designer's review. |
| 1 | FAIL | A step failed. The report's first line names it (`DATA MERGE CHECK: FAIL at step 2 (rules: ...)`), with the failing files and rule IDs below. |
| 2 | SETUP | The check could not run (no Godot, no jsonschema, build or import failed, bad `--base`), or, with `--base`, the tree has uncommitted changes outside `data/` content. Says nothing about the change. |
| 3 | REVIEW | Every step passed, but the change also touches code: anything but a `.json` file under `data/`, or anything in `data/schema/` (the schemas are the C# loader's contract). A rename counts both paths, so moving a test from `game/` into `data/` is a code change. A symlink or submodule (git mode 120000/160000) is code whatever its name, and so is a path with a `..` segment or a backslash in a root file name. Code goes through Code Critic, Test Breaker and designer review as before. |
| 4 | INCOMPLETE | Every step that ran passed, but `--only` skipped steps, `--runs` was under 200 or `--no-build` was given. No merge verdict. |
| 5 | NOTHING TO MERGE | `--base` shows no changed file. No verdict, nothing to review. |

## CI hook

`.github/workflows/data-merge-check.yml` runs the check on every pull request that touches `data/**`
(and by hand), always with the defaults: 200 runs and a fresh build and import. It diffs against the
PR's base branch, uploads the report and the suite log as the `data-merge-report` artifact and writes
the report to the job summary in a step of its own, from the report file. The check step runs the PR's
game code (harness, suite), so the base checker runs as `python -I` with every runner file command
removed (`GITHUB_STEP_SUMMARY`, `GITHUB_ENV`, `GITHUB_PATH`, `GITHUB_OUTPUT`, `GITHUB_STATE`): that code
cannot write the summary or change the env, path, outputs or state of later steps. The summary and
artifact steps read the literal path `$RUNNER_TEMP/data-merge`, never a path from env.
Residual risk, accepted by the designer (2026-10-18) because every code PR is reviewed: game code can
still find the runner's command files by path (`$RUNNER_TEMP/../_runner_file_commands`). MERGEABLE and NOTHING TO MERGE pass with a notice, REVIEW passes with a
warning, FAIL, SETUP and INCOMPLETE fail the job. The workflow has read-only permissions and never merges.

The checker never comes from the PR itself. The base branch's `tools/` (the checker, `validate_data.py`,
`check_rules.py`) is extracted with `git archive` into `$RUNNER_TEMP`, outside the checkout, and run on
the PR's checkout with `--root` and `--tools` (the validator is pointed at the PR's `data/` through
`YOK_CONTENT_ROOT`). A PR therefore cannot edit the checker to call itself mergeable, and a PR that
changes `tools/` or `game/` is a code change anyway: REVIEW at best. Godot, the report and the suite log
also live in `$RUNNER_TEMP`, so the checkout is clean when the check reads its scope. While the base has
no checker yet (the PR that adds it), the PR's own checker runs and its verdict is capped at REVIEW.
Every action is pinned to a commit SHA, with its version tag in a comment.

## Designer decisions (2026-10-17)

- **Empty diff is nothing to merge.** With `--base`, a diff with no changed file gives
  `NOTHING TO MERGE` (exit 5) before any step runs: never MERGEABLE.
- **Uncommitted changes outside `data/` are refused.** With `--base`, the scope comes from commits
  (`REF...HEAD`), so any staged, unstaged or untracked (not ignored) file that is not content in `data/`
  (a `.json` file outside `data/schema/`) gives SETUP (exit 2), listing the files. Uncommitted content
  files are allowed, for local runs on a draft, but only as regular files: a symlink or nested
  repository named like one is code and gives SETUP. CI is a clean checkout and the check runs before the
  build, so the build's own outputs never trip it. Locally, untracked Godot import outputs (`.uid`,
  `.import`) count: commit or remove them first.
- **MERGEABLE needs the full batch and a fresh build.** `--runs` under 200 or `--no-build` give
  INCOMPLETE (exit 4) even when every step passes. Both flags stay for quick local runs. CI runs the
  defaults (200 runs, build and import).

## Samples

`tools/tests/merge_fixtures/` holds sample changes laid over `data/` by `tools/test_data_merge_check.py`:

| Sample | Expected |
|---|---|
| `valid-story-edit` | passes every step |
| `schema-wrong-type` | FAIL at step 1 (a normal's startup given as text) |
| `rules-trial-foreign-move` | FAIL at step 2, R7 (schema-valid trial that asks for a Kitsune normal) |
| `harness-bot-range` | FAIL at step 3 (bot config with `preferred_min` above `preferred_max`, which only the harness refuses) |

The YOK-35 samples in `tools/tests/rules_fixtures/` (cancel loop, missing Kihon slot) are also caught
before the harness.
