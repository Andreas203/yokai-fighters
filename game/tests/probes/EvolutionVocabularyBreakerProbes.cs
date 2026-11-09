using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Regex = System.Text.RegularExpressions.Regex;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-220 Test Breaker. Expected values come from the ticket ("loaded and shape-checked, no behaviour": the move plays
/// exactly as without the property), frame-data.schema.json / common.schema.json, and the loader-equals-validator rule
/// (YOK-103). The case list evolution_vocabulary_cases.json is the one tools/tests/test_evolution_vocabulary_probes.py reads.
/// </summary>
public static class EvolutionVocabularyBreakerProbes
{
	const int S = SimConfig.Scale;
	static string Repo => Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "..");
	static JsonNode Cases() => JsonNode.Parse(File.ReadAllText($"{Repo}/game/tests/probes/evolution_vocabulary_cases.json"))!;
	static string Fixtures(string dir) => ProjectSettings.GlobalizePath(dir);

	static MoveData[] Moves() =>
		MoveLoader.LoadDirectory(Fixtures(FightScene.FixtureNormalsDir))
			.Concat(MoveLoader.LoadDirectory(Fixtures(FightScene.FixtureThrowsDir))).ToArray();

	static SpecialData[] AllSpecials() =>
		AbilityPool.LoadRepo(Repo).Specials.Concat(AbilityPool.LoadFixtures(Repo).Specials)
			.GroupBy(sp => sp.Id).Select(g => g.First()).ToArray();

	// Shared with the validator: a modifier case with a charge object, loaded then built on every special it applies to.
	// Rule: the schema (effectShape) and the ticket's shapes decide accept, not the loader's current answer.
	[Test]
	public static void ModifierCases_LoaderAgreesWithTheProbeList()
	{
		var root = Cases();
		var specials = AllSpecials();
		var wrong = new List<string>();
		foreach (var c in root["cases"]!.AsArray())
		{
			string name = c!["name"]!.GetValue<string>();
			var file = new JsonObject
			{
				["kind"] = "modifier", ["id"] = name, ["name"] = name, ["source"] = "oni", ["rarity"] = "common", ["locked"] = false,
				["applies_to"] = new JsonObject { ["requires"] = c["requires"]!.DeepClone() },
				["effects"] = c["effects"]!.DeepClone(),
			};
			if (c["withCharge"]?.GetValue<bool>() == true) file["charge"] = root["charge"]!.DeepClone();
			string? refused = null;
			try
			{
				var mod = ModifierLoader.Parse(file.ToJsonString());
				foreach (var sp in specials.Where(mod.AppliesTo))
					for (int level = 1; level <= sp.MaxLevel; level++)
					{
						sp.Build(level, false, mod, null);
						if (sp.HasEx) sp.Build(level, true, mod, null);
					}
			}
			catch (FormatException e) { refused = e.Message; }
			if ((refused is null) != c["accept"]!.GetValue<bool>())
				wrong.Add($"{name}: loader {(refused is null ? "accepted" : "refused: " + refused)}");
		}
		Assert.Equal("", string.Join(" | ", wrong), "modifier cases");
	}

	// A special's own frame data and evolution changes (SpecialLoader.Parse builds every level, plain and EX).
	[Test]
	public static void SpecialCases_LoaderAgreesWithTheProbeList()
	{
		var wrong = new List<string>();
		foreach (var c in Cases()["special_cases"]!.AsArray())
		{
			string name = c!["name"]!.GetValue<string>();
			var doc = JsonNode.Parse(File.ReadAllText(Fixtures(FightScene.FixtureSpecialsDir) + $"/{c["base"]!.GetValue<string>()}.json"))!.AsObject();
			doc["id"] = name;
			var fd = doc["levels"]!["1"]!["frame_data"]!.AsObject();
			foreach (var kv in c["set"]!.AsObject()) fd[kv.Key] = kv.Value?.DeepClone();
			foreach (var k in c["remove"]!.AsArray()) fd.Remove(k!.GetValue<string>());
			if (c["changes"] is { } changes) doc["levels"]!["3"]!["evolution"]!["changes"] = changes.DeepClone();
			string? refused = null;
			try { SpecialLoader.Parse(doc.ToJsonString()); }
			catch (FormatException e) { refused = e.Message; }
			if ((refused is null) != c["accept"]!.GetValue<bool>())
				wrong.Add($"{name}: loader {(refused is null ? "accepted" : "refused: " + refused)}");
		}
		Assert.Equal("", string.Join(" | ", wrong), "special cases");
	}

	// A normal's properties go through MoveLoader too (CheckPendingProperties runs for every kind).
	[Test]
	public static void NormalCases_LoaderAgreesWithTheProbeList()
	{
		string text = File.ReadAllText(Fixtures(FightScene.FixtureNormalsDir) + "/test-ryo-heavy-kick.json");
		var wrong = new List<string>();
		foreach (var c in Cases()["normal_cases"]!.AsArray())
		{
			var doc = JsonNode.Parse(text)!.AsObject();
			doc["frame_data"]!["properties"] = c!["properties"]!.DeepClone();
			string? refused = null;
			try { MoveLoader.Parse(doc.ToJsonString(), "x"); }
			catch (FormatException e) { refused = e.Message; }
			if ((refused is null) != c["accept"]!.GetValue<bool>())
				wrong.Add($"{c["name"]}: loader {(refused is null ? "accepted" : "refused: " + refused)}");
		}
		Assert.Equal("", string.Join(" | ", wrong), "normal cases");
	}

	// The reverse of EveryVocabularyTargetIsOneTheSchemaNames: a string the schema pattern matches that the loader
	// refuses (or the other way) is a disagreement the author's one-way check cannot see.
	[Test]
	public static void SchemaTargetPattern_AndLoaderVocabularyAreTheSameSet()
	{
		var common = JsonNode.Parse(File.ReadAllText($"{Repo}/data/schema/common.schema.json"))!;
		var pattern = new Regex(common["$defs"]!["effectTarget"]!["pattern"]!.GetValue<string>());
		var candidates = new List<string>(Effects.Vocabulary.Keys);
		candidates.AddRange(Effects.Vocabulary.Keys.Where(k => k.StartsWith("properties.")).Select(k => k["properties.".Length..]));
		candidates.AddRange(new[]
		{
			"throw.range", "properties.throw.range", "throw.range.x", "throw.breakable", "properties.throw.break_window", "throw",
			"wall_bounce", "properties.wall_bounce", "wall_bounce.x", "multi_hit", "multi_hit.hits", "multi_hit.interval",
			"decoy", "properties.decoy", "decoy.x", "forward_travel", "properties.forward_travel", "forward_travel.x",
			"properties.properties.decoy", "Decoy", "decoy ", "properties.", "properties", "knockdown", "hitboxes", "teleport",
		});
		var wrong = new List<string>();
		foreach (var t in candidates.Distinct())
		{
			bool schema = pattern.IsMatch(t);
			bool loader = Effects.Vocabulary.ContainsKey(string.Join('.', Effects.Resolve(t)));
			if (schema != loader) wrong.Add($"{t}: schema {schema}, loader {loader}");
		}
		Assert.Equal("", string.Join(" | ", wrong), "effect target sets");
	}

	// "Plays unchanged" with a CONDITIONAL pending modifier: on_hit effects make an OnHitVariant, and a projectile's
	// FiredBy is mixed into the hash only when a variant exists (Match.Specials.cs:395). The ticket says the pending
	// vocabulary changes nothing, so the hash trace must equal the same special with no modifier at all.
	static ModifierData PendingModifier(string when, string effects) =>
		ModifierLoader.Parse(
			"{\"kind\":\"modifier\",\"id\":\"p-pending\",\"source\":\"oni\",\"applies_to\":{\"requires\":[]},\"effects\":[" + effects.Replace("WHEN", when) + "]}");

	// The baseline has the same modifier id (Fighter.StateHash mixes it) and the same condition, with an effect that changes nothing.
	const string NoOpEffects = "{\"target\":\"damage\",\"op\":\"add\",\"value\":0,\"when\":\"WHEN\"}";

	const string PendingEffects =
		"{\"target\":\"wall_bounce\",\"op\":\"set\",\"value\":true,\"when\":\"WHEN\"}," +
		"{\"target\":\"decoy\",\"op\":\"set\",\"value\":1,\"when\":\"WHEN\"}," +
		"{\"target\":\"multi_hit\",\"op\":\"set\",\"value\":{\"hits\":3,\"interval\":6},\"when\":\"WHEN\"}";

	static void TraceEqual(string specialId, string when, int level, bool p2Jabs, int p2x)
	{
		var sp = AbilityPool.LoadFixtures(Repo).Specials.First(s => s.Id == specialId);
		var mod = PendingModifier(when, PendingEffects);
		Match Fight(ModifierData? m)
		{
			var match = new Match(null, Moves(), Moves());
			match.P1.Equip(SpecialSlot.B, sp, level, m);
			match.P1.X = -60 * S;
			match.P2.X = p2x * S;
			return match;
		}
		var a = Fight(PendingModifier(when, NoOpEffects));
		var b = Fight(mod);
		int lp = Array.FindIndex(a.P2.Moves, mv => mv.Button == InputBits.LightPunch && !mv.Air);
		bool connected = false;
		for (int t = 1; t <= 150; t++)
		{
			var p1 = t == 1 ? FighterInput.Special(SpecialSlot.B) : FighterInput.None;
			var p2 = p2Jabs && t == 1 ? FighterInput.Attack(lp) : FighterInput.None;
			a.Step(p1, p2);
			b.Step(p1, p2);
			connected |= a.P2.Health < 1000 || a.P1.Health < 1000;
			Assert.Equal(a.StateHash(), b.StateHash(), $"{specialId} {when} lv{level} jabs={p2Jabs}: tick {t}");
		}
		Assert.True(connected, $"{specialId} {when}: something connected, so the trace means something");
	}

	[Test]
	public static void OnHitPendingModifier_MeleeSpecial_HashEqualsPlain()
	{
		TraceEqual("test-rising-talisman", "on_hit", 3, false, 60);
		TraceEqual("test-rising-talisman", "on_hit", 3, true, 60);
	}

	[Test]
	public static void OnBlockPendingModifier_MeleeSpecial_HashEqualsPlain() =>
		TraceEqual("test-rising-talisman", "on_block", 3, false, 60);

	[Test]
	public static void AlwaysPendingModifier_ProjectileSpecial_HashEqualsPlain() =>
		TraceEqual("test-spirit-wave", "always", 1, false, 300);

	[Test]
	public static void OnHitPendingModifier_ProjectileSpecial_HashEqualsPlain() =>
		TraceEqual("test-spirit-wave", "on_hit", 1, false, 300);

	[Test]
	public static void OnBlockPendingModifier_ProjectileSpecial_HashEqualsPlain() =>
		TraceEqual("test-spirit-wave", "on_block", 1, false, 300);

	// An accepted shape the sim must be able to run: a modifier that carries the charge dash switch AND an integer
	// forward_travel loads, builds on every special and a fight with it steps without throwing.
	[Test]
	public static void ChargeDashAndIntegerTravelInOneModifier_Fights()
	{
		var mod = ModifierLoader.Parse(
			"{\"kind\":\"modifier\",\"id\":\"p-both\",\"source\":\"oni\",\"applies_to\":{\"requires\":[]},\"charge\":" +
			Cases()["charge"]!.ToJsonString() + ",\"effects\":[" +
			"{\"target\":\"properties.forward_travel\",\"op\":\"set\",\"value\":\"charge_dash\",\"when\":\"charge_released\"}," +
			"{\"target\":\"properties.forward_travel\",\"op\":\"set\",\"value\":120}]}");
		Assert.True(mod.Charge != null, "the charge object is carried");
		Assert.Equal(1, mod.FrameEffects.Count, "the switch is not a frame effect, the distance is");
		var sp = AbilityPool.LoadFixtures(Repo).Specials.First(s => s.Id == "test-rising-talisman");
		var match = new Match(null, Moves(), Moves());
		match.P1.Equip(SpecialSlot.B, sp, 3, mod);
		match.P1.X = -60 * S;
		match.P2.X = 60 * S;
		for (int t = 1; t <= 90; t++) match.Step(t == 1 ? FighterInput.Special(SpecialSlot.B) : FighterInput.None, FighterInput.None);
		Assert.True(true, "stepped without throwing");
	}

	// Determinism: the same equip twice gives the same hash trace (a pending property must not add unseeded state).
	[Test]
	public static void OnHitPendingModifier_SameRunTwice_SameHash()
	{
		ulong Run(bool pending)
		{
			var sp = AbilityPool.LoadFixtures(Repo).Specials.First(s => s.Id == "test-spirit-wave");
			var mod = PendingModifier("on_hit", pending ? PendingEffects : NoOpEffects);
			var match = new Match(null, Moves(), Moves());
			match.P1.Equip(SpecialSlot.B, sp, 1, mod);
			match.P1.X = -60 * S;
			match.P2.X = 300 * S;
			for (int t = 1; t <= 120; t++) match.Step(t == 1 ? FighterInput.Special(SpecialSlot.B) : FighterInput.None, FighterInput.None);
			return (ulong)match.StateHash();
		}
		Assert.Equal(Run(true), Run(true), "same inputs, same hash");
		Assert.Equal(Run(false), Run(true), "pending effects == a no-op effect under the same modifier id");
	}
}
