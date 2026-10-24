using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// Test Breaker probes for YOK-95 (the merchant shop: R6, T5, T2, T4, M3, A2, A3, A6, A9, A11, R8). Expected values
/// come from rules.md and the designer rulings of 2026-10-11/12, never from what the code happens to do.
/// Save probes are in MerchantShopSaveProbes.cs (the only probe file besides SaveProbes that may name the save store).
/// </summary>
public static class MerchantShopProbes
{
	static string MapDir => ContentPaths.Data("map");
	static NodeSceneTable Table() => NodeSceneTable.Default(MapDir);
	static MapGraph DefaultMap() => FileMapSource.Default(MapDir).Load();
	static readonly string[] Walk = { "r1-apprentice", "r2-kitsune", "r3-oni" };
	const string Merchant4 = "r4-merchant";

	static void OnFixtures(Action body)
	{
		var saved = FightScene.Sources; var savedRun = FightScene.Run;
		FightScene.Sources = FightScene.FixtureSources; FightScene.Run = null;
		try { body(); } finally { FightScene.Sources = saved; FightScene.Run = savedRun; }
	}

	internal static SpecialData Clone(string id, string source, string rarity, string slot, bool locked = false)
	{
		var node = JsonNode.Parse(File.ReadAllText(Path.Combine(ContentPaths.FixturesDir, "rewards", "test-foxfire.json")))!.AsObject();
		node["id"] = id; node["source"] = source; node["rarity"] = rarity; node["locked"] = locked;
		node["input"]!["kata"]!["slot"] = slot; node["input"]!["kihon"]!["slot"] = slot;
		return SpecialLoader.Parse(node.ToJsonString(), id);
	}

	internal static ModifierData CloneMod(string from, string id, string source, string rarity, bool locked = false, string[]? requires = null)
	{
		var node = JsonNode.Parse(File.ReadAllText(Path.Combine(ContentPaths.FixturesDir, "rewards", from + ".json")))!.AsObject();
		node["id"] = id; node["source"] = source; node["rarity"] = rarity; node["locked"] = locked;
		if (requires is not null)
		{
			var arr = new JsonArray(); foreach (var r in requires) arr.Add(r);
			node["applies_to"]!["requires"] = arr;
		}
		return ModifierLoader.Parse(node.ToJsonString(), id);
	}

	internal static AbilityPool Pool()
	{
		var fx = FightScene.LoadAbilityPool();
		var extra = new[]
		{
			Clone("test-kappa-spin", "kappa", "common", "D"),
			Clone("test-kappa-grab", "kappa", "common", "C"),
			Clone("test-oni-charge", "oni", "common", "D"),
			Clone("test-kitsune-mirage", "kitsune", "common", "D"),
		};
		var mods = new[]
		{
			CloneMod("test-fox-patience", "test-kappa-patience", "kappa", "common"),
			CloneMod("test-will-o-wisp", "test-kappa-wisp", "kappa", "common"),
			CloneMod("test-fox-patience", "test-kappa-noway", "kappa", "common", requires: new[] { "projectile", "no-such-tag" }),
			CloneMod("test-fox-patience", "test-oni-patience", "oni", "common"),
		};
		return new AbilityPool(fx.Specials.Concat(extra), fx.Modifiers.Concat(mods));
	}

	static SpecialData S(AbilityPool p, string id) => p.Special(id) ?? throw new InvalidOperationException(id);
	static ModifierData M(AbilityPool p, string id) => p.Modifier(id) ?? throw new InvalidOperationException(id);

	internal static RunSession At(AbilityPool pool, int coins, string? enter, string[] walk, params (SpecialSlot Slot, string Id, int Level)[] slots)
	{
		var graph = DefaultMap();
		var run = RunState.Restore(600, slots.Select(x => (x.Slot, new OwnedSpecial(S(pool, x.Id), x.Level, null))));
		var order = new int[5]; int rank = 0;
		foreach (var x in slots.OrderBy(x => x.Slot)) if (x.Level > 1) order[(int)x.Slot] = ++rank;
		run.RestoreLevelledOrder(order[1], order[2], order[3], order[4]);
		run.RestoreCoins(coins);
		run.SetMap(RunMap.Restore(graph, new RunMapSnapshot(graph.Id, walk, walk[^1])));
		var s = new RunSession(run, pool, Table(), 7);
		if (enter is not null) Assert.True(s.Enter(enter).Ok, "enter " + enter);
		return s;
	}

	static readonly (SpecialSlot, string, int)[] Starters = { (SpecialSlot.A, "test-spirit-wave", 1), (SpecialSlot.B, "test-rising-talisman", 1) };
	static (SpecialSlot, string, int)[] With(params (SpecialSlot, string, int)[] more) => Starters.Concat(more).ToArray();

