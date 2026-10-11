using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using YokaiFighters.Sim;

namespace YokaiFighters.SaveGame;

/// <summary>One slot of the saved run: the special by id, its level (A3), its modifier by id (A2) and its T3 level-up rank.</summary>
public sealed record SlotSave(string Slot, string Special, int Level, string? Modifier, int Levelled);

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
			slots.Add(new SlotSave(slot.ToString(), s.Data.Id, s.Level, s.Modifier?.Id, session.Run.LevelledOrder(slot)));
		return new RunSave(session.Seed, snap.MapId, snap.Visited.ToArray(), snap.Current, session.Run.Health, slots)
		{
			MapFile = mapFile,
			MapFingerprint = Hex(session.Map.Graph.Fingerprint),
		};
	}

	/// <summary>
	/// The saved run on this build's data. Throws <see cref="FormatException"/> naming the reason when the file
	/// describes a run this build cannot hold: another map (id or fingerprint), a special or modifier that is not
	/// in the pool, a slot, level or modifier the rules do not allow, no special at all (A1, T5), a walk the map
	/// does not allow, or a walk already at the end of the map (that run is over).
	/// <paramref name="notes"/> receives anything repaired (only the T3 level-up order can be).
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
			SpecialSlot slot = s.Slot switch
			{
				"A" => SpecialSlot.A,
				"B" => SpecialSlot.B,
				"C" => SpecialSlot.C,
				"D" => SpecialSlot.D,
				_ => throw new FormatException($"'{s.Slot}' is not a slot (A, B, C or D)"),
			};
			SpecialData data = pool.Special(s.Special) ?? throw new FormatException($"special '{s.Special}' is not in this build's abilities");
			ModifierData? modifier = null;
			if (s.Modifier is { } id) modifier = pool.Modifier(id) ?? throw new FormatException($"modifier '{id}' is not in this build's abilities");
			owned.Add((slot, new OwnedSpecial(data, s.Level, modifier)));
			order[(int)slot] = s.Levelled;
		}

		// A1: a run starts with its starters; T5: the merchant never releases the last special, so the copy target is always defined.
		if (owned.Count == 0) throw new FormatException("no slot holds a special: a run always owns at least one (A1, T5)");

		RunState run;
		try { run = RunState.Restore(Health, owned); }
		catch (ArgumentException e) { throw new FormatException(e.Message); }
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
		}
		RunMap map = RunMap.Restore(graph, new RunMapSnapshot(MapId, Visited, Current));
		// A finished walk is a won run: it has ended, there is nothing to pick and nothing to continue.
		if (map.IsFinished) throw new FormatException($"the saved walk is already at the end of map '{MapId}': that run is over");
		run.SetMap(map);
		return new RunSession(run, pool, scenes, Seed);
	}
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
