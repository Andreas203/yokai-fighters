using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using YokaiFighters.Fight;
using YokaiFighters.Sim;
using YokaiFighters.Story;

namespace YokaiFighters.Dev;

/// <summary>What a jump opens: a fight or rest stop through the real run flow, or a screen in the dev viewer.</summary>
public enum DevJumpKind { Fight, RestStop, RewardDraft, Trial, StoryCard, LoseScreen, RunComplete, ControlSelect, MoveList }

/// <summary>
/// One entry of the Jump page. <see cref="NotBuilt"/> names why it is greyed (the screen or system is not on main
/// yet); null = it opens. <see cref="Arg"/> is the kind's argument (story card id, trial id, rest-stop type).
/// </summary>
public sealed record DevJumpTarget(string Id, string Group, string Title, DevJumpKind Kind, string? Arg = null, string? NotBuilt = null)
{
	public bool Built => NotBuilt is null;
}

/// <summary>
/// A fight opponent on the Jump page; greyed (<see cref="NotBuilt"/>) when it has neither a fighter nor a body of its own.
/// <see cref="Note"/> qualifies a pickable one that shows its own body on a stand-in's moves (YOK-135).
/// </summary>
public sealed record DevOpponent(string Yokai, string Name, string? NotBuilt, string? Note = null)
{
	public bool Built => NotBuilt is null;

	/// <summary>The menu line: the name, with the note or the greyed reason in brackets.</summary>
	public string Label => Name + (NotBuilt is { } why ? $" ({why})" : Note is { } note ? $" ({note})" : "");
}

/// <summary>Y1/Y3, rules.md Y table: each yokai's elder and its one twist.</summary>
public sealed record DevElder(string Yokai, string ElderYokai, string Name, string Twist);

/// <summary>Stage choice. Level presets (data/presets/) are not built yet, so the stage is the only look to pick.</summary>
public enum DevStage { Grove, Placeholder }

/// <summary>Everything the fight setup chooses (YOK-125).</summary>
public sealed record DevFightSetup(
	string Opponent = "kitsune",
	string? Temperament = null,
	bool Elder = false,
	DevStage Stage = DevStage.Grove,
	ControlScheme Scheme = ControlScheme.Kihon,
	string? CopySpecial = null,
	int CopyLevel = 1,
	uint Seed = DevJump.DefaultSeed);

/// <summary>A session built for a jump and the node to enter on it.</summary>
public sealed record DevJumpRun(RunSession Session, string NodeId);

/// <summary>
/// YOK-125: the Jump page's catalog and the run states its jumps build. Godot-free apart from the content loaders
/// on <see cref="FightScene"/>. Every jump builds a real <see cref="RunSession"/> (or a real <see cref="RunState"/>
/// for a screen viewed on its own) from <see cref="DevRunStates.Source"/>, on a small map of its own
/// (<see cref="MapId"/>), so the screens show real data and the run flow, copy lock (T1) and rest stops run their
/// normal code. The map's nodes are real node types resolved by data/map/node-scenes.json; the only override is
/// the fight node's starting profile (the chosen temperament).
/// </summary>
public static class DevJump
{
	public const string MapId = "dev-jump";
	public const string FightNodeId = "dev-fight", StopNodeId = "dev-stop", AfterStopNodeId = "dev-after-stop";
	public const uint DefaultSeed = 125;
	public const string NotBuiltYet = "not built yet";

	/// <summary>Y1: the three yokai and the Tanuki, in the GDD's order. Human guises have no data yet.</summary>
	public static readonly string[] OpponentIds = { "kitsune", "oni", "kappa", "tanuki" };
	public const string TanukiId = "tanuki";

	/// <summary>Rules Y table: one elder and one twist per yokai (the twists themselves are YOK-90).</summary>
	public static readonly DevElder[] Elders =
	{
		new("kitsune", "nine-tailed-kitsune", "Nine-Tailed Kitsune", "Reflects any special used twice in a row"),
		new("oni", "elder-oni", "Elder Oni", "Armour on every special"),
		new("kappa", "elder-kappa", "Elder Kappa", "Throws can't be broken"),
	};
	public const string TwistNotBuilt = "twist not built yet (YOK-90): the HUD shows its placeholder strip";

