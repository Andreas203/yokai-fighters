# Yokai Fighters: Clip Needs (YOK-14)

For Asset Scout (YOK-29) and Clip Matcher. Lengths are rough clip lengths at 60 fps, derived from the rules.md first-pass targets (startup + active + recovery) where they exist. Per F2, a matched clip's real timing overrides these numbers; Movesmith keeps the clip's truth and flags the delta.

Rig: all clips are humanoid and retargeted onto Ryo and onto each yokai model. Pack requirement: humanoid skeleton (Mixamo-style or Unity/Godot humanoid), in-place (no root motion) unless noted, toon-shader friendly.

Demo column: `D` = needed by the YOK-39 Ryo-vs-Kitsune demo (priority 1 for the shortlist). Fighters: R = Ryo (and the Tanuki when shapeshifted, T0), K = Kitsune, O = Oni, B = Kappa.

## 1. Shared reactions (listed once, used by R, K, O, B)

| ID | Clip | Must show | Frames | Demo | Notes |
|---|---|---|---|---|---|
| R01 | Idle | Fighting stance, subtle breathing | 60-90 loop | D | Seamless loop |
| R02 | Walk forward | Guarded forward step cycle | ~30 loop | D | Walk crosses screen in ~2.5 s [C2]; G4 retarget gate clip |
| R03 | Walk back | Guarded backward step cycle | ~30 loop | D | Mirrors R02 speed; G4 |
| R04 | Dash | Short forward burst (18f dash [C2]); also used by Fox Step cancel | 18 | D | A forward lunge/run-start. Back-dash not required |
| R05 | Jump | Crouch-takeoff, rise, apex, fall, land | 40 airborne + ~4 land | D | Neutral jump; forward and back jumps reuse with root motion in data |
| R06 | Crouch | Down transition then held crouch, then stand-up | ~6 in, hold, ~6 out | D | Hold frame must be a clean pose |
| R07 | Block high | Standing guard, brief impact flex | ~10 (hold last pose) | D | Hold back [C3] |
| R08 | Block low | Crouching guard, impact flex | ~10 (hold) | D | Must be blockable crouching [C3] |
| R09 | Hit high | Head/torso snap-back | ~16 | D | Duration is set by data hitstun; clip must hold or loop cleanly |
| R10 | Hit low | Knees/legs buckle | ~16 | D | Same rules as R09 |
| R11 | Knockdown | Fall to ground, land flat. Also the generic throw victim [C4, T] and the airborne result for Heaven Seal / Crushing Charge | ~30 | D | Single-fighter only. Tail (K) and shell (B) clipping on landing |
| R12 | Wake-up | Rise from the ground to stance | ~25 | D | Kitsune habit is jumping after knockdown [Y2], so rise must read clearly |
| R13 | KO | Defeat collapse, ends on held pose | ~60 | D | Plays under the round-ending blow |
| R14 | Win pose | Victory pose / brief taunt, hold | ~90 | D | Ryo and yokai each need one (can be shared) |
| R15 | Binding (round-ending blow) | **Not a new clip.** Last hit plays at half speed for 30 frames [V4], defender plays R13, then an ink-stroke VFX seals them into a talisman | 30 (half speed) | D | Reuses the finishing move's own clip + R13 + VFX. No bespoke animation |
| R16 | Burst | Defensive flare/shove: breaks combo, 20 invulnerable frames [C6] | ~20 | D | Rules require a burst but the ticket list omitted it; fallback below |

Fallbacks: R16 burst: use R07 (block high) pose with a shockwave VFX. R14: reuse R01 idle with a victory VFX if no clip. Throw break (T02) is separate below.

## 2. Throws

