using System;
using Godot;

namespace YokaiFighters.Ui;

/// <summary>
/// YOK-238: the node's name tag, a scrap of torn rice paper: an irregular outline (deterministic from <see cref="Seed"/>), a soft shadow and a thin
/// ink edge. The text is live labels inside it; this only draws the paper.
/// </summary>
public partial class MapTag : PanelContainer
{
	[Export] public uint Seed { get; set; }

	public override void _Ready() { MouseFilter = MouseFilterEnum.Ignore; SetMargins(10, 2, 10, 5); }

	/// <summary>The paper's inner margins around the text (the drawn paper is the whole control; the style box only keeps the text off its edge).</summary>
	public void SetMargins(float left, float top, float right, float bottom) =>
		AddThemeStyleboxOverride("panel", new StyleBoxEmpty { ContentMarginLeft = left, ContentMarginTop = top, ContentMarginRight = right, ContentMarginBottom = bottom });
	public override void _Notification(int what) { if (what == NotificationResized) QueueRedraw(); }

	Vector2[] Outline()
	{
		var pts = new System.Collections.Generic.List<Vector2>();
		float w = Size.X, h = Size.Y;
		int nx = Math.Max(3, (int)(w / 16f)), ny = Math.Max(2, (int)(h / 16f));
		float J(int k, float amp) => (MapLayout.Unit(Seed, k) - 0.5f) * 2f * amp;
		for (int i = 0; i <= nx; i++) pts.Add(new Vector2(w * i / nx + (i == 0 || i == nx ? 0 : J(i, 1.5f)), J(40 + i, 1.6f)));
		for (int j = 1; j <= ny; j++) pts.Add(new Vector2(w + J(80 + j, 1.8f), h * j / ny));
		for (int i = nx - 1; i >= 0; i--) pts.Add(new Vector2(w * i / nx + J(120 + i, 1.5f), h + J(160 + i, 2.6f)));
		for (int j = ny - 1; j >= 1; j--) pts.Add(new Vector2(J(200 + j, 1.8f), h * j / ny));
		return pts.ToArray();
	}

	public override void _Draw()
	{
		if (Size.X < 8f || Size.Y < 8f) return;
		var pts = Outline();
		var shadow = new Vector2[pts.Length];
		for (int i = 0; i < pts.Length; i++) shadow[i] = pts[i] + new Vector2(2f, 3.5f);
		DrawColoredPolygon(shadow, new Color(0.1f, 0.06f, 0.02f, 0.28f));
		DrawColoredPolygon(pts, MapNodePalette.Paper);
		var edge = new Vector2[pts.Length + 1];
		pts.CopyTo(edge, 0); edge[^1] = pts[0];
		DrawPolyline(edge, new Color(MapNodePalette.Ink, 0.92f), 2.2f, true);
	}
}
