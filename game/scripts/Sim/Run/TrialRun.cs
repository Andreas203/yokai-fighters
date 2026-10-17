using System;
using System.Collections.Generic;

namespace YokaiFighters.Sim;

/// <summary>
/// YOK-98, R7: plays one training trial on the ordinary fight loop (<see cref="Match.Step"/>), deterministic, no
/// Godot. Ryo is P1, the training dummy P2. Each attempt starts with a fresh <see cref="Match.Reset"/> at the
/// trial's start distance (<see cref="Config"/>), lasts <see cref="TrialData.AttemptFrames"/> ticks and checks
/// the goal steps in order from the sim's own events (<see cref="Match.Hit"/>, <see cref="Match.Throw"/>), never
/// from buttons, so Kihon and Kata play the same trial (K2, K3). The dummy is driven by
/// <see cref="TrialData.Dummy"/>, never by an AI.
/// <para>
/// When an attempt ends (goal met, time up, or a KO) <see cref="AttemptEnded"/> is raised with whether it landed;
/// the owner reports it to <see cref="Dojo.RecordAttempt"/> and calls <see cref="NextAttempt"/> while the dojo
/// still has attempts left. Health in the trial fight never touches the run: only the dojo's heal does (R7).
/// PROVISIONAL (YOK-98): a KO of either fighter ends the attempt; the dummy starts every attempt at full health.
/// </para>
/// </summary>
public sealed class TrialRun
{
	public TrialData Trial { get; }
	public Match Match { get; }
	/// <summary>Attempt being played, 1-based.</summary>
	public int Attempt { get; private set; } = 1;
	/// <summary>Ticks played in this attempt.</summary>
	public int AttemptTick { get; private set; }
	/// <summary>Goal steps met so far in this attempt.</summary>
	public int StepsMet { get; private set; }
	/// <summary>The current attempt has ended and waits for <see cref="NextAttempt"/>.</summary>
	public bool AttemptOver { get; private set; }
	public bool Landed { get; private set; }
	/// <summary>The dummy's input on the last tick played.</summary>
	public FighterInput LastDummyInput { get; private set; }

	/// <summary>Raised once per attempt when it ends: (this run, landed).</summary>
	public event Action<TrialRun, bool>? AttemptEnded;

	int _lastAirTick = int.MinValue / 2;
	int _count;            // hits/blocks counted toward the current step
	bool _comboMoveSeen;   // a combo step's named move landed inside the combo
	(bool Airborne, int SinceAir, int Units)? _grab; // the player's grab tick, for a throw step
	readonly Queue<int> _dummyQueue = new();
	readonly HashSet<string> _p1Throws = new(StringComparer.Ordinal);

	/// <summary>The fight config for <paramref name="trial"/>: <paramref name="baseConfig"/> with the fighters <see cref="TrialData.Distance"/> units apart.</summary>
	public static SimConfig Config(SimConfig baseConfig, TrialData trial) =>
		baseConfig with { StartOffset = trial.Distance * SimConfig.Scale / 2 };

	/// <param name="match">A match built on <see cref="Config"/> (Ryo P1, the dummy P2); it is reset here.</param>
	public TrialRun(TrialData trial, Match match)
	{
		Trial = trial ?? throw new ArgumentNullException(nameof(trial));
		Match = match ?? throw new ArgumentNullException(nameof(match));
		foreach (var m in match.P1.Moves) if (m.IsThrow) _p1Throws.Add(m.Id);
		match.Hit += OnHit;
		match.Throw += OnThrow;
		Begin();
	}

	void Begin()
	{
		Match.Reset();
		AttemptTick = 0;
		StepsMet = 0;
		AttemptOver = false;
		Landed = false;
		_lastAirTick = int.MinValue / 2;
		_count = 0;
		_comboMoveSeen = false;
		_grab = null;
		_dummyQueue.Clear();
	}

	/// <summary>Starts the next attempt (after one ended). Refused when the current attempt is still playing.</summary>
	public bool NextAttempt()
	{
		if (!AttemptOver) return false;
		Attempt++;
		Begin();
		return true;
	}

