using System;
using YokaiFighters.Ui.Kit;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Ui;

public enum SettingsTab { Sound, Controls }

/// <summary>
/// Settings sheet (<c>scenes/ui/settings_panel.tscn</c>), opened from the title and the pause menu.
/// SOUND: master, music and effects sliders (Left/Right on focus, steps of 5), a mute toggle and a Preview cue per
/// slider (placeholder synthesised sound). CONTROLS: Kata / Kihon toggle with its one-line explanation (K1, K2;
/// P4: Kihon is easier, not stronger) and read-only keyboard and pad bindings built from the live layouts
/// (<see cref="StartScreen.Rows"/>). Values live in <see cref="MenuSettings"/>, which saves each change when the boot
/// flow has attached the player's settings file; the sheet is the same wherever it is opened from.
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
	private Button _back = null!;
 private ScrollContainer _bindings = null!;
 // The generated plate's torn perimeter stays outside this design-canvas inset.
 public Rect2 SheetSafeArea
 {
  get
  {
   var panel = UiFind.Get<Control>(this, "Panel");
   return panel.GetGlobalTransform() * new Rect2(Vector2.One * 100, panel.Size - Vector2.One * 200);
  }
 }
 private TextureRect _activeTab = null!;
 private ColorRect _rowBand = null!;
 private Label _rowArrow = null!;

	public InkSlider SliderOf(int i) => _sliders[i];
	public Button MuteButton => _mute;
	public Label SchemeLineLabel => _schemeLine;
	public Button KataButton => _kata;
	public Button KihonButton => _kihon;
	public Button PreviewOf(int i) => _previews[i];
	public AudioStreamPlayer PlayerOf(int i) => _players[i];

	public override void _Ready()
	{
		MenuInput.EnsurePadConfirm();
		_back = UiFind.Get<Button>(this, "Back");
		_back.Pressed += () => BackRequested?.Invoke();
		UiFind.Get<KeyChipFooter>(this, "Footer").SetHints(new("choose", "Adjust"), new("tab", "Tab"), new("back", "Back"));
		_soundPage = UiFind.Get<Control>(this, "SoundPage");
		_controlsPage = UiFind.Get<Control>(this, "ControlsPage");
		_tabSound = UiFind.Get<Button>(this, "TabSound");
		_tabControls = UiFind.Get<Button>(this, "TabControls");
		_mute = UiFind.Get<Button>(this, "Mute");
		_kata = UiFind.Get<Button>(this, "Kata");
		_kihon = UiFind.Get<Button>(this, "Kihon");
		_schemeLine = UiFind.Get<Label>(this, "SchemeLine");
		_bindings = UiFind.Get<ScrollContainer>(this, "BindingsScroll");
  _bindings.GuiInput += e => {
   int direction = e.IsActionPressed("ui_down") ? 1 : e.IsActionPressed("ui_up") ? -1 : 0;
   if (direction == 0) return;
   var bar = _bindings.GetVScrollBar();
   if (direction < 0 && _bindings.ScrollVertical == 0) _kihon.GrabFocus();
   else if (direction > 0 && _bindings.ScrollVertical >= bar.MaxValue - bar.Page) _back.GrabFocus();
   else _bindings.ScrollVertical += direction * 60;
   _bindings.AcceptEvent();
  };
  foreach (var tab in new[] { _tabSound, _tabControls })
   foreach (string state in new[] { "normal", "hover", "pressed", "focus" })
    tab.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
  _activeTab = new TextureRect { Texture = GD.Load<Texture2D>("res://assets/generated/ui/red-brush.png"), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, MouseFilter = MouseFilterEnum.Ignore };
  UiFind.Get<Control>(this, "Panel").AddChild(_activeTab);
  _rowBand = new ColorRect { Color = new Color(0.847f, 0.388f, 0.173f, 0.12f), MouseFilter = MouseFilterEnum.Ignore, Visible = false };
  _soundPage.AddChild(_rowBand); _soundPage.MoveChild(_rowBand, 0);
  _rowArrow = new Label { Text = "▶", MouseFilter = MouseFilterEnum.Ignore, Visible = false };
  _rowArrow.AddThemeColorOverride("font_color", FightHud.Seal);
  _soundPage.AddChild(_rowArrow);
  _tabSound.Pressed += () => ShowTab(SettingsTab.Sound);
		_tabControls.Pressed += () => ShowTab(SettingsTab.Controls);
		_mute.Pressed += () => MenuSettings.SetMuted(!MenuSettings.Muted);
		_kata.Pressed += () => MenuSettings.SetScheme(ControlScheme.Kata);
		_kihon.Pressed += () => MenuSettings.SetScheme(ControlScheme.Kihon);

		var (musicBus, sfxBus) = MenuSettings.EnsureBuses();
		string[] buses = { MenuSettings.MasterBus,MenuSettings.MusicBus, MenuSettings.SfxBus };
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
			_sliders[i].FocusEntered += () => HighlightRow(idx);
   _previews[i].FocusEntered += () => HighlightRow(idx);
   _sliders[i].FocusExited += () => { _rowBand.Hide(); _rowArrow.Hide(); };
   _previews[i].FocusExited += () => { _rowBand.Hide(); _rowArrow.Hide(); };
   _previews[i].Pressed += () => _players[idx].Play();
		}
		_ = musicBus; _ = sfxBus;
		MenuSettings.Changed += Refresh;
		TreeExiting += () => MenuSettings.Changed -= Refresh;
		FillBindings();
		WireFocus();
		ShowTab(SettingsTab.Sound);
		Visible = false;
		VisibilityChanged += () => { if (Visible) FocusFirst(); };
	}

	private void HighlightRow(int i)
 {
  _rowBand.Position = new Vector2(0, 10 + i * 120); _rowBand.Size = new Vector2(1360, 86); _rowBand.Show();
  _rowArrow.Position = new Vector2(12, 30 + i * 120); _rowArrow.Show();
 }

 private static void SetLevel(int i, int v)
	{
		switch (i) { case 0: MenuSettings.SetMaster(v); break; case 1: MenuSettings.SetMusic(v); break; default: MenuSettings.SetSfx(v); break; }
	}

	public void ShowTab(SettingsTab tab)
	{
		Tab = tab;
  _activeTab.Position = new Vector2(tab == SettingsTab.Sound ? 100 : 400, 280);
  _activeTab.Size = new Vector2(tab == SettingsTab.Sound ? 260 : 300, 10);
  UiFind.Get<Label>(this, "Hint").Text = tab == SettingsTab.Sound ? "Adjust the volume." : "Focus the bindings, then use Up / Down to scroll.";
		_soundPage.Visible = tab == SettingsTab.Sound;
		_controlsPage.Visible = tab == SettingsTab.Controls;
		_tabSound.ThemeTypeVariation = "GhostButton";
		_tabControls.ThemeTypeVariation = "GhostButton";
		_tabSound.AddThemeColorOverride("font_color", tab == SettingsTab.Sound ? KitBrushButton.StrokeRed : FightHud.Ink);
		_tabControls.AddThemeColorOverride("font_color", tab == SettingsTab.Controls ? KitBrushButton.StrokeRed : FightHud.Ink);
		WireRing();
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
			_values[i].Text = MenuSettings.Muted ? "mute" : vals[i] + "%";
		}
		_syncing = false;
		_mute.Text = MenuSettings.Muted ? "Mute all: ON" : "Mute all: off";
		_mute.ThemeTypeVariation = "GhostButton";
		bool kata = MenuSettings.Scheme == ControlScheme.Kata;
		_kata.ThemeTypeVariation = "GhostButton";
		_kihon.ThemeTypeVariation = "GhostButton";
		_kata.AddThemeColorOverride("font_color", kata ? KitBrushButton.StrokeRed : FightHud.Ink);
		_kihon.AddThemeColorOverride("font_color", kata ? FightHud.Ink : KitBrushButton.StrokeRed);
		_schemeLine.Text = kata ? KataLine : KihonLine;
		if (_shownScheme != MenuSettings.Scheme) FillBindings(); // Special / EX rows are scheme-specific (E16 / E18)
	}

	private ControlScheme? _shownScheme;

	/// <summary>
	/// Explicit focus graph, so the sheet never relies on layout geometry and nothing outside it is reachable: tabs on top, then the
	/// page (Sound: slider rows with Preview to the right, then Mute; Controls: Kata / Kihon). Tab / Shift+Tab cycle the same set
	/// (<see cref="WireRing"/>). A direction with nothing beyond it stays on the control.
	/// </summary>
	private void WireFocus()
	{
		MenuInput.Neighbours(_tabSound, null, _sliders[0], _tabControls, _tabControls);
		MenuInput.Neighbours(_tabControls, null, _kata, _tabSound, _tabSound);
		for (int i = 0; i < 3; i++)
		{
			Control up = i == 0 ? _tabSound : _sliders[i - 1], upP = i == 0 ? _tabControls : _previews[i - 1];
			Control down = i == 2 ? _mute : _sliders[i + 1], downP = i == 2 ? _mute : _previews[i + 1];
			MenuInput.Neighbours(_sliders[i], up, down, null, _previews[i]);
			MenuInput.Neighbours(_previews[i], upP, downP, _sliders[i], null);
		}
		MenuInput.Neighbours(_bindings, _kihon, _back, null, null);
  MenuInput.Neighbours(_mute, _sliders[2], _back, null, _back);
		MenuInput.Neighbours(_kata, _tabControls, _bindings, null, _kihon);
		MenuInput.Neighbours(_kihon, _tabControls, _bindings, _kata, null);
	}

	private void WireRing()
	{
		MenuInput.Neighbours(_back, Tab == SettingsTab.Sound ? _mute : _kihon, _tabSound, null, null);
		Control first = Tab == SettingsTab.Sound ? _sliders[0] : _kata;
		_tabSound.FocusNeighborBottom = _tabSound.GetPathTo(first);
		_tabControls.FocusNeighborBottom = _tabControls.GetPathTo(first);
		if (Tab == SettingsTab.Sound)
			MenuInput.Ring(_tabSound, _tabControls, _sliders[0], _previews[0], _sliders[1], _previews[1], _sliders[2], _previews[2], _mute, _back);
		else
			MenuInput.Ring(_tabSound, _tabControls, _kata, _kihon, _bindings, _back);
	}

	/// <summary>Keyboard and pad tables from the live bindings, the same rows as the boot start screen, with the Special / EX rows for the chosen scheme.</summary>
	private void FillBindings()
	{
		_shownScheme = MenuSettings.Scheme;
		var rows = StartScreen.Rows(MenuSettings.Scheme);
		Fill(UiFind.Get<GridContainer>(this, "KeyboardGrid"), rows.Select(r => (r.Action, r.Keyboard)).ToList());
		Fill(UiFind.Get<GridContainer>(this, "PadGrid"), rows.Select(r => (r.Action, r.Pad)).ToList());
		static void Fill(GridContainer grid, System.Collections.Generic.List<(string Action, string Binding)> rows)
		{
			foreach (Node c in grid.GetChildren()) { grid.RemoveChild(c); c.QueueFree(); }
			foreach (var (a, b) in rows)
			{
				var la = new Label { Text = a, ThemeTypeVariation = "PineLabel", CustomMinimumSize = new Vector2(310, 42) };
				var lb = new Label { Text = b, CustomMinimumSize = new Vector2(900, 42) };
				la.AddThemeFontSizeOverride("font_size", 28); lb.AddThemeFontSizeOverride("font_size", 28);
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
