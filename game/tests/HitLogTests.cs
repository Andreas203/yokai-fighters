using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using YokaiFighters.Ai;
using YokaiFighters.Harness;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-157 (P5, P6, Y2, R5; YOK-154 report findings 4 and 5): the harness's per-hit and habit log, and the bot's anti-air check.
/// The log only listens: the same seed plays the same runs with it on or off.
/// </summary>
public static class HitLogTests
{
	static HarnessContent? _content;
	static HarnessContent Content => _content ??= HarnessContent.Load(ContentPaths.DataDir, ContentPaths.FixturesDir);
	static BotConfig Bot => BotConfig.LoadDefault(ContentPaths.DataDir);

	static BatchResult? _logged;
	/// <summary>Ten runs from seed 1 with the raw hit log on.</summary>
	static BatchResult Logged => _logged ??= RunHarness.PlayBatch(Content with { RawHitLog = true }, Bot, 1, 10);

	static IEnumerable<FightRecord> Fights(BatchResult b) => b.Runs.SelectMany(r => r.Nodes).Where(n => n.Fight is not null).Select(n => n.Fight!);

	static string Outcome(BatchResult b) => string.Join("\n", b.Runs.Select(r =>
		$"{r.Seed} {r.Won} {r.RowReached} {r.EndNode} {r.FinalHealth} {r.Coins} {r.FightTicks} {r.SessionHash:x16} | " +
		string.Join(" ", r.Nodes.Select(n => n.Fight is { } f ? $"{n.Id}:{f.Won}/{f.Ticks}/{f.DamageTaken}/{f.DamageDealt}/{f.MatchHash:x16}/aa{f.Bot.AntiAirs}" : n.Id))));

	[Test]
	public static void Logging_OnOrOff_SameSeed_SameRuns()
	{
		var bot = Bot;
		var off = RunHarness.PlayBatch(Content with { HitLog = false }, bot, 1, 10);
		var on = RunHarness.PlayBatch(Content with { HitLog = true }, bot, 1, 10);
		Assert.True(Fights(off).All(f => f.Log is null), "log off: no log on any fight");
		Assert.True(Fights(Logged).All(f => f.Log is { Raw: not null }), "raw log on: every fight has its lines");
		string o = Outcome(off);
		Assert.True(o == Outcome(on), "the tallies change no tick of any fight or run");
		Assert.True(o == Outcome(Logged), "nor does the raw hit log");
		Assert.True(HarnessReport.ToJson(on) == HarnessReport.ToJson(RunHarness.PlayBatch(Content, bot, 1, 10)), "logged results are byte-identical run to run");
		Assert.True(!HarnessReport.ToJson(off).Contains("\"log\":"), "log off: the fight blocks are the old schema");
	}

