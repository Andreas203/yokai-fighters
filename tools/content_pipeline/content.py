"""The three content types this pipeline writes, and the prompts that write them.

Each type fills a gap in the game's data (see the `gap` field of each type) and
names the crew agent that writes it. The prompts carry only the output contract:
shape, length, placeholders. Everything about the world and the voice has to
come from the vault notes that agent loads.
"""
from __future__ import annotations

# The crew agent that checks every draft. Its note list is in .claude/agents/rules-lawyer.md.
CRITIC = "rules-lawyer"

# Stretch-goal names the GDD keeps out of the 5-week build (vault: Characters#Not in the 5-week build).
OUT_OF_BUILD = ["jorogumo", "tengu", "tetsu", "kaede", "yurei"]


def _wake_up(item_id, yokai, rows, row_note):
    return {
        "id": item_id,
        "label": f"Wake-up card: {yokai} wins at {rows}",
        "brief": (
            f"Write the wake-up story card shown after Ryo loses a duel to the {yokai} at {rows}. "
            f"{row_note}"
        ),
        "trigger": f"Later loss: Ryo is knocked out by the {yokai} at {rows} (wake-up variant by row reached and winning yokai, S3)",
    }


def _elder_intro(item_id, elder):
    return {
        "id": item_id,
        "label": f"Elder intro card: {elder}",
        "brief": f"Write the one-line intro card shown on the map node of the {elder}, before the duel starts.",
        "trigger": f"Intro card on the {elder}'s map node, rows 5 and 8 (Y3)",
        "title": elder,
    }


def _modifier_card(item_id, name, yokai):
    return {
        "id": item_id,
        "label": f"Reward card text: {name}",
        "brief": f"Write the reward card text for the modifier {name}, offered by the {yokai}.",
        "modifier": name,
        "source": yokai.lower(),
    }


CONTENT_TYPES = {
    "wake-up-card": {
        "title": "Wake-up cards (later losses)",
        "writer": "habit-writer",
        "gap": "The GDD schedules a wake-up card for every later loss, varying by row reached and winning yokai. "
               "data/story/ varies the loss card by row only; no card is about the yokai that won.",
        "output": '{"text": "<the card>", "sources": [...]}',
        "contract": [
            "One or two sentences, at most 280 characters in total.",
            "Name the yokai that won only through the placeholder {yokai}; the game fills in its name. Use {yokai} at least once.",
            "No other placeholders. Do not name any move.",
        ],
        "items": [
            _wake_up("wake-up-oni-early", "Oni", "row 2 or 3", "The run ended at row 5 or earlier."),
            _wake_up("wake-up-nine-tails-row-5", "Nine-Tailed Kitsune", "row 5", "The run ended at row 5 or earlier."),
            _wake_up("wake-up-kappa-row-6", "Kappa", "row 6", "The run reached row 6."),
        ],
    },
    "elder-intro-card": {
        "title": "Elder intro cards",
        "writer": "habit-writer",
        "gap": "Each elder's twist needs a one-line intro card on its map node (Y3). None of the three is written.",
        "output": '{"text": "<the card>", "sources": [...]}',
        "contract": [
            "Exactly one sentence, at most 140 characters.",
            "A player who reads it should be able to work out the elder's twist before the duel.",
            "No placeholders.",
        ],
        "items": [
            _elder_intro("elder-intro-nine-tailed-kitsune", "Nine-Tailed Kitsune"),
            _elder_intro("elder-intro-elder-oni", "Elder Oni"),
            _elder_intro("elder-intro-elder-kappa", "Elder Kappa"),
        ],
    },
    "modifier-card": {
        "title": "Reward card text for modifiers",
        "writer": "movesmith",
        "gap": "14 modifiers ship and each needs reward card text. Two are written; these four are the unlocked "
               "modifiers that survive even a balance-gate cut.",
        "output": '{"plain": "<plain-language line>", "frames": "<exact effect>", "sources": [...]}',
        "contract": [
            "plain: one sentence, at most 140 characters, shown first on the card.",
            "frames: at most 140 characters, shown when the frame-data toggle is on.",
        ],
        "items": [
            _modifier_card("onis-hide-card", "Oni's Hide", "Oni"),
            _modifier_card("iron-will-card", "Iron Will", "Oni"),
            _modifier_card("slippery-skin-card", "Slippery Skin", "Kappa"),
            _modifier_card("river-pull-card", "River Pull", "Kappa"),
        ],
    },
}

WRITER_SYSTEM = (
    "You write in-game text for Yokai Fighters, a roguelite fighting game. "
    "You know nothing about this game except the NOTES you are given, which are whole notes from its design vault. "
    "Take every fact about the world, the characters and the mechanics from the NOTES, and write in the voice the NOTES describe. "
    "If the NOTES do not state something, do not make it up. "
    "Reply with one JSON object and nothing else."
)

# The writer names the lines its text rests on; code then checks each one is in a note it loaded.
SOURCES_RULE = (
    '- "sources" lists the lines of the NOTES your text rests on, one to four of them, as '
    '{"note": "<note id>", "line": "<the line>"}. Copy each line word for word from the NOTES: '
    "one sentence or one table row, with no ellipsis and no rewording."
)


def format_notes(notes) -> str:
    return "\n\n".join(f"[{n.id}]\n{n.text}" for n in notes)


def writing_prompt(ctype: dict, item: dict, notes) -> str:
    contract = "\n".join(f"- {line}" for line in ctype["contract"])
    return (
        f"TASK\n{item['brief']}\n\n"
        f"OUTPUT CONTRACT\n{contract}\n"
        f"{SOURCES_RULE}\n"
        f"- Reply as JSON: {ctype['output']}\n\n"
        f"NOTES (design vault, each note complete)\n{format_notes(notes)}\n"
    )


def revision_prompt(ctype: dict, item: dict, notes, draft: dict, issues: list[dict]) -> str:
    import json
    listed = "\n".join(
        f"{n}. [{i['kind']}] \"{i.get('quote', '')}\": {i['problem']}"
        + (f" Evidence: {i['evidence']}" if i.get("evidence") else "")
        + (f" Suggested fix: {i['fix']}" if i.get("fix") else "")
        for n, i in enumerate(issues, 1)
    )
    return (
        writing_prompt(ctype, item, notes)
        + f"\nYOUR PREVIOUS DRAFT\n{json.dumps(draft, ensure_ascii=False)}\n\n"
        f"THE CRITIC REJECTED IT\n{listed}\n\n"
        "Rewrite the draft so that every issue is fixed. Keep what was not criticised. "
        "Reply with the same JSON shape and nothing else.\n"
    )