	/// <summary>Rows the dev map puts each node type on (the default map's rows: R2, Y4, S3 by row).</summary>
	public static int RowOf(string type) => type switch
	{
		NodeTypeRegistry.Boss => 9,
		NodeTypeRegistry.ElderDuel => 5,
		NodeTypeRegistry.Merchant => 4,
		NodeTypeRegistry.Dojo => 7,
		_ => 2,
	};

	static string MapDir => ContentPaths.Data("map");

	/// <summary>The node-scene table, with <paramref name="profile"/> as the fight node's starting profile when given.</summary>
	public static NodeSceneTable Table(string? profile = null)
	{
		var root = JsonNode.Parse(File.ReadAllText(Path.Combine(MapDir, NodeSceneTable.DefaultFile)))!.AsObject();
		if (profile != null)
		{
			if (root["nodes"] is not JsonObject nodes) root["nodes"] = nodes = new JsonObject();
			nodes[FightNodeId] = new JsonObject { ["profile"] = profile };
		}
		return NodeSceneTable.Parse(root.ToJsonString());
	}

	static string TypeFor(string yokai, bool elder) =>
		yokai == TanukiId ? NodeTypeRegistry.Boss : elder ? NodeTypeRegistry.ElderDuel : NodeTypeRegistry.Duel;

	static MapNodeData FightNode(string yokai, bool elder)
	{
		string type = TypeFor(yokai, elder);
		string who = elder && ElderOf(yokai) is { } e ? e.ElderYokai : yokai;
		return new MapNodeData(FightNodeId, RowOf(type), type, who, elder && type == NodeTypeRegistry.ElderDuel, StoryLibrary.DisplayName(who));
	}

	public static DevElder? ElderOf(string yokai) => Array.Find(Elders, e => e.Yokai == yokai);

	/// <summary>
	/// The opponents. One is built when the node-scene table gives it a fighter of its own, or when it stands in on
	/// another fighter's kit but has a body of its own (<see cref="FighterAnimSet.HasBody"/>: the Oni and Kappa fight as
	/// themselves on the Kitsune's moves). A stand-in with no body of its own stays greyed.
	/// </summary>
	public static IReadOnlyList<DevOpponent> Opponents(NodeSceneTable? table = null)
	{
		table ??= Table();
		var list = new List<DevOpponent>();
		foreach (string id in OpponentIds)
		{
			var ctx = table.Resolve(FightNode(id, false)).Context;
			string name = StoryLibrary.DisplayName(id);
			if (!ctx.StandIn) list.Add(new DevOpponent(id, name, null));
			else if (FighterAnimSet.HasBody(id)) list.Add(new DevOpponent(id, name, null, $"own body, {StoryLibrary.DisplayName(ctx.Fighter)}'s moves"));
			else list.Add(new DevOpponent(id, name, $"{NotBuiltYet}: no fighter of its own (would fight as the {ctx.Fighter} stand-in)"));
		}
		list.Add(new DevOpponent("human-guises", "Human guises", $"{NotBuiltYet}: no guise has data"));
		return list;
	}

	/// <summary>The fighter id the node-scene table gives the setup's node (the elder's stand-in is its yokai's fighter).</summary>
	public static string FighterOf(DevFightSetup setup) => Table().Resolve(FightNode(setup.Opponent, setup.Elder)).Context.Fighter;

	/// <summary>YOK-174: the yokai whose behaviour profiles (and own move files) the setup's opponent fights with.</summary>
	public static string PlaysAsOf(DevFightSetup setup) => Table().Resolve(FightNode(setup.Opponent, setup.Elder)).Context.MovesAndProfilesOf;

	/// <summary>YOK-187: the name the lose and demo-complete cards use for the setup's foe: the yokai (an elder as "Elder Oni"), never the stand-in body.</summary>
	public static string NameOf(DevFightSetup setup) => StoryLibrary.DisplayName(Table().Resolve(FightNode(setup.Opponent, setup.Elder)).Context.BindingName);

	/// <summary>YOK-212: the lose card a loss to the setup's foe on its node's row would show (<see cref="FightScene.LoseCardFor"/>; a duel is row 2, an elder row 5, the Tanuki row 9).</summary>
	public static (string? Title, string Text) LoseCardOf(DevFightSetup setup, StoryLibrary story)
	{
		var ctx = Table().Resolve(FightNode(setup.Opponent, setup.Elder)).Context;
		return FightScene.LoseCardFor(story, ctx, ctx.Fighter);
	}

