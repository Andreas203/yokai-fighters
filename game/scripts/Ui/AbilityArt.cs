using System.Collections.Generic;
using Godot;

namespace YokaiFighters.Ui;

/// <summary>
/// YOK-79: the one ability-art lookup. <c>ui/ability_art.tres</c> maps an ability id (the <c>id</c> in
/// data/moves, data/modifiers, data/cancels; never a slot, display name or level) to a texture. Null, empty
/// and unmapped ids get the generic <see cref="Fallback"/>; missing art is legal. Loaded once and cached.
/// Pause and rest stop use it now; reward cards, modifier-target and move list (YOK-81) call the same API.
/// To add art: add an ext_resource and one entry in <c>metadata/icons</c>.
/// </summary>
public static class AbilityArt
{
	public const string ResourcePath = "res://ui/ability_art.tres";
	static Dictionary<string, Texture2D>? _icons;
	static Texture2D? _fallback;

	static void Load()
	{
		if (_icons != null) return;
		var art = GD.Load<Resource>(ResourcePath);
		var icons = new Dictionary<string, Texture2D>();
		foreach (var kv in art.GetMeta("icons").AsGodotDictionary<string, Texture2D>()) icons[kv.Key] = kv.Value;
		_fallback = (Texture2D)art.GetMeta("fallback").AsGodotObject();
		_icons = icons;
	}

	/// <summary>The generic image used when an id has no art.</summary>
	public static Texture2D Fallback { get { Load(); return _fallback!; } }

	/// <summary>True when the id has its own art (otherwise <see cref="ForId"/> returns the fallback).</summary>
	public static bool Has(string? id) { Load(); return !string.IsNullOrEmpty(id) && _icons!.ContainsKey(id); }

	/// <summary>Art for the id, or the fallback for null / empty / unknown ids. Never null.</summary>
	public static Texture2D ForId(string? id)
	{
		Load();
		return !string.IsNullOrEmpty(id) && _icons!.TryGetValue(id, out var icon) ? icon : _fallback!;
	}
}
