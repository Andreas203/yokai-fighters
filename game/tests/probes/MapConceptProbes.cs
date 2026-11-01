using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using YokaiFighters.Ai;
using YokaiFighters.Flow;
using YokaiFighters.Sim;
using YokaiFighters.Story;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>YOK-109 probes: map screen concept (habit line, twist seal, twist legend, boss card). Rules Y1, Y2, Y3, R2.</summary>
public static class MapConceptProbes
{
	static MapScreen Make(Node host, IMapView view, Vector2? size = null)
	{
		var s = GD.Load<PackedScene>("res://scenes/ui/map_screen.tscn").Instantiate<MapScreen>();
		var stage = new Control { Size = size ?? new Vector2(1920, 1080) };
		host.AddChild(stage);
		stage.AddChild(s);
		s.SetView(view);
		return s;
	}

	static RunSession StartSession(uint seed = 5)
	{
		string md = ContentPaths.Data("map");
		return RunSession.Start(YokaiFighters.Fight.FightScene.LoadAbilityPool(), FileMapSource.Default(md).Load(), NodeSceneTable.Default(md), seed);
	}

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

	static void Press(MapScreen s, string action) => s._UnhandledInput(new InputEventAction { Action = action, Pressed = true });

	static void WithProfiles(BehaviourProfile[]? p, Action body)
	{
		var saved = RunMapView.Profiles;
		try { RunMapView.Profiles = p; body(); } finally { RunMapView.Profiles = saved; }
	}

	// Y1/Y2: the habit shown ahead is the habit of the profile the fight will use (seeded temperament roll). Profiles whose
	// temperaments differ expose a view that ignores the seed or picks the wrong temperament.
	[Test]
	public static void HabitLineFollowsTheSeededTemperamentNotAFixedOne()
	{
		var real = RunMapView.Profiles!;
		var custom = real.Select(p => p.Yokai == "kappa" ? p with { Habit = p.Habit with { Description = p.Temperament.ToUpperInvariant() + " habit. Counter." } } : p).ToArray();
		WithProfiles(custom, () =>
		{
			int agg = 0, pat = 0;
			for (uint seed = 1; seed <= 40; seed++)
			{
				var sess = StartSession(seed);
				var view = new RunMapView(sess.Map, sess.Run, sess.Scenes, seed);
				string want = NodeTemperament.Roll(seed, "r3-kappa").ToUpperInvariant() + " habit.";
				string? got = view.Detail("r3-kappa")!.Habit;
				Assert.Equal(want, got ?? "<null>", $"seed {seed}: the fight rolls {want} for r3-kappa (Y1/Y2)");
				if (want.StartsWith("AGG")) agg++; else pat++;
			}
			Assert.True(agg > 0 && pat > 0, "forty seeds reach both temperaments (50/50 roll)");
		});
	}

	// M5: a restored run keeps its seed, so a resumed node shows the same habit line as the original.
	[Test]
	public static void RestoredRunSameSeedSameHabitLine()
	{
		var real = RunMapView.Profiles!;
		var custom = real.Select(p => p with { Habit = p.Habit with { Description = p.Id + " habit. Counter." } }).ToArray();
		WithProfiles(custom, () =>
		{
			var a = StartSession(77);
			var b = StartSession(77);
			var va = new SessionMapView(a); var vb = new SessionMapView(b);
			foreach (var n in a.Map.Graph.Nodes)
				Assert.Equal(va.Detail(n.Id)?.Habit ?? "<null>", vb.Detail(n.Id)?.Habit ?? "<null>", "same seed, same habit line for " + n.Id);
		});
	}

