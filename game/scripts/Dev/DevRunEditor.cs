using System;
using System.Collections.Generic;
using System.Linq;
using YokaiFighters.Sim;

namespace YokaiFighters.Dev;

/// <summary>
/// YOK-126: one slot of a dev build. <paramref name="Special"/> is a special id from the data; <paramref name="Modifier"/>
/// a modifier id or null. <paramref name="LevelledOrder"/> is T3's level-up rank (1 = earliest, higher = more recent;
/// 0 = work it out: a levelled slot left at 0 counts as levelled after every slot given a rank, in slot order A-D).
/// <paramref name="Borrowed"/> / <paramref name="ModifierBorrowed"/>: merchant "borrowed ink" (R6), for shop edge cases.
/// </summary>
public sealed record DevSlotEdit(SpecialSlot Slot, string Special, int Level = 1, string? Modifier = null,
	int LevelledOrder = 0, bool Borrowed = false, bool ModifierBorrowed = false);

/// <summary>
/// YOK-126: a whole run state set by hand, as plain data (what a preset stores). <paramref name="Depth"/> = rows
/// completed on the map (0 = before row 1); <paramref name="Seed"/> = the session's reward-draft seed (node n uses Seed + n).
/// </summary>
public sealed record DevRunBuild(IReadOnlyList<DevSlotEdit> Slots, int Coins, int Health, int Depth, uint Seed)
{
	/// <summary>YOK-186: the cancel rule ids Ryo holds (X1, X2), kept through an edit; empty = none.</summary>
	public IReadOnlyList<string> CancelRules { get; init; } = Array.Empty<string>();

	public DevSlotEdit? this[SpecialSlot slot] => Slots.FirstOrDefault(s => s.Slot == slot);

	/// <summary>This build with <paramref name="slot"/> replaced by <paramref name="edit"/> (null empties it).</summary>
	public DevRunBuild WithSlot(SpecialSlot slot, DevSlotEdit? edit) =>
		this with { Slots = Slots.Where(s => s.Slot != slot).Concat(edit is null ? Array.Empty<DevSlotEdit>() : new[] { edit with { Slot = slot } })
			.OrderBy(s => s.Slot).ToArray() };
}

/// <summary>A named build (a preset).</summary>
public sealed record DevPreset(string Name, DevRunBuild Build, bool BuiltIn = false);

/// <summary>
/// YOK-126: the answer for one build. <see cref="Ok"/>: <see cref="Session"/> is the run it makes (not yet adopted) and
/// <see cref="Copy"/> the Tanuki's copy target on it (<see cref="CopyRule.Target"/>, T2-T4). Otherwise
/// <see cref="Refusal"/> says why the sim refuses it and nothing else is set.
/// </summary>
public sealed record DevRunCheck(RunSession? Session, CopyTarget? Copy, string? Refusal)
{
	public bool Ok => Refusal is null;
}

/// <summary>
/// YOK-126: the logic behind the dev menu's Run state page (no Godot, no file, no randomness). A build is turned into a
/// fresh <see cref="RunSession"/> through the sim's own restore path, the one a saved run comes back through, so every
/// rule it checks applies: the special exists as data and sits in its own slot (A3), a level it has (A3/A4, Lv 3 = the
/// evolution), one modifier the special meets (A2), no borrowed starter (R6, A1), a coherent T3 level-up order, coins
/// 0 or more (R8), a walk the map allows, and nothing locked before the Tanuki's node (T1). It also refuses a build
/// with no special (T5: the copy target is always defined) and health outside 1-1,000 (C1). The page only adopts a
/// session this class built, through <see cref="DevContext.TryChangeRun"/> (never mid-fight).
/// </summary>
public sealed class DevRunEditor
{
	public const string NoSpecialReason = "Ryo must keep at least one special (T5: the copy target is always defined).";

	public AbilityPool Pool { get; }
	public MapGraph Graph { get; }
	public INodeSceneResolver Scenes { get; }
	/// <summary>The walk a new depth starts from (one node id per row); the first route is taken past its end.</summary>
	public IReadOnlyList<string> PreferredWalk { get; }

