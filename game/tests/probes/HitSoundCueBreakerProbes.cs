using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Godot;
using YokaiFighters.Audio;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-93 Test Breaker probes (V6: every hit layers a thud and a short sting, timed to the impact frame).
/// Expected values come from data/schema/sound-cue.schema.json and rules.md V6, not from the code. The Python side of
/// the shared case list is tools/tests/test_sound_cue_breaker_probes.py.
/// </summary>
public static class HitSoundCueBreakerProbes
{
	const string H = "res://sound/hits/hit_0";

	static HitCue Cue(string move = "test-jab", string source = "ryo", string? special = null, int level = 1,
		HitStrength strength = HitStrength.Light, HitSparkKind kind = HitSparkKind.Hit, int tick = 100, int attacker = 0) =>
		new(tick, attacker, move, source, special, level, strength, kind, 0, 0);

	sealed class Out : IHitSoundOut
	{
		public readonly List<(int Tick, string Sample, int Db)> Played = new();
		readonly Func<int> _clock;
		public Out(Func<int>? clock = null) { _clock = clock ?? (() => -1); }
		public void Play(string sample, int volumeDb) => Played.Add((_clock(), sample, volumeDb));
	}

	static string Thud(int n) => $"\"{H}{n}.wav\"";

	static string Table(string id, params string[] cues) =>
		$"{{\"kind\":\"sound_cue\",\"id\":\"{id}\",\"cues\":[{string.Join(",", cues)}]}}";

	static HitCueLibrary Lib(params string[] tables) => new(tables.Select(HitCueLibrary.Parse));

	// --- precedence and coverage ---------------------------------------------------------------------------------

	// Sound-cue schema (when): specific to general, a move beats a source beats a default; V6: every hit sounds.
	[Test]
	public static void Coverage_StrengthRulesAloneStillSoundEveryCombination()
	{
		var lib = Lib(Table("a",
			$"{{\"when\":{{\"strength\":\"light\"}},\"thud\":[{Thud(1)}]}}",
			$"{{\"when\":{{\"strength\":\"medium\"}},\"thud\":[{Thud(2)}]}}",
			$"{{\"when\":{{\"strength\":\"heavy\"}},\"thud\":[{Thud(3)}]}}",
			$"{{\"when\":{{\"move\":\"big-wave\",\"level\":2,\"outcome\":\"hit\"}},\"thud\":[{Thud(4)}]}}",
			$"{{\"when\":{{\"source\":\"oni\",\"level\":3,\"strength\":\"heavy\"}},\"thud\":[{Thud(5)}]}}"));
		foreach (var src in new[] { "", "ryo", "kitsune", "oni", "kappa" })
			foreach (int lvl in new[] { 1, 2, 3 })
				foreach (var st in new[] { HitStrength.Light, HitStrength.Medium, HitStrength.Heavy })
					foreach (var k in new[] { HitSparkKind.Hit, HitSparkKind.Block, HitSparkKind.Counter })
						foreach (var sp in new string?[] { null, "big-wave" })
							Assert.True(lib.Resolve(Cue(move: sp ?? "jab", source: src, special: sp, level: lvl, strength: st, kind: k)) is not null,
								$"{src} L{lvl} {st} {k} special={sp}: a hit must never be silent (V6)");
		var counterOnLv2 = lib.Resolve(Cue(move: "big-wave", special: "big-wave", level: 2, strength: HitStrength.Light, kind: HitSparkKind.Counter))!.Value;
		Assert.Equal(H + "1.wav", counterOnLv2.Thud, "a counter on a level-2 special the move rule does not name falls to the strength rule");
	}

	// Sound-cue schema: rank is move > source > default first; naming more fields only breaks ties inside a rank.
	[Test]
	public static void Precedence_RankBeatsNarrowingBothWays()
	{
		var lib = Lib(Table("a",
			$"{{\"thud\":[{Thud(1)}]}}",
			$"{{\"when\":{{\"level\":2,\"strength\":\"light\",\"outcome\":\"hit\"}},\"thud\":[{Thud(2)}]}}",
			$"{{\"when\":{{\"source\":\"oni\"}},\"thud\":[{Thud(3)}]}}",
			$"{{\"when\":{{\"source\":\"oni\",\"level\":2,\"strength\":\"light\",\"outcome\":\"hit\"}},\"thud\":[{Thud(4)}]}}",
			$"{{\"when\":{{\"move\":\"x\"}},\"thud\":[{Thud(5)}]}}"));
		Assert.Equal(H + "2.wav", lib.Resolve(Cue(level: 2))!.Value.Thud, "default tier: the rule naming three fields beats the bare default");
		Assert.Equal(H + "3.wav", lib.Resolve(Cue(source: "oni", level: 3))!.Value.Thud, "a bare source rule beats a default naming three fields");
		Assert.Equal(H + "4.wav", lib.Resolve(Cue(source: "oni", level: 2))!.Value.Thud, "source tier: more fields wins");
		Assert.Equal(H + "5.wav", lib.Resolve(Cue(move: "x", source: "oni", level: 2))!.Value.Thud, "a bare move rule beats a source rule naming everything");
	}

