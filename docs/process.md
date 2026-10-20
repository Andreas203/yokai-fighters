# Process: daily review queue

The designer reviews code once a day, not per ticket. This page says where finished code waits and in what order it is read. Digest format: `docs/digest_template.md`.

## Linear facts
- The team `Yokai-fighters` has these statuses only: Backlog, Todo, In Progress, Done, Canceled, Duplicate. There is **no review status**.
- So a code ticket waiting for the designer stays **In Progress** with its pull request linked (attachment or PR link on the ticket). It moves to Done only when the designer merges.

## Queue convention
A ticket is **in the review queue** when all of these hold:
1. Status is In Progress.
2. The branch `YOK-<n>-<brief-name>` is pushed and its PR is open and linked on the ticket.
3. Build and tests are green, Code Critic is PASS and Test Breaker is PASS (the goal agent applies the same two gates itself).
4. A comment on the ticket says `READY FOR REVIEW` with the PR number, the gate verdicts and the open probe findings, if any.

The producer reads the queue from that comment plus the open PR list; no extra Linear field is needed. Proposed (designer decision, not created): a label `ready-for-review`, added at step 4 and removed on merge, so the queue becomes one Linear filter.

Tickets with a failed gate are not in the queue: they go back to the author with the findings and stay In Progress with no `READY FOR REVIEW` comment.

## Review order
Within the daily batch, review in this order:
1. Never-cut work (G1): copy rule, merchant, Kihon, harness, story cards.
2. Tickets that unblock others (most blocking relations first).
3. Tickets on the current week's milestone (G5), oldest first.
4. Everything else, smallest diff first.

Stacked branches are reviewed base first.

## Cadence
- Three production runs a day; the producer posts one digest at the end of the last run (template in `docs/digest_template.md`).
- Review budget is about 60 h across the build, so the queue is capped at what fits one sitting; the producer flags overflow in the digest instead of dropping tickets.
- Data merges (after schema, Rules Lawyer and harness checks) skip the queue and are only listed in the digest. Code never merges without the designer.

## Designer decisions needed
- Create the `ready-for-review` label (or keep the comment-only convention above).
- Confirm the review order and the queue cap.
- Where the daily digest lives: a comment on the epic F ticket (per YOK-37), and whether decisions in it are also appended to `docs/decision-log.md`.
