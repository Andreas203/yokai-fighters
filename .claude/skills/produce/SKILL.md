---
name: produce
description: Run one Yokai Fighters production pass. The producer agent triages and tickets outstanding work, then this session dispatches each ticket to the right crew agent through its workflow (content pipeline with the Rules Lawyer gate, asset generation specs, clip specs, sound shortlist, code build-and-test loop, or balance review). Use when the designer says "produce", "run the crew", "work the board" or names ticket ids.
---

# Production pass

You (the main session) are the orchestrator. Subagents cannot call each other, so you carry work between them. Tickets live in Linear (team `YOK`). Arguments, if given, are ticket ids (`YOK-<n>`) or a focus area; otherwise work the producer's dispatch plan.

## 1. Plan
Run the `producer` agent with any new context (designer notes, harness reports, playtest notes, the arguments). It creates and updates tickets in Linear and returns an ordered dispatch plan with a branch name per ticket.

## 2. Dispatch each ticket by workflow
Move the Linear ticket to in progress before starting and update its status as it moves. Run independent tickets in parallel; respect blocking relations.

Every branch and PR title is `YOK-<number>-<brief-name>` for its ticket (e.g. `YOK-19-generic-throws`), or `YOK-<brief-name>` when no ticket exists. Tell each agent its branch name.

**content** (moves, modifiers, cancels, trials, cards, profiles, story):
1. If the ticket needs a clip that `data/clips/` doesn't have: run `clip-matcher` first.
2. Run the author: `movesmith` (moves, frame data, hitboxes, cards, trials) or `habit-writer` (profiles, story).
3. Run `rules-lawyer` on the files written. On FAIL, send the verdict back to the same author and re-run the gate. Stop after 3 failed rounds and mark the Linear ticket blocked, commenting the last verdict for the designer.
4. If a harness exists, run its checks for the affected content (trials completable, cancel loops, copy rule). Content merges only after schema, Rules Lawyer and harness checks pass.

**generate** (models, stages, UI/VFX textures): run `asset-smith` to write the spec, then move the ticket to review and list the job and its credit estimate for the designer. Once they approve, run `asset-smith` again to run the job through the Meshy MCP and do the acceptance check. RETAKE goes back to the designer for approval with the change and its cost; after 3 takes, mark it blocked with Asset Smith's fallback.

**clip**: run `clip-matcher` to write the clip spec, then list the take and its credit estimate for the designer. Once they approve, run `clip-matcher` to run the take through the Meshy MCP and measure it and write `data/clips/`, then continue with the content workflow. Never run a Meshy job without the designer's approval, and never hand-key animation.

**sound**: run `sound-scout`, then move the ticket to review — purchases are the designer's call. After purchase, a follow-up ticket goes to `clip-matcher` for sound cues.

**code**: run `gameplay-programmer` or `ui-designer`. They work on their own branch with a closed build-and-test loop. Open a PR titled with the branch name, move the ticket to review and add it to the daily review queue. **Never merge code to main** — designer approval only.

**balance**: run `sparring-partner` on the latest `harness/results/`, then run `producer` again to ticket its recommendations.

## 3. Report
Finish with a short summary for the designer: tickets done, tickets in review (with branches), tickets blocked and why, and any decisions waiting on them (generation jobs to run, purchases, scope/balance gate cuts, open questions in `docs/design/rules.md`).
