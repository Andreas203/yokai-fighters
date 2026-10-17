"""Vault loader: each agent reads whole notes.

The knowledge base is the design vault (vault/*.md). There is no index and no
search. An agent's definition file (.claude/agents/<name>.md) lists the notes
it reads under a "## Vault notes" heading, and the pipeline hands the agent
those notes complete. The vault is small (13 notes, about 42 KB), so a whole
note costs little and nothing the text needs can be missed by a bad query.
"""
from __future__ import annotations

import re
import unicodedata
from dataclasses import dataclass
from pathlib import Path

# Undecided design is not canon, so it is cut from a note before an agent reads it.
SKIP_SECTIONS = {"open questions"}


@dataclass(frozen=True)
class Note:
    id: str        # "01-vibe/Tone.md"
    title: str     # note title from frontmatter
    source: str    # GDD sections the note is distilled from
    text: str      # the note body, without frontmatter or open questions


def _frontmatter(raw: str) -> tuple[dict, str]:
    if not raw.startswith("---"):
        return {}, raw
    end = raw.find("\n---", 3)
    if end == -1:
        return {}, raw
    meta = {}
    for line in raw[3:end].strip().splitlines():
        key, _, value = line.partition(":")
        meta[key.strip()] = value.strip()
    return meta, raw[end + 4:]


def load_note(vault_dir: Path, note_id: str) -> Note:
    path = vault_dir / note_id
    if not path.exists():
        raise SystemExit(f"No vault note '{note_id}'. Was it renamed?")
    meta, body = _frontmatter(path.read_text(encoding="utf-8"))
    # Split on level-2 headings; text before the first one is the note's lead.
    parts = re.split(r"^(## +.+)$", body, flags=re.MULTILINE)
    kept = [parts[0]]
    for i in range(1, len(parts), 2):
        if parts[i][2:].strip().lower() not in SKIP_SECTIONS:
            kept += [parts[i], parts[i + 1]]
    return Note(id=note_id, title=meta.get("title", path.stem), source=meta.get("source", ""),
                text="".join(kept).strip())


def agent_notes(agents_dir: Path, agent: str) -> list[str]:
    """The note ids listed under "## Vault notes" in the agent's definition file."""
    path = agents_dir / f"{agent}.md"
    if not path.exists():
        raise SystemExit(f"No agent definition at {path}")
    match = re.search(r"^## Vault notes\s*$(.*?)(?=^## |\Z)", path.read_text(encoding="utf-8"), flags=re.MULTILINE | re.DOTALL)
    notes = re.findall(r"^- `?vault/([^`\s]+\.md)`?", match.group(1), flags=re.MULTILINE) if match else []
    if not notes:
        raise SystemExit(f"{path} lists no notes under '## Vault notes'.")
    return notes


def load_agent_notes(vault_dir: Path, agents_dir: Path, agent: str) -> list[Note]:
    return [load_note(vault_dir, note_id) for note_id in agent_notes(agents_dir, agent)]


def words(text: str) -> list[str]:
    text = unicodedata.normalize("NFKD", text)
    text = "".join(c for c in text if not unicodedata.combining(c)).lower()
    return re.findall(r"[a-z0-9]+", text.replace("'", "").replace("’", ""))


def has_line(note: Note, line: str) -> bool:
    """True if the cited line's words appear in the note in the same order, unbroken.

    Comparing words, not characters, lets a table row be cited with or without its pipes.
    """
    needle, hay = words(line), words(note.text)
    if not needle:
        return False
    n = len(needle)
    return any(hay[i:i + n] == needle for i in range(len(hay) - n + 1))


def copied_run(text: str, note: Note) -> str:
    """The longest run of consecutive words in `text` that also appears, unbroken, in the note."""
    needle, hay = words(text), words(note.text)
    starts: dict[str, list[int]] = {}
    for i, w in enumerate(hay):
        starts.setdefault(w, []).append(i)
    best: list[str] = []
    for i, w in enumerate(needle):
        for j in starts.get(w, []):
            k = 0
            while i + k < len(needle) and j + k < len(hay) and needle[i + k] == hay[j + k]:
                k += 1
            if k > len(best):
                best = needle[i:i + k]
    return " ".join(best)


def table_row(note: Note, first_cell: str) -> str:
    """The table row whose first cell is `first_cell`, or an empty string."""
    for line in note.text.splitlines():
        if not line.startswith("|"):
            continue
        cells = [c.strip() for c in line.strip("|").split("|")]
        if cells and cells[0].replace("◆", "").strip().lower() == first_cell.lower():
            return line
    return ""


def ability_names(vault_dir: Path) -> set[str]:
    """Every special, evolution, modifier and cancel-rule name in Abilities.md."""
    body = (vault_dir / "02-mechanics" / "Abilities.md").read_text(encoding="utf-8")
    names: set[str] = set()
    section = ""
    for line in body.splitlines():
        if line.startswith("## "):
            section = line[3:].strip().lower()
            continue
        wanted = section == "specials" or section.endswith("modifiers") or section == "cancel rules"
        if not wanted or not line.startswith("|") or set(line) <= set("|- "):
            continue
        cells = [c.strip() for c in line.strip("|").split("|")]
        first = cells[0].replace("◆", "").strip()
        if first.lower() in {"special", "modifier", "cancel rule"}:
            continue
        names.add(first)
        if section == "specials" and ":" in cells[-1]:
            names.add(cells[-1].split(":")[0].strip())
    return names
