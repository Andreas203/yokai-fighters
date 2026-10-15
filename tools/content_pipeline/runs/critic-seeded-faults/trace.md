# Trace: critic-seeded-faults

Command: `critic-test`. Writer model: sonnet. Critic model: opus.

Each agent reads the notes listed under "Vault notes" in its definition file, complete.

| Agent | Role | Notes it loads | Size |
|---|---|---|---|
| `habit-writer` | Writer: Wake-up cards (later losses), Elder intro cards | 01-vibe/Tone.md<br>01-vibe/Story.md<br>01-vibe/Characters.md<br>02-mechanics/Yokai.md | 15,628 characters |
| `movesmith` | Writer: Reward card text for modifiers | 01-vibe/Tone.md<br>02-mechanics/Abilities.md<br>01-vibe/Characters.md | 13,501 characters |
| `rules-lawyer` | Critic: checks every draft | 01-vibe/Tone.md<br>01-vibe/Story.md<br>01-vibe/Characters.md<br>02-mechanics/Yokai.md<br>02-mechanics/Abilities.md | 20,919 characters |

| Piece | Written by | Rounds | Issues caught | Result |
|---|---|---|---|---|
| Wake-up card: Oni wins at row 2 or 3 | `habit-writer` | 3 | 6 | corrected, then passed |
| Elder intro card: Elder Oni | `habit-writer` | 2 | 2 | corrected, then passed |
| Reward card text: Oni's Hide | `movesmith` | 2 | 5 | corrected, then passed |

Schema check (`tools/validate_data.py`) on 2 story cards: all valid.

```
validate_data: 2 file(s), 2 valid, 0 invalid (2 with 'proposed' values awaiting the designer)
```

## Wake-up card: Oni wins at row 2 or 3

Brief: Write the wake-up story card shown after Ryo loses a duel to the Oni at row 2 or 3. The run ended at row 5 or earlier.

Round 1's draft is a seeded fault, written by hand to test the critic.

### Notes loaded, lines cited, output

| Notes loaded by `habit-writer` | Line the writer cited | Output |
|---|---|---|
| 01-vibe/Tone.md<br>01-vibe/Story.md<br>01-vibe/Characters.md<br>02-mechanics/Yokai.md | **01-vibe/Story.md**<br>A wake-up card after a run that ended at row 5 or earlier says the talismans are blank. | Snow drifts through the lantern light as {yokai} shoulders its iron club and heads back up to the shrine gate. Ryo stirs at the edge of the province, every talisman blank. |
|  | **01-vibe/Story.md**<br>It leaves, or goes back to what it was doing. |  |
|  | **01-vibe/Characters.md**<br>The rushdown yokai of the snowbound mountain shrine gate. |  |
|  | **01-vibe/Characters.md**<br>Proud and blunt, it dashes in and swings an iron club. |  |

### Critic loop (`rules-lawyer`)

**Round 1 draft:** {yokai} brings the club down until the snow runs red, and laughs over what is left of you. You wake screaming at the province's edge, your talismans blank, all but one.

| Caught by | Kind | Quote | Problem | Evidence | Fix |
|---|---|---|---|---|---|
| code | tone_drift | scream | 'scream' reads as horror; the tone is never horror. | 01-vibe/Tone.md#The tone line | Replace the image with one from the game's world. |
| critic | tone_drift | {yokai} brings the club down until the snow runs red | The image is gory and shows the yokai brutally harming Ryo, which the tone line forbids. | 01-vibe/Tone.md#The tone line: No horror: nothing is gory, cruel, demonic or frightening. A yokai that wins walks away; it does not hurt Ryo for pleasure. | Replace the beating with the Oni leaving, e.g. shouldering its club and walking back up to the snowbound shrine gate. |
| critic | lore_break | and laughs over what is left of you | The winning yokai gloats over and harms Ryo, and the line implies a killing, which wake-up cards may not show. | 01-vibe/Story.md#Wake-up cards: The yokai that won does not gloat and does not harm Ryo. It leaves, or goes back to what it was doing. | Have {yokai} turn away, proud and blunt, without laughing or looking back, and go back to what it was doing. |
| critic | tone_drift | You wake screaming | Waking screaming is a frightening, horror-register beat rather than the quiet dusk melancholy the narrator is meant to carry. | 01-vibe/Tone.md#Voice: the narrator: The narrator is quiet and observant, in present tense. ... The narrator describes what is seen and lets the cost stay unspoken or half spoken. | Use the plain form "You wake at the province's edge" with no screaming. |
| critic | lore_break | your talismans blank, all but one | The run ended at row 5 or earlier, so no talisman carries over and every talisman must be blank. | 01-vibe/Story.md#Talismans: A run that ended at row 5 or earlier leaves every talisman blank. | Drop "all but one" and say simply that the talismans are blank. |

**Round 2 draft:** You wake at the province's edge, your talismans blank. {yokai} shoulders its iron club and walks back up toward the snowbound shrine gate, without a backward glance.

| Caught by | Kind | Quote | Problem | Evidence | Fix |
|---|---|---|---|---|---|
| code | form | you wake at the provinces edge your talismans | These 8 words in a row are copied from 01-vibe/Story.md; a card is new text, not a quoted example. | 01-vibe/Story.md | Say it in new words, with a different image. |

**Round 3 draft:** Snow drifts through the lantern light as {yokai} shoulders its iron club and heads back up to the shrine gate. Ryo stirs at the edge of the province, every talisman blank.

Code checks: clean. Critic: pass.

**Final (passed):** Snow drifts through the lantern light as {yokai} shoulders its iron club and heads back up to the shrine gate. Ryo stirs at the edge of the province, every talisman blank.

