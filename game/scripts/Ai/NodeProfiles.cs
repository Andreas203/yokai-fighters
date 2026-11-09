using System;
using System.Linq;
using YokaiFighters.Sim;

namespace YokaiFighters.Ai;

/// <summary>
/// YOK-174 (designer 2026-10-21, Y1): the behaviour profiles a fight node can fight with, shared by the fight scene
/// and the harness (and its profile check). The yokai the node plays as gives its own profiles (aggressive first;
/// the TEST FIXTURE profiles when it has none). A node that names a profile which is not one of them (the
/// apprentice's own profile, whose <c>yokai</c> is "apprentice" while it plays as the Kitsune for body and kit) gets
/// that profile appended, found among all of data/profiles/. A named profile found nowhere is an error, never a fallback.
/// YOK-211: <see cref="Pick(NodeContext, BehaviourProfile[], BehaviourProfile[], uint, string?)"/> is the one habit pick the
/// map's habit line, the fight scene and the harness all make, so the map always shows the profile the fight uses.
/// </summary>
public static class NodeProfiles
{
	/// <summary>A yokai's own profiles, else the fixtures; sorted by temperament (aggressive first).</summary>
	public static BehaviourProfile[] Of(string yokai, BehaviourProfile[] all, BehaviourProfile[] fixtures)
	{
		var own = Array.FindAll(all, p => p.Yokai == yokai);
		if (own.Length == 0) own = (BehaviourProfile[])fixtures.Clone();
		Array.Sort(own, (a, b) => string.CompareOrdinal(a.Temperament, b.Temperament));
		return own;
	}

	/// <summary>
	/// <paramref name="own"/> (the plays-as yokai's profiles), plus the profile <paramref name="named"/> from
	/// <paramref name="all"/> when it is not already among them. Throws <see cref="InvalidOperationException"/> when
	/// <paramref name="named"/> is in neither.
	/// </summary>
	public static BehaviourProfile[] WithNamed(string nodeId, BehaviourProfile[] own, string? named, BehaviourProfile[] all)
	{
		if (named is null || own.Any(p => p.Id == named)) return own;
		var found = all.FirstOrDefault(p => p.Id == named)
			?? throw new InvalidOperationException($"node '{nodeId}' names profile '{named}', which is not in data/profiles/");
		return own.Append(found).ToArray();
	}

	/// <summary>
	/// YOK-211: the profile a node's opponent fights with, from every profile in data/profiles/ (<paramref name="all"/>):
	/// the plays-as yokai's own (<see cref="Of"/>, else <paramref name="fixtures"/>), plus the node's named one
	/// (<see cref="WithNamed"/>), then <see cref="PickFrom"/>. Throws <see cref="InvalidOperationException"/> when the node
	/// names a profile found nowhere.
	/// </summary>
	public static NodePick Pick(NodeContext node, BehaviourProfile[] all, BehaviourProfile[] fixtures, uint runSeed, string? debugTemperament = null) =>
		PickFrom(node, Of(node.MovesAndProfilesOf, all, fixtures), all, runSeed, debugTemperament);

	/// <summary>
	/// YOK-211: <see cref="Pick(NodeContext, BehaviourProfile[], BehaviourProfile[], uint, string?)"/> over profiles the
	/// caller already has (<paramref name="own"/>, sorted as <see cref="Of"/>). The node's named profile is kept (no roll);
	/// otherwise E23's debug temperament when one of the profiles has it, else the node's seeded 50/50 roll (Y1/Y2,
	/// <see cref="NodeTemperament"/>). Index -1 only when there is no profile at all.
	/// </summary>
	public static NodePick PickFrom(NodeContext node, BehaviourProfile[] own, BehaviourProfile[] all, uint runSeed, string? debugTemperament = null)
	{
		var profiles = WithNamed(node.NodeId, own, node.Profile, all);
		if (profiles.Length == 0) return new NodePick(profiles, -1);
		if (node.Profile is null && debugTemperament is { } t && Array.FindIndex(profiles, p => p.Temperament == t) is var d and >= 0)
			return new NodePick(profiles, d); // E23: the debug temperament holds over the roll
		return new NodePick(profiles, Math.Max(0, NodeTemperament.Pick(profiles, node, runSeed))); // a named profile is in profiles now
	}
}

/// <summary>YOK-211: a node's profiles (<see cref="Profiles"/>) and the index of the one its opponent fights with (-1 = none).</summary>
public readonly record struct NodePick(BehaviourProfile[] Profiles, int Index)
{
	public BehaviourProfile? Profile => Index >= 0 ? Profiles[Index] : null;
}
