using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using YokaiFighters.Harness;

namespace YokaiFighters.Tests;

/// <summary>YOK-170 probes: the bot skill levels loader, mixed and per-level batches, the per-level summary (R5, Y4, P6).</summary>
public static class BotLevelsProbes
{
	static HarnessContent? _content;
	static HarnessContent Content => _content ??= HarnessContent.Load(ContentPaths.DataDir, ContentPaths.FixturesDir);
	static string BaseId => BotConfig.DefaultId;
	static string BaseText => File.ReadAllText(Path.Combine(ContentPaths.DataDir, "harness", BaseId + ".json"));
	static BotLevels Levels => BotLevels.LoadDefault(ContentPaths.DataDir);

	static string File1(string levelsArray, string baseId = "newcomer-kihon-bot") =>
		$"{{\"kind\":\"harness_bot_levels\",\"id\":\"t-levels\",\"base\":\"{baseId}\",\"levels\":[{levelsArray}]}}";
	static string Lv(string name, string extra = "", string? botId = null) =>
		$"{{\"level\":\"{name}\",\"bot_id\":\"{botId ?? name + "-bot"}\",\"status\":\"proposed\"{extra}}}";
	static BotLevels Load(string json, Func<string, string>? b = null) => BotLevels.Parse(json, b ?? (_ => BaseText));

	static void Rejects(string json, string why, Func<string, string>? b = null)
	{
		try { Load(json, b); }
		catch (FormatException) { return; }
		catch (Exception e) { throw new Exception($"{why}: expected FormatException, got {e.GetType().Name}: {e.Message}"); }
		throw new Exception($"{why}: accepted");
	}

	// Strict loader, base ids: only a bare id names a file in data/harness (data schema, YOK-170).
	[Test]
	public static void Base_TraversalAbsoluteSeparatorsNewline_Rejected_WithoutReadingAFile()
	{
		bool read = false;
		Func<string, string> spy = id => { read = true; return BaseText; };
		foreach (string bad in new[] { "../newcomer-kihon-bot", "..", "/etc/passwd", "C:/x", "a/b", "a\\\\b", "newcomer-kihon-bot\\n", "Newcomer-kihon-bot", "", "-x", "n\\u00e9" })
			Rejects(File1(Lv("a"), bad), $"base '{bad}'", spy);
		Assert.True(!read, "no file is read for a rejected base id");
	}

	[Test]
	public static void Names_UppercaseUnicodeReservedNewlineSpaces_Rejected()
	{
		foreach (string bad in new[] { "Newcomer", "caf\\u00e9", "a\\u0663", "all", "mixed", "a b", "a_b", "1a", "", "a\\n", "-a" })
			Rejects(File1(Lv(bad, "", "ok-bot")), $"level name '{bad}'");
		foreach (string bad in new[] { "Bot", "b\\u00e9", "b\\n", "b b" })
			Rejects(File1(Lv("ok", "", bad)), $"bot_id '{bad}'");
	}

	[Test]
	public static void DuplicateKeys_AreAFormatException_NotAnotherException()
	{
		Rejects("{\"kind\":\"harness_bot_levels\",\"id\":\"a\",\"id\":\"b\",\"base\":\"" + BaseId + "\",\"levels\":[" + Lv("a") + "]}", "duplicate top key");
		Rejects(File1("{\"level\":\"a\",\"level\":\"b\",\"bot_id\":\"a-bot\",\"status\":\"final\"}"), "duplicate level key");
		Rejects(File1(Lv("a", ",\"fight\":{\"mistake_pct\":1,\"mistake_pct\":2}")), "duplicate fight key");
	}

	[Test]
	public static void FixedKeys_CannotBeOverridden_AtAnyDepth()
	{
		foreach (string extra in new[]
		{
			",\"fight\":{\"max_fight_ticks\":600}", ",\"fight\":{\"weights\":{\"max_fight_ticks\":1}}", ",\"run\":{\"menu_seconds_per_node\":0}",
			",\"id\":\"x\"", ",\"scheme\":\"kihon\"", ",\"kind\":\"harness_bot\"", ",\"fight\":{\"weights\":{\"walk\":1,\"Walk\":2}}",
			",\"run\":{\"route\":{\"rule\":\"random\"},\"menu_seconds_per_node\":1}", ",\"fight\":{\"__proto__\":1}", ",\"run\":{\"id\":\"y\"}",
		})
			Rejects(File1(Lv("a", extra)), $"extra {extra}");
	}

