---
title: Conventions
status: draft
source: YOK-1
updated: 2026-10-10
---

# How vault notes are written

## Frontmatter

Every note starts with four fields.

| Field | Value |
|---|---|
| `title` | The note's name, same as the file name |
| `status` | `draft` (written, not reviewed), `review` (designer is reading it) or `locked` (designer approved) |
| `source` | The GDD sections, amendments or tickets the note is distilled from |
| `updated` | Date of the last edit, `YYYY-MM-DD` |

## Writing rules

- One topic per note. If a note needs a second topic, it becomes a second note and a link.
- Numbers go in tables, copied exactly from the GDD or its amendments.
- Rules are written as statements someone can check, with the `rules.md` ID in brackets where one exists, for example (S2).
- Each `##` section should make sense read alone. The content pipeline retrieves one section at a time, so a section names its subject instead of saying "it" or "the above".
- Examples beat adjectives. A tone note quotes lines, an art note names colours.
- Open questions go at the bottom of the note. Nobody invents an answer to one; the designer decides.

## Links

- Between notes: `[[Note]]` or `[[Note#Section]]` wikilinks, which Obsidian resolves by file name.
- `Home.md` uses ordinary Markdown links as well, so the vault can be browsed on GitHub.

## Conflicts

- An amendment in `docs/design/gdd-amendments.md` wins over the GDD section it names.
- The GDD plus amendments win over a vault note until that note is `locked`.
- A note that disagrees with its source gets fixed, not argued with.
