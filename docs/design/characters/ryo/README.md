# Ryo — character sketch v0.1

Apprentice exorcist under Master Sōen. He binds yokai into ofuda and fights with what they lend him, and he has promised to give every one of them back.

Status: first-pass concept, 2026-10-05. Not final art.

## Description

- **Silhouette:** lean and upright, with a wide stance. Black undercut hair with one lock over his right eye. A high collar zipped to the nose, so his eyes carry every expression.
- **Outfit:** indigo coat with spiral brass buttons, closed left over right like a kimono, its tail cut long on one side with a persimmon lining. Shimenawa rope sash with shide paper and an ofuda pouch at the hip. Indigo trousers, kyahan shin wraps and jika-tabi. Ofuda earring.
- **Face:** sharp tapered eyelids and a vermilion binding mark under his left eye.
- **Power:** ink-black energy with brush edges, matching his hit sparks (V5). Four ofuda wrap his right forearm, one per special slot (A1). Two carry red seals, for Spirit Wave and Rising Talisman. His ready pose holds an ofuda up between two fingers.

## Palette (V1)

| Colour | Hex | Used on |
|---|---|---|
| Indigo | `#2D3A5E` | coat, collar, trousers |
| Ink | `#1D1B21` | hair, energy, tabi |
| Persimmon | `#D8632C` | coat lining, rope, glow |
| Rice paper | `#F1E8D4` | ofuda, wraps |
| Brass | `#B08A48` | spiral buttons |

## Files

| File | What it is |
|---|---|
| `sheet.html` | Full model sheet: annotated front sketch, head, silhouette test, starter move studies, open questions. Open in a browser. |
| `ryo-meshy-front.png` | Clean front view for Meshy image-to-3D, 1024×2112: A-pose (arms ~16° out), white background, no text or effects. |
| `ryo-meshy-front.svg` | Vector source of the Meshy image. |

The current Meshy image is a flat visual reference, not final concept art. Asset Smith specs the front/side/back turnaround from it and, once you approve the job, runs it and Image-to-3D through the Meshy MCP; only the selected front view goes to Image-to-3D. The turnaround drops the held ofuda, ink energy and trailing talisman strip, which would become stray geometry. The generated mesh is auto-rigged in Meshy, and its clips come from the Meshy animation pipeline (clip first, F6).

## Open questions (designer)

1. Keep the collar over his mouth, or open it for story-card art and the Win 3 ending?
2. Light the forearm wraps per filled slot on the model? In the Tanuki fight the copied move's wrap could glow to match the HUD talisman (T6).
3. Coat tail must be bone-driven or baked into clips so the 60-tick loop stays deterministic; record the choice in Asset Smith's rig plan (`assets/specs/ryo.md`).
4. Name kanji: 涼 (cool), 了 (to finish, to settle), or keep katakana リョウ.
