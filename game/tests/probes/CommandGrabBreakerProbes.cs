using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-225 breaker probes (command grabs). Expected values come from rules.md C4 / E12 / E24 / K3 / T4, never from the
/// code. The TEST FIXTURE River Grab (6/2/30, grab boxes frames 7-8, range 130) sits in slot C and is fired by a direct
/// slot request on tick 1, so frame n is tick n: grab on tick 7, break window ticks 8..7+W, land on tick 8+W.
/// </summary>
public static class CommandGrabBreakerProbes
{
	const int S = SimConfig.Scale;
	const int GrabTick = 7;
	const InputBits LP = InputBits.LightPunch, LK = InputBits.LightKick, THROW = LP | LK;
	static readonly FighterInput Idle = FighterInput.None;
	static readonly FighterInput Grab = FighterInput.Special(SpecialSlot.C);
	static string Repo => Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "..");
	static string FixtureText => File.ReadAllText(ProjectSettings.GlobalizePath("res://tests/fixtures/command-grabs/test-river-grab.json"));
	static JsonObject Fd(JsonObject doc) => doc["levels"]!["1"]!["frame_data"]!.AsObject();
	static SpecialData River(Action<JsonObject>? edit = null)
	{
		var doc = JsonNode.Parse(FixtureText)!.AsObject();
		edit?.Invoke(doc);
		return SpecialLoader.Parse(doc.ToJsonString());
	}
	static SpecialData[] Starters() => SpecialLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureSpecialsDir));
	static MoveData[] Moves() =>
		MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureNormalsDir))
			.Concat(MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureThrowsDir))).ToArray();
	static Func<int, FighterInput> At(int tick, FighterInput i) => t => t == tick ? i : Idle;
	static Func<int, FighterInput> None => _ => Idle;

	sealed class Log
	{
		public readonly List<(int tick, ThrowEvent e)> Throws = new();
		public ThrowOutcome[] Outcomes => Throws.Select(t => t.e.Outcome).ToArray();
		public string Text => string.Join(",", Throws.Select(t => $"{t.e.Attacker}:{t.e.Outcome}@{t.tick}"));
		public int TickOf(ThrowOutcome o) => Throws.FirstOrDefault(t => t.e.Outcome == o).tick;
	}

	static Match Setup(Log log, SpecialData? grab = null, bool p2Grab = false, int? gap = null)
	{
		var m = new Match(null, Moves(), Moves());
		foreach (var f in m.Fighters) FightScene.EquipStarters(f, Starters());
		m.P1.Equip(SpecialSlot.C, grab ?? River(), 1, null, m.Config);
		if (p2Grab) m.P2.Equip(SpecialSlot.C, grab ?? River(), 1, null, m.Config);
		int d = gap is int g ? g * S : m.Config.BodyWidth;
		m.P1.X = -d / 2;
		m.P2.X = d - d / 2;
		m.Throw += (mm, e) => log.Throws.Add((mm.Tick, e));
		return m;
	}

	static void Run(Match m, int n, Func<int, FighterInput> p1, Func<int, FighterInput> p2)
	{
		for (int t = 1; t <= n; t++) m.Step(p1(t), p2(t));
	}

	// E12: "A throw's recovery must outlast its break window", so a window of recovery-1 is legal, and the clip must still
	// be playing when the throw lands. Grab boxes only on the LAST active frame (8): grab tick 8, window 29 -> lands tick 38
	// = the move's last frame (6+2+30 = 38). Expected: it lands (130 damage, knockdown), not "the clip ended early".
	[Test]
	public static void WindowOfRecoveryMinusOne_GrabOnTheLastActiveFrame_StillLands()
	{
		var log = new Log();
		var grab = River(d =>
		{
			Fd(d)["hitboxes"]![0]!["frames"] = JsonNode.Parse("[8, 8]");
			Fd(d)["properties"]!["throw"]!["break_window"] = 29;
		});
		var m = Setup(log, grab);
		Run(m, 60, At(1, Grab), None);
		Assert.True(log.Outcomes.SequenceEqual(new[] { ThrowOutcome.Grab, ThrowOutcome.Land }), "grab then land: " + log.Text);
		Assert.Equal(1000 - 130, m.P2.Health, "C4/E12: an unbroken throw lands with its damage");
	}

	// C4/E12: a data break window of 3: LP+LK on the last window tick (grab+3) breaks, the tick after (grab+4) lands.
	[Test]
	public static void CustomBreakWindow_EdgesFollowTheData()
	{
		foreach (var (press, expect) in new[] { (GrabTick + 3, ThrowOutcome.Break), (GrabTick + 4, ThrowOutcome.Land) })
		{
			var log = new Log();
			var m = Setup(log, River(d => Fd(d)["properties"]!["throw"]!["break_window"] = 3));
			Run(m, 40, At(1, Grab), At(press, new FighterInput(THROW)));
			Assert.True(log.Outcomes.SequenceEqual(new[] { ThrowOutcome.Grab, expect }), $"press on tick {press}: {log.Text}");
		}
	}

	// E12 + AM12: a clash breaks a grab even if it says breakable:false (the ticket text). Slippery Skin startup vs a
	// breakable:false grab, and the generic throw clashing with a breakable:false grab.
	[Test]
	public static void BreakableFalse_StillClashes_WithTheGenericThrow()
	{
		var log = new Log();
		var m = Setup(log, River(d => Fd(d)["properties"]!["throw"]!["breakable"] = false));
		Run(m, 20, At(1, Grab), At(2, new FighterInput(THROW)));
		Assert.True(log.Outcomes.SequenceEqual(new[] { ThrowOutcome.Clash }), "E12: same-frame throws clash: " + log.Text);
		Assert.True(m.P1.Health == 1000 && m.P2.Health == 1000 && m.P2.State != FighterState.Thrown, "no damage, nobody held");
	}

	// Slippery Skin is only "startup": the talisman (startup 5, FirstActive 6) started on tick 3 is on frame 5 at tick 7
	// (still startup: clash); started on tick 2 it is on frame 6 (active: the grab lands normally).
	[Test]
	public static void SlipperySkin_StartupEdge_ClashOnLastStartupFrame_NoClashFromFirstActiveFrame()
	{
		var skin = ModifierLoader.LoadFile($"{Repo}/data/modifiers/slippery-skin.json");
		foreach (var (start, first) in new[] { (3, ThrowOutcome.Clash), (2, ThrowOutcome.Grab) })
		{
			var log = new Log();
			var m = Setup(log);
			m.P2.AttachModifier(SpecialSlot.B, skin, m.Config);
			Run(m, 20, At(1, Grab), At(start, FighterInput.Special(SpecialSlot.B)));
			Assert.True(log.Throws.Count > 0 && log.Throws[0].e.Outcome == first, $"talisman from tick {start}: {log.Text}");
		}
	}

	[Test]
	public static void SlipperySkin_BreakableFalse_StillClashes()
	{
		var skin = ModifierLoader.LoadFile($"{Repo}/data/modifiers/slippery-skin.json");
		var log = new Log();
		var m = Setup(log, River(d => Fd(d)["properties"]!["throw"]!["breakable"] = false));
		m.P2.AttachModifier(SpecialSlot.B, skin, m.Config);
		Run(m, 20, At(1, Grab), At(4, FighterInput.Special(SpecialSlot.B)));
		Assert.True(log.Outcomes.SequenceEqual(new[] { ThrowOutcome.Clash }), "AM12 breaks any grab: " + log.Text);
		Assert.Equal(1000, m.P2.Health, "no damage");
	}

	// Refused press (breakable:false): the defender's LP+LK on the last window tick must leave no trace: the match goes on
	// exactly as with no press at all, through the knockdown, the wake-up and after (no buffered throw leaking out).
	[Test]
	public static void BreakableFalse_RefusedPress_LeavesNoTrace()
	{
		(int, int, int, int, int)[] Trace(bool press)
		{
			var log = new Log();
			var m = Setup(log, River(d => Fd(d)["properties"]!["throw"]!["breakable"] = false));
			var rows = new List<(int, int, int, int, int)>();
			for (int t = 1; t <= 140; t++)
			{
				m.Step(t == 1 ? Grab : Idle, press && t == GrabTick + 7 ? new FighterInput(THROW) : Idle);
				rows.Add(((int)m.P1.State, (int)m.P2.State, m.P1.X + m.P2.X * 3, m.P1.Health * 2000 + m.P2.Health, m.P1.Meter * 100 + m.P2.Meter));
			}
			return rows.ToArray();
		}
		var a = Trace(false);
		var b = Trace(true);
		for (int i = 0; i < a.Length; i++)
			Assert.True(a[i] == b[i], $"tick {i + 1}: a refused press changed the state {a[i]} vs {b[i]}");
	}

	// Roles: the grab does not care who owns it. P2 owns the grab and P1 presses the break on the edge tick.
	[Test]
	public static void SecondFighterOwnsTheGrab_MirrorsTheFirst()
	{
		foreach (var (press, expect) in new[] { (GrabTick + 7, ThrowOutcome.Break), (GrabTick + 8, ThrowOutcome.Land) })
		{
			var log = new Log();
			var m = Setup(log, p2Grab: true);
			Run(m, 40, At(press, new FighterInput(THROW)), At(1, Grab));
			Assert.True(log.Throws.Count == 2 && log.Throws.All(t => t.e.Attacker == 1) && log.Throws[1].e.Outcome == expect, $"press {press}: {log.Text}");
			if (expect == ThrowOutcome.Land) Assert.Equal(1000 - 130, m.P1.Health, "P1 takes the 130");
		}
	}

	// E12: throw vs throw on the same frame clash: two command grabs fired on the same tick.
	[Test]
	public static void TwoCommandGrabs_SameTick_Clash_NoDamage()
	{
		var log = new Log();
		var m = Setup(log, p2Grab: true);
		Run(m, 30, At(1, Grab), At(1, Grab));
		Assert.True(log.Outcomes.SequenceEqual(new[] { ThrowOutcome.Clash }), log.Text);
		Assert.True(m.P1.Health == 1000 && m.P2.Health == 1000, "no damage");
	}

	// E12: the grab that is active first wins; the one 1 tick behind is in startup, gets Thrown and never grabs back.
	[Test]
	public static void TwoCommandGrabs_OneTickApart_FirstOneLands_SecondNeverActs()
	{
		var log = new Log();
		var m = Setup(log, p2Grab: true);
		Run(m, 30, At(1, Grab), At(2, Grab));
		Assert.True(log.Throws.All(t => t.e.Attacker == 0), "only P1 ever grabs: " + log.Text);
		Assert.True(log.Outcomes.SequenceEqual(new[] { ThrowOutcome.Grab, ThrowOutcome.Land }), log.Text);
		Assert.Equal(1000, m.P1.Health, "P1 untouched");
	}

	// E24: during the wake-up the fighter "can't be thrown".
	[Test]
	public static void WakeUpDefender_CannotBeGrabbed_ButIsOnceUp()
	{
		var log = new Log();
		var m = Setup(log);
		m.P2.State = FighterState.WakeUp;
		m.P2.StunLeft = 28;
		Run(m, 10, At(1, Grab), None);
		Assert.True(!log.Outcomes.Contains(ThrowOutcome.Grab), "E24: no grab on a waking-up fighter: " + log.Text);
	}

	// C4: an airborne fighter can't be grabbed, but one that has landed (Idle) can: grab at a defender who jumped on
	// tick 1 must be a whiff; the same defender idle is grabbed (control).
	[Test]
	public static void Defender_Knocked_Down_IsNotGrabbed()
	{
		var log = new Log();
		var m = Setup(log);
		m.P2.State = FighterState.Knockdown;
		m.P2.StunLeft = 40;
		Run(m, 12, At(1, Grab), None);
		Assert.True(log.Throws.Count == 0, "E4/E24: no grab on the floor: " + log.Text);
	}

	// C4: throws can't be blocked: a crouching, guarding defender is still grabbed by a box that reaches the stance body.
	[Test]
	public static void CrouchingGuardingDefender_IsGrabbed_AndLeavesTheCrouch()
	{
		var log = new Log();
		var m = Setup(log);
		var hold = new FighterInput(InputBits.Down | InputBits.Right); // P2 faces left: back = Right, crouch block
		Run(m, 9, At(1, Grab), _ => hold);
		Assert.True(log.Outcomes.Contains(ThrowOutcome.Grab), "C4: throws beat the guard: " + log.Text);
		Assert.True(!m.P2.Guarding && !m.P2.Crouching, "grabbed: no guard, no crouch");
	}

	// Corner: the defender stands at the wall; the full on_hit pushback (80) moves the thrower back instead (the Slide
	// hands the remainder over), exactly as the generic throw does.
	[Test]
	public static void Corner_LandedGrab_PushesTheThrowerBackTheFullAmount()
	{
		var log = new Log();
		var m = Setup(log);
		m.P2.X = m.CornerHi(m.P2);
		m.P1.X = m.P2.X - m.Config.BodyWidth;
		int x0 = m.P1.X;
		Run(m, 16, At(1, Grab), None);
		Assert.True(log.Outcomes.Contains(ThrowOutcome.Land), log.Text);
		Assert.Equal(m.CornerHi(m.P2), m.P2.X, "the defender stays in the corner");
		Assert.Equal(x0 - 80 * S, m.P1.X, "the thrower is pushed back the whole 80 units");
	}

	// K3: the break press is read from the raw buffer before any scheme parser, so a Kihon and a Kata defender break
	// (or fail to) on exactly the same ticks.
	[Test]
	public static void KataAndKihonDefender_BreakOnTheSameTicks_K3()
	{
		foreach (int press in new[] { GrabTick + 1, GrabTick + 7, GrabTick + 8 })
		{
			string Outcome(ControlScheme s)
			{
				var log = new Log();
				var m = Setup(log);
				m.P2.Input.Scheme = s;
				Run(m, 30, At(1, Grab), At(press, new FighterInput(THROW)));
				return log.Text;
			}
			Assert.Equal(Outcome(ControlScheme.Kata), Outcome(ControlScheme.Kihon), $"K3: press on tick {press}");
		}
	}

	// Determinism: the same inputs twice give the same StateHash every tick; a break one tick earlier differs.
	[Test]
	public static void Deterministic_SameInputsSameHash_DifferentBreakDifferentHash()
	{
		ulong[] Hashes(int press)
		{
			var m = Setup(new Log());
			var hs = new List<ulong>();
			for (int t = 1; t <= 60; t++) { m.Step(t == 1 ? Grab : Idle, t == press ? new FighterInput(THROW) : Idle); hs.Add(m.StateHash()); }
			return hs.ToArray();
		}
		Assert.True(Hashes(GrabTick + 3).SequenceEqual(Hashes(GrabTick + 3)), "same run twice");
		Assert.True(!Hashes(GrabTick + 3).SequenceEqual(Hashes(GrabTick + 4)), "a different break tick differs");
		Assert.True(!Hashes(GrabTick + 3).SequenceEqual(Hashes(-1)), "a break differs from no break");
	}

	// Whiff: a grab with nobody in reach hands out no damage/meter/events; a whiffed move can still be cancelled by a
	// whiff cancel window (the held-grab refusal must not leak to a whiff).
	[Test]
	public static void Whiff_NoEvents_NoMeter_AndWindowStillOpensForAWhiffCancel()
	{
		var log = new Log();
		var m = Setup(log, River(d => Fd(d)["cancel_windows"] = JsonNode.Parse("[{ \"frames\": [9, 38], \"on\": [\"whiff\"] }]")), gap: 400);
		m.P1.HoldCancelRule(CancelRuleLoader.LoadFile($"{Repo}/data/cancels/fox-step.json"));
		var dash = InputParserTests.Record("6 5 6");
		bool dashed = false;
		for (int t = 1; t <= 50; t++)
		{
			m.Step(t == 1 ? Grab : t >= 9 ? dash[(t - 9) % dash.Count] : Idle, Idle);
			if (m.P1.State == FighterState.Dash) dashed = true;
		}
		Assert.True(log.Throws.Count == 0 && m.P1.Meter == 0 && m.P2.Health == 1000, "a whiff does nothing: " + log.Text);
		Assert.True(dashed, "a whiffed grab with a whiff window can be Fox-Step cancelled");
	}
}
