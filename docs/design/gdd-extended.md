# Yokai Fighters

> Markdown transcription of Yokai_Fighters_GDD_Extended.pdf. The PDF plus docs/design/gdd-amendments.md remain the source of truth; if this file disagrees with the PDF, the PDF wins and this file gets fixed.

<!-- page 1 -->

**Extended Game Design Document**

This is the long-form companion to the 5-page readable GDD. It goes deeper into how the game feels, how every system works, and where the full game goes after the 5-week build. Anything marked Full game is a stretch goal outside the assessed build; everything else is must-ship. All numbers are first-pass targets that the test harness and playtests will tune.

## 1. Vision

### 1.1 The pitch

Yokai Fighters is a single-player 2.5D roguelite fighting game for PC. You play Ryo, an earnest, stubborn apprentice exorcist who binds yokai rather than destroying them, apologising to each one he seals into a talisman. Every yokai he binds lends him one of three abilities drawn from its power, so the yokai you choose to fight decide the fighter you become. At the end of every run waits the Tanuki, a shapeshifter that copies the move you have invested in most.

### 1.2 A run in practice

You pick Kihon controls, because motion inputs still feel foreign. The first duel is an apprentice Kitsune, slow to react and only half as dangerous, and on-screen prompts walk you through blocking, throwing, breaking a throw, meter, burst and one cancel. You win, the Kitsune is sealed into a paper talisman with an ink stroke, and Ryo murmurs: "Forgive me, Kitsune. I'll give it back." Three cards slide up. You take Foxfire.

At row 2 the map forks between an Oni and a Kappa. You want projectiles, so you avoid the grappler and fight the Oni, whose relentless pressure teaches you to block. At row 4 a cheerful travelling merchant offers you a choice: a free heal, or coins for a new ability. You are at 600 health, so you heal. As you leave, he grins: "That Foxfire of yours... I'd love one." You don't think much of it.

The Nine-Tailed Kitsune at row 5 reflects any special used twice in a row, so you learn to mix Foxfire with your anti-air instead of spamming it. It offers a cancel rule, Fox Step, which lets your specials cancel into a dash, and suddenly you're chasing your own fireballs across the screen. At the row-7 dojo you gamble on a training trial, land it on the second attempt, and Foxfire reaches Lv 3, evolving into Piercing Foxfire.

At the Tanuki's node the merchant drops his disguise. The rest-stop forecast had warned you: the Tanuki will copy Foxfire at Lv 3, without your evolution or modifiers. It throws plain Foxfire; yours pierce straight through it. You win with 140 health left, and a story card reveals why the Tanuki collects techniques. Next run, your talismans are blank again, all but one: Piercing Foxfire stays faintly inked, carried forward at Lv 2.

### 1.3 Design pillars

1. Every upgrade changes how you fight. Abilities change frame data, move properties and combo routes, not just damage. When Foxfire evolves into Piercing Foxfire, it cuts through the Kitsune's own projectiles, which changes how you win that matchup.
2. Your fighter is yours. The yokai you hunt shape the fighter you become: chase Kappa and you finish a grappler, chase Kitsune and you finish a zoner. Moves also change colour and effects as they level, so your build shows on screen.
3. Every choice costs something. Picking one path means skipping another yokai's powers. Slots are limited, a replaced move loses its levels, and dojos make you choose between healing and training.
<!-- page 2 -->
4. Easy to start, deep to master. Kihon controls and a guided apprentice fight that teaches blocking, throws and throw breaks, meter, burst and a cancel let newcomers win fights in their first run. Kata motion inputs, frame traps and self-discovered cancel routes give veterans depth, and one-button specials never outperform manual inputs.
5. The game answers your habits. A strong build should never win on autopilot, meaning repeating one safe pattern without reading the opponent. Each elder yokai adds one rule twist that breaks a common habit: an elder Oni has armour on every special, so mashing jabs to interrupt it stops working, and a Nine-Tailed Kitsune reflects any special used twice in a row. The Tanuki copies your highest-level special at full level but never your modifiers or evolution: it steals the move, not the bond behind it, so the move you invested in is the one you must out-think.
6. Readable, fair fights. Every yokai telegraphs its attacks and has one habit to learn and punish. Yokai only react to what's on screen, never to your button presses directly, and harder yokai simply react faster, so every loss comes back to a decision the player made.

### 1.4 Audience, inspirations and build purpose

