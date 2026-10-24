# Change tracking

Every kind of change has exactly one home. Before you write a new `.md` file to track something, find its row below: the answer is almost always "a Linear ticket", "one line in an existing log" or "the PR".

## Where each change is recorded

| What changed | Recorded in | Written by | When |
|---|---|---|---|
| **The design**: a rule of the game changes | A new amendment `AMn` in `docs/design/gdd-amendments.md`, plus its index row; `docs/design/rules.md` updated and tagged `[AMn]`; an `> **Amended by**` callout under each GDD section it replaces. All of it in one PR (steps in the amendments file) | the author agent; the designer approves | when the designer approves |
| **A ruling inside the rules**: a tuning value, a provisional default accepted, a gate verdict, a spend approval | One line in `docs/decision-log.md`, under its section, with date, ticket and who decided. Never edit old lines; a reversal is a new line | whoever records the decision | the same day |
| **An open question, a proposal or an approval request** | A Linear ticket with the `decision` label. The proposal text goes in the ticket (description, comment or a Linear document). The ticket gets a line on the designer queue, YOK-189 | the agent that raises it | when it is raised |
| **The live state of work** (in progress, blocked, in review) | Linear status, with the `ready-for-review` label for the review queue (`review-queue.md`) | the ticket's owner | as it moves |
| **Code, data, specs or assets** | The PR (`.github/pull_request_template.md`): what changed, design impact, gate verdicts and one changelog line. `CHANGELOG.md` is generated from merged PRs | the PR author | on every PR |
| **Generation jobs and credit spend** | The spec's acceptance section in `assets/specs/`, plus a line under "Assets and spend" in the decision log | asset-smith, rig-specialist, animator | when a job runs |
| **How a screen looks now** | `docs/screenshots/current/` (a new capture overwrites the old) | ui-designer, gameplay-programmer | with the PR that changes it |
| **Balance evidence** | `harness/results/` (raw) and `harness/reports/` (one report per gate) | gameplay-programmer, sparring-partner | per batch |

## The life of a design change

```
question or proposal ──► Linear ticket (label `decision`, listed on YOK-189)
                                │ designer answers on the ticket
               ┌────────────────┴────────────────┐
     changes the GDD                    a ruling within the rules
  AMn + rules.md + GDD callouts       one line in the decision log
               └────────────────┬────────────────┘
                     data / code PR (template)
                                │ merged by the designer
               CHANGELOG.md line (tools/changelog.py) · ticket Done · tick it on YOK-189
```

A proposal never lands in the repo as its own file. If it needs a table or long reasoning, put it on the ticket. When it is approved, the result goes where the table above says, and the ticket keeps the reasoning.

## CHANGELOG.md

`CHANGELOG.md` lists every merged PR by build week. It is generated; never edit it by hand.

```
python tools/changelog.py            # rewrite CHANGELOG.md from GitHub (needs `gh` logged in)
python tools/changelog.py --check    # exit 1 if CHANGELOG.md is out of date
```

Each entry is the PR's `## Changelog` line, or the PR title when that line is missing. A PR whose line is `none` is left out. The producer regenerates it at the end of each production pass and commits it on that pass's branch, never on `main`.

## What CI enforces

`tools/check_docs.py` (`docs-check` workflow, on every PR) fails when:
- a relative Markdown link or image points at a file that does not exist;
- a tracked `.md` file sits outside the known homes listed in the script (`DOC_HOMES`);
- a tracked `.md` file is named like a proposal or a to-do list;
- the amendments index and the `## AMn` headings disagree.

A new kind of document is fine. Give it a home in `DOC_HOMES` and a row in `docs/README.md` in the same PR.
