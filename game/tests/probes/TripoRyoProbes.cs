using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>YOK-146 probes: Tripo/Meshy Ryo switch, clip ownership per skeleton, hip scale, determinism, skinned reach.
/// Expectations come from the branch contract (FighterAnimSet.ClipRoot: a body never plays another skeleton's file;
/// HipScale: same-size rigs keep ratio 1), F3 determinism, and SimConfig.ModelFrontReach.</summary>
public static class TripoRyoProbes
{
	static ClipCatalog Cat => FightScene.Catalog;

	static void AllTracksResolve(FighterModel m, string who)
	{
		var lib = m.Player.GetAnimationLibrary(FighterModel.LibraryName);
		foreach (var name in lib.GetAnimationList())
		{
			var a = lib.GetAnimation(name);
			for (int t = 0; t < a.GetTrackCount(); t++)
			{
				string bone = a.TrackGetPath(t).GetConcatenatedSubNames();
				Assert.True(m.Skeleton.FindBone(bone) >= 0, $"{who}: clip {name} animates bone '{bone}' the skeleton lacks (another skeleton's file)");
			}
		}
	}

	// Contract: ClipRoot keeps skeletons apart, whatever order the toggle flips (cache key includes ClipRoot).
	[Test]
	public static void ToggleFlipFlop_EveryLibraryTrackBelongsToItsOwnSkeleton(Node runner)
	{
		try
		{
			for (int round = 0; round < 3; round++)
			{
				foreach (bool tripo in new[] { true, false, false, true })
				{
					FighterAnimSet.TripoRyo = tripo;
					var m = FighterModel.Create(FighterAnimSet.Ryo, Cat)!;
					Assert.Equal(tripo ? 25 : 24, m.Skeleton.GetBoneCount(), $"round {round} tripo={tripo}: bone count");
					AllTracksResolve(m, $"Ryo tripo={tripo}");
					float hip = m.Skeleton.GetBoneRest(m.Skeleton.FindBone("Hips")).Origin.Y;
					Assert.True(tripo ? hip < 2f : hip > 20f, $"tripo={tripo}: hip rest {hip} in the units of its own rig");
					m.Free();
				}
			}
		}
		finally { FighterAnimSet.TripoRyo = true; }
	}

	// Mixed match: Ryo on Tripo while Kitsune/Oni/Kappa borrow ryo-* clips: each keeps the Meshy file.
	[Test]
	public static void MixedMatch_BorrowedRyoClips_StayOnTheBorrowersSkeleton(Node runner)
	{
		var ryo = FighterModel.Create(FighterAnimSet.RyoTripo, Cat)!;
		foreach (var set in new[] { FighterAnimSet.Kitsune, FighterAnimSet.Oni, FighterAnimSet.Kappa })
		{
			var m = FighterModel.Create(set, Cat)!;
			AllTracksResolve(m, set.Fighter);
			foreach (var id in set.Clips.Values.Concat(set.ClipOverrides.Values).Distinct())
				Assert.True(m.Has(id), $"{set.Fighter} lost clip {id} after a Tripo Ryo was built");
			m.Free();
		}
		AllTracksResolve(ryo, "Tripo Ryo (after Meshy bodies)");
		ryo.Free();
	}

	// Mirror match with one Meshy and one Tripo Ryo (toggle flipped between the two builds).
	[Test]
	public static void MirrorMatch_OneMeshyOneTripo_KeepDistinctLibraries(Node runner)
	{
		try
		{
			FighterAnimSet.TripoRyo = true; var a = FighterModel.Create(FighterAnimSet.Ryo, Cat)!;
			FighterAnimSet.TripoRyo = false; var b = FighterModel.Create(FighterAnimSet.Ryo, Cat)!;
			var la = a.Player.GetAnimationLibrary(FighterModel.LibraryName);
			var lb = b.Player.GetAnimationLibrary(FighterModel.LibraryName);
			Assert.True(!ReferenceEquals(la, lb), "the two Ryo bodies share one library");
			AllTracksResolve(a, "P1 tripo"); AllTracksResolve(b, "P2 meshy");
			Assert.True(a.Has("ryo-idle-guard") && b.Has("ryo-idle-guard"), "idle on both");
			a.Free(); b.Free();
		}
		finally { FighterAnimSet.TripoRyo = true; }
	}

	// Tanuki (T0 = Ryo's kit) plus any copied special: every move clip it can be asked for plays on Tripo.
	// Other fighters' normals (kitsune-, oni-) are not in Ryo's or the Tanuki's kit and are skipped.
	[Test]
	public static void TanukiAndRyo_EveryKitAndSpecialMoveClip_PlaysOnTheTripoSet(Node runner)
	{
		var set = FighterAnimSet.For("tanuki");
		Assert.True(ReferenceEquals(set, FighterAnimSet.RyoTripo), "tanuki body is the Tripo Ryo");
		var missing = new List<string>();
		foreach (var (moveId, clip) in FightScene.MoveClips)
		{
			if (moveId.StartsWith("kitsune-") || moveId.StartsWith("oni-")) continue;
			string c = set.MoveClip(clip);
			if (!set.CanPlay(Cat[c])) missing.Add($"{moveId}->{c}");
		}
		Assert.True(missing.Count == 0, "no Tripo twin (presenter falls back to light punch): " + string.Join(", ", missing));
	}

