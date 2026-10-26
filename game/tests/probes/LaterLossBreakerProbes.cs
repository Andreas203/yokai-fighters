using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.Dev;
using YokaiFighters.Fight;
using YokaiFighters.Sim;
using YokaiFighters.Story;

namespace YokaiFighters.Tests;

/// <summary>
/// Test Breaker probes for YOK-212 (S3 "later losses: variants by row reached and winning yokai"). Expected values come from S3
/// and the ticket's selection order (yokai card, then row band, then lose-screen; ties: narrower rows, then lower id; an elder
/// counts as its yokai), from the schema's own text ("a yokai id, never an elder's") and from the validator's documented contract
/// (tools/validate_data.py: "a file the game would refuse to load fails here first"). Cards named fixture-* are TEST FIXTURES.
/// </summary>
public static class LaterLossBreakerProbes
{
	static string Card(string id, string? yokai, string rows, string text = "TEST FIXTURE {0}: {{yokai}} stands.", string placeholders = "\"yokai\"") =>
		$$"""
		{ "kind": "story_card", "id": "{{id}}", "status": "proposed", "trigger": "TEST FIXTURE", "speaker": "narrator",
		  "text": "{{string.Format(text, id)}}", "placeholders": [{{placeholders}}], "rules": ["S3"],
		  "later_loss": { {{(yokai is null ? "" : $"\"yokai\": \"{yokai}\", ")}}{{rows}} } }
		""";

	static StoryCardData Fixture(string id, string? yokai, int min, int max) => StoryLibrary.Parse(Card(id, yokai, $"\"row_min\": {min}, \"row_max\": {max}"));

	static NodeContext At(string yokai, int row, string type = "duel", bool elder = false) =>
		NodeSceneTable.Default(ContentPaths.Data("map")).Resolve(new MapNodeData("n", row, type, yokai, elder)).Context;

	static StoryLibrary With(params StoryCardData[] extra) => new(FightScene.LoadStory().Cards.Concat(extra));

	static string Shown(StoryLibrary lib, NodeContext node) => FightScene.LoseCardFor(lib, node, node.Fighter).Text;

	static string Fill(StoryLibrary lib, string id, string name) => lib[id].FillText(yokai: name);

	static MapGraph DefaultMap() => new FileMapSource(Path.Combine(ContentPaths.Data("map"), "default-map.json")).Load();

	static IEnumerable<MapNodeData> FightNodes(MapGraph map) =>
		map.Nodes.Where(n => n.Type is NodeTypeRegistry.Duel or NodeTypeRegistry.ElderDuel or NodeTypeRegistry.Boss);

	// ---- advisory 1: the validator passes it, then the game ----

