using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Godot;
using YokaiFighters.Flow;
using YokaiFighters.Sim;
using SimCardKind = YokaiFighters.Sim.CardKind;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-153 (M1, M2, A11): the save's unlocks reach the reward draft and the merchant shop. A <c>locked: true</c>
/// ability is offered only once its id is in the save; source (A8), rarity (A9) and the one upgrade card (A7) still
/// hold; unlocked-by-data abilities and evolutions (A5) never depend on the save. The run-to-run case through the boot
/// scene is <see cref="SaveTests"/>' <c>Boot_UnlocksFromOneRun_ReachTheNextRunsDraftAndShop_M1</c>.
/// </summary>
public static class UnlockEffectTests
{
	static string RepoRoot => Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "..");

	const string SealedSpecial = "test-kappa-sealed";
	const string SealedRare = "test-kappa-sealed-rare";
	const string OpenSpecial = "test-kappa-open";
	const string SealedMod = "test-kappa-sealed-mod";
	const string OpenMod = "test-kappa-open-mod";

	static JsonObject Fixture(string file) =>
		JsonNode.Parse(File.ReadAllText(Path.Combine(RepoRoot, "game", "tests", "fixtures", "rewards", file + ".json")))!.AsObject();

	static SpecialData Special(string id, string source, string rarity, string slot, bool locked)
	{
		var node = Fixture("test-foxfire");
		node["id"] = id;
		node["source"] = source;
		node["rarity"] = rarity;
		node["locked"] = locked;
		node["input"]!["kata"]!["slot"] = slot;
		node["input"]!["kihon"]!["slot"] = slot;
		return SpecialLoader.Parse(node.ToJsonString(), id);
	}

	static ModifierData Modifier(string id, string source, bool locked)
	{
		var node = Fixture("test-fox-patience"); // applies to any special
		node["id"] = id;
		node["source"] = source;
		node["locked"] = locked;
		return ModifierLoader.Parse(node.ToJsonString(), id);
	}

	/// <summary>The fixture pool plus Kappa abilities: a locked common, a locked rare (A9), an open common; a locked and an open modifier.</summary>
	static AbilityPool Pool()
	{
		var fx = AbilityPool.LoadFixtures(RepoRoot);
		return new AbilityPool(
			fx.Specials.Concat(new[]
			{
				Special(SealedSpecial, "kappa", "common", "D", locked: true),
				Special(SealedRare, "kappa", "rare", "D", locked: true),
				Special(OpenSpecial, "kappa", "common", "C", locked: false),
			}),
			fx.Modifiers.Concat(new[] { Modifier(SealedMod, "kappa", locked: true), Modifier(OpenMod, "kappa", locked: false) }));
	}

	/// <summary>Every ability id any of the first <paramref name="seeds"/> drafts offers as NEW or MODIFIER.</summary>
	static HashSet<string> Offered(AbilityPool pool, string yokai, bool elder = false, int seeds = 64)
	{
		var ids = new HashSet<string>(StringComparer.Ordinal);
		for (uint seed = 1; seed <= seeds; seed++)
			foreach (var c in RewardDraft.Build(RunState.NewRun(pool), pool, yokai, seed, elder).Offer.Cards)
				if (c.Kind != SimCardKind.Upgrade) ids.Add(c.AbilityId);
		return ids;
	}

	static string[] ShelfIds(AbilityPool pool, params string[] bound)
	{
		var run = RunState.NewRun(pool);
		return MerchantShop.ShelfFor(run, pool, bound).Select(s => s.Id)
			.Concat(MerchantShop.ModifierShelfFor(run, pool, bound).Select(m => m.Id)).ToArray();
	}

	static string Offers(AbilityPool pool, string yokai, uint seed, bool elder = false) =>
		string.Join("|", RewardDraft.Build(RunState.NewRun(pool), pool, yokai, seed, elder).Offer.Cards
			.Select(c => $"{c.Kind}:{c.AbilityId}:{string.Join(",", c.Targets.Select(t => t.Slot))}"));

	[Test]
	public static void LockedAndAbsentFromTheSave_IsNeverOffered()
	{
		var pool = Pool();
		var offered = Offered(pool, "kappa", elder: true);
		Assert.True(!offered.Contains(SealedSpecial) && !offered.Contains(SealedRare) && !offered.Contains(SealedMod),
			$"draft: no locked ability without an unlock (A11), got {string.Join(",", offered)}");
		Assert.True(offered.Contains(OpenSpecial) && offered.Contains(OpenMod), "draft: the open ones are offered");
		var shelf = ShelfIds(pool);
		Assert.True(!shelf.Contains(SealedSpecial) && !shelf.Contains(SealedMod), "shop: no locked ability without an unlock");
		Assert.True(shelf.Contains(OpenSpecial) && shelf.Contains(OpenMod), "shop: the open ones are sold");
	}

	[Test]
	public static void LockedAndInTheSave_IsOfferedByTheDraft()
	{
		var pool = Pool().WithUnlocks(new[] { SealedSpecial, SealedMod });
		var offered = Offered(pool, "kappa");
		Assert.True(offered.Contains(SealedSpecial), "draft: the unlocked special is offered (M1)");
		Assert.True(offered.Contains(SealedMod), "draft: the unlocked modifier is offered (M1)");
		Assert.True(offered.Contains(OpenSpecial) && offered.Contains(OpenMod), "the open ones still are");
	}

	[Test]
	public static void LockedAndInTheSave_IsSoldByTheShop()
	{
		var pool = Pool().WithUnlocks(new[] { SealedSpecial, SealedMod, SealedRare });
		var shelf = ShelfIds(pool);
		Assert.True(shelf.Contains(SealedSpecial) && shelf.Contains(SealedMod), "shop: the unlocked special and modifier are sold (M1)");
		Assert.True(!shelf.Contains(SealedRare), "shop: an unlocked rare is still never sold (R6, A9)");
		var bound = ShelfIds(pool, "kappa");
		Assert.True(!bound.Contains(SealedSpecial) && !bound.Contains(SealedMod), "shop: nor from a yokai bound this run (R6)");
	}

	[Test]
	public static void AnUnknownOrBlankIdInTheSave_IsIgnored()
	{
		var bare = Pool();
		var odd = bare.WithUnlocks(new[] { "no-such-ability", "", "   ", "TEST-KAPPA-SEALED", SealedSpecial + " " });
		Assert.Equal(0, odd.Unlocked.Count(id => id.Trim().Length == 0), "blanks are not kept");
		for (uint seed = 1; seed <= 32; seed++)
			foreach (bool elder in new[] { false, true })
				Assert.Equal(Offers(bare, "kappa", seed, elder), Offers(odd, "kappa", seed, elder), $"seed {seed}: ids naming no ability change no draft (case and padding count)");
		Assert.Equal(string.Join(",", ShelfIds(bare)), string.Join(",", ShelfIds(odd)), "nor the shelf");
		Assert.True(bare.WithUnlocks(null).Unlocked.Count == 0, "no save list at all is an empty one");
	}

	[Test]
	public static void UnlockedSources_Rarity_AndTheUpgradeCard_StillHold_A7_A8_A9()
	{
		var pool = Pool().WithUnlocks(new[] { SealedSpecial, SealedRare, SealedMod });
		foreach (string other in new[] { "kitsune", "oni" })
		{
			var offered = Offered(pool, other, elder: true);
			Assert.True(!offered.Any(id => id.StartsWith("test-kappa", StringComparison.Ordinal)), $"{other}: a Kappa ability never comes from it (A8)");
		}
		Assert.True(!Offered(pool, "kappa").Contains(SealedRare), "an unlocked rare: not from a common Kappa (A9)");
		Assert.True(Offered(pool, "kappa", elder: true).Contains(SealedRare), "an unlocked rare: from the elder Kappa (A9)");
		for (uint seed = 1; seed <= 32; seed++)
		{
			var cards = RewardDraft.Build(RunState.NewRun(pool), pool, "kappa", seed, elder: true).Offer.Cards;
			Assert.True(cards.Count <= 3, $"seed {seed}: three cards at most (A7)");
			Assert.Equal(1, cards.Count(c => c.Kind == SimCardKind.Upgrade), $"seed {seed}: exactly one upgrade card (A7)");
		}
	}

	[Test]
	public static void OpenAbilitiesAndEvolutions_DoNotDependOnTheSave_A5()
	{
		var bare = Pool();
		// Unlocking the locked ones only adds cards: what the open ones are and how the evolution reads stay the same.
		var all = bare.WithUnlocks(bare.Specials.Select(s => s.Id).Concat(bare.Modifiers.Select(m => m.Id))
			.Append("great-wave")); // the fixture Spirit Wave's evolution id (the loader keeps no evolution id)
		Assert.True(bare.Specials.Where(s => !s.Locked).All(s => bare.IsAvailable(s) && all.IsAvailable(s)), "an open special is available with any save");
		Assert.True(bare.Modifiers.Where(m => !m.Locked).All(m => bare.IsAvailable(m) && all.IsAvailable(m)), "an open modifier too");
		var openShelf = ShelfIds(bare);
		Assert.True(openShelf.All(ShelfIds(all).Contains), "the open shelf is part of every shelf");

		var evolving = bare.Starters.First(s => s.EvolutionName != "");
		string Lv3Card(AbilityPool p)
		{
			var run = RunState.NewRun(p);
			var slot = run.Owned.First(o => o.Special.Data.Id == evolving.Id).Slot;
			run.LevelUp(slot); // Lv 2: the next upgrade is the evolution (A5)
			var up = RewardDraft.Build(run, p, "kappa", 3, upgradeChoice: true).Offer.Cards.Single(c => c.Kind == SimCardKind.Upgrade);
			return up.Targets.Single(t => t.Slot == slot).Plain;
		}
		string evo = Lv3Card(bare);
		Assert.True(evo.Contains(evolving.EvolutionName), $"the evolution is offered with an empty save: {evo}");
		Assert.Equal(evo, Lv3Card(all), "and reads the same with every id saved (A5: never locked)");
	}

	[Test]
	public static void SameSeedSaveAndPool_GiveTheSameOffers()
	{
		var a = Pool().WithUnlocks(new[] { SealedSpecial, SealedMod });
		var b = Pool().WithUnlocks(new[] { SealedMod, SealedSpecial, SealedMod });
		for (uint seed = 1; seed <= 32; seed++)
		{
			Assert.Equal(Offers(a, "kappa", seed), Offers(a, "kappa", seed), $"seed {seed}: twice the same");
			Assert.Equal(Offers(a, "kappa", seed), Offers(b, "kappa", seed), $"seed {seed}: the save's order and repeats do not matter");
		}
		Assert.True(a.Specials.Select(s => s.Id).SequenceEqual(Pool().Specials.Select(s => s.Id))
			&& a.Modifiers.Select(m => m.Id).SequenceEqual(Pool().Modifiers.Select(m => m.Id)), "the pool order is kept");
		Assert.True(a.Specials.Single(s => s.Id == SealedSpecial).Locked, "the data's lock flag is untouched (the unlock order reads it)");
	}

	[Test]
	public static void UnlockOrderFromAnUnlockedPool_IsTheSame()
	{
		var bare = Pool();
		var order = Unlocks.FromData(bare, new[] { "undertow" }).Order;
		Assert.Equal(string.Join(",", order), string.Join(",", Unlocks.FromData(bare.WithUnlocks(order), new[] { "undertow" }).Order),
			"the award order never changes with what is unlocked (M1)");
	}

	// ---- YOK-153 leftovers from the YOK-86 review ----

	[Test]
	public static void ABlankDroppedId_SaysItIsBlank()
	{
		foreach (string? blank in new[] { "", " ", "\t", null })
		{
			string w = Unlocks.WhyDropped(blank);
			Assert.True(w.Contains("blank") && !w.Contains("twice"), $"'{blank}': {w}");
		}
		Assert.Equal("'x' is named twice in the data; kept once", Unlocks.WhyDropped("x"), "a repeat still says so");
		var warnings = new List<string>();
		GameFlow.UnlockOrder(Pool(), () => new[] { "", SealedMod }, warnings.Add);
		Assert.Equal(2, warnings.Count, "both dropped ids are logged");
		Assert.True(warnings[0].Contains("blank") && !warnings[0].Contains("''"), $"the blank one: {warnings[0]}");
		Assert.True(warnings[1].Contains($"'{SealedMod}' is named twice"), $"the repeat: {warnings[1]}");
	}

	[Test]
	public static void AnyCancelReadFailure_LeavesTheOrderWithoutCancels_A_NullFileAndUnauthorizedAccess()
	{
		var pool = Pool();
		string expected = string.Join(",", Unlocks.FromData(pool).Order);
		var thrown = new Exception[]
		{
			new UnauthorizedAccessException("access denied"),
			new NullReferenceException("null cancel"),
			new ArgumentException("odd"),
		};
		foreach (var e in thrown)
		{
			var warnings = new List<string>();
			var order = GameFlow.UnlockOrder(pool, () => throw e, warnings.Add);
			Assert.Equal(expected, string.Join(",", order.Order), $"{e.GetType().Name}: the specials and modifiers are still awarded");
			Assert.True(warnings.Count == 1 && warnings[0].Contains(e.GetType().Name), $"{e.GetType().Name}: one warning naming it");
		}
		Assert.Equal(expected, string.Join(",", GameFlow.UnlockOrder(pool, () => null, _ => { }).Order), "a null list is an empty one");

		// The real loader on a cancel file holding literal null.
		string dir = Path.Combine(Path.GetTempPath(), "yokai-null-cancel-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(dir);
		try
		{
			File.WriteAllText(Path.Combine(dir, "nothing.json"), "null");
			var warnings = new List<string>();
			var order = GameFlow.UnlockOrder(pool, () => CancelRuleLoader.LockedIds(dir), warnings.Add);
			Assert.Equal(expected, string.Join(",", order.Order), "a literal null cancel file: left out, the rest kept");
			Assert.Equal(1, warnings.Count, "and logged once");
		}
		finally { try { Directory.Delete(dir, recursive: true); } catch (IOException) { } }
	}
}
