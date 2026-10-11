using System;

namespace YokaiFighters.Sim;

/// <summary>
/// T2-T4: what the Tanuki would copy at one moment of a run: the slot and special it takes from Ryo and
/// the level it takes it at. The copy never has a modifier and never the Lv 3 evolution (T4), so there
/// is no modifier member here and <see cref="Build"/> stops at the last level that is not an evolution.
/// </summary>
/// <param name="Slot">The slot the special sits in on Ryo's side (T2: only specials in a slot count).</param>
/// <param name="Data">The copied special's data: the move itself, shared with Ryo.</param>
/// <param name="Level">Ryo's level of the special at the moment of asking (T4: "at Ryo's level").</param>
public sealed record CopyTarget(SpecialSlot Slot, SpecialData Data, int Level)
{
	/// <summary>
	/// T4: the level whose frame data the copy runs. Ryo's level, except that Lv 3 is the evolution (A4)
	/// and the copy never gets it, so a copy of a Lv 3 special runs the Lv 2 tuning only.
	/// </summary>
	public int MoveLevel => Math.Min(Level, CopyRule.HighestUnevolvedLevel);

	/// <summary>
	/// T4: the copy's move, built from data at <see cref="MoveLevel"/> with no modifier. Ryo's own version
	/// is <c>Data.Build(Level, ex, hisModifier, config)</c>; this one never sees his modifier or evolution.
	/// </summary>
	public MoveData Build(bool ex = false) => Data.Build(MoveLevel, ex, null, null);
}

/// <summary>
/// T2-T4, GDD 5.3: the Tanuki's copy target, answered from the run state alone (pure C#, deterministic,
/// reads only). Nothing here locks the target (T1) or builds the Tanuki's fighter (T0).
/// </summary>
public static class CopyRule
{
	/// <summary>T4 with A4: Lv 3 is the evolution, so Lv 2 is the highest level a copy's frame data reaches.</summary>
	public const int HighestUnevolvedLevel = 2;

	/// <summary>
	/// T2: the highest-level special currently in a slot, whatever kind of move it is (throws included; there
	/// is no filter). A sold or replaced special is no longer in a slot, so it can't be chosen.
	/// T3: among equal levels the one levelled most recently (<see cref="RunState.LevelledOrder"/>), then the
	/// lower slot. Null only when no slot holds a special.
	/// </summary>
	public static CopyTarget? Target(RunState run)
	{
		if (run is null) throw new ArgumentNullException(nameof(run));
		OwnedSpecial? best = null;
		SpecialSlot bestSlot = SpecialSlot.None;
		int bestOrder = 0;
		// Owned walks slots A-D upwards and every comparison is strict, so a full tie keeps the lower slot (T3).
		foreach (var (slot, s) in run.Owned)
		{
			int order = run.LevelledOrder(slot);
			bool better = best is null
				|| s.Level > best.Level                            // T2: highest level
				|| (s.Level == best.Level && order > bestOrder);   // T3: most recently levelled
			if (!better) continue;
			best = s;
			bestSlot = slot;
			bestOrder = order;
		}
		return best is null ? null : new CopyTarget(bestSlot, best.Data, best.Level);
	}
}
