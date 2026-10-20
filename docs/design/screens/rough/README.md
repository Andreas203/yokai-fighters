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

## 2. Merchant shop (`merchant-shop.png`, `merchant-shop-states.png`) — revision 3 (YOK-159, shelf and speech bubble)

Source: `gen_merchant_shop.py` (writes both SVGs; render at 1920x1080 with headless Chrome or Edge). Designer direction: the merchant speaks in a bubble, choices stand on a proper shelf two per shelf, and the description comes on hover or focus. Decision (from the designer, via the coordinator): the description is spoken by the merchant in the bubble; there is no separate tooltip or paper tag.

### Elements

| Element | Shows | Rule | Binds to |
|---|---|---|---|
| Title, health, coins | "SHOP", "RYO · 640 / 1000", coin chip | C1, R8 | run state |
| Merchant portrait | Placeholder box for the human guise; never the Tanuki | S3 | art only |
| Speech bubble | Brush-ink bubble, tail to the portrait. Sized for the longest card text (4 lines, drawn with Slippery Skin). Replaces the old "Merchant line" box | S3 | merchant lines (story data) plus the focused offer's `card.plain` |
| Shelf unit | Wooden unit, 3 shelves, 2 items per shelf; 5 Kappa items as 2 / 2 / 1. Specials first, then modifiers | R6 (shelf size open) | shop offer list, ordered specials then modifiers |
| Shelf item | Standing ofuda on a stand: icon (letter-square until YOK-80/81), type mark SPECIAL or MODIFIER, name and price (50 + coin) on a tag hung on a string. No description text on the shelf | R6, A8, A12 | offer: icon, name, type, price |
| Focus state | Focused item lifts 12 px with a foxfire frame and glow | T6 style, Kihon | focus index |
| Borrowed ink note | Once, under the shelf heading: "Bought abilities are borrowed ink: grey, this run only." The bubble does not repeat it | R6 | static |
| Heading | "ABILITIES · 50 coins · common · from yokai you have not bound (Kappa)" | R6 | unbound-yokai list |
| Slots panel | Hanging scroll "YOUR SLOTS": slots 1 to 4, Release 40 chip on the focused slot only, Tanuki-target frame (foxfire), tied cord and seal for the T5 lock (states sheet) | A1, A2, A6, R6, T2, T5 | slot list, levels, copy-target query |
| Footer | Chips: Move, Buy / Release, Back to rest stop (one back action) | n/a | input map |

Bubble focus line: a short lead-in in his voice, then the card's plain text: "Ah, River Grab. Command grab. Hard to block, slow to start." For a modifier, add "You pick the special that carries it." (E20, A2). Name, price and type mark stay on the tag, so the bubble does not restate them. Real lines are habit-writer's; the ones drawn are placeholders. Plain text comes from `card.plain` in data/modifiers; the Kappa specials are not in data/moves yet, so River Grab and Shell Spin text is carried over from the earlier mockup.

Bubble events (states sheet): enter, buy, can't afford, slot full, modifier target, release prompt, release done, locked (T5), sold out, leave. An event line replaces the focus line; the next focus change brings the focused item's description back.

### Input and navigation

- Grid: the shelf is a 2-wide grid in order River Grab, Shell Spin / River Pull, Slippery Skin / Cucumber Gift. Left / Right move within a shelf; Up / Down move between shelves, keeping the column where the target shelf has an item there, else landing on its nearest item.
- Right from the right column (or Right from the single item on a short shelf) enters the slots panel on the nearest slot; Up / Down moves between slots; Left returns to the shelf at the last focused item.
- Enter / A buys the focused item (confirm step "Buy River Grab for 50?") or, in the slots panel, starts Release. Esc / B backs out of a prompt, else leaves to the rest stop. Mouse: hover focuses (and the bubble speaks), click does what Enter does.
- Default focus: the first affordable item (River Grab); if none are affordable, the first item.
- Mouse and keyboard share one focus, so the bubble only ever speaks for one item.
- Buying a special with no empty slot opens the replace prompt (state 3). Buying a modifier opens the target step (state 4), the same as the reward screen's modifier card.

### States (merchant-shop-states.png)

