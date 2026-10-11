"""The adversaries. Three of them stand between the writer and an accepted feature.

  code checks    no model: patterns that are never acceptable in this codebase.
  code-critic    a model told to assume the code is wrong and prove it against the rule
                 text. A finding counts only if the line it quotes is really in the file
                 and the rule it cites is really in rules.md.
  test-breaker   a model that writes probe tests meant to fail. The probes are compiled
                 and run inside the game's test runner: a probe that fails is a finding,
                 a probe that passes proves nothing was wrong there.

The two model critics take their brief from the crew's own role files
(.claude/agents/code-critic.md and test-breaker.md), so the agent's critics and the
crew's reviewers attack the same things.
"""
from __future__ import annotations

import re
import sys
from dataclasses import dataclass
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "content_pipeline"))
from llm import parse_json  # noqa: E402

import verify  # noqa: E402
import writer  # noqa: E402
from scan import strip_comments  # noqa: E402

CONSTRAINTS = ("determinism", "sim-purity", "data-driven", "scope", "state", "refused-action", "tests")
PROBE_CLASS = "GoalAgentProbes"


@dataclass
class Issue:
    source: str            # format | code-check | build | tests | scan | code-critic | test-breaker
    blocking: bool
    where: str
    rule: str
    problem: str
    suggestion: str = ""

    def render(self) -> str:
        head = f"[{self.source}{'' if self.blocking else ', advisory'}]"
        where = f" {self.where}" if self.where else ""
        rule = f" ({self.rule})" if self.rule else ""
        fix = f" → {self.suggestion}" if self.suggestion else ""
        return f"{head}{where}{rule}: {self.problem}{fix}"


# ---------------------------------------------------------------- code checks (no model)

_SIM_FORBIDDEN = [
    (r"\busing\s+Godot\b|\bGD\.\w+", "sim-purity", "Godot type in sim code", "keep game/scripts/Sim and Ai pure C#"),
    (r"\b(float|double|decimal)\b", "determinism", "floating-point type in sim code", "use integers (C1, F3)"),
    (r"System\.Random|\bnew Random\(|Guid\.NewGuid|DateTime\.|Stopwatch|Environment\.TickCount",
     "determinism", "clock or unseeded randomness", "SimRng is the only random source"),
]
_ANYWHERE = [
    (r"NotImplementedException|\bTODO\b|\bFIXME\b", "scope", "unfinished code", "finish it or leave it out"),
]
_TEST_FORBIDDEN = [
    (r"LoadRepo\(|ContentPaths\.Data\(|\"data/", "tests", "test reads data/ instead of fixtures", "use AbilityPool.LoadFixtures and tests/fixtures"),
]


def content_ids(root: Path) -> set[str]:
    """Ids that must come from data, never from a string in shipping code."""
    ids = {"kitsune", "oni", "kappa", "tanuki"}
    for folder in ("data/moves", "data/modifiers", "data/cancels", "game/tests/fixtures/specials", "game/tests/fixtures/rewards"):
        ids |= {p.stem for p in (root / folder).glob("*.json")}
    return ids


def code_checks(root: Path, change: writer.Change) -> list[Issue]:
    issues = []
    ids = content_ids(root)
    for path in change.paths:
        added = strip_comments(change.new_text(path))
        is_test = path.startswith("game/tests/")
        is_sim = path.startswith(("game/scripts/Sim/", "game/scripts/Ai/"))
        rules = list(_ANYWHERE) + (_SIM_FORBIDDEN if is_sim else []) + (_TEST_FORBIDDEN if is_test else [])
        for pattern, rule, problem, fix in rules:
            if m := re.search(pattern, added):
                issues.append(Issue("code-check", True, path, rule, f"{problem}: `{m.group(0)}`", fix))
        if not is_test:
            for lit in sorted(set(re.findall(r'"([a-z][a-z0-9-]+)"', added)) & ids):
                issues.append(Issue("code-check", True, path, "data-driven",
                                    f"content id \"{lit}\" hard-coded in shipping code", "read it from data (A5, A10)"))
    return issues


# ---------------------------------------------------------------- code-critic (model, static)

