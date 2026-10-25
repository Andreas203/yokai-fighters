# Ui: screens, map, shop, menus and the screen kit

`game/scripts/Ui/` (namespace `YokaiFighters.Ui`; `Kit/` = `YokaiFighters.Ui.Kit`). Owned by ui-designer with gameplay-programmer. Every screen is a real scene under `game/scenes/ui/`; scripts bind data, signals and input and find nodes by **name** (`UiFind`), so layout is edited in the editor. **No rule in the UI**: screens ask pure models that ask the sim; a refusal changes nothing and shows its `WhyNot*` text. Scene list, theme and editing notes: [project.md](project.md). Index: [README.md](README.md).

## Rewards, story and HUD pieces

| File | What it is and its public surface |
|---|---|
| `Ui/IRewardSource.cs` | Screen contract: `CardKind` (NewMove, Upgrade, Modifier, CancelRule), `CardView` (`Rare`, YOK-186), `TargetView`, `RewardOffer`, `IRewardSource` (`Offer`, `Pick(cardIndex, targetId?)`), `IStorySource` (implemented by `StoryLibrary` and `DraftRewardSource`), TEST FIXTURE `StubRewardSource`. |
| `Ui/DraftRewardSource.cs` | Wraps an `IRewardDraft` + `RunState` as `IRewardSource`: offer `Targets` = slots A–D with `Eligible`/`WhyNot`; `Pick(cardIndex, targetId)` maps the id to its slot and throws when the draft refuses; `LastResult`. A cancel-rule card becomes `CancelRule` tagged "RARE - CANCEL RULE"; a rare NEW is tagged "RARE - NEW - Lv 1" (YOK-186). Story defaults to `FightScene.LoadStory()`. |
| `Ui/RewardScreen.cs` | `scenes/ui/reward_screen.tscn`: card step + modifier target step, frame-data toggle (A12); Left/Right, Enter, Esc, Tab, pad; event `Picked(card, target)`, `Dismiss()`. Tests `RewardScreenTests`. |
| `Ui/RewardCard.cs` | `reward_card.tscn`: crest, name, source, tag, art via `AbilityArt.ForId` (modifiers and cancel rules use `Fallback`), `RareGold` crest and tag on a `Rare` card (YOK-186, provisional colour), plain text, frame-data panel, `LevelPips` for Lv 3. |
| `Ui/TargetCard.cs` | `target_card.tscn`: one slot A–D for a modifier: art, name, level, reason; selected and ineligible states. |
| `Ui/StoryCard.cs` | `story_card.tscn`: `Display(text)`, event `Finished`, `Dismiss()` (hides without `Finished`). Tests `StoryScreenTests`. |
| `Ui/AbilityArt.cs` | `ui/ability_art.tres` lookup: `ForId(moveId)` → icon, `Fallback` for null/empty/unknown. Used by reward, target, move-list, pause, rest stop, shop and the HUD talisman. Tests `AbilityArtTests`, `AbilityIconScreensTests`. |
| `Ui/CopyTalisman.cs` | T6, AM6: the copy-target talisman. In the HUD **only in the Tanuki fight**; on the map as the copy chip (`scenes/ui/copy_chip.tscn`). States `None`/`Defined`/`Locked`; `Apply(target, locked)`: higher level = pulse, other move = toast, silent after the lock; `LastEvent`, `DrawnBounds`, `Cuts` (forwards to `TextCuts.Cuts`). Tests `FightHudTests.CopyTarget_*`, `MapCopyChipTests`, `TextCutTests`. |
| `Ui/TextCuts.cs` | Pure: `Cuts(s)` returns the text-element cut offsets; `Badge(name, empty = "?")` returns the whole first text element upper-cased (falls back to `empty` for a null or empty name or a lone surrogate). Used by `CopyTalisman`, `RewardCard`, `TargetCard`, `ShopItem`, `ShopScreen` (YOK-162, YOK-205). Tests `TextCutTests`, `TextElementBadgeTests`. |
| `Ui/TwistCopy.cs` | Pure: twist wording from story data by id (`intro_card`, `callout_card`, `label_key` then `<twist-id>-label`), else a `[PLACEHOLDER]` line. Tests `TwistUiTests`. |
| `Ui/TwistSeal.cs` | Draws the placeholder twist icon by the data's icon id (looks from `MapNodeStyles.TwistLook`). |
| `Ui/TwistStripDecor.cs` | Scroll look of the HUD's bottom strip (elder twist, Tanuki copy line); decoration only. |
| `Ui/Paint.cs` | Godot-drawn talisman, brush and seal shapes, used by the `[Tool]` nodes below. |
| `Ui/TalismanPanel.cs`, `Ui/BrushStroke.cs`, `Ui/SealStamp.cs` | `[Tool]` drawing nodes with exported colours / thickness / glyph (need `dotnet build` before they draw in the editor). |
| `Ui/UiFind.cs` | `UiFind.Get<T>(root, name)`: finds scene nodes by name wherever the designer moved them. |

## Map screen

