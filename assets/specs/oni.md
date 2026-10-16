# Oni (fighter) - generation spec

Ticket: YOK-102. Status: DRAFT, awaiting designer approval (see `yok-102-job-list.md`). No job run.
References: none yet (sheet and front view do not exist; `docs/design/characters/oni/` is empty). The concept front view is made first (O1) and becomes the reference for the turnaround. Source: `vault/01-vibe/Characters.md`, `vault/02-mechanics/Yokai.md`.

## Character

The rushdown yokai of the snowbound mountain shrine gate. Proud and blunt (personality proposed in the vault, designer to confirm). It dashes in and swings an iron club (kanabo). Offers Iron-Club Charge and Oni Quake. Hit sparks are oni red (V5). Stage: snowbound shrine gate (V9). Elder Oni is told apart by level preset colour and VFX, not by a new model.

- **Silhouette:** the biggest fighter: broad shoulders, thick forearms, a heavy forward-leaning stance, two short horns, a mane of coarse hair. Reads as "wide and heavy" against Ryo's lean line at 720p. Roughly 2.0 m (Ryo 1.75, Kitsune 1.65).
- **Face:** open face, fierce brow, two short horns, a pair of small tusks. (The mask rule is Kitsune's only.)
- **Outfit:** bare muscular torso, plain indigo short trousers tied with a rope belt, rice-paper wraps on the forearms and shins, bare feet.
- **Colours (V1):** skin oni red, hair and outline ink `#1D1B21`, horns rice paper `#F1E8D4`, trousers indigo `#2D3A5E`, belt/wraps rice paper `#F1E8D4`, trim brass `#B08A48`.
  - Palette gap (designer to confirm, then added to `style.md`): oni skin red, PROPOSED `#A8372C` (a deeper, browner red than shrine red `#B5332B` so the skin does not merge with shrine trim or the hit-spark red). Until confirmed, use `#B5332B` darkened one tone step.
- **Club:** iron kanabo with studs, `ink` shaft with brass studs. Kept OFF the body model (see rig notes). Not in the turnaround.

## Prompts (after the style block in `assets/specs/style.md`)

These are written so the same text works as an Astra brief (no tool-specific syntax). Replace "Avoid: [negative prompt]" with the block from style.md.

Concept front view (O1, text_to_image, 2:3 portrait, nano-banana-pro):
> Full-body concept art of a towering horned ogre fighter, front view, neutral standing pose with the arms slightly away from the body and the feet apart, plain white background. Broad shoulders, thick arms, heavy chest, proud blunt expression with a heavy brow and two small tusks, two short pale horns, coarse ink-black mane, skin in deep oni red #A8372C, indigo #2D3A5E short trousers tied with a rice-paper rope belt, rice-paper wraps on the forearms and shins, bare feet. No weapon, no held items, no effects. Avoid: [negative prompt].

Turnaround (O2, image_to_image, reference = the chosen O1 image):
> Character turnaround sheet of the same horned ogre fighter: front, side and back views, same height and scale, T-pose (arms straight out), plain white background. Keep the art style, proportions and palette of the reference exactly. No weapon, no held items. Avoid: [negative prompt].

Club (O4, text_to_image, 1:1, nano-banana):
> A single iron club with a thick studded head and a wrapped handle, shown upright on its own, side view, plain white background, flat cel shading, ink shaft, brass studs. Avoid: [negative prompt], hands, body, effects.

## Job chain

| # | Tool | Settings | Credits |
|---|---|---|---|
| O1 | `meshy_text_to_image` | `nano-banana-pro`, aspect 2:3, concept prompt (or Astra, see job list) | 9 |
| O2 | `meshy_image_to_image` | `nano-banana-pro`, `reference_file_paths: [O1]`, `generate_multi_view: true` | 9 |
| O3 | `meshy_image_to_3d` | shared settings (style.md): `meshy-7.1`, front view of O2, `target_polycount: 15000`, `pose_mode: t-pose`, `texture_resolution: 2k`, `enable_pbr: false`, `image_enhancement: false`, `auto_size`, `origin_at: bottom` (~2.0 m) | 30 |
| O4 | `meshy_text_to_image` | `nano-banana`, club prompt, 1:1 | 3 |
| O5 | `meshy_image_to_3d` | `meshy-6-lite`, O4 image, `target_polycount: 4000`, `texture_resolution: 2k`, `enable_pbr: false` | 15 |

Per-take total 66 (body 48, club 18). Take cap 3. Stop at the unrigged T-pose mesh. Rig (`meshy_rig`, 2.0 m, 5 credits) is `rig-specialist`'s and is NOT in this approval.

## Rig notes for rig-specialist

| Part | Modelled as | Note |
|---|---|---|
| Body, head, limbs | one mesh | Meshy humanoid skeleton, shared clips |
| Horns, mane | baked to head | mane must stay short enough not to clip the shoulders in kicks |
| Kanabo (club) | SEPARATE rigid mesh (O5) | attached to the right-hand `BoneAttachment3D`; grip pivot at the handle, scaled to ~1.2 m. Needed because Ryo's shared clips are bare-handed: club visibility and attach pose is a rig/animator call |
| Rope belt, wraps | baked | no sway |

Bulk risk: the thick arms and torso will overlap on the shared clips (hands-on-hips, throws). Record in the G4 test.

## Acceptance criteria

- Palette matches (oni red skin, ink mane, indigo trousers); two horns and tusks readable; no weapon baked into the body mesh or turnaround.
- Silhouette reads wide and heavy at 720p, clearly bigger than Ryo; hands separate from the torso.
- Consistency: outline weight and tone steps match accepted Ryo and Kitsune (take 2).
- Mesh: ~15k tris, one 2K texture, no PBR, T-pose, origin at feet, Y-up, fingers separated.
- Club: one clean piece, pivot at the handle, ink shaft.
- G4 readiness: bulk and horn clearance for walk, heavy and throw; record the risk. Fallback if the gate fails: folklore human guise (a stout man in plain work clothes, new spec, optional line G1 in the job list).
- No resemblance to an existing character or franchise; no text.
- Licence: plan tier unknown, flagged.

## Takes
None yet.

## Acceptance
Not run.
