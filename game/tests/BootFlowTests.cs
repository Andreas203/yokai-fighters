using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Flow;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>
/// Boot flow: the pure <see cref="ScreenRouter"/> (transitions, disabled entries and their seam, scheme, bypass) and
/// the boot scene (<c>scenes/boot.tscn</c>, <see cref="GameFlow"/>): boots to the title with no fight in the tree,
/// title → control select → fight with the chosen scheme on P1 for every duel, Settings and Quit routed, return to
/// title, and the direct-to-fight bypass. Scene tests run on the fixture kits.
/// </summary>
public static class BootFlowTests
{
	// ---- router (no Godot) ----

	[Test]
	public static void Router_BootsToTitle_StartGoesThroughControlSelectToFight()
	{
		var r = new ScreenRouter();
		var seen = new List<string>();
		r.Changed += (from, to) => seen.Add($"{from}>{to}");
		Assert.Equal(Screen.Title, r.Current, "boot screen");
		Assert.True(!r.Confirm(), "no fight from the title without control select");
		Assert.True(r.Select(TitleEntry.Start), "Start");
		Assert.Equal(Screen.ControlSelect, r.Current, "Start opens control select");
		Assert.True(!r.Select(TitleEntry.Settings), "title entries are dead off the title");
		Assert.True(r.Back(), "back from control select");
		Assert.Equal(Screen.Title, r.Current, "back on the title");
		r.Select(TitleEntry.Start);
		Assert.True(r.Confirm(), "Start Game");
		Assert.Equal(Screen.Fight, r.Current, "in the fight");
		Assert.True(!r.Back() && !r.Confirm() && !r.Select(TitleEntry.Start), "menu actions do nothing during a fight");
		Assert.Equal("Title>ControlSelect|ControlSelect>Title|Title>ControlSelect|ControlSelect>Fight", string.Join("|", seen), "transitions");
	}

	[Test]
	public static void Router_SettingsAndBack_Quit()
	{
		var r = new ScreenRouter();
		int quits = 0;
		r.Quit += () => quits++;
		Assert.True(r.Select(TitleEntry.Settings), "Settings");
		Assert.Equal(Screen.Settings, r.Current, "settings open");
		Assert.True(!r.Select(TitleEntry.Quit), "no quit from under the settings sheet");
		Assert.True(r.Back(), "back");
		Assert.Equal(Screen.Title, r.Current, "title again");
		Assert.True(!r.Back(), "nothing behind the title");
		Assert.True(r.Select(TitleEntry.Quit), "Quit");
		Assert.True(r.QuitRequested && quits == 1, "quit raised once");
		Assert.Equal(Screen.Title, r.Current, "quit is not a screen");
	}

	[Test]
	public static void Router_ContinueAndPractice_DisabledWithReason_UntilEnabled()
	{
		var r = new ScreenRouter();
		Assert.Equal("Start|Continue|Practice|Settings|Quit", string.Join("|", r.Entries.Select(e => e.Entry)), "five entries in menu order");
		foreach (TitleEntry e in new[] { TitleEntry.Continue, TitleEntry.Practice })
		{
			Assert.True(!r.Entry(e).Enabled && r.Entry(e).Reason.Length > 0, $"{e} disabled with a reason");
			Assert.True(!r.Select(e), $"{e} cannot be selected");
			Assert.Equal(Screen.Title, r.Current, "still on the title");
		}
		Assert.Equal(ScreenRouter.NoSaveReason, r.Entry(TitleEntry.Continue).Reason, "Continue reason");
		Assert.Equal(ScreenRouter.NoPracticeReason, r.Entry(TitleEntry.Practice).Reason, "Practice reason");
		Assert.True(r.Entries.Where(e => e.Entry is TitleEntry.Start or TitleEntry.Settings or TitleEntry.Quit).All(e => e.Enabled && e.Reason == ""), "the rest are enabled");

		// The seam later work uses: enable, then listen.
		var changed = new List<TitleEntryState>();
		var activated = new List<TitleEntry>();
		r.EntryChanged += changed.Add;
		r.Activated += activated.Add;
		r.Enable(TitleEntry.Practice);
		Assert.True(changed.Count == 1 && changed[0] is { Entry: TitleEntry.Practice, Enabled: true }, "the UI is told");
		Assert.True(r.Select(TitleEntry.Practice), "selectable once enabled");
		Assert.Equal("Practice", string.Join("|", activated), "handed to whoever builds practice");
		Assert.Equal(Screen.Title, r.Current, "the router itself goes nowhere: no practice mode here");
		r.Disable(TitleEntry.Practice, "closed");
		Assert.True(!r.Select(TitleEntry.Practice) && r.Entry(TitleEntry.Practice).Reason == "closed", "disabled again with the new reason");
		bool threw = false;
		try { r.Disable(TitleEntry.Start, " "); } catch (ArgumentException) { threw = true; }
		Assert.True(threw, "a disabled entry must carry a reason");
	}

