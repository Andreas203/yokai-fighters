using System;
using System.Collections.Generic;
using System.Linq;
using YokaiFighters.Ai;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-176 (E14, E22, P6, F3): the opt-in AI guard (YOK-165) after its own move holds through the recovery and
/// <see cref="ProfileAi.GuardTicks"/> after it, counted in sim steps the fighters actually move: every screen at or after
/// the press that shows hitstop (one frozen step) adds one tick to the guard, if the AI sees it while still guarding.
/// Scripted-screen tests drive <see cref="ProfileAi.Next"/> with hand-made <see cref="AiView"/>s to place hitstop on exact
/// ticks; the fight tests use the fixture kits (as AiGuardTests).
/// </summary>
public static class AiGuardHitstopTests
{
	const string Light = "normal:test-ryo-light-punch", Heavy = "normal:test-ryo-heavy-punch";
	const int LightFrames = 13; // fixture light punch: 4 + 2 + 7

	static Match NewMatch(Func<SimConfig, SimConfig>? shape = null)
	{
		var saved = FightScene.Sources;
		FightScene.Sources = FightScene.FixtureSources;
		FightScene.Run = null;
		try { return FightScene.NewMatch(null, null, shape); }
		finally { FightScene.Sources = saved; }
	}

	/// <summary>Preferred mid, one close-range move: at close range neutral is that move or a walk back (standing, never
	/// crouching), so every crouch guard is a guard step of the move's plan.</summary>
	static BehaviourProfile Profile(string move) => BehaviourProfile.Parse($$"""
		{ "kind": "profile", "id": "test-guard-hitstop", "yokai": "kitsune", "temperament": "patient",
		  "reaction": { "tier": "apprentice", "frames": 30 }, "aggression_bias": 50, "preferred_range": "mid",
		  "habit": { "id": "jumps-after-knockdown", "description": "d", "when": "self_got_up", "do": "jump", "chance_pct": 60 },
		  "behaviours": [ { "when": "at_close_range", "do": "{{move}}", "weight": 100 } ], "rules": ["Y4"] }
		""");

	// --- Scripted screens -------------------------------------------------------------------------------

	static FighterView Idle(int x, int facing) =>
		new(x, 0, facing, FighterState.Idle, 0, false, false, 1000, 0, null, false, 0, 0, 0, false) { MaxHealth = 1000 };

	/// <summary>Both idle 150 units apart (close range), P2 (the AI) on the right: its guard is Right + Down.</summary>
	static AiView Screen(int tick, int hitstop) =>
		new(tick, MatchPhase.Fighting, hitstop, Idle(-75 * SimConfig.Scale, 1), Idle(75 * SimConfig.Scale, -1), Array.Empty<ProjectileView>())
		{ LightHitstop = 6, WakeUpFrames = SimConfig.Default.WakeUpFrames, BurstFrames = SimConfig.Default.BurstFrames };

	static readonly InputBits GuardBits = InputBits.Right | InputBits.Down;

	/// <summary>Feeds screens 0..ticks-1 (screen n shows <paramref name="hitstop"/>(n)); out[t] = the AI's input for step t.</summary>
	static Dictionary<int, FighterInput> Drive(int reaction, int guardPct, Func<int, int> hitstop, int ticks, List<AiDecision>? log = null)
	{
		var kit = AiLoadout.From(NewMatch().P2);
		var ai = new ProfileAi(Profile(Light), 1, kit, 7, reaction, guardPct: guardPct) { Log = log };
		var outs = new Dictionary<int, FighterInput>();
		for (int n = 0; n < ticks; n++) outs[n + 1] = ai.Next(Screen(n, hitstop(n)));
		return outs;
	}

	static int FirstPress(Dictionary<int, FighterInput> outs) => outs.Where(kv => kv.Value.Move != 0).Min(kv => kv.Key);

	/// <summary>Crouch-guard ticks straight after the first press.</summary>
	static int GuardRun(Dictionary<int, FighterInput> outs)
	{
		int p = FirstPress(outs), n = 0;
		while (outs.TryGetValue(p + 1 + n, out var o) && o.Move == 0 && o.Bits == GuardBits) n++;
		return n;
	}

