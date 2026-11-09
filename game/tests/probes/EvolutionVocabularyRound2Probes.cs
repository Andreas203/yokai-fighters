using System;
using System.IO;
using Godot;
using System.Text.Json.Nodes;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-220 Test Breaker round 2 (after 232d26f). The loader side of tools/tests/test_evolution_vocabulary_round2_probes.py:
/// every level and the EX version of a special are run by the loader, a throw's frame data is read with the whole-number rule.
/// </summary>
public static class EvolutionVocabularyRound2Probes
{
	static JsonObject Doc(string dir, string file) =>
		JsonNode.Parse(File.ReadAllText(ProjectSettings.GlobalizePath(dir) + "/" + file))!.AsObject();

	static bool Refused(Action a)
	{
		try { a(); } catch (FormatException) { return true; }
		return false;
	}

	static JsonObject Effect(string target, string value) =>
		JsonNode.Parse($"{{\"target\":\"{target}\",\"op\":\"set\",\"value\":{value}}}")!.AsObject();

	// EX versions: the EX changes of a special are built too (SpecialLoader.Parse builds plain and EX of every level).
	[Test]
	public static void ExChanges_WithPendingTargets_AreShapeChecked()
	{
		foreach (var (target, value, accept) in new[]
		{
			("decoy", "0", false), ("forward_travel", "-1", false), ("wall_bounce", "1", false),
			("decoy", "1", true), ("wall_bounce", "true", true), ("forward_travel", "0", true),
		})
		{
			var doc = Doc(FightScene.FixtureSpecialsDir, "test-spirit-wave.json");
			doc["id"] = "p2-ex";
			doc["ex"]!["changes"] = new JsonArray(Effect(target, value));
			Assert.True(Refused(() => SpecialLoader.Parse(doc.ToJsonString())) == !accept, $"ex {target} {value}: accept={accept}");
		}
	}

	[Test]
	public static void Lv2Tuning_WithPendingTarget_IsShapeChecked()
	{
		var doc = Doc(FightScene.FixtureSpecialsDir, "test-spirit-wave.json");
		doc["id"] = "p2-tuning";
		doc["levels"]!["2"]!["tuning"] = Effect("decoy", "0");
		Assert.True(Refused(() => SpecialLoader.Parse(doc.ToJsonString())), "Lv 2 tuning decoy 0 gives a decoy of 0 hits");
	}

	// The throw's own cancel_windows (YOK-220 lets them load): whole numbers inside int32, like every other frame number.
	[Test]
	public static void ThrowCancelWindow_OutsideInt32_IsRefused()
	{
		var doc = Doc(FightScene.FixtureThrowsDir, "test-ryo-throw.json");
		doc["frame_data"]!["cancel_windows"] = JsonNode.Parse("[{\"frames\":[1,3000000000],\"on\":[\"hit\"]}]");
		Assert.True(Refused(() => MoveLoader.Parse(doc.ToJsonString(), "test-ryo-throw")), "loader refuses 3000000000");
	}
}
