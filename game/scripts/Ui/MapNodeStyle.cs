using Godot;

namespace YokaiFighters.Ui;

/// <summary>How one node type looks on the map: a label, an accent colour, a placeholder icon glyph and the illustrated sprite the node is drawn as (map v2, YOK-238).</summary>
[GlobalClass]
public partial class MapNodeStyle : Resource
{
	[Export] public string Type { get; set; } = "";
	[Export] public string Label { get; set; } = "";
	[Export] public Color Color { get; set; } = new(0.45f, 0.42f, 0.38f);
	[Export] public string Icon { get; set; } = "?";
	/// <summary>Optional texture; replaces the glyph when set.</summary>
	[Export] public Texture2D? IconTexture { get; set; }
	/// <summary>YOK-238: the node's illustration (a mask, the merchant's stall, the dojo, the tea house). A yokai's own mask (<see cref="MapNodeStyles.YokaiMasks"/>) wins over it. Null = a big type letter stands in.</summary>
	[Export] public Texture2D? Sprite { get; set; }
	/// <summary>YOK-238: size of this type's sprite against the standard one (a stall reads larger than a mask).</summary>
	[Export] public float SpriteScale { get; set; } = 1f;
	/// <summary>YOK-109: optional lines under the node's name (one per line), for a type that says more on its card (the boss). Empty = none.</summary>
	[Export(PropertyHint.MultilineText)] public string Tagline { get; set; } = "";
}
