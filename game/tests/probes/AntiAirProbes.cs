using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.Ai;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>Test Breaker final pass for YOK-165 (Y4, designer 2026-10-20/21): at rate 100 every jump the AI sees gets at most
/// one anti-air start per decision, whatever P1 does (cross-ups, quick re-jumps, hits on the AI's startup, EX specials).</summary>
public static class AntiAirProbes
{
	static Match NewMatch(Func<SimConfig, SimConfig>? shape = null)
	{
		var saved = FightScene.Sources;
		FightScene.Sources = FightScene.FixtureSources;
		FightScene.Run = null;
		try { return FightScene.NewMatch(null, null, shape); }
		finally { FightScene.Sources = saved; }
	}

	static BehaviourProfile P(string behaviours, string extra = "") => BehaviourProfile.Parse($$"""
		{ "kind": "profile", "id": "probe-aa", "yokai": "kitsune", "temperament": "patient",
		  "reaction": { "tier": "apprentice", "frames": 30 }, "aggression_bias": 50, "preferred_range": "close",
		  "habit": { "id": "jumps-after-knockdown", "description": "d", "when": "self_got_up", "do": "jump", "chance_pct": 1 },
		  "behaviours": [ {{behaviours}} ]{{extra}}, "rules": ["Y4"] }
		""");

	// Random jumper: forward/back/straight jumps (cross-ups when P1 jumps over P2), air re-presses (double-jump attempt), light punches.
	static FighterInput ScriptedP1(SimRng r, Match m)
	{
		uint v = r.NextUInt() % 100;
		if (m.P1.Airborne) return v < 30 ? new FighterInput(InputBits.Up) : v < 45 ? new FighterInput(InputBits.LightPunch) : FighterInput.None;
		if (v < 12) return new FighterInput(InputBits.Up | (m.P1.X < m.P2.X ? InputBits.Right : InputBits.Left));
		if (v < 18) return new FighterInput(InputBits.Up);
		if (v < 24) return new FighterInput(InputBits.LightPunch);
		if (v < 28) return new FighterInput(m.P1.X < m.P2.X ? InputBits.Right : InputBits.Left);
		return FighterInput.None;
	}

	record Run(int Decisions, int Starts, List<string> Bad, ulong Hash, string LogText, int Ex);

	static Run Play(AiFightTier tier, int reaction, uint seed, string answerId, string behaviours, string extra, bool special)
	{
		var m = NewMatch(c => c with { P2JumpPunishPct = 100, P1MaxHealth = 1_000_000, P2MaxHealth = 1_000_000 });
		if (special)
		{
			string json = File.ReadAllText(Path.Combine(ProjectSettings.GlobalizePath(FightScene.FixtureSpecialsDir), "test-spirit-wave.json"));
			m.P2.Equip(SpecialSlot.B, SpecialLoader.Parse(json));
		}
		m.P1.X = -150 * SimConfig.Scale; m.P2.X = 150 * SimConfig.Scale;
		var ai = new ProfileAi(P(behaviours, extra), 1, AiLoadout.From(m.P2), seed, reaction, tier, guardPct: 0) { Log = new() };
		var r = new SimRng(seed * 7 + 1);
		var starts = new List<int>();
		string? last = null; int lastFrame = 0;
		for (int t = 1; t <= 4000 && m.Phase != MatchPhase.Over; t++)
		{
			m.P1.Health = m.P1.MaxHealth; m.P2.Meter = 300;
			m.Step(ScriptedP1(r, m), ai.Next(AiView.Capture(m)));
			string? id = m.P2.State == FighterState.Attack ? m.P2.CurrentMove?.Id : null;
			int frame = id is null ? 0 : m.P2.MoveFrame;
			if (id == answerId && frame == 1 && (id != last || lastFrame != 1)) starts.Add(t);
			last = id; lastFrame = frame;
		}
		var dec = ai.Log!.Where(d => d.Trigger == "opponent_jumping" && d.Kind == "react").Select(d => d.Tick).ToList();
		var bad = new List<string>();
		if (starts.Count > dec.Count) bad.Add($"{starts.Count} starts for {dec.Count} decisions");
		for (int i = 0; i < dec.Count; i++)
		{
			int to = i + 1 < dec.Count ? dec[i + 1] : int.MaxValue;
			int n = starts.Count(s => s >= dec[i] && s < to);
			if (n > 1) bad.Add($"decision@{dec[i]}: {n} starts");
		}
		string text = string.Join("|", ai.Log!.Select(d => $"{d.Tick}:{d.Kind}:{d.Trigger}:{d.Roll}"));
		return new Run(dec.Count, starts.Count, bad, m.StateHash(), text, ai.Log!.Count(d => d.Kind == "ex"));
	}

	const string Medium = "normal:test-ryo-medium-punch";

