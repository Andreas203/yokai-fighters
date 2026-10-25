using System;
using System.Collections.Generic;
using System.Text.Json;
using YokaiFighters.Fight;

namespace YokaiFighters.Tests;

/// <summary>YOK-164 test-breaker probes on the retiming curve (F2: 0.5x-2x per segment, hold at most 4 frames,
/// contiguous 1..frames_total, ticks inside the trim; F3: deterministic Seek stepping).</summary>
public static class ClipRetimeBreakerProbes
{
	static string? Check(string json, int total, (int, int)? trim = null)
	{
		using var doc = JsonDocument.Parse(json);
		return ClipCatalog.ParseRetime(doc.RootElement, out var segs) ?? ClipCatalog.CheckRetime(segs!, total, trim);
	}

	static ClipInfo Clip(int total, int trimStart, int trimEnd, params RetimeSegment[] segs) =>
		new("x", "res://x.glb", trimStart, trimEnd, 1.0, total) { Retime = segs };

	// F2: a hold of exactly 4 frames is allowed at the curve start and end, 5 refused.
	[Test]
	public static void Hold_4AcceptedAnd5Refused_AtCurveStartAndEnd()
	{
		Assert.True(Check("[{\"frames\":[1,4],\"ticks\":[7,7]},{\"frames\":[4,8],\"ticks\":[7,15]}]", 8, (0, 20)) == null, "hold on frames 1-4");
		Assert.True(Check("[{\"frames\":[1,5],\"ticks\":[7,7]},{\"frames\":[5,8],\"ticks\":[7,13]}]", 8, (0, 20)) != null, "hold on frames 1-5");
		Assert.True(Check("[{\"frames\":[1,5],\"ticks\":[0,8]},{\"frames\":[5,8],\"ticks\":[8,8]}]", 8, (0, 20)) == null, "hold on last frames 5-8 (4)");
		Assert.True(Check("[{\"frames\":[1,4],\"ticks\":[0,6]},{\"frames\":[4,9],\"ticks\":[6,6]}]", 9, (0, 20)) != null, "hold 4-9 = 6 frames");
	}

	// F2: hold, move, hold again are separate runs of at most 4.
	[Test]
	public static void Hold_ResetsAfterAMovingSegment()
	{
		string j = "[{\"frames\":[1,4],\"ticks\":[5,5]},{\"frames\":[4,6],\"ticks\":[5,6]},{\"frames\":[6,9],\"ticks\":[6,6]}]";
		Assert.True(Check(j, 9, (0, 20)) == null, "4-frame hold, move, 4-frame hold is legal");
	}

	// F2: 0.5x and 2x exact accepted; one tick off refused; reverse refused.
	[Test]
	public static void SpeedBounds_ExactAndOneOff()
	{
		Assert.True(Check("[{\"frames\":[1,3],\"ticks\":[0,4]}]", 3, (0, 9)) == null, "2x exact");
		Assert.True(Check("[{\"frames\":[1,3],\"ticks\":[0,5]}]", 3, (0, 9)) != null, "2.5x");
		Assert.True(Check("[{\"frames\":[1,3],\"ticks\":[0,1]}]", 3, (0, 9)) == null, "0.5x exact");
		Assert.True(Check("[{\"frames\":[1,4],\"ticks\":[0,1]}]", 4, (0, 9)) != null, "1/3x");
		Assert.True(Check("[{\"frames\":[1,7],\"ticks\":[0,3]}]", 7, (0, 9)) == null, "3/6 = 0.5x exact on a long segment");
		Assert.True(Check("[{\"frames\":[1,7],\"ticks\":[0,2]}]", 7, (0, 9)) != null, "2/6 below 0.5x");
		Assert.True(Check("[{\"frames\":[1,7],\"ticks\":[9,0]}]", 7, (0, 9)) != null, "reverse playback");
	}

	// F2: ticks exactly on the trim are accepted; one tick outside refused.
	[Test]
	public static void TrimEdges_ExactlyOnAndOneOutside()
	{
		Assert.True(Check("[{\"frames\":[1,3],\"ticks\":[10,14]}]", 3, (10, 14)) == null, "exactly the trim");
		Assert.True(Check("[{\"frames\":[1,3],\"ticks\":[10,14]}]", 3, (11, 14)) != null, "one below trim");
		Assert.True(Check("[{\"frames\":[1,3],\"ticks\":[10,14]}]", 3, (10, 13)) != null, "one above trim");
	}

	// F2: a one-frame curve has no segment with last frame after first.
	[Test]
	public static void OneFrameClip_RefusedNotCrashed()
	{
		Assert.True(Check("[{\"frames\":[1,1],\"ticks\":[3,3]}]", 1, (0, 9)) != null, "zero-length segment");
	}

