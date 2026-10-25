using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YokaiFighters.Ai;
using YokaiFighters.Harness;
using YokaiFighters.Replay;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>Test Breaker probes for YOK-174 (per-yokai profiles, seeded temperament roll, Kit.LoadAs).</summary>
public static class PerYokaiProfileProbes
{
	static KitSources Sources => KitSources.At(ContentPaths.DataDir, ContentPaths.FixturesDir);
	static HarnessContent? _content;
	static HarnessContent Content => _content ??= HarnessContent.Load(ContentPaths.DataDir, ContentPaths.FixturesDir);
	static string MapDir => ContentPaths.Data("map");
	static NodeContext Resolve(string yokai, string type = "duel", bool elder = false) =>
		NodeSceneTable.Default(MapDir).Resolve(new MapNodeData("n", 2, type, yokai, elder, yokai)).Context;

	// Y1/Y2 (designer 2026-10-20): 50/50 per node. Edge seeds and look-alike node ids still roll validly and balanced.
	[Test]
	public static void Roll_EdgeSeedsAndLookAlikeIds_StayBalancedAndPure()
	{
		foreach (uint seed in new[] { 0u, 1u, uint.MaxValue, 0x80000000u })
		{
			string t = NodeTemperament.Roll(seed, "r2-oni");
			Assert.True(t is NodeTemperament.Aggressive or NodeTemperament.Patient, $"seed {seed}: {t}");
			Assert.Equal(t, NodeTemperament.Roll(seed, "r2-oni"), $"seed {seed} pure");
		}
		Assert.True(NodeTemperament.Roll(1, "") is NodeTemperament.Aggressive or NodeTemperament.Patient, "empty node id still rolls");
		int agg = Enumerable.Range(0, 1000).Count(i => NodeTemperament.Roll(7, "r" + i) == NodeTemperament.Aggressive);
		Assert.True(agg is > 430 and < 570, $"1000 sequential ids, one seed: aggressive {agg}");
		agg = Enumerable.Range(0, 1000).Count(i => NodeTemperament.Roll((uint)i, "r4-kappa") == NodeTemperament.Aggressive);
		Assert.True(agg is > 430 and < 570, $"1000 consecutive seeds: aggressive {agg}");
	}

	// Y1/Y2: nodes of one run are independent: two nodes agree about half the time; adjacent seeds are not a copy.
	[Test]
	public static void Roll_TwoNodes_AgreeAboutHalfTheTime_NotCorrelatedBySeed()
	{
		int agree = 0;
		for (uint s = 1; s <= 1000; s++)
			if (NodeTemperament.Roll(s, "r2-oni") == NodeTemperament.Roll(s, "r3-kappa")) agree++;
		Assert.True(agree is > 430 and < 570, $"two nodes agree {agree}/1000");
		int same = 0;
		for (uint s = 1; s <= 1000; s++)
			if (NodeTemperament.Roll(s, "r2-oni") == NodeTemperament.Roll(s + 1, "r2-oni")) same++;
		Assert.True(same is > 430 and < 570, $"adjacent seeds agree {same}/1000");
	}

	// Named profile ids are exact: never a path, never case-folded or trimmed; none falls back.
	[Test]
	public static void NamedProfile_PathLikeCaseAndOddNames_AreRefused()
	{
		foreach (string bad in new[] { "../profiles/apprentice", "profiles/apprentice.json", "apprentice.json", "Apprentice", "APPRENTICE", " apprentice", "apprentice ", "kitsune-aggressive\n", "res://x" })
		{
			string? msg = null;
			try { Content.ProfilesForNode(Resolve("apprentice") with { Profile = bad }); } catch (InvalidOperationException e) { msg = e.Message; }
			Assert.True(msg is not null, $"'{bad}' is refused, never matched loosely");
			Assert.Equal(-1, NodeTemperament.Pick(Content.Profiles("kitsune"), Resolve("kitsune") with { Profile = bad }, 3), $"'{bad}' picks nothing");
		}
	}

	// Kit.LoadAs: no Kitsune special on any borrowed body, one throw, no duplicate moves, deterministic.
	[Test]
	public static void LoadAs_BorrowedBodies_CarryNoKitsuneSpecial_AndAreDeterministic()
	{
		foreach (string y in new[] { "oni", "kappa", "nobody" })
		{
			var a = Kit.LoadAs(y, "kitsune", Sources);
			var b = Kit.LoadAs(y, "kitsune", Sources);
			Assert.True(a.Moves.Select(x => x.Id).SequenceEqual(b.Moves.Select(x => x.Id)), y + ": same moves, same order, twice");
			Assert.True(a.Specials.Select(s => s.Id).SequenceEqual(b.Specials.Select(s => s.Id)), y + ": same specials twice");
			Assert.True(!a.Specials.Any(s => s.Source == "kitsune" || s.Id.Contains("foxfire")), y + ": no Kitsune special: " + string.Join(",", a.Specials.Select(s => s.Id)));
			Assert.Equal(1, a.Moves.Count(x => x.IsThrow), y + ": exactly one throw");
			Assert.True(a.Moves.Select(x => x.Id).Distinct().Count() == a.Moves.Length, y + ": no move twice");
		}
		var k = Kit.LoadAs("kappa", "kitsune", Sources).Body;
		Assert.Equal(Kit.Load("kitsune", Sources).Body.PushWidth, k.PushWidth, "kappa push width = the body's");
		Assert.True(Kit.Load("kitsune", Sources).Specials.Any(s => s.Id.Contains("foxfire")), "the Kitsune keeps Foxfire");
	}