| ID | Clip | Must show | Frames | Fighters | Demo | Notes |
|---|---|---|---|---|---|---|
| T01 | Generic grab (attacker) | Reach and grab, then toss/pull. Defender plays R11 | ~30 (startup 5) | R K O B | D | Never paired [C4]. **Also River Grab's clip** (River Grab 6/2/30, 130 dmg). Fast reach is needed (startup 5-6) |
| T02 | Throw break | Both fighters recoil apart after a throw is broken within the 7-frame window | ~15 | R K O B | D | No dedicated clip: fallback is both fighters play R09 pushback. Hard rule: not paired |

## 3. Normals (Ryo, and the same six slots for each yokai)

Yokai normals are an assumption (see `move_list.md` section 5). One purchase per row, retargeted to all four fighters.

| ID | Normal | Must show | Frames (S+A+R) | Fighters | Demo | Rig risk |
|---|---|---|---|---|---|---|
| N01 | Light punch | Fast straight jab | 13 (4/2/7) | R K O B | D | Low |
| N02 | Medium punch | Cross punch | 21 (6/3/12) | R K O B | D | Low |
| N03 | Heavy punch | Big wind-up hook/uppercut, early frames work as anti-air | 34 (10/4/20) | R K O B | D | **G4 gate clip.** Oni club in hand, Kitsune tails swinging, Kappa shell during twist |
| N04 | Light kick | Quick low shin kick | 16 (5/2/9) | R K O B | D | Tails (K) |
| N05 | Medium kick | Long-range front kick | 24 (7/3/14) | R K O B | D | Tails (K) |
| N06 | Heavy kick | Big round/side kick that knocks down | 38 (12/4/22) | R K O B | D | Largest swing: tails/shell/club all at risk |

Yokai normals clip IDs: Kitsune D (N01-N06), Oni and Kappa use N01-N06 not for the demo.

## 4. Specials

| ID | Special | Fighter | Must show | Frames | Demo | Hard to find? |
|---|---|---|---|---|---|---|
| S01 | Spirit Wave | R | Palm/talisman push, projectile spawn on release | 43 (13/-/30) | D | Low. Common "fireball push" |
| S02 | Rising Talisman | R | Rising uppercut or upward talisman slap, airborne feet-off-ground late | 41 (5/8/28) | D | Low. Common uppercut |
| S03 | Foxfire | K (also Ryo when drafted, and Tanuki copy) | Two-hand flick or cast releasing a slow flame | 49 (15/4/30) | D | Low-medium. Any one-handed cast/throw |
| S04 | Fox Mirage | K | Vanish and re-appear behind the opponent | 28 (18/-/10) | - | **YES.** See flag 1 |
| S05 | Iron-Club Charge | O | Lunging forward charge, shoulder or club lead | 46 (16/6/24) | - | **YES.** See flag 2 |
| S06 | Oni Quake | O | Overhead two-hand ground slam, hits low | 53 (22/5/26) | - | Medium. See flag 3 |
| S07 | Shell Spin | B | In-place spinning body with long active window | 48 (10/18/20) | - | **YES.** See flag 4 |
| (T01) | River Grab | B | Reuses T01 grab clip | 38 (6/2/30) | - | Low (reuse) but see flag 5 |

## 5. Lv 3 evolutions (all data-only, A5/A10)

| ID | Evolution | Reuses clip | New clip? | Preset / VFX need |
|---|---|---|---|---|
| E01 | Great Wave | S01 | No | Bigger wave, absorb flash |
| E02 | Heaven Seal | S02 | No | Seal trail; launch result plays R11 on the opponent |
| E03 | Piercing Foxfire | S03 | No | Elongated white flame |
| E04 | Mirage Feint | S04 | No | Decoy = translucent copy of model in R01 idle pose |
| E05 | Crushing Charge | S05 | No | Dust trail; wall bounce plays R09/R11 |
| E06 | Aftershock | S06 | No | Second quake: ground-crack VFX only, clip not replayed |
| E07 | Whirlpool Grab | T01 | No | Teal water swirl; range is data |
| E08 | Torrent Spin | S07 | No | Water ribbon trail; forward travel via root-motion data |

All 8 only need a visual preset reference for Clip Matcher.

## 6. Tanuki boss

