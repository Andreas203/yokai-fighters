using System;
using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.Dev;
using YokaiFighters.Fight;
using YokaiFighters.Flow;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-125 dev mode Jump page: every built target opens from the title and the map without errors, greyed targets
/// refuse with their reason, fight setups reach the fight scene (opponent, temperament, elder, stage, scheme), the
/// Tanuki's chosen copy is locked on entering its node (T1, AM6), fights start deterministically, jumps are refused
/// mid-fight or with dev mode off, and a dev jump never writes a save (neither the player's file nor the dev slot).
/// </summary>
public static class DevJumpTests
{
	static void WithDev(Action body)
	{
		DevMode.Configure(new DevModeState(true, true));
		try { body(); }
		finally { DevMode.ResetToLaunch(); DevRunStates.Reset(); }
	}

	static void WithBoot(Node runner, string? saveFile, Action<GameFlow> body)
	{
		var saved = FightScene.Sources;
		var savedRun = FightScene.Run;
		var savedFoe = FightScene.Opponent;
		bool savedStage = FightScene.UsePlaceholderStage;
		FightScene.Sources = FightScene.FixtureSources;
		FightScene.Run = null;
		MenuSettings.Reset();
		var flow = GD.Load<PackedScene>("res://scenes/boot.tscn").Instantiate<GameFlow>();
		flow.QuitAction = () => { };
		flow.FixedRunSeed = 91;
		flow.SaveFile = saveFile;
		flow.ConfigureFight = f => { f.ExternalDrive = true; f.PauseEnabled = true; f.HeldBitsSource = () => InputBits.None; };
		var stage = new SubViewport { Size = new Vector2I(1920, 1080), Name = "DevJumpStage" };
		runner.AddChild(stage);
		try { stage.AddChild(flow); body(flow); }
		finally
		{
			Shell.Menu.Close();
			flow.DevScreen?.Finish();
			runner.RemoveChild(stage);
			stage.QueueFree();
			MenuSettings.Reset();
			FightScene.Sources = saved;
			FightScene.Run = savedRun;
			FightScene.Opponent = savedFoe;
			FightScene.UsePlaceholderStage = savedStage;
		}
	}

	static DevShell Shell => DevShell.Instance ?? throw new AssertFailed("the DevShell autoload is missing");

	/// <summary>Opens the dev menu where the flow is (title or map) on the Jump page.</summary>
	static DevJumpPage OpenJump(GameFlow flow)
	{
		Assert.True(flow.OpenDevMenu(), "the dev menu opens on " + flow.Router.Current);
		Assert.True(Shell.Menu.ShowSection(DevMenu.JumpId) && Shell.Menu.Page is DevJumpPage, "the Jump page");
		return (DevJumpPage)Shell.Menu.Page!;
	}

	/// <summary>Back to the title from wherever a jump left the flow.</summary>
	static void Leave(GameFlow flow)
	{
		flow.DevScreen?.Finish();
		if (flow.Router.Current != Screen.Title) flow.ReturnToTitle();
		Assert.Equal(Screen.Title, flow.Router.Current, "back on the title");
	}

	static void CheckOpened(GameFlow flow, DevJumpTarget t)
	{
		Assert.True(!Shell.Menu.IsOpen, t.Id + ": the dev menu closed for the jump");
		switch (t.Kind)
		{
			case DevJumpKind.Fight:
				Assert.True(flow.Router.Current == Screen.Fight && flow.Fight is { } f && f.IsInsideTree() && f.Node.NodeId == DevJump.FightNodeId, t.Id + ": the fight runs the dev node");
				break;
			case DevJumpKind.RestStop:
				Assert.True(flow.Router.Current == Screen.Fight && flow.NodeScene is RestStopScreen { Model: not null } stop
					&& stop.Model.Kind == (t.Arg == NodeTypeRegistry.Merchant ? RestStopKind.Merchant : RestStopKind.Dojo), t.Id + ": the rest stop is bound to the run");
				break;
			default:
				Assert.True(flow.DevScreen is { IsClosed: false } host && host.Screen.IsInsideTree() && host.Screen.Visible, t.Id + ": the screen shows in the dev viewer");
				Assert.True(!flow.Title.Visible && !flow.Map.Visible, t.Id + ": the menu underneath is hidden");
				break;
		}
	}

	// ---- the catalog ----

