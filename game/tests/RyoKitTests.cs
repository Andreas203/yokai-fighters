using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-18: Ryo's base kit. Movement (C2), block high/low (C3, E6) and the six normals (C8) driven
/// through the real input layer: raw bits -> each fighter's InputReader (Kata) -> normal by button
/// and direction. Normals come from the TEST FIXTURE files in tests/fixtures/ryo-normals/; the C8
/// table below is the spec they are checked against.
/// </summary>
public static class RyoKitTests
{
	static readonly FighterInput Idle = FighterInput.None;
	static FighterInput In(InputBits b) => new(b);

	const InputBits L = InputBits.Left, R = InputBits.Right, U = InputBits.Up, D = InputBits.Down;
	const InputBits LP = InputBits.LightPunch, MP = InputBits.MediumPunch, HP = InputBits.HeavyPunch;
	const InputBits LK = InputBits.LightKick, MK = InputBits.MediumKick, HK = InputBits.HeavyKick;

	/// <summary>C8: button, startup, active, recovery, damage, knockdown.</summary>
	static readonly (InputBits button, int s, int a, int r, int dmg, bool kd)[] C8 =
	{
		(LP, 4, 2, 7, 30, false),
		(MP, 6, 3, 12, 50, false),
		(HP, 10, 4, 20, 80, false),
		(LK, 5, 2, 9, 30, false),
		(MK, 7, 3, 14, 55, false),
		(HK, 12, 4, 22, 90, true),
	};

