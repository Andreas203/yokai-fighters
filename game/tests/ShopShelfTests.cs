using System;
using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;
using YokaiFighters.Story;
using YokaiFighters.Ui;
using YokaiFighters.Ui.Kit;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-159 (rev 3 shelf): the merchant-lines loader and the shop's cabinet, speech bubble, ledger slip, states and overflow.
/// Expected values come from the written rules (R6, T5, A12, S2) and from the lines data, not from the code.
/// </summary>
public static class ShopShelfTests
{
	static string StoryDir => ContentPaths.Data("story");
	static MerchantShopLines Real => MerchantShopLines.LoadDirectory(StoryDir);

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

	static string AllText(Node n) => string.Join("\n", n.FindChildren("*", "Label", true, false).Cast<Label>().Where(l => l.IsVisibleInTree()).Select(l => l.Text));

	// ---- the lines data and its loader ----

	[Test]
	public static void Lines_Data_HasEveryEvent_EveryLineAtMostTwoSentences_S2()
	{
		var lines = Real;
		foreach (string ev in new[] { "greeting_row4", "greeting_row7", "bought_special", "bought_modifier", "cannot_afford", "slot_full", "release_binding", "release_last_special", "shelf_empty", "leaving" })
		{
			Assert.True(lines.Variants(ev).Count >= 1, $"event {ev} has lines");
			foreach (string v in lines.Variants(ev)) Assert.True(MerchantShopLines.Sentences(v) is >= 1 and <= 2, $"S2: '{v}' is {MerchantShopLines.Sentences(v)} sentences");
		}
		Assert.True(lines.LeadIns.Count >= 1 && lines.LeadIns.All(l => l.Contains("{name}") && l.Contains("{card_text}")), "lead-ins join the name and the card text");
	}

	[Test]
	public static void Lines_Data_CarriesNoCopyHinting()
	{
		var lines = Real;
		var all = lines.Events.SelectMany(lines.Variants).Concat(lines.LeadIns).Select(l => l.ToLowerInvariant());
		foreach (string line in all)
			foreach (string word in new[] { "copy", "copies", "copied", "tanuki", "target", "steal", "stolen", "boss" })
				Assert.True(!line.Contains(word), $"the shop lines never hint at the copy rule: '{line}' says '{word}'");
	}

	[Test]
	public static void Lines_ForItem_InsertsTheCardTextVerbatim_AndFallsBackToItAloneWhenTooLong_S2()
	{
		var lines = Real;
		string one = "Slick kappa skin makes you impossible to throw while the move starts up.";
		for (int pick = 0; pick < lines.LeadIns.Count; pick++)
		{
			string said = lines.ForItem("Slippery Skin", one, pick);
			Assert.True(said.Contains(one) && said.Contains("Slippery Skin") && MerchantShopLines.Sentences(said) <= 2, $"lead-in {pick}: '{said}'");
		}
		string two = "Command grab. Hard to block, slow to start.";
		for (int pick = 0; pick < lines.LeadIns.Count; pick++)
		{
			string said = lines.ForItem("River Grab", two, pick);
			Assert.True(said.Contains(two) && MerchantShopLines.Sentences(said) <= 2, $"two-sentence card text stays within S2: '{said}'");
		}
		Assert.True(lines.ForItem("River Grab", "One. Two. Three.", 0) == "One. Two. Three.", "three sentences of card text: spoken alone, untouched");
		Assert.True(MerchantShopLines.Empty.ForItem("X", "Plain text.", 0) == "Plain text." && MerchantShopLines.Empty.Event("bought_special", 0) == "", "no data: the card text alone, events silent");
		Assert.True(lines.ForItem("A", "Plain.", 0) == lines.ForItem("A", "Plain.", lines.LeadIns.Count), "the pick wraps");
	}

