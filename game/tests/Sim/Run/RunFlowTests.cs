using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Flow;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-77: the run flow. The node-scene table (data/map/node-scenes.json), <see cref="RunSession"/> walked through
/// whole runs with the sim only (no scene), and the boot flow with the real map screen: map → fight → map,
/// loss → new run, the last node → end card.
/// </summary>
public static class RunFlowTests
{
	static readonly FighterInput Idle = FighterInput.None;
	static FighterInput Strike(int t) => new(t % 2 == 0 ? InputBits.DebugStrike : InputBits.None);

	static string MapDir => ContentPaths.Data("map");
	static NodeSceneTable Table() => NodeSceneTable.Default(MapDir);
	static MapGraph DefaultMap() => FileMapSource.Default(MapDir).Load();

	static void OnFixtures(Action body)
	{
		var saved = FightScene.Sources;
		var savedRun = FightScene.Run;
		var savedFoe = FightScene.Opponent;
		FightScene.Sources = FightScene.FixtureSources;
		FightScene.Run = null;
		try { body(); }
		finally { FightScene.Sources = saved; FightScene.Run = savedRun; FightScene.Opponent = savedFoe; }
	}

	static void Throws<T>(Action body, string what) where T : Exception
	{
		try { body(); }
		catch (T) { return; }
		throw new AssertFailed($"{what}: expected {typeof(T).Name}");
	}

	// ---- the node-scene table ----

	[Test]
	public static void Table_DefaultMap_EveryNodeHasItsScene_WithItsY4Row()
	{
		var table = Table();
		var frames = new Dictionary<string, int> { ["apprentice"] = 30, ["yokai"] = 22, ["elder"] = 18, ["tanuki"] = 15 }; // Y4
		foreach (var node in DefaultMap().Nodes)
		{
			var (scene, ctx) = table.Resolve(node);
			string expected = node.Type is "merchant" or "dojo" ? "res://scenes/ui/rest_stop.tscn" : "res://scenes/fight.tscn"; // YOK-96: stops have their own screen
			Assert.Equal(expected, scene, $"{node.Id}: the fight scene, or the rest stop for a merchant or dojo");
			Assert.True(ResourceLoader.Exists(scene), $"{node.Id}: {scene} exists");
			string tier = node.Row == 1 ? "apprentice" : node.Type switch { "elder" => "elder", "boss" => "tanuki", _ => "yokai" };
			Assert.Equal(tier, ctx.Tier, $"{node.Id}: Y4 row");
			Assert.Equal(frames[tier], ctx.ReactionFrames, $"{node.Id}: Y4 reaction frames");
			Assert.Equal(node.Type is "duel" or "elder", ctx.Binds, $"{node.Id}: only duels and elders bind (R3, A7)");
			Assert.True(ctx.NodeId == node.Id && ctx.Type == node.Type && ctx.Row == node.Row && ctx.Yokai == node.Yokai && ctx.Elder == node.Elder, $"{node.Id}: the node's own data is carried");
			Assert.Equal(node.Yokai == "tanuki" ? "tanuki" : "kitsune", ctx.Fighter, $"{node.Id}: the Kitsune's kit, the Tanuki's (YOK-94, T0) on the boss node");
			Assert.Equal(node.Yokai is not ("kitsune" or "tanuki"), ctx.StandIn, $"{node.Id}: stand-in unless the node is a Kitsune or the Tanuki (YOK-94)");
			Assert.Equal(ctx.StandIn, ctx.Notes.Count > 0, $"{node.Id}: a stand-in is always explained for the log");
		}
	}

