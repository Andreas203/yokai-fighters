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
/// r1-apprentice, r2-kitsune, r3-oni. On today's node-scene table the Oni is a stand-in that fights with the
/// Kitsune's kit, so the walk exercises the stand-in rule of <see cref="RunSession.BoundYokai"/>: the Kitsune (the
/// fighter) and the Oni (the yokai the map names) are both bound; the Kappa is not.
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

	static ModifierData CloneMod(string from, string id, string source, string rarity, bool locked = false)
	{
		var node = JsonNode.Parse(File.ReadAllText(Path.Combine(ContentPaths.FixturesDir, "rewards", from + ".json")))!.AsObject();
		node["id"] = id;
		node["source"] = source;
		node["rarity"] = rarity;
		node["locked"] = locked;
		return ModifierLoader.Parse(node.ToJsonString(), id);
	}

	/// <summary>Fixture pool + kappa commons (D, C), oni common (D), oni rare (D), kappa locked common (D), kitsune common (D).</summary>
	static AbilityPool ShopPool()
	{
		var fx = FightScene.LoadAbilityPool();
		var extra = new[]
		{
			Clone("test-kappa-spin", "kappa", "common", "D"),
			Clone("test-oni-charge", "oni", "common", "D"),
			Clone("test-oni-quake", "oni", "rare", "D"),
			Clone("test-kappa-grab", "kappa", "common", "C"),
			Clone("test-kappa-sealed", "kappa", "common", "D", locked: true),
			Clone("test-kitsune-mirage", "kitsune", "common", "D"),
		};
		// Modifiers: the fixtures' two are the Kitsune's (bound on the test walk); these are clones from other yokai.
		var mods = new[]
		{
			CloneMod("test-fox-patience", "test-kappa-patience", "kappa", "common"), // applies to any special
			CloneMod("test-will-o-wisp", "test-kappa-wisp", "kappa", "common"),      // needs a projectile
			CloneMod("test-fox-patience", "test-oni-patience", "oni", "common"),
			CloneMod("test-fox-patience", "test-kappa-rare-mod", "kappa", "rare"),
			CloneMod("test-fox-patience", "test-kappa-sealed-mod", "kappa", "common", locked: true),
		};
		return new AbilityPool(fx.Specials.Concat(extra), fx.Modifiers.Concat(mods));
	}

	static SpecialData S(AbilityPool pool, string id) => pool.Special(id) ?? throw new InvalidOperationException(id);

	/// <summary>
	/// A session whose walk has completed r1-r3 with <paramref name="slots"/> and <paramref name="coins"/>, standing on
	/// the map before the merchant; <paramref name="enter"/> = also enter <c>r4-merchant</c>.
	/// </summary>
	static RunSession At(AbilityPool pool, int coins, bool enter, params (SpecialSlot Slot, string Id, int Level)[] slots) =>
		At(pool, coins, enter ? Merchant4 : null, Walk, MerchantChoice.None, slots);

	/// <summary>
	/// The general form: <paramref name="walk"/> completed, then <paramref name="enter"/> entered through
	/// <see cref="RunSession.Enter"/>; or, with a <paramref name="restored"/> choice, a session put back while Ryo
	/// stands at <paramref name="enter"/> with that choice already made (R6).
	/// </summary>
	static RunSession At(AbilityPool pool, int coins, string? enter, string[] walk, MerchantChoice restored, params (SpecialSlot Slot, string Id, int Level)[] slots)
	{
		var graph = DefaultMap();
		var run = RunState.Restore(600, slots.Select(x => (x.Slot, new OwnedSpecial(S(pool, x.Id), x.Level, null))));
		var order = new int[5];
		int rank = 0;
		foreach (var x in slots.OrderBy(x => x.Slot)) if (x.Level > 1) order[(int)x.Slot] = ++rank;
		run.RestoreLevelledOrder(order[1], order[2], order[3], order[4]);
		run.RestoreCoins(coins);
		bool inNode = restored != MerchantChoice.None;
		run.SetMap(RunMap.Restore(graph, new RunMapSnapshot(graph.Id, walk, inNode ? enter : walk[^1])));
		var s = new RunSession(run, pool, Table(), 7, restored);
		if (enter is not null && !inNode) Assert.True(s.Enter(enter).Ok, $"enter {enter}");
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
		var oniNode = Table().Resolve(DefaultMap().Node("r3-oni")).Context;
		Assert.True(oniNode is { StandIn: true, Fighter: "kitsune", Yokai: "oni", Binds: true }, "this walk exercises the stand-in: the Oni node fights with the Kitsune's kit");
		var bound = s.BoundYokai();
		Assert.True(bound.Contains("kitsune") && bound.Contains("oni") && !bound.Contains("kappa"), "stand-in: the fighter and the yokai the map names are both bound: " + string.Join(",", bound));
		Assert.Equal(bound.Count, bound.Distinct().Count(), "once each");
		var shelf = s.Merchant!.Shop.Shelf.Select(x => x.Id).ToArray();
		Assert.Equal("test-kappa-spin,test-kappa-grab", string.Join(",", shelf), "R6: unbound commons, in pool order (file-name order for a loaded pool); the beaten Oni's common is not sold");
		// The same rule from the run alone: nothing bound, owning the oni common.
		var run = RunState.Restore(600, new[] { (SpecialSlot.D, new OwnedSpecial(S(pool, "test-kappa-spin"), 1, null)) });
		var open = MerchantShop.ShelfFor(run, pool, Array.Empty<string>()).Select(x => x.Id).ToArray();
		Assert.True(open.Contains("test-foxfire") && open.Contains("test-kitsune-mirage") && open.Contains("test-kappa-grab"), "unbound kitsune commons are sold");
		Assert.True(!open.Contains("test-kappa-spin"), "an owned special is not sold again");
		foreach (var id in open)
		{
			var d = S(pool, id);
			Assert.True(d.Rarity == "common" && !d.Locked && !d.Starter, $"{id}: R6/A9/A11/A1");
		}
		Assert.True(!open.Contains("test-oni-quake") && !open.Contains("test-kappa-sealed"), "never a rare (R6), never a locked ability (A11)");
		// Modifiers are common abilities too (A9, A11): the same rule, on their own list.
		Assert.Equal("test-kappa-patience,test-kappa-wisp", string.Join(",", s.Merchant.Shop.ModifierShelf.Select(m => m.Id)),
			"R6: common modifiers from unbound yokai, pool order; none of the bound Kitsune's or Oni's, no rare, no locked");
		var mods = MerchantShop.ModifierShelfFor(run, pool, Array.Empty<string>());
		Assert.True(mods.Count > 0 && mods.All(m => m.Rarity == "common" && !m.Locked), "never a rare, never a locked modifier");
		Assert.Equal(pool.Modifiers.Count(m => m.Rarity == "common" && !m.Locked), mods.Count, "nothing bound: every common unlocked modifier is sold");
		Assert.True(!pool.Modifiers.Any(m => open.Contains(m.Id)) && !pool.Specials.Any(x => mods.Any(m => m.Id == x.Id)), "specials and modifiers stay on their own lists");
	});

	[Test]
	public static void Buy_Costs50_Lv1_BorrowedInk_InItsOwnSlot() => OnFixtures(() =>
	{
		var s = At(ShopPool(), 75, true, StartersAnd());
		var shop = Shopping(s);
		Assert.True(shop.Buy("test-kappa-spin").Ok, "bought");
		Assert.Equal(25, s.Run.Coins, "75 - 50");
		Assert.True(s.Run[SpecialSlot.D] is { Level: 1, Borrowed: true, Modifier: null } d && d.Data.Id == "test-kappa-spin", "A3 Lv 1, R6 borrowed ink, slot D");
		Assert.True(!shop.Shelf.Any(x => x.Id == "test-kappa-spin"), "no longer on the shelf");
		Assert.True(!s.Run.CarryOverCandidates.Any(o => o.Special.Borrowed) && s.Run.CarryOverCandidates.Count() == 2, "M3/R6: borrowed ink never carries over");
		Assert.True(s.Run.Owned.Count() == 3, "it is Ryo's to fight with this run");
	});

	[Test]
	public static void Buy_Refusals_ChangeNothing() => OnFixtures(() =>
	{
		var s = At(ShopPool(), 49, true, StartersAnd((SpecialSlot.C, "test-foxfire", 1)));
		var shop = s.Merchant!.Shop;
		ulong before = s.StateHash();
		Assert.True(!shop.Buy("test-kappa-spin").Ok && !shop.CanBuy("test-kappa-spin"), "R6: refused before the shop is chosen");
		Assert.True(s.StateHash() == before, "unchanged");
		Shopping(s);
		before = s.StateHash();
		var poor = shop.Buy("test-kappa-spin");
		Assert.True(!poor.Ok && poor.Error!.Contains("50 coins"), "49 coins: refused");
		Assert.True(s.StateHash() == before && s.Run.Coins == 49 && s.Run[SpecialSlot.D] is null, "insufficient coins: state unchanged");
		s.Run.AddCoins(100);
		before = s.StateHash();
		foreach (var id in new[] { "test-oni-charge", "test-oni-quake", "test-kappa-sealed", "test-kitsune-mirage", "test-spirit-wave", "test-foxfire", "test-fox-patience", "nope" })
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
		Assert.True(!shop.Open && !shop.Buy("test-kappa-spin").Ok && !shop.Release(SpecialSlot.A).Ok, "R6: no shop after the heal");
		Assert.True(!healed.Merchant.Choose(MerchantChoice.Shop).Ok, "and the shop can't be chosen");

		var shopped = At(ShopPool(), 200, true, StartersAnd());
		Shopping(shopped);
		int health = shopped.Run.Health;
		Assert.True(!shopped.Merchant!.TakeHeal().Ok && shopped.Run.Health == health, "R6: Choose(Shop) closes the heal");
		ulong chosen = shopped.StateHash();
		Assert.True(shopped.Merchant.Shop.Buy("test-kappa-spin").Ok, "buy");
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
		Assert.True(!shop.Open && !shop.Buy("test-kappa-spin").Ok, "a kept shop reference refuses once Ryo has left");
		Assert.True(s.StateHash() == after && s.Merchant is null, "unchanged");
	});

	[Test]
	public static void Borrowed_IsRunState() => OnFixtures(() =>
	{
		var pool = ShopPool();
		RunState Make(bool borrowed) => RunState.Restore(600, new[] { (SpecialSlot.D, new OwnedSpecial(S(pool, "test-kappa-spin"), 1, null, borrowed)) });
		Assert.True(Make(true).StateHash() != Make(false).StateHash(), "the borrowed flag alone changes the hash");
		Assert.True(Throws(() => RunState.Restore(600, new[] { (SpecialSlot.A, new OwnedSpecial(S(pool, "test-spirit-wave"), 1, null, true)) })), "a starter is never borrowed");
		var run = Make(true);
		Assert.Equal(0, run.CarryOverCandidates.Count(), "M3: nothing borrowed carries over");
	});

	[Test]
	public static void Borrowed_StaysBorrowedWhenLevelled_AndNeverCarriesOver() => OnFixtures(() =>
	{
		var pool = ShopPool();
		var s = At(pool, 60, true, StartersAnd());
		Assert.True(Shopping(s).Buy("test-kappa-spin").Ok, "bought");
		Assert.Equal(2, s.Run.LevelUp(SpecialSlot.D), "levelled by hand");
		Assert.True(s.Run[SpecialSlot.D] is { Level: 2, Borrowed: true }, "still borrowed ink at Lv 2");
		Assert.Equal(SpecialSlot.D, s.Run.Equip(S(pool, "test-kappa-spin")), "drafting it later levels it (A3)");
		Assert.True(s.Run[SpecialSlot.D] is { Level: 3, Borrowed: true }, "still borrowed ink at Lv 3");
		Assert.True(s.Run.CarryOverCandidates.Select(o => o.Slot).SequenceEqual(new[] { SpecialSlot.A, SpecialSlot.B }), "M3/R6: the Lv 3 borrowed special is not a carry-over candidate");
		Assert.Equal("test-kappa-spin", CopyRule.Target(s.Run)!.Data.Id, "T2: it is in a slot, so it is the copy target");
	});

	[Test]
	public static void Release_ABorrowedSpecial_AndT5WhenOnlyBorrowedInkIsLeft() => OnFixtures(() =>
	{
		// Releasing what was bought: 40 more coins, the slot is free and the special is back on the shelf.
		var s = At(ShopPool(), 90, true, StartersAnd());
		var shop = Shopping(s);
		Assert.True(shop.Buy("test-kappa-spin").Ok && shop.Release(SpecialSlot.D).Ok, "bought, then released");
		Assert.True(s.Run.Coins == 0 && s.Run[SpecialSlot.D] is null && shop.Shelf.Any(x => x.Id == "test-kappa-spin"), "90 - 50 - 40, slot D free, sold again");

		// One bound special (C) and one borrowed (D): the bound one may go, then the borrowed one is Ryo's last (T5).
		var last = At(ShopPool(), 500, true, (SpecialSlot.C, "test-foxfire", 1));
		var shop2 = Shopping(last);
		Assert.True(shop2.Buy("test-kappa-spin").Ok, "borrowed into D");
		Assert.True(shop2.Release(SpecialSlot.C).Ok, "the last non-borrowed special can be released while a borrowed one remains");
		ulong before = last.StateHash();
		var refused = shop2.Release(SpecialSlot.D);
		Assert.True(!refused.Ok && refused.Error!.Contains("T5"), "T5: the borrowed special is now Ryo's last");
		Assert.True(last.StateHash() == before && last.Run.Coins == 410 && CopyRule.Target(last.Run)!.Data.Id == "test-kappa-spin", "unchanged; the copy target stays defined");
		Assert.Equal(0, last.Run.CarryOverCandidates.Count(), "and nothing here would carry over (M3)");
	});

	[Test]
	public static void RestoredChoice_ShopStaysOpen_HealKeepsTheShopClosed() => OnFixtures(() =>
	{
		var shopped = At(ShopPool(), 100, Merchant4, Walk, MerchantChoice.Shop, StartersAnd());
		Assert.True(shopped.Merchant is { Choice: MerchantChoice.Shop, CanHeal: false } && shopped.Merchant.Shop.Open, "a restored Shop choice: shop open, heal closed");
		Assert.True(!shopped.Merchant!.TakeHeal().Ok, "R6: no heal");
		Assert.True(shopped.Merchant.Shop.Buy("test-kappa-spin").Ok && shopped.Run.Coins == 50, "and it sells");

		var healed = At(ShopPool(), 100, Merchant4, Walk, MerchantChoice.Heal, StartersAnd());
		ulong before = healed.StateHash();
		Assert.True(healed.Merchant is { Choice: MerchantChoice.Heal } && !healed.Merchant.Shop.Open, "a restored Heal choice: shop closed");
		Assert.True(!healed.Merchant!.Shop.Buy("test-kappa-spin").Ok && !healed.Merchant.Choose(MerchantChoice.Shop).Ok && healed.StateHash() == before, "R6: nothing sold, nothing changed");
	});

	[Test]
	public static void Row7Merchant_ShopsTheSame_WithTheWalksBindings() => OnFixtures(() =>
	{
		var walk = new[] { "r1-apprentice", "r2-kitsune", "r3-oni", "r4-merchant", "r5-nine-tails", "r6-kitsune" };
		var s = At(ShopPool(), 90, "r7-merchant", walk, MerchantChoice.None, StartersAnd());
		Assert.True(s.Merchant is { NodeId: "r7-merchant", Choice: MerchantChoice.None }, "a new stop with its own choice");
		var bound = s.BoundYokai();
		Assert.True(bound.Contains("kitsune") && bound.Contains("oni") && !bound.Contains("kappa") && !bound.Contains("merchant"), "bindings of the whole walk; a merchant binds nothing: " + string.Join(",", bound));
		var shop = Shopping(s);
		Assert.Equal("test-kappa-spin,test-kappa-grab", string.Join(",", shop.Shelf.Select(x => x.Id)), "R6 shelf at row 7");
		Assert.True(shop.Buy("test-kappa-spin").Ok && shop.Release(SpecialSlot.D).Ok && s.Run.Coins == 0, "50 to buy, 40 to release");
		Assert.True(!shop.Release(SpecialSlot.A).Ok && !s.Merchant!.TakeHeal().Ok, "starters stay, heal closed");
	});

	[Test]
	public static void RealPool_ItsCommonModifiersAreSold_UnlessTheirYokaiIsBound()
	{
		var pool = FightScene.LoadAbilityPool(); // data/, not the fixtures
		var run = RunState.NewRun(pool);
		var open = MerchantShop.ModifierShelfFor(run, pool, Array.Empty<string>()).Select(m => m.Id).ToArray();
		Assert.True(open.Contains("fox-patience") && open.Contains("will-o-wisp"), "the Kitsune's two modifiers are sold while it is not bound: " + string.Join(",", open));
		Assert.Equal(pool.Modifiers.Count(m => m.Rarity == "common" && !m.Locked), open.Length, "every common unlocked modifier in data/");
		var bound = MerchantShop.ModifierShelfFor(run, pool, new[] { "kitsune" });
		Assert.True(!bound.Any(m => m.Source == "kitsune"), "none of the Kitsune's once it is bound (R6)");
	}

	[Test]
	public static void BuyModifier_Costs50_AttachesByA2_InBorrowedInk() => OnFixtures(() =>
	{
		var pool = ShopPool();
		var s = At(pool, 120, true, StartersAnd());
		var shop = Shopping(s);
		Assert.Equal("A,B", string.Join(",", shop.ModifierTargets("test-kappa-patience")), "any special takes it");
		Assert.Equal("A", string.Join(",", shop.ModifierTargets("test-kappa-wisp")), "applies_to: only the projectile (Spirit Wave)");
		ulong before = s.StateHash();
		Assert.True(shop.BuyModifier("test-kappa-wisp", SpecialSlot.A).Ok, "bought onto Spirit Wave");
		Assert.True(s.Run.Coins == 70 && s.StateHash() != before, "120 - 50, and it is in the state hash");
		Assert.True(s.Run[SpecialSlot.A] is { Borrowed: false, ModifierBorrowed: true, Level: 1 } a && a.Modifier!.Id == "test-kappa-wisp", "attached, the modifier is borrowed ink, the special is still Ryo's own");
		Assert.True(!shop.ModifierShelf.Any(m => m.Id == "test-kappa-wisp") && shop.ModifierTargets("test-kappa-wisp").Count == 0, "no longer on the shelf");
		Assert.Equal("B", string.Join(",", shop.ModifierTargets("test-kappa-patience")), "A2: Spirit Wave has its one modifier");
		// The same attach as the draft's: Ryo's Spirit Wave is built with it.
		int plain = S(pool, "test-spirit-wave").Build(1, false).Projectile!.Speed;
		var worn = s.Run[SpecialSlot.A]!;
		Assert.True(worn.Data.Build(1, false, worn.Modifier, null).Projectile!.Speed > plain, "the modifier works on Ryo's special");
		// M3: the special may carry over, the borrowed modifier never does.
		var carry = s.Run.CarryOverCandidates.Single(o => o.Slot == SpecialSlot.A).Special;
		Assert.True(carry.Modifier is null && !carry.ModifierBorrowed && s.Run.CarryOverCandidates.Count() == 2, "listed for carry-over without the borrowed modifier");
		// A borrowed special can wear a borrowed modifier: both are borrowed ink.
		Assert.True(shop.Buy("test-kappa-spin").Ok && s.Run.Coins == 20, "a borrowed special");
		s.Run.AddCoins(30);
		Assert.True(shop.BuyModifier("test-kappa-patience", SpecialSlot.D).Ok, "a borrowed modifier on it");
		Assert.True(s.Run[SpecialSlot.D] is { Borrowed: true, ModifierBorrowed: true } && s.Run.Coins == 0, "both flags");
	});

	[Test]
	public static void BuyModifier_Refusals_SpendNothing_ChangeNothing() => OnFixtures(() =>
	{
		var pool = ShopPool();
		var s = At(pool, 49, true, StartersAnd());
		var shop = s.Merchant!.Shop;
		ulong before = s.StateHash();
		Assert.True(!shop.BuyModifier("test-kappa-patience", SpecialSlot.A).Ok && !shop.CanBuyModifier("test-kappa-patience", SpecialSlot.A), "R6: refused before the shop is chosen");
		Assert.True(s.StateHash() == before, "unchanged");
		Shopping(s);
		before = s.StateHash();
		var poor = shop.BuyModifier("test-kappa-patience", SpecialSlot.A);
		Assert.True(!poor.Ok && poor.Error!.Contains("50 coins") && s.StateHash() == before && s.Run[SpecialSlot.A]!.Modifier is null, "49 coins: refused, unchanged");
		s.Run.AddCoins(151);
		before = s.StateHash();
		foreach (var id in new[] { "test-fox-patience", "test-will-o-wisp", "test-oni-patience", "test-kappa-rare-mod", "test-kappa-sealed-mod", "test-kappa-spin", "nope" })
			Assert.True(!shop.BuyModifier(id, SpecialSlot.A).Ok, $"{id}: not on the modifier shelf");
		Assert.True(!shop.Buy("test-kappa-patience").Ok, "a modifier is not bought as a special");
		var wrong = shop.BuyModifier("test-kappa-wisp", SpecialSlot.B);
		Assert.True(!wrong.Ok && wrong.Error!.Contains("A2"), "applies_to: Rising Talisman has no projectile");
		Assert.True(!shop.BuyModifier("test-kappa-patience", SpecialSlot.C).Ok && !shop.BuyModifier("test-kappa-patience", SpecialSlot.None).Ok, "an empty slot, not a slot");
		Assert.True(s.StateHash() == before && s.Run.Coins == 200, "every refusal spent nothing and changed nothing");
		Assert.True(shop.BuyModifier("test-kappa-patience", SpecialSlot.A).Ok, "bought");
		before = s.StateHash();
		var second = shop.BuyModifier("test-kappa-wisp", SpecialSlot.A);
		Assert.True(!second.Ok && second.Error!.Contains("one modifier per special") && s.StateHash() == before && s.Run.Coins == 150, "A2: the worn modifier is not replaced, nothing spent");
	});

	[Test]
	public static void BoughtModifier_NeverChangesTheCopy() => OnFixtures(() =>
	{
		var pool = ShopPool();
		var s = At(pool, 200, true, StartersAnd());
		var shop = Shopping(s);
		var before = CopyRule.Target(s.Run)!;
		int speed = before.Build().Projectile!.Speed;
		Assert.True(shop.BuyModifier("test-kappa-wisp", SpecialSlot.A).Ok && shop.BuyModifier("test-kappa-patience", SpecialSlot.B).Ok, "two modifiers bought");
		var after = CopyRule.Target(s.Run)!;
		Assert.True(after == before && after.Data.Id == "test-spirit-wave", "T2/T3: the target is the same special at the same level");
		Assert.Equal(speed, after.Build().Projectile!.Speed, "T4: the copy never has Ryo's modifier");
		var mine = s.Run[SpecialSlot.A]!;
		Assert.True(mine.Data.Build(mine.Level, false, mine.Modifier, null).Projectile!.Speed > speed, "while Ryo's own is faster");
	});

	[Test]
	public static void BoughtModifier_GoesWithItsSpecial() => OnFixtures(() =>
	{
		var pool = ShopPool();
		var s = At(pool, 300, true, StartersAnd((SpecialSlot.C, "test-foxfire", 1)));
		var shop = Shopping(s);
		Assert.True(shop.BuyModifier("test-kappa-patience", SpecialSlot.C).Ok, "onto Foxfire");
		Assert.Equal(2, s.Run.LevelUp(SpecialSlot.C), "levelled");
		Assert.True(s.Run[SpecialSlot.C] is { Level: 2, ModifierBorrowed: true }, "levelling keeps the modifier and its ink");
		Assert.True(shop.Release(SpecialSlot.C).Ok, "the special is released");
		Assert.True(s.Run[SpecialSlot.C] is null && !s.Run.HasModifier("test-kappa-patience"), "A6: the modifier goes with it");
		Assert.True(shop.ModifierShelf.Any(m => m.Id == "test-kappa-patience"), "and is sold again");
		// A draft that replaces the special drops the bought modifier with it (A6).
		Assert.True(shop.BuyModifier("test-kappa-patience", SpecialSlot.A).Ok, "onto Spirit Wave");
		var other = Clone("test-other-a", "kappa", "common", "A");
		s.Run.Equip(other);
		Assert.True(s.Run[SpecialSlot.A] is { Modifier: null, ModifierBorrowed: false, Borrowed: false }, "replaced: no modifier, no borrowed ink left behind");
		Assert.True(Throws(() => RunState.Restore(600, new[] { (SpecialSlot.A, new OwnedSpecial(S(pool, "test-spirit-wave"), 1, null, false, true)) })), "a borrowed modifier needs a modifier");
	});

	static bool Throws(Action a)
	{
		try { a(); return false; }
		catch (Exception) { return true; }
	}
}
