using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Godot;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-16: data-driven moves. Every number checked here comes from the test fixtures in
/// tests/fixtures/moves/ (labelled TEST FIXTURE, not game content), never from the code.
/// </summary>
public static class MoveSystemTests
{
	static readonly FighterInput Idle = FighterInput.None;
	static FighterInput In(InputBits b) => new(b);

	static string FixtureDir => ProjectSettings.GlobalizePath("res://tests/fixtures/moves");
	static MoveData[] Fixtures() => MoveLoader.LoadDirectory(FixtureDir);
	static int Slot(MoveData[] moves, string id) => Array.FindIndex(moves, m => m.Id == id);
	static MoveData Move(string id) => Fixtures()[Slot(Fixtures(), id)];

	/// <summary>A match with both fighters at push-box distance (120 units), mid-stage.</summary>
	static Match Close(MoveData[] moves, List<HitEvent>? hits = null)
	{
		var m = new Match(null, moves, moves);
		m.P1.X = -m.Config.BodyWidth / 2;
		m.P2.X = m.Config.BodyWidth / 2;
		if (hits != null) m.Hit += (_, e) => hits.Add(e);
		return m;
	}

	/// <summary>Plays the move alone (out of range) and records each tick's (state, frame, active).</summary>
	static List<(FighterState s, int frame, bool active)> Trace(MoveData[] moves, int slot, int ticks)
	{
		var m = new Match(null, moves, moves);
		var trace = new List<(FighterState, int, bool)>();
		for (int t = 1; t <= ticks; t++)
		{
			m.Step(t == 1 ? FighterInput.Attack(slot) : Idle, Idle);
			var mv = m.P1.CurrentMove;
			trace.Add((m.P1.State, m.P1.MoveFrame, mv != null && mv.IsActive(m.P1.MoveFrame)));
		}
		return trace;
	}

	static void AssertFrameCounts(MoveData mv, List<(FighterState s, int frame, bool active)> trace, string what)
	{
		for (int t = 1; t <= mv.TotalFrames; t++)
		{
			var (s, frame, active) = trace[t - 1];
			Assert.Equal(FighterState.Attack, s, $"{what}: tick {t} in move");
			Assert.Equal(t, frame, $"{what}: tick {t} move frame");
			Assert.Equal(t > mv.Startup && t <= mv.Startup + mv.Active, active, $"{what}: tick {t} active");
		}
		Assert.Equal(FighterState.Idle, trace[mv.TotalFrames].s, $"{what}: actionable on tick {mv.TotalFrames + 1}");
	}

	[Test]
	public static void FrameData_MovePlaysExactStartupActiveRecovery()
	{
		var moves = Fixtures();
		var jab = moves[Slot(moves, "test-jab")];
		Assert.Equal(4, jab.Startup, "fixture startup");
		var trace = Trace(moves, Slot(moves, "test-jab"), 20);
		AssertFrameCounts(jab, trace, "jab");
		Assert.Equal(2, trace.Count(x => x.active), "active frame count");
		Assert.Equal(13, trace.Count(x => x.s == FighterState.Attack), "total frame count");
	}

	[Test]
	public static void FrameData_ChangingDataChangesBehaviourWithoutCode()
	{
		var node = JsonNode.Parse(File.ReadAllText(Path.Combine(FixtureDir, "test-jab.json")))!;
		var fd = node["levels"]!["1"]!["frame_data"]!;
		fd["startup"] = 7;
		fd["active"] = 3;
		fd["recovery"] = 11;
		fd["hitboxes"]![0]!["frames"] = new JsonArray(8, 10);
		var edited = MoveLoader.Parse(node.ToJsonString());
		var moves = new[] { edited };
		var trace = Trace(moves, 0, 30);
		AssertFrameCounts(edited, trace, "edited jab");
		Assert.Equal(21, trace.Count(x => x.s == FighterState.Attack), "7+3+11 frames");
		Assert.Equal(8, trace.FindIndex(x => x.active) + 1, "first active tick moved to 8");
	}

