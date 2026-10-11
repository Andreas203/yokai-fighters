using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Flow;
using YokaiFighters.Sim;
using YokaiFighters.Ui;
using YokaiFighters.Ui.Kit;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-pause-flow-wiring: the in-fight pause (<c>FightScene.Pause.cs</c>). Pause / resume leaves the sim hash-identical to an
/// unpaused run of the same inputs, held buttons are masked on resume, every menu entry routes, the confirm box guards
/// Restart and Quit, pause is off under ExternalDrive and over the between-duel screens, and the summary view model
/// carries the live run state. Scene tests run on the fixture kits.
/// </summary>
public static class PauseFlowTests
{
	static readonly FighterInput Idle = FighterInput.None;
	static FighterInput Strike(int t) => new(t % 2 == 0 ? InputBits.DebugStrike : InputBits.None);
	static void Press(Button b) => b.EmitSignal(BaseButton.SignalName.Pressed);
	static Button Yes(PauseMenu m) => ((PaperDialog)m.ConfirmBox).ConfirmButton!;
	static Button No(PauseMenu m) => ((PaperDialog)m.ConfirmBox).CancelButton!;

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

	/// <summary>
	/// A viewport of the test's own: the runner runs every test inside one <c>_Ready</c>, so scenes earlier tests freed are
	/// still in the root viewport (a visible leftover sheet would take Esc first). Input, focus and the unhandled-input
	/// order here are those of a tree holding only this scene, as in the game.
	/// </summary>
	static SubViewport NewStage(Node runner)
	{
		var vp = new SubViewport { Size = new Vector2I(1920, 1080), Name = "PauseTestStage" };
		runner.AddChild(vp);
		return vp;
	}
	static void FreeStage(Node runner, SubViewport vp) { runner.RemoveChild(vp); vp.QueueFree(); }

	/// <summary>A fight on the fixture kits in its own viewport, removed from the tree afterwards.</summary>
	static void WithScene(Node runner, Action<FightScene> body, bool external = true, bool? pause = true, RunState? run = null)
	{
		var saved = FightScene.Sources;
		var savedRun = FightScene.Run;
		FightScene.Sources = FightScene.FixtureSources;
		FightScene.Run = run;
		MenuSettings.Reset();
		var stage = NewStage(runner);
		try
		{
			var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
			scene.ExternalDrive = external;
			scene.PauseEnabled = pause;
			scene.FixedRunSeed = 48;
			scene.HeldBitsSource = () => InputBits.None; // never the machine's real keyboard
			stage.AddChild(scene);
			body(scene);
		}
		finally
		{
			FreeStage(runner, stage);
			MenuSettings.Reset();
			FightScene.Sources = saved;
			FightScene.Run = savedRun;
		}
	}

	static FighterInput Random(SimRng rng) => new((InputBits)(rng.NextUInt() & (uint)(InputBits.Directions | InputBits.Attacks | InputBits.Special)));

	static string Pose(FightScene s) => string.Join(" | ", Enumerable.Range(0, 2).Select(i =>
	{
		var player = s.GetNode("Fighters").GetChild(i).FindChild("Animator", true, false) as AnimationPlayer;
		return $"{s.PresenterOf(i)?.Sample} @{player?.CurrentAnimation}:{player?.CurrentAnimationPosition}";
	}));

	// ---- freeze and resume ----

