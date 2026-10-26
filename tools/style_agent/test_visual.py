"""Tests for the visual loop's code: pixel measurements, prompt wrapping, the image budget and the loop's stop rule.

    python -I -m unittest discover -s tools/style_agent -p 'test_*.py' -v
"""
from __future__ import annotations

import argparse
import json
import sys
import tempfile
import unittest
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
sys.path.insert(1, str(HERE.parent / "content_pipeline"))

try:
    from PIL import Image, ImageDraw, ImageOps
except ImportError:  # the story loop needs no Pillow; these tests do
    Image = None

if Image is not None:
    import visual_agents
    # No test may spend an Astra take: a miss on the recorded index raises instead of calling codex.
    visual_agents.Astra.allow_live = False


def _scene(path: Path, size=(1280, 720), colour=(0x2D, 0x3A, 0x5E)) -> Path:
    im = Image.new("RGB", size, colour)
    d = ImageDraw.Draw(im)
    w, h = size
    # A few flat palette shapes and an outline, all in the upper two thirds, so the lower third stays calm.
    d.rectangle((int(w * 0.6), int(h * 0.15), int(w * 0.7), int(h * 0.6)), fill=(0xB5, 0x33, 0x2B), outline=(0x1D, 0x1B, 0x21), width=6)
    d.ellipse((int(w * 0.1), int(h * 0.1), int(w * 0.3), int(h * 0.4)), fill=(0x34, 0x48, 0x3B), outline=(0x1D, 0x1B, 0x21), width=6)
    d.rectangle((0, int(h * 0.7), w, h), fill=(0xA9, 0xB4, 0xC8))
    im.save(path)
    return path


@unittest.skipIf(Image is None, "Pillow is not installed")
class Measure(unittest.TestCase):
    def setUp(self):
        self.dir = Path(tempfile.mkdtemp())

    def test_clean_16_9_scene_has_no_flags(self):
        from image_tools import measure_image
        m = measure_image(_scene(self.dir / "ok.png"))
        self.assertEqual(m["flags"], [])
        self.assertGreater(m["on_palette_share"], 0.95)

    def test_a_cluttered_floor_reads_busier_than_the_sky(self):
        from image_tools import measure_image
        path = _scene(self.dir / "busy.png")
        im = Image.open(path)
        d = ImageDraw.Draw(im)
        for x in range(0, 1280, 24):   # teacups and cushions all over the fighter band
            d.rectangle((x, 560, x + 12, 700), fill=(0xD8, 0x63, 0x2C), outline=(0x1D, 0x1B, 0x21), width=3)
        im.save(path)
        m = measure_image(path)
        self.assertGreater(m["lower_third_edge"], m["upper_edge"])

    def test_square_and_framed_are_flagged(self):
        from image_tools import measure_image
        square = measure_image(_scene(self.dir / "sq.png", size=(1400, 1400)))
        self.assertIn("aspect ratio", " ".join(square["flags"]))
        framed = self.dir / "framed.png"
        ImageOps.expand(Image.open(_scene(self.dir / "in.png", size=(1240, 680))), border=20, fill=(90, 60, 40)).save(framed)
        self.assertIn("flat band at the edges", " ".join(measure_image(framed)["flags"]))

    def test_neon_is_off_palette(self):
        from image_tools import measure_image
        m = measure_image(_scene(self.dir / "neon.png", colour=(0, 255, 255)))
        self.assertLess(m["on_palette_share"], 0.6)
        self.assertGreater(m["neon_share"], 0.3)

    def test_store_take_keeps_aspect_at_1280(self):
        from image_tools import store_take
        kept = store_take(_scene(self.dir / "big.png", size=(1672, 941)), self.dir / "kept" / "t.png")
        self.assertEqual(Image.open(kept).size, (1280, 720))


