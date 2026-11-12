using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;
using YokaiFighters.Ai;
using YokaiFighters.Fight;
using YokaiFighters.Harness;
using YokaiFighters.Replay;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// Test Breaker probes for YOK-228 (the Kappa's kit, the recorded NeutralThrowHeld AI flag). Rules: C4/E12 (generic
/// throw), E4, E5, F1/F3 (hitboxes are move data), Y2/P6 (what the AI does), T0 (Tanuki kit), A-replay determinism.
/// </summary>
public static class KappaKitProbes
{
	static KitSources Sources => KitSources.At(ContentPaths.DataDir, ContentPaths.FixturesDir);
	static BehaviourProfile Profile(string id) => BehaviourProfile.LoadFile(ContentPaths.Data("profiles") + "/" + id + ".json");
	static ReplayCatalog Catalog => ReplayCatalog.FromRepo(Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + ".."));
	const InputBits ThrowBits = InputBits.LightPunch | InputBits.LightKick;

	static Match KappaMatch()
	{
		var ryo = Kit.Load(Kit.Ryo, Sources);
		var foe = Kit.LoadAs("kappa", "kitsune", Sources);
		var m = new Match(null, ryo.Moves, foe.Moves);
		ryo.Equip(m.P1);
		foe.Equip(m.P2);
		return m;
	}

	static ProfileAi KappaAi(Match m, string profile, uint seed, bool held, bool habits = true) =>
		new(Profile(profile), 1, AiLoadout.From(m.P2), seed, null, AiFightTier.Standard, 0, null, habits, held);

	static Func<FighterInput> RandomRyo(uint seed)
	{
		var rng = new SimRng(seed);
		InputBits h = InputBits.None; int left = 0;
		return () => AiHarness.RandomInput(rng, ref h, ref left);
	}

	// ---- replay -------------------------------------------------------------------------------------------------

	static ReplayRecording Record(string profile, uint seed, bool held, int ticks, out int heldPicks)
	{
		var m = KappaMatch();
		var log = new List<AiDecision>();
		var ai = KappaAi(m, profile, seed, held); ai.Log = log;
		var rec = ReplayRecorder.Begin(m, ai, seed);
		var ryo = RandomRyo(seed * 31u + 7u);
		for (int t = 0; t < ticks && m.Phase != MatchPhase.Over; t++)
		{
			var b = ai.Next(AiView.Capture(m)); var a = ryo();
			m.Step(a, b);
			rec.Record(a, b, m.StateHash());
		}
		heldPicks = log.Count(d => d.Kind == "neutral" && d.Action == "throw");
		return rec.Finish(m);
	}

	// Replay determinism: a new recording writes NeutralThrowHeld true and replays; a flag-off one omits it and replays;
	// the round trip through bytes keeps both.
	[Test]
	public static void Replay_HeaderRoundTrip_NewWritesTrue_OldReadsFalse_BothReplay()
	{
		int picks = 0;
		foreach (string prof in new[] { "kappa-aggressive", "kappa-patient" })
			foreach (uint seed in new[] { 1u, 2u, 3u })
			{
				var on = ReplayRecording.FromBytes(Record(prof, seed, true, 3000, out int p1).ToBytes());
				Assert.True(on.Header.Ai is { NeutralThrowHeld: true, StandingHabits: true }, $"{prof}/{seed}: new recording writes true");
				var r = Replayer.Run(on, Catalog);
				Assert.True(r.Ok, $"{prof}/{seed}: flag-on replays: {r}");
				var off = ReplayRecording.FromBytes(Record(prof, seed, false, 3000, out _).ToBytes());
				Assert.True(off.Header.Ai is { NeutralThrowHeld: false }, $"{prof}/{seed}: flag-off writes false");
				r = Replayer.Run(off, Catalog);
				Assert.True(r.Ok, $"{prof}/{seed}: flag-off replays: {r}");
				picks += p1;
			}
		Assert.True(picks > 0, "the Kappa made at least one neutral throw pick in the probe fights");
	}

	// A header with the field missing is a recording from before YOK-228: it must read false, not true.
	[Test]
	public static void Replay_HeaderWithoutTheField_ReadsFalse()
	{
		var rec = Record("kappa-aggressive", 5, true, 600, out _);
		string json = JsonSerializer.Serialize(rec.Header);
		Assert.True(json.Contains("NeutralThrowHeld"), "flag-on header carries the field: " + json);
		var node = System.Text.Json.Nodes.JsonNode.Parse(json)!;
		node["Ai"]!.AsObject().Remove("NeutralThrowHeld");
		var h = JsonSerializer.Deserialize<ReplayHeader>(node.ToJsonString())!;
		Assert.True(h.Ai is { NeutralThrowHeld: false }, "a missing field is the old rule");
	}

	// The flag must matter: stripped from a flag-on Kappa recording, at least one seed has to fail to replay (AI
	// mismatch), otherwise the flag is never exercised by the Kappa's profiles and the header test above proves nothing.
	[Test]
	public static void Replay_FlagStrippedFromAFlagOnRecording_IsDetected()
	{
		int detected = 0, ran = 0;
		foreach (string prof in new[] { "kappa-aggressive", "kappa-patient" })
			foreach (uint seed in Enumerable.Range(1, 8).Select(i => (uint)i))
			{
				var rec = Record(prof, seed, true, 4000, out int picks);
				if (picks == 0) continue;
				ran++;
				rec.Header.Ai!.NeutralThrowHeld = false;
				if (!Replayer.Run(rec, Catalog).Ok) detected++;
			}
		Assert.True(ran > 0, "some fight had a neutral throw pick");
		Assert.True(detected > 0, $"stripping the flag changed no replay in {ran} fights with throw picks: the flag has no effect on a Kappa fight");
	}

	// A mid-fight AI swap (event) records the flag too.
	[Test]
	public static void Replay_MidFightAiSwap_KeepsTheFlag()
	{
		var m = KappaMatch();
		var ai = KappaAi(m, "kappa-aggressive", 9, true);
		var rec = ReplayRecorder.Begin(m, ai, 9);
		var ryo = RandomRyo(99);
		for (int t = 0; t < 1500 && m.Phase != MatchPhase.Over; t++)
		{
			if (t == 700) { ai = KappaAi(m, "kappa-patient", 10, true); rec.OnAi(ai, 10); }
			var b = ai.Next(AiView.Capture(m)); var a = ryo();
			m.Step(a, b); rec.Record(a, b, m.StateHash());
		}
		var done = ReplayRecording.FromBytes(rec.Finish(m).ToBytes());
		Assert.True(done.Header.Events.Any(e => e.Kind == "ai" && e.Ai is { NeutralThrowHeld: true }), "the swap event carries true");
		var r = Replayer.Run(done, Catalog);
		Assert.True(r.Ok, "swap replays: " + r);
	}

	// ---- AI: a picked neutral throw comes out -----------------------------------------------------------------

	static bool Stunned(FighterState s) => s is FighterState.Hitstun or FighterState.Blockstun or FighterState.Thrown or FighterState.Knockdown or FighterState.Burst;

	// Y2/P6: what the AI decides it does. A neutral throw pick that nothing interrupts (no new hit, block, throw on the AI)
	// must start the throw: neither hang (a plan that never ends) nor press while busy and lose the pick.
	static (int picks, int thrown, int interrupted, int lost, List<string> lostWhy) ThrowPicks(string prof, uint seed, bool held, int ticks)
	{
		var m = KappaMatch();
		var log = new List<AiDecision>();
		var ai = KappaAi(m, prof, seed, held); ai.Log = log;
		var ryo = RandomRyo(seed * 17u + 3u);
		int picks = 0, thrown = 0, interrupted = 0, lost = 0, from = 0, pickTick = -1, lastStun = -1000;
		var why = new List<string>();
		var prev = AiView.Capture(m).Fighter(1);
		for (int t = 0; t < ticks; t++)
		{
			var b = ai.Next(AiView.Capture(m));
			m.Step(ryo(), b);
			var s = AiView.Capture(m).Fighter(1);
			bool newStun = Stunned(s.State) && (!Stunned(prev.State) || s.StunLeft > prev.StunLeft);
			if (newStun) lastStun = m.Tick;
			for (; from < log.Count; from++)
			{
				var d = log[from];
				if (d.Kind == "neutral" && d.Action == "throw") { if (pickTick >= 0 && lastStun < pickTick - 30) { lost++; why.Add($"{prof}/{seed}: pick {pickTick} replaced with no hit around it"); } picks++; pickTick = m.Tick; }
				else if (pickTick >= 0 && d.Kind is "habit" or "react") { interrupted++; pickTick = -1; }
			}
			if (pickTick >= 0)
			{
				if (s.State == FighterState.Attack && s.MoveId == "kappa-throw") { thrown++; pickTick = -1; }
				else if (m.Tick > pickTick && newStun) { interrupted++; pickTick = -1; }
				else if (m.Tick - pickTick > 500) { if (lastStun >= pickTick - 30) interrupted++; else lost++; if (lastStun < pickTick - 30) why.Add($"{prof}/{seed}: pick at {pickTick} never threw by {m.Tick} (state {s.State})"); pickTick = -1; }
			}
			prev = s;
			if (m.Phase == MatchPhase.Over) { m.Reset(); ai = KappaAi(m, prof, seed + (uint)t, held); ai.Log = log; from = log.Count; pickTick = -1; }
		}
		return (picks, thrown, interrupted, lost, why);
	}

	[Test]
	public static void KappaAi_NeutralThrowPick_ComesOutUnlessInterrupted_NeverHangs()
	{
		int picks = 0, thrown = 0, lost = 0; var why = new List<string>();
		foreach (string prof in new[] { "kappa-aggressive", "kappa-patient" })
			foreach (uint seed in new[] { 1u, 2u, 3u, 4u })
			{
				var r = ThrowPicks(prof, seed, true, 8000);
				picks += r.picks; thrown += r.thrown; lost += r.lost; why.AddRange(r.lostWhy);
			}
		GD.Print($"YOK-228 probe: neutral throw picks {picks}, started {thrown}, lost {lost}");
		Assert.True(picks >= 20, $"enough throw picks to mean something ({picks})");
		Assert.True(thrown > 0, "the Kappa throws at all");
		Assert.True(lost == 0, $"{lost} uninterrupted throw picks never came out: {string.Join(" | ", why.Take(4))}");
	}

	// The hold must not make the AI throw LESS than the old rule across the board (a pick pressed while busy is lost
	// under both rules; held should be no worse). Not a rule number: a regression guard on the share that comes out.
	[Test]
	public static void KappaAi_HeldThrow_StartsAtLeastAsOftenAsTheRawPress()
	{
		int onT = 0, offT = 0, onP = 0, offP = 0;
		foreach (string prof in new[] { "kappa-aggressive", "kappa-patient" })
			foreach (uint seed in new[] { 1u, 2u, 3u })
			{
				var a = ThrowPicks(prof, seed, true, 8000); var b = ThrowPicks(prof, seed, false, 8000);
				onT += a.thrown; onP += a.picks; offT += b.thrown; offP += b.picks;
			}
		double on = (double)onT / Math.Max(1, onP), off = (double)offT / Math.Max(1, offP);
		GD.Print($"YOK-228 probe: throws started per pick, held {onT}/{onP} = {on:0.00}, raw {offT}/{offP} = {off:0.00}");
		Assert.True(on + 0.02 >= off, $"held {on:0.00} vs raw {off:0.00}");
	}

	// ---- flag off = the rule before YOK-228 ---------------------------------------------------------------------

	// Flag off, a neutral throw pick presses the raw LP+LK on the pick tick (no wait, no slot request).
	[Test]
	public static void FlagOff_NeutralThrowPick_PressesRawButtonsAtOnce()
	{
		int checkedPicks = 0;
		foreach (string prof in new[] { "kappa-aggressive", "kappa-patient" })
			foreach (uint seed in new[] { 1u, 2u, 3u })
			{
				var m = KappaMatch();
				var log = new List<AiDecision>();
				var ai = KappaAi(m, prof, seed, false); ai.Log = log;
				var ryo = RandomRyo(seed + 40);
				int from = 0;
				for (int t = 0; t < 6000 && m.Phase != MatchPhase.Over; t++)
				{
					var b = ai.Next(AiView.Capture(m));
					for (; from < log.Count; from++)
						if (log[from].Kind == "neutral" && log[from].Action == "throw")
						{
							checkedPicks++;
							Assert.Equal((int)ThrowBits, (int)(b.Bits & InputBits.Attacks), $"{prof}/{seed}: raw LP+LK at pick tick {m.Tick}");
							Assert.True(b.Move < 0 || b.Move == FighterInput.None.Move, $"{prof}/{seed}: no slot request with the flag off");
						}
					m.Step(ryo(), b);
				}
			}
		Assert.True(checkedPicks > 0, "some neutral throw picks");
	}

	// "Same draws": fed the same screens, the held and the raw AI roll the same dice up to and including the first
	// neutral throw pick (only what is pressed afterwards differs).
	[Test]
	public static void FlagOnAndOff_SameDrawsUntilTheFirstThrowPick()
	{
		int compared = 0;
		foreach (string prof in new[] { "kappa-aggressive", "kappa-patient" })
			foreach (uint seed in new[] { 1u, 2u, 3u, 4u })
			{
				var m = KappaMatch();
				var l1 = new List<AiDecision>(); var l2 = new List<AiDecision>();
				var on = KappaAi(m, prof, seed, true); on.Log = l1;
				var off = KappaAi(m, prof, seed, false); off.Log = l2;
				var ryo = RandomRyo(seed + 70);
				for (int t = 0; t < 4000 && m.Phase != MatchPhase.Over; t++)
				{
					var v = AiView.Capture(m);
					var a = on.Next(v); var b = off.Next(v);
					int first = l2.FindIndex(d => d.Kind == "neutral" && d.Action == "throw");
					if (first >= 0) break;
					Assert.True(a == b, $"{prof}/{seed}: inputs differ at tick {m.Tick} before any throw pick: {a.Bits} vs {b.Bits}");
					m.Step(ryo(), b);
				}
				int n = Math.Min(l1.Count, l2.Count);
				for (int i = 0; i < n; i++) { Assert.True(l1[i] == l2[i], $"{prof}/{seed}: decision {i} differs {l1[i]} vs {l2[i]}"); compared++; }
			}
		Assert.True(compared > 0, "compared decisions");
	}

	// ---- whole runs ---------------------------------------------------------------------------------------------

	static string Hashes(HarnessContent c, uint seed, int runs)
	{
		var bot = BotConfig.LoadDefault(ContentPaths.DataDir);
		using var d = JsonDocument.Parse(HarnessReport.ToJson(RunHarness.PlayBatch(c, bot, seed, runs)));
		return string.Join(",", d.RootElement.GetProperty("runs").EnumerateArray().Select(r => r.GetProperty("session_hash").GetString()));
	}

	// The shipping path (flag on, default content) is deterministic, seed-sensitive, and differs from the flag-off run.
	[Test]
	public static void WholeRun_ShippingPath_DeterministicSeedSensitive_AndNotTheLegacyRun()
	{
		var shipping = HarnessContent.Load(ContentPaths.DataDir, ContentPaths.FixturesDir);
		Assert.True(shipping.NeutralThrowHeld && shipping.StandingHabits, "default content ships both flags on");
		string a = Hashes(shipping, 2, 3), b = Hashes(shipping, 2, 3);
		Assert.Equal(a, b, "same seed twice");
		Assert.True(a != Hashes(shipping, 3, 3), "another seed gives another batch");
		Assert.True(a != Hashes(shipping with { NeutralThrowHeld = false }, 2, 3), "the flag changes some whole run");
	}

	// ---- the Kappa kit ----------------------------------------------------------------------------------------

	static MoveData[] KappaMoves() => Kit.LoadAs("kappa", "kitsune", Sources).Moves.Where(m => m.Source == "kappa").ToArray();

	// F1/F3: hitboxes are 2D rectangles in move data. Every active frame is covered by a hitbox, none outside the active
	// window, hurtboxes cover the whole move, rectangles have area, throwboxes sit in the active window.
	[Test]
	public static void KappaMoves_BoxesAreUsableBySim()
	{
		var moves = KappaMoves();
		Assert.Equal(7, moves.Length, "six normals and a throw");
		foreach (var mv in moves)
		{
			for (int f = 1; f <= mv.TotalFrames; f++)
				Assert.True(mv.Hurtboxes.Any(h => h.Covers(f)), $"{mv.Id}: no hurtbox on frame {f}");
			foreach (var h in mv.Hurtboxes.Concat(mv.Hitboxes).Concat(mv.Throwboxes))
				Assert.True(h.Box.W > 0 && h.Box.H > 0 && h.First >= 1 && h.Last <= mv.TotalFrames && h.First <= h.Last, $"{mv.Id}: bad box {h}");
			var strike = mv.IsThrow ? mv.Throwboxes : mv.Hitboxes;
			Assert.True(strike.Count > 0, $"{mv.Id}: no strike boxes");
			for (int f = mv.FirstActive; f <= mv.LastActive; f++)
				Assert.True(strike.Any(h => h.Covers(f)), $"{mv.Id}: active frame {f} has no hit/throw box");
			foreach (var h in strike)
				Assert.True(h.First >= mv.FirstActive && h.Last <= mv.LastActive, $"{mv.Id}: strike box {h.First}-{h.Last} outside active {mv.FirstActive}-{mv.LastActive}");
			if (mv.IsThrow) Assert.True(mv.Hitboxes.Count == 0, $"{mv.Id}: a throw has no hitboxes");
		}
	}

	// A move's data must reach: a standing Ryo walked into the Kappa (push contact) is hit by each normal, with the data's damage.
	[Test]
	public static void KappaNormals_EachConnectsWithAStandingRyoAtPushContact()
	{
		var misses = new List<string>();
		foreach (var mv in KappaMoves().Where(x => !x.IsThrow))
		{
			var m = KappaMatch();
			int slot = Array.FindIndex(m.P2.Moves, x => x.Id == mv.Id);
			Assert.True(slot >= 0, mv.Id + " is in P2's kit");
			HitEvent? hit = null;
			m.Hit += (_, e) => { if (e.Attacker == 1 && hit is null) hit = e; };
			var toward = new FighterInput(InputBits.Right);
			for (int t = 0; t < 400; t++) m.Step(toward, FighterInput.None); // P1 walks to push contact
			Assert.True(m.P1.Health == m.P1.MaxHealth, "no damage while closing in");
			m.Step(FighterInput.None, FighterInput.Attack(slot));
			for (int t = 0; t < mv.TotalFrames + 5; t++) m.Step(FighterInput.None, FighterInput.None);
			if (!(hit is { } h && h.MoveId == mv.Id && !h.Blocked)) { misses.Add($"{mv.Id} (reach {mv.Hitboxes.Max(b => b.Box.X + b.Box.W)} units from his centre; Ryo's 90-wide body starts at 80 when the push boxes (130 + 120) touch)"); continue; }
			Assert.Equal(mv.Damage, m.P1.MaxHealth - m.P1.Health, $"{mv.Id}: damage from data");
		}
		Assert.True(misses.Count == 0, "no hit on an idle, standing Ryo at push contact (the closest the bodies can stand): " + string.Join("; ", misses));
	}

	// ---- throw rules (C4, E4, E12) ----------------------------------------------------------------------------

	static (Match m, List<ThrowEvent> ev) Close()
	{
		var m = KappaMatch();
		var ev = new List<ThrowEvent>();
		m.Throw += (_, e) => ev.Add(e);
		for (int t = 0; t < 400; t++) m.Step(new FighterInput(InputBits.Right), FighterInput.None);
		return (m, ev);
	}

	// C4: 120 damage, startup 5, break window 7; E4: the defender plays the standard 40-frame knockdown; E12: recovery outlasts the break window.
	[Test]
	public static void KappaThrow_LandsOnIdleRyo_C4Numbers_StandardKnockdown()
	{
		var mv = KappaMoves().Single(x => x.IsThrow);
		Assert.Equal(5, mv.Startup, "C4 startup"); Assert.Equal(7, mv.BreakWindow, "C4 break window");
		Assert.Equal(120, mv.Damage, "C4 damage"); Assert.True(mv.Recovery > mv.BreakWindow, "E12 recovery outlasts the break window");
		Assert.True(mv.ThrowButtons == ThrowBits, "LP+LK");
		var (m, ev) = Close();
		int hp = m.P1.Health;
		m.Step(FighterInput.None, new FighterInput(ThrowBits));
		bool sawKnockdown = false; int downTicks = 0;
		for (int t = 0; t < 120; t++)
		{
			m.Step(FighterInput.None, FighterInput.None);
			if (m.P1.State == FighterState.Knockdown) { sawKnockdown = true; downTicks++; }
		}
		Assert.True(ev.Any(e => e.Outcome == ThrowOutcome.Grab) && ev.Any(e => e.Outcome == ThrowOutcome.Land), "grab then land");
		Assert.Equal(120, hp - m.P1.Health, "C4 120 damage");
		Assert.True(sawKnockdown, "E4 knockdown");
		// E4 + E12 (a landed throw's heavy hitstop): the same time down as Ryo's own throw on the Kappa.
		var (m2, _) = Close();
		m2.Step(new FighterInput(ThrowBits), FighterInput.None);
		int ref2 = 0;
		for (int t = 0; t < 120; t++) { m2.Step(FighterInput.None, FighterInput.None); if (m2.P2.State == FighterState.Knockdown) ref2++; }
		Assert.Equal(ref2, downTicks, "time down after the Kappa's throw = after Ryo's throw (E4)");
		Assert.True(downTicks >= 40, "E4: at least the 40 knockdown frames");
	}

	// E12: throw vs throw on the same frame clash: automatic break, no damage, both freed.
	[Test]
	public static void KappaThrow_SameFrameAsRyosThrow_Clashes_NoDamage()
	{
		var (m, ev) = Close();
		int h1 = m.P1.Health, h2 = m.P2.Health;
		m.Step(new FighterInput(ThrowBits), new FighterInput(ThrowBits));
		for (int t = 0; t < 40; t++) m.Step(FighterInput.None, FighterInput.None);
		Assert.True(ev.Any(e => e.Outcome == ThrowOutcome.Clash), "clash: " + string.Join(",", ev.Select(e => e.Outcome)));
		Assert.True(!ev.Any(e => e.Outcome == ThrowOutcome.Land), "no throw lands");
		Assert.True(m.P1.Health == h1 && m.P2.Health == h2, "no damage on a clash");
		Assert.True(m.P1.State != FighterState.Thrown && m.P2.State != FighterState.Thrown, "both freed");
	}

	// E12: pressing the throw buttons inside the break window breaks the Kappa's throw: no damage, both freed.
	[Test]
	public static void KappaThrow_BrokenInTheWindow_NoDamage_BothFreed_AndOneTickLateLands()
	{
		foreach (int lateBy in new[] { 0, 6, 8 }) // press on break-window tick 1, 7 (last), and 8 (too late)
		{
			var (m, ev) = Close();
			int hp = m.P1.Health;
			m.Step(FighterInput.None, new FighterInput(ThrowBits));
			int guard = 0;
			while (!ev.Any(e => e.Outcome == ThrowOutcome.Grab) && guard++ < 30) m.Step(FighterInput.None, FighterInput.None);
			Assert.True(ev.Any(e => e.Outcome == ThrowOutcome.Grab), "grab connected");
			for (int i = 0; i < lateBy; i++) m.Step(FighterInput.None, FighterInput.None);
			m.Step(new FighterInput(ThrowBits), FighterInput.None);
			for (int t = 0; t < 60; t++) m.Step(FighterInput.None, FighterInput.None);
			bool broke = ev.Any(e => e.Outcome == ThrowOutcome.Break);
			if (lateBy <= 6) { Assert.True(broke && m.P1.Health == hp, $"press {lateBy} ticks after the grab breaks it (E12, window 7)"); }
			else Assert.True(!broke && hp - m.P1.Health == 120, $"press {lateBy} ticks after the grab is too late (window 7)");
		}
	}

	// E12: Ryo throwing the Kappa lands too (the Kappa's 130 push width and body box are no throw-invulnerability).
	[Test]
	public static void RyoThrow_OnTheKappa_Lands()
	{
		var (m, ev) = Close();
		int hp = m.P2.Health;
		m.Step(new FighterInput(ThrowBits), FighterInput.None);
		for (int t = 0; t < 60; t++) m.Step(FighterInput.None, FighterInput.None);
		Assert.True(ev.Any(e => e.Attacker == 0 && e.Outcome == ThrowOutcome.Land), "Ryo's throw lands on the Kappa at push contact: " + string.Join(",", ev.Select(e => e.Outcome)));
		Assert.True(hp - m.P2.Health > 0, "damage");
	}

	// ---- the Kappa plays the Kappa's moves ---------------------------------------------------------------------

	// What the Kappa starts in a fight is his own (the Kitsune's air normals are a declared stand in, and he has none that the AI presses).
	[Test]
	public static void KappaAi_OnlyEverStartsKappaMoves_AndEachPlaysItsOwnClip()
	{
		var seen = new HashSet<string>();
		foreach (string prof in new[] { "kappa-aggressive", "kappa-patient" })
			foreach (uint seed in new[] { 1u, 2u })
			{
				var m = KappaMatch(); var ai = KappaAi(m, prof, seed, true); var ryo = RandomRyo(seed + 5);
				for (int t = 0; t < 6000 && m.Phase != MatchPhase.Over; t++)
				{
					m.Step(ryo(), ai.Next(AiView.Capture(m)));
					if (m.P2.State == FighterState.Attack && m.P2.CurrentMove is { } mv) seen.Add(mv.Id);
				}
			}
		Assert.True(seen.Contains("kappa-throw"), "he threw: " + string.Join(",", seen));
		foreach (string id in seen)
		{
			// YOK-226: river-grab is a real Kappa special without the kappa- prefix; a Kappa-source special is the Kappa's too.
			Assert.True(id.StartsWith("kappa-") || KappaSpecialMoveIds().Contains(id), "started a move that is not the Kappa's: " + id);
			// YOK-226: a special's move id (river-grab) differs from its Kappa clip name (kappa-river-grab); it still names its own Kappa clip.
			string want = id.StartsWith("kappa-") ? id : "kappa-" + id;
			Assert.True(FightScene.MoveClips.TryGetValue(id, out var clip) && clip == want, $"{id} names its own clip ({want})");
			Assert.Equal(want, FighterAnimSet.Kappa.MoveClip(want), want + " has no Kitsune override on the Kappa body");
		}
	}

	static HashSet<string> KappaSpecialMoveIds() => SpecialLoader.LoadDirectory(ContentPaths.Data("moves"))
		.Where(s => s.Source == "kappa").Select(s => s.Id).ToHashSet();

	// F2/F6: a Kappa clip file agrees with its move: same length, hit window = the move's active frames.
	[Test]
	public static void KappaClips_AgreeWithTheirMoves()
	{
		foreach (var mv in KappaMoves())
		{
			string path = Path.Combine(ContentPaths.Data("clips"), mv.Id + ".json");
			Assert.True(File.Exists(path), "clip file for " + mv.Id);
			using var doc = JsonDocument.Parse(File.ReadAllText(path));
			var c = doc.RootElement;
			Assert.Equal(mv.Id, c.GetProperty("id").GetString(), "clip id");
			Assert.Equal(mv.TotalFrames, c.GetProperty("frames_total").GetInt32(), mv.Id + " frames_total");
			Assert.Equal(mv.FirstActive, c.GetProperty("hit_start").GetInt32(), mv.Id + " hit_start");
			Assert.Equal(mv.LastActive, c.GetProperty("hit_end").GetInt32(), mv.Id + " hit_end");
		}
	}

	// The Kappa's push width is data (E5 default 120 otherwise) and reaches the sim and the replay header.
	[Test]
	public static void KappaPushWidth_ReachesTheSimAndTheRecording()
	{
		var m = KappaMatch();
		Assert.Equal(130 * SimConfig.Scale, m.P2.PushWidth, "Kappa push width in the sim");
		Assert.True(m.P1.PushWidth == 0 || m.P1.PushWidth == SimConfig.Default.BodyWidth * SimConfig.Scale, "Ryo keeps the default (0 = unset): " + m.P1.PushWidth);
		var rec = ReplayRecorder.Begin(m, null, 0);
		Assert.Equal(130 * SimConfig.Scale, rec.Recording.Header.Fighters[1].PushWidth, "recorded");
	}

	// ---- the Tanuki and the Kappa ------------------------------------------------------------------------------

	// T0/T2: the Tanuki copies one of RYO's specials. The Kappa files (a "kappa" source in data/moves) must not leak into
	// Ryo's kit, the Tanuki's kit, or the draft pool.
	[Test]
	public static void Tanuki_AndRyo_AndTheDraftPool_NeverContainKappaMoves()
	{
		var ryo = Kit.Load(Kit.Ryo, Sources);
		Assert.True(!ryo.Moves.Any(x => x.Id.StartsWith("kappa")) && !ryo.Specials.Any(x => x.Id.StartsWith("kappa")), "Ryo's kit");
		var kit = Kit.Load("tanuki", Sources);
		Assert.True(!kit.Moves.Any(x => x.Id.StartsWith("kappa")), "Tanuki's own kit");
		var content = HarnessContent.Load(ContentPaths.DataDir, ContentPaths.FixturesDir);
		var m = new Match(null, ryo.Moves, TanukiKit.Build(ryo, null).Moves);
		Assert.True(!m.P2.Moves.Any(x => x.Id.StartsWith("kappa")), "Tanuki match P2 moves: " + string.Join(",", m.P2.Moves.Select(x => x.Id)));
		var pool = string.Join(",", content.Pool.Specials.Select(a => a.Id));
		Assert.True(!pool.Contains("kappa-light") && !pool.Contains("kappa-medium") && !pool.Contains("kappa-heavy") && !pool.Contains("kappa-throw"), "pool: " + pool);
	}

	// ---- StandingHabitTests exemption is too wide --------------------------------------------------------------

	static string? JumpAnswerMove(AiDecision d) => d.Action == "throw" ? "ryo-generic-throw" : d.Action.StartsWith("normal:") ? d.Action[7..] : null;

	// YOK-177/Y4: while a standing habit is pending, a jump answer may replace it; then the move that starts is the
	// jump answer's, not any move. The author's exemption accepts any move on that step.
	[Test]
	public static void PendingHabit_OnlyTheJumpAnswersMoveMayStart()
	{
		var p = Profile("tanuki-boss");
		for (uint seed = 1; seed <= 3; seed++)
		{
			var ryo = Kit.Load(Kit.Ryo, Sources);
			var tan = TanukiKit.Build(ryo, null);
			var cfg = SimConfig.Default with { P2JumpPunishPct = 50 };
			var m = new Match(cfg, ryo.Moves, tan.Moves); ryo.Equip(m.P1); tan.Equip(m.P2);
			var log = new List<AiDecision>();
			var ai = new ProfileAi(p, 1, AiLoadout.From(m.P2), 900 + seed, null, AiFightTier.Tanuki) { Log = log };
			var rng = new SimRng(seed ^ 0x7177u); InputBits held = InputBits.None; int left = 0; bool turtle = true;
			int from = 0, pending = -1; string? lastAnswer = null; FighterState ps = m.P2.State; string? pm = null; int pf = 0;
			for (int i = 0; i < 20000; i++)
			{
				if (m.Tick % 30 == 0) turtle = rng.Next(100) < 70;
				var raw = AiHarness.RandomInput(rng, ref held, ref left);
				var a = turtle ? new FighterInput(m.P1.X < m.P2.X ? InputBits.Left : InputBits.Right) : raw;
				var b = ai.Next(AiView.Capture(m));
				m.Step(a, b);
				var v = AiView.Capture(m).Fighter(1);
				bool start = v.State == FighterState.Attack && (ps != FighterState.Attack || pm != v.MoveId || v.MoveFrame < pf);
				for (; from < log.Count; from++)
				{
					var d = log[from];
					if (d.Kind == "habit") pending = d.Tick;
					else if (pending >= 0 && d.Kind == "react" && d.Trigger == "opponent_jumping") { lastAnswer = JumpAnswerMove(d); pending = -1; }
				}
				if (start && pending >= 0 && v.MoveId != "ryo-generic-throw")
					Assert.True(false, $"seed {seed}: {v.MoveId} started at {m.Tick} while the habit from {pending} was pending (last jump answer {lastAnswer})");
				if (start && v.MoveId == "ryo-generic-throw") pending = -1;
				ps = v.State; pm = v.MoveId; pf = v.MoveFrame;
				if (m.Phase == MatchPhase.Over) break;
			}
		}
	}
}
