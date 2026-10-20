using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using YokaiFighters.Ai;
using YokaiFighters.Sim;

namespace YokaiFighters.Harness;

/// <summary>YOK-157: one fighter's connects with one move in one fight (a landed throw counts as a hit and as a throw).</summary>
public sealed class MoveTally
{
	public int Hits, Blocked, Counter, Damage, ThrowsLanded, ThrowsBroken;

	public void Add(MoveTally o)
	{
		Hits += o.Hits; Blocked += o.Blocked; Counter += o.Counter; Damage += o.Damage;
		ThrowsLanded += o.ThrowsLanded; ThrowsBroken += o.ThrowsBroken;
	}
}

/// <summary>YOK-157: one raw log line (opt-in, <see cref="HarnessContent.RawHitLog"/>). Kind: hit, throw, throw_break, throw_clash.
/// Damage is on hit lines only (a landed throw's damage is on the hit line the sim raises with it).</summary>
public readonly record struct HitLine(int Tick, int Attacker, string MoveId, string Kind, bool Blocked, bool Counter, int Damage);

/// <summary>
/// YOK-157 (P5, P6, Y2): what the harness hears from one fight, from the sim's own <see cref="Match.Hit"/> and
/// <see cref="Match.Throw"/> events and the opponent AI's decision log. Listening only: nothing here feeds back into
/// the match or either controller, so a fight plays the same with or without it.
/// Moves are tallied per side (0 = Ryo, 1 = the opponent) and move id. A break is tallied on the thrower's move
/// (<see cref="MoveTally.ThrowsBroken"/>) and counted for the side that broke it (<see cref="ThrowBreaks"/>).
/// Habit (Y2): every roll of the profile's habit, whether it fired, was covered (tell_cover_pct), and whether Ryo
/// punished it: an unblocked Ryo hit while the habit's plan runs or within <see cref="PunishTailTicks"/> after it ends.
/// </summary>
public sealed class FightLog
{
	/// <summary>Proposed (harness only, no rule): ticks after a habit plan's last input in which Ryo's hit still counts as its punish.</summary>
	public const int PunishTailTicks = 30;

	public SortedDictionary<string, MoveTally>[] Moves { get; } = { new(System.StringComparer.Ordinal), new(System.StringComparer.Ordinal) };
	/// <summary>Throws each side broke as the defender, and grab-vs-grab clashes.</summary>
	public int[] ThrowBreaks { get; } = new int[2];
	public int ThrowClashes { get; set; }
	public string HabitWhen { get; set; } = "";
	public string HabitAction { get; set; } = "";
	public int HabitRolls, HabitFired, HabitCovered, HabitPunished;
	public List<HitLine>? Raw { get; }

	int _aiLogged, _punishUntil = -1;
	bool _windowOpen;

	public FightLog(bool raw) { if (raw) Raw = new(); }

	/// <summary>Listens to <paramref name="m"/> and to <paramref name="ai"/>'s decision log (setting it changes no roll).</summary>
	public void Attach(Match m, ProfileAi ai)
	{
		HabitWhen = BehaviourProfile.ObservationName(ai.Profile.Habit.When); // as the profile data names it
		HabitAction = ai.Profile.Habit.Do.ToString();
		ai.Log ??= new List<AiDecision>();
		_aiLogged = ai.Log.Count;
		m.Hit += (match, e) => OnHit(match.Tick, e);
		m.Throw += (match, e) => OnThrow(match.Tick, e);
	}

	MoveTally Tally(int side, string move)
	{
		var d = Moves[side];
		if (!d.TryGetValue(move, out var t)) d[move] = t = new MoveTally();
		return t;
	}

