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
/// YOK-49: the same setup + recorded inputs replay to an identical fight (per-tick StateHash, AI outputs, result),
/// through the file format; a one-tick input change diverges; changed data fails clearly at setup.
/// </summary>
public static class ReplayTests
{
	static string RepoRoot => Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "..");
	static ReplayCatalog? _catalog;
	static ReplayCatalog Catalog => _catalog ??= ReplayCatalog.FromRepo(RepoRoot);
	static BehaviourProfile Profile(string id) => BehaviourProfile.LoadFile(ProjectSettings.GlobalizePath(FightScene.FixtureProfilesDir) + "/" + id + ".json");

	/// <summary>Plays a match the way FightScene does (P2 = AI when given) and records it.</summary>
	static ReplayRecording Play(Match m, Func<int, Match, FighterInput> p1, Func<int, Match, FighterInput>? p2, int maxSteps,
		ProfileAi? ai = null, uint aiSeed = 0, RunState? run = null, bool stopAtOver = true)
	{
		var rec = ReplayRecorder.Begin(m, ai, aiSeed, run);
		for (int t = 0; t < maxSteps; t++)
		{
			var a = p1(t, m);
			var b = ai is not null ? ai.Next(AiView.Capture(m)) : p2!(t, m);
			m.Step(a, b);
			rec.Record(a, b, m.StateHash());
			if (stopAtOver && m.Phase == MatchPhase.Over) break;
		}
		return rec.Finish(m);
	}

	/// <summary>Round-trips through the file format, then replays and requires an exact match.</summary>
	static ReplayReport AssertReplays(ReplayRecording rec, string what)
	{
		var back = ReplayRecording.FromBytes(rec.ToBytes());
		Assert.Equal(rec.Steps, back.Steps, $"{what}: steps survive the file");
		var r = Replayer.Run(back, Catalog);
		Assert.True(r.Ok, $"{what}: {r}");
		Assert.Equal(rec.Steps, r.StepsRun, $"{what}: every step replayed");
		GD.Print($"  {what}: {r} ({back.ToBytes().Length} bytes)");
		return r;
	}

	/// <summary>Raw bits plus direct special/EX requests, LP+LK throws, punch pairs (burst) and jump-ins.</summary>
	static Func<int, Match, FighterInput> Chaos(int seed)
	{
		var rng = new Random(seed);
		var buttons = new[] { InputBits.LightPunch, InputBits.MediumPunch, InputBits.HeavyPunch, InputBits.LightKick, InputBits.MediumKick, InputBits.HeavyKick, InputBits.Special };
		return (_, _) =>
		{
			var bits = (InputBits)0;
			int d = rng.Next(9);
			if (d is 0 or 1 or 2) bits |= InputBits.Right; else if (d is 3 or 4) bits |= InputBits.Left;
			if (rng.Next(6) == 0) bits |= InputBits.Up;
			if (rng.Next(6) == 0) bits |= InputBits.Down;
			if (rng.Next(4) == 0) bits |= buttons[rng.Next(buttons.Length)];
			int k = rng.Next(40);
			if (k == 0) return FighterInput.Special((SpecialSlot)(1 + rng.Next(4)), rng.Next(3) == 0, bits);
			if (k == 1) bits |= InputBits.LightPunch | InputBits.LightKick;
			if (k == 2) bits |= InputBits.LightPunch | InputBits.MediumPunch;
			return new FighterInput(bits);
		};
	}

	/// <summary>Walk in, heavy punch when close (P2 stands still): a KO in a few hundred ticks.</summary>
	static FighterInput Bully(int t, Match m)
	{
		int dist = Math.Abs(m.P2.X - m.P1.X);
		if (dist > 12000) return new FighterInput(m.P1.X < m.P2.X ? InputBits.Right : InputBits.Left);
		return t % 20 == 0 ? new FighterInput(InputBits.HeavyPunch) : FighterInput.None;
	}

	[Test]
	public static void ScriptedFightToKo_ReplaysIdentically()
	{
		FightScene.Run = null;
		var rec = Play(FightScene.NewMatch(), Bully, (_, _) => FighterInput.None, 6000);
		Assert.Equal("Over", rec.Header.Result!.Phase, "the scripted fight reaches a KO");
		Assert.Equal(0, rec.Header.Result.Winner, "P1 wins");
		var r = AssertReplays(rec, "scripted KO");
		Assert.Equal(0, r.Result!.Winner, "replay winner");
	}

	[Test]
	public static void RandomSpecialsThrowsBurstAirNormals_ReplayIdentically()
	{
		var saved = FightScene.Sources;
		FightScene.Sources = FightScene.FixtureSources; // YOK-56: never depend on what is in data/ (coverage needs the fixture kit)
		try { RandomCoverage(); }
		finally { FightScene.Sources = saved; }
	}

	static void RandomCoverage()
	{
		FightScene.Run = null;
		foreach (var (seed, s1, s2) in new[] { (1, ControlScheme.Kata, ControlScheme.Kihon), (2, ControlScheme.Kihon, ControlScheme.Kata), (3, ControlScheme.Kata, ControlScheme.Kata) })
		{
			var m = FightScene.NewMatch();
			m.P1.Input.Scheme = s1;
			m.P2.Input.Scheme = s2;
			bool proj = false, air = false, thr = false, burst = false, special = false;
			var p1 = Chaos(seed * 10 + 1);
			var p2 = Chaos(seed * 10 + 2);
			var rec = Play(m, (t, mm) =>
			{
				foreach (var f in mm.Fighters)
				{
					proj |= mm.Projectiles.Count > 0;
					air |= f.State == FighterState.Attack && f.ActiveMove?.Air == true;
					thr |= f.State == FighterState.Attack && f.ActiveMove?.IsThrow == true;
					special |= f.State == FighterState.Attack && f.ActiveSpecial != SpecialSlot.None;
					burst |= f.BurstUsed;
				}
				return p1(t, mm);
			}, p2, 5400, stopAtOver: false);
			Assert.True(proj && air && thr && burst && special, $"seed {seed} covers projectile {proj} air {air} throw {thr} burst {burst} special {special}");
			AssertReplays(rec, $"random seed {seed} ({s1} vs {s2})");
		}
	}

	[Test]
	public static void AiVsRandomP1_ReplaysAndAiIsReRun()
	{
		FightScene.Run = null;
		foreach (var id in new[] { "test-kitsune-aggressive", "test-kitsune-patient" })
		{
			var m = FightScene.NewMatch();
			var ai = new ProfileAi(Profile(id), 1, AiLoadout.From(m.P2), 27);
			var rec = Play(m, Chaos(id.Length), null, 5400, ai, 27, stopAtOver: false);
			var r = AssertReplays(rec, $"AI {id} vs random");
			Assert.Equal(rec.Steps, r.AiStepsChecked, "every P2 input re-derived by the AI");

			// The recorded AI output is checked: tamper with one P2 tick and the replay names it.
			var bad = ReplayRecording.FromBytes(rec.ToBytes());
			bad.P2[300] = new FighterInput(bad.P2[300].Bits ^ InputBits.HeavyKick);
			var rb = Replayer.Run(bad, Catalog);
			Assert.True(!rb.Ok && rb.FirstAiMismatchStep == 300, $"tampered AI input reported at 300: {rb}");
			Assert.Equal(-1, rb.FirstDivergentStep, "the AI re-run, not the tampered input, drives P2 (state still identical)");
		}
	}

	[Test]
	public static void RematchAfterRewardPick_CarriesRunState()
	{
		var pool = AbilityPool.Load(
			Path.Combine(RepoRoot, "game", "tests", "fixtures", "none-moves"), Path.Combine(RepoRoot, "game", "tests", "fixtures", "none-modifiers"),
			Path.Combine(RepoRoot, "game", "tests", "fixtures", "specials"), Path.Combine(RepoRoot, "game", "tests", "fixtures", "rewards"));
		var run = RunState.NewRun(pool);
		try
		{
			FightScene.Run = run;
			var first = Play(FightScene.NewMatch(), Bully, (_, _) => FighterInput.None, 6000, run: run);
			AssertReplays(first, "duel 1");
			run.AfterDuel(first.Header.Result!.P1Health);

			// Pick NEW (Foxfire, slot C), then a modifier on the next draft: levels, slots and a modifier carried.
			var d1 = RewardDraft.Build(run, pool, "kitsune", 5);
			var pick = d1.Pick(d1.Offer.Cards.First(c => c.Kind == CardKind.NewSpecial).Index);
			Assert.True(pick.Ok, "picked the new special: " + pick.Error);
			var d2 = RewardDraft.Build(run, pool, "kitsune", 6);
			var mod = d2.Offer.Cards.First(c => c.Kind == CardKind.Modifier);
			Assert.True(d2.Pick(mod.Index, mod.Targets[0].Slot).Ok, "picked the modifier");
			run.AfterDuel(700);

			var m = FightScene.NewMatch();
			var ai = new ProfileAi(Profile("test-kitsune-aggressive"), 1, AiLoadout.From(m.P2), 27);
			var rec = Play(m, Chaos(44), null, 5400, ai, 27, run, stopAtOver: false);
			var h = rec.Header;
			Assert.Equal(run.Health, h.P1StartHealth, "carried health recorded");
			Assert.True(h.Fighters[0].Specials.Any(s => s.Slot == (int)SpecialSlot.C), "the drafted slot C special is recorded");
			Assert.True(h.Fighters[0].Specials.Any(s => s.Modifier is not null), "the modifier is recorded");
			Assert.Equal(run.StateHash().ToString("x16"), h.RunHash, "run hash stamped");
			AssertReplays(rec, "rematch after reward picks");
		}
		finally { FightScene.Run = null; }
	}

	[Test]
	public static void OneTickInputChange_DivergesAtThatTick()
	{
		FightScene.Run = null;
		var rec = Play(FightScene.NewMatch(), Bully, (_, _) => FighterInput.None, 6000);
		var bad = ReplayRecording.FromBytes(rec.ToBytes());
		Assert.True(bad.P1[10].Has(InputBits.Right) || bad.P1[10].Has(InputBits.Left), "P1 is walking at step 10");
		bad.P1[10] = FighterInput.None;
		var r = Replayer.Run(bad, Catalog);
		Assert.True(!r.Ok, "a one-tick change fails the replay");
		Assert.Equal(10, r.FirstDivergentStep, "the hash diverges on the changed tick");
	}

	[Test]
	public static void ChangedOrMissingData_FailsClearlyAtSetup()
	{
		FightScene.Run = null;
		var rec = Play(FightScene.NewMatch(), Bully, (_, _) => FighterInput.None, 120, stopAtOver: false);
		var changed = ReplayRecording.FromBytes(rec.ToBytes());
		var mv = changed.Header.Fighters[0].Moves[0];
		mv.Fp = "0000000000000000";
		var r = Replayer.Run(changed, Catalog);
		Assert.True(!r.Ok && r.Error!.Contains($"'{mv.Id}' changed"), "changed move named: " + r.Error);
		Assert.Equal(0, r.StepsRun, "nothing replays on a setup failure");

		var missing = ReplayRecording.FromBytes(rec.ToBytes());
		missing.Header.Fighters[1].Specials[0].Id = "no-such-special";
		r = Replayer.Run(missing, Catalog);
		Assert.True(!r.Ok && r.Error!.Contains("'no-such-special'") && r.Error.Contains("no longer exists"), "missing special named: " + r.Error);

		bool threw = false;
		try { ReplayRecording.FromBytes(new byte[] { 1, 2, 3, 4, 5 }); } catch (FormatException) { threw = true; }
		Assert.True(threw, "a non-replay file is rejected");
	}

	[Test]
	public static void Scene_RecordsAndF8SavesAReplayableFile(Node runner)
	{
		FightScene.Run = null;
		var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		scene.ExternalDrive = true;
		runner.AddChild(scene);
		var p1 = Chaos(7);
		for (int t = 0; t < 900; t++) scene.StepWithAi(p1(t, scene.Match));
		scene.ToggleScheme(0);                       // F4 mid-fight
		for (int t = 0; t < 300; t++) scene.StepWithAi(p1(t, scene.Match));
		scene.SwapTemperament();                     // F6 mid-fight: AI rebuilt
		for (int t = 0; t < 300; t++) scene.StepWithAi(p1(t, scene.Match));
		scene.ToggleP2Ai();                          // F7: P2 to devices (recorded input)
		for (int t = 0; t < 120; t++) scene.StepWithAi(p1(t, scene.Match));
		string path = scene.SaveReplay(Path.Combine(OS.GetUserDataDir(), "replays", "test-scene.yfr"));
		var rec = ReplayRecording.Load(path);
		Assert.Equal(1620, rec.Steps, "every scene step recorded");
		Assert.Equal(3, rec.Header.Events.Count, "scheme, AI swap, AI off (and nothing else) recorded as events");
		AssertReplays(rec, "scene with F4/F6/F7 mid-fight");

		scene.ResetFight();                          // a reset starts a new recording
		Assert.Equal(0, scene.Recorder!.Recording.Steps, "reset starts a fresh recording");
		scene.QueueFree();
	}
}