def _changed_files(root: Path, change: writer.Change) -> str:
    out = []
    for path in change.paths:
        text = (root / path).read_text(encoding="utf-8")
        numbered = "\n".join(f"{n:4} {line}" for n, line in enumerate(text.split("\n"), 1))
        what = "new file" if path in change.files else "existing file, shown whole" + ("; its edits follow" if change.edits.get(path) else "")
        out.append(f"===== {path} ({what}) =====\n{numbered}")
        for search, replace in change.edits.get(path, []):
            out.append(f"--- edit in {path}: this text\n{search}\n--- was replaced by\n{replace}")
    return "\n\n".join(out)


def critic_prompts(root: Path, feature: dict, design, change: writer.Change) -> tuple[str, str]:
    lens = writer.agent_section(root, "code-critic", "What to attack")
    proof = writer.agent_section(root, "code-critic", "Standard of proof")
    system = f"""You are Code Critic, the adversary in the Yokai Fighters code workflow. Another model wrote the code below and wants it accepted. Assume it is wrong and find the proof. A green test run is not evidence that it is right.

You have no tools: the rules, the feature and the code are all in the message.

What to attack:
{lens}

Standard of proof:
{proof}

Answer with one JSON object and nothing else:
{{"issues": [{{"severity": "blocking" | "advisory", "file": "<path>", "quote": "<one line copied exactly from that file>", "rule": "<a rule ID from the rules given, or one of: {', '.join(CONSTRAINTS)}>", "problem": "<what is wrong, with the input or state that shows it>", "suggestion": "<the change that fixes it>"}}]}}
An empty list means you could not prove anything wrong. Do not pad it: a finding whose quote is not in the file, or whose rule does not exist, is thrown away."""
    user = "\n\n".join([
        f"# Feature under review: {feature['title']}  (`{feature['id']}`)",
        "## The rules it claims to implement\n" + design.rule_text(feature["rules"]),
        f"## The GDD section behind them ({feature['gdd']})\n" + design.gdd_sections[feature["gdd"]],
        "## What the writer was asked for\n" + feature["build"]["brief"],
        "## The writer's notes\n" + (change.notes or "none"),
        "## The change\n" + _changed_files(root, change),
    ])
    return system, user


def _norm(s: str) -> str:
    return " ".join(s.split())


def verify_findings(root: Path, raw: list[dict], design, allowed_paths: list[str]) -> tuple[list[Issue], list[str]]:
    """Keeps only findings that can be checked: a real file, a real quoted line, a real rule."""
    kept, dropped = [], []
    for f in raw:
        path, quote, rule = str(f.get("file", "")), str(f.get("quote", "")), str(f.get("rule", ""))
        label = f"{path}: {str(f.get('problem', ''))[:90]}"
        if path not in allowed_paths or not (root / path).is_file():
            dropped.append(f"{label} — file is not part of the change")
            continue
        lines = (root / path).read_text(encoding="utf-8").split("\n")
        line_no = next((n for n, line in enumerate(lines, 1) if _norm(quote) and _norm(quote) in _norm(line)), None)
        if line_no is None:
            dropped.append(f"{label} — quoted line is not in the file: {quote[:70]!r}")
            continue
        if rule not in design.rules and rule not in CONSTRAINTS:
            dropped.append(f"{label} — cites '{rule}', which is not a rule")
            continue
        kept.append(Issue("code-critic", f.get("severity") == "blocking", f"{path}:{line_no}", rule,
                          str(f.get("problem", "")).strip(), str(f.get("suggestion", "")).strip()))
    return kept, dropped


def code_critic(llm, model: str, root: Path, feature: dict, design, change: writer.Change):
    system, user = critic_prompts(root, feature, design, change)
    reply = llm.complete("code-critic", model, system, user)
    try:
        raw = parse_json(reply).get("issues", [])
    except ValueError:
        return [], [f"reply was not JSON: {reply[:120]!r}"]
    return verify_findings(root, raw, design, change.paths)


# ---------------------------------------------------------------- test-breaker (model, then run)

