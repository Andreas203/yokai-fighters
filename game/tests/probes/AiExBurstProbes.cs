using System;
using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.Ai;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>Test Breaker probes for YOK-57 (E22, E13, C6, E16, C5, T4, P6). Expected values come from rules.md.</summary>
public static class AiExBurstProbes
{
	static string Root => Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "..");
	static BehaviourProfile Fx(string id) =>
		BehaviourProfile.LoadFile(Path.Combine(ProjectSettings.GlobalizePath(FightScene.FixtureProfilesDir), id + ".json"));
	static Match NewMatch()
	{
		var saved = FightScene.Sources;
		FightScene.Sources = FightScene.FixtureSources;
		try { return FightScene.NewMatch(); } finally { FightScene.Sources = saved; }
	}

	// E13/C6: a burst press that the sim does not turn into a burst would be read as three punches (a normal or a throw, E12).
	[Test]
	public static void BurstPress_AlwaysBecomesBurst_NotANormal()
	{
		var p = Fx("test-kitsune-aggressive") with { BurstUse = new BurstUse(100) };
		int pressed = 0, fired = 0;
		foreach (var script in new[] { P1Script.Random, P1Script.Thrower, P1Script.Jumper })
			for (uint seed = 1; seed <= 12; seed++)
			{
				var m = NewMatch();
				var ai = new ProfileAi(p, 1, AiLoadout.From(m.P2), seed, tier: AiFightTier.Elder);
				var rng = new SimRng(seed * 31);
				InputBits held = InputBits.None; int hold = 0;
				int pendingPress = -1;
				m.BurstFired += (_, i) => { if (i == 1) { fired++; pendingPress = -1; } };
				for (int k = 0; k < 3000; k++)
				{
					var o = ai.Next(AiView.Capture(m));
					bool press = (o.Bits & Match.BurstButtons) == Match.BurstButtons;
					if (press) { pressed++; pendingPress = k; }
					var p1 = script == P1Script.Random ? AiHarness.RandomInput(rng, ref held, ref hold)
						: script == P1Script.Thrower ? AiHarness.Thrower(m) : FighterInput.None;
					m.Step(p1, o);
					if (pendingPress >= 0 && k - pendingPress >= 3)
					{
						Assert.True(false, $"{script} seed {seed}: burst press at tick {pendingPress} produced no burst; P2 state {m.P2.State}, move {m.P2.CurrentMove?.Id}");
					}
					if (m.Phase == MatchPhase.Over) { m.Reset(); ai = new ProfileAi(p, 1, AiLoadout.From(m.P2), seed + 7919u, tier: AiFightTier.Elder); pendingPress = -1; }
				}
			}
		Assert.True(pressed > 0 && fired > 0, "sanity: bursts happened");
	}

	// E22: tier by node. Boss type = Tanuki, elder = Elder, yokai/apprentice rows = Standard.
	[Test]
	public static void TierOf_ByNode()
	{
		NodeContext N(string type, bool elder, string tier) => new("n", type, 1, "kitsune", elder, "kitsune", null, tier, 22, false, false,
			new System.Collections.Generic.Dictionary<string, System.Text.Json.JsonElement>(), new string[0]);
		Assert.Equal(AiFightTier.Standard, FightScene.TierOf(N("fight", false, "yokai")), "yokai fight");
		Assert.Equal(AiFightTier.Standard, FightScene.TierOf(N("fight", false, "apprentice")), "apprentice fight");
		Assert.Equal(AiFightTier.Elder, FightScene.TierOf(N("fight", true, "elder")), "elder fight");
		Assert.Equal(AiFightTier.Tanuki, FightScene.TierOf(N(NodeTypeRegistry.Boss, false, "tanuki")), "boss");
	}

	// T4/E16: the Tanuki's EX of its copy runs the copy's Lv 2 move for a Lv 3 copy, and needs the EX meter cost.
	[Test]
	public static void TanukiCopyEx_RunsCappedLevel()
	{
		var src = KitSources.Repo(Root).FixturesOnly();
		var pool = AbilityPool.LoadFixtures(Root);
		foreach (var data in pool.Specials.Where(s => s.HasEx && s.Slot != SpecialSlot.None))
			for (int lv = 1; lv <= Math.Min(3, data.MaxLevel); lv++)
			{
				var copy = new CopyTarget(data.Slot, data, lv);
				var ryo = Kit.Load(Kit.Ryo, src);
				var kit = TanukiKit.Build(ryo, copy);
				var m = new Match(null, ryo.Moves, kit.Moves);
				ryo.Equip(m.P1); kit.Equip(m.P2);
				var load = AiLoadout.From(m.P2);
				Assert.True(load.TryGetEx(BehaviourProfile.ParseAction("special:copied"), out var inp, out int cost), $"{data.Id} Lv{lv}: copied EX available");
				Assert.Equal(data.ExCost, cost, $"{data.Id} Lv{lv}: cost");
				m.P2.Meter = m.Config.MeterMax;
				int level = -1; bool ex = false;
				m.SpecialStarted += (_, e) => { if (e.Fighter == 1) { level = e.Level; ex = e.Ex; } };
				m.Step(FighterInput.None, inp);
				Assert.True(ex, $"{data.Id} Lv{lv}: EX started");
				Assert.True(level <= 2, $"{data.Id} Lv{lv}: runs level {level}, T4 caps at 2");
			}
	}
}
