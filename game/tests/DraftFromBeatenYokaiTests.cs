using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using YokaiFighters.Fight;
using YokaiFighters.SaveGame;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-178 (A7, A8, A9, R6, R8, M5): a won node's draft comes from the yokai beaten (<see cref="NodeContext.DraftYokai"/>,
/// the table's <c>plays_as</c>), never from the stand-in body that fights for it (<see cref="NodeContext.Fighter"/>,
/// the Kitsune today). The shop's "not bound this run" uses the same identity (<see cref="NodeContext.BoundAs"/>).
/// </summary>
public static class DraftFromBeatenYokaiTests
{
	static string MapDir => ContentPaths.Data("map");
	static NodeSceneTable Table() => NodeSceneTable.Default(MapDir);
	static MapGraph DefaultMap() => FileMapSource.Default(MapDir).Load();

	static readonly string[] ToR5 = { "r1-apprentice", "r2-oni", "r3-oni", "r4-merchant" };
	static readonly string[] ToR8 = { "r1-apprentice", "r2-oni", "r3-oni", "r4-merchant", "r5-elder-oni", "r6-kappa", "r7-dojo" };

	/// <summary>A session that walked <paramref name="walk"/> and has entered <paramref name="node"/>.</summary>
	static RunSession At(AbilityPool pool, string[] walk, string node, uint seed = 11, NodeSceneTable? table = null)
	{
		var g = DefaultMap();
		var run = RunState.NewRun(pool);
		run.SetMap(RunMap.Restore(g, new RunMapSnapshot(g.Id, walk, walk.Length > 0 ? walk[^1] : null)));
		var s = new RunSession(run, pool, table ?? Table(), seed);
		Assert.True(s.Enter(node).Ok, $"enter {node}");
		return s;
	}

	static RewardOffer Win(AbilityPool pool, string[] walk, string node, uint seed = 11) =>
		(At(pool, walk, node, seed).WinFight(800) ?? throw new InvalidOperationException($"{node}: no draft")).Offer;

	/// <summary>NEW and MODIFIER cards are the yokai's own (A8); UPGRADE cards level Ryo's owned specials.</summary>
	static IEnumerable<CardView> Drafted(RewardOffer o) => o.Cards.Where(c => c.Kind != CardKind.Upgrade);

	static void OffersOnly(string node, string[] walk, string yokai)
	{
		var pool = FightScene.LoadAbilityPool();
		var seen = new SortedSet<string>(StringComparer.Ordinal);
		for (uint seed = 1; seed <= 40; seed++)
		{
			var o = Win(pool, walk, node, seed);
			Assert.Equal(yokai, o.Yokai, $"{node} seed {seed}: the offer is the beaten yokai's (A7)");
			foreach (var c in Drafted(o))
			{
				Assert.Equal(yokai, c.Source, $"{node} seed {seed}: {c.AbilityId} is {c.Source}'s, not {yokai}'s (A8)");
				seen.Add(c.AbilityId);
			}
		}
		var unlockedMods = pool.Modifiers.Where(m => m.Source == yokai && m.Rarity == "common" && pool.IsAvailable(m)).Select(m => m.Id).ToArray();
		Assert.True(unlockedMods.Length > 0 && unlockedMods.All(seen.Contains), $"{node}: every unlocked {yokai} common modifier ({string.Join(",", unlockedMods)}) is drafted over 40 seeds; seen {string.Join(",", seen)}");
	}

	[Test]
	public static void StandInNodes_FightAsTheKitsuneBody_TheBugsPrecondition()
	{
		var oni = Table().Resolve(DefaultMap().Node("r2-oni")).Context;
		Assert.True(oni is { Fighter: "kitsune", StandIn: true, Yokai: "oni", DraftYokai: "oni", BoundAs: "oni" }, $"r2-oni: body {oni.Fighter}, draft {oni.DraftYokai}");
		var elder = Table().Resolve(DefaultMap().Node("r5-elder-oni")).Context;
		Assert.True(elder is { Fighter: "kitsune", DraftYokai: "oni", BoundAs: "elder-oni" }, $"r5-elder-oni: draft {elder.DraftYokai}, bound as {elder.BoundAs}");
	}

	[Test]
	public static void BeatingTheOni_OffersOniCards() => OffersOnly("r2-oni", new[] { "r1-apprentice" }, "oni");

	[Test]
	public static void BeatingTheKappa_OffersKappaCards() => OffersOnly("r3-kappa", new[] { "r1-apprentice", "r2-kappa" }, "kappa");

	[Test]
	public static void TheApprentice_OffersTheKitsunesCards() => // GDD p.1: "an apprentice Kitsune ... You take Foxfire"
		OffersOnly("r1-apprentice", Array.Empty<string>(), "kitsune");

	[Test]
	public static void TheKitsune_StillOffersItsOwnCards() => OffersOnly("r2-kitsune", new[] { "r1-apprentice" }, "kitsune");

	[Test]
	public static void Elders_OfferTheirYokaisCards()
	{
		OffersOnly("r5-elder-oni", ToR5, "oni");
		OffersOnly("r5-nine-tails", ToR5, "kitsune");
		OffersOnly("r8-elder-kappa", ToR8, "kappa"); // the row-8 elder: a rare instead of coins (R8)
		OffersOnly("r8-elder-oni", ToR8, "oni");
	}