| File | What it is and its public surface |
|---|---|
| `Ui/IMapView.cs` | `IMapView`: `Nodes` (`MapNodeView(Id, Row, Type, Yokai?, Elder, State)`, `MapNodeState` Visited/Current/Reachable/Locked/PassedOver), `Routes` (`MapRouteView`), `Select(id)`, defaults `Status` (`MapStatusView`), `CopyTarget`/`CopyLocked` (T6), `Detail(id)` (`MapNodeDetail(Reward, Twist)`), `RunKey`. TEST FIXTURE `StubMapView`. |
| `Ui/RunMapView.cs` | `RunMapView(map, run?, scenes?)`: the real adapter (`Select` = `RunMap.Select`; reward lines from node-scene `Coins`/`Binds`, `Merchant.HealPercent`, `Dojo.RestHealPercent`). `SessionMapView` forwards it with the session's copy target. |
| `Ui/MapScreen.cs` | `ui/map_screen.tscn`: `SetView(view)`, `Refresh()`, `Move(dir)`, `Confirm()`, signal `NodeChosen(string)` once per accepted pick; reachable nodes only; status panel, row headers (`HeaderText`), detail panel (`FocusId`), legend, copy chip. Tests `MapScreenTests`. |
| `Ui/MapLayout.cs` | Pure `Compute(nodes, boardSize)`: one column per row value, nodes stacked in view order, size shrinks with counts. |
| `Ui/MapNode.cs` | `ui/map_node.tscn` script: one node button (icon, type label, elder mark, yokai name, state); `TwistIcon`. |
| `Ui/MapNodeStyle.cs` | Resource `MapNodeStyle(Type, Label, Color, Icon, IconTexture?)`: how one node type looks. |
| `Ui/MapNodeStyles.cs` | `ui/map_node_styles.tres`: the only type → look lookup (unknown type = fallback with its id), `TwistIcons`/`TwistLook`, `MapNodePalette`. A new node type needs an entry, no code. |
| `Ui/MapRoutes.cs` | Draws routes between node edges; `RouteKind` Idle/Open/Walked. Knows no map rules. |

## Rest stop and shop

| File | What it is and its public surface |
|---|---|
| `Ui/RestStopScreen.cs` | `ui/rest_stop.tscn` (merchant and dojo nodes): `INodeScene` (`BindRun`), `TakeFree()` (heal/rest), `OpenSecond()` (shop or trial), `Shop`, `Leave()`, `ShopClosed()`, `TrialClosed()`, events `ShopRequested`, `TrialRequested`, `TitleRequested`, `Left`. Not saved inside the stop (M5). Tests `RestStopTests`, `RestStopDojoPickerTests`. |
| `Ui/RestStopScreenModel.cs` | Pure: asks the `Merchant`/`Dojo` it was made for; one choice; `ChooseTrial()` (provisional: first offer), `Leave` refused while a trial runs; refusals change nothing. |
| `Ui/RestStopScreenForecast.cs` | T6 Tanuki forecast: `CopyRule.Target` before the lock, `LockedCopy` after; `For(session)`, `ExpectedCost(target)` = `target.Build().Damage` (Lv ≤ 2, no modifier, T4). Tests `ForecastCostTests`. |
| `Ui/RestStopScreenDecor.cs` | Drawn scroll rods and hangers. |
| `Ui/ShopScreen.cs` | `ui/shop.tscn` (R6, A2, T2, T5, A12, S2): `Bind(session)`, `BuySpecial`, `BeginPick`/`PickTarget`, `ReleaseSlot`, `ToggleFrameData`, `Refresh(focusKey)`, `Lines`, `MerchantLine`, `Closed`. 3 shelves × 3 items, scrolls a shelf at a time; the merchant's bubble speaks on focus (`MerchantShopLines.ForItem`) and on events. Tests `ShopScreenTests`, `ShopShelfTests`. |
| `Ui/ShopScreenModel.cs` | Pure: asks `MerchantShop` for shelves, prices, eligibility, reasons; `ShopEntry.Refusal` (Coins/SlotFull/Other), `Say(reason)` (display names, never ids). |
| `Ui/ShopItem.cs` | One standing talisman on a cradle with a hanging price tag; focus lift, sold, "Need 50" states. |
| `Ui/ShopCabinet.cs` | The cabinet plate (`shop-cabinet.png`, always 3 shelves) and per-row cell placement. |
| `Ui/ShopArt.cs` | Shop plate textures and scaled nine-slice helpers. |
| `Ui/ShopPlate.cs` | The ledger slip plate as a nine-slice (`PlateScale`). |
| `Ui/ShopBubble.cs` | Speech bubble body + tail. |
| `Ui/ShopSealMark.cs` | T5 mark on the last special's row (tied cord + red seal). |
| `Ui/ShopScreenDecor.cs` | Shop decoration drawing. |

## Title and menus (`Ui/Menus/`)

