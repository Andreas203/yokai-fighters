using System.Collections.Generic;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>YOK-39: the bamboo grove stage loads, stays behind the gameplay plane, covers the camera range, falls back.</summary>
public static class StageTests
{
	static FightScene Load(Node runner)
	{
		var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		scene.ExternalDrive = true;
		runner.AddChild(scene);
		return scene;
	}

	static IEnumerable<MeshInstance3D> Meshes(Node n) => n.FindChildren("*", "MeshInstance3D", true, false).Cast<MeshInstance3D>();

	[Test]
	public static void Stage_SceneLoadsBambooGrove(Node runner)
	{
		var scene = Load(runner);
		Assert.Equal(BambooGroveStage.Kind, scene.StageKind, "the fight scene builds the bamboo grove");
		var stage = scene.StageRoot;
		Assert.True(stage.HasNode("Backdrop") && stage.HasNode("Floor") && stage.HasNode("Sun") && stage.HasNode("Fill"), "backdrop, floor, key and fill");
		Assert.True(stage.GetNode<WorldEnvironment>("Environment").Environment.FogEnabled, "dusk fog");
		var props = stage.GetNode<Node3D>("Props").GetChildren();
		Assert.Equal(10, props.Count(p => p.Name.ToString().StartsWith("Bamboo")), "bamboo clusters (YOK-39: 12 -> 10)");
		Assert.Equal(2, props.Count(p => p.Name.ToString().StartsWith("Lantern")), "lanterns (YOK-39: 3 -> 2, deep)");
		foreach (var mi in Meshes(stage.GetNode("Props")))
			Assert.True(mi.GetActiveMaterial(0) is BaseMaterial3D { DiffuseMode: BaseMaterial3D.DiffuseModeEnum.Toon, NextPass: not null }, $"{mi.Name}: toon + outline");
		scene.Step(FighterInput.None, FighterInput.None);
		scene.QueueFree();
	}

	[Test]
	public static void Stage_PropsStayBehindGameplayPlane(Node runner)
	{
		var scene = Load(runner);
		foreach (var mi in Meshes(scene.StageRoot.GetNode("Props")))
		{
			Aabb b = mi.GlobalTransform * mi.Mesh.GetAabb();
			Assert.True(b.End.Z <= -1f, $"{mi.Name} reaches z {b.End.Z} (fighters live at Z = 0)");
			Assert.True(b.Position.Y >= -0.05f, $"prop base on the floor, got y {b.Position.Y}");
		}
		scene.QueueFree();
	}

	[Test]
	public static void Stage_ClearBandBehindTheFight_LanternsDeep(Node runner)
	{
		// YOK-39: the props are placed by hand in bamboo_grove.tscn, so the rule is checked here, not enforced in code.
		// Each prop's front = its centre z + its footprint radius (largest horizontal mesh extent / 2, any yaw).
		var scene = Load(runner);
		foreach (var n in scene.StageRoot.GetNode("Props").GetChildren().Cast<Node3D>())
		{
			var p = n.Position;
			bool lantern = n.Name.ToString().StartsWith("Lantern");
			Aabb own = n.GetChildren().Select(BambooGroveStage.LocalBounds).Aggregate((a, b) => a.Merge(b)); // in the prop's own (unyawed, unscaled) space
			float radius = Mathf.Max(own.Size.X, own.Size.Z) * 0.5f * n.Basis.Scale.Y;
			float front = p.Z + radius, limit = BambooGroveStage.FrontLimit(p.X, lantern);
			if (lantern)
				Assert.True(p.Z <= BambooGroveStage.LanternMaxZ, $"{n.Name} at z {p.Z}: lanterns stay deep in the grove");
			if (Mathf.Abs(p.X) < BambooGroveStage.ClearBandHalfX)
				Assert.True(front <= BambooGroveStage.ClearBandBackZ + 1e-3f, $"{n.Name} at ({p.X}, {p.Z}) reaches z {front}, inside the clear band behind the fight (|x| < {BambooGroveStage.ClearBandHalfX}, z > {BambooGroveStage.ClearBandBackZ})");
			Assert.True(front <= limit + 1e-3f, $"{n.Name}: front z {front} past its limit {limit}");
		}
		scene.QueueFree();
	}

