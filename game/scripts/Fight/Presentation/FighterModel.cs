using System;
using System.Collections.Generic;
using Godot;

namespace YokaiFighters.Fight;

/// <summary>
/// YOK-53: a rigged fighter (Meshy humanoid) posed from a <see cref="PoseSample"/>. The AnimationPlayer never
/// advances on its own (manual callback mode): every render calls <c>Seek(t, true)</c> at the exact frame time.
/// Blends are computed from the tick (seek the source pose, capture, seek the target, slerp). Mirroring is
/// Scale.X = Facing on <see cref="Mirror"/>, like the hitbox data (mirrored by facing).
/// YOK-39: the body is a scene (<c>scenes/fighters/&lt;id&gt;.tscn</c>, path in <see cref="FighterAnimSet.ScenePath"/>)
/// edited in Godot: Mirror → Yaw → Model (the rigged GLB), the "Animator" AnimationPlayer (Manual, rooted at the
/// model), a <see cref="ToonLook"/>, and for the Kitsune "Tails" (a BoneAttachment3D on the hips) holding the placed
/// tails. Nodes are found by name; this script only adds the clip library and steps poses from the sim.
/// </summary>
public partial class FighterModel : Node3D
{
	public const string LibraryName = "yf";
	/// <summary>Node names the script looks up (the scene's contract).</summary>
	public const string MirrorName = "Mirror", PlayerName = "Animator", TailsName = "Tails";

	/// <summary>Fighter id this scene shows (matches <see cref="FighterAnimSet.Fighter"/>).</summary>
	[Export] public string Fighter { get; set; } = "";

	public AnimationPlayer Player { get; private set; } = null!;
	public Skeleton3D Skeleton { get; private set; } = null!;
	public Node3D Mirror { get; private set; } = null!;
	public FighterAnimSet AnimSet { get; private set; } = null!;
	private readonly List<Node3D> _tails = new();
	private int _hips = -1;

	/// <summary>Imported clip animations by GLB path, shared by every model in the process.</summary>
	private static readonly Dictionary<string, Animation?> SourceCache = new();
	/// <summary>Built (root-motion-stripped) library per fighter id.</summary>
	private static readonly Dictionary<string, AnimationLibrary> LibraryCache = new();

	/// <summary>Instances the fighter's scene and sets it up; null when the model or its skeleton can't load (the fight keeps the capsule).</summary>
	public static FighterModel? Create(FighterAnimSet set, ClipCatalog catalog)
	{
		if (!ResourceLoader.Exists(set.ModelPath) || !ResourceLoader.Exists(set.ScenePath)) return null;
		if (GD.Load<PackedScene>(set.ScenePath)?.Instantiate() is not FighterModel model) return null;
		if (model.Setup(set, catalog)) return model;
		model.Free();
		return null;
	}

	/// <summary>Binds the scene's nodes and adds the clip library; false when the scene has no skeleton or Animator.</summary>
	public bool Setup(FighterAnimSet set, ClipCatalog catalog)
	{
		var skel = FindChild("Skeleton3D", true, false) as Skeleton3D;
		var player = FindChild(PlayerName, true, false) as AnimationPlayer;
		var mirror = FindChild(MirrorName, true, false) as Node3D;
		if (skel is null || player is null || mirror is null) return false;
		AnimSet = set; Skeleton = skel; Player = player; Mirror = mirror;
		_hips = skel.FindBone("Hips");

		// F3: never self-advance (also set in the scene; enforced here because determinism depends on it).
		player.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Manual;
		player.Deterministic = true;
		if (!LibraryCache.TryGetValue(set.Fighter, out var lib))
		{
			lib = BuildLibrary(set, catalog, skel, _hips);
			LibraryCache[set.Fighter] = lib;
		}
		if (player.HasAnimationLibrary(LibraryName)) player.RemoveAnimationLibrary(LibraryName);
		player.AddAnimationLibrary(LibraryName, lib);

		_tails.Clear();
		if (FindChild(TailsName, true, false) is Node tails)
			foreach (Node t in tails.GetChildren())
				if (t is Node3D t3) _tails.Add(t3);
		SwayTails(0);
		return true;
	}

