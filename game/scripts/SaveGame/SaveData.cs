using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using YokaiFighters.Sim;

namespace YokaiFighters.SaveGame;

/// <summary>T1: the Tanuki's locked copy as saved: slot, special id and level (a <see cref="CopyTarget"/> by id).</summary>
public sealed record LockedCopySave(string Slot, string Special, int Level);

/// <summary>One slot of the saved run: the special by id, its level (A3), its modifier by id (A2) and its T3 level-up rank.</summary>
public sealed record SlotSave(string Slot, string Special, int Level, string? Modifier, int Levelled)
{
	/// <summary>R6: bought at the merchant, borrowed ink (version 3; false when the file has none).</summary>
	public bool Borrowed { get; init; }
	/// <summary>R6: the modifier was bought at the merchant, borrowed ink (version 3; false when the file has none).</summary>
	public bool ModifierBorrowed { get; init; }
}

/// <summary>
/// YOK-87: a run in progress as plain data (ids and numbers only), written at a node boundary: the run's seed,
/// its walk on the map (<see cref="RunMapSnapshot"/>), Ryo's health (C1) and slots A-D. <see cref="Capture"/> takes
/// it from a <see cref="RunSession"/>, <see cref="Restore"/> builds an equal session (same
/// <see cref="RunSession.StateHash"/>) from the ability pool and the map of this build.
/// <para>
/// Adding run state later (coins, a locked copy target, a used merchant): add a property with a default here,
/// read it in <c>SaveStore</c> with that default when the field is missing, write it, and raise
/// <see cref="SaveStore.CurrentVersion"/>. Older files load unchanged through the defaults; the raised version is
/// what makes an older build back the file up before it writes one without the new field.
/// </para>
/// </summary>
public sealed record RunSave(
	uint Seed, string MapId, IReadOnlyList<string> Visited, string? Current, int Health, IReadOnlyList<SlotSave> Slots)
{
	/// <summary>The file in data/map/ the run walks (null = the build's default map).</summary>
	public string? MapFile { get; init; }
	/// <summary><see cref="MapGraph.Fingerprint"/> when saved (hex); null = not checked on restore.</summary>
	public string? MapFingerprint { get; init; }
	/// <summary>R8: the purse (version 2; 0 when the file has none). A negative value is not a run.</summary>
	public int Coins { get; init; }
	/// <summary>
	/// T1: whether the copy was locked when saved (version 2). Null = the file does not say (version 1): the lock is
	/// then worked out on restore when that can be exact, see <see cref="Restore"/>.
	/// </summary>
	public bool? CopyLocked { get; init; }
	/// <summary>T1: the locked copy; null before the lock, or a lock that found no special.</summary>
	public LockedCopySave? LockedCopy { get; init; }
	/// <summary>
	/// YOK-128, R8/R3: the entered node's fight was won and paid when saved (<see cref="RunSession.NodePaid"/>), so
	/// its coins and heal are already in <see cref="Coins"/> and <see cref="Health"/> (version 4; false when missing).
	/// </summary>
	public bool NodePaid { get; init; }

	public static string Hex(ulong value) => value.ToString("x16", CultureInfo.InvariantCulture);

	/// <summary>
	/// True for a bare <c>.json</c> file name. <see cref="MapFile"/> comes from the save file, so it may only name a
	/// file inside data/map/: no directory part, no rooted path, no <c>..</c>.
	/// </summary>
	public static bool IsMapFileName(string? name) =>
		!string.IsNullOrWhiteSpace(name) && name == name.Trim() && name.IndexOfAny(new[] { '/', '\\', ':' }) < 0
		&& System.IO.Path.GetFileName(name) == name && name.Length > ".json".Length
		&& name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && !name.StartsWith('.');

	/// <summary>
	/// The session as it stands. Call it only at a node boundary (run started, node entered, node completed):
	/// a fight in progress or an unpicked reward is not part of the file, so a run saved inside a node comes
	/// back at that node's start. Refused for a run that is over or has an offer open.
	/// </summary>
	public static RunSave Capture(RunSession session, string? mapFile = null)
	{
		if (session.IsOver) throw new InvalidOperationException("the run is over: there is nothing to continue");
		if (session.Draft is not null) throw new InvalidOperationException("a reward offer is open: save at a node boundary");
		var snap = session.Map.Snapshot();
		var slots = new List<SlotSave>();
		foreach (var (slot, s) in session.Run.Owned)
			slots.Add(new SlotSave(slot.ToString(), s.Data.Id, s.Level, s.Modifier?.Id, session.Run.LevelledOrder(slot)) { Borrowed = s.Borrowed, ModifierBorrowed = s.ModifierBorrowed });
		return new RunSave(session.Seed, snap.MapId, snap.Visited.ToArray(), snap.Current, session.Run.Health, slots)
		{
			MapFile = mapFile,
			MapFingerprint = Hex(session.Map.Graph.Fingerprint),
			Coins = session.Run.Coins,
			CopyLocked = session.CopyLocked,
			LockedCopy = session.LockedCopy is { } c ? new LockedCopySave(c.Slot.ToString(), c.Data.Id, c.Level) : null,
			NodePaid = session.NodePaid && session.Draft is null, // A7: a draft node is never saved paid
		};
	}

	static SpecialSlot ParseSlot(string name) => name switch
	{
		"A" => SpecialSlot.A,
		"B" => SpecialSlot.B,
		"C" => SpecialSlot.C,
		"D" => SpecialSlot.D,
		_ => throw new FormatException($"'{name}' is not a slot (A, B, C or D)"),
	};

	/// <summary>
	/// The saved run on this build's data. Throws <see cref="FormatException"/> naming the reason when the file
	/// describes a run this build cannot hold: another map (id or fingerprint), a special or modifier that is not
	/// in the pool, a slot, level or modifier the rules do not allow, no special at all (A1, T5), a walk the map
	/// does not allow, a walk already at the end of the map (that run is over), negative coins, or a copy lock
	/// that disagrees with the walk or names a copy the run cannot hold (T1).
	/// <paramref name="notes"/> receives anything repaired (only the T3 level-up order can be) or worked out.
	/// <para>
	/// A file without the copy lock (version 1) whose walk is past the lock point: the lock is worked out from the
	/// restored slots (<see cref="CopyRule.Target"/>) only when the run was saved on entering the Tanuki's node,
	/// the only save point past the lock where the slots are still those of the entry, and the level-up order
	/// was read as saved. Otherwise the run is refused: the copy cannot be known exactly.
	/// </para>
	/// </summary>
	public RunSession Restore(AbilityPool pool, MapGraph graph, INodeSceneResolver scenes, List<string>? notes = null)
	{
		if (graph.Id != MapId) throw new FormatException($"the run was saved on map '{MapId}', this is '{graph.Id}'");
		if (MapFingerprint is { } print && !string.Equals(print, Hex(graph.Fingerprint), StringComparison.OrdinalIgnoreCase))
			throw new FormatException($"map '{MapId}' has changed since the run was saved");
		if (Health < 1 || Health > RunState.MaxHealth)
			throw new FormatException($"health {Health} is not a run in progress (1-{RunState.MaxHealth})");

		var owned = new List<(SpecialSlot, OwnedSpecial)>();
		var order = new int[5];
		foreach (SlotSave s in Slots)
		{
			SpecialSlot slot = ParseSlot(s.Slot);
			SpecialData data = pool.Special(s.Special) ?? throw new FormatException($"special '{s.Special}' is not in this build's abilities");
			ModifierData? modifier = null;
			if (s.Modifier is { } id) modifier = pool.Modifier(id) ?? throw new FormatException($"modifier '{id}' is not in this build's abilities");
			owned.Add((slot, new OwnedSpecial(data, s.Level, modifier, s.Borrowed, s.ModifierBorrowed)));
			order[(int)slot] = s.Levelled;
		}

		// A1: a run starts with its starters; T5: the merchant never releases the last special, so the copy target is always defined.
		if (owned.Count == 0) throw new FormatException("no slot holds a special: a run always owns at least one (A1, T5)");

		if (Coins < 0) throw new FormatException($"coins {Coins}: a purse can't be negative (R8)");
		if (NodePaid && Current is null) throw new FormatException("a paid node with no node entered (R8)");
		if (CopyLocked is null && LockedCopy is not null) throw new FormatException("a locked copy without the copy lock (T1)");
		if (CopyLocked == false && LockedCopy is not null) throw new FormatException("a locked copy while the copy is not locked (T1)");

		RunState run;
		try { run = RunState.Restore(Health, owned); }
		catch (ArgumentException e) { throw new FormatException(e.Message); }
		run.RestoreCoins(Coins);
		bool orderRepaired = false;
		try { run.RestoreLevelledOrder(order[1], order[2], order[3], order[4]); }
		catch (ArgumentException e)
		{
			// The order only breaks T3 ties. Without a usable one, fall back to T3's last tie-break (the lower
			// slot wins) by making lower slots the more recent, rather than refusing the whole run.
			int rank = 4;
			var fallback = new int[5];
			for (int i = 1; i <= 4; i++) fallback[i] = run[(SpecialSlot)i] is { Level: > 1 } ? rank-- : 0;
			run.RestoreLevelledOrder(fallback[1], fallback[2], fallback[3], fallback[4]);
			notes?.Add($"level-up order not usable ({e.Message}): lower slots taken as more recent");
			orderRepaired = true;
		}
		RunMap map = RunMap.Restore(graph, new RunMapSnapshot(MapId, Visited, Current));
		// A finished walk is a won run: it has ended, there is nothing to pick and nothing to continue.
		if (map.IsFinished) throw new FormatException($"the saved walk is already at the end of map '{MapId}': that run is over");
		run.SetMap(map);

		// T1: the copy lock. Saved: put back as it was. Not in the file: see the remarks above.
		bool locked;
		CopyTarget? copy = null;
		if (CopyLocked is { } saved)
		{
			locked = saved;
			if (LockedCopy is { } c)
			{
				SpecialData data = pool.Special(c.Special) ?? throw new FormatException($"locked copy '{c.Special}' is not in this build's abilities (T1)");
				copy = new CopyTarget(ParseSlot(c.Slot), data, c.Level);
			}
			// The Tanuki's node is the only save point past the lock (a completed one is a finished walk, refused
			// above), and nothing changes the slots between the lock and that save. So the saved copy must be the one
			// the copy rule gives on the restored slots (T2-T5); with a repaired level-up order that can't be checked.
			if (saved && PastLockPoint(map))
			{
				if (orderRepaired) throw new FormatException("the copy lock can't be checked: the level-up order had to be repaired (T3)");
				CopyTarget? expected = CopyRule.Target(run);
				bool same = expected is null ? copy is null
					: copy is not null && copy.Slot == expected.Slot && copy.Data.Id == expected.Data.Id && copy.Level == expected.Level;
				if (!same)
					throw new FormatException($"the locked copy {Describe(copy)} is not the one this run locks, {Describe(expected)} (T2-T5)");
			}
		}
		else if (!PastLockPoint(map)) locked = false;
		else if (map.InNode && map.CurrentNode!.Type == NodeTypeRegistry.Boss && !orderRepaired)
		{
			locked = true;
			copy = CopyRule.Target(run); // saved on entering the Tanuki's node: the slots are the ones it locked on
			notes?.Add($"copy lock not in the file: worked out from the slots at the Tanuki's node ({copy?.Data.Id ?? "no special"})");
		}
		else throw new FormatException("the walk is past the Tanuki's node but the file has no copy lock, and it can't be worked out exactly (T1)");

		try { return RunSession.Restore(run, pool, scenes, Seed, locked, copy, nodePaid: NodePaid); }
		catch (ArgumentException e) { throw new FormatException(e.Message); }
	}

	/// <summary>T1: has the walk entered a Tanuki's node (the current node or one already visited)?</summary>
	static bool PastLockPoint(RunMap map) => RunSession.PassedLockPoint(map);

	static string Describe(CopyTarget? c) => c is null ? "none" : $"{c.Slot}:{c.Data.Id}:Lv {c.Level}";
}

