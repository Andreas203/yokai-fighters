using Godot;

namespace YokaiFighters.Ui.Kit;

public enum FooterDevice { Auto, Keyboard, Pad }

/// <summary>One footer entry: a menu action id (see <see cref="InputGlyphs.Glyph"/>) and what it does.</summary>
public readonly record struct KeyHint(string Action, string Label);

/// <summary>
/// Footer row of key chips ("[ENTER] Confirm | [ESC] Resume"). Chip text follows the last device used (keyboard or pad) unless
/// <see cref="Pinned"/> fixes it. Set the hints with <see cref="SetHints"/>; <see cref="Light"/> inks the labels for use on paper.
/// </summary>
[Tool]
public partial class KeyChipFooter : HBoxContainer
{
	private KeyHint[] _hints = System.Array.Empty<KeyHint>();
	private FooterDevice _pinned = FooterDevice.Auto;
	private bool _onPaper;

	[Export] public FooterDevice Pinned { get => _pinned; set { _pinned = value; Rebuild(); } }
	[Export] public bool OnPaper { get => _onPaper; set { _onPaper = value; Rebuild(); } }
	public InputDevice Device => _pinned switch { FooterDevice.Keyboard => InputDevice.Keyboard, FooterDevice.Pad => InputDevice.Pad, _ => InputGlyphs.Last };

	public override void _EnterTree() => InputGlyphs.Changed += OnDevice;
	public override void _ExitTree() => InputGlyphs.Changed -= OnDevice;
	public override void _Ready() { Alignment = AlignmentMode.Center; Rebuild(); }

	public override void _Input(InputEvent e) => InputGlyphs.Observe(e);

	private void OnDevice(InputDevice _) => Rebuild();

	public void SetHints(params KeyHint[] hints) { _hints = hints; Rebuild(); }

	/// <summary>Chip texts as currently shown, in order (tests read this).</summary>
	public string[] ChipTexts()
	{
		var r = new System.Collections.Generic.List<string>();
		foreach (var h in _hints) r.Add(InputGlyphs.Glyph(h.Action, Device));
		return r.ToArray();
	}

	private void Rebuild()
	{
		if (!IsInsideTree() && GetChildCount() == 0 && _hints.Length == 0) return;
		foreach (var c in GetChildren()) { RemoveChild(c); c.QueueFree(); }
		for (int i = 0; i < _hints.Length; i++)
		{
			if (i > 0)
			{
				var bar = new ColorRect { CustomMinimumSize = new Vector2(2, 34), Color = _onPaper ? new Color(0.114f, 0.106f, 0.129f, 0.5f) : new Color(0.945f, 0.91f, 0.831f, 0.5f), SizeFlagsVertical = SizeFlags.ShrinkCenter, Name = "Divider" + i };
				AddChild(bar);
			}
			var chip = new PanelContainer { ThemeTypeVariation = "KeyChip", Name = "Chip" + i };
			chip.AddChild(new Label { ThemeTypeVariation = "KeyChipLabel", Text = InputGlyphs.Glyph(_hints[i].Action, Device), HorizontalAlignment = HorizontalAlignment.Center, Name = "Glyph" });
			AddChild(chip);
			AddChild(new Label { ThemeTypeVariation = _onPaper ? "FooterLabelInk" : "FooterLabel", Text = _hints[i].Label, Name = "Action" + i });
		}
	}
}
