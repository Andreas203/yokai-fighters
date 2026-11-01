using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using YokaiFighters.Flow;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>
/// Test Breaker probes for the YOK-109 handscroll map (AM18, R2: only cleared nodes, Ryo's node and the next choice are drawn; Y3 twist seal;
/// T6 copy chip; R1 one pick per row; M5 Continue; the reveal is presentation only so the run's StateHash never moves).
/// Every expected value comes from those rule IDs, never from what the screen happens to do.
/// </summary>
public static class MapHandscrollBreakerProbes
{
	static readonly Vector2 P1080 = new(1920, 1080), P720 = new(1280, 720);
	static readonly string[] Path = { "r1-apprentice", "r2-oni", "r3-kappa", "r4-merchant", "r5-elder-oni", "r6-kitsune", "r7-dojo", "r8-elder-kappa" };

	static MapScreen Make(Node host, IMapView view, Vector2? size = null)
	{
		var s = GD.Load<PackedScene>("res://scenes/ui/map_screen.tscn").Instantiate<MapScreen>();
		var stage = new Control { Size = size ?? P1080 };
		host.AddChild(stage);
		stage.AddChild(s);
		s.SetView(view);
		return s;
	}

	static RunSession Start(uint seed = 5)
	{
		string md = ContentPaths.Data("map");
		return RunSession.Start(YokaiFighters.Fight.FightScene.LoadAbilityPool(), FileMapSource.Default(md).Load(), NodeSceneTable.Default(md), seed);
	}

	static void FinishNode(RunSession s)
	{
		if (s.Node!.Type is not ("merchant" or "dojo") && s.WinFight(840) is { } d) { var c = d.Offer.Cards[0]; d.Pick(0, c.Targets.Count > 0 ? c.Targets[0].Slot : null); }
		s.CompleteNode();
	}

	static void WalkTo(RunSession s, params string[] ids) { foreach (var id in ids) { s.Enter(id); FinishNode(s); } }

	static void Press(MapScreen s, string action) => s._UnhandledInput(new InputEventAction { Action = action, Pressed = true });

	/// <summary>Steps the cursor to <paramref name="id"/> and confirms, like a player would.</summary>
	static void Pick(MapScreen s, string id)
	{
		for (int i = 0; i < 10 && s.CursorId != id; i++) s.Move(1);
		Assert.Equal(id, s.CursorId, "the cursor can reach " + id);
		Assert.True(s.Confirm(), "pick " + id + " accepted");
	}

	static void Gui(MapScreen s, InputEvent e) => s.ScrollView.EmitSignal(Control.SignalName.GuiInput, e);

	static IEnumerable<Node> Descend(Node n, bool skipQueued)
	{
		if (skipQueued && n.IsQueuedForDeletion()) yield break;
		yield return n;
		foreach (var c in n.GetChildren()) foreach (var x in Descend(c, skipQueued)) yield return x;
	}

	/// <summary>Every string the player or a screen reader can reach in the screen's subtree: node names, label and button text, tooltips, seal ids.</summary>
	static List<string> Strings(Node root, bool skipQueued)
	{
		var l = new List<string>();
		foreach (var n in Descend(root, skipQueued))
		{
			l.Add(n.Name.ToString());
			if (n is Label lb) l.Add(lb.Text);
			if (n is Button b) l.Add(b.Text);
			if (n is Control c) { l.Add(c.TooltipText); l.Add(c.AccessibilityName); l.Add(c.AccessibilityDescription); }
			if (n is TwistSeal ts) l.Add(ts.Icon);
		}
		return l.Where(x => !string.IsNullOrEmpty(x)).Select(x => x.ToLowerInvariant()).ToList();
	}

	static void Drive(Node host, Action<MapScreen, SessionMapView, RunSession, string> check)
	{
		foreach (var size in new[] { P1080, P720 })
		{
			var sess = Start(); var view = new SessionMapView(sess);
			var s = Make(host, view, size);
			string tag = $"{size.X}x{size.Y}";
			check(s, view, sess, tag + " start");
			foreach (var id in Path)
			{
				Pick(s, id);
				check(s, view, sess, $"{tag} entered {id}");
				FinishNode(sess); s.Refresh(); // no FinishPresentation: the next look is taken while ink and paint may still run
				check(s, view, sess, $"{tag} after {id}");
			}
			s.GetParent().QueueFree();
		}
	}

