using System;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>YOK-53: clips + models are presentation only, stepped by exact sim frames (F3).</summary>
public static class ModelPresentationTests
{
	static ClipCatalog Cat => FightScene.Catalog;

	static FighterPresenter Presenter(int i) =>
		new(i == 0 ? FighterAnimSet.Ryo : FighterAnimSet.Kitsune, Cat, FightScene.MoveClips);

	static int Slot(Fighter f, string id)
	{
		int s = Array.FindIndex(f.Moves, m => m.Id == id);
		Assert.True(s >= 0, $"{id} is in the kit");
		return s;
	}

	[Test]
	public static void Catalog_EveryClipParsesWithAGlbAndAgreesWithFramesTotal()
	{
		Assert.Equal(0, Cat.Problems.Count, "clip problems: " + string.Join("; ", Cat.Problems));
		Assert.True(Cat.Clips.Count >= 40, $"all clip files load ({Cat.Clips.Count})");
		foreach (var c in Cat.Clips.Values)
		{
			Assert.True(ResourceLoader.Exists(c.ResPath), $"{c.Id}: {c.ResPath} exists");
			Assert.True(Math.Abs(c.ComputedFrames - c.FramesTotal) <= 1,
				$"{c.Id}: trim {c.TrimStart}-{c.TrimEnd} at {c.Speed}x gives {c.ComputedFrames} frames, file says {c.FramesTotal}");
		}
	}

	[Test]
	public static void Catalog_TrimParsing_RetimedPassWins()
	{
		var t = ClipCatalog.ParseTrim("Raw trim: ticks 30-66 at 1.5x (60 ticks per second). RE-TIMED (supersedes the trim above; was 25 frames, hit 5-6): trim raw ticks 30-48 at 1.5x = 13 frames.");
		Assert.True(t == (30, 48, 1.5), $"retimed trim wins: {t}");
		Assert.True(ClipCatalog.ParseTrim("trim raw 60 fps ticks 14-50 inclusive (trim is data, no hand keying).") == (14, 50, 1.0), "no speed = 1x");
		Assert.True(ClipCatalog.ParseTrim("Trim raw 32-76 (E10: no pre-jump frames), 0.85x: 53 frames") == (32, 76, 0.85), "speed after a parenthesis");
		var lp = Cat["ryo-light-punch"]!;
		Assert.True(Math.Abs(lp.TimeAt(1) - 30 / 60.0) < 1e-9 && Math.Abs(lp.TimeAt(5) - 36 / 60.0) < 1e-9, "frame n = raw trim start + (n-1) x speed");
		Assert.True(Math.Abs(lp.TimeAt(99) - 48 / 60.0) < 1e-9, "clamped to the trim end");
	}