	/// <summary>The scripted AI's first press tick P (the screens before it hold nothing it reacts to, so P does not
	/// depend on the hitstop placed after it) and its guard run with no hitstop: the light punch's 12 wait ticks + GuardTicks.</summary>
	static (int P, int Base) Baseline(int reaction)
	{
		var outs = Drive(reaction, 100, _ => 0, 200);
		return (FirstPress(outs), GuardRun(outs));
	}

	/// <summary>The guard run with hitstop on screens P+from .. P+to (inclusive), each relative to the first press P.</summary>
	static int RunWith(int reaction, params (int From, int To)[] spans)
	{
		var (p, _) = Baseline(reaction);
		var outs = Drive(reaction, 100, n => spans.Any(s => n >= p + s.From && n <= p + s.To) ? 9 : 0, 200);
		Assert.Equal(p, FirstPress(outs), "hitstop after the press does not move the press");
		return GuardRun(outs);
	}

	[Test]
	public static void NoHitstop_TailIsRecoveryPlusGuardTicks_Unchanged()
	{
		foreach (int r in new[] { 5, 15, 30 })
			Assert.Equal(LightFrames - 1 + ProfileAi.GuardTicks, Baseline(r).Base, $"reaction {r}: 12 wait + {ProfileAi.GuardTicks} tail, as before YOK-176");
	}

	/// <summary>Reaction 15: guard ticks P+1..P+22 see screens P-14..P+7. A light connect's 6 hitstop screens (P+3..P+8)
	/// are all seen while it still guards (the last one on the first added tick): the tail grows by exactly 6.</summary>
	[Test]
	public static void OwnConnectHitstop_LengthensTheGuardByItsLength()
	{
		Assert.Equal(22 + 6, RunWith(15, (3, 8)), "6 hitstop screens = 6 frozen steps = 6 more guard ticks");
		Assert.Equal(22 + 12, RunWith(15, (3, 14)), "a heavy's 12: each screen seen extends the guard far enough to see the next");
	}

	/// <summary>Counting starts at the press: hitstop shown before it (an earlier exchange) does not count; on the press tick's own screen it does.</summary>
	[Test]
	public static void HitstopBeforeThePress_NotCounted_AtThePress_Counted()
	{
		Assert.Equal(22, RunWith(15, (-6, -1)), "screens before the press: frozen steps before the move, the plan is unchanged");
		Assert.Equal(22 + 2, RunWith(15, (0, 1)), "screens P and P+1: steps P+1 and P+2 frozen");
	}

	/// <summary>The tail's start: with reaction 5 the tail's first tick (P+13) sees screen P+8. A freeze shown from there
	/// (6 screens P+8..P+13, i.e. the frozen steps include the tail's first world tick P+13) is counted whole.</summary>
	[Test]
	public static void HitstopAtTheTailsStart_Counted()
	{
		Assert.Equal(22 + 6, RunWith(5, (8, 13)), "reaction 5: a freeze at the tail's start lengthens it by its length");
		Assert.Equal(22 + 6, RunWith(5, (12, 17)), "reaction 5: a freeze whose first frozen step is the tail's first world tick (screen P+12)");
	}

	/// <summary>The tail's end: the last guard tick (P+22) sees screen P+22-R. Hitstop on that screen adds one tick, and a
	/// run of it keeps adding (each added tick sees the next screen). One screen later the guard is already over: P6, it
	/// cannot count a freeze it sees too late, and nothing is revived.</summary>
	[Test]
	public static void HitstopAtTheTailsEnd_LastTickCounts_OneLaterDoesNot()
	{
		foreach (int r in new[] { 5, 15 })
		{
			int last = 22 - r;
			Assert.Equal(22 + 1, RunWith(r, (last, last)), $"reaction {r}: one hitstop screen seen on the last guard tick");
			Assert.Equal(22 + 6, RunWith(r, (last, last + 5)), $"reaction {r}: a 6-screen freeze starting there is counted whole");
			Assert.Equal(22, RunWith(r, (last + 1, last + 6)), $"reaction {r}: seen after the guard ended: not counted, not revived");
		}
	}

