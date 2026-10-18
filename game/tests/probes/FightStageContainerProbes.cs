using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using YokaiFighters.Dev;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>YOK-144 probes. Presentation-only ticket: the expected values come from the ticket's claims (no rule ID); the sim must be untouched (determinism, hash).</summary>
public static class FightStageContainerProbes
{
	sealed class Over(INodeSceneResolver inner, string? stage) : INodeSceneResolver
	{
		public NodeScene Resolve(MapNodeData node)
		{
			var s = inner.Resolve(node);
			return s with { Context = s.Context with { Stage = stage } };
		}
	}

	static (FightScene scene, RunSession session) Make(Node runner, string opponent = "kitsune", bool elder = false, bool overrideStage = false, string? stage = null, Action<FightScene>? beforeAdd = null)
	{
		var pool = FightScene.LoadAbilityPool();
		var jump = DevJump.BuildFight(new DevFightSetup(opponent, Elder: elder), pool);
		var session = overrideStage ? new RunSession(jump.Session.Run, pool, new Over(jump.Session.Scenes, stage), jump.Session.Seed) : jump.Session;
		Assert.True(session.Enter(jump.NodeId).Ok, "node entered");
		var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		scene.ExternalDrive = true;
		scene.Session = session;
		beforeAdd?.Invoke(scene);
		runner.AddChild(scene);
		return (scene, session);
	}

	static void CleanBodies(FightScene s)
	{
		var c = s.GetNode(FightScene.StageContainerName);
		Assert.Equal(1, c.GetChildCount(), "exactly one stage under StageContainer");
		Assert.Equal(2, s.GetNode(FightScene.FightersName).GetChildCount(), "exactly two bodies under Fighters");
	}

	[Test]
	public static void MissingStagePath_FallsBackToGrove_NoLeak(Node runner)
	{
		var (s, _) = Make(runner, overrideStage: true, stage: "res://scenes/stage/does_not_exist.tscn");
		try { Assert.Equal(FightScene.GroveScene, s.StageRoot.SceneFilePath, "missing stage -> grove"); CleanBodies(s); }
		finally { s.QueueFree(); }
	}

	[Test]
	public static void NonFightStageScene_GivesPlaceholder_NoLeak(Node runner)
	{
		// the claim: a non-FightStage stage gives the placeholder
		var (s, _) = Make(runner, overrideStage: true, stage: "res://scenes/fighters/ryo.tscn");
		try
		{
			Assert.Equal(FightScene.PlaceholderScene, s.StageRoot.SceneFilePath, "not a FightStage -> placeholder");
			Assert.Equal("placeholder", s.StageKind, "kind");
			CleanBodies(s);
		}
		finally { s.QueueFree(); }
	}

	[Test]
	public static void NonNode3DStageScene_GivesPlaceholder(Node runner)
	{
		// a stage scene whose root is not a Node3D at all (the scene tree root of fight.tscn's hud)
		var (s, _) = Make(runner, overrideStage: true, stage: "res://scenes/ui/hud.tscn");
		try { Assert.Equal(FightScene.PlaceholderScene, s.StageRoot.SceneFilePath, "a Control-rooted scene -> placeholder"); CleanBodies(s); }
		finally { s.QueueFree(); }
	}

	[Test]
	public static void Table_RefusesStagePathThatEscapesTheStageFolder()
	{
		// claim: a stage outside res://scenes/stage/ is refused. '..' resolves outside it.
		const string json = """
		{ "kind": "node_scenes", "id": "t", "schema_version": 1, "default_scene": "res://scenes/fight.tscn", "fallback_fighter": "kitsune",
		  "tiers": { "yokai": { "reaction_frames": 22 } }, "types": { "duel": { "tier": "yokai", "stage": "res://scenes/stage/../fight.tscn" } },
		  "opponents": { "kitsune": { "fighter": "kitsune" } } }
		""";
		bool threw = false;
		try { NodeSceneTable.Parse(json); } catch (FormatException) { threw = true; }
		Assert.True(threw, "res://scenes/stage/../fight.tscn is outside res://scenes/stage/");
	}

