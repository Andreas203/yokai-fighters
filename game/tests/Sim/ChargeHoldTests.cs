using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using YokaiFighters.Ai;
using YokaiFighters.Fight;
using YokaiFighters.Harness;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-171 (AM11, Sumo Stance): hold the special's trigger before it starts; on release the held ticks pick a dash
/// speed that moves the fighter on each startup frame. The modifier is a TEST FIXTURE written here with the numbers of
/// the YOK-142 data (window 0-30, step 10, 14 / 18 / 22 / 26 units per tick, cap 26), attached to the fixture Rising
/// Talisman in slot B (startup 5, Kihon Special + forward, Kata 623). Press on tick 1, hold through tick 1 + n,
/// release on tick 2 + n: the special plays frame 1 on the release tick and dashes on frames 1-5.
/// </summary>
public static class ChargeHoldTests
{
	const int S = SimConfig.Scale;
	const int Startup = 5; // test-rising-talisman
	static readonly FighterInput Idle = FighterInput.None;

	static string Charge(int min = 0, int max = 30, int step = 10, string units = "14, 18, 22, 26", int cap = 26, string extra = "") => $$"""
		{
		  "kind": "modifier", "id": "test-charge", "name": "Test Charge", "source": "kappa", "rarity": "common", "locked": false,
		  "rules": ["A10"], "applies_to": { "requires": [], "excludes": ["projectile"] },
		  "charge": {
		    "button": "special_trigger", "window_ticks": { "min": {{min}}, "max": {{max}} }, "step_ticks": {{step}}, "grace_ticks": 0,
		    "release": "dash_then_move", "hold_pose_clip": "ryo-crouch",
		    "dash": { "duration": "startup", "units_per_tick_by_step": [{{units}}], "cap_units_per_tick": {{cap}} }
		  },
		  "effects": [{ "target": "properties.forward_travel", "op": "set", "value": "charge_dash", "when": "charge_released" }{{extra}}],
		  "card": { "plain": "Test.", "frames": "Test." }
		}
		""";

