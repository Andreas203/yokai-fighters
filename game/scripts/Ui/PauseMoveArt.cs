using Godot;

namespace YokaiFighters.Ui;

/// <summary>Pause diamond icons keyed by move identity, never slot or display name.</summary>
public static class PauseMoveArt
{
	public static Texture2D ForId(string? id)
	{
		var art = GD.Load<Resource>("res://ui/pause_move_art.tres");
		var icons = art.GetMeta("icons").AsGodotDictionary<string, Texture2D>();
		return id != null && icons.TryGetValue(id, out var icon) ? icon : (Texture2D)art.GetMeta("fallback").AsGodotObject();
	}
}
