using System;
using Godot;

namespace YokaiFighters.Ui;

/// <summary>
/// One node on the map screen (<c>scenes/ui/map_node.tscn</c>): a button showing the type label and icon, the yokai that waits
/// there (R2), the elder marker (Y3) and a state style. Only reachable nodes are enabled, so a click cannot pick any other.
/// The look comes in as a <see cref="MapNodeLook"/> (from the styles resource); nothing here names a node type.
/// </summary>
public partial class MapNode : Button
{
	static readonly Color Paper = new(0.945f, 0.91f, 0.831f), Ink = new(0.114f, 0.106f, 0.129f), Persimmon = new(0.89f, 0.42f, 0.24f), ElderRed = new(0.72f, 0.2f, 0.16f);

	public string NodeId { get; private set; } = "";
	public MapNodeState State { get; private set; } = MapNodeState.Locked;
	public bool CursorOn { get; private set; }
	public string TypeText => UiFind.Get<Label>(this, "TypeLabel").Text;
	public string YokaiText => UiFind.Get<Label>(this, "NameLabel").Text;
	public bool ElderShown => UiFind.Get<Control>(this, "ElderMark").Visible;

	public event Action<MapNode>? Chosen, Hovered;

	public override void _Ready()
	{
		FocusMode = FocusModeEnum.None; // the screen owns the cursor; Godot focus never reaches a locked node
		Pressed += () => Chosen?.Invoke(this);
		MouseEntered += () => Hovered?.Invoke(this);
	}

	public void Bind(MapNodeView v, MapNodeLook look, Vector2 size, bool cursor)
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
		icon.Text = look.Icon; type.Text = look.Label; name.Text = v.Yokai ?? "";
		tex.Texture = look.IconTexture; tex.Visible = look.IconTexture != null; icon.Visible = look.IconTexture == null;
		stateLabel.Text = v.State switch { MapNodeState.Current => "HERE", MapNodeState.Visited => "cleared", _ => "" };
		stateLabel.Visible = stateLabel.Text != "";
		foreach (var l in new[] { icon, type, name, stateLabel })
			l.AddThemeFontSizeOverride("font_size", l == name ? font + 2 : font - 2);
		var elder = UiFind.Get<Control>(this, "ElderMark");
		elder.Visible = v.Elder;

		bool reachable = v.State == MapNodeState.Reachable;
		Disabled = !reachable;
		MouseDefaultCursorShape = reachable ? CursorShape.PointingHand : CursorShape.Arrow;
		bool lit = v.State is MapNodeState.Reachable or MapNodeState.Current;
		var box = new StyleBoxFlat
		{
			BgColor = lit ? Paper : v.State == MapNodeState.Visited ? new Color(0.40f, 0.39f, 0.40f) : new Color(0.20f, 0.22f, 0.30f),
			BorderColor = cursor ? new Color(0.98f, 0.8f, 0.3f) : v.State == MapNodeState.Current ? Persimmon : lit ? look.Color : new Color(look.Color, 0.6f),
			CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
		};
		box.SetBorderWidthAll(cursor ? 7 : lit ? 4 : 2);
		foreach (var s in new[] { "normal", "hover", "pressed", "disabled", "focus" }) AddThemeStyleboxOverride(s, box);
		var dimText = v.State == MapNodeState.Visited ? new Color(0.78f, 0.76f, 0.72f) : new Color(0.62f, 0.63f, 0.70f);
		icon.AddThemeColorOverride("font_color", lit ? look.Color : dimText);
		type.AddThemeColorOverride("font_color", lit ? look.Color : dimText);
		name.AddThemeColorOverride("font_color", lit ? Ink : dimText);
		stateLabel.AddThemeColorOverride("font_color", v.State == MapNodeState.Current ? ElderRed : dimText);
		elder.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = ElderRed, CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12, CornerRadiusBottomLeft = 12, CornerRadiusBottomRight = 12 });
		UiFind.Get<Label>(this, "ElderGlyph").AddThemeColorOverride("font_color", Paper);
		ZIndex = cursor ? 2 : 0;
	}
}
