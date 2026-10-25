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
	/// <summary>Each lantern keeps this fraction of the previous one's distance to the vanish point, and of its scale.</summary>
	[Export(PropertyHint.Range, "0.2,0.95,0.01")] public float Recede { get => _recede; set { _recede = value; Changed(); } }
	/// <summary>Scale of the nearest lantern of each row (the grove's front lanterns are about 0.7-0.8).</summary>
	[Export] public float NearScale { get => _nearScale; set { _nearScale = value; Changed(); } }
	/// <summary>How many of each row's nearest lanterns keep their OmniLight3D (lights cost; far ones glow by fill).</summary>
	[Export(PropertyHint.Range, "0,8,1")] public int LitPerSide { get => _litPerSide; set { _litPerSide = value; Changed(); } }
	/// <summary>The backdrop quad the line converges on (its centre, width and depth).</summary>
	[Export] public NodePath Backdrop { get => _backdrop; set { _backdrop = value; Changed(); } }

	/// <summary>One built lantern: its node name, floor position (this node's space), uniform scale, yaw in degrees and whether it keeps its light.</summary>
	public readonly record struct Placement(string Name, Vector3 Position, float Scale, float YawDegrees, bool Lit);

	/// <summary>Pure layout of both rows toward <paramref name="vanish"/> (see the class summary).</summary>
	public static List<Placement> Placements(Vector3 nearLeft, Vector3 nearRight, Vector3 vanish, int perSide, float recede, float nearScale, int litPerSide)
	{
		var list = new List<Placement>();
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

	/// <summary>The node's world transform: <c>GlobalTransform</c> in the tree, else the chain of local transforms (ancestors above the scene cancel out).</summary>
	private static Transform3D WorldOf(Node3D n)
	{
		if (n.IsInsideTree()) return n.GlobalTransform;
		Transform3D xf = n.Transform;
		for (Node? p = n.GetParent(); p != null; p = p.GetParent())
			if (p is Node3D p3) xf = p3.Transform * xf;
		return xf;
	}

	public List<Placement> Placements() =>
		Placements(NearLeft, NearRight, VanishPoint(), PerSide, Recede, NearScale, LitPerSide);

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
		foreach (var p in Placements())
		{
			var lantern = scene.Instantiate<Node3D>();
			lantern.Name = p.Name;
			lantern.Transform = new Transform3D(new Basis(Vector3.Up, Mathf.DegToRad(p.YawDegrees)).Scaled(Vector3.One * p.Scale), p.Position);
			if (!p.Lit) lantern.GetNodeOrNull("Pivot/Light")?.Free();
			AddChild(lantern);
		}
	}
}
