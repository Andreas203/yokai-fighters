using System;
using System.Collections.Generic;
using YokaiFighters.Ai;
using YokaiFighters.Sim;

namespace YokaiFighters.Harness;

/// <summary>
/// YOK-26: what Ryo's own fighter can do, as the player knows it from his move list: ground normals (button +
/// direction), the throw buttons, and which special slots are filled. Built once at fight start from Ryo's
/// fighter (his own kit, never the opponent's state or input).
/// </summary>
public sealed class BotKit
{
	public readonly record struct Press(InputBits Bits, int Frames, string? Id = null);

	public IReadOnlyList<Press> Normals { get; }
	/// <summary>Filled slots A-D with each special's total frames.</summary>
	public IReadOnlyList<(SpecialSlot Slot, int Frames)> Specials { get; }
	public Press Throw { get; }
	/// <summary>YOK-157: total frames of each of Ryo's own moves, by id (his move list, never the opponent's).</summary>
	public IReadOnlyDictionary<string, int> MoveFrames { get; }

	BotKit(List<Press> normals, List<(SpecialSlot, int)> specials, Press thrw, IReadOnlyDictionary<string, int> moveFrames)
	{
		MoveFrames = moveFrames;
		Normals = normals;
		Specials = specials;
		Throw = thrw;
	}

	public static BotKit From(Fighter f)
	{
		var normals = new List<Press>();
		Press thrw = default;
		var frames = new Dictionary<string, int>(StringComparer.Ordinal);
		foreach (var m in f.Moves)
		{
			frames[m.Id] = m.TotalFrames;
			if (m.IsThrow) { if (thrw.Bits == InputBits.None) thrw = new Press(m.ThrowButtons, m.TotalFrames); continue; }
			if (!m.IsNormal || m.Air) continue;
			normals.Add(new Press(m.Button | DirectionBits(m.DirectionMask), m.TotalFrames, m.Id));
		}
		var specials = new List<(SpecialSlot, int)>();
		for (int s = (int)SpecialSlot.A; s <= (int)SpecialSlot.D; s++)
			if (f.Specials[s] is { } eq) specials.Add(((SpecialSlot)s, eq.Move.TotalFrames));
		for (int s = (int)SpecialSlot.A; s <= (int)SpecialSlot.D; s++)
			if (f.Specials[s] is { } eq) frames[eq.Move.Id] = eq.Move.TotalFrames;
		return new BotKit(normals, specials, thrw, frames);
	}

	/// <summary>
	/// The direction a normal is pressed with, as numpad bits for facing right (the bot flips them by facing):
	/// none when the move takes any direction or neutral, else its lowest listed numpad direction.
	/// </summary>
	static InputBits DirectionBits(int mask)
	{
		if (mask == 0 || (mask & (1 << 5)) != 0) return InputBits.None;
		for (int d = 1; d <= 9; d++) if ((mask & (1 << d)) != 0) return Numpad.ToBits(d, 1);
		return InputBits.None;
	}
}

/// <summary>
/// YOK-26: the player-side newcomer bot for one fight, on Kihon (K2). Like the yokai AI (P6) it is fed
/// <see cref="AiView"/> snapshots only and acts on the one <see cref="BotFightConfig.ReactionFrames"/> ticks old,
/// so it reacts to what is on screen, never to the opponent's buttons. It outputs raw buttons only (no slot
/// requests): normals = button (+ direction), specials = Special + direction (neutral A, forward B, back C,
/// down D), throw = the throw buttons. Newcomer habits, all from data: decides every <c>decide_every</c> ticks,
/// guards (standing) against a seen threat with <c>block_pct</c>, anti-airs a seen jump with <c>anti_air_pct</c>, picks
/// neutral actions by weight with simple spacing, holds a standing guard through <c>idle_guard_pct</c> of its wait and
/// in-range walk plans (YOK-155), and on <c>mistake_pct</c> of its decisions presses a random
/// normal whatever the range. The one thing it reads off the live screen is its own fighter (whether its move is
/// over), never the opponent. No EX, burst, throw breaks or crouch guard. Deterministic: one seeded <see cref="SimRng"/>.
/// </summary>
public sealed class NewcomerBot
{
	public const int Self = 0;
	/// <summary>E7: ticks a recognised command waits to be used; a press sent later than this before Ryo can act is lost.</summary>
	static readonly int CommandBuffer = InputConfig.Default.CommandBuffer;

	enum PlanKind { None, Walk, Wait, Block, Attack }

