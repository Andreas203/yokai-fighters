using System;
using Godot;
using YokaiFighters.Ui.Kit;
using YokaiFighters.Ui;

namespace YokaiFighters.Fight;

/// <summary>
/// YOK-48: the "demo complete" card after the won rematch (<c>scenes/ui/demo_complete.tscn</c>), in the HUD's
/// talisman style. Restart raises <see cref="RestartRequested"/> (the flow
/// starts a fresh run). Layout is in the scene; the text comes from data/story/demo-complete.json via <see cref="SetText"/>.
/// </summary>
public partial class DemoCompleteScreen : Control
{
	public event Action? RestartRequested, TitleRequested;
	public Button Restart { get; private set; } = null!;
	public Button BackToTitle { get; private set; } = null!;
	/// <summary>Placeholders until <see cref="SetText"/> fills them from data/story/demo-complete.json.</summary>
	public const string PlaceholderTitle = "Demo complete", PlaceholderText = "Thanks for playing.";
	public string TitleText { get; private set; } = PlaceholderTitle;
	public string BodyText { get; private set; } = PlaceholderText;
	private Label? _title, _sub;

	/// <summary>The story card's title (null keeps the placeholder title) and text.</summary>
	public void SetText(string? title, string text)
	{
		TitleText = title ?? PlaceholderTitle;
		BodyText = text;
		if (_title != null) _title.Text = TitleText;
		if (_sub != null) _sub.Text = BodyText;
	}

	public override void _Ready()
	{
		UiFind.Get<KeyChipFooter>(this, "Footer").SetHints(new KeyHint("confirm", "Select"));
		_title = UiFind.Get<Label>(this, "Title");
		_sub = UiFind.Get<Label>(this, "Sub");
		Restart = UiFind.Get<Button>(this, "Restart");
		_title.Text = TitleText; _sub.Text = BodyText;
		BackToTitle = UiFind.Get<Button>(this, "BackToTitle");
		Restart.Pressed += () => RestartRequested?.Invoke();
		BackToTitle.Pressed += () => TitleRequested?.Invoke();
		Visible = false;
	}

	public new void Show() { Visible = true; Restart.GrabFocus(); }
}
