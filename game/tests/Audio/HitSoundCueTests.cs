using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.Audio;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-93 (V6): hit-sound cue tables (data/schema/sound-cue.schema.json), the loader's precedence and sample pick,
/// the data sink's tick-timed playback and the scene's switch away from the placeholder hit pack. The cue table is the
/// TEST FIXTURE in tests/fixtures/sound/ (never game content); no audio device is needed.
/// </summary>
public static class HitSoundCueTests
{
	static string Fixtures => ProjectSettings.GlobalizePath("res://tests/fixtures/sound");
	static HitCueLibrary Lib() => HitCueLibrary.LoadDirectory(Fixtures);
	const string H = "res://sound/hits/hit_0";

	static HitCue Cue(string move = "test-jab", string source = "ryo", string? special = null, int level = 1,
		HitStrength strength = HitStrength.Light, HitSparkKind kind = HitSparkKind.Hit, int tick = 100, int attacker = 0) =>
		new(tick, attacker, move, source, special, level, strength, kind, 0, 0);

	sealed class Out : IHitSoundOut
	{
		public readonly List<(string Sample, int Db)> Played = new();
		public void Play(string sample, int volumeDb) => Played.Add((sample, volumeDb));
	}

	[Test]
	public static void Precedence_MoveBeatsSourceBeatsDefault()
	{
		var lib = Lib();
		Assert.True(lib.HasCues, "the fixture table loads");

		var s = lib.Resolve(Cue())!.Value;
		Assert.True(new[] { H + "1.wav", H + "2.wav", H + "3.wav" }.Contains(s.Thud), $"default rule's thud: {s.Thud}");
		Assert.True(s.Sting is null, "the default rule has no sting");
		Assert.Equal(6, s.VolumeDb, "default volume");
		Assert.Equal(100, s.StartTick, "offset 0 = the impact tick");

		var block = lib.Resolve(Cue(kind: HitSparkKind.Block))!.Value;
		Assert.Equal(H + "4.wav", block.Thud, "a block picks the block rule over the bare default");
		Assert.Equal(-6, block.VolumeDb, "block volume");

		var fox = lib.Resolve(Cue(source: "kitsune"))!.Value;
		Assert.Equal(H + "5.wav", fox.Thud, "a source rule beats the default");
		Assert.True(fox.Sting is H + "6.wav" or H + "7.wav", $"its yokai sting: {fox.Sting}");

		var foxBlock = lib.Resolve(Cue(source: "kitsune", kind: HitSparkKind.Block))!.Value;
		Assert.Equal(H + "5.wav", foxBlock.Thud, "a source rule beats a default naming the outcome");

		var foxCounter = lib.Resolve(Cue(source: "kitsune", kind: HitSparkKind.Counter))!.Value;
		Assert.Equal(H + "8.wav", foxCounter.Thud, "within the source tier, naming the outcome wins");
		Assert.Equal(102, foxCounter.StartTick, "its 2-tick offset");

		var wave = lib.Resolve(Cue(move: "test-spirit-wave", source: "kitsune", special: "test-spirit-wave"))!.Value;
		Assert.Equal(H + "9.wav", wave.Thud, "a move rule beats a source rule");
		Assert.Equal(3, wave.VolumeDb, "move volume");
		Assert.Equal(104, wave.StartTick, "move offset");

		var ex = lib.Resolve(Cue(move: "test-spirit-wave-ex", special: "test-spirit-wave"))!.Value;
		Assert.Equal(H + "9.wav", ex.Thud, "a special's id covers its EX move");

		var lv3 = lib.Resolve(Cue(move: "great-wave", special: "test-spirit-wave", level: 3))!.Value;
		Assert.Equal(H + "1.wav", lv3.Thud, "naming the level narrows the move rule (the evolution)");
		Assert.Equal(H + "2.wav", lv3.Sting, "with its sting");

		foreach (var src in new[] { "ryo", "kitsune", "oni", "kappa" })
			foreach (var st in new[] { HitStrength.Light, HitStrength.Medium, HitStrength.Heavy })
				foreach (var k in new[] { HitSparkKind.Hit, HitSparkKind.Block, HitSparkKind.Counter })
					Assert.True(lib.Resolve(Cue(move: "any-move", source: src, strength: st, kind: k)) is not null, $"{src} {st} {k}: every hit sounds (V6)");
	}

