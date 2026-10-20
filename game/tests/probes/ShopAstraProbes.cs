using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;
using YokaiFighters.Story;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>Probes for YOK-159 shop Astra plates. Expected values: shop rules (R6/T5/A12) and plain layout containment.</summary>
public static class ShopAstraProbes
{
	static void OnFixtures(Action body)
	{
		var saved = FightScene.Sources; var savedRun = FightScene.Run;
		FightScene.Sources = FightScene.FixtureSources; FightScene.Run = null;
		try { body(); } finally { FightScene.Sources = saved; FightScene.Run = savedRun; }
	}

	static RunSession Shopping(AbilityPool pool, int coins)
	{
		var s = ShopScreenFixtures.At(pool, coins, ShopScreenFixtures.Starters);
		Assert.True(s.Merchant!.Choose(MerchantChoice.Shop).Ok, "shop chosen");
		return s;
	}

	static ShopScreen Open(Node runner, RunSession s)
	{
		var screen = GD.Load<PackedScene>("res://scenes/ui/shop.tscn").Instantiate<ShopScreen>();
		screen.Bind(s); runner.AddChild(screen); return screen;
	}

	static void Close(Node runner, ShopScreen s) { runner.RemoveChild(s); s.Free(); }

	static void Tap(Node n, Key k)
	{
		n.GetViewport().PushInput(new InputEventKey { Keycode = k, PhysicalKeycode = k, Pressed = true });
		n.GetViewport().PushInput(new InputEventKey { Keycode = k, PhysicalKeycode = k, Pressed = false });
	}

	static AbilityPool LongNamePool(string name)
	{
		var node = JsonNode.Parse(File.ReadAllText(Path.Combine(ContentPaths.FixturesDir, "rewards", "test-foxfire.json")))!.AsObject();
		node["id"] = "test-long"; node["name"] = name; node["source"] = "kappa"; node["rarity"] = "common"; node["locked"] = false;
		node["input"]!["kata"]!["slot"] = "C"; node["input"]!["kihon"]!["slot"] = "C";
		node["card"]!["plain"] = "Plain.";
		var sp = SpecialLoader.Parse(node.ToJsonString(), "test-long");
		var basePool = ShopScreenFixtures.StockedPool(false, false);
		return new AbilityPool(basePool.Specials.Append(sp), basePool.Modifiers);
	}

	static float TextW(Label l, string text) => l.GetThemeFont("font").GetStringSize(text, HorizontalAlignment.Left, -1, l.GetThemeFontSize("font_size")).X;

	[Test]
	public static void Probe_ZeroToTenItems_EveryScroll_ItemsInsideCabinet_FocusVisible(Node runner) => OnFixtures(() =>
	{
		for (int extra = 0; extra <= 10; extra++)
		{
			var screen = Open(runner, Shopping(ShopScreenFixtures.WidePool(extra, Math.Max(0, 10 - extra)), 1000));
			try
			{
				int n = screen.Items.Count;
				for (int step = 0; step < 8; step++)
				{
					var cab = screen.GetNode<Control>("Cabinet");
					foreach (var it in screen.Items)
					{
						int row = it.Index / 3 - screen.FirstShelf;
						Assert.True(it.Visible == (row >= 0 && row < 3), $"n={n} {it.Name} visibility vs row {row}");
						if (it.Visible) Assert.True(new Rect2(Vector2.Zero, cab.Size).Encloses(new Rect2(it.Position, it.Size)), $"n={n} {it.Name} inside cabinet");
					}
					if (n > 0) Assert.True(screen.GetViewport().GuiGetFocusOwner() is ShopItem { Visible: true }, $"n={n} focus visible, step {step}");
					Tap(screen, step < 5 ? Key.Down : Key.Pagedown);
				}
				for (int step = 0; step < 8; step++) Tap(screen, Key.Up);
				Assert.True(n == 0 || screen.FirstShelf == 0, $"n={n}: Up x8 returns to first shelf, got {screen.FirstShelf}");
			}
			finally { Close(runner, screen); }
		}
	});

	[Test]
	public static void Probe_LongName_FitsTagFace(Node runner) => OnFixtures(() =>
	{
		foreach (string name in new[] { "Reed Whistle", "Trickster's Angle" })
		{
			var screen = Open(runner, Shopping(LongNamePool(name), 1000));
			try
			{
				var it = screen.Items.First(i => i.Entry.Name == name);
				float w = TextW(it.NameLabel, name);
				Assert.True(w <= it.NameLabel.Size.X, $"'{name}' is {w}px, the tag face is {it.NameLabel.Size.X}");
			}
			finally { Close(runner, screen); }
		}
	});

