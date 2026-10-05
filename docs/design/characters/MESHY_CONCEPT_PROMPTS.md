# Meshy Concept-Render Prompt Package

Use the existing front-view PNGs as **character-detail references**, not as the
final Meshy upload. Generate the renders with an image model that supports an
image reference and character consistency, then use the selected front render
in Meshy Image-to-3D.

The target is an original toon-shaded Japanese folklore fighting-game look:
athletic proportions, readable layered costumes, angular silhouettes, ink-like
outlines, and the project's muted ukiyo-e palette. Do not request or name a
specific existing game, artist, or character.

## Generation settings

| Setting | Value |
|---|---|
| Generator | FLUX or an equivalent image-reference model with character consistency |
| Reference strength | High for costume/palette; medium for anatomy and rendering |
| Canvas | 1024 x 2048 minimum; 1536 x 3072 preferred |
| Views | Generate front first; use it as the character reference for side and back |
| Pose | Neutral A-pose, palms forward, feet shoulder-width apart, head facing forward |
| Background | Pure white (`#FFFFFF`), no ground shadow |
| Lighting | Even studio light, soft frontal key, no dramatic rim light |
| Delivery | One full character per image, no captions, borders, props, effects, or scene |

Reject a render if it changes the palette, merges clothing layers, hides a
limb, uses a perspective pose, or adds asymmetrical details that cannot be
verified in the other views.

## Shared negative prompt

```text
existing copyrighted character, franchise likeness, text, logo, watermark,
signature, weapon, spell effect, projectile, energy aura, held object,
dynamic pose, bent elbows, crossed limbs, cropped feet, cropped ears, cropped
tails, foreshortening, perspective camera, scene, background, cast shadow,
multiple characters, duplicate limbs, missing fingers, fused fingers,
unreadable costume layers, photorealism, painterly texture, elaborate
micro-detail, transparent clothing, sexualized clothing
```

## Ryo

Reference image: `ryo\ryo-meshy-front.png`

### Front view

```text
Original male apprentice exorcist for a toon-shaded Japanese folklore fighting
game. Full-body FRONT ORTHOGRAPHIC character turnaround, strict neutral
A-pose: arms held 15 degrees from the torso, relaxed open hands with palms
forward, feet shoulder-width apart, straight-on face, symmetrical balanced
stance. Lean athletic young adult with an upright, capable silhouette and
slightly broad shoulders; approximately seven-and-three-quarter heads tall.

Black undercut hair with a sharp, slightly asymmetric fringe and one lock over
his right eye. Tapered focused eyes, a small vermilion binding mark below his
left eye. High indigo collar zipped just below the nose. Indigo asymmetrical
coat, closed left-over-right like a kimono, with four visible brass spiral
buttons; coat has a long left-side tail and a narrow persimmon-orange lining.
Indigo trousers, rice-paper shin wraps, black jika-tabi boots. Shimenawa rope
sash with clean paper shide and a compact ofuda pouch at the hip. Rice-paper
wraps on the right forearm, with four flat talisman strips aligned cleanly to
the arm; two have small red seals. One simple ofuda earring.

Original stylized 3D fighting-game concept render, sculptable anatomy and
clear separated clothing volumes, bold ink-outline treatment, two-tone
toon shading, muted ukiyo-e palette only: indigo #2D3A5E, near-black #1D1B21,
persimmon #D8632C, rice paper #F1E8D4, brass #B08A48, natural warm skin.
Pure white background, even studio lighting, no shadow, no visual effects,
no text, no weapons, no held talisman.
```

### Side and back views

Use the accepted front render as the image reference. Keep the same prompt,
replacing `FRONT ORTHOGRAPHIC` with `LEFT SIDE ORTHOGRAPHIC` or
`BACK ORTHOGRAPHIC`. Preserve the exact costume, facial marking position,
asymmetric coat-tail position, talisman count, hair silhouette, and palette.

## Kitsune

Reference image: `kitsune\kitsune-meshy-front.png`

### Front view

```text
Original female fox yokai for a toon-shaded Japanese folklore fighting game.
Full-body FRONT ORTHOGRAPHIC character turnaround, strict neutral A-pose:
arms held 18 degrees from the torso, relaxed open hands with palms forward,
feet shoulder-width apart, straight-on masked head, symmetrical balanced
stance. Athletic, agile adult with a clean readable silhouette; approximately
seven-and-a-half heads tall.

Two upright fox ears, white hair with controlled orange tips, and a single
low straight ponytail. Her face is completely hidden behind a full white fox
mask with precise shrine-red markings; only subtle foxfire-orange light is
visible through the narrow eye slits. Never expose a human face, eyes, nose,
or mouth. Cropped white haori with broad sleeves and shrine-red edging,
over an indigo high-neck inner top. Foxfire-orange obi, indigo cord and one
small suzu bell. Shrine-red pleated hakama ending at the knee, black tabi,
and simple wooden geta.

Exactly THREE separate fox tails, attached at the lower back and fanned low:
left, centre, and right. Tails are white with clean orange tips and a
moderate sculptable thickness; keep every full tail visible and separated.
No foxfire orb, hand sign, flame, or other VFX.

Original stylized 3D fighting-game concept render, sculptable anatomy and
clear separated clothing volumes, bold ink-outline treatment, two-tone
toon shading, muted ukiyo-e palette only: fur white #FBF6EC, foxfire
#F2A23A, shrine red #B5332B, indigo #2D3A5E, near-black #1D1B21, brass
#B08A48. Pure white background, even studio lighting, no shadow, no text.
```

### Side and back views

Use the accepted front render as the image reference. Keep the same prompt,
replacing `FRONT ORTHOGRAPHIC` with `LEFT SIDE ORTHOGRAPHIC` or
`BACK ORTHOGRAPHIC`. Retain the full mask, three tails, ponytail, one bell,
and all garment lengths. In the back view, ensure the tails remain three
separately readable forms rather than one merged mass.

## Meshy handoff

1. Upload only the accepted **front orthographic** render, cropped with 8-12%
   white margin around the full silhouette.
2. Generate Ryo first. Choose the output with separated forearms, readable
   collar, coat tail, sash, and boots; discard outputs that turn the talismans
   into floating cards.
3. Generate Kitsune **without tails** if the first output fuses or deletes
   them. Build tails as a separate rigged mesh or reuse the `notails` source
   reference for the body pass.
4. Retopologize and rig the selected mesh before animation retargeting. The
   coat tail, Ryo's shide, Kitsune's sleeves, ponytail, and tails require
   dedicated bones or deterministic secondary-motion baking.
5. Keep the Meshy output as presentation geometry only. Combat collision is
   still data-driven 2D rectangles, and animation must be stepped by exact
   frames.