	[Test]
	public static void Stage_CoversCameraRangeAtBothCorners(Node runner)
	{
		var scene = Load(runner);
		// YOK-129: the dynamic camera's extreme framings: closest and widest zoom (plus the pitch's 1% slack), at rest and
		// raised for a jump apex at the top of the HUD clamp, pushed to either stage edge. Every screen corner's ray must
		// land on the backdrop (above the floor) or on the floor.
		var c = scene.Match.Config;
		var cam = new DynamicCamera(c);
		var backdrop = scene.StageRoot.GetNode<MeshInstance3D>("Backdrop");
		var size = ((QuadMesh)backdrop.Mesh).Size;
		var floor = scene.StageRoot.GetNode<MeshInstance3D>("Floor");
		var fs = ((PlaneMesh)floor.Mesh).Size;
		Assert.True(backdrop.Position.Y - size.Y / 2 <= 0f, "backdrop meets the floor");
		Assert.True(floor.Position.Z - fs.Y / 2 <= backdrop.Position.Z, "floor reaches the backdrop");
		float apex = c.JumpY(c.JumpFrames / 2) / (float)SimConfig.Scale;
		var air = new DynamicCamera.Subject(new DynamicCamera.Body(0, apex, -240, 240), new DynamicCamera.Body(0, 0, -240, 240), 0, 0);
		var framings = new System.Collections.Generic.List<(DynamicCamera Cam, float W, float H)>();
		foreach (float w in new[] { cam.MinWidth, cam.MaxWidth * 1.01f })
			foreach (float h in new[] { cam.Config.BaseHeightMeters, cam.Config.WideHeightMeters, Mathf.Max(cam.TargetHeight(air, w), cam.MinHeight(w, air)) })
				framings.Add((cam, w, h));
		// YOK-133 / AM10: an Oni fight's camera (framed for his club) at the widest width and the highest height its feet floor
		// reaches, over jumps (one or both fighters) at close range and at the walls, from either corner.
		var oni = new DynamicCamera(c, CameraConfig.Default.ForBodies("ryo", "oni"));
		float maxW = 0, hAtMaxW = 0, maxH = 0, wAtMaxH = 0;
		int corner = c.StageHalfWidth - c.BodyWidth / 2;
		foreach (int sep in new[] { c.BodyWidth, c.MaxSeparation })
			foreach (int at in new[] { -corner, corner })
				foreach (bool both in new[] { false, true })
				{
					var m = new Match();
					m.P1.X = System.Math.Clamp(at - sep / 2, -corner, corner - sep); m.P2.X = m.P1.X + sep;
					oni.Snap(DynamicCamera.SubjectOf(m));
					for (int t = 0; t < c.JumpFrames * 3; t++)
					{
						m.Step(new FighterInput(InputBits.Up), both ? new FighterInput(InputBits.Up) : FighterInput.None);
						oni.Step(DynamicCamera.SubjectOf(m));
						if (oni.Width > maxW) { maxW = oni.Width; hAtMaxW = oni.HeightMeters; }
						if (oni.HeightMeters > maxH) { maxH = oni.HeightMeters; wAtMaxH = oni.Width; }
					}
				}
		GD.Print($"YOK-133 stage: Oni camera widest {maxW:0} (h {hAtMaxW:0.00} m), highest {maxH:0.00} m (w {wAtMaxH:0})");
		Assert.True(maxW > cam.MaxWidth, "the Oni camera widens past the sim screen somewhere (else this pass proves nothing)");
		framings.Add((oni, maxW, hAtMaxW));
		framings.Add((oni, wAtMaxH, maxH));
		framings.Add((oni, maxW, maxH));
		foreach (var (fc, w, h) in framings)
				foreach (int side in new[] { -1, 1 })
				{
					var cam2 = fc;
					cam2.Place(w, h, side * (cam2.StageHalf - w / 2f));
					foreach (var (nx, ny) in new[] { (-1f, -1f), (1f, -1f), (-1f, 1f), (1f, 1f) })
					{
						var (o, dir) = cam2.ScreenRay(nx, ny);
						float t = (backdrop.Position.Z - o.Z) / dir.Z, x = o.X + dir.X * t, y = o.Y + dir.Y * t;
						string at = $"width {w:0}, height {h:0.00}, side {side}, corner ({nx}, {ny})";
						if (y >= 0f)
						{
							Assert.True(Mathf.Abs(x - backdrop.Position.X) <= size.X / 2, $"{at}: backdrop covers x {x:0.00}");
							Assert.True(y <= backdrop.Position.Y + size.Y / 2, $"{at}: backdrop reaches the top ({y:0.00})");
						}
						else
						{
							float tf = -o.Y / dir.Y, fx = o.X + dir.X * tf, fz = o.Z + dir.Z * tf;
							Assert.True(Mathf.Abs(fx - floor.Position.X) <= fs.X / 2 && Mathf.Abs(fz - floor.Position.Z) <= fs.Y / 2, $"{at}: floor covers ({fx:0.00}, {fz:0.00})");
						}
					}
				}
		scene.QueueFree();
	}

	[Test]
	public static void Stage_PropLayoutIsDeterministic(Node runner)
	{
		string Layout()
		{
			var scene = Load(runner);
			string s = string.Join(";", scene.StageRoot.GetNode("Props").GetChildren().Cast<Node3D>().Select(n => $"{n.Name}:{n.Transform}"));
			scene.QueueFree();
			return s;
		}
		Assert.Equal(Layout(), Layout(), "fixed seed, same grove every build");
	}

	[Test]
	public static void Stage_FallsBackToPlaceholder(Node runner)
	{
		string dir = FightScene.StageDir;
		try
		{
			FightScene.StageDir = "res://assets/generated/stage/missing/";
			var scene = Load(runner);
			Assert.Equal("placeholder", scene.StageKind, "missing assets fall back to the placeholder");
			Assert.True(scene.StageRoot.HasNode("Floor") && scene.StageRoot.HasNode("Backdrop") && scene.StageRoot.HasNode("CornerLeft"), "placeholder pieces");
			Assert.True(!scene.StageRoot.HasNode("Props"), "no half-built grove left behind");
			scene.Step(FighterInput.None, FighterInput.None);
			scene.QueueFree();
		}
		finally { FightScene.StageDir = dir; }
		bool flag = FightScene.UsePlaceholderStage;
		try
		{
			FightScene.UsePlaceholderStage = true;
			var scene = Load(runner);
			Assert.Equal("placeholder", scene.StageKind, "debug flag forces the placeholder");
			scene.QueueFree();
		}
		finally { FightScene.UsePlaceholderStage = flag; }
	}
}