	// Y2 table: the line of every fight node is the habit sentence only (no counter-advice).
	[Test]
	public static void HabitLineIsOneSentenceAndNeverTheCounterAdvice()
	{
		var sess = StartSession();
		var view = new RunMapView(sess.Map, sess.Run, sess.Scenes, sess.Seed);
		foreach (var n in sess.Map.Graph.Nodes)
		{
			var h = view.Detail(n.Id)?.Habit;
			if (h == null) continue;
			Assert.True(!h.Contains(". "), $"{n.Id}: one sentence only: {h}");
			Assert.True(h.EndsWith("."), $"{n.Id}: ends on the habit's full stop: {h}");
			Assert.True(!h.Contains("Block it") && !h.Contains("Meet her") && !h.Contains("Stop at one") && !h.Contains("Throw a second"), $"{n.Id}: no counter-advice in the habit line: {h}");
		}
	}

	[Test]
	public static void EmptyProfileArrayGivesNoHabitAndNoCrash()
	{
		WithProfiles(Array.Empty<BehaviourProfile>(), () =>
		{
			var sess = StartSession();
			var view = new RunMapView(sess.Map, sess.Run, sess.Scenes, sess.Seed);
			foreach (var n in sess.Map.Graph.Nodes)
				Assert.True(view.Detail(n.Id)!.Habit == null, $"{n.Id}: no profile data, no habit line (got '{view.Detail(n.Id)!.Habit}')");
		});
	}

	[Test]
	public static void NodeNamingAMissingProfileShowsNoHabitAndDoesNotThrow()
	{
		var noApprentice = RunMapView.Profiles!.Where(p => p.Id != "apprentice").ToArray();
		WithProfiles(noApprentice, () =>
		{
			var sess = StartSession();
			var view = new RunMapView(sess.Map, sess.Run, sess.Scenes, sess.Seed);
			Assert.True(view.Detail("r1-apprentice")!.Habit == null, "apprentice profile missing: no habit line, no stand-in");
			Assert.True(view.Detail("r2-oni")!.Habit != null, "other nodes unaffected");
		});
	}

	// Y3/YOK-109: no stand-in text. Remove every twist label card: legend rows, twist line and seal disappear.
	[Test]
	public static void MissingStoryLabelDropsLegendRowTwistLineAndSeal(Node host)
	{
		var saved = TwistCopy.Story;
		try
		{
			TwistCopy.Story = new StoryLibrary(Array.Empty<StoryCardData>());
			var sess = StartSession();
			var view = new SessionMapView(sess);
			Assert.Equal(0, view.TwistLegend.Count, "no label cards: no legend rows");
			Walk(sess, "r1-apprentice", "r2-oni", "r3-kappa", "r4-merchant");
			var s = Make(host, new SessionMapView(sess));
			Assert.Equal("r5-elder-oni", s.CursorId, "on the elder");
			Assert.True(!s.DetailTwist.Visible && !s.DetailSeal.Visible, "no story label: neither twist line nor seal");
			Assert.Equal(0, s.LegendTwists.Count, "no rows");
			Assert.True(s.DetailHabit.Visible, "the habit line is independent of the twist story");
			s.GetParent().QueueFree();
		}
		finally { TwistCopy.Story = saved; }
	}

