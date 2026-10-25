# Astra brief: map, HUD copy target and elder twist (YOK-109 step 1)

**Status: SETTLED 2026-10-25, ready for Astra.** The designer answered the open questions on YOK-109: (1) pine cloth across the kit, (2) the walked route is brushed ink, (3) twist wording comes from the story data (`data/story/*-intro`, `-callout`, `-label`), (4) the boss card keeps "copies your best move", (5) neutral Oni/Kappa portrait placeholders are fine for this pass, (6) the talisman shows Ryo's level. The prompts below carry those answers. Six Sol rough variations (1672×941, ROUGH PROTOTYPE) are the references. This is screen-art preparation, not a game-rule change.

## Sources

| What | Where |
|---|---|
| Layout, element inventory, states, questions | [Rough README](rough/README-map-hud-twist.md); sibling map, hud-copy-target, hud-copy-target-states and elder-twist-card PNG/SVG pairs |
| Current map and copy chip | `docs/screenshots/current/map*.png`, including midrun, changed, locked, none and twist-icons captures |
| Current fight visibility and strip timing | `docs/screenshots/current/hud_copy_*.png`, `hud_fight.png` |
| Current twist presentation | `docs/screenshots/current/twist_intro_card.png`, `twist_callout_hud.png` |
| Finished art hand | [HUD](hud.png), [reward](reward.png), [story card](story-card.png), [title](title.png); executed prompts in `prompts/prompts-all-screens-executed.json`, corrections and `prompts.json` |
| Format and adjacent screens | [Shop/dojo brief](astra-brief-shop-dojo.md) |
| Style, palette, plates | `assets/specs/style.md`, `assets/specs/ui/ui-screen-kit.md`, `ui-screen-plates.md`, `ui-night-backdrop.md` (superseded job; composition only) |
| Rules | `docs/design/rules.md`: R1, R2, Y2, Y3, T1–T6, P5, V1; `docs/design/gdd-amendments.md`: AM6 |
| Current bindings and wording | `docs/codebase-map/ui.md`, `fight.md`, `savegame.md`; `data/twists/` and elder intro/callout story JSON under `data/story/` |

## Images for Astra

