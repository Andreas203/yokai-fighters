using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YokaiFighters.Ai;
using YokaiFighters.Sim;

namespace YokaiFighters.Replay;

/// <summary>
/// Every piece of content a replay may name: moves, specials, modifiers and AI profiles, from <c>data/</c> and the
/// TEST FIXTURES (a fight may have used either). Ids can repeat (data vs fixture); the fingerprint picks the one.
/// </summary>
public sealed class ReplayCatalog
{
	public List<MoveData> Moves { get; } = new();
	public List<SpecialData> Specials { get; } = new();
	public List<ModifierData> Modifiers { get; } = new();
	public List<BehaviourProfile> Profiles { get; } = new();

	/// <summary>Scans <c>data/</c> and <c>game/tests/fixtures/</c> under the repo root, every *.json, by its kind.</summary>
	public static ReplayCatalog FromRepo(string repoRoot)
	{
		var c = new ReplayCatalog();
		c.AddTree(Path.Combine(repoRoot, "data"));
		c.AddTree(Path.Combine(repoRoot, "game", "tests", "fixtures"));
		return c;
	}

	public void AddTree(string dir)
	{
		if (!Directory.Exists(dir)) return;
		var files = Directory.GetFiles(dir, "*.json", SearchOption.AllDirectories);
		Array.Sort(files, StringComparer.Ordinal);
		foreach (var path in files)
		{
			try
			{
				switch (MoveLoader.KindOf(path))
				{
					case "special": Specials.Add(SpecialLoader.LoadFile(path)); break;
					case "modifier": Modifiers.Add(ModifierLoader.LoadFile(path)); break;
					case "profile": Profiles.Add(BehaviourProfile.LoadFile(path)); break;
					default: Moves.Add(MoveLoader.LoadFile(path)); break; // non-move kinds fail to parse and are skipped
				}
			}
			catch (Exception) { /* not content a fight uses (clips, schemas, story...) */ }
		}
	}
}

/// <summary>Outcome of a replay check. <see cref="Ok"/> = setup rebuilt, every tick's hash, the AI's outputs and the result matched.</summary>
public sealed class ReplayReport
{
	public bool Ok => Error is null;
	public string? Error { get; set; }
	public int StepsRun { get; set; }
	/// <summary>First 0-based step whose StateHash differs (-1 = none).</summary>
	public int FirstDivergentStep { get; set; } = -1;
	/// <summary>First step where the re-run AI gave a different P2 input than recorded (-1 = none).</summary>
	public int FirstAiMismatchStep { get; set; } = -1;
	public int AiStepsChecked { get; set; }
	public ReplayResult? Result { get; set; }
	public override string ToString() => Ok
		? $"OK: {StepsRun} steps identical (AI checked on {AiStepsChecked}); winner {Result!.Winner}, health {Result.P1Health}/{Result.P2Health}, tick {Result.MatchTick}, hash {Result.FinalHash}"
		: $"FAIL: {Error}";
}

/// <summary>
/// YOK-49: rebuilds the recorded fight from ids + fingerprints and re-runs it headless. P1 = recorded input;
/// P2 = the AI re-run from its seed when the recording had one (its outputs are checked against the recorded
/// P2 input, which a replay doesn't need), else the recorded input. Stops at the first divergence.
/// </summary>
public static class Replayer
{
	public static ReplayReport Run(ReplayRecording rec, ReplayCatalog catalog)
	{
		var report = new ReplayReport();
		Match m;
		try { m = Build(rec.Header, catalog); }
		catch (ReplaySetupException e) { report.Error = "setup: " + e.Message; return report; }

		ProfileAi? ai;
		try { ai = MakeAi(rec.Header.Ai, catalog, m); }
		catch (ReplaySetupException e) { report.Error = "setup: " + e.Message; return report; }

		var events = rec.Header.Events.OrderBy(e => e.Tick).ToList();
		int ev = 0;
		for (int i = 0; i < rec.Steps; i++)
		{
			for (; ev < events.Count && events[ev].Tick <= i; ev++)
			{
				var e = events[ev];
				if (e.Kind == "scheme") m.Fighters[e.Player].Input.Scheme = Enum.Parse<ControlScheme>(e.Scheme!);
				else if (e.Kind == "ai")
				{
					try { ai = MakeAi(e.Ai, catalog, m); }
					catch (ReplaySetupException ex) { report.Error = $"setup (step {i}): " + ex.Message; return report; }
				}
			}
			var p1 = rec.P1[i];
			var p2 = rec.P2[i];
			if (ai is not null)
			{
				var mine = ai.Next(AiView.Capture(m));
				report.AiStepsChecked++;
				if (mine != p2 && report.FirstAiMismatchStep < 0)
				{
					report.FirstAiMismatchStep = i;
					report.Error = $"AI diverged at step {i} (tick {m.Tick}): recorded P2 {Show(p2)}, re-run AI gave {Show(mine)}";
				}
				p2 = mine;
			}
			m.Step(p1, p2);
			report.StepsRun = i + 1;
			ulong h = m.StateHash();
			if (h != rec.Hashes[i])
			{
				report.FirstDivergentStep = i;
				report.Error ??= $"state hash diverged at step {i} (match tick {m.Tick}): recorded {rec.Hashes[i]:x16}, replay {h:x16}";
				report.Result = ReplayRecorder.ResultOf(m, i + 1);
				return report;
			}
		}
		report.Result = ReplayRecorder.ResultOf(m, rec.Steps);
		if (report.Error is null && rec.Header.Result is { } want)
		{
			var got = report.Result;
			if (got.Winner != want.Winner || got.P1Health != want.P1Health || got.P2Health != want.P2Health
				|| got.MatchTick != want.MatchTick || got.Phase != want.Phase || got.FinalHash != want.FinalHash || got.Steps != want.Steps)
				report.Error = $"result differs: recorded winner {want.Winner} {want.P1Health}/{want.P2Health} tick {want.MatchTick} {want.Phase} {want.FinalHash}, " +
					$"replay winner {got.Winner} {got.P1Health}/{got.P2Health} tick {got.MatchTick} {got.Phase} {got.FinalHash}";
		}
		return report;
	}

