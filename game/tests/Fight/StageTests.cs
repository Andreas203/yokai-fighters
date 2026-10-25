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

	/// <summary>YOK-208: every placed prop with its transform in Props space; the lantern line's built lanterns count one by one.</summary>
	static IEnumerable<(Node3D Node, Transform3D Xf)> PropItems(Node props)
	{
		foreach (var n in props.GetChildren().Cast<Node3D>())
		{
			if (n is LanternLine line)
				foreach (var l in line.GetChildren().Cast<Node3D>()) yield return (l, line.Transform * l.Transform);
			else yield return (n, n.Transform);
		}
	}

	static bool IsLantern(Node3D n) => n.Name.ToString().StartsWith("Lantern") || n.Name.ToString().StartsWith(LanternLine.Prefix);
	static readonly Color Persimmon = Color.FromHtml("#D8632C");

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
		Assert.Equal(2, props.Count(p => p.Name.ToString().StartsWith("Lantern")), "front lanterns (YOK-39: 3 -> 2, deep)");
		var line = stage.GetNode<LanternLine>("Props/PathLanterns");
		Assert.Equal(2 * line.PerSide, line.GetChildCount(), "YOK-208: the lit lantern line, one row each side of the path");
		Assert.True(line.PerSide >= 2, "the line recedes (at least two lanterns a row)");
		foreach (var mi in Meshes(stage.GetNode("Props")))
		{
			if (mi.IsInGroup(ToonLook.FlatFillGroup))
				Assert.True(mi.GetActiveMaterial(0) is BaseMaterial3D { ShadingMode: BaseMaterial3D.ShadingModeEnum.Unshaded, NextPass: null, DisableFog: true } m && m.AlbedoColor.IsEqualApprox(Persimmon), $"{mi.Name}: flat persimmon fill, no outline");
			else
				Assert.True(mi.GetActiveMaterial(0) is BaseMaterial3D { DiffuseMode: BaseMaterial3D.DiffuseModeEnum.Toon, NextPass: not null }, $"{mi.Name}: toon + outline");
		}
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
		// YOK-208: the lit lantern line's lanterns are checked one by one, and every lantern stays in front of the backdrop.
		var scene = Load(runner);
		var backdrop = scene.StageRoot.GetNode<MeshInstance3D>("Backdrop");
		int lanterns = 0;
		foreach (var (n, xf) in PropItems(scene.StageRoot.GetNode("Props")))
		{
			var p = xf.Origin;
			bool lantern = IsLantern(n);
			Aabb own = n.GetChildren().Select(BambooGroveStage.LocalBounds).Aggregate((a, b) => a.Merge(b)); // in the prop's own (unyawed, unscaled) space
			float radius = Mathf.Max(own.Size.X, own.Size.Z) * 0.5f * xf.Basis.Scale.Y;
			float front = p.Z + radius, limit = BambooGroveStage.FrontLimit(p.X, lantern);
			if (lantern)
			{
				lanterns++;
				Assert.True(p.Z <= BambooGroveStage.LanternMaxZ, $"{n.Name} at z {p.Z}: lanterns stay deep in the grove");
				Assert.True(p.Z - radius > backdrop.Position.Z, $"{n.Name} at z {p.Z} (radius {radius}) pokes through the backdrop at z {backdrop.Position.Z}");
			}
			if (Mathf.Abs(p.X) < BambooGroveStage.ClearBandHalfX)
				Assert.True(front <= BambooGroveStage.ClearBandBackZ + 1e-3f, $"{n.Name} at ({p.X}, {p.Z}) reaches z {front}, inside the clear band behind the fight (|x| < {BambooGroveStage.ClearBandHalfX}, z > {BambooGroveStage.ClearBandBackZ})");
			Assert.True(front <= limit + 1e-3f, $"{n.Name}: front z {front} past its limit {limit}");
		}
		Assert.True(lanterns > 2, $"the line's lanterns were checked too ({lanterns})");
		scene.QueueFree();
	}

	[Test]
	public static void Stage_LanternsLitWithFewShadowlessLights(Node runner)
	{
		// YOK-208: every lantern shows the flat persimmon window fill; a handful of shadowless warm lights, no bloom.
		var scene = Load(runner);
		var stage = scene.StageRoot;
		var lanterns = PropItems(stage.GetNode("Props")).Where(i => IsLantern(i.Node)).ToList();
		foreach (var (n, _) in lanterns)
		{
			var fills = Meshes(n).Where(m => m.IsInGroup(ToonLook.FlatFillGroup)).ToList();
			Assert.True(fills.Count >= 1, $"{n.Name}: lit window fill");
			foreach (var f in fills) Assert.True(f.IsVisibleInTree(), $"{n.Name}/{f.Name}: the window fill shows");
		}
		var lights = stage.FindChildren("*", "OmniLight3D", true, false).Cast<OmniLight3D>().ToList();
		Assert.True(lights.Count >= 2 && lights.Count <= 6, $"a handful of lantern lights, got {lights.Count}");
		foreach (var l in lights)
		{
			Assert.True(!l.ShadowEnabled && l.OmniRange <= 4f, $"{l.GetParent().GetParent().Name}: shadowless, local light");
			Assert.True(l.LightEnergy > 0f && l.IsVisibleInTree(), $"{l.GetParent().GetParent().Name}: the light is on");
		}
		foreach (var front in new[] { "Props/Lantern0", "Props/Lantern1" })
			Assert.True(stage.HasNode(front + "/Pivot/Light"), $"{front} lights the ground and stalks near it");
		Assert.True(!stage.GetNode<WorldEnvironment>("Environment").Environment.GlowEnabled, "flat fill, never a bloom halo");
		scene.QueueFree();
	}

	[Test]
	public static void Stage_LanternLineFollowsAMovedBackdrop(Node runner)
	{
		// YOK-208 (Code Critic advisory): YOK-209 may nudge the backdrop quad; the line's vanish point follows the floor seam
		// at VanishU from the quad's full transform: moved, scaled, and re-parented under a moved node.
		var scene = Load(runner);
		var stage = scene.StageRoot;
		var line = stage.GetNode<LanternLine>("Props/PathLanterns");
		var backdrop = stage.GetNode<MeshInstance3D>("Backdrop");
		var size = ((QuadMesh)backdrop.Mesh).Size;
		void Check(string what, Vector3 expectWorld)
		{
			Vector3 got = line.GlobalTransform * line.VanishPoint();
			Assert.True(got.DistanceTo(expectWorld) < 1e-3f, $"{what}: vanish point {got}, expected {expectWorld}");
			line.Rebuild();
			var row = line.GetChildren().Cast<Node3D>().Where(n => n.Name.ToString().StartsWith(LanternLine.Prefix + "L")).ToList();
			Assert.True(row[^1].GlobalPosition.DistanceTo(expectWorld) < row[0].GlobalPosition.DistanceTo(expectWorld), $"{what}: the rebuilt row recedes toward it");
		}
		float u = line.VanishU;
		Check("as placed", new Vector3(backdrop.Position.X + (u - 0.5f) * size.X, 0, backdrop.Position.Z));
		// Moved 1.5 m right and 0.5 m nearer, raised 1 m, scaled 1.2: the seam stays on the floor, x scales about the centre.
		backdrop.Transform = new Transform3D(Basis.Identity.Scaled(Vector3.One * 1.2f), backdrop.Position + new Vector3(1.5f, 1f, 0.5f));
		Check("moved and scaled", new Vector3(backdrop.Position.X + (u - 0.5f) * size.X * 1.2f, 0, backdrop.Position.Z));
		// Re-parented under a holder 2 m to the left: the line resolves it by path and world transform.
		var holder = new Node3D { Name = "BackdropHolder", Position = new Vector3(-2f, 0, 0) };
		stage.AddChild(holder);
		backdrop.Reparent(holder, keepGlobalTransform: false);
		line.Backdrop = line.GetPathTo(backdrop);
		Vector3 world = backdrop.GlobalPosition;
		Check("re-parented", new Vector3(world.X + (u - 0.5f) * size.X * 1.2f, 0, world.Z));
		scene.QueueFree();
	}

	[Test]
	public static void Stage_LanternLineRecedesTowardTheVanishPoint()
	{
		// YOK-208: the pure layout: each row converges on the vanish point and shrinks with the same factor; only the nearest are lit.
		var vanish = new Vector3(2f, 0, -16f);
		var list = LanternLine.Placements(new Vector3(-2.4f, 0, -13.8f), new Vector3(3f, 0, -13.4f), vanish, 4, 0.6f, 0.6f, 2);
		Assert.Equal(8, list.Count, "two rows of four");
		Assert.Equal(list.Count, list.Select(p => p.Name).Distinct().Count(), "unique names");
		foreach (string side in new[] { "L", "R" })
		{
			var row = list.Where(p => p.Name.StartsWith(LanternLine.Prefix + side)).ToList();
			for (int i = 1; i < row.Count; i++)
			{
				Assert.True(row[i].Position.DistanceTo(vanish) < row[i - 1].Position.DistanceTo(vanish), $"{row[i].Name} nearer the vanish point");
				Assert.True(row[i].Position.Z < row[i - 1].Position.Z && row[i].Scale < row[i - 1].Scale, $"{row[i].Name} deeper and smaller");
				Assert.True(Mathf.IsEqualApprox(row[i].Position.DistanceTo(vanish) / row[i - 1].Position.DistanceTo(vanish), row[i].Scale / row[i - 1].Scale), "forced perspective: distance and scale shrink together");
			}
			Assert.Equal(2, row.Count(p => p.Lit), "the two nearest of each row keep their light");
			Assert.True(row[0].Lit && row[1].Lit, "lit = the nearest");
			Assert.True(row.All(p => p.Position.Y == 0f), "on the floor");
		}
		Assert.Equal(0, LanternLine.Placements(Vector3.Zero, Vector3.Zero, vanish, 0, 0.6f, 0.6f, 2).Count, "no lanterns");
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
			string s = string.Join(";", PropItems(scene.StageRoot.GetNode("Props")).Select(i => $"{i.Node.Name}:{i.Xf}"));
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