	/// <summary>F3: pausing three times mid-fight (with paused frames and refused steps in between) changes no tick's hash.</summary>
	[Test]
	public static void PauseResume_HashesEqualAnUnpausedRun(Node runner)
	{
		const int Ticks = 600;
		int[] pauseAt = { 60, 200, 333 };
		List<ulong> Play(FightScene s, bool withPauses)
		{
			var rng = new SimRng(7331);
			var hashes = new List<ulong>();
			for (int i = 0; i < Ticks; i++)
			{
				if (withPauses && pauseAt.Contains(i))
				{
					int tick = s.Match.Tick, world = s.Match.WorldFrame;
					ulong hash = s.Match.StateHash();
					string pose = Pose(s);
					Assert.True(s.PresenterOf(0) != null && !pose.Contains("@:"), "the pose check reads real presenters and Animators: " + pose);
					Assert.True(s.TryPause(), $"pause opens at step {i}");
					Assert.True(s.Paused && s.FlowPaused && s.PauseMenu!.IsOpen, "paused, menu up");
					for (int f = 0; f < 90; f++)
					{
						s._Process(1.0 / 60);
						s.Step(new FighterInput(InputBits.HeavyPunch), new FighterInput(InputBits.HeavyKick)); // refused while paused
					}
					Assert.Equal(tick, s.Match.Tick, "no tick while paused");
					Assert.Equal(world, s.Match.WorldFrame, "no world frame while paused");
					Assert.Equal(hash, s.Match.StateHash(), "state untouched while paused");
					Assert.Equal(pose, Pose(s), "animation holds its frame while paused");
					s.PauseMenu.Resume();
					Assert.True(!s.Paused && !s.FlowPaused, "resumed");
					Assert.Equal(pose, Pose(s), "no animation jump on resume");
					Assert.Equal(hash, s.Match.StateHash(), "resume changes nothing by itself");
				}
				s.Step(Random(rng), Random(rng));
				hashes.Add(s.Match.StateHash());
			}
			Assert.Equal(MatchPhase.Fighting, s.Match.Phase, "the stream does not end the duel (so every pause point is mid-fight)");
			Assert.Equal(Ticks, s.Recorder!.Finish(s.Match).Steps, "the recording has one step per tick: paused frames are not recorded");
			return hashes;
		}
		List<ulong> plain = null!, paused = null!;
		WithScene(runner, s => plain = Play(s, false));
		WithScene(runner, s => paused = Play(s, true));
		int diff = Enumerable.Range(0, Ticks).FirstOrDefault(i => plain[i] != paused[i], -1);
		Assert.Equal(-1, diff, "first step whose hash differs between the paused and the unpaused run");
	}

	[Test]
	public static void LiveClock_EscAndStartOpenAndClose_NoTicksWhilePaused(Node runner) => WithScene(runner, s =>
	{
		Assert.True(s.CanPause, "a fight on the live clock can pause");
		Tap(s, Key.Escape);
		Assert.True(s.Paused, "Esc opens the pause menu");
		Assert.True(s.GetViewport().GuiGetFocusOwner() == s.PauseMenu!.ResumeButton, "Resume has focus");
		int tick = s.Match.Tick;
		ulong hash = s.Match.StateHash();
		for (int i = 0; i < 20; i++) { OS.DelayMsec(5); s._Process(0.005); }
		Assert.Equal(tick, s.Match.Tick, "100 ms of real time paused: no ticks");
		Assert.Equal(hash, s.Match.StateHash(), "state untouched");
		Tap(s, Key.Escape);
		Assert.True(!s.Paused, "Esc resumes");
		s._Process(0.001);
		Assert.True(s.Match.Tick - tick <= 1, $"no catch-up burst after the pause (ran {s.Match.Tick - tick})");
		PadTap(s, JoyButton.Start);
		Assert.True(s.Paused, "pad Start opens it");
		PadTap(s, JoyButton.Start);
		Assert.True(!s.Paused, "pad Start resumes");
	}, external: false, pause: null);

	// ---- held inputs ----