	static string Show(FighterInput f) => $"[{f.Bits} move {f.Move}]";

	/// <summary>The match exactly as recorded at tick 0, or a <see cref="ReplaySetupException"/> naming what changed.</summary>
	public static Match Build(ReplayHeader h, ReplayCatalog cat)
	{
		if (h.Fighters.Length != 2) throw new ReplaySetupException($"expected 2 fighters, got {h.Fighters.Length}");
		var cfg = SimConfig.Default with
		{
			P1MaxHealth = h.P1MaxHealth, P2MaxHealth = h.P2MaxHealth, P1StartHealth = h.P1StartHealth, P2StartHealth = h.P2StartHealth,
		};
		if (Fingerprint.Of(cfg) != h.ConfigFp)
			throw new ReplaySetupException("SimConfig differs from the recording (fight constants changed since it was made)");
		var m = new Match(cfg, Kit(h.Fighters[0], cat, "P1"), Kit(h.Fighters[1], cat, "P2"));
		for (int p = 0; p < 2; p++)
		{
			var f = m.Fighters[p];
			var s = h.Fighters[p];
			for (int i = 1; i < f.Specials.Length; i++) f.Unequip((SpecialSlot)i);
			foreach (var sp in s.Specials) Equip(f, sp, cat, cfg, $"P{p + 1}");
			foreach (var c in s.CancelRules) f.HoldCancelRule(new CancelRule(c.Id, c.From, c.Into, c.OnHit, c.OnBlock, c.OnWhiff));
			f.Input.Scheme = Enum.Parse<ControlScheme>(s.Scheme);
		}
		m.Reset(); // health from config, as at fight start (Reset keeps specials and cancel rules)
		return m;
	}

	static MoveData[] Kit(FighterSetup s, ReplayCatalog cat, string who) => s.Moves.Select((k, slot) =>
	{
		var same = cat.Moves.Where(mv => mv.Id == k.Id).ToList();
		if (same.Count == 0) throw new ReplaySetupException($"{who} move slot {slot} '{k.Id}' no longer exists in data/ or the fixtures");
		return same.FirstOrDefault(mv => Fingerprint.Of(mv) == k.Fp)
			?? throw new ReplaySetupException($"{who} move slot {slot} '{k.Id}' changed since the recording (frame data or boxes differ)");
	}).ToArray();

	static void Equip(Fighter f, SpecialEntry e, ReplayCatalog cat, SimConfig cfg, string who)
	{
		var slot = (SpecialSlot)e.Slot;
		var datas = cat.Specials.Where(s => s.Id == e.Id).ToList();
		if (datas.Count == 0) throw new ReplaySetupException($"{who} special '{e.Id}' (slot {slot}) no longer exists");
		var mods = e.Modifier is null ? new List<ModifierData?> { null } : cat.Modifiers.Where(x => x.Id == e.Modifier).Cast<ModifierData?>().ToList();
		if (mods.Count == 0) throw new ReplaySetupException($"{who} modifier '{e.Modifier}' on '{e.Id}' no longer exists");
		// The fight may have equipped with the run's config (RunState.ApplyTo) or none (starters): try both.
		foreach (var d in datas)
		foreach (var mod in mods)
		foreach (var defaults in new[] { cfg, null })
		{
			EquippedSpecial built;
			try { built = new EquippedSpecial(d, e.Level, mod, defaults); }
			catch (Exception) { continue; }
			if (Fingerprint.Of(built.Move) != e.MoveFp) continue;
			if ((built.ExMove is null ? null : Fingerprint.Of(built.ExMove)) != e.ExFp) continue;
			f.Equip(slot, d, e.Level, mod, defaults);
			return;
		}
		throw new ReplaySetupException($"{who} special '{e.Id}' Lv {e.Level}{(e.Modifier is null ? "" : " + " + e.Modifier)} changed since the recording");
	}

	static ProfileAi? MakeAi(AiSetup? a, ReplayCatalog cat, Match m)
	{
		if (a is null) return null;
		var ps = cat.Profiles.Where(p => p.Id == a.ProfileId).ToList();
		if (ps.Count == 0) throw new ReplaySetupException($"AI profile '{a.ProfileId}' no longer exists");
		var prof = ps.FirstOrDefault(p => Fingerprint.Of(p) == a.ProfileFp)
			?? throw new ReplaySetupException($"AI profile '{a.ProfileId}' changed since the recording");
		return new ProfileAi(prof, 1, AiLoadout.From(m.P2), a.Seed, a.ReactionFrames);
	}
}

public sealed class ReplaySetupException(string message) : Exception(message);
