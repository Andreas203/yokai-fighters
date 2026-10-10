# Trace: v1-single-query

Command: `generate`. Retrieval: v1. Generator: sonnet. Critic: opus.
Vault: 83 chunks from 13 notes.

| Piece | Rounds | Issues caught | Result |
|---|---|---|---|
| Wake-up card: Oni wins at row 2 or 3 | 1 | 0 | passed first time |
| Wake-up card: Nine-Tailed Kitsune wins at row 5 | 2 | 1 | corrected, then passed |
| Wake-up card: Kappa wins at row 6 | 1 | 0 | passed first time |
| Elder intro card: Nine-Tailed Kitsune | 1 | 0 | passed first time |
| Elder intro card: Elder Oni | 1 | 0 | passed first time |
| Elder intro card: Elder Kappa | 1 | 0 | passed first time |
| Reward card text: Oni's Hide | 1 | 0 | passed first time |
| Reward card text: Iron Will | 2 | 2 | corrected, then passed |
| Reward card text: Slippery Skin | 1 | 0 | passed first time |
| Reward card text: River Pull | 1 | 0 | passed first time |

Schema check (`tools/validate_data.py`) on 6 story cards: all valid.

```
validate_data: 6 file(s), 6 valid, 0 invalid (6 with 'proposed' values awaiting the designer)
```

## Wake-up card: Oni wins at row 2 or 3

Brief: Write the wake-up story card shown after Ryo loses a duel to the Oni at row 2 or 3. The run ended at row 5 or earlier.

### Query, retrieved chunk, output

| Query | Retrieved chunk (score) | Output |
|---|---|---|
| **request** in whole vault:<br>Wake-up story card shown after Ryo loses a duel to the Oni at row 2 or 3. | **01-vibe/Story.md#Wake-up cards** (17.868)<br>A wake-up card shows after a loss. Ryo wakes at the province's edge.<br><br>- First loss: a cheerful voice names the move the Tanuki would have stolen. This teaches the copy rule before the player has met the boss.<br>- Later losses vary by the row reached and by which yokai won. The yokai that won is named through `{yokai}`.<br>- A wake-up card after a run that ended at row 5 or earlier says the talismans are blank. A wake-up card after a run that reached row 6 or further may say one talisman stays faintly inked.<br>- The yokai that won does not gloat and does not harm Ryo. It leaves, or goes back to what it was doing.<br>- A later-loss card does not reveal that the merchant is the Tanuki. The reveal belongs to the boss node. | Ryo wakes at the province's edge, every talisman blank. Whatever {yokai} wanted, it has already gone back to its own business, leaving him only the dust and the walk back. |
| **request** in whole vault:<br>Wake-up story card shown after Ryo loses a duel to the Oni at row 2 or 3. | **01-vibe/Story.md#Card schedule** (13.313)<br>The story is delivered through short cards of one or two sentences, so it never interrupts the run for long (S2, S3). No new beats are added to this schedule.<br><br>\| When \| Card \| Speaker \|<br>\|---\|---\|---\|<br>\| First run, before row 1 \| Intro: Master Sōen's rule, "name the debt" \| Master Sōen \|<br>\| Every binding \| "Forgive me, {yokai}. I'll set your spirit free." \| Ryo \|<br>\| Row 4, merchant still disguised \| "That {move} of yours... I'd love one." \| Merchant \|<br>\| First loss \| Wake-up card: the Tanuki names the move it would have stolen, teaching the copy rule \| Narrator, with the Tanuki's voice \|<br>\| Later losses \| Wake-up variants by row reached and by which yokai won \| Narrator \|<br>\| Every run that reaches the boss \| The merchant drops his disguise at the Tanuki's node \| Narrator \|<br>\| Win 1 \| Why the Tanuki collects techniques \| Narrator and the Tanuki \|<br>\| Win 2 \| The Tanuki points out that Ryo's talismans are cages too \| The Tanuki \|<br>\| Win 3 (ending) \| Ryo binds the Tanuki and releases every yokai, his own included: "I said I'd set you free." \| Ryo \| |  |
| **request** in whole vault:<br>Wake-up story card shown after Ryo loses a duel to the Oni at row 2 or 3. | **01-vibe/Story.md#Elder intro cards** (13.264)<br>Each elder's twist is shown three ways: an icon on its map node, a one-line intro card, and a short callout the first time the player meets it (Y3). The intro card is one sentence that lets the player work out the twist before the duel starts.<br><br>\| Elder \| Row \| Twist the intro card must teach \|<br>\|---\|---\|---\|<br>\| Nine-Tailed Kitsune \| 5 or 8 \| Reflects any special used twice in a row \|<br>\| Elder Oni \| 5 or 8 \| Armour on every special \|<br>\| Elder Kappa \| 5 or 8 \| Its throws can't be broken \| |  |
| **request** in whole vault:<br>Wake-up story card shown after Ryo loses a duel to the Oni at row 2 or 3. | **02-mechanics/Run-Loop.md#The map** (11.419)<br>Eight rows, one node per row (R1). Each node shows which yokai waits there, so choosing a path is choosing which powers you can earn (R2).<br><br>\| Row \| Node \|<br>\|---\|---\|<br>\| 1 \| Apprentice duel, guided on the first run \|<br>\| 2 \| Duel \|<br>\| 3 \| Duel \|<br>\| 4 \| Rest stop: always the merchant \|<br>\| 5 \| Duel: always an elder \|<br>\| 6 \| Duel \|<br>\| 7 \| Rest stop: the merchant again, or a dojo \|<br>\| 8 \| Duel: always an elder \|<br>\| After 8 \| The Tanuki \|<br><br>That is 6 duels, two of them against elders, and 2 rest stops. |  |

