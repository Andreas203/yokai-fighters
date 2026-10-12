using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using YokaiFighters.Fight;
using YokaiFighters.SaveGame;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// Test Breaker probes for YOK-95's save half (R6 borrowed ink, M5, version 3). In memory only: no file, no SaveStore
/// instance, never the player's save. Named *SaveProbes.cs so SaveProbes' isolation scan treats it as a save file,
/// which still must not name the player's path (see the explicit list there).
/// </summary>
public static class MerchantShopSaveProbes
{
	static void OnFixtures(Action body)
	{
		var saved = FightScene.Sources; var savedRun = FightScene.Run;
		FightScene.Sources = FightScene.FixtureSources; FightScene.Run = null;
		try { body(); } finally { FightScene.Sources = saved; FightScene.Run = savedRun; }
	}

	static string MapDir => ContentPaths.Data("map");
	static MapGraph Map() => FileMapSource.Default(MapDir).Load();
	static NodeSceneTable Table() => NodeSceneTable.Default(MapDir);

	static RunSession RoundTrip(RunSession s, AbilityPool pool)
	{
		var run = RunSave.Capture(s);
		var load = SaveStore.Parse(SaveStore.Serialize(SaveData.Empty.WithRun(run)));
		Assert.Equal(SaveLoadStatus.Loaded, load.Status, "current version loads");
		return load.Data.Run!.Restore(pool, Map(), Table());
	}

	/// <summary>Plays the default map on the first path, shopping at both merchants, saving and restoring at every node boundary.</summary>
	[Test]
	public static void FullRun_WithPurchasesAtBothMerchants_RoundTripsAtEveryBoundary_IncludingTheLockedCopy() => OnFixtures(() =>
	{
		var pool = MerchantShopProbes.Pool();
		var s = RunSession.Start(pool, Map(), Table(), 21);
		s.Run.AddCoins(400);
		int checks = 0;
		void Check(string where)
		{
			var back = RoundTrip(s, pool);
			Assert.True(back.StateHash() == s.StateHash(), where + ": StateHash equal after the round trip");
			Assert.Equal(s.CopyLocked, back.CopyLocked, where + ": lock flag");
			Assert.Equal(s.LockedCopy?.Data.Id, back.LockedCopy?.Data.Id, where + ": locked copy");
			Assert.Equal(s.LockedCopy?.Level ?? 0, back.LockedCopy?.Level ?? 0, where + ": locked level");
			foreach (var (slot, o) in s.Run.Owned)
				Assert.True(back.Run[slot] is { } b && b.Borrowed == o.Borrowed && b.ModifierBorrowed == o.ModifierBorrowed && b.Level == o.Level && b.Modifier?.Id == o.Modifier?.Id, where + ": slot " + slot);
			checks++;
		}
		string[] path = { "r1-apprentice", "r2-kitsune", "r3-oni", "r4-merchant", "r5-elder-oni", "r6-kitsune", "r7-merchant", "r8-elder-kappa", "boss-tanuki" };
		foreach (var id in path)
		{
			Assert.True(s.Enter(id).Ok, "enter " + id);
			Check("entered " + id);
			if (id == "r4-merchant")
			{
				var shop = MerchantShopProbes.Shop(s);
				if (!shop.CanBuy("test-kappa-spin")) Assert.True(shop.Release(SpecialSlot.D).Ok, "free slot D first (a draft filled it)");
				Assert.True(shop.Buy("test-kappa-spin").Ok && shop.BuyModifier("test-kappa-patience", SpecialSlot.A).Ok, "buy at r4");
			}
			if (id == "r7-merchant")
			{
				var shop = MerchantShopProbes.Shop(s);
				Assert.True(shop.Release(SpecialSlot.D).Ok, "release the special in D at r7");
				if (!shop.CanBuy("test-kappa-grab")) Assert.True(shop.Release(SpecialSlot.C).Ok, "free slot C");
				Assert.True(shop.Buy("test-kappa-grab").Ok, "buy again at r7");
			}
			var d = s.WinFight(700);
			if (d is not null && d.Offer.Cards.Count > 0)
			{
				var c = d.Offer.Cards[0];
				Assert.True(d.Pick(0, c.Targets.Count > 0 ? c.Targets[0].Slot : null).Ok, "pick at " + id);
			}
			if (id == "boss-tanuki") { Check("boss won"); break; }
			Assert.True(s.CompleteNode().Ok, "complete " + id);
			Check("completed " + id);
		}
		Assert.True(checks >= 16, "checked at every boundary: " + checks);
	});

