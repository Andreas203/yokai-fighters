using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Godot;
using YokaiFighters.Fight;
using Environment = System.Environment;

namespace YokaiFighters.Tests;

/// <summary>YOK-113: clip retiming curves (F2, AM5) map game frame → raw clip tick, deterministically (F3).</summary>
public static class ClipRetimeTests
{
	// trim 22-53 over 33 frames: startup snaps (2x), hit pose holds (4 frames), recovery stretched (0.5x), then 1x.
	static readonly RetimeSegment[] Curve =
	{
		new(1, 5, 22, 30), new(5, 8, 30, 30), new(8, 12, 30, 32), new(12, 33, 32, 53),
	};

	static ClipInfo Retimed() => new("ryo-test", "res://x.glb", 22, 53, 1.0, 33) { Retime = Curve };

	[Test]
	public static void Retime_CompressedSegmentAdvancesTwoTicksPerFrame()
	{
		var c = Retimed();
		for (int f = 1; f <= 5; f++)
			Assert.Equal(22.0 + 2 * (f - 1), c.RawTickAt(f), $"frame {f} (2x)");
		Assert.Equal(26.0 / 60.0, c.TimeAt(3), "TimeAt = tick / 60");
	}

	[Test]
	public static void Retime_HoldShowsTheSameTickOnItsFrames()
	{
		var c = Retimed();
		for (int f = 5; f <= 8; f++) Assert.Equal(30.0, c.RawTickAt(f), $"frame {f} holds tick 30");
		Assert.True(c.RawTickAt(4) < 30.0 && c.RawTickAt(9) > 30.0, "the hold is exactly frames 5-8");
	}

	[Test]
	public static void Retime_StretchedSegmentAdvancesHalfATickPerFrame()
	{
		var c = Retimed();
		Assert.Equal(30.5, c.RawTickAt(9), "frame 9 (0.5x)");
		Assert.Equal(31.0, c.RawTickAt(10), "frame 10");
		Assert.Equal(31.5, c.RawTickAt(11), "frame 11");
		Assert.Equal(32.0, c.RawTickAt(12), "frame 12 = boundary");
		Assert.Equal(53.0, c.RawTickAt(33), "last frame = last tick");
		Assert.Equal(33, c.ComputedFrames, "the curve sets the frame count");
	}

	[Test]
	public static void Retime_SameFrameSameTick_AndClamped()
	{
		var a = Retimed();
		var b = Retimed();
		for (int f = -3; f <= 40; f++)
		{
			Assert.Equal(a.RawTickAt(f), b.RawTickAt(f), $"frame {f} reproducible");
			Assert.Equal(a.RawTickAt(f), a.RawTickAt(f), $"frame {f} stable");
		}
		Assert.Equal(22.0, a.RawTickAt(0), "below frame 1 clamps to the first tick");
		Assert.Equal(53.0, a.RawTickAt(99), "past frames_total clamps to the last tick");
		// Fractional ticks come from one exact division: 1 tick over 3 frames.
		var third = new ClipInfo("t", "res://x.glb", 0, 10, 1.0, 4) { Retime = new RetimeSegment[] { new(1, 4, 0, 1) } };
		Assert.Equal(1.0 / 3.0, third.RawTickAt(2), "tick = ta + (tb-ta)(f-a)/(b-a)");
	}

	[Test]
	public static void Retime_NoCurveKeepsTrimAndSpeed()
	{
		var plain = new ClipInfo("p", "res://x.glb", 30, 48, 1.5, 13);
		Assert.True(plain.Retime == null, "no curve by default");
		for (int f = -1; f <= 15; f++)
		{
			int n = Math.Clamp(f, 1, 13);
			Assert.Equal(Math.Min(30 + (n - 1) * 1.5, 48), plain.RawTickAt(f), $"frame {f}: trim + (n-1) x speed");
		}
		Assert.Equal(13, plain.ComputedFrames, "trim/speed frame count");
	}

