using System;
using System.Collections.Generic;
using System.Linq;
using YokaiFighters.Ai;
using YokaiFighters.Fight;
using YokaiFighters.Harness;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>YOK-177 final probes. Designer decision 2026-10-21: a fired habit always comes out; Y2; YOK-165 throw answers.</summary>
public static class StandingHabitAlwaysProbes
{
	static BehaviourProfile Profile(string id) => BehaviourProfile.LoadFile(ContentPaths.Data("profiles") + "/" + id + ".json");

	static Match NewMatch(string opp, int? jp = null)
	{
		FightScene.Run = null;
		return NodeFight.NewMatch(FightScene.Sources, null, null, opp, FightScene.DefaultScheme,
			shape: jp is null ? null : c => c with { P2JumpPunishPct = jp });
	}

	static Func<Match, FighterInput> Random(uint seed)
	{
		var rng = new SimRng(seed); InputBits held = InputBits.None; int left = 0;
		return _ => AiHarness.RandomInput(rng, ref held, ref left);
	}

	// Ryo walks at the AI and mashes light punch / light kick: heavy pressure.
	static Func<Match, FighterInput> Mash() => m =>
	{
		var fwd = m.P1.X < m.P2.X ? InputBits.Right : InputBits.Left;
		bool close = Math.Abs(m.P2.X - m.P1.X) / SimConfig.Scale < 90;
		return new FighterInput((close ? InputBits.None : fwd) | (m.Tick % 2 == 0 ? (m.Tick % 4 == 0 ? InputBits.LightPunch : InputBits.LightKick) : InputBits.None));
	};

	sealed class Res { public int Fired, Doubles, Replaced, Cut, Lost, Rounds; }

	static Res Track(string prof, string opp, AiFightTier tier, Func<Match, FighterInput> p1, int ticks, string move, int react, int? jp, bool standing = true, BehaviourProfile? custom = null, AiAction? act = null)
	{
		var p = custom ?? Profile(prof);
		var m = NewMatch(opp, jp);
		var log = new List<AiDecision>();
		int round = 0;
		ProfileAi NewAi() => new(p, 1, AiLoadout.From(m.P2), 5000u + (uint)round, react, tier, standingHabits: standing) { Log = log };
		var ai = NewAi();
		var r = new Res(); var trace = new List<(int i, string s)>();
		bool pending = false, wasMove = false; int prevFrame = 0; int starts = 0, from = 0, firedAt = 0, tickNow = 0;
		void Close(bool cut)
		{
			if (!pending) return;
			if (starts > 1) { r.Doubles++; if (r.Doubles <= 3) Godot.GD.Print($"DOUBLE fired@{firedAt} {string.Join(" | ", log.Skip(Math.Max(0, from - 6)).Take(8).Select(d => d.ViewTick + ":" + d.Kind + ":" + d.Action))}"); }
			if (starts == 0) { if (cut) r.Cut++; else { r.Lost++; if (r.Lost <= 4) Godot.GD.Print($"TRACE {string.Join(" ", trace.Where(t => t.i >= firedAt - 2 && t.i <= firedAt + 70).Select(t => t.i + t.s))}"); if (r.Lost <= 4) Godot.GD.Print($"LOST fired@{firedAt} now {tickNow} {string.Join(" | ", log.Skip(Math.Max(0, from - 5)).Take(7).Select(d => d.ViewTick + ":" + d.Kind + ":" + d.Action))}"); } }
			pending = false;
		}
		for (int i = 0; i < ticks; i++)
		{
			var b = ai.Next(AiView.Capture(m));
			m.Step(p1(m), b); tickNow = i;
			for (; from < log.Count; from++)
			{
				var d = log[from];
				if (d.Kind == "habit") { Close(false); pending = true; starts = 0; firedAt = i; r.Fired++; }
				else if (pending && d.Kind == "react" && d.Trigger == "opponent_jumping") { if (starts == 0) r.Replaced++; else Close(false); pending = false; }
				else if (pending && starts > 0 && d.Action.EndsWith(move.Replace("ryo-generic-throw", "throw"))) Close(false); // another pick of the same move, not the habit
			}
			var v = AiView.Capture(m).Fighter(1);
			trace.Add((i, "[" + v.State.ToString()[..2] + (v.MoveId ?? "").Replace("kitsune-", "") + "/" + b.Bits + "]")); if (trace.Count > 400) trace.RemoveAt(0);
			bool isMove = v.State == FighterState.Attack && (act is { } aa ? AiLoadout.From(m.P2).IsScreenMoveOf(aa, v.MoveId) : v.MoveId == move);
			// a start is a new Attack state or the frame counter going back (same move back-to-back)
			if (isMove && (!wasMove || v.MoveFrame < prevFrame) && pending && i - firedAt < 150) starts++;
			wasMove = isMove; prevFrame = v.MoveFrame;
			if (m.Phase == MatchPhase.Over) { Close(true); m.Reset(); round++; ai = NewAi(); wasMove = false; }
		}
		Close(true);
		r.Rounds = round;
		return r;
	}

