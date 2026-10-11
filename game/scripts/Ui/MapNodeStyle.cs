using Godot;

namespace YokaiFighters.Ui;

/// <summary>How one node type looks on the map: a label, an accent colour and a placeholder icon glyph (no art yet).</summary>
[GlobalClass]
public partial class MapNodeStyle : Resource
{
	[Export] public string Type { get; set; } = "";
	[Export] public string Label { get; set; } = "";
	[Export] public Color Color { get; set; } = new(0.45f, 0.42f, 0.38f);
	[Export] public string Icon { get; set; } = "?";
	/// <summary>Optional texture; replaces the glyph when set.</summary>
	[Export] public Texture2D? IconTexture { get; set; }
}
