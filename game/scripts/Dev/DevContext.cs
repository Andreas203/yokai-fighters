using System;
using YokaiFighters.Fight;
using YokaiFighters.Flow;

namespace YokaiFighters.Dev;

/// <summary>Where the dev menu was opened from.</summary>
public enum DevPlace { Title, Map, Fight }

/// <summary>
/// YOK-123: what a dev menu page may see and do. Handed to every section's builder each time the menu opens.
/// <para>
/// Determinism (F3, YOK-122): dev actions change the run only between nodes, never inside a fight. A page that changes
/// the run or the flow goes through <see cref="TryChangeRun"/>, which refuses while <see cref="Place"/> is
/// <see cref="DevPlace.Fight"/> (the menu is then over the pause menu, the sim frozen); pages may still read state there.
/// </para>
/// </summary>
public sealed class DevContext
{
	public const string MidFightReason = "Inside a fight: run changes wait for the map (dev actions never touch a fight in progress).";
	public const string DevOffReason = "Dev mode is off.";

	public DevPlace Place { get; }
	/// <summary>The boot flow (null when a scene runs on its own, e.g. fight.tscn opened in the editor).</summary>
	public GameFlow? Flow { get; }
	/// <summary>The paused fight when opened from the pause menu; null otherwise.</summary>
	public FightScene? Fight { get; }

	public DevContext(DevPlace place, GameFlow? flow = null, FightScene? fight = null)
	{
		Place = place;
		Flow = flow;
		Fight = fight;
	}

	/// <summary>On the title (no run) or the map (between nodes): the only places a dev action may change the run.</summary>
	public bool AtNodeBoundary => Place != DevPlace.Fight;

	/// <summary>Why a run change is refused right now; null when it is allowed (dev mode on, at a node boundary).</summary>
	public string? RunChangeRefusal => !DevMode.On ? DevOffReason : !AtNodeBoundary ? MidFightReason : null;

	/// <summary>Runs <paramref name="change"/> only when <see cref="RunChangeRefusal"/> is null; otherwise does nothing and gives the reason.</summary>
	public bool TryChangeRun(Action change, out string? refusal)
	{
		refusal = RunChangeRefusal;
		if (refusal != null) return false;
		change();
		return true;
	}
}
