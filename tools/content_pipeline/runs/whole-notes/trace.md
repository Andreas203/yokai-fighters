# Trace: whole-notes

Command: `generate`. Writer model: sonnet. Critic model: opus.

Each agent reads the notes listed under "Vault notes" in its definition file, complete.

| Agent | Role | Notes it loads | Size |
|---|---|---|---|
| `habit-writer` | Writer: Wake-up cards (later losses), Elder intro cards | 01-vibe/Tone.md<br>01-vibe/Story.md<br>01-vibe/Characters.md<br>02-mechanics/Yokai.md | 15,628 characters |
| `movesmith` | Writer: Reward card text for modifiers | 01-vibe/Tone.md<br>02-mechanics/Abilities.md<br>01-vibe/Characters.md | 13,501 characters |
| `rules-lawyer` | Critic: checks every draft | 01-vibe/Tone.md<br>01-vibe/Story.md<br>01-vibe/Characters.md<br>02-mechanics/Yokai.md<br>02-mechanics/Abilities.md | 20,919 characters |

| Piece | Written by | Rounds | Issues caught | Result |
|---|---|---|---|---|
| Wake-up card: Oni wins at row 2 or 3 | `habit-writer` | 1 | 0 | passed first time |
| Wake-up card: Nine-Tailed Kitsune wins at row 5 | `habit-writer` | 2 | 1 | corrected, then passed |
| Wake-up card: Kappa wins at row 6 | `habit-writer` | 2 | 1 | corrected, then passed |
| Elder intro card: Nine-Tailed Kitsune | `habit-writer` | 1 | 0 | passed first time |
| Elder intro card: Elder Oni | `habit-writer` | 2 | 1 | corrected, then passed |
| Elder intro card: Elder Kappa | `habit-writer` | 1 | 0 | passed first time |
| Reward card text: Oni's Hide | `movesmith` | 1 | 0 | passed first time |
| Reward card text: Iron Will | `movesmith` | 1 | 0 | passed first time |
| Reward card text: Slippery Skin | `movesmith` | 1 | 0 | passed first time |
| Reward card text: River Pull | `movesmith` | 1 | 0 | passed first time |

Schema check (`tools/validate_data.py`) on 6 story cards: all valid.

```
validate_data: 6 file(s), 6 valid, 0 invalid (6 with 'proposed' values awaiting the designer)
```

## Wake-up card: Oni wins at row 2 or 3

Brief: Write the wake-up story card shown after Ryo loses a duel to the Oni at row 2 or 3. The run ended at row 5 or earlier.

### Notes loaded, lines cited, output

| Notes loaded by `habit-writer` | Line the writer cited | Output |
|---|---|---|
| 01-vibe/Tone.md<br>01-vibe/Story.md<br>01-vibe/Characters.md<br>02-mechanics/Yokai.md | **01-vibe/Story.md**<br>A wake-up card after a run that ended at row 5 or earlier says the talismans are blank. | {yokai} shoulders its iron club and heads back toward the snowy shrine gate without a word. You wake at the province's edge, and every talisman is blank. |
|  | **01-vibe/Story.md**<br>The yokai that won does not gloat and does not harm Ryo. |  |
|  | **01-vibe/Story.md**<br>Ryo wakes at the province's edge. |  |
|  | **01-vibe/Characters.md**<br>The rushdown yokai of the snowbound mountain shrine gate. |  |

### Critic loop (`rules-lawyer`)

**Round 1 draft:** {yokai} shoulders its iron club and heads back toward the snowy shrine gate without a word. You wake at the province's edge, and every talisman is blank.

Code checks: clean. Critic: pass.

**Final (passed):** {yokai} shoulders its iron club and heads back toward the snowy shrine gate without a word. You wake at the province's edge, and every talisman is blank.

## Wake-up card: Nine-Tailed Kitsune wins at row 5

Brief: Write the wake-up story card shown after Ryo loses a duel to the Nine-Tailed Kitsune at row 5. The run ended at row 5 or earlier.

### Notes loaded, lines cited, output

