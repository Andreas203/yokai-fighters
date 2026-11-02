using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Godot;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-225 breaker probes, data side. command-grab-build-cases.json is read here and by
/// tools/tests/test_command_grab_probes.py: the loader and the validator must give the case list the same answer.
/// </summary>
public static class CommandGrabBuildProbes
{
	static string Repo => Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "..");

	static JsonObject Patched(JsonNode c)
	{
		var doc = JsonNode.Parse(File.ReadAllText(ProjectSettings.GlobalizePath("res://tests/fixtures/command-grabs/test-river-grab.json")))!.AsObject();
		foreach (var p in c["patch"]!.AsArray())
		{
			var path = p!["path"]!.AsArray().Select(x => x!.GetValue<string>()).ToArray();
			JsonNode node = doc;
			foreach (var k in path[..^1]) node = node is JsonArray a ? a[int.Parse(k)]! : node[k]!;
			var value = p["value"]?.DeepClone();
			if (value is null) ((JsonObject)node).Remove(path[^1]);
			else node[path[^1]] = value;
		}
		return doc;
	}

	[Test]
	public static void LoaderAnswersTheSharedBuildCases()
	{
		var root = JsonNode.Parse(File.ReadAllText(ProjectSettings.GlobalizePath("res://tests/probes/command-grab-build-cases.json")))!;
		var wrong = new List<string>();
		foreach (var c in root["cases"]!.AsArray())
		{
			string? refused = null;
			try { SpecialLoader.Parse(Patched(c!).ToJsonString()); }
			catch (FormatException e) { refused = e.Message; }
			catch (Exception e) { wrong.Add($"{c!["name"]}: loader CRASHED with {e.GetType().Name} instead of a FormatException refusal: {e.Message} at {e.StackTrace?.Split((char)10).FirstOrDefault()}"); continue; }
			if ((refused is null) != c!["accept"]!.GetValue<bool>())
				wrong.Add($"{c["name"]}: loader {(refused is null ? "accepted" : "refused: " + refused)}");
		}
		Assert.Equal("", string.Join(" | ", wrong), "loader vs the shared cases");
	}

	// "Strike-only modifiers on a grab": every real modifier that applies to the command grab must build at every level,
	// plain and EX (what AbilityPool does at load), or the draft crashes when a player pairs them.
	[Test]
	public static void EveryRealModifier_ThatAppliesToACommandGrab_BuildsOnIt()
	{
		var grab = SpecialLoader.Parse(File.ReadAllText(ProjectSettings.GlobalizePath("res://tests/fixtures/command-grabs/test-river-grab.json")));
		var bad = new List<string>();
		int applied = 0;
		foreach (var f in Directory.GetFiles($"{Repo}/data/modifiers", "*.json"))
		{
			ModifierData mod;
			try { mod = ModifierLoader.LoadFile(f); } catch (FormatException) { continue; }
			if (!mod.AppliesTo(grab)) continue;
			applied++;
			for (int level = 1; level <= grab.MaxLevel; level++)
				foreach (bool ex in new[] { false, true })
				{
					if (ex && !grab.HasEx) continue;
					try { grab.Build(level, ex, mod, null); }
					catch (FormatException e) { bad.Add($"{Path.GetFileNameWithoutExtension(f)} Lv{level}{(ex ? " EX" : "")}: {e.Message}"); }
				}
		}
		Assert.True(applied > 0, "some modifier applies to a command grab");
		Assert.Equal("", string.Join(" | ", bad), "modifier x command grab builds");
	}
}
