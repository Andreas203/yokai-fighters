# Yokai Fighters: Must-Ship Move List (YOK-14)

Source: `docs/design/rules.md` (rule IDs in brackets) and `Yokai_Fighters_GDD_Extended.pdf` sections 3.2, 4.2, 5.3, 10.2.
Companion: `docs/moves/clip_needs.md` (what each animation must show, length, rig risk).
No frame data is written here beyond the rules.md first-pass targets; real frame data comes after clip picks (F2).

Legend: `Demo` = needed by the YOK-39 Ryo-vs-Kitsune demo. Clip IDs refer to `clip_needs.md`.

## 1. Totals

| Group | Count | Notes |
|---|---|---|
| Ryo normals | 6 | C8 |
| Generic throw (+ throw break) | 1 + 1 reaction | C4 |
| Specials | 8 | 2 Ryo starters + 2 per yokai (A11, 10.2) |
| Lv 3 evolutions | 8 | All data-only, all reuse the base clip (A5) |
| Yokai normals | 3 yokai x 6 slots | Assumption, see section 5 |
| Tanuki boss moves | 0 new fighting moves | T0; 3 non-combat needs (merchant / reveal) |
| Shared reactions | 16 | Listed once, reused by every fighter |

Modifiers (14) and cancel rules (3) are data-only and need no clips (A10). Fox Step is "specials -> dash", so it reuses the dash clip (R04).

## 2. Ryo: normals and throw [C8, C4]

| Move | S/A/R (target) | Dmg | Clip ID | Demo |
|---|---|---|---|---|
| Light punch | 4/2/7 | 30 | N01 | Yes |
| Medium punch | 6/3/12 | 50 | N02 | Yes |
| Heavy punch | 10/4/20 | 80 | N03 | Yes |
| Light kick | 5/2/9 | 30 | N04 | Yes |
| Medium kick | 7/3/14 | 55 | N05 | Yes |
| Heavy kick | 12/4/22 | 90, knockdown | N06 | Yes |
| Generic throw | 5 startup, 7-frame break window | 120 | T01 (attacker) + R11 (defender) | Yes |
| Throw break | press throw within 7 frames | 0 | T02 | Yes |

## 3. Specials [A1, A11, section 4.2 table in rules.md]

| Special | Source / rarity | S/A/R | Dmg | Clip ID | Lv 3 evolution (clip reuse) | Demo |
|---|---|---|---|---|---|---|
| Spirit Wave | Ryo starter | 13/-/30 | 60 | S01 | Great Wave (E01, reuses S01) | Yes |
| Rising Talisman | Ryo starter | 5/8/28 | 90 | S02 | Heaven Seal (E02, reuses S02) | Yes |
| Foxfire | Kitsune, common | 15/4/30 | 70 | S03 | Piercing Foxfire (E03, reuses S03) | Yes (Ryo drafts it; Kitsune casts it) |
| Fox Mirage (locked) | Kitsune, rare | 18/-/10 | 0 | S04 | Mirage Feint (E04, reuses S04) | No |
| Iron-Club Charge | Oni, common | 16/6/24 | 110 | S05 | Crushing Charge (E05, reuses S05) | No |
| Oni Quake (locked) | Oni, rare | 22/5/26 | 90, hits low | S06 | Aftershock (E06, reuses S06) | No |
| River Grab | Kappa, common | 6/2/30 | 130 | T01 (reuses generic grab) | Whirlpool Grab (E07, reuses T01) | No |
| Shell Spin (locked) | Kappa, rare | 10/18/20 | 80 | S07 | Torrent Spin (E08, reuses S07) | No |

Source fidelity [A8]: each special belongs only to its source yokai (or Ryo). The EX version of any special reuses the base clip with a preset [A10 spirit; no new animation].

## 4. Lv 3 evolutions [A5]

Every evolution is a property change plus a visual preset on the base clip. None needs a new clip.

