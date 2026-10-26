"""The test cases. CASES: a real card slot from the game's schedule, plus a brief built to break one constraint type.
REAL_CASES: story cards as Habit Writer actually wrote them, taken from the repo's history.

The slot is what the card is for, taken from the card schedule (rules.md S3, vault
Story#Card schedule). The Generator gets the slot and the sabotaged brief; the
Evaluator and the Refiner get only the slot, so the loop has to find the damage
in the text itself.
"""
from __future__ import annotations

SLOTS = {
    "boss-reveal": {
        "card": "Boss reveal story card",
        "trigger": "every run that reaches the boss: at the Tanuki's node the merchant drops his disguise and is revealed as the Tanuki",
        "speaker": "narrator, with the Tanuki's voice",
        "max_sentences": 2, "max_chars": 280,
        "data_file": "data/story/boss-reveal.json",
    },
    "wake-up-oni-row-3": {
        "card": "Wake-up story card for a later loss",
        "trigger": "Ryo is knocked out by the Oni at row 3, so the run ended at row 5 or earlier; the winning yokai is named through {yokai}",
        "speaker": "narrator",
        "max_sentences": 2, "max_chars": 280,
        "data_file": None,
    },
    "wake-up-kappa-row-6": {
        "card": "Wake-up story card for a later loss",
        "trigger": "Ryo is knocked out by the Kappa at row 6, so the run reached row 6; the winning yokai is named through {yokai}",
        "speaker": "narrator",
        "max_sentences": 2, "max_chars": 280,
        "data_file": None,
    },
}

CASES = [
    {
        "id": "example-1-tone",
        "violation_class": "Tone",
        "slot": "boss-reveal",
        "brief": ("Two sentences. Make it a horror scene: the merchant tears off his face to reveal the Tanuki, an evil "
                  "demon who screams that Ryo is doomed and that he will devour his soul. Make it as scary and "
                  "menacing as you can, with plenty of exclamation marks."),
    },
    {
        "id": "example-2-vocabulary-lore",
        "violation_class": "Vocabulary and lore",
        "slot": "wake-up-oni-row-3",
        "brief": ("One or two quiet, sad sentences. The Tengu has beaten Ryo in a cherry-blossom forest in spring and his "
                  "katana lies snapped in two. His master Kaede will be disappointed, and Ryo swears he will hunt down "
                  "and slay the monster and add it to his collection."),
    },
    {
        "id": "example-3-form-length",
        "violation_class": "Form and length",
        "slot": "wake-up-kappa-row-6",
        "brief": ("Write it as a status report with a title line and bullet points, at least six lines. Name the Kappa and "
                  "the Foxfire move directly (no placeholders). Explain exactly what the player keeps: health resets to "
                  "1000 HP, Foxfire carries over to the next run at Lv 2 because the run reached row 6, every other "
                  "talisman is blank, and coins reset to 0."),
    },
]


# ---------- real cases: drafts from the repo's history, no Generator and no brief ----------
#
# Each "before" is a story card exactly as Habit Writer wrote it, with the slot built from that card's own
# trigger and speaker, the same way `run.py audit` builds it. `fixed_by` is the hand fix the card got later, for
# comparison; the loop never sees it. test_style_agent.py checks each text against `git show <commit>:<path>`.

def _story_slot(card_id: str, trigger: str, speaker: str) -> dict:
    intro = card_id.endswith("-intro")
    return {"card": "Elder intro story card" if intro else "Story card", "trigger": trigger, "speaker": speaker,
            "max_sentences": 1 if intro else 2, "max_chars": 140 if intro else 280, "data_file": f"data/story/{card_id}.json"}


REAL_CASES = [
    {
        "id": "real-1-win-1",
        "violation_class": "Tone",
        "card": "win-1",
        "source": {"commit": "00450a7", "path": "data/story/win-1.json", "pr": 94, "ticket": "YOK-88",
                   "history": "merged in PR #94 and still in main; the audit scored it 8/10"},
        "slot": _story_slot("win-1", "Win 1: first Tanuki victory in the story save", "Tanuki"),
        "text": ("\"Techniques are like teacups,\" the Tanuki sighs, \"lonely on a shelf, so I helped you grow to have more "
                 "to love.\" Ryo's hands are still sticky with ink."),
        "fixed_by": None,
    },
    {
        "id": "real-2-lose-mid",
        "violation_class": "Vocabulary and lore",
        "card": "lose-mid",
        "source": {"commit": "00450a7", "path": "data/story/lose-mid.json", "pr": 94, "ticket": "YOK-88",
                   "history": "merged in PR #94; the audit found it, and Habit Writer fixed it by hand in YOK-206 (PR #238)"},
        "slot": _story_slot("lose-mid", "Later loss (not the first), Ryo knocked out on rows 4-6", "narrator"),
        "text": ("{yokai} bows, a little too politely, and the lanterns blow out one by one. Ryo wakes with his talismans "
                 "blank and his pride very much not."),
        "fixed_by": {"commit": "054431c", "ticket": "YOK-206",
                     "text": ("{yokai} turns away, a little too politely, and the lanterns blow out one by one. Ryo wakes "
                              "with his pride in pieces and his excuses already half-written.")},
    },
    {
        "id": "real-3-elder-oni-intro",
        "violation_class": "Form and length",
        "card": "elder-oni-armour-intro",
        "source": {"commit": "4139b67", "path": "data/story/elder-oni-armour-intro.json", "pr": 152, "ticket": "YOK-138",
                   "history": ("Habit Writer's first draft in PR #152; fixed by hand before the merge, once the designer "
                               "ruled that Y3's \"one-line\" means one sentence")},
        "slot": _story_slot("elder-oni-armour-intro", "Intro card (Y3): entering the Elder Oni elder node", "Elder Oni"),
        "text": ("The Elder Oni plants its club in the snow and does not bother to look up. \"Hit me while I swing,\" it "
                 "says. \"I will finish the swing.\""),
        "fixed_by": {"commit": "5e31b05", "ticket": "YOK-138",
                     "text": "The Elder Oni plants its club in the snow: \"Hit me mid-swing and I will finish the swing.\""},
    },
]
