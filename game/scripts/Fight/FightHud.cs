using System;
using Godot;
using YokaiFighters.Sim;

namespace YokaiFighters.Fight;

/// <summary>
/// Fight HUD in placeholder paper-talisman style (Godot-drawn shapes, V1): health bars, Ryo's
/// 3-bar meter and burst seal, plus a lose screen with Restart. Reads the Match and an
/// <see cref="IHudView"/> only; never writes sim state. Everything sits in the top strip so the
/// fighters' gameplay plane stays clear. Layout is in 1920x1080 units (scales with the viewport).
/// </summary>
public partial class FightHud : CanvasLayer
{
	public static readonly Color Paper = new(0.93f, 0.89f, 0.78f), Ink = new(0.13f, 0.14f, 0.25f),
		Seal = new(0.72f, 0.16f, 0.14f), Pine = new(0.22f, 0.38f, 0.3f), Persimmon = new(0.85f, 0.4f, 0.15f);

	private const float BarWidth = 760f, BarHeight = 34f, Margin = 60f, TopY = 56f;

	/// <summary>Meter/burst source (YOK-20): FightScene assigns a MatchHudView over Ryo; defaults to one over the refreshed match.</summary>
	public IHudView? View { get; set; }

	/// <summary>Raised by the lose screen's Restart button; the scene wires it to its reset path.</summary>
	public event Action? RestartRequested;

	private Canvas _canvas = null!;
	private Label _banner = null!;
	private Control _loseScreen = null!;
	private Button _restart = null!;
	private Label _loseTitle = null!, _loseText = null!;
	private Match? _match;

	public override void _Ready()
	{
		_canvas = new Canvas { Name = "Canvas", Hud = this, MouseFilter = Control.MouseFilterEnum.Ignore };
		_canvas.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(_canvas);

		_banner = new Label { Name = "Banner", Position = new Vector2(0f, 330f), Size = new Vector2(1920f, 160f), HorizontalAlignment = HorizontalAlignment.Center };
		_banner.AddThemeFontSizeOverride("font_size", 96);
		_banner.AddThemeColorOverride("font_color", Paper);
		_banner.AddThemeColorOverride("font_outline_color", Ink);
		_banner.AddThemeConstantOverride("outline_size", 12);
		AddChild(_banner);

		_loseScreen = new Control { Name = "LoseScreen", Visible = false, MouseFilter = Control.MouseFilterEnum.Stop };
		_loseScreen.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_loseScreen.AddChild(new ColorRect { Color = new Color(Ink, 0.55f), Size = new Vector2(1920f, 1080f), MouseFilter = Control.MouseFilterEnum.Ignore });
		_loseScreen.AddChild(new TalismanPanel { Position = new Vector2(660f, 300f), Size = new Vector2(600f, 440f), MouseFilter = Control.MouseFilterEnum.Ignore });
		// Placeholders until SetLoseText gives the YOK-44 story card (data/story/lose-screen.json).
		_loseScreen.AddChild(_loseTitle = MakeLabel("Title", "Ryo is defeated", new Vector2(660f, 340f), new Vector2(600f, 70f), 50));
		_loseScreen.AddChild(_loseText = MakeLabel("Sub", "The yokai slips free.", new Vector2(700f, 415f), new Vector2(520f, 160f), 24));
		_loseText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_loseText.VerticalAlignment = VerticalAlignment.Center;
		if (_pendingLose is { } pl) SetLoseText(pl.Title, pl.Text);
		_restart = new Button { Name = "Restart", Text = "Restart", Position = new Vector2(760f, 600f), Size = new Vector2(400f, 100f) };
		_restart.AddThemeFontSizeOverride("font_size", 44);
		foreach (string c in new[] { "font_color", "font_hover_color", "font_focus_color", "font_pressed_color" })
			_restart.AddThemeColorOverride(c, Paper);
		foreach (string s in new[] { "normal", "hover", "pressed", "focus" })
			_restart.AddThemeStyleboxOverride(s, new StyleBoxFlat
			{
				BgColor = s == "hover" || s == "focus" ? Seal.Lightened(0.12f) : Seal,
				BorderColor = Ink, BorderWidthBottom = 4, BorderWidthTop = 4, BorderWidthLeft = 4, BorderWidthRight = 4,
			});
		_restart.Pressed += () => RestartRequested?.Invoke();
		_loseScreen.AddChild(_restart);
		AddChild(_loseScreen);
	}

	private (string? Title, string Text)? _pendingLose;

	/// <summary>The lose screen's story card, already filled with the run's names (S4). Null title keeps the current one.</summary>
	public void SetLoseText(string? title, string text)
	{
		_pendingLose = (title, text);
		if (_loseText == null) return; // applied in _Ready
		if (title != null) _loseTitle.Text = title;
		_loseText.Text = text;
	}

