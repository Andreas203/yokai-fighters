using System;
using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.Fight;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-finish-club still tool (not a test): the Oni in idle, walk, block high, crouch, light punch and heavy punch, as the
/// fight poses him (the game's <see cref="FighterAnimSet.Oni"/> clips through <see cref="FighterModel"/>), shot whole
/// from the fight camera and close on the right hand from the camera and the side, to see how the hand meets the club.
/// Prints, per pose, the angle between the club's handle and the hand bone and the grip's offset in hand space.
/// Run: Godot --path game res://tests/captures/club_grip_capture.tscn --audio-driver Dummy -- out=&lt;dir&gt; [facing=1|-1]
/// </summary>
public partial class ClubGripCapture : Node3D
{
	private string _dir = ".";
	private int _facing = -1;
	private bool _analyseOnly;
	private Vector3 _gripShift;
	private SubViewport _vp = null!;
	private Camera3D _cam = null!;

	public override async void _Ready()
	{
		foreach (string a in OS.GetCmdlineUserArgs())
		{
			if (a.StartsWith("out=")) _dir = a[4..];
			if (a.StartsWith("facing=")) _facing = int.Parse(a[7..]);
			if (a == "analyse") _analyseOnly = true;
			if (a.StartsWith("grip=")) { var g = a[5..].Split(',').Select(float.Parse).ToArray(); _gripShift = new Vector3(g[0], g[1], g[2]); }
		}
		Directory.CreateDirectory(_dir);
		var env = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color(0.93f, 0.93f, 0.9f), AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = new Color(0.6f, 0.6f, 0.6f), AmbientLightEnergy = 1f };
		AddChild(new WorldEnvironment { Environment = env });
		var sun = new DirectionalLight3D { LightEnergy = 1.1f };
		AddChild(sun);
		sun.RotationDegrees = new Vector3(-50, 30, 0);
		_vp = new SubViewport { Size = new Vector2I(420, 420), RenderTargetUpdateMode = SubViewport.UpdateMode.Always, Msaa3D = Viewport.Msaa.Msaa4X };
		AddChild(_vp);
		_cam = new Camera3D { Fov = 34f, Current = true };
		_vp.AddChild(_cam);

