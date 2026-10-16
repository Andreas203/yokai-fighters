# Kappa (fighter) - generation spec

Ticket: YOK-102. Status: DRAFT, awaiting designer approval (see `yok-102-job-list.md`). No job run.
References: none yet (`docs/design/characters/kappa/` does not exist). The concept front view (K1) becomes the turnaround reference. Source: `vault/01-vibe/Characters.md`, `vault/02-mechanics/Yokai.md`.

## Character

The grappler of the lantern riverbank. Formal, stubborn and polite (personality proposed in the vault, designer to confirm); it walks straight into throw range. Offers River Grab and Shell Spin. Hit sparks are kappa teal (V5). Stage: lantern riverbank (V9). Elder Kappa is told apart by preset colour and VFX, not a new model.

- **Silhouette:** low and wide: hunched shoulders, short thick legs, big hands, a round shell on the back, a flat dish on the crown. About 1.5 m, the shortest fighter. Reads as "round and grounded" at 720p. The shell gives a back bulge; the dish a flat-topped head.
- **Face:** beaked mouth, round eyes, calm stubborn expression, webbed hands and feet.
- **Outfit:** a plain rice-paper loincloth sash with a rope tie (no charm, designer 2026-10-16). Otherwise bare skin.
- **Colours (V1):** skin kappa teal, shell dark pine `#34483B` with ochre rim, dish rice paper `#F1E8D4` with a thin ink rim (water NOT modelled), beak and belly pale, outline ink `#1D1B21`, sash rice paper, charm pine.
  - Palette gap (designer to confirm, then added to `style.md`): kappa teal skin PROPOSED `#3E8C84`; shell ochre rim PROPOSED `#8A6A3A`. Pine `#34483B` is the only existing green.

## Prompts (after the style block in `assets/specs/style.md`)

Written so the same text works as an Astra brief. Replace "Avoid: [negative prompt]" with the block from style.md.

Concept front view (K1, text_to_image, 2:3, nano-banana-pro):
> Full-body concept art of a squat river-spirit fighter, front view, neutral standing pose with the arms slightly away from the body and the feet apart, plain white background. Hunched wide shoulders, short thick legs, large webbed hands and feet, a beaked mouth, round calm stubborn eyes, a flat pale dish on the top of the head (empty, no water), a round dark-green shell on the back visible at the shoulders, skin in kappa teal #3E8C84, a rice-paper sash with a rope tie. No held items, no water, no effects. Avoid: [negative prompt].

Turnaround (K2, image_to_image, reference = chosen K1 image):
> Character turnaround sheet of the same river-spirit fighter: front, side and back views, same height and scale, T-pose (arms straight out), plain white background. Keep the art style, proportions and palette of the reference exactly. The back view clearly shows the round shell. No held items, no water. Avoid: [negative prompt].

Shell (K4, text_to_image, 1:1, nano-banana):
> A single round turtle-like shell with a hexagon plate pattern, dark pine green with an ochre rim, shown from the front on its own, plain white background, flat cel shading. Avoid: [negative prompt], body, water.

## Job chain

| # | Tool | Settings | Credits |
|---|---|---|---|
| K1 | `meshy_text_to_image` | `nano-banana-pro`, 2:3 (or Astra) | 9 |
| K2 | `meshy_image_to_image` | `nano-banana-pro`, reference K1, `generate_multi_view: true` | 9 |
| K3 | `meshy_image_to_3d` | shared settings; front view of K2; `target_polycount: 15000`, `t-pose`, `2k`, no PBR, `auto_size`, `origin_at: bottom` (~1.5 m) | 30 |
| K4 | `meshy_text_to_image` | `nano-banana`, shell prompt, 1:1 | 3 |
| K5 | `meshy_image_to_3d` | `meshy-6-lite`, K4 image, `target_polycount: 3000`, `2k`, no PBR | 15 |

Per-take total 66 (body 48, shell 18). Take cap 3. Stops at the unrigged T-pose mesh. `meshy_rig` (1.5 m, 5 credits) belongs to `rig-specialist` and is not in this approval.

Open design call for the designer: a SEPARATE shell (K4/K5) is only worth it if Shell Spin needs the shell to rotate on its own. If the shell stays baked on the back, drop K4/K5 (-18). The spec recommends baked shell first and the separate shell as an optional line, listed in the job list as "recommended, designer chooses".

## Rig notes for rig-specialist

| Part | Modelled as | Note |
|---|---|---|
| Body, head, limbs | one mesh | Meshy humanoid skeleton; short legs and thick body may need a scale/leg-length check against the shared skeleton and clips |
| Shell | baked on the back by default; optional separate rigid mesh K5 on a spine `BoneAttachment3D` | back clearance in throw, crouch and back-turn; a separate shell lets Shell Spin rotate it |
| Dish | baked to head | flat, no water, no sway |
| Sash, charm | baked | no sway |
| Webbed hands | baked | fingers must be separate enough to rig; webbing reads as the fighting-hand silhouette |

Short stature: the shared clips are authored for 1.65-1.75 m humans. Hand and reach positions will under-reach for throws; record in the G4 gate (retarget reach, not mesh fix).

## Acceptance criteria

- Palette matches (teal skin, dark shell, pale dish, ink outline); beak, dish and shell readable; the dish is empty (no baked water or highlight).
- Silhouette round and low at 720p, clearly shorter than Ryo.
- Consistency: outline weight and tone steps match accepted fighters.
- Mesh: ~15k tris, one 2K texture, no PBR, T-pose, origin at feet, Y-up, fingers/toes separated.
- G4 readiness: shell, dish and short limb proportions on walk, heavy and throw; record the risk. Fallback: folklore human guise (a river boatman in plain clothes, new spec, optional line G2).
- No resemblance to an existing character or franchise; no text.
- Licence: plan tier unknown, flagged.

## Takes
None yet.

## Acceptance
Not run.
