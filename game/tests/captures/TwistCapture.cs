using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Flow;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>
/// Windowed screenshot tool (not a test) for YOK-139: the map with elder twist icons (row 5 reachable, Nine-Tails focused),
/// then the elder fight's intro card, and the first-meeting callout with the HUD strip.
/// Run: godot --path game res://tests/captures/twist_capture.tscn --resolution 1920x1080 -- out=&lt;dir&gt; [suffix=_720p]
/// </summary>
public partial class TwistCapture : Node
{
	public override async void _Ready()
	{
		string dir = ".", suffix = "";
		foreach (string a in OS.GetCmdlineUserArgs())
		{
			if (a.StartsWith("out=")) dir = a[4..];
			if (a.StartsWith("suffix=")) suffix = a[7..];
		}
		var session = ToRowFive();
		var screen = GD.Load<PackedScene>("res://scenes/ui/map_screen.tscn").Instantiate<MapScreen>();
		AddChild(screen);
		screen.SetView(new SessionMapView(session));
		await Snap(dir + $"/map_twist_icons{suffix}.png");
		screen.QueueFree();

		session.Enter("r5-nine-tails");
		var fight = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		fight.Session = session;
		fight.FirstMeeting = true;
		AddChild(fight);
		await Snap(dir + $"/twist_intro_card{suffix}.png");
		fight.TwistCard!.Continue();
		for (int i = 0; i < 20; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		await Snap(dir + $"/twist_callout_hud{suffix}.png");
		fight.QueueFree();

		// The other two elders: no portrait art yet, so the card leaves the portrait space out (E1 placeholders approved).
		var oni = ToRowFive();
		oni.Enter("r5-elder-oni");
		var oniFight = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		oniFight.Session = oni;
		AddChild(oniFight);
		await Snap(dir + $"/twist_intro_card_oni{suffix}.png");
		oniFight.QueueFree();
		GetTree().Quit();
	}

	private static RunSession ToRowFive()
	{
		string md = ContentPaths.Data("map");
		var session = RunSession.Start(FightScene.LoadAbilityPool(), FileMapSource.Default(md).Load(), NodeSceneTable.Default(md), 5);
		foreach (string id in new[] { "r1-apprentice", "r2-oni", "r3-kappa", "r4-merchant" })
		{
			session.Enter(id);
			if (session.Node!.Type != "merchant")
			{
				var d = session.WinFight(840);
				if (d != null) { var c = d.Offer.Cards[0]; d.Pick(0, c.Targets.Count > 0 ? c.Targets[0].Slot : null); }
			}
			session.CompleteNode();
		}
		return session;
	}

	private async System.Threading.Tasks.Task Snap(string path)
	{
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		GetViewport().GetTexture().GetImage().SavePng(path);
		GD.Print("saved " + path);
	}
}
