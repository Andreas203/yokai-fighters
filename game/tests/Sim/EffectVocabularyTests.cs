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
	/// The ticket's "loads and plays unchanged": Rising Talisman at Lv 3 with wall_bounce, multi_hit and decoy added
	/// hashes the same every tick as the plain Lv 3, whether it connects or gets jabbed, by its evolution or by a
	/// modifier (always and on_hit, Test Breaker finding). forward_travel is left out: YOK-221 gives it behaviour; so is
	/// properties.throw: YOK-225 makes it a command grab (CommandGrabTests). decoy stays: YOK-224 leaves a decoy only
	/// where a move teleports, and Rising Talisman doesn't (FoxMirageTests covers the teleport). Each behaviour ticket
	/// drops its target here.
	/// </summary>
	[Test]
	public static void TargetsWithNoBehaviourYetLoadAndPlayUnchanged()
	{
		string path = ProjectSettings.GlobalizePath(FightScene.FixtureSpecialsDir) + "/test-rising-talisman.json";
		var plain = SpecialLoader.Parse(File.ReadAllText(path));
		var doc = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
		var changes = doc["levels"]!["3"]!["evolution"]!["changes"]!.AsArray();
		changes.Add(new JsonObject { ["target"] = "properties.wall_bounce", ["op"] = "set", ["value"] = true });
		changes.Add(new JsonObject { ["target"] = "multi_hit", ["op"] = "set", ["value"] = new JsonObject { ["hits"] = 3, ["interval"] = 6.0 } });
		changes.Add(new JsonObject { ["target"] = "decoy", ["op"] = "set", ["value"] = 1 }); // no teleport: no decoy (YOK-224)
		var pending = SpecialLoader.Parse(doc.ToJsonString());

		var built = pending.Build(3, false);
		Assert.Equal(plain.Build(3, false).TotalFrames, built.TotalFrames, "same frame data");

		// The level path: the evolution carries them.
		Trace(plain, null, pending, null, "evolution");
		// The modifier path, plain and conditional (outcome variants): a same-id modifier carrying them against one
		// whose only change is damage + 0.
		const string pendingEffects =
			"{\"target\":\"wall_bounce\",\"op\":\"set\",\"value\":true,\"when\":\"W\"}," +
			"{\"target\":\"multi_hit\",\"op\":\"set\",\"value\":{\"hits\":2,\"interval\":4},\"when\":\"W\"}," +
			"{\"target\":\"decoy\",\"op\":\"set\",\"value\":2,\"when\":\"W\"}"; // throw.range: YOK-225, CommandGrabTests
		const string noOp = "{\"target\":\"damage\",\"op\":\"add\",\"value\":0,\"when\":\"W\"}";
		static ModifierData Mod(string effects, string when) => ModifierLoader.Parse(
			"{\"kind\":\"modifier\",\"id\":\"yok220-pending\",\"source\":\"oni\",\"applies_to\":{\"requires\":[]},\"effects\":["
			+ effects.Replace("\"W\"", $"\"{when}\"") + "]}");
		foreach (string when in new[] { "always", "on_hit" })
			Trace(pending, Mod(noOp, when), pending, Mod(pendingEffects + "," + noOp, when), $"modifier {when}");
	}

	/// <summary>Two fights, P1 Rising Talisman Lv 3 on tick 1 (P2 idle, then P2 jabbing): equal hashes on every tick.</summary>
	static void Trace(SpecialData spA, ModifierData? modA, SpecialData spB, ModifierData? modB, string label)
	{
		foreach (bool p2Jabs in new[] { false, true })
		{
			Match Fight(SpecialData sp, ModifierData? mod)
			{
				var m = new Match(null, Moves(), Moves());
				m.P1.Equip(SpecialSlot.B, sp, 3, mod);
				m.P1.X = -60 * S;
				m.P2.X = 60 * S;
				return m;
			}
			var a = Fight(spA, modA);
			var b = Fight(spB, modB);
			int lp = Array.FindIndex(a.P2.Moves, mv => mv.Button == InputBits.LightPunch && !mv.Air);
			bool talismanHit = false;
			for (int t = 1; t <= 120; t++)
			{
				var p1 = t == 1 ? FighterInput.Special(SpecialSlot.B) : FighterInput.None;
				var p2 = p2Jabs && t == 1 ? FighterInput.Attack(lp) : FighterInput.None;
				a.Step(p1, p2);
				b.Step(p1, p2);
				talismanHit |= a.P2.Health < 1000;
				Assert.Equal(a.StateHash(), b.StateHash(), $"{label}: tick {t} (P2 jabs: {p2Jabs})");
			}
			Assert.True(talismanHit || p2Jabs, $"{label}: the talisman connected, so on_hit variants were played");
			Assert.True(a.P1.Health < 1000 || a.P2.Health < 1000, $"{label}: something connected");
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
