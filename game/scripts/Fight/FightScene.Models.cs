using System.Collections.Generic;
using Godot;
using YokaiFighters.Sim;

namespace YokaiFighters.Fight;

/// <summary>
/// YOK-53 partial: the rigged fighters. Each sim tick <see cref="ObservePresentation"/> maps every fighter's sim
/// state to a clip frame (<see cref="FighterPresenter"/>); each render poses the models with AnimationPlayer.Seek.
/// The capsules stay as the fallback (models missing) and a debug view (F10). Presentation never writes the sim.
/// </summary>
public partial class FightScene
{
	/// <summary>Show rigged models (true) or the placeholder capsules; F10 flips it in debug builds.</summary>
	public static bool ShowModels { get; set; } = true;
	public const Key ModelToggleKey = Key.F10;

	private static ClipCatalog? _catalog;
	private static Dictionary<string, string>? _moveClips;
	public static ClipCatalog Catalog => _catalog ??= ClipCatalog.Load(ContentPaths.Data("clips"), r => ResourceLoader.Exists("res://" + r)); // YOK-39: GLBs live in the PCK when exported
	public static IReadOnlyDictionary<string, string> MoveClips => _moveClips ??= ClipCatalog.LoadMoveClips(ContentPaths.Data("moves"));

	private readonly FighterModel?[] _models = new FighterModel?[2];
	private readonly FighterPresenter?[] _presenters = new FighterPresenter?[2];
	private Match? _presentedMatch;
	private bool _modelKeyHeld;

	public FighterModel? ModelOf(int i) => _models[i];
	public FighterPresenter? PresenterOf(int i) => _presenters[i];

	/// <summary>
	/// YOK-133: the camera config framed for the tallest body in this fight (<see cref="CameraConfig.ForBodies"/>):
	/// <see cref="CameraConfig.Default"/> itself for Ryo vs the Kitsune, a higher model top when the Oni's club is in the fight.
	/// </summary>
	public CameraConfig CameraFraming { get; private set; } = CameraConfig.Default;

	/// <summary>YOK-143: fight.tscn's Fighters container and its spawn markers (siblings of the container).</summary>
	public const string FightersName = "Fighters", P1SpawnName = "P1Spawn", P2SpawnName = "P2Spawn";

	/// <summary>YOK-143: where a fight.tscn without its markers places each body (the markers' own values).</summary>
	public static readonly Vector3[] DefaultSpawn = { new(-1.5f, 0f, 0f), new(1.5f, 0f, 0f) };

	/// <summary>
	/// YOK-143: fight.tscn is a template. It holds an empty Fighters container and the P1Spawn / P2Spawn markers;
	/// both bodies (<see cref="FighterAnimSet.ScenePath"/>) are instanced here, Ryo for P1 and the node's yokai for P2
	/// (<see cref="FighterAnimSet.ForNode"/>), as Fighters/P1 and Fighters/P2 at their marker's transform. From the
	/// first render the sim places them (<see cref="RenderModels"/>), so the markers only set the pre-render pose.
	/// </summary>
	private void BuildModels()
	{
		var slots = GetNodeOrNull<Node3D>(FightersName);
		if (slots is null) { slots = new Node3D { Name = FightersName }; AddChild(slots); }
		var bodies = new string[2];
		for (int i = 0; i < 2; i++)
		{
			var set = i == 0 ? FighterAnimSet.For(Kit.Ryo) : FighterAnimSet.ForNode(Opponent, Node?.Yokai); // YOK-131: P2 shows the node's yokai body (presentation only)
			bodies[i] = set.Fighter;
			_presenters[i] = new FighterPresenter(set, Catalog, MoveClips);
			string slot = i == 0 ? "P1" : "P2";
			if (slots.GetNodeOrNull(slot) is { } stale) { slots.RemoveChild(stale); stale.QueueFree(); }
			var model = FighterModel.Create(set, Catalog);
			if (model is null) { GD.PushWarning($"YOK-53: no model for {set.Fighter}, keeping the capsule"); continue; }
			model.Name = slot;
			model.Transform = SpawnOf(i);
			slots.AddChild(model);
			_models[i] = model;
		}
		CameraFraming = CameraConfig.Default.ForBodies(bodies[0], bodies[1]);
		foreach (string p in Catalog.Problems) GD.PushWarning("YOK-53 clip: " + p);
		ObservePresentation();
	}

	/// <summary>YOK-143: player <paramref name="i"/>'s spawn marker transform, or <see cref="DefaultSpawn"/> without one.</summary>
	public Transform3D SpawnOf(int i) =>
		GetNodeOrNull<Marker3D>(i == 0 ? P1SpawnName : P2SpawnName)?.Transform ?? new Transform3D(Basis.Identity, DefaultSpawn[i]);

	/// <summary>Hook in StepSim: after every sim tick (so blends and loops count world frames, not renders).</summary>
	private void ObservePresentation()
	{
		var m = Match; // YOK-48: the match may be replaced (rematch); never cache fighters across ticks
		if (!ReferenceEquals(m, _presentedMatch))
		{
			foreach (var p in _presenters) p?.Reset();
			_presentedMatch = m;
		}
		for (int i = 0; i < 2; i++) _presenters[i]?.Observe(m, i);
	}

	/// <summary>Hook at the end of Render's fighter loop.</summary>
	private void RenderModels()
	{
		if (!ReferenceEquals(Match, _presentedMatch)) ObservePresentation();
		for (int i = 0; i < 2; i++)
		{
			var model = _models[i];
			bool show = ShowModels && model != null;
			_bodies[i].Visible = !show;
			if (model is null || _presenters[i] is null) continue;
			model.Visible = show;
			Fighter f = Match.Fighters[i];
			model.Position = new Vector3(ToMeters(f.X), ToMeters(f.Y), 0f);
			model.Apply(_presenters[i]!.Sample, Catalog, f.Facing, Match.WorldFrame);
		}
	}

	private void PollModelKey()
	{
		if (!OS.IsDebugBuild()) return;
		bool down = Input.IsPhysicalKeyPressed(ModelToggleKey);
		if (down && !_modelKeyHeld) ShowModels = !ShowModels;
		_modelKeyHeld = down;
	}
}