	[Test]
	public static void Router_Scheme_DirectFight_ReturnToTitle()
	{
		var r = new ScreenRouter();
		Assert.Equal(ControlScheme.Kihon, r.Scheme, "Kihon by default (E18)");
		int changes = 0;
		r.SchemeChanged += _ => changes++;
		r.Select(TitleEntry.Start);
		Assert.True(r.ChooseScheme(ControlScheme.Kata) && r.ChooseScheme(ControlScheme.Kata), "choose Kata (twice)");
		Assert.Equal(1, changes, "one change");
		r.Confirm();
		Assert.Equal(ControlScheme.Kata, r.Scheme, "carried into the fight");
		Assert.True(!r.ChooseScheme(ControlScheme.Kihon) && r.Scheme == ControlScheme.Kata, "menus cannot change the scheme mid-fight");
		Assert.True(r.ReturnToTitle(), "return to title");
		Assert.Equal(Screen.Title, r.Current, "title");
		Assert.True(!r.ReturnToTitle(), "only from a fight");
		Assert.Equal(ControlScheme.Kata, r.Scheme, "the choice is remembered for the next run");

		var direct = new ScreenRouter();
		Assert.True(direct.StartFightDirect(ControlScheme.Kata), "bypass from the title");
		Assert.True(direct.Current == Screen.Fight && direct.Scheme == ControlScheme.Kata, "straight into a Kata fight");
		Assert.True(!direct.StartFightDirect(ControlScheme.Kihon), "not twice");
	}

	[Test]
	public static void DirectFightArg_Parses()
	{
		Assert.True(GameFlow.DirectFightFromArgs(new[] { "--temperament=patient" }) == null, "no arg = menus");
		Assert.True(GameFlow.DirectFightFromArgs(new[] { "--fight" }) == ControlScheme.Kihon, "--fight = Kihon");
		Assert.True(GameFlow.DirectFightFromArgs(new[] { "x", "--fight=kata" }) == ControlScheme.Kata, "--fight=kata");
		Assert.True(GameFlow.DirectFightFromArgs(new[] { "--fight=Kihon" }) == ControlScheme.Kihon, "--fight=Kihon");
		Assert.True(GameFlow.DirectFightFromArgs(new[] { "--fighter" }) == null, "a longer arg is not ours");
	}

	// ---- the boot scene ----

	static void WithBoot(Node runner, Action<GameFlow> body, ControlScheme? direct = null)
	{
		var saved = FightScene.Sources;
		var savedRun = FightScene.Run;
		FightScene.Sources = FightScene.FixtureSources;
		FightScene.Run = null;
		MenuSettings.Reset();
		var flow = GD.Load<PackedScene>("res://scenes/boot.tscn").Instantiate<GameFlow>();
		flow.QuitAction = () => { };
		flow.DirectFight = direct;
		flow.ConfigureFight = f => f.ExternalDrive = true;
		try { runner.AddChild(flow); body(flow); }
		finally
		{
			runner.RemoveChild(flow);
			flow.QueueFree();
			MenuSettings.Reset();
			FightScene.Sources = saved;
			FightScene.Run = savedRun;
		}
	}

	static void Press(Button b) => b.EmitSignal(BaseButton.SignalName.Pressed);

	[Test]
	public static void MainScene_IsTheBootScene()
	{
		Assert.Equal("res://scenes/boot.tscn", (string)ProjectSettings.GetSetting("application/run/main_scene"), "main scene");
	}

	[Test]
	public static void Boot_ShowsTitle_NoFightInTheTree(Node runner) => WithBoot(runner, flow =>
	{
		Assert.Equal(Screen.Title, flow.Router.Current, "boot screen");
		Assert.True(flow.Title.Visible && !flow.ControlSelect.Visible, "title shown, control select hidden");
		Assert.True(flow.Fight == null && !flow.GetChildren().OfType<FightScene>().Any(), "no fight scene (so no sim) behind the menus");
		Assert.True(!flow.Title.StartButton.Disabled && !flow.Title.SettingsButton.Disabled && !flow.Title.QuitButton.Disabled, "Start, Settings, Quit live");
		foreach (var (button, entry, reason) in new[]
		{
			(flow.Title.ContinueButton, TitleEntry.Continue, ScreenRouter.NoSaveReason),
			(flow.Title.PracticeButton, TitleEntry.Practice, ScreenRouter.NoPracticeReason),
		})
		{
			Assert.True(button.Visible && button.Disabled && button.FocusMode == Control.FocusModeEnum.None, $"{entry} present, greyed, skipped by focus");
			Assert.Equal(reason, flow.Title.DisabledReason(entry), $"{entry} reason on the menu");
			Assert.Equal(reason, button.TooltipText, $"{entry} reason in the tooltip");
			Press(button); // a stray signal still cannot route
			Assert.Equal(Screen.Title, flow.Router.Current, $"{entry} goes nowhere");
		}
		flow.Router.Enable(TitleEntry.Practice);
		Assert.True(!flow.Title.PracticeButton.Disabled && flow.Title.PracticeButton.FocusMode == Control.FocusModeEnum.All, "the seam: enabling on the router lights the entry");
		Assert.Equal("", flow.Title.DisabledReason(TitleEntry.Practice), "no reason once enabled");
	});

