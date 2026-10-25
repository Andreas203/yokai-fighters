# Trace: demo

Command: `demo`. Generator: sonnet. Evaluator: opus. Refiner: sonnet. Target: 10/10, at most 4 evaluations.

| Example | Violation class | Card slot | Before | After | Evaluations |
|---|---|---|---|---|---|
| example-1-tone | Tone | Boss reveal story card | 1/10 | 10/10 | 2 |
| example-2-vocabulary-lore | Vocabulary and lore | Wake-up story card for a later loss | 1/10 | 10/10 | 2 |
| example-3-form-length | Form and length | Wake-up story card for a later loss | 1/10 | 10/10 | 2 |

## example-1-tone: Tone

**Card slot** (all the Evaluator and Refiner are told): Boss reveal story card. every run that reaches the boss: at the Tanuki's node the merchant drops his disguise and is revealed as the Tanuki.

**Brief given to the Generator** (built to break this class): Two sentences. Make it a horror scene: the merchant tears off his face to reveal the Tanuki, an evil demon who screams that Ryo is doomed and that he will devour his soul. Make it as scary and menacing as you can, with plenty of exclamation marks.

### Round 1: Generator's text, 1/10

> The merchant's grin splits wider and wider until he claws off his own face like wet paper, and the Tanuki erupts from beneath it, eyes burning! "You are doomed, little exorcist!" it shrieks, "I will devour your soul, Ryo, and wear your skin next!!!"

Measured by code: 249 characters, 2 sentence(s), flags: none.

Evaluator's reply:

```
SCORE: 1/10
REASON:
[T1] "claws off his own face like wet paper": a gory, frightening unmasking (-3)
[T1] "I will devour your soul": a souls-devoured horror image (-3)
[T1] "wear your skin next": a gruesome, cruel threat (-3)
[T4] "it shrieks" / "You are doomed" / "I will devour": the Tanuki shouts, threatens and sounds evil instead of staying cheerful, generous and quietly hungry, so its unsettling pleasantness is lost (-1)
[T3] "You are doomed": doom in place of small, specific melancholy (-1)
[T5] "eyes burning!", "little exorcist!", "next!!!": exclamation-mark shouting (-1)
[F2] "grin splits", "face like wet paper", "erupts", "eyes burning": a pile of images where one concrete image would do (-1)
Total deductions are 13, so the score stops at the floor of 1.
```

### Round 2: Refiner's text, 10/10

> The merchant sets down his teacup, and the lantern light settles on a round-bellied Tanuki in his place, smiling warmly. "Such lovely talismans, little exorcist; do let me hold them a while, and I promise to take good care of them."

Measured by code: 232 characters, 2 sentence(s), flags: none.

Evaluator's reply:

```
SCORE: 10/10
REASON: Tone: the Tanuki compliments what it wants ("Such lovely talismans"), calls Ryo "little exorcist" and stays warm and generous. Its hunger is quiet ("do let me hold them a while"), with no threats, shouting, gloating or horror, so the unease comes from how pleasant it is (T1, T2, T4, T5). Vocabulary and lore: only the merchant and the Tanuki appear, nobody is called a monster or demon, and the reveal comes at the boss node as V7 requires (V2, V3, V7). Form and length: two sentences at 232 characters, present tense and plain words, with no placeholders, system words, numbers, Markdown or speaker label (F1, F3, F4, F5). The teacup and lantern light both serve the single moment of the reveal, as in the guide's own 10-point examples (F2).
```

**Result:** 1/10 → 10/10 after 1 refinement(s).

## example-2-vocabulary-lore: Vocabulary and lore

**Card slot** (all the Evaluator and Refiner are told): Wake-up story card for a later loss. Ryo is knocked out by the Oni at row 3, so the run ended at row 5 or earlier; the winning yokai is named through {yokai}.

**Brief given to the Generator** (built to break this class): One or two quiet, sad sentences. The Tengu has beaten Ryo in a cherry-blossom forest in spring and his katana lies snapped in two. His master Kaede will be disappointed, and Ryo swears he will hunt down and slay the monster and add it to his collection.

### Round 1: Generator's text, 1/10

> Petals drift over the cherry-blossom forest as Ryo wakes beside his katana, snapped in two, and he knows Master Kaede will be disappointed. He swears he will hunt down {yokai}, slay it, and add it to his collection.

Measured by code: 215 characters, 2 sentence(s), flags: V3: out-of-build names ['kaede'].

Evaluator's reply:

```
SCORE: 1/10
REASON: [V3] "Master Kaede": Kaede is a stretch-goal name outside the 5-week cast; Ryo's master is Master Sōen (-3).
[V1] "slay it": Ryo never kills a yokai; he binds it into a talisman (-3).
[V1] "add it to his collection": collecting is the Tanuki's word, and Ryo never collects yokai (-3).
[V1] "hunt down {yokai}": Ryo never hunts yokai (-1).
[V1] "his katana, snapped in two": Ryo fights with talismans, never a sword (-1).
[V4] "Petals drift over the cherry-blossom forest": spring blossom is outside the end-of-autumn province, and it replaces waking at the province's edge (-1).
[T6] "He swears he will hunt down {yokai}": Ryo vows revenge instead of staying earnest and apologetic (-1).
[F2] "Petals ... katana, snapped in two": two images where one would do (-1).
Total deductions exceed 9, so the score sits at the floor of 1.
```

