using System;

namespace YokaiFighters.Sim;

/// <summary>
/// YOK-221: a move's own forward travel (<see cref="MoveData.ForwardTravel"/>, over its active frames) and the
/// YOK-171 charge dash (<see cref="Fighter.ChargeSpeed"/>, over its startup frames) are one step: each tick a fighter
/// in a move moves the sum of both toward its facing (YOK-210 e: the dash adds to the move's travel). Both fighters
/// step at once, after their input, so neither side goes first. The two parts meet the opponent differently:
/// <list type="bullet">
/// <item>the forward_travel part stops at the opponent's push box when both are grounded (a jumper is passed under,
/// as walking does, C2) and never pushes; an opponent stepping away this tick leaves that much more room;</item>
/// <item>the charge-dash part is never capped there: <see cref="ResolvePositions"/> shares the overlap, so the dash
/// shoves the opponent as YOK-210 approved it.</item>
/// </list>
/// The walls are <see cref="ResolvePositions"/>'s, as for every other step (E1). Nothing here is keyed on a move or
/// modifier (A10), and nothing new is state: the step is a function of the move and its frame, both hashed (F3).
/// </summary>
public sealed partial class Match
{
	/// <summary>Centi-units of the move's own forward_travel on this tick (0 = none), at most a whole stage.</summary>
	public long TravelPart(Fighter f) =>
		f.State == FighterState.Attack && f.ActiveMove is { } m ? Math.Min(m.TravelAt(f.MoveFrame), StageSpan) : 0;

	/// <summary>Centi-units of the YOK-171 charge dash on this tick (startup frames only), at most a whole stage.</summary>
	public long DashPart(Fighter f) =>
		f.ChargeSpeed > 0 && f.State == FighterState.Attack && f.ActiveMove is { } m && f.MoveFrame <= m.Startup
			? Math.Min((long)f.ChargeSpeed * SimConfig.Scale, StageSpan) : 0;

	/// <summary>Data puts no ceiling on travel or dash speed: never step past the whole stage, so X can't wrap int32 (F3).</summary>
	private long StageSpan => 2L * Config.StageHalfWidth;

	/// <param name="moved">Each fighter's movement this tick; the step is added so the screen walls can give it back.</param>
	private void StepTravel(Span<int> moved)
	{
		long t0 = TravelPart(P1), t1 = TravelPart(P2), d0 = DashPart(P1), d1 = DashPart(P2);
		if (t0 == 0 && t1 == 0 && d0 == 0 && d1 == 0) return;

		if (P1.Y == 0 && P2.Y == 0)
		{
			int dx = P2.X - P1.X;
			// Toward = forward is the opponent's side; on equal X there is no room either way.
			bool in0 = dx == 0 || Math.Sign(dx) == P1.Facing;
			bool in1 = dx == 0 || Math.Sign(-dx) == P2.Facing;
			long room = Math.Max(0, (long)Math.Abs(dx) - PushGap(P1, P2));
			if (in0 && in1)
			{
				// Both travel in: each gets up to half the room, and what one leaves the other may take.
				long r0 = room - Math.Min(t1, room - room / 2);
				t0 = Math.Min(t0, r0);
				t1 = Math.Min(t1, room - t0);
			}
			else if (in0) t0 = Math.Min(t0, room + AwayStep(P2, t1 + d1));
			else if (in1) t1 = Math.Min(t1, room + AwayStep(P1, t0 + d0));
		}
		long s0 = Math.Min(t0 + d0, StageSpan), s1 = Math.Min(t1 + d1, StageSpan);
		P1.X += P1.Facing * (int)s0;
		P2.X += P2.Facing * (int)s1;
		moved[0] += P1.Facing * (int)s0;
		moved[1] += P2.Facing * (int)s1;
	}

	/// <summary>How far <paramref name="f"/>, stepping <paramref name="step"/> away from the opponent, really gets before its corner.</summary>
	private long AwayStep(Fighter f, long step)
	{
		long toCorner = f.Facing > 0 ? (long)CornerHi(f) - f.X : (long)f.X - CornerLo(f);
		return Math.Clamp(step, 0, Math.Max(0, toCorner));
	}
}
