"""The Yokai Fighters style guide for generated visual assets (stage backdrops), as data.

Every rule is taken from the design: rules.md V1 and V9, assets/specs/style.md (the shared
style block, palette and negative prompt every asset prompt uses), the Astra brief's rules for
every output, and the three stage specs (snowbound-shrine-gate.md, lantern-riverbank.md,
tanuki-tea-house.md). Three constraint types: look, palette and lore, spec form.
"""
from __future__ import annotations

CLASSES = {
    "L": "Look",
    "P": "Palette and lore",
    "S": "Spec form",
}

# assets/specs/style.md, "Palette (V1)". Names and hexes copied exactly.
PALETTE = {
    "indigo": "#2D3A5E", "ink": "#1D1B21", "persimmon": "#D8632C", "rice paper": "#F1E8D4",
    "pine": "#34483B", "fur white": "#FBF6EC", "foxfire": "#F2A23A", "shrine red": "#B5332B",
    "brass": "#B08A48", "snow shade": "#A9B4C8", "water teal": "#3E7F86", "brown": "#6E5A48",
    "spring pink": "#E8A6B0", "wood dark": "#4A3425", "wood mid": "#7A5535", "straw": "#C9B58C",
}

# assets/specs/style.md, "Style block" and "Negative prompt". Every asset prompt starts and ends with these.
STYLE_BLOCK = ("Stylised toon-shaded game art, flat cel colour in 2-3 tone steps, clean confident ink-brush outline of even "
               "weight, muted ukiyo-e colour palette, matte surfaces, soft even lighting with no cast shadows, no photographic "
               "or painterly texture, no gloss, no baked ambient occlusion, simple readable shapes.")
NEGATIVE_BLOCK = ("Avoid: photorealism, PBR detail, metallic sheen, painterly brush noise, film grain, text, watermark, logo, "
                  "signature, extra limbs, extra fingers, held props or effects not in the reference, glowing particles, cast "
                  "shadows, busy background, any resemblance to an existing franchise, artist or character.")

