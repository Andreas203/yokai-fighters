using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Godot;
using YokaiFighters.Ai;
using YokaiFighters.Fight;
using YokaiFighters.Harness;
using YokaiFighters.Replay;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-225: command grabs. The TEST FIXTURE River Grab (6/2/30, 130 damage, <c>properties.throw</c> range 130) sits in
/// P1's slot C and is fired by a direct slot request on tick 1, so its frame n plays on tick n: the grab (first active
/// frame 7) is tick 7, the break window ticks 8-14 and an unbroken grab lands on tick 15 (C4, E12).
/// </summary>
public static class CommandGrabTests
{
	const int S = SimConfig.Scale;
	const int GrabTick = 7, LandTick = 15;
	const InputBits LP = InputBits.LightPunch, LK = InputBits.LightKick, THROW = LP | LK;
	const InputBits MP = InputBits.MediumPunch, HP = InputBits.HeavyPunch, BURST = LP | MP | HP;
	static readonly FighterInput Idle = FighterInput.None;
	static readonly FighterInput Grab = FighterInput.Special(SpecialSlot.C);
	static string Repo => Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "..");
	static string FixturePath => ProjectSettings.GlobalizePath("res://tests/fixtures/command-grabs/test-river-grab.json");
	static string FixtureJson => File.ReadAllText(FixturePath);
	static SpecialData RiverGrab(Action<JsonObject>? edit = null)
	{
		var doc = JsonNode.Parse(FixtureJson)!.AsObject();
		edit?.Invoke(doc);
		return SpecialLoader.Parse(doc.ToJsonString());
	}
	static JsonObject Fd(JsonObject doc) => doc["levels"]!["1"]!["frame_data"]!.AsObject();
	static SpecialData[] Starters() => SpecialLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureSpecialsDir));
	static MoveData[] Moves() =>
		MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureNormalsDir))
			.Concat(MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureThrowsDir))).ToArray();

	sealed class Log
	{
		public readonly List<(int tick, ThrowEvent e)> Throws = new();
		public readonly List<(int tick, HitEvent e)> Hits = new();
		public readonly List<ImpactEvent> Impacts = new();
		public bool Has(ThrowOutcome o) => Throws.Any(t => t.e.Outcome == o);
		public ThrowOutcome[] Outcomes => Throws.Select(t => t.e.Outcome).ToArray();
	}

	/// <summary>Both fighters with the fixture starters; P1 also holds River Grab in slot C. <paramref name="gap"/> = centre distance in units (null = touching).</summary>
	static Match Setup(Log log, int level = 1, int? gap = null, SpecialData? grab = null, ModifierData? mod = null, bool place = true)
	{
		var m = new Match(null, Moves(), Moves());
		foreach (var f in m.Fighters) FightScene.EquipStarters(f, Starters());
		m.P1.Equip(SpecialSlot.C, grab ?? RiverGrab(), level, mod, m.Config);
		int d = gap is int g ? g * S : m.Config.BodyWidth;
		if (place) { m.P1.X = -d / 2; m.P2.X = d - d / 2; }
		m.Throw += (mm, e) => log.Throws.Add((mm.Tick, e));
		m.Hit += (mm, e) => log.Hits.Add((mm.Tick, e));
		m.Impact += (_, e) => log.Impacts.Add(e);
		return m;
	}

	static void Run(Match m, int n, Func<int, FighterInput> p1, Func<int, FighterInput> p2)
	{
		for (int t = 1; t <= n; t++) m.Step(p1(t), p2(t));
	}
	static Func<int, FighterInput> At(int tick, FighterInput input) => t => t == tick ? input : Idle;
	static Func<int, FighterInput> None => _ => Idle;

	static bool Refused(Action a)
	{
		try { a(); return false; }
		catch (FormatException) { return true; }
	}

	// --- Data ------------------------------------------------------------------------------

	[Test]
	public static void Loads_AsACommandGrab_HitboxesBecomeTheGrab()
	{
		var sp = RiverGrab();
		var m = sp.Build(1, false);
		Assert.True(m.IsCommandGrab && m.IsGrab && !m.IsThrow && !m.IsNormal, "a special that is a throw, not the LP+LK throw");
		Assert.Equal((6, 2, 30, 130), (m.Startup, m.Active, m.Recovery, m.Damage), "River Grab 6/2/30, 130");
		Assert.Equal(0, m.Hitboxes.Count, "its hitboxes never strike");
		Assert.Equal(new TimedBox(7, 8, new Box(20, 150, 110, 120)), m.Throwboxes.Single(), "grab box x 20 .. range 130");
		Assert.Equal(MoveData.DefaultBreakWindow, m.BreakWindow, "C4: break window 7 when the data gives none");
		Assert.Equal(7, m.BreakWindow, "C4");
		Assert.True(m.Breakable, "breakable");
		Assert.Equal(THROW, m.BreakButtons, "E12: LP+LK breaks it");
		Assert.Equal(HitStrength.Heavy, m.Strength, "E12: lands as a heavy hit");
		Assert.Equal(130, sp.Build(2, false).GrabRange, "Lv 2 keeps the range");
		var lv3 = sp.Build(3, false);
		Assert.Equal(195, lv3.GrabRange, "Whirlpool Grab: +50% range");
		Assert.Equal(new Box(20, 150, 175, 120), lv3.Throwboxes.Single().Box, "the box reaches 195");
		Assert.True(sp.Build(1, true).IsCommandGrab, "the EX version grabs too");
	}

	[Test]
	public static void Loader_RefusesBadCommandGrabs()
	{
		Assert.True(Refused(() => RiverGrab(d => Fd(d)["hitboxes"] = new JsonArray()).Build(1, false)), "no hitboxes: nothing places the grab");
		Assert.True(Refused(() => RiverGrab(d => Fd(d)["properties"]!["throw"]!["range"] = 20).Build(1, false)), "range not past the box's near edge");
		Assert.True(Refused(() => RiverGrab(d => Fd(d)["properties"]!["throw"]!["break_window"] = 30).Build(1, false)), "E12: recovery must outlast the break window");
		Assert.True(Refused(() => RiverGrab(d => Fd(d)["properties"]!["throw"] = new JsonObject { ["range"] = 130 }).Build(1, false)), "breakable is required");
		Assert.True(Refused(() => RiverGrab(d => Fd(d)["properties"]!["throw"]!["range"] = 0).Build(1, false)), "range >= 1");
		string thr = File.ReadAllText(ProjectSettings.GlobalizePath(FightScene.FixtureThrowsDir) + "/test-ryo-throw.json");
		var doc = JsonNode.Parse(thr)!.AsObject();
		doc["frame_data"]!["properties"] = new JsonObject { ["throw"] = new JsonObject { ["range"] = 130, ["breakable"] = true } };
		Assert.True(Refused(() => MoveLoader.Parse(doc.ToJsonString())), "the LP+LK throw can't also carry properties.throw");
		var jab = JsonNode.Parse(File.ReadAllText(ProjectSettings.GlobalizePath(FightScene.FixtureNormalsDir) + "/" +
			Path.GetFileName(Directory.GetFiles(ProjectSettings.GlobalizePath(FightScene.FixtureNormalsDir), "*.json").OrderBy(f => f).First())))!.AsObject();
		jab["frame_data"]!["properties"] = new JsonObject { ["throw"] = new JsonObject { ["range"] = 130, ["breakable"] = true } };
		Assert.True(Refused(() => MoveLoader.Parse(jab.ToJsonString())), "nor can a normal");
		Assert.True(!Refused(() => RiverGrab(d => Fd(d)["properties"]!["throw"]!["break_window"] = 9).Build(1, false)), "a longer break window loads");
		Assert.Equal(9, RiverGrab(d => Fd(d)["properties"]!["throw"]!["break_window"] = 9).Build(1, false).BreakWindow, "break_window from data");
	}

	// --- The throw pipeline ------------------------------------------------------------------

	[Test]
	public static void SpecialInput_Grabs_LandsAfterTheWindow_HeavyHitWithKnockdownAndMeter()
	{
		var log = new Log();
		var m = Setup(log);
		int hp = m.P2.Health;
		Run(m, LandTick, At(1, Grab), None);
		Assert.True(log.Outcomes.SequenceEqual(new[] { ThrowOutcome.Grab, ThrowOutcome.Land }), "grab, then land" + ": " + string.Join(",", log.Outcomes));
		Assert.Equal(GrabTick, log.Throws[0].tick, "the grab connects on its first active frame");
		Assert.Equal(LandTick, log.Throws[1].tick, "lands BreakWindow + 1 ticks after the grab");
		Assert.Equal("test-river-grab", log.Throws[1].e.MoveId, "the special's id");
		Assert.Equal(130, log.Hits.Single().e.Damage, "130 damage, raised as a Hit too");
		Assert.True(m.P2.Health == hp - 130 && m.P2.State == FighterState.Knockdown, "standard knockdown (C4)");
		Assert.True(log.Impacts.Single().Strength == HitStrength.Heavy && m.HitstopLeft > 0, "heavy hitstop (E12)");
		Assert.Equal(6, m.P1.Meter, "C5: +6 on a landed throw");
	}

	[Test]
	public static void ThrowButtonsNeverFireIt_KihonSpecialInputDoes()
	{
		var log = new Log();
		var m = Setup(log);
		Run(m, 3, At(1, new FighterInput(THROW)), None);
		Assert.True(m.P1.CurrentMove is { IsThrow: true }, "LP+LK is the generic throw, never the command grab");

		log = new Log();
		m = Setup(log);
		m.P1.Input.Scheme = ControlScheme.Kihon;
		Run(m, LandTick, At(1, new FighterInput(InputBits.Special | InputBits.Left)), None); // K2: Special + back = slot C
		Assert.True(log.Has(ThrowOutcome.Grab) && log.Has(ThrowOutcome.Land), "Kihon Special + back grabs and lands");
	}

	[Test]
	public static void BreakPress_LpLkInsideTheWindow_Breaks()
	{
		foreach (int t in new[] { GrabTick + 1, GrabTick + 7 })
		{
			var log = new Log();
			var m = Setup(log);
			Run(m, 30, At(1, Grab), At(t, new FighterInput(THROW)));
			Assert.True(log.Outcomes.SequenceEqual(new[] { ThrowOutcome.Grab, ThrowOutcome.Break }), $"LP+LK on tick {t} breaks" + ": " + string.Join(",", log.Outcomes));
			Assert.True(log.Hits.Count == 0 && m.P2.Health == 1000, "no damage");
		}
		var late = new Log();
		var ml = Setup(late);
		Run(ml, 30, At(1, Grab), At(GrabTick + 8, new FighterInput(THROW)));
		Assert.True(late.Has(ThrowOutcome.Land) && !late.Has(ThrowOutcome.Break), "past the window: lands");
		var one = new Log();
		var mo = Setup(one);
		Run(mo, 30, At(1, Grab), At(GrabTick + 2, new FighterInput(LP)));
		Assert.True(one.Has(ThrowOutcome.Land), "one button doesn't break");
	}

	[Test]
	public static void BreakableFalse_TheBreakPressDoesNothing()
	{
		var log = new Log();
		var m = Setup(log, grab: RiverGrab(d => Fd(d)["properties"]!["throw"]!["breakable"] = false));
		Run(m, 30, At(1, Grab), At(GrabTick + 2, new FighterInput(THROW)));
		Assert.True(log.Outcomes.SequenceEqual(new[] { ThrowOutcome.Grab, ThrowOutcome.Land }), "breakable false: lands" + ": " + string.Join(",", log.Outcomes));
	}

	[Test]
	public static void ElderKappaUnbreakableTwist_AppliesToTheCommandGrab()
	{
		var log = new Log();
		var m = Setup(log);
		m.P1.Twist = new ElderTwist("t-grip", "elder-kappa", TwistEffect.UnbreakableThrows, 0, 0, "twist.t", null, null, null);
		int twists = 0;
		m.TwistTriggered += (_, _) => twists++;
		Run(m, 30, At(1, Grab), At(GrabTick + 2, new FighterInput(THROW)));
		Assert.True(log.Has(ThrowOutcome.Land) && !log.Has(ThrowOutcome.Break) && twists == 1, "the break press is refused (YOK-90)");
	}

	[Test]
	public static void VsTheGenericThrow_SameFrameClashes()
	{
		// The generic throw grabs on frame 6: pressed on tick 2 it is active on tick 7 with the command grab.
		var log = new Log();
		var m = Setup(log);
		Run(m, 20, At(1, Grab), At(2, new FighterInput(THROW)));
		Assert.True(log.Outcomes.SequenceEqual(new[] { ThrowOutcome.Clash }), "E12: throw vs throw on the same frame clash" + ": " + string.Join(",", log.Outcomes));
		Assert.True(m.P1.Health == 1000 && m.P2.Health == 1000, "no damage");
	}

	[Test]
	public static void SlipperySkinStartup_ClashesTheCommandGrab_AM12()
	{
		var skin = ModifierLoader.LoadFile($"{Repo}/data/modifiers/slippery-skin.json");
		var log = new Log();
		var m = Setup(log);
		m.P2.AttachModifier(SpecialSlot.B, skin, m.Config);
		// The talisman's startup is frames 1-5: started on tick 4 it is in startup on tick 7.
		Run(m, 20, At(1, Grab), At(4, FighterInput.Special(SpecialSlot.B)));
		Assert.True(log.Outcomes.SequenceEqual(new[] { ThrowOutcome.Clash }), "AM12: a grab on a Slippery Skin startup is broken" + ": " + string.Join(",", log.Outcomes));
		Assert.Equal(1000, m.P2.Health, "no damage");
	}

	[Test]
	public static void BurstInvulnerability_AvoidsIt_StrikeInvulnerabilityDoesNot()
	{
		var log = new Log();
		var m = Setup(log);
		m.P2.State = FighterState.Hitstun;
		m.P2.StunLeft = 30;
		int x1 = m.P1.X;
		Run(m, 12, t => t == 2 ? Grab : Idle, t => t == 1 ? new FighterInput(BURST) : Idle);
		Assert.True(!log.Has(ThrowOutcome.Grab), "C6: the burst's invulnerable frames avoid the grab");
		_ = x1;

		// E12: a strike's invulnerable frames (the talisman's air invuln 1-5) don't dodge a throw.
		log = new Log();
		m = Setup(log);
		Run(m, 20, At(1, Grab), At(4, FighterInput.Special(SpecialSlot.B)));
		Assert.True(log.Has(ThrowOutcome.Grab), "grabbed out of the talisman's startup");
	}

	[Test]
	public static void Airborne_AndOutOfRange_Whiff()
	{
		var log = new Log();
		var m = Setup(log);
		Run(m, 30, At(1, Grab), At(1, new FighterInput(InputBits.Up)));
		Assert.True(!log.Has(ThrowOutcome.Grab), "C4: an airborne fighter can't be grabbed");

		log = new Log();
		m = Setup(log, gap: 200);
		Run(m, 40, At(1, Grab), None);
		Assert.True(log.Throws.Count == 0 && m.P2.Health == 1000, "200 apart: Lv 1 (reach 130 + body) whiffs");
	}

	[Test]
	public static void WhirlpoolGrab_ReachesHalfAgainAsFar()
	{
		var lv1 = new Log();
		Run(Setup(lv1, level: 1, gap: 200), 20, At(1, Grab), None);
		var lv3 = new Log();
		Run(Setup(lv3, level: 3, gap: 200), 20, At(1, Grab), None);
		Assert.True(!lv1.Has(ThrowOutcome.Grab) && lv3.Has(ThrowOutcome.Grab), "200 apart: Lv 1 whiffs, Lv 3 (range 195) grabs");
		var far = new Log();
		Run(Setup(far, level: 3, gap: 260), 20, At(1, Grab), None);
		Assert.True(!far.Has(ThrowOutcome.Grab), "260 apart: past even the Whirlpool reach");
	}

	[Test]
	public static void FasterStrikeBeatsTheStartup_GrabBeatsASameFrameStrike()
	{
		int lp = -1;
		var log = new Log();
		var m = Setup(log);
		lp = Array.FindIndex(m.P2.Moves, mv => mv.Button == LP && !mv.Air);
		Run(m, 20, At(1, Grab), At(1, FighterInput.Attack(lp)));
		Assert.True(!log.Has(ThrowOutcome.Grab) && log.Hits.Any(h => h.e.Attacker == 1), "a jab hits during the grab's startup (E12)");

		// A normal whose first active frame is tick 7 = the grab's frame: the grab wins.
		log = new Log();
		m = Setup(log);
		var moves = m.P2.Moves;
		int hp = Enumerable.Range(0, moves.Length).Where(i => moves[i].IsNormal && !moves[i].Air && moves[i].Startup < GrabTick)
			.OrderByDescending(i => moves[i].Startup).First();
		int start = GrabTick - moves[hp].Startup;
		Run(m, 20, At(1, Grab), At(start, FighterInput.Attack(hp)));
		Assert.True(log.Has(ThrowOutcome.Grab) && !log.Hits.Any(h => h.e.Attacker == 1), "grab beats a strike active on the same frame");
	}

	// --- Throw for cancels and trials ------------------------------------------------------------

	[Test]
	public static void Undertow_SuccessfulThrows_SeesTheCommandGrab_X4()
	{
		int Cancels(bool landed)
		{
			var log = new Log();
			var grab = RiverGrab(d => Fd(d)["cancel_windows"] = JsonNode.Parse("[{ \"frames\": [16, 38], \"on\": [\"hit\", \"whiff\"] }]"));
			var m = Setup(log, grab: grab, gap: landed ? null : 400);
			m.P1.HoldCancelRule(CancelRuleLoader.LoadFile($"{Repo}/data/cancels/undertow.json"));
			int into = 0; // only a start out of the grab's own frames (world frames 16-38) is a cancel
			m.SpecialStarted += (mm, e) => { if (e.Fighter == 0 && e.Slot == SpecialSlot.A && mm.WorldFrame <= 38) into++; };
			var motion = InputParserTests.Record("2 3 6+MP");
			Run(m, 60, t => t == 1 ? Grab : t >= 16 ? motion[(t - 16) % motion.Count] : Idle, None);
			return into;
		}
		Assert.Equal(1, Cancels(landed: true), "a landed command grab cancels into a special (Undertow)");
		Assert.Equal(0, Cancels(landed: false), "a whiffed one does not: successful throws only");
	}

	/// <summary>Code Critic (YOK-225): a held grab reads as a hit, but no cancel rule (Fox Step here) may end it while the
	/// defender is Thrown; once it has landed the window is open again.</summary>
	[Test]
	public static void FoxStep_CannotCancelAHeldGrab_TheGrabStillLands()
	{
		var log = new Log();
		var grab = RiverGrab(d => Fd(d)["cancel_windows"] = JsonNode.Parse("[{ \"frames\": [8, 38], \"on\": [\"hit\"] }]"));
		var m = Setup(log, grab: grab);
		m.P1.HoldCancelRule(CancelRuleLoader.LoadFile($"{Repo}/data/cancels/fox-step.json"));
		var dash = InputParserTests.Record("6 5 6");
		bool dashedWhileHeld = false, dashedAfter = false;
		for (int t = 1; t <= 60; t++)
		{
			var p1 = t == 1 ? Grab : t >= 8 ? dash[(t - 8) % dash.Count] : Idle;
			bool held = m.P2.State == FighterState.Thrown;
			m.Step(p1, Idle);
			if (m.P1.State == FighterState.Dash) { if (held) dashedWhileHeld = true; else dashedAfter = true; }
		}
		Assert.True(!dashedWhileHeld, "no dash cancel while the defender is Thrown");
		Assert.True(log.Outcomes.SequenceEqual(new[] { ThrowOutcome.Grab, ThrowOutcome.Land }), "the grab lands: " + string.Join(",", log.Outcomes));
		Assert.Equal(1000 - 130, m.P2.Health, "130 damage");
		Assert.True(dashedAfter, "after the land, Fox Step may dash out of the window as usual");
	}

	/// <summary>The shared trial cases (fixtures/command-grabs/trial-cases.json, also run by tools/tests/test_validate_data.py):
	/// TrialData.MoveProblems on the kit accepts exactly the cases the validator accepts.</summary>
	[Test]
	public static void TrialCases_LoaderAndValidatorAgree()
	{
		var root = JsonNode.Parse(File.ReadAllText(ProjectSettings.GlobalizePath("res://tests/fixtures/command-grabs/trial-cases.json")))!;
		var kit = Moves().Concat(Starters().Select(sp => sp.Build(1, false))).Append(RiverGrab().Build(1, false)).ToArray();
		var wrong = new List<string>();
		foreach (var c in root["cases"]!.AsArray())
		{
			var doc = JsonNode.Parse(File.ReadAllText(Path.Combine(ContentPaths.FixturesDir, "trials", "test-spirit-wave-trial.json")))!.AsObject();
			doc["special"] = "test-river-grab";
			doc["goal"]!["steps"] = c!["steps"]!.DeepClone();
			var problems = TrialLoader.Parse(doc.ToJsonString()).MoveProblems(kit);
			if ((problems.Count == 0) != c["accept"]!.GetValue<bool>())
				wrong.Add($"{c["name"]}: {(problems.Count == 0 ? "accepted" : string.Join("; ", problems))}");
		}
		Assert.Equal("", string.Join(" | ", wrong), "C# vs the shared trial cases");
	}

	[Test]
	public static void Trial_ThrowStepIsMetByTheCommandGrab_HitStepIsNot_R7()
	{
		string Trial(string ev) => $$"""
			{ "kind": "trial", "id": "test-river-grab-trial", "status": "proposed", "special": "test-river-grab",
			  "prompt": "Throw the dummy with River Grab.", "attempts": 3,
			  "setup": { "distance": 120, "attempt_frames": 120, "dummy": { "action": "idle" } },
			  "goal": { "steps": [ { "event": "{{ev}}", "move": "test-river-grab" } ] },
			  "rules": ["R7"], "notes": "TEST FIXTURE (YOK-225)." }
			""";
		bool Landed(string ev)
		{
			var data = TrialLoader.Parse(Trial(ev));
			var cfg = TrialRun.Config(SimConfig.Default, data);
			var m = new Match(cfg, Moves(), Moves());
			FightScene.EquipStarters(m.P1, Starters());
			m.P1.Equip(SpecialSlot.C, RiverGrab());
			var run = new TrialRun(data, m);
			for (int t = 1; !run.AttemptOver && t <= 200; t++) run.Step(t == 1 ? Grab : Idle);
			return run.Landed;
		}
		Assert.True(Landed("throw"), "a throw step naming River Grab is met when it lands");
		Assert.True(!Landed("hit"), "its land is not a hit (throws only meet throw steps)");
	}

	// --- Tanuki copy, AI, bot ------------------------------------------------------------------

	[Test]
	public static void TanukiCopy_GrabsThroughTheSamePath_AtRyosLevel_T0_T4()
	{
		var ryo = Kit.Load(Kit.Ryo, FightScene.FixtureSources);
		foreach (int level in new[] { 1, 2, 3 })
		{
			var kit = TanukiKit.Build(ryo, new CopyTarget(SpecialSlot.C, RiverGrab(), level));
			var m = new Match(null, ryo.Moves, kit.Moves);
			ryo.Equip(m.P1);
			kit.Equip(m.P2);
			var copy = m.P2.Specials[(int)SpecialSlot.C]!.Move;
			Assert.True(copy.IsCommandGrab, $"Lv {level}: the copy is a command grab");
			Assert.Equal(130, copy.GrabRange, $"Lv {level}: the copy never gets Whirlpool's range (T4)");
			Assert.Equal(level >= 2 ? 143 : 130, copy.Damage, $"Lv {level}: at Ryo's level, capped at Lv 2");
			var log = new Log();
			m.Throw += (mm, e) => log.Throws.Add((mm.Tick, e));
			m.P1.X = -m.Config.BodyWidth / 2;
			m.P2.X = m.Config.BodyWidth / 2;
			Run(m, LandTick, None, At(1, Grab));
			Assert.True(log.Throws.Select(x => (x.e.Attacker, x.e.Outcome)).SequenceEqual(new[] { (1, ThrowOutcome.Grab), (1, ThrowOutcome.Land) }),
				$"Lv {level}: the Tanuki's copy grabs and lands");
		}
	}

	[Test]
	public static void Ai_SpecialAction_UsesTheCommandGrab()
	{
		var m = Setup(new Log());
		var l = AiLoadout.From(m.P1);
		Assert.True(l.TryGet(BehaviourProfile.ParseAction("special:test-river-grab"), out var input, out int frames), "the AI can press it as special:<id>");
		Assert.Equal(38, frames, "its total frames");
		var log = new Log();
		m = Setup(log);
		Run(m, LandTick, At(1, input), None);
		Assert.True(log.Has(ThrowOutcome.Land), "the AI's request grabs");
	}

	[Test]
	public static void Bot_UsesTheCommandGrabOnlyInItsReach()
	{
		var m = Setup(new Log());
		var kit = BotKit.From(m.P1);
		Assert.Equal(130, kit.GrabReach[SpecialSlot.C], "slot C is a command grab with reach 130");
		Assert.True(!kit.GrabReach.ContainsKey(SpecialSlot.A) && !kit.GrabReach.ContainsKey(SpecialSlot.B), "strikes are not");
	}

	// --- Sumo Stance, determinism, replay ------------------------------------------------------

	const string SumoLike = """
		{
		  "kind": "modifier", "id": "test-charge", "name": "Test Charge", "source": "kappa", "rarity": "common", "locked": false,
		  "rules": ["A10"], "applies_to": { "requires": [], "excludes": ["projectile"] },
		  "charge": {
		    "button": "special_trigger", "window_ticks": { "min": 0, "max": 30 }, "step_ticks": 10, "grace_ticks": 0,
		    "release": "dash_then_move", "hold_pose_clip": "ryo-crouch",
		    "dash": { "duration": "startup", "units_per_tick_by_step": [14, 18, 22, 26], "cap_units_per_tick": 26 }
		  },
		  "effects": [{ "target": "properties.forward_travel", "op": "set", "value": "charge_dash", "when": "charge_released" }],
		  "card": { "plain": "Test.", "frames": "Test." }
		}
		""";

	[Test]
	public static void SumoStanceDash_AddsReach_TheGrabStillNeedsToTouch()
	{
		bool Grabs(int gap, int hold, int level = 1)
		{
			var log = new Log();
			var m = Setup(log, level: level, gap: gap, mod: ModifierLoader.Parse(SumoLike));
			m.P1.Input.Scheme = ControlScheme.Kihon;
			Run(m, 60, t => t == 1 ? new FighterInput(InputBits.Special | InputBits.Left) : t <= 1 + hold ? new FighterInput(InputBits.Special) : Idle, None);
			return log.Has(ThrowOutcome.Grab);
		}
		Assert.True(!Grabs(300, 0) && Grabs(300, 30), "300 apart: a tap whiffs, a full hold (26 x 6 = 156 units) grabs");
		Assert.True(!Grabs(700, 30, level: 3), "700 apart: dash + Whirlpool reach still whiffs");
	}

	[Test]
	public static void RandomFights_Deterministic_AndReplayExactly()
	{
		static FighterInput Chaos(Random rng, InputBits fwd, InputBits back)
		{
			int k = rng.Next(12);
			if (k == 0) return Grab;
			if (k == 1) return new FighterInput(THROW);
			var bits = (InputBits)0;
			int d = rng.Next(5);
			if (d <= 2) bits |= fwd; else if (d == 3) bits |= back; // drift in, so grabs come in range
			if (rng.Next(5) == 0) bits |= new[] { LP, LK, MP, HP }[rng.Next(4)];
			return new FighterInput(bits);
		}
		var hashes = new List<ulong>[2];
		int grabs = 0;
		for (int pass = 0; pass < 2; pass++)
		{
			var log = new Log();
			var m = Setup(log, level: 3, place: false); // the recording starts from the match's own start positions
			m.P2.Equip(SpecialSlot.C, RiverGrab(), 2);
			var r1 = new Random(225); var r2 = new Random(522);
			hashes[pass] = new List<ulong>();
			var rec = ReplayRecorder.Begin(m, null, 0, null);
			for (int t = 0; t < 1200 && m.Phase != MatchPhase.Over; t++)
			{
				var a = Math.Abs(m.P2.X - m.P1.X) > 150 * S ? new FighterInput(InputBits.Right) : r1.Next(3) == 0 ? Grab : Chaos(r1, InputBits.Right, InputBits.Left); // P1 walks in and grabs
				var b = Chaos(r2, InputBits.Left, InputBits.Right);
				m.Step(a, b);
				rec.Record(a, b, m.StateHash());
				hashes[pass].Add(m.StateHash());
			}
			grabs = log.Throws.Count(x => x.e.MoveId == "test-river-grab");
			if (pass == 1)
			{
				var back = ReplayRecording.FromBytes(rec.Finish(m).ToBytes());
				var report = Replayer.Run(back, ReplayCatalog.FromRepo(Repo));
				Assert.True(report.Ok, $"replays exactly: {report}");
			}
		}
		Assert.True(hashes[0].SequenceEqual(hashes[1]), "same inputs, same hash every tick");
		Assert.True(grabs > 0, "the command grab came out and connected");
	}
}