	[Test]
	public static void Table_StageOnOpponentRefused_AlsoWhenNamedByTypeBlock()
	{
		const string json = """
		{ "kind": "node_scenes", "id": "t", "schema_version": 1, "default_scene": "res://scenes/fight.tscn", "fallback_fighter": "kitsune",
		  "tiers": { "yokai": { "reaction_frames": 22 } }, "types": { "duel": { "tier": "yokai" } },
		  "opponents": { "kitsune": { "tier": "yokai", "stage": "res://scenes/stage/placeholder.tscn" } } }
		""";
		bool threw = false;
		try { NodeSceneTable.Parse(json); } catch (FormatException) { threw = true; }
		Assert.True(threw, "an opponent entry without 'fighter' still refuses a stage");
	}

	[Test]
	public static void OpponentBodiesAndFraming(Node runner)
	{
		foreach (var (opp, elder) in new[] { ("kitsune", false), ("oni", false), ("oni", true), ("kappa", false), ("kappa", true), ("tanuki", false) })
		{
			var (s, session) = Make(runner, opp, elder);
			try
			{
				var node = session.Node!;
				var want = FighterAnimSet.ForNode(FightScene.Opponent, node.Yokai);
				var p2 = s.GetNode<Node3D>($"{FightScene.FightersName}/P2");
				Assert.Equal(want.ScenePath, p2.SceneFilePath, $"{opp} elder={elder}: P2 body");
				Assert.Equal(FighterAnimSet.Ryo.ScenePath, s.GetNode<Node3D>($"{FightScene.FightersName}/P1").SceneFilePath, "P1 is Ryo");
				bool tall = node.Yokai is "oni" or "elder-oni";
				Assert.Equal(tall ? 3.15f : CameraConfig.Default.ModelTopMeters, s.CameraFraming.ModelTopMeters, $"{opp} elder={elder}: framing top");
				Assert.Equal(tall ? s.CameraFraming.TallFeetClearPx : 0f, s.CameraFraming.FeetClearPx, $"{opp} elder={elder}: feet clear");
				CleanBodies(s);
			}
			finally { s.QueueFree(); }
		}
	}

	[Test]
	public static void SpawnOf_FollowsMovedMarkers_AndDefaultsWhenMissing(Node runner)
	{
		// Bodies are re-placed by the sim at the first Render, so the marker is observable only through SpawnOf.
		var p1 = new Vector3(-4f, 0.5f, 0.25f);
		var (s, _) = Make(runner, beforeAdd: f =>
		{
			f.GetNode<Marker3D>(FightScene.P1SpawnName).Position = p1;
			var m = f.GetNode(FightScene.P2SpawnName); f.RemoveChild(m); m.Free();
		});
		try
		{
			Assert.True(s.SpawnOf(0).Origin.IsEqualApprox(p1), "SpawnOf(0) follows the moved marker");
			Assert.True(s.SpawnOf(1).Origin.IsEqualApprox(FightScene.DefaultSpawn[1]), "missing marker: DefaultSpawn");
			CleanBodies(s);
		}
		finally { s.QueueFree(); }
	}

	[Test]
	public static void MissingContainers_AreCreated_StageStillFirst(Node runner)
	{
		var (s, _) = Make(runner, beforeAdd: f =>
		{
			foreach (string n in new[] { FightScene.StageContainerName, FightScene.FightersName, FightScene.P2SpawnName })
			{ var c = f.GetNode(n); f.RemoveChild(c); c.Free(); }
		});
		try
		{
			Assert.Equal(0, s.GetNode(FightScene.StageContainerName).GetIndex(), "created container sits first");
			CleanBodies(s);
			Assert.True(s.GetNode<Node3D>("Fighters/P2").Position.IsEqualApprox(FightScene.DefaultSpawn[1]), "P2 default");
		}
		finally { s.QueueFree(); }
	}

	[Test]
	public static void RestartAndReplaceMatch_DoNotDuplicateStageOrBodies(Node runner)
	{
		var s = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		s.ExternalDrive = true;
		runner.AddChild(s);
		try
		{
			var stage = s.StageRoot; var p1 = s.ModelOf(0); var p2 = s.ModelOf(1);
			for (int i = 0; i < 3; i++) { s.Step(FighterInput.None, FighterInput.None); s.RestartRun(); s.ResetFight(); }
			CleanBodies(s);
			Assert.True(ReferenceEquals(stage, s.StageRoot) && ReferenceEquals(p1, s.ModelOf(0)) && ReferenceEquals(p2, s.ModelOf(1)), "same stage and bodies kept");
			Assert.Equal(0, s.Match.Tick, "restart gave a fresh match");
		}
		finally { s.QueueFree(); }
	}

