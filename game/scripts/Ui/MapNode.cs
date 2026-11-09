using System;
using Godot;

namespace YokaiFighters.Ui;

/// <summary>
/// One node on the map screen (<c>scenes/ui/map_node.tscn</c>), YOK-238 map v2: not a card but an illustrated object (a yokai's mask, the
/// merchant's stall, the dojo, the tea house) with a small torn rice-paper name tag below: the type letter, the name of whoever waits there
/// (R2; a merchant or dojo shows its type name), a red tick once cleared, and "YOU ARE HERE" under the node Ryo is on. An elder (Y3) wears its
/// twist seal at the illustration's upper right. States are marks round the illustration: here = a loose persimmon brush ring, reachable = a
/// thin indigo ring, selected = a foxfire halo with a small pointer on its left. The button's rect (illustration, ring clearance and tag) is the
/// hit area; only a reachable node is enabled, so a click cannot pick any other. The look comes in as a <see cref="MapNodeLook"/>; nothing here names a node type.
/// </summary>
public partial class MapNode : Button
{
	public string NodeId { get; private set; } = "";
	public MapNodeState State { get; private set; } = MapNodeState.Locked;
	public bool CursorOn { get; private set; }
	/// <summary>The type's letter on the tag (D, E, M, J, B; "?" for an unknown type).</summary>
	public string TypeText => UiFind.Get<Label>(this, "TypeLabel").Text;
	/// <summary>The type's full label ("Duel", "Elder duel"); the node carries no tooltip.</summary>
	public string TypeName { get; private set; } = "";
	/// <summary>The node's name as shown: the yokai, or the type's label when no yokai waits there.</summary>
	public string YokaiText => UiFind.Get<Label>(this, "NameLabel").Text;
	/// <summary>"YOU ARE HERE" on the node Ryo is on, else "".</summary>
	public string StateText => UiFind.Get<Label>(this, "StateLabel").Text;
	/// <summary>YOK-109: the lines under the name from the type's style (the boss: "waits at the end" / "copies your best move"); "" = none shown.</summary>
	public string TagText => UiFind.Get<Label>(this, "TagLabel").Text;
	public bool ElderShown => UiFind.Get<Control>(this, "ElderMark").Visible;
	/// <summary>The red tick on the tag: shown for a cleared node.</summary>
	public bool ClearedShown => UiFind.Get<Control>(this, "ClearedMark").Visible;
	/// <summary>The illustration: its yokai's mask, or the type's sprite; null = the big type letter stands in.</summary>
	public Texture2D? SpriteTexture => UiFind.Get<TextureRect>(this, "IconTexture").Texture;
	/// <summary>The illustration's rect inside this node (the routes end on it, the marks go round it).</summary>
	public Rect2 SpriteRect { get; private set; }
	/// <summary>Where the art is actually drawn (box coordinates): <see cref="SpriteRect"/> itself, or larger around it when the type's sprite is drawn bigger (a stall, the dojo, the tea house). Marks go round this; nothing else reads it.</summary>
	public Rect2 ArtRect { get; private set; }
	/// <summary>The mark the node wears now: the ring or halo drawn round the illustration, null for none.</summary>
	public MapMark? Mark => State == MapNodeState.Current ? MapMark.Here : State == MapNodeState.Reachable ? (CursorOn ? MapMark.Selected : MapMark.Reachable) : null;
	/// <summary>YOK-139: the twist icon id drawn on the elder mark; "" = the plain "E" mark (no twist data).</summary>
	public string TwistIconId { get; private set; } = "";
	public Control Tag => UiFind.Get<Control>(this, "Tag");

	public event Action<MapNode>? Chosen, Hovered;

	public override void _Ready()
	{
		FocusMode = FocusModeEnum.None; // the screen owns the cursor; Godot focus never reaches a locked node
		Pressed += () => Chosen?.Invoke(this);
		MouseEntered += () => Hovered?.Invoke(this);
		foreach (var s in new[] { "normal", "hover", "pressed", "disabled", "focus" }) AddThemeStyleboxOverride(s, new StyleBoxEmpty());
	}

