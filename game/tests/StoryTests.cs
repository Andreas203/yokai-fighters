using System;
using System.IO;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Story;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>YOK-44: story cards from data/story/ (S3, S4) reach the reward screen's binding line and the lose screen.</summary>
public static class StoryTests
{
	const string Card = """
		{ "kind": "story_card", "id": "t", "status": "final", "trigger": "x", "title": "{yokai} wins",
		  "text": "That {move} of yours, {yokai}.", "placeholders": ["yokai", "move"], "speaker": "Tanuki", "rules": ["S4"] }
		""";

	[Test]
	public static void Loader_FillsPlaceholdersAndRejectsBadCards()
	{
		var c = StoryLibrary.Parse(Card);
		Assert.Equal("That Foxfire of yours, Kitsune.", c.FillText("Kitsune", "Foxfire"), "S4: run names filled");
		Assert.Equal("Kitsune wins", c.FillTitle("Kitsune", "Foxfire"), "title filled too");
		bool threw = false;
		try { c.FillText(yokai: "Kitsune"); } catch (InvalidOperationException) { threw = true; }
		Assert.True(threw, "a used placeholder with no value throws, never shows '{move}'");
		foreach (var (from, to) in new[] { ("\"story_card\"", "\"profile\""), ("[\"yokai\", \"move\"]", "[\"yokai\", \"item\"]"), ("\"speaker\": \"Tanuki\", ", "") })
		{
			threw = false;
			try { StoryLibrary.Parse(Card.Replace(from, to)); } catch (FormatException) { threw = true; }
			Assert.True(threw, $"rejects {to}");
		}
		Assert.Equal(0, StoryLibrary.LoadDirectory(Path.Combine(Path.GetTempPath(), "no-such-story-dir")).Cards.Count, "absent folder = empty library");
		Assert.Equal("Kitsune", StoryLibrary.DisplayName("kitsune"), "display name");
		Assert.Equal("Elder Oni", StoryLibrary.DisplayName("elder-oni"), "display name, two words");
	}

	[Test]
	public static void RealCards_BindingLineAndLoseScreen()
	{
		var lib = FightScene.LoadStory();
		IStorySource src = lib;
		Assert.Equal("Forgive me, Kitsune. I'll give it back.", src.BindingLine("Kitsune"), "S3 binding line from data/story");
		var (title, text) = lib.LoseScreen("Kitsune");
		Assert.Equal("Ryo is defeated", title, "lose title from data/story");
		Assert.True(text.StartsWith("Kitsune slips off between the lanterns") && !text.Contains('{'), $"lose text filled: {text}");
	}

	[Test]
	public static void FightScene_LoseScreenShowsTheStoryCard(Node runner)
	{
		var saved = FightScene.Sources;
		FightScene.Sources = FightScene.FixtureSources; // YOK-56: kits never depend on data/moves/
		try
		{
			var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
			scene.ExternalDrive = true;
			runner.AddChild(scene);
			var hud = scene.GetNode<FightHud>("Hud");
			var (title, text) = FightScene.LoadStory().LoseScreen(StoryLibrary.DisplayName(FightScene.Opponent));
			Assert.Equal(title, hud.LoseTitle, "HUD lose title = story card");
			Assert.Equal(text, hud.LoseText, "HUD lose text = story card with {yokai} = the opponent");
			Assert.True(!hud.LoseText.Contains("The yokai slips free"), "placeholder replaced");
			scene.QueueFree();
		}
		finally { FightScene.Sources = saved; }
	}
}
