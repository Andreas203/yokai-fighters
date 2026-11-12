using System.Collections.Generic;
using Godot;

namespace YokaiFighters.Fight;

/// <summary>
/// YOK-finish-vfx-real partial: what makes a special read as real energy, fire, water or impact on top of the
/// paper-talisman sprite. Every drawn quad gets an additive aura quad (game/shaders/vfx_aura.gdshader: glow halo, hot
/// core, procedural embers, sparks, droplets, debris or motes as pure functions of the sim age); casts, releases and
/// impacts flash an <see cref="OmniLight3D"/> and projectiles carry a flickering one; and the stage's
/// <see cref="WorldEnvironment"/> gets a glow pass that only HDR values bloom. Presentation only and timed from the sim
/// (the same age and life the sprite uses), so a replay draws the same frames (F3).
/// </summary>
public partial class VfxLayer
{
	public const string AuraShaderPath = "res://shaders/vfx_aura.gdshader";
	/// <summary>At most this many lights at once (cast and impact flashes first, then projectiles in flight).</summary>
	public const int MaxLights = 4;
	/// <summary>Ticks a cast, release or impact flash lasts.</summary>
	public const int FlashTicks = 14;
	/// <summary>
	/// The stage glow pass (<see cref="ApplyGlow"/>) is off: the stage specs say the lanterns are a flat fill, never a
	/// bloom (lantern-riverbank.md, bamboo-grove-dusk.md; pinned by StageSetTests). The aura draws its own soft halo, so
	/// the effects glow without it. The designer can turn it on here.
	/// </summary>
	public static bool StageGlow = false;

	private static Shader? _auraShader;
	private readonly List<MeshInstance3D> _aura = new();
	private readonly List<OmniLight3D> _lights = new();
	private readonly List<(Vector3 At, Color Colour, float Energy, float Range)> _flashes = new();
	private WorldEnvironment? _glowEnv;
	private int _glowRetry;

	/// <summary>The additive energy layer, one per drawn quad, in the same order as <see cref="Quads"/> (tests).</summary>
	public IReadOnlyList<MeshInstance3D> Auras => _aura;
	public IReadOnlyList<OmniLight3D> Lights => _lights;
	/// <summary>Lights lit on the last <see cref="Draw"/>.</summary>
	public int Lit { get; private set; }

	private void GrowAuras(int n)
	{
		while (_aura.Count < System.Math.Max(n, _pool.Count))
		{
			var mat = new ShaderMaterial { Shader = _auraShader ??= GD.Load<Shader>(AuraShaderPath), RenderPriority = RenderPriority };
			var a = new MeshInstance3D
			{
				Name = $"VfxAura{_aura.Count}",
				Mesh = new QuadMesh { Size = Vector2.One },
				MaterialOverride = mat,
				CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			};
			AddChild(a);
			_aura.Add(a);
		}
	}

	/// <summary>How much larger than the visual its aura is, whether it sits on the ground (bottom edges match), and its HDR gain.</summary>
	public static (float X, float Y, bool Ground, float Energy) AuraShape(VfxLook look, bool projectile) => look switch
	{
		_ when projectile => (2.6f, 2.8f, false, 1.4f),
		VfxLook.Seal or VfxLook.InkSplash => (2.3f, 2.3f, false, 1.4f),
		VfxLook.FlameBurst or VfxLook.Orb => (2.4f, 2.4f, false, 1.4f),
		VfxLook.Dust => (1.5f, 2.6f, true, 1.5f),
		VfxLook.Trail => (1.4f, 2.0f, false, 1.4f),
		VfxLook.Ring => (1.6f, 2.0f, false, 1.3f),
		VfxLook.Grab => (2.2f, 2.4f, false, 1.3f),
		VfxLook.Afterimage => (1.6f, 1.3f, false, 1.4f),
		_ => (2.0f, 2.0f, false, 1.8f),
	};

	/// <summary>HDR gain on the sprite's fill: fire and spirit energy burn brighter than dust (flicker from the sim age).</summary>
	public static float Emission(VfxLook look, int age, float life) => look switch
	{
		VfxLook.FoxFlame or VfxLook.FlameBurst or VfxLook.Orb => 1.2f + 0.1f * Mathf.Sin(age * 0.9f),
		VfxLook.InkWave or VfxLook.Seal => 1.35f,
		VfxLook.InkSplash => 1.2f + 0.8f * Mathf.Max(0f, 1f - life * 4f),
		VfxLook.Ring or VfxLook.Grab or VfxLook.Afterimage or VfxLook.Trail => 1.2f,
		_ => 1f,
	};

