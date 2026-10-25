# Story: story-card and shop-line loaders

`game/scripts/Story/`. System.Text.Json, no Godot, no RNG. Text lives in `data/story/` (habit-writer); code only loads and fills it. Index: [README.md](README.md).

| File | What it is and its public surface |
|---|---|
| `Story/StoryLibrary.cs` | Kind `story_card` loader. `StoryCardData` (`FillText`/`FillTitle(yokai, move)`; throws on a used placeholder with no value), `StoryLibrary.Parse/LoadFile/LoadDirectory` (absent folder = empty), indexer by id, `BindingLine(yokai)` (implements `Ui.IStorySource`), `LoseScreen(yokai)`, `DemoComplete(yokai)`, `DisplayName(id)`. `FightScene.StoryDir`/`LoadStory()` point at `data/story/`. |
| `Story/MerchantShopLines.cs` | S1, S2, R6, A12: kind `merchant_shop_lines` (`data/story/shop/merchant-shop-lines.json`). `Event(key, pick)` (variant, wraps), `ForItem(name, cardText, pick)` (lead-in + card text verbatim, or card text alone past 2 sentences, S2), `Sentences(text)`, `LoadDirectory(storyDir)` (absent = `Empty`). `Parse` rejects a wrong kind, placeholders in plain events, lead-ins without `{name}`/`{card_text}`, lines over 2 sentences. Tests `ShopShelfTests`. |
