using System;
using Godot;
using YokaiFighters.Dev;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-133 (designer: frame for the tallest fighter in the fight): the camera frames each fight for its tallest body.
/// Ryo vs the Kitsune keeps the default camera exactly; a fight with the Oni frames his club (measured in
/// ModelExtentTests.ModelTop_FitsTheCameraFraming) under the HUD band at every zoom, corner and jump, feet still on screen.
/// Presentation only: the sim is the same whichever body shows (OniKappaBodyTests checks the hash per tick).
/// </summary>
public static class TallestFighterCameraTests
{
	static readonly SimConfig C = SimConfig.Default;
	static FighterInput In(InputBits b) => new(b);

	[Test]
	public static void ForBodies_RyoVsKitsuneKeepsTheDefault_OniRaisesTheTop()
	{
		var d = CameraConfig.Default;
		Assert.True(ReferenceEquals(d, d.ForBodies("ryo", "kitsune")), "Ryo vs the Kitsune: the very same default config");
		Assert.True(ReferenceEquals(d, d.ForBodies("ryo", "kappa")), "Ryo vs the Kappa: the default config");
		Assert.True(ReferenceEquals(d, d.ForBodies(null, null)), "no bodies: the default config");
		Assert.Equal(1.9f, d.ModelTopMeters, "the default top is unchanged (Ryo and the Kitsune)");
		Assert.Equal(0f, d.FeetClearPx, "the default camera has no feet floor");
		foreach (var (a, b) in new[] { ("ryo", "oni"), ("oni", "ryo"), ("oni", "kitsune") })
		{
			var f = d.ForBodies(a, b);
			Assert.Equal(d.TallBodyTopMeters["oni"], f.ModelTopMeters, $"{a} vs {b}: framed for the Oni's club");
			Assert.True(f with { ModelTopMeters = d.ModelTopMeters, FeetClearPx = d.FeetClearPx } == d, $"{a} vs {b}: only the model top and the feet line change");
			Assert.Equal(d.TallFeetClearPx, f.FeetClearPx, $"{a} vs {b}: the feet floor is on");
		}
	}

	[Test]
	public static void OniFraming_ClubTopUnderTheHud_FeetOnScreen_EveryZoomCornerAndJump()
	{
		Sweep(CameraConfig.Default.ForBodies("ryo", "oni"));
		Fuzz(CameraConfig.Default.ForBodies("ryo", "oni"));
	}

	/// <summary>The feet floor never engages for Ryo vs the Kitsune: their feet stay above <see cref="CameraConfig.FeetClearPx"/> everywhere, so the default framing is untouched.</summary>
	[Test]
	public static void DefaultFraming_FeetFloorNeverEngages()
	{
		Sweep(CameraConfig.Default);
		Fuzz(CameraConfig.Default);
	}

	/// <summary>Seeded random fights (knockdowns, jumps, corners): in frame, on stage, no snapping; the default camera never takes the YOK-133 path.</summary>
	static void Fuzz(CameraConfig cfg)
	{
		bool tall = !ReferenceEquals(cfg, CameraConfig.Default);
		var dirs = new[] { InputBits.None, InputBits.Left, InputBits.Right, InputBits.Up, InputBits.Down, InputBits.Up | InputBits.Right, InputBits.Up | InputBits.Left };
		int corner = C.StageHalfWidth - C.BodyWidth / 2;
		foreach (int start in new[] { -corner, 0, corner })
			for (int seed = 1; seed <= 4; seed++)
			{
				var m = FightScene.NewMatch();
				m.P1.X = start; m.P2.X = Math.Clamp(start + (start >= 0 ? -300 : 300), -corner, corner);
				var rng = new Random(seed * 97 + start);
				var cam = new DynamicCamera(C, cfg);
				int hold = 0; FighterInput i1 = FighterInput.None, i2 = FighterInput.None;
				for (int t = 0; t < 2000; t++)
				{
					if (hold-- <= 0)
					{
						hold = rng.Next(1, 18);
						i1 = rng.Next(3) == 0 ? FighterInput.Attack(rng.Next(m.P1.Moves.Length)) : In(dirs[rng.Next(dirs.Length)]);
						i2 = rng.Next(3) == 0 ? FighterInput.Attack(rng.Next(m.P2.Moves.Length)) : In(dirs[rng.Next(dirs.Length)]);
					}
					m.Step(i1, i2);
					float pw = cam.Width, ph = cam.HeightMeters;
					var s = DynamicCamera.SubjectOf(m);
					cam.Step(s);
					string where = $"start {start} seed {seed} tick {t}";
					Assert.True(cam.InFrame(s) && cam.OnStage(), $"{where}: out of frame or off the stage (width {cam.Width:0})");
					if (t > 0)
					{
						Assert.True(Math.Abs(cam.Width - pw) <= (C.ViewWidth / (float)SimConfig.Scale - cfg.CloseViewWidth) * 0.25f, $"{where}: width jumped {cam.Width - pw:0} ({pw:0} -> {cam.Width:0}, y {s.A.Y}/{s.B.Y}, sep {s.Separation}, ko {s.KoProgress}, feet floor {cam.FeetFloorEngaged})");
						Assert.True(Math.Abs(cam.HeightMeters - ph) <= MaxHeightStep(cfg), $"{where}: height jumped {cam.HeightMeters - ph:0.00} m ({ph:0.00} -> {cam.HeightMeters:0.00}, width {pw:0} -> {cam.Width:0}, y {s.A.Y}/{s.B.Y}, ko {s.KoProgress}, floor {cam.FeetFloorEngaged})");
					}
					if (!tall) Assert.True(!cam.FeetFloorEngaged, $"start {start} seed {seed} tick {t}: the feet floor changed the default framing");
				}
			}
	}

