using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using YokaiFighters.Sim;
using YokaiFighters.Ui;
using YokaiFighters.Ui.Kit;

namespace YokaiFighters.Fight;

/// <summary>
/// YOK-39 start screen, now the boot flow's control-select step (title → this → fight; <see cref="Flow.GameFlow"/>
/// instances it, the fight scene no longer does): title, subtitle, Kata / Kihon choice, Start Game and the chosen
/// scheme's controls, keyboard and pad side by side. The choice is <see cref="MenuSettings.Scheme"/> (shared with the
/// settings sheet); Left / Right or a click picks, Q / pad X flips it, Esc / pad B raises <see cref="BackRequested"/>.
/// Two wide scheme cards follow control-select.png; the live controls tables are behind a reference toggle. The layout, fonts and fixed copy live in
/// <c>scenes/ui/start_screen.tscn</c>; this script renders the controls table into the scene's two grids and
/// handles Start. Start = Enter / Space / pad A (the focused button) or a click; it raises <see cref="Started"/> once.
/// Keyboard text is read from <see cref="InputDevices.P1Keys"/>; the pad table <see cref="PadMap"/> is pinned to
/// <see cref="InputDevices.FromPad"/> by a test, so neither can drift.
/// </summary>
public partial class StartScreen : Control
{
	public event Action? Started;
	/// <summary>Esc / pad B: back to the title.</summary>
	public event Action? BackRequested;

	public Button KataButton { get; private set; } = null!;
	public Button KihonButton { get; private set; } = null!;
	/// <summary>The scheme the fight will start with (the settings sheet's value).</summary>
	public static ControlScheme Scheme => MenuSettings.Scheme;
	private Label _specialsLine = null!, _normalNote = null!;
	private string _kihonSpecials = "";
	private Control _reference = null!;
	private Button _referenceToggle = null!;
	public bool ReferenceVisible => _reference.Visible;
	public const string KataDescription = "Motions: +10% precision bonus on motion-input specials.";

	public Button Start { get; private set; } = null!;
	private Control? _debugKeys;
	private bool _showDebugKeys = OS.IsDebugBuild();
	/// <summary>Show the debug-keys footer (debug builds; tests can force it).</summary>
	public bool ShowDebugKeys { get => _showDebugKeys; set { _showDebugKeys = value; if (_debugKeys != null) _debugKeys.Visible = value; } }

	/// <summary>Pad bindings of <see cref="InputDevices.FromPad"/> for the six attack buttons and Special (Xbox names).</summary>
	public static readonly (InputBits Bit, string Label)[] PadMap =
	{
		(InputBits.LightPunch, "X"), (InputBits.MediumPunch, "Y"), (InputBits.HeavyPunch, "RB"),
		(InputBits.LightKick, "A"), (InputBits.MediumKick, "B"), (InputBits.HeavyKick, "RT"),
		(InputBits.Special, "LB"),
	};

	public static string PadLabel(InputBits bit)
	{
		foreach (var (b, l) in PadMap) if (b == bit) return l;
		throw new ArgumentException($"no pad label for {bit}");
	}

	public static string KeyLabel(Key k) => OS.GetKeycodeString(k);

	/// <summary>One row: what it does, the keyboard text, the pad text.</summary>
	public readonly record struct Row(string Action, string Keyboard, string Pad);

	/// <summary>The controls outline for the boot screen (Kihon is the default scheme, E18).</summary>
	public static List<Row> Rows() => Rows(ControlScheme.Kihon);

