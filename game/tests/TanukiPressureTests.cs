using System;
using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.Ai;
using YokaiFighters.Fight;
using YokaiFighters.Replay;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-99, Y5: each Tanuki win (max 3) makes yokai react 1 frame faster and pushes them toward aggressive.
/// The pressure's numbers, the AI that applies it, the replay that records it and the run end that counts a win.
/// The save that keeps the count across runs (version 7, <c>tanuki_wins</c>) and the boot flow that hands it to the
/// next run are in <c>SaveTests</c> (the only test files allowed to name the save store).
/// </summary>
public static class TanukiPressureTests
{
	static string FixtureDir => ProjectSettings.GlobalizePath(FightScene.FixtureProfilesDir);
	static BehaviourProfile Patient => BehaviourProfile.LoadFile(Path.Combine(FixtureDir, "test-kitsune-patient.json"));
	static string MapDir => ContentPaths.Data("map");

	static Match NewMatch()
	{
		var saved = FightScene.Sources;
		FightScene.Sources = FightScene.FixtureSources;
		try { return FightScene.NewMatch(); }
		finally { FightScene.Sources = saved; }
	}

	static void WithFixtures(Action body)
	{
		var saved = FightScene.Sources;
		FightScene.Sources = FightScene.FixtureSources;
		try { body(); }
		finally { FightScene.Sources = saved; }
	}

	// ---- the numbers ----

	[Test]
	public static void Pressure_OneFrameFasterPerWin_StopsAtThreeWins()
	{
		int[] expected = { 22, 21, 20, 19, 19, 19 };
		for (int wins = 0; wins < expected.Length; wins++)
		{
			var p = new TanukiPressure(wins);
			Assert.Equal(expected[wins], p.ReactionFrames(22), $"Y5: yokai tier 22f after {wins} Tanuki wins");
			Assert.Equal(Math.Min(wins, 3) * TanukiPressure.AggressionPerWin, p.AggressionBonus, $"aggression bonus after {wins} wins");
		}
		Assert.Equal(15 - 3, new TanukiPressure(3).ReactionFrames(15), "Tanuki tier 15f after three wins (Y4 + Y5)");
		Assert.Equal(22, new TanukiPressure(-4).ReactionFrames(22), "a negative count is no pressure");
		Assert.Equal(0, new TanukiPressure(-4).AggressionBonus, "a negative count is no aggression");
		Assert.Equal(1, new TanukiPressure(3).ReactionFrames(2), "never below one frame");
		Assert.Equal(1, new TanukiPressure(int.MaxValue).ReactionFrames(1), "a huge count is still three steps, one frame floor");
		Assert.Equal(0, TanukiPressure.None.Steps, "None = no pressure");
	}

	// ---- the AI ----

	[Test]
	public static void ProfileAi_AggressionBonus_ShiftsNeutralWeights_TowardOffence()
	{
		var calm = AiHarness.Run(NewMatch, Patient, P1Script.Idle, 4, 1800);
		var pushed = AiHarness.Run(NewMatch, Patient, P1Script.Idle, 4, 1800, aggressionBonus: 3 * TanukiPressure.AggressionPerWin);
		// test-kitsune-patient: aggression_bias 25. walk_back (defensive, weight 20): 20 × (150 − bias) / 100.
		int Weight(AiHarness.Result r, string action) =>
			r.Decisions.Where(d => d.Kind == "neutral" && d.Action == action).Select(d => d.Pct).Distinct().Single();
		Assert.Equal(20 * (150 - 25) / 100, Weight(calm, "walk_back"), "no pressure: the profile's own bias");
		Assert.Equal(20 * (150 - 55) / 100, Weight(pushed, "walk_back"), "three wins: bias 25 + 30 makes backing off rarer");
		Assert.True(calm.FinalHash != pushed.FinalHash, "a different fight");
	}

	[Test]
	public static void ProfileAi_Aggression_IsCappedAt100()
	{
		var kit = AiLoadout.From(NewMatch().P2);
		Assert.Equal(55, new ProfileAi(Patient, 1, kit, 1, aggressionBonus: 30).Aggression, "25 + 30");
		Assert.Equal(100, new ProfileAi(Patient, 1, kit, 1, aggressionBonus: 500).Aggression, "capped at 100");
		Assert.Equal(25, new ProfileAi(Patient, 1, kit, 1).Aggression, "no bonus: the profile's bias");
	}