	[Test]
	public static void Retime_CheckRejectsMalformedCurves()
	{
		static string? Check(int total, params RetimeSegment[] s) => ClipCatalog.CheckRetime(s, total);
		static RetimeSegment S(int a, int b, int ta, int tb) => new(a, b, ta, tb);
		Assert.True(Check(33, Curve) == null, "the good curve passes");
		Assert.True(Check(33) != null, "empty");
		Assert.True(Check(33, S(2, 33, 22, 53))?.Contains("frame 1") == true, "must start on frame 1");
		Assert.True(Check(33, S(1, 30, 22, 51))?.Contains("frames_total") == true, "must cover frames_total");
		Assert.True(Check(33, S(1, 5, 22, 30), S(6, 33, 30, 57))?.Contains("contiguous") == true, "frame gap");
		Assert.True(Check(33, S(1, 5, 22, 30), S(5, 33, 31, 58))?.Contains("contiguous") == true, "tick jump");
		Assert.True(Check(33, S(1, 5, 22, 40), S(5, 33, 40, 68))?.Contains("outside 0.5x-2x") == true, "4.5x too fast");
		Assert.True(Check(33, S(1, 5, 22, 23), S(5, 33, 23, 51))?.Contains("outside 0.5x-2x") == true, "0.25x too slow");
		Assert.True(Check(33, S(1, 5, 30, 22), S(5, 33, 22, 50))?.Contains("outside 0.5x-2x") == true, "backwards");
		Assert.True(Check(33, S(1, 5, 22, 30), S(5, 9, 30, 30), S(9, 33, 30, 54))?.Contains("5 frames") == true, "hold of 5 frames");
		Assert.True(Check(33, S(1, 5, 22, 30), S(5, 7, 30, 30), S(7, 9, 30, 30), S(9, 33, 30, 54))?.Contains("5 frames") == true,
			"split hold still counts as one");
		Assert.True(Check(33, S(1, 1, 22, 22), S(1, 33, 22, 54))?.Contains("after first") == true, "zero-length segment");
		Assert.True(ClipCatalog.CheckRetime(new[] { S(1, 5, 5, 9) }, 5, (10, 20))?.Contains("outside trim 10-20") == true, "ticks below the trim");
		Assert.True(ClipCatalog.CheckRetime(new[] { S(1, 5, 18, 22) }, 5, (10, 20))?.Contains("outside trim 10-20") == true, "ticks past the trim");
		Assert.True(ClipCatalog.CheckRetime(new[] { S(1, 5, 10, 14) }, 5, (10, 20)) == null && Check(5, S(1, 5, 5, 9)) == null, "inside the trim, or no trim, passes");
		Assert.True(Check(33, S(1, 2, 0, 2), S(2, 33, 2, 33)) == null && Check(2, S(1, 2, 0, 1)) == null, "exactly 2x and 0.5x pass");
	}

