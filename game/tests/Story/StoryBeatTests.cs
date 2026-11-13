using System.Collections.Generic;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;
using YokaiFighters.Story;
using YokaiFighters.Tutorial;

namespace YokaiFighters.Tests;

/// <summary>S3 beats: which card shows when, the cards exist and fill, no placeholder text reaches the player.</summary>
public static class StoryBeatTests
{
	static NodeContext Node(string type, int row, string tier = "yokai", string fighter = "kitsune") =>
		new("n", type, row, fighter, false, fighter, null, tier, 30, false, false, new Dictionary<string, System.Text.Json.JsonElement>(), System.Array.Empty<string>());

	[Test]
	public static void BeatFor_IntroOnFirstRunRowOne_RevealOnTheTanukisNode_NothingElse()
	{
		Assert.Equal(FightScene.IntroId, FightScene.BeatFor(Node("duel", 1), false), "first run, row 1: the intro");
		Assert.True(FightScene.BeatFor(Node("duel", 1), true) == null, "intro seen: nothing");
		Assert.True(FightScene.BeatFor(Node("duel", 3), false) == null, "row 3: nothing");
		Assert.Equal(FightScene.RevealId, FightScene.BeatFor(Node(NodeTypeRegistry.Boss, 9, "tanuki", "tanuki"), true), "the boss node: the reveal, every run");
	}

	[Test]
	public static void WinCard_IsTheFirstUnseen_ThenTheLast()
	{
		var seen = new HashSet<string>();
		Assert.Equal("win-1", FightScene.WinCardFor(seen.Contains), "no wins yet");
		seen.Add("win-1"); Assert.Equal("win-2", FightScene.WinCardFor(seen.Contains), "after win 1");
		seen.Add("win-2"); Assert.Equal("win-3", FightScene.WinCardFor(seen.Contains), "after win 2");
		seen.Add("win-3"); Assert.Equal("win-3", FightScene.WinCardFor(seen.Contains), "all seen: the last");
	}

	[Test]
	public static void EveryBeatCardExists_AndFillsWithoutAPlaceholderTag()
	{
		var story = FightScene.LoadStory();
		foreach (string id in new[] { "intro-soen", "boss-reveal", "win-1", "win-2", "win-3", "lose-early", "lose-mid", "lose-late", "lose-oni-early", "lose-oni-late", "dojo-master-line", "merchant-row4" })
		{
			Assert.True(story.Has(id), id + " exists");
			string t = story[id].FillText(yokai: "Oni", move: "Foxfire");
			Assert.True(t.Length > 0 && !t.Contains("PLACEHOLDER") && !t.Contains("{"), id + " fills cleanly");
		}
	}

	[Test]
	public static void LoseCards_FollowTheYokaiAndTheRow()
	{
		var story = FightScene.LoadStory();
		Assert.Equal("lose-oni-early", story.LaterLossCard("oni", 3).Id, "oni, row 3");
		Assert.Equal("lose-oni-late", story.LaterLossCard("oni", 6).Id, "oni, row 6");
		Assert.Equal("lose-early", story.LaterLossCard("kitsune", 2).Id, "kitsune, row 2: the row band");
		Assert.Equal("lose-mid", story.LaterLossCard("kappa", 5).Id, "kappa, row 5: the row band");
		Assert.Equal("lose-late", story.LaterLossCard("tanuki", 9).Id, "tanuki, row 9");
	}

	[Test]
	public static void Portraits_ElderUsesItsYokai_ApprenticeHasItsOwn_NoneIsBlank()
	{
		Assert.Equal(FightHud.PortraitPath("oni"), FightHud.PortraitPath("elder-oni"), "elder oni wears oni's plate");
		Assert.Equal(FightHud.PortraitPath("kappa"), FightHud.PortraitPath("elder-kappa"), "elder kappa wears kappa's plate");
		Assert.Equal("res://assets/generated/ui/apprentice-portrait.png", FightHud.PortraitPath("apprentice"), "the apprentice's own plate");
		Assert.Equal("apprentice", FightScene.PortraitIdFor(Node("duel", 1, "apprentice"), "kitsune"), "an apprentice node asks for the apprentice plate");
		foreach (string id in new[] { "apprentice", "oni", "kappa", "elder-oni", "elder-kappa", "tanuki", "kitsune" })
			Assert.True(FightHud.PortraitTexture(id) != null, id + ": a plate or the seal, never blank");
	}

	[Test]
	public static void TutorialKeys_FillTheCardTokens_PerDevice()
	{
		TutorialPrompts.Library = FightScene.LoadStory();
		FightScene.SetTutorialKeyNames(pad: false);
		string kb = TutorialPrompts.For(TutorialLesson.Throw);
		Assert.True(!kb.Contains("{") && kb.Contains("+"), "keyboard throw chord filled: " + kb);
		Assert.True(TutorialPrompts.For(TutorialLesson.Burst) is { } b && !b.Contains("{"), "burst chord filled");
		FightScene.SetTutorialKeyNames(pad: true);
		Assert.True(TutorialPrompts.For(TutorialLesson.Throw).Contains("X+A"), "pad: X+A");
		Assert.True(TutorialPrompts.For(TutorialLesson.Burst).Contains("X+Y+RB"), "pad: X+Y+RB");
		Assert.True(TutorialPrompts.SkipHint.StartsWith("Select"), "pad: the skip hint names Select");
		FightScene.SetTutorialKeyNames(pad: false);
		Assert.True(TutorialPrompts.SkipHint.StartsWith("Tab"), "keyboard: Tab");
	}
}