	[Test]
	public static void Replay_RecordsTheBonus_AndLeavesItOutWhenZero()
	{
		var kit = AiLoadout.From(NewMatch().P2);
		var with = ReplayRecorder.AiOf(new ProfileAi(Patient, 1, kit, 9, 20, AiFightTier.Standard, 20), 9)!;
		Assert.True(with.AggressionBonus == 20 && with.ReactionFrames == 20, "bonus and pressured frames recorded");
		string none = System.Text.Json.JsonSerializer.Serialize(ReplayRecorder.AiOf(new ProfileAi(Patient, 1, kit, 9), 9));
		Assert.True(!none.Contains("AggressionBonus"), "no pressure: the field is left out, so old recordings read the same: " + none);
	}

	// ---- which fights ----

	[Test]
	public static void Apprentice_IsNeverPressed_OtherNodesAre()
	{
		var graph = FileMapSource.Default(MapDir).Load();
		var table = NodeSceneTable.Default(MapDir);
		NodeContext Ctx(string id) => table.Resolve(graph.Node(id)).Context;
		var kit = AiLoadout.From(NewMatch().P2);

		var apprentice = Ctx("r1-apprentice");
		Assert.True(apprentice.Tier == FightScene.ApprenticeTier && apprentice.ReactionFrames == 30, "row 1 is the Y4 apprentice at 30f");
		var p1 = FightScene.PressureFor(apprentice, 3);
		var ai = new ProfileAi(Patient, 1, kit, 1, p1.ReactionFrames(apprentice.ReactionFrames), AiFightTier.Standard, p1.AggressionBonus);
		Assert.True(ai.ReactionFrames == 30 && ai.AggressionBonus == 0, "Y4/Y6: three wins leave the guided apprentice fight at 30f, no bonus");

		var yokai = Ctx("r2-kitsune");
		var p2 = FightScene.PressureFor(yokai, 3);
		Assert.True(p2.ReactionFrames(yokai.ReactionFrames) == yokai.ReactionFrames - 3 && p2.AggressionBonus == 3 * TanukiPressure.AggressionPerWin, "a row-2 yokai is pressed");
		var boss = Ctx("boss-tanuki");
		Assert.Equal(boss.ReactionFrames - 3, FightScene.PressureFor(boss, 3).ReactionFrames(boss.ReactionFrames), "the Tanuki is pressed (reading flagged to the designer)");
		Assert.Equal(3, FightScene.PressureFor(null, 3).Steps, "no node (the scene on its own): pressed as asked");
	}

	// ---- the run end ----

	[Test]
	public static void RunEnd_BeatTanuki_OnlyForAWinOnTheTanukisNode() => WithFixtures(() =>
	{
		var pool = FightScene.LoadAbilityPool();
		var graph = FileMapSource.Default(MapDir).Load();
		var table = NodeSceneTable.Default(MapDir);

		RunSession Walk(uint seed, bool toBoss)
		{
			var s = RunSession.Start(pool, graph, table, seed);
			while (!toBoss || s.Reachable()[0] != "boss-tanuki")
			{
				Assert.True(s.Enter(s.Reachable()[0]).Ok, "entered");
				if (!toBoss) return s;
				if (s.WinFight(700) is { } d) Assert.True(d.Pick(0, d.Offer.Cards[0].Targets.Count > 0 ? d.Offer.Cards[0].Targets[0].Slot : null).Ok, "picked");
				Assert.True(s.CompleteNode().Ok, "completed");
			}
			Assert.True(s.Enter("boss-tanuki").Ok, "entered the Tanuki's node");
			return s;
		}

		var won = Walk(3, toBoss: true);
		won.WinFight(400);
		Assert.True(won.CompleteNode().Ok && won.Ended is { Won: true, AtTanuki: true, BeatTanuki: true }, "beat the Tanuki");

		var lostAtBoss = Walk(3, toBoss: true);
		lostAtBoss.LoseFight();
		Assert.True(lostAtBoss.Ended is { Won: false, AtTanuki: true, BeatTanuki: false }, "lost to the Tanuki: no win");

		var lostEarly = Walk(3, toBoss: false);
		lostEarly.LoseFight();
		Assert.True(lostEarly.Ended is { AtTanuki: false, BeatTanuki: false }, "lost on row 1");
	});
}
