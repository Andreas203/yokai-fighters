# Ability icons, batch 1: five specials (YOK-80)

Status: SPEC ONLY, revision 2 (2026-10-25; first draft 2026-10-17). No job run, no credits spent. Waits on the designer's choice of tool and approval of the job list and cost (ticket YOK-80, AM1).

Extends the four existing diamond icons in [`ui-screen-plates.md`](ui-screen-plates.md) (spirit-wave, rising-talisman, foxfire, fallback). Same template, with the changes listed under "Template". Rules: A11 and A9 (the five and their rares), A8 (the motif belongs only to the owning yokai), V1 (look), V5 (source colours, a hint only), AM1 (generated visuals; the designer approves job and cost). The ticket does not wire the icons into any screen.

## Revision 2026-10-25 (what changed since 2026-10-17)

- Moved from `assets/specs/ability-icons.md` to `assets/specs/ui/` with the other `ui-*.md` specs. The link to the four existing prompts now points at `ui/ui-screen-plates.md`.
- Tool: recommends the built-in imagegen that made the first four icons (0 Meshy credits) instead of Meshy. Meshy is unavailable on this machine (`.mcp.json` is per-machine and missing), the first draft's full prompts were over Meshy's 600-character field limit, and a different generator risks drifting from the four accepted icons. The three candidates are priced side by side under "Tool and cost".
- Kappa diamond: water teal `#3E7F86` (in `style.md` since 2026-10-20, YOK-151, and the colour of the Kappa hit spark), replacing the rice diamond chosen while teal was outside the palette. Oni stays ink. Kitsune stays shrine red (the ticket fixes it).
- Fox Mirage motif replaced. The first draft's three fading flames are how the Kitsune sheet describes **Foxfire** ("leaves fading copies behind it"), on the same shrine-red diamond as the Foxfire icon: the duplicate risk the first draft itself flagged. The sheet's Fox Mirage study is a dissolve and reappear behind the opponent, and the template bans masks, so the new motif is a vacated ring, a swoosh and a solid pearl.
- Shell Spin and River Grab motifs rebuilt for a teal diamond and to keep clear of the pine fallback's white swirl ring (Shell Spin is a filled disc, River Grab a mirrored pair of currents). "Turtle" and "arrowhead" are out of the prompts: they invite an animal or a weapon.
- Ids checked against `data/` and the design (section Ids). Acceptance numbers measured from the four accepted icons. Registration step and probe note added.
- Decision-log flag: `docs/decision-log.md` (2026-10-18, "approve all") lists "the YOK-80 icon job (30 credits)" as approved. That was the first draft's chain (Meshy `nano-banana-2`, 30 credits). This revision recommends a different tool and changes the prompts, so it asks again. Nothing here runs on the old approval.

## Ids

The ticket's ids are kept. None of the five is in `data/moves/` yet (it holds `foxfire`, `spirit-wave`, `rising-talisman` and the normals); they are designed in `docs/moves/move_list.md` and the rules.md specials table, and Epic D has not authored them. The ids follow the convention of the three authored specials: move id = kebab-case display name, and the clip id adds the fighter (clip `kitsune-foxfire` for move `foxfire`), so the existing clips `oni-quake` and `oni-iron-club-charge` fit moves `oni-quake` and `iron-club-charge`.

| Id (file and lookup key) | Display name | Source | Rarity | Starts locked (A11) | In `data/moves/` | Existing clip |
|---|---|---|---|---|---|---|
| `fox-mirage` | Fox Mirage | Kitsune | rare | yes | no | none (S04, flagged in `docs/moves/clip_needs.md`) |
| `iron-club-charge` | Iron-Club Charge | Oni | common | no | no | `oni-iron-club-charge` |
| `oni-quake` | Oni Quake | Oni | rare | yes | no | `oni-quake` |
| `river-grab` | River Grab | Kappa | common | no | no | none (reuses the generic grab) |
| `shell-spin` | Shell Spin | Kappa | rare | yes | no | none yet (`assets/specs/clips/yok-134-kappa-clip-kit.md`) |

