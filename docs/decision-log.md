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

## Rules and tuning
- 2026-10-06 E1–E24: engine defaults approved by the designer. Max separation is 1,520 units (lowered from 1,600 in YOK-39).
- 2026-10-06 YOK-41 / E21: at Lv 2, Spirit Wave recovers 4 frames faster and Rising Talisman 6 faster, both relative to the clip-derived Lv 1 recovery. Damage-only steps are out (P1).
- 2026-10-06 YOK-31 / E15: strikes are open-handed (the rig has no finger bones). The generic throw is the Meshy 259 `Step_Forward_and_Push` clip, head-trimmed from tick 30.
- 2026-10-11 YOK-87: a whole number written as `1e2` reads as 100. Save format is v2.

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

## Waiting on the designer
- **YOK-115 (AM5):** Tripo plan and licence, Tripo MCP or manual runs, AccuRIG 2 licence, Auto-Rig Pro, ActorCore, mocap tool, Cascadeur.
- **YOK-36 open rules:**
  - Lv 2 steps for the other specials.
  - Credit and sound spend, plan tier and licence.
  - Practice-mode trials.
  - From PR #105:
    - Heal and rest percentages against the flat 250/300 as built.
    - Leaving a rest stop without choosing, and healing at full health.
    - The shop shelf's size and how its items are picked.
    - The forecast's expected-cost formula.
    - Slippery Skin against E12/E17.
    - Sumo Stance's "holding charge".
    - Which level the Tanuki fight shows for the copied move.
    - Whether River Grab counts as a throw or a hit.
- **YOK-102:** an amendment so AM1, rule F6 and asset-smith.md allow Astra turnarounds.
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
- **YOK-71:** fonts. Options are a licensed brush font plus a serif, a free serif, or system fonts.
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
- **Review queue, in suggested order:** #106 Tanuki → #108 merchant → #107 rest stop → #105 rulings (#109 and #110 merged 10-15).
