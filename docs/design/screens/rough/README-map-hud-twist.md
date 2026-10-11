# Rough mockups: map, HUD copy-target state, elder-twist card (YOK-101)

**ROUGH PROTOTYPES, awaiting an Astra pass.** Greybox layout only: flat shapes, placeholder boxes marked "portrait / face / icon / backdrop", system serif type. They fix where each element sits, what it says and how the player moves through it. They are not target art. No image generation or Meshy was used; every file is hand-written SVG rendered to PNG with headless Google Chrome (`--screenshot`, 1920x1080). Edit the `.svg` and re-render the same way.

| Mockup | Render | Source | Unblocks |
|---|---|---|---|
| Map | `map.png` | `map.svg` | YOK-78 follow-up, YOK-90 |
| Fight HUD, copy-target state | `hud-copy-target.png` | `hud-copy-target.svg` | YOK-74 |
| Same, talisman states sheet | `hud-copy-target-states.png` | `hud-copy-target-states.svg` | YOK-74 |
| Elder-twist intro card + callout | `elder-twist-card.png` | `elder-twist-card.svg` | YOK-90 |

Green numbered dots on the PNGs match the numbered lists below. Palette and framing follow `docs/design/screens/hud.png`, `story-card.png`, `reward.png` and the build's `docs/screenshots/current/map_midrun.png`. Yokai portraits are dashed placeholders (Oni, Kappa portraits do not exist yet). The Tanuki is never drawn: its node is a text label plus a "mark" placeholder, and its copy tell is a talisman and text only. The Kitsune portrait box notes the full face mask stays on.

---

## 1. Map (`map.png`)

Shown state: Ryo has finished the row-4 merchant; row-5 elders are reachable and Nine-Tails is the focused node. Layout extends the build (`map_midrun.png`): rows run left to right, nodes are paper cards, routes are lines. New against the build: elder twist seals shown by type, the copy-target talisman, health and coins, a focused-node detail panel, a legend.

| # | Element | Shows | Rule | Binds to |
|---|---|---|---|---|
| 1 | Title | "Choose your path" | R1 | static |
| 2 | Copy-target chip | Glowing paper strip: "COPY TARGET, Foxfire, Lv 2" | T6, T2 | copy-target getter (see gaps) |
| 3 | Run status | "RYO, 840 / 1000", health bar, coins, "row 4 of 8 done" | C1, R8 | `RunState` health; coins and row count (coins not yet in a documented API) |
| 4 | Row labels | ROW 1..8, "ELDER" on 5 and 8, "AFTER ROW 8" over the boss | R1, Y3 | `MapNodeData.Row` |
| 5 | Node cards | Type tag (D/E/M/J/B), yokai or node name, portrait placeholder, state caption | R1, R2 | `MapNodeData(Id, Row, Type, Yokai, Elder, Label)` |
| 6 | Elder twist seal | Red seal on the card's top-right corner: reflect (Nine-Tails), shield (Elder Oni armour), linked rings (Elder Kappa unbreakable throws) | Y3, Y2 | `MapNodeData.Elder` plus the profile's elder twist (`profile.schema.json`) |
| 7 | Tanuki node | Larger red-bordered card, "The Tanuki, waits at the end, copies your best move"; no likeness | R1, P5, T1 | `boss` node type |
| 8 | Focus detail panel | For the focused node: name, row, twist line in red, reward line, habit to punish | R2, Y2, Y3, R8, A9 | `MapNodeData`, node payload, profile habit and twist text |
| 9 | Legend | State swatches, node-type letters, the three twist seals | Y3 | static |
| 10 | Prompt strip | ARROWS / D-PAD Choose route, ENTER / A Go, ESC / START Pause | n/a | input |

Route layout drawn: row 1 one node, row 2 three, row 3 two, row 4 one, rows 5 to 8 two each, then the Tanuki. This is the proposed `data/map/default-map.json` and GDD Figure 6, with Figure 6's orientation turned sideways to match the build.