	void OnHit(int tick, HitEvent e)
	{
		if (e.Attacker is not (0 or 1)) return;
		var t = Tally(e.Attacker, e.MoveId);
		t.Hits++;
		if (e.Blocked) t.Blocked++;
		if (e.Counter) t.Counter++;
		t.Damage += e.Damage;
		Raw?.Add(new HitLine(tick, e.Attacker, e.MoveId, "hit", e.Blocked, e.Counter, e.Damage));
		if (_windowOpen && e.Attacker == 0 && !e.Blocked)
		{
			HabitPunished++;
			_windowOpen = false;
		}
	}

	void OnThrow(int tick, ThrowEvent e)
	{
		if (e.Attacker is not (0 or 1)) return;
		switch (e.Outcome)
		{
			case ThrowOutcome.Land:
				Tally(e.Attacker, e.MoveId).ThrowsLanded++; // its damage arrives with the Hit the sim raises next
				Raw?.Add(new HitLine(tick, e.Attacker, e.MoveId, "throw", false, false, 0)); // damage on its hit line, not twice
				break;
			case ThrowOutcome.Break:
				Tally(e.Attacker, e.MoveId).ThrowsBroken++;
				ThrowBreaks[1 - e.Attacker]++;
				Raw?.Add(new HitLine(tick, e.Attacker, e.MoveId, "throw_break", false, false, 0));
				break;
			case ThrowOutcome.Clash:
				ThrowClashes++;
				Raw?.Add(new HitLine(tick, e.Attacker, e.MoveId, "throw_clash", false, false, 0));
				break;
		}
	}

	/// <summary>Call once per tick after the AI's <c>Next</c> and before the match steps: reads the AI's new habit rolls and its plan.</summary>
	public void AfterAi(ProfileAi ai, int tick)
	{
		var log = ai.Log!;
		for (; _aiLogged < log.Count; _aiLogged++)
		{
			string k = log[_aiLogged].Kind;
			if (k is not ("habit" or "habit_no" or "habit_covered")) continue;
			HabitRolls++;
			if (k == "habit_no") continue;
			HabitFired++;
			if (k == "habit_covered") { HabitCovered++; continue; }
			_windowOpen = true; // a new habit replaces an unpunished one's window
			_punishUntil = tick + PunishTailTicks;
		}
		if (!_windowOpen) return;
		if (ai.HabitPlanRunning) _punishUntil = tick + PunishTailTicks;
		else if (tick > _punishUntil) _windowOpen = false;
	}
}

/// <summary>
/// YOK-157: the results-file blocks for <see cref="FightLog"/> (additive to schema version 1: readers of the old keys are
/// unaffected). Per fight <c>log</c>; in the summary <c>moves</c> (P5 single-move dominance) and <c>habits</c> (P6/Y2 punish rate).
/// </summary>
public static class FightLogReport
{
	public static readonly string[] Sides = { "ryo", "opponent" };

	/// <summary>The fight's <c>log</c> block (nothing when the log was off).</summary>
	public static void WriteFight(Utf8JsonWriter w, FightLog? log)
	{
		if (log is null) return;
		w.WriteStartObject("log");
		w.WriteStartObject("moves");
		for (int side = 0; side < 2; side++)
		{
			w.WriteStartObject(Sides[side]);
			foreach (var (id, t) in log.Moves[side]) Tally(w, id, t);
			w.WriteEndObject();
		}
		w.WriteEndObject();
		w.WriteStartObject("throw_breaks");
		for (int side = 0; side < 2; side++) w.WriteNumber(Sides[side], log.ThrowBreaks[side]);
		w.WriteEndObject();
		w.WriteNumber("throw_clashes", log.ThrowClashes);
		w.WriteStartObject("habit");
		w.WriteString("when", log.HabitWhen);
		w.WriteString("do", log.HabitAction);
		w.WriteNumber("rolls", log.HabitRolls);
		w.WriteNumber("fired", log.HabitFired);
		w.WriteNumber("covered", log.HabitCovered);
		w.WriteNumber("punished", log.HabitPunished);
		w.WriteEndObject();
		if (log.Raw is { } raw)
		{
			w.WriteStartArray("hit_log");
			foreach (var h in raw)
			{
				w.WriteStartObject();
				w.WriteNumber("tick", h.Tick);
				w.WriteString("attacker", Sides[h.Attacker]);
				w.WriteString("move", h.MoveId);
				w.WriteString("kind", h.Kind);
				w.WriteBoolean("blocked", h.Blocked);
				w.WriteBoolean("counter", h.Counter);
				w.WriteNumber("damage", h.Damage);
				w.WriteEndObject();
			}
			w.WriteEndArray();
		}
		w.WriteEndObject();
	}

