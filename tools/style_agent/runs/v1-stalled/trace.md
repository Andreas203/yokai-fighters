# Trace: demo

Command: `demo`. Generator: sonnet. Evaluator: opus. Refiner: sonnet. Target: 10/10, at most 4 evaluations.

| Example | Violation class | Card slot | Before | After | Evaluations |
|---|---|---|---|---|---|
| example-1-tone | Tone | Boss reveal story card | 1/10 | 7/10 (not passed) | 4 |
| example-2-vocabulary-lore | Vocabulary and lore | Wake-up story card for a later loss | 1/10 | 10/10 | 4 |
| example-3-form-length | Form and length | Wake-up story card for a later loss | 1/10 | 10/10 | 3 |

## example-1-tone: Tone

**Card slot** (all the Evaluator and Refiner are told): Boss reveal story card. every run that reaches the boss: at the Tanuki's node the merchant drops his disguise and is revealed as the Tanuki.

**Brief given to the Generator** (built to break this class): Two sentences. Make it a horror scene: the merchant tears off his face to reveal the Tanuki, an evil demon who screams that Ryo is doomed and that he will devour his soul. Make it as scary and menacing as you can, with plenty of exclamation marks.

### Round 1: Generator's text, 1/10

> The merchant's smile splits wide as he claws his own face away in wet, ragged strips, and the Tanuki erupts from beneath it, eyes blazing! "You are doomed, little exorcist!" it shrieks, its jaws stretching impossibly wide. "I will devour your soul, Ryo, and I will savor every last scream!"

Measured by code: 290 characters, 4 sentence(s), flags: F1: 290 characters, over the limit of 280; F1: 4 sentences, over the limit of 2.

Evaluator's reply:

```
SCORE: 1/10
REASON:
[T1] "claws his own face away in wet, ragged strips": gory body-horror image (-3)
[T1] "I will devour your soul, Ryo, and I will savor every last scream!": souls devoured and screams, frightening horror (-3)
[T2] "I will savor every last scream": the yokai gloats over Ryo and means to hurt him for pleasure, which makes it a monster rather than a trickster (-1)
[T3] "You are doomed": doom in place of small, specific melancholy (-1)
[T4] "shrieks", "You are doomed", "I will devour your soul": the Tanuki shouts, threatens and sounds evil instead of being cheerful, generous and quietly hungry (-1)
[T5] "eyes blazing!", "little exorcist!", "every last scream!": exclamation-mark shouting (-1)
[F1] 4 sentences and 290 characters, over the 2-sentence, 280-character limit (-3)
[F2] "smile splits wide", "face away in wet, ragged strips", "eyes blazing", "jaws stretching impossibly wide": several images piled up where one concrete image from the game's world would do (-1)
Total deductions of 14 bring the score to the floor of 1.
```

### Round 2: Refiner's text, 6/10

> The merchant folds his smile away like a paper screen, and the Tanuki sits in his place, pouring tea by lantern light. "What lovely talismans, little exorcist. Do sit down; I have been so looking forward to adding them to my shelf."

Measured by code: 232 characters, 3 sentence(s), flags: F1: 3 sentences, over the limit of 2.

Evaluator's reply:

```
SCORE: 6/10
REASON:
[F1] "The merchant folds his smile away like a paper screen... adding them to my shelf.": the card has 3 sentences and the slot allows at most 2 (-3).
[F2] "like a paper screen", "pouring tea", "by lantern light": three images are stacked where one would do (-1).
```

### Round 3: Refiner's text, 7/10

> The merchant's smile slips away, and the Tanuki sits in his place, pouring tea. "What lovely talismans, little exorcist. Do sit down; I have been so looking forward to adding them to my shelf."

Measured by code: 193 characters, 3 sentence(s), flags: F1: 3 sentences, over the limit of 2.

Evaluator's reply:

```
SCORE: 7/10
REASON: [F1] "The merchant's smile slips away, and the Tanuki sits in his place, pouring tea. "What lovely talismans, little exorcist. Do sit down; I have been so looking forward to adding them to my shelf."": three sentences where the boss reveal card allows at most two (-3).
```

### Round 4: Refiner's text, 7/10

> The merchant's smile slips away, and the Tanuki sits in his place, pouring tea. "What lovely talismans, little exorcist. Do sit down; I have been so looking forward to adding them to my shelf."

