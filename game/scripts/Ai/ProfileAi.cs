using System;
using System.Collections.Generic;
using YokaiFighters.Sim;

namespace YokaiFighters.Ai;

/// <summary>One logged AI decision. Tick = the sim tick the resulting input first applies to; ViewTick = the
/// screen it was decided from (Tick - ViewTick = reaction frames). Kind: habit / habit_covered / habit_no /
/// react / neutral / unavailable; YOK-57: ex / ex_no (trigger self_meter), burst / burst_no (trigger self_in_combo).</summary>
public readonly record struct AiDecision(int Tick, int ViewTick, string Kind, string Trigger, string Action, int Roll, int Pct);

/// <summary>
/// The behaviour-profile fighter AI (Y1, Y2, Y4, P6). Each tick the owner passes <see cref="AiView.Capture"/>
/// of the current screen; the AI only decides from the screen <see cref="ReactionFrames"/> ticks old, so
/// something first shown on tick j can change its output on tick j + ReactionFrames at the earliest. It
/// outputs a <see cref="FighterInput"/> like a player (direction bits, raw throw buttons, direct
/// normal/special slot requests), so Kihon/Kata, throws and specials all go through the same sim code (K3).
/// Deterministic: every roll is on its own seeded <see cref="SimRng"/>.
/// </summary>
public sealed class ProfileAi
{
	/// <summary>Range bands in gameplay units between the fighters (proposed for the designer).</summary>
	public const int CloseRange = 250, MidRange = 600;
	/// <summary>Neutral weight of "move to the preferred range" next to the profile's standing behaviours (proposed).</summary>
	public const int SpacingWeight = 40;
	public const int WalkTicks = 15, SpacingTicks = 10, BlockTicks = 20;
	/// <summary>
	/// YOK-165: pre-emptive guard rate when the profile names no <c>guard_pct</c>: 0, off (designer 2026-10-20; profiles opt in).
	/// Every strike normal hits within 15 frames, faster than any reaction tier (Y4), so a guard that waits to see the
	/// swing is always late (YOK-154 F1). The guard is rolled on the AI's own decisions, never on anything Ryo pressed (P6).
	/// </summary>
	public const int DefaultGuardPct = 0;
	/// <summary>YOK-165: how long the guard is held after the AI's own move ends (proposed, = <see cref="SpacingTicks"/>).</summary>
	public const int GuardTicks = 10;

	public BehaviourProfile Profile { get; }
	public int Self { get; }
	public int ReactionFrames { get; }
	public List<AiDecision>? Log { get; set; }
	/// <summary>YOK-157: the AI is playing out a habit plan (Y2) this tick. Read-only; for the harness's habit-punish log.</summary>
	public bool HabitPlanRunning => _plan is { Habit: true, Done: false };
	/// <summary>YOK-57: the fight this AI is in. E22: ex_use / burst_use apply only in elder and Tanuki fights.</summary>
	public AiFightTier Tier { get; }
	/// <summary>YOK-99, Y5: aggression-bias points added by Tanuki wins (<see cref="TanukiPressure.AggressionBonus"/>); 0 = none.</summary>
	public int AggressionBonus { get; }
	/// <summary>The bias the neutral weights use: the profile's own plus <see cref="AggressionBonus"/>, within 0-100.</summary>
	public int Aggression => (int)Math.Clamp((long)Profile.AggressionBias + AggressionBonus, 0, 100); // long: no overflow before the clamp
	/// <summary>
	/// YOK-165: 0-100, the chance the AI holds crouch guard (down-back: blocks lows and mids, E6) instead of standing
	/// open (a) when it settles in its preferred range to wait and (b) through its own move's recovery and
	/// <see cref="GuardTicks"/> after it. Ctor argument, else the profile's <c>guard_pct</c>, else <see cref="DefaultGuardPct"/>.
	/// 0 = no roll at all, so the draw order is the one from before YOK-165 (old replays record no guard and read as 0).
	/// </summary>
	public int GuardPct { get; }
	private bool SpendsMeter => Tier != AiFightTier.Standard;

	private readonly AiLoadout _kit;
	private readonly SimRng _rng;
	private readonly AiView?[] _seen;
	private int _calls;
	private AiView? _prev;
	private Plan? _plan;
	private int _blockedInRow;
	private string? _oppLastSpecial;
	private int _comboHits, _burstPressTick = -1;
	private readonly bool[] _rose = new bool[Enum.GetValues<Observation>().Length];

