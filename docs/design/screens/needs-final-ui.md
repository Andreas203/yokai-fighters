# Screens that still need final UI (2026-10-15)

For the **ui-designer**. These screens are greybox or placeholder, or not built at all. None of them has a concept image in `docs/design/screens/`, so there is no finished target to build to. For layout and visual fixes on screens that do have a concept, see `ui-review-2026-10-15.md`.

## Rules for this list
- The designer owns the concepts in `docs/design/screens/`. Where a screen has no concept, build to the **paper-talisman kit** (`game/scenes/ui/kit/`, `ui/talisman_theme.tres`) and to the composition of the closest existing concept (named below). Don't invent new art. Any new generated plate needs a spec and designer approval first (asset-smith, Meshy).
- Where the screen's *behaviour* isn't built yet, the screen needs gameplay-programmer work first (named below). Don't build UI over logic that doesn't exist; build against the interface once it lands.
- The kit is enough for greybox. "Final" means it matches the kit and the nearest concept at 1920x1080 and 1280x720, with captures in `docs/screenshots/current/`.

---

## A. Built, but greybox or without a concept

### A1. Run map — `map_screen.tscn`, `map_node.tscn`, `MapScreen.cs`, `MapNodeStyles.cs`
Capture: `docs/screenshots/current/map.png`, `map_midrun.png`. Built in YOK-78 as a "simple map screen".
Current state:
- Nodes are flat dark boxes on the night backdrop, with no paper or talisman look.
- Labels use a debug letter prefix: "D Duel", "E Elder duel", "M Merchant", "J Dojo", "B Boss". These letters look like internal codes.
- Merchant and Dojo nodes have no name in the body, so the box is empty.
- "Choose your path" heading sits on the dark background with no paper and no brush underline.
- "cleared" and "HERE" are plain text states, with no seal or stamp.
- Route lines are thin grey strokes. The taken route is orange, which is off the kit palette (persimmon #D8632C may be intended; confirm).
- The footer shows only `LEFT / RIGHT`, but nodes in the same row are stacked vertically, so check that up and down are handled and shown.
Target: small talisman cards per node with node-type icons (duel, elder, merchant, dojo, boss), an ink-brush route between them, a red seal on cleared nodes and a clear marker for "you are here". Closest concepts: `reward.png` (card style) and `story-card.png` (seal).
**Needs a designer concept** before final. The prompt can follow `prompts/prompts.json`.

### A2. Move list — `move_list_panel.tscn`
Capture: `docs/screenshots/current/menus/move_list.png`, `move_list_kata.png`, `move_list_frame_data.png`.
No concept image. The prompt exists in `prompts/prompts-movelist-controls.json` and hasn't been generated.
Works, but has overflow bugs (see the review). Needs a concept for the final layout of the two columns (base kit and specials) and for the frame-data view.

### A3. Settings, controls tab — `settings_panel.tscn`
Capture: `docs/screenshots/current/menus/settings_controls.png`, `settings_controls_kata.png`.
No concept image. The prompt exists in `prompts/prompts-movelist-controls.json` and hasn't been generated.
Works, but has layout bugs (see the review). Final layout depends on the concept.

### A4. Run won screen
`FightScene.Flow.cs` shows "a placeholder when hosted" for the last node of a real run. Only the demo version (`demo_complete.tscn`) is styled.
Target: reuse the `demo-complete.png` composition with the win story cards (S3: Win 1–3). Needs designer copy and approval of the layout.

---

## B. Not built: behaviour is missing too

These are in the GDD and `docs/design/rules.md` but have no screen and no concept. Each needs gameplay-programmer logic first, then a designer concept, then UI.

| Screen | Rules | What it must show | Logic status | Closest concept to build from |
|---|---|---|---|---|
| **Merchant** (row 4, possibly row 7) | R6, T5, S3 row-4 card | Choice between a free 25% heal **or** the shop. Shop: common abilities from yokai not bound this run (50 coins), "borrowed ink" items shown greyed, release a binding (40). Never offers Ryo's last special. Row-4 story line templated to the copy target. | Not built: in `data/map/node-scenes.json` the merchant is a stand-in Kitsune fight | `reward.png` (cards), `modifier-target.png` (slot pick for release) |
| **Dojo** (row 7) | R7 | Rest (heal 30%) **or** a training trial. The trial heals 15% either way and levels the special if landed within 3 attempts. Trial prompt and an attempt counter. | Not built: stand-in fight | `control-select.png` (two-choice rows), HUD for the trial overlay |
| **Rest stop and Tanuki forecast** | T6 | Forecast naming the move the Tanuki will copy, its level and expected cost. | Not built | `story-card.png`, `pause.png` (run panel) |
| **HUD target glow** | T6 | The copy-target talisman glows in the HUD all run. | Not built; the HUD has no special slots | `hud.png` |
| **Run-end unlocks** | M1, M4 | Unlocks awarded by depth (0 / 1 / 2 / 4) at run end, in a fixed order. | Not built. Cut candidate at the scope gate (G2): **check with the designer before starting** | `reward.png` |
| **Tanuki boss intro / reveal** | S3 (merchant drops disguise) | Story card or cards. Probably the existing `story_card.tscn` with new text. | Story text to come from habit-writer | `story-card.png` |

---

## C. Housekeeping
- `docs/design/screens/README.md` is out of date. It still lists the title, pause and settings as "Mockup only", but all three are now wired (the title and map in `boot.tscn`, pause in `fight.tscn`, settings through the title). The designer owns that file; flag it rather than edit it.
- `game/scenes/ui/menu_mockups.tscn` and the superseded `pause-no-characters.png`: confirm with the designer whether the mockup scene is still needed.

## Suggested order
1. Finish A1 (map) greybox clean-up now: drop the letter prefixes, name every node, use kit paper cards. Ask the designer for a map concept.
2. Ask the designer to generate the A2 and A3 concepts (the prompts are ready).
3. Do section B screens in the order their logic lands. Merchant comes first: it is never cut (G1).
