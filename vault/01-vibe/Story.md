---
title: Story
status: draft
source: GDD §1.2, §2.2, §2.3, §2.4, §5.2, §7; AM2
updated: 2026-10-10
---

# Story

## The illusion loop

The Tanuki has folded a rural Japanese province, at the end of autumn, into an illusion. Every run is one pass through that illusion. The yokai inside it are real spirits, trapped and made to fight again each loop. When Ryo falls, the loop closes and he wakes at the province's edge to walk in again. Each Tanuki win, up to three, makes the yokai react faster and lean aggressive, framed as the illusion growing stronger.

## The merchant's disguise

A cheerful travelling merchant waits at the row 4 rest stop, and sometimes again at row 7. He is the Tanuki in disguise. He helps Ryo grow so there is more to steal.

- At row 4, still disguised, he says: "That {move} of yours... I'd love one." The move named is the current copy target.
- In every run that reaches the boss, the merchant drops his disguise at the Tanuki's node.
- The Tanuki's theme borrows the merchant's cheerful melody and slowly detunes it.

## Talismans

- Binding: the round-ending blow slows, then an ink stroke seals the yokai into a paper talisman.
- After a loss, Ryo's talismans are blank again.
- The one exception is the carry-over: if the run reached row 6 or further, one talisman stays faintly inked, and that special starts the next run at Lv 2. A run that ended at row 5 or earlier leaves every talisman blank.
- Abilities bought from the merchant appear in grey "borrowed ink" and never carry over.

## Card schedule

The story is delivered through short cards of one or two sentences, so it never interrupts the run for long (S2, S3). No new beats are added to this schedule.

| When | Card | Speaker |
|---|---|---|
| First run, before row 1 | Intro: Master Sōen's rule, "name the debt" | Master Sōen |
| Every binding | "Forgive me, {yokai}. I'll set your spirit free." | Ryo |
| Row 4, merchant still disguised | "That {move} of yours... I'd love one." | Merchant |
| First loss | Wake-up card: the Tanuki names the move it would have stolen, teaching the copy rule | Narrator, with the Tanuki's voice |
| Later losses | Wake-up variants by row reached and by which yokai won | Narrator |
| Every run that reaches the boss | The merchant drops his disguise at the Tanuki's node | Narrator |
| Win 1 | Why the Tanuki collects techniques | Narrator and the Tanuki |
| Win 2 | The Tanuki points out that Ryo's talismans are cages too | The Tanuki |
| Win 3 (ending) | Ryo binds the Tanuki and releases every yokai, his own included: "I said I'd set you free." | Ryo |

## Wake-up cards

A wake-up card shows after a loss. Ryo wakes at the province's edge.

- First loss: a cheerful voice names the move the Tanuki would have stolen. This teaches the copy rule before the player has met the boss.
- Later losses vary by the row reached and by which yokai won. The yokai that won is named through `{yokai}`.
- A wake-up card after a run that ended at row 5 or earlier says the talismans are blank. A wake-up card after a run that reached row 6 or further may say one talisman stays faintly inked.
- The yokai that won does not gloat and does not harm Ryo. It leaves, or goes back to what it was doing.
- A later-loss card does not reveal that the merchant is the Tanuki. The reveal belongs to the boss node.

## Elder intro cards

Each elder's twist is shown three ways: an icon on its map node, a one-line intro card, and a short callout the first time the player meets it (Y3). The intro card is one sentence that lets the player work out the twist before the duel starts.

| Elder | Row | Twist the intro card must teach |
|---|---|---|
| Nine-Tailed Kitsune | 5 or 8 | Reflects any special used twice in a row |
| Elder Oni | 5 or 8 | Armour on every special |
| Elder Kappa | 5 or 8 | Its throws can't be broken |

## Sample cards from the GDD

First loss: "You fall in the tea house, and the paper screens fold shut. Somewhere a cheerful voice says: 'Shame about that Foxfire. I'd have taken good care of it.' You wake at the province's edge. Your talismans are blank, all but one."

Win 2: "'You keep them in paper, I keep them in my sleeves,' the Tanuki laughs. 'Which of us is the collector, little exorcist?' Ryo has no answer yet."

Cards already in the game (`data/story/`): the binding line (final); a lose-screen card, "{yokai} slips off between the lanterns, tails swaying, and does not look back. Somewhere a bound thing is still waiting for someone to return it." (proposed); and a demo-complete card (proposed).

## Saving

Story progress has its own save flag, independent of unlocks, so the first reveal and the first-loss card never depend on unlocks (M4). There is no post-game mode.

## Open questions

- The GDD's two sample cards run longer than the "one or two sentences" rule (S2) they sit under. Until the designer says otherwise, new cards follow S2 and the samples are read for voice only.
- Proposed, not in the GDD: a later-loss card does not reveal that the merchant is the Tanuki. Should it, once the player has seen the reveal?
- How many later-loss variants ship: one per yokai, one per row band, or the full grid?