	const string Custom = """
	{
	  "kind": "node_scenes", "id": "node-scenes", "schema_version": 1,
	  "default_scene": "res://scenes/fight.tscn", "fallback_fighter": "kitsune",
	  "tiers": { "yokai": { "reaction_frames": 22 }, "elder": { "reaction_frames": 18 } },
	  "types": {
	    "duel": { "tier": "yokai", "binds": true },
	    "merchant": { "scene": "res://scenes/ui/merchant.tscn", "tier": "yokai", "opponent": "oni" }
	  },
	  "opponents": { "oni": { "fighter": "oni", "profile": "oni-patient" }, "kitsune": { "fighter": "kitsune" } },
	  "nodes": { "special": { "scene": "res://scenes/special.tscn", "tier": "elder", "binds": false, "profile": "kitsune-patient" } }
	}
	""";

	[Test]
	public static void Table_PerTypePerNodeAndPayloadScenes_AreDataEdits()
	{
		var table = NodeSceneTable.Parse(Custom);
		var oni = table.Resolve(new MapNodeData("a", 1, "duel", "oni"));
		Assert.True(oni.Scene == "res://scenes/fight.tscn" && oni.Context.Fighter == "oni" && !oni.Context.StandIn && oni.Context.Notes.Count == 0, "a yokai with its own fighter entry: default scene, no stand-in");
		Assert.Equal("oni-patient", oni.Context.Profile, "profile from the opponent entry");

		var merchant = table.Resolve(new MapNodeData("m", 2, "merchant"));
		Assert.Equal("res://scenes/ui/merchant.tscn", merchant.Scene, "a type's own scene");
		Assert.True(merchant.Context.Fighter == "oni" && merchant.Context.StandIn && merchant.Context.Yokai == null && !merchant.Context.Binds, "a node without a yokai takes the type's stand-in opponent");

		var special = table.Resolve(new MapNodeData("special", 3, "duel", "kitsune"));
		Assert.True(special.Scene == "res://scenes/special.tscn" && special.Context.Tier == "elder" && special.Context.ReactionFrames == 18 && !special.Context.Binds, "a node entry wins over its type");
		Assert.Equal("kitsune-patient", special.Context.Profile, "profile from the node entry");

		var payload = new Dictionary<string, System.Text.Json.JsonElement> { ["scene"] = System.Text.Json.JsonDocument.Parse("\"res://scenes/den.tscn\"").RootElement.Clone() };
		var den = table.Resolve(new MapNodeData("special", 3, "duel", "kitsune", Payload: payload));
		Assert.Equal("res://scenes/den.tscn", den.Scene, "the node's payload.scene wins over everything");
		Assert.True(den.Context.Payload.ContainsKey("scene"), "the payload is handed on");

		Throws<FormatException>(() => table.Resolve(new MapNodeData("d", 1, "dojo")), "a type with no entry has no tier");
		Throws<FormatException>(() => NodeSceneTable.Parse(Custom.Replace("\"tier\": \"elder\"", "\"tier\": \"legend\"")), "an undefined tier");
		Throws<FormatException>(() => NodeSceneTable.Parse(Custom.Replace("node_scenes", "run_map")), "wrong kind");
		Throws<FormatException>(() => NodeSceneTable.Parse(Custom.Replace("\"schema_version\": 1", "\"schema_version\": 2")), "unknown schema version");
	}

	// ---- the session, sim only ----

	sealed record Walk(List<string> Nodes, List<string> Events, List<int> Health, ulong Hash, RunSession Session);

