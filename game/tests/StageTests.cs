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
		var scene = Load(runner);
		foreach (var n in scene.StageRoot.GetNode("Props").GetChildren().Cast<Node3D>())
		{
			var p = n.Position;
			if (n.Name.ToString().StartsWith("Lantern"))
				Assert.True(p.Z <= BambooGroveStage.LanternMaxZ, $"{n.Name} at z {p.Z}: lanterns stay deep in the grove");
			if (Mathf.Abs(p.X) < BambooGroveStage.ClearBandHalfX)
				Assert.True(p.Z <= BambooGroveStage.ClearBandBackZ, $"{n.Name} at ({p.X}, {p.Z}) is inside the clear band behind the fight");
		}
		scene.QueueFree();
	}

	[Test]
	public static void Stage_CoversCameraRangeAtBothCorners(Node runner)
	{
		var scene = Load(runner);
		var c = scene.Match.Config;
		float tan = Mathf.Tan(Mathf.DegToRad(FightScene.CameraFovDegrees) / 2f), d = FightScene.CameraDistance(c);
		float camLimit = FightScene.ToMeters(FightCamera.Limit(c)); // YOK-39: reaches the cornered model
		var backdrop = scene.StageRoot.GetNode<MeshInstance3D>("Backdrop");
		var size = ((QuadMesh)backdrop.Mesh).Size;
		float dist = d - backdrop.Position.Z, halfW = dist * tan * 16f / 9f, halfH = dist * tan;
		foreach (float camX in new[] { -camLimit, camLimit })
		{
			Assert.True(backdrop.Position.X - size.X / 2 <= camX - halfW, $"backdrop left edge covers camera x {camX}");
			Assert.True(backdrop.Position.X + size.X / 2 >= camX + halfW, $"backdrop right edge covers camera x {camX}");
		}
		Assert.True(backdrop.Position.Y + size.Y / 2 >= FightScene.CameraHeight + halfH, "backdrop reaches the top of the view");
		Assert.True(backdrop.Position.Y - size.Y / 2 <= 0f, "backdrop meets the floor");
		var floor = scene.StageRoot.GetNode<MeshInstance3D>("Floor");
		var fs = ((PlaneMesh)floor.Mesh).Size;
		Assert.True(floor.Position.Z + fs.Y / 2 >= d - FightScene.CameraHeight / tan, "floor reaches the bottom of the view");
		Assert.True(floor.Position.Z - fs.Y / 2 <= backdrop.Position.Z, "floor reaches the backdrop");
		Assert.True(fs.X / 2 >= camLimit + halfW, "floor wide enough at the backdrop");
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