	internal static MerchantShop Shop(RunSession s)
	{
		Assert.True(s.Merchant!.Choose(MerchantChoice.Shop).Ok, "shop chosen");
		return s.Merchant.Shop;
	}

	// ---- 1. a bought modifier drafted again (critic item 1) ----

	[Test]
	public static void KnownOpen_BoughtModifier_CanBeDraftedAgain_FlagsAreIndependentPerSlot() => OnFixtures(() =>
	{
		// R6/A2/E20: the rules say one modifier per special and say nothing about two specials wearing the same modifier.
		var pool = Pool();
		var s = At(pool, 100, Merchant4, Walk, With((SpecialSlot.C, "test-foxfire", 1)));
		var shop = Shop(s);
		Assert.True(shop.BuyModifier("test-kappa-patience", SpecialSlot.C).Ok, "bought onto C");
		Assert.True(s.Run[SpecialSlot.C] is { ModifierBorrowed: true }, "borrowed on C");
		Assert.True(!shop.ModifierShelf.Any(m => m.Id == "test-kappa-patience"), "off the shelf once worn");
		// A Kappa draft after the shop.
		RewardDraft draft = null!; int card = -1;
		for (uint seed = 1; seed < 40 && card < 0; seed++)
		{
			draft = RewardDraft.Build(s.Run, pool, "kappa", seed);
			for (int i = 0; i < draft.Offer.Cards.Count; i++) if (draft.Offer.Cards[i].AbilityId == "test-kappa-patience") card = i;
		}
		Assert.True(card >= 0, "KNOWN-OPEN: the draft offers the modifier Ryo already wears (no 'already worn' filter in RewardDraft)");
		var targets = draft.Offer.Cards[card].Targets.Select(t => t.Slot).ToArray();
		Assert.True(!targets.Contains(SpecialSlot.C), "A2: not offered onto the special that already carries a modifier");
		Assert.True(draft.Pick(card, targets[0]).Ok, "drafted onto another special");
		Assert.True(s.Run[targets[0]] is { ModifierBorrowed: false } o && o.Modifier!.Id == "test-kappa-patience", "the drafted copy is owned ink");
		Assert.True(s.Run[SpecialSlot.C] is { ModifierBorrowed: true }, "the bought copy stays borrowed");
		Assert.True(s.Run.CarryOverCandidates.First(c => c.Slot == SpecialSlot.C).Special.Modifier is null, "M3: only the bought one is dropped from carry-over");
	});

	// ---- 2. targets empty ----

	[Test]
	public static void ModifierWithNoTarget_IsAlwaysRefused_NothingSpent() => OnFixtures(() =>
	{
		var pool = Pool();
		var s = At(pool, 500, Merchant4, Walk, With((SpecialSlot.C, "test-foxfire", 1)));
		var shop = Shop(s);
		Assert.True(shop.ModifierShelf.Any(m => m.Id == "test-kappa-noway"), "listed on the shelf");
		Assert.True(shop.ModifierTargets("test-kappa-noway").Count == 0, "no special can take it");
		ulong h = s.StateHash();
		foreach (var slot in new[] { SpecialSlot.None, SpecialSlot.A, SpecialSlot.B, SpecialSlot.C, SpecialSlot.D, (SpecialSlot)9 })
			Assert.True(!shop.BuyModifier("test-kappa-noway", slot).Ok && !shop.CanBuyModifier("test-kappa-noway", slot), $"slot {slot}: refused (A2)");
		Assert.True(s.Run.Coins == 500 && s.StateHash() == h, "nothing spent, nothing changed");
	});

	// ---- 3. hashes ----

	static RunState Four(bool borrowedSpecial, bool borrowedMod, SpecialSlot modSlot = SpecialSlot.C)
	{
		var pool = Pool();
		var owned = new List<(SpecialSlot, OwnedSpecial)>
		{
			(SpecialSlot.A, new OwnedSpecial(S(pool, "test-spirit-wave"), 1, null)),
			(SpecialSlot.C, new OwnedSpecial(S(pool, "test-foxfire"), 1, modSlot == SpecialSlot.C ? M(pool, "test-kappa-patience") : null, false, borrowedMod && modSlot == SpecialSlot.C)),
			(SpecialSlot.D, new OwnedSpecial(S(pool, "test-kappa-spin"), 1, null, borrowedSpecial)),
		};
		return RunState.Restore(700, owned);
	}

