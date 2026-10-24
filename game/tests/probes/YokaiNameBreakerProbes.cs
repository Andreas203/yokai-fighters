using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Godot;
using YokaiFighters.Dev;
using YokaiFighters.Fight;
using YokaiFighters.Sim;
using YokaiFighters.Story;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>Test Breaker probes for YOK-187. Expected names come from R6 (elders "Elder &lt;yokai&gt;") and the decision log 2026-10-21; a duel names its yokai, the apprentice the Kitsune.</summary>
public static class YokaiNameBreakerProbes
{
	static StoryLibrary LibFor(ElderTwist t)
	{
		var ids = new[] { t.LabelKey, t.Id + "-label", t.IntroCard, t.CalloutCard }.Where(x => x != null).Select(x => x!).Distinct();
		return new(ids.Select(i => new StoryCardData(i, null, "{yokai} stands.", Array.Empty<string>(), "")));
	}

	static MapGraph DefaultMap() => new FileMapSource(Path.Combine(ContentPaths.Data("map"), "default-map.json")).Load();

	static IEnumerable<MapNodeData> FightNodes() =>
		DefaultMap().Nodes.Where(n => n.Type is NodeTypeRegistry.Duel or NodeTypeRegistry.ElderDuel or NodeTypeRegistry.Boss);

	/// <summary>R6/Y1: the name a node's screens must show. Null = the rule leaves it open (the Nine-Tailed Kitsune elder).</summary>
	static string? Expected(MapNodeData n) => n.Yokai switch
	{
		"apprentice" => "Kitsune",
		"elder-oni" => "Elder Oni",
		"elder-kappa" => "Elder Kappa",
		"nine-tailed-kitsune" => null,
		{ } y => StoryLibrary.DisplayName(y),
		_ => null,
	};

	static RunSession SessionOn(MapNodeData node, AbilityPool pool)
	{
		var run = DevRunStates.Source.Build(pool);
		run.StartMap(new MapGraph("probe", new[] { node }, Array.Empty<MapRoute>(), NodeTypeRegistry.Default(), "probe"));
		var s = new RunSession(run, pool, NodeSceneTable.Default(ContentPaths.Data("map")), 21);
		Assert.True(s.Enter(node.Id).Ok, "enter " + node.Id);
		return s;
	}

	static FightScene Host(Node runner, RunSession s)
	{
		var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		scene.ExternalDrive = true;
		scene.Session = s;
		scene.FirstMeeting = true;
		runner.AddChild(scene);
		return scene;
	}

	static string Surfaces(FightScene s, FightHud hud)
	{
		hud.Refresh(s.Match);
		var pause = s.BuildPauseSummary();
		return string.Join(" | ", s.OpponentName, hud.OpponentLabel, hud.LoseText, pause.Position, pause.Opponent.Name, hud.TwistLine);
	}

	// R6 + decision log 2026-10-21: every fight node of the default map, entered through a real RunSession and fight scene.
	[Test]
	public static void EveryDefaultMapFightNode_NamesTheYokai_NeverTheBodyOrRawId(Node runner)
	{
		var pool = FightScene.LoadAbilityPool();
		var bad = new List<string>();
		foreach (var node in FightNodes())
		{
			var scene = Host(runner, SessionOn(node, pool));
			try
			{
				var hud = scene.GetNode<FightHud>("Hud");
				string all = Surfaces(scene, hud);
				string? want = Expected(node);
				bool kitsuneOk = node.Yokai is "kitsune" or "apprentice" or "nine-tailed-kitsune";
				if (want != null && scene.OpponentName != want) bad.Add($"{node.Id}: OpponentName '{scene.OpponentName}' want '{want}'");
				if (want != null && hud.OpponentLabel != want.ToUpperInvariant()) bad.Add($"{node.Id}: HUD '{hud.OpponentLabel}' want '{want.ToUpperInvariant()}'");
				if (want != null && !hud.LoseText.Contains(want)) bad.Add($"{node.Id}: lose card lacks '{want}': {hud.LoseText}");
				if (!kitsuneOk && all.Contains("Kitsune", StringComparison.OrdinalIgnoreCase)) bad.Add($"{node.Id}: shows Kitsune: {all}");
				foreach (string raw in new[] { "elder-", "nine-tailed", "-kappa", "-oni" })
					if (all.Contains(raw, StringComparison.OrdinalIgnoreCase)) bad.Add($"{node.Id}: raw id '{raw}': {all}");
				if (node.Elder && !scene.OpponentName.StartsWith("Elder ") && !scene.OpponentName.Contains("Nine")) bad.Add($"{node.Id}: elder not named Elder: {scene.OpponentName}");
			}
			finally { scene.QueueFree(); }
		}
		Assert.True(bad.Count == 0, string.Join("\n", bad));
	}

