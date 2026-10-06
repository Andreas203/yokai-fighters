using System.Collections.Generic;
using Godot;
using YokaiFighters.Sim;

namespace YokaiFighters.Fight;

/// <summary>
/// Presentation and driver for one fight. Owns a <see cref="Match"/> (pure sim) and a
/// <see cref="FixedStepClock"/>: each rendered frame it runs exactly the sim ticks that are due,
/// then draws the current state. Gameplay never reads Godot delta (F3). 3D is presentation only:
/// fighters sit on a fixed-Z plane (F4) under a ~25 deg perspective camera.
/// Placeholder capsules, stage and health bars until generated assets and the HUD land.
/// Moves load from data/moves/ at fight start (MoveLoader); while it has no normals yet, the labelled
/// TEST FIXTURE normals in tests/fixtures/ryo-normals/ (C8 numbers) stand in so the scene is playable.
/// Controls are the E9 defaults through InputDevices.Read; each fighter's InputReader (Kata) parses
/// them inside the sim. R resets after KO.
/// </summary>
public partial class FightScene : Node3D
{
	/// <summary>200 gameplay units per metre: the 1920-unit screen is 9.6 m wide, a 1.8 m fighter is 360 units.</summary>
	public const float UnitsPerMeter = 200f;
	public const float CameraFovDegrees = 25f; // F4
	public const float CameraHeight = 1.5f;
	public const float BodyHeight = 1.8f;

	/// <summary>When true, the clock and keyboard are bypassed and the owner calls Step() (tests, harness).</summary>
	[Export] public bool ExternalDrive { get; set; }

	public Match Match { get; private set; } = NewMatch();
	private readonly FixedStepClock _clock = new();
	/// <summary>YOK-22 frame-step gate (driven by DebugOverlay, debug builds only).</summary>
	public FrameStepper Stepper { get; } = new();

	private Camera3D _camera = null!;
	private readonly Node3D[] _bodies = new Node3D[2];
	private FightHud _hud = null!;
	private bool _resetHeld;
	/// <summary>V3 (YOK-20): started by Match.Impact, stepped once per sim tick.</summary>
	public ScreenShake Shake { get; } = new(SimConfig.Default.ShakeFrames);

	public const string FixtureNormalsDir = "res://tests/fixtures/ryo-normals";

	public const string FixtureThrowsDir = "res://tests/fixtures/throws";

	public const string FixtureAirNormalsDir = "res://tests/fixtures/ryo-air-normals";

	public const string FixtureSpecialsDir = "res://tests/fixtures/specials";

	/// <summary>
	/// YOK-23 (minimum of YOK-25): the scheme every player starts a fight with. The demo plays on Kihon (K2,
	/// never cut, K4); a title-screen choice can set it before the fight scene loads.
	/// </summary>
	public static ControlScheme DefaultScheme { get; set; } = ControlScheme.Kihon;

	/// <summary>YOK-56: whom P2 (the AI) fights as; its kit loads from data like Ryo's.</summary>
	public static string Opponent { get; set; } = "kitsune";

	/// <summary>
	/// YOK-56: where kits and the draft pool load from. Tests that check fallbacks set
	/// <c>KitSources.FixturesOnly()</c> (and restore it) so they never depend on what is in data/.
	/// </summary>
	public static KitSources Sources { get; set; } = KitSources.At(ContentPaths.DataDir, ContentPaths.FixturesDir);

	public static KitSources FixtureSources => KitSources.At(ContentPaths.DataDir, ContentPaths.FixturesDir).FixturesOnly();

	/// <summary>YOK-44: story cards (habit-writer's data/story/).</summary>
	public static string StoryDir => ContentPaths.Data("story");

	public static Story.StoryLibrary LoadStory() => Story.StoryLibrary.LoadDirectory(StoryDir);

