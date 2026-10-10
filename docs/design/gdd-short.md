# Yokai Fighters

> Markdown transcription of Yokai_Fighters_GDD_Short.pdf. The PDF plus docs/design/gdd-amendments.md remain the source of truth; if this file disagrees with the PDF, the PDF wins and this file gets fixed.

<!-- page 1 -->

**Readable GDD — Standard Boss Edition**

## 1. Executive Summary

### 1.1 The game

Yokai Fighters is a single-player 2.5D roguelite fighting game for PC. You play Ryo, an earnest, stubborn apprentice exorcist who binds yokai rather than destroying them, apologising to each one he seals into a talisman. Each yokai he binds lends him one of three abilities drawn from its power: beat a Kitsune and you might bind its Foxfire. The yokai you choose to fight decide the fighter you become.

Tone: mischievous folklore with a melancholy edge, never horror. Platform: PC. Run length: 25–40 minutes.

### 1.2 Core loop

The map has 8 rows, and you pick one node per row: 6 duels (including two elders) and 2 rest stops. Win a one-round duel, bind one of three abilities from that yokai, and move on. After row 8, the Tanuki waits.

```text
                                                        health hits 0
Choose a node on the map        Fight the yokai                          Run over
8 rows, one node per row        one round; health carries over           moveset lost

next node            CORE LOOP            win                            lose
                 (once per map node)

Merchant or dojo                 next     Draft 1 of 3 abilities          Boss: Tanuki
rows 4 and 7: heal, shop, train           drawn from the yokai you beat   illusion tricks
                                                        map cleared
                                                                         win
                                                                         Run won
                                                                         may unlock more
```

*Figure 1. The Yokai Fighters game loop.*

Win: defeat the Tanuki in a seventh duel. Loss: health carries between duels; at zero, the run ends.

### 1.3 Design pillars

1. Every upgrade changes how you fight. Abilities change frame data, move properties and combo routes, not just damage. When Foxfire evolves into Piercing Foxfire, it cuts through the Kitsune's own projectiles, which changes how you win that matchup.
2. Your fighter is yours. The yokai you hunt shape the fighter you become: chase Kappa and you finish a grappler, chase Kitsune and you finish a zoner. Moves also change colour and effects as they level, so your build shows on screen.
3. Every choice costs something. Picking one path means skipping another yokai's powers. Slots are limited, a replaced move loses its levels, and dojos make you choose between healing and training.
4. Easy to start, deep to master. Kihon controls and a guided apprentice fight that teaches blocking, throws and throw breaks, meter, burst and a cancel let newcomers win fights in their first run. Kata motion inputs, frame traps and self-discovered cancel routes give veterans depth, and one-button specials never outperform manual inputs.
5. The game answers your habits. A strong build should never win on autopilot, meaning repeating one safe pattern without reading the opponent. Each elder yokai adds one rule twist that breaks a common habit: an elder Oni has armour on every special, so mashing jabs to interrupt it stops working, and a Nine-Tailed Kitsune reflects any special used twice in a row. The Tanuki, the final test, mixes every archetype's tricks, so no single habit carries a run to the end.
6. Readable, fair fights. Every yokai telegraphs its attacks and has one habit to learn and punish. Yokai only react to what's on screen, never to your button presses directly, and harder yokai simply react faster, so every loss comes back to a decision the player made.

### 1.4 Audience and story

Primary audience: roguelite players curious about fighting games. Secondary: fighting-game fans wanting deep solo content, served by the Kata bonus, cancel depth, a frame-data toggle and a practice mode; if Kata is cut, cancel depth alone, an accepted trade. Inspirations: Hades, Slay the Spire, Street Fighter 6.

<!-- page 2 -->

Build purpose: the 5-week build is the capstone's assessed deliverable, a complete, polished one-act game, not a retail release. Success means a first-time player can finish a full run in 25–40 minutes and the Tanuki is beatable with every archetype.

Story. The province is the Tanuki's game. The yokai are real spirits trapped in its illusion and made to fight again each loop, which is why Ryo apologises. Disguised as the merchant, it helps Ryo bind more yokai, because every talisman he fills is one more it plans to take. After a loss, Ryo wakes with blank talismans except one that stays faintly inked (the carry-over).

