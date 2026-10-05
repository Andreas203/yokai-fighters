# Yokai Fighters — Written Rules

The canonical, citable rules distilled from `Yokai_Fighters_GDD_Extended.pdf` and
the approved changes in `docs/design/gdd-amendments.md` (amendments win over the PDF).
Every agent reads this file; **Rules Lawyer** rejects content by citing a rule ID
(e.g. "violates A7"). If this file and the PDF + amendments disagree, they win
and this file gets a fix ticket. All numbers are first-pass targets that the harness tunes; a
tuned value changes here first, with the harness evidence linked in the commit.

Items marked **Full game** are stretch goals and must not be built in the 5-week build.

---

## P — Pillars (tie-breakers for any judgment call)

- **P1** Every upgrade changes how you fight: frame data, properties or combo routes, never damage alone.
- **P2** Your fighter is yours: the yokai hunted shape the build; moves change colour/effects as they level.
- **P3** Every choice costs something: limited slots, replaced moves lose levels, heal vs. train.
- **P4** Easy to start, deep to master: Kihon newcomers can win in run 1; **one-button specials never outperform manual inputs**.
- **P5** The game answers your habits: each elder adds one twist that breaks a common habit; the Tanuki copies your best move.
- **P6** Readable, fair fights: every yokai telegraphs, has one punishable habit, and reacts **only to what is on screen, never to button presses**. Harder yokai just react faster.

## C — Combat basics

- **C1** One round, 60 ticks/s, deterministic. Ryo has 1,000 health that carries across the run.
- **C2** Movement: walk crosses the screen in ~2.5 s; dash 18 frames; jump 40 frames airtime.
- **C3** Block by holding back. Lows must be blocked crouching (Oni Quake hits low).
- **C4** Throws are generic: attacker plays a grab clip, defender a standard knockdown. Startup 5, break window 7 frames (press throw), 120 damage. **No paired throw animations anywhere.**
- **C5** Meter: 3 bars × 100. +6 per hit landed, +3 per hit blocked or taken. EX special costs 1 bar.
- **C6** Burst: once per fight, breaks a combo, 20 invulnerable frames, costs all current meter.
- **C7** Counterhit: +20% damage, +6 frames hitstun.
- **C8** Ryo's normals (startup / active / recovery, damage):

  | Normal | Frames | Dmg |
  |---|---|---|
  | Light punch | 4/2/7 | 30 |
  | Medium punch | 6/3/12 | 50 |
  | Heavy punch | 10/4/20 | 80 |
  | Light kick | 5/2/9 | 30 |
  | Medium kick | 7/3/14 | 55 |
  | Heavy kick | 12/4/22 | 90 (knockdown) |

## K — Controls

- **K1** Kata: six buttons + motions. Slot A ↓↘→, B →↓↘, C ↓↙←, D ↓↓. +10% precision bonus on motion-input specials.
- **K2** Kihon: six buttons + Special button. Neutral = A, forward = B, back = C, down = D. 100% damage.
- **K3** Combos and cancels behave identically in both schemes.
- **K4** Kata is the first cut at the scope gate. **Kihon is never cut.**

## F — Frame data and engine constraints

- **F1** Every move has startup, active and recovery frames.
- **F2** Clip first: Clip Matcher specs the move's clip, the designer generates it, Clip Matcher measures the approved take, then Movesmith writes frame data from that clip's real timing. Frame data that doesn't match its clip is invalid.
- **F3** Animations step by exact frames via `AnimationPlayer.Seek()`. Hitboxes are 2D rectangles stored in move data, not physics shapes.
- **F4** Gameplay lives on a flat 2D plane (fixed Z); 3D is presentation only. Perspective camera, ~25° FOV.
- **F5** No hand-keyed animation: every clip comes from the Meshy animation pipeline (F6). A move gets at most 3 generation takes; if none is usable, it becomes a mechanically equivalent move on an approved clip.
- **F6** Visual assets are generated, sound is sourced [AM1]. Models: image-model turnaround → Meshy Image-to-3D → Meshy auto-rig (shared humanoid skeleton) → glTF into Godot. Clips: Meshy animation library or prompt generation, in place (no root motion), one clip shared by every fighter's rig. Stages: generated props over generated backdrops. UI/VFX textures: image model. Sound and music: bought packs. The designer runs every generation job and owns the credit spend; agents write specs and check results.