	// Y3/R6: elder twist strip, intro card and callout all name the elder.
	[Test]
	public static void ElderNodes_TwistSurfacesNameTheElder(Node runner)
	{
		var pool = FightScene.LoadAbilityPool();
		var bad = new List<string>();
		foreach (var node in FightNodes().Where(n => n.Elder && Expected(n) != null))
		{
			var twist = FightScene.Twists.For(DevJump.ElderOf(node.Yokai!.Replace("elder-", ""))?.ElderYokai, true);
			if (twist == null) continue;
			var saved = TwistCopy.Story;
			TwistCopy.Story = LibFor(twist);
			var scene = Host(runner, SessionOn(node, pool));
			try
			{
				var hud = scene.GetNode<FightHud>("Hud");
				hud.Refresh(scene.Match);
				string want = Expected(node)! + " stands.";
				if (hud.TwistLine != want) bad.Add($"{node.Id}: strip '{hud.TwistLine}'");
				if (scene.TwistCard?.Text != want) bad.Add($"{node.Id}: intro '{scene.TwistCard?.Text}'");
				scene.TwistCard?.Continue();
				if (hud.TwistCalloutText != want) bad.Add($"{node.Id}: callout '{hud.TwistCalloutText}'");
			}
			finally { TwistCopy.Story = saved; scene.QueueFree(); }
		}
		Assert.True(bad.Count == 0, string.Join("\n", bad));
	}

