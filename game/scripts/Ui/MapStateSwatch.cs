using System;
using Godot;

namespace YokaiFighters.Ui;

/// <summary>YOK-238: one of the legend's four marks, drawn the way a node wears it.</summary>
public partial class MapStateSwatch : Control
{
	MapMark _mark;
	[Export] public MapMark Mark { get => _mark; set { _mark = value; QueueRedraw(); } }
	public override void _Ready() { MouseFilter = MouseFilterEnum.Ignore; CustomMinimumSize = new Vector2(36, 22); }
	public override void _Draw() => MapNodeArt.Swatch(this, new Rect2(Vector2.Zero, Size), _mark);
}
