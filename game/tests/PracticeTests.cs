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
/// YOK-197, GDD 8: practice mode. The router's Practice screen, the training dummy (held stances, no AI), the
/// practice fight scene (the dojo's dummy, Ryo's starters, reset on KO or the button, the YOK-22 overlay, no
/// recording), determinism, and practice through the real boot. The save file side (nothing read or written) is in
/// SaveTests (the only test file that may name the save).
/// </summary>
public static class PracticeTests
{
	// ---- router (no Godot) ----

	[Test]
	public static void Router_Practice_FromTheTitleOnly_OnceEnabled_AndBack()
	{
		var r = new ScreenRouter();
		var seen = new List<string>();
		var activated = new List<TitleEntry>();
		r.Changed += (from, to) => seen.Add($"{from}>{to}");
		r.Activated += activated.Add;
		Assert.True(!r.StartPractice() && r.Current == Screen.Title, "greyed by default: no practice");
		r.Enable(TitleEntry.Practice);
		Assert.True(r.Select(TitleEntry.Practice) && r.Current == Screen.Title, "Select hands Practice to the flow (no screen change of its own)");
		Assert.Equal("Practice", string.Join("|", activated), "Activated(Practice)");
		Assert.True(r.StartPractice(), "practice starts from the title");
		Assert.Equal(Screen.Practice, r.Current, "on the practice screen");
		Assert.True(!r.StartPractice() && !r.Select(TitleEntry.Start) && !r.Back() && !r.Confirm() && !r.EnterNode() && !r.ReturnToMap() && !r.ContinueRun() && !r.StartFightDirect(ControlScheme.Kihon), "menu and run actions do nothing in practice");
		Assert.True(!r.ChooseScheme(ControlScheme.Kata) && r.Scheme == ControlScheme.Kihon, "the scheme is fixed while practising, as in a fight");
		Assert.True(!r.OpenShop(), "no shop in practice");
		Assert.True(r.ReturnToTitle() && r.Current == Screen.Title, "Quit to title");
		r.Select(TitleEntry.Start);
		Assert.True(!r.StartPractice(), "not from control select");
		Assert.Equal("Title>Practice|Practice>Title|Title>ControlSelect", string.Join("|", seen), "transitions");
	}

	// ---- the dummy (no Godot) ----

	[Test]
	public static void Dummy_CyclesItsStances_AndHoldsTheTrialDummysInputs()
	{
		var d = new PracticeDummy();
		Assert.Equal(PracticeStance.Stand, d.Stance, "stands by default");
		Assert.Equal("Block|Crouch|CrouchBlock|Stand", string.Join("|", Enumerable.Range(0, 4).Select(_ => d.Next())), "menu order, wrapping");
		var f = new Fighter();
		foreach (int facing in new[] { -1, +1 })
		{
			f.Facing = facing;
			InputBits back = facing > 0 ? InputBits.Left : InputBits.Right;
			var want = new Dictionary<PracticeStance, InputBits>
			{
				[PracticeStance.Stand] = InputBits.None,
				[PracticeStance.Block] = back,
				[PracticeStance.Crouch] = InputBits.Down,
				[PracticeStance.CrouchBlock] = InputBits.Down | back,
			};
			foreach (var (stance, bits) in want)
			{
				d.Stance = stance;
				Assert.Equal(new FighterInput(bits), d.Input(f), $"{stance} facing {facing}");
				Assert.Equal(TrialRun.HeldInput(PracticeDummy.ActionOf(stance), facing), d.Input(f), $"{stance}: the trial dummy's held input (R7)");
			}
		}
	}

	// ---- the practice fight scene ----

	static void WithPractice(Node runner, Action<FightScene> body, Action<FightScene>? configure = null)
	{
		string foe = FightScene.Opponent;
		var run = FightScene.Run;
		FightScene.Run = null;
		var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		scene.ExternalDrive = true;
		scene.PracticeMode = true;
		configure?.Invoke(scene);
		runner.AddChild(scene);
		try { body(scene); }
		finally
		{
			runner.RemoveChild(scene);
			scene.QueueFree();
			FightScene.Opponent = foe;
			FightScene.Run = run;
		}
	}

