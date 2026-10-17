using System;
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
/// Test Breaker probes for YOK-73 (title Continue). Expected values: M5 (a node restarts from its entry state), T1
/// (the copy locks on entering the boss node), and the ticket's greyed-reason list. The save file is reached only
/// through the boot scene's SaveFile (the isolation probe is the only place that names the player's file).
/// </summary>
public static class TitleContinueProbes
{
	const string Bak = ".bak";

	static System.Text.Json.Nodes.JsonObject Edit(string path, Action<System.Text.Json.Nodes.JsonObject> change)
	{
		var root = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!.AsObject();
		change(root["run"]!.AsObject());
		File.WriteAllText(path, root.ToJsonString());
		return root;
	}

	static void WithTemp(Action<string> body)
	{
		string dir = Path.Combine(Path.GetTempPath(), "yokai-tc-probes-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(dir);
		try { body(Path.Combine(dir, "probe-slot.json")); }
		finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
	}

	static void WithBoot(Node runner, string? saveFile, Action<GameFlow> body, uint seed = 73)
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

	static void StartRun(GameFlow flow)
	{
		Press(flow.Title.StartButton);
		Press(flow.ControlSelect.Start);
		Assert.True(flow.Session != null, "a run started");
	}

	static void Win(GameFlow flow, int healthLeft)
	{
		var s = flow.Session!;
		if (s.WinFight(healthLeft) is { } draft)
		{
			var card = draft.Offer.Cards[0];
			Assert.True(draft.Pick(0, card.Targets.Count > 0 ? card.Targets[0].Slot : null).Ok, "picked");
		}
		Assert.True(s.CompleteNode().Ok, "completed");
	}

	static void NotBuiltNotSaid(GameFlow flow)
	{
		Assert.True(!flow.Title.DisabledReason(TitleEntry.Continue).Contains("not built", StringComparison.OrdinalIgnoreCase), "Continue's reason must not say 'not built': " + flow.Title.DisabledReason(TitleEntry.Continue));
		Assert.Equal(ScreenRouter.NoPracticeReason, flow.Title.DisabledReason(TitleEntry.Practice), "Practice keeps its own reason");
	}

	// Ticket: a corrupt file gives "The save file could not be read...". A zero-byte file is not a save.
	[Test]
	public static void ZeroByteFile_ContinueGreyed_NoThrow_FileUntouched(Node runner) => WithTemp(path =>
	{
		File.WriteAllBytes(path, Array.Empty<byte>());
		WithBoot(runner, path, flow =>
		{
			Assert.True(flow.Title.ContinueButton.Disabled && !flow.Router.Entry(TitleEntry.Continue).Enabled, "zero bytes: Continue greyed");
			string reason = flow.Title.DisabledReason(TitleEntry.Continue);
			Assert.True(reason.StartsWith("The save file could not be read") || reason == ScreenRouter.NoSaveReason, "an honest reason: " + reason);
			NotBuiltNotSaid(flow);
			Press(flow.Title.ContinueButton);
			Assert.True(flow.Session == null && flow.Router.Current == Screen.Title, "nothing starts");
			Assert.Equal(0, (int)new FileInfo(path).Length, "file untouched");
		});
	});

	// Valid JSON that is not a save object: same as a file the build cannot read.
	[Test]
	public static void JsonNullFile_ContinueGreyed_NoThrow_StartStillWorks(Node runner) => WithTemp(path =>
	{
		File.WriteAllText(path, "null");
		foreach (string text in new[] { "null", "[]", "42", "\"x\"", "{\"run\": 5}" })
		{
			File.Delete(path + Bak);
			File.WriteAllText(path, text);
			WithBoot(runner, path, flow =>
			{
				Assert.True(flow.Title.ContinueButton.Disabled, $"'{text}': Continue greyed");
				NotBuiltNotSaid(flow);
				Press(flow.Title.ContinueButton);
				Assert.True(flow.Session == null && flow.Router.Current == Screen.Title, $"'{text}': nothing starts");
				Assert.Equal(text, File.ReadAllText(path), $"'{text}': file untouched until a run is saved");
				StartRun(flow);
				Assert.True(flow.ChooseNode(flow.Session!.Reachable()[0]).Ok, $"'{text}': a new run enters a node");
				Assert.True(File.Exists(path + Bak) && File.ReadAllText(path + Bak) == text, $"'{text}': the odd file is kept as .bak before the first rewrite");
			});
		}
	});

	// M5 / T1: a run saved on the boss node re-enters it from its entry state with the copy still locked to the same target.
	[Test]
	public static void BossNodeSave_ContinueReentersBoss_CopyLockedToTheSameTarget(Node runner) => WithTemp(path =>
	{
		string node = "", target = "";
		int health = 0, coins = 0;
		WithBoot(runner, path, flow =>
		{
			StartRun(flow);
			var s = flow.Session!;
			for (int i = 0; i < 20 && !s.CopyLocked; i++)
			{
				Assert.True(flow.ChooseNode(s.Reachable()[0]).Ok, "node entered");
				if (s.CopyLocked) break;
				Win(flow, 700);
			}
			Assert.True(s.CopyLocked && s.LockedCopy != null, "reached the boss node");
			node = s.Node!.NodeId; health = s.Run.Health; coins = s.Run.Coins;
			target = $"{s.LockedCopy!.Slot}:{s.LockedCopy.Data.Id}:{s.LockedCopy.Level}";
			flow.ReturnToTitle();
		});
		WithBoot(runner, path, flow =>
		{
			Assert.True(!flow.Title.ContinueButton.Disabled, "boss save is offered: " + flow.Title.DisabledReason(TitleEntry.Continue));
			Press(flow.Title.ContinueButton);
			var s = flow.Session!;
			Assert.True(flow.Router.Current == Screen.Fight && s.Node?.NodeId == node, "boss node re-entered");
			Assert.True(s.CopyLocked && s.LockedCopy != null, "T1: copy is locked on the boss node");
			Assert.Equal(target, $"{s.LockedCopy!.Slot}:{s.LockedCopy.Data.Id}:{s.LockedCopy.Level}", "same locked target");
			Assert.True(s.Run.Health == health && s.Run.Coins == coins && flow.Fight!.Match.Tick == 0, "entry health and coins, tick 0");
		});
	});

	// A node saved as the current node but also listed as visited (completed) is not a state a walk can reach (R1).
	[Test]
	public static void CurrentNodeAlsoVisited_IsNotOffered_OrIsRefusedCleanly(Node runner) => WithTemp(path =>
	{
		WithBoot(runner, path, flow =>
		{
			StartRun(flow);
			Assert.True(flow.ChooseNode(flow.Session!.Reachable()[0]).Ok, "entered");
			flow.ReturnToTitle();
		});
		Edit(path, r => { string id = r["current"]!.GetValue<string>(); r["visited"] = new System.Text.Json.Nodes.JsonArray(id); });
		string id = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!["run"]!["current"]!.GetValue<string>();
		string before = File.ReadAllText(path);
		WithBoot(runner, path, flow =>
		{
			bool lit = !flow.Title.ContinueButton.Disabled;
			if (lit)
			{
				Press(flow.Title.ContinueButton);
				var s = flow.Session!;
				Assert.True(!s.Map.Visited.Contains(id) || !s.Map.InNode, "a node is never both completed and entered");
			}
			else
			{
				Assert.True(flow.Title.DisabledReason(TitleEntry.Continue).StartsWith("The saved run cannot be continued"), "its own reason: " + flow.Title.DisabledReason(TitleEntry.Continue));
				Assert.Equal(before, File.ReadAllText(path), "file kept");
			}
		});
	});

	// Special id missing from data on a run saved INSIDE a node (the author's test covers the between-nodes case).
	[Test]
	public static void InNodeSave_WithUnknownSpecial_GreyedWithIdInReason_FileKept(Node runner) => WithTemp(path =>
	{
		WithBoot(runner, path, flow =>
		{
			StartRun(flow);
			Assert.True(flow.ChooseNode(flow.Session!.Reachable()[0]).Ok, "entered");
			flow.ReturnToTitle();
		});
		Edit(path, r => { Assert.True(r["current"] != null, "saved inside a node"); r["slots"]![0]!["special"] = "no-such-special"; });
		string before = File.ReadAllText(path);
		WithBoot(runner, path, flow =>
		{
			Assert.True(flow.Title.ContinueButton.Disabled, "greyed");
			Assert.True(flow.Title.DisabledReason(TitleEntry.Continue).Contains("no-such-special"), "reason names it: " + flow.Title.DisabledReason(TitleEntry.Continue));
			Assert.True(!flow.ContinueRun() && flow.Session == null, "forced Continue refused");
			Assert.Equal(before, File.ReadAllText(path), "file kept");
			NotBuiltNotSaid(flow);
		});
	});

	// Press Continue twice: the second is a no-op (same session object, same screen, same state hash, no second fight).
	[Test]
	public static void ContinueTwice_SecondPressChangesNothing(Node runner) => WithTemp(path =>
	{
		WithBoot(runner, path, flow => { StartRun(flow); flow.ChooseNode(flow.Session!.Reachable()[0]); flow.ReturnToTitle(); });
		WithBoot(runner, path, flow =>
		{
			Press(flow.Title.ContinueButton);
			var s = flow.Session; var fight = flow.Fight; ulong hash = s!.StateHash(); var screen = flow.Router.Current;
			Press(flow.Title.ContinueButton);
			Assert.True(!flow.ContinueRun(), "off the title, ContinueRun refuses");
			Assert.True(ReferenceEquals(s, flow.Session) && ReferenceEquals(fight, flow.Fight) && flow.Router.Current == screen && s.StateHash() == hash, "nothing changed");
		});
	});

	// Save deleted between boot and pressing Continue: no crash, and the screen state is coherent.
	[Test]
	public static void SaveDeletedAfterBoot_ContinueDoesNotThrow_AndStateIsCoherent(Node runner) => WithTemp(path =>
	{
		WithBoot(runner, path, flow => { StartRun(flow); flow.ChooseNode(flow.Session!.Reachable()[0]); flow.ReturnToTitle(); });
		WithBoot(runner, path, flow =>
		{
			Assert.True(!flow.Title.ContinueButton.Disabled, "lit at boot");
			File.Delete(path);
			Press(flow.Title.ContinueButton);
			bool started = flow.Session != null;
			Assert.True(started == (flow.Router.Current != Screen.Title), "either Continue ran fully or nothing changed");
			if (started) Assert.True(flow.Session!.Node != null && flow.Fight != null, "a continued in-node run has its fight");
		});
	});

	// After a lost run (file cleared) the title greys Continue with the no-save reason, in the same session.
	[Test]
	public static void AfterALostRun_ContinueGreyed_NoSaveReason(Node runner) => WithTemp(path =>
	{
		WithBoot(runner, path, flow =>
		{
			StartRun(flow);
			Assert.True(flow.ChooseNode(flow.Session!.Reachable()[0]).Ok, "entered");
			flow.ReturnToTitle();
			Assert.True(!flow.Title.ContinueButton.Disabled, "lit after quitting inside a node");
			Press(flow.Title.ContinueButton);
			Assert.True(flow.Session!.LoseFight().Ok, "lost");
			flow.ReturnToTitle();
			Assert.True(flow.Title.ContinueButton.Disabled && !flow.Router.Entry(TitleEntry.Continue).Enabled, "greyed after a lost run");
			Assert.Equal(ScreenRouter.NoSaveReason, flow.Title.DisabledReason(TitleEntry.Continue), "with the no-save reason");
			Assert.True(!flow.ContinueRun(), "cannot be forced");
		});
		WithBoot(runner, path, flow => Assert.True(flow.Title.ContinueButton.Disabled, "and a fresh boot agrees"));
	});

	// Reason text updates: boot with no save (reason A) -> start and quit a run (lit) ; corrupt boot (reason B) -> run saved -> lit, corrupt kept as .bak.
	[Test]
	public static void ReasonUpdates_AfterARunIsStartedAndQuit(Node runner) => WithTemp(path =>
	{
		WithBoot(runner, path, flow =>
		{
			Assert.Equal(ScreenRouter.NoSaveReason, flow.Title.DisabledReason(TitleEntry.Continue), "no save at boot");
			StartRun(flow);
			Assert.True(flow.ChooseNode(flow.Session!.Reachable()[0]).Ok, "entered");
			flow.ReturnToTitle();
			Assert.True(!flow.Title.ContinueButton.Disabled && flow.Title.DisabledReason(TitleEntry.Continue) == "", "lit, no reason shown: '" + flow.Title.DisabledReason(TitleEntry.Continue) + "'");
		});
		const string corrupt = "{ \"version\": 4, \"run\": { broken";
		File.WriteAllText(path, corrupt);
		WithBoot(runner, path, flow =>
		{
			Assert.True(flow.Title.DisabledReason(TitleEntry.Continue).StartsWith("The save file could not be read"), "corrupt reason at boot");
			StartRun(flow);
			Assert.True(flow.ChooseNode(flow.Session!.Reachable()[0]).Ok, "entered");
			flow.ReturnToTitle();
			Assert.True(!flow.Title.ContinueButton.Disabled && flow.Title.DisabledReason(TitleEntry.Continue) == "", "lit after the run saved: '" + flow.Title.DisabledReason(TitleEntry.Continue) + "'");
			Assert.Equal(corrupt, File.ReadAllText(path + Bak), "corrupt file kept as .bak");
		});
	});
}