	[Test]
	public static void Catalog_ListsEveryStoryCardAndTrial_GreysWhatIsNotBuilt()
	{
		var targets = DevJump.Targets();
		Assert.True(targets.Select(t => t.Id).Distinct().Count() == targets.Count, "ids are unique");
		foreach (var c in DevJump.StoryCards())
			Assert.True(targets.Any(t => t.Id == "story:" + c.Id && t.Built), "story card listed and built: " + c.Id);
		foreach (string id in new[] { "intro-soen", "binding-line", "merchant-row4", "first-loss", "lose-early", "boss-reveal", "win-1", "win-2", "win-3" })
			Assert.True(targets.Any(t => t.Id == "story:" + id), "S3 card listed: " + id);
		var trials = targets.Where(t => t.Kind == DevJumpKind.Trial).ToList();
		Assert.True(trials.Count >= 1 && trials.All(t => !t.Built && t.NotBuilt!.Contains(DevJump.NotBuiltYet)), "dojo trials greyed: not built yet (no trial runner on main)");
		Assert.True(TrialLoader.LoadDirectory(ContentPaths.Data("trials")).All(tr => trials.Any(t => t.Arg == tr.Id)), "one entry per trial in data/trials");
		foreach (var k in new[] { DevJumpKind.Fight, DevJumpKind.RewardDraft, DevJumpKind.RestStop, DevJumpKind.LoseScreen, DevJumpKind.RunComplete, DevJumpKind.ControlSelect, DevJumpKind.MoveList })
			Assert.True(targets.Any(t => t.Kind == k && t.Built), "built: " + k);
		Assert.Equal(2, targets.Count(t => t.Kind == DevJumpKind.RestStop && t.Built), "merchant and dojo");

		var foes = DevJump.Opponents();
		Assert.True(foes.First(o => o.Yokai == "kitsune").Built && foes.First(o => o.Yokai == DevJump.TanukiId).Built, "Kitsune and Tanuki have fighters");
		var table = DevJump.Table();
		foreach (var o in foes.Where(o => DevJump.OpponentIds.Contains(o.Yokai)))
		{
			bool standIn = table.Resolve(new MapNodeData("probe", 2, o.Yokai == DevJump.TanukiId ? NodeTypeRegistry.Boss : NodeTypeRegistry.Duel, o.Yokai)).Context.StandIn;
			bool body = o.Yokai != DevJump.TanukiId && FighterAnimSet.HasBody(o.Yokai); // YOK-131: Oni/Kappa bodies on the stand-in kit
			Assert.Equal(!standIn || body, o.Built, o.Yokai + ": built when node-scenes gives it its own fighter or it has a body of its own");
		}
		Assert.True(foes.Any(o => o.Yokai == "human-guises" && !o.Built), "human guises greyed");
		Assert.Equal(3, DevJump.Elders.Length, "one elder per yokai (Y table)");
	}

	// ---- every target opens ----

	[Test]
	public static void EveryBuiltTarget_OpensFromTheTitle_AndFromTheMap(Node runner) => WithDev(() => WithBoot(runner, null, flow =>
	{
		var built = DevJump.Targets().Where(t => t.Built).ToList();
		foreach (var t in built)
		{
			var page = OpenJump(flow);
			Assert.True(page.Jump(t.Id), $"{t.Id} from the title: {page.Status}");
			CheckOpened(flow, t);
			Leave(flow);
		}
		foreach (var t in built)
		{
			Assert.True(flow.Router.Select(TitleEntry.Start) && flow.Router.Confirm() && flow.Router.Current == Screen.Map, "a run on the map");
			var page = OpenJump(flow);
			Assert.True(page.Jump(t.Id), $"{t.Id} from the map: {page.Status}");
			CheckOpened(flow, t);
			if (t.Kind is not (DevJumpKind.Fight or DevJumpKind.RestStop))
			{
				flow.DevScreen!.Finish();
				Assert.True(flow.Router.Current == Screen.Map && flow.Map.Visible && flow.DevScreen == null, t.Id + ": closing the viewer gives the map back");
			}
			Leave(flow);
		}
	}));

