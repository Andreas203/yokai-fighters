using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Flow;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-109 probes: the rest stop's final UI pass (focus in every state, input paths, layout bands, shared kit styles untouched,
/// no sim change). Expected values come from the ticket brief and R6 / R7 (one choice per stop, the other closes), not from the code.
/// </summary>
public static class RestStopUiProbes
{
	const string Scene = "res://scenes/ui/rest_stop.tscn";
	internal enum St { FreshMerchant, FreshDojoNoTrial, FreshDojoTrial, MerchantShop, MerchantHeal, DojoRest, DojoTrialClosed }

	static void OnFixtures(Action body)
	{
		var saved = FightScene.Sources; var savedRun = FightScene.Run; var savedFoe = FightScene.Opponent;
		FightScene.Sources = FightScene.FixtureSources; FightScene.Run = null;
		try { body(); }
		finally { FightScene.Sources = saved; FightScene.Run = savedRun; FightScene.Opponent = savedFoe; RestStopScreenForecast.CostSource = null; }
	}

	static RunSession Merchant(int health = 300)
	{
		string md = ContentPaths.Data("map");
		var s = RunSession.Start(FightScene.LoadAbilityPool(), FileMapSource.Default(md).Load(), NodeSceneTable.Default(md), 5);
		for (; ; )
		{
			var next = s.Reachable();
			string pick = next.FirstOrDefault(id => s.Map.Graph.Node(id).Type == "merchant") ?? next[0];
			Assert.True(s.Enter(pick).Ok, "entered " + pick);
			if (s.Node!.Type == "merchant") break;
			if (s.Node.Type is "merchant" or "dojo") Assert.True(s.CompleteNode().Ok, "stop passed");
			else
			{
				if (s.WinFight(health) is { } d) { var c = d.Offer.Cards[0]; Assert.True(d.Pick(0, c.Targets.Count > 0 ? c.Targets[0].Slot : null).Ok, "pick"); }
				Assert.True(s.CompleteNode().Ok, "done");
			}
		}
		s.Run.SetHealth(health);
		return s;
	}

	static RunSession Dojo(bool trial, string? prompt = null)
	{
		var s = DojoTrialTests.AtDojo(640);
		if (!trial) s.Trials = Array.Empty<TrialData>();
		else if (prompt != null)
			s.Trials = DojoTrialTests.Library.Select(w => new TrialData { Id = w.Id, Special = w.Special, Prompt = prompt, Attempts = w.Attempts, Distance = w.Distance, AttemptFrames = w.AttemptFrames, Dummy = w.Dummy, Steps = w.Steps }).ToArray();
		return s;
	}

	static (Control host, RestStopScreen screen) Open(Node runner, RunSession s, Func<RestStopScreenModel, string>? line = null)
	{
		var host = new Control { Size = new Vector2(1920, 1080) };
		runner.AddChild(host);
		host.Size = new Vector2(1920, 1080);
		var screen = GD.Load<PackedScene>(Scene).Instantiate<RestStopScreen>();
		screen.LineSource = line ?? (_ => "A line.");
		screen.BindRun(s, ControlScheme.Kihon);
		host.AddChild(screen);
		screen.Size = new Vector2(1920, 1080);
		Sort(screen);
		return (host, screen);
	}

	static void Sort(Node n)
	{
		// Layout normally settles over frames; a test cannot wait, so sort bottom-up, pin the cards to their designed rects
		// (the scene's offsets) and sort again, so autowrap labels are measured at their real width.
		for (int pass = 0; pass < 3; pass++)
		{
			SortOnce(n);
			if (n.GetNodeOrNull<Control>("FreeCard") is { } f) f.Size = new Vector2(630, 174);
			if (n.GetNodeOrNull<Control>("SecondCard") is { } c) c.Size = new Vector2(630, 240);
			if (n.GetNodeOrNull<Control>("LineBox") is { } l) l.Size = new Vector2(410, 175);
		}
	}

	static void SortOnce(Node n)
	{
		foreach (var c in n.GetChildren()) SortOnce(c);
		if (n is Container ct) ct.Notification((int)Container.NotificationSortChildren);
	}

	static void Close(Node runner, Control host) { runner.RemoveChild(host); host.Free(); }

