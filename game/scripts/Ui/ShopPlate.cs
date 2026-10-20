using Godot;

namespace YokaiFighters.Ui;

/// <summary>Draws the shop's ledger slip plate (YOK-159) at its size as a nine-slice; margins are source pixels (52/72/72/48), <see cref="PlateScale"/> scales them with the plate.</summary>
public partial class ShopPlate : Control
{
	[Export] public float PlateScale { get; set; } = 0.6f;

	public override void _Ready() { MouseFilter = MouseFilterEnum.Ignore; }

	public override void _Draw() => ShopArt.NineSlice(this, ShopArt.Slip, new Rect2(Vector2.Zero, Size), 52, 72, 72, 48, PlateScale);
}