	/// <summary>Fills the HUD lose screen from data/story/lose-screen.json; keeps the placeholder (and warns) if it is missing or bad.</summary>
	private void ApplyLoseCard()
	{
		try
		{
			var (title, text) = LoadStory().LoseScreen(Story.StoryLibrary.DisplayName(Opponent));
			_hud.SetLoseText(title, text);
		}
		catch (System.Exception e) when (e is System.IO.IOException or System.FormatException or System.Text.Json.JsonException
			or System.Collections.Generic.KeyNotFoundException or System.InvalidOperationException)
		{
			GD.PushWarning($"lose screen keeps its placeholder: {e.Message}");
		}
	}

	/// <summary>
	/// YOK-56: P1 = Ryo's kit (his normals, throw and starters, A1), P2 = <see cref="Opponent"/>'s kit (the AI
	/// loadout); each falls back to the TEST FIXTURES per move kind while data/moves/ has none of its own.
	/// </summary>
	public static Match NewMatch() => NewMatch(Run);

	/// <summary>YOK-48: the next fight for <paramref name="run"/> (null = starters at full health).</summary>
	public static Match NewMatch(RunState? run)
	{
		var cfg = run?.MatchConfig();
		var ryo = LoadKit(Kit.Ryo);
		var foe = LoadKit(Opponent);
		var m = new Match(cfg, ryo.Moves, foe.Moves);
		ryo.Equip(m.P1);
		foe.Equip(m.P2);
		foreach (var f in m.Fighters) f.Input.Scheme = DefaultScheme;
		run?.ApplyTo(m.P1, cfg); // YOK-47: Ryo's drafted slots, levels, modifiers and carried health
		return m;
	}

	/// <summary>YOK-56: one fighter's kit from <see cref="Sources"/>; fixture fallbacks are logged.</summary>
	public static Kit LoadKit(string fighter)
	{
		var kit = Kit.Load(fighter, Sources);
		foreach (var n in kit.Notes) GD.Print("FightScene: " + n);
		return kit;
	}

	/// <summary>
	/// YOK-47: the run in progress. When set, the next fight's P1 (Ryo) takes its specials (levels,
	/// modifiers) and carried health (C1, R3); null = a standalone fight with the starters at full health.
	/// </summary>
	public static RunState? Run { get; set; }

	/// <summary>YOK-47: specials + modifiers for the reward draft, from data/ with TEST FIXTURE fallback (YOK-56: starters = Ryo's kit).</summary>
	public static AbilityPool LoadAbilityPool() =>
		AbilityPool.Load(Sources, ContentPaths.Data("modifiers"));

	/// <summary>YOK-23: a player's scheme (0 = P1). Only parsing changes (K3); safe mid-fight, kept across resets.</summary>
	public ControlScheme SchemeOf(int player) => Match.Fighters[player].Input.Scheme;
	public void SetScheme(int player, ControlScheme scheme)
	{
		Match.Fighters[player].Input.Scheme = scheme;
		Recorder?.OnScheme(player, scheme); // YOK-49
	}

	/// <summary>Debug builds: F4 (DebugOverlay) flips P1 between Kihon and Kata.</summary>
	public void ToggleScheme(int player)
	{
		SetScheme(player, SchemeOf(player) == ControlScheme.Kihon ? ControlScheme.Kata : ControlScheme.Kihon);
		GD.Print($"FightScene: P{player + 1} scheme {SchemeOf(player)}");
	}

	/// <summary>
	/// YOK-21: kind "special" files in data/moves/, falling back to the TEST FIXTURE Spirit Wave and Rising
	/// Talisman in tests/fixtures/specials/ while data/moves/ has no specials (real ones come after F2).
	/// </summary>
	/// YOK-56: now the fighter's kit specials (default Ryo: his starters, else the fixture starters).
	/// </summary>
	public static SpecialData[] LoadSpecials(string fighter = Kit.Ryo) => LoadKit(fighter).Specials;

	/// <summary>A1: the starters fill their own slots (Spirit Wave A, Rising Talisman B) at Lv 1.</summary>
	public static void EquipStarters(Fighter f, SpecialData[] specials) => Kit.EquipSpecials(f, specials);

