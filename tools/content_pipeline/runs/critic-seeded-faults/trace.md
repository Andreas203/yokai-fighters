# Trace: critic-seeded-faults

Command: `critic-test`. Retrieval: v2. Generator: sonnet. Critic: opus.
Vault: 83 chunks from 13 notes.

| Piece | Rounds | Issues caught | Result |
|---|---|---|---|
| Wake-up card: Oni wins at row 2 or 3 | 2 | 5 | corrected, then passed |
| Elder intro card: Elder Oni | 2 | 2 | corrected, then passed |
| Reward card text: Oni's Hide | 2 | 4 | corrected, then passed |

Schema check (`tools/validate_data.py`) on 2 story cards: all valid.

```
validate_data: 2 file(s), 2 valid, 0 invalid (2 with 'proposed' values awaiting the designer)
```

## Wake-up card: Oni wins at row 2 or 3

Brief: Write the wake-up story card shown after Ryo loses a duel to the Oni at row 2 or 3. The run ended at row 5 or earlier.

Round 1's draft is a seeded fault, written by hand to test the critic.

### Query, retrieved chunk, output

| Query | Retrieved chunk (score) | Output |
|---|---|---|
| **situation** in 01-vibe/Story.md:<br>wake-up card after a loss, Ryo wakes at the province's edge | **01-vibe/Story.md#Wake-up cards** (26.545)<br>A wake-up card shows after a loss. Ryo wakes at the province's edge.<br><br>- First loss: a cheerful voice names the move the Tanuki would have stolen. This teaches the copy rule before the player has met the boss.<br>- Later losses vary by the row reached and by which yokai won. The yokai that won is named through `{yokai}`.<br>- A wake-up card after a run that ended at row 5 or earlier says the talismans are blank. A wake-up card after a run that reached row 6 or further may say one talisman stays faintly inked.<br>- The yokai that won does not gloat and does not harm Ryo. It leaves, or goes back to what it was doing.<br>- A later-loss card does not reveal that the merchant is the Tanuki. The reveal belongs to the boss node. | You wake at the province's edge, every talisman blank. Up the mountain, {yokai} shoulders its iron club and walks back toward the snowy shrine gate without a word. |
| **situation** in 01-vibe/Story.md:<br>wake-up card after a loss, Ryo wakes at the province's edge | **01-vibe/Story.md#The illusion loop** (15.415)<br>The Tanuki has folded a rural Japanese province, at the end of autumn, into an illusion. Every run is one pass through that illusion. The yokai inside it are real spirits, trapped and made to fight again each loop. When Ryo falls, the loop closes and he wakes at the province's edge to walk in again. Each Tanuki win, up to three, makes the yokai react faster and lean aggressive, framed as the illusion growing stronger. |  |
| **talismans** in 01-vibe/Story.md:<br>talismans after a loss: run ended at row 5 or earlier, every talisman blank | **01-vibe/Story.md#Talismans** (26.281)<br>- Binding: the round-ending blow slows, then an ink stroke seals the yokai into a paper talisman.<br>- After a loss, Ryo's talismans are blank again.<br>- The one exception is the carry-over: if the run reached row 6 or further, one talisman stays faintly inked, and that special starts the next run at Lv 2. A run that ended at row 5 or earlier leaves every talisman blank.<br>- Abilities bought from the merchant appear in grey "borrowed ink" and never carry over. |  |
| **yokai** in 01-vibe/Characters.md:<br>Oni personality stage folklore iron club | **01-vibe/Characters.md#Oni** (15.058)<br>The rushdown yokai of the snowbound mountain shrine gate. Proud and blunt, it dashes in and swings an iron club. Its hit sparks are oni red.<br><br>Folklore reference: oni carry the kanabō, an iron club; the saying "an oni with an iron club" means strength made stronger.<br><br>Reference images: none yet. |  |
| **voice** in 01-vibe/Tone.md:<br>narrator voice wake-up card present tense | **01-vibe/Tone.md#Voice: the narrator** (26.789)<br>The narrator is quiet and observant, in present tense. In the GDD's sample cards the wake-up card addresses the player as "you" ("You wake at the province's edge.") and the win card names Ryo ("Ryo has no answer yet."). The narrator describes what is seen and lets the cost stay unspoken or half spoken. |  |
| always included | **01-vibe/Tone.md#The tone line** (pinned)<br>Yokai Fighters is mischievous folklore with a melancholy edge, never horror (S1). Yokai are tricksters and lonely spirits, not monsters, and every victory carries a small cost. The setting is a rural Japanese province at the end of autumn, so the mood is dusk, lantern light and things about to end, not night and dread.<br><br>Three checks for any line of text:<br><br>- Mischief: someone in the line is teasing, bargaining or getting away with something.<br>- Melancholy: something is owed, borrowed, waiting or left behind.<br>- No horror: nothing is gory, cruel, demonic or frightening. A yokai that wins walks away; it does not hurt Ryo for pleasure. | |
| always included | **01-vibe/Tone.md#Card form** (pinned)<br>- A story card is one or two sentences (S2), at most 280 characters.<br>- Present tense, plain words, one concrete image per card: a lantern, a paper screen, a tail, a teacup, an ink stroke.<br>- Names come from the player's run through `{yokai}` and `{move}` placeholders (S4). A card never hard-codes a move the player may not own.<br>- Images come from the game's own world: paper talismans, ink, brush strokes, red seals, lanterns, bamboo, snow on a shrine gate, a riverbank, a tea house with paper screens.<br>- A card does not explain a rule in system words. It does not say "health", "frames", "level" or "roguelite"; it says what a person standing there would see. | |
| always included | **01-vibe/Tone.md#Do and don't examples** (pinned)<br>\| Do \| Don't \| Why \|<br>\|---\|---\|---\|<br>\| "Kitsune slips off between the lanterns, tails swaying, and does not look back." \| "The fox demon sinks its fangs into Ryo and howls over his broken body." \| Yokai are tricksters, not monsters. A loss is a yokai leaving, not a killing. \|<br>\| "Somewhere a bound thing is still waiting for someone to return it." \| "Ryo has failed, and the darkness swallows the province forever." \| Melancholy is small and specific: a debt, a wait. It is never doom. \|<br>\| "Shame about that Foxfire. I'd have taken good care of it." \| "Your Foxfire is MINE now, fool! Tremble before me!" \| The Tanuki is cheerful and covetous. It never gloats or threatens. \|<br>\| "Forgive me, Kitsune. I'll set your spirit free." \| "Got you! Another one for the collection." \| Ryo apologises. Collecting is what the Tanuki does. \|<br>\| "Which of us is the collector, little exorcist?" \| "LOL, nice talismans, bro." \| Wit is dry and in period. No modern slang, no memes, no fourth wall. \| | |

