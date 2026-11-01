# Yokai Fighters — Written Rules

The canonical, citable rules distilled from `Yokai_Fighters_GDD_Extended.pdf` and
the approved changes in `docs/design/gdd-amendments.md` (amendments win over the PDF).
Every agent reads this file; **Rules Lawyer** rejects content by citing a rule ID
(e.g. "violates A7"). If this file and the PDF + amendments disagree, they win
and this file gets a fix ticket. All numbers are first-pass targets that the harness tunes; a
tuned value changes here first, with the harness evidence linked in the commit.

Items marked **Full game** are stretch goals and must not be built in the 5-week build.
Items marked **Extra (not MVP)** may be built only once every MVP item is done; they never delay MVP work and are the first thing dropped [AM17].

---

## P — Pillars (tie-breakers for any judgment call)

- **P1** Every upgrade changes how you fight: frame data, properties or combo routes, never damage alone.
- **P2** Your fighter is yours: the yokai hunted shape the build; moves change colour/effects as they level.
- **P3** Every choice costs something: limited slots, replaced moves lose levels, heal vs. train.
- **P4** Easy to start, deep to master: Kihon newcomers can win in run 1; **one-button specials never outperform manual inputs**.
- **P5** The game answers your habits: each elder adds one twist that breaks a common habit; the Tanuki copies your best move.
- **P6** Readable, fair fights: every yokai telegraphs, has one punishable habit, and reacts **only to what is on screen, never to button presses**. Harder yokai just react faster.

## C — Combat basics

- **C1** One round, 60 ticks/s, deterministic. Ryo has 1,000 health that carries across the run. **No round timer: a fight goes on until one fighter is KO'd** (designer, 2026-10-21; the GDD sets no timer, so this fills a gap). The harness's tick cap is only a safety limit; a capped fight is a stall, reported separately and not counted as a win or a loss.
- **C2** Movement: walk crosses the screen in ~2.5 s (measured on the original 1,920-unit screen: 12.8 units/tick; ~1.9 s across AM8's 1,440-unit screen); dash 18 frames; jump 40 frames airtime.
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
- **F2** Clip first: the Animator specs the move's clip, the designer approves the take, the Animator makes it (generator, motion capture or a bought pack), cleans it, measures it and may give it a retiming curve [AM1, AM5]; then Movesmith writes frame data from that clip's real timing as retimed. A retiming segment plays between 0.5× and 2× the clip's speed and a held pose lasts at most 4 frames (first pass). Frame data that doesn't match its clip is invalid.
- **F3** Animations step by exact frames via `AnimationPlayer.Seek()`. Hitboxes are 2D rectangles stored in move data, not physics shapes.
- **F4** Gameplay lives on a flat 2D plane (fixed Z); 3D is presentation only. Perspective camera, ~25° FOV.
- **F5** No hand-keyed animation [AM1, AM5]: clips come from a generator, motion capture of a real performance, or a bought motion-capture pack, plus scripted cleanup and retiming and Cascadeur's automatic physics pass; nobody poses or edits keys by hand. A move gets at most 3 takes across all sources; if none is usable, it becomes a mechanically equivalent move on an approved clip.
- **F6** Visual assets are generated, sound is sourced [AM1, AM5, AM7, AM16]. Generators: Tripo or Meshy, the designer's choice per job. Models: turnaround (a generator's 2D image model, or Astra run by the designer from Asset Smith's brief [AM7]) → Image-to-3D → auto-rig (the generator's or AccuRIG 2, whichever passes the Rig Specialist's check) → Blender check and extra bones → glTF into Godot. Every rig plays clips on one shared humanoid skeleton (Tripo's since 2026-10-19 [AM16]; the Meshy skeleton stays for rigs not yet rebuilt) through the Rig Specialist's bone map. Clips: generator library or prompt, video motion capture, or ActorCore packs; cleaned and retargeted in Blender by script, in place (no root motion), one clip shared by every fighter's rig. Stages: generated props over generated backdrops. UI/VFX textures and backdrops: generator 2D image models. Sound and music: bought packs. Agents write specs, run jobs and check results, but run only jobs, capture sessions and purchases the designer has approved at the approved cost.

## E — Engine defaults (designer-approved 2026-10-06; E1's screen and wall numbers changed by AM8, approved 2026-10-16)