Story beats. Every run that reaches the boss sees the merchant unmasked as the Tanuki, so the reveal lands in a first run; the first loss shows a wake-up card in which a cheerful voice hints that the merchant isn't what he seems. Wins 1–3 show why the Tanuki collects yokai, that Ryo's talismans are cages too, and finally Ryo binding it and releasing every yokai. Ryo apologises because his master taught that binding borrows a spirit's strength ("Forgive me, Kitsune. I'll give it back."). Story progress is saved separately from unlocks; there is no post-game mode.

## 2. Game Mechanics

### 2.1 Fighting

Ryo walks, dashes, jumps, blocks by holding back, throws, and uses six normal attacks. He starts every run with two Lv 1 specials: Spirit Wave (a projectile) and Rising Talisman (an anti-air). Meter powers EX specials, a once-per-fight burst breaks an opponent's combo, and counterhits reward reading the opponent.

Row 1 is always an apprentice duel (slow reactions, half damage) that counts as one of the six; on the first run only, it adds skippable prompts teaching blocking, throwing, breaking a throw (the apprentice tries one), meter, burst and one cancel.

### 2.2 Control schemes

Kata ("form") uses six buttons and motion inputs, such as ↓↘→ + punch, and earns a +10% precision bonus on motion-input specials. Kihon ("basics") adds a Special button: press it with a direction for a one-press special at full damage.

### 2.3 Abilities and levels

- Slots: four special slots (two start empty), one modifier per special, and up to two cancel rules.
- Levels belong to the move: anything newly drafted starts at Lv 1.
- Guaranteed upgrades: one of every three offers is always an upgrade to a move you own, starters included.
- Evolutions: every special evolves at Lv 3. Foxfire, for example, becomes Piercing Foxfire.
- Cancels: a cancel skips a move's recovery by starting another the instant the first hits. Cancel rules add paths such as "heavy normals cancel into specials"; each move can be cancelled into once per combo, so nothing loops.

### 2.4 The yokai

Each yokai comes in two temperaments, aggressive and patient, so repeat fights play differently. Every path passes two elders, whose offers always include a cancel rule. Each elder's twist shows as a map icon, a one-line intro card and a first-time callout. Rare abilities (cancel rules and each yokai's second special) come only from elders; everything else is common.

| Yokai | Archetype | How it fights | Elder twist |
|---|---|---|---|
| Kitsune (fox spirit) | Zoner | Keeps distance with foxfire | Nine-Tailed Kitsune reflects any special used twice in a row |
| Oni (ogre) | Rushdown | Relentless close pressure | Elder Oni has armour on every special |
| Kappa (river imp) | Grappler | Punishes blocking with sumo throws | Elder Kappa's throws can't be broken |

Difficulty curve. Reactions tighten through the run (apprentice 30 frames, yokai 22, elders 18, Tanuki 15), and each Tanuki win, up to three, makes yokai 1 frame faster and more aggressive as the illusion grows stronger.

### 2.5 The Tanuki

The Tanuki is a boss with its own fixed moveset, drawn from tanuki folklore. It mixes every archetype's tricks, so no single habit or build beats it, and it fights with the boss's 15-frame reactions.

<!-- page 3 -->

| Move | What it does | Counterplay |
|---|---|---|
| Leaf Shift | Places a leaf on its head and teleports behind Ryo | Watch for the leaf; block the other way |
| Belly Drum | Close-range shockwave that pushes Ryo to the corner | Stay out of range or jump it |
| Teakettle Guard | Turns into a teakettle (the Bunbuku Chagama tale) and parries the next hit | Throw it instead; parries don't stop throws |
| Illusion Rush | A dash that splits into two decoys; only one is real | The real one casts a shadow |

### 2.6 Health and tuning

A run is exactly 6 duels (the row-1 apprentice included) plus the Tanuki, and rows 4 and 7 are always rest stops. Ryo has 1,000 health, binding a yokai restores 50, and wins pay 25 coins per duel on rows 1–6 and 50 for the row-5 elder (the row-8 elder pays a rare ability instead), so every coin can be spent at a rest stop.

| Duel cost | Apprentice (row 1) | Yokai (rows 2, 3, 6) | Elder (rows 5, 8) | Tanuki |
|---|---|---|---|---|
| Newcomer | 110 | 220 | 280 | 350 |
| Average player | 80 | 160 | 200 | 250 |

| Route to the boss | Health on arrival |
|---|---|
| Newcomer: heal at row 4, rest at row 7 | about 520 |
| Newcomer: heal at row 4, train at row 7 | about 370 |
| Newcomer: shop at row 4, rest at row 7 | about 270, short of the boss's cost |
| Average player: shop at row 4, train at row 7 | about 490 |

