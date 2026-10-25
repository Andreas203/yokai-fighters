# Astra brief: Oni, Kappa, Tanuki concept fronts and turnarounds (YOK-102)

Designer decision 2026-10-16 (Option B): Astra makes the concept fronts and the turnarounds. The designer runs Astra. The sheets then feed Meshy image-to-3D (mesh jobs are approved separately, see `assets/specs/jobs/yok-102-job-list.md`). Specs with the full character notes: `assets/specs/characters/oni.md`, `kappa.md`, `tanuki.md`.

## Shared style block (paste at the start of every prompt)

Stylised toon-shaded game art, flat cel colour in 2-3 tone steps, clean confident ink-brush outline of even weight, muted ukiyo-e colour palette, matte surfaces, soft even lighting with no cast shadows, no photographic or painterly texture, no gloss, no baked ambient occlusion, simple readable shapes.

## Shared negative block (append to every prompt)

Avoid: photorealism, PBR detail, metallic sheen, painterly brush noise, film grain, text, watermark, logo, signature, extra limbs, extra fingers, held props or effects not described, glowing particles, cast shadows, busy background, gradient background, any resemblance to an existing franchise, artist or character.

## Rules for every output

- Flat, even lighting and flat colour. No baked shading, rim light, highlights or shadows: the game applies its own toon shader and outline, and baked shading would ruin the 3D texture.
- Neutral flat white (or very light grey) background, nothing else in frame.
- Fronts: standing, arms slightly away from the body, feet apart.
- Turnarounds: front, side and back views on one sheet, the same height and scale, T-pose (arms straight out), feet apart, flat on the ground line. Match the chosen front view exactly in proportion, outline weight and palette.
- No held items, weapons or effects. The Oni club and Kappa shell-as-prop are separate jobs.
- Do not write any text, labels or kanji on the sheet.
- Palette hexes marked PROPOSED are not yet confirmed by the designer. Existing palette entries come from `assets/specs/style.md`.

## Output paths

Save as PNG, full body, at least 1024 px on the short side. Create the folder if missing.

| Sheet | File |
|---|---|
| Oni front | `docs/design/characters/oni/oni-astra-front.png` |
| Oni turnaround | `docs/design/characters/oni/oni-astra-turnaround.png` (and, if exported separately, `oni-astra-{front,side,back}.png`) |
| Kappa front | `docs/design/characters/kappa/kappa-astra-front.png` |
| Kappa turnaround | `docs/design/characters/kappa/kappa-astra-turnaround.png` |
| Tanuki merchant | `docs/design/characters/tanuki/tanuki-merchant-astra-front.png` |
| Tanuki true form | `docs/design/characters/tanuki/tanuki-true-form-astra-front.png` |