### Critic loop

**Round 1 draft:** Ryo wakes at the province's edge, every talisman blank. Whatever {yokai} wanted, it has already gone back to its own business, leaving him only the dust and the walk back.

Code checks: clean. Critic: pass.

**Final (passed):** Ryo wakes at the province's edge, every talisman blank. Whatever {yokai} wanted, it has already gone back to its own business, leaving him only the dust and the walk back.

## Wake-up card: Nine-Tailed Kitsune wins at row 5

Brief: Write the wake-up story card shown after Ryo loses a duel to the Nine-Tailed Kitsune at row 5. The run ended at row 5 or earlier.

### Query, retrieved chunk, output

| Query | Retrieved chunk (score) | Output |
|---|---|---|
| **request** in whole vault:<br>Wake-up story card shown after Ryo loses a duel to the Nine-Tailed Kitsune at row 5. | **01-vibe/Story.md#Elder intro cards** (20.421)<br>Each elder's twist is shown three ways: an icon on its map node, a one-line intro card, and a short callout the first time the player meets it (Y3). The intro card is one sentence that lets the player work out the twist before the duel starts.<br><br>\| Elder \| Row \| Twist the intro card must teach \|<br>\|---\|---\|---\|<br>\| Nine-Tailed Kitsune \| 5 or 8 \| Reflects any special used twice in a row \|<br>\| Elder Oni \| 5 or 8 \| Armour on every special \|<br>\| Elder Kappa \| 5 or 8 \| Its throws can't be broken \| | Ryo wakes at the province's edge, his talismans blank as unwritten paper. Far off, {yokai} slips back into the bamboo, her tails swaying in the dusk, and does not look back. |
| **request** in whole vault:<br>Wake-up story card shown after Ryo loses a duel to the Nine-Tailed Kitsune at row 5. | **01-vibe/Story.md#Wake-up cards** (19.038)<br>A wake-up card shows after a loss. Ryo wakes at the province's edge.<br><br>- First loss: a cheerful voice names the move the Tanuki would have stolen. This teaches the copy rule before the player has met the boss.<br>- Later losses vary by the row reached and by which yokai won. The yokai that won is named through `{yokai}`.<br>- A wake-up card after a run that ended at row 5 or earlier says the talismans are blank. A wake-up card after a run that reached row 6 or further may say one talisman stays faintly inked.<br>- The yokai that won does not gloat and does not harm Ryo. It leaves, or goes back to what it was doing.<br>- A later-loss card does not reveal that the merchant is the Tanuki. The reveal belongs to the boss node. |  |
| **request** in whole vault:<br>Wake-up story card shown after Ryo loses a duel to the Nine-Tailed Kitsune at row 5. | **01-vibe/Story.md#Card schedule** (11.61)<br>The story is delivered through short cards of one or two sentences, so it never interrupts the run for long (S2, S3). No new beats are added to this schedule.<br><br>\| When \| Card \| Speaker \|<br>\|---\|---\|---\|<br>\| First run, before row 1 \| Intro: Master Sōen's rule, "name the debt" \| Master Sōen \|<br>\| Every binding \| "Forgive me, {yokai}. I'll set your spirit free." \| Ryo \|<br>\| Row 4, merchant still disguised \| "That {move} of yours... I'd love one." \| Merchant \|<br>\| First loss \| Wake-up card: the Tanuki names the move it would have stolen, teaching the copy rule \| Narrator, with the Tanuki's voice \|<br>\| Later losses \| Wake-up variants by row reached and by which yokai won \| Narrator \|<br>\| Every run that reaches the boss \| The merchant drops his disguise at the Tanuki's node \| Narrator \|<br>\| Win 1 \| Why the Tanuki collects techniques \| Narrator and the Tanuki \|<br>\| Win 2 \| The Tanuki points out that Ryo's talismans are cages too \| The Tanuki \|<br>\| Win 3 (ending) \| Ryo binds the Tanuki and releases every yokai, his own included: "I said I'd set you free." \| Ryo \| |  |
| **request** in whole vault:<br>Wake-up story card shown after Ryo loses a duel to the Nine-Tailed Kitsune at row 5. | **01-vibe/Characters.md#Kitsune** (11.345)<br>The fox of the bamboo grove at dusk, a zoner who holds the screen with slow foxfire. She teases and slips away. She wears a full white fox mask with red markings for the whole fight, and has three tails; the Nine-Tailed elder has nine. Her hit sparks are foxfire orange.<br><br>Folklore reference: kitsune are fox spirits that grow more tails with age, up to nine, and kitsunebi (foxfire) is the ghost light they carry.<br><br>Reference images: `docs/design/characters/kitsune/`. |  |

