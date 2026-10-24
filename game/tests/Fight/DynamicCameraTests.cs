using System;
using System.Collections.Generic;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-129 / AM9: the dynamic fight camera, as pure camera maths (no scene boot). Zoom monotonic with
/// separation, the framing targets (fighter share of the screen height, chest height, pitch), both models in frame
/// and heads under the HUD through jumps at every zoom and in both corners, smooth easing, and easing deterministic.
/// </summary>
public static class DynamicCameraTests
{
	static FighterInput In(InputBits b) => new(b);
	static readonly SimConfig C = SimConfig.Default;

	/// <summary>Two grounded fighters facing each other, sep units apart around x (units).</summary>
	static DynamicCamera.Subject Apart(float sep, float x = 0f, float y1 = 0f, float y2 = 0f)
	{
		var m = new Match();
		int corner = C.StageHalfWidth - C.BodyWidth / 2;
		m.P1.X = Math.Clamp((int)((x - sep / 2) * SimConfig.Scale), -corner, corner);
		m.P2.X = Math.Clamp((int)((x + sep / 2) * SimConfig.Scale), -corner, corner);
		m.P1.Y = (int)(y1 * SimConfig.Scale); m.P2.Y = (int)(y2 * SimConfig.Scale);
		m.P1.Facing = 1; m.P2.Facing = -1;
		return DynamicCamera.SubjectOf(m);
	}

	static DynamicCamera Settled(in DynamicCamera.Subject s)
	{
		var cam = new DynamicCamera(C);
		cam.Snap(s);
		for (int t = 0; t < 400; t++) cam.Step(s);
		return cam;
	}

	[Test]
	public static void Zoom_MonotonicWithSeparation_CloseToWidest()
	{
		var cam = new DynamicCamera(C);
		float maxSep = C.MaxSeparation / (float)SimConfig.Scale, last = 0f, lastSettled = 0f;
		for (float sep = C.BodyWidth / (float)SimConfig.Scale; sep <= maxSep; sep += 10f)
		{
			var s = Apart(sep);
			float w = cam.TargetWidth(s);
			Assert.True(w >= last - 1e-3f, $"target width falls at separation {sep}: {w} < {last}");
			float settled = Settled(s).Width;
			Assert.True(settled >= lastSettled - 1e-2f, $"settled width falls at separation {sep}: {settled} < {lastSettled}");
			last = w; lastSettled = settled;
		}
		Assert.True(Math.Abs(cam.TargetWidth(Apart(C.BodyWidth / (float)SimConfig.Scale)) - cam.MinWidth) < 1e-3f, "closest zoom when close");
		Assert.True(Math.Abs(cam.TargetWidth(Apart(maxSep)) - cam.MaxWidth) < 1e-3f, "widest zoom at the screen walls");
		Assert.True(cam.MinWidth < cam.MaxWidth, "the camera zooms");
	}

	/// <summary>Share of the screen height Ryo's fighting stance (<see cref="CameraConfig.StanceHeightMeters"/>) fills, feet to hair.</summary>
	static float HeightShare(DynamicCamera cam, float x) => (cam.Project(x, cam.Config.StanceHeightMeters).Y - cam.Project(x, 0f).Y) / 2f;

	[Test]
	public static void Framing_FighterHeightShare_ChestHeight_SlightPitch()
	{
		var close = Settled(Apart(C.BodyWidth / (float)SimConfig.Scale));
		float near = HeightShare(close, close.CenterX);
		Assert.True(near >= 0.50f && near <= 0.55f, $"close in a fighter fills {near:P1} of the screen height (target 50-55%)");
		Assert.True(close.HeightMeters >= 1.1f && close.HeightMeters <= 1.3f, $"close in the camera sits at chest height ({close.HeightMeters:0.00} m)");
		var wide = Settled(Apart(C.MaxSeparation / (float)SimConfig.Scale));
		float far = HeightShare(wide, wide.CenterX);
		Assert.True(far >= 0.35f && far <= 0.40f, $"at the screen walls a fighter fills {far:P1} (target 35-40%)");
		Assert.True(wide.HeightMeters >= 1.1f && wide.HeightMeters <= 1.3f, $"at the walls the camera stays at chest height ({wide.HeightMeters:0.00} m)");
		foreach (var cam in new[] { close, wide })
			Assert.True(Math.Abs(cam.PitchDegrees) <= 5f && Math.Abs(-cam.Transform.Pitch * 180f / MathF.PI - cam.PitchDegrees) < 1e-3f, $"slight pitch ({cam.PitchDegrees} deg, at most 5)");
		Assert.True(close.PitchDegrees >= 0f, "close in the camera looks level or slightly down");
		Assert.True(Math.Abs(close.Config.FovDegrees - FightScene.CameraFovDegrees) < 1e-4f, "F4 FOV unchanged");
		// Designer review 2026-10-16: the feet sit low, ~88-92% of the way down at the walls (no empty floor band).
		float feetWide = wide.ScreenYPx(0f, 0f) / 1080f, feetClose = close.ScreenYPx(0f, 0f) / 1080f;
		Assert.True(feetWide >= 0.88f && feetWide <= 0.92f, $"at the walls the feet sit {feetWide:P1} down (target 88-92%)");
		Assert.True(feetClose >= 0.85f && feetClose <= 0.93f, $"close in the feet sit {feetClose:P1} down");
	}

