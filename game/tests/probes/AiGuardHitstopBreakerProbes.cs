using System;
using System.Collections.Generic;
using System.Linq;
using YokaiFighters.Ai;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>Test Breaker probes for YOK-176 (E14, E22 guard tail through hitstop; P6 the AI reads only its view).</summary>
public static class AiGuardHitstopBreakerProbes
{
	const string Light = "normal:test-ryo-light-punch", Heavy = "normal:test-ryo-heavy-punch";

	static Match NewMatch(Func<SimConfig, SimConfig>? shape = null)
	{
		var saved = FightScene.Sources;
		FightScene.Sources = FightScene.FixtureSources;
		FightScene.Run = null;
		try { return FightScene.NewMatch(null, null, shape); }
		finally { FightScene.Sources = saved; }
	}

	static BehaviourProfile Profile(string move) => BehaviourProfile.Parse($$"""
		{ "kind": "profile", "id": "tb-guard-hitstop", "yokai": "kitsune", "temperament": "patient",
		  "reaction": { "tier": "apprentice", "frames": 30 }, "aggression_bias": 50, "preferred_range": "mid",
		  "habit": { "id": "jumps-after-knockdown", "description": "d", "when": "self_got_up", "do": "jump", "chance_pct": 60 },
		  "behaviours": [ { "when": "at_close_range", "do": "{{move}}", "weight": 100 } ], "rules": ["Y4"] }
		""");

	const InputBits Dirs = InputBits.Left | InputBits.Right | InputBits.Down | InputBits.Up;

	static Func<FighterInput> Mash(uint seed, int mode)
	{
		uint s = seed * 2654435761u + 12345;
		return () =>
		{
			s ^= s << 13; s ^= s >> 17; s ^= s << 5;
			return mode switch
			{
				0 => new FighterInput(InputBits.Left | InputBits.Down), // crouch block
				1 => FighterInput.None,                                  // stand: is hit / trades
				_ => (s & 3) == 0 ? FighterInput.Attack((int)(s >> 8) % 3) : (s & 12) == 0 ? new FighterInput((InputBits)(s >> 8) & Dirs) : FighterInput.None,
			};
		};
	}

	sealed class Log
	{
		public List<bool> Frozen = new(), Attack = new(), Guard = new(), HitOnAi = new();
		public List<FighterState> State = new();
		public List<FighterInput> Ai = new();
		public ulong Hash;
	}

	static Log Play(string move, int reaction, uint seed, int mode, int guardPct, int steps = 1500)
	{
		var m = NewMatch(c => c with { P1MaxHealth = 1_000_000, P2MaxHealth = 1_000_000 });
		int gap = m.Config.BodyWidth;
		m.P1.X = m.P2.X - gap;
		var ai = new ProfileAi(Profile(move), 1, AiLoadout.From(m.P2), seed, reaction, guardPct: guardPct);
		bool hit = false;
		m.Hit += (_, e) => { if (e.Attacker == 0) hit = true; };
		var p1 = Mash(seed, mode);
		var l = new Log();
		for (int t = 1; t <= steps && m.Phase == MatchPhase.Fighting; t++)
		{
			m.P1.X = m.P2.X - gap;
			var o = ai.Next(AiView.Capture(m));
			l.Frozen.Add(m.HitstopLeft > 0);
			hit = false;
			m.Step(p1(), o);
			l.Ai.Add(o);
			l.Attack.Add(m.P2.State == FighterState.Attack);
			l.Guard.Add(m.P2.Guarding && m.P2.Crouching);
			l.HitOnAi.Add(hit);
			l.State.Add(m.P2.State);
		}
		l.Hash = m.StateHash();
		return l;
	}

	// E22/E14: after its own move, GuardTicks moving crouch-guard steps from the first free step. Over seeds and P1
	// behaviours (block, stand and be hit, mash = trades/counterhits). Tails where the AI was hit are skipped.
	static void CheckTails(string move, int reaction, int mode, bool lower)
	{
		int tails = 0;
		for (uint seed = 1; seed <= 12; seed++)
		{
			var l = Play(move, reaction, seed, mode, 100);
			for (int i = 1; i < l.Attack.Count; i++)
			{
				if (!l.Attack[i - 1] || l.Attack[i]) continue;
				int free = 0, j = i; bool hurt = false;
				for (; j < l.Attack.Count; j++)
				{
					hurt |= l.HitOnAi[j] || l.State[j] is FighterState.Hitstun or FighterState.Blockstun;
					if (l.Frozen[j]) continue;
					if (!l.Guard[j]) break;
					free++;
				}
				if (j == l.Attack.Count || hurt) continue;
				tails++;
				string ctx = $"{move} r{reaction} mode{mode} seed{seed} move ending step {i + 1}, state at end {l.State[j]}";
				Assert.True(free <= ProfileAi.GuardTicks, $"{ctx}: guarded {free} moving steps, more than {ProfileAi.GuardTicks}");
				if (lower) Assert.True(free >= ProfileAi.GuardTicks, $"{ctx}: guarded {free} moving steps, fewer than {ProfileAi.GuardTicks}");
			}
		}
		Assert.True(tails > 0, $"{move} r{reaction} mode{mode}: no tails observed");
	}