At each rest stop, a forecast shows the Tanuki's expected cost from the player's own fights, so the risk is visible. Targets are fixed: across 200 simulated first runs by a newcomer-profile Kihon bot, 50–70% reach the Tanuki and 40–60% of those beat it.

### 2.7 Rest stops and coins

| Stop | Where | What it offers |
|---|---|---|
| Merchant | Always at row 4; one option at row 7 | Choose one per visit: a free heal (25%), or shopping, where coins buy common abilities from yokai you haven't bound this run (50) or release a binding to free a slot (40). Bought abilities show in grey borrowed ink and never carry over. Never sells rares or cancel rules. |
| Dojo | The other option at row 7 | Choose one: rest to heal 30%, or a training trial, a hand-authored combo that heals 15% either way and levels a special if landed within 3 attempts. |

### 2.8 Between runs

12 of the 33 abilities start locked: each yokai's second special, 8 modifiers (2 if the balance gate cuts modifiers to 8) and 1 cancel rule. Evolutions come with their special and are never locked. Unlocks are awarded at the end of a run by depth reached, so pushing on always beats dying on purpose. The carry-over is the one exception to new moves starting at Lv 1.

| Run reaches | Row 5 or earlier | Rows 6–8 | The Tanuki | Tanuki win |
|---|---|---|---|---|
| Unlocks | 0 | 1 | 2 | 4 |
| Best special carried at Lv 2 | No | Yes | Yes | Yes |

## 3. AI Architecture

The AI agents are development tools: an orchestrator leading three teams. Each is described by what the player sees because of it; section 4.1 gives each one's development role.

| Team | Agent | What the player sees |
|---|---|---|
| Orchestrator | Producer | Balance fixes, such as a Piercing Foxfire nerf, actually reach the game. |
| Content | Movesmith | Rewards that change how a move plays, not just its damage. |
| Content | Habit Writer | Yokai with learnable habits, and a story that names the player's own moves. |
| Content | Rules Lawyer | Abilities that always fit their yokai: a Kappa never offers a projectile. |
| Content | Sparring Partner | Difficulty that was measured across whole runs, not assumed. |
| Asset | Asset Scout | Ryo and every yokai look and sound like one world. |
| Asset | Clip Matcher | Hits land, look and sound right on the frame they connect, and each level's look shows the build on screen. |
| Build | Gameplay Programmer | Every yokai and the Tanuki fight, and every number is bot-tested. |
| Build | UI Designer | Cards and meters that stay readable mid-combo. |

<!-- page 4 (begins mid-table above, at the Habit Writer row) -->

How they work together. Clip Matcher picks each clip first, so frame data comes from real timing, and a debug overlay draws hitboxes for the daily spot-check. Content runs as a pipeline with a gatekeeper: Rules Lawyer returns broken drafts to their author with a reason. The harness checks trials and cancel loops at runtime. Data merges after schema, Rules Lawyer and harness checks. Code runs a closed build-and-test loop through the Godot MCP on its own branch and merges only with designer approval.

## 4. Technical Strategy

### 4.1 Agent roles

| Agent | Development role (one sentence) |
|---|---|
| Producer | Creates tickets for outstanding work and starts the matching agent workflow. |
| Movesmith | Writes the move list, then each ability's frame data from its matched clip's real timing, plus hitboxes, card text and dojo trial. |
| Habit Writer | Writes each yokai's temperament profiles and all story-card text, to a stated tone. |
| Rules Lawyer | Rejects any content file that breaks the written game rules, with a reason. |
| Sparring Partner | Reads whole-run bot results to find overpowered builds and missed health targets. |
| Asset Scout | Shortlists model, animation, sound and music packs for Ryo and the yokai against the move list. |
| Clip Matcher | Picks each move's clip first, then adds its level presets and frame-timed sound cues. |
| Gameplay Programmer | Writes C# systems, fight AI and the whole-run test bot, built and tested through the Godot MCP. |
| UI Designer | Builds each game screen and the fight HUD directly in Godot through the MCP. |

### 4.2 What ships

