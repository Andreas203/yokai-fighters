using System;
using System.Linq;
using Godot;
using YokaiFighters.Dev;
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
	public static void Copy_MissingCards_AreFlaggedAndEmpty_NoInventedWording()
	{
		var t = Twist();
		foreach (var line in new[] { TwistCopy.Intro(t, Lib()), TwistCopy.Callout(t, Lib()), TwistCopy.Label(t, Lib()), TwistCopy.Label(t, null) })
			Assert.True(line.Placeholder && line.Text.Length == 0, "a missing card is flagged and empty, never a stand-in: " + line.Text);
	}

	// YOK-109: the three wording cards of every shipped twist exist, so no screen ever has to show a stand-in.
	[Test]
	public static void EveryShippedTwist_HasIntroCalloutAndLabelCards()
	{
		var story = TwistCopy.Story!;
		Assert.True(story != null && FightScene.Twists.All.Any(), "story data and twists load");
		foreach (var tw in FightScene.Twists.All)
		{
			Assert.True(!TwistCopy.Intro(tw, story).Placeholder, $"{tw.Id}: intro card");
			Assert.True(!TwistCopy.Callout(tw, story).Placeholder, $"{tw.Id}: callout card");
			Assert.True(!TwistCopy.Label(tw, story).Placeholder, $"{tw.Id}: label card");
			Assert.True(TwistCopy.IntroSpeaker(tw, story) is { Length: > 0 }, $"{tw.Id}: the intro card names the elder (the scroll's title)");
		}
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
		Assert.True(TwistCopy.Intro(Twist(intro: "missing"), lib).Placeholder, "an id with no card is flagged missing");
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
			// strip text is cached per twist id for the fight; a new HUD with no label card shows no strip and no invented line
			var hud2 = Hud(host);
			hud2.RunView = new ElderView();
			hud2.Refresh(m);
			Assert.True(!hud2.TwistStripVisible && hud2.TwistLine.Length == 0, "no card: no strip, no stand-in text");
			hud2.Free();
		}
		finally { TwistCopy.Story = null; hud.Free(); }
	}

	// ---- YOK-109: the intro card as a hanging scroll (E1), wording from story data ----

	static StoryCard Card(Node host)
	{
		var card = GD.Load<PackedScene>("res://scenes/ui/story_card.tscn").Instantiate<StoryCard>();
		host.AddChild(card);
		return card;
	}

	[Test]
	public static void IntroScroll_ShowsEveryE1Element_AndAPlainCardHidesIt(Node host)
	{
		var card = Card(host);
		card.Display("The quote.", "ELDER TWIST · ROW 5", "twist-reflect", "E", "Nine-Tailed Kitsune", "Reflects a special used twice in a row", FightHud.PortraitPath("kitsune"));
		var scroll = card.Scroll!;
		Assert.True(scroll.Visible && !card.GetNode<Control>("Panel").Visible, "the scroll replaces the plain paper");
		Assert.Equal("ELDER TWIST · ROW 5", scroll.Kicker, "kicker");
		Assert.Equal("Nine-Tailed Kitsune", scroll.TitleText, "elder name as the title");
		Assert.Equal("The quote.", scroll.Quote, "intro quote");
		Assert.Equal("Reflects a special used twice in a row", scroll.RuleText, "red rule line");
		Assert.Equal("twist-reflect", scroll.StampIcon, "the stamp is the twist's icon id (the map's seal)");
		Assert.True(scroll.HasPortrait, "the Kitsune has portrait art (the full mask)");
		Assert.True(scroll.Footer.ChipTexts().Length == 1, "one key chip: confirm / Continue");
		card.Display("plain");
		Assert.True(!scroll.Visible && card.GetNode<Control>("Panel").Visible, "a plain story card shows the paper, not the scroll");
		card.QueueFree();
	}

	[Test]
	public static void IntroScroll_NoPortraitArt_LeavesTheSpaceOut(Node host)
	{
		var card = Card(host);
		var scroll = card.Scroll!;
		card.Display("q", "ELDER TWIST · ROW 5", "twist-armour", "E", "Elder Oni", "Armour on every special", FightHud.PortraitPath("oni"));
		Assert.True(!scroll.HasPortrait && scroll.TextColumn.Position.X == TwistScroll.ColumnLeftAlone && scroll.TextColumn.Size.X == TwistScroll.ColumnWidthAlone, "no Oni art: no box, the text column takes the space");
		card.Display("q", "ELDER TWIST · ROW 5", "twist-armour", "E", "Elder Oni", null, null);
		Assert.True(!scroll.HasPortrait, "a null path leaves it out too");
		Assert.True(!scroll.GetNode<Control>("TextCol/Rule").Visible && !scroll.GetNode<Control>("TextCol/RuleLine").Visible, "no label, no rule line");
		card.Display("q", "ELDER TWIST · ROW 5", "twist-reflect", "E", "Nine-Tailed Kitsune", "r", FightHud.PortraitPath("kitsune"));
		Assert.True(scroll.HasPortrait && scroll.TextColumn.Position.X == TwistScroll.ColumnLeftWithPortrait, "back with art: the portrait and the narrow column");
		card.QueueFree();
	}

	[Test]
	public static void IntroScroll_ShippedTwists_FitTheirScroll(Node host)
	{
		var card = Card(host);
		var story = TwistCopy.Story!;
		foreach (var tw in FightScene.Twists.All)
		{
			card.Display(TwistCopy.Intro(tw, story, "x").Text, "ELDER TWIST · ROW 8", tw.Icon, "E", TwistCopy.IntroSpeaker(tw, story), TwistCopy.Label(tw, story, "x").Text, null);
			Assert.True(card.Scroll!.TextFits, $"{tw.Id}: title on one line, quote inside its box (title {card.Scroll.TitleFontSize}px, quote {card.Scroll.QuoteFontSize}px)");
			Assert.True(card.Scroll.QuoteFontSize >= TwistScroll.QuoteSizeMin && card.Scroll.TitleFontSize >= TwistScroll.TitleSizeMin, $"{tw.Id}: never below the floor");
		}
		card.QueueFree();
	}

	// The card schema allows 280 characters of text (S2): such a quote must show whole, with or without a portrait; past that it is
	// shortened with an ellipsis and flagged, never cut silently (YOK-109 round 2).
	static string Words(int chars)
	{
		const string seed = "The old fox watched the apprentice climb the shrine steps and said nothing. Every tail he had lost was a promise kept, and Ryo would learn what that cost before the lanterns burned out. ";
		return string.Concat(Enumerable.Repeat(seed, 4))[..chars].TrimEnd();
	}

	static float QuoteNeeds(TwistScroll s)
	{
		var q = s.GetNode<Label>("TextCol/Quote");
		return q.GetThemeFont("font").GetMultilineStringSize(q.Text, HorizontalAlignment.Left, s.TextColumn.Size.X, s.QuoteFontSize).Y;
	}

	[Test]
	public static void IntroScroll_280CharacterQuote_ShowsWhole_WithAndWithoutPortrait(Node host)
	{
		var card = Card(host);
		foreach (bool portrait in new[] { true, false })
			foreach (int chars in new[] { 150, 180, 230, 280 })
			{
				string quote = Words(chars);
				card.Display(quote, "ELDER TWIST · ROW 5", "twist-armour", "E", "Elder Oni", "Armour on every special", portrait ? FightHud.PortraitPath("kitsune") : null);
				var s = card.Scroll!;
				string where = $"{chars} chars, {(portrait ? "with" : "without")} portrait";
				Assert.Equal(quote, s.Quote, $"{where}: every character is there");
				Assert.True(QuoteNeeds(s) <= TwistScroll.QuoteBoxHeight, $"{where}: needs {QuoteNeeds(s)} px at {s.QuoteFontSize} px, box is {TwistScroll.QuoteBoxHeight}");
				Assert.True(s.TextFits && !s.QuoteTrimmed, $"{where}: reported as fitting");
				var line = s.GetNode<Control>("TextCol/RuleLine");
				var footer = s.GetNode<Control>("TextCol/Footer");
				Assert.True(line.Position.Y >= TwistScroll.QuoteTop + QuoteNeeds(s) && s.GetNode<Control>("TextCol/Rule").Position.Y + 40f <= footer.Position.Y, $"{where}: rule line under the quote and above the key chip");
				Assert.True(footer.Position.Y + footer.Size.Y <= TwistScroll.RiceInset.End.Y, $"{where}: key chip inside the paper");
			}
		card.QueueFree();
	}

	[Test]
	public static void IntroScroll_QuotePastTheSchemaLimit_IsShortenedWithAnEllipsis_NeverCutSilently(Node host)
	{
		var card = Card(host);
		string quote = Words(700);
		card.Display(quote, "ELDER TWIST · ROW 5", "twist-armour", "E", "Elder Oni", "r", null);
		var s = card.Scroll!;
		Assert.True(s.QuoteTrimmed && !s.TextFits, "flagged as shortened");
		Assert.True(s.Quote.EndsWith("…") && s.Quote.Length < quote.Length, "ends in an ellipsis");
		Assert.True(QuoteNeeds(s) <= TwistScroll.QuoteBoxHeight, "what is shown fits the box");
		Assert.Equal(quote, card.Text, "the card's own text is untouched");
		card.Display("Short.", "ELDER TWIST · ROW 5", "twist-armour", "E", "Elder Oni", "r", null);
		Assert.True(!s.QuoteTrimmed && s.TextFits && s.Quote == "Short.", "a later short quote clears the flag and the ellipsis");
		card.QueueFree();
	}

	static void AtElder(Node host, string yokai, Action<FightScene, FightHud> body)
	{
		var jump = DevJump.BuildFight(new DevFightSetup(yokai, Elder: true, Seed: 11), FightScene.LoadAbilityPool());
		Assert.True(jump.Session.Enter(jump.NodeId).Ok, "node entered");
		var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		scene.ExternalDrive = true;
		scene.Session = jump.Session;
		scene.FirstMeeting = true;
		host.AddChild(scene);
		try { body(scene, scene.GetNode<FightHud>("Hud")); }
		finally { scene.QueueFree(); }
	}

	[Test]
	public static void ElderFight_ShowsTheStoryDataOnCardCalloutAndStrip_NoPlaceholders(Node host)
	{
		foreach (string yokai in new[] { "kitsune", "oni", "kappa" })
			AtElder(host, yokai, (s, hud) =>
			{
				var tw = s.Twist!;
				var story = TwistCopy.Story!;
				var scroll = s.TwistCard!.Scroll!;
				Assert.True(s.TwistCard.Showing && scroll.Visible, $"{yokai}: the intro card is up");
				Assert.Equal(TwistCopy.Intro(tw, story, s.OpponentName).Text, scroll.Quote, $"{yokai}: quote = the -intro card");
				Assert.Equal(TwistCopy.IntroSpeaker(tw, story), scroll.TitleText, $"{yokai}: title = the intro card's speaker");
				Assert.Equal(TwistCopy.Label(tw, story, s.OpponentName).Text, scroll.RuleText, $"{yokai}: red rule line = the -label card");
				Assert.Equal(tw.Icon, scroll.StampIcon, $"{yokai}: one icon per twist, the map's");
				Assert.True(scroll.Kicker.StartsWith("ELDER TWIST · ROW "), $"{yokai}: kicker");
				Assert.Equal(yokai == "kitsune", scroll.HasPortrait, $"{yokai}: portrait only where approved art exists (the Kitsune's full mask)");
				s.TwistCard.Continue();
				Assert.True(hud.TwistCalloutVisible, $"{yokai}: first meeting shows the callout");
				Assert.Equal(TwistCopy.Callout(tw, story, s.OpponentName).Text, hud.TwistCalloutText, $"{yokai}: callout = the -callout card");
				hud.Refresh(s.Match);
				Assert.Equal(TwistCopy.Label(tw, story, s.OpponentName).Text, hud.TwistLine, $"{yokai}: strip = the -label card");
				foreach (string text in new[] { scroll.Quote, scroll.RuleText, hud.TwistCalloutText, hud.TwistLine })
					Assert.True(text.Length > 0 && !text.Contains("PLACEHOLDER"), $"{yokai}: no placeholder wording: {text}");
			});
	}

	[Test]
	public static void ElderFight_WithNoIntroCard_ShowsNoScrollButStillTheFirstMeetingCallout(Node host)
	{
		var saved = TwistCopy.Story;
		try
		{
			var tw = FightScene.Twists.All.First(t => t.Id == "elder-oni-armour");
			TwistCopy.Story = Lib(((tw.CalloutCard ?? ""), "Callout words."), (tw.Id + "-label", "Label words."));
			var jump = DevJump.BuildFight(new DevFightSetup("oni", Elder: true, Seed: 11), FightScene.LoadAbilityPool());
			Assert.True(jump.Session.Enter(jump.NodeId).Ok, "node entered");
			var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
			scene.ExternalDrive = true; scene.Session = jump.Session; scene.FirstMeeting = true;
			host.AddChild(scene);
			try
			{
				Assert.True(scene.Twist != null && !scene.TwistCard!.Showing && !scene.FlowPaused, "no intro card: nothing shows and the fight is not held");
				Assert.True(scene.GetNode<FightHud>("Hud").TwistCalloutVisible, "the first-meeting callout still shows");
			}
			finally { scene.QueueFree(); }
		}
		finally { TwistCopy.Story = saved; }
	}
}
