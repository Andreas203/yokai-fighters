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
game is already built and imported.

| Option | Use |
|---|---|
| `--base REF` | Diff `REF...HEAD` to tell a data-only change from one that touches code. Without it the tree is checked as it is. |
| `--runs N`, `--seed N` | Harness batch (default 200 runs from seed 1, the Sparring Partner's batch). |
| `--only schema,rules` | Run some steps while iterating. Skipped steps make the verdict INCOMPLETE, never MERGEABLE. |
| `--report FILE` | Also write the text report to a file. |
| `--suite-log FILE` | Where the test suite's full output goes (default: a uniquely named temp file). |
| `--json` | The report as one JSON object (verdict, scope, each step's status, details and notes). |
| `--root DIR` | Check another checkout (its `tools/`, `data/` and `game/`). |

## The steps, in order

The check stops at the first step that fails, so a later step never runs on content an earlier one refused.

| # | Step | What it checks | Fails when |
|---|---|---|---|
| 1 | schema | `tools/validate_data.py`: every file against `data/schema/`, ids, rule IDs, clip references, hitboxes in the active window (YOK-12) | any file is invalid |
| 2 | rules | `tools/check_rules.py`: the written rules over the whole content set, each citing its rules.md ID and GDD section (YOK-35): slots (A1), one modifier per special (A2), cancel-rule caps (X2, A11/X4), once per combo and cancel loops (X3), three levels (A3-A5, E21, P1), meter for EX only (C5, E16), Kata and Kihon on every special (K1, K2, E18), trials with Kihon inputs (R7, K2) | any violation |
| 3 | harness | The whole-run harness (YOK-26): the newcomer Kihon bot plays the batch headless on this `data/` | the harness errors, or a run is missing or has no won/lost outcome |
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
| 0 | MERGEABLE | Every step passed and the change touches only content in `data/`. Ready for the designer's review. |
| 1 | FAIL | A step failed. The report's first line names it (`DATA MERGE CHECK: FAIL at step 2 (rules: ...)`), with the failing files and rule IDs below. |
| 2 | SETUP | The check could not run (no Godot, no jsonschema, build or import failed, bad `--base`). Says nothing about the change. |
| 3 | REVIEW | Every step passed, but the change also touches code: anything but a `.json` file under `data/`, or anything in `data/schema/` (the schemas are the C# loader's contract). A rename counts both paths, so moving a test from `game/` into `data/` is a code change. Code goes through Code Critic, Test Breaker and designer review as before. |
| 4 | INCOMPLETE | `--only` skipped steps; no merge verdict. |

## CI hook

`.github/workflows/data-merge-check.yml` runs the check on every pull request that touches `data/**`
(and by hand). It diffs against the PR's base branch, uploads the report and the suite log as the
`data-merge-report` artifact and writes the report to the job summary. FAIL and SETUP fail the job;
REVIEW passes with a warning; MERGEABLE passes with a notice. The workflow has read-only permissions and
never merges.

The checker never comes from the PR itself. A second checkout of the base branch supplies `tools/`
(the checker, `validate_data.py`, `check_rules.py`), which replaces the PR's `tools/` before the check
runs on the PR's `data/`, so a PR cannot edit the checker to call itself mergeable. A PR that changes
`tools/` or `game/` is a code change anyway and gets REVIEW at best. While the base has no checker yet
(the PR that adds it), the PR's own checker runs and its verdict is capped at REVIEW. Every action is
pinned to a commit SHA, with its version tag in a comment.

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
