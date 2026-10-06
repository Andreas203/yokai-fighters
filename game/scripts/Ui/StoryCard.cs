using System;
using Godot;

namespace YokaiFighters.Ui;

/// <summary>
/// Story-card beat (intro, binding, wake-up...): layout lives in <c>scenes/ui/story_card.tscn</c> (paper panel,
/// 1-2 sentences, a red seal, a continue prompt). This script only sets the text from data (<see cref="Display"/>)
/// and handles input. Confirm = Enter / pad A / click; Space is not used (a gameplay key).
/// </summary>
public partial class StoryCard : Control
{
	public event Action? Finished;
	public string Text { get; private set; } = "";
	public bool Showing { get; private set; }

	private Label? _text;

	public override void _Ready()
	{
		_text = UiFind.Get<Label>(this, "Text");
		GuiInput += OnGuiInput;
		Visible = false;
	}

	public void Display(string text)
	{
		Text = text; Showing = true; Visible = true;
		if (_text != null) _text.Text = text;
	}

	/// <summary>YOK-48: hide without raising <see cref="Finished"/> (the flow restarted underneath it).</summary>
	public void Dismiss() { Showing = false; Visible = false; }

	public void Continue()
	{
		if (!Showing) return;
		Showing = false; Visible = false;
		Finished?.Invoke();
	}

	private void OnGuiInput(InputEvent e)
	{
		if (Showing && e is InputEventMouseButton { Pressed: true }) { AcceptEvent(); Continue(); }
	}

	public override void _UnhandledInput(InputEvent e)
	{
		if (!Showing) return;
		if (e is InputEventKey { Pressed: true, Echo: false } k && (k.Keycode == Key.Enter || k.Keycode == Key.KpEnter || k.Keycode == Key.Escape)
			|| e is InputEventJoypadButton { Pressed: true } j && (j.ButtonIndex == JoyButton.A || j.ButtonIndex == JoyButton.B))
		{ GetViewport().SetInputAsHandled(); Continue(); }
	}
}
