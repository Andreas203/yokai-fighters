using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-19: generic throws (C4, E4) through the real input layer. P1 presses LP+LK (E12 throw
/// input) on tick 1, so throw frame n plays on tick n and the grab (frame 6) is tick 6. The break
/// window is ticks 7..13 and an unbroken throw lands on tick 14. Uses the TEST FIXTURE throw in
/// tests/fixtures/throws/ and the TEST FIXTURE normals.
/// </summary>
public static class ThrowTests
{
	static readonly FighterInput Idle = FighterInput.None;
	static FighterInput In(InputBits b) => new(b);
	const InputBits LP = InputBits.LightPunch, LK = InputBits.LightKick, THROW = LP | LK;
	const InputBits L = InputBits.Left, R = InputBits.Right, U = InputBits.Up, D = InputBits.Down;
	const int GrabTick = 6, LandTick = 14;

	static MoveData ThrowMove() => MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureThrowsDir)).Single();
	static MoveData[] Moves() =>
		MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureNormalsDir)).Append(ThrowMove()).ToArray();

	sealed class Log
	{
		public readonly List<(int tick, ThrowEvent e)> Throws = new();
		public readonly List<(int tick, HitEvent e)> Hits = new();
		public readonly List<ImpactEvent> Impacts = new();
		public readonly List<int> Bursts = new();
		public bool Has(ThrowOutcome o) => Throws.Any(t => t.e.Outcome == o);
	}

	/// <summary>Fighters touching mid-stage (push-box distance), or P2 pinned in the right corner.</summary>
	static Match Setup(Log log, bool cornered = false, SimConfig? cfg = null)
	{
		var m = new Match(cfg, Moves(), Moves());
		if (cornered)
		{
			m.P2.X = m.Config.StageHalfWidth - m.Config.BodyWidth / 2;
			m.P1.X = m.P2.X - m.Config.BodyWidth;
		}
		else
		{
			m.P1.X = -m.Config.BodyWidth / 2;
			m.P2.X = m.Config.BodyWidth / 2;
		}
		m.Throw += (mm, e) => log.Throws.Add((mm.Tick, e));
		m.Hit += (mm, e) => log.Hits.Add((mm.Tick, e));
		m.Impact += (_, e) => log.Impacts.Add(e);
		m.BurstFired += (_, i) => log.Bursts.Add(i);
		return m;
	}

	/// <summary>Runs ticks 1..n; p1/p2 give each tick's raw input.</summary>
	static void Run(Match m, int n, Func<int, InputBits> p1, Func<int, InputBits> p2, Action<int>? after = null)
	{
		for (int t = 1; t <= n; t++)
		{
			m.Step(In(p1(t)), In(p2(t)));
			after?.Invoke(t);
		}
	}

	static InputBits ThrowOn1(int t) => t == 1 ? THROW : 0;
	static InputBits None(int t) => 0;

	// --- Data --------------------------------------------------------------------------------

	[Test]
	public static void Throw_FixtureMatchesC4()
	{
		var t = ThrowMove();
		Assert.True(t.IsThrow && !t.IsNormal, "kind throw, not a normal");
		Assert.Equal(THROW, t.ThrowButtons, "E12 input LP+LK");
		Assert.Equal((5, 7, 120), (t.Startup, t.BreakWindow, t.Damage), "C4 startup / break window / damage");
		Assert.Equal(40, SimConfig.Default.KnockdownFrames, "E4 standard knockdown");
		Assert.True(t.Hitboxes.Count == 0 && t.Throwboxes.Count > 0, "throwboxes, no hitboxes");
		Assert.True(t.ThrowMatch(THROW | InputBits.HeavyKick) && !t.ThrowMatch(LP) && !t.ThrowMatch(LK), "both buttons on the same tick");
	}

	[Test]
	public static void Throw_LoaderRejectsBadThrows()
	{
		string Doc(string buttons, string fd) =>
			$"{{\"kind\":\"throw\",\"id\":\"t\",\"input\":{{\"buttons\":[{buttons}]}},\"frame_data\":{{{fd}}}}}";
		const string box = "\"throwboxes\":[{\"frames\":[6,7],\"rect\":{\"x\":0,\"y\":0,\"w\":10,\"h\":10}}]";
		const string ok = "\"startup\":5,\"active\":2,\"recovery\":20,\"damage\":120,\"break_window\":7," + box;
		MoveLoader.Parse(Doc("\"LP\",\"LK\"", ok)); // sanity: valid
		foreach (var (b, fd, why) in new[]
		{
			("\"LP\",\"LK\"", "\"startup\":5,\"active\":2,\"recovery\":20,\"damage\":120," + box, "no break_window"),
			("\"LP\",\"LK\"", "\"startup\":5,\"active\":2,\"recovery\":7,\"damage\":120,\"break_window\":7," + box, "recovery inside the window"),
			("\"LP\",\"LP\"", ok, "same button twice"),
			("\"LP\"", ok, "one button"),
			("\"LP\",\"LK\"", "\"startup\":5,\"active\":2,\"recovery\":20,\"damage\":120,\"break_window\":7,\"throwboxes\":[]", "no throwboxes"),
			("\"LP\",\"LK\"", ok.Replace("[6,7]", "[5,7]"), "throwbox outside the active window"),
		})
		{
			bool threw = false;
			try { MoveLoader.Parse(Doc(b, fd)); } catch (FormatException) { threw = true; }
			Assert.True(threw, $"loader rejects: {why}");
		}
	}

	// --- Timing ------------------------------------------------------------------------------

	[Test]
	public static void Throw_GrabsOnFrame6_LandsAfterWindow_KnockdownIs40()
	{
		var log = new Log();
		var m = Setup(log);
		var states = new List<FighterState>();
		var downFrames = new HashSet<int>(); // world frames: the landing's 12-tick hitstop (E12) freezes the knockdown
		Run(m, 90, ThrowOn1, None, t =>
		{
			states.Add(m.P2.State);
			if (m.P2.State == FighterState.Knockdown) downFrames.Add(m.WorldFrame);
			if (t == 1) Assert.True(m.P1.CurrentMove?.IsThrow == true && m.P1.MoveFrame == 1, "throw (not the LP jab) starts on the press");
			if (t < LandTick) Assert.Equal(1000, m.P2.Health, $"no damage before the throw lands (tick {t})");
		});
		Assert.Equal(GrabTick, log.Throws.Single(x => x.e.Outcome == ThrowOutcome.Grab).tick, "grab on frame 6 (startup 5)");
		for (int t = 1; t < GrabTick; t++) Assert.Equal(FighterState.Idle, states[t - 1], $"P2 free during startup (tick {t})");
		for (int t = GrabTick; t < LandTick; t++) Assert.Equal(FighterState.Thrown, states[t - 1], $"P2 held for the 7-frame window (tick {t})");
		var land = log.Throws.Single(x => x.e.Outcome == ThrowOutcome.Land);
		Assert.Equal((LandTick, 120), (land.tick, land.e.Damage), "lands after the window for 120");
		Assert.Equal(880, m.P2.Health, "120 damage (C4)");
		Assert.Equal(1, log.Hits.Count(h => h.e.Attacker == 0 && h.e.Damage == 120 && !h.e.Blocked), "a landed throw raises one Hit");
		Assert.Equal(40, downFrames.Count, "standard knockdown is 40 world frames (E4)");
		const int Stop = 12; // heavy hitstop on the landing (E12, V2)
		Assert.Equal(52, states.Count(s => s == FighterState.Knockdown), "40 frames + 12 hitstop ticks");
		Assert.Equal(FighterState.Knockdown, states[LandTick + Stop + 39 - 1], "still down on the 40th frame");
		Assert.Equal(FighterState.Idle, states[LandTick + Stop + 40 - 1], "acts again 40 frames (+ hitstop) after landing");
		Assert.True(m.P1.Actionable, "thrower recovered");
	}

	[Test]
	public static void Throw_KnockdownCannotBeHit_OrThrownAgain()
	{
		var log = new Log();
		var m = Setup(log);
		// P1 throws, then throws again on landing + 21 (thrower actionable again) while P2 is still down.
		Run(m, 60, t => t == 1 || t == LandTick + 21 ? THROW : 0, None, t =>
		{
			if (t == LandTick + 20) m.P2.X = m.P1.X + m.Config.BodyWidth; // back in range (the throw slid P2 away)
			if (t == LandTick + 26) Assert.Equal(FighterState.Knockdown, m.P2.State, "P2 still down when the second grab is active");
		});
		Assert.Equal(1, log.Throws.Count(x => x.e.Outcome == ThrowOutcome.Grab), "a knocked-down fighter can't be grabbed");
	}

	[Test]
	public static void Throw_Whiffs_OutOfRange()
	{
		var log = new Log();
		var m = Setup(log);
		m.P1.X = -400 * SimConfig.Scale;
		m.P2.X = 400 * SimConfig.Scale;
		var t = ThrowMove();
		Run(m, t.TotalFrames, ThrowOn1, None);
		Assert.True(log.Throws.Count == 0 && m.P2.Health == 1000, "nothing in range: whiff");
		Assert.True(m.P1.State == FighterState.Attack && m.P1.MoveFrame == t.TotalFrames, "whiff plays the whole grab clip");
		m.Step(Idle, Idle);
		Assert.True(m.P1.Actionable, "acts on tick Total+1");
	}

	// --- Block and throwability --------------------------------------------------------------

	[Test]
	public static void Throw_IgnoresStandingAndCrouchingBlock()
	{
		foreach (var guard in new[] { R, R | D }) // P2 faces left: back = Right
		{
			var log = new Log();
			var m = Setup(log, cornered: true);
			Run(m, LandTick, ThrowOn1, _ => guard, t => { if (t == GrabTick - 1) Assert.True(m.P2.Guarding, "P2 is guarding"); });
			Assert.True(log.Has(ThrowOutcome.Land), $"throw beats guard {guard}");
			Assert.Equal(880, m.P2.Health, "full damage through block");
		}
	}

	[Test]
	public static void Throw_CannotGrab_Airborne_Hitstun_Blockstun_Knockdown()
	{
		{
			var log = new Log();
			var m = Setup(log);
			Run(m, 30, ThrowOn1, t => t == 1 ? U : 0); // P2 jumps on the same tick
			Assert.True(!log.Has(ThrowOutcome.Grab) && m.P2.Health == 1000, "an airborne fighter can't be thrown");
		}
		foreach (var s in new[] { FighterState.Hitstun, FighterState.Blockstun, FighterState.Knockdown })
		{
			var log = new Log();
			var m = Setup(log);
			m.P2.State = s;
			m.P2.StunLeft = 30;
			Run(m, 20, ThrowOn1, None);
			Assert.True(!log.Has(ThrowOutcome.Grab), $"a fighter in {s} can't be thrown");
		}
		var f = new Fighter();
		f.Reset(0, 1, 1000);
		Assert.True(Match.Throwable(f), "idle is throwable");
		f.State = FighterState.Dash;
		Assert.True(Match.Throwable(f), "dashing is throwable");
		f.State = FighterState.Thrown;
		Assert.True(!Match.Throwable(f), "already thrown is not");
	}

	// --- Break ---------------------------------------------------------------------------------

	[Test]
	public static void Throw_BreaksOnlyInsideTheWindow()
	{
		for (int press = GrabTick - 1; press <= LandTick; press++)
		{
			var log = new Log();
			var m = Setup(log);
			int gapBefore = 0;
			Run(m, 60, ThrowOn1, t => t == press ? THROW : 0, t =>
			{
				if (t == press - 1) gapBefore = m.P2.X - m.P1.X;
				if (t == press && log.Has(ThrowOutcome.Break))
				{
					Assert.True(m.P1.State == FighterState.Idle && m.P2.State == FighterState.Idle, "break frees both");
					Assert.True(m.P2.X - m.P1.X > gapBefore, "break pushes them apart");
				}
			});
			bool inside = press > GrabTick && press <= GrabTick + 7;
			Assert.Equal(inside, log.Has(ThrowOutcome.Break), $"press on tick {press} breaks = {inside}");
			Assert.Equal(inside ? 1000 : 880, m.P2.Health, $"press on tick {press}: damage");
			if (inside)
			{
				Assert.True(!log.Has(ThrowOutcome.Land), "broken throw never lands");
				Assert.True(log.Throws.Count(x => x.e.Outcome == ThrowOutcome.Grab) == 1, "the break press doesn't start a new throw");
				Assert.True(log.Hits.Count == 0, "the break press doesn't come out as a jab");
			}
		}
	}

	[Test]
	public static void Throw_OneButtonDoesNotBreak()
	{
		foreach (var b in new[] { LP, LK, InputBits.HeavyPunch | InputBits.HeavyKick })
		{
			var log = new Log();
			var m = Setup(log);
			Run(m, LandTick, ThrowOn1, t => t == GrabTick + 3 ? b : 0);
			Assert.True(log.Has(ThrowOutcome.Land) && !log.Has(ThrowOutcome.Break), $"{b} inside the window does not break");
		}
	}

	// --- Priority ------------------------------------------------------------------------------

	[Test]
	public static void Throw_VsThrow_SameFrameClashes_LaterOneIsGrabbed()
	{
		var log = new Log();
		var m = Setup(log);
		Run(m, 30, ThrowOn1, ThrowOn1);
		Assert.True(log.Throws.Single().e.Outcome == ThrowOutcome.Clash && log.Throws.Single().tick == GrabTick, "same-frame grabs clash");
		Assert.True(m.P1.Health == 1000 && m.P2.Health == 1000, "a clash deals no damage");

		log = new Log();
		m = Setup(log);
		Run(m, 30, ThrowOn1, t => t == 2 ? THROW : 0); // P2 one frame late: grabbed in its startup
		Assert.True(log.Throws[0].e.Attacker == 0 && log.Has(ThrowOutcome.Land), "the earlier throw wins");
	}

	[Test]
	public static void Throw_VsStrike()
	{
		// Ryo LK: startup 5, first active frame 6 = the grab frame. Control: alone, it hits P1 on tick 6.
		var log = new Log();
		var m = Setup(log);
		Run(m, 10, None, t => t == 1 ? LK : 0);
		Assert.True(log.Hits.Any(h => h.e.Attacker == 1 && h.tick == GrabTick), "control: P2's LK connects on tick 6");

		// Same frame: the grab wins (E12) and the strike never lands.
		log = new Log();
		m = Setup(log);
		Run(m, 30, ThrowOn1, t => t == 1 ? LK : 0);
		Assert.True(log.Has(ThrowOutcome.Land) && !log.Hits.Any(h => h.e.Attacker == 1), "grab beats a strike active on the same frame");
		Assert.Equal(1000, m.P1.Health, "thrower untouched");

		// A faster strike (LP, startup 4) hits during the throw's startup: the throw never grabs.
		log = new Log();
		m = Setup(log);
		Run(m, 30, ThrowOn1, t => t == 1 ? LP : 0);
		Assert.True(!log.Has(ThrowOutcome.Grab) && log.Hits.Any(h => h.e.Attacker == 1), "a strike in the throw's startup beats it");
	}

	[Test]
	public static void Throw_CanKO()
	{
		var log = new Log();
		var m = Setup(log);
		m.P2.Health = 100;
		Run(m, LandTick, ThrowOn1, None);
		Assert.True(m.P2.KnockedOut && m.Winner == 0 && m.Phase == MatchPhase.KoSlowMo, "a landed throw can KO");
	}

	// --- Input leniency (E12) ------------------------------------------------------------------

	const InputBits MP = InputBits.MediumPunch, HP = InputBits.HeavyPunch, BURST = LP | MP | HP;

	/// <summary>Presses <paramref name="first"/> on tick 1 and <paramref name="second"/> on tick 1+gap, holding both.</summary>
	static Func<int, InputBits> Pair(InputBits first, InputBits second, int gap) =>
		t => (t >= 1 ? first : 0) | (t >= 1 + gap ? second : 0);

	[Test]
	public static void Throw_LpLkWithinThreeTicks_BothOrders_FourIsAMiss()
	{
		string throwId = ThrowMove().Id;
		foreach (var (first, second) in new[] { (LP, LK), (LK, LP) })
			for (int gap = 0; gap <= 4; gap++)
			{
				var log = new Log();
				var m = Setup(log);
				Run(m, 40, Pair(first, second, gap), None, t =>
				{
					if (gap <= 3 && t == 1 + gap)
						Assert.True(m.P1.CurrentMove?.IsThrow == true && m.P1.MoveFrame == 1, $"{first} then {second} +{gap}: throw frame 1 on the second press (a started normal is cancelled)");
				});
				bool throws = gap <= 3;
				Assert.Equal(throws, log.Has(ThrowOutcome.Grab), $"{first} then {second} {gap} ticks apart throws = {throws}");
				if (throws)
				{
					Assert.Equal(GrabTick + gap, log.Throws.First(x => x.e.Outcome == ThrowOutcome.Grab).tick, $"gap {gap}: grab 5 ticks after the second press");
					Assert.True(log.Hits.All(h => h.e.MoveId == throwId), $"gap {gap}: the cancelled normal never hits");
				}
				else Assert.True(log.Hits.Any(h => h.e.Attacker == 0 && h.e.MoveId != throwId), "gap 4: the first button's normal plays out");
			}
	}

	[Test]
	public static void Throw_CancelsOnlyANormalStillInStartup()
	{
		// A wider window (config) lets the second press arrive after the jab's startup (LP: active from frame 5).
		var cfg = SimConfig.Default with { ThrowPressWindow = 8 };
		var log = new Log();
		var m = Setup(log, cfg: cfg);
		Run(m, 40, Pair(LP, LK, 3), None); // second press on jab frame 4: startup
		Assert.True(log.Has(ThrowOutcome.Grab), "inside startup: cancelled into the throw");

		log = new Log();
		m = Setup(log, cfg: cfg);
		Run(m, 40, Pair(LP, LK, 4), None); // second press on jab frame 5: active
		Assert.True(!log.Throws.Any(), "past startup: the throw input is ignored");
		Assert.True(log.Hits.Any(h => h.e.Attacker == 0), "the jab plays on and hits");

		log = new Log();
		m = Setup(log);
		Run(m, 40, t => t == 1 ? MP : t == 2 ? LP | LK : 0, None); // MP is not a throw button
		Assert.True(!log.Throws.Any(), "a normal from a non-throw button is not cancelled");
	}

	sealed class NoCommandParser : ICommandParser
	{
		public InputCommand Parse(InputBuffer buffer, int facing) => InputCommand.None;
	}

	[Test]
	public static void Throw_ReadFromRawInput_SameForAnyScheme()
	{
		// Kihon (YOK-23) only swaps the parser; the throw check reads the raw buffer before it (K3).
		var log = new Log();
		var m = Setup(log);
		m.P1.Input.Parser = new NoCommandParser();
		Run(m, 30, Pair(LK, LP, 2), None);
		Assert.True(log.Has(ThrowOutcome.Grab) && log.Has(ThrowOutcome.Land), "throw with a parser that yields no commands");
	}

	// --- Meter, hitstop, burst (YOK-20 integration) ---------------------------------------------

	[Test]
	public static void Throw_LandBuildsMeter_HeavyHitstopAndShake()
	{
		var log = new Log();
		var m = Setup(log);
		Run(m, LandTick, ThrowOn1, None);
		var imp = log.Impacts.Single();
		Assert.True(imp.Strength == HitStrength.Heavy && imp.Hitstop == 12 && imp.Shake && !imp.Blocked, "a landed throw is a heavy impact: 12 frames, shakes (E12, V2, V3)");
		Assert.Equal(12, m.HitstopLeft, "hitstop starts on the landing tick");
		Assert.Equal((6, 3), (m.P1.Meter, m.P2.Meter), "C5: thrower +6, thrown +3");

		log = new Log();
		m = Setup(log);
		Run(m, 30, ThrowOn1, t => t == GrabTick + 2 ? THROW : 0);
		Assert.True(log.Has(ThrowOutcome.Break) && log.Impacts.Count == 0, "a break is no connect");
		Assert.Equal((0, 0), (m.P1.Meter, m.P2.Meter), "a break builds no meter");
	}

	[Test]
	public static void Throw_CannotGrabABurstingFighter()
	{
		var f = new Fighter();
		f.Reset(0, 1, 1000);
		f.State = FighterState.Burst;
		Assert.True(!Match.Throwable(f), "burst invulnerability also stops throws");

		var log = new Log();
		var m = Setup(log);
		m.P2.State = FighterState.Hitstun;
		m.P2.StunLeft = 30;
		int x1 = m.P1.X;
		Run(m, 40, t => t == 2 || t == 30 ? THROW : 0, t => t == 1 ? BURST : 0, t =>
		{
			if (t == 1 || t == 29) m.P1.X = x1; // undo the burst pushback: P1 back in throw range
		});
		Assert.Equal(1, log.Bursts.Single(), "P2 burst out of hitstun");
		var grabs = log.Throws.Where(x => x.e.Outcome == ThrowOutcome.Grab).Select(x => x.tick).ToList();
		Assert.True(!grabs.Contains(GrabTick + 1), "a throw active during the 20 invulnerable frames whiffs");
		Assert.True(grabs.Contains(30 + GrabTick - 1), "control: after the burst the same throw grabs");
	}

	[Test]
	public static void Throw_ThrownFighterBreaks_NeverBursts_HitstunBurstWins()
	{
		var log = new Log();
		var m = Setup(log);
		Run(m, LandTick, ThrowOn1, t => t == GrabTick + 2 ? BURST : 0);
		Assert.True(log.Bursts.Count == 0 && !m.P2.BurstUsed && log.Has(ThrowOutcome.Land), "LP+MP+HP while thrown: no burst, no break");

		log = new Log();
		m = Setup(log);
		Run(m, 30, ThrowOn1, t => t == GrabTick + 2 ? BURST | LK : 0);
		Assert.True(log.Bursts.Count == 0 && !m.P2.BurstUsed && log.Has(ThrowOutcome.Break), "thrown: LP+LK(+MP+HP) breaks instead");

		log = new Log();
		m = Setup(log);
		m.P2.State = FighterState.Hitstun;
		m.P2.StunLeft = 30;
		Run(m, 10, None, t => t == 1 ? BURST | LK : 0);
		Assert.True(log.Bursts.SequenceEqual(new[] { 1 }) && !log.Throws.Any(), "in hitstun LP+LK+MP+HP bursts (burst wins when legal)");
	}


	// --- Overlay and determinism ---------------------------------------------------------------

	[Test]
	public static void Throw_DebugOverlayShowsThrowboxes()
	{
		foreach (bool swap in new[] { false, true })
		{
			var log = new Log();
			var m = Setup(log);
			if (swap) { (m.P1.X, m.P2.X) = (m.P2.X, m.P1.X); m.P1.Facing = -1; m.P2.Facing = 1; }
			var t = ThrowMove();
			m.P2.X += (swap ? -1 : 1) * 400 * SimConfig.Scale; // out of range so the boxes show on every active frame
			Run(m, t.TotalFrames, ThrowOn1, None, tick =>
			{
				var boxes = DebugBoxes.For(m, 0).Where(b => b.Kind == DebugBoxKind.Throw).ToList();
				var want = t.Throwboxes.Where(tb => t.IsActive(tick) && tb.Covers(tick))
					.Select(tb => Match.WorldBox(m.P1, tb.Box)).ToList();
				Assert.Equal(want.Count, boxes.Count, $"throwbox count on frame {tick}");
				for (int i = 0; i < want.Count; i++)
					Assert.Equal(want[i], (boxes[i].X0, boxes[i].Y0, boxes[i].X1, boxes[i].Y1), $"throwbox on frame {tick} (facing {m.P1.Facing})");
			});
			Assert.True(!log.Has(ThrowOutcome.Grab), "out of range");
		}
		{
			var log = new Log();
			var m = Setup(log);
			Run(m, GrabTick, ThrowOn1, None);
			Assert.True(!DebugBoxes.For(m, 0).Any(b => b.Kind == DebugBoxKind.Throw), "no throwbox once the grab connected");
		}
	}

	[Test]
	public static void Throw_Deterministic_RandomInputs()
	{
		(ulong hash, int throws) Run1()
		{
			var log = new Log();
			var m = Setup(log);
			var rng = new SimRng(19);
			InputBits Rand()
			{
				var b = (InputBits)rng.Next(16);
				int r = rng.Next(8);
				if (r == 0) b |= THROW;
				else if (r == 1) b |= LP;
				else if (r == 2) b |= LK;
				return b & ~U | (rng.Next(20) == 0 ? U : 0);
			}
			for (int t = 0; t < 6000 && m.Phase != MatchPhase.Over; t++)
			{
				m.Step(In(Rand()), In(Rand()));
				var ds = new[] { m.P1, m.P2 }.Where(f => f.State == FighterState.Thrown).ToList();
				Assert.True(ds.Count <= 1, "never both thrown");
			}
			return (m.StateHash(), log.Throws.Count);
		}
		var a = Run1();
		var b = Run1();
		Assert.True(a.throws > 0, "random play produced throws");
		Assert.Equal(a, b, "same inputs, same hash and throw count");
	}

	[Test]
	public static void Scene_LoadsFixtureThrow()
	{
		Assert.Equal(1, FightScene.LoadMoves().Count(mv => mv.IsThrow), "the fight scene has the TEST FIXTURE throw while data/moves/ has none");
	}
}