	[Test]
	public static void BothModelsInFrame_AnySeparationFacingAndJump()
	{
		int corner = C.StageHalfWidth - C.BodyWidth / 2;
		float apex = C.JumpY(C.JumpFrames / 2) / (float)SimConfig.Scale;
		var cam = new DynamicCamera(C);
		for (int sep = C.BodyWidth; sep <= C.MaxSeparation; sep += 40 * SimConfig.Scale)
			foreach (int at in new[] { -corner, 0, corner })
				foreach (int f1 in new[] { -1, 1 })
					foreach (int f2 in new[] { -1, 1 })
						foreach (float y in new[] { 0f, apex / 2, apex })
						{
							var m = new Match();
							m.P1.X = Math.Clamp(at - sep / 2, -corner, corner - sep); m.P2.X = m.P1.X + sep;
							if (at == corner) { m.P2.X = corner; m.P1.X = corner - sep; }
							if (at == -corner) { m.P1.X = -corner; m.P2.X = -corner + sep; }
							m.P1.Facing = f1; m.P2.Facing = f2; m.P1.Y = (int)(y * SimConfig.Scale);
							var s = DynamicCamera.SubjectOf(m);
							cam.Snap(s);
							Assert.True(cam.InFrame(s) && cam.OnStage(), $"snap: sep {sep / SimConfig.Scale}, at {at / SimConfig.Scale}, facings {f1}/{f2}, y {y}: width {cam.Width}, cx {cam.CenterX}");
						}
	}

	[Test]
	public static void VerticalFollow_RisesPartway_NeverCropsTheHigherHead()
	{
		int corner = C.StageHalfWidth - C.BodyWidth / 2;
		float jump = C.JumpHeight / (float)SimConfig.Scale / DynamicCamera.UnitsPerMeter;
		foreach (int sep in new[] { C.BodyWidth, C.MaxSeparation / 2, C.MaxSeparation })
			foreach (int at in new[] { -corner, 0, corner })
				foreach (bool both in new[] { false, true })
				{
					var m = new Match();
					m.P1.X = Math.Clamp(at - sep / 2, -corner, corner - sep); m.P2.X = m.P1.X + sep;
					var cam = new DynamicCamera(C);
					cam.Snap(DynamicCamera.SubjectOf(m));
					float rest = cam.HeightMeters, highest = rest, lowestHeadPx = float.MaxValue;
					for (int t = 0; t < C.JumpFrames * 3; t++)
					{
						m.Step(In(InputBits.Up), both ? In(InputBits.Up) : FighterInput.None);
						var s = DynamicCamera.SubjectOf(m);
						cam.Step(s);
						string at2 = $"sep {sep / SimConfig.Scale}, at {at / SimConfig.Scale}, both {both}, tick {t}";
						Assert.True(cam.InFrame(s), $"{at2}: out of frame or a head under the HUD (width {cam.Width:0}, h {cam.HeightMeters:0.00})");
						float top = Math.Max(s.A.Y, s.B.Y) / DynamicCamera.UnitsPerMeter + cam.Config.ModelTopMeters;
						lowestHeadPx = Math.Min(lowestHeadPx, cam.ScreenYPx(s.A.Y >= s.B.Y ? s.A.X : s.B.X, top));
						highest = Math.Max(highest, cam.HeightMeters);
					}
					Assert.True(lowestHeadPx >= cam.Config.HudClearPx - 0.01f, $"sep {sep}, at {at}: highest head at {lowestHeadPx:0} px, under the HUD line {cam.Config.HudClearPx}");
					Assert.True(highest > rest + 0.1f, $"sep {sep}, at {at}: the camera rises on a jump ({rest:0.00} -> {highest:0.00})");
					Assert.True(highest - rest < jump, $"sep {sep}, at {at}: rises only partway ({highest - rest:0.00} m of a {jump:0.00} m jump)");
				}
	}

