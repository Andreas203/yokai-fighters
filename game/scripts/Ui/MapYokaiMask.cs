using Godot;

namespace YokaiFighters.Ui;

/// <summary>YOK-238: one yokai's mask on the map. <c>Yokai</c> is the yokai id (data/map node <c>yokai</c>) or its lower-case display name.</summary>
[GlobalClass]
public partial class MapYokaiMask : Resource
{
	[Export] public string Yokai { get; set; } = "";
	[Export] public Texture2D? Mask { get; set; }
}