class Prompts(unittest.TestCase):
    def test_wrap_adds_house_blocks_and_refiner_never_sees_the_brief(self):
        if Image is None:
            self.skipTest("Pillow is not installed")
        from visual_agents import refiner_prompt, wrap_prompt
        from visual_cases import CASES, SLOTS
        from visual_guide import NEGATIVE_BLOCK, STYLE_BLOCK
        wrapped = wrap_prompt("A shrine gate at blue dusk.")
        self.assertTrue(wrapped.startswith(STYLE_BLOCK) and wrapped.endswith(NEGATIVE_BLOCK))
        case = CASES[0]
        m = {"width": 1, "height": 1, "aspect": 1, "border_px": {}, "dominant_colours": [], "on_palette_share": 0,
             "flatness": 0, "lower_third_edge": 0, "upper_edge": 0, "neon_share": 0, "pure_white_share": 0}
        self.assertNotIn(case["brief"], refiner_prompt(SLOTS[case["slot"]], "scene", 3, "[L3] x", m))

    def test_keep_rule_is_on_by_default_and_v1_is_unchanged(self):
        if Image is None:
            self.skipTest("Pillow is not installed")
        from visual_agents import KEEP_RULE, refiner_prompt
        from visual_cases import SLOTS
        m = {"width": 1, "height": 1, "aspect": 1, "border_px": {}, "dominant_colours": [], "on_palette_share": 0,
             "flatness": 0, "lower_third_edge": 0, "upper_edge": 0, "neon_share": 0, "pure_white_share": 0}
        slot = SLOTS["oni-shrine-gate"]
        self.assertIn(KEEP_RULE, refiner_prompt(slot, "s", 9, "r", m))
        v1 = refiner_prompt(slot, "s", 9, "r", m, keep=False)
        self.assertNotIn(KEEP_RULE, v1)
        # Byte-identical to the prompt runs/visual was recorded with, so that run still replays.
        self.assertIn("keep what the asset is for (the ASSET SLOT). Write the scene description only", v1)

    def test_guide_has_three_types_each_sourced(self):
        from visual_guide import CLASSES, PALETTE, RULES
        self.assertEqual(len(CLASSES), 3)
        for prefix in CLASSES:
            self.assertTrue(any(r["id"].startswith(prefix) for r in RULES))
        self.assertTrue(all(r["source"] for r in RULES))
        self.assertEqual(PALETTE["indigo"], "#2D3A5E")


@unittest.skipIf(Image is None, "Pillow is not installed")
class Budget(unittest.TestCase):
    def test_takes_already_in_the_folder_count(self):
        from visual_agents import Astra, BudgetSpent
        run = Path(tempfile.mkdtemp())
        (run / "images.json").write_text(json.dumps({"a": {"file": "a.png"}, "b": {"file": "b.png"}}), encoding="utf-8")
        with self.assertRaises(BudgetSpent):
            Astra(run, replay=False, budget=2).render("a new prompt", "x")

    def test_tests_cannot_call_astra(self):
        from visual_agents import Astra
        with self.assertRaises(RuntimeError):
            Astra(Path(tempfile.mkdtemp()), replay=False, budget=9).render("not recorded", "x")

    def test_replay_never_renders(self):
        from visual_agents import Astra
        with self.assertRaises(SystemExit):
            Astra(Path(tempfile.mkdtemp()), replay=True, budget=9).render("p", "x")

    def test_seeded_take_is_free_and_brings_its_evaluation(self):
        from visual_agents import Astra, BudgetSpent, seed_first_takes
        src, dest = Path(tempfile.mkdtemp()), Path(tempfile.mkdtemp())
        _scene((src / "images").mkdir() or src / "images" / "visual-1-look-take1.png")
        k1 = Astra.key("gpt-6-astra", "brief")
        (src / "images.json").write_text(json.dumps({
            k1: {"file": "visual-1-look-take1.png", "prompt": "brief"},
            "k2": {"file": "visual-1-look-take2.png", "prompt": "refined"}}), encoding="utf-8")
        (src / "llm-calls.json").write_text(json.dumps({
            "e1": {"role": "evaluator", "images": ["ref.png", "visual-1-look-take1.png"], "response": "SCORE: 1/10"},
            "e2": {"role": "evaluator", "images": ["ref.png", "visual-1-look-take2.png"], "response": "SCORE: 9/10"},
            "r1": {"role": "refiner", "response": "scene"}}), encoding="utf-8")
        self.assertEqual(seed_first_takes(src, dest, ["visual-1-look"]), 1)
        self.assertEqual(set(json.loads((dest / "images.json").read_text(encoding="utf-8"))), {k1})
        self.assertEqual(set(json.loads((dest / "llm-calls.json").read_text(encoding="utf-8"))), {"e1"})
        astra = Astra(dest, replay=False, budget=1)
        self.assertTrue(astra.render("brief", "visual-1-look-take1").exists())   # seeded: no new take
        (dest / "images.json").write_text(json.dumps({**astra.index, "k3": {"file": "t2.png"}}), encoding="utf-8")
        with self.assertRaises(BudgetSpent):   # one new take used the whole budget of 1; the seeded one didn't count
            Astra(dest, replay=False, budget=1).render("another", "x")