- Primary audience: roguelite players curious about fighting games. Kihon controls, a guided first fight, readable yokai habits and the rest-stop forecast exist for them.
- Secondary audience: fighting-game fans wanting deep solo content, served by the Kata precision bonus, cancel-rule depth, a frame-data toggle and a practice mode. If the scope gate cuts Kata, they are served by cancel depth alone, an accepted trade.
- Inspirations: Hades (run structure and story told across runs), Slay the Spire (drafting and branching maps), Street Fighter 6 (2.5D presentation and dual control schemes).
- Build purpose: the 5-week build is the capstone's assessed deliverable, a complete, polished one-act game, not a retail release. Success means a first-time player can finish a full run in 25–40 minutes and the copy rule works in every build.

### 1.5 Core loop

The map has 8 rows and you pick one node per row: 6 duels (including two elders) and 2 rest stops. Win a one-round duel, bind one of three abilities from that yokai, and move on. After row 8, the Tanuki waits. Health carries between duels; at zero, the run ends.

```text
                                                        health hits 0
Choose a node on the map        Fight the yokai                          Run over
8 rows, one node per row        one round; health carries over           moveset lost

next node            CORE LOOP            win                            lose
                 (once per map node)

Merchant or dojo                 next     Draft 1 of 3 abilities          Boss: Tanuki
rows 4 and 7: heal, shop, train           drawn from the yokai you beat   copies your moves
                                                        map cleared
                                                                         win
                                                                         Run won
                                                                         may unlock more
```

*Figure 1. The core loop.*

<!-- page 3 -->

## 2. World and story

### 2.1 Setting, tone and art direction

A rural Japanese province at the end of autumn, folded into an illusion by the Tanuki. Tone: mischievous folklore with a melancholy edge, never horror. Yokai are tricksters and lonely spirits, not monsters, and every victory carries a small cost.

- Look: toon-shaded 3D with ink outlines and a muted ukiyo-e palette of persimmon, indigo, rice paper and pine. UI elements are paper talismans, brush strokes and red seals.
- Stages: one per yokai. A bamboo grove at dusk (Kitsune), a snowbound mountain shrine gate (Oni), a riverbank of floating lanterns (Kappa), and the Tanuki's tea house, whose paper screens flicker between seasons.
- Music: shamisen, shakuhachi and taiko, layered so intensity rises row by row. The Tanuki's theme borrows the merchant's cheerful melody and slowly detunes it.

### 2.2 Characters

| Character | Role | Voice and motive |
|---|---|---|
| Ryo | Player character, apprentice exorcist | Earnest and stubborn. His master taught him that binding borrows a spirit's strength and a debt must be named, so he apologises to every yokai he binds. |
| Master Sōen | Ryo's teacher, seen only in the intro card | Patient and wry. Her rule, "name the debt", is the line the ending pays off. |
| The merchant / the Tanuki | Rest-stop trader; final boss | Cheerful, generous and quietly hungry. It collects techniques the way others collect teacups, and helps Ryo grow so there is more to steal. |
| The yokai | Opponents and sources of power | Real spirits trapped in the illusion and made to fight again each loop, which is why Ryo's apologies matter. |

### 2.3 Story structure

The story is delivered through short cards, one or two sentences each, so it never interrupts the run for long. Story progress has its own save flag, independent of unlocks, and there is no post-game mode.

| When | Card |
|---|---|
| First run, before row 1 | Intro: Master Sōen's rule, "name the debt". |
| Every binding | Ryo's templated apology: "Forgive me, {yokai}. I'll give it back." |
| Row 4, merchant still disguised | "That {move} of yours... I'd love one." (templated to the current copy target) |
| First loss | Wake-up card: the Tanuki names the move it would have stolen, teaching the copy rule. |
| Later losses | Wake-up variants by row reached and by which yokai won. |
| Every run that reaches the boss | The merchant drops his disguise at the Tanuki's node. |
| Win 1 | Why the Tanuki collects techniques. |
| Win 2 | The Tanuki points out that Ryo's talismans are cages too. |
| Win 3 (ending) | Ryo binds the Tanuki and releases every yokai, his own included: "I said I'd give it back." |

<!-- page 4 -->

### 2.4 Sample cards

First loss. "You fall in the tea house, and the paper screens fold shut. Somewhere a cheerful voice says: 'Shame about that Foxfire. I'd have taken good care of it.' You wake at the province's edge. Your talismans are blank, all but one."

Win 2. "'You keep them in paper, I keep them in my sleeves,' the Tanuki laughs. 'Which of us is the collector, little exorcist?' Ryo has no answer yet."