	/// <summary>
	/// One whole run with no scene: at every step the node is picked from <c>Reachable()</c> by <paramref name="pick"/>,
	/// the fight is a real <see cref="Match"/> on the node's context (Ryo takes <paramref name="hits"/> strikes, then
	/// wins), the first card is drafted, the node completed.
	/// </summary>
	static Walk Play(uint seed, Func<IReadOnlyList<string>, string> pick, int hits = 1)
	{
		var session = RunSession.Start(FightScene.LoadAbilityPool(), DefaultMap(), Table(), seed);
		var events = new List<string>();
		session.NodeEntered += n => events.Add("enter " + n.NodeId);
		session.NodeCompleted += n => events.Add("complete " + n.NodeId);
		session.RunEnded += e => events.Add($"end {e.Outcome} row {e.RowReached}");
		var nodes = new List<string>();
		var health = new List<int>();
		ulong hash = Fnv.Offset;
		for (int step = 1; step <= 20 && !session.IsOver; step++)
		{
			var reachable = session.Reachable();
			Assert.True(reachable.Count > 0, $"step {step}: something is reachable");
			string id = pick(reachable);
			Assert.True(session.Enter(id).Ok, $"enter {id}");
			var node = session.Node!;
			Assert.True(node.NodeId == id && session.Step == step && session.Reachable().Count == 0, $"{id}: entered, nothing else reachable (R1)");
			nodes.Add(id);

			int before = session.Run.Health;
			FightScene.Opponent = node.Fighter;
			Match m = FightScene.NewMatch(session.Run);
			Assert.Equal(before, m.P1.Health, $"{id}: Ryo starts on the carried health (C1)");
			for (int t = 0; t < hits * 2; t++) m.Step(Idle, Strike(t));
			for (int t = 0; t < 900 && m.Phase != MatchPhase.Over; t++) m.Step(Strike(t), Idle);
			Assert.True(m.Phase == MatchPhase.Over && m.Winner == 0, $"{id}: Ryo won (winner {m.Winner}, health {m.P1.Health} v {m.P2.Health}, from {before})");
			hash = Fnv.Mix(hash, unchecked((int)m.StateHash()));

			bool last = session.Map.Graph.Next(id).Count == 0;
			var draft = session.WinFight(m.P1.Health);
			Assert.Equal(Math.Min(RunState.MaxHealth, m.P1.Health + (node.Binds ? RunState.BindHeal : 0)), session.Run.Health, $"{id}: R3 heal only when the node binds");
			Assert.Equal(node.Binds && !last, draft != null, $"{id}: a draft after a bound duel (A7), none on a stand-in stop or the last node");
			if (draft != null)
			{
				Assert.True(draft.Offer.Yokai == node.DraftYokai && draft.Offer.Cards.Count > 0, $"{id}: cards from the yokai beaten, not the stand-in body (A7, YOK-178)");
				Assert.True(!session.CompleteNode().Ok && session.Node != null, $"{id}: not completed before the pick");
				var card = draft.Offer.Cards[0];
				Assert.True(draft.Pick(0, card.Targets.Count > 0 ? card.Targets[0].Slot : null).Ok, $"{id}: picked {card.AbilityId}");
			}
			Assert.True(session.CompleteNode().Ok, $"{id}: completed");
			Assert.True(session.Node == null && session.Draft == null, $"{id}: back on the map");
			health.Add(session.Run.Health);
			ulong s = session.StateHash();
			hash = Fnv.Mix(Fnv.Mix(hash, unchecked((int)s)), unchecked((int)(s >> 32)));
		}
		return new Walk(nodes, events, health, hash, session);
	}

