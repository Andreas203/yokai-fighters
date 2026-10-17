using System;
using System.Collections.Generic;

namespace YokaiFighters.Sim;

/// <summary>What Ryo chose at a dojo stop (R7: one choice, rest or a training trial).</summary>
public enum DojoChoice { None = 0, Rest = 1, Trial = 2 }

/// <summary>YOK-98, R7: where a chosen trial stands. Running = attempts left; Landed / Failed = done and healed.</summary>
public enum DojoTrialResult { None = 0, Running = 1, Landed = 2, Failed = 3 }

/// <summary>YOK-98: a trial the dojo offers, for the special Ryo owns in <c>Slot</c>.</summary>
public sealed record DojoTrialOffer(SpecialSlot Slot, TrialData Trial);

/// <summary>
/// YOK-84 / YOK-98, R7 (GDD 6.3): one dojo stop. Ryo makes one choice: rest (30% of full health, capped) or a
/// training trial (<see cref="ChooseTrial"/>: 15% when it ends, landed or not, and a level for its special when
/// landed within 3 attempts). Pure C#, deterministic, no Godot.
/// <para>
/// The stop is plain data: <see cref="NodeId"/>, <see cref="Choice"/> and, for a trial, <see cref="TrialId"/>,
/// <see cref="TrialSlot"/>, <see cref="Attempts"/> and <see cref="TrialResult"/>. <see cref="RunSession"/> opens one
/// when a dojo node is entered and drops it when the node is completed. The game saves only at node boundaries
/// (entry: no choice yet; completion: the stop is gone), so no choice is ever in the save file: M5, a quit
/// mid-trial and Continue restart the node from its entry save, attempts at 0. <see cref="Restore"/> puts a rest
/// back for a caller that rebuilds a stop by hand (a trial has no restore: nothing saves one).
/// A stop the session opened is <see cref="IsOpen"/> only while it is that session's entered stop and the run is
/// not over: a reference kept past the node, or past a lost fight, refuses everything and changes nothing.
/// </para>
/// </summary>
public sealed class Dojo
{
	public const int RestHealPercent = 30; // R7
	public const int TrialHealPercent = 15; // R7: a trial heals this whether it is landed or not
	/// <summary>Mixed into <see cref="RunSession.StateHash"/> before the choice, so another stop's choice can't hash the same ("DOJO").</summary>
	public const int HashTag = 0x444F4A4F;

	/// <summary>R7: what a rest heals before the cap: 30% of full health (C1: 1,000 → 300). Exact today; nearest whole number, as E20 rounds percentages for the reward draft.</summary>
	public static int RestHeal => (RunState.MaxHealth * RestHealPercent + 50) / 100;
	/// <summary>R7: what a finished trial heals before the cap: 15% of full health (C1: 1,000 → 150), landed or not.</summary>
	public static int TrialHeal => (RunState.MaxHealth * TrialHealPercent + 50) / 100;
	/// <summary>R7's rest from <paramref name="health"/>, capped at full (the rest card's preview).</summary>
	public static int Rested(int health) => Math.Min(RunState.MaxHealth, health + RestHeal);

	readonly RunState _run;
	readonly Func<Dojo, bool>? _open;
	readonly Func<IReadOnlyList<TrialData>>? _trials;

	public string NodeId { get; }
	public DojoChoice Choice { get; private set; }
	/// <summary>Health the choice actually gave back (0 until a rest or a finished trial; less than the heal near full health).</summary>
	public int Healed { get; private set; }
	/// <summary>Ryo is still at this stop: its owner says so (<see cref="RunSession"/>: the entered node's stop, run not over). A stop with no owner is always open.</summary>
	public bool IsOpen => _open is null || _open(this);
	/// <summary>R7: Ryo is at the stop and its one choice is still unmade.</summary>
	public bool CanRest => IsOpen && Choice == DojoChoice.None;

	/// <summary>YOK-98: the chosen trial's id (null until <see cref="ChooseTrial"/>).</summary>
	public string? TrialId { get; private set; }
	/// <summary>YOK-98: the slot of the special the chosen trial levels (None until chosen).</summary>
	public SpecialSlot TrialSlot { get; private set; }
	/// <summary>YOK-98, R7: attempts used on the chosen trial (0..<see cref="TrialLoader.Attempts"/>).</summary>
	public int Attempts { get; private set; }
	public DojoTrialResult TrialResult { get; private set; }
	/// <summary>YOK-98: the special's level when the trial was chosen, and after it (equal unless a landed trial levelled it).</summary>
	public int LevelBefore { get; private set; }
	public int LevelAfter { get; private set; }
	/// <summary>A trial is chosen and still has attempts to play: the stop can't be left yet.</summary>
	public bool TrialRunning => TrialResult == DojoTrialResult.Running;

	/// <param name="open">Asked before every change: is this still the stop Ryo is at? Null = always (a stop on its own).</param>
	/// <param name="trials">YOK-98: the hand-authored trials of this build (data/trials/); null = none, so no trial is offered.</param>
	public Dojo(RunState run, string nodeId, Func<Dojo, bool>? open = null, Func<IReadOnlyList<TrialData>>? trials = null)
	{
		_run = run ?? throw new ArgumentNullException(nameof(run));
		NodeId = nodeId ?? throw new ArgumentNullException(nameof(nodeId));
		_open = open;
		_trials = trials;
	}