		var set = FighterAnimSet.Oni;
		var cat = FightScene.Catalog;
		var model = FighterModel.Create(set, cat);
		if (model is null) { GD.PrintErr("no oni model"); GetTree().Quit(1); return; }
		AddChild(model);
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		var skel = model.Skeleton;
		int hand = skel.FindBone("RightHand");
		var pivot = model.FindChild("ClubPivot", true, false) as Node3D;
		HandShape(model, hand, pivot!);
		if (_analyseOnly) { GetTree().Quit(); return; }
		pivot.Position += _gripShift; // trial shift of the grip, bone space (cm)
		var poses = new (string Name, string Clip, int Frame)[]
		{
			("idle", set.ClipFor(PoseKind.Idle)!, 1),
			("walk", set.ClipFor(PoseKind.WalkFwd)!, 0),
			("block-high", set.ClipFor(PoseKind.BlockHigh)!, 0),
			("crouch", set.ClipFor(PoseKind.Crouch)!, 0),
			("light-punch", set.MoveClip("kitsune-light-punch"), -1),
			("heavy-punch", set.MoveClip("kitsune-heavy-punch"), -1),
		};
		foreach (var (name, clip, f0) in poses)
		{
			var info = cat[clip]!;
			int f = f0 switch { 0 => Math.Max(1, info.FramesTotal / 2), -1 => HitStart(clip) ?? info.FramesTotal / 2, _ => f0 };
			model.Apply(new PoseSample(PoseKind.Attack, clip, f, null, 0, 1f), cat, _facing, f);
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			var hg = skel.GlobalTransform * skel.GetBoneGlobalPose(hand);
			var pg = pivot!.GlobalTransform;
			Vector3 clubAxis = pg.Basis.Z.Normalized();
			Vector3 handAxis = hg.Basis.Y.Normalized();
			float ang = Mathf.RadToDeg(clubAxis.AngleTo(handAxis));
			Vector3 gripInHand = hg.AffineInverse() * pg.Origin;
			GD.Print($"club {name} {clip} f{f}: hand {hg.Origin} grip {pg.Origin} club-vs-hand-axis {ang:F1} deg grip-in-hand(bone units) {gripInHand}");
			Vector3 c = hg.Origin;
			await Shoot(new Vector3(0, 1.5f, 6.2f), new Vector3(0, 1.3f, 0), $"{name}__body");
			await Shoot(c + new Vector3(0, 0.15f, 1.3f), c, $"{name}__hand-cam");
			await Shoot(c + new Vector3(_facing * 1.3f, 0.15f, 0), c, $"{name}__hand-side");
		}
		GetTree().Quit();
	}

	/// <summary>The right hand's verts (dominant weight on RightHand) in bone space: mean, extent along the bone (Y) and principal
	/// axes, so the club's handle can be laid across the palm (the knuckle line) instead of through it.</summary>
	private static void HandShape(FighterModel model, int hand, Node3D pivot)
	{
		var skel = model.Skeleton;
		var pts = new System.Collections.Generic.List<Vector3>();
		foreach (var mi in model.FindChildren("*", "MeshInstance3D", true, false))
		{
			if (mi is not MeshInstance3D m || m.Skin is not { } skin || m.Mesh is not ArrayMesh mesh) continue;
			int nb = skin.GetBindCount();
			var bindBone = new int[nb];
			for (int i = 0; i < nb; i++) { string nm = skin.GetBindName(i); bindBone[i] = !string.IsNullOrEmpty(nm) ? skel.FindBone(nm) : skin.GetBindBone(i); }
			for (int sI = 0; sI < mesh.GetSurfaceCount(); sI++)
			{
				var arr = mesh.SurfaceGetArrays(sI);
				var v = arr[(int)Mesh.ArrayType.Vertex].AsVector3Array();
				var bo = arr[(int)Mesh.ArrayType.Bones].AsInt32Array();
				var w = arr[(int)Mesh.ArrayType.Weights].AsFloat32Array();
				int per = bo.Length / Math.Max(1, v.Length);
				for (int k = 0; k < v.Length; k++)
				{
					int best = 0;
					for (int j = 1; j < per; j++) if (w[k * per + j] > w[k * per + best]) best = j;
					int bind = bo[k * per + best];
					if (w[k * per + best] < 0.5f || bindBone[bind] != hand) continue;
					pts.Add(skin.GetBindPose(bind) * v[k]);
				}
			}
		}
		if (pts.Count == 0) { GD.Print("club hand: no verts"); return; }
		var mean = Vector3.Zero; foreach (var q in pts) mean += q; mean /= pts.Count;
		var c = new double[3, 3];
		foreach (var q in pts) { var d = q - mean; for (int a = 0; a < 3; a++) for (int b = 0; b < 3; b++) c[a, b] += d[a] * d[b] / pts.Count; }
		// Power iteration with deflation: three principal axes.
		var axes = new Vector3[3]; var vars = new double[3];
		for (int e = 0; e < 3; e++)
		{
			var x = new Vector3(0.3f + e, 1f - e * 0.4f, 0.5f).Normalized();
			for (int it = 0; it < 200; it++)
			{
				var y = new Vector3((float)(c[0, 0] * x.X + c[0, 1] * x.Y + c[0, 2] * x.Z), (float)(c[1, 0] * x.X + c[1, 1] * x.Y + c[1, 2] * x.Z), (float)(c[2, 0] * x.X + c[2, 1] * x.Y + c[2, 2] * x.Z));
				for (int f = 0; f < e; f++) y -= axes[f] * y.Dot(axes[f]);
				if (y.Length() < 1e-9f) break;
				x = y.Normalized();
			}
			axes[e] = x;
			vars[e] = 0; foreach (var q in pts) { double d = (q - mean).Dot(x); vars[e] += d * d / pts.Count; }
		}
		float ymin = pts.Min(q => q.Y), ymax = pts.Max(q => q.Y);
		GD.Print($"club hand: {pts.Count} verts, mean {mean}, Y {ymin:F2}..{ymax:F2}");
		for (int e = 0; e < 3; e++)
		{
			float lo = pts.Min(q => (q - mean).Dot(axes[e])), hi = pts.Max(q => (q - mean).Dot(axes[e]));
			GD.Print($"club hand axis{e}: {axes[e]} sd {Math.Sqrt(vars[e]):F2} range {lo:F2}..{hi:F2}");
		}
		// Slices along Y: where the fingers start (width and thickness per 1 cm band).
		for (float y0 = ymin; y0 < ymax; y0 += 1.5f)
		{
			var band = pts.Where(q => q.Y >= y0 && q.Y < y0 + 1.5f).ToList();
			if (band.Count == 0) continue;
			GD.Print($"club hand band y {y0:F1}: n {band.Count} x {band.Min(q => q.X):F1}..{band.Max(q => q.X):F1} z {band.Min(q => q.Z):F1}..{band.Max(q => q.Z):F1}");
		}
		var rb = skel.GetBoneGlobalRest(hand).Basis;
		GD.Print($"club hand rest (skeleton space): palm normal axis2 -> {(rb * axes[2]).Normalized()}, bone Y -> {(rb * Vector3.Up).Normalized()}");
		GD.Print($"club pivot (bone space): origin {pivot.Transform.Origin} axis {pivot.Transform.Basis.Z.Normalized()}");
	}

	private static int? HitStart(string clip)
	{
		string path = Path.Combine(ContentPaths.Data("clips"), clip + ".json");
		if (!File.Exists(path)) return null;
		using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
		return doc.RootElement.TryGetProperty("hit_start", out var a) && a.ValueKind == System.Text.Json.JsonValueKind.Number ? a.GetInt32() : null;
	}

	private async System.Threading.Tasks.Task Shoot(Vector3 pos, Vector3 look, string name)
	{
		_cam.Position = pos;
		_cam.LookAt(look);
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		_vp.GetTexture().GetImage().SavePng(Path.Combine(_dir, name + ".png"));
	}
}
