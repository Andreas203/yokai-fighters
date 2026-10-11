using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Ui;
using YokaiFighters.Ui.Kit;
using YokaiFighters.Flow;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

public static class BootScreenLayoutTests
{
	static void Tap(Node n, InputEvent e) => n.GetViewport().PushInput(e);
	static void Key(Node n, Godot.Key k)
	{
		Tap(n, new InputEventKey { Keycode = k, PhysicalKeycode = k, Pressed = true });
		Tap(n, new InputEventKey { Keycode = k, PhysicalKeycode = k });
	}
	static void Pad(Node n, JoyButton b)
	{
		Tap(n, new InputEventJoypadButton { ButtonIndex = b, Pressed = true });
		Tap(n, new InputEventJoypadButton { ButtonIndex = b });
	}
	static void Audit(Control screen)
	{
		screen.Size = new Vector2(1920, 1080);
		for (int i = 0; i < 3; i++) KitGallery.ForceLayout(screen);
		void Walk(Node n)
		{
			if (n is Control c)
			{
				if (!c.IsVisibleInTree()) return;
				if (c is Label l && l.Text.Length > 0)
				{
					Assert.True(new Rect2(0, 0, 1920, 1080).Encloses(l.GetGlobalRect()), $"{l.Name} within canvas");
					Assert.True(l.GetMinimumSize().X <= l.Size.X + 1 && l.GetMinimumSize().Y <= l.Size.Y + 1, $"{l.Name} text fits");
					Assert.True(l.GetThemeFontSize("font_size") * 2f / 3 >= 16, $"{l.Name} readable at 720p scale");
				}
			}
			foreach (var child in n.GetChildren()) Walk(child);
		}
		Walk(screen);
	}

	[Test]
	public static void BootScreens_RealCopyFitsDesignCanvas(Node host)
	{
		MenuSettings.Reset();
		var title = GD.Load<PackedScene>("res://scenes/ui/title_menu.tscn").Instantiate<TitleMenu>();
		host.AddChild(title);
		title.SetEntry(new(TitleEntry.Continue, false, ScreenRouter.NoSaveReason));
		title.SetEntry(new(TitleEntry.Practice, false, ScreenRouter.NoPracticeReason));
		Audit(title);
		title.Free();
		var select = GD.Load<PackedScene>("res://scenes/ui/start_screen.tscn").Instantiate<StartScreen>();
		host.AddChild(select);
		foreach (var scheme in new[] { ControlScheme.Kata, ControlScheme.Kihon })
		{
			MenuSettings.SetScheme(scheme);
			select.SetReferenceVisible(false); Audit(select);
			select.SetReferenceVisible(true); Audit(select);
		}
		select.Free(); MenuSettings.Reset();
	}

	[Test]
	public static void ControlSelect_ReferenceAndShortcutsThroughRealInput(Node host)
	{
		MenuSettings.Reset();
		var s = GD.Load<PackedScene>("res://scenes/ui/start_screen.tscn").Instantiate<StartScreen>();
		host.AddChild(s);
		Assert.True(!s.ReferenceVisible, "concept layout on entry");
		Key(host, Godot.Key.Q);
		Assert.Equal(ControlScheme.Kata, StartScreen.Scheme, "Q flips scheme");
		Assert.True(s.KataButton.GetNode<Control>("Selected").Visible, "Kata brush frame");
		Assert.True(UiFind.Get<Label>(s, "KataDescription").Text.Contains("+10% precision bonus on motion-input specials"), "K1 wording");
		Key(host, Godot.Key.Down); // Start -> reference toggle
		Key(host, Godot.Key.Enter);
		Assert.True(s.ReferenceVisible, "keyboard opens reference");
		Pad(host, JoyButton.X);
		Assert.Equal(ControlScheme.Kihon, StartScreen.Scheme, "pad X flips while reference open");
		Assert.Equal("X", UiFind.Get<KeyChipFooter>(s, "Footer").ChipTexts()[0], "pad footer");
		Pad(host, JoyButton.A);
		Assert.True(!s.ReferenceVisible, "pad closes reference");
		int back = 0, started = 0;
		s.BackRequested += () => back++;
		s.Started += () => started++;
		Key(host, Godot.Key.Escape); Pad(host, JoyButton.B);
		Assert.Equal(2, back, "back preserved on keyboard and pad");
		s.Start.GrabFocus(); Pad(host, JoyButton.A);
		Assert.Equal(1, started, "pad starts once");
		s.Free(); MenuSettings.Reset();
	}
}
