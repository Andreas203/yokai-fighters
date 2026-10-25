using System.Collections.Generic;
using System.Linq;
using Godot;
using YokaiFighters.Flow;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>YOK-78: map screen against the stub view (every node and route, reachable-only selection, one signal per confirm, any size) and the RunMap adapter.</summary>
public static class MapScreenTests
{
	static MapScreen Make(Node host, IMapView view, bool viewFirst = false)
	{
		var s = GD.Load<PackedScene>("res://scenes/ui/map_screen.tscn").Instantiate<MapScreen>();
		var stage = new Control { Size = new Vector2(1920, 1080) }; // the screen's real canvas, so its anchors resolve to 1920x1080
		host.AddChild(stage);
		if (viewFirst) s.SetView(view);
		stage.AddChild(s);
		if (!viewFirst) s.SetView(view);
		return s;
	}

	static void Press(MapScreen s, string action) => s._UnhandledInput(new InputEventAction { Action = action, Pressed = true });

	static StubMapView Fork()
	{
		var v = new StubMapView()
			.Add("a", 1, "duel", "Kitsune").Add("b", 2, "duel", "Oni").Add("c", 2, "elder", "Elder Kappa", true).Add("d", 2, "shrine", null).Add("e", 3, "boss", "Tanuki")
			.Route("a", "b").Route("a", "c").Route("a", "d").Route("b", "e").Route("c", "e").Route("d", "e");
		return v.Open("a");
	}

	[Test]
	public static void StubRendersEveryNodeAndRoute(Node host)
	{
		var v = Fork();
		var s = Make(host, v);
		Assert.Equal(5, s.Nodes.Count, "a node per stub node");
		Assert.Equal(6, s.Routes.Lines.Count, "a line per stub route");
		Assert.Equal("Duel", s.NodeFor("a")!.TypeText, "type label from the styles resource");
		Assert.Equal("Kitsune", s.NodeFor("a")!.YokaiText, "the yokai that waits (R2)");
		Assert.True(s.NodeFor("c")!.ElderShown && !s.NodeFor("b")!.ElderShown, "elder marker only on the elder node (Y3)");
		Assert.Equal("Shrine", s.NodeFor("d")!.TypeText, "unknown type still renders, with its id as label");
		Assert.Equal("Shrine", s.NodeFor("d")!.YokaiText, "no yokai: the node shows its type name, never a blank");
		Assert.True(s.NodeFor("a")!.State == MapNodeState.Reachable && s.NodeFor("e")!.State == MapNodeState.Locked, "states shown");
		foreach (var (a, b, _) in s.Routes.Lines) Assert.True(a.X < b.X, "routes run forward");
		var r = s.RectOf("a");
		var line = s.Routes.Lines.First();
		Assert.True(Mathf.IsEqualApprox(line.A.X, r.End.X) && Mathf.IsEqualApprox(line.A.Y, r.GetCenter().Y), "route starts on its node's edge");
		s.GetParent().QueueFree();
	}

	[Test]
	public static void ViewCanBeHandedInBeforeTheScreenEntersTheTree(Node host)
	{
		var s = Make(host, Fork(), viewFirst: true);
		Assert.Equal(5, s.Nodes.Count, "nodes built when the view came first");
		s.GetParent().QueueFree();
	}

	[Test]
	public static void SelectionOnlyAmongReachableAndConfirmFiresOnce(Node host)
	{
		var v = Fork();
		var s = Make(host, v);
		var fired = new List<string>();
		s.NodeChosen += id => fired.Add(id);
		Assert.True(s.NodeFor("b")!.Disabled && !s.NodeFor("a")!.Disabled, "locked node button disabled");
		s.NodeFor("e")!.EmitSignal(BaseButton.SignalName.Pressed); // even a forced press on a locked node is refused
		Assert.Equal(0, fired.Count, "locked node cannot be chosen");
		Press(s, "ui_accept"); Press(s, "ui_accept");
		Assert.Equal("a", string.Join(",", fired), "confirm fires once, then locks");
		Assert.True(s.Locked && s.NodeFor("a")!.State == MapNodeState.Current, "screen re-read the state after the pick");
		Assert.Equal(1, v.Selected.Count, "the view saw one Select");
		v.Complete();
		s.Refresh();
		Assert.Equal("b,c,d", string.Join(",", s.ReachableIds), "next row opens");
		Press(s, "ui_down"); Press(s, "ui_down");
		Assert.Equal("d", s.CursorId, "cursor steps through reachable nodes only");
		Press(s, "ui_down");
		Assert.Equal("b", s.CursorId, "cursor wraps");
		s.NodeFor("c")!.EmitSignal(BaseButton.SignalName.Pressed); // mouse click on a reachable node
		Assert.Equal("a,c", string.Join(",", fired), "click picks that node");
		Assert.True(!s.Confirm(), "locked after a pick");
		s.NodeFor("c")!.EmitSignal(BaseButton.SignalName.Pressed);
		Assert.Equal(2, fired.Count, "a second click is ignored");
		s.GetParent().QueueFree();
	}

