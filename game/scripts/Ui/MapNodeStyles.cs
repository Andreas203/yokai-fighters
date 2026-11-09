using Godot;
using Godot.Collections;

namespace YokaiFighters.Ui;

/// <summary>What the screen reads for one node: always something, even for an unknown type. <c>Sprite</c> is the illustration (YOK-238).</summary>
public readonly record struct MapNodeLook(string Label, Color Color, string Icon, Texture2D? IconTexture, string Tagline = "", Texture2D? Sprite = null, float SpriteScale = 1f);

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
	/// <summary>YOK-238: yokai -> mask lookup. A yokai with no entry falls back to its node type's sprite; a new yokai is a new entry here, no code.</summary>
	[Export] public Array<MapYokaiMask> YokaiMasks { get; set; } = new();

	static string Key(string? s) => (s ?? "").Trim().ToLowerInvariant().Replace(' ', '-');

	/// <summary>The mask of this yokai (by id first, then display name), or null when there is none (the Tanuki has none: it is never drawn).</summary>
	public Texture2D? MaskFor(string? yokaiId, string? yokaiName)
	{
		foreach (var k in new[] { Key(yokaiId), Key(yokaiName) })
			if (k != "") foreach (var m in YokaiMasks) if (m != null && Key(m.Yokai) == k && m.Mask != null) return m.Mask;
		return null;
	}

	/// <summary>The look of a node: its type's look, with the yokai's mask as the sprite when it has one (else the type's sprite, else the fallback's).</summary>
	public MapNodeLook Look(MapNodeView v)
	{
		var look = Look(v.Type);
		var mask = MaskFor(v.YokaiId, v.Yokai);
		return mask != null ? look with { Sprite = mask, SpriteScale = 1f } : look;
	}

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
			if (s != null && s.Type == type) return new(s.Label == "" ? type : s.Label, s.Color, s.Icon, s.IconTexture, s.Tagline, s.Sprite, s.SpriteScale);
		var f = Fallback;
		string label = type.Length == 0 ? "?" : char.ToUpperInvariant(type[0]) + type[1..].Replace('-', ' ');
		return new(label, f?.Color ?? new Color(0.45f, 0.42f, 0.38f), f?.Icon ?? "?", f?.IconTexture, "", f?.Sprite, f?.SpriteScale ?? 1f);
	}
}
