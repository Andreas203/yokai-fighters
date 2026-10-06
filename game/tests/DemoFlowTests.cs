using System;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;
using YokaiFighters.Story;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-48: the YOK-39 demo flow, driven headless through the fight scene (fixture kits): win → binding line →
/// reward pick (each card type) → rematch with the pick and the carried health; loss → lose screen → Restart
/// = a fresh run; the won rematch → demo complete → Restart; the debug temperament menu.
/// </summary>
public static class DemoFlowTests
{
	static readonly FighterInput Idle = FighterInput.None;
	static FighterInput Strike(int t) => new(t % 2 == 0 ? InputBits.DebugStrike : InputBits.None);

	static FightScene NewScene(Node runner)
	{
		var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		scene.ExternalDrive = true;
		runner.AddChild(scene);
		return scene;
	}

	/// <summary>Runs a scene test on the fixture kits (never data/moves/) with no caller-set run, then frees it.</summary>
	static void WithScene(Node runner, Action<FightScene> body)
	{
		var saved = FightScene.Sources;
		var savedRun = FightScene.Run;
		FightScene.Sources = FightScene.FixtureSources;
		FightScene.Run = null;
		FightScene? scene = null;
		try { scene = NewScene(runner); body(scene); }
		finally
		{
			scene?.QueueFree();
			FightScene.Sources = saved;
			FightScene.Run = savedRun;
		}
	}

	/// <summary>Kitsune lands <paramref name="taken"/> debug strikes, then Ryo strikes until the KO slow-down ends.</summary>
	static void WinDuel(FightScene s, int taken)
	{
		for (int t = 0; t < taken * 2; t++) s.Step(Idle, Strike(t));
		for (int t = 0; t < 600 && s.Match.Phase != MatchPhase.Over; t++) s.Step(Strike(t), Idle);
		Assert.Equal(MatchPhase.Over, s.Match.Phase, "duel over");
		Assert.Equal(0, s.Match.Winner, "Ryo won");
	}

	static void LoseDuel(FightScene s)
	{
		for (int t = 0; t < 600 && s.Match.Phase != MatchPhase.Over; t++) s.Step(Idle, Strike(t));
		Assert.Equal(MatchPhase.Over, s.Match.Phase, "duel over");
		Assert.Equal(1, s.Match.Winner, "Kitsune won");
	}

	/// <summary>Picks the card of <paramref name="kind"/> through the screen's own controls; returns it.</summary>
	static Ui.CardView PickThroughScreen(FightScene s, Ui.CardKind kind)
	{
		var screen = s.Rewards;
		var cards = s.Draft!.Offer.Cards;
		int index = cards.ToList().FindIndex(c => c.Kind == kind);
		Assert.True(index >= 0, $"the offer has a {kind} card");
		while (screen.CardCursor != index) screen.Move(1);
		screen.Confirm();
		if (kind == Ui.CardKind.Modifier)
		{
			Assert.Equal(RewardStep.Target, screen.Step, "modifier opens the target step");
			screen.Confirm(); // the cursor starts on the first eligible special
		}
		return cards[index];
	}

	[Test]
	public static void Boot_IsTheFirstFightOnAFreshRun(Node runner) => WithScene(runner, s =>
	{
		Assert.Equal(DemoStage.Fighting, s.Stage, "boots into the fight");
		Assert.Equal(1, s.Duel, "first duel");
		Assert.Equal(RunState.MaxHealth, s.Match.P1.Health, "Ryo at 1,000 (C1)");
		Assert.Equal(RunState.MaxHealth, s.DemoRun.Health, "fresh run");
		Assert.Equal(ControlScheme.Kihon, s.SchemeOf(0), "Kihon default");
		Assert.Equal("kitsune", FightScene.Opponent, "against the Kitsune");
		Assert.True(s.Recorder != null && s.Recorder.Recording.Header.RunHash == s.DemoRun.StateHash().ToString("x16"), "recording carries the run");
	});

