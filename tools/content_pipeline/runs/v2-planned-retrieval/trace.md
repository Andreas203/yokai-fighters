# Trace: v2-planned-retrieval

Command: `generate`. Retrieval: v2. Generator: sonnet. Critic: opus.
Vault: 83 chunks from 13 notes.

| Piece | Rounds | Issues caught | Result |
|---|---|---|---|
| Wake-up card: Oni wins at row 2 or 3 | 1 | 0 | passed first time |
| Wake-up card: Nine-Tailed Kitsune wins at row 5 | 1 | 0 | passed first time |
| Wake-up card: Kappa wins at row 6 | 1 | 0 | passed first time |
| Elder intro card: Nine-Tailed Kitsune | 2 | 1 | corrected, then passed |
| Elder intro card: Elder Oni | 1 | 0 | passed first time |
| Elder intro card: Elder Kappa | 1 | 0 | passed first time |
| Reward card text: Oni's Hide | 1 | 0 | passed first time |
| Reward card text: Iron Will | 1 | 0 | passed first time |
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
| **situation** in 01-vibe/Story.md:<br>wake-up card after a loss, Ryo wakes at the province's edge | **01-vibe/Story.md#Wake-up cards** (26.545)<br>A wake-up card shows after a loss. Ryo wakes at the province's edge.<br><br>- First loss: a cheerful voice names the move the Tanuki would have stolen. This teaches the copy rule before the player has met the boss.<br>- Later losses vary by the row reached and by which yokai won. The yokai that won is named through `{yokai}`.<br>- A wake-up card after a run that ended at row 5 or earlier says the talismans are blank. A wake-up card after a run that reached row 6 or further may say one talisman stays faintly inked.<br>- The yokai that won does not gloat and does not harm Ryo. It leaves, or goes back to what it was doing.<br>- A later-loss card does not reveal that the merchant is the Tanuki. The reveal belongs to the boss node. | You wake at the province's edge, every talisman blank as new paper. Far off, {yokai} trudges back to its snowy shrine gate, as if the duel were a chore finished and nothing more. |
| **situation** in 01-vibe/Story.md:<br>wake-up card after a loss, Ryo wakes at the province's edge | **01-vibe/Story.md#The illusion loop** (15.415)<br>The Tanuki has folded a rural Japanese province, at the end of autumn, into an illusion. Every run is one pass through that illusion. The yokai inside it are real spirits, trapped and made to fight again each loop. When Ryo falls, the loop closes and he wakes at the province's edge to walk in again. Each Tanuki win, up to three, makes the yokai react faster and lean aggressive, framed as the illusion growing stronger. |  |
| **talismans** in 01-vibe/Story.md:<br>talismans after a loss: run ended at row 5 or earlier, every talisman blank | **01-vibe/Story.md#Talismans** (26.281)<br>- Binding: the round-ending blow slows, then an ink stroke seals the yokai into a paper talisman.<br>- After a loss, Ryo's talismans are blank again.<br>- The one exception is the carry-over: if the run reached row 6 or further, one talisman stays faintly inked, and that special starts the next run at Lv 2. A run that ended at row 5 or earlier leaves every talisman blank.<br>- Abilities bought from the merchant appear in grey "borrowed ink" and never carry over. |  |
| **yokai** in 01-vibe/Characters.md:<br>Oni personality stage folklore iron club | **01-vibe/Characters.md#Oni** (15.058)<br>The rushdown yokai of the snowbound mountain shrine gate. Proud and blunt, it dashes in and swings an iron club. Its hit sparks are oni red.<br><br>Folklore reference: oni carry the kanabō, an iron club; the saying "an oni with an iron club" means strength made stronger.<br><br>Reference images: none yet. |  |
| **voice** in 01-vibe/Tone.md:<br>narrator voice wake-up card present tense | **01-vibe/Tone.md#Voice: the narrator** (26.789)<br>The narrator is quiet and observant, in present tense. In the GDD's sample cards the wake-up card addresses the player as "you" ("You wake at the province's edge.") and the win card names Ryo ("Ryo has no answer yet."). The narrator describes what is seen and lets the cost stay unspoken or half spoken. |  |
| always included | **01-vibe/Tone.md#The tone line** (pinned)<br>Yokai Fighters is mischievous folklore with a melancholy edge, never horror (S1). Yokai are tricksters and lonely spirits, not monsters, and every victory carries a small cost. The setting is a rural Japanese province at the end of autumn, so the mood is dusk, lantern light and things about to end, not night and dread.<br><br>Three checks for any line of text:<br><br>- Mischief: someone in the line is teasing, bargaining or getting away with something.<br>- Melancholy: something is owed, borrowed, waiting or left behind.<br>- No horror: nothing is gory, cruel, demonic or frightening. A yokai that wins walks away; it does not hurt Ryo for pleasure. | |
| always included | **01-vibe/Tone.md#Card form** (pinned)<br>- A story card is one or two sentences (S2), at most 280 characters.<br>- Present tense, plain words, one concrete image per card: a lantern, a paper screen, a tail, a teacup, an ink stroke.<br>- Names come from the player's run through `{yokai}` and `{move}` placeholders (S4). A card never hard-codes a move the player may not own.<br>- Images come from the game's own world: paper talismans, ink, brush strokes, red seals, lanterns, bamboo, snow on a shrine gate, a riverbank, a tea house with paper screens.<br>- A card does not explain a rule in system words. It does not say "health", "frames", "level" or "roguelite"; it says what a person standing there would see. | |
| always included | **01-vibe/Tone.md#Do and don't examples** (pinned)<br>\| Do \| Don't \| Why \|<br>\|---\|---\|---\|<br>\| "Kitsune slips off between the lanterns, tails swaying, and does not look back." \| "The fox demon sinks its fangs into Ryo and howls over his broken body." \| Yokai are tricksters, not monsters. A loss is a yokai leaving, not a killing. \|<br>\| "Somewhere a bound thing is still waiting for someone to return it." \| "Ryo has failed, and the darkness swallows the province forever." \| Melancholy is small and specific: a debt, a wait. It is never doom. \|<br>\| "Shame about that Foxfire. I'd have taken good care of it." \| "Your Foxfire is MINE now, fool! Tremble before me!" \| The Tanuki is cheerful and covetous. It never gloats or threatens. \|<br>\| "Forgive me, Kitsune. I'll set your spirit free." \| "Got you! Another one for the collection." \| Ryo apologises. Collecting is what the Tanuki does. \|<br>\| "Which of us is the collector, little exorcist?" \| "LOL, nice talismans, bro." \| Wit is dry and in period. No modern slang, no memes, no fourth wall. \| | |