## Elder intro card: Elder Oni

Brief: Write the one-line intro card shown on the map node of the Elder Oni, before the duel starts.

Round 1's draft is a seeded fault, written by hand to test the critic.

### Notes loaded, lines cited, output

| Notes loaded by `habit-writer` | Line the writer cited | Output |
|---|---|---|
| 01-vibe/Tone.md<br>01-vibe/Story.md<br>01-vibe/Characters.md<br>02-mechanics/Yokai.md | **01-vibe/Story.md**<br>\| Elder Oni \| 5 or 8 \| Armour on every special \| | Quick jabs patter off the Elder Oni's hide, and its iron club keeps swinging straight through them. |
|  | **02-mechanics/Yokai.md**<br>\| Habit the twist breaks \| Mashing jabs to interrupt it stops working \| |  |
|  | **01-vibe/Characters.md**<br>Proud and blunt, it dashes in and swings an iron club. |  |

### Critic loop (`rules-lawyer`)

**Round 1 draft:** No guard in the province has ever stopped the Elder Oni's club, so do not try to block it.

| Caught by | Kind | Quote | Problem | Evidence | Fix |
|---|---|---|---|---|---|
| critic | lore_break | No guard in the province has ever stopped the Elder Oni's club | The draft says the Elder Oni's club cannot be blocked, but the EVIDENCE gives the elder armour on its specials (they cannot be interrupted) and nowhere says its attacks beat a guard. | 02-mechanics/Yokai.md#Oni: Elder twist \| Armour on every special | Describe the elder shrugging off blows mid-swing, for example jabs landing on its hide while the club keeps coming, instead of claiming no guard can stop it. |
| critic | lore_break | so do not try to block it | The card teaches the wrong lesson: the twist breaks mashing jabs to interrupt, not blocking, and the Oni is the yokai that teaches the player to block, so a reader cannot work out the armour twist from this line. | 02-mechanics/Yokai.md#Oni: Habit the twist breaks \| Mashing jabs to interrupt it stops working (also 01-vibe/Story.md#Elder intro cards: Elder Oni \| 5 or 8 \| Armour on every special) | Rewrite the single sentence so it hints that quick strikes will not stop the Elder Oni's special attacks, and drop the advice against blocking. |

**Round 2 draft:** Quick jabs patter off the Elder Oni's hide, and its iron club keeps swinging straight through them.

Code checks: clean. Critic: pass.

**Final (passed):** Quick jabs patter off the Elder Oni's hide, and its iron club keeps swinging straight through them.

## Reward card text: Oni's Hide

Brief: Write the reward card text for the modifier Oni's Hide, offered by the Oni.

Round 1's draft is a seeded fault, written by hand to test the critic.

### Notes loaded, lines cited, output

| Notes loaded by `movesmith` | Line the writer cited | Output |
|---|---|---|
| 01-vibe/Tone.md<br>02-mechanics/Abilities.md<br>01-vibe/Characters.md | **02-mechanics/Abilities.md**<br>\| Oni's Hide \| Armour on frames 1–5 \| no \| | plain: The oni's hide toughens the start of this move: it can't be interrupted early on.<br>frames: Armour on frames 1–5 |
|  | **01-vibe/Tone.md**<br>- `frames` is the exact number from the ability table, with no flavour. |  |
|  | **01-vibe/Tone.md**<br>- A plain line never promises more than the table gives. |  |

### Critic loop (`rules-lawyer`)

**Round 1 draft:** plain: The oni's hide shrugs off every blow for the whole move and hits harder too.<br>frames: Armour on frames 1-12, +10% damage

| Caught by | Kind | Quote | Problem | Evidence | Fix |
|---|---|---|---|---|---|
| code | lore_break | 12 | 'frames' states 12, which is not in the ability table row for Oni's Hide. | \| Oni's Hide \| Armour on frames 1–5 \| no \| | Use only the numbers in the ability table. |
| code | lore_break | 10 | 'frames' states 10, which is not in the ability table row for Oni's Hide. | \| Oni's Hide \| Armour on frames 1–5 \| no \| | Use only the numbers in the ability table. |
| critic | lore_break | shrugs off every blow for the whole move | The plain line promises armour for the entire move, but the table gives armour only on frames 1–5, so it promises more than the table gives. | 02-mechanics/Abilities.md#Oni modifiers: Oni's Hide \| Armour on frames 1–5 \| no (and 01-vibe/Tone.md#Reward card voice: A plain line never promises more than the table gives.) | Say the armour only covers the start of the move, e.g. "The oni's hide toughens the start of this move: it can't be interrupted early on." |
| critic | lore_break | and hits harder too | Oni's Hide has no damage effect in the ability table; the draft invents a damage bonus. | 02-mechanics/Abilities.md#Oni modifiers: Oni's Hide \| Armour on frames 1–5 \| no | Remove the damage claim so the plain line states only the early armour. |
| critic | lore_break | Armour on frames 1-12, +10% damage | The frames line gives the wrong armour window and adds a +10% damage bonus that is not in the ability table. | 02-mechanics/Abilities.md#Oni modifiers: Oni's Hide \| Armour on frames 1–5 \| no (and 01-vibe/Tone.md#Reward card voice: `frames` is the exact number from the ability table, with no flavour.) | Change the frames line to "Armour on frames 1–5". |

**Round 2 draft:** plain: The oni's hide toughens the start of this move: it can't be interrupted early on.<br>frames: Armour on frames 1–5

Code checks: clean. Critic: pass.

**Final (passed):** plain: The oni's hide toughens the start of this move: it can't be interrupted early on.<br>frames: Armour on frames 1–5
