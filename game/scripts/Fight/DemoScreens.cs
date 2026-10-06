using System;
using Godot;

namespace YokaiFighters.Fight;

/// <summary>
/// YOK-48: the "demo complete" card after the won rematch, in the HUD's talisman style (placeholder UI
/// copy, not a story card). Restart raises <see cref="RestartRequested"/> (the flow starts a fresh run).
/// </summary>
public partial class DemoCompleteScreen : Control
{
	public event Action? RestartRequested;
	public Button Restart { get; private set; } = null!;
	/// <summary>Placeholders until <see cref="SetText"/> fills them from data/story/demo-complete.json.</summary>
	public const string PlaceholderTitle = "Demo complete", PlaceholderText = "Thanks for playing.";
	public string TitleText { get; private set; } = PlaceholderTitle;
	public string BodyText { get; private set; } = PlaceholderText;
	private Label? _title, _sub;

	/// <summary>The story card's title (null keeps the placeholder title) and text.</summary>
	public void SetText(string? title, string text)
	{
		TitleText = title ?? PlaceholderTitle;
		BodyText = text;
		if (_title != null) _title.Text = TitleText;
		if (_sub != null) _sub.Text = BodyText;
	}

	public override void _Ready()
	{
		Visible = false;
		MouseFilter = MouseFilterEnum.Stop;
		SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(new ColorRect { Color = new Color(FightHud.Ink, 0.55f), Size = new Vector2(1920f, 1080f), MouseFilter = MouseFilterEnum.Ignore });
		AddChild(new Panel { Position = new Vector2(660f, 300f), Size = new Vector2(600f, 440f), MouseFilter = MouseFilterEnum.Ignore });
		AddChild(_title = DemoUi.Label("Title", TitleText, new Vector2(660f, 340f), new Vector2(600f, 70f), 40));
		AddChild(_sub = DemoUi.Label("Sub", BodyText, new Vector2(700f, 410f), new Vector2(520f, 180f), 24));
		AddChild(Restart = DemoUi.Button("Restart", new Vector2(760f, 600f), new Vector2(400f, 100f), 44));
		Restart.Pressed += () => RestartRequested?.Invoke();
	}

	public new void Show() { Visible = true; Restart.GrabFocus(); }

	private sealed partial class Panel : Control
	{
		public override void _Draw() => Ui.Paint.Talisman(this, new Rect2(Vector2.Zero, Size), FightHud.Paper);
	}
}

/// <summary>
/// YOK-48 debug menu (debug builds only, F9): pick the Kitsune's temperament (one button per loaded profile),
/// restart the run from the first fight, or resume. The flow pauses the fight while it is open.
/// </summary>
public partial class DemoDebugMenu : Control
{
	public FightScene Scene { get; set; } = null!;
	private Label _current = null!;

	public override void _Ready()
	{
		Visible = false;
		MouseFilter = MouseFilterEnum.Stop;
		SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(new ColorRect { Color = new Color(0f, 0f, 0f, 0.6f), Size = new Vector2(1920f, 1080f), MouseFilter = MouseFilterEnum.Ignore });
		var box = new VBoxContainer { Name = "Box", Position = new Vector2(710f, 260f), Size = new Vector2(500f, 0f) };
		box.AddThemeConstantOverride("separation", 16);
		AddChild(box);
		box.AddChild(DemoUi.Label("Heading", "DEBUG  (F9 closes)", Vector2.Zero, new Vector2(500f, 50f), 34, FightHud.Paper));
		box.AddChild(_current = DemoUi.Label("Current", "", Vector2.Zero, new Vector2(500f, 40f), 26, FightHud.Paper));
		foreach (var p in Scene.Profiles)
		{
			string t = p.Temperament;
			var b = DemoUi.Button("Temperament_" + t, Vector2.Zero, new Vector2(500f, 70f), 30, $"Kitsune: {t}");
			b.Pressed += () => { Scene.SetTemperament(t); Refresh(); };
			box.AddChild(b);
		}
		var restart = DemoUi.Button("RestartRun", Vector2.Zero, new Vector2(500f, 70f), 30, "Restart run (first fight)");
		restart.Pressed += Scene.RestartRun; // also closes the menu
		box.AddChild(restart);
		var resume = DemoUi.Button("Resume", Vector2.Zero, new Vector2(500f, 70f), 30, "Resume");
		resume.Pressed += () => { if (Visible) Scene.ToggleDebugMenu(); };
		box.AddChild(resume);
	}

	public void Open() { Refresh(); Visible = true; GetNode<Button>("Box/Resume").GrabFocus(); }
	public void Close() => Visible = false;

	private void Refresh() => _current.Text = $"Kitsune temperament: {Scene.Temperament}";
}

internal static class DemoUi
{
	public static Label Label(string name, string text, Vector2 pos, Vector2 size, int fontSize, Color? color = null)
	{
		var l = new Label { Name = name, Text = text, Position = pos, CustomMinimumSize = size, Size = size, HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
		l.AddThemeFontSizeOverride("font_size", fontSize);
		l.AddThemeColorOverride("font_color", color ?? FightHud.Ink);
		return l;
	}

	/// <summary>The HUD lose screen's seal-red button.</summary>
	public static Button Button(string name, Vector2 pos, Vector2 size, int fontSize, string? text = null)
	{
		var b = new Button { Name = name, Text = text ?? name, Position = pos, CustomMinimumSize = size, Size = size };
		b.AddThemeFontSizeOverride("font_size", fontSize);
		foreach (string c in new[] { "font_color", "font_hover_color", "font_focus_color", "font_pressed_color" })
			b.AddThemeColorOverride(c, FightHud.Paper);
		foreach (string s in new[] { "normal", "hover", "pressed", "focus" })
			b.AddThemeStyleboxOverride(s, new StyleBoxFlat
			{
				BgColor = s == "hover" || s == "focus" ? FightHud.Seal.Lightened(0.12f) : FightHud.Seal,
				BorderColor = FightHud.Ink, BorderWidthBottom = 4, BorderWidthTop = 4, BorderWidthLeft = 4, BorderWidthRight = 4,
			});
		return b;
	}
}
