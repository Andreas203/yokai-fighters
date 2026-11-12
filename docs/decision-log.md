# Decision log

The append-only history of decisions: one line per decision, with date, ticket and who made the call. Agents load it as memory before planning. Built on 2026-10-13 from Linear comments and decision tickets, `docs/design/gdd-amendments.md`, `docs/design/rules.md` and git history. When this log disagrees with rules.md, the amendments or the PDF, the sources of truth in CLAUDE.md win and this log gets fixed.

This file records what was decided, not what is still open. Open questions and approvals waiting on the designer live in Linear: the tracker is YOK-189, and each question is a ticket with the `decision` label. `docs/process/change-tracking.md` says where each kind of change is recorded.

Milestones in Linear: W1 ends 10-10, W2 ends 10-17, W3 ends 10-24, W4–5 end 11-07 (first commit 2026-10-06).

## Design amendments (designer, they win over the PDF)
- **AM1** 2026-10-05: visuals are generated through the Meshy MCP and sound is sourced. Every job needs designer approval of the job and its credits. Cap of 3 takes per move, then an F5 substitute.
- **AM2** 2026-10-06: the binding line is "Forgive me, {yokai}. I'll set your spirit free." Win 3 says "I said I'd set you free."
- **AM3** 2026-10-11: saves happen only at node boundaries. Quitting mid-node restarts that node with its entry health, coins and slots. Allowed, not an exploit (M5).
- **AM4** 2026-10-11/12, *on PR #105, not merged yet*:
  - The Tanuki's copied special sits in Ryo's slot for it and replaces the starter in that slot (T0).
  - The Tanuki's habit is throwing at a blocking player, 60% of the time (Y2).
  - The shop sells common specials **and** modifiers at 50 each (R6), and releasing a binding costs Ryo 40.
  - Coins have no cap, for now (R8).
