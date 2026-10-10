using System;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>YOK-39 start screen: boots first, the sim waits for Start, Restart skips it, text matches InputDevices.</summary>
public static class StartScreenTests
{
	static FightScene NewScene(Node runner, bool? start)
	{
		var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		scene.ExternalDrive = true;
		scene.StartScreenOverride = start;
		runner.AddChild(scene);
		return scene;
	}

	[Test]
	public static void Boots_ToStartScreen_SimWaits(Node runner)
	{
		var s = NewScene(runner, true);
		Assert.Equal(DemoStage.Title, s.Stage, "boot stage");
		Assert.True(s.Title.Visible, "start screen visible");
		int tick = s.Match.Tick;
		for (int i = 0; i < 30; i++) s.Step(FighterInput.None, FighterInput.None);
		Assert.Equal(tick, s.Match.Tick, "no ticks before Start");
		s.Title.OnStart();
		Assert.Equal(DemoStage.Fighting, s.Stage, "Start begins the fight");
		Assert.True(!s.Title.Visible, "screen hidden");
		s.Step(FighterInput.None, FighterInput.None);
		Assert.Equal(tick + 1, s.Match.Tick, "sim ticks after Start");
		s.QueueFree();
	}

	[Test]
	public static void Start_DoesNotLeakHeldInput(Node runner)
	{
		var s = NewScene(runner, true);
		s.Title.OnStart();
		s.ArmSuppressForTest(InputBits.Special); // Space held to press Start
		Assert.Equal(InputBits.None, s.SuppressedInputForTest(InputBits.Special), "Special masked while held");
		Assert.Equal(InputBits.Left, s.SuppressedInputForTest(InputBits.Special | InputBits.Left), "other keys pass");
		Assert.Equal(InputBits.None, s.SuppressedInputForTest(InputBits.None), "released");
		Assert.Equal(InputBits.Special, s.SuppressedInputForTest(InputBits.Special), "a fresh press works after release");
		s.QueueFree();
	}

	[Test]
	public static void Restart_SkipsStartScreen(Node runner)
	{
		var s = NewScene(runner, true);
		s.Title.OnStart();
		s.RestartRun();
		Assert.Equal(DemoStage.Fighting, s.Stage, "restart goes straight to the fight");
		Assert.True(!s.Title.Visible, "start screen stays hidden");
		s.QueueFree();
	}

	[Test]
	public static void TestDrivenScenes_SkipStartScreen(Node runner)
	{
		var s = NewScene(runner, null);
		Assert.Equal(DemoStage.Fighting, s.Stage, "ExternalDrive scenes boot into the fight");
		s.QueueFree();
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