	static SpecialData[] Fixtures() => SpecialLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureSpecialsDir));
	static MoveData[] Moves() =>
		MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureNormalsDir))
			.Concat(MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureThrowsDir))).ToArray();

	static Match Setup(string? modifierJson = null, ControlScheme scheme = ControlScheme.Kihon, int p1x = -300, int p2x = 300)
	{
		var m = new Match(null, Moves(), Moves());
		foreach (var f in m.Fighters) FightScene.EquipStarters(f, Fixtures());
		m.P1.AttachModifier(SpecialSlot.B, ModifierLoader.Parse(modifierJson ?? Charge()), m.Config);
		m.P1.Input.Scheme = scheme;
		m.P1.X = p1x * S;
		m.P2.X = p2x * S;
		return m;
	}

	/// <summary>Kihon: Special + forward on tick 1, Special held through tick 1 + <paramref name="hold"/>, then nothing.</summary>
	static FighterInput KihonHold(int t, int hold, InputBits extraOnPress = InputBits.None) =>
		t == 1 ? new FighterInput(InputBits.Special | InputBits.Right | extraOnPress)
		: t <= 1 + hold ? new FighterInput(InputBits.Special) : Idle;

	/// <summary>Plays the hold and returns P1's X before the press, the tick the special started and P1's X after its startup.</summary>
	static (int x0, int started, int x1, Match m) Dash(int hold, string? modifierJson = null)
	{
		var m = Setup(modifierJson);
		int x0 = m.P1.X, started = 0;
		m.SpecialStarted += (mm, e) => { if (e.Fighter == 0 && started == 0) started = mm.Tick; };
		for (int t = 1; started == 0 || m.Tick < started + Startup - 1; t++)
		{
			m.Step(KihonHold(t, hold), Idle);
			Assert.True(t < 200, "the special starts");
		}
		return (x0, started, m.P1.X, m);
	}

	static void AssertDash(int hold, int expectStart, int speed, string? json = null, string why = "")
	{
		var (x0, started, x1, _) = Dash(hold, json);
		Assert.Equal(expectStart, started, $"hold {hold}: the special starts on tick {expectStart} {why}");
		Assert.Equal(Startup * speed * S, x1 - x0, $"hold {hold}: {speed} units per tick over the {Startup} startup frames {why}");
	}

	[Test]
	public static void Tap_StartsOnTheNextTick_WithTheStepZeroDash()
	{
		var m = Setup();
		m.Step(KihonHold(1, 0), Idle);
		Assert.Equal(FighterState.Bracing, m.P1.State, "the trigger is down on the command tick: bracing, no move yet");
		Assert.True(m.P1.CurrentMove is null && !m.P1.Actionable, "no move, no control while bracing");
		AssertDash(0, 2, 14, why: "(a tap is step 0, as the YOK-142 card states)");
	}

	[Test]
	public static void HoldSteps_PickTheSpeedPerStep()
	{
		AssertDash(9, 11, 14);
		AssertDash(10, 12, 18);
		AssertDash(19, 21, 18);
		AssertDash(20, 22, 22);
		AssertDash(29, 31, 22);
	}

	[Test]
	public static void WindowMax_ReleasesItself_AtTheTopSpeed()
	{
		AssertDash(30, 31, 26, why: "(held reaches the window's max on tick 31: the special starts that tick)");
		AssertDash(90, 31, 26, why: "(holding on past the max changes nothing)");
		var (_, _, _, m) = Dash(90);
		Assert.Equal(FighterState.Attack, m.P1.State, "the special plays while the button is still down");
	}

	[Test]
	public static void Cap_LimitsTheStepSpeeds()
	{
		string capped = Charge(cap: 20);
		AssertDash(0, 2, 14, capped);
		AssertDash(10, 12, 18, capped);
		AssertDash(20, 22, 20, capped, "(22 capped at 20)");
		AssertDash(30, 31, 20, capped, "(26 capped at 20)");
		AssertDash(30, 32, 22, Charge(max: 40, units: "14, 18, 22"), "(past the last step the last speed holds)");
	}

	[Test]
	public static void ReleaseBeforeTheWindow_StartsThePlainSpecial()
	{
		string late = Charge(min: 5, extra: """, { "target": "damage", "op": "add", "value": 7, "when": "charge_released" }""");
		var (x0, started, x1, m) = Dash(4, late);
		Assert.Equal(6, started, "a release still starts the special");
		Assert.Equal(0, x1 - x0, "held 4 < min 5: no dash");
		Assert.Equal(0, m.P1.ChargeSpeed, "no charge speed");
		Assert.True(!m.P1.ActiveConditions.HasFlag(StartConditions.ChargeReleased), "not charge_released");
		int plain = m.P1.ActiveMove!.Damage;
		var (y0, _, y1, m2) = Dash(5, late);
		Assert.Equal(Startup * 14 * S, y1 - y0, "held 5 = min: dashes");
		Assert.Equal(plain + 7, m2.P1.ActiveMove!.Damage, "and a charge_released effect applies to the build that plays");
	}

	[Test]
	public static void SpecialWithoutCharge_StartsOnThePressTick()
	{
		var m = Setup();
		m.Step(new FighterInput(InputBits.Special | InputBits.Up), Idle); // Kihon 8 = slot A, Spirit Wave, no modifier
		Assert.Equal(FighterState.Attack, m.P1.State, "no charge: the special starts on the press, as before");
		Assert.Equal(0, m.P1.ChargeSpeed, "no dash");
	}

	[Test]
	public static void Kata_HoldsTheMotionsButton_SameSpeeds()
	{
		// K3: Kata holds the button that completed 623; same window and steps as Kihon.
		var m = Setup(scheme: ControlScheme.Kata);
		int x0 = m.P1.X, started = 0;
		bool? precision = null;
		m.SpecialStarted += (mm, e) => { if (e.Fighter == 0) { started = mm.Tick; precision = e.Precision; } };
		FighterInput In(int t) => t switch
		{
			1 => new FighterInput(InputBits.Right),
			2 => new FighterInput(InputBits.Down),
			3 => new FighterInput(InputBits.Down | InputBits.Right | InputBits.HeavyPunch),
			<= 13 => new FighterInput(InputBits.HeavyPunch), // held ticks 4-13 = 10
			_ => Idle,
		};
		for (int t = 1; t <= 3; t++) m.Step(In(t), Idle);
		Assert.Equal(FighterState.Bracing, m.P1.State, "623 + HP held: bracing");
		x0 = m.P1.X; // after the motion's forward walk step
		for (int t = 4; t <= 14 + Startup - 1; t++) m.Step(In(t), Idle);
		Assert.Equal(14, started, "released on tick 14");
		Assert.True(precision == true, "Kata precision kept through the hold (K1)");
		Assert.Equal(Startup * 18 * S, m.P1.X - x0, "10 ticks held = step 1 = 18");
	}

	[Test]
	public static void Ex_ChargesToo_AndSpendsOnRelease()
	{
		var m = Setup();
		m.P1.Meter = 300;
		for (int t = 1; t <= 12; t++)
		{
			m.Step(KihonHold(t, 10, InputBits.LightPunch), Idle);
			if (t == 1) Assert.Equal(300, m.P1.Meter, "nothing spent while bracing");
		}
		Assert.True(m.P1.ActiveEx, "Special + punch on the press: the EX build plays on release (E16)");
		Assert.Equal(18, m.P1.ChargeSpeed, "with the charge dash");
		Assert.True(m.P1.Meter < 300, "EX meter spent on release");
	}

	[Test]
	public static void HitWhileBracing_EndsTheHold()
	{
		var m = Setup(p1x: -60, p2x: 60);
		int lp = Array.FindIndex(m.P2.Moves, mv => mv.Button == InputBits.LightPunch && !mv.Air);
		bool started = false;
		m.SpecialStarted += (mm, e) => started |= e.Fighter == 0;
		for (int t = 1; t <= 40; t++) m.Step(KihonHold(t, 30), t == 2 ? new FighterInput(InputBits.None, (byte)(lp + 1)) : Idle);
		Assert.True(!started, "the hit ended the hold: no special after it");
		Assert.True(m.P1.BraceSlot == SpecialSlot.None && m.P1.BraceHeld == 0, "brace state cleared");
	}

	[Test]
	public static void HoldTicks_AreInTheStateHash()
	{
		var m = Setup();
		for (int t = 1; t <= 6; t++) m.Step(KihonHold(t, 20), Idle);
		Assert.Equal(5, m.P1.BraceHeld, "five ticks held");
		ulong h = m.StateHash();
		m.P1.BraceHeld++;
		Assert.True(m.StateHash() != h, "held ticks are state");
		m.P1.BraceHeld--;
		Assert.True(m.StateHash() == h, "and nothing else moved");
		for (int t = 7; t <= 23; t++) m.Step(KihonHold(t, 20), Idle);
		h = m.StateHash();
		m.P1.ChargeSpeed++;
		Assert.True(m.StateHash() != h, "the released dash speed is state");
	}

	[Test]
	public static void ChargeFight_ReplaysExactly()
	{
		ulong Play()
		{
			var m = Setup(p1x: -300, p2x: 300);
			m.P2.AttachModifier(SpecialSlot.B, ModifierLoader.Parse(Charge()), m.Config);
			m.P2.Input.Scheme = ControlScheme.Kihon;
			var rng = new SimRng(171);
			ulong h = 0;
			InputBits held1 = 0, held2 = 0;
			for (int t = 0; t < 1500 && m.Phase != MatchPhase.Over; t++)
			{
				FighterInput In(ref InputBits held)
				{
					int r = rng.Next(16);
					held = r == 0 ? InputBits.Special | (rng.Next(2) == 0 ? InputBits.Right : InputBits.Left)
						: r < 12 && (held & InputBits.Special) != 0 ? InputBits.Special
						: (InputBits)(rng.NextUInt() & (uint)(InputBits.Directions | InputBits.Attacks));
					return new FighterInput(held);
				}
				m.Step(In(ref held1), In(ref held2));
				h = h * 31 + m.StateHash();
			}
			return h;
		}
		Assert.Equal(Play(), Play(), "same inputs, same hashes");
	}

	[Test]
	public static void Loader_ReadsTheChargeAndExcludes()
	{
		var mod = ModifierLoader.Parse(Charge());
		Assert.True(mod.Charge is { WindowMin: 0, WindowMax: 30, StepTicks: 10, Cap: 26, HoldPoseClip: "ryo-crouch" }, "charge read");
		Assert.Equal("14,18,22,26", string.Join(",", mod.Charge!.UnitsPerTickByStep), "speeds read");
		Assert.True(mod.FrameEffects.Count == 0, "the charge_dash switch is not a frame-data change");
		var specials = Fixtures();
		Assert.True(!mod.AppliesTo(specials.First(s => s.HasProperty("projectile"))), "excludes projectile");
		Assert.True(mod.AppliesTo(specials.First(s => !s.HasProperty("projectile"))), "applies to the rest");
		Assert.Equal(0, mod.Charge.SpeedFor(31), "outside the window: no speed");
	}

	[Test]
	public static void Bot_HoldsForItsConfiguredTicks()
	{
		var content = HarnessContent.Load(ContentPaths.DataDir, ContentPaths.FixturesDir);
		var baseBot = BotConfig.LoadDefault(ContentPaths.DataDir);
		var cfg = baseBot.Fight with
		{
			WalkWeight = 0, NormalWeight = 0, SpecialWeight = 1, ThrowWeight = 0, JumpWeight = 0, WaitWeight = 0,
			MistakePct = 0, BlockPct = 0, AntiAirPct = 0, IdleGuardPct = 0, SpecialHoldTicks = 20,
		};
		var session = RunSession.Start(content.Pool, content.Map, content.Scenes, 5);
		Assert.True(session.Enter(session.Reachable()[0]).Ok, "enter row 1");
		var m = NodeFight.NewMatch(content.Sources, session.Run, session, session.Node!.Fighter, ControlScheme.Kihon);
		var mod = ModifierLoader.Parse(Charge());
		for (int s = (int)SpecialSlot.A; s <= (int)SpecialSlot.D; s++)
			if (m.P1.Specials[s] is { } eq && mod.AppliesTo(eq.Data)) m.P1.AttachModifier((SpecialSlot)s, mod, m.Config);
		var kit = BotKit.From(m.P1);
		Assert.True(kit.ChargeSlots.Count > 0, "Ryo holds a charge special");
		var bot = new NewcomerBot(cfg, kit, 9);
		var speeds = new HashSet<int>();
		int maxHeld = 0;
		for (int t = 0; t < 1200 && m.Phase == MatchPhase.Fighting; t++)
		{
			m.Step(bot.Next(AiView.Capture(m)), Idle);
			maxHeld = Math.Max(maxHeld, m.P1.BraceHeld);
			if (m.P1.ChargeSpeed > 0) speeds.Add(m.P1.ChargeSpeed);
		}
		Assert.Equal(20, maxHeld, "the bot keeps Special down for special_hold_ticks");
		Assert.True(speeds.SetEquals(new[] { 22 }), $"every charged release dashes at step 2 ({string.Join(",", speeds)})");
		string json = System.IO.File.ReadAllText(System.IO.Path.Combine(ContentPaths.DataDir, "harness", BotConfig.DefaultId + ".json"));
		Assert.Equal(0, BotConfig.Parse(json).Fight.SpecialHoldTicks, "absent = 0, a tap");
	}
}