	/// <summary>
	/// YOK-98, R7: the trials Ryo may pick, one per owned special (slot order A-D) that has a trial in data/trials/
	/// and can still level (<see cref="RunState.CanLevelUp"/>). A special with no trial gets no trial option; when
	/// two trials name one special the first in library order (file name) is offered. Empty = no trial card.
	/// PROVISIONAL (YOK-98): a special already at its top level gets no trial (a landed trial would level nothing).
	/// </summary>
	public IReadOnlyList<DojoTrialOffer> Trials
	{
		get
		{
			var offers = new List<DojoTrialOffer>();
			var library = _trials?.Invoke();
			if (library is null || library.Count == 0) return offers;
			foreach (var (slot, owned) in _run.Owned)
			{
				if (!_run.CanLevelUp(slot)) continue;
				foreach (var t in library)
					if (t.Special == owned.Data.Id) { offers.Add(new DojoTrialOffer(slot, t)); break; }
			}
			return offers;
		}
	}

	/// <summary>The trial data of <paramref name="trialId"/> in this build's library, or null.</summary>
	public TrialData? Trial(string? trialId)
	{
		var library = _trials?.Invoke();
		if (library is null || trialId is null) return null;
		foreach (var t in library) if (t.Id == trialId) return t;
		return null;
	}

	/// <summary>R7: Ryo is at the stop, its one choice is unmade and at least one trial is offered.</summary>
	public bool CanTrial => IsOpen && Choice == DojoChoice.None && Trials.Count > 0;

	/// <summary>
	/// R7: Ryo rests: health + <see cref="RestHeal"/>, capped at full. It is the stop's one choice, so a second
	/// rest (or a rest after a trial) is refused and changes nothing. So is a rest at a stop Ryo has left
	/// or whose run is over.
	/// </summary>
	public MapResult Rest()
	{
		if (!IsOpen) return MapResult.Fail($"dojo '{NodeId}': Ryo is no longer at this stop (R7)");
		if (Choice != DojoChoice.None) return MapResult.Fail($"dojo '{NodeId}': the choice is already made ({Choice}) (R7)");
		int before = _run.Health;
		_run.SetHealth(before + RestHeal); // SetHealth caps at full health
		Healed = _run.Health - before;
		Choice = DojoChoice.Rest;
		return MapResult.Done;
	}

	/// <summary>
	/// YOK-98, R7: Ryo takes the training trial <paramref name="trialId"/> as the stop's one choice (the rest closes).
	/// Nothing heals yet: the heal and the level come when the trial ends (<see cref="RecordAttempt"/>). Refused,
	/// changing nothing, at a stop Ryo has left, once a choice is made, or for a trial the stop does not offer.
	/// </summary>
	public MapResult ChooseTrial(string trialId)
	{
		if (!IsOpen) return MapResult.Fail($"dojo '{NodeId}': Ryo is no longer at this stop (R7)");
		if (Choice != DojoChoice.None) return MapResult.Fail($"dojo '{NodeId}': the choice is already made ({Choice}) (R7)");
		DojoTrialOffer? offer = null;
		foreach (var o in Trials) if (o.Trial.Id == trialId) { offer = o; break; }
		if (offer is null) return MapResult.Fail($"dojo '{NodeId}': no trial '{trialId}' is offered here (R7)");
		Choice = DojoChoice.Trial;
		TrialId = offer.Trial.Id;
		TrialSlot = offer.Slot;
		Attempts = 0;
		TrialResult = DojoTrialResult.Running;
		LevelBefore = LevelAfter = _run[offer.Slot]!.Level;
		return MapResult.Done;
	}

	/// <summary>
	/// YOK-98, R7: one attempt of the chosen trial ended, <paramref name="landed"/> or not. A landed attempt, or the
	/// third miss, ends the trial: health + <see cref="TrialHeal"/> (capped) either way, and a landed trial levels
	/// its special (<see cref="RunState.LevelUp"/>, so T3's level-up order sees it) when that slot still holds it
	/// and it can still level. Refused, changing nothing, unless a trial is running at a stop Ryo is still at.
	/// </summary>
	public MapResult RecordAttempt(bool landed)
	{
		if (!IsOpen) return MapResult.Fail($"dojo '{NodeId}': Ryo is no longer at this stop (R7)");
		if (TrialResult != DojoTrialResult.Running) return MapResult.Fail($"dojo '{NodeId}': no trial is running (R7)");
		Attempts++;
		if (!landed && Attempts < TrialLoader.Attempts) return MapResult.Done;
		int before = _run.Health;
		_run.SetHealth(before + TrialHeal); // SetHealth caps at full health
		Healed = _run.Health - before;
		if (landed && _run[TrialSlot] is { } s && s.Data.Id == Trial(TrialId)?.Special && _run.CanLevelUp(TrialSlot))
			LevelAfter = _run.LevelUp(TrialSlot);
		TrialResult = landed ? DojoTrialResult.Landed : DojoTrialResult.Failed;
		return MapResult.Done;
	}

	/// <summary>
	/// Puts back the choice of a saved stop without healing again, on a stop just opened. Throws, changing nothing,
	/// for a value that is not a <see cref="DojoChoice"/> or is a trial (never saved, M5), once a
	/// choice has been made or restored here (it can't be taken back, R7), or at a stop Ryo has left.
	/// <see cref="Healed"/> is not saved state and reads 0 after this.
	/// </summary>
	public void Restore(DojoChoice choice)
	{
		if (choice == DojoChoice.Trial) throw new ArgumentException($"dojo '{NodeId}': a trial is never restored: Continue restarts the node (M5)", nameof(choice));
		if (!Enum.IsDefined(choice)) throw new ArgumentException($"dojo '{NodeId}': unknown choice {(int)choice}", nameof(choice));
		if (!IsOpen) throw new InvalidOperationException($"dojo '{NodeId}': Ryo is no longer at this stop (R7)");
		if (Choice != DojoChoice.None) throw new InvalidOperationException($"dojo '{NodeId}': the choice is already made ({Choice}) (R7)");
		Choice = choice;
		Healed = 0;
	}
}
