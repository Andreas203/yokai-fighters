using Godot;

namespace YokaiFighters;

/// <summary>Boot scene. Confirms the C# assembly loads; replaced as real screens land.</summary>
public partial class Main : Node
{
	public override void _Ready()
	{
		GD.Print($"Yokai Fighters booted: {Engine.PhysicsTicksPerSecond} ticks/s");
	}
}
