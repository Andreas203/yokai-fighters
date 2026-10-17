using System;
using System.Linq;
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

	/// <summary>YOK-102: the Oni's own club takes are cut on his 2.2 m rig (their GLBs live under an <c>oni/</c> folder) and
	/// only his body plays them; Ryo's and the Kitsune's library still loads every catalogued clip, so their checks skip these.</summary>
	public static bool OnOniRig(string? resPath) => resPath != null && resPath.Contains("/oni/");

	/// <summary>(front, back) reach in units for one model, facing +X, over every frame of every clip in its library.</summary>
	public static (int Front, int Back, string FrontClip, string BackClip) Measure(FighterModel model) => Measure(model, out _, out _);

	/// <summary>YOK-129: also the highest bone above the model's feet, in metres (no pad), over every frame of every clip.</summary>
	public static (int Front, int Back, string FrontClip, string BackClip) Measure(FighterModel model, out float top, out string topClip)
	{
		model.Position = Vector3.Zero;
		model.Mirror.Scale = Vector3.One;
		var lib = model.Player.GetAnimationLibrary(FighterModel.LibraryName);
		float front = 0, back = 0; string fc = "", bc = "";
		top = 0; topClip = "";
		foreach (var name in lib.GetAnimationList())
		{
			if (OnOniRig(YokaiFighters.Fight.FightScene.Catalog[name.ToString()]?.ResPath)) continue;
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
					var o = (xf * skel.GetBoneGlobalPose(b)).Origin;
					float x = o.X;
					if (o.Y > top) { top = o.Y; topClip = name; }
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

	/// <summary>Clips a body plays in a fight: its pose and move clips, plus every take cut on its own rig (the Oni's
	/// <c>oni/</c> folder, YOK-102). Ryo and the Kitsune keep the YOK-39 rule (every catalogued clip but the Oni's takes).</summary>
	public static bool PlaysClip(FighterAnimSet set, string clip)
	{
		bool onOni = OnOniRig(FightScene.Catalog[clip]?.ResPath);
		if (set.Fighter != "oni") return !onOni;
		return onOni || set.Clips.Values.Contains(clip) || set.ClipOverrides.Values.Contains(clip);
	}

	/// <summary>Highest Y (m, feet = 0) of the meshes under a node that pass <paramref name="keep"/>: the 8 corners of each mesh AABB, posed by its global transform.</summary>
	static float MeshTop(Node root, Func<MeshInstance3D, bool> keep)
	{
		float top = float.NegativeInfinity;
		foreach (var n in root.FindChildren("*", "MeshInstance3D", true, false))
		{
			var mesh = (MeshInstance3D)n;
			if (!keep(mesh)) continue;
			var box = mesh.GetAabb();
			// A skinned mesh's vertices are in bind space (the GLB armature's centimetres): place them through the skin's
			// first bind at the rest pose (exact for the rest pose the allowance is measured in).
			var xf = mesh.GlobalTransform;
			if (mesh.Skin is { } skin && skin.GetBindCount() > 0 && mesh.GetNodeOrNull<Skeleton3D>(mesh.Skeleton) is { } sk)
			{
				int bone = skin.GetBindBone(0) >= 0 ? skin.GetBindBone(0) : sk.FindBone(skin.GetBindName(0));
				if (bone >= 0) xf = sk.GlobalTransform * sk.GetBoneGlobalRest(bone) * skin.GetBindPose(0);
			}
			for (int c = 0; c < 8; c++) top = Math.Max(top, (xf * box.GetEndpoint(c)).Y);
		}
		return top;
	}

	/// <summary>Highest club point per clip from the last <see cref="MeasureTop"/> (m), for the printout.</summary>
	static readonly System.Collections.Generic.Dictionary<string, float> ClubTopByClip = new();

	static bool UnderClub(Node n)
	{
		for (var p = n.GetParent(); p != null; p = p.GetParent()) if (p.Name == "Club") return true;
		return false;
	}

	/// <summary>
	/// YOK-133: the highest point of a model over every clip it plays, in metres above its feet: the top bone plus the body
	/// mesh's reach past it (horns, hair: the skinned mesh's rest top minus the rest top bone, at least the 15 cm the HUD
	/// test always allowed), or the club tip (the club's mesh corners every frame; it rides the hand bone rigidly).
	/// </summary>
	public static (float Top, string Clip, float Bone, float Allowance, float Club, string ClubClip) MeasureTop(FighterModel model)
	{
		model.Position = Vector3.Zero;
		model.Mirror.Scale = Vector3.One;
		var skel = model.Skeleton;
		model.Player.Stop();
		skel.ResetBonePoses();
		float restBone = 0;
		for (int b = 0; b < skel.GetBoneCount(); b++) restBone = Math.Max(restBone, (skel.GlobalTransform * skel.GetBoneGlobalRest(b)).Origin.Y);
		float restMesh = MeshTop(skel, m => !UnderClub(m));
		float allowance = Math.Max(0.15f, restMesh - restBone);
		GD.Print($"YOK-133 rest {model.AnimSet.Fighter}: mesh top {restMesh:0.000} m, top bone {restBone:0.000} m");
		// The club rides the hand bone rigidly. BoneAttachment3D only follows the skeleton on its deferred update, so the
		// club is posed here from the bone itself: hand bone pose x (attachment -> club mesh), fixed for the clip.
		var attach = model.FindChildren("Club", "BoneAttachment3D", true, false).Cast<BoneAttachment3D>().FirstOrDefault();
		int hand = attach is null ? -1 : skel.FindBone(attach.BoneName);
		var clubMeshes = attach is null ? Array.Empty<(MeshInstance3D, Transform3D)>()
			: attach.FindChildren("*", "MeshInstance3D", true, false).Cast<MeshInstance3D>()
				.Select(m => (m, attach.GlobalTransform.AffineInverse() * m.GlobalTransform)).ToArray();
		var lib = model.Player.GetAnimationLibrary(FighterModel.LibraryName);
		float bone = 0, club = 0; string bc = "", cc = "";
		foreach (var name in lib.GetAnimationList())
		{
			if (!PlaysClip(model.AnimSet, name)) continue;
			var anim = lib.GetAnimation(name);
			model.Player.Play(FighterModel.LibraryName + "/" + name);
			int frames = Math.Max(1, (int)Math.Ceiling(anim.Length * 60));
			for (int k = 0; k <= frames; k++)
			{
				model.Player.Seek(Math.Min(anim.Length, k / 60.0), true);
				var xf = skel.GlobalTransform;
				for (int b = 0; b < skel.GetBoneCount(); b++)
				{
					float y = (xf * skel.GetBoneGlobalPose(b)).Origin.Y;
					if (y > bone) { bone = y; bc = name; }
				}
				if (hand < 0) continue;
				var handXf = xf * skel.GetBoneGlobalPose(hand);
				foreach (var (mesh, rel) in clubMeshes)
				{
					var box = mesh.GetAabb();
					for (int c = 0; c < 8; c++)
					{
						float y = (handXf * rel * box.GetEndpoint(c)).Y;
						if (y > club) { club = y; cc = name; }
						if (!ClubTopByClip.TryGetValue(name, out float was) || y > was) ClubTopByClip[name] = y;
					}
				}
			}
		}
		float body = bone + allowance;
		return club > body ? (club, cc + " (club)", bone, allowance, club, cc) : (body, bc, bone, allowance, club, cc);
	}

	/// <summary>YOK-133: every body's highest point (bones, body mesh, club tip) stays under the top the camera frames it for.</summary>
	[Test]
	public static void ModelTop_FitsTheCameraFraming(Node runner)
	{
		foreach (var set in new[] { FighterAnimSet.Ryo, FighterAnimSet.Kitsune, FighterAnimSet.Oni, FighterAnimSet.Kappa })
		{
			var model = FighterModel.Create(set, FightScene.Catalog);
			Assert.True(model != null, $"{set.Fighter}: model loads");
			if (model is null) continue;
			runner.AddChild(model);
			var (top, clip, bone, allow, club, cc) = MeasureTop(model);
			float framed = CameraConfig.Default.TopFor(set.Fighter);
			GD.Print($"YOK-133 top {set.Fighter}: {top:0.000} m ({clip}); bone {bone:0.000} + body {allow:0.000}, club {club:0.000} ({cc}); framed for {framed:0.000}");
			Assert.True(top <= framed, $"{set.Fighter}: highest point {top:0.000} m ({clip}) > framed top {framed:0.000}");
			if (set.Fighter == "oni")
				foreach (var (c, y) in ClubTopByClip.OrderByDescending(kv => kv.Value).Take(5))
					GD.Print($"YOK-133 oni club top {c}: {y:0.000} m");
			ClubTopByClip.Clear();
			if (set.Fighter == "oni") Assert.True(club > bone, $"oni: the club tip ({club:0.000} m) is measured and rises past the bones ({bone:0.000})");
			model.QueueFree();
		}
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
			var (front, back, fc, bc) = Measure(model, out float top, out string tc);
			GD.Print($"YOK-39 reach {model.Name}: front {front} ({fc}), back {back} ({bc}); YOK-129 top bone {top:0.000} m ({tc})");
			// YOK-129: the dynamic camera keeps the top bone + the HUD test's 15 cm hair allowance below the HUD band.
			Assert.True(top + 0.15f <= YokaiFighters.Fight.CameraConfig.Default.ModelTopMeters,
				$"{model.Name}: top bone {top:0.000} m + 0.15 > CameraConfig.ModelTopMeters {YokaiFighters.Fight.CameraConfig.Default.ModelTopMeters}");
			Assert.True(front * SimConfig.Scale <= c.ModelFrontReach, $"{model.Name}: front reach {front} ({fc}) > {c.ModelFrontReach / SimConfig.Scale}");
			Assert.True(back * SimConfig.Scale <= c.ModelBackReach, $"{model.Name}: back reach {back} ({bc}) > {c.ModelBackReach / SimConfig.Scale}");
		}
		scene.QueueFree();
	}
}
