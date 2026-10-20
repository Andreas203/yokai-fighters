using System;
using Godot;

namespace YokaiFighters.Ui;

/// <summary>
/// The shop's wooden cabinet (YOK-159, docs/design/screens/rough/merchant-shop.png rev 3): side panels, crown and
/// plinth, a darker back wall with plank lines, and each board drawn as a top surface (back edge narrower, so it reads as
/// depth) plus a front lip. Pure decoration: it knows how many shelves are visible and, when more items exist than the
/// shelves hold, where the visible page sits (the rod on the right). Items are its children.
/// </summary>
public partial class ShopCabinet : Control
{
	public const float Width = 718, RowH = 180, Crown = 22, Plinth = 23;
	public const int Columns = 3, MaxTiers = 3;
	static readonly Color Ink = new("1D1B21"), Wall = new("3E2B1D"), WallLine = new("4A3424"), Frame = new("5A3E2A"), FrameDark = new("46301F"), Board = new("93673F"), Lip = new("6B4528");

	int _tiers = MaxTiers, _first, _total;

	/// <summary>Shelves drawn (2 or 3: 6 items fit two, 9 fill three, none shows three bare ones).</summary>
	public int Tiers => _tiers;
	public static float HeightFor(int tiers) => Crown + RowH * tiers + Plinth;

	public void Show(int tiers, int firstVisibleRow, int totalRows)
	{
		_tiers = Math.Clamp(tiers, 1, MaxTiers); _first = firstVisibleRow; _total = totalRows;
		CustomMinimumSize = new Vector2(Width, HeightFor(_tiers));
		Size = CustomMinimumSize;
		QueueRedraw();
	}

	/// <summary>Where an item sits in the cabinet: the item control's top-left for a column and a visible shelf (0 = top).</summary>
	public static Vector2 ItemPos(int column, int shelf) => new(148 + 210 * column - 80, 30 + RowH * shelf);

	public override void _Ready() { MouseFilter = MouseFilterEnum.Ignore; }

	void Box(Rect2 r, Color fill, float radius = 0)
	{
		var b = new StyleBoxFlat { BgColor = fill, BorderColor = Ink, CornerRadiusBottomLeft = (int)radius, CornerRadiusBottomRight = (int)radius, CornerRadiusTopLeft = (int)radius, CornerRadiusTopRight = (int)radius };
		b.SetBorderWidthAll(2);
		DrawStyleBox(b, r);
	}

	public override void _Draw()
	{
		float h = HeightFor(_tiers), inner = Crown + RowH * _tiers;
		Box(new Rect2(0, 6, 40, h - 6), Frame);
		Box(new Rect2(Width - 40, 6, 40, h - 6), Frame);
		DrawRect(new Rect2(40, Crown, Width - 80, RowH * _tiers), Wall);
		for (float x = 110; x < Width - 40; x += 70) DrawLine(new Vector2(x, Crown), new Vector2(x, inner), WallLine, 2f);
		for (int i = 0; i < _tiers; i++)
		{
			float y = 128 + RowH * i;
			DrawRect(new Rect2(40, y + 32, Width - 80, 12), new Color(0, 0, 0, 0.32f)); // shadow under the board
			var top = new[] { new Vector2(52, y), new Vector2(Width - 52, y), new Vector2(Width - 40, y + 24), new Vector2(40, y + 24) };
			DrawColoredPolygon(top, Board);
			DrawPolyline(new[] { top[0], top[1], top[2], top[3], top[0] }, Ink, 2f);
			Box(new Rect2(40, y + 24, Width - 80, 8), Lip);
		}
		Box(new Rect2(0, 0, Width, Crown), Frame, 3);
		Box(new Rect2(-4, h - Plinth, Width + 8, Plinth), FrameDark, 3);
		if (_total > _tiers)
		{
			// the rod on the right: position of the visible shelves among all of them, and a "more below" cue
			float top = Crown + 10, span = RowH * _tiers - 20, thumb = span * _tiers / _total;
			DrawRect(new Rect2(Width - 26, top, 8, span), new Color(0, 0, 0, 0.4f));
			float ty = top + (span - thumb) * (_total - _tiers == 0 ? 0 : (float)_first / (_total - _tiers));
			Box(new Rect2(Width - 28, ty, 12, thumb), new Color("F2A23A"), 4);
			if (_first + _tiers < _total) DrawColoredPolygon(new[] { new Vector2(Width / 2 - 14, inner - 16), new Vector2(Width / 2 + 14, inner - 16), new Vector2(Width / 2, inner - 4) }, new Color("F2A23A"));
			if (_first > 0) DrawColoredPolygon(new[] { new Vector2(Width / 2 - 14, Crown + 16), new Vector2(Width / 2 + 14, Crown + 16), new Vector2(Width / 2, Crown + 4) }, new Color("F2A23A"));
		}
	}
}