	[Test]
	public static void GreyedTargets_Refuse_WithTheirReason(Node runner) => WithDev(() => WithBoot(runner, null, flow =>
	{
		foreach (var t in DevJump.Targets().Where(t => !t.Built))
		{
			var page = OpenJump(flow);
			Assert.True(page.TargetButtons[t.Id].Disabled, t.Id + ": its button is disabled");
			Assert.True(!page.Jump(t.Id) && page.Status.Contains(DevJump.NotBuiltYet), t.Id + ": refused: " + page.Status);
			Assert.True(flow.Router.Current == Screen.Title && flow.DevScreen == null && Shell.Menu.IsOpen, t.Id + ": nothing opened");
			Shell.Menu.Close();
		}
	}));

	[Test]
	public static void ViewerScreens_FinishOnTheirOwnDoneEvents(Node runner) => WithDev(() => WithBoot(runner, null, flow =>
	{
		var fight = new DevFightSetup();
		var draft = new DevDraftSetup();
		bool Open(string id) => DevJumpPage.Perform(flow, DevJump.Targets().First(t => t.Id == id), fight, draft, null, out _);

		Assert.True(Open("story:binding-line") && flow.DevScreen!.Screen is StoryCard, "binding card");
		var card = (StoryCard)flow.DevScreen!.Screen;
		Assert.True(card.Showing && card.Text.Contains("Kitsune"), "filled with the yokai: " + card.Text);
		card.Continue();
		Assert.True(flow.DevScreen == null && flow.Title.Visible, "Continue closes the viewer");
		Assert.True(Open("results-lose") && flow.DevScreen!.Screen is LoseScreen, "lose screen");
		((LoseScreen)flow.DevScreen!.Screen).Restart.EmitSignal(BaseButton.SignalName.Pressed);
		Assert.True(flow.DevScreen == null, "Restart closes it");
		Assert.True(Open("reward-draft") && flow.DevScreen!.Screen is RewardScreen rewards && rewards.Showing, "reward draft");
		Assert.True(!Open("move-list"), "one viewer at a time");
		flow.DevScreen!.CloseButton.EmitSignal(BaseButton.SignalName.Pressed);
		Assert.True(flow.DevScreen == null && flow.Title.Visible, "Close gives the title back");
	}));

	// ---- fight setup ----

	[Test]
	public static void FightSetup_ReachesTheFight_OpponentTemperamentElderStageScheme(Node runner) => WithDev(() => WithBoot(runner, null, flow =>
	{
		var pool = FightScene.LoadAbilityPool();
		foreach (var foe in DevJump.Opponents().Where(o => o.Built))
			foreach (bool elder in DevJump.ElderOf(foe.Yokai) is null ? new[] { false } : new[] { false, true })
			{
				var probe = new DevFightSetup(foe.Yokai, Elder: elder);
				foreach (string temp in DevJump.Temperaments(DevJump.FighterOf(probe)))
					foreach (var scheme in new[] { ControlScheme.Kihon, ControlScheme.Kata })
					{
						var setup = probe with { Temperament = temp, Scheme = scheme, Stage = scheme == ControlScheme.Kata ? DevStage.Placeholder : DevStage.Grove };
						var jump = DevJump.BuildFight(setup, pool);
						string what = $"{foe.Yokai} elder={elder} {temp} {scheme}";
						Assert.True(flow.StartDevRun(jump.Session, jump.NodeId, scheme, setup.Stage).Ok, what + ": started");
						var f = flow.Fight!;
						Assert.Equal(DevJump.FighterOf(setup), FightScene.Opponent, what + ": opponent fighter");
						Assert.Equal(temp, f.Temperament, what + ": temperament");
						Assert.Equal(scheme, f.SchemeOf(0), what + ": P1 scheme");
						Assert.Equal(elder, f.Node.Elder, what + ": elder node");
						string type = foe.Yokai == DevJump.TanukiId ? NodeTypeRegistry.Boss : elder ? NodeTypeRegistry.ElderDuel : NodeTypeRegistry.Duel;
						Assert.Equal(type, f.Node.Type, what + ": node type");
						bool groveThere = BambooGroveStage.AssetsPresent(FightScene.StageDir);
						Assert.Equal(setup.Stage == DevStage.Grove && groveThere ? FightScene.GroveScene : FightScene.PlaceholderScene, f.StageRoot.SceneFilePath, what + ": stage");
						flow.ReturnToTitle();
					}
			}
		Assert.True(!FightScene.UsePlaceholderStage || OS.GetCmdlineUserArgs().Contains("--stage=placeholder"), "the stage choice does not outlive the jump");
	}));

