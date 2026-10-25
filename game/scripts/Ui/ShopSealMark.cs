using Godot;

namespace YokaiFighters.Ui;

/// <summary>T5's mark on the last special's row: a tied cord across it and a red seal (drawn shapes; the row keeps its text).</summary>
public partial class ShopSealMark : Control
{
	public override void _Ready() { MouseFilter = MouseFilterEnum.Ignore; }

	public override void _Draw()
	{
		float w = Size.X, y = Size.Y - 5;
		var red = new Color("BD2E26");
		DrawLine(new Vector2(4, y), new Vector2(w - 34, y - 1), red, 3f); // the tied cord along the row's foot
		DrawLine(new Vector2(w - 34, y - 1), new Vector2(w - 4, y + 2), red, 3f);
		var seal = new Rect2(w - 34, y - 14, 20, 20);
		DrawRect(seal, red);
		DrawRect(seal, new Color("1D1B21"), false, 2f);
	}
}