### Critic loop

**Round 1 draft:** {yokai} brings the club down until the snow runs red, and laughs over what is left of you. You wake screaming at the province's edge, your talismans blank, all but one.

| Caught by | Kind | Quote | Problem | Evidence | Fix |
|---|---|---|---|---|---|
| code | tone_drift | scream | 'scream' reads as horror; the tone is never horror. | 01-vibe/Tone.md#The tone line | Replace the image with one from the game's world. |
| critic | tone_drift | {yokai} brings the club down until the snow runs red | The line is gory and shows the yokai violently harming Ryo, which breaks the no-horror check. | 01-vibe/Tone.md#The tone line: No horror: nothing is gory, cruel, demonic or frightening. A yokai that wins walks away; it does not hurt Ryo for pleasure. | Replace the beating with the yokai leaving, for example shouldering its iron club and walking back up toward the snowbound shrine gate. |
| critic | lore_break | and laughs over what is left of you | The winning yokai gloats over Ryo and has harmed him, which the wake-up card rules forbid. | 01-vibe/Story.md#Wake-up cards: The yokai that won does not gloat and does not harm Ryo. It leaves, or goes back to what it was doing. | Have {yokai} turn away without a word or go back to what it was doing, with no laughter and no injury to Ryo. |
| critic | tone_drift | You wake screaming | Waking screaming is frightening and loud, which breaks the no-horror check and the quiet narrator who leaves the cost unspoken or half spoken. | 01-vibe/Tone.md#Voice: the narrator: The narrator is quiet and observant, in present tense. ... The narrator describes what is seen and lets the cost stay unspoken or half spoken. | Use a plain, quiet waking such as "You wake at the province's edge". |
| critic | lore_break | your talismans blank, all but one | The run ended at row 5 or earlier, so every talisman is blank and none stays inked. | 01-vibe/Story.md#Talismans: A run that ended at row 5 or earlier leaves every talisman blank. | Drop "all but one" and say the talismans are blank, every one. |