### Critic loop

**Round 1 draft:** Ryo wakes at the province's edge, his talismans blank as unwritten paper. Far off, {yokai} slips back into the bamboo, her foxfire dimming behind her, and does not look back.

| Caught by | Kind | Quote | Problem | Evidence | Fix |
|---|---|---|---|---|---|
| code | lore_break | Foxfire | The card names the move 'Foxfire', which the player may not own in this run. | 01-vibe/Tone.md#Card form | Remove the move name. |

**Round 2 draft:** Ryo wakes at the province's edge, his talismans blank as unwritten paper. Far off, {yokai} slips back into the bamboo, her tails swaying in the dusk, and does not look back.

Code checks: clean. Critic: pass.

**Final (passed):** Ryo wakes at the province's edge, his talismans blank as unwritten paper. Far off, {yokai} slips back into the bamboo, her tails swaying in the dusk, and does not look back.

## Wake-up card: Kappa wins at row 6

Brief: Write the wake-up story card shown after Ryo loses a duel to the Kappa at row 6. The run reached row 6.

### Query, retrieved chunk, output

| Query | Retrieved chunk (score) | Output |
|---|---|---|
| **request** in whole vault:<br>Wake-up story card shown after Ryo loses a duel to the Kappa at row 6. | **01-vibe/Story.md#Wake-up cards** (19.227)<br>A wake-up card shows after a loss. Ryo wakes at the province's edge.<br><br>- First loss: a cheerful voice names the move the Tanuki would have stolen. This teaches the copy rule before the player has met the boss.<br>- Later losses vary by the row reached and by which yokai won. The yokai that won is named through `{yokai}`.<br>- A wake-up card after a run that ended at row 5 or earlier says the talismans are blank. A wake-up card after a run that reached row 6 or further may say one talisman stays faintly inked.<br>- The yokai that won does not gloat and does not harm Ryo. It leaves, or goes back to what it was doing.<br>- A later-loss card does not reveal that the merchant is the Tanuki. The reveal belongs to the boss node. | You wake at the province's edge. {yokai} has already gone back to what it was doing, and one talisman stays faintly inked. |
| **request** in whole vault:<br>Wake-up story card shown after Ryo loses a duel to the Kappa at row 6. | **01-vibe/Story.md#Elder intro cards** (13.39)<br>Each elder's twist is shown three ways: an icon on its map node, a one-line intro card, and a short callout the first time the player meets it (Y3). The intro card is one sentence that lets the player work out the twist before the duel starts.<br><br>\| Elder \| Row \| Twist the intro card must teach \|<br>\|---\|---\|---\|<br>\| Nine-Tailed Kitsune \| 5 or 8 \| Reflects any special used twice in a row \|<br>\| Elder Oni \| 5 or 8 \| Armour on every special \|<br>\| Elder Kappa \| 5 or 8 \| Its throws can't be broken \| |  |
| **request** in whole vault:<br>Wake-up story card shown after Ryo loses a duel to the Kappa at row 6. | **01-vibe/Story.md#Card schedule** (11.61)<br>The story is delivered through short cards of one or two sentences, so it never interrupts the run for long (S2, S3). No new beats are added to this schedule.<br><br>\| When \| Card \| Speaker \|<br>\|---\|---\|---\|<br>\| First run, before row 1 \| Intro: Master Sōen's rule, "name the debt" \| Master Sōen \|<br>\| Every binding \| "Forgive me, {yokai}. I'll set your spirit free." \| Ryo \|<br>\| Row 4, merchant still disguised \| "That {move} of yours... I'd love one." \| Merchant \|<br>\| First loss \| Wake-up card: the Tanuki names the move it would have stolen, teaching the copy rule \| Narrator, with the Tanuki's voice \|<br>\| Later losses \| Wake-up variants by row reached and by which yokai won \| Narrator \|<br>\| Every run that reaches the boss \| The merchant drops his disguise at the Tanuki's node \| Narrator \|<br>\| Win 1 \| Why the Tanuki collects techniques \| Narrator and the Tanuki \|<br>\| Win 2 \| The Tanuki points out that Ryo's talismans are cages too \| The Tanuki \|<br>\| Win 3 (ending) \| Ryo binds the Tanuki and releases every yokai, his own included: "I said I'd set you free." \| Ryo \| |  |
| **request** in whole vault:<br>Wake-up story card shown after Ryo loses a duel to the Kappa at row 6. | **01-vibe/Tone.md#Voice: the narrator** (11.013)<br>The narrator is quiet and observant, in present tense. In the GDD's sample cards the wake-up card addresses the player as "you" ("You wake at the province's edge.") and the win card names Ryo ("Ryo has no answer yet."). The narrator describes what is seen and lets the cost stay unspoken or half spoken. |  |

### Critic loop

**Round 1 draft:** You wake at the province's edge. {yokai} has already gone back to what it was doing, and one talisman stays faintly inked.

Code checks: clean. Critic: pass.

**Final (passed):** You wake at the province's edge. {yokai} has already gone back to what it was doing, and one talisman stays faintly inked.

