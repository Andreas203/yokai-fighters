using System;
using System.Collections.Generic;

namespace YokaiFighters.Sim;

/// <summary>
/// YOK-97, M3: the best special of a run that reached row 6 or further carries to the next run at Lv 2. Pure C#,
/// deterministic: the choice depends only on the ended run's slots, level-up order and the row it ended on.
/// <para>
/// "Best" (reading flagged for the designer): the T2/T3 ranking the copy rule uses (highest level, then most
/// recently levelled, then lower slot), over <see cref="RunState.CarryOverCandidates"/>, so borrowed ink never
/// carries over (R6) and is never "best" even when it is the copy target.
/// </para>
/// <para>
/// PROVISIONAL (not in M3, designer to confirm): the carried special comes back with no modifier and no evolution
/// (Lv 2 is below the Lv 3 evolution, A4); it is set to Lv 2 even when it ended the run at Lv 1 (capped at the
/// special's own top level); it takes its own slot (A3) and so replaces the starter there (A1); in the next run it
/// counts as levelled earliest for T3, the order a Lv 2 special needs.
/// </para>
/// </summary>
public static class CarryOver
{
	/// <summary>M3: the run must reach this row (lost on it, or anywhere past it, or won).</summary>
	public const int MinRow = 6;

	/// <summary>M3: the level the carried special starts the next run at.</summary>
	public const int Level = 2;

	/// <summary>M3: did the run that ended this way reach far enough to carry a special over?</summary>
	public static bool Qualifies(RunEnd end) =>
		(end ?? throw new ArgumentNullException(nameof(end))).RowReached >= MinRow;

	/// <summary>
	/// The best special Ryo may carry over from <paramref name="run"/> (borrowed ink left out, R6), by T2/T3: highest
	/// level, then most recently levelled, then the lower slot. Null when he holds only borrowed specials.
	/// </summary>
	public static OwnedSpecial? Best(RunState run)
	{
		if (run is null) throw new ArgumentNullException(nameof(run));
		OwnedSpecial? best = null;
		int bestOrder = 0;
		// Candidates walk slots A-D upwards and every comparison is strict, so a full tie keeps the lower slot (T3).
		foreach (var (slot, s) in run.CarryOverCandidates)
		{
			int order = run.LevelledOrder(slot);
			bool better = best is null
				|| s.Level > best.Level
				|| (s.Level == best.Level && order > bestOrder);
			if (!better) continue;
			best = s;
			bestOrder = order;
		}
		return best;
	}

	/// <summary>
	/// M3 at run end: the id of the special the next run carries, or null when the run ended before row 6 or Ryo
	/// owns nothing that may carry. The result replaces any earlier carry-over: only the last ended run counts.
	/// </summary>
	public static string? FromRunEnd(RunState run, RunEnd end) =>
		Qualifies(end) ? Best(run)?.Data.Id : null;

	/// <summary>
	/// A new run (A1 starters at Lv 1, full health) with <paramref name="carriedId"/> placed in its own slot at
	/// <see cref="Level"/> (capped at the special's top level). An id the pool does not know (the special was
	/// removed from the data) is dropped: the run starts plain and <paramref name="dropped"/> says why.
	/// </summary>
	public static RunState NewRun(AbilityPool pool, string? carriedId, MapGraph? map, out string? dropped)
	{
		if (pool is null) throw new ArgumentNullException(nameof(pool));
		dropped = null;
		var plain = RunState.NewRun(pool, map);
		if (carriedId is null) return plain;
		if (pool.Special(carriedId) is not { } data)
		{
			dropped = $"carry-over '{carriedId}' is not a special this build knows; the run starts without it";
			return plain;
		}
		if (data.Slot is < SpecialSlot.A or > SpecialSlot.D)
		{
			dropped = $"carry-over '{carriedId}' has no slot; the run starts without it";
			return plain;
		}
		int level = Math.Min(Level, Math.Min(3, data.MaxLevel));
		var slots = new List<(SpecialSlot, OwnedSpecial)>();
		foreach (var (slot, s) in plain.Owned)
			if (slot != data.Slot) slots.Add((slot, s));
		slots.Add((data.Slot, new OwnedSpecial(data, level, null)));
		var run = RunState.Restore(RunState.MaxHealth, slots);
		if (level > 1)
		{
			var order = new int[5];
			order[(int)data.Slot] = 1; // the only levelled special: earliest (T3)
			run.RestoreLevelledOrder(order[1], order[2], order[3], order[4]);
		}
		if (map is not null) run.StartMap(map);
		return run;
	}
}
