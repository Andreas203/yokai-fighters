"""The three test cases: a real card slot from the game's schedule, plus a brief built to break one constraint type.

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
