using System;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>YOK-15: fixed-step clock, facing/side switch, bounds/corner, camera, KO flow, determinism.</summary>
public static class FightLoopTests
{
	static readonly FighterInput Idle = FighterInput.None;
	static FighterInput In(InputBits b) => new(b);

	// --- Fixed step: exactly one tick per 1/60 s regardless of render rate -------------------

	static long RunClock(long frameUsec, long totalUsec, bool jitter)
	{
		var clock = new FixedStepClock();
		var rng = new SimRng(1234);
		long now = 1_000_000; // arbitrary non-zero start
		long start = now, ticks = clock.Advance(now);
		while (now - start < totalUsec)
		{
			long step = jitter ? frameUsec / 2 + rng.Next((int)frameUsec) : frameUsec;
			now = Math.Min(now + step, start + totalUsec);
			ticks += clock.Advance(now);
			long expected = (now - start) * 60 / 1_000_000;
			Assert.Equal(expected, ticks, $"ticks after {now - start} us at frame {frameUsec} us");
		}
		Assert.Equal(0L, clock.TicksDropped, "no ticks dropped");
		return ticks;
	}

	[Test]
	public static void Clock_SixtyTicksPerSecondAtAnyRenderRate()
	{
		foreach (int hz in new[] { 24, 30, 59, 60, 75, 120, 144, 240, 1000 })
			Assert.Equal(600L, RunClock(1_000_000 / hz, 10_000_000, false), $"10 s at {hz} Hz");
		Assert.Equal(600L, RunClock(1_000_000 / 90, 10_000_000, true), "10 s with jittered frame times");
	}

	[Test]
	public static void Clock_HitchDropsTicksInsteadOfSpiralling()
	{
		var clock = new FixedStepClock(maxCatchUp: 8);
		clock.Advance(0);
		Assert.Equal(8, clock.Advance(1_000_000), "1 s hitch runs at most 8 ticks");
		Assert.Equal(52L, clock.TicksDropped, "rest dropped");
		Assert.Equal(1, clock.Advance(1_000_000 + 16_667), "then one tick per 1/60 s again");
	}

	// --- Facing and side switch ---------------------------------------------------------------

	static void AssertFacing(Match m, string when)
	{
		int dx = m.P2.X - m.P1.X;
		if (dx != 0) Assert.Equal(Math.Sign(dx), m.P1.Facing, $"P1 faces P2 ({when})");
		Assert.Equal(-m.P1.Facing, m.P2.Facing, $"P2 faces P1 ({when})");
	}

	[Test]
	public static void Facing_AlwaysFaceEachOther_RandomInputs()
	{
		var m = new Match();
		var rng = new SimRng(42);
		InputBits[] pool = { InputBits.None, InputBits.Left, InputBits.Right, InputBits.DebugCrossUp, InputBits.Left | InputBits.Right };
		for (int t = 0; t < 5000; t++)
		{
			m.Step(In(pool[rng.Next(pool.Length)]), In(pool[rng.Next(pool.Length)]));
			AssertFacing(m, $"tick {t}");
			Assert.True(Math.Abs(m.P2.X - m.P1.X) >= m.Config.BodyWidth, $"push boxes keep fighters apart at tick {t}");
			int edge = m.Config.StageHalfWidth - m.Config.BodyWidth / 2;
			Assert.True(Math.Abs(m.P1.X) <= edge && Math.Abs(m.P2.X) <= edge, $"both inside the stage at tick {t}");
			Assert.True(Math.Abs(m.P2.X - m.P1.X) <= m.Config.MaxSeparation, $"screen walls hold at tick {t}");
			Assert.True(FightCamera.BothInView(m), $"camera shows both fighters at tick {t} (P1 {m.P1.X}, P2 {m.P2.X}, cam {FightCamera.CenterX(m)})");
		}
	}

