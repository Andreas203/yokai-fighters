using System;
using Godot;
using YokaiFighters.Fight;

namespace YokaiFighters.Ui;

/// <summary>
/// YOK-58 title-screen yokai mask hung from a cord (V1 paper-talisman look). Placeholder art is Godot-drawn
/// (Kitsune: white fox with red markings, Oni: red with horns and fangs, Kappa: green with a water dish);
/// a generated PNG at <c>res://assets/generated/ui/title/mask-&lt;yokai&gt;.png</c> replaces the drawing when it exists
/// (or assign <see cref="Art"/>). The node's rect is the cord plus the mask; it sways about the top-centre pivot.
/// <see cref="Lit"/> brightens the mask, adds a halo, sways wider and shows the name plaque in persimmon.
/// Menu-only motion, so it uses delta time rather than the fight tick. Never shows the Tanuki (story reveal).
/// </summary>
[Tool]
public partial class YokaiMask : Control
{
	public const string ArtDir = "res://assets/generated/ui/title/";

	private string _yokai = "kitsune";
	private bool _lit;
	private float _t, _amp = 1.2f, _glow;
	private Texture2D? _art;
	private bool _artLoaded;

	[Export] public string Yokai { get => _yokai; set { _yokai = value; _artLoaded = false; QueueRedraw(); } }
	[Export] public float CordLength { get; set; } = 70f;
	[Export] public float Phase { get; set; }
	/// <summary>Generated art slot; left empty it is loaded from <see cref="ArtPath"/> if that file exists.</summary>
	[Export] public Texture2D? Art { get => _art; set { _art = value; _artLoaded = value != null; QueueRedraw(); } }
	[Export] public bool Lit { get => _lit; set { _lit = value; QueueRedraw(); } }

	public string ArtPath => $"{ArtDir}mask-{_yokai}.png";
	public bool UsesGeneratedArt => _art != null;
	public float SwayDegrees => Mathf.RadToDeg(Rotation);
	public float Glow => _glow;

	private static readonly Color Ink = FightHud.Ink, Seal = FightHud.Seal, Paper = FightHud.Paper;

	public override void _Ready()
	{
		PivotOffset = new Vector2(Size.X / 2f, 0);
		TryLoadArt();
	}

