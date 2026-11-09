using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using YokaiFighters.Ai;
using YokaiFighters.Sim;

namespace YokaiFighters.Ui;

/// <summary>
/// YOK-78 adapter: a <see cref="RunMap"/> (pure C#, YOK-76) as the map screen's <see cref="IMapView"/>. No rules here: reachability,
/// visited and the walk all come from the RunMap, read fresh on every <see cref="Nodes"/> call. <see cref="Select"/> enters the node
/// (<c>RunMap.Select</c>); the run flow owns <c>Complete</c> and what the node does.
/// State: the node Ryo is on (entered, or just completed) is Current; other completed nodes Visited; <c>Reachable()</c> Reachable;
/// an unchosen node in a row Ryo has left (row at or below the Current node's) PassedOver (YOK-108); the rest Locked.
/// YOK-108: with a <see cref="RunState"/> the view also gives the status panel's numbers and the T6 copy target (read-only,
/// <see cref="CopyRule.Target"/>); with an <see cref="INodeSceneResolver"/> it words each node's reward line from the same
/// table the run flow uses (<c>NodeContext.Coins</c>, <c>Binds</c>), so no reward number is typed here.
/// </summary>
public class RunMapView : IMapView
{
	readonly RunMap _map;
	readonly RunState? _run;
	readonly INodeSceneResolver? _scenes;
	readonly IReadOnlyList<MapRouteView> _routes;
	readonly HashSet<int> _playRows;
	readonly uint _seed;

	/// <summary>The run's seed decides each node's temperament (<see cref="NodeTemperament"/>), so the habit line shows the profile the fight will use. 0 = unknown.</summary>
	public RunMapView(RunMap map, RunState? run = null, INodeSceneResolver? scenes = null, uint seed = 0)
	{
		_map = map; _run = run; _scenes = scenes; _seed = seed;
		_routes = map.Graph.Routes.Select(r => new MapRouteView(r.From, r.To)).ToList();
		_playRows = map.Graph.Nodes.Where(n => n.Type != NodeTypeRegistry.Boss).Select(n => n.Row).ToHashSet();
	}

	public IReadOnlyList<MapRouteView> Routes => _routes;

	public IReadOnlyList<MapNodeView> Nodes
	{
		get
		{
			var reachable = _map.Reachable();
			int leftRow = _map.CurrentNode?.Row ?? int.MinValue;
			return _map.Graph.Nodes.Select(n => new MapNodeView(n.Id, n.Row, n.Type, YokaiName(n), n.Elder,
				n.Id == _map.Current ? MapNodeState.Current
				: _map.IsVisited(n.Id) ? MapNodeState.Visited
				: reachable.Contains(n.Id) ? MapNodeState.Reachable
				: n.Row <= leftRow ? MapNodeState.PassedOver
				: MapNodeState.Locked) { TwistIcon = n.Elder ? Fight.FightScene.Twists.ForNode(n)?.Icon : null, YokaiId = n.Yokai }).ToList();
		}
	}

	public MapStatusView? Status => _run is null ? null
		: new(_run.Health, RunState.MaxHealth, _run.Coins, _map.Visited.Count(id => _playRows.Contains(_map.Graph.Node(id).Row)), _playRows.Count) { LastPlayRow = _playRows.Count == 0 ? 0 : _playRows.Max() };

	public MapNodeDetail? Detail(string nodeId) => _map.Graph.Has(nodeId) ? new MapNodeDetail(RewardLine(_map.Graph.Node(nodeId)), TwistLine(_map.Graph.Node(nodeId)), HabitLine(_map.Graph.Node(nodeId))) : null;

	/// <summary>YOK-109: one legend row per distinct twist icon among the map's elders, worded by the twist's story label (a missing label drops the row, no stand-in text).</summary>
	public IReadOnlyList<MapTwistLegendEntry> TwistLegend =>
		_map.Graph.Nodes.Where(n => n.Elder).Select(n => Fight.FightScene.Twists.ForNode(n)).OfType<ElderTwist>()
			.Where(t => !string.IsNullOrEmpty(t.Icon)).GroupBy(t => t.Icon!).Select(g => g.First())
			.Select(t => (Twist: t, Line: TwistCopy.Label(t, TwistCopy.Story)))
			.Where(x => !x.Line.Placeholder).Select(x => new MapTwistLegendEntry(x.Twist.Icon!, x.Line.Text)).ToList();

	/// <summary>Y3: the elder's twist line for the detail panel, the twist's story label; null off an elder node or when the story has no label (YOK-109: no stand-in text).</summary>
	static string? TwistLine(MapNodeData n) =>
		n.Elder && Fight.FightScene.Twists.ForNode(n) is { } t && TwistCopy.Label(t, TwistCopy.Story, YokaiName(n)) is { Placeholder: false } line ? "Twist: " + line.Text : null;

	/// <summary>
	/// The profiles the habit line reads: <c>data/profiles/</c>, loaded once (null when they cannot be read; YOK-211: that
	/// result is kept too, never re-read on each call). A test may set its own; setting null means "no profile data".
	/// </summary>
	public static BehaviourProfile[]? Profiles
	{
		get
		{
			if (!_profilesLoaded) { _profiles = LoadProfiles(); _profilesLoaded = true; }
			return _profiles;
		}
		set { _profiles = value; _profilesLoaded = true; }
	}

	static BehaviourProfile[]? _profiles;
	static bool _profilesLoaded;

	/// <summary>YOK-211: how many times <see cref="Profiles"/> has read data/profiles/ (tests check it is once).</summary>
	public static int ProfileLoads { get; private set; }

