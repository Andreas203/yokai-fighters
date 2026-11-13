using System.Linq;
using Godot;
using YokaiFighters.Ai;
using YokaiFighters.Dev;
using YokaiFighters.Fight;
using YokaiFighters.Flow;
using YokaiFighters.Harness;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-trailer recording tool (not a test, ships nothing): one deterministic trailer shot per launch, recorded with
/// Godot's Movie Maker. The fight shots are real fights from <see cref="DevJump.BuildFight"/>: Ryo is the newcomer
/// Kihon bot (harness), the yokai is the scene's own ProfileAi, both on fixed seeds, one sim tick per rendered frame.
/// Run: godot --path game --write-movie out.avi --fixed-fps 60 --resolution 1920x1080 res://tests/captures/trailer_capture.tscn
///   -- shot=fight opp=kitsune seed=3 ff=600 len=360 [copy=foxfire] [foe=40] [ko=1] [scout=1]
/// ff = ticks simulated before the first frame; foe = also fast-forward until the yokai is under this % health;
/// ko = run on past len until the KO lands plus 150 frames; scout = no frames, print the fight's events.
/// Other shots: shot=map, shot=reward, shot=reveal (Tanuki boss reveal card), each len frames.
/// </summary>
public partial class TrailerCapture : Node
{
	string _shot = "fight", _opp = "kitsune";
	string? _copy;
	uint _seed = DevJump.DefaultSeed;
	int _ff, _len = 360, _foe = 101, _frame, _koFrame = -1;
	bool _ko, _scout, _elder;
	DevStage _stage = DevStage.Node;
	string? _give, _want, _kind;
	int _giveLv = 1, _pickAt = -1, _fdAt = -1, _cursorTo;
	FightScene? _scene;
	NewcomerBot? _bot;
	RewardScreen? _reward;
	MapScreen? _map;

	public override void _Ready()
	{
		foreach (string a in OS.GetCmdlineUserArgs())
		{
			int eq = a.IndexOf('=');
			if (eq < 0) continue;
			string k = a[..eq], v = a[(eq + 1)..];
			switch (k)
			{
				case "shot": _shot = v; break;
				case "opp": _opp = v; break;
				case "seed": _seed = uint.Parse(v); break;
				case "ff": _ff = int.Parse(v); break;
				case "len": _len = int.Parse(v); break;
				case "foe": _foe = int.Parse(v); break;
				case "copy": _copy = v; break;
				case "ko": _ko = v == "1"; break;
				case "scout": _scout = v == "1"; break;
				case "elder": _elder = v == "1"; break;
				case "give": _give = v; break;
				case "givelv": _giveLv = int.Parse(v); break;
				case "want": _want = v; break;
				case "kind": _kind = v; break;
				case "fd": _fdAt = int.Parse(v); break;
				case "stage": _stage = System.Enum.Parse<DevStage>(v); break;
			}
		}
		int music = AudioServer.GetBusIndex("Music"); // the trailer lays its own music bed; keep the hit sounds
		if (music >= 0) AudioServer.SetBusMute(music, true);
		try { Shoot(); }
		catch (System.Exception e) { GD.PushError($"trailer shot failed: {e.Message}"); GetTree().Quit(1); }
	}

	void Shoot()
	{
		switch (_shot)
		{
			case "map": Map(); break;
			case "reward": Reward(); break;
			case "draft": Draft(); break;
			case "reveal": Fight(reveal: true); break;
			default: Fight(reveal: false); break;
		}
	}

	void Fight(bool reveal)
	{
		var pool = FightScene.LoadAbilityPool();
		var jump = DevJump.BuildFight(new DevFightSetup(_opp, Elder: _elder, Stage: _stage, CopySpecial: _copy, CopyLevel: _copy is null ? 1 : 3, Seed: _seed), pool, _give is null ? null : new GiveSource(_give, _giveLv));
		if (!jump.Session.Enter(jump.NodeId).Ok) { GD.PushError($"{_opp}: node not entered"); GetTree().Quit(1); return; }
		var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		scene.ExternalDrive = true;
		scene.Session = jump.Session;
		if (reveal) scene.StoryHas = _ => false;
		AddChild(scene);
		scene.GetNodeOrNull("DebugOverlay")?.QueueFree();
		_scene = scene;
		if (!reveal && scene.TwistCard is { Showing: true } card) card.Continue(); // the elder twist card would cover the whole shot
		_bot = new NewcomerBot(BotConfig.LoadDefault(ContentPaths.DataDir).Fight, BotKit.From(scene.Match.P1), _seed * 2654435761u + 17u);
		GD.Print($"trailer {_opp} seed {_seed}: P1 moves {string.Join(",", scene.Match.P1.Moves.Select(m => m.Id))}; P2 moves {string.Join(",", scene.Match.P2.Moves.Select(m => m.Id))}");
		if (reveal) return;
		if (_scout) { Scout(); return; }
		var m = scene.Match;
		for (int t = 0; t < _ff && m.Phase == MatchPhase.Fighting; t++) Tick();
		while (m.Phase == MatchPhase.Fighting && m.P2.Health * 100 >= _foe * m.P2.MaxHealth && m.Tick < 20000) Tick();
		GD.Print($"trailer: recording from tick {m.Tick}, P1 {m.P1.Health}/{m.P1.MaxHealth}, P2 {m.P2.Health}/{m.P2.MaxHealth}, phase {m.Phase}");
	}

	void Tick() => _scene!.StepWithAi(_bot!.Next(AiView.Capture(_scene.Match)));