	[Test]
	public static void Title_Settings_OpensAndBacksOut_Quit_Quits(Node runner) => WithBoot(runner, flow =>
	{
		int quits = 0;
		flow.QuitAction = () => quits++;
		Press(flow.Title.SettingsButton);
		Assert.Equal(Screen.Settings, flow.Router.Current, "router in Settings");
		Assert.True(flow.Title.Settings!.Visible, "the existing settings sheet is open");
		Press(flow.Title.QuitButton);
		Assert.Equal(0, quits, "no quit from under the sheet");
		flow.Router.Back(); // what the sheet's Back does
		Assert.True(flow.Router.Current == Screen.Title && !flow.Title.Settings.Visible && flow.Title.Visible, "back on the title, sheet closed");
		Assert.True(flow.Title.StartButton.FocusMode == Control.FocusModeEnum.All, "title buttons take focus again");
		Press(flow.Title.QuitButton);
		Assert.Equal(1, quits, "Quit quits");
	});

	[Test]
	public static void Start_ControlSelect_Kata_IsCarriedIntoEveryDuel(Node runner) => WithBoot(runner, flow =>
	{
		Press(flow.Title.StartButton);
		Assert.Equal(Screen.ControlSelect, flow.Router.Current, "control select");
		Assert.True(flow.ControlSelect.Visible && !flow.Title.Visible && flow.Fight == null, "control select shown, still no fight");
		Press(flow.ControlSelect.KataButton);
		Assert.Equal(ControlScheme.Kata, flow.Router.Scheme, "router holds the choice");
		Press(flow.ControlSelect.Start);
		Assert.Equal(Screen.Fight, flow.Router.Current, "fight");
		FightScene fight = flow.Fight!;
		Assert.True(fight.IsInsideTree() && !flow.ControlSelect.Visible && !flow.Title.Visible, "fight scene in the tree, menus hidden");
		Assert.Equal(DemoStage.Fighting, fight.Stage, "one way in: the scene starts fighting");
		Assert.Equal(ControlScheme.Kata, fight.SchemeOf(0), "P1 plays Kata");
		Assert.True(fight.Match.P1.Input.Scheme == ControlScheme.Kata, "the sim's reader is on the Kata parser");
		Assert.Equal(ControlScheme.Kata.ToString(), fight.Recorder!.Recording.Header.Fighters[0].Scheme, "and the recording says so");

		// The fight really parses Kata: 236 + punch fires slot A with the precision bonus (K1); Special does nothing.
		foreach (InputBits bits in new[] { InputBits.Down, InputBits.Down | InputBits.Right, InputBits.Right, InputBits.Right | InputBits.LightPunch })
			fight.Step(new FighterInput(bits), FighterInput.None);
		Assert.True(fight.Match.P1.ActiveSpecial != null && fight.Match.P1.ActivePrecision, "236+P = slot A special with precision");

		fight.RestartRun();
		Assert.Equal(ControlScheme.Kata, fight.SchemeOf(0), "kept across Restart (a new match)");
		Assert.Equal(Screen.Fight, flow.Router.Current, "Restart stays in the fight");
	});

	[Test]
	public static void Start_ControlSelect_DefaultKihon_AndBack(Node runner) => WithBoot(runner, flow =>
	{
		Press(flow.Title.StartButton);
		flow.Router.Back(); // Esc / pad B on control select
		Assert.True(flow.Router.Current == Screen.Title && flow.Title.Visible && !flow.ControlSelect.Visible, "back to the title");
		Press(flow.Title.StartButton);
		Press(flow.ControlSelect.Start);
		Assert.Equal(ControlScheme.Kihon, flow.Fight!.SchemeOf(0), "Kihon unless chosen otherwise (E18)");
		flow.Fight.Step(new FighterInput(InputBits.Special), FighterInput.None);
		Assert.True(flow.Fight.Match.P1.ActiveSpecial != null && !flow.Fight.Match.P1.ActivePrecision, "Special + neutral = slot A, no precision (K2)");
	});

