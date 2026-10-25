using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// Windowed screenshot tool (not a test), YOK-74: the HUD in a yokai, an elder and the Tanuki fight (plus the Tanuki fight 10 s in, strip gone, YOK-121) (AM6: the talisman shows only in the last).
/// Run: godot --path game res://tests/captures/copy_hud_capture.tscn --resolution 1920x1080 -- out=&lt;dir&gt; tag=1080
/// </summary>
public partial class CopyHudCapture : Node
{
	private sealed class View : IHudRunView
	{
		public bool CopyLocked { get; set; }
		public CopyTarget? Copy { get; set; }
		public bool InTanukiFight { get; set; }
		public bool ElderFight { get; set; }
	}

	public override async void _Ready()
	{
		string dir = ".", tag = "1080";
		foreach (string a in OS.GetCmdlineUserArgs())
		{
			if (a.StartsWith("out=")) dir = a[4..];
			if (a.StartsWith("tag=")) tag = a[4..];
		}
		var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		scene.ExternalDrive = true;
		AddChild(scene);
		var hud = scene.GetNode<FightHud>("Hud");
		var run = RunState.NewRun(AbilityPool.LoadRepo(System.IO.Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "..")));
		var view = new View();
		hud.RunView = view;
		void Tick() { scene.Step(FighterInput.None, FighterInput.None); }
		Tick();
		await Snap($"{dir}/hud_copy_yokai_hidden_{tag}.png"); // AM6: a yokai fight shows no copy target
		view.Copy = CopyRule.Target(run); view.ElderFight = true; Tick();
		await Snap($"{dir}/hud_copy_elder_hidden_{tag}.png"); // AM6: nor does an elder fight (twist strip only)
		view.CopyLocked = true; view.ElderFight = false; view.InTanukiFight = true; Tick(); await Wait(0.3);
		await Snap($"{dir}/hud_copy_tanuki_{tag}.png"); // AM6, T1: the Tanuki fight shows the locked copy
		for (int i = 0; i < FightHud.TwistStripTicks; i++) Tick();
		await Snap($"{dir}/hud_copy_tanuki_strip_gone_{tag}.png"); // YOK-121: after 10 s the bottom strip is gone, the top talisman stays
		GetTree().Quit();
	}

	private async System.Threading.Tasks.Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

	private async System.Threading.Tasks.Task Snap(string path)
	{
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		GetViewport().GetTexture().GetImage().SavePng(path);
		GD.Print("saved " + path);
	}
}