	static HitCueTable Table(string id, params string[] whens) => HitCueLibrary.Parse(
		$"{{\"kind\":\"sound_cue\",\"id\":\"{id}\",\"cues\":[" +
		string.Join(",", whens.Select(w => $"{{\"when\":{w},\"thud\":[\"res://sound/hits/hit_01.wav\"]}}")) + "]}");

	static string? Refusal(params HitCueTable[] tables)
	{
		try { _ = new HitCueLibrary(tables); return null; }
		catch (FormatException e) { return e.Message; }
	}

	[Test]
	public static void Coverage_ASetWithoutADefaultIsRefused()
	{
		string? why = Refusal(Table("kitsune", "{\"source\":\"kitsune\"}"));
		Assert.True(why != null && why.Contains("no default"), $"a source rule alone would leave Ryo's hits silent: {why}");
		Assert.True(Refusal(Table("a", "{\"move\":\"x\"}", "{\"level\":2}")) != null, "a level rule is not a default");
		Assert.True(Refusal(Table("a", "{\"strength\":\"light\"}", "{\"strength\":\"medium\"}")) != null, "heavy hits uncovered");
		Assert.True(Refusal(Table("a", "{\"outcome\":\"hit\",\"strength\":\"heavy\"}", "{\"outcome\":\"block\"}", "{\"outcome\":\"counter\"}")) != null,
			"light and medium clean hits uncovered");
		Assert.True(Refusal(Table("a", "{\"outcome\":\"hit\"}", "{\"outcome\":\"block\"}", "{\"outcome\":\"counter\"}")) is null,
			"outcome rules covering every outcome are a default");
		Assert.True(Refusal(Table("a", "{\"source\":\"oni\"}"), Table("b", "{}")) is null, "the default may sit in another table");
		Assert.True(Refusal() is null && !HitCueLibrary.Empty.HasCues, "no tables at all is fine: the placeholder pack plays");

		string tmp = Path.Combine(Path.GetTempPath(), "yok93-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tmp);
		try
		{
			File.WriteAllText(Path.Combine(tmp, "kitsune.json"), "{\"kind\":\"sound_cue\",\"id\":\"kitsune\",\"cues\":[{\"when\":{\"source\":\"kitsune\"},\"thud\":[\"res://sound/hits/hit_01.wav\"]}]}");
			Assert.Equal(HitCueLibrary.Empty, FightScene.LoadSoundCues(tmp), "the scene refuses a set with no default and keeps the placeholder pack");
		}
		finally { Directory.Delete(tmp, true); }
	}

	[Test]
	public static void Ties_FirstRuleInFileThenArrayOrderWins()
	{
		string T(string id, string when, string n) =>
			$"{{\"kind\":\"sound_cue\",\"id\":\"{id}\",\"cues\":[{{\"when\":{when},\"thud\":[\"res://sound/hits/hit_0{n}.wav\"]}}]}}";
		var lib = new HitCueLibrary(new[]
		{
			HitCueLibrary.Parse(T("a", "{\"source\":\"oni\",\"strength\":\"heavy\"}", "1")),
			HitCueLibrary.Parse(T("b", "{\"source\":\"oni\",\"outcome\":\"hit\"}", "2")),
			HitCueLibrary.Parse(T("c", "{}", "3")),
		});
		var s = lib.Resolve(Cue(source: "oni", strength: HitStrength.Heavy))!.Value;
		Assert.Equal(H + "1.wav", s.Thud, "same tier and narrowing: the first table's rule");
	}

	[Test]
	public static void Pick_IsDeterministicAndUsesEverySample()
	{
		var lib = Lib();
		var seen = new HashSet<string>();
		for (int t = 0; t < 300; t++)
		{
			var a = lib.Resolve(Cue(tick: t, attacker: t % 2))!.Value;
			var b = lib.Resolve(Cue(tick: t, attacker: t % 2))!.Value;
			Assert.Equal(a, b, $"tick {t}: the same hit picks the same sample");
			seen.Add(a.Thud);
		}
		Assert.Equal(3, seen.Count, "every listed thud gets played over a fight");
		Assert.Equal(0, HitCueLibrary.Pick(1, 12345, 1, 0), "one sample = that sample");
		for (int t = -5; t < 500; t += 7)
			for (int n = 1; n <= 5; n++)
			{
				int i = HitCueLibrary.Pick(n, t, t & 1, t % 3);
				Assert.True(i >= 0 && i < n, $"pick {i} in range for {n}");
			}
	}

	[Test]
	public static void Sink_PlaysOnTheImpactTickOrAfterItsOffset()
	{
		var output = new Out();
		var sink = new DataHitCueSink(Lib(), output);
		sink.Play(Cue(tick: 10));
		Assert.Equal(1, output.Played.Count, "offset 0 plays at once, thud only");
		Assert.Equal(6, output.Played[0].Db, "at the rule's volume");

		output.Played.Clear();
		sink.Play(Cue(source: "kitsune", kind: HitSparkKind.Counter, tick: 20));
		Assert.Equal(0, output.Played.Count, "a 2-tick offset waits");
		sink.Advance(21);
		Assert.Equal(0, output.Played.Count, "not yet on tick 21");
		sink.Advance(22);
		Assert.Equal(2, output.Played.Count, "thud and sting together on tick 22");
		Assert.Equal((H + "8.wav", 0), output.Played[0], "thud first");
		Assert.Equal((H + "6.wav", 0), output.Played[1], "then the sting");
		sink.Advance(23);
		Assert.Equal(2, output.Played.Count, "played once");

		output.Played.Clear();
		sink.Play(Cue(move: "test-spirit-wave", special: "test-spirit-wave", tick: 30));
		sink.Advance(5); // a reset or a new match: the tick went back
		sink.Advance(34);
		Assert.Equal(0, output.Played.Count, "a reset drops the waiting sound");

		sink.Play(Cue(move: "test-spirit-wave", special: "test-spirit-wave", tick: 40));
		sink.Clear();
		sink.Advance(44);
		Assert.Equal(0, output.Played.Count, "Clear drops the waiting sound");

		sink.Play(Cue(move: "test-spirit-wave", special: "test-spirit-wave", tick: 50));
		sink.Advance(60);
		Assert.Equal(1, output.Played.Count, "a late Advance (several ticks in one frame) still plays it once");
	}

	[Test]
	public static void EmptyOrMissingFolder_KeepsThePlaceholderPack()
	{
		Assert.Equal(HitCueLibrary.Empty, HitCueLibrary.LoadDirectory(Path.Combine(Fixtures, "no-such-folder")), "absent folder = no cues");
		string tmp = Path.Combine(Path.GetTempPath(), "yok93-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tmp);
		try
		{
			Assert.True(!HitCueLibrary.LoadDirectory(tmp).HasCues, "empty folder = no cues");
			File.WriteAllText(Path.Combine(tmp, "x.json"), "{\"kind\":\"sound_cue\",\"id\":\"y\",\"cues\":[{\"thud\":[\"res://sound/hits/hit_01.wav\"]}]}");
			string? why = null;
			try { HitCueLibrary.LoadDirectory(tmp); } catch (FormatException e) { why = e.Message; }
			Assert.True(why != null && why.Contains("file name"), $"id must equal the file name: {why}");
			Assert.Equal(HitCueLibrary.Empty, FightScene.LoadSoundCues(tmp), "the scene falls back on a bad folder");
			File.Delete(Path.Combine(tmp, "x.json"));
			File.WriteAllText(Path.Combine(tmp, "a.json"), "{\"kind\":\"sound_cue\",\"id\":\"a\",\"cues\":[{\"when\":{\"source\":\"oni\"},\"thud\":[\"res://sound/hits/hit_01.wav\"]}]}");
			File.WriteAllText(Path.Combine(tmp, "b.json"), "{\"kind\":\"sound_cue\",\"id\":\"b\",\"cues\":[{\"when\":{\"source\":\"oni\"},\"thud\":[\"res://sound/hits/hit_02.wav\"]}]}");
			why = null;
			try { HitCueLibrary.LoadDirectory(tmp); } catch (FormatException e) { why = e.Message; }
			Assert.True(why != null && why.Contains("same case"), $"one rule per case across tables: {why}");
		}
		finally { Directory.Delete(tmp, true); }
	}

	[Test]
	public static void Loader_RefusesWhatTheSchemaRefuses()
	{
		const string ok = "{\"kind\":\"sound_cue\",\"id\":\"t\",\"cues\":[{\"thud\":[\"res://sound/hits/hit_01.wav\"]}]}";
		var t = HitCueLibrary.Parse(ok);
		Assert.Equal(1, t.Cues.Count, "one rule");
		Assert.Equal(HitCueWhen.Any, t.Cues[0].When, "no when = default");
		Assert.Equal(0, t.Cues[0].VolumeDb, "volume defaults to 0 dB");
		Assert.Equal(0, t.Cues[0].OffsetTicks, "offset defaults to the impact tick");

		string Cue1(string body) => "{\"kind\":\"sound_cue\",\"id\":\"t\",\"cues\":[" + body + "]}";
		const string thud = "\"thud\":[\"res://sound/hits/hit_01.wav\"]";
		var bad = new (string json, string field)[]
		{
			(ok.Replace("sound_cue", "level_preset"), "kind"),
			(ok.Replace("\"id\":\"t\"", "\"id\":\"T\""), "id"),
			(ok.Replace("\"id\":\"t\",", ""), "id"),
			(Cue1(""), "cues"),
			("{\"kind\":\"sound_cue\",\"id\":\"t\"}", "cues"),
			(ok.TrimEnd('}') + ",\"volume\":1}", "volume"),
			(Cue1("{}"), "cues.0.thud"),
			(Cue1("{\"thud\":[]}"), "cues.0.thud"),
			(Cue1("{\"thud\":[\"sound/hits/hit_01.wav\"]}"), "cues.0.thud.0"),
			(Cue1("{\"thud\":[\"res://vfx/hit.wav\"]}"), "cues.0.thud.0"),
			(Cue1("{\"thud\":[\"res://sound/../x.wav\"]}"), "cues.0.thud.0"),
			(Cue1("{\"thud\":[\"res://sound/hits/hit_01.wav\\n\"]}"), "cues.0.thud.0"),
			(Cue1("{\"thud\":[\"res://sound/a.wav\",\"res://sound/a.wav\"]}"), "cues.0.thud.1"),
			(Cue1("{" + thud + ",\"sting\":[]}"), "cues.0.sting"),
			(Cue1("{" + thud + ",\"volume_db\":13}"), "cues.0.volume_db"),
			(Cue1("{" + thud + ",\"volume_db\":-41}"), "cues.0.volume_db"),
			(Cue1("{" + thud + ",\"volume_db\":1.5}"), "cues.0.volume_db"),
			(Cue1("{" + thud + ",\"offset_ticks\":-1}"), "cues.0.offset_ticks"),
			(Cue1("{" + thud + ",\"offset_ticks\":31}"), "cues.0.offset_ticks"),
			(Cue1("{" + thud + ",\"pitch\":1}"), "cues.0.pitch"),
			(Cue1("{" + thud + ",\"when\":{\"level\":4}}"), "cues.0.when.level"),
			(Cue1("{" + thud + ",\"when\":{\"strength\":\"huge\"}}"), "cues.0.when.strength"),
			(Cue1("{" + thud + ",\"when\":{\"outcome\":\"whiff\"}}"), "cues.0.when.outcome"),
			(Cue1("{" + thud + ",\"when\":{\"move\":\"Foxfire\"}}"), "cues.0.when.move"),
			(Cue1("{" + thud + ",\"when\":{\"source\":\"elder-oni\"}}"), "cues.0.when.source"),
			(Cue1("{" + thud + ",\"when\":{\"source\":\"tanuki\"}}"), "cues.0.when.source"),
			(Cue1("{" + thud + ",\"when\":{\"button\":\"LP\"}}"), "cues.0.when.button"),
			(Cue1("{" + thud + "},{" + thud + ",\"when\":{}}"), "cues.1.when"),
			(ok.TrimEnd('}') + ",\"status\":\"maybe\"}", "status"),
		};
		foreach (var (json, field) in bad)
		{
			string? msg = null;
			try { HitCueLibrary.Parse(json); }
			catch (FormatException e) { msg = e.Message; }
			Assert.True(msg != null && msg.StartsWith(field), $"refused naming '{field}': {json} -> {msg ?? "accepted"}");
		}
	}

	// --- the scene -------------------------------------------------------------------------------------------

	static int SlotOf(MoveData[] moves, InputBits b) => Array.FindIndex(moves, m => m.Button == b && !m.Air && m.DirectionMask == 0);

	static FightScene Scene(Node runner, HitCueLibrary tables)
	{
		var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		scene.ExternalDrive = true;
		scene.SoundCueTables = tables;
		runner.AddChild(scene);
		if (scene.P2Ai != null) scene.ToggleP2Ai();
		var m = scene.Match;
		m.P1.X = -m.Config.BodyWidth / 2;
		m.P2.X = m.Config.BodyWidth / 2;
		return scene;
	}

	static void LandJab(FightScene scene)
	{
		int lp = SlotOf(scene.Match.P1.Moves, InputBits.LightPunch);
		Assert.True(lp >= 0, "Ryo has a jab");
		scene.Step(FighterInput.Attack(lp), FighterInput.None);
		for (int t = 0; t < 40 && scene.LastHitCue is null; t++) scene.Step(FighterInput.None, FighterInput.None);
		Assert.True(scene.LastHitCue is not null, "the jab lands");
	}

	[Test]
	public static void Scene_NoCueTables_KeepsTheRoundRobinPack(Node runner)
	{
		var scene = Scene(runner, HitCueLibrary.Empty);
		try
		{
			Assert.True(scene.HitCues is SilentHitCues, "no tables: the cue hook stays silent");
			Assert.True(scene.DataCues is null && scene.HitSoundPool is null, "no data sink, no pool");
			Assert.True(scene.GetNodeOrNull("HitSound1") is AudioStreamPlayer, "the placeholder players are there");
			LandJab(scene);
			var p = (AudioStreamPlayer)scene.GetNode("HitSound1");
			Assert.True(p.Stream is not null, "the placeholder pack played on the hit");
		}
		finally { scene.QueueFree(); }
	}

	[Test]
	public static void Scene_CueTables_ReplaceTheRoundRobinPack(Node runner)
	{
		var scene = Scene(runner, Lib());
		try
		{
			Assert.True(scene.DataCues is not null && ReferenceEquals(scene.HitCues, scene.DataCues), "the data sink takes the cues");
			Assert.True(scene.HitSoundPool is not null && scene.HitSoundPool.GetParent() == scene, "its pool is in the scene");
			Assert.True(scene.GetNodeOrNull("HitSound1") is null, "no placeholder players: hits never sound twice");
			foreach (var pl in scene.HitSoundPool!.Players)
			{
				Assert.Equal(Ui.MenuSettings.SfxBus, pl.Bus.ToString(), "on the Effects bus");
				Assert.Equal(Node.ProcessModeEnum.Pausable, pl.ProcessMode, "pausable");
			}
			LandJab(scene);
			var played = scene.HitSoundPool.Players.Where(pl => pl.Stream != null).ToList();
			Assert.Equal(1, played.Count, "one thud for the jab (the default rule has no sting)");
			string want = Lib().Resolve(scene.LastHitCue!.Value)!.Value.Thud;
			Assert.Equal(want, played[0].Stream.ResourcePath, "the sample the table picked for that hit");
			Assert.Equal(6f, played[0].VolumeDb, "at the rule's volume");
		}
		finally { scene.QueueFree(); }
	}

	[Test]
	public static void Sink_ListeningLeavesTheMatchUnchanged()
	{
		var n = MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureNormalsDir));
		int lp = SlotOf(n, InputBits.LightPunch), hp = SlotOf(n, InputBits.HeavyPunch);
		FighterInput P1(int t) => t % 37 == 0 ? FighterInput.Attack(t % 74 == 0 ? hp : lp) : t % 5 == 0 ? new FighterInput(InputBits.Right) : FighterInput.None;
		FighterInput P2(int t) => t % 90 < 30 ? new FighterInput(InputBits.Right) : FighterInput.None;
		Match Close()
		{
			var m = new Match(null, n, n);
			m.P1.X = -m.Config.BodyWidth / 2;
			m.P2.X = m.Config.BodyWidth / 2;
			return m;
		}
		var plain = Close();
		var watched = Close();
		var sparks = new HitSparks();
		var output = new Out();
		var sink = new DataHitCueSink(Lib(), output);
		watched.Impact += (mm, e) => sink.Play(sparks.OnImpact(mm, e));
		for (int t = 0; t < 1200; t++)
		{
			plain.Step(P1(t), P2(t));
			watched.Step(P1(t), P2(t));
			sink.Advance(watched.Tick);
			Assert.Equal(plain.StateHash(), watched.StateHash(), $"tick {t}: same state with hit sounds listening");
		}
		Assert.True(output.Played.Count > 0, "the run played hit sounds");
	}
}
