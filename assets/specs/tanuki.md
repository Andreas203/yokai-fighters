# Tanuki (merchant and boss) - generation spec

Ticket: YOK-102. Status: DRAFT, awaiting designer approval. No job run.

## Does the Tanuki need its own model? (T0)

No, for the fight. Rule T0 and AM1/AM4: the Tanuki fights with Ryo's model and Ryo's clips, so no new fighter mesh, rig or clip is needed, and none is specced as required.

What T0 does not cover: the merchant (row 4 and row 7 rest-stop trader, a 2D screen), the "merchant drops disguise" boss-node card (S3), the Tanuki reveal with a slow camera push-in (V7), and the win cards (Win 1-3, S3). The sources do not say whether the reveal is shown as a 3D model or as 2D card art. This spec therefore makes **concept art only** (required) and a **true-form 3D model as a flagged optional line**. The designer decides (see job list).

## Character (vault `Characters.md`, `Tanuki.md`)

The rest-stop trader and the final boss are the same creature. Cheerful, generous, quietly hungry; it collects techniques the way others collect teacups and helps Ryo grow so there is more to steal. Its stage is a tea house with season-flickering paper screens (V9). Spark colour: none assigned (it fights as Ryo with Ryo's ink).

- **Merchant look (disguise):** a short round travelling trader: broad-brimmed straw hat, rice-paper cloth bundle on the back, a gourd and a few cups on a rope, a folded abacus-style tally board, friendly smile. Plain kimono in muted brown and rice paper.
- **True form (reveal):** a round, tall-eared raccoon-dog spirit, big belly, bushy striped tail, leaf on the head, rice-paper money-leaf tucked in the sash, a faint hungry half-smile; the straw hat has fallen off. Folklore detail: a teakettle shape may echo on the belly or beside it, but NO kettle transformation is drawn.
- **Palette (V1):** straw `#B08A48` brass, fur muted brown PROPOSED `#6E5A48` (not in style.md; designer to confirm), belly rice paper `#F1E8D4`, sash shrine red `#B5332B`, outline ink `#1D1B21`, accent persimmon `#D8632C` for the tea-house glow.
- In the fight the Tanuki wears Ryo's model (T0); this art is only for the merchant screen, the reveal and the win cards.

## Prompts (after the style block). Work as Astra briefs too.

Merchant concept (T1, text_to_image, 2:3, nano-banana-pro):
> Full-body concept art of a cheerful round travelling merchant, front view, plain white background. Broad straw hat, warm half-smile, short plump body in a muted brown kimono with a rice-paper cloth bundle on the back, a gourd and small tea cups hanging from a rope, a bushy tail just visible under the kimono hem, wooden sandals. Palette: straw #B08A48, brown #6E5A48, rice paper #F1E8D4, shrine red #B5332B. No held weapon, no effects. Avoid: [negative prompt].

True-form concept (T2, text_to_image, 2:3, nano-banana-pro):
> Full-body concept art of the same merchant revealed as a round raccoon-dog spirit, front view, plain white background. Large round belly pale rice paper, brown fur, dark ringed bushy tail, a single leaf on the head, small round ears, hungry knowing half-smile, rice-paper leaf-money tucked in a red sash, the straw hat lying at the feet. Same art style, outline and palette as the merchant. No effects. Avoid: [negative prompt].

Optional turnaround (T3, image_to_image, reference = chosen T2): front, side, back, T-pose, same height and scale, plain white background, keep style exactly. Optional mesh (T4, image_to_3d): shared settings, `target_polycount: 15000`, `t-pose`, `2k`, no PBR, ~1.4 m, stops unrigged. Only if the designer chooses a 3D reveal.

## Job chain

| # | Tool | Settings | Credits |
|---|---|---|---|
| T1 | `meshy_text_to_image` | `nano-banana-pro`, 2:3 (or Astra) | 9 |
| T2 | `meshy_text_to_image` | `nano-banana-pro`, 2:3 (or Astra) | 9 |
| T3 (optional) | `meshy_image_to_image` | multi-view from T2 | 9 |
| T4 (optional) | `meshy_image_to_3d` | `meshy-7.1`, 15k tri, 2K, no PBR, t-pose | 30 |

Required: 18 per take (concepts). Optional 3D true form: +39 per take. The 2D concept images feed the merchant screen and the reveal/win cards; whoever makes the screen art (ui-designer/Astra) uses them.

## Rig notes (only if T4 runs)

Not rigged unless the designer approves; for a reveal-only cutscene a static T-pose or a posed still may be enough. If rigged later it would need the same Meshy humanoid skeleton; the belly and the bushy tail are baked (tail not rigged, like Kitsune's).

## Acceptance criteria

- Merchant and true form read as the same creature (hat and tail vs leaf and belly); friendly, not menacing.
- Palette within tolerance; flat tones; no baked PBR; no kettle transformation, no text.
- Consistency of outline weight with accepted fighters.
- No resemblance to an existing character or franchise.
- T4 only: mesh criteria as in `oni.md`.
- Licence: plan tier unknown, flagged.

## Takes
None yet.

## Acceptance
Not run.
