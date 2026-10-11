using System;
using Godot;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Fight;

/// <summary>
/// Fight HUD (<c>scenes/ui/hud.tscn</c>): health bars, Ryo's 3-bar meter and burst seal, the K.O. banner and the
/// lose screen. The layout, fonts and colours are nodes and theme styles in the scene; this script only pushes
/// values into them. Reads the Match and an <see cref="IHudView"/> only; never writes sim state. Everything sits in
/// the top strip so the fighters' gameplay plane stays clear. Layout is in 1920x1080 units (scales with the viewport).
/// </summary>
public partial class FightHud : CanvasLayer
{
	public static readonly Color Paper = new(0.93f, 0.89f, 0.78f), Ink = new(0.13f, 0.14f, 0.25f),
		Seal = new(0.72f, 0.16f, 0.14f), Pine = new(0.22f, 0.38f, 0.3f), Persimmon = new(0.85f, 0.4f, 0.15f);

	private string _opponentId = "";
	private string _opponentName = "";
	private TextureRect _portrait = null!, _burstArt = null!;
	private Label _burstLetter = null!, _temperament = null!;

	/// <summary>Meter/burst source (YOK-20): FightScene assigns a MatchHudView over Ryo; defaults to one over the refreshed match.</summary>
	public IHudView? View { get; set; }

	/// <summary>Raised by the lose screen's Restart button; the scene wires it to its reset path.</summary>
	public event Action? RestartRequested;

	private Label _banner = null!, _meterText = null!;
	private LoseScreen _lose = null!;
	private readonly ProgressBar[] _health = new ProgressBar[2], _meter = new ProgressBar[3];
	private readonly Label[] _healthText = new Label[2];
	private Control _burst = null!;
	private bool _ready;

	public override void _Ready()
	{
		_banner = UiFind.Get<Label>(this, "Banner");
		_lose = UiFind.Get<LoseScreen>(this, "LoseScreen");
		_lose.RestartRequested += () => RestartRequested?.Invoke();
		for (int i = 0; i < 2; i++)
		{
			_health[i] = UiFind.Get<ProgressBar>(this, $"P{i + 1}Health");
			_healthText[i] = UiFind.Get<Label>(this, $"P{i + 1}HealthText");
		}
		for (int i = 0; i < 3; i++) _meter[i] = UiFind.Get<ProgressBar>(this, $"Meter{i + 1}");
		_meterText = UiFind.Get<Label>(this, "MeterText");
		_burst = UiFind.Get<Control>(this, "BurstSeal");
		_burstArt = UiFind.Get<TextureRect>(this, "BurstArt");
		_burstLetter = UiFind.Get<Label>(this, "BurstLetter");
		_portrait = UiFind.Get<TextureRect>(this, "P2Portrait");
		_temperament = UiFind.Get<Label>(this, "Temperament");
		_banner.Text = "";
		_ready = true;
	}

	/// <summary>The lose screen's story card, already filled with the run's names (S4). Null title keeps the current one.</summary>
	public void SetLoseText(string? title, string text) => _lose.SetText(title, text);

	public string LoseTitle => _lose?.Title ?? "";
	public string LoseText => _lose?.Text ?? "";

	public void Refresh(Match m)
	{
		if (!_ready) return; // before _Ready
		View ??= new MatchHudView(m);
		ShowOpponent(FightScene.Opponent);
		_temperament.Text = GetParent() is FightScene fight ? fight.Temperament.ToUpperInvariant() : "";
		for (int i = 0; i < 2; i++)
		{
			Fighter f = m.Fighters[i];
			_health[i].MaxValue = f.MaxHealth;
			_health[i].Value = Math.Clamp(f.Health, 0, f.MaxHealth);
			_healthText[i].Text = HealthText(i == 0 ? "RYO" : _opponentName, f);
			// Keep the longest current opponent names readable without clipping their health.
			var label = _healthText[i];
			var font = label.GetThemeFont("font");
			int size = 34;
			while (size > 26 && font.GetStringSize(label.Text, fontSize: size).X > label.Size.X) size--;
			label.AddThemeFontSizeOverride("font_size", size);
		}
		if (View != null) ShowMeter(View);
		bool ryoLost = m.Phase == MatchPhase.Over && m.Winner != 0;
		_banner.Text = m.Phase switch
		{
			MatchPhase.KoSlowMo => "K.O.",
			MatchPhase.Over when m.Winner == 0 => "Ryo wins",
			_ => "",
		};
		if (ryoLost && !_lose.Visible) _lose.Open();
		_lose.Visible = ryoLost;
	}

	private void ShowMeter(IHudView v)
	{
		int perBar = Math.Max(1, v.MeterMax / 3);
		for (int i = 0; i < 3; i++)
		{
			int fill = Math.Clamp(v.Meter - i * perBar, 0, perBar);
			_meter[i].MaxValue = perBar;
			_meter[i].Value = fill;
			_meter[i].ThemeTypeVariation = fill >= perBar ? "MeterBarFull" : "MeterBar";
		}
		// Burst seal: red when unspent, grey when spent (C6).
		_burst.ThemeTypeVariation = v.BurstAvailable ? "BurstSeal" : "BurstSealSpent";
		_burstArt.Modulate = v.BurstAvailable ? Colors.White : new Color(0.45f, 0.45f, 0.45f, 0.65f);
		((ShaderMaterial)_burstArt.Material).SetShaderParameter("spent", !v.BurstAvailable);
		_burstLetter.Text = v.BurstAvailable ? "B" : "×";
		_burst.TooltipText = v.BurstAvailable ? "Burst unspent" : "Burst spent";
		_meterText.Text = $"METER {v.Meter} / {v.MeterMax}";
	}

	/// <summary>Presentation lookup only. Unknown opponents have no portrait, never another fighter's mask.</summary>
	public static string PortraitPath(string opponent) => opponent switch
	{
		"kitsune" or "nine-tailed-kitsune" => "res://assets/generated/ui/fox-mask.png",
		"tanuki" => "", // No boss reveal in the HUD.
		_ => $"res://assets/generated/ui/{opponent}-portrait.png",
	};

	private void ShowOpponent(string id)
	{
		if (_opponentId == id) return;
		_opponentId = id;
		_opponentName = Story.StoryLibrary.DisplayName(id).ToUpperInvariant();
		string path = PortraitPath(id);
		_portrait.Texture = path.Length > 0 && ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
		_portrait.Visible = _portrait.Texture != null;
	}

	/// <summary>Health text, e.g. "RYO · 720 / 1000".</summary>
	public static string HealthText(string name, Fighter f) => $"{name} · {Math.Max(0, f.Health)} / {f.MaxHealth}";
}
