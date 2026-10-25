"""The Yokai Fighters style guide for story-card text, as data.

Every rule is taken from the design, never invented here: each one names the
rules.md ID, GDD section or vault note it comes from. The Evaluator and the
Refiner both read render_guide(); the code measurements in agents.py use the
word lists below. Three constraint types: tone, vocabulary and lore, form and length.
"""
from __future__ import annotations

CLASSES = {
    "T": "Tone",
    "V": "Vocabulary and lore",
    "F": "Form and length",
}

RULES = [
    # ---------- Tone ----------
    {"id": "T1", "rule": "Mischievous folklore with a melancholy edge, never horror. Nothing gory, cruel, demonic or frightening: "
                         "no blood, screams, corpses, souls devoured, darkness swallowing anything.",
     "source": "rules.md S1; GDD §2.1; vault Tone#The tone line"},
    {"id": "T2", "rule": "Yokai are tricksters and lonely spirits, not monsters. A yokai that wins walks away or goes back to what "
                         "it was doing; it never hurts Ryo for pleasure and never gloats over him.",
     "source": "rules.md S1; vault Tone#The tone line, Story#Wake-up cards"},
    {"id": "T3", "rule": "Melancholy is small and specific: something owed, borrowed, waiting or left behind. Never doom, despair "
                         "or the end of the world.",
     "source": "vault Tone#Do and don't examples"},
    {"id": "T4", "rule": "The merchant and the Tanuki share one voice: cheerful, generous and quietly hungry. It compliments what it "
                         "wants, teases Ryo as \"little exorcist\" and never shouts, threatens or sounds evil. The unease comes "
                         "from how pleasant it is.",
     "source": "vault Tone#Voice: the merchant and the Tanuki; Characters#The merchant and the Tanuki"},
    {"id": "T5", "rule": "Wit is dry and in period. No modern slang, memes, fourth-wall jokes, exclamation-mark shouting or "
                         "capitals for emphasis.",
     "source": "vault Tone#Do and don't examples (\"LOL, nice talismans, bro\"; \"MINE now, fool!\")"},
    {"id": "T6", "rule": "Ryo is earnest and stubborn. He apologises to the yokai he binds; he never boasts, threatens, vows revenge "
                         "or jokes at a yokai's expense.",
     "source": "vault Tone#Voice: Ryo; GDD §2.2"},

    # ---------- Vocabulary and lore ----------
    {"id": "V1", "rule": "Ryo is an apprentice exorcist who binds yokai: an ink stroke seals the yokai into a paper talisman. He never "
                         "kills, slays, destroys, defeats-and-captures or hunts them, and he does not \"collect\" them: collecting "
                         "is the Tanuki's word. Ryo fights with talismans, never a sword or a gun.",
     "source": "GDD §2.2; vault Story#Talismans, Tone#Do and don't examples, Characters#Ryo"},
    {"id": "V2", "rule": "Never call a yokai a monster, demon, beast or creature of evil. \"Oni\" is a yokai's name, not an insult.",
     "source": "rules.md S1; vault Characters#The yokai as a group, Tone#Voice: Ryo"},
    {"id": "V3", "rule": "Only the 5-week build's cast exists: Ryo, Master Sōen (she), the merchant who is the Tanuki, Kitsune, Oni, "
                         "Kappa, and the elders Nine-Tailed Kitsune, Elder Oni and Elder Kappa. Never the stretch-goal names "
                         "Jorōgumo, Tengu, Tetsu, Kaede or Yūrei, and never supers, Acts 2 or 3 or the Yūrei gauntlet.",
     "source": "rules.md G1-G5; vault Characters#Not in the 5-week build"},
    {"id": "V4", "rule": "The world is one rural Japanese province at the end of autumn, folded into the Tanuki's illusion: dusk, "
                         "lantern light, things about to end. Each fighter has a home: Kitsune the bamboo grove at dusk, Oni the "
                         "snowbound shrine gate, Kappa the lantern riverbank, the Tanuki a tea house whose paper screens flicker "
                         "between seasons. No spring blossom, castles, cities, neon or night-time dread.",
     "source": "rules.md V9; GDD §2.1; vault Tone#The tone line, Story#The illusion loop"},
    {"id": "V5", "rule": "Each yokai keeps its folklore and voice: the Kitsune (she) teases and slips away, white fox mask with red "
                         "markings, tails, foxfire; the Oni (it) is proud and blunt with an iron club; the Kappa (it) is formal, "
                         "stubborn and polite, bows and carries water in the dish on its head.",
     "source": "vault Characters#Kitsune, #Oni, #Kappa; Tone#Voice: yokai"},
    {"id": "V6", "rule": "Fixed lines are quoted word for word when a card uses them: \"Forgive me, {yokai}. I'll set your spirit "
                         "free.\" (every binding), \"That {move} of yours... I'd love one.\" (row 4), \"I said I'd set you free.\" "
                         "(ending), and Master Sōen's rule \"name the debt\".",
     "source": "rules.md S3; AM2; vault Tone#Voice: Ryo"},
    {"id": "V7", "rule": "Story facts hold: after a loss Ryo wakes at the province's edge and his talismans are blank, except that a "
                         "run that reached row 6 or further leaves one talisman faintly inked. The merchant is revealed as the "
                         "Tanuki only at the boss node.",
     "source": "rules.md M3, S3; vault Story#Talismans, Story#Wake-up cards"},

    # ---------- Form and length ----------
    {"id": "F1", "rule": "A story card is one or two sentences and at most 280 characters. An elder intro card is exactly one "
                         "sentence of at most 140 characters.",
     "source": "rules.md S2, Y3; vault Tone#Card form, Story#Elder intro cards"},
    {"id": "F2", "rule": "Present tense, plain words, one concrete image from the game's world: a lantern, a paper screen, a tail, "
                         "a teacup, an ink stroke, a red seal, snow on a shrine gate.",
     "source": "vault Tone#Card form"},
    {"id": "F3", "rule": "Names that depend on the player's run come through placeholders: {yokai} for the yokai and {move} for a "
                         "move. A card never hard-codes a move the player may not own. No other placeholders.",
     "source": "rules.md S4; vault Tone#Card form"},
    {"id": "F4", "rule": "No system words. A card does not say health, HP, damage, frames, level, Lv, meter, XP, unlock or "
                         "roguelite, and shows no game numbers; it says what a person standing there would see.",
     "source": "vault Tone#Card form; rules.md A12 (numbers live behind the frame-data toggle)"},
    {"id": "F5", "rule": "Plain card text only, as it appears in a story card's \"text\" field: no title, heading, bullet points, "
                         "Markdown, emoji, speaker label or stage directions.",
     "source": "data/story/*.json (story_card text field); vault Story#Card schedule"},
]