	[Test]
	public static void Apprentice_EveryFiredHabit_StartsOnce_UnderPressure()
	{
		foreach (var (name, pf) in new (string, Func<Func<Match, FighterInput>>)[] { ("random", () => Random(8)), ("mash", () => Mash()) })
			foreach (int react in new[] { 1, 30 })
			{
				var r = Track("apprentice", "kitsune", AiFightTier.Standard, pf(), 40000, "kitsune-heavy-punch", react, null);
				Assert.True(r.Fired >= 20, $"{name}/{react}: fired {r.Fired}");
				Assert.Equal(0, r.Doubles, $"{name}/{react}: habits that started twice");
				Assert.Equal(0, r.Lost, $"{name}/{react}: fired habits that never started (replaced {r.Replaced}, cut {r.Cut}, fired {r.Fired})");
			}
	}

	[Test]
	public static void Tanuki_ThrowHabit_UnderMash_StillComesOut()
	{
		foreach (int react in new[] { 1, 6, 30 })
		{
			var r = Track("tanuki-boss", "ryo", AiFightTier.Tanuki, Mash(), 40000, "ryo-generic-throw", react, 100);
			Assert.Equal(0, r.Doubles, $"react {react}: doubles (fired {r.Fired})");
			Assert.Equal(0, r.Lost, $"react {react}: lost (fired {r.Fired}, replaced {r.Replaced}, cut {r.Cut})");
		}
	}

	[Test]
	public static void Tanuki_ThrowHabit_AtBlocker_HitMidWait_StillComesOut()
	{
		// Ryo turtles, and every 37 ticks lashes out with a light punch (hitting the Tanuki while its habit waits).
		FighterInput P1(Match m)
		{
			var back = m.P1.X < m.P2.X ? InputBits.Left : InputBits.Right;
			return new FighterInput(m.Tick % 37 < 3 ? InputBits.LightPunch : back);
		}
		foreach (int react in new[] { 1, 12 })
		{
			var r = Track("tanuki-boss", "ryo", AiFightTier.Tanuki, P1, 40000, "ryo-generic-throw", react, 100);
			Assert.Equal(0, r.Doubles, $"react {react}: doubles (fired {r.Fired})");
			Assert.Equal(0, r.Lost, $"react {react}: lost (fired {r.Fired}, replaced {r.Replaced}, cut {r.Cut})");
		}
	}

