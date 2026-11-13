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

	/// <summary>Where an id's own icon file lives; dropping <c>ability-icon-&lt;id&gt;.png</c> here is all it takes to show it.</summary>
	public static string FilePath(string id) => $"res://assets/generated/ui/ability-icon-{id}.png";

	static readonly Dictionary<string, Texture2D> _files = new();
	static readonly HashSet<string> _missing = new();

	/// <summary>Forgets every loaded and missing icon file, so art added while the game runs is found (dev/tests).</summary>
	public static void ClearFileCache() { _files.Clear(); _missing.Clear(); }
	static Dictionary<string, string>? _evolutions;

	/// <summary>An evolution shows its special's icon: evolution id to special id, read once from data/moves/.</summary>
	static string SpecialOf(string id)
	{
		if (_evolutions is null)
		{
			var map = new Dictionary<string, string>();
			try
			{
				string dir = ContentPaths.Data("moves");
				if (System.IO.Directory.Exists(dir))
					foreach (string f in System.IO.Directory.GetFiles(dir, "*.json"))
					{
						using var doc = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(f));
						if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object || !doc.RootElement.TryGetProperty("id", out var sid) || sid.GetString() is not { } special) continue;
						foreach (string evo in EvolutionIds(doc.RootElement)) map[evo] = special;
					}
			}
			catch (System.Exception e) when (e is System.IO.IOException or System.Text.Json.JsonException) { GD.PushWarning($"AbilityArt: evolution ids unread: {e.Message}"); }
			_evolutions = map;
		}
		return _evolutions.TryGetValue(id, out var s) ? s : id;
	}

	static IEnumerable<string> EvolutionIds(System.Text.Json.JsonElement e)
	{
		if (e.ValueKind == System.Text.Json.JsonValueKind.Object)
			foreach (var p in e.EnumerateObject())
			{
				if (p.Name == "evolution" && p.Value.ValueKind == System.Text.Json.JsonValueKind.Object && p.Value.TryGetProperty("id", out var id) && id.GetString() is { } s) yield return s;
				foreach (string x in EvolutionIds(p.Value)) yield return x;
			}
		else if (e.ValueKind == System.Text.Json.JsonValueKind.Array)
			foreach (var item in e.EnumerateArray()) foreach (string x in EvolutionIds(item)) yield return x;
	}

	/// <summary>The icon file for the id (or for the special it evolves), loaded and kept; null when there is none.</summary>
	static Texture2D? FromFile(string id)
	{
		if (id.Length > 80 || !System.Text.RegularExpressions.Regex.IsMatch(id, @"^[a-z0-9][a-z0-9_-]*$")) return null; // data ids only: never a path
		if (_files.TryGetValue(id, out var cached)) return cached;
		if (_missing.Contains(id)) return null;
		var t = UiTextures.Load(FilePath(id)) ?? (SpecialOf(id) is var sp && sp != id ? UiTextures.Load(FilePath(sp)) : null);
		if (t != null) _files[id] = t;
		else _missing.Add(id); // misses are cached too (no disk check per frame); ClearFileCache() looks again
		return t;
	}

	/// <summary>True when the id has its own art (otherwise <see cref="ForId"/> returns the fallback).</summary>
	public static bool Has(string? id) { Load(); return !string.IsNullOrEmpty(id) && (_icons!.ContainsKey(id) || FromFile(id) != null); }

	/// <summary>Art for the id: its mapped icon, else <c>ability-icon-&lt;id&gt;.png</c> (an evolution: its special's), else the fallback for null / empty / unknown ids. Never null.</summary>
	public static Texture2D ForId(string? id)
	{
		Load();
		if (string.IsNullOrEmpty(id)) return _fallback!;
		if (_icons!.TryGetValue(id, out var icon)) return icon;
		return FromFile(id) ?? _fallback!;
	}
}
