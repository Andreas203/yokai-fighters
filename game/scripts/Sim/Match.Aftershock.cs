using System;
using System.Collections.Generic;

namespace YokaiFighters.Sim;

public enum AftershockEventKind { Spawn, Quake, Hit, Blocked, Expired }

public readonly record struct AftershockEvent(int Owner, string MoveId, AftershockEventKind Kind, int X);

/// <summary>
/// YOK-136: a pending second quake (<see cref="MoveData.AftershockDelay"/>). Spawned on the tick the owner's move
/// plays its first active frame, anchored where the owner stood and facing then; it does not follow the owner.
/// It replays the move's hitboxes <c>Delay</c> frames later: a hitbox covering move frame n is live at
/// <c>Age</c> = n − first active frame + Delay. One connect, then it is gone.
/// </summary>
public sealed class Aftershock
{
	public int Owner;
	public int Serial;
	public required MoveData Move;
	public int X, Dir;
	/// <summary>World frames since the spawn (0 on the spawn tick).</summary>
	public int Age;

	/// <summary>The move frame whose hitboxes this aftershock replays now (outside the active window = none).</summary>
	public int ReplayFrame => Move.FirstActive + Age - Move.AftershockDelay;
	public bool Live => Move.IsActive(ReplayFrame);
	public bool Done => ReplayFrame > Move.LastActive;

	/// <summary>A move-data box to world centi-units from the anchor, mirrored by the facing at spawn (ground level).</summary>
	public (int x0, int y0, int x1, int y1) WorldBox(Box b)
	{
		int s = SimConfig.Scale;
		int x0 = Dir > 0 ? X + b.X * s : X - (b.X + b.W) * s;
		return (x0, b.Y * s, x0 + b.W * s, (b.Y + b.H) * s);
	}
}

/// <summary>
/// YOK-136: aftershocks (A5 data-only evolution key <c>properties.aftershock_delay</c>). Hooks in
/// <c>Match.WorldStep</c>: <see cref="StepAftershocks"/> after <c>StepProjectiles</c> (age, expire, spawn), and
/// <see cref="ResolveAftershocks"/> after <c>ResolveProjectiles</c>. A hit is a strike from the ground with the
/// move's numbers (damage, stun, pushback, low, strength, meter); like a projectile's it doesn't mark the owner's
/// move connected, the push follows the aftershock's facing and a cornered defender hands none back.
/// </summary>
public sealed partial class Match
{
	public List<Aftershock> Aftershocks { get; } = new();
	private int _aftershockSerial;

	public event Action<Match, AftershockEvent>? AftershockChanged;

	private void StepAftershocks()
	{
		for (int k = 0; k < Aftershocks.Count; k++)
		{
			var a = Aftershocks[k];
			a.Age++;
			if (a.Done)
			{
				Aftershocks.RemoveAt(k--);
				AftershockChanged?.Invoke(this, new AftershockEvent(a.Owner, a.Move.Id, AftershockEventKind.Expired, a.X));
			}
			else if (a.ReplayFrame == a.Move.FirstActive)
				AftershockChanged?.Invoke(this, new AftershockEvent(a.Owner, a.Move.Id, AftershockEventKind.Quake, a.X));
		}
		for (int i = 0; i < 2; i++)
		{
			Fighter f = Fighters[i];
			if (f.CurrentMove is not { AftershockDelay: > 0 } m || f.MoveFrame != m.FirstActive) continue;
			var a = new Aftershock { Owner = i, Serial = ++_aftershockSerial, Move = m, X = f.X, Dir = f.Facing };
			Aftershocks.Add(a);
			AftershockChanged?.Invoke(this, new AftershockEvent(i, m.Id, AftershockEventKind.Spawn, a.X));
		}
	}

	private void ResolveAftershocks()
	{
		if (Aftershocks.Count == 0) return;
		bool any = false;
		for (int k = 0; k < Aftershocks.Count; k++)
		{
			var a = Aftershocks[k];
			if (!a.Live || !AftershockConnects(a)) continue;
			Fighter def = Fighters[1 - a.Owner];
			bool blocked = ApplyHit(a.Owner, a.Move, a.ReplayFrame, def.CurrentMove != null, remoteDir: a.Dir);
			Aftershocks.RemoveAt(k--);
			any = true;
			AftershockChanged?.Invoke(this, new AftershockEvent(a.Owner, a.Move.Id,
				blocked ? AftershockEventKind.Blocked : AftershockEventKind.Hit, a.X));
		}
		if (any) ResolvePositions(stackalloc int[2]);
	}

	private bool AftershockConnects(Aftershock a)
	{
		Fighter def = Fighters[1 - a.Owner];
		if (def.State == FighterState.Knockdown || def.KnockedOut || def.Invulnerable) return false;
		if (def.CurrentMove?.InvulnAt(def.MoveFrame, InvulnAgainst.Strike) == true) return false;
		foreach (var hb in a.Move.Hitboxes)
			if (hb.Covers(a.ReplayFrame) && HurtOverlaps(def, a.WorldBox(hb.Box))) return true;
		return false;
	}

	/// <summary>Live aftershock hitboxes of one owner in world centi-units (the debug overlay draws these).</summary>
	public IEnumerable<(int x0, int y0, int x1, int y1)> AftershockBoxes(int owner)
	{
		foreach (var a in Aftershocks)
			if (a.Owner == owner && a.Live)
				foreach (var hb in a.Move.Hitboxes)
					if (hb.Covers(a.ReplayFrame)) yield return a.WorldBox(hb.Box);
	}

	private void ResetAftershocks()
	{
		Aftershocks.Clear();
		_aftershockSerial = 0;
	}

	/// <summary>Mixed in only once an aftershock has existed, so fights without one hash exactly as before.</summary>
	private ulong HashAftershocks(ulong h)
	{
		if (_aftershockSerial == 0) return h;
		h = Fnv.Mix(h, Aftershocks.Count);
		h = Fnv.Mix(h, _aftershockSerial);
		foreach (var a in Aftershocks)
		{
			h = Fnv.Mix(h, a.Owner | (a.Dir > 0 ? 2 : 0));
			h = Fnv.Mix(h, a.Serial);
			h = Fnv.Mix(h, a.X);
			h = Fnv.Mix(h, a.Age);
			h = Fnv.Mix(h, a.Move.AftershockDelay);
			h = Fnv.Mix(h, a.Move.Damage);
		}
		return h;
	}
}
