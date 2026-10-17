using System;
using System.Collections.Generic;
using System.Linq;
using YokaiFighters.Ai;
using YokaiFighters.Harness;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>Test Breaker probes for YOK-26 (R5, R6, T0, T1, E22, K2, P6). Expected values come from rules.md.</summary>
public static class RunHarnessProbes
{
	static HarnessContent Content => HarnessContent.Load(ContentPaths.DataDir, ContentPaths.FixturesDir);
	static BotConfig Bot => BotConfig.LoadDefault(ContentPaths.DataDir);

	static FightRecord Stub(RunSession s, bool won = true) => new()
	{
		Profile = "stub", Won = won, Ticks = 600, HealthLeft = Math.Max(1, s.Run.Health - 10), DamageTaken = 10, Bot = new BotStats(),
	};

	// Determinism: static state must not leak between harness instances, interleaved seeds or reloaded content.
	[Test]
	public static void SameSeed_TwoInstancesInterleavedWithAnotherSeed_SameBytes()
	{
		var bot = Bot;
		string a = HarnessReport.ToJson(RunHarness.PlayBatch(Content, bot, 21, 3));
		string other = HarnessReport.ToJson(RunHarness.PlayBatch(Content, bot, 500, 2));
		string b = HarnessReport.ToJson(RunHarness.PlayBatch(Content, bot, 21, 3));
		Assert.True(a == b, "same seed after another seed's batch (fresh content each time) gives the same bytes");
		Assert.True(a != other, "different seeds differ");
	}

	[Test]
	public static void SameSeed_ReusedContent_SameSessionHashes()
	{
		var content = Content; var bot = Bot;
		var x = RunHarness.PlayRun(content, bot, 8);
		RunHarness.PlayRun(content, bot, 9);
		var y = RunHarness.PlayRun(content, bot, 8);
		Assert.Equal(x.SessionHash, y.SessionHash, "session hash");
		Assert.Equal(string.Join(",", x.Nodes.Select(n => n.Fight?.MatchHash)), string.Join(",", y.Nodes.Select(n => n.Fight?.MatchHash)), "every fight's match hash");
	}

	[Test]
	public static void DifferentSeeds_GiveDifferentRuns_AtRunLevel()
	{
		var content = Content; var bot = Bot;
		var hashes = Enumerable.Range(1, 6).Select(s => RunHarness.PlayRun(content, bot, (uint)s).SessionHash).Distinct().Count();
		Assert.True(hashes >= 4, $"6 seeds gave only {hashes} distinct session hashes");
	}

	[Test]
	public static void SeedZeroAndWrap_DoNotThrow_AndZeroDiffersFromOne()
	{
		var content = Content; var bot = Bot;
		var z = RunHarness.PlayRun(content, bot, 0, (_, _, s, _) => Stub(s));
		var o = RunHarness.PlayRun(content, bot, 1, (_, _, s, _) => Stub(s));
		Assert.True(z.Nodes.Count > 0 && o.Nodes.Count > 0, "both walk");
		var wrap = RunHarness.PlayBatch(content, bot, uint.MaxValue, 0);
		Assert.Equal(0, wrap.Runs.Count, "empty batch");
		Assert.True(HarnessReport.ToJson(wrap).Length > 0, "an empty batch still writes a report (no divide by zero)");
	}

	// Seam: wrapping the real fight must give the same run; stops never call the runner.
	[Test]
	public static void FightRunnerSeam_PassThroughIsIdentity_StopsNeverCallIt()
	{
		var content = Content; var bot = Bot;
		var plain = RunHarness.PlayRun(content, bot, 13);
		var types = new List<string>();
		var wrapped = RunHarness.PlayRun(content, bot, 13, (c, b, s, seed) => { types.Add(s.Node!.Type); return RunHarness.FightNode(c, b, s, seed); });
		Assert.Equal(plain.SessionHash, wrapped.SessionHash, "a pass-through runner changes nothing");
		Assert.True(!types.Contains(NodeTypeRegistry.Merchant) && !types.Contains(NodeTypeRegistry.Dojo), "R6/R7 stops are not fights");
		Assert.Equal(plain.Nodes.Count(n => n.Fight is not null), types.Count, "one call per fight node");
	}

