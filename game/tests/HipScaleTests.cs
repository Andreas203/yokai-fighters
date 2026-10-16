using System;
using Godot;
using YokaiFighters.Fight;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-130: shared clips keep their hips height relative to the rig. A clip's <c>Hips</c> Y keys are scaled by
/// (this rig's rest hips Y) / (the clip rig's rest hips Y), so a taller or shorter rig keeps its toes on the floor.
/// The test rigs are synthetic (Ryo's rig with every bone rest scaled, optionally with the Oni's 1.1 on the Armature
/// node) so this branch stands without the Oni and Kappa rigs.
/// </summary>
public static class HipScaleTests
{
	static ClipCatalog Cat => FightScene.Catalog;
	const string Idle = "ryo-idle-guard", Walk = "ryo-walk-guard-fwd";

	[Test]
	public static void HipScale_IsRigRestOverClipRest_AndOneWhenUnknown()
	{
		// The rig specs' measured rests (bone space, cm): Oni 112.8, Kappa 73.5, Ryo 94.9.
		Assert.True(Math.Abs(FighterModel.HipScale(112.8f, 94.9f) - 1.19f) < 0.005f, "Oni about 1.19");
		Assert.True(Math.Abs(FighterModel.HipScale(73.5f, 94.9f) - 0.77f) < 0.005f, "Kappa about 0.77");
		Assert.Equal(1f, FighterModel.HipScale(94.9f, 94.9f), "Ryo on his own clips");
		Assert.Equal(1f, FighterModel.HipScale(94.9f, 0f), "no clip skeleton: unchanged");
		Assert.Equal(1f, FighterModel.HipScale(0f, 94.9f), "no rig hips: unchanged");
	}

	[Test]
	public static void HipScale_RyoAndKitsuneAreAboutOne(Node runner)
	{
		foreach (var set in new[] { FighterAnimSet.Ryo, FighterAnimSet.Kitsune })
		{
			var model = FighterModel.Create(set, Cat);
			Assert.True(model != null, $"{set.Fighter} loads");
			foreach (var (id, info) in Cat.Clips)
			{
				float k = model!.HipScaleFor(info);
				Assert.True(Math.Abs(k - 1f) < 0.02f, $"{set.Fighter} on {id}: hip scale {k:0.000} is about 1");
			}
			model!.Free();
		}
	}

	[Test]
	public static void HipScale_IsReadFromTheRig_NotTheNodeScale(Node runner)
	{
		var ryo = FighterModel.Create(FighterAnimSet.Ryo, Cat)!;
		float baseK = ryo.HipScaleFor(Cat[Walk]!);
		ryo.Free();
		foreach (var (k, node) in new[] { (1.19f, 1f), (1.19f, 1.1f), (0.77f, 1f) })
		{
			var model = Synthetic(k, node);
			float got = model.HipScaleFor(Cat[Walk]!);
			Assert.True(Math.Abs(got - k * baseK) < 1e-3f, $"bones x{k}, node x{node}: hip scale {got:0.000} = {k * baseK:0.000}");
			model.Free();
		}
	}

	[Test]
	public static void ScaledRig_KeepsItsToesOnTheFloor_AtRestAndThroughTheWalk(Node runner)
	{
		var ryo = FighterModel.Create(FighterAnimSet.Ryo, Cat)!;
		runner.AddChild(ryo);
		foreach (var (k, node) in new[] { (1.19f, 1f), (1.19f, 1.1f), (0.77f, 1f) })
		{
			var model = Synthetic(k, node);
			runner.AddChild(model);
			float s = k * node; // the whole body is this much bigger, so the toes are too
			foreach (var clip in new[] { Idle, Walk })
			{
				var anim = ryo.Player.GetAnimation(FighterModel.LibraryName + "/" + clip);
				int frames = Math.Max(1, (int)Math.Ceiling(anim.Length * 60));
				float worst = 0f, low = float.MaxValue, ryoLow = float.MaxValue;
				for (int f = 0; f <= frames; f++)
				{
					double t = Math.Min(anim.Length, f / 60.0);
					float ryoToe = LowestToe(ryo, clip, t);
					float got = LowestToe(model, clip, t);
					worst = Math.Max(worst, Math.Abs(got - s * ryoToe));
					low = Math.Min(low, got);
					ryoLow = Math.Min(ryoLow, ryoToe);
				}
				GD.Print($"YOK-130 bones x{k}, node x{node}, {clip}: lowest toe {low:0.000} m (Ryo {ryoLow:0.000} m), worst gap to scaled Ryo {worst:0.0000} m");
				// Every frame: the toes sit where Ryo's would on a body this much bigger (unscaled hips would miss by ~18 cm).
				Assert.True(worst < 0.01f, $"bones x{k}, node x{node}, {clip}: toes within 1 cm of the scaled Ryo's (worst {worst:0.0000} m)");
				// Ryo's own toe-joint height is the clip's floor contact; it never goes through the floor.
				Assert.True(low > -0.01f && Math.Abs(low - s * ryoLow) < 0.01f, $"bones x{k}, node x{node}, {clip}: lowest toe {low:0.000} m at Ryo's floor contact x{s} ({s * ryoLow:0.000} m)");
			}
			model.QueueFree();
		}
		ryo.QueueFree();
	}

	/// <summary>Ryo's scene with every bone rest translation scaled by <paramref name="bones"/> (a rig of other proportions,
	/// hips included) and the Armature node scaled by <paramref name="node"/> (like the Oni's 1.1), then set up.</summary>
	static FighterModel Synthetic(float bones, float node)
	{
		var model = GD.Load<PackedScene>(FighterAnimSet.Ryo.ScenePath).Instantiate<FighterModel>();
		var skel = (Skeleton3D)model.FindChild("Skeleton3D", true, false)!;
		for (int b = 0; b < skel.GetBoneCount(); b++)
		{
			var r = skel.GetBoneRest(b);
			skel.SetBoneRest(b, new Transform3D(r.Basis, r.Origin * bones));
		}
		skel.ResetBonePoses();
		if (skel.GetParent() is Node3D armature) armature.Scale *= node;
		Assert.True(model.Setup(FighterAnimSet.Ryo, Cat), "synthetic rig sets up");
		return model;
	}

	/// <summary>Lowest toe joint above the model's feet (metres) at time <paramref name="t"/> of a library clip.</summary>
	static float LowestToe(FighterModel model, string clip, double t)
	{
		model.Position = Vector3.Zero;
		model.Mirror.Scale = Vector3.One;
		model.Player.Play(FighterModel.LibraryName + "/" + clip);
		model.Player.Seek(t, true);
		var skel = model.Skeleton;
		float y = float.MaxValue;
		foreach (var bone in new[] { "LeftToeBase", "RightToeBase" })
		{
			int b = skel.FindBone(bone);
			Assert.True(b >= 0, $"{bone} on the rig");
			y = Math.Min(y, (skel.GlobalTransform * skel.GetBoneGlobalPose(b)).Origin.Y);
		}
		return y;
	}
}
