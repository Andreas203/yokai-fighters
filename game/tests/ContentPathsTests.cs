using System;
using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-39 (Windows export): the exported build finds data/ and fixtures/ next to the executable; the editor and
/// tests keep the repo layout. The release check builds the export layout in a temp folder (a copy of data/ and
/// the fixture folders beside a stand-in exe), so it exercises exactly what tools/export-windows.sh ships.
/// </summary>
public static class ContentPathsTests
{
	static string ResDir => ProjectSettings.GlobalizePath("res://");
	static string RepoRoot => Path.GetFullPath(Path.Combine(ResDir, ".."));

	static void CopyTree(string from, string to)
	{
		Directory.CreateDirectory(to);
		foreach (string f in Directory.GetFiles(from, "*.json")) File.Copy(f, Path.Combine(to, Path.GetFileName(f)));
		foreach (string d in Directory.GetDirectories(from)) CopyTree(d, Path.Combine(to, Path.GetFileName(d)));
	}

	[Test]
	public static void EditorLayoutIsTheRepo()
	{
		var l = ContentPaths.Resolve(false, "C:/anywhere/godot.exe", ResDir);
		Assert.Equal(Path.Combine(RepoRoot, "data"), l.DataDir, "editor data dir");
		Assert.True(Directory.Exists(Path.Combine(l.DataDir, "moves")), "editor finds data/moves");
		Assert.True(Directory.Exists(Path.Combine(l.FixturesDir, "ryo-normals")), "editor finds the fixtures");
		Assert.Equal(l.DataDir, ContentPaths.DataDir, "this (non-exported) process uses the repo layout");
	}

	[Test]
	public static void ReleaseLayoutFindsDataNextToTheExe()
	{
		string tmp = Path.Combine(Path.GetTempPath(), "yok39-" + Guid.NewGuid().ToString("N"));
		try
		{
			CopyTree(Path.Combine(RepoRoot, "data"), Path.Combine(tmp, "data"));
			CopyTree(Path.Combine(RepoRoot, "game", "tests", "fixtures"), Path.Combine(tmp, "fixtures"));
			var l = ContentPaths.Resolve(true, Path.Combine(tmp, "YokaiFighters.exe"), "C:/not/the/repo/");
			Assert.Equal(Path.Combine(tmp, "data"), l.DataDir, "release data dir is next to the exe");
			foreach (string sub in new[] { "moves", "clips", "profiles", "story", "modifiers" })
				Assert.True(Directory.Exists(Path.Combine(l.DataDir, sub)), $"release finds data/{sub}");

			var src = KitSources.At(l.DataDir, l.FixturesDir);
			var ryo = Kit.Load(Kit.Ryo, src);
			var repo = Kit.Load(Kit.Ryo, KitSources.Repo(RepoRoot));
			Assert.Equal(string.Join(",", repo.Moves.Select(m => m.Id)), string.Join(",", ryo.Moves.Select(m => m.Id)), "same Ryo kit as the repo");
			var foe = Kit.Load("kitsune", src);
			var repoFoe = Kit.Load("kitsune", KitSources.Repo(RepoRoot));
			Assert.Equal(string.Join(",", repoFoe.Moves.Select(m => m.Id)), string.Join(",", foe.Moves.Select(m => m.Id)), "same Kitsune kit as the repo");
			Assert.True(Story.StoryLibrary.LoadDirectory(Path.Combine(l.DataDir, "story")) is not null, "story loads");
			Assert.True(Ai.BehaviourProfile.LoadDirectory(Path.Combine(l.DataDir, "profiles")).Any(p => p.Yokai == "kitsune"), "kitsune profiles load");
		}
		finally { if (Directory.Exists(tmp)) Directory.Delete(tmp, true); }
	}
}