	/// <param name="size">The node's box.</param>
	/// <param name="sprite">The illustration's rect inside the box.</param>
	/// <param name="lineH">Height of one tag line; the tag's text scales with it.</param>
	public void Bind(MapNodeView v, MapNodeLook look, Vector2 size, Rect2 sprite, float lineH, bool cursor, MapNodeLook? twistLook = null)
	{
		NodeId = v.Id; State = v.State; CursorOn = cursor; SpriteRect = sprite; TypeName = look.Label;
		Name = "Node_" + v.Id;
		CustomMinimumSize = size; Size = size;
		int font = (int)Math.Clamp(lineH * 0.78f, 13, 28);
		var icon = UiFind.Get<Label>(this, "IconLabel");
		var type = UiFind.Get<Label>(this, "TypeLabel");
		var name = UiFind.Get<Label>(this, "NameLabel");
		var stateLabel = UiFind.Get<Label>(this, "StateLabel");
		var tag = UiFind.Get<Label>(this, "TagLabel");
		var tex = UiFind.Get<TextureRect>(this, "IconTexture");
		var tagBox = UiFind.Get<MapTag>(this, "Tag");
		var cleared = UiFind.Get<Control>(this, "ClearedMark");
		icon.Text = look.Icon; type.Text = look.Icon; name.Text = v.Yokai ?? look.Label;
		tex.Texture = look.Sprite ?? look.IconTexture; tex.Visible = tex.Texture != null; icon.Visible = tex.Texture == null;
		// fit the real art inside the fixed sprite rect (centred on it, its foot on the rect's foot), then draw it SpriteScale times larger: only the drawn sprite
		// overflows the rect (up and to the sides), the box, the routes and the layout never see it (AM19). Only a drawn (revealed) node is ever bound.
		var art = sprite;
		if (tex.Texture is { } t && t.GetHeight() > 0)
		{
			float k = Math.Min(sprite.Size.X / t.GetWidth(), sprite.Size.Y / t.GetHeight()) * Math.Max(0.1f, look.SpriteScale);
			var sz = new Vector2(t.GetWidth(), t.GetHeight()) * k;
			art = new Rect2(new Vector2(sprite.GetCenter().X - sz.X / 2f, sprite.End.Y - sz.Y), sz);
		}
		ArtRect = art.Merge(sprite);
		tex.Position = art.Position; tex.Size = art.Size;
		icon.Position = sprite.Position; icon.Size = sprite.Size;
		icon.HorizontalAlignment = HorizontalAlignment.Center; icon.VerticalAlignment = VerticalAlignment.Center;
		icon.AddThemeFontSizeOverride("font_size", (int)Math.Max(24, sprite.Size.Y * 0.6f));
		icon.AddThemeColorOverride("font_color", look.Color);
		stateLabel.Text = v.State == MapNodeState.Current ? "YOU ARE HERE" : "";
		stateLabel.Visible = stateLabel.Text != "";
		tag.Text = look.Tagline; tag.Visible = look.Tagline != "";
		cleared.Visible = v.State == MapNodeState.Visited;
		cleared.CustomMinimumSize = new Vector2(font, font);
		foreach (var l in new[] { type, name, stateLabel, tag })
			l.AddThemeFontSizeOverride("font_size", l == name ? font : l == tag || l == stateLabel ? Math.Max(11, font - 5) : font);
		foreach (var l in new[] { tag, stateLabel }) l.AddThemeConstantOverride("line_spacing", -2); // the theme's caption leading would gap the boss's two lines
		type.AddThemeColorOverride("font_color", look.Color);
		name.AddThemeColorOverride("font_color", MapNodePalette.Ink);
		tag.AddThemeColorOverride("font_color", new Color(MapNodePalette.Ink, 0.85f));
		stateLabel.AddThemeColorOverride("font_color", MapNodePalette.ElderRed);
		tagBox.Seed = MapLayout.Hash(v.Id);
		tagBox.SetMargins(font * 0.5f, 2f, font * 0.5f, 5f);

		// the tag sits under the illustration, overlapping its foot, centred; it is as wide as its text needs
		tagBox.ResetSize();
		var ts = tagBox.GetCombinedMinimumSize();
		tagBox.Size = ts;
		tagBox.Position = new Vector2((size.X - ts.X) / 2f, MapNodeBox.TagTop(sprite.Size.Y));
		TooltipText = "";

		var elder = UiFind.Get<Control>(this, "ElderMark");
		elder.Visible = v.Elder;
		TwistIconId = v.Elder && twistLook != null ? v.TwistIcon ?? "" : "";
		// YOK-139 (Y3): an elder with twist data wears its twist's seal (red stamp, placeholder shape) in place of the plain "E".
		float sealS = Math.Clamp(sprite.Size.Y * 0.3f, 26f, 48f);
		var seal = elder.GetNodeOrNull<TwistSeal>("TwistSeal");
		if (seal == null) { seal = new TwistSeal { Name = "TwistSeal", SizeFlagsVertical = SizeFlags.ShrinkCenter }; elder.AddChild(seal); }
		seal.CustomMinimumSize = new Vector2(sealS, sealS);
		seal.Visible = TwistIconId != "";
		if (seal.Visible) { seal.Icon = TwistIconId; seal.Fallback = twistLook!.Value.Icon; }
		UiFind.Get<Label>(this, "ElderGlyph").Visible = !seal.Visible;
		elder.CustomMinimumSize = new Vector2(sealS * 0.8f, sealS * 0.8f);
		elder.Size = new Vector2(sealS, sealS);
		elder.Position = new Vector2(art.End.X - sealS * 0.62f, art.Position.Y - sealS * 0.18f); // the seal on the mask's upper right
		elder.AddThemeStyleboxOverride("panel", seal.Visible ? new StyleBoxEmpty() : new StyleBoxFlat { BgColor = MapNodePalette.ElderRed, CornerRadiusTopLeft = 14, CornerRadiusTopRight = 14, CornerRadiusBottomLeft = 14, CornerRadiusBottomRight = 14 });
		UiFind.Get<Label>(this, "ElderGlyph").AddThemeColorOverride("font_color", MapNodePalette.Paper);

		bool reachable = v.State == MapNodeState.Reachable;
		Disabled = !reachable;
		MouseDefaultCursorShape = reachable ? CursorShape.PointingHand : CursorShape.Arrow;
		ZIndex = cursor ? 2 : 0;
		QueueRedraw();
	}

	/// <summary>Behind the illustration and the tag: the state's ring or halo, and the pointer of the selected node.</summary>
	public override void _Draw()
	{
		if (SpriteRect.Size.X <= 0f || Mark is not { } mark) return;
		var c = ArtRect.GetCenter();
		float pad = MapNodeBox.Pad(SpriteRect.Size.Y);
		var r = ArtRect.Size / 2f + new Vector2(pad * 0.72f, pad * 0.72f);
		uint seed = MapLayout.Hash(NodeId);
		switch (mark)
		{
			case MapMark.Here: MapNodeArt.Ring(this, c, r, MapNodePalette.Persimmon, Mathf.Max(5f, SpriteRect.Size.Y * 0.045f), seed); break;
			case MapMark.Reachable: MapNodeArt.Ring(this, c, r, MapNodePalette.Indigo, Mathf.Max(2.5f, SpriteRect.Size.Y * 0.02f), seed); break;
			case MapMark.Selected:
				MapNodeArt.Halo(this, c, r, Mathf.Max(3f, SpriteRect.Size.Y * 0.025f));
				MapNodeArt.Pointer(this, c.X - r.X - 4f, c.Y, Mathf.Max(11f, SpriteRect.Size.Y * 0.1f));
				break;
		}
	}
}
