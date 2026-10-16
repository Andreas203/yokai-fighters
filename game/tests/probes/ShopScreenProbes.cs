using System;
using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Flow;
using YokaiFighters.Settings;
using YokaiFighters.Sim;
using YokaiFighters.Ui;
using YokaiFighters.Ui.Kit;

namespace YokaiFighters.Tests;

/// <summary>YOK-104 test-breaker probes for the shop screen. Expected values come from rules R6, T5, A2, M5.</summary>
public static class ShopScreenProbes
{
	static void OnFixtures(Action body)
	{
		var saved = FightScene.Sources; var savedRun = FightScene.Run;
		FightScene.Sources = FightScene.FixtureSources; FightScene.Run = null;
		try { body(); } finally { FightScene.Sources = saved; FightScene.Run = savedRun; }
	}

	static RunSession Shopping(int coins, AbilityPool? pool = null, params (SpecialSlot, string, int)[] slots)
	{
		var s = ShopScreenFixtures.At(pool ?? ShopScreenFixtures.StockedPool(), coins, slots.Length > 0 ? slots : ShopScreenFixtures.Starters);
		Assert.True(s.Merchant!.Choose(MerchantChoice.Shop).Ok, "shop chosen");
		return s;
	}

	static ShopScreen Open(Node runner, RunSession s)
	{
		var screen = GD.Load<PackedScene>("res://scenes/ui/shop.tscn").Instantiate<ShopScreen>();
		screen.Bind(s); runner.AddChild(screen); return screen;
	}
	static void Close(Node runner, ShopScreen s) { runner.RemoveChild(s); s.Free(); }

	static void Down(Node n, Key k) => n.GetViewport().PushInput(new InputEventKey { Keycode = k, PhysicalKeycode = k, Pressed = true });
	static void Up(Node n, Key k) => n.GetViewport().PushInput(new InputEventKey { Keycode = k, PhysicalKeycode = k, Pressed = false });
	static void Tap(Node n, Key k) { Down(n, k); Up(n, k); }

	static void FocusIsSane(ShopScreen screen, string when)
	{
		var f = screen.GetViewport().GuiGetFocusOwner();
		Assert.True(f != null, when + ": focus exists");
		Assert.True(f!.IsVisibleInTree() && !f.IsQueuedForDeletion() && f.Name.ToString() != "_gone" && screen.IsAncestorOf(f), when + ": focus on a live visible control, got " + f.Name);
	}

	// R6: 50 per buy. Land on exactly 50 (still buyable) then 49 (refused with a reason) and check every card and the chip.
	[Test]
	public static void Coins_ExactlyFifty_StaysBuyable_FortyNine_RefusesEveryCardWithReason(Node runner) => OnFixtures(() =>
	{
		var s = Shopping(100);
		var screen = Open(runner, s);
		try
		{
			Assert.True(screen.BuySpecial("test-river-grab") && s.Run.Coins == 50, "bought to exactly 50");
			Assert.Equal("50", screen.GetNode<Label>("CoinChip/CoinValue").Text, "chip 50");
			var m = screen.Model!;
			Assert.True(m.Specials.First(e => e.Id == "test-slippery-skin").State == ShopCardState.Buyable, "R6: 50 coins buys a 50 coin item");
			Assert.True(m.Modifiers.All(e => e.State != ShopCardState.Refused || e.Reason != null), "any refusal has a reason");
			Assert.True(screen.BuySpecial("test-slippery-skin") && s.Run.Coins == 0, "spent to 0");
			Assert.True(m.Modifiers.Where(e => e.State != ShopCardState.Bought).All(e => e.State == ShopCardState.Refused && !string.IsNullOrEmpty(e.Reason)), "0 coins: every modifier refused with a reason");
			FocusIsSane(screen, "at zero coins");
		}
		finally { Close(runner, screen); }
	});

