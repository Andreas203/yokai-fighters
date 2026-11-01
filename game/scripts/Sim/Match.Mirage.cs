using System;

namespace YokaiFighters.Sim;

/// <summary>
/// YOK-224: the image Mirage Feint leaves where its owner teleported from. It stands in the idle stance with the
/// owner's facing at the teleport and absorbs <see cref="HitsLeft"/> of the opponent's hits, then vanishes.
/// Visible state (P6): <c>AiView.Decoys</c> shows it; it has no push box and can't be thrown.
/// </summary>
public sealed class Decoy
{
	public int Owner;
	public int X, Facing;
	public int HitsLeft;
}

public enum DecoyEventKind { Spawn, Absorbed, Vanished }

/// <summary>Presentation only: no sim state reads it.</summary>
public readonly record struct DecoyEvent(int Owner, DecoyEventKind Kind, int X);

/// <summary>
/// YOK-224: Fox Mirage's teleport (<see cref="MoveData.Teleport"/>) and Mirage Feint's decoy (<see cref="MoveData.Decoy"/>),
/// both from move data (A5, A10). Hooks in <c>Match.WorldStep</c>: <see cref="StepTeleport"/> after the fighter's own
/// movement, <see cref="ResolveDecoys"/> after throws and before strikes; <see cref="ResetDecoys"/> in Reset and
/// <see cref="HashDecoys"/> in StateHash. A teleport is not an invulnerability or a throw escape (E12): the move's own
/// hurtboxes stay, a grab or strike before the teleport frame stops it, and the recovery is open.
/// </summary>
public sealed partial class Match
{
	private readonly Decoy?[] _decoys = new Decoy?[2];

	/// <summary>The decoy fighter <paramref name="owner"/> left, or null. One per fighter: a new one replaces the old.</summary>
	public Decoy? DecoyOf(int owner) => _decoys[owner];

	public event Action<Match, DecoyEvent>? DecoyChanged;

	/// <summary>
	/// On the move's teleport frame the fighter lands just behind the opponent (push gap away, on the far side) and
	/// turns to face them. Behind a cornered opponent there is no room: the fighter takes the stage limit and a
	/// grounded opponent is pushed out the push gap, so neither ends in the wall or in the other's push box.
	/// </summary>
	private void StepTeleport(int i)
	{
		Fighter f = Fighters[i], o = Fighters[1 - i];
		if (f.CurrentMove is not { Teleport: { } t } m || f.MoveFrame != t.Frame) return;
		int originX = f.X, originFacing = f.Facing;
		int side = Math.Sign(o.X - f.X);
		if (side == 0) side = f.Facing;
		int gap = PushGap(f, o);
		f.X = Math.Clamp(o.X + side * gap, CornerLo(f), CornerHi(f));
		f.Facing = -side;
		if (!f.Airborne && !o.Airborne && Math.Abs(o.X - f.X) < gap) o.X = f.X - side * gap;
		if (m.Decoy > 0) SpawnDecoy(i, originX, originFacing, m.Decoy);
	}

	private void SpawnDecoy(int owner, int x, int facing, int hits)
	{
		if (_decoys[owner] is { } old) DecoyChanged?.Invoke(this, new DecoyEvent(owner, DecoyEventKind.Vanished, old.X));
		_decoys[owner] = new Decoy { Owner = owner, X = x, Facing = facing, HitsLeft = hits };
		DecoyChanged?.Invoke(this, new DecoyEvent(owner, DecoyEventKind.Spawn, x));
	}

	/// <summary>The decoy's hurtbox: the idle stance box at its spot, mirrored by its facing, on the ground.</summary>
	public (int x0, int y0, int x1, int y1) DecoyBox(Decoy d)
	{
		Box b = Config.IdleHurtbox;
		int s = SimConfig.Scale;
		int x0 = d.Facing > 0 ? d.X + b.X * s : d.X - (b.X + b.W) * s;
		return (x0, b.Y * s, x0 + b.W * s, (b.Y + b.H) * s);
	}

	/// <summary>
	/// Before strikes resolve, each decoy takes the opponent's hits that reach it: the body strike (the move is spent,
	/// counted as blocked: no damage, stun, meter or hitstop), then projectiles (each loses one hit) and aftershocks
	/// (spent). Each absorbed hit costs the decoy one; at none left it vanishes. Throws pass it by (throwboxes only
	/// grab a fighter).
	/// </summary>
	private void ResolveDecoys()
	{
		for (int owner = 0; owner < 2; owner++)
		{
			if (_decoys[owner] is not { } d) continue;
			int a = 1 - owner;
			Fighter att = Fighters[a];
			var box = DecoyBox(d);

			if (att.CurrentMove is { } m && !att.MoveConnected && m.IsActive(att.MoveFrame) && !m.IsThrow)
				foreach (var hb in m.Hitboxes)
					if (hb.Covers(att.MoveFrame) && Overlaps(WorldBox(att, hb.Box), box))
					{
						att.MoveConnected = true;
						att.MoveBlocked = true;
						Absorb(d);
						break;
					}

			for (int k = 0; k < Projectiles.Count && _decoys[owner] != null; k++)
			{
				var p = Projectiles[k];
				if (p.Owner != a || p.HitsLeft <= 0 || !Overlaps(p.WorldBox(), box)) continue;
				p.HitsLeft--; // ResolveProjectiles drops it at none left
				Absorb(d);
			}

			for (int k = 0; k < Aftershocks.Count && _decoys[owner] != null; k++)
			{
				var s = Aftershocks[k];
				if (s.Owner != a || !s.Live) continue;
				foreach (var hb in s.Move.Hitboxes)
					if (hb.Covers(s.ReplayFrame) && Overlaps(s.WorldBox(hb.Box), box))
					{
						Aftershocks.RemoveAt(k--);
						AftershockChanged?.Invoke(this, new AftershockEvent(s.Owner, s.Move.Id, AftershockEventKind.Expired, s.X));
						Absorb(d);
						break;
					}
			}
		}
	}

	/// <summary>One hit into the decoy; true when that was its last and it vanished.</summary>
	private bool Absorb(Decoy d)
	{
		if (_decoys[d.Owner] != d) return true;
		d.HitsLeft--;
		DecoyChanged?.Invoke(this, new DecoyEvent(d.Owner, DecoyEventKind.Absorbed, d.X));
		if (d.HitsLeft > 0) return false;
		_decoys[d.Owner] = null;
		DecoyChanged?.Invoke(this, new DecoyEvent(d.Owner, DecoyEventKind.Vanished, d.X));
		return true;
	}

	private void ResetDecoys() => Array.Clear(_decoys);

	/// <summary>A fight with no decoy hashes exactly as before YOK-224 (the golden-hash probes rely on it).</summary>
	private ulong HashDecoys(ulong h)
	{
		foreach (var d in _decoys)
		{
			if (d is null) continue;
			h = Fnv.Mix(h, 0x4445 + d.Owner); // marks whose decoy follows
			h = Fnv.Mix(h, d.X);
			h = Fnv.Mix(h, d.Facing);
			h = Fnv.Mix(h, d.HitsLeft);
		}
		return h;
	}
}