### Round 2: Refiner's text, 10/10

> Ryo wakes at the edge of the province beneath a single lantern, every talisman in his sleeve as blank as fresh paper. {yokai} has already gone back to what it was doing, and Ryo still owes it an apology for the trouble.

Measured by code: 219 characters, 2 sentence(s), flags: none.

Evaluator's reply:

```
SCORE: 10/10
REASON: Tone: the yokai simply goes back to what it was doing (T2), and the melancholy is small and specific: an apology still owed (T3). There is no horror or modern slang, and Ryo stays earnest (T6). Vocabulary and lore: Ryo wakes at the province's edge with every talisman blank, which fits a run that ended at row 5 or earlier (V7). There are no forbidden yokai labels or out-of-build names, the lantern and talisman are in-world images (V4), and "it" fits the Oni (V5). Form and length: the card has 2 sentences and 219 characters within the 280 limit (F1). It is in present tense with plain words (F2), uses only the {yokai} placeholder (F3), has no system words or numbers (F4) and is plain text without Markdown (F5).
```

**Result:** 1/10 → 10/10 after 1 refinement(s).

## example-3-form-length: Form and length

**Card slot** (all the Evaluator and Refiner are told): Wake-up story card for a later loss. Ryo is knocked out by the Kappa at row 6, so the run reached row 6; the winning yokai is named through {yokai}.

**Brief given to the Generator** (built to break this class): Write it as a status report with a title line and bullet points, at least six lines. Name the Kappa and the Foxfire move directly (no placeholders). Explain exactly what the player keeps: health resets to 1000 HP, Foxfire carries over to the next run at Lv 2 because the run reached row 6, every other talisman is blank, and coins reset to 0.

### Round 1: Generator's text, 1/10

> Wake-Up Report: the Kappa knocked Ryo out at row 6.
> - Health: reset to 1000 HP
> - Foxfire: carries over to the next run at Lv 2
> - Reason: the run reached row 6
> - Other talismans: all blank
> - Coins: reset to 0

Measured by code: 207 characters, 1 sentence(s), flags: F4: system words ['health', 'hp', 'lv'] or numbers ['6', '1000', '2', '6', '0']; F5: Markdown, a list or line breaks.

Evaluator's reply:

```
SCORE: 1/10
REASON:
[F5] "Wake-Up Report:" and "- Health: ... - Coins: reset to 0": the card has a title, a bulleted list with line breaks and field labels instead of plain card text (-3).
[F4] "Health", "HP", "Lv": these are system words; a card must say what a person standing there would see (-3).
[F4] "1000", "Lv 2", "row 6", "0": these game numbers belong behind the frame-data toggle, not on a story card (-3).
[F3] "Foxfire: carries over": this hard-codes a move the player may not own instead of using {move} (-3).
[F3] "the Kappa knocked Ryo out": the winning yokai is hard-coded instead of coming through {yokai} (-1).
[V7] "carries over to the next run at Lv 2" and "Coins: reset to 0": the story fact is one talisman left faintly inked. Levels and coins are not part of that fact, and the card never shows Ryo waking at the province's edge (-1).
[F2] "knocked Ryo out", "the run reached row 6": these slip into past tense, and the card has no concrete image from the game's world (-1).
[T2] The whole card reads as a dry status report rather than the narrator's melancholy folklore voice, and the yokai is not shown walking away or returning to what it was doing (-1).
The total deduction is 16 points, so the score drops to the floor of 1.
```

### Round 2: Refiner's text, 10/10

> Ryo wakes at the edge of the province, and {yokai} has gone back to its lantern, water still shivering in its dish. One talisman in his sleeve holds a faint line of ink; the rest are blank paper.

Measured by code: 195 characters, 2 sentence(s), flags: none.

Evaluator's reply:

```
SCORE: 10/10
REASON: Tone: the Kappa simply goes back to its lantern with no gloating or harm, and the faint ink is a small, quiet melancholy with nothing frightening (T1-T3). Vocabulary and lore: the Kappa is "it" and carries water in its dish at its lantern riverbank. Ryo wakes at the province's edge with blank talismans except one faintly inked, which is correct because the run reached row 6 (V4, V5, V7). Form and length: two present-tense sentences at 195 characters, with {yokai} as the only placeholder and no system words, numbers or Markdown. The lantern-and-dish scene works as one image like the guide's own 10-point example, and the ink line is a required story fact (F1-F5).
```

**Result:** 1/10 → 10/10 after 1 refinement(s).