	private void TryLoadArt()
	{
		if (_artLoaded) return;
		_artLoaded = true;
		if (ResourceLoader.Exists(ArtPath)) _art = GD.Load<Texture2D>(ArtPath);
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized) { PivotOffset = new Vector2(Size.X / 2f, 0); QueueRedraw(); }
	}

	public override void _Process(double delta)
	{
		_t += (float)delta;
		float targetAmp = _lit ? 3.4f : 1.2f;
		_amp = Mathf.Lerp(_amp, targetAmp, 1f - Mathf.Exp(-6f * (float)delta));
		float g = Mathf.Lerp(_glow, _lit ? 1f : 0f, 1f - Mathf.Exp(-8f * (float)delta));
		bool glowChanged = Mathf.Abs(g - _glow) > 0.002f;
		_glow = g;
		Rotation = Mathf.DegToRad(_amp * Mathf.Sin(_t * (_lit ? 1.9f : 1.1f) + Phase));
		if (glowChanged) QueueRedraw();
	}

	private Vector2 MaskCentre => new(Size.X / 2f, CordLength + MaskSize.Y / 2f);
	private Vector2 MaskSize => new(Mathf.Min(Size.X - 24f, 170f), Mathf.Min(Size.X - 24f, 170f) * 1.18f);

	public override void _Draw()
	{
		TryLoadArt();
		var c = MaskCentre; var sz = MaskSize; float hx = sz.X / 2f, hy = sz.Y / 2f;
		// cord and knot
		DrawLine(new Vector2(c.X, 0), new Vector2(c.X, CordLength - 4), new Color(0.72f, 0.16f, 0.14f), 5f);
		DrawCircle(new Vector2(c.X, CordLength - 4), 7f, Seal);
		// halo when lit
		if (_glow > 0.01f)
			for (int i = 4; i >= 1; i--)
				DrawCircle(c, hy * (1.0f + i * 0.13f), new Color(0.85f, 0.4f, 0.15f, 0.13f * _glow));
		float dim = _lit ? 1f : 0.82f;
		if (_art != null)
		{
			DrawTextureRect(_art, new Rect2(c - new Vector2(hx, hy), sz), false, new Color(dim, dim, dim));
		}
		else
		{
			switch (_yokai)
			{
				case "oni": DrawOni(c, hx, hy, dim); break;
				case "kappa": DrawKappa(c, hx, hy, dim); break;
				default: DrawKitsune(c, hx, hy, dim); break;
			}
		}
		// name plaque
		var plaque = new Rect2(new Vector2(c.X - 62, c.Y + hy + 10), new Vector2(124, 34));
		DrawRect(plaque, _lit ? Paper : new Color(Paper, 0.78f));
		DrawRect(plaque, Ink, false, 3f);
		DrawString(ThemeDB.FallbackFont, plaque.Position + new Vector2(0, 25), _yokai.ToUpperInvariant(), HorizontalAlignment.Center, plaque.Size.X, 22, _lit ? Seal : Ink);
	}

	private static Vector2[] Ellipse(Vector2 c, float rx, float ry, int n = 28, float skewTop = 1f)
	{
		var p = new Vector2[n];
		for (int i = 0; i < n; i++)
		{
			float a = Mathf.Tau * i / n;
			float yy = Mathf.Sin(a);
			p[i] = c + new Vector2(Mathf.Cos(a) * rx * (yy < 0 ? skewTop : 1f), yy * ry);
		}
		return p;
	}

	private static Vector2[] Pts(Vector2 c, float hx, float hy, params float[] xy)
	{
		var p = new Vector2[xy.Length / 2];
		for (int i = 0; i < p.Length; i++) p[i] = c + new Vector2(xy[i * 2] * hx, xy[i * 2 + 1] * hy);
		return p;
	}

	private void Poly(Vector2[] pts, Color fill, Color? outline = null, float w = 4f)
	{
		DrawColoredPolygon(pts, fill);
		var closed = new Vector2[pts.Length + 1]; pts.CopyTo(closed, 0); closed[^1] = pts[0];
		DrawPolyline(closed, outline ?? Ink, w, true);
	}

	private static Color Dim(Color c, float k) => new(c.R * k, c.G * k, c.B * k, c.A);

	private void DrawKitsune(Vector2 c, float hx, float hy, float k)
	{
		var white = Dim(new Color(0.96f, 0.94f, 0.9f), k); var red = Dim(new Color(0.78f, 0.15f, 0.12f), k);
		Poly(Pts(c, hx, hy, -0.95f, -1f, -0.5f, -0.5f, 0f, -0.58f, 0.5f, -0.5f, 0.95f, -1f, 0.92f, -0.05f, 0.58f, 0.5f, 0.14f, 1f, -0.14f, 1f, -0.58f, 0.5f, -0.92f, -0.05f), white);
		// inner ears
		DrawColoredPolygon(Pts(c, hx, hy, -0.82f, -0.86f, -0.56f, -0.55f, -0.62f, -0.7f), red);
		DrawColoredPolygon(Pts(c, hx, hy, 0.82f, -0.86f, 0.56f, -0.55f, 0.62f, -0.7f), red);
		// forehead mark
		DrawColoredPolygon(Pts(c, hx, hy, 0f, -0.52f, -0.12f, -0.22f, 0f, -0.08f, 0.12f, -0.22f), red);
		// cheek stripes (two per side) tapering to the snout
		foreach (float s in new[] { -1f, 1f })
		{
			DrawColoredPolygon(Pts(c, hx, hy, s * 0.92f, -0.05f, s * 0.5f, 0.12f, s * 0.9f, 0.2f), red);
			DrawColoredPolygon(Pts(c, hx, hy, s * 0.72f, 0.3f, s * 0.38f, 0.38f, s * 0.64f, 0.45f), red);
			// slanted eye slits
			DrawColoredPolygon(Pts(c, hx, hy, s * 0.62f, -0.34f, s * 0.2f, -0.12f, s * 0.24f, -0.2f, s * 0.58f, -0.44f), Ink);
			DrawPolyline(Pts(c, hx, hy, s * 0.66f, -0.36f, s * 0.2f, -0.1f), red, 4f, true);
		}
		DrawColoredPolygon(Pts(c, hx, hy, -0.1f, 0.86f, 0.1f, 0.86f, 0f, 1f), Ink); // nose
		DrawLine(c + new Vector2(0, hy * 0.5f), c + new Vector2(0, hy * 0.84f), Ink, 3f);
	}

	private void DrawOni(Vector2 c, float hx, float hy, float k)
	{
		var skin = Dim(new Color(0.74f, 0.2f, 0.16f), k); var horn = Dim(new Color(0.93f, 0.89f, 0.78f), k); var gold = Dim(new Color(0.95f, 0.78f, 0.3f), k);
		DrawColoredPolygon(Pts(c, hx, hy, -0.55f, -0.5f, -0.78f, -1.12f, -0.2f, -0.7f), horn); // horns behind the face
		DrawColoredPolygon(Pts(c, hx, hy, 0.55f, -0.5f, 0.78f, -1.12f, 0.2f, -0.7f), horn);
		DrawPolyline(Pts(c, hx, hy, -0.55f, -0.5f, -0.78f, -1.12f, -0.2f, -0.7f), Ink, 4f, true);
		DrawPolyline(Pts(c, hx, hy, 0.55f, -0.5f, 0.78f, -1.12f, 0.2f, -0.7f), Ink, 4f, true);
		Poly(Pts(c, hx, hy, -0.9f, -0.45f, -0.55f, -0.7f, 0f, -0.62f, 0.55f, -0.7f, 0.9f, -0.45f, 1f, 0.1f, 0.78f, 0.7f, 0.36f, 1f, -0.36f, 1f, -0.78f, 0.7f, -1f, 0.1f), skin);
		foreach (float s in new[] { -1f, 1f })
		{
			DrawColoredPolygon(Pts(c, hx, hy, s * 0.8f, -0.3f, s * 0.18f, -0.34f, s * 0.2f, -0.12f, s * 0.7f, -0.04f), Ink);   // glaring eye socket
			DrawColoredPolygon(Pts(c, hx, hy, s * 0.5f, -0.24f, s * 0.3f, -0.22f, s * 0.34f, -0.1f, s * 0.52f, -0.12f), gold);
			DrawLine(c + new Vector2(s * hx * 0.9f, -hy * 0.46f), c + new Vector2(s * hx * 0.1f, -hy * 0.26f), Ink, 8f); // heavy brow
		}
		DrawColoredPolygon(Pts(c, hx, hy, -0.6f, 0.45f, 0.6f, 0.45f, 0.52f, 0.85f, -0.52f, 0.85f), Ink); // mouth
		foreach (float s in new[] { -1f, 1f }) // fangs
			DrawColoredPolygon(Pts(c, hx, hy, s * 0.5f, 0.45f, s * 0.3f, 0.45f, s * 0.4f, 0.7f), horn);
		DrawColoredPolygon(Pts(c, hx, hy, -0.12f, 0.12f, 0.12f, 0.12f, 0f, 0.3f), Dim(new Color(0.5f, 0.1f, 0.1f), k)); // nose
	}

	private void DrawKappa(Vector2 c, float hx, float hy, float k)
	{
		var skin = Dim(new Color(0.3f, 0.52f, 0.4f), k); var dish = Dim(new Color(0.78f, 0.86f, 0.8f), k); var beak = Dim(new Color(0.9f, 0.72f, 0.3f), k);
		Poly(Ellipse(c + new Vector2(0, hy * 0.05f), hx * 0.98f, hy * 0.95f, 32), skin);
		// water dish crown with a hair ring
		DrawColoredPolygon(Ellipse(c + new Vector2(0, -hy * 0.78f), hx * 0.52f, hy * 0.17f), dish);
		DrawPolyline(Close(Ellipse(c + new Vector2(0, -hy * 0.78f), hx * 0.52f, hy * 0.17f)), Ink, 4f, true);
		DrawColoredPolygon(Ellipse(c + new Vector2(0, -hy * 0.78f), hx * 0.3f, hy * 0.08f), Dim(new Color(0.35f, 0.6f, 0.7f), k));
		foreach (float s in new[] { -1f, 1f })
		{
			DrawColoredPolygon(Ellipse(c + new Vector2(s * hx * 0.4f, -hy * 0.22f), hx * 0.22f, hy * 0.17f, 20), Paper);
			DrawPolyline(Close(Ellipse(c + new Vector2(s * hx * 0.4f, -hy * 0.22f), hx * 0.22f, hy * 0.17f, 20)), Ink, 4f, true);
			DrawCircle(c + new Vector2(s * hx * 0.4f, -hy * 0.2f), hx * 0.08f, Ink);
		}
		// beak
		Poly(Pts(c, hx, hy, -0.55f, 0.28f, 0.55f, 0.28f, 0.42f, 0.7f, 0f, 0.82f, -0.42f, 0.7f), beak);
		DrawLine(c + new Vector2(-hx * 0.5f, hy * 0.5f), c + new Vector2(hx * 0.5f, hy * 0.5f), Ink, 3f);
		DrawCircle(c + new Vector2(-hx * 0.12f, hy * 0.38f), 4f, Ink); DrawCircle(c + new Vector2(hx * 0.12f, hy * 0.38f), 4f, Ink);
	}

	private static Vector2[] Close(Vector2[] p) { var q = new Vector2[p.Length + 1]; p.CopyTo(q, 0); q[^1] = p[0]; return q; }
}
