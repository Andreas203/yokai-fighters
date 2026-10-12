using YokaiFighters.Sim;

namespace YokaiFighters.SaveGame;

/// <summary>
/// YOK-106: Ryo's first meeting with each elder (Y3: the intro card and the callout show on first meeting).
/// "Met" is story progress, kept in the save's story section (M4, <see cref="StoryProgress.EldersMet"/>), per save:
/// it outlives the run. It is set when an elder node is entered, at that node-boundary save (M5), never in a fight.
/// <para>
/// An elder is whatever the map data says: a node with <see cref="MapNodeData.Elder"/> set, known by the yokai it
/// names (the same elder can wait on more than one node, e.g. the Elder Oni on rows 5 and 8). No id is hard-coded.
/// </para>
/// <para>
/// Quit and Continue inside the elder node (M5: the node restarts as Ryo entered it): the entry save holds the elder
/// as met and <see cref="RunSave.FirstMeeting"/>, so <see cref="Resume"/> gives the same answer the entry did. The
/// meeting is neither lost (the flag is already in the file) nor doubled (it is a set; a restarted first meeting is
/// still the first). Pure: no Godot, no clock; not fight state, so in neither <c>Match.StateHash</c> nor
/// <see cref="RunSession.StateHash"/>.
/// </para>
/// </summary>
public static class ElderMeetings
{
	/// <summary>The elder waiting on this node (the yokai id it names), or null when the node is not an elder's.</summary>
	public static string? ElderOf(MapNodeData? node) =>
		node is { Elder: true, Yokai: { } yokai } && !string.IsNullOrWhiteSpace(yokai) ? yokai : null;

	/// <summary>
	/// The node-entered save point: the save with the node's elder marked met, and whether this entry is the first
	/// meeting (an elder not met before it). A node that is not an elder's changes nothing and is never a meeting.
	/// </summary>
	public static SaveData Enter(SaveData saved, MapNodeData? node, out bool firstMeeting)
	{
		firstMeeting = false;
		if (ElderOf(node) is not { } elder) return saved;
		firstMeeting = !saved.Story.HasMetElder(elder);
		return saved.WithElderMet(elder);
	}

	/// <summary>
	/// Continue: a run saved inside a node goes back into that node from its start (M5). First meeting when the entry
	/// save said so, or when the elder is not in the story section (a version 3 or older file saved inside an elder
	/// node). The elder is marked met in the returned save, which the next save point writes.
	/// Every elder whose node the walk already completed is marked met first: a version 3 or older file has no
	/// <c>elders_met</c>, but its walk shows who Ryo met (an Elder Oni passed on row 5 is no first meeting on row 8).
	/// </summary>
	public static SaveData Resume(SaveData saved, RunSession session, out bool firstMeeting)
	{
		firstMeeting = false;
		foreach (string visited in session.Map.Visited)
			if (ElderOf(session.Map.Graph.Find(visited)) is { } met) saved = saved.WithElderMet(met);
		if (!session.Map.InNode || ElderOf(session.Map.CurrentNode) is not { } elder) return saved;
		firstMeeting = saved.Run is { FirstMeeting: true } || !saved.Story.HasMetElder(elder);
		return saved.WithElderMet(elder);
	}
}
