"""Consistency checking: code checks for form, a critic model for lore and tone.

The code checks cover what can be decided without judgment (length, sentence
count, placeholders, names that are out of the build, numbers that are not in
the ability table, cited lines that are not in the vault). The critic model
reads the draft against the vault notes its agent loads and reports lore breaks
and tone drift, quoting the line for each. Both feed the same issue list, which
goes back to the writer for a rewrite.
"""
from __future__ import annotations

import json
import re
import unicodedata

from content import OUT_OF_BUILD, format_notes
from llm import parse_json
from vault import copied_run, has_line, table_row

# Words the tone line rules out: the game is never horror (vault: Tone#The tone line).
HORROR = [
    "blood", "bloody", "bleed", "gore", "corpse", "scream", "demon", "devour", "flesh", "rot",
    "slaughter", "murder", "kill", "terror", "nightmare", "hell", "doom", "agony", "fang",
]
# System words a story card must not use (vault: Tone#Card form).
SYSTEM_WORDS = ["health", "hp", "frames", "level", "roguelite"]

# A run of this many words taken straight from a note counts as copying, not writing.
# Eight lets a stock opening through ("You wake at the province's edge, and") and stops a lifted example.
COPY_LIMIT = 8

LIMITS = {
    "wake-up-card": {"field": "text", "max_chars": 280, "min_sent": 1, "max_sent": 2, "placeholders": {"yokai"}},
    "elder-intro-card": {"field": "text", "max_chars": 140, "min_sent": 1, "max_sent": 1, "placeholders": set()},
}


def _plain(text: str) -> str:
    text = unicodedata.normalize("NFKD", text)
    return "".join(c for c in text if not unicodedata.combining(c)).lower()


def count_sentences(text: str) -> int:
    t = text.replace("...", "…")
    return len(re.findall(r"[.!?]+[\"'”’)]*(?=\s|$)", t.strip()))


def _issue(kind, check, quote, problem, evidence="", fix=""):
    return {"by": "code", "kind": kind, "check": check, "quote": quote, "problem": problem, "evidence": evidence, "fix": fix}


def _word_hits(text: str, words: list[str]) -> list[str]:
    plain = _plain(text)
    return [w for w in words if re.search(rf"\b{re.escape(w)}", plain)]


def source_checks(draft: dict, notes) -> list[dict]:
    """Every line the writer cites must be in a note it loaded, word for word."""
    sources = draft.get("sources")
    if not isinstance(sources, list) or not sources:
        return [_issue("form", "cited lines", "", "The reply cites no lines from the notes.",
                       fix='Add "sources": the lines of the notes the text rests on.')]
    by_id = {n.id: n for n in notes}
    issues = []
    for s in sources:
        note_id, line = (str(s.get("note", "")), str(s.get("line", ""))) if isinstance(s, dict) else ("", str(s))
        if note_id not in by_id:
            issues.append(_issue("lore_break", "cited note not loaded", note_id or line,
                                 f"'{note_id}' is not one of the notes you were given.", fix="Cite only the notes you were given."))
        elif not has_line(by_id[note_id], line):
            issues.append(_issue("lore_break", "cited line not in the vault", line,
                                 f"This line is not in {note_id} word for word.", note_id,
                                 "Copy the line exactly, or drop the claim that rests on it."))
    return issues


def copy_checks(text: str, notes) -> list[dict]:
    """With whole notes in view the writer can lift a quoted example. A card is new text."""
    run, note = max(((copied_run(text, n), n) for n in notes), key=lambda pair: len(pair[0].split()))
    if len(run.split()) < COPY_LIMIT:
        return []
    return [_issue("form", "copied from the notes", run,
                   f"These {len(run.split())} words in a row are copied from {note.id}; a card is new text, not a quoted example.",
                   note.id, "Say it in new words, with a different image.")]