	[Test]
	public static void BadValues_HugeNegativeFractionalWrongKind_AreFormatExceptions()
	{
		foreach (string f in new[]
		{
			"\"reaction_frames\":2147483648", "\"reaction_frames\":-1", "\"reaction_frames\":0", "\"reaction_frames\":1e400", "\"reaction_frames\":1.5",
			"\"reaction_frames\":\"18\"", "\"reaction_frames\":true", "\"reaction_frames\":null", "\"reaction_frames\":[18]", "\"reaction_frames\":{}",
			"\"mistake_pct\":101", "\"mistake_pct\":-1", "\"mistake_pct\":99999999999999999999", "\"anti_air_button\":\"nope\"", "\"anti_air_button\":5",
			"\"preferred_min\":4000,\"preferred_max\":0", "\"weights\":null", "\"weights\":[]", "\"weights\":{\"walk\":-1}", "\"weights\":{\"walk\":1001}",
			"\"weights\":{\"walk\":0,\"normal\":0,\"special\":0,\"throw\":0,\"jump\":0,\"wait\":0}", "\"weights\":{\"walk\":null}",
		})
			Rejects(File1(Lv("a", $",\"fight\":{{{f}}}")), $"fight {f}");
		foreach (string r in new[]
		{
			"\"heal_below_pct\":101", "\"heal_below_pct\":-5", "\"merchant\":\"steal\"", "\"merchant\":3", "\"dojo\":null", "\"reward_order\":[\"upgrade\",\"upgrade\"]",
			"\"reward_order\":[5]", "\"reward_order\":\"upgrade\"", "\"route\":{\"rule\":\"random\",\"prefer_types\":[5]}", "\"route\":null", "\"route\":{\"rule\":\"x\"}",
			"\"route\":5",
		})
			Rejects(File1(Lv("a", $",\"run\":{{{r}}}")), $"run {r}");
	}

	[Test]
	public static void TopLevel_WrongShapes_AreFormatExceptions()
	{
		foreach (string j in new[] { "", "null", "[]", "5", "\"x\"", "{", "{}", "{\"kind\":5}",
			"{\"kind\":\"harness_bot_levels\",\"id\":\"a\",\"base\":\"" + BaseId + "\",\"levels\":[]}",
			"{\"kind\":\"harness_bot_levels\",\"id\":\"a\",\"base\":\"" + BaseId + "\",\"levels\":{}}",
			"{\"kind\":\"harness_bot_levels\",\"id\":\"a\",\"base\":\"" + BaseId + "\",\"levels\":[null]}",
			"{\"kind\":\"harness_bot_levels\",\"id\":\"a\",\"base\":\"" + BaseId + "\",\"levels\":[5]}",
			"{\"kind\":\"harness_bot_levels\",\"id\":7,\"base\":\"" + BaseId + "\",\"levels\":[" + Lv("a") + "]}",
			File1(Lv("a", ",\"fight\":5")), File1(Lv("a", ",\"run\":[]")), File1("{\"level\":\"a\",\"bot_id\":\"a-bot\",\"status\":\"draft\"}"),
			File1(Lv("a") + "," + Lv("a", "", "other-bot")), File1(Lv("a") + "," + Lv("b", "", "a-bot")),
		})
			Rejects(j, $"file '{(j.Length > 60 ? j[..60] : j)}'");
	}

	[Test]
	public static void BaseFile_Problems_AreFormatExceptions()
	{
		Rejects(File1(Lv("a")), "base file missing", _ => throw new FileNotFoundException("gone"));
		Rejects(File1(Lv("a")), "base directory missing", _ => throw new DirectoryNotFoundException("gone"));
		Rejects(File1(Lv("a")), "base file's id differs", _ => BaseText.Replace("\"" + BaseId + "\"", "\"other-bot\""));
		Rejects(File1(Lv("a")), "base file is not a bot", _ => "{\"kind\":\"x\"}");
	}

	// An empty level is the base bot under its own id; levels do not leak into each other.
	[Test]
	public static void EmptyLevel_EqualsBase_AndOverridesDoNotLeakBetweenLevels()
	{
		var l = Load(File1(Lv("a", ",\"fight\":{\"mistake_pct\":50,\"weights\":{\"jump\":9}},\"run\":{\"merchant\":\"skip\"}") + "," + Lv("b") + "," + Lv("c", ",\"fight\":{}")));
		var bas = BotConfig.Parse(BaseText);
		Assert.Equal(50, l["a"].Fight.MistakePct, "a overrides");
		Assert.True(l["b"].Fight == bas.Fight && l["c"].Fight == bas.Fight, "b and c (empty fight object) are the base's fight config");
		Assert.True(l["b"].Run.Merchant == bas.Run.Merchant, "b keeps the base merchant plan");
		Assert.Equal("b-bot", l["b"].Id, "own bot id");
		Assert.Equal("c", l["c"].Level, "level carried");
	}