	[Test]
	public static void Resume_MasksButtonsNotHeldThroughThePause(Node runner) => WithScene(runner, s =>
	{
		const InputBits LP = InputBits.LightPunch, HP = InputBits.HeavyPunch, Sp = InputBits.Special, L = InputBits.Left, D = InputBits.Down;
		InputBits held = LP | HP | L;
		s.HeldBitsSource = () => held;
		s.Step(new FighterInput(held), Idle); // the sim's last tick saw LP + HP + back
		Assert.True(s.TryPause(), "pause");
		s._Process(0.016);
		held = LP | L; s._Process(0.016); // HP let go during the pause
		held = LP | HP | L | D | Sp; s._Process(0.016); // HP pressed again, Down from the menu, Space (= Special) on Resume
		s.PauseMenu!.Resume();
		Assert.Equal(LP | L | D, s.SuppressedInputForTest(held), "held right through (LP) and directions pass; re-pressed HP and the Resume key do not fire");
		held = LP | HP | L;
		Assert.Equal(LP | L, s.SuppressedInputForTest(held), "HP stays masked while it is still down");
		held = LP | L;
		Assert.Equal(LP | L, s.SuppressedInputForTest(held), "released");
		held = LP | HP | L | Sp;
		Assert.Equal(held, s.SuppressedInputForTest(held), "a fresh press after the release counts");

		Assert.Equal(InputBits.None, FightScene.ResumeMask(InputBits.None, InputBits.Directions), "directions are never masked");
		Assert.Equal(FightScene.PauseMaskable, FightScene.ResumeMask(InputBits.None, (InputBits)0xffff) , "every attack button and Special can be");
		Assert.Equal(InputBits.None, FightScene.ResumeMask(FightScene.PauseMaskable, FightScene.PauseMaskable), "nothing held throughout is");
	});

	[Test]
	public static void RestartFromPause_MasksEverythingHeld(Node runner) => WithScene(runner, s =>
	{
		InputBits held = InputBits.Special | InputBits.Left;
		s.HeldBitsSource = () => held;
		Assert.True(s.TryPause(), "pause");
		Press(s.PauseMenu!.RestartButton);
		Press(Yes(s.PauseMenu));
		Assert.Equal(InputBits.None, s.SuppressedInputForTest(held), "as on the end screens: nothing held at Restart reaches the new fight");
	});

	// ---- menu entries ----

	[Test]
	public static void Entries_Route_AndConfirmGuardsRestart(Node runner) => WithScene(runner, s =>
	{
		var m = s.PauseMenu!;
		for (int t = 0; t < 40; t++) s.Step(Idle, Strike(t)); // Ryo takes some damage first
		int tick = s.Match.Tick;
		var run = s.DemoRun;
		int titles = 0;
		s.TitleRequested += () => titles++;
		Assert.True(s.TryPause(), "pause");
		Assert.True(!s.TryPause(), "already paused");

		Press(m.MoveListButton);
		Assert.True(m.MoveList.Visible && s.Paused, "Move list opens its panel over the pause");
		Assert.Equal(4, m.MoveList.View!.Slots.Count, "bound to the run's four slots");
		Tap(s, Key.Escape);
		Assert.True(!m.MoveList.Visible && s.Paused, "back from the move list returns to the pause menu, still paused");
		Assert.True(s.GetViewport().GuiGetFocusOwner() == m.MoveListButton, "focus back on Move list");

		Press(m.SettingsButton);
		Assert.True(m.Settings.Visible && s.Paused, "Settings opens its panel over the pause");
		Tap(s, Key.Escape);
		Assert.True(!m.Settings.Visible && s.Paused, "back from settings returns to the pause menu, still paused");

		Press(m.RestartButton);
		Assert.True(m.ConfirmBox.Visible && s.Paused, "Restart run asks first");
		Assert.Equal("Restart run?|Restart|Cancel", $"{m.ConfirmBox.GetNode<Label>("Center/Frame/Sheet/Box/Title").Text}|{Yes(m).Text}|{No(m).Text}", "confirm dialog text");
		Press(No(m));
		Assert.True(!m.ConfirmBox.Visible && s.Paused && ReferenceEquals(run, s.DemoRun) && s.Match.Tick == tick, "Cancel: same run, same tick, still paused");
		Press(m.RestartButton);
		Tap(s, Key.Escape);
		Assert.True(!m.ConfirmBox.Visible && s.Paused && ReferenceEquals(run, s.DemoRun), "Esc cancels the confirm box, not the pause");

		Press(m.ResumeButton);
		Assert.True(!s.Paused && !m.Visible, "Resume closes the menu");
		s.Step(Idle, Idle);
		Assert.Equal(tick + 1, s.Match.Tick, "and the fight goes on from the next tick");

		Assert.True(s.TryPause(), "pause again");
		Press(m.RestartButton);
		Press(Yes(m));
		Assert.True(!s.Paused && !ReferenceEquals(run, s.DemoRun), "Restart accepted: a new run");
		Assert.True(s.Duel == 1 && s.Match.Tick == 0 && s.Match.P1.Health == RunState.MaxHealth && s.Stage == DemoStage.Fighting, "first duel, tick 0, full health");
		Assert.Equal(0, s.Recorder!.Finish(s.Match).Steps, "with a fresh recording");

		Assert.True(s.TryPause(), "pause in the new run");
		Press(m.QuitButton);
		Assert.True(m.ConfirmBox.Visible && titles == 0, "Quit to title asks first");
		Press(No(m));
		Assert.True(titles == 0 && s.Paused, "Cancel keeps the fight");
		Press(m.QuitButton);
		Press(Yes(m));
		Assert.True(titles == 1 && !s.Paused, "Quit accepted: FightScene.RequestTitle raised once");
	});

