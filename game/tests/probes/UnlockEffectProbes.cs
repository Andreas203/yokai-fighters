using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Godot;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>YOK-153 probes: saved unlocks in the draft and the shop (M1, A7, A8, A9, A11, R6).</summary>
public static class UnlockEffectProbes
{
	static string Root => Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "..");

	static JsonObject Fx(string f) =>
		JsonNode.Parse(File.ReadAllText(Path.Combine(Root, "game", "tests", "fixtures", "rewards", f + ".json")))!.AsObject();

	static SpecialData Sp(string id, string source, string rarity, string slot, bool locked, bool starter = false)
	{
		var n = Fx("test-foxfire");
		n["id"] = id; n["source"] = source; n["rarity"] = rarity; n["locked"] = locked;
		n["input"]!["kata"]!["slot"] = slot; n["input"]!["kihon"]!["slot"] = slot;
		if (starter) n["starter"] = true;
		return SpecialLoader.Parse(n.ToJsonString(), id);
	}

	static AbilityPool Pool(params SpecialData[] extra)
	{
		var fx = AbilityPool.LoadFixtures(Root);
		return new AbilityPool(fx.Specials.Concat(extra), fx.Modifiers);
	}

	// M1/A11: WithUnlocks replaces the set, never accumulates, and leaves the source pool untouched.
	[Test]
	public static void WithUnlocks_ReplacesAndDoesNotMutateTheSource()
	{
		var s = Sp("pk-a", "kappa", "common", "D", true);
		var bare = Pool(s);
		var a = bare.WithUnlocks(new[] { "pk-a" });
		var b = a.WithUnlocks(new string[0]);
		Assert.True(a.IsAvailable(s), "a has it");
		Assert.True(!b.IsAvailable(s), "replaced by an empty list: locked again");
		Assert.True(!bare.IsAvailable(s), "the source pool is not changed");
	}

	// A1/A11: a locked starter named in the save is still never drafted or sold.
	[Test]
	public static void UnlockedStarterFlag_IsNeverOfferedOrSold_A1()
	{
		var s = Sp("pk-starter", "kappa", "common", "D", true, starter: true);
		var pool = Pool(s).WithUnlocks(new[] { "pk-starter" });
		for (uint seed = 1; seed <= 32; seed++)
			Assert.True(!RewardDraft.Build(RunState.NewRun(pool), pool, "kappa", seed, true).Offer.Cards.Any(c => c.AbilityId == "pk-starter"), "draft");
		Assert.True(!MerchantShop.ShelfFor(RunState.NewRun(pool), pool, new string[0]).Any(x => x.Id == "pk-starter"), "shop");
	}

	// A3: an unlocked special already owned is not offered again.
	[Test]
	public static void UnlockedAndOwned_IsNotOfferedAgain()
	{
		var s = Sp("pk-own", "kappa", "common", "D", true);
		var pool = Pool(s).WithUnlocks(new[] { "pk-own" });
		var run = RunState.NewRun(pool);
		run.Equip(s);
		for (uint seed = 1; seed <= 32; seed++)
			Assert.True(!RewardDraft.Build(run, pool, "kappa", seed).Offer.Cards.Any(c => c.Kind == CardKind.NewSpecial && c.AbilityId == "pk-own"), "draft");
		Assert.True(!MerchantShop.ShelfFor(run, pool, new string[0]).Any(x => x.Id == "pk-own"), "shop");
	}

	// Determinism: a huge, shuffled, duplicated list of unlocks gives the same offers as the exact set.
	[Test]
	public static void HugeShuffledUnlockList_SameOffers()
	{
		var s = Sp("pk-h", "kappa", "common", "D", true);
		var a = Pool(s).WithUnlocks(new[] { "pk-h" });
		var big = Enumerable.Range(0, 50000).Select(i => "junk-" + (i % 5000)).Append("pk-h").Reverse().ToArray();
		var b = Pool(s).WithUnlocks(big);
		for (uint seed = 1; seed <= 16; seed++)
			Assert.Equal(
				string.Join("|", RewardDraft.Build(RunState.NewRun(a), a, "kappa", seed).Offer.Cards.Select(c => c.AbilityId)),
				string.Join("|", RewardDraft.Build(RunState.NewRun(b), b, "kappa", seed).Offer.Cards.Select(c => c.AbilityId)), $"seed {seed}");
	}

	// A9/A11: a locked rare is neither drafted from a common yokai nor sold, even when unlocked; locked and not unlocked: never from an elder.
	[Test]
	public static void LockedRare_NeedsBothElderAndUnlock()
	{
		var s = Sp("pk-r", "kappa", "rare", "D", true);
		var bare = Pool(s);
		var open = bare.WithUnlocks(new[] { "pk-r" });
		for (uint seed = 1; seed <= 32; seed++)
		{
			Assert.True(!RewardDraft.Build(RunState.NewRun(open), open, "kappa", seed, false).Offer.Cards.Any(c => c.AbilityId == "pk-r"), "unlocked, common yokai");
			Assert.True(!RewardDraft.Build(RunState.NewRun(bare), bare, "kappa", seed, true).Offer.Cards.Any(c => c.AbilityId == "pk-r"), "elder, still locked");
		}
		Assert.True(Enumerable.Range(1, 64).Any(i => RewardDraft.Build(RunState.NewRun(open), open, "kappa", (uint)i, true).Offer.Cards.Any(c => c.AbilityId == "pk-r")), "elder + unlocked offers it");
	}

	// A7: with a locked ability unlocking mid-run via a new pool, exactly one upgrade card, at most three cards.
	[Test]
	public static void PoolSwappedMidRun_KeepsOneUpgradeCard_A7()
	{
		var s = Sp("pk-u", "kappa", "common", "D", true);
		var open = Pool(s).WithUnlocks(new[] { "pk-u" });
		var run = RunState.NewRun(open);
		for (uint seed = 1; seed <= 16; seed++)
		{
			var cards = RewardDraft.Build(run, open, "kappa", seed, false, true).Offer.Cards;
			Assert.True(cards.Count <= 3 && cards.Count(c => c.Kind == CardKind.Upgrade) == 1, $"seed {seed}");
		}
	}
}
