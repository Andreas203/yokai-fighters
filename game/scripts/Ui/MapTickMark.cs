using System;
using Godot;

namespace YokaiFighters.Ui;

/// <summary>YOK-238: the small red tick on a cleared node's tag.</summary>
public partial class MapTickMark : Control
{
	public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;
	public override void _Draw() { float s = Mathf.Min(Size.X, Size.Y); MapNodeArt.Tick(this, new Rect2((Size - new Vector2(s, s)) / 2f, new Vector2(s, s))); }
}
