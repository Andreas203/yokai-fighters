using System;
using System.Collections.Generic;
using System.Linq;

namespace YokaiFighters.Sim;

/// <summary>
/// YOK-94, T0: the Tanuki's kit = Ryo's normals, throw and air normals + his starters (A1, Lv 1) + the one special
/// it copied (T2-T4, in Ryo's slot for it, replacing the starter there), on Ryo's model and clips (the presentation maps any fighter but the Kitsune to Ryo's set).
/// Pure data: built once at fight start from the copy target, never from Ryo's live fighter, so his modifiers and
/// evolution never reach it (T4).
/// </summary>
public sealed class TanukiKit
{
	/// <summary>
	/// The kit kind an opponent names in data/map/node-scenes.json (<c>opponents.&lt;yokai&gt;.kit</c>): which opponent
	/// fights with this kit is data, never a fighter or yokai id in code.
	/// </summary>
	public const string KitId = "copy_of_ryo";

	/// <summary>Ryo's normals, throw and air normals, the same objects as his kit (T0: no moves of its own).</summary>
	public MoveData[] Moves { get; }
	/// <summary>
	/// Ryo's starters (A1) the Tanuki holds, each in its own slot at Lv 1: all of them, except the one whose slot the
	/// copy takes (designer ruling 2026-10-11: that slot mirrors Ryo's own, so a starter Ryo replaced there is gone
	/// on the Tanuki's side too, and a levelled starter replaces its Lv 1 self).
	/// </summary>
	public SpecialData[] Starters { get; }
	/// <summary>The copied special (T2-T4), or null when the copy rule names none (no special in any slot).</summary>
	public CopyTarget? Copy { get; }
	/// <summary>The Tanuki's slot for the copy = Ryo's slot for it (<see cref="CopyTarget.Slot"/>); None without a copy.</summary>
	public SpecialSlot CopiedSlot { get; }
	/// <summary>What the kit was built from, for the log.</summary>
	public IReadOnlyList<string> Notes { get; }

	private TanukiKit(MoveData[] moves, SpecialData[] starters, CopyTarget? copy, List<string> notes)
	{
		Moves = moves; Copy = copy; Notes = notes;
		CopiedSlot = copy?.Slot ?? SpecialSlot.None;
		Starters = starters.Where(s => s.Slot != CopiedSlot).ToArray();
	}

	/// <summary>The kit from Ryo's own kit (<see cref="Kit.Load"/> of <see cref="Kit.Ryo"/>) and a copy target.</summary>
	public static TanukiKit Build(Kit ryo, CopyTarget? copy)
	{
		if (ryo is null) throw new ArgumentNullException(nameof(ryo));
		if (ryo.Fighter != Kit.Ryo) throw new ArgumentException($"the Tanuki fights with Ryo's kit, not {ryo.Fighter}'s (T0)", nameof(ryo));
		var notes = ryo.Notes.Select(n => "tanuki (Ryo's kit): " + n).ToList();
		notes.Add(copy is null
			? "tanuki: no copied special (no special in any of Ryo's slots); normals, throw and starters only"
			: $"tanuki: copies {copy.Data.Id} from slot {copy.Slot} at Ryo's Lv {copy.Level} (runs Lv {copy.MoveLevel}, no modifier, T4)");
		return new TanukiKit(ryo.Moves, ryo.Specials.ToArray(), copy, notes);
	}

	public static TanukiKit Load(KitSources src, CopyTarget? copy) => Build(Kit.Load(Kit.Ryo, src), copy);

	/// <summary>
	/// The copy the fight uses. Once the copy is locked (T1) it is the locked target, null included (no special was
	/// in a slot), and the slots now are never read again. Before any lock (a fight not started through the boss
	/// node: a test, a debug fight) the copy rule answers from the run as it stands (T2-T4), and with no run at all
	/// from a new run's slots: the starters at Lv 1, so T3's lower slot (Spirit Wave). Never throws on a missing run.
	/// </summary>
	public static CopyTarget? CopyAtFightStart(bool copyLocked, CopyTarget? lockedCopy, RunState? run, IEnumerable<SpecialData> starters)
	{
		if (copyLocked) return lockedCopy;
		if (run is not null) return CopyRule.Target(run);
		SpecialData? first = null;
		foreach (var s in starters)
			if (s.Slot != SpecialSlot.None && (first is null || s.Slot < first.Slot)) first = s;
		return first is null ? null : new CopyTarget(first.Slot, first, 1);
	}

	/// <summary>
	/// Equips the Tanuki's fighter: the kept starters at Lv 1 in their slots (A1), then the copy in Ryo's slot for it at
	/// <see cref="CopyTarget.MoveLevel"/> with no modifier (T4), and marks that slot for <c>special:copied</c>.
	/// </summary>
	public void Equip(Fighter f)
	{
		Kit.EquipSpecials(f, Starters);
		f.CopiedSlot = CopiedSlot;
		if (Copy is null) return;
		f.Equip(CopiedSlot, Copy.Data, Copy.MoveLevel);
	}
}
