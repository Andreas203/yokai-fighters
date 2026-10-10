using Godot;

namespace YokaiFighters.Ui.Kit;

/// <summary>Heading text with a red brush-stroke underline (the brush look until a brush font is chosen). Text and underline width are exports.</summary>
[Tool]
public partial class KitHeading : VBoxContainer
{
	private string _text = "Heading";
	private float _underline = 420f;
	[Export] public string Text { get => _text; set { _text = value; Apply(); } }
	[Export] public float UnderlineWidth { get => _underline; set { _underline = value; Apply(); } }

	public override void _Ready() => Apply();

	private void Apply()
	{
		if (GetNodeOrNull<Label>("Title") is { } l) l.Text = _text;
		if (GetNodeOrNull<Control>("Underline") is { } u) u.CustomMinimumSize = new Vector2(_underline, u.CustomMinimumSize.Y);
	}
}