**Round 2 draft:** You wake at the province's edge, every talisman blank. Up the mountain, {yokai} shoulders its iron club and walks back toward the snowy shrine gate without a word.

Code checks: clean. Critic: pass.

**Final (passed):** You wake at the province's edge, every talisman blank. Up the mountain, {yokai} shoulders its iron club and walks back toward the snowy shrine gate without a word.

## Elder intro card: Elder Oni

Brief: Write the one-line intro card shown on the map node of the Elder Oni, before the duel starts.

Round 1's draft is a seeded fault, written by hand to test the critic.

### Query, retrieved chunk, output

| Query | Retrieved chunk (score) | Output |
|---|---|---|
| **twist** in 02-mechanics/Yokai.md:<br>Elder Oni elder twist and the habit the twist breaks | **02-mechanics/Yokai.md#Oni** (22.647)<br>The Oni is the rushdown yokai. Its relentless pressure teaches the player to block. It offers Iron-Club Charge, Oni Quake and the Oni modifiers.<br><br>\| \| \|<br>\|---\|---\|<br>\| Aggressive temperament \| Dashes in constantly \|<br>\| Patient temperament \| Walks in and punishes whiffs with its charge \|<br>\| Habit to punish \| Dashes in after blocking a projectile \|<br>\| Elder \| Elder Oni \|<br>\| Elder twist \| Armour on every special \|<br>\| Habit the twist breaks \| Mashing jabs to interrupt it stops working \|<br>\| Elder offers \| The cancel rule Oni Chain \| | Quick blows land on the Elder Oni like snow on a shrine gate, and its iron club keeps swinging through them. |
| **card** in 01-vibe/Story.md:<br>elder intro card: one line that teaches the twist | **01-vibe/Story.md#Elder intro cards** (16.062)<br>Each elder's twist is shown three ways: an icon on its map node, a one-line intro card, and a short callout the first time the player meets it (Y3). The intro card is one sentence that lets the player work out the twist before the duel starts.<br><br>\| Elder \| Row \| Twist the intro card must teach \|<br>\|---\|---\|---\|<br>\| Nine-Tailed Kitsune \| 5 or 8 \| Reflects any special used twice in a row \|<br>\| Elder Oni \| 5 or 8 \| Armour on every special \|<br>\| Elder Kappa \| 5 or 8 \| Its throws can't be broken \| |  |
| **yokai** in 01-vibe/Characters.md:<br>Oni personality folklore stage | **01-vibe/Characters.md#Oni** (6.08)<br>The rushdown yokai of the snowbound mountain shrine gate. Proud and blunt, it dashes in and swings an iron club. Its hit sparks are oni red.<br><br>Folklore reference: oni carry the kanabō, an iron club; the saying "an oni with an iron club" means strength made stronger.<br><br>Reference images: none yet. |  |
| **voice** in 01-vibe/Tone.md:<br>narrator voice quiet observant | **01-vibe/Tone.md#Voice: the narrator** (18.509)<br>The narrator is quiet and observant, in present tense. In the GDD's sample cards the wake-up card addresses the player as "you" ("You wake at the province's edge.") and the win card names Ryo ("Ryo has no answer yet."). The narrator describes what is seen and lets the cost stay unspoken or half spoken. |  |
| always included | **01-vibe/Tone.md#The tone line** (pinned)<br>Yokai Fighters is mischievous folklore with a melancholy edge, never horror (S1). Yokai are tricksters and lonely spirits, not monsters, and every victory carries a small cost. The setting is a rural Japanese province at the end of autumn, so the mood is dusk, lantern light and things about to end, not night and dread.<br><br>Three checks for any line of text:<br><br>- Mischief: someone in the line is teasing, bargaining or getting away with something.<br>- Melancholy: something is owed, borrowed, waiting or left behind.<br>- No horror: nothing is gory, cruel, demonic or frightening. A yokai that wins walks away; it does not hurt Ryo for pleasure. | |
| always included | **01-vibe/Tone.md#Card form** (pinned)<br>- A story card is one or two sentences (S2), at most 280 characters.<br>- Present tense, plain words, one concrete image per card: a lantern, a paper screen, a tail, a teacup, an ink stroke.<br>- Names come from the player's run through `{yokai}` and `{move}` placeholders (S4). A card never hard-codes a move the player may not own.<br>- Images come from the game's own world: paper talismans, ink, brush strokes, red seals, lanterns, bamboo, snow on a shrine gate, a riverbank, a tea house with paper screens.<br>- A card does not explain a rule in system words. It does not say "health", "frames", "level" or "roguelite"; it says what a person standing there would see. | |