class FakeAstra:
    def __init__(self, files):
        self.files, self.prompts = list(files), []

    def render(self, prompt, name, settings=None):
        self.prompts.append(prompt)
        self.settings = settings
        return self.files.pop(0)


class FakeVision:
    def __init__(self, evals, refines):
        self.evals, self.refines, self.refiner_prompts = list(evals), list(refines), []

    def complete_with_images(self, role, model, system, user, images):
        return self.evals.pop(0)

    def complete(self, role, model, system, user):
        self.refiner_prompts.append(user)
        return self.refines.pop(0)


@unittest.skipIf(Image is None, "Pillow is not installed")
class Loop(unittest.TestCase):
    def setUp(self):
        d = Path(tempfile.mkdtemp())
        self.good = _scene(d / "good.png")
        self.square = _scene(d / "square.png", size=(1400, 1400))
        self.args = argparse.Namespace(evaluator_model="e", refiner_model="r", target=10, max_rounds=3)

    def test_refines_until_ten_and_wraps_the_refined_prompt(self):
        from visual_agents import STYLE_BLOCK
        from visual_cases import CASES
        from visual_loop import run_visual_case
        astra = FakeAstra([self.good, self.good])
        vision = FakeVision(["SCORE: 2/10\nREASON: [L3] photographic (-3)", "SCORE: 10/10\nREASON: clean"], ["A gate at dusk."])
        r = run_visual_case(vision, astra, self.good, self.args, CASES[0])
        self.assertEqual((r["before_score"], r["after_score"], r["passed"], len(r["rounds"])), (2, 10, True, 2))
        self.assertTrue(astra.prompts[1].startswith(STYLE_BLOCK))
        self.assertNotIn(STYLE_BLOCK, astra.prompts[0])   # the Generator's take uses the sabotaged brief alone
        self.assertIn("[L3] photographic (-3)", vision.refiner_prompts[0])

    def test_a_ten_on_a_square_image_is_not_accepted(self):
        from visual_cases import CASES
        from visual_loop import run_visual_case
        astra = FakeAstra([self.square, self.good])
        vision = FakeVision(["SCORE: 10/10\nREASON: fine", "SCORE: 10/10\nREASON: fine"], ["Wide 16:9 scene."])
        r = run_visual_case(vision, astra, self.good, self.args, CASES[2])
        self.assertEqual(len(r["rounds"]), 2)
        self.assertIn("Also measured by code: S1: aspect ratio", vision.refiner_prompts[0])

    def test_keeps_the_best_take_and_refines_from_it_after_a_regression(self):
        from visual_cases import CASES
        from visual_loop import run_visual_case
        args = argparse.Namespace(evaluator_model="e", refiner_model="r", target=10, max_rounds=4)
        vision = FakeVision(["SCORE: 1/10\nREASON: [L3] photo (-3)", "SCORE: 9/10\nREASON: [L4] busy (-1)",
                             "SCORE: 6/10\nREASON: [P2] wrong hour (-3)", "SCORE: 8/10\nREASON: x"],
                            ["scene two", "scene three", "scene four"])
        r = run_visual_case(vision, FakeAstra([self.good] * 4), self.good, args, CASES[0])
        self.assertEqual((r["after_score"], r["best_take"], r["passed"]), (9, 2, False))
        # Take 3 regressed, so take 4's rewrite starts again from take 2's prompt and reason.
        self.assertIn("scene two", vision.refiner_prompts[2])
        self.assertIn("[L4] busy (-1)", vision.refiner_prompts[2])
        # ...and is told what failed from there, so it can't come back as the same rewrite (runs/visual-real, Kappa take 3).
        self.assertIn("ALREADY SCORED LOWER\n<<<\nscene three\n>>>\nscored 6/10: [P2] wrong hour (-3)", vision.refiner_prompts[2])
        self.assertNotIn("ALREADY SCORED LOWER", vision.refiner_prompts[1])

    def test_a_refiner_prompt_with_no_failures_is_unchanged(self):
        from visual_agents import refiner_prompt
        from visual_cases import SLOTS
        m = {"width": 1, "height": 1, "aspect": 1, "border_px": {}, "dominant_colours": [], "on_palette_share": 0,
             "flatness": 0, "lower_third_edge": 0, "upper_edge": 0, "neon_share": 0, "pure_white_share": 0}
        slot = SLOTS["oni-shrine-gate"]
        self.assertEqual(refiner_prompt(slot, "s", 9, "r", m), refiner_prompt(slot, "s", 9, "r", m, failed=[]))

    def test_budget_spent_stops_the_case(self):
        from visual_agents import BudgetSpent
        from visual_cases import CASES
        from visual_loop import run_visual_case

        class Broke(FakeAstra):
            def render(self, prompt, name, settings=None):
                if self.prompts:
                    raise BudgetSpent("spent")
                return super().render(prompt, name, settings)
        r = run_visual_case(FakeVision(["SCORE: 3/10\nREASON: x"], ["s"]), Broke([self.good]), self.good, self.args, CASES[1])
        self.assertEqual((len(r["rounds"]), r["passed"], r["stopped"]), (1, False, "spent"))

    def test_a_real_spec_prompt_is_wrapped_from_take_one_and_keeps_its_settings(self):
        from visual_agents import wrap_prompt
        from visual_cases import REAL_CASES
        from visual_loop import classes_found, run_visual_case
        case = REAL_CASES[0]
        astra = FakeAstra([self.good, self.good])
        vision = FakeVision(["SCORE: 7/10\nREASON: [L4] heavy outline (-1)\n[S4] busy floor (-1)", "SCORE: 10/10\nREASON: ok"],
                            ["A gate at dusk."])
        r = run_visual_case(vision, astra, self.good, self.args, case)
        self.assertEqual(astra.prompts[0], wrap_prompt(case["scene"]))
        self.assertEqual(astra.settings, "aspect_ratio: 16:9")
        self.assertIn(case["scene"], vision.refiner_prompts[0])   # the Refiner rewrites the spec's scene, not the wrapped prompt
        self.assertEqual(classes_found(r["rounds"][0]["reason"]), "Look, Spec form")


class RealCases(unittest.TestCase):
    def test_each_scene_is_its_specs_backdrop_prompt_word_for_word(self):
        from visual_cases import REAL_CASES
        root = HERE.parent.parent
        for case in REAL_CASES:
            spec = (root / case["spec"]).read_text(encoding="utf-8")
            self.assertIn(f"> {case['scene']} Avoid: [negative prompt].", spec, case["id"])
            self.assertIn(f"`{case['settings']}`", spec, case["id"])

    def test_settings_leave_old_keys_alone(self):
        if Image is None:
            self.skipTest("Pillow is not installed")
        from visual_agents import Astra
        self.assertEqual(Astra.key("m", "p"), Astra.key("m", "p", None))
        self.assertNotEqual(Astra.key("m", "p"), Astra.key("m", "p", "aspect_ratio: 16:9"))


if __name__ == "__main__":
    unittest.main()
