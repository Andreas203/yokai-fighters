using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using YokaiFighters.Harness;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-170 (designer 2026-10-20; R5, Y4, P6): the test bot's skill levels in <c>data/harness/bot-levels.json</c>. The
/// newcomer level is today's bot exactly (pinned), the loader is strict, and the harness plays per-level and mixed batches
/// with the level recorded per run and summarised per level.
/// </summary>
public static class BotLevelsTests
{
	static HarnessContent? _content;
	static HarnessContent Content => _content ??= HarnessContent.Load(ContentPaths.DataDir, ContentPaths.FixturesDir);
	static BotLevels Levels => BotLevels.LoadDefault(ContentPaths.DataDir);
	static string LevelsJson => File.ReadAllText(Path.Combine(ContentPaths.DataDir, "harness", BotLevels.DefaultFile));
	static string BaseJson(string id) => File.ReadAllText(Path.Combine(ContentPaths.DataDir, "harness", id + ".json"));

	static string[] Hashes(BatchResult b)
	{
		using var doc = JsonDocument.Parse(HarnessReport.ToJson(b));
		return doc.RootElement.GetProperty("runs").EnumerateArray().Select(r => r.GetProperty("session_hash").GetString()!).ToArray();
	}

	[Test]
	public static void Levels_LoadFromData_NewcomerIsTodaysBot()
	{
		var levels = Levels;
		Assert.Equal("newcomer,intermediate,skilled", string.Join(",", levels.LevelNames()), "three levels, in file order");
		var today = BotConfig.LoadDefault(ContentPaths.DataDir);
		var n = levels["newcomer"];
		Assert.Equal(today.Id, n.Id, "newcomer plays as newcomer-kihon-bot (same results file name)");
		Assert.True(today.Fight == n.Fight, "newcomer fight config = the base file's, field for field");
		Assert.True(today.Run.Route == n.Run.Route && today.Run.PreferTypes.SequenceEqual(n.Run.PreferTypes)
			&& today.Run.RewardOrder.SequenceEqual(n.Run.RewardOrder) && today.Run.Merchant == n.Run.Merchant
			&& today.Run.Dojo == n.Run.Dojo && today.Run.HealBelowPct == n.Run.HealBelowPct
			&& today.Run.MenuSecondsPerNode == n.Run.MenuSecondsPerNode, "newcomer run habits = the base file's");
		Assert.Equal("newcomer", n.Level, "the level is carried on the config");

		var skilled = levels["skilled"];
		Assert.True(skilled.Fight.ReactionFrames < levels["intermediate"].Fight.ReactionFrames
			&& levels["intermediate"].Fight.ReactionFrames < n.Fight.ReactionFrames, "reaction frames drop with skill (Y4)");
		Assert.Equal(4, skilled.Fight.JumpWeight, "a weight override applies");
		Assert.Equal(today.Fight.WalkWeight, skilled.Fight.WalkWeight, "weights it does not name stay the base's");
		Assert.Equal(today.Fight.MaxFightTicks, skilled.Fight.MaxFightTicks, "the fight cap is the base's");

		var doc = JsonNode.Parse(LevelsJson)!;
		foreach (var lv in doc["levels"]!.AsArray().Where(l => (string?)l!["level"] != "newcomer"))
			Assert.Equal("proposed", (string?)lv!["status"], $"{lv["level"]}: proposed values for the designer");
	}

	/// <summary>Newcomer = today's bot: the seed-1 batch's first runs play to the same pinned session hashes as the base file.</summary>
	[Test]
	public static void Newcomer_PlaysTodaysBatch_Pinned()
	{
		// YOK-174: all three re-pinned (were a4a30e288fb83033, 28a9873a8981ac73, 9be8a5bed90de1fa): every run opens on
		// r1-apprentice, which now fights with its own apprentice profile (designer 2026-10-21) instead of a Kitsune roll.
		var want = new[] { "d573ca8d44add598", "d0fd95d189fe78ed", "974faac26a455244" };
		var legacy = AiGuardTests.LegacyOpponent(Content); // YOK-165: the opponent as on main before it (no tier jump-punish rate)
		var level = Hashes(RunHarness.PlayBatch(legacy, Levels["newcomer"], 1, 3));
		var file = Hashes(RunHarness.PlayBatch(legacy, BotConfig.LoadDefault(ContentPaths.DataDir), 1, 3));
		Assert.Equal(string.Join(" ", want), string.Join(" ", file), "the base file's batch (pinned)");
		Assert.Equal(string.Join(" ", want), string.Join(" ", level), "the newcomer level's batch is the same runs");

		// YOK-177: with standing-trigger habits on (the apprentice's at_close_range habit rolls), new pins.
		var wantOn = new[] { "03fbce1913c3ede3", "c5a8b3d97602d4e5", "c70a98e03e1d4d23" };
		var on = legacy with { StandingHabits = true };
		Assert.Equal(string.Join(" ", wantOn), string.Join(" ", Hashes(RunHarness.PlayBatch(on, BotConfig.LoadDefault(ContentPaths.DataDir), 1, 3))), "the base file's batch with standing habits (pinned)");
		Assert.Equal(string.Join(" ", wantOn), string.Join(" ", Hashes(RunHarness.PlayBatch(on, Levels["newcomer"], 1, 3))), "the newcomer level's, the same");
	}

