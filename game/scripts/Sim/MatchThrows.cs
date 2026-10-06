using System;

namespace YokaiFighters.Sim;

public enum ThrowOutcome
{
	/// <summary>The throwbox caught a throwable defender; the break window opens.</summary>
	Grab,
	/// <summary>The window closed unbroken: damage and the standard knockdown (C4, E4).</summary>
	Land,
	/// <summary>The defender pressed the throw buttons inside the window: no damage, both pushed apart.</summary>
	Break,
	/// <summary>Both grabs caught on the same frame: treated as an automatic break.</summary>
	Clash,
}

/// <summary>One throw step. Attacker is the thrower's index (for Clash, P1 = 0).</summary>
public readonly record struct ThrowEvent(int Attacker, string MoveId, ThrowOutcome Outcome, int Damage);

/// <summary>
/// YOK-19: generic throws (C4). The thrower plays its own grab clip (a normal Attack on a kind "throw"
/// move: startup, active, recovery like any move); the defender plays only the standard knockdown, so
/// no paired animation exists. Throws ignore block. Every number comes from the move's data.
///
/// Who can be thrown: a grounded fighter that is Idle, dashing or in a move (strike or throw startup).
/// Not airborne, not in hitstun, blockstun, knockdown or another throw, not KO'd.
///
/// Timing: the grab connects on tick g. The defender is Thrown for the next BreakWindow ticks
/// (g+1..g+BreakWindow); pressing the throw buttons on one of them breaks the throw. Otherwise on tick
/// g+BreakWindow+1 the throw lands: damage, and the defender is in Knockdown for exactly
/// SimConfig.KnockdownFrames ticks (that tick included), acting again KnockdownFrames ticks later.
/// The thrower's clip keeps playing throughout (MoveLoader makes recovery outlast the window).
///
/// Priorities (E12, designer decisions): a grab and a strike active on the same frame - the grab
/// wins (grabs resolve before strikes and the grabbed fighter's move ends). Two grabs on the same frame
/// clash (automatic break, no damage). A strike landing during the throw's startup beats it as usual.
/// Input: the two throw buttons within ThrowPressWindow ticks; a normal the first button started is
/// cancelled into the throw while still in startup. A landed throw builds meter and hitstop as a heavy
/// hit (OnConnect). A Thrown defender cannot burst (TryBurst needs Hitstun): it breaks instead.
/// </summary>
public sealed partial class Match
{
	/// <summary>Grab, land, break and clash, for presentation and tests. A landed throw also raises Hit.</summary>
	public event Action<Match, ThrowEvent>? Throw;

	/// <summary>
	/// Can this fighter be grabbed this frame? Invulnerability (a burst's 20 frames, C6) is the same
	/// check strikes use in Connects; a strike's own invulnerable hurtbox frames don't avoid throws (E12).
	/// </summary>
	public static bool Throwable(Fighter d) =>
		!d.KnockedOut && !d.Invulnerable && !d.Airborne
		&& d.State is FighterState.Idle or FighterState.Dash or FighterState.Attack or FighterState.Landing; // YOK-55: landing recovery

	/// <summary>
	/// E12: the throw whose buttons were all pressed within ThrowPressWindow ticks, the last of them
	/// this tick. Read from the raw input buffer before any scheme parser, so Kata and Kihon share it
	/// (K3). -1 = none.
	/// </summary>
	public int FindThrow(Fighter f)
	{
		for (int i = 0; i < f.Moves.Length; i++)
			if (f.Moves[i].IsThrow && ThrowPressed(f.Input.Buffer, f.Moves[i].ThrowButtons)) return i;
		return -1;
	}

	/// <summary>Every button of <paramref name="buttons"/> went down within the window, one of them this tick.</summary>
	public bool ThrowPressed(InputBuffer buf, InputBits buttons)
	{
		if ((buf.PressedAt(0) & buttons) == 0) return false;
		InputBits seen = InputBits.None;
		for (int age = 0; age <= Config.ThrowPressWindow; age++) seen |= buf.PressedAt(age);
		return (seen & buttons) == buttons;
	}

	/// <summary>
	/// E12: the first throw button started a normal, and the second arrived inside the window while
	/// that normal is still in startup: the throw cancels it. Past startup the throw input is ignored.
	/// </summary>
	private static bool InThrowCancelableStartup(Fighter f)
	{
		if (f.KnockedOut || f.State != FighterState.Attack || f.Airborne) return false; // no throws from an air normal
		MoveData? m = f.CurrentMove;
		if (m is null || !m.IsNormal || f.MoveFrame >= m.FirstActive) return false;
		foreach (var t in f.Moves)
			if (t.IsThrow && (t.ThrowButtons & m.Button) != 0) return true;
		return false;
	}

