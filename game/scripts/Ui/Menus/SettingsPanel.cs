using System;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Ui;

public enum SettingsTab { Sound, Controls }

/// <summary>
/// YOK-58 MOCKUP settings sheet (<c>scenes/ui/settings_panel.tscn</c>), opened from the title and the pause menu.
/// SOUND: master, music and effects sliders (Left/Right on focus, steps of 5), a mute toggle and a Preview cue per
/// slider (placeholder synthesised sound). CONTROLS: Kata / Kihon toggle with its one-line explanation (K1, K2;
/// P4: Kihon is easier, not stronger) and read-only keyboard and pad bindings built from the live layouts
/// (<see cref="StartScreen.Rows"/>). Values live in <see cref="MenuSettings"/>; nothing is saved yet.
/// </summary>
public partial class SettingsPanel : Control
{
	public event Action? BackRequested;

	/// <summary>K1: motion specials get the precision bonus. K2: Kihon is 100% damage. One line each (shown under the toggle).</summary>
	public const string KataLine = "Kata: draw a motion for each special. Motion specials get +10% damage (precision).";
	public const string KihonLine = "Kihon: Special + a direction picks slot A-D. Easier to learn, 100% damage.";

	public SettingsTab Tab { get; private set; } = SettingsTab.Sound;

	private Control _soundPage = null!, _controlsPage = null!;
	private Button _tabSound = null!, _tabControls = null!, _mute = null!, _kata = null!, _kihon = null!;
	private readonly InkSlider[] _sliders = new InkSlider[3];
	private readonly Label[] _values = new Label[3];
	private readonly Button[] _previews = new Button[3];
	private readonly AudioStreamPlayer[] _players = new AudioStreamPlayer[3];
	private Label _schemeLine = null!;
	private static readonly string[] Names = { "Master", "Music", "Sfx" };
	private bool _syncing;

	public InkSlider SliderOf(int i) => _sliders[i];
	public Button MuteButton => _mute;
	public Label SchemeLineLabel => _schemeLine;
	public Button KataButton => _kata;
	public Button KihonButton => _kihon;
	public Button PreviewOf(int i) => _previews[i];
	public AudioStreamPlayer PlayerOf(int i) => _players[i];

	public override void _Ready()
	{
		_soundPage = UiFind.Get<Control>(this, "SoundPage");
		_controlsPage = UiFind.Get<Control>(this, "ControlsPage");
		_tabSound = UiFind.Get<Button>(this, "TabSound");
		_tabControls = UiFind.Get<Button>(this, "TabControls");
		_mute = UiFind.Get<Button>(this, "Mute");
		_kata = UiFind.Get<Button>(this, "Kata");
		_kihon = UiFind.Get<Button>(this, "Kihon");
		_schemeLine = UiFind.Get<Label>(this, "SchemeLine");
		_tabSound.Pressed += () => ShowTab(SettingsTab.Sound);
		_tabControls.Pressed += () => ShowTab(SettingsTab.Controls);
		_mute.Pressed += () => MenuSettings.SetMuted(!MenuSettings.Muted);
		_kata.Pressed += () => MenuSettings.SetScheme(ControlScheme.Kata);
		_kihon.Pressed += () => MenuSettings.SetScheme(ControlScheme.Kihon);

		var (musicBus, sfxBus) = MenuSettings.EnsureBuses();
		string[] buses = { "Master", MenuSettings.MusicBus, MenuSettings.SfxBus };
		var cues = new[] { MenuSettings.Cue.Bell, MenuSettings.Cue.Pad, MenuSettings.Cue.Hit };
		for (int i = 0; i < 3; i++)
		{
			int idx = i;
			_sliders[i] = UiFind.Get<InkSlider>(this, Names[i] + "Slider");
			_values[i] = UiFind.Get<Label>(this, Names[i] + "Value");
			_previews[i] = UiFind.Get<Button>(this, Names[i] + "Preview");
			_players[i] = new AudioStreamPlayer { Bus = buses[i], Stream = MenuSettings.MakeCue(cues[i]) };
			AddChild(_players[i]);
			_sliders[i].ValueChanged += v => { if (_syncing) return; SetLevel(idx, (int)v); };
			_previews[i].Pressed += () => _players[idx].Play();
		}
		_ = musicBus; _ = sfxBus;
		MenuSettings.Changed += Refresh;
		TreeExiting += () => MenuSettings.Changed -= Refresh;
		FillBindings();
		ShowTab(SettingsTab.Sound);
		Visible = false;
		VisibilityChanged += () => { if (Visible) FocusFirst(); };
	}

