using System;
using System.Collections.Generic;
using System.Linq;

namespace YokaiFighters.Sim;

/// <summary>
/// YOK-26: one node's fight as a <see cref="Match"/>, pure C# so the fight scene and the headless whole-run harness
/// build it with the same code. P1 = Ryo's kit (A1) with the run's slots, levels, modifiers and carried health (C1);
/// P2 = <paramref name="opponent"/>'s kit, or, when the session's entered node names the Tanuki's kit, Ryo's kit +
/// the copy locked on entering that node (T0, T1). P2 deals the node tier's damage percent (Y4, YOK-156) and its AI anti-airs at the tier's jump-punish percent (YOK-165). Both fighters start on <paramref name="scheme"/> (K3: only parsing differs).
/// </summary>
public static class NodeFight
{
	public static Match NewMatch(KitSources sources, RunState? run, RunSession? session, string opponent, ControlScheme scheme,
		Action<string>? log = null, Func<SimConfig, SimConfig>? shape = null)
	{
		var cfg = run?.MatchConfig();
		if (shape is not null) cfg = shape(cfg ?? SimConfig.Default); // YOK-98: a dojo trial's start distance
		// YOK-156, Y4: the node's tier scales the opponent's damage (the apprentice deals half).
		if (session?.Node is { DamageDealtPct: not SimConfig.FullDamagePct } node)
			cfg = (cfg ?? SimConfig.Default) with { P2DamagePct = node.DamageDealtPct };
		// YOK-165, Y4: the node's tier sets how often the opponent's AI anti-airs a jump (null = the profile's weights).
		if (session?.Node?.JumpPunishPct is { } jump)
			cfg = (cfg ?? SimConfig.Default) with { P2JumpPunishPct = jump };
		var ryo = Load(Kit.Ryo, sources, log);
		Match m;
		if (session?.Node?.Kit == TanukiKit.KitId)
		{
			var tanuki = TanukiKit.Build(ryo, TanukiKit.CopyAtFightStart(session.CopyLocked, session.LockedCopy, run, ryo.Specials));
			foreach (var n in tanuki.Notes) log?.Invoke(n);
			m = new Match(cfg, ryo.Moves, tanuki.Moves);
			tanuki.Equip(m.P2);
		}
		else
		{
			var foe = OpponentKit(sources, session?.Node, opponent, log);
			m = new Match(cfg, ryo.Moves, foe.Moves);
			foe.Equip(m.P2);
		}
		ryo.Equip(m.P1);
		foreach (var f in m.Fighters) f.Input.Scheme = scheme;
		run?.ApplyTo(m.P1, cfg); // YOK-47: Ryo's drafted slots, levels, modifiers and carried health
		return m;
	}

	/// <summary>
	/// The opponent's kit at <paramref name="node"/> (not the Tanuki's). YOK-174: a yokai with no fighter of its own fights
	/// with its own move files on the fallback's kit. YOK-226: a node that is not an elder's drops the rare specials
	/// (a yokai's second special belongs to its elder, A9; the elder profiles name it, YOK-234). No node (a bare or dev fight) = commons only too: only an elder node carries a rare.
	/// </summary>
	public static Kit OpponentKit(KitSources sources, NodeContext? node, string opponent, Action<string>? log = null)
	{
		string playsAs = node is { } at && at.Fighter == opponent ? at.MovesAndProfilesOf : opponent;
		var foe = Load(playsAs, opponent, sources, log);
		return node is { Elder: true } ? foe : foe.CommonsOnly();
	}

	/// <summary>
	/// YOK-226: the special ids <see cref="OpponentKit"/> holds back at <paramref name="node"/> (the yokai's rare specials
	/// at any node but an elder's). The temperament profiles are shared by the yokai and its elder (YOK-234), so the profile
	/// check (<c>AiLoadout.Missing</c>) accepts these; at run time <c>AiLoadout.CanDo</c> skips them.
	/// </summary>
	public static IReadOnlyList<string> HeldBack(KitSources sources, NodeContext? node, string opponent)
	{
		if (node is { Elder: true }) return Array.Empty<string>();
		string playsAs = node is { } at && at.Fighter == opponent ? node.MovesAndProfilesOf : opponent;
		return Kit.LoadAs(playsAs, opponent, sources).Specials.Where(s => s.Rarity == "rare").Select(s => s.Id).ToArray();
	}

	static Kit Load(string fighter, KitSources sources, Action<string>? log) => Load(fighter, fighter, sources, log);

	static Kit Load(string playsAs, string fighter, KitSources sources, Action<string>? log)
	{
		var kit = Kit.LoadAs(playsAs, fighter, sources);
		foreach (var n in kit.Notes) log?.Invoke(n);
		return kit;
	}
}