All four are opaque concept PNGs, generated and saved at 1672×941 (the kit's nominal 16:9). These are layout targets, not importable plates. Text remains live in the eventual game.

| ID | Astra image | Recommended Sol reference | Reason | Output |
|---|---|---|---|---|
| M1 | Full-screen run map | [map-v1](rough/sol/map-v1.png), route treatment from [v2](rough/sol/map-v2.png) | Restrained borders separate route states from scenery; v2's brushed walked route (Q16: brushed ink). | `docs/design/screens/map.png` |
| H1 | Full-screen locked-copy HUD | [hud-copy-target-v1](rough/sol/hud-copy-target-v1.png), compare [v2](rough/sol/hud-copy-target-v2.png) | Quieter edges protect the narrow centre slot and fighter plane. Correct encounter visibility before Astra. | `docs/design/screens/hud-copy-target.png` |
| H2 | Six-state talisman sheet | H1 v1 material treatment plus [original state sheet](rough/hud-copy-target-states.png) | One consistent ofuda across A–F. The full-screen Sol variants are material references, not a replacement state sheet. | `docs/design/screens/hud-copy-target-states.png` |
| E1 | Elder intro and callout presentation board | [elder-twist-card-v1](rough/sol/elder-twist-card-v1.png) (layout only: its Kitsune mask is wrong) | Pine cloth matches the rest-stop kit (Q15: pine). | `docs/design/screens/elder-twist-card.png` |

## Source conflicts to resolve before the run

- **T6 / AM6 wins over the rough.** Sol's HUD variations preserve the requested historical elder/talisman layout for comparison. Astra H1 instead shows the locked Tanuki encounter. E1 removes the talisman from its elder thumbnail. B–D are map states, not ordinary-fight states. A is diagnostic only; T5 keeps a run target defined.
- **T4 and Q6:** the talisman and the copy strip show **Ryo's level** (Lv 3), as the designer decided. The copy never gets the evolution, which the code models as Lv 2 tuning (`CopyRule.MoveLevel`); the strip says that in words ("no modifiers, no evolution") instead of showing a second level.
- **No new API gap is inferred from the old README.** CopyTalisman, map chip, saved elder-meeting flags and TwistCopy now exist in the codebase map. First meeting is tracked per save, shared by the same elder across rows. Current callout duration is 3 s; the standing strip has a 600-tick timeout. A permanent reminder would be a separate decision.
- **Wording comes from the story data (Q3).** Intro card = `*-intro` text, first-meeting banner = `*-callout`, standing strip, map twist line and legend = `*-label`. The files are `proposed`; if Habit Writer or Rules Lawyer change them later, the live game text follows the data, not this concept.

## Exact Astra prompts

Attach the recommended Sol image as a composition/material reference, the original rough as the inventory reference, and the listed finished concept as the style reference. The prompts are self-contained. Do not copy prototype annotations or known generation errors. Run one image at a time, only after designer approval.

### M1 — Map (style: reward.png)

```text
Stylised toon-shaded game art, flat cel colour in 2-3 tone steps, clean confident ink-brush outline of even weight, muted ukiyo-e colour palette, matte surfaces, soft even lighting with no cast shadows, no photographic or painterly texture, no gloss, no baked ambient occlusion, simple readable shapes. Use case: ui-mockup. One full-bleed 1672 x 941 landscape screen concept (nominal 16:9), opaque PNG. Match the existing paper-talisman screen kit: bold serif body text, brush headings only, flat matte torn paper, restrained ink outlines. Palette: indigo #2D3A5E, ink #1D1B21, persimmon #D8632C, rice paper #F1E8D4, pine #34483B, fur white #FBF6EC, foxfire #F2A23A, shrine red #B5332B, brass #B08A48, wood dark #4A3425, wood mid #7A5535. Positions below are percentages of the full canvas. Exact English lettering is required; no filler text. Tanuki is NEVER drawn, including portrait, silhouette, animal, mask or disguise. Kitsune always wears her full white and red face mask, covering the whole face down past the chin; only foxfire eyes show through the eye slits; no exposed jaw, mouth or skin. Avoid photorealism, PBR, gloss, painterly noise, grain, neon, cast shadows, particles, extra UI, real kanji, watermark, logo, signature, extra limbs, weapons, franchise or artist references. Night bamboo valley backdrop; pale moon upper right, red torii, waterfall and bridge. Keep the central map quiet and dark. No enclosing scroll. Top-left x4–35 y4–13 heading "Choose your path". Top-centre x37–61 y4–13 paper chip, flame icon left, "COPY TARGET" above "Foxfire · Lv 2", bounded persimmon glow. Top-right x65–97 y4–13 rice-paper status "RYO · 840 / 1000", 84% red health bar, "75 coins", "row 4 of 8 done".
Row centres x7,18,29,39,50,61,71,82; boss x93. Header y17: "ROW 1", "ROW 2", "ROW 3", "ROW 4", "ROW 5 · ELDER", "ROW 6", "ROW 7", "ROW 8 · ELDER", "AFTER ROW 8". Cards width8%, height11%. Centres: row1 y50 "D Duel", "Apprentice", "cleared"; row2 y31 "D Duel", "Kitsune", "passed over", y50 "D Duel", "Oni", "cleared", y69 "D Duel", "Kappa", "passed over"; row3 y40 "D Duel", "Oni", "passed over", y60 "D Duel", "Kappa", "cleared"; row4 y50 "M Merchant", "Merchant", "YOU ARE HERE"; row5 y40 "E Elder duel", "Elder Oni", "reachable", y60 "E Elder duel", "Nine-Tails", "reachable · selected"; row6 y40 "D Duel", "Kitsune", y60 "D Duel", "Kappa"; row7 y40 "M Merchant", "Merchant", y60 "J Dojo", "Dojo"; row8 y40 "E Elder duel", "Elder Kappa", y60 "E Elder duel", "Elder Oni". Boss y50 slightly larger red-bordered text-only card: "B Boss", "The Tanuki", "waits at the end", "copies your best move". No boss illustration.
Routes behind cards, exactly these connections: row1 to all row2; row2 top and middle to row3 top, row2 middle and bottom to row3 bottom; both row3 to row4; row4 to both row5; all-to-all between adjacent two-node rows5–8; both row8 to boss. Walked route row1–row2 middle–row3 bottom–row4 as one thick persimmon brushed-ink stroke with dry-brush edges and tapered ends, painted over the night plate. Row4 to both row5 rice-white; others dim indigo. Cleared cards grey derived from ink/paper; passed over dashed/dim; current rice with persimmon border; reachable rice with indigo border; selected Nine-Tails brass/foxfire border and left triangle; later indigo with readable names. Type borders elder red, merchant pine, dojo brass. Tiny portrait areas on duel cards only; use full fox mask for Kitsune, neutral labelled portrait placeholders for Oni/Kappa. Red twist seals upper right: row5 Oni shield, Nine-Tails opposing arrows; row8 Kappa linked rings, Oni shield.
Bottom-left x4–52 y79–92 paper focus panel, reflect seal left: "NINE-TAILS · Elder duel · row 5"; red "Twist: Reflects a special used twice in a row"; "Pays 50 coins + bind 1 of 3 abilities (rares can appear)."; "Habit to punish: often jumps after a knockdown."
Bottom-right x53–96 y79–92 dark legend: "LEGEND (state · type · twist)"; swatches "cleared", "here", "reachable", "selected", "later", "passed over"; seals "Reflects a special used twice in a row", "Armour on every special", "Its throws can't be broken"; "D duel · E elder · M merchant · J dojo · B boss". Footer y95–98 key chips "ARROWS / D-PAD" "Choose route", "ENTER / A" "Go", "ESC / START" "Pause". No prototype annotations on finished image.
```

### H1 — HUD (style: hud.png)

```text
Stylised toon-shaded game art, flat cel colour in 2-3 tone steps, clean confident ink-brush outline of even weight, muted ukiyo-e colour palette, matte surfaces, soft even lighting with no cast shadows, no photographic or painterly texture, no gloss, no baked ambient occlusion, simple readable shapes. Use case: ui-mockup. One full-bleed 1672 x 941 landscape screen concept (nominal 16:9), opaque PNG. Match the existing paper-talisman screen kit: bold serif body text, brush headings only, flat matte torn paper, restrained ink outlines. Palette: indigo #2D3A5E, ink #1D1B21, persimmon #D8632C, rice paper #F1E8D4, pine #34483B, fur white #FBF6EC, foxfire #F2A23A, shrine red #B5332B, brass #B08A48, wood dark #4A3425, wood mid #7A5535. Positions below are percentages of the full canvas. Exact English lettering is required; no filler text. Tanuki is NEVER drawn, including portrait, silhouette, animal, mask or disguise. Kitsune always wears her full white and red face mask, covering the whole face down past the chin; only foxfire eyes show through the eye slits; no exposed jaw, mouth or skin. Avoid photorealism, PBR, gloss, painterly noise, grain, neon, cast shadows, particles, extra UI, real kanji, watermark, logo, signature, extra limbs, weapons, franchise or artist references. Tanuki-fight locked-copy state, correcting the historical elder rough under T6/AM6. Dark flat bamboo stage. Upper-left paper panel x0–46 y3–18: small unarmed black-haired apprentice in high indigo collar at far left; "RYO · 720 / 1000"; health72% shrine red; three meter segments 100%,40%,0%; "METER 140 / 300"; round red "B" seal; "burst ready". Upper-right paper panel x54–100 y3–18: "THE TANUKI · 640 / 1000"; health64% persimmon filling from the right; "AGGRESSIVE"; no portrait or mask on this side. Top-centre gap x47–53 y4–20 vertical ofuda (120x170 at 1920x1080 equivalent), bounded persimmon glow, double ink/red border, small abstract red lock stamp, caption "LOCKED", flame icon, "FOXFIRE", "Lv 3". No real kanji. Lower-right x52–98 y91–97 thin rice-paper strip: "THE TANUKI COPIES" above "Foxfire · Lv 3 · no modifiers, no evolution". Both show Ryo's level. Strip timing is a separate implementation question.
Gameplay plane x0–100 y48–81 contains only stage and unarmed Ryo fighter at x32 y67; opponent space at x68 stays empty because Tanuki cannot be drawn. No invented stand-in, no likeness. No HUD, particles or labels inside that plane. Keep all HUD fixed to viewport. No elder twist strip or first-meeting callout in this Tanuki encounter. No prototype annotations, timer, added buttons or hanging fittings.
```

### H2 — Talisman states (style: hud.png and reward.png)

```text
Stylised toon-shaded game art, flat cel colour in 2-3 tone steps, clean confident ink-brush outline of even weight, muted ukiyo-e colour palette, matte surfaces, soft even lighting with no cast shadows, no photographic or painterly texture, no gloss, no baked ambient occlusion, simple readable shapes. Use case: ui-mockup. One full-bleed 1672 x 941 landscape screen concept (nominal 16:9), opaque PNG. Match the existing paper-talisman screen kit: bold serif body text, brush headings only, flat matte torn paper, restrained ink outlines. Palette: indigo #2D3A5E, ink #1D1B21, persimmon #D8632C, rice paper #F1E8D4, pine #34483B, fur white #FBF6EC, foxfire #F2A23A, shrine red #B5332B, brass #B08A48, wood dark #4A3425, wood mid #7A5535. Positions below are percentages of the full canvas. Exact English lettering is required; no filler text. Tanuki is NEVER drawn, including portrait, silhouette, animal, mask or disguise. Kitsune always wears her full white and red face mask, covering the whole face down past the chin; only foxfire eyes show through the eye slits; no exposed jaw, mouth or skin. Avoid photorealism, PBR, gloss, painterly noise, grain, neon, cast shadows, particles, extra UI, real kanji, watermark, logo, signature, extra limbs, weapons, franchise or artist references. One presentation sheet, six equal cells in 3 columns by 2 rows, preserving the rough state inventory A–F. Indigo background, rice-paper/ink labels; no scenery or characters. Heading x4–96 y4–8: "Copy-target talisman · states". Cells x3–33,35–65,67–97; first row y10–50, second y54–95. One vertical ofuda at each cell's left third, same aspect ratio and local size; 120x170 at 1080p equivalent in actual HUD, enlarged here for review. Clear text to its right/below. Consistent flame/rising-paper/neutral special icons, no real kanji.
A title "A · No target yet"; dim grey ofuda "COPY TARGET", "—"; caption "Diagnostic fallback only. Not shown in ordinary fights."
B title "B · Target defined"; steady persimmon glow; ofuda "COPY TARGET", "RISING TALISMAN" wrapped to two lines, "Lv 1"; caption "Map: highest-level slotted special. Ties: most recently levelled, then lower slot."
C title "C · Target levels up"; bounded pulse ring with "+1 Lv · 0.4 s"; ofuda "COPY TARGET", "FOXFIRE", "Lv 3"; caption "Map: same target, level 2 → 3."
D title "D · Target changes"; ofuda "COPY TARGET", "ONI'S HIDE" wrapped, "Lv 3"; fading annotation "was: FOXFIRE Lv 3"; adjacent thin toast "Copy target: Oni's Hide · Lv 3"; caption "Map: old name fades; new name slides in."
E title "E · Locked at the Tanuki node"; glow stays; red abstract lock stamp; ofuda "LOCKED", "FOXFIRE", "Lv 3"; caption "Entry locks the target. No more target-change toast."
F title "F · In the Tanuki fight"; same locked ofuda "LOCKED", "FOXFIRE", "Lv 3"; adjacent thin strip "THE TANUKI COPIES" / "Foxfire · Lv 3 · no modifiers, no evolution"; caption "Tanuki fight only. No talisman in yokai or elder fights."
No animation trails crossing cells; no extra states or glyphs. No ROUGH PROTOTYPE strip on finished concept.
```

### E1 — Elder twist board (style: story-card.png)

```text
Stylised toon-shaded game art, flat cel colour in 2-3 tone steps, clean confident ink-brush outline of even weight, muted ukiyo-e colour palette, matte surfaces, soft even lighting with no cast shadows, no photographic or painterly texture, no gloss, no baked ambient occlusion, simple readable shapes. Use case: ui-mockup. One full-bleed 1672 x 941 landscape screen concept (nominal 16:9), opaque PNG. Match the existing paper-talisman screen kit: bold serif body text, brush headings only, flat matte torn paper, restrained ink outlines. Palette: indigo #2D3A5E, ink #1D1B21, persimmon #D8632C, rice paper #F1E8D4, pine #34483B, fur white #FBF6EC, foxfire #F2A23A, shrine red #B5332B, brass #B08A48, wood dark #4A3425, wood mid #7A5535. Positions below are percentages of the full canvas. Exact English lettering is required; no filler text. Tanuki is NEVER drawn, including portrait, silhouette, animal, mask or disguise. Kitsune always wears her full white and red face mask, covering the whole face down past the chin; only foxfire eyes show through the eye slits; no exposed jaw, mouth or skin. Avoid photorealism, PBR, gloss, painterly noise, grain, neon, cast shadows, particles, extra UI, real kanji, watermark, logo, signature, extra limbs, weapons, franchise or artist references. One presentation board matching the rough: large elder intro scroll in upper half; lower-left in-fight callout thumbnail; lower-right two equal elder-scroll alternatives side by side. Dim night bamboo/moon/torii backdrop. Upper hanging scroll x15–85 y9–49, cord to peg at x50 y6, wooden rods with projecting knobs, pine #34483B cloth bands, rice inset, offset paper edge lines. Left portrait x18–31 y14–44: white-furred fox spirit, indigo inner layer and red trim, full white and red face mask always on. Right text x34–76: red kicker y16 "ELDER TWIST · ROW 5"; title y21 "Nine-Tailed Kitsune"; ink underline y25; italic speaker quote y29–37 in two lines, exactly: Nine tails stir behind the mask: "Show me a trick once, little exorcist, and I will hand it back the second time." ; small red rule line y38 "Reflects a special used twice in a row"; key chip and label y41 "ENTER / A" "Continue". Bottom-right x76–82 y34–43 slightly rotated red stamp with rice opposing arrows. Text area plain.
Lower-left x6–48 y56–97 fight thumbnail. Caption above "CALLOUT · first meeting". Two top HUD panels: "RYO · 720 / 1000", "ELDER NINE-TAILS · 1000 / 1000"; no copy-target talisman (AM6); small full fox mask on right, three meter bars and B left. Thin paper banner x13–42 y65–69 with reflect seal: "ELDER TWIST · NEW"; "Use the same special twice in a row and the Nine-Tails reflects it. Change your trick." Unarmed Ryo and full-face masked fox spirit below, plane y76–89 unobstructed. Standing paper reminder at thumbnail bottom-right: "Reflects a special used twice in a row" Caption below thumbnail "Callout fades after ~3 s." No hanging fittings on either fight strip.
Lower-right two scrolls x50–73 and75–98 y60–95, equal scale, each cord/peg, wood rods, pine bands, rice inset. Neutral labelled portrait placeholders "portrait" at each upper-left; do not invent Oni/Kappa designs. First text "ELDER TWIST · ROW 5 / 8", "Elder Oni", italic quote: The Elder Oni plants its club in the snow: "Hit me mid-swing and I will finish the swing." ; red rule line "Armour on every special"; red shield stamp. Second text "ELDER TWIST · ROW 8", "Elder Kappa", italic quote: The Elder Kappa bows, not a drop spilled: "Once I have taken hold, honoured guest, there is no breaking free." ; red rule line "Its throws can't be broken"; red linked-ring stamp. Keep both rules full-size and legible. No additional story copy, no talisman in the elder thumbnail, no prototype annotations on finished board.
```


## What Astra must not change

- Preserve the original rough's element inventory, topology, positions, selected/current route, six talisman states and three elder templates. Only the explicitly listed AM6/T4 corrections override it.
- Map stays on the night plate. No enclosing handscroll, new nodes, invented rewards, prices or route choices. All duel names stay visible, including later nodes (R2).
- The three twist seals keep their meaning everywhere: opposing arrows = reflect, shield = armour, linked rings = unbreakable throws. No reflect seal on Elder Oni.
- No Tanuki likeness anywhere. No Kitsune face reveal. No invented Oni/Kappa portrait identity; retain neutral placeholders until approved art is supplied.
- Keep health direction, three meter bars, burst, temperament, key chips and long-name wrapping. No timer. No UI enters the fighter plane. No HUD cords or scroll rods.
- Glow stays bounded around the selected target. No particles or shake-coupled HUD. Lock uses an abstract stamp, not the rough's real kanji.
- No new ornament inventory. No photographic paper grain, gloss, dense hatching or decoration behind copy. Finished outputs omit green numbered dots, dashed plane guides and the ROUGH PROTOTYPE strip.

## Sol review notes

- Map v2 preserves neutral portrait boxes. Map v1 received targeted corrections for repeated fox masks, leftover portrait wells and the row-5 Oni seal.
- HUD v1 fills the portrait areas; v2 retains portrait placeholders. Both deliberately retain the historical elder/talisman combination, for layout comparison only.
- Elder v1's Kitsune portrait leaves the chin and jaw bare below the mask. That breaks the full-mask rule; use it for layout only. The E1 prompt asks for the full mask explicitly.
- Elder v1 uses pine bands; v2 uses indigo. Both retain the complete callout thumbnail and two other twist templates.
- The green guides and numbered dots are review annotations. Do not import them. The prompt text, not generated lettering, is the copy authority.

## Designer answers (2026-10-25, YOK-109)

| Question | Answer | Where it lands |
|---|---|---|
| README 15: cloth colour | Pine for the whole kit | E1 |
| README 16: walked route | Brushed ink | M1 |
| Twist wording | Story data (`-intro`, `-callout`, `-label`) | M1, E1 |
| README 10: boss card before the reveal | Keep "The Tanuki / copies your best move" | M1 |
| Oni/Kappa portraits | Neutral placeholders for this pass | M1, E1 |
| Talisman level | Ryo's level (Lv 3); the strip says "no modifiers, no evolution" | H1, H2 |

Still open, not blocking Astra: README 1–5, 7, 11–13 (map orientation, status line, intro repetition, 720p name wrapping). The concepts keep the rough's answers to these.

## Sol generation record

Built-in image_gen; six initial image requests, two per screen, plus two targeted map-v1 corrections. Every request specifies 1672×941 and a visible ROUGH PROTOTYPE strip. No Astra or game code. Exact Sol prompts are in [the prompt manifest](rough/sol/prompts.json). Inputs are the corresponding original rough PNG plus reward.png (map), hud.png (HUD), or story-card.png (elder). v1 asks for restrained paper/ink and pine cloth. v2 explores stronger brush edges, a darker backdrop, a brushed persimmon route and indigo elder cloth. Image-model deviations are review notes, never instructions to change rules.

## Astra run record (2026-10-25)

One take per image, run from this brief with the Sol variant, the rough and the finished-style concept attached. All four are 1672x941, with no prototype strip and no annotations.

| ID | Output | Verdict | Notes |
|---|---|---|---|
| M1 | [map.png](map.png) | Accept as layout and style target, with misses | Brushed persimmon route, legend, focus panel and twist seals are right. Misses: the Oni and Kappa cards get invented faces (the brief asked for placeholders), the merchant card uses a money bag marked "$", and some card text is garbled ("papred tivee", "Kuel", the Nine-Tails card). Text stays live in the game, so the garbling does not reach the build. Retake only if the designer wants the portraits fixed. |
| H1 | [hud-copy-target.png](hud-copy-target.png) | Accept | Locked talisman and strip both read Lv 3. The Tanuki's side is empty and the plane is clear. Ryo has no red headband here, unlike his other art. |
| H2 | [hud-copy-target-states.png](hud-copy-target-states.png) | Accept | All six states are present with the right text. B (Rising Talisman) and D (Oni's Hide) reuse the flame icon; the build uses each move's own icon (YOK-81). |
| E1 | [elder-twist-card.png](elder-twist-card.png) | Accept | The full mask stays on, and the story-data text is verbatim. The reflect stamp is drawn as circling arrows where the map draws opposing arrows; the build should use one icon for both. |