	[Test]
	public static void Probe_BoughtAndNeedText_FitFace(Node runner) => OnFixtures(() =>
	{
		var screen = Open(runner, Shopping(ShopScreenFixtures.StockedPool(), 1));
		try
		{
			var it = screen.Items[0];
			Assert.True(it.PriceText.StartsWith("Need"), "short on coins: " + it.PriceText);
			Assert.True(TextW(it.PriceLabel, it.PriceText) <= it.PriceLabel.Size.X, $"Need text wider than {it.PriceLabel.Size.X}");
		}
		finally { Close(runner, screen); }
		var s2 = Open(runner, Shopping(ShopScreenFixtures.StockedPool(), 1000));
		try
		{
			var sp = s2.Items.First(i => i.Entry.Kind == ShopKind.Special);
			string key = sp.Name;
			Assert.True(s2.BuySpecial(sp.Entry.Id), "bought");
			var it = s2.Cards[key];
			Assert.True(it.PriceText.StartsWith("SOLD"), it.PriceText);
			Assert.True(TextW(it.PriceLabel, it.PriceText) <= it.PriceLabel.Size.X, $"SOLD text {TextW(it.PriceLabel, it.PriceText)} > {it.PriceLabel.Size.X}");
			Assert.True(it.FocusMode == Control.FocusModeEnum.None && !it.Lifted, "bought: not focusable, not lifted");
		}
		finally { Close(runner, s2); }
	});

	[Test]
	public static void Probe_HoverEveryCell_FocusFollows_NothingChanges(Node runner) => OnFixtures(() =>
	{
		var s = Shopping(ShopScreenFixtures.WidePool(3, 3), 1000);
		ulong h = s.StateHash();
		var screen = Open(runner, s);
		try
		{
			foreach (var it in screen.Items.Where(i => i.Visible).ToList())
			{
				it.EmitSignal(Control.SignalName.MouseEntered);
				Assert.True(it.HasFocus() && it.Lifted, $"{it.Name} focused and lifted by hover");
				Assert.True(screen.Items.Count(i => i.Lifted) == 1, "exactly one lifted item");
			}
			Assert.True(s.StateHash() == h, "hover changes nothing");
		}
		finally { Close(runner, screen); }
	});

	[Test]
	public static void Probe_Refusals_LeaveStateUnchanged(Node runner) => OnFixtures(() =>
	{
		var s = Shopping(ShopScreenFixtures.StockedPool(), 0);
		ulong h = s.StateHash();
		var screen = Open(runner, s);
		try
		{
			for (int idx = 0; idx < screen.Items.Count; idx++)
			{
				var it = screen.Items[idx]; string nm = it.Name; bool ok = it.Entry.Kind == ShopKind.Special ? screen.BuySpecial(it.Entry.Id) : screen.BeginPick(it.Entry.Id);
				Assert.True(!ok, $"{nm} refused with 0 coins");
				Assert.True(s.StateHash() == h && screen.Picking == null, $"{nm}: hash and pick state unchanged");
			}
			Tap(screen, Key.Enter);
			Assert.True(s.StateHash() == h, "Enter on refused item changes nothing");
			Assert.True(screen.Items.All(i => i.PriceText.StartsWith("Need")), "every tag says Need");
		}
		finally { Close(runner, screen); }
	});

	[Test]
	public static void Probe_Slip_ToggleTwice_AndScrolled(Node runner) => OnFixtures(() =>
	{
		var screen = Open(runner, Shopping(ShopScreenFixtures.WidePool(5, 5), 1000));
		try
		{
			screen.ToggleFrameData(); Assert.True(screen.SlipVisible, "on");
			for (int i = 0; i < 8; i++) Tap(screen, Key.Down);
			Assert.True(screen.SlipVisible, "still on after scrolling");
			screen.ToggleFrameData(); Assert.True(!screen.SlipVisible, "off");
			screen.ToggleFrameData();
			var mod = screen.Items.First(i => i.Entry.Kind == ShopKind.Modifier);
			Assert.True(screen.BeginPick(mod.Entry.Id), "pick begins");
			Assert.True(!screen.SlipVisible, "slip hidden while picking");
			Tap(screen, Key.Escape);
			Assert.True(screen.Picking == null && screen.SlipVisible, "Esc leaves the pick, slip back");
		}
		finally { Close(runner, screen); }
	});

	[Test]
	public static void Probe_BuyEverything_ScrolledDown_ShelvesStable(Node runner) => OnFixtures(() =>
	{
		var screen = Open(runner, Shopping(ShopScreenFixtures.WidePool(5, 0), 100000));
		try
		{
			int n = screen.Items.Count;
			for (int i = 0; i < 12; i++) Tap(screen, Key.Down);
			foreach (var e in screen.Items.Select(i => i.Entry).Where(e => e.Kind == ShopKind.Special).ToList()) screen.BuySpecial(e.Id);
			Assert.True(screen.Items.Count == n && screen.Tiers == 3, "same items, three shelves");
			Assert.True(screen.Items.Count(i => i.CardDisabled) >= 1, "some bought");
			var f = screen.GetViewport().GuiGetFocusOwner();
			Assert.True(f == null || f.IsVisibleInTree(), "focus not on a hidden control: " + f?.Name);
		}
		finally { Close(runner, screen); }
	});
}