	/// <summary>One tick: Ryo's input, the dummy's scripted input, then the goal check. Does nothing once the attempt is over.</summary>
	public void Step(FighterInput p1)
	{
		if (AttemptOver) return;
		AttemptTick++;
		LastDummyInput = DummyInput();
		Match.Step(p1, LastDummyInput);
		if (Match.P2.Airborne) _lastAirTick = AttemptTick;
		CheckComboHeld();
		if (StepsMet >= Trial.Steps.Count) End(true);
		else if (AttemptTick >= Trial.AttemptFrames || Match.Phase != MatchPhase.Fighting) End(false);
	}

	void End(bool landed)
	{
		AttemptOver = true;
		Landed = landed;
		AttemptEnded?.Invoke(this, landed);
	}

	/// <summary>The dummy's input this tick (R7 setup): held stance, a jump press, or the next move of its string.</summary>
	public FighterInput DummyInput()
	{
		var d = Trial.Dummy;
		var dummy = Match.P2;
		InputBits back = dummy.Facing > 0 ? InputBits.Left : InputBits.Right;
		bool due = d.Every > 0 && (AttemptTick - 1) % d.Every == 0; // tick 1, 1 + Every, ...
		switch (d.Action)
		{
			case TrialDummyAction.Crouch: return new FighterInput(InputBits.Down);
			case TrialDummyAction.Guard: return new FighterInput(back);
			case TrialDummyAction.CrouchGuard: return new FighterInput(InputBits.Down | back);
			case TrialDummyAction.Jump: return new FighterInput(due ? InputBits.Up : InputBits.None);
			case TrialDummyAction.Attack:
				if (due) foreach (string id in d.Moves) { int i = Array.FindIndex(dummy.Moves, m => m.Id == id); if (i >= 0) _dummyQueue.Enqueue(i); }
				if (_dummyQueue.Count > 0 && dummy.Actionable) return FighterInput.Attack(_dummyQueue.Dequeue());
				return default;
			default: return default;
		}
	}

	TrialStep? Current => StepsMet < Trial.Steps.Count ? Trial.Steps[StepsMet] : null;

	(bool Airborne, int SinceAir, int Units) Sample(int tick) =>
		(Match.P2.Airborne, tick - _lastAirTick, Math.Abs(Match.P1.X - Match.P2.X) / SimConfig.Scale);

	bool Met(TrialStep step, (bool Airborne, int SinceAir, int Units) s) =>
		step.DummyStateMet(s.Airborne, s.SinceAir) && step.DistanceMet(s.Units);

	void Advance() { StepsMet++; _count = 0; _comboMoveSeen = false; }

	void OnHit(Match m, HitEvent e)
	{
		if (AttemptOver || Current is not { } step) return;
		// The Hit fires inside Match.Step, before this tick's airborne bookkeeping: this tick is AttemptTick.
		var s = Sample(AttemptTick);
		if (step.Event == TrialEvent.Block)
		{
			if (e.Attacker != 1 || !e.Blocked) return;
			if (++_count >= Math.Max(1, step.Count)) Advance();
			return;
		}
		if (e.Attacker != 0 || e.Blocked || _p1Throws.Contains(e.MoveId)) return; // throws are Throw steps only
		switch (step.Event)
		{
			case TrialEvent.Hit:
				if (step.Move is { } mv && mv != e.MoveId) return;
				if (step.Counter && !e.Counter) return;
				if (Met(step, s)) Advance();
				break;
			case TrialEvent.Combo:
				_count++;
				if (step.Move is null || step.Move == e.MoveId) _comboMoveSeen = true;
				// PROVISIONAL (YOK-98): state, distance and counter are read on the hit that completes the count.
				if (_count >= step.Count && _comboMoveSeen && (!step.Counter || e.Counter) && Met(step, s)) Advance();
				break;
		}
	}

	void CheckComboHeld()
	{
		if (Current is { Event: TrialEvent.Combo } && _count > 0 && !TrialStep.ComboHolds(Match.P2.State))
		{ _count = 0; _comboMoveSeen = false; }
	}

	void OnThrow(Match m, ThrowEvent e)
	{
		if (AttemptOver || e.Attacker != 0 || Current is not { Event: TrialEvent.Throw } step) return;
		if (e.Outcome == ThrowOutcome.Grab) { _grab = Sample(AttemptTick); return; }
		if (e.Outcome != ThrowOutcome.Land) { _grab = null; return; }
		if (_grab is { } g && (step.Move is null || step.Move == e.MoveId) && Met(step, g)) Advance();
		_grab = null;
	}
}