	/// <summary>Ticks between the attacker and defender becoming actionable after a connect on the first active frame.</summary>
	static int MeasureAdvantage(string id, InputBits defenderHold, out HitEvent hit)
	{
		var moves = Fixtures();
		var hits = new List<HitEvent>();
		var m = Close(moves, hits);
		int attFree = -1, defFree = -1;
		for (int t = 1; t <= 120 && (attFree < 0 || defFree < 0); t++)
		{
			// Hold back only from the first active frame on, so walking back doesn't take P2 out of range.
			var def = t >= moves[Slot(moves, id)].FirstActive ? In(defenderHold) : Idle;
			m.Step(t == 1 ? FighterInput.Attack(Slot(moves, id)) : Idle, def);
			if (t > 1 && attFree < 0 && m.P1.State == FighterState.Idle) attFree = t;
			if (hits.Count > 0 && defFree < 0 && m.P2.State == FighterState.Idle) defFree = t;
		}
		Assert.Equal(1, hits.Count, $"{id}: one connect");
		hit = hits[0];
		return defFree - attFree;
	}

	[Test]
	public static void FrameAdvantage_OnHitMatchesData()
	{
		var jab = Move("test-jab");
		int adv = MeasureAdvantage("test-jab", InputBits.None, out var hit);
		Assert.True(!hit.Blocked && !hit.Counter, "clean hit");
		Assert.Equal(jab.FirstActive, hit.Frame, "lands on first active frame");
		Assert.Equal(jab.AdvantageOnHit, adv, "advantage on hit (hitstun - remaining frames)");
		Assert.Equal(4, adv, "fixture: 12 - (1 + 7)");
	}

	[Test]
	public static void FrameAdvantage_OnBlockMatchesData()
	{
		var jab = Move("test-jab");
		// P2 faces left, so holding Right is holding back (C3).
		int adv = MeasureAdvantage("test-jab", InputBits.Right, out var hit);
		Assert.True(hit.Blocked && hit.Damage == 0, "blocked, no damage");
		Assert.Equal(jab.AdvantageOnBlock, adv, "advantage on block");
		var sweep = Move("test-sweep");
		adv = MeasureAdvantage("test-sweep", InputBits.Right | InputBits.Down, out hit);
		Assert.True(hit.Blocked, "low blocked crouching");
		Assert.Equal(sweep.AdvantageOnBlock, adv, "sweep advantage on block");
		Assert.Equal(-15, adv, "fixture: 10 - (3 + 22)");
	}

	[Test]
	public static void FrameAdvantage_PlusOnBlockWinsTheNextExchange()
	{
		// Jab is +1 on block: if both press jab as soon as they can, the attacker's lands first.
		var moves = Fixtures();
		int jab = Slot(moves, "test-jab");
		var hits = new List<HitEvent>();
		var m = Close(moves, hits);
		for (int t = 1; t <= 60; t++)
		{
			var p1 = t == 1 || (hits.Count > 0 && m.P1.Actionable) ? FighterInput.Attack(jab) : Idle;
			var p2 = hits.Count == 0 ? (t >= 5 ? In(InputBits.Right) : Idle) : m.P2.Actionable ? FighterInput.Attack(jab) : Idle;
			m.Step(p1, p2);
			if (hits.Count >= 2) break;
		}
		Assert.True(hits[0].Blocked, "first jab blocked");
		Assert.Equal(0, hits[1].Attacker, "attacker wins the follow-up");
		Assert.True(!hits[1].Blocked && hits[1].Counter, "follow-up counterhits");
	}

	[Test]
	public static void Low_MustBeBlockedCrouching()
	{
		MeasureAdvantage("test-sweep", InputBits.Right, out var standing);
		Assert.True(!standing.Blocked, "standing block loses to a low (C3)");
		MeasureAdvantage("test-jab", InputBits.Right | InputBits.Down, out var crouchHigh);
		Assert.True(crouchHigh.Blocked, "crouch block stops a mid");
	}

