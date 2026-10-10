using System;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-39 start screen, now the control-select step of the boot flow (see BootFlowTests for the routing): the fight
/// scene no longer holds it and always starts in the fight; held buttons are masked on entry; the scheme choice
/// redraws the controls; text matches InputDevices.
/// </summary>
public static class StartScreenTests
{
	static FightScene NewScene(Node runner)
	{
		var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		scene.ExternalDrive = true;
		runner.AddChild(scene);
		return scene;
	}

	[Test]
	public static void FightScene_HasNoStartScreen_AndTicksAtOnce(Node runner)
	{
		var s = NewScene(runner);
		Assert.Equal(DemoStage.Fighting, s.Stage, "the fight scene starts in the fight");
		Assert.True(s.GetNodeOrNull("Flow/Title") == null, "no start screen inside the fight scene");
		int tick = s.Match.Tick;
		s.Step(FighterInput.None, FighterInput.None);
		Assert.Equal(tick + 1, s.Match.Tick, "the first Step ticks");
		s.QueueFree();
	}

	[Test]
	public static void Entry_DoesNotLeakHeldInput(Node runner)
	{
		var s = NewScene(runner);
		s.ArmSuppressForTest(InputBits.Special); // Space held to press Start Game
		Assert.Equal(InputBits.None, s.SuppressedInputForTest(InputBits.Special), "Special masked while held");
		Assert.Equal(InputBits.Left, s.SuppressedInputForTest(InputBits.Special | InputBits.Left), "other keys pass");
		Assert.Equal(InputBits.None, s.SuppressedInputForTest(InputBits.None), "released");
		Assert.Equal(InputBits.Special, s.SuppressedInputForTest(InputBits.Special), "a fresh press works after release");
		s.QueueFree();
	}

	[Test]
	public static void Restart_StaysInTheFight(Node runner)
	{
		var s = NewScene(runner);
		s.RestartRun();
		Assert.Equal(DemoStage.Fighting, s.Stage, "restart goes straight to the fight");
		Assert.Equal(1, s.Duel, "first duel");
		s.QueueFree();
	}

	[Test]
	public static void ControlSelect_ChoiceRedrawsControls_AndStartsOnce(Node runner)
	{
		YokaiFighters.Ui.MenuSettings.Reset();
		var screen = GD.Load<PackedScene>("res://scenes/ui/start_screen.tscn").Instantiate<StartScreen>();
		runner.AddChild(screen);
		try
		{
			var grid = YokaiFighters.Ui.UiFind.Get<GridContainer>(screen, "KeyboardGrid");
			string Table() => string.Join("|", grid.GetChildren().OfType<Label>().Where(l => l.Visible).Select(l => l.Text));
			Assert.Equal(ControlScheme.Kihon, StartScreen.Scheme, "Kihon by default (E18)");
			Assert.True(screen.KihonButton.Text.StartsWith("●") && screen.KataButton.Text.StartsWith("○"), "Kihon marked");
			Assert.True(Table().Contains("Space + direction"), "Kihon table: Special + direction (K2)");
			screen.KataButton.EmitSignal(BaseButton.SignalName.Pressed);
			Assert.Equal(ControlScheme.Kata, StartScreen.Scheme, "Kata chosen");
			Assert.True(screen.KataButton.Text.StartsWith("●") && screen.KihonButton.Text.StartsWith("○"), "Kata marked");
			Assert.True(Table().Contains("draw the motion") && !Table().Contains("Space + direction"), "Kata table: motions (K1)");
			screen.KihonButton.EmitSignal(BaseButton.SignalName.Pressed);
			Assert.Equal(ControlScheme.Kihon, StartScreen.Scheme, "back to Kihon");
			int started = 0, back = 0;
			screen.Started += () => started++;
			screen.BackRequested += () => back++;
			screen.OnStart();
			screen.OnStart();
			Assert.Equal(1, started, "Start raises once per opening");
			Assert.True(!screen.Visible, "hidden after Start");
		}
		finally { YokaiFighters.Ui.MenuSettings.Reset(); screen.QueueFree(); }
	}

	[Test]
	public static void ControlsText_MatchesInputDevices()
	{
		var k = InputDevices.P1Keys;
		string kb = string.Join("\n", StartScreen.Rows().Select(r => r.Keyboard));
		foreach (Key key in new[] { k.Left, k.Right, k.Up, k.Down, k.LP, k.MP, k.HP, k.LK, k.MK, k.HK, k.Special })
			Assert.True(kb.Contains(OS.GetKeycodeString(key)), $"keyboard text lists {key}");
		var map = new (JoyButton Btn, InputBits Bit, string Label)[]
		{
			(JoyButton.X, InputBits.LightPunch, "X"), (JoyButton.Y, InputBits.MediumPunch, "Y"), (JoyButton.RightShoulder, InputBits.HeavyPunch, "RB"),
			(JoyButton.A, InputBits.LightKick, "A"), (JoyButton.B, InputBits.MediumKick, "B"), (JoyButton.LeftShoulder, InputBits.Special, "LB"),
		};
		foreach (var m in map)
		{
			Assert.Equal(m.Bit, InputDevices.FromPad(b => b == m.Btn, 0, 0, 0), $"pad {m.Label} bit");
			Assert.Equal(m.Label, StartScreen.PadLabel(m.Bit), "pad label");
		}
		Assert.Equal(InputBits.HeavyKick, InputDevices.FromPad(_ => false, 0, 0, 1f), "RT = heavy kick");
		Assert.Equal("RT", StartScreen.PadLabel(InputBits.HeavyKick), "RT label");
		Assert.True(StartScreen.Rows().Any(r => r.Action.StartsWith("Throw") && r.Keyboard == "U + J"), "throw U + J");
		Assert.True(StartScreen.Rows().Any(r => r.Action.StartsWith("Burst") && r.Keyboard == "U + I + O"), "burst U + I + O");
	}
}
