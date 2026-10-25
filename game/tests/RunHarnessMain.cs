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
/// godot --headless --path game res://tests/run_harness.tscn -- [--runs 200] [--seed 1] [--level newcomer|&lt;level&gt;|all|mixed]
///   [--levels data/harness/bot-levels.json] [--bot data/harness/x.json] [--out dir] [--data dir] [--hit-log]
/// YOK-170: <c>--level</c> picks a bot skill level from the levels file (default newcomer = today's bot); <c>all</c> writes one
/// batch per level, <c>mixed</c> one batch where run i plays level i mod n. <c>--bot</c> plays one bot file on its own (no level).
/// Relative paths are from the repo root. <c>--data</c> plays another content folder (YOK-34: the data merge check runs a
/// candidate copy of data/); fixtures stay game/tests/fixtures/. Exit 0 when the batch was written, 1 on an error.
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
			string dataDir = args.TryGetValue("data", out var d) ? Path.GetFullPath(d, root) : ContentPaths.DataDir;
			if (!Directory.Exists(dataDir)) throw new DirectoryNotFoundException($"--data {dataDir} is not a folder");
			string outDir = args.TryGetValue("out", out var o) ? Path.GetFullPath(o, root) : Path.Combine(root, "harness", "results", "whole-run");
			var content = HarnessContent.Load(dataDir, ContentPaths.FixturesDir) with { RawHitLog = args.ContainsKey("hit-log") }; // YOK-157: every hit line too
			var clock = Stopwatch.StartNew(); // reporting only, never written to the file
			var batches = new List<BatchResult>();
			if (args.TryGetValue("bot", out var b))
			{
				if (args.ContainsKey("level") || args.ContainsKey("levels")) throw new ArgumentException("--bot plays one bot file; it takes no --level/--levels");
				batches.Add(RunHarness.PlayBatch(content, BotConfig.LoadFile(Path.GetFullPath(b, root)), seed, runs));
			}
			else
			{
				var levels = args.TryGetValue("levels", out var lf) ? BotLevels.LoadFile(Path.GetFullPath(lf, root)) : BotLevels.LoadDefault(dataDir);
				string level = args.GetValueOrDefault("level", BotLevels.DefaultLevel);
				if (level == "mixed") batches.Add(RunHarness.PlayMixed(content, levels, seed, runs));
				else if (level == "all") foreach (var lv in levels.Levels) batches.Add(RunHarness.PlayBatch(content, lv, seed, runs));
				else batches.Add(RunHarness.PlayBatch(content, levels[level], seed, runs));
			}
			foreach (var batch in batches)
			{
				string path = HarnessReport.Write(batch, outDir);
				GD.Print(HarnessReport.SummaryJson(batch));
				GD.Print($"RunHarness: {runs} runs from seed {seed} with '{batch.Name}' -> {path}");
			}
			GD.Print($"RunHarness: {batches.Count} batch(es) in {clock.Elapsed.TotalSeconds:0.0} s");
			GetTree().Quit(0);
		}
		catch (Exception e)
		{
			GD.PrintErr($"RunHarness: {e}");
			GetTree().Quit(1);
		}
	}
}
