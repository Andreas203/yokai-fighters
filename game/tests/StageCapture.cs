using System;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-39 windowed screenshot tool (not a test): the bamboo grove with both fighters at mid-stage and in each corner.
/// Run: godot --path game res://tests/stage_capture.tscn --resolution 1920x1080 -- out=&lt;dir&gt; [--stage=placeholder]
/// </summary>
public partial class StageCapture : Node
{
	private string _dir = ".";

	public override async void _Ready()
	{
		foreach (string a in OS.GetCmdlineUserArgs()) if (a.StartsWith("out=")) _dir = a[4..];
		await Shot("mid-stage", 0);
		await Shot("left-corner", -1);
		await Shot("right-corner", 1);
		GetTree().Quit();
	}

	/// <summary>side 0: walk in to mid-range; -1/+1: both walk to that corner (Ryo leads, the Kitsune follows).</summary>
	private async System.Threading.Tasks.Task Shot(string name, int side)
	{
		var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		scene.ExternalDrive = true;
		AddChild(scene);
		var toward = side < 0 ? InputBits.Left : InputBits.Right;
		if (side == 0)
			for (int t = 0; t < 20; t++) scene.Step(new FighterInput(InputBits.Right), new FighterInput(InputBits.Left));
		else
			for (int t = 0, lastX = int.MinValue; t < 1500 && scene.Match.Fighters[side < 0 ? 0 : 1].X != lastX; t++)
			{
				lastX = scene.Match.Fighters[side < 0 ? 0 : 1].X; // until the leader stops at the stage edge
				scene.Step(new FighterInput(toward), new FighterInput(toward));
			}
		for (int t = 0; t < 20; t++) scene.Step(FighterInput.None, FighterInput.None);
		GD.Print($"{name}: stage {scene.StageKind} camera x {FightCamera.CenterX(scene.Match)} P1 {scene.Match.P1.X} P2 {scene.Match.P2.X}");
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		GetViewport().GetTexture().GetImage().SavePng($"{_dir}/{name}.png");
		scene.QueueFree();
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
	}
}