RULES = [
    # ---------- Look ----------
    {"id": "L1", "rule": "Toon-shaded game art: flat cel colour in 2-3 tone steps per surface, with a clean ink-brush outline of "
                         "even weight. Shapes are simple and read at 720p.",
     "source": "rules.md V1; style.md Style block"},
    {"id": "L2", "rule": "Matte surfaces and soft even lighting: no cast shadows, gloss, metallic sheen, rim light, baked ambient "
                         "occlusion, bloom or glowing particles. A lantern's glow is a flat orange fill, never a bloom.",
     "source": "style.md Style block and Negative prompt; Astra brief, Rules for every output; lantern-riverbank.md Palette"},
    {"id": "L3", "rule": "No photographic, PBR or painterly texture: no film grain, painterly brush noise, gritty detail, depth-of-field "
                         "blur or volumetric light. The game adds its own toon shader and outline, so baked rendering is a RETAKE.",
     "source": "rules.md V1; style.md Shared 3D settings; Astra brief, Rules for every output"},
    {"id": "L4", "rule": "It matches the accepted bamboo grove backdrop (the REFERENCE image) in outline weight, flatness and level "
                         "of detail, so the stages read as one game.",
     "source": "style.md Acceptance checklist 2; snowbound-shrine-gate.md Acceptance criteria"},

    # ---------- Palette and lore ----------
    {"id": "P1", "rule": "Colours come from the muted ukiyo-e palette in style.md: indigo #2D3A5E, ink #1D1B21, persimmon #D8632C, "
                         "rice paper #F1E8D4, pine #34483B, foxfire #F2A23A, shrine red #B5332B, brass #B08A48, snow shade "
                         "#A9B4C8, water teal #3E7F86, brown #6E5A48, spring pink #E8A6B0 and the wood and straw tones. No "
                         "neon, saturated primaries, hot pink or turquoise, and snow is never pure white.",
     "source": "rules.md V1; style.md Palette; snowbound-shrine-gate.md Palette"},
    {"id": "P2", "rule": "Each stage shows its own place and hour: the Oni's snowbound mountain shrine gate at blue dusk (a shrine-red "
                         "gate with a straw rope, snow-laden pines, indigo ridges); the Kappa's river at deep indigo night with "
                         "paper lanterns drifting on dark water and reeds; the Tanuki's lamplit tea house (wooden beams, tatami, a "
                         "row of tall blank paper screens); the Kitsune's bamboo grove at dusk.",
     "source": "rules.md V9; vault Art-and-Audio#Stages; the three stage specs, Stage mood"},
    {"id": "P3", "rule": "The world is one rural Japanese province at the end of autumn. No modern objects, cities, castles, neon "
                         "signs or tropical scenery; spring colour appears only on the tea house's season screens.",
     "source": "GDD §2.1; vault Tone#The tone line, Story#The illusion loop; tanuki-tea-house.md Palette"},
    {"id": "P4", "rule": "Mischievous folklore with a melancholy edge, never horror: no skulls, blood, menacing faces, ruins or "
                         "dread. Quiet, lantern-lit and a little lonely.",
     "source": "rules.md S1 (tone applies to the whole game); vault Tone#The tone line"},

    # ---------- Spec form ----------
    {"id": "S1", "rule": "A stage backdrop is a wide 16:9 landscape image, full-bleed to the edges: no border, frame, vignette, "
                         "letterbox or rounded corners.",
     "source": "the three stage specs, Backdrop (S1) prompt and Acceptance criteria (\"no letterbox after crop\")"},
    {"id": "S2", "rule": "No characters, people, yokai or animals in a backdrop: the fighters stand in front of it. The tea house's "
                         "screens are blank (a drawing on them is a RETAKE).",
     "source": "the three stage specs, Backdrop (S1) prompt; tanuki-tea-house.md Acceptance criteria"},
    {"id": "S3", "rule": "No text of any kind: no title, letters, kanji, logo, signature or watermark.",
     "source": "style.md Negative prompt; stage specs Acceptance criteria"},
    {"id": "S4", "rule": "The lower third is calm, empty and low-contrast, because that is the band the fighters stand in; detail "
                         "sits in the upper two thirds and the edges.",
     "source": "the three stage specs, Backdrop (S1) prompt (\"empty and uncluttered in the lower third\") and Acceptance criteria"},
]

RUBRIC = (
    "Start at 10 and deduct for every violation. A hard break costs 3 points: a photographic, glossy or painterly render; "
    "cast shadows or bloom; colours plainly outside the palette (neon, saturated primaries, pure-white snow); the wrong "
    "place, season or hour for the stage; horror; a border, frame or letterbox; not 16:9; any character or figure; any "
    "text or signature; a busy lower third. A soft drift costs 1 point: a little more detail or gradient than the "
    "reference, one colour slightly off the palette, outline weight unlike the reference. The score never goes below 1. "
    "Give 10 only when no rule is broken."
)


def palette_lines() -> str:
    return ", ".join(f"{name} {hexv}" for name, hexv in PALETTE.items())


def render_guide() -> str:
    """The visual style guide as the Evaluator and the Refiner read it."""
    lines = ["YOKAI FIGHTERS STYLE GUIDE: STAGE BACKDROPS", ""]
    for prefix, title in CLASSES.items():
        lines.append(title.upper())
        for r in RULES:
            if r["id"].startswith(prefix):
                lines.append(f"{r['id']}. {r['rule']} (Source: {r['source']})")
        lines.append("")
    lines += ["PROMPT CONVENTION (style.md): every asset prompt starts with the shared style block and ends with the shared "
              "negative block. The pipeline adds both around the Refiner's scene description:",
              f"STYLE BLOCK: {STYLE_BLOCK}", f"NEGATIVE BLOCK: {NEGATIVE_BLOCK}", ""]
    return "\n".join(lines)