When movesmith authors each move, re-check its id. If it differs, rename the PNG and the lookup key; no regeneration is needed.

**Open default (ticket): locked specials get art now, in the same batch.** Fox Mirage, Oni Quake and Shell Spin are all in the job list; the move list and reward screens will show them once unlocked. If the designer flips this, only `iron-club-charge` and `river-grab` run (2 first-pass takes, ceiling 6) and the other three stay on the fallback until unlocked.

## Template

The four accepted prompts (see `ui-screen-plates.md`), word for word, except:
1. Negatives read "people, characters, animals, faces, weapons, masks": the ticket's "no characters" added, plus animals and faces because the Kitsune and Kappa motifs could drift into a creature.
2. The two Kappa prompts add "water teal #3E7F86" to the palette list. The ticket's six-colour list predates the 2026-10-20 additions to `style.md`.
3. `style.md` says every prompt starts with the shared style block. The four accepted icons use this compact UI template instead, which carries the same intent (flat matte cel, ink outline, palette). Kept, because the ticket says "same template".

Take 1 is text only, as the four were. No reference image is attached.

## Diamond colours

Existing diamonds: indigo (spirit-wave), persimmon (rising-talisman), shrine red (foxfire), pine (fallback). Within a source the two icons share the diamond colour and differ by motif and silhouette. If that is not enough at 90 px, the fix is a motif change, not a new colour.

| Source | Diamond | Motif colours | V5 hint | Why |
|---|---|---|---|---|
| Kitsune | shrine red `#B5332B` | rice `#F1E8D4` | foxfire orange `#F2A23A` | Fixed by the ticket, and the existing Foxfire is already red, so Kitsune stays one colour. Foxfire orange was rejected: 20.8 dE from persimmon (13.0 under deuteranopia). |
| Oni | ink `#1D1B21` | rice, persimmon accents | oni red | Oni red in code (`HitSparks.OniRed`) is provisionally shrine red, which Kitsune holds, and the proposed oni skin red `#A8372C` is 2.5 dE from shrine red: a red Oni diamond would duplicate Foxfire. Ink is the only dark diamond not in use. The rough ink edge is the border. |
| Kappa | water teal `#3E7F86` | rice, ink accents | kappa teal | The Kappa spark colour, in the palette since 2026-10-20. Rice on teal is 39.6 dE, enough for a white motif. A mid-dark filled diamond also avoids the flat-pale-plate alpha failure (see Tool and cost). |

Colour distance, CIEDE2000 on the hex values, deuteranopia and protanopia by the Machado matrices. Nearest existing diamond for each new colour:

| New diamond | Normal | Deuteranopia | Protanopia | Nearest existing |
|---|---|---|---|---|
| ink | 16.0 | 14.5 | 15.9 | indigo (normal, protanopia), pine (deuteranopia) |
| water teal | 22.0 | 20.9 | 23.4 | pine (normal, protanopia), indigo (deuteranopia) |

Ink against teal is 37.6 and ink against shrine red 35.8. The weakest pair in the set is ink against indigo (16.0), marginally below the existing persimmon against shrine red (17.4; 14.0 under deuteranopia). That is the risk to check first at 90 px (Acceptance 5). The lever is a retake with a thin rice-white keyline just inside the Oni diamond's edge, or the designer naming another Oni colour. Wood dark `#4A3425` was tested and rejected (7.4 dE to pine under deuteranopia); brass `#B08A48` collapses into persimmon under deuteranopia (4.5).

## Shared prompt (full)

> Create one standalone square game UI diamond move icon, tight framing with small transparent padding, genuine transparent alpha outside diamond. Thin rough ink #1D1B21 diamond border, flat matte ukiyo-e cel ink-brush illustration, [MOTIF COLOUR] motif. Bold readable silhouette at 90px. No lettering, numbers, real kanji, watermark, people, characters, animals, faces, weapons, masks, Tanuki or scenery. Palette only indigo #2D3A5E, ink #1D1B21, rice #F1E8D4, pine #34483B, shrine red #B5332B, persimmon #D8632C[, water teal #3E7F86 on Kappa jobs]. [DIAMOND + MOTIF SENTENCE]