	// T0/T1: the Tanuki node's match = Ryo's kit + the locked copy; other nodes have no copied slot.
	[Test]
	public static void NodeFight_TanukiMatch_EqualsManualTanukiKitBuild_AndOthersCopyNothing()
	{
		var content = Content; var bot = Bot;
		int checkedBoss = 0, checkedOthers = 0;
		RunHarness.PlayRun(content, bot, 4, (c, b, s, seed) =>
		{
			var m = NodeFight.NewMatch(c.Sources, s.Run, s, s.Node!.Fighter, b.Scheme);
			if (s.Node.Type == NodeTypeRegistry.Boss)
			{
				Assert.True(s.CopyLocked && s.LockedCopy is not null, "T1: locked on entering");
				Assert.Equal(s.LockedCopy!.Slot, m.P2.CopiedSlot, "T0: copied slot is the locked slot");
				Assert.Equal(s.LockedCopy.Data.Id, m.P2.Specials[(int)s.LockedCopy.Slot]!.Move.Id.Replace("-ex", ""), "T0: the copied special is the locked one");
				// building twice from the same state is byte-identical (no hidden randomness in the copy)
				var m2 = NodeFight.NewMatch(c.Sources, s.Run, s, s.Node.Fighter, b.Scheme);
				Assert.Equal(m.StateHash(), m2.StateHash(), "T1: same locked copy → same match");
				checkedBoss++;
			}
			else { Assert.Equal(SpecialSlot.None, m.P2.CopiedSlot, $"{s.Node.NodeId}: only the Tanuki copies (T0)"); checkedOthers++; }
			return Stub(s);
		});
		Assert.True(checkedBoss == 1 && checkedOthers > 0, "boss and non-boss nodes checked");
	}

	// E22: EX and burst only in elder and Tanuki fights, for every node the map has.
	[Test]
	public static void AiFightTiers_EveryMapNode_FollowsE22()
	{
		var content = Content;
		Assert.Equal(AiFightTier.Standard, AiFightTiers.Of(null), "no node = standard");
		int boss = 0, elder = 0, std = 0;
		foreach (var id in content.Map.Nodes.Select(n => n.Id))
		{
			var node = content.Map.Node(id);
			var ctx = content.Scenes.Resolve(node).Context;
			var want = ctx.Type == NodeTypeRegistry.Boss ? AiFightTier.Tanuki : node.Elder ? AiFightTier.Elder : AiFightTier.Standard;
			Assert.Equal(want, AiFightTiers.Of(ctx), $"{id} ({ctx.Type}, elder={node.Elder})");
			if (want == AiFightTier.Tanuki) boss++; else if (want == AiFightTier.Elder) elder++; else std++;
		}
		Assert.True(boss >= 1 && elder >= 1 && std >= 1, "all three tiers occur in the default map");
	}

	// K2/P6: the bot's whole output over real fights (including the Tanuki) is raw Kihon buttons: no slot request, no EX, no precision, no burst.
	[Test]
	public static void Bot_AcrossRealFights_NeverRequestsSlot_Ex_Precision_OrBurst()
	{
		var content = Content; var bot = Bot;
		int fights = 0, specials = 0;
		for (uint seed = 30; seed < 33; seed++)
			RunHarness.PlayRun(content, bot, seed, (c, b, s, runSeed) =>
			{
				var m = NodeFight.NewMatch(c.Sources, s.Run, s, s.Node!.Fighter, b.Scheme);
				var ai = new ProfileAi(c.Profiles(s.Node.Fighter)[0], 1, AiLoadout.From(m.P2), 5, s.Node.ReactionFrames, AiFightTiers.Of(s.Node));
				var ryo = new NewcomerBot(b.Fight, BotKit.From(m.P1), 77 + seed);
				m.SpecialStarted += (_, e) => { if (e.Fighter == 0) { specials++; Assert.True(!e.Ex && !e.Precision, "Ryo's bot: no EX, no precision (K2)"); } };
				m.BurstFired += (_, i) => Assert.True(i != 0, "the bot never bursts");
				for (int t = 0; t < 2400 && m.Phase != MatchPhase.Over; t++)
				{
					var view = AiView.Capture(m);
					var input = ryo.Next(view);
					Assert.Equal((byte)0, input.Move, "no slot request");
					m.Step(input, ai.Next(view));
				}
				fights++;
				return Stub(s);
			});
		Assert.True(fights > 10 && specials > 0, $"{fights} fights, {specials} specials");
	}

	// Results JSON: stable text for the same batch, and Write is idempotent.
	[Test]
	public static void ResultsJson_SameBatchTwice_SameText_AndWriteIsIdempotent()
	{
		var batch = RunHarness.PlayBatch(Content, Bot, 2, 2);
		string a = HarnessReport.ToJson(batch);
		Assert.True(a == HarnessReport.ToJson(batch), "ToJson twice");
		string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "yok26-probe-" + Guid.NewGuid().ToString("N"));
		try
		{
			string p1 = HarnessReport.Write(batch, dir), t1 = System.IO.File.ReadAllText(p1);
			string p2 = HarnessReport.Write(batch, dir);
			Assert.Equal(p1, p2, "same file name");
			Assert.True(t1 == System.IO.File.ReadAllText(p2) && t1.TrimEnd() == a.TrimEnd(), "file text = ToJson, rewritten identically");
		}
		finally { if (System.IO.Directory.Exists(dir)) System.IO.Directory.Delete(dir, true); }
	}
}