	[Test]
	public static void LevelNamedLikeBaseId_IsAllowed_AndLookupIsExactByLevelName()
	{
		var l = Load(File1(Lv(BaseId, "", "other-bot")));
		Assert.Equal("other-bot", l[BaseId].Id, "level name = base id still indexes by level, not bot id");
		foreach (string n in new[] { "bogus", "", "all", "mixed", "newcomer ", "NEWCOMER", "Newcomer", "newcomer-kihon-bot" })
		{
			bool threw = false;
			try { _ = Levels[n]; } catch (KeyNotFoundException) { threw = true; }
			Assert.True(threw, $"'{n}' is not a level name");
		}
	}

	// Results file names: one per level, never the mixed file's, never a bot file's own name.
	[Test]
	public static void ResultsFileNames_PerLevelAreDistinct_MixedDiffersFromAll()
	{
		var levels = Levels;
		var names = levels.Levels.Select(b => HarnessReport.FileName(new BatchResult(b, 1))).ToList();
		names.Add(HarnessReport.FileName(new BatchResult(levels.Levels[0], 1, $"{levels.Id}-mixed")));
		Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count(), "every batch writes its own file (case-insensitive FS)");
	}

	[Test]
	public static void Mixed_RunsNotDividingByLevelCount_AndTinyCounts()
	{
		var levels = Levels;
		var m4 = RunHarness.PlayMixed(Content, levels, 7, 4); // 4 runs over 3 levels: n, i, s, n
		string[] want = { "newcomer", "intermediate", "skilled", "newcomer" };
		Assert.Equal(string.Join(",", want), string.Join(",", m4.Runs.Select(r => r.Level)), "level of run i = i mod 3");
		for (int i = 0; i < 4; i++)
			Assert.Equal(RunHarness.PlayRun(Content, levels[want[i]], 7u + (uint)i).SessionHash, m4.Runs[i].SessionHash, $"run {i}: seed base+{i}, that level's bot");
		using var doc = JsonDocument.Parse(HarnessReport.ToJson(m4));
		var by = doc.RootElement.GetProperty("summary").GetProperty("levels");
		Assert.Equal("newcomer,intermediate,skilled", string.Join(",", by.EnumerateObject().Select(p => p.Name)), "summary order = first appearance");
		Assert.Equal(2, by.GetProperty("newcomer").GetProperty("runs").GetInt32(), "newcomer plays runs 0 and 3");
		Assert.Equal(1, by.GetProperty("skilled").GetProperty("runs").GetInt32(), "skilled plays run 2");

		var m1 = RunHarness.PlayMixed(Content, levels, 7, 1);
		Assert.Equal("newcomer", m1.Runs.Single().Level, "one run is the first level");
		using var d1 = JsonDocument.Parse(HarnessReport.ToJson(m1));
		Assert.Equal(1, d1.RootElement.GetProperty("summary").GetProperty("levels").EnumerateObject().Count(), "levels that played nothing are not listed");

		var m0 = RunHarness.PlayMixed(Content, levels, 7, 0);
		using var d0 = JsonDocument.Parse(HarnessReport.ToJson(m0));
		Assert.Equal(0, d0.RootElement.GetProperty("summary").GetProperty("levels").EnumerateObject().Count(), "zero runs: empty levels, no crash");
	}

	[Test]
	public static void Mixed_SeedWrapAtUIntMax_StillSeedBasePlusIndex()
	{
		var levels = Levels;
		uint s = uint.MaxValue - 1;
		var m = RunHarness.PlayMixed(Content, levels, s, 3);
		for (int i = 0; i < 3; i++)
			Assert.Equal(unchecked(s + (uint)i), m.Runs[i].Seed, $"run {i} seed wraps like the per-level batch's");
	}

	static FightRecord Fake(int dealt, int taken, bool won = false) =>
		new() { Profile = "p", Won = won, Ticks = 10, DamageDealt = dealt, DamageTaken = taken, HealthLeft = 0, Bot = new BotStats() };

	static JsonDocument LevelDoc(int dealt, int taken, string level = "newcomer")
	{
		var bot = Levels[level];
		var batch = new BatchResult(bot, 1);
		batch.Runs.Add(RunHarness.PlayRun(Content, bot, 1, (c, b, s, _) => Fake(dealt, taken)));
		return JsonDocument.Parse(HarnessReport.ToJson(batch));
	}

	static JsonElement Lvl(JsonDocument d, string level = "newcomer") => d.RootElement.GetProperty("summary").GetProperty("levels").GetProperty(level);

	[Test]
	public static void Summary_ZeroDamageFights_RatioIsNull_NoDivisionByZero()
	{
		using (var d = LevelDoc(0, 0))
			Assert.Equal(JsonValueKind.Null, Lvl(d).GetProperty("damage_ratio").ValueKind, "0/0 is null, not NaN or 0");
		using (var d = LevelDoc(50, 0))
			Assert.Equal(JsonValueKind.Null, Lvl(d).GetProperty("damage_ratio").ValueKind, "x/0 is null");
		using (var d = LevelDoc(0, 40))
			Assert.Equal(0.0, Lvl(d).GetProperty("damage_ratio").GetDouble(), "0/40 is 0");
	}

	[Test]
	public static void Summary_ExchangeRatio_RoundsHalfAwayAtTwoPlaces_AndHasNoHitRatioWithoutLog()
	{
		using var d = LevelDoc(3, 8); // 0.375
		var s = Lvl(d);
		Assert.Equal(0.38, s.GetProperty("damage_ratio").GetDouble(), "3/8 = 0.375 -> 0.38");
		// YOK-179, designer 2026-10-21: exchange_ratio counts hits, so with no hit log it is null; hit_exchange_ratio is gone.
		Assert.Equal(JsonValueKind.Null, s.GetProperty("exchange_ratio").ValueKind, "no hit log: exchange_ratio (hits) is null");
		Assert.True(!s.TryGetProperty("hit_exchange_ratio", out _), "hit_exchange_ratio is gone");
		Assert.Equal(3L, s.GetProperty("damage_dealt").GetInt64(), "damage dealt");
		Assert.Equal(8L, s.GetProperty("damage_taken").GetInt64(), "damage taken");
		Assert.Equal(1, s.GetProperty("duels").GetInt32(), "one duel");
		Assert.Equal(0, s.GetProperty("duels_won").GetInt32(), "none won");
	}

	// Newcomer = today's bot: same runs, same summary, byte for byte apart from the level labels.
	[Test]
	public static void Newcomer_MatchesBotFile_ByteForByteApartFromLevelLabel()
	{
		var viaLevel = JsonNode.Parse(HarnessReport.ToJson(RunHarness.PlayBatch(Content, Levels["newcomer"], 3, 2)))!.AsObject();
		var viaFile = JsonNode.Parse(HarnessReport.ToJson(RunHarness.PlayBatch(Content, BotConfig.LoadDefault(ContentPaths.DataDir), 3, 2)))!.AsObject();
		foreach (var doc in new[] { viaLevel, viaFile })
		{
			foreach (var r in doc["runs"]!.AsArray()) r!.AsObject().Remove("level");
			doc["summary"]!.AsObject().Remove("levels");
		}
		Assert.Equal(viaFile.ToJsonString(), viaLevel.ToJsonString(), "level newcomer plays and reports exactly as --bot newcomer-kihon-bot.json");
		Assert.Equal(BotConfig.LoadDefault(ContentPaths.DataDir).Id, Levels["newcomer"].Id, "same id, so the same results file name");
	}

	[Test]
	public static void Determinism_SameSeedSameBytes_DifferentLevelOrSeedDifferentFile()
	{
		var a = HarnessReport.ToJson(RunHarness.PlayBatch(Content, Levels["skilled"], 5, 2));
		var b = HarnessReport.ToJson(RunHarness.PlayBatch(Content, Levels["skilled"], 5, 2));
		Assert.Equal(a, b, "same level, seed and count: same bytes");
		var c = HarnessReport.ToJson(RunHarness.PlayBatch(Content, Levels["intermediate"], 5, 2));
		Assert.True(a != c, "a different level gives a different file");
		var d = HarnessReport.ToJson(RunHarness.PlayBatch(Content, Levels["skilled"], 6, 2));
		Assert.True(a != d, "a different seed gives a different file");
	}

	// Round 2: BotConfig.Parse wraps duplicate keys at any depth; a bot_id of '<id>-mixed' is refused.
	[Test]
	public static void BotConfig_DuplicateKeysAtAnyDepth_AreFormatException()
	{
		string b = BaseText;
		foreach (var (from, to) in new[] { ("\"weights\": {", "\"weights\": {\"walk\": 1, "), ("\"route\": {", "\"route\": {\"rule\": \"random\", "), ("\"fight\": {", "\"fight\": {\"mistake_pct\": 1, "), ("\"run\": {", "\"run\": {\"dojo\": \"skip\", ") })
		{
			Assert.True(b.Contains(from), $"fixture shape: {from}");
			try { BotConfig.Parse(b.Replace(from, to)); }
			catch (FormatException) { continue; }
			catch (Exception e) { throw new Exception($"{from}: expected FormatException, got {e.GetType().Name}"); }
			throw new Exception($"{from}: duplicate accepted");
		}
		foreach (string bad in new[] { "{", "", "[]", "{\"kind\":\"harness_bot\",\"id\":5}" })
		{
			try { BotConfig.Parse(bad); throw new Exception($"'{bad}' accepted"); }
			catch (FormatException) { }
		}
	}

	[Test]
	public static void BotIdMixedName_IsRejected()
	{
		Rejects(File1(Lv("a", "", "t-levels-mixed")), "bot_id '<id>-mixed'");
	}
}