### Node states
- **Cleared** (grey card, "cleared"), on the walked route (thick persimmon line).
- **Current / you are here** (rice-paper card, persimmon border, "YOU ARE HERE"). Matches `RunMap.Current`; if the player quits mid-node the same marker reads "IN PROGRESS" (`RunMap.InNode`).
- **Reachable** (paper card, indigo border, bright route line in), from `RunMap.Reachable()`.
- **Selected** (reachable plus gold glow, cursor triangle), drives the detail panel.
- **Later** (dark indigo, name still shown so R2 holds); border tint by type: red elder, pine merchant, ochre dojo.
- **Passed over** (dashed, dimmer): in a column the player did not choose. Cannot be entered.
- **Locked** is not a separate state: anything not in `Reachable()` is not selectable (`RunMap.CanSelect`). Rejected select changes nothing.
- Node types: duel, elder duel, merchant, dojo, boss. Merchant and dojo cards have no yokai name; duels and elders always do (R2).

### Input and flow
- Keyboard: arrows or WASD move the selection between reachable nodes only (up/down picks between the two branches, left/right is not needed since routes are forward only, but is kept for parity with the build's LEFT/RIGHT); ENTER enters the node; ESC pauses.
- Pad: d-pad or left stick to choose, A to enter, Start to pause.
- Flow: Map, ENTER, node scene (fight, merchant, dojo; elder first-meeting shows the twist card first, section 3), back to Map after the node completes. Selecting an unreachable node does nothing and does not move state.

---

## 2. Fight HUD with the copy target (`hud-copy-target.png`, `hud-copy-target-states.png`)

Base is `hud.png` and the build's `FightHud` (Ryo panel top-left, opponent panel top-right, burst seal, 3-bar meter). Added: the copy-target talisman in the top-centre gap, the elder twist reminder, a temperament line. The green dashed band marks the gameplay plane (about y 520 to 880 at 1080p); no HUD element enters it. All HUD pieces sit in the top 210 px or bottom 100 px.

| # | Element | Shows | Rule | Binds to |
|---|---|---|---|---|
| 1 | Ryo panel | "RYO, 720 / 1000", health bar, portrait | C1, V1 | `FightHud`, Ryo health |
| 2 | Meter | 3 bars, "METER 140 / 300" | E-rules for meter (as built) | `IHudView.Meter/MeterMax` |
| 3 | Burst seal | Red seal "B", lit when available; "burst ready" caption | burst rules (as built) | `IHudView.BurstAvailable` |
| 4 | Opponent panel | "ELDER NINE-TAILS, 640 / 1000", health bar fills from the right, mask icon | R2 | opponent fighter health, node context |
| 5 | Copy-target talisman | Vertical ofuda 120x170 px, persimmon glow, caption COPY TARGET, move icon, move name, level ("Foxfire, Lv 2") | T6, T2 | copy-target getter (see gaps), slot special and level |
| 6 | Temperament | "PATIENT" under the opponent bar (build shows "AGGRESSIVE") | Y2 | opponent profile temperament |
| 7 | Elder twist reminder | Bottom-right strip: seal plus "ELDER TWIST, Specials used twice in a row are reflected." Only in elder fights | Y3, P5 | `NodeContext.Elder` plus profile twist text |
| 8 | Gameplay-plane guard | Dashed band, annotation only (not drawn in game) | V1, brief | n/a |

Layout rules worth keeping: panels end at x 880 and start at x 1040 so the 120 px talisman fits between them; names too long for one line (e.g. "RISING TALISMAN") wrap to two lines at the same slot size; legibility during hitstop and shake means the talisman and strip never move or scale with the camera.

### Talisman states (`hud-copy-target-states.png`)
- A **No target yet**: dim grey, dash. Fallback for practice mode or a fight with no run only; T5 says a run always has one.
- B **Defined**: steady glow, name and level.
- C **Levels up**: brief pulse and level tick, 0.4 s.
- D **Changes**: old name fades, new slides in, toast "Copy target: Oni's Hide, Lv 3" (T2 and T3 tie rules decide it).
- E **Locked at the Tanuki node**: red seal stamp "封", glow stays, caption reads LOCKED (T1).
- F **In the Tanuki fight**: LOCKED talisman plus a line "Foxfire at Lv 3, no modifiers" (T4). Replaces the elder-twist strip; no Tanuki likeness anywhere.

---

## 3. Elder-twist card (`elder-twist-card.png`)

Closest relative is `story-card.png` (paper card, one big line, ENTER Continue, seal bottom-right). Shows all three Y3 pieces: (a) the map-node icon (section 1), (b) the one-line intro card, (c) the first-meeting callout.

| # | Element | Shows | Rule | Binds to |
|---|---|---|---|---|
| 1 | Card | Rice-paper card centred on the dimmed night plate | V1, Y3 | scene |
| 2 | Portrait placeholder | Elder portrait (Astra); Kitsune keeps the full face mask | designer note | elder yokai id |
| 3 | Kicker + title | "ELDER TWIST, ROW 5", "Nine-Tailed Kitsune" | Y3, R1 | `MapNodeData` row, elder name |
| 4 | The one line | "Nine-Tails reflects any special you use twice in a row." | Y3, Y2 | profile elder twist text (or story data, see questions) |
| 5 | Twist seal | Same seal as on the map node, large | Y3 | `Elder` icon id |
| 6 | Continue | ENTER / A Continue | n/a | input |
| 7 | First-meeting callout | In the fight, a paper banner between HUD and gameplay plane: "ELDER TWIST, NEW. Nine-Tails reflects specials used twice in a row." Fades after about 3 s, then only the standing reminder strip remains | Y3 | first-meeting flag |
| 8 | Other twists | Same template: Elder Oni "...specials have armour: jabs won't stop them", Elder Kappa "...throws can't be broken." | Y2, Y3 | profiles |

Twist wording follows the Y2 table. The card text above is a draft for the Habit Writer and Rules Lawyer, not approved copy.

### Flow
Map, ENTER on an elder node, intro card (first time only), ENTER, fight starts and the callout banner shows once, then the reminder strip stays for the whole fight. Pad: A to continue. ESC pauses as elsewhere.

---

## Gaps found in the code (not for me to fix; Producer to ticket for `gameplay-programmer`)
- I found no copy-target getter in `docs/codebase-map.md` or the sim (T2 and T3: highest-level special in a slot, tie rule, locked from T1). The HUD chip, map chip, toast and state E/F all need a read-only `CopyTarget` (move id, level, locked flag, copy-strength level per T4).
- No documented coins in `RunState` for the map status line (R8).
- No "first meeting" flag for twists (see questions).

## Open questions for the designer
1. **One node per row or branching?** R1 says "one node per row", yet GDD Figure 6, the proposed `default-map.json` and the build all show two or three nodes per row. The mockup follows the build and the figure (choice between nodes). Confirm.
2. **Orientation.** Build runs left to right; Figure 6 reads bottom to top. Mockup keeps the build's left-to-right. Keep?
3. **Up/down input.** The build's prompt reads LEFT/RIGHT only, but branch choice is vertical. Proposed ARROWS/D-PAD with up/down picking the branch. OK?
4. **Status line on the map** (health, coins, rows done): not required by a rule, added for readability. Keep, or leave to the HUD only?
5. **Copy target on the map.** The screens table asks for it; I show a chip top-centre. Same glow as the HUD, or a quieter version?
6. **Does a run have a copy target from the start?** Ryo's starters are specials, so T2 makes a Lv 1 starter the target at turn one. Is that wanted on screen, or should the talisman only glow after the first drafted special? (State A exists as a fallback.)
7. **Intro card timing.** The rules say "intro card" and "callout on first meeting" but not when each shows. Proposed: card on entering the elder's node, callout on fight start. Is "first meeting" per save, per run, or every time? Does the card repeat on a second meeting of the same elder (Elder Oni appears on rows 5 and 8)?
8. **Twist text source.** Does the one line live in the behaviour profile, the story data, or a new twist file? Affects whose job the wording is.
9. **Elder Oni armour wording.** "Armour on every special" (Y2) is clear to designers; what does the player read: "armour", or "can't be interrupted by jabs"?
10. **Tanuki node blurb.** "Copies your best move" on the node gives away the reveal the story cards hold back (S3: the merchant drops his disguise at the boss node). The GDD map labels it "The Tanuki (boss)". Keep the node label as "Boss" with no blurb?
11. **Detail panel content.** Shows reward and habit to punish. Habit text is a design hint for the player (Y2 says habits are learnable); show before the fight or only after first meeting?
12. **Merchant and dojo node blurbs** (heal 25% or shop; rest 30% or trial) are not on the map cards. Add to the focus panel? Another agent is mocking the rest stop itself.
13. **Talisman size** at 720p: 120x170 px scales to 80x113. Move names may need a short form.