	/// <summary>
	/// Every catalogued clip onto this skeleton (one shared Meshy skeleton, so track paths match): rotations kept,
	/// per-bone translation and scale dropped (bones keep this rig's rest lengths), hips X/Z pinned to rest
	/// (root motion stripped: movement is move data), hips Y kept except above rest in air clips (the sim draws the arc).
	/// </summary>
	private static AnimationLibrary BuildLibrary(FighterAnimSet set, ClipCatalog catalog, Skeleton3D skel, int hips)
	{
		var lib = new AnimationLibrary();
		Vector3 rest = hips >= 0 ? skel.GetBoneRest(hips).Origin : Vector3.Zero;
		foreach (var (id, info) in catalog.Clips)
		{
			var src = Source(info.ResPath);
			if (src is null) continue;
			var anim = (Animation)src.Duplicate(true);
			anim.LoopMode = Animation.LoopModeEnum.None;
			bool air = set.AirClips.Contains(id);
			for (int t = anim.GetTrackCount() - 1; t >= 0; t--)
			{
				var type = anim.TrackGetType(t);
				string bone = anim.TrackGetPath(t).GetConcatenatedSubNames();
				if (type == Animation.TrackType.Scale3D || (type == Animation.TrackType.Position3D && bone != "Hips"))
				{
					anim.RemoveTrack(t);
					continue;
				}
				if (type != Animation.TrackType.Position3D) continue;
				for (int k = 0; k < anim.TrackGetKeyCount(t); k++)
				{
					var p = (Vector3)anim.TrackGetKeyValue(t, k);
					float y = air ? Mathf.Min(p.Y, rest.Y) : p.Y;
					anim.TrackSetKeyValue(t, k, new Vector3(rest.X, y, rest.Z));
				}
			}
			lib.AddAnimation(id, anim);
		}
		return lib;
	}

	private static Animation? Source(string path)
	{
		if (SourceCache.TryGetValue(path, out var a)) return a;
		Animation? found = null;
		if (ResourceLoader.Exists(path))
		{
			var node = GD.Load<PackedScene>(path).Instantiate();
			if (node.FindChild("AnimationPlayer", true, false) is AnimationPlayer ap)
				foreach (string name in ap.GetAnimationList())
				{
					var cand = ap.GetAnimation(name);
					if (cand.Length > 0.1f) { found = cand; break; } // skip a rig's one-key T-pose clip
				}
			node.Free();
		}
		SourceCache[path] = found;
		return found;
	}

	/// <summary>Poses the body: exact clip frames, blended from the previous clip's frame when the sample says so.</summary>
	public void Apply(PoseSample s, ClipCatalog catalog, int facing, int worldFrame)
	{
		Mirror.Scale = new Vector3(facing, 1f, 1f);
		SwayTails(worldFrame);
		var to = catalog[s.Clip];
		if (to is null || !Has(to.Id)) return;
		if (s.Blending && catalog[s.FromClip] is { } from && Has(from.Id))
		{
			SeekTo(from.Id, from.TimeAt(s.FromFrame));
			int n = Skeleton.GetBoneCount();
			Span<Quaternion> rot = n <= 64 ? stackalloc Quaternion[n] : new Quaternion[n];
			for (int b = 0; b < n; b++) rot[b] = Skeleton.GetBonePoseRotation(b);
			Vector3 hipsFrom = _hips >= 0 ? Skeleton.GetBonePosePosition(_hips) : Vector3.Zero;
			SeekTo(to.Id, to.TimeAt(s.Frame));
			float w = s.Weight;
			for (int b = 0; b < n; b++)
				Skeleton.SetBonePoseRotation(b, rot[b].Slerp(Skeleton.GetBonePoseRotation(b), w));
			if (_hips >= 0) Skeleton.SetBonePosePosition(_hips, hipsFrom.Lerp(Skeleton.GetBonePosePosition(_hips), w));
			return;
		}
		SeekTo(to.Id, to.TimeAt(s.Frame));
	}

	public bool Has(string clipId) => Player.HasAnimation(LibraryName + "/" + clipId);

	/// <summary>Current animation name and seek position (tests).</summary>
	public (string Name, double Time) Playhead => (Player.CurrentAnimation, Player.CurrentAnimationPosition);

	private void SeekTo(string clipId, double t)
	{
		string name = LibraryName + "/" + clipId;
		if (Player.CurrentAnimation != name) Player.Play(name);
		Player.Seek(t, true);
	}

	// ---- Kitsune's nine tails: rigid, placed in the scene on the hips, sway from the world frame (no physics) ----

	public const int TailCount = 9;
	/// <summary>Sway amplitude (degrees, in the side plane) and period (world frames per cycle).</summary>
	[Export] public float TailSwayDeg { get; set; } = 5f;
	[Export] public int TailSwayPeriod { get; set; } = 96;

	/// <summary>
	/// Deterministic sway from the world frame (no physics): each tail on its own phase. Each "Tails" child is a pivot
	/// whose rotation only carries the sway; the fan pose (pitch, splay) is its placed "Fan" child, edited in the scene.
	/// </summary>
	private void SwayTails(int worldFrame)
	{
		int n = _tails.Count;
		if (n == 0 || TailSwayPeriod <= 0) return;
		for (int i = 0; i < n; i++)
		{
			float phase = (worldFrame + i * 11) % TailSwayPeriod / (float)TailSwayPeriod;
			float sway = TailSwayDeg * Mathf.Sin(Mathf.Tau * phase);
			_tails[i].RotationDegrees = new Vector3(-sway, 0f, 0f);
		}
	}
}
