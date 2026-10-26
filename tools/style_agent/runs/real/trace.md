# Trace: real

Command: `demo --real`. Generator: none (real drafts). Evaluator: opus. Refiner: sonnet. Target: 10/10, at most 4 evaluations.

| Example | Violation class | Card slot | Before | After | Evaluations |
|---|---|---|---|---|---|
| real-1-win-1 | Tone | Story card | 7/10 | 10/10 | 2 |
| real-2-lose-mid | Vocabulary and lore | Story card | 8/10 | 10/10 | 2 |
| real-3-elder-oni-intro | Form and length | Elder intro story card | 7/10 | 10/10 | 2 |

## real-1-win-1: Tone

**Card slot** (all the Evaluator and Refiner are told): Story card. Win 1: first Tanuki victory in the story save.

**Source:** `data/story/win-1.json` at `00450a7` (YOK-88), as Habit Writer wrote it: merged in PR #94 and still in main; the audit scored it 8/10. No brief and no Generator: the loop starts from this text.

### Round 1: Habit Writer's draft, 7/10

> "Techniques are like teacups," the Tanuki sighs, "lonely on a shelf, so I helped you grow to have more to love." Ryo's hands are still sticky with ink.

Measured by code: 151 characters, 2 sentence(s), flags: none.

Evaluator's reply:

```
SCORE: 7/10
REASON:
[T4] "the Tanuki sighs": the Tanuki's voice should be cheerful and generous, and a sigh reads as wistful. The line also lacks the clear compliment on what it wants that drives its quiet hunger (-1).
[F2] "so I helped you grow to have more to love": the wording is muddled rather than plain, and it does not follow from the teacup comparison (-1).
[F2] "like teacups... lonely on a shelf" / "Ryo's hands are still sticky with ink": two images where one would do (-1).
```

### Round 2: Refiner's text, 10/10

> "What a lovely hand with a brush you have, little exorcist," the Tanuki says, pouring tea that was not there a moment ago. "I would so love to keep it company."

Measured by code: 160 characters, 2 sentence(s), flags: none.

Evaluator's reply:

```
SCORE: 10/10
REASON: Tone: the Tanuki is cheerful and quietly hungry. It compliments what it wants ("What a lovely hand with a brush you have"), calls Ryo "little exorcist" and never gloats, threatens or shouts. The unease comes from its pleasantness, and nothing in the card is horror, slang or emphasis capitals (T1, T2, T4, T5). Vocabulary and lore: the card uses only in-build names, and the Tanuki naming itself fits a Tanuki-victory card shown at the boss node, so the reveal timing holds (V3, V7). Tea poured from nowhere suits the Tanuki's tea house illusion (V4), and "collect" and "monster" do not appear. Form and length: two sentences, 160 of 280 characters, present-tense narration ("says") and one concrete image, the tea that appears from nothing (F1, F2). It uses no placeholders, system words, numbers, Markdown, title or speaker label (F3, F4, F5).
```

**Result:** 7/10 → 10/10 after 1 refinement(s).

## real-2-lose-mid: Vocabulary and lore

**Card slot** (all the Evaluator and Refiner are told): Story card. Later loss (not the first), Ryo knocked out on rows 4-6.

**Source:** `data/story/lose-mid.json` at `00450a7` (YOK-88), as Habit Writer wrote it: merged in PR #94; the audit found it, and Habit Writer fixed it by hand in YOK-206 (PR #238). No brief and no Generator: the loop starts from this text.

### Round 1: Habit Writer's draft, 8/10

> {yokai} bows, a little too politely, and the lanterns blow out one by one. Ryo wakes with his talismans blank and his pride very much not.

Measured by code: 138 characters, 2 sentence(s), flags: none.

Evaluator's reply:

```
SCORE: 8/10
REASON: [V7] "his talismans blank": this slot covers losses on rows 4-6, and a run that reached row 6 leaves one talisman faintly inked, so the card states the wrong fact for part of its slot (-1).
[V5] "{yokai} bows, a little too politely": bowing with excess politeness is the Kappa's folklore and voice, but {yokai} can also be the proud, blunt Oni or the teasing Kitsune, so the line is off-voice for them (-1).
```

### Round 2: Refiner's text, 10/10

> {yokai} goes back to the lantern light without a word, and Ryo wakes at the province's edge with his talismans blank, or nearly so, and his pride very much not.

Measured by code: 160 characters, 1 sentence(s), flags: none.

Evaluator's reply:

```
SCORE: 10/10
REASON: Tone: {yokai} returns quietly to the lantern light without hurting or gloating over Ryo (T2), the dry joke about his pride is aimed at Ryo by the narrator rather than spoken by him (T5, T6), and nothing is horror or doom (T1, T3). Vocabulary and lore: the wake-up at the province's edge with blank talismans matches the loss facts, "or nearly so" covers a run that reached row 6 (V7), and no out-of-build names or banned terms appear (V1-V3). Form and length: one present-tense sentence of 160 characters (F1), one concrete in-world image of lantern light (F2), only the {yokai} placeholder (F3), and no system words, numbers or Markdown (F4, F5).
```

**Result:** 8/10 → 10/10 after 1 refinement(s).

**The hand fix it got later** (`054431c`, YOK-206; the loop never saw it):

> {yokai} turns away, a little too politely, and the lanterns blow out one by one. Ryo wakes with his pride in pieces and his excuses already half-written.

## real-3-elder-oni-intro: Form and length

**Card slot** (all the Evaluator and Refiner are told): Elder intro story card. Intro card (Y3): entering the Elder Oni elder node.

**Source:** `data/story/elder-oni-armour-intro.json` at `4139b67` (YOK-138), as Habit Writer wrote it: Habit Writer's first draft in PR #152; fixed by hand before the merge, once the designer ruled that Y3's "one-line" means one sentence. No brief and no Generator: the loop starts from this text.

### Round 1: Habit Writer's draft, 7/10

> The Elder Oni plants its club in the snow and does not bother to look up. "Hit me while I swing," it says. "I will finish the swing."

Measured by code: 133 characters, 3 sentence(s), flags: F1: 3 sentences, over the limit of 1.

Evaluator's reply:

```
SCORE: 7/10
REASON: [F1] "The Elder Oni plants its club in the snow and does not bother to look up. "Hit me while I swing," it says. "I will finish the swing."": an elder intro card must be exactly one sentence, but this one has three, counting the sentences in quoted speech (-3).
```

### Round 2: Refiner's text, 10/10

> The Elder Oni plants its club in the snow, not bothering to look up, and says, "Hit me while I swing, and I will finish the swing."

Measured by code: 131 characters, 1 sentence(s), flags: none.

Evaluator's reply:

```
SCORE: 10/10
REASON: Tone: the Elder Oni is proud and blunt, as V5 asks, with no horror, gore, shouting or gloating. "Not bothering to look up" and "I will finish the swing" read as dry warrior pride rather than cruelty, so T1, T2 and T5 hold. Vocabulary and lore: the Elder Oni is in the build cast (V3) and is called "it". It carries its club (V5) and stands in the Oni's snowbound home (V4), with no forbidden names or labels. Form and length: the card is one present-tense sentence of 131 characters, within the 140 limit for an elder intro (F1). It holds one concrete image, the club planted in snow (F2). It has no placeholders, system words, numbers, speaker label or Markdown (F3, F4, F5).
```

**Result:** 7/10 → 10/10 after 1 refinement(s).

**The hand fix it got later** (`5e31b05`, YOK-138; the loop never saw it):

> The Elder Oni plants its club in the snow: "Hit me mid-swing and I will finish the swing."
