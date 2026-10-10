"""Vault loader and BM25 retriever.

The knowledge base is the design vault (vault/*.md). Each `##` section of a note
becomes one chunk, so a retrieved chunk is something a person could have linked
to: `01-vibe/Tone.md#Voice: Ryo`. Retrieval is plain BM25 over those chunks; the
vault is small and its vocabulary is proper nouns (Kappa, Foxfire, talisman), so
exact-term scoring finds the right section without an embedding model.
"""
from __future__ import annotations

import math
import re
import unicodedata
from dataclasses import dataclass
from pathlib import Path

# Not design content: the index page and the note-writing guide.
SKIP_FILES = {"Home.md", "_conventions.md"}
# Undecided design is not canon, so it is never handed to the generator.
SKIP_SECTIONS = {"open questions"}

STOPWORDS = set(
    "a an and are as at be but by can for from has have how if in into is it its "
    "of on one or so than that the their then there these this to was what when "
    "which who will with you your not no does do each every".split()
)


@dataclass(frozen=True)
class Chunk:
    id: str        # "01-vibe/Tone.md#Voice: Ryo"
    note: str      # "01-vibe/Tone.md"
    title: str     # note title from frontmatter
    heading: str   # section heading
    source: str    # GDD sections the note is distilled from
    text: str      # section body


def tokenize(text: str) -> list[str]:
    text = unicodedata.normalize("NFKD", text)
    text = "".join(c for c in text if not unicodedata.combining(c)).lower()
    text = text.replace("'", "").replace("’", "")
    tokens = re.findall(r"[a-z0-9]+", text)
    out = []
    for t in tokens:
        if t in STOPWORDS:
            continue
        # Light plural folding so "talismans" finds "talisman".
        if len(t) > 3 and t.endswith("s") and not t.endswith("ss"):
            t = t[:-1]
        out.append(t)
    return out


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


def load_vault(vault_dir: Path) -> list[Chunk]:
    chunks: list[Chunk] = []
    for path in sorted(vault_dir.rglob("*.md")):
        if path.name in SKIP_FILES:
            continue
        note = path.relative_to(vault_dir).as_posix()
        meta, body = _frontmatter(path.read_text(encoding="utf-8"))
        # Split on level-2 headings; text before the first one is the note's lead.
        parts = re.split(r"^## +(.+)$", body, flags=re.MULTILINE)
        for i in range(1, len(parts), 2):
            heading, text = parts[i].strip(), parts[i + 1].strip()
            if heading.lower() in SKIP_SECTIONS or not text:
                continue
            chunks.append(Chunk(
                id=f"{note}#{heading}", note=note, title=meta.get("title", path.stem),
                heading=heading, source=meta.get("source", ""), text=text,
            ))
    if not chunks:
        raise SystemExit(f"No vault chunks found under {vault_dir}")
    return chunks


class VaultIndex:
    """BM25 (Okapi) over vault chunks."""

    K1 = 1.5
    B = 0.75

    def __init__(self, chunks: list[Chunk]):
        self.chunks = chunks
        self.by_id = {c.id: c for c in chunks}
        # The note title and section heading are indexed with the body.
        self.docs = [tokenize(f"{c.title} {c.heading} {c.text}") for c in chunks]
        self.avg_len = sum(len(d) for d in self.docs) / len(self.docs)
        df: dict[str, int] = {}
        for doc in self.docs:
            for term in set(doc):
                df[term] = df.get(term, 0) + 1
        n = len(self.docs)
        self.idf = {t: math.log(1 + (n - f + 0.5) / (f + 0.5)) for t, f in df.items()}

    def search(self, query: str, k: int = 4, scope: str | None = None) -> list[tuple[Chunk, float]]:
        """Top-k chunks for a query. `scope` limits the search to chunk ids starting with it."""
        terms = tokenize(query)
        scored = []
        for chunk, doc in zip(self.chunks, self.docs):
            if scope and not chunk.id.startswith(scope):
                continue
            score = 0.0
            for term in terms:
                tf = doc.count(term)
                if not tf:
                    continue
                norm = tf * (self.K1 + 1) / (tf + self.K1 * (1 - self.B + self.B * len(doc) / self.avg_len))
                score += self.idf[term] * norm
            if score > 0:
                scored.append((chunk, round(score, 3)))
        scored.sort(key=lambda pair: (-pair[1], pair[0].id))
        return scored[:k]

    def get(self, chunk_id: str) -> Chunk:
        if chunk_id not in self.by_id:
            raise KeyError(f"No vault chunk '{chunk_id}'. Did a heading change?")
        return self.by_id[chunk_id]


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
