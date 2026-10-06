using System.Linq;
using Godot;
using YokaiFighters.Sim;

namespace YokaiFighters.Fight;

/// <summary>
/// YOK-39 partial: the stage. fight.tscn instances <see cref="GroveScene"/> (V9, the bamboo grove at dusk) as its
/// "Stage" child, edited in Godot like any scene. <see cref="BuildStage"/> (called once from <c>_Ready</c>) swaps it
/// for <see cref="PlaceholderScene"/> when the grove assets are missing or <see cref="UsePlaceholderStage"/> is set
/// (debug, user arg <c>--stage=placeholder</c>). Purely visual: nothing here reads or writes the sim, and every prop
/// sits behind the Z = 0 gameplay plane (F4; checked by StageTests, not by code placement).
/// </summary>
public partial class FightScene
{
	public const string GroveScene = "res://scenes/stage/bamboo_grove.tscn", PlaceholderScene = "res://scenes/stage/placeholder.tscn";

	/// <summary>Folder the grove's assets are checked in; tests point it at a missing folder to exercise the fallback.</summary>
	public static string StageDir { get; set; } = BambooGroveStage.DefaultDir;

	/// <summary>Debug: show the plain placeholder stage instead of the grove.</summary>
	public static bool UsePlaceholderStage { get; set; } = OS.GetCmdlineUserArgs().Contains("--stage=placeholder");

	/// <summary>The "Stage" node (all scenery, lights and environment).</summary>
	public Node3D StageRoot { get; private set; } = null!;

	/// <summary>"bamboo-grove" or "placeholder" (the stage scene's <see cref="FightStage.Kind"/>).</summary>
	public string StageKind { get; private set; } = "";

	private void BuildStage()
	{
		var stage = GetNodeOrNull<FightStage>("Stage");
		bool grove = !UsePlaceholderStage && BambooGroveStage.AssetsPresent(StageDir);
		if (!grove && !UsePlaceholderStage) GD.PushWarning($"YOK-39: bamboo grove assets missing under {StageDir}; using the placeholder stage");
		string want = grove ? GroveScene : PlaceholderScene;
		if (stage is null || stage.SceneFilePath != want)
		{
			if (stage != null) { RemoveChild(stage); stage.QueueFree(); }
			stage = GD.Load<PackedScene>(want).Instantiate<FightStage>();
			stage.Name = "Stage";
			AddChild(stage);
			MoveChild(stage, 0);
		}
		StageRoot = stage;
		StageKind = stage.Kind;
	}

	/// <summary>Camera distance from the Z = 0 plane so <see cref="SimConfig.ViewWidth"/> fills a 16:9 screen at FOV 25 (F4).</summary>
	public static float CameraDistance(SimConfig c)
	{
		float halfFovTan = Mathf.Tan(Mathf.DegToRad(CameraFovDegrees) / 2f);
		return ToMeters(c.ViewWidth) / (2f * halfFovTan * (16f / 9f));
	}
}