	[Test]
	public static void Win_BindingRewardRematch_ForEachCardKind(Node runner)
	{
		foreach (var kind in new[] { Ui.CardKind.NewMove, Ui.CardKind.Upgrade, Ui.CardKind.Modifier })
			WithScene(runner, s =>
			{
				WinDuel(s, taken: 3); // Ryo ends on 700
				Assert.Equal(700, s.Match.P1.Health, "Ryo's health left");
				Assert.Equal(DemoStage.Binding, s.Stage, $"{kind}: binding line after the win");
				Assert.True(s.BindingCard.Showing, "binding card shown");
				Assert.Equal(FightScene.LoadStory().BindingLine("Kitsune"), s.BindingCard.Text, "binding line from data/story, {yokai} = Kitsune");
				Assert.Equal(750, s.DemoRun.Health, "R3: 700 left + 50");
				Assert.Equal(1, s.DuelRecordings.Count, "duel 1 recording kept");
				Assert.Equal(0, s.DuelRecordings[0].Header.Result!.Winner, "duel 1 result stamped");

				s.BindingCard.Continue();
				Assert.Equal(DemoStage.Reward, s.Stage, "reward screen after the binding line");
				Assert.True(s.Rewards.Showing, "reward screen shown");
				var kinds = s.Draft!.Offer.Cards.Select(c => c.Kind).ToArray();
				Assert.Equal(3, kinds.Length, "three cards");
				Assert.True(kinds.Contains(Ui.CardKind.NewMove) && kinds.Contains(Ui.CardKind.Upgrade) && kinds.Contains(Ui.CardKind.Modifier), "NEW, UPGRADE and MODIFIER from the Kitsune");

				var card = PickThroughScreen(s, kind);
				Assert.Equal(DemoStage.Fighting, s.Stage, "rematch starts after the pick");
				Assert.Equal(2, s.Duel, "rematch");
				Assert.Equal(MatchPhase.Fighting, s.Match.Phase, "new match");
				Assert.Equal(750, s.Match.P1.Health, "rematch at the carried health");
				Assert.Equal(RunState.MaxHealth, s.Match.P1.MaxHealth, "max health unchanged");
				Assert.True(!s.Rewards.Showing && !s.BindingCard.Showing, "screens closed");

				var specials = s.Match.P1.Specials;
				switch (kind)
				{
					case Ui.CardKind.NewMove:
						Assert.Equal(card.Id, specials[(int)SpecialSlot.C]?.Data.Id, "drafted special in slot C");
						break;
					case Ui.CardKind.Upgrade:
						Assert.True(specials.Any(e => e?.Data.Id == card.Id && e.Level == 2), $"{card.Id} at Lv 2 in the rematch");
						break;
					default:
						Assert.True(specials.Any(e => e?.Modifier?.Id == card.Id), $"{card.Id} attached in the rematch");
						break;
				}
				var h = s.Recorder!.Recording.Header;
				Assert.Equal(750, h.P1StartHealth, "rematch recording: carried health");
				Assert.Equal(s.DemoRun.StateHash().ToString("x16"), h.RunHash, "rematch recording: run with the pick");
				Assert.Equal(0, s.Recorder.Recording.Steps, "rematch recording starts at tick 0");
			});
	}

	[Test]
	public static void HealthCarry_CapsAt1000(Node runner) => WithScene(runner, s =>
	{
		WinDuel(s, taken: 0);
		s.BindingCard.Continue();
		PickThroughScreen(s, Ui.CardKind.Upgrade);
		Assert.Equal(RunState.MaxHealth, s.Match.P1.Health, "1,000 left + 50 capped at 1,000");
	});

