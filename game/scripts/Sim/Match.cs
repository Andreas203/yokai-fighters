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
public sealed partial class Match
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
		// YOK-47: run health carries into the fight (C1, R3).
		if (Config.P1StartHealth is int h1) P1.Health = Math.Clamp(h1, 1, Config.P1MaxHealth);
		if (Config.P2StartHealth is int h2) P2.Health = Math.Clamp(h2, 1, Config.P2MaxHealth);
		Phase = MatchPhase.Fighting;
		Tick = 0;
		WorldFrame = 0;
		KoFrame = 0;
		_koTicks = 0;
		Winner = -1;
		ResetMeterState(); // YOK-20
		Projectiles.Clear(); // YOK-21
		_projectileSerial = 0;
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

		if (HitstopStep(in1, in2)) return; // V2 (YOK-20): both fighters frozen
		WorldStep(in1, in2);
	}

	private void WorldStep(FighterInput in1, FighterInput in2)
	{
		WorldFrame++;
		AdvanceThrows(); // YOK-19: break windows count down; an unbroken throw lands
		Span<FighterInput> inputs = stackalloc FighterInput[] { in1, in2 };
		Span<int> moved = stackalloc int[2];

		for (int i = 0; i < 2; i++)
		{
			Fighter f = Fighters[i], o = Fighters[1 - i];
			FighterInput input = inputs[i];
			AdvanceState(f);
			f.WalkDir = 0; // YOK-39: set below only by this tick's own walk step
			f.Input.Update(input, f.Facing); // YOK-17 parser; numpad relative to facing, SOCD-clean
			TryBurst(i); // C6 (YOK-20): from hitstun; the fighter is then not actionable

			int pad = f.Input.Direction;
			int rel = pad % 3 == 0 ? 1 : pad % 3 == 1 ? -1 : 0; // 3/6/9 forward, 1/4/7 back
			int dir = rel * f.Facing;                           // absolute walk direction
			bool up = pad >= 7, down = pad <= 3;
			int before = f.X;
			bool canGuard = !f.Airborne && f.State is FighterState.Idle or FighterState.Blockstun;
			f.Crouching = canGuard && down;
			f.Guarding = canGuard && rel < 0; // C3: hold back, standing or crouching (E6)

			int slot = -1;
			if (input.Move > 0 && input.Move <= f.Moves.Length && CanStartSlot(f, input.Move - 1)) slot = input.Move - 1; // explicit request (tests, AI); air normals only in a jump (YOK-55)
			else if (f.Actionable && input.IsSpecialRequest(out var reqSlot, out bool reqEx))
				TryStartSpecial(i, reqSlot, reqEx, precision: false, InputBits.None); // YOK-21: direct slot request
			else if ((f.Actionable || InThrowCancelableStartup(f)) && (slot = FindThrow(f)) >= 0) f.Input.TryConsume(out _); // E12
			else if (f.Actionable && f.Input.TryConsume(out var cmd))
			{
				// YOK-21: a special command fires its slot's special; an empty slot (or no room for another
				// projectile) gives the button's normal instead.
				if (cmd.Kind != CommandKind.Special || !TryStartSpecial(i, cmd.Slot, ExPair(cmd.Pressed), cmd.Precision, cmd.Pressed))
					slot = FindNormal(f, cmd with { Kind = CommandKind.Normal });
			}
			else if (CanAirAttack(f) && f.Input.TryConsume(out var airCmd))
				slot = FindNormal(f, airCmd with { Kind = CommandKind.Normal }); // E11 (YOK-55): the button's air normal
			else if (f.State == FighterState.Attack && !f.Airborne && !TryExUpgrade(i)) TryCancel(i); // YOK-21 hooks; none in the air

			if (slot >= 0) StartMove(f, slot);
			else if (f.Actionable && up) StartJump(f, dir);
			else if (f.Actionable && f.Input.Dash != 0) StartDash(f, f.Input.Dash * f.Facing);
			else if (f.Actionable && !f.Crouching) { f.X += dir * Config.WalkSpeed; f.WalkDir = dir; } // YOK-39: WalkDir tells walking from being pushed

			if (f.State == FighterState.Dash)
				f.X += f.DashDir * Config.DashStep(f.DashFrame, f.DashDir == f.Facing ? Config.DashDistance : Config.BackDashDistance);
			if (f.Airborne) f.X += f.JumpDir * Config.JumpSpeedX;

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

		ResolveThrowBreaks(); // YOK-19: after both fighters' input, so neither side acts first
		ResolvePositions(moved);
		UpdateFacing();
		StepProjectiles();    // YOK-21: travel, despawn, spawn on the move's spawn frame
		ResolveThrows();      // YOK-19: grabs before strikes, so a grabbed fighter's strike never lands
		ResolveHits();
		ResolveProjectiles(); // YOK-21: clashes, then projectile hits
		ApplyDamage();
	}

	/// <summary>
	/// Start-of-frame state step. A move started on tick t plays frame n on tick t+n-1 and the fighter
	/// acts again on tick t+TotalFrames. A stun of N frames set on tick h holds ticks h+1..h+N; the
	/// fighter acts on tick h+N+1. So advantage = stun - (TotalFrames - frame the hit landed on).
	/// </summary>
	private void AdvanceState(Fighter f)
	{
		// Jump arc (C2): air frames 1..JumpFrames, then land. The arc keeps going through air hitstun.
		if (f.Airborne)
		{
			if (++f.AirFrame > Config.JumpFrames)
			{
				f.AirFrame = 0;
				f.JumpDir = 0;
				f.Y = 0;
				f.AirAttackUsed = false;
				if (f.State == FighterState.Jump) ToIdle(f);
				else if (f.State == FighterState.Attack) LandAirAttack(f); // E11: touchdown ends an air normal
			}
			else f.Y = Config.JumpY(f.AirFrame);
		}

		switch (f.State)
		{
			case FighterState.Attack:
				if (++f.MoveFrame <= f.ActiveMove!.TotalFrames) break;
				if (f.Airborne) SetState(f, FighterState.Jump, 0); // air normal over before touchdown: fall
				else ToIdle(f);
				break;
			case FighterState.Dash:
				if (++f.DashFrame > Config.DashFrames) ToIdle(f);
				break;
			case FighterState.Hitstun:
			case FighterState.Blockstun:
			case FighterState.Knockdown:
			case FighterState.Burst:
			case FighterState.Landing:
				if (f.StunLeft > 0) f.StunLeft--;
				else if (f.Airborne) { SetState(f, FighterState.Jump, 0); f.AirAttackUsed = true; } // stun over mid-air: fall, no control (no air normal)
				else ToIdle(f);
				break;
		}
	}

	private static void StartMove(Fighter f, int slot)
	{
		f.State = FighterState.Attack;
		f.ResetSpecialMoveState();
		f.ActiveMove = f.Moves[slot];
		f.MoveSlot = slot;
		f.MoveFrame = 1;
		f.MoveConnected = false;
		f.Guarding = false;
		f.Crouching = false;
		if (f.Airborne) f.AirAttackUsed = true; // E11: one air normal per jump
	}

	/// <summary>A jump started on tick t is air frame 1 on tick t; the fighter acts again on t+JumpFrames.</summary>
	private void StartJump(Fighter f, int dir)
	{
		SetState(f, FighterState.Jump, 0);
		f.AirFrame = 1;
		f.Y = Config.JumpY(1);
		f.JumpDir = dir;
		f.AirAttackUsed = false;
		f.Guarding = false;
		f.Crouching = false;
	}

	/// <summary>A dash started on tick t plays dash frame n on tick t+n-1; the fighter acts again on t+DashFrames.</summary>
	private static void StartDash(Fighter f, int dir)
	{
		SetState(f, FighterState.Dash, 0);
		f.DashFrame = 1;
		f.DashDir = dir;
		f.Guarding = false;
		f.Crouching = false;
	}

	/// <summary>
	/// The normal for a parsed command: same button, and a listed direction beats an any-direction
	/// normal (ties by slot order). Kata and Kihon commands look identical here (K3). -1 = none.
	/// E11 (YOK-55): airborne fighters pick only air normals, grounded ones only ground normals.
	/// </summary>
	public static int FindNormal(Fighter f, in InputCommand cmd)
	{
		if (cmd.Kind != CommandKind.Normal) return -1; // specials: mapped to slots A-D by the special system
		int best = -1, bestScore = 0;
		for (int i = 0; i < f.Moves.Length; i++)
		{
			if (f.Moves[i].Air != f.Airborne) continue;
			int score = f.Moves[i].NormalMatch(cmd.Button, cmd.Direction);
			if (score > bestScore) { best = i; bestScore = score; }
		}
		return best;
	}

	private static void ToIdle(Fighter f) => SetState(f, FighterState.Idle, 0);

	private static void SetState(Fighter f, FighterState state, int stun)
	{
		f.State = state;
		f.MoveSlot = -1;
		f.MoveFrame = 0;
		f.MoveConnected = false;
		f.StunLeft = stun;
		f.DashFrame = 0;
		f.DashDir = 0;
		f.ResetSpecialMoveState();
		if (state == FighterState.Idle) f.CancelUsed = 0; // X3: the combo is over
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
		if (def.State == FighterState.Knockdown || def.KnockedOut || def.Invulnerable) return false;
		InvulnAgainst kind = att.Airborne ? InvulnAgainst.Strike | InvulnAgainst.Air : InvulnAgainst.Strike;
		if (def.CurrentMove?.InvulnAt(def.MoveFrame, kind) == true) return false; // YOK-21: properties.invuln
		foreach (var hb in m.Hitboxes)
			if (hb.Covers(att.MoveFrame) && HurtOverlaps(def, WorldBox(att, hb.Box))) return true;
		return false;
	}

	/// <summary>A world box against the defender's hurtboxes: its move's boxes on this frame, else its stance box.</summary>
	private bool HurtOverlaps(Fighter def, (int x0, int y0, int x1, int y1) a)
	{
		MoveData? dm = def.CurrentMove;
		if (dm is null) return Overlaps(a, WorldBox(def, BodyHurtbox(def)));
		foreach (var hu in dm.Hurtboxes) // frames a move gives no hurtbox are invulnerable, by data
			if (hu.Covers(def.MoveFrame) && Overlaps(a, WorldBox(def, hu.Box))) return true;
		return false;
	}

	/// <summary>Hurtbox of a fighter not in a move, by stance (SimConfig: proposed sizes).</summary>
	public Box BodyHurtbox(Fighter f) =>
		f.Airborne ? Config.AirHurtbox : f.Crouching ? Config.CrouchHurtbox : Config.IdleHurtbox;

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

	/// <param name="projectile">YOK-21: the hit came from this projectile, not the attacker's body: the attacker's
	/// move doesn't count as connected, the push follows the projectile and a cornered defender hands none back.</param>
	private bool ApplyHit(int attIndex, MoveData m, int frame, bool defenderWasAttacking, Projectile? projectile = null)
	{
		Fighter att = Fighters[attIndex], def = Fighters[1 - attIndex];
		bool blocked = def.Guarding && (!m.Low || def.Crouching); // C3: lows only crouching
		if (projectile is null)
		{
			att.MoveConnected = true;
			att.MoveBlocked = blocked;
		}

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
		def.JumpDir = 0; // a hit in the air stops the drift; the fighter still falls along the arc

		// Pushback: the defender slides away from the attacker; what a corner stops goes to the attacker.
		int dir = projectile?.Dir ?? att.Facing, want = push * SimConfig.Scale;
		int lo = -Config.StageHalfWidth + Config.BodyWidth / 2, hi = Config.StageHalfWidth - Config.BodyWidth / 2;
		int target = Math.Clamp(def.X + dir * want, lo, hi);
		int got = Math.Abs(target - def.X);
		def.X = target;
		if (projectile is null) att.X = Math.Clamp(att.X - dir * (want - got), lo, hi);

		Hit?.Invoke(this, new HitEvent(attIndex, m.Id, frame, blocked, counter, damage));
		OnConnect(attIndex, m, blocked, counter); // C5 meter, V2 hitstop (YOK-20)
		return blocked;
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

		// Push boxes: grounded fighters never overlap; each gives half. A jumper passes over (C2).
		bool grounded = a.Y == 0 && b.Y == 0;
		int dx = b.X - a.X;
		if (grounded && Math.Abs(dx) < Config.BodyWidth)
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
		if (grounded && Math.Abs(dx) < Config.BodyWidth)
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
		h = P2.Hash(h);
		return HashProjectiles(HashMeterState(h));
	}
}