	[Test]
	public static void Hash_FourBorrowedStatesAreDistinct_AndSlotsAreDistinct() => OnFixtures(() =>
	{
		var hs = new[] { Four(false, false).StateHash(), Four(true, false).StateHash(), Four(false, true).StateHash(), Four(true, true).StateHash() };
		Assert.Equal(4, hs.Distinct().Count(), "neither / borrowed special / borrowed modifier / both");
		var pool = Pool();
		RunState Mk(SpecialSlot borrowedAt) => RunState.Restore(700, new[]
		{
			(SpecialSlot.C, new OwnedSpecial(S(pool, "test-kappa-grab"), 1, null, borrowedAt == SpecialSlot.C)),
			(SpecialSlot.D, new OwnedSpecial(S(pool, "test-kappa-spin"), 1, null, borrowedAt == SpecialSlot.D)),
		});
		Assert.True(Mk(SpecialSlot.C).StateHash() != Mk(SpecialSlot.D).StateHash(), "which slot is borrowed matters");
	});

	static ulong OldFormula(RunState r)
	{
		ulong h = Fnv.Mix(Fnv.Offset, r.Health);
		foreach (var slot in new[] { SpecialSlot.A, SpecialSlot.B, SpecialSlot.C, SpecialSlot.D })
		{
			h = Fnv.MixString(h, r[slot]?.Data.Id ?? "");
			h = Fnv.Mix(h, r[slot]?.Level ?? 0);
			h = Fnv.MixString(h, r[slot]?.Modifier?.Id ?? "");
		}
		bool any = false;
		foreach (var slot in new[] { SpecialSlot.A, SpecialSlot.B, SpecialSlot.C, SpecialSlot.D }) any |= r.LevelledOrder(slot) > 0;
		if (any) foreach (var slot in new[] { SpecialSlot.A, SpecialSlot.B, SpecialSlot.C, SpecialSlot.D }) h = Fnv.Mix(h, r.LevelledOrder(slot));
		if (r.Map is not null) { ulong m = r.Map.StateHash(); h = Fnv.Mix(h, unchecked((int)m)); h = Fnv.Mix(h, unchecked((int)(m >> 32))); }
		if (r.Coins != 0) h = Fnv.Mix(Fnv.MixString(h, "coins"), r.Coins);
		return h;
	}

	[Test]
	public static void Hash_RunThatNeverShops_KeepsTheBaseFormula_DraftAttachedModifierToo() => OnFixtures(() =>
	{
		var pool = Pool();
		var r = RunState.NewRun(pool);
		Assert.True(r.StateHash() == OldFormula(r), "new run");
		r.Equip(S(pool, "test-foxfire"));
		r.Attach(SpecialSlot.C, M(pool, "test-kappa-patience")); // a draft-attached modifier
		r.LevelUp(SpecialSlot.C);
		r.AddCoins(33);
		r.SetHealth(640);
		Assert.True(r.StateHash() == OldFormula(r), "draft-attached modifier, level-up, coins: the pre-branch hash");
		Assert.True(r[SpecialSlot.C] is { Borrowed: false, ModifierBorrowed: false }, "not borrowed");
	});

	// ---- economy ----

	[Test]
	public static void Economy_ReleaseRebuy_LosesLevels_ExactCoins() => OnFixtures(() =>
	{
		var pool = Pool();
		var s = At(pool, 200, Merchant4, Walk, With());
		var shop = Shop(s);
		Assert.True(shop.Buy("test-kappa-spin").Ok, "buy");
		s.Run.Equip(S(pool, "test-kappa-spin")); // a later draft levels it (A3)
		Assert.True(s.Run[SpecialSlot.D]!.Level == 2 && s.Run.LevelledOrder(SpecialSlot.D) > 0, "levelled by a draft");
		Assert.True(shop.Release(SpecialSlot.D).Ok, "release");
		Assert.True(s.Run[SpecialSlot.D] is null && s.Run.LevelledOrder(SpecialSlot.D) == 0, "slot empty, level-up order cleared (T2)");
		Assert.True(shop.Buy("test-kappa-spin").Ok, "rebuy");
		Assert.True(s.Run[SpecialSlot.D] is { Level: 1, Borrowed: true }, "A6: back at Lv 1");
		Assert.Equal(200 - 50 - 40 - 50, s.Run.Coins, "R6: honest price");
		Assert.True(s.Run.LevelledOrder(SpecialSlot.D) == 0, "no level-up credit survives");
	});