	/// <summary>Prints every move start, hit and the KO so a recording window can be picked without rendering.</summary>
	void Scout()
	{
		var m = _scene!.Match;
		var prev = new (FighterState S, int Slot, int Hp)[2];
		for (int i = 0; i < 2; i++) prev[i] = (m.Fighters[i].State, -1, m.Fighters[i].Health);
		int shots = 0;
		while (m.Phase != MatchPhase.Over && m.Tick < 12000)
		{
			Tick();
			for (int i = 0; i < 2; i++)
			{
				var f = m.Fighters[i];
				if (f.State == FighterState.Attack && (prev[i].S != FighterState.Attack || prev[i].Slot != f.MoveSlot))
					GD.Print($"t{m.Tick} P{i + 1} {f.CurrentMove?.Id}");
				if (f.State != prev[i].S && f.State is FighterState.Knockdown or FighterState.Thrown or FighterState.Blockstun)
					GD.Print($"t{m.Tick} P{i + 1} -> {f.State}");
				if (f.Health != prev[i].Hp) GD.Print($"t{m.Tick} P{i + 1} hp {prev[i].Hp}->{f.Health}");
				prev[i] = (f.State, f.MoveSlot, f.Health);
			}
			if (m.Projectiles.Count > shots) GD.Print($"t{m.Tick} projectile x{m.Projectiles.Count}");
			shots = m.Projectiles.Count;
		}
		GD.Print($"SCOUT {_opp} seed {_seed}: end t{m.Tick} phase {m.Phase} P1 {m.P1.Health} P2 {m.P2.Health}");
		GetTree().Quit();
	}

	void Map()
	{
		var screen = GD.Load<PackedScene>("res://scenes/ui/map_screen.tscn").Instantiate<MapScreen>();
		AddChild(screen);
		string md = ContentPaths.Data("map");
		var session = RunSession.Start(FightScene.LoadAbilityPool(), FileMapSource.Default(md).Load(), NodeSceneTable.Default(md), _seed);
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
		session.Run.SetHealth(840);
		screen.SetView(new SessionMapView(session));
		_map = screen;
	}

	void Reward()
	{
		var layer = new CanvasLayer();
		AddChild(layer);
		_reward = GD.Load<PackedScene>("res://scenes/ui/reward_screen.tscn").Instantiate<RewardScreen>();
		layer.AddChild(_reward);
		_reward.Present(new StubRewardSource());
	}

	/// <summary>The real reward draft (A7-A12) after beating <c>opp</c>: the first seed from <c>seed</c> whose offer holds
	/// card <c>want</c> (of <c>kind</c>, default NewMove). The cursor walks to it, rests, then takes it.</summary>
	void Draft()
	{
		var pool = FightScene.LoadAbilityPool();
		var run = _give is null ? RunState.NewRun(pool) : new GiveSource(_give, _giveLv).Build(pool);
		var kind = _kind is null ? Ui.CardKind.NewMove : System.Enum.Parse<Ui.CardKind>(_kind);
		for (uint seed = _seed; seed < _seed + 500; seed++)
		{
			var draft = RewardDraft.Build(run, pool, _opp, seed, elder: _elder);
			var src = new DraftRewardSource(draft, run);
			int at = -1;
			for (int i = 0; i < src.Offer.Cards.Count; i++) if (src.Offer.Cards[i].Id == _want && src.Offer.Cards[i].Kind == kind) at = i;
			if (at < 0) continue;
			GD.Print($"trailer draft {_opp}: seed {seed}, {_want} ({kind}) at card {at}: {string.Join(" | ", src.Offer.Cards.Select(c => c.Kind + " " + c.Id))}");
			var layer = new CanvasLayer();
			AddChild(layer);
			_reward = GD.Load<PackedScene>("res://scenes/ui/reward_screen.tscn").Instantiate<RewardScreen>();
			layer.AddChild(_reward);
			_reward.Present(src);
			_cursorTo = at == 0 ? 3 : at; // card 0 wanted: walk the cursor round once (0 -> 1 -> 2 -> 0)
			_pickAt = _len - 40;
			_shot = "draft";
			return;
		}
		GD.PushError($"no {_opp} draft offers {_want}");
		GetTree().Quit(1);
	}

	public override void _Process(double delta)
	{
		if (_scout) return;
		_frame++;
		if (_scene is { } s && _shot == "fight")
		{
			Tick();
			if (_ko && _koFrame < 0 && s.Match.Phase != MatchPhase.Fighting) _koFrame = _frame;
		}
		if (_reward is { } r && _shot == "reward" && (_frame == 70 || _frame == 130)) r.Move(1);
		if (_reward is { } d && _shot == "draft")
		{
			if (_frame >= 40 && _frame % 30 == 10 && (_frame - 40) / 30 < _cursorTo) d.Move(1);
			if (_frame == _fdAt || (_fdAt > 0 && _frame == _fdAt + 60)) d.ToggleFrameData();
			if (_frame == _pickAt) d.Confirm();
		}
		if (_map is { } mp && _frame <= 200) mp.PanTo(120 + _frame * 4); // a slow pan along the paper
		bool done = _ko ? (_koFrame >= 0 && _frame >= _koFrame + 150) || _frame >= _len + 1800 : _frame >= _len;
		if (done) GetTree().Quit();
	}
}

/// <summary>Trailer loadout: the default new run with one special equipped (A3) at a level, through the dev copy shaper.</summary>
sealed class GiveSource : IDevRunStateSource
{
	readonly string _id;
	readonly int _level;
	public GiveSource(string id, int level) { _id = id; _level = level; }
	public RunState Build(AbilityPool pool)
	{
		var run = RunState.NewRun(pool);
		if (_level > 1) DevJump.ShapeCopy(run, pool, _id, _level);
		else run.Equip(pool.Special(_id)!); // a fresh draft pick: A3, its own slot at Lv 1
		return run;
	}
	public string Describe => $"Trailer: {_id} Lv {_level} equipped.";
}
