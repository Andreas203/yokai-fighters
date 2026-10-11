using Godot;

namespace YokaiFighters.Ui.Kit;

/// <summary>
/// Paper sheet or small card. Root Control sized by its parent; <c>Panel</c> (PanelContainer, theme variation PaperSheet or PaperCard)
/// holds <c>Panel/Content</c> where screens put their controls; <c>CornerTL</c> / <c>CornerBR</c> are the ornament slots (sheet only).
/// The look is entirely the theme's PaperSheet / PaperCard panel style (see <see cref="PaperStyleBox"/>).
/// </summary>
[Tool]
public partial class PaperSheet : Control
{
	private bool _card;
	[Export] public bool Card { get => _card; set { _card = value; Apply(); } }
	private bool _ornaments = true;
	[Export] public bool Ornaments { get => _ornaments; set { _ornaments = value; Apply(); } }

	public VBoxContainer? Content => GetNodeOrNull<VBoxContainer>("Panel/Content");

	public override void _Ready()
	{
		var panel = GetNodeOrNull<Control>("Panel");
		if (panel != null) panel.MinimumSizeChanged += UpdateMinimumSize;
		Apply();
	}

	public override Vector2 _GetMinimumSize() => GetNodeOrNull<Control>("Panel")?.GetCombinedMinimumSize() ?? Vector2.Zero;

	private void Apply()
	{
		var panel = GetNodeOrNull<PanelContainer>("Panel");
		if (panel == null) return;
		panel.ThemeTypeVariation = _card ? "PaperCard" : "PaperSheet";
		foreach (var n in new[] { "CornerTL", "CornerBR" })
			if (GetNodeOrNull<Control>(n) is { } c) c.Visible = !_card && Ornaments;
		UpdateMinimumSize();
	}
}
