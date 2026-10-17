using System;
using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Flow;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-72: "Back to title" beside Restart on the lose screen and the demo-complete card. Both route through
/// <see cref="FightScene.RequestTitle"/> like the pause menu's Quit to title; the run has ended by then, so the saved run is
/// already gone and the title offers no Continue. A press outside the end stages, or with no boot flow, changes nothing.
/// </summary>
public static class EndScreenTitleTests
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

	static void LoseDuel(FightScene s)
	{
		for (int t = 0; t < 600 && s.Match.Phase != MatchPhase.Over; t++) s.Step(Idle, Strike(t));
		Assert.Equal(1, s.Match.Winner, "Kitsune won");
	}
	static void WinDuel(FightScene s, int taken)
	{
		for (int t = 0; t < taken * 2; t++) s.Step(Idle, Strike(t));
		for (int t = 0; t < 600 && s.Match.Phase != MatchPhase.Over; t++) s.Step(Strike(t), Idle);
		Assert.Equal(0, s.Match.Winner, "Ryo won");
	}

	/// <summary>A standalone fight in a viewport of its own (clean focus and input).</summary>
	static void WithScene(Node runner, Action<FightScene> body)
	{
		var saved = FightScene.Sources; var savedRun = FightScene.Run;
		FightScene.Sources = FightScene.FixtureSources; FightScene.Run = null;
		var stage = new SubViewport { Size = new Vector2I(1920, 1080), Name = "EndTitleStage" };
		runner.AddChild(stage);
		try
		{
			var s = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
			s.ExternalDrive = true; s.FixedRunSeed = 72;
			s.HeldBitsSource = () => InputBits.None;
			stage.AddChild(s);
			body(s);
		}
		finally { runner.RemoveChild(stage); stage.QueueFree(); FightScene.Sources = saved; FightScene.Run = savedRun; }
	}

	static void ReachComplete(FightScene s)
	{
		WinDuel(s, 1);
		s.BindingCard.Continue();
		var cards = s.Draft!.Offer.Cards;
		int i = cards.ToList().FindIndex(c => c.Kind == Ui.CardKind.Modifier);
		while (s.Rewards.CardCursor != i) s.Rewards.Move(1);
		s.Rewards.Confirm(); s.Rewards.Confirm();
		WinDuel(s, 0);
		Assert.Equal(DemoStage.Complete, s.Stage, "demo complete");
	}

	[Test]
	public static void LoseScreen_BackToTitle_RoutesOnce_RestartStillWorks(Node runner) => WithScene(runner, s =>
	{
		int titles = 0; s.TitleRequested += () => titles++;
		Assert.True(!s.LeaveForTitle() && titles == 0, "refused mid-fight, nothing raised");
		LoseDuel(s);
		var lose = s.GetNode<LoseScreen>("Hud/LoseScreen");
		Assert.True(lose.Visible && lose.BackToTitle.Visible, "lose screen with the button");
		Assert.True(lose.BackToTitle.GetParent() == lose.Restart.GetParent() && lose.Restart.GetNode(lose.Restart.FocusNeighborRight) == lose.BackToTitle, "beside Restart, reachable by right");
		Assert.True(lose.Restart.GetNode(lose.BackToTitle.FocusNeighborLeft) == lose.Restart, "and back by left");
		Press(lose.BackToTitle);
		Assert.Equal(1, titles, "Back to title raised TitleRequested once");
		Press(lose.Restart);
		Assert.Equal(1, titles, "Restart does not route to the title");
		Assert.Equal(DemoStage.Fighting, s.Stage, "Restart gave a fresh run");
	});

	[Test]
	public static void LoseScreen_KeyboardAndPad_ReachBackToTitle(Node runner) => WithScene(runner, s =>
	{
		int titles = 0; s.TitleRequested += () => titles++;
		LoseDuel(s);
		var lose = s.GetNode<LoseScreen>("Hud/LoseScreen");
		Assert.True(lose.Restart.HasFocus(), "Restart focused first");
		Tap(s, Key.Right);
		Assert.True(lose.BackToTitle.HasFocus(), "Right moves to Back to title");
		Tap(s, Key.Enter);
		Assert.Equal(1, titles, "Enter presses it");
		lose.Restart.GrabFocus();
		PadTap(s, JoyButton.DpadRight);
		Assert.True(lose.BackToTitle.HasFocus(), "pad d-pad moves too");
		PadTap(s, JoyButton.A);
		Assert.Equal(2, titles, "pad A presses it");
	});

	[Test]
	public static void DemoComplete_BackToTitle_RoutesOnce(Node runner) => WithScene(runner, s =>
	{
		int titles = 0; s.TitleRequested += () => titles++;
		ReachComplete(s);
		var c = s.CompleteScreen;
		Assert.True(c.Restart.HasFocus(), "Restart focused first");
		Tap(s, Key.Right);
		Assert.True(c.BackToTitle.HasFocus(), "Right moves to Back to title");
		Tap(s, Key.Enter);
		Assert.Equal(1, titles, "Enter routes to the title once");
		Assert.Equal(DemoStage.Complete, s.Stage, "the scene itself did not restart; the boot flow owns the switch");
		Press(c.Restart);
		Assert.Equal(1, titles, "Restart does not route to the title");
		Assert.Equal(DemoStage.Fighting, s.Stage, "Restart gave a fresh run");
	});

	[Test]
	public static void BackToTitle_WithNoBootFlow_StaysOnTheScreen(Node runner) => WithScene(runner, s =>
	{
		LoseDuel(s);
		Press(s.GetNode<LoseScreen>("Hud/LoseScreen").BackToTitle);
		Assert.True(s.GetNode<Control>("Hud/LoseScreen").Visible && s.Stage == DemoStage.Lost, "nothing listening: the lose screen stays");
	});

	[Test]
	public static void LoseScreen_BackToTitle_ThroughBootFlow_LeavesTheSaveWithoutARun(Node runner)
	{
		string dir = Path.Combine(Path.GetTempPath(), "yokai-endtitle-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(dir);
		string save = Path.Combine(dir, "progress.json");
		var saved = FightScene.Sources; var savedRun = FightScene.Run; var savedFoe = FightScene.Opponent;
		FightScene.Sources = FightScene.FixtureSources; FightScene.Run = null;
		MenuSettings.Reset();
		var flow = GD.Load<PackedScene>("res://scenes/boot.tscn").Instantiate<GameFlow>();
		flow.QuitAction = () => { };
		flow.FixedRunSeed = 72;
		flow.SaveFile = save;
		flow.ConfigureFight = f => { f.ExternalDrive = true; f.HeldBitsSource = () => InputBits.None; };
		var stage = new SubViewport { Size = new Vector2I(1920, 1080), Name = "EndTitleBootStage" };
		runner.AddChild(stage);
		try
		{
			stage.AddChild(flow);
			Press(flow.Title.StartButton);
			Press(flow.ControlSelect.Start);
			Assert.True(flow.ChooseNode(flow.Session!.Reachable()[0]).Ok, "entered the first node");
			Assert.True(flow.Saved.Run != null && File.Exists(save), "the run is saved while it lives");
			string held = File.ReadAllText(save);
			var fight = flow.Fight!;
			LoseDuel(fight);
			Assert.True(flow.Saved.Run == null, "the run ended: the saved run is gone");
			Assert.True(File.ReadAllText(save) != held, "and the file on disk was rewritten without it");
			Press(fight.GetNode<LoseScreen>("Hud/LoseScreen").BackToTitle);
			Assert.Equal(Screen.Title, flow.Router.Current, "back on the title");
			Assert.True(flow.Fight == null && flow.Title.Visible, "fight freed, title shown");
			Assert.True(flow.Title.ContinueButton.Disabled, "no Continue for the ended run");
		}
		finally
		{
			runner.RemoveChild(stage); stage.QueueFree();
			MenuSettings.Reset();
			FightScene.Sources = saved; FightScene.Run = savedRun; FightScene.Opponent = savedFoe;
			try { Directory.Delete(dir, true); } catch (IOException) { }
		}
	}
}