	[Test]
	public static void Facing_SwitchesWhenSidesSwap()
	{
		var m = new Match();
		Assert.True(m.P1.X < m.P2.X && m.P1.Facing == 1 && m.P2.Facing == -1, "P1 starts left facing right");
		m.Step(In(InputBits.DebugCrossUp), Idle);
		Assert.True(m.P1.X > m.P2.X, "P1 is now right of P2");
		Assert.Equal(-1, m.P1.Facing, "P1 turned to face left");
		Assert.Equal(1, m.P2.Facing, "P2 turned to face right");
		m.Step(Idle, In(InputBits.DebugCrossUp));
		Assert.True(m.P2.X > m.P1.X, "P2 crossed back");
		AssertFacing(m, "after second switch");
	}

	// --- Stage bounds, corner, screen walls ---------------------------------------------------

	[Test]
	public static void Bounds_CornerHoldsAndPushesOpponentOut()
	{
		var m = new Match();
		int lo = -m.Config.StageHalfWidth + m.Config.BodyWidth / 2;
		// P2 walks P1 (walking back) all the way into the left corner.
		for (int t = 0; t < 600; t++) m.Step(In(InputBits.Left), In(InputBits.Left));
		Assert.Equal(lo, m.P1.X, "P1 stops at the left corner");
		Assert.Equal(lo + m.Config.BodyWidth, m.P2.X, "P2 is pressed against P1, not overlapping");
		AssertFacing(m, "in corner");
		// P2 tries to cross up into the corner: no room behind P1, so P2 stays in front.
		m.Step(Idle, In(InputBits.DebugCrossUp));
		Assert.True(m.P2.X >= lo && m.P1.X >= lo, "both inside the stage after a corner cross-up");
		Assert.True(Math.Abs(m.P2.X - m.P1.X) >= m.Config.BodyWidth, "no overlap after corner cross-up");
		AssertFacing(m, "after corner cross-up");
	}

	[Test]
	public static void Bounds_ScreenWallsStopWalkingApart()
	{
		var m = new Match();
		for (int t = 0; t < 600; t++) m.Step(In(InputBits.Left), In(InputBits.Right));
		Assert.Equal(m.Config.MaxSeparation, m.P2.X - m.P1.X, "separation capped by screen walls");
		Assert.True(FightCamera.BothInView(m), "both in view");
		for (int t = 0; t < 600; t++) m.Step(In(InputBits.Left), Idle); // P1 drags the camera left
		Assert.Equal(m.Config.MaxSeparation, m.P2.X - m.P1.X, "P1 cannot walk away from an idle P2");
	}

	[Test]
	public static void Walk_CrossesScreenInAboutTwoAndAHalfSeconds()
	{
		// C2: 1920 units in ~150 ticks.
		Assert.Equal(1920 * SimConfig.Scale, SimConfig.Default.WalkSpeed * 150, "walk speed");
	}

	// --- KO flow (V4) and reset -------------------------------------------------------------

	static Match KoMatch()
	{
		var m = new Match();
		int hits = m.Config.P2MaxHealth / m.Config.DebugStrikeDamage;
		for (int i = 0; i < hits; i++)
		{
			Assert.Equal(MatchPhase.Fighting, m.Phase, $"still fighting before hit {i + 1}");
			m.Step(In(InputBits.DebugStrike), Idle);
			if (m.Phase == MatchPhase.Fighting) m.Step(Idle, Idle); // release so the next press is a new edge
		}
		return m;
	}

	[Test]
	public static void Ko_EndsRoundAfterHalfSpeedSlowdown()
	{
		var m = KoMatch();
		Assert.Equal(MatchPhase.KoSlowMo, m.Phase, "KO starts the slow-down");
		Assert.Equal(0, m.Winner, "Ryo (P1) wins");
		Assert.True(m.P2.KnockedOut && m.P2.Health == 0, "P2 knocked out at 0 health");
		int koTick = m.Tick, koWorld = m.WorldFrame;
		int x1 = m.P1.X;
		for (int t = 1; t <= 2 * m.Config.KoSlowFrames; t++)
		{
			Assert.Equal(MatchPhase.KoSlowMo, m.Phase, $"still slow-mo at KO tick {t}");
			m.Step(In(InputBits.Right | InputBits.DebugStrike), Idle); // ignored after the blow
		}
		Assert.Equal(MatchPhase.Over, m.Phase, "round over after 30 frames at half speed (60 ticks)");
		Assert.Equal(koTick + 60, m.Tick, "60 real ticks elapsed");
		Assert.Equal(koWorld + 30, m.WorldFrame, "30 world frames elapsed");
		Assert.Equal(x1, m.P1.X, "inputs ignored after KO");
		ulong h = m.StateHash();
		m.Step(In(InputBits.Left), In(InputBits.Right));
		Assert.Equal(h, m.StateHash(), "match frozen once over");
	}

