using System.Collections.Generic;
using Godot;

namespace YokaiFighters.Fight;

/// <summary>
/// YOK-39: root of a stage scene (<c>scenes/stage/*.tscn</c>): environment, lights, backdrop, floor and props, all
/// placed in the editor. Purely visual; the fight never reads it. <see cref="Kind"/> names the stage for tests/logs.
/// YOK-150: the fight presentation hands it the <see cref="StageClock"/> once per render (<see cref="Present"/>), and it
/// passes that on to every <see cref="IStageScenery"/> node under it (bobbing lanterns, swaying lamps, the tea house's
/// season screens). The stage reads the clock; the sim never reads the stage.
/// </summary>
[GlobalClass]
public partial class FightStage : Node3D
{
	[Export] public string Kind { get; set; } = "";

	private List<IStageScenery>? _scenery;

	/// <summary>Every <see cref="IStageScenery"/> under the stage (found once, on first use).</summary>
	public IReadOnlyList<IStageScenery> Scenery => _scenery ??= Collect(this, new List<IStageScenery>());

	/// <summary>The last clock presented (round 0, tick 0 before the first).</summary>
	public StageClock Clock { get; private set; }

	/// <summary>Shows the stage at <paramref name="clock"/>: each scenery node poses itself from it (a pure function of the clock).</summary>
	public void Present(StageClock clock)
	{
		Clock = clock;
		foreach (var s in Scenery) s.Present(clock);
	}

	private static List<IStageScenery> Collect(Node n, List<IStageScenery> into)
	{
		foreach (Node c in n.GetChildren())
		{
			if (c is IStageScenery s) into.Add(s);
			Collect(c, into);
		}
		return into;
	}
}

/// <summary>
/// YOK-150: what the fight presentation tells the stage each render. <see cref="Round"/> counts the round starts the
/// presentation has seen on this node's fight (1 = the first; a reset or rematch starts the next, C1: one round per
/// fight); <see cref="Tick"/> is the sim tick of the round in progress (<c>Match.Tick</c>, 0 at the round start).
/// </summary>
public readonly record struct StageClock(int Round, int Tick);

/// <summary>YOK-150: stage scenery that moves or changes with the <see cref="StageClock"/> only (no wall clock, no physics, no free-running animation).</summary>
public interface IStageScenery
{
	void Present(StageClock clock);
}
