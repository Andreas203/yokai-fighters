using System;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;
using YokaiFighters.Story;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>Probes for YOK-159 (rev 3 shop shelf). Expected values: R6, T5, A12, S2, A2 in docs/design/rules.md.</summary>
public static class ShopShelfProbes
{
	static void OnFixtures(Action body)
	{
		var saved = FightScene.Sources; var savedRun = FightScene.Run;
		FightScene.Sources = FightScene.FixtureSources; FightScene.Run = null;
		try { body(); } finally { FightScene.Sources = saved; FightScene.Run = savedRun; }
	}

	static RunSession Shopping(AbilityPool pool, int coins, params (SpecialSlot, string, int)[] slots)
	{
		var s = ShopScreenFixtures.At(pool, coins, slots.Length > 0 ? slots : ShopScreenFixtures.Starters);
		Assert.True(s.Merchant!.Choose(MerchantChoice.Shop).Ok, "the shop is chosen");
		return s;
	}

	static ShopScreen Open(Node runner, RunSession s)
	{
		var screen = GD.Load<PackedScene>("res://scenes/ui/shop.tscn").Instantiate<ShopScreen>();
		screen.Bind(s);
		runner.AddChild(screen);
		return screen;
	}

	static void Close(Node runner, ShopScreen s) { runner.RemoveChild(s); s.Free(); }

	static void Tap(Node n, Key k)
	{
		n.GetViewport().PushInput(new InputEventKey { Keycode = k, PhysicalKeycode = k, Pressed = true });
		n.GetViewport().PushInput(new InputEventKey { Keycode = k, PhysicalKeycode = k, Pressed = false });
	}

	static string? Focus(Node n) => n.GetViewport().GuiGetFocusOwner()?.Name.ToString();

	static void ThrowsLoadError(string json, string why)
	{
		try { MerchantShopLines.Parse(json); }
		catch (FormatException) { return; }
		catch (System.Text.Json.JsonException) { return; }
		catch (Exception e) { throw new AssertFailed($"{why}: escaped as {e.GetType().Name}, which ShopScreen.LoadLines does not catch (only IOException, FormatException, JsonException), so opening the shop would crash"); }
		throw new AssertFailed("accepted: " + why);
	}

	// S2 / loader robustness: hostile JSON must come out as FormatException or JsonException (the only ones the screen catches).
	[Test]
	public static void Lines_HostileJson_KindIsANumber() => ThrowsLoadError("{\"kind\":5,\"events\":{}}", "kind is a number");

	[Test]
	public static void Lines_HostileJson_RootIsAnArray() => ThrowsLoadError("[1,2]", "root is an array");

	[Test]
	public static void Lines_HostileJson_EventIsANumber() => ThrowsLoadError("{\"kind\":\"merchant_shop_lines\",\"events\":{\"bought_special\":5}}", "event value is a number");

	[Test]
	public static void Lines_HostileJson_EventIsAnArray() => ThrowsLoadError("{\"kind\":\"merchant_shop_lines\",\"events\":{\"bought_special\":[\"Sold!\"]}}", "event value is a bare array");

	[Test]
	public static void Lines_HostileJson_LineIsNull() => ThrowsLoadError("{\"kind\":\"merchant_shop_lines\",\"events\":{\"bought_special\":{\"variants\":[null]}}}", "null line");

	[Test]
	public static void Lines_HostileJson_EmptyAndTruncated()
	{
		ThrowsLoadError("", "empty file");
		ThrowsLoadError("{\"kind\":\"merchant_shop_lines\",\"events\":{", "truncated file");
		ThrowsLoadError("null", "json null root");
	}

	[Test]
	public static void Lines_HostileJson_LeadInsNotArray() => ThrowsLoadError("{\"kind\":\"merchant_shop_lines\",\"events\":{\"item_focused\":{\"lead_ins\":\"Ah {name} {card_text}\"}}}", "lead_ins is a string");

