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

	/// <summary>Both fighters get the moves and Ryo's starters (A1). P2 shares Ryo's kit until the yokai kits land.</summary>
	public static Match NewMatch()
	{
		var m = new Match(null, LoadMoves(), LoadMoves());
		foreach (var f in m.Fighters)
		{
			EquipStarters(f, LoadSpecials());
			f.Input.Scheme = DefaultScheme;
		}
		return m;
	}

	/// <summary>YOK-23: a player's scheme (0 = P1). Only parsing changes (K3); safe mid-fight, kept across resets.</summary>
	public ControlScheme SchemeOf(int player) => Match.Fighters[player].Input.Scheme;
	public void SetScheme(int player, ControlScheme scheme) => Match.Fighters[player].Input.Scheme = scheme;

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
	public static SpecialData[] LoadSpecials()
	{
		var specials = SpecialLoader.LoadDirectory(ProjectSettings.GlobalizePath("res://") + "../data/moves");
		if (specials.Length > 0) return specials;
		GD.Print("FightScene: data/moves/ has no specials, using TEST FIXTURE Spirit Wave and Rising Talisman");
		return SpecialLoader.LoadDirectory(ProjectSettings.GlobalizePath(FixtureSpecialsDir));
	}

	/// <summary>A1: the starters fill their own slots (Spirit Wave A, Rising Talisman B) at Lv 1.</summary>
	public static void EquipStarters(Fighter f, SpecialData[] specials)
	{
		foreach (var s in specials)
			if (f.Specials[(int)s.Slot] is null) f.Equip(s.Slot, s, 1);
	}

	/// <summary>
	/// Both fighters' moves from data; adds the TEST FIXTURE normals (C8) while data/moves/ has no normals
	/// and the TEST FIXTURE throw (C4, YOK-19) while it has no throw (the grab clip is being retaken, YOK-31),
	/// and the TEST FIXTURE jump-in normals (E11, YOK-55) while it has no air normal.
	/// </summary>
	public static MoveData[] LoadMoves()
	{
		var moves = MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath("res://") + "../data/moves", includeSpecials: false);
		if (!System.Array.Exists(moves, m => m.IsNormal))
		{
			GD.Print("FightScene: data/moves/ has no normals, using TEST FIXTURE normals (C8)");
			moves = [.. moves, .. MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FixtureNormalsDir))];
		}
		if (!System.Array.Exists(moves, m => m.IsThrow))
		{
			GD.Print("FightScene: data/moves/ has no throw, using the TEST FIXTURE throw (C4)");
			moves = [.. moves, .. MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FixtureThrowsDir))];
		}
		if (!System.Array.Exists(moves, m => m.IsNormal && m.Air))
		{
			GD.Print("FightScene: data/moves/ has no air normals, using TEST FIXTURE jump-ins (E11)");
			moves = [.. moves, .. MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FixtureAirNormalsDir))];
		}
		return moves;
	}

	public override void _Ready()
	{
		BuildStage();
		_bodies[0] = BuildFighter("Ryo", new Color(0.85f, 0.85f, 0.95f));
		_bodies[1] = BuildFighter("Kitsune", new Color(0.95f, 0.55f, 0.2f));
		_camera = new Camera3D { Name = "Camera", Fov = CameraFovDegrees, Current = true };
		AddChild(_camera);
		_hud = new FightHud { Name = "Hud" };
		_hud.RestartRequested += ResetFight;
		_hud.View = new MatchHudView(Match, 0); // YOK-20: Ryo's live meter and burst
		Match.Impact += (_, e) => Shake.OnImpact(e);
		AddChild(_hud);
		if (OS.IsDebugBuild()) AddChild(new DebugOverlay { Scene = this }); // YOK-22: F1 boxes, F2 pause, F3 step; YOK-23: F4 P1 scheme
		Render();
	}

	public override void _Process(double delta)
	{
		if (ExternalDrive) return;

		bool resetDown = Input.IsPhysicalKeyPressed(Key.R);
		if (resetDown && !_resetHeld && Match.Phase == MatchPhase.Over) ResetFight();
		_resetHeld = resetDown;

		int due = Stepper.Filter(_clock.Advance((long)Time.GetTicksUsec()));
		for (int i = 0; i < due; i++) { Shake.Advance(); Match.Step(InputDevices.Read(0), InputDevices.Read(1)); }
		Render();
	}

	/// <summary>One sim tick with explicit inputs (ExternalDrive), then redraw.</summary>
	public void Step(FighterInput p1, FighterInput p2)
	{
		Shake.Advance();
		Match.Step(p1, p2);
		Render();
	}

	public void ResetFight()
	{
		Match.Reset();
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

	private void BuildStage()
	{
		var c = Match.Config;
		float stageW = ToMeters(c.StageHalfWidth * 2);
		AddChild(new WorldEnvironment
		{
			Environment = new Environment
			{
				BackgroundMode = Environment.BGMode.Color,
				BackgroundColor = new Color(0.32f, 0.22f, 0.3f), // dusk placeholder (V9 bamboo grove)
				AmbientLightSource = Environment.AmbientSource.Color,
				AmbientLightColor = new Color(0.5f, 0.45f, 0.5f),
			},
		});
		AddChild(new DirectionalLight3D { Name = "Sun", RotationDegrees = new Vector3(-50f, -30f, 0f) });
		AddChild(new MeshInstance3D
		{
			Name = "Floor",
			Mesh = new BoxMesh { Size = new Vector3(stageW + 8f, 0.2f, 6f), Material = new StandardMaterial3D { AlbedoColor = new Color(0.3f, 0.36f, 0.22f) } },
			Position = new Vector3(0f, -0.1f, 0f),
		});
		AddChild(new MeshInstance3D
		{
			Name = "Backdrop",
			Mesh = new QuadMesh { Size = new Vector2(stageW + 12f, 8f), Material = new StandardMaterial3D { AlbedoColor = new Color(0.45f, 0.3f, 0.35f), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded } },
			Position = new Vector3(0f, 4f, -4f),
		});
		// Corner posts mark the stage bounds.
		foreach (int side in new[] { -1, 1 })
		{
			AddChild(new MeshInstance3D
			{
				Name = side < 0 ? "CornerLeft" : "CornerRight",
				Mesh = new BoxMesh { Size = new Vector3(0.2f, 5f, 0.2f), Material = new StandardMaterial3D { AlbedoColor = new Color(0.4f, 0.55f, 0.3f) } },
				Position = new Vector3(side * stageW / 2f, 2.5f, -0.5f),
			});
		}
	}
}
