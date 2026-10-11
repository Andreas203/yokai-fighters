using System;
using System.Collections.Generic;
using System.Linq;

namespace YokaiFighters.Sim;

/// <summary>
/// YOK-94, T0: the Tanuki's kit = Ryo's normals, throw and air normals + his starters (A1, Lv 1) + the one special
/// it copied (T2-T4), on Ryo's model and clips (the presentation maps any fighter but the Kitsune to Ryo's set).
/// Pure data: built once at fight start from the copy target, never from Ryo's live fighter, so his modifiers and
/// evolution never reach it (T4).
/// </summary>
public sealed class TanukiKit
{
	/// <summary>The fighter id the node table gives the Tanuki (data/map/node-scenes.json <c>opponents.tanuki</c>).</summary>
	public const string Fighter = "tanuki";

	/// <summary>Ryo's normals, throw and air normals, the same objects as his kit (T0: no moves of its own).</summary>
	public MoveData[] Moves { get; }
	/// <summary>Ryo's starters (A1), each in its own slot at Lv 1.</summary>
	public SpecialData[] Starters { get; }
	/// <summary>The copied special (T2-T4), or null when the copy rule names none (no special in any slot).</summary>
	public CopyTarget? Copy { get; }
	/// <summary>The Tanuki's slot for the copy; None without a copy. See <see cref="SlotFor"/>.</summary>
	public SpecialSlot CopiedSlot { get; }
	/// <summary>What the kit was built from, for the log.</summary>
	public IReadOnlyList<string> Notes { get; }

	private TanukiKit(MoveData[] moves, SpecialData[] starters, CopyTarget? copy, List<string> notes)
	{
		Moves = moves; Starters = starters; Copy = copy; Notes = notes;
		CopiedSlot = copy is null ? SpecialSlot.None : SlotFor(copy, starters);
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
	/// Where the copy sits in the Tanuki's slots: Ryo's slot when the Tanuki's starters leave it free or the copy is
	/// that slot's starter (a levelled starter then replaces its Lv 1 self); otherwise (a drafted special that took
	/// a starter's slot on Ryo's side) the lowest free slot, so the Tanuki keeps both starters (T0) and the copy.
	/// </summary>
	public static SpecialSlot SlotFor(CopyTarget copy, IReadOnlyCollection<SpecialData> starters)
	{
		var held = new SpecialData?[5];
		foreach (var s in starters) if (s.Slot != SpecialSlot.None) held[(int)s.Slot] ??= s;
		if (held[(int)copy.Slot] is not { } there || there.Id == copy.Data.Id) return copy.Slot;
		for (int i = 1; i <= 4; i++) if (held[i] is null) return (SpecialSlot)i;
		return copy.Slot; // every slot holds a starter: the copy takes its own (A1 has two starters, so not reached)
	}

	/// <summary>
	/// Equips the Tanuki's fighter: starters at Lv 1 in their slots (A1), then the copy at <see cref="CopyTarget.MoveLevel"/>
	/// with no modifier (T4), and marks its slot for <c>special:copied</c>.
	/// </summary>
	public void Equip(Fighter f)
	{
		Kit.EquipSpecials(f, Starters);
		f.CopiedSlot = CopiedSlot;
		if (Copy is null) return;
		f.Equip(CopiedSlot, Copy.Data, Copy.MoveLevel);
	}
}