	[Test]
	public static void Retime_CatalogLoadsCurvesAndRejectsMalformedOnes()
	{
		string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "yf-clip-retime-" + Environment.ProcessId);
		string clips = System.IO.Path.Combine(dir, "clips"), game = System.IO.Path.Combine(dir, "game");
		System.IO.Directory.CreateDirectory(clips);
		System.IO.Directory.CreateDirectory(System.IO.Path.Combine(game, "assets/generated/clips/ryo"));
		System.IO.File.WriteAllText(System.IO.Path.Combine(game, "assets/generated/clips/ryo/a.glb"), "");
		void Clip(string id, string extra) => System.IO.File.WriteAllText(System.IO.Path.Combine(clips, id + ".json"),
			$"{{\"kind\":\"clip\",\"id\":\"{id}\",\"frames_total\":33,\"file\":\"assets/generated/clips/ryo/a.glb\"{extra}}}");
		const string good = "[{\"frames\":[1,5],\"ticks\":[22,30],\"note\":\"snap\"},{\"frames\":[5,8],\"ticks\":[30,30]},{\"frames\":[8,12],\"ticks\":[30,32]},{\"frames\":[12,33],\"ticks\":[32,53]}]";
		try
		{
			Clip("ryo-curve", ",\"trim_start\":22,\"trim_end\":53,\"speed_scale\":1,\"retime\":" + good);
			Clip("ryo-curve-no-trim", ",\"retime\":" + good);
			Clip("ryo-plain", ",\"trim_start\":30,\"trim_end\":78,\"speed_scale\":1.5");
			Clip("ryo-bad-gap", ",\"trim_start\":22,\"trim_end\":53,\"retime\":[{\"frames\":[1,5],\"ticks\":[22,30]},{\"frames\":[6,33],\"ticks\":[30,57]}]");
			Clip("ryo-bad-shape", ",\"trim_start\":22,\"trim_end\":53,\"retime\":[{\"frames\":[1],\"ticks\":[22,54]}]");
			Clip("ryo-bad-float", ",\"trim_start\":22,\"trim_end\":53,\"retime\":[{\"frames\":[1,33],\"ticks\":[22.5,54]}]");
			Clip("ryo-bad-empty", ",\"trim_start\":22,\"trim_end\":53,\"retime\":[]");
			Clip("ryo-bad-trim", ",\"trim_start\":30,\"trim_end\":53,\"retime\":" + good);
			var cat = ClipCatalog.Load(clips, game);
			var a = cat["ryo-curve"]!;
			Assert.True(a.Retime != null && a.Retime.Count == 4, "curve read");
			for (int f = 1; f <= 33; f++) Assert.Equal(Retimed().RawTickAt(f), a.RawTickAt(f), $"frame {f} through the loaded curve");
			var b = cat["ryo-curve-no-trim"]!;
			Assert.Equal((22, 53), (b.TrimStart, b.TrimEnd), "no trim fields: the curve's first/last tick is the trim");
			Assert.Equal(30.0, b.RawTickAt(7), "hold through the curve");
			var p = cat["ryo-plain"]!;
			Assert.True(p.Retime == null && p.RawTickAt(5) == 36.0, "no curve: trim + speed as before");
			foreach (string id in new[] { "ryo-bad-gap", "ryo-bad-shape", "ryo-bad-float", "ryo-bad-empty", "ryo-bad-trim" })
				Assert.True(!cat.Has(id) && cat.Problems.Exists(x => x.StartsWith(id + ": retime")), $"{id} rejected with a problem");
			var notesOnly = ClipCatalog.Load(clips, game, notesOnly: true);
			Assert.True(!notesOnly.Has("ryo-curve"), "notesOnly ignores structured fields, the curve included");
		}
		finally { System.IO.Directory.Delete(dir, true); }
	}

	// ---- YOK-164: the curve engine end to end: the gates agree, and both Quake timings play through Seek() ----

	static string Repo => Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "..");

	/// <summary>
	/// The shared case list both gates run (tools/tests/test_validate_data.py runs the same file through the clip schema and
	/// validate_data.semantic): the loader takes a curve exactly when the validator does.
	/// </summary>
	[Test]
	public static void ValidatorAndLoaderAgreeOnTheSharedCases()
	{
		using var doc = JsonDocument.Parse(File.ReadAllText($"{Repo}/game/tests/fixtures/clip-retime-gate/cases.json"));
		var cases = doc.RootElement.GetProperty("cases");
		Assert.True(cases.GetArrayLength() >= 30, "the case list is there");
		string dir = Path.Combine(Path.GetTempPath(), "yf-retime-gate-" + Environment.ProcessId);
		Directory.CreateDirectory(dir);
		try
		{
			foreach (var c in cases.EnumerateArray())
			{
				string name = c.GetProperty("name").GetString()!;
				string trim = c.GetProperty("trim") is { ValueKind: JsonValueKind.Array } t ? $",\"trim_start\":{t[0]},\"trim_end\":{t[1]}" : "";
				File.WriteAllText(Path.Combine(dir, name + ".json"),
					$"{{\"kind\":\"clip\",\"id\":\"{name}\",\"frames_total\":{c.GetProperty("frames_total")},\"file\":\"assets/generated/clips/ryo/a.glb\"{trim},\"retime\":{c.GetProperty("retime").GetRawText()}}}");
			}
			var cat = ClipCatalog.Load(dir, _ => true);
			var wrong = new List<string>();
			foreach (var c in cases.EnumerateArray())
			{
				string name = c.GetProperty("name").GetString()!;
				if (cat.Has(name) != c.GetProperty("accept").GetBoolean())
					wrong.Add($"{name}: loader {(cat.Has(name) ? "accepted" : "refused: " + cat.Problems.Find(x => x.StartsWith(name + ":")))}");
			}
			Assert.True(wrong.Count == 0, "validator and loader disagree:\n" + string.Join("\n", wrong));
		}
		finally { Directory.Delete(dir, true); }
	}

	/// <summary>
	/// The rules table's Quake timing, 22/5/26, as a TEST FIXTURE curve over the same take and trim as
	/// data/clips/oni-quake.json (made up here to show the curve engine plays it within F2's limits; YOK-137 leaves the
	/// choice to the designer, and the real curve, if chosen, is the animator's).
	/// </summary>
	static ClipCatalog QuakeTableTimingFixture(ClipInfo quake)
	{
		// 22/5/26 = 53 frames: startup snaps to the overhead peak (2x), holds 4 frames (the tell), the slam falls
		// at 16/11x, 5 active frames at 1x, recovery eases out at 16/26x.
		const string curve = "[{\"frames\":[1,9],\"ticks\":[0,16]},{\"frames\":[9,12],\"ticks\":[16,16]},{\"frames\":[12,23],\"ticks\":[16,32]},"
			+ "{\"frames\":[23,27],\"ticks\":[32,36]},{\"frames\":[27,53],\"ticks\":[36,52]}]";
		string dir = Path.Combine(Path.GetTempPath(), "yf-quake-table-" + Environment.ProcessId);
		Directory.CreateDirectory(dir);
		try
		{
			string file = quake.ResPath["res://".Length..];
			File.WriteAllText(Path.Combine(dir, "oni-quake.json"),
				$"{{\"kind\":\"clip\",\"id\":\"oni-quake\",\"frames_total\":53,\"trim_start\":{quake.TrimStart},\"trim_end\":{quake.TrimEnd},\"file\":\"{file}\",\"retime\":{curve}}}");
			return ClipCatalog.Load(dir, _ => true);
		}
		finally { Directory.Delete(dir, true); }
	}

	static (int Start, int End) HitWindowOf(string clipId)
	{
		using var doc = JsonDocument.Parse(File.ReadAllText($"{Repo}/data/clips/{clipId}.json"));
		return (doc.RootElement.GetProperty("hit_start").GetInt32(), doc.RootElement.GetProperty("hit_end").GetInt32());
	}

	/// <summary>
	/// Plays every game frame of <paramref name="clipId"/> on the Oni's body through <see cref="FighterModel.Apply"/>
	/// (one Seek per 60 Hz tick, F3) and returns the playhead tick of each frame. Checks it equals the curve's tick and
	/// that the played motion keeps F2's limits: each frame advances 0 or 0.5-2 raw ticks, a held tick shows on at most
	/// 4 frames. Seeking the frames again backwards gives the same playheads (deterministic, no carried state).
	/// </summary>
	static double[] PlayThroughSeek(Node runner, ClipCatalog cat, string clipId, string label)
	{
		var info = cat[clipId]!;
		var model = FighterModel.Create(FighterAnimSet.Oni, FightScene.Catalog);
		Assert.True(model != null, "the Oni's model loads");
		runner.AddChild(model);
		try
		{
			Assert.True(model!.Player.CallbackModeProcess == AnimationMixer.AnimationCallbackModeProcess.Manual, "the AnimationPlayer never advances on its own");
			Assert.True(model.Has(clipId), $"{clipId} is in the Oni's library");
			string anim = FighterModel.LibraryName + "/" + clipId;
			double length = model.Player.GetAnimation(anim).Length;
			var ticks = new double[info.FramesTotal + 1];
			for (int f = 1; f <= info.FramesTotal; f++)
			{
				model.Apply(new PoseSample(PoseKind.Attack, clipId, f, null, 0, 1f), cat, 1, f);
				PoseIsTheTakeAt(model, anim, info.TimeAt(f), $"{label} frame {f}");
				var (name, time) = model.Playhead;
				// A seek onto the very end of a non-looping animation finishes it (Godot clears the current animation,
				// the pose stays, checked above); everywhere else the playhead is the curve's time. Length is a float32.
				if (info.TimeAt(f) >= length - 1e-6 && string.IsNullOrEmpty(name)) { ticks[f] = info.RawTickAt(f); continue; }
				Assert.Equal(anim, name, $"{label} frame {f}: playing the clip (t {info.TimeAt(f):R}, length {length:R})");
				Assert.True(Math.Abs(time - info.TimeAt(f)) < 1e-6, $"{label} frame {f}: seek {time} = curve time {info.TimeAt(f)}");
				ticks[f] = time * ClipInfo.RawTicksPerSecond;
			}
			int held = 1;
			for (int f = 2; f <= info.FramesTotal; f++)
			{
				double step = ticks[f] - ticks[f - 1];
				held = Math.Abs(step) < 1e-6 ? held + 1 : 1;
				Assert.True(held <= ClipCatalog.RetimeMaxHoldFrames, $"{label} frame {f}: a tick held on {held} frames");
				Assert.True(Math.Abs(step) < 1e-6 || (step > 0.5 - 1e-6 && step < 2 + 1e-6), $"{label} frame {f}: advances {step:0.###} ticks (0.5x-2x)");
			}
			for (int f = info.FramesTotal; f >= 1; f--)
			{
				model.Apply(new PoseSample(PoseKind.Attack, clipId, f, null, 0, 1f), cat, 1, f);
				PoseIsTheTakeAt(model, anim, ticks[f] / ClipInfo.RawTicksPerSecond, $"{label} frame {f} again");
				var (name, time) = model.Playhead;
				if (info.TimeAt(f) >= length - 1e-6 && string.IsNullOrEmpty(name)) continue; // the end seek, as above
				Assert.Equal(anim, name, $"{label} frame {f} again: playing the clip");
				Assert.True(Math.Abs(time * ClipInfo.RawTicksPerSecond - ticks[f]) < 1e-6, $"{label} frame {f} again: same playhead {time * ClipInfo.RawTicksPerSecond} = {ticks[f]}");
			}
			return ticks;
		}
		finally { runner.RemoveChild(model); model!.Free(); }
	}

	/// <summary>Every bone the take rotates is posed as the take at <paramref name="t"/> seconds.</summary>
	static void PoseIsTheTakeAt(FighterModel model, string animName, double t, string label)
	{
		var anim = model.Player.GetAnimation(animName);
		int bones = 0;
		for (int i = 0; i < anim.GetTrackCount(); i++)
		{
			if (anim.TrackGetType(i) != Animation.TrackType.Rotation3D) continue;
			var path = anim.TrackGetPath(i);
			if (path.GetSubNameCount() == 0) continue;
			int b = model.Skeleton.FindBone(path.GetSubName(0));
			if (b < 0) continue;
			var want = anim.RotationTrackInterpolate(i, t);
			var got = model.Skeleton.GetBonePoseRotation(b);
			Assert.True(Math.Abs(want.Dot(got)) > 1 - 1e-5, $"{label}: {path.GetSubName(0)} posed as the take at {t * ClipInfo.RawTicksPerSecond:0.###} ticks");
			bones++;
		}
		Assert.True(bones >= 10, $"{label}: the take rotates the skeleton ({bones} bones)");
	}

	/// <summary>YOK-137's two Quake timings: the clip's own curve (data/clips/oni-quake.json, read as is) and the
	/// table's 22/5/26 fixture, both played frame by frame on the Oni through Seek().</summary>
	[Test]
	public static void OniQuake_BothTimingsPlayThroughTheCurveAndSeek(Node runner)
	{
		var quake = FightScene.Catalog["oni-quake"];
		Assert.True(quake?.Retime is { Count: > 0 }, "oni-quake is catalogued with its retime curve");
		var (hs, he) = HitWindowOf("oni-quake");
		var clip = PlayThroughSeek(runner, FightScene.Catalog, "oni-quake", $"clip {hs - 1}/{he - hs + 1}/{quake!.FramesTotal - he}");

		var table = QuakeTableTimingFixture(quake);
		Assert.True(table.Problems.Count == 0 && table["oni-quake"]?.Retime != null, "the 22/5/26 curve is inside F2's limits: " + string.Join("; ", table.Problems));
		Assert.Equal(53, table["oni-quake"]!.FramesTotal, "22 + 5 + 26 frames");
		var fixture = PlayThroughSeek(runner, table, "oni-quake", "table 22/5/26");
		Assert.True(Math.Abs(clip[hs] - fixture[23]) < 1e-6, $"both timings land the first active frame on the same pose of the take ({clip[hs]} vs {fixture[23]})");
		Assert.True(Math.Abs(clip[quake.FramesTotal] - fixture[53]) < 1e-6, "both end on the take's last trimmed tick");
	}
}
