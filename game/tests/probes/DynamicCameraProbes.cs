using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>Test Breaker probes for YOK-129 (AM9 dynamic camera). Expected values come from rules.md E1, V3, V7, F4.</summary>
public static class DynamicCameraProbes
{
	static FighterInput In(InputBits b) => new(b);
	static MoveData[] Moves() =>
		MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureNormalsDir))
			.Concat(MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureThrowsDir))).ToArray();
	static SpecialData[] Specials() => SpecialLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureSpecialsDir));
	static Match NewMatch()
	{
		var m = new Match(null, Moves(), Moves());
		foreach (var f in m.Fighters) FightScene.EquipStarters(f, Specials());
		return m;
	}
	static readonly InputBits[] Dirs = { InputBits.Left, InputBits.Right, InputBits.None, InputBits.Up | InputBits.Left, InputBits.Up | InputBits.Right,
		InputBits.Up, InputBits.Down, InputBits.Left, InputBits.Right };
	static readonly InputBits[] Btn = { InputBits.None, InputBits.LightPunch, InputBits.MediumPunch, InputBits.HeavyPunch, InputBits.LightKick, InputBits.MediumKick, InputBits.HeavyKick,
		InputBits.LightPunch | InputBits.LightKick, InputBits.HeavyPunch | InputBits.HeavyKick };

	// E1 / F4 / V7: every tick both models in frame, heads under the HUD, view on stage; eased width and height do not jump
	// by more than a few units a tick even when the hard floors act.
	[Test]
	public static void Fuzz_FramingStageAndSmoothness_EveryTick()
	{
		var c = SimConfig.Default;
		int corner = c.StageHalfWidth - c.BodyWidth / 2;
		float maxDW = 0, maxDH = 0; string worst = "";
		foreach (int start in new[] { -corner, corner, 0 })
			for (int seed = 1; seed <= 6; seed++)
			{
				var m = NewMatch();
				m.P1.X = Math.Clamp(start, -corner, corner);
				m.P2.X = Math.Clamp(start + (start >= 0 ? -1 : 1) * 300, -corner, corner);
				var rng = new Random(seed * 31 + start);
				var cam = new DynamicCamera(c);
				float pw = 0, ph = 0; bool first = true; int hold = 0; InputBits d1 = 0, b1 = 0, d2 = 0, b2 = 0;
				for (int t = 0; t < 2500; t++)
				{
					if (hold-- <= 0) { hold = rng.Next(1, 18); d1 = Dirs[rng.Next(Dirs.Length)]; b1 = Btn[rng.Next(Btn.Length)]; d2 = Dirs[rng.Next(Dirs.Length)]; b2 = Btn[rng.Next(Btn.Length)]; }
					m.Step(In(d1 | b1), In(d2 | b2));
					var s = DynamicCamera.SubjectOf(m);
					cam.Step(s);
					Assert.True(cam.InFrame(s), $"model out of frame / head under HUD: start {start} seed {seed} tick {t}");
					Assert.True(cam.OnStage(), $"view past the stage: start {start} seed {seed} tick {t} cx {cam.CenterX} w {cam.Width}");
					if (!first)
					{
						float dw = Math.Abs(cam.Width - pw), dh = Math.Abs(cam.HeightMeters - ph);
						if (dw > maxDW) { maxDW = dw; worst = $"start {start} seed {seed} tick {t}"; }
						maxDH = Math.Max(maxDH, dh);
					}
					first = false; pw = cam.Width; ph = cam.HeightMeters;
				}
			}
		var cfg = CameraConfig.Default;
		float allowW = (cfg.CloseViewWidth > 0 ? (c.ViewWidth / (float)SimConfig.Scale - cfg.CloseViewWidth) : 0) * 0.25f;
		GD.Print($"[probe] maxDW {maxDW} ({worst}) maxDH {maxDH}");
		Assert.True(maxDW <= allowW, $"width jumped {maxDW} in one tick (allowed {allowW}) at {worst}");
		Assert.True(maxDH <= 0.15f, $"height jumped {maxDH} m in one tick");
	}

	// V7: KO push-in at both walls still frames both models.
	[Test]
	public static void KoPushIn_AtWalls_BothModelsInFrame()
	{
		var c = SimConfig.Default;
		int corner = c.StageHalfWidth - c.BodyWidth / 2;
		foreach (int side in new[] { -1, 1 })
			for (float ko = 0; ko <= 1.0001f; ko += 0.1f)
				foreach (float gap in new[] { 100f, 400f, 900f })
				{
					var m = new Match();
					m.P1.X = side * corner; m.P2.X = side * corner - side * (int)(gap * SimConfig.Scale);
					m.P1.Facing = -side; m.P2.Facing = side;
					var s = DynamicCamera.SubjectOf(m) with { KoProgress = ko };
					var cam = new DynamicCamera(c);
					cam.Snap(s); for (int i = 0; i < 100; i++) cam.Step(s);
					Assert.True(cam.InFrame(s), $"KO push-in loses a model: side {side} ko {ko} gap {gap}");
					Assert.True(cam.OnStage(), $"KO push-in off stage: side {side} ko {ko} gap {gap}");
				}
	}

	// Determinism: two cameras fed identical ticks have bit-identical transforms; a different config does not change StateHash.
	[Test]
	public static void Determinism_BitIdentical_AndHashIndependentOfConfig()
	{
		ulong Run(CameraConfig cfg, List<(float, float, float, float)> log)
		{
			var m = NewMatch(); var cam = new DynamicCamera(m.Config, cfg); var rng = new Random(5);
			for (int t = 0; t < 600; t++)
			{
				m.Step(In(Dirs[rng.Next(Dirs.Length)] | Btn[rng.Next(Btn.Length)]), In(Dirs[rng.Next(Dirs.Length)]));
				cam.Step(DynamicCamera.SubjectOf(m)); log.Add(cam.Transform);
			}
			return m.StateHash();
		}
		var a = new List<(float, float, float, float)>(); var b = new List<(float, float, float, float)>(); var c2 = new List<(float, float, float, float)>();
		ulong h1 = Run(CameraConfig.Default, a), h2 = Run(CameraConfig.Default, b), h3 = Run(CameraConfig.Default with { CloseViewWidth = 900f, ZoomEase = 0.3f, KoPushIn = 0.3f }, c2);
		Assert.True(a.SequenceEqual(b), "same ticks, different transform");
		Assert.True(h1 == h2 && h1 == h3, "StateHash depends on camera config");
		Assert.True(!a.SequenceEqual(c2), "config has no effect on transform");
	}

	// V3: shake of 2-4 px at the current zoom, mid-easing included: converting px through the width gives width/1920 units per px.
	[Test]
	public static void Shake_TwoToFourPx_AtEveryWidthMidEasing()
	{
		var cam = new DynamicCamera(SimConfig.Default);
		var close = new Match(); close.P1.X = -50 * SimConfig.Scale; close.P2.X = 50 * SimConfig.Scale;
		var far = new Match(); far.P1.X = -400 * SimConfig.Scale; far.P2.X = 400 * SimConfig.Scale;
		cam.Snap(DynamicCamera.SubjectOf(close));
		var sf = DynamicCamera.SubjectOf(far);
		for (int t = 0; t < 60; t++)
		{
			cam.Step(sf);
			foreach (var px in new[] { (2, 0), (0, 4), (-3, 2), (4, -4) })
			{
				var (h, v) = FightScene.ShakeOffsetMeters(cam.Width, px);
				float screenPxH = h * 200f / cam.Width * 1920f, screenPxV = v * 200f / cam.Width * 1920f;
				Assert.True(Math.Abs(screenPxH - px.Item1) < 1e-2f && Math.Abs(screenPxV - px.Item2) < 1e-2f, $"shake px drifts at width {cam.Width}");
			}
		}
	}

	// Stage coverage: at the widest zoom with the jump pad, the view stays on stage even for a corner jump at max separation.
	[Test]
	public static void StageCoverage_CornerJumps_AtWidestZoom()
	{
		var c = SimConfig.Default;
		int corner = c.StageHalfWidth - c.BodyWidth / 2;
		foreach (int side in new[] { -1, 1 })
			foreach (float jump in new[] { 0f, 40f, 120f, 250f })
				for (float sep = 100; sep <= c.MaxSeparation / (float)SimConfig.Scale; sep += 100)
				{
					var m = new Match();
					m.P1.X = side * corner; m.P2.X = side * corner - side * (int)(sep * SimConfig.Scale);
					m.P1.Y = (int)(jump * SimConfig.Scale); m.P2.Y = (int)(jump * SimConfig.Scale);
					var s = DynamicCamera.SubjectOf(m);
					var cam = new DynamicCamera(c); cam.Snap(s);
					Assert.True(cam.OnStage() && cam.InFrame(s), $"side {side} jump {jump} sep {sep}: onStage {cam.OnStage()} inFrame {cam.InFrame(s)}");
				}
	}

	// Refused/backwards: Place then Step from a different state must still satisfy the hard floors on the very first Step.
	[Test]
	public static void FirstStepAfterWildPlacement_HardFloorsHold()
	{
		var m = new Match(); m.P1.X = -300 * SimConfig.Scale; m.P2.X = 300 * SimConfig.Scale; m.P1.Y = 200 * SimConfig.Scale;
		var s = DynamicCamera.SubjectOf(m);
		var cam = new DynamicCamera(SimConfig.Default);
		cam.Place(300f, 0.2f, 1500f); cam.Step(s);
		Assert.True(cam.InFrame(s) && cam.OnStage(), "floors not applied after a wild Place");
	}
}
