using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Fight;

/// <summary>
/// YOK-39 start screen, shown when the game boots (before the first fight): title, subtitle, Start Game and the
/// default Kihon controls, keyboard and pad side by side. The layout, fonts and fixed copy live in
/// <c>scenes/ui/start_screen.tscn</c>; this script renders the controls table into the scene's two grids and
/// handles Start. Start = Enter / Space / pad A (the focused button) or a click; it raises <see cref="Started"/> once.
/// Keyboard text is read from <see cref="InputDevices.P1Keys"/>; the pad table <see cref="PadMap"/> is pinned to
/// <see cref="InputDevices.FromPad"/> by a test, so neither can drift.
/// </summary>
public partial class StartScreen : Control
{
	public event Action? Started;

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
		var rows = Rows();
		FillGrid(UiFind.Get<GridContainer>(this, "KeyboardGrid"), rows.Select(r => (r.Action, r.Keyboard)).ToList());
		FillGrid(UiFind.Get<GridContainer>(this, "PadGrid"), rows.Select(r => (r.Action, r.Pad)).ToList());
		Start = UiFind.Get<Button>(this, "Start");
		Start.Pressed += OnStart;
		Start.GrabFocus();
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

	public void Open() { Visible = true; Start.GrabFocus(); }

	/// <summary>Raises <see cref="Started"/> and hides the screen (once per opening).</summary>
	public void OnStart()
	{
		if (!Visible) return;
		Visible = false;
		Started?.Invoke();
	}
}
