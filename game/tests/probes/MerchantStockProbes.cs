using System;
using System.Collections.Generic;
using System.Linq;
using YokaiFighters.Fight;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>Probes for AM27 / R6: the merchant always has something to sell, and a visit never drops a shown card.</summary>
public static class MerchantStockProbes
{
	static MapGraph Map() => FileMapSource.Default(ContentPaths.Data("map")).Load();

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

	static string[] Shown(MerchantShop shop) => shop.Shelf.Select(x => "s:" + x.Id).Concat(shop.ModifierShelf.Select(m => "m:" + m.Id)).ToArray();

	// AM27/R6: a buy removes only its own card; a release removes none; the shelf holds at least 3 while enough commons exist.
	[Test]
	public static void Probe_RandomVisits_NoShownCardVanishes_AcrossEveryWalk()
	{
		var pool = FightScene.LoadAbilityPool();
		var fails = new List<string>();
		int visits = 0;
		foreach (var merchant in Map().Nodes.Where(n => n.Type == NodeTypeRegistry.Merchant))
			foreach (var walk in WalksTo(merchant.Id))
				for (int variant = 0; variant < 3; variant++)
				{
					var s = DraftFromBeatenYokaiTests.At(pool, walk, merchant.Id);
					s.Run.RestoreCoins(2000);
					var shop = s.Merchant!.Shop;
					s.Merchant.Choose(MerchantChoice.Shop);
					visits++;
					var rng = new Random(variant * 7919 + walk.Length);
					string ctx = string.Join(">", walk) + ">" + merchant.Id;
					for (int step = 0; step < 14; step++)
					{
						var before = Shown(shop);
						string act = "none"; string gone = "";
						int kind = rng.Next(3);
						if (kind == 0 && shop.Shelf.Where(x => shop.CanBuy(x.Id)).ToArray() is { Length: > 0 } cb)
						{ var x = cb[rng.Next(cb.Length)]; shop.Buy(x.Id); act = "buy " + x.Id; gone = "s:" + x.Id; }
						else if (kind == 1)
						{
							var opts = shop.ModifierShelf.SelectMany(m => shop.ModifierTargets(m.Id).Select(t => (m, t))).Where(p => shop.CanBuyModifier(p.m.Id, p.t)).ToArray();
							if (opts.Length > 0) { var p = opts[rng.Next(opts.Length)]; shop.BuyModifier(p.m.Id, p.t); act = $"buymod {p.m.Id}->{p.t}"; gone = "m:" + p.m.Id; }
						}
						else
						{
							var slots = new[] { SpecialSlot.A, SpecialSlot.B, SpecialSlot.C, SpecialSlot.D }.Where(sl => shop.CanRelease(sl)).ToArray();
							if (slots.Length > 0) { var sl = slots[rng.Next(slots.Length)]; act = $"release {sl}:{s.Run[sl]!.Data.Id}"; shop.Release(sl); }
						}
						var after = Shown(shop);
						var vanished = before.Where(c => c != gone && !after.Contains(c)).ToArray();
						if (vanished.Length > 0) fails.Add($"{ctx} v{variant} step{step} [{act}] vanished {string.Join(",", vanished)}: {string.Join(",", before)} -> {string.Join(",", after)}");
						if (after.Length != after.Distinct().Count()) fails.Add($"{ctx} duplicate cards {string.Join(",", after)}");
					}
				}
		Assert.True(fails.Count == 0, $"{visits} visits, {fails.Count} vanishings; first: {string.Join(" || ", fails.Take(3))}");
	}

	// AM27: shelf >= min(3, every sellable common) after any sequence; never above max(3, unbound count).
	[Test]
	public static void Probe_ShelfSizeBounds_HoldThroughoutAVisit()
	{
		var pool = FightScene.LoadAbilityPool();
		var fails = new List<string>();
		foreach (var merchant in Map().Nodes.Where(n => n.Type == NodeTypeRegistry.Merchant))
			foreach (var walk in WalksTo(merchant.Id))
			{
				var s = DraftFromBeatenYokaiTests.At(pool, walk, merchant.Id);
				s.Run.RestoreCoins(2000);
				var shop = s.Merchant!.Shop;
				s.Merchant.Choose(MerchantChoice.Shop);
				var rng = new Random(walk.Length * 31 + 5);
				var soldUnbound = new HashSet<string>();
				for (int step = 0; step < 12; step++)
				{
					if (rng.Next(2) == 0) { var b = shop.Shelf.FirstOrDefault(x => shop.CanBuy(x.Id)); if (b != null) { bool ub = !s.BoundYokai().Contains(b.Source); if (shop.Buy(b.Id).Ok && ub) soldUnbound.Add(b.Id); } }
					else { var sl = new[] { SpecialSlot.C, SpecialSlot.D, SpecialSlot.B, SpecialSlot.A }.FirstOrDefault(x => shop.CanRelease(x)); if (shop.CanRelease(sl)) shop.Release(sl); }
					var bound = s.BoundYokai();
					int total = shop.Shelf.Count + shop.ModifierShelf.Count;
					int unbound = shop.Shelf.Count(x => !bound.Contains(x.Source)) + shop.ModifierShelf.Count(m => !bound.Contains(m.Source));
					var all = MerchantShop.ShelfFor(s.Run, pool, Array.Empty<string>()).Count + MerchantShop.ModifierShelfFor(s.Run, pool, Array.Empty<string>()).Count;
					if (total < Math.Min(3, all)) fails.Add($"{merchant.Id} step{step}: total {total} < min(3,{all})");
					int back = shop.Shelf.Count(x => !bound.Contains(x.Source) && soldUnbound.Contains(x.Id));
					if (total > Math.Max(3, unbound - back) + back) fails.Add($"{merchant.Id} step{step}: total {total} > max(3,{unbound - back})+{back}");
				}
			}
		Assert.True(fails.Count == 0, string.Join(" || ", fails.Take(4)));
	}

