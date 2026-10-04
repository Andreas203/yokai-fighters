---
name: produce
description: Run one Yokai Fighters production pass. The producer agent triages and tickets outstanding work, then this session dispatches each ticket to the right crew agent through its workflow (content pipeline with the Rules Lawyer gate, asset shortlist, code build-and-test loop, or balance review). Use when the designer says "produce", "run the crew", "work the board" or names ticket ids.
---

# Production pass

You (the main session) are the orchestrator. Subagents cannot call each other, so you carry work between them. Arguments, if given, are ticket ids or a focus area; otherwise work the board's "Next up" list.

## 1. Plan
Run the `producer` agent with any new context (designer notes, harness reports, playtest notes, the arguments). It updates `production/tickets/` and `production/BOARD.md` and returns an ordered dispatch plan.

## 2. Dispatch each ticket by workflow
Mark the ticket `in-progress` before starting and update its status as it moves. Run independent tickets in parallel; respect `depends_on`.

**content** (moves, modifiers, cancels, trials, cards, profiles, story):
1. If the ticket needs a clip that `data/clips/` doesn't have: run `clip-matcher` first.
2. Run the author: `movesmith` (moves, frame data, hitboxes, cards, trials) or `habit-writer` (profiles, story).
3. Run `rules-lawyer` on the files written. On FAIL, send the verdict back to the same author and re-run the gate. Stop after 3 failed rounds and mark the ticket `blocked` with the last verdict for the designer.
4. If a harness exists, run its checks for the affected content (trials completable, cancel loops, copy rule). Content merges only after schema, Rules Lawyer and harness checks pass.

**asset**: run `asset-scout`, then mark the ticket `review` — purchases are the designer's call. After purchase, a follow-up ticket goes to `clip-matcher`.

**code**: run `gameplay-programmer` or `ui-designer`. They work on their own branch with a closed build-and-test loop. Mark the ticket `review` and add it to the daily review queue in `production/BOARD.md`. **Never merge code to main** — designer approval only.

**balance**: run `sparring-partner` on the latest `harness/results/`, then run `producer` again to ticket its recommendations.

## 3. Report
Finish with a short summary for the designer: tickets done, tickets in review (with branches), tickets blocked and why, and any decisions waiting on them (purchases, scope/balance gate cuts, open questions in `docs/design/rules.md`).
