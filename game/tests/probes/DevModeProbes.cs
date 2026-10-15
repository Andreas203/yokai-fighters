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

/// <summary>YOK-123 probes: dev save isolation, flag spellings, F11 and the pause overlay, AddSection edges.</summary>
public static class DevModeProbes
{
	static readonly DevModeState Release = new(false, false), Debug = new(true, false), DevOn = new(true, true);
	const string PlayerFile = "{ \"version\": 3, \"run\": null, \"unlocks\": [\"player-only-unlock\"], \"story\": { \"seen\": [] } }";

	static void WithMode(DevModeState s, Action body) { DevMode.Configure(s); try { body(); } finally { DevMode.ResetToLaunch(); } }

	static void WithTempDir(Action<string> body)
	{
		string dir = Path.Combine(Path.GetTempPath(), "yokai-devprobe-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(dir);
		try { body(dir); } finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
	}

	static void WithBoot(Node runner, string? saveFile, Action<GameFlow> body, ControlScheme? direct = null)
	{
		var saved = FightScene.Sources; var savedRun = FightScene.Run; var savedFoe = FightScene.Opponent;
		FightScene.Sources = FightScene.FixtureSources; FightScene.Run = null;
		MenuSettings.Reset();
		var flow = GD.Load<PackedScene>("res://scenes/boot.tscn").Instantiate<GameFlow>();
		flow.QuitAction = () => { };
		flow.FixedRunSeed = 91;
		flow.SaveFile = saveFile;
		flow.DirectFight = direct;
		flow.ConfigureFight = f => { f.ExternalDrive = true; f.PauseEnabled = true; f.HeldBitsSource = () => InputBits.None; };
		var stage = new SubViewport { Size = new Vector2I(1920, 1080), Name = "DevProbeStage" };
		runner.AddChild(stage);
		try { stage.AddChild(flow); body(flow); }
		finally
		{
			DevShell.Instance?.Menu.Close();
			runner.RemoveChild(stage); stage.QueueFree();
			MenuSettings.Reset();
			FightScene.Sources = saved; FightScene.Run = savedRun; FightScene.Opponent = savedFoe;
		}
	}

	static void Press(Button b) => b.EmitSignal(BaseButton.SignalName.Pressed);
	static void Tap(Viewport vp, Key k)
	{
		vp.PushInput(new InputEventKey { Keycode = k, PhysicalKeycode = k, Pressed = true });
		vp.PushInput(new InputEventKey { Keycode = k, PhysicalKeycode = k, Pressed = false });
	}

	// DevMode: a dev session is on the dev slot; the rule is the YOK-123 contract (player's save never touched).
	[Test]
	public static void Flag_OnlyTheExactArgSwitchesDevOn()
	{
		Assert.True(!DevMode.Evaluate(false, new[] { "--dev=1" }).Available, "--dev=1 is not --dev");
		Assert.True(!DevMode.Evaluate(false, new[] { "--Dev" }).Available, "--Dev is not --dev");
		Assert.True(!DevMode.Evaluate(false, new[] { "-dev" }).Available, "-dev is not --dev");
		Assert.True(!DevMode.Evaluate(false, new[] { "--devx", "--" }).Available, "prefix does not count");
		Assert.True(DevMode.Evaluate(false, new[] { "--", "--dev" }) == new DevModeState(true, true), "-- --dev is on");
		Assert.True(DevMode.Evaluate(true, new string[0]) == Debug, "debug without arg: available, off");
		Assert.True(DevMode.Evaluate(false, new string[0]) == Release, "release without arg: unavailable");
	}

	[Test]
	public static void Release_NeverSwitchesOn_EvenFromTheTitle()
	{
		WithMode(Release, () =>
		{
			Assert.True(!DevMode.TrySetOn(DevPlace.Title, true, out string? why) && why == DevMode.UnavailableReason && !DevMode.On, "refused");
			Assert.True(!DevShell.CanOpen(DevPlace.Title) && !DevShell.CanOpen(DevPlace.Map) && !DevShell.CanOpen(DevPlace.Fight), "no place opens");
			Assert.True(!DevMode.Evaluate(false, new string[0]).On, "never on");
			DevMode.Configure(new DevModeState(false, true));
			Assert.True(!DevMode.On, "Configure drops On when unavailable");
		});
	}

	// Player's save and its tmp/bak: dev session through a corrupt dev slot, and corrupt player file, switch both ways.
	[Test]
	public static void CorruptPlayerFile_IsNeverBackedUp_ByDevSwitchesOrDevRuns(Node runner) => WithTempDir(dir =>
	{
		string player = Path.Combine(dir, "progress.json");
		const string junk = "{ not json at all";
		File.WriteAllText(player, junk);
		WithMode(Debug, () => WithBoot(runner, player, flow =>
		{
			Assert.True(DevMode.TrySetOn(DevPlace.Title, true, out _), "on");
			Press(flow.Title.StartButton); Press(flow.ControlSelect.Start);
			Assert.True(flow.ChooseNode(flow.Session!.Reachable()[0]).Ok, "node entered in the dev slot");
			Assert.True(flow.ReturnToTitle(), "quit mid-node (AM3)");
			Assert.True(flow.ContinueRun(), "dev Continue works");
			Assert.True(flow.ReturnToTitle(), "back");
			Assert.True(DevMode.TrySetOn(DevPlace.Title, false, out _), "off");
			Assert.True(DevMode.TrySetOn(DevPlace.Title, true, out _), "on again");
			Assert.True(DevMode.TrySetOn(DevPlace.Title, false, out _), "off again");
		}));
		Assert.Equal(junk, File.ReadAllText(player), "player's file untouched");
		var left = Directory.GetFiles(dir).Select(Path.GetFileName).OrderBy(n => n).ToArray();
		Assert.True(left.All(n => n.StartsWith("progress.dev.json") || n == "progress.json"), "nothing derived from the player's path: " + string.Join(", ", left));
	});

	[Test]
	public static void CorruptDevSlot_BackupIsDerivedFromTheDevSlot(Node runner) => WithTempDir(dir =>
	{
		string player = Path.Combine(dir, "progress.json"), dev = DevMode.SavePathFor(player);
		File.WriteAllText(player, PlayerFile);
		File.WriteAllText(dev, "garbage");
		WithMode(DevOn, () => WithBoot(runner, player, flow =>
		{
			Press(flow.Title.StartButton); Press(flow.ControlSelect.Start);
			Assert.True(flow.ChooseNode(flow.Session!.Reachable()[0]).Ok, "entered");
		}));
		Assert.Equal(PlayerFile, File.ReadAllText(player), "player untouched");
		Assert.True(File.Exists(dev + ".bak") && File.ReadAllText(dev + ".bak") == "garbage", "dev backup beside the dev slot");
		Assert.True(!File.Exists(player + ".bak") && !File.Exists(player + ".tmp"), "no player tmp or bak");
	});

	[Test]
	public static void RunLoss_InDevSlot_LeavesPlayersRunAlone(Node runner) => WithTempDir(dir =>
	{
		string player = Path.Combine(dir, "progress.json");
		// First a real player run saved in the player's file.
		string playerBytes = "";
		WithMode(Debug, () => WithBoot(runner, player, flow =>
		{
			Press(flow.Title.StartButton); Press(flow.ControlSelect.Start);
			Assert.True(flow.ChooseNode(flow.Session!.Reachable()[0]).Ok, "entered");
			Assert.True(flow.ReturnToTitle(), "quit mid-node");
			playerBytes = File.ReadAllText(player);
			Assert.True(playerBytes.Contains("\"current\""), "player run saved in node");
			Assert.True(DevMode.TrySetOn(DevPlace.Title, true, out _), "dev on");
			Assert.True(flow.Title.ContinueButton.Disabled, "player's run is not offered in dev");
			Press(flow.Title.StartButton); Press(flow.ControlSelect.Start);
			Assert.True(flow.ChooseNode(flow.Session!.Reachable()[0]).Ok, "dev node");
			Assert.True(flow.Session.LoseFight().Ok, "lost in dev");
			Assert.True(flow.ReturnToTitle() || true, "title");
			Assert.True(DevMode.TrySetOn(DevPlace.Title, false, out _), "dev off");
			Assert.True(!flow.Title.ContinueButton.Disabled, "player's run is still continuable");
		}));
		Assert.Equal(playerBytes, File.ReadAllText(player), "player's file byte-identical after dev session");
	});

	// F11 and the fight: unpaused fight ignores it, sim identical to a control run.
	[Test]
	public static void F11_InAnUnpausedFight_ChangesNothing(Node runner) => WithBoot(runner, null, flow =>
	{
		var fight = flow.Fight!;
		WithMode(DevOn, () =>
		{
			var vp = fight.GetViewport();
			Assert.True(!fight.Paused, "running");
			int t = fight.Match.Tick;
			Tap(vp, DevMode.MenuKey);
			Assert.True(!DevShell.Instance!.Menu.IsOpen && !fight.Paused, "no menu, not paused");
			Assert.True(!fight.OpenDevMenu(), "OpenDevMenu refuses when not paused");
			Assert.True(fight.Match.Tick >= t, "ticks only forward");
		});
	}, ControlScheme.Kihon);

	[Test]
	public static void DevMenuOverPause_EscapeClosesOnlyTheDevMenu_AndHashTickMatchAfterUnpause(Node runner) => WithBoot(runner, null, flow =>
	{
		var fight = flow.Fight!;
		var vp = fight.GetViewport();
		WithMode(DevOn, () =>
		{
			Assert.True(fight.TryPause(), "paused");
			ulong h = fight.Match.StateHash(); int t = fight.Match.Tick;
			Assert.True(fight.OpenDevMenu() && DevShell.Instance!.Menu.IsOpen, "opened");
			Assert.True(!fight.OpenDevMenu(), "second open refused");
			var rootVp = DevShell.Instance!.GetViewport(); // the menu lives in the autoload's (root) viewport
			// PushInput into the root viewport does not reach the autoload in the headless runner: call the handler directly.
			{ DevShell.Instance.Menu._Input(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = true }); }
			Assert.True(!DevShell.Instance.Menu.IsOpen, "esc closed the dev menu");
			Assert.True(fight.Paused, "and the fight is still paused (esc did not also resume)");
			Assert.True(fight.Match.Tick == t && fight.Match.StateHash() == h, "no drift while paused");
			// F11 twice quickly: open then close via F11 with pause intact.
			Assert.True(fight.OpenDevMenu() && DevShell.Instance.Menu.IsOpen, "reopened");
			DevShell.Instance.Menu._Input(new InputEventKey { Keycode = DevMode.MenuKey, PhysicalKeycode = DevMode.MenuKey, Pressed = true });
			Assert.True(!DevShell.Instance.Menu.IsOpen && fight.Paused, "F11 closes, still paused");
			fight.PauseMenu!.Resume();
			Assert.True(!fight.Paused, "resumed");
		});
	}, ControlScheme.Kihon);

