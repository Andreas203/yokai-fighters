using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-55: Ryo's jump-in normals (E11) from the TEST FIXTURE files in tests/fixtures/ryo-air-normals/
/// (air punch on HP, air kick on HK; made-up numbers), next to the ground fixtures. Driven through the
/// real input layer, so Kata and Kihon share it (K3).
/// </summary>
public static class AirNormalTests
{
	static readonly FighterInput Idle = FighterInput.None;
	static FighterInput In(InputBits b) => new(b);
	const InputBits U = InputBits.Up, R = InputBits.Right, D = InputBits.Down;
	const InputBits HP = InputBits.HeavyPunch, HK = InputBits.HeavyKick, LP = InputBits.LightPunch;

	static MoveData[] Air() => MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureAirNormalsDir));
	static MoveData[] Kit() =>
		MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureNormalsDir)).Concat(Air()).ToArray();
	static MoveData AirMove(InputBits b) => Air().Single(m => m.Button == b);

	/// <summary>Fighters far apart (nothing connects): P1 at -150 units, P2 at +150.</summary>
	static Match Apart()
	{
		var m = new Match(null, Kit(), Kit());
		m.P1.X = -150 * SimConfig.Scale;
		m.P2.X = 150 * SimConfig.Scale;
		return m;
	}

	/// <summary>P2 in the right corner, P1 <paramref name="gap"/> units in front of it.</summary>
	static Match Cornered(List<HitEvent> hits, int gap = 100)
	{
		var m = new Match(null, Kit(), Kit());
		m.P2.X = m.Config.StageHalfWidth - m.Config.BodyWidth / 2;
		m.P1.X = m.P2.X - gap * SimConfig.Scale;
		m.Hit += (_, e) => hits.Add(e);
		return m;
	}

	// --- Data -------------------------------------------------------------------------------

	[Test]
	public static void Fixtures_LoadAsAirNormals()
	{
		var air = Air();
		Assert.Equal(2, air.Length, "proposal: one air punch + one air kick");
		Assert.True(air.All(a => a.Air && a.IsNormal && a.LandingRecovery == 3), "air flag and landing recovery 3 from data");
		Assert.True(new[] { HP, HK }.All(b => air.Count(a => a.Button == b) == 1), "on HP and HK");
		Assert.True(Kit().Where(k => !Air().Any(a => a.Id == k.Id)).All(k => !k.Air), "ground fixtures are not air normals");
		Assert.True(FightScene.LoadMoves().Count(mv => mv.Air) == 2, "the fight scene falls back to the air fixtures");
	}

	[Test]
	public static void Loader_RejectsLandingRecoveryWithoutAir()
	{
		const string json = "{\"kind\":\"normal\",\"id\":\"x\",\"input\":{\"button\":\"LP\"},\"landing_recovery\":3," +
			"\"frame_data\":{\"startup\":3,\"active\":2,\"recovery\":5,\"damage\":10}}";
		bool threw = false;
		try { MoveLoader.Parse(json); } catch (FormatException) { threw = true; }
		Assert.True(threw, "landing_recovery without air is rejected");
		var ok = MoveLoader.Parse(json.Replace("\"landing_recovery\":3,", "\"air\":true,"));
		Assert.True(ok.Air && ok.LandingRecovery == null, "air without landing_recovery uses the engine default");
	}

	// --- Ground vs air ------------------------------------------------------------------------

	[Test]
	public static void Ground_ButtonGivesGroundNormal_UpPlusButtonToo()
	{
		foreach (var bits in new[] { HP, HP | U })
		{
			var m = Apart();
			m.Step(In(bits), Idle);
			Assert.True(m.P1.State == FighterState.Attack && !m.P1.ActiveMove!.Air, $"{bits}: ground normal (E11)");
			Assert.True(!m.P1.Airborne, $"{bits}: no jump");
		}
	}

	[Test]
	public static void Air_PressInAJumpGivesTheAirNormal_ArcCarriesOn()
	{
		foreach (var b in new[] { HP, HK })
		{
			var m = Apart();
			m.Step(In(U | R), Idle); // forward jump, tick 1 = air frame 1
			for (int t = 2; t < 10; t++) m.Step(Idle, Idle);
			int x = m.P1.X;
			m.Step(In(b), Idle); // tick 10
			Assert.True(m.P1.State == FighterState.Attack && m.P1.ActiveMove!.Id == AirMove(b).Id, $"{b}: air normal comes out");
			Assert.Equal((10, 1), (m.P1.AirFrame, m.P1.MoveFrame), $"{b}: air frame and move frame");
			Assert.Equal(m.Config.JumpY(10), m.P1.Y, $"{b}: on the arc");
			Assert.Equal(x + m.Config.JumpSpeedX, m.P1.X, $"{b}: drift continues");
		}
	}

	[Test]
	public static void Air_ExplicitSlotOnlyWhileAirborne()
	{
		var m = Apart();
		int slot = Array.FindIndex(m.P1.Moves, mv => mv.Air) + 1;
		m.Step(FighterInput.Attack(slot - 1), Idle);
		Assert.Equal(FighterState.Idle, m.P1.State, "an air normal never comes out on the ground");
		m.Step(In(U), Idle);
		m.Step(FighterInput.Attack(slot - 1), Idle);
		Assert.True(m.P1.State == FighterState.Attack && m.P1.ActiveMove!.Air, "it does in a jump");

		// Ground-only kit: a press in the air does nothing.
		var g = new Match(null, MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureNormalsDir)), Kit());
		g.Step(In(U), Idle);
		g.Step(In(HP), Idle);
		Assert.Equal(FighterState.Jump, g.P1.State, "ground normals are never used in the air");
	}

	[Test]
	public static void Air_OnePerJump_AndNoneForButtonsWithoutOne()
	{
		var m = Apart();
		var punch = AirMove(HP);
		m.Step(In(U), Idle);
		m.Step(In(LP), Idle);
		Assert.Equal(FighterState.Jump, m.P1.State, "LP has no air normal (proposal: HP and HK only)");
		m.Step(In(HP), Idle); // tick 3
		for (int t = 4; t <= 3 + punch.TotalFrames; t++) m.Step(Idle, Idle);
		Assert.Equal(FighterState.Jump, m.P1.State, "the air punch ended mid-air: falling");
		m.Step(In(HK), Idle);
		Assert.Equal(FighterState.Jump, m.P1.State, "one air normal per jump");
		while (m.P1.Airborne) m.Step(Idle, Idle);
		Assert.Equal(FighterState.Idle, m.P1.State, "a finished air normal lands with no recovery");
		m.Step(In(U), Idle);
		m.Step(In(HK), Idle);
		Assert.True(m.P1.State == FighterState.Attack && m.P1.ActiveMove!.Id == AirMove(HK).Id, "a new jump gets a new air normal");
	}

	[Test]
	public static void Air_TouchdownEndsTheMove_LandingRecovery()
	{
		var m = Apart();
		var kick = AirMove(HK);
		m.Step(In(U), Idle);
		for (int t = 2; t < 30; t++) m.Step(Idle, Idle);
		m.Step(In(HK), Idle); // tick 30: move frame 12 would be tick 41, touchdown
		Assert.True(kick.TotalFrames > m.Config.JumpFrames - 30 + 1, "fixture outlasts the jump");
		for (int t = 31; t <= 40; t++) m.Step(Idle, Idle);
		Assert.True(m.P1.State == FighterState.Attack && m.P1.Airborne, "still in the air on tick 40");
		var states = new List<FighterState>();
		for (int t = 41; t <= 45; t++) { m.Step(In(U), Idle); states.Add(m.P1.State); }
		Assert.Equal("Landing,Landing,Landing,Jump,Jump", string.Join(",", states), "3 landing frames, then up jumps again (E11)");
		Assert.Equal(1, m.P1.AirFrame - 1, "the new jump started on tick 44");
	}

	// --- Hit and block (E6: no overheads) -----------------------------------------------------

	[Test]
	public static void Air_HitsAndIsBlockedStandingOrCrouching_WithHitstopAndMeter()
	{
		var kick = AirMove(HK);
		foreach (var (guard, blocked) in new[] { (InputBits.None, false), (R, true), (R | D, true), (D, false) })
		{
			var hits = new List<HitEvent>();
			var m = Cornered(hits);
			m.Step(In(U), Idle); // neutral jump: no drift
			for (int t = 2; t < 20; t++) m.Step(Idle, In(guard));
			m.Step(In(HK), In(guard)); // tick 20
			int t0 = 20, tick = t0;
			while (hits.Count == 0 && tick < t0 + kick.TotalFrames) { m.Step(Idle, In(guard)); tick++; }
			Assert.Equal(1, hits.Count, $"guard {guard}: the air kick connects");
			var h = hits[0];
			Assert.Equal(blocked, h.Blocked, $"guard {guard}: blocked");
			Assert.True(m.P1.Airborne && m.P1.ActiveMove!.Air, $"guard {guard}: from the air");
			Assert.Equal(blocked ? 0 : kick.Damage, h.Damage, $"guard {guard}: damage (no chip, E6)");
			Assert.Equal(m.Config.HitstopHeavy, m.HitstopLeft, $"guard {guard}: V2 heavy hitstop, also on block");
			Assert.Equal(blocked ? m.Config.MeterOnBlock : m.Config.MeterOnHit, m.P1.Meter, $"guard {guard}: attacker meter (E14)");
			Assert.Equal(blocked ? 0 : m.Config.MeterOnTaken, m.P2.Meter, $"guard {guard}: defender meter (E14)");
			Assert.Equal(blocked ? FighterState.Blockstun : FighterState.Hitstun, m.P2.State, $"guard {guard}: stun");
			Assert.Equal(blocked ? kick.Blockstun : kick.Hitstun, m.P2.StunLeft, $"guard {guard}: stun from data");
		}
	}

	[Test]
	public static void Landing_IsThrowable_NotGuarding()
	{
		var f = new Fighter { State = FighterState.Landing };
		Assert.True(Match.Throwable(f), "landing recovery can be thrown");
		f.AirFrame = 5;
		f.State = FighterState.Attack;
		Assert.True(!Match.Throwable(f), "an air normal can't be thrown");
	}

	// --- Overlay (YOK-22) -----------------------------------------------------------------------

	[Test]
	public static void Overlay_DrawsTheAirNormalsBoxesOnTheArc()
	{
		foreach (var b in new[] { HP, HK })
		{
			var m = Apart();
			var mv = AirMove(b);
			m.Step(In(U), Idle);
			for (int t = 2; t < 8; t++) m.Step(Idle, Idle);
			m.Step(In(b), Idle);
			for (int f = 1; f <= mv.TotalFrames && m.P1.State == FighterState.Attack; f++)
			{
				var boxes = DebugBoxes.For(m, 0);
				var want = mv.Hitboxes.Where(h => h.Covers(f)).Select(h => Match.WorldBox(m.P1, h.Box)).ToList();
				var got = boxes.Where(x => x.Kind == DebugBoxKind.Hit).Select(x => (x.X0, x.Y0, x.X1, x.Y1)).ToList();
				Assert.True(want.SequenceEqual(got), $"{mv.Id} frame {f}: hitboxes");
				var wantHurt = mv.Hurtboxes.Where(h => h.Covers(f)).Select(h => Match.WorldBox(m.P1, h.Box)).ToList();
				var gotHurt = boxes.Where(x => x.Kind == DebugBoxKind.Hurt).Select(x => (x.X0, x.Y0, x.X1, x.Y1)).ToList();
				Assert.True(wantHurt.SequenceEqual(gotHurt), $"{mv.Id} frame {f}: hurtboxes");
				Assert.True(m.P1.Y > 0 && gotHurt.Any(h => h.Y0 == m.P1.Y), $"{mv.Id} frame {f}: body box drawn at jump height");
				m.Step(Idle, Idle);
			}
		}
	}

	// --- Determinism ---------------------------------------------------------------------------

	[Test]
	public static void Air_Deterministic_RandomRawInputs()
	{
		InputBits[] buttons = { InputBits.LightPunch, InputBits.MediumPunch, HP, InputBits.LightKick, InputBits.MediumKick, HK };
		ulong Run(List<string>? errors)
		{
			var m = new Match(null, Kit(), Kit());
			var rng = new SimRng(55);
			InputBits Rand()
			{
				var b = (InputBits)rng.Next(16);
				if (rng.Next(5) == 0) b |= buttons[rng.Next(6)];
				return b;
			}
			int airAttacks = 0;
			for (int t = 0; t < 6000 && m.Phase != MatchPhase.Over; t++)
			{
				m.Step(In(Rand()), In(Rand()));
				if (errors == null) continue;
				foreach (var f in m.Fighters)
				{
					if (f.State != FighterState.Attack) continue;
					if (f.ActiveMove!.Air != f.Airborne) errors.Add($"tick {t}: {f.ActiveMove.Id} airborne={f.Airborne}");
					if (f.ActiveMove.Air && f.MoveFrame == 1) airAttacks++;
				}
			}
			if (errors != null && airAttacks == 0) errors.Add("no air normal came out");
			return m.StateHash();
		}
		var errs = new List<string>();
		ulong a = Run(errs), b = Run(null);
		Assert.True(errs.Count == 0, string.Join("; ", errs.Take(5)));
		Assert.Equal(a, b, "same raw inputs, same state hash");
	}
}
