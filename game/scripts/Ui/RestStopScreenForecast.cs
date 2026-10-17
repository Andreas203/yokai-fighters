using System;
using YokaiFighters.Sim;

namespace YokaiFighters.Ui;

/// <summary>
/// T6: what the rest stop tells Ryo about the Tanuki's copy: the move, the level it is copied at, and what it is
/// expected to cost him. Read-only on the run: before the T1 lock the target is <see cref="CopyRule.Target"/> (the
/// current best special), after it <see cref="RunSession.LockedCopy"/>. Pure C# so a test can name it without a scene.
/// </summary>
/// <param name="Level">The level the Tanuki's copy runs at (T4: never the Lv 3 evolution), what the player is told.</param>
/// <param name="RyoLevel">Ryo's own level of the move; above <paramref name="Level"/> only for an evolved special.</param>
/// <param name="Locked">True once the T1 lock has been taken: the target will not change any more.</param>
/// <param name="Cost">Expected health cost to Ryo, null while there is nothing to estimate from.</param>
/// <param name="CostIsPlaceholder">True while <see cref="ExpectedCost"/> is still the stand-in: the screen says so.</param>
public sealed record RestStopScreenForecast(string SpecialId, string Name, SpecialSlot Slot, int Level, int RyoLevel, bool Locked, int? Cost, bool CostIsPlaceholder)
{
	/// <summary>The stand-in figure while no formula exists (designer question: the rules give none).</summary>
	public const int PlaceholderCost = 190;

	static readonly Func<CopyTarget, int?> Stand = _ => PlaceholderCost;
	static Func<CopyTarget, int?> _source = Stand;

	/// <summary>
	/// THE one place the expected cost comes from: "its expected cost from the player's own fights" (GDD 5.3, T6) has
	/// no formula in the rules. Replace this (or set <see cref="CostSource"/>) when the designer decides what it means;
	/// the screen shows whatever comes back and labels it a placeholder only while it is this stand-in.
	/// </summary>
	public static int? ExpectedCost(CopyTarget target) => _source(target);

	/// <summary>Test / later-ticket seam: swaps the estimate. Null restores the placeholder.</summary>
	public static Func<CopyTarget, int?>? CostSource
	{
		set => _source = value ?? Stand;
	}

	static bool IsStandIn => ReferenceEquals(_source, Stand);

	/// <summary>The forecast for the run's current moment; null when no special sits in a slot (T5 makes that unreachable in play).</summary>
	public static RestStopScreenForecast? For(RunSession session)
	{
		if (session is null) throw new ArgumentNullException(nameof(session));
		CopyTarget? t = session.CopyLocked ? session.LockedCopy : CopyRule.Target(session.Run);
		if (t is null) return null;
		return new RestStopScreenForecast(t.Data.Id, t.Data.Name.Length > 0 ? t.Data.Name : t.Data.Id, t.Slot, t.MoveLevel, t.Level, session.CopyLocked, ExpectedCost(t), IsStandIn);
	}
}
