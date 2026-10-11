"""Tests for tools/validate_data.py. Run: python3 -m unittest discover -s tools -p 'test_*.py'"""
import io
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import validate_data  # noqa: E402

ROOT = Path(__file__).resolve().parent.parent
SAMPLES = ROOT / "data" / "samples"
FIXTURE_NORMALS = ROOT / "game" / "tests" / "fixtures" / "ryo-normals"
FIXTURE_THROWS = ROOT / "game" / "tests" / "fixtures" / "throws"
FIXTURE_AIR = ROOT / "game" / "tests" / "fixtures" / "ryo-air-normals"


def run(*paths):
    out = io.StringIO()
    code = validate_data.validate([str(p) for p in paths], out=out)
    return code, out.getvalue()


class ValidateDataTest(unittest.TestCase):
    def test_default_run_over_data_passes(self):
        code, out = run()
        self.assertEqual(code, 0, out)
        self.assertNotIn("invalid/", out)

    def test_each_valid_sample_type_present_and_passes(self):
        code, out = run(SAMPLES)
        self.assertEqual(code, 0, out)
        self.assertIn("8 valid, 0 invalid", out)

    def test_malformed_special_fails_with_clear_messages(self):
        code, out = run(SAMPLES / "ryo-spirit-wave.json", SAMPLES / "invalid" / "broken-wave.json")
        self.assertEqual(code, 1)
        prefix = "data/samples/invalid/broken-wave.json: "
        self.assertIn(prefix + "input: 'kihon' is a required property", out)
        self.assertIn(prefix + "levels.1.frame_data.damage: 'sixty' is not of type 'integer'", out)
        self.assertIn(prefix + "rules.1: rule ID 'Z9' does not exist in docs/design/rules.md", out)
        self.assertIn(prefix + "levels.1.frame_data.hitboxes.0.frames: hitbox frames 20-22 fall outside 14-15", out)
        self.assertIn("1 valid, 1 invalid", out)

    def test_profile_reading_buttons_fails(self):
        code, out = run(SAMPLES / "invalid" / "oni-reads-buttons.json")
        self.assertEqual(code, 1)
        self.assertIn("behaviours.0.when: 'opponent_pressed_punch' is not one of", out)
        self.assertIn("temperament: 'boss' temperament is for the Tanuki only", out)

    def test_malformed_normal_fails_with_clear_messages(self):
        code, out = run(SAMPLES / "sample-light-punch-clip.json", SAMPLES / "invalid" / "broken-normal.json")
        self.assertEqual(code, 1)
        prefix = "data/samples/invalid/broken-normal.json: "
        self.assertIn(prefix + "input.button: 'XP' is not one of", out)
        self.assertIn(prefix + "input.directions.0: 0 is less than the minimum of 1", out)
        self.assertIn(prefix + "frame_data.strength: 'huge' is not one of", out)  # V2 (YOK-20)
        self.assertIn(prefix + "frame_data.hitboxes.0.frames: hitbox frames 8-9 fall outside 5-6", out)
        self.assertIn(prefix + "frame_data: startup+active+recovery = 14 but clip 'sample-light-punch-clip' has frames_total 13 (F2)", out)

    def test_normal_kind_allowed_in_moves_folder(self):
        self.assertIn("normal", validate_data.DIR_KIND["moves"])
        self.assertIn("special", validate_data.DIR_KIND["moves"])

    def test_ryo_normal_test_fixtures_match_schema_except_clip(self):
        # YOK-18 TEST FIXTURES: C8 numbers without a clip yet (F2). Everything but the clip must be valid,
        # so the C# loader reads the same format movesmith will write.
        code, out = run(FIXTURE_NORMALS)
        lines = [l for l in out.splitlines() if not l.startswith("validate_data:")]
        self.assertEqual(len(lines), 6, out)
        for line in lines:
            self.assertTrue(line.endswith("(root): 'clip' is a required property"), line)

    def test_throw_test_fixture_matches_schema_except_clip(self):
        # YOK-19 TEST FIXTURE: C4 numbers, grab clip still being retaken (YOK-31).
        self.assertIn("throw", validate_data.DIR_KIND["moves"])
        code, out = run(FIXTURE_THROWS)
        lines = [l for l in out.splitlines() if not l.startswith("validate_data:")]
        self.assertEqual(len(lines), 1, out)
        self.assertTrue(lines[0].endswith("(root): 'clip' is a required property"), lines[0])

    def test_air_normal_fixtures_match_schema_except_clip(self):
        # YOK-55 TEST FIXTURES (E11): jump-in normals with the air flag and landing recovery, no clip yet (F2).
        code, out = run(FIXTURE_AIR)
        lines = [l for l in out.splitlines() if not l.startswith("validate_data:")]
        self.assertEqual(len(lines), 2, out)
        for line in lines:
            self.assertTrue(line.endswith("(root): 'clip' is a required property"), line)

    def test_landing_recovery_needs_air(self):
        import json
        path = FIXTURE_AIR / "test-ryo-air-punch.json"
        doc = json.loads(path.read_text(encoding="utf-8"))
        v = validate_data.load_validators()["normal"]
        doc["air"] = "yes"
        self.assertTrue(any("is not of type 'boolean'" in e.message for e in v.iter_errors(doc)))
        del doc["air"]
        self.assertTrue(any("'air' is a dependency of 'landing_recovery'" in e.message for e in v.iter_errors(doc)))
        doc["air"] = False
        errs = validate_data.semantic(doc, path, validate_data.load_rule_ids(), {})
        self.assertIn(("landing_recovery", 'landing_recovery is only for air normals (set "air": true) (E11)'), errs)
        doc["air"] = True
        self.assertEqual([], validate_data.semantic(doc, path, validate_data.load_rule_ids(), {}))

    def test_air_normal_buttons(self):
        # E19: an air normal lists every punch (or kick); exactly one of button / buttons; ground normals use one button.
        import json
        path = FIXTURE_AIR / "test-ryo-air-punch.json"
        doc = json.loads(path.read_text(encoding="utf-8"))
        self.assertEqual(["LP", "MP", "HP"], doc["input"]["buttons"])
        kick = json.loads((FIXTURE_AIR / "test-ryo-air-kick.json").read_text(encoding="utf-8"))
        self.assertEqual(["LK", "MK", "HK"], kick["input"]["buttons"])
        v = validate_data.load_validators()["normal"]
        self.assertEqual([], [e.message for e in v.iter_errors(doc) if "clip" not in e.message])
        doc["input"]["button"] = "HP"
        self.assertTrue(any(e.validator == "oneOf" for e in v.iter_errors(doc)))
        del doc["input"]["button"], doc["input"]["buttons"]
        self.assertTrue(any(e.validator == "oneOf" for e in v.iter_errors(doc)))
        for bad in (["LP", "LP"], [], ["XP"]):
            doc["input"]["buttons"] = bad
            self.assertTrue(list(v.iter_errors(doc)), bad)
        doc["input"]["buttons"] = ["LP", "MP", "HP"]
        del doc["air"], doc["landing_recovery"]
        errs = validate_data.semantic(doc, path, validate_data.load_rule_ids(), {})
        self.assertIn(("input.buttons", 'input.buttons is only for air normals (set "air": true); ground normals use input.button (E19)'), errs)

    def test_throw_semantic_checks(self):
        import json
        doc = json.loads((FIXTURE_THROWS / "test-ryo-throw.json").read_text(encoding="utf-8"))
        doc["frame_data"]["throwboxes"][0]["frames"] = [4, 9]
        doc["input"]["buttons"] = ["LP", "LP"]
        errs = validate_data.semantic(doc, FIXTURE_THROWS / "test-ryo-throw.json", validate_data.load_rule_ids(), {})
        self.assertIn(("frame_data.throwboxes.0.frames", "throwbox frames 4-9 fall outside 6-7"), errs)
        v = validate_data.load_validators()["throw"]
        self.assertTrue(any("non-unique" in e.message for e in v.iter_errors(doc)))

    def test_rule_id_pattern_accepts_engine_defaults(self):
        import json, re as _re
        common = json.loads((validate_data.SCHEMA_DIR / "common.schema.json").read_text(encoding="utf-8"))
        pattern = common["$defs"]["ruleIds"]["items"]["pattern"]
        for rid in ("C4", "E4", "E12", "T1", "G3"):
            self.assertRegex(rid, pattern)
        for rid in ("Z9", "E", "e4", "4E"):
            self.assertIsNone(_re.match(pattern, rid), rid)
        ids = validate_data.load_rule_ids()
        for rid in ("E4", "E12", "E13", "E14", "E15"):
            self.assertIn(rid, ids)

    def test_clip_structured_fields(self):
        """YOK-53: trim_start/trim_end/speed_scale/file/yaw_offset are schema-checked and cross-checked."""
        import json
        path = ROOT / "data" / "clips" / "ryo-get-up.json"
        good = json.loads(path.read_text(encoding="utf-8"))
        self.assertEqual((good["trim_start"], good["trim_end"], good["speed_scale"]), (22, 102, 2.5))
        v = validate_data.load_validators()["clip"]
        ids = validate_data.load_rule_ids()
        self.assertEqual([], [e.message for e in v.iter_errors(good)])
        self.assertEqual([], validate_data.semantic(good, path, ids, {}))
        bad = dict(good, trim_start=-1, speed_scale=0, file="clips/x.fbx", yaw_offset=270)
        msgs = " | ".join(e.message for e in v.iter_errors(bad))
        for m in ("-1 is less than the minimum of 0", "0 is less than or equal to the minimum of 0", "does not match", "270 is greater than the maximum of 180"):
            self.assertIn(m, msgs)
        def sem(**kw):
            d = dict(good, **kw)
            for k in [k for k, x in kw.items() if x is None]:
                d.pop(k)
            return validate_data.semantic(d, path, ids, {})
        self.assertIn(("trim_end", "trim_start and trim_end must both be set or both be absent"), sem(trim_end=None))
        self.assertIn(("trim_end", "need trim_start <= trim_end, got 102-22"), sem(trim_start=102, trim_end=22))
        self.assertIn(("frames_total", "trim 22-102 at 1x gives 81 frames but frames_total is 33 (F2)"), sem(speed_scale=1))
        self.assertIn(("file", "game/assets/generated/clips/ryo/nope.glb does not exist"), sem(file="assets/generated/clips/ryo/nope.glb"))
        bare = {k: x for k, x in good.items() if k not in ("trim_start", "trim_end", "speed_scale", "file")}
        self.assertEqual([], [e.message for e in v.iter_errors(bare)], "the fields are optional")

    def test_story_cards_validate(self):
        code, out = run(ROOT / "data" / "story", SAMPLES / "sample-story-card.json")
        self.assertEqual(code, 0, out)
        n = len(list((ROOT / "data" / "story").glob("*.json"))) + 1  # every story card + the sample
        self.assertIn(f"{n} file(s), {n} valid, 0 invalid", out)

    def test_malformed_story_card_fails_with_clear_messages(self):
        code, out = run(SAMPLES / "invalid" / "broken-story-card.json")
        self.assertEqual(code, 1)
        prefix = "data/samples/invalid/broken-story-card.json: "
        self.assertIn(prefix + "(root): 'speaker' is a required property", out)
        self.assertIn(prefix + "(root): Additional properties are not allowed ('mood' was unexpected)", out)
        self.assertIn(prefix + "trigger: '' should be non-empty", out)
        self.assertIn(prefix + "placeholders: {item} is used but not listed in placeholders (S4)", out)
        self.assertIn(prefix + "placeholders: 'move' is listed but {move} is not used in title or text", out)

    def test_story_card_kind_only_in_story_folder(self):
        import json, tempfile
        card = json.loads((ROOT / "data" / "story" / "binding-line.json").read_text(encoding="utf-8"))
        with tempfile.TemporaryDirectory() as tmp:
            bad = Path(tmp) / "data" / "moves" / "binding-line.json"
            bad.parent.mkdir(parents=True)
            bad.write_text(json.dumps(card), encoding="utf-8")
            code, out = run(bad)
        self.assertEqual(code, 1)
        self.assertIn("files in data/moves/ must be kind", out)

    def _map(self, edit):
        """The default map with one edit, validated from a temp data/map/ folder."""
        import json, tempfile
        doc = json.loads((ROOT / "data" / "map" / "default-map.json").read_text(encoding="utf-8"))
        edit(doc)
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "data" / "map" / "default-map.json"
            path.parent.mkdir(parents=True)
            path.write_text(json.dumps(doc), encoding="utf-8")
            return run(path)

    def test_default_run_map_validates(self):
        code, out = run(ROOT / "data" / "map")
        self.assertEqual(code, 0, out)
        self.assertEqual(self._map(lambda d: None)[0], 0)

    def test_run_map_graph_checks(self):
        def node(d, i):
            return next(n for n in d["nodes"] if n["id"] == i)
        cases = [
            (lambda d: d["routes"].append({"from": "r2-oni", "to": "r9-nowhere"}), "routes: route r2-oni -> r9-nowhere: unknown node 'r9-nowhere'"),
            (lambda d: d["routes"].append({"from": "r3-oni", "to": "r2-oni"}), "goes backwards (row 3 to row 2)"),
            (lambda d: d["routes"].append({"from": "r2-oni", "to": "r2-kappa"}), "goes sideways"),
            (lambda d: d["routes"].append({"from": "r2-oni", "to": "r4-merchant"}), "skips a row (row 2 to row 4)"),
            (lambda d: d["routes"].append(dict(d["routes"][0])), "is listed twice"),
            (lambda d: d["nodes"].append(dict(node(d, "r2-oni"))), "node id 'r2-oni' is defined twice"),
            (lambda d: d.update(routes=[r for r in d["routes"] if r["to"] != "r2-kappa"]), "node 'r2-kappa' is unreachable"),
            (lambda d: d.update(routes=[r for r in d["routes"] if r["from"] != "r2-kitsune"]), "node 'r2-kitsune' is a dead end"),
            (lambda d: node(d, "r4-merchant").update(type="duel", yokai="oni"), "R1: node 'r4-merchant': row 4 must be merchant, got 'duel'"),
            (lambda d: node(d, "r5-elder-oni").update(type="duel", elder=False), "R1: node 'r5-elder-oni': row 5 must be elder, got 'duel'"),
            (lambda d: node(d, "r1-apprentice").update(yokai="kitsune"), "R1: node 'r1-apprentice': row 1 is the apprentice"),
            (lambda d: node(d, "boss-tanuki").update(yokai="oni"), "R1: row 9 must be exactly one boss node with yokai 'tanuki'"),
            (lambda d: d.update(nodes=[]), "nodes: [] should be non-empty"),
            (lambda d: d.update(schema_version=2), "schema_version: 1 was expected"),
            (lambda d: node(d, "r2-oni").update(colour="red"), "Additional properties are not allowed"),
        ]
        for edit, expect in cases:
            code, out = self._map(edit)
            self.assertEqual(code, 1, expect)
            self.assertIn(expect, out)

    def test_run_map_without_r1_is_only_a_graph(self):
        def other_layout(d):
            d["rules"] = ["R2"]
            for n in d["nodes"]:
                if n["id"] == "r4-merchant":
                    n.update(type="duel", yokai="oni")
            d["nodes"][-1]["payload"] = {"scene": "tanuki-den", "anything": [1, 2]}
        code, out = self._map(other_layout)
        self.assertEqual(code, 0, out)

    def _node_scenes(self, edit, with_map=True):
        """data/map/node-scenes.json with one edit, validated from a temp data/map/ folder next to the default map."""
        import json, shutil, tempfile
        doc = json.loads((ROOT / "data" / "map" / "node-scenes.json").read_text(encoding="utf-8"))
        edit(doc)
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "data" / "map" / "node-scenes.json"
            path.parent.mkdir(parents=True)
            path.write_text(json.dumps(doc), encoding="utf-8")
            if with_map:
                shutil.copy(ROOT / "data" / "map" / "default-map.json", path.parent)
            return run(path)

    def test_node_scenes_validate_and_cover_the_maps(self):
        self.assertEqual(self._node_scenes(lambda d: None)[0], 0)

        def per_node(d):
            d["nodes"]["boss-tanuki"] = {"scene": "res://scenes/tanuki_den.tscn", "tier": "tanuki"}
            d["opponents"]["oni"] = {"fighter": "oni", "profile": "oni-patient"}
        code, out = self._node_scenes(per_node)
        self.assertEqual(code, 0, out)

        cases = [
            (lambda d: d["types"]["duel"].update(tier="legend"), "tier 'legend' is not defined in 'tiers' (Y4)"),
            (lambda d: d["opponents"]["apprentice"].update(tier="novice"), "tier 'novice' is not defined"),
            (lambda d: d["types"].pop("dojo"), "node type 'dojo' is used by default-map.json but has no entry"),
            (lambda d: d["types"]["duel"].pop("tier"), "'tier' is a required property"),
            (lambda d: d["types"]["duel"].update(scene="scenes/fight.tscn"), "does not match"),
            (lambda d: d.pop("fallback_fighter"), "'fallback_fighter' is a required property"),
        ]
        for edit, expect in cases:
            code, out = self._node_scenes(edit)
            self.assertEqual(code, 1, out)
            self.assertIn(expect, out)
        self.assertEqual(self._node_scenes(lambda d: d["types"].pop("dojo"), with_map=False)[0], 0)

    def test_explicit_invalid_folder_is_checked(self):
        code, out = run(SAMPLES / "invalid")
        self.assertEqual(code, 1)
        self.assertIn("4 file(s), 0 valid, 4 invalid", out)


