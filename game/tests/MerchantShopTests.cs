using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-95: the merchant's shop (R6, T5 with T2, M3, M5), sim only. The fixture pool plus a few made-up specials
/// (clones of the fixture Foxfire with another id, source, rarity, slot or lock) so the shelf has something to
/// sell and something it must never sell. Runs are put at the row-4 merchant of the default map with a walk of
/// r1-apprentice, r2-kitsune, r3-oni (every node there fights as the Kitsune stand-in, so the Kitsune is bound).
/// The save round trips (purchase saved at node completion, M5 quit in the shop, version 2 files) are in
/// MerchantShopSaveTests.cs, the same class.
/// </summary>
public static partial class MerchantShopTests
{
	static string MapDir => ContentPaths.Data("map");
	static NodeSceneTable Table() => NodeSceneTable.Default(MapDir);
	static MapGraph DefaultMap() => FileMapSource.Default(MapDir).Load();
	static readonly string[] Walk = { "r1-apprentice", "r2-kitsune", "r3-oni" };
	const string Merchant4 = "r4-merchant";

	static void OnFixtures(Action body)
	{
		var saved = FightScene.Sources;
		var savedRun = FightScene.Run;
		FightScene.Sources = FightScene.FixtureSources;
		FightScene.Run = null;
		try { body(); }
		finally { FightScene.Sources = saved; FightScene.Run = savedRun; }
	}

	static SpecialData Clone(string id, string source, string rarity, string slot, bool locked = false)
	{
		var node = JsonNode.Parse(File.ReadAllText(Path.Combine(ContentPaths.FixturesDir, "rewards", "test-foxfire.json")))!.AsObject();
		node["id"] = id;
		node["source"] = source;
		node["rarity"] = rarity;
		node["locked"] = locked;
		node["input"]!["kata"]!["slot"] = slot;
		node["input"]!["kihon"]!["slot"] = slot;
		return SpecialLoader.Parse(node.ToJsonString(), id);
	}

	/// <summary>Fixture pool + oni common (D), oni rare (D), kappa common (C), kappa locked common (D), kitsune common (D).</summary>
	static AbilityPool ShopPool()
	{
		var fx = FightScene.LoadAbilityPool();
		var extra = new[]
		{
			Clone("test-oni-charge", "oni", "common", "D"),
			Clone("test-oni-quake", "oni", "rare", "D"),
			Clone("test-kappa-grab", "kappa", "common", "C"),
			Clone("test-kappa-sealed", "kappa", "common", "D", locked: true),
			Clone("test-kitsune-mirage", "kitsune", "common", "D"),
		};
		return new AbilityPool(fx.Specials.Concat(extra), fx.Modifiers);
	}

	static SpecialData S(AbilityPool pool, string id) => pool.Special(id) ?? throw new InvalidOperationException(id);

	/// <summary>
	/// A session whose walk has completed r1-r3 with <paramref name="slots"/> and <paramref name="coins"/>, standing on
	/// the map before the merchant; <paramref name="enter"/> = also enter <c>r4-merchant</c>.
	/// </summary>
	static RunSession At(AbilityPool pool, int coins, bool enter, params (SpecialSlot Slot, string Id, int Level)[] slots)
	{
		var graph = DefaultMap();
		var run = RunState.Restore(600, slots.Select(x => (x.Slot, new OwnedSpecial(S(pool, x.Id), x.Level, null))));
		var order = new int[5];
		int rank = 0;
		foreach (var x in slots.OrderBy(x => x.Slot)) if (x.Level > 1) order[(int)x.Slot] = ++rank;
		run.RestoreLevelledOrder(order[1], order[2], order[3], order[4]);
		run.RestoreCoins(coins);
		run.SetMap(RunMap.Restore(graph, new RunMapSnapshot(graph.Id, Walk, Walk[^1])));
		var s = new RunSession(run, pool, Table(), 7);
		if (enter) Assert.True(s.Enter(Merchant4).Ok, "enter the merchant");
		return s;
	}

	static readonly (SpecialSlot, string, int)[] Starters = { (SpecialSlot.A, "test-spirit-wave", 1), (SpecialSlot.B, "test-rising-talisman", 1) };
	static (SpecialSlot, string, int)[] StartersAnd(params (SpecialSlot, string, int)[] more) => Starters.Concat(more).ToArray();

	static MerchantShop Shopping(RunSession s)
	{
		Assert.True(s.Merchant!.Choose(MerchantChoice.Shop).Ok, "R6: the shop is chosen");
		return s.Merchant.Shop;
	}

	[Test]
	public static void Prices_AreR6() =>
		Assert.True(MerchantShop.BuyCost == 50 && MerchantShop.ReleaseCost == 40 && MerchantShop.SoldRarity == "common", "R6: 50 to buy, 40 to release, commons only");

