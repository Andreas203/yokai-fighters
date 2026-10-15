# Yokai Fighters — GDD Amendments

Changes to `Yokai_Fighters_GDD_Extended.pdf` (and the Short PDF) that the designer has approved since the PDF was written. **Where an amendment and the PDF disagree, the amendment wins.** `docs/design/rules.md` reflects both. Each amendment names the PDF sections it replaces; everything it does not name stands as written.

---

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

## AM2 — The binding line promises freedom (2026-10-06)

**Decision.** Ryo's line at every binding changes from a promise to *give back* to a promise to *set the yokai's spirit free*. He still apologises first. The ending pays off the new promise.

### PDF sections replaced

| PDF section | Was | Now |
|---|---|---|
| Story card schedule, every binding (and the §1 walkthrough) | "Forgive me, {yokai}. I'll give it back." | "Forgive me, {yokai}. I'll set your spirit free." |
| Story card schedule, Win 3 (ending) | Ryo binds the Tanuki and releases every yokai, his own included: "I said I'd give it back." | Same scene: "I said I'd set you free." |

Rule S3 in `docs/design/rules.md` carries the new wording.

---

## AM3 — Quitting mid-node restarts the node (2026-10-11)

**Decision.** The game saves only at node boundaries (run start, node entered, node completed). Quitting to the title inside a node and choosing Continue puts Ryo back at the start of that node with the health, coins and slots he entered with. A fight in progress restarts; a heal, rest or reward pick taken but not completed with the node is not kept. This is allowed: it is not an exploit to close. The route pick is still committed (R1): Continue always re-enters the same node.

### Sections replaced

| Where | Was | Now |
|---|---|---|
| GDD §6 run structure (save behaviour) | Not specified | As above. Rule M5 in `docs/design/rules.md`. |

---

## AM4 — Rulings on the Tanuki, the shop and coins (2026-10-11 and 2026-10-12)

**Decisions.** Answers the designer gave where the PDF was silent or could be read two ways.

- **The Tanuki's copy mirrors Ryo's slot.** The copied special sits in the slot it has on Ryo's side. If a starter sits in that slot on the Tanuki's side, the copy replaces it; the other starter stays in its own slot. So when Ryo has drafted a special over a starter and that special is the copy, the Tanuki does not keep the starter Ryo replaced. A copied starter replaces its own Lv 1 version.
- **The Tanuki's habit.** It throws when the player keeps blocking, 60% of the time. The counter is to break the throw.
- **The shop sells modifiers as well as specials.** "Common abilities" in the shop means common specials and common modifiers. A bought modifier attaches to one of Ryo's specials, chosen by the player, under the same rule as a drafted modifier, and is borrowed ink like a bought special.
- **Releasing a binding costs Ryo 40 coins.** It is a paid service to free a slot, not a sale.
- **Coins are not capped,** for now.

### GDD text affected

| Topic | Was | Now |
|---|---|---|
| The Tanuki's kit | "Ryo's normals, his starters and the one copied special" | The same, with the copy in Ryo's slot for it and replacing a starter there. Rule T0. |
| Yokai habits | No habit named for the Tanuki | Throws at a blocking player, 60%. Rule Y2. |
| The merchant | "sells common abilities … (50 coins); release a binding (40)" | Common specials and common modifiers, 50 each; releasing costs Ryo 40. Rule R6. |
| Coins | No cap stated | No cap, for now. Rule R8. |