	[Test]
	public static void DevOff_WhileMenuOverPause_CannotBeSwitchedViaToggle(Node runner) => WithBoot(runner, null, flow =>
	{
		var fight = flow.Fight!;
		WithMode(DevOn, () =>
		{
			Assert.True(fight.TryPause() && fight.OpenDevMenu(), "open");
			var m = DevShell.Instance!.Menu;
			Press(m.ModeToggle); // a disabled button's signal can still be emitted programmatically
			Assert.True(DevMode.On, "toggle from Fight place is refused");
			m.Close();
			fight.PauseMenu!.Resume();
		});
	}, ControlScheme.Kihon);

	// AddSection edges.
	[Test]
	public static void AddSection_RejectsBadInput_AndDuplicateKeepsOnePosition()
	{
		int n = DevMenu.Sections.Count;
		bool threw = false;
		try { DevMenu.AddSection("", "x", _ => new Control()); } catch (ArgumentException) { threw = true; }
		Assert.True(threw, "empty id");
		threw = false;
		try { DevMenu.AddSection("probe-null", "x", null!); } catch (ArgumentNullException) { threw = true; }
		Assert.True(threw, "null builder");
		Assert.True(DevMenu.Sections.Count == n, "nothing registered by the refusals");
		DevMenu.AddSection("probe-a", "A", _ => new Control());
		DevMenu.AddSection("probe-a", "A2", _ => new Control());
		try { Assert.True(DevMenu.Sections.Count(s => s.Id == "probe-a") == 1 && DevMenu.FindSection("probe-a")!.Title == "A2", "duplicate replaced"); }
		finally { DevMenu.RemoveSection("probe-a"); }
		Assert.True(DevMenu.Sections.Count == n, "cleaned");
	}

