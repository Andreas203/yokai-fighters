using System;
using System.Linq;

namespace YokaiFighters.Ai;

/// <summary>
/// YOK-174 (designer 2026-10-21, Y1): the behaviour profiles a fight node can fight with, shared by the fight scene
/// and the harness (and its profile check). The yokai the node plays as gives its own profiles (aggressive first;
/// the TEST FIXTURE profiles when it has none). A node that names a profile which is not one of them (the
/// apprentice's own profile, whose <c>yokai</c> is "apprentice" while it plays as the Kitsune for body and kit) gets
/// that profile appended, found among all of data/profiles/. A named profile found nowhere is an error, never a fallback.
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
}
