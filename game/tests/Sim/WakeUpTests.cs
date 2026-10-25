using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-53 follow-up: the wake-up phase after the E4 knockdown (40 frames, unchanged). For SimConfig.WakeUpFrames
/// frames the fighter is invulnerable (no strike, throw or projectile connects) and not actionable while the get-up
/// clip plays; then Idle. Driven by the TEST FIXTURE throw (lands on tick 14, 12-tick hitstop) like ThrowTests.
/// </summary>
public static class WakeUpTests
{
	const InputBits LP = InputBits.LightPunch, LK = InputBits.LightKick, THROW = LP | LK;
	const int LandTick = 14, Stop = 12;

	static MoveData[] Moves() =>
		MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureNormalsDir))
			.Append(MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureThrowsDir)).Single()).ToArray();

	static Match Setup(SimConfig? cfg = null)
	{
		var m = new Match(cfg, Moves(), Moves());
		m.P1.X = -m.Config.BodyWidth / 2;
		m.P2.X = m.Config.BodyWidth / 2;
		return m;
	}

	/// <summary>Ticks 1..n; P1 throws on tick 1 unless p1 says otherwise. Returns P2's state after each tick.</summary>
	static List<FighterState> Run(Match m, int n, Func<int, InputBits>? p1 = null, Func<int, InputBits>? p2 = null, Action<int>? after = null)
	{
		var states = new List<FighterState>();
		for (int t = 1; t <= n; t++)
		{
			m.Step(new FighterInput(p1?.Invoke(t) ?? (t == 1 ? THROW : 0)), new FighterInput(p2?.Invoke(t) ?? 0));
			states.Add(m.P2.State);
			after?.Invoke(t);
		}
		return states;
	}

	static int FirstWake => LandTick + Stop + 40; // tick of wake-up frame 1 (1-based index into states)

	[Test]
	public static void WakeUp_FollowsThe40FrameKnockdown_ThenActionable()
	{
		var m = Setup();
		int n = m.Config.WakeUpFrames;
		Assert.Equal(28, n, "proposed wake-up length (designer proposal)");
		Assert.Equal(40, m.Config.KnockdownFrames, "E4 stays 40");
		var states = Run(m, FirstWake + n + 5);
		Assert.Equal(52, states.Count(s => s == FighterState.Knockdown), "knockdown unchanged: 40 frames + 12 hitstop ticks");
		Assert.Equal(FighterState.Knockdown, states[FirstWake - 2], "last knockdown frame");
		for (int k = 0; k < n; k++) Assert.Equal(FighterState.WakeUp, states[FirstWake - 1 + k], $"wake-up frame {k + 1}");
		Assert.Equal(n, states.Count(s => s == FighterState.WakeUp), "exactly WakeUpFrames frames");
		Assert.Equal(FighterState.Idle, states[FirstWake - 1 + n], "actionable on the frame after the wake-up");
	}

	[Test]
	public static void WakeUp_NotActionableAndInvulnerable_EveryFrame()
	{
		var m = Setup();
		int wakeTicks = 0;
		Run(m, FirstWake + m.Config.WakeUpFrames, after: t =>
		{
			if (m.P2.State != FighterState.WakeUp) return;
			wakeTicks++;
			Assert.True(!m.P2.Actionable, $"not actionable (tick {t})");
			Assert.True(m.P2.Invulnerable, $"invulnerable (tick {t})");
			Assert.True(!Match.Throwable(m.P2), $"not throwable (tick {t})");
			Assert.True(!DebugBoxes.For(m, 1).Any(b => b.Kind == DebugBoxKind.Hurt), $"no hurtbox drawn (tick {t})");
		});
		Assert.Equal(m.Config.WakeUpFrames, wakeTicks, "checked every wake-up frame");
	}

	[Test]
	public static void WakeUp_StrikesAndThrowsDontConnect()
	{
		foreach (var (name, button) in new[] { ("throw", THROW), ("jab", LP) })
		{
			var m = Setup();
			int hits = 0, grabs = 0;
			m.Hit += (_, e) => { if (m.Tick > LandTick) hits++; };
			m.Throw += (_, e) => { if (e.Outcome == ThrowOutcome.Grab && m.Tick > LandTick) grabs++; };
			// Pressed every 4th tick through the whole wake-up, with P2 put back in range each tick.
			var states = Run(m, FirstWake + m.Config.WakeUpFrames - 1,
				t => t == 1 ? THROW : t >= FirstWake - 1 && t % 4 == 0 ? button : 0,
				after: t => { if (t >= FirstWake - 2) m.P2.X = m.P1.X + m.Config.BodyWidth; });
			Assert.Equal(0, hits + grabs, $"{name}: nothing lands on a fighter getting up");
			Assert.Equal(1000 - 120, m.P2.Health, $"{name}: only the throw's damage");
			Assert.Equal(FighterState.WakeUp, states[^1], $"{name}: still getting up on the last wake-up frame");
		}
	}

	[Test]
	public static void WakeUp_HeldJumpComesOutOnTheFirstActionableFrame()
	{
		var m = Setup();
		int n = m.Config.WakeUpFrames;
		var states = Run(m, FirstWake + n + 2, p2: t => t > LandTick ? InputBits.Up : 0);
		Assert.Equal(FighterState.WakeUp, states[FirstWake - 2 + n], "still getting up on the last wake-up frame");
		Assert.Equal(FighterState.Jump, states[FirstWake - 1 + n], "the held jump leaves on the first actionable frame");
	}

	[Test]
	public static void WakeUpFrames_ZeroIsTheOldTiming()
	{
		var m = Setup(SimConfig.Default with { WakeUpFrames = 0 });
		var states = Run(m, FirstWake + 2);
		Assert.Equal(0, states.Count(s => s == FighterState.WakeUp), "no wake-up phase");
		Assert.Equal(FighterState.Idle, states[FirstWake - 1], "acts right after the 40 frames");
	}

	[Test]
	public static void WakeUp_Deterministic()
	{
		ulong Hash()
		{
			var m = Setup();
			var rng = new SimRng(7);
			Run(m, 300, t => t == 1 ? THROW : (InputBits)(rng.Next(64) & 0x3F), t => (InputBits)(rng.Next(64) & 0x3F));
			return m.StateHash();
		}
		Assert.Equal(Hash(), Hash(), "same inputs, same state through knockdowns and wake-ups");
	}

	/// <summary>Presentation: knockdown clip for the 40 frames, then the get-up clip's last WakeUpFrames frames, the
	/// first WakeRollBlend of them blended from the knockdown's final frame (the roll-over), ending on its last frame.</summary>
	[Test]
	public static void Presenter_WakeUpRollsFromTheKnockdownIntoTheGetUp()
	{
		var m = FightScene.NewMatch();
		var p = new FighterPresenter(FighterAnimSet.Kitsune, FightScene.Catalog, FightScene.MoveClips);
		string getUp = FighterAnimSet.Kitsune.ClipFor(PoseKind.GetUp)!;
		int total = FightScene.Catalog[getUp]!.FramesTotal;
		int n = m.Config.WakeUpFrames;
		int thr = Array.FindIndex(m.P1.Moves, x => x.IsThrow);
		void Step(FighterInput a) { m.Step(a, FighterInput.None); p.Observe(m, 1); }
		for (int t = 0; t < 300 && m.P2.X - m.P1.X > 9000; t++) Step(new FighterInput(InputBits.Right));
		Step(FighterInput.Attack(thr));
		for (int t = 0; t < 200 && m.P2.State != FighterState.Knockdown; t++) Step(FighterInput.None);
		PoseSample last = default;
		for (int t = 0; t < 200 && m.P2.State == FighterState.Knockdown; t++) { Assert.Equal(PoseKind.Knockdown, p.Sample.Kind, "knockdown clip while down"); last = p.Sample; Step(FighterInput.None); }
		for (int k = 1; k <= n; k++)
		{
			Assert.Equal(FighterState.WakeUp, m.P2.State, $"wake-up frame {k}");
			var s = p.Sample;
			Assert.Equal((PoseKind.GetUp, getUp, total - n + k), (s.Kind, s.Clip, s.Frame), $"get-up frame on wake-up frame {k}");
			if (k < FighterPresenter.WakeRollBlend)
			{
				Assert.Equal((last.Clip, last.Frame), (s.FromClip, s.FromFrame), $"rolling from the knockdown's final pose (frame {k})");
				Assert.True(Math.Abs(s.Weight - (float)k / FighterPresenter.WakeRollBlend) < 1e-4, $"roll weight {s.Weight} on frame {k}");
			}
			else Assert.True(!s.Blending, $"roll done by frame {k}");
			Step(FighterInput.None);
		}
		Assert.True(m.P2.State != FighterState.WakeUp, "up after the wake-up");
	}
}
