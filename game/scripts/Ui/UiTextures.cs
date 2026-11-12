using Godot;

namespace YokaiFighters.Ui;

/// <summary>
/// Loads a generated UI image by res:// path: the imported resource when there is one, else the file straight from disk
/// (art dropped in before the editor imported it). Null when the file is not there; callers draw a fallback, never a blank box.
/// </summary>
public static class UiTextures
{
	public static bool Exists(string path) =>
		ResourceLoader.Exists(path) || System.IO.File.Exists(ProjectSettings.GlobalizePath(path));

	public static Texture2D? Load(string path)
	{
		if (ResourceLoader.Exists(path) && GD.Load<Texture2D>(path) is { } t) return t;
		string disk = ProjectSettings.GlobalizePath(path);
		if (!System.IO.File.Exists(disk)) return null;
		var img = Image.LoadFromFile(disk);
		return img is null || img.IsEmpty() ? null : ImageTexture.CreateFromImage(img);
	}
}
