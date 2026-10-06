using System;
using Godot;
using YokaiFighters.Ui;

namespace YokaiFighters.Fight;

/// <summary>
/// The lose screen (<c>scenes/ui/lose_screen.tscn</c>, instanced by the HUD): dimmed backdrop, paper panel, the
/// story card's title and text, and a Restart button. Layout is in the scene; the title and text come from
/// <c>data/story/lose-screen.json</c> through <see cref="SetText"/> (the scene holds placeholders).
/// </summary>
public partial class LoseScreen : Control
{
	public const string PlaceholderTitle = "Ryo is defeated", PlaceholderText = "The yokai slips free.";

	public event Action? RestartRequested;
	public string Title { get; private set; } = PlaceholderTitle;
	public string Text { get; private set; } = PlaceholderText;
	public Button Restart { get; private set; } = null!;
	private Label? _title, _text;

	public override void _Ready()
	{
		_title = UiFind.Get<Label>(this, "Title");
		_text = UiFind.Get<Label>(this, "Sub");
		Restart = UiFind.Get<Button>(this, "Restart");
		Restart.Pressed += () => RestartRequested?.Invoke();
		_title.Text = Title; _text.Text = Text;
		Visible = false;
	}

	/// <summary>The story card, already filled with the run's names (S4). A null title keeps the current one.</summary>
	public void SetText(string? title, string text)
	{
		if (title != null) Title = title;
		Text = text;
		if (_title != null) _title.Text = Title;
		if (_text != null) _text.Text = Text;
	}

	/// <summary>Show the screen with the Restart button focused (Enter / Space / A press it).</summary>
	public void Open() { Visible = true; Restart.GrabFocus(); }
}