	[Test]
	public static void ViewRefusalRaisesNothing(Node host)
	{
		var v = Fork();
		var s = Make(host, v);
		int n = 0; s.NodeChosen += _ => n++;
		v.Select("a"); // view moved on without the screen: the cursor node is no longer selectable
		Assert.True(!s.Confirm(), "view refused");
		Assert.Equal(0, n, "no signal on refusal");
		s.GetParent().QueueFree();
	}

	[Test]
	public static void ThreeAndTwelveRowsLayOutInsideTheBoard(Node host)
	{
		foreach (var (rows, per) in new[] { (3, 2), (12, 3), (12, 5) })
		{
			var s = Make(host, StubMapView.Grid(rows, per));
			var rects = s.Nodes.Select(n => s.RectOf(n.NodeId)).ToList();
			var board = new Rect2(Vector2.Zero, s.Board.Size);
			Assert.True(board.Size.X > 1000, "board has a real size");
			Assert.True(rects.All(r => board.Encloses(r)), $"{rows}x{per}: every node inside the board");
			for (int i = 0; i < rects.Count; i++)
				for (int j = i + 1; j < rects.Count; j++)
					Assert.True(!rects[i].Intersects(rects[j]), $"{rows}x{per}: nodes {i} and {j} overlap");
			Assert.Equal(s.View!.Nodes.Count, s.Nodes.Count, $"{rows}x{per}: all nodes shown");
			Assert.Equal(s.View.Routes.Count, s.Routes.Lines.Count, $"{rows}x{per}: all routes drawn");
			Assert.Equal(rows, rects.Select(r => (int)r.Position.X).Distinct().Count(), $"{rows}x{per}: one column per row");
			s.GetParent().QueueFree();
		}
	}

	[Test]
	public static void LayoutIsDrivenByRowValuesNotCounts()
	{
		var nodes = new List<MapNodeView> { new("x", 10, "duel", null, false, MapNodeState.Locked), new("y", 40, "duel", null, false, MapNodeState.Locked), new("z", 40, "duel", null, false, MapNodeState.Locked) };
		var l = MapLayout.Compute(nodes, new Vector2(1000, 600));
		Assert.True(l.Rects["x"].Position.X < l.Rects["y"].Position.X && Mathf.IsEqualApprox(l.Rects["y"].Position.X, l.Rects["z"].Position.X), "sparse row values still make adjacent columns");
		Assert.True(l.Rects["y"].Position.Y < l.Rects["z"].Position.Y, "order within the row is the view's order");
		Assert.Equal(0, MapLayout.Compute(new List<MapNodeView>(), new Vector2(100, 100)).Rects.Count, "empty map is empty");
	}

