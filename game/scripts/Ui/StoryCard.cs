using System;
using Godot;
using YokaiFighters.Ui.Kit;

namespace YokaiFighters.Ui;

/// <summary>
/// Story-card beat (intro, binding, wake-up...): layout lives in <c>scenes/ui/story_card.tscn</c> (paper panel,
/// 1-2 sentences, a red seal, a continue prompt). This script only sets the text from data (<see cref="Display"/>)
/// and handles input. Confirm = Enter / Space / pad A / click; Escape / pad B also dismiss.
/// </summary>
public partial class StoryCard : Control
{
	public event Action? Finished;
	public string Text { get; private set; } = "";
	public bool Showing { get; private set; }

	private Label? _text;
	private Vector2 _textArea;
	private int _bodyFontSize;

	public override void _Ready()
	{
		UiFind.Get<KeyChipFooter>(this, "Footer").SetHints(new KeyHint("confirm", "Continue"));
		_text = UiFind.Get<Label>(this, "Text");
		_textArea = _text.Size;
		_bodyFontSize = _text.GetThemeFontSize("font_size");
		ApplyText();
		GuiInput += OnGuiInput;
		Visible = false;
	}

	public void Display(string text)
	{
		Text = text; Showing = true; Visible = true;
		ShowTwistHeader(null, null, "E");
		ApplyText();
	}

	/// <summary>YOK-139 (Y3): the elder-twist intro card: <see cref="Display(string)"/> plus a header (the twist's seal and a kicker such as "ELDER TWIST · ROW 5").</summary>
	public void Display(string text, string kicker, string? iconId, string fallbackGlyph = "E")
	{
		Text = text; Showing = true; Visible = true;
		ShowTwistHeader(kicker, iconId ?? "", fallbackGlyph);
		ApplyText();
	}

	public string KickerText => GetNodeOrNull<Label>("Panel/Kicker")?.Visible == true ? GetNode<Label>("Panel/Kicker").Text : "";
	public string TwistIconId => GetNodeOrNull<TwistSeal>("Panel/TwistSeal") is { Visible: true } s ? s.Icon : "";

	void ShowTwistHeader(string? kicker, string? icon, string fallback)
	{
		var panel = GetNode<Control>("Panel");
		var label = panel.GetNodeOrNull<Label>("Kicker");
		var seal = panel.GetNodeOrNull<TwistSeal>("TwistSeal");
		if (kicker == null) { if (label != null) label.Visible = false; if (seal != null) seal.Visible = false; return; }
		if (seal == null)
		{
			seal = new TwistSeal { Name = "TwistSeal", Position = new Vector2(140, 30), Size = new Vector2(66, 66) };
			panel.AddChild(seal);
		}
		if (label == null)
		{
			label = new Label { Name = "Kicker", Position = new Vector2(226, 40), Size = new Vector2(880, 46), MouseFilter = MouseFilterEnum.Ignore, VerticalAlignment = VerticalAlignment.Center };
			label.AddThemeFontSizeOverride("font_size", 34);
			label.AddThemeColorOverride("font_color", MapNodePalette.ElderRed);
			panel.AddChild(label);
		}
		label.Text = kicker; label.Visible = true;
		seal.Icon = icon ?? ""; seal.Fallback = fallback; seal.Visible = true;
	}


	// Keep the short binding beat large; longer data cards wrap above the footer without truncation.
	private void ApplyText()
	{
		if (_text == null) return;
		var font = _text.GetThemeFont("font");
		int size = _bodyFontSize;
		while (size > 34 && font.GetMultilineStringSize(Text, _text.HorizontalAlignment, _textArea.X, size).Y > _textArea.Y)
			size--;
		_text.AddThemeFontSizeOverride("font_size", size);
		_text.Text = Text;
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
		if (e is InputEventKey { Pressed: true, Echo: false } k && (k.Keycode == Key.Enter || k.Keycode == Key.KpEnter || k.Keycode == Key.Space || k.Keycode == Key.Escape)
			|| e is InputEventJoypadButton { Pressed: true } j && (j.ButtonIndex == JoyButton.A || j.ButtonIndex == JoyButton.B))
		{ GetViewport().SetInputAsHandled(); Continue(); }
	}
}