	[Test]
	public static void Loss_LoseScreen_RestartGivesAFreshRun(Node runner) => WithScene(runner, s =>
	{
		var firstRun = s.DemoRun;
		LoseDuel(s);
		Assert.Equal(DemoStage.Lost, s.Stage, "lost");
		var hud = s.GetNode<FightHud>("Hud");
		Assert.True(hud.GetNode<Control>("LoseScreen").Visible, "lose screen shown");
		var (title, text) = FightScene.LoadStory().LoseScreen(StoryLibrary.DisplayName(FightScene.Opponent));
		Assert.Equal(text, hud.LoseText, "lose text from data/story/lose-screen.json");
		Assert.Equal(title, hud.LoseTitle, "lose title from data/story/lose-screen.json");

		hud.GetNode<Button>("LoseScreen/Restart").EmitSignal(BaseButton.SignalName.Pressed);
		Assert.Equal(DemoStage.Fighting, s.Stage, "back to fighting");
		Assert.Equal(1, s.Duel, "first fight");
		Assert.True(!ReferenceEquals(firstRun, s.DemoRun), "a new RunState");
		Assert.Equal(RunState.MaxHealth, s.Match.P1.Health, "Ryo at 1,000");
		Assert.Equal(RunState.MaxHealth, s.Match.P2.Health, "Kitsune at full");
		Assert.True(!hud.GetNode<Control>("LoseScreen").Visible, "lose screen hidden");
		Assert.Equal(0, s.DuelRecordings.Count, "run's recordings cleared");
		Assert.Equal(0, s.Recorder!.Recording.Steps, "a new recording");
	});

	[Test]
	public static void RematchLoss_RestartDropsThePick(Node runner) => WithScene(runner, s =>
	{
		WinDuel(s, taken: 2);
		s.BindingCard.Continue();
		PickThroughScreen(s, Ui.CardKind.NewMove);
		Assert.True(s.Match.P1.Specials[(int)SpecialSlot.C] != null, "pick active in the rematch");
		LoseDuel(s);
		Assert.Equal(DemoStage.Lost, s.Stage, "rematch lost");
		Assert.True(s.GetNode<Control>("Hud/LoseScreen").Visible, "lose screen after the rematch");
		s.GetNode<Button>("Hud/LoseScreen/Restart").EmitSignal(BaseButton.SignalName.Pressed);
		Assert.Equal(1, s.Duel, "first fight again");
		Assert.True(s.Match.P1.Specials[(int)SpecialSlot.C] == null && s.DemoRun[SpecialSlot.C] == null, "drafted special gone");
		Assert.Equal(RunState.MaxHealth, s.Match.P1.Health, "full health again");
	});

	[Test]
	public static void RematchWin_DemoComplete_Restart(Node runner) => WithScene(runner, s =>
	{
		WinDuel(s, taken: 1);
		s.BindingCard.Continue();
		PickThroughScreen(s, Ui.CardKind.Modifier);
		WinDuel(s, taken: 0);
		Assert.Equal(DemoStage.Complete, s.Stage, "demo complete after the won rematch");
		Assert.True(s.CompleteScreen.Visible, "demo complete card shown");
		Assert.True(!s.BindingCard.Showing && !s.Rewards.Showing, "no third draft");
		Assert.Equal(2, s.DuelRecordings.Count, "both duels recorded");
		Assert.True(s.DuelRecordings[1].Header.RunHash != s.DuelRecordings[0].Header.RunHash, "rematch recording carries the changed run");
		s.CompleteScreen.Restart.EmitSignal(BaseButton.SignalName.Pressed);
		Assert.Equal(DemoStage.Fighting, s.Stage, "restarted");
		Assert.True(!s.CompleteScreen.Visible, "card hidden");
		Assert.Equal(1, s.Duel, "first fight");
		Assert.True(s.Match.P1.Specials.All(e => e?.Modifier == null), "modifier gone with the old run");
	});

	[Test]
	public static void RestartMidDraft_ClosesTheScreens(Node runner) => WithScene(runner, s =>
	{
		WinDuel(s, taken: 1);
		s.BindingCard.Continue();
		s.RestartRun();
		Assert.True(!s.Rewards.Showing && !s.Rewards.Visible, "reward screen dismissed");
		s.Rewards.Confirm(); // ignored: nothing is offered
		Assert.Equal(1, s.Duel, "still the first fight");
		Assert.Equal(DemoStage.Fighting, s.Stage, "fighting");
	});