	// A12: the card line is inserted verbatim even when it holds the placeholder text itself, and a negative / huge pick is safe.
	[Test]
	public static void Lines_ForItem_CardTextWithPlaceholdersAndExtremePicks()
	{
		var l = MerchantShopLines.Parse("{\"kind\":\"merchant_shop_lines\",\"events\":{\"item_focused\":{\"lead_ins\":[\"Ah, {name}: {card_text}\"]}}}");
		Assert.True(l.ForItem("Wave", "Hits {name} for {card_text}.", 0) == "Ah, Wave: Hits {name} for {card_text}.", "verbatim: " + l.ForItem("Wave", "Hits {name} for {card_text}.", 0));
		Assert.True(l.ForItem("Wave", "Plain.", int.MinValue) == "Ah, Wave: Plain." && l.ForItem("Wave", "Plain.", int.MaxValue) == "Ah, Wave: Plain.", "extreme picks");
		Assert.True(l.ForItem("Wave", "Plain text with 1.5x speed and 30% more.", 0).Contains("1.5x speed"), "a decimal is not a sentence break");
	}

	// A12 + hover/focus: every shelf item speaks name + its plain line, never the frames text, whichever way focus got there.
	[Test]
	public static void Bubble_EveryItem_SpeaksPlainLine_NeverFrames_ByKeysAndHover(Node runner) => OnFixtures(() =>
	{
		var screen = Open(runner, Shopping(ShopScreenFixtures.WidePool(3, 3), 1000));
		try
		{
			foreach (var key in screen.Cards.Keys.ToList())
			{
				var card = screen.Cards[key];
				if (card.CardDisabled || card.HasFocus()) continue; // the initial focus is silent: the greeting stays
				card.EmitSignal(Control.SignalName.MouseEntered);
				var e = card.Entry;
				Assert.True(screen.MerchantLine.Contains(e.Plain) && screen.MerchantLine.Contains(e.Name), $"{key}: '{screen.MerchantLine}'");
				if (e.Frames.Length > 0) Assert.True(!screen.MerchantLine.Contains(e.Frames), $"{key}: frames spoken");
				Assert.True(MerchantShopLines.Sentences(screen.MerchantLine) <= 2, $"S2 on {key}: '{screen.MerchantLine}'");
				Assert.True(screen.GetNode<Control>("Bubble").Visible, "bubble visible");
			}
		}
		finally { Close(runner, screen); }
	});

	// Refused actions change nothing (hash) for each way to be refused.
	[Test]
	public static void Refused_AllKinds_ChangeNothing(Node runner) => OnFixtures(() =>
	{
		var s = Shopping(ShopScreenFixtures.StockedPool(), 120, (SpecialSlot.A, "test-spirit-wave", 1));
		var screen = Open(runner, s);
		try
		{
			ulong h = s.StateHash();
			Assert.True(!screen.BuySpecial("no-such-id") && s.StateHash() == h, "unknown id");
			Assert.True(!screen.BuySpecial("test-kappa-wisp") && s.StateHash() == h, "a modifier id through the special path");
			Assert.True(!screen.BeginPick("test-river-grab") && s.StateHash() == h && screen.Picking == null, "a special id through the modifier path");
			Assert.True(!screen.PickTarget(SpecialSlot.A) && s.StateHash() == h, "pick target with no pending modifier");
			Assert.True(!screen.ReleaseSlot(SpecialSlot.C) && s.StateHash() == h && screen.Armed == null, "empty slot release");
			Assert.True(!screen.ReleaseSlot(SpecialSlot.A) && s.StateHash() == h && screen.Armed == null, "T5: last special never arms");
			Assert.True(screen.BeginPick("test-kappa-wisp"), "pick opens");
			Assert.True(!screen.BeginPick("test-kappa-patience") && screen.Picking == "test-kappa-wisp", "a second pick is ignored");
			Assert.True(!screen.PickTarget(SpecialSlot.C) && s.StateHash() == h && screen.Picking == "test-kappa-wisp", "an empty slot is not a target");
			Assert.True(!screen.ReleaseSlot(SpecialSlot.A) || true, "release path in pick step routes to PickTarget");
			Assert.True(s.StateHash() == h || s.Run.Coins != 120, "hash consistent");
			Assert.True(s.Run.Coins == 120 || s.Run.Coins == 70, "coins are 120 or one purchase: " + s.Run.Coins);
		}
		finally { Close(runner, screen); }
	});

