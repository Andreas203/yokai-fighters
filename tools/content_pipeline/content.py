"""The three content types this pipeline writes, and the prompts that write them.

Each type fills a gap in the game's data (see the `gap` field of each type). The prompts carry only
the output contract: shape, length, placeholders. Everything about the world and
the voice has to come from the retrieved vault chunks.
"""
from __future__ import annotations

TONE = "01-vibe/Tone.md"
STORY = "01-vibe/Story.md"
CHARACTERS = "01-vibe/Characters.md"
ABILITIES = "02-mechanics/Abilities.md"
YOKAI = "02-mechanics/Yokai.md"

# Stretch-goal names the GDD keeps out of the 5-week build (vault: Characters#Not in the 5-week build).
OUT_OF_BUILD = ["jorogumo", "tengu", "tetsu", "kaede", "yurei"]


def _wake_up(item_id, yokai, yokai_query, rows, row_note, talismans):
    return {
        "id": item_id,
        "label": f"Wake-up card: {yokai} wins at {rows}",
        "request": f"Wake-up story card shown after Ryo loses a duel to the {yokai} at {rows}.",
        "brief": (
            f"Write the wake-up story card shown after Ryo loses a duel to the {yokai} at {rows}. "
            f"{row_note}"
        ),
        "trigger": f"Later loss: Ryo is knocked out by the {yokai} at {rows} (wake-up variant by row reached and winning yokai, S3)",
        "plan": [
            ("situation", "wake-up card after a loss, Ryo wakes at the province's edge", STORY, 2),
            ("talismans", f"talismans after a loss: {talismans}", STORY, 1),
            ("yokai", yokai_query, CHARACTERS, 1),
            ("voice", "narrator voice wake-up card present tense", TONE, 1),
        ],
        "pinned": [f"{TONE}#The tone line", f"{TONE}#Card form", f"{TONE}#Do and don't examples"],
        "critic_extra": [],
    }


def _elder_intro(item_id, elder, yokai, section):
    return {
        "id": item_id,
        "label": f"Elder intro card: {elder}",
        "request": f"One-line intro card for the {elder} that teaches its elder twist before the duel.",
        "brief": f"Write the one-line intro card shown on the map node of the {elder}, before the duel starts.",
        "trigger": f"Intro card on the {elder}'s map node, rows 5 and 8 (Y3)",
        "title": elder,
        "plan": [
            ("twist", f"{elder} elder twist and the habit the twist breaks", YOKAI, 1),
            ("card", "elder intro card: one line that teaches the twist", STORY, 1),
            ("yokai", f"{yokai} personality folklore stage", CHARACTERS, 1),
            ("voice", "narrator voice quiet observant", TONE, 1),
        ],
        "pinned": [f"{TONE}#The tone line", f"{TONE}#Card form"],
        "critic_extra": [f"{YOKAI}#{section}", f"{CHARACTERS}#{section}"],
    }


def _modifier_card(item_id, name, yokai, section):
    return {
        "id": item_id,
        "label": f"Reward card text: {name}",
        "request": f"Reward card text for the {yokai} modifier {name}.",
        "brief": f"Write the reward card text for the modifier {name}, offered by the {yokai}.",
        "modifier": name,
        "source": yokai.lower(),
        "plan": [
            ("effect", f"{name} modifier effect on the special it is attached to", ABILITIES, 1),
            ("voice", "reward card voice: plain and frames examples", TONE, 1),
            ("yokai", f"{yokai} personality folklore", CHARACTERS, 1),
        ],
        "pinned": [],
        "critic_extra": [f"{ABILITIES}#{section}"],
    }


