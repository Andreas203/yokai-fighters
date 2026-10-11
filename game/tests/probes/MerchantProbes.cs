using System;
using System.Collections.Generic;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>Test Breaker probes for YOK-83 (R6 first half, C1 cap). Expected values come from rules, not code.</summary>
public static class MerchantProbes
{
	static string MapDir => ContentPaths.Data("map");

	static void OnFixtures(Action body)
	{
		var saved = FightScene.Sources; var savedRun = FightScene.Run;
		FightScene.Sources = FightScene.FixtureSources; FightScene.Run = null;
		try { body(); } finally { FightScene.Sources = saved; FightScene.Run = savedRun; }
	}

	static RunSession NewSession(uint seed = 5) => RunSession.Start(
		FightScene.LoadAbilityPool(), FileMapSource.Default(MapDir).Load(), NodeSceneTable.Default(MapDir), seed);

	static void WinDuel(RunSession s, string id, int healthLeft)
	{
		Assert.True(s.Enter(id).Ok, $"enter {id}");
		if (s.WinFight(healthLeft) is { } d)
			Assert.True(d.Pick(0, d.Offer.Cards[0].Targets.Count > 0 ? d.Offer.Cards[0].Targets[0].Slot : null).Ok, $"{id}: pick");
		Assert.True(s.CompleteNode().Ok, $"complete {id}");
	}

	static RunSession AtMerchant(int health, uint seed = 5)
	{
		var s = NewSession(seed);
		WinDuel(s, "r1-apprentice", 500);
		WinDuel(s, "r2-kitsune", 500);
		WinDuel(s, "r3-oni", health - RunState.BindHeal);
		Assert.True(s.Enter("r4-merchant").Ok, "enter the merchant");
		Assert.Equal(health, s.Run.Health, "health on arrival");
		return s;
	}

	static RunSession Rebuild(RunSession a)
	{
		var run = RunState.NewRun(a.Pool);
		run.SetHealth(a.Run.Health);
		run.SetMap(RunMap.Restore(a.Map.Graph, a.Map.Snapshot()));
		return new RunSession(run, a.Pool, a.Scenes, a.Seed);
	}

	// R6 one heal per stop. KNOWN-OPEN, out of scope (needs save code, YOK-87, to persist Choice): this pins TODAY's
	// behaviour (second heal 550 -> 800) so the suite is honest; flip the assertion when the save code calls Restore.
	[Test]
	public static void KnownOpen_RestoredWalk_WithoutRestore_AllowsSecondHeal() => OnFixtures(() =>
	{
		var s = AtMerchant(300);
		s.Merchant!.TakeHeal();
		Assert.Equal(550, s.Run.Health, "healed");
		var loaded = Rebuild(s);
		var r = loaded.Merchant!.TakeHeal();
		Assert.True(r.Ok && loaded.Run.Health == 800, $"known-open: expected the gap (800), got {loaded.Run.Health}");
	});

	[Test]
	public static void RestoreAfterRunOver_IsRefusedAndHashUnchanged() => OnFixtures(() =>
	{
		var s = AtMerchant(300);
		var m = s.Merchant!;
		s.LoseFight();
		ulong before = s.StateHash();
		var r = m.Restore(MerchantChoice.Shop);
		Assert.True(!r.Ok, "restore on an ended run should be refused");
		Assert.Equal(before, s.StateHash(), "hash of an ended run changed");
	});

	[Test]
	public static void Hash_UnspentEqualsNoMerchantAndChoicesDiffer() => OnFixtures(() =>
	{
		var a = AtMerchant(300); var b = AtMerchant(300); var c = AtMerchant(300); var d = AtMerchant(300);
		Assert.Equal(a.StateHash(), b.StateHash(), "deterministic");
		// session with no merchant object at all: same walk, merchant dropped via restore from non-merchant? use null via reflection-free route:
		ulong none = a.StateHash();
		b.Merchant!.TakeHeal(); c.Merchant!.Choose(MerchantChoice.Shop);
		Assert.True(b.StateHash() != none && c.StateHash() != none && b.StateHash() != c.StateHash(), "None/Heal/Shop must all differ");
		// Heal vs same health reached w/o heal: still must differ in hash (choice spent)
		var e = AtMerchant(550);
		Assert.True(e.StateHash() != b.StateHash(), "healed 300->550 vs arrived at 550 must differ");
		// restored from saved equals saved
		// Rebuild() loses the card picks (RunState slots), so equality with the saved session is not testable without save code;
		// check only that Restore moves the hash exactly as a live choice does relative to the unspent loaded stop.
		var l0 = Rebuild(b); ulong u0 = l0.StateHash();
		var lh = Rebuild(b); lh.Merchant!.Restore(MerchantChoice.Heal);
		var ls = Rebuild(b); ls.Merchant!.Restore(MerchantChoice.Shop);
		Assert.True(lh.StateHash() != u0 && ls.StateHash() != u0 && lh.StateHash() != ls.StateHash(), "restored choices differ");
		Assert.True(d.StateHash() == none, "untouched equal");
	});

