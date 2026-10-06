using System;
using System.Collections.Generic;
using YokaiFighters.Sim;

namespace YokaiFighters.Ai;

/// <summary>One logged AI decision. Tick = the sim tick the resulting input first applies to; ViewTick = the
/// screen it was decided from (Tick - ViewTick = reaction frames). Kind: habit / habit_covered / habit_no /
/// react / neutral / unavailable.</summary>
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

	public BehaviourProfile Profile { get; }
	public int Self { get; }
	public int ReactionFrames { get; }
	public List<AiDecision>? Log { get; set; }

	private readonly AiLoadout _kit;
	private readonly SimRng _rng;
	private readonly AiView?[] _seen;
	private int _calls;
	private AiView? _prev;
	private Plan? _plan;
	private int _blockedInRow;
	private string? _oppLastSpecial;
	private readonly bool[] _rose = new bool[Enum.GetValues<Observation>().Length];

	public ProfileAi(BehaviourProfile profile, int self, AiLoadout kit, uint seed, int? reactionFrames = null)
	{
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
		if (prev is not null && prev.Tick != view.Tick) Events(prev, view, rose);

		// The habit (Y2) first. A wake-up habit is rolled when the AI sees itself go down and pre-holds its
		// direction through the knockdown, so it comes out on the first actionable frame (and is punishable).
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
				int wake = view.Tick + view.HitstopLeft + s.StunLeft + 1;
				_plan = Build(h.Do, view, tick, true, wakeUp ? Math.Max(0, wake - tick) : 0);
			}
		}

		// Event reactions: each weight is a percent rate per occurrence (anti-air rate, block habit...).
		if (_plan is not { Habit: true })
			foreach (var b in Profile.Behaviours)
			{
				if (!BehaviourProfile.IsEvent(b.When) || !rose[(int)b.When]) continue;
				int roll = _rng.Next(100);
				if (roll >= b.Weight) continue;
				var p = Build(b.Do, view, tick, false, 0);
				Record(tick, view, p is null ? "unavailable" : "react", b.When, b.Do.ToString(), roll, b.Weight);
				if (p is null) continue;
				_plan = p;
				break;
			}

		// Neutral: a weighted pick among standing behaviours that hold now, plus spacing toward the preferred range.
		if (_plan is null || _plan.Done) _plan = Neutral(view, s, o, tick);
		return _plan.Emit();
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
			w[i] = BehaviourProfile.IsEvent(b.When) || !holds ? 0 : Math.Max(1, b.Weight * (b.Do.Offensive ? 50 + Profile.AggressionBias : 150 - Profile.AggressionBias) / 100);
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
			_ => _kit.TryGet(a, out var input, out int frames) ? new[] { (input, 1), (none, Math.Max(1, frames - 1)) } : null,
		};
		if (steps is null) return null;
		if (preHold <= 0) return new Plan(habit, steps);
		// Hold the action's direction until the predicted first actionable tick, then do it.
		var all = new (FighterInput, int)[steps.Length + 1];
		all[0] = (new FighterInput(steps[0].Item1.Bits & InputBits.Directions), preHold);
		Array.Copy(steps, 0, all, 1, steps.Length);
		return new Plan(habit, all);
	}

	private void Events(AiView prev, AiView view, bool[] rose)
	{
		FighterView s = view.Fighter(Self), o = view.Fighter(1 - Self), ps = prev.Fighter(Self), po = prev.Fighter(1 - Self);
		void Edge(Observation ob, bool now, bool before) { if (now && !before) rose[(int)ob] = true; }

		Edge(Observation.OpponentJumping, o.Airborne, po.Airborne);
		Edge(Observation.OpponentKnockedDown, o.State == FighterState.Knockdown, po.State == FighterState.Knockdown);
		Edge(Observation.SelfKnockedDown, s.State == FighterState.Knockdown, ps.State == FighterState.Knockdown);
		Edge(Observation.SelfGotUp, ps.State == FighterState.Knockdown && s.State != FighterState.Knockdown, false);
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
		Log?.Add(new AiDecision(tick, view.Tick, kind, BehaviourProfile.ObservationName(when), action, roll, pct));

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
