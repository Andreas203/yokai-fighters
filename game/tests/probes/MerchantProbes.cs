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

	/// <summary>A faithful copy of a live session through the public API: slots, levels, modifiers, level-up order, health, map, choice.</summary>
	static RunSession FullRebuild(RunSession a, MerchantChoice choice)
	{
		var run = RunState.NewRun(a.Pool);
		foreach (var (slot, o) in a.Run.Owned)
		{
			if (run[slot]?.Data.Id != o.Data.Id) run.Equip(o.Data);
			for (int l = 1; l < o.Level; l++) run.LevelUp(slot);
			if (o.Modifier is { } mod) run.Attach(slot, mod);
		}
		run.RestoreLevelledOrder(a.Run.LevelledOrder((SpecialSlot)1), a.Run.LevelledOrder((SpecialSlot)2), a.Run.LevelledOrder((SpecialSlot)3), a.Run.LevelledOrder((SpecialSlot)4));
		run.SetHealth(a.Run.Health);
		run.RestoreCoins(a.Run.Coins); // R8 purse is run state since YOK-85
		run.RestoreCancelRules(a.Run.CancelRules); // X1/X2 held cancel rules are run state since YOK-186 (the walk to r7 takes Oni Chain at the row-5 elder)
		run.SetMap(RunMap.Restore(a.Map.Graph, a.Map.Snapshot()));
		return new RunSession(run, a.Pool, a.Scenes, a.Seed, choice);
	}

	// R6 one heal per stop. Pins ONLY this: a rebuild helper that omits the merchant choice (here Rebuild, 4-arg
	// constructor) re-heals 550 -> 800. It says nothing about any save loader. Delete it, or retarget it at the real
	// loader asserting the second heal is refused, when YOK-73/87 land; RestoredThroughConstructor_* pin the fix.
	[Test]
	public static void RebuildThatOmitsTheChoice_ReHeals() => OnFixtures(() =>
	{
		var s = AtMerchant(300);
		s.Merchant!.TakeHeal();
		Assert.Equal(550, s.Run.Health, "healed");
		var loaded = Rebuild(s);
		var r = loaded.Merchant!.TakeHeal();
		Assert.True(r.Ok && loaded.Run.Health == 800, $"omitted choice re-heals (800), got {loaded.Run.Health}");
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
	public static void Hash_UnspentIsThePreTicketFormula_AndChoicesDiffer() => OnFixtures(() =>
	{
		var a = AtMerchant(300); var b = AtMerchant(300); var c = AtMerchant(300); var d = AtMerchant(300);
		Assert.Equal(a.StateHash(), b.StateHash(), "deterministic");
		ulong none = a.StateHash();
		// the pre-YOK-83 formula, computed here: an unspent stop must hash exactly this
		ulong r = a.Run.StateHash();
		ulong pre = Fnv.Mix(Fnv.Offset, unchecked((int)r));
		pre = Fnv.Mix(pre, unchecked((int)(r >> 32)));
		pre = Fnv.Mix(pre, unchecked((int)a.Seed));
		pre = Fnv.Mix(pre, 0);
		Assert.Equal(pre, none, "unspent stop hashes as a session that never had a merchant choice");
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

	// ---- round 2: the constructor's merchantChoice argument ----

	[Test]
	public static void CtorChoice_HealSpent_NoHealAndRefusesSecond() => OnFixtures(() =>
	{
		var s = AtMerchant(300); s.Merchant!.TakeHeal();
		var l = FullRebuild(s, MerchantChoice.Heal);
		Assert.Equal(550, l.Run.Health, "constructor does not heal");
		Assert.True(l.Merchant!.Choice == MerchantChoice.Heal && !l.Merchant.TakeHeal().Ok && !l.Merchant.Choose(MerchantChoice.Shop).Ok && l.Run.Health == 550, "spent");
		Assert.Equal(s.StateHash(), l.StateHash(), "restored via ctor hashes equal to the live session it came from");
	});

	[Test]
	public static void CtorChoice_ShopSpent_RefusesHeal_HashEqualsLive() => OnFixtures(() =>
	{
		var s = AtMerchant(300); s.Merchant!.Choose(MerchantChoice.Shop);
		var l = FullRebuild(s, MerchantChoice.Shop);
		Assert.True(!l.Merchant!.TakeHeal().Ok && l.Run.Health == 300, "shop spent refuses heal");
		Assert.Equal(s.StateHash(), l.StateHash(), "equal to live");
	});

	[Test]
	public static void CtorChoice_UnspentAndDefault_MatchLiveAndFourArg() => OnFixtures(() =>
	{
		var s = AtMerchant(300);
		var l = FullRebuild(s, MerchantChoice.None);
		Assert.Equal(s.StateHash(), l.StateHash(), "None equals live unspent");
		Assert.True(l.Merchant!.CanHeal && l.Merchant.TakeHeal().Ok && l.Run.Health == 550, "still healable");
		var run = RunState.NewRun(s.Pool); run.SetMap(RunMap.Restore(s.Map.Graph, s.Map.Snapshot()));
		var four = new RunSession(run, s.Pool, s.Scenes, s.Seed);
		var run2 = RunState.NewRun(s.Pool); run2.SetMap(RunMap.Restore(s.Map.Graph, s.Map.Snapshot()));
		var five = new RunSession(run2, s.Pool, s.Scenes, s.Seed, MerchantChoice.None);
		Assert.Equal(four.StateHash(), five.StateHash(), "default None == four-arg");
	});

	[Test]
	public static void CtorChoice_EqualsHandRestore() => OnFixtures(() =>
	{
		var s = AtMerchant(300); s.Merchant!.TakeHeal();
		var viaCtor = FullRebuild(s, MerchantChoice.Heal);
		var byHand = FullRebuild(s, MerchantChoice.None);
		Assert.True(byHand.Merchant!.Restore(MerchantChoice.Heal).Ok, "hand restore");
		Assert.Equal(viaCtor.StateHash(), byHand.StateHash(), "ctor == Restore by hand");
	});

	[Test]
	public static void CtorChoice_BadArguments_Throw() => OnFixtures(() =>
	{
		var s = AtMerchant(300);
		static bool Throws(Func<RunSession> f) { try { f(); return false; } catch (ArgumentException) { return true; } }
		Assert.True(Throws(() => FullRebuild(s, (MerchantChoice)9)), "undefined value throws");
		Assert.True(Throws(() => FullRebuild(s, (MerchantChoice)(-1))), "negative undefined throws");
		// on the map (merchant completed)
		var done = AtMerchant(300); done.CompleteNode();
		Assert.True(Throws(() => FullRebuild(done, MerchantChoice.Heal)), "on the map throws");
		// inside a non-merchant node
		var duel = NewSession(); duel.Enter("r1-apprentice");
		Assert.True(Throws(() => FullRebuild(duel, MerchantChoice.Shop)), "non-merchant node throws");
		// fresh run, no node
		Assert.True(Throws(() => FullRebuild(NewSession(), MerchantChoice.Heal)), "no node throws");
		// nothing half-built: the source sessions and a good rebuild are unaffected
		Assert.True(s.Merchant!.CanHeal && s.Run.Health == 300, "source untouched");
		Assert.True(FullRebuild(s, MerchantChoice.None).Merchant!.TakeHeal().Ok, "a later good rebuild works");
	});

	[Test]
	public static void Restore_StaleStopAndEndedRun_RefusedHashUnchanged() => OnFixtures(() =>
	{
		var s = AtMerchant(300); var stale = s.Merchant!;
		s.CompleteNode();
		ulong h = s.StateHash();
		Assert.True(!stale.Restore(MerchantChoice.Heal).Ok && stale.Choice == MerchantChoice.None && s.StateHash() == h, "stale stop after CompleteNode");
		var e = AtMerchant(300); var m = e.Merchant!; m.TakeHeal();
		e.LoseFight(); ulong he = e.StateHash();
		Assert.True(!m.Restore(MerchantChoice.Shop).Ok && m.Choice == MerchantChoice.Heal && e.StateHash() == he, "ended run, spent choice kept");
		var e2 = AtMerchant(300); var m2 = e2.Merchant!; e2.LoseFight(); ulong h2 = e2.StateHash();
		Assert.True(!m2.Restore(MerchantChoice.Heal).Ok && m2.Choice == MerchantChoice.None && e2.StateHash() == h2 && e2.Run.Health == 300, "ended run, unspent");
	});

	[Test]
	public static void CtorChoice_AtSecondMerchantAndAfterStandInFight() => OnFixtures(() =>
	{
		var s = AtMerchant(300); s.Merchant!.TakeHeal(); s.CompleteNode();
		WinDuel(s, "r5-elder-oni", 500); WinDuel(s, "r6-kitsune", 200);
		Assert.True(s.Enter("r7-merchant").Ok, "r7");
		s.Merchant!.TakeHeal(); s.WinFight(600);
		var l = FullRebuild(s, MerchantChoice.Heal);
		Assert.Equal(s.StateHash(), l.StateHash(), "r7 after stand-in fight, equal to live");
		Assert.True(!l.Merchant!.TakeHeal().Ok && l.Run.Health == 600, "no second heal at r7");
	});
}