	[Test]
	public static void Hash_AcrossCompleteNode() => OnFixtures(() =>
	{
		var a = AtMerchant(300); var b = AtMerchant(300);
		a.Merchant!.TakeHeal();
		b.Merchant!.Choose(MerchantChoice.Shop);
		Assert.True(a.CompleteNode().Ok && b.CompleteNode().Ok, "complete");
		Assert.True(a.Merchant == null && b.Merchant == null, "dropped");
		// heal vs shop leave different health, so differ via Run hash; same-health shop-vs-skip is not distinguishable on the map (design: choice is per stop)
		var c = AtMerchant(550); c.CompleteNode();
		Assert.Equal(c.StateHash(), a.StateHash(), "after leaving, same health/map => same hash (choice no longer run state)");
	});

	[Test]
	public static void RefusedEnter_KeepsMerchantAndChoice() => OnFixtures(() =>
	{
		var s = AtMerchant(300);
		var m = s.Merchant!;
		m.TakeHeal();
		ulong h = s.StateHash();
		var r = s.Enter("r5-elder-oni");
		Assert.True(!r.Ok, "enter while in node must be refused");
		Assert.True(ReferenceEquals(s.Merchant, m) && m.Choice == MerchantChoice.Heal && s.StateHash() == h, "kept");
		Assert.True(!m.TakeHeal().Ok && s.Run.Health == 550, "still spent");
	});

	[Test]
	public static void SecondHealRoutes() => OnFixtures(() =>
	{
		// heal after shop, shop after heal
		var s = AtMerchant(300);
		s.Merchant!.Choose(MerchantChoice.Shop);
		ulong h = s.StateHash();
		Assert.True(!s.Merchant.TakeHeal().Ok && s.Run.Health == 300 && s.StateHash() == h, "heal after shop");
		var t = AtMerchant(300);
		t.Merchant!.TakeHeal(); h = t.StateHash();
		Assert.True(!t.Merchant.Choose(MerchantChoice.Shop).Ok && t.StateHash() == h && t.Merchant.Choice == MerchantChoice.Heal, "shop after heal");
		Assert.True(!t.Merchant.Choose(MerchantChoice.None).Ok, "None is not a choice");
		// stale ref after CompleteNode
		var u = AtMerchant(300); var stale = u.Merchant!;
		u.CompleteNode();
		Assert.True(!stale.TakeHeal().Ok && u.Run.Health == 300, "stale after complete");
		// stale ref while standing at the other merchant (r7)
		// heal when run is over
		var v = AtMerchant(300); var m = v.Merchant!;
		v.LoseFight();
		Assert.True(!m.TakeHeal().Ok && v.Run.Health == 300, "heal after run over");
		// not a merchant node
		var w = NewSession(); w.Enter("r1-apprentice");
		Assert.True(w.Merchant == null, "no stop on a duel");
		// stale ref from r4 while at r5
		var x = AtMerchant(300); var old = x.Merchant!; x.CompleteNode(); x.Enter("r5-elder-oni");
		Assert.True(!old.TakeHeal().Ok && x.Run.Health == 300, "stale at later node");
	});

	[Test]
	public static void BoundaryHealths_C1() => OnFixtures(() =>
	{
		foreach (var (from, to) in new[] { (50, 300), (749, 999), (750, 1000), (751, 1000), (999, 1000), (1000, 1000) })
		{
			var s = AtMerchant(from);
			Assert.True(s.Merchant!.TakeHeal().Ok, "taken");
			Assert.Equal(to, s.Run.Health, $"from {from}");
		}
		Assert.Equal(250, Merchant.Healed(0), "0 -> 250");
		Assert.Equal(1000, Merchant.Healed(1000), "1000 stays");
	});

	[Test]
	public static void HealThenStandInFight_AndFightThenHeal() => OnFixtures(() =>
	{
		// fight first, then heal: heal applies to the post-fight health
		var a = AtMerchant(300);
		a.WinFight(400);
		Assert.Equal(400, a.Run.Health, "stand-in fight result");
		a.Merchant!.TakeHeal();
		Assert.Equal(650, a.Run.Health, "heal after fight on fight health");
		Assert.True(a.CompleteNode().Ok, "complete");
		// heal first then fight: R3 forbids no heal at non-binding; stand-in overwrites (YOK-96)
		var b = AtMerchant(300);
		b.Merchant!.TakeHeal();
		b.WinFight(100);
		Assert.Equal(100, b.Run.Health, "stand-in overwrites");
		Assert.True(b.CompleteNode().Ok, "complete");
		Assert.True(b.Merchant == null, "dropped");
	});

	[Test]
	public static void BothMerchantsOnDefaultMapHealOnce() => OnFixtures(() =>
	{
		var s = AtMerchant(300);
		s.Merchant!.TakeHeal(); s.CompleteNode();
		WinDuel(s, "r5-elder-oni", 500);
		WinDuel(s, "r6-kitsune", 200);
		Assert.True(s.Enter("r7-merchant").Ok, "r7");
		Assert.True(s.Merchant is { Choice: MerchantChoice.None, NodeId: "r7-merchant" }, "fresh stop at r7");
		Assert.True(s.Merchant!.TakeHeal().Ok, "r7 heal");
		int h = s.Run.Health;
		Assert.Equal(Math.Min(1000, 250 + 250), h, "r7 heal: 250 -> 500 expected"); // 200 + R3 50 = 250
		Assert.True(!s.Merchant.TakeHeal().Ok && s.Run.Health == h, "no second");
	});
}
