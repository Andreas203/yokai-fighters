using System;
using System.Collections.Generic;
using Godot;
using YokaiFighters.Fight;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-finish-qa: the per-body hips offset (Fix B: the Oni and Kappa stood sunk into the floor on clips cut for Ryo's
/// proportions) and the lying lift curve (Fix D: the Tanuki's belly and tail, and the Kappa's legs, sank through the floor
/// on the borrowed knockdown and get-up). Both are presentation only: the sim never reads them (F3). The floor numbers
/// themselves are measured on the skinned mesh by tests/captures/FinishQaCapture.cs (assets/specs/rigs/finish-qa.md).
/// </summary>
public static class HipOffsetTests
{
	static ClipCatalog Cat => FightScene.Catalog;

	/// <summary>The same body table without its offsets (a separate instance, so it builds its own library).</summary>
	static FighterAnimSet Bare(FighterAnimSet s) => new()
	{
		Fighter = s.Fighter, ModelPath = s.ModelPath, ScenePath = s.ScenePath, ClipRoot = s.ClipRoot, Clips = s.Clips,
		ClipOverrides = s.ClipOverrides, AirClips = s.AirClips, ReversedPoses = s.ReversedPoses,
		JumpFrameOffset = s.JumpFrameOffset, LandingFrame = s.LandingFrame, BurstFrame = s.BurstFrame,
	};

	/// <summary>World hips height of <paramref name="set"/>'s body on one clip frame.</summary>
	static float HipsY(Node runner, FighterAnimSet set, string clip, int frame)
	{
		var m = FighterModel.Create(set, Cat)!;
		runner.AddChild(m);
		m.Position = Vector3.Zero;
		m.Apply(new PoseSample(PoseKind.Attack, clip, frame, null, 0, 1f), Cat, 1, frame);
		int h = m.Skeleton.FindBone("Hips");
		float y = (m.Skeleton.GlobalTransform * m.Skeleton.GetBoneGlobalPose(h)).Origin.Y;
		runner.RemoveChild(m);
		m.Free();
		return y;
	}

	[Test]
	public static void LiftAt_IsLinearBetweenPoints_AndHeldPastTheEnds()
	{
		var clip = new ClipInfo("t", "", 0, 10_000, 1.0, 100);
		var curve = new[] { new HipLift(10, 0.1f), new HipLift(20, 0.3f) };
		Assert.True(Math.Abs(FighterModel.LiftAt(curve, clip, clip.TimeAt(5)) - 0.1f) < 1e-5f, "held before the first point");
		Assert.True(Math.Abs(FighterModel.LiftAt(curve, clip, clip.TimeAt(15)) - 0.2f) < 1e-5f, "linear between points");
		Assert.True(Math.Abs(FighterModel.LiftAt(curve, clip, clip.TimeAt(40)) - 0.3f) < 1e-5f, "held past the last point");
		Assert.Equal(0f, FighterModel.LiftAt(Array.Empty<HipLift>(), clip, 0.5), "an empty curve lifts nothing");
	}

	[Test]
	public static void RyoAndKitsune_KeepTheirClipsHipsUntouched()
	{
		foreach (var s in new[] { FighterAnimSet.RyoTripo, FighterAnimSet.RyoMeshy, FighterAnimSet.Kitsune })
		{
			Assert.Equal(0f, s.HipOffsetY, $"{s.Fighter}: no body offset");
			// YOK-246: the Kitsune's two upright Tripo takes carry a per-clip lowering; so does the Tripo Ryo's box_01
			// Iron-Club Charge (YOK-250). Their other clips and the Meshy Ryo's stay untouched.
			int expected = s == FighterAnimSet.Kitsune ? 2 : s == FighterAnimSet.RyoTripo ? 1 : 0;
			Assert.Equal(expected, s.ClipHipOffsetY.Count, $"{s.Fighter}: clip offsets are only the Tripo takes'");
			if (s == FighterAnimSet.RyoTripo)
				Assert.True(s.ClipHipOffsetY.ContainsKey("ryo-iron-club-charge"), "ryo: the offset is the charge's");
			Assert.Equal(0, s.LyingHipLift.Count, $"{s.Fighter}: no lying lift");
		}
	}

	[Test]
	public static void LyingLift_PosesSharingAClipShareItsCurve()
	{
		foreach (var s in new[] { FighterAnimSet.TanukiTripo, FighterAnimSet.Kappa, FighterAnimSet.Oni })
			foreach (var (kind, curve) in s.LyingHipLift)
			{
				Assert.True(kind is PoseKind.Knockdown or PoseKind.GetUp or PoseKind.Ko, $"{s.Fighter}: lift only on lying poses, not {kind}");
				foreach (var (other, c2) in s.LyingHipLift)
					if (s.ClipFor(other) == s.ClipFor(kind))
						Assert.True(ReferenceEquals(curve, c2), $"{s.Fighter}: {kind} and {other} share clip {s.ClipFor(kind)} but not its curve");
				for (int i = 1; i < curve.Length; i++)
					Assert.True(curve[i].Frame > curve[i - 1].Frame, $"{s.Fighter} {kind}: curve frames ascend");
			}
	}

	[Test]
	public static void Oni_HipsRaisedByHisOffset_InMetres(Node runner)
	{
		var oni = FighterAnimSet.Oni; var bare = Bare(oni);
		foreach (var (clip, want) in new[] { ("oni-idle", oni.HipOffsetY), ("oni-heavy-punch", oni.HipOffsetFor("oni-heavy-punch")) })
		{
			float d = HipsY(runner, oni, clip, 1) - HipsY(runner, bare, clip, 1);
			Assert.True(Math.Abs(d - want) < 2e-3f, $"oni {clip}: hips raised {d:0.0000} m, want {want:0.000}");
		}
		Assert.True(oni.HipOffsetY > oni.HipOffsetFor("oni-heavy-punch"), "his own slam take is raised less than the body offset");
	}

	[Test]
	public static void Tanuki_GetUpLifted_WhileLying_NotOnceStanding(Node runner)
	{
		var tan = FighterAnimSet.TanukiTripo; var bare = Bare(tan);
		string getUp = tan.ClipFor(PoseKind.GetUp)!;
		float lying = HipsY(runner, tan, getUp, 13) - HipsY(runner, bare, getUp, 13);
		Assert.True(Math.Abs(lying - 0.43f) < 0.01f, $"tanuki {getUp} f13: lifted {lying:0.000} m, want 0.43 (measured sink 42.4 cm)");
		int last = Cat[getUp]!.FramesTotal;
		float standing = HipsY(runner, tan, getUp, last) - HipsY(runner, bare, getUp, last);
		Assert.True(Math.Abs(standing) < 1e-3f, $"tanuki {getUp} f{last}: standing again, lift {standing:0.0000}");
		string idle = tan.ClipFor(PoseKind.Idle)!;
		Assert.True(Math.Abs(HipsY(runner, tan, idle, 1) - HipsY(runner, bare, idle, 1)) < 1e-4f, "his idle is not lifted");
	}
}
