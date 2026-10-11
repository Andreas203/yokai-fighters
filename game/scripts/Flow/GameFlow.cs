using System;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Settings;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Flow;

/// <summary>
/// The game's boot scene (<c>scenes/boot.tscn</c>, the project's main scene): title → control select → map ⇄ fight,
/// with Settings and Quit routed. YOK-77: this node holds the <see cref="RunSession"/>, so the run outlives every
/// node's scene: pick on the map (<see cref="ChooseNode"/>) → the scene the session's resolver names runs the node
/// → <see cref="RunSession.NodeCompleted"/> brings the map back, until the run ends on the fight's own end screens. All decisions are <see cref="ScreenRouter"/>'s; this node only shows and hides the menu
/// scenes and creates or frees the fight scene when the router changes screen.
/// <para>
/// The sim is untouched by menus because the fight scene (<c>scenes/fight.tscn</c>) does not exist until the router
/// reaches <see cref="Screen.Fight"/>, and is freed on the way back to the title. The fight scene still runs on its
/// own (tests, captures, the harness, F6 in the editor): opening it directly is the bypass, with <c>ExternalDrive</c>
/// for stepped runs. From the boot scene, the user arg <c>--fight</c> (or <c>--fight=kata</c> / <c>--fight=kihon</c>)
/// skips the menus: <c>godot --path game -- --fight=kihon</c>.
/// </para>
/// </summary>
public partial class GameFlow : Node
{
	public const string FightScenePath = "res://scenes/fight.tscn";
	public const string DirectFightArg = "--fight";

	public ScreenRouter Router { get; } = new();
	public TitleMenu Title { get; private set; } = null!;
	public StartScreen ControlSelect { get; private set; } = null!;
	/// <summary>The running fight; null on every other screen.</summary>
	public FightScene? Fight { get; private set; }
	/// <summary>The scene running the entered node (today always <see cref="Fight"/>); null off a node.</summary>
	public Node? NodeScene { get; private set; }

	/// <summary>The map every new run walks (in data/map/).</summary>
	public string MapFile { get; set; } = FileMapSource.DefaultFile;
	/// <summary>YOK-77: the run in progress, from Start Game until the title is shown again. Null on the menus and in a direct fight.</summary>
	public RunSession? Session { get; private set; }
	/// <summary>YOK-78's map screen (<c>Menus/Map</c> in boot.tscn = <c>scenes/ui/map_screen.tscn</c>), shown on <see cref="Screen.Map"/>.</summary>
	public MapScreen Map { get; private set; } = null!;
	/// <summary>Pins every run's seed (tests, harness); null = a new random seed per run.</summary>
	public uint? FixedRunSeed { get; set; }
	private bool _freshRun;

	/// <summary>Quit: the tree by default; tests replace it.</summary>
	public Action? QuitAction { get; set; }
	/// <summary>Set before the node enters the tree to go straight into a fight with this scheme (what <c>--fight</c> does).</summary>
	public ControlScheme? DirectFight { get; set; }
	/// <summary>Configures each fight scene before it joins the tree (tests: <c>ExternalDrive</c>).</summary>
	public Action<FightScene>? ConfigureFight { get; set; }

	/// <summary>
	/// Settings file for this boot. Null (the default) = the player's <see cref="UserSettingsPath"/> when this node is
	/// the running game's main scene, and no file at all when it is instanced by something else (tests, captures), so
	/// those can never read or write the player's file. Tests set a temp path to exercise the load.
	/// </summary>
	public string? SettingsFile { get; set; }
	/// <summary>What the settings file held at boot; null when none was used.</summary>
	public SettingsLoad? SettingsLoaded { get; private set; }

	public static string UserSettingsPath => ProjectSettings.GlobalizePath("user://" + SettingsStore.FileName);