	private static void SetLevel(int i, int v)
	{
		switch (i) { case 0: MenuSettings.SetMaster(v); break; case 1: MenuSettings.SetMusic(v); break; default: MenuSettings.SetSfx(v); break; }
	}

	public void ShowTab(SettingsTab tab)
	{
		Tab = tab;
		_soundPage.Visible = tab == SettingsTab.Sound;
		_controlsPage.Visible = tab == SettingsTab.Controls;
		_tabSound.ThemeTypeVariation = tab == SettingsTab.Sound ? "ToggleButtonOn" : "ToggleButton";
		_tabControls.ThemeTypeVariation = tab == SettingsTab.Controls ? "ToggleButtonOn" : "ToggleButton";
		Refresh();
		if (Visible) FocusFirst();
	}

	private void FocusFirst()
	{
		if (Tab == SettingsTab.Sound) _sliders[0].GrabFocus(); else (MenuSettings.Scheme == ControlScheme.Kata ? _kata : _kihon).GrabFocus();
	}

	private void Refresh()
	{
		if (_mute == null) return;
		_syncing = true;
		int[] vals = { MenuSettings.Master, MenuSettings.Music, MenuSettings.Sfx };
		for (int i = 0; i < 3; i++)
		{
			_sliders[i].Value = vals[i];
			_values[i].Text = MenuSettings.Muted ? "mute" : vals[i].ToString();
		}
		_syncing = false;
		_mute.Text = MenuSettings.Muted ? "Mute all: ON" : "Mute all: off";
		_mute.ThemeTypeVariation = MenuSettings.Muted ? "ToggleButtonOn" : "ToggleButton";
		bool kata = MenuSettings.Scheme == ControlScheme.Kata;
		_kata.ThemeTypeVariation = kata ? "ToggleButtonOn" : "ToggleButton";
		_kihon.ThemeTypeVariation = kata ? "ToggleButton" : "ToggleButtonOn";
		_schemeLine.Text = kata ? KataLine : KihonLine;
	}

	/// <summary>Keyboard and pad tables from the live bindings, the same rows as the boot start screen.</summary>
	private void FillBindings()
	{
		var rows = StartScreen.Rows();
		Fill(UiFind.Get<GridContainer>(this, "KeyboardGrid"), rows.Select(r => (r.Action, r.Keyboard)).ToList());
		Fill(UiFind.Get<GridContainer>(this, "PadGrid"), rows.Select(r => (r.Action, r.Pad)).ToList());
		static void Fill(GridContainer grid, System.Collections.Generic.List<(string Action, string Binding)> rows)
		{
			foreach (Node c in grid.GetChildren()) c.QueueFree();
			foreach (var (a, b) in rows)
			{
				var la = new Label { Text = a, ThemeTypeVariation = "PineLabel", CustomMinimumSize = new Vector2(215, 34), ClipText = true };
				var lb = new Label { Text = b, CustomMinimumSize = new Vector2(355, 34), ClipText = true };
				la.AddThemeFontSizeOverride("font_size", 24); lb.AddThemeFontSizeOverride("font_size", 24);
				grid.AddChild(la); grid.AddChild(lb);
			}
		}
	}

	public override void _UnhandledInput(InputEvent e)
	{
		if (!Visible) return;
		int t = MenuInput.Tab(e);
		if (MenuInput.Back(e)) BackRequested?.Invoke();
		else if (t != 0) ShowTab(t > 0 ? SettingsTab.Controls : SettingsTab.Sound);
		else return;
		GetViewport().SetInputAsHandled();
	}
}
