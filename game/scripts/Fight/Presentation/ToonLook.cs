using System;
using System.Collections.Generic;
using Godot;

namespace YokaiFighters.Fight;

/// <summary>
/// YOK-39 (V1 toon look, editable): put one in a scene and every mesh under <see cref="Target"/> gets toon
/// diffuse/specular (copied from the <see cref="Toon"/> template) and an inverted-hull ink outline (a copy of
/// <see cref="Outline"/> grown by <see cref="OutlineMetres"/>, converted to each mesh's own scale). Both templates are
/// shared resources in <c>res://materials/</c>; edit them to restyle every fighter and prop. A [Tool] node, so the
/// look shows in the editor too (meshes inside instanced models are overridden at load, never saved into the scene).
/// </summary>
[Tool]
[GlobalClass]
public partial class ToonLook : Node
{
	public const string ToonPath = "res://materials/toon.tres", OutlinePath = "res://materials/ink_outline.tres";
	public const float DefaultOutlineMetres = 0.008f;

	/// <summary>Template: its diffuse mode, specular mode and roughness are copied onto each surface's own material.</summary>
	[Export] public StandardMaterial3D? Toon { get; set; }
	/// <summary>Template for the ink hull (colour, cull, unshaded); its grow amount is set per mesh scale.</summary>
	[Export] public StandardMaterial3D? Outline { get; set; }
	/// <summary>Outline thickness in world metres (fighters 0.008, stage props 0.02).</summary>
	[Export] public float OutlineMetres { get; set; } = DefaultOutlineMetres;
	/// <summary>Whose meshes get the look (default: the parent).</summary>
	[Export] public NodePath Target { get; set; } = "..";

	public override void _Ready() => ApplyNow();

	public void ApplyNow()
	{
		Node? root = GetNodeOrNull(Target);
		if (root != null) Apply(root, OutlineMetres, Toon, Outline);
	}

	private static readonly Dictionary<(ulong, float), StandardMaterial3D> Outlines = new();

	/// <summary>Toon + outline on every mesh under <paramref name="root"/> (templates default to the shared .tres files).</summary>
	public static void Apply(Node root, float outlineMetres = DefaultOutlineMetres, StandardMaterial3D? toon = null, StandardMaterial3D? outline = null)
	{
		toon ??= GD.Load<StandardMaterial3D>(ToonPath);
		outline ??= GD.Load<StandardMaterial3D>(OutlinePath);
		// Attachments that follow a bone (the Kitsune's tails) take their bone's transform first, so scales are real.
		foreach (var n in root.FindChildren("*", "BoneAttachment3D", true, false))
			if (((Node)n).IsInsideTree()) ((BoneAttachment3D)n).OnSkeletonUpdate();
		foreach (var node in root.FindChildren("*", "MeshInstance3D", true, false))
		{
			var mi = (MeshInstance3D)node;
			if (mi.Mesh is null) continue;
			float scale = WorldScale(mi);
			for (int sfc = 0; sfc < mi.Mesh.GetSurfaceCount(); sfc++)
			{
				// From the mesh's own material, so applying twice (editor reloads) never stacks.
				if ((mi.Mesh.SurfaceGetMaterial(sfc) ?? mi.GetActiveMaterial(sfc)) is not BaseMaterial3D baseMat) continue;
				var m = (BaseMaterial3D)baseMat.Duplicate();
				m.DiffuseMode = toon.DiffuseMode;
				m.SpecularMode = toon.SpecularMode;
				m.Roughness = toon.Roughness;
				m.NextPass = OutlineFor(outline, outlineMetres / scale);
				mi.SetSurfaceOverrideMaterial(sfc, m);
			}
		}
	}

	/// <summary>Vertical scale of the mesh in the world (from the transform chain; uniform scales throughout).</summary>
	private static float WorldScale(Node3D n)
	{
		float s = n.IsInsideTree() ? n.GlobalTransform.Basis.Y.Length() : ChainScale(n);
		return Mathf.Abs(s) < 1e-6f ? 1f : Mathf.Abs(s);
	}

	private static float ChainScale(Node3D n)
	{
		float s = 1f;
		for (Node? p = n; p != null; p = p.GetParent()) if (p is Node3D p3) s *= p3.Scale.Y;
		return s;
	}

	private static StandardMaterial3D OutlineFor(StandardMaterial3D template, float grow)
	{
		float key = MathF.Round(grow, 5);
		if (Outlines.TryGetValue((template.GetInstanceId(), key), out var m)) return m;
		m = (StandardMaterial3D)template.Duplicate();
		m.Grow = true;
		m.GrowAmount = key;
		Outlines[(template.GetInstanceId(), key)] = m;
		return m;
	}
}
