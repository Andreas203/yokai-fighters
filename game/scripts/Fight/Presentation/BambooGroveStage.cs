using System;
using Godot;
using YokaiFighters.Sim;
using Environment = Godot.Environment;

namespace YokaiFighters.Fight;

/// <summary>
/// YOK-39: the bamboo grove at dusk (V9), assembled from the generated assets in <see cref="DefaultDir"/>
/// (spec <c>assets/specs/bamboo-grove-dusk.md</c>). Layers back to front: cropped backdrop quad, bamboo clusters,
/// stone lanterns, tiled ground strip. Dusk = warm low key light + cool fill + indigo depth fog.
/// Purely visual and fixed: prop jitter comes from a seeded <see cref="SimRng"/>, so every build is identical.
/// Every prop stands at Z &lt;= <see cref="PropMaxZ"/>, behind the Z = 0 gameplay plane (F4).
/// </summary>
public static class BambooGroveStage
{
	public const string Kind = "bamboo-grove";
	public const string DefaultDir = "res://assets/generated/stage/bamboo-grove/";
	public const uint Seed = 39;

	public const float BackdropZ = -16f;
	public const float BackdropWidth = 36f;
	/// <summary>Art inside the generated PNGs' grey letterbox border (asset-smith acceptance, in pixels).</summary>
	public static readonly Rect2 BackdropArt = new(110, 75, 1156, 618), BackdropImage = new(0, 0, 1376, 768);
	public static readonly Rect2 GroundArt = new(103, 173, 1137, 422), GroundImage = new(0, 0, 1344, 768);
	/// <summary>Where the painted ground line sits in the cropped backdrop, from its bottom (0..1); placed just under y = 0.</summary>
	public const float BackdropGroundLine = 0.18f;
	public const float GroundTileMetres = 4f;
	public const float FloorNearZ = 8f;
	public const float PropMaxZ = -2f;
	public const float PropOutlineMetres = 0.02f;

	/// <summary>Bamboo clusters: x, z, height (m) before jitter.</summary>
	private static readonly (float X, float Z, float H)[] Bamboo =
	{
		(-14f, -9.5f, 8f), (-11f, -5f, 7f), (-8f, -11f, 8.5f), (-5.5f, -6.5f, 7.5f), (-2.5f, -12.5f, 9f),
		(3f, -12f, 9f), (5.5f, -6f, 7.5f), (8.5f, -10.5f, 8.5f), (11.5f, -4.5f, 7f), (14.5f, -8.5f, 8f),
		(-17f, -6f, 7.5f), (17.5f, -5.5f, 7.5f),
	};
	/// <summary>Stone lanterns: x, z, height (m), outside the middle of the fighter band.</summary>
	private static readonly (float X, float Z, float H)[] Lanterns =
	{
		(-7.5f, -5.5f, 1.3f), (8f, -6f, 1.3f), (-16.5f, -6f, 1.4f),
	};

	/// <summary>Builds the grove under <paramref name="root"/>; false (partly built, caller clears it) when the backdrop or ground is missing.</summary>
	public static bool Build(Node3D root, SimConfig c, string dir)
	{
		Texture2D? backdrop = LoadTexture(dir + "backdrop.png"), ground = LoadTexture(dir + "ground-strip.png");
		if (backdrop is null || ground is null) return false;

		AddDusk(root);
		AddBackdrop(root, backdrop);
		AddFloor(root, ground, c);
		var rng = new SimRng(Seed);
		var props = new Node3D { Name = "Props" };
		root.AddChild(props);
		PackedScene? bamboo = LoadScene(dir + "bamboo-cluster.glb"), lantern = LoadScene(dir + "lantern.glb");
		for (int i = 0; i < Bamboo.Length && bamboo != null; i++)
			AddProp(props, bamboo, $"Bamboo{i}", Bamboo[i], rng, scaleJitterPct: 15, posJitterCm: 60);
		for (int i = 0; i < Lanterns.Length && lantern != null; i++)
			AddProp(props, lantern, $"Lantern{i}", Lanterns[i], rng, scaleJitterPct: 8, posJitterCm: 20);
		return true;
	}

