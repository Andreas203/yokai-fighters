using System;
using System.Linq;
using System.Collections.Generic;
using Godot;
using YokaiFighters.Flow;

namespace YokaiFighters.Ui;

/// <summary>What the title screen knows about a saved run (mock data here; the save system fills it later).</summary>
public sealed record SavedRunSummary(int Row, int Rows, int Health, int MaxHealth, string Specials);

/// <summary>
/// YOK-58 MOCKUP title menu (<c>scenes/ui/title_menu.tscn</c>): Start (new run), Continue (greyed and skipped by
/// focus navigation while there is no saved run), Practice, Settings, Quit. Up/Down/stick move focus and wrap,
/// Enter / Space / pad A confirm, mouse works. Three hanging yokai masks (Kitsune, Oni, Kappa; never the Tanuki) sway, and the focused entry lights one. A card on the right describes the focused entry (and the saved run
/// for Continue). Raises one event per entry; <see cref="GameFlow"/> routes them at boot (the mockup host just logs them).
/// An entry with no backing system yet is switched off with <see cref="SetEntry"/>: greyed, skipped by focus, its reason
/// in the tooltip and on the detail card while the mouse is over it (<see cref="DisabledReason"/>).
/// M4: no post-game mode is offered here; the story flag is separate from the run.
/// </summary>
public partial class TitleMenu : Control
{
	public event Action? StartRequested, ContinueRequested, PracticeRequested, SettingsRequested, QuitRequested;
	/// <summary>Back out of the settings sheet. With no listener the menu closes the sheet itself (standalone / mockup);
	/// the same goes for <see cref="SettingsRequested"/> and opening it. A router that listens opens and closes it.</summary>
	public event Action? SettingsBackRequested;

	public Button StartButton { get; private set; } = null!;
	public Button ContinueButton { get; private set; } = null!;
	public Button PracticeButton { get; private set; } = null!;
	public Button SettingsButton { get; private set; } = null!;
	public Button QuitButton { get; private set; } = null!;
	public Label DetailHeadingLabel { get; private set; } = null!;
	public Label DetailBodyLabel { get; private set; } = null!;

	private Label _extra = null!;
	private YokaiMask? _kitsune, _oni, _kappa;
	private Button[] _all = null!;
	private SavedRunSummary? _saved;
	private SettingsPanel? _settings;
	private readonly FocusLock _focusLock = new();
	private readonly Dictionary<TitleEntry, string> _blocked = new();
	private Button? _hovered;

	public bool HasSavedRun => _saved != null;
	public SettingsPanel? Settings => _settings;

	public override void _Ready()
	{
		MenuInput.EnsurePadConfirm();
		StartButton = UiFind.Get<Button>(this, "Start");
		ContinueButton = UiFind.Get<Button>(this, "Continue");
		PracticeButton = UiFind.Get<Button>(this, "Practice");
		SettingsButton = UiFind.Get<Button>(this, "Settings");
		QuitButton = UiFind.Get<Button>(this, "Quit");
		DetailHeadingLabel = UiFind.Get<Label>(this, "DetailHeading");
		DetailBodyLabel = UiFind.Get<Label>(this, "DetailBody");
		_extra = UiFind.Get<Label>(this, "DetailExtra");
		_kitsune = UiFind.Get<YokaiMask>(this, "MaskKitsune");
		_oni = UiFind.Get<YokaiMask>(this, "MaskOni");
		_kappa = UiFind.Get<YokaiMask>(this, "MaskKappa");
		_all = new[] { StartButton, ContinueButton, PracticeButton, SettingsButton, QuitButton };
		StartButton.Pressed += () => StartRequested?.Invoke();
		ContinueButton.Pressed += () => ContinueRequested?.Invoke();
		PracticeButton.Pressed += () => PracticeRequested?.Invoke();
		SettingsButton.Pressed += () => { if (SettingsRequested != null) SettingsRequested.Invoke(); else OpenSettings(); };
		QuitButton.Pressed += () => QuitRequested?.Invoke();
		foreach (var b in _all)
		{
			b.FocusEntered += Describe;
			b.MouseEntered += () =>
			{
				if (!b.Disabled && b.FocusMode != FocusModeEnum.None) b.GrabFocus();
				else if (b.Disabled) { _hovered = b; Describe(); } // a greyed entry says why on the detail card
			};
			b.MouseExited += () => { if (_hovered == b) { _hovered = null; Describe(); } };
		}
		// Wrap focus top <-> bottom. Disabled buttons are skipped by Godot's focus search, so Continue greys out cleanly.
		MenuInput.WrapVertical(_all); // explicit chain through every entry: greyed Continue is passed over, no layout geometry involved
		_settings = GD.Load<PackedScene>("res://scenes/ui/settings_panel.tscn").Instantiate<SettingsPanel>();
		AddChild(_settings);
		_settings.BackRequested += () => { if (SettingsBackRequested != null) SettingsBackRequested.Invoke(); else CloseSettings(); };
		SetSavedRun(_saved);
		StartButton.GrabFocus();
	}