	/// <summary>Temperaments of the fighter's profiles (data/profiles/; Y1), aggressive first.</summary>
	public static IReadOnlyList<string> Temperaments(string fighter) =>
		FightScene.LoadProfiles(fighter).Select(p => p.Temperament).Distinct().ToArray();

	/// <summary>The profile id the fight starts on for <paramref name="temperament"/>; null = the fighter's first.</summary>
	public static string? ProfileFor(string fighter, string? temperament) =>
		temperament is null ? null : FightScene.LoadProfiles(fighter).FirstOrDefault(p => p.Temperament == temperament)?.Id;

	/// <summary>
	/// The fight's session: the run from <paramref name="source"/>, a one-node map holding the chosen opponent (a
	/// duel, an elder, or the Tanuki's boss node), and for the Tanuki the run shaped so the copy rule picks the
	/// chosen special at the chosen level (T2-T4). The copy is locked by <see cref="RunSession.Enter"/> as in a real
	/// run (T1). Throws ArgumentException for a setup that cannot be built.
	/// </summary>
	public static DevJumpRun BuildFight(DevFightSetup setup, AbilityPool pool, IDevRunStateSource? source = null)
	{
		if (!OpponentIds.Contains(setup.Opponent)) throw new ArgumentException($"unknown opponent '{setup.Opponent}'");
		if (setup.Elder && (setup.Opponent == TanukiId || ElderOf(setup.Opponent) is null)) throw new ArgumentException($"{setup.Opponent} has no elder");
		var node = FightNode(setup.Opponent, setup.Elder);
		var table = Table(ProfileFor(Table().Resolve(node).Context.MovesAndProfilesOf, setup.Temperament)); // YOK-174
		var run = (source ?? DevRunStates.Source).Build(pool);
		if (setup.Opponent == TanukiId && setup.CopySpecial is { } copy) ShapeCopy(run, pool, copy, setup.CopyLevel);
		run.StartMap(new MapGraph(MapId, new[] { node }, Array.Empty<MapRoute>(), NodeTypeRegistry.Default(), "Dev jump"));
		return new DevJumpRun(new RunSession(run, pool, table, setup.Seed), FightNodeId);
	}

	/// <summary>
	/// A rest stop (R6 merchant or R7 dojo) and a Kitsune duel after it, so Continue leads back to a map with a node to
	/// take rather than ending the run on the stop.
	/// </summary>
	public static DevJumpRun BuildRestStop(string type, AbilityPool pool, IDevRunStateSource? source = null, uint seed = DefaultSeed)
	{
		if (type is not (NodeTypeRegistry.Merchant or NodeTypeRegistry.Dojo)) throw new ArgumentException($"'{type}' is not a rest stop");
		int row = RowOf(type);
		var nodes = new[]
		{
			new MapNodeData(StopNodeId, row, type, Label: type == NodeTypeRegistry.Merchant ? "Merchant" : "Dojo"),
			new MapNodeData(AfterStopNodeId, row + 1, NodeTypeRegistry.Duel, "kitsune", Label: "Kitsune"),
		};
		var run = (source ?? DevRunStates.Source).Build(pool);
		run.StartMap(new MapGraph(MapId, nodes, new[] { new MapRoute(StopNodeId, AfterStopNodeId) }, NodeTypeRegistry.Default(), "Dev jump"));
		return new DevJumpRun(new RunSession(run, pool, Table(), seed), StopNodeId);
	}

	/// <summary>
	/// T2-T4: makes <paramref name="specialId"/> at <paramref name="level"/> the copy target of <paramref name="run"/>:
	/// equips it (A3), levels it, and releases whichever special the copy rule still prefers (a higher level, or an
	/// equal one levelled later or in a lower slot, T3) until it is the chosen one. Returns the target.
	/// </summary>
	public static CopyTarget ShapeCopy(RunState run, AbilityPool pool, string specialId, int level)
	{
		var data = pool.Special(specialId) ?? throw new ArgumentException($"'{specialId}' is not in the ability pool");
		int top = Math.Min(3, data.MaxLevel);
		if (level < 1 || level > top) throw new ArgumentException($"{specialId} has no level {level} (1-{top})");
		var slot = data.Slot;
		if (run[slot] is { } held && held.Data.Id == specialId && held.Level > level)
		{
			if (held.Data.Starter) throw new ArgumentException($"{held.Data.Name} is at Lv {held.Level}: Ryo's starter can't lose levels (A1, R6)");
			if (run.SpecialCount == 1) throw new ArgumentException($"{specialId} is Ryo's only special at Lv {held.Level}; it can't go down to Lv {level}");
			run.Release(slot);
		}
		if (run[slot]?.Data.Id != specialId) run.Equip(data);
		while (run[slot]!.Level < level) run.LevelUp(slot);
		while (CopyRule.Target(run) is { } t && t.Slot != slot)
		{
			// A1/R6: starters are never released (the merchant refuses it), so a run that would need it is unreachable.
			if (t.Data.Starter)
				throw new ArgumentException($"T3 picks {t.Data.Name} (Lv {t.Level}) over {data.Name} Lv {level}: not reachable with Ryo's starters");
			run.Release(t.Slot);
		}
		return CopyRule.Target(run)!;
	}