Output for every job: `game/assets/generated/ui/ability-icon-<id>.png`, 256x256 RGBA.

## Jobs

### 1. fox-mirage (Kitsune, rare, teleport behind the opponent)

Motif: she dissolves and reappears behind. A hollow ring of broken dry-brush dashes (where she was) dissolving into flecks, a bold swoosh arcing over to a solid round pearl (where she lands). No fox, mask, flame or figure; Foxfire owns the flame.

Prompt (754 characters): `Create one standalone square game UI diamond move icon, tight framing with small transparent padding, genuine transparent alpha outside diamond. Thin rough ink #1D1B21 diamond border, flat matte ukiyo-e cel ink-brush illustration, rice paper #F1E8D4 motif. Bold readable silhouette at 90px. No lettering, numbers, real kanji, watermark, people, characters, animals, faces, weapons, masks, Tanuki or scenery. Palette only indigo #2D3A5E, ink #1D1B21, rice #F1E8D4, pine #34483B, shrine red #B5332B, persimmon #D8632C. Shrine-red diamond containing, on the left, a ring of broken rice-white dry-brush dashes dissolving into flecks, on the right a solid rice-white round pearl, and one bold curved rice-white swoosh arcing from the broken ring to the pearl.`

Retake lever: if the broken ring reads as noise, make it a thin unbroken ring and keep the fade in the swoosh; if it still reads as a second Foxfire, make both shapes plain discs.

### 2. iron-club-charge (Oni, common, forward charge with 1-hit armour)

Motif: the rush, with the club told through round iron studs (no club or weapon is drawn, per the template): a bold chevron pointing right with studs along its edge, three speed streaks behind it, one persimmon impact ring at the tip.

Prompt (826 characters): `Create one standalone square game UI diamond move icon, tight framing with small transparent padding, genuine transparent alpha outside diamond. Thin rough ink #1D1B21 diamond border, flat matte ukiyo-e cel ink-brush illustration, rice paper #F1E8D4 and persimmon motif. Bold readable silhouette at 90px. No lettering, numbers, real kanji, watermark, people, characters, animals, faces, weapons, masks, Tanuki or scenery. Palette only indigo #2D3A5E, ink #1D1B21, rice #F1E8D4, pine #34483B, shrine red #B5332B, persimmon #D8632C. Near-black ink diamond, its rough ragged edge acting as the border, containing a bold rice-white right-pointing chevron made of two thick dry-brush strokes, a row of small round studs along its outer edge, three rice-white speed streaks trailing behind it and a persimmon impact ring at the tip.`

Retake lever: if the chevron reads as an arrow (a weapon), thicken it into a wedge and drop one streak.

### 3. oni-quake (Oni, rare, ground pound, hits low)

Motif: low and wide, against the Charge's pointed right-facing shape. A ground line across the lower third, a jagged crack splitting upward from its centre, one wide persimmon shockwave arc, four rubble chips.

Prompt (820 characters): `Create one standalone square game UI diamond move icon, tight framing with small transparent padding, genuine transparent alpha outside diamond. Thin rough ink #1D1B21 diamond border, flat matte ukiyo-e cel ink-brush illustration, rice paper #F1E8D4 and persimmon motif. Bold readable silhouette at 90px. No lettering, numbers, real kanji, watermark, people, characters, animals, faces, weapons, masks, Tanuki or scenery. Palette only indigo #2D3A5E, ink #1D1B21, rice #F1E8D4, pine #34483B, shrine red #B5332B, persimmon #D8632C. Near-black ink diamond, its rough ragged edge acting as the border, containing a horizontal rice-white ground line across the lower third with a jagged rice-white crack splitting upward from its centre, a wide persimmon shockwave arc above the crack and four small rice-white rubble chips.`

Retake lever: if it reads like the Charge at 90 px, remove the arc and widen the ground line to the diamond's full width.

### 4. river-grab (Kappa, common, command grab)