CONTENT_TYPES = {
    "wake-up-card": {
        "title": "Wake-up cards (later losses)",
        "gap": "The GDD schedules a wake-up card for every later loss, varying by row reached and winning yokai. "
               "data/story/ has one generic lose-screen card.",
        "output": '{"text": "<the card>"}',
        "contract": [
            "One or two sentences, at most 280 characters in total.",
            "Name the yokai that won only through the placeholder {yokai}; the game fills in its name. Use {yokai} at least once.",
            "No other placeholders. Do not name any move.",
        ],
        "critic_pinned": [
            f"{TONE}#The tone line", f"{TONE}#Do and don't examples", f"{TONE}#Card form",
            f"{TONE}#Voice: the narrator", f"{STORY}#Wake-up cards", f"{STORY}#Talismans",
        ],
        "items": [
            _wake_up("wake-up-oni-early", "Oni", "Oni personality stage folklore iron club", "row 2 or 3",
                     "The run ended at row 5 or earlier.", "run ended at row 5 or earlier, every talisman blank"),
            _wake_up("wake-up-nine-tails-row-5", "Nine-Tailed Kitsune", "Kitsune fox nine tails foxfire personality stage",
                     "row 5", "The run ended at row 5 or earlier.", "run ended at row 5 or earlier, every talisman blank"),
            _wake_up("wake-up-kappa-row-6", "Kappa", "Kappa personality stage folklore river", "row 6",
                     "The run reached row 6.", "run reached row 6, one talisman stays faintly inked carry-over"),
        ],
    },
    "elder-intro-card": {
        "title": "Elder intro cards",
        "gap": "Each elder's twist needs a one-line intro card on its map node (Y3). None of the three is written.",
        "output": '{"text": "<the card>"}',
        "contract": [
            "Exactly one sentence, at most 140 characters.",
            "A player who reads it should be able to work out the elder's twist before the duel.",
            "No placeholders.",
        ],
        "critic_pinned": [
            f"{TONE}#The tone line", f"{TONE}#Do and don't examples", f"{TONE}#Card form",
            f"{STORY}#Elder intro cards", f"{YOKAI}#Elders",
        ],
        "items": [
            _elder_intro("elder-intro-nine-tailed-kitsune", "Nine-Tailed Kitsune", "Kitsune", "Kitsune"),
            _elder_intro("elder-intro-elder-oni", "Elder Oni", "Oni", "Oni"),
            _elder_intro("elder-intro-elder-kappa", "Elder Kappa", "Kappa", "Kappa"),
        ],
    },
    "modifier-card": {
        "title": "Reward card text for modifiers",
        "gap": "14 modifiers ship and each needs reward card text. Two are written; these four are the unlocked "
               "modifiers that survive even a balance-gate cut.",
        "output": '{"plain": "<plain-language line>", "frames": "<exact effect>"}',
        "contract": [
            "plain: one sentence, at most 140 characters, shown first on the card.",
            "frames: at most 140 characters, shown when the frame-data toggle is on.",
        ],
        "critic_pinned": [f"{TONE}#Reward card voice", f"{ABILITIES}#The reward screen"],
        "items": [
            _modifier_card("onis-hide-card", "Oni's Hide", "Oni", "Oni modifiers"),
            _modifier_card("iron-will-card", "Iron Will", "Oni", "Oni modifiers"),
            _modifier_card("slippery-skin-card", "Slippery Skin", "Kappa", "Kappa modifiers"),
            _modifier_card("river-pull-card", "River Pull", "Kappa", "Kappa modifiers"),
        ],
    },
}

GENERATOR_SYSTEM = (
    "You write in-game text for Yokai Fighters, a roguelite fighting game. "
    "You know nothing about this game except the CONTEXT you are given, which is retrieved from its design vault. "
    "Take every fact about the world, the characters and the mechanics from the CONTEXT, and write in the voice the CONTEXT describes. "
    "If the CONTEXT does not state something, do not make it up. "
    "Reply with one JSON object and nothing else."
)


def format_context(chunks) -> str:
    return "\n\n".join(f"[{c.id}]\n{c.text}" for c in chunks)


def generation_prompt(ctype: dict, item: dict, chunks) -> str:
    contract = "\n".join(f"- {line}" for line in ctype["contract"])
    return (
        f"TASK\n{item['brief']}\n\n"
        f"OUTPUT CONTRACT\n{contract}\n"
        f"- Reply as JSON: {ctype['output']}\n\n"
        f"CONTEXT (design vault excerpts)\n{format_context(chunks)}\n"
    )


def revision_prompt(ctype: dict, item: dict, chunks, draft: dict, issues: list[dict]) -> str:
    import json
    listed = "\n".join(
        f"{n}. [{i['kind']}] \"{i.get('quote', '')}\": {i['problem']}"
        + (f" Evidence: {i['evidence']}" if i.get("evidence") else "")
        + (f" Suggested fix: {i['fix']}" if i.get("fix") else "")
        for n, i in enumerate(issues, 1)
    )
    return (
        generation_prompt(ctype, item, chunks)
        + f"\nYOUR PREVIOUS DRAFT\n{json.dumps(draft, ensure_ascii=False)}\n\n"
        f"THE CRITIC REJECTED IT\n{listed}\n\n"
        "Rewrite the draft so that every issue is fixed. Keep what was not criticised. "
        "Reply with the same JSON shape and nothing else.\n"
    )
