using System;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-39 camera fix: the rigged models' horizontal reach (every frame of every clip, bones plus a flesh pad, and the
/// Kitsune's tails) fits inside <see cref="SimConfig.ModelFrontReach"/> / <see cref="SimConfig.ModelBackReach"/>,
/// the visual extents <see cref="FightCamera"/> keeps in frame.
/// </summary>
public static class ModelExtentTests
{
	/// <summary>Skin, fingers and toes past the last bone joint, in units (bones are joint centres).</summary>
	public const int FleshPad = 20;

	/// <summary>(front, back) reach in units for one model, facing +X, over every frame of every clip in its library.</summary>
	public static (int Front, int Back, string FrontClip, string BackClip) Measure(FighterModel model)
	{
		model.Position = Vector3.Zero;
		model.Mirror.Scale = Vector3.One;
		var lib = model.Player.GetAnimationLibrary(FighterModel.LibraryName);
		float front = 0, back = 0; string fc = "", bc = "";
		foreach (var name in lib.GetAnimationList())
		{
			var anim = lib.GetAnimation(name);
			string full = FighterModel.LibraryName + "/" + name;
			model.Player.Play(full);
			int frames = Math.Max(1, (int)Math.Ceiling(anim.Length * 60));
			for (int k = 0; k <= frames; k++)
			{
				model.Player.Seek(Math.Min(anim.Length, k / 60.0), true);
				var skel = model.Skeleton;
				var xf = skel.GlobalTransform;
				for (int b = 0; b < skel.GetBoneCount(); b++)
				{
					float x = (xf * skel.GetBoneGlobalPose(b)).Origin.X;
					if (x > front) { front = x; fc = name; }
					if (-x > back) { back = -x; bc = name; }
				}
				foreach (var mi in model.FindChildren("Tail*", "Node3D", true, false))
					foreach (var m in ((Node)mi).FindChildren("*", "MeshInstance3D", true, false))
					{
						var mesh = (MeshInstance3D)m;
						var box = mesh.GlobalTransform * mesh.GetAabb();
						if (box.End.X > front) { front = box.End.X; fc = name + " (tails)"; }
						if (-box.Position.X > back) { back = -box.Position.X; bc = name + " (tails)"; }
					}
			}
		}
		const float unitsPerMetre = 200f; // E1
		return ((int)Math.Ceiling(front * unitsPerMetre) + FleshPad, (int)Math.Ceiling(back * unitsPerMetre) + FleshPad, fc, bc);
	}

	[Test]
	public static void ModelReach_FitsTheCameraMargins(Node runner)
	{
		var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		scene.ExternalDrive = true;
		runner.AddChild(scene);
		var c = scene.Match.Config;
		for (int i = 0; i < 2; i++)
		{
			var model = scene.ModelOf(i);
			if (model is null) continue; // no model: the capsule (BodyWidth) is what shows
			var (front, back, fc, bc) = Measure(model);
			GD.Print($"YOK-39 reach {model.Name}: front {front} ({fc}), back {back} ({bc})");
			Assert.True(front * SimConfig.Scale <= c.ModelFrontReach, $"{model.Name}: front reach {front} ({fc}) > {c.ModelFrontReach / SimConfig.Scale}");
			Assert.True(back * SimConfig.Scale <= c.ModelBackReach, $"{model.Name}: back reach {back} ({bc}) > {c.ModelBackReach / SimConfig.Scale}");
		}
		scene.QueueFree();
	}
}