	[Test]
	public static void RepeatedRefreshAndSetViewDoNotDuplicateLegendRows(Node host)
	{
		// YOK-109 / AM18 (R2, Y3): the legend lists a twist only while an elder carrying it is drawn, so a new run lists none and the
		// two row-5 elders list theirs once they are the choice. Intent kept: repeated Refresh / SetView never duplicate rows or leave orphans.
		var sess = StartSession();
		var s = Make(host, new SessionMapView(sess));
		Assert.Equal(0, s.LegendTwists.Count, "run start: no elder is drawn, so no twist is listed (AM18)");
		Walk(sess, "r1-apprentice", "r2-oni", "r3-kappa", "r4-merchant");
		s.SetView(new SessionMapView(sess));
		var drawn = new SessionMapView(sess).Nodes.Where(n => n.Elder && n.TwistIcon != null && s.ShownIds.Contains(n.Id)).Select(n => n.TwistIcon!).Distinct().ToList();
		Assert.True(drawn.Count >= 1, "the row-5 elders are drawn and carry twists (data)");
		Assert.Equal(drawn.Count, s.LegendTwists.Count, "one row per twist of a drawn elder");
		for (int i = 0; i < 5; i++) { s.Refresh(); s.SetView(new SessionMapView(sess)); }
		Assert.Equal(drawn.Count, s.LegendTwists.Count, "after repeated Refresh and SetView");
		var holder = UiFind.Get<Control>(s, "LegendTwists");
		Assert.Equal(drawn.Count, holder.GetChildCount(), "no orphan children in the holder");
		s.SetView(StubMapView.Grid(3, 2));
		Assert.Equal(0, s.LegendTwists.Count, "a view without twists clears the rows");
		s.SetView(new SessionMapView(sess));
		Assert.Equal(drawn.Count, s.LegendTwists.Count, "back");
		s.GetParent().QueueFree();
	}

	[Test]
	public static void UnknownTwistIconRendersWithFallback(Node host)
	{
		// AM18 (Y3, R2): entries whose icon is on no drawn elder are filtered out; entries for a drawn elder's icon (known or not) render with the
		// fallback seal and keep their text. Both elders below are the choice once a is cleared, so both are drawn; "twist-hidden" is on an undrawn row.
		var v = new StubMapView().Add("a", 1, "duel", "Kitsune")
			.Add("e1", 2, "elder", "Elder A", true, "twist-nonexistent").Add("e2", 2, "elder", "Elder B", true, "")
			.Add("e3", 3, "elder", "Elder C", true, "twist-hidden")
			.Route("a", "e1").Route("a", "e2").Route("e1", "e3").Route("e2", "e3").Open("a");
		v.Legend.Add(new MapTwistLegendEntry("twist-nonexistent", "Does something odd."));
		v.Legend.Add(new MapTwistLegendEntry("twist-nonexistent", "Same icon twice."));
		v.Legend.Add(new MapTwistLegendEntry("", "Empty icon id."));
		v.Legend.Add(new MapTwistLegendEntry("twist-hidden", "Secret of a later row."));
		var s = Make(host, v);
		Assert.Equal(0, s.LegendTwists.Count, "run start: no elder drawn, no row");
		s.Confirm(); v.Complete(); s.Refresh(); s.FinishPresentation();
		var rows = s.LegendTwists;
		Assert.Equal(3, rows.Count, "one row per entry whose icon a drawn elder carries");
		Assert.Equal("Does something odd.", rows[0].Text, "unknown icon id row keeps its text");
		Assert.True(!rows.Any(r => r.Text.Contains("Secret")), "the later row's twist is not listed (AM18)");
		s.GetParent().QueueFree();
	}

	[Test]
	public static void SealFollowsTheCursorOffAnElderAndOnAgain(Node host)
	{
		var v = new StubMapView()
			.Add("a", 1, "duel", "Kitsune").Add("e1", 2, "elder", "Elder Kappa", true, "twist-grip").Add("e2", 2, "elder", "Elder X", true, "twist-wat").Add("d", 2, "duel", "Oni").Add("b", 3, "boss", "Tanuki")
			.Route("a", "e1").Route("a", "e2").Route("a", "d").Route("e1", "b").Route("e2", "b").Route("d", "b");
		v.Open("e1", "e2", "d");
		v.Details["e1"] = new MapNodeDetail("r", "Twist: grip", "Throws.");
		v.Details["e2"] = new MapNodeDetail("r", "Twist: wat", null);
		v.Details["d"] = new MapNodeDetail("r");
		var s = Make(host, v);
		Assert.Equal("e1", s.CursorId, "starts on first reachable");
		Assert.True(s.DetailSeal.Visible && s.DetailSeal.Icon == "twist-grip", "elder 1 seal");
		Press(s, "ui_down");
		Assert.Equal("e2", s.CursorId, "second elder");
		Assert.True(s.DetailSeal.Visible && s.DetailSeal.Icon == "twist-wat", "unknown icon id: seal still shown with the id and fallback glyph");
		Assert.True(!s.DetailHabit.Visible, "no habit on e2: the line from e1 must not linger");
		Press(s, "ui_down");
		Assert.Equal("d", s.CursorId, "non-elder");
		Assert.True(!s.DetailSeal.Visible && !s.DetailTwist.Visible && !s.DetailHabit.Visible, "cursor on a non-elder after an elder: seal, twist and habit all hidden");
		Assert.Equal("", s.DetailTwist.Text, "twist text cleared");
		Press(s, "ui_down");
		Assert.True(s.DetailSeal.Visible && s.DetailSeal.Icon == "twist-grip", "wraps back to the first elder");
		Assert.Equal("Habit to punish: Throws.", s.DetailHabit.Text, "its habit again");
		s.GetParent().QueueFree();
	}

