using System.IO;
using Godot;

namespace YokaiFighters;

/// <summary>
/// YOK-39 (Windows export): where the JSON content lives at runtime. The game reads <c>data/</c> with System.IO,
/// so it must be a real folder on disk, never inside the PCK.
/// <list type="bullet">
/// <item>Editor, tests, harness: the repo root (the folder holding <c>data/</c> and <c>game/</c>); fixtures are
/// <c>game/tests/fixtures/</c>.</item>
/// <item>Exported build (feature <c>template</c>): the folder holding the executable; <c>tools/export-windows.sh</c>
/// copies <c>data/</c> and the fixture folders the kits fall back to (<c>fixtures/</c>) next to the exe.</item>
/// </list>
/// </summary>
public static class ContentPaths
{
	public sealed record Layout(string Root, string DataDir, string FixturesDir);

	/// <summary>Pure resolution (tested): <paramref name="resDir"/> = globalized <c>res://</c> (the <c>game/</c> folder).</summary>
	public static Layout Resolve(bool exported, string executablePath, string resDir)
	{
		if (exported)
		{
			string root = Path.GetDirectoryName(Path.GetFullPath(executablePath))!;
			return new(root, Path.Combine(root, "data"), Path.Combine(root, "fixtures"));
		}
		string repo = Path.GetFullPath(Path.Combine(resDir, ".."));
		return new(repo, Path.Combine(repo, "data"), Path.Combine(repo, "game", "tests", "fixtures"));
	}

	private static Layout? _current;

	/// <summary>The layout of this process (exported build or editor/tests).</summary>
	public static Layout Current => _current ??= Resolve(OS.HasFeature("template"), OS.GetExecutablePath(), ProjectSettings.GlobalizePath("res://"));

	public static string DataDir => Current.DataDir;
	public static string Data(string sub) => Path.Combine(Current.DataDir, sub);
	public static string FixturesDir => Current.FixturesDir;
}
