using System;
using System.Collections.Generic;
using Godot;
using YokaiFighters.Dev;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-144: fight.tscn holds an empty StageContainer; the stage comes from the node-scene table's <c>stage</c>
/// (nodes → types → default_stage), the bamboo grove by default. Presentation only.
/// </summary>
public static class StageContainerTests
{
	const string Table = """
	{ "kind": "node_scenes", "id": "t", "schema_version": 1, "default_scene": "res://scenes/fight.tscn", "fallback_fighter": "kitsune",
	  "tiers": { "yokai": { "reaction_frames": 22 } },
	  "types": { "duel": { "tier": "yokai", "stage": "res://scenes/stage/placeholder.tscn" }, "elder": { "tier": "yokai" } },
	  "nodes": { "den": { "stage": "res://scenes/stage/den.tscn" } },
	  "opponents": { "kitsune": { "fighter": "kitsune" } } }
	""";

	[Test]
	public static void Table_StageIsResolvedNodeThenTypeThenDefault()
	{
		var t = NodeSceneTable.Parse(Table);
		Assert.Equal("res://scenes/stage/den.tscn", t.Resolve(new MapNodeData("den", 1, "duel", "kitsune")).Context.Stage, "the node's own stage wins");
		Assert.Equal(FightScene.PlaceholderScene, t.Resolve(new MapNodeData("a", 1, "duel", "kitsune")).Context.Stage, "then the type's");
		Assert.Equal<string?>(null, t.Resolve(new MapNodeData("e", 1, "elder", "kitsune")).Context.Stage, "no default_stage: null (the fight scene's default)");
		var withDefault = NodeSceneTable.Parse(Table.Replace("\"fallback_fighter\"", "\"default_stage\": \"res://scenes/stage/bamboo_grove.tscn\", \"fallback_fighter\""));
		Assert.Equal(FightScene.GroveScene, withDefault.Resolve(new MapNodeData("e", 1, "elder", "kitsune")).Context.Stage, "default_stage fills the rest");
	}

	[Test]
	public static void Table_RejectsAStageOutsideTheStageFolderOrOnAnOpponent()
	{
		foreach (var (json, what) in new[]
		{
			(Table.Replace("res://scenes/stage/placeholder.tscn", "res://scenes/fight.tscn"), "a type stage outside scenes/stage/"),
			(Table.Replace("res://scenes/stage/den.tscn", "scenes/stage/den.tscn"), "a node stage without res://"),
			(Table.Replace("res://scenes/stage/den.tscn", "res://scenes/stage/.tscn"), "an empty stage name"),
			(Table.Replace("res://scenes/stage/den.tscn", "res://scenes/stage/../fight.tscn"), "a '..' escaping the stage folder"),
			(Table.Replace("res://scenes/stage/den.tscn", "res://scenes/stage//den.tscn"), "a '//' in the stage path"),
			(Table.Replace("res://scenes/stage/den.tscn", "res://scenes/stage/sub\\\\den.tscn"), "a backslash in the stage path"),
			(Table.Replace("\"fallback_fighter\"", "\"default_stage\": \"res://scenes/stage/x.png\", \"fallback_fighter\""), "a default_stage that is not a scene"),
			(Table.Replace("{ \"fighter\": \"kitsune\" }", "{ \"fighter\": \"kitsune\", \"stage\": \"res://scenes/stage/placeholder.tscn\" }"), "a stage on an opponent"),
		})
		{
			bool threw = false;
			try { NodeSceneTable.Parse(json); } catch (FormatException) { threw = true; }
			Assert.True(threw, $"{what} is refused");
		}
	}

	[Test]
	public static void DataTable_EveryFightNodeShowsTheBambooGrove()
	{
		var t = NodeSceneTable.Default(ContentPaths.Data("map"));
		foreach (string type in new[] { "duel", "elder", "boss" })
			Assert.Equal(FightScene.GroveScene, t.Resolve(new MapNodeData("n", 1, type, "kitsune")).Context.Stage, $"{type}: the grove (unchanged by YOK-144)");
	}

	[Test]
	public static void StageSceneFor_PicksTheNodeStage_FallsBackToGroveThenPlaceholder()
	{
		Func<string, bool> all = _ => true, none = _ => false;
		const string den = "res://scenes/stage/den.tscn";
		Assert.Equal(FightScene.GroveScene, FightScene.StageSceneFor(null, false, true, all), "no node stage: the grove");
		Assert.Equal(den, FightScene.StageSceneFor(den, false, true, all), "the node's stage");
		Assert.Equal(den, FightScene.StageSceneFor(den, false, false, all), "grove assets do not matter for another stage");
		var warnings = new List<string>();
		Assert.Equal(FightScene.GroveScene, FightScene.StageSceneFor(den, false, true, none, warnings.Add), "a missing stage falls back to the grove");
		Assert.True(warnings.Count == 1 && warnings[0].Contains(den), "and says so");
		Assert.Equal(FightScene.PlaceholderScene, FightScene.StageSceneFor(den, false, false, none), "missing stage and grove assets: the placeholder");
		Assert.Equal(FightScene.PlaceholderScene, FightScene.StageSceneFor(null, false, false, all), "grove assets missing: the placeholder");
		Assert.Equal(FightScene.PlaceholderScene, FightScene.StageSceneFor(den, true, true, all), "the debug flag wins");
	}

	[Test]
	public static void FightScene_BuildsTheStageInsideTheContainer(Node runner)
	{
		var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		scene.ExternalDrive = true;
		runner.AddChild(scene);
		try
		{
			var container = scene.GetNode<Node3D>(FightScene.StageContainerName);
			Assert.Equal(0, container.GetIndex(), "the container is fight.tscn's first child (scenery under the UI and fighters)");
			Assert.Equal(1, container.GetChildCount(), "one stage");
			Assert.True(ReferenceEquals(container.GetNode(FightScene.StageName), scene.StageRoot), "StageRoot is StageContainer/Stage");
			Assert.Equal(FightScene.GroveScene, scene.StageRoot.SceneFilePath, "default: the bamboo grove");
			Assert.True(scene.GetNodeOrNull(FightScene.StageName) is null, "no stage left directly under the fight");
		}
		finally { scene.QueueFree(); }
	}

	sealed class StageOverride(INodeSceneResolver inner, string stage) : INodeSceneResolver
	{
		public NodeScene Resolve(MapNodeData node)
		{
			var s = inner.Resolve(node);
			return s with { Context = s.Context with { Stage = stage } };
		}
	}

	[Test]
	public static void FightScene_ShowsTheStageTheNodeNames(Node runner)
	{
		// The wiring: a node whose data names the placeholder stage gets it, with the grove's assets present.
		var pool = FightScene.LoadAbilityPool();
		var jump = DevJump.BuildFight(new DevFightSetup("kitsune"), pool);
		var session = new RunSession(jump.Session.Run, pool, new StageOverride(jump.Session.Scenes, FightScene.PlaceholderScene), jump.Session.Seed);
		Assert.True(session.Enter(jump.NodeId).Ok, "node entered");
		var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		scene.ExternalDrive = true;
		scene.Session = session;
		runner.AddChild(scene);
		try
		{
			Assert.Equal(FightScene.PlaceholderScene, scene.StageRoot.SceneFilePath, "the node's stage");
			Assert.Equal("placeholder", scene.StageKind, "placeholder kind");
		}
		finally { scene.QueueFree(); }
	}
}
