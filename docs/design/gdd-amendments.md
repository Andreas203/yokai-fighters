# Yokai Fighters — GDD Amendments

Changes to `Yokai_Fighters_GDD_Extended.pdf` (and the Short PDF) that the designer has approved since the PDF was written. **Where an amendment and the PDF disagree, the amendment wins.** `docs/design/rules.md` reflects both. Each amendment names the PDF sections it replaces; everything it does not name stands as written.

## Index

| AM | Approved | Change | Rules | Ticket / PR |
|---|---|---|---|---|
| [AM1](#am1) | 2026-10-05 | Visual assets are generated, sound is sourced | F2, F5, V1 | — |
| [AM2](#am2) | 2026-10-06 | The binding line promises freedom | S3 | — |
| [AM3](#am3) | 2026-10-11 | Quitting mid-node restarts the node | M5 | — |
| [AM4](#am4) | 2026-10-11/12 | Rulings on the Tanuki, the shop and coins | T0, Y2, R6, R8 | PR #105 |
| [AM5](#am5) | 2026-10-15 | Rigging and animation get their own specialists and tools | F2, F5, F6, G4 | YOK-110–115 |
| [AM6](#am6) | 2026-10-15 | The copy-target talisman shows only in the Tanuki fight | T6 | — |
| [AM7](#am7) | 2026-10-16 | Fighter concept art and turnarounds may come from Astra | F6 | YOK-102 |
| [AM8](#am8) | 2026-10-16 | Closer fight camera, screen walls brought in (superseded in part by AM9) | E1, C2 | YOK-120, PR #133 |
| [AM9](#am9) | 2026-10-16 | Dynamic fight camera in the style of Guilty Gear Strive | E1, V7 | YOK-129, PR #145 |
| [AM10](#am10) | 2026-10-17 | The camera frames the tallest fighter in the fight | E1, V7 | YOK-133 |
| [AM11](#am11) | 2026-10-20 | Sumo Stance: holding gives a faster dash, not more damage | modifier table | YOK-142, YOK-171 |
| [AM12](#am12) | 2026-10-20 | Slippery Skin breaks throws during startup | modifier table, E12 | YOK-172 |
| [AM13](#am13) | 2026-10-21 | The apprentice has its own behaviour profile | Y1, G5 | YOK-174 |
| [AM14](#am14) | 2026-10-21 | The Tanuki's throw habit triggers after one blocked hit | Y2 | YOK-177, PR #214 |
| [AM15](#am15) | 2026-10-20 | The forecast's expected cost is the copied move's damage per hit | T6 | YOK-173, PR #217 |
| [AM16](#am16) | 2026-10-19 | Tripo's skeleton becomes the shared skeleton | F6 | YOK-146 |
| [AM17](#am17) | 2026-10-25 | A story-card style video on first meeting each yokai (extra, not MVP) | S6, header | YOK-207 |
| [AM18](#am18) | 2026-10-25 | The map is a handscroll that shows only the next choice | R2 | YOK-109, PR #245 |

### Adding an amendment
An amendment is how the GDD changes; the PDF and its transcriptions are never edited. In the same PR:
1. Add `<a id="amN"></a>` and a `## AMN — <change> (<date>)` section at the end, with its **Sections replaced** table (Where / Was / Now).
2. Add its row to the index above (`tools/check_docs.py` fails if a heading and the index disagree).
3. Update the rules it changes in `docs/design/rules.md`, tagged `[AMN]`.
4. Add or extend the `> **Amended by**` callout under each GDD section it replaces in `gdd-extended.md` and `gdd-short.md`.
5. Add one line under "Design amendments" in `docs/decision-log.md`.

---

<a id="am1"></a>

## AM1 — Visual assets are generated, sound is sourced (2026-10-05)

**Decision.** Characters, animation, stages, UI textures and VFX textures are **generated**, not bought. Sound effects and music are still **sourced** from packs. Every generation job runs through the **Meshy MCP** (`meshy-mcp-server`). The crew writes the spec and runs the job, but only after the designer approves that job and its credit cost; the designer controls the credit spend the same way they control purchases.

**Pipeline.**
- **3D models**: concept turnaround (front, side, back) from Meshy's 2D image models (`meshy_image_to_image` on the character's reference image) → front view into **Meshy Image-to-3D** → **Meshy auto-rig** onto its standard humanoid skeleton → Godot import (glTF) under the shared toon shader with ink outlines.
- **Animation**: clips come from **Meshy's animation tools** (its preset animation library or its prompt-driven generation), applied to each fighter's auto-rig. Because every fighter shares the Meshy humanoid skeleton, one clip serves all four fighters, as one bought pack would have. Clips are in place (no root motion); movement lives in move data.
- **Stages**: generated props and set pieces (Meshy) assembled in Godot over generated painted backdrops (Meshy 2D image models).
- **UI and VFX textures**: paper, brush stroke, ink, red seal, hit sparks and level-preset effects come from Meshy's 2D image models, then get cleaned up into tileable or alpha textures.
- **Sound and music**: sourced packs, shortlisted by the crew, bought by the designer.

**What does not change.** Clip first, then frame data (F2): a clip is generated, measured, and only then does Movesmith write frame data from its real timing. No hand-keyed animation. No paired throws (C4). Modifiers and evolutions stay data-only (A5, A10). The Tanuki still uses Ryo's model and clips (T0). The week-1 go/no-go gate still tests walk, heavy and throw on each yokai, with the folklore human guise as the fallback (G4).

**Take cap.** A move gets at most **3 generation takes** (first-pass, designer may change). If none is usable, the move becomes a mechanically equivalent move on an approved clip (F5).

**Licence.** Every generator's output has to be cleared for commercial use in a shipped game on the plan the designer uses. Free-tier terms may differ from paid tiers, so check them before generating final assets. Sourced sound keeps the licence column it always had.

### PDF sections replaced

| PDF section | Was | Now |
|---|---|---|
| §3.4 Frame data | "Clip Matcher picks each move's animation clip first" | Clip Matcher specs each move's clip, the designer approves the take, and Clip Matcher runs it through the Meshy MCP and measures it. Movesmith then writes frame data from that clip's real timing. |
| §3.7 Game feel | "Hits must feel heavy even with pre-bought animation" | "…even with generated animation". |
| §9 Agents table | Asset Scout: "Shortlists model, animation, sound and music packs." | **Asset Smith**: "Writes generation specs for models, stages and textures, runs approved jobs through the Meshy MCP, and checks what comes back." — *Ryo and the yokai look like one world.* **Sound Scout**: "Shortlists sound and music packs." — *Every hit and every row sounds like the same world.* |
| §9 Agents table | Clip Matcher: "Picks each clip first, then adds level presets and frame-timed sound." | "Specs each move's clip and measures the generated take, then adds level presets and frame-timed sound." |
| §9 Figure 7 | Asset Scout → Clip Matcher ("packs shortlisted") | Asset Smith (models rigged) → Clip Matcher (clip generated and measured) → Movesmith…; Sound Scout (packs shortlisted) → Clip Matcher (sound cues). |
| §10.1 Engine, assets | "All assets are pre-bought and unified by a toon shader." | "Visual assets are generated through the Meshy MCP (3D, rigs, animation and 2D images); sound and music are bought. Everything visual is unified by a toon shader." |
| §10.1 API | "web search for the Asset Scout" | "web search for the Sound Scout". |
| §10.3 Token budget | Asset Scout 45 searches × (15,000 + 1,500) = 742,500. Clip Matcher 30 clip + 12 cue batches = 324,000. | Sound Scout 20 searches × (15,000 + 1,500) = 330,000. Asset Smith ~30 assets × 2 passes (spec, acceptance) × (6,000 + 2,000) = 480,000. Clip Matcher 30 spec + 30 measure batches × (6,000 + 2,000) + 12 cue batches × (5,000 + 2,000) = 564,000. Total ≈ 27.2M; ceiling ≈ 40.8M with 50% contingency. |
| §10.3 Designer time | "10 purchases and gate" | Unchanged for now; approving and reviewing generation jobs may need more than 10 h (see open questions). |
| §10.4 Week 1 | "Move list, retargeting gate, packs bought" | "Move list, fighters generated and auto-rigged, retargeting gate on generated clips, sound packs bought". |
| §11.2 Supers | "each needs bespoke animation" | "each needs its own generated animation and camera work". Still out of the 5-week build. |
| §12 Risks | "Purchased packs lack a clip for a planned move" | "Generation can't produce a usable clip for a planned move within the take cap" → same mitigation: a mechanically equivalent move on an approved clip. |
| §12 Risks | — | **New:** "Generated assets drift apart in style" → shared style block (`assets/specs/style.md`), one toon shader and outline pass, and Asset Smith acceptance checks against the V1 palette and silhouette. |
| §12 Risks | — | **New:** "Generated clips are noisy (foot slide, jitter, inconsistent timing between takes)" → Clip Matcher measures every take on 60-tick frames and rejects jitter on hit frames; take cap, then F5 substitution. |
| §12 Open questions | "the asset spend" | "the generation credit spend and the sound/music spend; the generator plan tier (licence); designer hours for approving generation jobs". |

---

<a id="am2"></a>

## AM2 — The binding line promises freedom (2026-10-06)

**Decision.** Ryo's line at every binding changes from a promise to *give back* to a promise to *set the yokai's spirit free*. He still apologises first. The ending pays off the new promise.

### PDF sections replaced

| PDF section | Was | Now |
|---|---|---|
| Story card schedule, every binding (and the §1 walkthrough) | "Forgive me, {yokai}. I'll give it back." | "Forgive me, {yokai}. I'll set your spirit free." |
| Story card schedule, Win 3 (ending) | Ryo binds the Tanuki and releases every yokai, his own included: "I said I'd give it back." | Same scene: "I said I'd set you free." |

Rule S3 in `docs/design/rules.md` carries the new wording.

---

<a id="am3"></a>

## AM3 — Quitting mid-node restarts the node (2026-10-11)

**Decision.** The game saves only at node boundaries (run start, node entered, node completed). Quitting to the title inside a node and choosing Continue puts Ryo back at the start of that node with the health, coins and slots he entered with. A fight in progress restarts; a heal, rest or reward pick taken but not completed with the node is not kept. This is allowed: it is not an exploit to close. The route pick is still committed (R1): Continue always re-enters the same node.

### Sections replaced

| Where | Was | Now |
|---|---|---|
| GDD §6 run structure (save behaviour) | Not specified | As above. Rule M5 in `docs/design/rules.md`. |

---

<a id="am4"></a>

## AM4 — Rulings on the Tanuki, the shop and coins (2026-10-11 and 2026-10-12)

**Decisions.** Answers the designer gave where the PDF was silent or could be read two ways.

- **The Tanuki's copy mirrors Ryo's slot.** The copied special sits in the slot it has on Ryo's side. If a starter sits in that slot on the Tanuki's side, the copy replaces it; the other starter stays in its own slot. So when Ryo has drafted a special over a starter and that special is the copy, the Tanuki does not keep the starter Ryo replaced. A copied starter replaces its own Lv 1 version.
- **The Tanuki's habit.** It throws when the player keeps blocking, 60% of the time. The counter is to break the throw. *(Trigger reworded by AM14: one blocked hit.)*
- **The shop sells modifiers as well as specials.** "Common abilities" in the shop means common specials and common modifiers. A bought modifier attaches to one of Ryo's specials, chosen by the player, under the same rule as a drafted modifier, and is borrowed ink like a bought special.
- **Releasing a binding costs Ryo 40 coins.** It is a paid service to free a slot, not a sale.
- **Coins are not capped,** for now.

### GDD text affected

| Topic | Was | Now |
|---|---|---|
| The Tanuki's kit | "Ryo's normals, his starters and the one copied special" | The same, with the copy in Ryo's slot for it and replacing a starter there. Rule T0. |
| Yokai habits | No habit named for the Tanuki | Throws at a blocking player, 60%. Rule Y2. Trigger superseded by AM14 (after one blocked hit). |
| The merchant | "sells common abilities … (50 coins); release a binding (40)" | Common specials and common modifiers, 50 each; releasing costs Ryo 40. Rule R6. |
| Coins | No cap stated | No cap, for now. Rule R8. |

---

<a id="am5"></a>

## AM5 — Rigging and animation get their own specialists and tools (2026-10-15)

**Decision.** A fighting game feels good only when its rigs and clips are very clean, so rigging and animation each get a specialist, and the pipeline gains tools beyond one generator. It amends AM1 where AM1 names the Meshy MCP as the only route.

**The crew.**
- **Rig Specialist** (`rig-specialist`, new) owns every rig: the auto-rig job and its settings, rig checks in Blender, extra bones for tails, sleeves, coat tail, shell and club, attachment bones, the bone map onto the shared skeleton, and the G4 retarget gate. Rigging moves out of Asset Smith.
- **Animator** (`animator`, was Clip Matcher) owns every clip: the clip spec, the take, the scripted cleanup pass, measuring it, its retiming curve and its frame-timed sound cues.
- **Asset Smith** keeps models, stages and textures, and takes over the level presets (V8), which are VFX work.

**Tools.**
- **Generators**: **Tripo** or **Meshy**, the designer's choice per job; Tripo is the likely default. Either runs through its MCP when one is installed, otherwise the designer runs the approved job in the generator's own app and drops the files in the repo for the specialist to check.
- **Auto-rigging**: the generator's own auto-rig (Tripo or Meshy) and **AccuRIG 2** (Reallusion, free) are both tried on the next fighter, and the Rig Specialist's acceptance check picks the better rig. AccuRIG 2 is a desktop app, so the designer runs it from the specialist's settings.
- **Blender** (4.1, headless Python): rig checks, weight repair, extra bones, retargeting, cleanup (strip root motion, resample to 60 fps, Butterworth smoothing on hit frames, trim) and glTF export. Scripted only: no hand-placed keys.
- **Auto-Rig Pro** (Blender add-on, bought by the designer): retargeting clips between skeletons and game-ready export.
- **Clip measurement**: a scripted check that reports foot slide and joint jitter per 60-tick frame and renders the hit-frame stills, so a take's verdict rests on numbers.
- **Clip sources** beyond the generators: **video motion capture** (Rokoko Vision, Move.ai Move One, DeepMotion or Plask) of a performance the designer records, and **ActorCore** motion-capture packs bought by the designer. **Cascadeur** may be used only for its automatic physics pass on an existing clip, never to pose keys.

**Rig source of truth.** Every clip plays on one shared humanoid skeleton, today the Meshy skeleton that Ryo and Kitsune passed the gate on *(replaced by AM16: Tripo's skeleton since 2026-10-19)*. A rig from another tool joins only through the Rig Specialist's bone map, and must play the existing walk, heavy and throw clips before any clip is made for it.

**Retiming curves (F2).** A measured clip may carry a **retiming curve**: a short list of segments mapping game frames onto clip time, so startup can be snappy, the pose holds through the active frames and recovery eases out. Each segment plays between 0.5× and 2× the clip's speed (first pass, designer may change), and a held pose lasts at most 4 frames. Frame data is written from the clip *as retimed*. Retiming stays data, played through `AnimationPlayer.Seek()` (F3).

**What "hand-keyed" means now (F5).** Allowed: generated clips, motion capture of a real performance, bought motion-capture packs, scripted cleanup and retiming, and Cascadeur's automatic physics pass. Not allowed: posing or editing keys by hand. The take cap of 3 counts takes from every source together.

**What does not change.** Every generation job, capture session and purchase needs the designer's approval and its cost. Clips are in place. No paired throws (C4). The Tanuki uses Ryo's model and clips (T0). G4 still gates each yokai with the folklore human guise as its fallback. Every tool's output must be cleared for commercial use on the plan the designer uses.

### Sections replaced

| Where | Was | Now |
|---|---|---|
| AM1 pipeline, 3D models and animation | Meshy only, Meshy auto-rig, Meshy animation tools | Tripo or Meshy; generator auto-rig or AccuRIG 2; Blender cleanup and retargeting; motion capture and ActorCore as clip sources. Rules F5, F6. |
| §3.4 Frame data / rule F2 | Frame data from the clip's real timing | Frame data from the clip's real timing as retimed by its curve. |
| §9 Agents table | Clip Matcher: "Specs each move's clip and measures the generated take, then adds level presets and frame-timed sound." | **Rig Specialist**: "Rigs every fighter, checks it, and keeps every rig on the shared skeleton." **Animator**: "Specs, cleans, measures and retimes each move's clip, then adds frame-timed sound." Asset Smith adds level presets. |
| §12 Risks | — | **New:** "Rigs from different tools disagree" → one shared skeleton and a bone map; a new rig must play the existing gate clips first. |

---

<a id="am6"></a>

## AM6 — The copy-target talisman shows only in the Tanuki fight (2026-10-15)

**Decision.** The fight HUD shows the copy-target talisman only in the Tanuki's fight. In every other fight (the yokai, the elders) the HUD carries no copy target. Outside fights the target stays visible all run: the map still shows the glowing copy-target talisman, and the rest-stop forecast still names the move, its level and its expected cost.

**Why.** In a yokai fight the copy plays no part: only the Tanuki uses it. Showing it there reads as if the copy rule were active in that fight. The map and the rest stop are where the player plans around the copy, so the warning stays there.

**What does not change.** The copy rule itself (T0–T5): the lock on entering the Tanuki's node, the target, ties, strength and "always defined". The row 4 story card stays templated to the copy target (S3). In the Tanuki fight the talisman shows the locked copy (T1).

### PDF sections replaced

| Where | Was | Now |
|---|---|---|
| §5.3 The Tanuki, point 6 | "Visible all run: the target's talisman glows in the HUD, and the rest-stop forecast names it." | "Visible all run outside fights: the map shows the target's glowing talisman and the rest-stop forecast names it. In fights, the HUD shows it only against the Tanuki." Rule T6. |
| Figure 4, Fight HUD mockup | "glowing: copy target" in a fight against a patient Kitsune | No copy target in a yokai fight; the talisman appears in the Tanuki fight only. |
| §8 Screens, Fight HUD row | "Health, meter, burst, copy target, elder twist reminder (Figure 4)." | "Health, meter, burst, elder twist reminder; the copy target in the Tanuki fight only (Figure 4, AM6)." |

---

<a id="am7"></a>

## AM7 — Fighter concept art and turnarounds may come from Astra (2026-10-16)

**Decision.** A fighter's concept front and its front/side/back turnaround may be made in **Astra** (`gpt-6-astra`, run by the designer in Codex) instead of a generator's 2D image model. The designer chooses the source per character. For Oni, Kappa and the Tanuki the designer chose Astra (YOK-102). Asset Smith writes the settled brief (`docs/design/characters/astra-brief.md`), the designer runs it and drops the sheets in `docs/design/characters/<name>/`, and Asset Smith checks them before any mesh job is listed for approval. Meshes, rigs and clips still come from the tools AM1 and AM5 name.

**Why.** Astra's character art is much stronger than the generators' 2D models, and the turnaround is the image every later step copies. Astra is expensive, so it runs only from a settled brief, never for exploration.

**What does not change.** Every generation job and its cost still needs the designer's approval. The sheet must be flat-lit with no baked shading, on a plain background, in T-pose, with no held items, so it can feed Image-to-3D; baked shading is a retake reason. The style block (`assets/specs/style.md`) and the V1 palette still apply. The output must be cleared for commercial use on the designer's plan.

### Sections replaced

| Where | Was | Now |
|---|---|---|
| AM1 pipeline, 3D models | Concept turnaround from Meshy's 2D image models | From a generator's 2D image model or from Astra, the designer's choice per character. Rule F6. |

---

<a id="am8"></a>

## AM8 — Closer fight camera, screen walls brought in (2026-10-15, approved 2026-10-16)

**Status.** Approved by the designer on 2026-10-16 (merge of PR #133). The designer asked to "try bringing the walls in" (YOK-120, 2026-10-15).

**Decision.** The fight camera sits closer to the fighters and stays fixed in distance. One screen at the gameplay plane is **1,440 units** (7.2 m), was 1,920 (9.6 m). The screen walls come in with it: fighters are at most **960 units** apart, was 1,520, so both rigged models stay in frame whichever way each faces. A model reaches up to 240 units ahead of its position and 200 behind; a fighter facing away from the other (after a cross-up, a throw or a back dash) shows its 240-unit front reach on the outside, so the walls budget the larger reach on both sides: 960 + 2 × 240 = 1,440. (The YOK-39 walls, 1,520 + 2 × 200 = 1,920, budgeted only the back reach, and a fighter facing away could clip by up to 40 units; test-breaker probes on YOK-120 found it.) `SimConfig` derives the walls from the screen width and the model reach, so the two cannot drift apart. The stage stays **3,840 units** wide (two of the old 1,920-unit screens, now about 2.7 of the new ones), so the stage art and the part of it the camera can ever show are unchanged. The camera still follows the fighters' midpoint and stops where a cornered fighter's model (up to 240 units ahead, 200 behind) meets the screen edge. The screen width and stage width are tunable in one place (`SimConfig`); the walls follow from the screen width.

**Why.** Bigger fighters on screen: at 1,920 units a 1.8 m fighter filled about a third of the screen height; at 1,440 units, about 44%. A fixed closer camera keeps V7 (no zoom with distance); bringing the walls in is what keeps both fighters in view.

**What does not change.** V7: the camera is steady except the slow push-in on the Tanuki reveal and the round-ending blow. F4: perspective camera, ~25° FOV; the camera moves closer (distance follows from the screen width at that FOV), the FOV is not narrowed. E1's 200 units per metre. E17's "on screen" means the sim's screen: the widest view (1,540 units) centred on the fighters, so when the camera is zoomed in a projectile can leave the visible view before it despawns. Walk speed stays 12.8 units per tick (C2's "~2.5 s" now refers to the original 1,920-unit width; across the closer screen a walk takes ~1.9 s). Dash and jump distances, hitboxes and pushback stay in units, so they now cover a larger share of the screen.

### Sections replaced

| Where | Was | Now |
|---|---|---|
| rules.md E1 (scale; written rule, not in the PDF) | "one screen is 1,920 units (9.6 m); the stage is two screens wide; fighters are at most 1,520 units apart … 1,520 + 2 × 200 = 1,920" | "one screen is 1,440 units (7.2 m); the stage is 3,840 units wide; fighters are at most 960 units apart … 960 + 2 × 240 = 1,440, either facing" |
| §3.2 Fight basics, Movement row | "Walk crosses the screen in about 2.5 seconds" | Walk speed unchanged: about 2.5 seconds across 1,920 units, about 1.9 seconds across the closer 1,440-unit screen. Rule C2. |
| §3.1 Presentation, Figure 2 | Camera3D at ~25° FOV | Unchanged FOV; the camera is closer to the plane. |

---

<a id="am9"></a>

## AM9 — Dynamic fight camera in the style of Guilty Gear Strive (2026-10-16, approved 2026-10-16)

**Status.** Approved by the designer on 2026-10-16 (PR #145, after reviewing the captures). Drafted for YOK-129 after the designer said on 2026-10-16: "I still don't love the camera, I want a view closer to Marvel Tokon or Guilty Gear Strive." It amends V7 and E1 and supersedes AM8's fixed camera; AM8's screen-wall derivation stays.

**Decision.** The fight camera zooms with the distance between the fighters, as in Arc System Works games. It eases in when they are close and out as they separate. It never snaps, and both models stay in frame whichever way each faces. Close in, the view is **1,060 units** wide and Ryo's fighting stance fills about 52% of the screen height. At the screen walls the view is **1,540 units** wide and he fills about 36%. The camera stays at chest height, 1.2 m, at every zoom. Its pitch goes from 0.5° down close in to 3° up at the widest zoom, so the fighters' feet sit about 88–90% of the way down the frame at every distance: a low, slightly heroic angle with no empty band of floor (designer review of the captures, 2026-10-16). On a jump it rises partway: 45% of the jump height, plus whatever keeps the higher fighter's head below the HUD strip. It also eases out a little, 1 unit of width per unit of height. The floor may leave the bottom of the frame. The screen walls follow the widest zoom: fighters are at most **1,060 units** apart, because 1,060 + 2 × 240 = 1,540 (AM8 had 960 + 2 × 240 = 1,440). The camera still follows the fighters' midpoint and stops at the stage edge. At the widest zoom the stage art covers the view at both edges, with the camera raised for a jump. The stage stays 3,840 units wide. The V7 push-ins layer on top of the zoom: the round-ending blow pushes in 12% over the KO slow-down, and the Tanuki reveal has a hook. Neither push-in can crop a model. Every value is in one place (`CameraConfig`), apart from the widest view, which is the sim's screen (`SimConfig.ViewWidth`).

**Why.** The designer wants tall fighters, a low horizon and a tight frame when close. A fixed camera can't give that and also show both fighters at the screen walls: AM8's 1,440 units put a fighter at about 44% of the screen height at every distance. With the zoom, close-range play reads much bigger and the walls can go back out from 960 to 1,060. The walls could go further, toward the ~1,520 the brief allowed, but the fighters would then shrink below the 35–40% target at full separation. 1,060 is the widest wall that keeps about 36%.

The first draft looked down 1° and rose to 1.4 m when zoomed out, which left the bottom ~20% of the frame as empty floor at the screen walls; the designer asked for the feet lower.

**What does not change.** F4: perspective camera, ~25° FOV. The camera dollies; the lens does not narrow. The camera is presentation only. It uses floats, but only in presentation code; it eases by a fixed amount per sim tick, never by frame time, so replays and captures reproduce. Nothing in the sim, the AI or the replay reads it. The sim keeps its integer screen (the widest view) for the screen walls and for where projectiles leave the screen, so `StateHash` changes only through the wall value. V3: the shake is still 2–4 screen pixels at every zoom, converted through the current view width. Walk speed stays 12.8 units per tick (about 2.0 s across the widest 1,540-unit view). E1's 200 units per metre stays.

### Sections replaced

| Where | Was | Now |
|---|---|---|
| rules.md V7 | "Camera steady except slow push-in on Tanuki reveal and round-ending blow." | The camera zooms with the fighters' separation (eased, deterministic per tick, both models always in frame), rises partway on jumps without cropping heads, and stops at the stage edge. The slow push-ins on the Tanuki reveal and the round-ending blow layer on top. |
| rules.md E1 (scale) | AM8: "one screen is 1,440 units … fighters are at most 960 units apart … 960 + 2 × 240 = 1,440. The camera stays at a fixed distance" | "The camera's view is 1,060 units close in and 1,540 at the widest; fighters are at most 1,060 units apart … 1,060 + 2 × 240 = 1,540, either facing." |
| AM8 Decision | "The fight camera sits closer to the fighters and stays fixed in distance. One screen … is 1,440 units" | Superseded by the dynamic zoom above. AM8's derivation of the walls from the widest screen and the model reach stays. |
| §3.1 Presentation, Figure 2 | Camera3D at ~25° FOV | Unchanged FOV; the camera dollies between two distances at 1.2 m and tilts from 0.5° down to 3° up as it pulls back. |

---

<a id="am10"></a>

## AM10 — The camera frames the tallest fighter in the fight (2026-10-17)

**Status.** Decided by the designer on 2026-10-17 for YOK-133, after Code Critic's review: "frame for the tallest fighter in the fight", allow the wider view, amend E1 and V7, accept a 0.25 m rise per tick. It amends AM9 for fights with a tall body. Ryo vs the Kitsune (and every body no taller than 1.9 m) keeps AM9's camera exactly.

**Decision.** The camera frames each fight for the highest point of its tallest body, over every clip that body plays, body mesh and props included. The Oni's is his club: 3.124 m in his heavy kick, 3.032 m in the HP overhead slam, so he is framed for 3.15 m (`CameraConfig.TallBodyTopMeters`). In such a tall-body fight:
- The club stays below the HUD band. Where keeping it there would push the lower fighter's feet off the bottom of the frame, the camera dollies out instead. The feet then sit no lower than **1,040 px** of 1,080 (about 96% down the frame, against AM9's ~90%).
- The view may widen past the sim's 1,540-unit screen, up to about **1,900 units** at a jump apex. Only the presentation widens: the sim's screen, the screen walls (1,060 units) and where projectiles leave the screen stay at 1,540.
- The camera may sit above 1.2 m while the HUD floor binds, and may rise up to **0.25 m per sim tick** (the first tick of a jump lifts the club at once). The dolly-out is limited to 80 units per tick, except when a fight starts.

**What does not change.** Everything else in AM9, F4 and E1. The camera is still presentation only, eased by a fixed step per sim tick. Nothing in the sim, the AI or the replay reads it, and `StateHash` does not change.

### Sections replaced

| Where | Was | Now |
|---|---|---|
| rules.md E1 (scale) | "… 1,540 units (7.7 m) at the widest zoom, which is the sim's screen" | Same, except that in a tall-body fight the presentation view may widen to about 1,900 units; the sim's screen stays 1,540. |
| rules.md V7 | "chest height (1.2 m) … feet ~90% down the frame" | Same, except that in a tall-body fight the camera frames the tallest body's highest point under the HUD, may sit above 1.2 m while that floor binds, rises up to 0.25 m per tick, and keeps the feet no lower than 1,040 px. |

---

<a id="am11"></a>

## AM11 — Sumo Stance: holding gives a faster dash, not more damage (2026-10-20)

**Status.** Decided by the designer on 2026-10-20 for YOK-142. It answers the open question "what does holding charge mean for Sumo Stance without a new animation".

**Decision.** The Kappa modifier Sumo Stance (A10, E7): the player holds the special's button before the special starts. The longer the hold, the faster the special's dash, up to a maximum speed. Nothing is added to damage. The hold uses no new animation. The sim steps the dash in integer units per tick, and the held ticks are part of the state hash. What the Tanuki does when it copies a charged special stays open. Engine work: YOK-171.

### PDF sections replaced

| Where | Was | Now |
|---|---|---|
| GDD modifier table (§ modifiers), Sumo Stance | "Hold the button to charge for +20% damage." | "Hold the button before the special: the longer the hold, the faster its dash, up to a maximum speed." |
| rules.md modifier table | "Hold to charge for +20% damage" | The new line above. |

---

<a id="am12"></a>

## AM12 — Slippery Skin breaks throws during startup (2026-10-20)

**Status.** Decided by the designer on 2026-10-20. It answers the open question on Slippery Skin against E12/E17, where "throw-invulnerable" would have done nothing.

**Decision.** Slippery Skin automatically BREAKS a throw attempted against the carrier during the carrying move's startup. The result is a clash with no damage, the same as throw vs throw (C4). E17's "throws ignore throw-invulnerability data" stays as written, because Slippery Skin is a break and not invulnerability. Engine, schema and data work: YOK-172.

### PDF sections replaced

| Where | Was | Now |
|---|---|---|
| GDD modifier table (§ modifiers), Slippery Skin | "Throw-invulnerable during startup." | "Automatically breaks a throw attempted during this move's startup: a clash, no damage, like throw vs throw." |
| rules.md modifier table | "Throw-invulnerable during startup" | The new line above. |

<a id="am13"></a>

## AM13 — The apprentice has its own behaviour profile (2026-10-21)

**Status.** Decided by the designer on 2026-10-21 ("the apprentice gets its own profile").

**Decision.** The row-1 apprentice fights with its own behaviour profile (`data/profiles/apprentice.json`, `yokai: "apprentice"`) instead of rolling one of the Kitsune's two temperaments. Its body and kit stay the Kitsune's (`plays_as: "kitsune"` in the node-scene table); the node names the profile, so it is never rolled and never joins the Kitsune's list. That makes 8 profiles: 3 yokai × 2 temperaments + the apprentice + the Tanuki. The apprentice is not a yokai, so Y2's "each yokai: two temperaments" does not apply to it.

**Why.** With the seeded per-node temperament roll, an apprentice that rolled the patient Kitsune held full screen and the fight timed out, which a newcomer's first duel must not do. Work: YOK-174.

### PDF sections replaced

| Where | Was | Now |
|---|---|---|
| GDD extended §10.4 Schedule and cut gates (p. 12), week 2 row; GDD short §4.6 Schedule and scope, week 2 row | "33 abilities, 7 profiles, …" | "33 abilities, 8 profiles, …" |
| rules.md G5, W2 | "33 abilities, 7 profiles" | "33 abilities, 8 profiles [AM13]" |
| rules.md Y1 | "7 profiles: 3 yokai × 2 temperaments + Tanuki." | The new line above, with [AM13]. |

<a id="am14"></a>

## AM14 — The Tanuki's throw habit triggers after one blocked hit (2026-10-21)

**Status.** Decided by the designer on 2026-10-21.

**Decision.** The Tanuki's habit (AM4) throws after ONE blocked hit, the first blockstun it sees, 60% of the time. It no longer waits for the player to "keep blocking". The counter is unchanged: break the throw. Work: YOK-177.

**Why.** Designer decision 2026-10-21: the trigger in AM4 was ambiguous; the designer chose one blocked hit.

### Text replaced

| Where | Was | Now |
|---|---|---|
| AM4, "The Tanuki's habit" (and its table row) | "throws when the player keeps blocking, 60% of the time" | "throws after one blocked hit, the first blockstun it sees, 60%" |
| rules.md Y2 | "throws when the player keeps blocking (60%) [AM4]" | "throws after one blocked hit, the first blockstun it sees (60%) [AM4, AM14]" |

<a id="am15"></a>

## AM15 — The forecast's expected cost is the copied move's damage per hit (2026-10-20)

**Status.** Decided by the designer on 2026-10-20, written as an amendment on 2026-10-24 (YOK-196). Built in YOK-173 (PR #217).

**Decision.** The rest-stop forecast's "expected cost" is the copied move's damage per hit at Ryo's level, read from move data. It is always available, so there is no "not enough fights" state. It is no longer estimated from the player's own fights.

**Why.** An estimate from the player's own fights has nothing to show at the row-4 rest stop of a first run and swings with a few fights; a number from data is defined from the first stop and tells the player what the copied move does.

### PDF sections replaced

| Where | Was | Now |
|---|---|---|
| GDD extended §6.2 Health and routes (p. 10) | "its expected cost from the player's own fights" | "its expected cost: the copied move's damage per hit at Ryo's level, from data" |
| GDD short §2.6 Health and tuning | "the Tanuki's expected cost from the player's own fights" | As above. |
| rules.md T6 | "expected cost … (decided 2026-10-20)" | Same text, tagged [AM15]. |

<a id="am16"></a>

## AM16 — Tripo's skeleton becomes the shared skeleton (2026-10-19)

**Status.** Decided by the designer on 2026-10-19 on YOK-146 (Option A), written as an amendment on 2026-10-24 (YOK-196).

**Decision.** The shared humanoid skeleton that every clip plays on is Tripo's, rebuilt from the Tripo Ryo (`assets/specs/rigs/skeleton.md`). Bones carry bare names (`Hips`, not `mixamorig:Hips`), and fingers are merged into the hand (25 joints). The Meshy skeleton stays on file as "Previous shared skeleton (Meshy)" (`skeleton-meshy.json`) for Kitsune, Oni and Kappa until their Tripo rebuilds (YOK-147/148/149). Ryo's heavy punch and heavy kick come from video motion capture.

**Why.** The Tripo Ryo was chosen over the Meshy one; making its skeleton the shared one avoids a bone map for the player character, whose clips matter most.

**What does not change.** One shared skeleton, the Rig Specialist's bone map for every other rig, and the G4 gate (AM5). Every job still needs the designer's approval and its cost.

### Text replaced

| Where | Was | Now |
|---|---|---|
| AM5, "Rig source of truth" | "today the Meshy skeleton that Ryo and Kitsune passed the gate on" | "Tripo's skeleton, from the Tripo Ryo; the Meshy skeleton stays as the previous one until each rig is rebuilt" |
| rules.md F6 | "one shared humanoid skeleton (today Meshy's)" | "one shared humanoid skeleton (Tripo's since 2026-10-19 [AM16]; the Meshy skeleton stays for rigs not yet rebuilt)" |

<a id="am17"></a>

## AM17 — A story-card style video on first meeting each yokai (extra, not MVP) (2026-10-25)

**Status.** Decided by the designer on 2026-10-25: "add as a new requirement that a short story card style video plays when you first encounter a yokai, this should be extra and not part of the mvp".

**Decision.** The first time the player meets a yokai, a short video in the style of the story cards plays before the duel: paper, ink and brush in the talisman look (V1), carrying at most two sentences of text (S2) in the game's tone (S1). It plays once per yokai per save and is recorded with story progress (M4), so it never depends on unlocks.

It is **extra, not part of the MVP**. It is built only once every MVP item is done, it never delays MVP work, and it is the first thing dropped if time runs short. The card schedule the build ships with (S3) does not depend on it.

**Open.** How long it runs; whether it can be skipped; whether the elders and the Tanuki get one (the elders already have intro cards, Y3, and the Tanuki has its reveal) and whether the row-1 apprentice counts as meeting the Kitsune; how it is made (a generated video needs a tool and a spend that AM1 and AM5 don't name, or it can be an animated card in the engine); and whether it can land before the week-3 feature freeze (G5) or only in the polish weeks with the designer's say-so.

**What does not change.** The S3 card schedule and the never-cut story cards (G1). The elder intro cards still show on every elder entry (Y3). Every generation job and its cost still need the designer's approval (AM1, AM5).

### Sections replaced

| Where | Was | Now |
|---|---|---|
| GDD extended §2.3 Story structure; GDD short §1.4 Audience and story | No beat on first meeting a yokai | A story-card style video on the first meeting with each yokai, extra and not MVP. Rule S6. |
| rules.md header | Only **Full game** marks work outside the build | **Extra (not MVP)** marks work that may be built only after every MVP item is done [AM17]. |

<a id="am18"></a>

## AM18 — The map is a handscroll that shows only the next choice (2026-10-25)

**Status.** Decided by the designer on 2026-10-25 on YOK-109 (PR #245): "I want the map to look like a scroll with a path looking like it was painted with a brush, currently the map is too cramped, it should be scrollable left to right and nodes should not appear until it is time for selection."

**Decision.** The map is an unrolled paper handscroll, row 1 at the left and the Tanuki at the right, wider than the screen and panned left to right. The route Ryo has walked is painted as a brush stroke. The map draws only the nodes Ryo has cleared, the node he stands on and the nodes he can choose next. Later rows, the Tanuki included, stay blank paper until it is their turn to be chosen.

**Why.** The designer found the map too cramped with all nine columns on one screen.

**What does not change.** The route graph, its rows and node types (R1): rows 5 and 8 are still always elders, row 4 always the merchant. Every node that is shown still names its yokai (R2) and carries its elder-twist icon (Y3). The copy-target talisman still shows on the map (T6). The reveal is presentation only: the run, the saves (M5), the harness and the test bot do not change.

### Sections replaced

| Where | Was | Now |
|---|---|---|
| GDD extended §6.1 The map | The whole map in view (Figure 6), "so choosing a path is choosing which powers you can earn" | A horizontal handscroll that shows only the walked path and the next choice; each choice still shows which yokai waits at each node. |
| GDD short §1.2 Core loop | "The map has 8 rows, and you pick one node per row" | Unchanged, but the map reveals each row only when it is time to pick from it. |
| rules.md R2 | "Each node shows which yokai waits there." | Each node shows which yokai waits there. The map is a horizontal handscroll with the walked route painted as a brush stroke; it shows only cleared nodes, Ryo's node and the nodes he can choose next, so later rows, the Tanuki included, stay hidden until their turn [AM18]. |
