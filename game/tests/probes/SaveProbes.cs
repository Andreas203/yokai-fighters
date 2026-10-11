using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Flow;
using YokaiFighters.SaveGame;
using YokaiFighters.Sim;
using YokaiFighters.Ui;
using CardKind = YokaiFighters.Sim.CardKind;
using CardView = YokaiFighters.Sim.CardView;

namespace YokaiFighters.Tests;

/// <summary>
/// Test Breaker probes for YOK-87 (save file). Expected values come from rules M4 (story on its own flag), A1-A3, T5, R1, C1
/// and the ticket text. <c>KnownOpen_*</c> probes pin today's behaviour on a point the rules leave to the designer: flip
/// the assertion when the designer decides.
/// </summary>
public static class SaveProbes
{
	static string MapDir => ContentPaths.Data("map");
	static NodeSceneTable Table() => NodeSceneTable.Default(MapDir);
	static MapGraph DefaultMap() => FileMapSource.Default(MapDir).Load();
	static RunSession NewSession(uint seed) => RunSession.Start(FightScene.LoadAbilityPool(), DefaultMap(), Table(), seed);

	static void WithTempDir(Action<string> body)
	{
		string dir = Path.Combine(Path.GetTempPath(), "yokai-save-probes-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(dir);
		try { body(dir); }
		finally
		{
			try
			{
				if (!OperatingSystem.IsWindows())
					foreach (string e in Directory.GetFileSystemEntries(dir, "*", SearchOption.AllDirectories))
						try { File.SetUnixFileMode(e, (Directory.Exists(e) ? UnixFileMode.UserExecute : 0) | UnixFileMode.UserRead | UnixFileMode.UserWrite); } catch (Exception) { }
				Directory.Delete(dir, recursive: true);
			}
			catch (IOException) { }
		}
	}

	static void WithTempFile(Action<string> body) => WithTempDir(dir => body(Path.Combine(dir, SaveStore.FileName)));

	static void WithFixtures(Action body)
	{
		var saved = FightScene.Sources;
		FightScene.Sources = FightScene.FixtureSources;
		try { body(); }
		finally { FightScene.Sources = saved; }
	}

	static string Slots(RunSession s) =>
		string.Join(",", s.Run.Owned.Select(o => $"{o.Slot}:{o.Special.Data.Id}:{o.Special.Level}:{o.Special.Modifier?.Id}:{s.Run.LevelledOrder(o.Slot)}"));

	static string Text(RunSession s, string? mapFile = null) => SaveStore.Serialize(SaveData.Empty.WithRun(RunSave.Capture(s, mapFile)));

	/// <summary>Wins the entered node and picks a card by preference (modifier, then upgrade, then new) so the run gains levels and modifiers.</summary>
	static void WinAndPick(RunSession s, int healthLeft, int turn)
	{
		if (s.WinFight(healthLeft) is { } draft)
		{
			var kinds = new[] { CardKind.Modifier, CardKind.Upgrade, CardKind.NewSpecial };
			var order = kinds.Skip(turn % 3).Concat(kinds.Take(turn % 3)).ToArray();
			CardView? card = null;
			foreach (var k in order) { card = draft.Offer.Cards.FirstOrDefault(c => c.Kind == k); if (card != null) break; }
			card ??= draft.Offer.Cards[0];
			Assert.True(draft.Pick(card.Index, card.Targets.Count > 0 ? card.Targets[0].Slot : null).Ok, "picked " + card.AbilityId);
		}
		Assert.True(s.CompleteNode().Ok, "completed");
	}

	// ---- 1. round trip at every save point ----

	[Test]
	public static void RoundTrip_EveryNodeEnteredAndCompleted_ThroughARealFile_SameHash() => WithFixtures(() => WithTempFile(path =>
	{
		// Ticket: continue restores the run exactly (C1 health, A3 levels, A2 modifiers, R1 walk): StateHash equal at every save point.
		var pool = FightScene.LoadAbilityPool();
		var live = NewSession(2024);
		int points = 0;
		bool levelled = false, modified = false;
		void Point(string what)
		{
			var writer = new SaveStore(path);
			Assert.True(writer.Save(SaveData.Empty.WithRun(RunSave.Capture(live, FileMapSource.DefaultFile)).WithUnlock("u").WithStory("intro")), what + ": saved");
			SaveLoad load = new SaveStore(path).Load();
			Assert.Equal(SaveLoadStatus.Loaded, load.Status, what + ": " + string.Join("; ", load.Problems));
			var back = load.Data.Run!.Restore(pool, DefaultMap(), Table());
			Assert.Equal(live.StateHash(), back.StateHash(), what + ": hash");
			Assert.Equal(Slots(live), Slots(back), what + ": slots");
			Assert.True(back.Run.Health == live.Run.Health && back.Step == live.Step && back.Map.InNode == live.Map.InNode && back.Node?.NodeId == live.Node?.NodeId, what + ": health, step, node");
			Assert.True(back.Reachable().SequenceEqual(live.Reachable()), what + ": routes");
			points++;
			levelled |= back.Run.Owned.Any(o => o.Special.Level > 1);
			modified |= back.Run.Owned.Any(o => o.Special.Modifier != null);
		}
		Point("start");
		int turn = 0;
		int[] health = { 900, 640, 880, 420, 700, 310, 560, 240, 90 };
		while (!live.IsOver)
		{
			Assert.True(live.Enter(live.Reachable()[0]).Ok, "entered");
			Point($"entered step {live.Step}");
			WinAndPick(live, health[turn], turn);
			if (live.IsOver) break; // an ended run is removed from the file, not saved
			Point($"completed step {live.Step}");
			turn++;
		}
		Assert.True(points >= 15, $"saved at {points} points");
		Assert.True(levelled && modified, $"the walk had levelled specials ({levelled}) and modifiers ({modified})");
	}));

	// ---- 2. retry by quitting (designer question) ----

	static void WithBoot(Node runner, string? saveFile, Action<GameFlow> body, uint seed = 77, bool pause = false, KitSources? sources = null)
	{
		var saved = FightScene.Sources;
		var savedRun = FightScene.Run;
		var savedFoe = FightScene.Opponent;
		FightScene.Sources = sources ?? FightScene.FixtureSources;
		FightScene.Run = null;
		MenuSettings.Reset();
		var flow = GD.Load<PackedScene>("res://scenes/boot.tscn").Instantiate<GameFlow>();
		flow.QuitAction = () => { };
		flow.FixedRunSeed = seed;
		flow.SaveFile = saveFile;
		flow.ConfigureFight = f => { f.ExternalDrive = true; if (pause) f.PauseEnabled = true; };
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

	static void StartRun(GameFlow flow)
	{
		Press(flow.Title.StartButton);
		Press(flow.ControlSelect.Start);
		Assert.True(flow.Session != null, "a run started");
	}

	static void Hit(GameFlow flow, int ticks = 12)
	{
		for (int t = 0; t < ticks; t++) flow.Fight!.Step(FighterInput.None, new FighterInput(t % 2 == 0 ? InputBits.DebugStrike : InputBits.None));
	}

	[Test]
	public static void KnownOpen_RetryByQuitting_NodeRestartsAtEntryHealth_RoutePickHolds(Node runner) => WithTempFile(path =>
	{
		// Rules are silent on quitting mid-fight (designer question). Today: Pause -> Quit to title -> Continue re-enters the SAME node
		// (no other route can be picked, R1) at the health Ryo entered with (C1): damage is forgiven. Flip when the designer decides.
		WithBoot(runner, path, flow =>
		{
			StartRun(flow);
			string node = flow.Session!.Reachable()[0];
			Assert.True(flow.ChooseNode(node).Ok, "entered");
			int entry = flow.Session.Run.Health;
			Hit(flow);
			int inFight = flow.Fight!.Match.P1.Health;
			Assert.True(inFight < entry, $"Ryo took damage in the fight ({inFight} < {entry})");
			Assert.True(flow.Fight.TryPause(), $"paused (stage {flow.Fight.Stage}, phase {flow.Fight.Match.Phase}, tick {flow.Fight.Match.Tick}, canPause {flow.Fight.CanPause}, menu {flow.Fight.PauseMenu != null}, enabled {flow.Fight.PauseEnabled})");
			Assert.True(flow.Fight.RequestTitle() && flow.Router.Current == Screen.Title, "quit to title from the pause menu");
			Assert.True(!flow.Title.ContinueButton.Disabled, "Continue is lit");
			Press(flow.Title.ContinueButton);
			Assert.True(flow.Router.Current == Screen.Fight && flow.Session!.Node?.NodeId == node, "back in the same node: the route pick holds");
			Assert.True(flow.Session.Reachable().Count == 0, "no other route is open (R1)");
			Assert.Equal(entry, flow.Session.Run.Health, "run health is the entry health");
			Assert.Equal(entry, flow.Fight!.Match.P1.Health, "KnownOpen: the damage taken before quitting is forgiven");
			Assert.Equal(0, flow.Fight.Match.Tick, "the fight starts from tick 0");
		}, pause: true);
	});

	// ---- 3. draft re-roll ----

	static string Offer(RewardDraft d) => string.Join("|", d.Offer.Cards.Select(c => $"{c.Kind}:{c.AbilityId}:{string.Join(",", c.Targets.Select(t => t.Slot))}"));

	[Test]
	public static void Draft_QuitBeforePicking_SameCardsOnReturn_AndASecondPickIsRefused(Node runner) => WithTempFile(path =>
	{
		string first = "";
		WithBoot(runner, path, flow =>
		{
			StartRun(flow);
			Assert.True(flow.ChooseNode(flow.Session!.Reachable()[0]).Ok, "entered");
			var draft = flow.Session.WinFight(700)!;
			first = Offer(draft);
			Assert.True(draft.Pick(0, draft.Offer.Cards[0].Targets.Count > 0 ? draft.Offer.Cards[0].Targets[0].Slot : null).Ok, "picked once");
			Assert.True(!draft.Pick(0, draft.Offer.Cards[0].Targets.Count > 0 ? draft.Offer.Cards[0].Targets[0].Slot : null).Ok, "a second pick on the same offer is refused");
			Assert.True(flow.Session.Draft is { Picked: true }, "the pick is on the live session");
			Assert.True(flow.Session.CompleteNode().Ok, "completing works after the pick");
		});
		// Redo: quit after winning, before picking.
		File.Delete(path);
		WithBoot(runner, path, flow =>
		{
			StartRun(flow);
			Assert.True(flow.ChooseNode(flow.Session!.Reachable()[0]).Ok, "entered");
			string slotsBefore = Slots(flow.Session);
			var d1 = flow.Session.WinFight(700)!;
			Assert.Equal(first, Offer(d1), "same seed: same offer");
			Assert.True(flow.ReturnToTitle(), "quit with the offer open (nothing was picked)");
			Press(flow.Title.ContinueButton);
			Assert.True(flow.Session!.Draft == null && flow.Session.Node != null, "the offer is not kept: back in the node, un-won");
			Assert.Equal(slotsBefore, Slots(flow.Session), "no card was applied");
			var d2 = flow.Session.WinFight(700)!;
			Assert.Equal(first, Offer(d2), "the same cards are offered again (no re-roll, deterministic from seed + step)");
			Assert.True(d2.Pick(0, d2.Offer.Cards[0].Targets.Count > 0 ? d2.Offer.Cards[0].Targets[0].Slot : null).Ok, "the pick works once");
			Assert.True(!d2.Pick(1, null).Ok, "and not twice");
		});
	});

	[Test]
	public static void KnownOpen_Draft_PickedButNotCompleted_QuitUndoesThePick(Node runner) => WithTempFile(path =>
	{
		// Rules are silent: a card picked but not yet confirmed by CompleteNode is not in the file, so quitting lets the player take back the
		// pick and choose another card from the same offer. Flip when the designer wants a pick to be final.
		WithBoot(runner, path, flow =>
		{
			StartRun(flow);
			Assert.True(flow.ChooseNode(flow.Session!.Reachable()[0]).Ok, "entered");
			string before = Slots(flow.Session);
			var d = flow.Session.WinFight(700)!;
			var c = d.Offer.Cards[0];
			Assert.True(d.Pick(0, c.Targets.Count > 0 ? c.Targets[0].Slot : null).Ok, "picked");
			Assert.True(Slots(flow.Session) != before || c.Kind == CardKind.NewSpecial, "the pick changed the live run");
			flow.ReturnToTitle();
			Press(flow.Title.ContinueButton);
			Assert.Equal(before, Slots(flow.Session!), "KnownOpen: the pick is gone after Continue");
		});
	});

	// ---- 4. WriteBlocked for the whole session ----

	[Test]
	public static void WriteBlocked_StaysBlocked_FileByteIdentical_NoBackupNoTemp(Node runner) => WithTempFile(path =>
	{
		if (OperatingSystem.IsWindows()) return;
		string good = SaveStore.Serialize(SaveData.Empty.WithUnlock("oni-quake").WithStory("intro").WithStory("win-1"));
		File.WriteAllText(path, good);
		File.SetUnixFileMode(path, UnixFileMode.None);
		bool stillBlocked = false, startedRun = false;
		try
		{
			WithBoot(runner, path, flow =>
			{
				Assert.True(flow.Saves!.WriteBlocked, "blocked at boot");
				File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite); // permissions come back mid-session
				StartRun(flow);
				startedRun = flow.Session != null;
				Assert.True(flow.ChooseNode(flow.Session!.Reachable()[0]).Ok, "entered");
				Assert.Equal(good, File.ReadAllText(path), "node entered: file untouched");
				flow.Session.WinFight(500);
				var d = flow.Session.Draft;
				if (d != null) d.Pick(0, d.Offer.Cards[0].Targets.Count > 0 ? d.Offer.Cards[0].Targets[0].Slot : null);
				flow.Session.CompleteNode();
				Assert.Equal(good, File.ReadAllText(path), "node completed: file untouched");
				flow.ReturnToTitle();
				flow.Write(flow.Saved.WithStory("win-2")); // even an explicit story write
				stillBlocked = flow.Saves.WriteBlocked;
				Assert.Equal(good, File.ReadAllText(path), "story write: file untouched");
				Assert.True(flow.Saves.LastSaveError != null && flow.Saves.LastSaveError.Contains("untouched"), "the refusal is reported: " + flow.Saves.LastSaveError);
			});
		}
		finally { try { File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite); } catch (Exception) { } }
		Assert.True(startedRun && stillBlocked, "still blocked for the session although the file is readable again");
		Assert.Equal(good, File.ReadAllText(path), "after the session: byte-identical");
		Assert.True(Directory.GetFiles(Path.GetDirectoryName(path)!).Length == 1, "no backup and no temp file");
		WithBoot(runner, path, flow =>
		{
			Assert.True(!flow.Saves!.WriteBlocked && flow.Saved.Unlocks.Count == 1 && flow.Saved.Story.Seen.Count == 2, "relaunch reads everything back (M4)");
		});
	});

