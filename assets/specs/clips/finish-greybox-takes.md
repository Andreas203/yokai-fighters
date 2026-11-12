# Finish the greybox clips: take list (spec only, nothing run, 0 credits spent)

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