	[Test]
	public static void Shelf_CommonsFromUnboundYokai_NeverRaresLockedStartersOrOwned() => OnFixtures(() =>
	{
		var pool = ShopPool();
		var s = At(pool, 75, true, StartersAnd());
		Assert.Equal("kitsune", string.Join(",", s.BoundYokai()), "the stand-in Kitsune is the one yokai bound on this walk");
		var shelf = s.Merchant!.Shop.Shelf.Select(x => x.Id).ToArray();
		Assert.Equal("test-oni-charge,test-kappa-grab", string.Join(",", shelf), "R6: unbound commons, in pool order (file-name order for a loaded pool)");
		// The same rule from the run alone: nothing bound, owning the oni common.
		var run = RunState.Restore(600, new[] { (SpecialSlot.D, new OwnedSpecial(S(pool, "test-oni-charge"), 1, null)) });
		var open = MerchantShop.ShelfFor(run, pool, Array.Empty<string>()).Select(x => x.Id).ToArray();
		Assert.True(open.Contains("test-foxfire") && open.Contains("test-kitsune-mirage") && open.Contains("test-kappa-grab"), "unbound kitsune commons are sold");
		Assert.True(!open.Contains("test-oni-charge"), "an owned special is not sold again");
		foreach (var id in open)
		{
			var d = S(pool, id);
			Assert.True(d.Rarity == "common" && !d.Locked && !d.Starter, $"{id}: R6/A9/A11/A1");
		}
		Assert.True(!open.Contains("test-oni-quake") && !open.Contains("test-kappa-sealed"), "never a rare (R6), never a locked ability (A11)");
		Assert.True(!pool.Modifiers.Any(m => open.Contains(m.Id)), "modifiers are not on the shelf");
	});

	[Test]
	public static void Buy_Costs50_Lv1_BorrowedInk_InItsOwnSlot() => OnFixtures(() =>
	{
		var s = At(ShopPool(), 75, true, StartersAnd());
		var shop = Shopping(s);
		Assert.True(shop.Buy("test-oni-charge").Ok, "bought");
		Assert.Equal(25, s.Run.Coins, "75 - 50");
		Assert.True(s.Run[SpecialSlot.D] is { Level: 1, Borrowed: true, Modifier: null } d && d.Data.Id == "test-oni-charge", "A3 Lv 1, R6 borrowed ink, slot D");
		Assert.True(!shop.Shelf.Any(x => x.Id == "test-oni-charge"), "no longer on the shelf");
		Assert.True(!s.Run.CarryOverCandidates.Any(o => o.Special.Borrowed) && s.Run.CarryOverCandidates.Count() == 2, "M3/R6: borrowed ink never carries over");
		Assert.True(s.Run.Owned.Count() == 3, "it is Ryo's to fight with this run");
	});

	[Test]
	public static void Buy_Refusals_ChangeNothing() => OnFixtures(() =>
	{
		var s = At(ShopPool(), 49, true, StartersAnd((SpecialSlot.C, "test-foxfire", 1)));
		var shop = s.Merchant!.Shop;
		ulong before = s.StateHash();
		Assert.True(!shop.Buy("test-oni-charge").Ok && !shop.CanBuy("test-oni-charge"), "R6: refused before the shop is chosen");
		Assert.True(s.StateHash() == before, "unchanged");
		Shopping(s);
		before = s.StateHash();
		var poor = shop.Buy("test-oni-charge");
		Assert.True(!poor.Ok && poor.Error!.Contains("50 coins"), "49 coins: refused");
		Assert.True(s.StateHash() == before && s.Run.Coins == 49 && s.Run[SpecialSlot.D] is null, "insufficient coins: state unchanged");
		s.Run.AddCoins(100);
		before = s.StateHash();
		foreach (var id in new[] { "test-oni-quake", "test-kappa-sealed", "test-kitsune-mirage", "test-spirit-wave", "test-foxfire", "test-fox-patience", "nope" })
			Assert.True(!shop.Buy(id).Ok, $"{id}: not on the shelf");
		var full = shop.Buy("test-kappa-grab");
		Assert.True(!full.Ok && full.Error!.Contains("release it first"), "slot C holds Foxfire: R6 frees a slot by release");
		Assert.True(s.StateHash() == before && s.Run.Coins == 149, "every refusal changed nothing");
	});

	[Test]
	public static void HealAndShop_AreOneChoice() => OnFixtures(() =>
	{
		var healed = At(ShopPool(), 200, true, StartersAnd());
		Assert.True(healed.Merchant!.TakeHeal().Ok, "heal taken");
		var shop = healed.Merchant.Shop;
		Assert.True(!shop.Open && !shop.Buy("test-oni-charge").Ok && !shop.Release(SpecialSlot.A).Ok, "R6: no shop after the heal");
		Assert.True(!healed.Merchant.Choose(MerchantChoice.Shop).Ok, "and the shop can't be chosen");

		var shopped = At(ShopPool(), 200, true, StartersAnd());
		Shopping(shopped);
		int health = shopped.Run.Health;
		Assert.True(!shopped.Merchant!.TakeHeal().Ok && shopped.Run.Health == health, "R6: Choose(Shop) closes the heal");
		ulong chosen = shopped.StateHash();
		Assert.True(shopped.Merchant.Shop.Buy("test-oni-charge").Ok, "buy");
		Assert.True(shopped.StateHash() != chosen, "a purchase is in the state hash");
		Assert.True(!shopped.Merchant.TakeHeal().Ok, "still no heal after buying");
	});