	[Test]
	public static void BuildFight_RefusesUnknownOpponentsAndElderlessElders()
	{
		var pool = FightScene.LoadAbilityPool();
		foreach (var bad in new[] { new DevFightSetup("nue"), new DevFightSetup(DevJump.TanukiId, Elder: true) })
		{
			bool threw = false;
			try { DevJump.BuildFight(bad, pool); } catch (ArgumentException) { threw = true; }
			Assert.True(threw, $"refused: {bad.Opponent} elder={bad.Elder}");
		}
	}

	// ---- the Tanuki's copy (T1-T4, AM6) ----

	[Test]
	public static void ShapeCopy_MakesTheChosenSpecialAndLevelTheTarget_OrRefusesWhenOnlyReleasingAStarterWould()
	{
		var pool = FightScene.LoadAbilityPool();
		foreach (var s in pool.Specials)
			for (int lv = 1; lv <= Math.Min(3, s.MaxLevel); lv++)
			{
				var run = RunState.NewRun(pool);
				CopyTarget t;
				try { t = DevJump.ShapeCopy(run, pool, s.Id, lv); }
				catch (ArgumentException e)
				{
					Assert.True(e.Message.Contains("not reachable with Ryo's starters"), $"{s.Id} Lv {lv}: refused only for a starter: {e.Message}");
					continue;
				}
				Assert.True(t.Data.Id == s.Id && t.Level == lv && t.Slot == s.Slot, $"{s.Id} Lv {lv}: target");
				Assert.True(CopyRule.Target(run) == t, $"{s.Id} Lv {lv}: the copy rule agrees");
				Assert.Equal(Math.Min(lv, CopyRule.HighestUnevolvedLevel), t.MoveLevel, $"{s.Id} Lv {lv}: T4 move level");
				foreach (var st in pool.Starters.Where(st => st.Slot != s.Slot))
					Assert.True(run.Owns(st.Id), $"{s.Id} Lv {lv}: starter {st.Id} is never released (A1, R6)");
			}
		// A2 cases from the critic: each needs a starter released, so each is refused, and nothing is chosen for it.
		foreach (var (id, lv) in new[] { ("rising-talisman", 1), ("foxfire", 1) })
		{
			if (pool.Special(id) is not { } sp) continue;
			var run = RunState.NewRun(pool);
			var natural = CopyRule.Target(run);
			bool reachable = natural?.Data.Id == id || pool.Starters.Where(st => st.Slot != sp.Slot).All(st => st.Slot > sp.Slot);
			bool threw = false;
			try { DevJump.ShapeCopy(run, pool, id, lv); } catch (ArgumentException) { threw = true; }
			Assert.True(threw != reachable, $"{id} Lv {lv}: refused exactly when a starter would have to go (T3 tie keeps the lower slot)");
			Assert.True(pool.Starters.Where(st => st.Slot != sp.Slot).All(st => run.Owns(st.Id)), $"{id} Lv {lv}: every other starter still held");
		}
		Assert.True(pool.Special("rising-talisman") is null || Throws(() => DevJump.ShapeCopy(RunState.NewRun(pool), pool, "rising-talisman", 1)), "Rising Talisman Lv 1: T3 picks Spirit Wave, refused");
		// Round 2: both refused on the default run (Foxfire sits in slot C, the starters in A and B; T3 keeps the lower slot).
		foreach (string id in new[] { "foxfire", "rising-talisman" })
			if (pool.Special(id) != null)
				Assert.True(Throws(() => DevJump.ShapeCopy(RunState.NewRun(pool), pool, id, 1)), id + " Lv 1 on the default run: refused");

		// A starter above the chosen level can't come back down (A1, R6): refused, the run unchanged.
		foreach (var starter in pool.Starters)
		{
			var high = RunState.NewRun(pool);
			while (high.CanLevelUp(starter.Slot)) high.LevelUp(starter.Slot);
			if (high[starter.Slot]!.Level == 1) continue;
			ulong before = high.StateHash();
			Assert.True(Throws(() => DevJump.ShapeCopy(high, pool, starter.Id, 1)), starter.Id + ": a starter never loses levels");
			Assert.Equal(before, high.StateHash(), starter.Id + ": the refused run is unchanged");
		}
		// A yokai special above the chosen level still comes back down (it can be replaced by a fresh draft, A6).
		var yokaiSpecial = pool.Specials.FirstOrDefault(s => !s.Starter && Math.Min(3, s.MaxLevel) >= 3);
		if (yokaiSpecial != null)
		{
			var run = RunState.NewRun(pool);
			run.Equip(yokaiSpecial);
			while (run.CanLevelUp(yokaiSpecial.Slot)) run.LevelUp(yokaiSpecial.Slot);
			var t = DevJump.ShapeCopy(run, pool, yokaiSpecial.Id, 2);
			Assert.True(t.Data.Id == yokaiSpecial.Id && t.Level == 2, yokaiSpecial.Id + ": Lv 3 comes back down to Lv 2");
		}
		else GD.Print("DevJumpTests: no yokai special reaches Lv 3 in this pool; the come-down case is skipped");
	}