	// Sound-cue schema: ties go to the first file in name order, then the first rule; not creation order.
	[Test]
	public static void Ties_FileNameOrderNotCreationOrder()
	{
		string tmp = Path.Combine(Path.GetTempPath(), "yok93b-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tmp);
		try
		{
			string F(string id, int n, string when) => Table(id, $"{{\"when\":{when},\"thud\":[{Thud(n)}]}}");
			File.WriteAllText(Path.Combine(tmp, "b.json"), F("b", 2, "{\"source\":\"oni\",\"outcome\":\"hit\"}"));
			File.WriteAllText(Path.Combine(tmp, "z.json"), F("z", 9, "{}"));
			File.WriteAllText(Path.Combine(tmp, "a.json"), F("a", 1, "{\"source\":\"oni\",\"strength\":\"heavy\"}"));
			var lib = HitCueLibrary.LoadDirectory(tmp);
			Assert.Equal(H + "1.wav", lib.Resolve(Cue(source: "oni", strength: HitStrength.Heavy))!.Value.Thud, "a.json before b.json, though written after it");
			var lib2 = Lib(Table("a", $"{{\"when\":{{\"source\":\"oni\",\"strength\":\"heavy\"}},\"thud\":[{Thud(1)}]}}"),
				Table("b", $"{{\"when\":{{\"source\":\"oni\",\"outcome\":\"hit\"}},\"thud\":[{Thud(2)}]}}", $"{{\"thud\":[{Thud(9)}]}}"));
			Assert.Equal(H + "1.wav", lib2.Resolve(Cue(source: "oni", strength: HitStrength.Heavy))!.Value.Thud, "table order as given");
		}
		finally { Directory.Delete(tmp, true); }
	}

	// --- loader against the schema, and against the validator's case list -------------------------------------------

	static string CasesPath => ProjectSettings.GlobalizePath("res://tests/fixtures/sound-probe/cases.json");

	// Schema + V6 per case (tools/tests/test_sound_cue_breaker_probes.py runs the same list through validate_data.py).
	// Also: the scene only catches FormatException / IOException, so the loader may throw nothing else.
	[Test]
	public static void LoaderAndCaseList_AcceptAndRefuseWhatTheSchemaSays()
	{
		using var doc = JsonDocument.Parse(File.ReadAllText(CasesPath));
		var wrong = new List<string>();
		int n = 0;
		foreach (var c in doc.RootElement.GetProperty("cases").EnumerateArray())
		{
			n++;
			string name = c.GetProperty("name").GetString()!;
			bool ok = c.TryGetProperty("loader_ok", out var lo) ? lo.GetBoolean() : c.GetProperty("ok").GetBoolean(); // a missing sample file is the validator's check; at runtime the pool skips it
			string tmp = Path.Combine(Path.GetTempPath(), "yok93c-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(tmp);
			try
			{
				foreach (var f in c.GetProperty("files").EnumerateObject())
					File.WriteAllText(Path.Combine(tmp, f.Name + ".json"), f.Value.ValueKind == JsonValueKind.String ? f.Value.GetString()! : f.Value.GetRawText());
				string? result = null;
				try { HitCueLibrary.LoadDirectory(tmp); }
				catch (FormatException) { result = "refused"; }
				catch (Exception e) when (e is IOException or UnauthorizedAccessException) { result = "io"; }
				catch (Exception e) { result = "CRASH " + e.GetType().Name + ": " + e.Message; }
				if (result != null && result.StartsWith("CRASH")) wrong.Add($"{name}: {result} (the scene would not catch it)");
				else if ((result == null) != ok) wrong.Add($"{name}: expected {(ok ? "accept" : "refuse")} ({c.GetProperty("why").GetString()}), loader {(result == null ? "accepted" : "refused")}");
			}
			finally { Directory.Delete(tmp, true); }
		}
		Assert.True(n >= 90, "the case list is there");
		Assert.True(wrong.Count == 0, string.Join("; ", wrong));
	}

	// --- runtime sample loading ----------------------------------------------------------------------------------

	// A missing sample warns once, plays nothing, does not crash and does not eat a player.
	[Test]
	public static void Pool_MissingSampleIsSkippedQuietly(Node runner)
	{
		var pool = new HitSoundPool();
		runner.AddChild(pool);
		try
		{
			pool.Play("res://sound/hits/not-there.wav", 0);
			pool.Play("res://sound/hits/not-there.wav", 0);
			pool.Play("res://sound/hits/not-there.ogg", 0);
			Assert.True(pool.Players.All(p => p.Stream == null), "nothing played for a sample that does not load");
			var cache = (Dictionary<string, AudioStream?>)typeof(HitSoundPool).GetField("_streams", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(pool)!;
			Assert.True(cache.TryGetValue("res://sound/hits/not-there.wav", out var s) && s == null, "the failure is remembered, so it warns once, not per hit");
			pool.Play(H + "1.wav", -3);
			Assert.True(pool.Players[0].Stream != null && pool.Players[0].VolumeDb == -3f, "a failed sample did not use up a player: the next one starts on the first");
		}
		finally { pool.QueueFree(); }
	}

	// --- the sink's clock ----------------------------------------------------------------------------------------

	static HitCueLibrary Offset7() => Lib(Table("d",
		$"{{\"thud\":[{Thud(1)},{Thud(2)},{Thud(3)}],\"sting\":[{Thud(4)},{Thud(5)},{Thud(6)}],\"offset_ticks\":7}}"));

	static MoveData[] Normals() => MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureNormalsDir));

	static int SlotOf(MoveData[] moves, InputBits b) => Array.FindIndex(moves, m => m.Button == b && !m.Air && m.DirectionMask == 0);

	/// <summary>One scripted fight; returns what played (stamped with the match tick it started on), the cues and the hashes.</summary>
	static (List<(int Tick, string Sample, int Db)> Played, List<HitCue> Cues, List<ulong> Hashes, Match M) Fight(HitCueLibrary lib, int p2Health, int shift, int steps)
	{
		var n = Normals();
		int lp = SlotOf(n, InputBits.LightPunch), hp = SlotOf(n, InputBits.HeavyPunch);
		var m = new Match(new SimConfig { P2MaxHealth = p2Health }, n, n);
		m.P1.X = -m.Config.BodyWidth / 2;
		m.P2.X = m.Config.BodyWidth / 2;
		var output = new Out(() => m.Tick);
		var sink = new DataHitCueSink(lib, output);
		var sparks = new HitSparks();
		var cues = new List<HitCue>();
		m.Impact += (mm, e) => { var c = sparks.OnImpact(mm, e); cues.Add(c); sink.Play(c); };
		var hashes = new List<ulong>();
		for (int t = 0; t < steps; t++)
		{
			var p1 = t % (37 + shift) == 0 ? FighterInput.Attack(t % 74 == 0 ? hp : lp) : t % 5 == 0 ? new FighterInput(InputBits.Right) : FighterInput.None;
			var p2 = t % 90 < 30 ? new FighterInput(InputBits.Right) : FighterInput.None;
			m.Step(p1, p2);
			sink.Advance(m.Tick);
			hashes.Add(m.StateHash());
		}
		return (output.Played, cues, hashes, m);
	}

	// V6 "timed to the impact frame": every sound starts exactly offset ticks after its impact tick, hitstop ticks
	// counting (schema offset_ticks), including the KO blow and the KO slow-down; no hit is dropped.
	[Test]
	public static void Sink_EveryHitSoundsOnImpactTickPlusOffsetThroughHitstop()
	{
		var (played, cues, _, _) = Fight(Offset7(), 1000, 0, 1500);
		Assert.True(cues.Count >= 2, $"several hits landed: {cues.Count}");
		AssertTimed(played, cues);
	}

	// The KO blow too: its sound (offset 7) starts inside the KO slow-down (60 ticks), not lost when the match ends.
	[Test]
	public static void Sink_KoBlowStillSoundsOnItsOffset()
	{
		var (played, cues, _, m) = Fight(Offset7(), 1, 0, 400);
		Assert.True(m.Phase != MatchPhase.Fighting && cues.Count >= 1, "the first hit was the KO (else the probe proves nothing)");
		AssertTimed(played, cues);
	}

	static void AssertTimed(List<(int Tick, string Sample, int Db)> played, List<HitCue> cues)
	{
		var thuds = played.Where(p => new[] { 1, 2, 3 }.Any(i => p.Sample == H + i + ".wav")).ToList();
		var stings = played.Where(p => new[] { 4, 5, 6 }.Any(i => p.Sample == H + i + ".wav")).ToList();
		Assert.Equal(cues.Count, thuds.Count, "one thud per hit (none lost, none doubled)");
		Assert.Equal(cues.Count, stings.Count, "one sting per hit");
		for (int i = 0; i < cues.Count; i++)
		{
			Assert.Equal(cues[i].Tick + 7, thuds[i].Tick, $"hit {i}: thud starts impact tick + 7");
			Assert.Equal(cues[i].Tick + 7, stings[i].Tick, $"hit {i}: sting starts with the thud");
		}
	}

	// Catch-up (several sim steps per frame): each Advance(tick) fires what is due, in hit order, once.
	[Test]
	public static void Sink_LateAdvanceFiresInHitOrderOnce()
	{
		var output = new Out();
		var sink = new DataHitCueSink(Lib(Table("d", $"{{\"thud\":[{Thud(1)}],\"sting\":[{Thud(2)}],\"offset_ticks\":4}}")), output);
		sink.Play(Cue(tick: 10, attacker: 0));
		sink.Advance(10);
		sink.Play(Cue(tick: 12, attacker: 1));
		sink.Advance(12);
		sink.Advance(13);
		Assert.Equal(0, output.Played.Count, "nothing before tick 14");
		sink.Advance(16);
		Assert.Equal(4, output.Played.Count, "both hits, thud and sting each");
		Assert.Equal(H + "1.wav", output.Played[0].Sample, "first hit's thud first");
		Assert.Equal(H + "2.wav", output.Played[1].Sample, "then its sting");
		sink.Advance(17);
		sink.Advance(40);
		Assert.Equal(4, output.Played.Count, "nothing plays twice");
		Assert.Equal(0, sink.Pending.Count, "nothing left waiting");
	}

	// Replay determinism: the same script gives the same sounds on the same ticks and the same hashes; a different script differs.
	[Test]
	public static void Sink_SameScriptSameSoundsAndHashesDifferentScriptDiffers()
	{
		var a = Fight(Offset7(), 1000, 0, 900);
		var b = Fight(Offset7(), 1000, 0, 900);
		Assert.True(a.Played.Count > 4, "the script played hit sounds");
		Assert.True(a.Played.SequenceEqual(b.Played), "same sounds, same ticks, same volumes");
		Assert.True(a.Hashes.SequenceEqual(b.Hashes), "same StateHash every tick");
		var none = Fight(HitCueLibrary.Empty, 1000, 0, 900);
		Assert.True(a.Hashes.SequenceEqual(none.Hashes), "StateHash unchanged with the sink listening (presentation only)");
		var c = Fight(Offset7(), 1000, 5, 900);
		Assert.True(!a.Played.SequenceEqual(c.Played), "different choices, different sounds");
		Assert.True(!a.Hashes.SequenceEqual(c.Hashes), "different choices, different hash");
	}

	// --- the scene -----------------------------------------------------------------------------------------------

	static FightScene Scene(Node runner, HitCueLibrary tables)
	{
		var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		scene.ExternalDrive = true;
		scene.SoundCueTables = tables;
		runner.AddChild(scene);
		if (scene.P2Ai != null) scene.ToggleP2Ai();
		Close(scene);
		return scene;
	}

	static void Close(FightScene scene)
	{
		var m = scene.Match;
		m.P1.X = -m.Config.BodyWidth / 2;
		m.P2.X = m.Config.BodyWidth / 2;
	}

	static void LandJab(FightScene scene)
	{
		int hits = 0;
		void Count(Match _, ImpactEvent __) => hits++;
		var match = scene.Match;
		match.Impact += Count;
		int lp = SlotOf(match.P1.Moves, InputBits.LightPunch);
		scene.Step(FighterInput.Attack(lp), FighterInput.None);
		for (int t = 0; t < 40 && hits == 0; t++) scene.Step(FighterInput.None, FighterInput.None);
		match.Impact -= Count;
		Assert.True(hits > 0 && scene.LastHitCue is not null, "the jab lands");
	}

	static int Sounding(FightScene scene) => scene.HitSoundPool!.Players.Count(p => p.Stream != null);

	static HitCueLibrary Offset5WithSting() => Lib(Table("d", $"{{\"thud\":[{Thud(1)}],\"sting\":[{Thud(2)}],\"offset_ticks\":5}}"));

	// V6: thud and sting on the impact frame (+ the rule's offset), through the scene's own stepping.
	[Test]
	public static void Scene_OffsetCountsFromTheImpactTick(Node runner)
	{
		var scene = Scene(runner, Offset5WithSting());
		try
		{
			LandJab(scene);
			int impact = scene.LastHitCue!.Value.Tick;
			Assert.Equal(0, Sounding(scene), "waiting on the offset");
			int guard = 0;
			while (Sounding(scene) == 0 && guard++ < 30) scene.Step(FighterInput.None, FighterInput.None);
			Assert.Equal(impact + 5, scene.Match.Tick, "starts on impact tick + 5");
			Assert.Equal(2, Sounding(scene), "thud and sting layered");
		}
		finally { scene.QueueFree(); }
	}

	// A waiting sound of the old fight never plays in the replayed one (in-place Match.Reset).
	[Test]
	public static void Scene_ResetFightDropsAWaitingSound(Node runner)
	{
		var scene = Scene(runner, Offset5WithSting());
		try
		{
			LandJab(scene);
			Assert.True(scene.DataCues!.Pending.Count > 0, "a sound is waiting");
			scene.ResetFight();
			Close(scene);
			for (int t = 0; t < 12; t++) scene.Step(FighterInput.None, FighterInput.None);
			Assert.Equal(0, Sounding(scene), "the old fight's sound did not play in the new one");
			Assert.Equal(0, scene.DataCues.Pending.Count, "nothing left waiting");
		}
		finally { scene.QueueFree(); }
	}

	// New match (restart): no stale sound, and still exactly one thud + one sting per hit (the placeholder pack never comes back).
	[Test]
	public static void Scene_RestartRunDropsWaitingSoundAndNeverDoubles(Node runner)
	{
		var scene = Scene(runner, Offset5WithSting());
		try
		{
			LandJab(scene);
			var before = scene.Match;
			scene.RestartRun();
			Assert.True(!ReferenceEquals(before, scene.Match), "a new match");
			Close(scene);
			for (int t = 0; t < 12; t++) scene.Step(FighterInput.None, FighterInput.None);
			Assert.Equal(0, Sounding(scene), "the old match's waiting sound did not play");
			Assert.True(scene.GetNodeOrNull("HitSound1") is null, "no placeholder players");
			LandJab(scene);
			for (int t = 0; t < 8; t++) scene.Step(FighterInput.None, FighterInput.None);
			Assert.Equal(2, Sounding(scene), "one thud and one sting for the new match's hit, nothing doubled");
		}
		finally { scene.QueueFree(); }
	}

	// While paused no tick runs, so a waiting sound neither plays early nor is lost.
	[Test]
	public static void Scene_PauseHoldsAWaitingSound(Node runner)
	{
		var scene = Scene(runner, Offset5WithSting());
		scene.PauseEnabled = true;
		try
		{
			LandJab(scene);
			Assert.True(scene.TryPause(), "pauses");
			for (int t = 0; t < 20; t++) scene.Step(FighterInput.None, FighterInput.None);
			Assert.Equal(0, Sounding(scene), "nothing plays while paused");
			Assert.True(scene.DataCues!.Pending.Count > 0, "still waiting");
			scene.PauseMenu!.Close();
			int guard = 0;
			while (Sounding(scene) == 0 && guard++ < 30) scene.Step(FighterInput.None, FighterInput.None);
			Assert.Equal(2, Sounding(scene), "plays after the resume");
		}
		finally { scene.QueueFree(); }
	}

	// Presentation only: the scene's sim is the same with or without cue tables.
	[Test]
	public static void Scene_StateHashSameWithAndWithoutTables(Node runner)
	{
		List<ulong> Run(HitCueLibrary tables)
		{
			var scene = Scene(runner, tables);
			try
			{
				var hashes = new List<ulong>();
				int lp = SlotOf(scene.Match.P1.Moves, InputBits.LightPunch);
				for (int t = 0; t < 120; t++)
				{
					scene.Step(t % 40 == 0 ? FighterInput.Attack(lp) : FighterInput.None, FighterInput.None);
					hashes.Add(scene.Match.StateHash());
				}
				return hashes;
			}
			finally { scene.QueueFree(); }
		}
		var plain = Run(HitCueLibrary.Empty);
		var cued = Run(Offset5WithSting());
		Assert.True(plain.SequenceEqual(cued), "same StateHash every tick with the sink listening");
	}
}
