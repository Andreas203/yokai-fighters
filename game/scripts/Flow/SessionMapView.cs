using System.Collections.Generic;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Flow;

/// <summary>
/// YOK-77: the map screen's <see cref="IMapView"/> for a run in the flow. Nodes and routes are YOK-78's
/// <see cref="RunMapView"/> over the session's map; the one difference is <see cref="Select"/>, which enters the node
/// through <see cref="RunSession.Enter"/> so the node is entered exactly once, its scene and
/// <see cref="NodeContext"/> are resolved, and <see cref="RunSession.NodeEntered"/> fires (T1: for the Tanuki's node, <see cref="RunSession.LockedCopy"/> is set by then).
/// </summary>
public sealed class SessionMapView : IMapView
{
	readonly RunSession _session;
	readonly RunMapView _map;

	public SessionMapView(RunSession session)
	{
		_session = session;
		_map = new RunMapView(session.Map, session.Run, session.Scenes, session.Seed);
	}

	public IReadOnlyList<MapNodeView> Nodes => _map.Nodes;
	public IReadOnlyList<MapRouteView> Routes => _map.Routes;
	public MapStatusView? Status => _map.Status;
	/// <summary>T1, T6: live target (T2-T4, <c>CopyRule.Target</c>) until the Tanuki's node is entered, then the locked copy. Read-only.</summary>
	public CopyTarget? CopyTarget => _session.CopyLocked ? _session.LockedCopy : _map.CopyTarget;
	public bool CopyLocked => _session.CopyLocked;
	public object? RunKey => _session;
	public MapNodeDetail? Detail(string nodeId) => _map.Detail(nodeId);
	public IReadOnlyList<MapTwistLegendEntry> TwistLegend => _map.TwistLegend;
	public bool Select(string nodeId) => _session.Enter(nodeId).Ok;
}

/// <summary>
/// YOK-77: a node scene that is not the fight scene (a merchant or dojo screen later). The flow hands it the run
/// before it enters the tree; it finishes by calling <see cref="RunSession.CompleteNode"/> (the flow then shows
/// the map) or <see cref="RunSession.LoseFight"/>.
/// </summary>
public interface INodeScene
{
	void BindRun(RunSession session, ControlScheme scheme);
}