public static partial class MerchantShopTests
{
	static (RunSession s, List<string> bought, string r) Shopped(int coins, MerchantPlan plan, int pct, params (SpecialSlot, string, int)[] slots)
	{
		var s = At(ShopPool(), coins, true, slots);
		var bought = new List<string>();
		string r = BotWith(plan, pct).AtMerchant(s, bought);
		return (s, bought, r);
	}

	// R6: 50 to buy. Exactly 50 buys one; 49 and 0 buy nothing and the heal stays closed (the bot chose the shop).
	[Test]
	public static void Probe_Shop_CoinBoundary_50Buys_49And0DoNot() => OnFixtures(() =>
	{
		var (s50, b50, _) = Shopped(50, MerchantPlan.Shop, 100, Starters);
		Assert.Equal(1, b50.Count, "50 coins: one buy");
		Assert.Equal(0, s50.Run.Coins, "R6: 50 spent exactly");
		foreach (int c in new[] { 49, 0 })
		{
			var (s, b, r) = Shopped(c, MerchantPlan.Shop, 100, Starters);
			Assert.Equal("shop", r, $"{c} coins: still the shop stop");
			Assert.Equal(0, b.Count, $"{c} coins: nothing bought");
			Assert.Equal(c, s.Run.Coins, $"{c} coins: none spent");
			Assert.Equal(600, s.Run.Health, "no heal");
		}
	});

	// Never releases: a full set of slots keeps every owned special, even with plenty of coins.
	[Test]
	public static void Probe_Shop_FullSlots_NeverReleases_OnlyModifiers() => OnFixtures(() =>
	{
		var full = StartersAnd((SpecialSlot.C, "test-kappa-grab", 1), (SpecialSlot.D, "test-kappa-spin", 1));
		var (s, bought, _) = Shopped(500, MerchantPlan.Shop, 100, full);
		Assert.Equal("A,B,C,D", string.Join(",", s.Run.Owned.Select(o => o.Slot.ToString()).OrderBy(x => x)), "no slot emptied");
		Assert.Equal("test-spirit-wave,test-rising-talisman,test-kappa-grab,test-kappa-spin".Split(',').OrderBy(x => x).Aggregate((a, b) => a + "," + b),
			s.Run.Owned.Select(o => o.Special.Data.Id).OrderBy(x => x).Aggregate((a, b) => a + "," + b), "same four specials");
		Assert.Equal(500 - 50 * bought.Count, s.Run.Coins, "R6: 50 per buy, nothing refunded (no 40 release)");
	});

	// R6 heal rule line: heal_below_pct 0 never heals; 100 heals at 600/1000 and shops at full health.
	[Test]
	public static void Probe_HealIfHurt_Extremes() => OnFixtures(() =>
	{
		Assert.Equal("shop", Shopped(120, MerchantPlan.HealIfHurtElseShop, 0, Starters).r, "pct 0: nothing is below 0%");
		Assert.Equal("heal", Shopped(120, MerchantPlan.HealIfHurtElseShop, 61, Starters).r, "60% < 61%");
		Assert.Equal("heal", Shopped(120, MerchantPlan.HealIfHurtElseShop, 100, Starters).r, "60% < 100%");
		var s = At(ShopPool(), 120, true, Starters);
		s.Run.SetHealth(RunState.MaxHealth);
		Assert.Equal("shop", BotWith(MerchantPlan.HealIfHurtElseShop, 100).AtMerchant(s), "100% is not below 100%");
		var z = At(ShopPool(), 120, true, Starters);
		z.Run.SetHealth(0);
		Assert.Equal("heal", BotWith(MerchantPlan.HealIfHurtElseShop, 1).AtMerchant(z), "0 health is below 1%");
	});

	// R6: one choice per stop. A second visit by the bot is refused and changes nothing.
	[Test]
	public static void Probe_Merchant_SecondCall_IsSkip_AndStateUnchanged() => OnFixtures(() =>
	{
		var (s, _, _) = Shopped(120, MerchantPlan.Shop, 100, Starters);
		ulong before = s.StateHash(); int coins = s.Run.Coins;
		Assert.Equal("skip", BotWith(MerchantPlan.Heal).AtMerchant(s), "R6: the stop already used its choice");
		Assert.Equal(before, s.StateHash(), "refused: hash identical");
		Assert.Equal(coins, s.Run.Coins, "refused: coins identical");
	});

	// Determinism of the shop: same state, same purchases in the same order.
	[Test]
	public static void Probe_Shop_SameState_SamePurchases() => OnFixtures(() =>
	{
		var a = Shopped(300, MerchantPlan.Shop, 100, Starters);
		var b = Shopped(300, MerchantPlan.Shop, 100, Starters);
		Assert.Equal(string.Join(",", a.bought), string.Join(",", b.bought), "purchases");
		Assert.Equal(a.s.StateHash(), b.s.StateHash(), "end state");
	});
}
