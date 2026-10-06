using System;
using System.Collections.Generic;
using Godot;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Fight;

/// <summary>
/// YOK-39 start screen, shown when the game boots (before the first fight): title, subtitle, Start Game and the
/// default Kihon controls, keyboard and pad side by side. Start = Enter / Space / pad A (the focused button) or a
/// click; it raises <see cref="Started"/> once. Keyboard text is read from <see cref="InputDevices.P1Keys"/>; the
/// pad table <see cref="PadMap"/> is pinned to <see cref="InputDevices.FromPad"/> by a test, so neither can drift.
/// </summary>
public partial class StartScreen : Control
{
	public event Action? Started;
	public const string TitleText = "Yokai Fighters", SubtitleText = "Demo — Ryo vs the Kitsune";
	public const string HintText = "Beat the Kitsune to bind her and draft one of her powers.";
	public const string DebugKeysText = "Debug:  F1 boxes   F2 pause   F3 step   F4 Kata/Kihon   F6 temperament   F7 P2 control   F8 replay   F9 menu   F10 models";
	public const string SpecialsLine = "Special + neutral: Spirit Wave   forward: Rising Talisman   back: your drafted special";

	public Button Start { get; private set; } = null!;
	/// <summary>Draw the debug-keys footer (debug builds; tests can force it).</summary>
	public bool ShowDebugKeys { get; set; } = OS.IsDebugBuild();

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

	/// <summary>The controls outline, built from the live bindings.</summary>
	public static List<Row> Rows()
	{
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
			new("Special", $"{K(k.Special)} + direction", $"{P(InputBits.Special)} + direction"),
			new("EX (1 bar)", "Special + direction + a button", "Special + direction + a button"),
			new("Throw", $"{K(k.LP)} + {K(k.LK)}", Chord(" + ", InputBits.LightPunch, InputBits.LightKick)),
			new("Burst (when hit)", $"{K(k.LP)} + {K(k.MP)} + {K(k.HP)}", Chord(" + ", InputBits.LightPunch, InputBits.MediumPunch, InputBits.HeavyPunch)),
			new("Block", "hold back (crouch for lows)", "hold back (down for lows)"),
		};
	}

	public override void _Ready()
	{
		Position = Vector2.Zero; Size = new Vector2(1920f, 1080f);
		MouseFilter = MouseFilterEnum.Stop;
		Start = DemoUi.Button("Start", new Vector2(710f, 856f), new Vector2(500f, 90f), 46, "Start Game");
		Start.Pressed += OnStart;
		AddChild(Start);
		Start.GrabFocus();
	}

	public void Open() { Visible = true; Start.GrabFocus(); QueueRedraw(); }

	/// <summary>Raises <see cref="Started"/> and hides the screen (once per opening).</summary>
	public void OnStart()
	{
		if (!Visible) return;
		Visible = false;
		Started?.Invoke();
	}

	public override void _Draw()
	{
		DrawRect(new Rect2(Vector2.Zero, Size), new Color(FightHud.Ink, 0.96f));
		var panel = new Rect2(150, 40, 1620, 1000);
		Paint.Talisman(this, panel, FightHud.Paper);
		Font f = ThemeDB.FallbackFont;
		DrawString(f, new Vector2(panel.Position.X, 140), TitleText, HorizontalAlignment.Center, panel.Size.X, 96, FightHud.Ink);
		Paint.Brush(this, new Vector2(760, 164), 400, 9, FightHud.Seal);
		DrawString(f, new Vector2(panel.Position.X, 214), SubtitleText, HorizontalAlignment.Center, panel.Size.X, 36, FightHud.Pine);
		Paint.Seal(this, new Vector2(panel.End.X - 110, 130), 44, "YF");

		DrawColumn(new Rect2(210, 250, 780, 440), "KEYBOARD", 0);
		DrawColumn(new Rect2(1010, 250, 700, 440), "GAMEPAD", 1);

		DrawString(f, new Vector2(210, 726), SpecialsLine, HorizontalAlignment.Left, 1500, 28, FightHud.Ink);
		DrawString(f, new Vector2(210, 764), "An attack pressed before Special gives its normal instead.", HorizontalAlignment.Left, 1500, 24, FightHud.Ink);
		Paint.Brush(this, new Vector2(210, 794), 1500, 4, new Color(FightHud.Ink, 0.5f));
		DrawString(f, new Vector2(panel.Position.X, 836), HintText, HorizontalAlignment.Center, panel.Size.X, 32, FightHud.Persimmon.Darkened(0.25f));
		DrawString(f, new Vector2(panel.Position.X, 982), "Enter / Space / A  start", HorizontalAlignment.Center, panel.Size.X, 26, FightHud.Ink);
		if (ShowDebugKeys)
			DrawString(f, new Vector2(panel.Position.X, 1020), DebugKeysText, HorizontalAlignment.Center, panel.Size.X, 20, new Color(FightHud.Ink, 0.7f));
	}

	private void DrawColumn(Rect2 r, string heading, int col)
	{
		Font f = ThemeDB.FallbackFont;
		DrawString(f, r.Position + new Vector2(0, 30), heading, HorizontalAlignment.Left, -1, 30, FightHud.Seal);
		Paint.Brush(this, r.Position + new Vector2(0, 46), r.Size.X, 5, FightHud.Ink);
		float y = r.Position.Y + 92, labelW = 250f;
		foreach (var row in Rows())
		{
			DrawString(f, new Vector2(r.Position.X, y), row.Action, HorizontalAlignment.Left, labelW, 26, FightHud.Pine);
			DrawString(f, new Vector2(r.Position.X + labelW, y), col == 0 ? row.Keyboard : row.Pad, HorizontalAlignment.Left, r.Size.X - labelW, 26, FightHud.Ink);
			y += 38f;
		}
	}
}