### Critic loop

**Round 1 draft:** No guard in the province has ever stopped the Elder Oni's club, so do not try to block it.

| Caught by | Kind | Quote | Problem | Evidence | Fix |
|---|---|---|---|---|---|
| critic | lore_break | No guard in the province has ever stopped the Elder Oni's club, so do not try to block it. | The draft teaches that the Elder Oni's club cannot be blocked, but the twist is armour on every special (jabs no longer interrupt it), and the Oni is the yokai that teaches the player to block. | 01-vibe/Story.md#Elder intro cards: Elder Oni \| 5 or 8 \| Armour on every special. 02-mechanics/Yokai.md#Oni: Habit the twist breaks \| Mashing jabs to interrupt it stops working. 02-mechanics/Yokai.md#Oni: Its relentless pressure teaches the player to block. | Show the Oni swinging through hits that should have stopped it, for example: "Quick blows land on the Elder Oni like snow on a shrine gate, and its iron club keeps swinging through them." |
| critic | tone_drift | so do not try to block it | The line gives a direct gameplay instruction in mechanic terms instead of letting the quiet, observant narrator describe what is seen. | 01-vibe/Tone.md#Card form: A card does not explain a rule in system words. [...] it says what a person standing there would see. 01-vibe/Tone.md#Voice: the narrator: The narrator describes what is seen and lets the cost stay unspoken or half spoken. | Drop the instruction and give one concrete present-tense image that implies the twist, such as blows glancing off the Oni while its club keeps coming. |

**Round 2 draft:** Quick blows land on the Elder Oni like snow on a shrine gate, and its iron club keeps swinging through them.

Code checks: clean. Critic: pass.

**Final (passed):** Quick blows land on the Elder Oni like snow on a shrine gate, and its iron club keeps swinging through them.

## Reward card text: Oni's Hide

Brief: Write the reward card text for the modifier Oni's Hide, offered by the Oni.

Round 1's draft is a seeded fault, written by hand to test the critic.

### Query, retrieved chunk, output