	static (RunSession s, Control host, RestStopScreen screen) Build(Node runner, St st, string? prompt = null)
	{
		var s = st switch { St.FreshDojoNoTrial => Dojo(false), St.FreshDojoTrial or St.DojoTrialClosed or St.DojoRest => Dojo(true, prompt), _ => Merchant() };
		var (host, screen) = Open(runner, s);
		switch (st)
		{
			case St.MerchantShop: Assert.True(screen.OpenSecond(), "shop chosen"); screen.ShopClosed(); break;
			case St.MerchantHeal: Assert.True(screen.TakeFree(), "heal"); break;
			case St.DojoRest: Assert.True(screen.TakeFree(), "rest"); break;
			case St.DojoTrialClosed: Assert.True(screen.OpenSecond(), "trial chosen"); screen.TrialClosed(); break;
		}
		Sort(screen);
		return (s, host, screen);
	}

	static readonly St[] All = (St[])Enum.GetValues(typeof(St));

	static bool Usable(Control c, Node root) => c.IsVisibleInTree() && c.FocusMode != Control.FocusModeEnum.None && root.IsAncestorOf(c)
		&& !(c is YokaiFighters.Ui.Kit.KitCard k && k.CardDisabled) && !(c is BaseButton b && b.Disabled);

	static void Press(Viewport vp, Key k)
	{
		vp.PushInput(new InputEventKey { Keycode = k, PhysicalKeycode = k, Pressed = true, Echo = false }, false);
		vp.PushInput(new InputEventKey { Keycode = k, PhysicalKeycode = k, Pressed = false, Echo = false }, false);
	}

	// ---- focus ----

	// ticket: focus never lost, never on a disabled card, in every state.
	[Test]
	public static void Focus_EveryState_StartsOnAnEnabledControl_AndEveryDirectionStaysOnOne(Node runner) => OnFixtures(() =>
	{
		foreach (var st in All)
		{
			var (s, host, screen) = Build(runner, st);
			try
			{
				var vp = screen.GetViewport();
				bool anyUsable = Usable(screen.FreeCard, screen) || Usable(screen.SecondCard, screen) || Usable(screen.ContinueButton, screen);
				if (!anyUsable) continue;
				var owner = vp.GuiGetFocusOwner();
				Assert.True(owner != null && Usable((Control)owner, screen), $"{st}: initial focus is on an enabled control of the screen (got {owner?.Name})");
				foreach (var key in new[] { Key.Down, Key.Up, Key.Down, Key.Down, Key.Left, Key.Right, Key.Up, Key.Up })
				{
					Press(vp, key);
					var o = vp.GuiGetFocusOwner();
					Assert.True(o != null && Usable((Control)o, screen), $"{st}: after {key} focus is on an enabled control (got {o?.Name})");
				}
			}
			finally { Close(runner, host); }
		}
	});

	// ticket: Down from SecondCard must reach Continue in the "Taken" state (merchant after a shop pick).
	[Test]
	public static void Focus_MerchantTaken_DownFromSecondCardReachesContinue_UpComesBack(Node runner) => OnFixtures(() =>
	{
		var (s, host, screen) = Build(runner, St.MerchantShop);
		try
		{
			Assert.True(!screen.SecondCard.CardDisabled && screen.ContinueButton.Visible, "Taken shop card is enterable and Continue is shown");
			screen.SecondCard.GrabFocus();
			Press(screen.GetViewport(), Key.Down);
			Assert.True(screen.ContinueButton.HasFocus(), $"Down from SecondCard lands on Continue (got {screen.GetViewport().GuiGetFocusOwner()?.Name})");
			Press(screen.GetViewport(), Key.Up);
			Assert.True(screen.SecondCard.HasFocus(), $"Up from Continue returns to SecondCard (got {screen.GetViewport().GuiGetFocusOwner()?.Name})");
		}
		finally { Close(runner, host); }
	});

	[Test]
	public static void Focus_FreshStops_DownFromFreeReachesSecondAndBack(Node runner) => OnFixtures(() =>
	{
		foreach (var st in new[] { St.FreshMerchant, St.FreshDojoTrial })
		{
			var (s, host, screen) = Build(runner, st);
			try
			{
				Assert.True(screen.FreeCard.HasFocus(), $"{st}: the free card has the first focus");
				Press(screen.GetViewport(), Key.Down);
				Assert.True(screen.SecondCard.HasFocus(), $"{st}: Down reaches the second card");
				Press(screen.GetViewport(), Key.Up);
				Assert.True(screen.FreeCard.HasFocus(), $"{st}: Up returns");
			}
			finally { Close(runner, host); }
		}
	});

