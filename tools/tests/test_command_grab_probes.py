"""YOK-225 breaker probes (Python side): the validator must answer the shared command-grab build cases as the loader does,
and no real modifier may be refused on a command grab. Expected values: C4/E12 (window under recovery), the schema."""
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
GRAB = ROOT / "game" / "tests" / "fixtures" / "command-grabs" / "test-river-grab.json"
CASES = ROOT / "game" / "tests" / "probes" / "command-grab-build-cases.json"


def run(*paths):
    out = io.StringIO()
    code = validate_data.validate([str(p) for p in paths], out=out)
    return code, out.getvalue()


def patched(case):
    doc = json.loads(GRAB.read_text(encoding="utf-8"))
    for p in case["patch"]:
        node = doc
        for k in p["path"][:-1]:
            node = node[int(k)] if isinstance(node, list) else node[k]
        if p["value"] is None:
            node.pop(p["path"][-1], None)
        else:
            node[p["path"][-1]] = copy.deepcopy(p["value"])
    return doc


class CommandGrabBreakerProbes(unittest.TestCase):
    def test_validator_answers_the_shared_build_cases_as_the_loader(self):
        cases = json.loads(CASES.read_text(encoding="utf-8"))["cases"]
        wrong = []
        with tempfile.TemporaryDirectory() as tmp:
            for c in cases:
                doc = patched(c)
                doc["id"] = "probe-" + c["name"]
                f = Path(tmp) / f"{doc['id']}.json"
                f.write_text(json.dumps(doc), encoding="utf-8")
                code, out = run(f)
                lines = [l for l in out.splitlines()[:-1] if "clip: clip '" not in l]  # the fixture has no clip file
                if (not lines) != c["accept"]:
                    wrong.append(f"{c['name']}: validator {'accepted' if not lines else 'refused'} (expected accept={c['accept']})\n{out}")
        self.assertEqual(wrong, [], "\n".join(wrong))

    def test_real_modifiers_validate_with_a_command_grab_in_the_run(self):
        """Every real modifier x the command grab fixture: a refusal here is a draft that would crash in game."""
        code, out = run(ROOT / "data" / "modifiers", GRAB)
        lines = [l for l in out.splitlines()[:-1] if "clip: clip '" not in l]
        self.assertEqual(lines, [], out)

    def test_is_command_grab_ignores_lookalikes(self):
        base = json.loads(GRAB.read_text(encoding="utf-8"))
        self.assertTrue(validate_data.is_command_grab(base))
        for kind in ("normal", "throw"):
            d = copy.deepcopy(base)
            d["kind"] = kind
            self.assertFalse(validate_data.is_command_grab(d), kind)
        d = copy.deepcopy(base)
        d["levels"]["1"]["frame_data"]["properties"]["throw"] = "yes"
        self.assertFalse(validate_data.is_command_grab(d), "a non-object throw is not a command grab")
        d = copy.deepcopy(base)
        del d["levels"]["1"]["frame_data"]["properties"]["throw"]
        self.assertFalse(validate_data.is_command_grab(d))

    def test_built_move_errors_default_kind_is_special(self):
        fd = json.loads(GRAB.read_text(encoding="utf-8"))["levels"]["1"]["frame_data"]
        self.assertEqual(validate_data.built_move_errors(copy.deepcopy(fd)), [])
        self.assertEqual(validate_data.built_move_errors(copy.deepcopy(fd), "special"), [])
        for kind in ("normal", "throw"):
            self.assertTrue(validate_data.built_move_errors(copy.deepcopy(fd), kind), kind)


if __name__ == "__main__":
    unittest.main()