	public string LoseTitle => _loseTitle?.Text ?? "";
	public string LoseText => _loseText?.Text ?? "";

	private static Label MakeLabel(string name, string text, Vector2 pos, Vector2 size, int fontSize)
	{
		var l = new Label { Name = name, Text = text, Position = pos, Size = size, HorizontalAlignment = HorizontalAlignment.Center };
		l.AddThemeFontSizeOverride("font_size", fontSize);
		l.AddThemeColorOverride("font_color", Ink);
		return l;
	}

	public void Refresh(Match m)
	{
		if (_banner == null) return; // before _Ready
		_match = m;
		View ??= new MatchHudView(m);
		_canvas.QueueRedraw();
		bool ryoLost = m.Phase == MatchPhase.Over && m.Winner != 0;
		_banner.Text = m.Phase switch
		{
			MatchPhase.KoSlowMo => "K.O.",
			MatchPhase.Over when m.Winner == 0 => "Ryo wins\nR to reset",
			_ => "",
		};
		if (ryoLost && !_loseScreen.Visible) _restart.GrabFocus();
		_loseScreen.Visible = ryoLost;
	}

	/// <summary>Health text, e.g. "RYO · 720 / 1000".</summary>
	public static string HealthText(string name, Fighter f) => $"{name} · {Math.Max(0, f.Health)} / {f.MaxHealth}";

	private sealed partial class TalismanPanel : Control
	{
		public override void _Draw()
		{
			DrawRect(new Rect2(Vector2.Zero, Size), Paper);
			DrawRect(new Rect2(Vector2.Zero, Size), Ink, false, 6f);
			DrawRect(new Rect2(14, 14, Size.X - 28, Size.Y - 28), Seal, false, 2f);
			DrawCircle(new Vector2(Size.X / 2f, 0f), 22f, Seal); // hanging seal
		}
	}

	private sealed partial class Canvas : Control
	{
		public FightHud Hud = null!;

		private void Text(Font font, Vector2 pos, string text, HorizontalAlignment align, float width, int size)
		{
			DrawStringOutline(font, pos, text, align, width, size, 8, Ink);
			DrawString(font, pos, text, align, width, size, Paper);
		}

		public override void _Draw()
		{
			Match? m = Hud._match;
			if (m == null) return;
			Font font = ThemeDB.FallbackFont;
			string[] names = { "RYO", "KITSUNE" };
			for (int i = 0; i < 2; i++)
			{
				Fighter f = m.Fighters[i];
				float x = i == 0 ? Margin : 1920f - Margin - BarWidth;
				DrawRect(new Rect2(x - 8, TopY - 8, BarWidth + 16, BarHeight + 16), Paper);
				DrawRect(new Rect2(x - 8, TopY - 8, BarWidth + 16, BarHeight + 16), Ink, false, 4f);
				float w = BarWidth * Math.Clamp(f.Health, 0, f.MaxHealth) / f.MaxHealth;
				float fx = i == 0 ? x : x + BarWidth - w; // P2 drains toward the screen edge
				DrawRect(new Rect2(fx, TopY, w, BarHeight), i == 0 ? Seal : Persimmon);
				Text(font, new Vector2(x, TopY + BarHeight + 40f), HealthText(names[i], f), i == 0 ? HorizontalAlignment.Left : HorizontalAlignment.Right, BarWidth, 30);
			}
			if (Hud.View != null) DrawMeter(Hud.View, font);
		}

		private void DrawMeter(IHudView v, Font font)
		{
			const float bx = Margin, by = TopY + BarHeight + 64f, bw = 240f, bh = 22f, gap = 8f;
			int perBar = v.MeterMax / 3;
			for (int i = 0; i < 3; i++)
			{
				float x = bx + i * (bw + gap);
				DrawRect(new Rect2(x, by, bw, bh), Ink);
				float fill = Math.Clamp(v.Meter - i * perBar, 0, perBar) / (float)perBar;
				DrawRect(new Rect2(x + 2, by + 2, (bw - 4) * fill, bh - 4), fill >= 1f ? Persimmon : Pine);
			}
			// Burst seal: red when unspent, grey when spent (C6).
			var c = new Vector2(bx + 3 * (bw + gap) + 34f, by + bh / 2f);
			DrawCircle(c, 26f, v.BurstAvailable ? Seal : new Color(0.45f, 0.45f, 0.45f));
			DrawArc(c, 26f, 0f, Mathf.Tau, 32, Ink, 3f);
			DrawString(font, c + new Vector2(-26f, 9f), "B", HorizontalAlignment.Center, 52f, 28, Paper);
			Text(font, new Vector2(bx, by + bh + 28f), $"METER {v.Meter} / {v.MeterMax}", HorizontalAlignment.Left, -1, 22);
		}
	}
}