FIXTURE_TRIAL = ROOT / "game" / "tests" / "fixtures" / "trials" / "test-spirit-wave-trial.json"


class TrialSchemaTest(unittest.TestCase):
    """YOK-105 (R7): kind 'trial' is registered and data/trials/ takes only trials."""

    def variant(self, tmp, change, name="test-spirit-wave-trial", folder=None):
        import json
        doc = json.loads(FIXTURE_TRIAL.read_text(encoding="utf-8"))
        doc["id"] = name
        change(doc)
        d = Path(tmp) / folder if folder else Path(tmp)
        d.mkdir(parents=True, exist_ok=True)
        p = d / f"{name}.json"
        p.write_text(json.dumps(doc), encoding="utf-8")
        return run(p)

    def test_fixture_trial_passes(self):
        code, out = run(FIXTURE_TRIAL)
        self.assertEqual(code, 0, out)
        self.assertIn("1 valid, 0 invalid", out)

    def test_bad_trials_fail_with_reasons(self):
        import tempfile
        cases = [
            (lambda d: d.__setitem__("attempts", 4), "attempts: 3 was expected"),
            (lambda d: d.pop("special"), "'special' is a required property"),
            (lambda d: d["setup"].pop("distance"), "setup: 'distance' is a required property"),
            (lambda d: d["goal"]["steps"][1].__setitem__("motion", "623"),
             "goal.steps.1.motion: 'motion' needs a Kata input; trials must be Kihon-completable (R7, K2)"),
            (lambda d: d["goal"]["steps"][1].__setitem__("precision", True), "Kihon-completable (R7, K2)"),
            (lambda d: d["goal"]["steps"][0].pop("move"), "no step names the trial's special 'test-spirit-wave'"),
            (lambda d: d["goal"]["steps"][0].__setitem__("distance", {"min": 900, "max": 400}), "min 900 is greater than max 400"),
            (lambda d: d["goal"]["steps"].__setitem__(1, {"event": "combo", "count": 1}), "goal.steps.1"),
            (lambda d: d["goal"]["steps"].__setitem__(1, {"event": "throw", "dummy_state": "airborne"}), "goal.steps.1"),
            (lambda d: d["goal"]["steps"].__setitem__(1, {"event": "hit", "dummy_state": "landed"}), "goal.steps.1"),
            (lambda d: d["setup"]["dummy"].pop("every"), "setup.dummy"),
            # A whole float is an integer to the schema and to the game's loader: compare it as one.
            (lambda d: d["goal"]["steps"][0].__setitem__("distance", {"min": 900.0, "max": 400}), "min 900 is greater than max 400"),
            # Only an attacking dummy can be blocked or counterhit (C7).
            (lambda d: d["goal"]["steps"].__setitem__(1, {"event": "block", "count": 3}),
             "goal.steps.1.event: block needs a dummy with action 'attack'"),
            (lambda d: d["goal"]["steps"][0].__setitem__("counter", True),
             "goal.steps.0.counter: a counterhit needs a dummy with action 'attack'"),
            (lambda d: d["setup"].__setitem__("dummy", {"action": "idle"}) or d["goal"]["steps"][0].__setitem__("counter", True),
             "goal.steps.0.counter: a counterhit needs a dummy with action 'attack'"),
            (lambda d: d["setup"].__setitem__("attempt_frames", 60), "setup.dummy.every: 90 is longer than the attempt (60 ticks)"),
            (lambda d: d["setup"].__setitem__("attempt_frames", 60.0), "setup.dummy.every: 90 is longer than the attempt (60 ticks)"),
        ]
        for change, expect in cases:
            with self.subTest(expect=expect), tempfile.TemporaryDirectory() as tmp:
                code, out = self.variant(tmp, change)
                self.assertEqual(code, 1, out)
                self.assertIn(expect, out)

    def test_block_and_counter_pass_against_an_attacking_dummy(self):
        import tempfile

        def change(d):
            d["setup"]["dummy"] = {"action": "attack", "moves": ["test-jab"], "every": 120}
            d["goal"]["steps"] = [{"event": "block", "count": 2}, {"event": "hit", "move": "test-spirit-wave", "counter": True}]
        with tempfile.TemporaryDirectory() as tmp:
            code, out = self.variant(tmp, change)
            self.assertEqual(code, 0, out)

    def test_throws_only_meet_throw_steps(self):
        import json, tempfile
        throw = FIXTURE_THROWS / "test-ryo-throw.json"
        punch = SAMPLES / "sample-light-punch.json"
        cases = [
            ({"event": "hit", "move": "test-ryo-throw"}, 1, "goal.steps.1.move: 'test-ryo-throw' is a throw; throws only meet a throw step"),
            ({"event": "combo", "count": 2, "move": "test-ryo-throw"}, 1, "is a throw; throws only meet a throw step"),
            ({"event": "throw", "move": "sample-light-punch"}, 1, "goal.steps.1.move: 'sample-light-punch' is not a throw"),
            ({"event": "throw", "move": "test-ryo-throw", "dummy_state": "landed", "landed_within": 3}, 0, ""),
        ]
        for step, want, expect in cases:
            with self.subTest(step=step), tempfile.TemporaryDirectory() as tmp:
                doc = json.loads(FIXTURE_TRIAL.read_text(encoding="utf-8"))
                doc["goal"]["steps"][1] = step
                p = Path(tmp) / "test-spirit-wave-trial.json"
                p.write_text(json.dumps(doc), encoding="utf-8")
                out = io.StringIO()
                validate_data.validate([str(p), str(throw), str(punch), str(SAMPLES / "sample-light-punch-clip.json")], out=out)
                text = "\n".join(l for l in out.getvalue().splitlines() if "test-spirit-wave-trial.json" in l)
                self.assertEqual(bool(text), bool(want), out.getvalue())
                self.assertIn(expect, text)

    def test_trials_folder_takes_only_trials(self):
        import tempfile
        with tempfile.TemporaryDirectory() as tmp:
            code, out = self.variant(tmp, lambda d: None, folder="data/trials")
            self.assertEqual(code, 0, out)
            code, out = self.variant(tmp, lambda d: d.__setitem__("kind", "trial"), name="x", folder="data/moves")
            self.assertEqual(code, 1, out)
            self.assertIn("files in data/moves/ must be kind", out)


if __name__ == "__main__":
    unittest.main()
