using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using YokaiFighters.Sim;

namespace YokaiFighters.Harness;

/// <summary>
/// YOK-26: the batch results file the Sparring Partner reads (<c>harness/results/whole-run/</c>), kind
/// <c>harness_results</c>, schema version 1 (YOK-157 log blocks are additive): the bot config id, a summary next to the design targets (R4, R5, R9,
/// Y4) and every run in full. No clock, no machine names, fixed key order and number format, so the same seed and
/// data write the same bytes.
/// </summary>
public static class HarnessReport
{
	public const string Kind = "harness_results";
	public const int SchemaVersion = 1;

	/// <summary>R5: share of runs reaching the Tanuki, and of those, share beating it (percent).</summary>
	public static readonly (int Lo, int Hi) ReachTarget = (50, 70), BeatTarget = (40, 60);
	/// <summary>R9: first-run length in minutes.</summary>
	public static readonly (int Lo, int Hi) RunMinutesTarget = (25, 40);
	/// <summary>Y4: damage the opponent deals a newcomer per duel, by tier.</summary>
	public static readonly IReadOnlyDictionary<string, int> NewcomerDamageTarget = new Dictionary<string, int>
	{ ["apprentice"] = 110, ["yokai"] = 220, ["elder"] = 280, ["tanuki"] = 350 };
	/// <summary>R4: health on arriving at the Tanuki for the newcomer's heal@4 + rest@7 line (the bot's), and heal@4 + train@7.</summary>
	public const int ArrivalHealRestTarget = 520, ArrivalHealTrainTarget = 370;

	public static string FileName(BatchResult b) => $"{b.Name}-n{b.Runs.Count}-s{b.BaseSeed}.json";

	public static string Write(BatchResult b, string dir)
	{
		Directory.CreateDirectory(dir);
		string path = Path.Combine(dir, FileName(b));
		File.WriteAllText(path, ToJson(b));
		return path;
	}