	[Test]
	public static void BossNodeSave_WithABoughtModifierOnTheCopyTarget_RoundTripsWithTheSameLockedCopy() => OnFixtures(() =>
	{
		var pool = MerchantShopProbes.Pool();
		var walk = new[] { "r1-apprentice", "r2-kitsune", "r3-oni", "r4-merchant", "r5-elder-oni", "r6-kitsune", "r7-merchant", "r8-elder-kappa" };
		var run = RunState.Restore(600, new[] { (SpecialSlot.A, new OwnedSpecial(pool.Special("test-spirit-wave")!, 1, null)), (SpecialSlot.B, new OwnedSpecial(pool.Special("test-rising-talisman")!, 1, null)), (SpecialSlot.C, new OwnedSpecial(pool.Special("test-foxfire")!, 2, null)) });
		run.RestoreLevelledOrder(0, 0, 1, 0); run.RestoreCoins(200);
		var g = Map();
		run.SetMap(RunMap.Restore(g, new RunMapSnapshot(g.Id, walk, walk[^1])));
		var s = new RunSession(run, pool, Table(), 5);
		// The shop is at row 7 on the real map; here Ryo buys at a hand-made stop by attaching the way the shop does.
		run.Attach(SpecialSlot.C, pool.Modifier("test-kappa-wisp")!, borrowed: true);
		Assert.True(s.Enter("boss-tanuki").Ok, "enter the boss");
		Assert.True(s.LockedCopy is { Slot: SpecialSlot.C, Level: 2 }, "the lock took the copy target");
		var back = RoundTrip(s, pool);
		Assert.True(back.StateHash() == s.StateHash(), "equal hash");
		Assert.True(back.CopyLocked && back.LockedCopy is { Slot: SpecialSlot.C, Level: 2 } l && l.Data.Id == "test-foxfire", "same locked copy");
		Assert.True(back.Run[SpecialSlot.C] is { ModifierBorrowed: true } m && m.Modifier!.Id == "test-kappa-wisp", "borrowed modifier restored");
		Assert.Equal(RewardDraft.FrameSummary(s.LockedCopy!.Build()), RewardDraft.FrameSummary(back.LockedCopy!.Build()), "T4: the copy builds the same, bare");
	});

	static JsonObject V3Json(out AbilityPool pool)
	{
		pool = MerchantShopProbes.Pool();
		var s = MerchantShopProbes.At(pool, 200, "r4-merchant", new[] { "r1-apprentice", "r2-kitsune", "r3-oni", "r4-merchant" }.Take(3).ToArray(),
			(SpecialSlot.A, "test-spirit-wave", 1), (SpecialSlot.B, "test-rising-talisman", 1), (SpecialSlot.C, "test-foxfire", 1));
		s.Run.Attach(SpecialSlot.C, pool.Modifier("test-kappa-wisp")!, borrowed: true);
		return JsonNode.Parse(SaveStore.Serialize(SaveData.Empty.WithRun(RunSave.Capture(s))))!.AsObject();
	}

	[Test]
	public static void Version1File_LoadsWithNothingBorrowed_AndMalformedFlagsAreRefusedOrDropped() => OnFixtures(() =>
	{
		var o = V3Json(out var pool);
		var slots = o["run"]!["slots"]!.AsArray();
		Assert.True(slots[2]!["modifier_borrowed"]!.GetValue<bool>() && !slots[2]!["borrowed"]!.GetValue<bool>(), "the v3 text carries both flags");
		// v1: no coins, no lock fields, no borrowed flags
		var v1 = JsonNode.Parse(o.ToJsonString())!.AsObject();
		v1["version"] = 1;
		foreach (var n in new[] { "coins", "copy_locked", "locked_copy" }) v1["run"]!.AsObject().Remove(n);
		foreach (var sl in v1["run"]!["slots"]!.AsArray()) { sl!.AsObject().Remove("borrowed"); sl.AsObject().Remove("modifier_borrowed"); }
		var load = SaveStore.Parse(v1.ToJsonString());
		Assert.True(load.Status == SaveLoadStatus.OlderVersion && load.Data.Run is not null, "v1 loads as an older version: " + load.Status + " " + string.Join(";", load.Problems));
		Assert.True(load.Data.Run!.Slots.All(x => !x.Borrowed && !x.ModifierBorrowed), "nothing borrowed");
		// non-bool values on either flag
		foreach (var bad in new[] { "\"yes\"", "1", "null", "[]" })
			foreach (var key in new[] { "borrowed", "modifier_borrowed" })
			{
				var t = JsonNode.Parse(o.ToJsonString())!.AsObject();
				t["run"]!["slots"]![0]!.AsObject()[key] = JsonNode.Parse(bad);
				var l = SaveStore.Parse(t.ToJsonString());
				bool refused = l.Data.Run is null;
				bool treatedAsFalse = l.Data.Run is { } r && !r.Slots[0].Borrowed && !r.Slots[0].ModifierBorrowed;
				Assert.True(refused || treatedAsFalse, $"{key}={bad}: dropped or read as false, never true");
			}
		// a modifier_borrowed flag with no modifier on the slot is refused on restore (RunState.Restore)
		var ghost = JsonNode.Parse(o.ToJsonString())!.AsObject();
		ghost["run"]!["slots"]![0]!.AsObject()["modifier_borrowed"] = true;
		bool ghostRefused = false;
		try { SaveStore.Parse(ghost.ToJsonString()).Data.Run!.Restore(pool, Map(), Table()); } catch (FormatException) { ghostRefused = true; }
		Assert.True(ghostRefused, "modifier_borrowed on a slot without a modifier is refused");
	});
}
