using System;
using YokaiFighters.Ai;
using YokaiFighters.Sim;

namespace YokaiFighters.Replay;

/// <summary>
/// YOK-49: records one fight from tick 0. <see cref="Begin"/> snapshots the match setup (config, both kits by
/// id + fingerprint, equipped specials with level and modifier, cancel rules, schemes, AI); then call
/// <see cref="Record"/> after every <c>Match.Step</c> with the inputs the sim saw.
/// </summary>
public sealed class ReplayRecorder
{
	/// <summary>An hour of ticks; past it the recorder stops (a fight never gets close).</summary>
	public const int MaxSteps = 60 * 60 * 60;

	public ReplayRecording Recording { get; } = new();
	public bool Full => Recording.Steps >= MaxSteps;

	public static ReplayRecorder Begin(Match m, ProfileAi? ai, uint aiSeed, RunState? run = null)
	{
		if (m.Tick != 0) throw new InvalidOperationException("record from tick 0 (start or reset the match first)");
		var rec = new ReplayRecorder();
		var h = rec.Recording.Header;
		h.ConfigFp = Fingerprint.Of(m.Config);
		h.P1MaxHealth = m.Config.P1MaxHealth;
		h.P2MaxHealth = m.Config.P2MaxHealth;
		h.P1StartHealth = m.Config.P1StartHealth;
		h.P2StartHealth = m.Config.P2StartHealth;
		h.Fighters = new[] { Capture(m.P1), Capture(m.P2) };
		h.Ai = AiOf(ai, aiSeed);
		h.RunHash = run?.StateHash().ToString("x16");
		return rec;
	}

	public static AiSetup? AiOf(ProfileAi? ai, uint seed) => ai is null ? null : new AiSetup
	{
		ProfileId = ai.Profile.Id, Temperament = ai.Profile.Temperament, ProfileFp = Fingerprint.Of(ai.Profile),
		ReactionFrames = ai.ReactionFrames, Seed = seed,
	};

	static FighterSetup Capture(Fighter f)
	{
		var s = new FighterSetup { Scheme = f.Input.Scheme.ToString() };
		foreach (var mv in f.Moves) s.Moves.Add(new KitEntry { Id = mv.Id, Fp = Fingerprint.Of(mv) });
		for (int i = 1; i < f.Specials.Length; i++)
			if (f.Specials[i] is { } sp)
				s.Specials.Add(new SpecialEntry
				{
					Slot = i, Id = sp.Data.Id, Level = sp.Level, Modifier = sp.Modifier?.Id,
					MoveFp = Fingerprint.Of(sp.Move), ExFp = sp.ExMove is null ? null : Fingerprint.Of(sp.ExMove),
				});
		foreach (var c in f.CancelRules)
			s.CancelRules.Add(new CancelEntry { Id = c.Id, From = c.From, Into = c.Into, OnHit = c.OnHit, OnBlock = c.OnBlock, OnWhiff = c.OnWhiff });
		return s;
	}

	/// <summary>One step: the raw inputs the sim was given and its StateHash afterwards.</summary>
	public void Record(FighterInput p1, FighterInput p2, ulong hashAfter)
	{
		if (Full) return;
		Recording.P1.Add(p1);
		Recording.P2.Add(p2);
		Recording.Hashes.Add(hashAfter);
	}

	/// <summary>F4 mid-fight: a player's scheme changes before the next step.</summary>
	public void OnScheme(int player, ControlScheme scheme) =>
		Recording.Header.Events.Add(new ReplayEvent { Tick = Recording.Steps, Kind = "scheme", Player = player, Scheme = scheme.ToString() });

	/// <summary>F6/F7 mid-fight: P2's AI is rebuilt (fresh delay buffer) or switched off (null) before the next step.</summary>
	public void OnAi(ProfileAi? ai, uint seed)
	{
		if (Recording.Steps == 0) { Recording.Header.Ai = AiOf(ai, seed); return; }
		Recording.Header.Events.Add(new ReplayEvent { Tick = Recording.Steps, Kind = "ai", Player = 1, Ai = AiOf(ai, seed) });
	}

	/// <summary>Stamps the result as it stands now (winner, health, ticks, final hash) and returns the recording.</summary>
	public ReplayRecording Finish(Match m)
	{
		Recording.Header.Result = ResultOf(m, Recording.Steps);
		return Recording;
	}

	public static ReplayResult ResultOf(Match m, int steps) => new()
	{
		Steps = steps, MatchTick = m.Tick, Phase = m.Phase.ToString(), Winner = m.Winner,
		P1Health = m.P1.Health, P2Health = m.P2.Health, FinalHash = m.StateHash().ToString("x16"),
	};
}
