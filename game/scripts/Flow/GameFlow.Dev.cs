using System;
using Godot;
using YokaiFighters.Dev;
using YokaiFighters.Fight;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Flow;

/// <summary>
/// YOK-125: the dev Jump page's two ways in. <see cref="StartDevRun"/> runs a dev-built session through the normal
/// flow (map → node scene), and <see cref="OpenDevScreen"/> shows one screen on its own over a hidden title or map.
/// Both only from the title or the map (a node boundary), never over a fight. A dev-built session is never saved, in
/// either slot: it is not a run the player (or the dev slot's Continue) should come back to.
/// </summary>
public partial class GameFlow
{
	/// <summary>The screen open in the dev viewer (null when none).</summary>
	public DevScreenHost? DevScreen { get; private set; }

	/// <summary>Stage for the next dev fight (null = leave the launch setting); applied while the fight scene readies.</summary>
	private DevStage? _devStage;

	/// <summary>
	/// Replaces the run with <paramref name="session"/> (dev-built, never saved) and enters <paramref name="nodeId"/>,
	/// played with <paramref name="scheme"/>. From the map the old run is left as of its last save (as Quit to title
	/// does). Refused anywhere but the title or the map, or while the dev viewer is open.
	/// </summary>
	public MapResult StartDevRun(RunSession session, string nodeId, ControlScheme scheme, DevStage? stage = null)
	{
		if (DevScreen != null) return MapResult.Fail("the dev viewer is open");
		if (Router.Current == Screen.Map) Router.ReturnToTitle();
		if (Router.Current != Screen.Title) return MapResult.Fail("dev jumps start from the title or the map");
		Router.ChooseScheme(scheme);
		_freshRun = false;
		_unsavedSession = session; // before Adopt: no save point of this session reaches a file
		Session = session;
		Adopt(session);
		_devStage = stage;
		Router.ContinueRun(); // Title → Map on this session
		var entered = ChooseNode(nodeId);
		_devStage = null;
		if (!entered.Ok) GD.PushWarning($"GameFlow: dev jump to '{nodeId}' refused: {entered.Error}");
		return entered;
	}

	/// <summary>Called by <see cref="BeginFight"/> before the fight scene enters the tree; gives the launch setting back once it is ready.</summary>
	private void ApplyDevStage(FightScene fight)
	{
		if (_devStage is not { } stage) return;
		bool launch = FightScene.UsePlaceholderStage;
		FightScene.UsePlaceholderStage = stage == DevStage.Placeholder;
		fight.Ready += () => FightScene.UsePlaceholderStage = launch;
	}

	/// <summary>
	/// Shows <paramref name="screen"/> on its own in the dev viewer, over the title or the map (hidden meanwhile, so
	/// nothing underneath takes focus or input). Closed by its Close button, Esc / pad B the screen leaves unhandled,
	/// or the screen's own finishing event (<see cref="DevScreenHost.Finish"/>). False anywhere else.
	/// </summary>
	public bool OpenDevScreen(string title, Control screen, Action<DevScreenHost>? wire = null)
	{
		if (DevScreen != null || Router.Current is not (Screen.Title or Screen.Map)) { screen.QueueFree(); return false; }
		Control under = Router.Current == Screen.Title ? Title : Map;
		under.Visible = false;
		var host = new DevScreenHost(title, screen) { Name = "DevScreen" };
		host.Closed += () =>
		{
			DevScreen = null;
			if (!IsInstanceValid(under)) return;
			under.Visible = true;
			if (under == Title) Title.StartButton.GrabFocus();
		};
		DevScreen = host;
		AddChild(host); // last child: it sees unhandled input before the menus
		wire?.Invoke(host);
		return true;
	}
}