	public ProfileAi(BehaviourProfile profile, int self, AiLoadout kit, uint seed, int? reactionFrames = null,
		AiFightTier tier = AiFightTier.Standard, int aggressionBonus = 0, int? guardPct = null)
	{
		GuardPct = Math.Clamp(guardPct ?? profile.GuardPct ?? DefaultGuardPct, 0, 100);
		Tier = tier;
		AggressionBonus = aggressionBonus;
		Profile = profile;
		Self = self;
		_kit = kit;
		_rng = new SimRng(seed);
		ReactionFrames = Math.Max(1, reactionFrames ?? profile.ReactionFrames);
		_seen = new AiView?[ReactionFrames];
	}

	/// <summary>Feed this tick's screen; returns the input for the next sim step.</summary>
	public FighterInput Next(AiView now)
	{
		// Delay buffer: keep the last ReactionFrames screens, decide from the oldest (the first screen until full).
		_seen[_calls % ReactionFrames] = now;
		AiView view = _calls >= ReactionFrames - 1 ? _seen[(_calls - ReactionFrames + 1) % ReactionFrames]! : _seen[0]!;
		_calls++;
		int tick = now.Tick + 1; // the clock only: the step this output feeds
		AiView? prev = _prev;
		_prev = view;

		if (view.Phase != MatchPhase.Fighting) { _plan = null; return FighterInput.None; }

		FighterView s = view.Fighter(Self), o = view.Fighter(1 - Self);
		bool[] rose = _rose;
		Array.Clear(rose);
		if (prev is not null && prev.Tick != view.Tick)
		{
			Events(prev, view, rose);
			// YOK-57 (C6, E13): a burst out of a combo the AI sees itself in comes before everything else.
			if (Burst(prev, view, s, tick) is { } burst) { _plan = burst; return _plan.Emit(); }
		}

		// The habit (Y2) first. A wake-up habit is rolled when the AI sees itself go down and pre-holds its
		// direction through the knockdown and the wake-up, so it comes out on the first actionable frame (and is punishable).
		var h = Profile.Habit;
		bool wakeUp = h.When == Observation.SelfGotUp;
		if (rose[(int)(wakeUp ? Observation.SelfKnockedDown : h.When)])
		{
			int roll = _rng.Next(100);
			bool fire = roll < h.ChancePct, covered = false;
			int cover = -1;
			if (fire && Profile.TellCoverPct > 0) { cover = _rng.Next(100); covered = cover < Profile.TellCoverPct; }
			string kind = !fire ? "habit_no" : covered ? "habit_covered" : "habit";
			Record(tick, view, kind, h.When, h.Do.ToString(), roll, h.ChancePct);
			if (fire && !covered)
			{
				int wake = view.Tick + view.HitstopLeft + s.StunLeft + 1 + (s.State == FighterState.Knockdown ? view.WakeUpFrames : 0);
				_plan = Build(h.Do, view, tick, true, wakeUp ? Math.Max(0, wake - tick) : 0);
			}
		}

		// Event reactions: each weight is a percent rate per occurrence (anti-air rate, block habit...).
		// YOK-165, Y4: with a tier jump-punish rate, the jump's attacking answers are one roll at that rate (JumpPunish),
		// taken where the first opponent_jumping behaviour sits in the list; its other answers roll by weight after a miss.
		if (_plan is not { Habit: true })
		{
			int? jumpRate = rose[(int)Observation.OpponentJumping] ? view.JumpPunishPct(Self) : null;
			bool jumpRolled = false;
			foreach (var b in Profile.Behaviours)
			{
				if (!BehaviourProfile.IsEvent(b.When) || !rose[(int)b.When]) continue;
				if (jumpRate is { } rate && b.When == Observation.OpponentJumping)
				{
					if (!jumpRolled)
					{
						jumpRolled = true;
						if (JumpPunish(rate, view, tick) is { } punish) { _plan = punish; break; }
					}
					if (b.Do.IsMove) continue; // the attacks had their one roll
				}
				int roll = _rng.Next(100);
				if (roll >= b.Weight) continue;
				var p = Build(b.Do, view, tick, false, 0);
				Record(tick, view, p is null ? "unavailable" : "react", b.When, b.Do.ToString(), roll, b.Weight);
				if (p is null) continue;
				_plan = p;
				break;
			}
		}

		// Neutral: a weighted pick among standing behaviours that hold now, plus spacing toward the preferred range.
		if (_plan is null || _plan.Done) _plan = Neutral(view, s, o, tick);
		return _plan.Emit();
	}