	// Stale Merchant from an earlier stop cannot buy, and does not change the shelf of the new stop.
	[Test]
	public static void Probe_StaleMerchantReference_RefusedAndInert()
	{
		var pool = FightScene.LoadAbilityPool();
		var s = DraftFromBeatenYokaiTests.At(pool, new[] { "r1-apprentice", "r2-oni", "r3-kappa" }, "r4-merchant");
		s.Run.RestoreCoins(500);
		var old = s.Merchant!;
		old.Choose(MerchantChoice.Shop);
		s.WinFight(10);
		Assert.True(s.CompleteNode().Ok, "leave the stop");
		var staleShelf = old.Shop.Shelf;
		if (staleShelf.Count > 0)
		{
			ulong h = s.StateHash(); int coins = s.Run.Coins;
			Assert.True(!old.Shop.Buy(staleShelf[0].Id).Ok, "a stale shop refuses buy");
			Assert.True(h == s.StateHash() && coins == s.Run.Coins, "refused buy changes nothing");
		}
		Assert.True(!old.Shop.Release(SpecialSlot.B).Ok, "a stale shop refuses release");
	}

	// Refused actions leave shelf, hash, coins untouched; the bot's buy-everything loop terminates; restore matches.
	[Test]
	public static void Probe_RefusedActions_ChangeNothing_AndBuyLoopTerminates()
	{
		var pool = FightScene.LoadAbilityPool();
		var fails = new List<string>();
		foreach (var merchant in Map().Nodes.Where(n => n.Type == NodeTypeRegistry.Merchant))
			foreach (var walk in WalksTo(merchant.Id))
			{
				var s = DraftFromBeatenYokaiTests.At(pool, walk, merchant.Id);
				var shop = s.Merchant!.Shop;
				s.Merchant.Choose(MerchantChoice.Shop);
				s.Run.RestoreCoins(0);
				var shelf = Shown(shop); ulong h = s.StateHash();
				foreach (var x in shop.Shelf.ToArray()) shop.Buy(x.Id);
				foreach (var m in shop.ModifierShelf.ToArray()) foreach (var sl in new[] { SpecialSlot.A, SpecialSlot.B, SpecialSlot.C, SpecialSlot.D }) shop.BuyModifier(m.Id, sl);
				foreach (var sl in new[] { SpecialSlot.A, SpecialSlot.B, SpecialSlot.C, SpecialSlot.D }) shop.Release(sl);
				shop.Buy("nonexistent");
				if (!Shown(shop).SequenceEqual(shelf) || h != s.StateHash()) fails.Add(merchant.Id + " no-coin refusals changed things");
				// last special release refused with money
				s.Run.RestoreCoins(5000);
				int guard = 0;
				for (; guard < 60; guard++)
				{
					var sl = new[] { SpecialSlot.A, SpecialSlot.B, SpecialSlot.C, SpecialSlot.D }.FirstOrDefault(x => shop.CanRelease(x));
					if (!shop.CanRelease(sl)) break;
					shop.Release(sl);
				}
				if (s.Run.SpecialCount < 1) fails.Add(merchant.Id + " released the last special");
				var before = Shown(shop); ulong h2 = s.StateHash();
				foreach (var sl in new[] { SpecialSlot.A, SpecialSlot.B, SpecialSlot.C, SpecialSlot.D }) if (!shop.CanRelease(sl)) shop.Release(sl);
				if (!Shown(shop).SequenceEqual(before) || h2 != s.StateHash()) fails.Add(merchant.Id + " refused release changed things");
				// bot loop
				int n = 0; bool any = true;
				while (any && n < 100)
				{
					any = false;
					foreach (var x in shop.Shelf) if (shop.CanBuy(x.Id) && shop.Buy(x.Id).Ok) { any = true; break; }
					if (any) { n++; continue; }
					foreach (var m in shop.ModifierShelf) { foreach (var sl in shop.ModifierTargets(m.Id).OrderBy(z => z)) if (shop.CanBuyModifier(m.Id, sl) && shop.BuyModifier(m.Id, sl).Ok) { any = true; break; } if (any) break; }
					if (any) n++;
				}
				if (n >= 100) fails.Add(merchant.Id + " bot loop did not terminate");
			}
		Assert.True(fails.Count == 0, string.Join(" || ", fails.Take(4)));
	}
}