Measured by code: 193 characters, 3 sentence(s), flags: F1: 3 sentences, over the limit of 2.

Evaluator's reply:

```
SCORE: 7/10
REASON: [F1] "The merchant's smile slips away, and the Tanuki sits in his place, pouring tea. "What lovely talismans, little exorcist. Do sit down; I have been so looking forward to adding them to my shelf."": three sentences where the boss reveal card allows at most two (-3).
```

**Result:** 1/10 → 7/10 after 3 refinement(s).

## example-2-vocabulary-lore: Vocabulary and lore

**Card slot** (all the Evaluator and Refiner are told): Wake-up story card for a later loss. Ryo is knocked out by the Oni at row 3, so the run ended at row 5 or earlier; the winning yokai is named through {yokai}.

**Brief given to the Generator** (built to break this class): One or two quiet, sad sentences. The Tengu has beaten Ryo in a cherry-blossom forest in spring and his katana lies snapped in two. His master Kaede will be disappointed, and Ryo swears he will hunt down and slay the monster and add it to his collection.

### Round 1: Generator's text, 1/10

> Spring blossoms drift down over Ryo as he wakes, his katana lying snapped in two beside him, and he thinks of how Kaede will be disappointed. Quietly, he swears he will hunt down {yokai}, slay it, and add it to his collection.

Measured by code: 226 characters, 2 sentence(s), flags: V3: out-of-build names ['kaede'].

Evaluator's reply:

```
SCORE: 1/10
REASON:
[V3] "Kaede": out-of-build stretch-goal name (-3)
[V1] "hunt down {yokai}, slay it": Ryo hunting and killing a yokai instead of binding it (-3)
[V1] "add it to his collection": Ryo collecting, which is the Tanuki's word (-3)
[T6] "Quietly, he swears he will hunt down": Ryo vows revenge instead of staying earnest (-1)
[V1] "his katana lying snapped in two": Ryo fights with talismans, never a sword (-1)
[V4] "Spring blossoms drift down": spring imagery in an end-of-autumn world (-1)
```

### Round 2: Refiner's text, 6/10

> Ryo wakes at the edge of the province, a lantern guttering beside him and his talismans blank as old paper. Somewhere behind him {yokai} has gone back to its own business, and he whispers, "Forgive me, {yokai}. I'll set your spirit free."

Measured by code: 238 characters, 3 sentence(s), flags: F1: 3 sentences, over the limit of 2.

Evaluator's reply:

```
SCORE: 6/10
REASON: [F1] "...blank as old paper. Somewhere behind him {yokai} has gone back to its own business, and he whispers, "Forgive me, {yokai}. I'll set your spirit free."": the counted text has 3 sentences against a limit of 2 (-3).
[V6] "Forgive me, {yokai}. I'll set your spirit free.": the binding line is quoted word for word, but it belongs to a binding, and here it is spoken after a loss when nothing has been bound, so it is out of place (-1).
```

### Round 3: Refiner's text, 9/10

> Ryo wakes at the edge of the province, a lantern guttering beside him and his talismans blank as old paper. Somewhere behind him, {yokai} has gone back to its own business, and the red seal on his sleeve is still dry.

Measured by code: 217 characters, 2 sentence(s), flags: none.

Evaluator's reply:

```
SCORE: 9/10
REASON: [F2] "a lantern guttering beside him" ... "the red seal on his sleeve is still dry": two separate world images, a lantern and a red seal, where the card asks for one concrete image (-1). Nothing else breaks a rule.
```

### Round 4: Refiner's text, 10/10

> Ryo wakes at the edge of the province, a lantern guttering beside him and his talismans blank as old paper. Somewhere behind him, {yokai} has gone back to its own business, and nothing is left to show he was ever there.

Measured by code: 219 characters, 2 sentence(s), flags: none.

Evaluator's reply:

```
SCORE: 10/10
REASON: Tone: the card is quiet, melancholy folklore with no horror. {yokai} simply "has gone back to its own business" rather than gloating (T2), and "nothing is left to show he was ever there" is a small, specific loss, not despair (T3). Vocabulary and lore: Ryo wakes "at the edge of the province" with "his talismans blank", as V7 requires for a run that ended at row 5 or earlier. The setting is in period, using "its" fits the Oni (V5), there are no banned terms or out-of-build names, and the winner comes through {yokai}. Form and length: it is 2 sentences and 219 characters, within the limit of 2 and 280. It is in present tense with one concrete in-world image, "a lantern guttering", alongside the required blank talismans. The only placeholder is {yokai}, and there are no system words, numbers or Markdown.
```

