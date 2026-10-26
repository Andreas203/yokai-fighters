using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using YokaiFighters.Flow;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>
/// Test Breaker probes for YOK-108 (map screen mockup). Expected values come from R1 (one pick per row), R8 (coins), C1 (health),
/// M5/AM3 (Continue restarts the entered node, the pick stays committed). Few screens are built (harness limit): one per screen probe.
/// </summary>
public static class MapScreenProbes
{
	static string MapDir => ContentPaths.Data("map");
	static MapGraph Graph() => FileMapSource.Default(MapDir).Load();
	static RunSession NewSession() => RunSession.Start(YokaiFighters.Fight.FightScene.LoadAbilityPool(), Graph(), NodeSceneTable.Default(MapDir), 5);

	static MapScreen Make(Node host, IMapView view)
	{
		var s = GD.Load<PackedScene>("res://scenes/ui/map_screen.tscn").Instantiate<MapScreen>();
		var stage = new Control { Size = new Vector2(1920, 1080) };
		host.AddChild(stage);
		stage.AddChild(s);
		s.SetView(view);
		return s;
	}

	static void Press(MapScreen s, string action) => s._UnhandledInput(new InputEventAction { Action = action, Pressed = true });

	static void Finish(RunSession s)
	{
		if (s.Node!.Type is not ("merchant" or "dojo") && s.WinFight(840) is { } d) { var c = d.Offer.Cards[0]; d.Pick(0, c.Targets.Count > 0 ? c.Targets[0].Slot : null); }
		s.CompleteNode();
	}

	static void Walk(RunSession s, params string[] ids) { foreach (var id in ids) { s.Enter(id); Finish(s); } }

	// R1: after any pick on any path, the picked row has exactly one Current/Visited node, its row-mates are PassedOver, no earlier row has a
	// Reachable/Locked node, and a PassedOver node is never Reachable.
	static void CheckStates(RunMap map, bool entered)
	{
		var nodes = new RunMapView(map).Nodes;
		int cur = map.CurrentNode?.Row ?? 0;
		foreach (var g in nodes.GroupBy(n => n.Row).Where(g => g.Key <= cur))
		{
			Assert.Equal(1, g.Count(n => n.State is MapNodeState.Visited or MapNodeState.Current), $"row {g.Key}: one node taken (R1)");
			Assert.True(g.All(n => n.State is MapNodeState.Visited or MapNodeState.Current or MapNodeState.PassedOver), $"row {g.Key}: the rest passed over, never reachable or later");
		}
		Assert.True(nodes.Where(n => n.Row > cur).All(n => n.State is MapNodeState.Reachable or MapNodeState.Locked), "rows ahead are never passed over");
		Assert.True(nodes.Where(n => n.State == MapNodeState.PassedOver).All(n => !map.CanSelect(n.Id)), "passed over can't be selected (R1)");
		Assert.True(nodes.Count(n => n.State == MapNodeState.Current) <= 1, "one Current");
		if (entered) Assert.True(!nodes.Any(n => n.State == MapNodeState.Reachable), "nothing reachable inside a node");
	}

	static int paths;
	static void Dfs(MapGraph g, RunMap map)
	{
		foreach (var id in map.Reachable())
		{
			var snap = map.Snapshot();
			var next = RunMap.Restore(g, snap);
			Assert.True(next.Select(id).Ok, "pick " + id);
			CheckStates(next, true);
			Assert.True(next.Complete(id).Ok, "complete " + id);
			CheckStates(next, false);
			if (next.Reachable().Count == 0) paths++; else Dfs(g, next);
		}
	}

	[Test]
	public static void PassedOverIsRightAfterEveryPossiblePickOnEveryPath()
	{
		// R1: every route through the default map, entered and completed
		paths = 0;
		var g = Graph();
		Dfs(g, new RunMap(g));
		Assert.True(paths >= 12, "walked many paths: " + paths);
	}

