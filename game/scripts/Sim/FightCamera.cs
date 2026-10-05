using System;

namespace YokaiFighters.Sim;

/// <summary>
/// Where the side-on camera centres, in centi-units on the gameplay plane: the fighters'
/// midpoint, clamped so the view never shows past the stage corners. Pure so tests can check
/// both fighters always stay in view; the presentation layer turns it into a Camera3D position.
/// </summary>
public static class FightCamera
{
	public static int CenterX(Match m)
	{
		var c = m.Config;
		int mid = (m.P1.X + m.P2.X) / 2;
		int limit = Math.Max(0, c.StageHalfWidth - c.ViewWidth / 2);
		return Math.Clamp(mid, -limit, limit);
	}

	/// <summary>True if both fighters' bodies are fully inside the camera's view width.</summary>
	public static bool BothInView(Match m)
	{
		var c = m.Config;
		int cx = CenterX(m), half = c.ViewWidth / 2, body = c.BodyWidth / 2;
		foreach (var f in m.Fighters)
			if (f.X - body < cx - half || f.X + body > cx + half) return false;
		return true;
	}
}