	/// <summary>
	/// A fighter's normals and throw (YOK-56 kit, default Ryo): its own data/moves/ files, with the TEST FIXTURE
	/// normals (C8), throw (C4, YOK-19) and jump-ins (E11, YOK-55) per kind while it has none of that kind.
	/// </summary>
	public static MoveData[] LoadMoves(string fighter = Kit.Ryo) => LoadKit(fighter).Moves;

	public override void _Ready()
	{
		SetUpRun(); // YOK-48: a fresh run, the first fight built from it
		BuildStage();
		_bodies[0] = BuildFighter("Ryo", new Color(0.85f, 0.85f, 0.95f));
		_bodies[1] = BuildFighter("Kitsune", new Color(0.95f, 0.55f, 0.2f));
		BuildModels(); // YOK-53: rigged fighters over the capsules
		_camera = new Camera3D { Name = "Camera", Fov = CameraFovDegrees, Current = true };
		AddChild(_camera);
		_hud = GetNode<FightHud>("Hud"); // YOK-39: scenes/ui/hud.tscn, an instance in fight.tscn
		_hud.RestartRequested += RestartRun; // YOK-48: Restart = a fresh run from the first fight
		_hud.View = new MatchHudView(Match, 0); // YOK-20: Ryo's live meter and burst
		ApplyLoseCard(); // YOK-44: the lose-screen story card, {yokai} = the opponent
		Match.Impact += (_, e) => Shake.OnImpact(e);
		SetUpAi(); // YOK-27
		StartRecording(); // YOK-49
		SetUpFlow(); // YOK-48: binding card, reward screen, demo complete, F9 debug menu
		// YOK-22: F1 boxes, F2 pause, F3 step; YOK-23: F4 P1 scheme. debug_overlay.tscn is in fight.tscn, kept in debug builds only.
		if (!OS.IsDebugBuild()) GetNode("DebugOverlay").QueueFree();
		Render();
	}

	public override void _Process(double delta)
	{
		if (ExternalDrive) return;

		bool resetDown = OS.IsDebugBuild() && Input.IsPhysicalKeyPressed(Key.R); // debug only: players can't skip the reward (YOK-48)
		if (resetDown && !_resetHeld && Match.Phase == MatchPhase.Over) ResetFight();
		_resetHeld = resetDown;
		PollAiKeys(); // YOK-27: F6 temperament, F7 P2 AI on/off
		PollReplayKey(); // YOK-49: F8 saves the replay
		PollModelKey(); // YOK-53: F10 models/capsules
		PollFlowKeys(); // YOK-48: F9 debug menu
		if (FlowPaused || Stage == DemoStage.Title) { _clock.Restart(); Render(); return; } // YOK-39: no ticks before Start

		int due = Stepper.Filter(_clock.Advance((long)Time.GetTicksUsec()));
		for (int i = 0; i < due; i++) { Shake.Advance(); StepSim(P1Live(), P2Input()); /* YOK-27: P2 AI by default; YOK-49 records */ }
		CheckDuelOver(); // YOK-48
		Render();
	}

	/// <summary>One sim tick with explicit inputs (ExternalDrive), then redraw.</summary>
	public void Step(FighterInput p1, FighterInput p2)
	{
		if (Stage == DemoStage.Title) return; // YOK-39: the sim waits for Start Game
		Shake.Advance();
		StepSim(p1, p2);
		CheckDuelOver(); // YOK-48
		Render();
	}

	/// <summary>Replays the current duel (R after a KO): same run, same carried health; the flow screens close.</summary>
	public void ResetFight()
	{
		HideFlowScreens(); // YOK-48
		Stage = DemoStage.Fighting;
		Match.Reset();
		RebuildAi(); // YOK-27: fresh delay buffer
		StartRecording(); // YOK-49
		Shake.Stop();
		_clock.Restart();
		Render();
	}

