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
