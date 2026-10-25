using System;
using System.Linq;
using YokaiFighters.Fight;
using YokaiFighters.Sim;
using YokaiFighters.Story;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-212, S3 "later losses: variants by row reached and winning yokai": the lose screen's card is chosen by the yokai
/// that won (an elder as its yokai) and the row, from each card's <c>later_loss</c>; else the row band; else lose-screen.
/// The Oni cards here are TEST FIXTURES (the real ones are another branch's, PR #249); the row bands are data/story's.
/// </summary>
public static class LoseCardTests
{
	const string OniEarly = "fixture-lose-oni-early", OniLate = "fixture-lose-oni-late";

	/// <summary>TEST FIXTURE: an Oni wake-up card for rows <paramref name="min"/>..<paramref name="max"/>.</summary>
	static StoryCardData Fixture(string id, string? yokai, int min, int max) => StoryLibrary.Parse($$"""
		{ "kind": "story_card", "id": "{{id}}", "status": "proposed", "trigger": "TEST FIXTURE", "speaker": "narrator",
		  "text": "TEST FIXTURE {{id}}: {yokai} stands.", "placeholders": ["yokai"], "rules": ["S3"],
		  "later_loss": { {{(yokai is null ? "" : $"\"yokai\": \"{yokai}\", ")}}"row_min": {{min}}, "row_max": {{max}} } }
		""");

	/// <summary>data/story/ + the two Oni fixture cards (rows 1-5 and 6-8, as on PR #249).</summary>
	static StoryLibrary WithOniCards() =>
		new(FightScene.LoadStory().Cards.Concat(new[] { Fixture(OniEarly, "oni", 1, 5), Fixture(OniLate, "oni", 6, 8) }));

	static NodeContext At(string yokai, int row, string type = "duel", bool elder = false) =>
		NodeSceneTable.Default(ContentPaths.Data("map")).Resolve(new MapNodeData("n", row, type, yokai, elder)).Context;

	/// <summary>The card FightScene puts on the lose screen for a loss on this node (its body as the opponent).</summary>
	static string Shown(StoryLibrary lib, NodeContext node) => FightScene.LoseCardFor(lib, node, node.Fighter).Text;

	static string Card(StoryLibrary lib, string id, string name) => lib[id].FillText(yokai: name);

	[Test]
	public static void OniWin_Row3_ShowsTheOniEarlyCard()
	{
		var lib = WithOniCards();
		var node = At("oni", 3);
		Assert.Equal("kitsune", node.Fighter, "the Oni still fights on the stand-in body");
		Assert.Equal(OniEarly, lib.LaterLossCard("oni", 3).Id, "rows 1-5 cover row 3");
		Assert.Equal(Card(lib, OniEarly, "Oni"), Shown(lib, node), "the scene picks by the yokai, not the body");
	}

	[Test]
	public static void OniWin_Row6_ShowsTheOniLateCard()
	{
		var lib = WithOniCards();
		Assert.Equal(OniLate, lib.LaterLossCard("oni", 6).Id, "rows 6-8 cover row 6");
		Assert.Equal(Card(lib, OniLate, "Oni"), Shown(lib, At("oni", 6)), "lose screen on row 6");
	}

	[Test]
	public static void ElderOni_CountsAsTheOni()
	{
		var lib = WithOniCards();
		var r5 = At("elder-oni", 5, "elder", elder: true);
		Assert.Equal("oni", r5.DraftYokai, "the elder's yokai (plays_as)");
		Assert.Equal(Card(lib, OniEarly, "Elder Oni"), Shown(lib, r5), "row 5 elder: the Oni early card, {yokai} = Elder Oni");
		Assert.Equal(Card(lib, OniLate, "Elder Oni"), Shown(lib, At("elder-oni", 8, "elder", elder: true)), "row 8 elder: the Oni late card");
	}

	[Test]
	public static void KitsuneWin_FallsBackToTheRowBand()
	{
		var lib = WithOniCards();
		foreach (var (row, band) in new[] { (1, "lose-early"), (3, "lose-early"), (5, "lose-mid"), (6, "lose-mid"), (8, "lose-late") })
			Assert.Equal(Card(lib, band, "Kitsune"), Shown(lib, At("kitsune", row)), $"Kitsune on row {row}: {band}");
		Assert.Equal(Card(lib, "lose-early", "Kitsune"), Shown(lib, At("apprentice", 1)), "the apprentice plays as the Kitsune: row band");
		Assert.Equal("lose-late", lib.LaterLossCard("tanuki", 9).Id, "the Tanuki at the boss row: row band 7-9");
		Assert.Equal("lose-late", lib.LaterLossCard("oni", 9).Id, "an Oni past its own cards' rows: row band");
	}

