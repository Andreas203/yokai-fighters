using System;
using System.Linq;
using Godot;
using YokaiFighters.Fight;

namespace YokaiFighters.Tests;

/// <summary>YOK-130 probes: hip height scaling edge cases (presentation only; expectations from the documented contract in FighterModel.HipScale).</summary>
public static class HipScaleProbes
{
	static ClipCatalog Cat => FightScene.Catalog;

	static FighterModel Synthetic(float bones)
	{
		var model = GD.Load<PackedScene>(FighterAnimSet.Ryo.ScenePath).Instantiate<FighterModel>();
		var skel = (Skeleton3D)model.FindChild("Skeleton3D", true, false)!;
		for (int b = 0; b < skel.GetBoneCount(); b++)
		{
			var r = skel.GetBoneRest(b);
			skel.SetBoneRest(b, new Transform3D(r.Basis, r.Origin * bones));
		}
		skel.ResetBonePoses();
		Assert.True(model.Setup(FighterAnimSet.Ryo, Cat), "setup");
		return model;
	}

	static Animation Clip(FighterModel m, string id) => m.Player.GetAnimation(FighterModel.LibraryName + "/" + id);

	static (float Min, float Max) HipY(Animation a)
	{
		float mn = float.MaxValue, mx = float.MinValue;
		for (int t = 0; t < a.GetTrackCount(); t++)
		{
			if (a.TrackGetType(t) != Animation.TrackType.Position3D) continue;
			for (int k = 0; k < a.TrackGetKeyCount(t); k++)
			{
				float y = ((Vector3)a.TrackGetKeyValue(t, k)).Y;
				mn = Math.Min(mn, y); mx = Math.Max(mx, y);
			}
		}
		return (mn, mx);
	}

	// Contract (HipScale doc): 1 when either rest is unknown. NaN is unknown; a NaN scale would poison every hips key.
	[Test]
	public static void NaNRest_DoesNotProduceNaNScale()
	{
		Assert.True(!float.IsNaN(FighterModel.HipScale(float.NaN, 94.9f)), "NaN rig rest gives a finite scale");
		Assert.True(!float.IsNaN(FighterModel.HipScale(94.9f, float.NaN)), "NaN clip rest gives a finite scale");
	}

	[Test]
	public static void NegativeAndInfiniteRest_AreOne()
	{
		Assert.Equal(1f, FighterModel.HipScale(-94.9f, 94.9f), "negative rig rest");
		Assert.Equal(1f, FighterModel.HipScale(94.9f, -1f), "negative clip rest");
		Assert.True(!float.IsInfinity(FighterModel.HipScale(float.PositiveInfinity, 94.9f)), "infinite rig rest gives a finite scale");
	}

	// Snap: within 0.015 of 1 -> exactly 1; past it -> the real ratio.
	[Test]
	public static void SnapBoundary_InsideSnaps_PastKeepsRatio()
	{
		Assert.Equal(1f, FighterModel.HipScale(100f * 1.014f, 100f), "1.014 snaps");
		Assert.Equal(1f, FighterModel.HipScale(100f * 0.986f, 100f), "0.986 snaps");
		float up = FighterModel.HipScale(101.6f, 100f), dn = FighterModel.HipScale(98.4f, 100f);
		Assert.True(Math.Abs(up - 1.016f) < 1e-4f, $"1.016 kept, got {up}");
		Assert.True(Math.Abs(dn - 0.984f) < 1e-4f, $"0.984 kept, got {dn}");
	}

	// cm vs m: a rig in metres (0.949) on a cm clip (94.9) gives ratio 0.01. The contract is a pure ratio, so the
	// scale is 0.01 and hips collapse; this documents it. Expectation: not 1 and not NaN (a units mismatch is visible, not hidden).
	[Test]
	public static void UnitsMismatch_IsTheRawRatio()
	{
		float k = FighterModel.HipScale(0.949f, 94.9f);
		Assert.True(Math.Abs(k - 0.01f) < 1e-4f, $"ratio {k}");
	}