	readonly BotFightConfig _cfg;
	readonly BotKit _kit;
	readonly SimRng _rng;
	readonly AiView?[] _seen;
	readonly BotKit.Press? _antiAir;
	int _calls, _nextDecision;
	bool _threatRolled, _jumpRolled;
	/// <summary>YOK-157 (designer): this fight's first jump seen under a held idle guard has passed unanswered.</summary>
	bool _guardJumpSeen;
	AiView? _prev;
	readonly List<(InputBits Bits, int Ticks)> _steps = new();
	int _step, _stepTick, _sincePress;
	PlanKind _plan;
	/// <summary>YOK-155/166: the plan is a standing guard (idle guard or reactive block); <see cref="Emit"/> resolves back from the delayed view every tick.</summary>
	bool _holdAway;

	/// <summary>How many decisions of each kind the bot took (for the results file).</summary>
	public BotStats Stats { get; } = new();
	/// <summary>YOK-157: the id of the normal the bot anti-airs with (null when Ryo has no ground normal).</summary>
	public string? AntiAirMove => _antiAir?.Id;

	public NewcomerBot(BotFightConfig cfg, BotKit kit, uint seed)
	{
		_cfg = cfg;
		_kit = kit;
		_rng = new SimRng(seed == 0 ? 1u : seed);
		_seen = new AiView?[cfg.ReactionFrames];
		foreach (var n in kit.Normals)
			if ((n.Bits & InputBits.Attacks) == cfg.AntiAirButton) { _antiAir = n; break; }
		if (_antiAir is null && kit.Normals.Count > 0) _antiAir = kit.Normals[0];
	}

	/// <summary>Feed this tick's screen; returns Ryo's raw input for the next sim step.</summary>
	public FighterInput Next(AiView now)
	{
		int n = _seen.Length;
		_seen[_calls % n] = now;
		AiView view = _calls >= n - 1 ? _seen[(_calls - n + 1) % n]! : _seen[0]!;
		_calls++;
		AiView? prev = _prev;
		_prev = view;
		if (view.Phase != MatchPhase.Fighting) { Clear(); return FighterInput.None; }

		// Its own fighter it reads off the live screen: a move that is over, or a press that never came out, ends the attack plan.
		FighterView me = now.Fighter(Self);
		if (_plan == PlanKind.Attack && _sincePress >= 2 && me.State == FighterState.Idle) Clear();

		FighterView s = view.Fighter(Self), o = view.Fighter(1 - Self);
		bool interruptible = _plan is PlanKind.None or PlanKind.Walk or PlanKind.Wait;
		if (prev is not null && prev.Tick != view.Tick)
		{
			FighterView po = prev.Fighter(1 - Self);
			bool attackShown = o.Threatening && (!po.Threatening || po.MoveId != o.MoveId || o.MoveFrame < po.MoveFrame);
			if (attackShown || Shots(view) > Shots(prev)) _threatRolled = false; // a new threat gets its own roll
		}
		// A threat = a swing in progress or one of the opponent's projectiles on screen. The bot rolls block_pct once per
		// threat, on the first tick it sees it while free to react (not mid-move); a guard, once up, is held while it lasts.
		bool threat = o.Threatening || Shots(view) > 0;
		if (!threat) _threatRolled = false;
		// YOK-157 (R5; Y4 delay kept): a jump gets one anti_air_pct roll, on the first tick (of the delayed screen) the bot
		// is free while it sees the opponent airborne, not only on the tick the jump first shows, when the bot is often
		// mid-move. Free = no plan to finish and the press will come out (FreeToPress).
		if (!o.Airborne) _jumpRolled = false;
		// YOK-157, designer decision: holding an idle guard (YOK-155), the bot keeps guarding through the FIRST jump it
		// sees in this fight (no anti-air roll for that jump); having seen the pattern, it rolls for every later jump as usual.
		if (o.Airborne && !_jumpRolled && !_guardJumpSeen && _holdAway && (_plan is PlanKind.Wait or PlanKind.Walk))
		{
			_guardJumpSeen = true;
			_jumpRolled = true;
			Stats.GuardedJumpsLetGo++;
		}
		if (threat && interruptible && !_threatRolled)
		{
			_threatRolled = true;
			if (Roll(_cfg.BlockPct))
			{
				// YOK-166 (designer 2026-10-20, new R5 baseline): the guard holds away from the opponent as the delayed view shows
				// it each tick (P6), like the YOK-155 idle guard, so a cross-up mid-block does not turn it into a walk forward.
				Start(PlanKind.Block, (Back(s, o), _cfg.BlockTicks));
				_holdAway = true;
				Stats.Blocks++;
			}
		}
		else if (o.Airborne && interruptible && FreeToPress(me, now.HitstopLeft) && !_jumpRolled && _antiAir is { } aa)
		{
			_jumpRolled = true;
			if (Roll(_cfg.AntiAirPct))
			{
				Start(PlanKind.Attack, (aa.Bits, 1), (InputBits.None, Math.Max(1, aa.Frames - 1)));
				Stats.AntiAirs++;
			}
		}
		if (_plan == PlanKind.Block && threat) _stepTick = 0;

		if (_plan == PlanKind.None && _calls >= _nextDecision)
		{
			Decide(s, o);
			_nextDecision = _calls + _cfg.DecideEvery;
		}
		return Emit(me.Facing, s, o);
	}