	[Test]
	public static void NoRowBand_ShowsLoseScreen()
	{
		var lib = WithOniCards();
		Assert.Equal(StoryLibrary.LoseScreenId, lib.LaterLossCard("kitsune", 10).Id, "row 10: no band covers it");
		Assert.Equal(StoryLibrary.LoseScreenId, lib.LaterLossCard("kitsune", null).Id, "no row (off the map)");
		Assert.Equal(lib.LoseScreen("Kitsune").Text, FightScene.LoseCardFor(lib, null, "kitsune").Text, "no node: lose-screen");
		var onlyOni = new StoryLibrary(new[] { lib[StoryLibrary.LoseScreenId], lib[OniEarly] });
		Assert.Equal(StoryLibrary.LoseScreenId, onlyOni.LaterLossCard("kitsune", 3).Id, "no Kitsune card, no row band: lose-screen");
		Assert.Equal(OniEarly, onlyOni.LaterLossCard("oni", 3).Id, "the Oni still gets its own");
	}

	[Test]
	public static void SeveralMatch_YokaiFirst_ThenNarrowerRows_ThenLowerId()
	{
		var screen = FightScene.LoadStory()[StoryLibrary.LoseScreenId];
		var cards = new[]
		{
			screen, Fixture("band-wide", null, 1, 8), Fixture("band-b", null, 2, 4), Fixture("band-a", null, 3, 5),
			Fixture("oni-wide", "oni", 1, 8), Fixture("oni-b", "oni", 3, 3), Fixture("oni-a", "oni", 3, 3),
		};
		foreach (var lib in new[] { new StoryLibrary(cards), new StoryLibrary(cards.AsEnumerable().Reverse()) })
		{
			Assert.Equal("oni-a", lib.LaterLossCard("oni", 3).Id, "the yokai's own card beats any band; same width: lower id");
			Assert.Equal("oni-wide", lib.LaterLossCard("oni", 6).Id, "the only Oni card covering row 6, though a band is there too");
			Assert.Equal("band-a", lib.LaterLossCard("kappa", 3).Id, "no Kappa card: bands; 2-4 and 3-5 tie on width: lower id");
			Assert.Equal("band-b", lib.LaterLossCard("kappa", 2).Id, "only 2-4 and 1-8 cover row 2: the narrower");
			Assert.Equal("band-wide", lib.LaterLossCard(null, 8).Id, "no yokai: the band");
		}
	}

	[Test]
	public static void LaterLoss_ParsedFromData_AndBadOnesRejected()
	{
		var lib = FightScene.LoadStory();
		foreach (var (id, min, max) in new[] { ("lose-early", 1, 3), ("lose-mid", 4, 6), ("lose-late", 7, 9) })
			Assert.Equal(new LaterLoss(null, min, max), lib[id].LaterLoss, $"{id}: the rows its trigger names, any yokai");
		Assert.True(lib[StoryLibrary.LoseScreenId].LaterLoss is null, "lose-screen is the default, never a later-loss card");
		Assert.Equal(new LaterLoss("oni", 1, 5), Fixture("x", "oni", 1, 5).LaterLoss, "a yokai's card");
		string good = """{ "kind": "story_card", "id": "x", "status": "proposed", "trigger": "t", "text": "t", "speaker": "s", "rules": ["S3"], "later_loss": LL }""";
		foreach (string bad in new[]
		{
			"""{ "row_min": 4, "row_max": 3 }""", """{ "row_min": 0, "row_max": 3 }""", """{ "row_min": 1 }""",
			"""{ "row_min": 1.5, "row_max": 3 }""", """{ "yokai": "", "row_min": 1, "row_max": 3 }""", """[1, 3]""",
		})
		{
			bool threw = false;
			try { StoryLibrary.Parse(good.Replace("LL", bad)); } catch (FormatException) { threw = true; }
			Assert.True(threw, "rejects later_loss " + bad);
		}
	}
}
