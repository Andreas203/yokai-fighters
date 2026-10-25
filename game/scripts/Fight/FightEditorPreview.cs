using Godot;

namespace YokaiFighters.Fight;

/// <summary>
/// YOK-144 follow-up: an editor-only preview for fight.tscn, so the template does not open empty. A [Tool] node
/// (<see cref="NodeName"/>) in fight.tscn: in the editor it adds the bamboo grove to StageContainer and Ryo / the
/// Kitsune at P1Spawn / P2Spawn (facing each other), all named <c>EditorPreview*</c> and with no owner, so they are
/// never saved into the .tscn. Outside the editor it builds nothing, and <see cref="FightScene"/> frees this node in
/// its <c>_Ready</c>, before it builds the real stage and bodies. Presentation only: no sim, hash or replay effect.
/// </summary>
[Tool]
public partial class FightEditorPreview : Node
{
	public const string NodeName = "EditorPreview", StageName = "EditorPreviewStage", P1Name = "EditorPreviewP1", P2Name = "EditorPreviewP2";

	/// <summary>The preview's scenes; tests check they match <see cref="FightScene.GroveScene"/> and <see cref="FighterAnimSet"/>.</summary>
	public const string StageScene = "res://scenes/stage/bamboo_grove.tscn", P2Scene = "res://scenes/fighters/kitsune.tscn";
	/// <summary>YOK-146: Ryo's body follows <see cref="FighterAnimSet.TripoRyo"/> (Tripo or Meshy).</summary>
	public static string P1Scene => FighterAnimSet.Ryo.ScenePath;

	/// <summary>How many times a preview was built in this process (tests: stays 0 outside the editor).</summary>
	public static int BuildCount { get; private set; }

	public override void _Ready()
	{
		if (!Engine.IsEditorHint()) return; // never at runtime
		BuildPreview(); // StageContainer, Fighters and the markers come before this node in fight.tscn, so they are ready
	}

	public override void _ExitTree()
	{
		if (Engine.IsEditorHint()) ClearPreview();
	}

	/// <summary>Adds the preview nodes (unowned) next to the fight's containers. Public for tests; the game never calls it.</summary>
	public void BuildPreview()
	{
		if (GetParent() is not Node3D fight) return;
		ClearPreview();
		BuildCount++;
		if (fight.GetNodeOrNull<Node3D>(FightScene.StageContainerName) is { } stages && Load(StageScene) is { } stage)
		{
			stage.Name = StageName;
			stages.AddChild(stage); // no owner: not saved
		}
		if (fight.GetNodeOrNull<Node3D>(FightScene.FightersName) is not { } fighters) return;
		foreach (var (scene, name, marker, facing) in new[] { (P1Scene, P1Name, FightScene.P1SpawnName, 1f), (P2Scene, P2Name, FightScene.P2SpawnName, -1f) })
		{
			if (Load(scene) is not { } body) continue;
			body.Name = name;
			body.Transform = fight.GetNodeOrNull<Marker3D>(marker)?.Transform ?? Transform3D.Identity;
			if (body.FindChild(FighterModel.MirrorName, true, false) is Node3D mirror) mirror.Scale = new Vector3(facing, 1f, 1f); // Mirror.Scale.X = facing, as at runtime
			fighters.AddChild(body); // no owner: not saved
		}	}

	/// <summary>Removes any preview nodes this node added.</summary>
	public void ClearPreview()
	{
		if (GetParent() is not Node fight) return;
		foreach (string path in new[] { $"{FightScene.StageContainerName}/{StageName}", $"{FightScene.FightersName}/{P1Name}", $"{FightScene.FightersName}/{P2Name}" })
			if (fight.GetNodeOrNull(path) is { } n) { n.GetParent().RemoveChild(n); n.QueueFree(); }
	}

	static Node3D? Load(string path) => ResourceLoader.Exists(path) ? GD.Load<PackedScene>(path).Instantiate() as Node3D : null;
}
