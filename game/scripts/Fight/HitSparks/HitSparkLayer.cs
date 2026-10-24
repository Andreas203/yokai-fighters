using System.Collections.Generic;
using Godot;

namespace YokaiFighters.Fight;

/// <summary>
/// YOK-198: draws <see cref="HitSparks"/> as camera-facing sprites on the fight plane (F4: Z = 0), over the fighters
/// (no depth test). Presentation only: it reads the spark list and the sim tick and writes nothing back. A pool of
/// <see cref="Sprite3D"/> children, grown on demand and hidden when unused.
/// </summary>
public partial class HitSparkLayer : Node3D
{
	public const string NodeName = "HitSparks";
	/// <summary>Drawn after the fighters and the stage.</summary>
	public const int RenderPriority = 20;

	private readonly List<Sprite3D> _pool = new();

	/// <summary>Sprites in use on the last <see cref="Draw"/> (tests).</summary>
	public int Shown { get; private set; }
	public IReadOnlyList<Sprite3D> Sprites => _pool;

	public void Draw(IReadOnlyList<HitSparkFrame> frames)
	{
		while (_pool.Count < frames.Count)
		{
			var s = new Sprite3D
			{
				Name = $"Spark{_pool.Count}",
				Billboard = BaseMaterial3D.BillboardModeEnum.Disabled, // flat on the fight plane, facing the camera (F4), so the Z spin shows
				Shaded = false,
				NoDepthTest = true,
				RenderPriority = RenderPriority,
				AlphaCut = SpriteBase3D.AlphaCutMode.Disabled,
				TextureFilter = BaseMaterial3D.TextureFilterEnum.Linear,
			};
			AddChild(s);
			_pool.Add(s);
		}
		for (int k = 0; k < _pool.Count; k++)
		{
			var sprite = _pool[k];
			sprite.Visible = k < frames.Count;
			if (sprite.Visible) Apply(sprite, frames[k]);
		}
		Shown = frames.Count;
	}

	private static void Apply(Sprite3D sprite, in HitSparkFrame f)
	{
		var spark = f.Spark;
		var look = spark.Look;
		var (tex, fromFile) = HitSparkArt.For(look, spark.Kind);
		int cols = fromFile ? System.Math.Max(1, look.SheetColumns) : 1;
		sprite.Texture = tex;
		sprite.Hframes = cols;
		sprite.Frame = System.Math.Min(f.SheetFrame, cols - 1);
		sprite.FlipH = spark.Dir < 0;
		float width = HitSparkStyle.BaseSizeMeters * look.SizePct / 100f * f.ScalePct / 100f;
		sprite.PixelSize = width / System.Math.Max(1, tex.GetWidth() / cols);
		sprite.Position = new Vector3(FightScene.ToMeters(spark.X), FightScene.ToMeters(spark.Y), 0f);
		sprite.RotationDegrees = new Vector3(0f, 0f, fromFile ? 0f : spark.RotationDeg);
		var c = look.Tint ? ColourOf(look.Colour) : Colors.White;
		sprite.Modulate = new Color(c.R, c.G, c.B, f.AlphaPct / 100f);
	}

	public static Color ColourOf(uint rgb) =>
		new(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
}
