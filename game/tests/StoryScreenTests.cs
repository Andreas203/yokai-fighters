using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Story;
using YokaiFighters.Ui;
using YokaiFighters.Ui.Kit;

namespace YokaiFighters.Tests;

/// <summary>Real story sources, layout bounds, dismissal, restart focus and device-aware hints.</summary>
public static class StoryScreenTests
{
	static T Make<T>(Node host, string scene) where T : Control
	{
		var screen = GD.Load<PackedScene>($"res://scenes/ui/{scene}.tscn").Instantiate<T>();
		host.AddChild(screen);
		screen.Size = new Vector2(1920, 1080);
		return screen;
	}

	static void CheckLayout(Control screen, string labelName, string copy)
	{
		var label = UiFind.Get<Label>(screen, labelName);
		Assert.Equal(copy, label.Text, "live copy is verbatim from the source");
		KitGallery.ForceLayout(screen);
		var panel = screen.GetNode<Control>("Panel");
		Assert.True(new Rect2(Vector2.Zero, screen.Size).Encloses(panel.GetRect()), "paper fits the design canvas");
		// Measure the whole shaped string, including wrapping, instead of trusting the label's expanded minimum.
		var font = label.GetThemeFont("font");
		var size = font.GetMultilineStringSize(copy, label.HorizontalAlignment, label.Size.X, label.GetThemeFontSize("font_size"));
		Assert.True(size.Y <= label.Size.Y, $"{labelName} full text fits: {size.Y} <= {label.Size.Y}");
		Assert.True(panel.GetGlobalRect().Encloses(label.GetGlobalRect()), "copy stays on the paper");
		Assert.True(label.GetThemeFontSize("font_size") * 720f / 1080f >= 22, "body remains readable on a stretched 720p canvas");
		var footer = UiFind.Get<KeyChipFooter>(screen, "Footer");
		InputGlyphs.Set(InputDevice.Keyboard);
		Assert.Equal("ENTER", footer.ChipTexts()[0], "keyboard hint");
		footer._Input(new InputEventJoypadButton { ButtonIndex = JoyButton.A, Pressed = true });
		Assert.Equal("A", footer.ChipTexts()[0], "pad hint follows actual input observation");
		InputGlyphs.Set(InputDevice.Keyboard);
	}

	[Test]
	public static void Story_RealCopyFitsAndAllDismissInputsSurvive(Node host)
	{
		var screen = Make<StoryCard>(host, "story_card");
		try
		{
			int finished = 0;
			screen.Finished += () => finished++;
			foreach (var card in FightScene.LoadStory().Cards)
			{
				var text = card.FillText("Kitsune");
				screen.Display(text);
				CheckLayout(screen, "Text", text);
				screen.Dismiss();
			}
			Assert.Equal(0, finished, "Dismiss never emits Finished");
			foreach (var key in new[] { Key.Enter, Key.KpEnter, Key.Space, Key.Escape })
			{
				screen.Display(FightScene.LoadStory().BindingLine("Kitsune"));
				screen._UnhandledInput(new InputEventKey { Keycode = key, Pressed = true, Echo = true });
				Assert.True(screen.Showing, "key echo ignored");
				screen._UnhandledInput(new InputEventKey { Keycode = key, Pressed = true });
				Assert.True(!screen.Showing && !screen.Visible, "key dismisses");
			}
			foreach (var button in new[] { JoyButton.A, JoyButton.B })
			{
				screen.Display("test");
				screen._UnhandledInput(new InputEventJoypadButton { ButtonIndex = button, Pressed = true });
			}
			screen.Display("test");
			screen.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true });
			screen.Continue();
			Assert.Equal(7, finished, "one completion per input, never twice");
			Assert.True(screen.GetNodeOrNull<KitBackdrop>("Backdrop") != null, "story has the night backdrop");
			Assert.True(screen.GetNode<TextureRect>("Panel/Seal").Texture.ResourcePath.EndsWith("burst-blank.png"), "text-free seal");
		}
		finally { screen.Free(); }
	}

	[Test]
	public static void Results_RealCopyFitsWithRestartAndStageVisible(Node host)
	{
		var lib = FightScene.LoadStory();
		var lose = Make<LoseScreen>(host, "lose_screen");
		var demo = Make<DemoCompleteScreen>(host, "demo_complete");
		try
		{
			var loss = lib.LoseScreen("Kitsune");
			lose.SetText(loss.Title, loss.Text);
			lose.Open();
			Assert.True(lose.Restart.HasFocus(), "loss opens with restart focus");
			var win = lib.DemoComplete("Kitsune");
			demo.SetText(win.Title, win.Text);
			demo.Show();
			Assert.True(demo.Restart.HasFocus(), "completion opens with restart focus");
			int restarts = 0;
			lose.RestartRequested += () => restarts++;
			demo.RestartRequested += () => restarts++;
			foreach (var (screen, title, text, button) in new[] {
				((Control)lose, loss.Title!, loss.Text, lose.Restart),
				((Control)demo, win.Title!, win.Text, demo.Restart) })
			{
				CheckLayout(screen, "Title", title);
				CheckLayout(screen, "Sub", text);
				var sub = UiFind.Get<Label>(screen, "Sub");
				Assert.True(sub.GetGlobalRect().End.Y < button.GlobalPosition.Y, "full copy clears restart");
				Assert.True(button is KitBrushButton && button.FocusMode == Control.FocusModeEnum.All, "live focusable kit brush button");
				button.EmitSignal(BaseButton.SignalName.Pressed);
				Assert.True(screen.GetNode<ColorRect>("Dim").Color.A < 1, "3D stage remains visible under dim");
				Assert.True(screen.GetNodeOrNull<Control>("Backdrop") == null, "results never replace the stage with a backdrop");
			}
			Assert.Equal(2, restarts, "both original restart signals wired");
		}
		finally { lose.Free(); demo.Free(); }
	}
}