	// Replays: a fight on a borrowed body records and replays exactly.
	[Test]
	public static void BorrowedBodyFight_ReplaysExactly()
	{
		var catalog = ReplayCatalog.FromRepo(Path.GetFullPath(Godot.ProjectSettings.GlobalizePath("res://") + ".."));
		foreach (string y in new[] { "oni", "kappa" })
		{
			var ryo = Kit.Load("kitsune", Sources);
			var foe = Kit.LoadAs(y, "kitsune", Sources);
			var m = new Match(null, ryo.Moves, foe.Moves);
			ryo.Equip(m.P1);
			foe.Equip(m.P2);
			var rng = new Random(174);
			var rec = ReplayRecorder.Begin(m, null, 0, null);
			var buttons = new[] { InputBits.LightPunch, InputBits.MediumKick, InputBits.HeavyPunch };
			FighterInput Next()
			{
				var bits = (InputBits)0;
				int d = rng.Next(5);
				if (d == 0) bits |= InputBits.Right; else if (d == 1) bits |= InputBits.Left;
				if (rng.Next(3) == 0) bits |= buttons[rng.Next(buttons.Length)];
				return new FighterInput(bits);
			}
			for (int t = 0; t < 2000 && m.Phase != MatchPhase.Over; t++)
			{
				var a = Next(); var b = Next();
				m.Step(a, b);
				rec.Record(a, b, m.StateHash());
			}
			var done = rec.Finish(m);
			var r = Replayer.Run(ReplayRecording.FromBytes(done.ToBytes()), catalog);
			Assert.True(r.Ok, $"{y} on the Kitsune body replays: {r}");
		}
	}

	// Determinism: run i is the same run however the batch is cut.
	[Test]
	public static void Batch_RunDoesNotDependOnTheBatchItWasPlayedIn()
	{
		var bot = BotConfig.LoadDefault(ContentPaths.DataDir);
		var big = RunHarness.PlayBatch(Content, bot, 10, 6);
		var small = RunHarness.PlayBatch(Content, bot, 13, 3);
		for (int i = 0; i < 3; i++)
		{
			var x = big.Runs[3 + i]; var y = small.Runs[i];
			Assert.Equal(x.Seed, y.Seed, "same seed");
			Assert.Equal(string.Join(">", x.Nodes.Select(n => n.Id)), string.Join(">", y.Nodes.Select(n => n.Id)), "same route");
			Assert.Equal(string.Join(",", x.Nodes.Select(n => n.Fight?.Profile + "/" + n.Fight?.MatchHash)), string.Join(",", y.Nodes.Select(n => n.Fight?.Profile + "/" + n.Fight?.MatchHash)), "same profiles and fight hashes");
		}
	}

	// fight_mix: tier order apprentice, yokai, elder, tanuki; won <= fights; one row per key.
	[Test]
	public static void FightMix_OrderedByTier_WonNeverExceedsFights()
	{
		var batch = RunHarness.PlayBatch(Content, BotConfig.LoadDefault(ContentPaths.DataDir), 1, 25);
		var mix = System.Text.Json.JsonDocument.Parse(HarnessReport.ToJson(batch)).RootElement.GetProperty("summary").GetProperty("fight_mix").EnumerateArray().ToList();
		string[] order = { "apprentice", "yokai", "elder", "tanuki" };
		int last = -1;
		foreach (var e in mix)
		{
			int o = Array.IndexOf(order, e.GetProperty("tier").GetString());
			Assert.True(o >= 0 && o >= last, "tier order: " + e.GetProperty("tier").GetString());
			last = o;
			Assert.True(e.GetProperty("won").GetInt32() <= e.GetProperty("fights").GetInt32() && e.GetProperty("fights").GetInt32() > 0, "won <= fights, fights > 0");
		}
		var keys = mix.Select(e => e.GetProperty("tier").GetString() + "|" + e.GetProperty("yokai").GetString() + "|" + e.GetProperty("plays_as").GetString() + "|" + e.GetProperty("profile").GetString() + "|" + e.GetProperty("fighter").GetString()).ToList();
		Assert.Equal(keys.Count, keys.Distinct().Count(), "one row per key");
		Assert.True(!mix.Any(e => e.GetProperty("yokai").GetString() == "kitsune" && e.GetProperty("profile").GetString() == "apprentice"), "the apprentice profile is only on the apprentice node");
	}
}