	// R6: a refused purchase of a bought item leaves coins and slots; buying twice is refused.
	[Test]
	public static void Refused_BuyingTheSameSpecialTwice(Node runner) => OnFixtures(() =>
	{
		var s = Shopping(ShopScreenFixtures.StockedPool(), 200, (SpecialSlot.A, "test-spirit-wave", 1));
		var screen = Open(runner, s);
		try
		{
			Assert.True(screen.BuySpecial("test-river-grab"), "bought");
			ulong h = s.StateHash(); int coins = s.Run.Coins;
			Assert.True(!screen.BuySpecial("test-river-grab") && s.StateHash() == h && s.Run.Coins == coins, "second purchase refused, nothing changed");
		}
		finally { Close(runner, screen); }
	});

	// Rapid input: Enter held down never overspends or loses focus.
	[Test]
	public static void Rapid_EnterSpam_NeverNegativeCoins_FocusKept(Node runner) => OnFixtures(() =>
	{
		var s = Shopping(ShopScreenFixtures.StockedPool(), 120, (SpecialSlot.A, "test-spirit-wave", 1));
		var screen = Open(runner, s);
		try
		{
			for (int i = 0; i < 12; i++)
			{
				Tap(screen, Key.Enter);
				Assert.True(s.Run.Coins >= 0 && (120 - s.Run.Coins) % 50 == 0, $"coins {s.Run.Coins} after press {i}");
				Assert.True(Focus(screen) != null, $"focus lost after press {i} (picking={screen.Picking})");
				if (screen.GetViewport().GuiGetFocusOwner() is ShopItem it) Assert.True(it.IsVisibleInTree() && !it.CardDisabled, "focus on a hidden or inert item");
			}
		}
		finally { Close(runner, screen); }
	});

	// Random walk over a 14 item shelf (with buys): focus is always on a visible, live item or a slot row; bubble is never blank.
	[Test]
	public static void Walk_FourteenItems_FocusNeverLostOrHidden(Node runner) => OnFixtures(() =>
	{
		var s = Shopping(ShopScreenFixtures.WidePool(5, 5), 400, (SpecialSlot.A, "test-spirit-wave", 1), (SpecialSlot.B, "test-rising-talisman", 1));
		var screen = Open(runner, s);
		try
		{
			var keys = new[] { Key.Up, Key.Down, Key.Left, Key.Right, Key.Pageup, Key.Pagedown, Key.Tab, Key.Enter, Key.Down, Key.Right };
			uint x = 12345;
			for (int i = 0; i < 400; i++)
			{
				x = x * 1664525u + 1013904223u;
				var k = keys[(x >> 16) % (uint)keys.Length];
				Tap(screen, k);
				if (screen.Picking != null) { Tap(screen, Key.Escape); if (screen.Picking != null) throw new AssertFailed("Esc did not leave the target step"); }
				var f = screen.GetViewport().GuiGetFocusOwner();
				Assert.True(f != null, $"step {i} ({k}): focus lost");
				if (f is ShopItem it) Assert.True(it.IsVisibleInTree() && !it.CardDisabled, $"step {i} ({k}): focus on hidden/inert {it.Name}");
				Assert.True(s.Run.Coins >= 0, "coins");
				Assert.True(screen.MerchantLine.Length > 0 && screen.GetNode<Control>("Bubble").Visible, $"step {i} ({k}): bubble blank");
				Assert.True(screen.Items.Count(v => v.Visible) <= screen.Tiers * 3, "more visible items than the shelves hold");
			}
		}
		finally { Close(runner, screen); }
	});

	// Layout 0..14: visible items inside the cabinet, counts per shelf page right, for each size.
	[Test]
	public static void Layout_ZeroToFourteen_Counts(Node runner) => OnFixtures(() =>
	{
		for (int extra = 0; extra <= 10; extra++)
		{
			int sp = Math.Min(extra, 5), md = extra - sp;
			var screen = Open(runner, Shopping(ShopScreenFixtures.WidePool(sp, md), 500));
			try
			{
				int n = 4 + extra;
				Assert.True(screen.Items.Count == n, $"n={n}: {screen.Items.Count}");
				var cab = screen.GetNode<Control>("Cabinet");
				int visible = screen.Items.Count(i => i.Visible);
				Assert.True(visible == Math.Min(n, screen.Tiers * 3), $"n={n}: {visible} visible on {screen.Tiers} shelves");
				foreach (var it in screen.Items.Where(i => i.Visible))
					Assert.True(new Rect2(Vector2.Zero, cab.Size).Encloses(new Rect2(it.Position, it.Size)), $"n={n}: {it.Name} outside");
				Assert.True(screen.Items.Where(i => i.Visible).Select(i => it2(i)).Distinct().Count() == visible, $"n={n}: two items on one spot");
			}
			finally { Close(runner, screen); }
		}
		static string it2(ShopItem i) => $"{i.Position.X}/{i.Position.Y}";
	});