	/// <summary>Repeated hitstop (a two-hit exchange): every hitstop screen counts once, and a later one counts because the
	/// earlier extension kept the guard running long enough to see it.</summary>
	[Test]
	public static void RepeatedHitstop_EachScreenCountsOnce()
	{
		Assert.Equal(22 + 3 + 2, RunWith(15, (2, 4), (6, 7)), "two freezes inside the seen window: 3 + 2");
		Assert.Equal(22 + 3 + 2 + 3, RunWith(15, (2, 4), (6, 7), (10, 12)), "a third one only visible thanks to the first two: + 3");
	}

	/// <summary>No extra draws, and guard 0 is untouched: with guard 0 the AI's inputs are identical with or without hitstop
	/// on screen; with guard 100 the decisions and their rolls come in the same order, only later.</summary>
	[Test]
	public static void NoExtraDraws_Guard0Unchanged()
	{
		Func<int, int> busy = n => n % 17 < 5 ? 6 : 0; // hitstop all through the fight
		var a = Drive(15, 0, _ => 0, 600);
		var b = Drive(15, 0, busy, 600);
		Assert.True(a.Keys.All(k => a[k] == b[k]), "guard 0: identical input every tick (no plan has guard steps)");
		var la = new List<AiDecision>(); var lb = new List<AiDecision>();
		Drive(15, 100, _ => 0, 600, la);
		Drive(15, 100, busy, 600, lb);
		int k2 = Math.Min(la.Count, lb.Count);
		Assert.True(k2 >= 10 && lb.Count < la.Count, $"guard 100: longer guards, fewer decisions ({lb.Count} < {la.Count})");
		for (int i = 0; i < k2; i++)
			Assert.True(la[i].Kind == lb[i].Kind && la[i].Roll == lb[i].Roll && la[i].Action == lb[i].Action, $"decision {i}: same kind and roll ({la[i]} vs {lb[i]})");
	}

	// --- In the fight ----------------------------------------------------------------------------------

	/// <summary>
	/// The ticket's acceptance: the AI's guarded move is blocked (P1 crouch-guards at touching range, kept there), and from
	/// its first free step the AI holds crouch guard for exactly <see cref="ProfileAi.GuardTicks"/> steps the fighters move
	/// in (frozen steps are skipped: the world does not move in them). Returns (moves, blocked connects).
	/// </summary>
	static (int Moves, int Blocked) PlayBlocked(string move, int reaction, Func<SimConfig, SimConfig>? shape = null) =>
		Play(Profile(move), reaction, shape, _ => new FighterInput(InputBits.Left | InputBits.Down), e => e.Blocked);

	/// <summary>Plays P1 (kept touching P2) against the guard-100 AI and checks every tail; returns (moves, counted connects).</summary>
	static (int Moves, int Connects) Play(BehaviourProfile prof, int reaction, Func<SimConfig, SimConfig>? shape,
		Func<Match, FighterInput> p1, Func<HitEvent, bool> counts)
	{
		string move = prof.Behaviours[0].Do.ToString();
		var m = NewMatch(c => (shape ?? (x => x))(c) with { P1MaxHealth = 1_000_000, P2MaxHealth = 1_000_000 });
		int gap = m.Config.BodyWidth;
		m.P1.X = m.P2.X - gap;
		var ai = new ProfileAi(prof, 1, AiLoadout.From(m.P2), 3, reaction, guardPct: 100);
		int blocked = 0;
		m.Hit += (_, e) => { if (e.Attacker == 1 && counts(e)) blocked++; };
		var frozen = new List<bool>();
		var attack = new List<bool>();
		var guard = new List<bool>();
		for (int t = 1; t <= 1500 && m.Phase == MatchPhase.Fighting; t++)
		{
			m.P1.X = m.P2.X - gap; // stays touching as the AI walks back and blocks push apart
			var o = ai.Next(AiView.Capture(m));
			frozen.Add(m.HitstopLeft > 0);
			m.Step(p1(m), o);
			attack.Add(m.P2.State == FighterState.Attack);
			guard.Add(m.P2.Guarding && m.P2.Crouching);
		}
		int moves = 0;
		for (int i = 1; i < attack.Count; i++)
		{
			if (!attack[i - 1] || attack[i]) continue; // i = the first step after its move
			int free = 0, j = i;
			for (; j < attack.Count; j++)
			{
				if (frozen[j]) continue;
				if (!guard[j]) break;
				free++;
			}
			if (j == attack.Count) break; // the fight ran out mid-tail
			moves++;
			Assert.Equal(ProfileAi.GuardTicks, free, $"{move}, reaction {reaction}: move ending at step {i + 1}: crouch guard from its first free step for GuardTicks moving steps");
		}
		return (moves, blocked);
	}

