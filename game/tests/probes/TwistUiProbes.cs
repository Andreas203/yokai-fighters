using System;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Flow;
using YokaiFighters.Sim;
using YokaiFighters.Story;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>YOK-139 test-breaker probes: elder twist UI (Y3). Presentation only; expected values from rules Y3 and the ticket's claims.</summary>
public static class TwistUiProbes
{
	static RunSession Walk(params string[] ids)
	{
		string md = ContentPaths.Data("map");
		var session = RunSession.Start(FightScene.LoadAbilityPool(), FileMapSource.Default(md).Load(), NodeSceneTable.Default(md), 5);
		foreach (string id in ids)
		{
			session.Enter(id);
			if (session.Node!.Type != "merchant")
			{
				var d = session.WinFight(840);
				if (d != null) { var c = d.Offer.Cards[0]; d.Pick(0, c.Targets.Count > 0 ? c.Targets[0].Slot : null); }
			}
			session.CompleteNode();
		}
		return session;
	}

	static readonly string[] ToRow5 = { "r1-apprentice", "r2-oni", "r3-kappa", "r4-merchant" };

	static FightScene Fight(Node host, RunSession s, bool first, bool external, Func<InputBits>? held = null)
	{
		var f = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		f.Session = s; f.FirstMeeting = first; f.ExternalDrive = external;
		f.HeldBitsSource = held ?? (() => InputBits.None);
		host.AddChild(f);
		return f;
	}

	static FightHud HudOf(FightScene f) => f.GetNode<FightHud>("Hud");

	// Y3 / claim: while the intro card is up the fight waits. Drives the real _Process path (not ExternalDrive).
	[Test]
	public static void IntroCardUp_RealProcessLoop_DoesNotTickOrChangeHash(Node host)
	{
		var f = Fight(host, Walk(ToRow5).Also(s => s.Enter("r5-nine-tails")), false, false);
		try
		{
			Assert.True(f.TwistCard!.Showing && f.FlowPaused, "elder entry shows the card and pauses");
			int t0 = f.Match.Tick; ulong h0 = f.Match.StateHash();
			for (int i = 0; i < 90; i++) { OS.DelayMsec(17); f._Process(0.017); }
			Assert.Equal(t0, f.Match.Tick, "no ticks behind the card");
			Assert.True(h0 == f.Match.StateHash(), "hash unchanged behind the card");
			f.TwistCard.Continue();
			for (int i = 0; i < 30; i++) { OS.DelayMsec(17); f._Process(0.017); }
			Assert.True(f.Match.Tick > t0, "control: after the card the sim runs");
		}
		finally { f.QueueFree(); }
	}

	// Claim: _suppress = HeldBits() on close. Held Special / HeavyKick must not reach the sim until released; no press invented after release.
	[Test]
	public static void CardClosedWithSpecialAndKickHeld_NotDelivered_UntilReleased(Node host)
	{
		InputBits held = InputBits.Special | InputBits.HeavyKick;
		var f = Fight(host, Walk(ToRow5).Also(s => s.Enter("r5-nine-tails")), false, false, () => held);
		try
		{
			f.TwistCard!.Continue();
			for (int i = 0; i < 40; i++)
			{
				OS.DelayMsec(17); f._Process(0.017);
				Assert.True(f.Match.P1.State != FighterState.Attack, "held bits stay suppressed (tick " + f.Match.Tick + ")");
			}
			Assert.True(f.Match.Tick > 0, "control: the sim ran");
			Assert.Equal(InputBits.None, f.SuppressedInputForTest(held), "mask still hides the held bits");
			held = InputBits.None;
			for (int i = 0; i < 5; i++) { OS.DelayMsec(17); f._Process(0.017); }
			Assert.Equal(InputBits.None, f.SuppressedInputForTest(InputBits.None), "released: nothing delivered, no invented press");
			Assert.Equal(InputBits.HeavyKick, f.SuppressedInputForTest(InputBits.HeavyKick), "a fresh press after release is delivered");
		}
		finally { f.QueueFree(); }
	}