	[Test]
	public static void Session_WalksWholeRuns_ThroughEveryNode_Deterministically() => OnFixtures(() =>
	{
		var graph = DefaultMap();
		var seen = new HashSet<string>();
		string Unseen(IReadOnlyList<string> r) => r.FirstOrDefault(id => !seen.Contains(id)) ?? r[0];
		int runs = 0;
		while (seen.Count < graph.Nodes.Count)
		{
			Assert.True(++runs <= graph.Nodes.Count, "every node is on some walk");
			int was = seen.Count;
			var picks = new List<string>();
			var walk = Play(77, r => { string id = Unseen(r); picks.Add(id); return id; });
			foreach (string id in walk.Nodes) seen.Add(id);
			Assert.True(seen.Count > was, "each run covers a new node");

			var end = walk.Session.Ended;
			Assert.True(end is { Won: true, RowReached: 9, NodeId: "boss-tanuki" }, $"run {runs}: won on the Tanuki's node, row 9");
			Assert.True(walk.Session.Map.IsFinished && walk.Session.Reachable().Count == 0 && !walk.Session.Enter(graph.StartNodes[0]).Ok, "nothing after the end");
			Assert.Equal(graph.Rows.Count, walk.Nodes.Count, "one node per row (R1)");
			Assert.True(walk.Nodes.SequenceEqual(walk.Session.Map.Visited), "the walk is the map's visited list");
			var expected = walk.Nodes.SelectMany(id => new[] { "enter " + id, "complete " + id }).Append("end Won row 9");
			Assert.True(walk.Events.SequenceEqual(expected), $"events in order, got {string.Join(", ", walk.Events)}");
			Assert.Equal(1, walk.Events.Count(e => e == "enter boss-tanuki"), "T1 hook: the boss node is entered exactly once");
			Assert.True(walk.Session.Run.Owned.Any(o => o.Special.Level > 1 || o.Special.Modifier != null || !o.Special.Data.Starter), "the picks are on the run");

			// The same seed and picks replay the same run, fight for fight; another seed drafts differently somewhere.
			int i = 0;
			var twin = Play(77, _ => picks[i++]);
			Assert.Equal(walk.Hash, twin.Hash, $"run {runs}: deterministic hash");
			Assert.True(walk.Health.SequenceEqual(twin.Health), "same health at every node");
		}
		Assert.True(runs >= 3, "the default map needs several walks to cover every node");
		Assert.True(Play(77, r => r[0]).Hash != Play(77, r => r[^1]).Hash, "another route, another hash");
		Assert.True(Play(77, r => r[0]).Hash != Play(77, r => r[0], hits: 0).Hash, "another fight, another hash");
	});

	[Test]
	public static void Session_HealthCarries_LossEndsTheRun_RefusedStepsChangeNothing() => OnFixtures(() =>
	{
		var session = RunSession.Start(FightScene.LoadAbilityPool(), DefaultMap(), Table(), 5);
		RunEnd? ended = null;
		session.RunEnded += e => ended = e;
		ulong fresh = session.StateHash();
		Assert.True(!session.Enter("r2-oni").Ok && !session.Enter("nowhere").Ok && !session.CompleteNode().Ok && !session.LoseFight().Ok, "refused: not reachable, unknown, nothing entered");
		Assert.True(session.StateHash() == fresh && session.Node == null, "a refused step changes nothing");
		Throws<InvalidOperationException>(() => session.WinFight(500), "no fight to win on the map");

		session.Enter("r1-apprentice");
		Assert.True(session.Entered!.Scene == "res://scenes/fight.tscn" && session.Node!.Tier == "apprentice", "the entered node's scene and context");
		var draft = session.WinFight(400)!;
		Assert.Equal(450, session.Run.Health, "R3: 400 left + 50");
		Throws<InvalidOperationException>(() => session.WinFight(400), "a node is won once");
		draft.Pick(0, draft.Offer.Cards[0].Targets.Count > 0 ? draft.Offer.Cards[0].Targets[0].Slot : null);
		session.CompleteNode();
		Assert.True(session.Reachable().SequenceEqual(new[] { "r2-kitsune", "r2-oni", "r2-kappa" }), "row 2 opens");

		session.Enter("r2-kappa");
		Assert.True(session.Node!.StandIn && session.Node.Fighter == "kitsune" && session.Node.Yokai == "kappa", "no Kappa kit yet: the fallback fights, the node still says Kappa");
		Assert.True(session.LoseFight().Ok, "lost");
		Assert.True(ended is { Won: false, RowReached: 2, NodeId: "r2-kappa" } && session.IsOver, "the run ends where it was lost");
		Assert.Equal(450, session.Run.Health, "the run's carried health is as it was going in");
		Assert.True(session.Reachable().Count == 0 && !session.Enter("r3-kappa").Ok && !session.CompleteNode().Ok && !session.LoseFight().Ok, "nothing after a lost run");

		// A stand-in stop: no heal, no draft, straight on.
		var stop = RunSession.Start(FightScene.LoadAbilityPool(), DefaultMap(), Table(), 5);
		foreach (string id in new[] { "r1-apprentice", "r2-kitsune", "r3-oni" })
		{
			stop.Enter(id);
			var d = stop.WinFight(300)!;
			d.Pick(0, d.Offer.Cards[0].Targets.Count > 0 ? d.Offer.Cards[0].Targets[0].Slot : null);
			stop.CompleteNode();
		}
		stop.Enter("r4-merchant");
		Assert.True(stop.Node!.Yokai == null && stop.Node.StandIn && !stop.Node.Binds, "merchant: a stand-in fight");
		Assert.True(stop.WinFight(300) == null && stop.Run.Health == 300, "no draft and no heal on a stand-in stop");
		Assert.True(stop.CompleteNode().Ok && stop.Reachable().Count == 2, "and the elders open");
	});

