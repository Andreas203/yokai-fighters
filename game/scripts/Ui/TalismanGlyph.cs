using Godot;

namespace YokaiFighters.Ui;

/// <summary>What a <see cref="TalismanGlyph"/> draws.</summary>
public enum GlyphKind { Heal, Rest, Shop, Trial }

/// <summary>
/// Small drawn marks for the rest-stop option cards, in the paper-talisman style (ink strokes, one red seal dot): a brush
/// cross for the free heal, a crescent for the dojo's rest, an old coin for the shop, a ring and bolt for the trial.
/// Pure drawing; it holds no game state.
/// </summary>
public partial class TalismanGlyph : Control
{
	static readonly Color Ink = new("2B2622"), Seal = new("B5392B");

	GlyphKind _kind;
	public GlyphKind Kind { get => _kind; set { _kind = value; QueueRedraw(); } }

	public override void _Ready() { MouseFilter = MouseFilterEnum.Ignore; SetAnchorsPreset(LayoutPreset.FullRect); }

	public override void _Draw()
	{
		var c = Size / 2f;
		float r = Mathf.Min(Size.X, Size.Y) * 0.34f;
		switch (_kind)
		{
			case GlyphKind.Heal:
				DrawLine(c + new Vector2(0, -r), c + new Vector2(0, r), Seal, r * 0.55f);
				DrawLine(c + new Vector2(-r, 0), c + new Vector2(r, 0), Seal, r * 0.55f);
				DrawCircle(c, r * 0.22f, Ink);
				break;
			case GlyphKind.Rest:
				DrawCircle(c, r, Ink);
				DrawCircle(c + new Vector2(r * 0.55f, -r * 0.3f), r * 0.85f, new Color("E8DCC0"));
				DrawCircle(c + new Vector2(-r * 0.1f, r * 0.55f), r * 0.14f, Seal);
				break;
			case GlyphKind.Shop:
				DrawArc(c, r, 0, Mathf.Tau, 32, Ink, r * 0.3f);
				DrawRect(new Rect2(c - new Vector2(r * 0.33f, r * 0.33f), new Vector2(r * 0.66f, r * 0.66f)), Seal);
				break;
			case GlyphKind.Trial:
				DrawArc(c, r, 0, Mathf.Tau, 32, Ink, r * 0.22f);
				DrawArc(c, r * 0.5f, 0, Mathf.Tau, 24, Ink, r * 0.18f);
				DrawCircle(c, r * 0.2f, Seal);
				break;
		}
	}
}
