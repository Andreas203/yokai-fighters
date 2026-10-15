using System.Linq;
using YokaiFighters.Flow;
using YokaiFighters.Sim;

namespace YokaiFighters.Dev;

/// <summary>
/// YOK-124: the dev menu's map tools. "Full map unlocked" sets <see cref="RunMap.FreeTravel"/> on the run's map, so
/// every node not yet walked is selectable on the map screen (and from the dev page); entering one still goes through
/// <see cref="RunSession.Enter"/> and <see cref="GameFlow.ChooseNode"/>, so the node's real rules run: the T1 copy lock on
/// the Tanuki's node, the elder fight and its draft, the merchant and dojo stops (R6, R7), the M5 save points (in the
/// dev slot: dev mode is on whenever this is). Nothing here touches a fight in progress: every change goes through
/// <see cref="DevContext.TryChangeRun"/>.
/// </summary>
public static class DevMap
{
	public const string NoRunReason = "No run in progress: start one, then travel from the map.";
	public const string NotOnMapReason = "Travel works from the map, between nodes.";
	public const string MetaUnlocksPending = "Meta-unlocks are not built yet (YOK-86): nothing to unlock.";

	/// <summary>The "Full map unlocked" toggle. It applies only while dev mode is on (<see cref="Active"/>).</summary>
	public static bool FullMapUnlocked { get; private set; }

	/// <summary>The "Reveal every node" toggle: the dev page lists every node's type and elder twist.</summary>
	public static bool RevealAll { get; private set; }

	/// <summary>Free travel is in force: dev mode on and the toggle set.</summary>
	public static bool Active => DevMode.On && FullMapUnlocked;

	/// <summary>Puts the toggle's state on <paramref name="session"/>'s map (the flow calls it each time the map shows). Off = the routes, R1.</summary>
	public static void Apply(RunSession? session)
	{
		if (session is not null) session.Map.FreeTravel = Active;
	}

	/// <summary>Sets "Full map unlocked" and applies it to the run on the map at once. Refused (nothing changes) mid-fight or with dev mode off.</summary>
	public static bool SetFullMap(DevContext ctx, bool on, out string? refusal) => ctx.TryChangeRun(() =>
	{
		FullMapUnlocked = on;
		Apply(ctx.Flow?.Session);
		if (ctx.Place == DevPlace.Map) ctx.Flow?.Map.Refresh(); // the screen re-reads which nodes are selectable
	}, out refusal);

	/// <summary>Sets "Reveal every node". It changes no run state, but follows the same gate as the rest of the page.</summary>
	public static bool SetReveal(DevContext ctx, bool on, out string? refusal) => ctx.TryChangeRun(() => RevealAll = on, out refusal);

	/// <summary>Why <paramref name="nodeId"/> cannot be travelled to now; null when it can.</summary>
	public static string? TravelRefusal(DevContext ctx, string nodeId)
	{
		if (ctx.RunChangeRefusal is { } r) return r;
		if (ctx.Flow?.Session is not { } session) return NoRunReason;
		if (ctx.Place != DevPlace.Map) return NotOnMapReason;
		if (!session.Reachable().Contains(nodeId))
			return session.Map.Graph.Has(nodeId) ? $"'{nodeId}' is not selectable now{(Active ? "" : " (Full map unlocked is off: the routes apply)")}." : $"'{nodeId}' is not on this map.";
		return null;
	}

	/// <summary>
	/// Travels to <paramref name="nodeId"/> the way the map screen does (<see cref="GameFlow.ChooseNode"/>: enter, then the
	/// node's scene). Refused with the reason and no change off the map, mid-fight, or for a node not selectable now.
	/// </summary>
	public static bool Travel(DevContext ctx, string nodeId, out string? refusal)
	{
		refusal = TravelRefusal(ctx, nodeId);
		if (refusal != null) return false;
		string? error = null;
		ctx.TryChangeRun(() => { if (ctx.Flow!.ChooseNode(nodeId) is { Ok: false } step) error = step.Error; }, out _);
		refusal = error;
		return error == null;
	}

	/// <summary>
	/// The "Unlock all 12 meta-unlocks" hook. A no-op until the unlocks exist (YOK-86): returns false with
	/// <see cref="MetaUnlocksPending"/>. YOK-86 fills this in (dev slot only).
	/// </summary>
	public static bool UnlockAllMetaUnlocks(DevContext ctx, out string? refusal)
	{
		if (ctx.RunChangeRefusal is { } r) { refusal = r; return false; }
		refusal = MetaUnlocksPending;
		return false;
	}

	/// <summary>Test hook: both toggles off.</summary>
	public static void Reset() => (FullMapUnlocked, RevealAll) = (false, false);
}