| ID | Evolution | Base | Property change | Preset need (for Clip Matcher) |
|---|---|---|---|---|
| E01 | Great Wave | Spirit Wave | Absorbs one projectile | Larger, brighter wave projectile; absorb flash |
| E02 | Heaven Seal | Rising Talisman | Launches for follow-up | Gold seal trail. Needs an air-hit result: reuse knockdown R11 (see risk notes) |
| E03 | Piercing Foxfire | Foxfire | Passes through projectiles | Elongated, white-hot flame |
| E04 | Mirage Feint | Fox Mirage | Decoy absorbs one hit | Translucent decoy copy of the model left in place (a frozen idle pose, not a clip) |
| E05 | Crushing Charge | Iron-Club Charge | Wall bounce | Heavier dust trail. Needs a wall-bounce result: reuse knockdown R11 / hit-high R09 |
| E06 | Aftershock | Oni Quake | Second quake 30f later | Second ground-crack VFX; the slam clip plays once, no second clip |
| E07 | Whirlpool Grab | River Grab | +50% range | Teal water swirl around the hand; range is a data change |
| E08 | Torrent Spin | Shell Spin | Travels forward, multi-hit | Water ribbon trail; forward travel is root motion in data, so the clip must be an in-place spin |

## 5. Yokai normals (ASSUMPTION, needs designer confirmation)

The GDD and rules.md define normals for Ryo only (C8). They give no per-yokai normal list. Kitsune, Oni and Kappa are fought by the AI (Y1) and need attack animations, so this list proposes the following, which adds no new moves:

- Each yokai has the same six normal slots as Ryo (N01-N06 slots), played from the same retargeted humanoid clips. One purchase, four retargets.
- Frame data for yokai normals is a Movesmith ticket after clip picks; this list does not set numbers.
- The AI may use only a subset per temperament (a Habit Writer concern).
- Yokai hit sparks use their source colour [V5].

| Fighter | Normals | Clip IDs | Demo |
|---|---|---|---|
| Kitsune | LP, MP, HP, LK, MK, HK | N01-N06 (shared) | Yes |
| Oni | LP, MP, HP, LK, MK, HK | N01-N06 (shared) | No |
| Kappa | LP, MP, HP, LK, MK, HK | N01-N06 (shared) | No |

Open question for the designer: should yokai have all six normals, or a reduced set (for example LP, MP, HP, HK) to shrink the retarget test surface for G4?

## 6. Tanuki boss [T0, T4, 5.3]

The Tanuki fights as Ryo: Ryo's model, Ryo's normals N01-N06, Ryo's throw, the starters S01/S02, plus one copied special that plays the copied move's existing clip (S01-S07 or T01). **No new combat animation.** Copied specials must not depend on paired animation (C4, T), which holds because every clip here is single-fighter.

Extra non-combat needs, not part of the fight (clip IDs B01-B03 in `clip_needs.md`):

| ID | Need | Where |
|---|---|---|
| B01 | Merchant model idle/offer loop | Row 4 and row 7 merchant node, rest stop |
| B02 | Disguise-drop reveal | Tanuki node, with slow camera push-in [V7] |
| B03 | Shapeshift into Ryo | Fight intro; model swap, no clip |

## 7. Shared reactions (listed once, reused by all fighters)

R01 idle, R02 walk forward, R03 walk back, R04 dash, R05 jump, R06 crouch, R07 block high, R08 block low, R09 hit high, R10 hit low, R11 knockdown, R12 wake-up, R13 KO, R14 win pose, R15 binding (round-ending blow), R16 burst. Details in `clip_needs.md`.

## 8. YOK-39 demo set (Ryo vs Kitsune)

Needed: N01-N06; T01, T02; S01, S02; S03 (Foxfire, Ryo drafted and Kitsune AI); all of R01-R16 for both Ryo and Kitsune. Everything else (S04-S07, E01-E08, Oni, Kappa, B01-B03) is not needed for the demo.

## 9. Rules relied on

P1, C1-C8, K3, F1-F5, X4, A1, A4, A5, A8, A10, A11, T0, T4, V4, V5, V7, G4.