### Critic loop

**Round 1 draft:** You wake at the province's edge, every talisman blank as new paper. Far off, {yokai} trudges back to its snowy shrine gate, as if the duel were a chore finished and nothing more.

Code checks: clean. Critic: pass.

**Final (passed):** You wake at the province's edge, every talisman blank as new paper. Far off, {yokai} trudges back to its snowy shrine gate, as if the duel were a chore finished and nothing more.

## Wake-up card: Nine-Tailed Kitsune wins at row 5

Brief: Write the wake-up story card shown after Ryo loses a duel to the Nine-Tailed Kitsune at row 5. The run ended at row 5 or earlier.

### Query, retrieved chunk, output

| Query | Retrieved chunk (score) | Output |
|---|---|---|
| **situation** in 01-vibe/Story.md:<br>wake-up card after a loss, Ryo wakes at the province's edge | **01-vibe/Story.md#Wake-up cards** (26.545)<br>A wake-up card shows after a loss. Ryo wakes at the province's edge.<br><br>- First loss: a cheerful voice names the move the Tanuki would have stolen. This teaches the copy rule before the player has met the boss.<br>- Later losses vary by the row reached and by which yokai won. The yokai that won is named through `{yokai}`.<br>- A wake-up card after a run that ended at row 5 or earlier says the talismans are blank. A wake-up card after a run that reached row 6 or further may say one talisman stays faintly inked.<br>- The yokai that won does not gloat and does not harm Ryo. It leaves, or goes back to what it was doing.<br>- A later-loss card does not reveal that the merchant is the Tanuki. The reveal belongs to the boss node. | You wake at the province's edge, and every talisman is blank paper again. {yokai} has already slipped back between the lanterns, tails swaying, as if the duel were only a passing thought. |
| **situation** in 01-vibe/Story.md:<br>wake-up card after a loss, Ryo wakes at the province's edge | **01-vibe/Story.md#The illusion loop** (15.415)<br>The Tanuki has folded a rural Japanese province, at the end of autumn, into an illusion. Every run is one pass through that illusion. The yokai inside it are real spirits, trapped and made to fight again each loop. When Ryo falls, the loop closes and he wakes at the province's edge to walk in again. Each Tanuki win, up to three, makes the yokai react faster and lean aggressive, framed as the illusion growing stronger. |  |
| **talismans** in 01-vibe/Story.md:<br>talismans after a loss: run ended at row 5 or earlier, every talisman blank | **01-vibe/Story.md#Talismans** (26.281)<br>- Binding: the round-ending blow slows, then an ink stroke seals the yokai into a paper talisman.<br>- After a loss, Ryo's talismans are blank again.<br>- The one exception is the carry-over: if the run reached row 6 or further, one talisman stays faintly inked, and that special starts the next run at Lv 2. A run that ended at row 5 or earlier leaves every talisman blank.<br>- Abilities bought from the merchant appear in grey "borrowed ink" and never carry over. |  |
| **yokai** in 01-vibe/Characters.md:<br>Kitsune fox nine tails foxfire personality stage | **01-vibe/Characters.md#Kitsune** (17.276)<br>The fox of the bamboo grove at dusk, a zoner who holds the screen with slow foxfire. She teases and slips away. She wears a full white fox mask with red markings for the whole fight, and has three tails; the Nine-Tailed elder has nine. Her hit sparks are foxfire orange.<br><br>Folklore reference: kitsune are fox spirits that grow more tails with age, up to nine, and kitsunebi (foxfire) is the ghost light they carry.<br><br>Reference images: `docs/design/characters/kitsune/`. |  |
| **voice** in 01-vibe/Tone.md:<br>narrator voice wake-up card present tense | **01-vibe/Tone.md#Voice: the narrator** (26.789)<br>The narrator is quiet and observant, in present tense. In the GDD's sample cards the wake-up card addresses the player as "you" ("You wake at the province's edge.") and the win card names Ryo ("Ryo has no answer yet."). The narrator describes what is seen and lets the cost stay unspoken or half spoken. |  |
| always included | **01-vibe/Tone.md#The tone line** (pinned)<br>Yokai Fighters is mischievous folklore with a melancholy edge, never horror (S1). Yokai are tricksters and lonely spirits, not monsters, and every victory carries a small cost. The setting is a rural Japanese province at the end of autumn, so the mood is dusk, lantern light and things about to end, not night and dread.<br><br>Three checks for any line of text:<br><br>- Mischief: someone in the line is teasing, bargaining or getting away with something.<br>- Melancholy: something is owed, borrowed, waiting or left behind.<br>- No horror: nothing is gory, cruel, demonic or frightening. A yokai that wins walks away; it does not hurt Ryo for pleasure. | |
| always included | **01-vibe/Tone.md#Card form** (pinned)<br>- A story card is one or two sentences (S2), at most 280 characters.<br>- Present tense, plain words, one concrete image per card: a lantern, a paper screen, a tail, a teacup, an ink stroke.<br>- Names come from the player's run through `{yokai}` and `{move}` placeholders (S4). A card never hard-codes a move the player may not own.<br>- Images come from the game's own world: paper talismans, ink, brush strokes, red seals, lanterns, bamboo, snow on a shrine gate, a riverbank, a tea house with paper screens.<br>- A card does not explain a rule in system words. It does not say "health", "frames", "level" or "roguelite"; it says what a person standing there would see. | |
| always included | **01-vibe/Tone.md#Do and don't examples** (pinned)<br>\| Do \| Don't \| Why \|<br>\|---\|---\|---\|<br>\| "Kitsune slips off between the lanterns, tails swaying, and does not look back." \| "The fox demon sinks its fangs into Ryo and howls over his broken body." \| Yokai are tricksters, not monsters. A loss is a yokai leaving, not a killing. \|<br>\| "Somewhere a bound thing is still waiting for someone to return it." \| "Ryo has failed, and the darkness swallows the province forever." \| Melancholy is small and specific: a debt, a wait. It is never doom. \|<br>\| "Shame about that Foxfire. I'd have taken good care of it." \| "Your Foxfire is MINE now, fool! Tremble before me!" \| The Tanuki is cheerful and covetous. It never gloats or threatens. \|<br>\| "Forgive me, Kitsune. I'll set your spirit free." \| "Got you! Another one for the collection." \| Ryo apologises. Collecting is what the Tanuki does. \|<br>\| "Which of us is the collector, little exorcist?" \| "LOL, nice talismans, bro." \| Wit is dry and in period. No modern slang, no memes, no fourth wall. \| | |