## Elder intro card: Nine-Tailed Kitsune

Brief: Write the one-line intro card shown on the map node of the Nine-Tailed Kitsune, before the duel starts.

### Query, retrieved chunk, output

| Query | Retrieved chunk (score) | Output |
|---|---|---|
| **request** in whole vault:<br>One-line intro card for the Nine-Tailed Kitsune that teaches its elder twist before the duel. | **01-vibe/Story.md#Elder intro cards** (26.583)<br>Each elder's twist is shown three ways: an icon on its map node, a one-line intro card, and a short callout the first time the player meets it (Y3). The intro card is one sentence that lets the player work out the twist before the duel starts.<br><br>\| Elder \| Row \| Twist the intro card must teach \|<br>\|---\|---\|---\|<br>\| Nine-Tailed Kitsune \| 5 or 8 \| Reflects any special used twice in a row \|<br>\| Elder Oni \| 5 or 8 \| Armour on every special \|<br>\| Elder Kappa \| 5 or 8 \| Its throws can't be broken \| | The Nine-Tailed Kitsune mirrors any special you repeat, so throw the same one twice and it comes straight back at you. |
| **request** in whole vault:<br>One-line intro card for the Nine-Tailed Kitsune that teaches its elder twist before the duel. | **01-vibe/Art-and-Audio.md#Elder twists on screen** (15.956)<br>Each elder's twist appears as an icon on its map node, a one-line intro card and a short callout the first time a player meets it. The fight HUD keeps a one-line reminder, for example "Elder twist: specials used twice are reflected". |  |
| **request** in whole vault:<br>One-line intro card for the Nine-Tailed Kitsune that teaches its elder twist before the duel. | **02-mechanics/Yokai.md#Kitsune** (14.287)<br>The Kitsune is the zoner. It offers Foxfire, Fox Mirage and the Kitsune modifiers.<br><br>\| \| \|<br>\|---\|---\|<br>\| Aggressive temperament \| Throws foxfire at mid range while advancing \|<br>\| Patient temperament \| Holds full screen and anti-airs jumps \|<br>\| Habit to punish \| Often jumps after a knockdown \|<br>\| Elder \| Nine-Tailed Kitsune \|<br>\| Elder twist \| Reflects any special used twice in a row \|<br>\| Habit the twist breaks \| Repeating one special, so the player learns to mix specials \|<br>\| Elder offers \| The cancel rule Fox Step \| |  |
| **request** in whole vault:<br>One-line intro card for the Nine-Tailed Kitsune that teaches its elder twist before the duel. | **02-mechanics/Yokai.md#Elders** (13.252)<br>Rows 5 and 8 always hold elders. Each elder adds one rule twist that breaks a common habit, so a strong build never wins on autopilot (P5). The twist appears as an icon on the map node, a one-line intro card and a short callout the first time the player meets it (Y3). The row-5 elder pays 50 coins; the row-8 elder pays a rare ability instead. |  |

### Critic loop

**Round 1 draft:** The Nine-Tailed Kitsune mirrors any special you repeat, so throw the same one twice and it comes straight back at you.

Code checks: clean. Critic: pass.

**Final (passed):** The Nine-Tailed Kitsune mirrors any special you repeat, so throw the same one twice and it comes straight back at you.

## Elder intro card: Elder Oni

Brief: Write the one-line intro card shown on the map node of the Elder Oni, before the duel starts.

### Query, retrieved chunk, output

| Query | Retrieved chunk (score) | Output |
|---|---|---|
| **request** in whole vault:<br>One-line intro card for the Elder Oni that teaches its elder twist before the duel. | **01-vibe/Story.md#Elder intro cards** (24.968)<br>Each elder's twist is shown three ways: an icon on its map node, a one-line intro card, and a short callout the first time the player meets it (Y3). The intro card is one sentence that lets the player work out the twist before the duel starts.<br><br>\| Elder \| Row \| Twist the intro card must teach \|<br>\|---\|---\|---\|<br>\| Nine-Tailed Kitsune \| 5 or 8 \| Reflects any special used twice in a row \|<br>\| Elder Oni \| 5 or 8 \| Armour on every special \|<br>\| Elder Kappa \| 5 or 8 \| Its throws can't be broken \| | The Elder Oni shrugs off every blow with armour on all its specials, so mashing jabs to interrupt it will get you flattened. |
| **request** in whole vault:<br>One-line intro card for the Elder Oni that teaches its elder twist before the duel. | **01-vibe/Art-and-Audio.md#Elder twists on screen** (19.086)<br>Each elder's twist appears as an icon on its map node, a one-line intro card and a short callout the first time a player meets it. The fight HUD keeps a one-line reminder, for example "Elder twist: specials used twice are reflected". |  |
| **request** in whole vault:<br>One-line intro card for the Elder Oni that teaches its elder twist before the duel. | **02-mechanics/Yokai.md#Oni** (16.554)<br>The Oni is the rushdown yokai. Its relentless pressure teaches the player to block. It offers Iron-Club Charge, Oni Quake and the Oni modifiers.<br><br>\| \| \|<br>\|---\|---\|<br>\| Aggressive temperament \| Dashes in constantly \|<br>\| Patient temperament \| Walks in and punishes whiffs with its charge \|<br>\| Habit to punish \| Dashes in after blocking a projectile \|<br>\| Elder \| Elder Oni \|<br>\| Elder twist \| Armour on every special \|<br>\| Habit the twist breaks \| Mashing jabs to interrupt it stops working \|<br>\| Elder offers \| The cancel rule Oni Chain \| |  |
| **request** in whole vault:<br>One-line intro card for the Elder Oni that teaches its elder twist before the duel. | **02-mechanics/Yokai.md#Elders** (16.549)<br>Rows 5 and 8 always hold elders. Each elder adds one rule twist that breaks a common habit, so a strong build never wins on autopilot (P5). The twist appears as an icon on the map node, a one-line intro card and a short callout the first time the player meets it (Y3). The row-5 elder pays 50 coins; the row-8 elder pays a rare ability instead. |  |

