using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Godot;
using YokaiFighters.Fight;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-finish-qa windowed measuring and still tool (not a test). Every body the game shows (Tripo Ryo, Kitsune, Oni +
/// club, Kappa + shell, Tanuki true form) plays every clip it can play in a fight, exactly as the fight poses it
/// (<see cref="FighterModel"/>: the game's clip library, root motion stripped, hips scaled by HipScale, game frames
/// through the clip's trim / retime). Each game frame is CPU-skinned from the skeleton and measured:
/// foot height against the floor (y = 0), contact slip of the planted sole, the lowest body point, and
/// self-intersection (limb surface behind another part's surface; props: club, shell, tails).
/// Writes &lt;out&gt;/metrics.json and, with render=1, front ("cam", the fight camera's side) and side stills of the
/// key frames (start, active, recovery, worst) to &lt;out&gt;/cells/.
/// Run: Godot --path game res://tests/captures/finish_qa_capture.tscn --audio-driver Dummy -- out=&lt;dir&gt; [bodies=ryo,kitsune] [render=1]
/// </summary>
public partial class FinishQaCapture : Node3D
{
	private string _dir = ".";
	private bool _render = true;
	private SubViewport _vpCam = null!, _vpSide = null!;
	private Camera3D _camCam = null!, _camSide = null!;
	private MeshInstance3D _lineCam = null!, _lineSide = null!;

	private static readonly string[] Specials = { "fox-mirage", "foxfire", "iron-club-charge", "oni-quake", "rising-talisman", "river-grab", "shell-spin", "spirit-wave" };

	public override async void _Ready()
	{
		string bodies = "ryo,kitsune,oni,kappa,tanuki";
		string tag = "";
		HashSet<string>? only = null;
		foreach (string a in OS.GetCmdlineUserArgs())
		{
			if (a.StartsWith("out=")) _dir = a[4..];
			if (a.StartsWith("bodies=")) bodies = a[7..];
			if (a.StartsWith("render=")) _render = a[7..] != "0";
			if (a.StartsWith("clips=")) only = new HashSet<string>(a[6..].Split(','));
			if (a.StartsWith("tag=")) tag = a[4..];
		}
		Directory.CreateDirectory(Path.Combine(_dir, "cells"));
		BuildStage();
		var cat = FightScene.Catalog;
		var moveClips = FightScene.MoveClips;
		var all = new Dictionary<string, object>();
		foreach (string b in bodies.Split(','))
		{
			var set = b switch
			{
				"ryo" => FighterAnimSet.RyoTripo,
				"tanuki" => FighterAnimSet.TanukiTripo,
				"kitsune" => FighterAnimSet.Kitsune,
				"oni" => FighterAnimSet.Oni,
				"kappa" => FighterAnimSet.Kappa,
				_ => throw new ArgumentException(b),
			};
			var model = FighterModel.Create(set, cat);
			if (model is null) { GD.PrintErr($"no model for {b}"); continue; }
			AddChild(model);
			model.Position = Vector3.Zero;
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			var body = new Body(model);
			GD.Print($"{b}: {body.Summary()}");
			var clips = ClipsFor(b, set, cat, moveClips);
			if (only != null) clips = clips.Where(c => only.Contains(c.Clip)).ToList();
			var res = new Dictionary<string, object>();
			foreach (var (clip, uses) in clips)
			{
				var info = cat[clip]!;
				var hit = HitFrames(clip);
				var frames = new List<FrameM>();
				for (int f = 1; f <= info.FramesTotal; f++)
				{
					model.Apply(new PoseSample(PoseKind.Attack, clip, f, null, 0, 1f), cat, 1, f);
					frames.Add(body.Measure(f, frames.Count > 0 ? frames[^1] : null));
				}
				var picks = PickFrames(frames, info.FramesTotal, hit);
				if (_render)
					foreach (var (label, f) in picks)
					{
						model.Apply(new PoseSample(PoseKind.Attack, clip, f, null, 0, 1f), cat, 1, f);
						await Shoot(model, $"{b}__{clip}__{label}-f{f:000}");
					}
				res[clip] = new Dictionary<string, object>
				{
					["uses"] = uses,
					["frames_total"] = info.FramesTotal,
					["hit"] = hit is { } h ? new[] { h.Start, h.End } : Array.Empty<int>(),
					["picks"] = picks.ToDictionary(p => p.Label, p => p.Frame),
					["frames"] = frames.Select(x => x.ToJson()).ToList(),
				};
				GD.Print($"{b} {clip}: {info.FramesTotal} frames, worst f{picks.Last().Frame}");
			}
			all[b] = new Dictionary<string, object> { ["body"] = body.Summary(), ["clips"] = res };
			RemoveChild(model);
			model.QueueFree();
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		}
		File.WriteAllText(Path.Combine(_dir, $"metrics-{bodies.Replace(',', '_')}{tag}.json"), JsonSerializer.Serialize(all, new JsonSerializerOptions { WriteIndented = false }));
		GD.Print("wrote " + Path.Combine(_dir, $"metrics-{bodies.Replace(',', '_')}{tag}.json"));
		GetTree().Quit();
	}

	// ---------- which clips each body plays in a fight ----------

	private static List<(string Clip, string Uses)> ClipsFor(string body, FighterAnimSet set, ClipCatalog cat, IReadOnlyDictionary<string, string> moveClips)
	{
		var uses = new SortedDictionary<string, List<string>>();
		void Add(string? clip, string why)
		{
			if (clip is null) return;
			if (!set.CanPlay(cat[clip])) { GD.Print($"{body}: {why} -> {clip} not playable on this body"); return; }
			if (!uses.TryGetValue(clip, out var l)) uses[clip] = l = new List<string>();
			if (!l.Contains(why)) l.Add(why);
		}
		foreach (var (kind, clip) in set.Clips) Add(clip, "pose:" + kind);
		string[] prefixes = body switch
		{
			"ryo" or "tanuki" => new[] { "ryo-" },
			"kitsune" => new[] { "kitsune-" },
			"oni" => new[] { "oni-", "kitsune-" },
			"kappa" => new[] { "kappa-", "kitsune-" },
			_ => Array.Empty<string>(),
		};
		var specials = body switch
		{
			"ryo" or "tanuki" => Specials,
			"kitsune" => new[] { "foxfire", "fox-mirage" },
			"oni" => new[] { "iron-club-charge", "oni-quake" },
			"kappa" => new[] { "river-grab", "shell-spin" },
			_ => Array.Empty<string>(),
		};
		foreach (var (move, clip) in moveClips)
		{
			if (!prefixes.Any(move.StartsWith) && !specials.Contains(move)) continue;
			string c = set.MoveClip(clip);
			if (!set.CanPlay(cat[c])) c = move.Contains("throw") || move.Contains("grab") ? set.FallbackThrowClip : set.ClipFor(PoseKind.Attack)!;
			Add(c, "move:" + move);
		}
		return uses.Select(kv => (kv.Key, string.Join(" ", kv.Value))).ToList();
	}

	private static (int Start, int End)? HitFrames(string clip)
	{
		string path = Path.Combine(ContentPaths.Data("clips"), clip + ".json");
		if (!File.Exists(path)) return null;
		using var doc = JsonDocument.Parse(File.ReadAllText(path));
		var r = doc.RootElement;
		if (r.TryGetProperty("hit_start", out var a) && a.ValueKind == JsonValueKind.Number &&
		    r.TryGetProperty("hit_end", out var b) && b.ValueKind == JsonValueKind.Number)
			return (a.GetInt32(), b.GetInt32());
		return null;
	}

	private static List<(string Label, int Frame)> PickFrames(List<FrameM> frames, int n, (int Start, int End)? hit)
	{
		int active = hit?.Start ?? Math.Max(1, (n + 1) / 2);
		int rec = hit is { } h ? Math.Min(n, (h.End + n + 1) / 2) : Math.Max(1, n * 3 / 4);
		int worst = 1; double w = -1;
		foreach (var f in frames) if (f.Badness > w) { w = f.Badness; worst = f.Frame; }
		return new() { ("start", 1), ("active", active), ("recovery", rec), ("worst", worst) };
	}

	// ---------- stage and stills ----------

	private void BuildStage()
	{
		var env = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color(0.93f, 0.93f, 0.9f), AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = new Color(0.6f, 0.6f, 0.6f), AmbientLightEnergy = 1f };
		AddChild(new WorldEnvironment { Environment = env });
		var sun = new DirectionalLight3D { LightEnergy = 1.1f };
		AddChild(sun);
		sun.RotationDegrees = new Vector3(-50, 30, 0);
		var floorMat = new StandardMaterial3D { AlbedoColor = new Color(0.35f, 0.45f, 0.6f, 0.35f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
		AddChild(new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(6, 6) }, MaterialOverride = floorMat });
		var lineMat = new StandardMaterial3D { AlbedoColor = new Color(0.9f, 0f, 0f), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, NoDepthTest = true, RenderPriority = 10 };
		// Floor line (y = 0) drawn over everything: one across the fight camera's view, one across the side view.
		_lineCam = new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(4f, 0.006f, 0.006f) }, MaterialOverride = lineMat, Layers = 2 };
		_lineSide = new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.006f, 0.006f, 4f) }, MaterialOverride = lineMat, Layers = 4 };
		AddChild(_lineCam); AddChild(_lineSide);
		(_vpCam, _camCam) = MakeView(new Vector3(0f, 0.95f, 4.0f), new Vector3(0f, 0.85f, 0f), 1 | 2);
		(_vpSide, _camSide) = MakeView(new Vector3(4.0f, 0.95f, 0f), new Vector3(0f, 0.85f, 0f), 1 | 4);
	}

	private (SubViewport, Camera3D) MakeView(Vector3 pos, Vector3 look, uint mask)
	{
		var vp = new SubViewport { Size = new Vector2I(300, 380), RenderTargetUpdateMode = SubViewport.UpdateMode.Always, Msaa3D = Viewport.Msaa.Msaa4X };
		AddChild(vp);
		var cam = new Camera3D { Fov = 34f, CullMask = mask, Current = true };
		vp.AddChild(cam);
		cam.Position = pos;
		cam.LookAt(look);
		return (vp, cam);
	}

	private async System.Threading.Tasks.Task Shoot(FighterModel model, string name)
	{
		// Oni + club and Kappa are taller / wider: pull the cameras back to the body's own height.
		float h = model.AnimSet.Fighter switch { "oni" => 1.25f, _ => 1f };
		_camCam.Position = new Vector3(0f, 0.95f * h, 4.0f * h);
		_camCam.LookAt(new Vector3(0f, 0.85f * h, 0f));
		_camSide.Position = new Vector3(4.0f * h, 0.95f * h, 0f);
		_camSide.LookAt(new Vector3(0f, 0.85f * h, 0f));
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		_vpCam.GetTexture().GetImage().SavePng(Path.Combine(_dir, "cells", name + "__cam.png"));
		_vpSide.GetTexture().GetImage().SavePng(Path.Combine(_dir, "cells", name + "__side.png"));
	}

}
