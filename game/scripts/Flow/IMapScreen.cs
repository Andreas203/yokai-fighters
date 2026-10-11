using YokaiFighters.Sim;

namespace YokaiFighters.Flow;

/// <summary>
/// YOK-77: the seam between the run flow and the map screen. The node at <c>Menus/Map</c> in <c>scenes/boot.tscn</c>
/// implements this and declares the Godot signal <c>NodeChosen(string nodeId)</c>; <see cref="GameFlow"/> connects
/// it to <see cref="GameFlow.ChooseNode"/>. That is everything the flow needs: "show the map of this run" and
/// "the player picked this node". The screen reads the run through <see cref="RunSession.Map"/>
/// (<c>Reachable()</c>, <c>Visited</c>, <c>Current</c>, <c>Graph</c>) and never changes it.
/// <para>
/// Optional: a signal <c>TitleRequested()</c> (leave the run for the title) is connected when the node has it.
/// Today the node is <see cref="DebugMapList"/>, a stand-in; YOK-78's <c>ui/map_screen.tscn</c> replaces it.
/// </para>
/// </summary>
public interface IMapScreen
{
	public const string NodeChosenSignal = "NodeChosen";
	public const string TitleRequestedSignal = "TitleRequested";

	/// <summary>Show the map of <paramref name="session"/> as it stands now and take focus. Called every time the map is entered (new run, and after each node).</summary>
	void ShowMap(RunSession session);
	/// <summary>Hide and stop taking input (a node's scene or the title is showing).</summary>
	void HideMap();
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
