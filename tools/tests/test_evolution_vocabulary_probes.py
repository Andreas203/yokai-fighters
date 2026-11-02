"""Test Breaker probes for YOK-220 (evolution vocabulary). Run: python -m unittest discover -s tools -p 'test_*.py'
The case list is game/tests/probes/evolution_vocabulary_cases.json, the one the C# loader reads too
(game/tests/probes/EvolutionVocabularyBreakerProbes.cs). Expected values come from the schemas and the ticket's shapes."""
import copy
import io
import json
import re
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import validate_data  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
CASES = json.loads((ROOT / "game" / "tests" / "probes" / "evolution_vocabulary_cases.json").read_text(encoding="utf-8"))
FIXTURES = ROOT / "game" / "tests" / "fixtures"


def run(*paths):
    out = io.StringIO()
    code = validate_data.validate([str(p) for p in paths], out=out)
    return code, out.getvalue()


class EvolutionVocabularyProbes(unittest.TestCase):
    def test_modifier_cases(self):
        wrong = []
        with tempfile.TemporaryDirectory() as tmp:
            for c in CASES["cases"]:
                doc = {"kind": "modifier", "id": c["name"], "name": c["name"], "source": "oni", "rarity": "common",
                       "locked": False, "rules": ["A10"], "applies_to": {"requires": c["requires"]},
                       "effects": c["effects"], "card": {"plain": "Test case.", "frames": "Test case."}}
                if c.get("withCharge"):
                    doc["charge"] = CASES["charge"]
                f = Path(tmp) / f"{c['name']}.json"
                f.write_text(json.dumps(doc), encoding="utf-8")
                code, out = run(f)
                if (code == 0) != c["accept"]:
                    wrong.append(f"{c['name']}: validator {'accepted' if code == 0 else 'refused'}\n{out}")
        self.assertEqual(wrong, [], "\n".join(wrong))

    def test_special_cases(self):
        wrong = []
        with tempfile.TemporaryDirectory() as tmp:
            for c in CASES["special_cases"]:
                doc = json.loads((FIXTURES / "specials" / f"{c['base']}.json").read_text(encoding="utf-8"))
                doc["id"] = c["name"]
                fd = doc["levels"]["1"]["frame_data"]
                fd.update(c["set"])
                for k in c["remove"]:
                    fd.pop(k, None)
                if "changes" in c:
                    doc["levels"]["3"]["evolution"]["changes"] = c["changes"]
                f = Path(tmp) / f"{c['name']}.json"
                f.write_text(json.dumps(doc), encoding="utf-8")
                code, out = run(f)
                lines = [l for l in out.splitlines()[:-1] if "clip: clip '" not in l]  # fixtures have no clip file
                if (not lines) != c["accept"]:
                    wrong.append(f"{c['name']}: validator {'accepted' if not lines else 'refused'}\n{out}")
        self.assertEqual(wrong, [], "\n".join(wrong))

    def test_normal_cases(self):
        """A normal's properties.* take the same shapes (frame-data.schema.json) and the loader's whole-number rule."""
        base = json.loads((FIXTURES / "ryo-normals" / "test-ryo-heavy-kick.json").read_text(encoding="utf-8"))
        wrong = []
        with tempfile.TemporaryDirectory() as tmp:
            for c in CASES["normal_cases"]:
                doc = copy.deepcopy(base)
                doc["id"] = c["name"]
                doc["frame_data"]["properties"] = c["properties"]
                f = Path(tmp) / f"{c['name']}.json"
                f.write_text(json.dumps(doc), encoding="utf-8")
                code, out = run(f)
                lines = [l for l in out.splitlines()[:-1] if "clip: clip '" not in l and "'clip' is a required" not in l]
                if (not lines) != c["accept"]:
                    wrong.append(f"{c['name']}: validator {'accepted' if not lines else 'refused'}\n{out}")
        self.assertEqual(wrong, [], "\n".join(wrong))

    def test_schema_target_pattern_matches_the_validator_int_targets(self):
        """Every integer target the validator folds with whole() is one the schema's effectTarget names, and back."""
        common = json.loads((validate_data.SCHEMA_DIR / "common.schema.json").read_text(encoding="utf-8"))
        pattern = re.compile(common["$defs"]["effectTarget"]["pattern"])
        wrong = []
        for t in sorted(validate_data.INT_TARGETS):
            for form in {t, t[len("properties."):] if t.startswith("properties.") else t}:
                if not pattern.match(form):
                    wrong.append(f"{form}: in validate_data.INT_TARGETS but not in the schema pattern")
        for t in ("throw.range", "properties.decoy", "forward_travel", "properties.forward_travel"):
            if t not in validate_data.INT_TARGETS and "properties." + t not in validate_data.INT_TARGETS:
                wrong.append(f"{t}: the schema names it, INT_TARGETS does not")
        self.assertEqual(wrong, [])


if __name__ == "__main__":
    unittest.main()
