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
/// YOK-123 dev mode shell: the flag (release / debug / <c>--dev</c>), the dev save slot, the dev menu's section
/// registry and where it opens (title, map, pause), the DEV badge, and that no dev action reaches a fight in progress.
/// Every test puts <see cref="DevMode"/> back to the launch state.
/// </summary>
public static class DevModeTests
{
	static readonly DevModeState Release = new(false, false), Debug = new(true, false), DevOn = new(true, true);

	static void WithMode(DevModeState state, Action body)
	{
		DevMode.Configure(state);
		try { body(); }
		finally { DevMode.ResetToLaunch(); }
	}

	static void WithTempDir(Action<string> body)
	{
		string dir = Path.Combine(Path.GetTempPath(), "yokai-dev-tests-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(dir);
		try { body(dir); }
		finally { try { Directory.Delete(dir, recursive: true); } catch (IOException) { } }
	}

	static void WithBoot(Node runner, string? saveFile, Action<GameFlow> body, ControlScheme? direct = null)
	{
		var saved = FightScene.Sources;
		var savedRun = FightScene.Run;
		var savedFoe = FightScene.Opponent;
		FightScene.Sources = FightScene.FixtureSources;
		FightScene.Run = null;
		MenuSettings.Reset();
		var flow = GD.Load<PackedScene>("res://scenes/boot.tscn").Instantiate<GameFlow>();
		flow.QuitAction = () => { };
		flow.FixedRunSeed = 91;
		flow.SaveFile = saveFile;
		flow.DirectFight = direct;
		flow.ConfigureFight = f => { f.ExternalDrive = true; f.PauseEnabled = true; f.HeldBitsSource = () => InputBits.None; };
		var stage = new SubViewport { Size = new Vector2I(1920, 1080), Name = "DevTestStage" };
		runner.AddChild(stage);
		try { stage.AddChild(flow); body(flow); }
		finally
		{
			Shell.Menu.Close();
			runner.RemoveChild(stage);
			stage.QueueFree();
			MenuSettings.Reset();
			FightScene.Sources = saved;
			FightScene.Run = savedRun;
			FightScene.Opponent = savedFoe;
		}
	}

	static DevShell Shell => DevShell.Instance ?? throw new AssertFailed("the DevShell autoload is missing (project.godot [autoload])");
	static void Press(Button b) => b.EmitSignal(BaseButton.SignalName.Pressed);
	static Label? FindLabel(Node root, string name) => root.FindChild(name, true, false) as Label;

	// ---- the flag ----

	[Test]
	public static void Flag_OffAndUnreachableInRelease_OnWithDevArg()
	{
		Assert.Equal(Release, DevMode.Evaluate(false, Array.Empty<string>()), "release build, no arg: unavailable and off");
		Assert.Equal(Release, DevMode.Evaluate(false, new[] { "--fight", "--devx", "dev", "--dev=1" }), "only the exact --dev counts");
		Assert.Equal(DevOn, DevMode.Evaluate(false, new[] { "--fight", "--dev" }), "release build with --dev: on");
		Assert.Equal(Debug, DevMode.Evaluate(true, Array.Empty<string>()), "debug build: available, off until toggled");
		Assert.Equal(DevOn, DevMode.Evaluate(true, new[] { "--dev" }), "debug build with --dev: on");

		WithMode(Release, () =>
		{
			Assert.True(!DevMode.TrySetOn(DevPlace.Title, true, out string? why) && why == DevMode.UnavailableReason && !DevMode.On && !DevMode.Available, "release: switching on is refused");
			foreach (var place in Enum.GetValues<DevPlace>()) Assert.True(!DevShell.CanOpen(place), $"release: no dev menu on {place}");
			Assert.True(!Shell.OpenMenu(new DevContext(DevPlace.Title)) && !Shell.Menu.IsOpen, "the shell refuses to open");
			Assert.True(!Shell.Badge.Visible, "no badge");
		});
		WithMode(Debug, () =>
		{
			Assert.True(!DevMode.On && !Shell.Badge.Visible, "debug build starts off, no badge");
			Assert.True(DevMode.TrySetOn(DevPlace.Title, true, out _) && DevMode.On && Shell.Badge.Visible, "the toggle switches it on; the badge shows");
			Assert.True(DevMode.TrySetOn(DevPlace.Title, false, out _) && !DevMode.On && !Shell.Badge.Visible, "and off again");
		});
		Assert.Equal(DevMode.Launch(), DevMode.State, "back to the launch state");
		Assert.True(!DevMode.On || OS.GetCmdlineUserArgs().Concat(OS.GetCmdlineArgs()).Contains(DevMode.Arg), "the test run itself is not in dev mode without --dev");
	}

	[Test]
	public static void DevSlot_IsBesideThePlayersFile_AndNeverThePlayersPath()
	{
		string player = Path.Combine("a", "b", "progress.json");
		Assert.Equal(Path.Combine("a", "b", "progress.dev.json"), DevMode.SavePathFor(player), "same folder, .dev before the extension");
		Assert.Equal("progress.dev", DevMode.SavePathFor("progress"), "no extension");
		foreach (string p in new[] { player, "x.json", "x.dev.json", "/tmp/y/z.json" })
			Assert.True(DevMode.SavePathFor(p) != p, "never the same path: " + p);
		WithMode(DevOn, () => Assert.Equal(DevMode.SavePathFor(player), DevMode.SavePath(player), "dev on: the dev slot"));
		WithMode(Debug, () => Assert.Equal(player, DevMode.SavePath(player), "dev off: the player's file"));
	}

	// ---- the dev save slot in the boot flow ----

	const string PlayerFile = "{ \"version\": 3, \"run\": null, \"unlocks\": [\"player-only-unlock\"], \"story\": { \"seen\": [] } }";

	[Test]
	public static void DevSession_NeverReadsOrWritesThePlayersSave_AndSavesAtNodeBoundariesInTheDevSlot(Node runner) => WithTempDir(dir =>
	{
		string player = Path.Combine(dir, "progress.json"), dev = DevMode.SavePathFor(player);
		File.WriteAllText(player, PlayerFile);
		DateTime stamp = File.GetLastWriteTimeUtc(player);

		WithMode(DevOn, () => WithBoot(runner, player, flow =>
		{
			Assert.Equal(dev, flow.Saves!.Path, "dev mode boots on the dev slot");
			Assert.True(flow.Saved.Unlocks.Count == 0, "the player's file is not read (its unlock is not loaded)");
			Assert.True(flow.Title.ContinueButton.Disabled, "nothing to continue in an empty dev slot");
			Press(flow.Title.StartButton);
			Press(flow.ControlSelect.Start);
			Assert.True(File.Exists(dev), "M5: the new run is saved at once, in the dev slot");
			Assert.True(flow.ChooseNode("r1-apprentice").Ok && flow.Router.Current == Screen.Fight, "row 1 entered");
			string entered = File.ReadAllText(dev);
			Assert.True(entered.Contains("\"current\": \"r1-apprentice\""), "saved on entering the node: " + entered);
			var s = flow.Session!;
			if (s.WinFight(700) is { } draft)
			{
				var card = draft.Offer.Cards[0];
				Assert.True(draft.Pick(0, card.Targets.Count > 0 ? card.Targets[0].Slot : null).Ok, "picked");
			}
			Assert.True(s.CompleteNode().Ok && flow.Router.Current == Screen.Map, "node completed, back on the map");
			Assert.True(File.ReadAllText(dev) != entered, "saved again on completing the node");
			Assert.True(flow.ReturnToTitle() && !flow.Title.ContinueButton.Disabled, "Continue lit from the dev slot");
		}));

		Assert.Equal(PlayerFile, File.ReadAllText(player), "the player's file is byte-for-byte untouched");
		Assert.Equal(stamp, File.GetLastWriteTimeUtc(player), "and not rewritten");
		var left = Directory.GetFiles(dir).Select(Path.GetFileName).OrderBy(n => n).ToArray();
		Assert.True(left.SequenceEqual(new[] { "progress.dev.json", "progress.json" }), "no temp or backup of the player's file: " + string.Join(", ", left));
	});

	[Test]
	public static void SwitchingOnTheTitle_SwapsTheSlot_AndAReleaseBootHasNoDevEntry(Node runner) => WithTempDir(dir =>
	{
		string player = Path.Combine(dir, "progress.json");
		File.WriteAllText(player, PlayerFile);
		WithMode(Debug, () => WithBoot(runner, player, flow =>
		{
			Assert.True(flow.Saves!.Path == player && flow.Saved.Unlocks.Contains("player-only-unlock"), "dev off: the player's file, read as usual");
			Assert.True(flow.DevMenuButton is { Visible: true }, "debug build: the title has a Dev mode button");
			Assert.True(flow.OpenDevMenu() && Shell.Menu.IsOpen, "the title opens the dev menu while dev mode is off");
			Assert.True(!Shell.Menu.ModeToggle.Disabled && Shell.Menu.ModeToggleRefusal == null, "on the title the toggle can be pressed");
			Press(Shell.Menu.ModeToggle);
			Assert.True(DevMode.On && Shell.Badge.Visible, "toggled on, badge shown");
			Assert.True(flow.Saves!.Path == DevMode.SavePathFor(player) && flow.Saved.Unlocks.Count == 0, "the boot moved to the dev slot");
			Press(Shell.Menu.ModeToggle);
			Assert.True(!DevMode.On && flow.Saves!.Path == player && flow.Saved.Unlocks.Contains("player-only-unlock"), "and back to the player's file");
			Shell.Menu.Close();
		}));
		Assert.Equal(PlayerFile, File.ReadAllText(player), "switching wrote nothing to the player's file");

		WithMode(Release, () => WithBoot(runner, null, flow =>
		{
			Assert.True(flow.DevMenuButton == null, "release: no Dev button on the title");
			Assert.True(!flow.OpenDevMenu() && !Shell.Menu.IsOpen, "release: F11 / the menu cannot open");
		}));
	});

	[Test]
	public static void Switch_IsTitleOnly_ForPagesToo()
	{
		WithMode(Debug, () =>
		{
			foreach (var place in new[] { DevPlace.Map, DevPlace.Fight })
				Assert.True(!DevMode.TrySetOn(place, true, out string? why) && why == DevMode.TitleOnlyReason && !DevMode.On, $"switching on from {place} is refused");
			Assert.True(DevMode.TrySetOn(DevPlace.Title, true, out _) && DevMode.On, "allowed on the title");
			foreach (var place in new[] { DevPlace.Map, DevPlace.Fight })
				Assert.True(!DevMode.TrySetOn(place, false, out string? why) && why == DevMode.TitleOnlyReason && DevMode.On, $"switching off from {place} is refused");
			Assert.True(DevMode.TrySetOn(DevPlace.Map, true, out string? none) && none == null && DevMode.On, "asking for the current state is no switch, so it is not refused");
		});
	}

	[Test]
	public static void Switch_IsRefusedWhileAWriteIsPending_SoTheEndedRunIsNeverOffered(Node runner) => WithTempDir(dir =>
	{
		if (OperatingSystem.IsWindows()) return;
		string player = Path.Combine(dir, "progress.json");
		WithMode(Debug, () => WithBoot(runner, player, flow =>
		{
			Press(flow.Title.StartButton);
			Press(flow.ControlSelect.Start);
			Assert.True(flow.ChooseNode(flow.Session!.Reachable()[0]).Ok, "entered");
			string held = File.ReadAllText(player);
			File.SetUnixFileMode(dir, UnixFileMode.UserRead | UnixFileMode.UserExecute);
			try
			{
				Assert.True(flow.Session.LoseFight().Ok && flow.Saved.Run == null, "lost: the flow knows the run is over");
				Assert.Equal(held, File.ReadAllText(player), "but the disk refused the write: the file still holds the run");
				Assert.True(flow.ReturnToTitle() && flow.Title.ContinueButton.Disabled, "the title does not offer the ended run");
				Assert.True(!DevMode.TrySetOn(DevPlace.Title, true, out string? why) && why == GameFlow.PendingWriteReason && !DevMode.On, "switching slots is refused while the write is pending: " + why);
				Assert.True(flow.Saves!.Path == player, "still on the player's file");
				Assert.True(flow.OpenDevMenu() && Shell.Menu.ModeToggle.Disabled && Shell.Menu.ModeToggleRefusal == GameFlow.PendingWriteReason, "the menu greys the toggle with the reason");
				Shell.Menu.Close();
			}
			finally { File.SetUnixFileMode(dir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); }
			Assert.True(DevMode.TrySetOn(DevPlace.Title, true, out _) && DevMode.On, "once the disk allows it the switch goes through");
			Assert.True(File.ReadAllText(player).Contains("\"run\": null"), "and the pending removal was written to the player's file first");
			Assert.True(DevMode.TrySetOn(DevPlace.Title, false, out _) && flow.Saves!.Path == player && flow.Title.ContinueButton.Disabled, "back on the player's file: the ended run is not offered");
		}));
	});

	// ---- the menu ----

	[Test]
	public static void Sections_Register_ReplaceInPlace_AndRemove()
	{
		string[] ids = DevMenu.Sections.Select(s => s.Id).ToArray();
		Assert.True(ids.Take(4).SequenceEqual(new[] { DevMenu.ToolsId, DevMenu.MapId, DevMenu.JumpId, DevMenu.RunId }), "built-in sections in order: " + string.Join(", ", ids));
		var map = DevMenu.FindSection(DevMenu.MapId)!;
		try
		{
			DevMenu.AddSection("probe-extra", "Extra", _ => new Label { Text = "extra" });
			Assert.Equal("probe-extra", DevMenu.Sections[^1].Id, "a new id is appended");
			DevMenu.AddSection(DevMenu.MapId, "Map (real)", _ => new Label { Text = "real map page" });
			Assert.True(DevMenu.Sections[1].Id == DevMenu.MapId && DevMenu.Sections[1].Title == "Map (real)", "an existing id is replaced in place");
			Assert.Equal(ids.Length + 1, DevMenu.Sections.Count, "no duplicate");
			Assert.True(DevMenu.RemoveSection("probe-extra") && !DevMenu.RemoveSection("probe-extra"), "removed once");
			bool threw = false;
			try { DevMenu.AddSection(" ", "Blank", _ => new Label()); } catch (ArgumentException) { threw = true; }
			Assert.True(threw, "an empty id is refused");
		}
		finally
		{
			DevMenu.RemoveSection("probe-extra");
			DevMenu.AddSection(map.Id, map.Title, map.Build, map.NeedsDevMode);
		}
	}

	[Test]
	public static void Menu_ShowsRegisteredPages_GreysThemWhileOff_AndListsEveryDebugKey(Node runner) => WithBoot(runner, null, flow =>
	{
		WithMode(Debug, () =>
		{
			Assert.True(flow.OpenDevMenu(), "title opens the menu");
			var menu = Shell.Menu;
			Assert.Equal(DevMenu.ToolsId, menu.CurrentSection, "dev off: the debug tools page");
			Assert.True(!menu.ShowSection(DevMenu.MapId), "dev off: Map, Jump and Run state are greyed");
			foreach (var t in DevMenu.DebugTools)
				Assert.True(FindLabel(menu.Page!, "Key_" + OS.GetKeycodeString(t.Key)) != null, "listed: " + OS.GetKeycodeString(t.Key));
			foreach (Key k in new[] { DebugOverlay.ToggleKey, DebugOverlay.PauseKey, DebugOverlay.StepKey, DebugOverlay.SchemeKey, FightScene.DebugMenuKey, FightScene.SaveReplayKey, Key.R })
				Assert.True(DevMenu.DebugTools.Any(t => t.Key == k), "overlay, frame-step, F4 scheme, F9, replay and R are listed: " + k);
			Assert.True(DevMenu.DebugTools.Select(t => t.Key).Distinct().Count() == DevMenu.DebugTools.Count, "no key listed twice");
			Press(menu.ModeToggle);
			Assert.True(menu.ShowSection(DevMenu.MapId) && menu.Page is DevMapPage, "dev on: the Map page (YOK-124)");
			foreach (string id in new[] { DevMenu.JumpId })
				Assert.True(menu.ShowSection(id) && menu.Page!.Name == "Page_" + id && FindLabel(menu.Page, "Stub") != null, "dev on: stub page " + id);
			Assert.True(menu.ShowSection(DevMenu.RunId) && menu.Page is DevRunStatePage && menu.Page.Name == "Page_" + DevMenu.RunId, "dev on: the Run state page (YOK-126)");
			menu.Close();
			Assert.True(!menu.IsOpen && menu.Page == null, "closed, page freed");
		});
	});

	[Test]
	public static void Map_OpensTheMenuOnlyInDevMode_WithRunChangesAllowed(Node runner) => WithBoot(runner, null, flow =>
	{
		Press(flow.Title.StartButton);
		Press(flow.ControlSelect.Start);
		Assert.Equal(Screen.Map, flow.Router.Current, "on the map");
		WithMode(Debug, () => Assert.True(!flow.OpenDevMenu(), "dev off: the map has no dev menu"));
		WithMode(DevOn, () =>
		{
			Assert.True(flow.OpenDevMenu() && Shell.Menu.Context is { Place: DevPlace.Map, AtNodeBoundary: true, RunChangeRefusal: null }, "dev on: the map opens it, between nodes");
			Assert.True(Shell.Menu.ModeToggle.Disabled && Shell.Menu.ModeToggleRefusal != null, "the slot cannot be switched mid-run");
			Shell.Menu.Close();
		});
	});

	[Test]
	public static void Pause_OpensTheMenuOverAFrozenFight_AndRefusesRunChanges(Node runner) => WithBoot(runner, null, flow =>
	{
		var fight = flow.Fight!;
		var vp = fight.GetViewport();
		void Tap(Key k)
		{
			vp.PushInput(new InputEventKey { Keycode = k, PhysicalKeycode = k, Pressed = true });
			vp.PushInput(new InputEventKey { Keycode = k, PhysicalKeycode = k, Pressed = false });
		}

		WithMode(Debug, () =>
		{
			Assert.True(fight.TryPause(), "paused");
			Assert.True(fight.PauseMenu!.DevButton is not { Visible: true }, "dev off: no Dev menu entry on pause");
			Tap(DevMode.MenuKey);
			Assert.True(!Shell.Menu.IsOpen && fight.Paused, "F11 does nothing; still paused");
			fight.PauseMenu.Resume();
		});
		WithMode(DevOn, () =>
		{
			ulong before = fight.Match.StateHash();
			int tick = fight.Match.Tick;
			Assert.True(fight.TryPause() && fight.PauseMenu!.DevButton is { Visible: true }, "dev on: the pause menu shows the Dev menu entry");
			Tap(DevMode.MenuKey);
			Assert.True(Shell.Menu.IsOpen && fight.Paused, "F11 on pause opens the dev menu; the fight stays paused");
			var ctx = Shell.Menu.Context!;
			Assert.True(ctx.Place == DevPlace.Fight && ctx.Fight == fight && ctx.Flow == flow && !ctx.AtNodeBoundary, "context: inside a fight");
			bool ran = false;
			Assert.True(!ctx.TryChangeRun(() => ran = true, out string? why) && !ran && why == DevContext.MidFightReason, "a run change mid-fight is refused and not run");
			Assert.True(Shell.Menu.ModeToggle.Disabled, "the slot cannot be switched mid-fight");
			Shell.Menu.Close();
			Assert.True(fight.Paused && fight.PauseMenu.ResumeButton.FocusMode != Control.FocusModeEnum.None, "closing returns to the pause menu, focus given back");
			fight.PauseMenu.Resume();
			Assert.True(!fight.Paused, "resumed");
			Assert.True(fight.Match.Tick == tick && fight.Match.StateHash() == before, "the dev menu changed nothing in the fight");
		});
	}, ControlScheme.Kihon);

	[Test]
	public static void Isolation_SimSourcesNeverReadDevMode()
	{
		string scripts = ProjectSettings.GlobalizePath("res://scripts");
		foreach (string sub in new[] { "Sim", "Ai", "Replay" })
			foreach (string f in Directory.GetFiles(Path.Combine(scripts, sub), "*.cs", SearchOption.AllDirectories))
				Assert.True(!File.ReadAllText(f).Contains("DevMode") && !File.ReadAllText(f).Contains("YokaiFighters.Dev"), f + " reads dev mode");
	}
}
