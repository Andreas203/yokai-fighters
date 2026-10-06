using System;
using Godot;
using YokaiFighters.Fight;

namespace YokaiFighters.Ui;

/// <summary>
/// Story-card beat (intro, binding, wake-up...): paper panel, 1-2 sentences, a red seal, a continue prompt.
/// Text is data (<see cref="Display"/>). Confirm = Enter / pad A / click; Space is not used (a gameplay key).
/// </summary>
public partial class StoryCard : Control
{
	public event Action? Finished;
	public string Text { get; private set; } = "";
	public bool Showing { get; private set; }

	private void FitViewport() { Position = Vector2.Zero; Size = GetViewportRect().Size; QueueRedraw(); }

	public override void _Ready()
	{
		FitViewport(); GetViewport().SizeChanged += FitViewport;
		MouseFilter = MouseFilterEnum.Stop;
		Visible = false;
	}

	public void Display(string text) { Text = text; Showing = true; Visible = true; QueueRedraw(); }

	public void Continue()
	{
		if (!Showing) return;
		Showing = false; Visible = false;
		Finished?.Invoke();
	}

	public override void _UnhandledInput(InputEvent e)
	{
		if (!Showing) return;
		if (e is InputEventKey { Pressed: true, Echo: false } k && (k.Keycode == Key.Enter || k.Keycode == Key.KpEnter || k.Keycode == Key.Escape)
			|| e is InputEventJoypadButton { Pressed: true } j && (j.ButtonIndex == JoyButton.A || j.ButtonIndex == JoyButton.B)
			|| e is InputEventMouseButton { Pressed: true })
		{ GetViewport().SetInputAsHandled(); Continue(); }
	}

	public override void _Draw()
	{
		if (!Showing) return;
		Vector2 s = Size;
		DrawRect(new Rect2(Vector2.Zero, s), new Color(FightHud.Ink, 0.94f));
		var panel = new Rect2(s.X / 2 - 560, s.Y / 2 - 190, 1120, 380);
		Paint.Talisman(this, panel, FightHud.Paper);
		Font f = ThemeDB.FallbackFont;
		DrawMultilineString(f, new Vector2(panel.Position.X + 60, panel.Position.Y + 150), Text, HorizontalAlignment.Center, panel.Size.X - 120, 52, 3, FightHud.Ink);
		Paint.Seal(this, new Vector2(panel.End.X - 80, panel.End.Y - 60), 36, "");
		Paint.Brush(this, new Vector2(panel.Position.X + 60, panel.End.Y - 78), 320, 8, FightHud.Ink);
		DrawString(f, new Vector2(panel.Position.X + 60, panel.End.Y - 36), "Enter / A  continue", HorizontalAlignment.Left, -1, 26, FightHud.Ink);
	}
}