	[Test]
	public static void SettingsSheetScheme_IsTheControlSelectScheme(Node runner) => WithBoot(runner, flow =>
	{
		Press(flow.Title.SettingsButton);
		MenuSettings.SetScheme(ControlScheme.Kata); // the sheet's Kata button
		flow.Router.Back();
		Assert.Equal(ControlScheme.Kata, flow.Router.Scheme, "the settings choice reaches the router");
		Press(flow.Title.StartButton);
		Assert.True(flow.ControlSelect.KataButton.Text.StartsWith("●"), "control select opens on it");
		Press(flow.ControlSelect.Start);
		Assert.Equal(ControlScheme.Kata, flow.Fight!.SchemeOf(0), "and the fight uses it");
		MenuSettings.SetScheme(ControlScheme.Kihon); // a menu value changing during the fight
		Assert.Equal(ControlScheme.Kata, flow.Fight.SchemeOf(0), "does not reach into the running fight");
	});

	[Test]
	public static void ReturnToTitle_FreesTheFight_AndANewRunStartsClean(Node runner) => WithBoot(runner, flow =>
	{
		Assert.True(!flow.ReturnToTitle(), "nothing to leave on the title");
		Press(flow.Title.StartButton);
		Press(flow.ControlSelect.Start);
		FightScene first = flow.Fight!;
		for (int i = 0; i < 20; i++) first.Step(FighterInput.None, FighterInput.None);
		Assert.True(first.RequestTitle(), "the fight scene's own hook (what pause will call)");
		Assert.Equal(Screen.Title, flow.Router.Current, "title");
		Assert.True(flow.Fight == null && !first.IsInsideTree() && first.IsQueuedForDeletion(), "fight scene removed and freed");
		Assert.True(flow.Title.Visible && !flow.Title.StartButton.Disabled, "title is back");
		Assert.True(!flow.GetTree().Paused, "tree not left paused");
		Press(flow.Title.StartButton);
		Press(flow.ControlSelect.Start);
		Assert.True(flow.Fight != null && flow.Fight != first, "a new fight scene");
		Assert.True(flow.Fight!.Match.Tick == 0 && flow.Fight.Duel == 1 && flow.Fight.Match.P1.Health == 1000, "a fresh run from tick 0");
		Assert.True(flow.ReturnToTitle(), "GameFlow.ReturnToTitle works too");
	});

	[Test]
	public static void StandaloneFightScene_RequestTitle_IsHarmless(Node runner)
	{
		var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		scene.ExternalDrive = true;
		runner.AddChild(scene);
		Assert.True(!scene.RequestTitle(), "no boot flow listening: nothing happens");
		Assert.True(scene.PlayerScheme == null && scene.SchemeOf(0) == FightScene.DefaultScheme, "standalone = the default scheme");
		scene.QueueFree();
	}

	[Test]
	public static void DirectFight_SkipsTheMenus(Node runner) => WithBoot(runner, flow =>
	{
		Assert.Equal(Screen.Fight, flow.Router.Current, "straight into the fight");
		Assert.True(flow.Fight != null && !flow.Title.Visible && !flow.ControlSelect.Visible, "fight up, menus hidden");
		Assert.Equal(ControlScheme.Kata, flow.Fight!.SchemeOf(0), "with the requested scheme");
		int tick = flow.Fight.Match.Tick;
		flow.Fight.Step(FighterInput.None, FighterInput.None);
		Assert.Equal(tick + 1, flow.Fight.Match.Tick, "steppable at once");
	}, direct: ControlScheme.Kata);

	/// <summary>F3: the same inputs give the same hashes whether the fight was opened directly or reached through the menus.</summary>
	[Test]
	public static void MenuPath_DoesNotPerturbTheSim(Node runner)
	{
		static List<ulong> Play(FightScene s)
		{
			var rng = new SimRng(5150);
			var hashes = new List<ulong>();
			for (int i = 0; i < 600; i++)
			{
				s.Step(new FighterInput((InputBits)(rng.NextUInt() & 0x3ff)), new FighterInput((InputBits)(rng.NextUInt() & 0x3ff)));
				hashes.Add(s.Match.StateHash());
			}
			return hashes;
		}
		List<ulong> viaMenus = null!, direct;
		WithBoot(runner, flow =>
		{
			Press(flow.Title.SettingsButton);
			flow.Router.Back();
			Press(flow.Title.StartButton);
			Press(flow.ControlSelect.Start);
			viaMenus = Play(flow.Fight!);
		});
		var saved = FightScene.Sources;
		FightScene.Sources = FightScene.FixtureSources;
		var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		scene.ExternalDrive = true;
		try { runner.AddChild(scene); direct = Play(scene); }
		finally { scene.QueueFree(); FightScene.Sources = saved; }
		Assert.True(viaMenus.SequenceEqual(direct), "600 ticks hash-identical with and without the menu path");
	}
}
