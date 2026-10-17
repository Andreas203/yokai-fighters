using System.Linq;
using YokaiFighters.SaveGame;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-95: the merchant shop's purchases in the save (R6, M5), in memory only (no file). Kept in a file of its own
/// so only save test files name the save store (see SaveProbes' isolation scan).
/// </summary>
public static partial class MerchantShopTests
{
	[Test]
	public static void Save_RoundTripsAPurchaseAtNodeCompletion() => OnFixtures(() =>
	{
		var pool = ShopPool();
		var s = At(pool, 100, true, StartersAnd((SpecialSlot.C, "test-foxfire", 2)));
		RunSave? completed = null;
		s.NodeCompleted += _ => completed = RunSave.Capture(s);
		var shop = Shopping(s);
		Assert.True(shop.Buy("test-kappa-spin").Ok, "buy");
		s.WinFight(700);
		Assert.True(s.CompleteNode().Ok, "complete the merchant node");
		string text = SaveStore.Serialize(SaveData.Empty.WithRun(completed));
		Assert.True(text.Contains("\"borrowed\": true") && text.Contains($"\"version\": {SaveStore.CurrentVersion}"), "the file has the borrowed slot");
		var load = SaveStore.Parse(text);
		Assert.Equal(SaveLoadStatus.Loaded, load.Status, "current version loads");
		var back = load.Data.Run!.Restore(pool, DefaultMap(), Table());
		Assert.True(back.StateHash() == s.StateHash(), "restores exactly (StateHash)");
		Assert.True(back.Run.Coins == 50 && back.Run[SpecialSlot.D] is { Borrowed: true, Level: 1 } && back.Run[SpecialSlot.C] is { Borrowed: false }, "coins and borrowed ink came back");
	});

	[Test]
	public static void M5_QuitInTheShop_RestartsTheNodeWithEntryCoinsAndSlots() => OnFixtures(() =>
	{
		var pool = ShopPool();
		var s = At(pool, 100, false, StartersAnd((SpecialSlot.C, "test-foxfire", 1)));
		RunSave? entered = null;
		s.NodeEntered += _ => entered = RunSave.Capture(s); // the save point the boot flow uses on entering a node
		Assert.True(s.Enter(Merchant4).Ok, "enter");
		ulong entry = s.StateHash();
		var shop = Shopping(s);
		Assert.True(shop.Release(SpecialSlot.C).Ok && shop.Buy("test-kappa-spin").Ok, "release then buy");
		Assert.True(s.Run.Coins == 10, "100 - 40 - 50");
		// Quit here: Continue loads the entry save (M5).
		var back = SaveStore.Parse(SaveStore.Serialize(SaveData.Empty.WithRun(entered))).Data.Run!.Restore(pool, DefaultMap(), Table());
		Assert.True(back.StateHash() == entry, "M5: the node restarts at its entry state");
		Assert.True(back.Run.Coins == 100 && back.Run[SpecialSlot.C]?.Data.Id == "test-foxfire" && back.Run[SpecialSlot.D] is null, "entry coins and slots");
		Assert.True(back.Merchant is { Choice: MerchantChoice.None, CanHeal: true }, "the stop's one choice is open again");
	});

	[Test]
	public static void Save_RoundTripsABoughtModifier_AndM5RestartsWithoutIt() => OnFixtures(() =>
	{
		var pool = ShopPool();
		var s = At(pool, 100, false, StartersAnd((SpecialSlot.C, "test-foxfire", 2)));
		RunSave? entered = null, completed = null;
		s.NodeEntered += _ => entered = RunSave.Capture(s);
		s.NodeCompleted += _ => completed = RunSave.Capture(s);
		Assert.True(s.Enter(Merchant4).Ok, "enter");
		ulong entry = s.StateHash();
		var shop = Shopping(s);
		Assert.True(shop.BuyModifier("test-kappa-wisp", SpecialSlot.C).Ok && s.Run.Coins == 50, "a modifier bought onto Foxfire");

		// M5: quit here, Continue loads the entry save.
		var again = SaveStore.Parse(SaveStore.Serialize(SaveData.Empty.WithRun(entered))).Data.Run!.Restore(pool, DefaultMap(), Table());
		Assert.True(again.StateHash() == entry && again.Run.Coins == 100 && again.Run[SpecialSlot.C] is { Modifier: null, ModifierBorrowed: false }, "M5: entry coins, no modifier");
		Assert.True(again.Merchant is { Choice: MerchantChoice.None, CanHeal: true }, "the choice is open again");

		// Completing the node saves it.
		s.WinFight(700);
		Assert.True(s.CompleteNode().Ok, "complete the merchant node");
		string text = SaveStore.Serialize(SaveData.Empty.WithRun(completed));
		Assert.True(text.Contains("\"modifier_borrowed\": true") && text.Contains("\"modifier\": \"test-kappa-wisp\""), "the file has the borrowed modifier");
		var load = SaveStore.Parse(text);
		Assert.Equal(SaveLoadStatus.Loaded, load.Status, "current version loads");
		var back = load.Data.Run!.Restore(pool, DefaultMap(), Table());
		Assert.True(back.StateHash() == s.StateHash(), "restores exactly (StateHash)");
		Assert.True(back.Run.Coins == 50 && back.Run[SpecialSlot.C] is { Level: 2, Borrowed: false, ModifierBorrowed: true } c && c.Modifier!.Id == "test-kappa-wisp", "coins, level and the borrowed modifier came back");
		Assert.True(back.Run[SpecialSlot.A] is { ModifierBorrowed: false }, "and only on that slot");
	});

