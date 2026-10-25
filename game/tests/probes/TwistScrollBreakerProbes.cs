using System;
using System.Linq;
using Godot;
using YokaiFighters.Dev;
using YokaiFighters.Fight;
using YokaiFighters.Sim;
using YokaiFighters.Story;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>YOK-109 test-breaker probes: the hanging-scroll intro card (Y3), the elder strip and the Tanuki copy strip (T4, T6, AM6).</summary>
public static class TwistScrollBreakerProbes
{
	static string Root => System.IO.Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "..");
	static readonly string Long = string.Concat(Enumerable.Repeat("The shadow of the nine tails falls across the stone. ", 12));

	static StoryCard Card(Node host)
	{
		var card = GD.Load<PackedScene>("res://scenes/ui/story_card.tscn").Instantiate<StoryCard>();
		host.AddChild(card);
		return card;
	}

	sealed class View : IHudRunView
	{
		public bool CopyLocked { get; set; }
		public CopyTarget? Copy { get; set; }
		public bool InTanukiFight { get; set; }
		public bool ElderFight { get; set; }
	}

	static FightHud Hud(Node host)
	{
		var hud = GD.Load<PackedScene>("res://scenes/ui/hud.tscn").Instantiate<FightHud>();
		host.AddChild(hud);
		return hud;
	}

	static SpecialData Named(string name)
	{
		string fd = AbilityPool.LoadFixtures(Root).Special("test-spirit-wave")!.Level1Json;
		return SpecialLoader.Parse("{\"kind\":\"special\",\"id\":\"hud-name\",\"name\":\"" + name + "\",\"input\":{\"kata\":{\"slot\":\"A\"}},\"levels\":{\"1\":{\"frame_data\":" + fd + "}}}");
	}

	static StoryLibrary Lib(params (string Id, string Text)[] cards) =>
		new(cards.Select(c => new StoryCardData(c.Id, null, c.Text, Array.Empty<string>(), "")));

	// The shipped twists, measured against the fixed column and quote box (the author's IntroScroll_ShippedTwists_FitTheirScroll uses TextFits, which cannot fail).
	[Test]
	public static void Scroll_ShippedTwists_RealMeasure_FitColumnAndBox(Node host)
	{
		var card = Card(host);
		var story = TwistCopy.Story!;
		foreach (var tw in FightScene.Twists.All)
			foreach (string portrait in new[] { "", FightHud.PortraitPath("kitsune") })
			{
				card.Display(TwistCopy.Intro(tw, story, "x").Text, "ELDER TWIST · ROW 8", tw.Icon, "E", TwistCopy.IntroSpeaker(tw, story), TwistCopy.Label(tw, story, "x").Text, portrait);
				var s = card.Scroll!;
				float col = s.TextColumn.Size.X;
				var t = UiFind.Get<Label>(s, "Title"); var q = UiFind.Get<Label>(s, "Quote"); var r = UiFind.Get<Label>(s, "Rule");
				float qh = q.GetThemeFont("font").GetMultilineStringSize(q.Text, HorizontalAlignment.Left, col, s.QuoteFontSize).Y;
				Assert.True(Width(t, s.TitleFontSize) <= col, tw.Id + ": title " + Width(t, s.TitleFontSize) + " px in " + col);
				Assert.True(qh <= QuoteBoxHeight, tw.Id + ": quote " + qh + " px tall at " + s.QuoteFontSize + " in " + QuoteBoxHeight);
				Assert.True(Width(r, r.GetThemeFontSize("font_size")) <= col, tw.Id + ": rule " + Width(r, r.GetThemeFontSize("font_size")) + " px in " + col);
			}
		card.QueueFree();
	}

	// ---- Fit probes. A Label with no clip/ellipsis grows its own Size to its text, so a fit check against the label's Size
	// can never fail (the author's TextFits does exactly that). These measure against the fixed column and the quote box of
	// the scene (twist_scroll.tscn: Quote offset_top 310 to offset_bottom 440 = 130 px). ----
	const float QuoteBoxHeight = 130f;

	static float Width(Label l, int fontSize) => l.GetThemeFont("font").GetStringSize(l.Text, fontSize: fontSize).X;
	static bool Contained(Label l, int fontSize, float room) => Width(l, fontSize) <= room || l.ClipText || l.TextOverrunBehavior != TextServer.OverrunBehavior.NoTrimming;

	// Y3 / V1 (TwistScroll.Fit doc: "shrink to a floor instead of clipping or leaving the paper"): a title longer than the column.
	[Test]
	public static void Scroll_VeryLongTitle_StaysInsideTheColumn(Node host)
	{
		var card = Card(host);
		card.Display("q", "ELDER TWIST · ROW 5", "twist-armour", "E", "The Exceedingly Honourable Nine-Tailed Fox Spirit Of The Hundred Shrines", "r", null);
		var s = card.Scroll!;
		var t = UiFind.Get<Label>(s, "Title");
		Assert.True(Contained(t, s.TitleFontSize, s.TextColumn.Size.X), "title is " + Width(t, s.TitleFontSize) + " px wide at " + s.TitleFontSize + " px in a " + s.TextColumn.Size.X + " px column, no clip or ellipsis");
		card.QueueFree();
	}

	// Same claim, a medium title that fits at some size between 72 and 44: it must be shrunk until it fits.
	[Test]
	public static void Scroll_MediumTitle_ShrinksUntilItFits(Node host)
	{
		var card = Card(host);
		card.Display("q", "K", null, "E", "Exceedingly Honourable Nine-Tailed Fox", null, null);
		var s = card.Scroll!;
		var t = UiFind.Get<Label>(s, "Title");
		Assert.True(Width(t, s.TitleFontSize) <= s.TextColumn.Size.X || s.TitleFontSize == TwistScroll.TitleSizeMin, "title " + Width(t, s.TitleFontSize) + " px wide at " + s.TitleFontSize + " px in a " + s.TextColumn.Size.X + " px column");
		card.QueueFree();
	}

	// Same claim: a ~600 character quote must fit the 130 px quote box at the 28 px floor, or be clipped inside it.
	[Test]
	public static void Scroll_VeryLongQuote_StaysInsideItsBox(Node host)
	{
		var card = Card(host);
		card.Display(Long, "ELDER TWIST · ROW 5", "twist-armour", "E", "Elder Oni", "r", null);
		var s = card.Scroll!;
		var q = UiFind.Get<Label>(s, "Quote");
		float h = q.GetThemeFont("font").GetMultilineStringSize(q.Text, HorizontalAlignment.Left, s.TextColumn.Size.X, s.QuoteFontSize).Y;
		Assert.True(h <= QuoteBoxHeight || q.ClipText || q.TextOverrunBehavior != TextServer.OverrunBehavior.NoTrimming, "quote is " + h + " px tall at " + s.QuoteFontSize + " px in a " + QuoteBoxHeight + " px box (the rule line sits under it)");
		card.QueueFree();
	}

	// Same claim: a quote that fits the box at a smaller size than 34 px must be shrunk until it fits.
	[Test]
	public static void Scroll_MediumQuote_ShrinksUntilItFits(Node host)
	{
		var card = Card(host);
		card.Display(string.Concat(Enumerable.Repeat("The shadow falls across the stone. ", 6)), "K", null, "E", "Elder Oni", null, null);
		var s = card.Scroll!;
		var q = UiFind.Get<Label>(s, "Quote");
		float h = q.GetThemeFont("font").GetMultilineStringSize(q.Text, HorizontalAlignment.Left, s.TextColumn.Size.X, s.QuoteFontSize).Y;
		Assert.True(h <= QuoteBoxHeight || s.QuoteFontSize == TwistScroll.QuoteSizeMin, "quote is " + h + " px tall at " + s.QuoteFontSize + " px in a " + QuoteBoxHeight + " px box");
		card.QueueFree();
	}

	// Same claim for the red rule line (one 40 px row, no fit step): a long twist label must not run past the paper (right edge 1416).
	[Test]
	public static void Scroll_VeryLongRuleLine_StaysOnThePaper(Node host)
	{
		var card = Card(host);
		card.Display("q", "ELDER TWIST · ROW 5", "twist-armour", "E", "Elder Oni", Long, FightHud.PortraitPath("kitsune"));
		var s = card.Scroll!;
		var rule = UiFind.Get<Label>(s, "Rule");
		int fs = rule.GetThemeFontSize("font_size");
		Assert.True(Contained(rule, fs, s.TextColumn.Size.X), "rule line is " + Width(rule, fs) + " px wide in a " + s.TextColumn.Size.X + " px column with no wrap, clip or ellipsis");
		card.QueueFree();
	}

	// Same claim for the kicker (the host passes any string).
	[Test]
	public static void Scroll_VeryLongKicker_StaysInTheColumn(Node host)
	{
		var card = Card(host);
		card.Display("q", "ELDER TWIST · ROW 5 · " + Long, "twist-armour", "E", "Elder Oni", null, null);
		var s = card.Scroll!;
		var k = UiFind.Get<Label>(s, "Kicker");
		int fs = k.GetThemeFontSize("font_size");
		Assert.True(Contained(k, fs, s.TextColumn.Size.X), "kicker is " + Width(k, fs) + " px wide in a " + s.TextColumn.Size.X + " px column");
		card.QueueFree();
	}

	// The author's TextFits must be able to say false for an overflowing title (it measures against the label's self-grown Size).
	[Test]
	public static void Scroll_TextFits_AgreesWithAMeasureAgainstTheColumn(Node host)
	{
		var card = Card(host);
		card.Display("q", "K", null, "E", "The Exceedingly Honourable Nine-Tailed Fox Spirit Of The Hundred Shrines", null, null);
		var s = card.Scroll!;
		var t = UiFind.Get<Label>(s, "Title");
		bool really = Width(t, s.TitleFontSize) <= s.TextColumn.Size.X;
		Assert.Equal(really, s.TextFits, "TextFits vs the column (title is " + Width(t, s.TitleFontSize) + " px in " + s.TextColumn.Size.X + ")");
		card.QueueFree();
	}

	// Empty and null strings: no throw, scroll still shown, nothing stale from an earlier longer Display.
	[Test]
	public static void Scroll_EmptyAndNullStrings_DoNotThrowOrLeakTheLastCard(Node host)
	{
		var card = Card(host);
		card.Display(Long, "ELDER TWIST · ROW 9", "twist-armour", "E", "The Exceedingly Honourable Nine-Tailed Fox Spirit Of The Hundred Shrines", "A rule", FightHud.PortraitPath("kitsune"));
		card.Display("", "", null, "E", null, null, null);
		var s = card.Scroll!;
		Assert.True(s.Visible && card.Showing, "still shown");
		Assert.Equal("", s.Quote, "quote cleared");
		Assert.Equal("", s.TitleText, "title cleared");
		Assert.Equal("", s.RuleText, "rule cleared");
		Assert.Equal("", s.StampIcon, "icon cleared");
		Assert.True(!s.HasPortrait, "portrait cleared");
		Assert.Equal(TwistScroll.QuoteSize, s.QuoteFontSize, "font size recovers after a long quote (not stuck at the floor)");
		Assert.Equal(TwistScroll.TitleSize, s.TitleFontSize, "title size recovers");
		card.QueueFree();
	}

	// Mode switching: plain / scroll / plain / scroll, never both layers visible.
	[Test]
	public static void Card_SwitchingPlainAndScroll_NeverShowsBoth(Node host)
	{
		var card = Card(host);
		var panel = card.GetNode<Control>("Panel");
		for (int i = 0; i < 3; i++)
		{
			card.Display("scroll " + i, "K", "twist-reflect", "E", "T", "R", null);
			Assert.True(card.Scroll!.Visible && !panel.Visible, "scroll mode " + i);
			Assert.Equal("scroll " + i, card.Scroll.Quote, "scroll quote");
			card.Display("plain " + i);
			Assert.True(!card.Scroll.Visible && panel.Visible, "plain mode " + i);
			Assert.Equal("", card.KickerText, "no kicker on a plain card");
			Assert.Equal("", card.TwistIconId, "no icon on a plain card");
			Assert.Equal("plain " + i, card.GetNode<Label>("Panel/Text").Text, "plain text shown");
		}
		card.QueueFree();
	}

	// Display, Dismiss, Display; Continue twice; Dismiss raises nothing.
	[Test]
	public static void Card_DismissThenDisplay_AndDoubleContinue_RaiseFinishedOnce(Node host)
	{
		var card = Card(host);
		int done = 0; card.Finished += () => done++;
		card.Display("a", "K", "twist-reflect", "E", "T", "R", null);
		card.Dismiss();
		Assert.True(!card.Showing && !card.Visible && done == 0, "dismiss: hidden, no Finished");
		card.Continue();
		Assert.Equal(0, done, "Continue after Dismiss does nothing");
		card.Display("b", "K", "twist-reflect", "E", "T", "R", null);
		Assert.True(card.Showing && card.Visible && card.Scroll!.Visible && card.Scroll.Quote == "b", "second Display shows the scroll");
		card.Continue(); card.Continue();
		Assert.Equal(1, done, "Finished exactly once");
		Assert.True(!card.Visible && !card.Showing, "hidden after Continue");
		card.QueueFree();
	}

	// Y3: Tanuki never gets a portrait; Kitsune gets the full mask file; a missing file leaves the portrait out.
	[Test]
	public static void Scroll_Portraits_TanukiNeverKitsuneFullMask_MissingFileOut(Node host)
	{
		var card = Card(host);
		var s = card.Scroll!;
		var art = UiFind.Get<TextureRect>(s, "PortraitArt");
		card.Display("q", "K", null, "E", "Tanuki", null, FightHud.PortraitPath("tanuki"));
		Assert.True(!s.HasPortrait, "Tanuki: no portrait");
		card.Display("q", "K", null, "E", "Kitsune", null, FightHud.PortraitPath("nine-tailed-kitsune"));
		Assert.True(s.HasPortrait, "Kitsune alias: full mask");
		Assert.Equal("res://assets/generated/ui/fox-mask.png", art.Texture.ResourcePath, "the full mask file");
		card.Display("q", "K", null, "E", "Ghost", null, "res://assets/generated/ui/does-not-exist.png");
		Assert.True(!s.HasPortrait && art.Texture == null, "missing file: no box, no stale texture");
		Assert.True(s.TextColumn.Position.X == TwistScroll.ColumnLeftAlone && UiFind.Get<Label>(s, "Quote").Size.X == TwistScroll.ColumnWidthAlone, "children widened too");
		Assert.Equal("", FightHud.PortraitPath("tanuki"), "PortraitPath(tanuki) is empty");
		card.QueueFree();
	}

	static void AtElder(Node host, string yokai, bool first, Action<FightScene, FightHud> body)
	{
		var jump = DevJump.BuildFight(new DevFightSetup(yokai, Elder: true, Seed: 11), FightScene.LoadAbilityPool());
		Assert.True(jump.Session.Enter(jump.NodeId).Ok, "node entered");
		var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		scene.ExternalDrive = true;
		scene.Session = jump.Session;
		scene.FirstMeeting = first;
		host.AddChild(scene);
		try { body(scene, scene.GetNode<FightHud>("Hud")); }
		finally { scene.QueueFree(); }
	}

	// Y3: the callout is for the first meeting only.
	[Test]
	public static void Elder_NotFirstMeeting_CardShowsButCalloutDoesNot(Node host)
	{
		AtElder(host, "kitsune", false, (s, hud) =>
		{
			Assert.True(s.TwistCard!.Showing && s.FlowPaused, "card up and flow paused");
			s.TwistCard.Continue();
			Assert.True(!s.FlowPaused, "unpaused after the card");
			Assert.True(!hud.TwistCalloutVisible, "no callout on a repeat meeting");
		});
	}

	// Y3: with the intro card missing, FlowPaused must not stick and the fight can run.
	[Test]
	public static void Elder_MissingIntroCard_DoesNotPauseTheFlow(Node host)
	{
		try
		{
			TwistCopy.Story = Lib();
			AtElder(host, "oni", true, (s, hud) =>
			{
				Assert.True(!s.TwistCard!.Showing && !s.TwistCard.Visible, "no card");
				Assert.True(!s.FlowPaused, "flow not paused");
				Assert.True(!hud.TwistCalloutVisible, "no callout with no card");
				hud.Refresh(s.Match);
				Assert.True(!hud.TwistStripVisible && hud.TwistLine.Length == 0, "no strip without a label card");
			});
		}
		finally { TwistCopy.Story = null; }
	}

	// Y3 "a one-line intro card, and a callout on first meeting": the callout is its own story card. If only the intro card is
	// absent from the data, the first-meeting callout is still owed. Today it is raised only from the intro card's Finished event.
	[Test]
	public static void Elder_MissingIntroButCalloutPresent_StillShowsCalloutOnFirstMeeting(Node host)
	{
		try
		{
			var cards = FightScene.Twists.All.Where(t => t.CalloutCard != null).Select(t => (t.CalloutCard!, "A callout line.")).ToArray();
			Assert.True(cards.Length > 0, "twists have callout card ids");
			TwistCopy.Story = Lib(cards);
			AtElder(host, "kitsune", true, (s, hud) =>
			{
				Assert.True(!s.TwistCard!.Showing, "no intro card");
				hud.Refresh(s.Match);
				Assert.True(hud.TwistCalloutVisible, "first meeting: callout still shown although the intro card is missing");
			});
		}
		finally { TwistCopy.Story = null; }
	}

	// Y3: a missing callout card: card shows, Continue, no callout, unpaused, no throw.
	[Test]
	public static void Elder_MissingCalloutCard_ContinueShowsNoCalloutAndUnpauses(Node host)
	{
		try
		{
			var cards = FightScene.Twists.All.SelectMany(t => new[] { (t.IntroCard!, "An intro."), (t.Id + "-label", "A label.") }).ToArray();
			TwistCopy.Story = Lib(cards);
			AtElder(host, "kitsune", true, (s, hud) =>
			{
				Assert.True(s.TwistCard!.Showing && s.FlowPaused, "card up");
				Assert.Equal("An intro.", s.TwistCard.Scroll!.Quote, "intro from data");
				s.TwistCard.Continue();
				Assert.True(!s.FlowPaused && !hud.TwistCalloutVisible, "unpaused, no callout");
			});
		}
		finally { TwistCopy.Story = null; }
	}

	// Y3: with no label card the scroll shows no red rule line while the card still shows; no strip.
	[Test]
	public static void Elder_NoLabelCard_ScrollHasNoRuleLine_ButStripHidden(Node host)
	{
		try
		{
			TwistCopy.Story = Lib(FightScene.Twists.All.Select(t => (t.IntroCard!, "An intro.")).ToArray());
			AtElder(host, "kitsune", true, (s, hud) =>
			{
				var sc = s.TwistCard!.Scroll!;
				Assert.True(s.TwistCard.Showing, "intro still shown");
				Assert.Equal("", sc.RuleText, "no rule text");
				Assert.True(!UiFind.Get<Control>(sc, "RuleLine").Visible, "no rule line");
				hud.Refresh(s.Match);
				Assert.True(!hud.TwistStripVisible, "no strip");
			});
		}
		finally { TwistCopy.Story = null; }
	}

	// T4 / T6 / AM6: strip text "<Move> · Lv N · no modifiers, no evolution", N = Ryo's level, for 1..3 and above.
	[Test]
	public static void TanukiStrip_ReadsRyosLevel_ForEveryLevel(Node host)
	{
		var hud = Hud(host);
		try
		{
			foreach (int lv in new[] { 1, 2, 3, 4 })
			{
				hud.RunView = new View { InTanukiFight = true, Copy = new CopyTarget(SpecialSlot.A, Named("Foxfire"), lv) };
				hud.Refresh(new Match());
				Assert.True(hud.TwistStripVisible, "strip shows at Lv " + lv);
				Assert.Equal("THE TANUKI COPIES", hud.TwistKicker, "kicker");
				Assert.Equal($"Foxfire · Lv {lv} · no modifiers, no evolution", hud.TwistLine, "Ryo's level " + lv);
			}
		}
		finally { hud.Free(); }
	}

	// T6/AM6: no yokai fight, no strip; elder flagged with no twist on P2 shows nothing; Tanuki with nothing locked shows nothing.
	[Test]
	public static void Strip_YokaiElderWithoutTwistAndTanukiWithoutCopy_AllHidden(Node host)
	{
		var hud = Hud(host);
		try
		{
			hud.RunView = new View();
			hud.Refresh(new Match());
			Assert.True(!hud.TwistStripVisible, "yokai fight: no strip");
			hud.RunView = new View { ElderFight = true };
			hud.Refresh(new Match());
			Assert.True(!hud.TwistStripVisible, "elder with no twist: no strip");
			hud.RunView = new View { InTanukiFight = true, ElderFight = true };
			hud.Refresh(new Match());
			Assert.True(!hud.TwistStripVisible, "Tanuki fight with nothing locked: no strip, even if flagged elder");
		}
		finally { hud.Free(); }
	}

	// Odd unicode and very long move name: the whole phrase is kept, fitted or ellipsised, never silently cut.
	[Test]
	public static void TanukiStrip_OddAndVeryLongMoveNames_FitOrEllipsise(Node host)
	{
		var hud = Hud(host);
		try
		{
			foreach (string name in new[] { "狐火の舞 — Foxfire​", "Ünïcödé Spïrit Wave ✦", string.Concat(Enumerable.Repeat("Mirage ", 14)).Trim() })
			{
				var copy = new CopyTarget(SpecialSlot.B, Named(name), 3);
				hud.RunView = new View { InTanukiFight = true, Copy = copy };
				hud.Refresh(new Match());
				Assert.Equal(FightHud.CopyLine(copy), hud.TwistLine, "full line kept for " + name);
				var label = UiFind.Get<Label>(hud, "TwistText");
				float w = label.GetThemeFont("font").GetStringSize(label.Text, fontSize: label.GetThemeFontSize("font_size")).X;
				Assert.True(w <= label.Size.X || label.TextOverrunBehavior != TextServer.OverrunBehavior.NoTrimming, "fits or ellipsises: " + name);
			}
		}
		finally { hud.Free(); }
	}

	// Elder strip: shows with a label card, hides without (the cache is per HUD instance).
	[Test]
	public static void ElderStrip_ShowsWithLabelCard_HidesWithout_AcrossHuds(Node host)
	{
		var tw = FightScene.Twists.All.First();
		var m = new Match(); m.P2.Twist = tw;
		var a = Hud(host);
		var b = Hud(host);
		try
		{
			TwistCopy.Story = Lib((tw.Id + "-label", "Label text."));
			a.RunView = new View { ElderFight = true }; a.Refresh(m);
			Assert.True(a.TwistStripVisible && a.TwistLine == "Label text.", "strip with card");
			TwistCopy.Story = Lib();
			b.RunView = new View { ElderFight = true }; b.Refresh(m);
			Assert.True(!b.TwistStripVisible && b.TwistLine == "", "no card, no strip");
		}
		finally { TwistCopy.Story = null; a.Free(); b.Free(); }
	}
}