	// ---- the boot flow with the real map screen ----

	static void WithBoot(Node runner, Action<GameFlow> body, string? mapFile = null)
	{
		var saved = FightScene.Sources;
		var savedRun = FightScene.Run;
		var savedFoe = FightScene.Opponent;
		FightScene.Sources = FightScene.FixtureSources;
		FightScene.Run = null;
		MenuSettings.Reset();
		var flow = GD.Load<PackedScene>("res://scenes/boot.tscn").Instantiate<GameFlow>();
		flow.QuitAction = () => { };
		flow.FixedRunSeed = 77;
		if (mapFile != null) flow.MapFile = mapFile;
		flow.ConfigureFight = f => f.ExternalDrive = true;
		try { runner.AddChild(flow); body(flow); }
		finally
		{
			runner.RemoveChild(flow);
			flow.QueueFree();
			MenuSettings.Reset();
			FightScene.Sources = saved;
			FightScene.Run = savedRun;
			FightScene.Opponent = savedFoe;
		}
	}

	static void Press(Button b) => b.EmitSignal(BaseButton.SignalName.Pressed);

	static void Win(FightScene s, int taken)
	{
		for (int t = 0; t < taken * 2; t++) s.Step(Idle, Strike(t));
		for (int t = 0; t < 900 && s.Match.Phase != MatchPhase.Over; t++) s.Step(Strike(t), Idle);
		Assert.True(s.Match.Phase == MatchPhase.Over && s.Match.Winner == 0, "Ryo won");
	}