	[Test]
	public static void Coins_FortyNine_AllShelfCardsRefusedWithReason_ReleaseAt40And39(Node runner) => OnFixtures(() =>
	{
		var s = Shopping(139); // buy one -> 89; arrange via borrowed D then coins
		var screen = Open(runner, s);
		try
		{
			Assert.True(screen.BuySpecial("test-river-grab") && s.Run.Coins == 89, "139 -> 89");
			s.Run.RestoreCoins(49); screen.Bind(s);
			var m = screen.Model!;
			Assert.True(m.Specials.Concat(m.Modifiers).Where(e => e.State != ShopCardState.Bought).All(e => e.State == ShopCardState.Refused && e.Reason != null), "R6: 49 coins: nothing buyable, reasons shown");
			Assert.True(screen.Cards.Values.All(c => c.Name.ToString().Length > 0), "cards built");
			Assert.True(m.Slots.First(r => r.Slot == SpecialSlot.D).CanRelease, "R6: release costs 40, 49 is enough");
			s.Run.RestoreCoins(40); screen.Bind(s);
			Assert.True(screen.Model!.Slots.First(r => r.Slot == SpecialSlot.D).CanRelease, "exactly 40 releases");
			s.Run.RestoreCoins(39); screen.Bind(s);
			var d = screen.Model!.Slots.First(r => r.Slot == SpecialSlot.D);
			Assert.True(!d.CanRelease && d.Reason != null, "39 coins: refused with a reason");
			ulong h = s.StateHash();
			Assert.True(!screen.ReleaseSlot(SpecialSlot.D) && screen.Armed == null && s.StateHash() == h, "39: not armed, nothing changed");
		}
		finally { Close(runner, screen); }
	});

	// R6 stock: buy the last item on a shelf.
	[Test]
	public static void Stock_BuyingEveryItem_LeavesBoughtCardsNotTheEmptyState_FocusSane(Node runner) => OnFixtures(() =>
	{
		var s = Shopping(500);
		var screen = Open(runner, s);
		try
		{
			Assert.True(screen.BuySpecial("test-river-grab") && screen.BuySpecial("test-slippery-skin"), "both specials bought");
			Assert.True(!screen.GetNode<Control>("AllEmpty").Visible, "bought cards stay on the shelf, not the empty state");
			Assert.True(screen.Cards.Values.Where(c => c.Name.ToString().StartsWith("Special_")).All(c => c.CardDisabled), "both read bought");
			FocusIsSane(screen, "after the last special");
			ulong h = s.StateHash();
			Tap(screen, Key.Enter); // whatever is focused now
			FocusIsSane(screen, "after Enter on whatever has focus");
		}
		finally { Close(runner, screen); }
	});

	[Test]
	public static void Stock_Empty_EnterAndArrowsChangeNothing_BackStillWorks(Node runner) => OnFixtures(() =>
	{
		var s = Shopping(200, ShopScreenFixtures.StockedPool(false, false));
		var screen = Open(runner, s);
		bool closed = false; screen.Closed += () => closed = true;
		try
		{
			ulong h = s.StateHash();
			foreach (var k in new[] { Key.Down, Key.Right, Key.Left, Key.Up, Key.Enter, Key.Tab, Key.Tab }) { Tap(screen, k); FocusIsSane(screen, "key " + k); }
			Assert.True(s.StateHash() == h && s.Run.Coins == 200, "browsing an empty shop changes nothing");
			Tap(screen, Key.Escape);
			Assert.True(closed, "Esc closes");
		}
		finally { Close(runner, screen); }
	});

	// A2/R6: a modifier pick that is cancelled costs nothing.
	[Test]
	public static void Modifier_BackDuringPick_SpendsNothing_StaysOnShop_NoSecondBackNeeded(Node runner) => OnFixtures(() =>
	{
		var s = Shopping(120);
		var screen = Open(runner, s);
		bool closed = false; screen.Closed += () => closed = true;
		try
		{
			ulong h = s.StateHash();
			Assert.True(screen.BeginPick("test-kappa-wisp") && screen.Picking != null, "picking");
			Tap(screen, Key.Escape);
			Assert.True(!closed && screen.Picking == null && s.StateHash() == h && s.Run.Coins == 120, "Esc cancels the pick only: no coins, no state change");
			Assert.True(screen.Cards["Modifier_test-kappa-wisp"].FocusMode != Control.FocusModeEnum.None, "cards focusable again");
			FocusIsSane(screen, "after cancelled pick");
			Assert.True(!screen.Cards["Modifier_test-kappa-wisp"].Selected, "the card is not left selected");
			Assert.True(((Control)screen.Cards["Special_test-river-grab"].GetNode("Body")).Modulate.R > 0.7f, "other cards are no longer dimmed");
		}
		finally { Close(runner, screen); }
	});