## X — Cancels

- **X1** Ryo starts every run with no cancels. Cancel rules are rare and offered **only by elders**.
- **X2** Up to two cancel rules held at once.
- **X3** Each move can be cancelled into **once per combo**, so stacked rules never loop.
- **X4** The three rules:

  | Rule | Offered by | Path | Locked |
  |---|---|---|---|
  | Fox Step | Nine-Tailed Kitsune | Specials → dash | no |
  | Oni Chain | Elder Oni | Heavy normals → specials | no |
  | Undertow | Elder Kappa | Successful throws → a special | ◆ |

## A — Abilities

- **A1** Four special slots; two start filled with Ryo's starters (Spirit Wave, Rising Talisman).
- **A2** One modifier per special.
- **A3** Levels belong to the move. Newly drafted = Lv 1 (sole exception: M3 carry-over at Lv 2). Drafting an owned special levels it up.
- **A4** Lv 2 = one tuning step chosen per move (e.g. −4 recovery or +10% damage). Lv 3 = evolution.
- **A5** Evolutions are data-only with a visual preset; they come with their special and are never locked.
- **A6** A replaced or sold move loses its levels.
- **A7** Offers: after each duel, 3 cards **from the yokai beaten**. One is always an upgrade to an owned move (starters included).
- **A8** Source fidelity: a yokai only offers abilities sourced from itself (a Kappa never offers a projectile).
- **A9** Rare = cancel rules + each yokai's second special; offered only by elders. Everything else is common.
- **A10** Modifiers are data-only: no new animation.
- **A11** 33 abilities = 8 specials + 8 evolutions + 14 modifiers + 3 cancel rules. 12 start locked: 3 specials, 8 modifiers, 1 cancel rule (◆ below).
- **A12** Reward cards: plain language first, frame data behind a toggle.

### Specials (S / A / R, dmg)

| Special | Source | Frames | Dmg | Notes | Lv 3 evolution |
|---|---|---|---|---|---|
| Spirit Wave | Ryo starter | 13/—/30 | 60 | Projectile | Great Wave: absorbs one projectile |
| Rising Talisman | Ryo starter | 5/8/28 | 90 | Anti-air; air-invuln frames 1–5 | Heaven Seal: launches for a follow-up |
| Foxfire | Kitsune, common | 15/4/30 | 70 | Slow projectile | Piercing Foxfire: passes through projectiles |
| Fox Mirage ◆ | Kitsune, rare | 18/—/10 | 0 | Teleport behind opponent | Mirage Feint: decoy absorbs one hit |
| Iron-Club Charge | Oni, common | 16/6/24 | 110 | Forward charge, 1-hit armour | Crushing Charge: wall bounce |
| Oni Quake ◆ | Oni, rare | 22/5/26 | 90 | Ground pound, hits low | Aftershock: second quake 30f later |
| River Grab | Kappa, common | 6/2/30 | 130 | Command grab (generic throw) | Whirlpool Grab: +50% range |
| Shell Spin ◆ | Kappa, rare | 10/18/20 | 80 | Spins through projectiles | Torrent Spin: travels forward, multi-hit |

### Modifiers

