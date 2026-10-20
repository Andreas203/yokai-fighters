using System;
using Godot;

namespace YokaiFighters.Ui;

/// <summary>
/// The merchant's speech bubble (YOK-159, S2, S3): a brush-ink bubble with a tail toward the portrait's head. The line is
/// the child label <c>MerchantLine</c>; with no line the bubble hides itself.
/// </summary>
public partial class ShopBubble : Control
{
	public const float BodyH = 143;
	static readonly Color Ink = new("1D1B21"), Paper = new("F8F1E0");

	public override void _Ready() { MouseFilter = MouseFilterEnum.Ignore; }

	public override void _Draw()
	{
		var body = new StyleBoxFlat { BgColor = Paper, BorderColor = Ink, CornerRadiusBottomLeft = 22, CornerRadiusBottomRight = 22, CornerRadiusTopLeft = 22, CornerRadiusTopRight = 22 };
		body.SetBorderWidthAll(5);
		var tail = new[] { new Vector2(50, BodyH - 3), new Vector2(112, BodyH - 3), new Vector2(76, BodyH + 95) };
		DrawColoredPolygon(tail, Paper);
		DrawLine(tail[0], tail[2], Ink, 5f); DrawLine(tail[1], tail[2], Ink, 5f);
		DrawStyleBox(body, new Rect2(0, 0, Size.X, BodyH));
		DrawRect(new Rect2(54, BodyH - 6, 55, 8), Paper); // opens the body's outline where the tail joins
	}
}
