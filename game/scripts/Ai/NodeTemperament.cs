using System;
using YokaiFighters.Sim;

namespace YokaiFighters.Ai;

/// <summary>
/// YOK-174, Y1/Y2 (designer 2026-10-20): which temperament a node's yokai fights with. A node that names a profile
/// keeps it (no roll). Otherwise a seeded 50/50 roll, aggressive or patient, from its own seed derived from the run's
/// seed and the node id, so it draws from no other RNG stream (route, drafts, AI, bot stay identical) and the same
/// run seed always gives the node the same temperament: a saved run (M5) carries its seed, so a resumed node rolls
/// the same. Shared by the fight scene, the harness and dev jumps.
/// </summary>
public static class NodeTemperament
{
	public const string Aggressive = "aggressive", Patient = "patient";
	/// <summary>Percent of nodes that roll aggressive (designer: 50/50).</summary>
	public const int AggressivePct = 50;
	const int Tag = 0x54454D50; // "TEMP": keeps this seed apart from every other seed derived from the run's

	/// <summary>The temperament the node rolls from <paramref name="runSeed"/> (ignores any named profile).</summary>
	public static string Roll(uint runSeed, string nodeId)
	{
		ulong h = Fnv.MixString(Fnv.Mix(Fnv.Mix(Fnv.Offset, unchecked((int)runSeed)), Tag), nodeId);
		uint v = (uint)(h ^ (h >> 32));
		return v % 100 < AggressivePct ? Aggressive : Patient;
	}

	/// <summary>
	/// Index into <paramref name="profiles"/> (the yokai's, aggressive first) the node fights with: the node's named
	/// profile when it has one and it exists (-1 when it names one that is missing; the caller reports it), else the
	/// first profile with the rolled temperament, else 0 (a yokai with one temperament only).
	/// </summary>
	public static int Pick(BehaviourProfile[] profiles, NodeContext node, uint runSeed)
	{
		if (node.Profile is { } named) return Array.FindIndex(profiles, p => p.Id == named);
		string t = Roll(runSeed, node.NodeId);
		int i = Array.FindIndex(profiles, p => p.Temperament == t);
		return i >= 0 ? i : 0;
	}
}