| Modifier | Source | Effect |
|---|---|---|
| Will-o'-wisp | Kitsune | Projectiles travel 30% faster |
| Fox's Patience | Kitsune | +25% meter on block |
| Illusion Cloak ◆ | Kitsune | First use each round can't be counterhit |
| Trickster's Angle ◆ | Kitsune | Projectiles angle upward by holding up |
| Ninefold Spark ◆ | Kitsune | EX version costs half a bar |
| Oni's Hide | Oni | Armour on frames 1–5 |
| Iron Will | Oni | Can't be interrupted by projectiles |
| Rage Ember ◆ | Oni | +15% damage while Ryo < 30% health |
| Club Momentum ◆ | Oni | Pushes opponent 50% further toward the corner |
| Brute Recovery ◆ | Oni | −4 recovery frames on block |
| Slippery Skin | Kappa | Throw-invulnerable during startup |
| River Pull | Kappa | Pulls opponent one step closer on hit |
| Cucumber Gift ◆ | Kappa | Heals 2% max health on hit |
| Sumo Stance ◆ | Kappa | Hold to charge for +20% damage |

## Y — Yokai

- **Y1** In-game AI is a deterministic C# state machine reading behaviour profiles. 7 profiles: 3 yokai × 2 temperaments + Tanuki.
- **Y2** Each yokai: two temperaments and exactly one learnable, punishable habit.

  | Yokai | Aggressive | Patient | Habit to punish | Elder twist |
  |---|---|---|---|---|
  | Kitsune (zoner) | Foxfire at mid range while advancing | Holds full screen, anti-airs jumps | Often jumps after a knockdown | Nine-Tailed: reflects any special used twice in a row |
  | Oni (rushdown) | Dashes in constantly | Walks in, punishes whiffs with charge | Dashes in after blocking a projectile | Elder Oni: armour on every special |
  | Kappa (grappler) | Walks straight into throw range | Blocks, throws after two blocked hits | Throws after blocking two hits in a row | Elder Kappa: throws can't be broken |

- **Y3** Each elder twist shows as a map-node icon, a one-line intro card, and a callout on first meeting.
- **Y4** Difficulty curve:

  | Opponent | Reaction | Damage dealt per duel (newcomer / average) |
  |---|---|---|
  | Apprentice (row 1) | 30f | 110 / 80 (half damage) |
  | Yokai (rows 2, 3, 6) | 22f | 220 / 160 |
  | Elder (rows 5, 8) | 18f | 280 / 200 |
  | Tanuki (boss) | 15f | 350 / 250 |

- **Y5** Each Tanuki win (max 3) makes yokai react 1 frame faster and pushes them toward aggressive.
- **Y6** The apprentice fight is guided: prompts teach blocking, throw, throw break, meter, burst and one cancel.

## T — The Tanuki copy rule (never cut)

- **T0** Fights with Ryo's normals + starters + one copied special, using Ryo's model and clips (no new animation).
- **T1** Lock point: when Ryo enters the Tanuki's node.
- **T2** Target: highest-level special currently in a slot, throws included. Sold/replaced moves don't count.
- **T3** Ties: most recently levelled, then lower slot.
- **T4** Strength: at Ryo's level, **never** his modifiers or evolution. An evolved Lv 3 move always beats its copy.
- **T5** Always defined: the merchant won't release Ryo's last special.
- **T6** Visible all run: target talisman glows in the HUD; rest-stop forecast names it, its level and expected cost.

## R — The run

- **R1** 8 rows, one node per row. Rows 1–3, 5, 6, 8 duels; 5 and 8 always elders; row 4 always merchant; row 7 merchant or dojo; row 1 apprentice. Tanuki after row 8.
- **R2** Each node shows which yokai waits there.
- **R3** Binding a yokai restores 50 health.
- **R4** Arrival-health targets: newcomer heal@4 + rest@7 ≈ 520; heal@4 + train@7 ≈ 370; shop@4 + rest@7 ≈ 270 (short of boss cost); average shop@4 + train@7 ≈ 490.
- **R5** Harness target: 200 simulated first runs by a newcomer-profile Kihon bot → 50–70% reach the Tanuki, 40–60% of those beat it. Calibrated against outside playtests (end of weeks 2 and 4).
- **R6** Merchant: free heal 25% **or** shop. Shop sells common abilities from yokai not bound this run (50 coins); release a binding (40). Bought abilities are grey "borrowed ink" and never carry over. Never sells rares or cancel rules.
- **R7** Dojo: rest (heal 30%) **or** training trial. Trial heals 15% either way, levels the special if landed within 3 attempts. Trials are hand-authored and Kihon-completable.
- **R8** Coins: 25 per duel on rows 1–6, 50 for the row-5 elder; the row-8 elder pays a rare ability instead.
- **R9** Target run length 25–40 minutes for a first-time player.

