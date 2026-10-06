using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Godot;
using YokaiFighters.Ai;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>YOK-27: behaviour-profile AI (Y1, Y2, Y4, P6).</summary>
public static class AiTests
{
	static string FixtureDir => ProjectSettings.GlobalizePath(FightScene.FixtureProfilesDir);
	static BehaviourProfile Fixture(string id) => BehaviourProfile.LoadFile(Path.Combine(FixtureDir, id + ".json"));
	static BehaviourProfile Patient => Fixture("test-kitsune-patient");
	static BehaviourProfile Aggressive => Fixture("test-kitsune-aggressive");

	/// <summary>A profile that anti-airs every jump it sees (rate 100) and otherwise only spaces.</summary>
	static string AntiAirJson(int frames) => $$"""
		{ "kind": "profile", "id": "test-anti-air", "yokai": "kitsune", "temperament": "patient",
		  "reaction": { "tier": "yokai", "frames": {{frames}} }, "aggression_bias": 50, "preferred_range": "mid",
		  "habit": { "id": "jumps-after-knockdown", "description": "d", "when": "self_got_up", "do": "jump", "chance_pct": 60 },
		  "behaviours": [ { "when": "opponent_jumping", "do": "normal:test-ryo-heavy-punch", "weight": 100 } ],
		  "rules": ["Y4"] }
		""";

	[Test]
	public static void Profiles_LoadFromData()
	{
		var p = Patient;
		Assert.Equal(22, p.ReactionFrames, "yokai tier = 22 frames (Y4)");
		Assert.Equal(Observation.SelfGotUp, p.Habit.When, "habit trigger");
		Assert.Equal(ActionKind.Jump, p.Habit.Do.Kind, "habit action");
		Assert.Equal(60, p.Habit.ChancePct, "habit chance");
		Assert.Equal(Observation.AtFullScreen, p.PreferredRange, "patient holds full screen");
		Assert.Equal("aggressive", Aggressive.Temperament, "aggressive fixture");
		Assert.True(Aggressive.AggressionBias > p.AggressionBias, "aggressive leans in");
		var sample = BehaviourProfile.LoadFile(ProjectSettings.GlobalizePath("res://") + "../data/samples/kitsune-patient.json");
		Assert.Equal("kitsune-patient", sample.Id, "the schema sample loads too");
		foreach (var bad in new[] { ("\"do\": \"jump\"", "\"do\": \"press_lp\""), ("\"when\": \"self_got_up\"", "\"when\": \"p1_pressed_hp\"") })
		{
			bool threw = false;
			try { BehaviourProfile.Parse(AntiAirJson(22).Replace(bad.Item1, bad.Item2)); } catch (FormatException) { threw = true; }
			Assert.True(threw, $"rejects {bad.Item2}");
		}
	}

	/// <summary>P6 by construction: the AI only ever gets AiView, which carries no input, parser or match.</summary>
	[Test]
	public static void View_HasNoInputOrMatchAccess()
	{
		var banned = new[] { typeof(Match), typeof(Fighter), typeof(InputReader), typeof(InputBuffer), typeof(FighterInput), typeof(InputBits), typeof(InputCommand) };
		foreach (var t in new[] { typeof(AiView), typeof(FighterView), typeof(ProjectileView), typeof(ProfileAi), typeof(AiLoadout) })
		foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
		{
			bool loadoutInputs = t == typeof(AiLoadout); // its own slot requests/throw buttons, built once from the kit
			if (loadoutInputs) continue;
			Assert.True(Array.IndexOf(banned, f.FieldType) < 0, $"{t.Name}.{f.Name} is {f.FieldType.Name}");
		}
	}

	static List<FighterInput> RunAi(BehaviourProfile profile, Func<int, FighterInput> p1, int steps, uint seed = 5)
	{
		var m = FightScene.NewMatch();
		var ai = new ProfileAi(profile, 1, AiLoadout.From(m.P2), seed);
		var outs = new List<FighterInput>();
		for (int k = 1; k <= steps && m.Phase != MatchPhase.Over; k++)
		{
			var o = ai.Next(AiView.Capture(m));
			outs.Add(o);
			m.Step(p1(k), o);
		}
		return outs;
	}

