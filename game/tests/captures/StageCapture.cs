using System;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-39 windowed screenshot tool (not a test), YOK-129 dynamic camera set: the bamboo grove at close range, mid range,
/// the screen walls, both corners and a jump-in, each after the camera has eased in.
/// Run: godot --path game res://tests/stage_capture.tscn --resolution 1920x1080 -- out=&lt;dir&gt; [--stage=placeholder]
/// </summary>
public partial class StageCapture : Node
{
	private string _dir = ".";

	static FighterInput In(InputBits b) => new(b);

	public override async void _Ready()
	{
		foreach (string a in OS.GetCmdlineUserArgs()) if (a.StartsWith("out=")) _dir = a[4..];
		// Start 600 apart. Close: walk in until the push boxes touch.
		await Shot("close-range", s => Walk(s, InputBits.Right, InputBits.Left, 40));
		// Mid: back off to ~880 apart.
		await Shot("mid-range", s => Walk(s, InputBits.Left, InputBits.Right, 11));
		// Walls: walk apart until the screen walls hold them.
		await Shot("max-separation", s => Walk(s, InputBits.Left, InputBits.Right, 400));
		await Shot("left-corner", s => Corner(s, -1));
		await Shot("right-corner", s => Corner(s, 1));
		// Jump-in: from close-mid range Ryo jumps toward the Kitsune; captured just past the apex.
		await Shot("jump-in", s => { Walk(s, InputBits.Right, InputBits.None, 14); Idle(s, 60); s.Step(In(InputBits.Up | InputBits.Right), FighterInput.None); Walk(s, InputBits.None, InputBits.None, 22); }, settle: false);
		GetTree().Quit();
	}

	static void Walk(FightScene s, InputBits p1, InputBits p2, int ticks) { for (int t = 0; t < ticks; t++) s.Step(In(p1), In(p2)); }
	static void Idle(FightScene s, int ticks) => Walk(s, InputBits.None, InputBits.None, ticks);

	/// <summary>Both walk to that corner (Ryo leads on the left, the Kitsune on the right) until the leader stops.</summary>
	static void Corner(FightScene s, int side)
	{
		var toward = side < 0 ? InputBits.Left : InputBits.Right;
		for (int t = 0, lastX = int.MinValue; t < 1500 && s.Match.Fighters[side < 0 ? 0 : 1].X != lastX; t++)
		{
			lastX = s.Match.Fighters[side < 0 ? 0 : 1].X;
			s.Step(In(toward), In(toward));
		}
		for (int t = 0; t < 30; t++) s.Step(In(toward), In(toward)); // the follower closes in on the cornered leader
	}

	private async System.Threading.Tasks.Task Shot(string name, Action<FightScene> pose, bool settle = true)
	{
		var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		scene.ExternalDrive = true;
		AddChild(scene);
		pose(scene);
		if (settle) Idle(scene, 90); // the eased zoom settles (0.08 per tick: 99.9% in 90 ticks)
		var cam = scene.Cam;
		GD.Print($"{name}: stage {scene.StageKind} P1 {scene.Match.P1.X / 100} y{scene.Match.P1.Y / 100} P2 {scene.Match.P2.X / 100} sep {Math.Abs(scene.Match.P2.X - scene.Match.P1.X) / 100}; camera width {cam.Width:0} x {cam.CenterX:0} h {cam.HeightMeters:0.00} m; in frame {cam.InFrame(DynamicCamera.SubjectOf(scene.Match))}");
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		GetViewport().GetTexture().GetImage().SavePng($"{_dir}/{name}.png");
		scene.QueueFree();
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
	}
}
