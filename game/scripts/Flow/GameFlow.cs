using System;
using Godot;
using YokaiFighters.Dev;
using YokaiFighters.Fight;
using YokaiFighters.SaveGame;
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

	/// <summary>
	/// YOK-87: save file for this boot, next to the settings file and under the same rule: null (the default) = the
	/// player's <see cref="UserSavePath"/> when this node is the running game's main scene, no file at all when it is
	/// instanced by something else (tests, captures). Tests set a temp path.
	/// </summary>
	public string? SaveFile { get; set; }
	/// <summary>The save file in use; null = nothing is read or written (and Continue stays off).</summary>
	public SaveStore? Saves { get; private set; }
	/// <summary>What the save file held at boot; null when none was used.</summary>
	public SaveLoad? SaveLoaded { get; private set; }
	/// <summary>The save as last written (or loaded): the run in progress, unlocks, story progress (M4).</summary>
	public SaveData Saved { get; private set; } = SaveData.Empty;

	public static string UserSavePath => ProjectSettings.GlobalizePath("user://" + SaveStore.FileName);
	/// <summary>The map file of the run in progress (a continued run keeps the one it was saved with).</summary>
	private string? _runMapFile;
	/// <summary>The last write was refused: the file is behind <see cref="Saved"/> (e.g. it still holds an ended run). Retried on the title and on exit.</summary>
	private bool _writePending;
	/// <summary>YOK-106: the node entered (or continued into) is Ryo's first meeting with its elder.</summary>
	private bool _firstMeeting;

	/// <summary>Y3 (YOK-106): Ryo has met this elder (the yokai id an elder node names), in this run or an earlier one. Story progress, per save (M4).</summary>
	public bool HasMetElder(string elderId) => Saved.Story.HasMetElder(elderId);
	/// <summary>
	/// Y3 (YOK-106): the node Ryo is in is an elder's, met for the first time on this entry (the intro card and the
	/// callout show). Set before <see cref="RunSession.NodeEntered"/> reaches handlers added after this flow's; stays
	/// true when a quit inside that node is continued (M5). False off a node and on any later meeting.
	/// </summary>
	public bool FirstMeeting => _firstMeeting && Session is { Map.InNode: true };
	/// <summary>YOK-90 (Y3): the twist of the elder node Ryo is in (intro card; the callout when <see cref="FirstMeeting"/>), else
	/// null. A map node's own twist (its icon): <c>FightScene.Twists.ForNode(node)</c>.</summary>
	public ElderTwist? CurrentTwist => Session is { Map.InNode: true } s ? FightScene.Twists.ForNode(s.Map.CurrentNode) : null;

	/// <summary>Why the save file gave no run at boot (unreadable file, unreadable run); shown on Continue until a save succeeds.</summary>
	private string? _saveTrouble;

	/// <summary>The player's save path for this boot (null = no file); while <see cref="DevMode.On"/> the store uses its dev slot instead.</summary>
	private string? _playerSavePath;
	/// <summary>The session that was running when dev mode was switched: it is never saved again, in either slot (YOK-123).</summary>
	private RunSession? _unsavedSession;
	/// <summary>The title's "Dev menu" button (dev mode available only; null otherwise).</summary>
	public Button? DevMenuButton { get; private set; }

	/// <summary>Before the menus are ready, so the title and control select first draw with the saved values.</summary>
	public override void _EnterTree()
	{
		bool mainScene = GetTree().CurrentScene == this;
		_playerSavePath = SaveFile ?? (mainScene ? UserSavePath : null);
		AttachSaves();
		string? path = SettingsFile ?? (mainScene ? UserSettingsPath : null);
		if (path == null) return;
		SettingsLoaded = MenuSettings.Attach(new SettingsStore(path));
		GD.Print($"GameFlow: settings {path}: {SettingsLoaded.Status}");
		if (SettingsLoaded.Status is not (SettingsLoadStatus.Loaded or SettingsLoadStatus.Missing))
			GD.PushWarning($"GameFlow: settings {path}: {SettingsLoaded.Status} ({string.Join("; ", SettingsLoaded.Problems)})");
	}

	/// <summary>
	/// YOK-123: opens the save slot for the current mode: the player's file, or while <see cref="DevMode.On"/> its dev
	/// slot (<see cref="DevMode.SavePathFor"/>), so a dev session never reads or writes the player's save. M5 / AM3
	/// node-boundary saves work the same in either slot. Called at boot and when dev mode is switched.
	/// </summary>
	private void AttachSaves()
	{
		Saves = null;
		SaveLoaded = null;
		Saved = SaveData.Empty;
		_saveTrouble = null;
		_writePending = false;
		if (_playerSavePath is { } playerPath)
		{
			string savePath = DevMode.SavePath(playerPath);
			Saves = new SaveStore(savePath);
			SaveLoaded = Saves.Load();
			Saved = SaveLoaded.Data;
			GD.Print($"GameFlow: save {savePath}: {SaveLoaded.Status}");
			if (SaveLoaded.Status is not (SaveLoadStatus.Loaded or SaveLoadStatus.Missing))
				GD.PushWarning($"GameFlow: save {savePath}: {SaveLoaded.Status} ({string.Join("; ", SaveLoaded.Problems)})");
			if (Saves.WriteBlocked) _saveTrouble = "The save file could not be opened. It is left untouched and nothing is saved this session.";
			else if (SaveLoaded.Status == SaveLoadStatus.Corrupt) _saveTrouble = "The save file could not be read. A copy is kept when the next run is saved.";
			else if (Saved.Run is null && SaveStore.LostSomething(SaveLoaded)) _saveTrouble = "The saved run could not be read. A copy of the file is kept when the next run is saved.";
		}
	}

	public const string PendingWriteReason = "The last save could not be written yet; dev mode can be switched once it is (check the disk, then try again).";

	/// <summary>
	/// <see cref="DevMode.SwitchBlocker"/>: retries a refused write in the current slot, and refuses the switch while it
	/// is still refused, so swapping slots can never drop a pending write (e.g. an ended run still in the player's file, M5).
	/// </summary>
	private string? DevSwitchBlocker()
	{
		RetryPendingWrite();
		return _writePending ? PendingWriteReason : null;
	}

	/// <summary>
	/// Dev mode switched (only possible with no write pending, see <see cref="DevSwitchBlocker"/>): open the other slot.
	/// A run in progress at that moment is not saved again in either slot (its earlier saves stay in the old one).
	/// </summary>
	private void OnDevModeChanged()
	{
		_unsavedSession = Session;
		AttachSaves();
		if (Router.Current == Screen.Title) RefreshContinue();
		RefreshDevButton();
	}

	/// <summary>
	/// YOK-123: opens the dev menu on the title or the map (F11 or the title's Dev menu button). False when the
	/// shell refuses (dev mode unavailable, or off away from the title) or on any other screen.
	/// </summary>
	public bool OpenDevMenu()
	{
		DevPlace? place = Router.Current switch { Screen.Title => DevPlace.Title, Screen.Map => DevPlace.Map, _ => null };
		if (place is not { } at || DevShell.Instance is not { } shell) return false;
		Control? back = at == DevPlace.Title ? Title.StartButton : null;
		return shell.OpenMenu(new DevContext(at, this), () => { if (back != null && IsInstanceValid(back) && back.IsVisibleInTree()) back.GrabFocus(); });
	}

	private void AddDevButton()
	{
		if (!DevMode.Available) return;
		DevMenuButton = new Button { Name = "DevMenuButton", FocusMode = Control.FocusModeEnum.None };
		DevMenuButton.AddThemeFontSizeOverride("font_size", 22);
		DevMenuButton.Position = new Vector2(16, 12);
		DevMenuButton.Size = new Vector2(220, 44);
		DevMenuButton.Pressed += () => OpenDevMenu();
		Title.AddChild(DevMenuButton);
		RefreshDevButton();
	}

	private void RefreshDevButton()
	{
		if (DevMenuButton != null) DevMenuButton.Text = DevMode.On ? "Dev menu (F11)" : "Dev mode (F11)";
	}

	public override void _UnhandledInput(InputEvent e)
	{
		if (e is InputEventKey { Pressed: true, Echo: false } k && k.PhysicalKeycode == DevMode.MenuKey && OpenDevMenu())
			GetViewport().SetInputAsHandled();
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
		Router.Activated += entry => { if (entry == TitleEntry.Continue) ContinueRun(); };
		RefreshContinue();
		Router.Quit += () => { if (QuitAction != null) QuitAction(); else GetTree().Quit(); };

		// One scheme value for the whole menu side: the settings sheet and control select both write MenuSettings.
		Router.ChooseScheme(MenuSettings.Scheme);
		MenuSettings.Changed += OnSettingsChanged;
		DevMode.Changed += OnDevModeChanged;
		DevMode.SwitchBlocker = DevSwitchBlocker;
		AddDevButton();

		ControlScheme? direct = DirectFight ?? DirectFightFromArgs(OS.GetCmdlineUserArgs());
		if (direct is { } scheme) Router.StartFightDirect(scheme);
	}

	public override void _ExitTree()
	{
		MenuSettings.Changed -= OnSettingsChanged;
		DevMode.Changed -= OnDevModeChanged;
		if (DevMode.SwitchBlocker == DevSwitchBlocker) DevMode.SwitchBlocker = null;
		RetryPendingWrite();
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

	/// <summary>Leave the fight or the map for the title (the pause menu's "Quit to title"); the run waits in the save file for Continue. False on the menus.</summary>
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

	/// <summary>
	/// YOK-126: the dev menu's Run state page puts a run it built (<see cref="Dev.DevRunEditor"/>) in place of the
	/// current one. Only on the title (the run then shows on the map) or the map, between nodes, with dev mode on, so
	/// the save it writes goes to the dev slot. Call it through <see cref="DevContext.TryChangeRun"/>. False, with the
	/// reason, anywhere else; nothing changes then.
	/// </summary>
	public bool AdoptDevSession(RunSession session, out string? refusal)
	{
		refusal = !DevMode.On ? DevContext.DevOffReason
			: Router.Current is not (Screen.Title or Screen.Map) ? DevContext.MidFightReason
			: session.Map.InNode || session.IsOver ? "the run must stand between nodes"
			: Router.Current == Screen.Map && Session is { } cur && cur.Map.Graph.Fingerprint != session.Map.Graph.Fingerprint
				? "on the map a dev run keeps the current run's map" : null;
		if (refusal != null) return false;
		// The map file the session's graph came from: on the map the current run's (Continue may have used the saved
		// run's own map_file); on the title the page built the run on MapFile.
		if (Router.Current == Screen.Title || _runMapFile is null) _runMapFile = MapFile;
		Session = session;
		_freshRun = false;
		GD.Print($"GameFlow: dev run state on map '{session.Map.Graph.Id}', seed {session.Seed}, health {session.Run.Health}");
		Adopt(session);
		SaveRun(session);
		if (Router.Current == Screen.Title) Router.ContinueRun();
		else Map.SetView(new SessionMapView(session));
		return true;
	}

	/// <summary>A new run from inside a fight (Restart on the lose screen, the end card or the pause menu): back to the map on a fresh session.</summary>
	public bool RestartRun()
	{
		if (Router.Current != Screen.Fight) return false;
		if (IsDevJumpSession) return Router.ReturnToTitle(); // YOK-125: a dev jump's run is not restarted into a new (saved) run
		_freshRun = true;
		return Router.ReturnToMap();
	}

	/// <summary>Starts the run the map will show. False (with the error logged) when the map data cannot be loaded.</summary>
	private bool StartRun()
	{
		_freshRun = false;
		_firstMeeting = false;
		try
		{
			// YOK-97, M3: the last ended run's best special starts this one at Lv 2 (kept in the save until a run ends).
			var pool = FightScene.LoadAbilityPool();
			var run = CarryOver.NewRun(pool, Saved.CarryOver, null, out string? dropped);
			if (dropped is not null) GD.PushWarning($"GameFlow: {dropped}");
			Session = FightScene.NewSession(MapFile, FixedRunSeed ?? (uint)Random.Shared.Next(), pool, run);
		}
		catch (Exception e) when (e is FormatException or System.IO.IOException)
		{
			GD.PushError($"GameFlow: no run: {e.Message}");
			Session = null;
			return false;
		}
		GD.Print($"GameFlow: new run on map '{Session.Map.Graph.Id}', seed {Session.Seed}");
		_runMapFile = MapFile;
		Adopt(Session);
		SaveRun(Session); // a new run replaces any saved one
		return true;
	}

	/// <summary>
	/// Follows a session (new or continued). YOK-87 save points, all at node boundaries: the run is written when a
	/// node is entered and when one is completed, and removed from the file when the run ends (won or lost).
	/// </summary>
	private void Adopt(RunSession session)
	{
		session.NodeEntered += n =>
		{
			GD.Print($"GameFlow: entered '{n.NodeId}' ({n.Type}, row {n.Row})");
			if (session == Session) Saved = ElderMeetings.Enter(Saved, session.Map.CurrentNode, out _firstMeeting); // written by this save point (M5)
			SaveRun(session);
		};
		session.NodeCompleted += n =>
		{
			if (Fight?.Tutorial is not null) MarkTutorialSeen(); // YOK-92: the guided duel was won
			GD.Print($"GameFlow: completed '{n.NodeId}', health {session.Run.Health}");
			if (session == Session) _firstMeeting = false;
			if (!session.IsOver) SaveRun(session);
			if (session == Session && !session.IsOver) Router.ReturnToMap(); // the last node stays on its end card
		};
		session.RunEnded += end =>
		{
			GD.Print($"GameFlow: run {(end.Won ? "won" : "lost")} on '{end.NodeId}' (row {end.RowReached})");
			// A dev jump's run, or a run in progress when dev mode was switched (both `_unsavedSession`), is never
			// written: neither its end nor a Tanuki win it scores reaches either save slot (intentional, YOK-99).
			if (session == Session && session != _unsavedSession)
			{
				// M3: the carry-over is decided here, at run end, and replaces the last one (null below row 6).
				string? carried = CarryOver.FromRunEnd(session.Run, end);
				if (carried is not null) GD.Print($"GameFlow: '{carried}' carries to the next run at Lv {CarryOver.Level}");
				SaveData after = Saved.WithRun(null).WithCarryOver(carried);
				// YOK-99, Y5: a Tanuki win raises the pressure on every later run (kept even with no file attached).
				if (end.BeatTanuki) after = after.WithTanukiWin();
				// YOK-86, M1: unlocks by the depth reached, the next still-locked ids in the fixed order, in the same write.
				foreach (string id in UnlockOrder(session.Pool).Award(end, after.Unlocks))
				{
					GD.Print($"GameFlow: unlocked '{id}'");
					after = after.WithUnlock(id);
				}
				Write(after);
			}
			if (Fight?.Tutorial is not null) MarkTutorialSeen(); // YOK-92: the guided duel was lost
		};
	}

	/// <summary>
	/// YOK-86, M1: the fixed unlock order, from the run's pool and data/cancels/ (<see cref="Unlocks.FromData"/>). A
	/// cancel folder that fails to load is left out with a warning, so a run end is always saved.
	/// </summary>
	static Unlocks UnlockOrder(AbilityPool pool)
	{
		string[] cancels;
		try { cancels = CancelRuleLoader.LockedIds(ContentPaths.Data("cancels")); }
		catch (Exception e) when (e is FormatException or System.IO.IOException or System.Text.Json.JsonException or InvalidOperationException)
		{
			GD.PushWarning($"GameFlow: cancel rules left out of the unlock order: {e.Message}");
			cancels = Array.Empty<string>();
		}
		try
		{
			var order = Unlocks.FromData(pool, cancels, out var dropped);
			foreach (string id in dropped) GD.PushWarning($"GameFlow: unlock order: '{id}' is named twice in the data; kept once");
			return order;
		}
		catch (Exception e) // the run end must still be written: no unlocks this time rather than no save
		{
			GD.PushWarning($"GameFlow: no unlock order, nothing awarded: {e.Message}");
			return new Unlocks(Array.Empty<string>());
		}
	}

	/// <summary>
	/// YOK-92, Y6: the guided apprentice fight is played once. Its story flag is set when its lessons finish or its
	/// duel ends (won or lost); a quit mid-fight leaves it unset, so the next run is guided again.
	/// </summary>
	public void MarkTutorialSeen()
	{
		if (Saved.Story.Has(StoryProgress.ApprenticeTutorial)) return;
		Write(Saved.WithStory(StoryProgress.ApprenticeTutorial));
	}

	private void SaveRun(RunSession session)
	{
		if (Saves is null || session != Session || session == _unsavedSession) return;
		Write(Saved.WithRun(RunSave.Capture(session, _runMapFile ?? MapFile) with { FirstMeeting = _firstMeeting }));
	}

	/// <summary>Replaces the save (and writes it when a file is attached). False when the disk refused; the value is kept either way.</summary>
	public bool Write(SaveData data)
	{
		Saved = data;
		if (Saves is null) return true;
		if (Saves.Save(data))
		{
			_writePending = false;
			_saveTrouble = null;
			return true;
		}
		_writePending = true; // the file is now behind Saved (it may still hold a run that has ended)
		GD.PushWarning($"GameFlow: save {Saves.Path} not written: {Saves.LastSaveError}");
		return false;
	}

	/// <summary>Writes <see cref="Saved"/> again after a refused write, so an ended run does not stay in the file. Not while the store refuses to write at all.</summary>
	private void RetryPendingWrite()
	{
		if (_writePending && Saves is { WriteBlocked: false }) Write(Saved);
	}

	/// <summary>The saved run on this build's data, or null with the reason (no run, or one this build cannot hold).</summary>
	public RunSession? RestoreSavedRun(out string reason)
	{
		reason = _saveTrouble ?? ScreenRouter.NoSaveReason;
		if (Saves is null || Saved.Run is not { } run) return null;
		try
		{
			string dir = ContentPaths.Data("map");
			// The name comes from the save file: only a bare file name inside data/map/ is ever opened.
			if (run.MapFile is { } named && !RunSave.IsMapFileName(named))
				throw new FormatException($"'{named}' is not the name of a map file");
			var graph = new FileMapSource(System.IO.Path.Combine(dir, run.MapFile ?? MapFile)).Load();
			var notes = new System.Collections.Generic.List<string>();
			var session = run.Restore(FightScene.LoadAbilityPool(), graph, NodeSceneTable.Default(dir), notes, allowOffRoute: DevMode.On); // YOK-124: a walk off the routes comes back only in a dev session
			foreach (string note in notes) GD.PushWarning($"GameFlow: saved run: {note}");
			session.Trials = FightScene.LoadTrials(); // YOK-98: the dojo's trials (not saved state: data/trials/)
			reason = "";
			return session;
		}
		// The save file is outside input and this runs inside _Ready: whatever it makes the loaders throw (a
		// missing or unreadable file, a bad walk, an id the data lacks), the run is unrestorable and boot goes on.
		catch (Exception e)
		{
			reason = "The saved run cannot be continued: " + e.Message;
			return null;
		}
	}

	/// <summary>Lights or greys the title's Continue from the save file. Nothing changes without a save file.</summary>
	private void RefreshContinue()
	{
		if (Saves is null) return;
		RetryPendingWrite();
		if (RestoreSavedRun(out string reason) is { } session)
		{
			var map = session.Map;
			int row = map.InNode ? map.CurrentNode!.Row : Math.Min(session.Steps, map.Visited.Count + 1);
			var names = new System.Collections.Generic.List<string>();
			foreach (var (_, s) in session.Run.Owned) names.Add($"{(s.Data.Name.Length > 0 ? s.Data.Name : s.Data.Id)} Lv {s.Level}");
			Title.SetSavedRun(new SavedRunSummary(row, session.Steps, session.Run.Health, RunState.MaxHealth, string.Join("  ·  ", names)));
			Router.Enable(TitleEntry.Continue);
		}
		else
		{
			if (Saved.Run is not null) GD.PushWarning($"GameFlow: {reason}");
			Title.SetSavedRun(null);
			Router.Disable(TitleEntry.Continue, reason);
		}
	}

	/// <summary>
	/// YOK-87: Continue on the title: the saved run comes back on its map; a run saved inside a node goes straight
	/// back into that node, from its start. False (nothing changes) off the title or without a usable saved run.
	/// </summary>
	public bool ContinueRun()
	{
		if (Router.Current != Screen.Title || RestoreSavedRun(out _) is not { } session)
			return false;
		Session = session;
		_freshRun = false;
		_runMapFile = Saved.Run?.MapFile ?? MapFile;
		Saved = ElderMeetings.Resume(Saved, session, out _firstMeeting); // not written here: the next save point writes it
		GD.Print($"GameFlow: continued run on map '{session.Map.Graph.Id}', seed {session.Seed}, health {session.Run.Health}");
		Adopt(session);
		Router.ContinueRun();
		if (session.Map.InNode) Router.EnterNode();
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
				DevMap.Apply(Session); // YOK-124: the dev full map (off unless dev mode is on and the toggle is set)
				Map.SetView(new SessionMapView(Session!)); // a fresh view each time: current node states, pick lock cleared
				Map.Visible = true;
				break;
			case Screen.Title:
				Session = null; // the run stays in the save file as of its last node boundary (YOK-87)
				Router.ChooseScheme(MenuSettings.Scheme); // YOK-125: a dev jump's scheme never outlives it (E18)
				ControlSelect.Close();
				Title.Open();
				RefreshContinue();
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
			fight.FirstMeeting = FirstMeeting; // Y3 (YOK-139): the callout shows on a first meeting only
			fight.PlayerScheme = Router.Scheme; // carried into every duel of the run
			fight.TanukiWins = Saved.TanukiWins; // YOK-99, Y5: the pressure of earlier runs
			fight.TutorialAllowed = !Saved.Story.Has(StoryProgress.ApprenticeTutorial); // YOK-92, Y6: first run only
			fight.TutorialFinished += _ => MarkTutorialSeen();
			fight.TitleRequested += () => Router.ReturnToTitle();
			fight.RunRestartRequested += () => RestartRun();
			ConfigureFight?.Invoke(fight);
			ApplyDevStage(fight); // YOK-125: a dev jump's stage choice
			Fight = fight;
		}
		else if (scene is INodeScene other)
		{
			other.BindRun(Session!, Router.Scheme);
			if (scene is RestStopScreen stop) { stop.TitleRequested += () => Router.ReturnToTitle(); stop.ShopRequested += () => OpenShop(stop); stop.TrialRequested += () => OpenTrial(stop); } // YOK-96: Esc on a rest stop; Continue brings the stop back as entered (M5)
		}
		else GD.PushWarning($"GameFlow: {path} is neither a FightScene nor an INodeScene; it must complete the node through GameFlow.Session itself");
		NodeScene = scene;
		AddChild(scene);
		MoveChild(scene, 0); // under the menu layer
	}

	/// <summary>YOK-104: the merchant's shop over the rest stop: the router says it is open, the stop hides, Back brings it back used.</summary>
	public ShopScreen? Shop { get; private set; }

	private void OpenShop(RestStopScreen stop)
	{
		if (Session == null || !Router.OpenShop()) return;
		var shop = GD.Load<PackedScene>("res://scenes/ui/shop.tscn").Instantiate<ShopScreen>();
		shop.Bind(Session);
		shop.MerchantLine = stop.MerchantLine;
		shop.Closed += () =>
		{
			Router.CloseShop();
			if (Shop == shop) { RemoveChild(shop); shop.QueueFree(); Shop = null; } // not Free: this runs inside the shop's own input
			if (IsInstanceValid(stop)) { stop.Visible = true; stop.ShopClosed(); }
		};
		stop.Visible = false;
		Shop = shop;
		AddChild(shop);
		MoveChild(shop, 1); // over the stop, under the menu layer
	}

	/// <summary>
	/// YOK-98, R7: the dojo's training trial on the fight scene (<see cref="FightScene.TrialStop"/>), over the hidden
	/// rest stop. When the trial ends the fight scene goes and the stop comes back with the result. Nothing is
	/// saved here: a quit mid-trial comes back at the node's entry save, so Continue restarts the node with the
	/// trial's attempts reset (M5).
	/// </summary>
	public FightScene? TrialFight { get; private set; }

	private void OpenTrial(RestStopScreen stop)
	{
		if (Session?.Dojo is not { TrialRunning: true } dojo || TrialFight != null) return;
		var fight = GD.Load<PackedScene>(FightScenePath).Instantiate<FightScene>();
		fight.Session = Session;
		fight.PlayerScheme = Router.Scheme;
		fight.TrialStop = dojo;
		fight.TitleRequested += () => Router.ReturnToTitle();
		fight.TrialFinished += () => CallDeferred(nameof(CloseTrial)); // not inside the fight's own _Process
		ConfigureFight?.Invoke(fight);
		stop.Visible = false;
		TrialFight = fight;
		AddChild(fight);
		MoveChild(fight, 1); // over the stop, under the menu layer
	}

	private void CloseTrial()
	{
		if (TrialFight is { } fight && IsInstanceValid(fight)) { RemoveChild(fight); fight.QueueFree(); }
		TrialFight = null;
		if (NodeScene is RestStopScreen stop && IsInstanceValid(stop)) { stop.Visible = true; stop.TrialClosed(); }
	}

	private void EndFight()
	{
		if (TrialFight is { } trial && IsInstanceValid(trial)) { RemoveChild(trial); trial.Free(); }
		TrialFight = null;
		if (Shop is { } shop && IsInstanceValid(shop)) { RemoveChild(shop); shop.Free(); }
		Shop = null;
		GetTree().Paused = false; // the pause menu pauses the tree; the title must run
		if (NodeScene is { } scene && IsInstanceValid(scene)) { RemoveChild(scene); scene.QueueFree(); }
		Fight = null;
		NodeScene = null;
	}
}