	[Test]
	public static void Lines_Sentences_CountsPlainCases()
	{
		Assert.True(MerchantShopLines.Sentences("Sold!") == 1 && MerchantShopLines.Sentences("Sold! Treat it kindly.") == 2, "bang and stop");
		Assert.True(MerchantShopLines.Sentences("Care for Pond Lantern? Your projectiles fly 30% faster.") == 2, "question then stop");
		Assert.True(MerchantShopLines.Sentences("No stop at all") == 1 && MerchantShopLines.Sentences("A. B. C.") == 3, "a bare line is one, three stops are three");
	}

	[Test]
	public static void Lines_Parse_RejectsBadData()
	{
		string Doc(string events) => "{\"kind\":\"merchant_shop_lines\",\"events\":{" + events + "}}";
		void Throws(string json, string why) { try { MerchantShopLines.Parse(json); } catch (FormatException) { return; } throw new AssertFailed("accepted: " + why); }
		Throws("{\"kind\":\"story_card\",\"events\":{}}", "wrong kind");
		Throws(Doc("\"bought_special\":{\"variants\":[\"One. Two. Three.\"]}"), "three sentences (S2)");
		Throws(Doc("\"bought_special\":{\"variants\":[\"Sold {name}!\"]}"), "a placeholder in a plain event");
		Throws(Doc("\"bought_special\":{\"variants\":[]}"), "no lines");
		Throws(Doc("\"bought_special\":{\"variants\":[\"\"]}"), "a blank line");
		Throws(Doc("\"item_focused\":{\"lead_ins\":[\"Look: {card_text}\"]}"), "lead-in without {name}");
		var ok = MerchantShopLines.Parse(Doc("\"bought_special\":{\"variants\":[\"Sold!\"]},\"item_focused\":{\"lead_ins\":[\"Ah, {name}: {card_text}\"]}"));
		Assert.True(ok.Event("bought_special", 3) == "Sold!" && ok.ForItem("X", "Y.", 0) == "Ah, X: Y.", "a valid file loads");
	}