	// Cache keyed on id and name: the same scene and HUD, moved to another node, must show the new label even when the id repeats or the name repeats.
	[Test]
	public static void Hud_LabelFollowsSceneToNextNode_NotStale(Node runner)
	{
		var pool = FightScene.LoadAbilityPool();
		var nodes = DefaultMap().Nodes.ToDictionary(n => n.Id);
		var scene = Host(runner, SessionOn(nodes["r5-elder-oni"], pool));
		try
		{
			var hud = scene.GetNode<FightHud>("Hud");
			hud.Refresh(scene.Match);
			Assert.Equal("ELDER ONI", hud.OpponentLabel, "first node");
			foreach (string next in new[] { "r3-oni", "r5-elder-oni", "r6-kappa", "r8-elder-kappa", "r2-oni", "boss-tanuki" })
			{
				scene.Session = SessionOn(nodes[next], pool);
				typeof(FightScene).GetMethod("BeginNode", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(scene, null);
				hud.Refresh(scene.Match);
				Assert.Equal(Expected(nodes[next])!.ToUpperInvariant(), hud.OpponentLabel, "after moving to " + next);
				Assert.True(hud.OpponentLabel != "KITSUNE", "stand-in leaked at " + next);
			}
			hud.ShowOpponent("oni", "Oni"); Assert.Equal("ONI", hud.OpponentLabel, "oni");
			hud.ShowOpponent("oni", "Elder Oni"); Assert.Equal("ELDER ONI", hud.OpponentLabel, "elder oni, same id");
			hud.ShowOpponent("oni"); Assert.Equal("ONI", hud.OpponentLabel, "null name falls back to the id's own");
		}
		finally { scene.QueueFree(); }
	}

	// The portrait is the yokai's, never the Kitsune's mask for the Oni/Kappa, and never an error for an id with no asset.
	[Test]
	public static void Portrait_PerYokaiId_RightOrHidden_NeverThrows(Node runner)
	{
		var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		scene.ExternalDrive = true;
		runner.AddChild(scene);
		try
		{
			var hud = scene.GetNode<FightHud>("Hud");
			var portrait = hud.FindChild("P2Portrait", true, false) as TextureRect ?? throw new Exception("no P2Portrait");
			var fox = GD.Load<Texture2D>("res://assets/generated/ui/fox-mask.png");
			foreach (string id in new[] { "kitsune", "nine-tailed-kitsune", "oni", "kappa", "elder-oni", "elder-kappa", "tanuki", "apprentice", "no-such-yokai", "" })
			{
				hud.ShowOpponent(id);
				string path = FightHud.PortraitPath(id);
				bool exists = path.Length > 0 && ResourceLoader.Exists(path);
				Assert.Equal(exists, portrait.Visible, "visible for '" + id + "'");
				if (exists) Assert.True(portrait.Texture != null, "texture for " + id);
				else Assert.True(portrait.Texture == null, "no stale texture for " + id);
				if (id is "oni" or "kappa" or "tanuki" or "no-such-yokai" or "elder-oni" or "elder-kappa")
					Assert.True(portrait.Texture != fox || !portrait.Visible, "fox mask on " + id);
			}
		}
		finally { scene.QueueFree(); }
	}

	// The portrait of an elder node is chosen by DraftYokai (the yokai), so Elder Oni shows the Oni, not the fox mask.
	[Test]
	public static void ElderOniNode_PortraitIsNotTheFoxMask(Node runner)
	{
		var pool = FightScene.LoadAbilityPool();
		var node = DefaultMap().Nodes.First(n => n.Id == "r5-elder-oni");
		var scene = Host(runner, SessionOn(node, pool));
		try
		{
			Assert.Equal("oni", scene.OpponentYokai, "OpponentYokai");
			var hud = scene.GetNode<FightHud>("Hud");
			hud.Refresh(scene.Match);
			var p = (TextureRect)hud.FindChild("P2Portrait", true, false)!;
			Assert.True(!p.Visible || p.Texture != GD.Load<Texture2D>("res://assets/generated/ui/fox-mask.png"), "fox mask on Elder Oni");
		}
		finally { scene.QueueFree(); }
	}

	// DevJump.NameOf for every opponent and elder combination, and the Tanuki.
	[Test]
	public static void DevJump_NameOf_EveryNode()
	{
		foreach (string id in DevJump.OpponentIds)
		{
			Assert.Equal(StoryLibrary.DisplayName(id), DevJump.NameOf(new DevFightSetup(id)), "plain " + id);
			if (DevJump.ElderOf(id) is not null && id != "kitsune")
				Assert.Equal("Elder " + StoryLibrary.DisplayName(id), DevJump.NameOf(new DevFightSetup(id, Elder: true)), "elder " + id);
		}
		Assert.Equal("Tanuki", DevJump.NameOf(new DevFightSetup("tanuki")), "tanuki");
		Assert.True(!DevJump.NameOf(new DevFightSetup("oni")).Contains("Kitsune"), "oni dev card names kitsune");
	}

	// Dev jump through a real scene, every opponent/elder: the scene surfaces equal NameOf.
	[Test]
	public static void DevJumpScene_EveryOpponent_SceneNameEqualsNameOf(Node runner)
	{
		var pool = FightScene.LoadAbilityPool();
		var bad = new List<string>();
		foreach (string id in DevJump.OpponentIds)
			foreach (bool elder in new[] { false, true })
			{
				if (elder && (id == "tanuki" || DevJump.ElderOf(id) is null)) continue;
				var setup = new DevFightSetup(id, Elder: elder, Seed: 5);
				var jump = DevJump.BuildFight(setup, pool);
				Assert.True(jump.Session.Enter(jump.NodeId).Ok, "enter");
				var scene = Host(runner, jump.Session);
				try
				{
					var hud = scene.GetNode<FightHud>("Hud");
					hud.Refresh(scene.Match);
					if (scene.OpponentName != DevJump.NameOf(setup)) bad.Add($"{id}/{elder}: scene '{scene.OpponentName}' vs NameOf '{DevJump.NameOf(setup)}'");
					if (hud.OpponentLabel != scene.OpponentName.ToUpperInvariant()) bad.Add($"{id}/{elder}: HUD '{hud.OpponentLabel}'");
				}
				finally { scene.QueueFree(); }
			}
		Assert.True(bad.Count == 0, string.Join("\n", bad));
	}

	// Restart after a loss keeps the right name: own-session scene, and a hosted elder scene whose host restarts.
	[Test]
	public static void RestartAfterLoss_KeepsRightName(Node runner)
	{
		var saved = (FightScene.Sources, FightScene.Run);
		var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		scene.ExternalDrive = true;
		runner.AddChild(scene);
		try
		{
			var hud = scene.GetNode<FightHud>("Hud");
			scene.Session!.LoseFight();
			scene.RestartRun();
			hud.Refresh(scene.Match);
			Assert.Equal("Kitsune", scene.OpponentName, "after restart");
			Assert.Equal("KITSUNE", hud.OpponentLabel, "HUD after restart");
			Assert.True(hud.LoseText.Contains("Kitsune"), "lose text after restart: " + hud.LoseText);
		}
		finally { scene.QueueFree(); (FightScene.Sources, FightScene.Run) = saved; }

		var pool = FightScene.LoadAbilityPool();
		var node = DefaultMap().Nodes.First(n => n.Id == "r8-elder-oni");
		var hosted = Host(runner, SessionOn(node, pool));
		try
		{
			bool asked = false;
			hosted.RunRestartRequested += () => asked = true;
			hosted.Session!.LoseFight();
			hosted.RestartRun();
			Assert.True(asked, "host asked for the restart");
			Assert.Equal("Elder Oni", hosted.OpponentName, "hosted scene keeps the node's name until the host replaces it");
		}
		finally { hosted.QueueFree(); }
	}
}