	[Test]
	public static void Economy_ExactBoundaries_50_49_0_Max() => OnFixtures(() =>
	{
		var pool = Pool();
		foreach (var (coins, ok) in new[] { (50, true), (49, false), (0, false), (51, true) })
		{
			var s = At(pool, coins, Merchant4, Walk, With());
			var shop = Shop(s); ulong h = s.StateHash();
			Assert.Equal(ok, shop.Buy("test-kappa-spin").Ok, $"buy with {coins}");
			Assert.Equal(ok ? coins - 50 : coins, s.Run.Coins, $"coins after {coins}");
			if (!ok) Assert.True(s.StateHash() == h, "refusal changed nothing");
			var t = At(pool, coins, Merchant4, Walk, With());
			var sh = Shop(t);
			Assert.Equal(ok, sh.BuyModifier("test-kappa-patience", SpecialSlot.A).Ok, $"buy modifier with {coins}");
		}
		var r = At(pool, 40, Merchant4, Walk, With((SpecialSlot.C, "test-foxfire", 1)));
		Assert.True(Shop(r).Release(SpecialSlot.C).Ok && r.Run.Coins == 0, "release with exactly 40");
		var r2 = At(pool, 39, Merchant4, Walk, With((SpecialSlot.C, "test-foxfire", 1)));
		var shop2 = Shop(r2);
		ulong h2 = r2.StateHash();
		Assert.True(!shop2.Release(SpecialSlot.C).Ok && r2.Run.Coins == 39 && r2.StateHash() == h2, "39: refused, unchanged");
		var big = At(pool, int.MaxValue, Merchant4, Walk, With());
		Assert.True(Shop(big).Buy("test-kappa-spin").Ok && big.Run.Coins == int.MaxValue - 50, "MaxValue coins");
		big.Run.AddCoins(int.MaxValue);
		Assert.True(big.Run.Coins == int.MaxValue, "purse caps, never wraps negative");
	});

	[Test]
	public static void Economy_Fuzz_NoOperationGainsAnything_RefusalsChangeNothing() => OnFixtures(() =>
	{
		var pool = Pool();
		var specials = pool.Specials.Select(x => x.Id).Append("nope").ToArray();
		var mods = pool.Modifiers.Select(x => x.Id).Append("nope").ToArray();
		for (uint seed = 1; seed <= 12; seed++)
		{
			uint x = seed * 2654435761u + 1;
			int Next(int n) { x ^= x << 13; x ^= x >> 17; x ^= x << 5; return (int)(x % (uint)n); }
			var s = At(pool, 300, Merchant4, Walk, With());
			var shop = Shop(s);
			for (int i = 0; i < 60; i++)
			{
				int coins = s.Run.Coins, count = s.Run.SpecialCount, levels = s.Run.Owned.Sum(o => o.Special.Level);
				int mcount = s.Run.Owned.Count(o => o.Special.Modifier is not null);
				ulong h = s.StateHash();
				int op = Next(5); MapResult r; string what;
				if (op == 0) { var id = specials[Next(specials.Length)]; r = shop.Buy(id); what = "buy " + id; }
				else if (op == 1) { var id = mods[Next(mods.Length)]; var sl = (SpecialSlot)Next(5); r = shop.BuyModifier(id, sl); what = $"mod {id} {sl}"; }
				else if (op == 2) { var sl = (SpecialSlot)Next(5); r = shop.Release(sl); what = "release " + sl; }
				else if (op == 3) { var q = pool.Specials.Where(z => !z.Starter).ElementAt(Next(pool.Specials.Count(z => !z.Starter))); if (s.Run[q.Slot]?.Data.Id == q.Id && !s.Run.CanLevelUp(q.Slot)) continue; s.Run.Equip(q); continue; }
				else { s.Run.AddCoins(Next(3) * 40); continue; }
				Assert.True(s.Run.Coins >= 0 && s.Run.Coins <= coins, what + ": coins never rise or go negative");
				Assert.True(s.Run.SpecialCount >= 1, what + ": T5");
				if (!r.Ok) Assert.True(s.StateHash() == h && s.Run.Coins == coins, what + ": refusal changed state");
				else if (op == 0) Assert.True(coins - s.Run.Coins == 50 && s.Run.SpecialCount == count + 1 && s.Run.Owned.Sum(o => o.Special.Level) == levels + 1, what);
				else if (op == 1) Assert.True(coins - s.Run.Coins == 50 && s.Run.Owned.Count(o => o.Special.Modifier is not null) == mcount + 1 && s.Run.SpecialCount == count, what);
				else Assert.True(coins - s.Run.Coins == 40 && s.Run.SpecialCount == count - 1, what);
				Assert.True(s.Run.Owned.All(o => !o.Special.ModifierBorrowed || o.Special.Modifier is not null), "flag only with a modifier");
			}
		}
	});

	// ---- T5 ----