| Notes loaded by `habit-writer` | Line the writer cited | Output |
|---|---|---|
| 01-vibe/Tone.md<br>01-vibe/Story.md<br>01-vibe/Characters.md<br>02-mechanics/Yokai.md | **01-vibe/Story.md**<br>A wake-up card after a run that ended at row 5 or earlier says the talismans are blank. | {yokai} wanders off through the bamboo at dusk, unhurried, one tail trailing like a sleeve. You wake at the province's edge, and every talisman is blank. |
|  | **01-vibe/Story.md**<br>The yokai that won does not gloat and does not harm Ryo. |  |
|  | **01-vibe/Story.md**<br>When Ryo falls, the loop closes and he wakes at the province's edge to walk in again. |  |

### Critic loop (`rules-lawyer`)

**Round 1 draft:** {yokai} slips off between the lanterns, tails swaying, and does not look back. You wake at the province's edge, and every talisman is blank.

| Caught by | Kind | Quote | Problem | Evidence | Fix |
|---|---|---|---|---|---|
| code | form | yokai slips off between the lanterns tails swaying and does not look back | These 13 words in a row are copied from 01-vibe/Story.md; a card is new text, not a quoted example. | 01-vibe/Story.md | Say it in new words, with a different image. |

**Round 2 draft:** {yokai} wanders off through the bamboo at dusk, unhurried, one tail trailing like a sleeve. You wake at the province's edge, and every talisman is blank.

Code checks: clean. Critic: pass.

**Final (passed):** {yokai} wanders off through the bamboo at dusk, unhurried, one tail trailing like a sleeve. You wake at the province's edge, and every talisman is blank.

## Wake-up card: Kappa wins at row 6

Brief: Write the wake-up story card shown after Ryo loses a duel to the Kappa at row 6. The run reached row 6.

### Notes loaded, lines cited, output

| Notes loaded by `habit-writer` | Line the writer cited | Output |
|---|---|---|
| 01-vibe/Tone.md<br>01-vibe/Story.md<br>01-vibe/Characters.md<br>02-mechanics/Yokai.md | **01-vibe/Story.md**<br>A wake-up card after a run that reached row 6 or further may say one talisman stays faintly inked. | {yokai} bows politely, spilling a little water from its dish, and goes back to the lantern riverbank. Ryo wakes at the province's edge, and one paper talisman still bears a faint stroke of ink. |
|  | **01-vibe/Story.md**<br>The yokai that won does not gloat and does not harm Ryo. |  |
|  | **01-vibe/Characters.md**<br>The grappler of the lantern riverbank. |  |
|  | **01-vibe/Characters.md**<br>Folklore reference: kappa are river spirits that love sumo and cucumbers and carry water in a dish on their heads; bow to one and it bows back, spilling the water. |  |

### Critic loop (`rules-lawyer`)

**Round 1 draft:** {yokai} bows politely, spilling a little water from its dish, and goes back to the lantern riverbank. You wake at the province's edge; your talismans are blank, all but one, faintly inked.

| Caught by | Kind | Quote | Problem | Evidence | Fix |
|---|---|---|---|---|---|
| code | form | you wake at the provinces edge your talismans are blank all but one | These 13 words in a row are copied from 01-vibe/Story.md; a card is new text, not a quoted example. | 01-vibe/Story.md | Say it in new words, with a different image. |

**Round 2 draft:** {yokai} bows politely, spilling a little water from its dish, and goes back to the lantern riverbank. Ryo wakes at the province's edge, and one paper talisman still bears a faint stroke of ink.

Code checks: clean. Critic: pass.

**Final (passed):** {yokai} bows politely, spilling a little water from its dish, and goes back to the lantern riverbank. Ryo wakes at the province's edge, and one paper talisman still bears a faint stroke of ink.

## Elder intro card: Nine-Tailed Kitsune

Brief: Write the one-line intro card shown on the map node of the Nine-Tailed Kitsune, before the duel starts.

### Notes loaded, lines cited, output

| Notes loaded by `habit-writer` | Line the writer cited | Output |
|---|---|---|
| 01-vibe/Tone.md<br>01-vibe/Story.md<br>01-vibe/Characters.md<br>02-mechanics/Yokai.md | **02-mechanics/Yokai.md**<br>\| Elder twist \| Reflects any special used twice in a row \| | Nine tails sway in the lantern light, and a trick shown to her twice in a row is handed straight back. |
|  | **01-vibe/Characters.md**<br>She wears a full white fox mask with red markings for the whole fight, and has three tails; the Nine-Tailed elder has nine. |  |

