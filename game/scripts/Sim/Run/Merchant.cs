using System;

namespace YokaiFighters.Sim;

/// <summary>R6: what Ryo did with a merchant stop. One per stop: the free heal <b>or</b> the shop.</summary>
public enum MerchantChoice { None, Heal, Shop }

/// <summary>
/// YOK-83: one merchant stop (pure C#, deterministic, no Godot). R6 gives Ryo one choice here, the free heal or
/// the shop; this class records it (<see cref="Choice"/>) and gives the heal. The shop (YOK-95) spends the same
/// choice through <see cref="Choose"/> with <see cref="MerchantChoice.Shop"/>, so the two can never both happen.
/// <para>
/// A <see cref="RunSession"/> makes one when Ryo enters a <c>merchant</c> node (<see cref="RunSession.Merchant"/>)
/// and drops it when the node is completed. A stop only acts while it is the session's current one, so a
/// reference kept from an earlier stop, or from a run that has ended, does nothing.
/// </para>
/// </summary>
public sealed class Merchant
{
	public const int HealPercent = 25; // R6
	/// <summary>The free heal in health points: 25% of Ryo's full health (C1), so 250.</summary>
	public const int HealAmount = RunState.MaxHealth * HealPercent / 100;

	readonly RunSession _session;

	public string NodeId { get; }
	public MerchantChoice Choice { get; private set; }

	internal Merchant(RunSession session, string nodeId)
	{
		_session = session;
		NodeId = nodeId;
	}

	/// <summary>Ryo's health after the free heal from <paramref name="health"/>: +250, capped at full health (C1).</summary>
	public static int Healed(int health) =>
		Math.Min(RunState.MaxHealth, Math.Clamp(health, 0, RunState.MaxHealth) + HealAmount);

	/// <summary>This is the stop Ryo is standing at, in a run that is still going.</summary>
	public bool Open => !_session.IsOver && ReferenceEquals(_session.Merchant, this);

	/// <summary>The stop's one choice is still unspent (R6).</summary>
	public bool CanChoose => Open && Choice == MerchantChoice.None;

	/// <summary>The free heal can still be taken here. True at full health too: the choice is Ryo's to waste.</summary>
	public bool CanHeal => CanChoose;

	/// <summary>R6: takes the free heal as this stop's choice. Refused, changing nothing, once a choice is made.</summary>
	public MapResult TakeHeal() => Choose(MerchantChoice.Heal);

	/// <summary>
	/// Spends the stop's one choice (R6). <see cref="MerchantChoice.Heal"/> heals Ryo; <see cref="MerchantChoice.Shop"/>
	/// only records that he shopped (what the shop sells is the shop's own code). A refused call changes nothing.
	/// </summary>
	public MapResult Choose(MerchantChoice choice)
	{
		if (choice != MerchantChoice.Heal && choice != MerchantChoice.Shop)
			return MapResult.Fail($"merchant '{NodeId}': '{choice}' is not a choice (R6)");
		if (!Open) return MapResult.Fail($"merchant '{NodeId}': Ryo is not at this stop");
		if (Choice != MerchantChoice.None)
			return MapResult.Fail($"merchant '{NodeId}': the choice here is already made ({Choice}) (R6)");
		Choice = choice;
		if (choice == MerchantChoice.Heal) _session.Run.SetHealth(Healed(_session.Run.Health));
		return MapResult.Done;
	}

	/// <summary>
	/// Puts back the choice of a saved stop without giving the heal again (the saved health already has it).
	/// Refused once a choice is recorded here.
	/// </summary>
	public MapResult Restore(MerchantChoice choice)
	{
		if (!Enum.IsDefined(choice)) return MapResult.Fail($"merchant '{NodeId}': '{choice}' is not a choice (R6)");
		if (Choice != MerchantChoice.None)
			return MapResult.Fail($"merchant '{NodeId}': the choice here is already made ({Choice}) (R6)");
		Choice = choice;
		return MapResult.Done;
	}
}
