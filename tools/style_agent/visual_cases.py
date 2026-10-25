"""The three visual test cases: a real asset slot from the stage specs, plus a brief built to break one constraint type.

The slots are the three stage backdrops still missing (S1 in YOK-150, 151 and 152; Meshy jobs approved
but never run). Astra gets the slot and the sabotaged brief, with no style block. The Evaluator and the
Refiner get only the slot, so the loop has to find the damage in the picture itself.
"""
from __future__ import annotations

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
