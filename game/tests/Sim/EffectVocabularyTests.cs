using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Regex = System.Text.RegularExpressions.Regex;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-220: the effect vocabulary for the remaining Lv 3 evolutions (wall_bounce, throw.range, multi_hit,
/// forward_travel as a distance, decoy). Valid and invalid shapes of each are the shared cases in
/// game/tests/fixtures/modifier-gate/cases.json (ModifierEffectTests.ValidatorAndLoaderAgreeOnTheSharedCases, and
/// tools/tests/test_validate_data.py on the validator side). Here: the C# list and the schema list are the same
/// set, and a move carrying the targets no fight code reads yet plays exactly as without them.
/// </summary>
public static class EffectVocabularyTests
{
	const int S = SimConfig.Scale;
	static string Repo => Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "..");

	static MoveData[] Moves() =>
		MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureNormalsDir))
			.Concat(MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureThrowsDir))).ToArray();

	[Test]
	public static void EveryVocabularyTargetIsOneTheSchemaNames()
	{
		var common = JsonNode.Parse(File.ReadAllText($"{Repo}/data/schema/common.schema.json"))!;
		var pattern = new Regex(common["$defs"]!["effectTarget"]!["pattern"]!.GetValue<string>());
		foreach (var target in Effects.Vocabulary.Keys)
		{
			Assert.True(pattern.IsMatch(target), $"schema effectTarget names {target}");
			if (target.StartsWith("properties.")) // the short form resolves to the same path (Effects.Resolve)
			{
				string shortForm = target["properties.".Length..];
				Assert.True(pattern.IsMatch(shortForm), $"schema effectTarget names {shortForm}");
				Assert.Equal(target, string.Join('.', Effects.Resolve(shortForm)), $"{shortForm} resolves under properties");
			}
		}
		foreach (var target in new[] { "wall_bounce", "throw.range", "multi_hit", "forward_travel", "decoy" })
			Assert.True(Effects.Vocabulary.ContainsKey($"properties.{target}"), $"YOK-220 target {target}");
		// What the schema still refuses, the loader refuses too.
		foreach (var target in new[] { "throw.breakable", "throw", "multi_hit.hits", "teleport", "absorbs_hits", "decoy.hits" })
		{
			Assert.True(!(pattern.IsMatch(target)), $"schema refuses {target}");
			Assert.True(!(Effects.Vocabulary.ContainsKey(string.Join('.', Effects.Resolve(target)))), $"loader refuses {target}");
		}
	}

	/// <summary>
	/// The ticket's "loads and plays unchanged": Rising Talisman at Lv 3 with wall_bounce, multi_hit, decoy and a
	/// properties.throw added hashes the same every tick as the plain Lv 3, whether it connects or gets jabbed.
	/// forward_travel is left out: YOK-221 gives it behaviour. Each behaviour ticket drops its target from this list.
	/// </summary>
	[Test]
	public static void TargetsWithNoBehaviourYetLoadAndPlayUnchanged()
	{
		string path = ProjectSettings.GlobalizePath(FightScene.FixtureSpecialsDir) + "/test-rising-talisman.json";
		var plain = SpecialLoader.Parse(File.ReadAllText(path));
		var doc = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
		doc["levels"]!["1"]!["frame_data"]!["properties"]!["throw"] = new JsonObject { ["range"] = 80, ["breakable"] = true };
		var changes = doc["levels"]!["3"]!["evolution"]!["changes"]!.AsArray();
		changes.Add(new JsonObject { ["target"] = "properties.wall_bounce", ["op"] = "set", ["value"] = true });
		changes.Add(new JsonObject { ["target"] = "multi_hit", ["op"] = "set", ["value"] = new JsonObject { ["hits"] = 3, ["interval"] = 6.0 } });
		changes.Add(new JsonObject { ["target"] = "decoy", ["op"] = "set", ["value"] = 1 });
		changes.Add(new JsonObject { ["target"] = "throw.range", ["op"] = "mul_pct", ["value"] = 150 });
		var pending = SpecialLoader.Parse(doc.ToJsonString());

		var built = pending.Build(3, false);
		Assert.Equal(plain.Build(3, false).TotalFrames, built.TotalFrames, "same frame data");

		foreach (bool p2Jabs in new[] { false, true })
		{
			Match Fight(SpecialData sp)
			{
				var m = new Match(null, Moves(), Moves());
				m.P1.Equip(SpecialSlot.B, sp, 3);
				m.P1.X = -60 * S;
				m.P2.X = 60 * S;
				return m;
			}
			var a = Fight(plain);
			var b = Fight(pending);
			int lp = Array.FindIndex(a.P2.Moves, mv => mv.Button == InputBits.LightPunch && !mv.Air);
			for (int t = 1; t <= 120; t++)
			{
				var p1 = t == 1 ? FighterInput.Special(SpecialSlot.B) : FighterInput.None;
				var p2 = p2Jabs && t == 1 ? FighterInput.Attack(lp) : FighterInput.None;
				a.Step(p1, p2);
				b.Step(p1, p2);
				Assert.Equal(a.StateHash(), b.StateHash(), $"tick {t} (P2 jabs: {p2Jabs})");
			}
			Assert.True(a.P1.Health < 1000 || a.P2.Health < 1000, "something connected");
		}
	}

	/// <summary>The coordinator's YOK-217 ask: a throw file's cancel_windows load (throw.schema.json now takes them; X4, Undertow).</summary>
	[Test]
	public static void AThrowsCancelWindowsLoad()
	{
		string path = ProjectSettings.GlobalizePath(FightScene.FixtureThrowsDir) + "/test-ryo-throw.json";
		var doc = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
		doc["frame_data"]!["cancel_windows"] = JsonNode.Parse("[{ \"frames\": [14, 22], \"on\": [\"hit\"] }]");
		var move = MoveLoader.Parse(doc.ToJsonString(), "test-ryo-throw");
		Assert.True(move.IsThrow, "still a throw");
		Assert.Equal(1, move.CancelWindows.Count, "one window");
		var w = move.CancelWindows[0];
		Assert.True(w.First == 14 && w.Last == 22 && w.OnHit && !w.OnBlock && !w.OnWhiff, "frames 14-22 on hit");
	}
}