| Item | Count | Makeup |
|---|---|---|
| Specials | 8 | 2 starters + 2 per yokai |
| Evolutions | 8 | One per special; data-only, reusing its clip with a visual preset |
| Modifiers | 14 | Data-only |
| Cancel rules | 3 | Elder offers only |
| Abilities | 33 | 12 start locked: 3 specials, 8 modifiers, 1 cancel rule |
| Behaviour profiles | 7 | 3 yokai × 2 temperaments + Tanuki |
| Tanuki moveset | 4 | Boss specials, not draftable |
| Screens / story cards | 8 / ~20 | Title, controls, map, HUD, reward, rest stop, results, story; intros, merchant lines, wake-up variants, 3 win cards |
| Code tickets | 85 | Combat 20, AI 12, run 20, harness 10, UI 10, save 4, overlay and practice 3, pipeline 6 |

### 4.3 Engine and assets

Built in Godot 4 (.NET) with C#, in 2.5D: 3D fighters and stages, with all gameplay on a 2D plane under a narrow-FOV camera. Assets are pre-bought and unified by a toon shader. The Tanuki's model and clips come from the purchased packs, like every yokai. All throws are generic: the attacker plays a grab clip and the defender its standard knockdown, so no paired throw animations are needed.

<!-- page 5 -->

Week 1 go/no-go gate: walk, heavy and throw clips must retarget onto each yokai without clipping tails, shell or club; a yokai that fails uses its folklore human guise.

### 4.4 Token budget

Code and UI revisions are priced as 3-turn MCP loops (change, build, fix).

| Agent | Calls | Tokens per call (in + out) | Total |
|---|---|---|---|
| Producer | 3 runs a day × 35 = 105 | 10,000 + 2,000 | 1,260,000 |
| Movesmith | 37 × 3 = 111 | 3,000 + 800 | 421,800 |
| Rules Lawyer | 37 × 3 = 111 | 4,000 + 300 | 477,300 |
| Habit Writer | 28 profile + 14 story = 42 | 4,000 + 1,500 | 231,000 |
| Sparring Partner | 4 weeks × 5 = 20 | 20,000 + 2,000 | 440,000 |
| Asset Scout | 45 searches | 15,000 + 1,500 | 742,500 |
| Clip Matcher | 31 clip + 12 cue batches | 6,000 + 2,000; 5,000 + 2,000 | 332,000 |
| Gameplay Programmer | 85 × 4 revisions × 3 turns = 1,020 | 18,000 avg + 3,000 | 21,420,000 |
| UI Designer | 8 × 5 revisions × 3 turns = 120 | 10,000 + 4,000 | 1,680,000 |
| Total | 1,617 calls | ~27.0M; ceiling 40.5M with 50% contingency | |

Spend is front-loaded: about 4.5M in week 1, 10.1M in week 2, 8M in week 3 and 2.2M in each polish week. Programmer context is modelled growing from 10k to 25k tokens, kept down by a codebase map and pulling only relevant files. If a week runs over, simple tickets move to a smaller Claude model; Sparring Partner is protected. Designer time: 210 hours (6 a day, 7 days a week): about 60 code review, 60 playtesting, 35 rules, 25 spot-checks, 10 purchases and gate, 20 buffer. Code is batched into one daily review queue; outside playtests with 3–5 fighting-game newcomers end weeks 2 and 4.

### 4.5 Constraints

A deterministic 60-tick loop. Frame data only means something if a frame is always exactly one step. Animations are stepped by exact frames with AnimationPlayer.Seek(), and hitboxes are 2D rectangles in move data rather than physics shapes, so whole-run bot tests are repeatable. API: Claude, with prompt caching, the Message Batches API for overnight content and web search for the scouts.

### 4.6 Schedule and scope

| Week | Milestone (5 weeks to a polished build) |
|---|---|
| 1 | Move list, retargeting gate, packs bought, open rules settled; combat core, both schemes, AI framework, test bot. Scope gate. |
| 2 | 33 abilities, 7 profiles, the Tanuki and its moveset; map, merchant, dojo and unlock logic with basic placeholder screens; whole-run harness; first outside playtest. |
| 3 | Final screens and HUD, story cards, integration; balance gate mid-week. Feature freeze. |
| 4–5 | Polish only: second outside playtest, balance, audio, effects, bug fixing. |

Scope gate (end of week 1, on velocity): cut Kata, then the meta-unlock layer (carry-over and story stay). Balance gate (mid-week 3, on test data): modifiers 14 to 8, then dojo trials, then colour-only presets. Never cut: the Tanuki, merchant, Kihon, harness, story cards.
