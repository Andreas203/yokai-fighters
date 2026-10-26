"""Tests for the style loop's code: score parsing, measurements, card cleaning and the loop's stop rule.

    python -I -m unittest discover -s tools/style_agent -p 'test_*.py' -v
"""
from __future__ import annotations

import argparse
import json
import subprocess
import sys
import unittest
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
sys.path.insert(1, str(HERE.parent / "content_pipeline"))

import run  # noqa: E402
from agents import (clean_card, count_sentences, evaluator_prompt, measure, parse_evaluation,  # noqa: E402
                    refiner_prompt, split_sentences)
from cases import CASES, REAL_CASES, SLOTS  # noqa: E402
from style_guide import CLASSES, RULES, render_guide  # noqa: E402

SLOT = SLOTS["wake-up-kappa-row-6"]


class ParseEvaluation(unittest.TestCase):
    def test_reads_score_and_reason(self):
        ev = parse_evaluation('SCORE: 4/10\nREASON: [T1] "devour his soul": horror (-3)\n[T5] "!!": shouting (-3)')
        self.assertEqual(ev["score"], 4)
        self.assertIn("[T5]", ev["reason"])
        self.assertTrue(ev["well_formed"])

    def test_bold_and_decimal_scores(self):
        self.assertEqual(parse_evaluation("**SCORE:** 9.6/10\nREASON: fine")["score"], 10)
        self.assertEqual(parse_evaluation("SCORE: 0/10\nREASON: x")["score"], 1)

    def test_no_score_is_a_failed_evaluation(self):
        ev = parse_evaluation("Looks great to me!")
        self.assertEqual(ev["score"], 1)
        self.assertFalse(ev["well_formed"])


class Measure(unittest.TestCase):
    def test_clean_card_has_no_flags(self):
        text = "{yokai} bows, spilling a little water, and wades back to the lantern riverbank. One talisman keeps a faint stroke of ink."
        self.assertEqual(measure(text, SLOT)["flags"], [])

    def test_report_breaks_every_form_limit(self):
        text = "# Run Report\n- The Kappa wins\n- Health resets to 1000 HP\n- Foxfire carries at Lv 2\n- The Tengu waits"
        flags = " ".join(measure(text, SLOT)["flags"])
        for rule in ("F4", "F5", "V3"):
            self.assertIn(rule, flags)

    def test_length_and_sentence_limits(self):
        flags = " ".join(measure("Snow falls. " * 30, SLOT)["flags"])
        self.assertIn("characters, over the limit", flags)
        self.assertIn("sentences, over the limit", flags)

    def test_unknown_placeholder(self):
        self.assertIn("F3", " ".join(measure("{row} is where {yokai} leaves.", SLOT)["flags"]))

    def test_accented_names_are_caught(self):
        self.assertEqual(measure("The Jorōgumo spins by the Yūrei.", SLOT)["out_of_build_names"], ["jorogumo", "yurei"])

    def test_ellipsis_is_not_a_sentence_break(self):
        self.assertEqual(count_sentences("That {move} of yours... I'd love one."), 1)

    def test_sentences_inside_quoted_speech_count(self):
        # Demo run 1 stalled here: the Refiner kept a two-sentence speech and never saw it as two sentences.
        text = ('The merchant\'s smile slips away, and the Tanuki sits in his place, pouring tea. "What lovely '
                'talismans, little exorcist. Do sit down; I have been so looking forward to adding them to my shelf."')
        self.assertEqual(split_sentences(text), [
            "The merchant's smile slips away, and the Tanuki sits in his place, pouring tea.",
            '"What lovely talismans, little exorcist.',
            'Do sit down; I have been so looking forward to adding them to my shelf."'])
        self.assertEqual(count_sentences('"Ah, you found me," says the Tanuki, and the screens unfold.'), 1)

    def test_speech_tag_after_a_quoted_question_is_the_same_sentence(self):
        text = 'The screens flicker. "Which of us is the collector, little exorcist?" it asks, pleasantly.'
        self.assertEqual(count_sentences(text), 2)
        self.assertEqual(count_sentences("{yokai} bows. {yokai} leaves."), 2)


class CleanCard(unittest.TestCase):
    def test_strips_fence_and_wrapping_quotes(self):
        self.assertEqual(clean_card('```\n"The lanterns gutter."\n```'), "The lanterns gutter.")

    def test_keeps_quoted_speech_inside(self):
        text = '"Ah, you found me," says the Tanuki.'
        self.assertEqual(clean_card(text), text)


