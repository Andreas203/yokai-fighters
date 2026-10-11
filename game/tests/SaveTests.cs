using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Flow;
using YokaiFighters.SaveGame;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-87: the save file. The pure <see cref="SaveStore"/> (round trip, missing / corrupt / older / newer / partial
/// files, fields added by later builds, a disk that refuses, backups), a run captured and restored mid-walk with the
/// same hash and the same rest of the run, restores the rules refuse, story progress independent of unlocks (M4),
/// and the boot scene: save points, Continue, the run removed when it ends, and isolation (nothing here, and nothing
/// a test or the harness instances, touches the player's file). Every file is under a temp directory.
/// </summary>
public static class SaveTests
{
	static string MapDir => ContentPaths.Data("map");
	static NodeSceneTable Table() => NodeSceneTable.Default(MapDir);
	static MapGraph DefaultMap() => FileMapSource.Default(MapDir).Load();

	static void WithTempFile(Action<string> body)
	{
		string dir = Path.Combine(Path.GetTempPath(), "yokai-save-tests-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(dir);
		try { body(Path.Combine(dir, SaveStore.FileName)); }
		finally { try { Directory.Delete(dir, recursive: true); } catch (IOException) { } }
	}

	static void Throws<T>(Action body, string what) where T : Exception
	{
		try { body(); }
		catch (T) { return; }
		throw new AssertFailed($"expected {typeof(T).Name}: {what}");
	}

	static void WithFixtures(Action body)
	{
		var saved = FightScene.Sources;
		FightScene.Sources = FightScene.FixtureSources;
		try { body(); }
		finally { FightScene.Sources = saved; }
	}

	static RunSession NewSession(uint seed) => RunSession.Start(FightScene.LoadAbilityPool(), DefaultMap(), Table(), seed);

	/// <summary>Plays the next node with no scene: first reachable node, won with <paramref name="healthLeft"/>, first card drafted.</summary>
	static void PlayNode(RunSession s, int healthLeft)
	{
		Assert.True(s.Enter(s.Reachable()[0]).Ok, "entered");
		if (s.WinFight(healthLeft) is { } draft)
		{
			var card = draft.Offer.Cards[0];
			Assert.True(draft.Pick(0, card.Targets.Count > 0 ? card.Targets[0].Slot : null).Ok, "picked " + card.AbilityId);
		}
		Assert.True(s.CompleteNode().Ok, "completed");
	}

	static string Slots(RunSession s) =>
		string.Join(",", s.Run.Owned.Select(o => $"{o.Slot}:{o.Special.Data.Id}:{o.Special.Level}:{o.Special.Modifier?.Id}:{s.Run.LevelledOrder(o.Slot)}"));

	static readonly SaveData Sample = new SaveData
	{
		Run = new RunSave(4000000000u, "default-map", new[] { "r1-apprentice", "r2-oni" }, "r3-kappa", 730, new[]
		{
			new SlotSave("A", "spirit-wave", 2, "some-modifier", 1),
			new SlotSave("C", "foxfire", 1, null, 0),
		}) { MapFile = "default-map.json", MapFingerprint = "00ff00ff00ff00ff" },
		Unlocks = new[] { "zeta", "alpha" },
		Story = new StoryProgress(new[] { StoryProgress.Reveal, StoryProgress.Intro }),
	};

	// ---- the store (no Godot) ----

	[Test]
	public static void Store_RoundTrip_SameBytes() => WithTempFile(path =>
	{
		var store = new SaveStore(path);
		Assert.True(store.Save(Sample), "saved");
		Assert.True(!File.Exists(path + ".tmp") && !File.Exists(path + SaveStore.BackupSuffix), "no temp or backup file left behind");
		SaveLoad load = new SaveStore(path).Load();
		Assert.Equal(SaveLoadStatus.Loaded, load.Status, "status: " + string.Join("; ", load.Problems));
		Assert.Equal(0, load.Problems.Count, "no problems");
		var run = load.Data.Run!;
		Assert.True(run.Seed == 4000000000u && run.MapId == "default-map" && run.Current == "r3-kappa" && run.Health == 730, $"run back: {run}");
		Assert.True(run.Visited.SequenceEqual(new[] { "r1-apprentice", "r2-oni" }), "visited in order");
		Assert.True(run.Slots.SequenceEqual(Sample.Run!.Slots), "slots back");
		Assert.True(run.MapFile == "default-map.json" && run.MapFingerprint == "00ff00ff00ff00ff", "map file and fingerprint back");
		Assert.True(load.Data.Unlocks.SequenceEqual(new[] { "zeta", "alpha" }), "unlocks keep their award order (M1)");
		Assert.True(load.Data.Story.Seen.SequenceEqual(new[] { "intro", "reveal" }), "story flags back");
		string text = File.ReadAllText(path);
		Assert.Equal(text, SaveStore.Serialize(load.Data), "what was read writes the same bytes");
		Assert.Equal(text, SaveStore.Serialize(Sample), "the same data always writes the same text (no time stamp)");
		Assert.True(text.Contains("\"version\": 1") && text.Contains("\"seed\": 4000000000"), "versioned, readable file: " + text);
		Assert.True(store.Save(SaveData.Empty) && new SaveStore(path).Load() is { Status: SaveLoadStatus.Loaded, Data.Run: null }, "overwrites; an empty save has no run");
	});

	[Test]
	public static void Store_MissingFile_IsEmpty_AndLoadWritesNothing() => WithTempFile(path =>
	{
		SaveLoad load = new SaveStore(path).Load();
		Assert.Equal(SaveLoadStatus.Missing, load.Status, "status");
		Assert.True(load.Data.Run == null && load.Data.Unlocks.Count == 0 && load.Data.Story.Seen.Count == 0, "an empty save");
		Assert.True(!File.Exists(path), "loading does not create the file");
		string nested = Path.Combine(Path.GetDirectoryName(path)!, "not", "there", SaveStore.FileName);
		Assert.True(new SaveStore(nested).Save(Sample) && File.Exists(nested), "saving creates the directory");
	});

	[Test]
	public static void Store_CorruptFile_IsEmpty_AndIsBackedUpBeforeTheFirstWrite() => WithTempFile(path =>
	{
		foreach (string bad in new[] { "", "{ \"version\": 1, \"run\": ", "[1, 2]", "\"save\"", "not json at all" })
		{
			File.WriteAllText(path, bad);
			var store = new SaveStore(path);
			SaveLoad load = store.Load();
			Assert.Equal(SaveLoadStatus.Corrupt, load.Status, $"status for '{bad}'");
			Assert.True(load.Data.Run == null && load.Problems.Count > 0, "empty, with the reason");
			Assert.Equal(bad, File.ReadAllText(path), "loading leaves the file alone");
			Assert.True(store.Save(Sample), "the game can still save");
			Assert.Equal(bad, File.ReadAllText(path + SaveStore.BackupSuffix), "the unreadable file is kept as a backup");
			Assert.Equal(SaveLoadStatus.Loaded, new SaveStore(path).Load().Status, "and the new file is good");
			File.Delete(path + SaveStore.BackupSuffix);
			Assert.True(store.Save(SaveData.Empty) && !File.Exists(path + SaveStore.BackupSuffix), "only the first write backs up");
		}
	});

	[Test]
	public static void Store_Versions_OlderIsRead_NewerKeepsKnownFields_AndIsBackedUp() => WithTempFile(path =>
	{
		string current = SaveStore.Serialize(Sample);
		SaveLoad older = SaveStore.Parse(current.Replace("\"version\": 1,", ""));
		Assert.Equal(SaveLoadStatus.OlderVersion, older.Status, "no version = older");
		Assert.Equal(current, SaveStore.Serialize(older.Data), "an older file's fields are all kept and rewritten as the current version");
		Assert.Equal(SaveLoadStatus.OlderVersion, SaveStore.Parse(current.Replace("\"version\": 1", "\"version\": 0")).Status, "version 0");

		string newer = current.Replace("\"version\": 1", "\"version\": 7, \"from_the_future\": { \"x\": [1] }");
		File.WriteAllText(path, newer);
		var store = new SaveStore(path);
		SaveLoad load = store.Load();
		Assert.Equal(SaveLoadStatus.NewerVersion, load.Status, "newer");
		Assert.True(load.Problems[0].Contains("version 7"), "says which version");
		Assert.Equal(current, SaveStore.Serialize(load.Data), "the fields this build knows are kept");
		Assert.True(store.Save(load.Data), "saved over it");
		Assert.Equal(newer, File.ReadAllText(path + SaveStore.BackupSuffix), "the newer build's file survives as a backup");
	});

	[Test]
	public static void Store_FieldsAddedLater_AreIgnored_FieldsMissing_TakeDefaults()
	{
		// Other tickets add run fields (coins, a copy lock, a used heal): a file with them loads here, a file without them loads there.
		string extra = """
			{ "version": 1, "coins": 75,
			  "run": { "seed": 9, "map": "default-map", "visited": [], "health": 1000, "coins": 50, "copy_lock": { "slot": "A" },
			           "slots": [ { "slot": "A", "special": "spirit-wave", "level": 1, "borrowed": true } ] },
			  "unlocks": [], "story": { "seen": [], "tanuki": 2 }, "carry": { "special": "foxfire" } }
			""";
		SaveLoad load = SaveStore.Parse(extra);
		Assert.Equal(SaveLoadStatus.Loaded, load.Status, "unknown fields are not a problem: " + string.Join("; ", load.Problems));
		var run = load.Data.Run!;
		Assert.True(run.Seed == 9 && run.Current == null && run.MapFile == null && run.MapFingerprint == null, "optional run fields default when missing");
		Assert.True(run.Slots.Single() == new SlotSave("A", "spirit-wave", 1, null, 0), "optional slot fields default when missing");

		SaveLoad bare = SaveStore.Parse("{ \"version\": 1 }");
		Assert.Equal(SaveLoadStatus.Partial, bare.Status, "sections missing");
		Assert.True(bare.Data.Run == null && bare.Data.Unlocks.Count == 0 && bare.Data.Story.Seen.Count == 0, "every missing section is its default");
	}

	[Test]
	public static void Store_ABrokenRun_IsDroppedAlone()
	{
		string good = SaveStore.Serialize(Sample);
		foreach (var (from, to) in new[]
		{
			("\"seed\": 4000000000", "\"seed\": -1"),
			("\"seed\": 4000000000", "\"seed\": 1.5"),
			("\"seed\": 4000000000", "\"seed\": \"x\""),
			("\"map\": \"default-map\"", "\"map\": 3"),
			("\"health\": 730", "\"health\": \"lots\""),
			("\"health\": 730", "\"hp\": 730"),
			("\"visited\": [", "\"visited\": [ 4,"),
			("\"slots\": [", "\"slots\": [ \"A\","),
			("\"level\": 2", "\"level\": null"),
			("\"special\": \"foxfire\"", "\"special\": \"\""),
		})
		{
			Assert.True(good.Contains(from), "fixture has " + from);
			SaveLoad load = SaveStore.Parse(good.Replace(from, to));
			Assert.Equal(SaveLoadStatus.Partial, load.Status, to);
			Assert.True(load.Data.Run == null && load.Problems.Any(p => p.StartsWith("run:")), $"{to}: the run is dropped with a reason");
			Assert.True(load.Data.Unlocks.SequenceEqual(Sample.Unlocks) && load.Data.Story == Sample.Story, $"{to}: unlocks and story are kept");
		}
		int at = good.IndexOf("\"run\": {", StringComparison.Ordinal), end = good.IndexOf("\"unlocks\"", StringComparison.Ordinal);
		SaveLoad notObject = SaveStore.Parse(good[..at] + "\"run\": 5,\n" + good[end..]);
		Assert.True(notObject.Status == SaveLoadStatus.Partial && notObject.Data.Run == null && notObject.Data.Story == Sample.Story, "a run that is not an object");
	}

	[Test]
	public static void Store_SaveFailure_IsReported_NotThrown() => WithTempFile(path =>
	{
		string blocked = Path.Combine(Path.GetDirectoryName(path)!, "is-a-file");
		File.WriteAllText(blocked, "x");
		var store = new SaveStore(Path.Combine(blocked, SaveStore.FileName));
		Assert.True(!store.Save(Sample) && !string.IsNullOrEmpty(store.LastSaveError), "a refused write returns false with the reason");
		Directory.CreateDirectory(path); // the save path is a directory
		var dirStore = new SaveStore(path);
		Assert.True(dirStore.Load().Status is SaveLoadStatus.Missing or SaveLoadStatus.Corrupt, "loading a directory does not throw");
		Assert.True(!dirStore.Save(Sample) && dirStore.LastSaveError != null, "saving onto a directory fails cleanly");
	});

	// ---- M4: story progress on its own flag ----

	[Test]
	public static void Story_IsIndependentOfUnlocks_M4()
	{
		SaveData d = SaveData.Empty.WithStory(StoryProgress.FirstLoss).WithUnlock("b").WithUnlock("a").WithUnlock("b");
		Assert.True(d.Story.Has(StoryProgress.FirstLoss) && !d.Story.Has(StoryProgress.Reveal), "one flag set");
		Assert.True(d.Unlocks.SequenceEqual(new[] { "b", "a" }), "unlocks: award order, no repeats");
		Assert.True(ReferenceEquals(d.WithStory(StoryProgress.FirstLoss), d) || d.WithStory(StoryProgress.FirstLoss).Story == d.Story, "setting a flag twice changes nothing");

		SaveData moreStory = d.WithStory(StoryProgress.Reveal);
		Assert.True(moreStory.Unlocks.SequenceEqual(d.Unlocks), "a story flag does not touch unlocks");
		SaveData moreUnlocks = d.WithUnlock("c");
		Assert.True(moreUnlocks.Story == d.Story, "an unlock does not touch story progress");
		Assert.True((d with { Unlocks = Array.Empty<string>() }).Story.Has(StoryProgress.FirstLoss), "story survives with no unlocks at all");
		Assert.True((d with { Story = StoryProgress.None }).Unlocks.Count == 2, "unlocks survive with no story progress");
		Assert.True(d.WithRun(null).Story == d.Story && d.WithRun(Sample.Run).Unlocks.SequenceEqual(d.Unlocks), "the run section touches neither");

		// In the file: each section is read on its own, so a lost or broken one never takes the other with it.
		string text = SaveStore.Serialize(d);
		Assert.True(text.Contains("\"story\": {") && text.Contains("\"unlocks\": ["), "two sections: " + text);
		SaveLoad noUnlocks = SaveStore.Parse(text.Replace("\"unlocks\": [", "\"unlocks\": 3, \"x\": ["));
		Assert.True(noUnlocks.Status == SaveLoadStatus.Partial && noUnlocks.Data.Unlocks.Count == 0 && noUnlocks.Data.Story == d.Story, "broken unlocks: story progress is kept");
		SaveLoad noStory = SaveStore.Parse(text.Replace("\"story\": {", "\"story\": null, \"x\": {"));
		Assert.True(noStory.Status == SaveLoadStatus.Partial && noStory.Data.Story.Seen.Count == 0 && noStory.Data.Unlocks.SequenceEqual(d.Unlocks), "broken story: unlocks are kept");
		SaveLoad junk = SaveStore.Parse("{ \"version\": 1, \"unlocks\": [\"a\", 4, \"\", \"a\", \"b\"], \"story\": { \"seen\": [\"x\", null, \"intro\", \"x\"] } }");
		Assert.True(junk.Data.Unlocks.SequenceEqual(new[] { "a", "b" }) && junk.Data.Story.Seen.SequenceEqual(new[] { "intro", "x" }), "bad entries are skipped, the rest kept");
		Assert.Equal(SaveLoadStatus.Partial, junk.Status, "and reported");
	}

	// ---- a run captured and restored ----

	[Test]
	public static void Run_CaptureRestore_SameHash_AndTheSameRestOfTheRun() => WithFixtures(() =>
	{
		// Reference: one run played straight through, hash after every node.
		var straight = NewSession(1234);
		var hashes = new List<ulong>();
		int[] health = { 900, 640, 880, 420, 700, 310, 560, 240, 90 };
		for (int i = 0; !straight.IsOver; i++) { PlayNode(straight, health[i]); hashes.Add(straight.StateHash()); }
		Assert.True(hashes.Count == straight.Steps && straight.Ended!.Won, "the reference run walked every row");

		// The same run, saved to text and restored after every node (and once inside each node).
		var live = NewSession(1234);
		ulong atStart = live.StateHash();
		RunSession fresh = SaveStore.Parse(SaveStore.Serialize(SaveData.Empty.WithRun(RunSave.Capture(live)))).Data.Run!.Restore(FightScene.LoadAbilityPool(), DefaultMap(), Table());
		Assert.True(fresh.StateHash() == atStart && fresh.Step == 0 && fresh.Reachable().SequenceEqual(live.Reachable()), "a run saved before its first node restores with the same hash");
		bool anyLevelled = false, anyModifier = false;
		for (int i = 0; i < hashes.Count - 1; i++)
		{
			PlayNode(live, health[i]);
			string text = SaveStore.Serialize(SaveData.Empty.WithRun(RunSave.Capture(live, FileMapSource.DefaultFile)));
			SaveLoad load = SaveStore.Parse(text);
			Assert.Equal(SaveLoadStatus.Loaded, load.Status, $"node {i + 1}: " + string.Join("; ", load.Problems));
			var notes = new List<string>();
			RunSession back = load.Data.Run!.Restore(FightScene.LoadAbilityPool(), DefaultMap(), Table(), notes);
			Assert.Equal(0, notes.Count, "nothing repaired");
			Assert.Equal(hashes[i], back.StateHash(), $"node {i + 1}: the restored run has the live run's hash");
			Assert.True(back.Seed == 1234 && back.Run.Health == live.Run.Health && back.Step == live.Step && back.Node == null, $"node {i + 1}: seed, health, step");
			Assert.Equal(Slots(live), Slots(back), $"node {i + 1}: slots, levels, modifiers and T3 order");
			Assert.True(back.Reachable().SequenceEqual(live.Reachable()), $"node {i + 1}: the same routes are open");
			Assert.Equal(text, SaveStore.Serialize(SaveData.Empty.WithRun(RunSave.Capture(back, FileMapSource.DefaultFile))), $"node {i + 1}: saving the restored run writes the same file");
			anyLevelled |= back.Run.Owned.Any(o => o.Special.Level > 1);
			anyModifier |= back.Run.Owned.Any(o => o.Special.Modifier != null);

			// Saved inside the next node: comes back in that node, at its start.
			var inside = load.Data.Run.Restore(FightScene.LoadAbilityPool(), DefaultMap(), Table());
			inside.Enter(inside.Reachable()[0]);
			var again = SaveStore.Parse(SaveStore.Serialize(SaveData.Empty.WithRun(RunSave.Capture(inside)))).Data.Run!.Restore(FightScene.LoadAbilityPool(), DefaultMap(), Table());
			Assert.True(again.Node?.NodeId == inside.Node!.NodeId && again.Entered!.Scene == inside.Entered!.Scene && again.StateHash() == inside.StateHash(), $"node {i + 1}: restored inside the next node");

			live = back; // carry on from the restored run: the rest must match the reference exactly (same drafts from the seed)
		}
		PlayNode(live, health[hashes.Count - 1]);
		Assert.True(live.Ended is { Won: true } && live.StateHash() == hashes[^1], "a run restored after every node ends exactly like the one played straight through");
		Assert.True(anyLevelled || anyModifier, "the walk covered a levelled special or a modifier");
	});

	[Test]
	public static void Run_Capture_OnlyAtANodeBoundary() => WithFixtures(() =>
	{
		var s = NewSession(5);
		s.Enter(s.Reachable()[0]);
		var draft = s.WinFight(800);
		Assert.True(draft != null, "the first node offers cards");
		Throws<InvalidOperationException>(() => RunSave.Capture(s), "an open offer is not saved");
		var lost = NewSession(5);
		lost.Enter(lost.Reachable()[0]);
		lost.LoseFight();
		Throws<InvalidOperationException>(() => RunSave.Capture(lost), "a run that is over is not saved");
	});

	[Test]
	public static void Run_Restore_RefusesWhatTheRulesDoNotAllow() => WithFixtures(() =>
	{
		var pool = FightScene.LoadAbilityPool();
		var s = NewSession(8);
		PlayNode(s, 700);
		RunSave good = RunSave.Capture(s);
		Assert.True(good.Restore(pool, DefaultMap(), Table()).StateHash() == s.StateHash(), "the untouched save restores");
		SlotSave first = good.Slots[0];
		RunSave With(SlotSave slot) => good with { Slots = good.Slots.Skip(1).Prepend(slot).ToArray() };

		Throws<FormatException>(() => (good with { MapId = "other-map" }).Restore(pool, DefaultMap(), Table()), "another map");
		Throws<FormatException>(() => (good with { MapFingerprint = "0000000000000001" }).Restore(pool, DefaultMap(), Table()), "the map changed");
		Throws<FormatException>(() => (good with { Visited = new[] { "r2-oni" } }).Restore(pool, DefaultMap(), Table()), "a walk the map does not allow (R1)");
		Throws<FormatException>(() => (good with { Current = "r8-elder-nowhere" }).Restore(pool, DefaultMap(), Table()), "an unknown current node");
		foreach (int health in new[] { 0, -5, RunState.MaxHealth + 1 })
			Throws<FormatException>(() => (good with { Health = health }).Restore(pool, DefaultMap(), Table()), $"health {health} (C1)");
		Throws<FormatException>(() => With(first with { Special = "no-such-special" }).Restore(pool, DefaultMap(), Table()), "an unknown special");
		Throws<FormatException>(() => With(first with { Modifier = "no-such-modifier" }).Restore(pool, DefaultMap(), Table()), "an unknown modifier");
		Throws<FormatException>(() => With(first with { Level = 0 }).Restore(pool, DefaultMap(), Table()), "level 0");
		Throws<FormatException>(() => With(first with { Level = 4 }).Restore(pool, DefaultMap(), Table()), "level 4 (A3)");
		foreach (string slot in new[] { "E", "None", "0", "1", "", "a", " A", " a ", "A,B", "A " })
			Throws<FormatException>(() => With(first with { Slot = slot }).Restore(pool, DefaultMap(), Table()), $"slot '{slot}'");
		string wrong = first.Slot == "A" ? "B" : "A";
		Throws<FormatException>(() => With(first with { Slot = wrong }).Restore(pool, DefaultMap(), Table()), "a special outside its own slot (A3)");
		Throws<FormatException>(() => (good with { Slots = good.Slots.Append(first).ToArray() }).Restore(pool, DefaultMap(), Table()), "one slot twice");

		// A1 / T5: a run always owns a special, so a save with none is not a run.
		Throws<FormatException>(() => (good with { Slots = Array.Empty<SlotSave>() }).Restore(pool, DefaultMap(), Table()), "no special in any slot (A1, T5)");
		Assert.True(SaveStore.Parse(SaveStore.Serialize(SaveData.Empty.WithRun(good with { Slots = Array.Empty<SlotSave>() }))).Data.Run is { } empty && empty.Slots.Count == 0, "the file reads, the restore refuses");

		// A walk already at the end of the map is a won run: nothing to pick, so nothing to continue.
		var walked = NewSession(8);
		var all = new List<string>();
		while (!walked.IsOver) { all.Add(walked.Reachable()[0]); PlayNode(walked, 600); }
		Assert.True(all.Count == walked.Steps && all[^1] == "boss-tanuki", "walked to the boss node");
		Throws<FormatException>(() => (good with { Visited = all.ToArray(), Current = all[^1] }).Restore(pool, DefaultMap(), Table()), "a finished walk");
		var atBoss = (good with { Visited = all.Take(all.Count - 1).ToArray(), Current = all[^1] }).Restore(pool, DefaultMap(), Table());
		Assert.True(atBoss.Node?.NodeId == "boss-tanuki" && !atBoss.IsOver, "inside the last node is still a run in progress");

		// A missing or impossible T3 order is repaired (lower slot = more recent), never a reason to lose the run.
		var levelled = RunState.NewRun(pool, DefaultMap());
		var slots = levelled.Owned.Where(o => o.Special.Data.MaxLevel > 1).Select(o => o.Slot).Take(2).ToArray();
		Assert.True(slots.Length == 2, "two starters can level");
		levelled.LevelUp(slots[0]);
		levelled.LevelUp(slots[1]);
		RunSave saved = RunSave.Capture(new RunSession(levelled, pool, Table(), 3));
		var notes = new List<string>();
		RunSession repaired = (saved with { Slots = saved.Slots.Select(x => x with { Levelled = 0 }).ToArray() }).Restore(pool, DefaultMap(), Table(), notes);
		Assert.True(notes.Count == 1, "the repair is reported");
		Assert.True(repaired.Run.LevelledOrder(slots[0]) > repaired.Run.LevelledOrder(slots[1]) && repaired.Run.LevelledOrder(slots[1]) > 0, "lower slot taken as more recent (T3's last tie-break)");
		Assert.True(saved.Restore(pool, DefaultMap(), Table()).Run.LevelledOrder(slots[1]) > saved.Restore(pool, DefaultMap(), Table()).Run.LevelledOrder(slots[0]), "a good order is restored as saved");
	});

	// ---- the boot scene ----

	static void WithBoot(Node runner, string? saveFile, Action<GameFlow> body, uint seed = 77)
	{
		var saved = FightScene.Sources;
		var savedRun = FightScene.Run;
		var savedFoe = FightScene.Opponent;
		FightScene.Sources = FightScene.FixtureSources;
		FightScene.Run = null;
		MenuSettings.Reset();
		var flow = GD.Load<PackedScene>("res://scenes/boot.tscn").Instantiate<GameFlow>();
		flow.QuitAction = () => { };
		flow.FixedRunSeed = seed;
		flow.SaveFile = saveFile;
		flow.ConfigureFight = f => f.ExternalDrive = true;
		try { runner.AddChild(flow); body(flow); }
		finally
		{
			runner.RemoveChild(flow);
			flow.QueueFree();
			MenuSettings.Reset();
			FightScene.Sources = saved;
			FightScene.Run = savedRun;
			FightScene.Opponent = savedFoe;
		}
	}

	static void Press(Button b) => b.EmitSignal(BaseButton.SignalName.Pressed);

	/// <summary>Finishes the entered node through the session (the fight scene's own win path is RunFlowTests' subject).</summary>
	static void WinEnteredNode(GameFlow flow, int healthLeft)
	{
		var s = flow.Session!;
		if (s.WinFight(healthLeft) is { } draft)
		{
			var card = draft.Offer.Cards[0];
			Assert.True(draft.Pick(0, card.Targets.Count > 0 ? card.Targets[0].Slot : null).Ok, "picked");
		}
		Assert.True(s.CompleteNode().Ok, "completed");
	}

	[Test]
	public static void Boot_SavesAtNodeBoundaries_ContinueRestoresTheRun(Node runner) => WithTempFile(path =>
	{
		ulong hash = 0;
		string slots = "", fileAtQuit = "";
		WithBoot(runner, path, flow =>
		{
			Assert.True(flow.SaveLoaded!.Status == SaveLoadStatus.Missing && !File.Exists(path), "first launch: no file, none created by loading");
			Assert.True(flow.Title.ContinueButton.Disabled && flow.Title.DisabledReason(TitleEntry.Continue) == ScreenRouter.NoSaveReason, "no saved run: Continue is off with its reason");
			Press(flow.Title.StartButton);
			Assert.True(!File.Exists(path), "nothing is saved before a run exists");
			Press(flow.ControlSelect.Start);
			var session = flow.Session!;
			SaveLoad started = new SaveStore(path).Load();
			Assert.True(started.Status == SaveLoadStatus.Loaded && started.Data.Run is { Seed: 77, Health: RunState.MaxHealth, Current: null } r0 && r0.Visited.Count == 0, "the new run is saved at once");

			Assert.True(flow.ChooseNode("r1-apprentice").Ok && flow.Router.Current == Screen.Fight, "row 1 entered");
			Assert.True(new SaveStore(path).Load().Data.Run is { Current: "r1-apprentice", Health: RunState.MaxHealth } r1 && r1.Visited.Count == 0, "saved on entering the node");
			string entered = File.ReadAllText(path);
			for (int t = 0; t < 6; t++) flow.Fight!.Step(FighterInput.None, new FighterInput(t % 2 == 0 ? InputBits.DebugStrike : InputBits.None));
			Assert.True(flow.Fight!.Match.P1.Health < RunState.MaxHealth && !flow.Session!.IsOver, "Ryo took damage in the fight");
			Assert.Equal(entered, File.ReadAllText(path), "nothing is written during a fight");

			WinEnteredNode(flow, 640);
			Assert.Equal(Screen.Map, flow.Router.Current, "back on the map");
			var done = new SaveStore(path).Load().Data.Run!;
			Assert.True(done.Current == "r1-apprentice" && done.Visited.SequenceEqual(new[] { "r1-apprentice" }) && done.Health == session.Run.Health, "saved on completing the node, with the carried health (C1, R3)");
			hash = session.StateHash();
			slots = Slots(session);
			fileAtQuit = File.ReadAllText(path);

			Assert.True(flow.ReturnToTitle() && flow.Session == null, "quit to the title");
			Assert.Equal(fileAtQuit, File.ReadAllText(path), "the run waits in the file");
			Assert.True(!flow.Title.ContinueButton.Disabled && flow.Router.Entry(TitleEntry.Continue).Enabled, "Continue is lit on the title");
		});

		// A new process: boot again on the same file.
		WithBoot(runner, path, flow =>
		{
			Assert.Equal(SaveLoadStatus.Loaded, flow.SaveLoaded!.Status, "the file loads");
			Assert.True(flow.Session == null && flow.Router.Current == Screen.Title, "boots to the title, no run started");
			Assert.True(!flow.Title.ContinueButton.Disabled && flow.Title.ContinueButton.FocusMode == Control.FocusModeEnum.All, "Continue is lit");
			Assert.Equal(fileAtQuit, File.ReadAllText(path), "booting writes nothing");
			Press(flow.Title.ContinueButton);
			Assert.Equal(Screen.Map, flow.Router.Current, "Continue opens the map, skipping control select");
			var session = flow.Session!;
			Assert.True(session.Seed == 77 && session.StateHash() == hash && Slots(session) == slots, "the same run: seed, hash, slots");
			Assert.True(flow.Map.Visible && flow.Map.ReachableIds.SequenceEqual(session.Reachable()) && session.Reachable().Count > 0, "the map shows row 2 open");
			Assert.Equal(fileAtQuit, File.ReadAllText(path), "continuing writes nothing new");

			// Quit inside the next fight: Continue goes back into that node, from its start.
			string next = session.Reachable()[0];
			Assert.True(flow.ChooseNode(next).Ok, "row 2 entered");
			int healthAtEntry = session.Run.Health;
			for (int t = 0; t < 6; t++) flow.Fight!.Step(FighterInput.None, new FighterInput(t % 2 == 0 ? InputBits.DebugStrike : InputBits.None));
			Assert.True(flow.ReturnToTitle(), "quit mid-fight");
			Press(flow.Title.ContinueButton);
			Assert.True(flow.Router.Current == Screen.Fight && flow.Session!.Node?.NodeId == next, "Continue re-enters the node");
			Assert.True(flow.Fight!.Match.P1.Health == healthAtEntry && flow.Fight.Match.Tick == 0, "the fight starts over with the health Ryo entered with");

			// Losing ends the run: nothing is left to continue, story and unlocks stay.
			flow.Write(flow.Saved.WithStory(StoryProgress.FirstLoss).WithUnlock("some-ability"));
			Assert.True(flow.Session!.LoseFight().Ok, "lost");
			SaveLoad after = new SaveStore(path).Load();
			Assert.True(after.Status == SaveLoadStatus.Loaded && after.Data.Run == null, "the ended run is removed from the file");
			Assert.True(after.Data.Story.Has(StoryProgress.FirstLoss) && after.Data.Unlocks.SequenceEqual(new[] { "some-ability" }), "story progress and unlocks outlive the run");
			Assert.True(flow.ReturnToTitle() && flow.Title.ContinueButton.Disabled, "back on the title, Continue is off again");
			Assert.True(!flow.ContinueRun() && flow.Router.Current == Screen.Title, "and cannot be forced");
		});
	});

	[Test]
	public static void Boot_AWonRun_LeavesNothingToContinue_ANewRunReplacesTheSavedOne(Node runner) => WithTempFile(path =>
	{
		WithBoot(runner, path, flow =>
		{
			Press(flow.Title.StartButton);
			Press(flow.ControlSelect.Start);
			var first = flow.Session!;
			Assert.True(flow.ChooseNode(first.Reachable()[0]).Ok, "entered");
			WinEnteredNode(flow, 500);
			Assert.True(new SaveStore(path).Load().Data.Run!.Health == first.Run.Health, "saved after row 1");
			flow.ReturnToTitle();

			Press(flow.Title.StartButton); // Start with a run saved: a new run takes its place
			Press(flow.ControlSelect.Start);
			Assert.True(!ReferenceEquals(flow.Session, first), "a new session");
			Assert.True(new SaveStore(path).Load().Data.Run is { Health: RunState.MaxHealth } fresh && fresh.Visited.Count == 0, "the file holds the new run");

			while (!flow.Session!.IsOver)
			{
				Assert.True(flow.ChooseNode(flow.Session.Reachable()[0]).Ok, "entered");
				WinEnteredNode(flow, 600);
			}
			Assert.True(flow.Session.Ended!.Won, "the run is won");
			Assert.True(new SaveStore(path).Load() is { Status: SaveLoadStatus.Loaded, Data.Run: null }, "a won run is removed from the file");
		}, seed: 9);
	});

	[Test]
	public static void Boot_ASavedRunThisBuildCannotHold_IsNotContinued_AndNotDestroyed(Node runner) => WithTempFile(path =>
	{
		var broken = Sample with { Run = Sample.Run! with { Visited = Array.Empty<string>(), Current = null, MapFingerprint = null, Slots = new[] { new SlotSave("A", "no-such-special", 1, null, 0) } } };
		File.WriteAllText(path, SaveStore.Serialize(broken));
		string before = File.ReadAllText(path);
		WithBoot(runner, path, flow =>
		{
			Assert.True(flow.Saved.Run != null, "the file parses");
			Assert.True(flow.Title.ContinueButton.Disabled, "Continue is off");
			Assert.True(flow.Title.DisabledReason(TitleEntry.Continue).Contains("no-such-special"), "with the reason: " + flow.Title.DisabledReason(TitleEntry.Continue));
			Press(flow.Title.ContinueButton);
			Assert.True(flow.Router.Current == Screen.Title && flow.Session == null, "nothing starts");
			Assert.Equal(before, File.ReadAllText(path), "the file is left as it was");
			Assert.True(flow.Saved.Story == Sample.Story && flow.Saved.Unlocks.SequenceEqual(Sample.Unlocks), "story and unlocks loaded all the same");
		});
	});

	// ---- isolation ----

	[Test]
	public static void Isolation_NoSaveFile_UnlessTheBootIsTheRunningGame(Node runner)
	{
		string player = GameFlow.UserSavePath;
		bool existed = File.Exists(player);
		DateTime stamp = existed ? File.GetLastWriteTimeUtc(player) : default;
		WithBoot(runner, null, flow =>
		{
			Assert.True(flow.Saves == null && flow.SaveLoaded == null, "an instanced boot scene attaches no save file");
			Press(flow.Title.StartButton);
			Press(flow.ControlSelect.Start);
			Assert.True(flow.ChooseNode(flow.Session!.Reachable()[0]).Ok, "a run plays without a save file");
			WinEnteredNode(flow, 700);
			flow.ReturnToTitle();
			Assert.True(flow.Title.ContinueButton.Disabled && flow.Saved.Run == null, "and leaves nothing to continue");
		});
		Assert.Equal(existed, File.Exists(player), "the player's save file was not created or removed");
		if (existed) Assert.Equal(stamp, File.GetLastWriteTimeUtc(player), "the player's save file was not written");
	}

	[Test]
	public static void Isolation_SimAiAndReplayCode_DoNotReferenceTheSave()
	{
		string scripts = ProjectSettings.GlobalizePath("res://scripts");
		int files = 0;
		foreach (string dir in new[] { "Sim", "Ai", "Replay" })
			foreach (string file in Directory.GetFiles(Path.Combine(scripts, dir), "*.cs", SearchOption.AllDirectories))
			{
				files++;
				string text = File.ReadAllText(file);
				Assert.True(!text.Contains("SaveStore") && !text.Contains("SaveData") && !text.Contains("RunSave") && !text.Contains("YokaiFighters.SaveGame") && !text.Contains("user://"),
					$"{Path.GetFileName(file)} must not read the save or user files");
			}
		Assert.True(files > 20, $"scanned the sim sources ({files} files)");
		foreach (string file in Directory.GetFiles(Path.Combine(scripts, "SaveGame"), "*.cs"))
		{
			string text = File.ReadAllText(file);
			foreach (string banned in Clocks.Concat(new[] { "Random", "Guid", "using Godot", "Godot." }))
				Assert.True(!text.Contains(banned), $"{Path.GetFileName(file)}: '{banned}': no wall clock, no unseeded randomness and no engine in the save (F3)");
		}
		// The save points live in the boot flow. It is a Godot node and picks a new run's seed with Random.Shared
		// (a designer decision, YOK-48: the seed is then run state and is what gets saved), so only clocks are barred there.
		string flowText = File.ReadAllText(Path.Combine(scripts, "Flow", "GameFlow.cs"));
		foreach (string banned in Clocks)
			Assert.True(!flowText.Contains(banned), $"GameFlow.cs: '{banned}': no clock decides when or what is saved (F3)");
		Assert.Equal(1, flowText.Split("Random").Length - 1, "GameFlow.cs: the only randomness is a new run's seed");
	}

	static readonly string[] Clocks = { "DateTime", "Stopwatch", "GetTicksMsec", "GetTicksUsec", "TickCount", "TimeProvider", "GetUnixTime", "GetTime" };

	// ---- code-critic findings ----

	[Test]
	public static void Store_AFileTheDiskWillNotOpen_IsNeverWrittenOver() => WithTempFile(path =>
	{
		if (OperatingSystem.IsWindows()) return; // no chmod; a sharing violation takes the same path
		string good = SaveStore.Serialize(SaveData.Empty.WithUnlock("oni-quake").WithUnlock("kappa-dive").WithStory(StoryProgress.Intro).WithStory(StoryProgress.FirstLoss));
		File.WriteAllText(path, good);
		File.SetUnixFileMode(path, UnixFileMode.None);
		try
		{
			var store = new SaveStore(path);
			SaveLoad load = store.Load();
			Assert.True(load.Status == SaveLoadStatus.Corrupt && load.Data.Unlocks.Count == 0 && load.Problems[0].StartsWith("cannot read"), "an unopenable file loads as empty, with the reason");
			Assert.True(store.WriteBlocked, "the store knows it could not read the file");
			Assert.True(!store.Save(Sample) && store.LastSaveError!.Contains("left untouched"), "nothing is written over it: " + store.LastSaveError);
			Assert.True(!store.Save(SaveData.Empty), "not on a second try either");
			Assert.True(!File.Exists(path + SaveStore.BackupSuffix) && !File.Exists(path + ".tmp"), "no backup and no temp file: the file itself is the copy");
			File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
			Assert.Equal(good, File.ReadAllText(path), "the file is intact");
			SaveLoad again = store.Load();
			Assert.True(again.Status == SaveLoadStatus.Loaded && again.Data.Unlocks.Count == 2 && again.Data.Story.Seen.Count == 2, "once readable, the unlocks and story are all there (M1, M4)");
			Assert.True(!store.WriteBlocked && store.Save(again.Data.WithUnlock("third")), "and saving works again");
		}
		finally { try { File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite); } catch (Exception) { } }
	});

	[Test]
	public static void Store_AnyLoadThatLostSomething_IsBackedUp_AndBackupsAreNeverReplaced() => WithTempFile(path =>
	{
		string bak = path + SaveStore.BackupSuffix;
		string partial = "{\"version\":1,\"run\":null,\"unlocks\":{\"0\":\"oni-quake\"},\"story\":{\"seen\":[\"intro\"]}}";
		File.WriteAllText(path, partial);
		var store = new SaveStore(path);
		SaveLoad load = store.Load();
		Assert.True(load.Status == SaveLoadStatus.Partial && load.Data.Unlocks.Count == 0 && load.Data.Story.Has(StoryProgress.Intro), "unlocks of the wrong shape: partial, story kept");
		Assert.True(store.Save(load.Data), "saved");
		Assert.Equal(partial, File.ReadAllText(bak), "the partly read file is kept before it is written over");

		// A later bad file gets its own backup name: the first backup is not replaced.
		File.WriteAllText(path, "");
		store = new SaveStore(path);
		Assert.Equal(SaveLoadStatus.Corrupt, store.Load().Status, "an empty file");
		Assert.True(store.Save(Sample), "saved");
		Assert.Equal(partial, File.ReadAllText(bak), "the earlier backup is untouched");
		Assert.Equal("", File.ReadAllText(bak + ".1"), "the new one is numbered");
		string older = SaveStore.Serialize(Sample).Replace("\"version\": 1,", "").Replace("\"unlocks\": [", "\"unlocks\": 5, \"x\": [");
		File.WriteAllText(path, older);
		store = new SaveStore(path);
		Assert.Equal(SaveLoadStatus.OlderVersion, store.Load().Status, "an older file with a broken section");
		Assert.True(store.Save(Sample) && File.ReadAllText(bak + ".2") == older, "is backed up too, under the next name");

		// Nothing lost, nothing backed up: a clean older file, a current file, and a second load of a good file.
		foreach (string clean in new[] { SaveStore.Serialize(Sample).Replace("\"version\": 1,", ""), SaveStore.Serialize(Sample) })
		{
			File.WriteAllText(path, clean);
			store = new SaveStore(path);
			Assert.True(!SaveStore.LostSomething(store.Load()) && store.Save(SaveData.Empty), "a fully read file is simply written over");
			Assert.True(!File.Exists(bak + ".3"), "with no backup");
		}
		File.WriteAllText(path, "nope");
		store = new SaveStore(path);
		store.Load();
		File.WriteAllText(path, SaveStore.Serialize(Sample));
		Assert.True(store.Load().Status == SaveLoadStatus.Loaded && store.Save(Sample) && !File.Exists(bak + ".3"), "a later good load clears the pending backup");
	});

	[Test]
	public static void Store_AVersionThisBuildCannotRead_IsNotTakenForAnOlderFile() => WithTempFile(path =>
	{
		string body = ", \"run\": null, \"unlocks\": [\"a\"], \"story\": { \"seen\": [] } }";
		foreach (string version in new[] { "\"1\"", "1.0", "1e0", "2147483648", "-3", "null", "true", "[1]", "1.5" })
		{
			string text = "{ \"version\": " + version + body;
			SaveLoad parsed = SaveStore.Parse(text);
			Assert.Equal(SaveLoadStatus.NewerVersion, parsed.Status, $"version {version}");
			Assert.True(parsed.Problems[0].Contains(version) && parsed.Data.Unlocks.SequenceEqual(new[] { "a" }), $"version {version}: named, known fields kept");
			File.WriteAllText(path, text);
			var store = new SaveStore(path);
			store.Load();
			foreach (string old in Directory.GetFiles(Path.GetDirectoryName(path)!, "*.bak*")) File.Delete(old);
			Assert.True(store.Save(SaveData.Empty) && File.ReadAllText(path + SaveStore.BackupSuffix) == text, $"version {version}: backed up before the first write");
		}
		Assert.Equal(SaveLoadStatus.NewerVersion, SaveStore.Parse("{ \"version\": 1, \"version\": 9" + body).Status, "a repeated version: the last one counts");
		Assert.Equal(SaveLoadStatus.Loaded, SaveStore.Parse("\uFEFF{ \"version\": 1" + body).Status, "a byte-order mark is not corruption");
		Assert.Equal(SaveLoadStatus.OlderVersion, SaveStore.Parse("{ \"version\": 0" + body).Status, "version 0 is older");
	});

	[Test]
	public static void MapFile_FromTheSave_IsOnlyEverABareJsonFileName()
	{
		foreach (string ok in new[] { "default-map.json", "demo-map.json", "My Map.JSON" })
			Assert.True(RunSave.IsMapFileName(ok), ok);
		foreach (string? bad in new[] { null, "", " ", "..", ".", "../moves", "../map/default-map.json", "/etc/hosts", "/etc/hosts.json", "sub/default-map.json", "sub\\default-map.json", "C:\\maps\\a.json", "C:a.json", "default-map", "default-map.txt", ".json", "..json", " default-map.json", "default-map.json " })
			Assert.True(!RunSave.IsMapFileName(bad), $"'{bad}' must be refused");
	}

	[Test]
	public static void Boot_ASaveNamingAnotherPath_GreysContinue_AndTheBootStillFinishes(Node runner) => WithTempFile(path =>
	{
		RunSave run = null!;
		WithFixtures(() => run = RunSave.Capture(NewSession(77), FileMapSource.DefaultFile));
		string mapDir = Path.GetFullPath(MapDir);
		foreach (string mapFile in new[] { "..", "../moves", ".", "/etc/hosts", mapDir, Path.Combine(mapDir, "default-map.json"), "../map/default-map.json", "no-such-map.json", "node-scenes.json" })
		{
			File.WriteAllText(path, SaveStore.Serialize(Sample with { Run = run with { MapFile = mapFile } }));
			string before = File.ReadAllText(path);
			WithBoot(runner, path, flow =>
			{
				Assert.True(flow.Saved.Run?.MapFile == mapFile, $"'{mapFile}': the file parses");
				Assert.True(flow.Title.ContinueButton.Disabled && !flow.Router.Entry(TitleEntry.Continue).Enabled, $"'{mapFile}': Continue is off");
				string reason = flow.Title.DisabledReason(TitleEntry.Continue);
				Assert.True(reason.StartsWith("The saved run cannot be continued"), $"'{mapFile}': with a reason: {reason}");
				Assert.True(!flow.ContinueRun() && flow.Session == null && flow.Router.Current == Screen.Title, $"'{mapFile}': nothing starts");
				// _Ready ran to its end: Quit is wired, the scheme follows the settings, Start still works.
				int quits = 0;
				flow.QuitAction = () => quits++;
				Press(flow.Title.QuitButton);
				Assert.Equal(1, quits, $"'{mapFile}': Quit is wired");
				MenuSettings.SetScheme(ControlScheme.Kata);
				Assert.Equal(ControlScheme.Kata, flow.Router.Scheme, $"'{mapFile}': the settings still reach the router");
				Assert.Equal(before, File.ReadAllText(path), $"'{mapFile}': the file is left as it was");
				Assert.True(flow.ReturnToTitle() == false && flow.Title.ContinueButton.Disabled, "still off");
				Press(flow.Title.StartButton);
				Press(flow.ControlSelect.Start);
				Assert.True(flow.Session != null && flow.Router.Current == Screen.Map, $"'{mapFile}': a new run still starts");
				Assert.True(flow.ReturnToTitle() && !flow.Title.ContinueButton.Disabled, $"'{mapFile}': and that one can be continued");
			});
		}
		File.WriteAllText(path, SaveStore.Serialize(Sample with { Run = run }));
		WithBoot(runner, path, flow => Assert.True(!flow.Title.ContinueButton.Disabled, "the same run with its real map file is offered"));
	});

	[Test]
	public static void Boot_ASaveNoRunCanReach_IsNotOffered(Node runner) => WithTempFile(path =>
	{
		RunSave run = null!;
		var all = new List<string>();
		WithFixtures(() =>
		{
			run = RunSave.Capture(NewSession(77), FileMapSource.DefaultFile);
			var walked = NewSession(77);
			while (!walked.IsOver) { all.Add(walked.Reachable()[0]); PlayNode(walked, 600); }
		});
		foreach (var (what, bad) in new[]
		{
			("no special", run with { Slots = Array.Empty<SlotSave>() }),
			("a finished walk", run with { Visited = all.ToArray(), Current = all[^1] }),
		})
		{
			File.WriteAllText(path, SaveStore.Serialize(SaveData.Empty.WithRun(bad)));
			WithBoot(runner, path, flow =>
			{
				Assert.True(flow.Saved.Run != null && flow.Title.ContinueButton.Disabled, $"{what}: Continue is off");
				Assert.True(flow.Title.DisabledReason(TitleEntry.Continue).StartsWith("The saved run cannot be continued"), $"{what}: {flow.Title.DisabledReason(TitleEntry.Continue)}");
				Press(flow.Title.ContinueButton);
				Assert.True(flow.Session == null && flow.Router.Current == Screen.Title, $"{what}: the title stays");
			});
		}
	});

	[Test]
	public static void Boot_AnUnreadableSaveFile_SaysSo_AndARefusedRunEndWriteIsRetried(Node runner) => WithTempFile(path =>
	{
		File.WriteAllText(path, "{ not a save");
		WithBoot(runner, path, flow =>
		{
			string reason = flow.Title.DisabledReason(TitleEntry.Continue);
			Assert.True(flow.Title.ContinueButton.Disabled && reason != ScreenRouter.NoSaveReason && reason.Contains("could not be read"), "a corrupt file is not 'no saved run yet': " + reason);
			Press(flow.Title.StartButton);
			Press(flow.ControlSelect.Start);
			Assert.Equal("{ not a save", File.ReadAllText(path + SaveStore.BackupSuffix), "the corrupt file is kept when the new run is saved");
			Assert.True(flow.ChooseNode(flow.Session!.Reachable()[0]).Ok, "entered");

			// The disk refuses the write that removes the lost run: the file still holds it.
			string held = File.ReadAllText(path);
			string dir = Path.GetDirectoryName(path)!;
			if (OperatingSystem.IsWindows()) return;
			File.SetUnixFileMode(dir, UnixFileMode.UserRead | UnixFileMode.UserExecute);
			try
			{
				Assert.True(flow.Session.LoseFight().Ok, "lost");
				Assert.True(flow.Saved.Run == null, "the flow knows the run is over");
				Assert.Equal(held, File.ReadAllText(path), "but the write was refused: the file still holds the run");
				Assert.True(flow.ReturnToTitle() && flow.Title.ContinueButton.Disabled, "the title does not offer the ended run");
			}
			finally { File.SetUnixFileMode(dir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); }
			Assert.True(new SaveStore(path).Load().Data.Run != null, "still in the file");
			Press(flow.Title.SettingsButton); // any return to the title retries the write
			flow.Router.Back();
			Assert.True(new SaveStore(path).Load() is { Status: SaveLoadStatus.Loaded, Data.Run: null }, "the pending removal is written once the disk allows it");
			Assert.True(flow.Title.ContinueButton.Disabled, "Continue stays off");
		});
	});
}