	[Test]
	public static void HitLog_DamageAddsUpToHealthLost_AndEachLineIsTallied()
	{
		int fights = 0, ryoHits = 0, throws = 0;
		foreach (var f in Fights(Logged))
		{
			fights++;
			var log = f.Log!;
			int[] dmg = { log.Moves[0].Values.Sum(t => t.Damage), log.Moves[1].Values.Sum(t => t.Damage) };
			// All fight damage reaches the sim as Hit events (strikes, projectiles, landed throws); a KO's last hit can overkill.
			if (f.HealthLeft > 0) Assert.Equal(f.DamageTaken, dmg[1], "opponent's logged damage = Ryo's health lost");
			else Assert.True(dmg[1] >= f.DamageTaken, "KO: logged damage covers the health lost");
			if (!f.Won) Assert.Equal(f.DamageDealt, dmg[0], "Ryo's logged damage = the opponent's health lost");
			else Assert.True(dmg[0] >= f.DamageDealt, "KO: Ryo's logged damage covers the opponent's health lost");
			for (int side = 0; side < 2; side++)
			{
				foreach (var (id, t) in log.Moves[side])
				{
					Assert.True(t.Blocked <= t.Hits && t.Counter <= t.Hits - t.Blocked, $"{id}: blocked and counter are within its hits");
					Assert.True(t.ThrowsLanded <= t.Hits, $"{id}: a landed throw is also a hit");
					var lines = log.Raw!.Where(h => h.Attacker == side && h.MoveId == id).ToList();
					Assert.Equal(t.Hits, lines.Count(h => h.Kind == "hit"), $"{id}: one hit line per hit");
					Assert.Equal(t.Blocked, lines.Count(h => h.Kind == "hit" && h.Blocked), $"{id}: blocked lines");
					Assert.Equal(t.Damage, lines.Sum(h => h.Damage), $"{id}: line damage = tallied damage (throw lines carry none)");
					Assert.Equal(t.ThrowsLanded, lines.Count(h => h.Kind == "throw"), $"{id}: throw lines");
					Assert.Equal(t.ThrowsBroken, lines.Count(h => h.Kind == "throw_break"), $"{id}: break lines");
					Assert.True(lines.Where(h => h.Kind == "hit" && h.Blocked).All(h => h.Damage == 0), $"{id}: a blocked hit does no damage");
				}
				Assert.Equal(log.ThrowBreaks[1 - side], log.Moves[side].Values.Sum(t => t.ThrowsBroken), "a break is counted for the side that broke it");
			}
			Assert.True(log.Raw!.Select(h => h.Tick).SequenceEqual(log.Raw!.Select(h => h.Tick).OrderBy(x => x)), "lines in tick order");
			ryoHits += log.Moves[0].Values.Sum(t => t.Hits - t.Blocked);
			throws += log.Moves[0].Values.Sum(t => t.ThrowsLanded) + log.Moves[1].Values.Sum(t => t.ThrowsLanded);
		}
		Assert.True(fights > 0 && ryoHits > 0, $"fights were logged ({fights}, {ryoHits} Ryo hits)");
		Assert.True(throws > 0, "at least one landed throw was logged in ten runs");
	}

	[Test]
	public static void HabitLog_CountsEveryRoll_AndPunishesStayWithinTheUncoveredHabits()
	{
		int fired = 0;
		foreach (var f in Fights(Logged))
		{
			var l = f.Log!;
			Assert.True(l.HabitWhen.Length > 0 && l.HabitAction.Length > 0, "the profile's habit is named");
			Assert.True(l.HabitRolls >= l.HabitFired && l.HabitFired >= l.HabitCovered && l.HabitPunished <= l.HabitFired - l.HabitCovered,
				$"{f.Profile}: rolls {l.HabitRolls} >= fired {l.HabitFired} >= covered {l.HabitCovered}; punished {l.HabitPunished} within the uncovered");
			fired += l.HabitFired;
		}
		Assert.True(fired > 0, "the opponents' habits fired in ten runs");
	}

	[Test]
	public static void HabitLog_MatchesTheAisOwnDecisionLog()
	{
		var session = RunSession.Start(Content.Pool, Content.Map, Content.Scenes, 3);
		Assert.True(session.Enter(session.Reachable()[0]).Ok, "enter row 1");
		var bot = Bot;
		var m = NodeFight.NewMatch(Content.Sources, session.Run, session, session.Node!.Fighter, bot.Scheme);
		var profile = Content.Profiles(session.Node.Fighter)[0];
		var ai = new ProfileAi(profile, 1, AiLoadout.From(m.P2), 5, session.Node.ReactionFrames);
		var ryo = new NewcomerBot(bot.Fight, BotKit.From(m.P1), 6);
		var log = new FightLog(raw: false);
		log.Attach(m, ai);
		for (int t = 0; t < 6000 && m.Phase != MatchPhase.Over; t++)
		{
			var view = AiView.Capture(m);
			var a = ryo.Next(view);
			var b = ai.Next(view);
			log.AfterAi(ai, m.Tick);
			m.Step(a, b);
		}
		var kinds = ai.Log!.Select(d => d.Kind).ToList();
		Assert.Equal(kinds.Count(k => k is "habit" or "habit_no" or "habit_covered"), log.HabitRolls, "every habit roll");
		Assert.Equal(kinds.Count(k => k is "habit" or "habit_covered"), log.HabitFired, "every habit fired");
		Assert.Equal(kinds.Count(k => k == "habit_covered"), log.HabitCovered, "every covered habit");
		Assert.Equal(BehaviourProfile.ObservationName(profile.Habit.When), log.HabitWhen, "the habit's trigger, as the data names it");
	}