# Words the code counts before the Evaluator reads a draft. The Evaluator decides; the counts stop it miscounting.
SYSTEM_WORDS = ["health", "hp", "damage", "frames", "frame", "level", "lv", "meter", "xp", "unlock", "roguelite"]
OUT_OF_BUILD = ["jorogumo", "tengu", "tetsu", "kaede", "yurei"]
HORROR_WORDS = ["blood", "gore", "corpse", "scream", "demon", "devour", "flesh", "slaughter", "kill", "slay",
                "terror", "nightmare", "hell", "doom", "soul"]
PLACEHOLDERS = {"yokai", "move"}

RUBRIC = (
    "Start at 10 and deduct for every violation. A hard break costs 3 points: horror, a yokai called a monster or demon, "
    "Ryo killing or collecting, an out-of-build name, a fixed line misquoted, a card over its sentence or character limit, "
    "a hard-coded move, a system word or game number, Markdown or a title. A soft drift costs 1 point: an image from "
    "outside the game's world, a voice slightly off, a tense slip, two images where one would do. The score never goes "
    "below 1. Give 10 only when no rule is broken."
)


def render_guide() -> str:
    """The style guide as the Evaluator and the Refiner read it."""
    lines = ["YOKAI FIGHTERS STYLE GUIDE: STORY-CARD TEXT", ""]
    for prefix, title in CLASSES.items():
        lines.append(f"{title.upper()}")
        for r in RULES:
            if r["id"].startswith(prefix):
                lines.append(f"{r['id']}. {r['rule']} (Source: {r['source']})")
        lines.append("")
    lines += ["CARDS THAT SCORE 10 (from the game's design, for voice only; do not copy them)",
              "- \"{yokai} slips off between the lanterns, tails swaying, and does not look back. Somewhere a bound "
              "thing is still waiting for someone to return it.\"",
              "- \"That {move} of yours... I'd love one.\"",
              "- \"Which of us is the collector, little exorcist?\"", ""]
    return "\n".join(lines)