- **AM5** 2026-10-15: clip-matcher splits into `rig-specialist` and `animator`; level presets move to asset-smith. Generators are Tripo or Meshy (Tripo likely). Rigs try the generator auto-rig against AccuRIG 2. Blender cleanup by script, video mocap, ActorCore and Auto-Rig Pro allowed; Cascadeur only for its physics pass. One shared skeleton (Meshy's) with bone maps. Clips may carry retiming curves (F2), 0.5×–2× per segment, holds ≤4 frames (first pass).
- **AM6** 2026-10-15: the fight HUD shows the copy-target talisman only in the Tanuki fight, never in yokai or elder fights. The map talisman and the rest-stop forecast still show the target all run (T6). Copy rule unchanged.
- **AM7** 2026-10-16: fighter concept fronts and turnarounds may come from Astra (designer-run) instead of a generator's 2D model; the designer picks per character. Oni, Kappa and Tanuki use Astra (YOK-102). Rule F6, asset-smith.md.
- **AM8** 2026-10-15, approved 2026-10-16 by the designer (merge of PR #133, YOK-120): closer fixed camera, screen 1,920 → 1,440 units, screen walls 1,520 → 960 (960 + 2 × 240 = 1,440: a fighter facing away shows its 240 front reach outside; first try 1,040 clipped by up to 40, found by test-breaker; derived in SimConfig); stage stays 3,840; V7 steady camera and F4 ~25° FOV unchanged (camera moves closer); walk speed unchanged (~1.9 s across the new screen, C2). Values tunable in SimConfig.
- **AM9** 2026-10-16, approved by the designer 2026-10-16 (YOK-129, PR #145): Guilty Gear Strive-style dynamic camera, replacing AM8's fixed one. Eased zoom from 1,060 units (close; Ryo's stance ~52% of the screen height) to 1,540 (screen walls; ~36%). Camera at 1.2 m at every zoom, pitch 0.5° down (close) → 3° up (widest), so the feet sit ~88–90% down the frame (designer review 2026-10-16: was 1.2 → 1.4 m and 1° down, which left ~20% empty floor at the walls). Rises partway on jumps and never puts a head under the HUD. Walls 960 → 1,060. FOV 25° kept. V3 shake goes through the current width. V7 push-ins layer on top. Presentation only: StateHash changes only through the walls. Amends V7 and E1.
- **AM10** 2026-10-17, designer decision (YOK-133, after Code Critic): the camera frames the tallest body in the fight (Oni: club 3.124 m, framed for 3.15 m). In tall-body fights only: the presentation view may widen to ~1,900 units (sim screen stays 1,540), the feet line is 1,040 px (~96%), the camera may sit above 1.2 m while the HUD floor binds and rise up to 0.25 m per tick. Ryo vs Kitsune unchanged. Amends E1, V7 and AM9.
- **AM11** 2026-10-20 (YOK-142, YOK-171): Sumo Stance: the longer the hold, the faster its dash, up to a maximum speed; replaces "+20% damage".
- **AM12** 2026-10-20 (YOK-172): Slippery Skin automatically breaks a throw attempted during the carrying move's startup (a clash, no damage).
- **AM13** 2026-10-21 (YOK-174): the apprentice has its own behaviour profile, so there are 8 profiles (Y1, G5).
- **AM14** 2026-10-21 (YOK-177, PR #214): the Tanuki's throw habit triggers after one blocked hit, the first blockstun it sees (Y2).
- **AM15** 2026-10-20, written 2026-10-24 (YOK-173, YOK-196): the forecast's expected cost is the copied move's damage per hit at Ryo's level, from data, not from the player's own fights (T6).
- **AM16** 2026-10-19, written 2026-10-24 (YOK-146, YOK-196): Tripo's skeleton becomes the shared skeleton; the Meshy one stays for rigs not yet rebuilt (F6, amends AM5).
- **AM17** 2026-10-25 (designer, YOK-207): a short story-card style video plays the first time the player meets each yokai; extra, not MVP, built only once every MVP item is done (S6).
- **AM18** 2026-10-25 (designer, YOK-109, PR #245): the map is a horizontal handscroll with the walked route painted as a brush stroke; it shows only cleared nodes, Ryo's node and the next choice, so later rows and the Tanuki stay hidden until their turn (R2). Presentation only.
- **AM19** 2026-11-09 (designer, YOK-109, PR #273): the real routes ahead, to the Tanuki, show as sketched lines under mist; their nodes, names and icons stay hidden until their turn (R2, amends AM18). Presentation only.
- **AM20** 2026-11-09 (designer, YOK-238): the map no longer shows the copy-target talisman; outside fights only the rest-stop forecast names the target, its level and expected cost (T6, amends AM6 and AM18). The copy rule itself is unchanged.
- **AM21** 2026-11-09 (designer, YOK-151, PR #277): the Kappa's stage is a night swamp without lanterns, painted or 3D (V9). The stage id `riverbank` and its file names stay.
- **AM22** 2026-11-09 (designer, YOK-54): sound and music may be generated with ElevenLabs as well as bought, the designer's choice per job; new agent Sound Smith runs approved jobs through `tools/elevenlabs/el.py` under the approved credit cap (F6, amends AM1).

## Rules and tuning
- 2026-10-06 E1–E24: engine defaults approved by the designer. Max separation is 1,520 units (lowered from 1,600 in YOK-39), then 960 by AM8 (2026-10-16).
- 2026-10-06 YOK-41 / E21: at Lv 2, Spirit Wave recovers 4 frames faster and Rising Talisman 6 faster, both relative to the clip-derived Lv 1 recovery. Damage-only steps are out (P1).
- 2026-10-06 YOK-31 / E15: strikes are open-handed (the rig has no finger bones). The generic throw is the Meshy 259 `Step_Forward_and_Push` clip, head-trimmed from tick 30.
- 2026-10-11 YOK-87: a whole number written as `1e2` reads as 100. Save format is v2.
- 2026-10-16 YOK-57: elders and the Tanuki may use EX and burst from profile fields `ex_use` / `burst_use`, applied only in elder and Tanuki fights (the node decides; standard fights never spend, E22). Tanuki values (EX 40%, burst 50% from the 2nd hit) are still `proposed`; elder values not added.
- 2026-10-16 YOK-121: the HUD twist/copy strip is a scroll, shown for the first 10 s (600 ticks) of every fight, then it fades.
- 2026-10-16 YOK-128: save format v4 adds `run.node_paid`, so a won node never pays coins or heals twice after Continue (R3, R8). Paid state is not in `StateHash` yet (two probes pin that; designer call).
- 2026-10-17 YOK-98 (branch, not merged): dojo trial built. No save format change: the game saves only at node boundaries, so a trial is never in the file and a quit mid-trial restarts the node with attempts reset (M5); the save version stays 4 (the v5/v6/v7 order of #149, YOK-97, YOK-99 is untouched). Designer question: the dojo offers trials whose `status` is still `proposed` (all three shipped ones), with no status filter. Provisional, waiting on the designer: no trial for a special already at its top level; the card offers the first trial in slot order (no picker); the dummy is the dojo node's `opponent` (kitsune); a KO ends an attempt and the dummy starts each attempt at full health; combo steps are sampled on the completing hit; trial-fight damage never touches run health.
- 2026-10-20 YOK-159 shop redesign (designer):
  - The merchant speaks in speech bubbles, and the shop is a physical shelf with 2 items per shelf.
  - On hover or focus the merchant speaks the item's description: a lead-in plus the card's plain line, verbatim.
  - Frame data goes on a Tab/Y ledger slip and is never spoken.
  - Bubbles follow S2 (at most 2 sentences).
  - No copy hinting in the shop lines. The S3 row-4 story card keeps its line; the rules are unchanged.
  - Status: mockup rev 2 on `YOK-159-shop-shelf-mockup` awaits approval; the merchant lines on `YOK-159-merchant-shop-lines` have a Rules Lawyer PASS and need a schema and loader in the build.
- 2026-10-20 YOK-159 (designer): shop mockup rev 3 approved (commit 06d4d1a on `YOK-159-shop-shelf-mockup`: 3x3 wooden cabinet, hanging tags, ledger slip over the portrait). Build on `YOK-159-shop-shelf-build`: schema `merchant-shop-lines.schema.json` and loader `MerchantShopLines` for the Rules Lawyer-approved lines. Provisional, for the designer: shelf size and item picking stay open (R6, stock logic untouched); a buy is one Enter with no "Buy X for 50?" step; buying a special into an occupied slot is refused with the slot_full line (no replace prompt: it needs a `MerchantShop` replace action, a gameplay ticket); the bubble opens on the greeting and the focus line takes over on the first move.
- 2026-10-20 YOK-157 (designer):
  - Under a held idle guard the bot lets the first jump it sees in each fight go, and may anti-air later jumps.
  - P5 dominance is damage_pct plus a per-opponent-profile breakdown.
  - Habit punish window: the designer said "no more than 3", unit unconfirmed (3 seconds or 3 hits). `PunishTailTicks = 30` stays proposed.
- 2026-10-20 YOK-166 (designer): the bot's reactive Block re-reads its direction from the delayed view every tick, so it follows a cross-up. Accepted as a new R5 baseline: it changes 25 of 200 runs at idle_guard_pct 0 and 30 of 200 at the default 50; `harness/results/whole-run/` re-run on the branch.
- 2026-10-20 YOK-169 (designer): the habit punish window is the next 3 hits, not 30 ticks. A fired habit is punished by an unblocked Ryo hit while it runs or within the next 3 hits exchanged afterwards (either side's count). KO damage is tallied as the full hit, no cap. `FightLog.PunishWindowHits = 3`.
- 2026-10-20 YOK-159 Astra shop plates (designer): shop half of `astra-brief-shop-dojo.md` settled for Astra. The merchant portrait is fully human with no tell, and takes only the costume from the Tanuki merchant sheet. Wood colours #4A3425, #7A5535 and straw #C9B58C go into `style.md`. The shop backdrop (B1) runs on Astra. One plate per run, 6 runs. Dojo half (D1, B2) stays draft. The Astra output licence is still unknown and must be confirmed before shipping.
- 2026-10-20 YOK-170 (designer): the bot's idle-guard default depends on its level; the harness runs several test-bot skill levels across batches. Levels live in `data/harness/bot-levels.json` (newcomer = today's bot, pinned; intermediate and skilled values proposed). The YOK-157 first-jump rule stays at every level.
- 2026-10-20 YOK-109 scope (designer): the shop items moved out of YOK-109 to YOK-159.
- 2026-10-25 YOK-172 / AM12 (proposed, for designer review): the move property is renamed `throw_invuln` -> `throw_break` (still `"startup"`); a grab that connects during that startup resolves as a `Clash` (no damage, both freed, the throw's on_break pushback). The carrier stays throwable (E17), so a whiffing throw is not broken. Against the Elder Kappa's unbreakable throws the slip still clashes, as a same-frame clash does (YOK-90).

## Assets and spend (designer)
- 2026-10-05 YOK-30: first pass approved at 163 credits.
  - HUD textures U1–U3: 9 credits.
  - Meshes in T-pose; the nine tails are one unrigged mesh instanced 9×, swaying with the tick.
  - Ryo (ref E) and Kitsune (ref D) rebuilt for 88 credits.
  - Retakes need a new approval.
- 2026-10-05 YOK-54 and YOK-32: no sound for now. The demo shipped silent, nothing was bought, and the shortlist (PR #11) is kept.
- 2026-10-06 YOK-31: retarget gate closed with Ryo GO and Kitsune GO. Library grabs 389, 259 and 421 failed as grabs. Kitsune's sleeve flare accepted. The gate cost 18 credits.
- 2026-10-06 YOK-32/42: clip takes approved up to 123 credits (Ryo 23 takes, Kitsune 18).
  - Retakes approved for 18 credits.
  - F5 reuses: Kitsune's light punch uses her medium punch; Ryo's air punch uses the 422 jump-in kick.
- Spend so far: 251 credits on models (balance 959 on 10-06), then 18 on the gate (balance 941), then clip takes.
- 2026-10-11 YOK-102: Astra (a separate GPT agent) makes the concept art and turnarounds that feed Meshy; Claude only makes greybox sketches. No Oni, Kappa or Tanuki image-to-3D, rig or clip jobs until that art lands.
- 2026-10-15 YOK-116: free scripted weight repair of Ryo's rig accepted and swapped into `game/` (`tools/blender/repair_weights.py`, 0 credits).
  - Deep crouch goes from FAIL to WARN (Hips mean 0.686 to 0.964); the high kick drops from 35 to 9 failing edges.
  - Same skeleton, so the clips, frame data and Tanuki carry over.
  - No regeneration: `assets/specs/ryo-regen.md` is kept on file as not needed.
  - Charm, pouch and sash are only partly freed from the thighs; a full rigid bind tears the crouch (`assets/specs/rigs/ryo-weight-repair.md`).
- 2026-10-19 YOK-146 (designer, on the ticket): **Option A: Tripo's skeleton becomes the shared skeleton.** `assets/specs/rigs/skeleton.md` is rewritten from the Tripo Ryo; the Meshy spec is kept there as "Previous shared skeleton (Meshy)" and in `skeleton-meshy.json` for Kitsune, Oni and Kappa until YOK-147/148/149.
  - **Ryo's heavy punch and heavy kick come from video mocap**; the animator specs them separately.
  - The designer wants a test branch where the new Ryo is playable soon.
- 2026-10-19 YOK-146 (rig-specialist, recorded for review):
  - **Prefix stripped:** bones are `Hips`, not `mixamorig:Hips`. Godot 4.7.2 imports `:` as `_` (`mixamorig_Hips`), and the game looks up `Hips` and `RightHand` by bare name. The Blender scripts strip `mixamorig:` and `mixamorig_` on input.
  - **Fingers removed, weights merged into the hand** (E15 mitts): 25 joints.
  - **The Tripo GLB is rebuilt by script** (`tools/blender/canon_tripo_rig.py`): its joints carry no transforms and its inverse binds are in a Z-up frame, so it tears raw. Then the `ryo-tripo` pelvis repair. Metre bones, 1.75 m. Expect the same on every Tripo file.
  - **Rig ACCEPT for the test branch (provisional):** deep-crouch volume FAIL from the painted charm and pouch; everything else PASS or WARN. Stopgap clips in `game/assets/generated/clips/ryo-tripo/` (Meshy clips retargeted by script). G4 for the Tripo set stays open until the real clips. 0 credits, no jobs (`assets/specs/rigs/ryo-tripo.md`).
- 2026-10-25 YOK-109 screen concepts (Astra, `gpt-6-astra` via Codex): one take each for M1 map, H1 HUD copy target, H2 talisman states, E1 elder-twist board, run from the settled brief; no Meshy credits. Saved in `docs/design/screens/`; acceptance notes in `astra-brief-map-hud-twist.md`. Retakes need a new approval.
- 2026-11-09 YOK-150/151/152 stages, generator switch (designer): the 2026-10-20 Meshy approval (195 credits) never ran (Meshy MCP not connected) and is replaced. Backdrops reuse the style loop's Astra takes, no new spend: the designer picked take 2 of each (`runs/visual/images/visual-1-look-take2.png`, `visual-2-palette-lore-take2.png`, `visual-3-spec-form-take2.png`) over the takes the Evaluator scored highest. Props on Tripo, **210 credits spent of 210 approved**: 7 x H3.1 text-to-model, smart_low_poly, 30 each (pine cluster, snow lantern, reeds, floating lantern, mooring post, tea table, hanging lantern), take cap 1, Tripo balance 315 → 105. Tea house season screens on Astra, **4 takes of 4 approved** (spring, then summer/autumn/winter from spring as the layout reference; Codex reports no cost). No ground or water strips generated: floors are flat-colour materials in Godot. Task ids and acceptance in the three stage specs.
- 2026-11-09 YOK-54 (designer): ElevenLabs sampler approved at a **1,800-credit cap**, one take each of `hit-light`, `hit-heavy`, `hit-block`, `sting-ryo`, `sting-kitsune`, `foxfire`, `ui-seal`, `music-battle-grove` from `assets/specs/sound/elevenlabs-batch-1.json` (AM22). The other 28 jobs (~9,500 credits estimated) wait for a new approval after the sampler is heard.
- 2026-11-09 YOK-54 (sound-smith): ElevenLabs sampler run on Starter: 8 takes, ~52 credits for the 7 sound effects (from the `character-cost` header) plus the battle-grove track (1,350 estimated, no cost header); 1,421 of the 1,800 cap counted. Hit-light, hit-heavy (clipped) and block (near-silent) measured RETAKE; retakes need a new approval.
- 2026-11-09 YOK-54 (designer): sampler stings, Foxfire, seal stamp and the bamboo-grove battle theme accepted; the three hits rejected and retaken "with more thud" under a 100-credit cap: 19 credits charged (header). Take 1 of each hit deleted.
- 2026-11-09 YOK-54 (designer): heavy hit t2 rejected as hollow; t3 run with an impact prompt, cap 50, 7 credits charged (header). Third and last take before a substitute.
- 2026-11-09 YOK-54 (designer): the 3-take limit is lifted for the hit sounds; four heavy-hit variants (`hit-heavy-a`..`d`) approved, cap 60 credits.
- 2026-11-09 YOK-54 (sound-smith): heavy variants a–d run, 24 credits (header). Variant c rejected (its weight lands late); a normalized for free to −1 dBFS with a 10 ms fade (`el.py normalize`); a, b and d wait for the designer's listen.
- 2026-11-09 YOK-54 (designer): block raised for free (+29.3 dB). For a less tinny heavy punch, variant a was EQ'd for free and takes e–g were run under a 40-credit cap: 18 credits charged (header). g rejected (near-silent); e raised for free; a-EQ, e and f wait for the designer's listen.
- 2026-11-09 YOK-54 (designer): heavy hit is `hit_heavy_f_t1.wav` (fist into a sandbag); the other heavy candidates were deleted.
- 2026-11-09 YOK-54 (designer, after a listen in the game: "sfx are good"): light hit `hit_light_t2.wav` and block `block_t2_norm.wav` accepted. The title track `Dusk_Over_Yokai_Village` loses its first 8 s (5 s, then 3 s more; now 52 s, 10 ms fade-in, by script, no credits); `MusicPlayer` now loops each WAV track over its real length, not a fixed 60 s.
- 2026-11-10 YOK-54 (designer: "use appropriate music for each unique stage, that includes the four seasons of the Tanuki"): 6 ElevenLabs music jobs approved, one take each, **cap 6,300 credits**: `music-battle-shrine` (90 s), `music-battle-swamp` (90 s; replaces `music-battle-river`, AM21) and `music-tanuki-spring`/`summer`/`autumn`/`winter` (60 s each, all D minor at 150 BPM so they can cross-fade on the Tanuki's health beats; they replace `music-battle-tanuki`). The bamboo grove keeps `battle_grove_t1`.
- 2026-11-10 YOK-54 (sound-smith): stage music run, 6 tracks, one take each: **5,250 credits** of the 6,300 cap (account 3,112 → 7,612 for the first five, → 8,362 after winter). el.py's cap check counted a lagging account counter twice and stopped before winter; fixed (estimate per call, cap against the account's movement), then winter run under the 1,800 left. `MusicPlayer` plays each stage's own track and cross-fades the tea-house seasons on the Tanuki's health beats (2 s). Takes wait for the designer's listen.
- 2026-11-12 YOK-54 (designer: "on the Tanuki stage, keep the first music playing, the crossfade between them is not good so lets simply and just use the first music"): the tea house plays `tanuki_spring_t1` for the whole fight; no music change on the health beats (the screens still change). Summer, autumn and winter are unused.
- 2026-11-12 YOK-54 (designer: "accepted, push and merge"): shrine-gate and night-swamp tracks accepted. The fight scene plays the accepted light, heavy and block thuds until the animator's cue tables exist (the old hit pack is the fallback). Merged without the Code Critic and Test Breaker gates, at the designer's word.

## Tooling and process
- 2026-10-11 YOK-59/62: UI moved to the Codex ui-designer (Astra). Its 18 generated plates replaced Meshy plates, because Claude's kit (#74) was not close enough to the concepts.
- 2026-10-11 YOK-69: screen PRs #73–#83 merged as one merge commit (`a6b3d3c`), with 336 tests passing.
- 2026-10-11 YOK-89 → YOK-103: PR #92 (the 33 abilities) was reverted in `ccd9581`, because the loader rejected 13 of its 15 files and 72 tests failed. The data comes back through YOK-103.
- 2026-10-11 YOK-103 rule: **content PRs run the full `dotnet test` suite before review**; schema plus Rules Lawyer is not enough.
- 2026-10-11: ui-designer owns the rough mockups (`docs/design/screens/rough/`).
- 2026-10-15 (designer): Astra (`gpt-6-astra`) is kept for finished concept art and 3D reference images and stays the designer's own Codex run, because it is costly. The UI Designer runs in Codex on `gpt-6.1-sol` (Sol 6.1): it expands concepts, prototypes (Sol image generation allowed, marked rough) and writes an `astra-brief.md`, then hands it to the designer. No Astra crew agent.
- 2026-10-15 (designer): rig-specialist runs on Opus (low volume; rig verdicts from stress-pose stills and `bpy` scripts gate every clip). animator stays on Sonnet; move single tickets to Opus if its verdicts prove unreliable.
- 2026-10-12 YOK-103: blocked after 3 failed code-critic rounds (the workflow's cap). Sumo Stance is left out.
- 2026-10-12 YOK-74 → YOK-107: the Oni and Kappa HUD portraits were split out to wait for Astra art.
- Rules Lawyer PASS does not merge a PR that also edits tests; it is held for the designer (YOK-43).
- 2026-10-15 AM5: tickets YOK-110 (Blender scripts), YOK-111 (shared skeleton), YOK-112 (Oni rig trial), YOK-113 (retiming in ClipCatalog), YOK-114 (mocap throw trial), YOK-115 (designer setup and purchases).
- 2026-10-16 (designer): merged #131 (YOK-121), #132 (YOK-113 clip retiming curves), #133 (YOK-120 closer camera, AM8), #134 (YOK-123 dev mode shell), #138 (YOK-57), #139 (YOK-124 full map), #140 (YOK-126 run-state editor), #141 (YOK-125 jump page), #142 (YOK-128). Suite 905 passed on the last merge.
- 2026-10-16: dev mode (YOK-122): F11 menu, `--dev` works in any build, separate `save.dev.json` slot, title-only switch; settings and the replays folder are shared with normal play.
- 2026-10-16: the test runner waits one frame between test classes, so the suite no longer overflows Godot's message queue (YOK-125).
- 2026-10-16 YOK-35 (rules check script): blocked after 3 failed gate rounds; last findings and open questions on the ticket.

## Backlog refinement 2026-10-13 (designer approved)
- Closed as done on evidence from main: YOK-1–5 (vault), 13 (codebase map), 32, 33, 42, 43, 44, 45, 48, 49, 53, 56, 58, 75, and the parent YOK-39 (demo shipped).
- YOK-32's remaining level presets and sound cues move to YOK-93, which YOK-54 now blocks.
- YOK-28 is a duplicate of YOK-26. The harness baseline runs once the whole-run harness lands, which needs YOK-94.
- New links:
  - YOK-103 blocks YOK-89.
  - YOK-105 blocks YOK-98.
  - YOK-95 blocks YOK-104.
  - YOK-36 is linked to PR #105.
- YOK-74 retitled; portraits live in YOK-107.
- 2026-10-15: missing priorities set, with YOK-102 (Astra art) and YOK-54 (sound) raised to High. Open tickets assigned to W2 (run loop, Tanuki, harness, abilities, art/sound), W3 (screens, icons, fonts, tutorial) and W4–5 (YOK-93 hit feedback). YOK-26 and YOK-99 marked blocked by YOK-94; YOK-107 blocked by YOK-102.
- 2026-10-15: YOK-105 (trial schema, PR #109) and YOK-74 (copy-target HUD, PR #110) closed after the designer merged them.

## Gates
- 2026-10-06 YOK-31 retarget gate: Ryo GO, Kitsune GO (see Assets).
- 2026-10-15 YOK-38 scope gate (designer): **no cut.**
  - Kata stays: it was already built (the precision bonus is applied in `Match.Specials.cs`), so YOK-24 closed as done.
  - Meta-unlocks (YOK-86) are deferred behind YOK-89. **If YOK-89 has not merged by 2026-10-17, the meta-unlock layer is cut** and YOK-86 canceled.
  - Carry-over (YOK-97) and story stay regardless (G2).
  - The real risks are off the G2 list: the abilities (YOK-103), the Astra art (YOK-102) and harness reach (0 of 200). They go to the balance gate, mid W3.
- 2026-10-18 meta-unlock gate (designer): **met, no cut.** YOK-103 (#171) landed YOK-89's modifiers and cancels before 10-21, all but Sumo Stance. YOK-86 (unlocks) stays and can be dispatched. Sumo Stance has its own ticket.
- 2026-10-18 designer ("approve all"): every provisional default in PRs #147–#164 is accepted as built. Each one can be retuned later.
  - **Tanuki pressure (Y5):** elders and the Tanuki are pressured; the apprentice is not. +10 aggression per win (capped at 3 wins), read as a higher aggression bias. The stored win count is uncapped, and the count lives in the save as `tanuki_wins`.
  - **Elder twists (Y2/Y3):**
    - Twists are fight rules in the sim, not in the profile AI.
    - "Twice in a row": normals, throws, jumps and dashes don't break the row; EX counts as the same special.
    - Oni armour takes 1 hit per special, from frame 1 to the last active frame. A reflected projectile counts toward the elder's MaxActive.
    - The intro card shows on every elder entry. Esc closes the card.
  - **Twist text (YOK-138):**
    - The proposed Oni voice (proud, blunt) and Kappa voice (formal, polite).
    - "One-line" in Y3 means one sentence. HUD labels are stored as story cards.
  - **Carry-over (M3), all six provisional choices:**
    1. "Best" uses the copy-rule ranking over Ryo's own specials.
    2. A Lv 1 best is raised to Lv 2.
    3. It comes back with no modifier or evolution.
    4. It replaces the starter in its slot.
    5. It counts as levelled earliest.
    6. It persists until a run ends.
  - **Dojo trials (R7):**
    - Trials still marked `proposed` are offered.
    - Borrowed-ink specials can be trained.
    - No trial for a special at its top level.
    - The first trial in slot order is offered.
    - The dummy is the node's opponent.
  - **Tutorial (Y6):** no cancel rule is lent. Lesson damage carries over, and the apprentice isn't protected from a KO. "First run" ends on a win or a loss. Carried-in meter needs a new gain.
  - **Harness:** a timeout or double KO counts as a loss. The bot's numbers and the shop order are as proposed.
  - **Oni:**
    - Slower clip-timed normals (MP 8/5/7, MK 15/6/7, HP 16/4/25).
    - Push width 150.
    - The aftershock counts from the first active frame, is anchored where it struck, and pushes in the owner's facing. It still lands after the owner is hit.
  - **UI:**
    - Reward cards show the diamond icons in place of the old plates, and the frame alone marks the pick.
    - Captures from the merge train are signed off.
  - **Approved work:** YOK-34's fourth gate round, YOK-103's fourth critic round and the YOK-80 icon job (30 credits) are approved.
- 2026-10-17 designer calls:
  - **YOK-133:** the camera frames the tallest fighter in each fight. In tall-body fights:
    - the view may widen past E1's 1,540 to about 1,900;
    - the feet line sits at 1,040 px;
    - the camera may rise 0.25 m per tick.
    E1 and V7 are amended on that branch.
  - **YOK-72:** "Back to title" goes next to Restart on the defeat and demo-complete screens.
  - **YOK-73:** Continue is enabled when a saved run exists; Practice stays greyed.
  - **YOK-135:** the Oni and Kappa become pickable in the dev menu.
  - **YOK-81:** the ticket's recommended defaults: the diamond in the reward card's art area, no HUD slots, and evolutions show the base icon.
- 2026-10-17 data merge check (YOK-34, designer):
  - An empty diff is not mergeable.
  - Uncommitted changes outside data/ give SETUP.
  - MERGEABLE needs the full 200-run batch and a fresh build; anything less is INCOMPLETE.
- 2026-10-17 save format order, set by the orchestrator in planned merge order:
  - v5: elder flags (#149, YOK-90/106)
  - v6: `carry_over` (YOK-97)
  - v7: `tanuki_wins` (YOK-99)
  - YOK-98 dropped its `run.dojo` field: no save change.
  - A PR that merges out of order renumbers on rebase.
- 2026-10-17 meta-unlock deadline (designer): **extended, no cut.** YOK-89 now has until 2026-10-21 (the balance gate) to merge; if it hasn't, the meta-unlock layer is cut and YOK-86 canceled.

## Waiting on the designer
Moved to Linear on 2026-10-21 (YOK-188): the live list is YOK-189. The last repo copy is in git history at `b438e31`.

## Designer decisions 2026-10-20
- **Lv 2 steps (A4/E21):** movesmith proposes one step per remaining special; the designer approves (proposal in progress on branch YOK-36-lv2-steps).
- **Forecast (T6):** "expected cost" is the copied move's damage per hit at Ryo's level, from data. Always available, so no "not enough fights" state. Ticket YOK-173 replaces the placeholder "~190 health".
- **AM12 Slippery Skin:** automatically breaks a throw attempted during the carrying move's startup (a clash, no damage, like throw vs throw). E17 is unchanged. Ticket YOK-172.
- **Heals (R6, R7):** shares of max health, merchant 25% and dojo rest 30%. Open question removed.
- **River Grab** is a throw (C4, R7), for trials and cancels.
- **AM11 Sumo Stance (YOK-142):** a longer hold gives a faster dash, up to a maximum speed; replaces "+20% damage". Engine ticket YOK-171 blocks YOK-142. Tanuki copy behaviour for a charged special is an open question.
- **Shop shelf (R6, YOK-159):** physical shelf, 3 items per shelf. Per visit 9 items (3 shelves) proposed, 6 as a balance alternative; balance decides (open question). Bought items roll up in place. The merchant has no idle line. Shop lines carry no copy hinting.
- **Y4:** half damage floors per hit. The 110/80 per-duel figures are harness targets only. The Y6 guided first fight is at half damage.
- **Enemy jump punishing (YOK-165):** apprentice and yokai punish jumps 50% of the time; elders and the Tanuki 100%. Per-tier data.
- **AI guard:** opponent pre-emptive guard yes (YOK-165). Bot reactive Block follows cross-ups, yes (YOK-166), with a new R5 baseline.
- **Test bot (YOK-170):** several skill levels across runs; `idle_guard` depends on the level.
- **Habit punish window (YOK-169):** the next 3 hits. KO damage is tallied as the full hit.
- **G3 balance gate:** WAIT for harness batch 3 (YOK-167) before deciding any cut.
- **YOK-146:** keep the 54k mesh; retake the deep crouch (route pending); commit `Ryo.glb` (PR #195); the groin-weights branch is rejected.
- **Review queue:** the `ready-for-review` Linear label now exists (YOK-37).
- **YOK-71 fonts:** specimen sheet in progress.
- **Stages first pass (YOK-150/151/152), approved 2026-10-20:** 195 credits on Meshy, Shrine Gate 66 (S1-S8; S9 snowflake sheet not approved, plain quads), Riverbank 69 (S1-S9; no bank lanterns, spec default), Tea House 60 (S1-S10). Tea House seasons change on beats, round start only; wiring is a gameplay follow-up. Palette added to `style.md`: snow #A9B4C8, water teal #3E7F86 (same as Kappa spark teal), brown #6E5A48, spring pink #E8A6B0. Take cap 3 per asset, never above 195 total without asking. **Status: jobs not run**, the Meshy MCP was unavailable to the asset-smith session, 0 credits spent.
- 2026-10-20 YOK-109 / YOK-163 dojo pass (branch): the designer chose mockup parity, so the trial card has a special selector (Left/Right, A/D, bumpers, D-pad; skips specials with no authored trial or at top level) and 3 attempt pips; this replaces the YOK-98 provisional "first trial in slot order, no picker". The trial card names the special by its display name (YOK-163). Esc / B still goes to the title (GameFlow ReturnToTitle), so the footer keeps "Title"; the mockup's "Esc = map, only before a choice" needs a leave-the-node API and waits on the designer. The dojo forecast line says "Training {move} to Lv {n} raises this copy" only while n <= 2 (T4: the copy never passes Lv 2), not the mockup's fixed "Lv 3".
- 2026-10-20 YOK-159 Astra shop plates accepted (asset-smith): S1-S5 and B1 ACCEPT and imported to `game/assets/generated/ui/` (Pillow Lanczos). S4 is a script repair of take 2. Strict misses recorded: cabinet row 3 is about 20% shorter, S2 and S4 need wider nine-slice margins than the brief. Astra licence still unknown; costs not reported by Codex. Details in `assets/specs/ui-screen-plates.md`.
- 2026-10-20 YOK-159 merchant portrait (designer): take 3 of 3 accepted, restyled to match `ryo-portrait.png` (black dry-brush hatching, flat shapes, heavy outlines). Ryo's portrait is the style reference for screen character art. Astra was run by the main session through `codex exec` on the designer's say-so.

## Designer decisions 2026-10-21
- **AM13 apprentice profile (Y1):** the apprentice gets its own behaviour profile (`data/profiles/apprentice.json`, `yokai: "apprentice"`), named by its node; it still plays as the Kitsune for body and kit. 8 profiles now. Reason: a patient Kitsune apprentice timed out. YOK-174.
- **Temperament roll (Y1/Y2, YOK-174):** each yokai node rolls its temperament, seeded per node from the run seed, 50/50 aggressive/patient. A node that names a profile keeps it.
- **YOK-165 accepted**, with the YOK-176 follow-up.
- **YOK-179 implementation** (the no-timer and exchange-ratio decisions themselves are recorded in the 2026-10-21 entries): `max_fight_ticks` 108000 (30 min, safety cap, still not per level); a capped fight is outcome `stalled`, left out of win/reach denominators (`fights_stalled`, `runs_stalled`, `stalls_by_profile`, `levels.<level>.duels_stalled`); `exchange_ratio` = hits, `damage_ratio` = damage, `hit_exchange_ratio` dropped. Harness elder fights now carry their twist, so elder numbers from batches 1-3 are not comparable.
- **A late anti-air counts.**
- **Habit punish window (P6/Y2, YOK-169):** a habit counts as punished by an unblocked Ryo hit while the habit's plan runs, or within the next 3 hits exchanged after it ends. Blocked connects DO count toward the 3; a landed throw counts once; throw breaks and clashes don't. KO damage is tallied in full. Supersedes the plain "next 3 hits" line above. Rules: Y4.
- **"Exchange ratio" (Y4, R5) means hits:** unblocked hits landed / unblocked hits taken. Written into Y4 without a number. Context: 4.5:1 is the Sparring Partner's derived reference (YOK-154 analysis of the ratio implied by Y4's damage-per-duel targets), not a designer-set target. Harness change: YOK-179.
- **E23 debug temperament is sticky:** a choice made with F9 or `--temperament=` stays in effect across later nodes until changed.
- **E22 constants:** `GuardTicks` 10 (how long an AI that opts into `guard_pct` keeps guarding after its own move's recovery; YOK-165; the guard ships off, `DefaultGuardPct` 0). Tier `jump_punish_pct`: apprentice 50, yokai 50, elder 100, Tanuki 100 (absolute chance; a late anti-air counts), recorded in Y4.
- **G3 balance gate (2026-10-21):** no cuts yet. Fix the batch-3 bugs (YOK-177/178/179), rerun as batch 4 (YOK-185), then decide. The newcomer playtest (YOK-50) calibrates whether the bot or the fights get retuned.
- **No round timer (C1):** fights go to KO. The harness cap is only a safety limit; stalls are reported separately. No AM needed: the GDD sets no timer, so this fills a gap.
- **R6:** beating an elder counts as binding its yokai for the shop. Open question removed from rules.md. YOK-178.
- **Naming:** elders are named "Elder <yokai>" (e.g. "Elder Oni") on the binding card and screens.
- **Y2 Tanuki habit:** the throw triggers after ONE blocked hit (the first blockstun it sees), not "keeps blocking".
- **Y2/E22 standing-condition habits:** fire when the condition becomes true, then may re-roll at most once every 60 view ticks while it holds (approved). Listed in E22.
- **Y2 fired habits always come out,** even if the AI is hit before it can act. Exceptions: a jump answer replacing a standing habit plan (Y4), and the round or fight ending. YOK-177.
- **AM14 written** for the Tanuki one-blocked-hit habit, because AM4 said "keeps blocking" and an amendment wins over rules.md.
- **YOK-177 held throws:** fired standing habits always come out (Tanuki throw habit on the first blockstun seen, 60-tick re-arm approved). The held throw jump answer is a slot request under the YOK-177 rule, which changes the Kappa profiles in tier-rate fights; approved.
- **Standing-trigger habit re-arm (Y2, YOK-177, approved 2026-10-21):** a habit on a standing condition (`at_close_range`, `opponent_blocking`) rolls when the condition becomes true on the AI's view, then at most once per 60 view ticks while it holds (`ProfileAi.StandingHabitRearmTicks`). Effect on the apprentice: about 30 rolls and 18 heavy punches per fight. Approved 2026-10-21.
- 2026-10-24 YOK-196 (designer: "update the GDD as necessary with the amendments"): the GDD-changing decisions without an amendment were written as AM15 (forecast cost) and AM16 (Tripo skeleton); [AM1] tags added to F2, F5 and V1. The shop shelf (YOK-159) and the Codex runtime of the UI Designer fill gaps the GDD leaves, so they stay log lines; the designer may still class them as amendments (YOK-189).

## Designer decisions 2026-10-25
- **YOK-109 PR #246:** story-card text may not contain emoji, zero-width joiners or combining marks (precomposed accents allowed); Godot 4.7.2's text server crashes on Linux when shaping long runs of them (designer).
- **YOK-109 PR #246:** 4th gate round allowed; elder-twist intro cards capped at 260 characters in the story-card schema, and the scroll's quote box fits 280 characters of prose whole; a quote that still overflows (for example an over-wide unbroken word) is shortened with an ellipsis and a warning, never cut silently (designer).
- **Tanuki fight shows Ryo's level (T4, T6; YOK-109):** the copy-target talisman and the "THE TANUKI COPIES" strip show Ryo's level for the copied move; the strip reads "<Move> · Lv N · no modifiers, no evolution", so the player is told the copy runs without the modifier or the Lv 3 evolution. Closes the rules.md open question on which level the player sees.
- **The Oni is the shrine gate's guardian (S5):** the illusion has bent his post into a gate no one may pass; freed, he goes back to guarding it. Proud and blunt, never a menace. Chosen over dropping the Oni for being "evil" in folklore and over a darker "Ryo frees something dangerous" reading; the binding line (S3, AM2) stays one template for every yokai. No AM needed: the GDD gives the yokai no backstory, so this fills a gap.
- **AM17 first-meeting video:** a short story-card style video on first meeting each yokai, extra and not MVP. Ticket YOK-207 holds the open details (also in rules.md's open questions).
- **YOK-109 map, HUD copy target and elder twist concepts (Astra brief settled):** pine cloth for the whole kit, elder scrolls included (Q15); the map's walked route is brushed ink (Q16); twist wording on the screens comes from the story data (`-intro`, `-callout`, `-label`), not the rough mockup; the boss card keeps "copies your best move" before the reveal; neutral Oni/Kappa portrait placeholders are fine for this pass; the copy talisman and the Tanuki-fight copy strip show Ryo's level, and the strip says "no modifiers, no evolution" (T4). Astra approved for one take per image (M1, H1, H2, E1).
- 2026-10-25 YOK-208 bamboo grove lit lanterns (designer: "1. overlap , add fixes"): Lantern0 moved in front of Bamboo3 to head the left path row; its near-total overlap of PathLanternL0 at the left-corner framing is accepted; LanternLine clamps NearScale and skips lanterns at or behind the backdrop, so the two open Test Breaker probes are fixed rather than left advisory.
- 2026-11-09 YOK-109 map v2 (designer: "lets do an astra pass on the map"): the map scroll follows the title screen's reason scroll; upcoming routes are sketched paths fading into mist, with no nodes in the mist; yokai nodes are masks, the merchant a wooden stand; routes wind. One Astra take (`gpt-6-astra` via Codex), brief `docs/design/screens/astra-brief-map-v2.md`. Mist sketches change AM18's "blank paper"; whether they follow the real route graph is open.
- 2026-11-09 YOK-109 map v2 take 1 reviewed (designer): the mist sketches follow the real routes (AM19); the Apprentice's plain wooden practice mask is approved. Retake approved with: no row headings; mist covers the routes ahead; a different heading font top left; elder masks are the normal yokai masks plus the rope, streamers and twist seal.
- 2026-11-09 YOK-109 map v2 take 2 reviewed (designer): keep take 1's trailing mist (wisps at the right edge, not a wide bank) and drop the row count ("row N of 8 done") from Ryo's status panel on the map. Take 3 approved and run; it becomes `map.png`, with take 1 as the mist reference for the build.
- 2026-11-09 YOK-238 map v2 rebuild (designer): Astra plate pass approved ("~8 takes"); the dojo node is a wooden dojo gate, the Tanuki node a tea house with its screens shut (no creature). Six takes run (MP1–MP6, `gpt-6-astra` via Codex; Codex reports no cost), all accepted; brief `docs/design/screens/astra-brief-map-plates.md`, sprites in `game/assets/generated/ui/map/`.

## Designer decisions 2026-11-01
- **Moves and balance work back in scope (designer, production pass):** "develop all remaining abilities, focus on the coding side, visuals can be greybox for now". YOK-171/172, 185, 180-184 and 142 are no longer excluded. No generation, rig or clip jobs this pass; the designer does the visuals pass later. F2 (clip first) stands.
- **YOK-219 greybox clip route A (designer):** a move with no take of its own gets its own `data/clips/<move>.json` that reuses a measured, accepted clip (retime 0.5x-2x, holds of at most 4 frames, notes "GREYBOX reuse of <clip>, replace when the take lands", `status: proposed`); its frame data follows that clip (F2), and is re-derived when the real take lands. Chosen over waiting for takes (B) and a `greybox` clip source by amendment (C). Iron-Club Charge takes the clip-limited total (about 32 frames, proposed 12/6/14) instead of 16/6/24; Oni Quake keeps the measured 27/4/25 instead of 22/5/26; Shell Spin uses a heavy-swing stand-in (or `oni-quake`) until a Kappa clip job runs. Both timing changes go into rules.md through an amendment in the Oni specials PR (YOK-227).
- **YOK-221 whole numbers follow the loader (designer, 2026-11-02):** a number in content is "whole" as the C# loader's .NET `decimal` reads it (rounded past about 28 significant digits; tiny exponents read as 0), and `tools/validate_data.py` mirrors that rounding, rather than both sides judging the exact written value. Chosen as the smaller change; hand-written data never carries that many digits. The validator's hang on huge exponents and the duplicate travel report are fixed regardless.

## Designer decisions 2026-11-09
- **Stage backdrops (YOK-150/151/152):** take 2 of each style-loop run is the accepted backdrop: shrine gate centre-right with stone lanterns; riverbank with round paper lanterns and their reflections; tea house with eight screens and painted tatami. Licence of Astra output still unconfirmed (as YOK-159).
- **Style-loop Evaluator tuned toward the picks (designer: "adjust the evaluator towards the second takes"):** `tools/style_agent/visual_guide.py` v2 is the default: L2 allows a graded glow inside a lantern's paper and a flat water reflection; L4 scores against the four accepted stages (one 2x2 REFERENCE) and never docks crisper ink line or more depth than the grove; S4 counts drawn floor structure (tatami seams, drifts) as calm; new S5 checks each spec's composition. v1 stays byte-identical behind `run.py visual --guide-v1`, so `runs/visual` and `runs/visual-v2` still replay.
- **Tea house seasons as one wide painting (implementation call, same cost):** the eight narrow screens show one continuous landscape masked by `panel-mask.png`, not a 2:3 panel image repeated per screen; changes at round start only (2026-10-20). Round → season mapping is a default (round n → (n-1) mod 4 from spring) for the designer to confirm.
- **Tea house seasons move on the Tanuki's health, not rounds (designer, replaces the 2026-10-20 round-start beat):** C1 makes every fight one round, so the round beat would only ever change on a rematch. Now spring at fight start, then summer, autumn and winter once the Tanuki first drops below 75%, 50% and 25% health. Monotonic within a fight, back to spring on a new fight or reset, 30-tick cross-fade, read on the presentation side only (the sim never depends on the stage).
- **Riverbank polish in YOK-150 (designer):** fewer floating 3D lanterns (about 8-10), spread in depth and out of the fighter band; water darker and more indigo behind the fighters, so Kappa's teal separates by hue. Kappa is not recoloured.
- **Stage per yokai, `opponent_stages` (gameplay-programmer, YOK-150; designer to review in the PR):** a top-level yokai id → stage map in `data/map/node-scenes.json`, instead of a stage on each opponent entry (existing tests and a probe refuse that). Lookup: the node's stage, then its yokai's, then the yokai it plays as, then the type's, then `default_stage`. Oni → shrine gate, Kappa → riverbank, Tanuki → tea house; Kitsune, Nine-Tails and the apprentice stay on the grove.
- **Stage build choices (gameplay-programmer, YOK-150; designer to review in the PR):** floors are one procedural shader with colours sampled from each backdrop (no strips generated); Tripo props recoloured by `tools/stage/recolor_props.py` (pines toward pine #34483B, floating-lantern paper to persimmon with a flat 0.45 emission, reeds to dry olive); no snowfall yet; no gate mesh (it's painted).
- **Riverbank lanterns, second pass (designer: "fix now", 2026-11-09; replaces the "about 8-10" above):** 14 floating 3D lanterns, 4 lit, so 4-5 show in each centre framing. The 4 deep ones (z -27 to -36, drawn at most 0.6 of the nearest one's size) may pass behind a fighter as background; near ones keep the hip-to-head clearance. The 3D water now meets the painting at row 191.5, so no painted lantern is ever half-hidden; the painting shows only the far reeds, hills and sky.
- **Kappa stage becomes a swamp (designer, 2026-11-09: "lets do retakes and have it resemble a swamp more without the lanterns"; replaces the two riverbank lantern lines above):**
  - **Takes:** 4 new Astra backdrop takes from a swamp brief, with no lanterns or light sources. The painting is composed as a far view with a low, level waterline. The designer picked take 4: twisted root trees, hanging moss, mossy logs and mist.
  - **Lanterns:** all 14 floating 3D lanterns and their lights are removed.
  - **Water:** murky swamp green.
  - **Unchanged:** the stage id (`riverbank`) and the `opponent_stages` mapping.
  - **Spend:** 4 Astra takes; no Tripo.
