using System;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Ui;

public enum MockScreen { Title, TitleNoSave, Pause, PauseConfirm, MoveList, MoveListFrames, MoveListKata, Sound, Controls }

/// <summary>
/// YOK-58 MOCKUP host (<c>scenes/ui/menu_mockups.tscn</c>): opens the title and pause menus on their own, without touching
/// the boot scene (YOK-48 still boots <c>fight.tscn</c>). Open it with
/// <c>godot --path game res://scenes/ui/menu_mockups.tscn</c> (or select it in the editor and press F6).
/// Keys 1-9 jump between the screens, H hides the key strip. The pause screens sit over a live fight (Ryo at 720 / 1000,
/// the run's starters plus Foxfire) that the menu freezes by pausing the tree; the title screens sit over the same stage.
/// Start / Continue / Practice / Quit only show a note, because run and save logic are not part of this ticket.
/// </summary>
public partial class MenuMockups : Node
{
	/// <summary>Instance the live fight behind the menus (tests turn it off).</summary>
	public bool WithFight { get; set; } = true;

	public TitleMenu Title { get; private set; } = null!;
	public PauseMenu Pause { get; private set; } = null!;
	public MockScreen Current { get; private set; }
	public RunState Run { get; private set; } = null!;

	private FightScene? _fight;
	private RunState? _previousRun;
	private Control _strip = null!;
	private Label _toast = null!;
	private double _toastLeft;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		var pool = FightScene.LoadAbilityPool();
		Run = RunState.NewRun(pool);
		Run.LevelUp(SpecialSlot.B);                            // Rising Talisman Lv 2
		if (pool.Special("foxfire") is { } fox) Run.Equip(fox); // a drafted special in slot C
		Run.SetHealth(720);

		if (WithFight)
		{
			_previousRun = FightScene.Run;
			FightScene.Run = Run;
			_fight = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
			_fight.StartScreenOverride = false;
			_fight.ProcessMode = ProcessModeEnum.Pausable; // the host runs while paused (keys); the fight must not
			AddChild(_fight);
		}

		var layer = new CanvasLayer { Layer = 30, ProcessMode = ProcessModeEnum.Always };
		AddChild(layer);
		Title = GD.Load<PackedScene>("res://scenes/ui/title_menu.tscn").Instantiate<TitleMenu>();
		Pause = GD.Load<PackedScene>("res://scenes/ui/pause_menu.tscn").Instantiate<PauseMenu>();
		layer.AddChild(Title); layer.AddChild(Pause);
		Title.ProcessMode = ProcessModeEnum.Always;
		Title.StartRequested += () => Say("Start: would open Control select, then a new run (not wired in this mockup)");
		Title.ContinueRequested += () => Say("Continue: would resume the saved run (not wired)");
		Title.PracticeRequested += () => Say("Practice: would open the dojo scene (not wired)");
		Title.QuitRequested += () => Say("Quit: would close the game (not wired)");
		Pause.Bind(Run, 3, 8, "Kitsune", ContentPaths.Data("moves"));
		Pause.ResumeRequested += () => Say("Resume: fight continues");
		Pause.RestartRequested += () => Say("Restart run: would start a new run (not wired)");
		Pause.QuitToTitleRequested += () => Show(MockScreen.Title);

		_strip = new PanelContainer { Position = new Vector2(12, 1040), MouseFilter = Control.MouseFilterEnum.Ignore };
		var strip = new Label { Text = "MOCKUP   1 title   2 title, no saved run   3 pause   4 restart confirm   5 move list   6 + frame data   7 Kata inputs   8 sound   9 controls   H hide", MouseFilter = Control.MouseFilterEnum.Ignore };
		strip.AddThemeFontSizeOverride("font_size", 20);
		strip.AddThemeColorOverride("font_color", FightHud.Paper);
		_strip.AddChild(strip);
		layer.AddChild(_strip);
		_toast = new Label { Position = new Vector2(0, 8), Size = new Vector2(1920, 40), HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
		_toast.AddThemeFontSizeOverride("font_size", 26);
		_toast.AddThemeColorOverride("font_color", FightHud.Paper);
		_toast.AddThemeColorOverride("font_outline_color", FightHud.Ink);
		_toast.AddThemeConstantOverride("outline_size", 8);
		layer.AddChild(_toast);
		MenuSettings.Reset();
		Show(MockScreen.Title);
	}

	public override void _ExitTree()
	{
		if (WithFight) FightScene.Run = _previousRun;
		if (IsInsideTree()) GetTree().Paused = false;
	}

	public bool StripVisible { get => _strip.Visible; set => _strip.Visible = value; }

	public void Show(MockScreen screen)
	{
		Current = screen;
		Pause.Close(); Title.Visible = false;
		GetTree().Paused = true; // title screens freeze the stage behind them too
		MenuSettings.SetScheme(screen == MockScreen.MoveListKata ? ControlScheme.Kata : ControlScheme.Kihon);
		switch (screen)
		{
			case MockScreen.Title: case MockScreen.TitleNoSave:
				Title.Visible = true;
				Title.SetSavedRun(screen == MockScreen.Title ? new SavedRunSummary(4, 8, 720, 1000, "Spirit Wave Lv 1  ·  Rising Talisman Lv 2  ·  Foxfire Lv 1") : null);
				Title.StartButton.GrabFocus();
				break;
			case MockScreen.Pause: Pause.Open(); break;
			case MockScreen.PauseConfirm: Pause.Open(); Pause.RestartButton.EmitSignal(BaseButton.SignalName.Pressed); break;
			case MockScreen.MoveList: case MockScreen.MoveListFrames: case MockScreen.MoveListKata:
				Pause.Open(); Pause.MoveList.Visible = true;
				if (screen == MockScreen.MoveListFrames && !Pause.MoveList.FrameDataVisible) Pause.MoveList.ToggleFrameData();
				if (screen != MockScreen.MoveListFrames && Pause.MoveList.FrameDataVisible) Pause.MoveList.ToggleFrameData();
				break;
			case MockScreen.Sound: case MockScreen.Controls:
				Pause.Open(); Pause.Settings.Visible = true;
				Pause.Settings.ShowTab(screen == MockScreen.Sound ? SettingsTab.Sound : SettingsTab.Controls);
				break;
		}
	}

	private void Say(string text) { _toast.Text = text; _toast.Visible = true; _toastLeft = 2.5; }

	public override void _Process(double delta)
	{
		if (_toastLeft > 0 && (_toastLeft -= delta) <= 0) _toast.Visible = false;
	}

	public override void _UnhandledInput(InputEvent e)
	{
		if (e is not InputEventKey { Pressed: true, Echo: false } k) return;
		if (k.Keycode == Key.H) { _strip.Visible = !_strip.Visible; return; }
		int n = (int)(k.Keycode - Key.Key1);
		if (n >= 0 && n < Enum.GetValues<MockScreen>().Length) Show((MockScreen)n);
	}
}