	[Test]
	public static void Lines_MissingFile_IsEmpty_NotAnError()
	{
		string dir = Path.Combine(Path.GetTempPath(), "yokai-nolines-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(dir);
		try { Assert.True(MerchantShopLines.LoadDirectory(dir).Events.Count == 0, "no file: empty lines"); }
		finally { Directory.Delete(dir, true); }
	}

	// ---- the speech bubble ----

	[Test]
	public static void Bubble_OpensWithTheRow4GreetingFromTheData(Node runner) => OnFixtures(() =>
	{
		var screen = Open(runner, Shopping(ShopScreenFixtures.StockedPool(), 120));
		try
		{
			Assert.True(screen.Model!.Row == 4, "the fixture merchant is the row 4 one");
			Assert.True(screen.Lines.Variants("greeting_row4").Contains(screen.MerchantLine), "greeting_row4 from the data: " + screen.MerchantLine);
			Assert.True(screen.GetNode<Control>("Bubble").Visible, "the bubble shows");
			Assert.True(MerchantShopLines.Sentences(screen.MerchantLine) <= 2, "S2");
		}
		finally { Close(runner, screen); }
	});

	[Test]
	public static void Bubble_FocusByKeysSpeaksLeadInPlusTheCardLineVerbatim_AndHoverDoesToo(Node runner) => OnFixtures(() =>
	{
		var screen = Open(runner, Shopping(ShopScreenFixtures.StockedPool(), 120));
		try
		{
			var m = screen.Model!;
			Tap(screen, Key.Right);
			var skin = m.Specials.First(e => e.Id == "test-slippery-skin");
			Assert.Equal("Special_test-slippery-skin", Focus(screen), "focus moved");
			Assert.True(screen.MerchantLine.Contains(skin.Plain) && screen.MerchantLine.Contains(skin.Name), "keyboard focus: the lead-in names it and the card line is inside verbatim: " + screen.MerchantLine);
			Assert.True(!screen.MerchantLine.Contains(skin.Frames) || skin.Frames.Length == 0, "frame data is never spoken (A12)");
			Assert.True(MerchantShopLines.Sentences(screen.MerchantLine) <= 2, "S2");
			var wisp = m.Modifiers.First(e => e.Id == "test-kappa-wisp");
			screen.Cards["Modifier_test-kappa-wisp"].EmitSignal(Control.SignalName.MouseEntered);
			Assert.Equal("Modifier_test-kappa-wisp", Focus(screen), "hover takes the one shared focus");
			Assert.True(screen.MerchantLine.Contains(wisp.Plain) && screen.MerchantLine.Contains(wisp.Name), "hover speaks the same way: " + screen.MerchantLine);
			Assert.True(!screen.MerchantLine.ToLowerInvariant().Contains("modifier"), "no modifier tag in the bubble");
		}
		finally { Close(runner, screen); }
	});

	[Test]
	public static void Bubble_EventLines_BoughtCannotAffordSlotFullRelease_AndTheFocusLineComesBack(Node runner) => OnFixtures(() =>
	{
		var sess = Shopping(ShopScreenFixtures.StockedPool(), 120, (SpecialSlot.A, "test-spirit-wave", 1), (SpecialSlot.B, "test-rising-talisman", 1), (SpecialSlot.C, "test-foxfire", 1));
		var screen = Open(runner, sess);
		try
		{
			var lines = screen.Lines;
			// slot full: the Kappa special's slot C holds Foxfire (R6: release it first)
			var run = sess.Run;
			ulong before = run.StateHash();
			Assert.True(!screen.BuySpecial("test-slippery-skin") && run.StateHash() == before, "refused, nothing changed");
			Assert.True(lines.Variants("slot_full").Contains(screen.MerchantLine), "slot_full line: " + screen.MerchantLine);
			Assert.True(!screen.DetailText.Contains("test-") && screen.DetailText.Contains("Foxfire"), "the reason names the move, not its id: " + screen.DetailText);
			Assert.True(screen.Model!.Specials.First(e => e.Id == "test-slippery-skin").Refusal == ShopRefusal.SlotFull, "the model classifies it");
			Assert.True(screen.BuySpecial("test-river-grab"), "the free slot buys");
			Assert.True(lines.Variants("bought_special").Contains(screen.MerchantLine), "bought_special line: " + screen.MerchantLine);
			Tap(screen, Key.Right);
			Assert.True(screen.MerchantLine.Contains(screen.Model!.Modifiers.Concat(screen.Model.Specials).First(e => e.Id == screen.Cards.Values.First(c => c.Name == Focus(screen)).Entry.Id).Plain), "the next focus change brings the item's line back: " + screen.MerchantLine);
			run.RestoreCoins(10);
			screen.Refresh("Modifier_test-kappa-patience");
			before = run.StateHash();
			Assert.True(!screen.BeginPick("test-kappa-patience") && run.StateHash() == before, "short of coins: refused");
			Assert.True(lines.Variants("cannot_afford").Contains(screen.MerchantLine), "cannot_afford line: " + screen.MerchantLine);
			Assert.True(screen.Cards["Modifier_test-kappa-patience"].PriceText == "Need 50", "the tag reads Need 50");
		}
		finally { Close(runner, screen); }
	});

	[Test]
	public static void Bubble_ReleaseAndT5Lines(Node runner) => OnFixtures(() =>
	{
		var s = Shopping(ShopScreenFixtures.StockedPool(), 200, (SpecialSlot.A, "test-spirit-wave", 1));
		var screen = Open(runner, s);
		try
		{
			Assert.True(screen.BuySpecial("test-river-grab"), "bought");
			Assert.True(!screen.ReleaseSlot(SpecialSlot.D) && screen.Armed == SpecialSlot.D, "armed");
			Assert.True(screen.ReleaseSlot(SpecialSlot.D), "released");
			Assert.True(screen.Lines.Variants("release_binding").Contains(screen.MerchantLine), "release_binding line: " + screen.MerchantLine);
			ulong h = s.StateHash();
			Assert.True(!screen.ReleaseSlot(SpecialSlot.A) && s.StateHash() == h, "T5: refused, nothing changed");
			Assert.True(screen.Lines.Variants("release_last_special").Contains(screen.MerchantLine), "release_last_special line: " + screen.MerchantLine);
		}
		finally { Close(runner, screen); }
	});

	[Test]
	public static void Bubble_BoughtModifierLine_AndTheTargetStep(Node runner) => OnFixtures(() =>
	{
		var s = Shopping(ShopScreenFixtures.StockedPool(), 120);
		var screen = Open(runner, s);
		try
		{
			Assert.True(screen.BeginPick("test-kappa-wisp"), "the target step opens");
			Assert.Equal("WHICH SPECIAL?", screen.GetNode<Label>("SlotsHeading").Text, "the slots panel asks");
			Assert.True(screen.GetNode<Label>("SlotsCaption").Text.Contains("Pond Lantern") && !AllText(screen).Contains("test-"), "the step names the modifier: " + screen.GetNode<Label>("SlotsCaption").Text);
			Assert.True(screen.Cards["Modifier_test-kappa-wisp"].Marked && screen.Cards["Special_test-river-grab"].Modulate.R < 0.7f, "the picked item stays lit, the rest dim");
			var footer = screen.GetNode<KeyChipFooter>("Footer");
			Assert.True(string.Join("|", Enumerable.Range(0, 3).Select(i => footer.GetNode<Label>("Action" + i).Text)) == "Choose|Attach|Cancel", "footer: Choose, Attach, Cancel");
			var slot = s.Merchant!.Shop.ModifierTargets("test-kappa-wisp")[0];
			Assert.True(screen.PickTarget(slot), "picked");
			Assert.True(screen.Lines.Variants("bought_modifier").Contains(screen.MerchantLine), "bought_modifier line: " + screen.MerchantLine);
		}
		finally { Close(runner, screen); }
	});

	// ---- the ledger slip (frame data) ----

	[Test]
	public static void Slip_TabToggles_ShowsFrameData_StaysWhileFocusMoves_NeverSpoken(Node runner) => OnFixtures(() =>
	{
		var screen = Open(runner, Shopping(ShopScreenFixtures.StockedPool(), 120));
		try
		{
			Assert.True(!screen.SlipVisible && !screen.FrameDataShown, "off to begin with");
			string spokenBefore = screen.MerchantLine;
			Tap(screen, Key.Tab);
			Assert.True(screen.SlipVisible && screen.FrameDataShown, "Tab opens the slip");
			var grab = screen.Model!.Specials[0];
			Assert.True(screen.GetNode<Label>("Slip/Column/Pad/Box/Title").Text == grab.Name, "the slip is for the focused item");
			Assert.True(AllText(screen.GetNode("Slip")).Contains(grab.Frames), "a special shows its frames text");
			Assert.True(screen.MerchantLine == spokenBefore, "opening the slip does not change what the merchant says");
			Assert.True(!screen.MerchantLine.Contains(grab.Frames), "frame data is never spoken (A12)");
			Tap(screen, Key.Right); Tap(screen, Key.Right);
			var mod = screen.Model.Modifiers[0];
			Assert.True(screen.SlipVisible && screen.GetNode<Label>("Slip/Column/Pad/Box/Title").Text == mod.Name, "the slip stays open and follows focus");
			string slip = AllText(screen.GetNode("Slip"));
			Assert.True(slip.Contains("CHANGE") && slip.Contains("Frame numbers unchanged") && slip.Contains(mod.Frames), "a modifier shows the change only");
			Assert.True(((Control)screen.GetNode("Footer").FindChild("Action2", true, false)!) is Label { Text: "Frame data: ON" }, "the chip reads ON");
			Tap(screen, Key.Tab);
			Assert.True(!screen.SlipVisible, "Tab again closes it");
		}
		finally { Close(runner, screen); }
	});

	[Test]
	public static void Slip_Pad_YToggles(Node runner) => OnFixtures(() =>
	{
		var screen = Open(runner, Shopping(ShopScreenFixtures.StockedPool(), 120));
		try
		{
			screen.GetViewport().PushInput(new InputEventJoypadButton { ButtonIndex = JoyButton.Y, Pressed = true });
			Assert.True(screen.SlipVisible, "pad Y opens the slip");
		}
		finally { Close(runner, screen); }
	});

	// ---- the cabinet: 0 to about 10 items ----

	[Test]
	public static void Cabinet_ZeroToTenItems_EveryItemInsideTheCabinet_ThreeAcrossSpecialsFirst(Node runner) => OnFixtures(() =>
	{
		// the stocked pool sells 4 (2 specials + 2 modifiers); the wide pool adds more; no stock is the empty cabinet
		foreach (var (extraSp, extraMd, expect) in new[] { (-1, -1, 0), (0, 0, 4), (1, 0, 5), (3, 2, 9), (3, 3, 10) })
		{
			var pool = extraSp < 0 ? ShopScreenFixtures.StockedPool(false, false) : ShopScreenFixtures.WidePool(extraSp, extraMd);
			var screen = Open(runner, Shopping(pool, 500));
			try
			{
				Assert.True(screen.Items.Count == expect, $"({extraSp},{extraMd}): {screen.Items.Count} items, wanted {expect}");
				var cab = screen.GetNode<Control>("Cabinet");
				foreach (var it in screen.Items.Where(i => i.Visible))
					Assert.True(new Rect2(Vector2.Zero, cab.Size).Encloses(new Rect2(it.Position, it.Size)), $"{it.Name} sits inside the cabinet {cab.Size}");
				Assert.True(screen.Items.Select(i => i.Entry.Kind).SequenceEqual(screen.Items.Select(i => i.Entry.Kind).OrderBy(k => k)), "specials first, then modifiers");
				Assert.True(screen.Tiers is >= 2 and <= 3, "two or three shelves");
				Assert.True(AllText(screen).Contains("Shelf empty") == (expect == 0), "the empty text only when empty");
				Assert.True(Focus(screen) != null, "focus is somewhere");
			}
			finally { Close(runner, screen); }
		}
	});

	[Test]
	public static void Cabinet_MoreItemsThanShelves_ScrollOneShelfAtATime_FocusAlwaysOnAVisibleItem(Node runner) => OnFixtures(() =>
	{
		var s = Shopping(ShopScreenFixtures.WidePool(5, 5), 1000);
		var screen = Open(runner, s);
		try
		{
			Assert.True(screen.Items.Count == 14 && screen.Tiers == 3 && screen.ShelfCount == 5 && screen.FirstShelf == 0, $"14 items on 5 shelves, 3 showing: {screen.Items.Count}/{screen.ShelfCount}");
			Assert.True(AllText(screen).Contains("Shelves 1-3 of 5") && AllText(screen).Contains("more below"), "the cue says where the page is and that there is more");
			ulong h = s.StateHash();
			for (int i = 0; i < 6; i++)
			{
				Tap(screen, Key.Down);
				var f = screen.GetViewport().GuiGetFocusOwner() as ShopItem;
				Assert.True(f != null && f.IsVisibleInTree(), "focus is on a visible item after Down " + i);
			}
			Assert.True(screen.FirstShelf == 2, "focus past the third shelf scrolled the page to shelves 3-5: " + screen.FirstShelf);
			Assert.True(AllText(screen).Contains("Shelves 3-5 of 5") && !AllText(screen).Contains("more below"), "last page: no more below");
			Tap(screen, Key.Pageup);
			Assert.True(screen.FirstShelf == 0 && (screen.GetViewport().GuiGetFocusOwner() as ShopItem)!.IsVisibleInTree(), "PgUp jumps a page back, focus on a visible item");
			Tap(screen, Key.Pagedown);
			Assert.True(screen.FirstShelf == 2 && (screen.GetViewport().GuiGetFocusOwner() as ShopItem)!.IsVisibleInTree(), "PgDn jumps a page on");
			Assert.True(s.StateHash() == h, "browsing changes nothing");
			Assert.True(screen.Items.Where(i => i.Visible).Count() == 8, "only the shown shelves' items are visible (shelves 3-5: 3 + 3 + 2)");
		}
		finally { Close(runner, screen); }
	});

	// ---- display names and chips ----

	[Test]
	public static void Names_EverythingShownComesFromTheData_NoIdsNoFixtureNotes(Node runner) => OnFixtures(() =>
	{
		var s = Shopping(ShopScreenFixtures.StockedPool(), 500, (SpecialSlot.A, "test-spirit-wave", 1), (SpecialSlot.B, "test-rising-talisman", 2), (SpecialSlot.C, "test-foxfire", 1));
		var screen = Open(runner, s);
		try
		{
			screen.BuySpecial("test-river-grab");
			screen.BuySpecial("test-slippery-skin"); // refused: slot C holds Foxfire
			Tap(screen, Key.Tab);
			string text = AllText(screen);
			Assert.True(!text.Contains("test-") && !text.Contains("TEST FIXTURE"), "no raw ids or fixture notes on screen: " + text);
			Assert.True(text.Contains("Spirit Wave") && text.Contains("Rising Talisman") && text.Contains("River Grab"), "the names are the data's");
		}
		finally { Close(runner, screen); }
	});

	[Test]
	public static void Footer_ChipsAreArrowsEnterTabEsc_AndThereIsNoBackButton(Node runner) => OnFixtures(() =>
	{
		var screen = Open(runner, Shopping(ShopScreenFixtures.StockedPool(), 120));
		try
		{
			Assert.True(screen.FindChild("BackButton", true, false) == null && screen.FindChildren("*", "Button", true, false).Count == 0, "no Back button: Esc / B is the one back action");
			var footer = screen.GetNode<KeyChipFooter>("Footer");
			footer.Pinned = FooterDevice.Keyboard;
			Assert.True(string.Join("|", footer.ChipTexts()) == "ARROWS|ENTER|TAB|ESC", "keyboard chips: " + string.Join("|", footer.ChipTexts()));
			footer.Pinned = FooterDevice.Pad;
			Assert.True(string.Join("|", footer.ChipTexts()) == "D-PAD|A|Y|B", "pad chips: " + string.Join("|", footer.ChipTexts()));
			Assert.True(string.Join("|", Enumerable.Range(0, 4).Select(i => footer.GetNode<Label>("Action" + i).Text)) == "Move|Buy / Release|Frame data|Back to rest stop", "labels as in the mockup");
		}
		finally { Close(runner, screen); }
	});

	[Test]
	public static void Model_RefusalKinds_CoinsSlotFullOther(Node runner) => OnFixtures(() =>
	{
		var pool = ShopScreenFixtures.StockedPool();
		var poor = ShopScreenModel.Create(Shopping(pool, 30))!;
		Assert.True(poor.Specials.All(e => e.Refusal == ShopRefusal.Coins) && poor.Modifiers.All(e => e.Refusal == ShopRefusal.Coins), "30 coins: coins");
		var full = ShopScreenModel.Create(Shopping(pool, 500, (SpecialSlot.A, "test-spirit-wave", 1), (SpecialSlot.B, "test-rising-talisman", 1), (SpecialSlot.C, "test-foxfire", 1)))!;
		Assert.True(full.Specials.First(e => e.Id == "test-slippery-skin").Refusal == ShopRefusal.SlotFull && full.Specials.First(e => e.Id == "test-river-grab").Refusal == ShopRefusal.None, "the occupied slot's special is SlotFull, the free one is buyable");
		var closed = ShopScreenModel.Create(ShopScreenFixtures.At(pool, 500, ShopScreenFixtures.Starters))!;
		Assert.True(closed.Specials.All(e => e.Refusal == ShopRefusal.Other), "a closed shop is neither coins nor slots");
	});
}