	// Every clip id catalogued as ryo-* has a Tripo twin file.
	[Test]
	public static void EveryRyoCatalogClip_HasATripoTwin(Node runner)
	{
		var missing = Cat.Clips.Where(kv => kv.Key.StartsWith("ryo-"))
			.Where(kv => !FighterAnimSet.RyoTripo.CanPlay(kv.Value)).Select(kv => kv.Key).ToList();
		Assert.True(missing.Count == 0, "ryo-* clips without a Tripo twin: " + string.Join(", ", missing));
	}

	// Every PoseKind of Ryo maps to a clip that plays on the Tripo body.
	[Test]
	public static void EveryRyoPose_HasATripoClip(Node runner)
	{
		var set = FighterAnimSet.RyoTripo;
		foreach (PoseKind k in Enum.GetValues<PoseKind>())
		{
			string? id = set.ClipFor(k);
			Assert.True(id != null && set.CanPlay(Cat[id]), $"pose {k} -> {id} cannot play on Tripo");
		}
	}

	// Hip scale 1 and hips Y keys near rest on every Tripo clip; air clips never above rest.
	[Test]
	public static void TripoClips_HipKeysStayNearRest_AirClipsNeverAboveRest(Node runner)
	{
		var set = FighterAnimSet.RyoTripo;
		var m = FighterModel.Create(set, Cat)!;
		float rest = m.Skeleton.GetBoneRest(m.Skeleton.FindBone("Hips")).Origin.Y;
		var lib = m.Player.GetAnimationLibrary(FighterModel.LibraryName);
		foreach (var name in lib.GetAnimationList())
		{
			var a = lib.GetAnimation(name);
			bool air = set.AirClips.Contains(name.ToString());
			for (int t = 0; t < a.GetTrackCount(); t++)
			{
				if (a.TrackGetType(t) != Animation.TrackType.Position3D) continue;
				for (int k = 0; k < a.TrackGetKeyCount(t); k++)
				{
					float y = ((Vector3)a.TrackGetKeyValue(t, k)).Y;
					bool lying = name.ToString().Contains("knockdown") || name.ToString().Contains("get-up"); Assert.True((lying || y > 0.2f * rest) && y < 1.3f * rest, $"{name} key {k}: hips Y {y} vs rest {rest}");
					if (air) Assert.True(y <= rest + 1e-5f, $"{name} key {k}: air clip hips {y} above rest {rest}");
				}
			}
			Assert.Equal(1f, m.HipScaleFor(Cat[name.ToString()]!), $"{name} hip scale");
		}
		m.Free();
	}

	// Feet stay near the floor: lowest foot/toe bone every 4th frame of each upright Tripo clip not below -5 cm.
	[Test]
	public static void TripoClips_FeetDoNotSinkThroughTheFloor(Node runner)
	{
		var m = FighterModel.Create(FighterAnimSet.RyoTripo, Cat)!;
		m.Position = Vector3.Zero; m.Mirror.Scale = Vector3.One;
		var sk = m.Skeleton;
		var feet = new List<int>();
		for (int b = 0; b < sk.GetBoneCount(); b++) { var n = sk.GetBoneName(b); if (n.Contains("Foot") || n.Contains("Toe")) feet.Add(b); }
		Assert.True(feet.Count > 0, "foot bones");
		var lib = m.Player.GetAnimationLibrary(FighterModel.LibraryName);
		var worst = new List<string>();
		foreach (var name in lib.GetAnimationList())
		{
			var a = lib.GetAnimation(name);
			string n = name.ToString();
			if (n.Contains("knockdown") || n.Contains("get-up")) continue;
			m.Player.Play(FighterModel.LibraryName + "/" + name);
			float low = float.MaxValue;
			int frames = Math.Max(1, (int)Math.Ceiling(a.Length * 60));
			for (int k = 0; k <= frames; k += 4)
			{
				m.Player.Seek(Math.Min(a.Length, k / 60.0), true);
				foreach (int b in feet) low = Math.Min(low, (sk.GlobalTransform * sk.GetBoneGlobalPose(b)).Origin.Y);
			}
			if (low < -0.05f) worst.Add($"{name} {low:0.000}");
		}
		m.Free();
		Assert.True(worst.Count == 0, "feet below floor (m): " + string.Join(", ", worst));
	}

	// Determinism (F3): the presentation switch must not touch the sim.
	[Test]
	public static void StateHash_IsTheSame_TripoOrMeshy(Node runner)
	{
		ulong Run(bool tripo)
		{
			FighterAnimSet.TripoRyo = tripo;
			var kit = FightScene.LoadMoves();
			var m = new Match(null, kit, kit);
			var rng = new SimRng(9);
			var all = new[] { InputBits.LightPunch, InputBits.MediumKick, InputBits.HeavyPunch, InputBits.Right, InputBits.Left, InputBits.Up, InputBits.Down };
			for (int t = 0; t < 1500 && m.Phase != MatchPhase.Over; t++)
				m.Step(new FighterInput(all[rng.Next(all.Length)]), new FighterInput(all[rng.Next(all.Length)]));
			return m.StateHash();
		}
		try
		{
			ulong a = Run(true), b = Run(false), c = Run(true);
			Assert.Equal(a, b, "hash with TripoRyo true vs false");
			Assert.Equal(a, c, "hash repeated");
		}
		finally { FighterAnimSet.TripoRyo = true; }
	}
}