	[Test]
	public static void QuitToTitle_WithNoBootFlow_Resumes(Node runner) => WithScene(runner, s =>
	{
		Assert.True(s.TryPause(), "pause");
		Press(s.PauseMenu!.QuitButton);
		Press(Yes(s.PauseMenu));
		Assert.True(!s.Paused, "nothing to quit to: the fight resumes");
		int tick = s.Match.Tick;
		s.Step(Idle, Idle);
		Assert.Equal(tick + 1, s.Match.Tick, "and steps");
	});

	[Test]
	public static void QuitToTitle_ThroughTheBootFlow_FreesTheFight(Node runner)
	{
		var saved = FightScene.Sources;
		var savedRun = FightScene.Run;
		FightScene.Sources = FightScene.FixtureSources;
		FightScene.Run = null;
		MenuSettings.Reset();
		var flow = GD.Load<PackedScene>("res://scenes/boot.tscn").Instantiate<GameFlow>();
		flow.QuitAction = () => { };
		flow.DirectFight = ControlScheme.Kihon;
		flow.ConfigureFight = f => { f.ExternalDrive = true; f.PauseEnabled = true; f.HeldBitsSource = () => InputBits.None; };
		var stage = NewStage(runner);
		try
		{
			stage.AddChild(flow);
			var fight = flow.Fight!;
			Tap(fight, Key.Escape);
			Assert.True(fight.Paused, "Esc pauses the fight under the boot scene");
			Assert.True(!flow.Title.Visible && flow.Router.Current == Screen.Fight, "still the fight screen");
			Press(fight.PauseMenu!.QuitButton);
			Press(Yes(fight.PauseMenu));
			Assert.Equal(Screen.Title, flow.Router.Current, "back on the title");
			Assert.True(flow.Fight == null && flow.Title.Visible, "fight freed, title shown");
			Assert.True(!flow.GetTree().Paused, "tree not paused");
		}
		finally
		{
			FreeStage(runner, stage);
			MenuSettings.Reset();
			FightScene.Sources = saved;
			FightScene.Run = savedRun;
		}
	}

	// ---- where pause is not available ----

	[Test]
	public static void ExternalDrive_HasNoPause_UnlessAsked(Node runner) => WithScene(runner, s =>
	{
		Assert.True(s.ExternalDrive && s.PauseMenu != null, "an externally driven fight still has the menu node");
		Assert.True(!s.CanPause && !s.TryPause(), "but cannot pause");
		Tap(s, Key.Escape);
		PadTap(s, JoyButton.Start);
		Assert.True(!s.Paused && !s.PauseMenu!.Visible, "Esc and Start do nothing");
		int tick = s.Match.Tick;
		s.Step(Idle, Idle);
		Assert.Equal(tick + 1, s.Match.Tick, "the harness keeps stepping");
		s.PauseEnabled = true;
		Assert.True(s.TryPause(), "a test that asks for it gets it");
		s.PauseMenu!.Resume();
		s.PauseEnabled = false;
	}, pause: null);