	[Test]
	public static void Easing_NoSnap_WalkingApartAndBackTogether()
	{
		var m = new Match();
		var cam = new DynamicCamera(C);
		cam.Snap(DynamicCamera.SubjectOf(m));
		float last = cam.Width, lastH = cam.HeightMeters, maxStep = 0f, maxHStep = 0f;
		float bound = (cam.MaxWidth - cam.MinWidth) * cam.Config.ZoomEase + 1f;
		for (int t = 0; t < 900; t++)
		{
			var b = t < 450 ? (In(InputBits.Left), In(InputBits.Right)) : (In(InputBits.Right), In(InputBits.Left));
			m.Step(b.Item1, b.Item2);
			cam.Step(DynamicCamera.SubjectOf(m));
			maxStep = Math.Max(maxStep, Math.Abs(cam.Width - last)); last = cam.Width;
			maxHStep = Math.Max(maxHStep, Math.Abs(cam.HeightMeters - lastH)); lastH = cam.HeightMeters;
		}
		Assert.True(maxStep <= bound, $"zoom eases: largest per-tick width change {maxStep:0.0} units (bound {bound:0.0})");
		Assert.True(maxHStep <= 0.05f, $"height eases: largest per-tick change {maxHStep:0.000} m");
		Assert.True(cam.Width < cam.MinWidth + 40f, $"back together: zoomed back in ({cam.Width:0})");
	}

	/// <summary>The camera transform after every tick of a seeded random fight, as exact bit patterns.</summary>
	static List<(int, int, int, int)> Trace(int seed)
	{
		var m = new Match();
		var rng = new SimRng((uint)seed);
		var cam = new DynamicCamera(C);
		cam.Snap(DynamicCamera.SubjectOf(m));
		InputBits[] dirs = { InputBits.Left, InputBits.Right, InputBits.None, InputBits.Up | InputBits.Left, InputBits.Up | InputBits.Right, InputBits.Up, InputBits.Down };
		var trace = new List<(int, int, int, int)>();
		for (int t = 0; t < 1500; t++)
		{
			m.Step(In(dirs[rng.Next(dirs.Length)] | (rng.Next(6) == 0 ? InputBits.HeavyPunch : InputBits.None)), In(dirs[rng.Next(dirs.Length)]));
			cam.Step(DynamicCamera.SubjectOf(m));
			var (x, y, z, p) = cam.Transform;
			trace.Add((BitConverter.SingleToInt32Bits(x), BitConverter.SingleToInt32Bits(y), BitConverter.SingleToInt32Bits(z), BitConverter.SingleToInt32Bits(p)));
			if (m.Phase == MatchPhase.Over) { m.Reset(); cam.Snap(DynamicCamera.SubjectOf(m)); }
		}
		return trace;
	}

	[Test]
	public static void Easing_Deterministic_SameTicksSameTransform()
	{
		foreach (int seed in new[] { 3, 129 })
		{
			var a = Trace(seed);
			var b = Trace(seed);
			for (int t = 0; t < a.Count; t++) Assert.Equal(a[t], b[t], $"seed {seed} tick {t}: same input ticks give the same camera transform");
		}
		Assert.True(new HashSet<(int, int, int, int)>(Trace(3)).Count > 50, "the camera moves during the fight");
	}

	[Test]
	public static void KoPushIn_NarrowsTheTarget_ButKeepsBothInFrame()
	{
		var s = Apart(800f);
		var cam = Settled(s);
		var ko = s with { KoProgress = 1f };
		Assert.True(cam.TargetWidth(ko) < cam.TargetWidth(s), "V7 round-ending blow pushes in");
		for (int t = 0; t < 120; t++) cam.Step(ko);
		Assert.True(cam.InFrame(ko), "still both in frame");
		var wall = Apart(C.MaxSeparation / (float)SimConfig.Scale) with { KoProgress = 1f, PushIn = 0.5f };
		var w2 = Settled(wall);
		Assert.True(w2.InFrame(wall) && w2.OnStage(), "a push-in at the screen walls never crops a model");
	}

	[Test]
	public static void SimNeverReadsTheCamera_StateHashUnchangedByCameraConfig()
	{
		// Presentation only: stepping cameras with wildly different configs leaves the sim's hash sequence identical.
		var a = new Match(); var b = new Match();
		var camA = new DynamicCamera(C); var camB = new DynamicCamera(C, CameraConfig.Default with { CloseViewWidth = 800f, PitchDegrees = 5f, ZoomEase = 1f });
		camA.Snap(DynamicCamera.SubjectOf(a)); camB.Snap(DynamicCamera.SubjectOf(b));
		var rng = new SimRng(7);
		for (int t = 0; t < 600; t++)
		{
			var i1 = In(rng.Next(2) == 0 ? InputBits.Left : InputBits.Right | InputBits.Up);
			a.Step(i1, FighterInput.None); b.Step(i1, FighterInput.None);
			camA.Step(DynamicCamera.SubjectOf(a)); camB.Step(DynamicCamera.SubjectOf(b));
			Assert.Equal(a.StateHash(), b.StateHash(), $"tick {t}");
		}
	}
}
