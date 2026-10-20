using System;
using Godot;

namespace YokaiFighters.Ui;

/// <summary>
/// The merchant's speech bubble (YOK-159, S2, S3): the Astra bubble plate (nine-slice 56 each side, 640x200) with its tail
/// plate pointing down-left to the portrait. The tail is drawn first and overlaps the body's bottom edge by 10 px so the
/// body's bottom border covers the seam (the tail art has no top ink line). The line is the child label <c>MerchantLine</c>.
/// </summary>
public partial class ShopBubble : Control
{
	public const float BodyH = 143, K = 0.7f, TailX = 36, SeamOverlap = 10;

	public override void _Ready() { MouseFilter = MouseFilterEnum.Ignore; }

	public override void _Draw()
	{
		var tail = ShopArt.BubbleTail;
		DrawTextureRect(tail, new Rect2(TailX, BodyH - SeamOverlap, tail.GetWidth() * K, tail.GetHeight() * K), false);
		ShopArt.NineSlice(this, ShopArt.Bubble, new Rect2(0, 0, Size.X, BodyH), 56, 56, 56, 56, K);
	}
}