	[Test]
	public static void Rares_OnlyFromElders_OfTheirYokai() => MerchantShopTests.OnFixtures(() =>
	{
		// A9: the Oni's rare special (a clone) is offered by the Elder Oni and never by an Oni duel or another yokai's elder.
		var pool = MerchantShopTests.ShopPool();
		bool elderRare = false;
		for (uint seed = 1; seed <= 40; seed++)
		{
			var duel = Win(pool, new[] { "r1-apprentice" }, "r2-oni", seed);
			Assert.True(Drafted(duel).All(c => c.Source == "oni" && c.AbilityId != "test-oni-quake"), $"seed {seed}: an Oni duel offers Oni commons only");
			var elder = Win(pool, ToR5, "r5-elder-oni", seed);
			Assert.True(Drafted(elder).All(c => c.Source == "oni"), $"seed {seed}: the Elder Oni offers the Oni's cards");
			elderRare |= elder.Cards.Any(c => c.AbilityId == "test-oni-quake");
			var row8 = Win(pool, ToR8, "r8-elder-kappa", seed);
			Assert.True(Drafted(row8).All(c => c.Source == "kappa"), $"seed {seed}: the row-8 Elder Kappa offers the Kappa's cards");
		}
		Assert.True(elderRare, "the Elder Oni offers the Oni's rare on some seed (A9)");
	});

	[Test]
	public static void M5_ContinueIntoTheNode_OffersTheSameCards()
	{
		var pool = FightScene.LoadAbilityPool();
		foreach (var (walk, node) in new[] { (new[] { "r1-apprentice" }, "r2-oni"), (ToR5, "r5-elder-oni"), (new[] { "r1-apprentice", "r2-kappa" }, "r3-kappa") })
		{
			var live = At(pool, walk, node, 29);
			var back = SaveStore.Parse(SaveStore.Serialize(SaveData.Empty.WithRun(RunSave.Capture(live)))).Data.Run!.Restore(pool, DefaultMap(), Table());
			static string Ids(RunSession s) => string.Join(",", s.WinFight(700)!.Offer.Cards.Select(c => $"{c.Kind}:{c.AbilityId}:{c.Source}"));
			string a = Ids(live), b = Ids(back);
			Assert.Equal(a, b, $"{node}: the entry save brings back the same offer");
			Assert.True(a.Contains(":" + Table().Resolve(DefaultMap().Node(node)).Context.DraftYokai), $"{node}: {a}");
		}
	}

	[Test]
	public static void Shop_BodyIsNeverBound_TheBeatenYokaiIs() => MerchantShopTests.OnFixtures(() =>
	{
		// The apprentice made to play as the Kappa: the Kitsune body fights, but only the Kappa (and the name the map
		// gives, 'apprentice') is bound, so the Kitsune's commons are on the shelf and the Kappa's are not (R6).
		var o = JsonNode.Parse(File.ReadAllText(Path.Combine(MapDir, "node-scenes.json")))!.AsObject();
		o["opponents"]!["apprentice"]!["plays_as"] = "kappa";
		var table = NodeSceneTable.Parse(o.ToJsonString());
		var pool = MerchantShopTests.ShopPool();
		var s = At(pool, new[] { "r1-apprentice", "r2-oni", "r3-oni" }, "r4-merchant", table: table);
		Assert.Equal("kappa,apprentice,oni", string.Join(",", s.BoundYokai()), "bound = the yokai beaten, in walk order; never the Kitsune body");
		var shelf = MerchantShop.ShelfFor(s.Run, pool, s.BoundYokai()).Select(x => x.Source).Distinct().ToArray();
		Assert.True(shelf.Contains("kitsune") && !shelf.Contains("kappa") && !shelf.Contains("oni"), "shelf sources: " + string.Join(",", shelf));
		var mods = MerchantShop.ModifierShelfFor(s.Run, pool, s.BoundYokai()).Select(x => x.Source).Distinct().ToArray();
		Assert.True(mods.Contains("kitsune") && !mods.Contains("kappa") && !mods.Contains("oni"), "modifier shelf sources: " + string.Join(",", mods));
	});

	[Test]
	public static void Shop_RealMap_AfterOniAndKappa_SellsNeither()
	{
		var pool = FightScene.LoadAbilityPool();
		var s = At(pool, new[] { "r1-apprentice", "r2-oni", "r3-kappa" }, "r4-merchant");
		Assert.Equal("kitsune,apprentice,oni,kappa", string.Join(",", s.BoundYokai()), "the apprentice binds the Kitsune it plays as, then the Oni and the Kappa");
		Assert.True(MerchantShop.ModifierShelfFor(s.Run, pool, s.BoundYokai()).Count == 0, "every yokai with modifiers is bound: nothing on the modifier shelf");
	}

	[Test]
	public static void Shop_ElderBindsItsOwnId_TheOpenQuestionStaysOpen()
	{
		// rules.md open question: whether binding an elder binds its yokai for the shop. Unchanged: 'elder-oni' as written.
		var pool = FightScene.LoadAbilityPool();
		var s = At(pool, new[] { "r1-apprentice", "r2-kappa", "r3-kappa", "r4-merchant", "r5-elder-oni", "r6-kappa" }, "r7-merchant");
		Assert.Equal("kitsune,apprentice,kappa,elder-oni", string.Join(",", s.BoundYokai()), "no 'oni', and no Kitsune body for the elder");
		var mods = MerchantShop.ModifierShelfFor(s.Run, pool, s.BoundYokai());
		Assert.True(mods.Count > 0 && mods.All(m => m.Source == "oni"), "the Oni's commons are still sold (open): " + string.Join(",", mods.Select(m => m.Id)));
	}
}
