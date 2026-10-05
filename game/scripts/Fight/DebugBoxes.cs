using System.Collections.Generic;
using YokaiFighters.Sim;

namespace YokaiFighters.Fight;

public enum DebugBoxKind { Hit, Hurt, Throw, Push }

/// <summary>One box to draw, in world centi-units (same space as <see cref="Fighter.X"/>). Spent = the move already connected.</summary>
public readonly record struct DebugBox(DebugBoxKind Kind, int X0, int Y0, int X1, int Y1, bool Spent = false);

/// <summary>
/// YOK-22: what the sim itself uses this tick, read-only. Pure C# (no Godot) so tests check it exactly.
/// Hit/hurt boxes follow <c>Match.Connects</c>: the move's data boxes covering the current move frame,
/// mirrored by facing through <see cref="Match.WorldBox"/>; out of a move the fight-wide idle hurtbox;
/// none while knocked down or KO'd (the sim can't hit them).
/// </summary>
public static class DebugBoxes
{
	public static List<DebugBox> For(Match match, int fighter)
	{
		var list = new List<DebugBox>();
		Fighter f = match.Fighters[fighter];
		SimConfig c = match.Config;

		// Push box: the sim's push is 1-D (BodyWidth between centres), drawn idle-hurtbox tall for readability.
		int half = c.BodyWidth / 2;
		list.Add(new DebugBox(DebugBoxKind.Push, f.X - half, f.Y + c.IdleHurtbox.Y * SimConfig.Scale,
			f.X + half, f.Y + (c.IdleHurtbox.Y + c.IdleHurtbox.H) * SimConfig.Scale));

		MoveData? m = f.CurrentMove;
		bool hittable = !f.KnockedOut && f.State != FighterState.Knockdown;
		if (m is null)
		{
			if (hittable) list.Add(Make(DebugBoxKind.Hurt, f, match.BodyHurtbox(f)));
			return list;
		}

		if (hittable)
			foreach (var hu in m.Hurtboxes)
				if (hu.Covers(f.MoveFrame)) list.Add(Make(DebugBoxKind.Hurt, f, hu.Box));
		if (m.IsActive(f.MoveFrame))
			foreach (var hb in m.Hitboxes)
				if (hb.Covers(f.MoveFrame)) list.Add(Make(DebugBoxKind.Hit, f, hb.Box, f.MoveConnected));
		foreach (var tb in ThrowBoxes(f))
			list.Add(Make(DebugBoxKind.Throw, f, tb));
		return list;
	}

	/// <summary>
	/// YOK-19 (C4): the current throw move's throwboxes covering this frame, as <c>Match.Grabs</c> tests
	/// them; none once the grab connected (or on a whiff's recovery).
	/// </summary>
	public static IEnumerable<Box> ThrowBoxes(Fighter f)
	{
		MoveData? m = f.CurrentMove;
		if (m is null || !m.IsThrow || f.MoveConnected || !m.IsActive(f.MoveFrame)) yield break;
		foreach (var tb in m.Throwboxes)
			if (tb.Covers(f.MoveFrame)) yield return tb.Box;
	}

	static DebugBox Make(DebugBoxKind kind, Fighter f, Box b, bool spent = false)
	{
		var (x0, y0, x1, y1) = Match.WorldBox(f, b);
		return new DebugBox(kind, x0, y0, x1, y1, spent);
	}

	/// <summary>Label like "Attack test-jab (slot 1) 5/13 active  stun 0".</summary>
	public static string Label(Fighter f)
	{
		string s = f.KnockedOut ? "KO" : f.State.ToString();
		MoveData? m = f.CurrentMove;
		if (m != null)
		{
			string phase = f.MoveFrame <= m.Startup ? "startup" : m.IsActive(f.MoveFrame) ? "active" : "recovery";
			s += $" {m.Id} (slot {f.MoveSlot + 1}) {f.MoveFrame}/{m.TotalFrames} {phase}";
		}
		return s + $"  stun {f.StunLeft}";
	}
}

/// <summary>
/// YOK-22 frame-step gate on the fixed-step clock's output. While paused, the ticks the clock reports
/// are dropped (so resuming doesn't burst) and each step request runs exactly one tick.
/// </summary>
public sealed class FrameStepper
{
	public bool Paused { get; private set; }
	private int _pendingSteps;

	public void TogglePause() { Paused = !Paused; _pendingSteps = 0; }
	public void Resume() { Paused = false; _pendingSteps = 0; }

	/// <summary>One press = one tick on the next Filter. Pauses first if running.</summary>
	public void RequestStep()
	{
		if (!Paused) { Paused = true; return; }
		_pendingSteps++;
	}

	/// <summary>Ticks to actually run given the clock's due count.</summary>
	public int Filter(int due)
	{
		if (!Paused) return due;
		int n = _pendingSteps;
		_pendingSteps = 0;
		return n;
	}
}