	/// <summary>
	/// YOK-157: Ryo (live screen, his own fighter) can act now, or his move ends, hitstop included, within the E7 command
	/// buffer so a press sent now is used. Not in stun, knockdown, a throw or the air, where it would be lost.
	/// </summary>
	bool FreeToPress(FighterView me, int hitstopLeft) =>
		me.State == FighterState.Idle
		|| me.State == FighterState.Attack && me.MoveId is { } id && _kit.MoveFrames.TryGetValue(id, out int total)
			&& total - me.MoveFrame + hitstopLeft < CommandBuffer;

	void Decide(FighterView s, FighterView o)
	{
		int dist = Math.Abs(o.X - s.X) / SimConfig.Scale;
		if (_kit.Normals.Count > 0 && Roll(_cfg.MistakePct))
		{
			Normal(_kit.Normals[_rng.Next(_kit.Normals.Count)]); // a button pressed whatever the range
			Stats.Mistakes++;
			return;
		}
		int walk = _cfg.WalkWeight, normal = _kit.Normals.Count > 0 ? _cfg.NormalWeight : 0, special = _kit.Specials.Count > 0 ? _cfg.SpecialWeight : 0;
		int thrw = _kit.Throw.Bits != InputBits.None ? _cfg.ThrowWeight : 0, jump = _cfg.JumpWeight, wait = _cfg.WaitWeight;
		int total = walk + normal + special + thrw + jump + wait;
		if (total == 0) { Wait(s, o); return; }
		int r = _rng.Next(total);
		if ((r -= walk) < 0) Walk(s, o, dist);
		else if ((r -= normal) < 0)
		{
			if (dist <= _cfg.PokeRange) Normal(_kit.Normals[_rng.Next(_kit.Normals.Count)]);
			else Approach(s, o);
		}
		else if ((r -= special) < 0)
		{
			var (slot, frames) = _kit.Specials[_rng.Next(_kit.Specials.Count)];
			Start(PlanKind.Attack, (InputBits.Special | SlotDirection(slot), 1), (InputBits.None, Math.Max(1, frames - 1)));
			Stats.Specials++;
		}
		else if ((r -= thrw) < 0)
		{
			if (dist <= _cfg.ThrowRange)
			{
				Start(PlanKind.Attack, (_kit.Throw.Bits, 1), (InputBits.None, Math.Max(1, _kit.Throw.Frames - 1)));
				Stats.Throws++;
			}
			else Approach(s, o);
		}
		else if ((r -= jump) < 0)
		{
			InputBits drift = _rng.Next(2) == 0 ? InputBits.None : InputBits.Right; // forward, as facing right (flipped in Emit)
			Start(PlanKind.Attack, (InputBits.Up | drift, 2), (InputBits.None, SimConfig.Default.JumpFrames));
			Stats.Jumps++;
		}
		else Wait(s, o);
	}

	/// <summary>
	/// YOK-155: a wait plan, with a standing guard (hold back, C3) on <c>idle_guard_pct</c> of them, rolled once per plan.
	/// Strike normals hit sooner than the bot's reaction, so a guard up before the swing is the only one that can block it.
	/// </summary>
	void Wait(FighterView s, FighterView o)
	{
		bool guard = IdleGuard();
		Start(PlanKind.Wait, (guard ? Back(s, o) : InputBits.None, _cfg.WaitTicks));
		_holdAway = guard;
	}

	/// <summary>YOK-155: one <c>idle_guard_pct</c> roll; 0 draws nothing from the RNG, so a 0 config plays exactly as before.</summary>
	bool IdleGuard()
	{
		if (!Roll(_cfg.IdleGuardPct)) return false;
		Stats.IdleGuards++;
		return true;
	}