Fights as Ryo with Ryo's model and clips [T0]; the copied special reuses its existing clip (S01-S07 or T01). No new combat clips.

| ID | Need | Frames | Notes / fallback |
|---|---|---|---|
| B01 | Merchant idle/offer | 60-90 loop | A cheerful travelling-merchant model on a node screen. Could be a still model with R01 idle retargeted if the merchant is a humanoid; otherwise a non-fighting character model |
| B02 | Disguise-drop reveal | ~60-90 | Slow camera push-in [V7]. Fallback: pose snap from B01 to Tanuki model with smoke VFX, no clip |
| B03 | Shapeshift into Ryo | ~30 | Fallback: model swap behind a VFX burst, no clip. A Tanuki true-form model is needed for the reveal and win-3 binding story card (stills acceptable) |

## 7. Hard-to-find flags (F5 fallbacks, no bespoke animation)

1. **Fox Mirage (S04), teleport.** Packs rarely have a vanish/re-appear clip. Fallback: any short crouch-to-stand or backstep clip played with a smoke/fade VFX, with the teleport itself in move data. Mechanics (18 startup, 10 recovery, behind opponent) unchanged. Evolution decoy is a model copy, not a clip.
2. **Iron-Club Charge (S05), forward charge with 1-hit armour.** A true shoulder-charge is uncommon. Fallback: a forward lunge or running tackle; else R04 dash start + heavy-punch strike pose (N03 held). Armour is data.
3. **Oni Quake (S06), ground pound.** Overhead slam is findable in melee packs but rarely with a hits-low read. Fallback: any two-hand overhead smash; "low" is a data hitbox property [C3].
4. **Shell Spin (S07), 18-frame active spin.** Needs a sustained in-place spin and it is the highest retarget-clipping risk (shell). Fallback: whirlwind / tornado-kick loop, trimmed to length. If the shell clips badly, Kappa uses its folklore human guise [G4].
5. **River Grab and generic grab (T01), fast reach.** Needs a 5-6 frame reach to the grab; many grab clips have a slow wind-up. Fallback: a short lunging arm grab; if the clip's startup is slower, Movesmith keeps the clip's truth and flags the delta to the designer (the rules.md C4 startup 5 may change).
6. **Burst (R16), Throw break (T02).** Not in the original scope list; they have no specific clips. Fallbacks as given above, VFX only.
7. **Knockdown (R11) onto back for Kappa (shell) and Kitsune (tails).** Prefer a lying-on-front or side-collapse variant if retargeting clips the shell/tails.

## 8. Retarget risk summary for the G4 gate

Gate clips: walk (R02/R03), heavy (N03/N06), throw (T01). Test on each yokai.

| Yokai | Feature | Risk clips |
|---|---|---|
| Kitsune | Nine tails | R02/R03 walk, N04-N06 kicks, R11 landing, R05 jump |
| Oni | Club (hand prop) and bulk | N03/N06 heavy swings, S05 charge, S06 slam |
| Kappa | Shell on back, wide body | R11 knockdown on back, S07 spin, N03/N06 twists, T01 grab |

Failure fallback: the yokai uses its folklore human guise [G4].

## 9. Counts

- Entries in this document: 16 shared reactions + 2 throws + 6 normal slots + 7 special clips (River Grab reuses T01) + 8 evolutions (no new clips) + 3 boss needs = 42 rows, covering 61 must-ship move instances once yokai normals (18) and special-slot reuses are expanded.
- Unique clips to source (the shopping list): 14 reaction clips (R01-R14; R15 is composed, R16 and T02 are fallback-driven) + T01 + 6 normals + 7 specials = about 28, plus up to 2 optional boss clips (B01, B02).
- YOK-39 demo set: R01-R16, T01, T02, N01-N06, S01-S03 = 27 rows (about 25 unique clips).

## 10. Rules relied on

P1, C2, C3, C4, C6, C8, F2, F3, F5, X4, A5, A10, T0, V4, V5, V7, G4.