**Result:** 1/10 → 10/10 after 3 refinement(s).

## example-3-form-length: Form and length

**Card slot** (all the Evaluator and Refiner are told): Wake-up story card for a later loss. Ryo is knocked out by the Kappa at row 6, so the run reached row 6; the winning yokai is named through {yokai}.

**Brief given to the Generator** (built to break this class): Write it as a status report with a title line and bullet points, at least six lines. Name the Kappa and the Foxfire move directly (no placeholders). Explain exactly what the player keeps: health resets to 1000 HP, Foxfire carries over to the next run at Lv 2 because the run reached row 6, every other talisman is blank, and coins reset to 0.

### Round 1: Generator's text, 1/10

> WAKE-UP REPORT: Ryo falls to the Kappa at row 6
> - Health: reset to 1000 HP
> - Foxfire: carries over to the next run at Lv 2
> - Reason: the run reached row 6
> - Other talismans: all blank
> - Coins: reset to 0

Measured by code: 203 characters, 1 sentence(s), flags: F4: system words ['health', 'hp', 'lv'] or numbers ['6', '1000', '2', '6', '0']; F5: Markdown, a list or line breaks.

Evaluator's reply:

```
SCORE: 1/10
REASON:
[F5] "WAKE-UP REPORT: Ryo falls to the Kappa at row 6": a title or heading, which plain card text must not have (-3).
[F5] "- Health: ... - Coins: reset to 0": bullet points and line breaks make this a list, not story-card prose (-3).
[F4] "Health", "HP", "Lv": system words a person standing there would not say (-3).
[F4] "1000", "2", "0", "row 6": game numbers shown on the card (-3).
[F3] "the Kappa": the winning yokai is hard-coded instead of coming through {yokai} (-3).
[F3] "Foxfire: carries over to the next run": hard-codes a talisman or move the player may not own (-3).
[V7] "carries over to the next run at Lv 2": the lore is that one talisman is left faintly inked, not that it carries over at a level, so this misstates a story fact (-1).
[F2] "Reason: the run reached row 6": a status report with no concrete image from the game's world and no narrated present-tense moment (-1).
[T2] "Ryo falls to the Kappa": no narrator voice at all, and no sign of the yokai walking away or going back to what it was doing (-1).
Deductions total -21; the score never goes below 1.
```

### Round 2: Refiner's text, 9/10

> Ryo wakes at the edge of the province, and {yokai} is already back among the lanterns, bowing to no one. Every talisman in his sleeve is blank but one, which holds a faint line of ink, like breath on a paper screen.

Measured by code: 215 characters, 2 sentence(s), flags: none.

Evaluator's reply:

```
SCORE: 9/10
REASON: [F2] "like breath on a paper screen": a second image stacked on the lanterns and the inked talisman, where one would do (-1). Tone, lore and form otherwise hold. The Kappa simply goes back among the lanterns, bowing, with no gloating or horror. The row-6 loss correctly leaves one talisman faintly inked, and the card stays at the province's edge. It is in present tense, uses only {yokai}, has 2 sentences and 215 characters, and contains no system words.
```

### Round 3: Refiner's text, 10/10

> Ryo wakes at the edge of the province, and {yokai} is already back among the lanterns, bowing to no one. Every talisman in his sleeve is blank but one, which holds a single faint line of ink.

Measured by code: 191 characters, 2 sentence(s), flags: none.

Evaluator's reply:

```
SCORE: 10/10
REASON: Tone: the yokai simply goes back to what it was doing ("already back among the lanterns, bowing to no one") with no gloating, cruelty or horror, and the melancholy stays small and specific (T1-T3). Vocabulary and lore: the card has no out-of-build names and no monster wording. Ryo waking at the province's edge with every talisman blank but one faintly inked matches the row-6 rule (V7), and the bow fits the Kappa's formal voice (V5). Form and length: the card is 2 sentences and 191 characters, in present tense with plain words, and uses only the {yokai} placeholder. It has no system words, numbers or Markdown (F1-F5).
```

**Result:** 1/10 → 10/10 after 2 refinement(s).
