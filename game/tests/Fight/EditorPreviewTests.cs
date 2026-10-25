using System.Linq;
using Godot;
using YokaiFighters.Fight;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-144 follow-up: fight.tscn's editor-only preview (<see cref="FightEditorPreview"/>) shows the grove and Ryo vs the
/// Kitsune in the editor, is never saved into the scene and never exists, or builds, at runtime.
/// </summary>
public static class EditorPreviewTests
{
	static bool IsPreview(Node n) => n.Name.ToString().StartsWith(FightEditorPreview.NodeName);

	static int CountPreview(Node root) =>
		(IsPreview(root) ? 1 : 0) + root.GetChildren().Sum(c => CountPreview(c));

	[Test]
	public static void SavedScene_HasThePreviewNodeOnly_AndNoPreviewChildren()
	{
		var state = GD.Load<PackedScene>("res://scenes/fight.tscn").GetState();
		int previewNodes = 0;
		for (int i = 0; i < state.GetNodeCount(); i++)
		{
			string path = state.GetNodePath(i).ToString();
			string name = state.GetNodeName(i).ToString();
			Assert.True(!path.Contains(FightScene.StageContainerName + "/") && !path.Contains(FightScene.FightersName + "/"), $"nothing saved under StageContainer or Fighters ({path})");
			if (name.StartsWith(FightEditorPreview.NodeName))
			{
				previewNodes++;
				Assert.Equal(FightEditorPreview.NodeName, name, "the only saved preview node is the tool node itself");
			}
		}
		Assert.Equal(1, previewNodes, "fight.tscn holds the EditorPreview tool node");
	}

	[Test]
	public static void RunningFightScene_HasNoPreviewNodes_AndBuiltNone(Node runner)
	{
		int built = FightEditorPreview.BuildCount;
		var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		scene.ExternalDrive = true;
		runner.AddChild(scene);
		try
		{
			Assert.Equal(0, CountPreview(scene), "no EditorPreview* node at runtime");
			Assert.Equal(built, FightEditorPreview.BuildCount, "the preview code path did not run");
			Assert.Equal(1, scene.GetNode(FightScene.StageContainerName).GetChildCount(), "only the real stage");
			Assert.Equal(2, scene.GetNode(FightScene.FightersName).GetChildCount(), "only the two real bodies");
		}
		finally { scene.QueueFree(); }
	}

	static Node3D FakeFight()
	{
		var fight = new Node3D { Name = "Fight" };
		fight.AddChild(new Node3D { Name = FightScene.StageContainerName });
		fight.AddChild(new Node3D { Name = FightScene.FightersName });
		fight.AddChild(new Marker3D { Name = FightScene.P1SpawnName, Position = FightScene.DefaultSpawn[0] });
		fight.AddChild(new Marker3D { Name = FightScene.P2SpawnName, Position = FightScene.DefaultSpawn[1] });
		return fight;
	}

	[Test]
	public static void PreviewNode_OutsideTheEditor_BuildsNothing(Node runner)
	{
		Assert.True(!Engine.IsEditorHint(), "tests run outside the editor");
		int built = FightEditorPreview.BuildCount;
		var fight = FakeFight();
		fight.AddChild(new FightEditorPreview { Name = FightEditorPreview.NodeName });
		runner.AddChild(fight);
		Assert.Equal(built, FightEditorPreview.BuildCount, "_Ready outside the editor builds no preview");
		Assert.Equal(0, fight.GetNode(FightScene.StageContainerName).GetChildCount() + fight.GetNode(FightScene.FightersName).GetChildCount(), "nothing added");
		fight.QueueFree();
	}

	[Test]
	public static void EditorPath_ShowsGroveAndBothFightersFacingEachOther_Unowned(Node runner)
	{
		Assert.Equal(FightScene.GroveScene, FightEditorPreview.StageScene, "preview stage = the default stage");
		Assert.Equal(FighterAnimSet.Ryo.ScenePath, FightEditorPreview.P1Scene, "preview P1 = Ryo");
		Assert.Equal(FighterAnimSet.Kitsune.ScenePath, FightEditorPreview.P2Scene, "preview P2 = the Kitsune");
		var fight = FakeFight();
		var preview = new FightEditorPreview { Name = FightEditorPreview.NodeName };
		fight.AddChild(preview);
		runner.AddChild(fight);
		try
		{
			preview.BuildPreview(); // what _Ready does in the editor
			var stage = fight.GetNode<Node3D>($"{FightScene.StageContainerName}/{FightEditorPreview.StageName}");
			Assert.Equal(FightScene.GroveScene, stage.SceneFilePath, "the grove in StageContainer");
			Assert.True(stage.Owner is null, "preview stage has no owner (never saved)");
			foreach (var (name, i, facing) in new[] { (FightEditorPreview.P1Name, 0, 1f), (FightEditorPreview.P2Name, 1, -1f) })
			{
				var body = fight.GetNode<Node3D>($"{FightScene.FightersName}/{name}");
				Assert.True(body.Owner is null, $"{name} has no owner (never saved)");
				Assert.True(body.Position.IsEqualApprox(FightScene.DefaultSpawn[i]), $"{name} at its marker ({body.Position})");
				var mirror = (Node3D)body.FindChild(FighterModel.MirrorName, true, false)!;
				Assert.True(Mathf.IsEqualApprox(mirror.Scale.X, facing), $"{name} faces {facing}");
			}
			preview.ClearPreview();
			Assert.True(fight.GetNodeOrNull($"{FightScene.FightersName}/{FightEditorPreview.P1Name}") is null
				&& fight.GetNodeOrNull($"{FightScene.StageContainerName}/{FightEditorPreview.StageName}") is null, "ClearPreview removes them");
		}
		finally { fight.QueueFree(); }
	}
}