	// Same id, different rigs: each gets its own library (cache key includes rest hips).
	[Test]
	public static void SameFighterId_DifferentRigs_DoNotShareLibrary(Node runner)
	{
		var a = Synthetic(1.19f); var b = Synthetic(0.77f);
		float ha = HipY(Clip(a, "ryo-idle-guard")).Max, hb = HipY(Clip(b, "ryo-idle-guard")).Max;
		Assert.True(ha > hb * 1.4f, $"tall rig hips {ha:0.000} well above short rig {hb:0.000}");
		var ryo = FighterModel.Create(FighterAnimSet.Ryo, Cat)!;
		float hr = HipY(Clip(ryo, "ryo-idle-guard")).Max;
		Assert.True(Math.Abs(ha / hr - 1.19f) < 0.01f && Math.Abs(hb / hr - 0.77f) < 0.01f, "ratios to Ryo");
		a.Free(); b.Free(); ryo.Free();
	}

	// Same rig twice: library is reused (same instance), and a scaled build does not mutate the shared source.
	[Test]
	public static void SameRigTwice_ReusesLibrary_AndSourceStaysUnscaled(Node runner)
	{
		var r1 = FighterModel.Create(FighterAnimSet.Ryo, Cat)!;
		float before = HipY(Clip(r1, "ryo-idle-guard")).Max;
		var s1 = Synthetic(1.19f); var s2 = Synthetic(1.19f);
		Assert.True(ReferenceEquals(s1.Player.GetAnimationLibrary(FighterModel.LibraryName), s2.Player.GetAnimationLibrary(FighterModel.LibraryName)), "same rig reuses cached library");
		var r2 = FighterModel.Create(FighterAnimSet.Ryo, Cat)!;
		Assert.Equal(before, HipY(Clip(r2, "ryo-idle-guard")).Max, "Ryo hips unchanged after scaled builds");
		r1.Free(); r2.Free(); s1.Free(); s2.Free();
	}

	// Air clips: capped at rest Y after scaling, on both a tall and a short rig; ground clips are not capped.
	[Test]
	public static void AirClips_CappedAtRigRest_GroundClipsNot(Node runner)
	{
		foreach (float k in new[] { 0.77f, 1.19f })
		{
			var m = Synthetic(k);
			float rest = m.Skeleton.GetBoneRest(m.Skeleton.FindBone("Hips")).Origin.Y;
			int air = 0;
			foreach (var id in FighterAnimSet.Ryo.AirClips)
			{
				if (!Cat.Clips.ContainsKey(id)) continue;
				air++;
				Assert.True(HipY(Clip(m, id)).Max <= rest + 1e-4f, $"x{k} {id}: hips max {HipY(Clip(m, id)).Max:0.000} <= rest {rest:0.000}");
			}
			Assert.True(air > 0, "at least one air clip checked");
			var ground = Cat.Clips.Keys.Where(c => !FighterAnimSet.Ryo.AirClips.Contains(c)).Select(c => HipY(Clip(m, c)).Max).Max();
			Assert.True(ground > 0f, "ground clips still carry hips keys");
			m.Free();
		}
	}

	// Air cap must not flatten a jump's crouch: air-clip minimum on a scaled rig is the scaled minimum (not clamped up).
	[Test]
	public static void AirClips_KeepScaledCrouch(Node runner)
	{
		var ryo = FighterModel.Create(FighterAnimSet.Ryo, Cat)!;
		var tall = Synthetic(1.19f);
		foreach (var id in FighterAnimSet.Ryo.AirClips.Where(Cat.Clips.ContainsKey))
		{
			float rMin = HipY(Clip(ryo, id)).Min, tMin = HipY(Clip(tall, id)).Min;
			float ratio = tall.HipScaleFor(Cat[id]!) / ryo.HipScaleFor(Cat[id]!);
			Assert.True(Math.Abs(tMin - rMin * ratio) < 0.01f * Math.Max(1f, Math.Abs(rMin)), $"{id}: min {tMin:0.000} = {ratio:0.000} x {rMin:0.000}");
		}
		ryo.Free(); tall.Free();
	}