	/// <summary>Before the menus are ready, so the title and control select first draw with the saved values.</summary>
	public override void _EnterTree()
	{
		string? path = SettingsFile ?? (GetTree().CurrentScene == this ? UserSettingsPath : null);
		if (path == null) return;
		SettingsLoaded = MenuSettings.Attach(new SettingsStore(path));
		GD.Print($"GameFlow: settings {path}: {SettingsLoaded.Status}");
		if (SettingsLoaded.Status is not (SettingsLoadStatus.Loaded or SettingsLoadStatus.Missing))
			GD.PushWarning($"GameFlow: settings {path}: {SettingsLoaded.Status} ({string.Join("; ", SettingsLoaded.Problems)})");
	}

	public override void _Ready()
	{
		Title = GetNode<TitleMenu>("Menus/Title");
		ControlSelect = GetNode<StartScreen>("Menus/ControlSelect");
		ControlSelect.Close();
		Map = GetNode<MapScreen>("Menus/Map");
		Map.Visible = false; // hidden = it reads no input
		Map.NodeChosen += id => ChooseNode(id); // the screen's view (SessionMapView) has already entered the node

		Title.StartRequested += () => Router.Select(TitleEntry.Start);
		Title.ContinueRequested += () => Router.Select(TitleEntry.Continue);
		Title.PracticeRequested += () => Router.Select(TitleEntry.Practice);
		Title.SettingsRequested += () => Router.Select(TitleEntry.Settings);
		Title.SettingsBackRequested += () => Router.Back();
		Title.QuitRequested += () => Router.Select(TitleEntry.Quit);
		ControlSelect.BackRequested += () => Router.Back();
		ControlSelect.Started += () => Router.Confirm();

		foreach (var entry in Router.Entries) Title.SetEntry(entry);
		Router.EntryChanged += Title.SetEntry;
		Router.Changed += OnScreenChanged;
		Router.Quit += () => { if (QuitAction != null) QuitAction(); else GetTree().Quit(); };

		// One scheme value for the whole menu side: the settings sheet and control select both write MenuSettings.
		Router.ChooseScheme(MenuSettings.Scheme);
		MenuSettings.Changed += OnSettingsChanged;

		ControlScheme? direct = DirectFight ?? DirectFightFromArgs(OS.GetCmdlineUserArgs());
		if (direct is { } scheme) Router.StartFightDirect(scheme);
	}

	public override void _ExitTree()
	{
		MenuSettings.Changed -= OnSettingsChanged;
		if (SettingsLoaded != null) MenuSettings.Detach(); // every change is already on disk
	}

	private void OnSettingsChanged() => Router.ChooseScheme(MenuSettings.Scheme);

	/// <summary><c>--fight</c> = Kihon (the default scheme), <c>--fight=kata</c> / <c>--fight=kihon</c>; null without the arg.</summary>
	public static ControlScheme? DirectFightFromArgs(string[] args)
	{
		foreach (string arg in args)
		{
			if (arg == DirectFightArg) return ControlScheme.Kihon;
			if (!arg.StartsWith(DirectFightArg + "=", StringComparison.Ordinal)) continue;
			string value = arg[(DirectFightArg.Length + 1)..];
			if (Enum.TryParse(value, ignoreCase: true, out ControlScheme scheme) && Enum.IsDefined(scheme)) return scheme;
			GD.PushWarning($"GameFlow: unknown scheme '{value}' in {arg}, using Kihon");
			return ControlScheme.Kihon;
		}
		return null;
	}

	/// <summary>Leave the fight or the map for the title (the pause menu's "Quit to title"); the run is dropped. False on the menus.</summary>
	public bool ReturnToTitle() => Router.ReturnToTitle();

	/// <summary>
	/// YOK-77: the player confirmed <paramref name="nodeId"/> on the map: its scene starts. The map screen's
	/// <c>NodeChosen</c> lands here with the node already entered (its view's <c>Select</c> is
	/// <see cref="RunSession.Enter"/>); called directly (tests, a harness) it enters the node first. Either way the
	/// node is entered once; a pick the map does not allow is refused with the reason and changes nothing.
	/// </summary>
	public MapResult ChooseNode(string nodeId)
	{
		if (Router.Current != Screen.Map || Session is null) return MapResult.Fail("the map is not showing");
		if (Session.Node?.NodeId != nodeId)
		{
			var step = Session.Enter(nodeId);
			if (!step.Ok) { GD.PushWarning($"GameFlow: node '{nodeId}' refused: {step.Error}"); return step; }
		}
		Router.EnterNode();
		return MapResult.Done;
	}

