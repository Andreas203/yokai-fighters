namespace YokaiFighters.Ui;

/// <summary>The shop's scroll framing (docs/design/screens/rough/merchant-shop.png): the rest stop's handscroll with one hanging scroll, "Your slots".</summary>
public partial class ShopScreenDecor : RestStopScreenDecor
{
	protected override string[] HangerNames => new[] { "SlotsHanger" };
}