| File | What it is and its public surface |
|---|---|
| `Ui/Menus/TitleMenu.cs` | `ui/title_menu.tscn`: `SetSavedRun(SavedRunSummary?)`, `SetEntry(TitleEntryState)` (greyed with reason, skips focus), `DisabledReason(entry)`, `Open()`, `OpenSettings`/`CloseSettings`, one event per entry, `SettingsRequested`/`SettingsBackRequested`. Reasons shown in `ReasonScroll`. Tests `BootScreenLayoutTests`. |
| `Ui/Menus/PauseMenu.cs` | `ui/pause_menu.tscn`: `SetRunSummary(PauseRunSummary)` (`PauseSlotSummary.MoveId` optional), `Bind(...)`, `Open/Close`, `Resume`, `FreezeTree`, `SetDevMenuEntry`, `LockForOverlay`; events `ResumeRequested`, `RestartRequested`, `MoveListRequested`, `SettingsRequested`, `QuitToTitleRequested` (confirmed). Tests `PauseLayoutTests`. |
| `Ui/Menus/MoveListPanel.cs` | `ui/move_list_panel.tscn`: base-kit and special columns, scheme and frame-data toggles. |
| `Ui/Menus/MoveListSource.cs` | `Build(run, movesDir)`: move list from `data/moves` + `RunState` (`SpecialData.Build(level)`); `SlotRow.MoveId`; base-kit lines are placeholder copy (`PlainCopy`). |
| `Ui/Menus/MoveSlotCard.cs` | `move_slot_card.tscn`: one slot card (icon, name, level, modifier). |
| `Ui/Menus/LevelPips.cs` | Level pips (1–3). |
| `Ui/Menus/SettingsPanel.cs` | `ui/settings_panel.tscn`: Sound tab (3 `InkSlider`s, mute, Preview cues) and Controls tab (Kata/Kihon, bindings table). Tests `SettingsLayoutTests`. |
| `Ui/Menus/InkSlider.cs` | HSlider drawn as ink on paper; stock slider behaviour. |
| `Ui/Menus/MenuSettings.cs` | The live static settings every menu reads and writes: `SetMaster/SetMusic/SetSfx/SetMuted/SetScheme`, `Changed`, `Scheme`, bus names; `Attach(store)` loads and saves each change (null store = nothing saved); `Reset()` test hook. |
| `Ui/Menus/MenuInput.cs` | Shared keys: Esc / pad B back, Esc / pad Start `Pause`, Tab / pad Y frame data, Q / pad X scheme, Q-E / LB-RB tabs. |
| `Ui/Menus/MenuMockups.cs` | `ui/menu_mockups.tscn` host (keys 1–9 jump between screens). Tests `MenuMockupTests`, `MenuInputPathTests`. |
| `Ui/YokaiMask.cs` | Title mask (placeholder drawing + sway + plaque; loads `res://assets/generated/ui/title/mask-<yokai>.png` when present). Never the Tanuki. |
| `Ui/TitleBackdrop.cs` | Title backdrop drawing (gradient, moon, ridges, mist). |

## Screen kit (`Ui/Kit/`, scenes `game/scenes/ui/kit/`, resources `game/ui/kit/`)

Generated text-free plates supply paper, brush, ornaments and scenery (prompts and margins in `assets/specs/ui/ui-screen-plates.md`); all text stays live. Gallery `scenes/ui/kit/kit_gallery.tscn` (keys 1–5). Tests `KitScreenTests`.

| File | What it is and its public surface |
|---|---|
| `Ui/Kit/PaperSheet.cs` | `paper_sheet.tscn` container (`Panel/Content`, corners): `Card`, `Ornaments`. |
| `Ui/Kit/PaperStyleBox.cs` | Legacy drawn paper style box (unused by the texture styles). |
| `Ui/Kit/PaperDialog.cs` | `paper_dialog.tscn` confirm dialog: `Open`, confirm/cancel signals, focus confinement, Esc / pad B, `ShowDim`. |
| `Ui/Kit/KitBrushButton.cs` | `menu_row`/`red_button`/`ghost_button.tscn`: `Kind`, `Preview`, state logic, `StrokeArt`. |
| `Ui/Kit/KitCard.cs` | `kit_card.tscn` selectable card: `Selected`, `CardDisabled`, `Activated`, `SelectionArt`. |
| `Ui/Kit/KitHeading.cs` | `kit_heading.tscn`: live `Text`, `UnderlineWidth`, `UnderlineArt`. |
| `Ui/Kit/KeyChipFooter.cs` | `key_chip_footer.tscn`: `SetHints`, `Pinned`, `OnPaper`; switches keyboard/pad glyphs with the last device. |
| `Ui/Kit/InputGlyphs.cs` | Keyboard/mouse vs pad glyphs and the device drift threshold. |
| `Ui/Kit/CornerOrnament.cs` | `corner_ornament.tscn`: blossom corner, `Corner` mirror, `Art`. |
| `Ui/Kit/KitBackdrop.cs` | `kit_backdrop.tscn`: night backdrop, cover scaling, `Art`, `Dim`. |
| `Ui/Kit/KitState.cs` | `KitState` Auto/Normal/Focused/Disabled/Pressed (gallery pins a state). |
| `Ui/Kit/KitGallery.cs` | The gallery scene's pages A–E. |
| `Ui/Kit/KitGallery.Proofs.cs` | Gallery proofs (rough title, real reward screen on a seeded draft, HUD studies). |