### Critic loop

**Round 1 draft:** The Elder Oni shrugs off every blow with armour on all its specials, so mashing jabs to interrupt it will get you flattened.

Code checks: clean. Critic: pass.

**Final (passed):** The Elder Oni shrugs off every blow with armour on all its specials, so mashing jabs to interrupt it will get you flattened.

## Elder intro card: Elder Kappa

Brief: Write the one-line intro card shown on the map node of the Elder Kappa, before the duel starts.

### Query, retrieved chunk, output

| Query | Retrieved chunk (score) | Output |
|---|---|---|
| **request** in whole vault:<br>One-line intro card for the Elder Kappa that teaches its elder twist before the duel. | **01-vibe/Story.md#Elder intro cards** (25.095)<br>Each elder's twist is shown three ways: an icon on its map node, a one-line intro card, and a short callout the first time the player meets it (Y3). The intro card is one sentence that lets the player work out the twist before the duel starts.<br><br>\| Elder \| Row \| Twist the intro card must teach \|<br>\|---\|---\|---\|<br>\| Nine-Tailed Kitsune \| 5 or 8 \| Reflects any special used twice in a row \|<br>\| Elder Oni \| 5 or 8 \| Armour on every special \|<br>\| Elder Kappa \| 5 or 8 \| Its throws can't be broken \| | The Elder Kappa's grip is unbreakable, so once its throw lands there is no escaping it, and you must avoid being grabbed at all. |
| **request** in whole vault:<br>One-line intro card for the Elder Kappa that teaches its elder twist before the duel. | **01-vibe/Art-and-Audio.md#Elder twists on screen** (19.086)<br>Each elder's twist appears as an icon on its map node, a one-line intro card and a short callout the first time a player meets it. The fight HUD keeps a one-line reminder, for example "Elder twist: specials used twice are reflected". |  |
| **request** in whole vault:<br>One-line intro card for the Elder Kappa that teaches its elder twist before the duel. | **02-mechanics/Yokai.md#Elders** (16.549)<br>Rows 5 and 8 always hold elders. Each elder adds one rule twist that breaks a common habit, so a strong build never wins on autopilot (P5). The twist appears as an icon on the map node, a one-line intro card and a short callout the first time the player meets it (Y3). The row-5 elder pays 50 coins; the row-8 elder pays a rare ability instead. |  |
| **request** in whole vault:<br>One-line intro card for the Elder Kappa that teaches its elder twist before the duel. | **02-mechanics/Yokai.md#Oni** (13.19)<br>The Oni is the rushdown yokai. Its relentless pressure teaches the player to block. It offers Iron-Club Charge, Oni Quake and the Oni modifiers.<br><br>\| \| \|<br>\|---\|---\|<br>\| Aggressive temperament \| Dashes in constantly \|<br>\| Patient temperament \| Walks in and punishes whiffs with its charge \|<br>\| Habit to punish \| Dashes in after blocking a projectile \|<br>\| Elder \| Elder Oni \|<br>\| Elder twist \| Armour on every special \|<br>\| Habit the twist breaks \| Mashing jabs to interrupt it stops working \|<br>\| Elder offers \| The cancel rule Oni Chain \| |  |

### Critic loop

**Round 1 draft:** The Elder Kappa's grip is unbreakable, so once its throw lands there is no escaping it, and you must avoid being grabbed at all.

Code checks: clean. Critic: pass.

**Final (passed):** The Elder Kappa's grip is unbreakable, so once its throw lands there is no escaping it, and you must avoid being grabbed at all.

## Reward card text: Oni's Hide

Brief: Write the reward card text for the modifier Oni's Hide, offered by the Oni.

### Query, retrieved chunk, output

