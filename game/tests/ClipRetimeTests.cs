using System;
using System.Collections.Generic;
using YokaiFighters.Fight;

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
}