	static bool Throws(Action a)
	{
		try { a(); return false; } catch (ArgumentException) { return true; }
	}

	[Test]
	public static void TanukiJump_LocksTheChosenCopy_AndTheHudShowsItLocked(Node runner) => WithDev(() => WithBoot(runner, null, flow =>
	{
		var pool = FightScene.LoadAbilityPool();
		foreach (var s in pool.Specials)
		{
			int lv = Math.Min(2, s.MaxLevel);
			var setup = new DevFightSetup(DevJump.TanukiId, CopySpecial: s.Id, CopyLevel: lv);
			var jump = DevJump.BuildFight(setup, pool);
			Assert.True(!jump.Session.CopyLocked, s.Id + ": nothing is locked before the Tanuki's node (T1)");
			Assert.True(flow.StartDevRun(jump.Session, jump.NodeId, setup.Scheme).Ok, s.Id + ": Tanuki fight");
			Assert.True(jump.Session.CopyLocked && jump.Session.LockedCopy is { } c && c.Data.Id == s.Id && c.Level == lv, s.Id + ": locked on entering (T1)");
			var hud = flow.Fight!.GetNode<FightHud>("Hud");
			hud.Refresh(flow.Fight.Match);
			Assert.True(hud.CopyTarget.Visible && hud.CopyTarget.State == CopyTalismanState.Locked, s.Id + ": the HUD shows the copy locked (AM6)");
			flow.ReturnToTitle();
		}
		// A yokai fight never shows the talisman (AM6).
		var duel = DevJump.BuildFight(new DevFightSetup("kitsune"), pool);
		Assert.True(flow.StartDevRun(duel.Session, duel.NodeId, ControlScheme.Kihon).Ok, "kitsune fight");
		var h = flow.Fight!.GetNode<FightHud>("Hud");
		h.Refresh(flow.Fight.Match);
		Assert.True(!h.CopyTarget.Visible, "no talisman in a yokai fight");
	}));

	// ---- determinism ----

	[Test]
	public static void DevFights_StartDeterministically(Node runner) => WithDev(() => WithBoot(runner, null, flow =>
	{
		var pool = FightScene.LoadAbilityPool();
		foreach (var setup in new[]
		{
			new DevFightSetup("kitsune", "aggressive", Seed: 7),
			new DevFightSetup("kitsune", "patient", Elder: true, Scheme: ControlScheme.Kata),
			new DevFightSetup(DevJump.TanukiId, CopySpecial: pool.Starters.First().Id, CopyLevel: 2),
		})
		{
			ulong Run()
			{
				var jump = DevJump.BuildFight(setup, pool);
				Assert.True(flow.StartDevRun(jump.Session, jump.NodeId, setup.Scheme, setup.Stage).Ok, "started");
				var f = flow.Fight!;
				ulong start = f.Match.StateHash() ^ jump.Session.StateHash();
				var rng = new SimRng(3);
				for (int t = 0; t < 600; t++) f.StepWithAi(new FighterInput((InputBits)(rng.Next(1 << 10))));
				ulong end = f.Match.StateHash();
				flow.ReturnToTitle();
				return start * 31 + end;
			}
			Assert.Equal(Run(), Run(), $"{setup.Opponent}: same setup, same inputs, same fight");
		}
	}));

	// ---- refusals and saves ----

