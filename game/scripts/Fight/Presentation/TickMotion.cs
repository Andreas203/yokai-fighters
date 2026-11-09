using Godot;

namespace YokaiFighters.Fight;

/// <summary>
/// YOK-150/152: a gentle bob and sway driven by the sim tick (<see cref="StageClock.Tick"/>): the tea house's hanging
/// lanterns sway (the bob served the riverbank's floating lanterns, removed 2026-11-09). The node sits at its rest transform plus
/// <c>offset = A * sin(tick * w + phase)</c>, w = 2 pi / <see cref="PeriodTicks"/>: no physics, no wall clock, so a replay
/// or a capture shows the same pose on the same tick. Visual only.
/// </summary>
[GlobalClass]
public partial class TickMotion : Node3D, IStageScenery
{
	/// <summary>Vertical bob amplitude in metres (0 = none).</summary>
	[Export] public float BobMetres { get; set; }
	/// <summary>Sway amplitude about the local z axis, in degrees (0 = none); about the node's origin, so put the origin at the hanging point.</summary>
	[Export] public float SwayDegrees { get; set; }
	/// <summary>Ticks per full cycle (60 = one second); below 2 counts as 2.</summary>
	[Export] public int PeriodTicks { get; set; } = 180;
	/// <summary>Phase offset in ticks, so neighbours do not move in step.</summary>
	[Export] public int PhaseTicks { get; set; }

	private Transform3D? _rest;

	/// <summary>The rest transform (the scene's placement), taken the first time the node is posed.</summary>
	public Transform3D Rest => _rest ??= Transform;

	/// <summary>Pure: sin(2 pi (tick + phase) / period), with (tick + phase) taken modulo the period in integers first (no float drift over a long fight).</summary>
	public static float Wave(int tick, int periodTicks, int phaseTicks)
	{
		int period = Mathf.Max(2, periodTicks);
		long t = ((long)tick + phaseTicks) % period;
		if (t < 0) t += period;
		return Mathf.Sin(Mathf.Tau * t / period);
	}

	public void Present(StageClock clock)
	{
		Transform3D rest = Rest;
		float w = Wave(clock.Tick, PeriodTicks, PhaseTicks);
		var basis = rest.Basis * new Basis(Vector3.Back, Mathf.DegToRad(SwayDegrees * w));
		Transform = new Transform3D(basis, rest.Origin + Vector3.Up * (BobMetres * w));
	}
}