	/// <summary>
	/// YOK-165, Y4: the fight's tier rate (<see cref="AiView.JumpPunishPct"/>, node-scenes <c>jump_punish_pct</c>) is the
	/// absolute chance the AI answers a jump it sees with an attack (designer 2026-10-20: elders and the Tanuki 100,
	/// apprentice and yokai 50). One draw at that rate; on a hit, a weighted pick among the profile's attacking answers
	/// to <c>opponent_jumping</c> (a second draw only when there are several). Null, so the other answers (block...)
	/// roll by weight, on a miss, when the profile has no attacking answer (no draw at all), or when the pick is unavailable.
	/// </summary>
	private Plan? JumpPunish(int rate, AiView view, int tick)
	{
		int total = 0, count = 0;
		Behaviour? pick = null;
		foreach (var b in Profile.Behaviours)
			if (b.When == Observation.OpponentJumping && b.Do.IsMove) { total += b.Weight; count++; pick ??= b; }
		if (pick is null) return null;
		int roll = _rng.Next(100);
		if (roll >= rate) return null;
		if (count > 1)
		{
			int w = _rng.Next(total);
			foreach (var b in Profile.Behaviours)
			{
				if (b.When != Observation.OpponentJumping || !b.Do.IsMove) continue;
				if (w < b.Weight) { pick = b; break; }
				w -= b.Weight;
			}
		}
		var p = Build(pick.Do, view, tick, false, 0);
		Record(tick, view, p is null ? "unavailable" : "react", Observation.OpponentJumping, pick.Do.ToString(), roll, rate);
		return p;
	}

	/// <summary>YOK-165: roll the pre-emptive guard (no draw at all when <see cref="GuardPct"/> is 0).</summary>
	private bool RollGuard(int tick, AiView view, string trigger)
	{
		if (GuardPct <= 0) return false;
		int roll = _rng.Next(100);
		bool guard = roll < GuardPct;
		if (guard) Record(tick, view, "guard", trigger, "crouch_block", roll, GuardPct);
		return guard;
	}

	private Plan Neutral(AiView view, FighterView s, FighterView o, int tick)
	{
		Observation range = RangeOf(s, o);
		int total = SpacingWeight;
		Span<int> w = stackalloc int[Profile.Behaviours.Count];
		for (int i = 0; i < w.Length; i++)
		{
			var b = Profile.Behaviours[i];
			bool holds = b.When == Observation.Neutral || b.When == range || (b.When == Observation.OpponentBlocking && o.State == FighterState.Blockstun);
			w[i] = BehaviourProfile.IsEvent(b.When) || !holds ? 0 : Math.Max(1, b.Weight * (b.Do.Offensive ? 50 + Aggression : 150 - Aggression) / 100);
			total += w[i];
		}
		int roll = _rng.Next(total);
		for (int i = 0; i < w.Length; i++)
		{
			if (roll >= w[i]) { roll -= w[i]; continue; }
			var b = Profile.Behaviours[i];
			var p = Build(b.Do, view, tick, false, 0);
			Record(tick, view, p is null ? "unavailable" : "neutral", b.When, b.Do.ToString(), roll, w[i]);
			if (p is not null) return p;
			break;
		}
		// Spacing: walk toward the preferred band, away when too close, else hold still.
		int diff = (int)Profile.PreferredRange - (int)range;
		var (fwd, back) = Dirs(s, o);
		InputBits bits = diff > 0 ? back : diff < 0 ? fwd : InputBits.None; // preferred band is farther: back off
		Record(tick, view, "neutral", Observation.Neutral, diff > 0 ? "space_back" : diff < 0 ? "space_forward" : "hold", roll, SpacingWeight);
		// YOK-165: settled in its band, it waits in crouch guard rather than standing open (walking back already guards, C3).
		if (diff == 0 && RollGuard(tick, view, "self_waiting")) bits = back | InputBits.Down;
		return new Plan(false, (new FighterInput(bits), SpacingTicks));
	}