### Critic loop

**Round 1 draft:** You wake at the province's edge, and every talisman is blank paper again. {yokai} has already slipped back between the lanterns, tails swaying, as if the duel were only a passing thought.

Code checks: clean. Critic: pass.

**Final (passed):** You wake at the province's edge, and every talisman is blank paper again. {yokai} has already slipped back between the lanterns, tails swaying, as if the duel were only a passing thought.

## Wake-up card: Kappa wins at row 6

Brief: Write the wake-up story card shown after Ryo loses a duel to the Kappa at row 6. The run reached row 6.

### Query, retrieved chunk, output

| Query | Retrieved chunk (score) | Output |
|---|---|---|
| **situation** in 01-vibe/Story.md:<br>wake-up card after a loss, Ryo wakes at the province's edge | **01-vibe/Story.md#Wake-up cards** (26.545)<br>A wake-up card shows after a loss. Ryo wakes at the province's edge.<br><br>- First loss: a cheerful voice names the move the Tanuki would have stolen. This teaches the copy rule before the player has met the boss.<br>- Later losses vary by the row reached and by which yokai won. The yokai that won is named through `{yokai}`.<br>- A wake-up card after a run that ended at row 5 or earlier says the talismans are blank. A wake-up card after a run that reached row 6 or further may say one talisman stays faintly inked.<br>- The yokai that won does not gloat and does not harm Ryo. It leaves, or goes back to what it was doing.<br>- A later-loss card does not reveal that the merchant is the Tanuki. The reveal belongs to the boss node. | You wake at the province's edge. {yokai} has already turned back toward the lantern river, bowing to no one, and one talisman in your sleeve stays faintly inked. |
| **situation** in 01-vibe/Story.md:<br>wake-up card after a loss, Ryo wakes at the province's edge | **01-vibe/Story.md#The illusion loop** (15.415)<br>The Tanuki has folded a rural Japanese province, at the end of autumn, into an illusion. Every run is one pass through that illusion. The yokai inside it are real spirits, trapped and made to fight again each loop. When Ryo falls, the loop closes and he wakes at the province's edge to walk in again. Each Tanuki win, up to three, makes the yokai react faster and lean aggressive, framed as the illusion growing stronger. |  |
| **talismans** in 01-vibe/Story.md:<br>talismans after a loss: run reached row 6, one talisman stays faintly inked carry-over | **01-vibe/Story.md#Talismans** (32.894)<br>- Binding: the round-ending blow slows, then an ink stroke seals the yokai into a paper talisman.<br>- After a loss, Ryo's talismans are blank again.<br>- The one exception is the carry-over: if the run reached row 6 or further, one talisman stays faintly inked, and that special starts the next run at Lv 2. A run that ended at row 5 or earlier leaves every talisman blank.<br>- Abilities bought from the merchant appear in grey "borrowed ink" and never carry over. |  |
| **yokai** in 01-vibe/Characters.md:<br>Kappa personality stage folklore river | **01-vibe/Characters.md#Kappa** (8.903)<br>The grappler of the lantern riverbank. Formal, stubborn and polite, it walks straight into throw range. Its hit sparks are kappa teal.<br><br>Folklore reference: kappa are river spirits that love sumo and cucumbers and carry water in a dish on their heads; bow to one and it bows back, spilling the water.<br><br>Reference images: none yet. |  |
| **voice** in 01-vibe/Tone.md:<br>narrator voice wake-up card present tense | **01-vibe/Tone.md#Voice: the narrator** (26.789)<br>The narrator is quiet and observant, in present tense. In the GDD's sample cards the wake-up card addresses the player as "you" ("You wake at the province's edge.") and the win card names Ryo ("Ryo has no answer yet."). The narrator describes what is seen and lets the cost stay unspoken or half spoken. |  |
| always included | **01-vibe/Tone.md#The tone line** (pinned)<br>Yokai Fighters is mischievous folklore with a melancholy edge, never horror (S1). Yokai are tricksters and lonely spirits, not monsters, and every victory carries a small cost. The setting is a rural Japanese province at the end of autumn, so the mood is dusk, lantern light and things about to end, not night and dread.<br><br>Three checks for any line of text:<br><br>- Mischief: someone in the line is teasing, bargaining or getting away with something.<br>- Melancholy: something is owed, borrowed, waiting or left behind.<br>- No horror: nothing is gory, cruel, demonic or frightening. A yokai that wins walks away; it does not hurt Ryo for pleasure. | |
| always included | **01-vibe/Tone.md#Card form** (pinned)<br>- A story card is one or two sentences (S2), at most 280 characters.<br>- Present tense, plain words, one concrete image per card: a lantern, a paper screen, a tail, a teacup, an ink stroke.<br>- Names come from the player's run through `{yokai}` and `{move}` placeholders (S4). A card never hard-codes a move the player may not own.<br>- Images come from the game's own world: paper talismans, ink, brush strokes, red seals, lanterns, bamboo, snow on a shrine gate, a riverbank, a tea house with paper screens.<br>- A card does not explain a rule in system words. It does not say "health", "frames", "level" or "roguelite"; it says what a person standing there would see. | |
| always included | **01-vibe/Tone.md#Do and don't examples** (pinned)<br>\| Do \| Don't \| Why \|<br>\|---\|---\|---\|<br>\| "Kitsune slips off between the lanterns, tails swaying, and does not look back." \| "The fox demon sinks its fangs into Ryo and howls over his broken body." \| Yokai are tricksters, not monsters. A loss is a yokai leaving, not a killing. \|<br>\| "Somewhere a bound thing is still waiting for someone to return it." \| "Ryo has failed, and the darkness swallows the province forever." \| Melancholy is small and specific: a debt, a wait. It is never doom. \|<br>\| "Shame about that Foxfire. I'd have taken good care of it." \| "Your Foxfire is MINE now, fool! Tremble before me!" \| The Tanuki is cheerful and covetous. It never gloats or threatens. \|<br>\| "Forgive me, Kitsune. I'll set your spirit free." \| "Got you! Another one for the collection." \| Ryo apologises. Collecting is what the Tanuki does. \|<br>\| "Which of us is the collector, little exorcist?" \| "LOL, nice talismans, bro." \| Wit is dry and in period. No modern slang, no memes, no fourth wall. \| | |

