using System.Linq;
using Godot;
using YokaiFighters.Sim;

namespace YokaiFighters.Fight;

/// <summary>
/// YOK-39 partial: the stage. YOK-144: fight.tscn holds an empty <see cref="StageContainerName"/>; <see cref="BuildStage"/>
/// (called once from <c>_Ready</c>) instances the node's stage there as "Stage": the node-scene table's <c>stage</c>
/// (<see cref="NodeContext.Stage"/>), else <see cref="GroveScene"/> (V9, the bamboo grove at dusk). The grove falls back to
/// <see cref="PlaceholderScene"/> when its assets are missing, and <see cref="UsePlaceholderStage"/> (debug, user arg
/// <c>--stage=placeholder</c>) forces the placeholder. Purely visual: nothing here reads or writes the sim, and every prop
/// sits behind the Z = 0 gameplay plane (F4; checked by StageTests, not by code placement).
/// </summary>
public partial class FightScene
{
	public const string GroveScene = "res://scenes/stage/bamboo_grove.tscn", PlaceholderScene = "res://scenes/stage/placeholder.tscn";

	/// <summary>YOK-144: fight.tscn's stage container and the name of the stage instanced in it.</summary>
	public const string StageContainerName = "StageContainer", StageName = "Stage";

	/// <summary>Folder the grove's assets are checked in; tests point it at a missing folder to exercise the fallback.</summary>
	public static string StageDir { get; set; } = BambooGroveStage.DefaultDir;

	/// <summary>Debug: show the plain placeholder stage instead of the grove.</summary>
	public static bool UsePlaceholderStage { get; set; } = OS.GetCmdlineUserArgs().Contains("--stage=placeholder");

	/// <summary>The stage node, StageContainer/Stage (all scenery, lights and environment).</summary>
	public Node3D StageRoot { get; private set; } = null!;

	/// <summary>"bamboo-grove" or "placeholder" (the stage scene's <see cref="FightStage.Kind"/>).</summary>
	public string StageKind { get; private set; } = "";

	/// <summary>
	/// YOK-144 (pure): the stage scene to instance. <paramref name="nodeStage"/> (the node data's choice) or the grove;
	/// a chosen scene that does not exist falls back to the grove; the grove with its assets missing, or the debug
	/// flag, gives the placeholder. <paramref name="warn"/> gets each substitution.
	/// </summary>
	public static string StageSceneFor(string? nodeStage, bool placeholderFlag, bool groveAssets, System.Func<string, bool> exists, System.Action<string>? warn = null)
	{
		if (placeholderFlag) return PlaceholderScene;
		string chosen = nodeStage ?? GroveScene;
		if (chosen != GroveScene && !exists(chosen)) { warn?.Invoke($"YOK-144: stage {chosen} not found; using the bamboo grove"); chosen = GroveScene; }
		if (chosen == GroveScene && !groveAssets) { warn?.Invoke($"YOK-39: bamboo grove assets missing under {StageDir}; using the placeholder stage"); return PlaceholderScene; }
		return chosen;
	}

	private void BuildStage()
	{
		var container = GetNodeOrNull<Node3D>(StageContainerName);
		if (container is null) { container = new Node3D { Name = StageContainerName }; AddChild(container); MoveChild(container, 0); }
		string want = StageSceneFor(Node?.Stage, UsePlaceholderStage, BambooGroveStage.AssetsPresent(StageDir), p => ResourceLoader.Exists(p), w => GD.PushWarning(w));
		var made = GD.Load<PackedScene>(want)?.Instantiate();
		if (made is not FightStage stage)
		{
			made?.Free();
			GD.PushWarning($"YOK-144: {want} is not a FightStage scene; using the placeholder stage");
			stage = GD.Load<PackedScene>(PlaceholderScene).Instantiate<FightStage>();
		}
		foreach (Node old in container.GetChildren()) { container.RemoveChild(old); old.QueueFree(); }
		stage.Name = StageName;
		container.AddChild(stage);
		StageRoot = stage;
		StageKind = stage.Kind;
	}

	/// <summary>Camera distance from the Z = 0 plane at the widest zoom: <see cref="SimConfig.ViewWidth"/> fills a 16:9 screen at FOV 25 (F4). YOK-129: the dynamic camera is at or closer than this.</summary>
	public static float CameraDistance(SimConfig c)
	{
		float halfFovTan = Mathf.Tan(Mathf.DegToRad(CameraFovDegrees) / 2f);
		return ToMeters(c.ViewWidth) / (2f * halfFovTan * (16f / 9f));
	}
}