	private Plan? Build(AiAction a, AiView view, int tick, bool habit, int preHold)
	{
		var (fwd, back) = Dirs(view.Fighter(Self), view.Fighter(1 - Self));
		var none = FighterInput.None;
		(FighterInput, int)[]? steps = a.Kind switch
		{
			ActionKind.WalkForward => new[] { (new FighterInput(fwd), WalkTicks) },
			ActionKind.WalkBack => new[] { (new FighterInput(back), WalkTicks) },
			ActionKind.DashForward => new[] { (new FighterInput(fwd), 1), (none, 1), (new FighterInput(fwd), 1), (none, SimConfig.Default.DashFrames) },
			ActionKind.DashBack => new[] { (new FighterInput(back), 1), (none, 1), (new FighterInput(back), 1), (none, SimConfig.Default.DashFrames) },
			ActionKind.Jump => new[] { (new FighterInput(InputBits.Up), 2), (none, SimConfig.Default.JumpFrames) },
			ActionKind.Block => new[] { (new FighterInput(back), BlockTicks) },
			ActionKind.CrouchBlock => new[] { (new FighterInput(back | InputBits.Down), BlockTicks) },
			ActionKind.Throw => _kit.ThrowButtons == InputBits.None ? null : new[] { (new FighterInput(_kit.ThrowButtons), 1), (none, _kit.ThrowFrames) },
			_ => _kit.TryGet(a, out var input, out int frames) ? new[] { (MaybeEx(a, input, view, tick), 1), (none, Math.Max(1, frames - 1)) } : null,
		};
		if (steps is null) return null;
		// YOK-165: after a move of its own (whiffed or not) it may hold crouch guard through the recovery, so it is guarding
		// on its first free frame, and a beat after. Not after the habit: that stays as punishable as the profile says (Y2).
		if (!habit && a.IsMove && RollGuard(tick, view, "self_attacked"))
		{
			var guard = new FighterInput(back | InputBits.Down);
			var tail = new (FighterInput, int)[steps.Length + 1];
			Array.Copy(steps, tail, steps.Length);
			for (int i = 1; i < steps.Length; i++) tail[i] = (guard, steps[i].Item2); // the wait steps; the press stays as built
			tail[^1] = (guard, GuardTicks);
			steps = tail;
		}
		if (preHold <= 0) return new Plan(habit, steps);
		// Hold the action's direction until the predicted first actionable tick, then do it.
		var all = new (FighterInput, int)[steps.Length + 1];
		all[0] = (new FighterInput(steps[0].Item1.Bits & InputBits.Directions), preHold);
		Array.Copy(steps, 0, all, 1, steps.Length);
		return new Plan(habit, all);
	}

	/// <summary>
	/// YOK-57 (E16, C5), elder and Tanuki fights only (E22): a special the AI decided on comes out as EX with the profile's chance, only when its own
	/// meter on screen covers the EX cost plus the reserve. The sim still checks the live meter: without it the plain
	/// version comes out and nothing is spent.
	/// </summary>
	private FighterInput MaybeEx(AiAction a, FighterInput plain, AiView view, int tick)
	{
		var e = Profile.ExUse;
		if (!SpendsMeter || e is null || e.ChancePct <= 0 || !_kit.TryGetEx(a, out var ex, out int cost)) return plain;
		if (view.Fighter(Self).Meter < cost + e.KeepMeter) return plain;
		int roll = _rng.Next(100);
		bool use = roll < e.ChancePct;
		Record(tick, view, use ? "ex" : "ex_no", "self_meter", a.ToString(), roll, e.ChancePct);
		return use ? ex : plain;
	}

	/// <summary>
	/// YOK-57 (C6, E13), elder and Tanuki fights only (E22): on each hit the AI sees itself take, roll the profile's burst chance once the combo is long
	/// enough, its health low enough and its burst seal still lit, and only if the hitstun it sees will still run on
	/// the tick the press lands (view tick + hitstop + stun left). A thrown fighter is never in hitstun, so it never
	/// rolls (and the sim refuses a burst outside hitstun anyway).
	/// </summary>
	private Plan? Burst(AiView prev, AiView view, FighterView s, int tick)
	{
		FighterView ps = prev.Fighter(Self);
		bool hit = s.State == FighterState.Hitstun && (ps.State != FighterState.Hitstun || s.StunLeft > ps.StunLeft);
		if (s.State != FighterState.Hitstun) _comboHits = 0;
		else if (hit) _comboHits++;
		var b = Profile.BurstUse;
		if (!hit || !SpendsMeter || b is null || b.ChancePct <= 0 || s.BurstUsed || s.KnockedOut) return null;
		if (_burstPressTick >= 0 && view.Tick < _burstPressTick) return null; // our press has not shown yet
		if (_comboHits < b.MinComboHits) return null;
		if (s.MaxHealth > 0 && (long)s.Health * 100 > (long)b.MaxHealthPct * s.MaxHealth) return null;
		if (view.Tick + view.HitstopLeft + s.StunLeft - 1 < tick) return null; // that hitstun is over by then
		int roll = _rng.Next(100);
		bool fire = roll < b.ChancePct;
		Record(tick, view, fire ? "burst" : "burst_no", "self_in_combo", "burst", roll, b.ChancePct);
		if (!fire) return null;
		_burstPressTick = tick;
		return new Plan(false, (new FighterInput(Match.BurstButtons), 1), (FighterInput.None, Math.Max(1, view.BurstFrames)));
	}