| Query | Retrieved chunk (score) | Output |
|---|---|---|
| **request** in whole vault:<br>Reward card text for the Oni modifier Oni's Hide. | **02-mechanics/Abilities.md#The reward screen** (17.152)<br>Three cards slide up after each win. Each card shows the yokai's crest, the ability's name, its source and rarity, one plain-language line, and a tag: NEW, UPGRADE or MODIFIER. Plain language comes first; frame data sits behind a toggle (A12).<br><br>\| Card in the GDD mockup \| Line under the name \| Plain text \| Tag \|<br>\|---\|---\|---\|---\|<br>\| Foxfire \| Kitsune · common \| "Launch a slow fox flame." \| NEW · Lv 1 \|<br>\| Rising Talisman \| Upgrade · starter \| "Lv 1 → Lv 2: recovers 4 frames faster." \| UPGRADE \|<br>\| Oni's Hide \| Oni · modifier \| "Your uppercut can't be interrupted early on." \| MODIFIER \| | plain: Thick oni skin shrugs off blows: the special it's attached to can't be interrupted during its first 5 frames.<br>frames: Armour on frames 1–5 |
| **request** in whole vault:<br>Reward card text for the Oni modifier Oni's Hide. | **02-mechanics/Abilities.md#Oni modifiers** (11.229)<br>A modifier attaches to one special and changes how it plays without new animation (A10). The Oni offers these five.<br><br>\| Modifier \| Effect on the special it is attached to \| Locked \|<br>\|---\|---\|---\|<br>\| Oni's Hide \| Armour on frames 1–5 \| no \|<br>\| Iron Will \| Can't be interrupted by projectiles \| no \|<br>\| Rage Ember \| +15% damage while Ryo is below 30% health \| ◆ \|<br>\| Club Momentum \| Pushes the opponent 50% further toward the corner \| ◆ \|<br>\| Brute Recovery \| 4 fewer recovery frames on block \| ◆ \| |  |
| **request** in whole vault:<br>Reward card text for the Oni modifier Oni's Hide. | **02-mechanics/Abilities.md#Balance-gate cuts** (9.983)<br>If the balance gate cuts modifiers from 14 to 8, the six unlocked ones stay (Will-o'-wisp, Fox's Patience, Oni's Hide, Iron Will, Slippery Skin, River Pull) and the locked ones drop to 2. |  |
| **request** in whole vault:<br>Reward card text for the Oni modifier Oni's Hide. | **01-vibe/Tone.md#Reward card voice** (8.901)<br>Reward cards are plain language first, with frame data behind a toggle (A12).<br><br>- `plain` is one sentence, at most 140 characters, that says what changes when the player fights. It may open with a short folklore image, then states the effect in ordinary words.<br>- `frames` is the exact number from the ability table, with no flavour.<br>- A plain line never promises more than the table gives.<br><br>\| Ability \| plain \| frames \|<br>\|---\|---\|---\|<br>\| Foxfire \| "Launch a slow fox flame that drifts across the screen." \| "15 / 4 / 30 frames, 70 damage, slow projectile" \|<br>\| Will-o'-wisp \| "Wisp-light lends your projectile wings: it flies faster across the screen." \| "Projectile speed +30% (x1.3, rounded to nearest)" \|<br>\| Fox's Patience \| "The fox waits and learns: this special builds more spirit meter when the foe blocks it." \| "Meter gain on block +25% (C5 default 3 becomes 4, rounded to nearest)" \|<br>\| Oni's Hide (GDD mockup, attached to Rising Talisman) \| "Your uppercut can't be interrupted early on." \| not written yet \| |  |

### Critic loop

**Round 1 draft:** plain: Thick oni skin shrugs off blows: the special it's attached to can't be interrupted during its first 5 frames.<br>frames: Armour on frames 1–5

Code checks: clean. Critic: pass.

**Final (passed):** plain: Thick oni skin shrugs off blows: the special it's attached to can't be interrupted during its first 5 frames.<br>frames: Armour on frames 1–5

## Reward card text: Iron Will

Brief: Write the reward card text for the modifier Iron Will, offered by the Oni.

### Query, retrieved chunk, output