	[Test]
	public static void Loader_RejectsBadLevels()
	{
		string good = LevelsJson;
		JsonObject Doc() => JsonNode.Parse(good)!.AsObject();
		JsonObject Lv(JsonObject d, int i) => d["levels"]![i]!.AsObject();
		void Bad(string what, Action<JsonObject> change)
		{
			var d = Doc();
			change(d);
			bool threw = false;
			try { BotLevels.Parse(d.ToJsonString(), BaseJson); }
			catch (FormatException) { threw = true; }
			Assert.True(threw, $"rejected: {what}");
		}
		BotLevels.Parse(good, BaseJson); // the real file is fine
		Bad("wrong kind", d => d["kind"] = "harness_bot");
		Bad("no levels", d => d["levels"] = new JsonArray());
		Bad("unknown fight key", d => Lv(d, 1)["fight"]!["reaction_frame"] = 10);
		Bad("max_fight_ticks is fixed", d => Lv(d, 1)["fight"]!["max_fight_ticks"] = 600);
		Bad("menu_seconds_per_node is fixed", d => Lv(d, 1)["run"] = new JsonObject { ["menu_seconds_per_node"] = 10 });
		Bad("unknown weight", d => Lv(d, 2)["fight"]!["weights"]!["dash"] = 1);
		Bad("unknown level key", d => Lv(d, 1)["speed"] = 1);
		Bad("block_pct 101", d => Lv(d, 1)["fight"]!["block_pct"] = 101);
		Bad("reaction_frames 0", d => Lv(d, 1)["fight"]!["reaction_frames"] = 0);
		Bad("idle_guard_pct -1", d => Lv(d, 1)["fight"]!["idle_guard_pct"] = -1);
		Bad("a string for a number", d => Lv(d, 1)["fight"]!["mistake_pct"] = "8");
		Bad("a null value", d => Lv(d, 1)["fight"]!["mistake_pct"] = null);
		Bad("every weight 0", d => Lv(d, 1)["fight"]!["weights"] = new JsonObject
			{ ["walk"] = 0, ["normal"] = 0, ["special"] = 0, ["throw"] = 0, ["jump"] = 0, ["wait"] = 0 });
		Bad("preferred_min above max", d => Lv(d, 1)["fight"]!["preferred_min"] = 4000);
		Bad("bad merchant", d => Lv(d, 1)["run"] = new JsonObject { ["merchant"] = "steal" });
		Bad("level listed twice", d => Lv(d, 2)["level"] = "intermediate");
		Bad("bot_id used twice", d => Lv(d, 2)["bot_id"] = "intermediate-kihon-bot");
		Bad("level named mixed", d => Lv(d, 2)["level"] = "mixed");
		Bad("level named all", d => Lv(d, 2)["level"] = "all");
		Bad("status missing", d => Lv(d, 1).Remove("status"));
		Bad("status unknown", d => Lv(d, 1)["status"] = "maybe");
		Bad("level name with a trailing newline (reserved 'mixed')", d => Lv(d, 2)["level"] = "mixed\n");
		Bad("level name with a trailing newline", d => Lv(d, 2)["level"] = "expert\n");
		Bad("bot_id with a trailing newline", d => Lv(d, 2)["bot_id"] = "x\n");
		Bad("base is a number", d => d["base"] = 5);
		Bad("kind is a number", d => d["kind"] = 5);
		Bad("id is an object", d => d["id"] = new JsonObject());
		Bad("level name is a number", d => Lv(d, 1)["level"] = 2);
		Bad("bot_id is an array", d => Lv(d, 1)["bot_id"] = new JsonArray());
		Bad("status is a bool", d => Lv(d, 1)["status"] = true);
		Bad("levels is an object", d => d["levels"] = new JsonObject());
		Bad("bot_id is the mixed batch's name", d => Lv(d, 2)["bot_id"] = "bot-levels-mixed");
		Bad("base file missing", d => d["base"] = "intermediate-kihon-bot");
		Bad("base is a path", d => d["base"] = "../harness/newcomer-kihon-bot");
	}