	[Test]
	public static void NoPause_OverTheScreensBetweenDuels(Node runner)
	{
		WithScene(runner, s =>
		{
			for (int t = 0; t < 600 && s.Match.Phase != MatchPhase.Over; t++) s.Step(Strike(t), Idle);
			Assert.Equal(DemoStage.Binding, s.Stage, "won: binding card");
			Tap(s, Key.Escape); // the story card's own skip key
			Assert.True(!s.Paused && !s.TryPause(), "no pause over the binding card or the reward screen");
			Assert.Equal(DemoStage.Reward, s.Stage, "Esc went to the card (skip), not to a pause");
			Assert.True(!s.TryPause(), "no pause over the reward screen");
		});
		WithScene(runner, s =>
		{
			for (int t = 0; t < 600 && s.Match.Phase != MatchPhase.Over; t++) s.Step(Idle, Strike(t));
			Assert.Equal(DemoStage.Lost, s.Stage, "lost");
			Assert.True(!s.TryPause(), "no pause over the lose screen");
			s.RestartRun();
			Assert.True(s.CanPause, "available again in the new fight");
			if (s.DebugMenu != null)
			{
				s.ToggleDebugMenu();
				Assert.True(!s.TryPause(), "no pause under the F9 debug menu");
				s.ToggleDebugMenu();
			}
			Assert.True(s.TryPause(), "pause");
			s.RestartRun(); // e.g. the debug menu's restart
			Assert.True(!s.Paused, "a restart from anywhere closes the pause menu");
		});
		WithScene(runner, s =>
		{
			s.DemoRun.SetHealth(RunState.MaxHealth);
			for (int duel = 1; duel <= FightScene.DemoDuels; duel++)
			{
				for (int t = 0; t < 600 && s.Match.Phase != MatchPhase.Over; t++) s.Step(Strike(t), Idle);
				if (duel == FightScene.DemoDuels) break;
				s.BindingCard.Continue();
				s.Rewards.Confirm();
				if (s.Rewards.Step == RewardStep.Target) s.Rewards.Confirm();
				Assert.Equal(DemoStage.Fighting, s.Stage, "rematch");
			}
			Assert.Equal(DemoStage.Complete, s.Stage, "demo complete");
			Assert.True(!s.TryPause(), "no pause over the demo-complete screen");
		});
	}

	// ---- summary ----

	static string[] SlotLines(PauseMenu m) => UiFind.Get<VBoxContainer>(m, "Slots").GetChildren()
		.Where(c => !c.IsQueuedForDeletion()).Select(c => UiFind.Get<Label>(c, "MoveName").Text + (UiFind.Get<Label>(c, "Modifier").Visible ? "\n" + UiFind.Get<Label>(c, "Modifier").Text : "")).ToArray();