	// AM18 / R2 / Y3: a node that is not drawn exposes nothing: no id in a node name, no twist text, no boss tagline, no type word, anywhere in the
	// subtree (labels, buttons, tooltips, accessibility names, seal ids). Taken at every step of a whole run at both resolutions, through Refresh.
	[Test]
	public static void NoLaterNodeLeaksAnyStringAtAnyStepOfAWholeRun(Node host)
	{
		Drive(host, (s, view, sess, where) =>
		{
			var nodes = view.Nodes;
			var later = nodes.Where(n => n.State == MapNodeState.Locked).ToList();
			var shown = nodes.Where(n => s.ShownIds.Contains(n.Id)).ToList();
			var all = Strings(s, false);
			foreach (var n in shown) Assert.True(all.Contains("node_" + n.Id.ToLowerInvariant()), $"{where}: the scan sees the drawn node {n.Id}, so it can see a leaked one");
			foreach (var h in later)
				Assert.True(!all.Any(t => t.Contains(h.Id.ToLowerInvariant())), $"{where}: a text or node name carries the later node id {h.Id}");
			var drawnIcons = shown.Where(n => n.Elder && n.TwistIcon != null).Select(n => n.TwistIcon!).ToHashSet();
			foreach (var e in view.TwistLegend.Where(e => !drawnIcons.Contains(e.Icon)))
				Assert.True(!all.Any(t => t.Contains(e.Text.ToLowerInvariant())), $"{where}: the legend or a label gives away the undrawn twist '{e.Text}'");
			var shownDetail = shown.Select(n => view.Detail(n.Id)).Where(d => d != null).SelectMany(d => new[] { d!.Twist, d.Habit }).Where(x => !string.IsNullOrEmpty(x)).ToHashSet();
			foreach (var h in later)
				foreach (var line in new[] { view.Detail(h.Id)?.Twist, view.Detail(h.Id)?.Habit })
					if (!string.IsNullOrEmpty(line) && !shownDetail.Contains(line))
						Assert.True(!all.Any(t => t.Contains(line!.ToLowerInvariant())), $"{where}: the detail strip gives away '{line}' of {h.Id}");
			if (!s.ShownIds.Contains("boss-tanuki"))
				foreach (var w in new[] { "waits at the end", "copies your best move", "tanuki" })
					Assert.True(!all.Any(t => t.Contains(w)), $"{where}: '{w}' shows before the boss is the choice");
			var shownTypes = shown.Select(n => n.Type).ToHashSet();
			foreach (var t in later.Select(n => n.Type).Distinct().Where(t => !shownTypes.Contains(t)))
			{
				string word = s.Styles!.Look(t).Label.ToLowerInvariant();
				Assert.True(!all.Any(x => x.Contains(word)), $"{where}: the word '{word}' (type {t}) shows before a {t} is drawn");
			}
		});
	}

	// AM18: "no stroke ends at a hidden node". Every stroke joins two drawn nodes that a route of the view links; none ends on the rect of any other node.
	[Test]
	public static void NoStrokeEndsAtAnUndrawnNodeAtAnyStepOfAWholeRun(Node host)
	{
		Drive(host, (s, view, sess, where) =>
		{
			var routes = view.Routes.Where(r => s.ShownIds.Contains(r.From) && s.ShownIds.Contains(r.To)).ToList();
			Assert.Equal(routes.Count, s.Routes.Lines.Count, $"{where}: one stroke per route between two drawn nodes, no more");
			foreach (var n in view.Nodes.Where(n => !s.ShownIds.Contains(n.Id)))
			{
				var r = s.RectOf(n.Id);
				foreach (var (a, b, _) in s.Routes.Lines)
				{
					Assert.True(!(Mathf.IsEqualApprox(a.X, r.End.X) && Mathf.IsEqualApprox(a.Y, r.GetCenter().Y)), $"{where}: a stroke starts at the undrawn {n.Id}");
					Assert.True(!(Mathf.IsEqualApprox(b.X, r.Position.X) && Mathf.IsEqualApprox(b.Y, r.GetCenter().Y)), $"{where}: a stroke ends at the undrawn {n.Id}");
				}
			}
			int done = view.Nodes.Count(n => n.State is MapNodeState.Visited or MapNodeState.Current);
			Assert.Equal(Math.Max(0, done - 1), s.Routes.Lines.Count(l => l.Kind == RouteKind.Walked), $"{where}: the walked stroke joins each pair of cleared nodes once");
		});
	}

	// R2 / Y3 / T6 still apply to what is drawn (AM18): every drawn node names its yokai (or its type when none waits), an elder wears its twist
	// seal, a non-elder wears none, its caption matches its state, and the copy chip is on the map at every step.
	[Test]
	public static void EveryDrawnNodeStillNamesItsYokaiAndWearsItsTwistSeal(Node host)
	{
		Drive(host, (s, view, sess, where) =>
		{
			Assert.True(s.CopyChip.Visible && s.CopyChip.IsVisibleInTree(), where + ": T6: the copy chip is on the map");
			Assert.Equal(s.ShownIds.Count, s.Nodes.Count, where + ": one card per drawn id");
			foreach (var v in view.Nodes.Where(n => s.ShownIds.Contains(n.Id)))
			{
				var card = s.NodeFor(v.Id)!;
				Assert.Equal(v.Yokai ?? s.Styles!.Look(v.Type).Label, card.YokaiText, $"{where}: {v.Id} names who waits (R2)");
				Assert.Equal(v.Elder, card.ElderShown, $"{where}: {v.Id} elder mark (Y3)");
				Assert.Equal(v.State, card.State, $"{where}: {v.Id} state");
				if (!v.Elder) Assert.Equal("", card.TwistIconId, $"{where}: {v.Id} is no elder, so no twist seal");
				else if (v.TwistIcon != null && s.Styles!.TwistLook(v.TwistIcon) != null) Assert.Equal(v.TwistIcon, card.TwistIconId, $"{where}: {v.Id} wears its twist seal (Y3)");
			}
		});
	}

