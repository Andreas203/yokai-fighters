using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>
/// Windowed screenshot tool (not a test) for YOK-104: the shop with stock (fixture pool), shelf empty (the real default
/// pool), the modifier target picker, a refused card, and the last special locked.
/// Run: godot --path game res://tests/shop_capture.tscn --resolution 1920x1080 -- out=&lt;dir&gt; [suffix=_720]
/// </summary>
public partial class ShopCapture : Node
{
	public override async void _Ready()
	{
		string dir = ".", suffix = "";
		foreach (string a in OS.GetCmdlineUserArgs()) { if (a.StartsWith("out=")) dir = a[4..]; if (a.StartsWith("suffix=")) suffix = a[7..]; }
		var saved = FightScene.Sources;
		FightScene.Sources = FightScene.FixtureSources;
		try
		{
			var pool = ShopScreenFixtures.StockedPool();
			var two = ShopScreenFixtures.Starters;
			// stock: 75 coins, a Lv 2 move (Tanuki target), one Kappa special already bought
			var stocked = ShopScreenFixtures.At(pool, 75, (SpecialSlot.A, "test-spirit-wave", 1), (SpecialSlot.B, "test-rising-talisman", 2));
			stocked.Merchant!.Choose(MerchantChoice.Shop);
			await Shot(dir, "shop_stock" + suffix, stocked, s => { s.BuySpecial("test-river-grab"); s.Refresh("Special_test-slippery-skin"); });
			var broke = ShopScreenFixtures.At(pool, 30, two);
			broke.Merchant!.Choose(MerchantChoice.Shop);
			await Shot(dir, "shop_cannot_afford" + suffix, broke, s => s.BuySpecial("test-river-grab"));
			var pick = ShopScreenFixtures.At(pool, 120, two);
			pick.Merchant!.Choose(MerchantChoice.Shop);
			await Shot(dir, "shop_modifier_picker" + suffix, pick, s => s.BeginPick("test-kappa-wisp"));
			var last = ShopScreenFixtures.At(pool, 120, (SpecialSlot.A, "test-spirit-wave", 1));
			last.Merchant!.Choose(MerchantChoice.Shop);
			await Shot(dir, "shop_last_special" + suffix, last, _ => { });
			var empty = ShopScreenFixtures.At(ShopScreenFixtures.StockedPool(modifiers: false, specials: false), 75, two); // YOK-103: the real pool stocks Kappa modifiers now
			empty.Merchant!.Choose(MerchantChoice.Shop);
			await Shot(dir, "shop_empty" + suffix, empty, _ => { });
		}
		finally { FightScene.Sources = saved; }
		GetTree().Quit();
	}

	private async System.Threading.Tasks.Task Shot(string dir, string name, RunSession s, System.Action<ShopScreen> setup)
	{
		var screen = GD.Load<PackedScene>("res://scenes/ui/shop.tscn").Instantiate<ShopScreen>();
		screen.Bind(s);
		AddChild(screen);
		setup(screen);
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		GetViewport().GetTexture().GetImage().SavePng($"{dir}/{name}.png");
		RemoveChild(screen);
		screen.QueueFree();
	}
}
