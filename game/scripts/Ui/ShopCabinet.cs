using System;
using Godot;

namespace YokaiFighters.Ui;

/// <summary>
/// The shop's wooden cabinet (YOK-159): the Astra plate <c>shop-cabinet.png</c> (800x704, fixed art, three shelves) drawn at
/// <see cref="Scale"/>. Pure decoration: items stand on the boards at the measured offsets below (saved pixels of the plate;
/// row 3 is shorter than rows 1 and 2, so every row has its own numbers). When more items exist than the three shelves hold
/// it draws where the visible page sits (a rod in the right side wall and more-above / more-below arrows). Items are its children.
/// </summary>
public partial class ShopCabinet : Control
{
	/// <summary>On-screen size of the plate over its saved pixels.</summary>
	public const float Scale = 0.9f, ArtW = 800, ArtH = 704;
	public const float Width = ArtW * Scale;
	public const int Columns = 3, MaxTiers = 3;
	/// <summary>Plate measurements in saved pixels: column centres (each 202 wide), item stand lines (board tops) and each row's cavity (top, bottom).</summary>
	public static readonly float[] ColumnX = { 198, 400, 602 }, StandY = { 236, 417, 575 };
	public static readonly (float Top, float Bottom)[] Cavity = { (101, 235), (285, 416), (468, 574) };
	public const float ColumnW = 202;
	static readonly Color Foxfire = new("F2A23A"), Ink = new("1D1B21");

	int _tiers = MaxTiers, _first, _total;

	/// <summary>Shelves drawn: always the plate's three (an unfilled shelf stays bare).</summary>
	public int Tiers => _tiers;
	public static float HeightFor(int tiers) => ArtH * Scale;

	public void Show(int tiers, int firstVisibleRow, int totalRows)
	{
		_tiers = Math.Clamp(tiers, 1, MaxTiers); _first = firstVisibleRow; _total = totalRows;
		CustomMinimumSize = new Vector2(Width, ArtH * Scale);
		Size = CustomMinimumSize;
		QueueRedraw();
	}

	/// <summary>Where an item sits in the cabinet: the item control's top-left for a column and a visible shelf (0 = top); its stand point lands on the board top.</summary>
	public static Vector2 ItemPos(int column, int shelf) => new(ColumnX[column] * Scale - ShopItem.W / 2, StandY[Math.Clamp(shelf, 0, MaxTiers - 1)] * Scale - ShopItem.StandY);

	public override void _Ready() { MouseFilter = MouseFilterEnum.Ignore; }

	public override void _Draw()
	{
		DrawTextureRect(ShopArt.Cabinet, new Rect2(0, 0, ArtW * Scale, ArtH * Scale), false);
		if (_total <= _tiers) return;
		// the rod in the right side wall (plate x 703..735): the visible page among all shelves, and more-above / more-below arrows
		float x = 719 * Scale, top = 104 * Scale, span = (574 - 104) * Scale, thumb = span * _tiers / _total;
		DrawRect(new Rect2(x - 4, top, 8, span), new Color(0, 0, 0, 0.45f));
		float ty = top + (span - thumb) * (_total - _tiers == 0 ? 0 : (float)_first / (_total - _tiers));
		var box = new StyleBoxFlat { BgColor = Foxfire, BorderColor = Ink, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4, CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4 };
		box.SetBorderWidthAll(2);
		DrawStyleBox(box, new Rect2(x - 6, ty, 12, thumb));
		float cx = ArtW / 2 * Scale;
		if (_first + _tiers < _total) DrawColoredPolygon(new[] { new Vector2(cx - 14, 632 * Scale), new Vector2(cx + 14, 632 * Scale), new Vector2(cx, 646 * Scale) }, Foxfire);
		if (_first > 0) DrawColoredPolygon(new[] { new Vector2(cx - 14, 96 * Scale), new Vector2(cx + 14, 96 * Scale), new Vector2(cx, 84 * Scale) }, Foxfire);
	}
}
