using System;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-83: the merchant's free heal (R6), sim only. A <see cref="RunSession"/> is walked to a merchant stop on the
/// default map (row 4 always, row 7 by route, R1) and the stop's one choice is taken, refused or restored.
/// </summary>
public static class MerchantTests
{
	static string MapDir => ContentPaths.Data("map");

	static void OnFixtures(Action body)
	{
		var saved = FightScene.Sources;
		var savedRun = FightScene.Run;
		FightScene.Sources = FightScene.FixtureSources;
		FightScene.Run = null;
		try { body(); }
		finally { FightScene.Sources = saved; FightScene.Run = savedRun; }
	}

	static RunSession NewSession(uint seed = 5) => RunSession.Start(
		FightScene.LoadAbilityPool(), FileMapSource.Default(MapDir).Load(), NodeSceneTable.Default(MapDir), seed);

	/// <summary>Wins the entered duel with <paramref name="healthLeft"/> (R3 adds 50), takes the first card and moves on.</summary>
	static void WinDuel(RunSession s, string id, int healthLeft)
	{
		Assert.True(s.Enter(id).Ok, $"enter {id}");
		if (s.WinFight(healthLeft) is { } d)
			Assert.True(d.Pick(0, d.Offer.Cards[0].Targets.Count > 0 ? d.Offer.Cards[0].Targets[0].Slot : null).Ok, $"{id}: pick");
		Assert.True(s.CompleteNode().Ok, $"complete {id}");
	}

	/// <summary>A run standing at the row-4 merchant with <paramref name="health"/> (50..1000).</summary>
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

	[Test]
	public static void Heal_Is25PercentOfFullHealth() => OnFixtures(() =>
	{
		Assert.Equal(25, Merchant.HealPercent, "R6: 25%");
		Assert.Equal(250, Merchant.HealAmount, "R6 on C1: 25% of 1,000");
		var s = AtMerchant(600);
		Assert.True(s.Merchant is { Choice: MerchantChoice.None, CanHeal: true, NodeId: "r4-merchant" }, "the stop offers its heal");
		Assert.True(s.Merchant!.TakeHeal().Ok, "the heal is taken");
		Assert.Equal(850, s.Run.Health, "600 + 250");
		Assert.Equal(MerchantChoice.Heal, s.Merchant.Choice, "the choice is recorded");
		Assert.Equal(850, s.Run.MatchConfig().P1StartHealth!.Value, "C1: the next fight starts from the healed health");
	});

	[Test]
	public static void Heal_CapsAtFullHealth() => OnFixtures(() =>
	{
		foreach (var (from, to) in new[] { (50, 300), (749, 999), (750, 1000), (751, 1000), (900, 1000), (1000, 1000) })
		{
			Assert.Equal(to, Merchant.Healed(from), $"healed from {from}");
			var s = AtMerchant(from);
			Assert.True(s.Merchant!.TakeHeal().Ok, $"from {from}: taken");
			Assert.Equal(to, s.Run.Health, $"from {from}: C1 cap");
		}
		Assert.Equal(250, Merchant.Healed(-40), "below 0 counts as 0");
		Assert.Equal(1000, Merchant.Healed(5000), "above full counts as full");
		var full = AtMerchant(1000);
		Assert.True(full.Merchant!.TakeHeal().Ok && full.Merchant.Choice == MerchantChoice.Heal, "a heal at full health still spends the choice");
	});

	[Test]
	public static void Heal_Once_ASecondHealChangesNothing() => OnFixtures(() =>
	{
		var s = AtMerchant(300);
		var m = s.Merchant!;
		Assert.True(m.TakeHeal().Ok, "first heal");
		ulong after = s.StateHash();
		var again = m.TakeHeal();
		Assert.True(!again.Ok && again.Error!.Contains("R6"), "R6: once");
		Assert.True(!m.Choose(MerchantChoice.Heal).Ok && !m.CanHeal && !m.CanChoose, "and not through Choose either");
		Assert.Equal(550, s.Run.Health, "300 + 250, once");
		Assert.True(s.StateHash() == after, "a refused heal changes nothing");
		Assert.True(ReferenceEquals(m, s.Merchant), "the session keeps the one stop while Ryo is on it");
	});