	public static string ToJson(BatchResult b)
	{
		using var ms = new MemoryStream();
		using (var w = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
		{
			w.WriteStartObject();
			w.WriteString("kind", Kind);
			w.WriteNumber("schema_version", SchemaVersion);
			w.WriteString("bot", b.Name);
			w.WriteNumber("base_seed", b.BaseSeed);
			w.WriteNumber("runs_played", b.Runs.Count);
			w.WriteString("caveats", Caveats);
			w.WritePropertyName("summary");
			WriteSummary(w, b);
			w.WriteStartArray("runs");
			foreach (var r in b.Runs) WriteRun(w, r);
			w.WriteEndArray();
			w.WriteEndObject();
		}
		return Encoding.UTF8.GetString(ms.ToArray()).Replace("\r\n", "\n") + "\n";
	}

	public const string Caveats =
		"Not representative yet: every yokai except the Kitsune and the Tanuki fights on the Kitsune's kit (node-scenes.json fallback_fighter), " +
		"but with its own behaviour profiles and its own move files where data/moves has them (YOK-174: the Oni's normals; the Kappa has none, so it fights with its profiles' generic actions (walk, dash, block, throw) on the Kitsune's normals and throw, and neither has a special);" +
		"temperament never varies (each node uses its yokai's first profile, aggressive, unless node-scenes.json names one; see fight_mix); the apprentice's half damage (Y4, YOK-156) is applied; the bot never takes the dojo trial (YOK-98) and shops " +
		"only when its config says so (the default newcomer heals at the merchant and rests at the dojo, R4's line), and fight time excludes menus (run length adds menu_seconds_per_node per node).";

	/// <summary>The summary block alone (also printed by the command-line entry).</summary>
	public static string SummaryJson(BatchResult b)
	{
		using var ms = new MemoryStream();
		using (var w = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true }))
			WriteSummary(w, b);
		return Encoding.UTF8.GetString(ms.ToArray()).Replace("\r\n", "\n");
	}

	static void WriteSummary(Utf8JsonWriter w, BatchResult b)
	{
		var runs = b.Runs;
		int n = runs.Count, reached = runs.Count(r => r.ReachedTanuki), beat = runs.Count(r => r.BeatTanuki);
		w.WriteStartObject();
		w.WriteNumber("runs", n);
		w.WriteNumber("reached_tanuki", reached);
		w.WriteNumber("beat_tanuki", beat);
		Pct(w, "reach_tanuki_pct", reached, n);
		Pct(w, "beat_tanuki_pct_of_reached", beat, reached);
		Range(w, "R5_reach_target_pct", ReachTarget);
		Range(w, "R5_beat_target_pct", BeatTarget);

		w.WriteStartObject("runs_ended_by_row");
		foreach (var g in runs.Where(r => !r.Won).GroupBy(r => r.RowReached).OrderBy(g => g.Key)) w.WriteNumber(g.Key.ToString(), g.Count());
		w.WriteEndObject();
		w.WriteNumber("fights_timed_out", runs.Sum(r => r.Nodes.Count(x => x.Fight is { TimedOut: true })));

		// R4: arrival health at rows 4 and 7 and at the Tanuki, the boss split by what the row-7 stop did.
		w.WriteStartObject("arrival_health");
		foreach (int row in new[] { 4, 7 }) Stats(w, $"row_{row}", runs.SelectMany(r => r.Nodes).Where(x => x.Row == row).Select(x => x.ArrivalHealth));
		Stats(w, "tanuki", runs.Select(r => r.Boss).Where(x => x is not null).Select(x => x!.ArrivalHealth));
		foreach (string act in new[] { "heal", "rest" })
			Stats(w, $"tanuki_after_row7_{act}", runs.Where(r => r.Boss is not null && r.Nodes.Exists(x => x.Row == 7 && x.Action == act)).Select(r => r.Boss!.ArrivalHealth));
		w.WriteNumber("R4_target_heal4_rest7", ArrivalHealRestTarget);
		w.WriteNumber("R4_target_heal4_train7", ArrivalHealTrainTarget);
		w.WriteEndObject();

		// Y4: damage taken per duel by tier (newcomer column), and dealt.
		w.WriteStartObject("damage_per_duel");
		foreach (string tier in new[] { "apprentice", "yokai", "elder", "tanuki" })
		{
			var fights = runs.SelectMany(r => r.Nodes).Where(x => x.Tier == tier && x.Fight is not null).Select(x => x.Fight!).ToList();
			w.WriteStartObject(tier);
			w.WriteNumber("duels", fights.Count);
			w.WriteNumber("won", fights.Count(f => f.Won));
			Stats(w, "taken", fights.Select(f => f.DamageTaken));
			Stats(w, "dealt", fights.Select(f => f.DamageDealt));
			Stats(w, "ticks", fights.Select(f => f.Ticks));
			w.WriteNumber("Y4_newcomer_target_taken", NewcomerDamageTarget[tier]);
			w.WriteEndObject();
		}
		w.WriteEndObject();

		// R9: run length.
		w.WriteStartObject("run_length");
		Stats(w, "fight_ticks", runs.Select(r => r.FightTicks));
		Stats(w, "fight_minutes", runs.Select(r => r.FightTicks / 3600.0));
		Stats(w, "estimated_minutes", runs.Select(r => r.EstimatedMinutes));
		Stats(w, "estimated_minutes_runs_reaching_tanuki", runs.Where(r => r.ReachedTanuki).Select(r => r.EstimatedMinutes));
		w.WriteNumber("menu_seconds_per_node", b.Bot.Run.MenuSecondsPerNode);
		Range(w, "R9_target_minutes", RunMinutesTarget);
		w.WriteEndObject();

		w.WriteStartObject("copy_locked");
		foreach (var g in runs.Where(r => r.ReachedTanuki).GroupBy(r => r.CopyLocked is { } c ? $"{c.Data.Id} Lv{c.Level} slot {c.Slot}" : "none").OrderBy(g => g.Key, StringComparer.Ordinal))
			w.WriteNumber(g.Key, g.Count());
		w.WriteEndObject();
		w.WriteStartObject("cards_picked");
		foreach (var g in runs.SelectMany(r => r.Nodes).Where(x => x.Card is not null).GroupBy(x => $"{x.Card!.Badge} {x.Card.AbilityId}").OrderBy(g => g.Key, StringComparer.Ordinal))
			w.WriteNumber(g.Key, g.Count());
		w.WriteEndObject();
		w.WriteStartObject("routes");
		foreach (var g in runs.GroupBy(r => string.Join(">", r.Nodes.Select(x => x.Id))).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).Take(10))
			w.WriteNumber(g.Key, g.Count());
		w.WriteEndObject();
		WriteFightMix(w, runs);
		FightLogReport.WriteSummary(w, runs.SelectMany(r => r.Nodes).Where(x => x.Fight is not null).Select(x => x.Fight!)); // YOK-157: P5, P6/Y2
		WriteLevels(w, runs);
		w.WriteEndObject();
	}

	/// <summary>
	/// YOK-170: per bot level (in the order the levels first appear; "none" for a bot file played on its own): reach (R5),
	/// win rates (runs won, Tanuki beaten of reached, duels won) and the exchange ratio, damage dealt / damage taken over
	/// every duel; with the hit log on, also Ryo's unblocked hits / the opponent's.
	/// </summary>
	static void WriteLevels(Utf8JsonWriter w, List<RunRecord> runs)
	{
		w.WriteStartObject("levels");
		foreach (var g in runs.GroupBy(r => r.Level ?? "none"))
		{
			var lr = g.ToList();
			int n = lr.Count, reached = lr.Count(r => r.ReachedTanuki), beat = lr.Count(r => r.BeatTanuki), won = lr.Count(r => r.Won);
			var duels = lr.SelectMany(r => r.Nodes).Where(x => x.Fight is not null).Select(x => x.Fight!).ToList();
			long dealt = duels.Sum(f => (long)f.DamageDealt), taken = duels.Sum(f => (long)f.DamageTaken);
			w.WriteStartObject(g.Key);
			w.WriteNumber("runs", n);
			w.WriteNumber("reached_tanuki", reached);
			Pct(w, "reach_tanuki_pct", reached, n);
			w.WriteNumber("beat_tanuki", beat);
			Pct(w, "beat_tanuki_pct_of_reached", beat, reached);
			Pct(w, "run_win_pct", won, n);
			w.WriteNumber("duels", duels.Count);
			w.WriteNumber("duels_won", duels.Count(f => f.Won));
			Pct(w, "duel_win_pct", duels.Count(f => f.Won), duels.Count);
			w.WriteNumber("damage_dealt", dealt);
			w.WriteNumber("damage_taken", taken);
			Ratio(w, "exchange_ratio", dealt, taken);
			if (duels.Count > 0 && duels.All(f => f.Log is not null))
			{
				long Clean(int side) => duels.Sum(f => (long)f.Log!.Moves[side].Values.Sum(t => t.Hits - t.Blocked));
				Ratio(w, "hit_exchange_ratio", Clean(0), Clean(1));
			}
			w.WriteEndObject();
		}
		w.WriteEndObject();
	}

	static void Ratio(Utf8JsonWriter w, string name, long num, long den)
	{
		if (den == 0) w.WriteNull(name);
		else w.WriteNumber(name, Math.Round((double)num / den, 2, MidpointRounding.AwayFromZero));
	}

	static void WriteRun(Utf8JsonWriter w, RunRecord r)
	{
		w.WriteStartObject();
		w.WriteNumber("seed", r.Seed);
		if (r.Level is null) w.WriteNull("level"); else w.WriteString("level", r.Level); // YOK-170
		w.WriteString("outcome", r.Won ? "won" : "lost");
		w.WriteNumber("row_reached", r.RowReached);
		w.WriteString("end_node", r.EndNode);
		w.WriteBoolean("reached_tanuki", r.ReachedTanuki);
		w.WriteBoolean("beat_tanuki", r.BeatTanuki);
		w.WriteStartArray("route");
		foreach (var x in r.Nodes) w.WriteStringValue(x.Id);
		w.WriteEndArray();
		w.WriteNumber("final_health", r.FinalHealth);
		w.WriteNumber("coins", r.Coins);
		w.WriteNumber("fight_ticks", r.FightTicks);
		w.WriteNumber("estimated_minutes", Round(r.EstimatedMinutes));
		if (r.CopyLocked is { } c)
		{
			w.WriteStartObject("copy_locked");
			w.WriteString("special", c.Data.Id);
			w.WriteString("slot", c.Slot.ToString());
			w.WriteNumber("level", c.Level);
			w.WriteEndObject();
		}
		else w.WriteNull("copy_locked");
		w.WriteStartArray("cards_picked");
		foreach (var x in r.Nodes) if (x.Card is { } card) w.WriteStringValue($"{card.Badge} {card.AbilityId} -> {card.Slot} Lv{card.Level}");
		w.WriteEndArray();
		w.WriteString("session_hash", r.SessionHash.ToString("x16"));
		w.WriteStartArray("nodes");
		foreach (var x in r.Nodes) WriteNode(w, x);
		w.WriteEndArray();
		w.WriteEndObject();
	}

	/// <summary>
	/// YOK-174: who the batch actually fought, so coverage is visible: one row per node tier × yokai (as the map names
	/// it) × plays-as yokai × profile × fighter body, with fights and wins, in tier order then by name.
	/// </summary>
	static void WriteFightMix(Utf8JsonWriter w, List<RunRecord> runs)
	{
		string[] tiers = { "apprentice", "yokai", "elder", "tanuki" };
		int TierOrder(string t) => Array.IndexOf(tiers, t) is var i and >= 0 ? i : tiers.Length;
		w.WriteStartArray("fight_mix");
		foreach (var g in runs.SelectMany(r => r.Nodes).Where(x => x.Fight is not null)
			.GroupBy(x => (x.Tier, Yokai: x.Yokai ?? "", x.PlaysAs, x.Fight!.Profile, x.Fighter))
			.OrderBy(g => TierOrder(g.Key.Tier)).ThenBy(g => g.Key.Tier, StringComparer.Ordinal)
			.ThenBy(g => g.Key.Yokai, StringComparer.Ordinal).ThenBy(g => g.Key.Profile, StringComparer.Ordinal).ThenBy(g => g.Key.Fighter, StringComparer.Ordinal))
		{
			w.WriteStartObject();
			w.WriteString("tier", g.Key.Tier);
			w.WriteString("yokai", g.Key.Yokai);
			w.WriteString("plays_as", g.Key.PlaysAs);
			w.WriteString("profile", g.Key.Profile);
			w.WriteString("fighter", g.Key.Fighter);
			w.WriteNumber("fights", g.Count());
			w.WriteNumber("won", g.Count(x => x.Fight!.Won));
			w.WriteEndObject();
		}
		w.WriteEndArray();
	}

	static void WriteNode(Utf8JsonWriter w, NodeRecord x)
	{
		w.WriteStartObject();
		w.WriteString("id", x.Id);
		w.WriteNumber("row", x.Row);
		w.WriteString("type", x.Type);
		if (x.Yokai is null) w.WriteNull("yokai"); else w.WriteString("yokai", x.Yokai);
		w.WriteString("fighter", x.Fighter);
		if (x.PlaysAs.Length > 0) w.WriteString("plays_as", x.PlaysAs); // YOK-174
		w.WriteString("tier", x.Tier);
		w.WriteBoolean("stand_in", x.StandIn);
		w.WriteString("action", x.Action);
		if (x.Bought.Count > 0)
		{
			w.WriteStartArray("bought");
			foreach (var id in x.Bought) w.WriteStringValue(id);
			w.WriteEndArray();
		}
		w.WriteNumber("arrival_health", x.ArrivalHealth);
		w.WriteNumber("arrival_coins", x.ArrivalCoins);
		w.WriteNumber("leave_health", x.LeaveHealth);
		w.WriteNumber("coins_won", x.CoinsWon);
		if (x.Fight is { } f)
		{
			w.WriteStartObject("fight");
			w.WriteString("result", f.TimedOut ? "timeout" : f.Won ? "won" : "lost");
			w.WriteString("profile", f.Profile);
			w.WriteNumber("reaction_frames", f.ReactionFrames);
			w.WriteNumber("ticks", f.Ticks);
			w.WriteNumber("damage_taken", f.DamageTaken);
			w.WriteNumber("damage_dealt", f.DamageDealt);
			w.WriteNumber("health_left", f.HealthLeft);
			if (f.OpponentCopiedSlot != SpecialSlot.None) w.WriteString("tanuki_copied_slot", f.OpponentCopiedSlot.ToString());
			w.WriteStartObject("bot");
			w.WriteNumber("walks", f.Bot.Walks);
			w.WriteNumber("normals", f.Bot.Normals);
			w.WriteNumber("specials", f.Bot.Specials);
			w.WriteNumber("throws", f.Bot.Throws);
			w.WriteNumber("jumps", f.Bot.Jumps);
			w.WriteNumber("blocks", f.Bot.Blocks);
			w.WriteNumber("idle_guards", f.Bot.IdleGuards);
			w.WriteNumber("guarded_jumps_let_go", f.Bot.GuardedJumpsLetGo); // YOK-157
			w.WriteNumber("anti_airs", f.Bot.AntiAirs);
			w.WriteNumber("mistakes", f.Bot.Mistakes);
			w.WriteEndObject();
			w.WriteString("match_hash", f.MatchHash.ToString("x16"));
			FightLogReport.WriteFight(w, f.Log); // YOK-157
			w.WriteEndObject();
		}
		if (x.Card is { } c)
		{
			w.WriteStartObject("card");
			w.WriteString("badge", c.Badge);
			w.WriteString("ability", c.AbilityId);
			w.WriteString("slot", c.Slot.ToString());
			w.WriteNumber("level", c.Level);
			w.WriteStartArray("offered");
			foreach (string o in c.Offered) w.WriteStringValue(o);
			w.WriteEndArray();
			w.WriteEndObject();
		}
		w.WriteEndObject();
	}

	static double Round(double v) => Math.Round(v, 1, MidpointRounding.AwayFromZero);

	static void Pct(Utf8JsonWriter w, string name, int part, int whole)
	{
		if (whole == 0) w.WriteNull(name);
		else w.WriteNumber(name, Round(100.0 * part / whole));
	}

	static void Range(Utf8JsonWriter w, string name, (int Lo, int Hi) r)
	{
		w.WriteStartArray(name);
		w.WriteNumberValue(r.Lo);
		w.WriteNumberValue(r.Hi);
		w.WriteEndArray();
	}

	static void Stats(Utf8JsonWriter w, string name, IEnumerable<int> values) => Stats(w, name, values.Select(v => (double)v));

	/// <summary>count, mean, median (lower middle), min, max; all null when there are no values.</summary>
	static void Stats(Utf8JsonWriter w, string name, IEnumerable<double> values)
	{
		var v = values.OrderBy(x => x).ToList();
		w.WriteStartObject(name);
		w.WriteNumber("count", v.Count);
		if (v.Count == 0)
		{
			foreach (string k in new[] { "mean", "median", "min", "max" }) w.WriteNull(k);
		}
		else
		{
			w.WriteNumber("mean", Round(v.Sum() / v.Count));
			w.WriteNumber("median", Round(v[(v.Count - 1) / 2]));
			w.WriteNumber("min", Round(v[0]));
			w.WriteNumber("max", Round(v[^1]));
		}
		w.WriteEndObject();
	}
}