	static string Snap(MapScreen s, bool withScroll) => string.Join("\n", new[]
	{
		"shown=" + string.Join(",", s.ShownIds.OrderBy(x => x)),
		"headers=" + string.Join("|", s.HeaderTexts),
		"twists=" + string.Join("|", s.LegendTwists.Select(t => t.Icon + "=" + t.Text)),
		"types=" + s.LegendTypesText,
		"lines=" + string.Join(";", s.Routes.Lines.Select(l => $"{l.A.X:F1},{l.A.Y:F1}>{l.B.X:F1},{l.B.Y:F1}:{l.Kind}").OrderBy(x => x)),
		"cursor=" + s.CursorId, "focus=" + s.FocusId, withScroll ? "locked=" + s.Locked : "", // Locked is the pick lock: set after a live pick by design, never in a screen built mid-node
		"name=" + s.DetailNameText, "reward=" + s.DetailRewardText, "habit=" + s.DetailHabit.Visible + s.DetailHabit.Text, "twist=" + s.DetailTwist.Visible + s.DetailTwist.Text,
		"rows=" + s.StatusRowsText, "chip=" + s.CopyChip.Visible,
		withScroll ? $"scroll={s.ScrollTarget:F1}" : "",
	});

	static void Disturb(MapScreen s, int k)
	{
		s.PanTo(k % 2 == 0 ? s.MaxScroll : 0f); s.SnapScroll();
		Gui(s, new InputEventMouseButton { ButtonIndex = MouseButton.WheelDown, Pressed = true, Factor = 2f });
		foreach (var n in s.Nodes.ToList()) n.EmitSignal(Control.SignalName.MouseEntered); // hover every drawn node
		s.Move(1); s.Move(-1);
		if (k % 3 == 0) s.FinishPresentation();
	}

	static void CompareFresh(Node host, MapScreen live, int stepsDone, bool entered, string where)
	{
		var sess = Start();
		WalkTo(sess, Path.Take(stepsDone).ToArray());
		if (entered) sess.Enter(Path[stepsDone]);
		var fresh = Make(host, new SessionMapView(sess));
		live.FinishPresentation(); fresh.FinishPresentation();
		Assert.Equal(Snap(fresh, !entered), Snap(live, !entered), $"{where}: the same state drawn the same way, built live or from scratch (R2)");
		fresh.GetParent().QueueFree();
	}

	// AM18: what is drawn is a function of the run's state, not of how the screen got there (pans, hovers, skips, cursor moves, Refresh vs a new screen).
	[Test]
	public static void TheSameStateReachedLiveOrBuiltFreshDrawsTheSameMap(Node host)
	{
		var live = Start(); var s = Make(host, new SessionMapView(live));
		for (int k = 0; k < Path.Length; k++)
		{
			Disturb(s, k);
			Pick(s, Path[k]);
			if (k is 0 or 3 or 4 or 7) CompareFresh(host, s, k, true, $"entered {Path[k]}");
			FinishNode(live); s.Refresh();
			if (k is 0 or 3 or 4 or 7) CompareFresh(host, s, k + 1, false, $"after {Path[k]}");
		}
		s.GetParent().QueueFree();
	}

	// AM18 / determinism: panning, paging, the wheel, hovering, skipping and double confirms never reach the run. A run driven through the screen
	// with no skip at all has, step by step, the same StateHash as one that never had a screen; a different pick gives a different one.
	[Test]
	public static void ScreenDrivenRunMatchesTheHeadlessRunAndPresentationNeverTouchesTheHash(Node host)
	{
		var a = Start(7); var b = Start(7);
		var s = Make(host, new SessionMapView(a));
		var fired = new List<string>(); s.NodeChosen += fired.Add;
		for (int k = 0; k < Path.Length; k++)
		{
			ulong h0 = a.StateHash();
			Gui(s, new InputEventMouseButton { ButtonIndex = MouseButton.WheelDown, Pressed = true, Factor = 1f });
			s._UnhandledInput(new InputEventKey { Keycode = Key.E, Pressed = true });
			s.PanTo(k * 100f); s.SnapScroll();
			foreach (var n in s.Nodes.ToList()) n.EmitSignal(Control.SignalName.MouseEntered);
			Assert.Equal(h0, a.StateHash(), $"step {k}: pan, page, wheel and hover leave the run alone");
			Pick(s, Path[k]);
			Assert.True(!s.Confirm() && !s.Confirm(), $"step {k}: a second confirm while the pick is made is refused");
			b.Enter(Path[k]);
			Assert.Equal(b.StateHash(), a.StateHash(), $"step {k}: entering through the screen equals entering without one");
			FinishNode(a); FinishNode(b); s.Refresh(); // the next pick follows at once, during ink-in, fade-out and paint
			Assert.Equal(b.StateHash(), a.StateHash(), $"step {k}: after the node");
		}
		Assert.Equal(Path.Length, fired.Count, "one NodeChosen per pick, never twice");
		var hashBranch = Start(7); WalkTo(hashBranch, "r1-apprentice"); hashBranch.Enter("r2-kappa");
		var hashMain = Start(7); WalkTo(hashMain, "r1-apprentice"); hashMain.Enter("r2-oni");
		Assert.True(hashBranch.StateHash() != hashMain.StateHash(), "a different choice at row 2 gives a different hash");
		s.GetParent().QueueFree();
	}

