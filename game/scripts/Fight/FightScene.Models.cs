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

	private void BuildModels()
	{
		for (int i = 0; i < 2; i++)
		{
			var set = FighterAnimSet.For(i == 0 ? Kit.Ryo : Opponent);
			_presenters[i] = new FighterPresenter(set, Catalog, MoveClips);
			var model = FighterModel.Create(set, Catalog);
			if (model is null) { GD.PushWarning($"YOK-53: no model for {set.Fighter}, keeping the capsule"); continue; }
			AddChild(model);
			_models[i] = model;
		}
		foreach (string p in Catalog.Problems) GD.PushWarning("YOK-53 clip: " + p);
		ObservePresentation();
	}

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
