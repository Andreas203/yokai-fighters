using System;
using System.Linq;
using Godot;

namespace YokaiFighters.Ui;

/// <summary>What the title screen knows about a saved run (mock data here; the save system fills it later).</summary>
public sealed record SavedRunSummary(int Row, int Rows, int Health, int MaxHealth, string Specials);

/// <summary>
/// YOK-58 MOCKUP title menu (<c>scenes/ui/title_menu.tscn</c>): Start (new run), Continue (greyed and skipped by
/// focus navigation while there is no saved run), Practice, Settings, Quit. Up/Down/stick move focus and wrap,
/// Enter / Space / pad A confirm, mouse works. Three hanging yokai masks (Kitsune, Oni, Kappa; never the Tanuki) sway, and the focused entry lights one. A card on the right describes the focused entry (and the saved run
/// for Continue). Raises one event per entry; routing, save and the real boot are other tickets (YOK-48 still boots to fight.tscn).
/// M4: no post-game mode is offered here; the story flag is separate from the run.
/// </summary>
public partial class TitleMenu : Control
{
	public event Action? StartRequested, ContinueRequested, PracticeRequested, SettingsRequested, QuitRequested;

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

	public bool HasSavedRun => _saved != null;
	public SettingsPanel? Settings => _settings;

	public override void _Ready()
	{
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
		SettingsButton.Pressed += OpenSettings;
		QuitButton.Pressed += () => QuitRequested?.Invoke();
		foreach (var b in _all)
		{
			b.FocusEntered += Describe;
			b.MouseEntered += () => { if (!b.Disabled) b.GrabFocus(); };
		}
		// Wrap focus top <-> bottom. Disabled buttons are skipped by Godot's focus search, so Continue greys out cleanly.
		MenuInput.WrapVertical(StartButton, QuitButton);
		_settings = GD.Load<PackedScene>("res://scenes/ui/settings_panel.tscn").Instantiate<SettingsPanel>();
		AddChild(_settings);
		_settings.BackRequested += CloseSettings;
		SetSavedRun(_saved);
		StartButton.GrabFocus();
	}

	/// <summary>Null = no saved run: Continue is greyed and cannot be focused or pressed.</summary>
	public void SetSavedRun(SavedRunSummary? saved)
	{
		_saved = saved;
		if (ContinueButton == null) return;
		ContinueButton.Disabled = saved == null;
		ContinueButton.FocusMode = saved == null ? FocusModeEnum.None : FocusModeEnum.All;
		if (saved == null && ContinueButton.HasFocus()) StartButton.GrabFocus();
		Describe();
	}

	public void OpenSettings() { _settings!.Visible = true; }
	public void CloseSettings() { _settings!.Visible = false; SettingsButton.GrabFocus(); }

	private void Describe()
	{
		if (_all == null) return;
		Button? f = _all.FirstOrDefault(b => b.HasFocus());
		(string head, string body, string extra) = f switch
		{
			_ when f == ContinueButton => _saved == null
				? ("CONTINUE", "No saved run yet.", "Start a new run and your progress is kept here.")
				: ("CONTINUE", $"Row {_saved.Row} of {_saved.Rows}.", $"Ryo  {_saved.Health} / {_saved.MaxHealth}\n{_saved.Specials}"),
			_ when f == PracticeButton => ("PRACTICE", "Train in the dojo against a dummy. No run, no risk.", ""),
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