	/// <summary>Drops the cached profiles; the next <see cref="Profiles"/> read loads data/profiles/ again.</summary>
	public static void ReloadProfiles() { _profiles = null; _profilesLoaded = false; }

	/// <summary>
	/// E23 debug (YOK-211): the temperament the fight's debug menu holds (<see cref="RunSession.DebugTemperament"/>), read on
	/// each habit line so the map shows the habit the fight will use. Null (or returning null) = each node's own roll.
	/// </summary>
	public Func<string?>? DebugTemperament { get; init; }

	static BehaviourProfile[]? LoadProfiles()
	{
		ProfileLoads++;
		try { return BehaviourProfile.LoadDirectory(ContentPaths.Data("profiles")); }
		catch (Exception e) when (e is System.IO.IOException or FormatException or System.Text.Json.JsonException) { return null; }
	}

	/// <summary>
	/// YOK-109: the habit of the profile this node's opponent fights with. YOK-211: the fight's own pick
	/// (<see cref="NodeProfiles.Pick(NodeContext, BehaviourProfile[], BehaviourProfile[], uint, string?)"/>: plays-as yokai,
	/// named profile, E23 debug temperament, the run's seeded roll), first sentence of the profile's own description. Null for
	/// a node whose type has no fight (node-type data, <see cref="INodeType.Fight"/>), the boss (its habit is not shown ahead),
	/// or no profile data. No TEST FIXTURE profiles here: a yokai with none in data/profiles/ shows no habit.
	/// </summary>
	string? HabitLine(MapNodeData n)
	{
		if (_scenes is null || Profiles is not { } all || _map.Graph.TypeOf(n) is not { Fight: true } || n.Type == NodeTypeRegistry.Boss) return null;
		NodeContext ctx;
		try { ctx = _scenes.Resolve(n).Context; }
		catch (FormatException) { return null; }
		NodePick pick;
		try { pick = NodeProfiles.Pick(ctx, all, Array.Empty<BehaviourProfile>(), _seed, DebugTemperament?.Invoke()); }
		catch (InvalidOperationException) { return null; }
		return pick.Profile is { } p ? HabitSentence(p.Habit.Description) : null;
	}

	/// <summary>Words that end in a full stop without ending the sentence (compared lower-case, without the stop).</summary>
	static readonly HashSet<string> Abbreviations = new(StringComparer.Ordinal) { "e.g", "i.e", "etc", "vs", "approx", "cf", "lv", "mr", "mrs", "ms", "dr", "st" };

	/// <summary>
	/// The habit part of a profile's description (its first sentence; the rest says how to beat it). Empty = null.
	/// YOK-211: a sentence ends at '.', '!' or '?' (any closing quote or bracket kept) followed by a space and a word that
	/// is not lower-case; not at an ellipsis or a common abbreviation ("e.g.", "Lv."). With no such end the whole text is the habit.
	/// </summary>
	public static string? HabitSentence(string description)
	{
		string d = description.Trim();
		if (d.Length == 0) return null;
		for (int i = 0; i < d.Length; i++)
		{
			char c = d[i];
			if (c is not ('.' or '!' or '?')) continue;
			int end = i + 1;
			while (end < d.Length && d[end] is '.' or '!' or '?') end++; // "?!" or "..." is one mark
			if (c == '.' && end - i > 1) { i = end - 1; continue; } // an ellipsis does not end the sentence
			while (end < d.Length && d[end] is '"' or '\'' or ')' or ']' or '”' or '’') end++;
			if (end < d.Length && !char.IsWhiteSpace(d[end])) { i = end - 1; continue; } // "1.5", "e.g" mid-word
			if (end == d.Length) return d;
			if (c == '.' && Abbreviations.Contains(WordBefore(d, i).ToLowerInvariant())) continue;
			int next = end;
			while (next < d.Length && char.IsWhiteSpace(d[next])) next++;
			if (char.IsLower(d[next])) continue; // a quoted "Ha!" mid-sentence: the next word is not a new sentence
			return d[..end];
		}
		return d;
	}

	/// <summary>The word that ends just before index <paramref name="dot"/> (letters and inner dots, so "e.g" for "e.g.").</summary>
	static string WordBefore(string d, int dot)
	{
		int start = dot;
		while (start > 0 && (char.IsLetter(d[start - 1]) || d[start - 1] == '.')) start--;
		return d[start..dot];
	}

	public bool Select(string nodeId) => _map.Select(nodeId).Ok;

	string? RewardLine(MapNodeData n)
	{
		switch (n.Type)
		{
			case NodeTypeRegistry.Merchant: return $"Heal {Merchant.HealPercent}%, or shop with your coins.";
			case NodeTypeRegistry.Dojo: return $"Rest {Dojo.RestHealPercent}%, or take a training trial.";
			case NodeTypeRegistry.Boss: return "The last duel of the run.";
		}
		if (_scenes is null) return null;
		NodeContext ctx;
		try { ctx = _scenes.Resolve(n).Context; }
		catch (FormatException) { return null; }
		string pay = ctx.Coins > 0 ? $"Pays {ctx.Coins} coins." : ctx.Elder && RunSession.R8FallbackCoins(_map.Graph, _scenes) is > 0 and var r8 ? $"Pays a rare ability, or {r8} coins if its offer has none." : "Pays no coins."; // R8; the coins are PROVISIONAL (YOK-186)
		if (!ctx.Binds) return pay;
		return $"{pay} Bind it and draft an ability{(n.Elder ? " (a rare can appear)" : "")}.";
	}

	static string? YokaiName(MapNodeData n) =>
		n.Yokai is null ? null : n.Label ?? CultureInfo.InvariantCulture.TextInfo.ToTitleCase(n.Yokai.Replace('-', ' '));
}
