using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Flow;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>
/// Windowed screenshot tool (not a test) for YOK-96: merchant, dojo and a used-choice rest stop.
/// Run: godot --path game res://tests/rest_stop_capture.tscn --resolution 1920x1080 -- out=&lt;dir&gt;
/// </summary>
public partial class RestStopCapture : Node
{
	public override async void _Ready()
	{
		string dir = ".", suffix = "";
		foreach (string a in OS.GetCmdlineUserArgs()) { if (a.StartsWith("out=")) dir = a[4..]; if (a.StartsWith("suffix=")) suffix = a[7..]; }
		// the real pool (move names as the player sees them), not the fixtures
		await Shot(dir, "rest_stop_merchant" + suffix, "merchant", false);
		await Shot(dir, "rest_stop_dojo" + suffix, "dojo", false);
		await Shot(dir, "rest_stop_used" + suffix, "merchant", true);
		GetTree().Quit();
	}

	private async System.Threading.Tasks.Task Shot(string dir, string name, string type, bool take)
	{
		string md = ContentPaths.Data("map");
		var s = RunSession.Start(FightScene.LoadAbilityPool(), FileMapSource.Default(md).Load(), NodeSceneTable.Default(md), 5);
		for (; ; )
		{
			var next = s.Reachable();
			string pick = next[0];
			foreach (string id in next) if (s.Map.Graph.Node(id).Type == type) pick = id;
			s.Enter(pick);
			if (s.Node!.Type == type) break;
			if (s.Node.Type is "merchant" or "dojo") s.CompleteNode();
			else
			{
				var d = s.WinFight(640);
				if (d != null) { var c = d.Offer.Cards[0]; d.Pick(0, c.Targets.Count > 0 ? c.Targets[0].Slot : null); }
				s.CompleteNode();
			}
		}
		s.Run.SetHealth(640);
		var screen = GD.Load<PackedScene>("res://scenes/ui/rest_stop.tscn").Instantiate<RestStopScreen>();
		screen.BindRun(s, ControlScheme.Kihon);
		AddChild(screen);
		if (take) screen.TakeFree();
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		GetViewport().GetTexture().GetImage().SavePng($"{dir}/{name}.png");
		RemoveChild(screen);
		screen.QueueFree();
	}
}