	[Test]
	public static void RefusedPicksLeaveTheMapAndItsHashUntouched()
	{
		// R1: only a reachable node can be entered
		var g = Graph();
		var map = new RunMap(g); var view = new RunMapView(map);
		map.Select("r1-apprentice"); map.Complete("r1-apprentice"); map.Select("r2-oni"); map.Complete("r2-oni");
		ulong h = map.StateHash(); var snap = map.Snapshot();
		foreach (var n in view.Nodes.Where(n => n.State is not MapNodeState.Reachable))
			Assert.True(!view.Select(n.Id), $"{n.Id} ({n.State}) refused");
		Assert.True(!view.Select("nope"), "unknown id refused");
		Assert.Equal(h, map.StateHash(), "hash unchanged by refusals");
		Assert.Equal(snap.Current, map.Snapshot().Current, "current unchanged");
		Assert.True(view.Detail("nope") == null, "detail of an unknown node is null, no throw");
		var before = view.Status; _ = view.Nodes; _ = view.CopyTarget;
		Assert.Equal(h, map.StateHash(), "reads do not change the hash");
	}

	[Test]
	public static void ContinueMidNodeShowsTheCommittedPickAndRowsDone()
	{
		// M5/AM3: Continue restarts the entered node; the pick stays committed (R1). A restored walk must read like the live one.
		var g = Graph();
		var live = new RunMap(g);
		live.Select("r1-apprentice"); live.Complete("r1-apprentice"); live.Select("r2-kappa"); // entered, not finished
		var restored = RunMap.Restore(g, live.Snapshot());
		var a = new RunMapView(live).Nodes; var b = new RunMapView(restored).Nodes;
		Assert.True(a.SequenceEqual(b), "restored map shows the same states as the live one");
		Assert.Equal(MapNodeState.Current, b.First(n => n.Id == "r2-kappa").State, "here");
		Assert.Equal(MapNodeState.PassedOver, b.First(n => n.Id == "r2-oni").State, "row-mates stay passed over after Continue");
		var run = RunState.NewRun(YokaiFighters.Fight.FightScene.LoadAbilityPool(), g);
		Assert.Equal(1, new RunMapView(restored, run).Status!.RowsDone, "an entered, unfinished row is not done: 1 of 8");
		Assert.Equal(1, new RunMapView(live, run).Status!.RowsDone, "same live");
	}

	[Test]
	public static void RowsDoneNeverCountsTheBossRow()
	{
		// R1: 8 rows then the Tanuki; the status reads rows of 8
		var sess = NewSession();
		var view = new RunMapView(sess.Map, sess.Run, sess.Scenes);
		Assert.Equal("0/8", $"{view.Status!.RowsDone}/{view.Status.RowsTotal}", "new run");
		Assert.True(view.Status.RowsDone == 0 && view.Status.LastPlayRow == 8, "last play row is 8");
		Walk(sess, "r1-apprentice", "r2-oni", "r3-kappa", "r4-merchant", "r5-elder-oni", "r6-kitsune", "r7-dojo");
		Assert.Equal(7, view.Status.RowsDone, "7 done");
		sess.Enter("r8-elder-kappa");
		Assert.Equal(7, view.Status.RowsDone, "entered row 8 is not yet done");
		Finish(sess);
		Assert.Equal(8, view.Status.RowsDone, "8 of 8 before the boss");
		sess.Enter("boss-tanuki");
		Assert.Equal(8, view.Status.RowsDone, "in the boss node: still 8 of 8");
		Assert.Equal(8, view.Status.RowsTotal, "total never becomes 9");
	}

