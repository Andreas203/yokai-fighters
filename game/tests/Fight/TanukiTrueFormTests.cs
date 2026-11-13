using System.Linq;
using Godot;
using YokaiFighters.Dev;
using YokaiFighters.Fight;

namespace YokaiFighters.Tests;

/// <summary>
/// AM25: the Tanuki boss fight shows his true form (Tripo, shared skeleton, assets/specs/rigs/tanuki.md v2b) on Ryo's
/// Tripo clips, behind a switch that is on by default. Off, he fights in Ryo's body as before (T0). Presentation only:
/// the sim never reads the body.
/// </summary>
public static class TanukiTrueFormTests
{
	static ClipCatalog Cat => FightScene.Catalog;

	[Test]
	public static void Switch_IsOnByDefault_AndOffGivesRyosBody()
	{
		Assert.True(FighterAnimSet.TripoTanuki, "the true form is on by default (AM25)");
		Assert.True(ReferenceEquals(FighterAnimSet.TanukiTripo, FighterAnimSet.For("tanuki")), "the Tanuki kit shows the true form");
		Assert.True(ReferenceEquals(FighterAnimSet.TanukiTripo, FighterAnimSet.ForNode("tanuki", "tanuki")), "the boss node too");
		Assert.True(ReferenceEquals(FighterAnimSet.RyoTripo, FighterAnimSet.For("ryo")), "Ryo keeps his body");
		Assert.Equal("tanuki", FighterAnimSet.TanukiTripo.Fighter, "fighter id");
		Assert.True(FighterAnimSet.TanukiTripo.ModelPath.EndsWith("tanuki-v2-rigged.glb"), "the v2b model");
		Assert.Equal(FighterAnimSet.RyoTripo.ClipRoot, FighterAnimSet.TanukiTripo.ClipRoot, "Ryo's Tripo clips (T0)");
		Assert.True(ReferenceEquals(FighterAnimSet.RyoTripo.Clips, FighterAnimSet.TanukiTripo.Clips) || FighterAnimSet.RyoTripo.Clips.SequenceEqual(FighterAnimSet.TanukiTripo.Clips), "Ryo's pose table");
		Assert.True(FighterAnimSet.RyoTripo.ClipOverrides.SequenceEqual(FighterAnimSet.TanukiTripo.ClipOverrides), "Ryo's special overrides: a copied special plays Ryo's take");
		try
		{
			FighterAnimSet.TripoTanuki = false;
			Assert.True(ReferenceEquals(FighterAnimSet.Ryo, FighterAnimSet.For("tanuki")), "off: Ryo's body (T0)");
			Assert.True(ReferenceEquals(FighterAnimSet.Ryo, FighterAnimSet.ForNode("tanuki", "tanuki")), "off: the boss node too");
		}
		finally { FighterAnimSet.TripoTanuki = true; }
	}

	/// <summary>The true form loads on the shared skeleton, plays every clip it can be asked for, and fits the camera.</summary>
	[Test]
	public static void TrueForm_Loads_PlaysEveryClipItShows_AndFitsTheFraming(Node runner)
	{
		var set = FighterAnimSet.TanukiTripo;
		var model = FighterModel.Create(set, Cat);
		Assert.True(model != null, $"loads ({set.ScenePath})");
		runner.AddChild(model);
		Assert.Equal(set.ScenePath, model!.SceneFilePath, "its own scene");
		Assert.Equal(25, model.Skeleton.GetBoneCount(), "25 bones (the shared skeleton)");
		Assert.True(model.Skeleton.FindBone("Hips") >= 0 && model.Skeleton.FindBone("RightHand") >= 0, "shared bone names");
		foreach (string id in set.Clips.Values.Concat(set.ClipOverrides.Values).Append(set.FallbackThrowClip).Distinct())
		{
			var info = Cat[id];
			Assert.True(info != null, $"{id} catalogued");
			Assert.True(set.ClipPath(info!) is { } p && p.StartsWith(set.ClipRoot!), $"{id} from {set.ClipRoot}");
			Assert.True(model.Has(id), $"{id} in his library");
		}
		float k = model.HipScaleFor(Cat["ryo-idle-guard"]!);
		Assert.True(k > 0.3f && k < 1f, $"hip scale from his own rig ({k:0.000}): shorter legs than Ryo's");
		var (top, clip, _, _, _, _) = ModelExtentTests.MeasureTop(model);
		float framed = CameraConfig.Default.TopFor(set.Fighter);
		GD.Print($"AM25 tanuki: hip scale {k:0.000}, top {top:0.000} m ({clip}); framed {framed:0.000}");
		Assert.True(top <= framed, $"highest point {top:0.000} m ({clip}) > framed top {framed:0.000}");
		model.QueueFree();
	}

	static FightScene BossFight(Node runner)
	{
		var jump = DevJump.BuildFight(new DevFightSetup("tanuki"), FightScene.LoadAbilityPool());
		Assert.True(jump.Session.Enter(jump.NodeId).Ok, "node entered");
		var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		scene.ExternalDrive = true;
		scene.Session = jump.Session;
		runner.AddChild(scene);
		return scene;
	}

	[Test]
	public static void BossFight_ShowsTheTrueForm_UnlessSwitchedOff(Node runner)
	{
		try
		{
			var on = BossFight(runner);
			try
			{
				Assert.True(ReferenceEquals(FighterAnimSet.TanukiTripo, on.ModelOf(1)!.AnimSet), "P2: the true form");
				Assert.Equal(FighterAnimSet.TanukiTripo.ScenePath, on.GetNode<Node3D>($"{FightScene.FightersName}/P2").SceneFilePath, "P2 scene");
				Assert.True(ReferenceEquals(FighterAnimSet.RyoTripo, on.ModelOf(0)!.AnimSet), "P1: Ryo");
			}
			finally { on.QueueFree(); }
			FighterAnimSet.TripoTanuki = false;
			var off = BossFight(runner);
			try { Assert.True(ReferenceEquals(FighterAnimSet.RyoTripo, off.ModelOf(1)!.AnimSet), "switched off: Ryo's body"); }
			finally { off.QueueFree(); }
		}
		finally { FighterAnimSet.TripoTanuki = true; }
	}

	[Test]
	public static void DevMenu_HasTheSwitch()
	{
		var page = DevMenu.ToolsPage();
		try
		{
			var box = page.GetNodeOrNull<CheckButton>("TripoTanuki");
			Assert.True(box != null && box.ButtonPressed, "the switch, on");
			box!.ButtonPressed = false;
			Assert.True(!FighterAnimSet.TripoTanuki, "unticked: off");
			box.ButtonPressed = true;
			Assert.True(FighterAnimSet.TripoTanuki, "ticked: on");
		}
		finally { FighterAnimSet.TripoTanuki = true; page.Free(); }
	}
}
