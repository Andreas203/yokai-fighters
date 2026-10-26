using System.Collections.Generic;
using Godot;

namespace YokaiFighters.Fight;

/// <summary>
/// YOK-208: a receding line of lit stone lanterns (two rows flanking the backdrop's stone path) leading back into the
/// grove. Each lantern is an instance of <see cref="LitLanternScene"/> (lantern.glb + a flat persimmon window insert +
/// an OmniLight3D). The line is built from a few parameters, so a new backdrop (YOK-209) re-aims it in one edit:
/// the near end of each row, where the painted path vanishes (<see cref="VanishU"/>, a fraction of the backdrop art's
/// width), the lanterns per row and how fast they recede. Lantern i of a row sits at
/// <c>vanish + (near - vanish) * Recede^i</c> scaled <c>NearScale * Recede^i</c> (forced perspective: the grove is
/// only a few metres deep between the near lanterns and the backdrop). Only the nearest <see cref="LitPerSide"/> of each
/// row keep their light; the rest glow by their flat fill alone. Visual only; built in <c>_Ready</c> (and in the
/// editor on every change), never saved into the scene. Placement rules: <see cref="BambooGroveStage"/>, checked by
/// <c>StageTests</c>.
/// </summary>
[Tool]
[GlobalClass]
public partial class LanternLine : Node3D
{
	public const string LitLanternScene = "res://scenes/stage_props/lit_lantern.tscn";
	/// <summary>Name prefix of the built lanterns: <c>PathLanternL0</c>, <c>PathLanternR2</c>…</summary>
	public const string Prefix = "PathLantern";
	/// <summary>The range <see cref="Recede"/> is clamped to: 0 would give scale-0 lanterns (a singular basis), 1 would stack a row on its near point.</summary>
	public const float MinRecede = 0.2f, MaxRecede = 0.95f;
	/// <summary>The range <see cref="NearScale"/> is clamped to: a near-zero scale gives a singular basis, a huge one lanterns bigger than the grove.</summary>
	public const float MinNearScale = 0.05f, MaxNearScale = 5f;

	private Vector3 _nearLeft = new(-1.7f, 0, -13.7f), _nearRight = new(2.3f, 0, -13.9f);
	private float _vanishU = 0.53f, _recede = 0.65f, _nearScale = 0.6f;
	private int _perSide = 4, _litPerSide = 2;
	private NodePath _backdrop = "../../Backdrop";

	/// <summary>Floor point (this node's space) of the nearest lantern in the left row.</summary>
	[Export] public Vector3 NearLeft { get => _nearLeft; set { _nearLeft = value; Changed(); } }
	/// <summary>Floor point (this node's space) of the nearest lantern in the right row.</summary>
	[Export] public Vector3 NearRight { get => _nearRight; set { _nearRight = value; Changed(); } }
	/// <summary>Where the painted path vanishes, as a fraction (0 left, 1 right) of the backdrop art's width; the rows converge on the floor under it, at the backdrop.</summary>
	[Export(PropertyHint.Range, "0,1,0.01")] public float VanishU { get => _vanishU; set { _vanishU = value; Changed(); } }
	/// <summary>Lanterns per row (0 = none).</summary>
	[Export(PropertyHint.Range, "0,8,1")] public int PerSide { get => _perSide; set { _perSide = value; Changed(); } }
	/// <summary>Each lantern keeps this fraction of the previous one's distance to the vanish point, and of its scale (clamped to [<see cref="MinRecede"/>, <see cref="MaxRecede"/>]).</summary>
	[Export(PropertyHint.Range, "0.2,0.95,0.01")] public float Recede { get => _recede; set { _recede = value; Changed(); } }
	/// <summary>Scale of the nearest lantern of each row (the grove's front lanterns are about 0.7-0.8; clamped to [<see cref="MinNearScale"/>, <see cref="MaxNearScale"/>]).</summary>
	[Export(PropertyHint.Range, "0.05,5,0.01")] public float NearScale { get => _nearScale; set { _nearScale = value; Changed(); } }
	/// <summary>How many of each row's nearest lanterns keep their OmniLight3D (lights cost; far ones glow by fill).</summary>
	[Export(PropertyHint.Range, "0,8,1")] public int LitPerSide { get => _litPerSide; set { _litPerSide = value; Changed(); } }
	/// <summary>The backdrop quad the line converges on (its centre, width and depth).</summary>
	[Export] public NodePath Backdrop { get => _backdrop; set { _backdrop = value; Changed(); } }

	/// <summary>One built lantern: its node name, floor position (this node's space), uniform scale, yaw in degrees and whether it keeps its light.</summary>
	public readonly record struct Placement(string Name, Vector3 Position, float Scale, float YawDegrees, bool Lit);

