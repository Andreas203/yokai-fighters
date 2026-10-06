using Godot;

namespace YokaiFighters.Fight;

/// <summary>
/// YOK-39: root of a stage scene (<c>scenes/stage/*.tscn</c>): environment, lights, backdrop, floor and props, all
/// placed in the editor. Purely visual; the fight never reads it. <see cref="Kind"/> names the stage for tests/logs.
/// </summary>
[GlobalClass]
public partial class FightStage : Node3D
{
	[Export] public string Kind { get; set; } = "";
}
