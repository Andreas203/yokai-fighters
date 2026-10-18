using System.Collections.Generic;
using System.Linq;
using Godot;

namespace YokaiFighters.Tests;

/// <summary>YOK-145 probes: inherited fighter scenes instantiate to the same tree as the pre-change standalone scenes
/// (copied from origin/main into tests/fixtures/old_fighters). Rule source: the ticket claim (presentation only), N/A rule ID.</summary>
public static class BaseFighterProbes
{
	static readonly string[] Names = { "ryo", "kitsune", "oni", "kappa" };
	static Node New(string n) => GD.Load<PackedScene>($"res://scenes/fighters/{n}.tscn").Instantiate();
	static Node Old(string n) => GD.Load<PackedScene>($"res://tests/fixtures/old_fighters/{n}.tscn").Instantiate();

	static string Props(Node n)
	{
		var parts = new List<string> { n.GetClass() };
		var sc = n.GetScript().As<Script>();
		parts.Add("script=" + (sc?.ResourcePath ?? ""));
		if (n is Node3D d) parts.Add("xf=" + d.Transform);
		if (n is AnimationPlayer ap)
			parts.Add($"cb={ap.CallbackModeProcess} root={ap.RootNode} det={ap.Deterministic} cbm={ap.CallbackModeMethod} cbd={ap.CallbackModeDiscrete} auto={ap.Autoplay} speed={ap.SpeedScale}");
		if (n is BoneAttachment3D b) parts.Add($"bone={b.BoneName} ext={b.UseExternalSkeleton} {b.ExternalSkeleton}");
		if (n.GetType().Name == "FighterModel") parts.Add("Fighter=" + n.Get("Fighter"));
		if (n.GetType().Name == "ToonLook")
			parts.Add($"toon={((Resource)n.Get("Toon").AsGodotObject())?.ResourcePath} outline={((Resource)n.Get("Outline").AsGodotObject())?.ResourcePath}");
		if (n is MeshInstance3D mi)
			parts.Add($"mesh={mi.Mesh?.ResourcePath}|{mi.Mesh?.GetSurfaceCount()} ov={mi.MaterialOverride?.ResourcePath} vis={mi.Visible}");
		return string.Join(" ", parts);
	}

	static Dictionary<string, string> Dump(Node root)
	{
		var d = new Dictionary<string, string>();
		void Walk(Node n) { d[root.GetPathTo(n).ToString()] = Props(n); foreach (var c in n.GetChildren()) Walk(c); }
		Walk(root);
		return d;
	}

	[Test]
	public static void EveryFighterTreeMatchesOriginMain()
	{
		foreach (var n in Names)
		{
			var a = Old(n); var b = New(n);
			var da = Dump(a); var db = Dump(b);
			var missing = da.Keys.Except(db.Keys).Take(3).ToList();
			var extra = db.Keys.Except(da.Keys).Take(3).ToList();
			Assert.True(missing.Count == 0 && extra.Count == 0, $"{n}: node set differs; missing [{string.Join(",", missing)}] extra [{string.Join(",", extra)}]");
			Assert.Equal(da.Count, db.Count, $"{n} node count");
			foreach (var k in da.Keys)
				Assert.Equal(da[k], db[k], $"{n} node '{k}'");
			// child order matters for NodePath-free consumers
			Assert.Equal(string.Join("|", a.GetChildren().Select(c => c.Name.ToString())), string.Join("|", b.GetChildren().Select(c => c.Name.ToString())), $"{n} root child order");
			Assert.Equal(a.Name.ToString(), b.Name.ToString(), $"{n} root name");
			a.Free(); b.Free();
		}
	}

	[Test]
	public static void KitsuneNineTailsAndOniClubAndKappaShellPresent()
	{
		var k = New("kitsune");
		Assert.Equal(9, k.GetNode("Mirror/Yaw/Tails").GetChildren().Count(c => c.Name.ToString().StartsWith("Tail")), "kitsune tail slots");
		Assert.Equal(9, k.FindChildren("Tail", "Node3D", true, false).Count(t => t.GetParent().Name.ToString() == "Fan" || t.GetParent().GetParent().Name.ToString().StartsWith("Tail")), "kitsune tail meshes");
		k.Free();
		var o = New("oni");
		Assert.True(o.HasNode("Mirror/Yaw/Club/ClubPivot/ClubMesh"), "oni club chain");
		Assert.Equal("RightHand", ((BoneAttachment3D)o.GetNode("Mirror/Yaw/Club")).BoneName.ToString(), "club on right hand bone");
		o.Free();
		var p = New("kappa");
		Assert.True(p.HasNode("Mirror/Yaw/Shell/ShellPivot/ShellMesh"), "kappa shell chain");
		p.Free();
	}

