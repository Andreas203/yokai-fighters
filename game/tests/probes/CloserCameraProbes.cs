using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>Test Breaker probes for YOK-120 (AM7 closer camera). Expected values come from rules.md E1, V3, F4, R7.</summary>
public static class CloserCameraProbes
{
	static FighterInput In(InputBits b) => new(b);

	static MoveData[] Moves() =>
		MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureNormalsDir))
			.Concat(MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureThrowsDir))).ToArray();
	static SpecialData[] Specials() => SpecialLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureSpecialsDir));

	static Match NewMatch(SimConfig? cfg = null)
	{
		var m = new Match(cfg, Moves(), Moves());
		foreach (var f in m.Fighters) FightScene.EquipStarters(f, Specials());
		return m;
	}

	// E1: fighters at most 1,040 apart AND both models in frame. Fuzz with throws, specials, jumps, dashes, from
	// corner starts and mid-stage, over several seeds; check every tick, including side switches (jump-overs).
	[Test]
	public static void Walls_HoldUnderEverything_CornersAndMid()
	{
		var c = SimConfig.Default;
		int corner = c.StageHalfWidth - c.BodyWidth / 2;
		InputBits[] dirs = { InputBits.Left, InputBits.Right, InputBits.None, InputBits.Up | InputBits.Left, InputBits.Up | InputBits.Right,
			InputBits.Up, InputBits.Down, InputBits.Left, InputBits.Right };
		InputBits[] btn = { InputBits.LightPunch, InputBits.MediumPunch, InputBits.HeavyPunch, InputBits.LightKick, InputBits.MediumKick, InputBits.HeavyKick,
			InputBits.LightPunch | InputBits.LightKick, InputBits.HeavyPunch | InputBits.HeavyKick };
		int switches = 0;
		foreach (int start in new[] { -corner, corner, 0, -corner / 2 })
			for (int seed = 1; seed <= 6; seed++)
			{
				var m = NewMatch();
				m.P1.X = start; m.P2.X = start + (start >= 0 ? -1 : 1) * 400;
				m.P1.X = Math.Clamp(m.P1.X, -corner, corner); m.P2.X = Math.Clamp(m.P2.X, -corner, corner);
				var rng = new Random(seed * 7919 + start);
				int lastSide = Math.Sign(m.P2.X - m.P1.X);
				for (int t = 0; t < 2500; t++)
				{
					var inp = new FighterInput[2];
					for (int i = 0; i < 2; i++)
					{
						var b = dirs[rng.Next(dirs.Length)];
						if (rng.Next(5) == 0) b |= btn[rng.Next(btn.Length)];
						inp[i] = rng.Next(40) == 0 ? FighterInput.Special((SpecialSlot)rng.Next(2), rng.Next(4) == 0, b) : In(b);
					}
					m.Step(inp[0], inp[1]);
					int sep = Math.Abs(m.P2.X - m.P1.X);
					Assert.True(sep <= c.MaxSeparation, $"start {start} seed {seed} tick {t}: separation {sep} > {c.MaxSeparation}");
					Assert.True(FightCamera.BothInView(m), $"start {start} seed {seed} tick {t}: a model is out of frame (P1 {m.P1.X} {m.P1.State}, P2 {m.P2.X} {m.P2.State})");
					Assert.True(Math.Abs(m.P1.X) <= corner && Math.Abs(m.P2.X) <= corner, "stays on the stage");
					int side = Math.Sign(m.P2.X - m.P1.X);
					if (side != 0 && lastSide != 0 && side != lastSide) switches++;
					if (side != 0) lastSide = side;
					if (m.Phase == MatchPhase.Over) m.Reset();
				}
			}
		Assert.True(switches > 0, $"the fuzz covered side switches ({switches})");
	}

	// E1: the exact wall: one unit past is refused by the walls, any single-tick teleport-ish displacement is pulled in.
	[Test]
	public static void Walls_PulledBackFromDisplacementPastLimit()
	{
		var c = SimConfig.Default;
		foreach (int sign in new[] { -1, 1 })
		{
			var m = NewMatch();
			m.P1.X = -sign * 100; m.P2.X = sign * (c.MaxSeparation - 100);
			m.Step(FighterInput.None, FighterInput.None);
			Assert.True(Math.Abs(m.P2.X - m.P1.X) <= c.MaxSeparation, "idle exactly at the wall stays legal");
			var m2 = NewMatch();
			m2.P1.X = -sign * 100; m2.P2.X = sign * (c.MaxSeparation - 100) ;
			for (int t = 0; t < 30; t++) m2.Step(In(sign > 0 ? InputBits.Left : InputBits.Right), In(sign > 0 ? InputBits.Right : InputBits.Left));
			Assert.Equal(c.MaxSeparation, Math.Abs(m2.P2.X - m2.P1.X), "walking apart holds at exactly 1,040 (not 1,039, not 1,041)");
		}
	}

	// F4/E1: the view is fixed 1,440 and centred on the midpoint: with fighters 1,040 apart in the corner, models 240/200 inside.
	[Test]
	public static void Camera_ExactCornerExtents_BothFacingsAndSides()
	{
		var c = SimConfig.Default;
		Assert.Equal(1440 * SimConfig.Scale, c.ViewWidth, "E1: one screen is 1,440 units");
		Assert.Equal(1040 * SimConfig.Scale, c.MaxSeparation, "E1: 1,040 apart");
		Assert.Equal(3840 * SimConfig.Scale, c.StageHalfWidth * 2, "E1: stage 3,840 wide");
		int corner = c.StageHalfWidth - c.BodyWidth / 2;
		foreach (int side in new[] { -1, 1 })
			foreach (int f1 in new[] { -1, 1 })
				foreach (int f2 in new[] { -1, 1 })
				{
					var m = NewMatch();
					m.P1.X = side * corner; m.P2.X = side * (corner - c.MaxSeparation);
					m.P1.Facing = f1; m.P2.Facing = f2;
					Assert.True(FightCamera.BothInView(m), $"cornered side {side}, facings {f1}/{f2}: both in frame at max separation");
				}
	}

	// E1: projectile never despawns off-screen before it passes the opponent, in either corner and mid-stage.
	[Test]
	public static void Projectile_NotOffScreenBeforeReachingOpponent()
	{
		var c = SimConfig.Default;
		int corner = c.StageHalfWidth - c.BodyWidth / 2;
		foreach (var (p1x, p2x) in new[] { (corner - c.MaxSeparation, corner), (-corner, -corner + c.MaxSeparation), (-c.MaxSeparation / 2, c.MaxSeparation / 2),
			(-corner, -corner + 600 * SimConfig.Scale), (corner - 600 * SimConfig.Scale, corner) })
			foreach (bool flip in new[] { false, true })
			{
				var m = NewMatch();
				int a = flip ? -p2x : p1x, b = flip ? -p1x : p2x; // mirror
				// owner is always P1 firing at P2 on its facing side
				m.P1.X = flip ? -p2x : p1x; m.P2.X = flip ? -p1x : p2x;
				if (m.P2.X < m.P1.X) { m.P1.Facing = -1; m.P2.Facing = 1; }
				m.P2.State = FighterState.Knockdown; m.P2.StunLeft = 100000; // flies past, not consumed
				var offs = new List<ProjectileEvent>();
				m.ProjectileChanged += (_, e) => { if (e.Kind == ProjectileEventKind.OffScreen) offs.Add(e); };
				m.Step(FighterInput.Special(SpecialSlot.A), FighterInput.None);
				for (int t = 0; t < 300 && offs.Count == 0; t++) m.Step(FighterInput.None, FighterInput.None);
				Assert.True(offs.Count == 1, $"({m.P1.X},{m.P2.X}): wave leaves the screen exactly once ({offs.Count})");
				int dir = Math.Sign(m.P2.X - m.P1.X);
				Assert.True((offs[0].X - m.P2.X) * dir >= 0, $"({m.P1.X},{m.P2.X}) flip {flip}: despawned at {offs[0].X}, before reaching the opponent at {m.P2.X}");
			}
	}

	// V3: shake 2-4 screen px on both axes for every pattern frame, at the default and at odd view widths (no integer truncation to 0/1 px).
	[Test]
	public static void Shake_TwoToFourPx_AllFrames_IncludingNegativeAxes()
	{
		var c = SimConfig.Default;
		float mPerPx = FightScene.ToMeters(c.ViewWidth) / FightScene.DesignCanvasWidthPx;
		var s = new ScreenShake(60);
		s.Start();
		for (int t = 0; t < 60; t++, s.Advance())
		{
			var (h, v) = FightScene.ShakeOffsetMeters(c, s.OffsetPx);
			float px = Math.Abs(h) / mPerPx, py = Math.Abs(v) / mPerPx;
			Assert.True(px >= 2f - 1e-3f && px <= 4f + 1e-3f && py >= 2f - 1e-3f && py <= 4f + 1e-3f, $"frame {t}: {px:0.00} x {py:0.00} px");
			Assert.True(Math.Sign(h) == Math.Sign(s.OffsetPx.x) && Math.Sign(v) == Math.Sign(s.OffsetPx.y), "sign preserved");
		}
		// idle gives exactly zero
		s.Stop();
		Assert.Equal(0f, FightScene.ShakeOffsetMeters(c, s.OffsetPx).H, "idle shake is zero");
	}

	// F4: camera distance gives a visible width of ViewWidth at the plane: a narrower view is closer.
	[Test]
	public static void CameraDistance_ScalesWithViewWidth()
	{
		var wide = SimConfig.Default with { ViewWidth = 1920 * SimConfig.Scale };
		float dn = FightScene.CameraDistance(SimConfig.Default), dw = FightScene.CameraDistance(wide);
		Assert.True(Math.Abs(dn / dw - 0.75f) < 1e-3f, $"distance ratio {dn / dw} should be 1440/1920 = 0.75 (same FOV)");
	}

	// R7 / E1: trial start distance 1,040 accepted, 1,041 rejected; also in a goal-step distance bound.
	[Test]
	public static void Trial_StartDistanceBoundary_1040In_1041Out()
	{
		string f = File.ReadAllText(Path.Combine(ContentPaths.FixturesDir, "trials", "test-spirit-wave-trial.json"));
		Assert.Equal(1040, TrialLoader.Parse(f.Replace("\"distance\": 600,", "\"distance\": 1040,")).Distance, "1,040 accepted");
		Assert.Equal(120, TrialLoader.Parse(f.Replace("\"distance\": 600,", "\"distance\": 120,")).Distance, "120 accepted");
		foreach (int bad in new[] { 1041, 1520, 119, 0, -1 })
		{
			bool threw = false;
			try { TrialLoader.Parse(f.Replace("\"distance\": 600,", $"\"distance\": {bad},")); } catch (FormatException) { threw = true; }
			Assert.True(threw, $"start distance {bad} rejected");
		}
		TrialLoader.Parse(f.Replace("\"max\": 900", "\"max\": 1040"));
		foreach (string repl in new[] { "\"max\": 1041", "\"min\": 1041", "\"max\": 1520" })
		{
			bool threw = false;
			string src = repl.StartsWith("\"max") ? f.Replace("\"max\": 900", repl) : f.Replace("\"min\": 400", repl);
			try { TrialLoader.Parse(src); } catch (FormatException) { threw = true; }
			Assert.True(threw, $"goal distance {repl} rejected");
		}
	}

	// A trial start at 1,040 plays out: the fighters at the declared distance are legal and in frame.
	[Test]
	public static void Trial_StartAtMaxDistance_InFrameAndLegal()
	{
		var m = NewMatch();
		m.P1.X = -520 * SimConfig.Scale; m.P2.X = 520 * SimConfig.Scale;
		for (int t = 0; t < 20; t++) m.Step(FighterInput.None, FighterInput.None);
		Assert.Equal(1040 * SimConfig.Scale, m.P2.X - m.P1.X, "start separation survives");
		Assert.True(FightCamera.BothInView(m), "in frame");
	}

	// Determinism: same seed and inputs twice give the same hash trail; a different input gives a different one.
	[Test]
	public static void Determinism_SameInputsSameHash_DifferentInputsDiffer()
	{
		ulong Run(int seed, bool tweak)
		{
			var m = NewMatch();
			var rng = new Random(seed);
			InputBits[] dirs = { InputBits.Left, InputBits.Right, InputBits.None, InputBits.Up };
			ulong h = 0;
			for (int t = 0; t < 1500; t++)
			{
				var a = In(dirs[rng.Next(4)] | (rng.Next(6) == 0 ? InputBits.HeavyKick : 0));
				var b = In(dirs[rng.Next(4)] | (rng.Next(6) == 0 ? InputBits.MediumPunch : 0));
				if (tweak && t == 700) a = In(InputBits.Left | InputBits.Left);
				if (tweak && t == 701) a = In(InputBits.Down);
				m.Step(a, b);
				h = h * 1099511628211UL ^ m.StateHash();
			}
			return h;
		}
		Assert.Equal(Run(5, false), Run(5, false), "same seed twice");
		Assert.True(Run(5, false) != Run(6, false), "different seed differs");
	}

	// Reset leaves nothing from a cornered fight: positions and camera back to the start.
	[Test]
	public static void Reset_AfterCornerFight_StartsCentred()
	{
		var m = NewMatch();
		ulong fresh = m.StateHash();
		for (int t = 0; t < 900; t++) m.Step(In(InputBits.Right), In(InputBits.Right));
		m.Reset();
		Assert.Equal(fresh, m.StateHash(), "Reset == new match");
		Assert.Equal(0, FightCamera.CenterX(m), "camera centred");
	}
}