	[Test]
	public static void Kappa_ThrowAntiAir_UnderPressure_NoSubstitutedNormals()
	{
		// YOK-165: a throw jump answer is the throw. After such a decision, the next move to start must be the throw.
		foreach (var pid in new[] { "kappa-patient", "kappa-aggressive" })
		{
			var p = Profile(pid);
			var m = NewMatch("kappa", 100);
			var log = new List<AiDecision>();
			var ai = new ProfileAi(p, 1, AiLoadout.From(m.P2), 77, 6) { Log = log };
			string throwId = AiLoadout.From(m.P2).ThrowMoveId!;
			var rng = new SimRng(4); InputBits held = InputBits.None; int left = 0;
			int from = 0, answers = 0, bad = 0; bool pending = false, wasAtk = false; string? lastId = null;
			for (int i = 0; i < 40000; i++)
			{
				var b = ai.Next(AiView.Capture(m));
				var a = m.Tick % 50 < 3 ? new FighterInput(InputBits.Up) : AiHarness.RandomInput(rng, ref held, ref left);
				m.Step(a, b);
				for (; from < log.Count; from++)
				{
					var d = log[from];
					if (d.Kind == "react" && d.Trigger == "opponent_jumping" && d.Action == "throw") { pending = true; answers++; }
					else pending = false;
				}
				var v = AiView.Capture(m).Fighter(1);
				bool atk = v.State == FighterState.Attack;
				if (pending && atk && (!wasAtk || v.MoveId != lastId))
				{
					if (v.MoveId != throwId) bad++;
					pending = false;
				}
				wasAtk = atk; lastId = v.MoveId;
				if (m.Phase == MatchPhase.Over) { m.Reset(); ai = new ProfileAi(p, 1, AiLoadout.From(m.P2), 78, 6) { Log = log }; pending = false; wasAtk = false; }
			}
			Assert.True(answers >= 5, $"{pid}: throw answers {answers}");
			Assert.Equal(0, bad, $"{pid}: a normal came out in place of a throw answer");
		}
	}

	[Test]
	public static void LegacyFlagOff_NeverFiresStandingHabits()
	{
		var r = Track("apprentice", "kitsune", AiFightTier.Standard, Mash(), 8000, "kitsune-heavy-punch", 30, null, standing: false);
		Assert.Equal(0, r.Fired, "flag off: no standing habit fires");
	}

	[Test]
	public static void Determinism_SameSeedSameResult_UnderMash()
	{
		var a = Track("tanuki-boss", "ryo", AiFightTier.Tanuki, Mash(), 15000, "ryo-generic-throw", 6, 100);
		var b = Track("tanuki-boss", "ryo", AiFightTier.Tanuki, Mash(), 15000, "ryo-generic-throw", 6, 100);
		Assert.Equal(a.Fired, b.Fired, "fired");
		Assert.Equal(a.Replaced, b.Replaced, "replaced");
		Assert.Equal(a.Lost, b.Lost, "lost");
	}

	// Special habit: a standing at_close_range habit whose action is a special must be recognised as started (no double press).
	[Test]
	public static void SpecialHabit_IsRecognisedAsStarted_NoDoubles()
	{
		var path = System.IO.Path.Combine(ContentPaths.Data("profiles"), "apprentice.json");
		var text = System.Text.RegularExpressions.Regex.Replace(System.IO.File.ReadAllText(path), "\"do\": \"normal:kitsune-heavy-punch\"", "\"do\": \"special:foxfire\"");
		var tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "apprentice-special-probe.json");
		System.IO.File.WriteAllText(tmp, text.Replace("\"id\": \"apprentice\"", "\"id\": \"apprentice\""));
		var prof = BehaviourProfile.LoadFile(tmp);
		Assert.True(prof.Habit.Do.Kind == ActionKind.Special, "the probe profile's habit is a special");
		foreach (int react in new[] { 1, 30 })
		{
			var r = Track("", "kitsune", AiFightTier.Standard, Mash(), 30000, "", react, null, true, prof, prof.Habit.Do);
			Assert.True(r.Fired >= 20, $"react {react}: fired {r.Fired}");
			Assert.Equal(0, r.Doubles, $"react {react}: special habit pressed twice");
			Assert.Equal(0, r.Lost, $"react {react}: special habit lost (fired {r.Fired}, replaced {r.Replaced})");
		}
	}
}