/// <summary>
/// M4: story progress, a set of named flags (one per one-time story card seen, S3) kept in its own section of
/// the file. Nothing here reads or writes unlocks, and the file's reader takes the two sections separately, so
/// losing or resetting one never changes the other.
/// </summary>
public sealed record StoryProgress
{
	// S3's one-time cards. The card flow that shows them sets the flag; ids are free-form, so later cards need no format change.
	public const string Intro = "intro";
	public const string FirstLoss = "first-loss";
	public const string Reveal = "reveal";
	public const string Win1 = "win-1";
	public const string Win2 = "win-2";
	public const string Win3 = "win-3";

	public static readonly StoryProgress None = new(Array.Empty<string>());

	/// <summary>Flags set, distinct, in ordinal order (so the same progress always writes the same file).</summary>
	public IReadOnlyList<string> Seen { get; }

	public StoryProgress(IEnumerable<string> seen) =>
		Seen = seen.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.Ordinal).OrderBy(s => s, StringComparer.Ordinal).ToArray();

	public bool Has(string flag) => Seen.Contains(flag, StringComparer.Ordinal);
	public StoryProgress With(string flag) => Has(flag) ? this : new StoryProgress(Seen.Append(flag));

	public bool Equals(StoryProgress? other) => other is not null && Seen.SequenceEqual(other.Seen, StringComparer.Ordinal);
	public override int GetHashCode() => Seen.Aggregate(17, (h, s) => unchecked(h * 31 + StringComparer.Ordinal.GetHashCode(s)));
}

/// <summary>
/// YOK-87: everything the save file holds. Three independent sections: the run in progress (null = none), the
/// unlocked ability ids in the order they were awarded (M1; nothing awards them yet) and story progress (M4).
/// Immutable: every change is a new value written whole by <see cref="SaveStore.Save"/>.
/// </summary>
public sealed record SaveData
{
	public static readonly SaveData Empty = new();

	public RunSave? Run { get; init; }
	public IReadOnlyList<string> Unlocks { get; init; } = Array.Empty<string>();
	public StoryProgress Story { get; init; } = StoryProgress.None;

	public SaveData WithRun(RunSave? run) => this with { Run = run };
	/// <summary>M1: unlocks keep their award order; one already held is not added twice. Story is untouched (M4).</summary>
	public SaveData WithUnlock(string abilityId) =>
		Unlocks.Contains(abilityId, StringComparer.Ordinal) ? this : this with { Unlocks = Unlocks.Append(abilityId).ToArray() };
	/// <summary>M4: sets one story flag. Unlocks and the run are untouched.</summary>
	public SaveData WithStory(string flag) => this with { Story = Story.With(flag) };
}
