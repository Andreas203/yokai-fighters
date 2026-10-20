using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>
/// Windowed screenshot tool (not a test) for the shop (YOK-104, rebuilt for YOK-159 rev 3): greeting, a focused item with
/// the merchant speaking it, the ledger slip open, a bought item, cannot afford, slot full, the modifier target step, the
/// release confirm, the T5 lock, shelf empty, and a shelf with more items than the cabinet shows.
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
			var three = new[] { (SpecialSlot.A, "test-spirit-wave", 1), (SpecialSlot.B, "test-rising-talisman", 1), (SpecialSlot.C, "test-foxfire", 2) };

			var hello = Shop(pool, 75, three);
			await Shot(dir, "shop_greeting" + suffix, hello, _ => { });

			var stocked = Shop(pool, 75, (SpecialSlot.A, "test-spirit-wave", 1), (SpecialSlot.B, "test-rising-talisman", 2));
			await Shot(dir, "shop_stock" + suffix, stocked, s => s.Cards["Modifier_test-kappa-wisp"].GrabFocus());

			var slip = Shop(pool, 75, (SpecialSlot.A, "test-spirit-wave", 1), (SpecialSlot.B, "test-rising-talisman", 2));
			await Shot(dir, "shop_slip" + suffix, slip, s => { s.Cards["Special_test-slippery-skin"].GrabFocus(); s.ToggleFrameData(); });

			var bought = Shop(pool, 120, (SpecialSlot.A, "test-spirit-wave", 1), (SpecialSlot.B, "test-rising-talisman", 2));
			await Shot(dir, "shop_bought" + suffix, bought, s => { s.BuySpecial("test-river-grab"); });

			var broke = Shop(pool, 30, two);
			await Shot(dir, "shop_cannot_afford" + suffix, broke, s => { s.Cards["Special_test-river-grab"].GrabFocus(); s.BuySpecial("test-river-grab"); });

			var full = Shop(pool, 120, three);
			await Shot(dir, "shop_slot_full" + suffix, full, s => { s.Cards["Special_test-slippery-skin"].GrabFocus(); s.BuySpecial("test-slippery-skin"); });

			var pick = Shop(pool, 120, two);
			await Shot(dir, "shop_modifier_picker" + suffix, pick, s => s.BeginPick("test-kappa-wisp"));

			var rel = Shop(pool, 200, (SpecialSlot.A, "test-spirit-wave", 1), (SpecialSlot.B, "test-rising-talisman", 1), (SpecialSlot.C, "test-foxfire", 2));
			await Shot(dir, "shop_release" + suffix, rel, s => { s.BuySpecial("test-river-grab"); s.GetNode<Control>("SlotRows/Slot_D").GrabFocus(); s.ReleaseSlot(SpecialSlot.D); });

			var last = Shop(pool, 120, (SpecialSlot.A, "test-spirit-wave", 1));
			await Shot(dir, "shop_last_special" + suffix, last, s => { s.GetNode<Control>("SlotRows/Slot_A").GrabFocus(); s.ReleaseSlot(SpecialSlot.A); });

			var empty = Shop(ShopScreenFixtures.StockedPool(modifiers: false, specials: false), 75, two); // YOK-103: the real pool stocks Kappa modifiers now
			await Shot(dir, "shop_empty" + suffix, empty, _ => { });

			var wide = Shop(ShopScreenFixtures.WidePool(3, 3), 400, two);
			await Shot(dir, "shop_overflow" + suffix, wide, s => { s.Cards["Modifier_test-wide-md0"].GrabFocus(); });
		}
		finally { FightScene.Sources = saved; }
		GetTree().Quit();
	}

	static RunSession Shop(AbilityPool pool, int coins, params (SpecialSlot, string, int)[] slots)
	{
		var s = ShopScreenFixtures.At(pool, coins, slots);
		s.Merchant!.Choose(MerchantChoice.Shop);
		return s;
	}

	private async System.Threading.Tasks.Task Shot(string dir, string name, RunSession s, System.Action<ShopScreen> setup)
	{
		var screen = GD.Load<PackedScene>("res://scenes/ui/shop.tscn").Instantiate<ShopScreen>();
		screen.Bind(s);
		AddChild(screen);
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		setup(screen);
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		GetViewport().GetTexture().GetImage().SavePng($"{dir}/{name}.png");
		RemoveChild(screen);
		screen.QueueFree();
	}
}