	/// <summary>Reward-draft sources (A8): every yokai that offers a special or a modifier in the pool.</summary>
	public static IReadOnlyList<string> DraftYokai(AbilityPool pool) =>
		pool.Specials.Where(s => !s.Starter).Select(s => s.Source).Concat(pool.Modifiers.Select(m => m.Source))
			.Where(s => s != "ryo").Distinct().OrderBy(s => s, StringComparer.Ordinal).ToArray();

	/// <summary>
	/// The A7 draft from <paramref name="yokai"/>, seeded. <paramref name="rares"/> = the elder's spread (commons and
	/// rares, A9); off = a duel's (commons only). The run is the source's, given to the screen so a pick lands on it.
	/// </summary>
	public static (RewardDraft Draft, RunState Run) BuildDraft(AbilityPool pool, string yokai, uint seed, bool rares, IDevRunStateSource? source = null)
	{
		var run = (source ?? DevRunStates.Source).Build(pool);
		return (RewardDraft.Build(run, pool, yokai, seed, elder: rares), run);
	}

	/// <summary>Every Jump target. Data-driven where the content is: one entry per story card and per dojo trial.</summary>
	public static IReadOnlyList<DevJumpTarget> Targets()
	{
		var list = new List<DevJumpTarget>
		{
			new("fight", "Fight", "Fight (setup above)", DevJumpKind.Fight),
			new("reward-draft", "Screens", "Reward draft (setup above)", DevJumpKind.RewardDraft),
			new("rest-merchant", "Screens", "Rest stop: merchant", DevJumpKind.RestStop, NodeTypeRegistry.Merchant),
			new("rest-dojo", "Screens", "Rest stop: dojo", DevJumpKind.RestStop, NodeTypeRegistry.Dojo),
		};
		TrialData[] trials;
		try { trials = TrialLoader.LoadDirectory(ContentPaths.Data("trials")); }
		catch (Exception e) when (e is IOException or FormatException or System.Text.Json.JsonException) { trials = Array.Empty<TrialData>(); }
		const string noTrialRunner = NotBuiltYet + ": the trial runner (YOK-98)";
		if (trials.Length == 0) list.Add(new("trial", "Dojo trials", "Dojo trials", DevJumpKind.Trial, NotBuilt: noTrialRunner));
		foreach (var t in trials) list.Add(new("trial:" + t.Id, "Dojo trials", $"Trial: {t.Id}", DevJumpKind.Trial, t.Id, noTrialRunner));
		foreach (var c in StoryCards()) list.Add(new("story:" + c.Id, "Story cards", $"Story: {c.Id}", DevJumpKind.StoryCard, c.Id));
		list.Add(new("results-lose", "Screens", "Results: lose screen", DevJumpKind.LoseScreen));
		list.Add(new("results-complete", "Screens", "Results: run complete card", DevJumpKind.RunComplete));
		list.Add(new("control-select", "Screens", "Control select", DevJumpKind.ControlSelect));
		list.Add(new("move-list", "Screens", "Move list", DevJumpKind.MoveList));
		return list;
	}

	/// <summary>data/story/ cards, sorted by id; none when the folder can't be read.</summary>
	public static IReadOnlyList<StoryCardData> StoryCards()
	{
		try { return FightScene.LoadStory().Cards.OrderBy(c => c.Id, StringComparer.Ordinal).ToArray(); }
		catch (Exception e) when (e is IOException or FormatException or System.Text.Json.JsonException or InvalidOperationException)
		{
			return Array.Empty<StoryCardData>();
		}
	}
}
