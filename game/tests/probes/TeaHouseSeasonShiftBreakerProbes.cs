using System;
using System.Linq;
using Godot;
using YokaiFighters.Fight;

namespace YokaiFighters.Tests;

/// <summary>Break probes for the tea house season-painting shift (YOK-tea-house-season-crop).</summary>
public static class TeaHouseSeasonShiftBreakerProbes
{
	// Rule: stage spec (tanuki-tea-house.md) - the tscn value is the constant, to float precision.
	[Test]
	public static void TscnPaintingBottomV_EqualsConstant_ToFloatPrecision(Node runner)
	{
		string tscn = System.IO.File.ReadAllText(ProjectSettings.GlobalizePath("res://scenes/stage/tea_house.tscn"));
		var m = System.Text.RegularExpressions.Regex.Match(tscn, @"shader_parameter/painting_bottom_v = ([0-9.]+)");
		Assert.True(m.Success, "tscn sets painting_bottom_v");
		float v = float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
		Assert.True(Math.Abs(v - TeaHouseStage.PaintingBottomV) * 566f < 0.01f, $"tscn {v} vs {TeaHouseStage.PaintingBottomV}: under 0.01 row apart");
	}

	// Rule: paintings shifted down must repeat a flat sky above; rows 0..8 must match row 0 (no visible seam where the clamp ends).
	[Test]
	public static void TopRows_AreFlatAcrossFirstRows_SoTheRepeatHasNoSeam(Node runner)
	{
		foreach (string s in TeaHouseStage.Seasons)
		{
			var img = Image.LoadFromFile(ProjectSettings.GlobalizePath($"{TeaHouseStage.DefaultDir}seasons/{s}.png"));
			for (int y = 1; y <= 8; y++)
			{
				int worst = 0;
				for (int x = 0; x < img.GetWidth(); x++)
				{
					Color a = img.GetPixel(x, 0), b = img.GetPixel(x, y);
					worst = Math.Max(worst, Math.Abs(a.R8 - b.R8) + Math.Abs(a.G8 - b.G8) + Math.Abs(a.B8 - b.B8));
				}
				Assert.True(worst < 12, $"{s}: row {y} differs from row 0 by {worst} (seam when row 0 repeats)");
			}
		}
	}
}
