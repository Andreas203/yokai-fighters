using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using YokaiFighters.Sim;

namespace YokaiFighters.Ai;

/// <summary>Scripted P1 for headless AI runs (reads the sim directly: it is the test rig, not the AI).</summary>
public enum P1Script { Idle, Random, Jumper, Thrower }

/// <summary>
/// Headless AI-vs-script runner (YOK-27). Plays P2 = <see cref="ProfileAi"/> against a scripted P1 for a
/// number of ticks (new round after each KO, AI rebuilt with the next seed), logs every AI decision, and
/// measures what the acceptance asks for: response ticks from a P1 jump to the AI's anti-air move start,
/// and how often the AI jumps on the first frame after a knockdown (the Kitsune tell).
/// </summary>
public static class AiHarness
{
	public const int JumpEvery = 90;
	/// <summary>Thrower presses LP+LK inside this distance (gameplay units).</summary>
	public const int ThrowReach = 140, AntiAirReach = 170; // fixture throwbox reaches 110 + half a 90 hurtbox

	public sealed class Result
	{
		public required string Profile { get; init; }
		public required int ReactionFrames { get; init; }
		public required uint Seed { get; init; }
		public required P1Script Script { get; init; }
		public int Ticks, Rounds, AiWins, P1Wins;
		public readonly List<AiDecision> Decisions = new();
		/// <summary>Ticks from a P1 jump (first airborne tick) to the AI starting a move, per jump answered.</summary>
		public readonly List<int> AntiAirResponse = new();
		public int P1Jumps, Knockdowns, WakeUps, WakeJumps, HabitFired, HabitCovered, HabitNo;
		/// <summary>P1 hits landed on the AI while it was airborne in a wake-up jump (the tell punished).</summary>
		public int WakeJumpsPunished;
		public readonly List<string> Trace = new();
		public ulong FinalHash;

		public double MeanResponse => AntiAirResponse.Count == 0 ? -1 : Average(AntiAirResponse);
		private static double Average(List<int> v) { long s = 0; foreach (int x in v) s += x; return (double)s / v.Count; }

		public string DecisionsCsv()
		{
			var sb = new StringBuilder("tick,view_tick,kind,trigger,action,roll,pct\n");
			foreach (var d in Decisions) sb.Append(CultureInfo.InvariantCulture, $"{d.Tick},{d.ViewTick},{d.Kind},{d.Trigger},{d.Action},{d.Roll},{d.Pct}\n");
			return sb.ToString();
		}

		public string SummaryJson()
		{
			var inv = CultureInfo.InvariantCulture;
			return string.Create(inv, $$"""
				{"profile":"{{Profile}}","reaction_frames":{{ReactionFrames}},"seed":{{Seed}},"p1":"{{Script.ToString().ToLowerInvariant()}}","ticks":{{Ticks}},"rounds":{{Rounds}},"ai_wins":{{AiWins}},"p1_wins":{{P1Wins}},"p1_jumps":{{P1Jumps}},"anti_air_answered":{{AntiAirResponse.Count}},"anti_air_mean_response":{{MeanResponse:0.00}},"anti_air_min_response":{{(AntiAirResponse.Count == 0 ? -1 : AntiAirResponse.Min())}},"knockdowns":{{Knockdowns}},"wake_ups":{{WakeUps}},"wake_jumps":{{WakeJumps}},"habit_fired":{{HabitFired}},"habit_covered":{{HabitCovered}},"habit_no":{{HabitNo}},"wake_jumps_punished":{{WakeJumpsPunished}},"decisions":{{Decisions.Count}},"final_hash":"{{FinalHash:x16}}"}
				""");
		}
	}

	private static int Min(this List<int> v) { int m = int.MaxValue; foreach (int x in v) m = Math.Min(m, x); return m; }

