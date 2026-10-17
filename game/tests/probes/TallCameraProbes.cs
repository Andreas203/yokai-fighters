using System;
using YokaiFighters.Fight;
using YokaiFighters.Sim;
using BaseCam = YokaiFighters.Tests.BaseCam;

namespace YokaiFighters.Tests;

/// <summary>YOK-133 probes. Expected values: AM10 (feet >= 1040 px floor with widening at most 80/tick, 0.25 m/tick rise,
/// default framing exactly AM9) and rules.md E1/V7 as amended.</summary>
public static class TallCameraProbes
{
	static readonly SimConfig C = SimConfig.Default;
	static FighterInput In(InputBits b) => new(b);

	static BaseCam.BaseDynamicCamera.Subject ToBase(in DynamicCamera.Subject s) =>
		new(new(s.A.X, s.A.Y, s.A.Left, s.A.Right), new(s.B.X, s.B.Y, s.B.Left, s.B.Right), s.KoProgress, s.PushIn);

	// AM10: "Ryo vs the Kitsune ... keeps AM9's camera exactly". Per-tick bit-equality to the base algorithm, seeded random fights with KO and push-in.
	[Test]
	public static void DefaultCamera_BitIdenticalToBase_SeededFights()
	{
		var dirs = new[] { InputBits.None, InputBits.Left, InputBits.Right, InputBits.Up, InputBits.Down, InputBits.Up | InputBits.Right, InputBits.Up | InputBits.Left };
		int corner = C.StageHalfWidth - C.BodyWidth / 2;
		foreach (int start in new[] { -corner, -200, 0, corner })
			for (int seed = 1; seed <= 6; seed++)
			{
				var m = FightScene.NewMatch();
				m.P1.X = start; m.P2.X = Math.Clamp(start + (start >= 0 ? -300 : 300), -corner, corner);
				var rng = new Random(seed * 131 + start);
				var cam = new DynamicCamera(C, CameraConfig.Default.ForBodies("ryo", "kitsune"));
				var bas = new BaseCam.BaseDynamicCamera(C);
				int hold = 0; FighterInput i1 = FighterInput.None, i2 = FighterInput.None;
				for (int t = 0; t < 1500; t++)
				{
					if (hold-- <= 0)
					{
						hold = rng.Next(1, 18);
						i1 = rng.Next(3) == 0 ? FighterInput.Attack(rng.Next(m.P1.Moves.Length)) : In(dirs[rng.Next(dirs.Length)]);
						i2 = rng.Next(3) == 0 ? FighterInput.Attack(rng.Next(m.P2.Moves.Length)) : In(dirs[rng.Next(dirs.Length)]);
					}
					m.Step(i1, i2);
					float push = t % 400 > 300 ? 0.3f : 0f;
					var s = DynamicCamera.SubjectOf(m, push);
					cam.Step(s); bas.Step(ToBase(s));
					Assert.True(cam.Width == bas.Width && cam.HeightMeters == bas.HeightMeters && cam.CenterX == bas.CenterX && cam.PitchDegrees == bas.PitchDegrees,
						$"start {start} seed {seed} tick {t}: width {cam.Width} vs base {bas.Width}, h {cam.HeightMeters} vs {bas.HeightMeters}, cx {cam.CenterX} vs {bas.CenterX}");
				}
			}
	}

	// AM10: only the Oni (3.15 m) is tall; argument order must not matter.
	[Test]
	public static void ForBodies_OrderUnknownIdsAndOniVsOni()
	{
		var d = CameraConfig.Default;
		var a = d.ForBodies("ryo", "oni"); var b = d.ForBodies("oni", "ryo"); var c = d.ForBodies("oni", "oni");
		Assert.True(a == b && a == c, "oni framing the same in either order and Oni vs Oni");
		Assert.True(ReferenceEquals(d, d.ForBodies("elder-oni-xyz", "")), "unknown ids stay default");
		Assert.True(ReferenceEquals(d, d.ForBodies("ONI", "Kitsune")), "ids are lowercase body ids; unknown case stays default");
		Assert.True(ReferenceEquals(a, a.ForBodies("oni", "ryo")), "re-framing a tall config for the Oni returns it unchanged");
		Assert.Equal(3.15f, a.ModelTopMeters, "tall top");
		Assert.Equal(1040f, a.FeetClearPx, "tall feet line");
		Assert.True(ReferenceEquals(d, d.ForBodies("kappa", "kappa")), "kappa vs kappa is default");
	}