	/// <summary>The aura copies the sprite's sim timing and colours, scaled around it.</summary>
	private static void Aura(MeshInstance3D a, MeshInstance3D q, bool projectile)
	{
		var src = (ShaderMaterial)q.MaterialOverride;
		var look = (VfxLook)(int)src.GetShaderParameter("shape");
		var (sx, sy, ground, energy) = AuraShape(look, projectile);
		float w = q.Scale.X, h = q.Scale.Y;
		a.Position = q.Position + new Vector3(0f, ground ? (sy - 1f) * h / 2f : 0f, 0.001f);
		a.Scale = new Vector3(w * sx, h * sy, 1f);
		var m = (ShaderMaterial)a.MaterialOverride;
		m.SetShaderParameter("shape", (int)look);
		foreach (var p in new[] { "colour", "core", "age", "life", "opacity", "flip" })
			m.SetShaderParameter(p, src.GetShaderParameter(p));
		m.SetShaderParameter("aspect", h > 0f ? w / h : 1f);
		m.SetShaderParameter("size", new Vector2(sx, sy));
		m.SetShaderParameter("ground", ground ? 1f : 0f);
		m.SetShaderParameter("energy", energy);
		m.SetShaderParameter("projectile", projectile);
	}

	private void DrawLights(IReadOnlyList<ProjectileVisual> projectiles, IReadOnlyList<VfxEffectFrame> effects)
	{
		_flashes.Clear();
		// a cast, release or impact lights the stage for FlashTicks ticks, fading from its spawn tick
		foreach (var f in effects)
		{
			if (f.Age >= FlashTicks || f.Effect.Style.Look == VfxLook.Afterimage) continue;
			float k = 1f - f.Age / (float)FlashTicks;
			float peak = f.Effect.Moment == VfxMoment.Impact ? 5f : 3f;
			_flashes.Add((new Vector3(FightScene.ToMeters(f.CentreX), FightScene.ToMeters(f.CentreY), 0.8f), Vivid(f.Effect.Style.Colour), peak * k * k, 4.5f));
		}
		// a projectile in flight carries a light that flickers with its sim age
		foreach (var p in projectiles)
		{
			float flicker = 0.85f + 0.15f * Mathf.Sin(p.Age * 0.9f) * Mathf.Sin(p.Age * 0.37f + 1f);
			_flashes.Add((new Vector3(FightScene.ToMeters(p.X0 + p.X1) / 2f, FightScene.ToMeters(p.Y0 + p.Y1) / 2f, 0.8f), Vivid(p.Style.Colour), 2.2f * flicker, 3.5f));
		}
		int n = System.Math.Min(_flashes.Count, MaxLights);
		while (_lights.Count < n)
		{
			var l = new OmniLight3D { Name = $"VfxLight{_lights.Count}", ShadowEnabled = false, OmniAttenuation = 1.6f };
			AddChild(l);
			_lights.Add(l);
		}
		for (int k = 0; k < _lights.Count; k++)
		{
			var l = _lights[k];
			l.Visible = k < n;
			if (!l.Visible) continue;
			var (at, colour, energy, range) = _flashes[k];
			l.Position = at;
			l.LightColor = colour;
			l.LightEnergy = energy;
			l.OmniRange = range;
		}
		Lit = n;
	}

	/// <summary>The preset colour pushed to a saturated light colour.</summary>
	public static Color Vivid(uint rgb)
	{
		var c = HitSparkLayer.ColourOf(rgb);
		float l = 0.299f * c.R + 0.587f * c.G + 0.114f * c.B;
		float S(float v) => Mathf.Clamp(l + (v - l) * 1.8f, 0f, 1f);
		return new Color(S(c.R), S(c.G), S(c.B));
	}

	/// <summary>
	/// Turns on the stage's glow pass once per stage: only HDR values over 1 bloom (the VFX fills and auras), so the
	/// stage and the HUD (a canvas layer, outside the 3D environment) do not wash out.
	/// </summary>
	private void EnsureGlow()
	{
		if (!StageGlow || (_glowEnv != null && IsInstanceValid(_glowEnv))) return;
		if (_glowRetry-- > 0 || !IsInsideTree()) return;
		_glowRetry = 30;
		var found = GetTree().Root.FindChildren("*", nameof(WorldEnvironment), true, false);
		if (found.Count == 0 || found[0] is not WorldEnvironment we || we.Environment == null) return;
		_glowEnv = we;
		we.Environment = (Environment)we.Environment.Duplicate(); // never the stage scene's shared resource
		ApplyGlow(we.Environment);
	}

	public static void ApplyGlow(Environment env)
	{
		env.GlowEnabled = true;
		env.GlowNormalized = false;
		env.GlowIntensity = 0.85f;
		env.GlowStrength = 1.1f;
		env.GlowBloom = 0f;
		env.GlowBlendMode = Environment.GlowBlendModeEnum.Screen;
		env.GlowHdrThreshold = 1.0f;
		env.GlowHdrScale = 2.2f;
		env.GlowHdrLuminanceCap = 12f;
		float[] levels = { 0f, 0.4f, 0.8f, 1f, 0.7f, 0.4f, 0f };
		for (int i = 0; i < levels.Length; i++) env.SetGlowLevel(i, levels[i]);
	}
}