	/// <summary>Pure layout of both rows toward <paramref name="vanish"/> (see the class summary). <paramref name="recede"/> is
	/// clamped to [<see cref="MinRecede"/>, <see cref="MaxRecede"/>] and <paramref name="nearScale"/> to
	/// [<see cref="MinNearScale"/>, <see cref="MaxNearScale"/>], so every lantern lies between its row's near point and the
	/// vanish point (never in front of it), each one deeper and smaller than the last, never scale 0.</summary>
	public static List<Placement> Placements(Vector3 nearLeft, Vector3 nearRight, Vector3 vanish, int perSide, float recede, float nearScale, int litPerSide)
	{
		var list = new List<Placement>();
		// Non-finite inputs (a NaN typed in the inspector or a hand-edited scene) or a non-positive scale build nothing.
		if (!float.IsFinite(recede) || !float.IsFinite(nearScale) || !(nearScale > 0f) || !Finite(nearLeft) || !Finite(nearRight) || !Finite(vanish))
			return list;
		recede = Mathf.Clamp(recede, MinRecede, MaxRecede);
		nearScale = Mathf.Clamp(nearScale, MinNearScale, MaxNearScale);
		foreach (var (side, near) in new[] { ("L", nearLeft), ("R", nearRight) })
		{
			float k = 1f;
			for (int i = 0; i < perSide; i++, k *= recede)
			{
				// A fixed yaw per lantern so the rows read as stone blocks, not one cube repeated; same every build.
				float yaw = 25f + 37f * list.Count;
				list.Add(new Placement($"{Prefix}{side}{i}", vanish + (near - vanish) * k, nearScale * k, yaw, i < litPerSide));
			}
		}
		return list;
	}