	[Test]
	public static void RunMapAdapterShowsTheDefaultMap(Node host)
	{
		var graph = FileMapSource.Default(ContentPaths.Data("map")).Load();
		var run = new RunMap(graph);
		var view = new RunMapView(run);
		Assert.Equal(graph.Nodes.Count, view.Nodes.Count, "a node per graph node");
		Assert.Equal(graph.Routes.Count, view.Routes.Count, "a route per graph route");
		Assert.True(view.Nodes.Where(n => n.State == MapNodeState.Reachable).Select(n => n.Id).SequenceEqual(graph.StartNodes), "start nodes reachable first");
		Assert.True(view.Nodes.Where(n => n.State != MapNodeState.Reachable).All(n => n.State == MapNodeState.Locked), "everything else locked");
		Assert.True(!view.Select("boss-tanuki") && run.Current is null, "unreachable node refused, run untouched");

		var s = Make(host, view);
		Assert.Equal(graph.Nodes.Count, s.Nodes.Count, "screen shows the whole default map");
		Assert.True(s.Nodes.All(n => n.Size.X > 0), "nodes sized");
		Assert.Equal("Apprentice", s.NodeFor("r1-apprentice")!.YokaiText, "yokai name from the map data");
		Assert.True(s.NodeFor("r5-elder-oni")!.ElderShown, "elder flag from the map data");
		string? chosen = null; s.NodeChosen += id => chosen = id;
		Press(s, "ui_accept");
		Assert.Equal("r1-apprentice", chosen, "pick reaches the signal");
		Assert.Equal("r1-apprentice", run.Current, "RunMap entered the node");
		Assert.True(view.Nodes.All(n => n.State != MapNodeState.Reachable) && view.Nodes.First(n => n.Id == chosen).State == MapNodeState.Current, "nothing reachable while in the node; it is Current");
		run.Complete(chosen!);
		s.Refresh();
		Assert.Equal("r2-kitsune,r2-oni,r2-kappa", string.Join(",", s.ReachableIds), "routes out of the completed node open");
		Assert.Equal(MapNodeState.Current, view.Nodes.First(n => n.Id == chosen).State, "last node stays Current");
		Press(s, "ui_down"); Press(s, "ui_accept");
		Assert.Equal("r2-oni", chosen, "second pick");
		Assert.True(view.Nodes.First(n => n.Id == "r1-apprentice").State == MapNodeState.Visited, "earlier node Visited");
		s.GetParent().QueueFree();
	}

	static RunSession StartSession()
	{
		string md = ContentPaths.Data("map");
		return RunSession.Start(YokaiFighters.Fight.FightScene.LoadAbilityPool(), FileMapSource.Default(md).Load(), NodeSceneTable.Default(md), 5);
	}

	/// <summary>Walk the session through <paramref name="ids"/> (win each fight with 840 left, take card 0).</summary>
	static void Walk(RunSession s, params string[] ids)
	{
		foreach (var id in ids)
		{
			s.Enter(id);
			if (s.Node!.Type is not ("merchant" or "dojo"))
			{
				var d = s.WinFight(840);
				if (d != null) { var c = d.Offer.Cards[0]; d.Pick(0, c.Targets.Count > 0 ? c.Targets[0].Slot : null); }
			}
			s.CompleteNode();
		}
	}

	[Test]
	public static void EveryNodeShowsItsNameIncludingMerchantAndDojo(Node host)
	{
		var s = Make(host, new SessionMapView(StartSession()));
		Assert.True(s.Nodes.All(n => n.YokaiText != ""), "no node is nameless (R2)");
		Assert.Equal("Merchant", s.NodeFor("r4-merchant")!.YokaiText, "the merchant is named");
		Assert.Equal("Dojo", s.NodeFor("r7-dojo")!.YokaiText, "the dojo is named");
		s.GetParent().QueueFree();
	}

	[Test]
	public static void NodeStatesAndCaptionsOnTheDefaultMap(Node host)
	{
		var sess = StartSession();
		var s = Make(host, new SessionMapView(sess));
		Assert.Equal("selected", s.NodeFor("r1-apprentice")!.StateText, "the cursor node reads selected");
		Walk(sess, "r1-apprentice", "r2-oni");
		s.SetView(new SessionMapView(sess));
		Assert.Equal(MapNodeState.Visited, s.NodeFor("r1-apprentice")!.State, "cleared");
		Assert.Equal("cleared", s.NodeFor("r1-apprentice")!.StateText, "caption");
		Assert.Equal(MapNodeState.Current, s.NodeFor("r2-oni")!.State, "here");
		Assert.Equal("YOU ARE HERE", s.NodeFor("r2-oni")!.StateText, "caption");
		Assert.Equal(MapNodeState.PassedOver, s.NodeFor("r2-kitsune")!.State, "an unchosen node in the row Ryo left is passed over");
		Assert.Equal(MapNodeState.PassedOver, s.NodeFor("r2-kappa")!.State, "so is its sibling");
		Assert.Equal("passed over", s.NodeFor("r2-kitsune")!.StateText, "caption");
		Assert.True(s.NodeFor("r2-kitsune")!.Disabled, "a passed-over node cannot be picked");
		Assert.True(s.NodeFor("r3-oni")!.State == MapNodeState.Reachable && s.NodeFor("r3-kappa")!.State == MapNodeState.Reachable, "row 3 opens");
		Assert.True(s.NodeFor("r6-kitsune")!.State == MapNodeState.Locked && s.NodeFor("r6-kitsune")!.StateText == "", "later rows stay locked, no caption");
		s.GetParent().QueueFree();
	}