	[Test]
	public static void T5_LastSpecial_EveryRoute() => OnFixtures(() =>
	{
		var pool = Pool();
		// a starter alone
		var a = At(pool, 500, Merchant4, Walk, new[] { (SpecialSlot.A, "test-spirit-wave", 1) });
		var sa = Shop(a); ulong h = a.StateHash();
		Assert.True(!sa.Release(SpecialSlot.A).Ok && a.StateHash() == h, "starter alone");
		// an owned non-starter alone
		var b = At(pool, 500, Merchant4, Walk, new[] { (SpecialSlot.C, "test-foxfire", 2) });
		var sb = Shop(b); h = b.StateHash();
		var rb = sb.Release(SpecialSlot.C);
		Assert.True(!rb.Ok && rb.Error!.Contains("T5") && b.StateHash() == h, "owned non-starter alone: T5");
		// borrowed alone, reached by releasing down
		var c = At(pool, 500, Merchant4, Walk, With());
		var sh = Shop(c);
		Assert.True(sh.Buy("test-kappa-spin").Ok && sh.Buy("test-kappa-grab").Ok, "two buys");
		Assert.True(sh.Release(SpecialSlot.C).Ok, "release one borrowed");
		Assert.True(!sh.Release(SpecialSlot.A).Ok && !sh.Release(SpecialSlot.B).Ok, "starters stay");
		// replace starters by drafts so only borrowed remain is not reachable; do it through Restore instead
		var d = At(pool, 500, Merchant4, Walk, new[] { (SpecialSlot.C, "test-kappa-grab", 1), (SpecialSlot.D, "test-kappa-spin", 1) });
		var dr = Shop(d);
		Assert.True(dr.Release(SpecialSlot.C).Ok, "two left: one may go");
		h = d.StateHash();
		Assert.True(!dr.Release(SpecialSlot.D).Ok && d.StateHash() == h && d.Run.Coins == 460, "the last: refused, nothing spent");
		// a draft that replaces the last special keeps exactly one
		d.Run.Equip(S(pool, "test-oni-charge"));
		Assert.True(d.Run.SpecialCount == 1 && !dr.Release(SpecialSlot.D).Ok && CopyRule.Target(d.Run) is not null, "after a replacing draft: still T5, copy target defined");
	});

	// ---- copy rule ----

	[Test]
	public static void Copy_ReleaseBeforeLock_MovesTargetByT2T3_BorrowedCanBeTarget() => OnFixtures(() =>
	{
		var pool = Pool();
		var s = At(pool, 300, Merchant4, Walk, With());
		s.Run.Equip(S(pool, "test-spirit-wave")); // A -> Lv 2 (earlier)
		var shop = Shop(s);
		Assert.True(shop.Buy("test-kappa-spin").Ok, "buy D");
		s.Run.Equip(S(pool, "test-kappa-spin")); // D Lv 2, later
		Assert.True(CopyRule.Target(s.Run) is { Slot: SpecialSlot.D, Level: 2 }, "T3: tie at Lv 2, D levelled last; a borrowed special can be the target");
		Assert.True(shop.Release(SpecialSlot.D).Ok, "release the target");
		Assert.True(CopyRule.Target(s.Run) is { Slot: SpecialSlot.A, Level: 2 }, "T2: the released move no longer counts");
		Assert.True(shop.Buy("test-kappa-spin").Ok, "rebuy");
		Assert.True(CopyRule.Target(s.Run) is { Slot: SpecialSlot.A }, "Lv 1 rebuy does not retake the target");
	});

	[Test]
	public static void Copy_T4_NeverCarriesABoughtModifier() => OnFixtures(() =>
	{
		var pool = Pool();
		var s = At(pool, 300, Merchant4, Walk, With((SpecialSlot.C, "test-foxfire", 2)));
		var before = CopyRule.Target(s.Run)!;
		var plainFrames = RewardDraft.FrameSummary(before.Build());
		Assert.True(Shop(s).BuyModifier("test-kappa-wisp", SpecialSlot.C).Ok, "bought");
		var after = CopyRule.Target(s.Run)!;
		Assert.True(after.Slot == before.Slot && after.Level == before.Level, "same target");
		Assert.Equal(plainFrames, RewardDraft.FrameSummary(after.Build()), "T4: the copy has no modifier");
		Assert.Equal(RewardDraft.FrameSummary(after.Data.Build(2, false, null, null)), RewardDraft.FrameSummary(after.Build()), "built from data at its level, bare");
	});