	public DevRunEditor(AbilityPool pool, MapGraph graph, INodeSceneResolver scenes, IReadOnlyList<string>? preferredWalk = null)
	{
		Pool = pool ?? throw new ArgumentNullException(nameof(pool));
		Graph = graph ?? throw new ArgumentNullException(nameof(graph));
		Scenes = scenes ?? throw new ArgumentNullException(nameof(scenes));
		PreferredWalk = preferredWalk ?? Array.Empty<string>();
	}

	/// <summary>An editor over <paramref name="session"/>'s pool, map and node table, keeping its walk where a depth allows.</summary>
	public static DevRunEditor For(RunSession session) =>
		new(session.Pool, session.Map.Graph, session.Scenes, session.Map.Visited.ToArray());

	/// <summary>The build <paramref name="session"/> stands on now (its slots, levels, modifiers, level-up order, coins, health, depth, seed).</summary>
	public static DevRunBuild Capture(RunSession session)
	{
		var run = session.Run;
		var slots = run.Owned.Select(o => new DevSlotEdit(o.Slot, o.Special.Data.Id, o.Special.Level, o.Special.Modifier?.Id,
			run.LevelledOrder(o.Slot), o.Special.Borrowed, o.Special.ModifierBorrowed)).ToArray();
		return new DevRunBuild(slots, run.Coins, run.Health, session.Map.Visited.Count, session.Seed)
			{ CancelRules = run.CancelRules.Select(c => c.Id).ToArray() }; // YOK-186
	}

	/// <summary>Specials that exist as data for <paramref name="slot"/> (A3: a special goes in its own slot), pool order.</summary>
	public IEnumerable<SpecialData> SpecialsFor(SpecialSlot slot) => Pool.Specials.Where(s => s.Slot == slot);

	/// <summary>Modifiers that can attach to <paramref name="specialId"/> (A2), pool order.</summary>
	public IEnumerable<ModifierData> ModifiersFor(string specialId) =>
		Pool.Special(specialId) is { } s ? Pool.Modifiers.Where(m => m.AppliesTo(s)) : Enumerable.Empty<ModifierData>();

	/// <summary>Builds the run <paramref name="build"/> describes, or says why the sim refuses it. Changes nothing anywhere.</summary>
	public DevRunCheck Check(DevRunBuild build)
	{
		if (build is null) return Refuse("no build");
		if (build.Slots.Count == 0) return Refuse(NoSpecialReason);
		if (build.Health < 1 || build.Health > RunState.MaxHealth)
			return Refuse($"Health {build.Health} is outside 1-{RunState.MaxHealth} (C1: Ryo starts the next fight on it).");
		if (build.Coins < 0) return Refuse($"Coins can't be negative ({build.Coins}, R8).");

		var owned = new List<(SpecialSlot, OwnedSpecial)>();
		foreach (var e in build.Slots)
		{
			if (Pool.Special(e.Special) is not { } data) return Refuse($"Slot {e.Slot}: no special '{e.Special}' in the data.");
			ModifierData? mod = null;
			if (!string.IsNullOrEmpty(e.Modifier) && (mod = Pool.Modifier(e.Modifier)) is null)
				return Refuse($"Slot {e.Slot}: no modifier '{e.Modifier}' in the data.");
			owned.Add((e.Slot, new OwnedSpecial(data, e.Level, mod, e.Borrowed, e.ModifierBorrowed)));
		}

		RunState run;
		try
		{
			run = RunState.Restore(build.Health, owned); // A2, A3/A4, R6: the saved-run path
			run.RestoreLevelledOrder(Orders(build, run, SpecialSlot.A), Orders(build, run, SpecialSlot.B),
				Orders(build, run, SpecialSlot.C), Orders(build, run, SpecialSlot.D)); // T3
			run.RestoreCoins(build.Coins); // R8
			var cancels = new List<CancelRuleData>(); // YOK-186, X1/X2: as the saved-run path
			foreach (string id in build.CancelRules)
				if (Pool.CancelRule(id) is { } c) cancels.Add(c); else return Refuse($"No cancel rule '{id}' in the data.");
			run.RestoreCancelRules(cancels);
		}
		catch (ArgumentException e) { return Refuse(Sentence(e.Message)); }
		if (run.SpecialCount == 0) return Refuse(NoSpecialReason);

		if (!TryWalk(build.Depth, out var walk, out string? why)) return Refuse(why!);
		try
		{
			run.SetMap(RunMap.Restore(Graph, new RunMapSnapshot(Graph.Id, walk, walk.Count > 0 ? walk[^1] : null)));
			var session = new RunSession(run, Pool, Scenes, build.Seed); // T1: refuses a walk past the lock point
			return new DevRunCheck(session, CopyRule.Target(run), null);
		}
		catch (Exception e) when (e is ArgumentException or FormatException) { return Refuse(Sentence(e.Message)); }
	}

