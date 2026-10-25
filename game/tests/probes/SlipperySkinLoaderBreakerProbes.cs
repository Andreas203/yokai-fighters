using System;
using System.IO;
using Godot;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-172 (AM12): the loader's closed list after the rename throw_invuln -> throw_break. Replaces the case the
/// advisory names in AbilityLoaderEffectsProbes.cs:59 (it now passes for the wrong reason).
/// </summary>
public static class SlipperySkinLoaderBreakerProbes
{
	static string ModJson(string effect) =>
		"{\"kind\":\"modifier\",\"id\":\"x\",\"source\":\"kappa\",\"applies_to\":{\"requires\":[]},\"effects\":[" + effect + "]}";
	static string Eff(string target, string op, string value) => $"{{\"target\":\"{target}\",\"op\":\"{op}\",\"value\":{value}}}";
	static bool Refused(string e)
	{
		try { ModifierLoader.Parse(ModJson(e)); } catch (FormatException) { return true; }
		return false;
	}

	// Closed list (loader vocabulary): control first, so the refusals below cannot pass for a broken harness.
	[Test]
	public static void ThrowBreakTarget_AcceptedOnlyAsSetStartup()
	{
		Assert.True(!Refused(Eff("properties.throw_break", "set", "\"startup\"")), "control: set \"startup\" accepted");
		foreach (var (t, op, v) in new[] {
			("properties.throw_break.startup", "set", "\"startup\""), // nested path
			("properties.throw_break.x", "set", "\"startup\""),
			("properties.throw_break", "set", "\"active\""),
			("properties.throw_break", "set", "\"Startup\""),
			("properties.throw_break", "set", "true"),
			("properties.throw_break", "set", "1"),
			("properties.throw_break", "set", "null"),
			("properties.throw_break", "add", "1"),
			("properties.throw_break", "mul_pct", "50"),
			("properties.throw_invuln", "set", "\"startup\""), // the old name is gone
			("properties.throw_invuln.startup", "set", "\"startup\""),
		})
			Assert.True(Refused(Eff(t, op, v)), $"{t} {op} {v} was accepted");
	}

	// AM12: the shipped modifier uses only the new name.
	[Test]
	public static void ShippedModifierUsesThrowBreak_NotTheOldName()
	{
		string repo = Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "..");
		string text = File.ReadAllText($"{repo}/data/modifiers/slippery-skin.json");
		Assert.True(text.Contains("properties.throw_break"), "targets throw_break");
		Assert.True(!text.Contains("throw_invuln"), "no stale throw_invuln in the data");
	}
}