	[Test]
	public static void Boot_TitleControlSelectMap_Fight_BackToTheMap_ThenALossAndANewRun(Node runner) => WithBoot(runner, flow =>
	{
		Press(flow.Title.StartButton);
		Assert.True(flow.Session == null && !flow.Map.Visible, "no run and no map before Start Game");
		Press(flow.ControlSelect.Start);
		Assert.Equal(Screen.Map, flow.Router.Current, "control select leads to the map");
		var session = flow.Session!;
		Assert.True(flow.Map.Visible && flow.Fight == null && !flow.Title.Visible && !flow.ControlSelect.Visible, "the map screen is up, no fight yet");
		Assert.True(session.Map.Graph.Id == "default-map" && session.Run.Health == RunState.MaxHealth && session.Seed == 77, "a fresh run on the default map");
		Assert.True(flow.Map.ReachableIds.SequenceEqual(new[] { "r1-apprentice" }) && flow.Map.CursorId == "r1-apprentice", "the screen shows the run: row 1 is reachable");
		Assert.True(!flow.ChooseNode("r2-oni").Ok && flow.Router.Current == Screen.Map && session.Node == null, "a node off the route is refused");

		int entered = 0;
		session.NodeEntered += _ => entered++;
		Assert.True(flow.Map.Confirm(), "confirm on the map screen");
		Assert.Equal(1, entered, "the node is entered exactly once (screen pick + flow)");
		Assert.Equal(Screen.Fight, flow.Router.Current, "the node's scene runs");
		var first = flow.Fight!;
		Assert.True(!flow.Map.Visible && first.IsInsideTree() && ReferenceEquals(first, flow.NodeScene), "fight up, map hidden");
		Assert.True(ReferenceEquals(first.Session, session) && !first.OwnsSession && first.Node.NodeId == "r1-apprentice", "the fight plays the session's node");
		Assert.True(first.Duel == 1 && first.Duels == 9, "fight 1 of 9");
		Assert.Equal(30, first.P2Ai!.ReactionFrames, "Y4: the apprentice row reacts in 30 frames");
		Assert.Equal(30, first.Recorder!.Recording.Header.Ai!.ReactionFrames, "and the recording replays with it");

		Win(first, taken: 2);
		Assert.True(first.Stage == DemoStage.Binding && session.Run.Health == 850, "won: binding card, R3 800 + 50");
		first.BindingCard.Continue();
		Assert.True(first.Stage == DemoStage.Reward && flow.Router.Current == Screen.Fight, "reward screen, still on the node");
		// The pick as a real key press: the screen's scene leaves the tree inside its own input handler (found by running the game).
		var enter = new InputEventKey { Keycode = Key.Enter, Pressed = true };
		first.Rewards._UnhandledInput(enter); // the first card
		if (first.Stage == DemoStage.Reward) first.Rewards._UnhandledInput(enter); // its target, when it asks for one

		Assert.Equal(Screen.Map, flow.Router.Current, "after the pick: back to the map");
		Assert.True(flow.Fight == null && !first.IsInsideTree() && first.IsQueuedForDeletion() && first.Stage == DemoStage.Left, "the fight scene is gone");
		Assert.True(ReferenceEquals(session, flow.Session) && session.Map.Visited.SequenceEqual(new[] { "r1-apprentice" }), "the same run survived the scene change");
		Assert.True(flow.Map.Visible && !flow.Map.Locked && flow.Map.ReachableIds.SequenceEqual(new[] { "r2-kitsune", "r2-oni", "r2-kappa" }), "the map shows row 2 and takes a pick again");
		var owned = session.Run.Owned.Select(o => (Id: o.Special.Data.Id, Level: o.Special.Level, Modifier: o.Special.Modifier?.Id)).ToList();

		flow.Map.Move(1); // r2-oni: no Oni kit yet
		Assert.True(flow.Map.Confirm(), "second pick");
		var second = flow.Fight!;
		Assert.True(!ReferenceEquals(first, second) && second.Node.NodeId == "r2-oni" && second.Node.StandIn && FightScene.Opponent == "kitsune", "a new scene for the Oni node, fought by the fallback kit");
		Assert.True(second.Match.P1.Health == 850 && second.Match.Tick == 0 && second.Duel == 2, "C1: the carried health, fight 2");
		Assert.Equal(22, second.P2Ai!.ReactionFrames, "Y4: yokai row");
		Assert.True(session.Run.Owned.Select(o => (Id: o.Special.Data.Id, Level: o.Special.Level, Modifier: o.Special.Modifier?.Id)).SequenceEqual(owned)
			&& owned.All(o => second.Match.P1.Specials.Any(e => e != null && e.Data.Id == o.Id && e.Level == o.Level)), "the draft pick came along");

		for (int t = 0; t < 900 && second.Match.Phase != MatchPhase.Over; t++) second.Step(Idle, Strike(t));
		Assert.True(second.Stage == DemoStage.Lost && session.Ended is { Won: false, RowReached: 2 }, "lost: the run ends on row 2");
		Assert.True(flow.Router.Current == Screen.Fight && second.IsInsideTree(), "the lose screen stays up");
		second.RestartRun(); // the lose screen's Restart
		Assert.True(flow.Router.Current == Screen.Map && flow.Fight == null && flow.Map.Visible, "Restart: back on the map");
		Assert.True(!ReferenceEquals(session, flow.Session) && flow.Session!.Run.Health == RunState.MaxHealth && flow.Session.Map.Visited.Count == 0, "of a new run");
		Assert.True(flow.Map.ReachableIds.SequenceEqual(new[] { "r1-apprentice" }) && !flow.Map.Locked, "from row 1");

		Assert.True(flow.ReturnToTitle(), "the map can be left for the title");
		Assert.True(flow.Session == null && !flow.Map.Visible && flow.Title.Visible, "title: the run is dropped");
	});