	[Test]
	public static void TwoInstancesShareNoMutableSubresources()
	{
		foreach (var n in Names)
		{
			var a = New(n); var b = New(n);
			var ra = new HashSet<Resource>(); var rb = new HashSet<Resource>();
			void Collect(Node x, HashSet<Resource> s)
			{
				if (x is MeshInstance3D m) { if (m.MaterialOverride != null) s.Add(m.MaterialOverride); if (m.MaterialOverlay != null) s.Add(m.MaterialOverlay); }
				foreach (var c in x.GetChildren()) Collect(c, s);
			}
			Collect(a, ra); Collect(b, rb);
			// Materials that are loaded from a path are shared by design (cache); only embedded (pathless) ones must differ.
			var shared = ra.Where(r => string.IsNullOrEmpty(r.ResourcePath) || r.ResourcePath.Contains("::")).Intersect(rb).ToList();
			Assert.Equal(0, shared.Count, $"{n}: embedded sub-resource shared between two instances");
			// Mutating one instance's transforms must not touch the other.
			((Node3D)a.GetNode("Mirror")).Scale = new Vector3(-1, 1, 1);
			Assert.Equal(Vector3.One, ((Node3D)b.GetNode("Mirror")).Scale, $"{n}: Mirror scale leaked to second copy");
			var pa = (AnimationPlayer)a.GetNode("Animator"); var pb = (AnimationPlayer)b.GetNode("Animator");
			pa.SpeedScale = 3f;
			Assert.Equal(1f, pb.SpeedScale, $"{n}: Animator state shared");
			a.Free(); b.Free();
		}
	}

	static float[] Pose(Node root)
	{
		var sk = (Skeleton3D)root.GetNode("Mirror/Yaw/Model/Armature/Skeleton3D");
		var l = new List<float>();
		for (int i = 0; i < sk.GetBoneCount(); i++) { var q = sk.GetBonePoseRotation(i); var p = sk.GetBonePosePosition(i); l.AddRange(new[] { q.X, q.Y, q.Z, q.W, p.X, p.Y, p.Z }); }
		return l.ToArray();
	}

	[Test]
	public static void SeekGivesIdenticalBonePosesBeforeAndAfter()
	{
		foreach (var n in Names)
		{
			var poses = new List<float[]>();
			foreach (var root in new[] { Old(n), New(n) })
			{
				var holder = new Node(); holder.AddChild(root);
				// runner-less: attach under the main loop root
				((SceneTree)Engine.GetMainLoop()).Root.AddChild(holder);
				var ap = (AnimationPlayer)root.GetNode("Animator");
				var sk = (Skeleton3D)root.GetNode("Mirror/Yaw/Model/Armature/Skeleton3D");
				var anim = new Animation { Length = 1f };
				for (int i = 0; i < System.Math.Min(sk.GetBoneCount(), 12); i++)
				{
					int t = anim.AddTrack(Animation.TrackType.Rotation3D);
					anim.TrackSetPath(t, $"Armature/Skeleton3D:{sk.GetBoneName(i)}");
					anim.RotationTrackInsertKey(t, 0, Quaternion.Identity);
					anim.RotationTrackInsertKey(t, 1, new Quaternion(Vector3.Up, 1.2f));
				}
				var lib = new AnimationLibrary(); lib.AddAnimation("t", anim); ap.AddAnimationLibrary("p", lib);
				ap.Play("p/t"); ap.Pause(); ap.Seek(0.37, true);
				poses.Add(Pose(root));
				((SceneTree)Engine.GetMainLoop()).Root.RemoveChild(holder); holder.Free();
			}
			Assert.True(poses[0].Length > 0, $"{n}: no bones");
			Assert.True(poses[0].SequenceEqual(poses[1]), $"{n}: bone pose after Seek differs from origin/main");
			var rest = New(n); var restPose = Pose(rest); rest.Free();
			Assert.True(!poses[1].SequenceEqual(restPose), $"{n}: seek did not move any bone, so the pose comparison proves nothing");
		}
	}

	// Oni facing -1: club stays a child of the RightHand bone attachment under Mirror, so it mirrors with the body.
	[Test]
	public static void OniMirroredClubStaysOnRightHandWithBody()
	{
		var res = new List<Vector3[]>();
		foreach (var root in new[] { Old("oni"), New("oni") })
		{
			((SceneTree)Engine.GetMainLoop()).Root.AddChild(root);
			((Node3D)root.GetNode("Mirror")).Scale = new Vector3(-1, 1, 1);
			var sk = (Skeleton3D)root.GetNode("Mirror/Yaw/Model/Armature/Skeleton3D");
			var club = (Node3D)root.GetNode("Mirror/Yaw/Club");
			int hand = sk.FindBone("RightHand");
			var handPos = (sk.GlobalTransform * sk.GetBoneGlobalPose(hand)).Origin;
			var pivot = (Node3D)root.GetNode("Mirror/Yaw/Club/ClubPivot");
			res.Add(new[] { handPos, club.GlobalPosition, pivot.GlobalPosition, (Vector3)root.GetNode<Node3D>("Mirror").GlobalTransform.Basis.X });
			((SceneTree)Engine.GetMainLoop()).Root.RemoveChild(root); root.Free();
		}
		for (int i = 0; i < 4; i++) Assert.True(res[0][i].IsEqualApprox(res[1][i]), $"oni mirrored vector {i} differs: {res[0][i]} vs {res[1][i]}");
		Assert.True(res[1][0].DistanceTo(res[1][1]) < 0.01f, "club attachment sits on the mirrored right hand");
	}
}