	[Test]
	public static void Scene_DojoDummy_RyosStarters_NoAi_NoRecording_NoNode(Node runner) => WithPractice(runner, scene =>
	{
		var dojo = FightScene.PracticeNode();
		Assert.True(scene.InPractice && scene.Dummy is not null, "practice mode with a dummy");
		Assert.Equal("dojo", dojo.Type, "the dummy comes from a dojo node (R7's dojo scene)");
		Assert.Equal(dojo.Fighter, FightScene.Opponent, "P2 is the dojo's dummy from node-scenes.json");
		Assert.True(!scene.P2AiEnabled && scene.P2Ai is null, "no AI profile drives the dummy");
		Assert.True(scene.Recorder is null, "no replay recording");
		Assert.Equal("", scene.Temperament, "the HUD names no temperament for the dummy");
		Assert.True(scene.Session is { Node: null, IsOver: false }, "the scene's own session is never entered (no node, no save point)");
		var fresh = RunState.NewRun(FightScene.LoadAbilityPool());
		foreach (var slot in new[] { SpecialSlot.A, SpecialSlot.B, SpecialSlot.C, SpecialSlot.D })
			Assert.Equal(fresh[slot]?.Data.Id, scene.DemoRun[slot]?.Data.Id, $"slot {slot}: Ryo's owned specials outside a run = his starters (A1)");
		Assert.Equal(RunState.MaxHealth, scene.Match.P1.Health, "Ryo at full health");
		Assert.True(scene.BuildPauseSummary().Position.StartsWith("Practice"), "the pause panel says practice, not a duel of a run: " + scene.BuildPauseSummary().Position);
		Assert.True(scene.GetNodeOrNull<DebugOverlay>("DebugOverlay") is { } overlay && overlay.HandleKey(DebugOverlay.ToggleKey) && overlay.ShowBoxes, "the YOK-22 overlay is there and F1 shows the boxes");
	});

	[Test]
	public static void Scene_DummyHoldsItsStance_BlockGuardsAndStandIsHit(Node runner)
	{
		foreach (var (stance, blocked) in new[] { (PracticeStance.Stand, false), (PracticeStance.Block, true) })
			WithPractice(runner, scene =>
			{
				scene.Dummy!.Stance = stance;
				HitEvent? hit = null;
				scene.Match.Hit += (_, e) => { if (e.Attacker == 0) hit ??= e; };
				var p2Start = scene.Match.P2.X;
				for (int t = 0; t < 600 && hit is null; t++)
					scene.Step(t % 40 == 0 ? FighterInput.Special(SpecialSlot.A) : FighterInput.None, new FighterInput(InputBits.Right | InputBits.Up));
				Assert.True(hit is not null, $"{stance}: Ryo's Spirit Wave reached the dummy");
				Assert.Equal(blocked, hit!.Value.Blocked, $"{stance}: blocked = {blocked}");
				Assert.True(!scene.Match.P2.Airborne, $"{stance}: P2's own input (Up) is ignored: the dummy holds its stance");
			});
	}

	/// <summary>Strikes the dummy (debug strike, 100 a press) until the scene has reset once.</summary>
	static int StrikeToKo(FightScene scene)
	{
		int start = scene.PracticeResets, t = 0;
		for (; t < 3000 && scene.PracticeResets == start; t++)
			scene.Step(new FighterInput(t % 2 == 0 ? InputBits.DebugStrike : InputBits.None), FighterInput.None);
		return t;
	}

