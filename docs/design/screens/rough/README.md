# Rough mockups: rest stop and merchant shop (YOK-100)

**ROUGH PROTOTYPES, AWAITING AN ASTRA PASS.** Greybox layouts only: flat shapes, real labels, placeholder boxes marked for art. They show where things sit, what they say and how the player moves through them. They are not finished art and not texture sources. Nothing here was generated with an image or 3D tool; each PNG is rendered from the SVG beside it (headless Chrome, 1920x1080). Edit the SVG, re-render, or hand the layout to Astra.

| Mockup | PNG | Source |
|---|---|---|
| Rest stop, merchant node (row 4) | `rest-stop.png` | `rest-stop.svg` |
| Rest stop, dojo node (row 7), extra variant | `rest-stop-dojo.png` | `rest-stop-dojo.svg` |
| Merchant shop | `merchant-shop.png` | `merchant-shop.svg` |

Layout language borrowed from `docs/design/screens/reward.png`: torn rice-paper sheet over the night backdrop, brush-italic heading with a red underline stroke, rice-paper talisman cards with an icon square, name, source line and a tag strip, persimmon frame on the selected card, key-chip footer. Palette from `assets/specs/style.md`. The Tanuki is never drawn: the forecast is text and a move icon only.

Numbers in the images are plausible placeholders, not tuned values. Ryo at 640 / 1000, 75 coins (rows 1 to 3 pay 25 each, R8).

## 1. Rest stop (`rest-stop.png`, `rest-stop-dojo.png`)

One screen shell, two node variants chosen by the map (R1: row 4 always merchant, row 7 merchant or dojo). Practice mode reuses the dojo variant (no new screen).

### Elements

| Element | Shows | Rule | Binds to |
|---|---|---|---|
| Title + row line | "REST STOP", "Row 4 · the merchant's lantern" or "Row 7 · the dojo at the bend" | R1 | run state: current row, node type |
| Health bar | "RYO · 640 / 1000", red fill | C1 | Ryo current / max health |
| Coins chip | Coin balance | R8 | run coin total |
| Rough tape | "ROUGH PROTOTYPE" banner, remove in the final | n/a | none |
| Portrait box | Merchant (human guise) or dojo master portrait, placeholder | R6, R7, S3 | node type |
| Line box | One or two sentences from the character | S3 | story data (habit-writer) |
| Option card 1, merchant: "Free heal" | Tag FREE, "Restores 25% of max health: +250 -> 890 / 1000", health preview bar with the gain in persimmon | R6 | heal amount = 25% of max, clamped at max |
| Option card 2, merchant: "Browse the shop" | Tag SHOP, prices 50 / 40, current coins; opens the shop | R6 | coin total, shop offer |
| Option card 1, dojo: "Rest" | Tag REST, "+300 -> 940 / 1000" with preview bar | R7 | 30% of max, clamped |
| Option card 2, dojo: "Training trial" | Tag TRIAL, heals 15% either way, levels the special if landed within 3 attempts, attempts pips, special selector | R7, A4 | trial data for the chosen special, attempts used |
| "Pick one" line | Heal or shop / rest or train, one choice per stop | R6, R7, P3 | stop-used flag |
| Tanuki forecast panel | Red "TANUKI FORECAST" strip; glowing move icon; "Will copy: Foxfire"; level chip; "Copied at your level, never your modifier or evolution"; "Expected cost to you ~190 health" with a 0 to 1000 bar and a one-line meaning; lock note | T6, T2, T3, T4, T1, Y3-style callout | the copy-target query (highest-level special in a slot, ties by most recent level then lower slot); that move's level; expected-cost figure from the run's own fight record |
| Dojo-only forecast line | "Training Foxfire to Lv 3 raises this copy" | T2, A4 | same query, shown only when the selected trial's special is the target |
| Footer key chips | Choose, Take, Forecast detail, Map | A12 style | input map |

The glowing icon in the forecast must use the same icon and glow as the HUD copy-target talisman, so the player recognises it (T6).

### Input and navigation

- Keyboard: Up / Down (or W / S) moves between the two option cards; Enter takes the highlighted option; Tab opens forecast detail; Esc goes back to the map only if no choice was made yet. In the dojo trial card, Left / Right (A / D) changes the special.
- Pad: D-pad or left stick up/down moves, A takes, X opens forecast detail, B backs out, left/right bumper or D-pad left/right changes the trial's special.
- Default focus: the free heal / rest card (the cheaper, safer choice).
- After "Browse the shop": shop screen opens; Esc there returns here with the stop still unused. After "Free heal", "Rest" or a finished trial: show the result, then continue to the next row.

### States

- Fresh: both options enabled, heal preview shown.
- Already full health: heal/rest still takeable (it costs the stop) but preview reads "+0", and the card carries a note. See open questions.
- Heal clamps: preview shows only the health that fits under 1000.
- Choice made / stop used: option cards dim, "Continue" replaces the footer Take.
- Shop unavailable (no shop stock, not enough coins for anything): card stays enabled and shows "Nothing to buy", or is dimmed with the reason. See open questions.
- Trial: attempts 0 to 3 pips; success ("Foxfire levelled") and miss states; heal 15% applied either way.
- Special has no trial authored: selector skips it; if none, trial card disabled with a reason.
- Forecast with no specials beyond starters: still shows a target (T5, always defined). Forecast with ties follows T3.
- Forecast before data exists (first rest stop): show target and level, with "Expected cost: not enough fights yet" in place of a number. See open questions.

