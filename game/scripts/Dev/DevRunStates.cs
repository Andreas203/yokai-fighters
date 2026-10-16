using YokaiFighters.Sim;

namespace YokaiFighters.Dev;

/// <summary>
/// YOK-125: where a dev jump gets Ryo's run state from. The default is a fresh run (starters at Lv 1, full health,
/// no coins, A1/C1). The YOK-126 run-state editor plugs in here by setting <see cref="DevRunStates.Source"/>; every
/// jump asks it for a new <see cref="RunState"/>, so a jump never shares or edits the editor's own copy.
/// </summary>
public interface IDevRunStateSource
{
	/// <summary>A new run state over <paramref name="pool"/>, with no map (the jump gives it its own).</summary>
	RunState Build(AbilityPool pool);

	/// <summary>One line for the Jump page: what the run state is ("defaults", or the editor's summary).</summary>
	string Describe { get; }
}

/// <summary>The defaults: <see cref="RunState.NewRun"/> (A1 starters at Lv 1, 1,000 health, 0 coins).</summary>
public sealed class DefaultDevRunState : IDevRunStateSource
{
	public RunState Build(AbilityPool pool) => RunState.NewRun(pool);
	public string Describe => "Run state: the defaults (starters at Lv 1, full health, no coins).";
}

public static class DevRunStates
{
	public static IDevRunStateSource Source { get; set; } = new DefaultDevRunState();
	public static void Reset() => Source = new DefaultDevRunState();
}