	static void AssertCursorOnADrawnChoice(MapScreen s, string where)
	{
		Assert.True(s.CursorId != null, where + ": a cursor exists while there is a choice");
		Assert.True(s.ReachableIds.Contains(s.CursorId!) && s.ShownIds.Contains(s.CursorId!), $"{where}: cursor {s.CursorId} is on a drawn, reachable node (R1)");
		Assert.True(s.FocusId != null && s.ShownIds.Contains(s.FocusId), $"{where}: the detail strip names a drawn node");
	}

	// R1 / AM18: Move with any step (also huge, negative or zero) and every arrow action keep the cursor on a drawn choice, at every width of choice.
	[Test]
	public static void CursorNeverLeavesTheDrawnChoicesWhateverTheStep(Node host)
	{
		var sess = Start(); WalkTo(sess, "r1-apprentice", "r2-oni");
		var s = Make(host, new SessionMapView(sess));
		Assert.Equal(2, s.ReachableIds.Count, "row 3 offers two");
		foreach (int d in new[] { 1, -1, 2, -2, 7, -7, 0, int.MaxValue, int.MinValue, 1 }) { s.Move(d); AssertCursorOnADrawnChoice(s, "Move " + d); }
		foreach (var a in new[] { "ui_up", "ui_down", "ui_left", "ui_right", "ui_up" }) { Press(s, a); AssertCursorOnADrawnChoice(s, a); }
		s.GetParent().QueueFree();
	}

	static StubMapView Fork() => new StubMapView()
		.Add("a", 1, "duel", "Kitsune").Add("b", 2, "duel", "Oni").Add("c", 2, "elder", "Elder Kappa", true).Add("d", 2, "duel", "Kappa").Add("e", 3, "boss", "Tanuki")
		.Route("a", "b").Route("a", "c").Route("a", "d").Route("b", "e").Route("c", "e").Route("d", "e").Open("a");

	static (MapScreen S, List<string> Fired, List<MapNode> Fading) FadingState(Node host)
	{
		var v = Fork();
		var s = Make(host, v);
		var fired = new List<string>(); s.NodeChosen += fired.Add;
		Press(s, "ui_accept"); v.Complete(); s.Refresh(); s.FinishPresentation();
		Press(s, "ui_accept"); // pick b: c and d begin to fade
		s.Refresh();           // the flow re-reads the map while they still fade
		var fading = s.Board.GetChildren().OfType<MapNode>().Where(n => !s.ShownIds.Contains(n.NodeId) && !n.IsQueuedForDeletion()).ToList();
		Assert.Equal(2, fading.Count, "precondition: the two not-taken nodes are still fading out");
		Assert.Equal(2, fired.Count, "precondition: a and b were picked");
		return (s, fired, fading);
	}

	// R1: a click on a node that is fading out cannot pick it or move the cursor onto it (real GUI path).
	[Test]
	public static void ClickOnAFadingNodeDoesNothing(Node host)
	{
		var (s, fired, fading) = FadingState(host);
		s.SnapScroll();
		foreach (var n in fading)
		{
			var pos = s.ScrollView.GlobalPosition + s.ViewRectOf(n.NodeId).GetCenter();
			s.GetViewport().PushInput(new InputEventMouseMotion { Position = pos, GlobalPosition = pos }, true);
			s.GetViewport().PushInput(new InputEventMouseButton { Position = pos, GlobalPosition = pos, ButtonIndex = MouseButton.Left, Pressed = true }, true);
			s.GetViewport().PushInput(new InputEventMouseButton { Position = pos, GlobalPosition = pos, ButtonIndex = MouseButton.Left, Pressed = false }, true);
		}
		Assert.Equal(2, fired.Count, "no third pick from a click on a leaving node");
		Assert.True(s.CursorId == null || s.ReachableIds.Contains(s.CursorId), "the cursor is still on nothing or on a choice");
		s.GetParent().QueueFree();
	}

	// R1 / AM18 ("not-taken nodes fade out and leave"): even a stray press or hover signal from a leaving node must not put the cursor, and so the
	// detail strip, on it. A node that is not in ShownIds is out of the screen's tables and must be ignored by both handlers.
	[Test]
	public static void SignalsFromAFadingNodeCannotMoveTheCursorOntoIt(Node host)
	{
		var (s, fired, fading) = FadingState(host);
		foreach (var n in fading)
		{
			n.EmitSignal(Control.SignalName.MouseEntered);
			n.EmitSignal(BaseButton.SignalName.Pressed);
			Assert.True(s.CursorId == null || s.ShownIds.Contains(s.CursorId), $"cursor is on {s.CursorId}, a node that is no longer drawn");
			Assert.True(s.FocusId == null || s.ShownIds.Contains(s.FocusId), $"the detail strip follows {s.FocusId}, a node that is no longer drawn");
		}
		Assert.Equal(2, fired.Count, "no extra pick");
		s.GetParent().QueueFree();
	}