	[Test]
	public static void EmptyHabitStringHidesTheLine(Node host)
	{
		var v = StubMapView.Grid(3, 2);
		v.Details["r1-0"] = new MapNodeDetail("r", null, "");
		var s = Make(host, v);
		Assert.True(!s.DetailHabit.Visible && s.DetailHabit.Text == "", "empty habit string is no habit");
		s.GetParent().QueueFree();
	}

	[Test]
	public static void ElderWithTwistTextButNoIconShowsNoSeal(Node host)
	{
		var v = new StubMapView().Add("a", 1, "elder", "Elder", true).Open("a");
		v.Details["a"] = new MapNodeDetail("r", "Twist: something");
		var s = Make(host, v);
		Assert.True(s.DetailTwist.Visible, "twist text");
		Assert.True(!s.DetailSeal.Visible, "no icon id, no seal");
		s.GetParent().QueueFree();
	}

	[Test]
	public static void OneRowTwelveRowAndEmptyMapsBuild(Node host)
	{
		var one = new StubMapView().Add("only", 1, "boss", "Tanuki").Open("only");
		var s1 = Make(host, one);
		Assert.Equal("waits at the end\ncopies your best move", s1.NodeFor("only")!.TagText, "a lone boss still carries its lines");
		var b = s1.RectOf("only");
		Assert.True(b.Position.X >= -0.5f && b.Position.Y >= -0.5f && b.End.X <= s1.Board.Size.X + 0.5f && b.End.Y <= s1.Board.Size.Y + 0.5f, $"lone boss card inside the board: {b} in {s1.Board.Size}");
		s1.GetParent().QueueFree();

		var s12 = Make(host, StubMapView.Grid(12, 3, new[] { "duel", "elder", "boss" }));
		foreach (var n in s12.Nodes.Where(n => n.TagText != ""))
		{
			var r = s12.RectOf(n.NodeId);
			Assert.True(r.End.X <= s12.Board.Size.X + 0.5f && r.Position.X >= -0.5f && r.End.Y <= s12.Board.Size.Y + 0.5f && r.Position.Y >= -0.5f, $"12-row: enlarged card {n.NodeId} inside the board: {r} in {s12.Board.Size}");
		}
		s12.GetParent().QueueFree();

		var empty = Make(host, new StubMapView());
		Assert.Equal(0, empty.Nodes.Count, "empty map builds");
		Assert.True(!UiFind.Get<Control>(empty, "Detail").Visible, "no detail without a node");
		empty.GetParent().QueueFree();
	}