	/// <summary>Per-tick height step allowed (m): the camera probes' 0.15, or 0.25 framing the Oni. His club keeps the HUD floor
	/// binding at rest, so the first tick of a jump (the sim's arc rises ~0.24 m at once) lifts the camera with it instead
	/// of into spare headroom as for Ryo vs the Kitsune. Designer review point (YOK-133).</summary>
	static float MaxHeightStep(CameraConfig cfg) => ReferenceEquals(cfg, CameraConfig.Default) ? 0.15f : 0.25f;

	static void Sweep(CameraConfig cfg)
	{
		bool tall = !ReferenceEquals(cfg, CameraConfig.Default), engaged = false;
		SweepAll(cfg, tall, ref engaged);
		if (tall) Assert.True(engaged, "the Oni's top engages the feet floor somewhere (else the test proves nothing)");
	}

	static void SweepAll(CameraConfig cfg, bool tall, ref bool engaged)
	{
		int corner = C.StageHalfWidth - C.BodyWidth / 2;
		foreach (int sep in new[] { C.BodyWidth, C.MaxSeparation / 2, C.MaxSeparation })
			foreach (int at in new[] { -corner, 0, corner })
				foreach (bool both in new[] { false, true })
				{
					var m = new Match();
					m.P1.X = Math.Clamp(at - sep / 2, -corner, corner - sep); m.P2.X = m.P1.X + sep;
					var cam = new DynamicCamera(C, cfg);
					cam.Snap(DynamicCamera.SubjectOf(m));
					var yok129 = new DynamicCamera(C); // the default camera on the same fight: feet may dip a little on a corner jump (YOK-129)
					yok129.Snap(DynamicCamera.SubjectOf(m));
					for (int t = 0; t < C.JumpFrames * 3; t++)
					{
						m.Step(In(InputBits.Up), both ? In(InputBits.Up) : FighterInput.None);
						var s = DynamicCamera.SubjectOf(m);
						float pw = cam.Width, ph = cam.HeightMeters;
						cam.Step(s);
						yok129.Step(s);
						string where = $"sep {sep / SimConfig.Scale}, at {at / SimConfig.Scale}, both {both}, tick {t}";
						Assert.True(cam.InFrame(s), $"{where}: out of frame or the club over the HUD (width {cam.Width:0}, h {cam.HeightMeters:0.00})");
						Assert.True(cam.OnStage(), $"{where}: view off the stage art (width {cam.Width:0}, cx {cam.CenterX:0})");
						// No snapping (the DynamicCameraProbes fuzz allowance: a quarter of the zoom range per tick, 0.15 m of height).
						Assert.True(Math.Abs(cam.Width - pw) <= (C.ViewWidth / (float)SimConfig.Scale - cfg.CloseViewWidth) * 0.25f, $"{where}: width jumped {cam.Width - pw:0} in a tick");
						Assert.True(Math.Abs(cam.HeightMeters - ph) <= MaxHeightStep(cfg), $"{where}: height jumped {cam.HeightMeters - ph:0.00} m in a tick");
						foreach (var body in new[] { s.A, s.B })
						{
							float feet = Math.Max(0f, body.Y) / DynamicCamera.UnitsPerMeter;
							Assert.True(cam.ScreenYPx(body.X, feet + cfg.ModelTopMeters) >= cfg.HudClearPx - 0.01f, $"{where}: club top over the HUD line");
							// Tall: on screen every tick (the feet floor holds them at FeetClearPx, catching up over a few ticks after a sudden rise).
							// Default: whatever YOK-129 does (a grounded fighter's feet dip ~15 px under the frame on a corner jump), i.e. the same camera.
							float px = cam.ScreenYPx(body.X, feet), limit = tall ? 1080f : yok129.ScreenYPx(body.X, feet) + 0.01f;
							Assert.True(px <= limit, $"{where}: feet at {px:0} px, lower than {limit:0} (width {cam.Width:0})");
						}
						if (!tall) Assert.True(!cam.FeetFloorEngaged, $"{where}: the feet floor changed the default framing");
						else engaged |= cam.FeetFloorEngaged;
					}
				}
	}

	[Test]
	public static void FightScene_FramesForTheBodiesItShows(Node runner)
	{
		var plain = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		plain.ExternalDrive = true;
		runner.AddChild(plain);
		plain.Step(FighterInput.None, FighterInput.None);
		Assert.True(ReferenceEquals(CameraConfig.Default, plain.Cam.Config), "Ryo vs the Kitsune: the default camera config");
		plain.QueueFree();

		var pool = FightScene.LoadAbilityPool();
		foreach (bool elder in new[] { false, true })
		{
			var jump = DevJump.BuildFight(new DevFightSetup("oni", Elder: elder, Seed: 11), pool);
			Assert.True(jump.Session.Enter(jump.NodeId).Ok, "oni node entered");
			var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
			scene.ExternalDrive = true;
			scene.Session = jump.Session;
			runner.AddChild(scene);
			try
			{
				scene.Step(FighterInput.None, FighterInput.None);
				Assert.Equal(CameraConfig.Default.TopFor("oni"), scene.Cam.Config.ModelTopMeters, $"elder {elder}: framed for the Oni's club");
			}
			finally { scene.QueueFree(); }
		}
	}
}
