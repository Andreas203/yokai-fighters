using System;
using System.Collections.Generic;
using YokaiFighters.Fight;

namespace YokaiFighters.Tests;

/// <summary>YOK-113 probes. Rule F2 (AM5): a segment plays 0.5x-2x or holds, a hold shows on at most 4 frames.</summary>
public static class ClipRetimeProbes
{
	static RetimeSegment S(int a, int b, int ta, int tb) => new(a, b, ta, tb);
	static string? Check(int total, params RetimeSegment[] s) => ClipCatalog.CheckRetime(s, total);

	// F2: 1 tick per frame over a huge clip is 1x, so it is valid; int overflow in 2*dt must not reject it.
	[Test]
	public static void Probe_LargeValidSegment_NotRejectedByOverflow()
	{
		const int Max = int.MaxValue;
		Assert.True(Check(Max, S(1, Max, 0, Max - 1)) == null, "1x over int.MaxValue frames (2*dt overflows)");
		Assert.True(Check(1_500_000_000, S(1, 1_500_000_000, 0, 2_000_000_000)) == null, "1.33x (2*df overflows)");
	}

	// F2: exact 0.5x and 2x pass, one tick over fails, with odd frame counts.
	[Test]
	public static void Probe_SpeedBoundaries()
	{
		Assert.True(Check(4, S(1, 4, 0, 6)) == null, "2x over 3 frames");
		Assert.True(Check(4, S(1, 4, 0, 7)) != null, "2.33x");
		Assert.True(Check(4, S(1, 4, 0, 1)) != null, "0.33x");
		Assert.True(Check(5, S(1, 5, 0, 2)) == null, "0.5x over 4 frames");
		Assert.True(Check(6, S(1, 6, 0, 2)) != null, "0.4x");
	}

	// F2: hold run counted across 3+ segments, shared boundaries; a non-hold between resets it.
	[Test]
	public static void Probe_HoldAcrossManySegments()
	{
		Assert.True(Check(5, S(1, 2, 5, 5), S(2, 3, 5, 5), S(3, 4, 5, 5), S(4, 5, 5, 5)) != null, "5 frames held over 4 segments");
		Assert.True(Check(4, S(1, 2, 5, 5), S(2, 3, 5, 5), S(3, 4, 5, 5)) == null, "exactly 4 frames over 3 segments");
		Assert.True(Check(8, S(1, 4, 5, 5), S(4, 5, 5, 6), S(5, 8, 6, 6)) == null, "two holds of 4 and 4 on different ticks");
		Assert.True(Check(9, S(1, 4, 5, 5), S(4, 6, 5, 6), S(6, 9, 6, 6)) == null, "holds of 4 either side of a slow segment");
	}

	// F2/F3: for any valid curve, ticks stay within [first, last] for frames -3..N+3 and never decrease.
	[Test]
	public static void Probe_RandomValidCurvesAreBoundedAndMonotonic()
	{
		var rng = new Random(113);
		for (int round = 0; round < 300; round++)
		{
			var segs = new List<RetimeSegment>();
			int f = 1, t = rng.Next(0, 20), holdRun = 0;
			int n = rng.Next(1, 7);
			for (int i = 0; i < n; i++)
			{
				int df = rng.Next(1, 8);
				int dt;
				if (rng.Next(4) == 0 && holdRun + df <= 3) { dt = 0; holdRun += df; }
				else { holdRun = 0; dt = rng.Next((df + 1) / 2, 2 * df + 1); if (dt == 0) dt = 1; }
				segs.Add(S(f, f + df, t, t + dt));
				f += df; t += dt;
			}
			string? why = ClipCatalog.CheckRetime(segs, f);
			if (why != null) continue; // generator may exceed 0.5x on tiny segments; only valid ones matter
			var c = new ClipInfo("p", "res://x.glb", segs[0].FirstTick, segs[^1].LastTick, 1.0, f) { Retime = segs };
			double first = segs[0].FirstTick, last = segs[^1].LastTick, prev = first;
			Assert.Equal(first, c.RawTickAt(0), "frame 0");
			Assert.Equal(first, c.RawTickAt(1), "frame 1");
			Assert.Equal(first, c.RawTickAt(-100), "negative");
			Assert.Equal(first, c.RawTickAt(int.MinValue), "int.MinValue");
			Assert.Equal(last, c.RawTickAt(f), "frames_total");
			Assert.Equal(last, c.RawTickAt(f + 1), "frames_total+1");
			Assert.Equal(last, c.RawTickAt(int.MaxValue), "int.MaxValue");
			for (int fr = 1; fr <= f; fr++)
			{
				double v = c.RawTickAt(fr);
				Assert.True(v >= first && v <= last, $"round {round} frame {fr} inside range");
				Assert.True(v >= prev, $"round {round} frame {fr} not decreasing");
				prev = v;
			}
			Assert.Equal(f, c.ComputedFrames, "ComputedFrames");
		}
	}

	// F2: retime wins over speed_scale; with both set the curve decides every frame.
	[Test]
	public static void Probe_RetimeIgnoresSpeedScale()
	{
		var curve = new[] { S(1, 5, 10, 18) };
		var a = new ClipInfo("a", "res://x.glb", 10, 18, 1.0, 5) { Retime = curve };
		var b = new ClipInfo("b", "res://x.glb", 10, 18, 7.5, 5) { Retime = curve };
		for (int fr = 0; fr <= 6; fr++) Assert.Equal(a.RawTickAt(fr), b.RawTickAt(fr), $"frame {fr}");
		Assert.Equal(a.ComputedFrames, b.ComputedFrames, "ComputedFrames ignores speed");
	}

	// No curve: every clip in data/clips matches the pre-YOK-113 formula for every frame, -2..FramesTotal+2.
	[Test]
	public static void Probe_NoCurveClipsMatchOldFormula()
	{
		string clipsDir = Godot.ProjectSettings.GlobalizePath("res://") + "../data/clips";
		var cat = ClipCatalog.Load(clipsDir, Godot.ProjectSettings.GlobalizePath("res://"));
		int checkedClips = 0;
		foreach (string path in System.IO.Directory.GetFiles(clipsDir, "*.json"))
		{
			string id = System.IO.Path.GetFileNameWithoutExtension(path);
			var c = cat[id];
			if (c == null) continue;
			Assert.True(c.Retime == null, $"{id}: no curve");
			for (int f = -2; f <= c.FramesTotal + 2; f++)
			{
				int n = Math.Clamp(f, 1, c.FramesTotal);
				Assert.Equal(Math.Min(c.TrimStart + (n - 1) * c.Speed, c.TrimEnd), c.RawTickAt(f), $"{id} frame {f}");
			}
			Assert.Equal((int)Math.Floor((c.TrimEnd - c.TrimStart) / c.Speed + 1e-9) + 1, c.ComputedFrames, $"{id} ComputedFrames");
			checkedClips++;
		}
		Assert.True(checkedClips > 0, "some clips were compared");
	}

	// Schema/validator: retime ints must be integers; 1.0 and 1e0 are rejected by Python (is_int), so C# must reject them too.
	[Test]
	public static void Probe_IntegralFloatSpellingsRejectedLikePython()
	{
		foreach (string lit in new[] { "1.0", "1e0", "10E-1" })
		{
			using var doc = System.Text.Json.JsonDocument.Parse("[{\"frames\":[" + lit + ",5],\"ticks\":[0,4]}]");
			Assert.True(ClipCatalog.ParseRetime(doc.RootElement, out _) != null, $"frame literal {lit} rejected");
		}
	}
}
