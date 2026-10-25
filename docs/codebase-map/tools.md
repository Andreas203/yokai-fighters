# Tools and CI: `tools/`, `.github/workflows/`

Python dev tools (none ship in the game). Run Python with `-I`. Index: [README.md](README.md).

## Content gates

| File | What it is and how to run it |
|---|---|
| `tools/validate_data.py` | Schema + semantic validator for `data/` (`pip install -r tools/requirements.txt`, jsonschema). `python3 tools/validate_data.py [paths]`; exit 0 valid / 1 errors / 2 missing dependency; errors `<file>: <field.path>: <message>`. Beyond the schema: id = file name, folder kinds, clips match frame data (F2), rule IDs exist, boxes inside windows, Kata/Kihon slots, starters, run-map graph (and R1 layout when listed), stages exist, one id per ability (A11). |
| (cont.) | Mirrors the C# loaders: `apply_effect`, `built_move_errors`, `check_modifier`, `check_retime`, `check_trial`, `whole()`/`combine()`, `STAGE_PATTERN`. Tests `tools/test_validate_data.py`. |
| `tools/check_rules.py` | Static whole-set rules check, stdlib only, never a traceback: A1, A2, A3–A5, X2–X4 (cancel graph loops), C5/E16 meter cost, K1/K2/E18 inputs, R7/K2 trials. `--json`; later roots overlay earlier ones. Runtime-only items print as `harness must confirm`. Exit 0 / 1 / 2. Fixtures `tools/tests/rules_fixtures/`; tests `test_check_rules*.py`. Rules-lawyer runs it too. |
| `tools/data_merge_check.py` | The content auto-merge verdict: schema → rules → whole-run harness (`--data`, must finish every run) → test suite, stopping at the first failure. Exit 0 MERGEABLE / 1 FAIL / 2 SETUP / 3 REVIEW (diff touches non-data) / 4 INCOMPLETE / 5 NOTHING TO MERGE. `--base`, `--runs`, `--only`, `--tools`, `--root`, `--json`. Never merges. Doc `docs/process/data-merge-check.md`; tests `test_data_merge_check*.py`; fixtures `tools/tests/merge_fixtures/`. |

## Docs and repo

| File | What it is and how to run it |
|---|---|
| `tools/check_docs.py` | Docs check over git-tracked `.md`: links resolve, every file in `DOC_HOMES`, no proposal/to-do names, amendment index = headings, and the codebase map (`docs/codebase-map/`: every `game/scripts/**/*.cs` in exactly one area file, no row for an untracked or deleted file, first cells name only scripts, every area linked from the index, rows ≤ 600 chars, area files ≤ 40 KB, index < 8 KB). Exit 0 / 1 / 2. Tests `tools/test_check_docs.py`. |
| `tools/changelog.py` | Writes `CHANGELOG.md` from merged PRs by build week (`## Changelog` line, else the title; `none` leaves it out); `--check` exits 1 when stale. Never edit `CHANGELOG.md` by hand. |
| `tools/sync_codex.py` | Generates the Codex copies of the crew from `.claude/agents/*.md` → `.codex/agents/*.toml` and `.claude/skills/*/SKILL.md` → `.agents/skills/`; edit the sources, never the copies. |
| `tools/export-windows.sh` | Windows export: Godot `--export-release`, copy `data/{clips,modifiers,moves,profiles,story,map,...}` and kit-fallback fixtures to `build/windows/`, zip to `build/YokaiFighters-Demo-Windows.zip`. Needs the 4.7.2 mono templates. |

## Asset tools

| Path | What it is |
|---|---|
| `tools/measure_clip.py`, `tools/strip_clip.py` | Clip gate measurement (F2, G4; numpy) and an animation-only copy of a clip GLB. |
| `tools/blender/` | Headless Blender scripts (rig-specialist, animator): rig checks, weight repair, clip cleanup and measurement, retargeting, texture tools. |
| `tools/ui/`, `tools/fonts/`, `tools/art/` | UI plate import/repair, font line-height trim, turnaround split. |

## Agents

| Path | What it is |
|---|---|
| `tools/goal_agent/` | Goal-oriented coding agent: `scan.py` + `features.json` + `priority.py` rank gaps between rules.md and the code; `run.py gaps` is the producer's gap report; writes the top gap through build, test, code-critic and test-breaker gates. Never commits. Tests `test_goal_agent.py`. |
| `tools/content_pipeline/` | Drafts card text from `vault/` with the writer agents and the Rules Lawyer as critic; output is `proposed`. Run archives in `runs/`. |

## CI (`.github/workflows/`)

`ci.yml` (dotnet build + headless test suite on Godot 4.7.2 mono), `docs-check.yml` (`check_docs.py` on every PR), `data-merge-check.yml` (PRs touching `data/**`, base branch's tools on the PR's data), `codex-sync.yml` (Codex copies in step), `release.yml` (tag `demo-v*` → build, test, export, **draft** release with the zip and SHA-256).