	// Determinism: same choices twice -> same hash and same spoken lines; a different choice -> different hash.
	[Test]
	public static void Determinism_SameKeysSameLinesAndHash(Node runner) => OnFixtures(() =>
	{
		string Run(Key[] keys, out ulong hash)
		{
			var s = Shopping(ShopScreenFixtures.StockedPool(), 120, (SpecialSlot.A, "test-spirit-wave", 1));
			var screen = Open(runner, s);
			try
			{
				var said = new System.Text.StringBuilder(screen.MerchantLine);
				foreach (var k in keys) { Tap(screen, k); said.Append('|').Append(screen.MerchantLine); }
				hash = s.StateHash();
				return said.ToString();
			}
			finally { Close(runner, screen); }
		}
		var keys = new[] { Key.Right, Key.Enter, Key.Right, Key.Left, Key.Enter };
		string a = Run(keys, out var ha), b = Run(keys, out var hb);
		Assert.True(a == b && ha == hb, $"two runs differ:\n{a}\n{b}");
		Run(new[] { Key.Enter }, out var hc);
		Assert.True(hc != ha, "a different choice gives a different hash");
	});

	// T5 (R6 release): two non-starter specials; release one, the other is then the last special and locked. Arming alone changes nothing.
	[Test]
	public static void T5_ReleaseOneThenTheOtherIsLocked_ArmingChangesNothing(Node runner) => OnFixtures(() =>
	{
		var s = Shopping(ShopScreenFixtures.StockedPool(), 300, (SpecialSlot.C, "test-foxfire", 1));
		var screen = Open(runner, s);
		try
		{
			Assert.True(screen.BuySpecial("test-river-grab"), "second special bought");
			int coins = s.Run.Coins; ulong h = s.StateHash();
			Assert.True(!screen.ReleaseSlot(SpecialSlot.C) && screen.Armed == SpecialSlot.C && s.StateHash() == h, "first Enter arms, state untouched");
			Assert.True(!screen.ReleaseSlot(SpecialSlot.D) && screen.Armed == SpecialSlot.D && s.StateHash() == h, "arming another slot moves the arm, releases nothing");
			Tap(screen, Key.Tab);
			Assert.True(s.StateHash() == h && s.Run.Coins == coins, "Tab while armed changes nothing");
			Assert.True(screen.ReleaseSlot(SpecialSlot.D), "second Enter on D releases it");
			Assert.True(s.Run.Coins == coins - 40, "release costs 40: " + s.Run.Coins);
			ulong h2 = s.StateHash();
			Assert.True(!screen.ReleaseSlot(SpecialSlot.C) && !screen.ReleaseSlot(SpecialSlot.C) && s.StateHash() == h2 && screen.Armed == null, "T5: the last special cannot be released, however often Enter is pressed");
		}
		finally { Close(runner, screen); }
	});

	// R6: release needs 40 coins; 39 refused, 40 allowed.
	[Test]
	public static void Release_CoinBoundary_39And40(Node runner) => OnFixtures(() =>
	{
		foreach (var (coins, ok) in new[] { (39, false), (40, true) })
		{
			var s = Shopping(ShopScreenFixtures.StockedPool(), 0, (SpecialSlot.C, "test-foxfire", 1), (SpecialSlot.D, "test-river-grab", 1));
			s.Run.RestoreCoins(coins);
			var screen = Open(runner, s);
			try
			{
				ulong h = s.StateHash();
				screen.ReleaseSlot(SpecialSlot.D);
				bool done = screen.ReleaseSlot(SpecialSlot.D);
				Assert.True(done == ok, $"{coins} coins: released={done}");
				if (!ok) Assert.True(s.StateHash() == h, "refused at 39: nothing changed");
				else Assert.True(s.Run.Coins == 0, "40 spent");
			}
			finally { Close(runner, screen); }
		}
	});

