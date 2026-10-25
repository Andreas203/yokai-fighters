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
						try { Chmod(e, (Directory.Exists(e) ? UnixFileMode.UserExecute : 0) | UnixFileMode.UserRead | UnixFileMode.UserWrite); } catch (Exception) { }
				Directory.Delete(dir, recursive: true);
			}
			catch (IOException) { }
		}
	}

	static void Chmod(string path, UnixFileMode mode) { if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, mode); }

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
		Chmod(path, UnixFileMode.None);
		bool stillBlocked = false, startedRun = false;
		try
		{
			WithBoot(runner, path, flow =>
			{
				Assert.True(flow.Saves!.WriteBlocked, "blocked at boot");
				Chmod(path, UnixFileMode.UserRead | UnixFileMode.UserWrite); // permissions come back mid-session
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
		finally { try { Chmod(path, UnixFileMode.UserRead | UnixFileMode.UserWrite); } catch (Exception) { } }
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
		Chmod(path, UnixFileMode.None);
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
		finally { try { Chmod(path, UnixFileMode.UserRead | UnixFileMode.UserWrite); } catch (Exception) { } }
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
				Chmod(dir, UnixFileMode.UserRead | UnixFileMode.UserExecute);
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
		finally { Chmod(dir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); }
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
		Chmod(dir, UnixFileMode.UserRead | UnixFileMode.UserExecute);
		try
		{
			Assert.True(!store.Save(SaveData.Empty), "refused");
			Assert.Equal("not json", File.ReadAllText(path), "original untouched");
		}
		finally { Chmod(dir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); }
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
				["nested 10MB"] = $"{{\"version\":{SaveStore.CurrentVersion},\"x\":" + new string('[', 5_000_000) + new string(']', 5_000_000) + "}",
				["fingerprint other"] = good.Replace("map_fingerprint\": \"", "map_fingerprint\": \"0000"),
				["map other"] = good.Replace("\"map\": \"default-map\"", "\"map\": \"other\""),
			};
			foreach (var (name, text) in cases)
			{
				SaveLoad load;
				try { load = SaveStore.Parse(text); }
				catch (Exception e) { throw new AssertFailed($"{name}: Parse threw {e.GetType().Name}: {e.Message}"); }
				Assert.True(load.Problems.Count >= 0 && load.Status != SaveLoadStatus.Missing, name + ": usable data");
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
		var bytes = Encoding.UTF8.GetBytes($"{{\"version\":{SaveStore.CurrentVersion},\"run\":null,\"unlocks\":[\"x").Concat(new byte[] { 0x80, 0xC0, 0xFF, 0xFE }).Concat(Encoding.UTF8.GetBytes("y\"],\"story\":{\"seen\":[]}}")).ToArray();
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
		int cur = SaveStore.CurrentVersion;
		var expect = new (string Version, SaveLoadStatus Status, bool Backup)[]
		{
			("", SaveLoadStatus.OlderVersion, false), // missing: older and fully read => rewritten without a backup
			("\"version\": 0", SaveLoadStatus.OlderVersion, false),
			($"\"version\": {cur - 1}", SaveLoadStatus.OlderVersion, false), // the previous format, fully read
			($"\"version\": {cur}", SaveLoadStatus.Loaded, false),
			($"\"version\": {cur + 1}", SaveLoadStatus.NewerVersion, true),
			($"\"version\": \"{cur}\"", SaveLoadStatus.NewerVersion, true),
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
			Assert.True(now.Contains($"\"version\": {SaveStore.CurrentVersion}") && !now.Contains("future"), $"[{v}] the file is now the current version; unknown fields are not carried");
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
			string name = Path.GetFileName(f);
			// Explicit list, not a suffix: a file earns the right to name the save store by being written here, not by its name.
			// The two original save files are exempt outright. The ticket-level save files (YOK-95) may use the in-memory store
			// (Parse / Serialize, no file) but are still scanned for anything that reaches the player's file.
			if (name == "SaveProbes.cs" || name == "SaveTests.cs") continue;
			files++;
			string t = File.ReadAllText(f);
			bool inMemorySave = name == "MerchantShopSaveTests.cs" || name == "MerchantShopSaveProbes.cs";
			// YOK-204: the practice probes (YOK-197) write a SaveStore under a temp dir to prove practice never touches the save,
			// so they may name SaveStore, but never the player's path nor the current scene.
			bool tempPathSave = name == "PracticeBreakerProbes.cs";
			if (tempPathSave)
			{
				Assert.True(!t.Contains("UserSavePath") && !t.Contains("user://") && !t.Contains("save.json"), name + " reaches the player's save path");
			}
			else if (inMemorySave)
			{
				Assert.True(!t.Contains("UserSavePath") && !t.Contains("user://") && !t.Contains("save.json") && !t.Contains("new SaveStore(") && !t.Contains("GameFlow") && !t.Contains("SaveStore.FileName"), name + " reaches a save file or the player's path");
			}
			else Assert.True(!t.Contains("UserSavePath") && !t.Contains("save.json") && !t.Contains("SaveStore"), name + " names the save");
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
			string plantedText = System.Text.RegularExpressions.Regex.Replace(src, @"(public const int CurrentVersion = \d+;)", "$1 static long Stamp => System.DateTime.Now.Ticks;");
			Assert.True(plantedText != src && plantedText.Contains("DateTime.Now"), "the clock really was planted, whatever the version number");
			File.WriteAllText(planted, plantedText);
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
		var sb = new StringBuilder($"{{\"version\":{SaveStore.CurrentVersion}," + "\"run\":null,\"story\":{\"seen\":[]},\"unlocks\":[");
		for (int i = 0; i < 20000; i++) sb.Append(i == 0 ? "" : ",").Append("\"ability-").Append(i).Append('"');
		sb.Append("]}");
		var sw = System.Diagnostics.Stopwatch.StartNew();
		SaveLoad l = SaveStore.Parse(sb.ToString());
		sw.Stop();
		GD.Print($"PROBE huge unlock list: 20000 ids parsed in {sw.ElapsedMilliseconds} ms");
		Assert.True(l.Data.Unlocks.Count == 20000 && sw.ElapsedMilliseconds < 5000, $"{sw.ElapsedMilliseconds} ms");
	}

	// ======== round 3: format version 2 (coins, the copy lock) ========

	static bool SameCopy(CopyTarget? a, CopyTarget? b) =>
		a == null ? b == null : b != null && a.Slot == b.Slot && a.Data.Id == b.Data.Id && a.Level == b.Level;

	/// <summary>Plays the default map (merchant: heal, dojo: rest, duels: won and a card picked) until the Tanuki's node is the next pick; enters it when asked.</summary>
	static RunSession WalkToBoss(uint seed, bool enterBoss, Action<RunSession, string>? atPoint = null)
	{
		var s = NewSession(seed);
		atPoint?.Invoke(s, "start");
		int turn = 0;
		while (!s.Reachable().Contains("boss-tanuki"))
		{
			Assert.True(s.Enter(s.Reachable()[0]).Ok, "entered");
			atPoint?.Invoke(s, "entered " + s.Node!.NodeId);
			if (s.Merchant is { } m) Assert.True(m.TakeHeal().Ok, "heal");
			else if (s.Dojo is { } d) Assert.True(d.Rest().Ok, "rest");
			else WinAndPick(s, 800 - turn * 30, turn);
			if (s.Merchant != null || s.Dojo != null) Assert.True(s.CompleteNode().Ok, "completed");
			atPoint?.Invoke(s, "completed " + s.Step);
			turn++;
		}
		if (enterBoss)
		{
			Assert.True(s.Enter("boss-tanuki").Ok && s.CopyLocked, "entered the Tanuki's node, locked (T1)");
			atPoint?.Invoke(s, "entered boss");
		}
		return s;
	}

	static string ToV1(string v2Text)
	{
		string t = System.Text.RegularExpressions.Regex.Replace(v2Text, "\"version\": \\d+", "\"version\": 1");
		t = System.Text.RegularExpressions.Regex.Replace(t, "\\s*\"coins\": -?\\d+,", "");
		t = System.Text.RegularExpressions.Regex.Replace(t, "\\s*\"copy_locked\": (true|false),", "");
		t = System.Text.RegularExpressions.Regex.Replace(t, "\\s*\"locked_copy\": (null|\\{[^}]*\\}),", "");
		Assert.True(!t.Contains("coins") && !t.Contains("copy_locked") && !t.Contains("locked_copy"), "the v1 text has no v2 fields");
		return t;
	}

	static RunSession RestoreText(string text, List<string>? notes = null)
	{
		SaveLoad l = SaveStore.Parse(text);
		Assert.True(l.Data.Run != null, "the run reads: " + string.Join("; ", l.Problems));
		return l.Data.Run!.Restore(FightScene.LoadAbilityPool(), DefaultMap(), Table(), notes);
	}

	static void Refused(Func<RunSession> restore, string what)
	{
		try { restore(); }
		catch (FormatException) { return; }
		catch (Exception e) { throw new AssertFailed($"{what}: threw {e.GetType().Name} instead of FormatException: {e.Message}"); }
		throw new AssertFailed($"{what}: was restored, expected a refusal");
	}

	[Test]
	public static void V2_Coins_RoundTripAtEverySavePoint_DamageIsUnrestorable() => WithFixtures(() =>
	{
		// R8: the purse is run state. Every save point of a walk with merchant, dojo and boss gives back the same coins and the same hash.
		var pool = FightScene.LoadAbilityPool();
		int points = 0, maxCoins = 0;
		string sample = "";
		WalkToBoss(404, true, (s, what) =>
		{
			string text = Text(s, FileMapSource.DefaultFile);
			var back = RestoreText(text);
			Assert.True(back.Run.Coins == s.Run.Coins, $"{what}: coins {s.Run.Coins} back as {back.Run.Coins}");
			Assert.Equal(s.StateHash(), back.StateHash(), what + ": hash");
			points++;
			if (s.Run.Coins > maxCoins) { maxCoins = s.Run.Coins; sample = text; }
		});
		Assert.True(points >= 10 && maxCoins > 0, $"the walk paid coins ({maxCoins}) over {points} points");
		string tail = $"\"coins\": {maxCoins}";
		Assert.True(sample.Contains(tail), "the purse is in the file");
		SaveLoad ok = SaveStore.Parse(sample.Replace(tail, "\"coins\": 2147483647"));
		Assert.True(ok.Data.Run!.Restore(pool, DefaultMap(), Table()).Run.Coins == int.MaxValue, "the largest purse an int holds restores");
		foreach (string bad in new[] { "-1", "-2147483648" })
			Refused(() => RestoreText(sample.Replace(tail, "\"coins\": " + bad)), "coins " + bad);
		// Designer, 2026-10-11: a whole number written another way JSON allows reads as that number; coins stay uncapped.
		foreach (string same in new[] { "1e2", "100.0", "1E+2" })
			Assert.True(SaveStore.Parse(sample.Replace(tail, "\"coins\": " + same)).Data.Run!.Coins == 100, $"coins {same}: read as 100");
		foreach (string bad in new[] { "\"lots\"", "1.5", "null", "true", "2147483648", "[5]", "{}", "1e-1", "\"25\"" })
		{
			SaveLoad l = SaveStore.Parse(sample.Replace(tail, "\"coins\": " + bad));
			Assert.True(l.Data.Run == null && l.Status == SaveLoadStatus.Partial, $"coins {bad}: run dropped, not zeroed ({l.Status})");
		}
		Assert.True(SaveStore.Parse(sample.Replace(tail + ",", "")).Data.Run!.Coins == 0, "no coins field: empty purse");
	});

	[Test]
	public static void V2_LockInsideTheTanukiNode_RoundTrip_SameHash_StableBytes() => WithFixtures(() =>
	{
		var live = WalkToBoss(505, true);
		string text = Text(live, FileMapSource.DefaultFile);
		Assert.True(text.Contains("\"copy_locked\": true") && text.Contains("\"locked_copy\": {"), "the lock is in the file: " + text);
		var back = RestoreText(text);
		Assert.True(back.CopyLocked && SameCopy(back.LockedCopy, live.LockedCopy) && back.Node?.NodeId == "boss-tanuki", "locked, same copy, inside the boss node");
		Assert.Equal(live.StateHash(), back.StateHash(), "hash (T1: the copy is part of the run)");
		Assert.Equal(text, Text(back, FileMapSource.DefaultFile), "save, restore, save: same bytes");
		Assert.True(SameCopy(live.LockedCopy, CopyRule.Target(live.Run)), "T2/T3: the lock is what the slots lock on");
		// one step earlier: the last node before the boss, completed: not locked, and the file says so
		var before = WalkToBoss(505, false);
		string t2 = Text(before, FileMapSource.DefaultFile);
		Assert.True(t2.Contains("\"copy_locked\": false") && t2.Contains("\"locked_copy\": null"), "before the Tanuki's node: not locked");
		var b2 = RestoreText(t2);
		Assert.True(!b2.CopyLocked && b2.LockedCopy == null && b2.StateHash() == before.StateHash(), "restored unlocked, same hash");
		// the lock is not re-derived on the way in: after restoring unlocked and entering the boss the copy equals the live run's
		Assert.True(b2.Enter("boss-tanuki").Ok && SameCopy(b2.LockedCopy, live.LockedCopy), "entering the boss after a Continue locks the same copy");
	});

	static IEnumerable<(string What, RunSave Save)> BadLocks(RunSave inBoss, RunSave beforeBoss, AbilityPool pool)
	{
		var c = inBoss.LockedCopy!;
		string otherSlot = c.Slot == "A" ? "B" : "A";
		var other = pool.Specials.First(x => x.Id != c.Special && x.Slot.ToString() == otherSlot);
		yield return ("locked before the boss, no copy", beforeBoss with { CopyLocked = true, LockedCopy = null });
		yield return ("locked before the boss, with a copy", beforeBoss with { CopyLocked = true, LockedCopy = c });
		yield return ("a copy before the boss, unlocked", beforeBoss with { CopyLocked = false, LockedCopy = c });
		yield return ("unlocked inside the boss", inBoss with { CopyLocked = false, LockedCopy = null });
		yield return ("copy while copy_locked false", inBoss with { CopyLocked = false });
		yield return ("copy with no copy_locked field", inBoss with { CopyLocked = null });
		yield return ("wrong slot (a different valid slot)", inBoss with { LockedCopy = c with { Slot = otherSlot } });
		yield return ("slot E", inBoss with { LockedCopy = c with { Slot = "E" } });
		yield return ("slot lower-case", inBoss with { LockedCopy = c with { Slot = c.Slot.ToLowerInvariant() } });
		yield return ("level 0", inBoss with { LockedCopy = c with { Level = 0 } });
		yield return ("level 99", inBoss with { LockedCopy = c with { Level = 99 } });
		yield return ("level -1", inBoss with { LockedCopy = c with { Level = -1 } });
		yield return ("unknown special", inBoss with { LockedCopy = c with { Special = "no-such-special" } });
		yield return ("special in another slot than named", inBoss with { LockedCopy = new LockedCopySave(c.Slot, other.Id, 1) });
	}

	[Test]
	public static void V2_LockThatDisagreesWithTheWalk_IsRefused_AndGreysContinue_WithoutThrowingOutOfBoot(Node runner) => WithTempDir(dir =>
	{
		// T1: nothing is locked before the Tanuki's node and the lock is set on entering it; a copy must be a real special in its own slot and a real level.
		RunSave inBoss = null!, beforeBoss = null!;
		AbilityPool pool = null!;
		WithFixtures(() =>
		{
			pool = FightScene.LoadAbilityPool();
			inBoss = RunSave.Capture(WalkToBoss(606, true), FileMapSource.DefaultFile);
			beforeBoss = RunSave.Capture(WalkToBoss(606, false), FileMapSource.DefaultFile);
		});
		Assert.True(inBoss.CopyLocked == true && inBoss.LockedCopy != null && beforeBoss.CopyLocked == false, "fixtures are the saves they should be");
		int n = 0;
		foreach (var (what, bad) in BadLocks(inBoss, beforeBoss, pool))
		{
			WithFixtures(() => Refused(() => bad.Restore(pool, DefaultMap(), Table()), what));
			string path = Path.Combine(dir, $"s{n++}.json");
			File.WriteAllText(path, SaveStore.Serialize(SaveData.Empty.WithRun(bad).WithStory("intro")));
			string before = File.ReadAllText(path);
			WithBoot(runner, path, flow =>
			{
				Assert.True(flow.Title.ContinueButton.Disabled, $"{what}: Continue greyed");
				Assert.True(flow.Title.DisabledReason(TitleEntry.Continue).StartsWith("The saved run cannot be continued"), $"{what}: {flow.Title.DisabledReason(TitleEntry.Continue)}");
				Assert.True(!flow.ContinueRun() && flow.Session == null && flow.Router.Current == Screen.Title, $"{what}: nothing starts");
				Assert.True(flow.Saved.Story.Has("intro"), $"{what}: story still loaded (M4)");
			});
			Assert.Equal(before, File.ReadAllText(path), $"{what}: the file is left as it was");
		}
		Assert.True(n >= 14, "variants run: " + n);
	});

	[Test]
	public static void V2_LockWrongTypedInTheFile_DropsTheRunAlone() => WithFixtures(() =>
	{
		string text = Text(WalkToBoss(707, true), FileMapSource.DefaultFile);
		text = text.Replace("\"version\": 2", "\"version\": 2").Replace("\"unlocks\": [", "\"unlocks\": [\"u\"], \"x\": [");
		foreach (var (from, to) in new[]
		{
			("\"copy_locked\": true", "\"copy_locked\": \"yes\""), ("\"copy_locked\": true", "\"copy_locked\": 1"), ("\"copy_locked\": true", "\"copy_locked\": null"),
			("\"locked_copy\": {", "\"locked_copy\": [], \"was\": {"), ("\"locked_copy\": {", "\"locked_copy\": \"A\", \"was\": {"),
		})
		{
			Assert.True(text.Contains(from), "fixture has " + from);
			SaveLoad l = SaveStore.Parse(text.Replace(from, to));
			Assert.True(l.Data.Run == null && l.Status == SaveLoadStatus.Partial && l.Data.Unlocks.SequenceEqual(new[] { "u" }), $"{to}: run dropped, unlocks kept ({l.Status})");
		}
		string noLevel = System.Text.RegularExpressions.Regex.Replace(text, "(\"locked_copy\": \\{[^}]*?)\"level\": \\d+", "$1\"lvl\": 1");
		Assert.True(noLevel != text && SaveStore.Parse(noLevel).Data.Run == null, "locked_copy without a level: run dropped");
		string half = System.Text.RegularExpressions.Regex.Replace(text, "(\"locked_copy\": \\{[^}]*?)\"level\": (\\d+)", "$1\"level\": $2.5");
		Assert.True(SaveStore.Parse(half).Data.Run == null, "locked_copy level 2.5: run dropped");
	});

	[Test]
	public static void T1_LockInsideTheTanukiNode_MustBeWhatTheEntrySlotsLock_ElseRefused() => WithFixtures(() =>
	{
		// T1/T2/T3/T5: once Ryo is inside the Tanuki's node the slots are exactly what they were when the copy locked, so a saved lock that is
		// not CopyRule.Target(slots) cannot have come from play: a special Ryo never held, a level he never had, or no copy while he owns specials.
		var pool = FightScene.LoadAbilityPool();
		var inBoss = RunSave.Capture(WalkToBoss(808, true), FileMapSource.DefaultFile);
		var c = inBoss.LockedCopy!;
		var accepted = new List<string>();
		void Try(string what, RunSave r)
		{
			try { r.Restore(pool, DefaultMap(), Table()); accepted.Add(what); }
			catch (FormatException) { }
		}
		int otherLevel = c.Level < pool.Special(c.Special)!.MaxLevel ? c.Level + 1 : c.Level - 1;
		Assert.True(otherLevel >= 1, "a different level exists");
		Try($"level {otherLevel} instead of {c.Level} for {c.Special}", inBoss with { LockedCopy = c with { Level = otherLevel } });
		var notOwned = pool.Specials.First(x => x.Id != c.Special);
		Try($"{notOwned.Id} (not the slots' target) at level 1", inBoss with { LockedCopy = new LockedCopySave(notOwned.Slot.ToString(), notOwned.Id, 1) });
		Try("no copy while specials are owned (T5)", inBoss with { LockedCopy = null });
		Assert.True(accepted.Count == 0, "restored although the lock cannot be what the slots lock on: " + string.Join(" | ", accepted));
	});

	[Test]
	public static void V1_Files_CoinsZero_NoLockBeforeBoss_LockReDerivedExactlyInsideBoss() => WithFixtures(() =>
	{
		var pool = FightScene.LoadAbilityPool();
		// start of a run: v1 equals the live session, hash and all (nothing was earned yet)
		var s0 = NewSession(909);
		string v1Start = ToV1(Text(s0, FileMapSource.DefaultFile));
		SaveLoad l0 = SaveStore.Parse(v1Start);
		Assert.True(l0.Status == SaveLoadStatus.OlderVersion && l0.Data.Run!.CopyLocked == null && l0.Data.Run.Coins == 0, "v1: older, no lock said, coins 0");
		var r0 = RestoreText(v1Start);
		Assert.True(r0.StateHash() == s0.StateHash() && !r0.CopyLocked && r0.Run.Coins == 0, "start: same hash, unlocked");
		// every pre-boss save point of a real walk: restores unlocked with an empty purse (coins are the one thing a v1 file cannot hold)
		int points = 0;
		var boss = WalkToBoss(909, true, (s, what) =>
		{
			if (what == "entered boss") return;
			var back = RestoreText(ToV1(Text(s, FileMapSource.DefaultFile)));
			Assert.True(!back.CopyLocked && back.LockedCopy == null && back.Run.Coins == 0 && back.Run.Health == s.Run.Health && Slots(back) == Slots(s), $"v1 {what}: unlocked, coins 0, same slots and health");
			points++;
		});
		Assert.True(points >= 10, "pre-boss points: " + points);
		// inside the boss: the lock is worked out from the slots and equals the live lock
		var notes = new List<string>();
		var inBoss = RestoreText(ToV1(Text(boss, FileMapSource.DefaultFile)), notes);
		Assert.True(inBoss.CopyLocked && SameCopy(inBoss.LockedCopy, boss.LockedCopy) && inBoss.Node?.NodeId == "boss-tanuki", "v1 inside the boss: the same copy as the live lock (T1)");
		Assert.True(notes.Any(x => x.Contains("copy lock")), "and the derivation is reported");
		Assert.True(Slots(inBoss) == Slots(boss) && inBoss.Run.Coins == 0, "slots equal, purse empty");
		// a level-up order that had to be repaired: refused, never guessed
		var save = RunSave.Capture(boss, FileMapSource.DefaultFile);
		var leveled = save.Slots.Select(x => pool.Special(x.Special)!.MaxLevel >= 2 ? x with { Level = 2, Levelled = 0 } : x).ToArray();
		Assert.True(leveled.Count(x => x.Level == 2) >= 2, "two specials can level");
		var repairable = save with { Slots = leveled, Coins = 0, CopyLocked = null, LockedCopy = null };
		Refused(() => repairable.Restore(pool, DefaultMap(), Table()), "v1 inside the boss with a repaired level-up order");
	});

	[Test]
	public static void V1_File_IsRewrittenAsVersion2_NothingLost_NoBackup() => WithFixtures(() => WithTempFile(path =>
	{
		var s = WalkToBoss(111, false);
		var data = SaveData.Empty.WithRun(RunSave.Capture(s, FileMapSource.DefaultFile)).WithUnlock("u1").WithUnlock("u0").WithStory("win-1").WithStory("intro");
		File.WriteAllText(path, ToV1(SaveStore.Serialize(data)));
		var store = new SaveStore(path);
		SaveLoad load = store.Load();
		Assert.Equal(SaveLoadStatus.OlderVersion, load.Status, "older");
		Assert.True(!SaveStore.LostSomething(load), "nothing lost on reading it");
		Assert.True(store.Save(load.Data), "saved");
		string now = File.ReadAllText(path);
		Assert.True(now.Contains("\"version\": " + SaveStore.CurrentVersion) && now.Contains("\"coins\": 0"), "rewritten as the current version: " + now);
		Assert.True(Directory.GetFiles(Path.GetDirectoryName(path)!).Length == 1, "no backup, no temp");
		SaveLoad again = new SaveStore(path).Load();
		Assert.True(again.Status == SaveLoadStatus.Loaded && again.Data.Unlocks.SequenceEqual(new[] { "u1", "u0" }) && again.Data.Story.Seen.SequenceEqual(new[] { "intro", "win-1" }), "unlocks (order) and story intact (M1, M4)");
		Assert.True(again.Data.Run!.Slots.SequenceEqual(data.Run!.Slots) && again.Data.Run.Visited.SequenceEqual(data.Run.Visited) && again.Data.Run.Health == data.Run.Health, "run fields intact");
		Assert.True(RestoreText(now).Step == s.Step, "and it still restores");
	}));

	[Test]
	public static void V1_BossFile_ThroughTheBoot_ContinueEntersTheBoss_AndTheNextWriteIsVersion2(Node runner) => WithTempFile(path =>
	{
		RunSession boss = null!;
		string v1 = "";
		WithFixtures(() => { boss = WalkToBoss(222, true); v1 = ToV1(SaveStore.Serialize(SaveData.Empty.WithRun(RunSave.Capture(boss, FileMapSource.DefaultFile)).WithUnlock("u").WithStory("intro"))); });
		File.WriteAllText(path, v1);
		WithBoot(runner, path, flow =>
		{
			Assert.True(!flow.Title.ContinueButton.Disabled, "Continue lit for a v1 save inside the boss node");
			Assert.Equal(v1, File.ReadAllText(path), "boot wrote nothing");
			Press(flow.Title.ContinueButton);
			Assert.True(flow.Session!.Node?.NodeId == "boss-tanuki" && flow.Session.CopyLocked && SameCopy(flow.Session.LockedCopy, boss.LockedCopy), "inside the boss, the same copy locked");
			Assert.True(flow.Session.LoseFight().Ok, "lost");
			SaveLoad after = new SaveStore(path).Load();
			// YOK-86, M1: a loss at the Tanuki now awards 2 unlocks after the saved ones; the saved "u" stays first.
			var order = Unlocks.FromData(flow.Session.Pool, CancelRuleLoader.LockedIds(ContentPaths.Data("cancels")));
			var awarded = order.Award(flow.Session.Ended!, new[] { "u" });
			Assert.Equal(Math.Min(2, order.StillLocked(new[] { "u" }).Count), awarded.Count, "M1: a loss at the Tanuki awards 2 (or what is left)");
			Assert.True(after.Status == SaveLoadStatus.Loaded && after.Data.Run == null && after.Data.Unlocks.SequenceEqual(new[] { "u" }.Concat(awarded)) && after.Data.Story.Has("intro"), "the next write is version 2 with unlocks and story intact: " + after.Status);
			Assert.True(Directory.GetFiles(Path.GetDirectoryName(path)!).Length == 1, "no backup");
		});
	});

	[Test]
	public static void MerchantAndDojo_EnteredSavePoints_RestoreOpenWithNoChoice_AndEveryCompletedPointToo() => WithFixtures(() =>
	{
		// The author's claim: a stop's choice is None at every save point. Probed through the file: restore at entry (inside the stop) and after completion.
		var types = new HashSet<string>();
		foreach (uint seed in new[] { 61u, 62u })
			foreach (bool last in new[] { false, true })
			{
				var s = NewSession(seed);
				while (!s.IsOver)
				{
					var reach = s.Reachable();
					Assert.True(s.Enter(last ? reach[^1] : reach[0]).Ok, "entered");
					types.Add(s.Node!.Type);
					var back = RestoreText(Text(s, FileMapSource.DefaultFile));
					Assert.Equal(s.StateHash(), back.StateHash(), $"{s.Node.NodeId}: restored inside the node");
					if (s.Node.Type == NodeTypeRegistry.Merchant)
					{
						Assert.True(back.Merchant is { CanChoose: true } && back.Merchant.Choice == MerchantChoice.None, "merchant open, nothing chosen");
						Assert.True(s.Merchant!.TakeHeal().Ok, "heal");
						Assert.True(s.StateHash() != back.StateHash(), "the live choice is visible in the hash (so a save after it would differ): it is NOT in the file");
					}
					else if (s.Node.Type == NodeTypeRegistry.Dojo)
					{
						Assert.True(back.Dojo is { } bd && bd.Choice == DojoChoice.None, "dojo open, nothing chosen");
						Assert.True(s.Dojo!.Rest().Ok, "rest");
					}
					else if (s.WinFight(650) is { } draft) draft.Pick(0, draft.Offer.Cards[0].Targets.Count > 0 ? draft.Offer.Cards[0].Targets[0].Slot : null);
					Assert.True(s.CompleteNode().Ok, "completed");
					if (!s.IsOver) Assert.Equal(s.StateHash(), RestoreText(Text(s, FileMapSource.DefaultFile)).StateHash(), "completed point restores");
				}
			}
		Assert.True(types.Contains(NodeTypeRegistry.Merchant) && types.Contains(NodeTypeRegistry.Dojo), "both stops visited");
	});

	[Test]
	public static void KnownOpen_MerchantHealThenQuit_ContinueOffersTheHealAgain() => WithFixtures(() =>
	{
		// Rules are silent on quitting inside a stop (designer question, same class as retry-by-quitting). A heal taken but not yet completed is not
		// in the file, so Continue returns to the stop with its entry health and the choice open again. Flip if a choice should be final.
		var s = NewSession(63);
		while (s.Reachable().Select(id => s.Map.Graph.Node(id).Type).All(t => t != NodeTypeRegistry.Merchant))
		{
			s.Enter(s.Reachable()[0]);
			if (s.Merchant is { } mm) mm.TakeHeal(); else if (s.Dojo is { } dd) dd.Rest(); else WinAndPick(s, 500, 0);
			s.CompleteNode();
		}
		s.Enter(s.Reachable().First(id => s.Map.Graph.Node(id).Type == NodeTypeRegistry.Merchant));
		string atEntry = Text(s, FileMapSource.DefaultFile);
		int entryHealth = s.Run.Health;
		Assert.True(s.Merchant!.TakeHeal().Ok && s.Run.Health > entryHealth, "the heal healed");
		var back = RestoreText(atEntry);
		Assert.True(back.Run.Health == entryHealth && back.Merchant is { CanHeal: true }, "KnownOpen: after quitting, the heal is offered again at the entry health");
	});

	[Test]
	public static void V2_RepairedLevelUpOrder_InsideTheBoss_WithASavedLock_IsRefused() => WithFixtures(() =>
	{
		// Critic round 3: with a repaired T3 order the saved lock cannot be cross-checked (the tie-break would be a guess), so the run is refused,
		// as the version 1 branch does. Pinned: flip to "restores with a note" if the designer prefers keeping the run on tie-break damage.
		var pool = FightScene.LoadAbilityPool();
		var save = RunSave.Capture(WalkToBoss(909, true), FileMapSource.DefaultFile);
		var leveled = save.Slots.Select(x => pool.Special(x.Special)!.MaxLevel >= 2 ? x with { Level = 2, Levelled = 0 } : x).ToArray();
		Assert.True(leveled.Count(x => x.Level == 2) >= 2, "two specials can level");
		var first = leveled.First(x => x.Level == 2);
		var v2 = save with { Slots = leveled, LockedCopy = new LockedCopySave(first.Slot, first.Special, 2) };
		Refused(() => v2.Restore(pool, DefaultMap(), Table()), "v2 inside the boss with a repaired level-up order");
		// before the boss the same damage is still repaired, never a reason to lose the run (the lock does not exist yet)
		var early = RunSave.Capture(WalkToBoss(909, false), FileMapSource.DefaultFile);
		var el = early.Slots.Select(x => pool.Special(x.Special)!.MaxLevel >= 2 ? x with { Level = 2, Levelled = 0 } : x).ToArray();
		var notes = new List<string>();
		Assert.True((early with { Slots = el }).Restore(pool, DefaultMap(), Table(), notes) != null && notes.Any(x => x.Contains("level-up order")), "before the boss: repaired with a note");
	});
}