	/// <summary>Null = no saved run: Continue is greyed and cannot be focused or pressed.</summary>
	public void SetSavedRun(SavedRunSummary? saved)
	{
		_saved = saved;
		Refresh();
	}

	/// <summary>Switches an entry on or off; off needs the reason shown to the player. Continue also needs a saved run.</summary>
	public void SetEntry(TitleEntryState state)
	{
		if (state.Enabled) _blocked.Remove(state.Entry); else _blocked[state.Entry] = state.Reason;
		Refresh();
	}

	/// <summary>Why the entry cannot be chosen right now; "" when it can.</summary>
	public string DisabledReason(TitleEntry entry) =>
		_blocked.TryGetValue(entry, out string? why) ? why
		: entry == TitleEntry.Continue && _saved == null ? "No saved run yet." : "";

	public Button ButtonOf(TitleEntry entry) => entry switch
	{
		TitleEntry.Start => StartButton, TitleEntry.Continue => ContinueButton, TitleEntry.Practice => PracticeButton,
		TitleEntry.Settings => SettingsButton, _ => QuitButton,
	};

	private void Refresh()
	{
		if (_all == null) return;
		bool locked = _focusLock.Active;
		if (locked) _focusLock.Release(); // settings is open over us: set the real mode, then lock again
		Button? focused = _all.FirstOrDefault(b => b.HasFocus()); // read first: turning focus off drops it
		foreach (TitleEntry e in Enum.GetValues<TitleEntry>())
		{
			Button b = ButtonOf(e);
			string why = DisabledReason(e);
			b.Disabled = why != "";
			b.FocusMode = b.Disabled ? FocusModeEnum.None : FocusModeEnum.All;
			b.TooltipText = why;
		}
		if (focused is { Disabled: true }) _all.FirstOrDefault(b => !b.Disabled)?.GrabFocus(); // never leave focus on a greyed entry
		if (locked) _focusLock.Engage(_all);
		Describe();
	}

	/// <summary>Shows the menu (again) with focus on the first entry that can be chosen.</summary>
	public void Open()
	{
		Visible = true;
		_hovered = null;
		if (_settings?.Visible != true) _all.FirstOrDefault(b => !b.Disabled)?.GrabFocus();
		Describe();
	}

	// The settings sheet is modal for focus: the five title buttons cannot be reached until it closes; Settings gets focus back.
	public void OpenSettings() { _focusLock.Engage(_all); _settings!.Visible = true; }
	public void CloseSettings() { _settings!.Visible = false; _focusLock.Release(); SettingsButton.GrabFocus(); }

	private void Describe()
	{
		if (_all == null) return;
		Button? f = _hovered ?? _all.FirstOrDefault(b => b.HasFocus());
		(string head, string body, string extra) = f switch
		{
			_ when f == ContinueButton => _saved == null
				? ("CONTINUE", "No saved run yet.", _blocked.TryGetValue(TitleEntry.Continue, out string? why) ? why : "Start a new run and your progress is kept here.")
				: ("CONTINUE", $"Row {_saved.Row} of {_saved.Rows}.", $"Ryo  {_saved.Health} / {_saved.MaxHealth}\n{_saved.Specials}"),
			_ when f == PracticeButton => ("PRACTICE", "Train in the dojo against a dummy. No run, no risk.", DisabledReason(TitleEntry.Practice)),
			_ when f == SettingsButton => ("SETTINGS", "Sound levels, Kata or Kihon, and the button layout.", ""),
			_ when f == QuitButton => ("QUIT", "Leave the game.", ""),
			_ => ("NEW RUN", "Begin a new run: eight duels, one Tanuki.", "Choose Kata or Kihon first."),
		};
		// The focused entry lights one mask: Start = Kitsune, Continue = Oni, Practice = Kappa; Settings / Quit light none.
		if (_kitsune != null)
		{
			_kitsune.Lit = f == StartButton || f == null;
			_oni!.Lit = f == ContinueButton;
			_kappa!.Lit = f == PracticeButton;
		}
		DetailHeadingLabel.Text = head; DetailBodyLabel.Text = body; _extra.Text = extra;
	}

	public override void _UnhandledInput(InputEvent e)
	{
		if (!Visible || _settings?.Visible == true) return;
		if (MenuInput.Back(e) && !QuitButton.HasFocus()) { QuitButton.GrabFocus(); GetViewport().SetInputAsHandled(); }
	}
}
