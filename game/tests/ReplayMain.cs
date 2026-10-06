using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using YokaiFighters.Replay;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-49 headless replay check for CI and the harness. Replays each file (or every *.yfr in a folder) against
/// the current data and code, prints OK/FAIL per file and exits 0 when all are identical, 1 otherwise.
/// Godot --headless --path game res://tests/replay.tscn -- --replay &lt;file-or-folder&gt; [--replay ...]
/// (user:// paths work: --replay user://replays)
/// </summary>
public partial class ReplayMain : Node
{
	public override void _Ready()
	{
		var raw = OS.GetCmdlineUserArgs();
		var targets = new List<string>();
		for (int i = 0; i < raw.Length; i++)
			if (raw[i] == "--replay" && i + 1 < raw.Length) targets.Add(raw[++i]);
			else if (!raw[i].StartsWith("--")) targets.Add(raw[i]);
		if (targets.Count == 0)
		{
			GD.PrintErr("replay: usage -- --replay <file.yfr|folder>");
			GetTree().Quit(1);
			return;
		}

		var files = new List<string>();
		foreach (var t in targets)
		{
			string p = t.StartsWith("res://") || t.StartsWith("user://") ? ProjectSettings.GlobalizePath(t) : Path.GetFullPath(t);
			if (Directory.Exists(p)) { var found = Directory.GetFiles(p, "*.yfr"); Array.Sort(found, StringComparer.Ordinal); files.AddRange(found); }
			else files.Add(p);
		}

		var catalog = ReplayCatalog.FromRepo(Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + ".."));
		int fail = files.Count == 0 ? 1 : 0;
		if (files.Count == 0) GD.PrintErr("replay: no .yfr files found");
		foreach (var f in files)
		{
			string line;
			try
			{
				var report = Replayer.Run(ReplayRecording.Load(f), catalog);
				line = report.ToString();
				if (!report.Ok) fail++;
			}
			catch (Exception e) { line = "FAIL: " + e.Message; fail++; }
			GD.Print($"{Path.GetFileName(f)}: {line}");
		}
		GD.Print($"replay: {files.Count - fail} identical, {fail} failed");
		GetTree().Quit(fail == 0 ? 0 : 1);
	}
}