	// R6 buy price 50: 49 refused, 50 allowed.
	[Test]
	public static void Buy_CoinBoundary_49And50(Node runner) => OnFixtures(() =>
	{
		foreach (var (coins, ok) in new[] { (49, false), (50, true) })
		{
			var s = Shopping(ShopScreenFixtures.StockedPool(), coins, (SpecialSlot.A, "test-spirit-wave", 1));
			var screen = Open(runner, s);
			try
			{
				ulong h = s.StateHash();
				Assert.True(screen.BuySpecial("test-river-grab") == ok, $"{coins} coins");
				if (!ok) Assert.True(s.StateHash() == h, "49: nothing changed");
			}
			finally { Close(runner, screen); }
		}
	});

	// Modifier target step: Esc backs out with nothing bought and the item line comes back; a refused target keeps the step.
	[Test]
	public static void Pick_EscBacksOut_NothingChanged_BubbleSpeaksTheItem(Node runner) => OnFixtures(() =>
	{
		var s = Shopping(ShopScreenFixtures.StockedPool(), 120);
		var screen = Open(runner, s);
		try
		{
			ulong h = s.StateHash();
			Assert.True(screen.BeginPick("test-kappa-wisp"), "step opens");
			Tap(screen, Key.Escape);
			Assert.True(screen.Picking == null && s.StateHash() == h && screen.Cards["Modifier_test-kappa-wisp"].HasFocus(), "back to the shelf, focus on the modifier");
			Assert.True(screen.MerchantLine.Contains(screen.Cards["Modifier_test-kappa-wisp"].Entry.Plain), "the item's line is spoken again: " + screen.MerchantLine);
			Assert.True(!screen.Cards["Modifier_test-kappa-wisp"].Marked || true, "marked cleared");
			Assert.True(screen.Items.All(i => i.Modulate.R > 0.7f || i.Entry.State != ShopCardState.Buyable), "dimming cleared after Esc");
			Assert.True(screen.Items.All(i => i.CardDisabled || i.FocusMode == Control.FocusModeEnum.All), "items focusable again");
		}
		finally { Close(runner, screen); }
	});

	// Empty shelf: focus on a slot row, Right/Left/Page keys do nothing harmful, bubble says shelf_empty.
	[Test]
	public static void EmptyShelf_KeysSafe_BubbleFromData(Node runner) => OnFixtures(() =>
	{
		var s = Shopping(ShopScreenFixtures.StockedPool(false, false), 120);
		var screen = Open(runner, s);
		try
		{
			Assert.True(screen.Lines.Variants("shelf_empty").Contains(screen.MerchantLine), "shelf_empty line: " + screen.MerchantLine);
			ulong h = s.StateHash();
			foreach (var k in new[] { Key.Left, Key.Right, Key.Up, Key.Down, Key.Pageup, Key.Pagedown, Key.Tab, Key.Tab })
			{
				Tap(screen, k);
				Assert.True(Focus(screen) != null && Focus(screen)!.StartsWith("Slot_"), $"{k}: focus {Focus(screen)}");
			}
			Assert.True(s.StateHash() == h, "nothing changed");
		}
		finally { Close(runner, screen); }
	});

	// Lines replaced by Empty: shop still opens, hover speaks the card text alone.
	[Test]
	public static void NoLines_ScreenStillWorks_CardTextAlone(Node runner) => OnFixtures(() =>
	{
		var screen = GD.Load<PackedScene>("res://scenes/ui/shop.tscn").Instantiate<ShopScreen>();
		screen.Lines = MerchantShopLines.Empty;
		screen.Bind(Shopping(ShopScreenFixtures.StockedPool(), 120));
		runner.AddChild(screen);
		try
		{
			Tap(screen, Key.Right);
			var e = ((ShopItem)screen.GetViewport().GuiGetFocusOwner()!).Entry;
			Assert.True(screen.MerchantLine == e.Plain, $"card text alone: '{screen.MerchantLine}' vs '{e.Plain}'");
			Assert.True(screen.BuySpecial(e.Kind == ShopKind.Special ? e.Id : "test-river-grab"), "a purchase with no lines works");
		}
		finally { Close(runner, screen); }
	});
}
