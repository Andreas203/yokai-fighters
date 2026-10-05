using Godot;

namespace YokaiFighters;

/// <summary>Boot scene. Goes straight to the fight (YOK-15) until the title screen lands.</summary>
public partial class Main : Node
{
	public override void _Ready()
	{
		GD.Print($"Yokai Fighters booted: {Engine.PhysicsTicksPerSecond} ticks/s");
		GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, "res://scenes/fight.tscn");
	}
}
