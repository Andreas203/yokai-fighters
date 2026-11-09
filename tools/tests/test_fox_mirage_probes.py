"""Test Breaker probes for YOK-224 (validator side of the teleport and decoy shapes). The case list mirrors
FoxMirageBreakerProbes.Loader_* in game/tests/probes: the validator must accept exactly what the loader accepts
(frame-data.schema.json: teleport = {to: behind_opponent, frame: integer >= 1 inside the move}; decoy = integer >= 1)."""
import copy
import io
import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import validate_data  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / "game" / "tests" / "fixtures" / "specials" / "test-rising-talisman.json"
TP = {"to": "behind_opponent", "frame": 18}


def mirage_doc(name):
    doc = json.loads(BASE.read_text(encoding="utf-8"))
    doc["id"] = name
    doc["levels"]["1"]["frame_data"].update({
        "startup": 18, "active": None, "recovery": 10, "hitboxes": [],
        "hurtboxes": [{"frames": [1, 28], "rect": {"x": -45, "y": 0, "w": 90, "h": 320}}],
        "properties": {"teleport": copy.deepcopy(TP)}})
    doc["levels"]["2"] = {"status": "proposed", "tuning": {"target": "recovery", "op": "add", "value": -2}}
    doc["levels"]["3"]["evolution"]["changes"] = [{"target": "properties.decoy", "op": "set", "value": 1}]
    doc["ex"] = {"meter_cost": 100, "changes": [{"target": "recovery", "op": "add", "value": -3}]}
    return doc


def refused(doc):
    with tempfile.TemporaryDirectory() as tmp:
        f = Path(tmp) / f"{doc['id']}.json"
        f.write_text(json.dumps(doc), encoding="utf-8")
        out = io.StringIO()
        validate_data.validate([str(f)], out=out)
        lines = [l for l in out.getvalue().splitlines()[:-1] if "clip: clip '" not in l]
        return lines


class FoxMirageValidatorProbes(unittest.TestCase):
    def test_teleport_and_decoy_shapes_match_the_loader(self):
        raw = lambda s: json.loads(s)
        cases = [
            ({"to": "behind_opponent", "frame": 18}, None, True),
            ({"to": "behind_opponent", "frame": raw("18.0")}, None, True),
            ({"to": "behind_opponent", "frame": raw("1.8e1")}, None, True),
            ({"to": "behind_opponent", "frame": 23}, None, True),
            ({"to": "behind_opponent", "frame": 1}, None, True),
            ({"to": "behind_opponent", "frame": 24}, None, False),
            ({"to": "behind_opponent", "frame": 0}, None, False),
            ({"to": "behind_opponent", "frame": -1}, None, False),
            ({"to": "behind_opponent", "frame": 17.5}, None, False),
            ({"to": "behind_opponent", "frame": True}, None, False),
            ({"to": "behind_opponent", "frame": "18"}, None, False),
            ({"to": "behind_opponent", "frame": None}, None, False),
            ({"to": "behind_opponent", "frame": 4294967314}, None, False),
            ({"to": "Behind_Opponent", "frame": 18}, None, False),
            ({"to": None, "frame": 18}, None, False),
            ({"frame": 18}, None, False),
            ({"to": "behind_opponent", "frame": 18, "x": 0}, None, False),
            (None, None, False),
            ([], None, False),
            (18, None, False),
            (True, None, False),
            (TP, 1, True), (TP, 1.0, True), (TP, 3, True),
            (TP, 0, False), (TP, -1, False), (TP, 1.5, False), (TP, True, False), (TP, "1", False),
            (TP, None, False), (TP, [1], False), (TP, 4294967297, False),
        ]
        wrong = []
        for i, (tp, decoy, ok) in enumerate(cases):
            doc = mirage_doc(f"bp-case-{i}")
            props = doc["levels"]["1"]["frame_data"]["properties"]
            props["teleport"] = tp
            if decoy is not None or (len(cases) and i >= 21):
                props["decoy"] = decoy
            lines = refused(doc)
            if (not lines) != ok:
                wrong.append(f"case {i} teleport={tp!r} decoy={decoy!r}: validator {'accepted' if not lines else 'refused'}, loader {'accepts' if ok else 'refuses'}")
        self.assertEqual(wrong, [], "\n".join(wrong))

    def test_tuning_or_ex_that_ends_the_move_before_the_teleport_is_refused(self):
        doc = mirage_doc("bp-short-lv2")
        doc["levels"]["2"]["tuning"]["value"] = -11  # total 17 < frame 18
        self.assertTrue(refused(doc), "Lv 2 recovery -11 leaves a 17-frame move with a frame-18 teleport")
        doc = mirage_doc("bp-short-ex")
        doc["ex"]["changes"][0]["value"] = -11
        self.assertTrue(refused(doc), "EX recovery -11 does the same")
        doc = mirage_doc("bp-exact-lv2")
        doc["levels"]["2"]["tuning"]["value"] = -10  # total 18 = the frame: legal
        doc["ex"]["changes"] = [{"target": "damage", "op": "add", "value": 10}]  # EX stacks on Lv 2: don't push recovery below 0
        self.assertEqual(refused(doc), [], "teleport on the last frame is legal")

    def test_effects_cannot_target_the_teleport_or_a_bad_decoy(self):
        for target, value, ok in [("properties.teleport.frame", 5, False), ("properties.teleport", {}, False),
                                  ("decoy", 0, False), ("decoy", 2.5, False), ("decoy", True, False),
                                  ("decoy", 2, True), ("properties.decoy", 2.0, True)]:
            doc = mirage_doc("bp-effect")
            doc["levels"]["3"]["evolution"]["changes"] = [{"target": target, "op": "set", "value": value}]
            self.assertEqual(not refused(doc), ok, f"{target}={value!r}")


if __name__ == "__main__":
    unittest.main()
