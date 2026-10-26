using System;
using Godot;
using YokaiFighters.Fight;

namespace YokaiFighters.Ui;

/// <summary>
/// One reward talisman (<c>scenes/ui/reward_card.tscn</c>): the layout is in the scene; this fills it from a
/// <see cref="CardView"/> (all text is card data, A12) and shows the selected / frame-data state.
/// </summary>
public partial class RewardCard : Control
{
	public static readonly Color Indigo = new(0.2f, 0.25f, 0.5f);
	/// <summary>
	/// YOK-186: the crest and tag of a rare card (A9). PROVISIONAL: not one of V1's four named palette colours, so the
	/// designer must confirm it. Dark enough for the cream tag text (about 5:1).
	/// </summary>
	public static readonly Color RareGold = Color.FromHtml("7A5C12");

	/// <summary>Body size when the frame-data box is hidden / shown (the card grows to fit it).</summary>
	[Export] public float BodyHeight { get; set; } = 640f;
	[Export] public float BodyHeightWithFrames { get; set; } = 700f;
	/// <summary>A selected card sits this far above the others (0: every name shares one baseline; the frame marks the pick).</summary>
	[Export] public float RaiseWhenSelected { get; set; } = 0f;

	public event Action<RewardCard>? Hovered, Clicked;

	private Panel _body = null!;
	private SealStamp _crest = null!, _fallback = null!;
	private TextureRect _illustration = null!;
	private NinePatchRect _selection = null!;
	private Label _name = null!, _source = null!, _tag = null!, _plain = null!, _prompt = null!, _frames = null!;
	private LevelPips _pips = null!;
	private bool _showPips, _frameData;
	private Control _tagPanel = null!, _frameBox = null!;

	public override void _Ready()
	{
		_body = UiFind.Get<Panel>(this, "Body");
		_crest = UiFind.Get<SealStamp>(this, "Crest");
		_fallback = UiFind.Get<SealStamp>(this, "Fallback");
		_illustration = UiFind.Get<TextureRect>(this, "Illustration");
		_selection = UiFind.Get<NinePatchRect>(this, "Selection");
		_name = UiFind.Get<Label>(this, "Name");
		_source = UiFind.Get<Label>(this, "Source");
		_tag = UiFind.Get<Label>(this, "Tag");
		_pips = UiFind.Get<LevelPips>(this, "Pips");
		_tagPanel = UiFind.Get<Control>(this, "TagPanel");
		_plain = UiFind.Get<Label>(this, "Plain");
		_prompt = UiFind.Get<Label>(this, "TargetPrompt");
		_frameBox = UiFind.Get<Control>(this, "FrameBox");
		_frames = UiFind.Get<Label>(this, "FrameText");
		MouseEntered += () => Hovered?.Invoke(this);
		GuiInput += e =>
		{
			if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) { AcceptEvent(); Clicked?.Invoke(this); }
		};
		SetState(false, false);
	}

	public void Bind(CardView c)
	{
		_crest.Glyph = TextCuts.Badge(c.Name);
		_fallback.Glyph = _crest.Glyph;
		// Modifier and cancel-rule cards carry no ability art of their own: the generic diamond. Otherwise the move id picks it (YOK-79).
		_illustration.Texture = c.Kind is CardKind.Modifier or CardKind.CancelRule ? AbilityArt.Fallback : AbilityArt.ForId(c.Id);
		_showPips = c.Kind == CardKind.Upgrade && c.ToLevel == 3; // Lv 3 evolution: base icon plus level pips
		_pips.Level = c.ToLevel; _pips.Max = 3;
		ApplyArtVisibility(); // a rebind while frame data is on must not bring art or pips back over the frame box
		// YOK-186, A9: a rare card (cancel rule, or a second special) wears gold; there was no rare style before.
		_crest.Fill = c.Rare ? RareGold : c.Kind switch { CardKind.Modifier => Color.FromHtml("2D3A5E"), CardKind.Upgrade => Color.FromHtml("34483B"), _ => Color.FromHtml("B5332B") };
		_fallback.Fill = _crest.Fill;
		_tagPanel.SelfModulate = _crest.Fill;
		_name.Text = c.Name;
		_source.Text = c.Source;
		_tag.Text = c.Tag;
		_tagPanel.ThemeTypeVariation = c.Kind switch { CardKind.NewMove or CardKind.CancelRule => "TagNew", CardKind.Upgrade => "TagUpgrade", _ => "TagModifier" };
		_plain.Text = c.Plain;
		_prompt.Visible = c.Kind == CardKind.Modifier;
		_prompt.Text = c.TargetPrompt ?? "Pick a special";
		_frames.Text = c.Frames;
	}

	public void SetState(bool selected, bool frameData)
	{
		_body.Position = new Vector2(0f, selected ? 0f : RaiseWhenSelected);
		_body.Size = new Vector2(Size.X, frameData ? BodyHeightWithFrames : BodyHeight);
		_selection.Visible = selected;
		_frameData = frameData;
		ApplyArtVisibility();
		_frameBox.Visible = frameData;
	}

	void ApplyArtVisibility()
	{
		_illustration.Visible = !_frameData && _illustration.Texture != null;
		_fallback.Visible = !_frameData && _illustration.Texture == null;
		_pips.Visible = _showPips && !_frameData;
	}
}