	[Test]
	public static void Knockdown_DefenderFloorsForConfiguredFramesAndIsUnhittable()
	{
		var moves = Fixtures();
		var hits = new List<HitEvent>();
		var m = Close(moves, hits);
		int sweep = Slot(moves, "test-sweep"), jab = Slot(moves, "test-jab");
		int down = 0, lastDown = 0;
		var hitTicks = new List<int>();
		for (int t = 1; t <= 120; t++)
		{
			int before = hits.Count, wf = m.WorldFrame;
			m.Step(t == 1 ? FighterInput.Attack(sweep) : m.P1.Actionable ? FighterInput.Attack(jab) : Idle, Idle);
			if (hits.Count > before) hitTicks.Add(t);
			if (m.P2.State == FighterState.Knockdown) { lastDown = t; if (m.WorldFrame != wf) down++; } // world frames (hitstop ticks freeze, V2)
		}
		Assert.Equal(m.Config.KnockdownFrames + 1, down, "the hit frame plus KnockdownFrames from config");
		Assert.Equal(13, hitTicks[0], "sweep lands on its first active frame");
		Assert.Equal(90, hits[0].Damage, "sweep damage from data");
		Assert.True(hitTicks.Count > 1, "jabs land again after wake-up");
		Assert.True(hitTicks.Skip(1).All(t => t > lastDown), "no hit lands while floored (P1 jabbed from tick 39)");
		Assert.Equal(m.P2.MaxHealth - hits.Sum(h => h.Damage), m.P2.Health, "health matches hit events");
	}

	[Test]
	public static void Invulnerability_FramesWithoutHurtboxCannotBeHit()
	{
		var moves = Fixtures();
		var hits = new List<HitEvent>();
		var m = Close(moves, hits);
		for (int t = 1; t <= 20; t++)
			m.Step(t == 1 ? FighterInput.Attack(Slot(moves, "test-jab")) : Idle, t == 1 ? FighterInput.Attack(Slot(moves, "test-dodge")) : Idle);
		Assert.Equal(0, hits.Count, "jab passes through the dodge's empty hurtbox frames");
	}

	[Test]
	public static void CounterHit_AddsDamageAndHitstun()
	{
		var moves = Fixtures();
		var jab = moves[Slot(moves, "test-jab")];
		var hits = new List<HitEvent>();
		var m = Close(moves, hits);
		m.Step(FighterInput.Attack(Slot(moves, "test-jab")), FighterInput.Attack(Slot(moves, "test-sweep")));
		int stun = 0;
		for (int t = 2; t <= 60; t++)
		{
			int wf = m.WorldFrame;
			m.Step(Idle, Idle);
			if (m.P2.State == FighterState.Hitstun && m.WorldFrame != wf) stun++; // world frames, not hitstop ticks (V2)
		}
		Assert.Equal(1, hits.Count, "jab beats the slower sweep");
		Assert.True(hits[0].Counter, "counterhit (C7)");
		Assert.Equal(jab.Damage * (100 + m.Config.CounterHitDamagePct) / 100, hits[0].Damage, "+20% damage");
		Assert.Equal(jab.Hitstun + m.Config.CounterHitHitstun + 1, stun, "the hit frame plus hitstun +6");
	}

	[Test]
	public static void Trade_BothHitOnTheSameFrame()
	{
		var moves = Fixtures();
		var hits = new List<HitEvent>();
		var m = Close(moves, hits);
		var jab = FighterInput.Attack(Slot(moves, "test-jab"));
		for (int t = 1; t <= 20; t++) m.Step(t == 1 ? jab : Idle, t == 1 ? jab : Idle);
		Assert.Equal(2, hits.Count, "both connect");
		Assert.Equal(m.P1.Health, m.P2.Health, "even trade");
	}

