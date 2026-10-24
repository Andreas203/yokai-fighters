using System;
using System.Collections.Generic;
using System.Linq;
using YokaiFighters.Fight;
using YokaiFighters.SaveGame;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>Test Breaker probes for YOK-178 (A7, A8, A9, E20, R6, M5): the draft comes from the yokai beaten.</summary>
public static class DraftFromBeatenYokaiProbes
{
	static string MapDir => ContentPaths.Data("map");
	static NodeSceneTable Table() => NodeSceneTable.Default(MapDir);
	static MapGraph Map() => FileMapSource.Default(MapDir).Load();

	static string[] PathTo(string node)
	{
		var g = Map();
		var q = new Queue<string[]>();
		foreach (var n in g.Nodes.Where(n => n.Row == 1)) q.Enqueue(new[] { n.Id });
		while (q.Count > 0)
		{
			var p = q.Dequeue();
			if (p[^1] == node) return p[..^1];
			foreach (var nx in g.Next(p[^1])) q.Enqueue(p.Append(nx).ToArray());
		}
		throw new InvalidOperationException("no path to " + node);
	}

	static RunSession At(AbilityPool pool, string node, uint seed) => DraftFromBeatenYokaiTests.At(pool, PathTo(node), node, seed);
	static string Sig(RewardOffer o) => o.Yokai + "|" + string.Join(",", o.Cards.Select(c => $"{c.Kind}:{c.AbilityId}:{c.Source}"));
	static bool Drafts(MapNodeData n) => Table().Resolve(n).Context.Binds && Map().Next(n.Id).Count > 0;

	// A8/A7: every card of every binding node comes from the draft yokai; upgrades only for owned specials.
	[Test]
	public static void EveryNode_EveryCardIsTheDraftYokais_UpgradesOnlyOwned()
	{
		var pool = FightScene.LoadAbilityPool();
		foreach (var n in Map().Nodes)
		{
			var ctx = Table().Resolve(n).Context;
			for (uint seed = 1; seed <= 25; seed++)
			{
				var s = At(pool, n.Id, seed);
				var owned = s.Run.Owned.Select(o => o.Special.Data.Id).ToHashSet();
				var d = s.WinFight(500);
				if (!Drafts(n)) { Assert.True(d is null, $"{n.Id}: no draft (R3/boss)"); continue; }
				Assert.True(d is not null && d.Offer.Cards.Count <= 3, $"{n.Id} seed {seed}: a draft of at most 3");
				var ids = d!.Offer.Cards.Select(c => c.Kind + c.AbilityId).ToArray();
				Assert.Equal(ids.Length, ids.Distinct().Count(), $"{n.Id} seed {seed}: no duplicate card");
				foreach (var c in d.Offer.Cards)
				{
					if (c.Kind == CardKind.Upgrade) Assert.True(owned.Contains(c.AbilityId), $"{n.Id}: upgrade of unowned {c.AbilityId}");
					else Assert.Equal(ctx.DraftYokai, c.Source, $"{n.Id} seed {seed}: {c.AbilityId}");
				}
			}
		}
	}

	// A9: a fixture rare present, no non-elder node offers it.
	[Test]
	public static void FixtureRare_NeverAtNonElderNodes() => MerchantShopTests.OnFixtures(() =>
	{
		var pool = MerchantShopTests.ShopPool();
		foreach (var n in Map().Nodes.Where(n => Drafts(n) && !Table().Resolve(n).Context.Elder))
			for (uint seed = 1; seed <= 30; seed++)
				foreach (var c in At(pool, n.Id, seed).WinFight(500)!.Offer.Cards.Where(c => c.Kind != CardKind.Upgrade))
				{
					var r = pool.Specials.Where(x => x.Id == c.AbilityId).Select(x => x.Rarity).Concat(pool.Modifiers.Where(x => x.Id == c.AbilityId).Select(x => x.Rarity));
					Assert.True(r.All(x => x == "common"), $"{n.Id} seed {seed}: {c.AbilityId} is a rare at a non-elder node (A9)");
				}
	});

	// E20: the Kappa's small pool: 1 to 3 cards, no duplicate, no crash.
	[Test]
	public static void KappaSmallPool_FewerCards_NoCrashNoDuplicate()
	{
		var pool = FightScene.LoadAbilityPool();
		for (uint seed = 1; seed <= 60; seed++)
		{
			var o = At(pool, "r3-kappa", seed).WinFight(500)!.Offer;
			var keys = o.Cards.Select(c => $"{c.Kind}:{c.AbilityId}").ToArray();
			Assert.True(keys.Length is >= 1 and <= 3, $"seed {seed}: {keys.Length} cards");
			Assert.Equal(keys.Length, keys.Distinct().Count(), $"seed {seed}: duplicate in {string.Join(",", keys)}");
		}
	}