	[Test]
	public static void Scene_KoResetsBothFighters_StanceKept_NoFlowScreen(Node runner) => WithPractice(runner, scene =>
	{
		var m = scene.Match;
		int p1x = m.P1.X, p2x = m.P2.X;
		scene.CyclePracticeStance(); // Block
		scene.Dummy!.Stance = PracticeStance.Stand; // debug strikes are unblockable anyway; stand for a plain KO
		int ticks = StrikeToKo(scene);
		Assert.Equal(1, scene.PracticeResets, $"one reset after the KO ({ticks} ticks)");
		Assert.True(ReferenceEquals(m, scene.Match), "the same match, reset (no new run, no node)");
		Assert.True(m.Phase == MatchPhase.Fighting && m.Tick == 0, "fighting again from tick 0");
		Assert.True(m.P2.Health == m.P2.MaxHealth && m.P1.Health == RunState.MaxHealth, "both at full health");
		Assert.True(m.P1.X == p1x && m.P2.X == p2x, "both back at their start");
		Assert.Equal(DemoStage.Fighting, scene.Stage, "no binding card, reward or end screen");
		Assert.True(scene.Draft is null && scene.Session!.Node is null, "nothing drafted, no node completed");
		Assert.Equal(PracticeStance.Stand, scene.Dummy.Stance, "the stance is kept across the reset");
	});

	[Test]
	public static void Scene_ResetButton_StanceKey_PauseRestart(Node runner) => WithPractice(runner, scene =>
	{
		var m = scene.Match;
		int p1x = m.P1.X;
		for (int t = 0; t < 30; t++) scene.Step(new FighterInput(InputBits.Right), FighterInput.None);
		Assert.True(m.P1.X != p1x, "Ryo walked");
		Assert.True(scene.HandlePracticeKey(FightScene.PracticeResetKey), "Backspace resets");
		Assert.True(m.P1.X == p1x && m.Tick == 0 && scene.PracticeResets == 1, "back at the start");
		Assert.True(scene.HandlePracticeKey(FightScene.PracticeStanceKey) && scene.Dummy!.Stance == PracticeStance.Block, "F5: the next stance");
		Assert.True(!scene.HandlePracticeKey(Key.F12), "other keys are not practice's");
		for (int t = 0; t < 10; t++) scene.Step(new FighterInput(InputBits.Right), FighterInput.None);
		scene.RestartRun(); // the pause menu's / end screens' Restart
		Assert.True(m.Tick == 0 && scene.PracticeResets == 2 && ReferenceEquals(m, scene.Match), "Restart in practice = reset positions, same match");
		Assert.Equal(PracticeStance.Block, scene.Dummy.Stance, "stance kept");
	}, s => s.PauseEnabled = true);

	[Test]
	public static void Scene_PausedPracticeKeysDoNothing(Node runner) => WithPractice(runner, scene =>
	{
		for (int t = 0; t < 5; t++) scene.Step(new FighterInput(InputBits.Right), FighterInput.None);
		Assert.True(scene.TryPause() && scene.Paused, "paused");
		int tick = scene.Match.Tick;
		Assert.True(!scene.HandlePracticeKey(FightScene.PracticeResetKey) && !scene.HandlePracticeKey(FightScene.PracticeStanceKey), "keys ignored behind the pause menu");
		Assert.True(scene.Match.Tick == tick && scene.Dummy!.Stance == PracticeStance.Stand && scene.PracticeResets == 0, "nothing changed");
	}, s => s.PauseEnabled = true);

	[Test]
	public static void Scene_NotPractice_HasNoDummy_AndIgnoresPracticeKeys(Node runner)
	{
		string foe = FightScene.Opponent;
		var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		scene.ExternalDrive = true;
		runner.AddChild(scene);
		try
		{
			Assert.True(!scene.InPractice && scene.Dummy is null, "an ordinary fight has no dummy");
			Assert.True(!scene.HandlePracticeKey(FightScene.PracticeResetKey) && !scene.ResetPractice() && scene.CyclePracticeStance() is null, "practice keys do nothing");
			Assert.True(scene.BuildPauseSummary().Position.StartsWith("Duel"), "the pause line is the run's");
		}
		finally { runner.RemoveChild(scene); scene.QueueFree(); FightScene.Opponent = foe; }
	}

