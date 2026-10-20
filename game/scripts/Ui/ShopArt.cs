using Godot;

namespace YokaiFighters.Ui;

/// <summary>
/// The Astra shop plates (YOK-159, assets/specs/ui-screen-plates.md "YOK-159 shop plates"). One place for the textures and
/// the nine-slice draw, which scales the margins with the plate (a plate drawn at 0.3 keeps its string hole and border
/// proportions) instead of Godot's NinePatch, whose margins are in source pixels.
/// </summary>
public static class ShopArt
{
	const string Dir = "res://assets/generated/ui/";
	public static Texture2D Cabinet => Load("shop-cabinet.png");
	public static Texture2D Tag => Load("price-tag.png");
	public static Texture2D Bubble => Load("speech-bubble.png");
	public static Texture2D BubbleTail => Load("speech-bubble-tail.png");
	public static Texture2D Slip => Load("ledger-slip.png");
	public static Texture2D Portrait => Load("merchant-portrait.png");
	public static Texture2D Backdrop => Load("shop-backdrop.png");

	static Texture2D Load(string file) => GD.Load<Texture2D>(Dir + file);

	/// <summary>Draws <paramref name="tex"/> into <paramref name="dst"/> as a nine-slice with source margins l/t/r/b, each scaled by <paramref name="k"/>; the centre and edges stretch.</summary>
	public static void NineSlice(CanvasItem ci, Texture2D tex, Rect2 dst, float l, float t, float r, float b, float k)
	{
		float w = tex.GetWidth(), h = tex.GetHeight();
		float dl = l * k, dt = t * k, dr = r * k, db = b * k;
		if (dst.Size.X < dl + dr) { float s = dst.Size.X / (dl + dr); dl *= s; dr *= s; }
		if (dst.Size.Y < dt + db) { float s = dst.Size.Y / (dt + db); dt *= s; db *= s; }
		float[] sx = { 0, l, w - r, w }, sy = { 0, t, h - b, h };
		float[] dx = { dst.Position.X, dst.Position.X + dl, dst.End.X - dr, dst.End.X }, dy = { dst.Position.Y, dst.Position.Y + dt, dst.End.Y - db, dst.End.Y };
		for (int iy = 0; iy < 3; iy++)
			for (int ix = 0; ix < 3; ix++)
				ci.DrawTextureRectRegion(tex, new Rect2(dx[ix], dy[iy], dx[ix + 1] - dx[ix], dy[iy + 1] - dy[iy]), new Rect2(sx[ix], sy[iy], sx[ix + 1] - sx[ix], sy[iy + 1] - sy[iy]));
	}
}
