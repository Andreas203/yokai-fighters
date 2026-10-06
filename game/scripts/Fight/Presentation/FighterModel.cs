using System;
using System.Collections.Generic;
using Godot;

namespace YokaiFighters.Fight;

/// <summary>
/// YOK-53: a rigged fighter (Meshy humanoid) posed from a <see cref="PoseSample"/>. The AnimationPlayer never
/// advances on its own (manual callback mode): every render calls <c>Seek(t, true)</c> at the exact frame time.
/// Blends are computed from the tick (seek the source pose, capture, seek the target, slerp). Mirroring is
/// Scale.X = Facing on <see cref="Mirror"/>, like the hitbox data (mirrored by facing).
/// </summary>
public partial class FighterModel : Node3D
{
	public const string LibraryName = "yf";
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

	/// <summary>Builds the model; null when the model or its skeleton can't load (the scene keeps the capsule).</summary>
	public static FighterModel? Create(FighterAnimSet set, ClipCatalog catalog)
	{
		if (!ResourceLoader.Exists(set.ModelPath)) return null;
		var scene = GD.Load<PackedScene>(set.ModelPath).Instantiate<Node3D>();
		var skel = scene.FindChild("Skeleton3D", true, false) as Skeleton3D;
		var player = scene.FindChild("AnimationPlayer", true, false) as AnimationPlayer;
		if (skel is null || player is null) { scene.Free(); return null; }

		var model = new FighterModel { Name = set.Fighter + "-model", AnimSet = set, Skeleton = skel, Player = player };
		model.Mirror = new Node3D { Name = "Mirror" };
		model.AddChild(model.Mirror);
		var yaw = new Node3D { Name = "Yaw", RotationDegrees = new Vector3(0f, 90f + set.YawOffsetDeg, 0f) }; // model +Z → +X
		model.Mirror.AddChild(yaw);
		yaw.AddChild(scene);
		model._hips = skel.FindBone("Hips");

		player.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Manual; // F3: never self-advance
		player.Deterministic = true;
		if (!LibraryCache.TryGetValue(set.Fighter, out var lib))
		{
			lib = BuildLibrary(set, catalog, skel, model._hips);
			LibraryCache[set.Fighter] = lib;
		}
		if (player.HasAnimationLibrary(LibraryName)) player.RemoveAnimationLibrary(LibraryName);
		player.AddAnimationLibrary(LibraryName, lib);

		ApplyToon(scene);
		if (set.TailPath != null) model.BuildTails(set.TailPath);
		return model;
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

	// ---- Kitsune's nine tails: rigid, on the hips, sway from the world frame (no physics) ----

	public const int TailCount = 9;
	public const float TailLength = 0.55f;      // metres along the tail mesh (its 1.9 m height scaled down)
	/// <summary>
	/// The fan opens in the side plane (what the 2.5D camera sees): from TailFanFromDeg off vertical, back over the
	/// floor, to TailFanToDeg. Narrowed and pushed off the back (TailBackCm) so the top tail clears her hair (gate note).
	/// </summary>
	public const float TailFanFromDeg = 45f, TailFanToDeg = 105f;
	public const float TailSpreadDeg = 12f;     // small left/right splay so the fan has depth
	public const float TailBackCm = 16f, TailDownCm = 4f;
	public const float TailSwayDeg = 5f;
	public const int TailSwayPeriod = 96;       // world frames per sway cycle

	private void BuildTails(string path)
	{
		if (_hips < 0 || !ResourceLoader.Exists(path)) return;
		var tailScene = GD.Load<PackedScene>(path);
		var attach = new BoneAttachment3D { Name = "Tails", BoneName = "Hips" };
		Skeleton.AddChild(attach);
		float s = TailLength / 1.9f;
		for (int i = 0; i < TailCount; i++)
		{
			// Pivot in metres (the Armature is scaled 0.01), behind the hips.
			var pivot = new Node3D { Name = $"Tail{i}", Scale = Vector3.One * 100f, Position = new Vector3(0f, -TailDownCm, -TailBackCm) };
			attach.AddChild(pivot);
			var tail = tailScene.Instantiate<Node3D>();
			tail.Scale = Vector3.One * s;
			// The mesh is a flat card centred on its origin: bottom end on the pivot, face turned to the side camera.
			tail.Position = new Vector3(0f, 0.95f * s, 0f);
			tail.RotationDegrees = new Vector3(0f, 90f, 0f);
			pivot.AddChild(tail);
			ApplyToon(tail);
			_tails.Add(pivot);
		}
		SwayTails(0);
	}

	/// <summary>Deterministic sway from the world frame (no physics): each tail on its own phase.</summary>
	private void SwayTails(int worldFrame)
	{
		int n = _tails.Count;
		for (int i = 0; i < n; i++)
		{
			float u = n == 1 ? 0.5f : i / (float)(n - 1);
			float phase = (worldFrame + i * 11) % TailSwayPeriod / (float)TailSwayPeriod;
			float sway = TailSwayDeg * Mathf.Sin(Mathf.Tau * phase);
			float pitch = Mathf.Lerp(TailFanFromDeg, TailFanToDeg, u) + sway;
			float splay = (i % 2 == 0 ? 1f : -1f) * TailSpreadDeg * (1f - Mathf.Abs(u - 0.5f));
			_tails[i].RotationDegrees = new Vector3(-pitch, 0f, splay);
		}
	}

	// ---- V1 toon look: toon diffuse/specular + inverted-hull ink outline (one shared outline material) ----

	public const float OutlineMetres = 0.008f;
	private static readonly Dictionary<float, StandardMaterial3D> Outlines = new();

	/// <summary>V1 toon diffuse/specular + ink outline on every mesh under <paramref name="root"/>; stage props reuse it (YOK-39).</summary>
	public static void ApplyToon(Node root, float outlineMetres = OutlineMetres)
	{
		foreach (var node in root.FindChildren("*", "MeshInstance3D", true, false))
		{
			var mi = (MeshInstance3D)node;
			if (mi.Mesh is null) continue;
			float scale = WorldScale(mi);
			for (int sfc = 0; sfc < mi.Mesh.GetSurfaceCount(); sfc++)
			{
				if (mi.GetActiveMaterial(sfc) is not BaseMaterial3D baseMat) continue;
				var m = (BaseMaterial3D)baseMat.Duplicate();
				m.DiffuseMode = BaseMaterial3D.DiffuseModeEnum.Toon;
				m.SpecularMode = BaseMaterial3D.SpecularModeEnum.Toon;
				m.Roughness = 1f;
				m.NextPass = Outline(outlineMetres / scale);
				mi.SetSurfaceOverrideMaterial(sfc, m);
			}
		}
	}

	private static float WorldScale(Node3D n)
	{
		float s = 1f;
		for (Node? p = n; p != null; p = p.GetParent()) if (p is Node3D p3) s *= p3.Scale.Y;
		return Mathf.Abs(s) < 1e-6f ? 1f : Mathf.Abs(s);
	}

	private static StandardMaterial3D Outline(float grow)
	{
		float key = MathF.Round(grow, 5);
		if (Outlines.TryGetValue(key, out var m)) return m;
		m = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoColor = new Color(0.08f, 0.06f, 0.07f),
			CullMode = BaseMaterial3D.CullModeEnum.Front,
			Grow = true,
			GrowAmount = key,
		};
		Outlines[key] = m;
		return m;
	}
}