	// AM10: feet >= 1040 px floor. A sudden rise (both airborne to height k at once) must never put the lower feet under the frame (1080),
	// and the widening beyond the eased width is at most 80 units per tick after the start snap.
	[Test]
	public static void OniFraming_SuddenRise_FeetStayOnScreen_AndSnapHoldsFloor()
	{
		var cfg = CameraConfig.Default.ForBodies("ryo", "oni");
		int corner = C.StageHalfWidth - C.BodyWidth / 2;
		foreach (float x in new[] { -corner / 200f, 0f, corner / 200f })
			foreach (float sepU in new[] { 60f, 400f, C.MaxSeparation / 200f })
				foreach (float y in new[] { 20f, 48f, 100f, 200f, 400f, 700f })
				{
					float x0 = Math.Clamp(x - sepU / 2, -corner / 200f, corner / 200f - sepU);
					DynamicCamera.Subject S(float yy) => new(new(x0, yy, x0 - 30, x0 + 30), new(x0 + sepU, yy, x0 + sepU - 30, x0 + sepU + 30), 0f, 0f);
					var cam = new DynamicCamera(C, cfg);
					cam.Snap(S(0));
					for (int t = 0; t < 40; t++)
					{
						float pw = cam.Width;
						var s = S(y);
						cam.Step(s);
						float px = cam.ScreenYPx(s.A.X, y / 200f);
						Assert.True(px <= 1080f, $"x {x} sep {sepU} rise to {y}: tick {t} feet at {px:0} px (below frame)");
						Assert.True(cam.InFrame(s), $"x {x} sep {sepU} rise to {y}: tick {t} club or body out of frame");
					}
					var snapCam = new DynamicCamera(C, cfg);
					var s2 = S(y); snapCam.Snap(s2);
					Assert.True(snapCam.ScreenYPx(s2.A.X, y / 200f) <= 1040.5f, $"snap at height {y} puts feet at {snapCam.ScreenYPx(s2.A.X, y / 200f):0} px, over the 1040 line");
				}
	}

	// V7/AM10: the KO push-in (KoProgress up to 1) and the scripted push-in (to 0.5) in an Oni fight keep both models in frame and feet on screen.
	[Test]
	public static void OniFraming_KoAndScriptedPushIn_KeepTheFrame()
	{
		var cfg = CameraConfig.Default.ForBodies("ryo", "oni");
		int corner = C.StageHalfWidth - C.BodyWidth / 2;
		foreach (float ko in new[] { 0f, 0.5f, 1f })
			foreach (float push in new[] { 0f, 0.5f })
				foreach (float sep in new[] { 60f, 400f, C.MaxSeparation / 200f })
					foreach (float y in new[] { 0f, 100f })
					{
						float x0 = -sep / 2;
						DynamicCamera.Subject S() => new(new(x0, y, x0 - 30, x0 + 30), new(x0 + sep, y, x0 + sep - 30, x0 + sep + 30), ko, push);
						var cam = new DynamicCamera(C, cfg);
						for (int t = 0; t < 120; t++)
						{
							var s = S(); cam.Step(s);
							Assert.True(cam.InFrame(s), $"ko {ko} push {push} sep {sep} y {y} tick {t}: out of frame");
							Assert.True(cam.ScreenYPx(s.A.X, y / 200f) <= 1080f, $"ko {ko} push {push} sep {sep} y {y} tick {t}: feet off frame");
						}
					}
	}

	// E1/V7: the two fighters cross over (P1 jumps through P2): frame holds, no jump over the caps, both corners.
	[Test]
	public static void OniFraming_CrossUpJump_BothCorners()
	{
		var cfg = CameraConfig.Default.ForBodies("ryo", "oni");
		int corner = C.StageHalfWidth - C.BodyWidth / 2;
		foreach (int side in new[] { -1, 1 })
			foreach (bool otherJumps in new[] { false, true })
			{
				var m = new Match();
				m.P1.X = side * (corner - 150); m.P2.X = side * corner;
				var cam = new DynamicCamera(C, cfg); cam.Snap(DynamicCamera.SubjectOf(m));
				var toward = In(InputBits.Up | (side < 0 ? InputBits.Right : InputBits.Left));
				for (int t = 0; t < C.JumpFrames * 4; t++)
				{
					m.Step(toward, otherJumps ? In(InputBits.Up) : FighterInput.None);
					var s = DynamicCamera.SubjectOf(m);
					float ph = cam.HeightMeters; cam.Step(s);
					Assert.True(cam.InFrame(s) && cam.OnStage(), $"side {side} tick {t}: out of frame/stage");
					Assert.True(MathF.Abs(cam.HeightMeters - ph) <= 0.25f + 1e-4f, $"side {side} tick {t}: height step {cam.HeightMeters - ph:0.000}");
					float lowFeet = MathF.Min(MathF.Max(0, s.A.Y), MathF.Max(0, s.B.Y)) / 200f;
					Assert.True(cam.ScreenYPx((s.A.X + s.B.X) / 2f, lowFeet) <= 1080f, $"side {side} tick {t}: feet off frame");
				}
			}
	}

	// AM10: determinism. Same subjects twice give identical per-tick state, a different jump gives a different one.
	[Test]
	public static void OniFraming_Deterministic()
	{
		var cfg = CameraConfig.Default.ForBodies("ryo", "oni");
		float[] Run(float y)
		{
			var cam = new DynamicCamera(C, cfg); var r = new float[60];
			for (int t = 0; t < 60; t++) { cam.Step(new(new(-100, t > 10 ? y : 0, -130, -70), new(100, 0, 70, 130), 0, 0)); r[t] = cam.Width + cam.HeightMeters * 1000f + cam.CenterX; }
			return r;
		}
		var a = Run(150); var b = Run(150); var c = Run(0);
		Assert.True(a.AsSpan().SequenceEqual(b), "same inputs, same camera");
		Assert.True(!a.AsSpan().SequenceEqual(c), "a jump changes the camera");
	}
}
