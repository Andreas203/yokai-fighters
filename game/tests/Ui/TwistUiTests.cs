using System;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;
using YokaiFighters.Story;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>YOK-139 (Y3): elder twist UI: wording read by id with marked placeholders, map icons, intro card, HUD strip and callout.</summary>
public static class TwistUiTests
{
	static ElderTwist Twist(string id = "elder-oni-armour", string? intro = null, string? callout = null, string icon = "twist-armour") =>
		new(id, "elder-oni", TwistEffect.SpecialArmour, 0, 1, "twist." + id, icon, intro, callout);

	static StoryLibrary Lib(params (string Id, string Text)[] cards) =>
		new(cards.Select(c => new StoryCardData(c.Id, null, c.Text, Array.Empty<string>(), "")));

	[Test]
	public static void Copy_NullIds_GiveMarkedPlaceholders()
	{
		var t = Twist();
		foreach (var line in new[] { TwistCopy.Intro(t, Lib()), TwistCopy.Callout(t, Lib()), TwistCopy.Label(t, Lib()), TwistCopy.Label(t, null) })
			Assert.True(line.Placeholder && line.Text.StartsWith(TwistCopy.PlaceholderMark), "placeholder is marked: " + line.Text);
	}

	[Test]
	public static void Copy_ReadsStoryCardsById_NoCodeChange()
	{
		var t = Twist(intro: "oni-intro", callout: "oni-callout");
		var lib = Lib(("oni-intro", "INTRO"), ("oni-callout", "CALL"), ("elder-oni-armour-label", "LABEL"));
		Assert.Equal("INTRO", TwistCopy.Intro(t, lib).Text, "intro_card id");
		Assert.Equal("CALL", TwistCopy.Callout(t, lib).Text, "callout_card id");
		Assert.True(!TwistCopy.Callout(t, lib).Placeholder, "real text is not a placeholder");
		Assert.Equal("LABEL", TwistCopy.Label(t, lib).Text, "<twist-id>-label card (YOK-138)");
		Assert.Equal("KEYED", TwistCopy.Label(t, Lib(("twist-elder-oni-armour", "KEYED"), ("elder-oni-armour-label", "LABEL"))).Text, "label_key (kebab) wins");
		Assert.True(TwistCopy.Intro(Twist(intro: "missing"), lib).Placeholder, "an id with no card falls back to the placeholder");
	}

	[Test]
	public static void Map_ElderNodeShowsTwistIconFromData(Node host)
	{
		var stage = new Control { Size = new Vector2(1920, 1080) };
		host.AddChild(stage);
		var s = GD.Load<PackedScene>("res://scenes/ui/map_screen.tscn").Instantiate<MapScreen>();
		stage.AddChild(s);
		var v = new StubMapView().Add("a", 1, "duel", "Kitsune").Add("e", 2, "elder", "Elder Oni", true, "twist-armour").Add("f", 2, "elder", "Elder X", true)
			.Route("a", "e").Route("a", "f").Open("a");
		v.Select("a"); v.Complete(); // YOK-109: the elders are drawn once they are the choice
		s.SetView(v);
		Assert.Equal("twist-armour", s.NodeFor("e")!.TwistIconId, "the twist's icon id reaches the node");
		Assert.Equal("", s.NodeFor("f")!.TwistIconId, "an elder with no twist data keeps the plain E mark");
		Assert.Equal("", s.NodeFor("a")!.TwistIconId, "a duel has none");
		Assert.True(s.NodeFor("f")!.ElderShown, "still an elder (Y3)");
		var styles = GD.Load<MapNodeStyles>("res://ui/map_node_styles.tres");
		foreach (var tw in FightScene.Twists.All)
			Assert.True(styles.TwistIcons.Any(i => i.Type == tw.Icon) && TwistSeal.HasShape(tw.Icon ?? ""), $"styles entry and drawn shape for {tw.Icon}");
		stage.QueueFree();
	}

	[Test]
	public static void IntroCard_ShowsTwistHeaderAndFinishes(Node host)
	{
		var card = GD.Load<PackedScene>("res://scenes/ui/story_card.tscn").Instantiate<StoryCard>();
		host.AddChild(card);
		int done = 0; card.Finished += () => done++;
		card.Display("hello", "ELDER TWIST · ROW 5", "twist-reflect");
		Assert.True(card.Showing && card.Text == "hello", "shows");
		Assert.Equal("ELDER TWIST · ROW 5", card.KickerText, "kicker");
		Assert.Equal("twist-reflect", card.TwistIconId, "seal icon");
		card.Display("plain");
		Assert.Equal("", card.KickerText, "a plain story card has no twist header");
		card.Continue();
		Assert.Equal(1, done, "Finished once");
		card.Continue();
		Assert.Equal(1, done, "a second confirm does nothing");
		card.QueueFree();
	}

	static FightHud Hud(Node host)
	{
		var hud = GD.Load<PackedScene>("res://scenes/ui/hud.tscn").Instantiate<FightHud>();
		host.AddChild(hud);
		return hud;
	}

	sealed class ElderView : IHudRunView
	{
		public bool InTanukiFight => false;
		public bool ElderFight => true;
		public CopyTarget? Copy => null;
		public bool CopyLocked => false;
	}

	[Test]
	public static void Hud_StripReadsTheMatchTwistAndCalloutExpires(Node host)
	{
		var hud = Hud(host);
		try
		{
			hud.RunView = new ElderView();
			var m = new Match();
			m.P2.Twist = Twist(callout: "x");
			TwistCopy.Story = Lib(("elder-oni-armour-label", "Its specials shrug off one hit."));
			hud.Refresh(m);
			Assert.True(hud.TwistStripVisible, "elder fight shows the strip");
			Assert.Equal("Its specials shrug off one hit.", hud.TwistLine, "strip text from the twist's story card");
			hud.ShowTwistCallout("NEW", "twist-armour");
			hud.Refresh(m);
			Assert.True(hud.TwistCalloutVisible && hud.TwistCalloutText == "NEW", "callout shows");
			Assert.Equal("twist-armour", hud.TwistCalloutIcon, "callout icon");
			for (int i = 0; i < FightHud.TwistCalloutTicks; i++) m.Step(default, default);
			hud.Refresh(m);
			Assert.True(!hud.TwistCalloutVisible, "expired after 180 ticks");
			hud.ShowTwistCallout("AGAIN", "twist-armour");
			hud.ClearTwistCallout();
			hud.Refresh(m);
			Assert.True(!hud.TwistCalloutVisible, "cleared");
			TwistCopy.Story = Lib();
			hud.Refresh(m);
			// strip text is cached per twist id for the fight; a new HUD sees the placeholder
			var hud2 = Hud(host);
			hud2.RunView = new ElderView();
			hud2.Refresh(m);
			Assert.True(hud2.TwistLine.StartsWith(TwistCopy.PlaceholderMark), "no card: marked placeholder");
			hud2.Free();
		}
		finally { TwistCopy.Story = null; hud.Free(); }
	}
}