	// Y4 + designer 2026-10-21: one roll = at most one anti-air, at both rate-100 tiers, with P1 cross-ups, re-jumps and hits on the AI.
	[Test]
	public static void Rate100_NeverMoreThanOneStartPerSeenJump_ElderAndTanuki()
	{
		foreach (var tier in new[] { AiFightTier.Elder, AiFightTier.Tanuki })
		foreach (int reaction in new[] { 4, 15, 30, 45 })
		foreach (uint seed in new uint[] { 1, 2, 3 })
		{
			var run = Play(tier, reaction, seed, "test-ryo-medium-punch", $$"""{ "when": "opponent_jumping", "do": "{{Medium}}", "weight": 100 }""", "", false);
			GD.Print($"YOK-165 probe {tier} r{reaction} s{seed}: {run.Starts} starts / {run.Decisions} decisions");
			Assert.True(run.Decisions >= 15, $"{tier} r{reaction} s{seed}: enough jumps were seen ({run.Decisions})");
			Assert.True(run.Bad.Count == 0, $"{tier} r{reaction} s{seed}: {string.Join("; ", run.Bad.Take(5))}");
		}
	}

	// Same, with an EX-able special (meter full, ex_use 100): the EX and plain versions share an id and must not double-start.
	[Test]
	public static void Rate100_ExSpecialAnswer_OneStartPerSeenJump()
	{
		foreach (var tier in new[] { AiFightTier.Elder, AiFightTier.Tanuki })
		foreach (int reaction in new[] { 8, 20, 40 })
		foreach (uint seed in new uint[] { 1, 2 })
		{
			var run = Play(tier, reaction, seed, "test-spirit-wave", """{ "when": "opponent_jumping", "do": "special:test-spirit-wave", "weight": 100 }""", ", \"ex_use\": { \"chance_pct\": 100 }", true);
			GD.Print($"YOK-165 probe EX {tier} r{reaction} s{seed}: {run.Starts} starts / {run.Decisions} decisions, {run.Ex} ex");
			Assert.True(run.Decisions >= 15, $"{tier} r{reaction} s{seed}: enough jumps seen ({run.Decisions})");
			Assert.True(run.Bad.Count == 0, $"{tier} r{reaction} s{seed}: {string.Join("; ", run.Bad.Take(5))}");
		}
	}

	// Determinism: the same seed and choices twice give the same hash and the same decision log; another seed differs.
	[Test]
	public static void AntiAir_SameSeedSameHashAndLog()
	{
		string beh = $$"""{ "when": "opponent_jumping", "do": "{{Medium}}", "weight": 100 }""";
		var a = Play(AiFightTier.Elder, 15, 9, "test-ryo-medium-punch", beh, "", false);
		var b = Play(AiFightTier.Elder, 15, 9, "test-ryo-medium-punch", beh, "", false);
		var c = Play(AiFightTier.Elder, 15, 10, "test-ryo-medium-punch", beh, "", false);
		Assert.Equal(a.Hash, b.Hash, "same seed, same hash");
		Assert.True(a.LogText == b.LogText, "same seed, same decision log");
		Assert.True(a.Hash != c.Hash || a.LogText != c.LogText, "another seed differs");
	}

	// A block-only jump answer: a tier rate must not change the draws (no attacking answer, no extra roll).
	[Test]
	public static void BlockOnlyJumpAnswer_RateDoesNotChangeTheDraws()
	{
		string beh = """{ "when": "opponent_jumping", "do": "crouch_block", "weight": 100 }, { "when": "neutral", "do": "walk_forward", "weight": 50 }""";
		string Go(int? rate)
		{
			var m = NewMatch(c => c with { P2JumpPunishPct = rate, P1MaxHealth = 1_000_000, P2MaxHealth = 1_000_000 });
			var ai = new ProfileAi(P(beh), 1, AiLoadout.From(m.P2), 4, 15, AiFightTier.Elder, guardPct: 0) { Log = new() };
			var r = new SimRng(5);
			for (int t = 1; t <= 2500 && m.Phase != MatchPhase.Over; t++) { m.P1.Health = m.P1.MaxHealth; m.Step(ScriptedP1(r, m), ai.Next(AiView.Capture(m))); }
			return m.StateHash() + "|" + string.Join("|", ai.Log!.Select(d => $"{d.Tick}:{d.Kind}:{d.Roll}"));
		}
		Assert.True(Go(100) == Go(null), "a block-only answer plays the same with or without a tier rate");
	}

	// After the round is over the AI emits nothing, however long it held a press.
	[Test]
	public static void NoPressAfterTheRoundEnds()
	{
		var m = NewMatch(c => c with { P2JumpPunishPct = 100, P2MaxHealth = 1_000_000 });
		m.P1.X = -150 * SimConfig.Scale; m.P2.X = 150 * SimConfig.Scale;
		var ai = new ProfileAi(P($$"""{ "when": "opponent_jumping", "do": "{{Medium}}", "weight": 100 }"""), 1, AiLoadout.From(m.P2), 3, 20, AiFightTier.Tanuki, guardPct: 0) { Log = new() };
		var r = new SimRng(11);
		for (int t = 1; t <= 600 && m.Phase == MatchPhase.Fighting; t++)
		{
			if (t == 300) m.P1.Health = 1;
			m.Step(ScriptedP1(r, m), ai.Next(AiView.Capture(m)));
		}
		for (int i = 0; i < 400; i++)
		{
			var o = ai.Next(AiView.Capture(m));
			if (i >= 60) Assert.True(o == FighterInput.None, $"call {i} after the fight: {o}");
			m.Step(FighterInput.None, o);
		}
	}
}
