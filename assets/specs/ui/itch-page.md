# itch.io store page art

Status: Astra takes run 2026-11-12 on the designer's request ("use astra to make my itchio page nicer"). Generator: Astra through `codex exec -m gpt-6-astra` (credits unreported by Codex). Budget: 3 takes plus 3 retakes (6 calls). Used: 4 calls (3 takes, 1 retake). The designer uploads by hand; this is marketing art, not game content (nothing under `game/`). Rules: V1, F6, AM1.

## Files (`assets/itch/`)

| File | Size | Use |
|---|---|---|
| `cover-630x500.png` | 630x500 | itch cover image (thumbnail) |
| `banner-960x300.png` | 960x300 | page banner / header |
| `background-1920x1080.png` | 1920x1080 | page background image |
| `screenshots/01..06-*.png` | 1920x1080 | gallery: Foxfire special, Ryo vs the Oni, map, reward cards, Tanuki boss, rest stop dojo |
| `contact-sheet.png` | - | everything on one sheet, plus the cover at 200 px |
| `raw/` | - | raw Astra takes (cover-t1, banner-t1, banner-t2, bg-t1) and the first-pass squashed resizes |

## Prompts

Shared style sentence: toon-shaded key art in the paper-talisman, ink and washi style, flat cel colour in 2-3 tone steps, heavy tapered ink outlines, vermilion, persimmon, indigo and cream washi. Logo sentence: title reads exactly YOKAI FIGHTERS in hand-brushed capitals, black ink with a vermilion seal stamp, cream paper outline. Full prompts and refs are in the job file used (kept in the session scratchpad; the logo sentence and negative list above are the load-bearing parts).
- Cover: refs `docs/design/characters/ryo/ryo-astra-front.png`, `oni/oni-astra-front.png`, `tanuki/tanuki-true-form-astra-front.png`, `docs/design/screens/title.png` (style). Ryo in a low stance with a talisman, the Oni mid-lunge with the club, the Tanuki looming in the dusk sky.
- Banner: refs kitsune turnaround, oni, kappa, Tanuki fronts. Wide dusk scene with bamboo, torii, stone lanterns, the four yokai and a small Ryo, logo in the centre. Take 2 added "content in the middle 40% band, it will be cropped".
- Background: ref `title.png`. Dark indigo-charcoal washi, faint clouds, torii and bamboo near the edges, quiet centre.

## Takes

| # | Piece | Size from Astra | Result |
|---|---|---|---|
| 1 | cover | 1402x1122 | ACCEPT (cover-fit crop to 630x500) |
| 2 | banner t1 | 1717x916 | RETAKE: 1.87:1, a 3.2:1 crop would cut the logo and the yokai |
| 3 | background | 1672x941 | ACCEPT (upscaled to 1920x1080) |
| 4 | banner t2 (retake) | 2172x724 | ACCEPT (centre crop to 3.2:1, resize to 960x300) |

Retakes used: 1 of 3. Codex usage limit not hit.

## Acceptance

- Logo spelling: YOKAI FIGHTERS exactly, checked on the cover and the banner at full size. PASS.
- Look (V1): ink outline, flat cel, washi texture and the dusk palette match `title.png` and the finish-art batch. PASS. The art is more illustrated than the in-game toon 3D, which is normal for key art.
- Cover legibility: logo and the two figures read at 200 px (contact sheet). PASS.
- Characters: Ryo (cover) matches the locked turnaround: dark-blue coat, orange lining, rope belt, talisman. Oni with spiked club and Tanuki match. In the banner the yokai match their turnarounds; Ryo is tiny and drawn with a white-and-red cape and a sword, so he does not match the locked model (minor; he is about 70 px tall). Offer a retake if the designer wants it (2 retakes left).
- Background: centre mean #282327, details only at the edges, mid-dark. A light text column reads over it. PASS.
- Screenshots: from this build, 1920x1080, `--audio-driver Dummy`, via `vfx_capture` (foxfire impact), `oni_kappa_capture` (Oni heavy, snow shrine gate), `map_capture` (mid-run), `reward_capture` (cards with ability icons), `stage_capture opponent=tanuki` (tea house), `rest_stop_capture` (dojo). The Tanuki and Oni shots are clinches, not wide action; the Foxfire shot is the strongest. Replace with the dev-menu captures if a wider shot is wanted.
- No other game's logo, key art or character imitated. Licence: Astra output used under the designer's plan; no unknowns flagged.

## Proposed itch theme (hex, pulled from the art)

| Slot | Hex | Source |
|---|---|---|
| Background | #282327 | centre of the page background |
| Text | #F2E6CF | washi cream |
| Link | #E8683A | persimmon of the dusk sky |
| Button | #B8321F | vermilion seal red (button text #F2E6CF) |
| Border / accent | #E39161 | sky orange |

## Tagline (82 characters)

Bind the yokai you beat, borrow their moves, and face the Tanuki who copies yours.
