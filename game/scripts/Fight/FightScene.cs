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

	public Match Match { get; private set; } = new(null, LoadMoves(), LoadMoves());
	private readonly FixedStepClock _clock = new();

	private Camera3D _camera = null!;
	private readonly Node3D[] _bodies = new Node3D[2];
	private FightHud _hud = null!;
	private bool _resetHeld;

	public const string FixtureNormalsDir = "res://tests/fixtures/ryo-normals";

	/// <summary>Both fighters' moves from data; adds the TEST FIXTURE normals (C8) while data/moves/ has no normals.</summary>
	public static MoveData[] LoadMoves()
	{
		var moves = MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath("res://") + "../data/moves");
		if (System.Array.Exists(moves, m => m.IsNormal)) return moves;
		GD.Print("FightScene: data/moves/ has no normals, using TEST FIXTURE normals (C8)");
		return [.. moves, .. MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FixtureNormalsDir))];
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
		AddChild(_hud);
		Render();
	}

	public override void _Process(double delta)
	{
		if (ExternalDrive) return;

		bool resetDown = Input.IsPhysicalKeyPressed(Key.R);
		if (resetDown && !_resetHeld && Match.Phase == MatchPhase.Over) ResetFight();
		_resetHeld = resetDown;

		int due = _clock.Advance((long)Time.GetTicksUsec());
		for (int i = 0; i < due; i++) Match.Step(InputDevices.Read(0), InputDevices.Read(1));
		Render();
	}

	/// <summary>One sim tick with explicit inputs (ExternalDrive), then redraw.</summary>
	public void Step(FighterInput p1, FighterInput p2)
	{
		Match.Step(p1, p2);
		Render();
	}

	public void ResetFight()
	{
		Match.Reset();
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

		_hud.Refresh(Match);
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