	[Test] public static void HeavyBlocked_AllReactions_Exactly10() { foreach (int r in new[] { 15, 18, 22 }) CheckTails(Heavy, r, 0, true); }
	[Test] public static void LightBlocked_Reaction15_18_Exactly10() { foreach (int r in new[] { 15, 18 }) CheckTails(Light, r, 0, true); }
	[Test] public static void Whiff_Or_Hit_StandingOpponent_NeverMoreThan10() { foreach (int r in new[] { 15, 30 }) CheckTails(Heavy, r, 1, false); }
	[Test] public static void Mashing_TradesAndCounterhits_NeverMoreThan10()
	{
		foreach (var mv in new[] { Light, Heavy }) foreach (int r in new[] { 15, 22, 30 }) CheckTails(mv, r, 2, false);
	}
	// E14 counterhit +4 hitstop: standing opponent hit by the heavy, AI sees it at reaction 15.
	[Test] public static void Reaction15_StandingOpponentHit_Exactly10() { CheckTails(Heavy, 15, 1, true); }
	[Test] public static void Mashing_Reaction15_Heavy_AtLeast10WhenNotHurt() { CheckTails(Heavy, 15, 2, true); }

	static ulong Digest(int reaction, uint seed, int mode, string move)
	{
		var l = Play(move, reaction, seed, mode, 0, 1200);
		ulong h = l.Hash;
		foreach (var o in l.Ai) h = (h ^ (ulong)(uint)o.Bits ^ ((ulong)o.Move << 20)) * 1099511628211UL;
		return h;
	}

	// Guard 0 byte-identical to main; the constant was recorded by running this probe against origin/main's ProfileAi.
	[Test]
	public static void Guard0_InputsAndHash_MatchMain()
	{
		ulong h = 14695981039346656037UL;
		foreach (uint seed in new uint[] { 1, 2, 3, 4, 5, 6 })
			foreach (int mode in new[] { 0, 1, 2 })
				foreach (var mv in new[] { Light, Heavy })
					foreach (int r in new[] { 15, 30 })
						h = (h ^ Digest(r, seed, mode, mv)) * 1099511628211UL;
		Assert.Equal(MainDigest, h, "guard 0 digest vs main");
	}
	const ulong MainDigest = 11061846650259428594UL;

	[Test]
	public static void SameSeedTwice_Identical_DifferentGuard_Differs()
	{
		var a = Play(Heavy, 15, 3, 0, 100); var b = Play(Heavy, 15, 3, 0, 100);
		Assert.True(a.Hash == b.Hash && a.Ai.SequenceEqual(b.Ai), "same seed and choices: same inputs and hash");
		var c = Play(Heavy, 15, 3, 0, 0);
		Assert.True(c.Hash != a.Hash || !c.Ai.SequenceEqual(a.Ai), "guard 0 vs 100 differ");
	}

	// P6: the AI follows the view it is given, not the live match.
	[Test]
	public static void FollowsViewNotLiveMatch()
	{
		var live1 = NewMatch(); var live2 = NewMatch();
		live2.P1.X = live2.P2.X - 5000;
		var a1 = new ProfileAi(Profile(Light), 1, AiLoadout.From(live1.P2), 9, 15, guardPct: 100);
		var a2 = new ProfileAi(Profile(Light), 1, AiLoadout.From(live2.P2), 9, 15, guardPct: 100);
		for (int t = 0; t < 400; t++)
		{
			var v = AiView.Capture(live1);
			var o1 = a1.Next(v); var o2 = a2.Next(v);
			Assert.True(o1.Bits == o2.Bits && o1.Move == o2.Move, $"tick {t}: AI input depended on something other than the view");
			live1.Step(FighterInput.None, o1);
			live2.Step(FighterInput.None, FighterInput.None);
		}
	}
}
