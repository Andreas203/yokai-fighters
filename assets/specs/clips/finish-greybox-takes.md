# Finish the greybox clips: take list (spec only, nothing run, 0 credits spent)

**2026-11-12 update: the designer chose Tripo over Meshy and the Meshy MCP is not connected. The Tripo plan in "Re-plan on Tripo (YOK-246)" below is the live one. The Meshy tables after it are kept as the fallback only.**

## Re-plan on Tripo (YOK-246)
Route: one Tripo API rig of Ryo (rig v1.0, the only rig version with combat presets), Tripo presets on it, then `tools/blender/retarget_bvh.py --clip` onto each body's existing skeleton: `--src-skeleton tripo` for Kitsune, Oni and Kappa (Meshy skeleton), the default for Ryo's own clips and the T0 twins (a Tripo clip needs no retarget on Ryo's Tripo rig). One preset is retargeted to every body that needs the motion, so the unique count stays small. No new mesh and no re-rig of a game body.
Free preset list: `tripo docs --topic commands/process` (section "Presets for rig v1.0"). Free checks: `tripo balance` (65), `tripo anim check`, `tripo task get`.

**Free test done (cross-skeleton retarget works):** `retarget_bvh.py` only mapped Meshy-to-Tripo. I added `--src-skeleton tripo` (the reversed chain map in `TRIPO_CLIP_MAP`) and retargeted `ryo-tripo/ryo-lp.glb` onto `kappa-rigged.glb`: it ran in 8 s, mapped all 22 limb, spine, neck and head bones, hip ratio 0.787, only `head_end` and `headfront` unmapped. Pose quality, feet and shell clearance are NOT yet measured (needs `measure_clip.py` and `prop_clearance.py` per clip). Also done free: `tools/blender/strip_for_rig.py` made `game/assets/generated/characters/ryo/takes/ryo-upright.glb` (bare standing mesh from the canonical rig, not committed, 4.3 MB), and `tripo anim check` on it returned **riggable: true, biped** (0 credits).

Unique presets (rig v1.0, all `preset:biped:`), 18 of them, and where each plays. Picks are from names; no Tripo preview is available to me, so fit is judged after the first retarget and measure.
| Preset | Plays as |
|---|---|
| idle | idle on all four bodies (Oni and Kappa stop borrowing) |
| walk | walk fwd, walk back (reversed), dash (2x) on all four |
| jump | jump, jump back, landing on all four |
| box_01 / box_02 / box_03 | light / medium / heavy punch (Ryo, Kappa, Kitsune LP); Rising Talisman stays on its own clip |
| front_kick_01 / front_kick_02 | light kick; medium kick, and heavy kick retimed slower and held (only two kick presets exist) |
| slash | Oni light and medium punch, medium kick, Iron-Club Charge (and its Ryo twin) |
| chop | Oni heavy punch and heavy kick; Oni Quake and its Ryo twin (overhead club slam, no ground-slam preset) |
| cast_a_spell | Foxfire, burst cast on all four, Kitsune Fox Mirage startup |
| flip | Fox Mirage (Kitsune and Ryo twin): an acrobatic vanish |
| turn | Shell Spin (Kappa and Ryo twin): the only rotation preset |
| hug | the generic throw (all four); Ryo, Kitsune, Oni |
| lift_heavy | River Grab (Kappa and Ryo twin): a seize and lift |
| hit_to_head | hit high, thrown |
| hit_to_stomach | hit low |
| defeat_02 | knockdown and KO |
Not possible on Tripo presets (v1.0 has none): block high, block low, crouch, dash back, get up, guard walk, an air punch (box_02 would be a ground punch), and a guard idle. Fallback for these: the existing Meshy-library clips already on the Tripo skeleton (`ryo-tripo/`), retargeted onto Kitsune, Oni and Kappa with the new flag (0 credits). That removes the borrowing between bodies but they stay Meshy motion. For true Tripo-or-mocap takes of those, the designer would need to buy ActorCore packs or record mocap (F6).

**Cost.** Rig 25 + 18 presets x 10 = **205 credits. Balance 65, so buy at least 140.** Split to buy less now:
- Phase 1, moves only (12 presets: box_01-03, front_kick_01-02, slash, chop, cast_a_spell, flip, turn, hug, lift_heavy): 25 + 120 = **145 credits, buy 80**.
- Phase 2, poses (6 presets: idle, walk, jump, hit_to_head, hit_to_stomach, defeat_02): +60.
No retake budget is included; one retake of any preset is +10. A rig fault means the 25 is spent again.

**Commands for the designer (paste each with `!`, in order).** The free check and `ryo-upright.glb` are already done. Stop after step 1 if `credits_consumed` is not 25.
```
tripo anim rig game/assets/generated/characters/ryo/takes/ryo-upright.glb --model v1.0 --rig-type biped --spec mixamo --out-format glb --name ryo-api-rig -o game/assets/generated/characters/ryo/takes/tripo
tripo anim retarget @ryo-api-rig --animation preset:biped:box_01 preset:biped:box_02 preset:biped:box_03 preset:biped:front_kick_01 preset:biped:front_kick_02 --out-format glb --animate-in-place --name ryo-batch-a -o game/assets/generated/clips/tripo-takes/a
tripo anim retarget @ryo-api-rig --animation preset:biped:slash preset:biped:chop preset:biped:cast_a_spell preset:biped:flip preset:biped:turn --out-format glb --animate-in-place --name ryo-batch-b -o game/assets/generated/clips/tripo-takes/b
tripo anim retarget @ryo-api-rig --animation preset:biped:hug preset:biped:lift_heavy preset:biped:hit_to_head preset:biped:hit_to_stomach preset:biped:defeat_02 --out-format glb --animate-in-place --name ryo-batch-c -o game/assets/generated/clips/tripo-takes/c
tripo anim retarget @ryo-api-rig --animation preset:biped:idle preset:biped:walk preset:biped:jump --out-format glb --animate-in-place --name ryo-batch-d -o game/assets/generated/clips/tripo-takes/d
```
Batches A and B are phase 1; batch C is half phase 1 (hug, lift_heavy) and half phase 2; batch D is phase 2. After each command read `credits_consumed` (rig 25, 10 per animation) and `tripo balance`. If `@ryo-api-rig` does not resolve, use the rig task id from `tripo history`. Each retarget returns the rig plus animation (`export_with_geometry` default true), 5 animations at a time.

**Timeline once credits are in.** Rig about 2 to 4 min; each batch about 3 to 6 min, so all takes in about 20 min. Then per take: cleanup, reverse-retarget to Kitsune, Oni and Kappa (8 s each, about 70 body-clips in all, scripted in one loop), measure, shell and club check, retime: about 60 to 90 min for the full set, 40 to 60 min for phase 1 alone. Movesmith can rewrite frame data from measured clips about 1.5 to 2 h after the credits arrive. Presets are low in variety, so every body punches and kicks with the same motion; retime and trim vary the feel only.

## Takes (Tripo)
None run. Free test only: ryo-lp onto kappa-rigged (scratchpad, not shipped).

## Earlier Meshy plan (fallback, superseded)

Branch YOK-finish-clips, 2026-11-12. Rules: F2, F3, F5, F6, T0, C4, G4. Author: animator.
Preset ids and names come from the public Meshy animation library table (docs.meshy.ai/en/api/animation-library, 656 rows, fetched today). **Picks are made from names and category, previews not yet inspected**; every primary has an alternate, and the take cap is 3 per move (F5). Real timing comes from measuring the exported GLB, not from this table.

## Balances (free checks)
- Tripo: `tripo balance` = **65 credits** (0 frozen), 2026-11-12.
- Meshy: **not checked**. The Meshy MCP is not in this agent's toolset. The producer or designer runs `meshy_check_balance` before approval. Known price: 3 credits per `meshy_animate`.

## Inventory: what is a stand-in today
| Group | Clips | Why it is a stand-in |
|---|---|---|
| Kappa normals (6) | kappa-{light,medium,heavy}-{punch,kick} | Kitsune and Ryo Meshy files played through hip scale 0.828. Shell clearance and feet never measured on Kappa (YOK-228). LP is the Kitsune MP at 1.5x. |
| Kappa specials (2) | kappa-river-grab, kappa-shell-spin | River Grab = the generic push 259 re-curved. Shell Spin = the 194 uppercut re-curved (not a spin). |
| Kappa throw | kappa-throw | The generic grab 259 (C4 says one generic grab, so by design), but it is Kitsune's file and unmeasured on the Kappa shell. |
| Kitsune | kitsune-fox-mirage, kitsune-light-punch | Mirage = Foxfire 136 at 2x (a cast, not a vanish). LP = her MP at 1.5x (F5 substitute after jab 192 failed). |
| Oni specials | oni-quake, oni-iron-club-charge | Quake = heavy punch 128 with a curve. Charge = preset 654 stride, no strike moment, 14 clean ticks. |
| Oni normals | oni-light-punch (plays ryo-light-punch), oni-heavy-kick (plays ryo-heavy-kick), oni-medium-kick (same 97 Left_Slash as oni-medium-punch) | Ryo's bare-handed clips on the club Oni; two moves on one clip. |
| Ryo-rig twins (T0) | ryo-fox-mirage, ryo-iron-club-charge, ryo-oni-quake, ryo-river-grab, ryo-shell-spin | GREYBOX reuse of ryo-heavy-punch, ryo-generic-throw, the Foxfire cast. |
| Ryo air punch | ryo-jump-punch (move ryo-air-punch) | Plays the jump-kick clip (F5 substitute). |
| Whole Ryo Tripo set | 26 files in game/assets/generated/clips/ryo-tripo/ | README: every file is a Meshy clip retargeted by `retarget_bvh.py` onto the Tripo skeleton (YOK-244). No Tripo preset was ever run on Ryo. The main checkout has uncommitted edits to 7 of these glbs; confirm they are not newer than this branch before measuring. |
| Not stand-ins | all Kitsune normals except LP, kitsune-throw, Ryo normals, specials Spirit Wave and Rising Talisman, walks, hits, blocks, idle, jump | own preset, measured |

Every move in data/moves has a clip id that exists. No move lacks a clip; the gaps are the shared and re-curved ones above.

## Route for the Ryo twins (recommended): Meshy take, retargeted free
A T0 twin must run the same frame data as the original (Tanuki copies Ryo's most-invested special with Ryo's model). The twin therefore must be the same motion as the new body clip. So each twin is the **same Meshy take as its body clip**, retargeted by script onto the Tripo skeleton with `tools/blender/retarget_bvh.py --clip` (0 credits, 0 Tripo credits). That keeps twin frames identical by construction. Tripo presets are listed below as an optional route only.

## Meshy jobs (3 credits each, preset applied on the body's existing Meshy rig task)
Rigs: Kappa `01a144ad-ca9c-7370-b2e3-c7b2f9e3aa11`, Oni `01a144ad-b98d-7520-8376-c776c414a476`, Kitsune `01a10e47-91f6-709b-bd34-8884f8cbabbb`, Ryo (Meshy) `01a10e47-857e-73a0-9f7a-cd74795198fb`. Raw GLBs to `game/assets/generated/clips/<body>/takes/`. Kappa shell check = `prop_clearance.py --clip` (pass: no shell vertex more than 2 cm newly inside); Oni club check the same with `club-grip.glb`.

| Clip id | Body | Source | Primary preset | Alternate | Why it fits | Cost | Replaces |
|---|---|---|---|---|---|---|---|
| kappa-light-punch | Kappa | Meshy | 191 Left_Jab_from_Guard | 214 Punch_Forward_with_Both_Fists | fast straight, 4/2/7, arm low and forward, shell-safe | 3 | Kitsune MP at 1.5x |
| kappa-medium-punch | Kappa | Meshy | 214 Punch_Forward_with_Both_Fists | 90 Counterstrike | sumo palm thrust, 6/3/12, hips level | 3 | Kitsune MP |
| kappa-heavy-punch | Kappa | Meshy | 260 Push_Forward_and_Stop | 96 Kung_Fu_Punch | two-hand shove, 10/4/20, fixes the -3.2 cm heavy-foot (option B) | 3 | Kitsune uppercut 194 |
| kappa-light-kick | Kappa | Meshy | 103 Simple_Kick | 211 Boxing_Guard_Step_Knee_Strike | short shin kick, 5/2/9 | 3 | Ryo knee clip |
| kappa-medium-kick | Kappa | Meshy | 209 Boxing_Guard_Right_Straight_Kick | 218 Step_in_High_Kick | straight kick, 7/3/14, no torso twist | 3 | Kitsune MK |
| kappa-heavy-kick | Kappa | Meshy | 206 Spartan_Kick | 217 Sweeping_Kick | thrust push-kick, 11/4/23 knockdown; avoids the shell-risky twist of roundhouse 207 | 3 | Kitsune HK |
| kappa-river-grab | Kappa | Meshy | 262 Crouch_and_Push_Forward | 398 Crouch_Charge_and_Throw | low fast reach then seize, 6/2/30, never paired (C4) | 3 | 259 re-curve |
| kappa-shell-spin | Kappa | Meshy | 100 Rightward_Spin | 384 Quick_Step_and_Spin_Dodge (then 216 Lunge_Spin_Kick) | a real turn in place: tuck startup 10, spin 18, settle 20 | 3 | 194 uppercut re-curve |
| kappa-throw (optional) | Kappa | Meshy | 259 Step_Forward_and_Push | none (generic grab by C4) | same generic grab applied on the Kappa rig so the shell is measured | 3 | Kitsune file |
| kitsune-fox-mirage | Kitsune | Meshy | 125 Charged_Spell_Cast | 384 Quick_Step_and_Spin_Dodge | charge then release, matches 18-frame startup, differs from Foxfire 136 | 3 | Foxfire 136 at 2x |
| kitsune-light-punch | Kitsune | Meshy | 191 Left_Jab_from_Guard | 214 | jab 192 failed on her sleeves (16.6x); the left jab keeps the arm nearer the body | 3 | MP at 1.5x |
| oni-quake | Oni | Meshy | 127 Charged_Ground_Slam | 382 Jump_and_Slam_Back_Down | ground pound, 27/4/25, charge rise then slam | 3 | 128 re-curve |
| oni-iron-club-charge | Oni | Meshy | 242 Charged_Slash | 512 Male_Head_Down_Charge (strip root) | needs 23+ clean ticks with wind-up, drive and strike; the 654 stride has no strike | 3 | 654 stride |
| oni-light-punch | Oni | Meshy | 240 Thrust_Slash | 4 Attack | short club poke, 4/2/7 | 3 | Ryo LP file |
| oni-medium-kick | Oni | Meshy | 99 Reaping_Swing | 213 Leg_Sweep | low sweeping swing, 15/6/7, separates it from MP | 3 | Left_Slash 97 |
| oni-heavy-kick | Oni | Meshy | 238 Axe_Spin_Attack | 237 Charged_Axe_Chop | big club knockdown, 11/4/23; spin risks club clearance | 3 | Ryo HK file |
| ryo-jump-punch (move ryo-air-punch) | Ryo | Meshy, retarget to Tripo | 457 Jumping_Punch | 94 Flying_Fist_Kick | own air punch, 6/10/9 | 3 | jump-kick clip |
| ryo-fox-mirage, ryo-iron-club-charge, ryo-oni-quake, ryo-river-grab, ryo-shell-spin | Ryo Tripo | the body clip above, retargeted | same as the body clip | same | frame-for-frame twins (T0) | 0 | GREYBOX reuse |

Primary total: 17 jobs including the optional throw (16 without) = **51 credits (48 without the throw)**. The six risky ones (shell-spin, iron-club-charge, river-grab, fox-mirage, kappa-heavy-kick, oni-quake) may need an alternate: +18. **Worst case 69 credits**; request ceiling 72. Meshy balance unchecked.

## Optional Tripo route for 4 of the 5 Ryo twins (designer pastes with `!`; not recommended)
Why not the default: Tripo has presets only and none matches Shell Spin; a Tripo clip has its own duration, so a twin would have to be retimed onto the body clip's frames (0.5x-2x limit). Price: a fresh API rig 25 + 10 per animation. 4 animations = **65 of 65 credits, zero margin, no retake**. Ryo's web-app rig task is invisible to the API, so the rig must be redone from the model. Presets (rig v1.0 only): cast_a_spell (Fox Mirage), chop (Quake overhead), hug (River Grab), slash (Iron-Club Charge). Shell Spin has no preset and stays on the Meshy retarget.
```
tripo anim check game/assets/generated/characters/ryo/ryo-upright.glb
tripo anim rig game/assets/generated/characters/ryo/ryo-upright.glb --model v1.0 --rig-type biped --spec mixamo --out-format glb --name ryo-api-rig -o game/assets/generated/characters/ryo/takes/tripo
tripo anim retarget @last --animation preset:biped:cast_a_spell preset:biped:chop preset:biped:hug preset:biped:slash --out-format glb --animate-in-place --name ryo-twins -o game/assets/generated/clips/ryo-tripo/takes
```
`ryo-upright.glb` does not exist: Ryo.glb must first be stood upright by script (+90 degrees about Blender X, feet at 0, front toward glTF +Z; the 2026-11-08 prep scripts were lost). The check is free; run it first and stop if not riggable. If `@last` does not resolve to the rig, use the rig's task id from `tripo history`.

## After approval: time per clip
Meshy run and download about 2 min per job, parallelisable. Scripted cleanup (`clean_clip.py`: trim, strip root, resample 60 fps, smoothing off the key poses), `measure_clip.py`, shell or club check and retime curve: about 5 min per clean pass, 10 min with a failed check and an alternate. 17 clips is roughly 90 to 120 minutes of wall time in one batch, so takes must land by about 12:30 to leave Movesmith time before 17:00. Each Ryo twin adds about 2 min (retarget, no new take). Every setting is recorded under `## Cleanup` in each clip's spec. Retiming curves: only `trim_start`, `trim_end` and `speed_scale` reach the engine until `ClipCatalog` reads curves (flag for the Producer). A move that fails 3 takes gets an F5 substitute and a flag, never hand-keyed animation.

## Takes
None run.
