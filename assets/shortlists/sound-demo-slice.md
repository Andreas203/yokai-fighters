# Sound shortlist: YOK-39 vertical-slice demo (YOK-54)

Scope: Ryo vs Kitsune, bamboo grove at dusk. Needs: hits, blocks, specials (Spirit Wave, Rising Talisman, Foxfire), throws, KO, one stage ambience/music loop. Sound is sourced (AM1). The designer decides and buys; nothing here is purchased. Screen shake is heavy/EX only (V3), so heavy impact and EX cues need a punchy "heavy" layer; lights stay unshaken but still need a clean light thud.

Prices checked 2026-10-05 from the store pages. itch.io prices are "or more" minimums and sales change them. **Licence caveat:** no store page names student capstone or public showcase use. All are "royalty-free / commercial use OK", which covers both in practice, but the designer should confirm with the seller for packs marked "unverified".

## Candidate packs

| # | Pack | Store / URL | Price | Licence (from page) | Format | Cues covered | Gaps |
|---|---|---|---|---|---|---|---|
| A | Karate Sound Effects (Gravity Sound), 149 files | [itch.io](https://gravity-sound.itch.io/karate-sound-effects) | $9.99 | "Royalty free"; commercial game use; no AI. Student/showcase not stated (unverified) | WAV 44.1 kHz 16-bit; imports directly in Godot | Hits (punch/kick/deep/flesh), whooshes, voices (attack/hurt) | No named block, throw or KO files on page (full list is on a Drive link, not checked). No specials |
| B | Magic Spell Sound Effects Pack Vol 1 (Placeholder Assets), 300 files | [itch.io](https://placeholder-assets.itch.io/magic-spell-sound-effects-pack-vol-1) | $11.99 (sale from $19.99) | "100% copyright-free", commercial indie use; no AI | WAV, 90 MB zip; Godot OK. Sample rate not stated | Fire (Foxfire), Arcane (Rising Talisman, Spirit Wave), projectiles, casts, loops, impacts | Not Japanese-flavoured; no paper/ink sounds |
| C | Spring Forest Sounds (Gravity Sound), 16 x 1 min | [itch.io](https://gravity-sound.itch.io/spring-forest-sounds) | $9.99 | "Royalty free" | WAV 44.1 kHz 16-bit | Night/day wind, crickets, trees; stage ambience bed | No bamboo-specific sound; loop points not confirmed (may need crossfade in Godot) |
| D | Japanese Style Game Music Collection (Airy / WOW Sound), 21 tracks | [itch.io](https://airyluvs.itch.io/japanese-style-game-music-collection), same pack on [WOW Sound](https://wowsound.com/p/japanese-style-game-music-pack/) | $49 | "Royalty-free"; WOW Sound page lists a Standard tier for video games under $100K revenue, lifetime licence; no attribution stated | WAV 44.1 kHz 16-bit, **OGG seamless loops**, AAC | Shakuhachi, koto, shamisen; one dusk fight loop | Single mixes, not stems (no intensity layering, V9); taiko unconfirmed; no merchant melody |
| E | Anime Epic Combat SFX Pack (WOW Sound), 390 files | [itch.io](https://wowsound.itch.io/anime-epic-combat-sfx-pack) | $79 (educational discount offered, amount unstated) | WOW Sound game licence, commercial OK; attribution required; no AI | WAV 48 kHz 24-bit stereo | Hits (106), skills (24), stun (11), whooshes (14), booms/stingers (95), epic (126) | Pricey for a demo; no ambience; no named throw/KO |
| F | Samurai Soundtrack Vol 1 (Bobby Cole), 25 tracks | [itch.io](https://bobbycolemusic.itch.io/samurai-soundtrack-25-tracks-vol-1) | $24.99 | "Unlimited royalty-free licence", commercial and personal | WAV 44.1 kHz 16-bit; **not loopable** | Feudal Japanese music, 1.5 h | No loops (trim or crossfade); no stems |
| G | Punching Whoosh and Impact (floraphonic), 50 files | [itch.io](https://floraphonic.itch.io/punching-whoosh-and-impact-sound-effects) | $5 | Terms shown only on purchase (unverified); no AI | ZIP 3.9 MB, format not stated | 20 impacts light tap to heavy thud, 20 whooshes | Small; licence unseen |
| H | Punch And Smash (Raw Ambience), 110 files | [itch.io](https://rawambience.itch.io/punch-and-smash-sound-effects) | $13.99 | Not stated (unverified) | 44.1 kHz 16-bit | Punches, hits, heavy impacts, swings | Licence unclear; gore elements unwanted |

## Recommended set: A + B + C + D

| Pack | Price |
|---|---|
| A Karate Sound Effects | $9.99 |
| B Magic Spell Vol 1 | $11.99 |
| C Spring Forest Sounds | $9.99 |
| D Japanese Style Game Music Collection | $49.00 |
| **Total** | **$80.97** |

Why: the smallest set that touches every demo cue. A gives impact weights and whooshes for under $10. B covers all three specials in one pack. C is the cheapest night ambience. D is the only music with verified seamless OGG loops, which drop into Godot with loop on.

Cheaper music variant: swap D for F, total **$56.96**, but F tracks do not loop and need a trim/crossfade.

## Backup set: E + B + C + F

| Pack | Price |
|---|---|
| E Anime Epic Combat | $79.00 |
| B Magic Spell Vol 1 | $11.99 |
| C Spring Forest Sounds | $9.99 |
| F Samurai Soundtrack Vol 1 | $24.99 |
| **Total** | **$125.97** |

Use if A's hits sound thin at heavy/EX level, or its file list shows no blocks/throws/KO. E has a documented game licence, 48 kHz 24-bit audio and a stun category. Cheap hit-only fallback: add G ($5) to A before escalating to E.

## Coverage (recommended set)

| Need | Source | Status |
|---|---|---|
| Light / medium / heavy hit (V6) | A (hits, deep hits) | Covered; check heavy weight |
| Block | A (flesh hit, muted impacts) | Probable, unverified until file list read |
| Throw | A hit + whoosh layered, or B wind impact | Layered, no dedicated cue |
| KO / knockdown | A deep hit + B impact | Layered, no dedicated cue |
| Startup whooshes | A | Covered |
| Spirit Wave (projectile) | B arcane/wind projectile and loop | Covered |
| Rising Talisman | B arcane cast + upward whoosh | Covered |
| Foxfire | B fire category | Covered |
| Kitsune / Ryo stings (V6) | none dedicated | Gap (bells, paper, ink) |
| Stage ambience (dusk grove) | C night tracks | Covered, not bamboo-specific |
| Fight music loop | D (OGG loops) | Covered, single mix |
| UI paper/seal sounds, ink-stroke binding (V4) | none | Gap, outside demo scope |

## Open questions for the designer
1. Is about $81 acceptable? Sound spend is still an open question.
2. Ask Gravity Sound and Placeholder Assets to confirm student/showcase use in writing, and ask Gravity Sound for the Drive file list (block, throw, KO names).
3. Stingers, UI sounds, taiko and stems are deferred past the demo.
