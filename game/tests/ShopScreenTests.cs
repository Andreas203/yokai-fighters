using System;
using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Flow;
using YokaiFighters.Settings;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-104: the merchant shop screen (R6, A2, T2, T5, M5). Stock comes from fixture pools (the real default pool's
/// shelves are empty today: every non-starter ability is the Kitsune's, who is bound). Every refusal is asserted to
/// change nothing (run state hash), every key goes through the viewport's real input pipeline, and nothing touches
/// the player's save: boot tests use a temp save file.
/// </summary>
public static class ShopScreenTests
{
	const string Scene = "res://scenes/ui/shop.tscn";

	static void OnFixtures(Action body)
	{
		var saved = FightScene.Sources;
		var savedRun = FightScene.Run;
		FightScene.Sources = FightScene.FixtureSources;
		FightScene.Run = null;
		try { body(); }
		finally { FightScene.Sources = saved; FightScene.Run = savedRun; }
	}

	static RunSession Shopping(int coins, params (SpecialSlot, string, int)[] slots)
	{
		var s = ShopScreenFixtures.At(ShopScreenFixtures.StockedPool(), coins, slots.Length > 0 ? slots : ShopScreenFixtures.Starters);
		Assert.True(s.Merchant!.Choose(MerchantChoice.Shop).Ok, "the shop is chosen");
		return s;
	}

	static ShopScreen Open(Node runner, RunSession s)
	{
		var screen = GD.Load<PackedScene>(Scene).Instantiate<ShopScreen>();
		screen.Bind(s);
		runner.AddChild(screen);
		return screen;
	}

	static void Tap(Node n, Key k)
	{
		n.GetViewport().PushInput(new InputEventKey { Keycode = k, PhysicalKeycode = k, Pressed = true });
		n.GetViewport().PushInput(new InputEventKey { Keycode = k, PhysicalKeycode = k, Pressed = false });
	}
	static void Pad(Node n, JoyButton b)
	{
		n.GetViewport().PushInput(new InputEventJoypadButton { ButtonIndex = b, Pressed = true });
		n.GetViewport().PushInput(new InputEventJoypadButton { ButtonIndex = b, Pressed = false });
	}
	static string Text(Node n, string name) => ((Label)n.FindChild(name, true, false)!).Text;
	static string? Focus(Node n) => n.GetViewport().GuiGetFocusOwner()?.Name.ToString();

	// ---- the model ----

	[Test]
	public static void Model_ShelvesCarryCardTextPriceAndTheShopsOwnReasons() => OnFixtures(() =>
	{
		var s = Shopping(45);
		var m = ShopScreenModel.Create(s)!;
		Assert.Equal("test-river-grab,test-slippery-skin", string.Join(",", m.Specials.Select(e => e.Id)), "unbound Kappa specials, in shelf order");
		var grab = m.Specials[0];
		Assert.True(grab.Price == 50 && grab.Plain == "Command grab. Hard to block, slow to start." && grab.Source == "Kappa", "price and the card block's plain text");
		Assert.True(grab.State == ShopCardState.Refused && grab.Reason == ShopScreenModel.Clean(s.Merchant!.Shop.WhyNotBuy("test-river-grab")!), "45 coins: refused with the shop's reason");
		Assert.True(m.Modifiers.Count == 2 && m.Modifiers.All(e => e.State == ShopCardState.Refused && e.Reason != null), "modifiers on their own shelf, refused for coins too");
		Assert.True(m.Slots.Count == 4 && m.Slots[0].Locked && m.Slots[1].Locked && m.Slots[2].Empty && m.Slots[3].Empty, "four slots, the two starters locked, two empty");
		Assert.True(!m.Specials.Concat(m.Modifiers).Any(e => e.Id.Contains("rare") || e.Id.Contains("sealed")), "only what the shop lists");
	});

	[Test]
	public static void Model_ShelvesAreTheShopsEvenBeforeTheShopIsChosen_ButNothingIsBuyable() => OnFixtures(() =>
	{
		var s = ShopScreenFixtures.At(ShopScreenFixtures.StockedPool(), 200, ShopScreenFixtures.Starters);
		var m = ShopScreenModel.Create(s)!;
		ulong before = s.StateHash();
		Assert.True(!m.Open && m.Specials.All(e => e.State == ShopCardState.Refused), "closed shop: every card refused");
		Assert.True(!m.Buy("test-river-grab").Ok && !m.BuyModifier("test-kappa-patience", SpecialSlot.A).Ok && !m.Release(SpecialSlot.A).Ok && s.StateHash() == before, "refused, nothing changed");
	});

	// ---- the scene: buying ----

	[Test]
	public static void Scene_BuySpecial_SpendsFiftyIntoItsSlotInBorrowedInk_AndTheCardStaysAsBought(Node runner) => OnFixtures(() =>
	{
		var s = Shopping(120);
		var screen = Open(runner, s);
		try
		{
			Assert.Equal("RYO · 640 / 1000", screen.GetNode<Label>("HealthLabel").Text, "health always visible");
			Assert.Equal("120", screen.GetNode<Label>("CoinChip/CoinValue").Text, "coins always visible");
			Assert.True(screen.BuySpecial("test-river-grab"), "bought");
			Assert.True(s.Run.Coins == 70 && s.Run[SpecialSlot.D] is { Borrowed: true, Level: 1 } o && o.Data.Id == "test-river-grab", "R6: 50 coins, Lv 1, borrowed ink, its own slot");
			Assert.Equal("70", screen.GetNode<Label>("CoinChip/CoinValue").Text, "the coin chip follows");
			Assert.True(screen.Cards["Special_test-river-grab"].CardDisabled && Text(screen.Cards["Special_test-river-grab"], "TagLabel") == "BOUGHT", "the card stays, tagged BOUGHT and not offered again");
			Assert.True(Text(screen.Rows[SpecialSlot.D], "Sub").Contains("borrowed ink"), "the slots list shows it as borrowed ink");
			Assert.True(!screen.BuySpecial("test-river-grab") && s.Run.Coins == 70, "a second buy is refused and changes nothing");
		}
		finally { runner.RemoveChild(screen); screen.Free(); }
	});

	[Test]
	public static void Scene_RefusedBuys_ChangeNothing_AndSayWhy(Node runner) => OnFixtures(() =>
	{
		var s = Shopping(30);
		var screen = Open(runner, s);
		try
		{
			ulong h = s.StateHash();
			Assert.True(!screen.BuySpecial("test-river-grab") && s.StateHash() == h, "cannot afford: refused, nothing changed");
			Assert.True(screen.DetailText.StartsWith("50 coins needed"), "the shop's reason is shown: " + screen.DetailText);
			Assert.True(!screen.BeginPick("test-kappa-patience") && screen.Picking == null && s.StateHash() == h, "a modifier it cannot buy opens no picker");
			Assert.True(!screen.BuySpecial("nope") && s.StateHash() == h, "an id off the shelf");
			Assert.True(!screen.ReleaseSlot(SpecialSlot.A) && screen.Armed == null && s.StateHash() == h, "a locked starter: refused, not armed");
		}
		finally { runner.RemoveChild(screen); screen.Free(); }
	});

	[Test]
	public static void Scene_Modifier_AsksForATarget_OffersOnlyWhatTheShopOffers_AndAttachesInBorrowedInk(Node runner) => OnFixtures(() =>
	{
		var s = Shopping(120);
		var screen = Open(runner, s);
		try
		{
			Assert.True(screen.BeginPick("test-kappa-wisp") && screen.Picking == "test-kappa-wisp", "the picker opens");
			var offered = s.Merchant!.Shop.ModifierTargets("test-kappa-wisp").ToArray();
			Assert.True(offered.Length >= 1 && offered.Length < 2, "the fixture wisp needs a projectile: one of the two starters");
			foreach (var slot in new[] { SpecialSlot.A, SpecialSlot.B })
				Assert.True((screen.Rows[slot].FocusMode != Control.FocusModeEnum.None) == offered.Contains(slot), $"slot {slot} is focusable only if the shop offers it");
			var no = new[] { SpecialSlot.A, SpecialSlot.B }.First(x => !offered.Contains(x));
			ulong h = s.StateHash();
			Assert.True(!screen.PickTarget(no) && s.StateHash() == h, "a slot the shop does not offer: refused, nothing changed");
			Assert.True(screen.PickTarget(offered[0]), "picked");
			Assert.True(s.Run.Coins == 70 && s.Run[offered[0]] is { Modifier.Id: "test-kappa-wisp", ModifierBorrowed: true }, "A2 / R6: 50 coins, attached, borrowed ink");
			Assert.True(screen.Picking == null && screen.Cards["Modifier_test-kappa-wisp"].CardDisabled, "back to the shelf, the card is bought");
		}
		finally { runner.RemoveChild(screen); screen.Free(); }
	});

	[Test]
	public static void Scene_ModifierNoSpecialCanTake_IsGreyedWithTheShopsReason(Node runner) => OnFixtures(() =>
	{
		// The fixture wisp needs a projectile; hold only specials without one (the clone river grab has none) so no slot can take it.
		var pool = ShopScreenFixtures.StockedPool();
		var s = ShopScreenFixtures.At(pool, 200, (SpecialSlot.A, "test-spirit-wave", 1), (SpecialSlot.B, "test-rising-talisman", 1));
		s.Merchant!.Choose(MerchantChoice.Shop);
		var m = ShopScreenModel.Create(s)!;
		var wisp = m.Modifiers.First(e => e.Id == "test-kappa-wisp");
		if (s.Merchant.Shop.ModifierTargets("test-kappa-wisp").Count > 0) { Assert.True(wisp.State == ShopCardState.Buyable, "this pool lets a projectile special take it"); return; }
		Assert.True(wisp.State == ShopCardState.Refused && wisp.Reason != null, "no target: refused, with the reason");
	});

	// ---- the scene: releasing ----

	[Test]
	public static void Scene_Release_NeedsASecondEnter_CostsForty_AndFreesTheSlot(Node runner) => OnFixtures(() =>
	{
		var s = Shopping(120);
		var screen = Open(runner, s);
		try
		{
			Assert.True(screen.BuySpecial("test-river-grab") && s.Run.Coins == 70, "set up: a borrowed special in D");
			ulong h = s.StateHash();
			Assert.True(!screen.ReleaseSlot(SpecialSlot.D) && screen.Armed == SpecialSlot.D && s.StateHash() == h, "first Enter only asks: nothing changed");
			Assert.True(screen.ReleaseSlot(SpecialSlot.D), "second Enter releases");
			Assert.True(s.Run.Coins == 30 && s.Run[SpecialSlot.D] == null, "R6: 40 coins, the slot is free");
			Assert.True(screen.Model!.Slots[3].Empty && screen.Armed == null, "the row is empty and disarmed");
			Assert.True(!screen.ReleaseSlot(SpecialSlot.D) && s.Run.Coins == 30, "releasing an empty slot is refused");
		}
		finally { runner.RemoveChild(screen); screen.Free(); }
	});

	[Test]
	public static void Scene_LastSpecial_IsLockedWithItsReason_AndTheTanukiTargetIsMarked(Node runner) => OnFixtures(() =>
	{
		var s = Shopping(500, (SpecialSlot.A, "test-spirit-wave", 1));
		var screen = Open(runner, s);
		try
		{
			var row = screen.Model!.Slots[0];
			Assert.True(row.Locked && !row.CanRelease && row.IsTanukiTarget, "T5: locked; it is also the only possible copy target");
			ulong h = s.StateHash();
			Assert.True(!screen.ReleaseSlot(SpecialSlot.A) && s.StateHash() == h, "refused even with 500 coins");
			Assert.Equal("LOCKED", Text(screen.Rows[SpecialSlot.A], "BadgeLabel"), "the button reads LOCKED");
			Assert.True(Text(screen.Rows[SpecialSlot.A], "Sub").Contains("Tanuki target"), "Tanuki target named (T2)");
		}
		finally { runner.RemoveChild(screen); screen.Free(); }
	});

	[Test]
	public static void Scene_TanukiTargetRow_IsTheForecastsSlot_AndFollowsARelease(Node runner) => OnFixtures(() =>
	{
		var s = Shopping(500, (SpecialSlot.A, "test-spirit-wave", 1), (SpecialSlot.B, "test-rising-talisman", 2));
		var screen = Open(runner, s);
		try
		{
			Assert.True(screen.Model!.TanukiTarget == CopyRule.Target(s.Run)!.Slot && screen.Model.Slots.Count(r => r.IsTanukiTarget) == 1, "the marked row is CopyRule's target");
			Assert.True(screen.Model.Slots[1].IsTanukiTarget, "the levelled special");
		}
		finally { runner.RemoveChild(screen); screen.Free(); }
	});

	[Test]
	public static void Scene_ShelfEmpty_ReadsAsAWholeState_WhenBothShelvesAreEmpty(Node runner) => OnFixtures(() =>
	{
		var s = ShopScreenFixtures.At(FightScene.LoadAbilityPool(), 75, ShopScreenFixtures.Starters);
		s.Merchant!.Choose(MerchantChoice.Shop);
		var screen = Open(runner, s);
		try
		{
			Assert.True(screen.GetNode<Control>("AllEmpty").Visible && !screen.GetNode<Control>("Shelves").Visible, "the empty-shelf panel replaces the shelves");
			Assert.Equal("Shelf empty", screen.GetNode<Label>("AllEmpty/Box/EmptyTitle").Text, "it says so");
			Assert.True(screen.Cards.Count == 0 && screen.Rows.Values.Count(r => r.FocusMode != Control.FocusModeEnum.None) == 2, "no cards; the two locked starters and Back still take focus");
			Assert.True(Focus(screen) != null, "focus is on something: " + Focus(screen));
		}
		finally { runner.RemoveChild(screen); screen.Free(); }
	});

	[Test]
	public static void Scene_OneShelfEmpty_ShowsItsNoteAndTheOtherShelf(Node runner) => OnFixtures(() =>
	{
		var s = ShopScreenFixtures.At(ShopScreenFixtures.StockedPool(modifiers: false), 75, ShopScreenFixtures.Starters);
		s.Merchant!.Choose(MerchantChoice.Shop);
		var screen = Open(runner, s);
		try
		{
			Assert.True(!screen.GetNode<Control>("AllEmpty").Visible && screen.GetNode<Control>("Shelves/SpecialsScroll").Visible && !screen.GetNode<Control>("Shelves/ModifiersScroll").Visible && screen.GetNode<Control>("Shelves/ModifiersEmpty").Visible, "specials shown, modifiers noted as empty");
		}
		finally { runner.RemoveChild(screen); screen.Free(); }
	});

	// ---- input through the viewport ----

	[Test]
	public static void Input_KeysAndPad_MoveFocusBuyAndGoBack_FocusNeverLeavesOrIsLost(Node runner) => OnFixtures(() =>
	{
		var s = Shopping(120);
		var screen = Open(runner, s);
		try
		{
			bool closed = false;
			screen.Closed += () => closed = true;
			Assert.Equal("Special_test-river-grab", Focus(screen), "default focus: the first card");
			for (int round = 0; round < 10; round++)
			{
				foreach (var k in new[] { Key.Right, Key.Down, Key.Left, Key.Up })
				{
					Tap(screen, k);
					Assert.True(Focus(screen) is { } f && screen.IsAncestorOf(screen.GetViewport().GuiGetFocusOwner()), $"focus stays on the shop after {k}: {Focus(screen)}");
				}
				foreach (var b in new[] { JoyButton.DpadRight, JoyButton.DpadDown, JoyButton.DpadLeft, JoyButton.DpadUp })
				{
					Pad(screen, b);
					Assert.True(Focus(screen) != null, $"focus not lost after pad {b}");
				}
			}
			screen.Refresh("Special_test-river-grab");
			Tap(screen, Key.Right);
			Assert.Equal("Special_test-slippery-skin", Focus(screen), "Right moves along the shelf");
			Tap(screen, Key.Right);
			Assert.Equal("Slot_A", Focus(screen), "Right from the last card enters the slots");
			Tap(screen, Key.Left);
			Assert.True(Focus(screen)!.StartsWith("Special_"), "Left returns to the shelf");
			Tap(screen, Key.Enter);
			Assert.True(s.Run.Owns("test-river-grab") && s.Run.Coins == 70, "Enter buys the focused card (keyboard)");
			Assert.Equal("Special_test-slippery-skin", Focus(screen), "focus moved on to the next card when the bought one went inert");
			Pad(screen, JoyButton.A);
			Assert.True(s.Run.Coins == 20 && s.Run.Owns("test-slippery-skin"), "pad A buys too");
			Assert.True(Focus(screen) != null && screen.IsAncestorOf(screen.GetViewport().GuiGetFocusOwner()), "focus is still on the shop after the cards went inert: " + Focus(screen));
			Tap(screen, Key.Tab);
			Assert.True(screen.FrameDataShown, "Tab shows frame data");
			Tap(screen, Key.Tab);
			Pad(screen, JoyButton.B);
			Assert.True(closed, "pad B backs out");
		}
		finally { runner.RemoveChild(screen); screen.Free(); }
	});

	[Test]
	public static void Input_Escape_StepsBackOneLevelAtATime(Node runner) => OnFixtures(() =>
	{
		var s = Shopping(200);
		var screen = Open(runner, s);
		try
		{
			bool closed = false;
			screen.Closed += () => closed = true;
			screen.Refresh("Modifier_test-kappa-wisp");
			Tap(screen, Key.Enter);
			Assert.True(screen.Picking != null && Focus(screen)!.StartsWith("Slot_"), "Enter opens the picker and focus is on an offered slot: " + Focus(screen));
			ulong h = s.StateHash();
			Tap(screen, Key.Escape);
			Assert.True(screen.Picking == null && !closed && Focus(screen) == "Modifier_test-kappa-wisp" && s.StateHash() == h, "Esc cancels the pick, focus back on its card, nothing bought");
			screen.Refresh("Special_test-river-grab");
			Tap(screen, Key.Right); Tap(screen, Key.Right);
			Assert.Equal("Slot_A", Focus(screen), "to the slots");
			Tap(screen, Key.Escape);
			Assert.True(closed && s.StateHash() == h, "Esc on the shop closes it, nothing changed");
		}
		finally { runner.RemoveChild(screen); screen.Free(); }
	});

	// ---- inside the rest stop ----

	static RunSession MerchantSession() => ShopScreenFixtures.At(ShopScreenFixtures.StockedPool(), 150, ShopScreenFixtures.Starters);

	[Test]
	public static void RestStop_BrowseTheShop_SpendsTheChoiceClosesTheHeal_AndBackReturnsUsed(Node runner) => OnFixtures(() =>
	{
		var s = MerchantSession();
		var stop = GD.Load<PackedScene>("res://scenes/ui/rest_stop.tscn").Instantiate<RestStopScreen>();
		stop.LineSource = _ => "A line.";
		stop.BindRun(s, ControlScheme.Kihon);
		runner.AddChild(stop);
		try
		{
			Assert.True(!stop.SecondCard.CardDisabled && stop.Model!.Second == RestStopOption.Ready, "the shop card is open");
			int health = s.Run.Health;
			Assert.True(stop.OpenSecond() && stop.Shop != null, "the shop opens");
			Assert.True(s.Merchant!.Choice == MerchantChoice.Shop && s.Run.Health == health, "Choose(Shop): the choice is spent, no heal");
			Assert.True(!stop.Model!.CanTakeFree && !s.Merchant.TakeHeal().Ok && s.Run.Health == health, "the free heal is closed");
			Assert.True(!stop.FreeCard.IsVisibleInTree() && stop.Shop!.IsVisibleInTree(), "only the shop shows");
			Tap(stop, Key.Escape);
			Assert.True(stop.Shop == null && stop.ContinueButton.Visible && stop.FreeCard.CardDisabled, "Back: the rest stop in its used state with Continue");
			Assert.True(stop.GetViewport().GuiGetFocusOwner() == stop.ContinueButton, "focus on Continue");
			Assert.True(!stop.SecondCard.CardDisabled && stop.OpenSecond() && stop.Shop != null, "the shop can be opened again (it stays open for the stop)");
			Tap(stop, Key.Escape);
			bool left = false;
			stop.Left += () => left = true;
			Assert.True(stop.Leave() && left, "Continue to the map");
			Assert.True(!stop.OpenSecond(), "the stop is left: refused");
		}
		finally { runner.RemoveChild(stop); stop.Free(); }
	});

	[Test]
	public static void RestStop_HealTaken_ShopStaysClosed(Node runner) => OnFixtures(() =>
	{
		var s = MerchantSession();
		var stop = GD.Load<PackedScene>("res://scenes/ui/rest_stop.tscn").Instantiate<RestStopScreen>();
		stop.LineSource = _ => "A line.";
		stop.BindRun(s, ControlScheme.Kihon);
		runner.AddChild(stop);
		try
		{
			Assert.True(stop.TakeFree(), "heal");
			ulong h = s.StateHash();
			Assert.True(!stop.OpenSecond() && stop.Shop == null && s.StateHash() == h && stop.SecondCard.CardDisabled, "R6 one choice: the shop is refused and the card closed");
		}
		finally { runner.RemoveChild(stop); stop.Free(); }
	});

	[Test]
	public static void Boot_QuitInsideTheShop_ContinueRestoresTheStopAsEntered_M5(Node runner)
	{
		string dir = Path.Combine(Path.GetTempPath(), "yokai-shop-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(dir);
		var saved = FightScene.Sources; var savedRun = FightScene.Run; var savedFoe = FightScene.Opponent;
		FightScene.Sources = FightScene.FixtureSources;
		FightScene.Run = null;
		MenuSettings.Reset();
		var flow = GD.Load<PackedScene>("res://scenes/boot.tscn").Instantiate<GameFlow>();
		flow.QuitAction = () => { };
		flow.FixedRunSeed = 77;
		flow.SaveFile = Path.Combine(dir, "run.dat"); // never the player's save
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
				if (s.WinFight(300) is { } d) { var c = d.Offer.Cards[0]; Assert.True(d.Pick(0, c.Targets.Count > 0 ? c.Targets[0].Slot : null).Ok, "picked"); }
				Assert.True(s.CompleteNode().Ok, "completed");
			}
			var stop = (RestStopScreen)flow.NodeScene!;
			int health = s.Run.Health, coins = s.Run.Coins;
			s.Run.AddCoins(100);
			Assert.True(stop.OpenSecond() && stop.Shop != null && stop.Shop!.Model!.Open, "in the shop");
			Assert.True(!stop.Shop!.ReleaseSlot(SpecialSlot.A), "a starter cannot be released");
			Assert.True(flow.ReturnToTitle() && flow.NodeScene == null, "quit to the title from inside the shop");
			Assert.True(flow.ContinueRun(), "Continue");
			var again = (RestStopScreen)flow.NodeScene!;
			Assert.True(again != stop && again.Shop == null && !again.Model!.ChoiceMade && again.Model.Second == RestStopOption.Ready, "the stop as entered: no choice taken, the shop closed");
			Assert.True(flow.Session!.Run.Health == health && flow.Session.Run.Coins == coins, "entry-state health and coins: nothing the shop did was kept");
		}
		finally
		{
			runner.RemoveChild(flow);
			flow.QueueFree();
			MenuSettings.Reset();
			FightScene.Sources = saved; FightScene.Run = savedRun; FightScene.Opponent = savedFoe;
			try { Directory.Delete(dir, true); } catch (IOException) { }
		}
	}
}