	private static bool Finite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);

	/// <summary>
	/// The vanish point in this node's space: where the backdrop quad's vertical line at <see cref="VanishU"/> of its
	/// width meets the floor (world y 0), from the quad's full transform, so moving, scaling, turning or re-parenting the
	/// backdrop still gives the right point. Without a backdrop: (0, 0, -16) in this node's space.
	/// </summary>
	public Vector3 VanishPoint()
	{
		var quad = GetNodeOrNull<MeshInstance3D>(Backdrop);
		if (quad?.Mesh is not QuadMesh q) return new Vector3(0, 0, -16f);
		Transform3D toWorld = WorldOf(quad), fromWorld = WorldOf(this).AffineInverse();
		float x = (VanishU - 0.5f) * q.Size.X;
		Vector3 low = toWorld * new Vector3(x, -q.Size.Y / 2f, 0), high = toWorld * new Vector3(x, q.Size.Y / 2f, 0);
		// The seam: the point of that line at y 0 (an edge-on or level quad: its lower end, dropped to the floor).
		Vector3 seam = Mathf.Abs(high.Y - low.Y) > 1e-6f ? low.Lerp(high, -low.Y / (high.Y - low.Y)) : new Vector3(low.X, 0, low.Z);
		return fromWorld * seam;
	}

	/// <summary>
	/// The backdrop's plane in world space: a point on the quad and its unit face normal (the cross of the quad's x and y
	/// axes, so a flattened z scale does not matter), turned to face the camera side (+Z), so a mirrored quad gives the same
	/// plane. Without a backdrop: the plane z = (this node's (0, 0, -16)), normal +Z.
	/// </summary>
	public (Vector3 Point, Vector3 Normal) BackdropPlane()
	{
		var quad = GetNodeOrNull<MeshInstance3D>(Backdrop);
		if (quad?.Mesh is not QuadMesh) return (WorldOf(this) * new Vector3(0, 0, -16f), Vector3.Back);
		Transform3D q = WorldOf(quad);
		Vector3 n = q.Basis.Column0.Cross(q.Basis.Column1).Normalized();
		return (q.Origin, n.Z < 0f ? -n : n);
	}

	/// <summary>Unsigned horizontal stretch of <paramref name="b"/>: the longer of its x and z axes (a mirror or a non-uniform scale never makes it negative).</summary>
	public static float HorizontalStretch(Basis b) => Mathf.Max(b.Column0.Length(), b.Column2.Length());

	/// <summary>The node's world transform: <c>GlobalTransform</c> in the tree, else the chain of local transforms (ancestors above the scene cancel out).</summary>
	private static Transform3D WorldOf(Node3D n)
	{
		if (n.IsInsideTree()) return n.GlobalTransform;
		Transform3D xf = n.Transform;
		for (Node? p = n.GetParent(); p != null; p = p.GetParent())
			if (p is Node3D p3) xf = p3.Transform * xf;
		return xf;
	}

	public List<Placement> Placements() => Placements(VanishPoint());

	private List<Placement> Placements(Vector3 vanish) =>
		Placements(NearLeft, NearRight, vanish, PerSide, Recede, NearScale, Mathf.Min(LitPerSide, LightBudgetPerSide()));

	/// <summary>
	/// Lights each row may keep: what is left of <see cref="BambooGroveStage.MaxLanternLights"/> after the stage's other
	/// OmniLight3D (the front lanterns, any other line), split between the two rows. The cap holds whatever the exports
	/// say. The stage is <see cref="StageScope"/>.
	/// </summary>
	public int LightBudgetPerSide()
	{
		Node scope = StageScope();
		int others = 0;
		foreach (Node n in scope.FindChildren("*", "OmniLight3D", true, false))
			if (!IsAncestorOf(n)) others++;
		return Mathf.Max(0, (BambooGroveStage.MaxLanternLights - others) / 2);
	}

	/// <summary>
	/// Whose lights count against the budget: the nearest <see cref="FightStage"/> ancestor (the stage scene's root, not the
	/// fight scene with its fighters); outside the tree, the topmost ancestor; otherwise the line's <c>Owner</c>, else the
	/// line alone (a line on its own counts only itself).
	/// </summary>
	public Node StageScope()
	{
		Node top = this;
		for (Node? a = GetParent(); a != null; a = a.GetParent())
		{
			if (a is FightStage) return a;
			top = a;
		}
		return !IsInsideTree() ? top : Owner ?? this;
	}

	public override void _Ready() => Rebuild();

	private void Changed()
	{
		if (!IsInsideTree()) return;
		Rebuild();
		// The stage's toon look styled only the first build: restyle with its settings (else the prop defaults).
		var look = StageLook();
		ToonLook.Apply(this, look?.OutlineMetres ?? BambooGroveStage.PropOutlineMetres, look?.Toon, look?.Outline);
	}

	/// <summary>The <see cref="ToonLook"/> beside one of this node's ancestors whose target holds this node (the grove's PropToon), if any.</summary>
	private ToonLook? StageLook()
	{
		for (Node? a = GetParent(); a != null; a = a.GetParent())
			foreach (Node c in a.GetChildren())
				if (c is ToonLook t && t.GetNodeOrNull(t.Target) is Node target && (target == this || target.IsAncestorOf(this)))
					return t;
		return null;
	}

	/// <summary>Frees the built lanterns and builds the line again from the parameters.</summary>
	public void Rebuild()
	{
		foreach (Node c in GetChildren())
		{
			RemoveChild(c);
			c.QueueFree();
		}
		var scene = GD.Load<PackedScene>(LitLanternScene);
		if (scene is null) return;
		Transform3D world = WorldOf(this);
		Vector3 vanish = VanishPoint();
		var (onBackdrop, facing) = BackdropPlane();
		foreach (var p in Placements(vanish))
		{
			// Guard: a lantern whose world depth is in front of the lantern depth (or not a number) is not built (BambooGroveStage.LanternMaxZ).
			float z = (world * p.Position).Z;
			if (!(z <= BambooGroveStage.LanternMaxZ))
			{
				GD.PushWarning($"LanternLine: {p.Name} at world z {z} is nearer than LanternMaxZ {BambooGroveStage.LanternMaxZ}; skipped");
				continue;
			}
			var lantern = scene.Instantiate<Node3D>();
			// Guard: a lantern whose footprint reaches the backdrop plane (signed distance along its camera-facing normal, world,
			// minus the footprint radius; or not a number) pokes through it; not built. Footprint radius as StageTests measures it:
			// half the largest horizontal extent of the unscaled lantern, times its scale and this node's unsigned horizontal stretch.
			Aabb own = BambooGroveStage.LocalBounds(lantern);
			float radius = Mathf.Max(own.Size.X, own.Size.Z) * 0.5f * p.Scale * HorizontalStretch(world.Basis);
			float d = facing.Dot(world * p.Position - onBackdrop);
			if (!(d - radius > 0f))
			{
				GD.PushWarning($"LanternLine: {p.Name} {d} m in front of the backdrop (radius {radius}) touches or crosses it; skipped");
				lantern.Free();
				continue;
			}
			lantern.Name = p.Name;
			lantern.Transform = new Transform3D(new Basis(Vector3.Up, Mathf.DegToRad(p.YawDegrees)).Scaled(Vector3.One * p.Scale), p.Position);
			if (!p.Lit) lantern.GetNodeOrNull("Pivot/Light")?.Free();
			AddChild(lantern);
		}
	}
}