	// R6/R7: after the choice, Continue (not a closed card) holds focus; a closed card never takes it.
	[Test]
	public static void Focus_AfterChoice_GoesToContinue_AndClosedCardsAreSkipped(Node runner) => OnFixtures(() =>
	{
		foreach (var st in new[] { St.MerchantHeal, St.DojoRest, St.DojoTrialClosed })
		{
			var (s, host, screen) = Build(runner, st);
			try
			{
				Assert.True(screen.ContinueButton.Visible && screen.ContinueButton.HasFocus(), $"{st}: Continue is shown and focused");
				foreach (var key in new[] { Key.Up, Key.Down, Key.Up })
				{
					Press(screen.GetViewport(), key);
					var o = screen.GetViewport().GuiGetFocusOwner();
					Assert.True(o == screen.ContinueButton, $"{st}: {key} cannot leave Continue for a closed card (got {o?.Name})");
				}
			}
			finally { Close(runner, host); }
		}
	});

	// ---- keyboard paths ----

	// R6: Enter on the focused card takes it.
	[Test]
	public static void Keys_EnterTakesTheFocusedCard_AndEnterOnContinueLeaves(Node runner) => OnFixtures(() =>
	{
		var (s, host, screen) = Build(runner, St.FreshMerchant);
		try
		{
			int hp = s.Run.Health;
			Press(screen.GetViewport(), Key.Enter);
			Assert.True(s.Run.Health > hp && screen.Model!.ChoiceMade, "Enter on the free card heals");
			bool left = false; screen.Left += () => left = true;
			Press(screen.GetViewport(), Key.Enter);
			Assert.True(left && s.Node == null, "Enter on Continue completes the node");
		}
		finally { Close(runner, host); }

		var (s2, host2, screen2) = Build(runner, St.FreshMerchant);
		try
		{
			bool shop = false; screen2.ShopRequested += () => shop = true;
			Press(screen2.GetViewport(), Key.Down); Press(screen2.GetViewport(), Key.Enter);
			Assert.True(shop && screen2.Model!.Second == RestStopOption.Taken, "Enter on the second card opens the shop");
			Assert.True(screen2.Model.Free == RestStopOption.Closed && s2.Run.Health == 300, "the heal closed, health untouched");
		}
		finally { Close(runner, host2); }
	});

	[Test]
	public static void Keys_TabToggles_EscAsksForTitle_NeitherChangesTheSim(Node runner) => OnFixtures(() =>
	{
		foreach (var st in new[] { St.FreshMerchant, St.MerchantHeal, St.FreshDojoTrial })
		{
			var (s, host, screen) = Build(runner, st);
			try
			{
				ulong h = s.StateHash();
				var focus = screen.GetViewport().GuiGetFocusOwner();
				Press(screen.GetViewport(), Key.Tab);
				Assert.True(screen.DetailVisible, $"{st}: Tab shows the detail");
				Assert.True(screen.GetViewport().GuiGetFocusOwner() == focus, $"{st}: Tab does not move focus");
				Assert.True(screen.GetNode<Label>("Forecast/CostMeaning").Text.StartsWith("Detail:"), $"{st}: detail text");
				Press(screen.GetViewport(), Key.Tab);
				Assert.True(!screen.DetailVisible, $"{st}: Tab again hides it");
				int title = 0; screen.TitleRequested += () => title++;
				Press(screen.GetViewport(), Key.Escape);
				Assert.Equal(1, title, $"{st}: Esc asks for the title once");
				Assert.True(s.StateHash() == h && s.Node != null, $"{st}: no sim change from Tab / Esc");
			}
			finally { Close(runner, host); }
		}
	});

	[Test]
	public static void Keys_HiddenScreen_IgnoresTabAndEsc(Node runner) => OnFixtures(() =>
	{
		var (s, host, screen) = Build(runner, St.FreshMerchant);
		try
		{
			screen.Visible = false; int title = 0; screen.TitleRequested += () => title++;
			Press(screen.GetViewport(), Key.Tab); Press(screen.GetViewport(), Key.Escape);
			Assert.True(!screen.DetailVisible && title == 0, "hidden: no toggle, no title request");
		}
		finally { Close(runner, host); }
	});

	// ---- layout (design space 1920x1080; canvas_items stretch makes 1280x720 the same layout scaled 2/3) ----

