using System;
using System.Collections.Generic;
using System.Linq;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

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
