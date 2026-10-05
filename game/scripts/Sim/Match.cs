using System;

namespace YokaiFighters.Sim;

/// <summary>One move connecting. Attacker is the fighter index; Frame is the move frame it landed on.</summary>
public readonly record struct HitEvent(int Attacker, string MoveId, int Frame, bool Blocked, bool Counter, int Damage);

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
	/// <summary>A move connected (hit or block). Raised during the world frame, before damage applies.</summary>
	public event Action<Match, HitEvent>? Hit;

	/// <param name="p1Moves">P1's moves by slot, loaded from data at fight start (MoveLoader).</param>
	public Match(SimConfig? config = null, MoveData[]? p1Moves = null, MoveData[]? p2Moves = null)
	{
		Config = config ?? SimConfig.Default;
		P1.Moves = p1Moves ?? Array.Empty<MoveData>();
		P2.Moves = p2Moves ?? Array.Empty<MoveData>();
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
			AdvanceState(f);

			int dir = (input.Has(InputBits.Right) ? 1 : 0) - (input.Has(InputBits.Left) ? 1 : 0);
			int before = f.X;
			bool canGuard = f.State is FighterState.Idle or FighterState.Blockstun;
			f.Crouching = canGuard && input.Has(InputBits.Down);
			f.Guarding = canGuard && dir == -f.Facing; // C3: hold back

			if (f.Actionable && input.Move > 0 && input.Move <= f.Moves.Length)
			{
				f.State = FighterState.Attack;
				f.MoveSlot = input.Move - 1;
				f.MoveFrame = 1;
				f.MoveConnected = false;
				f.Guarding = false;
				f.Crouching = false;
			}
			else if (f.Actionable && !f.Crouching)
			{
				f.X += dir * Config.WalkSpeed;
			}

			if (f.Actionable && f.Pressed(input, InputBits.DebugCrossUp))
			{
				// Placeholder for a side-switching move (jump-over, Fox Mirage): land just behind the opponent.
				int side = Math.Sign(o.X - before);
				if (side == 0) side = f.Facing;
				f.X = o.X + side * Config.BodyWidth;
			}
			if (f.Actionable && f.Pressed(input, InputBits.DebugStrike))
				Fighters[1 - i].PendingDamage += Config.DebugStrikeDamage;

			moved[i] = f.X - before;
			f.PrevBits = input.Bits;
		}

		ResolvePositions(moved);
		UpdateFacing();
		ResolveHits();
		ApplyDamage();
	}

	/// <summary>
	/// Start-of-frame state step. A move started on tick t plays frame n on tick t+n-1 and the fighter
	/// acts again on tick t+TotalFrames. A stun of N frames set on tick h holds ticks h+1..h+N; the
	/// fighter acts on tick h+N+1. So advantage = stun - (TotalFrames - frame the hit landed on).
	/// </summary>
	private static void AdvanceState(Fighter f)
	{
		switch (f.State)
		{
			case FighterState.Attack:
				if (++f.MoveFrame > f.Moves[f.MoveSlot].TotalFrames) ToIdle(f);
				break;
			case FighterState.Hitstun:
			case FighterState.Blockstun:
			case FighterState.Knockdown:
				if (f.StunLeft == 0) ToIdle(f);
				else f.StunLeft--;
				break;
		}
	}

	private static void ToIdle(Fighter f) => SetState(f, FighterState.Idle, 0);

	private static void SetState(Fighter f, FighterState state, int stun)
	{
		f.State = state;
		f.MoveSlot = -1;
		f.MoveFrame = 0;
		f.MoveConnected = false;
		f.StunLeft = stun;
	}

	/// <summary>
	/// Both fighters' hitboxes are tested against the other's hurtboxes as they stood this frame,
	/// then applied together, so two moves that connect on the same frame trade.
	/// </summary>
	private void ResolveHits()
	{
		bool hit0 = Connects(P1, P2), hit1 = Connects(P2, P1);
		if (!hit0 && !hit1) return;
		// Snapshot both moves before either hit applies: a trade interrupts both (C7: both counterhit).
		MoveData? m0 = P1.CurrentMove, m1 = P2.CurrentMove;
		int f0 = P1.MoveFrame, f1 = P2.MoveFrame;
		if (hit0) ApplyHit(0, m0!, f0, m1 != null);
		if (hit1) ApplyHit(1, m1!, f1, m0 != null);
		ResolvePositions(stackalloc int[2]);
	}

	private bool Connects(Fighter att, Fighter def)
	{
		MoveData? m = att.CurrentMove;
		if (m is null || att.MoveConnected || !m.IsActive(att.MoveFrame)) return false;
		if (def.State == FighterState.Knockdown || def.KnockedOut) return false;
		MoveData? dm = def.CurrentMove;
		foreach (var hb in m.Hitboxes)
		{
			if (!hb.Covers(att.MoveFrame)) continue;
			var a = WorldBox(att, hb.Box);
			if (dm is null)
			{
				if (Overlaps(a, WorldBox(def, Config.IdleHurtbox))) return true;
				continue;
			}
			foreach (var hu in dm.Hurtboxes) // frames a move gives no hurtbox are invulnerable, by data
				if (hu.Covers(def.MoveFrame) && Overlaps(a, WorldBox(def, hu.Box))) return true;
		}
		return false;
	}

	private static bool Overlaps((int x0, int y0, int x1, int y1) a, (int x0, int y0, int x1, int y1) b) =>
		a.x0 < b.x1 && b.x0 < a.x1 && a.y0 < b.y1 && b.y0 < a.y1;

	/// <summary>Move-data box (units, x toward the opponent) to world centi-units, mirrored by facing.</summary>
	public static (int x0, int y0, int x1, int y1) WorldBox(Fighter f, Box b)
	{
		int s = SimConfig.Scale;
		int x0 = f.Facing > 0 ? f.X + b.X * s : f.X - (b.X + b.W) * s;
		int y0 = f.Y + b.Y * s;
		return (x0, y0, x0 + b.W * s, y0 + b.H * s);
	}

	private void ApplyHit(int attIndex, MoveData m, int frame, bool defenderWasAttacking)
	{
		Fighter att = Fighters[attIndex], def = Fighters[1 - attIndex];
		att.MoveConnected = true;

		bool blocked = def.Guarding && (!m.Low || def.Crouching); // C3: lows only crouching
		bool counter = !blocked && defenderWasAttacking;           // C7
		int push, damage = 0;
		if (blocked)
		{
			SetState(def, FighterState.Blockstun, m.Blockstun);
			push = m.BlockPushback ?? Config.BlockPushback;
		}
		else
		{
			damage = counter ? m.Damage * (100 + Config.CounterHitDamagePct) / 100 : m.Damage;
			def.PendingDamage += damage;
			if (m.Knockdown) SetState(def, FighterState.Knockdown, Config.KnockdownFrames);
			else SetState(def, FighterState.Hitstun, m.Hitstun + (counter ? Config.CounterHitHitstun : 0));
			push = m.HitPushback ?? Config.HitPushback;
		}
		def.Guarding = false;
		def.Crouching = false;

		// Pushback: the defender slides away from the attacker; what a corner stops goes to the attacker.
		int dir = att.Facing, want = push * SimConfig.Scale;
		int lo = -Config.StageHalfWidth + Config.BodyWidth / 2, hi = Config.StageHalfWidth - Config.BodyWidth / 2;
		int target = Math.Clamp(def.X + dir * want, lo, hi);
		int got = Math.Abs(target - def.X);
		def.X = target;
		att.X = Math.Clamp(att.X - dir * (want - got), lo, hi);

		Hit?.Invoke(this, new HitEvent(attIndex, m.Id, frame, blocked, counter, damage));
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

	/// <summary>
	/// Free fighters turn to face the opponent; on equal X they keep their last facing. Facing is held
	/// during a move, hitstun, blockstun and knockdown, so a crossed-up attack keeps its direction.
	/// </summary>
	private void UpdateFacing()
	{
		int dx = P2.X - P1.X;
		if (dx == 0) return;
		if (P1.State == FighterState.Idle) P1.Facing = Math.Sign(dx);
		if (P2.State == FighterState.Idle) P2.Facing = -Math.Sign(dx);
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
