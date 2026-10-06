using System;

namespace YokaiFighters.Sim;

/// <summary>
/// Where the side-on camera centres, in centi-units on the gameplay plane: the fighters' midpoint (steady, V7),
/// clamped at <see cref="Limit"/>. Pure so tests can check both fighters always stay in view; the presentation
/// layer turns it into a Camera3D position.
/// YOK-39: the clamp lets the view reach a cornered fighter's full model (its X plus the larger of
/// <see cref="SimConfig.ModelFrontReach"/> / <see cref="SimConfig.ModelBackReach"/>), so the visible stage is
/// <see cref="VisibleHalfWidth"/> each side of 0; the stage art covers exactly that and the camera never shows more.
/// </summary>
public static class FightCamera
{
	/// <summary>The larger of the model's front and back reach: what a cornered fighter may show toward the wall.</summary>
	public static int ModelReach(SimConfig c) => Math.Max(Math.Max(c.ModelFrontReach, c.ModelBackReach), c.BodyWidth / 2);

	/// <summary>Farthest the camera centre goes from 0: the view edge lands on the cornered fighter's model edge.</summary>
	public static int Limit(SimConfig c)
	{
		int corner = c.StageHalfWidth - c.BodyWidth / 2;
		return Math.Max(0, corner + ModelReach(c) - c.ViewWidth / 2);
	}

	/// <summary>Half-width of the stage the camera can ever show (the art must cover at least this).</summary>
	public static int VisibleHalfWidth(SimConfig c) => Limit(c) + c.ViewWidth / 2;

	public static int CenterX(Match m)
	{
		int mid = (m.P1.X + m.P2.X) / 2;
		int limit = Limit(m.Config);
		return Math.Clamp(mid, -limit, limit);
	}

	/// <summary>A fighter's model extent on the plane (left, right) from its facing: front reach ahead, back reach behind.</summary>
	public static (int Left, int Right) VisualExtent(SimConfig c, Fighter f)
	{
		int front = Math.Max(c.ModelFrontReach, c.BodyWidth / 2), back = Math.Max(c.ModelBackReach, c.BodyWidth / 2);
		return f.Facing >= 0 ? (f.X - back, f.X + front) : (f.X - front, f.X + back);
	}

	/// <summary>True if both fighters' models (<see cref="VisualExtent"/>) are fully inside the camera's view width.</summary>
	public static bool BothInView(Match m)
	{
		var c = m.Config;
		int cx = CenterX(m), half = c.ViewWidth / 2;
		foreach (var f in m.Fighters)
		{
			var (l, r) = VisualExtent(c, f);
			if (l < cx - half || r > cx + half) return false;
		}
		return true;
	}
}