	void Walk(FighterView s, FighterView o, int dist)
	{
		if (dist > _cfg.PreferredMax) Approach(s, o);
		else if (dist < _cfg.PreferredMin) Start(PlanKind.Walk, (Back(s, o), _cfg.WalkTicks));
		else
		{
			bool guard = IdleGuard(); // YOK-155: in range, a guarded walk backs off; one roll per plan, before the old coin flip
			Start(PlanKind.Walk, (guard || _rng.Next(2) != 0 ? Back(s, o) : Forward(s, o), _cfg.WalkTicks));
			_holdAway = guard;
		}
		Stats.Walks++;
	}

	void Approach(FighterView s, FighterView o) => Start(PlanKind.Walk, (Forward(s, o), _cfg.WalkTicks));

	void Normal(BotKit.Press p)
	{
		Start(PlanKind.Attack, (p.Bits, 1), (InputBits.None, Math.Max(1, p.Frames - 1)));
		Stats.Normals++;
	}

	/// <summary>K2 / E18: neutral = A, forward = B, back = C, down = D (as numpad, flipped by facing in <see cref="Emit"/>).</summary>
	static InputBits SlotDirection(SpecialSlot slot) => slot switch
	{
		SpecialSlot.B => Numpad.ToBits(6, 1),
		SpecialSlot.C => Numpad.ToBits(4, 1),
		SpecialSlot.D => Numpad.ToBits(2, 1),
		_ => InputBits.None,
	};

	void Start(PlanKind kind, params (InputBits, int)[] steps)
	{
		_plan = kind;
		_holdAway = false;
		_steps.Clear();
		_steps.AddRange(steps);
		_step = _stepTick = _sincePress = 0;
	}

	void Clear()
	{
		_plan = PlanKind.None;
		_holdAway = false;
		_steps.Clear();
	}

	/// <summary>
	/// The current plan step. Attack plans store directions as if facing right and are flipped by the fighter's
	/// facing on the live screen; walk plans already hold absolute directions. A guard plan (YOK-155 guarded wait or walk, YOK-166 block)
	/// holds away from the opponent as the delayed view <paramref name="s"/>/<paramref name="o"/> shows it this tick,
	/// so a cross-up mid-plan does not turn the guard into a walk forward. Buttons go down on a step's first tick only.
	/// </summary>
	FighterInput Emit(int facing, FighterView s, FighterView o)
	{
		if (_plan == PlanKind.None || _step >= _steps.Count) { Clear(); return FighterInput.None; }
		var (bits, ticks) = _steps[_step];
		_sincePress++;
		if (_plan == PlanKind.Attack) bits = Flip(bits, facing);
		if (_holdAway) bits = (bits & ~(InputBits.Left | InputBits.Right)) | Back(s, o);
		if (_stepTick > 0) bits &= ~(InputBits.Attacks | InputBits.Special);
		if (++_stepTick >= ticks) { _step++; _stepTick = 0; }
		if (_step >= _steps.Count) _plan = PlanKind.None;
		return new FighterInput(bits);
	}

	static InputBits Flip(InputBits bits, int facing)
	{
		if (facing >= 0) return bits;
		bool l = (bits & InputBits.Left) != 0, r = (bits & InputBits.Right) != 0;
		bits &= ~(InputBits.Left | InputBits.Right);
		if (l) bits |= InputBits.Right;
		if (r) bits |= InputBits.Left;
		return bits;
	}

	static int Side(FighterView s, FighterView o) => o.X != s.X ? Math.Sign(o.X - s.X) : s.Facing;
	static InputBits Forward(FighterView s, FighterView o) => Side(s, o) > 0 ? InputBits.Right : InputBits.Left;
	static InputBits Back(FighterView s, FighterView o) => Side(s, o) > 0 ? InputBits.Left : InputBits.Right;

	static int Shots(AiView v)
	{
		int c = 0;
		foreach (var p in v.Projectiles) if (p.Owner != Self) c++;
		return c;
	}

	bool Roll(int pct) => pct > 0 && _rng.Next(100) < pct;
}

/// <summary>YOK-26: counts of the bot's choices in one fight.</summary>
public sealed class BotStats
{
	public int Walks, Normals, Specials, Throws, Jumps, Blocks, AntiAirs, Mistakes;
	/// <summary>YOK-155: wait and in-range walk plans held as a standing guard.</summary>
	public int IdleGuards;
	/// <summary>YOK-157: jumps let go under a held idle guard (at most one per fight).</summary>
	public int GuardedJumpsLetGo;
}