	/// <summary>A new run from inside a fight (Restart on the lose screen, the end card or the pause menu): back to the map on a fresh session.</summary>
	public bool RestartRun()
	{
		if (Router.Current != Screen.Fight) return false;
		_freshRun = true;
		return Router.ReturnToMap();
	}

	/// <summary>Starts the run the map will show. False (with the error logged) when the map data cannot be loaded.</summary>
	private bool StartRun()
	{
		_freshRun = false;
		try { Session = FightScene.NewSession(MapFile, FixedRunSeed ?? (uint)Random.Shared.Next()); }
		catch (Exception e) when (e is FormatException or System.IO.IOException)
		{
			GD.PushError($"GameFlow: no run: {e.Message}");
			Session = null;
			return false;
		}
		var session = Session;
		GD.Print($"GameFlow: new run on map '{session.Map.Graph.Id}', seed {session.Seed}");
		session.NodeEntered += n => GD.Print($"GameFlow: entered '{n.NodeId}' ({n.Type}, row {n.Row})");
		session.NodeCompleted += n =>
		{
			GD.Print($"GameFlow: completed '{n.NodeId}', health {session.Run.Health}");
			if (session == Session && !session.IsOver) Router.ReturnToMap(); // the last node stays on its end card
		};
		session.RunEnded += end => GD.Print($"GameFlow: run {(end.Won ? "won" : "lost")} on '{end.NodeId}' (row {end.RowReached})");
		return true;
	}

	private void OnScreenChanged(Screen from, Screen to)
	{
		if (from == Screen.Settings) Title.CloseSettings();
		if (from == Screen.Fight) EndFight();
		if (from == Screen.Map) Map.Visible = false;
		switch (to)
		{
			case Screen.Map:
				Title.Visible = false;
				ControlSelect.Close();
				if ((from == Screen.ControlSelect || _freshRun || Session is null) && !StartRun()) { Router.ReturnToTitle(); break; }
				Map.SetView(new SessionMapView(Session!)); // a fresh view each time: current node states, pick lock cleared
				Map.Visible = true;
				break;
			case Screen.Title:
				Session = null; // no save yet (YOK-73): leaving for the title drops the run
				ControlSelect.Close();
				Title.Open();
				break;
			case Screen.Settings:
				Title.OpenSettings();
				break;
			case Screen.ControlSelect:
				Title.Visible = false;
				ControlSelect.Open();
				break;
			case Screen.Fight:
				Title.Visible = false;
				ControlSelect.Close();
				BeginFight();
				break;
		}
	}

	/// <summary>
	/// The entered node's scene, from the session's resolver (data/map/node-scenes.json); with no session (the
	/// direct-fight bypass) the fight scene on its own, which walks the demo map by itself.
	/// </summary>
	private void BeginFight()
	{
		string path = Session?.Entered?.Scene ?? FightScenePath;
		Node scene = GD.Load<PackedScene>(path).Instantiate();
		if (scene is FightScene fight)
		{
			fight.Session = Session; // null = the bypass
			fight.PlayerScheme = Router.Scheme; // carried into every duel of the run
			fight.TitleRequested += () => Router.ReturnToTitle();
			fight.RunRestartRequested += () => RestartRun();
			ConfigureFight?.Invoke(fight);
			Fight = fight;
		}
		else if (scene is INodeScene other) other.BindRun(Session!, Router.Scheme);
		else GD.PushWarning($"GameFlow: {path} is neither a FightScene nor an INodeScene; it must complete the node through GameFlow.Session itself");
		NodeScene = scene;
		AddChild(scene);
		MoveChild(scene, 0); // under the menu layer
	}

	private void EndFight()
	{
		GetTree().Paused = false; // the pause menu pauses the tree; the title must run
		if (NodeScene is { } scene && IsInstanceValid(scene)) { RemoveChild(scene); scene.QueueFree(); }
		Fight = null;
		NodeScene = null;
	}
}
