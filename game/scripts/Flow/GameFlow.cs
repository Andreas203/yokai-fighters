using System;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Settings;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Flow;

/// <summary>
/// The game's boot scene (<c>scenes/boot.tscn</c>, the project's main scene): title → control select → fight, with
/// Settings and Quit routed. All decisions are <see cref="ScreenRouter"/>'s; this node only shows and hides the menu
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

	/// <summary>Leave the fight for the title (the pause menu's "Quit to title"). False when no fight is running.</summary>
	public bool ReturnToTitle() => Router.ReturnToTitle();

	private void OnScreenChanged(Screen from, Screen to)
	{
		if (from == Screen.Settings) Title.CloseSettings();
		if (from == Screen.Fight) EndFight();
		switch (to)
		{
			case Screen.Title:
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

	private void BeginFight()
	{
		var fight = GD.Load<PackedScene>(FightScenePath).Instantiate<FightScene>();
		fight.PlayerScheme = Router.Scheme; // carried into every duel of the run
		fight.TitleRequested += () => Router.ReturnToTitle();
		ConfigureFight?.Invoke(fight);
		Fight = fight;
		AddChild(fight);
		MoveChild(fight, 0); // under the menu layer
	}

	private void EndFight()
	{
		GetTree().Paused = false; // the pause menu pauses the tree; the title must run
		if (Fight is { } fight && IsInstanceValid(fight)) { RemoveChild(fight); fight.QueueFree(); }
		Fight = null;
	}
}