| Query | Retrieved chunk (score) | Output |
|---|---|---|
| **request** in whole vault:<br>Reward card text for the Oni modifier Iron Will. | **02-mechanics/Abilities.md#The reward screen** (12.543)<br>Three cards slide up after each win. Each card shows the yokai's crest, the ability's name, its source and rarity, one plain-language line, and a tag: NEW, UPGRADE or MODIFIER. Plain language comes first; frame data sits behind a toggle (A12).<br><br>\| Card in the GDD mockup \| Line under the name \| Plain text \| Tag \|<br>\|---\|---\|---\|---\|<br>\| Foxfire \| Kitsune · common \| "Launch a slow fox flame." \| NEW · Lv 1 \|<br>\| Rising Talisman \| Upgrade · starter \| "Lv 1 → Lv 2: recovers 4 frames faster." \| UPGRADE \|<br>\| Oni's Hide \| Oni · modifier \| "Your uppercut can't be interrupted early on." \| MODIFIER \| | plain: Like an oni handed an iron club, your special can't be interrupted by projectiles.<br>frames: MODIFIER (Oni · Iron Will): the attached special can't be interrupted by projectiles. |
| **request** in whole vault:<br>Reward card text for the Oni modifier Iron Will. | **02-mechanics/Abilities.md#Oni modifiers** (7.913)<br>A modifier attaches to one special and changes how it plays without new animation (A10). The Oni offers these five.<br><br>\| Modifier \| Effect on the special it is attached to \| Locked \|<br>\|---\|---\|---\|<br>\| Oni's Hide \| Armour on frames 1–5 \| no \|<br>\| Iron Will \| Can't be interrupted by projectiles \| no \|<br>\| Rage Ember \| +15% damage while Ryo is below 30% health \| ◆ \|<br>\| Club Momentum \| Pushes the opponent 50% further toward the corner \| ◆ \|<br>\| Brute Recovery \| 4 fewer recovery frames on block \| ◆ \| |  |
| **request** in whole vault:<br>Reward card text for the Oni modifier Iron Will. | **01-vibe/Characters.md#Oni** (7.638)<br>The rushdown yokai of the snowbound mountain shrine gate. Proud and blunt, it dashes in and swings an iron club. Its hit sparks are oni red.<br><br>Folklore reference: oni carry the kanabō, an iron club; the saying "an oni with an iron club" means strength made stronger.<br><br>Reference images: none yet. |  |
| **request** in whole vault:<br>Reward card text for the Oni modifier Iron Will. | **02-mechanics/Yokai.md#Oni** (7.351)<br>The Oni is the rushdown yokai. Its relentless pressure teaches the player to block. It offers Iron-Club Charge, Oni Quake and the Oni modifiers.<br><br>\| \| \|<br>\|---\|---\|<br>\| Aggressive temperament \| Dashes in constantly \|<br>\| Patient temperament \| Walks in and punishes whiffs with its charge \|<br>\| Habit to punish \| Dashes in after blocking a projectile \|<br>\| Elder \| Elder Oni \|<br>\| Elder twist \| Armour on every special \|<br>\| Habit the twist breaks \| Mashing jabs to interrupt it stops working \|<br>\| Elder offers \| The cancel rule Oni Chain \| |  |

### Critic loop

**Round 1 draft:** plain: Your special can't be interrupted by projectiles. Strength made stronger, like an oni with an iron club.<br>frames: MODIFIER (Oni · Iron Will): the attached special can't be interrupted by projectiles.

| Caught by | Kind | Quote | Problem | Evidence | Fix |
|---|---|---|---|---|---|
| code | form | Your special can't be interrupted by projectiles. Strength made stronger, like an oni with an iron club. | 'plain' must be exactly one sentence. |  | Make it one sentence. |
| critic | tone_drift | Your special can't be interrupted by projectiles. Strength made stronger, like an oni with an iron club. | The plain line is two sentences, and the folklore image trails the effect instead of opening the line. | 01-vibe/Tone.md#Reward card voice: `plain` is one sentence, at most 140 characters, that says what changes when the player fights. It may open with a short folklore image, then states the effect in ordinary words. | Merge into one sentence that opens with the image and ends on the effect, e.g. "Like an oni handed an iron club, your special can't be interrupted by projectiles." |

**Round 2 draft:** plain: Like an oni handed an iron club, your special can't be interrupted by projectiles.<br>frames: MODIFIER (Oni · Iron Will): the attached special can't be interrupted by projectiles.

Code checks: clean. Critic: pass.

**Final (passed):** plain: Like an oni handed an iron club, your special can't be interrupted by projectiles.<br>frames: MODIFIER (Oni · Iron Will): the attached special can't be interrupted by projectiles.

## Reward card text: Slippery Skin

Brief: Write the reward card text for the modifier Slippery Skin, offered by the Kappa.

### Query, retrieved chunk, output

| Query | Retrieved chunk (score) | Output |
|---|---|---|
| **request** in whole vault:<br>Reward card text for the Kappa modifier Slippery Skin. | **02-mechanics/Abilities.md#Kappa modifiers** (12.935)<br>A modifier attaches to one special and changes how it plays without new animation (A10). The Kappa offers these four.<br><br>\| Modifier \| Effect on the special it is attached to \| Locked \|<br>\|---\|---\|---\|<br>\| Slippery Skin \| Throw-invulnerable during startup \| no \|<br>\| River Pull \| Pulls the opponent one step closer on hit \| no \|<br>\| Cucumber Gift \| Heals 2% of max health on hit \| ◆ \|<br>\| Sumo Stance \| Hold the button to charge for +20% damage \| ◆ \| | plain: Your chosen special can't be grabbed or thrown while it's starting up.<br>frames: Attached special: throw-invulnerable during startup frames. Kappa modifier. Unlocked from the start. |
| **request** in whole vault:<br>Reward card text for the Kappa modifier Slippery Skin. | **02-mechanics/Abilities.md#Balance-gate cuts** (10.892)<br>If the balance gate cuts modifiers from 14 to 8, the six unlocked ones stay (Will-o'-wisp, Fox's Patience, Oni's Hide, Iron Will, Slippery Skin, River Pull) and the locked ones drop to 2. |  |
| **request** in whole vault:<br>Reward card text for the Kappa modifier Slippery Skin. | **02-mechanics/Abilities.md#The reward screen** (10.408)<br>Three cards slide up after each win. Each card shows the yokai's crest, the ability's name, its source and rarity, one plain-language line, and a tag: NEW, UPGRADE or MODIFIER. Plain language comes first; frame data sits behind a toggle (A12).<br><br>\| Card in the GDD mockup \| Line under the name \| Plain text \| Tag \|<br>\|---\|---\|---\|---\|<br>\| Foxfire \| Kitsune · common \| "Launch a slow fox flame." \| NEW · Lv 1 \|<br>\| Rising Talisman \| Upgrade · starter \| "Lv 1 → Lv 2: recovers 4 frames faster." \| UPGRADE \|<br>\| Oni's Hide \| Oni · modifier \| "Your uppercut can't be interrupted early on." \| MODIFIER \| |  |
| **request** in whole vault:<br>Reward card text for the Kappa modifier Slippery Skin. | **02-mechanics/Run-Loop.md#Winning and losing** (5.693)<br>\| Outcome \| What happens \|<br>\|---\|---\|<br>\| Win a duel \| The yokai is bound, 50 health is restored, three reward cards are offered \|<br>\| Health reaches 0 \| The run ends and the moveset is lost, apart from the carry-over \|<br>\| Beat the Tanuki \| The run is won and a win story card shows \|<br><br>A first-time player should finish a run in 25–40 minutes (R9). |  |

