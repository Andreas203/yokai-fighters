# Font specimens (YOK-71, ROUGH, docs only)

The designer asked to see different heading and body font choices for the paper-talisman style (`assets/specs/style.md`: paper, brush strokes, red seals, muted ukiyo-e palette). This is a choice sheet, not a decision.

| File | What it is |
|---|---|
| `font-specimens.png` / `.svg` | Sheet 1, 1920x1080: pairings 1 to 3 (free Google Fonts) |
| `font-specimens-2.png` / `.svg` | Sheet 2, 1920x1080: pairing 4 (licensed brush, stand-in), pairing 5 (system fallback), plus one more free pair |
| `font-specimens-720p.png` / `-720p.svg` | Sheet 3, 1280x720: all five pairings at the sizes a 720p screen would use (1080p sizes x 2/3) |
| `gen_font_specimens.py` | Source for the SVGs |

The committed SVGs load their fonts from Google Fonts (`@import`), so they render when opened online. The PNGs were rendered from a copy that loads the TTFs from a scratch folder (`--local DIR`). No font files are committed and nothing was bought. Fonts were fetched from the Google Fonts repository (`github.com/google/fonts`, folder `ofl`).

Every pairing renders: "SHOP" (92 px), "REST STOP" (52 px), "Foxfire Lv 2 · Tanuki target" (26 px heading font and 16 px body font), a reward card (name 30 px, plain line 16 px), a merchant bubble line (20 px), a key-chip row (18 and 20 px), and on the 720p sheet the smallest sizes in use (14, 15 and 13 px at 1080p).

## Pairings

| # | Heading + body | Licence | Latin glyphs | File size |
|---|---|---|---|---|
| 1 | Yuji Syuku + Shippori Mincho | OFL 1.1, free to ship | ASCII 95/95, Latin-1 96/96, dashes, quotes, dot, times | 8.4 + 8.7 MB full; Latin-only subset 237 + 107 KB |
| 2 | Yuji Boku + Zen Kaku Gothic New | OFL 1.1, free to ship | same, plus arrows | 8.5 + 2.4 MB full; Latin subset 274 + 28 KB |
| 3 | Kaisei Decol Bold + Noto Serif JP | OFL 1.1, free to ship | same, plus arrows | 4.3 + 13.6 MB (variable) full; Latin subset 185 + 192 KB |
| 4 | Licensed brush face (not rendered; stand-in Yuji Boku + Zen Old Mincho) | Commercial: e.g. a Fontworks or Morisawa brush family; game embedding licence, price on request, not bought, not verified | varies by family and must be checked before buying | unknown |
| 5 | System fallback: Georgia (then Yu Mincho / Times) | OS-bundled, nothing to ship; not guaranteed on every PC | full Latin; Japanese falls back to whatever the OS has | 0 MB |
| also | Zen Old Mincho Bold + Sawarabi Mincho | OFL 1.1 | ASCII full; Sawarabi lacks en/em dash and arrows (use a hyphen) | 5.4 + 3.7 MB full; Latin subset 39 + 35 KB |

Glyph and size figures were measured on the downloaded TTFs (fontTools); "Latin-only subset" is the same font cut to Basic Latin, Latin-1, dashes, quotes, dot and times. All the free families also cover kana and 3.3k to 13.6k kanji, which matters for the story cards.

## What the sheets show

- **Yuji Boku (pairing 2) is the most brush-like but its Latin capitals are idiosyncratic**: the "O" in "STOP" and "SHOP" reads as a curl and the "P" is open. Fine for a seal or a single decorative word, risky for headings players must read mid-fight. At 720p "REST STOP" is the weakest line of any pairing.
- **Yuji Syuku (pairing 1) keeps brush character with readable capitals** and holds up at 720p. Its body partner Shippori Mincho is readable at 16 px and still readable at 11 px on the 720p sheet.
- **Pairing 3 is the most legible and has the widest kanji coverage**, but it looks printed rather than brushed, and Noto Serif JP is the largest download (13.6 MB, 192 KB if subset to Latin).
- **The sans body (Zen Kaku Gothic New) is the crispest at the smallest sizes** (14 px slot line, 12 px slip text), which suits hitstop and screen shake. It is the one pairing where body and heading have clearly different voices.
- **The system fallback is legible everywhere but has no brush character** and changes per machine; keep it only as the last fallback in a font stack.
- **The licensed option is only worth buying if the free brush faces fall short.** I rendered only a stand-in, so I can't say whether a bought family would do better at 720p. The designer owns that purchase.
- Specimen text is upright. None of these families ships an italic, so the game's merchant lines would be upright; the italic in the mockups is the browser's fake slant.

## Recommendation

Pairing 1, **Yuji Syuku for headings (SHOP, REST STOP, card names, Lv labels) with Shippori Mincho for body text** (plain lines, bubbles, slips). It is free under the OFL, it is the best match to the brush-and-paper look without hurting legibility, and the Latin-only subsets are small (237 + 107 KB) if the build wants them. Use Zen Kaku Gothic New (pairing 2's body) for the smallest text: the slot sub-lines, frame-data slips and key chips, where a sans reads best under hitstop and shake. Keep Georgia as the final fallback. Reserve Yuji Boku for a single decorative word if the designer wants a rougher brush.

Do not buy a licensed brush font yet. Revisit only if the designer sees Yuji Syuku in the finished Astra concept and wants more weight.

## Open questions

1. Does the designer want to see the licensed option rendered for real? That needs a font purchase or a trial licence, which only the designer can approve.
2. The sheets use Latin text only. Should a second pass render Japanese story-card text in the shortlisted pairing?
3. Should the HUD and the cards use one body font, or the sans for the smallest text as recommended above?
