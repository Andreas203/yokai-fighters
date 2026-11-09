using Godot;

namespace YokaiFighters.Ui;

/// <summary>
/// YOK-109, YOK-238: the map's paper. Map v2 (designer, 2026-11-09): the same plain rice-paper handscroll the title screen's reason scroll
/// uses (<c>assets/generated/ui/title-reason-scroll.png</c>): thin ink outline, a wooden roller at each end with indigo caps and brass rings.
/// It is stretched to the whole scroll (many screens wide) as a nine-patch, so the rollers and the outline keep their shape. No silk mount,
/// no sheet seams, no row headings. It carries no information, so blank paper is all a hidden row shows. <see cref="MapScreen"/> sizes it
/// and slides it inside a clipping viewport.
/// </summary>
public partial class MapScroll : Control
{
	public static readonly Color PaperTone = new(0.945f, 0.89f, 0.76f);
	public const string TexturePath = "res://assets/generated/ui/title-reason-scroll.png";

	/// <summary>Width of the roller and the rolled paper on each end (the nine-patch's left / right margin); the walkable paper starts after it.</summary>
	public const float RollerWidth = 108f;
	/// <summary>The texture's own roller width in pixels; the roller is drawn <see cref="RollerWidth"/> / this wide (1.5x: as thick as the concept's, map.png).</summary>
	const float TexRoller = 72f;
	/// <summary>The roller caps and the paper edge's outline above and below the flat paper (the nine-patch's top / bottom margin).</summary>
	public const float TopInset = 52f, BottomInset = 50f;
	/// <summary>Where the walkable paper begins along x, for the screen's layout.</summary>
	public static float PaperLeft => RollerWidth;

	static Texture2D? _tex;
	static Texture2D? Tex => _tex ??= GD.Load<Texture2D>(TexturePath);

	public override void _Notification(int what) { if (what == NotificationResized) QueueRedraw(); }

	public override void _Draw()
	{
		if (Size.X < 2 * RollerWidth + 40f || Size.Y < TopInset + BottomInset + 40f) return;
		if (Tex is { } tex)
		{
			// a nine-patch drawn by hand: the texture's corners and edges, but the roller columns drawn wider than the texture has them (1.5x), so the rollers read as thick as the concept's
			float tw = tex.GetWidth(), th = tex.GetHeight();
			float[] sx = { 0f, TexRoller, tw - TexRoller, tw }, dx = { 0f, RollerWidth, Size.X - RollerWidth, Size.X };
			float[] sy = { 0f, TopInset, th - BottomInset, th }, dy = { 0f, TopInset, Size.Y - BottomInset, Size.Y };
			for (int ix = 0; ix < 3; ix++)
				for (int iy = 0; iy < 3; iy++)
					DrawTextureRectRegion(tex, new Rect2(dx[ix], dy[iy], dx[ix + 1] - dx[ix], dy[iy + 1] - dy[iy]), new Rect2(sx[ix], sy[iy], sx[ix + 1] - sx[ix], sy[iy + 1] - sy[iy]));
		}
		else DrawRect(new Rect2(RollerWidth, TopInset, Size.X - 2 * RollerWidth, Size.Y - TopInset - BottomInset), PaperTone); // texture missing: plain paper still reads
	}
}