	[Test]
	public static void RoutesStillMeetTheEnlargedBossCard(Node host)
	{
		// AM18: the boss and its routes are not drawn until row 8 is cleared; then the stroke from the cleared elder must meet the enlarged card.
		var sess = StartSession();
		var s = Make(host, new SessionMapView(sess));
		var r = s.RectOf("boss-tanuki");
		Assert.True(!s.Routes.Lines.Any(l => Mathf.IsEqualApprox(l.B.X, r.Position.X)), "start: no stroke ends on the (undrawn) boss card");
		Walk(sess, "r1-apprentice", "r2-oni", "r3-kappa", "r4-merchant", "r5-elder-oni", "r6-kitsune", "r7-dojo", "r8-elder-kappa");
		s.SetView(new SessionMapView(sess)); s.FinishPresentation();
		Assert.True(s.NodeFor("boss-tanuki") != null, "the boss is the choice now, so it is drawn");
		var into = s.Routes.Lines.Where(l => Mathf.IsEqualApprox(l.B.X, r.Position.X)).ToList();
		Assert.True(into.Count >= 1, "a route ends on the boss card's left edge");
		foreach (var l in into) Assert.True(Mathf.IsEqualApprox(l.B.Y, r.GetCenter().Y), "and on its centre line");
		foreach (var n in s.Nodes.Where(n => n.NodeId != "boss-tanuki"))
			Assert.True(!s.RectOf(n.NodeId).Intersects(r), $"boss card overlaps {n.NodeId}");
		s.GetParent().QueueFree();
	}

	[Test]
	public static void BossCardInsideBoardAt720p(Node host)
	{
		var sess = StartSession();
		var s = Make(host, new SessionMapView(sess), new Vector2(1280, 720));
		var r = s.RectOf("boss-tanuki");
		Assert.True(r.Position.X >= -0.5f && r.End.X <= s.Board.Size.X + 0.5f && r.Position.Y >= -0.5f && r.End.Y <= s.Board.Size.Y + 0.5f, $"boss card {r} inside board {s.Board.Size} at 720p");
		s.GetParent().QueueFree();
	}

	[Test]
	public static void RefusedConfirmChangesNothing(Node host)
	{
		// AM18: a hidden node has no control at all, so the refusal left to test is a press on a drawn node that is not a choice (the one Ryo
		// stands on), plus "no control exists" for later rows. Intent kept: refused presses change no field, no signal, no hash.
		var sess = StartSession();
		Walk(sess, "r1-apprentice");
		var s = Make(host, new SessionMapView(sess));
		string hash = sess.StateHash().ToString();
		var fired = new List<string>();
		s.NodeChosen += id => fired.Add(id);
		Assert.True(s.NodeFor("boss-tanuki") == null && s.NodeFor("r5-elder-oni") == null, "hidden nodes have no button to press");
		s.NodeFor("r1-apprentice")!.EmitSignal(Godot.BaseButton.SignalName.Pressed);
		Assert.Equal(0, fired.Count, "a node that is not a choice refuses");
		Assert.Equal(hash, sess.StateHash().ToString(), "run hash unchanged by refused presses");
		string habit = s.DetailHabit.Text; string name = s.DetailNameText;
		s.Refresh();
		Assert.Equal(habit, s.DetailHabit.Text, "habit unchanged after Refresh");
		Assert.Equal(name, s.DetailNameText, "name unchanged after Refresh");
		s.GetParent().QueueFree();
	}

	[Test]
	public static void DetailAndLegendReadsAreStable()
	{
		var sess = StartSession(9);
		string hash = sess.StateHash().ToString();
		var v = new SessionMapView(sess);
		var first = sess.Map.Graph.Nodes.Select(n => v.Detail(n.Id)).ToList();
		var again = sess.Map.Graph.Nodes.Select(n => v.Detail(n.Id)).ToList();
		for (int i = 0; i < first.Count; i++) Assert.Equal(first[i]!.ToString(), again[i]!.ToString(), "Detail is a pure read");
		Assert.True(v.TwistLegend.SequenceEqual(v.TwistLegend), "legend is stable");
		Assert.Equal(hash, sess.StateHash().ToString(), "reading details does not change the run");
	}

}