	[Test]
	public static void Copy_AfterLock_ShopCannotChangeLockedCopy_OnAMapWithAMerchantAfterTheBoss() => OnFixtures(() =>
	{
		// T1: the lock is taken on entering the boss and never again. The shipped map has no merchant after the boss (R1);
		// the loader does not enforce that, so this builds one to check the lock holds whatever the shop does.
		var root = JsonNode.Parse(File.ReadAllText(Path.Combine(MapDir, "default-map.json")))!.AsObject();
		root["nodes"]!.AsArray().Add(new JsonObject { ["id"] = "z-merchant", ["row"] = 10, ["type"] = "merchant", ["label"] = "M" });
		root["routes"]!.AsArray().Add(new JsonObject { ["from"] = "boss-tanuki", ["to"] = "z-merchant" });
		var g = MapLoader.Parse(root.ToJsonString());
		var pool = Pool();
		// YOK-178 (designer 2026-10-21): an elder binds its yokai, so the walk ends at the Elder Oni to leave the Kappa unbound and on the shelf.
		var walk = new[] { "r1-apprentice", "r2-kitsune", "r3-oni", "r4-merchant", "r5-elder-oni", "r6-kitsune", "r7-merchant", "r8-elder-oni" };
		var run = RunState.Restore(600, With((SpecialSlot.C, "test-foxfire", 2)).Select(x => (x.Item1, new OwnedSpecial(S(pool, x.Item2), x.Item3, null))));
		run.RestoreLevelledOrder(0, 0, 1, 0); run.RestoreCoins(500);
		run.SetMap(RunMap.Restore(g, new RunMapSnapshot(g.Id, walk, walk[^1])));
		var s = new RunSession(run, pool, Table(), 7);
		Assert.True(s.Enter("boss-tanuki").Ok && s.LockedCopy is { Slot: SpecialSlot.C, Level: 2 }, "locked on entering the boss");
		s.WinFight(500);
		Assert.True(s.CompleteNode().Ok && s.Enter("z-merchant").Ok, "on to the merchant");
		var shop = Shop(s);
		Assert.True(shop.Release(SpecialSlot.C).Ok && shop.Buy("test-kappa-spin").Ok, "shopping after the lock");
		Assert.True(s.LockedCopy is { Slot: SpecialSlot.C, Level: 2 } && s.LockedCopy.Data.Id == "test-foxfire", "the locked copy is untouched");
		Assert.True(CopyRule.Target(s.Run)!.Data.Id != "test-foxfire", "while the live target moved on: the lock is not recomputed");
	});

	[Test]
	public static void Copy_LockedBeforeMerchantRow_NothingInTheShopChangesIt() => OnFixtures(() =>
	{
		// T1: the lock is taken on entering the boss node only; a session restored with a lock keeps it whatever is shopped.
		var pool = Pool();
		var s = At(pool, 300, Merchant4, Walk, With((SpecialSlot.C, "test-foxfire", 2)));
		Assert.True(!s.CopyLocked && s.LockedCopy is null, "before the boss nothing is locked");
		var shop = Shop(s);
		Assert.True(shop.Release(SpecialSlot.C).Ok && shop.Buy("test-kappa-spin").Ok, "shopping");
		Assert.True(!s.CopyLocked && s.LockedCopy is null, "the shop never locks (T1)");
	});

	// ---- R6 exclusivity ----

	[Test]
	public static void R6_HealThenEveryShopActionRefused_ShopThenHealRefused() => OnFixtures(() =>
	{
		var pool = Pool();
		var s = At(pool, 500, Merchant4, Walk, With((SpecialSlot.C, "test-foxfire", 1)));
		Assert.True(s.Merchant!.TakeHeal().Ok, "heal");
		var shop = s.Merchant.Shop; ulong h = s.StateHash();
		Assert.True(!shop.Buy("test-kappa-spin").Ok && !shop.BuyModifier("test-kappa-patience", SpecialSlot.A).Ok && !shop.Release(SpecialSlot.C).Ok, "all refused after the heal");
		Assert.True(!s.Merchant.Choose(MerchantChoice.Shop).Ok && s.StateHash() == h && s.Run.Coins == 500, "and the shop can't be chosen either");
		var t = At(pool, 500, Merchant4, Walk, With());
		Shop(t); int hp = t.Run.Health; ulong h2 = t.StateHash();
		Assert.True(!t.Merchant!.TakeHeal().Ok && t.Run.Health == hp && t.StateHash() == h2, "shop then heal refused, health unchanged");
	});