	[Test]
	public static void HealOrShop_OneChoicePerStop() => OnFixtures(() =>
	{
		var shopped = AtMerchant(400);
		Assert.True(shopped.Merchant!.Choose(MerchantChoice.Shop).Ok, "the shop takes the stop's choice");
		Assert.Equal(400, shopped.Run.Health, "shopping does not heal");
		Assert.True(!shopped.Merchant.CanHeal && !shopped.Merchant.TakeHeal().Ok, "R6: heal or shop, not both");
		Assert.Equal(400, shopped.Run.Health, "the refused heal gave nothing");
		Assert.Equal(MerchantChoice.Shop, shopped.Merchant.Choice, "the shop's choice stands");

		var healed = AtMerchant(400);
		healed.Merchant!.TakeHeal();
		Assert.True(!healed.Merchant.Choose(MerchantChoice.Shop).Ok && healed.Merchant.Choice == MerchantChoice.Heal, "healed: the shop is closed");

		var none = AtMerchant(400);
		Assert.True(!none.Merchant!.Choose(MerchantChoice.None).Ok && !none.Merchant.Choose((MerchantChoice)9).Ok, "None and unknown values are not choices");
		Assert.True(none.Merchant.CanHeal && none.Run.Health == 400, "a refused choice spends nothing");
		Assert.True(none.CompleteNode().Ok, "Ryo may leave without choosing");
	});

	[Test]
	public static void Stop_OnlyOnMerchantNodes_AndOnlyWhileRyoIsThere() => OnFixtures(() =>
	{
		var s = NewSession();
		Assert.True(s.Merchant == null, "on the map: no merchant");
		s.Enter("r1-apprentice");
		Assert.True(s.Merchant == null, "a duel is not a merchant");
		s.LoseFight();

		s = AtMerchant(300);
		var row4 = s.Merchant!;
		Assert.True(s.CompleteNode().Ok && s.Merchant == null, "the stop closes when Ryo leaves");
		Assert.True(!row4.Open && !row4.CanHeal && !row4.TakeHeal().Ok && s.Run.Health == 300, "a stop Ryo has left gives nothing");

		WinDuel(s, "r5-elder-oni", 250);
		WinDuel(s, "r6-kitsune", 250);
		Assert.True(s.Enter("r7-dojo").Ok && s.Merchant == null, "a dojo is not a merchant");

		// the row-7 merchant is a new stop with its own choice
		s = AtMerchant(300);
		s.Merchant!.TakeHeal();
		s.CompleteNode();
		WinDuel(s, "r5-elder-oni", 250);
		WinDuel(s, "r6-kitsune", 250);
		Assert.True(s.Enter("r7-merchant").Ok, "enter the row-7 merchant");
		Assert.Equal(300, s.Run.Health, "250 left + 50");
		Assert.True(s.Merchant is { Choice: MerchantChoice.None, NodeId: "r7-merchant" } && s.Merchant.TakeHeal().Ok, "a second stop heals again");
		Assert.Equal(550, s.Run.Health, "300 + 250");

		// a lost run: the stop is shut
		var lost = AtMerchant(300);
		var m = lost.Merchant!;
		lost.LoseFight();
		Assert.True(!m.Open && !m.TakeHeal().Ok && lost.Run.Health == 300 && m.Choice == MerchantChoice.None, "no heal after the run is over");
	});