	// Claim: callout only on first meeting; exactly 3 s (180 ticks) of Match.Tick.
	[Test]
	public static void Callout_NotFirstMeeting_NeverShows(Node host)
	{
		var f = Fight(host, Walk(ToRow5).Also(s => s.Enter("r5-nine-tails")), false, true);
		try
		{
			f.TwistCard!.Continue();
			for (int i = 0; i < 10; i++) { f.Step(FighterInput.None, FighterInput.None); Assert.True(!HudOf(f).TwistCalloutVisible, "no callout, tick " + i); }
		}
		finally { f.QueueFree(); }
	}

	[Test]
	public static void Callout_FirstMeeting_Shows180Ticks_ThenGone(Node host)
	{
		var f = Fight(host, Walk(ToRow5).Also(s => s.Enter("r5-nine-tails")), true, true);
		try
		{
			f.TwistCard!.Continue();
			var hud = HudOf(f);
			int visible = hud.TwistCalloutVisible ? 1 : 0; // tick 0 counts: the banner is up the moment the card closes
			for (int i = 0; i < 260; i++)
			{
				f.Step(FighterInput.None, FighterInput.None);
				if (hud.TwistCalloutVisible) visible++;
			}
			Assert.Equal(FightHud.TwistCalloutTicks, visible, "visible for exactly 180 ticks (Tick 0..179) of Match.Tick");
			Assert.True(f.Match.Tick > 200 && !hud.TwistCalloutVisible, "gone after");
		}
		finally { f.QueueFree(); }
	}

	// Non-elder node: no card (Y3 is for elders only).
	[Test]
	public static void NonElderNode_ShowsNoCard_AndNoCallout(Node host)
	{
		var f = Fight(host, Walk().Also(s => s.Enter("r1-apprentice")), true, false);
		try
		{
			Assert.True(!f.TwistCard!.Showing && !f.FlowPaused, "duel: no intro card, not paused");
			Assert.True(!HudOf(f).TwistCalloutVisible, "no callout");
		}
		finally { f.QueueFree(); }
	}

	// HUD-level: a callout started at tick 300 on one match, then a new match (tick 0): must not come back at tick 300 of the new one.
	[Test]
	public static void Callout_NegativeAgeAfterNewMatch_DoesNotReappearLater(Node host)
	{
		var hud = GD.Load<PackedScene>("res://scenes/ui/hud.tscn").Instantiate<FightHud>();
		host.AddChild(hud);
		try
		{
			var m1 = new Match();
			for (int i = 0; i < 300; i++) m1.Step(FighterInput.None, FighterInput.None);
			hud.Refresh(m1);
			hud.ShowTwistCallout("NEW", "twist-armour");
			var m2 = new Match();
			hud.Refresh(m2);
			for (int i = 0; i < 400; i++) { m2.Step(FighterInput.None, FighterInput.None); hud.Refresh(m2); }
			Assert.True(!hud.TwistCalloutVisible, "a callout from the replaced match does not return at tick 300+ of the new one");
		}
		finally { hud.Free(); }
	}

	static FightHud ElderHud(Node host, Match m, ElderTwist t)
	{
		var hud = GD.Load<PackedScene>("res://scenes/ui/hud.tscn").Instantiate<FightHud>();
		hud.RunView = new Elder();
		host.AddChild(hud);
		m.P2.Twist = t;
		return hud;
	}
	sealed class Elder : IHudRunView { public bool InTanukiFight => false; public bool ElderFight => true; public CopyTarget? Copy => null; public bool CopyLocked => false; }

	// V1: strip text before first layout (Size.X == 0) must still end up readable (>= 26 px floor, not shrunk to nothing) once laid out.
	[Test]
	public static void Strip_BeforeLayout_DoesNotLockTheFontSmall(Node host)
	{
		var t = new ElderTwist("elder-x", "elder-oni", TwistEffect.SpecialArmour, 0, 1, "twist.x", "twist-armour", null, null);
		var saved = TwistCopy.Story;
		TwistCopy.Story = new StoryLibrary(new[] { new StoryCardData("elder-x-label", null, "Short", Array.Empty<string>(), "") });
		var hud = ElderHud(host, new Match(), t);
		try
		{
			var m = new Match { };
			m.P2.Twist = t;
			var label = hud.GetNode<Label>("Strip/TwistStrip/TwistText");
			hud.Refresh(m);
			int size = label.GetThemeFontSize("font_size");
			Assert.True(size >= 26, "font size never below the 26 px floor: " + size);
			Assert.Equal("Short", hud.TwistLine, "text shown");
		}
		finally { TwistCopy.Story = saved; hud.Free(); }
	}