	[Test]
	public static void StatusRowHeadersDetailAndLegend(Node host)
	{
		var sess = StartSession();
		Walk(sess, "r1-apprentice", "r2-oni", "r3-kappa", "r4-merchant");
		sess.Run.SetHealth(840);
		var s = Make(host, new SessionMapView(sess));
		Assert.Equal("RYO · 840 / 1000", s.StatusHealthText, "health as the HUD words it (C1)");
		Assert.Equal($"{sess.Run.Coins} coins", s.StatusCoinsText, "coins (R8)");
		Assert.Equal("row 4 of 8 done", s.StatusRowsText, "rows done; the boss row is not counted");
		Assert.Equal(9, s.HeaderTexts.Count, "a header per column");
		Assert.Equal("ROW 1", s.HeaderTexts[0], "plain row");
		Assert.Equal("ROW 5 · ELDER", s.HeaderTexts[4], "elder row marked (R1, Y3)");
		Assert.Equal("ROW 8 · ELDER", s.HeaderTexts[7], "second elder row");
		Assert.Equal("AFTER ROW 8", s.HeaderTexts[8], "the boss column");
		Assert.True(s.DetailNameText.EndsWith("row 5") && s.DetailNameText.Contains("Elder duel"), "detail names the focused node: " + s.DetailNameText);
		Assert.True(s.DetailRewardText.Contains("Pays 50 coins") && s.DetailRewardText.Contains("rare"), "reward line from the node table: " + s.DetailRewardText);
		Assert.True(s.DetailTwist.Visible && s.DetailTwist.Text.StartsWith("Twist: "), "YOK-139: the focused elder's twist line shows: " + s.DetailTwist.Text);
		Assert.True(!s.DetailTwist.Text.Contains(TwistCopy.PlaceholderMark.Trim()), "YOK-109: the line is the story label, never a stand-in: " + s.DetailTwist.Text);
		Assert.Equal(6, s.LegendStateCount, "a swatch per node state");
		Assert.True(s.LegendTypesText.Contains("merchant") && s.LegendTypesText.Contains("dojo") && s.LegendTypesText.Contains("elder"), "legend types come from the styles resource");
		string before = sess.StateHash().ToString();
		Press(s, "ui_down");
		Assert.Equal("r5-nine-tails", s.CursorId, "down steps to the next route");
		Press(s, "ui_up");
		Assert.Equal("r5-elder-oni", s.CursorId, "up steps back");
		Assert.True(s.DetailNameText.StartsWith("ELDER ONI"), "the detail follows the cursor");
		Assert.Equal(before, sess.StateHash().ToString(), "moving the cursor leaves the run untouched");
		s.GetParent().QueueFree();
	}

	/// <summary>The story-data label of a twist, as the map words it.</summary>
	static string Label(string elder) => TwistCopy.Label(YokaiFighters.Fight.FightScene.Twists.For(elder, true)!, TwistCopy.Story).Text;