	/// <summary>
	/// Summary <c>moves</c>: per side and move id, the batch totals, the fights it connected in and its share of that side's
	/// damage (P5). Summary <c>habits</c>: per opponent profile, rolls / fired / covered / punished and punished as a percent
	/// of the uncovered habits (P6, Y2). Both empty when the log was off.
	/// </summary>
	public static void WriteSummary(Utf8JsonWriter w, IEnumerable<FightRecord> fights)
	{
		var logged = fights.Where(f => f.Log is not null).ToList();
		w.WriteStartObject("moves");
		for (int side = 0; side < 2; side++)
		{
			var total = new SortedDictionary<string, MoveTally>(StringComparer.Ordinal);
			var used = new Dictionary<string, int>(StringComparer.Ordinal);
			foreach (var f in logged)
				foreach (var (id, t) in f.Log!.Moves[side])
				{
					if (!total.TryGetValue(id, out var sum)) total[id] = sum = new MoveTally();
					sum.Add(t);
					used[id] = used.GetValueOrDefault(id) + 1;
				}
			long all = total.Values.Sum(t => (long)t.Damage);
			w.WriteStartObject(Sides[side]);
			foreach (var (id, t) in total)
			{
				Tally(w, id, t, open: false);
				w.WriteNumber("fights", used[id]);
				if (all == 0) w.WriteNull("damage_pct");
				else w.WriteNumber("damage_pct", Math.Round(100.0 * t.Damage / all, 1, MidpointRounding.AwayFromZero));
				w.WriteEndObject();
			}
			w.WriteEndObject();
		}
		w.WriteEndObject();
		w.WriteStartObject("habits");
		foreach (var g in logged.GroupBy(f => f.Profile).OrderBy(g => g.Key, StringComparer.Ordinal))
		{
			var first = g.First().Log!;
			int rolls = g.Sum(f => f.Log!.HabitRolls), fired = g.Sum(f => f.Log!.HabitFired), covered = g.Sum(f => f.Log!.HabitCovered);
			int punished = g.Sum(f => f.Log!.HabitPunished), open = fired - covered;
			w.WriteStartObject(g.Key);
			w.WriteString("when", first.HabitWhen);
			w.WriteString("do", first.HabitAction);
			w.WriteNumber("fights", g.Count());
			w.WriteNumber("rolls", rolls);
			w.WriteNumber("fired", fired);
			w.WriteNumber("covered", covered);
			w.WriteNumber("punished", punished);
			if (open == 0) w.WriteNull("punished_pct_of_uncovered");
			else w.WriteNumber("punished_pct_of_uncovered", Math.Round(100.0 * punished / open, 1, MidpointRounding.AwayFromZero));
			w.WriteNumber("bot_anti_airs", g.Sum(f => f.Bot.AntiAirs));
			w.WriteEndObject();
		}
		w.WriteEndObject();
	}

	static void Tally(Utf8JsonWriter w, string id, MoveTally t, bool open = true)
	{
		w.WriteStartObject(id);
		w.WriteNumber("hits", t.Hits);
		w.WriteNumber("blocked", t.Blocked);
		w.WriteNumber("counter", t.Counter);
		w.WriteNumber("damage", t.Damage);
		w.WriteNumber("throws_landed", t.ThrowsLanded);
		w.WriteNumber("throws_broken", t.ThrowsBroken);
		if (open) w.WriteEndObject();
	}
}