### Critic loop

**Round 1 draft:** You wake at the province's edge. {yokai} has already turned back toward the lantern river, bowing to no one, and one talisman in your sleeve stays faintly inked.

Code checks: clean. Critic: pass.

**Final (passed):** You wake at the province's edge. {yokai} has already turned back toward the lantern river, bowing to no one, and one talisman in your sleeve stays faintly inked.

## Elder intro card: Nine-Tailed Kitsune

Brief: Write the one-line intro card shown on the map node of the Nine-Tailed Kitsune, before the duel starts.

### Query, retrieved chunk, output

| Query | Retrieved chunk (score) | Output |
|---|---|---|
| **twist** in 02-mechanics/Yokai.md:<br>Nine-Tailed Kitsune elder twist and the habit the twist breaks | **02-mechanics/Yokai.md#Kitsune** (23.601)<br>The Kitsune is the zoner. It offers Foxfire, Fox Mirage and the Kitsune modifiers.<br><br>\| \| \|<br>\|---\|---\|<br>\| Aggressive temperament \| Throws foxfire at mid range while advancing \|<br>\| Patient temperament \| Holds full screen and anti-airs jumps \|<br>\| Habit to punish \| Often jumps after a knockdown \|<br>\| Elder \| Nine-Tailed Kitsune \|<br>\| Elder twist \| Reflects any special used twice in a row \|<br>\| Habit the twist breaks \| Repeating one special, so the player learns to mix specials \|<br>\| Elder offers \| The cancel rule Fox Step \| | Nine tails sway in the dusk; show her the same trick twice running and she hands it back to you as foxfire. |
| **card** in 01-vibe/Story.md:<br>elder intro card: one line that teaches the twist | **01-vibe/Story.md#Elder intro cards** (16.062)<br>Each elder's twist is shown three ways: an icon on its map node, a one-line intro card, and a short callout the first time the player meets it (Y3). The intro card is one sentence that lets the player work out the twist before the duel starts.<br><br>\| Elder \| Row \| Twist the intro card must teach \|<br>\|---\|---\|---\|<br>\| Nine-Tailed Kitsune \| 5 or 8 \| Reflects any special used twice in a row \|<br>\| Elder Oni \| 5 or 8 \| Armour on every special \|<br>\| Elder Kappa \| 5 or 8 \| Its throws can't be broken \| |  |
| **yokai** in 01-vibe/Characters.md:<br>Kitsune personality folklore stage | **01-vibe/Characters.md#Kitsune** (5.157)<br>The fox of the bamboo grove at dusk, a zoner who holds the screen with slow foxfire. She teases and slips away. She wears a full white fox mask with red markings for the whole fight, and has three tails; the Nine-Tailed elder has nine. Her hit sparks are foxfire orange.<br><br>Folklore reference: kitsune are fox spirits that grow more tails with age, up to nine, and kitsunebi (foxfire) is the ghost light they carry.<br><br>Reference images: `docs/design/characters/kitsune/`. |  |
| **voice** in 01-vibe/Tone.md:<br>narrator voice quiet observant | **01-vibe/Tone.md#Voice: the narrator** (18.509)<br>The narrator is quiet and observant, in present tense. In the GDD's sample cards the wake-up card addresses the player as "you" ("You wake at the province's edge.") and the win card names Ryo ("Ryo has no answer yet."). The narrator describes what is seen and lets the cost stay unspoken or half spoken. |  |
| always included | **01-vibe/Tone.md#The tone line** (pinned)<br>Yokai Fighters is mischievous folklore with a melancholy edge, never horror (S1). Yokai are tricksters and lonely spirits, not monsters, and every victory carries a small cost. The setting is a rural Japanese province at the end of autumn, so the mood is dusk, lantern light and things about to end, not night and dread.<br><br>Three checks for any line of text:<br><br>- Mischief: someone in the line is teasing, bargaining or getting away with something.<br>- Melancholy: something is owed, borrowed, waiting or left behind.<br>- No horror: nothing is gory, cruel, demonic or frightening. A yokai that wins walks away; it does not hurt Ryo for pleasure. | |
| always included | **01-vibe/Tone.md#Card form** (pinned)<br>- A story card is one or two sentences (S2), at most 280 characters.<br>- Present tense, plain words, one concrete image per card: a lantern, a paper screen, a tail, a teacup, an ink stroke.<br>- Names come from the player's run through `{yokai}` and `{move}` placeholders (S4). A card never hard-codes a move the player may not own.<br>- Images come from the game's own world: paper talismans, ink, brush strokes, red seals, lanterns, bamboo, snow on a shrine gate, a riverbank, a tea house with paper screens.<br>- A card does not explain a rule in system words. It does not say "health", "frames", "level" or "roguelite"; it says what a person standing there would see. | |