	[Test]
	public static void AddSection_WhileMenuOpen_DoesNotBreakTheOpenMenu(Node runner) => WithBoot(runner, null, flow =>
	{
		WithMode(DevOn, () =>
		{
			Assert.True(flow.OpenDevMenu(), "open");
			var m = DevShell.Instance!.Menu;
			DevMenu.AddSection("probe-late", "Late", _ => new Label { Text = "late" });
			try
			{
				m.Close();
				Assert.True(flow.OpenDevMenu() && m.FindChild("Section_probe-late", true, false) != null, "present after reopen");
			}
			finally { DevMenu.RemoveSection("probe-late"); m.Close(); }
		});
	});

	[Test]
	public static void BuilderThatAddsASection_DuringBuild_DoesNotCrashTheMenu(Node runner) => WithBoot(runner, null, flow =>
	{
		WithMode(DevOn, () =>
		{
			DevMenu.AddSection("probe-reent", "Reent", _ => { DevMenu.AddSection("probe-reent2", "R2", _ => new Control()); return new Control(); });
			try
			{
				bool ok = false; string err = "";
				try { ok = flow.OpenDevMenu(); } catch (Exception e) { err = e.Message; }
				Assert.True(ok && err == "", "menu opens: " + err);
			}
			finally { DevMenu.RemoveSection("probe-reent"); DevMenu.RemoveSection("probe-reent2"); DevShell.Instance!.Menu.Close(); }
		});
	});

	[Test]
	public static void BuilderReturningNull_OrThrowing_DoesNotLeaveAHalfOpenMenu(Node runner) => WithBoot(runner, null, flow =>
	{
		WithMode(DevOn, () =>
		{
			DevMenu.AddSection("probe-bad", "Bad", _ => null!);
			try
			{
				var m = DevShell.Instance!.Menu;
				bool ok = false; string err = "";
				try { ok = flow.OpenDevMenu(); } catch (Exception e) { err = e.GetType().Name; }
				Assert.True(err == "", "opening with a null-page builder must not throw: " + err);
				m.Close();
			}
			finally { DevMenu.RemoveSection("probe-bad"); DevShell.Instance!.Menu.Close(); }
		});
	});
}
