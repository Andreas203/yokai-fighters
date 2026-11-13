using System;

namespace YokaiFighters.Sim;

/// <summary>
/// YOK-226 (YOK-222, YOK-223 behaviours): launch, wall bounce and multi-hit, read from move data
/// (<see cref="MoveData.Launch"/>, <see cref="MoveData.WallBounce"/>, <see cref="MoveData.MultiHit"/>; A5, never keyed
/// on a move id). Integers only, no new clock (F3).
/// </summary>
public sealed partial class Match
{
	/// <summary>
	/// A body that already connected this use may connect again only through <c>multi_hit</c>: fewer than
	/// <see cref="MultiHit.Hits"/> connects so far and at least <see cref="MultiHit.Interval"/> move frames since the last.
	/// </summary>
	private static bool CanConnectAgain(Fighter att, MoveData m) =>
		!att.MoveConnected
		|| m.MultiHit is { } mh && att.HitsLanded < mh.Hits && att.MoveFrame - att.LastHitFrame >= mh.Interval;

	/// <summary>Counts a body connect of a multi-hit move (only those, so other fights hash as before).</summary>
	private static void CountConnect(Fighter att, MoveData m, int frame)
	{
		if (m.MultiHit is null) return;
		att.HitsLanded++;
		att.LastHitFrame = frame;
	}

	/// <summary>
	/// Launch (Heaven Seal) or wall bounce (Crushing Charge): the defender goes up the jump arc in hitstun until it
	/// lands, where <see cref="LandLaunched"/> knocks it down. Launch height and air time are the jump arc's
	/// (<see cref="SimConfig.JumpHeight"/> 250, <see cref="SimConfig.JumpFrames"/> 40): a proposal, rules.md gives
	/// no figure. A fighter already in the air keeps its height and rises again (the arc is mirrored onto its rising
	/// half). <paramref name="drift"/> is the sideways drift (a wall bounce comes off the wall, 0 for a launch).
	/// </summary>
	private void Launch(Fighter def, int drift)
	{
		int half = Config.JumpFrames / 2;
		def.AirFrame = def.AirFrame == 0 ? 1 : def.AirFrame > half ? Config.JumpFrames + 1 - def.AirFrame : def.AirFrame;
		def.Y = Config.JumpY(def.AirFrame);
		def.JumpDir = drift;
		def.Launched = true;
		def.AirAttackUsed = true;
		SetState(def, FighterState.Hitstun, Config.JumpFrames - def.AirFrame + 1); // in hitstun until it lands
	}

	/// <summary>A launched fighter touching down: the standard knockdown (E4), then wake-up (E24).</summary>
	private void LandLaunched(Fighter f)
	{
		f.Launched = false;
		SetState(f, FighterState.Knockdown, Config.KnockdownFrames);
	}
}