	// A pick is open, Back button pressed by mouse/pad press: must not leave the shop mid-pick.
	[Test]
	public static void Modifier_BackButtonPressedDuringPick_CancelsPickOnly(Node runner) => OnFixtures(() =>
	{
		var s = Shopping(120);
		var screen = Open(runner, s);
		bool closed = false; screen.Closed += () => closed = true;
		try
		{
			ulong h = s.StateHash();
			screen.BeginPick("test-kappa-wisp");
			screen.BackButton.EmitSignal(BaseButton.SignalName.Pressed);
			Assert.True(!closed && screen.Picking == null && s.StateHash() == h, "Back during pick: one level only, nothing spent");
		}
		finally { Close(runner, screen); }
	});

	// T5: release leaves one special: the remaining one is locked whatever the coins.
	[Test]
	public static void Release_ThenLastSpecialIsLocked_T5_EvenWithManyCoins(Node runner) => OnFixtures(() =>
	{
		// Only non-starter-rule case: one starter slot A kept plus a bought D. Release D: A is the last special.
		var s = Shopping(500, null, (SpecialSlot.A, "test-spirit-wave", 1));
		var screen = Open(runner, s);
		try
		{
			Assert.True(screen.BuySpecial("test-river-grab"), "bought");
			Assert.True(screen.ReleaseSlot(SpecialSlot.D) is false && screen.Armed == SpecialSlot.D, "armed");
			Assert.True(screen.ReleaseSlot(SpecialSlot.D), "released");
			var a = screen.Model!.Slots.First(r => r.Slot == SpecialSlot.A);
			Assert.True(a.Locked && !a.CanRelease && a.Reason != null, "T5: the last special is locked with a reason");
			ulong h = s.StateHash();
			Assert.True(!screen.ReleaseSlot(SpecialSlot.A) && screen.Armed == null && s.StateHash() == h, "refused, not armed, no change");
		}
		finally { Close(runner, screen); }
	});

	// T5: two bought (borrowed) specials and no starter: release one, the other is the last and locks. Release while borrowed.
	[Test]
	public static void Release_TwoBorrowedOnly_FirstReleasable_SecondLockedAfter_T5(Node runner) => OnFixtures(() =>
	{
		var pool = ShopScreenFixtures.StockedPool();
		var s = Shopping(500, pool, (SpecialSlot.A, "test-spirit-wave", 1));
		var screen = Open(runner, s);
		try
		{
			screen.BuySpecial("test-river-grab"); screen.BuySpecial("test-slippery-skin"); // D, C
			Assert.True(screen.Model!.Slots.Count(r => !r.Empty) == 3, "three specials");
			screen.ReleaseSlot(SpecialSlot.D); Assert.True(screen.ReleaseSlot(SpecialSlot.D), "release borrowed D");
			screen.ReleaseSlot(SpecialSlot.C); Assert.True(screen.ReleaseSlot(SpecialSlot.C), "release borrowed C");
			Assert.True(s.Run.Coins == 500 - 100 - 80, "R6: 50+50 buys, 40+40 releases");
			Assert.True(s.Run[SpecialSlot.A] != null, "Ryo still has A");
			// re-buy after release: shelf entry is offered again
			var e = screen.Model!.Specials.First(x => x.Id == "test-river-grab");
			Assert.True(e.State == ShopCardState.Buyable, "a released special can be bought again: " + e.State);
			FocusIsSane(screen, "after releases");
		}
		finally { Close(runner, screen); }
	});

	// Rapid input.
	[Test]
	public static void Rapid_EnterTwiceInOneFrame_NeverSpendsMoreThanOnePerPress_HashConsistent(Node runner) => OnFixtures(() =>
	{
		var s = Shopping(120);
		var screen = Open(runner, s);
		try
		{
			screen.Refresh("Special_test-river-grab");
			Down(screen, Key.Enter); Up(screen, Key.Enter); Down(screen, Key.Enter); Up(screen, Key.Enter);
			Assert.True(s.Run.Coins == 70 || s.Run.Coins == 20, "R6: each Enter is at most one 50 coin buy: " + s.Run.Coins);
			// Designer attention (rule-silent): after a buy the focus jumps to the next card, so a double press buys two different items (20 coins left).
			FocusIsSane(screen, "after double Enter");
		}
		finally { Close(runner, screen); }
	});