### Critic loop

**Round 1 draft:** plain: Your chosen special can't be grabbed or thrown while it's starting up.<br>frames: Attached special: throw-invulnerable during startup frames. Kappa modifier. Unlocked from the start.

Code checks: clean. Critic: pass.

**Final (passed):** plain: Your chosen special can't be grabbed or thrown while it's starting up.<br>frames: Attached special: throw-invulnerable during startup frames. Kappa modifier. Unlocked from the start.

## Reward card text: River Pull

Brief: Write the reward card text for the modifier River Pull, offered by the Kappa.

### Query, retrieved chunk, output

| Query | Retrieved chunk (score) | Output |
|---|---|---|
| **request** in whole vault:<br>Reward card text for the Kappa modifier River Pull. | **02-mechanics/Abilities.md#Kappa modifiers** (13.281)<br>A modifier attaches to one special and changes how it plays without new animation (A10). The Kappa offers these four.<br><br>\| Modifier \| Effect on the special it is attached to \| Locked \|<br>\|---\|---\|---\|<br>\| Slippery Skin \| Throw-invulnerable during startup \| no \|<br>\| River Pull \| Pulls the opponent one step closer on hit \| no \|<br>\| Cucumber Gift \| Heals 2% of max health on hit \| ◆ \|<br>\| Sumo Stance \| Hold the button to charge for +20% damage \| ◆ \| | plain: Your special pulls the opponent one step closer when it hits.<br>frames: Modifier on one special: on hit, pulls opponent one step closer. Kappa. Unlocked. |
| **request** in whole vault:<br>Reward card text for the Kappa modifier River Pull. | **02-mechanics/Abilities.md#The reward screen** (10.408)<br>Three cards slide up after each win. Each card shows the yokai's crest, the ability's name, its source and rarity, one plain-language line, and a tag: NEW, UPGRADE or MODIFIER. Plain language comes first; frame data sits behind a toggle (A12).<br><br>\| Card in the GDD mockup \| Line under the name \| Plain text \| Tag \|<br>\|---\|---\|---\|---\|<br>\| Foxfire \| Kitsune · common \| "Launch a slow fox flame." \| NEW · Lv 1 \|<br>\| Rising Talisman \| Upgrade · starter \| "Lv 1 → Lv 2: recovers 4 frames faster." \| UPGRADE \|<br>\| Oni's Hide \| Oni · modifier \| "Your uppercut can't be interrupted early on." \| MODIFIER \| |  |
| **request** in whole vault:<br>Reward card text for the Kappa modifier River Pull. | **02-mechanics/Abilities.md#Balance-gate cuts** (9.509)<br>If the balance gate cuts modifiers from 14 to 8, the six unlocked ones stay (Will-o'-wisp, Fox's Patience, Oni's Hide, Iron Will, Slippery Skin, River Pull) and the locked ones drop to 2. |  |
| **request** in whole vault:<br>Reward card text for the Kappa modifier River Pull. | **02-mechanics/Yokai.md#Kappa** (7.199)<br>The Kappa is the grappler. It offers River Grab, Shell Spin and the Kappa modifiers.<br><br>\| \| \|<br>\|---\|---\|<br>\| Aggressive temperament \| Walks straight into throw range \|<br>\| Patient temperament \| Blocks, then throws after two blocked hits \|<br>\| Habit to punish \| Throws after blocking two hits in a row \|<br>\| Elder \| Elder Kappa \|<br>\| Elder twist \| Its throws can't be broken \|<br>\| Habit the twist breaks \| Relying on the throw break, so the player has to stay out of throw range \|<br>\| Elder offers \| The cancel rule Undertow \| |  |

### Critic loop

**Round 1 draft:** plain: Your special pulls the opponent one step closer when it hits.<br>frames: Modifier on one special: on hit, pulls opponent one step closer. Kappa. Unlocked.

Code checks: clean. Critic: pass.

**Final (passed):** plain: Your special pulls the opponent one step closer when it hits.<br>frames: Modifier on one special: on hit, pulls opponent one step closer. Kappa. Unlocked.