	[Test]
	public static void RewardLinesAgreeWithR8AndWithWhatIsActuallyPaid()
	{
		// R8: 25 per duel on rows 1-6, 50 for the row-5 elder; the row-8 elder pays a rare ability instead. PROVISIONAL (YOK-186): when
		// its offer holds no rare it pays what the row-5 elder pays, 50, and its map line says so.
		const string R8Line = "Pays a rare ability, or 50 coins if its offer has none.";
		var sess = NewSession();
		var view = new RunMapView(sess.Map, sess.Run, sess.Scenes);
		foreach (var n in sess.Map.Graph.Nodes.Where(n => n.Type is "duel" or "elder"))
		{
			string line = view.Detail(n.Id)!.Reward!;
			if (n.Row == 8) Assert.True(line.StartsWith(R8Line), $"{n.Id} reads: {line}");
			else Assert.True(line.StartsWith($"Pays {(n.Row == 5 ? 50 : 25)} coins."), $"{n.Id} reads: {line}");
		}
		// and the line is what the win pays. The walk takes each node's card 0, which for an elder is its cancel rule (X1): Oni Chain
		// at row 5, so the row-8 Elder Oni then has no rare left (pays 50, as its line says) while the row-8 Elder Kappa's Undertow is locked (50).
		foreach (var (id, want) in new[] { ("r1-apprentice", 25), ("r2-oni", 25), ("r3-kappa", 25), ("r4-merchant", 0), ("r5-elder-oni", 50), ("r6-kappa", 25), ("r7-dojo", 0), ("r8-elder-oni", 50) })
		{
			int before = sess.Run.Coins;
			sess.Enter(id); Finish(sess);
			Assert.Equal(want, sess.Run.Coins - before, $"{id} paid");
		}
		// The same line over a walk that leaves Oni Chain on offer at row 8: the line names the rare, and the win pays no coins.
		var other = NewSession();
		Walk(other, "r1-apprentice", "r2-oni", "r3-kappa", "r4-merchant", "r5-nine-tails", "r6-kappa", "r7-dojo"); // Fox Step taken, Oni Chain not
		Assert.True(!other.Run.HoldsCancelRule("oni-chain") && other.Run.HoldsCancelRule("fox-step"), "setup: Fox Step, not Oni Chain");
		Assert.True(new RunMapView(other.Map, other.Run, other.Scenes).Detail("r8-elder-oni")!.Reward!.StartsWith(R8Line), "line at the row-8 Elder Oni");
		int b4 = other.Run.Coins;
		other.Enter("r8-elder-oni");
		var offer = other.WinFight(840)!;
		Assert.True(offer.Offer.Cards.Any(c => c.AbilityId == "oni-chain" && c.Rare) && other.Run.Coins == b4, "Oni Chain on offer, no coins");
	}

	[Test]
	public static void RowHeadersAndDetailWhenRowValuesSkip(Node host)
	{
		// R1 rows only need to ascend: play rows 2, 5, 9 with the boss on row 20 -> "AFTER ROW 9", "row 3 of 3 done" style counts follow the count
		var v = new StubMapView().Add("a", 2, "duel", "A").Add("b", 5, "elder", "B", true).Add("c", 9, "duel", "C").Add("d", 20, "boss", "D")
			.Route("a", "b").Route("b", "c").Route("c", "d").Open("a");
		v.Status = new MapStatusView(1000, 1000, 0, 0, 3) { LastPlayRow = 9 };
		var s = Make(host, v);
		var h = s.HeaderTexts;
		Assert.Equal("ROW 2|ROW 5 · ELDER|ROW 9|AFTER ROW 9", string.Join("|", h), "headers use row values, boss column after the last play row");
		s.GetParent().QueueFree();
	}

