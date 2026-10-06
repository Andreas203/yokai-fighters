namespace YokaiFighters.Sim;

/// <summary>
/// YOK-55 partial of <see cref="Match"/>: jump-in normals (E11). An air normal is an ordinary Attack that
/// starts during a jump: the arc and drift carry on, hitstop (V2), meter (C5/E14), counterhit and guard
/// work as for ground normals. It ends early on touchdown (landing recovery), or falls as Jump if it ends
/// first. One per jump; none while falling from an air hit or burst.
/// </summary>
public sealed partial class Match
{
	/// <summary>Can this fighter start an air normal this frame (in its own jump, none used yet)?</summary>
	public static bool CanAirAttack(Fighter f) =>
		f.State == FighterState.Jump && f.Airborne && !f.AirAttackUsed && !f.KnockedOut;

	/// <summary>An explicit slot request (tests, AI) is honoured only where that move can come out.</summary>
	private static bool CanStartSlot(Fighter f, int slot) =>
		f.Moves[slot].Air ? CanAirAttack(f) : f.Actionable;

	/// <summary>Touchdown mid air normal: the move ends; LandingRecovery frames (this tick is the first) follow.</summary>
	private void LandAirAttack(Fighter f)
	{
		int frames = f.ActiveMove?.LandingRecovery ?? Config.AirLandingRecovery;
		SetState(f, FighterState.Landing, frames);
	}
}