	[Test]
	public static void R6_StaleShop_AfterLeaving_AfterRunEnds_AtNextMerchant() => OnFixtures(() =>
	{
		var pool = Pool();
		// after leaving
		var s = At(pool, 500, Merchant4, Walk, With());
		var old = Shop(s);
		s.WinFight(600); Assert.True(s.CompleteNode().Ok, "leave");
		ulong h = s.StateHash();
		Assert.True(!old.Buy("test-kappa-spin").Ok && !old.BuyModifier("test-kappa-patience", SpecialSlot.A).Ok && !old.Release(SpecialSlot.A).Ok && s.StateHash() == h, "stale after leaving");
		// next merchant: walk on to r7
		foreach (var n in new[] { "r5-elder-oni", "r6-kitsune" })
		{
			Assert.True(s.Enter(n).Ok, n);
			var d = s.WinFight(600);
			if (d is not null) { var c = d.Offer.Cards[0]; Assert.True(d.Pick(0, c.Targets.Count > 0 ? c.Targets[0].Slot : null).Ok, "pick"); }
			Assert.True(s.CompleteNode().Ok, "complete " + n);
		}
		Assert.True(s.Enter("r7-merchant").Ok, "second merchant");
		var fresh = Shop(s);
		int coins = s.Run.Coins; ulong h3 = s.StateHash();
		Assert.True(!old.Buy("test-kappa-spin").Ok && !old.Release(SpecialSlot.A).Ok && s.StateHash() == h3, "first stop's shop does nothing at the second stop");
		Assert.True(fresh.Open && coins == s.Run.Coins, "second stop's own shop is open");
		// run ends
		Assert.True(s.LoseFight().Ok, "run lost");
		Assert.True(!fresh.Buy("test-kappa-spin").Ok && !fresh.BuyModifier("test-kappa-patience", SpecialSlot.A).Ok && !fresh.Release(SpecialSlot.A).Ok, "no purchases after the run ends");
	});

	[Test]
	public static void R6_RestoredChoice_ShopOpensOnlyForShop() => OnFixtures(() =>
	{
		var pool = Pool();
		RunSession Restored(MerchantChoice c)
		{
			var g = DefaultMap();
			var run = RunState.Restore(600, With().Select(x => (x.Item1, new OwnedSpecial(S(pool, x.Item2), x.Item3, null))));
			run.RestoreCoins(200);
			run.SetMap(RunMap.Restore(g, new RunMapSnapshot(g.Id, Walk, Merchant4)));
			return new RunSession(run, pool, Table(), 7, c);
		}
		Assert.True(Restored(MerchantChoice.Shop).Merchant!.Shop.Buy("test-kappa-spin").Ok, "restored Shop: open");
		Assert.True(!Restored(MerchantChoice.Heal).Merchant!.Shop.Buy("test-kappa-spin").Ok, "restored Heal: closed");
		Assert.True(!Restored(MerchantChoice.None).Merchant!.Shop.Buy("test-kappa-spin").Ok, "not chosen: closed");
	});

	// ---- M3 ----

	[Test]
	public static void M3_CarryOver_NeverBorrowed_EvenAfterLevelling() => OnFixtures(() =>
	{
		var pool = Pool();
		var s = At(pool, 500, Merchant4, Walk, With());
		var shop = Shop(s);
		Assert.True(shop.Buy("test-kappa-spin").Ok && shop.BuyModifier("test-kappa-patience", SpecialSlot.A).Ok, "buys");
		s.Run.Equip(S(pool, "test-kappa-spin")); s.Run.Equip(S(pool, "test-kappa-spin"));
		Assert.True(s.Run[SpecialSlot.D] is { Level: 3, Borrowed: true }, "borrowed special levelled by drafts stays borrowed");
		var cands = s.Run.CarryOverCandidates.ToList();
		Assert.True(cands.All(c => !c.Special.Borrowed && !c.Special.ModifierBorrowed) && !cands.Any(c => c.Slot == SpecialSlot.D), "no borrowed special or modifier");
		Assert.True(cands.First(c => c.Slot == SpecialSlot.A).Special.Modifier is null, "A offered bare");
		Assert.True(s.Run[SpecialSlot.A]!.Modifier is not null, "listing does not strip the live run");
		s.Run.Attach(SpecialSlot.B, M(pool, "test-oni-patience")); // owned modifier on a starter
		Assert.True(s.Run.CarryOverCandidates.First(c => c.Slot == SpecialSlot.B).Special.Modifier is not null, "an owned modifier stays");
		// replaced by an owned special in the same slot: no longer borrowed
		s.Run.Equip(S(pool, "test-oni-charge"));
		Assert.True(s.Run.CarryOverCandidates.Any(c => c.Slot == SpecialSlot.D), "a drafted replacement is owned ink");
	});

	// ---- determinism ----

	[Test]
	public static void Determinism_SameInputsSameHashes_DifferentChoiceDiffers() => OnFixtures(() =>
	{
		List<ulong> Run(string spec)
		{
			var s = At(Pool(), 300, Merchant4, Walk, With());
			var shop = Shop(s); var hs = new List<ulong> { s.StateHash() };
			if (spec == "spin") shop.Buy("test-kappa-spin"); else shop.Buy("test-kappa-grab");
			hs.Add(s.StateHash());
			shop.BuyModifier("test-kappa-patience", SpecialSlot.A); hs.Add(s.StateHash());
			shop.Release(SpecialSlot.D); hs.Add(s.StateHash());
			return hs;
		}
		Assert.True(Run("spin").SequenceEqual(Run("spin")), "same inputs");
		Assert.True(Run("spin").Last() != Run("grab").Last(), "different choice, different hash");
	});

