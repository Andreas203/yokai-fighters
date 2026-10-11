"""Reads the design: the written rules, the GDD transcription and the amendments.

The agent takes three things from the design and nothing about priority from anywhere else:

  * the text of every rule ID (C4, T2, R6...), so a feature can be checked against the
    words it claims to implement and the writer and critics see those exact words;
  * the scope facts in section G: what is never cut (G1), what is cut first (G2, G3)
    and which week each milestone belongs to (G5);
  * the GDD section a feature comes from, for the longer explanation behind the rule.

If the designer edits G1, G2, G3 or G5, the ranking changes with no change to this tool.
"""
from __future__ import annotations

import re
from dataclasses import dataclass, field
from pathlib import Path

RULES = "docs/design/rules.md"
GDD = "docs/design/gdd-extended.md"
AMENDMENTS = "docs/design/gdd-amendments.md"

_RULE = re.compile(r"^- \*\*([A-Z]\d+)\*\* (.*)$")
_SECTION = re.compile(r"^## (.+)$")


@dataclass
class Design:
    rules: dict[str, str]                 # rule ID -> full text, tables included
    rule_section: dict[str, str]          # rule ID -> "T — The Tanuki copy rule (never cut)"
    never_cut: list[str]                  # G1, in the order written
    cut_order: list[str]                  # G2 then G3: first entry is cut first
    schedule: dict[int, str]              # G5: week -> milestone text
    last_week: int
    open_questions: list[str]
    amendments: list[str]                 # "AM1 — ..." headings, for the trace
    gdd_sections: dict[str, str] = field(default_factory=dict)  # "5.3" -> section text

    def rule_text(self, ids: list[str]) -> str:
        return "\n".join(f"- {i}: {self.rules[i]}" for i in ids)

    def week_of(self, milestone: str) -> int | None:
        """The first week whose G5 milestone text names this milestone."""
        for week in sorted(self.schedule):
            if milestone.lower() in self.schedule[week].lower():
                return week
        return None

    def is_never_cut(self, terms: list[str]) -> str | None:
        """The G1 entry one of the feature's terms names, if any."""
        for entry in self.never_cut:
            if any(t.lower() == entry.lower() for t in terms):
                return entry
        return None

    def cut_rank(self, terms: list[str]) -> int | None:
        """Position in the cut order (0 = cut first), if a cut-gate entry names one of the terms."""
        for rank, entry in enumerate(self.cut_order):
            if any(t.lower() in entry.lower() for t in terms):
                return rank
        return None


def _split_list(text: str) -> list[str]:
    """'the copy rule, the merchant, Kihon' or 'cut Kata, then the meta-unlock layer (x stay)' -> entries."""
    text = re.sub(r"\([^)]*\)", "", text)
    parts = re.split(r",\s*then\s+|,\s*", text)
    out = []
    for p in parts:
        p = re.sub(r"^(cut|then|the)\s+", "", p.strip().rstrip("."), flags=re.I)
        p = re.sub(r"^the\s+", "", p, flags=re.I).strip()
        if p:
            out.append(p)
    return out


def load(root: Path) -> Design:
    lines = (root / RULES).read_text(encoding="utf-8").split("\n")
    rules: dict[str, list[str]] = {}
    rule_section: dict[str, str] = {}
    open_questions: list[str] = []
    section, current = "", None
    for line in lines:
        if m := _SECTION.match(line):
            section, current = m.group(1), None
            continue
        if section.startswith("Open questions"):
            if line.startswith("- "):
                open_questions.append(line[2:].strip())
            continue
        if m := _RULE.match(line):
            current = m.group(1)
            rules[current] = [m.group(2)]
            rule_section[current] = section
        elif current and line.strip() and not line.startswith("#") and line.strip() != "---":
            rules[current].append(line.rstrip())  # a rule's table or continuation
        elif line.startswith("#"):
            current = None
    text = {k: "\n".join(v).strip() for k, v in rules.items()}

    for needed in ("G1", "G2", "G3", "G5"):
        if needed not in text:
            raise SystemExit(f"{RULES}: rule {needed} is missing; the agent ranks work from it")

    never_cut = _split_list(text["G1"].split(":", 1)[1])
    cut_order = _split_list(text["G2"].split(":", 1)[1]) + _split_list(text["G3"].split(":", 1)[1])

    schedule: dict[int, str] = {}
    last_week = 0
    for m in re.finditer(r"W(\d)(?:–(\d))?\s+(.*?)(?=\s+W\d|$)", text["G5"], re.S):
        schedule[int(m.group(1))] = m.group(3).strip()
        last_week = max(last_week, int(m.group(2) or m.group(1)))
    if not schedule:
        raise SystemExit(f"{RULES}: G5 has no 'W1 ... W2 ...' schedule to read")

    amendments = re.findall(r"^## (AM\d+ — .+)$", (root / AMENDMENTS).read_text(encoding="utf-8"), re.M)

    gdd_sections: dict[str, str] = {}
    key, buf = None, []
    for line in (root / GDD).read_text(encoding="utf-8").split("\n"):
        m = re.match(r"^#{2,3} (\d+(?:\.\d+)?)\.? ", line)
        if m:
            if key:
                gdd_sections[key] = "\n".join(buf).strip()
            key, buf = m.group(1), [line]
        elif key and not line.startswith("<!-- page"):
            buf.append(line)
    if key:
        gdd_sections[key] = "\n".join(buf).strip()

    return Design(text, rule_section, never_cut, cut_order, schedule, last_week,
                  open_questions, amendments, gdd_sections)
