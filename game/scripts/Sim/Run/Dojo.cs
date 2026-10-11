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
/// </para>
/// </summary>
public sealed class Dojo
{
	/// <summary>The map node type this stop belongs to (<see cref="NodeTypeRegistry.Default"/>).</summary>
	public const string NodeType = "dojo";
	public const int RestHealPercent = 30; // R7

	/// <summary>R7: what a rest heals before the cap: 30% of full health (C1: 1,000 → 300), to the nearest whole number (E20).</summary>
	public static int RestHeal => (RunState.MaxHealth * RestHealPercent + 50) / 100;

	readonly RunState _run;

	public string NodeId { get; }
	public DojoChoice Choice { get; private set; }
	/// <summary>Health the choice actually gave back (0 until a choice is made; less than <see cref="RestHeal"/> near full health).</summary>
	public int Healed { get; private set; }
	/// <summary>R7: the stop's one choice is still open.</summary>
	public bool CanRest => Choice == DojoChoice.None;

	public Dojo(RunState run, string nodeId)
	{
		_run = run ?? throw new ArgumentNullException(nameof(run));
		NodeId = nodeId ?? throw new ArgumentNullException(nameof(nodeId));
	}

	/// <summary>
	/// R7: Ryo rests: health + <see cref="RestHeal"/>, capped at full. It is the stop's one choice, so a second
	/// rest (or a rest after another choice) is refused and changes nothing.
	/// </summary>
	public MapResult Rest()
	{
		if (!CanRest) return MapResult.Fail($"dojo '{NodeId}': the choice is already made ({Choice}) (R7)");
		int before = _run.Health;
		_run.SetHealth(before + RestHeal); // SetHealth caps at full health
		Healed = _run.Health - before;
		Choice = DojoChoice.Rest;
		return MapResult.Done;
	}

	/// <summary>
	/// Puts back the choice of a saved stop without healing again. Throws, changing nothing, for a value that is
	/// not a <see cref="DojoChoice"/>. <see cref="Healed"/> is not saved state and reads 0 after this.
	/// </summary>
	public void Restore(DojoChoice choice)
	{
		if (!Enum.IsDefined(choice)) throw new ArgumentException($"dojo '{NodeId}': unknown choice {(int)choice}", nameof(choice));
		Choice = choice;
		Healed = 0;
	}
}