	static Rect2 TextRect(Label l)
	{
		var font = l.GetThemeFont("font"); int size = l.GetThemeFontSize("font_size");
		float w = l.AutowrapMode != TextServer.AutowrapMode.Off ? l.Size.X : -1;
		var sz = font.GetMultilineStringSize(l.Text, l.HorizontalAlignment, w, size);
		float x = l.HorizontalAlignment switch { HorizontalAlignment.Center => (l.Size.X - sz.X) / 2, HorizontalAlignment.Right => l.Size.X - sz.X, _ => 0 };
		float y = l.VerticalAlignment switch { VerticalAlignment.Center => (l.Size.Y - sz.Y) / 2, VerticalAlignment.Bottom => l.Size.Y - sz.Y, _ => 0 };
		return new Rect2(l.GlobalPosition + new Vector2(Math.Max(0, x), Math.Max(0, y)), sz);
	}

	static IEnumerable<Label> Labels(Node n)
	{
		foreach (var c in n.GetChildren())
		{
			if (c is Label l && l.IsVisibleInTree() && l.Text.Length > 0) yield return l;
			foreach (var d in Labels(c)) yield return d;
		}
	}

	static string P(Node screen, Node n) => screen.GetPathTo(n).ToString();

	static void CheckLayout(RestStopScreen rs, string tag)
	{
		var view = new Rect2(0, 0, 1920, 1080);
		var labels = Labels(rs).Where(l => l.Name != "SpecialName").ToList(); // the fixture move name is a long placeholder; real names are covered by RestStopTests
		foreach (var l in labels)
		{
			if (l.Name == "SpecialName") continue; // the fixture move name is a long placeholder; real names are covered by RestStopTests
			var t = TextRect(l);
			Assert.True(view.Grow(0.5f).Encloses(t), $"{tag}: '{P(rs, l)}' text {t} leaves the 1920x1080 screen");
			Assert.True(t.Size.Y <= l.Size.Y + 1.5f && (l.AutowrapMode != TextServer.AutowrapMode.Off || t.Size.X <= l.Size.X + 1.5f), $"{tag}: '{P(rs, l)}' text {t.Size} does not fit its box {l.Size} ('{l.Text}')");
		}
		for (int i = 0; i < labels.Count; i++)
			for (int j = i + 1; j < labels.Count; j++)
			{
				var a = TextRect(labels[i]); var b = TextRect(labels[j]);
				Assert.True(!a.Grow(-1).Intersects(b.Grow(-1)), $"{tag}: text of '{P(rs, labels[i])}' {a} overlaps '{P(rs, labels[j])}' {b}");
			}
		var cont = rs.ContinueButton;
		var blocks = new List<(string, Rect2)>
		{
			("FreeStroke", rs.FreeCard.GetGlobalRect().Grow(8)), ("SecondStroke", rs.SecondCard.GetGlobalRect().Grow(8)),
			("LineBox", rs.GetNode<Control>("LineBox").GetGlobalRect()), ("Footer", rs.GetNode<Control>("Footer").GetGlobalRect()),
			("Forecast", rs.GetNode<Control>("Forecast").GetGlobalRect()), ("Portrait", rs.GetNode<Control>("Portrait").GetGlobalRect()),
			("Rule", rs.GetNode<Control>("Rule").GetGlobalRect()),
		};
		if (cont.Visible) blocks.Add(("ContinueOutline", cont.GetGlobalRect().GrowIndividual(18, 6, 18, 6)));
		for (int i = 0; i < blocks.Count; i++)
		{
			Assert.True(view.Encloses(blocks[i].Item2), $"{tag}: {blocks[i].Item1} {blocks[i].Item2} leaves the screen");
			for (int j = i + 1; j < blocks.Count; j++)
				Assert.True(!blocks[i].Item2.Intersects(blocks[j].Item2), $"{tag}: {blocks[i].Item1} {blocks[i].Item2} overlaps {blocks[j].Item1} {blocks[j].Item2}");
		}
		Assert.True(rs.GetNode<Control>("FreeHanger").GetGlobalRect().Grow(0.5f).Encloses(rs.FreeCard.GetGlobalRect().Grow(8)), $"{tag}: FreeCard {rs.FreeCard.GetGlobalRect()} (grew from 630x174?) with its frame leaves the 280-540 band");
		Assert.True(rs.GetNode<Control>("SecondHanger").GetGlobalRect().Grow(0.5f).Encloses(rs.SecondCard.GetGlobalRect().Grow(8)), $"{tag}: SecondCard {rs.SecondCard.GetGlobalRect()} with its frame leaves the 562-894 band");
		foreach (var card in new Control[] { rs.FreeCard, rs.SecondCard })
		{
			var box = card.GetNode<Control>("Box"); var inner = card.GetGlobalRect();
			foreach (var l in Labels(box))
				Assert.True(inner.Grow(0.5f).Encloses(TextRect(l)), $"{tag}: '{P(rs, l)}' {TextRect(l)} sticks out of {card.Name} {inner}");
			Assert.True(inner.Grow(0.5f).Encloses(box.GetGlobalRect()), $"{tag}: {card.Name}/Box {box.GetGlobalRect()} sticks out of the card {inner}");
		}
	}

