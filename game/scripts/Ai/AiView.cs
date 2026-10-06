using System.Collections.Generic;
using YokaiFighters.Sim;

namespace YokaiFighters.Ai;

/// <summary>
/// What one fighter shows on screen on one tick (P6): position, pose (state and move frame), health and
/// meter as the HUD draws them. Deliberately has no input, parser, buffer or command field, so nothing
/// built from it can read a button press.
/// </summary>
public readonly record struct FighterView(
	int X, int Y, int Facing, FighterState State, int StunLeft, bool Airborne, bool KnockedOut,
	int Health, int Meter,
	string? MoveId, bool MoveIsSpecial, int MoveFrame, int MoveFirstActive, int MoveLastActive, bool MoveConnected)
{
	public bool Attacking => State == FighterState.Attack && MoveId is not null;
	/// <summary>Startup or active frames of a visible move (the swing is coming or out).</summary>
	public bool Threatening => Attacking && MoveFrame <= MoveLastActive;
	/// <summary>Recovery frames of a visible move.</summary>
	public bool Recovering => Attacking && MoveFrame > MoveLastActive;
}

public readonly record struct ProjectileView(int Owner, int X, int Dir);

/// <summary>
/// A read-only snapshot of the screen after one sim tick. The AI only ever receives these (through its
/// reaction-delay buffer), never <see cref="Match"/>: that is what makes reading inputs before they show
/// structurally impossible (P6).
/// </summary>
public sealed record AiView(int Tick, MatchPhase Phase, int HitstopLeft, FighterView P1, FighterView P2, IReadOnlyList<ProjectileView> Projectiles)
{
	public FighterView Fighter(int i) => i == 0 ? P1 : P2;

	/// <summary>The only bridge from the sim to the AI: copies on-screen state, nothing else.</summary>
	public static AiView Capture(Match m)
	{
		var shots = new ProjectileView[m.Projectiles.Count];
		for (int i = 0; i < shots.Length; i++) shots[i] = new(m.Projectiles[i].Owner, m.Projectiles[i].X, m.Projectiles[i].Dir);
		return new AiView(m.Tick, m.Phase, m.HitstopLeft, Of(m.P1), Of(m.P2), shots);
	}

	private static FighterView Of(Fighter f)
	{
		MoveData? mv = f.CurrentMove;
		return new FighterView(f.X, f.Y, f.Facing, f.State, f.StunLeft, f.Airborne, f.KnockedOut, f.Health, f.Meter,
			mv?.Id, mv is not null && f.ActiveSpecial != SpecialSlot.None, mv is null ? 0 : f.MoveFrame,
			mv?.FirstActive ?? 0, mv?.LastActive ?? 0, f.MoveConnected);
	}
}