	private static Texture2D? LoadTexture(string path) => ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
	private static PackedScene? LoadScene(string path) => ResourceLoader.Exists(path) ? GD.Load<PackedScene>(path) : null;

	private static void AddDusk(Node3D root)
	{
		root.AddChild(new WorldEnvironment
		{
			Name = "Environment",
			Environment = new Environment
			{
				BackgroundMode = Environment.BGMode.Color,
				BackgroundColor = new Color(0.18f, 0.21f, 0.34f), // indigo, behind everything
				AmbientLightSource = Environment.AmbientSource.Color,
				AmbientLightColor = new Color(0.42f, 0.42f, 0.58f), // cool dusk sky
				AmbientLightEnergy = 0.55f,
				FogEnabled = true,
				FogMode = Environment.FogModeEnum.Depth,
				FogLightColor = new Color(0.27f, 0.29f, 0.45f), // indigo mist
				FogDensity = 0.55f,
				FogDepthBegin = 14f, // just past the fighters (camera ~12 m from Z = 0)
				FogDepthEnd = 30f,
				FogDepthCurve = 1.2f,
				TonemapMode = Environment.ToneMapper.Linear,
			},
		});
		// Warm low key from front-left (persimmon afterglow), cool fill from the right; no shadow from the fill.
		root.AddChild(new DirectionalLight3D
		{
			Name = "Sun",
			LightColor = new Color(1f, 0.74f, 0.52f),
			LightEnergy = 1.15f,
			RotationDegrees = new Vector3(-24f, -55f, 0f),
			ShadowEnabled = true,
		});
		root.AddChild(new DirectionalLight3D
		{
			Name = "Fill",
			LightColor = new Color(0.55f, 0.64f, 0.95f),
			LightEnergy = 0.4f,
			RotationDegrees = new Vector3(-35f, 60f, 0f),
		});
	}

	private static void AddBackdrop(Node3D root, Texture2D tex)
	{
		float h = BackdropWidth * BackdropArt.Size.Y / BackdropArt.Size.X;
		root.AddChild(new MeshInstance3D
		{
			Name = "Backdrop",
			Mesh = new QuadMesh { Size = new Vector2(BackdropWidth, h) },
			MaterialOverride = new StandardMaterial3D
			{
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				AlbedoTexture = tex,
				// Crop the letterbox by UV (the PNG stays untouched).
				Uv1Scale = new Vector3(BackdropArt.Size.X / BackdropImage.Size.X, BackdropArt.Size.Y / BackdropImage.Size.Y, 1f),
				Uv1Offset = new Vector3(BackdropArt.Position.X / BackdropImage.Size.X, BackdropArt.Position.Y / BackdropImage.Size.Y, 0f),
				TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps,
				DisableFog = true, // the painting carries its own mist
			},
			Position = new Vector3(0f, h * (0.5f - BackdropGroundLine) - 0.3f, BackdropZ),
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
		});
	}

	/// <summary>Floor tiled with the cropped ground strip (cropped in memory, mirror-tiled in a toon shader).</summary>
	private static void AddFloor(Node3D root, Texture2D tex, SimConfig c)
	{
		float width = BackdropWidth + 4f, depth = FloorNearZ - BackdropZ + 1f;
		float tileH = GroundTileMetres * GroundArt.Size.Y / GroundArt.Size.X;
		var mat = new ShaderMaterial { Shader = new Shader { Code = FloorShader } };
		mat.SetShaderParameter("tex", Crop(tex, GroundArt, GroundImage));
		mat.SetShaderParameter("tiles", new Vector2(width / GroundTileMetres, depth / tileH));
		root.AddChild(new MeshInstance3D
		{
			Name = "Floor",
			Mesh = new PlaneMesh { Size = new Vector2(width, depth), Material = mat },
			Position = new Vector3(0f, 0f, (FloorNearZ + BackdropZ - 1f) / 2f),
		});
	}

