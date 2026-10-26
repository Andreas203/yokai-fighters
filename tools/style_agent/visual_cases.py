"""The three visual test cases: a real asset slot from the stage specs, plus a brief built to break one constraint type.

The slots are the three stage backdrops still missing (S1 in YOK-150, 151 and 152; Meshy jobs approved
but never run). Astra gets the slot and the sabotaged brief, with no style block. The Evaluator and the
Refiner get only the slot, so the loop has to find the damage in the picture itself.
"""
from __future__ import annotations

# The spec paths below are as the recorded run saw them; #236 later moved the specs to assets/specs/stages/.
# They are prompt text, so changing them would break --replay of runs/visual and cost new Astra takes.
SLOTS = {
    "oni-shrine-gate": {
        "asset": "Stage backdrop S1 for the Oni's stage (YOK-150, assets/specs/snowbound-shrine-gate.md)",
        "subject": "The Oni's stage: a snowbound mountain shrine gate.",
    },
    "kappa-riverbank": {
        "asset": "Stage backdrop S1 for the Kappa's stage (YOK-151, assets/specs/lantern-riverbank.md)",
        "subject": "The Kappa's stage: a riverbank of floating lanterns.",
    },
    "tanuki-tea-house": {
        "asset": "Stage backdrop S1 for the Tanuki's stage (YOK-152, assets/specs/tanuki-tea-house.md)",
        "subject": "The Tanuki's stage: the inside of its tea house.",
    },
}

CASES = [
    {
        "id": "visual-1-look",
        "violation_class": "Look",
        "slot": "oni-shrine-gate",
        "brief": ("Wide 16:9 landscape, full-bleed to the edges, no people and no text, the lower third left open. Make it a "
                  "photorealistic cinematic render like a AAA game screenshot: ray-traced glossy ice, volumetric god rays, "
                  "deep dramatic cast shadows, film grain, shallow depth of field and ultra-detailed 8k textures."),
    },
    {
        "id": "visual-2-palette-lore",
        "violation_class": "Palette and lore",
        "slot": "kappa-riverbank",
        "brief": ("Wide 16:9 landscape, full-bleed, nobody in it and no text, in a flat cel-shaded cartoon style with clean "
                  "ink outlines. Set it on a bright sunny tropical beach at noon in spring: turquoise water, hot-pink cherry "
                  "blossoms, palm trees, neon lanterns in electric blue and lime green, and a modern city skyline on the horizon."),
    },
    {
        "id": "visual-3-spec-form",
        "violation_class": "Spec form",
        "slot": "tanuki-tea-house",
        "brief": ("Flat cel-shaded style with ink outlines and muted ukiyo-e colours. Make it a square poster with an ornate "
                  "decorative border: the round Tanuki merchant standing in the centre foreground pouring tea, guests kneeling "
                  "around him, cushions and teacups crowding the floor, and the title YOKAI FIGHTERS in big calligraphy "
                  "across the top with an artist's signature in the corner."),
    },
]


# ---------- real cases: the stage specs' own backdrop prompts, no sabotaged brief ----------
#
# Each scene is the spec's S1 backdrop prompt word for word, without its closing "Avoid: [negative prompt]."; the
# pipeline wraps it in style.md's style and negative blocks, as the spec says. The spec's job settings
# (`aspect_ratio: 16:9`) go to Astra as a setting. No violation class is set in advance: the Evaluator finds what
# breaks. test_visual.py checks each scene against its spec.

REAL_SLOTS = {
    "oni-stage": {
        "asset": "Stage backdrop S1 for the Oni's stage (YOK-150, assets/specs/stages/snowbound-shrine-gate.md)",
        "subject": "The Oni's stage: a snowbound mountain shrine gate.",
    },
    "kappa-stage": {
        "asset": "Stage backdrop S1 for the Kappa's stage (YOK-151, assets/specs/stages/lantern-riverbank.md)",
        "subject": "The Kappa's stage: a riverbank of floating lanterns.",
    },
    "tanuki-stage": {
        "asset": "Stage backdrop S1 for the Tanuki's stage (YOK-152, assets/specs/stages/tanuki-tea-house.md)",
        "subject": "The Tanuki's stage: the inside of its tea house.",
    },
}
SLOTS.update(REAL_SLOTS)

REAL_CASES = [
    {
        "id": "real-1-oni-shrine-gate",
        "slot": "oni-stage",
        "spec": "assets/specs/stages/snowbound-shrine-gate.md",
        "scene": ('Wide stage backdrop, a snowbound mountain shrine gate at blue dusk seen from a low flat viewpoint, a large '
                  'shrine-red gate standing in the centre-right distance with a thick straw rope across it, snow-laden dark '
                  'pine trees on both sides, layered indigo mountain ridges fading into pale mist, soft large snowflakes in the '
                  'air, a faint stone path buried in snow, empty and uncluttered in the lower third, no characters, no text, '
                  'full-bleed to the image edges with no border or frame. Flat cel colours, soft ink-brush edges. Palette: '
                  'indigo #2D3A5E, rice paper #F1E8D4, snow shade #A9B4C8, shrine red #B5332B, pine #34483B.'),
        "settings": "aspect_ratio: 16:9",
    },
    {
        "id": "real-2-kappa-riverbank",
        "slot": "kappa-stage",
        "spec": "assets/specs/stages/lantern-riverbank.md",
        "scene": ('Wide stage backdrop, a calm river at night seen from a low flat viewpoint, dozens of small round paper '
                  'lanterns glowing persimmon orange drifting on dark indigo water in layered depth, a far bank of pine-green '
                  'reeds and low hills fading into indigo mist, a small arched wooden footbridge far in the distance, a few '
                  'rice-paper-lit stars, empty and uncluttered in the lower third, no characters, no text, full-bleed to the '
                  'image edges with no border or frame. Flat cel colours, soft ink-brush edges, water as flat bands. Palette: '
                  'indigo #2D3A5E, persimmon #D8632C, water teal #3E7F86, pine #34483B, rice paper #F1E8D4.'),
        "settings": "aspect_ratio: 16:9",
    },
    {
        "id": "real-3-tanuki-tea-house",
        "slot": "tanuki-stage",
        "spec": "assets/specs/stages/tanuki-tea-house.md",
        "scene": ('Wide stage backdrop, the interior of a warm Japanese tea house seen from a low flat viewpoint, dark wooden '
                  'beams and posts, a long back wall made of five tall shoji screens with blank rice-paper panels evenly spaced '
                  '(panels completely empty and plain pale paper, no drawing on them), a low wooden ceiling with two hanging '
                  'paper lanterns glowing persimmon, tatami floor visible in the lower third, empty and uncluttered in the '
                  'lower third, no characters, no text, full-bleed to the image edges with no border or frame. Flat cel '
                  'colours, soft ink-brush edges. Palette: rice paper #F1E8D4, straw #B08A48, brown #6E5A48, persimmon #D8632C, '
                  'shrine red #B5332B.'),
        "settings": "aspect_ratio: 16:9",
    },
]
