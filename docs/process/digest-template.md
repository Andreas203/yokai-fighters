# Daily digest template

Posted once a day by the producer as a comment on the epic F ticket. Keep it to one screen. Write "none" for an empty section, never drop a section.

```
# Digest YYYY-MM-DD (build week N of 5, G5 freeze in X days)

## Review queue (read in this order)
| # | Ticket | PR | Gates | Size | Note |
|---|--------|----|-------|------|------|
| 1 | YOK-n title | #nnn | Critic PASS, Breaker PASS | +a/-b | one line, unblocks YOK-m |

## Done since last digest
- YOK-n title (PR #nnn, merged by designer | closed)

## Auto-merged data
- file or ticket: schema OK, Rules Lawyer PASS, harness OK

## Blocked
- YOK-n: blocked by YOK-m or named reason, and who unblocks it

## Waiting on the designer
- Generation jobs to approve: spec path, credits
- Purchases / gate decisions / open questions (rule ID)
- Code merges: see review queue

## Gaps and schedule
- Gap report: N open, M ticketed, K blocked, J waiting on designer (top 3 by score)
- Milestone risk and the next cut from G2/G3 if velocity slips (proposal only)

## Spend
- Tokens today: X; week to date: Y of W budget (W1 about 4.5M)
- Generation credits today: A; total approved: B
```

Rules: cite rule IDs and ticket IDs, no emojis, never answer design questions (those stay with the designer).