	[Test]
	public static void FocusPanelNamesTheTwistSealAndTheHabitFromData(Node host)
	{
		var sess = StartSession();
		Walk(sess, "r1-apprentice", "r2-oni", "r3-kappa", "r4-merchant");
		var s = Make(host, new SessionMapView(sess));
		Assert.Equal("r5-elder-oni", s.CursorId, "the cursor starts on the first reachable elder");
		Assert.True(s.DetailSeal.Visible && s.DetailSeal.Icon == "twist-armour", "YOK-109: the Elder Oni's armour seal beside its panel");
		Assert.Equal("Twist: " + Label("elder-oni"), s.DetailTwist.Text, "the red twist line is the twist's story label");
		var oni = RunMapView.Profiles!.First(p => p.Yokai == "oni" && p.Temperament == YokaiFighters.Ai.NodeTemperament.Roll(sess.Seed, "r5-elder-oni"));
		Assert.Equal("Habit to punish: " + RunMapView.HabitSentence(oni.Habit.Description), s.DetailHabit.Text, "the habit of the profile this node's Oni fights with (the fight's own pick)");
		Assert.True(s.DetailHabit.Visible, "habit line shown");
		Press(s, "ui_down");
		Assert.Equal("r5-nine-tails", s.CursorId, "down steps to the Nine-Tails");
		Assert.Equal("twist-reflect", s.DetailSeal.Icon, "its reflect seal");
		Assert.Equal("Twist: " + Label("nine-tailed-kitsune"), s.DetailTwist.Text, "its own label");
		Assert.True(s.DetailHabit.Text.StartsWith("Habit to punish: ") && s.DetailHabit.Text.Contains("jumps"), "the Kitsune profile's habit: " + s.DetailHabit.Text);
		s.GetParent().QueueFree();
	}

	[Test]
	public static void NoSealAndNoHabitLineWhereTheDataHasNone(Node host)
	{
		var v = Fork();
		v.Details["a"] = new MapNodeDetail("Pays 25 coins.");
		var s = Make(host, v);
		Assert.True(!s.DetailSeal.Visible && !s.DetailHabit.Visible && !s.DetailTwist.Visible, "a node with no twist or habit data shows neither line nor seal (no invented text)");
		var sess = StartSession();
		var view = new RunMapView(sess.Map, sess.Run, sess.Scenes, sess.Seed);
		Assert.True(view.Detail("r4-merchant")!.Habit == null && view.Detail("r7-dojo")!.Habit == null && view.Detail("boss-tanuki")!.Habit == null, "no habit for a merchant, a dojo or the boss");
		Assert.True(view.Detail("r2-oni")!.Habit != null && view.Detail("r1-apprentice")!.Habit != null, "a duel has one, the apprentice's own too");
		Assert.True(new RunMapView(sess.Map, sess.Run, sess.Scenes).Detail("r2-oni")!.Habit != null, "no seed given: still the profile of the 0 seed's roll, never a crash");
		Assert.Equal("Often jumps.", RunMapView.HabitSentence("Often jumps. Hit her."), "the habit is the first sentence of the description");
		Assert.Equal("Just this", RunMapView.HabitSentence("Just this"), "one sentence without a full stop stays whole");
		Assert.True(RunMapView.HabitSentence("  ") == null, "an empty description gives no line");
		s.GetParent().QueueFree();
	}

	[Test]
	public static void LegendListsEachElderTwistWithItsLabel(Node host)
	{
		var sess = StartSession();
		var s = Make(host, new SessionMapView(sess));
		var rows = s.LegendTwists;
		Assert.Equal(3, rows.Count, "one row per twist on the map (armour, reflect, grip)");
		Assert.Equal("twist-armour twist-grip twist-reflect", string.Join(" ", rows.Select(r => r.Icon).OrderBy(x => x)), "the three seals");
		Assert.Equal(Label("elder-oni"), rows.First(r => r.Icon == "twist-armour").Text, "armour row words the story label");
		Assert.Equal(Label("nine-tailed-kitsune"), rows.First(r => r.Icon == "twist-reflect").Text, "reflect row words the story label");
		Assert.Equal(Label("elder-kappa"), rows.First(r => r.Icon == "twist-grip").Text, "grip row words the story label");
		Assert.Equal(0, Make(host, Fork()).LegendTwists.Count, "a view with no twist data shows no twist rows");
		s.GetParent().QueueFree();
	}