### Critic loop

**Round 1 draft:** Nine tails sway in the dusk, and any special you use twice in a row comes drifting back to you as foxfire.

| Caught by | Kind | Quote | Problem | Evidence | Fix |
|---|---|---|---|---|---|
| critic | tone_drift | any special you use twice in a row | The card states the twist in rulebook terms, using the mechanics word "special" and near-verbatim rule text instead of describing what a person standing there would see. | 01-vibe/Tone.md#Card form: A card does not explain a rule in system words. It does not say "health", "frames", "level" or "roguelite"; it says what a person standing there would see. | Keep the twice-in-a-row cue but phrase it in-world, e.g. "Nine tails sway in the dusk; show her the same trick twice running and she hands it back to you." |

**Round 2 draft:** Nine tails sway in the dusk; show her the same trick twice running and she hands it back to you as foxfire.

Code checks: clean. Critic: pass.

**Final (passed):** Nine tails sway in the dusk; show her the same trick twice running and she hands it back to you as foxfire.

## Elder intro card: Elder Oni

Brief: Write the one-line intro card shown on the map node of the Elder Oni, before the duel starts.

### Query, retrieved chunk, output

| Query | Retrieved chunk (score) | Output |
|---|---|---|
| **twist** in 02-mechanics/Yokai.md:<br>Elder Oni elder twist and the habit the twist breaks | **02-mechanics/Yokai.md#Oni** (22.647)<br>The Oni is the rushdown yokai. Its relentless pressure teaches the player to block. It offers Iron-Club Charge, Oni Quake and the Oni modifiers.<br><br>\| \| \|<br>\|---\|---\|<br>\| Aggressive temperament \| Dashes in constantly \|<br>\| Patient temperament \| Walks in and punishes whiffs with its charge \|<br>\| Habit to punish \| Dashes in after blocking a projectile \|<br>\| Elder \| Elder Oni \|<br>\| Elder twist \| Armour on every special \|<br>\| Habit the twist breaks \| Mashing jabs to interrupt it stops working \|<br>\| Elder offers \| The cancel rule Oni Chain \| | Once the Elder Oni raises its iron club, jabs fall on it like snow on a shrine gate, and the swing comes down anyway. |
| **card** in 01-vibe/Story.md:<br>elder intro card: one line that teaches the twist | **01-vibe/Story.md#Elder intro cards** (16.062)<br>Each elder's twist is shown three ways: an icon on its map node, a one-line intro card, and a short callout the first time the player meets it (Y3). The intro card is one sentence that lets the player work out the twist before the duel starts.<br><br>\| Elder \| Row \| Twist the intro card must teach \|<br>\|---\|---\|---\|<br>\| Nine-Tailed Kitsune \| 5 or 8 \| Reflects any special used twice in a row \|<br>\| Elder Oni \| 5 or 8 \| Armour on every special \|<br>\| Elder Kappa \| 5 or 8 \| Its throws can't be broken \| |  |
| **yokai** in 01-vibe/Characters.md:<br>Oni personality folklore stage | **01-vibe/Characters.md#Oni** (6.08)<br>The rushdown yokai of the snowbound mountain shrine gate. Proud and blunt, it dashes in and swings an iron club. Its hit sparks are oni red.<br><br>Folklore reference: oni carry the kanabō, an iron club; the saying "an oni with an iron club" means strength made stronger.<br><br>Reference images: none yet. |  |
| **voice** in 01-vibe/Tone.md:<br>narrator voice quiet observant | **01-vibe/Tone.md#Voice: the narrator** (18.509)<br>The narrator is quiet and observant, in present tense. In the GDD's sample cards the wake-up card addresses the player as "you" ("You wake at the province's edge.") and the win card names Ryo ("Ryo has no answer yet."). The narrator describes what is seen and lets the cost stay unspoken or half spoken. |  |
| always included | **01-vibe/Tone.md#The tone line** (pinned)<br>Yokai Fighters is mischievous folklore with a melancholy edge, never horror (S1). Yokai are tricksters and lonely spirits, not monsters, and every victory carries a small cost. The setting is a rural Japanese province at the end of autumn, so the mood is dusk, lantern light and things about to end, not night and dread.<br><br>Three checks for any line of text:<br><br>- Mischief: someone in the line is teasing, bargaining or getting away with something.<br>- Melancholy: something is owed, borrowed, waiting or left behind.<br>- No horror: nothing is gory, cruel, demonic or frightening. A yokai that wins walks away; it does not hurt Ryo for pleasure. | |
| always included | **01-vibe/Tone.md#Card form** (pinned)<br>- A story card is one or two sentences (S2), at most 280 characters.<br>- Present tense, plain words, one concrete image per card: a lantern, a paper screen, a tail, a teacup, an ink stroke.<br>- Names come from the player's run through `{yokai}` and `{move}` placeholders (S4). A card never hard-codes a move the player may not own.<br>- Images come from the game's own world: paper talismans, ink, brush strokes, red seals, lanterns, bamboo, snow on a shrine gate, a riverbank, a tea house with paper screens.<br>- A card does not explain a rule in system words. It does not say "health", "frames", "level" or "roguelite"; it says what a person standing there would see. | |

### Critic loop