	[Test]
	public static void Jumps_RefusedMidFightAndWithDevModeOff(Node runner) => WithBoot(runner, null, flow =>
	{
		WithDev(() =>
		{
			var page = new DevJumpPage(new DevContext(DevPlace.Fight, flow));
			Assert.True(!page.Jump("fight") && page.Status.Contains(DevContext.MidFightReason), "mid-fight: refused");
			Assert.True(flow.Router.Current == Screen.Title && flow.DevScreen == null, "nothing happened");
			page.QueueFree();
			var lone = new DevJumpPage(new DevContext(DevPlace.Title));
			Assert.True(!lone.Jump("move-list") && lone.Status.Contains(DevJumpPage.ReasonNoFlow), "no flow: refused");
			lone.QueueFree();
		});
		DevMode.Configure(new DevModeState(true, false));
		try
		{
			var off = new DevJumpPage(new DevContext(DevPlace.Title, flow));
			Assert.True(!off.Jump("fight") && off.Status.Contains(DevContext.DevOffReason), "dev off: refused");
			off.QueueFree();
		}
		finally { DevMode.ResetToLaunch(); }
	});

	[Test]
	public static void DevJumps_NeverWriteASave_PlayerFileOrDevSlot(Node runner)
	{
		string dir = Path.Combine(Path.GetTempPath(), "yokai-devjump-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(dir);
		try
		{
			string player = Path.Combine(dir, "progress.json"), dev = DevMode.SavePathFor(player);
			const string playerText = "{ \"version\": 3, \"run\": null, \"unlocks\": [\"player-only-unlock\"], \"story\": { \"seen\": [] } }";
			File.WriteAllText(player, playerText);
			DateTime stamp = File.GetLastWriteTimeUtc(player);
			WithDev(() => WithBoot(runner, player, flow =>
			{
				Assert.Equal(dev, flow.Saves!.Path, "dev mode is on the dev slot");
				foreach (string id in new[] { "fight", "rest-merchant", "rest-dojo" })
				{
					var page = OpenJump(flow);
					Assert.True(page.Jump(id), id + ": " + page.Status);
					var s = flow.Session!;
					if (id == "fight") { s.LoseFight(); }
					else { Assert.True(s.CompleteNode().Ok && flow.Router.Current == Screen.Map, id + ": left the stop, on the map"); }
					Leave(flow);
				}
			}));
			Assert.Equal(playerText, File.ReadAllText(player), "the player's file is untouched");
			Assert.Equal(stamp, File.GetLastWriteTimeUtc(player), "and not rewritten");
			Assert.True(!File.Exists(dev), "no dev-jump session reaches the dev slot either");
		}
		finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
	}

	// ---- code-critic round 1: the jump leaves nothing behind ----

	[Test]
	public static void JumpScheme_DoesNotOutliveTheJump_NormalPlayUsesTheSavedScheme(Node runner) => WithDev(() => WithBoot(runner, null, flow =>
	{
		var saved = MenuSettings.Scheme;
		var other = saved == ControlScheme.Kata ? ControlScheme.Kihon : ControlScheme.Kata;
		var pool = FightScene.LoadAbilityPool();
		var jump = DevJump.BuildFight(new DevFightSetup(Scheme: other), pool);
		Assert.True(flow.StartDevRun(jump.Session, jump.NodeId, other).Ok && flow.Fight!.SchemeOf(0) == other, "the jump plays its own scheme");
		Assert.True(flow.ReturnToTitle() && flow.Router.Scheme == saved, $"back on the title: the saved scheme ({saved}) again (E18)");
		Assert.True(flow.Router.Select(TitleEntry.Start) && flow.Router.Confirm(), "a normal run");
		Assert.True(flow.ChooseNode("r1-apprentice").Ok && flow.Fight!.SchemeOf(0) == saved, "the normal run plays the saved scheme");
		flow.ReturnToTitle();
	}));

	[Test]
	public static void RestartAfterAJump_GoesToTheTitle_NotANewRun(Node runner) => WithDev(() => WithBoot(runner, null, flow =>
	{
		var jump = DevJump.BuildFight(new DevFightSetup(), FightScene.LoadAbilityPool());
		Assert.True(flow.StartDevRun(jump.Session, jump.NodeId, ControlScheme.Kihon).Ok, "jumped");
		jump.Session.LoseFight();
		Assert.True(flow.RestartRun() && flow.Router.Current == Screen.Title && flow.Session == null, "Restart: the title, no new run");
		// A normal run still restarts into a new run.
		Assert.True(flow.Router.Select(TitleEntry.Start) && flow.Router.Confirm() && flow.ChooseNode("r1-apprentice").Ok, "a normal fight");
		var before = flow.Session;
		Assert.True(flow.RestartRun() && flow.Router.Current == Screen.Map && flow.Session != before, "a normal Restart: a fresh run on the map");
		flow.ReturnToTitle();
	}));

	[Test]
	public static void RefusedJump_ChangesNothing(Node runner) => WithDev(() => WithBoot(runner, null, flow =>
	{
		Assert.True(flow.Router.Select(TitleEntry.Start) && flow.Router.Confirm(), "a run on the map");
		var run = flow.Session;
		var bad = DevJump.BuildRestStop(NodeTypeRegistry.Merchant, FightScene.LoadAbilityPool());
		Assert.True(!flow.StartDevRun(bad.Session, DevJump.AfterStopNodeId, ControlScheme.Kata).Ok, "a node the map does not allow yet: refused");
		Assert.True(flow.Router.Current == Screen.Map && flow.Session == run && flow.Map.Visible && flow.Router.Scheme == MenuSettings.Scheme, "still on the same run's map, scheme unchanged");
		Assert.True(bad.Session.Node is null, "the refused session entered nothing");
		flow.ReturnToTitle();
	}));

	[Test]
	public static void ControlSelectView_RestoresThePlayersScheme(Node runner) => WithDev(() => WithBoot(runner, null, flow =>
	{
		var saved = MenuSettings.Scheme;
		Assert.True(DevJumpPage.Perform(flow, DevJump.Targets().First(t => t.Id == "control-select"), new DevFightSetup(), new DevDraftSetup(), null, out _), "control select in the viewer");
		var screen = (StartScreen)flow.DevScreen!.Screen;
		(saved == ControlScheme.Kata ? screen.KihonButton : screen.KataButton).EmitSignal(BaseButton.SignalName.Pressed);
		Assert.True(MenuSettings.Scheme != saved, "the real screen switches the scheme while open");
		flow.DevScreen!.Finish();
		Assert.Equal(saved, MenuSettings.Scheme, "closing the dev view puts the player's scheme back");

		// Leaving the tree without Close (quit with the view open) restores it too.
		Assert.True(DevJumpPage.Perform(flow, DevJump.Targets().First(t => t.Id == "control-select"), new DevFightSetup(), new DevDraftSetup(), null, out _), "opened again");
		var host = flow.DevScreen!;
		screen = (StartScreen)host.Screen;
		(saved == ControlScheme.Kata ? screen.KihonButton : screen.KataButton).EmitSignal(BaseButton.SignalName.Pressed);
		host.GetParent().RemoveChild(host);
		Assert.Equal(saved, MenuSettings.Scheme, "the view leaving the tree puts the scheme back");
		host.Finish();
	}));

	// ---- reward draft ----

	[Test]
	public static void RewardDraft_IsSeeded_AndTheRaritySpreadDecidesRares()
	{
		var pool = FightScene.LoadAbilityPool();
		foreach (string yokai in DevJump.DraftYokai(pool))
		{
			string Ids(uint seed, bool rares) => string.Join(",", DevJump.BuildDraft(pool, yokai, seed, rares).Draft.Offer.Cards.Select(c => c.Kind + ":" + c.AbilityId));
			for (uint seed = 0; seed < 8; seed++) Assert.Equal(Ids(seed, false), Ids(seed, false), $"{yokai} seed {seed}: same draft");
			bool Rare(string id) => pool.Special(id)?.Rarity == "rare" || pool.Modifier(id)?.Rarity == "rare";
			for (uint seed = 0; seed < 32; seed++)
				Assert.True(DevJump.BuildDraft(pool, yokai, seed, false).Draft.Offer.Cards.All(c => c.AbilityId is null || !Rare(c.AbilityId)), $"{yokai} seed {seed}: a duel's spread offers no rare (A9)");
			bool hasRare = pool.Specials.Any(s => s.Source == yokai && s.Rarity == "rare" && !s.Locked && !s.Starter);
			if (hasRare)
				Assert.True(Enumerable.Range(0, 64).Any(seed => DevJump.BuildDraft(pool, yokai, (uint)seed, true).Draft.Offer.Cards.Any(c => c.AbilityId is { } id && Rare(id))), $"{yokai}: the elder's spread can offer its rare");
		}
	}
}
