using System;
using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.Dev;
using YokaiFighters.Fight;
using YokaiFighters.Flow;
using YokaiFighters.Sim;
using YokaiFighters.Ui;
using YokaiFighters.Ui.Kit;
using YokaiFighters.SaveGame;

namespace YokaiFighters.Tests;

/// <summary>YOK-72 probes: Back to title on the lose and demo-complete screens. Expected values: the claims of the ticket
/// (route once through RequestTitle, only in Lost/Complete, save holds no run, M1/M4 unlocks and story are kept).</summary>
public static class EndScreenTitleProbes
{
	static readonly FighterInput Idle = FighterInput.None;
	static FighterInput Strike(int t) => new(t % 2 == 0 ? InputBits.DebugStrike : InputBits.None);
	static void Press(Button b) => b.EmitSignal(BaseButton.SignalName.Pressed);
	static void Push(Node n, InputEvent e) => n.GetViewport().PushInput(e);
	static void Tap(Node n, Key k)
	{
		Push(n, new InputEventKey { Keycode = k, PhysicalKeycode = k, Pressed = true });
		Push(n, new InputEventKey { Keycode = k, PhysicalKeycode = k, Pressed = false });
	}
	static void PadTap(Node n, JoyButton b)
	{
		Push(n, new InputEventJoypadButton { ButtonIndex = b, Pressed = true, Pressure = 1f });
		Push(n, new InputEventJoypadButton { ButtonIndex = b, Pressed = false });
	}
	static void Lose(FightScene s)
	{
		for (int t = 0; t < 600 && s.Match.Phase != MatchPhase.Over; t++) s.Step(Idle, Strike(t));
		Assert.Equal(1, s.Match.Winner, "Kitsune won");
	}
	static void Win(FightScene s, int taken)
	{
		for (int t = 0; t < taken * 2; t++) s.Step(Idle, Strike(t));
		for (int t = 0; t < 900 && s.Match.Phase != MatchPhase.Over; t++) s.Step(Strike(t), Idle);
		Assert.True(s.Match.Winner == 0, "Ryo won");
	}