def breaker_prompts(root: Path, feature: dict, design, change: writer.Change, class_name: str,
                    compile_errors: list[str] | None = None, previous: str = "") -> tuple[str, str]:
    lens = writer.agent_section(root, "test-breaker", "What to attack")
    system = f"""You are Test Breaker, the second adversary in the Yokai Fighters code workflow. Another model wrote the code and the tests below. Its tests show the cases it thought of; you write the cases it did not. You do not argue that the code is wrong: you write probe tests, and they are compiled and run in the game's own test runner. A probe that fails is your finding.

You have no tools: the rules, the feature and the code are all in the message. Call only types and members you can see in it.

What to attack:
{lens}

Rules for probes:
- The expected value of every probe comes from the rule text, never from what the code happens to do. If the rule is silent on a case, do not probe it.
- Above each probe, one comment: `// <rule ID>: <what the rule says must happen>`.
- At most 8 probes, each a `[Test] public static void` with a name that says what it tries to break, using `Assert.True(cond, msg)` and `Assert.Equal(expected, actual, msg)`.
- One file: namespace `YokaiFighters.Tests`, `public static class {class_name}`. Test fixtures only, never `data/`. Tabs for indentation.
- A probe that does not compile is thrown away, so prefer the helper code the author's tests already use.

Answer with marked blocks only, each marker at the start of a line:
@@@ PROBES
<the whole C# file>
@@@ END
@@@ NOTES
<author tests that could not fail even with the feature removed, and why; or "none">
@@@ END"""
    parts = [
        f"# Feature under attack: {feature['title']}  (`{feature['id']}`)",
        "## The rules it claims to implement\n" + design.rule_text(feature["rules"]),
        "## What the writer was asked for\n" + feature["build"]["brief"],
        "## The change (the author's tests included)\n" + _changed_files(root, change),
        "## The test runner and the code next to the feature\n" + writer.read_context(
            root, [p for p in feature["build"]["context"] + ["game/tests/TestRunner.cs"]
                   if (root / p).is_file() and p not in change.paths]),
    ]
    if compile_errors:
        parts.append("## Your previous probes did not compile\n" + previous.strip()
                     + "\n\nCompiler errors:\n" + "\n".join(compile_errors) + "\n\nWrite the file again so that it compiles.")
    return system, "\n\n".join(parts)


def probe_rules(source: str) -> dict[str, str]:
    """probe method -> the rule comment written above it."""
    out = {}
    for m in re.finditer(r"//\s*([^\n]*)\n(?:\s*//[^\n]*\n)*\s*\[Test\]\s*public\s+static\s+void\s+(\w+)", source):
        out[m.group(2)] = m.group(1).strip()
    return out


def probe_path(class_name: str) -> str:
    return f"game/tests/{class_name}.cs"


def remove_probes(root: Path) -> None:
    for p in (root / "game/tests").glob(f"{PROBE_CLASS}*.cs*"):
        p.unlink()


def write_probes(llm, model: str, root: Path, feature: dict, design, change: writer.Change, class_name: str, log):
    """Asks for probes and makes them compile (one retry). Returns (source, notes), source "" if none compiled."""
    errors, previous = None, ""
    for round_no in (1, 2):
        system, user = breaker_prompts(root, feature, design, change, class_name, errors, previous)
        reply = writer.parse(llm.complete("test-breaker", model, system, user))
        source = reply.probes
        if f"class {class_name}" not in source:
            log(f"test-breaker: reply had no `{class_name}` class; no probes")
            return "", reply.notes
        (root / probe_path(class_name)).write_text(source, encoding="utf-8")
        errors = [e for e in verify.build(root) if class_name in e]
        if not errors:
            return source, reply.notes
        (root / probe_path(class_name)).unlink()
        previous = source
        log(f"test-breaker: probes did not compile (round {round_no}): {errors[0]}")
    return "", reply.notes


def judge_probes(run: verify.TestRun, class_name: str, source: str, only: set[str] | None = None) -> list[Issue]:
    """Failing probes become findings. `only` limits it to probes that failed before and are being re-run."""
    rules = probe_rules(source)
    issues = []
    for test, message in sorted(run.failed.items()):
        cls, _, name = test.partition(".")
        if cls != class_name or (only is not None and name not in only):
            continue
        said = rules.get(name, "")
        rule_id = (re.match(r"([A-Z]\d+)\b", said) or [None, ""])[1]
        issues.append(Issue("test-breaker", True, f"probe {name}", rule_id,
                            f"probe fails: {message}" + (f" — the probe expects: {said}" if said else ""),
                            "make the code follow the rule, or dispute the probe if the rule does not say that"))
    return issues