	[Test]
	public static void Results_HaveTheLogBlocks_AndTheSummaryPerMoveAndPerHabit()
	{
		using var doc = JsonDocument.Parse(HarnessReport.ToJson(Logged));
		var root = doc.RootElement;
		Assert.Equal(1, root.GetProperty("schema_version").GetInt32(), "additive: still schema version 1");
		var s = root.GetProperty("summary");
		var moves = s.GetProperty("moves");
		foreach (string side in FightLogReport.Sides)
		{
			double pct = 0;
			foreach (var mv in moves.GetProperty(side).EnumerateObject())
			{
				foreach (string k in new[] { "hits", "blocked", "counter", "damage", "throws_landed", "throws_broken", "fights", "damage_pct" })
					Assert.True(mv.Value.TryGetProperty(k, out _), $"summary.moves.{side}.{mv.Name}.{k}");
				if (mv.Value.GetProperty("damage_pct").ValueKind == JsonValueKind.Number) pct += mv.Value.GetProperty("damage_pct").GetDouble();
			}
			Assert.True(moves.GetProperty(side).EnumerateObject().Any(), $"{side} has moves");
			Assert.True(Math.Abs(pct - 100) < 1, $"{side}: damage shares add to 100% ({pct})");
		}
		var byProfile = s.GetProperty("moves_by_profile");
		Assert.True(byProfile.EnumerateObject().Any(), "moves_by_profile has a row per opponent profile");
		int profileFights = 0;
		foreach (var pr in byProfile.EnumerateObject())
		{
			profileFights += pr.Value.GetProperty("fights").GetInt32();
			foreach (string side in FightLogReport.Sides)
			{
				var mv = pr.Value.GetProperty(side).EnumerateObject().ToList();
				double sum = mv.Where(x => x.Value.GetProperty("damage_pct").ValueKind == JsonValueKind.Number).Sum(x => x.Value.GetProperty("damage_pct").GetDouble());
				Assert.True(mv.Count == 0 || sum == 0 || Math.Abs(sum - 100) < 1, $"{pr.Name}.{side}: damage shares add to 100% ({sum})");
			}
		}
		Assert.Equal(Fights(Logged).Count(), profileFights, "every logged fight is in one profile row");
		var habits = s.GetProperty("habits");
		Assert.True(habits.EnumerateObject().Any(), "a habit row per profile fought");
		foreach (var h in habits.EnumerateObject())
			foreach (string k in new[] { "when", "do", "fights", "rolls", "fired", "covered", "punished", "punished_pct_of_uncovered", "bot_anti_airs" })
				Assert.True(h.Value.TryGetProperty(k, out _), $"summary.habits.{h.Name}.{k}");
		int fightBlocks = 0;
		foreach (var run in root.GetProperty("runs").EnumerateArray())
			foreach (var node in run.GetProperty("nodes").EnumerateArray())
			{
				if (!node.TryGetProperty("fight", out var fight)) continue;
				var log = fight.GetProperty("log");
				foreach (string k in new[] { "moves", "throw_breaks", "throw_clashes", "habit", "hit_log" })
					Assert.True(log.TryGetProperty(k, out _), $"fight.log.{k}");
				foreach (var line in log.GetProperty("hit_log").EnumerateArray())
					foreach (string k in new[] { "tick", "attacker", "move", "kind", "blocked", "counter", "damage" })
						Assert.True(line.TryGetProperty(k, out _), $"hit_log line .{k}");
				fightBlocks++;
			}
		Assert.True(fightBlocks > 0, "fight log blocks written");
		var plain = JsonDocument.Parse(HarnessReport.ToJson(RunHarness.PlayBatch(Content, Bot, 1, 2))).RootElement;
		bool anyRaw = plain.GetProperty("runs").EnumerateArray().SelectMany(r => r.GetProperty("nodes").EnumerateArray())
			.Any(n => n.TryGetProperty("fight", out var f) && f.GetProperty("log").TryGetProperty("hit_log", out _));
		Assert.True(!anyRaw, "the raw hit log is opt-in");
	}