	static void WithBoot(Node runner, string? save, string? mapFile, Action<GameFlow> body)
	{
		var saved = FightScene.Sources; var savedRun = FightScene.Run; var savedFoe = FightScene.Opponent;
		FightScene.Sources = FightScene.FixtureSources; FightScene.Run = null;
		MenuSettings.Reset();
		var flow = GD.Load<PackedScene>("res://scenes/boot.tscn").Instantiate<GameFlow>();
		flow.QuitAction = () => { };
		flow.FixedRunSeed = 72;
		flow.SaveFile = save;
		if (mapFile != null) flow.MapFile = mapFile;
		flow.ConfigureFight = f => { f.ExternalDrive = true; f.PauseEnabled = true; f.HeldBitsSource = () => InputBits.None; };
		var stage = new SubViewport { Size = new Vector2I(1920, 1080), Name = "EndTitleProbeStage" };
		runner.AddChild(stage);
		try { stage.AddChild(flow); body(flow); }
		finally
		{
			runner.RemoveChild(stage); stage.QueueFree();
			MenuSettings.Reset();
			FightScene.Sources = saved; FightScene.Run = savedRun; FightScene.Opponent = savedFoe;
		}
	}
	static string TmpSave() { string d = Path.Combine(Path.GetTempPath(), "yok72-probe-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return Path.Combine(d, "progress.json"); }
	static void Cleanup(string f) { try { Directory.Delete(Path.GetDirectoryName(f)!, true); } catch (IOException) { } }

	static FightScene StartAndLose(GameFlow flow)
	{
		Press(flow.Title.StartButton);
		Press(flow.ControlSelect.Start);
		Assert.True(flow.ChooseNode(flow.Session!.Reachable()[0]).Ok, "entered a node");
		var f = flow.Fight!;
		Lose(f);
		return f;
	}

	// Claim: routes once. Two presses before the freed scene is gone: one Changed event Fight->Title, no second route.
	[Test]
	public static void BackToTitleTwice_RoutesOnce_NoSecondChange(Node runner)
	{
		string save = TmpSave();
		try
		{
			WithBoot(runner, save, null, flow =>
			{
				var f = StartAndLose(flow);
				var lose = f.GetNode<LoseScreen>("Hud/LoseScreen");
				int changes = 0; flow.Router.Changed += (_, _) => changes++;
				Press(lose.BackToTitle);
				Press(lose.BackToTitle);
				Assert.Equal(1, changes, "exactly one screen change");
				Assert.Equal(Screen.Title, flow.Router.Current, "on the title");
				Assert.True(flow.Saved.Run == null && flow.Title.ContinueButton.Disabled, "no run, no Continue");
			});
		}
		finally { Cleanup(save); }
	}

	// Same frame: Back to title then Restart (and Restart then Back to title). First action wins; the screen does not end on the map.
	[Test]
	public static void BackToTitleThenRestart_SameFrame_EndsOnTitle(Node runner)
	{
		string save = TmpSave();
		try
		{
			WithBoot(runner, save, null, flow =>
			{
				var f = StartAndLose(flow);
				var lose = f.GetNode<LoseScreen>("Hud/LoseScreen");
				Press(lose.BackToTitle); Press(lose.Restart);
				Assert.Equal(Screen.Title, flow.Router.Current, "the title stays: a stale Restart must not pull the player to the map");
				Assert.True(flow.Saved.Run == null && flow.Title.ContinueButton.Disabled, "still no run saved by the stale Restart");
			});
			WithBoot(runner, save, null, flow =>
			{
				var f = StartAndLose(flow);
				var lose = f.GetNode<LoseScreen>("Hud/LoseScreen");
				Press(lose.Restart); Press(lose.BackToTitle);
				Assert.True(flow.Router.Current == Screen.Map || flow.Router.Current == Screen.Title, "one of the two routes");
				Assert.True(flow.Fight == null || flow.Router.Current != Screen.Title || flow.Title.Visible, "consistent screen");
			});
		}
		finally { Cleanup(save); }
	}

	// Refused: a button signal outside Lost/Complete (mid fight, after Restart) raises nothing and changes nothing.
	[Test]
	public static void BackToTitleSignal_OutsideEndStages_Refused(Node runner)
	{
		var saved = FightScene.Sources; var savedRun = FightScene.Run;
		FightScene.Sources = FightScene.FixtureSources; FightScene.Run = null;
		var stage = new SubViewport { Size = new Vector2I(1920, 1080) };
		runner.AddChild(stage);
		try
		{
			var s = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
			s.ExternalDrive = true; s.FixedRunSeed = 72; s.HeldBitsSource = () => InputBits.None;
			stage.AddChild(s);
			int titles = 0; s.TitleRequested += () => titles++;
			var lose = s.GetNode<LoseScreen>("Hud/LoseScreen");
			ulong h = s.Match.StateHash();
			Press(lose.BackToTitle); Press(s.CompleteScreen.BackToTitle);
			Assert.True(titles == 0 && s.Stage == DemoStage.Fighting && h == s.Match.StateHash(), "mid fight: refused, nothing changed");
			Lose(s);
			Press(lose.Restart);
			Press(lose.BackToTitle); Press(s.CompleteScreen.BackToTitle);
			Assert.Equal(0, titles, "after Restart the hidden buttons raise nothing");
			// Lost stage must not let the Complete card's button route either.
			Lose(s);
			Press(s.CompleteScreen.BackToTitle);
			Assert.True(titles <= 1, "at most one route");
		}
		finally { runner.RemoveChild(stage); stage.QueueFree(); FightScene.Sources = saved; FightScene.Run = savedRun; }
	}

	// Focus: d-pad / arrows do not wrap past the ends or leave the two buttons; Left from Restart and Right from Back stay put.
	[Test]
	public static void LoseScreen_NavigationDoesNotWrapOrEscape(Node runner)
	{
		var saved = FightScene.Sources; var savedRun = FightScene.Run;
		FightScene.Sources = FightScene.FixtureSources; FightScene.Run = null;
		var stage = new SubViewport { Size = new Vector2I(1920, 1080) };
		runner.AddChild(stage);
		try
		{
			var s = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
			s.ExternalDrive = true; s.FixedRunSeed = 72; s.HeldBitsSource = () => InputBits.None;
			stage.AddChild(s);
			int titles = 0; s.TitleRequested += () => titles++;
			Lose(s);
			var lose = s.GetNode<LoseScreen>("Hud/LoseScreen");
			Assert.True(lose.Restart.HasFocus(), "Restart first");
			Tap(s, Key.Left); PadTap(s, JoyButton.DpadLeft);
			Assert.True(lose.Restart.HasFocus(), "Left at the left end stays on Restart");
			Tap(s, Key.Up); Tap(s, Key.Down); PadTap(s, JoyButton.DpadUp); PadTap(s, JoyButton.DpadDown);
			Assert.True(lose.Restart.HasFocus(), "Up/Down stay put");
			PadTap(s, JoyButton.DpadRight); PadTap(s, JoyButton.DpadRight); Tap(s, Key.Right);
			Assert.True(lose.BackToTitle.HasFocus(), "Right at the right end stays on Back to title (no wrap)");
			Tap(s, Key.Tab);
			var owner = s.GetViewport().GuiGetFocusOwner();
			Assert.True(owner == lose.Restart || owner == lose.BackToTitle, "Tab stays inside the end screen");
			Assert.Equal(0, titles, "navigation alone routes nothing");
		}
		finally { runner.RemoveChild(stage); stage.QueueFree(); FightScene.Sources = saved; FightScene.Run = savedRun; }
	}

	// Input on the frame the lose screen appears: Enter held through the KO frame must not press Back to title; focus is on Restart.
	[Test]
	public static void EnterOnTheLoseFrame_PressesRestartNotTitle(Node runner)
	{
		var saved = FightScene.Sources; var savedRun = FightScene.Run;
		FightScene.Sources = FightScene.FixtureSources; FightScene.Run = null;
		var stage = new SubViewport { Size = new Vector2I(1920, 1080) };
		runner.AddChild(stage);
		try
		{
			var s = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
			s.ExternalDrive = true; s.FixedRunSeed = 72; s.HeldBitsSource = () => InputBits.None;
			stage.AddChild(s);
			int titles = 0; s.TitleRequested += () => titles++;
			Push(s, new InputEventKey { Keycode = Key.Enter, PhysicalKeycode = Key.Enter, Pressed = true });
			Lose(s);
			Tap(s, Key.Enter);
			Assert.Equal(0, titles, "Enter on the lose frame is Restart's (focus starts there)");
		}
		finally { runner.RemoveChild(stage); stage.QueueFree(); FightScene.Sources = saved; FightScene.Run = savedRun; }
	}

	// Pause menu over the end screen: not available in Lost/Complete (CanPause), so Esc cannot open it over the end screen.
	[Test]
	public static void PauseOverEndScreen_Refused(Node runner)
	{
		var saved = FightScene.Sources; var savedRun = FightScene.Run;
		FightScene.Sources = FightScene.FixtureSources; FightScene.Run = null;
		var stage = new SubViewport { Size = new Vector2I(1920, 1080) };
		runner.AddChild(stage);
		try
		{
			var s = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
			s.ExternalDrive = true; s.PauseEnabled = true; s.FixedRunSeed = 72; s.HeldBitsSource = () => InputBits.None;
			stage.AddChild(s);
			Lose(s);
			Assert.True(!s.TryPause() && !s.Paused, "no pause over the lose screen");
			Tap(s, Key.Escape);
			Assert.True(!s.Paused, "Esc does not open it either");
			var lose = s.GetNode<LoseScreen>("Hud/LoseScreen");
			Assert.True(lose.Visible && lose.Restart.HasFocus(), "the lose screen keeps focus");
		}
		finally { runner.RemoveChild(stage); stage.QueueFree(); FightScene.Sources = saved; FightScene.Run = savedRun; }
	}

	// Dev jump: losing a dev fight and Back to title leaves the real save file byte for byte.
	[Test]
	public static void DevJumpLoss_BackToTitle_RealSaveUntouched(Node runner)
	{
		string save = TmpSave();
		DevMode.Configure(new DevModeState(true, true));
		try
		{
			WithBoot(runner, save, null, flow =>
			{
				Press(flow.Title.StartButton); Press(flow.ControlSelect.Start);
				Assert.True(flow.ChooseNode(flow.Session!.Reachable()[0]).Ok, "real run entered");
				flow.Fight!.RequestTitle();
				Assert.True(flow.Saved.Run != null && flow.Title.Visible, "the real run is saved, on the title");
				var before = flow.Saved;
				var pool = FightScene.LoadAbilityPool();
				var jump = DevJump.BuildFight(new DevFightSetup("kitsune"), pool);
				Assert.True(flow.StartDevRun(jump.Session, jump.NodeId, ControlScheme.Kihon).Ok, "dev jump");
				var f = flow.Fight!;
				Lose(f);
				Assert.True(ReferenceEquals(before, flow.Saved) || before == flow.Saved, "the saved data is untouched by the dev loss");
				Press(f.GetNode<LoseScreen>("Hud/LoseScreen").BackToTitle);
				Assert.Equal(Screen.Title, flow.Router.Current, "title");
				Assert.True(before == flow.Saved, "and by Back to title");
				Assert.True(flow.Saved.Run != null && !flow.Title.ContinueButton.Disabled, "the real run is still continuable");
			});
		}
		finally { DevMode.ResetToLaunch(); DevRunStates.Reset(); Cleanup(save); }
	}

	// M1/M4: a won demo then Back to title keeps unlocks and story flags in memory and on disk; the run is gone.
	[Test]
	public static void DemoCompleteWin_BackToTitle_KeepsUnlocksAndStory(Node runner)
	{
		string save = TmpSave();
		try
		{
			WithBoot(runner, save, FightScene.DemoMapFile, flow =>
			{
				Press(flow.Title.StartButton); Press(flow.ControlSelect.Start);
				flow.Map.Confirm();
				Win(flow.Fight!, 0);
				flow.Fight!.BindingCard.Continue();
				flow.Fight.Rewards.Confirm();
				if (flow.Fight?.Rewards.Step == RewardStep.Target) flow.Fight.Rewards.Confirm();
				flow.Map.Confirm();
				var last = flow.Fight!;
				Win(last, 0);
				Assert.Equal(DemoStage.Complete, last.Stage, "complete");
				var before = flow.Saved;
				Press(last.CompleteScreen.BackToTitle);
				Press(last.CompleteScreen.BackToTitle);
				Assert.Equal(Screen.Title, flow.Router.Current, "title");
				Assert.True(flow.Saved.Run == null && flow.Title.ContinueButton.Disabled, "no run, no Continue");
				Assert.True(flow.Saved.Unlocks.SequenceEqual(before.Unlocks) && flow.Saved.Story.Equals(before.Story), "unlocks and story as they were at the win");
			});
		}
		finally { Cleanup(save); }
	}

}