	[Test]
	public static void BossCardSaysTheTanukiWaitsAndCopies(Node host)
	{
		var sess = StartSession();
		var s = Make(host, new SessionMapView(sess));
		var boss = s.NodeFor("boss-tanuki")!;
		Assert.Equal("The Tanuki", boss.YokaiText, "boss name (R2)");
		Assert.Equal("waits at the end\ncopies your best move", boss.TagText, "designer-approved boss lines, from the styles resource");
		Assert.Equal("", s.NodeFor("r2-oni")!.TagText, "other cards carry no extra lines");
		Assert.True(s.RectOf("boss-tanuki").Size.X > s.RectOf("r8-elder-oni").Size.X, "the boss card is a little larger");
		s.GetParent().QueueFree();
	}

	[Test]
	public static void WalkedRoutesAreTheBrushedOnes(Node host)
	{
		var sess = StartSession();
		Walk(sess, "r1-apprentice", "r2-oni", "r3-kappa", "r4-merchant");
		var s = Make(host, new SessionMapView(sess));
		Assert.Equal(3, s.Routes.Lines.Count(l => l.Kind == RouteKind.Walked), "the walked path is three routes");
		Assert.Equal(2, s.Routes.Lines.Count(l => l.Kind == RouteKind.Open), "the two routes out of Ryo are open");
		s.GetParent().QueueFree();
	}

	[Test]
	public static void TwistSlotShowsWhenTheViewGivesATwist(Node host)
	{
		var v = Fork();
		v.Details["a"] = new MapNodeDetail("Pays 25 coins.", "Reflects any special used twice in a row.");
		var s = Make(host, v);
		Assert.True(s.DetailTwist.Visible && s.DetailTwist.Text.StartsWith("Reflects"), "twist line shown in its slot");
		Assert.Equal("Pays 25 coins.", s.DetailRewardText, "reward line");
		s.GetParent().QueueFree();
	}

	[Test]
	public static void CopyChipShowsTheLiveTargetOrNothing(Node host)
	{
		var sess = StartSession();
		var s = Make(host, new SessionMapView(sess));
		var target = CopyRule.Target(sess.Run);
		Assert.True(target != null && s.CopyChip.State == CopyTalismanState.Defined, "a new run has a target at turn one (T2): the lowest-slot starter");
		Assert.Equal(target!.Data.Name.ToUpperInvariant(), s.CopyChip.MoveName, "the chip names the target's move");
		Assert.Equal($"Lv {target.Level}", s.CopyChip.LevelText, "and its level");
		var bare = Make(host, Fork());
		Assert.Equal(CopyTalismanState.None, bare.CopyChip.State, "no target in the view: the chip is the grey plate");
		s.GetParent().QueueFree(); bare.GetParent().QueueFree();
	}

	[Test]
	public static void FooterSaysArrowsAndDpadAndRowsAreNotCountedWithoutARun(Node host)
	{
		var s = Make(host, Fork());
		var chips = UiFind.Get<YokaiFighters.Ui.Kit.KeyChipFooter>(s, "Hint").ChipTexts();
		Assert.Equal("ARROWS / D-PAD", chips[0], "up/down move the cursor, so the chip no longer says LEFT / RIGHT; YOK-109: both devices on the chip");
		Assert.Equal("ENTER / A", chips[1], "confirm names keyboard and pad");
		Assert.Equal(2, chips.Length, "no Esc / Start chip: the map has no pause binding, so none is claimed");
		Assert.True(!UiFind.Get<Control>(s, "Status").Visible, "no run behind the view: no status panel");
		Assert.Equal("ROW 3", s.HeaderTexts[2], "no play-row count: every column is a plain row");
		s.GetParent().QueueFree();
	}