	[Test]
	public static void StateHash_MatchesAMatchBuiltWithoutAScene(Node runner)
	{
		foreach (string opp in new[] { "kitsune", "oni", "kappa" })
		{
			var (s, _) = Make(runner, opp);
			var pool = FightScene.LoadAbilityPool();
			var jump = DevJump.BuildFight(new DevFightSetup(opp), pool);
			jump.Session.Enter(jump.NodeId);
			FightScene.Opponent = jump.Session.Node!.Fighter;
			var plain = FightScene.NewMatch(jump.Session.Run, jump.Session);
			try
			{
				var rng = new SimRng(9001); var rng2 = new SimRng(9001);
				for (int i = 0; i < 400; i++)
				{
					var a = new FighterInput((InputBits)(rng.NextUInt() & 0x3ff)); var b = new FighterInput((InputBits)(rng.NextUInt() & 0x3ff));
					s.Step(a, b);
					plain.Step(new FighterInput((InputBits)(rng2.NextUInt() & 0x3ff)), new FighterInput((InputBits)(rng2.NextUInt() & 0x3ff)));
					Assert.Equal(plain.StateHash(), s.Match.StateHash(), $"{opp} tick {i}: hash");
				}
			}
			finally { s.QueueFree(); }
		}
	}

	[Test]
	public static void RunningScene_NeverHasPreview_AndPackedSaveHasNone(Node runner)
	{
		var (s, _) = Make(runner);
		try
		{
			Assert.True(s.GetNodeOrNull(FightEditorPreview.NodeName) is null, "preview node freed first");
			int count = 0;
			void Walk(Node n) { if (n.Name.ToString().StartsWith("EditorPreview")) count++; foreach (var c in n.GetChildren()) Walk(c); }
			Walk(s);
			Assert.Equal(0, count, "no EditorPreview* in the running tree");
			var packed = new PackedScene();
			// the runtime children are unowned: packing the running tree must not carry the stage or the bodies
			Assert.Equal(Error.Ok, packed.Pack(s), "pack");
			var st = packed.GetState();
			for (int i = 0; i < st.GetNodeCount(); i++)
			{
				string p = st.GetNodePath(i).ToString();
				Assert.True(!p.Contains("EditorPreview") && !p.Contains("StageContainer/") && !p.Contains("Fighters/"), $"packed save holds {p}");
			}
		}
		finally { s.QueueFree(); }
	}

	[Test]
	public static void PreviewBuiltTwice_NoDuplicates_ClearRemovesAll_AndKeepsRealNodes(Node runner)
	{
		var fight = new Node3D { Name = "Fight" };
		var sc = new Node3D { Name = FightScene.StageContainerName }; var fi = new Node3D { Name = FightScene.FightersName };
		fight.AddChild(sc); fight.AddChild(fi);
		var real = new Node3D { Name = "RealStage" }; sc.AddChild(real);
		var realBody = new Node3D { Name = "P1" }; fi.AddChild(realBody);
		var pv = new FightEditorPreview { Name = FightEditorPreview.NodeName };
		fight.AddChild(pv);
		runner.AddChild(fight);
		try
		{
			pv.BuildPreview(); pv.BuildPreview();
			Assert.Equal(2, sc.GetChildCount(), "one real + one preview stage after two builds");
			Assert.Equal(3, fi.GetChildCount(), "one real + two preview bodies after two builds");
			// QueueFree'd leftovers: names must not have been auto-renamed with @
			Assert.True(fi.GetNodeOrNull(FightEditorPreview.P1Name) is not null && fi.GetNodeOrNull(FightEditorPreview.P2Name) is not null, "preview bodies keep their names (no @ renames)");
			pv.ClearPreview();
			Assert.Equal(1, sc.GetChildCount(), "clear leaves the real stage");
			Assert.Equal(1, fi.GetChildCount(), "clear leaves the real body");
			pv.ClearPreview(); // idempotent
			Assert.Equal(2, sc.GetChildCount() + fi.GetChildCount(), "second clear harmless");
		}
		finally { fight.QueueFree(); }
	}
}
