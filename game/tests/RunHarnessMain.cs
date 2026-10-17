using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Godot;
using YokaiFighters.Harness;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-26 headless whole-run harness (G1, R5). Plays N seeded first runs with the newcomer Kihon bot and writes
/// harness/results/whole-run/&lt;bot&gt;-n&lt;N&gt;-s&lt;seed&gt;.json (summary + every run), then prints the summary.
/// godot --headless --path game res://tests/run_harness.tscn -- [--runs 200] [--seed 1] [--bot data/harness/x.json] [--out dir]
/// Relative paths are from the repo root. Exit 0 when the batch was written, 1 on an error.
/// </summary>
public partial class RunHarnessMain : Node
{
	public override void _Ready()
	{
		var args = new Dictionary<string, string>();
		var raw = OS.GetCmdlineUserArgs();
		for (int i = 0; i < raw.Length; i++)
			if (raw[i].StartsWith("--")) args[raw[i][2..]] = i + 1 < raw.Length && !raw[i + 1].StartsWith("--") ? raw[++i] : "true";
		try
		{
			int runs = int.Parse(args.GetValueOrDefault("runs", "200"));
			uint seed = uint.Parse(args.GetValueOrDefault("seed", "1"));
			string root = Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"), ".."));
			var bot = args.TryGetValue("bot", out var b) ? BotConfig.LoadFile(Path.GetFullPath(b, root)) : BotConfig.LoadDefault(ContentPaths.DataDir);
			string outDir = args.TryGetValue("out", out var o) ? Path.GetFullPath(o, root) : Path.Combine(root, "harness", "results", "whole-run");
			var content = HarnessContent.Load(ContentPaths.DataDir, ContentPaths.FixturesDir);
			var clock = Stopwatch.StartNew(); // reporting only, never written to the file
			var batch = RunHarness.PlayBatch(content, bot, seed, runs);
			string path = HarnessReport.Write(batch, outDir);
			GD.Print(HarnessReport.SummaryJson(batch));
			GD.Print($"RunHarness: {runs} runs from seed {seed} with '{bot.Id}' in {clock.Elapsed.TotalSeconds:0.0} s -> {path}");
			GetTree().Quit(0);
		}
		catch (Exception e)
		{
			GD.PrintErr($"RunHarness: {e}");
			GetTree().Quit(1);
		}
	}
}