	// Hips X/Z are the rig's rest on every key of a scaled rig.
	[Test]
	public static void HipsXZ_AreRigRest_OnScaledRig(Node runner)
	{
		var m = Synthetic(1.19f);
		var rest = m.Skeleton.GetBoneRest(m.Skeleton.FindBone("Hips")).Origin;
		var a = Clip(m, "ryo-walk-guard-fwd");
		for (int t = 0; t < a.GetTrackCount(); t++)
		{
			if (a.TrackGetType(t) != Animation.TrackType.Position3D) continue;
			for (int k = 0; k < a.TrackGetKeyCount(t); k++)
			{
				var p = (Vector3)a.TrackGetKeyValue(t, k);
				Assert.True(Math.Abs(p.X - rest.X) < 1e-5f && Math.Abs(p.Z - rest.Z) < 1e-5f, "hips X/Z at rig rest");
			}
		}
		m.Free();
	}

	// Seek determinism: same tick twice (with a different tick between) gives the same pose.
	[Test]
	public static void Seek_SameTickTwice_SamePose(Node runner)
	{
		var m = Synthetic(1.19f);
		runner.AddChild(m);
		int hips = m.Skeleton.FindBone("Hips");
		m.Player.Play(FighterModel.LibraryName + "/ryo-walk-guard-fwd");
		m.Player.Seek(0.25, true);
		var p1 = m.Skeleton.GetBoneGlobalPose(hips).Origin;
		m.Player.Seek(0.5, true);
		m.Player.Seek(0.25, true);
		var p2 = m.Skeleton.GetBoneGlobalPose(hips).Origin;
		Assert.True(p1 == p2, $"pose at 0.25 s identical ({p1} vs {p2})");
		m.QueueFree();
	}

	// Round 2: guard edges.
	[Test]
	public static void NegativeZero_AndDenormalClipRest_Guarded()
	{
		Assert.Equal(1f, FighterModel.HipScale(-0f, 94.9f), "-0 rig rest is unknown");
		Assert.Equal(1f, FighterModel.HipScale(94.9f, -0f), "-0 clip rest is unknown");
		float k = FighterModel.HipScale(94.9f, float.Epsilon); // ratio overflows to +inf
		Assert.True(float.IsFinite(k), "denormal clip rest gives a finite scale");
	}

	// A denormal rig rest is positive and finite, so the ratio underflows to 0 and would flatten every hips key to 0.
	// A scale of 0 is not a body size; expected: treated as unknown (1).
	[Test]
	public static void DenormalRigRest_DoesNotCollapseHipsToZero()
	{
		float k = FighterModel.HipScale(float.Epsilon, 94.9f);
		Assert.True(k > 0f, $"scale {k} must be above 0");
	}

	[Test]
	public static void MaxValueRest_GivesFiniteScale()
	{
		Assert.True(float.IsFinite(FighterModel.HipScale(float.MaxValue, 94.9f)), "MaxValue rig rest");
		Assert.True(float.IsFinite(FighterModel.HipScale(94.9f, float.MaxValue)), "MaxValue clip rest");
		Assert.True(float.IsFinite(FighterModel.HipScale(float.MaxValue, float.Epsilon)), "overflowing ratio");
	}

	[Test]
	public static void WarnBandEdges_AreAppliedRaw()
	{
		Assert.True(Math.Abs(FighterModel.HipScale(30f, 100f) - 0.3f) < 1e-5f, "0.3 applied");
		Assert.True(Math.Abs(FighterModel.HipScale(300f, 100f) - 3f) < 1e-5f, "3 applied");
		Assert.True(Math.Abs(FighterModel.HipScale(1f, 100f) - 0.01f) < 1e-6f, "out-of-band still applied, not clamped");
	}

	// Extreme rig builds and its keys stay finite (the warning is per clip in BuildLibrary, outside the key loop).
	[Test]
	public static void ExtremeRig_BuildsWithFiniteKeys(Node runner)
	{
		var m = Synthetic(10f);
		var (mn, mx) = HipY(Clip(m, "ryo-idle-guard"));
		Assert.True(float.IsFinite(mn) && float.IsFinite(mx), "finite keys");
		m.Free();
	}
}