## 3. Combat

### 3.1 Presentation: 2.5D

Built in Godot 4 (.NET) with C#. Fighters and stages are 3D models, but every rule happens on a flat 2D plane, as Street Fighter has done since SF4. A perspective camera with a narrow field of view keeps spacing easy to read while bodies and stages still look solid.

```text
            3D stage: background set, lighting, parallax layers

                     movement: X (and jumps in Y)
                                                      2D gameplay plane (fixed Z)
                       Ryo             Kitsune

                                ~25° FOV

                      Camera3D, perspective
```

*Figure 2. Top-down sketch: gameplay locked to a plane in front of a 3D stage.*

### 3.2 Fight basics

Fights are one round at 60 frames per second. Ryo has 1,000 health, which carries across the whole run.

| System | Rule (first-pass target) |
|---|---|
| Movement | Walk crosses the screen in about 2.5 seconds; dash 18 frames; jump 40 frames of airtime. |
| Blocking | Hold back. Lows must be blocked crouching (the Oni Quake hits low). |
| Throws | Generic: attacker plays a grab clip and the defender a standard knockdown. Startup 5, break window 7 frames by pressing throw. 120 damage. |
| Meter | Three bars of 100. +6 per hit landed, +3 per hit blocked or taken. An EX special costs 1 bar. |
| Burst | Once per fight. Breaks a combo with 20 invulnerable frames; costs all current meter. |
| Counterhits | Hitting an opponent mid-attack deals +20% damage and +6 frames of hitstun. |

Ryo's normals (startup / active / recovery, damage):

<!-- page 5 -->

| Normal | Frames | Damage | Use |
|---|---|---|---|
| Light punch | 4 / 2 / 7 | 30 | Fastest button; interrupts pressure |
| Medium punch | 6 / 3 / 12 | 50 | Mid-range poke |
| Heavy punch | 10 / 4 / 20 | 80 | Big punish; anti-air if timed early |
| Light kick | 5 / 2 / 9 | 30 | Quick low |
| Medium kick | 7 / 3 / 14 | 55 | Longest-range poke |
| Heavy kick | 12 / 4 / 22 | 90 | Knockdown |

### 3.3 Control schemes

| Scheme | Specials | Damage | For |
|---|---|---|---|
| Kata ("form") | Six buttons and motion inputs: ↓↘→ (slot A), →↓↘ (B), ↓↙← (C), ↓↓ (D) | 100%, +10% precision bonus on motion-input specials | Players who want execution |
| Kihon ("basics") | Six buttons plus a Special button: neutral (A), forward (B), back (C), down (D) | 100% | Newcomers |

Combos and cancels work the same in both schemes. Kata is the first cut at the week-1 scope gate if the schedule slips; Kihon is never cut.

### 3.4 Frame data

Every move has startup (before it can hit), active (when it can hit) and recovery (when you are stuck finishing it). Clip Matcher picks each move's animation clip first, and Movesmith then writes frame data from that clip's real timing, so what you see always matches what the game resolves. A debug overlay draws hitboxes for the daily spot-check.

```text
                     cancel window, on hit

    Startup 15f      4f        Recovery 30f

0    5    10    15    20    25    30    35    40    45
```

*Foxfire, Lv 1 (first-pass targets): 15 startup, 4 active, 30 recovery. Gold marks the window where a cancel rule can chain.*

*Figure 3. Frame data for Foxfire at Lv 1.*

### 3.5 Cancels

A cancel skips a move's recovery by starting another the instant the first hits, so the two combo. Ryo can't cancel anything at the start of a run; cancel rules, offered only by elders, add specific paths. Each move can be cancelled into once per combo, so stacked rules never loop.

- Oni Chain (heavy normals cancel into specials): heavy punch → Iron-Club Charge, a 190-damage punish.
- Fox Step (specials cancel into dash): Foxfire → dash → light punch as the flame connects, keeping pressure on a zoner.
- Undertow (successful throws cancel into a special): River Grab → Shell Spin while the opponent is still rising.

<!-- page 6 -->

### 3.6 The fight screen

```text
                                    Foxfire Lv 2
RYO  ·  720 / 1000                  glowing: copy target              KITSUNE (patient)

                                        Foxfire

METER
                     BURST                         Elder twist: specials used twice are reflected
```

*Figure 4. Fight HUD mockup: health, meter, burst, the glowing copy target and the elder twist reminder.*

### 3.7 Game feel

Hits must feel heavy even with pre-bought animation, so most of the feel comes from timing, camera and sound layered on top.