	/// <summary>Start of the world frame: count the open break windows down; a closed one lands the throw.</summary>
	private void AdvanceThrows()
	{
		for (int i = 0; i < 2; i++)
		{
			Fighter d = Fighters[i];
			if (d.State != FighterState.Thrown) continue;
			if (d.StunLeft > 0) { d.StunLeft--; continue; }
			LandThrow(1 - i);
		}
	}

	/// <summary>After both fighters' input: a Thrown defender who pressed the throw buttons this tick breaks it.</summary>
	private void ResolveThrowBreaks()
	{
		for (int i = 0; i < 2; i++)
		{
			Fighter d = Fighters[i], att = Fighters[1 - i];
			if (d.State != FighterState.Thrown) continue;
			MoveData? m = att.CurrentMove;
			if (m is null || !m.IsThrow) continue;
			if (!ThrowPressed(d.Input.Buffer, m.ThrowButtons)) continue; // same E12 leniency as the throw
			d.Input.TryConsume(out _); // the break press must not come out as a normal afterwards
			BreakApart(1 - i, m, ThrowOutcome.Break);
		}
	}

	/// <summary>Both throwboxes against the other's body, tested together so same-frame grabs clash.</summary>
	private void ResolveThrows()
	{
		bool g0 = Grabs(P1, P2), g1 = Grabs(P2, P1);
		if (g0 && g1) { BreakApart(0, P1.CurrentMove!, ThrowOutcome.Clash); return; }
		if (g0) Grab(0);
		else if (g1) Grab(1);
	}

	private bool Grabs(Fighter att, Fighter def)
	{
		MoveData? m = att.CurrentMove;
		if (m is null || !m.IsThrow || att.MoveConnected || !m.IsActive(att.MoveFrame)) return false;
		if (!Throwable(def)) return false;
		// Proposal: throws test the defender's stance body box, not a move's hurtboxes (a strike's
		// invulnerable frames don't dodge a throw).
		var body = WorldBox(def, BodyHurtbox(def));
		foreach (var tb in m.Throwboxes)
			if (tb.Covers(att.MoveFrame) && Overlaps(WorldBox(att, tb.Box), body)) return true;
		return false;
	}

	private void Grab(int attIndex)
	{
		Fighter att = Fighters[attIndex], def = Fighters[1 - attIndex];
		MoveData m = att.CurrentMove!;
		att.MoveConnected = true;
		SetState(def, FighterState.Thrown, m.BreakWindow);
		def.Guarding = false; // C4: throws can't be blocked
		def.Crouching = false;
		Throw?.Invoke(this, new ThrowEvent(attIndex, m.Id, ThrowOutcome.Grab, 0));
	}

	private void LandThrow(int attIndex)
	{
		Fighter att = Fighters[attIndex], def = Fighters[1 - attIndex];
		MoveData? m = att.CurrentMove;
		if (m is null || !m.IsThrow) { ToIdle(def); return; } // defensive: the thrower's clip ended early
		def.PendingDamage += m.Damage;
		SetState(def, FighterState.Knockdown, Config.KnockdownFrames); // set before AdvanceState: down this tick + 39 more
		Slide(att, def, m.HitPushback ?? Config.HitPushback, 0);
		Throw?.Invoke(this, new ThrowEvent(attIndex, m.Id, ThrowOutcome.Land, m.Damage));
		Hit?.Invoke(this, new HitEvent(attIndex, m.Id, att.MoveFrame, false, false, m.Damage));
		OnConnect(attIndex, m, blocked: false, counter: false); // C5 meter (+6/+3), V2 hitstop as heavy (E12)
	}

	/// <summary>Break or clash: no damage, both fighters free and slid apart by the throw's on_break pushback.</summary>
	private void BreakApart(int attIndex, MoveData m, ThrowOutcome outcome)
	{
		Fighter att = Fighters[attIndex], def = Fighters[1 - attIndex];
		ToIdle(att);
		ToIdle(def);
		int push = m.BreakPushback ?? Config.BlockPushback;
		Slide(att, def, push, push);
		Throw?.Invoke(this, new ThrowEvent(attIndex, m.Id, outcome, 0));
	}

	/// <summary>Defender slides defPush units away from the attacker, attacker attPush back; a corner hands the rest over.</summary>
	private void Slide(Fighter att, Fighter def, int defPush, int attPush)
	{
		int dir = Math.Sign(def.X - att.X);
		if (dir == 0) dir = att.Facing;
		int lo = -Config.StageHalfWidth + Config.BodyWidth / 2, hi = Config.StageHalfWidth - Config.BodyWidth / 2;
		int want = defPush * SimConfig.Scale;
		int target = Math.Clamp(def.X + dir * want, lo, hi);
		int lost = want - Math.Abs(target - def.X);
		def.X = target;
		att.X = Math.Clamp(att.X - dir * (attPush * SimConfig.Scale + lost), lo, hi);
	}
}
