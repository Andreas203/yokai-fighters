using System;
using Godot;

namespace YokaiFighters.Ui;

/// <summary>The colours of each node state, shared by the nodes and the legend's swatches so the two cannot drift (YOK-108).</summary>
public static class MapNodePalette
{
	public static readonly Color Paper = new(0.945f, 0.91f, 0.831f), Ink = new(0.114f, 0.106f, 0.129f), Persimmon = new(0.89f, 0.42f, 0.24f),
		ElderRed = new(0.72f, 0.2f, 0.16f), Gold = new(0.98f, 0.8f, 0.3f);
	public static readonly Color Cleared = new(0.40f, 0.39f, 0.40f), Later = new(0.20f, 0.22f, 0.30f),
		PassedOverFill = new(0.12f, 0.13f, 0.21f, 0.55f), PassedOverLine = new(0.62f, 0.63f, 0.70f, 0.7f);

	public static bool Lit(MapNodeState s) => s is MapNodeState.Reachable or MapNodeState.Current;

	public static Color Fill(MapNodeState s) => s switch
	{
		MapNodeState.Reachable or MapNodeState.Current => Paper,
		MapNodeState.Visited => Cleared,
		MapNodeState.PassedOver => PassedOverFill,
		_ => Later,
	};

	public static Color Border(MapNodeState s, bool cursor, Color typeColor) =>
		cursor ? Gold : s switch
		{
			MapNodeState.Current => Persimmon,
			MapNodeState.Reachable => typeColor,
			MapNodeState.PassedOver => Colors.Transparent, // drawn dashed instead
			_ => new Color(typeColor, 0.6f),
		};

	public static string Caption(MapNodeState s, bool cursor) => s switch
	{
		MapNodeState.Current => "YOU ARE HERE",
		MapNodeState.Visited => "cleared",
		MapNodeState.Reachable => cursor ? "selected" : "reachable",
		MapNodeState.PassedOver => "passed over",
		_ => "",
	};
}

/// <summary>
/// One node on the map screen (<c>scenes/ui/map_node.tscn</c>): a button showing the type label and icon, the name of whoever
/// waits there (R2; a merchant or dojo shows its type name), the elder marker (Y3) and a state style: cleared, here, reachable,
/// selected, later, passed over (YOK-108). Only reachable nodes are enabled, so a click cannot pick any other.
/// The look comes in as a <see cref="MapNodeLook"/> (from the styles resource); nothing here names a node type.
/// </summary>
public partial class MapNode : Button
{
	public string NodeId { get; private set; } = "";
	public MapNodeState State { get; private set; } = MapNodeState.Locked;
	public bool CursorOn { get; private set; }
	public string TypeText => UiFind.Get<Label>(this, "TypeLabel").Text;
	/// <summary>The node's name as shown: the yokai, or the type's label when no yokai waits there.</summary>
	public string YokaiText => UiFind.Get<Label>(this, "NameLabel").Text;
	public string StateText => UiFind.Get<Label>(this, "StateLabel").Text;
	/// <summary>YOK-109: the lines under the name from the type's style (the boss: "waits at the end" / "copies your best move"); "" = none shown.</summary>
	public string TagText => UiFind.Get<Label>(this, "TagLabel").Text;
	public bool ElderShown => UiFind.Get<Control>(this, "ElderMark").Visible;
	/// <summary>YOK-139: the twist icon id drawn on the elder mark; "" = the plain "E" mark (no twist data).</summary>
	public string TwistIconId { get; private set; } = "";

	public event Action<MapNode>? Chosen, Hovered;

	public override void _Ready()
	{
		FocusMode = FocusModeEnum.None; // the screen owns the cursor; Godot focus never reaches a locked node
		Pressed += () => Chosen?.Invoke(this);
		MouseEntered += () => Hovered?.Invoke(this);
	}

