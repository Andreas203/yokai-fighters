using System;
using System.Linq;
using Godot;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-39 corner camera: both fighters' models (front/back reach by facing, <see cref="FightCamera.VisualExtent"/>)
/// stay fully in frame at both corners, at max separation and over random play; the view never goes past the
/// visible stage (<see cref="FightCamera.VisibleHalfWidth"/>, which the stage art covers: StageTests).
/// </summary>
public static class CameraTests
{
	static FighterInput In(InputBits b) => new(b);

	static MoveData[] Kit() =>
		MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(Fight.FightScene.FixtureNormalsDir))
			.Concat(MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath("res://tests/fixtures/moves"))).ToArray();

	static void AssertFramed(Match m, string when)
	{
		var c = m.Config;
		int cx = FightCamera.CenterX(m), half = c.ViewWidth / 2;
		Assert.True(Math.Abs(cx) + half <= FightCamera.VisibleHalfWidth(c), $"view stays on the stage ({when})");
		foreach (var f in m.Fighters)
		{
			var (l, r) = FightCamera.VisualExtent(c, f);
			Assert.True(l >= cx - half && r <= cx + half,
				$"fighter at {f.X} (facing {f.Facing}, {f.State}) spans {l}..{r}, view {cx - half}..{cx + half} ({when})");
		}
	}

	[Test]
	public static void Camera_ReachCoversBothCorners_EitherFacing()
	{
		var c = SimConfig.Default;
		int corner = c.StageHalfWidth - c.BodyWidth / 2, limit = FightCamera.Limit(c), half = c.ViewWidth / 2;
		Assert.True(limit < c.StageHalfWidth, "the camera still travels: limit inside the stage");
		foreach (int side in new[] { -1, 1 })
			foreach (int facing in new[] { -1, 1 })
			{
				var f = new Fighter { X = side * corner, Facing = facing };
				var (l, r) = FightCamera.VisualExtent(c, f);
				Assert.True(l >= -limit - half && r <= limit + half, $"corner {side}, facing {facing}: model {l}..{r} inside the view at the camera limit");
			}
		Assert.Equal(corner + FightCamera.ModelReach(c), FightCamera.VisibleHalfWidth(c), "the view edge meets the cornered model's edge, no further");
	}

	[Test]
	public static void Camera_BothCornersByWalking()
	{
		foreach (var dir in new[] { InputBits.Left, InputBits.Right })
		{
			var m = new Match();
			for (int t = 0; t < 900; t++) { m.Step(In(dir), In(dir)); AssertFramed(m, $"walking {dir}, tick {t}"); }
			int edge = m.Config.StageHalfWidth - m.Config.BodyWidth / 2;
			Assert.True(m.Fighters.Any(f => Math.Abs(f.X) == edge), $"someone reached the {dir} corner");
			// The front fighter now walks away to the screen wall from the cornered one.
			var away = dir == InputBits.Left ? InputBits.Right : InputBits.Left;
			for (int t = 0; t < 900; t++)
			{
				if (dir == InputBits.Left) m.Step(FighterInput.None, In(away)); else m.Step(In(away), FighterInput.None);
				AssertFramed(m, $"walking out of the {dir} corner, tick {t}");
			}
			Assert.Equal(m.Config.MaxSeparation, Math.Abs(m.P2.X - m.P1.X), "max separation from the corner");
		}
	}

	[Test]
	public static void Camera_ShakeStaysTwoToFourScreenPx_AtAnyViewWidth()
	{
		// V3 (YOK-120, YOK-129): the 2-4 px shake is screen px; the camera offset goes through the current view width,
		// so it holds at the closest zoom, the widest, anything between and the old 1,920.
		var cam = new YokaiFighters.Fight.DynamicCamera(SimConfig.Default);
		foreach (float width in new[] { cam.MinWidth, (cam.MinWidth + cam.MaxWidth) / 2f, cam.MaxWidth, 1920f, 1003.7f })
		{
			float metresPerPx = width / YokaiFighters.Fight.FightScene.UnitsPerMeter / YokaiFighters.Fight.FightScene.DesignCanvasWidthPx;
			var s = new YokaiFighters.Fight.ScreenShake(6);
			s.Start();
			for (int t = 0; t < 6; t++, s.Advance())
			{
				var (h, v) = YokaiFighters.Fight.FightScene.ShakeOffsetMeters(width, s.OffsetPx);
				float px = Math.Abs(h) / metresPerPx, py = Math.Abs(v) / metresPerPx; // vertical: 16:9 keeps the same px per metre
				Assert.True(px >= 2f - 1e-3f && px <= 4f + 1e-3f && py >= 2f - 1e-3f && py <= 4f + 1e-3f,
					$"view {width}: shake frame {t} is {px:0.00} x {py:0.00} screen px (V3: 2-4)");
			}
		}
	}

	[Test]
	public static void Camera_MaxSeparationFitsBothBackReaches()
	{
		var c = SimConfig.Default;
		Assert.True(c.MaxSeparation + 2 * c.ModelBackReach <= c.ViewWidth, "screen walls leave room for both models' back reach");
		var m = new Match();
		for (int t = 0; t < 600; t++) { m.Step(In(InputBits.Left), In(InputBits.Right)); AssertFramed(m, $"walking apart, tick {t}"); }
		Assert.Equal(c.MaxSeparation, m.P2.X - m.P1.X, "at the screen walls");
	}

	[Test]
	public static void Camera_BothModelsInView_5000RandomTicks()
	{
		var kit = Kit();
		var m = new Match(null, kit, kit);
		var rng = new SimRng(39);
		var held = new InputBits[2];
		int[] left = { 0, 0 };
		InputBits[] dirs = { InputBits.Left, InputBits.Right, InputBits.Left, InputBits.Right, InputBits.None,
			InputBits.Up | InputBits.Left, InputBits.Up | InputBits.Right, InputBits.Up, InputBits.Down, InputBits.Down | InputBits.Left };
		InputBits[] buttons = { InputBits.LightPunch, InputBits.MediumPunch, InputBits.HeavyPunch, InputBits.LightKick, InputBits.MediumKick, InputBits.HeavyKick };
		int knockdowns = 0, corners = 0, walls = 0;
		var cam = new YokaiFighters.Fight.DynamicCamera(m.Config);
		cam.Snap(YokaiFighters.Fight.DynamicCamera.SubjectOf(m));
		float minW = float.MaxValue, maxW = 0f;
		for (int t = 0; t < 7000; t++) // YOK-129: 7,000 ticks in 1,400-tick drifts (was 5,000 / 1,000) so the wider 1,240 walls still reach the corners
		{
			var input = new FighterInput[2];
			for (int i = 0; i < 2; i++)
			{
				if (left[i]-- <= 0)
				{
					// Long holds, mostly toward this phase's side (left then right every 1,000 ticks) so the run reaches corners.
					var drift = t / 1400 % 2 == 0 ? InputBits.Left : InputBits.Right;
					held[i] = rng.Next(3) < 2 ? drift : dirs[rng.Next(dirs.Length)];
					left[i] = 10 + rng.Next(120);
				}
				var b = held[i];
				if (rng.Next(8) == 0) b |= buttons[rng.Next(buttons.Length)];
				input[i] = In(b);
			}
			m.Step(input[0], input[1]);
			AssertFramed(m, $"tick {t}");
			// YOK-129: the dynamic camera, stepped each tick, keeps both models (and heads under the HUD) in its current view.
			var s = YokaiFighters.Fight.DynamicCamera.SubjectOf(m);
			cam.Step(s);
			Assert.True(cam.InFrame(s) && cam.OnStage(), $"tick {t}: out of the dynamic view (width {cam.Width:0.0}, cx {cam.CenterX:0.0}, h {cam.HeightMeters:0.00}; P1 {m.P1.X} y{m.P1.Y}, P2 {m.P2.X} y{m.P2.Y})");
			minW = Math.Min(minW, cam.Width); maxW = Math.Max(maxW, cam.Width);
			if (m.Fighters.Any(f => f.State == FighterState.Knockdown)) knockdowns++;
			if (m.Fighters.Any(f => Math.Abs(f.X) == m.Config.StageHalfWidth - m.Config.BodyWidth / 2)) corners++;
			if (Math.Abs(m.P2.X - m.P1.X) == m.Config.MaxSeparation) walls++;
			if (m.Phase == MatchPhase.Over) { m.Reset(); cam.Snap(YokaiFighters.Fight.DynamicCamera.SubjectOf(m)); }
		}
		Assert.True(knockdowns > 0 && corners > 0 && walls > 0, $"the run covers knockdowns ({knockdowns}), corners ({corners}) and screen walls ({walls})");
		Assert.True(minW < cam.MinWidth + 60f && maxW > cam.MaxWidth - 30f, $"the run covers the zoom range ({minW:0} .. {maxW:0} of {cam.MinWidth} .. {cam.MaxWidth})");
	}
}