	[Test]
	public static void Save_Version2File_LoadsWithNothingBorrowed() => OnFixtures(() =>
	{
		var pool = ShopPool();
		var s = At(pool, 100, true, StartersAnd((SpecialSlot.C, "test-foxfire", 1)));
		string v3 = SaveStore.Serialize(SaveData.Empty.WithRun(RunSave.Capture(s)));
		string v2 = string.Join("\n", v3.Split('\n').Where(l => !l.Contains("borrowed\"")))
			.Replace($"\"version\": {SaveStore.CurrentVersion}", "\"version\": 2").Replace("\"levelled\": 0,", "\"levelled\": 0");
		var load = SaveStore.Parse(v2);
		Assert.Equal(SaveLoadStatus.OlderVersion, load.Status, "version 2 is older");
		Assert.True(!v2.Contains("borrowed") && load.Data.Run!.Slots.All(x => !x.Borrowed && !x.ModifierBorrowed), "missing borrowed / modifier_borrowed = false");
		Assert.True(load.Data.Run!.Restore(pool, DefaultMap(), Table()).StateHash() == s.StateHash(), "and restores the same run");

		// A borrowed starter is not a state the shop can reach (R6, A1): the file parses, the run is refused on restore.
		int at = v3.IndexOf("\"borrowed\": false", System.StringComparison.Ordinal);
		string starter = v3.Remove(at, "\"borrowed\": false".Length).Insert(at, "\"borrowed\": true");
		var starterLoad = SaveStore.Parse(starter);
		Assert.True(starterLoad.Data.Run is { } sr && sr.Slots[0] is { Slot: "A", Borrowed: true }, "the file itself is readable");
		bool refused = false;
		try { starterLoad.Data.Run!.Restore(pool, DefaultMap(), Table()); }
		catch (System.FormatException e) { refused = e.Message.Contains("starter"); }
		Assert.True(refused, "a borrowed starter is refused by RunSave.Restore");

		var badMod = SaveStore.Parse(v3.Replace("\"modifier_borrowed\": false", "\"modifier_borrowed\": 1"));
		Assert.True(badMod.Data.Run is null && badMod.Problems.Any(p => p.Contains("modifier_borrowed")), "a modifier_borrowed flag that is not true/false drops the run");
		// Borrowed ink on a modifier that is not there: readable, refused on restore.
		int m = v3.IndexOf("\"modifier_borrowed\": false", System.StringComparison.Ordinal);
		var ghost = SaveStore.Parse(v3.Remove(m, "\"modifier_borrowed\": false".Length).Insert(m, "\"modifier_borrowed\": true"));
		bool ghostRefused = false;
		try { ghost.Data.Run!.Restore(pool, DefaultMap(), Table()); }
		catch (System.FormatException) { ghostRefused = true; }
		Assert.True(ghostRefused, "modifier_borrowed without a modifier is refused by RunSave.Restore");

		string bad = v3.Replace("\"borrowed\": false", "\"borrowed\": \"yes\"");
		var badLoad = SaveStore.Parse(bad);
		Assert.True(badLoad.Data.Run is null && badLoad.Problems.Any(p => p.Contains("borrowed")), "a borrowed flag that is not true/false drops the run");
	});
}