	/// <summary>Acceptance: P1 streams identical until tick t give identical AI output until t + reaction frames.</summary>
	[Test]
	public static void Ai_NeverSeesInputBeforeItShows()
	{
		int diverged = 0;
		foreach (int frames in new[] { 15, 22, 30 })
		foreach (int t in new[] { 30, 90, 200 })
		foreach (var prof in new[] { Patient, Aggressive })
		{
			var profile = prof with { ReactionFrames = frames };
			FighterInput StreamA(int k) => k <= t ? Scripted(k, 1) : Scripted(k, 2);
			FighterInput StreamB(int k) => k <= t ? Scripted(k, 1) : Scripted(k, 3);
			var a = RunAi(profile, StreamA, t + 160);
			var b = RunAi(profile, StreamB, t + 160);
			for (int k = 0; k < Math.Min(t + frames, Math.Min(a.Count, b.Count)); k++)
				Assert.Equal(a[k], b[k], $"{profile.Id} r{frames} t{t}: AI output on tick {k + 1} (before t + {frames})");
			for (int k = t + frames; k < Math.Min(a.Count, b.Count); k++)
				if (a[k] != b[k]) { diverged++; break; }
		}
		Assert.True(diverged >= 6, $"the P1 change does reach the AI after its reaction delay ({diverged}/18 runs diverged)");
	}

	/// <summary>Deterministic per-tick P1 noise: hash of (tick, stream) picks bits.</summary>
	static FighterInput Scripted(int k, int stream)
	{
		uint h = (uint)(k / 6) * 2654435761u ^ (uint)stream * 40503u;
		h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
		InputBits[] pool = { InputBits.None, InputBits.Right, InputBits.Left, InputBits.Up, InputBits.Down, InputBits.LightPunch, InputBits.HeavyKick, InputBits.Right | InputBits.Up };
		var bits = pool[h % (uint)pool.Length];
		if (k % 6 != 0) bits &= ~InputBits.Attacks; // buttons pressed once per held chunk
		return new FighterInput(bits);
	}

	/// <summary>Acceptance: reaction frames from data change the measured response time.</summary>
	[Test]
	public static void Reaction_FramesFromDataSetResponseTime()
	{
		double prev = double.MaxValue;
		foreach (int frames in new[] { 30, 22, 18, 15 })
		{
			var profile = BehaviourProfile.Parse(AntiAirJson(frames)); // changed in the data, not in code
			var r = AiHarness.Run(FightScene.NewMatch, profile, P1Script.Jumper, 11, 2400);
			Assert.True(r.AntiAirResponse.Count >= 15, $"r{frames}: answered {r.AntiAirResponse.Count} of {r.P1Jumps} jumps");
			foreach (int resp in r.AntiAirResponse) Assert.Equal(frames, resp, $"r{frames}: anti-air starts exactly reaction frames after the jump shows");
			Assert.True(r.MeanResponse < prev, $"faster tier answers faster ({r.MeanResponse} vs {prev})");
			prev = r.MeanResponse;
		}
	}

	/// <summary>Acceptance: the Kitsune tell (jump on wake-up) shows in the logs at its data rate, and covering hides it.</summary>
	[Test]
	public static void Habit_WakeUpJumpIsVisibleAndRepeatable()
	{
		var r = AiHarness.Run(FightScene.NewMatch, Patient, P1Script.Thrower, 3, 7200);
		Assert.True(r.WakeUps >= 20, $"enough knockdowns to read the tell ({r.WakeUps})");
		int pct = 100 * r.WakeJumps / r.WakeUps;
		Assert.True(pct is >= 40 and <= 80, $"wake-up jump rate {pct}% near the profile's 60% ({r.WakeJumps}/{r.WakeUps})");
		// Every fired habit is a jump on the first actionable frame (bar a KO knockdown with no wake-up), and only those.
		Assert.True(r.WakeJumps <= r.HabitFired && r.WakeJumps >= r.HabitFired - r.Rounds, $"wake jumps {r.WakeJumps} = habits fired {r.HabitFired}");
		Assert.True(r.WakeJumpsPunished * 10 >= r.WakeJumps * 9, $"punishable: a tester who knows it anti-airs {r.WakeJumpsPunished}/{r.WakeJumps}");
		var again = AiHarness.Run(FightScene.NewMatch, Patient, P1Script.Thrower, 3, 7200);
		Assert.Equal(r.DecisionsCsv(), again.DecisionsCsv(), "same seed, same log");

		var covered = AiHarness.Run(FightScene.NewMatch, Patient with { TellCoverPct = 100 }, P1Script.Thrower, 3, 3600);
		Assert.True(covered.HabitCovered > 0 && covered.WakeJumps == 0, $"tell_cover_pct 100 hides the tell ({covered.HabitCovered} covered, {covered.WakeJumps} jumps)");
	}