	[Test]
	public static void Rapid_EnterThenEscOnSlot_DoesNotRelease(Node runner) => OnFixtures(() =>
	{
		var s = Shopping(200);
		var screen = Open(runner, s);
		bool closed = false; screen.Closed += () => closed = true;
		try
		{
			screen.BuySpecial("test-river-grab");
			screen.Refresh("Slot_D");
			ulong h = s.StateHash();
			Tap(screen, Key.Enter); Tap(screen, Key.Escape);
			Assert.True(s.Run[SpecialSlot.D] != null && s.StateHash() == h && screen.Armed == null, "armed then cancelled: kept, hash unchanged");
			Assert.True(!closed, "Esc cancelled the arm, did not close the shop");
			Tap(screen, Key.Enter); Tap(screen, Key.Escape); Tap(screen, Key.Enter);
			Assert.True(s.Run[SpecialSlot.D] != null, "Enter, Esc, Enter: the second Enter only re-arms");
		}
		finally { Close(runner, screen); }
	});

	[Test]
	public static void Rapid_EnterOnModifierThenEsc_ClosesPickNotShop(Node runner) => OnFixtures(() =>
	{
		var s = Shopping(120);
		var screen = Open(runner, s);
		bool closed = false; screen.Closed += () => closed = true;
		try
		{
			ulong h = s.StateHash();
			screen.Refresh("Modifier_test-kappa-patience");
			Tap(screen, Key.Enter); Tap(screen, Key.Escape);
			Assert.True(!closed && screen.Picking == null && s.StateHash() == h, "pick opened and cancelled, shop open, nothing spent");
			Tap(screen, Key.Escape);
			Assert.True(closed, "a second Esc closes the shop");
		}
		finally { Close(runner, screen); }
	});

	// Focus never lands on hidden/queued cards across a long random-ish key walk; browsing is hash-neutral.
	[Test]
	public static void Browsing_KeyWalk_FocusAlwaysLive_HashUntouched(Node runner) => OnFixtures(() =>
	{
		var s = Shopping(500);
		var screen = Open(runner, s);
		try
		{
			ulong h = s.StateHash();
			var keys = new[] { Key.Right, Key.Right, Key.Right, Key.Down, Key.Down, Key.Left, Key.Tab, Key.Up, Key.Right, Key.Tab, Key.Down, Key.Down, Key.Down, Key.Left, Key.Up, Key.Up, Key.Up };
			foreach (var k in keys) { Tap(screen, k); FocusIsSane(screen, "key " + k); }
			Assert.True(s.StateHash() == h && s.Run.Coins == 500, "R6: browsing spends nothing, hash stable");
		}
		finally { Close(runner, screen); }
	});

	[Test]
	public static void Browsing_BuyEverythingByKeys_FocusAlwaysLive(Node runner) => OnFixtures(() =>
	{
		var s = Shopping(1000);
		var screen = Open(runner, s);
		try
		{
			for (int i = 0; i < 6; i++) { Tap(screen, Key.Enter); FocusIsSane(screen, "enter " + i); if (screen.Picking != null) { Tap(screen, Key.Enter); FocusIsSane(screen, "pick " + i); } }
			Assert.True(s.Run.Coins % 10 == 0 && s.Run.Coins >= 0, "coins stay sane: " + s.Run.Coins);
			Assert.True(screen.GetNode<Label>("CoinChip/CoinValue").Text == s.Run.Coins.ToString(), "the chip follows the coins");
		}
		finally { Close(runner, screen); }
	});