	static (bool Started, int Code) Validate(string file)
	{
		foreach (string exe in new[] { "python", "python3" })
		{
			try
			{
				var psi = new ProcessStartInfo(exe) { WorkingDirectory = ContentPaths.Current.Root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
				psi.ArgumentList.Add(Path.Combine(ContentPaths.Current.Root, "tools", "validate_data.py")); psi.ArgumentList.Add(file);
				using var p = Process.Start(psi)!;
				p.StandardOutput.ReadToEnd(); p.StandardError.ReadToEnd();
				p.WaitForExit();
				if (p.ExitCode > 1) continue; // 2 = the validator's setup problem (no jsonschema), not a verdict
				return (true, p.ExitCode);
			}
			catch (System.ComponentModel.Win32Exception) { }
		}
		return (false, -1);
	}

	// Validator contract (validate_data.py header): a file it passes must load. Each variant is a later_loss that the schema calls
	// an integer; if validate_data passes the file, StoryLibrary.Parse and the lose screen must take it.
	[Test]
	public static void ValidatorPass_ImpliesTheGameLoadsTheCard_AndTheLossScreenFillsIt()
	{
		var variants = new (string Name, string Json, int Row)[]
		{
			("row_min 4.0", Card("fx-float", null, "\"row_min\": 4.0, \"row_max\": 6"), 4),
			("row_max 1e1", Card("fx-exp", null, "\"row_min\": 1, \"row_max\": 1e1"), 4),
			("row_max 3000000000 (over Int32)", Card("fx-big", null, "\"row_min\": 1, \"row_max\": 3000000000"), 4),
			("row_min 9.0 above row_max 3", Card("fx-inverted", null, "\"row_min\": 9.0, \"row_max\": 3"), 4),
			("a {move} the lose screen never supplies", Card("fx-move", null, "\"row_min\": 1, \"row_max\": 1", "TEST FIXTURE {0}: {{move}} is gone.", "\"move\""), 1),
		};
		string tmp = Path.Combine(Path.GetTempPath(), "yok212-probe-" + Guid.NewGuid().ToString("N"));
		var bad = new List<string>();
		bool pythonSeen = false;
		try
		{
			foreach (var (name, json, row) in variants)
			{
				string dir = Path.Combine(tmp, name.Replace(' ', '_').Replace('{', '_').Replace('}', '_'), "data", "story");
				Directory.CreateDirectory(dir);
				string id = System.Text.Json.JsonDocument.Parse(json).RootElement.GetProperty("id").GetString()!;
				string file = Path.Combine(dir, id + ".json");
				File.WriteAllText(file, json);
				var (started, code) = Validate(file);
				if (!started) continue;
				pythonSeen = true;
				if (code != 0) continue; // the validator refused it: nothing to disagree about
				try
				{
					var lib = With(StoryLibrary.Parse(json));
					FightScene.LoseCardFor(lib, At("oni", row), "kitsune");
				}
				catch (Exception e) when (e is FormatException or InvalidOperationException or KeyNotFoundException)
				{
					bad.Add($"{name}: validate_data PASSES it, the game {e.GetType().Name}: {e.Message}");
				}
			}
		}
		finally { try { Directory.Delete(tmp, true); } catch (IOException) { } }
		if (!pythonSeen) { GD.Print("LaterLossBreakerProbes: no python on PATH, validator half skipped"); return; }
		Assert.True(bad.Count == 0, "\n" + string.Join("\n", bad));
	}

	// Schema text: yokai is "a yokai id, never an elder's"; the ticket: the winner is always the plain yokai. A card naming an id that no
	// loss can carry (elder-oni, apprentice, nine-tailed-kitsune: all have plays_as) must be refused at load, or must win for its node.
	[Test]
	public static void CardNamingAnIdNoWinnerCanHave_IsRefusedAtLoad_OrMatchesItsNode()
	{
		var bad = new List<string>();
		foreach (var node in FightNodes(DefaultMap()).Where(n => n.Yokai is "elder-oni" or "elder-kappa" or "apprentice" or "nine-tailed-kitsune").GroupBy(n => n.Yokai).Select(g => g.First()))
		{
			StoryCardData card;
			try { card = Fixture("fixture-dead", node.Yokai, 1, 9); }
			catch (FormatException) { continue; } // refused at load: fine
			var ctx = At(node.Yokai!, node.Row, node.Type, node.Elder);
			string got = FightScene.LoseCardFor(With(card), ctx, ctx.Fighter).Text;
			if (got != card.FillText(yokai: FightScene.NameOf(ctx, ctx.Fighter)))
				bad.Add($"yokai '{node.Yokai}' loads, is never chosen on its own node (row {node.Row}); that node's winner is '{ctx.DraftYokai}'");
		}
		Assert.True(bad.Count == 0, "\n" + string.Join("\n", bad));
	}

	// S4 + advisory 1: LoseCardFor supplies only {yokai}. A later_loss card using {move} must be refused at load, or the loss must fill it;
	// it must never be accepted and then throw when the player loses (the HUD keeps whatever text it had).
	[Test]
	public static void LaterLossCardWithMove_IsRefusedAtLoad_OrFilledAtTheLoss()
	{
		StoryCardData card;
		try { card = StoryLibrary.Parse(Card("fixture-move", null, "\"row_min\": 3, \"row_max\": 3", "TEST FIXTURE {0}: {{move}} is gone.", "\"move\"")); }
		catch (FormatException) { return; }
		var node = At("oni", 3);
		string shown;
		try { shown = FightScene.LoseCardFor(With(card), node, node.Fighter).Text; }
		catch (InvalidOperationException e) { throw new AssertFailed("accepted at load, thrown at the loss: " + e.Message); }
		Assert.True(!shown.Contains("{move}"), "no raw placeholder on the screen: " + shown);
	}

	// ---- advisory 2: matched AS the yokai it plays as ----

	[Test]
	public static void ApprenticeAndNineTails_AreMatchedAsTheKitsune_NotByBandAlone()
	{
		var lib = With(Fixture("fixture-kitsune", "kitsune", 1, 9), Fixture("fixture-oni", "oni", 1, 9));
		foreach (var (yokai, row, type, elder) in new[] { ("apprentice", 1, "duel", false), ("kitsune", 2, "duel", false), ("nine-tailed-kitsune", 5, "elder", true) })
		{
			var node = At(yokai, row, type, elder);
			Assert.Equal("kitsune", node.DraftYokai, yokai + " plays as the Kitsune");
			Assert.Equal(Fill(lib, "fixture-kitsune", FightScene.NameOf(node, node.Fighter)), Shown(lib, node), $"{yokai} row {row}: the Kitsune card, not a band");
		}
		Assert.Equal(Fill(lib, "fixture-oni", "Oni"), Shown(lib, At("oni", 4)), "Oni card for the Oni");
		Assert.Equal(Fill(lib, "lose-mid", "Kappa"), Shown(lib, At("kappa", 4)), "no Kappa card: the band, not the Kitsune or Oni card");
	}

	[Test]
	public static void ElderKappaAndTanuki_MatchByYokai_AndEveryElderNamesItselfInTheCard()
	{
		var lib = With(Fixture("fixture-kappa", "kappa", 1, 9), Fixture("fixture-tanuki", "tanuki", 9, 9), Fixture("fixture-oni", "oni", 1, 9), Fixture("fixture-kitsune", "kitsune", 1, 9));
		Assert.Equal(Fill(lib, "fixture-kappa", "Elder Kappa"), Shown(lib, At("elder-kappa", 8, "elder", true)), "Elder Kappa -> Kappa card, {yokai} = Elder Kappa");
		Assert.Equal(Fill(lib, "fixture-oni", "Elder Oni"), Shown(lib, At("elder-oni", 8, "elder", true)), "Elder Oni -> Oni card, {yokai} = Elder Oni");
		Assert.Equal(Fill(lib, "fixture-tanuki", "Tanuki"), Shown(lib, At("tanuki", 9, "boss")), "the Tanuki at row 9");
		Assert.Equal(Fill(lib, "lose-late", "Tanuki"), Shown(lib, At("tanuki", 8, "boss")), "a Tanuki card for row 9 only: row 8 falls to the band");
		string nine = Shown(lib, At("nine-tailed-kitsune", 5, "elder", true));
		foreach (string raw in new[] { "nine-tailed", "elder-", "{yokai}" })
			Assert.True(!nine.Contains(raw), $"raw '{raw}' on the Nine-Tails screen: {nine}");
	}

	// ---- ties, ranges, boundaries ----

	[Test]
	public static void OneRowCards_CoverExactlyTheirRow()
	{
		var lib = With(Fixture("fixture-oni-4", "oni", 4, 4), Fixture("fixture-band-5", null, 5, 5));
		Assert.Equal("fixture-oni-4", lib.LaterLossCard("oni", 4).Id, "row_min == row_max covers that row");
		Assert.Equal("lose-early", lib.LaterLossCard("oni", 3).Id, "one row below: the band");
		Assert.Equal("fixture-band-5", lib.LaterLossCard("oni", 5).Id, "one row above: the one-row band (0 wide) beats lose-mid (2 wide)");
		Assert.Equal("lose-mid", lib.LaterLossCard("kitsune", 4).Id, "the Oni's one row is not the Kitsune's");
		Assert.Equal("lose-mid", lib.LaterLossCard("kitsune", 6).Id, "one-row band 5 does not leak to 6");
	}

	[Test]
	public static void RealBands_RowEdgesAndOutOfRangeRows()
	{
		var lib = FightScene.LoadStory();
		foreach (var (row, want) in new[] { (1, "lose-early"), (3, "lose-early"), (4, "lose-mid"), (6, "lose-mid"), (7, "lose-late"), (9, "lose-late"),
			(0, "lose-screen"), (-1, "lose-screen"), (10, "lose-screen"), (int.MinValue, "lose-screen"), (int.MaxValue, "lose-screen") })
			Assert.Equal(want, lib.LaterLossCard("kitsune", row).Id, "row " + row);
		foreach (string? winner in new string?[] { null, "", "KITSUNE", "no-such-yokai" })
			Assert.Equal("lose-early", lib.LaterLossCard(winner, 2).Id, $"winner '{winner}' at row 2: bands only");
		Assert.Equal("lose-screen", lib.LaterLossCard(null, null).Id, "no winner, no row");
	}

	[Test]
	public static void EveryShippedMapFightNode_HasABand_NeverFallsToLoseScreen()
	{
		var lib = FightScene.LoadStory();
		var bad = new List<string>();
		foreach (string file in new[] { "default-map.json", "demo-map.json" })
		{
			var map = new FileMapSource(Path.Combine(ContentPaths.Data("map"), file)).Load();
			foreach (var n in FightNodes(map))
			{
				var ctx = NodeSceneTable.Default(ContentPaths.Data("map")).Resolve(n).Context;
				string id = lib.LaterLossCard(ctx.DraftYokai, ctx.Row).Id;
				if (id == StoryLibrary.LoseScreenId) bad.Add($"{file} {n.Id} row {n.Row}");
			}
		}
		Assert.True(bad.Count == 0, "no later-loss card covers: " + string.Join(", ", bad));
	}

	// Determinism: the same cards in any load order, the same pick, for every winner and row; checked against a plain scan of S3's order.
	[Test]
	public static void RandomLibraries_AnyLoadOrder_PickWhatTheWrittenOrderPicks()
	{
		string?[] winners = { null, "", "kitsune", "oni", "kappa", "tanuki" };
		string?[] yokai = { null, null, "kitsune", "oni", "kappa" };
		for (int seed = 1; seed <= 25; seed++)
		{
			var rng = new Random(seed);
			var cards = Enumerable.Range(0, 14).Select(i =>
			{
				int a = rng.Next(1, 10), b = rng.Next(1, 10);
				return Fixture($"c{i:D2}", yokai[rng.Next(yokai.Length)], Math.Min(a, b), Math.Max(a, b));
			}).Append(FightScene.LoadStory()[StoryLibrary.LoseScreenId]).ToList();
			var shuffled = cards.OrderBy(_ => rng.Next()).ToList();
			var libs = new[] { new StoryLibrary(cards), new StoryLibrary(shuffled), new StoryLibrary(shuffled.AsEnumerable().Reverse()) };
			foreach (string? w in winners)
				for (int row = -1; row <= 11; row++)
				{
					int bestStage = 99, bestWidth = 99; string? want = null;
					foreach (var c in cards)
					{
						if (c.LaterLoss is not { } l || row < l.RowMin || row > l.RowMax) continue;
						int stage = w is not null && l.Yokai == w ? 0 : l.Yokai is null ? 1 : 2;
						if (stage == 2) continue; // names another yokai
						int width = l.RowMax - l.RowMin;
						bool better = stage != bestStage ? stage < bestStage : width != bestWidth ? width < bestWidth : string.CompareOrdinal(c.Id, want) < 0;
						if (better) { bestStage = stage; bestWidth = width; want = c.Id; }
					}
					want ??= StoryLibrary.LoseScreenId;
					foreach (var lib in libs)
						Assert.Equal(want, lib.LaterLossCard(w, row).Id, $"seed {seed}, winner '{w}', row {row}");
				}
		}
	}

	// ---- the scene: a real loss, in a hosted run ----

	static MapNodeData NodeOn(string id) => DefaultMap().Nodes.First(n => n.Id == id);

	static RunSession SessionOn(MapNodeData node, AbilityPool pool)
	{
		var run = DevRunStates.Source.Build(pool);
		run.StartMap(new MapGraph("probe", new[] { node }, Array.Empty<MapRoute>(), NodeTypeRegistry.Default(), "probe"));
		var s = new RunSession(run, pool, NodeSceneTable.Default(ContentPaths.Data("map")), 21);
		Assert.True(s.Enter(node.Id).Ok, "enter " + node.Id);
		return s;
	}

	static FightScene Host(Godot.Node runner, RunSession s)
	{
		var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		scene.ExternalDrive = true;
		scene.Session = s;
		scene.FirstMeeting = true;
		runner.AddChild(scene);
		return scene;
	}

	static void LoseTo(FightScene s)
	{
		s.TwistCard?.Continue();
		for (int t = 0; t < 900 && s.Match.Phase != MatchPhase.Over; t++) s.Step(FighterInput.None, new FighterInput(t % 2 == 0 ? InputBits.DebugStrike : InputBits.None));
		Assert.Equal(1, s.Match.Winner, "the opponent won");
	}

	// S3 on the screen: a real loss on every fight node of the default map shows the band the row names (data/story triggers: rows
	// 1-3, 4-6, 7-9 and the boss), with {yokai} = the name the screens use; a stale HUD text must not survive the loss.
	[Test]
	public static void RealLossOnEveryDefaultMapFightNode_ShowsTheRowBand_NotStale(Godot.Node runner)
	{
		var pool = FightScene.LoadAbilityPool();
		var lib = FightScene.LoadStory();
		var bad = new List<string>();
		foreach (var n in FightNodes(DefaultMap()))
		{
			string band = n.Row <= 3 ? "lose-early" : n.Row <= 6 ? "lose-mid" : "lose-late";
			var scene = Host(runner, SessionOn(n, pool));
			try
			{
				var hud = scene.GetNode<FightHud>("Hud");
				hud.SetLoseText(null, "stale");
				LoseTo(scene);
				string name = FightScene.NameOf(scene.Node, scene.Node.Fighter);
				string want = lib[band].FillText(yokai: name);
				if (hud.LoseText != want) bad.Add($"{n.Id} (row {n.Row}): HUD '{hud.LoseText}', want {band} '{want}'");
			}
			finally { scene.QueueFree(); }
		}
		Assert.True(bad.Count == 0, "\n" + string.Join("\n", bad));
	}

	// Repeated losses in one scene: lose, Restart (a fresh run), lose again; the second screen is the card for the second loss, never the first's or a leftover.
	[Test]
	public static void TwoLossesInOneScene_EachScreenIsTheCardForThatLoss(Godot.Node runner)
	{
		var saved = FightScene.Sources; var savedRun = FightScene.Run;
		FightScene.Sources = FightScene.FixtureSources; FightScene.Run = null;
		var s = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		s.ExternalDrive = true;
		runner.AddChild(s);
		try
		{
			var hud = s.GetNode<FightHud>("Hud");
			for (int loss = 1; loss <= 2; loss++)
			{
				hud.SetLoseText(null, "stale");
				LoseTo(s);
				var (_, want) = FightScene.LoseCardFor(FightScene.LoadStory(), s.Node, FightScene.Opponent);
				Assert.Equal(want, hud.LoseText, $"loss {loss}: the card for row {s.Node.Row}");
				hud.GetNode<Godot.Button>("LoseScreen/Restart").EmitSignal(Godot.BaseButton.SignalName.Pressed);
				Assert.Equal(DemoStage.Fighting, s.Stage, $"loss {loss}: restarted");
			}
		}
		finally { s.QueueFree(); FightScene.Sources = saved; FightScene.Run = savedRun; }
	}
}