	// AM18 / R2: a new run on the same screen (restart after a loss) shows only row 1. Nodes still fading from the last run are the new run's
	// later-row nodes (same ids, same yokai), so they must not survive SetView, not even for the length of a fade.
	[Test]
	public static void NewRunOnTheSameScreenKeepsNothingOfTheLastRunsFadingNodes(Node host)
	{
		var old = Start(5); WalkTo(old, "r1-apprentice");
		var s = Make(host, new SessionMapView(old));
		Pick(s, "r2-oni"); // r2-kitsune and r2-kappa start to fade
		Assert.True(s.Presenting, "precondition: a fade is running");
		old.LoseFight();
		var fresh = Start(9);
		s.SetView(new SessionMapView(fresh));
		Assert.Equal("r1-apprentice", string.Join(",", s.ShownIds), "a new run draws row 1 only");
		var strays = s.Board.GetChildren().OfType<MapNode>().Where(n => !s.ShownIds.Contains(n.NodeId) && !n.IsQueuedForDeletion()).Select(n => n.NodeId).ToList();
		Assert.Equal("", string.Join(",", strays), "no leftover control of the last run under the board: they name later rows of the new run");
		var all = Strings(s, true);
		foreach (var h in new[] { "r2-kitsune", "r2-oni", "r2-kappa", "kappa", "kitsune" })
			Assert.True(!all.Any(t => t.Contains(h)), $"the new run's row 1 screen mentions '{h}'");
		s.GetParent().QueueFree();
	}

	// AM18 / R1: a restart on the same screen puts everything back: one header, no twist, no stroke, scroll at the left, cursor on row 1, unlocked.
	[Test]
	public static void RestartResetsHeadersLegendStrokesScrollAndCursor(Node host)
	{
		var old = Start(5); WalkTo(old, "r1-apprentice", "r2-oni", "r3-kappa", "r4-merchant");
		var s = Make(host, new SessionMapView(old));
		Pick(s, "r5-elder-oni");
		Assert.True(s.LegendTwists.Count > 0 && s.Routes.Lines.Count > 0 && s.HeaderTexts.Count == 5, "precondition: a run in progress shows twists, strokes, five headers");
		s.PanTo(s.MaxScroll); s.SnapScroll();
		old.LoseFight();
		s.SetView(new SessionMapView(Start(5)));
		s.FinishPresentation();
		Assert.Equal("ROW 1", string.Join("|", s.HeaderTexts), "headers of the last run are gone");
		Assert.Equal(0, s.LegendTwists.Count, "no twist is listed again before an elder is drawn");
		Assert.Equal("D duel", s.LegendTypesText, "only the drawn type");
		Assert.Equal(0, s.Routes.Lines.Count, "no stroke");
		Assert.Equal(0f, s.ScrollTarget, "the scroll goes back to the left end where row 1 is");
		Assert.True(!s.Locked && s.CursorId == "r1-apprentice", "unlocked, cursor on the only choice");
		Assert.True(s.ViewRectOf("r1-apprentice").Position.X >= 0 && s.ViewRectOf("r1-apprentice").End.X <= s.ScrollView.Size.X, "row 1 is in view");
		s.GetParent().QueueFree();
	}

	// AM18: calling SetView with the same state again and again, or Refresh, never duplicates a node, a header, a legend row or a stroke.
	[Test]
	public static void RepeatedSetViewAndRefreshWithTheSameStateDrawOnceOnly(Node host)
	{
		var sess = Start(); WalkTo(sess, "r1-apprentice", "r2-oni", "r3-kappa", "r4-merchant");
		var view = new SessionMapView(sess);
		var s = Make(host, view);
		string snap = Snap(s, false);
		int lines = s.Routes.Lines.Count;
		for (int i = 0; i < 8; i++)
		{
			if (i % 3 == 0) s.SetView(new SessionMapView(sess)); else if (i % 3 == 1) s.SetView(view); else s.Refresh();
			s.Move(1); s.Move(-1);
			var cards = s.Board.GetChildren().OfType<MapNode>().Where(n => !n.IsQueuedForDeletion()).ToList();
			Assert.Equal(s.ShownIds.Count, cards.Count, $"round {i}: one card per drawn node");
			Assert.Equal(cards.Count, cards.Select(c => c.NodeId).Distinct().Count(), $"round {i}: no duplicate card");
			Assert.Equal(s.HeaderTexts.Count, UiFind.Get<Control>(s, "RowHeaders").GetChildren().OfType<Label>().Count(l => !l.IsQueuedForDeletion()), $"round {i}: one header label per header");
			Assert.Equal(lines, s.Routes.Lines.Count, $"round {i}: strokes unchanged");
		}
		s.FinishPresentation();
		Assert.Equal(snap, Snap(s, false), "the same state looks the same after eight rebuilds");
		s.GetParent().QueueFree();
	}