	[Test]
	public static void Determinism_SameInputs_SameHashes_ThroughAResetAndAStanceChange(Node runner)
	{
		List<ulong> Play()
		{
			var hashes = new List<ulong>();
			WithPractice(runner, scene =>
			{
				for (int t = 0; t < 900; t++)
				{
					if (t == 200) scene.HandlePracticeKey(FightScene.PracticeStanceKey);
					if (t == 450) scene.HandlePracticeKey(FightScene.PracticeResetKey);
					InputBits b = (t % 7) switch { 0 => InputBits.DebugStrike, 1 or 2 => InputBits.Right, _ => InputBits.None };
					scene.Step(t % 53 == 0 ? FighterInput.Special(SpecialSlot.A) : new FighterInput(b), FighterInput.None);
					hashes.Add(scene.Match.StateHash());
				}
				Assert.True(scene.PracticeResets >= 2, "the script KOs the dummy at least once besides the button: " + scene.PracticeResets);
			});
			return hashes;
		}
		var a = Play();
		var b = Play();
		Assert.True(a.SequenceEqual(b), "900 ticks hash-identical across two practice sessions");
	}

	// ---- through the boot ----

	static void WithBoot(Node runner, Action<GameFlow> body)
	{
		var saved = FightScene.Sources;
		var savedRun = FightScene.Run;
		string foe = FightScene.Opponent;
		FightScene.Run = null;
		MenuSettings.Reset();
		var flow = GD.Load<PackedScene>("res://scenes/boot.tscn").Instantiate<GameFlow>();
		flow.QuitAction = () => { };
		flow.ConfigureFight = f => f.ExternalDrive = true;
		try { runner.AddChild(flow); body(flow); }
		finally
		{
			runner.RemoveChild(flow);
			flow.QueueFree();
			MenuSettings.Reset();
			FightScene.Sources = saved;
			FightScene.Run = savedRun;
			FightScene.Opponent = foe;
		}
	}

	static void Press(Button b) => b.EmitSignal(BaseButton.SignalName.Pressed);

	[Test]
	public static void Boot_TitlePractice_OpensThePracticeFight_AndQuitToTitleFreesIt(Node runner) => WithBoot(runner, flow =>
	{
		Assert.True(!flow.Title.PracticeButton.Disabled && flow.Router.Entry(TitleEntry.Practice).Enabled, "Practice is lit on the title");
		flow.Router.ChooseScheme(ControlScheme.Kata);
		Press(flow.Title.PracticeButton);
		Assert.Equal(Screen.Practice, flow.Router.Current, "Practice opens practice");
		var fight = flow.Practice;
		Assert.True(fight is not null && ReferenceEquals(fight, flow.Fight) && fight.InPractice && fight.IsInsideTree(), "the fight scene, in practice mode");
		Assert.True(!flow.Title.Visible && flow.Session is null, "the title hides; no run is started");
		Assert.Equal(ControlScheme.Kata, fight!.Match.P1.Input.Scheme, "Ryo plays the menu's scheme");
		for (int t = 0; t < 120; t++) fight.Step(new FighterInput(InputBits.Right), FighterInput.None);
		Assert.True(fight.RequestTitle(), "the pause menu's Quit to title");
		Assert.True(flow.Router.Current == Screen.Title && flow.Title.Visible, "back on the title");
		Assert.True(flow.Fight is null && flow.Practice is null && flow.NodeScene is null && !flow.GetChildren().OfType<FightScene>().Any(n => !n.IsQueuedForDeletion()), "the practice scene is gone");
		Assert.True(flow.Session is null && flow.Title.ContinueButton.Disabled, "still no run, no Continue");
		Press(flow.Title.PracticeButton);
		Assert.True(flow.Router.Current == Screen.Practice && flow.Practice is { Match.Tick: 0 }, "practice again starts clean");
	});
}