	/// <summary>
	/// YOK-157 (R5; E7 buffer, Y4 delay): the opponent jumps over and over while the bot (anti_air_pct 100) presses normals non-stop, so a jump often
	/// shows while the bot is mid-move. Each jump still gets its anti-air, on the first free tick, never before the bot's
	/// reaction frames, at most one per jump, and the anti-air move really starts within the E7 command buffer of the roll
	/// (a roll spent on a press the sim then drops would over-count bot_anti_airs and waste the jump).
	/// </summary>
	[Test]
	public static void Bot_AntiAirs_OnTheFirstFreeTick_WhileTheOpponentIsAirborne()
	{
		AntiAirRun(walkIn: false); // jumps from range: the bot's normals whiff
		AntiAirRun(walkIn: true);  // the opponent walks in between jumps: the bot's normals connect, so hitstop outlasts its plans
	}

	static void AntiAirRun(bool walkIn)
	{
		var session = RunSession.Start(Content.Pool, Content.Map, Content.Scenes, 5);
		Assert.True(session.Enter(session.Reachable()[0]).Ok, "enter row 1");
		var bot = Bot;
		var cfg = bot.Fight with
		{
			AntiAirPct = 100, BlockPct = 0, MistakePct = 0, PokeRange = 100000, DecideEvery = 1,
			NormalWeight = 100, WalkWeight = 0, SpecialWeight = 0, ThrowWeight = 0, JumpWeight = 0, WaitWeight = 0,
		};
		var m = NodeFight.NewMatch(Content.Sources, session.Run, session, session.Node!.Fighter, bot.Scheme);
		var ryo = new NewcomerBot(cfg, BotKit.From(m.P1), 99);
		int n = cfg.ReactionFrames;
		var jumpStarts = new List<int>();
		var antiAirTicks = new List<int>();
		var aaStarts = new List<int>();
		string aaMove = ryo.AntiAirMove!;
		Assert.True(aaMove is not null, "Ryo has an anti-air normal");
		bool wasAirborne = false;
		for (int t = 0; t < 1800 && m.Phase != MatchPhase.Over; t++)
		{
			var view = AiView.Capture(m);
			bool air = view.Fighter(1).Airborne;
			if (air && !wasAirborne) jumpStarts.Add(view.Tick);
			wasAirborne = air;
			int before = ryo.Stats.AntiAirs;
			var input = ryo.Next(view);
			if (ryo.Stats.AntiAirs > before) antiAirTicks.Add(view.Tick);
			var toward = m.P2.X > m.P1.X ? InputBits.Left : InputBits.Right;
			m.Step(input, new FighterInput(t % 70 < 2 ? InputBits.Up : walkIn ? toward : InputBits.None)); // a fresh jump every 70 ticks
			if (m.P1.CurrentMove?.Id == aaMove && m.P1.MoveFrame == 1) aaStarts.Add(m.Tick);
		}
		Assert.True(jumpStarts.Count >= (walkIn ? 3 : 5), $"walk-in {walkIn}: the opponent jumped ({jumpStarts.Count}; phase {m.Phase}, ticks {m.Tick})");
		int late = 0;
		var answered = new HashSet<int>();
		foreach (int aa in antiAirTicks)
		{
			int j = jumpStarts.Last(s => s <= aa - (n - 1));
			Assert.True(answered.Add(j), $"one anti-air roll per jump: the jump at {j} was answered twice");
			Assert.True(aa - j >= n - 1, $"Y4: the anti-air at {aa} comes no sooner than {n} reaction frames after the jump at {j}");
			if (aa - j > n - 1) late++;
			int buffer = InputConfig.Default.CommandBuffer;
			Assert.True(aaStarts.Any(st => st > aa && st <= aa + buffer), $"walk-in {walkIn}, E7: the anti-air rolled at {aa} starts {aaMove} within {buffer} ticks");
		}
		Assert.True(antiAirTicks.Count >= jumpStarts.Count - 1, $"walk-in {walkIn}: every jump the bot could see was anti-aired ({antiAirTicks.Count} of {jumpStarts.Count})");
		Assert.True(late > 0, $"some anti-airs came after the jump's first shown tick, when the bot got free ({late})");
	}

