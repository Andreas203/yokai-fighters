using System.Linq;
using Godot;
using YokaiFighters.Sim;

namespace YokaiFighters.Fight;

/// <summary>
/// YOK-39 partial: the stage. <see cref="BuildStage"/> (called once from <c>_Ready</c>) adds one "Stage" node holding
/// either the bamboo grove at dusk (V9, <see cref="BambooGroveStage"/>) or, when its assets fail to load or
/// <see cref="UsePlaceholderStage"/> is set (debug, user arg <c>--stage=placeholder</c>), the plain placeholder.
/// Purely visual: nothing here reads or writes the sim, and every prop sits behind the Z = 0 gameplay plane (F4).
/// </summary>
public partial class FightScene
{
	/// <summary>Folder the grove assets load from; tests point it at a missing folder to exercise the fallback.</summary>
	public static string StageDir { get; set; } = BambooGroveStage.DefaultDir;

	/// <summary>Debug: build the plain placeholder stage instead of the grove.</summary>
	public static bool UsePlaceholderStage { get; set; } = OS.GetCmdlineUserArgs().Contains("--stage=placeholder");

	/// <summary>The "Stage" node (all scenery, lights and environment).</summary>
	public Node3D StageRoot { get; private set; } = null!;

	/// <summary>"bamboo-grove" or "placeholder".</summary>
	public string StageKind { get; private set; } = "";

	private void BuildStage()
	{
		StageRoot = new Node3D { Name = "Stage" };
		AddChild(StageRoot);
		if (!UsePlaceholderStage && BambooGroveStage.Build(StageRoot, Match.Config, StageDir))
		{
			StageKind = BambooGroveStage.Kind;
			return;
		}
		if (!UsePlaceholderStage) GD.PushWarning($"YOK-39: bamboo grove assets missing under {StageDir}; using the placeholder stage");
		foreach (Node n in StageRoot.GetChildren()) { StageRoot.RemoveChild(n); n.QueueFree(); }
		BuildPlaceholderStage(StageRoot, Match.Config);
		StageKind = "placeholder";
	}

	/// <summary>The pre-YOK-39 placeholder: flat floor, plain backdrop, corner posts.</summary>
	public static void BuildPlaceholderStage(Node3D root, SimConfig c)
	{
		float stageW = ToMeters(c.StageHalfWidth * 2);
		root.AddChild(new WorldEnvironment
		{
			Environment = new Environment
			{
				BackgroundMode = Environment.BGMode.Color,
				BackgroundColor = new Color(0.32f, 0.22f, 0.3f),
				AmbientLightSource = Environment.AmbientSource.Color,
				AmbientLightColor = new Color(0.5f, 0.45f, 0.5f),
			},
		});
		root.AddChild(new DirectionalLight3D { Name = "Sun", RotationDegrees = new Vector3(-50f, -30f, 0f) });
		root.AddChild(new MeshInstance3D
		{
			Name = "Floor",
			Mesh = new BoxMesh { Size = new Vector3(stageW + 8f, 0.2f, 6f), Material = new StandardMaterial3D { AlbedoColor = new Color(0.3f, 0.36f, 0.22f) } },
			Position = new Vector3(0f, -0.1f, 0f),
		});
		root.AddChild(new MeshInstance3D
		{
			Name = "Backdrop",
			Mesh = new QuadMesh { Size = new Vector2(stageW + 12f, 8f), Material = new StandardMaterial3D { AlbedoColor = new Color(0.45f, 0.3f, 0.35f), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded } },
			Position = new Vector3(0f, 4f, -4f),
		});
		foreach (int side in new[] { -1, 1 })
		{
			root.AddChild(new MeshInstance3D
			{
				Name = side < 0 ? "CornerLeft" : "CornerRight",
				Mesh = new BoxMesh { Size = new Vector3(0.2f, 5f, 0.2f), Material = new StandardMaterial3D { AlbedoColor = new Color(0.4f, 0.55f, 0.3f) } },
				Position = new Vector3(side * stageW / 2f, 2.5f, -0.5f),
			});
		}
	}

	/// <summary>Camera distance from the Z = 0 plane so <see cref="SimConfig.ViewWidth"/> fills a 16:9 screen at FOV 25 (F4).</summary>
	public static float CameraDistance(SimConfig c)
	{
		float halfFovTan = Mathf.Tan(Mathf.DegToRad(CameraFovDegrees) / 2f);
		return ToMeters(c.ViewWidth) / (2f * halfFovTan * (16f / 9f));
	}
}