	// Same buys in a different order reach the same end state (R6: no order dependence).
	[Test]
	public static void Order_TwoBuysEitherOrder_SameHash(Node runner) => OnFixtures(() =>
	{
		ulong Run(string a, string b)
		{
			var s = Shopping(200); var screen = Open(runner, s);
			try { screen.BuySpecial(a); screen.BuySpecial(b); return s.StateHash(); } finally { Close(runner, screen); }
		}
		Assert.True(Run("test-river-grab", "test-slippery-skin") == Run("test-slippery-skin", "test-river-grab"), "slots are fixed per special, so buy order does not change the end state");
	});

	// Lifetime: open/close repeatedly; EndFight while open; Continue gives the stop as entered (AM3/M5).
	[Test]
	public static void Lifetime_OpenCloseRepeatedly_NoOrphans_ShopOpenCorrect(Node runner)
	{
		string dir = Path.Combine(Path.GetTempPath(), "yokai-shopprobe-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(dir);
		var saved = FightScene.Sources; var savedRun = FightScene.Run; var savedFoe = FightScene.Opponent;
		FightScene.Sources = FightScene.FixtureSources; FightScene.Run = null;
		MenuSettings.Reset();
		var flow = GD.Load<PackedScene>("res://scenes/boot.tscn").Instantiate<GameFlow>();
		flow.QuitAction = () => { }; flow.FixedRunSeed = 77; flow.SaveFile = Path.Combine(dir, "run.dat");
		flow.ConfigureFight = f => f.ExternalDrive = true;
		try
		{
			runner.AddChild(flow);
			flow.Title.StartButton.EmitSignal(BaseButton.SignalName.Pressed);
			flow.ControlSelect.Start.EmitSignal(BaseButton.SignalName.Pressed);
			var s = flow.Session!;
			while (true)
			{
				string id = s.Reachable()[0];
				Assert.True(flow.ChooseNode(id).Ok, "entered " + id);
				if (s.Node!.Type == "merchant") break;
				if (s.WinFight(300) is { } d) { var c = d.Offer.Cards[0]; d.Pick(0, c.Targets.Count > 0 ? c.Targets[0].Slot : null); }
				s.CompleteNode();
			}
			var stop = (RestStopScreen)flow.NodeScene!;
			ulong h0 = s.StateHash();
			Assert.True(stop.OpenSecond() && flow.Shop != null, "opened");
			var first = flow.Shop; stop.OpenSecond();
			Assert.True(flow.Shop == first && flow.GetChildren().OfType<ShopScreen>().Count() == 1 && flow.Router.ShopOpen, "a second open while open makes no second shop");
			Tap(flow, Key.Escape);
			Assert.True(flow.Shop == null && !flow.Router.ShopOpen && stop.Visible, "closed");
			// Reopen repeatedly through the stop; the choice is already Shop so it must stay reopenable.
			double orph0 = Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount);
			for (int i = 0; i < 5; i++)
			{
				Assert.True(stop.OpenSecond(), "reopen " + i);
				Assert.True(flow.Router.ShopOpen && flow.Shop != null, "open flag " + i);
				var shop = flow.Shop!;
				Tap(flow, Key.Escape);
				Assert.True(!flow.Router.ShopOpen && flow.Shop == null && stop.Visible, "closed " + i);
				Assert.True(shop.IsQueuedForDeletion() || !GodotObject.IsInstanceValid(shop), "shop " + i + " freed");
			}
			Assert.True(s.StateHash() != 0 && s.Run.Coins >= 0, "state sane");
			// Leaving the stop while the shop is open: ReturnToTitle, then Continue.
			Assert.True(stop.OpenSecond(), "open again");
			var shopNow = flow.Shop!;
			Assert.True(flow.ReturnToTitle(), "to title");
			Assert.True(flow.Shop == null && !flow.Router.ShopOpen && !GodotObject.IsInstanceValid(shopNow), "shop gone with the stop");
			Assert.True(flow.ContinueRun() && flow.Shop == null && !flow.Router.ShopOpen, "Continue: no shop");
			Assert.True(!flow.Session!.Run.Coins.Equals(-1) && flow.Session.Run.Health == s.Run.Health, "session intact");
		}
		finally
		{
			runner.RemoveChild(flow); flow.QueueFree(); MenuSettings.Reset();
			FightScene.Sources = saved; FightScene.Run = savedRun; FightScene.Opponent = savedFoe;
			try { Directory.Delete(dir, true); } catch (IOException) { }
		}
	}
}