Motif: a river current that seizes. Two thick mirrored rice-white curling currents from the left and right, their hooked tips closing on one small ink pebble, a few foam flecks. No hand, claw or creature. Symmetrical and pincer-shaped, against Spirit Wave's single diagonal sweep and the fallback's spiral.

Prompt (797 characters): `Create one standalone square game UI diamond move icon, tight framing with small transparent padding, genuine transparent alpha outside diamond. Thin rough ink #1D1B21 diamond border, flat matte ukiyo-e cel ink-brush illustration, rice paper #F1E8D4 and ink motif. Bold readable silhouette at 90px. No lettering, numbers, real kanji, watermark, people, characters, animals, faces, weapons, masks, Tanuki or scenery. Palette only indigo #2D3A5E, ink #1D1B21, rice #F1E8D4, pine #34483B, shrine red #B5332B, persimmon #D8632C, water teal #3E7F86. Water-teal diamond containing two thick mirrored rice-white curling water currents, one entering from the left and one from the right, their hooked tips closing together around one small ink-black round pebble at the centre, with a few ink foam flecks.`

Retake lever: if it reads as Spirit Wave or the fallback swirl, straighten the currents into two blunt hooks and enlarge the pebble.

### 5. shell-spin (Kappa, rare, spins through projectiles)

Motif: the shell, spinning. A round shell-plate disc seen from above, filled rice-white with bold ink hexagon plate lines, ringed by two broken spin arcs. A filled bright disc, so it is not the fallback's hollow white ring.

Prompt (743 characters): `Create one standalone square game UI diamond move icon, tight framing with small transparent padding, genuine transparent alpha outside diamond. Thin rough ink #1D1B21 diamond border, flat matte ukiyo-e cel ink-brush illustration, rice paper #F1E8D4 and ink motif. Bold readable silhouette at 90px. No lettering, numbers, real kanji, watermark, people, characters, animals, faces, weapons, masks, Tanuki or scenery. Palette only indigo #2D3A5E, ink #1D1B21, rice #F1E8D4, pine #34483B, shrine red #B5332B, persimmon #D8632C, water teal #3E7F86. Water-teal diamond containing a round shell-plate disc seen from above, filled rice-white with bold ink hexagon plate lines and a thick ink rim, surrounded by two broken curved rice-white spin arcs.`

Retake lever: if the disc reads as a plain circle, cut the hexagon lines to three and thicken the rim; if the arcs look like the fallback ring, use one arc.

## Tool and cost (AM1)

Prices are per take (one image). The cap is 3 takes per icon, matching the 3-take fallback rule: 5 first-pass takes, ceiling 15. Every retake needs its own approval (ticket and agent rules).