	[Test]
	public static void Summary_CarriesTheLiveRunState(Node runner)
	{
		var saved = FightScene.Sources;
		FightScene.Sources = FightScene.FixtureSources;
		AbilityPool pool;
		try { pool = FightScene.LoadAbilityPool(); } finally { FightScene.Sources = saved; }
		var run = RunState.NewRun(pool);
		run.SetHealth(720);
		var drafted = pool.Specials.First(x => !x.Starter && run[x.Slot] == null);
		run.Equip(drafted);
		run.LevelUp(SpecialSlot.A);
		var mod = pool.Modifiers.First(x => run.CanAttach(SpecialSlot.A, x));
		run.Attach(SpecialSlot.A, mod);
		var a = run[SpecialSlot.A]!;
		var b = run[SpecialSlot.B]!;

		WithScene(runner, s =>
		{
			for (int t = 0; t < 6; t++) s.Step(Idle, Strike(t)); // Kitsune hits Ryo
			for (int t = 0; t < 30; t++) s.Step(Idle, Idle);
			for (int t = 0; t < 4; t++) s.Step(Strike(t), Idle); // Ryo hits back
			int p1 = s.Match.P1.Health, p2 = s.Match.P2.Health, max2 = s.Match.P2.MaxHealth;
			Assert.True(p1 < 720 && p2 < max2, $"both took damage ({p1}, {p2})");

			var v = s.BuildPauseSummary();
			Assert.Equal(new PauseFighterSummary("RYO", p1, 1000), v.Player, "Ryo's health in this duel (not the run's carried 720)");
			Assert.Equal(new PauseFighterSummary("Kitsune", p2, max2), v.Opponent, "the opponent's health");
			Assert.Equal("Duel 1 of 2 · vs Kitsune", v.Position, "position in the run");
			Assert.Equal("A|B|C|D", string.Join("|", v.Slots.Select(x => x.Slot)), "four slots in order");
			Assert.Equal(new PauseSlotSummary("A", a.Data.Name, 2, mod.Name), v.Slots[0], "slot A: level 2 with its modifier");
			Assert.Equal(new PauseSlotSummary("B", b.Data.Name, 1, ""), v.Slots[1], "slot B: level 1, no modifier");
			Assert.Equal(new PauseSlotSummary(drafted.Slot.ToString(), drafted.Name, 1, ""), v.Slots[drafted.Slot - SpecialSlot.A], "the drafted special in its own slot");
			Assert.Equal(1, v.Slots.Count(x => x == new PauseSlotSummary(x.Slot, "Empty", 0, "")), "one slot still empty");

			// The page shows exactly those values, set when the pause opens.
			Assert.True(s.TryPause(), "pause");
			var m = s.PauseMenu!;
			Assert.Equal($"{p1} / 1000", UiFind.Get<Label>(m, "RunHealth").Text, "Ryo's line");
			Assert.Equal((double)p1, UiFind.Get<ProgressBar>(m, "RunBar").Value, "Ryo's bar");
			Assert.Equal("RYO", UiFind.Get<Label>(m, "RunName").Text, "separate player heading");
			Assert.Equal("Duel 1 of 2 · vs Kitsune", UiFind.Get<Label>(m, "RunSub").Text, "position line");
			string[] lines = SlotLines(m);
			Assert.Equal(4, lines.Length, "four slot strips");
			Assert.Equal($"{a.Data.Name} · Lv 2\n{mod.Name}", lines[0], "slot A strip");
			Assert.Equal($"{b.Data.Name} · Lv 1", lines[1], "slot B strip");
			Assert.Equal(1, lines.Count(x => x.EndsWith("Empty", StringComparison.Ordinal)), "one empty strip");
			m.Resume();

			// Live: more damage shows the next time the pause opens.
			for (int t = 0; t < 6; t++) s.Step(Idle, Strike(t));
			int later = s.Match.P1.Health;
			Assert.True(later < p1, "Ryo is hit again");
			Assert.True(s.TryPause(), "pause again");
			Assert.Equal($"{later} / 1000", UiFind.Get<Label>(m, "RunHealth").Text, "refilled on every open");
			Assert.Equal(4, SlotLines(m).Length, "still four strips (the old ones are gone)");
			m.Resume();
		}, run: run);
	}

	[Test]
	public static void Summary_SecondDuel_IsDuel2Of2(Node runner) => WithScene(runner, s =>
	{
		for (int t = 0; t < 600 && s.Match.Phase != MatchPhase.Over; t++) s.Step(Strike(t), Idle);
		s.BindingCard.Continue();
		s.Rewards.Confirm();
		if (s.Rewards.Step == RewardStep.Target) s.Rewards.Confirm();
		Assert.True(s.Stage == DemoStage.Fighting && s.Duel == 2, "in the rematch");
		Assert.True(s.TryPause(), "pause in the rematch");
		Assert.Equal("Duel 2 of 2 · vs Kitsune", UiFind.Get<Label>(s.PauseMenu!, "RunSub").Text, "position");
		Assert.Equal($"{s.DemoRun.Health} / 1000", UiFind.Get<Label>(s.PauseMenu!, "RunHealth").Text, "carried health at tick 0");
		s.PauseMenu!.Resume();
	});
}