**Round 1 draft:** Once the Elder Oni raises its iron club, jabs fall on it like snow on a shrine gate, and the swing comes down anyway.

Code checks: clean. Critic: pass.

**Final (passed):** Once the Elder Oni raises its iron club, jabs fall on it like snow on a shrine gate, and the swing comes down anyway.

## Elder intro card: Elder Kappa

Brief: Write the one-line intro card shown on the map node of the Elder Kappa, before the duel starts.

### Query, retrieved chunk, output

| Query | Retrieved chunk (score) | Output |
|---|---|---|
| **twist** in 02-mechanics/Yokai.md:<br>Elder Kappa elder twist and the habit the twist breaks | **02-mechanics/Yokai.md#Kappa** (23.442)<br>The Kappa is the grappler. It offers River Grab, Shell Spin and the Kappa modifiers.<br><br>\| \| \|<br>\|---\|---\|<br>\| Aggressive temperament \| Walks straight into throw range \|<br>\| Patient temperament \| Blocks, then throws after two blocked hits \|<br>\| Habit to punish \| Throws after blocking two hits in a row \|<br>\| Elder \| Elder Kappa \|<br>\| Elder twist \| Its throws can't be broken \|<br>\| Habit the twist breaks \| Relying on the throw break, so the player has to stay out of throw range \|<br>\| Elder offers \| The cancel rule Undertow \| | The Elder Kappa bows by the lantern river, and once its hands close on you, nothing pulls them open, so keep your distance. |
| **card** in 01-vibe/Story.md:<br>elder intro card: one line that teaches the twist | **01-vibe/Story.md#Elder intro cards** (16.062)<br>Each elder's twist is shown three ways: an icon on its map node, a one-line intro card, and a short callout the first time the player meets it (Y3). The intro card is one sentence that lets the player work out the twist before the duel starts.<br><br>\| Elder \| Row \| Twist the intro card must teach \|<br>\|---\|---\|---\|<br>\| Nine-Tailed Kitsune \| 5 or 8 \| Reflects any special used twice in a row \|<br>\| Elder Oni \| 5 or 8 \| Armour on every special \|<br>\| Elder Kappa \| 5 or 8 \| Its throws can't be broken \| |  |
| **yokai** in 01-vibe/Characters.md:<br>Kappa personality folklore stage | **01-vibe/Characters.md#Kappa** (6.101)<br>The grappler of the lantern riverbank. Formal, stubborn and polite, it walks straight into throw range. Its hit sparks are kappa teal.<br><br>Folklore reference: kappa are river spirits that love sumo and cucumbers and carry water in a dish on their heads; bow to one and it bows back, spilling the water.<br><br>Reference images: none yet. |  |
| **voice** in 01-vibe/Tone.md:<br>narrator voice quiet observant | **01-vibe/Tone.md#Voice: the narrator** (18.509)<br>The narrator is quiet and observant, in present tense. In the GDD's sample cards the wake-up card addresses the player as "you" ("You wake at the province's edge.") and the win card names Ryo ("Ryo has no answer yet."). The narrator describes what is seen and lets the cost stay unspoken or half spoken. |  |
| always included | **01-vibe/Tone.md#The tone line** (pinned)<br>Yokai Fighters is mischievous folklore with a melancholy edge, never horror (S1). Yokai are tricksters and lonely spirits, not monsters, and every victory carries a small cost. The setting is a rural Japanese province at the end of autumn, so the mood is dusk, lantern light and things about to end, not night and dread.<br><br>Three checks for any line of text:<br><br>- Mischief: someone in the line is teasing, bargaining or getting away with something.<br>- Melancholy: something is owed, borrowed, waiting or left behind.<br>- No horror: nothing is gory, cruel, demonic or frightening. A yokai that wins walks away; it does not hurt Ryo for pleasure. | |
| always included | **01-vibe/Tone.md#Card form** (pinned)<br>- A story card is one or two sentences (S2), at most 280 characters.<br>- Present tense, plain words, one concrete image per card: a lantern, a paper screen, a tail, a teacup, an ink stroke.<br>- Names come from the player's run through `{yokai}` and `{move}` placeholders (S4). A card never hard-codes a move the player may not own.<br>- Images come from the game's own world: paper talismans, ink, brush strokes, red seals, lanterns, bamboo, snow on a shrine gate, a riverbank, a tea house with paper screens.<br>- A card does not explain a rule in system words. It does not say "health", "frames", "level" or "roguelite"; it says what a person standing there would see. | |

### Critic loop

**Round 1 draft:** The Elder Kappa bows by the lantern river, and once its hands close on you, nothing pulls them open, so keep your distance.

Code checks: clean. Critic: pass.

**Final (passed):** The Elder Kappa bows by the lantern river, and once its hands close on you, nothing pulls them open, so keep your distance.

## Reward card text: Oni's Hide

Brief: Write the reward card text for the modifier Oni's Hide, offered by the Oni.

### Query, retrieved chunk, output