	// ---- BoundYokai ----

	static NodeSceneTable RealKitsTable()
	{
		var o = JsonNode.Parse(File.ReadAllText(Path.Combine(MapDir, "node-scenes.json")))!.AsObject();
		o["opponents"]!["oni"] = new JsonObject { ["fighter"] = "oni" };
		o["opponents"]!["kappa"] = new JsonObject { ["fighter"] = "kappa" };
		return NodeSceneTable.Parse(o.ToJsonString());
	}

	[Test]
	public static void BoundYokai_RealKits_NoOverOrUnderBinding() => OnFixtures(() =>
	{
		var pool = Pool(); var g = DefaultMap();
		RunSession Sess(string[] walk, string? cur = null)
		{
			var run = RunState.NewRun(pool);
			run.SetMap(RunMap.Restore(g, new RunMapSnapshot(g.Id, walk, cur ?? walk[^1])));
			return new RunSession(run, pool, RealKitsTable(), 1);
		}
		var b = Sess(new[] { "r1-apprentice", "r2-oni", "r3-kappa", "r4-merchant" }).BoundYokai();
		Assert.Equal("kitsune,apprentice,oni,kappa", string.Join(",", b), "stand-in apprentice binds kitsune+apprentice; real oni and kappa bind only themselves; the merchant binds nothing");
		var inFight = Sess(new[] { "r1-apprentice" }, "r2-kappa");
		Assert.True(!inFight.BoundYokai().Contains("kappa"), "a node entered but not completed binds nothing yet");
	});

	[Test]
	public static void KnownOpen_ElderOni_DoesNotBindOni_RealKitsOnTheRealMap() => OnFixtures(() =>
	{
		// Designer-open (BoundYokai doc): an elder's yokai id 'elder-oni' is not mapped to 'oni'. With a real Oni kit the
		// path r2-kitsune, r5-elder-oni leaves the Oni unbound, so the Oni's commons could be sold after beating Elder Oni.
		var pool = Pool(); var g = DefaultMap();
		var run = RunState.NewRun(pool);
		run.SetMap(RunMap.Restore(g, new RunMapSnapshot(g.Id, new[] { "r1-apprentice", "r2-kitsune", "r3-oni", "r4-merchant", "r5-elder-oni" }, "r5-elder-oni")));
		var s = new RunSession(run, pool, Table(), 1);
		Assert.True(s.BoundYokai().Contains("oni"), "oni was bound already by r3-oni (stand-in), so nothing sold here today");
	});

	[Test]
	public static void RealDefaultMap_AfterApprenticeAlone_ShopSellsOnlyUnboundCommonModifiers() =>
		// Was KnownOpen_..._ShopSellsNothing: the content gap closed with YOK-103 (the Oni's and Kappa's modifiers are in
		// data/modifiers/). Specials: still only the Kitsune's kit, bound by the apprentice stand-in, so none are sold.
		WithRealData(() =>
		{
			var pool = FightScene.LoadAbilityPool(); var g = DefaultMap();
			var run = RunState.NewRun(pool);
			run.SetMap(RunMap.Restore(g, new RunMapSnapshot(g.Id, new[] { "r1-apprentice" }, "r1-apprentice")));
			var s = new RunSession(run, pool, Table(), 1);
			var bound = s.BoundYokai();
			Assert.True(bound.Contains("kitsune") && bound.Contains("apprentice"), "bound = {kitsune, apprentice}: " + string.Join(",", bound));
			var sell = MerchantShop.ShelfFor(run, pool, bound).Select(x => x.Id).ToArray();
			var sellMods = MerchantShop.ModifierShelfFor(run, pool, bound).Select(x => x.Id).ToArray();
			Assert.True(sell.Length == 0, "no unbound specials in data yet: " + string.Join(",", sell));
			var want = pool.Modifiers.Where(m => m.Rarity == "common" && !m.Locked && !bound.Contains(m.Source)).Select(m => m.Id).ToArray();
			Assert.True(want.Length > 0 && want.All(id => !pool.Modifiers.Any(m => m.Id == id && m.Source == "kitsune")), "the Oni's and Kappa's unlocked commons: " + string.Join(",", want));
			Assert.Equal(string.Join(",", want), string.Join(",", sellMods), "R6: common, unlocked modifiers of unbound yokai, pool order");
		});

	static void WithRealData(Action body)
	{
		var saved = FightScene.Sources; var savedRun = FightScene.Run;
		FightScene.Run = null;
		try { body(); } finally { FightScene.Sources = saved; FightScene.Run = savedRun; }
	}
}