	/// <summary>Test Breaker (YOK-170 round 1): a key written twice, in the levels file or in the base bot file, is a
	/// FormatException like every other bad file (System.Text.Json itself throws ArgumentException).</summary>
	[Test]
	public static void DuplicateKeys_AreAFormatException_InLevelsAndBotFiles()
	{
		static bool Format(Action parse)
		{
			try { parse(); }
			catch (FormatException) { return true; }
			return false;
		}
		string levels = LevelsJson, bot = BaseJson(BotConfig.DefaultId);
		Assert.True(Format(() => BotLevels.Parse(levels.Replace("\"id\": \"bot-levels\",", "\"id\": \"bot-levels\", \"id\": \"x\","), BaseJson)), "levels: top-level key twice");
		Assert.True(Format(() => BotLevels.Parse(levels.Replace("\"reaction_frames\": 18,", "\"reaction_frames\": 18, \"reaction_frames\": 20,"), BaseJson)), "levels: fight key twice");
		Assert.True(Format(() => BotLevels.Parse(levels.Replace("\"level\": \"skilled\",", "\"level\": \"skilled\", \"level\": \"expert\","), BaseJson)), "levels: level key twice");
		string dupBot = bot.Replace("\"block_pct\": 50,", "\"block_pct\": 50, \"block_pct\": 60,");
		Assert.True(dupBot != bot, "setup: the base file names block_pct");
		Assert.True(Format(() => BotConfig.Parse(dupBot)), "bot file: key twice");
		Assert.True(Format(() => BotLevels.Parse(levels, _ => dupBot)), "levels over a base file with a key twice");
		Assert.True(Format(() => BotConfig.Parse(bot.Replace("\"block_pct\": 50", "\"block_pct\": \"50\""))), "bot file: a string for a number");
		Assert.True(Format(() => BotConfig.Parse("{ not json")), "bot file: not JSON");
	}

	/// <summary>A mixed batch plays run i as level i mod 3, each the very run that level's own batch plays from that seed.</summary>
	[Test]
	public static void MixedBatch_RoundRobin_SameRunsAsPerLevel_LevelRecordedAndSummarised()
	{
		var levels = Levels;
		var mixed = RunHarness.PlayMixed(Content, levels, 1, 6);
		Assert.Equal("bot-levels-mixed", mixed.Name, "the mixed batch's name");
		Assert.Equal("bot-levels-mixed-n6-s1.json", HarnessReport.FileName(mixed), "its results file");
		var per = levels.Levels.Select(l => Hashes(RunHarness.PlayBatch(Content, l, 1, 6))).ToArray();
		var got = Hashes(mixed);
		for (int i = 0; i < 6; i++)
		{
			Assert.Equal(levels.Levels[i % 3].Level, mixed.Runs[i].Level, $"run {i}: level");
			Assert.Equal(per[i % 3][i], got[i], $"run {i}: same run as the level's own batch");
		}
		Assert.True(per[0].Zip(per[2]).Any(p => p.First != p.Second), "skilled plays differently from newcomer");

		string json = HarnessReport.ToJson(mixed);
		Assert.Equal(json, HarnessReport.ToJson(RunHarness.PlayMixed(Content, levels, 1, 6)), "deterministic: same bytes");
		using var doc = JsonDocument.Parse(json);
		var root = doc.RootElement;
		Assert.Equal("bot-levels-mixed", root.GetProperty("bot").GetString(), "bot = the batch name");
		var runs = root.GetProperty("runs");
		for (int i = 0; i < 6; i++) Assert.Equal(levels.Levels[i % 3].Level, runs[i].GetProperty("level").GetString(), $"run {i}: level in the file");
		var by = root.GetProperty("summary").GetProperty("levels");
		Assert.Equal("newcomer,intermediate,skilled", string.Join(",", by.EnumerateObject().Select(p => p.Name)), "one summary per level");
		foreach (var lv in by.EnumerateObject())
		{
			var s = lv.Value;
			Assert.Equal(2, s.GetProperty("runs").GetInt32(), $"{lv.Name}: 2 of the 6 runs");
			foreach (string k in new[] { "reach_tanuki_pct", "beat_tanuki_pct_of_reached", "run_win_pct", "duel_win_pct", "exchange_ratio", "hit_exchange_ratio" })
				Assert.True(s.TryGetProperty(k, out _), $"{lv.Name}: {k}");
			long dealt = s.GetProperty("damage_dealt").GetInt64(), taken = s.GetProperty("damage_taken").GetInt64();
			Assert.True(taken > 0 && Math.Abs(s.GetProperty("exchange_ratio").GetDouble() - Math.Round((double)dealt / taken, 2)) < 1e-9,
				$"{lv.Name}: exchange ratio = damage dealt / taken");
		}
	}

	[Test]
	public static void SingleBot_LevelIsRecorded_FileAloneHasNone()
	{
		var batch = RunHarness.PlayBatch(Content, Levels["intermediate"], 1, 1);
		Assert.Equal("intermediate-kihon-bot-n1-s1.json", HarnessReport.FileName(batch), "per-level file name = its bot id");
		using var doc = JsonDocument.Parse(HarnessReport.ToJson(batch));
		Assert.Equal("intermediate", doc.RootElement.GetProperty("runs")[0].GetProperty("level").GetString(), "level per run");
		Assert.True(doc.RootElement.GetProperty("summary").GetProperty("levels").TryGetProperty("intermediate", out _), "summary per level");

		using var alone = JsonDocument.Parse(HarnessReport.ToJson(RunHarness.PlayBatch(Content, BotConfig.LoadDefault(ContentPaths.DataDir), 1, 1)));
		Assert.Equal(JsonValueKind.Null, alone.RootElement.GetProperty("runs")[0].GetProperty("level").ValueKind, "a bot file on its own has no level");
		Assert.True(alone.RootElement.GetProperty("summary").GetProperty("levels").TryGetProperty("none", out _), "summarised as 'none'");
	}
}