	[Test]
	public static void Ko_ResetRestoresInitialState()
	{
		ulong fresh = new Match().StateHash();
		var m = KoMatch();
		for (int t = 0; t < 60; t++) m.Step(Idle, Idle);
		Assert.Equal(MatchPhase.Over, m.Phase, "over");
		m.Reset();
		Assert.Equal(fresh, m.StateHash(), "reset equals a fresh match");
		m.Step(In(InputBits.Right), Idle);
		Assert.Equal(MatchPhase.Fighting, m.Phase, "can fight again after reset");
	}

	[Test]
	public static void Ko_DoubleKoIsADraw()
	{
		var m = new Match(SimConfig.Default with { P1MaxHealth = 100, P2MaxHealth = 100 });
		m.Step(In(InputBits.DebugStrike), In(InputBits.DebugStrike));
		Assert.Equal(MatchPhase.KoSlowMo, m.Phase, "simultaneous KO");
		Assert.Equal(-1, m.Winner, "draw");
	}

	// --- Determinism -----------------------------------------------------------------------

	static ulong[] Replay(uint seed, int ticks)
	{
		var m = new Match();
		var rng = new SimRng(seed);
		var hashes = new ulong[ticks];
		for (int t = 0; t < ticks; t++)
		{
			m.Step(In((InputBits)(rng.Next(4) | (rng.Next(20) == 0 ? (int)InputBits.DebugStrike : 0))),
				In((InputBits)(rng.Next(4) | (rng.Next(25) == 0 ? (int)InputBits.DebugCrossUp : 0))));
			hashes[t] = m.StateHash();
		}
		return hashes;
	}

	[Test]
	public static void Determinism_SameInputsSameStateEveryTick()
	{
		var a = Replay(7, 3000);
		var b = Replay(7, 3000);
		for (int t = 0; t < a.Length; t++) Assert.Equal(a[t], b[t], $"hash at tick {t}");
		Assert.True(a[^1] != Replay(8, 3000)[^1], "different inputs diverge");
	}

	// --- Scene smoke test: the presentation follows the sim through KO and reset -----------

	[Test]
	public static void Scene_RunsKoAndResetHeadless(Node runner)
	{
		var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		scene.ExternalDrive = true;
		runner.AddChild(scene);
		var hud = scene.GetNode<FightHud>("Hud");
		var bar = hud.GetNode<ColorRect>("P2Health");
		float full = bar.Size.X;
		scene.Step(In(InputBits.DebugStrike), Idle);
		Assert.True(bar.Size.X < full, "P2 health bar drops on hit");
		scene.Step(In(InputBits.DebugCrossUp), Idle);
		var ryo = scene.GetNode<Node3D>("Ryo");
		Assert.True(ryo.Scale.X < 0, "Ryo's model mirrors to face left after switching sides");
		for (int t = 0; t < 400 && scene.Match.Phase != MatchPhase.Over; t++)
			scene.Step(In(t % 2 == 0 ? InputBits.DebugStrike : InputBits.None), Idle);
		Assert.Equal(MatchPhase.Over, scene.Match.Phase, "KO reached in scene");
		Assert.True(hud.GetNode<Label>("Banner").Text.StartsWith("Ryo wins"), "banner shows the winner");
		scene.ResetFight();
		Assert.Equal(full, bar.Size.X, "health bar refilled after reset");
		Assert.Equal("", hud.GetNode<Label>("Banner").Text, "banner cleared");
		scene.QueueFree();
	}
}
