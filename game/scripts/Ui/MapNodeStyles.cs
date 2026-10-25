using Godot;
using Godot.Collections;

namespace YokaiFighters.Ui;

/// <summary>What the screen reads for one node: always something, even for an unknown type.</summary>
public readonly record struct MapNodeLook(string Label, Color Color, string Icon, Texture2D? IconTexture);

/// <summary>
/// YOK-78: the one lookup node type -> look (<c>ui/map_node_styles.tres</c>). A new node type is a new entry in that resource, no code.
/// A type with no entry gets <see cref="Fallback"/> with the type id as its label.
/// </summary>
[GlobalClass]
public partial class MapNodeStyles : Resource
{
	[Export] public Array<MapNodeStyle> Styles { get; set; } = new();
	[Export] public MapNodeStyle? Fallback { get; set; }
	/// <summary>YOK-139 (Y3): one entry per elder-twist icon id (<c>Type</c> = the id in data/twists/*.json); a new twist icon is a new entry here.</summary>
	[Export] public Array<MapNodeStyle> TwistIcons { get; set; } = new();

	/// <summary>The look of a twist icon id: its entry, else a generic "E" look for an id with no entry. Null id = no twist (null).</summary>
	public MapNodeLook? TwistLook(string? iconId)
	{
		if (string.IsNullOrEmpty(iconId)) return null;
		foreach (var s in TwistIcons)
			if (s != null && s.Type == iconId) return new(s.Label == "" ? iconId : s.Label, s.Color, s.Icon, s.IconTexture);
		return new(iconId, Fallback?.Color ?? new Color(0.72f, 0.2f, 0.16f), "E", null);
	}

	public MapNodeLook Look(string type)
	{
		foreach (var s in Styles)
			if (s != null && s.Type == type) return new(s.Label == "" ? type : s.Label, s.Color, s.Icon, s.IconTexture);
		var f = Fallback;
		string label = type.Length == 0 ? "?" : char.ToUpperInvariant(type[0]) + type[1..].Replace('-', ' ');
		return new(label, f?.Color ?? new Color(0.45f, 0.42f, 0.38f), f?.Icon ?? "?", f?.IconTexture);
	}
}
