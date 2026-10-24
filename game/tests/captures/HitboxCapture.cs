using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-33 windowed screenshot tool (not a test): every Ryo and Kitsune move on its first and last hitbox frame (and a
/// projectile's spawn frame) with the YOK-22 box overlay on, cropped around the fighters.
/// Run: godot --path game res://tests/hitbox_capture.tscn --resolution 1920x1080 -- out=&lt;dir&gt; [only=&lt;move id&gt;]
/// </summary>
public partial class HitboxCapture : Node
{
	private string _dir = ".";
	private string? _only;
	private const int WalkTicks = 14;
	private static readonly Rect2I Crop = new(560, 420, 800, 460);

	public override async void _Ready()
	{
		foreach (string a in OS.GetCmdlineUserArgs())
		{
			if (a.StartsWith("out=")) _dir = a[4..];
			if (a.StartsWith("only=")) _only = a[5..];
		}
		var probe = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		AddChild(probe);
		var jobs = new List<(int who, string id, int frame, int slot, bool air)>();
		for (int who = 0; who < 2; who++)
		{
			var f = probe.Match.Fighters[who];
			foreach (var m in f.Moves)
			{
				if (m.Hitboxes.Count == 0 && m.Throwboxes.Count == 0) continue;
				var frames = new SortedSet<int>();
				var boxes = m.Hitboxes.Count > 0 ? m.Hitboxes : m.Throwboxes;
				frames.Add(boxes[0].First);
				frames.Add(boxes[^1].Last);
				foreach (int fr in frames) jobs.Add((who, m.Id, fr, -1, m.Air));
			}
			for (int s = 0; s < f.Specials.Length; s++)
			{
				var sp = f.Specials[s];
				if (sp is null) continue;
				var m = sp.Move;
				var frames = new SortedSet<int>();
				if (m.Hitboxes.Count > 0) { frames.Add(m.Hitboxes[0].First); frames.Add(m.Hitboxes[^1].Last); }
				if (m.Projectile is { } pd) frames.Add(pd.SpawnFrame);
				foreach (int fr in frames) jobs.Add((who, m.Id, fr, s, false));
			}
		}
		probe.QueueFree();
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		foreach (var j in jobs)
		{
			if (_only != null && j.id != _only) continue;
			await Shot(j.who, j.id, j.frame, j.slot, j.air);
		}
		GetTree().Quit();
	}

	private async System.Threading.Tasks.Task Shot(int who, string id, int frame, int slot, bool air)
	{
		var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		scene.ExternalDrive = true;
		AddChild(scene);
		scene.GetNode<DebugOverlay>("DebugOverlay").HandleKey(DebugOverlay.ToggleKey);
		var fighter = scene.Match.Fighters[who];
		for (int t = 0; t < WalkTicks; t++) scene.Step(new FighterInput(InputBits.Right), new FighterInput(InputBits.Left));
		for (int t = 0; t < 30; t++) scene.Step(FighterInput.None, FighterInput.None);
		if (air)
		{
			var jump = new FighterInput(InputBits.Up);
			scene.Step(who == 0 ? jump : FighterInput.None, who == 0 ? FighterInput.None : jump);
			for (int t = 0; t < 8; t++) scene.Step(FighterInput.None, FighterInput.None);
		}
		FighterInput go = slot >= 0 ? FighterInput.Special((SpecialSlot)slot) : FighterInput.Attack(Array.FindIndex(fighter.Moves, m => m.Id == id));
		scene.Step(who == 0 ? go : FighterInput.None, who == 0 ? FighterInput.None : go);
		for (int t = 0; t < 60 && !(fighter.CurrentMove?.Id == id && fighter.MoveFrame >= frame); t++) scene.Step(FighterInput.None, FighterInput.None);
		string tag = $"{id}-f{frame}";
		GD.Print($"{tag}: {fighter.State} {fighter.CurrentMove?.Id} {fighter.MoveFrame} y={fighter.Y}");
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		GetViewport().GetTexture().GetImage().GetRegion(Crop).SaveJpg($"{_dir}/{tag}.jpg", 0.88f);
		scene.QueueFree();
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
	}
}