	[Test]
	public static void PassedOverEdgeCases()
	{
		var sess = StartSession();
		var view = new SessionMapView(sess);
		MapNodeState St(string id) => view.Nodes.First(n => n.Id == id).State;
		Walk(sess, "r1-apprentice", "r2-kitsune");
		Assert.Equal(MapNodeState.PassedOver, St("r2-oni"), "siblings of the chosen row-2 node are passed over");
		// Rows-only definition: r3-kappa is not reachable from r2-kitsune, but its row (3) is still ahead of Ryo, so it stays "later" (Locked), not passed over.
		Assert.Equal(MapNodeState.Locked, St("r3-kappa"), "a node in a row not yet reached is later, even when this route cannot get there");
		Assert.Equal(MapNodeState.Reachable, St("r3-oni"), "the route's own next node");

		var inside = StartSession();
		var iv = new SessionMapView(inside);
		Walk(inside, "r1-apprentice");
		inside.Enter("r2-oni"); // entered, not completed
		Assert.Equal(MapNodeState.Current, iv.Nodes.First(n => n.Id == "r2-oni").State, "the entered node is Current");
		Assert.Equal(MapNodeState.PassedOver, iv.Nodes.First(n => n.Id == "r2-kitsune").State, "its row-mates are passed over as soon as it is entered");
		Assert.True(iv.Nodes.All(n => n.State != MapNodeState.Reachable), "nothing is reachable inside a node");

		var boss = StartSession();
		var bv = new SessionMapView(boss);
		while (boss.Map.Graph.Node(boss.Reachable()[0]).Type != "boss") Walk(boss, boss.Reachable()[0]);
		Assert.Equal(MapNodeState.Reachable, bv.Nodes.First(n => n.Id == "boss-tanuki").State, "the boss is reachable, not passed over, after row 8");
		boss.Enter("boss-tanuki");
		Assert.Equal(MapNodeState.Current, bv.Nodes.First(n => n.Id == "boss-tanuki").State, "boss row: the boss is Current");
		Assert.Equal(1, bv.Nodes.Count(n => n.Row == 8 && n.State == MapNodeState.PassedOver), "exactly one row-8 elder was passed over, the other was walked");
		Assert.Equal(1, bv.Nodes.Count(n => n.Row == 9), "one boss node");
	}

	[Test]
	public static void AfterRowUsesTheLastPlayRowNotTheCount(Node host)
	{
		// play rows 1, 2, 4 (three of them), boss on row 5: the boss header must read "AFTER ROW 4", not "AFTER ROW 3"
		var v = new StubMapView().Add("a", 1, "duel", "A").Add("b", 2, "duel", "B").Add("c", 4, "duel", "C").Add("d", 5, "boss", "D")
			.Route("a", "b").Route("b", "c").Route("c", "d").Open("a");
		v.Status = new MapStatusView(1000, 1000, 0, 0, 3) { LastPlayRow = 4 };
		var s = Make(host, v);
		Assert.Equal("ROW 4", s.HeaderTexts[2], "row 4 is a play row");
		Assert.Equal("AFTER ROW 4", s.HeaderTexts[3], "the boss column");
		Assert.Equal("row 0 of 3 done", s.StatusRowsText, "the count still says how many play rows");
		Assert.Equal("AFTER ROW 4", MapScreen.HeaderText(5, false, v.Status.PlayRowLimit), "pure helper agrees");
		s.GetParent().QueueFree();
	}

	[Test]
	public static void BossOnlyGraphHasAStatusWithNoPlayRows()
	{
		var graph = new MapGraph("boss-only", new[] { new MapNodeData("b", 1, NodeTypeRegistry.Boss, "tanuki") }, System.Array.Empty<MapRoute>(), NodeTypeRegistry.Default());
		var run = RunState.NewRun(YokaiFighters.Fight.FightScene.LoadAbilityPool(), graph);
		var st = new RunMapView(run.Map!, run).Status!;
		Assert.Equal(0, st.RowsTotal, "no play rows");
		Assert.Equal(0, st.LastPlayRow, "no crash, no row");
	}

	[Test]
	public static void RestStopLinesDoNotNameABase()
	{
		var sess = StartSession();
		var view = new RunMapView(sess.Map, sess.Run, sess.Scenes);
		Assert.Equal($"Heal {Merchant.HealPercent}%, or shop with your coins.", view.Detail("r4-merchant")!.Reward, "merchant line, no base health named (open question)");
		Assert.Equal($"Rest {Dojo.RestHealPercent}%, or take a training trial.", view.Detail("r7-dojo")!.Reward, "dojo line in the same form");
	}

	[Test]
	public static void NodeStylesAreALookupWithAFallback()
	{
		var st = GD.Load<MapNodeStyles>("res://ui/map_node_styles.tres");
		Assert.Equal("Elder duel", st.Look("elder").Label, "known type");
		Assert.Equal("Merchant", st.Look("merchant").Label, "placeholder type has a look too");
		Assert.Equal("Rest stop", st.Look("rest-stop").Label, "unknown type falls back to its id");
	}
}