	/// <summary>Migration check (structured clip fields): every clip file carries trim/speed/file, and the catalog
	/// built from them maps every frame exactly as the old notes parse did. If a later clip pass changes the fields
	/// on purpose without rewriting the notes, relax this to the fields alone.</summary>
	[Test]
	public static void Catalog_StructuredFieldsGiveTheSameFrameMappingAsTheNotes()
	{
		string clipsDir = System.IO.Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "../data/clips");
		var fields = ClipCatalog.Load(clipsDir, ProjectSettings.GlobalizePath("res://"));
		var notes = ClipCatalog.Load(clipsDir, ProjectSettings.GlobalizePath("res://"), notesOnly: true);
		foreach (string path in System.IO.Directory.GetFiles(clipsDir, "*.json"))
		{
			string text = System.IO.File.ReadAllText(path);
			foreach (string key in new[] { "\"trim_start\"", "\"trim_end\"", "\"speed_scale\"", "\"file\"" })
				Assert.True(text.Contains(key), $"{System.IO.Path.GetFileName(path)} has {key}");
		}
		Assert.Equal(notes.Clips.Count, fields.Clips.Count, "same clips");
		foreach (var n in notes.Clips.Values)
		{
			var f = fields[n.Id];
			Assert.True(f != null, $"{n.Id} loads from fields");
			Assert.Equal(n.ResPath, f!.ResPath, $"{n.Id}: same GLB");
			Assert.Equal((n.TrimStart, n.TrimEnd, n.Speed, n.FramesTotal), (f.TrimStart, f.TrimEnd, f.Speed, f.FramesTotal), $"{n.Id}: same trim/speed");
			for (int fr = 0; fr <= n.FramesTotal + 1; fr++)
				Assert.Equal(n.TimeAt(fr), f.TimeAt(fr), $"{n.Id}: frame {fr} shows the same time");
		}
	}

	[Test]
	public static void Catalog_FieldsWinOverNotes_NotesFillWhatIsMissing()
	{
		string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "yf-clip-fields-" + System.Environment.ProcessId);
		string clips = System.IO.Path.Combine(dir, "clips"), game = System.IO.Path.Combine(dir, "game");
		System.IO.Directory.CreateDirectory(clips);
		System.IO.Directory.CreateDirectory(System.IO.Path.Combine(game, "assets/generated/clips/ryo"));
		System.IO.File.WriteAllText(System.IO.Path.Combine(game, "assets/generated/clips/ryo/a.glb"), "");
		System.IO.File.WriteAllText(System.IO.Path.Combine(game, "assets/generated/clips/ryo/b.glb"), "");
		const string notes = "file b.glb. Trim raw 10-40 at 2x.";
		void Clip(string id, string extra) => System.IO.File.WriteAllText(System.IO.Path.Combine(clips, id + ".json"),
			$"{{\"kind\":\"clip\",\"id\":\"{id}\",\"frames_total\":16{extra},\"notes\":\"{notes}\"}}");
		try
		{
			Clip("ryo-fields", ",\"trim_start\":5,\"trim_end\":20,\"speed_scale\":1.5,\"file\":\"assets/generated/clips/ryo/a.glb\",\"yaw_offset\":-45");
			Clip("ryo-notes", "");
			Clip("ryo-trim-only", ",\"trim_start\":0,\"trim_end\":15");
			Clip("ryo-missing-file", ",\"file\":\"assets/generated/clips/ryo/none.glb\"");
			var cat = ClipCatalog.Load(clips, game);
			var a = cat["ryo-fields"]!;
			Assert.Equal(("res://assets/generated/clips/ryo/a.glb", 5, 20, 1.5, -45.0), (a.ResPath, a.TrimStart, a.TrimEnd, a.Speed, a.YawOffsetDeg), "fields win");
			var b = cat["ryo-notes"]!;
			Assert.Equal(("res://assets/generated/clips/ryo/b.glb", 10, 40, 2.0, 0.0), (b.ResPath, b.TrimStart, b.TrimEnd, b.Speed, b.YawOffsetDeg), "notes fallback");
			var c = cat["ryo-trim-only"]!;
			Assert.Equal((0, 15, 1.0, "res://assets/generated/clips/ryo/b.glb"), (c.TrimStart, c.TrimEnd, c.Speed, c.ResPath), "trim fields without speed_scale = 1x; file from notes");
			Assert.True(!cat.Has("ryo-missing-file") && cat.Problems.Exists(p => p.Contains("ryo-missing-file") && p.Contains("none.glb")), "a missing field file is a problem, not a silent fallback");
		}
		finally { System.IO.Directory.Delete(dir, true); }
	}

	[Test]
	public static void AnimSets_EveryStateAndEveryMoveHasAClip()
	{
		foreach (var set in new[] { FighterAnimSet.Ryo, FighterAnimSet.Kitsune })
			foreach (PoseKind k in Enum.GetValues<PoseKind>())
				Assert.True(Cat.Has(set.ClipFor(k)), $"{set.Fighter} {k}: clip {set.ClipFor(k)} is catalogued");
		foreach (var (move, clip) in FightScene.MoveClips)
			Assert.True(Cat.Has(clip), $"move {move}: clip {clip} is catalogued");
		Assert.Equal("ryo-foxfire-cast", FighterAnimSet.Ryo.MoveClip("kitsune-foxfire"), "Ryo casts Foxfire with his own take (YOK-48 rematch)");
	}

	[Test]
	public static void Presenter_AttackShowsTheMoveFrameEveryTick()
	{
		var m = FightScene.NewMatch();
		var p = Presenter(0);
		p.Observe(m, 0);
		int jab = Slot(m.P1, "ryo-light-punch");
		m.Step(FighterInput.Attack(jab), FighterInput.None);
		for (int t = 0; t < 30 && m.P1.State == FighterState.Attack; t++)
		{
			p.Observe(m, 0);
			Assert.Equal("ryo-light-punch", p.Sample.Clip, $"tick {t}: jab clip");
			Assert.Equal(m.P1.MoveFrame, p.Sample.Frame, $"tick {t}: clip frame = MoveFrame");
			if (m.P1.MoveFrame >= 4) Assert.True(!p.Sample.Blending, $"frame {m.P1.MoveFrame}: blend-in done by the hit frame (startup 4)");
			m.Step(FighterInput.None, FighterInput.None);
		}
		p.Observe(m, 0);
		Assert.Equal(PoseKind.Idle, p.Sample.Kind, "back to idle");
		Assert.True(p.Sample.Blending && p.Sample.FromClip == "ryo-light-punch", "blend-out from the jab's last frame");
	}

	[Test]
	public static void Presenter_HitstopFreezesThePoseAndFrameStepAdvancesOne()
	{
		var m = FightScene.NewMatch();
		var p1 = Presenter(0); var p2 = Presenter(1);
		int hk = Slot(m.P1, "ryo-heavy-punch");
		for (int t = 0; t < 200 && m.P2.X - m.P1.X > 15000; t++) { m.Step(new FighterInput(InputBits.Right), FighterInput.None); p1.Observe(m, 0); p2.Observe(m, 1); }
		m.Step(FighterInput.Attack(hk), FighterInput.None);
		p1.Observe(m, 0); p2.Observe(m, 1);
		bool sawHitstop = false;
		for (int t = 0; t < 60; t++)
		{
			var before = p1.Sample; int world = m.WorldFrame;
			m.Step(FighterInput.None, FighterInput.None);
			p1.Observe(m, 0); p2.Observe(m, 1);
			if (m.WorldFrame == world) { sawHitstop = true; Assert.Equal(before, p1.Sample, $"tick {t}: hitstop holds the pose"); }
			else if (before.Kind == PoseKind.Attack && p1.Sample.Kind == PoseKind.Attack)
				Assert.Equal(before.Frame + 1, p1.Sample.Frame, $"tick {t}: one world frame = one clip frame");
		}
		Assert.True(sawHitstop, "the heavy punch connected (hitstop seen)");
	}

	[Test]
	public static void Presenter_WalkLoopsOnTheTickAndIdleFollowsTheWorldFrame()
	{
		var m = FightScene.NewMatch();
		var p = Presenter(0);
		p.Observe(m, 0);
		Assert.Equal(PoseKind.Idle, p.Sample.Kind, "starts idle");
		Assert.Equal(m.WorldFrame % Cat["ryo-idle-guard"]!.FramesTotal + 1, p.Sample.Frame, "idle frame from the world frame");
		int total = Cat["ryo-walk-guard-fwd"]!.FramesTotal;
		for (int t = 1; t <= total + 5; t++)
		{
			m.Step(new FighterInput(InputBits.Right), FighterInput.None);
			p.Observe(m, 0);
			if (m.P2.X - m.P1.X < 20000) break;
			Assert.Equal(PoseKind.WalkFwd, p.Sample.Kind, $"tick {t}: walking forward");
			Assert.Equal((t - 1) % total + 1, p.Sample.Frame, $"tick {t}: walk loop frame");
		}
	}

	[Test]
	public static void Presenter_NewMatchInstanceStartsFresh()
	{
		var p = Presenter(0);
		var a = FightScene.NewMatch();
		for (int t = 0; t < 10; t++) { a.Step(new FighterInput(InputBits.Right), FighterInput.None); p.Observe(a, 0); }
		var b = FightScene.NewMatch(); // YOK-48: a rematch is a new Match
		p.Observe(b, 0);
		Assert.Equal(PoseKind.Idle, p.Sample.Kind, "fresh match: idle");
		Assert.True(!p.Sample.Blending, "no blend carried over from the old match");
	}

	[Test]
	public static void Scene_ModelsSeekExactFramesAndSimIsUntouched(Node runner)
	{
		var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		scene.ExternalDrive = true;
		runner.AddChild(scene);
		var bare = FightScene.NewMatch();
		var ryo = scene.ModelOf(0); var kit = scene.ModelOf(1);
		Assert.True(ryo != null && kit != null, "both rigged models load");
		Assert.True(ryo!.Player.CallbackModeProcess == AnimationMixer.AnimationCallbackModeProcess.Manual, "the AnimationPlayer never advances on its own");
		int jab = Slot(scene.Match.P1, "ryo-light-punch");
		var inputs = Enumerable.Range(0, 12).Select(_ => new FighterInput(InputBits.Right))
			.Append(FighterInput.Attack(jab)).Concat(Enumerable.Repeat(FighterInput.None, 20)).ToArray();
		foreach (var p1 in inputs)
		{
			var p2 = new FighterInput(InputBits.Left);
			scene.Step(p1, p2);
			bare.Step(p1, p2);
			Assert.Equal(bare.StateHash(), scene.Match.StateHash(), $"tick {bare.Tick}: presentation doesn't change the sim");
			var s = scene.PresenterOf(0)!.Sample;
			if (s.Blending) continue;
			var (name, time) = ryo.Playhead;
			Assert.Equal(FighterModel.LibraryName + "/" + s.Clip, name.ToString(), $"tick {bare.Tick}: playing the sample's clip");
			Assert.True(Math.Abs(time - Cat[s.Clip]!.TimeAt(s.Frame)) < 1e-4, $"tick {bare.Tick}: seek {time} = frame {s.Frame} time {Cat[s.Clip]!.TimeAt(s.Frame)}");
		}
		var tails = kit!.FindChild("Tails", true, false)!.GetChildren();
		Assert.Equal(FighterModel.TailCount, tails.Count, "nine tails on the Kitsune");
		scene.QueueFree();
	}

	[Test]
	public static void Scene_FrameStepAdvancesTheAnimationOneTick(Node runner)
	{
		var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		runner.AddChild(scene); // live clock path
		var overlay = scene.GetNodeOrNull<DebugOverlay>("DebugOverlay");
		if (overlay == null) { scene.QueueFree(); return; }
		overlay.HandleKey(DebugOverlay.PauseKey);
		var model = scene.ModelOf(0)!;
		scene._Process(0.1);
		var (_, t0) = model.Playhead;
		int w0 = scene.Match.WorldFrame;
		for (int i = 0; i < 5; i++) scene._Process(0.1);
		Assert.True(Math.Abs(model.Playhead.Time - t0) < 1e-9, "paused: the animation holds");
		overlay.HandleKey(DebugOverlay.StepKey);
		scene._Process(0.1);
		Assert.Equal(w0 + 1, scene.Match.WorldFrame, "F3: one tick");
		var s = scene.PresenterOf(0)!.Sample;
		Assert.Equal(PoseKind.Idle, s.Kind, "still idle");
		var idle = Cat["ryo-idle-guard"]!;
		Assert.True(Math.Abs(model.Playhead.Time - t0 - idle.Speed / 60.0) < 1e-6, $"F3: the animation moved exactly one frame ({t0} -> {model.Playhead.Time})");
		scene.QueueFree();
	}
}