	public void Bind(MapNodeView v, MapNodeLook look, Vector2 size, bool cursor, MapNodeLook? twistLook = null)
	{
		NodeId = v.Id; State = v.State; CursorOn = cursor;
		Name = "Node_" + v.Id;
		CustomMinimumSize = size; Size = size;
		int font = (int)Math.Clamp(size.X / 8f, 13, 26);
		var icon = UiFind.Get<Label>(this, "IconLabel");
		var type = UiFind.Get<Label>(this, "TypeLabel");
		var name = UiFind.Get<Label>(this, "NameLabel");
		var stateLabel = UiFind.Get<Label>(this, "StateLabel");
		var tex = UiFind.Get<TextureRect>(this, "IconTexture");
		icon.Text = look.Icon; type.Text = look.Label; name.Text = v.Yokai ?? look.Label;
		tex.Texture = look.IconTexture; tex.Visible = look.IconTexture != null; icon.Visible = look.IconTexture == null;
		stateLabel.Text = MapNodePalette.Caption(v.State, cursor);
		stateLabel.Visible = stateLabel.Text != "";
		var tag = UiFind.Get<Label>(this, "TagLabel");
		tag.Text = look.Tagline; tag.Visible = look.Tagline != "";		foreach (var l in new[] { icon, type, name, stateLabel, tag })
			l.AddThemeFontSizeOverride("font_size", l == name ? font + 2 : l == tag ? font - 8 : font - 2);
		var elder = UiFind.Get<Control>(this, "ElderMark");
		elder.Visible = v.Elder;
		TwistIconId = v.Elder && twistLook != null ? v.TwistIcon ?? "" : "";
		// YOK-139 (Y3): an elder with twist data wears its twist's seal (red stamp, placeholder shape) in place of the plain "E".
		var seal = elder.GetNodeOrNull<TwistSeal>("TwistSeal");
		if (seal == null) { seal = new TwistSeal { Name = "TwistSeal", CustomMinimumSize = new Vector2(30, 30), SizeFlagsVertical = SizeFlags.ShrinkCenter }; elder.AddChild(seal); }
		seal.Visible = TwistIconId != "";
		if (seal.Visible) { seal.Icon = TwistIconId; seal.Fallback = twistLook!.Value.Icon; }
		UiFind.Get<Label>(this, "ElderGlyph").Visible = !seal.Visible;

		bool reachable = v.State == MapNodeState.Reachable;
		Disabled = !reachable;
		MouseDefaultCursorShape = reachable ? CursorShape.PointingHand : CursorShape.Arrow;
		bool lit = MapNodePalette.Lit(v.State);
		var box = new StyleBoxFlat
		{
			BgColor = MapNodePalette.Fill(v.State),
			BorderColor = MapNodePalette.Border(v.State, cursor, look.Color),
			CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
		};
		box.SetBorderWidthAll(v.State == MapNodeState.PassedOver ? 0 : cursor ? 7 : lit ? 4 : 2);
		foreach (var s in new[] { "normal", "hover", "pressed", "disabled", "focus" }) AddThemeStyleboxOverride(s, box);
		var dimText = v.State == MapNodeState.Visited ? new Color(0.78f, 0.76f, 0.72f) : v.State == MapNodeState.PassedOver ? new Color(0.5f, 0.52f, 0.6f) : new Color(0.62f, 0.63f, 0.70f);
		icon.AddThemeColorOverride("font_color", lit ? look.Color : dimText);
		type.AddThemeColorOverride("font_color", lit ? look.Color : dimText);
		name.AddThemeColorOverride("font_color", lit ? MapNodePalette.Ink : dimText);
		tag.AddThemeColorOverride("font_color", lit ? new Color(MapNodePalette.Ink, 0.8f) : dimText);
		stateLabel.AddThemeColorOverride("font_color", v.State == MapNodeState.Current ? MapNodePalette.ElderRed : lit ? new Color(MapNodePalette.Ink, 0.8f) : dimText);
		elder.AddThemeStyleboxOverride("panel", seal.Visible ? new StyleBoxEmpty() : new StyleBoxFlat { BgColor = MapNodePalette.ElderRed, CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12, CornerRadiusBottomLeft = 12, CornerRadiusBottomRight = 12 });
		UiFind.Get<Label>(this, "ElderGlyph").AddThemeColorOverride("font_color", MapNodePalette.Paper);
		ZIndex = cursor ? 2 : 0;
		QueueRedraw();
	}

	/// <summary>Over the button's own box: the dashed edge of a passed-over node, the gold glow and pointer of the selected one.</summary>
	public override void _Draw()
	{
		var r = new Rect2(Vector2.Zero, Size);
		if (State == MapNodeState.PassedOver)
		{
			var c = MapNodePalette.PassedOverLine;
			DrawDashedLine(r.Position, new Vector2(r.End.X, 0), c, 2f, 8f);
			DrawDashedLine(new Vector2(r.End.X, 0), r.End, c, 2f, 8f);
			DrawDashedLine(r.End, new Vector2(0, r.End.Y), c, 2f, 8f);
			DrawDashedLine(new Vector2(0, r.End.Y), r.Position, c, 2f, 8f);
		}
		if (CursorOn)
		{
			for (int i = 2; i >= 1; i--) DrawRect(r.Grow(i * 4f), new Color(MapNodePalette.Gold, 0.25f), false, 4f);
			float y = Size.Y / 2;
			DrawColoredPolygon(new[] { new Vector2(-24, y - 16), new Vector2(-24, y + 16), new Vector2(-6, y) }, MapNodePalette.Gold);
		}
	}
}