	public static float ToMeters(int centiUnits) => centiUnits / (SimConfig.Scale * UnitsPerMeter);

	private void Render()
	{
		var c = Match.Config;
		for (int i = 0; i < 2; i++)
		{
			Fighter f = Match.Fighters[i];
			Node3D body = _bodies[i];
			body.Position = new Vector3(ToMeters(f.X), ToMeters(f.Y), 0f);
			// Facing: the capsule's local +X ("nose") points toward the opponent.
			float tilt = 0f;
			if (f.KnockedOut)
				tilt = Mathf.DegToRad(80f) * Match.KoFrame / c.KoSlowFrames; // falls over during the V4 slow-down
			body.Scale = new Vector3(f.Facing, f.Crouching ? 0.6f : 1f, 1f); // placeholder crouch squash
			body.Rotation = new Vector3(0f, 0f, tilt * f.Facing);
		}
		RenderModels(); // YOK-53

		// Visible width ViewWidth at FOV 25 deg (vertical, keep-height) on a 16:9 screen.
		float viewW = ToMeters(c.ViewWidth);
		float halfFovTan = Mathf.Tan(Mathf.DegToRad(CameraFovDegrees) / 2f);
		float distance = viewW / (2f * halfFovTan * (16f / 9f));
		_camera.Position = new Vector3(ToMeters(FightCamera.CenterX(Match)), CameraHeight, distance);
		var (sx, sy) = Shake.OffsetPx; // V3: px = gameplay units at the Z = 0 plane
		_camera.HOffset = ToMeters(sx * SimConfig.Scale);
		_camera.VOffset = ToMeters(sy * SimConfig.Scale);

		RenderProjectiles();
		_hud.Refresh(Match);
	}

	private readonly List<MeshInstance3D> _projectiles = new();

	/// <summary>YOK-21 placeholder: one glowing box per projectile, sized from its data hitbox.</summary>
	private void RenderProjectiles()
	{
		var list = Match.Projectiles;
		while (_projectiles.Count < list.Count)
		{
			var node = new MeshInstance3D
			{
				Name = $"Projectile{_projectiles.Count}",
				Mesh = new BoxMesh { Size = Vector3.One, Material = new StandardMaterial3D { AlbedoColor = new Color(0.6f, 0.9f, 1f), EmissionEnabled = true, Emission = new Color(0.4f, 0.8f, 1f) } },
			};
			AddChild(node);
			_projectiles.Add(node);
		}
		for (int k = 0; k < _projectiles.Count; k++)
		{
			var node = _projectiles[k];
			node.Visible = k < list.Count;
			if (!node.Visible) continue;
			var (x0, y0, x1, y1) = list[k].WorldBox();
			node.Position = new Vector3(ToMeters((x0 + x1) / 2), ToMeters((y0 + y1) / 2), 0f);
			node.Scale = new Vector3(ToMeters(x1 - x0), ToMeters(y1 - y0), 0.3f);
		}
	}

	private Node3D BuildFighter(string name, Color color)
	{
		var root = new Node3D { Name = name };
		var mat = new StandardMaterial3D { AlbedoColor = color };
		float radius = ToMeters(Match.Config.BodyWidth) / 2f;
		root.AddChild(new MeshInstance3D
		{
			Name = "Body",
			Mesh = new CapsuleMesh { Radius = radius, Height = BodyHeight, Material = mat },
			Position = new Vector3(0f, BodyHeight / 2f, 0f),
		});
		root.AddChild(new MeshInstance3D
		{
			Name = "Nose",
			Mesh = new BoxMesh { Size = new Vector3(0.25f, 0.12f, 0.12f), Material = new StandardMaterial3D { AlbedoColor = new Color(0.1f, 0.1f, 0.1f) } },
			Position = new Vector3(radius + 0.08f, BodyHeight * 0.8f, 0f),
		});
		AddChild(root);
		return root;
	}
}
