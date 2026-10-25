"""The three agents of the style loop and the code measurements they share.

    Generator (no style guide) --> Evaluator: SCORE: X/10 + REASON --> Refiner rewrites from the REASON --> Evaluator ...

The Generator knows only the game's one-line pitch and the brief, so it writes
the way an outside model would. The Evaluator and the Refiner read the whole
style guide. Before every evaluation, code measures what a model miscounts
(characters, sentences, placeholders, listed words) and hands the counts to
the Evaluator as facts; the Evaluator still decides the score.
"""
from __future__ import annotations

import re
import unicodedata

from style_guide import HORROR_WORDS, OUT_OF_BUILD, PLACEHOLDERS, RUBRIC, SYSTEM_WORDS, render_guide

GENERATOR_SYSTEM = (
    "You write in-game text for Yokai Fighters, a 2.5D fighting game for PC in which Ryo, an apprentice exorcist, "
    "fights yokai. Follow the brief exactly. Reply with the card text only."
)

EVALUATOR_SYSTEM = (
    "You are the style Evaluator for Yokai Fighters. You grade one piece of story-card text against the game's "
    "STYLE GUIDE and nothing else: ignore anything you believe about the game from elsewhere, and do not mark down "
    "matters of taste that no rule covers. Reply strictly in the format you are given and nothing else."
)

REFINER_SYSTEM = (
    "You are the style Refiner for Yokai Fighters. You rewrite one piece of story-card text so that it follows the "
    "game's STYLE GUIDE exactly. Take every fact about the world from the STYLE GUIDE and the CARD SLOT; do not "
    "invent new characters, places or events. Reply with the rewritten card text only."
)


# ---------- code measurements ----------

def _plain(text: str) -> str:
    text = unicodedata.normalize("NFKD", text)
    return "".join(c for c in text if not unicodedata.combining(c)).lower()


def split_sentences(text: str) -> list[str]:
    """Sentences as the card limit counts them: sentences inside quoted speech count too."""
    t = text.replace("...", "…").strip()
    # An ellipsis is a pause inside a sentence ("That {move} of yours... I'd love one."), not an end.
    # A new sentence starts with a capital, a quote or a placeholder; '"...exorcist?" it asks' stays one sentence.
    starts = r"(?=[A-Z\"“'‘{])"
    parts = [p.strip() for p in re.split(rf"(?<=[.!?][\"'”’)])\s+{starts}|(?<=[.!?])\s+{starts}", t) if p.strip()]
    return [p.replace("…", "...") for p in parts]


def count_sentences(text: str) -> int:
    return len(split_sentences(text))


def _hits(text: str, words: list[str]) -> list[str]:
    plain = _plain(text)
    return [w for w in words if re.search(rf"\b{re.escape(w)}", plain)]


def measure(text: str, slot: dict) -> dict:
    """What code can count for sure. Each entry is a fact; `flags` lists the ones that break a limit."""
    placeholders = sorted(set(re.findall(r"\{(\w+)\}", text)))
    m = {
        "characters": len(text),
        "max_characters": slot["max_chars"],
        "sentences": count_sentences(text),
        "sentence_list": split_sentences(text),
        "max_sentences": slot["max_sentences"],
        "placeholders": placeholders,
        "system_words": _hits(text, SYSTEM_WORDS),
        "out_of_build_names": _hits(text, OUT_OF_BUILD),
        "horror_words": _hits(text, HORROR_WORDS),
        "exclamation_marks": text.count("!"),
        "markdown_or_list": bool(re.search(r"^\s*(#|[-*•]\s|\d+\.\s)|\*\*", text, re.M)),
        "line_breaks": text.strip().count("\n"),
        "digits": re.findall(r"\d+", text),
    }
    flags = []
    if m["characters"] > m["max_characters"]:
        flags.append(f"F1: {m['characters']} characters, over the limit of {m['max_characters']}")
    if m["sentences"] > m["max_sentences"]:
        flags.append(f"F1: {m['sentences']} sentences, over the limit of {m['max_sentences']}")
    if set(placeholders) - PLACEHOLDERS:
        flags.append(f"F3: unknown placeholders {sorted(set(placeholders) - PLACEHOLDERS)}")
    if m["system_words"] or m["digits"]:
        flags.append(f"F4: system words {m['system_words']} or numbers {m['digits']}")
    if m["out_of_build_names"]:
        flags.append(f"V3: out-of-build names {m['out_of_build_names']}")
    if m["markdown_or_list"] or m["line_breaks"]:
        flags.append("F5: Markdown, a list or line breaks")
    m["flags"] = flags
    return m