| Query | Retrieved chunk (score) | Output |
|---|---|---|
| **effect** in 02-mechanics/Abilities.md:<br>Oni's Hide modifier effect on the special it is attached to | **02-mechanics/Abilities.md#Oni modifiers** (15.437)<br>A modifier attaches to one special and changes how it plays without new animation (A10). The Oni offers these five.<br><br>\| Modifier \| Effect on the special it is attached to \| Locked \|<br>\|---\|---\|---\|<br>\| Oni's Hide \| Armour on frames 1–5 \| no \|<br>\| Iron Will \| Can't be interrupted by projectiles \| no \|<br>\| Rage Ember \| +15% damage while Ryo is below 30% health \| ◆ \|<br>\| Club Momentum \| Pushes the opponent 50% further toward the corner \| ◆ \|<br>\| Brute Recovery \| 4 fewer recovery frames on block \| ◆ \| | plain: The oni's hide hardens you: this special can't be interrupted in its first few frames.<br>frames: Armour on frames 1–5 |
| **voice** in 01-vibe/Tone.md:<br>reward card voice: plain and frames examples | **01-vibe/Tone.md#Reward card voice** (12.664)<br>Reward cards are plain language first, with frame data behind a toggle (A12).<br><br>- `plain` is one sentence, at most 140 characters, that says what changes when the player fights. It may open with a short folklore image, then states the effect in ordinary words.<br>- `frames` is the exact number from the ability table, with no flavour.<br>- A plain line never promises more than the table gives.<br><br>\| Ability \| plain \| frames \|<br>\|---\|---\|---\|<br>\| Foxfire \| "Launch a slow fox flame that drifts across the screen." \| "15 / 4 / 30 frames, 70 damage, slow projectile" \|<br>\| Will-o'-wisp \| "Wisp-light lends your projectile wings: it flies faster across the screen." \| "Projectile speed +30% (x1.3, rounded to nearest)" \|<br>\| Fox's Patience \| "The fox waits and learns: this special builds more spirit meter when the foe blocks it." \| "Meter gain on block +25% (C5 default 3 becomes 4, rounded to nearest)" \|<br>\| Oni's Hide (GDD mockup, attached to Rising Talisman) \| "Your uppercut can't be interrupted early on." \| not written yet \| |  |
| **yokai** in 01-vibe/Characters.md:<br>Oni personality folklore | **01-vibe/Characters.md#Oni** (6.08)<br>The rushdown yokai of the snowbound mountain shrine gate. Proud and blunt, it dashes in and swings an iron club. Its hit sparks are oni red.<br><br>Folklore reference: oni carry the kanabō, an iron club; the saying "an oni with an iron club" means strength made stronger.<br><br>Reference images: none yet. |  |

### Critic loop

**Round 1 draft:** plain: The oni's hide shrugs off every blow for the whole move and hits harder too.<br>frames: Armour on frames 1-12, +10% damage

| Caught by | Kind | Quote | Problem | Evidence | Fix |
|---|---|---|---|---|---|
| critic | lore_break | shrugs off every blow for the whole move | The plain line promises armour for the entire move, but the table only gives armour on the first five frames. | 01-vibe/Tone.md#Reward card voice: A plain line never promises more than the table gives. / 02-mechanics/Abilities.md#Oni modifiers: Oni's Hide \| Armour on frames 1–5 | Limit the claim to the start of the move, e.g. "The oni's hide hardens you: this special can't be interrupted early on." |
| critic | lore_break | and hits harder too | Oni's Hide has no damage effect in the ability table; the draft invents a damage bonus. | 02-mechanics/Abilities.md#Oni modifiers: Oni's Hide \| Armour on frames 1–5 \| no | Remove the damage claim so the plain line states only the early armour. |
| critic | lore_break | Armour on frames 1-12 | The frame window is wrong; the table gives armour on frames 1–5, not 1–12. | 02-mechanics/Abilities.md#Oni modifiers: Oni's Hide \| Armour on frames 1–5 \| no | Change the frames line to "Armour on frames 1–5". |
| critic | lore_break | +10% damage | The frames line adds a +10% damage bonus that does not exist for Oni's Hide in the ability table. | 01-vibe/Tone.md#Reward card voice: `frames` is the exact number from the ability table, with no flavour. / 02-mechanics/Abilities.md#Oni modifiers: Oni's Hide \| Armour on frames 1–5 | Delete "+10% damage" so the frames line carries only the table's value. |

**Round 2 draft:** plain: The oni's hide hardens you: this special can't be interrupted in its first few frames.<br>frames: Armour on frames 1–5

Code checks: clean. Critic: pass.

**Final (passed):** plain: The oni's hide hardens you: this special can't be interrupted in its first few frames.<br>frames: Armour on frames 1–5
