using System;

namespace YokaiFighters.Sim;

/// <summary>What Ryo chose at a dojo stop (R7: one choice). The training trial will add its own value.</summary>
public enum DojoChoice { None = 0, Rest = 1 }

/// <summary>
/// YOK-84, R7 (GDD 6.3): one dojo stop, the rest half only. Ryo may rest once, as his one choice at the stop;
/// resting heals 30% of his full health, capped at full. Pure C#, deterministic, no Godot. The training trial
/// (the other choice of R7) is a later feature on this same stop.
/// <para>
/// The stop is plain data: <see cref="NodeId"/> and <see cref="Choice"/>. <see cref="RunSession"/> opens one when a
/// dojo node is entered and drops it when the node is completed; a save writes <see cref="Choice"/> and puts it
/// back with <see cref="Restore"/> (the heal itself is already in the saved health).
/// A stop the session opened is <see cref="IsOpen"/> only while it is that session's entered stop and the run is
/// not over: a reference kept past the node, or past a lost fight, refuses everything and changes nothing.
/// </para>
/// </summary>
public sealed class Dojo
{
	public const int RestHealPercent = 30; // R7
	/// <summary>Mixed into <see cref="RunSession.StateHash"/> before the choice, so another stop's choice can't hash the same ("DOJO").</summary>
	public const int HashTag = 0x444F4A4F;

	/// <summary>R7: what a rest heals before the cap: 30% of full health (C1: 1,000 → 300). Exact today; nearest whole number, as E20 rounds percentages for the reward draft.</summary>
	public static int RestHeal => (RunState.MaxHealth * RestHealPercent + 50) / 100;

	readonly RunState _run;
	readonly Func<Dojo, bool>? _open;

	public string NodeId { get; }
	public DojoChoice Choice { get; private set; }
	/// <summary>Health the choice actually gave back (0 until a choice is made; less than <see cref="RestHeal"/> near full health).</summary>
	public int Healed { get; private set; }
	/// <summary>Ryo is still at this stop: its owner says so (<see cref="RunSession"/>: the entered node's stop, run not over). A stop with no owner is always open.</summary>
	public bool IsOpen => _open is null || _open(this);
	/// <summary>R7: Ryo is at the stop and its one choice is still unmade.</summary>
	public bool CanRest => IsOpen && Choice == DojoChoice.None;

	/// <param name="open">Asked before every change: is this still the stop Ryo is at? Null = always (a stop on its own).</param>
	public Dojo(RunState run, string nodeId, Func<Dojo, bool>? open = null)
	{
		_run = run ?? throw new ArgumentNullException(nameof(run));
		NodeId = nodeId ?? throw new ArgumentNullException(nameof(nodeId));
		_open = open;
	}

	/// <summary>
	/// R7: Ryo rests: health + <see cref="RestHeal"/>, capped at full. It is the stop's one choice, so a second
	/// rest (or a rest after another choice) is refused and changes nothing. So is a rest at a stop Ryo has left
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
	/// Puts back the choice of a saved stop without healing again, on a stop just opened. Throws, changing nothing,
	/// for a value that is not a <see cref="DojoChoice"/>, once a choice has been made or restored here (it can't
	/// be taken back, R7), or at a stop Ryo has left. <see cref="Healed"/> is not saved state and reads 0 after this.
	/// </summary>
	public void Restore(DojoChoice choice)
	{
		if (!Enum.IsDefined(choice)) throw new ArgumentException($"dojo '{NodeId}': unknown choice {(int)choice}", nameof(choice));
		if (!IsOpen) throw new InvalidOperationException($"dojo '{NodeId}': Ryo is no longer at this stop (R7)");
		if (Choice != DojoChoice.None) throw new InvalidOperationException($"dojo '{NodeId}': the choice is already made ({Choice}) (R7)");
		Choice = choice;
		Healed = 0;
	}
}