	// F2: huge values are refused or exact, never wrapped.
	[Test]
	public static void Overflow_HugeFramesAndTicks()
	{
		int M = int.MaxValue;
		Assert.True(ClipCatalog.CheckRetime(new List<RetimeSegment> { new(1, M, 0, M - 1) }, M) == null, "1x over int.MaxValue frames");
		Assert.True(ClipCatalog.CheckRetime(new List<RetimeSegment> { new(1, M, 0, M), new(M, M, M, M) }, M) != null, "zero-length tail");
		Assert.True(ClipCatalog.CheckRetime(new List<RetimeSegment> { new(int.MinValue, M, 0, M) }, M) != null, "start frame must be 1");
		Assert.True(ClipCatalog.CheckRetime(new List<RetimeSegment> { new(1, 3, 0, M) }, 3) != null, "huge tick delta in 2 frames");
		Assert.True(ClipCatalog.CheckRetime(new List<RetimeSegment> { new(1, M, M, 0) }, M) != null, "huge reverse");
		var c = Clip(M, 0, M, new RetimeSegment(1, M, 0, M - 1));
		Assert.Equal((double)(M - 2), c.RawTickAt(M - 1), "RawTickAt near int.MaxValue is exact");
		Assert.Equal((double)M - 1, c.RawTickAt(M), "last frame maps to the last tick (tick f-1 at 1x)");
	}

	// F3: frame 0, negative, int.MinValue clamp to frame 1; beyond total clamps to the last tick; shared boundary is shared.
	[Test]
	public static void RawTickAt_ClampsOutOfRangeFrames()
	{
		var c = Clip(9, 20, 60, new RetimeSegment(1, 5, 20, 28), new RetimeSegment(5, 9, 28, 32));
		foreach (int f in new[] { 0, -1, int.MinValue }) Assert.Equal(20.0, c.RawTickAt(f), $"frame {f} -> first tick");
		foreach (int f in new[] { 10, 1000, int.MaxValue }) Assert.Equal(32.0, c.RawTickAt(f), $"frame {f} -> last tick");
		Assert.Equal(28.0, c.RawTickAt(5), "shared boundary frame gives the shared tick");
		Assert.Equal(26.0, c.RawTickAt(4), "frame before the boundary (2x)");
		Assert.Equal(29.0, c.RawTickAt(6), "frame after the boundary (1x)");
	}

	// F3: stepping every frame never goes backwards, stays in the trim, repeats, and ends on the last tick.
	[Test]
	public static void RawTickAt_MonotonicRepeatableInTrim()
	{
		var c = Clip(33, 22, 53, new RetimeSegment(1, 5, 22, 30), new RetimeSegment(5, 8, 30, 30),
			new RetimeSegment(8, 12, 30, 32), new RetimeSegment(12, 33, 32, 53));
		double prev = double.MinValue;
		for (int f = 1; f <= 33; f++)
		{
			double t = c.RawTickAt(f);
			Assert.True(t >= prev, $"frame {f} went backwards");
			Assert.True(t == c.RawTickAt(f), "same frame, same tick");
			Assert.True(t >= 22 && t <= 53, $"frame {f} outside trim");
			prev = t;
		}
		Assert.Equal(53.0, c.RawTickAt(33), "last frame lands exactly on the last tick");
	}

	// F3: TimeAt * 60 is the tick; times stay inside the trim in seconds.
	[Test]
	public static void TimeAt_RoundTripsAndStaysInsideTheTrim()
	{
		var c = Clip(33, 22, 53, new RetimeSegment(1, 5, 22, 30), new RetimeSegment(5, 8, 30, 30),
			new RetimeSegment(8, 12, 30, 32), new RetimeSegment(12, 33, 32, 53));
		for (int f = 1; f <= 33; f++)
		{
			Assert.True(Math.Abs(c.TimeAt(f) * 60.0 - c.RawTickAt(f)) < 1e-9, $"frame {f} round trip");
			Assert.True(c.TimeAt(f) <= 53 / 60.0 + 1e-12 && c.TimeAt(f) >= 22 / 60.0 - 1e-12, $"frame {f} inside the trim");
		}
	}

	// F2: an unchecked hand-built curve running past TrimEnd never reaches Seek.
	[Test]
	public static void RawTickAt_NeverPastTrimEnd()
	{
		var c = Clip(5, 0, 6, new RetimeSegment(1, 5, 0, 20));
		for (int f = 1; f <= 5; f++) Assert.True(c.RawTickAt(f) <= 6.0, $"frame {f}");
	}

	// F2: a hold split across adjacent hold segments counts as one run (6 frames refused; 3+2 pieces = 4 accepted).
	[Test]
	public static void Hold_SplitAcrossTwoSegmentsCountsOnce()
	{
		Assert.True(Check("[{\"frames\":[1,4],\"ticks\":[3,3]},{\"frames\":[4,6],\"ticks\":[3,3]},{\"frames\":[6,9],\"ticks\":[3,9]}]", 9, (0, 20)) != null, "frames 1-6 held");
		Assert.True(Check("[{\"frames\":[1,3],\"ticks\":[3,3]},{\"frames\":[3,4],\"ticks\":[3,3]},{\"frames\":[4,8],\"ticks\":[3,11]}]", 8, (0, 20)) == null, "frames 1-4 held in 2 pieces");
	}

	// F2: no trim fields; negative start tick refused, 0 accepted.
	[Test]
	public static void NoTrim_NegativeTickRefused_ZeroAccepted()
	{
		Assert.True(Check("[{\"frames\":[1,3],\"ticks\":[0,2]}]", 3) == null, "tick 0 ok");
		Assert.True(Check("[{\"frames\":[1,3],\"ticks\":[-1,1]}]", 3) != null, "negative tick");
	}
}