| Query | Retrieved chunk (score) | Output |
|---|---|---|
| **effect** in 02-mechanics/Abilities.md:<br>Oni's Hide modifier effect on the special it is attached to | **02-mechanics/Abilities.md#Oni modifiers** (15.437)<br>A modifier attaches to one special and changes how it plays without new animation (A10). The Oni offers these five.<br><br>\| Modifier \| Effect on the special it is attached to \| Locked \|<br>\|---\|---\|---\|<br>\| Oni's Hide \| Armour on frames 1–5 \| no \|<br>\| Iron Will \| Can't be interrupted by projectiles \| no \|<br>\| Rage Ember \| +15% damage while Ryo is below 30% health \| ◆ \|<br>\| Club Momentum \| Pushes the opponent 50% further toward the corner \| ◆ \|<br>\| Brute Recovery \| 4 fewer recovery frames on block \| ◆ \| | plain: An oni's hide turns aside blows: the start of this special shrugs off hits with armour on its first few frames.<br>frames: Armour on frames 1–5 of the attached special |
| **voice** in 01-vibe/Tone.md:<br>reward card voice: plain and frames examples | **01-vibe/Tone.md#Reward card voice** (12.664)<br>Reward cards are plain language first, with frame data behind a toggle (A12).<br><br>- `plain` is one sentence, at most 140 characters, that says what changes when the player fights. It may open with a short folklore image, then states the effect in ordinary words.<br>- `frames` is the exact number from the ability table, with no flavour.<br>- A plain line never promises more than the table gives.<br><br>\| Ability \| plain \| frames \|<br>\|---\|---\|---\|<br>\| Foxfire \| "Launch a slow fox flame that drifts across the screen." \| "15 / 4 / 30 frames, 70 damage, slow projectile" \|<br>\| Will-o'-wisp \| "Wisp-light lends your projectile wings: it flies faster across the screen." \| "Projectile speed +30% (x1.3, rounded to nearest)" \|<br>\| Fox's Patience \| "The fox waits and learns: this special builds more spirit meter when the foe blocks it." \| "Meter gain on block +25% (C5 default 3 becomes 4, rounded to nearest)" \|<br>\| Oni's Hide (GDD mockup, attached to Rising Talisman) \| "Your uppercut can't be interrupted early on." \| not written yet \| |  |
| **yokai** in 01-vibe/Characters.md:<br>Oni personality folklore | **01-vibe/Characters.md#Oni** (6.08)<br>The rushdown yokai of the snowbound mountain shrine gate. Proud and blunt, it dashes in and swings an iron club. Its hit sparks are oni red.<br><br>Folklore reference: oni carry the kanabō, an iron club; the saying "an oni with an iron club" means strength made stronger.<br><br>Reference images: none yet. |  |

### Critic loop

**Round 1 draft:** plain: An oni's hide turns aside blows: the start of this special shrugs off hits with armour on its first few frames.<br>frames: Armour on frames 1–5 of the attached special

Code checks: clean. Critic: pass.

**Final (passed):** plain: An oni's hide turns aside blows: the start of this special shrugs off hits with armour on its first few frames.<br>frames: Armour on frames 1–5 of the attached special

## Reward card text: Iron Will

Brief: Write the reward card text for the modifier Iron Will, offered by the Oni.

### Query, retrieved chunk, output

| Query | Retrieved chunk (score) | Output |
|---|---|---|
| **effect** in 02-mechanics/Abilities.md:<br>Iron Will modifier effect on the special it is attached to | **02-mechanics/Abilities.md#Oni modifiers** (12.121)<br>A modifier attaches to one special and changes how it plays without new animation (A10). The Oni offers these five.<br><br>\| Modifier \| Effect on the special it is attached to \| Locked \|<br>\|---\|---\|---\|<br>\| Oni's Hide \| Armour on frames 1–5 \| no \|<br>\| Iron Will \| Can't be interrupted by projectiles \| no \|<br>\| Rage Ember \| +15% damage while Ryo is below 30% health \| ◆ \|<br>\| Club Momentum \| Pushes the opponent 50% further toward the corner \| ◆ \|<br>\| Brute Recovery \| 4 fewer recovery frames on block \| ◆ \| | plain: An oni with an iron club shrugs off arrows: this special can't be interrupted by projectiles.<br>frames: Can't be interrupted by projectiles |
| **voice** in 01-vibe/Tone.md:<br>reward card voice: plain and frames examples | **01-vibe/Tone.md#Reward card voice** (12.664)<br>Reward cards are plain language first, with frame data behind a toggle (A12).<br><br>- `plain` is one sentence, at most 140 characters, that says what changes when the player fights. It may open with a short folklore image, then states the effect in ordinary words.<br>- `frames` is the exact number from the ability table, with no flavour.<br>- A plain line never promises more than the table gives.<br><br>\| Ability \| plain \| frames \|<br>\|---\|---\|---\|<br>\| Foxfire \| "Launch a slow fox flame that drifts across the screen." \| "15 / 4 / 30 frames, 70 damage, slow projectile" \|<br>\| Will-o'-wisp \| "Wisp-light lends your projectile wings: it flies faster across the screen." \| "Projectile speed +30% (x1.3, rounded to nearest)" \|<br>\| Fox's Patience \| "The fox waits and learns: this special builds more spirit meter when the foe blocks it." \| "Meter gain on block +25% (C5 default 3 becomes 4, rounded to nearest)" \|<br>\| Oni's Hide (GDD mockup, attached to Rising Talisman) \| "Your uppercut can't be interrupted early on." \| not written yet \| |  |
| **yokai** in 01-vibe/Characters.md:<br>Oni personality folklore | **01-vibe/Characters.md#Oni** (6.08)<br>The rushdown yokai of the snowbound mountain shrine gate. Proud and blunt, it dashes in and swings an iron club. Its hit sparks are oni red.<br><br>Folklore reference: oni carry the kanabō, an iron club; the saying "an oni with an iron club" means strength made stronger.<br><br>Reference images: none yet. |  |

### Critic loop

**Round 1 draft:** plain: An oni with an iron club shrugs off arrows: this special can't be interrupted by projectiles.<br>frames: Can't be interrupted by projectiles

Code checks: clean. Critic: pass.

**Final (passed):** plain: An oni with an iron club shrugs off arrows: this special can't be interrupted by projectiles.<br>frames: Can't be interrupted by projectiles

## Reward card text: Slippery Skin

Brief: Write the reward card text for the modifier Slippery Skin, offered by the Kappa.

### Query, retrieved chunk, output