	static AiView Staged(AiView b, int p1x, int p2x, bool p2Air, int tick) =>
		b with { Tick = tick, P1 = b.P1 with { X = p1x, Facing = 1 }, P2 = b.P2 with { X = p2x, Facing = -1, Airborne = p2Air } };

	/// <summary>
	/// Designer decision (YOK-157): holding an idle guard (YOK-155), the bot guards through the first jump it sees in the
	/// fight, with no anti-air roll, and anti-airs later jumps as usual. Per fight: a new bot lets its first one go again.
	/// Without a guard up, the first jump is anti-aired.
	/// </summary>
	[Test]
	public static void Bot_UnderAnIdleGuard_LetsTheFightsFirstJumpGo_ThenAntiAirs()
	{
		var session = RunSession.Start(Content.Pool, Content.Map, Content.Scenes, 5);
		Assert.True(session.Enter(session.Reachable()[0]).Ok, "enter row 1");
		var m = NodeFight.NewMatch(Content.Sources, session.Run, session, session.Node!.Fighter, ControlScheme.Kihon);
		var kit = BotKit.From(m.P1);
		var b = AiView.Capture(m);
		int far = 100 * SimConfig.Scale;
		BotFightConfig Cfg(int guardPct) => Bot.Fight with
		{
			ReactionFrames = 1, DecideEvery = 1, WaitTicks = 200, WaitWeight = 1, WalkWeight = 0, NormalWeight = 0, SpecialWeight = 0,
			ThrowWeight = 0, JumpWeight = 0, MistakePct = 0, BlockPct = 0, AntiAirPct = 100, IdleGuardPct = guardPct,
		};
		bool Attacks(FighterInput i) => (i.Bits & InputBits.Attacks) != 0;

		for (int fight = 0; fight < 2; fight++)
		{
			var bot = new NewcomerBot(Cfg(100), kit, 3);
			int t = 0;
			for (; t < 5; t++) Assert.True(!Attacks(bot.Next(Staged(b, 0, far, false, t))), "guarded wait");
			Assert.True(bot.Stats.IdleGuards > 0, "an idle guard is up");
			for (int k = 0; k < 10; k++, t++)
			{
				var o = bot.Next(Staged(b, 0, far, true, t));
				Assert.True(!Attacks(o), $"fight {fight}: the first jump under guard is not anti-aired (tick {t})");
				Assert.True((o.Bits & InputBits.Left) != 0, $"fight {fight}: still holding away (tick {t}, {o.Bits})");
			}
			Assert.Equal(1, bot.Stats.GuardedJumpsLetGo, $"fight {fight}: one jump let go");
			Assert.Equal(0, bot.Stats.AntiAirs, $"fight {fight}: no anti-air roll spent on it");
			for (int k = 0; k < 5; k++, t++) bot.Next(Staged(b, 0, far, false, t));
			Assert.True(Attacks(bot.Next(Staged(b, 0, far, true, t++))), $"fight {fight}: the second jump is anti-aired");
			Assert.Equal(1, bot.Stats.AntiAirs, $"fight {fight}: one anti-air");
			Assert.Equal(1, bot.Stats.GuardedJumpsLetGo, $"fight {fight}: only the first jump is let go");
		}

		var open = new NewcomerBot(Cfg(0), kit, 3);
		for (int t = 0; t < 5; t++) open.Next(Staged(b, 0, far, false, t));
		Assert.True(Attacks(open.Next(Staged(b, 0, far, true, 5))), "no guard up: the first jump is anti-aired");
		Assert.Equal(0, open.Stats.GuardedJumpsLetGo, "nothing let go without a guard");
	}
}
