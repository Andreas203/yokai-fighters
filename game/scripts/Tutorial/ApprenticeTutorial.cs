using System;
using System.Collections.Generic;
using YokaiFighters.Sim;

namespace YokaiFighters.Tutorial;

/// <summary>Y6's lessons, in the order the GDD lists them (blocking, throw, throw break, meter, burst, one cancel).</summary>
public enum TutorialLesson { Block, Throw, ThrowBreak, Meter, Burst, Cancel }

public enum LessonOutcome
{
	/// <summary>Not reached yet, or the one on screen.</summary>
	Pending,
	/// <summary>The player did what the prompt asked.</summary>
	Done,
	/// <summary>The player skipped the prompt (GDD 6.1: the prompts are skippable).</summary>
	Skipped,
	/// <summary>The fight cannot show it: no cancel rule held (X1), or the once-per-fight burst already spent (C6).</summary>
	Unavailable,
}

/// <summary>
/// Tunables of the guided fight. Y6 names the lessons but no counts or timings: every value here is
/// PROVISIONAL (gameplay-programmer's first pass, YOK-92) until the designer sets it.
/// </summary>
public sealed record TutorialOptions
{
	/// <summary>PROVISIONAL: apprentice hits Ryo must block to pass the blocking lesson.</summary>
	public int BlocksToPass { get; init; } = 2;
	/// <summary>PROVISIONAL: a lesson stays on screen at least this many ticks (1 s) even when done at once.</summary>
	public int MinPromptTicks { get; init; } = 60;
	/// <summary>PROVISIONAL: ticks between the apprentice's swings while it is teaching blocking or burst.</summary>
	public int AttackEvery { get; init; } = 45;
	/// <summary>PROVISIONAL: ticks the apprentice waits after a throw attempt before trying again (throw-break lesson).</summary>
	public int ThrowRetryTicks { get; init; } = 120;
	/// <summary>
	/// OPEN (X1 vs Y6): X1 gives Ryo no cancel at run start, yet Y6 teaches one cancel. A rule set here is lent to
	/// the player for the whole guided fight (held from tick 0, taken back when the match ends). Null (the default) lends nothing,
	/// so the cancel lesson is <see cref="LessonOutcome.Unavailable"/> unless Ryo already holds a rule.
	/// </summary>
	public CancelRule? LentCancel { get; init; }
}

/// <summary>
/// YOK-92, Y6: the guided apprentice fight. Pure C#, no Godot, no clock, no randomness: it watches a
/// <see cref="Match"/> (events and state after each tick) and walks the lessons in <see cref="Order"/>. A lesson
/// ends when the player does the thing (<see cref="LessonOutcome.Done"/>), skips it, or it cannot happen in this
/// fight. The apprentice's side of each lesson is <see cref="TutorialCoach"/>.
///
/// Done means, for the player (fighter <see cref="Player"/>):
/// Block = <see cref="TutorialOptions.BlocksToPass"/> opponent hits blocked; Throw = a throw landed (C4);
/// ThrowBreak = an opponent throw broken inside the window (a clash does not count); Meter = one full bar (C5,
/// the EX cost, and more meter than on entry, so a bar carried in does not pass it); Burst = the burst fired (C6); Cancel = a move cancelled through a held rule (X3's per-combo
/// mark set). Only what happens while a lesson is on screen counts for it.
///
/// Call <see cref="Observe"/> once after every <see cref="Match.Step"/>; <see cref="Detach"/> when the fight ends.
/// </summary>
public sealed class ApprenticeTutorial
{
	public static readonly IReadOnlyList<TutorialLesson> Order = new[]
	{
		TutorialLesson.Block, TutorialLesson.Throw, TutorialLesson.ThrowBreak,
		TutorialLesson.Meter, TutorialLesson.Burst, TutorialLesson.Cancel,
	};

	public Match Match { get; }
	public TutorialOptions Options { get; }
	public int Player { get; }
	/// <summary>Index into <see cref="Order"/> of the lesson on screen; Order.Count once finished.</summary>
	public int Index { get; private set; }
	public TutorialLesson? Current => Index < Order.Count ? Order[Index] : null;
	public bool Finished => Index >= Order.Count;
	/// <summary>Ticks the current lesson has been on screen.</summary>
	public int LessonTicks { get; private set; }
	/// <summary>Opponent hits blocked during the blocking lesson.</summary>
	public int Blocks { get; private set; }
	/// <summary>The lesson's PLACEHOLDER prompt (habit-writer owns the copy), empty once finished.</summary>
	public string Prompt => Current is { } l ? TutorialPrompts.For(l) : "";