	/// <summary>A blocked heavy punch (hitstop 12, 24 frames from its first active frame to its end) at every reaction tier
	/// (E22: apprentice 30, yokai 22, elders 18, Tanuki 15): the AI sees the hitstop while still guarding.</summary>
	[Test]
	public static void BlockedHeavy_GuardsFromFirstFreeStepThroughGuardTicks_AllTiers()
	{
		foreach (int r in new[] { 15, 18, 22, 30 })
		{
			var (moves, blocked) = PlayBlocked(Heavy, r);
			Assert.True(moves >= 10 && blocked >= 10, $"reaction {r}: the AI swung and was blocked ({moves} moves, {blocked} blocked)");
		}
	}

	/// <summary>A blocked light punch: its hitstop shows on the AI's screen within the guard for reaction up to 18 (the
	/// move's active + recovery 9 + GuardTicks - 1). Slower reactions see it only after the guard ended (P6; see the report).</summary>
	[Test]
	public static void BlockedLight_GuardsThroughGuardTicks_Tanuki_And_Elder()
	{
		foreach (int r in new[] { 15, 18 })
		{
			var (moves, blocked) = PlayBlocked(Light, r);
			Assert.True(moves >= 10 && blocked >= 10, $"reaction {r}: the AI swung and was blocked ({moves} moves, {blocked} blocked)");
		}
	}

	/// <summary>
	/// The held jump answer (YOK-165): P1 jumps the step after each AI heavy punch starts, so the AI (Tanuki tier 15, tier
	/// rate 100) sees the jump while busy and holds its medium-punch answer for its first actionable tick. The answer's end
	/// is timed from the screen that shows it (AnswerEnd, hitstop on that screen included); its connect on the jumper shows
	/// later and is counted from there, once: every move still ends in GuardTicks moving steps of crouch guard.
	/// </summary>
	[Test]
	public static void HeldJumpAnswer_ConnectHitstop_CountedOnce()
	{
		var prof = BehaviourProfile.Parse($$"""
			{ "kind": "profile", "id": "test-guard-hitstop-aa", "yokai": "kitsune", "temperament": "patient",
			  "reaction": { "tier": "tanuki", "frames": 15 }, "aggression_bias": 50, "preferred_range": "mid",
			  "habit": { "id": "jumps-after-knockdown", "description": "d", "when": "self_got_up", "do": "jump", "chance_pct": 60 },
			  "behaviours": [ { "when": "at_close_range", "do": "{{Heavy}}", "weight": 100 },
			                  { "when": "opponent_jumping", "do": "normal:test-ryo-medium-punch", "weight": 100 } ], "rules": ["Y4"] }
			""");
		var (moves, answers) = Play(prof, 15, c => c with { P2JumpPunishPct = 100 },
			m => m.P2.State == FighterState.Attack && m.P2.MoveFrame == 1 && m.P2.CurrentMove?.Id == "test-ryo-heavy-punch" && !m.P1.Airborne
				? new FighterInput(InputBits.Up) : new FighterInput(InputBits.Left | InputBits.Down),
			e => e.MoveId == "test-ryo-medium-punch");
		Assert.True(moves >= 10 && answers >= 5, $"the AI swung, and its held answer hit the jumper ({moves} moves, {answers} answers connected)");
	}

	/// <summary>Hitstop off: nothing freezes, and the tail is GuardTicks as it was.</summary>
	[Test]
	public static void HitstopOff_TailUnchanged()
	{
		var (moves, blocked) = PlayBlocked(Heavy, 22, c => c with { HitstopEnabled = false });
		Assert.True(moves >= 10 && blocked >= 10, $"hitstop off: the AI swung and was blocked ({moves}, {blocked})");
	}
}