	private void Events(AiView prev, AiView view, bool[] rose)
	{
		FighterView s = view.Fighter(Self), o = view.Fighter(1 - Self), ps = prev.Fighter(Self), po = prev.Fighter(1 - Self);
		void Edge(Observation ob, bool now, bool before) { if (now && !before) rose[(int)ob] = true; }

		Edge(Observation.OpponentJumping, o.Airborne, po.Airborne);
		Edge(Observation.OpponentKnockedDown, o.State == FighterState.Knockdown, po.State == FighterState.Knockdown);
		Edge(Observation.SelfKnockedDown, s.State == FighterState.Knockdown, ps.State == FighterState.Knockdown);
		Edge(Observation.SelfGotUp, ps.State.IsDown() && !s.State.IsDown(), false); // after the wake-up, on the first actionable frame
		bool shot = HasShot(view), shotBefore = HasShot(prev);
		Edge(Observation.ProjectileOnScreen, shot, shotBefore);
		Edge(Observation.OpponentInRecovery, o.Recovering, po.Recovering);
		Edge(Observation.OpponentAttacking, o.Threatening, po.Threatening);

		bool blocked = s.State == FighterState.Blockstun && (ps.State != FighterState.Blockstun || s.StunLeft > ps.StunLeft);
		if (s.State is not (FighterState.Idle or FighterState.Blockstun or FighterState.Dash)) _blockedInRow = 0;
		if (blocked)
		{
			if (shotBefore) rose[(int)Observation.BlockedProjectile] = true;
			if (++_blockedInRow == 2) rose[(int)Observation.BlockedTwoHitsInRow] = true;
		}

		// Whiff: a move with active frames leaves them without having connected.
		if (o.Recovering && po.Attacking && po.MoveId == o.MoveId && po.MoveFrame <= po.MoveLastActive
			&& o.MoveFirstActive <= o.MoveLastActive && !o.MoveConnected)
			rose[(int)Observation.OpponentWhiffed] = true;

		bool started = o.Attacking && (!po.Attacking || po.MoveId != o.MoveId || o.MoveFrame < po.MoveFrame);
		if (started)
		{
			if (o.MoveIsSpecial && o.MoveId == _oppLastSpecial) rose[(int)Observation.OpponentUsedSameSpecialTwice] = true;
			_oppLastSpecial = o.MoveIsSpecial ? o.MoveId : null;
		}
	}

	private bool HasShot(AiView v)
	{
		foreach (var p in v.Projectiles) if (p.Owner != Self) return true;
		return false;
	}

	public static Observation RangeOf(FighterView s, FighterView o)
	{
		int d = Math.Abs(o.X - s.X) / SimConfig.Scale;
		return d < CloseRange ? Observation.AtCloseRange : d < MidRange ? Observation.AtMidRange : Observation.AtFullScreen;
	}

	private static (InputBits fwd, InputBits back) Dirs(FighterView s, FighterView o)
	{
		int side = o.X != s.X ? Math.Sign(o.X - s.X) : s.Facing;
		return side > 0 ? (InputBits.Right, InputBits.Left) : (InputBits.Left, InputBits.Right);
	}

	private void Record(int tick, AiView view, string kind, Observation when, string action, int roll, int pct) =>
		Record(tick, view, kind, BehaviourProfile.ObservationName(when), action, roll, pct);

	private void Record(int tick, AiView view, string kind, string trigger, string action, int roll, int pct) =>
		Log?.Add(new AiDecision(tick, view.Tick, kind, trigger, action, roll, pct));

	/// <summary>A queued sequence of inputs; a slot request is sent on the first tick of its step only.</summary>
	private sealed class Plan
	{
		public bool Habit { get; }
		private readonly (FighterInput In, int Ticks)[] _steps;
		private int _i, _t;
		public Plan(bool habit, params (FighterInput, int)[] steps) { Habit = habit; _steps = steps; }
		public bool Done => _i >= _steps.Length;
		public FighterInput Emit()
		{
			if (Done) return FighterInput.None;
			var (input, ticks) = _steps[_i];
			var result = _t == 0 ? input : new FighterInput(input.Bits);
			if (++_t >= ticks) { _i++; _t = 0; }
			return result;
		}
	}
}