	// FitStripText cache: a long line shrinks the font, then a different twist (shorter text, same width) must restore the size.
	[Test]
	public static void Strip_LongThenShortText_SameWidth_RestoresFontSize(Node host)
	{
		var longT = new ElderTwist("elder-l", "elder-oni", TwistEffect.SpecialArmour, 0, 1, "twist.l", null, null, null);
		var shortT = longT with { Id = "elder-s", LabelKey = "twist.s" };
		var saved = TwistCopy.Story;
		TwistCopy.Story = new StoryLibrary(new[] {
			new StoryCardData("elder-l-label", null, string.Concat(Enumerable.Repeat("a very long twist reminder ", 8)), Array.Empty<string>(), ""),
			new StoryCardData("elder-s-label", null, "Short", Array.Empty<string>(), "") });
		var hud = ElderHud(host, new Match(), longT);
		try
		{
			var m = new Match(); m.P2.Twist = longT;
			var label = hud.GetNode<Label>("Strip/TwistStrip/TwistText");
			int theme = label.GetThemeFontSize("font_size");
			hud.Refresh(m);
			int shrunk = label.GetThemeFontSize("font_size");
			if (label.Size.X <= 0) { Assert.True(true, "no layout in headless: inconclusive"); return; }
			Assert.True(shrunk < theme && shrunk >= 26, $"long text shrinks within the floor: {theme} -> {shrunk}");
			m.P2.Twist = shortT;
			hud.Refresh(m);
			Assert.Equal("Short", hud.TwistLine, "text follows the new twist");
			Assert.Equal(theme, label.GetThemeFontSize("font_size"), "short text gets the full size back");
		}
		finally { TwistCopy.Story = saved; hud.Free(); }
	}

	// Unknown icon id: the seal falls back to a glyph instead of throwing; the map node keeps the id and stays an elder.
	[Test]
	public static void UnknownIconId_FallsBackWithoutThrowing(Node host)
	{
		var seal = new TwistSeal { Size = new Vector2(60, 60), Icon = "twist-nonexistent" };
		host.AddChild(seal);
		Assert.True(!TwistSeal.HasShape("twist-nonexistent") && !TwistSeal.HasShape(""), "no shape for unknown/empty ids");
		seal.QueueFree();
		var stage = new Control { Size = new Vector2(1920, 1080) };
		host.AddChild(stage);
		var s = GD.Load<PackedScene>("res://scenes/ui/map_screen.tscn").Instantiate<MapScreen>();
		stage.AddChild(s);
		var v = new StubMapView().Add("a", 1, "duel", "Kitsune").Add("e", 2, "elder", "Elder X", true, "twist-nonexistent").Route("a", "e").Open("a");
		s.SetView(v);
		Assert.True(s.NodeFor("e")!.ElderShown, "still marked an elder");
		stage.QueueFree();
	}

	// Null card ids: every line flagged missing and empty (no invented wording), none throwing, also for an unnamed elder.
	[Test]
	public static void TwistWithNullCardIds_AllLinesMarkedPlaceholder_AndNullIconSafe(Node host)
	{
		foreach (var effect in Enum.GetValues<TwistEffect>())
		{
			var t = new ElderTwist("id", "no-such-elder", effect, 0, 0, "", null, null, null);
			foreach (var line in new[] { TwistCopy.Intro(t, TwistCopy.Story), TwistCopy.Callout(t, TwistCopy.Story, null), TwistCopy.Label(t, TwistCopy.Story, "") })
				Assert.True(line.Placeholder && line.Text.Length == 0, $"{effect}: '{line.Text}'");
		}
		var card = GD.Load<PackedScene>("res://scenes/ui/story_card.tscn").Instantiate<StoryCard>();
		host.AddChild(card);
		card.Display("x", "ELDER TWIST", null);
		Assert.Equal("", card.TwistIconId, "null icon: empty id, no throw");
		card.QueueFree();
	}

	static T Also<T>(this T v, Action<T> a) { a(v); return v; }
}