class Guide(unittest.TestCase):
    def test_three_constraint_types_each_with_sources(self):
        self.assertEqual(len(CLASSES), 3)
        for prefix in CLASSES:
            self.assertTrue(any(r["id"].startswith(prefix) for r in RULES))
        for r in RULES:
            self.assertTrue(r["source"], r["id"])

    def test_evaluator_and_refiner_read_the_guide_but_never_the_brief(self):
        case = CASES[0]
        slot = SLOTS[case["slot"]]
        m = measure("x", slot)
        for prompt in (evaluator_prompt(slot, "x", m), refiner_prompt(slot, "x", 3, "[T1] bad", m)):
            self.assertIn(render_guide(), prompt)
            self.assertNotIn(case["brief"], prompt)


class FakeLLM:
    """Scripted replies by role, so the loop's control flow is tested without a model."""

    def __init__(self, replies):
        self.replies = {role: list(r) for role, r in replies.items()}
        self.prompts = []

    def complete(self, role, model, system, user):
        self.prompts.append((role, user))
        return self.replies[role].pop(0)


ARGS = argparse.Namespace(generator_model="g", evaluator_model="e", refiner_model="r", target=10, max_rounds=4)
CASE = {"id": "t", "violation_class": "Tone", "slot": "wake-up-kappa-row-6", "brief": "b"}
GOOD = "{yokai} bows and wades back to the lantern riverbank. One talisman keeps a faint stroke of ink."


class Loop(unittest.TestCase):
    def test_refines_until_ten(self):
        llm = FakeLLM({"generator": ["The Kappa crushes Ryo!"],
                       "evaluator": ["SCORE: 3/10\nREASON: [T2] crushes (-3)", "SCORE: 10/10\nREASON: clean"],
                       "refiner": [GOOD]})
        r = run.run_case(llm, ARGS, CASE)
        self.assertEqual((r["before_score"], r["after_score"], r["passed"], len(r["rounds"])), (3, 10, True, 2))
        refiner_prompt_sent = [u for role, u in llm.prompts if role == "refiner"][0]
        self.assertIn("[T2] crushes (-3)", refiner_prompt_sent)

    def test_a_ten_with_a_code_flag_is_not_accepted(self):
        llm = FakeLLM({"generator": ["# Report\n- Kappa wins"],
                       "evaluator": ["SCORE: 10/10\nREASON: fine", "SCORE: 10/10\nREASON: fine"],
                       "refiner": [GOOD]})
        r = run.run_case(llm, ARGS, CASE)
        self.assertEqual(len(r["rounds"]), 2)
        refiner_prompt_sent = [u for role, u in llm.prompts if role == "refiner"][0]
        self.assertIn("Also counted by code: F5", refiner_prompt_sent)

    def test_stops_at_max_rounds(self):
        llm = FakeLLM({"generator": ["bad"], "evaluator": ["SCORE: 2/10\nREASON: x"] * 4, "refiner": ["bad"] * 3})
        r = run.run_case(llm, ARGS, CASE)
        self.assertEqual(len(r["rounds"]), 4)
        self.assertFalse(r["passed"])

    def test_a_real_draft_skips_the_generator(self):
        case = REAL_CASES[1]
        llm = FakeLLM({"evaluator": ["SCORE: 8/10\nREASON: [V7] blank (-1)", "SCORE: 10/10\nREASON: clean"],
                       "refiner": [GOOD]})
        r = run.run_case(llm, ARGS, case)
        self.assertNotIn("generator", [role for role, _ in llm.prompts])
        self.assertEqual((r["before"], r["after"], r["passed"]), (case["text"], GOOD, True))


class RealCases(unittest.TestCase):
    def test_each_draft_is_the_text_at_its_commit(self):
        for case in REAL_CASES:
            src = case["source"]
            proc = subprocess.run(["git", "show", f"{src['commit']}:{src['path']}"], cwd=run.ROOT, capture_output=True,
                                  text=True, encoding="utf-8")
            if proc.returncode:
                self.skipTest(f"git history not available here: {proc.stderr.strip()[:200]}")
            card = json.loads(proc.stdout)
            self.assertEqual(card["text"], case["text"], case["id"])
            self.assertEqual((card["trigger"], card["speaker"]), (case["slot"]["trigger"], case["slot"]["speaker"]), case["id"])

    def test_slots_are_built_the_way_the_audit_builds_them(self):
        audit = {c["id"]: c["slot"] for c in run.audit_cards()}
        win_1 = REAL_CASES[0]
        self.assertEqual(win_1["slot"], audit[win_1["card"]])   # still in main unchanged, so the slots must agree
        self.assertEqual(REAL_CASES[2]["slot"]["max_sentences"], 1)

    def test_the_hand_fix_never_reaches_the_loop(self):
        for case in REAL_CASES:
            if not case["fixed_by"]:
                continue
            m = measure(case["text"], case["slot"])
            for prompt in (evaluator_prompt(case["slot"], case["text"], m),
                           refiner_prompt(case["slot"], case["text"], 3, "[V7] x", m)):
                self.assertNotIn(case["fixed_by"]["text"], prompt)


if __name__ == "__main__":
    unittest.main()