## M — Between runs

- **M1** Unlocks by depth: row ≤5 → 0; rows 6–8 → 1; reached Tanuki → 2; Tanuki win → 4. Awarded at run end in a fixed order.
- **M2** 12 locked abilities take roughly 4–6 runs.
- **M3** Best special carried to next run at Lv 2 if the run reached row 6 or further.
- **M4** Story progress has its own save flag, independent of unlocks. No post-game mode.

## S — Story and tone

- **S1** Tone: mischievous folklore with a melancholy edge, never horror. Yokai are tricksters and lonely spirits, not monsters.
- **S2** Cards are one or two sentences.
- **S3** Card schedule: intro (first run: Master Sōen, "name the debt"); every binding: "Forgive me, {yokai}. I'll give it back."; row 4: "That {move} of yours... I'd love one." (templated to copy target); first loss: Tanuki names the move it would have stolen; later losses: variants by row reached and winning yokai; boss node: merchant drops disguise; Win 1: why the Tanuki collects; Win 2: talismans are cages too; Win 3: Ryo binds the Tanuki and releases every yokai — "I said I'd give it back."
- **S4** Templated cards must use the actual move/yokai names from the player's run.

## V — Presentation and feel

- **V1** Toon-shaded 3D, ink outlines, muted ukiyo-e palette (persimmon, indigo, rice paper, pine). UI = paper talismans, brush strokes, red seals. Generated assets are made to this look by prompt and unified by one toon shader and outline pass; no baked PBR detail or painterly texture.
- **V2** Hitstop: light 6, medium 9, heavy 12 frames; counterhit +4.
- **V3** Screen shake: heavy hits and EX only, 2–4 px for 6 frames. Never on lights.
- **V4** Round-ending blow: 30 frames at half speed, then the ink-stroke binding.
- **V5** Hit sparks: foxfire orange, oni red, kappa teal, Ryo ink black.
- **V6** Every hit layers impact thud + short yokai sting, timed to the impact frame.
- **V7** Camera steady except slow push-in on Tanuki reveal and round-ending blow.
- **V8** Moves change colour/effects as they level (level presets).
- **V9** Stages: bamboo grove at dusk (Kitsune), snowbound shrine gate (Oni), lantern riverbank (Kappa), Tanuki's tea house with season-flickering screens. Music: shamisen, shakuhachi, taiko, intensity rising by row; Tanuki theme detunes the merchant's melody.

## G — Scope

- **G1** Never cut: the copy rule, the merchant, Kihon, the harness, the story cards.
- **G2** Scope gate (end of week 1): cut Kata, then the meta-unlock layer (carry-over and story stay).
- **G3** Balance gate (mid week 3): modifiers 14 → 8 (six unlocked stay, locked drop to 2), then dojo trials, then colour-only presets.
- **G4** Week-1 retarget go/no-go: generated walk, heavy and throw clips must play on each yokai's auto-rig without clipping tails/shell/club; failing yokai use their folklore human guise.
- **G5** Schedule: W1 move list, fighters generated and auto-rigged, retarget gate, sound packs, combat core, both schemes, AI framework, test bot. W2 33 abilities, 7 profiles, Tanuki + copy rule, map/merchant/dojo/unlocks with placeholder screens, whole-run harness, first playtest. W3 final screens/HUD, story cards, integration, balance gate, feature freeze. W4–5 polish only.

## Open questions (designer decides; do not invent answers)

- Exact Lv 2 tuning step for each special.
- Generation credit spend and sound/music spend.
- Generator plan tier (commercial licence for generated output).
- Designer hours for running generation jobs (the GDD budgets 10 h for purchases and gates).
- Whether practice mode unlocks trials for every special or only owned ones.
