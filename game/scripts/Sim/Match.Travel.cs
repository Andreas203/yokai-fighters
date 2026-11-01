using System;

namespace YokaiFighters.Sim;

/// <summary>
/// YOK-221: a move's own forward travel (<see cref="MoveData.ForwardTravel"/>, over its active frames) and the
/// YOK-171 charge dash (<see cref="Fighter.ChargeSpeed"/>, over its startup frames) are one mechanism: each tick a
/// fighter in a move wants the sum of both, toward its facing (YOK-210 e: the dash adds to the move's travel). Both
/// fighters travel at once, after their input, so neither side goes first. Travel stops at the opponent's push box
/// when both are grounded (a jumper is passed under, as walking does, C2) and never pushes; the walls are
/// <see cref="ResolvePositions"/>'s, as for every other step (E1). Nothing here is keyed on a move or modifier (A10),
/// and nothing new is state: the step is a function of the move and its frame, both hashed (F3).
/// </summary>
public sealed partial class Match
{
	/// <summary>Centi-units <paramref name="f"/> wants to travel forward this tick (0 = none), at most a whole stage.</summary>
	public long TravelWanted(Fighter f)
	{
		if (f.State != FighterState.Attack || f.ActiveMove is not { } m) return 0;
		long step = m.TravelAt(f.MoveFrame);
		if (f.ChargeSpeed > 0 && f.MoveFrame <= m.Startup) step += (long)f.ChargeSpeed * SimConfig.Scale;
		// Data puts no ceiling on either: never step past the whole stage, so X can't wrap int32 (F3).
		return Math.Min(step, 2L * Config.StageHalfWidth);
	}

	/// <param name="moved">Each fighter's movement this tick; the travel is added so the screen walls can give it back.</param>
	private void StepTravel(Span<int> moved)
	{
		long w0 = TravelWanted(P1), w1 = TravelWanted(P2);
		if (w0 == 0 && w1 == 0) return;

		long s0 = w0, s1 = w1;
		if (P1.Y == 0 && P2.Y == 0)
		{
			int dx = P2.X - P1.X;
			// Toward = forward is the opponent's side; on equal X there is no room either way.
			bool in0 = w0 > 0 && (dx == 0 || Math.Sign(dx) == P1.Facing);
			bool in1 = w1 > 0 && (dx == 0 || Math.Sign(-dx) == P2.Facing);
			long room = Math.Max(0, (long)Math.Abs(dx) - PushGap(P1, P2));
			if (in0 && in1)
			{
				// Both charge in: each gets up to half the room, and what one leaves the other may take.
				s0 = Math.Min(w0, room - Math.Min(w1, room - room / 2));
				s1 = Math.Min(w1, room - s0);
			}
			else if (in0) s0 = Math.Min(w0, room);
			else if (in1) s1 = Math.Min(w1, room);
		}
		P1.X += P1.Facing * (int)s0;
		P2.X += P2.Facing * (int)s1;
		moved[0] += P1.Facing * (int)s0;
		moved[1] += P2.Facing * (int)s1;
	}
}