	// AM18 / M5: swapping a real map for an empty one leaves no header, legend row, stroke, card or detail behind.
	[Test]
	public static void ClearingToAnEmptyMapLeavesNothingOfTheLastOne(Node host)
	{
		var sess = Start(); WalkTo(sess, "r1-apprentice", "r2-oni", "r3-kappa", "r4-merchant");
		var s = Make(host, new SessionMapView(sess));
		s.SetView(new StubMapView());
		s.FinishPresentation();
		Assert.Equal(0, s.ShownIds.Count + s.HeaderTexts.Count + s.LegendTwists.Count + s.Routes.Lines.Count, "nothing drawn");
		Assert.Equal(0, s.Board.GetChildren().OfType<MapNode>().Count(n => !n.IsQueuedForDeletion()), "no card left under the board");
		Assert.True(!UiFind.Get<Control>(s, "Detail").Visible && !UiFind.Get<Control>(s, "Status").Visible, "no detail and no status without a run");
		Assert.True(s.ScrollOffset >= 0f && s.ScrollOffset <= s.MaxScroll, "scroll in range");
		s.GetParent().QueueFree();
	}

	static void AssertScrollSane(MapScreen s, string where)
	{
		Assert.True(float.IsFinite(s.ScrollTarget) && float.IsFinite(s.ScrollOffset) && float.IsFinite(s.Scroll.Position.X), where + ": every scroll value is a finite number");
		Assert.True(s.ScrollTarget >= 0f && s.ScrollTarget <= s.MaxScroll + 0.01f, $"{where}: target {s.ScrollTarget} outside 0..{s.MaxScroll}");
		Assert.True(s.ScrollOffset >= -0.01f && s.ScrollOffset <= s.MaxScroll + 0.01f, $"{where}: offset {s.ScrollOffset} outside 0..{s.MaxScroll}");
		Assert.Equal(-Mathf.Round(s.ScrollOffset), s.Scroll.Position.X, where + ": the paper sits where the offset says");
	}

