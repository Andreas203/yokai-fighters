using System;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-53 windowed screenshot tool (not a test): both rigged fighters mid-move with the YOK-22 hitbox overlay on.
/// Run: godot --path game res://tests/model_capture.tscn --resolution 1920x1080 -- out=&lt;dir&gt;
/// </summary>
public partial class ModelCapture : Node
{
	private string _dir = ".";
	private const int WalkTicks = 14;

	public override async void _Ready()
	{
		foreach (string a in OS.GetCmdlineUserArgs()) if (a.StartsWith("out=")) _dir = a[4..];
		await Shot("ryo-heavy-kick-active", "ryo-heavy-kick", null, 0, 12, boxes: true);
		await Shot("kitsune-heavy-kick-active", null, "kitsune-heavy-kick", 1, 12, boxes: true);
		await Shot("ryo-jab-active", "ryo-light-punch", null, 0, 5, boxes: true);
		await Shot("kitsune-medium-punch-active", null, "kitsune-medium-punch", 1, 7, boxes: true);
		await Shot("ryo-medium-kick-models", "ryo-medium-kick", null, 0, 9, boxes: false);
		await Shot("idle", null, null, 0, 0, boxes: false);
		await Shot("kitsune-tails-closeup", null, null, 0, 0, boxes: false, closeup: 1);
		await Shot("kitsune-tails-back", null, null, 0, 0, boxes: false, closeup: 1, swap: true);
		await Custom("ryo-jump-apex", sc => { for (int t = 0; t < 60 && sc.Match.P1.AirFrame < 18; t++) sc.Step(new FighterInput(InputBits.Up), FighterInput.None); });
		await Custom("kitsune-crouch", sc => { for (int t = 0; t < 20; t++) sc.Step(FighterInput.None, new FighterInput(InputBits.Down)); });
		await Custom("kitsune-knockdown-mid", sc => Throw(sc, 20));
		await Custom("kitsune-getup-join", sc => Throw(sc, 15));
		await Custom("kitsune-getup-late", sc => Throw(sc, 6));
		GetTree().Quit();
	}

	/// <summary>Ryo throws the Kitsune, then steps until her knockdown has <paramref name="stunLeft"/> frames left.</summary>
	private static void Throw(FightScene sc, int stunLeft)
	{
		int thr = Array.FindIndex(sc.Match.P1.Moves, m => m.IsThrow);
		for (int t = 0; t < 200 && sc.Match.P2.X - sc.Match.P1.X > 9000; t++) sc.Step(new FighterInput(InputBits.Right), FighterInput.None);
		sc.Step(FighterInput.Attack(thr), FighterInput.None);
		for (int t = 0; t < 200 && !(sc.Match.P2.State == FighterState.Knockdown && sc.Match.P2.StunLeft <= stunLeft); t++) sc.Step(FighterInput.None, FighterInput.None);
	}

	private async System.Threading.Tasks.Task Custom(string name, Action<FightScene> drive)
	{
		var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		scene.ExternalDrive = true;
		AddChild(scene);
		for (int t = 0; t < WalkTicks; t++) scene.Step(new FighterInput(InputBits.Right), new FighterInput(InputBits.Left));
		drive(scene);
		GD.Print($"{name}: P1 {scene.Match.P1.State} pose {scene.PresenterOf(0)?.Sample}; P2 {scene.Match.P2.State} stun {scene.Match.P2.StunLeft} pose {scene.PresenterOf(1)?.Sample}");
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		GetViewport().GetTexture().GetImage().SavePng($"{_dir}/{name}.png");
		scene.QueueFree();
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
	}

	private async System.Threading.Tasks.Task Shot(string name, string? p1Move, string? p2Move, int who, int frame, bool boxes, int closeup = -1, bool swap = false)
	{
		var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		scene.ExternalDrive = true;
		AddChild(scene);
		var overlay = scene.GetNode<DebugOverlay>("DebugOverlay");
		if (boxes) overlay.HandleKey(DebugOverlay.ToggleKey);
		int a = p1Move is null ? -1 : Array.FindIndex(scene.Match.P1.Moves, m => m.Id == p1Move);
		int b = p2Move is null ? -1 : Array.FindIndex(scene.Match.P2.Moves, m => m.Id == p2Move);
		for (int t = 0; t < WalkTicks; t++) scene.Step(new FighterInput(InputBits.Right), new FighterInput(InputBits.Left));
		for (int t = 0; t < 30; t++) scene.Step(FighterInput.None, FighterInput.None);
		if (a >= 0 || b >= 0)
		{
			scene.Step(a >= 0 ? FighterInput.Attack(a) : FighterInput.None, b >= 0 ? FighterInput.Attack(b) : FighterInput.None);
			for (int t = 0; t < 40 && scene.Match.Fighters[who].MoveFrame < frame; t++) scene.Step(FighterInput.None, FighterInput.None);
		}
		if (closeup >= 0)
		{
			var cam = scene.GetNode<Camera3D>("Camera");
			var who3 = scene.ModelOf(closeup)!;
			cam.Position = who3.Position + (swap ? new Vector3(2.4f, 1.3f, -1.2f) : new Vector3(0.6f, 1.1f, 2.6f)); // swap: from behind her back
			cam.LookAt(who3.Position + new Vector3(0f, 0.9f, 0f));
		}
		var f1 = scene.Match.P1; var f2 = scene.Match.P2;
		GD.Print($"{name}: P1 {f1.State} {f1.CurrentMove?.Id} {f1.MoveFrame} pose {scene.PresenterOf(0)?.Sample}; P2 {f2.State} {f2.CurrentMove?.Id} {f2.MoveFrame} pose {scene.PresenterOf(1)?.Sample}");
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		string path = $"{_dir}/{name}.png";
		GetViewport().GetTexture().GetImage().SavePng(path);
		GD.Print("saved " + path);
		scene.QueueFree();
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
	}
}