### Critic loop (`rules-lawyer`)

**Round 1 draft:** Nine tails sway in the lantern light, and a trick shown to her twice in a row is handed straight back.

Code checks: clean. Critic: pass.

**Final (passed):** Nine tails sway in the lantern light, and a trick shown to her twice in a row is handed straight back.

## Elder intro card: Elder Oni

Brief: Write the one-line intro card shown on the map node of the Elder Oni, before the duel starts.

### Notes loaded, lines cited, output

| Notes loaded by `habit-writer` | Line the writer cited | Output |
|---|---|---|
| 01-vibe/Tone.md<br>01-vibe/Story.md<br>01-vibe/Characters.md<br>02-mechanics/Yokai.md | **02-mechanics/Yokai.md**<br>\| Elder twist \| Armour on every special \| | Snow drifts past the shrine gate as blows land on the Elder Oni, and its iron club swings on through without a flinch. |
|  | **02-mechanics/Yokai.md**<br>\| Habit the twist breaks \| Mashing jabs to interrupt it stops working \| |  |
|  | **01-vibe/Characters.md**<br>The rushdown yokai of the snowbound mountain shrine gate. |  |
|  | **01-vibe/Characters.md**<br>Proud and blunt, it dashes in and swings an iron club. |  |

### Critic loop (`rules-lawyer`)

**Round 1 draft:** Snow gathers on the shrine gate as the Elder Oni swings its iron club, and no jab ever turns a special aside.

| Caught by | Kind | Quote | Problem | Evidence | Fix |
|---|---|---|---|---|---|
| critic | tone_drift | no jab ever turns a special aside | The card states the armour rule in game-system terms ("jab", "special") instead of showing what a person standing at the shrine gate would see. | 01-vibe/Tone.md#Card form: A card does not explain a rule in system words. It does not say "health", "frames", "level" or "roguelite"; it says what a person standing there would see. | Keep the snow and iron club but show the twist as something seen, for example quick blows landing on the Elder Oni while its swing carries through without a flinch. |

**Round 2 draft:** Snow drifts past the shrine gate as blows land on the Elder Oni, and its iron club swings on through without a flinch.

Code checks: clean. Critic: pass.

**Final (passed):** Snow drifts past the shrine gate as blows land on the Elder Oni, and its iron club swings on through without a flinch.

## Elder intro card: Elder Kappa

Brief: Write the one-line intro card shown on the map node of the Elder Kappa, before the duel starts.

### Notes loaded, lines cited, output

| Notes loaded by `habit-writer` | Line the writer cited | Output |
|---|---|---|
| 01-vibe/Tone.md<br>01-vibe/Story.md<br>01-vibe/Characters.md<br>02-mechanics/Yokai.md | **01-vibe/Story.md**<br>\| Elder Kappa \| 5 or 8 \| Its throws can't be broken \| | The Elder Kappa bows politely at the lantern riverbank, and once its hands close on you, no struggle will pry them loose. |
|  | **01-vibe/Characters.md**<br>The grappler of the lantern riverbank. Formal, stubborn and polite, it walks straight into throw range. Its hit sparks are kappa teal. |  |

### Critic loop (`rules-lawyer`)

**Round 1 draft:** The Elder Kappa bows politely at the lantern riverbank, and once its hands close on you, no struggle will pry them loose.

Code checks: clean. Critic: pass.

**Final (passed):** The Elder Kappa bows politely at the lantern riverbank, and once its hands close on you, no struggle will pry them loose.

## Reward card text: Oni's Hide

Brief: Write the reward card text for the modifier Oni's Hide, offered by the Oni.

### Notes loaded, lines cited, output

| Notes loaded by `movesmith` | Line the writer cited | Output |
|---|---|---|
| 01-vibe/Tone.md<br>02-mechanics/Abilities.md<br>01-vibe/Characters.md | **02-mechanics/Abilities.md**<br>\| Oni's Hide \| Armour on frames 1–5 \| no \| | plain: Thick oni hide: the special you attach this to can't be interrupted early on.<br>frames: Armour on frames 1–5 |
|  | **02-mechanics/Abilities.md**<br>\| Oni's Hide \| Oni · modifier \| "Your uppercut can't be interrupted early on." \| MODIFIER \| |  |

