using Godot;
using YokaiFighters.Sim;

namespace YokaiFighters.Fight;

/// <summary>
/// Placeholder fight HUD: two health bars and the KO banner. The ui-designer replaces this with
/// the paper-talisman HUD; it only reads the Match, never writes it.
/// </summary>
public partial class FightHud : CanvasLayer
{
	private const float BarWidth = 760f, BarHeight = 32f, Margin = 60f;

	private readonly ColorRect[] _fills = new ColorRect[2];
	private Label _banner = null!;

	public override void _Ready()
	{
		for (int i = 0; i < 2; i++)
		{
			float x = i == 0 ? Margin : 1920f - Margin - BarWidth;
			AddChild(new ColorRect { Color = new Color(0.1f, 0.1f, 0.1f), Position = new Vector2(x, Margin), Size = new Vector2(BarWidth, BarHeight) });
			_fills[i] = new ColorRect { Name = i == 0 ? "P1Health" : "P2Health", Color = new Color(0.85f, 0.2f, 0.2f), Position = new Vector2(x, Margin), Size = new Vector2(BarWidth, BarHeight) };
			AddChild(_fills[i]);
		}
		_banner = new Label { Name = "Banner", Position = new Vector2(0f, 420f), Size = new Vector2(1920f, 200f), HorizontalAlignment = HorizontalAlignment.Center };
		_banner.AddThemeFontSizeOverride("font_size", 96);
		AddChild(_banner);
	}

	public void Refresh(Match m)
	{
		if (_banner == null) return; // before _Ready
		for (int i = 0; i < 2; i++)
		{
			Fighter f = m.Fighters[i];
			float w = BarWidth * f.Health / f.MaxHealth;
			_fills[i].Size = new Vector2(w, BarHeight);
			// P2's bar drains toward the screen edge, mirroring P1.
			if (i == 1) _fills[i].Position = new Vector2(1920f - Margin - w, Margin);
		}
		_banner.Text = m.Phase switch
		{
			MatchPhase.KoSlowMo => "K.O.",
			MatchPhase.Over => (m.Winner switch { 0 => "Ryo wins", 1 => "Kitsune wins", _ => "Double K.O." }) + "\nR to reset",
			_ => "",
		};
	}
}