	/// <summary>The habit mechanism is generic: any trigger → action → chance from data.</summary>
	[Test]
	public static void Habit_IsGenericTriggerActionChance()
	{
		var p = BehaviourProfile.Parse(AntiAirJson(18).Replace("\"when\": \"self_got_up\", \"do\": \"jump\", \"chance_pct\": 60",
			"\"when\": \"opponent_jumping\", \"do\": \"dash_back\", \"chance_pct\": 100"));
		var r = AiHarness.Run(FightScene.NewMatch, p, P1Script.Jumper, 2, 1200);
		int habits = r.Decisions.FindAll(d => d.Kind == "habit" && d.Trigger == "opponent_jumping" && d.Action == "dash_back").Count;
		Assert.True(habits >= 10 && habits == r.P1Jumps, $"dash back on every seen jump ({habits}/{r.P1Jumps})");
		Assert.Equal(0, r.AntiAirResponse.Count, "the habit outranks the anti-air reaction");
	}

	[Test]
	public static void Ai_IsDeterministic()
	{
		foreach (var script in new[] { P1Script.Random, P1Script.Thrower })
		{
			var a = AiHarness.Run(FightScene.NewMatch, Aggressive, script, 9, 5000);
			var b = AiHarness.Run(FightScene.NewMatch, Aggressive, script, 9, 5000);
			var c = AiHarness.Run(FightScene.NewMatch, Aggressive, script, 10, 5000);
			Assert.Equal(a.FinalHash, b.FinalHash, $"{script}: same seed, same fight");
			Assert.Equal(a.DecisionsCsv(), b.DecisionsCsv(), $"{script}: same decisions");
			Assert.True(a.DecisionsCsv() != c.DecisionsCsv(), $"{script}: another seed plays differently");
		}
	}

	/// <summary>Temperaments differ in play: aggressive closes in and attacks more than patient.</summary>
	[Test]
	public static void Temperaments_PlayDifferently()
	{
		int Offence(AiHarness.Result r) => r.Decisions.FindAll(d => d.Action.StartsWith("normal:") || d.Action.StartsWith("special:") || d.Action == "throw" || d.Action.StartsWith("dash_forward") || d.Action == "walk_forward").Count;
		var agg = AiHarness.Run(FightScene.NewMatch, Aggressive, P1Script.Random, 4, 3600);
		var pat = AiHarness.Run(FightScene.NewMatch, Patient, P1Script.Random, 4, 3600);
		Assert.True(Offence(agg) > Offence(pat), $"aggressive {Offence(agg)} offensive decisions vs patient {Offence(pat)}");
	}

	[Test]
	public static void Scene_P2IsAiByDefaultAndSwaps(Node runner)
	{
		var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		scene.ExternalDrive = true;
		runner.AddChild(scene);
		Assert.True(scene.P2Ai is not null, "P2 is AI-driven by default");
		string first = scene.P2Ai!.Profile.Temperament;
		scene.SwapTemperament();
		Assert.True(scene.P2Ai!.Profile.Temperament != first, "swap changes temperament");
		int x0 = scene.Match.P2.X;
		for (int t = 0; t < 240; t++) scene.StepWithAi(FighterInput.None);
		Assert.True(scene.Match.P2.X != x0 || scene.Match.Projectiles.Count > 0 || scene.Match.P2.State != FighterState.Idle, "the AI moves P2");
		scene.ToggleP2Ai();
		Assert.True(scene.P2Ai is null, "F7 hands P2 back to the devices");
		scene.QueueFree();
	}
}