### Critic loop (`rules-lawyer`)

**Round 1 draft:** plain: Thick oni hide: the special you attach this to can't be interrupted early on.<br>frames: Armour on frames 1–5

Code checks: clean. Critic: pass.

**Final (passed):** plain: Thick oni hide: the special you attach this to can't be interrupted early on.<br>frames: Armour on frames 1–5

## Reward card text: Iron Will

Brief: Write the reward card text for the modifier Iron Will, offered by the Oni.

### Notes loaded, lines cited, output

| Notes loaded by `movesmith` | Line the writer cited | Output |
|---|---|---|
| 01-vibe/Tone.md<br>02-mechanics/Abilities.md<br>01-vibe/Characters.md | **02-mechanics/Abilities.md**<br>\| Iron Will \| Can't be interrupted by projectiles \| no \| | plain: Proud as an oni with an iron club, this special can't be interrupted by projectiles.<br>frames: Can't be interrupted by projectiles |
|  | **02-mechanics/Abilities.md**<br>A modifier attaches to one special and changes how it plays without new animation (A10). The Oni offers these five. |  |

### Critic loop (`rules-lawyer`)

**Round 1 draft:** plain: Proud as an oni with an iron club, this special can't be interrupted by projectiles.<br>frames: Can't be interrupted by projectiles

Code checks: clean. Critic: pass.

**Final (passed):** plain: Proud as an oni with an iron club, this special can't be interrupted by projectiles.<br>frames: Can't be interrupted by projectiles

## Reward card text: Slippery Skin

Brief: Write the reward card text for the modifier Slippery Skin, offered by the Kappa.

### Notes loaded, lines cited, output

| Notes loaded by `movesmith` | Line the writer cited | Output |
|---|---|---|
| 01-vibe/Tone.md<br>02-mechanics/Abilities.md<br>01-vibe/Characters.md | **02-mechanics/Abilities.md**<br>\| Slippery Skin \| Throw-invulnerable during startup \| no \| | plain: Slick as a riverbank stone, your special can't be thrown while it's starting up.<br>frames: Throw-invulnerable during startup |
|  | **01-vibe/Tone.md**<br>- `frames` is the exact number from the ability table, with no flavour. |  |

### Critic loop (`rules-lawyer`)

**Round 1 draft:** plain: Slick as a riverbank stone, your special can't be thrown while it's starting up.<br>frames: Throw-invulnerable during startup

Code checks: clean. Critic: pass.

**Final (passed):** plain: Slick as a riverbank stone, your special can't be thrown while it's starting up.<br>frames: Throw-invulnerable during startup

## Reward card text: River Pull

Brief: Write the reward card text for the modifier River Pull, offered by the Kappa.

### Notes loaded, lines cited, output

| Notes loaded by `movesmith` | Line the writer cited | Output |
|---|---|---|
| 01-vibe/Tone.md<br>02-mechanics/Abilities.md<br>01-vibe/Characters.md | **02-mechanics/Abilities.md**<br>\| River Pull \| Pulls the opponent one step closer on hit \| no \| | plain: The river tugs at your foe: when the special it's attached to hits, the opponent is pulled one step closer.<br>frames: Pulls the opponent one step closer on hit |
|  | **02-mechanics/Abilities.md**<br>A modifier attaches to one special and changes how it plays without new animation (A10). The Kappa offers these four. |  |
|  | **01-vibe/Tone.md**<br>- `frames` is the exact number from the ability table, with no flavour. |  |

### Critic loop (`rules-lawyer`)

**Round 1 draft:** plain: The river tugs at your foe: when the special it's attached to hits, the opponent is pulled one step closer.<br>frames: Pulls the opponent one step closer on hit

Code checks: clean. Critic: pass.

**Final (passed):** plain: The river tugs at your foe: when the special it's attached to hits, the opponent is pulled one step closer.<br>frames: Pulls the opponent one step closer on hit
