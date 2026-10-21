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
	/// <summary>YOK-165: the shortest hitstun in the move data (11); AiGuardTests checks it against data/moves and the fixtures.</summary>
	public const int ShortestHitstun = 11;
	/// <summary>
	/// YOK-177 (Y2): a habit whose trigger is a standing condition (<c>at_close_range</c>, <c>opponent_blocking</c>...) is
	/// rolled when the condition becomes true on the AI's view, and again every this many view ticks while it keeps
	/// holding; a re-entry within this many ticks of the last roll does not roll (proposed: 60 = 1 s, longer than any
	/// move in the kits, so each roll is played out before the next, and a fighter walking on a range edge cannot
	/// make it roll every few ticks). Entry-only would leave a player who stays in range never seeing the tell again
	/// after one miss; a roll every tick would turn 60% into ~100%.
	/// </summary>
	public const int StandingHabitRearmTicks = 60;

	/// <summary>
	/// YOK-165: the fewest ticks a hit keeps a fighter from acting in this fight: the light hitstop the screen's fight
	/// runs with (<see cref="AiView.LightHitstop"/>: 6, or 0 with hitstop off) + <see cref="ShortestHitstun"/>. A held
	/// anti-air's repeated press never runs longer than this when the AI reacts slower than it (see JumpPunish).
	/// </summary>
	public static int MinInterruptTicks(AiView view) => view.LightHitstop + ShortestHitstun;

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
	/// <summary>
	/// YOK-177: a standing-condition habit trigger fires (<see cref="StandingHabitRearmTicks"/>). False = the rule before
	/// YOK-177, when such a habit never rolled; only replays of recordings made before it pass false (<c>AiSetup.StandingHabits</c>).
	/// </summary>
	public bool StandingHabits { get; }
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
	/// <summary>YOK-165: the tick the AI last pressed a move of its own and that move's length (its own plan, not the screen).</summary>
	private int _ownPressTick = -1, _ownPressFrames;
	/// <summary>YOK-177: the view tick of the last standing-trigger habit roll (-1 = none yet).</summary>
	private int _standingRollTick = -1;
	/// <summary>YOK-177: a fired standing habit a burst interrupted; it is resumed when the burst plan ends.</summary>
	private Plan? _resume;
	private readonly bool[] _rose = new bool[Enum.GetValues<Observation>().Length];

	public ProfileAi(BehaviourProfile profile, int self, AiLoadout kit, uint seed, int? reactionFrames = null,
		AiFightTier tier = AiFightTier.Standard, int aggressionBonus = 0, int? guardPct = null, bool standingHabits = true)
	{
		StandingHabits = standingHabits;
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

		if (view.Phase != MatchPhase.Fighting) { _plan = null; _resume = null; return FighterInput.None; }

		FighterView s = view.Fighter(Self), o = view.Fighter(1 - Self);
		bool[] rose = _rose;
		Array.Clear(rose);
		bool fresh = prev is not null && prev.Tick != view.Tick;
		if (fresh)
		{
			Events(prev, view, rose);
			// YOK-57 (C6, E13): a burst out of a combo the AI sees itself in comes before everything else.
			if (Burst(prev, view, s, tick) is { } burst)
			{
				if (_plan is { StandingHabit: true, Done: false } fired) _resume = fired; // YOK-177: the fired habit comes out after the burst
				_plan = burst;
				_ownPressTick = tick; _ownPressFrames = Math.Max(1, view.BurstFrames); // YOK-165: busy until press + BurstFrames
				return _plan.Emit();
			}
		}

		// The habit (Y2) first. A wake-up habit is rolled when the AI sees itself go down and pre-holds its
		// direction through the knockdown and the wake-up, so it comes out on the first actionable frame (and is punishable).
		var h = Profile.Habit;
		bool wakeUp = h.When == Observation.SelfGotUp, standing = !BehaviourProfile.IsEvent(h.When);
		if (standing ? fresh && StandingHabitDue(view, s, o) : rose[(int)(wakeUp ? Observation.SelfKnockedDown : h.When)])
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
				// YOK-177 (designer 2026-10-21: a fired habit always comes out): a standing-trigger habit is a held press like
				// YOK-165's anti-air. Its wait is open-ended and released only when the latest screen predicts the AI free
				// (re-checked every tick, so a re-hit or a longer stun keeps it waiting); see HabitPress for the press itself.
				_plan = Build(h.Do, view, tick, true, standing ? int.MaxValue : wakeUp ? Math.Max(0, wake - tick) : 0, heldThrow: standing);
				if (standing && _plan is not null)
				{
					_plan.StandingHabit = true;
					_plan.PressStep = 1; // Build puts the wait in step 0, the press in step 1
					_plan.AnswerMoveId = h.Do.Kind == ActionKind.Throw ? _kit.ThrowMoveId : h.Do.MoveId; // code-critic r3: a substituted normal is not the throw
					_plan.HoldPress(PressWindow(_plan, view));
				}
			}
		}

		// Event reactions: each weight is a percent rate per occurrence (anti-air rate, block habit...).
		// YOK-165, Y4: with a tier jump-punish rate, the jump's attacking answers are one roll at that rate (JumpPunish),
		// taken where the first opponent_jumping behaviour sits in the list; its other answers roll by weight after a miss.
		// YOK-177 (Y4): a standing-trigger habit plan (the Tanuki's throw at a blocker) does not stop a seen jump being
		// answered: only the opponent_jumping answers roll then, and a hit replaces the habit. An event habit still holds
		// everything off, so those profiles draw as before.
		bool jumpOverHabit = _plan is { Habit: true, StandingHabit: true, Done: false } && rose[(int)Observation.OpponentJumping];
		if (_plan is not { Habit: true } || jumpOverHabit)
		{
			int? jumpRate = rose[(int)Observation.OpponentJumping] ? view.JumpPunishPct(Self) : null;
			bool jumpRolled = false;
			foreach (var b in Profile.Behaviours)
			{
				if (!BehaviourProfile.IsEvent(b.When) || !rose[(int)b.When]) continue;
				if (jumpOverHabit && b.When != Observation.OpponentJumping) continue;
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
		// YOK-165: an anti-air held for the first actionable tick is dropped (no draw) if the jumper shows landed by then.
		if (_plan is { InHeldWait: true } held && FirstActionableTick(view) <= tick) held.ReleaseWait(tick);
		if (_plan is { InPress: true, StandingHabit: true } hp) { if (tick > hp.ReleaseTick) HabitPress(hp, view, s, prev, tick); } // YOK-177
		else if (_plan is { AtPress: true } && !o.Airborne) _plan = null; // nothing pressed yet: drop the whole answer
		else if (_plan is { InPress: true } rp && tick > rp.ReleaseTick)
		{
			// It may have started: stop pressing (no draw) once the screen shows the answer out or a new hit/block on the AI,
			// the jumper landed, or the window is spent; the rest of the plan is timed from the answer's start.
			bool shown = view.Tick >= rp.ReleaseTick && AnswerShown(rp, s, prev);
			if (shown || !o.Airborne || tick - rp.ReleaseTick >= rp.Window) rp.EndPress(AnswerEnd(rp, view, s, shown) - tick);
		}
		if ((_plan is null || _plan.Done) && _resume is { } back) { _plan = back; _resume = null; if (back.InPress) back.Paused = true; } // YOK-177: HabitPress decides
		if (_plan is null || _plan.Done) _plan = Neutral(view, s, o, tick);
		if (_plan.AtMovePress) { _ownPressTick = tick; _ownPressFrames = _plan.MoveFrames; } // YOK-165: its own commitment, not a read
		return _plan.Emit(Dirs(s, o).back); // a guard step holds back as the AI's own (delayed) view shows it now
	}

	/// <summary>
	/// YOK-165, Y4: the fight's tier rate (<see cref="AiView.JumpPunishPct"/>, node-scenes <c>jump_punish_pct</c>) is the
	/// absolute chance the AI answers a jump it sees with an attack (designer 2026-10-20: elders and the Tanuki 100,
	/// apprentice and yokai 50). One draw at that rate; on a hit, a weighted pick among the profile's attacking answers
	/// to <c>opponent_jumping</c> that this kit can perform (a second draw only when there are several). Null, so the
	/// other answers (block...) roll by weight, on a miss, or when no attacking answer is performable (no draw at all).
	/// </summary>
	private Plan? JumpPunish(int rate, AiView view, int tick)
	{
		int total = 0, count = 0;
		Behaviour? pick = null;
		foreach (var b in Profile.Behaviours)
			if (CanPunishWith(b)) { total += b.Weight; count++; pick ??= b; }
		if (pick is null) return null;
		int roll = _rng.Next(100);
		if (roll >= rate) return null;
		if (count > 1)
		{
			int w = _rng.Next(total);
			foreach (var b in Profile.Behaviours)
			{
				if (!CanPunishWith(b)) continue;
				if (w < b.Weight) { pick = b; break; }
				w -= b.Weight;
			}
		}
		// Test-breaker YOK-165: a press while the AI is still in its own move is dropped by the sim, so (like the wake-up habit)
		// the answer waits for its first actionable tick, predicted from its own state on screen. No draw is added: Build's
		// EX and guard rolls happen now, as before; the plan is dropped at the press if the jumper shows landed (Next).
		// The wait is open-ended: Next releases it once the latest screen predicts the AI free (hitstop from a later hit,
		// e.g. its own projectile's second hit, only shows on a later screen).
		bool wait = FirstActionableTick(view) > tick;
		var p = Build(pick.Do, view, tick, false, wait ? int.MaxValue : 0, heldThrow: StandingHabits); // YOK-177: a throw answer is a slot request (old recordings: the raw buttons). This changes event-habit profiles with a throw anti-air (the Kappa) in tier-rate fights: designer-approved 2026-10-21
		if (p is not null && wait)
		{
			p.PressStep = 1; // Build puts the wait in step 0, the press in step 1
			p.AnswerMoveId = pick.Do.Kind == ActionKind.Throw ? (StandingHabits ? _kit.ThrowMoveId : null) : pick.Do.MoveId;
			// A hit the AI cannot see yet (its own projectile's next hit) can add hitstop after the screen it predicted from,
			// so the press is repeated for up to the answer's length minus one: the sim takes it on the first actionable tick
			// and ignores the rest while the move plays (a slot request needs an actionable fighter; no cancels into one).
			// Next ends the press, with no draw, once the screen at or after the release shows the answer out or a new hit or
			// block on the AI, the jumper landed, or the window is spent, and times the rest of the plan (wait, guard tail)
			// from the answer's start. Before the first press, a landed jumper drops the whole plan (nothing has started).
			// An AI slower than MinInterruptTicks cannot see a hit on its answer before it could act again, so its window
			// is capped there: an interrupted answer is never pressed a second time from one roll.
			p.HoldPress(PressWindow(p, view));
		}
		Record(tick, view, p is null ? "unavailable" : "react", Observation.OpponentJumping, pick.Do.ToString(), roll, rate);
		return p;
	}

	/// <summary>YOK-165: how long a held press is repeated: the move's length minus one, capped at
	/// <see cref="MinInterruptTicks"/> for an AI slower than that (an interrupted move is never pressed twice from one press).</summary>
	private int PressWindow(Plan p, AiView view)
	{
		int window = Math.Max(1, p.MoveFrames - 1);
		int minInterrupt = MinInterruptTicks(view);
		return ReactionFrames > minInterrupt ? Math.Min(window, minInterrupt) : window;
	}

	/// <summary>
	/// YOK-177 (designer 2026-10-21: a fired habit always comes out): the held habit press, each tick after its release.
	/// Once the screen shows its move started at or after the release, the press ends and the rest is timed from it.
	/// The press stops (no more repeats) when its window is spent or the screen shows a new hit or block on the AI; then,
	/// once the screen has covered the whole window with no start (the press landed while the AI was busy, unseen), the
	/// plan goes back to its open-ended wait and presses again when the latest screen predicts the AI free. No draw.
	/// Code-critic r3: a throw habit (and, under the YOK-177 rule, a throw jump answer) is pressed as the throw's slot
	/// request (<see cref="AiLoadout.ThrowRequest"/>), so a press while busy buffers no normal that could come out in its
	/// place, and its start is checked by the throw's own move id, so another move on screen is never taken for it.
	/// A burst that interrupts the plan resumes it afterwards (<c>_resume</c>).
	/// </summary>
	private void HabitPress(Plan hp, AiView view, FighterView s, AiView? prev, int tick)
	{
		bool started = view.Tick >= hp.ReleaseTick && s.State == FighterState.Attack
			&& view.Tick - s.MoveFrame + 1 >= hp.ReleaseTick && (hp.AnswerMoveId is null || s.MoveId == hp.AnswerMoveId);
		if (started) { hp.EndPress(AnswerEnd(hp, view, s, true) - tick); return; }
		if (tick - hp.ReleaseTick >= hp.Window || (view.Tick >= hp.ReleaseTick && AnswerShown(hp, s, prev))) hp.Paused = true;
		if (hp.Paused && view.Tick >= hp.ReleaseTick + hp.Window) hp.Rewait();
	}

	/// <summary>
	/// YOK-165: the first tick the AI can act, predicted from its own fighter on the (delayed) screen, the way the wake-up
	/// habit predicts its wake tick: its own move's remaining frames (total from its kit), or the stun left (+ the get-up
	/// after a knockdown). Hitstop holds every count. States with no visible count (dash, jump, thrown) predict nothing
	/// (0 = act now, as before).
	/// </summary>
	private int FirstActionableTick(AiView view)
	{
		FighterView s = view.Fighter(Self);
		int left = s.State switch
		{
			FighterState.Attack when s.MoveId is not null && _kit.TryGetTotalFrames(s.MoveId, s.MoveIsEx, out int total) => Math.Max(0, total - s.MoveFrame),
			FighterState.Hitstun or FighterState.Blockstun or FighterState.Burst or FighterState.Landing or FighterState.WakeUp => s.StunLeft,
			FighterState.Knockdown => s.StunLeft + view.WakeUpFrames,
			_ => -1,
		};
		int seen = left < 0 ? 0 : view.Tick + view.HitstopLeft + left + 1;
		// Its own press made after the screen it sees has not shown yet (the view is reaction frames old): the AI knows its
		// own commitment, so it waits for that move too. Once the view reaches the press, the screen decides alone.
		return _ownPressTick > view.Tick ? Math.Max(seen, _ownPressTick + _ownPressFrames) : seen;
	}

	/// <summary>
	/// YOK-165: the screen (at or after the held press's release) shows the answer came out, or the AI newly hit or
	/// blocking: its own answer on screen, any attack once the screen reaches the press when the id is unknown (a throw),
	/// or a hitstun / blockstun that starts or is refreshed on this screen. Its earlier move still playing (hitstop the
	/// prediction could not see) is not the answer, so the repeats go on.
	/// </summary>
	private int AnswerEnd(Plan rp, AiView view, FighterView s, bool shown)
	{
		if (!shown) return rp.ReleaseTick + rp.MoveFrames; // not on screen yet: as if it came out on the release tick
		if (s.State != FighterState.Attack) return 0;      // hit or blocking: the answer is over, go on to the rest now
		// On screen: its start is view.Tick - MoveFrame + 1; the end below also counts the hitstop the screen shows.
		return view.Tick + view.HitstopLeft + Math.Max(0, rp.MoveFrames - s.MoveFrame) + 1;
	}

	private bool AnswerShown(Plan rp, FighterView s, AiView? prev)
	{
		if (s.State == FighterState.Attack) return rp.AnswerMoveId is null ? s.MoveFrame <= s.MoveLastActive : s.MoveId == rp.AnswerMoveId;
		if (s.State is not (FighterState.Hitstun or FighterState.Blockstun or FighterState.Thrown)) return false;
		var ps = prev?.Fighter(Self);
		return ps is not { } p || p.State != s.State || s.StunLeft > p.StunLeft;
	}

	/// <summary>YOK-165: an attacking answer to a jump that this kit can perform (side-effect free: no draw, no EX roll).</summary>
	private bool CanPunishWith(Behaviour b) => b.When == Observation.OpponentJumping && b.Do.IsMove && _kit.CanDo(b.Do);

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
			bool holds = Holds(b.When, range, o);
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
		if (diff == 0 && RollGuard(tick, view, "self_waiting"))
			return new Plan(false, (new FighterInput(back | InputBits.Down), SpacingTicks)) { Guard = new[] { true } };
		return new Plan(false, (new FighterInput(bits), SpacingTicks));
	}

	/// <summary>A standing observation holds on this screen (neutral always; a range band; the opponent in blockstun).</summary>
	private static bool Holds(Observation when, Observation range, FighterView o) =>
		when == Observation.Neutral || when == range || (when == Observation.OpponentBlocking && o.State == FighterState.Blockstun);

	/// <summary>
	/// YOK-177 (Y2, P6): the standing-condition habit is due on this (fresh) view: its condition holds on screen, no habit
	/// plan is still playing, and the last roll was at least <see cref="StandingHabitRearmTicks"/> view ticks ago (or there
	/// was none). So the first roll comes the moment the condition becomes true, then one per re-arm period while it holds.
	/// </summary>
	private bool StandingHabitDue(AiView view, FighterView s, FighterView o)
	{
		if (!StandingHabits || !Holds(Profile.Habit.When, RangeOf(s, o), o) || HabitPlanRunning || _resume is not null) return false;
		if (_plan is { PressStep: >= 0, Done: false }) return false; // a held jump answer (YOK-165) is not overwritten
		// A clock that went back (the match was reset under this AI) re-arms it.
		if (_standingRollTick >= 0 && view.Tick >= _standingRollTick && view.Tick - _standingRollTick < StandingHabitRearmTicks) return false;
		_standingRollTick = view.Tick;
		return true;
	}

	private Plan? Build(AiAction a, AiView view, int tick, bool habit, int preHold, bool heldThrow = false)
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
			ActionKind.Throw => _kit.ThrowButtons == InputBits.None ? null : new[] { (heldThrow ? _kit.ThrowRequest : new FighterInput(_kit.ThrowButtons), 1), (none, _kit.ThrowFrames) },
			_ => _kit.TryGet(a, out var input, out int frames) ? MoveSteps(a, input, frames, view, tick) : null,
		};
		if (steps is null) return null;
		// YOK-165: after a move of its own (whiffed or not) it may hold crouch guard through the recovery, so it is guarding
		// on its first free frame, and a beat after. Not after the habit: that stays as punishable as the profile says (Y2).
		bool[]? guardSteps = null;
		if (!habit && a.IsMove && RollGuard(tick, view, "self_attacked"))
		{
			var guard = new FighterInput(back | InputBits.Down);
			var tail = new (FighterInput, int)[steps.Length + 1];
			Array.Copy(steps, tail, steps.Length);
			guardSteps = new bool[tail.Length];
			for (int i = 1; i < tail.Length; i++) // the wait steps and the tail; the press stays as built
			{
				tail[i] = (guard, i < steps.Length ? steps[i].Item2 : GuardTicks);
				guardSteps[i] = true;
			}
			steps = tail;
		}
		// YOK-165: a move's press step and length, so the AI knows when its own press makes it busy until (FirstActionableTick).
		int moveFrames = !a.IsMove ? 0 : a.Kind == ActionKind.Throw ? _kit.ThrowFrames : 1 + steps[1].Item2;
		if (preHold <= 0) return new Plan(habit, steps) { Guard = guardSteps, MoveStep = a.IsMove ? 0 : -1, MoveFrames = moveFrames };
		// Hold the action's direction until the predicted first actionable tick, then do it.
		var all = new (FighterInput, int)[steps.Length + 1];
		all[0] = (new FighterInput(steps[0].Item1.Bits & InputBits.Directions), preHold);
		Array.Copy(steps, 0, all, 1, steps.Length);
		bool[]? guardAll = null;
		if (guardSteps is not null) { guardAll = new bool[all.Length]; Array.Copy(guardSteps, 0, guardAll, 1, guardSteps.Length); }
		return new Plan(habit, all) { Guard = guardAll, MoveStep = a.IsMove ? 1 : -1, MoveFrames = moveFrames };
	}

	/// <summary>A normal or special: the press, then wait out the move. YOK-165: when <see cref="MaybeEx"/> asks for the EX
	/// version (same draw as before), the wait is the EX move's own length, so the AI knows how long its own press keeps it busy.</summary>
	private (FighterInput, int)[] MoveSteps(AiAction a, FighterInput plain, int frames, AiView view, int tick)
	{
		var press = MaybeEx(a, plain, view, tick);
		if (press != plain && _kit.TryGetExFrames(a, out int exFrames)) frames = exFrames;
		return new[] { (press, 1), (FighterInput.None, Math.Max(1, frames - 1)) };
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
		/// <summary>YOK-177: a habit plan from a standing trigger; a seen jump may still be answered over it.</summary>
		public bool StandingHabit { get; set; }
		private readonly (FighterInput In, int Ticks)[] _steps;
		private int _i, _t;
		public Plan(bool habit, params (FighterInput, int)[] steps) { Habit = habit; _steps = steps; }
		/// <summary>YOK-165: the steps that are the pre-emptive guard. Their "back" is re-read every tick in <see cref="Emit"/>,
		/// so a cross-up the AI sees mid-plan does not leave it holding down-forward.</summary>
		public bool[]? Guard { get; init; }
		public bool Done => _i >= _steps.Length;
		/// <summary>YOK-165: the step that presses a held anti-air (-1 = none); <see cref="AtPress"/> = it emits on this tick.</summary>
		public int PressStep { get; set; } = -1;
		public bool AtPress => PressStep >= 0 && _i == PressStep && _t == 0;
		/// <summary>YOK-165: still in the open-ended wait before a held anti-air's press.</summary>
		public bool InHeldWait => PressStep > 0 && _i < PressStep;
		public void ReleaseWait(int tick) { if (InHeldWait) { _i = PressStep; _t = 0; ReleaseTick = tick; } }
		/// <summary>YOK-165: the held answer's move id as the screen shows it (null for a throw).</summary>
		public string? AnswerMoveId { get; set; }
		/// <summary>YOK-165: the tick the held wait was released (the first press).</summary>
		public int ReleaseTick { get; private set; } = -1;
		/// <summary>YOK-165: a held anti-air's press is sent every tick from the release until <see cref="EndPress"/>, which
		/// Next calls at the latest <paramref name="window"/> ticks after the release (see JumpPunish).</summary>
		public void HoldPress(int window) { Window = window; _repeat = int.MaxValue; _steps[PressStep] = (_steps[PressStep].In, int.MaxValue); }
		public int Window { get; private set; }
		/// <summary>YOK-165: in the press step of a released held anti-air.</summary>
		public bool InPress => PressStep >= 0 && _i == PressStep && ReleaseTick >= 0;
		/// <summary>YOK-165: stop pressing now and wait <paramref name="remaining"/> more ticks (the answer's predicted end,
		/// timed from its start) before the rest of the plan (the guard tail, if any). 0 = go straight to the rest.</summary>
		/// <summary>YOK-177: a held habit press that stopped repeating while the screen catches up (emits nothing).</summary>
		public bool Paused { get; set; }
		/// <summary>YOK-177: back to the open-ended wait before the press (the press was never seen to start).</summary>
		public void Rewait() { _i = 0; _t = 0; ReleaseTick = -1; Paused = false; }
		public void EndPress(int remaining)
		{
			Paused = false;
			if (!InPress) return;
			_i = PressStep + 1; _t = 0;
			if (_i >= _steps.Length) return;
			if (remaining > 0) _steps[_i] = (_steps[_i].In, remaining);
			else _i++;
		}
		private int _repeat = 1;
		/// <summary>YOK-165: the step that presses a move of the AI's own (-1 = none) and the move's length in ticks.</summary>
		public int MoveStep { get; init; } = -1;
		public int MoveFrames { get; init; }
		public bool AtMovePress => MoveStep >= 0 && _i == MoveStep && _t == 0;
		public FighterInput Emit(InputBits back = InputBits.None)
		{
			if (Done) return FighterInput.None;
			if (Paused && _i == PressStep) return FighterInput.None;
			var (input, ticks) = _steps[_i];
			if (back != InputBits.None && Guard is { } g && g[_i]) input = new FighterInput(back | InputBits.Down);
			var result = _t == 0 || (_i == PressStep && _t < _repeat) ? input : new FighterInput(input.Bits);
			if (++_t >= ticks) { _i++; _t = 0; }
			return result;
		}
	}
}
