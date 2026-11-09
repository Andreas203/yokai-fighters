using System.Linq;
using Godot;
using YokaiFighters.Sim;

namespace YokaiFighters.Fight;

/// <summary>
/// YOK-39 partial: the stage. YOK-144: fight.tscn holds an empty <see cref="StageContainerName"/>; <see cref="BuildStage"/>
/// (called once from <c>_Ready</c>) instances the node's stage there as "Stage": the node-scene table's <c>stage</c>
/// (<see cref="NodeContext.Stage"/>), else <see cref="GroveScene"/> (V9, the bamboo grove at dusk). The grove falls back to
/// <see cref="PlaceholderScene"/> when its assets are missing, and <see cref="UsePlaceholderStage"/> (debug, user arg
/// <c>--stage=placeholder</c>) forces the placeholder. YOK-150: the shrine gate, riverbank and tea house
/// (<see cref="StageCatalog"/>) fall back to the grove when their assets are missing; each render hands the stage its
/// <see cref="StageClock"/> (<see cref="PresentStage"/>, every sim tick and every render: the round starts seen, <c>Match.Tick</c>
/// and the opponent's health, which the tea house's seasons follow).
/// Purely visual: nothing here writes the sim, the sim never reads the stage, and every prop sits behind the Z = 0
/// gameplay plane (F4; checked by StageTests, not by code placement).
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

	/// <summary>
	/// YOK-150 debug: a stage scene shown whatever the node names (user arg <c>--stage=&lt;name&gt;</c> for
	/// <c>res://scenes/stage/&lt;name&gt;.tscn</c>, e.g. <c>--stage=tea_house</c>; the dev menu's stage choice). Null = the
	/// node's own stage. Still falls back like any stage when it is missing or its assets are.
	/// </summary>
	public static string? ForcedStage { get; set; } = ForcedStageArg(OS.GetCmdlineUserArgs());

	/// <summary>The scene a <c>--stage=&lt;name&gt;</c> user arg names (not "placeholder", which has its own flag); null when none or not a plain stage name.</summary>
	public static string? ForcedStageArg(System.Collections.Generic.IEnumerable<string> args)
	{
		foreach (string a in args)
		{
			if (!a.StartsWith("--stage=") || a == "--stage=placeholder") continue;
			string scene = NodeSceneTable.StagePrefix + a["--stage=".Length..] + ".tscn";
			if (System.Text.RegularExpressions.Regex.IsMatch(scene, NodeSceneTable.StagePattern)) return scene;
			GD.PushWarning($"YOK-150: {a} is not a stage name under {NodeSceneTable.StagePrefix}; ignored");
		}
		return null;
	}

	/// <summary>The stage node, StageContainer/Stage (all scenery, lights and environment).</summary>
	public Node3D StageRoot { get; private set; } = null!;

	/// <summary>"bamboo-grove", "shrine-gate", "riverbank", "tea-house" or "placeholder" (the stage scene's <see cref="FightStage.Kind"/>).</summary>
	public string StageKind { get; private set; } = "";

	/// <summary>
	/// YOK-144 (pure): the stage scene to instance. <paramref name="nodeStage"/> (the node data's choice) or the grove;
	/// a chosen scene that does not exist, or (YOK-150) whose assets <paramref name="stageAssets"/> says are missing, falls
	/// back to the grove; the grove with its assets missing, or the debug flag, gives the placeholder. <paramref name="warn"/>
	/// gets each substitution.
	/// </summary>
	public static string StageSceneFor(string? nodeStage, bool placeholderFlag, bool groveAssets, System.Func<string, bool> exists, System.Action<string>? warn = null, System.Func<string, bool>? stageAssets = null)
	{
		if (placeholderFlag) return PlaceholderScene;
		string chosen = nodeStage ?? GroveScene;
		if (chosen != GroveScene && !exists(chosen)) { warn?.Invoke($"YOK-144: stage {chosen} not found; using the bamboo grove"); chosen = GroveScene; }
		else if (chosen != GroveScene && stageAssets is not null && !stageAssets(chosen)) { warn?.Invoke($"YOK-150: stage {chosen} has assets missing; using the bamboo grove"); chosen = GroveScene; }
		if (chosen == GroveScene && !groveAssets) { warn?.Invoke($"YOK-39: bamboo grove assets missing under {StageDir}; using the placeholder stage"); return PlaceholderScene; }
		return chosen;
	}

	private void BuildStage()
	{
		var container = GetNodeOrNull<Node3D>(StageContainerName);
		if (container is null) { container = new Node3D { Name = StageContainerName }; AddChild(container); MoveChild(container, 0); }
		string want = StageSceneFor(ForcedStage ?? Node?.Stage, UsePlaceholderStage, BambooGroveStage.AssetsPresent(StageDir), p => ResourceLoader.Exists(p), w => GD.PushWarning(w), StageCatalog.AssetsPresent);
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

	/// <summary>
	/// YOK-150: round starts this fight scene has seen (1 = the first). A new <c>Match</c> (a new node's fight, a Restart, a
	/// rematch) or a <c>Match.Tick</c> that went back (a reset, a practice reset) starts the next. The stage uses it only to
	/// know a round started over (the tea house goes back to spring); C1 has one round per fight.
	/// </summary>
	public int StageRound { get; private set; }
	private Match? _roundMatch;
	private int _roundTick;

	/// <summary>
	/// YOK-150: counts a round start if one happened, then hands the stage its clock: the round, <c>Match.Tick</c> and the
	/// opponent's (P2's) health. Called after every sim tick (so a health beat is seen on the tick it happens, whatever the
	/// frame rate: same tick, same picture) and from <c>Render</c>; presenting the same tick twice changes nothing.
	/// </summary>
	private void PresentStage()
	{
		if (!ReferenceEquals(_roundMatch, Match) || Match.Tick < _roundTick) { StageRound++; _roundMatch = Match; }
		_roundTick = Match.Tick;
		(StageRoot as FightStage)?.Present(new StageClock(StageRound, Match.Tick, Match.P2.Health, Match.P2.MaxHealth));
	}

	/// <summary>Camera distance from the Z = 0 plane at the widest zoom: <see cref="SimConfig.ViewWidth"/> fills a 16:9 screen at FOV 25 (F4). YOK-129: the dynamic camera is at or closer than this.</summary>
	public static float CameraDistance(SimConfig c)
	{
		float halfFovTan = Mathf.Tan(Mathf.DegToRad(CameraFovDegrees) / 2f);
		return ToMeters(c.ViewWidth) / (2f * halfFovTan * (16f / 9f));
	}
}
