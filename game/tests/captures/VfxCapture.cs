using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-242 windowed screenshot tool (not a test): each projectile in flight (Spirit Wave, Foxfire, Great Wave), the
/// cast, release and impact effects, and the default effects the data-less specials get from their move properties
/// (dust, trail, ring, afterimage, grab), all at the game camera in the fight scene.
/// Run: godot --audio-driver Dummy --path game res://tests/captures/vfx_capture.tscn --resolution 1920x1080 -- out=&lt;dir&gt;
/// </summary>
public partial class VfxCapture : Node
{
	private string _dir = ".";
	const int S = SimConfig.Scale;
	static readonly FighterInput Idle = FighterInput.None;

	public override async void _Ready()
	{
		foreach (string a in OS.GetCmdlineUserArgs())
			if (a.StartsWith("out=")) _dir = a[4..];
		var moves = SpecialLoader.LoadDirectory(ContentPaths.Data("moves"));
		var wave = moves.Single(s => s.Id == "spirit-wave");
		var fox = moves.Single(s => s.Id == "foxfire");

		// Spirit Wave Lv 1: cast, release, in flight, impact.
		await Projectile("spirit_wave", wave, 1, shooter: 0, new[] { (2, "cast"), (0, "release"), (14, "flight"), (-1, "impact") });
		await Projectile("great_wave", wave, 3, shooter: 0, new[] { (14, "flight"), (-1, "impact") });
		await Projectile("foxfire", fox, 1, shooter: 1, new[] { (3, "cast"), (0, "release"), (30, "flight"), (-1, "impact") });
		await Projectile("foxfire_lv2", fox, 2, shooter: 1, new[] { (30, "flight") });
		await Projectile("foxfire_piercing", fox, 3, shooter: 1, new[] { (30, "flight") });
		await DatalessEffects();
		GetTree().Quit();
	}

	private FightScene NewScene()
	{
		var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		scene.ExternalDrive = true;
		AddChild(scene);
		if (scene.P2Ai != null) scene.ToggleP2Ai();
		return scene;
	}

	/// <summary>
	/// One special fired by <paramref name="shooter"/>. Each shot is (n, tag): n &gt; 0 = n ticks after the press (cast) or
	/// after the spawn (flight), 0 = the spawn tick (release), -1 = the impact tick.
	/// </summary>
	private async Task Projectile(string name, SpecialData data, int level, int shooter, (int N, string Tag)[] shots)
	{
		var scene = NewScene();
		var m = scene.Match;
		var f = m.Fighters[shooter];
		f.Equip(data.Slot, data, level);
		m.P1.X = -260 * S;
		m.P2.X = 260 * S;
		for (int t = 0; t < 4; t++) scene.Step(Idle, Idle);
		var go = FighterInput.Special(data.Slot);
		scene.Step(shooter == 0 ? go : Idle, shooter == 0 ? Idle : go);
		int pressed = m.Tick, spawned = -1;
		bool hit = false;
		m.Impact += (_, _) => hit = true;
		foreach (var (n, tag) in shots)
		{
			for (int t = 0; t < 200; t++)
			{
				if (m.Projectiles.Count > 0 && spawned < 0) spawned = m.Tick;
				bool due = tag switch
				{
					"cast" => m.Tick - pressed >= n,
					"release" => spawned >= 0,
					"flight" => spawned >= 0 && m.Tick - spawned >= n,
					_ => hit,
				};
				if (due) break;
				scene.Step(Idle, Idle);
			}
			if (tag == "impact") { scene.Step(Idle, Idle); scene.Step(Idle, Idle); } // the splash two ticks in
			await Shot($"{name}_{tag}");
		}
		scene.QueueFree();
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
	}

	/// <summary>The default looks a data-less special gets from its properties, spawned through the shared hook.</summary>
	private async Task DatalessEffects()
	{
		var shots = new (string Name, VfxLook Look, uint Colour, int Owner)[]
		{
			("oni_quake_dust", VfxLook.Dust, HitSparkStyle.OniRed, 0),
			("iron_club_charge_trail", VfxLook.Trail, HitSparkStyle.OniRed, 0),
			("shell_spin_ring", VfxLook.Ring, HitSparkStyle.KappaTeal, 0),
			("fox_mirage_afterimage", VfxLook.Afterimage, HitSparkStyle.Foxfire, 1),
			("river_grab_whirl", VfxLook.Grab, HitSparkStyle.KappaTeal, 0),
			("talisman_seal_cast", VfxLook.Seal, HitSparkStyle.Ink, 0),
		};
		foreach (var (name, look, colour, owner) in shots)
		{
			var scene = NewScene();
			var m = scene.Match;
			m.P1.X = -110 * S;
			m.P2.X = 110 * S;
			scene.Step(Idle, Idle);
			var f = m.Fighters[owner];
			var shape = SpecialVfxStyle.ShapeOf(look);
			int x = look switch
			{
				VfxLook.Grab => (m.P1.X + m.P2.X) / 2,
				VfxLook.Afterimage => f.X - f.Facing * 220 * S, // left behind where the teleport started
				_ => f.X + shape.OffX * S * f.Facing,
			};
			int y = look == VfxLook.Dust ? shape.H * S / 2 : f.Y + shape.OffY * S;
			int frames = shape.Frames > 0 ? shape.Frames : 40;
			scene.Fx.Spawn(new VfxEffect(m.Tick, VfxMoment.Cast, owner, f.MoveSerial, VfxAnchor.Point, x, y, shape.W * S, shape.H * S, f.Facing, frames,
				new VfxStyle(look, colour, SpecialVfxStyle.DefaultCore)));
			for (int t = 0; t < Math.Min(7, frames / 3); t++) scene.Step(Idle, Idle);
			await Shot(name);
			scene.QueueFree();
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		}
	}

	private async Task Shot(string tag)
	{
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		var img = GetViewport().GetTexture().GetImage();
		img.SavePng($"{_dir}/{tag}.png");
		GD.Print($"VfxCapture: {tag}");
	}
}