	[Test]
	public static void DebugMenu_SwitchesTemperament(Node runner) => WithScene(runner, s =>
	{
		Assert.Equal("aggressive", s.Temperament, "aggressive by default");
		Assert.True(s.SetTemperament("patient"), "patient exists");
		Assert.Equal("patient", s.P2Ai!.Profile.Temperament, "AI rebuilt as patient");
		Assert.True(!s.SetTemperament("sleepy"), "unknown temperament refused");
		Assert.Equal("patient", s.Temperament, "unchanged by a refused switch");

		var menu = s.DebugMenu;
		Assert.True(menu != null, "debug menu exists in debug builds");
		s.ToggleDebugMenu();
		Assert.True(menu!.Visible && s.FlowPaused, "F9 opens the menu and pauses");
		menu.GetNode<Button>("Box/Temperament_aggressive").EmitSignal(BaseButton.SignalName.Pressed);
		Assert.Equal("aggressive", s.P2Ai!.Profile.Temperament, "menu button switches the temperament");
		Assert.Equal(s.P2Ai.Profile.Id, s.Recorder!.Recording.Header.Ai!.ProfileId, "recording logs the switch");

		WinDuel(s, taken: 0);
		menu.GetNode<Button>("Box/RestartRun").EmitSignal(BaseButton.SignalName.Pressed);
		Assert.True(!menu.Visible && !s.FlowPaused, "menu closes on restart");
		Assert.Equal(DemoStage.Fighting, s.Stage, "restart from the menu = first fight");
		Assert.Equal("aggressive", s.P2Ai!.Profile.Temperament, "the chosen temperament carries into the restart");
		Assert.Equal(FightScene.DebugMenuKey, Key.F9, "F9: clear of E9 keys and the F1-F8 debug keys");
	});

	/// <summary>Designer decision (YOK-48): each new run draws a new reward seed; a pinned seed stays put.</summary>
	[Test]
	public static void RunSeed_NewPerRunUnlessPinned(Node runner) => WithScene(runner, s =>
	{
		var seeds = new System.Collections.Generic.HashSet<uint> { s.RunSeed };
		for (int i = 0; i < 8; i++) { s.RestartRun(); seeds.Add(s.RunSeed); }
		Assert.True(seeds.Count > 1, "restarts draw new seeds");

		s.FixedRunSeed = 7;
		s.RestartRun();
		Assert.Equal(7u, s.RunSeed, "pinned seed");
		s.RestartRun();
		Assert.Equal(7u, s.RunSeed, "pinned seed survives restart");
	});

	[Test]
	public static void DemoComplete_ShowsTheStoryCard(Node runner) => WithScene(runner, s =>
	{
		var lib = FightScene.LoadStory();
		if (!lib.Has(StoryLibrary.DemoCompleteId)) { Assert.Equal(DemoCompleteScreen.PlaceholderTitle, s.CompleteScreen.TitleText, "no card: placeholder"); return; }
		var (title, text) = lib.DemoComplete("Kitsune");
		Assert.Equal(title, s.CompleteScreen.TitleText, "title from data/story/demo-complete.json");
		Assert.Equal(text, s.CompleteScreen.BodyText, "text from the card");
		Assert.True(text.StartsWith("Kitsune ") && !text.Contains('{'), "{yokai} filled with the opponent's display name");
	});

	[Test]
	public static void DemoComplete_KeepsThePlaceholderWithoutACard()
	{
		var lib = new StoryLibrary(Array.Empty<StoryCardData>());
		bool threw = false;
		try { lib.DemoComplete("Kitsune"); } catch (System.Collections.Generic.KeyNotFoundException) { threw = true; }
		Assert.True(threw, "missing card throws the exception ApplyDemoCompleteCard catches");
		var screen = new DemoCompleteScreen();
		Assert.Equal(DemoCompleteScreen.PlaceholderTitle, screen.TitleText, "placeholder title");
		Assert.Equal(DemoCompleteScreen.PlaceholderText, screen.BodyText, "placeholder text");
		screen.SetText(null, "x");
		Assert.Equal(DemoCompleteScreen.PlaceholderTitle, screen.TitleText, "a card without a title keeps the placeholder title");
		screen.Free();
	}
}