## 2. Merchant shop (`merchant-shop.png`)

### Elements

| Element | Shows | Rule | Binds to |
|---|---|---|---|
| Title, health, coins | As above | C1, R8 | run state |
| Merchant face + line | Small portrait and a line | S3 | story data |
| Section heading | "ABILITIES · 50 coins · common · from yokai you have not bound (Kappa)" | R6 | unbound-yokai list for this run |
| Ability cards (3 shown) | Icon, name, source and type ("Kappa special · throw", "Kappa modifier"), tag (NEW · Lv 1 or MODIFIER), art box, plain-language line, red price seal 50, "grey borrowed ink, run only" note | R6, A7, A8, A12 | shop offer list; each card's `card` block text |
| Modifier line | "Pick the special to carry it" | E20, A2 | modifier-target step |
| Slots panel | "YOUR SLOTS", four slots with icon, name, level and a "Release 40" button; one empty slot; Tanuki-target slot framed in foxfire orange | A1, A2, A6, R6, T2 | Ryo's slot list, levels, copy-target query |
| Release note | "Releasing frees a slot; the move loses its levels" | A6 | none |
| Target note | "Foxfire is the Tanuki's target (T2); release moves it" | T2, T6 | copy-target query |
| Locked state | "LOCKED · last special", button replaced | T5 | count of specials in slots |
| States strip | Mini examples of cannot afford, bought (grey borrowed ink), shelf empty; remove in the final | n/a | n/a |
| Footer | Move, Buy / Release, Back to rest stop | n/a | input map |

Bought abilities render grey (R6 "borrowed ink") and are never offered as carry-over (M3 does not apply). The shop never lists rares or cancel rules (R6, A9, X1).

### Input and navigation

- Keyboard: Left / Right moves across ability cards; from the last card Right (or Tab) enters the slots panel, Up / Down moves between slots, Left returns to the cards. Enter buys the focused card or releases the focused slot. Esc returns to the rest stop.
- Pad: D-pad / stick to move, A buy or release, B back.
- Buying a special with no empty slot: prompt to pick a slot to replace, or tell the player to release one first. Buying a modifier opens the same target step as the reward screen's modifier card.
- Buy and release both confirm with a short "Buy River Grab for 50?" step. See open questions.
- Default focus: first affordable card.

### States

- Affordable: normal card, price seal red.
- Cannot afford: card dims, seal grey, text "Need 50 · you have 25". Enter does nothing and shakes the coin chip; nothing changes.
- Bought: card turns grey borrowed ink, tag "BOUGHT", not selectable again.
- No free slot (special bought): card remains buyable only after a release, or opens replace prompt.
- Shelf empty: all yokai bound or all items bought; text "Nothing left to sell".
- Release affordable / not: Release 40 disabled when coins under 40.
- Last special (T5): the button becomes "LOCKED · last special"; Enter does nothing.
- Empty slot: dashed slot, not releasable.
- Tanuki-target slot: orange frame plus warning line.

## Open questions for the designer

1. **Choice or two sections?** GDD p.10 calls the rest stop "merchant or dojo choice". R1 fixes row 4 as merchant and row 7 as merchant or dojo, so I drew one shell with two variants. Does a row-7 stop ever offer both merchant and dojo together?
2. **Heal and shop together?** R6 says heal "or" shop. I assumed one choice per stop. Can a player take the free heal and then shop, or shop and then still heal?
3. **Heal base.** I read "25% / 30% / 15%" as a share of max health (250 / 300 / 150). Confirm it is not a share of missing health.
4. **Expected cost.** The GDD says "its expected cost from the player's own fights" and the rules give no formula. I drew "~190 health, average health you lose per fight to this move". Is that the intended meaning, or is it the damage the copied move deals per hit, or a share of Ryo's health? What does the first rest stop show before any fight data exists?
5. **Leave without choosing.** Is skipping a stop allowed? Drawn as a ghost button; delete if not.
6. **What can the shop sell?** R6 says "common abilities from yokai not bound this run". I included modifiers as well as specials. Are modifiers sold, and how many cards does the shelf show (I drew three)?
7. **Release a binding.** I drew it as releasing a special from a slot (the move loses its levels, A6) so a slot frees up. The GDD wording is "release a binding to free a slot". Does release remove the special, the yokai binding, or both? Does it refund anything?
8. **Slot full on buy.** Buying a special with all four slots full: replace flow or forced release first?
9. **Dojo trial selection.** Who picks the special for the trial: the player (drawn as a selector) or the seed? Samples in the GDD pair each trial with one special.
10. **Forecast on the shop screen.** Not required by T6; I only marked the Tanuki-target slot there. Should the shop show the forecast panel as well?
11. **Heal at full health.** Block the option, allow it with "+0", or hide it?
12. **Elder twist reminder (Y3).** Not placed here since it is a pre-fight element. Say so if the rest stop should preview the next node's twist.
13. **Merchant portrait.** Merchant is the Tanuki in disguise (S3). The portrait box is for the human guise only; the Tanuki is never shown on these screens.
