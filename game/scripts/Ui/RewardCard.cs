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

	/// <summary>Body size when the frame-data box is hidden / shown (the card grows to fit it).</summary>
	[Export] public float BodyHeight { get; set; } = 640f;
	[Export] public float BodyHeightWithFrames { get; set; } = 700f;
	/// <summary>A selected card sits this far above the others.</summary>
	[Export] public float RaiseWhenSelected { get; set; } = 14f;

	public event Action<RewardCard>? Hovered, Clicked;

	private TalismanPanel _body = null!;
	private SealStamp _crest = null!;
	private Label _name = null!, _source = null!, _tag = null!, _plain = null!, _prompt = null!, _frames = null!;
	private Control _tagPanel = null!, _frameBox = null!;

	public override void _Ready()
	{
		_body = UiFind.Get<TalismanPanel>(this, "Body");
		_crest = UiFind.Get<SealStamp>(this, "Crest");
		_name = UiFind.Get<Label>(this, "Name");
		_source = UiFind.Get<Label>(this, "Source");
		_tag = UiFind.Get<Label>(this, "Tag");
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
		_crest.Glyph = c.Name[..1].ToUpperInvariant();
		_crest.Fill = c.Kind == CardKind.Modifier ? Indigo : FightHud.Seal;
		_name.Text = c.Name;
		_source.Text = c.Source;
		_tag.Text = c.Tag;
		_tagPanel.ThemeTypeVariation = c.Kind switch { CardKind.NewMove => "TagNew", CardKind.Upgrade => "TagUpgrade", _ => "TagModifier" };
		_plain.Text = c.Plain;
		_prompt.Visible = c.Kind == CardKind.Modifier;
		_prompt.Text = c.TargetPrompt ?? "Pick a special";
		_frames.Text = c.Frames;
	}

	public void SetState(bool selected, bool frameData)
	{
		_body.Position = new Vector2(0f, selected ? 0f : RaiseWhenSelected);
		_body.Size = new Vector2(Size.X, frameData ? BodyHeightWithFrames : BodyHeight);
		_body.Paper = selected ? FightHud.Paper.Lightened(0.08f) : FightHud.Paper.Darkened(0.06f);
		_body.Border = selected ? FightHud.Persimmon : FightHud.Ink;
		_body.Highlight = selected;
		_frameBox.Visible = frameData;
	}
}