1. Bought: the standing ofuda becomes a rolled scroll, tied with a cord and sealed, grey borrowed ink; tag reads "SOLD · borrowed ink"; not selectable again. Bubble: buy line.
2. Cannot afford: item dims, tag price becomes "Need 50"; Enter does nothing, the coin chip shakes, nothing changes. Bubble: can't-afford line.
3. Slot full when buying a special: pick a slot to replace (no refund); the Foxfire row warns it is the Tanuki target; Esc cancels.
4. Modifier target step: "Slippery Skin: pick the special that carries it", a row per special, attach rule A2.
5. Release confirmation: "Release Rising Talisman for 40? It loses its levels; the slot frees up." Default focus on Keep.
6. T5 lock: tied cord and seal across the last special's entry, "LOCKED · last special"; no Release chip; Enter does nothing; bubble line.
7. Shelf empty: bare shelf, bubble line, footer keeps only Back.
8. Overflow (more than 6, about 10): 3 shelves visible, focus past the edge scrolls one shelf, a rod on the right shows position ("1-3 / 5"), a faded "more below" cue, PgUp / PgDn or LB / RB jump a page.

Release unaffordable (coins under 40): the Release chip is greyed on the focused slot. Empty slot: dashed, not releasable. Bought abilities are grey and never offered as carry-over (M3). The shop never lists rares or cancel rules (R6, A9, X1).

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


## Scroll theme (revision 2)

Designer feedback: lean into the scroll theme. This is framing only; the element tables, numbers, states and questions above are unchanged. Where the text above says "torn rice-paper sheet", read the scroll forms below.

Vocabulary, drawn as flat shapes (shared with the map and twist-card mockups):
- Roller rods: wooden rod darker than the paper, rounded knobs sticking out past the paper at each end; a few offset paper-edge lines beside the side rods show the rolled part.
- Mounting: indigo (hanging scrolls) or pine (the big sheet) cloth bands along top and bottom, paper inset.
- Hanging cord: thin cord from a peg at the top centre of each hanging scroll.
- Tie cord and seal: a red seal stamp marks the Tanuki forecast, the bought state and the locked last special; a rolled, tied scroll means bought.

Changes:
- Rest stop (both variants): the sheet is now an unrolled handscroll (rods left and right, pine cloth top and bottom). Free heal / Rest and Browse the shop / Training trial are small hanging scrolls on pegs, stacked as before, the selected one keeping its persimmon frame and foxfire glow. The Tanuki forecast is its own hanging scroll with a red seal; still text and move icon only. Portrait box, line box, health, coins and footer are unchanged. The ROUGH strip moved above the scroll so it does not sit on the paper.
- Merchant shop: the three ability cards hang from pegs. "Your slots" is one tall hanging scroll with the four entries. The example-states strip shows cannot afford as a dimmed hanging scroll, bought as a rolled scroll tied with a cord and seal, and shelf empty as bare pegs. The T5 lock is a cord tied across the entry with a red seal instead of a grey lozenge.

Judgement calls (redirect any):
- Cards hang from pegs on the sheet rather than lying flat; the sheet itself is fully unrolled (no rolled portion), since all content must be readable at once.
- Option cards stay stacked, not side by side, to keep text widths. The shop cards lost 12 px of width (312 to 300) so rod knobs do not collide; text sizes are unchanged or larger and nothing wraps worse.
- The footer rule and key chips now stop short of the forecast / slots scroll.

Left plain on purpose: the portrait and line boxes, health bar, coin chip, key chips and the dashed art placeholders (decoration behind text would hurt legibility); the slot entries inside the slots scroll stay flat paper rows.

New questions:
1. In the real shelf, should a bought card be rolled up in place (hiding its text) or stay open, grey, with a seal? Drawn as rolled, tied and sealed in the states strip only.
2. Should the main rest-stop sheet animate unrolling on entry? Not drawn.

## Open questions: merchant shop shelf (YOK-159)

1. **Shelf size (open R6 rule).** Drawn as 3 shelves of 2, with 5 Kappa items. How many items does a visit offer, and how are they picked? Overflow (state 8) is a proposal in case it goes to about 10.
2. **What object stands on the shelf?** Drawn as a standing ofuda on a small stand with the ability icon. Alternatives: a small scroll, a bottle, a tablet. Astra pass decides the look; the layout works for any upright object about 80 x 130.
3. **Does the bubble also speak a hover-out line?** Decided: the bubble speaks the description on hover or focus, and event lines replace it until the next focus change. Open only: whether it falls back to a neutral idle line when nothing is focused (mouse leaves the shelf).
4. **Bought item: roll up in place or stay open and grey?** Drawn rolled and tied (state 1).
5. **Modifier flow order.** Buy then pick the special, or pick first. Drawn as buy then pick (state 4).
