#!/usr/bin/env python3
"""Docs check (YOK-188): keeps the repo's Markdown navigable. Stdlib only.

Usage:
    python -I tools/check_docs.py [--root DIR]

Over the .md files git tracks under ROOT (default: this checkout):

  links       every relative Markdown link and image resolves to a file or folder in the checkout
              (code blocks and URLs are skipped; #anchors are not checked)
  placement   every .md sits in one of DOC_HOMES; a new kind of document gets a home here and a row
              in docs/README.md in the same PR
  proposals   no .md is named like a proposal or a to-do list: open proposals live on their Linear
              ticket (docs/process/change-tracking.md)
  amendments  docs/design/gdd-amendments.md: every `## AMn` heading has a row in its index table
              and every row a heading

Exit 0 clean, 1 findings, 2 not a git checkout.
"""
from __future__ import annotations

import argparse
import fnmatch
import re
import subprocess
import sys
from pathlib import Path
from urllib.parse import unquote

ROOT = Path(__file__).resolve().parent.parent

# Where Markdown may live (fnmatch: `*` also crosses `/`). docs/README.md says what each home is for.
DOC_HOMES = [
    "README.md", "CLAUDE.md", "AGENTS.md", "CHANGELOG.md",
    ".claude/agents/*.md", ".claude/skills/*", ".agents/skills/*", ".github/*.md",
    "docs/README.md", "docs/codebase-map.md", "docs/decision-log.md",
    "docs/design/*", "docs/process/*.md", "docs/crew/*.md", "docs/moves/*.md", "docs/coursework/*.md",
    "docs/screenshots/*README.md",
    "assets/specs/*", "assets/shortlists/*.md",
    "harness/reports/*.md",
    "vault/*",
    "tools/*/runs/*/trace.md", "tools/blender/fixtures/*.md",
    "game/assets/*README.md",
]
STRAY_NAMES = re.compile(r"(proposal|todo|to-do|notes?-to-self|scratch)", re.I)
LINK = re.compile(r"!?\[[^\]]*\]\(\s*<?([^)\s>]+)>?(?:\s+\"[^\"]*\")?\s*\)")
FENCE = re.compile(r"^(```|~~~).*?^\1", re.M | re.S)
INLINE_CODE = re.compile(r"`[^`\n]*`")
AMENDMENTS = "docs/design/gdd-amendments.md"


def tracked_markdown(root: Path) -> list[str]:
    proc = subprocess.run(["git", "ls-files", "-z", "--", "*.md"], cwd=root, capture_output=True)
    if proc.returncode != 0:
        raise RuntimeError(proc.stderr.decode(errors="replace").strip() or "git ls-files failed")
    return sorted(p for p in proc.stdout.decode("utf-8").split("\0") if p)


def link_targets(text: str) -> list[str]:
    text = INLINE_CODE.sub("", FENCE.sub("", text))
    return [m.group(1) for m in LINK.finditer(text)]


def check_links(root: Path, path: str, text: str) -> list[str]:
    found = []
    for target in link_targets(text):
        if re.match(r"^[a-zA-Z][a-zA-Z0-9+.-]*:", target) or target.startswith("#"):
            continue
        rel = unquote(target.split("#", 1)[0].split("?", 1)[0])
        if not rel:
            continue
        dest = (root / rel.lstrip("/")) if rel.startswith("/") else (root / path).parent / rel
        if not dest.exists():
            found.append(f"links: {path}: '{target}' does not resolve")
    return found


def check_placement(path: str) -> list[str]:
    found = []
    if not any(fnmatch.fnmatchcase(path, home) for home in DOC_HOMES):
        found.append(f"placement: {path}: not in a known home (DOC_HOMES in tools/check_docs.py); "
                     "move it, or add the home and a docs/README.md row")
    if STRAY_NAMES.search(Path(path).name):
        found.append(f"proposals: {path}: open proposals and to-do lists live on their Linear ticket "
                     "(docs/process/change-tracking.md)")
    return found


def check_amendments(text: str) -> list[str]:
    headings = set(re.findall(r"^## (AM\d+)\b", text, flags=re.M))
    index = set(re.findall(r"^\|\s*\[?(AM\d+)\]?", text, flags=re.M))
    found = [f"amendments: {AMENDMENTS}: {am} has a heading but no index row" for am in sorted(headings - index)]
    found += [f"amendments: {AMENDMENTS}: {am} has an index row but no heading" for am in sorted(index - headings)]
    return found


def run(root: Path) -> list[str]:
    findings = []
    for path in tracked_markdown(root):
        file = root / path
        if not file.exists():  # deleted in the working tree, not yet staged
            continue
        text = file.read_text(encoding="utf-8", errors="replace")
        findings += check_placement(path)
        findings += check_links(root, path, text)
        if path == AMENDMENTS:
            findings += check_amendments(text)
    return findings


def main(argv: list[str] | None = None, out=sys.stdout) -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--root", type=Path, default=ROOT)
    args = ap.parse_args(argv)
    try:
        findings = run(args.root.resolve())
    except (OSError, RuntimeError) as e:
        print(f"docs check: {e}", file=out)
        return 2
    for f in findings:
        print(f, file=out)
    print(f"docs check: {len(findings)} finding(s)", file=out)
    return 1 if findings else 0


if __name__ == "__main__":
    sys.exit(main())