	/// <summary>The deepest depth a build can take: rows completed before the first node of the Tanuki's type on the walk (T1).</summary>
	public int MaxDepth
	{
		get
		{
			int d = 0;
			while (TryWalk(d + 1, out _, out _)) d++;
			return d;
		}
	}

	/// <summary>
	/// The walk for <paramref name="depth"/> rows completed: <see cref="PreferredWalk"/> while it fits, then the first
	/// route. Refused past the last row, onto a node of the Tanuki's type (its copy locks on entry, T1: walk there on
	/// the map) or onto the end of the map (the run would be over).
	/// </summary>
	public bool TryWalk(int depth, out List<string> walk, out string? refusal)
	{
		walk = new List<string>();
		refusal = null;
		if (depth < 0) { refusal = $"Depth {depth} can't be negative."; return false; }
		IReadOnlyList<string> next = Graph.StartNodes;
		for (int i = 0; i < depth; i++)
		{
			if (next.Count == 0) { refusal = $"Depth {depth}: the map has only {i} rows to complete."; return false; }
			string pick = i < PreferredWalk.Count && next.Contains(PreferredWalk[i]) ? PreferredWalk[i] : next[0];
			if (Graph.Node(pick).Type == NodeTypeRegistry.Boss)
			{
				refusal = $"Depth {depth} would pass the Tanuki's node (row {Graph.Node(pick).Row}): its copy locks on entry (T1); walk there on the map.";
				return false;
			}
			walk.Add(pick);
			next = Graph.Next(pick);
		}
		if (depth > 0 && next.Count == 0) { refusal = $"Depth {depth} completes the map: the run would be over."; return false; }
		return true;
	}

	/// <summary>
	/// Built-in presets on this pool: "all starters Lv1" (A1, a new run) and "Lv3 &lt;special&gt; evolved" = the starters at
	/// Lv 1 plus the first drafted special in the data (pool order) that has a Lv 3 evolution (A4), when there is one
	/// (Foxfire on today's data). Chosen by data, never by id. Both keep <paramref name="seed"/>.
	/// </summary>
	public IReadOnlyList<DevPreset> BuiltInPresets(uint seed)
	{
		var starters = Pool.Starters.Select(s => new DevSlotEdit(s.Slot, s.Id)).GroupBy(e => e.Slot).Select(g => g.First()).ToArray();
		var list = new List<DevPreset> { new("all starters Lv1", new DevRunBuild(starters, 0, RunState.MaxHealth, 0, seed), true) };
		if (Pool.Specials.FirstOrDefault(s => !s.Starter && s.MaxLevel >= 3 && s.Slot is >= SpecialSlot.A and <= SpecialSlot.D) is { } evo)
		{
			var b = new DevRunBuild(starters, 0, RunState.MaxHealth, 0, seed).WithSlot(evo.Slot, new DevSlotEdit(evo.Slot, evo.Id, 3));
			list.Add(new DevPreset($"Lv3 {(evo.Name.Length > 0 ? evo.Name : evo.Id)} evolved", b, true));
		}
		return list;
	}

	/// <summary>T3 ranks for one slot: as given, or (0 on a levelled slot) after every given rank, in slot order.</summary>
	static int Orders(DevRunBuild build, RunState run, SpecialSlot slot)
	{
		var e = build[slot];
		if (e is null) return 0;
		if (e.LevelledOrder != 0 || e.Level <= 1) return e.LevelledOrder;
		int top = build.Slots.Max(s => Math.Max(0, s.LevelledOrder));
		int auto = build.Slots.Count(s => s.Slot < slot && s.LevelledOrder == 0 && s.Level > 1);
		return top + 1 + auto;
	}

	static string Sentence(string m) => m.Length == 0 ? m : char.ToUpperInvariant(m[0]) + m[1..] + (m.EndsWith('.') || m.EndsWith(')') ? "" : ".");

	static DevRunCheck Refuse(string why) => new(null, null, why);
}