| Feel lever | Rule |
|---|---|
| Hitstop | Light 6 frames, medium 9, heavy 12; counterhits add 4. Both fighters freeze, selling the impact. |
| Screen shake | Heavy hits and EX specials only; 2–4 pixels for 6 frames. Never on light hits. |
| Round-ending blow | 30 frames at half speed, then the binding: an ink stroke seals the yokai into a paper talisman. |
| Hit sparks | Coloured by source: foxfire orange, oni red, kappa teal, Ryo's own ink black. |
| Sound | Every hit layers an impact thud with a short yokai sting, timed by Clip Matcher to the impact frame. |
| Camera | Slow push-in on the Tanuki's reveal and on the round-ending blow; otherwise steady, so spacing stays readable. |

## 4. Abilities

### 4.1 Slots, levels and offers

- Slots: four special slots (two start filled with Ryo's starters), one modifier per special, and up to two cancel rules.
- Levels belong to the move: anything newly drafted starts at Lv 1. Drafting a special you own levels it up; at Lv 3 it evolves.
- Offers: after each duel, three cards from the yokai you beat. One is always an upgrade to a move you own, starters included.
- Rarity: rare means cancel rules and each yokai's second special, offered only by elders. Everything else is common.
- Locks: 12 of the 33 abilities start locked (marked ◆ below). Evolutions come with their special and are never locked.

<!-- page 7 -->

### 4.2 Specials

| Special | Source | Frames (S / A / R) | Dmg | Notes | Lv 3 evolution |
|---|---|---|---|---|---|
| Spirit Wave | Ryo starter | 13 / — / 30 | 60 | Projectile | Great Wave: absorbs one projectile |
| Rising Talisman | Ryo starter | 5 / 8 / 28 | 90 | Anti-air; invulnerable to air attacks on frames 1–5 | Heaven Seal: launches for a follow-up |
| Foxfire | Kitsune, common | 15 / 4 / 30 | 70 | Slow projectile | Piercing Foxfire: passes through projectiles |
| Fox Mirage ◆ | Kitsune, rare | 18 / — / 10 | 0 | Teleport behind the opponent | Mirage Feint: leaves a decoy that absorbs one hit |
| Iron-Club Charge | Oni, common | 16 / 6 / 24 | 110 | Forward charge with 1-hit armour | Crushing Charge: wall bounce |
| Oni Quake ◆ | Oni, rare | 22 / 5 / 26 | 90 | Ground pound; hits low | Aftershock: a second quake 30 frames later |
| River Grab | Kappa, common | 6 / 2 / 30 | 130 | Command grab (generic throw) | Whirlpool Grab: +50% range |
| Shell Spin ◆ | Kappa, rare | 10 / 18 / 20 | 80 | Spins through projectiles | Torrent Spin: travels forward, multi-hit |

Lv 2 gives each special one tuning step chosen per move, such as 4 fewer recovery frames or +10% damage.

### 4.3 Modifiers

Modifiers are data-only: they attach to one special and change how it plays without new animation. If the balance gate cuts modifiers to 8, the six unlocked ones stay and locked ones drop to 2.

| Modifier | Source | Effect on the special it's attached to |
|---|---|---|
| Will-o'-wisp | Kitsune | Projectiles travel 30% faster. |
| Fox's Patience | Kitsune | Builds 25% more meter on block. |
| Illusion Cloak ◆ | Kitsune | The first use each round can't be counterhit. |
| Trickster's Angle ◆ | Kitsune | Projectiles can be angled upward by holding up. |
| Ninefold Spark ◆ | Kitsune | The EX version costs half a bar. |
| Oni's Hide | Oni | Armour on frames 1–5. |
| Iron Will | Oni | Can't be interrupted by projectiles. |
| Rage Ember ◆ | Oni | +15% damage while Ryo is below 30% health. |
| Club Momentum ◆ | Oni | Pushes the opponent 50% further toward the corner. |
| Brute Recovery ◆ | Oni | 4 fewer recovery frames on block. |
| Slippery Skin | Kappa | Throw-invulnerable during startup. |
| River Pull | Kappa | Pulls the opponent one step closer on hit. |
| Cucumber Gift ◆ | Kappa | Heals 2% of max health on hit. |
| Sumo Stance ◆ | Kappa | Hold the button to charge for +20% damage. |

### 4.4 Cancel rules

| Cancel rule | Offered by | Path |
|---|---|---|
| Fox Step | Nine-Tailed Kitsune | Specials cancel into a dash. |
| Oni Chain | Elder Oni | Heavy normals cancel into specials. |
| Undertow ◆ | Elder Kappa | Successful throws cancel into a special. |

<!-- page 8 -->

### 4.5 The reward screen

```text
      crest                        crest                        crest

     Foxfire                  Rising Talisman                 Oni's Hide
  Kitsune · common           Upgrade · starter              Oni · modifier

Launch a slow fox flame.   Lv 1 → Lv 2: recovers 4       Your uppercut can't be
                              frames faster.              interrupted early on.

    NEW · Lv 1                    UPGRADE                      MODIFIER
```

*Reward screen: three cards, one always an upgrade. Plain language first; frame data behind a toggle.*

*Figure 5. Reward screen mockup after beating a Kitsune.*

## 5. Yokai

### 5.1 Profiles

In-game yokai are a deterministic C# state machine reading behaviour profiles that Habit Writer drafts. Each yokai has two temperaments, so repeat fights play differently, and one learnable habit that an attentive player can punish.

| Yokai | Aggressive temperament | Patient temperament | Habit to punish | Elder twist |
|---|---|---|---|---|
| Kitsune (zoner) | Throws foxfire at mid range while advancing | Holds full screen and anti-airs jumps | Often jumps after a knockdown | Nine-Tailed Kitsune reflects any special used twice in a row |
| Oni (rushdown) | Dashes in constantly | Walks in and punishes whiffs with its charge | Dashes in after blocking a projectile | Elder Oni has armour on every special |
| Kappa (grappler) | Walks straight into throw range | Blocks, then throws after two blocked hits | Throws after blocking two hits in a row | Elder Kappa's throws can't be broken |

Each elder's twist appears as an icon on its map node, a one-line intro card and a short callout the first time a player meets it.

### 5.2 Difficulty curve

| Opponent | Reaction time | Damage per duel (newcomer / average) |
|---|---|---|
| Apprentice (row 1) | 30 frames | 110 / 80 (half damage) |
| Yokai (rows 2, 3, 6) | 22 frames | 220 / 160 |
| Elder (rows 5, 8) | 18 frames | 280 / 200 |
| Tanuki (boss) | 15 frames | 350 / 250 |

Across runs, each Tanuki win, up to three, makes yokai react 1 frame faster and pushes them toward their aggressive temperament, framed as the illusion growing stronger. Yokai only react to what is on screen, never to button presses directly.

### 5.3 The Tanuki

The Tanuki fights with Ryo's normals and starters plus one copied special. The copy rule is never cut and is built in week 2.

1. Lock point: when Ryo enters the Tanuki's node.
<!-- page 9 -->
2. Target: his highest-level special in a slot, throws included. Sold and replaced moves don't count.
3. Ties: the one levelled most recently, then the lower slot.
4. Strength: at Ryo's level, but never his modifiers or evolution. It steals the move, not the bond, so an evolved Lv 3 move always beats its copy.
5. Always defined: the merchant won't release Ryo's last special.
6. Visible all run: the target's talisman glows in the HUD, and the rest-stop forecast names it.

When shapeshifted, the Tanuki uses Ryo's model and clips, so copying needs no new animation.

## 6. The run

### 6.1 The map

Eight rows, one node per row. Rows 1–3, 5, 6 and 8 are duels; rows 5 and 8 always hold elders; row 4 is always the merchant; row 7 offers the merchant again or a dojo. Each node shows which yokai waits there, so choosing a path is choosing which powers you can earn.

```text
                         The Tanuki (boss)

Row 8          ★ Elder Kappa             ★ Elder Oni

Row 7             Merchant                  Dojo

Row 6             Kitsune                   Kappa

Row 5           ★ Elder Oni               ★ Nine-Tails

Row 4                        Merchant

Row 3               Oni                     Kappa

Row 2      Kitsune             Oni                   Kappa

Row 1                       Apprentice              ★ elder (twist icon)   ■ rest stop
```

*Figure 6. Map sketch for one run. Lines show which nodes connect.*

### 6.2 Health and routes

Binding a yokai restores 50 health. The values below are tuned by the harness; the targets are fixed.

| Route to the boss | Health on arrival |
|---|---|
| Newcomer: heal at row 4, rest at row 7 | about 520 |
| Newcomer: heal at row 4, train at row 7 | about 370 |
| Newcomer: shop at row 4, rest at row 7 | about 270, short of the boss's cost |
| Average player: shop at row 4, train at row 7 | about 490 |

<!-- page 10 -->

Targets: across 200 simulated first runs by a newcomer-profile Kihon bot, calibrated against outside playtests, 50–70% reach the Tanuki and 40–60% of those beat it. At each rest stop, a forecast shows the Tanuki's copied move, its level and its expected cost from the player's own fights, so the risk is visible before choosing.

### 6.3 Rest stops

| Stop | Choose one | Details |
|---|---|---|
| Merchant | A free heal (25%), or shopping | Shopping: common abilities from yokai you haven't bound this run (50 coins); release a binding to free a slot (40). Bought abilities appear in grey borrowed ink and never carry over. Never sells rares or cancel rules. |
| Dojo | Rest (heal 30%), or a training trial | A trial is a hand-authored, Kihon-completable sequence for one of your specials. It heals 15% either way and levels the special if landed within 3 attempts. |

Sample trials: Spirit Wave: hit the dummy at mid range, then anti-air its jump with Rising Talisman. Iron-Club Charge: block the dummy's string, then punish with the charge before it recovers. River Grab: grab the dummy as it lands from a jump.

### 6.4 Coins

25 coins per duel on rows 1–6 and 50 for the row-5 elder; the row-8 elder pays a rare ability instead. That is about 75 coins at row 4 and 75 more by row 7, so every coin can be spent.

## 7. Between runs

| Run reaches | Row 5 or earlier | Rows 6–8 | The Tanuki | Tanuki win |
|---|---|---|---|---|
| Unlocks | 0 | 1 | 2 | 4 |
| Best special carried at Lv 2 | No | Yes | Yes | Yes |

- Unlocks are awarded at the end of a run, in a fixed order, so pushing on always beats dying on purpose. Twelve locked abilities take roughly 4–6 runs.
- The carry-over is the one exception to new moves starting at Lv 1. In the fiction, one talisman stays faintly inked.
- Story progress is saved separately, so the first reveal and the first-loss card never depend on unlocks.

## 8. Screens

| Screen | Purpose |
|---|---|
| Title | Start, continue, practice mode, settings. |
| Control select | Kata or Kihon, with one line explaining the precision bonus. |
| Map | Choose the next node; elder twist icons; the glowing copy-target talisman. |
| Fight HUD | Health, meter, burst, copy target, elder twist reminder (Figure 4). |
| Reward | Three cards; plain language first, frame data behind a toggle (Figure 5). |
| Rest stop | Merchant or dojo choice, with the Tanuki forecast. |
| Results | Depth reached, unlocks earned, the carried special. |
| Story card | Intro, wake-up, reveal and win cards. |

Practice mode reuses the dojo scene with a training dummy, so it needs no new screen. Basic placeholder versions of every screen exist by the end of week 2.

<!-- page 11 -->

## 9. AI architecture

The AI agents are development tools: an orchestrator leading three teams. None of them run in the shipped game.

| Agent | Development role | What the player sees |
|---|---|---|
| Producer | Creates tickets for outstanding work and starts the matching workflow. | Balance fixes, like a Piercing Foxfire nerf, actually reach the game. |
| Movesmith | Writes the move list, then frame data from each matched clip, hitboxes, card text and trials. | Rewards that change how a move plays. |
| Habit Writer | Writes temperament profiles and all story-card text to the tone line. | Learnable yokai, and a story that names your own moves. |
| Rules Lawyer | Rejects content files that break the written rules, with a reason. | A Kappa never offers a projectile. |
| Sparring Partner | Reads whole-run bot results. | Difficulty that was measured, not assumed. |
| Asset Scout | Shortlists model, animation, sound and music packs. | Ryo and the yokai look and sound like one world. |
| Clip Matcher | Picks each clip first, then adds level presets and frame-timed sound. | Hits land, look and sound right on the frame. |
| Gameplay Programmer | Writes C# systems, fight AI and the test bot through the Godot MCP. | Every yokai fights; every number is tested. |
| UI Designer | Builds every screen and the HUD in Godot through the MCP. | Cards and meters stay readable mid-combo. |

```text
                                   Producer
                               tickets + workflows

                                                                 fail: back with reason

Asset Scout          Clip Matcher           Movesmith            Rules Lawyer        Harness
packs shortlisted    clip, preset, sound    frame data from clip     gate            bot runs

             Gameplay Programmer            UI Designer             Designer review
               C# via Godot MCP           screens via MCP            merge to main
```

*Data merges after schema, Rules Lawyer and harness checks; code merges only after designer approval.*

*Figure 7. How work flows between agents.*

Content runs as a pipeline with a gatekeeper: Rules Lawyer returns broken drafts with a reason, and the harness checks trials, cancel loops and copied moves at runtime. Code runs a closed build-and-test loop through the MCP on its own branch and merges only with designer approval.

## 10. Technical strategy

### 10.1 Engine, assets and constraints

- Engine: Godot 4 (.NET) with C#. All assets are pre-bought and unified by a toon shader.
- Week 1 go/no-go gate: walk, heavy and throw clips must retarget onto each yokai without clipping tails, shell or club; a yokai that fails uses its folklore human guise.
<!-- page 12 -->
- Generic throws: no paired throw animations anywhere, so any special, throws included, can be copied.
- Constraint, a deterministic 60-tick loop: animations are stepped by exact frames with AnimationPlayer.Seek(), and hitboxes are 2D rectangles in move data, not physics shapes. Frame data is exactly true, and whole-run bot tests are repeatable.
- API: Claude, with prompt caching, the Message Batches API for overnight content and web search for the Asset Scout.

### 10.2 What ships

| Item | Count | Makeup |
|---|---|---|
| Specials / evolutions | 8 / 8 | 2 starters + 2 per yokai; one evolution each, data-only with a visual preset |
| Modifiers / cancel rules | 14 / 3 | Data-only; cancel rules from elders only |
| Abilities | 33 | 12 start locked: 3 specials, 8 modifiers, 1 cancel rule |
| Behaviour profiles | 7 | 3 yokai × 2 temperaments + Tanuki |
| Screens / story cards | 8 / ~20 | See sections 8 and 2.3 |
| Code tickets | 85 | Combat 20, AI 12, run 20, harness 10, UI 10, save 4, overlay and practice 3, pipeline 6 |

### 10.3 Token budget

| Agent | Calls | Tokens per call (in + out) | Total |
|---|---|---|---|
| Producer | 3 runs a day × 35 = 105 | 10,000 + 2,000 | 1,260,000 |
| Movesmith | 33 × 3 = 99 | 3,000 + 800 | 376,200 |
| Rules Lawyer | 33 × 3 = 99 | 4,000 + 300 | 425,700 |
| Habit Writer | 28 profile + 14 story = 42 | 4,000 + 1,500 | 231,000 |
| Sparring Partner | 4 weeks × 5 = 20 | 20,000 + 2,000 | 440,000 |
| Asset Scout | 45 searches | 15,000 + 1,500 | 742,500 |
| Clip Matcher | 30 clip + 12 cue batches | 6,000 + 2,000; 5,000 + 2,000 | 324,000 |
| Gameplay Programmer | 85 × 4 revisions × 3 turns = 1,020 | 18,000 avg + 3,000 | 21,420,000 |
| UI Designer | 8 × 5 revisions × 3 turns = 120 | 10,000 + 4,000 | 1,680,000 |
| Total | 1,592 calls | ~26.9M; ceiling 40.3M with 50% contingency | |

Spend is front-loaded: about 4.5M in week 1, 10M in week 2, 8M in week 3 and 2.2M in each polish week. The Programmer's context is modelled growing from about 10k to 25k tokens; a maintained codebase map keeps it down by pulling only relevant files. If a week runs over, simple tickets move to a smaller Claude model; Sparring Partner is protected. Designer time: 210 hours (6 a day, 7 days a week): about 60 code review, 60 playtesting, 35 rules, 25 spot-checks, 10 purchases and gate, 20 buffer, with code batched into one daily review queue.

### 10.4 Schedule and cut gates

| Week | Milestone |
|---|---|
| 1 | Move list, retargeting gate, packs bought, open rules settled. Combat core, both schemes, AI framework, test bot. Scope gate. |
| 2 | 33 abilities, 7 profiles, Tanuki and copy rule. Map, merchant, dojo and unlock logic with basic placeholder screens. Whole-run harness. First outside playtest. |
| 3 | Final screens and HUD, story cards, integration. Balance gate mid-week. Feature freeze. |
| 4–5 | Polish only: second outside playtest, balance, audio, effects, bug fixing. |

<!-- page 13 -->

- Scope gate (end of week 1, on actual velocity): cut Kata, then the meta-unlock layer (carry-over and story stay).
- Balance gate (mid-week 3, on harness and playtest data): modifiers 14 to 8, then dojo trials, then colour-only presets.
- Never cut: the copy rule, the merchant, Kihon, the harness and the story cards.

## 11. Full game: stretch goals

None of this is in the 5-week build. Each goal is described by what it adds to the feel of the game, then by what it would take.

| Goal | What it adds to the feel | Rough cost |
|---|---|---|
| Evolution choices | A real decision at Lv 3: two evolutions per special | Low: +8 data-only evolutions and presets |
| Supers | A cinematic three-bar finisher with a camera push | High: new animation per fighter |
| Shrines | Risk, folklore and surprise between duels | Medium: writing plus one screen |
| Jorōgumo and Tengu | Two new archetypes and four new matchups | Medium–high per yokai |
| Acts 2 and 3 | A longer journey with new regions and act bosses | High |
| Tetsu and Kaede | New starting kits that change how a run begins | High per character |
| Mastery | A reason to replay one character | Medium |
| Heat | Optional difficulty for players who have won | Low |
| Yūrei gauntlet | Fighting other players' winning builds | High: needs an online backend |

### 11.1 Evolution choices

At Lv 3, each special offers a choice of two evolutions instead of one, such as Twin Foxfire (two hits) or Piercing Foxfire (passes through projectiles). The moment should feel like choosing a path on the map: a commitment you can see in the very next fight. The Tanuki still never copies either evolution.

### 11.2 Supers

A three-bar finisher per fighter, with the camera pushing in and the stage dimming to ink. Supers would make meter a bigger strategic resource, but each needs bespoke animation, which is why they are out of the 5-week build.

### 11.3 Shrines

Shrines would replace some duel nodes with moments of risk and folklore. One of three appears at random:

- Offering shrine: give up a move or 20% of your health for a random rare ability, never a cancel rule.
- Folklore encounter: a short choice from yokai legend. Bow to a kappa and it bows back, spilling the water from the dish on its head, so the next Kappa starts at 25% less health. Refuse and you gain 20 coins.
- Cursed shrine: a strong ability with a drawback, such as Oni's Fury: specials deal 20% more damage, but you can't block for a second after using one. The merchant removes curses, which ties shrines to the economy.

<!-- page 14 -->

### 11.4 New yokai

| Yokai | Archetype | How it fights | Elder twist (proposed) |
|---|---|---|---|
| Jorōgumo (spider woman) | Counter | Sets web traps and waits for you to attack; parries | Webs slow every dash |
| Tengu (mountain spirit) | Trickster | Wind dashes and feather teleports; punishes weak anti-airs | Teleports after every blocked hit |

### 11.5 Acts 2 and 3

Two more regions, a mountain pass and the provincial capital, each with its own act boss, before the Tanuki becomes the final boss. The story's three win cards would spread across the acts, so each act ends on a beat.

### 11.6 New characters

| Character | Base kit | Starting specials |
|---|---|---|
| Tetsu, a wandering monk | Slow, long-reaching heavies; a grappler at heart | A command grab and an armoured charge |
| Kaede, a shrine maiden | Fast, short-range normals; a trickster | A thrown talisman and a short teleport |

Each character has their own normals, starting specials and a signature move pool weighted into their reward offers, while sharing universal rewards. Mastery is tracked per character and never carries over.

### 11.7 Mastery and Heat

Mastery: landing a move earns it mastery, which adds new evolutions and modifiers for that move to the reward pool. It adds options, never raw power. Heat: after a first win, optional modifiers make runs harder, such as yokai gaining meter twice as fast.

### 11.8 The Yūrei gauntlet

After the final boss, a gauntlet of three yūrei: ghostly copies of other players' winning builds, played back by their recorded habits. Ghosts are picked at random, but each must differ from your build and from each other, sharing at most two specials, where the pool allows. Because the fight loop is deterministic, ghost fights need no netcode, only an online store of builds and a leaderboard.

## 12. Risks and open questions

| Risk | Mitigation |
|---|---|
| Purchased packs lack a clip for a planned move | The move becomes a mechanically equivalent one on an approved clip; no bespoke animation. |
| Retargeting clips onto non-human yokai fails | Week-1 go/no-go gate, with a fallback to the yokai's folklore human guise. |
| Agent-written code hides subtle bugs | Closed build-and-test loop, own branch, designer approval for every merge. |
| The bot misjudges newcomer difficulty | Bot calibrated against outside newcomer playtests at the end of weeks 2 and 4. |
| Week 1 or week 3 overruns | Scope gate and balance gate, with a fixed cut order and a never-cut list. |

Open questions: the exact Lv 2 tuning step for each special; the asset spend; and whether practice mode should unlock trials for every special or only those you own.
