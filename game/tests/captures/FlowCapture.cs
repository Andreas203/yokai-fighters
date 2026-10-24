using System;
using System.Threading.Tasks;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Flow;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// Windowed walk-through tool (not a test) for YOK-77: the real boot scene on the real clock, driven with input
/// events the way a player would (confirm on the title, control select, the map, the binding card and the reward
/// screen), with a screenshot at each step. The one scripted part is Ryo's hands in the fight: P1's input source
/// is a debug-strike tapper so the duel ends in a second; the Kitsune's AI, the fixed-step clock, the HUD and the
/// screens are the game's own.
/// Run: godot --path game res://tests/flow_capture.tscn --resolution 1920x1080 -- out=&lt;dir&gt;
/// </summary>
public partial class FlowCapture : Node
{
	private string _dir = ".";
	private GameFlow _flow = null!;

	public override async void _Ready()
	{
		foreach (string a in OS.GetCmdlineUserArgs())
			if (a.StartsWith("out=")) _dir = a[4..];
		_flow = GD.Load<PackedScene>("res://scenes/boot.tscn").Instantiate<GameFlow>();
		_flow.SettingsFile = System.IO.Path.Combine(_dir, "flow-capture-settings.json"); // never the player's file
		_flow.ConfigureFight = f => f.HeldBitsSource = () => Engine.GetProcessFrames() / 2 % 2 == 0 ? InputBits.DebugStrike : InputBits.None;
		AddChild(_flow);
		try { await Walk(); }
		catch (Exception e) { GD.PushError("FlowCapture: " + e.Message); }
		GetTree().Quit();
	}

	private async Task Walk()
	{
		await Snap("flow_1_title");
		await Confirm(() => _flow.Router.Current == Screen.ControlSelect, "title: Start");
		await Snap("flow_2_control_select");
		await Confirm(() => _flow.Router.Current == Screen.Map, "control select: Start Game");
		await Snap("flow_3_map_new_run");
		for (int node = 1; node <= 2; node++)
		{
			string picked = _flow.Map.CursorId ?? "?";
			await Confirm(() => _flow.Router.Current == Screen.Fight, $"map: pick {picked}");
			FightScene fight = _flow.Fight!;
			Say($"fight on '{fight.Node.NodeId}' vs {fight.Node.Fighter} ({fight.Node.Tier} {fight.Node.ReactionFrames}f), Ryo {fight.Match.P1.Health}");
			await Frames(20);
			await Snap($"flow_{2 + node * 2}_fight_node{node}");
			await Until(() => fight.Stage != DemoStage.Fighting, 1200, "the duel to end");
			Say($"duel over at tick {fight.Match.Tick}: stage {fight.Stage}, winner P{fight.Match.Winner + 1}, Ryo {fight.Match.P1.Health}");
			if (fight.Stage == DemoStage.Lost) { await Snap("flow_lost"); return; }
			for (int i = 0; i < 12 && _flow.Router.Current == Screen.Fight; i++)
			{
				if (fight.Stage == DemoStage.Reward && i < 4) await Snap($"flow_reward_node{node}");
				await Confirm(() => true, $"confirm on {fight.Stage}");
				await Frames(20);
			}
			Say($"router {_flow.Router.Current}; run health {_flow.Session?.Run.Health}, visited [{string.Join(", ", _flow.Session?.Map.Visited ?? Array.Empty<string>())}], fight freed: {_flow.Fight == null}");
			await Snap($"flow_{3 + node * 2}_map_after_node{node}");
			if (_flow.Router.Current != Screen.Map) return;
		}
	}

	private static void Say(string what) => GD.Print("FlowCapture: " + what);

	/// <summary>One Enter key press and release as input events, then waits for <paramref name="done"/>.</summary>
	private async Task Confirm(Func<bool> done, string what)
	{
		Input.ParseInputEvent(new InputEventKey { Keycode = Key.Enter, PhysicalKeycode = Key.Enter, Pressed = true });
		await Frames(2);
		Input.ParseInputEvent(new InputEventKey { Keycode = Key.Enter, PhysicalKeycode = Key.Enter, Pressed = false });
		for (int i = 0; i < 30 && !done(); i++) await Frames(1);
		Say($"{what}: {(done() ? "ok" : "NOT REACHED")} (router {_flow.Router.Current})");
	}

	private async Task Until(Func<bool> done, int frames, string what)
	{
		for (int i = 0; i < frames && !done(); i++) await Frames(1);
		if (!done()) Say($"timed out waiting for {what}");
	}

	private async Task Frames(int n)
	{
		for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
	}

	private async Task Snap(string name)
	{
		await Frames(2);
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		string path = System.IO.Path.Combine(_dir, name + ".png");
		GetViewport().GetTexture().GetImage().SavePng(path);
		Say("saved " + path);
	}
}