	/// <summary>The art inside the letterbox as its own texture (crops the loaded image in memory; the PNG is untouched).</summary>
	public static Texture2D Crop(Texture2D tex, Rect2 art, Rect2 full)
	{
		Image img = tex.GetImage();
		if (img.IsCompressed()) img.Decompress();
		var sx = img.GetWidth() / full.Size.X; var sy = img.GetHeight() / full.Size.Y;
		img = img.GetRegion(new Rect2I((int)(art.Position.X * sx), (int)(art.Position.Y * sy), (int)(art.Size.X * sx), (int)(art.Size.Y * sy)));
		img.GenerateMipmaps();
		return ImageTexture.CreateFromImage(img);
	}

	private const string FloorShader = @"
shader_type spatial;
render_mode diffuse_toon, specular_toon;
uniform sampler2D tex : source_color, filter_linear_mipmap, repeat_enable;
uniform vec2 tiles;
uniform vec3 tint = vec3(0.78, 0.74, 0.70);
void fragment() {
	vec2 t = UV * tiles;
	vec2 uv = 1.0 - abs(mod(t, 2.0) - 1.0); // mirrored tiling: the strip isn't seamless (asset-smith note)
	vec3 c = textureGrad(tex, uv, dFdx(t), dFdy(t)).rgb;
	float l = dot(c, vec3(0.299, 0.587, 0.114));
	ALBEDO = mix(c, vec3(l), 0.35) * tint; // calm the gritty strip toward the muted palette (V1)
	ROUGHNESS = 1.0;
	SPECULAR = 0.0;
}";

	private static void AddProp(Node3D parent, PackedScene scene, string name, (float X, float Z, float H) spot, SimRng rng, int scaleJitterPct, int posJitterCm)
	{
		var model = scene.Instantiate<Node3D>();
		Aabb box = LocalBounds(model);
		if (box.Size.Y <= 1e-4f) { model.Free(); return; }
		float jitter = 1f + (rng.Next(2 * scaleJitterPct + 1) - scaleJitterPct) / 100f;
		float x = spot.X + (rng.Next(2 * posJitterCm + 1) - posJitterCm) / 100f;
		float z = Math.Min(PropMaxZ, spot.Z + (rng.Next(2 * posJitterCm + 1) - posJitterCm) / 100f);
		float yaw = rng.Next(360);
		float scale = spot.H * jitter / box.Size.Y;
		// Re-pivot: the generated meshes have their origin at the mesh centre; put the base centre at the origin.
		var pivot = new Node3D { Name = "Pivot", Position = -new Vector3(box.GetCenter().X, box.Position.Y, box.GetCenter().Z) };
		pivot.AddChild(model);
		var holder = new Node3D { Name = name, Position = new Vector3(x, 0f, z), RotationDegrees = new Vector3(0f, yaw, 0f), Scale = Vector3.One * scale };
		holder.AddChild(pivot);
		parent.AddChild(holder);
		FighterModel.ApplyToon(model, PropOutlineMetres);
		// Keep the back of the prop (its radius after the yaw) behind the gameplay plane too.
		float radius = Mathf.Max(box.Size.X, box.Size.Z) * 0.5f * scale;
		if (z + radius > PropMaxZ) holder.Position = new Vector3(x, 0f, PropMaxZ - radius);
	}

	/// <summary>Bounds of every mesh under <paramref name="n"/> in its parent's space (works before entering the tree).</summary>
	public static Aabb LocalBounds(Node n)
	{
		Aabb? acc = null;
		Walk(n, Transform3D.Identity, ref acc);
		return acc ?? new Aabb();
	}

	private static void Walk(Node n, Transform3D xf, ref Aabb? acc)
	{
		if (n is Node3D n3) xf = xf * n3.Transform;
		if (n is MeshInstance3D mi && mi.Mesh != null)
		{
			Aabb b = xf * mi.Mesh.GetAabb();
			acc = acc is null ? b : acc.Value.Merge(b);
		}
		foreach (Node ch in n.GetChildren()) Walk(ch, xf, ref acc);
	}
}
