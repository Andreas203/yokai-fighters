using System;

namespace YokaiFighters.Sim;

public enum MatchPhase
{
	/// <summary>Round in progress; inputs drive the fighters.</summary>
	Fighting,
	/// <summary>V4: the round-ending blow plays at half speed for KoSlowFrames world frames.</summary>
	KoSlowMo,
	/// <summary>Round over; waits for Reset (later: the ink-stroke binding, results).</summary>
	Over,
}

/// <summary>
/// One round between two fighters (C1: one round, 60 ticks/s, deterministic). Pure C#: the
/// presentation layer reads it and never writes it, so the same code runs headless for the
/// harness bot. Step() is exactly one 1/60 s tick; a world frame is one tick of fighter motion
/// (the two differ only during the KO slow-down).
/// </summary>
public sealed class Match
{
	public SimConfig Config { get; }
	public Fighter[] Fighters { get; } = { new(), new() };
	public Fighter P1 => Fighters[0];
	public Fighter P2 => Fighters[1];

	public MatchPhase Phase { get; private set; }
	/// <summary>Match ticks since reset (always 60 per second).</summary>
	public int Tick { get; private set; }
	/// <summary>World frames since reset (slower than ticks during the KO slow-down).</summary>
	public int WorldFrame { get; private set; }
	/// <summary>World frames played since the KO blow landed (0..KoSlowFrames).</summary>
	public int KoFrame { get; private set; }
	/// <summary>0 or 1 when someone won, -1 for a double KO or while fighting.</summary>
	public int Winner { get; private set; } = -1;

	private int _koTicks;

	public event Action<Match>? KnockOut;
	public event Action<Match>? RoundOver;

	public Match(SimConfig? config = null)
	{
		Config = config ?? SimConfig.Default;
		Reset();
	}

	public void Reset()
	{
		P1.Reset(-Config.StartOffset, +1, Config.P1MaxHealth);
		P2.Reset(+Config.StartOffset, -1, Config.P2MaxHealth);
		Phase = MatchPhase.Fighting;
		Tick = 0;
		WorldFrame = 0;
		KoFrame = 0;
		_koTicks = 0;
		Winner = -1;
	}

	/// <summary>Queue damage on a fighter; applied at the end of the current world frame.</summary>
	public void QueueDamage(int fighterIndex, int damage)
	{
		if (Phase == MatchPhase.Fighting) Fighters[fighterIndex].PendingDamage += damage;
	}

	/// <summary>Advance exactly one 1/60 s tick.</summary>
	public void Step(FighterInput in1, FighterInput in2)
	{
		if (Phase == MatchPhase.Over) return;
		Tick++;

		if (Phase == MatchPhase.KoSlowMo)
		{
			// Inputs are ignored after the blow; the world advances one frame every KoSlowDivisor ticks.
			_koTicks++;
			if (_koTicks % Config.KoSlowDivisor == 0)
			{
				WorldFrame++;
				KoFrame++;
				if (KoFrame >= Config.KoSlowFrames)
				{
					Phase = MatchPhase.Over;
					RoundOver?.Invoke(this);
				}
			}
			return;
		}

		WorldStep(in1, in2);
	}

	private void WorldStep(FighterInput in1, FighterInput in2)
	{
		WorldFrame++;
		Span<FighterInput> inputs = stackalloc FighterInput[] { in1, in2 };
		Span<int> moved = stackalloc int[2];

		for (int i = 0; i < 2; i++)
		{
			Fighter f = Fighters[i], o = Fighters[1 - i];
			FighterInput input = inputs[i];

			int dir = (input.Has(InputBits.Right) ? 1 : 0) - (input.Has(InputBits.Left) ? 1 : 0);
			int before = f.X;
			f.X += dir * Config.WalkSpeed;

			if (f.Pressed(input, InputBits.DebugCrossUp))
			{
				// Placeholder for a side-switching move (jump-over, Fox Mirage): land just behind the opponent.
				int side = Math.Sign(o.X - before);
				if (side == 0) side = f.Facing;
				f.X = o.X + side * Config.BodyWidth;
			}
			if (f.Pressed(input, InputBits.DebugStrike))
				Fighters[1 - i].PendingDamage += Config.DebugStrikeDamage;

			moved[i] = f.X - before;
			f.PrevBits = input.Bits;
		}

		ResolvePositions(moved);
		UpdateFacing();
		ApplyDamage();
	}