| Query | Retrieved chunk (score) | Output |
|---|---|---|
| **effect** in 02-mechanics/Abilities.md:<br>Slippery Skin modifier effect on the special it is attached to | **02-mechanics/Abilities.md#Kappa modifiers** (17.647)<br>A modifier attaches to one special and changes how it plays without new animation (A10). The Kappa offers these four.<br><br>\| Modifier \| Effect on the special it is attached to \| Locked \|<br>\|---\|---\|---\|<br>\| Slippery Skin \| Throw-invulnerable during startup \| no \|<br>\| River Pull \| Pulls the opponent one step closer on hit \| no \|<br>\| Cucumber Gift \| Heals 2% of max health on hit \| ◆ \|<br>\| Sumo Stance \| Hold the button to charge for +20% damage \| ◆ \| | plain: Slick as river mud, the Kappa slips free: the special this is attached to can't be thrown while it starts up.<br>frames: Throw-invulnerable during startup of the attached special |
| **voice** in 01-vibe/Tone.md:<br>reward card voice: plain and frames examples | **01-vibe/Tone.md#Reward card voice** (12.664)<br>Reward cards are plain language first, with frame data behind a toggle (A12).<br><br>- `plain` is one sentence, at most 140 characters, that says what changes when the player fights. It may open with a short folklore image, then states the effect in ordinary words.<br>- `frames` is the exact number from the ability table, with no flavour.<br>- A plain line never promises more than the table gives.<br><br>\| Ability \| plain \| frames \|<br>\|---\|---\|---\|<br>\| Foxfire \| "Launch a slow fox flame that drifts across the screen." \| "15 / 4 / 30 frames, 70 damage, slow projectile" \|<br>\| Will-o'-wisp \| "Wisp-light lends your projectile wings: it flies faster across the screen." \| "Projectile speed +30% (x1.3, rounded to nearest)" \|<br>\| Fox's Patience \| "The fox waits and learns: this special builds more spirit meter when the foe blocks it." \| "Meter gain on block +25% (C5 default 3 becomes 4, rounded to nearest)" \|<br>\| Oni's Hide (GDD mockup, attached to Rising Talisman) \| "Your uppercut can't be interrupted early on." \| not written yet \| |  |
| **yokai** in 01-vibe/Characters.md:<br>Kappa personality folklore | **01-vibe/Characters.md#Kappa** (6.101)<br>The grappler of the lantern riverbank. Formal, stubborn and polite, it walks straight into throw range. Its hit sparks are kappa teal.<br><br>Folklore reference: kappa are river spirits that love sumo and cucumbers and carry water in a dish on their heads; bow to one and it bows back, spilling the water.<br><br>Reference images: none yet. |  |

### Critic loop

**Round 1 draft:** plain: Slick as river mud, the Kappa slips free: the special this is attached to can't be thrown while it starts up.<br>frames: Throw-invulnerable during startup of the attached special

Code checks: clean. Critic: pass.

**Final (passed):** plain: Slick as river mud, the Kappa slips free: the special this is attached to can't be thrown while it starts up.<br>frames: Throw-invulnerable during startup of the attached special

## Reward card text: River Pull

Brief: Write the reward card text for the modifier River Pull, offered by the Kappa.

### Query, retrieved chunk, output

| Query | Retrieved chunk (score) | Output |
|---|---|---|
| **effect** in 02-mechanics/Abilities.md:<br>River Pull modifier effect on the special it is attached to | **02-mechanics/Abilities.md#Kappa modifiers** (17.994)<br>A modifier attaches to one special and changes how it plays without new animation (A10). The Kappa offers these four.<br><br>\| Modifier \| Effect on the special it is attached to \| Locked \|<br>\|---\|---\|---\|<br>\| Slippery Skin \| Throw-invulnerable during startup \| no \|<br>\| River Pull \| Pulls the opponent one step closer on hit \| no \|<br>\| Cucumber Gift \| Heals 2% of max health on hit \| ◆ \|<br>\| Sumo Stance \| Hold the button to charge for +20% damage \| ◆ \| | plain: The river tugs back: when this special hits, your opponent is pulled one step closer to you.<br>frames: On hit: pulls opponent one step closer |
| **voice** in 01-vibe/Tone.md:<br>reward card voice: plain and frames examples | **01-vibe/Tone.md#Reward card voice** (12.664)<br>Reward cards are plain language first, with frame data behind a toggle (A12).<br><br>- `plain` is one sentence, at most 140 characters, that says what changes when the player fights. It may open with a short folklore image, then states the effect in ordinary words.<br>- `frames` is the exact number from the ability table, with no flavour.<br>- A plain line never promises more than the table gives.<br><br>\| Ability \| plain \| frames \|<br>\|---\|---\|---\|<br>\| Foxfire \| "Launch a slow fox flame that drifts across the screen." \| "15 / 4 / 30 frames, 70 damage, slow projectile" \|<br>\| Will-o'-wisp \| "Wisp-light lends your projectile wings: it flies faster across the screen." \| "Projectile speed +30% (x1.3, rounded to nearest)" \|<br>\| Fox's Patience \| "The fox waits and learns: this special builds more spirit meter when the foe blocks it." \| "Meter gain on block +25% (C5 default 3 becomes 4, rounded to nearest)" \|<br>\| Oni's Hide (GDD mockup, attached to Rising Talisman) \| "Your uppercut can't be interrupted early on." \| not written yet \| |  |
| **yokai** in 01-vibe/Characters.md:<br>Kappa personality folklore | **01-vibe/Characters.md#Kappa** (6.101)<br>The grappler of the lantern riverbank. Formal, stubborn and polite, it walks straight into throw range. Its hit sparks are kappa teal.<br><br>Folklore reference: kappa are river spirits that love sumo and cucumbers and carry water in a dish on their heads; bow to one and it bows back, spilling the water.<br><br>Reference images: none yet. |  |

### Critic loop

**Round 1 draft:** plain: The river tugs back: when this special hits, your opponent is pulled one step closer to you.<br>frames: On hit: pulls opponent one step closer

Code checks: clean. Critic: pass.

**Final (passed):** plain: The river tugs back: when this special hits, your opponent is pulled one step closer to you.<br>frames: On hit: pulls opponent one step closer