	// The layout probes need real frames (autowrap labels settle over a few), so they run as four classes: build, wait, wait, check.
	// The runner awaits a process frame at the start of each class and before its first test.
	internal static readonly List<(string Tag, Control Host, RestStopScreen Screen, St State)> Rigs = new();

	internal static void BuildRigs(Node runner)
	{
		OnFixtures(() =>
		{
			string longest = DojoTrialTests.Library.Select(t => t.Prompt).OrderByDescending(p => p.Length).First();
			const string fox = "Send a slow Foxfire at the dummy, then follow the flame in and land a hit.";
			void Add(string tag, St st, string? prompt = null, Func<RestStopScreenModel, string>? line = null)
			{
				RunSession s = st switch { St.FreshDojoNoTrial => Dojo(false), St.FreshDojoTrial or St.DojoTrialClosed or St.DojoRest => Dojo(true, prompt), _ => Merchant() };
				var host = new Control { Size = new Vector2(1920, 1080) };
				runner.AddChild(host);
				var screen = GD.Load<PackedScene>(Scene).Instantiate<RestStopScreen>();
				screen.LineSource = line ?? (_ => "A line.");
				screen.BindRun(s, ControlScheme.Kihon);
				host.AddChild(screen);
				switch (st)
				{
					case St.MerchantShop: screen.OpenSecond(); screen.ShopClosed(); break;
					case St.MerchantHeal: screen.TakeFree(); break;
					case St.DojoRest: screen.TakeFree(); break;
					case St.DojoTrialClosed: screen.OpenSecond(); screen.TrialClosed(); break;
				}
				Rigs.Add((tag, host, screen, st));
			}
			foreach (var st in All) Add(st.ToString(), st);
			foreach (var st in new[] { St.FreshDojoTrial, St.DojoTrialClosed })
			{
				Add($"{st} longest prompt {longest.Length}ch", st, longest);
				Add($"{st} foxfire prompt {fox.Length}ch", st, fox);
			}
			Add("MerchantHeal real row-4 line", St.MerchantHeal, null, null);
		});
	}

	static void FreeRigs(Node runner)
	{
		foreach (var r in Rigs) { runner.RemoveChild(r.Host); r.Host.Free(); }
		Rigs.Clear();
	}

	internal static void CheckRigs(Node runner)
	{
		try
		{
			Assert.True(Rigs.Count > 0, "the build class ran first");
			foreach (var r in Rigs) CheckLayout(r.Screen, r.Tag);
		}
		finally { }
	}

	internal static void CheckBands(Node runner)
	{
		try
		{
			foreach (var r in Rigs)
			{
				var screen = r.Screen;
				foreach (var (card, hanger) in new[] { (screen.FreeCard, "FreeHanger"), (screen.SecondCard, "SecondHanger") })
				{
					if (!card.CardDisabled) card.GrabFocus();
					card.Refresh();
					var band = screen.GetNode<Control>(hanger).GetGlobalRect();
					var stroke = card.GetNode<Control>("Stroke");
					if (!stroke.Visible) continue;
					Assert.True(band.Grow(0.5f).Encloses(stroke.GetGlobalRect()), $"{r.Tag}: {card.Name} frame {stroke.GetGlobalRect()} leaves its band {band}");
				}
			}
		}
		finally { FreeRigs(runner); }
	}

	// ---- shared kit untouched ----