def format_measurements(m: dict) -> str:
    rows = [
        f"- characters: {m['characters']} (limit {m['max_characters']})",
        f"- sentences: {m['sentences']} (limit {m['max_sentences']}; sentences inside quoted speech count)",
        *[f"    {i}. {s}" for i, s in enumerate(m["sentence_list"], 1)],
        f"- placeholders used: {', '.join('{' + p + '}' for p in m['placeholders']) or 'none'}",
        f"- system words found: {', '.join(m['system_words']) or 'none'}",
        f"- numbers found: {', '.join(m['digits']) or 'none'}",
        f"- out-of-build names found: {', '.join(m['out_of_build_names']) or 'none'}",
        f"- horror-list words found: {', '.join(m['horror_words']) or 'none'} (a hit is a lead, not a verdict: judge the line)",
        f"- exclamation marks: {m['exclamation_marks']}",
        f"- Markdown, list or line breaks: {'yes' if m['markdown_or_list'] or m['line_breaks'] else 'no'}",
    ]
    return "\n".join(rows)


def describe_slot(slot: dict) -> str:
    return (f"{slot['card']}. When it shows: {slot['trigger']}. Speaker: {slot['speaker']}. "
            f"Limit: at most {slot['max_sentences']} sentence(s) and {slot['max_chars']} characters.")


# ---------- prompts ----------

def generator_prompt(slot: dict, brief: str) -> str:
    return f"CARD\n{describe_slot(slot)}\n\nBRIEF\n{brief}\n"


def evaluator_prompt(slot: dict, text: str, m: dict) -> str:
    return (
        f"STYLE GUIDE\n{render_guide()}\n"
        f"SCORING\n{RUBRIC}\n\n"
        f"CARD SLOT\n{describe_slot(slot)}\n\n"
        f"TEXT TO GRADE\n<<<\n{text}\n>>>\n\n"
        f"MEASUREMENTS (counted by code; trust these over your own count)\n{format_measurements(m)}\n\n"
        "Review the text. Grade it on a scale of 1-10 against the style guide's three constraint types: tone, "
        "vocabulary and lore, form and length. Reply strictly in this format and nothing else:\n"
        "SCORE: <X>/10\n"
        "REASON: <one line per violation: the rule ID in brackets, the words quoted, what breaks the rule and the "
        "points deducted, e.g. [T1] \"devour his soul\": horror image (-3). If nothing is broken, say why it meets "
        "each of the three constraint types.>\n"
    )


def refiner_prompt(slot: dict, text: str, score: int, reason: str, m: dict) -> str:
    return (
        f"STYLE GUIDE\n{render_guide()}\n"
        f"CARD SLOT\n{describe_slot(slot)}\n\n"
        f"ORIGINAL TEXT (scored {score}/10)\n<<<\n{text}\n>>>\n\n"
        f"MEASUREMENTS OF THE ORIGINAL (counted by code)\n{format_measurements(m)}\n\n"
        f"EVALUATOR'S REASON\n{reason}\n\n"
        "Take the original text and the Evaluator's REASON, and rewrite the text so that it scores a perfect 10/10 on "
        "the style guide. Fix every violation named in the REASON; keep what the card is for (the CARD SLOT) and "
        "anything the REASON did not criticise. Your rewrite must stay within the slot's sentence and character limits "
        "as the code counts them. Reply with the card text only: no quotation marks around it, no notes.\n"
    )


# ---------- parsing ----------

SCORE_LINE = re.compile(r"SCORE:\s*\**\s*(\d+(?:\.\d+)?)\s*/\s*10", re.I)
REASON_LINE = re.compile(r"REASON:\s*(.*)", re.I | re.S)


def parse_evaluation(reply: str) -> dict:
    """Read SCORE: X/10 and REASON: ... out of the Evaluator's reply. A reply with no score counts as 1/10."""
    score = SCORE_LINE.search(reply)
    reason = REASON_LINE.search(reply)
    if not score:
        return {"score": 1, "reason": "The Evaluator's reply had no SCORE line; treated as a failed evaluation.",
                "well_formed": False}
    value = max(1, min(10, round(float(score.group(1)))))
    return {"score": value, "reason": (reason.group(1).strip() if reason else "").strip(),
            "well_formed": bool(reason)}


def clean_card(reply: str) -> str:
    """The card text out of a Generator or Refiner reply: strip a code fence and quotes wrapped round the whole card."""
    text = reply.strip()
    fence = re.match(r"^```\w*\n(.*?)\n```$", text, re.S)
    if fence:
        text = fence.group(1).strip()
    pairs = {'"': '"', "“": "”"}
    if len(text) > 1 and text[0] in pairs and text[-1] == pairs[text[0]]:
        inner = text[1:-1]
        # Only a quote round the whole card: a card that quotes speech inside keeps its marks.
        if text[0] not in inner and pairs[text[0]] not in inner:
            text = inner.strip()
    return text