def code_checks(type_id: str, item: dict, draft: dict, notes, move_names: set[str], check_sources: bool = True) -> list[dict]:
    issues: list[dict] = []
    if type_id == "modifier-card":
        fields = {"plain": draft.get("plain"), "frames": draft.get("frames")}
    else:
        fields = {"text": draft.get("text")}
    for name, value in fields.items():
        if not isinstance(value, str) or not value.strip():
            return [_issue("form", "shape", "", f"The reply has no '{name}' text.", fix=f"Return a JSON object with a '{name}' string.")]

    if check_sources:
        issues += source_checks(draft, notes)
    # 'frames' is meant to repeat the ability table, so only the written line is checked for copying.
    issues += copy_checks(fields["plain" if type_id == "modifier-card" else "text"], notes)
    all_text = " ".join(fields.values())
    for name in _word_hits(all_text, OUT_OF_BUILD):
        issues.append(_issue("lore_break", "out of build", name, f"'{name}' is a stretch goal that is not in the 5-week build.",
                             "01-vibe/Characters.md#Not in the 5-week build", "Remove it."))

    if type_id == "modifier-card":
        for name, value in fields.items():
            if len(value) > 140:
                issues.append(_issue("form", "A12 length", value, f"'{name}' is {len(value)} characters; the card field holds 140.", fix="Shorten it."))
        if count_sentences(fields["plain"]) != 1:
            issues.append(_issue("form", "one sentence", fields["plain"], "'plain' must be exactly one sentence.", fix="Make it one sentence."))
        # A whole note holds every modifier's numbers, so compare against this modifier's own table row.
        row = next((r for r in (table_row(n, item["modifier"]) for n in notes) if r), "")
        known = set(re.findall(r"\d+(?:\.\d+)?", row))
        for number in re.findall(r"\d+(?:\.\d+)?", fields["frames"]):
            if number not in known:
                issues.append(_issue("lore_break", "number not in the ability table", number,
                                     f"'frames' states {number}, which is not in the ability table row for {item['modifier']}.",
                                     row, "Use only the numbers in the ability table."))
        return issues

    limits = LIMITS[type_id]
    text = fields["text"]
    if len(text) > limits["max_chars"]:
        issues.append(_issue("form", "length", text, f"The card is {len(text)} characters; the limit is {limits['max_chars']}.", fix="Shorten it."))
    n = count_sentences(text)
    if not limits["min_sent"] <= n <= limits["max_sent"]:
        want = "exactly one sentence" if limits["max_sent"] == 1 else "one or two sentences"
        issues.append(_issue("form", "S2 sentence count", text, f"The card has {n} sentences; a card of this kind is {want}.",
                             "01-vibe/Tone.md#Card form", f"Rewrite as {want}."))
    used = set(re.findall(r"\{(\w+)\}", text))
    if used != limits["placeholders"]:
        want = ", ".join("{" + p + "}" for p in sorted(limits["placeholders"])) or "none"
        got = ", ".join("{" + p + "}" for p in sorted(used)) or "none"
        issues.append(_issue("form", "S4 placeholders", got, f"Placeholders used: {got}. Expected: {want}.",
                             "01-vibe/Tone.md#Card form", f"Use exactly these placeholders: {want}."))
    if type_id == "wake-up-card":
        plain = _plain(text)
        for move in sorted(move_names):
            if re.search(rf"\b{re.escape(_plain(move))}\b", plain):
                issues.append(_issue("lore_break", "S4 hard-coded move", move,
                                     f"The card names the move '{move}', which the player may not own in this run.",
                                     "01-vibe/Tone.md#Card form", "Remove the move name."))
    for word in _word_hits(text, HORROR):
        issues.append(_issue("tone_drift", "S1 never horror", word, f"'{word}' reads as horror; the tone is never horror.",
                             "01-vibe/Tone.md#The tone line", "Replace the image with one from the game's world."))
    for word in _word_hits(text, SYSTEM_WORDS):
        issues.append(_issue("tone_drift", "system word", word, f"'{word}' is a system word; a card says what a person would see.",
                             "01-vibe/Tone.md#Card form", "Say it as an image."))
    return issues


CRITIC_SYSTEM = (
    "You are the consistency critic for Yokai Fighters, a roguelite fighting game. "
    "You judge one DRAFT of in-game text against EVIDENCE, which is whole notes from the game's design vault. "
    "The EVIDENCE is the only source of truth; ignore anything you believe about the game from elsewhere. "
    "Report two kinds of problem. "
    "lore_break: the draft contradicts the EVIDENCE, or asserts something about the world, a character or a mechanic that the EVIDENCE does not support. "
    "tone_drift: the draft breaks the tone line, a voice rule or a card-form rule stated in the EVIDENCE. "
    "Do not report matters of taste, and do not report anything you cannot tie to a line of EVIDENCE. "
    "Reply with one JSON object and nothing else."
)


def critic_prompt(ctype: dict, item: dict, draft: dict, notes) -> str:
    # The critic judges the text. The cited lines are checked by code, so they are left out.
    draft = {k: v for k, v in draft.items() if k != "sources"}
    contract = "\n".join(f"- {line}" for line in ctype["contract"])
    return (
        f"WHAT THE DRAFT IS FOR\n{item['brief']}\n\n"
        f"OUTPUT CONTRACT IT WAS GIVEN\n{contract}\n\n"
        f"DRAFT\n{json.dumps(draft, ensure_ascii=False)}\n\n"
        f"EVIDENCE (design vault, each note complete)\n{format_notes(notes)}\n\n"
        "Reply as JSON:\n"
        '{"verdict": "pass" or "revise", "issues": [{"kind": "lore_break" or "tone_drift", '
        '"quote": "<the words in the draft>", "problem": "<what is wrong, one sentence>", '
        '"evidence": "<note id>#<section heading>: <the line of evidence it breaks>", "fix": "<how to fix it, one sentence>"}]}\n'
        'Use "pass" with an empty issues list when the draft is consistent with the EVIDENCE.\n'
    )


def critic_review(llm, model: str, ctype: dict, item: dict, draft: dict, notes) -> dict:
    reply = llm.complete("critic", model, CRITIC_SYSTEM, critic_prompt(ctype, item, draft, notes))
    try:
        review = parse_json(reply)
    except ValueError:
        return {"verdict": "revise", "issues": [{"by": "critic", "kind": "form", "quote": "", "problem": "The critic's reply was not JSON; treated as a failed review.", "evidence": "", "fix": ""}]}
    issues = []
    for raw in review.get("issues") or []:
        kind = raw.get("kind") if raw.get("kind") in ("lore_break", "tone_drift") else "lore_break"
        issues.append({"by": "critic", "kind": kind, "quote": str(raw.get("quote", "")), "problem": str(raw.get("problem", "")),
                       "evidence": str(raw.get("evidence", "")), "fix": str(raw.get("fix", ""))})
    verdict = "pass" if review.get("verdict") == "pass" and not issues else "revise"
    return {"verdict": verdict, "issues": issues}
