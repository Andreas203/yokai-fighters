using Godot;

namespace YokaiFighters.Ui.Kit;

/// <summary>
/// The paper-sheet look as one StyleBox resource (<c>ui/kit/paper_sheet_style.tres</c>, <c>paper_card_style.tres</c>).
/// Draws, with no art: a soft drop shadow, a torn-edge polygon (seeded jitter, so a given size always tears the same way),
/// the generated rice-paper fibre texture tiled over it (plain tiles; tileability of the source is unverified, so a seam may show at low opacity),
/// an ink outline and an optional inset red hairline.
/// To use a generated torn-edge texture later: in <c>talisman_theme.tres</c> point the PaperSheet / PaperCard
/// <c>panel</c> style at a StyleBoxTexture (9-slice) instead of this resource. Nothing else changes: every sheet, card and dialog
/// reads that one style, and the content margins are the style's own.
/// </summary>
[Tool, GlobalClass]
public partial class PaperStyleBox : StyleBox
{
	[Export] public Color Paper { get; set; } = new(0.945f, 0.910f, 0.831f);
	[Export] public Color Ink { get; set; } = new(0.114f, 0.106f, 0.129f);
	[Export] public Color InnerLine { get; set; } = new(0.710f, 0.200f, 0.169f, 0.55f);
	[Export] public float InnerLineInset { get; set; } = 16f;
	[Export] public float InnerLineWidth { get; set; } = 2f;
	[Export] public float OutlineWidth { get; set; } = 4f;
	[Export] public Vector2 ShadowOffset { get; set; } = new(8f, 10f);
	[Export] public Color ShadowColor { get; set; } = new(0f, 0f, 0f, 0.38f);
	/// <summary>How far the torn edge wanders (px). 0 = straight edge.</summary>
	[Export] public float TearAmplitude { get; set; } = 5f;
	[Export] public float TearStep { get; set; } = 20f;
	[Export] public int Seed { get; set; } = 7;
	[Export] public Texture2D? Grain { get; set; }
	[Export] public Rect2 GrainRegion { get; set; } = new(120, 120, 780, 780);
	[Export] public float GrainScale { get; set; } = 0.6f;
	[Export(PropertyHint.Range, "0,1,0.05")] public float GrainOpacity { get; set; } = 0.4f;

	public override void _Draw(Rid toCanvasItem, Rect2 rect)
	{
		float inset = TearAmplitude + 1f;
		var body = rect.Grow(-Mathf.Max(inset, OutlineWidth * 0.5f + 1f));
		if (body.Size.X < 8f || body.Size.Y < 8f) return;
		var pts = TornOutline(body, TearAmplitude, TearStep, Seed);
		var one = new[] { Paper };

		var shadow = new Vector2[pts.Length];
		for (int i = 0; i < pts.Length; i++) shadow[i] = pts[i] + ShadowOffset;
		RenderingServer.CanvasItemAddPolygon(toCanvasItem, shadow, new[] { ShadowColor });
		RenderingServer.CanvasItemAddPolygon(toCanvasItem, pts, one);

		if (Grain != null && GrainOpacity > 0f) DrawGrain(toCanvasItem, body.Grow(-inset));

		var closed = new Vector2[pts.Length + 1];
		pts.CopyTo(closed, 0);
		closed[^1] = pts[0];
		if (OutlineWidth > 0f) RenderingServer.CanvasItemAddPolyline(toCanvasItem, closed, new[] { Ink }, OutlineWidth, true);
		if (InnerLine.A > 0f && InnerLineWidth > 0f)
		{
			var r = body.Grow(-InnerLineInset);
			if (r.Size.X > 4f && r.Size.Y > 4f)
				RenderingServer.CanvasItemAddPolyline(toCanvasItem, new[] { r.Position, new Vector2(r.End.X, r.Position.Y), r.End, new Vector2(r.Position.X, r.End.Y), r.Position }, new[] { InnerLine }, InnerLineWidth, true);
		}
	}

	private void DrawGrain(Rid canvas, Rect2 area)
	{
		float tw = GrainRegion.Size.X * GrainScale, th = GrainRegion.Size.Y * GrainScale;
		if (tw < 16f || th < 16f) return;
		var mod = new Color(1f, 1f, 1f, GrainOpacity);
		for (float x = area.Position.X; x < area.End.X; x += tw)
		{
			for (float y = area.Position.Y; y < area.End.Y; y += th)
			{
				float w = Mathf.Min(tw, area.End.X - x), h = Mathf.Min(th, area.End.Y - y);
				float fu = w / tw, fv = h / th;
				var src = new Rect2(GrainRegion.Position, new Vector2(GrainRegion.Size.X * fu, GrainRegion.Size.Y * fv));
				var dst = new Rect2(x, y, w, h);
				Grain!.DrawRectRegion(canvas, dst, src, mod);
			}
		}
	}

	/// <summary>Closed outline of <paramref name="r"/> with each point nudged by up to <paramref name="amp"/>; deterministic for (size, seed).</summary>
	public static Vector2[] TornOutline(Rect2 r, float amp, float step, int seed)
	{
		var pts = new System.Collections.Generic.List<Vector2>();
		if (amp <= 0.01f || step < 4f)
		{
			pts.AddRange(new[] { r.Position, new Vector2(r.End.X, r.Position.Y), r.End, new Vector2(r.Position.X, r.End.Y) });
			return pts.ToArray();
		}
		int n = 0;
		void Edge(Vector2 a, Vector2 b, Vector2 normal)
		{
			float len = a.DistanceTo(b);
			int k = Mathf.Max(2, Mathf.RoundToInt(len / step));
			for (int i = 0; i < k; i++)
			{
				float t = (float)i / k;
				float along = i == 0 ? 0f : Noise(seed, n++) * step * 0.3f / len;
				float off = Noise(seed, n++) * amp;
				pts.Add(a.Lerp(b, Mathf.Clamp(t + along, 0f, 0.999f)) + normal * off);
			}
		}
		var tl = r.Position; var tr = new Vector2(r.End.X, r.Position.Y); var br = r.End; var bl = new Vector2(r.Position.X, r.End.Y);
		Edge(tl, tr, Vector2.Up); Edge(tr, br, Vector2.Right); Edge(br, bl, Vector2.Down); Edge(bl, tl, Vector2.Left);
		return pts.ToArray();
	}

	/// <summary>Stable hash noise in [-1, 1].</summary>
	public static float Noise(int seed, int i)
	{
		uint h = unchecked((uint)i * 2654435761u ^ (uint)seed * 40503u);
		h ^= h >> 13; h = unchecked(h * 1274126177u); h ^= h >> 16;
		return (h & 0xFFFF) / 32767.5f - 1f;
	}
}