	static MoveData[] Normals() => MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(Fight.FightScene.FixtureNormalsDir));
	static MoveData[] WithSweep() =>
		Normals().Concat(MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath("res://tests/fixtures/moves"))).ToArray();
	static MoveData Normal(InputBits b) => Normals().Single(m => m.Button == b);

	/// <summary>Fighters at push-box distance mid-stage.</summary>
	static Match Close(MoveData[] moves, List<HitEvent>? hits = null)
	{
		var m = new Match(null, moves, moves);
		m.P1.X = -m.Config.BodyWidth / 2;
		m.P2.X = m.Config.BodyWidth / 2;
		if (hits != null) m.Hit += (_, e) => hits.Add(e);
		return m;
	}

	/// <summary>P2 pinned in the right corner (so holding back doesn't walk it out of range), P1 touching.</summary>
	static Match Cornered(MoveData[] moves, List<HitEvent> hits)
	{
		var m = new Match(null, moves, moves);
		m.P2.X = m.Config.StageHalfWidth - m.Config.BodyWidth / 2;
		m.P1.X = m.P2.X - m.Config.BodyWidth;
		m.Hit += (_, e) => hits.Add(e);
		return m;
	}

	// --- Data -------------------------------------------------------------------------------

	[Test]
	public static void Normals_FixturesMatchC8()
	{
		var normals = Normals();
		Assert.Equal(6, normals.Length, "six normals");
		foreach (var (button, s, a, r, dmg, kd) in C8)
		{
			var mv = normals.Single(m => m.Button == button);
			Assert.Equal((s, a, r, dmg, kd), (mv.Startup, mv.Active, mv.Recovery, mv.Damage, mv.Knockdown), $"{mv.Id} frames/damage = C8");
			Assert.True(mv.Hitboxes.Count > 0 && mv.Hitboxes.All(h => mv.IsActive(h.First) && mv.IsActive(h.Last)), $"{mv.Id} hitboxes in the active window");
			Assert.Equal(0, mv.DirectionMask, $"{mv.Id} any direction");
		}
	}

	[Test]
	public static void Scene_FallsBackToFixtureNormals()
	{
		var moves = Kit.Load(Kit.Ryo, Fight.FightScene.FixtureSources).Moves; // YOK-56: fixtures, whatever is in data/
		Assert.True(C8.All(c => moves.Count(m => m.Button == c.button && !m.Air) == 1), "the fight scene has one ground normal per button");
	}

	// --- Six normals through the input layer -------------------------------------------------

	[Test]
	public static void Normals_EachButtonStartsItsNormal_FrameByFrame()
	{
		foreach (var (button, s, a, r, _, _) in C8)
		{
			var moves = Normals();
			var m = new Match(null, moves, moves); // out of range: whiffs
			int total = s + a + r;
			for (int t = 1; t <= total + 1; t++)
			{
				m.Step(t == 1 ? In(button) : Idle, Idle);
				if (t <= total)
				{
					Assert.Equal(FighterState.Attack, m.P1.State, $"{button}: tick {t} in the move");
					Assert.Equal(button, m.P1.CurrentMove!.Button, $"{button}: the matching normal");
					Assert.Equal(t, m.P1.MoveFrame, $"{button}: tick {t} frame");
					Assert.Equal(t > s && t <= s + a, m.P1.CurrentMove.IsActive(t), $"{button}: tick {t} active");
				}
				else Assert.Equal(FighterState.Idle, m.P1.State, $"{button}: actionable on tick {total + 1}");
			}
		}
	}

	[Test]
	public static void Normals_EachHitsForC8Damage()
	{
		foreach (var (button, s, _, _, dmg, kd) in C8)
		{
			var hits = new List<HitEvent>();
			var m = Close(Normals(), hits);
			for (int t = 1; t <= s + 1; t++) m.Step(t == 1 ? In(button) : Idle, Idle);
			Assert.Equal(1, hits.Count, $"{button} connects on its first active frame");
			Assert.Equal(s + 1, hits[0].Frame, $"{button} hit frame");
			Assert.Equal(m.Config.P2MaxHealth - dmg, m.P2.Health, $"{button} deals {dmg}");
			Assert.Equal(kd ? FighterState.Knockdown : FighterState.Hitstun, m.P2.State, $"{button} hit state");
		}
	}

	[Test]
	public static void Normals_PressDuringRecoveryIsBuffered()
	{
		var mp = Normal(MP);
		int actionable = 1 + mp.TotalFrames;
		foreach (int lead in new[] { 4, 5, 6 })
		{
			var m = new Match(null, Normals(), Normals());
			for (int t = 1; t <= actionable; t++)
				m.Step(t == 1 ? In(MP) : t == actionable - lead ? In(LP) : Idle, Idle);
			bool expect = lead <= m.P1.Input.Config.CommandBuffer;
			Assert.Equal(expect, m.P1.State == FighterState.Attack && m.P1.CurrentMove!.Button == LP,
				$"LP pressed {lead} ticks early {(expect ? "comes out" : "has expired")}");
		}
	}

	[Test]
	public static void Normals_ListedDirectionBeatsAnyDirection()
	{
		const string crouchJab = """
		{ "kind": "normal", "id": "test-crouch-jab", "name": "TEST FIXTURE", "input": { "button": "LP", "directions": [1, 2, 3] },
		  "frame_data": { "startup": 3, "active": 2, "recovery": 6, "damage": 20, "hitboxes": [], "hurtboxes": [] } }
		""";
		var moves = Normals().Append(MoveLoader.Parse(crouchJab)).ToArray();
		var m = new Match(null, moves, moves);
		m.Step(In(D | LP), Idle);
		Assert.Equal("test-crouch-jab", m.P1.CurrentMove?.Id, "2+LP picks the down-listed normal");
		m = new Match(null, moves, moves);
		m.Step(In(LP), Idle);
		Assert.Equal(LP, m.P1.CurrentMove?.Button, "5+LP picks the any-direction normal");
		Assert.True(m.P1.CurrentMove!.Id != "test-crouch-jab", "5+LP is not the crouch jab");
	}

	// --- Movement (C2) ----------------------------------------------------------------------

	[Test]
	public static void Walk_ForwardAndBack()
	{
		var m = new Match();
		int x = m.P1.X;
		m.Step(In(R), Idle);
		Assert.Equal(x + m.Config.WalkSpeed, m.P1.X, "walk forward");
		Assert.True(!m.P1.Guarding, "walking forward doesn't guard");
		m.Step(Idle, Idle);
		m.Step(In(L), Idle); // single taps a tick apart are not a dash: R . L
		Assert.Equal(x, m.P1.X, "walk back at the same speed");
		Assert.True(m.P1.Guarding && !m.P1.Crouching, "holding back = standing guard (C3)");
	}

	[Test]
	public static void Dash_ForwardAndBack_18Frames()
	{
		foreach (int sign in new[] { 1, -1 })
		{
			var m = new Match();
			InputBits tap = sign > 0 ? R : L;
			m.Step(In(tap), Idle);
			m.Step(Idle, Idle);
			int x = m.P1.X;
			int start = m.Tick + 1;
			for (int t = start; t < start + m.Config.DashFrames; t++)
			{
				m.Step(t == start ? In(tap) : Idle, Idle);
				Assert.Equal(FighterState.Dash, m.P1.State, $"dash {sign}: tick {t - start + 1} of 18");
			}
			int dist = sign > 0 ? m.Config.DashDistance : m.Config.BackDashDistance;
			Assert.Equal(x + sign * dist, m.P1.X, $"dash {sign} covers its distance");
			m.Step(Idle, Idle);
			Assert.Equal(FighterState.Idle, m.P1.State, "actionable on the 19th tick");
		}
	}

	[Test]
	public static void Dash_NeedsTwoTapsInsideTheWindow()
	{
		var m = new Match();
		int window = m.P1.Input.Config.DashWindow;
		m.Step(In(R), Idle);
		for (int i = 0; i < window; i++) m.Step(Idle, Idle);
		m.Step(In(R), Idle);
		Assert.Equal(FighterState.Idle, m.P1.State, "taps too far apart walk, not dash");
		m = new Match();
		m.Step(In(R), Idle);
		m.Step(In(R), Idle);
		Assert.Equal(FighterState.Idle, m.P1.State, "holding forward walks");
		m.Step(In(R | D), Idle);
		m.Step(In(R), Idle);
		Assert.Equal(FighterState.Dash, m.P1.State, "6 3 6 within the window dashes");
	}

	[Test]
	public static void Jump_Neutral_40FramesAirtime()
	{
		var m = new Match();
		int x = m.P1.X;
		for (int t = 1; t <= m.Config.JumpFrames; t++)
		{
			m.Step(t == 1 ? In(U) : Idle, Idle);
			Assert.Equal(FighterState.Jump, m.P1.State, $"tick {t} in the air state");
			Assert.Equal(t, m.P1.AirFrame, $"tick {t} air frame");
			Assert.True(t < m.Config.JumpFrames ? m.P1.Y > 0 : m.P1.Y == 0, $"tick {t} height {m.P1.Y}");
			Assert.True(!m.P1.Guarding && !m.P1.Crouching, "no guard in the air");
		}
		Assert.Equal(m.Config.JumpHeight, m.Config.JumpY(m.Config.JumpFrames / 2), "apex at mid-jump");
		m.Step(Idle, Idle);
		Assert.Equal(FighterState.Idle, m.P1.State, "lands and acts on the 41st tick");
		Assert.Equal((0, x), (m.P1.Y, m.P1.X), "neutral jump lands where it started");
	}

	[Test]
	public static void Jump_ForwardAndBack_Drift()
	{
		foreach (var (bits, sign) in new[] { (U | R, 1), (U | L, -1) })
		{
			var m = new Match();
			int x = m.P1.X;
			for (int t = 1; t <= m.Config.JumpFrames + 1; t++) m.Step(t == 1 ? In(bits) : Idle, Idle);
			Assert.Equal(x + sign * m.Config.JumpFrames * m.Config.JumpSpeedX, m.P1.X, $"jump {sign} drift");
			Assert.Equal(FighterState.Idle, m.P1.State, "landed");
		}
	}

	[Test]
	public static void Jump_OverTheOpponent_SwitchesSidesOnLanding()
	{
		var m = Close(Normals());
		for (int t = 1; t <= m.Config.JumpFrames + 1; t++)
		{
			m.Step(t == 1 ? In(U | R) : Idle, Idle);
			if (m.P1.Airborne) Assert.Equal(1, m.P1.Facing, $"facing held in the air (tick {t})");
		}
		Assert.True(m.P1.X > m.P2.X, "P1 landed on the far side");
		Assert.True(m.P2.X - m.P1.X <= -m.Config.BodyWidth, "push boxes apart after landing");
		Assert.Equal((-1, 1), (m.P1.Facing, m.P2.Facing), "both turn after the cross-over");
	}

	[Test]
	public static void Crouch_HoldsStill_AndCrouchBlocksWithDownBack()
	{
		var m = new Match();
		int x = m.P1.X;
		m.Step(In(D | R), Idle);
		Assert.True(m.P1.Crouching && !m.P1.Guarding && m.P1.X == x, "3 crouches without walking or guarding");
		m.Step(In(D | L), Idle);
		Assert.True(m.P1.Crouching && m.P1.Guarding && m.P1.X == x, "1 = crouch guard");
		Assert.Equal(m.Config.CrouchHurtbox, m.BodyHurtbox(m.P1), "crouch hurtbox");
		m.Step(Idle, Idle);
		Assert.Equal(m.Config.IdleHurtbox, m.BodyHurtbox(m.P1), "standing hurtbox");
	}

	// --- Blocking (C3, E6) ------------------------------------------------------------------

	/// <summary>P1 does `start` on tick 1; P2 holds `guard` throughout. Returns the hit and P2's health.</summary>
	static (HitEvent hit, Match m) Exchange(FighterInput start, InputBits guard, MoveData mv)
	{
		var hits = new List<HitEvent>();
		var m = Cornered(WithSweep(), hits);
		for (int t = 1; t <= mv.Startup + 1; t++) m.Step(t == 1 ? start : Idle, In(guard));
		Assert.Equal(1, hits.Count, $"{mv.Id} connects");
		return (hits[0], m);
	}

	[Test]
	public static void Block_StandingAndCrouching_AllSixNormals()
	{
		// P2 faces left: back = Right.
		foreach (var (button, _, _, _, _, _) in C8)
		{
			var mv = Normal(button);
			foreach (var (guard, what) in new[] { (R, "standing"), (R | D, "crouching") })
			{
				var (hit, m) = Exchange(In(button), guard, mv);
				Assert.True(hit.Blocked, $"{mv.Id} blocked {what} (E6: crouch blocks mids)");
				Assert.Equal(m.Config.P2MaxHealth, m.P2.Health, $"{mv.Id}: no chip damage (E6)");
				Assert.Equal((FighterState.Blockstun, mv.Blockstun), (m.P2.State, m.P2.StunLeft), $"{mv.Id} blockstun from data");
			}
			var (h, m2) = Exchange(In(button), InputBits.None, mv);
			Assert.True(!h.Blocked && m2.P2.Health == m2.Config.P2MaxHealth - mv.Damage, $"{mv.Id} hits when not holding back");
		}
	}

	[Test]
	public static void Block_LowMustBeCrouchBlocked()
	{
		var moves = WithSweep();
		int sweep = Array.FindIndex(moves, m => m.Id == "test-sweep");
		var mv = moves[sweep];
		Assert.True(mv.Low, "fixture sweep is low");
		var (standing, _) = Exchange(FighterInput.Attack(sweep), R, mv);
		Assert.True(!standing.Blocked, "a low beats a standing guard (C3)");
		var (crouching, m) = Exchange(FighterInput.Attack(sweep), R | D, mv);
		Assert.True(crouching.Blocked && m.P2.Health == m.Config.P2MaxHealth, "a low is blocked crouching, no chip");
	}

	[Test]
	public static void Air_NoBlock_HitFallsAndLands()
	{
		var hits = new List<HitEvent>();
		var m = Close(Normals(), hits);
		var hp = Normal(HP);
		// P2 jumps straight up holding back; P1's HP meets it on the way down.
		int press = 26;
		for (int t = 1; t <= 80; t++)
		{
			m.Step(t == press ? In(HP) : Idle, In(t == 1 ? U : R));
			if (hits.Count == 1 && t == press + hp.Startup)
			{
				Assert.True(!hits[0].Blocked, "no blocking in the air");
				Assert.True(m.P2.Airborne && m.P2.State == FighterState.Hitstun, "hit in the air: hitstun while falling");
				Assert.Equal(0, m.P2.JumpDir, "drift stops");
			}
		}
		Assert.Equal(1, hits.Count, "HP hits the descending jumper");
		Assert.Equal((0, 0), (m.P2.Y, m.P2.AirFrame), "landed");
		Assert.True(m.P2.State is FighterState.Idle or FighterState.Jump, $"free again ({m.P2.State})");
	}

	// --- Determinism -------------------------------------------------------------------------

	[Test]
	public static void Kit_Deterministic_RandomRawInputs()
	{
		ulong Run(List<string>? errors)
		{
			var m = new Match(null, Normals(), Normals());
			var rng = new SimRng(7);
			InputBits Rand()
			{
				var b = (InputBits)rng.Next(16); // any direction combination
				if (rng.Next(6) == 0) b |= C8[rng.Next(6)].button;
				return b;
			}
			for (int t = 0; t < 4000 && m.Phase != MatchPhase.Over; t++)
			{
				m.Step(In(Rand()), In(Rand()));
				if (errors == null) continue;
				int edge = m.Config.StageHalfWidth - m.Config.BodyWidth / 2;
				if (Math.Abs(m.P1.X) > edge || Math.Abs(m.P2.X) > edge) errors.Add($"outside stage at {t}");
				if (m.P1.Y == 0 && m.P2.Y == 0 && Math.Abs(m.P2.X - m.P1.X) < m.Config.BodyWidth) errors.Add($"grounded overlap at {t}");
				if (m.P1.Y < 0 || m.P2.Y < 0) errors.Add($"below the floor at {t}");
			}
			return m.StateHash();
		}
		var errs = new List<string>();
		ulong a = Run(errs), b = Run(null);
		Assert.True(errs.Count == 0, string.Join("; ", errs.Take(5)));
		Assert.Equal(a, b, "same raw inputs, same state hash");
	}
}