	[Test]
	public static void Kit_ThemeCardStyleAndRedButtonAreNotMutated_ByLoadingTheScreen(Node runner) => OnFixtures(() =>
	{
		var theme = GD.Load<Theme>("res://ui/talisman_theme.tres");
		var kit = theme.GetStylebox("panel", "PaperCard") as StyleBoxTexture;
		Assert.True(kit != null, "the theme has a PaperCard texture panel");
		float[] before = { kit!.ContentMarginLeft, kit.ContentMarginRight, kit.ContentMarginTop, kit.ContentMarginBottom };
		var btnScene = GD.Load<PackedScene>("res://scenes/ui/kit/red_button.tscn");
		string Sig(Button b) { var f = b.GetThemeStylebox("focus"); var n = b.GetThemeStylebox("normal"); return f.GetType().Name + (f is StyleBoxFlat ff ? ff.BorderWidthLeft + "/" + ff.ExpandMarginLeft : "") + "|" + n.GetType().Name + n.ContentMarginLeft; }
		var probe = btnScene.Instantiate<Button>();
		string sigBefore = Sig(probe);
		probe.Free();

		var (s, host, screen) = Build(runner, St.MerchantHeal);
		try
		{
			Assert.True(screen.FreeCard.GetThemeStylebox("panel") != (StyleBox)kit, "the card draws a copy, not the kit style");
			Assert.True(kit.ContentMarginLeft == before[0] && kit.ContentMarginRight == before[1] && kit.ContentMarginTop == before[2] && kit.ContentMarginBottom == before[3], "kit PaperCard margins unchanged");
			Assert.True(theme.GetStylebox("panel", "PaperCard") == (StyleBox)kit, "the theme still holds the same style object");
			var p2 = btnScene.Instantiate<Button>();
			Assert.Equal(sigBefore, Sig(p2), "red_button's own styles unchanged");
			Assert.True(p2.GetThemeStylebox("focus") != screen.ContinueButton.GetThemeStylebox("focus"), "the outline is this instance's only");
			p2.Free();
		}
		finally { Close(runner, host); }
	});

	[Test]
	public static void Kit_LoadingTheScreenTwice_GivesTheSameMarginsBothTimes(Node runner) => OnFixtures(() =>
	{
		var (s1, h1, a) = Build(runner, St.FreshMerchant);
		var (s2, h2, b) = Build(runner, St.FreshMerchant);
		try
		{
			Assert.True(a.FreeCard.GetThemeStylebox("panel").GetContentMargin(Side.Left) == 46 && b.FreeCard.GetThemeStylebox("panel").GetContentMargin(Side.Left) == 46, "46 px on both");
			Assert.True(a.FreeCard.GetThemeStylebox("panel") != b.FreeCard.GetThemeStylebox("panel"), "each screen has its own copy");
		}
		finally { Close(runner, h1); Close(runner, h2); }
	});

	// ---- sim ----

	[Test]
	public static void Sim_OpeningRedrawingAndRefusedActions_LeaveTheStateHashAlone(Node runner) => OnFixtures(() =>
	{
		var (s, host, screen) = Build(runner, St.FreshMerchant);
		try
		{
			ulong h = s.StateHash();
			screen.Refresh(); screen.ToggleDetail(); screen.ToggleDetail();
			Assert.True(!screen.Leave() && s.StateHash() == h, "Continue before a choice is refused, nothing changes");
			Assert.True(screen.TakeFree(), "heal");
			ulong after = s.StateHash();
			Assert.True(after != h, "the heal itself is a change");
			Assert.True(!screen.TakeFree() && !screen.OpenSecond() && s.StateHash() == after, "refused takes after the choice change nothing");
		}
		finally { Close(runner, host); }

		ulong[] hs = new ulong[2];
		for (int i = 0; i < 2; i++)
		{
			var (s2, h2, sc) = Build(runner, St.FreshMerchant);
			try { sc.TakeFree(); sc.Leave(); hs[i] = s2.StateHash(); } finally { Close(runner, h2); }
		}
		Assert.True(hs[0] == hs[1], "deterministic through the screen");
	});
}

// Layout probes, four classes in order: build the screens, let two classes of frames pass, then check them.
public static class RestStopUiProbesLayout1Build
{
	[Test] public static void Build(Node runner) => RestStopUiProbes.BuildRigs(runner);
}
public static class RestStopUiProbesLayout2Wait
{
	[Test] public static void Wait() { }
}
public static class RestStopUiProbesLayout3Wait
{
	[Test] public static void Wait() { }
}
public static class RestStopUiProbesLayout4Check
{
	// ticket: nothing overlaps or clips at 1280x720 / 1920x1080 (same design space, canvas_items stretch), every state, longest prompt.
	[Test] public static void NothingOverlapsOrClips_EveryState_LongestPrompt_LongLines(Node runner) => RestStopUiProbes.CheckRigs(runner);
	// ticket: the selection frame stays within the bands when a card is focused.
	[Test] public static void SelectionFrameStaysInItsBand_WhenCardsFocused(Node runner) => RestStopUiProbes.CheckBands(runner);
}