	public event Action<TutorialLesson, LessonOutcome>? LessonEnded;
	public event Action<ApprenticeTutorial>? Completed;

	private readonly LessonOutcome[] _outcomes = new LessonOutcome[Order.Count];
	private bool _met, _detached, _lent;
	private int _meterAtEntry;

	public ApprenticeTutorial(Match match, TutorialOptions? options = null, int player = 0)
	{
		Match = match;
		Options = options ?? new TutorialOptions();
		Player = player;
		Fighter p = match.Fighters[player];
		if (Options.LentCancel is { } rule && !p.CancelRules.Contains(rule) && p.CancelRules.Count < Fighter.MaxCancelRules)
		{
			p.CancelRules.Add(rule);
			_lent = true;
		}
		match.Hit += OnHit;
		match.Throw += OnThrow;
		match.BurstFired += OnBurst;
		Enter();
	}

	public LessonOutcome Outcome(TutorialLesson lesson) => _outcomes[IndexOf(lesson)];

	/// <summary>After each tick: counts the lesson's time and ends it once met and shown long enough.</summary>
	public void Observe()
	{
		if (Finished || _detached) return;
		LessonTicks++;
		Fighter p = Match.Fighters[Player];
		if (Current == TutorialLesson.Meter && p.Meter >= Match.Config.ExCost && p.Meter > _meterAtEntry) _met = true; // C5: built during the lesson
		if (Current == TutorialLesson.Cancel && p.CancelUsed != 0) _met = true;
		if (_met && LessonTicks >= Options.MinPromptTicks) End(LessonOutcome.Done);
	}

	/// <summary>Skips the lesson on screen. False once finished.</summary>
	public bool SkipLesson()
	{
		if (Finished || _detached) return false;
		End(LessonOutcome.Skipped);
		return true;
	}

	/// <summary>Skips every lesson left.</summary>
	public void SkipAll()
	{
		while (SkipLesson()) { }
	}

	/// <summary>
	/// Stops listening to the match and takes back a lent cancel rule. Lessons left stay Pending. Call only when the
	/// match is over or being replaced (KO, reset, next duel): the rule must not vanish mid-fight (C1/F3).
	/// </summary>
	public void Detach()
	{
		if (_detached) return;
		_detached = true;
		Match.Hit -= OnHit;
		Match.Throw -= OnThrow;
		Match.BurstFired -= OnBurst;
		if (_lent && Options.LentCancel is { } rule) Match.Fighters[Player].CancelRules.Remove(rule);
		_lent = false;
	}

	private static int IndexOf(TutorialLesson l)
	{
		for (int i = 0; i < Order.Count; i++) if (Order[i] == l) return i;
		throw new ArgumentOutOfRangeException(nameof(l));
	}

	private void OnHit(Match m, HitEvent e)
	{
		if (Current != TutorialLesson.Block || e.Attacker == Player || !e.Blocked) return;
		if (++Blocks >= Options.BlocksToPass) _met = true;
	}

	private void OnThrow(Match m, ThrowEvent e)
	{
		if (Current == TutorialLesson.Throw && e.Outcome == ThrowOutcome.Land && e.Attacker == Player) _met = true;
		if (Current == TutorialLesson.ThrowBreak && e.Outcome == ThrowOutcome.Break && e.Attacker != Player) _met = true;
	}

	private void OnBurst(Match m, int fighter)
	{
		if (Current == TutorialLesson.Burst && fighter == Player) _met = true;
	}

	/// <summary>Opens the lesson at <see cref="Index"/>, ending at once any this fight cannot show.</summary>
	private void Enter()
	{
		LessonTicks = 0;
		_met = false;
		if (Finished || _detached) return;
		Fighter p = Match.Fighters[Player];
		_meterAtEntry = p.Meter;
		bool unavailable = Current switch
		{
			TutorialLesson.Meter => p.Meter >= Match.Config.MeterMax, // full: nothing left to build
			TutorialLesson.Burst => p.BurstUsed, // C6: once per fight
			TutorialLesson.Cancel => p.CancelRules.Count == 0, // X1: nothing to cancel with
			_ => false,
		};
		if (unavailable) End(LessonOutcome.Unavailable);
	}

	private void End(LessonOutcome outcome)
	{
		TutorialLesson lesson = Order[Index];
		_outcomes[Index] = outcome;
		Index++;
		LessonEnded?.Invoke(lesson, outcome);
		if (Finished)
		{
			// A lent rule is NOT taken back here: removing it mid-match would change the fight with no input to
			// replay it from (C1/F3). It goes back on Detach, which the scene calls at KO, reset or a new match.
			Completed?.Invoke(this);
			return;
		}
		Enter();
	}
}