	[Test]
	public static void OneHitPerMove_AcrossActiveFrames()
	{
		MeasureAdvantage("test-jab", InputBits.None, out _); // asserts exactly one connect over 2 active frames
	}

	[Test]
	public static void Facing_HeldDuringMoveThenTurns()
	{
		var moves = Fixtures();
		var m = Close(moves);
		m.Step(FighterInput.Attack(Slot(moves, "test-sweep")), In(InputBits.DebugCrossUp));
		Assert.True(m.P2.X < m.P1.X, "P2 crossed to P1's left");
		Assert.Equal(1, m.P1.Facing, "P1 keeps facing right during its move");
		for (int t = 2; t <= 38; t++) m.Step(Idle, Idle);
		Assert.Equal(1, m.P1.Facing, "still held on the last move frame");
		m.Step(Idle, Idle);
		Assert.Equal(-1, m.P1.Facing, "turns once actionable");
	}

	[Test]
	public static void Pushback_SeparatesByDataAndCornerPassesItToAttacker()
	{
		var moves = Fixtures();
		var jab = Slot(moves, "test-jab");
		var m = Close(moves);
		int gap0 = m.P2.X - m.P1.X;
		for (int t = 1; t <= 5; t++) m.Step(t == 1 ? FighterInput.Attack(jab) : Idle, Idle);
		Assert.Equal(gap0 + 20 * SimConfig.Scale, m.P2.X - m.P1.X, "on_hit pushback 20 units");

		var c = new Match(null, moves, moves);
		int hi = c.Config.StageHalfWidth - c.Config.BodyWidth / 2;
		c.P2.X = hi;
		c.P1.X = hi - c.Config.BodyWidth;
		c.Step(In(InputBits.Right), Idle); // face right, settle
		int p1Before = c.P1.X;
		for (int t = 1; t <= 5; t++) c.Step(t == 1 ? FighterInput.Attack(jab) : Idle, Idle);
		Assert.Equal(hi, c.P2.X, "cornered defender stays");
		Assert.Equal(p1Before - 20 * SimConfig.Scale, c.P1.X, "attacker takes the pushback");
	}

	[Test]
	public static void Loader_RejectsWhatTheSimCannotRun()
	{
		string Base(string extra) => "{\"id\":\"bad\",\"frame_data\":{\"startup\":3,\"active\":2,\"recovery\":4,\"damage\":10," + extra +
			"\"hurtboxes\":[{\"frames\":[1,9],\"rect\":{\"x\":0,\"y\":0,\"w\":1,\"h\":1}}]}}";
		string box(int a, int b) => "\"hitboxes\":[{\"frames\":[" + a + "," + b + "],\"rect\":{\"x\":0,\"y\":0,\"w\":1,\"h\":1}}],";
		bool Throws(string json) { try { MoveLoader.Parse(json); return false; } catch (FormatException) { return true; } }
		Assert.True(!Throws(Base("\"hitstun\":5,\"blockstun\":3," + box(4, 5))), "valid move loads");
		Assert.True(Throws(Base(box(4, 5))), "hitboxes without hitstun/blockstun");
		Assert.True(Throws(Base("\"hitstun\":5,\"blockstun\":3," + box(2, 5))), "hitbox before the active window");
	}

	[Test]
	public static void Determinism_RandomMovesReplayExactly()
	{
		var moves = Fixtures();
		ulong Run()
		{
			var m = new Match(null, moves, moves);
			var rng = new SimRng(99);
			ulong h = 0;
			for (int t = 0; t < 3000 && m.Phase == MatchPhase.Fighting; t++)
			{
				FighterInput Rand() => new((InputBits)rng.Next(16), (byte)(rng.Next(6) == 0 ? 1 + rng.Next(moves.Length) : 0));
				m.Step(Rand(), Rand());
				h ^= m.StateHash() + (ulong)t;
			}
			return h;
		}
		Assert.Equal(Run(), Run(), "same inputs, same hash stream");
	}
}
