# Decision log

A compact history of the decisions made so far, for agents to load as memory. Built on 2026-10-13 from Linear comments and decision tickets, `docs/design/gdd-amendments.md`, `docs/design/rules.md` and git history. When this log disagrees with rules.md, the amendments or the PDF, the sources of truth in CLAUDE.md win and this log gets fixed. Add new decisions at the bottom of the matching section, one line each, with date, ticket and who made the call.

Build week 2 of 5 (first commit 2026-10-06). Milestones in Linear: W1 ends 10-10, W2 ends 10-17, W3 ends 10-24, W4–5 end 11-07.

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
- **YOK-146 (Tripo Ryo):** decided 2026-10-20, see Backlog refinement 2026-10-20. Waiting only on the deep-crouch retake route.
- **YOK-115 (AM5):** Tripo plan and licence, Tripo MCP or manual runs, AccuRIG 2 licence, Auto-Rig Pro, ActorCore, mocap tool, Cascadeur.
- **YOK-36 open rules:**
  - Credit and sound spend, plan tier and licence.
  - Practice-mode trials.
  - From PR #105: all answered 2026-10-20 except the shop's item count (balance decides), whether Ryo may leave a rest stop without choosing, healing at full health, and the level the Tanuki fight shows for the copied move.
- **YOK-102:** run Astra from `docs/design/characters/astra-brief.md`; palette hexes, Oni/Kappa heights, guise fallbacks (G1, G2) and the Meshy licence. 0 Meshy credits approved; mesh lines (96 first pass) wait for the sheets.
- **YOK-103 and YOK-89:**
  - Whether to allow a 4th critic round (the `-4.0` fix at `RewardDraft.cs:166-167`).
  - Unset values: River Pull, Trickster's Angle, Oni's Hide, Iron Will.
  - Cancel triggers for Oni Chain and Fox Step.
  - The River Grab clip.
- **YOK-91:** merge or hold the provisional Oni, Kappa and Tanuki profiles.
- **YOK-26 harness:**
  - A 3-minute fight cap, per-run AI seeds (fixed seed 27 today), and 45 s per node.
  - First batch: **0 of 200 runs reached the Tanuki** (target 50–70%, R5).
- **YOK-30:** U3 and S6 retakes (3 credits each), the Meshy licence, and Ryo's simplified outfit.
- **YOK-69:**
  - Close PR #72.
  - Ryo's HUD portrait.
  - Worktree and screenshot cleanup.
  - A hand play-through with a pad.
- **YOK-71:** fonts. Specimen sheet in progress (2026-10-20); the designer picks from it.
- **YOK-85, 87, 88:**
  - Coins keyed by node type or by row.
  - Start Game overwriting a save without asking.
  - Where the Tanuki win count lives.
  - S2 card length against the GDD's own 3–4 sentence cards.
  - A card for losing to the Tanuki.
- **YOK-96, 104, 106:** reopening the shop, and whether "met" is per save or per run.
- **YOK-50 and YOK-46:** whether the demo newcomer playtest and the Rules Lawyer pass on the demo content are still needed.
- **Vault (YOK-3/4/5):**
  - Oni and Kappa voices, and the tail count.
  - The order of the 12 unlocks.
  - Which modifiers a cut keeps.
- **Review queue, in suggested order (2026-10-17; #105–#108 merged):** #148 harness (YOK-26) → #147 Oni/Kappa bodies (YOK-131; captures first) → #150 Oni normals (YOK-132) → #149 elder twists (YOK-90, carries YOK-106, save v5) → #151 tutorial (YOK-92).

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
