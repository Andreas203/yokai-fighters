using System;

namespace YokaiFighters.Sim;

/// <summary>
/// YOK-26: one node's fight as a <see cref="Match"/>, pure C# so the fight scene and the headless whole-run harness
/// build it with the same code. P1 = Ryo's kit (A1) with the run's slots, levels, modifiers and carried health (C1);
/// P2 = <paramref name="opponent"/>'s kit, or, when the session's entered node names the Tanuki's kit, Ryo's kit +
/// the copy locked on entering that node (T0, T1). Both fighters start on <paramref name="scheme"/> (K3: only parsing differs).
/// </summary>
public static class NodeFight
{
	public static Match NewMatch(KitSources sources, RunState? run, RunSession? session, string opponent, ControlScheme scheme,
		Action<string>? log = null, Func<SimConfig, SimConfig>? shape = null)
	{
		var cfg = run?.MatchConfig();
		if (shape is not null) cfg = shape(cfg ?? SimConfig.Default); // YOK-98: a dojo trial's start distance
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
			var foe = Load(opponent, sources, log);
			m = new Match(cfg, ryo.Moves, foe.Moves);
			foe.Equip(m.P2);
		}
		ryo.Equip(m.P1);
		foreach (var f in m.Fighters) f.Input.Scheme = scheme;
		run?.ApplyTo(m.P1, cfg); // YOK-47: Ryo's drafted slots, levels, modifiers and carried health
		return m;
	}

	static Kit Load(string fighter, KitSources sources, Action<string>? log)
	{
		var kit = Kit.Load(fighter, sources);
		foreach (var n in kit.Notes) log?.Invoke(n);
		return kit;
	}
}
