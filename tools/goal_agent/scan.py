"""Scans the codebase and compares it with the features the design requires.

`scan()` walks the game code, its tests and the content data and records what is there:
C# types, public members, `[Test]` methods and data files. `detect_gaps()` then checks each
feature's evidence against that scan. A feature is

  present  when every piece of evidence is found,
  partial  when some is,
  missing  when none is.

Evidence is matched against code with comments removed, so a comment that says
"copy rule not built here" cannot make the copy rule look built.
"""
from __future__ import annotations

import fnmatch
import json
import re
from dataclasses import dataclass
from pathlib import Path

SCAN_ROOTS = ("game/scripts", "game/tests", "data")
FEATURES = "tools/goal_agent/features.json"

_TYPE = re.compile(r"\b(?:class|record|interface|enum|struct)\s+([A-Z]\w*)")
_TEST = re.compile(r"\[Test\]\s*(?:///[^\n]*\n\s*)*public\s+static\s+void\s+(\w+)")
_MEMBER = re.compile(r"\bpublic\s+(?:static\s+|sealed\s+|override\s+|readonly\s+|virtual\s+|const\s+)*"
                     r"[\w<>\[\]?,. ]+?\s+(\w+)\s*(?:\(|\{|=>|=|;)")
_COMMENT = re.compile(r"//[^\n]*|/\*.*?\*/", re.S)


def strip_comments(code: str) -> str:
    return _COMMENT.sub("", code)


@dataclass
class Scan:
    files: dict[str, dict]      # repo-relative path -> {"types", "members", "tests", "lines"}
    code: dict[str, str]        # .cs path -> source with comments removed (not written to disk)

    @property
    def tests(self) -> list[str]:
        return sorted(t for f in self.files.values() for t in f.get("tests", []))

    def summary(self) -> dict:
        cs = [f for p, f in self.files.items() if p.endswith(".cs")]
        return {
            "files": len(self.files),
            "cs_files": len(cs),
            "types": sum(len(f["types"]) for f in cs),
            "tests": len(self.tests),
            "data_files": sum(1 for p in self.files if p.startswith("data/")),
        }

    def to_json(self) -> str:
        return json.dumps({"summary": self.summary(), "files": self.files}, indent=1, sort_keys=True)


def scan(root: Path) -> Scan:
    files: dict[str, dict] = {}
    code: dict[str, str] = {}
    for base in SCAN_ROOTS:
        for path in sorted((root / base).rglob("*")):
            if not path.is_file() or path.suffix not in (".cs", ".json", ".tscn"):
                continue
            rel = path.relative_to(root).as_posix()
            if path.suffix != ".cs":
                files[rel] = {}
                continue
            raw = path.read_text(encoding="utf-8", errors="replace")
            src = strip_comments(raw)
            code[rel] = src
            types = sorted(set(_TYPE.findall(src)))
            owner = types[0] if types else path.stem
            files[rel] = {
                "types": types,
                "members": sorted(set(_MEMBER.findall(src)) - set(types)),
                "tests": [f"{path.stem}.{name}" for name in _TEST.findall(raw)] if "/tests/" in rel else [],
                "lines": raw.count("\n") + 1,
                "owner": owner,
            }
    return Scan(files, code)


def load_features(root: Path) -> dict:
    return json.loads((root / FEATURES).read_text(encoding="utf-8"))


def _glob(paths, pattern: str) -> list[str]:
    return [p for p in paths if fnmatch.fnmatch(p, pattern)]


def check_evidence(ev: dict, s: Scan) -> tuple[bool, str]:
    """One piece of evidence against the scan -> (found, what was looked for)."""
    if "symbol" in ev:
        where = ev.get("in", "game/scripts/*")
        rx = re.compile(ev["symbol"])
        hit = next((p for p in _glob(s.code, where) if rx.search(s.code[p])), None)
        return hit is not None, f"code matching /{ev['symbol']}/ in {where}" + (f" (found in {hit})" if hit else "")
    if "test" in ev:
        rx = re.compile(ev["test"])
        hits = [t for t in s.tests if rx.search(t)]
        need = ev.get("min", 1)
        return len(hits) >= need, f"at least {need} [Test] matching /{ev['test']}/ (found {len(hits)})"
    if "file" in ev:
        hits = _glob(s.files, ev["file"])
        need = ev.get("min", 1)
        return len(hits) >= need, f"at least {need} file(s) matching {ev['file']} (found {len(hits)})"
    raise SystemExit(f"features.json: unknown evidence {ev}")


@dataclass
class Gap:
    feature: dict
    status: str                          # present | partial | missing
    checks: list[tuple[bool, str]]

    @property
    def id(self) -> str:
        return self.feature["id"]

    @property
    def unmet(self) -> list[str]:
        return [what for ok, what in self.checks if not ok]


def detect_gaps(features: list[dict], s: Scan) -> dict[str, Gap]:
    """The gap-detection pass: every feature the design requires, checked against the scan."""
    out: dict[str, Gap] = {}
    for f in features:
        checks = [check_evidence(ev, s) for ev in f["evidence"]]
        found = sum(1 for ok, _ in checks if ok)
        status = "present" if found == len(checks) else "missing" if found == 0 else "partial"
        out[f["id"]] = Gap(f, status, checks)
    return out


def validate_manifest(manifest: dict, design, root: Path) -> list[str]:
    """The manifest has to stay true to the design. Returns every problem found."""
    problems = []
    features = manifest["features"]
    ids = [f["id"] for f in features]
    for dup in sorted({i for i in ids if ids.count(i) > 1}):
        problems.append(f"duplicate feature id '{dup}'")
    covered = set(manifest.get("not_tracked", {}))
    for f in features:
        where = f"feature '{f['id']}'"
        for r in f["rules"]:
            if r not in design.rules:
                problems.append(f"{where}: rule {r} is not in rules.md")
            covered.add(r)
        if design.week_of(f["milestone"]) is None:
            problems.append(f"{where}: milestone '{f['milestone']}' is not named in the G5 schedule")
        if f.get("gdd", "").startswith("11"):
            problems.append(f"{where}: GDD section 11 is the full game, not the 5-week build")
        if f.get("gdd") and f["gdd"] not in design.gdd_sections:
            problems.append(f"{where}: GDD section {f['gdd']} not found")
        for n in f.get("needs", []):
            if n not in ids:
                problems.append(f"{where}: needs unknown feature '{n}'")
        if not f["evidence"]:
            problems.append(f"{where}: no evidence, so the scan could never find it")
        if "build" in f and not all(p.endswith(".cs") and p.startswith(("game/scripts/", "game/tests/")) for p in f["build"]["files"]):
            problems.append(f"{where}: build.files must be C# files under game/scripts/ or game/tests/")
    for r in manifest.get("not_tracked", {}):
        if r not in design.rules:
            problems.append(f"not_tracked: rule {r} is not in rules.md")
    for r in sorted(set(design.rules) - covered, key=lambda x: (x[0], int(x[1:]))):
        problems.append(f"rule {r} is neither covered by a feature nor listed under not_tracked")
    return problems
