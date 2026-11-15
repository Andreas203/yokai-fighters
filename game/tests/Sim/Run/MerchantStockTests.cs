using System;
using System.Collections.Generic;
using System.Linq;
using YokaiFighters.Fight;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>
/// AM27: the merchant always has something to sell. R6 stocks commons from yokai not bound this run; when those come to
/// fewer than <see cref="MerchantShop.MinStock"/> items, commons from bound yokai top the shelf up to three, buyable-now
/// first. Real data (data/moves, data/modifiers) and the default map, so these walk the merchants a player meets.
/// </summary>
public static class MerchantStockTests
{
	static MapGraph Map() => FileMapSource.Default(ContentPaths.Data("map")).Load();

	/// <summary>Every walk from row 1 that ends just before <paramref name="node"/>.</summary>
	static IEnumerable<string[]> WalksTo(string node)
	{
		var g = Map();
		var q = new Queue<string[]>(g.Nodes.Where(n => n.Row == 1).Select(n => new[] { n.Id }));
		while (q.Count > 0)
		{
			var p = q.Dequeue();
			if (p[^1] == node) { yield return p[..^1]; continue; }
			foreach (var next in g.Next(p[^1])) q.Enqueue(p.Append(next).ToArray());
		}
	}

	static (IReadOnlyList<SpecialData> Specials, IReadOnlyList<ModifierData> Modifiers) Stock(RunState run, AbilityPool pool, IEnumerable<string> bound) =>
		(MerchantShop.ShelfFor(run, pool, bound), MerchantShop.ModifierShelfFor(run, pool, bound));

	[Test]
	public static void EveryMerchantOnTheMap_EveryWalk_SellsAtLeastThree()
	{
		var pool = FightScene.LoadAbilityPool();
		int walks = 0;
		foreach (var merchant in Map().Nodes.Where(n => n.Type == NodeTypeRegistry.Merchant))
			foreach (var walk in WalksTo(merchant.Id))
			{
				var s = DraftFromBeatenYokaiTests.At(pool, walk, merchant.Id);
				var shop = s.Merchant!.Shop;
				int stock = shop.Shelf.Count + shop.ModifierShelf.Count;
				Assert.True(stock >= 3, $"AM27, one full shelf: {string.Join(">", walk)} > {merchant.Id}: {stock} items (bound {string.Join(",", s.BoundYokai())})");
				walks++;
			}
		Assert.True(walks > 1, "walked the map's merchants: " + walks);
	}

	[Test]
	public static void EveryYokaiBound_TopUpIsExactlyThree_BuyableNowFirst_InPoolOrder()
	{
		var pool = FightScene.LoadAbilityPool();
		var run = RunState.NewRun(pool); // starters in A and B: slot C is empty, every modifier has a special to try
		var (sp, md) = Stock(run, pool, new[] { "kitsune", "oni", "kappa" });
		Assert.Equal(MerchantShop.MinStock, sp.Count + md.Count, "topped up to three: " + string.Join(",", sp.Select(x => x.Id).Concat(md.Select(m => m.Id))));
		Assert.True(sp.All(x => run[x.Slot] is null) && md.All(m => run.Owned.Any(o => run.CanAttach(o.Slot, m))), "everything topped up can be bought now");
		Assert.True(sp.All(x => x.Rarity == "common" && !x.Starter && pool.IsAvailable(x)) && md.All(m => m.Rarity == "common" && pool.IsAvailable(m)), "still never a rare, a starter or a locked ability");
		var order = pool.Specials.Select(x => x.Id).ToList();
		Assert.True(sp.Select(x => order.IndexOf(x.Id)).SequenceEqual(sp.Select(x => order.IndexOf(x.Id)).OrderBy(i => i)), "specials keep pool order");
		var again = Stock(run, pool, new[] { "kappa", "oni", "kitsune" });
		Assert.True(again.Specials.SequenceEqual(sp) && again.Modifiers.SequenceEqual(md), "deterministic: the same run gives the same stock");
	}

	[Test]
	public static void ThreeOrMoreFromUnboundYokai_NoTopUp()
	{
		var pool = FightScene.LoadAbilityPool();
		var run = RunState.NewRun(pool);
		var (sp, md) = Stock(run, pool, new[] { "kitsune" });
		Assert.True(sp.Count + md.Count >= MerchantShop.MinStock, "the Oni and the Kappa stock the shelf");
		Assert.True(sp.All(x => x.Source != "kitsune") && md.All(m => m.Source != "kitsune"), "R6: nothing of the bound Kitsune while the shelf is full enough");
	}

