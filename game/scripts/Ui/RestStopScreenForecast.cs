using System;
using YokaiFighters.Sim;

namespace YokaiFighters.Ui;

/// <summary>
/// T6: what the rest stop tells Ryo about the Tanuki's copy: the move, the level it is copied at, and what it is
/// expected to cost him. Read-only on the run: before the T1 lock the target is <see cref="CopyRule.Target"/> (the
/// current best special), after it <see cref="RunSession.LockedCopy"/>. Pure C# so a test can name it without a scene.
/// </summary>
/// <param name="Level">The level the Tanuki's copy runs at (T4: never the Lv 3 evolution), what the player is told.</param>
/// <param name="RyoLevel">Ryo's own level of the move; above <paramref name="Level"/> only for an evolved special.</param>
/// <param name="Locked">True once the T1 lock has been taken: the target will not change any more.</param>
/// <param name="Cost">Expected health cost to Ryo: the copy's damage per hit (<see cref="ExpectedCost"/>). Always a figure (T6).</param>
public sealed record RestStopScreenForecast(string SpecialId, string Name, SpecialSlot Slot, int Level, int RyoLevel, bool Locked, int Cost)
{
	/// <summary>
	/// T6 (designer, 2026-10-20, YOK-173): the expected cost is the copied move's damage per hit at Ryo's level, read
	/// from data, so it is always defined (no "not enough fights" state). It is read from the move the Tanuki actually
	/// fights with, <see cref="CopyTarget.Build"/>: Ryo's level capped at Lv 2 (T4: the copy never gets the Lv 3
	/// evolution, nor his modifier), plain (not EX). Per hit: a multi-hit move (projectile <c>hits</c> &gt; 1) reads
	/// the damage one hit deals, not the sum.
	/// </summary>
	public static int ExpectedCost(CopyTarget target)
	{
		if (target is null) throw new ArgumentNullException(nameof(target));
		return target.Build().Damage;
	}

	/// <summary>The forecast for the run's current moment; null when no special sits in a slot (T5 makes that unreachable in play).</summary>
	public static RestStopScreenForecast? For(RunSession session)
	{
		if (session is null) throw new ArgumentNullException(nameof(session));
		CopyTarget? t = session.CopyLocked ? session.LockedCopy : CopyRule.Target(session.Run);
		if (t is null) return null;
		return new RestStopScreenForecast(t.Data.Id, t.Data.Name.Length > 0 ? t.Data.Name : t.Data.Id, t.Slot, t.MoveLevel, t.Level, session.CopyLocked, ExpectedCost(t));
	}
}