	/// <summary>
	/// The controls outline, built from the live bindings. The Special and EX rows follow the scheme: Kata draws the motion
	/// (K1) and EX is the motion + two punches or two kicks (E16); Kihon is Special + direction (K2) and EX adds one punch or
	/// kick (E18). The Throw row also names the break (E12, GDD 3.2: break by pressing throw).
	/// </summary>
	public static List<Row> Rows(ControlScheme scheme)
	{
		bool kata = scheme == ControlScheme.Kata;
		var k = InputDevices.P1Keys;
		static string K(Key key) => KeyLabel(key);
		static string P(InputBits b) => PadLabel(b);
		static string Chord(string sep, params InputBits[] bits) => string.Join(sep, Array.ConvertAll(bits, P));
		return new List<Row>
		{
			new("Move", $"{K(k.Left)} / {K(k.Right)}", "D-pad / stick"),
			new("Crouch / Jump", $"{K(k.Down)} / {K(k.Up)}", "Down / Up"),
			new("Dash", $"double-tap {K(k.Left)} / {K(k.Right)}", "double-tap left / right"),
			new("Punch  L M H", $"{K(k.LP)}  {K(k.MP)}  {K(k.HP)}", Chord("  ", InputBits.LightPunch, InputBits.MediumPunch, InputBits.HeavyPunch)),
			new("Kick  L M H", $"{K(k.LK)}  {K(k.MK)}  {K(k.HK)}", Chord("  ", InputBits.LightKick, InputBits.MediumKick, InputBits.HeavyKick)),
			kata
				? new("Special (A-D)", "draw the motion", "draw the motion")
				: new("Special", $"{K(k.Special)} + direction", $"{P(InputBits.Special)} + direction"),
			kata
				? new("EX (1 bar)", "motion + 2 punches or 2 kicks", "motion + 2 punches or 2 kicks")
				: new("EX (1 bar)", $"{K(k.Special)} + direction + 1 button", $"{P(InputBits.Special)} + direction + 1 button"),
			new("Throw / break it", $"{K(k.LP)} + {K(k.LK)}", Chord(" + ", InputBits.LightPunch, InputBits.LightKick)),
			new("Burst (when hit)", $"{K(k.LP)} + {K(k.MP)} + {K(k.HP)}", Chord(" + ", InputBits.LightPunch, InputBits.MediumPunch, InputBits.HeavyPunch)),
			new("Block", "hold back (crouch for lows)", "hold back (down for lows)"),
		};
	}

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Stop;
		_debugKeys = UiFind.Get<Control>(this, "DebugKeys");
		_debugKeys.Visible = _showDebugKeys;
		MenuInput.EnsurePadConfirm();
		_specialsLine = UiFind.Get<Label>(this, "SpecialsLine");
		_normalNote = UiFind.Get<Label>(this, "NormalNote");
		_kihonSpecials = _specialsLine.Text; // the scene's copy is the Kihon line
		KataButton = UiFind.Get<Button>(this, "Kata");
		KihonButton = UiFind.Get<Button>(this, "Kihon");
		KataButton.Pressed += () => MenuSettings.SetScheme(ControlScheme.Kata);
		KihonButton.Pressed += () => MenuSettings.SetScheme(ControlScheme.Kihon);
		Start = UiFind.Get<Button>(this, "Start");
		Start.Pressed += OnStart;
		_reference = UiFind.Get<Control>(this, "Reference");
		_referenceToggle = UiFind.Get<Button>(this, "ReferenceToggle");
		_referenceToggle.Pressed += () => SetReferenceVisible(!ReferenceVisible);
		UiFind.Get<Label>(this, "KataDescription").Text = KataDescription;
		UiFind.Get<KeyChipFooter>(this, "Footer").SetHints(
			new KeyHint("scheme", "Scheme"), new KeyHint("back", "Back"), new KeyHint("confirm", "Select"));
		// Retain left/right scheme navigation; vertical/Tab navigation also reaches the reference toggle.
		MenuInput.Ring(KataButton, Start, KihonButton, _referenceToggle);
		MenuInput.Neighbours(KataButton, _referenceToggle, KihonButton, KihonButton, Start);
		MenuInput.Neighbours(KihonButton, KataButton, Start, Start, KataButton);
		MenuInput.Neighbours(Start, KihonButton, _referenceToggle, KataButton, KihonButton);
		MenuInput.Neighbours(_referenceToggle, Start, KataButton, Start, KataButton);
		MenuSettings.Changed += ShowScheme;
		ShowScheme();
		if (IsVisibleInTree()) Start.GrabFocus();
	}

	public override void _ExitTree() => MenuSettings.Changed -= ShowScheme;

	/// <summary>Marks the chosen scheme and redraws the controls tables and the specials line for it.</summary>
	private void ShowScheme()
	{
		bool kata = Scheme == ControlScheme.Kata;
		KataButton.Text = (kata ? "● " : "○ ") + "Kata";
		KihonButton.Text = (kata ? "○ " : "● ") + "Kihon";
		KataButton.GetNode<Control>("Selected").Visible = kata;
		KihonButton.GetNode<Control>("Selected").Visible = !kata;
		var rows = Rows(Scheme);
		FillGrid(UiFind.Get<GridContainer>(this, "KeyboardGrid"), rows.Select(r => (r.Action, r.Keyboard)).ToList());
		FillGrid(UiFind.Get<GridContainer>(this, "PadGrid"), rows.Select(r => (r.Action, r.Pad)).ToList());
		_specialsLine.Text = kata ? KataDescription : _kihonSpecials;
		_normalNote.Visible = !kata; // "an attack before Special" is a Kihon note
	}

	public override void _UnhandledInput(InputEvent e)
	{
		if (!IsVisibleInTree()) return;
		if (MenuInput.Back(e)) { GetViewport().SetInputAsHandled(); BackRequested?.Invoke(); }
		else if (MenuInput.Scheme(e))
		{
			GetViewport().SetInputAsHandled();
			MenuSettings.SetScheme(Scheme == ControlScheme.Kata ? ControlScheme.Kihon : ControlScheme.Kata);
		}
	}

	/// <summary>Writes the rows into the grid's Label pairs (action, binding), copying the last pair if the scene has fewer than needed.</summary>
	private static void FillGrid(GridContainer grid, List<(string Action, string Binding)> rows)
	{
		var labels = grid.GetChildren().OfType<Label>().ToList();
		while (labels.Count < rows.Count * 2 && labels.Count >= 2)
			foreach (Label src in new[] { labels[^2], labels[^1] })
			{
				var copy = (Label)src.Duplicate();
				grid.AddChild(copy);
				labels.Add(copy);
			}
		for (int i = 0; i < labels.Count; i += 2)
		{
			bool used = i / 2 < rows.Count;
			labels[i].Visible = used;
			if (i + 1 < labels.Count) labels[i + 1].Visible = used;
			if (!used) continue;
			labels[i].Text = rows[i / 2].Action;
			labels[i + 1].Text = rows[i / 2].Binding;
		}
	}

	public void SetReferenceVisible(bool visible)
	{
		_reference.Visible = visible;
		KataButton.Visible = KihonButton.Visible = !visible;
		_referenceToggle.Text = visible ? "Hide controls" : "Controls reference";
		_referenceToggle.GrabFocus();
	}

	public void Open() { Visible = true; SetReferenceVisible(false); Start.GrabFocus(); }

	public void Close() => Visible = false;

	/// <summary>Raises <see cref="Started"/> and hides the screen (once per opening).</summary>
	public void OnStart()
	{
		if (!Visible) return;
		Visible = false;
		Started?.Invoke();
	}
}