	[Test]
	public static void ShortStock_TopsUpOnlyTheMissingItems_AndKeepsTheUnbound()
	{
		var pool = FightScene.LoadAbilityPool();
		var run = RunState.NewRun(pool);
		var bound = new[] { "kitsune", "oni" };
		var (free, freeMods) = Stock(run, pool, Array.Empty<string>());
		int kappa = free.Count(x => x.Source == "kappa") + freeMods.Count(m => m.Source == "kappa");
		var (sp, md) = Stock(run, pool, bound);
		int unbound = sp.Count(x => !bound.Contains(x.Source)) + md.Count(m => !bound.Contains(m.Source));
		Assert.Equal(kappa, unbound, "every Kappa common is still sold");
		Assert.Equal(Math.Max(MerchantShop.MinStock, kappa), sp.Count + md.Count, "bound commons fill only up to three");
	}

	static string[] Shown(MerchantShop shop) => shop.Shelf.Select(x => x.Id).Concat(shop.ModifierShelf.Select(m => m.Id)).ToArray();

	[Test]
	public static void AtAStop_ABuyFreesOnlyItsOwnPlace_AReleaseReshufflesNothing()
	{
		// Every yokai bound (Oni then Kappa, then the Elder Oni): the row-7 shelf is all top-up. Today's three commons
		// specials are all slot C, so buying one fills C; the stop's order was fixed on entry, so the others stay.
		var pool = FightScene.LoadAbilityPool();
		var s = DraftFromBeatenYokaiTests.At(pool, new[] { "r1-apprentice", "r2-oni", "r3-kappa", "r4-merchant", "r5-elder-oni", "r6-kappa" }, "r7-merchant");
		s.Run.RestoreCoins(500);
		var shop = s.Merchant!.Shop;
		Assert.True(s.Merchant.Choose(MerchantChoice.Shop).Ok, "shop chosen");
		var before = Shown(shop);
		Assert.Equal(MerchantShop.MinStock, before.Length, "a full top-up shelf: " + string.Join(",", before));
		var screen = ShopScreenModel.Create(s)!;
		string[] Cards() => screen.Specials.Concat(screen.Modifiers).Select(e => e.Id).ToArray();
		var cardsBefore = Cards();
		var buy = shop.Shelf.First(x => s.Run[x.Slot] is null);
		Assert.True(screen.Buy(buy.Id).Ok, "bought " + buy.Id + " through the screen");
		Assert.True(cardsBefore.All(Cards().Contains), $"the screen keeps every card it showed: {string.Join(",", cardsBefore)} -> {string.Join(",", Cards())}");
		var afterBuy = Shown(shop);
		Assert.True(before.Where(id => id != buy.Id).All(afterBuy.Contains), $"the unbought cards stay: {string.Join(",", before)} -> {string.Join(",", afterBuy)}");
		Assert.Equal(MerchantShop.MinStock, afterBuy.Length, "the bought card's place is filled: " + string.Join(",", afterBuy));
		Assert.True(shop.Release(buy.Slot).Ok, "released " + buy.Id);
		var afterRelease = Shown(shop);
		Assert.True(afterBuy.All(afterRelease.Contains), $"a release reshuffles nothing: {string.Join(",", afterBuy)} -> {string.Join(",", afterRelease)}");
	}

	[Test]
	public static void BuyingTheTopUp_TheNextBoundCommonTakesItsPlace_ShelfNeverEmpties()
	{
		var pool = FightScene.LoadAbilityPool();
		var run = RunState.NewRun(pool);
		var bound = new[] { "kitsune", "oni", "kappa" };
		for (int i = 0; i < 4; i++)
		{
			var (sp, md) = Stock(run, pool, bound);
			Assert.True(sp.Count + md.Count > 0, $"round {i}: the shelf is not empty");
			if (sp.FirstOrDefault(x => run[x.Slot] is null) is { } buy) run.Borrow(buy);
			else
			{
				var pick = md.SelectMany(m => run.Owned.Where(o => run.CanAttach(o.Slot, m)).Select(o => (Mod: m, o.Slot))).ToList();
				if (pick.Count == 0) break;
				run.Attach(pick[0].Slot, pick[0].Mod, borrowed: true);
			}
		}
		var (lastSp, lastMd) = Stock(run, pool, bound);
		Assert.True(lastSp.Count + lastMd.Count > 0, "after buying round after round, the merchant still has stock");
	}
}