Values the GDD leaves open, proposed by the YOK-15/16/17/18 code and accepted by the designer. Tunable like every other number here.

- **E1** Scale: 200 units per metre; the camera's view is 1,060 units wide close in and 1,540 units (7.7 m) at the widest zoom, which is the sim's screen [AM9; AM8 had a fixed 1,440, before it 1,920]; in a tall-body fight (V7) the presentation view may widen to about 1,900 units, while the sim's screen stays 1,540 [AM10, YOK-133, designer 2026-10-17]; the stage is 3,840 units wide (two of the old 1,920-unit screens); fighters are at most 1,060 units apart (screen walls, so both rigged models, up to 240 units ahead of and 200 behind their position, stay in frame at the widest zoom whichever way each faces: 1,060 + 2 × 240 = 1,540) [AM9; 960 under AM8, 1,520 from YOK-39, 1,600 before]. The camera (~25° FOV, F4) zooms with separation (V7), follows the fighters' midpoint and stops where the cornered fighter's model (up to 240 units ahead, 200 behind) meets the screen edge.
- **E2** KO slow-down (V4): the world plays the 30 frames over 60 real ticks; inputs are ignored meanwhile.
- **E3** Every yokai has 1,000 health until the yokai rules give its own figure.
- **E4** Standard knockdown (C4): 40 frames.
- **E5** Default pushback when a move's data gives none: 40 units on hit, 50 on block. Hurtbox when a move's data gives none: standing 90 × 320, crouching 90 × 240, airborne 90 × 270 starting 40 units above the feet (x -45, y 40), measured from the rigged models in YOK-33 hitbox alignment (designer accepted, YOK-39).
- **E6** Blocking: crouch-blocking also blocks mids (there are no overheads). No chip damage.
- **E7** Input timing: a motion must finish within 16 ticks of its first direction; a recognised command waits 5 ticks to be used; a charge needs 40 ticks held, with 8 ticks of grace.
- **E8** Overlapping motions: when two motions complete in the window, the most recently completed one wins; ties go 623 > 236 > 214 > 22. Charge is a held button (`HeldTicks`); `[4]6` / `[2]8` are recognised but in no Kata slot.
- **E9** Default controls. Keyboard P1: WASD, U/I/O punches, J/K/L kicks, Space Special. Keyboard P2: arrows, numpad 4/5/6 punches, 1/2/3 kicks, 0 Special. Pad: d-pad or left stick (0.5 deadzone), X/Y/RB punches, A/B/RT kicks, LB Special.
- **E10** Movement (C2): forward dash 300 units, back dash 240; dash by double tap within 12 ticks. Jump height 250 units, sideways drift 8 units per frame, no pre-jump frames.
- **E11** Jumping: holding up jumps again on landing; up + a button gives the ground normal. Ryo gets jump-in (air) normals; the data format needs an air flag for them.
- **E12** Throw input (C4): LP+LK pressed within 3 ticks of each other, in Kata and Kihon; if the first button already started a normal, the throw cancels it while it is still in startup, otherwise the throw input is ignored. A grab beats a strike active on the same frame, but a faster strike beats the throw's startup. Throw vs throw on the same frame clash: automatic break, no damage. A strike's invulnerability does not avoid throws (a burst's invulnerability does). After a break both fighters are freed and pushed apart; no mash penalty. A throw's recovery must outlast its break window. A landed throw builds meter and causes hitstop as a heavy hit (12 frames, shakes).
- **E13** Burst input (C6): LP+MP+HP within 3 ticks (keyboard U+I+O / numpad 4+5+6, pad X+Y+RB). The burst pushes the opponent back 200 units. A thrown fighter cannot burst; it breaks instead.
- **E14** Meter (C5 reading): attacker +6 on hit, +3 when blocked; defender +3 per hit taken, nothing for blocking. Hitstop (V2) also applies on block. Meter resets to 0 each fight; only health carries over (C1).
- **E15** Generated rig limits: strikes are open-handed (the Meshy rig has no finger bones; designer accepted, no code-posed fists). The generic throw clip is the Meshy "Step_Forward_and_Push" (preset 259) take, head-trimmed from tick 30, with its forward step in move data: an F5 substitute after three library grabs failed.
- **E16** EX specials (C5): the special's motion + any two punches or any two kicks, the second button up to 2 ticks late (the special turns into EX in place); costs the special's meter from data (1 bar). LP+LK stays the throw (E12) and wins. Without enough meter the plain version comes out and nothing is spent.
- **E17** Levels and projectiles: Lv 3 keeps the Lv 2 step and adds its evolution. One projectile per fighter on screen unless the move's data allows more; a second input gives the button's normal instead. Equal projectiles cancel out; a projectile with more hits beats a weaker one and keeps its remaining hits. Throws ignore throw-invulnerability data (E12). Slippery Skin is not throw-invulnerability: it breaks a throw (AM12).
- **E18** Kihon (K2): Special + direction picks slots A–D (neutral A, forward B, back C, down D; down-diagonals D, up-diagonals as their horizontal, up A); Special beats jump. K1 motions also work in Kihon, at 100% damage. Kihon EX: Special + direction + one extra punch or kick within 2 ticks. An attack pressed before Special gives its normal. Both players default to Kihon in the demo; F4 toggles P1's scheme in debug builds.
- **E19** Air normals (E11): any punch gives Ryo's air punch, any kick his air kick; one per jump; touchdown ends the move into 3 landing frames (per-move override), during which the fighter can't block and can be thrown. Facing is fixed for the whole jump.
- **E20** Reward draft (A2–A4, A7, A12): percentage effects round to the nearest whole number, so Fox's Patience's +25% meter on block turns the 3 of C5 into 4. The UPGRADE card names one owned special (chosen by the run's seed); the player picks only a MODIFIER's target. A special that already carries a modifier can't take another. If the pool can't fill three cards, extra NEW or MODIFIER cards fill in, otherwise fewer cards show. Card text lives in each special's or modifier's `card` block; there is no separate `data/cards/` folder.
- **E21** Lv 2 steps (A4, designer decision YOK-41): Spirit Wave recovers 4 frames faster; Rising Talisman recovers 6 frames faster. Both are relative to the clip-derived Lv 1 recovery. Damage-only steps are out (P1). Every other special gets one step: movesmith proposes one step per remaining special and the designer approves it (proposal in progress on branch YOK-36-lv2-steps; decided 2026-10-20).
- **E22** Behaviour-profile AI (Y2, Y4, P6): the AI sees a read-only snapshot of the screen, its own fighter included, delayed by its reaction frames (apprentice 30, yokai 22, elders 18, Tanuki 15). Habits are data: trigger → action → chance, and `tell_cover_pct` hides the tell some of the time (a per-tier difficulty knob, default 0). First-pass spacing: close under 250 units, mid under 600; neutral spacing weight 40; walk plans 15 ticks, block plans 20. Standing-condition habits re-roll at most once per 60 view ticks (approved 2026-10-21). `GuardTicks` 10: how long an AI that opts into `guard_pct` keeps guarding after its own move's recovery (YOK-165; the guard ships off, `DefaultGuardPct` 0). The standard-tier AI doesn't use EX or burst yet.
- **E23** Demo flow (YOK-39): each new run draws a new reward-draft seed (duel n uses seed + n, so a replay of the run offers the same cards). A debug temperament choice (F9 or `--temperament=`) is sticky: it stays in effect across later nodes, and across restart, until changed. Replaying a finished duel with R is debug-only, so players can't skip the reward. The "demo complete" text is a story card.
- **E24** Wake-up (C4, E4): after the 40-frame knockdown the fighter has a 28-frame wake-up: invulnerable, can't be thrown, can't act. The model rolls onto its front over the first 10 frames, then plays the get-up. Total down time is 68 frames. A held jump leaves on the first actionable frame (the Kitsune's habit, Y2).

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
| River Grab | Kappa, common | 6/2/30 | 130 | Command grab (generic throw); counts as a throw (C4, R7) for trials and cancels | Whirlpool Grab: +50% range |
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
| Slippery Skin | Kappa | Automatically breaks a throw attempted during this move's startup: a clash, no damage, like throw vs throw [AM12] |
| River Pull | Kappa | Pulls opponent one step closer on hit |
| Cucumber Gift ◆ | Kappa | Heals 2% max health on hit |
| Sumo Stance ◆ | Kappa | Hold the button before the special: the longer the hold, the faster its dash, up to a maximum speed [AM11] |

## Y — Yokai

- **Y1** In-game AI is a deterministic C# state machine reading behaviour profiles. 8 profiles: 3 yokai × 2 temperaments + the apprentice + Tanuki [AM13].
- **Y2** Each yokai: two temperaments and exactly one learnable, punishable habit. The Tanuki's habit: it throws after one blocked hit, the first blockstun it sees (60%) [AM4, AM14]. A habit whose trigger is a standing condition fires when the condition becomes true, then may re-roll at most once every 60 view ticks while it holds. A fired habit always comes out, even if the AI is hit before it can act; the only exceptions are a jump answer replacing a standing habit plan (Y4) and the round or fight ending (YOK-177).

  | Yokai | Aggressive | Patient | Habit to punish | Elder twist |
  |---|---|---|---|---|
  | Kitsune (zoner) | Foxfire at mid range while advancing | Holds full screen, anti-airs jumps | Often jumps after a knockdown | Nine-Tailed: reflects any special used twice in a row |
  | Oni (rushdown) | Dashes in constantly | Walks in, punishes whiffs with charge | Dashes in after blocking a projectile | Elder Oni: armour on every special |
  | Kappa (grappler) | Walks straight into throw range | Blocks, throws after two blocked hits | Throws after blocking two hits in a row | Elder Kappa: throws can't be broken |

- **Y3** Each elder twist shows as a map-node icon, a one-line intro card, and a callout on first meeting.
- **Y4** Difficulty curve:

  | Opponent | Reaction | Damage dealt per duel (newcomer / average) |
  |---|---|---|
  | Apprentice (row 1) | 30f | 110 / 80 (half damage per hit) |
  | Yokai (rows 2, 3, 6) | 22f | 220 / 160 |
  | Elder (rows 5, 8) | 18f | 280 / 200 |
  | Tanuki (boss) | 15f | 350 / 250 |

  The damage-per-duel figures are harness targets only, not rules the game enforces. Half damage (apprentice, and the Y6 guided first fight) floors per hit. Enemy jump punishing scales by tier, from per-tier data: apprentice and yokai punish a jump 50% of the time; elders and the Tanuki 100%. The opponent AI may guard pre-emptively. The test bot's reactive Block follows cross-ups, with a new R5 baseline. The bot has several skill levels across runs, and `idle_guard` depends on the level. A habit counts as punished by an unblocked Ryo hit while the habit's plan runs, or within the next 3 hits exchanged after it ends. Blocked connects count toward the 3; a landed throw counts once; throw breaks and clashes don't count. A KO tallies the full hit's damage (YOK-169). **Exchange ratio** (here and in R5) means hits: unblocked hits landed ÷ unblocked hits taken (harness, YOK-179). Tier `jump_punish_pct` values (absolute chance; a late anti-air counts): apprentice 50, yokai 50, elder 100, Tanuki 100. (Decided 2026-10-20: YOK-165, 166, 169, 170; 2026-10-21: punish window, exchange ratio.)

- **Y5** Each Tanuki win (max 3) makes yokai react 1 frame faster and pushes them toward aggressive.
- **Y6** The apprentice fight is guided (and at half damage, Y4): prompts teach blocking, throw, throw break, meter, burst and one cancel.

## T — The Tanuki copy rule (never cut)

- **T0** Fights with Ryo's normals + starters + one copied special, using Ryo's model and clips (no new animation). The copy sits in the slot it has on Ryo's side and replaces a starter there; the other starter stays in its own slot [AM4].
- **T1** Lock point: when Ryo enters the Tanuki's node.
- **T2** Target: highest-level special currently in a slot, throws included. Sold/replaced moves don't count.
- **T3** Ties: most recently levelled, then lower slot.
- **T4** Strength: at Ryo's level, **never** his modifiers or evolution. An evolved Lv 3 move always beats its copy.
- **T5** Always defined: the merchant won't release Ryo's last special.
- **T6** Visible all run outside fights: the map shows the target talisman glowing; the rest-stop forecast names it, its level and expected cost. Expected cost is the copied move's damage per hit at Ryo's level, read from data; it is always available, so there is no "not enough fights" state (decided 2026-10-20) [AM15]. In fights the HUD shows the talisman **only in the Tanuki fight** (the locked copy, T1); no yokai or elder fight shows it [AM6].

## R — The run

- **R1** 8 rows, one node per row. Rows 1–3, 5, 6, 8 duels; 5 and 8 always elders; row 4 always merchant; row 7 merchant or dojo; row 1 apprentice. Tanuki after row 8.
- **R2** Each node shows which yokai waits there. The map is a horizontal handscroll with the walked route painted as a brush stroke; it shows only cleared nodes, Ryo's node and the nodes he can choose next, so later rows, the Tanuki included, stay hidden until their turn [AM18].
- **R3** Binding a yokai restores 50 health.
- **R4** Arrival-health targets: newcomer heal@4 + rest@7 ≈ 520; heal@4 + train@7 ≈ 370; shop@4 + rest@7 ≈ 270 (short of boss cost); average shop@4 + train@7 ≈ 490.
- **R5** Harness target: 200 simulated first runs by a newcomer-profile Kihon bot → 50–70% reach the Tanuki, 40–60% of those beat it. Calibrated against outside playtests (end of weeks 2 and 4).
- **R6** Merchant: free heal 25% of max health **or** shop. The shop shows items on a physical shelf, 3 per shelf; a bought item rolls up in place; the merchant has no idle line. Beating an elder counts as binding its yokai for the shop (YOK-178). Elders are named "Elder <yokai>" (e.g. "Elder Oni") on the binding card and screens. Shop sells common abilities from yokai not bound this run (50 coins): common specials and common modifiers alike; a bought modifier attaches to a special the player picks, by the usual attach rule (A2) [AM4]. Release a binding: Ryo pays 40 [AM4]. Bought abilities are grey "borrowed ink" and never carry over. Never sells rares or cancel rules.
- **R7** Dojo: rest (heal 30% of max health) **or** training trial. Trial heals 15% either way, levels the special if landed within 3 attempts. Trials are hand-authored and Kihon-completable.
- **R8** Coins: 25 per duel on rows 1–6, 50 for the row-5 elder; the row-8 elder pays a rare ability instead. No cap on coins held, for now [AM4].
- **R9** Target run length 25–40 minutes for a first-time player.

## M — Between runs

- **M1** Unlocks by depth: row ≤5 → 0; rows 6–8 → 1; reached Tanuki → 2; Tanuki win → 4. Awarded at run end in a fixed order.
- **M2** 12 locked abilities take roughly 4–6 runs.
- **M3** Best special carried to next run at Lv 2 if the run reached row 6 or further.
- **M4** Story progress has its own save flag, independent of unlocks. No post-game mode.
- **M5** The game saves only at node boundaries. Quitting inside a node and choosing Continue restarts that node with the health, coins and slots Ryo entered it with; a fight, heal, rest or pick not completed with the node is not kept. Allowed by design, not an exploit [AM3]. The route pick stays committed (R1).

## S — Story and tone

- **S1** Tone: mischievous folklore with a melancholy edge, never horror. Yokai are tricksters and lonely spirits, not monsters.
- **S2** Cards are one or two sentences.
- **S3** Card schedule: intro (first run: Master Sōen, "name the debt"); every binding: "Forgive me, {yokai}. I'll set your spirit free."; row 4: "That {move} of yours... I'd love one." (templated to copy target); first loss: Tanuki names the move it would have stolen; later losses: variants by row reached and winning yokai; boss node: merchant drops disguise; Win 1: why the Tanuki collects; Win 2: talismans are cages too; Win 3: Ryo binds the Tanuki and releases every yokai — "I said I'd set you free." [AM2]
- **S4** Templated cards must use the actual move/yokai names from the player's run.
- **S5** The Oni is the guardian of the snowbound shrine gate (V9). The illusion has bent his post into a gate no one may pass; once freed, he goes back to guarding it. Proud and blunt, a guardian and never a menace: Oni cards show him holding his ground or returning to his post, never as a demon to be cast out (designer, 2026-10-25).
- **S6** **Extra (not MVP)** [AM17]: the first time the player meets each yokai, a short video in the story-card style plays before the duel (paper, ink and brush, V1), carrying at most two sentences (S2). It plays once per yokai per save, recorded with story progress (M4). The S3 schedule the build ships with does not depend on it.

## V — Presentation and feel

- **V1** Toon-shaded 3D, ink outlines, muted ukiyo-e palette (persimmon, indigo, rice paper, pine). UI = paper talismans, brush strokes, red seals. Generated assets [AM1] are made to this look by prompt and unified by one toon shader and outline pass; no baked PBR detail or painterly texture.
- **V2** Hitstop: light 6, medium 9, heavy 12 frames; counterhit +4.
- **V3** Screen shake: heavy hits and EX only, 2–4 px for 6 frames. Never on lights.
- **V4** Round-ending blow: 30 frames at half speed, then the ink-stroke binding.
- **V5** Hit sparks: foxfire orange, oni red, kappa teal, Ryo ink black.
- **V6** Every hit layers impact thud + short yokai sting, timed to the impact frame.
- **V7** Camera zooms with the fighters' separation [AM9; was "steady"]: eased with no snapping, a fixed step per sim tick, both models always in frame; chest height (1.2 m) with a slight pitch (0.5° down close in to 3° up at the widest zoom, feet ~90% down the frame); rises partway on jumps and never puts the higher fighter's head under the HUD; stops at the stage edge. In a tall-body fight (a body whose highest point, props included, passes 1.9 m: the Oni's club) it frames that point under the HUD, may sit above 1.2 m while that floor binds, rises up to 0.25 m per tick, and keeps the lower fighter's feet no lower than 1,040 px of 1,080 [AM10, YOK-133, designer 2026-10-17]. The slow push-ins on the Tanuki reveal and the round-ending blow layer on top. Presentation only: the sim never reads it.
- **V8** Moves change colour/effects as they level (level presets).
- **V9** Stages: bamboo grove at dusk (Kitsune), snowbound shrine gate (Oni), lantern riverbank (Kappa), Tanuki's tea house with season-flickering screens. Music: shamisen, shakuhachi, taiko, intensity rising by row; Tanuki theme detunes the merchant's melody.
- **V10** Screens (GDD §8): title (start, continue, practice mode, settings), control select, map, fight HUD, reward cards, rest stop with the Tanuki forecast, results, story cards. Practice mode reuses the dojo scene with a training dummy, so it needs no new screen. Which trials it offers is an open question below.

## G — Scope

- **G1** Never cut: the copy rule, the merchant, Kihon, the harness, the story cards.
- **G2** Scope gate (end of week 1): cut Kata, then the meta-unlock layer (carry-over and story stay).
- **G3** Balance gate (mid week 3): modifiers 14 → 8 (six unlocked stay, locked drop to 2), then dojo trials, then colour-only presets (status 2026-10-21: no cuts yet; the batch-3 bugs YOK-177, 178 and 179 are fixed and rerun as batch 4, YOK-185, before the decision; the newcomer playtest, YOK-50, calibrates whether the bot or the fights get retuned; fights go to KO, C1).
- **G4** Week-1 retarget go/no-go: generated walk, heavy and throw clips must play on each yokai's auto-rig without clipping tails/shell/club; failing yokai use their folklore human guise.
- **G5** Schedule: W1 move list, fighters generated and auto-rigged, retarget gate, sound packs, combat core, both schemes, AI framework, test bot. W2 33 abilities, 8 profiles [AM13], Tanuki + copy rule, map/merchant/dojo/unlocks with placeholder screens, whole-run harness, first playtest. W3 final screens/HUD, story cards, integration, balance gate, feature freeze. W4–5 polish only.

## Open questions (designer decides; do not invent answers)

- Generation credit spend and sound/music spend.
- Generator plan tier (commercial licence for generated output).
- Designer hours for approving generation jobs (the GDD budgets 10 h for purchases and gates).
- Whether practice mode unlocks trials for every special or only owned ones.
- Whether Ryo may leave a rest stop without choosing, and whether a heal at full health should be offered (R6, R7).
- The shop's item count per visit: 9 (3 shelves of 3) proposed, 6 as a balance alternative; balance decides (R6). How the items are picked.
- What the Tanuki does when it copies a charged special such as Sumo Stance (AM11, T4).
- In the Tanuki fight, whether the player is shown Ryo's level for the copied move or the strength the Tanuki copies it at (T4, T6).
- The first-meeting video (S6, AM17): its length; whether it can be skipped; whether elders, the Tanuki and the row-1 apprentice count; how it is made (generated video, which needs a tool and spend, or an animated card in the engine); whether it may land before the week-3 feature freeze (G5).