Tanuki is 2D concept only: no turnaround, no 3D (it fights with Ryo's model, T0).

---

## Oni

Palette: skin oni red `#A8372C` PROPOSED; mane and outline ink `#1D1B21`; horns and wraps rice paper `#F1E8D4`; short trousers indigo `#2D3A5E`; studs brass `#B08A48`. About 2.0 m tall, the biggest fighter.

### Oni front (copy-paste)

[style block] Full-body concept art of a towering horned ogre fighter, front view, neutral standing pose with the arms slightly away from the body and the feet apart, plain flat white background. Broad shoulders, thick arms, heavy chest, a proud blunt expression with a heavy brow and two small tusks, two short pale horns, a coarse ink-black mane, skin in deep red #A8372C, plain indigo #2D3A5E short trousers tied with a rice-paper rope belt, rice-paper wraps on the forearms and shins, bare feet. No weapon, no held items, no effects. [negative block]

### Oni turnaround (copy-paste, attach the chosen front)

[style block] Character turnaround sheet of the same horned ogre fighter as the attached image: front view, side view and back view, same height and scale, T-pose with the arms straight out, feet apart, plain flat white background. Keep the art style, proportions, outline weight and palette of the attached image exactly. No weapon, no held items, no effects, no text. [negative block]

## Kappa

Palette: skin kappa teal `#3E8C84` PROPOSED; shell dark pine `#34483B` with rim ochre `#8A6A3A` PROPOSED; dish rice paper `#F1E8D4`; sash rice paper; outline ink `#1D1B21`. About 1.5 m tall, the shortest fighter, low and wide.

### Kappa front (copy-paste)

[style block] Full-body concept art of a squat river-spirit fighter, front view, neutral standing pose with the arms slightly away from the body and the feet apart, plain flat white background. Hunched wide shoulders, short thick legs, large webbed hands and feet, a beaked mouth, round calm stubborn eyes, a flat pale dish on the top of the head (empty, no water), a round dark green shell #34483B with an ochre rim #8A6A3A on the back visible beside the shoulders, skin in teal #3E8C84, a rice-paper sash with a rope tie, nothing hanging from it. No held items, no water, no effects. [negative block]

### Kappa turnaround (copy-paste, attach the chosen front)

[style block] Character turnaround sheet of the same river-spirit fighter as the attached image: front view, side view and back view, same height and scale, T-pose with the arms straight out, feet apart, plain flat white background. The back view clearly shows the round shell. Keep the art style, proportions, outline weight and palette of the attached image exactly. No held items, no water, no effects, no text. [negative block]

## Tanuki (2D concept only)

Palette: straw `#B08A48`; fur brown `#6E5A48` PROPOSED; belly and paper `#F1E8D4`; sash shrine red `#B5332B`; outline ink `#1D1B21`. The two images show one creature, the merchant disguise and the true form, in the same style and scale.

### Tanuki merchant (copy-paste)

[style block] Full-body concept art of a cheerful round travelling merchant, front view, plain flat white background. Broad straw hat, warm half-smile, a short plump body in a muted brown #6E5A48 kimono, a rice-paper cloth bundle on the back, a gourd and small tea cups hanging from a rope, a bushy tail just visible under the hem, wooden sandals. Straw #B08A48, rice paper #F1E8D4, red sash #B5332B. No weapon, no effects. [negative block]

### Tanuki true form (copy-paste, attach the merchant image)

[style block] Full-body concept art of the same merchant as the attached image, revealed as a round raccoon-dog spirit, front view, plain flat white background. Large round pale belly #F1E8D4, brown #6E5A48 fur, a dark-ringed bushy tail, a single leaf on the head, small round ears, a hungry knowing half-smile, rice-paper leaf money tucked in a red #B5332B sash, the straw hat lying at its feet. Same art style, outline weight and palette as the merchant. No kettle, no effects. [negative block]

## After the sheets land

Asset Smith checks them against the specs (palette, flat lighting, same scale across views, no held items) before any Meshy mesh job is proposed for approval. Sheets with baked shading or a painted look are sent back to the designer.

---

## Ryo texture paint-over (YOK-146)

Why: the Tripo Ryo's base colour has cloth folds and shading painted in, which stretch when the mesh bends and clash with the game's toon shader. Blender rendered four orthographic views of the rigged T-pose mesh (`tools/blender/texture_views.py`). Astra repaints each as flat cel colour, inside the exact same silhouette. Blender then projects the paintings back onto the existing UVs (phase B). Diagnosis and render details: `assets/specs/texture/ryo-tripo/README.md`. The designer runs Astra; no credits are spent by Asset Smith.

### Inputs per view (attach all three, in this order)

| View | 1. Albedo render | 2. Mask | 3. Concept |
|---|---|---|---|
| front | `assets/specs/texture/ryo-tripo/ryo-albedo-front.png` | `ryo-mask-front.png` | `docs/design/characters/ryo/ryo-astra-view-front.png` |
| left | `ryo-albedo-left.png` | `ryo-mask-left.png` | `ryo-astra-view-left.png` |
| right | `ryo-albedo-right.png` | `ryo-mask-right.png` | `ryo-astra-view-right.png` |
| back | `ryo-albedo-back.png` | `ryo-mask-back.png` | `ryo-astra-view-back.png` |

Outputs, 2048 x 2048 PNG, same name pattern: `assets/specs/texture/ryo-tripo/ryo-paint-{front,left,right,back}.png`.

### Rules for these outputs (on top of "Rules for every output")

- The canvas, pose, scale and framing are the albedo render's. The silhouette must match the mask pixel for pixel: nothing added outside it, nothing cropped, no outline stroke that grows or shrinks the shape. The mask's black region is where paint goes; everything else stays pure white `#FFFFFF`.
- Do not redraw or re-pose the figure from the concept. Take design, colours and details from the concept view; take position and shape from the render.
- Preferably flat base colour only, one colour per material area (coat indigo, lining persimmon, rope and wraps rice paper, skin, hair ink, boots ink). At most 2 to 3 tone steps where the concept has them. The game's toon shader adds the shading.
- No drawn shadows, fold lines, wrinkles, highlights, gradients, rim light, ambient occlusion or outline. Keep small marks that are design, not shading: buttons, the seal on the wrist wrap, rope twist as flat colour blocks, bandage lines as flat colour, the paper charm and its tassel, face details.
- Palette from `assets/specs/style.md`: indigo `#2D3A5E`, ink `#1D1B21`, persimmon `#D8632C`, rice paper `#F1E8D4`, shrine red `#B5332B`, brass `#B08A48`.
- Check before accepting: toggle the mask over the paint; any coloured pixel outside the mask, or a mask pixel left white, is a retake. Left and right views must agree with front and back on every colour.

### Ryo paint, front (copy-paste)

[style block] Repaint the first attached image (a flat, unlit render of a 3D character, front view, T-pose) as flat cel colour art, using the third attached image as the design and palette reference for the same character. The second attached image is the exact silhouette mask: black is where the character is. Keep the pose, scale, framing and canvas size of the first image, and keep the silhouette pixel-exact to the mask, with nothing added outside it and nothing cropped. Fill every area with one flat base colour from the reference (indigo coat and trousers, persimmon lining, rice-paper rope and wraps, skin, ink hair and boots, brass buttons), at most 2 to 3 tone steps, preferably flat colour only. No drawn shadows, no fold lines, no highlights, no gradients, no outline. Keep face, buttons, wrist seal, rope and charm details from the reference as flat shapes. Pure white background #FFFFFF. Output 2048 x 2048. [negative block]

### Ryo paint, left (copy-paste)

[style block] Repaint the first attached image (a flat, unlit render of a 3D character, side view of his left side, T-pose, facing image-left) as flat cel colour art, using the third attached image as the design and palette reference for the same character. The second attached image is the exact silhouette mask: black is where the character is. Keep the pose, scale, framing and canvas size of the first image, and keep the silhouette pixel-exact to the mask, with nothing added outside it and nothing cropped. Fill every area with one flat base colour from the reference (indigo coat and trousers, persimmon lining, rice-paper rope and wraps, skin, ink hair and boots, brass buttons), at most 2 to 3 tone steps, preferably flat colour only. No drawn shadows, no fold lines, no highlights, no gradients, no outline. Keep the colours identical to the front view. Pure white background #FFFFFF. Output 2048 x 2048. [negative block]

### Ryo paint, right (copy-paste)

[style block] Repaint the first attached image (a flat, unlit render of a 3D character, side view of his right side, T-pose, facing image-right) as flat cel colour art, using the third attached image as the design and palette reference for the same character. The second attached image is the exact silhouette mask: black is where the character is. Keep the pose, scale, framing and canvas size of the first image, and keep the silhouette pixel-exact to the mask, with nothing added outside it and nothing cropped. Fill every area with one flat base colour from the reference (indigo coat and trousers, persimmon lining, rice-paper rope and wraps, skin, ink hair and boots, brass buttons), at most 2 to 3 tone steps, preferably flat colour only. No drawn shadows, no fold lines, no highlights, no gradients, no outline. Keep the colours identical to the front view. Pure white background #FFFFFF. Output 2048 x 2048. [negative block]

### Ryo paint, back (copy-paste)

[style block] Repaint the first attached image (a flat, unlit render of a 3D character, back view, T-pose) as flat cel colour art, using the third attached image as the design and palette reference for the same character. The second attached image is the exact silhouette mask: black is where the character is. Keep the pose, scale, framing and canvas size of the first image, and keep the silhouette pixel-exact to the mask, with nothing added outside it and nothing cropped. Fill every area with one flat base colour from the reference (indigo coat and trousers, persimmon lining, rice-paper rope and wraps, skin, ink hair and boots, brass buttons), at most 2 to 3 tone steps, preferably flat colour only. No drawn shadows, no fold lines, no highlights, no gradients, no outline. Keep the colours identical to the front view. Pure white background #FFFFFF. Output 2048 x 2048. [negative block]

Style block and negative block are the shared ones at the top of this file; paste their text in place of the bracketed names.