	[Test]
	public static void CursorNeverLandsOnPassedOverOrLockedAndInputIsDeadWhileLocked(Node host)
	{
		var sess = NewSession();
		var view = new SessionMapView(sess);
		Walk(sess, "r1-apprentice", "r2-oni", "r3-kappa", "r4-merchant");
		var s = Make(host, view); // one shared screen for the whole probe
		var chosen = new List<string>(); s.NodeChosen += id => chosen.Add(id);
		ulong h0 = sess.StateHash();
		var states = view.Nodes.ToDictionary(n => n.Id, n => n.State);
		for (int i = 0; i < 7; i++)
		{
			Assert.Equal(MapNodeState.Reachable, states[s.CursorId!], "cursor on a reachable node");
			Press(s, i % 2 == 0 ? "ui_down" : "ui_up");
			Press(s, "ui_right"); Press(s, "ui_left");
		}
		// every passed-over / locked / visited node ignores a forced press and a hover
		foreach (var n in s.Nodes.Where(n => n.State != MapNodeState.Reachable))
		{
			string cursor = s.CursorId!;
			n.EmitSignal(Control.SignalName.MouseEntered);
			Assert.Equal(cursor, s.CursorId, $"hover on {n.NodeId} ({n.State}) does not move the cursor");
			n.EmitSignal(BaseButton.SignalName.Pressed);
		}
		Assert.Equal(0, chosen.Count, "no signal from non-reachable nodes");
		Assert.Equal(h0, sess.StateHash(), "reads, moves, hovers and refused presses leave the hash alone");
		Assert.True(s.FocusId != null && states[s.FocusId] == MapNodeState.Reachable, "detail focus is the cursor");

		// pick, then the screen is locked: the detail must not follow arrow keys that do nothing
		Press(s, "ui_accept");
		Assert.Equal(1, chosen.Count, "one pick");
		ulong h1 = sess.StateHash();
		string name = s.DetailNameText, reward = s.DetailRewardText, cursorAfter = s.CursorId!;
		for (int i = 0; i < 4; i++) { Press(s, "ui_down"); Press(s, "ui_up"); Press(s, "ui_accept"); }
		Assert.Equal(1, chosen.Count, "Enter while locked does nothing");
		Assert.Equal(cursorAfter, s.CursorId, "cursor frozen while locked");
		Assert.Equal(name, s.DetailNameText, "detail name frozen while locked");
		Assert.Equal(reward, s.DetailRewardText, "detail reward frozen while locked");
		Assert.Equal(h1, sess.StateHash(), "locked input changes nothing in the run");
		s.GetParent().QueueFree();
	}

	[Test]
	public static void ContinueMidNodeScreenAndTheBossColumn(Node host)
	{
		// M5: a screen built over a restored mid-node walk (Ryo inside r5): here + passed over, rows done 4 of 8, detail on the node Ryo is on.
		var sess = NewSession();
		Walk(sess, "r1-apprentice", "r2-oni", "r3-kappa", "r4-merchant");
		sess.Enter("r5-nine-tails");
		var restored = RunMap.Restore(sess.Map.Graph, sess.Map.Snapshot());
		var s = Make(host, new RunMapView(restored, sess.Run, sess.Scenes));
		Assert.Equal(MapNodeState.PassedOver, s.NodeFor("r5-elder-oni")!.State, "the other elder is passed over after Continue");
		Assert.True(s.NodeFor("r5-elder-oni")!.Disabled, "and can't be picked");
		Assert.Equal("row 4 of 8 done", s.StatusRowsText, "entered row 5 is not done");
		Assert.True(s.DetailNameText.StartsWith("NINE") && s.DetailNameText.EndsWith("row 5"), "detail shows the node Ryo is in: " + s.DetailNameText);
		Assert.True(s.DetailRewardText.StartsWith("Pays 50 coins"), "row 5 elder pays 50: " + s.DetailRewardText);
		Press(s, "ui_down"); Press(s, "ui_accept");
		Assert.True(sess.Map.Current == "r5-nine-tails", "no pick possible inside a node");
		// reuse the same screen for the boss row and a row-8 elder (pays 0)
		var done = NewSession();
		Walk(done, "r1-apprentice", "r2-oni", "r3-kappa", "r4-merchant", "r5-elder-oni", "r6-kitsune", "r7-dojo");
		s.SetView(new SessionMapView(done));
		Assert.Equal("AFTER ROW 8", s.HeaderTexts[^1], "boss column");
		// R8, PROVISIONAL (YOK-186): the row-8 elder's line names the rare and the coins it falls back on, not "Pays no coins."
		Assert.True(s.DetailRewardText.StartsWith("Pays a rare ability, or 50 coins if its offer has none."), "row-8 elder line (R8): " + s.DetailRewardText);
		Assert.True(s.DetailNameText.EndsWith("row 8"), "row 8 not 'after row 8': " + s.DetailNameText);
		Walk(done, "r8-elder-kappa");
		s.SetView(new SessionMapView(done));
		Assert.Equal("row 8 of 8 done", s.StatusRowsText, "all play rows done");
		Assert.True(s.DetailNameText.Contains("TANUKI") || s.DetailNameText.StartsWith("ELDER KAPPA"), "detail follows cursor or here: " + s.DetailNameText);
		s.GetParent().QueueFree();
	}
}