	/// <summary>Screen walls, push boxes and stage corners, in that order of priority (corners win).</summary>
	private void ResolvePositions(ReadOnlySpan<int> moved)
	{
		Fighter a = P1, b = P2;

		// Screen walls: walking away from the opponent stops at MaxSeparation. The fighter(s) that
		// moved outward give the distance back.
		int dist = Math.Abs(b.X - a.X);
		if (dist > Config.MaxSeparation)
		{
			int excess = dist - Config.MaxSeparation;
			int awayA = Math.Sign(a.X - b.X), awayB = -awayA;
			int outA = Math.Max(0, moved[0] * awayA), outB = Math.Max(0, moved[1] * awayB);
			int takeA = Math.Min(outA, (excess + 1) / 2);
			int takeB = Math.Min(outB, excess - takeA);
			takeA = Math.Min(outA, excess - takeB);
			a.X -= takeA * awayA;
			b.X -= takeB * awayB;
			int rest = excess - takeA - takeB; // e.g. a teleport: pull both in
			a.X -= (rest + 1) / 2 * awayA;
			b.X -= rest / 2 * awayB;
		}

		// Push boxes: grounded fighters never overlap; each gives half.
		int dx = b.X - a.X;
		if (Math.Abs(dx) < Config.BodyWidth)
		{
			int side = dx != 0 ? Math.Sign(dx) : a.Facing; // side of b relative to a
			int overlap = Config.BodyWidth - Math.Abs(dx);
			a.X -= side * ((overlap + 1) / 2);
			b.X += side * (overlap / 2);
		}

		// Stage corners: clamp, then push the other fighter out so the cornered one stays put.
		int lo = -Config.StageHalfWidth + Config.BodyWidth / 2;
		int hi = Config.StageHalfWidth - Config.BodyWidth / 2;
		a.X = Math.Clamp(a.X, lo, hi);
		b.X = Math.Clamp(b.X, lo, hi);
		dx = b.X - a.X;
		if (Math.Abs(dx) < Config.BodyWidth)
		{
			int side = dx != 0 ? Math.Sign(dx) : a.Facing; // side of b relative to a
			Fighter left = side > 0 ? a : b, right = side > 0 ? b : a;
			// Only possible against a corner: the fighter nearest the wall keeps it.
			if (left.X - lo < hi - right.X) right.X = left.X + Config.BodyWidth;
			else left.X = right.X - Config.BodyWidth;
		}
	}

	/// <summary>Fighters always face each other; on equal X they keep their last facing.</summary>
	private void UpdateFacing()
	{
		int dx = P2.X - P1.X;
		if (dx == 0) return;
		P1.Facing = Math.Sign(dx);
		P2.Facing = -P1.Facing;
	}

	private void ApplyDamage()
	{
		bool anyKo = false;
		foreach (var f in Fighters)
		{
			if (f.PendingDamage == 0) continue;
			f.Health = Math.Max(0, f.Health - f.PendingDamage);
			f.PendingDamage = 0;
			if (f.Health == 0) { f.KnockedOut = true; anyKo = true; }
		}
		if (!anyKo) return;

		Winner = P1.KnockedOut && P2.KnockedOut ? -1 : P1.KnockedOut ? 1 : 0;
		Phase = MatchPhase.KoSlowMo;
		_koTicks = 0;
		KoFrame = 0;
		KnockOut?.Invoke(this);
	}

	public ulong StateHash()
	{
		ulong h = Fnv.Offset;
		h = Fnv.Mix(h, (int)Phase);
		h = Fnv.Mix(h, Tick);
		h = Fnv.Mix(h, WorldFrame);
		h = Fnv.Mix(h, KoFrame);
		h = Fnv.Mix(h, _koTicks);
		h = Fnv.Mix(h, Winner);
		h = P1.Hash(h);
		return P2.Hash(h);
	}
}