	[Test]
	public static void Boot_LastNodeWon_ShowsTheEndCard_AndPauseQuitDropsTheRun(Node runner) => WithBoot(runner, flow =>
	{
		Press(flow.Title.StartButton);
		Press(flow.ControlSelect.Start);
		var session = flow.Session!;
		Assert.Equal("demo-map", session.Map.Graph.Id, "the map is the flow's MapFile");
		flow.Map.Confirm();
		Win(flow.Fight!, taken: 0);
		flow.Fight!.BindingCard.Continue();
		flow.Fight.Rewards.Confirm();
		if (flow.Fight?.Rewards.Step == RewardStep.Target) flow.Fight.Rewards.Confirm();
		Assert.Equal(Screen.Map, flow.Router.Current, "map between the two nodes");
		flow.Map.Confirm();
		var last = flow.Fight!;
		Win(last, taken: 0);
		Assert.True(session.Ended is { Won: true, RowReached: 2 } && session.Map.IsFinished, "the run is won on its last node");
		Assert.True(flow.Router.Current == Screen.Fight && last.Stage == DemoStage.Complete && last.CompleteScreen.Visible, "the end card shows on the node's scene, no map");
		Assert.True(last.CompleteScreen.TitleText == FightScene.RunWonTitle && !last.BindingCard.Showing && !last.Rewards.Showing, "placeholder end card, no draft after the last node");
		Press(last.CompleteScreen.Restart);
		Assert.True(flow.Router.Current == Screen.Map && !ReferenceEquals(session, flow.Session) && flow.Session!.Map.Visited.Count == 0, "Restart: a new run on the map");

		flow.Map.Confirm();
		Assert.True(flow.Fight!.RequestTitle(), "pause menu's Quit to title");
		Assert.True(flow.Router.Current == Screen.Title && flow.Session == null && flow.Fight == null, "title, run dropped");
	}, mapFile: FightScene.DemoMapFile);

	[Test]
	public static void FightSceneOnItsOwn_WalksTheDemoMap_AsOneConfigurationOfTheFlow(Node runner) => OnFixtures(() =>
	{
		var s = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		s.ExternalDrive = true;
		s.FixedRunSeed = 48;
		runner.AddChild(s);
		try
		{
			Assert.True(s.OwnsSession && s.Session!.Map.Graph.Id == "demo-map" && s.Node.NodeId == "demo-kitsune" && s.Duels == 2, "its own session on the demo map");
			Assert.True(ReferenceEquals(s.DemoRun, s.Session.Run) && s.Session.Seed == 48, "the run is the session's");
			Win(s, taken: 1);
			s.BindingCard.Continue();
			s.Rewards.Confirm();
			if (s.Rewards.Step == RewardStep.Target && s.Stage == DemoStage.Reward) s.Rewards.Confirm();
			Assert.True(s.Stage == DemoStage.Fighting && s.Node.NodeId == "demo-rematch" && s.Duel == 2, "no map screen: straight into the next node");
			Win(s, taken: 0);
			Assert.True(s.Stage == DemoStage.Complete && s.Session.Ended is { Won: true }, "the demo ends as a won run");
			var old = s.Session;
			s.RestartRun();
			Assert.True(!ReferenceEquals(old, s.Session) && s.Node.NodeId == "demo-kitsune" && s.Duel == 1 && s.Stage == DemoStage.Fighting, "Restart: a new session, first node");
		}
		finally { runner.RemoveChild(s); s.QueueFree(); }
	});
}
