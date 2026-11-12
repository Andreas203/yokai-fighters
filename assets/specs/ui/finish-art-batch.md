# Finish-art batch (placeholder removal)

Status: approved by the designer 2026-11-12 (~29 takes plus up to 4 retakes, retakes only for acceptance failures; changed prompts allowed within the cap). Generator: Astra through `codex exec -m gpt-6-astra` (credits unreported by Codex). Rules: V1, V5, F6, AM1.

## Jobs

1. **22 ability icons** (`game/assets/generated/ui/ability-icon-<id>.png`, 256x256 RGBA): the five specials reuse the approved prompts in `ui/ui-ability-icons.md`; 14 modifiers and 3 cancels use the same template (`HEAD` sentence of that spec) with a motif read from `data/modifiers`, `data/cancels`. Diamond colour follows the source: shrine red = kitsune modifiers, near-black ink = oni, water teal = kappa, indigo = cancels. The three accepted icons (spirit-wave, rising-talisman, foxfire) are attached as style references.
2. **4 portraits** (480x320 RGBA, like `ryo-portrait.png`): oni, kappa, apprentice, dojo-master. Style reference `ryo-portrait.png` (copy its technique: heavy tapered ink outline, solid black hatching, flat colour, torn lower edge); identity from the locked turnarounds in `docs/design/characters/`. The apprentice is the row-1 trainee (AM13: not a yokai, plays the Kitsune kit): drawn as a young human trainee with a plain fox half-mask on the forehead; the designer may redirect. The dojo master follows `docs/design/screens/astra-brief-shop-dojo.md` D1 (Master Soen, elderly woman).
3. **3 hit-spark textures** (`game/vfx/hit_sparks/{hit,block,counter}.png`, 256x256): white ink burst on solid black, luminance converted to alpha, tinted by source colour (V5).

## Acceptance (per file)

Icons: genuine alpha outside the diamond, bold at 90 px, palette within the listed colours, no lettering, distinct from its neighbours. Portraits: matches ryo-portrait hand and line weight, transparent background, no text, identity matches the turnaround. Sparks: white on clear, alpha core over 240, mid band under 25%.

## Takes

2026-11-12. Raw takes kept in `assets/specs/ui/finish-art-raw/` (outside game/ so Godot does not import them); VFX raws are there too. Codex does not report credits.

| Group | Takes | Result |
|---|---|---|
| 3 hit sparks | 3 | ACCEPT (white on clear; `block` is small, scale it up in the preset) |
| 4 portraits | 4 | ACCEPT: oni, kappa, apprentice, dojo-master match ryo-portrait line and torn edge; identity matches the turnarounds |
| 5 special icons | 5 | ACCEPT |
| 5 modifier icons | 5 (fox-patience, brute-recovery, iron-will, cucumber-gift, illusion-cloak, ninefold-spark: 6 takes) | ACCEPT. Notes: cucumber-gift draws pine sprigs (scenery tell, minor); iron-will came out on an indigo diamond, not ink |
| remaining 12 icons | 0 | BLOCKED: Codex hit the account usage limit at 10:29, resets 15:23. Pending: club-momentum, onis-hide, rage-ember, river-pull, slippery-skin, sumo-stance, tricksters-angle, will-o-wisp, fox-step, oni-chain, undertow |

Takes spent: 8 VFX + 3 + 4 + 5 + 6 = 26 (12 icons outstanding; 17 were launched after the 5 specials, 6 produced an image, 11 returned nothing). Retakes used: 0 of 4.
