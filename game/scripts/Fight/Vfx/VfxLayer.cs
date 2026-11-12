using System.Collections.Generic;
using Godot;

namespace YokaiFighters.Fight;

/// <summary>
/// YOK-242: draws projectile visuals and special-move effects (<see cref="SpecialFx"/>) as flat quads on the fight
/// plane (F4: Z = 0), facing the camera, over the stage and fighters (no depth test). Presentation only: it reads the
/// visual lists and writes nothing back. One pool of <see cref="MeshInstance3D"/>s with a <see cref="ShaderMaterial"/>
/// each (game/shaders/vfx.gdshader); the shader's <c>age</c>/<c>life</c> come from sim ticks, never the wall clock.
/// </summary>
public partial class VfxLayer : Node3D
{
	public const string NodeName = "SpecialVfx";
	/// <summary>Drawn after the fighters and the stage, under the hit sparks (<see cref="HitSparkLayer.RenderPriority"/>).</summary>
	public const int RenderPriority = 15;

	private static Shader? _shader;
	private readonly List<MeshInstance3D> _pool = new();

	/// <summary>Quads in use on the last <see cref="Draw"/>: projectiles first, then effects (tests).</summary>
	public int Shown { get; private set; }
	public IReadOnlyList<MeshInstance3D> Quads => _pool;

	public static Shader Shader => _shader ??= GD.Load<Shader>(VfxArt.ShaderPath);

	public void Draw(IReadOnlyList<ProjectileVisual> projectiles, IReadOnlyList<VfxEffectFrame> effects)
	{
		int n = projectiles.Count + effects.Count;
		while (_pool.Count < n)
		{
			var mat = new ShaderMaterial { Shader = Shader, RenderPriority = RenderPriority };
			var q = new MeshInstance3D
			{
				Name = $"Vfx{_pool.Count}",
				Mesh = new QuadMesh { Size = Vector2.One },
				MaterialOverride = mat,
				CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			};
			AddChild(q);
			_pool.Add(q);
		}
		for (int k = 0; k < _pool.Count; k++)
		{
			var q = _pool[k];
			q.Visible = k < n;
			if (!q.Visible) continue;
			if (k < projectiles.Count) Apply(q, projectiles[k]);
			else Apply(q, effects[k - projectiles.Count]);
		}
		Shown = n;
	}

	private static void Apply(MeshInstance3D q, in ProjectileVisual p)
	{
		float w = FightScene.ToMeters(p.X1 - p.X0), h = FightScene.ToMeters(p.Y1 - p.Y0);
		var tex = VfxArt.For(p.Style);
		if (tex != null) h = FitHeight(w, tex, p.Style.SheetColumns); // an image keeps its proportions; the leading edge stays on the hitbox
		q.Position = new Vector3(FightScene.ToMeters(p.X0 + p.X1) / 2f, FightScene.ToMeters(p.Y0 + p.Y1) / 2f, 0f);
		q.Scale = new Vector3(Mathf.Max(w, 0.001f), Mathf.Max(h, 0.001f), 1f);
		Set((ShaderMaterial)q.MaterialOverride, p.Style, tex, w, h, p.Dir, p.Age, 0f, 1f, p.SheetFrame);
	}

	private static void Apply(MeshInstance3D q, in VfxEffectFrame f)
	{
		var e = f.Effect;
		float w = FightScene.ToMeters(e.W), h = FightScene.ToMeters(e.H);
		var tex = VfxArt.For(e.Style);
		if (tex != null) (w, h) = Contain(w, h, tex, e.Style.SheetColumns); // the image fits inside the look's box, unstretched
		q.Position = new Vector3(FightScene.ToMeters(f.CentreX), FightScene.ToMeters(f.CentreY), 0f);
		q.Scale = new Vector3(Mathf.Max(w, 0.001f), Mathf.Max(h, 0.001f), 1f);
		Set((ShaderMaterial)q.MaterialOverride, e.Style, tex, w, h, e.Dir, f.Age, f.LifePct / 100f, f.AlphaPct / 100f, f.SheetFrame);
	}

	/// <summary>Width over height of one flipbook frame of an image.</summary>
	public static float FrameAspect(int texW, int texH, int columns) => texH > 0 ? texW / (float)System.Math.Max(1, columns) / texH : 1f;

	private static float FitHeight(float w, Texture2D tex, int columns) => w / FrameAspect(tex.GetWidth(), tex.GetHeight(), columns);

	/// <summary>The largest box with the image's aspect inside w × h (YOK-243 slots are 2:1, 4:1, 1:1 and 1:2).</summary>
	public static (float W, float H) Contain(float w, float h, float aspect)
	{
		if (aspect <= 0f || h <= 0f) return (w, h);
		return w / h > aspect ? (h * aspect, h) : (w, w / aspect);
	}

	private static (float, float) Contain(float w, float h, Texture2D tex, int columns) =>
		Contain(w, h, FrameAspect(tex.GetWidth(), tex.GetHeight(), columns));

	private static void Set(ShaderMaterial m, VfxStyle s, Texture2D? tex, float w, float h, int dir, int age, float life, float opacity, int frame)
	{
		m.SetShaderParameter("shape", (int)s.Look);
		m.SetShaderParameter("colour", HitSparkLayer.ColourOf(s.Colour));
		m.SetShaderParameter("core", HitSparkLayer.ColourOf(s.Core));
		m.SetShaderParameter("ink", HitSparkLayer.ColourOf(SpecialVfxStyle.OutlineFor(s.Colour)));
		m.SetShaderParameter("age", (float)age);
		m.SetShaderParameter("life", life);
		m.SetShaderParameter("opacity", opacity);
		m.SetShaderParameter("flip", dir < 0 ? -1f : 1f);
		m.SetShaderParameter("aspect", h > 0f ? w / h : 1f);
		m.SetShaderParameter("use_tex", tex != null);
		m.SetShaderParameter("tex", tex != null ? tex : new Variant());
		m.SetShaderParameter("tint", s.Tint);
		m.SetShaderParameter("columns", tex != null ? System.Math.Max(1, s.SheetColumns) : 1);
		m.SetShaderParameter("frame", frame);
	}
}