	public static Result Run(Func<Match> newMatch, BehaviourProfile profile, P1Script script, uint seed, int ticks,
		int? reactionFrames = null, bool trace = false)
	{
		var match = newMatch();
		ProfileAi NewAi(int round) => new(profile, 1, AiLoadout.From(match.P2), seed + (uint)round * 7919u, reactionFrames);
		var ai = NewAi(0);
		var r = new Result { Profile = profile.Id, ReactionFrames = ai.ReactionFrames, Seed = seed, Script = script };
		ai.Log = r.Decisions;
		var p1Rng = new SimRng(seed ^ 0x5EED1234u);
		InputBits held = InputBits.None;
		int holdLeft = 0, pendingJump = -1;
		FighterState aiPrev = match.P2.State;
		bool p1WasAir = false, inWakeJump = false;
		void OnHit(Match m, HitEvent e) { if (e.Attacker == 0 && !e.Blocked && inWakeJump) { r.WakeJumpsPunished++; inWakeJump = false; } }
		match.Hit += OnHit;

		for (int t = 0; t < ticks; t++)
		{
			var p2In = ai.Next(AiView.Capture(match));
			FighterInput p1In = script switch
			{
				P1Script.Random => RandomInput(p1Rng, ref held, ref holdLeft),
				P1Script.Jumper => match.P1.Actionable && match.Tick % JumpEvery == JumpEvery - 1 ? new FighterInput(InputBits.Up) : FighterInput.None,
				P1Script.Thrower => Thrower(match),
				_ => FighterInput.None,
			};
			match.Step(p1In, p2In);
			r.Ticks++;
			Fighter p1 = match.P1, p2 = match.P2;

			if (p1.Airborne && !p1WasAir && p1.State == FighterState.Jump) { r.P1Jumps++; pendingJump = match.Tick; }
			p1WasAir = p1.Airborne;
			// Only a move the AI started as its answer to that jump counts (not a neutral poke that happened then).
			if (pendingJump >= 0 && p2.State == FighterState.Attack && aiPrev != FighterState.Attack)
			{
				if (r.Decisions.Exists(d => d.Tick == match.Tick && d.Trigger == "opponent_jumping")) r.AntiAirResponse.Add(match.Tick - pendingJump);
				pendingJump = -1;
			}
			if (pendingJump >= 0 && !p1.Airborne) pendingJump = -1; // landed unanswered

			if (p2.State == FighterState.Knockdown && aiPrev != FighterState.Knockdown) r.Knockdowns++;
			if (aiPrev == FighterState.Knockdown && p2.State != FighterState.Knockdown)
			{
				r.WakeUps++;
				if (p2.Airborne) { r.WakeJumps++; inWakeJump = true; }
				if (trace) r.Trace.Add($"tick {match.Tick}: wake-up, {(p2.Airborne ? "JUMPED on the first frame" : p2.State.ToString())}, gap {Math.Abs(p2.X - p1.X) / SimConfig.Scale}, P1 {p1.State}");
			}
			if (!p2.Airborne) inWakeJump = false;
			aiPrev = p2.State;

			if (match.Phase == MatchPhase.Over)
			{
				r.Rounds++;
				if (match.Winner == 0) r.P1Wins++; else if (match.Winner == 1) r.AiWins++;
				r.FinalHash = Fnv.Mix(r.FinalHash == 0 ? Fnv.Offset : r.FinalHash, (int)(match.StateHash() & 0x7FFFFFFF));
				match.Reset();
				ai = NewAi(r.Rounds);
				ai.Log = r.Decisions;
				aiPrev = match.P2.State;
				p1WasAir = false;
				pendingJump = -1;
			}
		}
		r.FinalHash = Fnv.Mix(r.FinalHash == 0 ? Fnv.Offset : r.FinalHash, (int)(match.StateHash() & 0x7FFFFFFF));
		foreach (var d in r.Decisions)
		{
			if (d.Kind == "habit") r.HabitFired++;
			else if (d.Kind == "habit_covered") r.HabitCovered++;
			else if (d.Kind == "habit_no") r.HabitNo++;
		}
		return r;
	}

	private static readonly InputBits[] RandomPool =
	{
		InputBits.None, InputBits.Left, InputBits.Right, InputBits.Down, InputBits.Up, InputBits.Left | InputBits.Down,
		InputBits.LightPunch, InputBits.MediumPunch, InputBits.HeavyPunch, InputBits.LightKick, InputBits.MediumKick,
		InputBits.HeavyKick, InputBits.LightPunch | InputBits.LightKick,
	};

	/// <summary>Seeded random raw bits, each held 1–12 ticks (buttons for one tick so they press again).</summary>
	public static FighterInput RandomInput(SimRng rng, ref InputBits held, ref int holdLeft)
	{
		if (holdLeft-- <= 0) { held = RandomPool[rng.Next(RandomPool.Length)]; holdLeft = 1 + rng.Next(12); }
		var bits = held;
		if ((held & InputBits.Attacks) != 0) held &= ~InputBits.Attacks;
		return new FighterInput(bits);
	}

	/// <summary>Walk in and throw (C4 knocks down), so the AI's wake-up habit shows; idle while it is down. A
	/// tester who has learned the tell: when the AI jumps, walk under it and heavy punch it on the way down.</summary>
	public static FighterInput Thrower(Match m)
	{
		Fighter p1 = m.P1, p2 = m.P2;
		if (!p1.Actionable || p2.State is FighterState.Knockdown or FighterState.Thrown) return FighterInput.None;
		int dx = p2.X - p1.X;
		InputBits fwd = dx >= 0 ? InputBits.Right : InputBits.Left;
		if (p2.Airborne)
		{
			// Walk under the jump, then heavy punch so its active frames meet the fall (fixture HP: startup 10).
			if (Math.Abs(dx) / SimConfig.Scale > AntiAirReach) return new FighterInput(fwd);
			return p2.AirFrame is >= 23 and <= 25 ? new FighterInput(InputBits.HeavyPunch) : FighterInput.None;
		}
		if (Math.Abs(dx) / SimConfig.Scale > ThrowReach) return new FighterInput(fwd);
		return (m.Tick & 1) == 0 ? new FighterInput(InputBits.LightPunch | InputBits.LightKick) : FighterInput.None;
	}
}
