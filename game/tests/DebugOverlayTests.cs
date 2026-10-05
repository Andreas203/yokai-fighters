using System;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-22: the overlay's boxes are the sim's data for the current frame (checked against the YOK-16
/// TEST FIXTURE moves, both facings), and frame-step runs exactly one tick per press through the clock.
/// </summary>
public static class DebugOverlayTests
{
	static readonly FighterInput Idle = FighterInput.None;
	static MoveData[] Fixtures() => MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath("res://tests/fixtures/moves"));

	/// <summary>Expected world rect of a data box, computed by hand from the rules (x toward the opponent, mirrored by facing).</summary>
	static (int, int, int, int) Expect(Fighter f, Box b)
	{
		int s = SimConfig.Scale;
		int x0 = f.Facing > 0 ? f.X + b.X * s : f.X - (b.X + b.W) * s;
		return (x0, f.Y + b.Y * s, x0 + b.W * s, f.Y + (b.Y + b.H) * s);
	}

	static void CheckMoveBoxes(string id, int who)
	{
		var moves = Fixtures();
		int slot = Array.FindIndex(moves, m => m.Id == id);
		MoveData mv = moves[slot];
		var m = new Match(null, moves, moves); // fighters start apart: nothing connects
		for (int t = 1; t <= mv.TotalFrames; t++)
		{
			var req = t == 1 ? FighterInput.Attack(slot) : Idle;
			m.Step(who == 0 ? req : Idle, who == 0 ? Idle : req);
			Fighter f = m.Fighters[who];
			Assert.Equal(t, f.MoveFrame, $"{id} P{who + 1} tick {t}: move frame");
			var boxes = DebugBoxes.For(m, who);

			var wantHit = mv.Hitboxes.Where(h => h.Covers(t) && mv.IsActive(t)).Select(h => Expect(f, h.Box)).ToList();
			var wantHurt = mv.Hurtboxes.Where(h => h.Covers(t)).Select(h => Expect(f, h.Box)).ToList();
			var gotHit = boxes.Where(b => b.Kind == DebugBoxKind.Hit).Select(b => (b.X0, b.Y0, b.X1, b.Y1)).ToList();
			var gotHurt = boxes.Where(b => b.Kind == DebugBoxKind.Hurt).Select(b => (b.X0, b.Y0, b.X1, b.Y1)).ToList();
			Assert.True(wantHit.SequenceEqual(gotHit), $"{id} P{who + 1} frame {t}: hitboxes {string.Join(",", gotHit)} vs data {string.Join(",", wantHit)}");
			Assert.True(wantHurt.SequenceEqual(gotHurt), $"{id} P{who + 1} frame {t}: hurtboxes {string.Join(",", gotHurt)} vs data {string.Join(",", wantHurt)}");
			Assert.Equal(1, boxes.Count(b => b.Kind == DebugBoxKind.Push), $"{id} frame {t}: one push box");
			Assert.Equal(0, boxes.Count(b => b.Kind == DebugBoxKind.Throw), $"{id} frame {t}: no throw data yet (YOK-19)");
		}
		m.Step(Idle, Idle);
		var idle = DebugBoxes.For(m, who).Where(b => b.Kind == DebugBoxKind.Hurt).Select(b => (b.X0, b.Y0, b.X1, b.Y1)).ToList();
		Assert.True(idle.SequenceEqual(new[] { Expect(m.Fighters[who], m.Config.IdleHurtbox) }), $"{id}: idle hurtbox after the move");
	}

	[Test]
	public static void Boxes_MatchFixtureDataEveryFrame_BothFacings()
	{
		foreach (var mv in Fixtures())
			for (int who = 0; who < 2; who++)
				CheckMoveBoxes(mv.Id, who);
	}

	[Test]
	public static void Boxes_MirrorByFacing_HandChecked()
	{
		// test-jab hitbox on frame 5: x 30, y 100, w 90, h 30 (units). P2 faces left.
		var moves = Fixtures();
		int slot = Array.FindIndex(moves, m => m.Id == "test-jab");
		var m = new Match(null, moves, moves);
		for (int t = 1; t <= 5; t++) m.Step(t == 1 ? FighterInput.Attack(slot) : Idle, t == 1 ? FighterInput.Attack(slot) : Idle);
		var p1 = DebugBoxes.For(m, 0).Single(b => b.Kind == DebugBoxKind.Hit);
		var p2 = DebugBoxes.For(m, 1).Single(b => b.Kind == DebugBoxKind.Hit);
		Assert.Equal(1, m.P1.Facing, "P1 faces right");
		Assert.Equal(-1, m.P2.Facing, "P2 faces left");
		Assert.Equal(m.P1.X + 3000, p1.X0, "P1 hitbox starts 30 units in front");
		Assert.Equal(m.P1.X + 12000, p1.X1, "P1 hitbox ends 120 units in front");
		Assert.Equal(m.P2.X - 12000, p2.X0, "P2 hitbox mirrored: starts 120 units to its left");
		Assert.Equal(m.P2.X - 3000, p2.X1, "P2 hitbox mirrored: ends 30 units to its left");
		Assert.Equal(10000, p1.Y0, "hitbox bottom at 100 units");
		Assert.True(DebugBoxes.Label(m.P1).Contains($"Attack test-jab (slot {slot + 1}) 5/13 active  stun 0"),
			"label shows move, frame/total and phase: " + DebugBoxes.Label(m.P1));
	}

	[Test]
	public static void Boxes_NoneHittableWhileKnockedDown()
	{
		var moves = Fixtures();
		var m = new Match(null, moves, moves);
		m.P2.State = FighterState.Knockdown;
		m.P2.StunLeft = 10;
		Assert.Equal(0, DebugBoxes.For(m, 1).Count(b => b.Kind == DebugBoxKind.Hurt), "knocked-down fighter shows no hurtbox (sim can't hit it)");
		Assert.True(DebugBoxes.Label(m.P2).Contains("Knockdown  stun 10"), "label shows state and stun left");
	}

	[Test]
	public static void FrameStep_OneTickPerPressThroughClock()
	{
		var clock = new FixedStepClock();
		var stepper = new FrameStepper();
		var m = new Match();
		long now = 0;
		int Frame(long usec) { now += usec; int n = stepper.Filter(clock.Advance(now)); for (int i = 0; i < n; i++) m.Step(Idle, Idle); return n; }

		Frame(0);
		Assert.Equal(60, Enumerable.Range(0, 60).Sum(_ => Frame(16_667)), "running: 60 ticks per second");
		stepper.TogglePause();
		Assert.Equal(0, Enumerable.Range(0, 120).Sum(_ => Frame(16_667)), "paused: no ticks for two seconds");
		int tick = m.Tick;
		for (int press = 1; press <= 5; press++)
		{
			stepper.RequestStep();
			Assert.Equal(1, Frame(16_667), $"press {press}: exactly one tick");
			Assert.Equal(0, Frame(16_667), $"press {press}: nothing on the following frame");
			Assert.Equal(tick + press, m.Tick, $"press {press}: sim tick");
		}
		stepper.RequestStep(); stepper.RequestStep();
		Assert.Equal(2, Frame(1), "two presses in one frame = two ticks");
		stepper.TogglePause();
		Assert.Equal(1, Frame(16_667), "resume: back to real time, no catch-up burst");
	}

	[Test]
	public static void FrameStep_RequestWhileRunningPausesWithoutStepping()
	{
		var s = new FrameStepper();
		s.RequestStep();
		Assert.True(s.Paused, "F3 while running pauses");
		Assert.Equal(0, s.Filter(5), "and runs nothing");
	}

	[Test]
	public static void Scene_OverlayKeysAndFrameStep(Node runner)
	{
		var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		runner.AddChild(scene); // live clock path (ExternalDrive off)
		var overlay = scene.GetNodeOrNull<DebugOverlay>("DebugOverlay");
		Assert.True(OS.IsDebugBuild() == (overlay != null), "overlay exists only in debug builds");
		if (overlay == null) { scene.QueueFree(); return; }
		Assert.True(!overlay.ShowBoxes, "overlay starts hidden");
		overlay.HandleKey(DebugOverlay.ToggleKey);
		Assert.True(overlay.ShowBoxes, "F1 shows boxes");
		overlay.HandleKey(DebugOverlay.PauseKey);
		int tick = scene.Match.Tick;
		for (int i = 0; i < 10; i++) scene._Process(0.1);
		Assert.Equal(tick, scene.Match.Tick, "paused scene runs no ticks");
		for (int press = 1; press <= 3; press++)
		{
			overlay.HandleKey(DebugOverlay.StepKey);
			scene._Process(0.1);
			scene._Process(0.1);
			Assert.Equal(tick + press, scene.Match.Tick, $"F3 press {press} = one tick");
		}
		overlay.HandleKey(DebugOverlay.ToggleKey);
		Assert.True(!overlay.ShowBoxes, "F1 hides boxes");
		scene.QueueFree();
	}
}
