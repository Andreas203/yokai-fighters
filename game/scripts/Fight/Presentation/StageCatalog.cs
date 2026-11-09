using System.Collections.Generic;
using System.Linq;

namespace YokaiFighters.Fight;

/// <summary>
/// YOK-150/151/152: one generated stage's scene, kind and the asset files it cannot be built without. <see cref="Dir"/> is
/// settable so tests can point it at a missing folder and exercise the fallback (as <c>FightScene.StageDir</c> does for
/// the grove).
/// </summary>
public sealed class StageAssets(string scene, string kind, string defaultDir, params string[] required)
{
	public string Scene { get; } = scene;
	public string Kind { get; } = kind;
	public string DefaultDir { get; } = defaultDir;
	/// <summary>The files (under <see cref="Dir"/>) the scene references; optional extras (the tea house's season paintings) are not listed.</summary>
	public IReadOnlyList<string> Required { get; } = required;
	public string Dir { get; set; } = defaultDir;

	/// <summary>Every required file exists under <see cref="Dir"/>.</summary>
	public bool Present() => StageRules.FilesPresent(Dir, Required.ToArray());
}

/// <summary>
/// YOK-150/151/152: the stages beyond the grove (which keeps <see cref="BambooGroveStage"/> and <c>FightScene.StageDir</c>).
/// <c>FightScene.StageSceneFor</c> asks <see cref="AssetsPresent"/> before building one; a stage with assets missing falls
/// back to the grove, then to the placeholder.
/// </summary>
public static class StageCatalog
{
	public const string ShrineGateScene = "res://scenes/stage/shrine_gate.tscn";
	public const string RiverbankScene = "res://scenes/stage/riverbank.tscn";
	public const string TeaHouseScene = "res://scenes/stage/tea_house.tscn";

	/// <summary>YOK-150: the Oni's snowbound shrine gate at cold dusk (V9, S5).</summary>
	public static readonly StageAssets ShrineGate = new(ShrineGateScene, "shrine-gate", "res://assets/generated/stage/shrine-gate/",
		"backdrop.png", "pine-cluster.glb", "snow-lantern.glb");
	/// <summary>YOK-151: the Kappa's riverbank at night, a still swamp without lanterns (V9; designer 2026-11-09).</summary>
	public static readonly StageAssets Riverbank = new(RiverbankScene, "riverbank", "res://assets/generated/stage/riverbank/",
		"backdrop.png", "reeds.glb", "mooring-post.glb");
	/// <summary>YOK-152: the Tanuki's tea house, lamplit (V9); its season paintings are optional (<see cref="SeasonScreens"/>).</summary>
	public static readonly StageAssets TeaHouse = new(TeaHouseScene, TeaHouseStage.Kind, TeaHouseStage.DefaultDir,
		"backdrop.png", "panel-mask.png", "tea-table.glb", "hanging-lantern.glb");

	public static readonly IReadOnlyList<StageAssets> All = new[] { ShrineGate, Riverbank, TeaHouse };

	/// <summary>The catalog entry for a stage scene path; null for the grove, the placeholder or an unknown scene.</summary>
	public static StageAssets? For(string scene) => All.FirstOrDefault(s => s.Scene == scene);

	/// <summary>The scene's assets are all present; true for a scene the catalog does not know (nothing to check).</summary>
	public static bool AssetsPresent(string scene) => For(scene)?.Present() ?? true;
}
