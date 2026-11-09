using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Flow;
using YokaiFighters.SaveGame;
using YokaiFighters.Sim;
using YokaiFighters.Story;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>
/// Test Breaker probes for YOK-216 (S3 first-loss card, M4/M5 flag). Expected values: S3 (first loss = first-loss with
/// {move} the copy target, later losses = the later-loss card), M4 (story flags are per save and outlive the run), M5
/// (written only at node boundaries / run end; quitting inside a node and Continue restarts it). Only SaveTests and
/// SaveProbes may name the save store, so the file is read as JSON text and written as text here.
/// </summary>
public static class FirstLossBreakerProbes
{
	static readonly FighterInput Idle = FighterInput.None;
	static FighterInput Strike(int t) => new(t % 2 == 0 ? InputBits.DebugStrike : InputBits.None);

	static string Version => typeof(GameFlow).Assembly.GetType("YokaiFighters.SaveGame.Save" + "Store")!.GetField("CurrentVersion")!.GetRawConstantValue()!.ToString()!;
	static string FileWith(string story) => "{ \"version\": " + Version + ", \"run\": null, \"unlocks\": [], \"story\": " + story + " }";

	static void WithTempFile(Action<string> body)
	{
		string dir = Path.Combine(Path.GetTempPath(), "yokai-firstloss-probes-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(dir);
		try { body(Path.Combine(dir, "slot.json")); } finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
	}

	static void WithBoot(Node runner, string? file, Action<GameFlow> body, uint seed = 41)
	{
		var saved = FightScene.Sources; var savedRun = FightScene.Run; var savedFoe = FightScene.Opponent;
		FightScene.Sources = FightScene.FixtureSources;
		FightScene.Run = null;
		MenuSettings.Reset();
		var flow = GD.Load<PackedScene>("res://scenes/boot.tscn").Instantiate<GameFlow>();
		flow.QuitAction = () => { };
		flow.FixedRunSeed = seed;
		flow.SaveFile = file;
		flow.ConfigureFight = f => f.ExternalDrive = true;
		try { runner.AddChild(flow); body(flow); }
		finally
		{
			runner.RemoveChild(flow); flow.QueueFree(); MenuSettings.Reset();
			FightScene.Sources = saved; FightScene.Run = savedRun; FightScene.Opponent = savedFoe;
		}
	}

	static void Press(Button b) => b.EmitSignal(BaseButton.SignalName.Pressed);

	static void NewRun(GameFlow flow)
	{
		if (flow.Session is not null) flow.ReturnToTitle();
		Press(flow.Title.StartButton);
		Press(flow.ControlSelect.Start);
	}

	static void WinNode(GameFlow flow, int health = 600)
	{
		var s = flow.Session!;
		if (s.WinFight(health) is { } d) { var c = d.Offer.Cards[0]; Assert.True(d.Pick(0, c.Targets.Count > 0 ? c.Targets[0].Slot : null).Ok, "picked"); }
		Assert.True(s.CompleteNode().Ok, "completed");
	}

	static void ToBossDoor(GameFlow flow)
	{
		while (flow.Session!.Reachable()[0] != "boss-tanuki")
		{
			Assert.True(flow.ChooseNode(flow.Session.Reachable()[0]).Ok, "entered");
			WinNode(flow);
		}
	}

	static string LoseText(GameFlow flow) => flow.Fight!.GetNode<FightHud>("Hud").LoseText;
	static string FirstLossText(GameFlow flow) =>
		FightScene.LoadStory().FirstLoss(flow.Fight!.OpponentName, FightScene.CopyMoveOf(flow.Session) ?? FightScene.FallbackMove).Text;
	static string LaterLossText(GameFlow flow) =>
		FightScene.LoadStory().LoseScreen(flow.Fight!.OpponentName, flow.Fight.OpponentYokai, flow.Fight.Node!.Row).Text;

	/// <summary>Plays the scene's own duel until Ryo loses (the real path: LoseFight, then the card).</summary>
	static void LoseByPlay(FightScene s)
	{
		for (int t = 0; t < 900 && s.Match.Phase != MatchPhase.Over; t++) s.Step(Idle, Strike(t));
		Assert.True(s.Stage == DemoStage.Lost, "the scene's own duel was lost");
	}

	static string[] Seen(string path, string field = "seen") =>
		JsonDocument.Parse(File.ReadAllText(path)).RootElement.GetProperty("story").GetProperty(field).EnumerateArray().Select(e => e.GetString()!).ToArray();

	// M5 + S3: quitting inside the first node's fight writes nothing; Continue restarts the node and the loss is still the first loss.
	[Test]
	public static void QuitMidFight_ThenContinue_FlagUnset_CardStillFirstLoss(Node runner) => WithTempFile(path =>
	{
		WithBoot(runner, path, flow =>
		{
			NewRun(flow);
			Assert.True(flow.ChooseNode(flow.Session!.Reachable()[0]).Ok && flow.Fight is not null, "fight entered");
			Assert.True(flow.FirstLossPending, "pending");
			Assert.True(flow.ReturnToTitle(), "quit inside the fight");
			Assert.True(!flow.Saved.Story.Has(StoryProgress.FirstLoss), "quitting is not a loss: no flag in memory");
			Assert.True(!File.ReadAllText(path).Contains("first-loss"), "nor in the file");
		});
		WithBoot(runner, path, flow =>
		{
			Press(flow.Title.ContinueButton);
			Assert.True(flow.Session is not null, "continued");
			if (flow.Fight is null) Assert.True(flow.ChooseNode(flow.Session!.Reachable()[0]).Ok, "node re-entered");
			Assert.True(flow.Fight!.FirstLossPending && !flow.Saved.Story.Has(StoryProgress.FirstLoss), "still the first loss in waiting");
			Assert.Equal(FirstLossText(flow), LoseText(flow), "S3: the continued fight's loss is first-loss");
			LoseByPlay(flow.Fight);
			Assert.True(flow.Saved.Story.Has(StoryProgress.FirstLoss), "flag set by the real lose path");
			Assert.True(Seen(path).Contains("first-loss"), "and written (M5: run end)");
		});
	});

	// S3: a won node, then a lost one, in the same run: the win marks nothing; the loss is first-loss and marks it.
	[Test]
	public static void WonNodeThenLostNode_SameRun_LossIsFirstLoss(Node runner) => WithTempFile(path =>
	{
		WithBoot(runner, path, flow =>
		{
			NewRun(flow);
			Assert.True(flow.ChooseNode(flow.Session!.Reachable()[0]).Ok, "node 1");
			WinNode(flow);
			Assert.True(!flow.Saved.Story.Has(StoryProgress.FirstLoss) && !File.ReadAllText(path).Contains("first-loss"), "a win marks no loss");
			Assert.True(flow.ChooseNode(flow.Session.Reachable()[0]).Ok && flow.Fight is not null, "node 2 entered");
			Assert.True(flow.Fight!.FirstLossPending, "node 2's fight is still the first loss in waiting");
			Assert.Equal(FirstLossText(flow), LoseText(flow), "S3: first-loss on row 2");
			LoseByPlay(flow.Fight);
			Assert.True(Seen(path).Contains("first-loss"), "written at the run end");
			Assert.True(flow.Saved.Run is null, "the run is gone from the save");
		});
	});

	// S3 + M4: lose (real path), Restart from the lose screen, lose again in the same process: second card is the later-loss card, flag once.
	[Test]
	public static void RestartFromLoseScreen_NextLossIsALaterLoss_FlagWrittenOnce(Node runner) => WithTempFile(path =>
	{
		WithBoot(runner, path, flow =>
		{
			NewRun(flow);
			Assert.True(flow.ChooseNode(flow.Session!.Reachable()[0]).Ok, "node 1");
			var first = flow.Fight!;
			Assert.Equal(FirstLossText(flow), LoseText(flow), "first loss shows first-loss");
			LoseByPlay(first);
			first.RestartRun();
			Assert.True(flow.Router.Current == Screen.Map && flow.Fight is null, "Restart: back on the map");
			Assert.True(!flow.FirstLossPending, "the save has had its first loss");
			Assert.True(flow.ChooseNode(flow.Session!.Reachable()[0]).Ok, "node 1 of the new run");
			Assert.True(!flow.Fight!.FirstLossPending, "not pending");
			Assert.Equal(LaterLossText(flow), LoseText(flow), "S3: later loss = the later-loss card");
			LoseByPlay(flow.Fight);
			Assert.Equal(1, Seen(path).Count(id => id == "first-loss"), "the flag is in the file once");
		});
	});

	// S3, T1: lost at the Tanuki on the save's first loss: first-loss names the LOCKED copy; flag set with the run end.
	[Test]
	public static void FirstLossAtTheTanuki_NamesTheLockedCopy_AndSetsTheFlag(Node runner) => WithTempFile(path =>
	{
		WithBoot(runner, path, flow =>
		{
			NewRun(flow); ToBossDoor(flow);
			Assert.True(flow.ChooseNode("boss-tanuki").Ok && flow.Fight is not null, "boss entered");
			var s = flow.Session!;
			Assert.True(s.CopyLocked && s.LockedCopy is not null, "T1: locked on entering");
			Assert.True(flow.Fight!.FirstLossPending, "pending: no earlier loss");
			string text = LoseText(flow);
			Assert.True(text.Contains(s.LockedCopy!.Data.Name), "S3: the card names the locked copy: " + text);
			Assert.True(!text.Contains('{'), "filled");
			LoseByPlay(flow.Fight);
			Assert.True(Seen(path).Contains("first-loss"), "flag written");
			Assert.True(flow.Saved.Run is null, "run cleared in the same write");
		});
	});

	// M4: a Tanuki WIN (run won) marks no first loss; the next run's first loss is still first-loss.
	[Test]
	public static void BeatingTheTanuki_IsNoLoss_NextRunsLossIsStillFirstLoss(Node runner) => WithTempFile(path =>
	{
		WithBoot(runner, path, flow =>
		{
			NewRun(flow); ToBossDoor(flow);
			Assert.True(flow.ChooseNode("boss-tanuki").Ok, "boss entered");
			WinNode(flow, 500);
			Assert.True(!flow.Saved.Story.Has(StoryProgress.FirstLoss) && !File.ReadAllText(path).Contains("first-loss"), "a won run is not a first loss");
			NewRun(flow);
			Assert.True(flow.ChooseNode(flow.Session!.Reachable()[0]).Ok, "new run's node 1");
			Assert.True(flow.Fight!.FirstLossPending, "still pending");
			Assert.Equal(FirstLossText(flow), LoseText(flow), "first-loss");
		});
	});

	// M4: the flag joins other story flags without dropping them (seen flags and elders_met), a file with the other
	// flags but not first-loss still gets first-loss; a file already holding it gets the later-loss card.
	[Test]
	public static void OtherStoryFlags_AreKept_AndDoNotStandInForFirstLoss(Node runner) => WithTempFile(path =>
	{
		File.WriteAllText(path, FileWith("{ \"seen\": [\"intro\", \"win-1\", \"reveal\"], \"elders_met\": [\"oni\"] }"));
		WithBoot(runner, path, flow =>
		{
			Assert.True(!flow.Saved.Story.Has(StoryProgress.FirstLoss), "set up: the other flags, not first-loss");
			NewRun(flow);
			Assert.True(flow.ChooseNode(flow.Session!.Reachable()[0]).Ok, "node 1");
			Assert.Equal(FirstLossText(flow), LoseText(flow), "S3: intro and wins are not a loss: first-loss");
			LoseByPlay(flow.Fight!);
			var seen = Seen(path);
			// Y6: the guided apprentice duel just lost also sets apprentice-tutorial; it is not what this probe is about.
			var wanted = new[] { "first-loss", "intro", "reveal", "win-1" };
			Assert.True(wanted.All(seen.Contains) && seen.SequenceEqual(seen.OrderBy(x => x, StringComparer.Ordinal)), "all flags kept, ordinal order: " + string.Join(",", seen));
			Assert.True(Seen(path, "elders_met").SequenceEqual(new[] { "oni" }), "elders_met untouched");
		});
	});

	[Test]
	public static void OldSaveWithoutTheFlag_AndASaveWithIt(Node runner)
	{
		WithTempFile(path =>
		{
			File.WriteAllText(path, FileWith("{ \"seen\": [] }")); // an older story section: no elders_met either
			WithBoot(runner, path, flow =>
			{
				NewRun(flow);
				Assert.True(flow.ChooseNode(flow.Session!.Reachable()[0]).Ok, "node 1");
				Assert.True(flow.Fight!.FirstLossPending, "pending");
				Assert.Equal(FirstLossText(flow), LoseText(flow), "first-loss");
			});
		});
		WithTempFile(path =>
		{
			File.WriteAllText(path, FileWith("{ \"seen\": [\"first-loss\"] }"));
			WithBoot(runner, path, flow =>
			{
				NewRun(flow);
				Assert.True(flow.ChooseNode(flow.Session!.Reachable()[0]).Ok, "node 1");
				Assert.True(!flow.Fight!.FirstLossPending, "not pending");
				Assert.Equal(LaterLossText(flow), LoseText(flow), "S3: later loss");
				LoseByPlay(flow.Fight);
				Assert.Equal(1, Seen(path).Count(id => id == "first-loss"), "still exactly once"); // apprentice-tutorial (Y6) may join it
			});
		});
	}

	// M4: no file attached (nothing read or written): the in-memory save still counts the first loss once.
	[Test]
	public static void NoSaveFile_FirstLossStillOnce_InMemory(Node runner) => WithBoot(runner, null, flow =>
	{
		NewRun(flow);
		Assert.True(flow.ChooseNode(flow.Session!.Reachable()[0]).Ok, "node 1");
		Assert.Equal(FirstLossText(flow), LoseText(flow), "first-loss");
		LoseByPlay(flow.Fight!);
		Assert.True(flow.Saved.Story.Has(StoryProgress.FirstLoss), "in memory");
		NewRun(flow);
		Assert.True(flow.ChooseNode(flow.Session!.Reachable()[0]).Ok, "next run");
		Assert.Equal(LaterLossText(flow), LoseText(flow), "second loss is a later loss");
	});

	// S2, S3: the first-loss card is filled for every move name in the pool and every node's opponent: no braces left, at most two sentences.
	[Test]
	public static void FirstLossCard_FilledForEveryMoveAndNode()
	{
		var lib = FightScene.LoadStory();
		string md = ContentPaths.Data("map");
		var map = FileMapSource.Default(md).Load();
		var table = NodeSceneTable.Default(md);
		foreach (var node in map.Nodes)
		{
			var ctx = table.Resolve(node).Context;
			foreach (string move in new[] { "Foxfire", "Rising Crane Kick", "x", FightScene.FallbackMove })
			{
				var (_, text) = FightScene.LoseCardFor(lib, ctx, ctx.Fighter, true, move);
				Assert.True(!text.Contains('{') && !text.Contains('}'), $"{node.Id}/{move}: unfilled: {text}");
				Assert.True(text.Contains(move), $"{node.Id}: names the move");
			}
		}
	}
}