	[Test]
	public static void Heal_AroundTheStandInFight_AndDeterministic() => OnFixtures(() =>
	{
		// Merchant nodes are stand-in fights today: the heal works before or after that fight.
		var before = AtMerchant(300);
		before.Merchant!.TakeHeal();
		Assert.True(before.WinFight(500) == null, "the stand-in fight gives no draft");
		Assert.Equal(500, before.Run.Health, "the fight's result carries (C1), no bind heal");
		Assert.True(!before.Merchant!.TakeHeal().Ok, "still once");

		var after = AtMerchant(300);
		after.WinFight(280);
		Assert.True(after.Merchant!.TakeHeal().Ok, "heal after the fight");
		Assert.Equal(530, after.Run.Health, "280 + 250");

		var a = AtMerchant(300, seed: 9);
		var b = AtMerchant(300, seed: 9);
		ulong unspent = a.StateHash();
		Assert.True(unspent == b.StateHash(), "same walk, same hash");
		a.Merchant!.TakeHeal();
		b.Merchant!.TakeHeal();
		Assert.True(a.StateHash() == b.StateHash() && a.StateHash() != unspent, "the heal is deterministic and part of the run's hash");

		var wasted = AtMerchant(1000, seed: 9);
		ulong full = wasted.StateHash();
		wasted.Merchant!.TakeHeal();
		Assert.True(wasted.StateHash() != full, "a heal at full health changes no health but is still spent state");
		var shop = AtMerchant(1000, seed: 9);
		shop.Merchant!.Choose(MerchantChoice.Shop);
		Assert.True(shop.StateHash() != wasted.StateHash() && shop.StateHash() != full, "heal and shop are different states");
	});

	[Test]
	public static void Restore_PutsTheChoiceBack_WithoutHealingAgain() => OnFixtures(() =>
	{
		var saved = AtMerchant(300);
		saved.Merchant!.TakeHeal();

		// a restored walk standing on the merchant: same state, a fresh session
		var run = RunState.NewRun(saved.Pool);
		run.SetHealth(saved.Run.Health);
		run.SetMap(RunMap.Restore(saved.Map.Graph, saved.Map.Snapshot()));
		var loaded = new RunSession(run, saved.Pool, saved.Scenes, saved.Seed);
		Assert.True(loaded.Merchant is { Choice: MerchantChoice.None, NodeId: "r4-merchant" }, "a restored walk on a merchant has its stop");
		Assert.True(loaded.Merchant!.Restore(MerchantChoice.Heal).Ok, "the saved choice goes back");
		Assert.Equal(550, loaded.Run.Health, "restoring does not heal again");
		Assert.True(!loaded.Merchant.TakeHeal().Ok && loaded.Run.Health == 550, "and the heal stays spent");
		Assert.True(!loaded.Merchant.Restore(MerchantChoice.None).Ok && !loaded.Merchant.Restore(MerchantChoice.Shop).Ok, "a recorded choice is not overwritten");
		Assert.True(!NewStop().Restore((MerchantChoice)9).Ok, "an unknown value is refused");

		static Merchant NewStop() => AtMerchant(300).Merchant!;

		// the session constructor takes the saved choice, so a restore need not call Restore itself
		RunState Saved()
		{
			var r = RunState.NewRun(saved.Pool);
			r.SetHealth(saved.Run.Health);
			r.SetMap(RunMap.Restore(saved.Map.Graph, saved.Map.Snapshot()));
			return r;
		}
		var direct = new RunSession(Saved(), saved.Pool, saved.Scenes, saved.Seed, MerchantChoice.Heal);
		Assert.True(direct.Merchant!.Choice == MerchantChoice.Heal && !direct.Merchant.TakeHeal().Ok && direct.Run.Health == 550, "restored spent: no second heal");
		Assert.True(direct.StateHash() == loaded.StateHash(), "the same state as restoring by hand (the rebuilt run has the starters only)");
		Throws(() => new RunSession(Saved(), saved.Pool, saved.Scenes, saved.Seed, (MerchantChoice)9), "an unknown saved choice");
		var onMap = RunState.NewRun(saved.Pool, saved.Map.Graph);
		Throws(() => new RunSession(onMap, saved.Pool, saved.Scenes, saved.Seed, MerchantChoice.Heal), "a saved choice with no merchant entered");

		// a stop of an ended run takes no restored choice either
		var lost = AtMerchant(300);
		var stale = lost.Merchant!;
		lost.LoseFight();
		ulong ended = lost.StateHash();
		Assert.True(!stale.Restore(MerchantChoice.Shop).Ok && stale.Choice == MerchantChoice.None && lost.StateHash() == ended, "no restore after the run is over");

		static void Throws(Action body, string what)
		{
			try { body(); }
			catch (ArgumentException) { return; }
			throw new AssertFailed($"{what}: expected ArgumentException");
		}
	});
}