	[Test]
	public static void Release_Costs40_FreesTheSlot_AndDropsItFromTheCopyTarget() => OnFixtures(() =>
	{
		var s = At(ShopPool(), 100, true, StartersAnd((SpecialSlot.C, "test-foxfire", 2)));
		Assert.Equal("test-foxfire", CopyRule.Target(s.Run)!.Data.Id, "T2: the Lv 2 Foxfire is the target");
		var shop = Shopping(s);
		Assert.True(shop.Release(SpecialSlot.C).Ok, "released");
		Assert.True(s.Run.Coins == 60 && s.Run[SpecialSlot.C] is null && s.Run.LevelledOrder(SpecialSlot.C) == 0, "100 - 40, slot C free");
		Assert.Equal("test-spirit-wave", CopyRule.Target(s.Run)!.Data.Id, "T2: a released move no longer counts (T3 lower slot among the Lv 1s)");
		Assert.True(shop.Buy("test-kappa-grab").Ok && s.Run[SpecialSlot.C]!.Borrowed, "the freed slot takes a purchase");
		Assert.True(s.Run.Coins == 10, "60 - 50");
	});

	[Test]
	public static void Release_NeverRyosLastSpecial_NorAStarter_NorWithoutCoins() => OnFixtures(() =>
	{
		var last = At(ShopPool(), 500, true, (SpecialSlot.C, "test-foxfire", 1));
		var shop = Shopping(last);
		ulong before = last.StateHash();
		var refused = shop.Release(SpecialSlot.C);
		Assert.True(!refused.Ok && refused.Error!.Contains("T5"), "T5: the last special stays");
		Assert.True(last.StateHash() == before && last.Run.Coins == 500 && CopyRule.Target(last.Run) is not null, "nothing changed, the copy target stays defined");
		Assert.True(!shop.Release(SpecialSlot.A).Ok && !shop.Release(SpecialSlot.D).Ok && !shop.Release(SpecialSlot.None).Ok, "empty slots and non-slots: refused");

		var starters = At(ShopPool(), 500, true, StartersAnd());
		var s2 = Shopping(starters);
		var own = s2.Release(SpecialSlot.A);
		Assert.True(!own.Ok && own.Error!.Contains("starter"), "a starter is Ryo's own, not a binding");

		var poor = At(ShopPool(), 39, true, StartersAnd((SpecialSlot.C, "test-foxfire", 1)));
		var s3 = Shopping(poor);
		before = poor.StateHash();
		Assert.True(!s3.Release(SpecialSlot.C).Ok && poor.StateHash() == before && poor.Run[SpecialSlot.C] is not null, "39 coins: refused, unchanged");
		Assert.True(Throws(() => RunState.Restore(600, new[] { (SpecialSlot.C, new OwnedSpecial(S(ShopPool(), "test-foxfire"), 1, null)) }).Release(SpecialSlot.C)), "RunState.Release guards T5 too");
	});

	[Test]
	public static void Shop_OnlyWhileRyoStandsAtTheStop() => OnFixtures(() =>
	{
		var s = At(ShopPool(), 200, true, StartersAnd());
		var shop = Shopping(s);
		s.WinFight(800); // the merchant node's stand-in fight (no bind, no coins)
		Assert.True(s.CompleteNode().Ok, "leave the stop");
		ulong after = s.StateHash();
		Assert.True(!shop.Open && !shop.Buy("test-oni-charge").Ok, "a kept shop reference refuses once Ryo has left");
		Assert.True(s.StateHash() == after && s.Merchant is null, "unchanged");
	});

	[Test]
	public static void Borrowed_IsRunState() => OnFixtures(() =>
	{
		var pool = ShopPool();
		RunState Make(bool borrowed) => RunState.Restore(600, new[] { (SpecialSlot.D, new OwnedSpecial(S(pool, "test-oni-charge"), 1, null, borrowed)) });
		Assert.True(Make(true).StateHash() != Make(false).StateHash(), "the borrowed flag alone changes the hash");
		Assert.True(Throws(() => RunState.Restore(600, new[] { (SpecialSlot.A, new OwnedSpecial(S(pool, "test-spirit-wave"), 1, null, true)) })), "a starter is never borrowed");
		var run = Make(true);
		Assert.Equal(0, run.CarryOverCandidates.Count(), "M3: nothing borrowed carries over");
	});

	static bool Throws(Action a)
	{
		try { a(); return false; }
		catch (Exception) { return true; }
	}
}
