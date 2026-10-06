using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using YokaiFighters.Ai;
using YokaiFighters.Fight;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-27 headless AI harness. Plays P2 = ProfileAi vs a scripted P1 and writes
/// harness/results/ai/&lt;profile&gt;-&lt;p1&gt;-r&lt;frames&gt;-s&lt;seed&gt;.decisions.csv and .summary.json, printing each summary.
/// Godot --headless --path game res://tests/ai_harness.tscn -- [--profile test-kitsune-patient] [--p1 thrower|jumper|random|idle]
///   [--reaction 22] [--seed 1] [--ticks 7200] [--sweep]   (--sweep: reactions 30/22/18/15 × jumper and thrower)
/// </summary>
public partial class AiHarnessMain : Node
{
	public override void _Ready()
	{
		var args = new Dictionary<string, string>();
		var raw = OS.GetCmdlineUserArgs();
		for (int i = 0; i < raw.Length; i++)
			if (raw[i].StartsWith("--")) args[raw[i][2..]] = i + 1 < raw.Length && !raw[i + 1].StartsWith("--") ? raw[++i] : "true";

		string id = args.GetValueOrDefault("profile", "test-kitsune-patient");
		string dir = ProjectSettings.GlobalizePath(FightScene.FixtureProfilesDir);
		string dataDir = ProjectSettings.GlobalizePath("res://") + "../data/profiles";
		string path = File.Exists(Path.Combine(dataDir, id + ".json")) ? Path.Combine(dataDir, id + ".json") : Path.Combine(dir, id + ".json");
		var profile = BehaviourProfile.LoadFile(path);
		uint seed = uint.Parse(args.GetValueOrDefault("seed", "1"));
		int ticks = int.Parse(args.GetValueOrDefault("ticks", "7200"));
		string outDir = Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "../harness/results/ai");
		Directory.CreateDirectory(outDir);

		var runs = new List<(P1Script, int?)>();
		if (args.ContainsKey("sweep"))
			foreach (var s in new[] { P1Script.Jumper, P1Script.Thrower })
			foreach (int r in new[] { 30, 22, 18, 15 }) runs.Add((s, r));
		else
			runs.Add((Enum.Parse<P1Script>(args.GetValueOrDefault("p1", "thrower"), true),
				args.TryGetValue("reaction", out var rf) ? int.Parse(rf) : null));

		foreach (var (script, reaction) in runs)
		{
			var res = AiHarness.Run(FightScene.NewMatch, profile, script, seed, ticks, reaction, trace: args.ContainsKey("trace"));
			string name = $"{profile.Id}-{script.ToString().ToLowerInvariant()}-r{res.ReactionFrames}-s{seed}";
			File.WriteAllText(Path.Combine(outDir, name + ".decisions.csv"), res.DecisionsCsv());
			File.WriteAllText(Path.Combine(outDir, name + ".summary.json"), res.SummaryJson() + "\n");
			GD.Print(res.SummaryJson());
			foreach (var line in res.Trace) GD.Print("  " + line);
		}
		GetTree().Quit(0);
	}
}