	[Test]
	public static void Batches_AreDeterministic()
	{
		var pool = FightScene.LoadAbilityPool();
		string Batch() => string.Join("\n", Map().Nodes.Where(Drafts)
			.SelectMany(n => Enumerable.Range(1, 12).Select(i => n.Id + i + ":" + Sig(At(pool, n.Id, (uint)i).WinFight(300)!.Offer))));
		Assert.Equal(Batch(), Batch(), "same seeds, same offers");
		Assert.True(Enumerable.Range(1, 30).Select(i => Sig(At(pool, "r2-oni", (uint)i).WinFight(300)!.Offer)).Distinct().Count() > 1, "different seeds differ");
	}

	// R6 (designer 2026-10-21): every elder binds its yokai and its own id; the shop after it sells none of that yokai.
	[Test]
	public static void EveryElder_BindsItsYokai_AndTheShopAfterItSellsNone()
	{
		var pool = FightScene.LoadAbilityPool();
		foreach (var n in Map().Nodes.Where(n => Table().Resolve(n).Context.Elder))
		{
			var ctx = Table().Resolve(n).Context;
			var walk = PathTo(n.Id).Append(n.Id).ToArray();
			var g = Map();
			var run = RunState.NewRun(pool);
			run.SetMap(RunMap.Restore(g, new RunMapSnapshot(g.Id, walk, walk[^1])));
			var b = new RunSession(run, pool, Table(), 5).BoundYokai();
			Assert.True(b.Contains(ctx.DraftYokai) && b.Contains(n.Yokai!), $"{n.Id}: bound {string.Join(",", b)} lacks {ctx.DraftYokai}/{n.Yokai}");
			Assert.Equal(b.Count, b.Distinct().Count(), $"{n.Id}: once each");
			Assert.True(!MerchantShop.ModifierShelfFor(run, pool, b).Any(m => m.Source == ctx.DraftYokai), $"{n.Id}: the shop still sells {ctx.DraftYokai} modifiers");
			Assert.True(!MerchantShop.ShelfFor(run, pool, b).Any(x => x.Source == ctx.DraftYokai), $"{n.Id}: the shop still sells {ctx.DraftYokai} specials");
		}
		Assert.Equal(0, DraftFromBeatenYokaiTests.At(pool, Array.Empty<string>(), "r1-apprentice").BoundYokai().Count, "new run binds nothing");
		Assert.Equal("kitsune,apprentice,oni", string.Join(",", DraftFromBeatenYokaiTests.At(pool, new[] { "r1-apprentice", "r2-kitsune", "r3-oni" }, "r4-merchant").BoundYokai()), "kitsune once");
	}

	// M5: restored session offers the same cards and binds the same yokai at every binding node.
	[Test]
	public static void SaveResume_SameOfferAtEveryNode()
	{
		var pool = FightScene.LoadAbilityPool();
		foreach (var n in Map().Nodes.Where(Drafts))
			foreach (uint seed in new uint[] { 3, 17, 4242 })
			{
				var live = At(pool, n.Id, seed);
				var back = RunSave.Capture(live).Restore(pool, Map(), Table());
				Assert.True(back.BoundYokai().SequenceEqual(live.BoundYokai()), $"{n.Id}: bound list survives");
				Assert.Equal(Sig(live.WinFight(700)!.Offer), Sig(back.WinFight(700)!.Offer), $"{n.Id} seed {seed}");
			}
	}

	[Test]
	public static void OldStyleSave_ElderWalk_OffersTheYokaisCards()
	{
		var pool = FightScene.LoadAbilityPool();
		var back = RunSave.Capture(At(pool, "r5-elder-oni", 9)).Restore(pool, Map(), Table());
		var o = back.WinFight(400)!.Offer;
		Assert.Equal("oni", o.Yokai, "A8");
		foreach (var c in o.Cards.Where(c => c.Kind != CardKind.Upgrade)) Assert.Equal("oni", c.Source, c.AbilityId);
	}

	// Binding card: an elder is named by its own id ("Elder Oni"), any other node by the yokai drafted; no placeholder left.
	[Test]
	public static void BindingCardText_PerNode()
	{
		var pool = FightScene.LoadAbilityPool();
		var sess = At(pool, "r2-oni", 1);
		var src = new Ui.DraftRewardSource(sess.WinFight(400)!, sess.Run);
		var expect = new Dictionary<string, string> { ["r1-apprentice"] = "Kitsune", ["r2-oni"] = "Oni", ["r3-kappa"] = "Kappa", ["r5-elder-oni"] = "Elder Oni", ["r8-elder-kappa"] = "Elder Kappa" };
		foreach (var n in Map().Nodes.Where(n => Table().Resolve(n).Context.Binds))
		{
			string name = Story.StoryLibrary.DisplayName(Table().Resolve(n).Context.BindingName);
			if (expect.TryGetValue(n.Id, out var e)) Assert.Equal(e, name, n.Id);
			string line = src.BindingLine(name);
			Assert.True(line.Contains(name) && !line.Contains("{"), $"{n.Id}: '{line}'");
		}
	}
}