	// AM18: pan stays on the paper whatever the wheel factor, drag distance or number of page presses, at both resolutions.
	[Test]
	public static void PanNeverLeavesThePaperUnderHugeWheelDragAndPageAbuse(Node host)
	{
		foreach (var size in new[] { P1080, P720 })
		{
			var sess = Start(); WalkTo(sess, "r1-apprentice", "r2-oni");
			var s = Make(host, new SessionMapView(sess), size);
			string tag = $"{size.X}x{size.Y}";
			foreach (float f in new[] { 0f, 1f, 3f, 1e6f, 1e30f, float.MaxValue, float.PositiveInfinity, -4f })
				foreach (var b in new[] { MouseButton.WheelDown, MouseButton.WheelUp, MouseButton.WheelLeft, MouseButton.WheelRight })
				{
					Gui(s, new InputEventMouseButton { ButtonIndex = b, Pressed = true, Factor = f });
					AssertScrollSane(s, $"{tag} wheel {b} x{f}");
				}
			Gui(s, new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true });
			foreach (float dx in new[] { -1e9f, 1e9f, float.MaxValue, -float.MaxValue, 0f, 12.5f })
			{
				Gui(s, new InputEventMouseMotion { Relative = new Vector2(dx, 0), ButtonMask = MouseButtonMask.Left });
				AssertScrollSane(s, $"{tag} drag {dx}");
			}
			Gui(s, new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false });
			for (int i = 0; i < 60; i++) s._UnhandledInput(new InputEventKey { Keycode = Key.E, Pressed = true });
			Assert.Equal(s.MaxScroll, s.ScrollTarget, tag + ": sixty pages right end exactly on the right roller");
			s.SnapScroll();
			Assert.True(!UiFind.Get<MapEdgeShade>(s, "ShadeRight").More && UiFind.Get<MapEdgeShade>(s, "ShadeLeft").More, tag + ": at the right end only the left chevron shows");
			for (int i = 0; i < 60; i++) s._UnhandledInput(new InputEventJoypadButton { ButtonIndex = JoyButton.LeftShoulder, Pressed = true });
			Assert.Equal(0f, s.ScrollTarget, tag + ": sixty pages left end exactly on the left roller");
			s.SnapScroll();
			Assert.True(UiFind.Get<MapEdgeShade>(s, "ShadeRight").More && !UiFind.Get<MapEdgeShade>(s, "ShadeLeft").More, tag + ": at the left end only the right chevron shows");
			foreach (double d in new[] { 0.0, 1e-9, 0.016, 1e6 }) { s._Process(d); AssertScrollSane(s, $"{tag} _Process({d}) with no pad"); }
			Assert.Equal(0f, s.ScrollTarget, tag + ": with no pad connected the stick never moves the paper");
			s.GetParent().QueueFree();
		}
	}

	// AM18: it must work at 1920x1080 and 1280x720, also when the viewport changes while the scroll is panned to the far end.
	[Test]
	public static void ResizingTheViewportMidPanKeepsTheScrollAndTheNodesInPlace(Node host)
	{
		var sess = Start(); WalkTo(sess, "r1-apprentice", "r2-oni", "r3-kappa", "r4-merchant");
		var s = Make(host, new SessionMapView(sess), P1080);
		var stage = (Control)s.GetParent();
		s.PanTo(s.MaxScroll); s.SnapScroll();
		float oldMax = s.MaxScroll;
		stage.Size = P720;
		Assert.True(Mathf.IsEqualApprox(s.ScrollView.Size.X, P720.X), "precondition: the viewport followed the stage: " + s.ScrollView.Size);
		AssertScrollSane(s, "1080 -> 720 at the far end");
		Assert.True(s.MaxScroll < oldMax, "a narrower, shorter viewport has a smaller range (the cards are smaller)");
		Assert.True(s.MaxScroll > 0f, "still a scroll wider than the screen");
		foreach (var n in s.Nodes)
		{
			var r = s.RectOf(n.NodeId);
			Assert.True(n.Position.IsEqualApprox(r.Position) && n.Size.IsEqualApprox(r.Size), $"card {n.NodeId} follows the new layout: at {n.Position}/{n.Size}, layout says {r}");
			Assert.True(new Rect2(Vector2.Zero, s.Board.Size).Encloses(r), $"card {n.NodeId} inside the board after the resize");
		}
		s.PanTo(s.MaxScroll); s.SnapScroll();
		stage.Size = P1080;
		AssertScrollSane(s, "720 -> 1080 at the far end");
		s.PanTo(0f); s.SnapScroll(); stage.Size = P720; AssertScrollSane(s, "720 at the left end");
		s.GetParent().QueueFree();
	}

	// AM18 (focus gate InputFree): a focused control outside the map gates the keys; once it is hidden, removed or its layer is hidden the gate
	// must open again, or the map would stay dead after a menu closes.
	[Test]
	public static void InputFreeOpensAgainWhenTheFocusedOutsideControlGoesAway(Node host)
	{
		var sess = Start(); var s = Make(host, new SessionMapView(sess));
		s.PanTo(0f); s.SnapScroll();
		var b1 = new Button { Position = new Vector2(10, 10), Size = new Vector2(100, 40) };
		host.AddChild(b1); b1.GrabFocus();
		Assert.True(!s.InputFree, "precondition: focus outside the map closes the gate");
		b1.Visible = false;
		Assert.True(s.InputFree, "hidden: the gate is open again");
		b1.Visible = true; b1.GrabFocus();
		Assert.True(!s.InputFree, "shown and focused again: closed");
		host.RemoveChild(b1);
		Assert.True(s.InputFree, "removed from the tree: open again");
		b1.Free();
		var layer = new CanvasLayer { Layer = 120 };
		var b2 = new Button { Position = new Vector2(10, 10), Size = new Vector2(100, 40) };
		layer.AddChild(b2); host.AddChild(layer); b2.GrabFocus();
		Assert.True(!s.InputFree, "a button on a menu layer holds the gate");
		layer.Visible = false;
		Assert.True(s.InputFree, "the whole layer hidden: open again");
		s._UnhandledInput(new InputEventKey { Keycode = Key.E, Pressed = true });
		Assert.True(s.ScrollTarget > 0f, "and the page key works again");
		layer.QueueFree();
		s.GetParent().QueueFree();
	}

	// AM18 ("click-drag on the paper"): a mouse motion with no button held is not a drag, even if the release was never seen (the window lost
	// focus, the screen was swapped mid-drag).
	[Test]
	public static void MotionWithNoButtonHeldAfterALostReleaseDoesNotPan(Node host)
	{
		var sess = Start(); var s = Make(host, new SessionMapView(sess));
		s.PanTo(900f); s.SnapScroll();
		Gui(s, new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true });
		Gui(s, new InputEventMouseMotion { Relative = new Vector2(-100, 0), ButtonMask = MouseButtonMask.Left });
		Assert.Equal(1000f, s.ScrollOffset, "precondition: a real drag pans");
		// no release arrives; the next thing the screen sees is a plain move
		Gui(s, new InputEventMouseMotion { Relative = new Vector2(-300, 0), ButtonMask = 0 });
		Assert.Equal(1000f, s.ScrollOffset, "a move with no button down leaves the paper where it is");
		s.GetParent().QueueFree();
	}

	static StubMapView Wide()
	{
		var v = new StubMapView().Add("a", 1, "duel", "Kitsune");
		for (int i = 1; i <= 5; i++) v.Add($"w{i}", 2, i == 3 ? "elder" : "duel", $"Wide {i}", i == 3, i == 3 ? "twist-grip" : null);
		v.Add("c", 3, "duel", "Solo");
		for (int i = 1; i <= 6; i++) v.Add($"x{i}", 4, "duel", $"Six {i}");
		v.Add("boss", 5, "boss", "Tanuki");
		for (int i = 1; i <= 5; i++) { v.Route("a", $"w{i}"); v.Route($"w{i}", "c"); }
		for (int i = 1; i <= 6; i++) { v.Route("c", $"x{i}"); v.Route($"x{i}", "boss"); }
		return v.Open("a");
	}

	static void AssertRowLaysOut(MapScreen s, string[] ids, string where)
	{
		Assert.Equal(string.Join(",", ids), string.Join(",", s.ReachableIds), where + ": the choice keeps the view's order");
		var board = new Rect2(Vector2.Zero, s.Board.Size);
		for (int i = 0; i < ids.Length; i++)
		{
			var r = s.RectOf(ids[i]); var vr = s.ViewRectOf(ids[i]);
			Assert.True(board.Encloses(r), $"{where}: {ids[i]} {r} inside the board {s.Board.Size}");
			Assert.True(vr.Position.Y >= 0 && vr.End.Y <= s.ScrollView.Size.Y, $"{where}: {ids[i]} fits the viewport height {s.ScrollView.Size.Y}");
			for (int j = i + 1; j < ids.Length; j++) Assert.True(!r.Intersects(s.RectOf(ids[j])), $"{where}: {ids[i]} overlaps {ids[j]}");
			if (i > 0) Assert.True(r.Position.Y > s.RectOf(ids[i - 1]).Position.Y, $"{where}: top to bottom in view order");
		}
		for (int i = 0; i < ids.Length; i++) { Assert.Equal(ids[i], s.CursorId, $"{where}: cursor step {i}"); s.Move(1); }
		Assert.Equal(ids[0], s.CursorId, where + ": wraps after a full turn");
	}

	// R1 / AM18: a row of five, a lone node between wide rows and a row of six: each is laid out inside the viewport at both resolutions, never
	// overlaps, and the cursor visits each in order.
	[Test]
	public static void WideAndSingleRowsLayOutAndNavigateAtBothResolutions(Node host)
	{
		foreach (var size in new[] { P1080, P720 })
		{
			var v = Wide();
			var s = Make(host, v, size);
			string tag = $"{size.X}x{size.Y}";
			Assert.True(s.Strings().All(t => !t.Contains("wide") && !t.Contains("six")), tag + ": later rows unnamed at the start");
			s.Confirm(); v.Complete(); s.Refresh(); s.FinishPresentation();
			AssertRowLaysOut(s, new[] { "w1", "w2", "w3", "w4", "w5" }, tag + " five");
			Assert.True(s.Strings().All(t => !t.Contains("six") && !t.Contains("solo")), tag + ": the rows after the five stay blank");
			for (int i = 0; i < 2; i++) s.Move(1);
			Assert.True(s.Confirm(), tag + ": pick the third of five");
			v.Complete(); s.Refresh(); s.FinishPresentation();
			Assert.Equal("c", string.Join(",", s.ReachableIds), tag + ": the lone node");
			Assert.True(Math.Abs(s.RectOf("c").GetCenter().Y - s.Board.Size.Y / 2f) < 1f, tag + ": a single node sits in the middle of its column");
			s.Confirm(); v.Complete(); s.Refresh(); s.FinishPresentation();
			AssertRowLaysOut(s, new[] { "x1", "x2", "x3", "x4", "x5", "x6" }, tag + " six");
			s.GetParent().QueueFree();
		}
	}

	static List<string> Strings(this MapScreen s) => Strings(s, true);

	// T6 / Y3 / AM18: with every drawn twist listed (the row-8 choice lists the row-5 elder and both row-8 elders) the legend still fits the
	// 1920 screen, stays clear of the detail strip and the footer, and the copy chip is visible.
	[Test]
	public static void ChipLegendAndDetailFitTheScreenWithEveryDrawnTwist(Node host)
	{
		var sess = Start(); WalkTo(sess, Path.Take(7).ToArray());
		var s = Make(host, new SessionMapView(sess), P1080);
		Assert.True(s.LegendTwists.Count >= 2, "precondition: several twists are listed: " + s.LegendTwists.Count);
		Assert.True(s.CopyChip.Visible, "T6: the copy chip is shown on the map");
		var legend = UiFind.Get<Control>(s, "Legend"); var detail = UiFind.Get<Control>(s, "Detail"); var hint = UiFind.Get<Control>(s, "Hint");
		float w = Math.Max(legend.Size.X, legend.GetCombinedMinimumSize().X), h = Math.Max(legend.Size.Y, legend.GetCombinedMinimumSize().Y);
		float left = 960f - w / 2f, top = legend.GlobalPosition.Y;
		Assert.True(left >= 0f && left + w <= 1920f, $"the legend ({w:F0} px) fits the 1920 screen: from {left:F0} to {left + w:F0}");
		Assert.True(top + h <= hint.GlobalPosition.Y + 0.5f, $"the legend ends at {top + h:F0}, above the footer at {hint.GlobalPosition.Y:F0}");
		Assert.True(detail.GlobalPosition.Y + detail.Size.Y <= top + 0.5f, $"the detail strip ends at {detail.GlobalPosition.Y + detail.Size.Y:F0}, above the legend at {top:F0}");
		Assert.True(detail.GlobalPosition.Y >= s.ScrollView.GlobalPosition.Y + s.ScrollView.Size.Y - 0.5f, "and below the scroll's viewport");
		s.GetParent().QueueFree();
	}
}