| | A. Built-in imagegen (recommended) | B. Meshy MCP `meshy_text_to_image` | C. Astra via `codex exec -m gpt-6-astra` |
|---|---|---|---|
| Price per take | no credits; Codex does not report a per-image cost (the 2026-10-20 plate runs logged spend as unrecorded) | `nano-banana` 3, `nano-banana-pro` 9 (both as in the stage specs and the YOK-29 list; the bamboo backdrop is 9 a take). `nano-banana-2` 6, plus `nano-banana-2-lite` 3, `nano-banana-2-1` 4 and `gpt-image-2` 9, are from the first draft and unverified | no credit price in the repo; Codex does not report it; counted in takes against a budget the designer sets (`tools/style_agent` `--max-images`, default 9) |
| First pass, 5 takes | **0 credits, 5 takes** | 15 / 30 / 45 credits (`nano-banana` / `nano-banana-2` / `nano-banana-pro`) | 5 takes |
| Ceiling, 15 takes | **0 credits, 15 takes** | 45 / 90 / 135 credits | 15 takes |
| Available now | yes, through a Codex session | **no**: `.mcp.json` (gitignored, per machine) is missing, so no Meshy call and no `meshy_check_balance` can run; balance unknown | yes, through `codex exec` |
| Look match to the four accepted | same tool and template: best | a different generator, to be checked against them | a different hand: the merchant portrait took 3 takes to match Ryo's style |
| Prompt length | no limit met | 600-character field limit (YOK-29, Ryo regen spec): the full prompts (743-826) do not fit; use the condensed set below | no limit met |
| Alpha | genuine alpha on 4 of 4 accepted icons | `remove_background: true` (from the first draft; verify against the live tool schema) | known failure: background removal eats flat pale plates (alpha 247-252, repaired by script on YOK-159). Filled red, ink and teal diamonds are not pale, so low risk |
| Standing under AM1, AM7 | the designer approved built-in imagegen for the four icons on 2026-10-11; no amendment covers it, so this job needs the designer's yes | the tool AM1 names | AM7 covers fighter concept art only; "costly", kept for finished art (decision log 2026-10-15); the style loop's last run spent its 9-take budget (#237), so icons need their own |

**Recommendation: A, built-in imagegen.** Per job: one take, 0 Meshy credits. First pass: 5 takes, 0 credits. Cap: 3 takes per icon, ceiling 15 takes. A Codex session runs it on the designer's say-so: the UI Designer's Codex session as for the first four, or the main session through `codex exec` as for the YOK-159 plates; the designer names which. Reasons: it made the four accepted icons, so the template carries over and style drift is smallest; it needs no Meshy credits or MCP; the full prompts fit.

**If the designer picks B.** Restore `.mcp.json`, run `meshy_check_balance`, then use the condensed prompts below with `aspect_ratio: 1:1`, `generate_multi_view: false`, `remove_background: true`. The 30 credits logged on 2026-10-18 applied to `nano-banana-2` at the first draft's prompts. The shared head is 406 characters, joined to the sentence by one space: `Square game UI diamond move icon, transparent alpha outside the diamond, small padding. Thin rough ink #1D1B21 border, flat matte ukiyo-e cel ink-brush art, bold silhouette readable at 90px. No text, numbers, kanji, watermark, people, animals, faces, weapons, masks or scenery. Palette only indigo #2D3A5E, ink #1D1B21, rice #F1E8D4, pine #34483B, shrine red #B5332B, persimmon #D8632C, water teal #3E7F86.` Then the job's sentence (total length in brackets):

| Id | Sentence |
|---|---|
| fox-mirage | `Shrine-red diamond: left, a ring of broken rice-white dashes dissolving into flecks; right, a solid rice-white pearl; one curved rice-white swoosh from ring to pearl.` (573) |
| iron-club-charge | `Near-black ink diamond: bold rice-white right-pointing chevron of two thick strokes with small round studs along its edge, three speed streaks behind, a persimmon ring at the tip.` (586) |
| oni-quake | `Near-black ink diamond: rice-white ground line across the lower third, a jagged rice-white crack rising from its centre, a wide persimmon shockwave arc above, four rubble chips.` (584) |
| river-grab | `Water-teal diamond: two thick mirrored rice-white curling water currents from left and right, hooked tips closing around one small ink-black pebble, a few ink foam flecks.` (578) |
| shell-spin | `Water-teal diamond: a round shell-plate disc from above, rice-white with bold ink hexagon lines and thick ink rim, two broken curved rice-white spin arcs around it.` (571) |

**If the designer picks C.** The main session runs the full prompts through `codex exec -m gpt-6-astra` as `tools/style_agent` does, one image per call, each take counted against the budget the designer sets (suggest 5, ceiling 15). Collect the image from `~/.codex/generated_images/` by modification time.

## Job chain (option A; B and C change only the generator call)

1. The designer approves the tool, 5 first-pass takes and the ceiling of 15 (Approval table below).
2. For each icon in id order, one generator call with its full prompt, one image requested. Keep the raw take outside the repo until the verdict.
3. Check it (Acceptance 1 and 2 by script, 3 to 8 by eye). On ACCEPT, resize to 256x256 with Lanczos, preserving alpha. Pillow premultiplies RGBA on resize, so no matte bleeds in; `tools/ui/import_shop_plates.py` does the same and refuses to overwrite. The four accepted icons came from 1254x1254 sources.
4. Save as `game/assets/generated/ui/ability-icon-<id>.png`. Let Godot write the `.png.import` file and commit both: `AbilityArtProbes.NoOrphanedOldArtOrImportFiles` fails on a PNG in that folder without one.
5. Log each take under Takes. When a job has run, add one line under "Assets and spend" in `docs/decision-log.md` (change-tracking.md).

## Registration after ACCEPT

`game/ui/ability_art.tres` maps an ability id to a texture, and unmapped ids get the fallback. For each accepted icon add one `ext_resource` line (`res://assets/generated/ui/ability-icon-<id>.png`, id `<id>`) and one entry in `metadata/icons`. The lookup is `AbilityArt.ForId(id)`. Mapping an id before its move exists breaks no test: `AbilityArtTests.EveryDataIdResolvesOrFallsBack` reads only ids that exist, and `AbilityArtProbes` checks its own three-id list. The probe's stated contract is "every mapped key is a real ability id in data (no dead keys)", so register each icon in the PR that authors its move, or accept dead keys until Epic D lands them and say so in the PR. Never register an icon before its PNG exists.

## Approval

| Item | Value |
|---|---|
| Tool chosen | pending (recommended: built-in imagegen) |
| Approver and date | pending |
| Approved first pass | pending (recommended: 5 takes, 0 Meshy credits) |
| Approved ceiling | pending (3 takes per icon, 15 in all; each retake approved separately) |
| Locked specials in this batch | default yes (ticket); pending |
| Diamond colours (Kitsune red, Oni ink, Kappa teal) and the new Fox Mirage motif | pending |

## Acceptance checks (per icon, after each take)

The thresholds come from the four accepted icons, measured on 2026-10-25: all 256x256 RGBA, alpha bounding box inset 3-8 px, canvas coverage 0.46-0.47, corner alpha 0, no exterior or interior alpha holes, 1.8-2.6% partial-alpha pixels, mean fill within 1.8-5.4 dE of the palette hex.

1. **Size and alpha (script).** 256x256 RGBA. Corner alpha 0. No pixel with alpha above 8 outside the diamond (|dx|+|dy| above 134 from the centre). No pixel with alpha below 250 well inside it (|dx|+|dy| below 110). Alpha bounding box inset 3-9 px, coverage 0.44-0.50, partial-alpha pixels at most 4%. A baked checkerboard, white fill or halo is a RETAKE.
2. **Fill colour (script).** The mean of the opaque non-bright pixels in the ring 70 < |dx|+|dy| < 105 is within 8 dE (CIEDE2000) of the diamond hex in the Diamond colours table.
3. **Text-free, no characters.** No letters, numbers, kanji, watermark, people, animals, faces, masks or weapons; no hand, claw or club. Inspect at full size.
4. **Palette.** Only the six palette hexes (and water teal on the Kappa pair); flat, no gradient, gloss or glow.
5. **Readable and distinct at 90 px.** Downscale to 90x90 and lay all nine (the four existing and the five new) on a contact sheet over rice paper `#F1E8D4` and over ink. The diamond colour and the silhouette each still read, with no confusable pair. Look first at: fox-mirage and foxfire (same red); iron-club-charge and oni-quake (same ink); river-grab and shell-spin (same teal); both ink diamonds against spirit-wave (16.0 dE); river-grab and shell-spin against the pine fallback ring and spirit-wave.
6. **Style match.** Thin rough ink border, flat matte cel fill, paper grain, outline weight like spirit-wave and foxfire; no painterly noise.
7. **Source fidelity (A8).** The motif is the owning yokai's alone: no Ryo motif (no talisman strip, no spirit wave).
8. **Licence.** The output terms on the designer's plan allow commercial use. UNKNOWN for built-in imagegen, for Astra (the Astra shop-plate licence is also still unconfirmed, decision log 2026-10-20) and for Meshy on the designer's plan, until the designer confirms.

Verdict per icon: `ACCEPT`, or `RETAKE` with the exact prompt change and its cost (one take on A and C; the Meshy credits on B), which goes back to the designer for approval. After 3 failed takes the icon stays on `ability-icon-fallback.png` (the generic pine energy ring, no `.tres` entry) and work on it stops.

## Takes

None. Nothing has been run.

## Acceptance

Pending first takes.