	[Test]
	public static void KnownOpen_WriteBlocked_ContinueIsOfferedForARunOnlyTheMemoryHolds(Node runner) => WithTempFile(path =>
	{
		if (OperatingSystem.IsWindows()) return;
		// Boot says "nothing is saved this session", yet after Start Game + quit to title Continue is lit from GameFlow.Saved (memory), though no
		// run is in the file. It works in-session and is gone on relaunch. Low impact; flip to Disabled if the designer wants Continue to mean "on disk".
		File.WriteAllText(path, SaveStore.Serialize(SaveData.Empty.WithStory("intro")));
		File.SetUnixFileMode(path, UnixFileMode.None);
		try
		{
			WithBoot(runner, path, flow =>
			{
				Assert.True(flow.Saves!.WriteBlocked, "blocked");
				StartRun(flow);
				Assert.True(flow.ReturnToTitle(), "quit to title");
				Assert.True(!flow.Title.ContinueButton.Disabled, "KnownOpen: Continue is lit for an in-memory run");
			});
		}
		finally { try { File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite); } catch (Exception) { } }
	});

	// ---- 5. pending lost-run clear ----

	[Test]
	public static void KnownOpen_LostRunClearRefused_TitleHidesContinue_ButAFreshBootOffersTheLostRun(Node runner) => WithTempDir(dir =>
	{
		if (OperatingSystem.IsWindows()) return;
		// The run ended (lost) but the write that removes it was refused. In-session the title must not offer it (it ended).
		// A fresh boot reads the file that still holds the run and offers Continue: today's behaviour, a designer question
		// (an ended run coming back as a retry) that only happens when the disk keeps refusing. Flip if the designer wants an ended-run marker.
		string path = Path.Combine(dir, SaveStore.FileName);
		try
		{
			WithBoot(runner, path, flow =>
			{
				StartRun(flow);
				Assert.True(flow.ChooseNode(flow.Session!.Reachable()[0]).Ok, "entered");
				File.SetUnixFileMode(dir, UnixFileMode.UserRead | UnixFileMode.UserExecute);
				Assert.True(flow.Session.LoseFight().Ok, "lost");
				Assert.True(flow.Saved.Run == null, "flow: no run");
				flow.ReturnToTitle();
				Assert.True(flow.Title.ContinueButton.Disabled, "the title does not offer Continue this session");
				Assert.True(!flow.ContinueRun(), "and Continue cannot be forced");
			});
			// the boot above has exited while the directory is still read-only
			WithBoot(runner, path, flow =>
			{
				Assert.Equal(SaveLoadStatus.Loaded, flow.SaveLoaded!.Status, "the file reads");
				Assert.True(flow.Saved.Run != null, "KnownOpen: the file still holds the run that was lost");
				Assert.True(!flow.Title.ContinueButton.Disabled, "KnownOpen: so a fresh boot offers Continue for a lost run");
			});
		}
		finally { File.SetUnixFileMode(dir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); }
	});

	// ---- 6. backup growth ----

	[Test]
	public static void KnownOpen_Backups_OnePerBadLoadThenWrite_NeverPruned() => WithTempFile(path =>
	{
		// Ticket: a backup is never replaced. Consequence: N bad-file-then-save cycles leave N backups; nothing prunes them. Flip if a cap is added.
		const int cycles = 12;
		for (int i = 0; i < cycles; i++)
		{
			File.WriteAllText(path, "junk " + i);
			var store = new SaveStore(path);
			store.Load();
			Assert.True(store.Save(SaveData.Empty), "saved");
			Assert.True(store.Save(SaveData.Empty), "saved again: one backup per bad load, not per save");
		}
		string[] baks = Directory.GetFiles(Path.GetDirectoryName(path)!, "save.json.bak*");
		Assert.Equal(cycles, baks.Length, "one backup per cycle");
		for (int i = 0; i < cycles; i++)
			Assert.True(baks.Select(File.ReadAllText).Contains("junk " + i), $"the backup of 'junk {i}' exists, none replaced");
		// A launch that never saves makes no backup.
		File.WriteAllText(path, "more junk");
		for (int i = 0; i < 5; i++) new SaveStore(path).Load();
		Assert.Equal(cycles, Directory.GetFiles(Path.GetDirectoryName(path)!, "save.json.bak*").Length, "loading alone never backs up");
	});

	[Test]
	public static void Backup_PendingBackupCannotBeMade_NothingIsWritten() => WithTempFile(path =>
	{
		if (OperatingSystem.IsWindows()) return;
		// Ticket: a failed backup copy writes nothing over the original. Make the backup name uncreatable by a read-only directory.
		File.WriteAllText(path, "not json");
		var store = new SaveStore(path);
		store.Load();
		string dir = Path.GetDirectoryName(path)!;
		File.SetUnixFileMode(dir, UnixFileMode.UserRead | UnixFileMode.UserExecute);
		try
		{
			Assert.True(!store.Save(SaveData.Empty), "refused");
			Assert.Equal("not json", File.ReadAllText(path), "original untouched");
		}
		finally { File.SetUnixFileMode(dir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); }
		Assert.True(store.Save(SaveData.Empty) && File.ReadAllText(path + SaveStore.BackupSuffix) == "not json", "the retry backs up first, then writes");
	});

	// ---- 7. the catch-all ----

	[Test]
	public static void Boot_AnyRestoreFailure_BootCompletes_ContinueGreyedWithReason(Node runner) => WithTempDir(dir =>
	{
		// The catch-all in RestoreSavedRun: make the ability loader (not the save) throw something other than FormatException / IOException,
		// by pointing the content sources at a directory of malformed special files. The boot must complete and Continue be greyed with a reason.
		string path = Path.Combine(dir, SaveStore.FileName);
		RunSave run = null!;
		WithFixtures(() => run = RunSave.Capture(NewSession(77), FileMapSource.DefaultFile));
		File.WriteAllText(path, SaveStore.Serialize(SaveData.Empty.WithRun(run)));
		string before = File.ReadAllText(path);
		int exotic = 0;
		foreach (string junk in new[] { "[]", "null", "{\"id\": 5}", "{\"slot\": []}", "\"x\"", "{", "{\"id\":\"s\",\"slot\":\"Z\",\"levels\":null}" })
		{
			string content = Path.Combine(dir, "content" + junk.GetHashCode());
			Directory.CreateDirectory(Path.Combine(content, "specials"));
			Directory.CreateDirectory(Path.Combine(content, "rewards"));
			File.WriteAllText(Path.Combine(content, "specials", "x.json"), junk);
			var sources = KitSources.At(content, content).FixturesOnly();
			Type? thrown = null;
			var saved = FightScene.Sources;
			FightScene.Sources = sources;
			try { FightScene.LoadAbilityPool(); } catch (Exception e) { thrown = e.GetType(); }
			finally { FightScene.Sources = saved; }
			GD.Print($"PROBE loader on '{junk}': {(thrown?.Name ?? "no exception")}");
			if (thrown == null || thrown == typeof(FormatException) || typeof(IOException).IsAssignableFrom(thrown)) continue;
			exotic++;
			WithBoot(runner, path, flow =>
			{
				Assert.True(flow.Title.ContinueButton.Disabled, $"{thrown.Name}: Continue greyed");
				Assert.True(flow.Title.DisabledReason(TitleEntry.Continue).StartsWith("The saved run cannot be continued"), flow.Title.DisabledReason(TitleEntry.Continue));
				Assert.True(!flow.ContinueRun() && flow.Router.Current == Screen.Title, "cannot be forced");
				Assert.Equal(before, File.ReadAllText(path), "the file is untouched");
			}, sources: sources);
		}
		GD.Print($"PROBE catch-all: {exotic} loader failure shapes reached it");
	});

	// ---- 8. hostile files ----

	static string Good(out RunSave run)
	{
		RunSave r = null!;
		WithFixtures(() =>
		{
			var s = NewSession(5);
			s.Enter(s.Reachable()[0]);
			s.WinFight(800);
			var d = s.Draft; if (d != null) d.Pick(0, d.Offer.Cards[0].Targets.Count > 0 ? d.Offer.Cards[0].Targets[0].Slot : null);
			s.CompleteNode();
			r = RunSave.Capture(s, FileMapSource.DefaultFile);
		});
		run = r;
		return SaveStore.Serialize(SaveData.Empty.WithRun(r).WithUnlock("a").WithStory("intro"));
	}

	static void AssertLegal(RunSession s, string what)
	{
		Assert.True(s.Run.Health is >= 1 and <= RunState.MaxHealth, $"{what}: health {s.Run.Health}");
		Assert.True(s.Run.Owned.Count() is >= 1 and <= 4, $"{what}: owns {s.Run.Owned.Count()} (A1, T5)");
		foreach (var o in s.Run.Owned)
		{
			Assert.True(o.Special.Data.Slot == o.Slot, $"{what}: {o.Special.Data.Id} in slot {o.Slot} (A3)");
			Assert.True(o.Special.Level >= 1 && o.Special.Level <= 3, $"{what}: level {o.Special.Level}");
			Assert.True(o.Special.Modifier == null || o.Special.Modifier.AppliesTo(o.Special.Data), $"{what}: modifier fits (A2)");
		}
		Assert.True(!s.Map.IsFinished && !s.IsOver, $"{what}: not an ended run");
		foreach (string id in s.Reachable()) Assert.True(s.Map.Graph.Node(id) != null, what + ": reachable node exists");
	}

	[Test]
	public static void Hostile_ParseNeverThrows_RestoreOnlyRefusesWithFormatException_NoIllegalState()
	{
		string good = Good(out RunSave run);
		WithFixtures(() =>
		{
			var pool = FightScene.LoadAbilityPool();
			string slot0 = $"{{ \"slot\": \"{run.Slots[0].Slot}\", \"special\": \"{run.Slots[0].Special}\", \"level\": {run.Slots[0].Level}";
			Assert.True(good.Contains("\"health\": " + run.Health), "fixture has health");
			string H(string v) => good.Replace("\"health\": " + run.Health, "\"health\": " + v);
			string L(string v) => good.Replace($"\"level\": {run.Slots[0].Level},", $"\"level\": {v},");
			string Sp(string v) => good.Replace($"\"special\": \"{run.Slots[0].Special}\"", $"\"special\": {v}");
			string Slot(string v) => good.Replace($"\"slot\": \"{run.Slots[0].Slot}\"", $"\"slot\": {v}");
			string C(string v) => System.Text.RegularExpressions.Regex.Replace(good, "\"current\": [^,]*,", $"\"current\": {v},");
			string V(string v) => System.Text.RegularExpressions.Regex.Replace(good, "\"visited\": \\[[^\\]]*\\]", $"\"visited\": {v}");
			string visited0 = run.Visited[0];
			var cases = new Dictionary<string, string>
			{
				["empty"] = "", ["bom only"] = "﻿", ["null"] = "null", ["array"] = "[]", ["run string"] = good.Replace("\"run\": {", "\"run\": \"x\", \"ignored\": {"),
				["5 slots dup"] = good.Replace("\"slots\": [", "\"slots\": [ " + slot0 + " },"),
				["health 0"] = H("0"), ["health -1"] = H("-1"), ["health 1001"] = H("1001"), ["health huge"] = H("99999999999999999999"), ["health 1.5"] = H("1.5"),
				["level 0"] = L("0"), ["level 4"] = L("4"), ["level 99"] = L("99"), ["level -1"] = L("-1"),
				["unknown special"] = Sp("\"no-such\""), ["special null"] = Sp("null"), ["slot E"] = Slot("\"E\""), ["slot lower"] = Slot("\"a\""), ["slot number"] = Slot("1"),
				["unknown modifier"] = good.Replace("\"modifier\": null", "\"modifier\": \"nope\""),
				["visited skips row"] = V("[\"" + visited0 + "\", \"boss-tanuki\"]"),
				["visited dup"] = V("[\"" + visited0 + "\", \"" + visited0 + "\"]"),
				["current not in visited, unreachable"] = C("\"boss-tanuki\""),
				["current unknown"] = C("\"zzz\""), ["current null after visited"] = C("null"), ["current number"] = C("7"),
				["visited empty, current set"] = V("[]"),
				["visited has non-id"] = V("[3]"),
				["nested 10MB"] = "{\"version\":1,\"x\":" + new string('[', 5_000_000) + new string(']', 5_000_000) + "}",
				["fingerprint other"] = good.Replace("map_fingerprint\": \"", "map_fingerprint\": \"0000"),
				["map other"] = good.Replace("\"map\": \"default-map\"", "\"map\": \"other\""),
			};
			foreach (var (name, text) in cases)
			{
				SaveLoad load;
				try { load = SaveStore.Parse(text); }
				catch (Exception e) { throw new AssertFailed($"{name}: Parse threw {e.GetType().Name}: {e.Message}"); }
				Assert.True(load.Data != null && load.Problems != null, name + ": usable data");
				if (load.Data.Run is not { } r) continue;
				try { AssertLegal(r.Restore(pool, DefaultMap(), Table()), name); }
				catch (FormatException) { /* refused: fine */ }
				catch (AssertFailed) { throw; }
				catch (Exception e) { throw new AssertFailed($"{name}: Restore threw {e.GetType().Name} (not FormatException): {e.Message}"); }
			}
		});
	}

	[Test]
	public static void Hostile_BoundaryHealthAndLevel_1_1000_AndLevel3_AreAccepted()
	{
		// C1 / A3: health 1..1000 and level 1..3 are legal values and must restore.
		string good = Good(out RunSave run);
		WithFixtures(() =>
		{
			var pool = FightScene.LoadAbilityPool();
			foreach (int h in new[] { 1, 1000 })
				Assert.Equal(h, (run with { Health = h }).Restore(pool, DefaultMap(), Table()).Run.Health, $"health {h}");
			var up = run.Slots.First(s => pool.Special(s.Special)!.MaxLevel >= 3);
			var three = run with { Slots = run.Slots.Select(s => s == up ? s with { Level = 3 } : s).ToArray() };
			Assert.Equal(3, three.Restore(pool, DefaultMap(), Table()).Run[Enum.Parse<SpecialSlot>(up.Slot)]!.Level, "level 3");
		});
	}

	[Test]
	public static void Hostile_InvalidUtf8_AndZeroBytes_LoadAsCorruptOrReadable_NeverThrow() => WithTempFile(path =>
	{
		File.WriteAllBytes(path, Array.Empty<byte>());
		Assert.Equal(SaveLoadStatus.Corrupt, new SaveStore(path).Load().Status, "zero bytes");
		var bytes = Encoding.UTF8.GetBytes("{\"version\":1,\"run\":null,\"unlocks\":[\"x").Concat(new byte[] { 0x80, 0xC0, 0xFF, 0xFE }).Concat(Encoding.UTF8.GetBytes("y\"],\"story\":{\"seen\":[]}}")).ToArray();
		File.WriteAllBytes(path, bytes);
		SaveLoad l = new SaveStore(path).Load();
		Assert.True(l.Status == SaveLoadStatus.Loaded && l.Data.Unlocks.Count == 1, "invalid UTF-8 inside a string is replaced, not fatal: " + l.Status);
		File.WriteAllBytes(path, new byte[] { 0xFF, 0xFE, 0xFD, 0x00, 0x80 });
		Assert.True(new SaveStore(path).Load().Status == SaveLoadStatus.Corrupt, "pure binary");
	});

	[Test]
	public static void MapFile_TheGuard_AcceptsAndRefuses()
	{
		foreach (string ok in new[] { "default-map.json", "demo-map.json", "a.json", "map v2.json", "a..b.json", "~a.json", "a.json.json" })
			Assert.True(RunSave.IsMapFileName(ok), "accept " + ok);
		foreach (string? bad in new[] { null, "", "\t", ".json", "..json", ".hidden.json", "a/b.json", "a\\b.json", "..\\a.json", "../a.json", "C:a.json", "a:b.json", "a.json\0", "a.json\n", "\ta.json", "a.JSON.", "a.json/", "a.txt", "con", "a.json:stream", "%2e%2e%2fa.json" })
			if (bad == "%2e%2e%2fa.json") { /* literal percent text is a legal bare name on disk: not traversal; skipped */ }
			else Assert.True(!RunSave.IsMapFileName(bad), $"refuse '{bad?.Replace("\0", "\\0")}'");
	}

	// ---- 9. M4 independence ----

	[Test]
	public static void M4_CorruptRunSectionOnly_UnlocksAndStoryIntact_AlsoAfterTheNextWrite() => WithTempFile(path =>
	{
		string good = Good(out _);
		File.WriteAllText(path, System.Text.RegularExpressions.Regex.Replace(good, "\"run\": \\{", "\"run\": [ ], \"was_run\": {"));
		var store = new SaveStore(path);
		SaveLoad l = store.Load();
		Assert.True(l.Data.Run == null && l.Data.Unlocks.SequenceEqual(new[] { "a" }) && l.Data.Story.Has("intro"), "run dropped, unlocks and story intact (M4)");
		Assert.True(store.Save(l.Data.WithStory("win-1")), "saved");
		SaveLoad again = new SaveStore(path).Load();
		Assert.True(again.Data.Unlocks.SequenceEqual(new[] { "a" }) && again.Data.Story.Seen.SequenceEqual(new[] { "intro", "win-1" }), "and after the write");
	});

	[Test]
	public static void M4_CorruptUnlocksOnly_RunAndStoryIntact_ContinueStillLit(Node runner) => WithTempFile(path =>
	{
		string good = Good(out _);
		string text = good.Replace("\"unlocks\": [", "\"unlocks\": {\"k\": 1}, \"was\": [");
		File.WriteAllText(path, text);
		SaveLoad l = new SaveStore(path).Load();
		Assert.True(l.Data.Run != null && l.Data.Unlocks.Count == 0 && l.Data.Story.Has("intro"), "unlocks dropped, run and story intact");
		WithBoot(runner, path, flow =>
		{
			Assert.True(!flow.Title.ContinueButton.Disabled, "the run is still continuable");
			Assert.True(flow.Saved.Story.Has("intro"), "story kept");
			Press(flow.Title.ContinueButton);
			Assert.True(flow.Session != null, "continued");
			Assert.Equal(text, File.ReadAllText(path), "continuing wrote nothing");
		});
	});

	[Test]
	public static void M4_StartGame_KeepsStoryAndUnlocks_ReplacesOnlyTheRun(Node runner) => WithTempFile(path =>
	{
		File.WriteAllText(path, Good(out _));
		WithBoot(runner, path, flow =>
		{
			StartRun(flow);
			var d = new SaveStore(path).Load().Data;
			Assert.True(d.Unlocks.SequenceEqual(new[] { "a" }) && d.Story.Seen.SequenceEqual(new[] { "intro" }), "Start Game: story and unlocks intact");
			Assert.True(d.Run is { Visited.Count: 0 }, "the run is the new one");
		});
	});

	// ---- versions ----

	[Test]
	public static void Versions_EachKind_StatusAndWhatTheNextWriteDoes() => WithTempFile(path =>
	{
		string body = ", \"run\": null, \"unlocks\": [\"a\"], \"story\": { \"seen\": [\"intro\"] }, \"future\": { \"coins\": 9 } }";
		var expect = new (string Version, SaveLoadStatus Status, bool Backup)[]
		{
			("", SaveLoadStatus.OlderVersion, false), // missing: older and fully read => rewritten without a backup
			("\"version\": 0", SaveLoadStatus.OlderVersion, false),
			("\"version\": 1", SaveLoadStatus.Loaded, false),
			("\"version\": 2", SaveLoadStatus.NewerVersion, true),
			("\"version\": \"1\"", SaveLoadStatus.NewerVersion, true),
			("\"version\": -1", SaveLoadStatus.NewerVersion, true),
		};
		foreach (var (v, status, backup) in expect)
		{
			foreach (string old in Directory.GetFiles(Path.GetDirectoryName(path)!, "*.bak*")) File.Delete(old);
			string text = "{" + (v == "" ? "" : v) + (v == "" ? body[1..] : body);
			File.WriteAllText(path, text);
			var store = new SaveStore(path);
			SaveLoad load = store.Load();
			Assert.Equal(status, load.Status, $"[{v}] status");
			Assert.True(load.Data.Unlocks.SequenceEqual(new[] { "a" }) && load.Data.Story.Has("intro"), $"[{v}] known fields kept");
			Assert.True(store.Save(load.Data), $"[{v}] saved");
			Assert.Equal(backup, File.Exists(path + SaveStore.BackupSuffix), $"[{v}] backup made");
			if (backup) Assert.Equal(text, File.ReadAllText(path + SaveStore.BackupSuffix), $"[{v}] the original is the backup");
			string now = File.ReadAllText(path);
			Assert.True(now.Contains("\"version\": 1") && !now.Contains("future"), $"[{v}] the file is now the current version; unknown fields are not carried");
		}
	});

	[Test]
	public static void KnownOpen_Versions_AnOlderFileWithAnUnknownField_IsRewrittenWithoutABackup() => WithTempFile(path =>
	{
		// A versionless or version-0 file is "older": by the ticket's own rule, it is fully understood, so it is not backed up.
		// An unknown field in it (e.g. hand edited) is dropped with no copy. Low risk; flip if the designer wants any dropped field backed up.
		File.WriteAllText(path, "{\"version\":0,\"run\":null,\"unlocks\":[],\"story\":{\"seen\":[]},\"hand_edit\":{\"x\":1}}");
		var store = new SaveStore(path);
		store.Load();
		store.Save(SaveData.Empty);
		Assert.True(!File.Exists(path + SaveStore.BackupSuffix) && !File.ReadAllText(path).Contains("hand_edit"), "KnownOpen: field dropped, no backup");
	});

	// ---- isolation, F3, determinism ----

	[Test]
	public static void Isolation_NoTestSourceNamesTheRealSavePath_OrSetsACurrentScene()
	{
		// Only GameFlow (main scene) may touch user://save.json. A test that sets SaveFile = null on a flow that is the CurrentScene
		// would write the player's file; no test may name UserSavePath or user://save.json, and none may make a GameFlow the current scene.
		string tests = ProjectSettings.GlobalizePath("res://tests");
		int files = 0;
		foreach (string f in Directory.GetFiles(tests, "*.cs", SearchOption.AllDirectories))
		{
			if (f.EndsWith("SaveProbes.cs") || f.EndsWith("SaveTests.cs")) continue;
			files++;
			string t = File.ReadAllText(f);
			Assert.True(!t.Contains("UserSavePath") && !t.Contains("save.json") && !t.Contains("SaveStore"), Path.GetFileName(f) + " names the save");
			Assert.True(!t.Contains("CurrentScene = "), Path.GetFileName(f) + " sets the current scene");
		}
		Assert.True(files > 30, "scanned " + files);
	}

	[Test]
	public static void F3_TheClockScan_FlagsAPlantedClockInACopyUnderTemp()
	{
		// The scan in SaveTests is an inline test body: only its banned-word list (SaveTests.Clocks, private) is reusable, by reflection.
		var clocks = (string[])typeof(SaveTests).GetField("Clocks", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
		WithTempDir(dir =>
		{
			string src = File.ReadAllText(Path.Combine(ProjectSettings.GlobalizePath("res://scripts"), "SaveGame", "SaveStore.cs"));
			string planted = Path.Combine(dir, "SaveStore.cs");
			File.WriteAllText(planted, src.Replace("public const int CurrentVersion = 1;", "public const int CurrentVersion = 1; static long Stamp => System.DateTime.Now.Ticks;"));
			string text = File.ReadAllText(planted);
			Assert.True(clocks.Any(c => text.Contains(c)), "a planted DateTime.Now is caught by the banned-word list");
			Assert.True(!clocks.Any(c => src.Contains(c)), "the real file is clean");
			foreach (string sneaky in new[] { "System.Environment.TickCount64", "Stopwatch.GetTimestamp()", "DateTimeOffset.UtcNow", "File.GetLastWriteTimeUtc(p)", "Time.GetTicksMsec()", "TimeProvider.System.GetUtcNow()" })
				Assert.True(clocks.Any(c => sneaky.Contains(c)) || sneaky.StartsWith("File."), $"'{sneaky}' is caught (File.GetLastWriteTime* is not a listed clock)");
		});
	}

	[Test]
	public static void Determinism_SameSeedSameInputs_IdenticalBytesAtEverySavePoint_DifferentChoiceDiffers() => WithFixtures(() =>
	{
		List<string> Walk(uint seed, int pickKind)
		{
			var texts = new List<string>();
			var s = NewSession(seed);
			texts.Add(Text(s, FileMapSource.DefaultFile));
			int turn = 0;
			while (!s.IsOver)
			{
				s.Enter(s.Reachable()[0]);
				texts.Add(Text(s, FileMapSource.DefaultFile));
				WinAndPick(s, 700 - turn * 40, turn + pickKind);
				if (!s.IsOver) texts.Add(Text(s, FileMapSource.DefaultFile));
				turn++;
			}
			return texts;
		}
		var a = Walk(31, 0);
		var b = Walk(31, 0);
		Assert.True(a.SequenceEqual(b), "same seed, same inputs: identical bytes at every point (F3)");
		Assert.True(!Walk(31, 1).SequenceEqual(a), "a different pick pattern writes different bytes");
		Assert.True(!Walk(32, 0).SequenceEqual(a), "a different seed writes different bytes");
		Assert.True(a[0].Contains("\"seed\": 31"), "the seed is in the file");
	});

	[Test]
	public static void Determinism_TwoBootsSameSeed_SameFinalFileBytes(Node runner)
	{
		string[] final = new string[2];
		for (int i = 0; i < 2; i++)
		{
			int k = i;
			WithTempFile(path => WithBoot(runner, path, flow =>
			{
				StartRun(flow);
				for (int n = 0; n < 3; n++)
				{
					Assert.True(flow.ChooseNode(flow.Session!.Reachable()[0]).Ok, "entered");
					flow.Session.WinFight(600 - n * 50);
					var d = flow.Session.Draft;
					if (d != null) d.Pick(0, d.Offer.Cards[0].Targets.Count > 0 ? d.Offer.Cards[0].Targets[0].Slot : null);
					flow.Session.CompleteNode();
				}
				final[k] = File.ReadAllText(path);
			}, seed: 4242));
		}
		Assert.Equal(final[0], final[1], "two boots, same seed and inputs: the same file");
	}

	[Test]
	public static void Hostile_HugeUnlockList_ParsesInReasonableTime()
	{
		// Non-blocking perf check: Ids() de-duplicates with List.Contains, O(n^2). 20k distinct unlocks must still load in well under a few seconds.
		var sb = new StringBuilder("{\"version\":1,\"run\":null,\"story\":{\"seen\":[]},\"unlocks\":[");
		for (int i = 0; i < 20000; i++) sb.Append(i == 0 ? "" : ",").Append("\"ability-").Append(i).Append('"');
		sb.Append("]}");
		var sw = System.Diagnostics.Stopwatch.StartNew();
		SaveLoad l = SaveStore.Parse(sb.ToString());
		sw.Stop();
		GD.Print($"PROBE huge unlock list: 20000 ids parsed in {sw.ElapsedMilliseconds} ms");
		Assert.True(l.Data.Unlocks.Count == 20000 && sw.ElapsedMilliseconds < 5000, $"{sw.ElapsedMilliseconds} ms");
	}
}
