using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;

namespace YokaiFighters.Tests;

/// <summary>Marks a public static void method as a test; TestRunner finds it by reflection.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class TestAttribute : Attribute { }

public sealed class AssertFailed : Exception
{
	public AssertFailed(string m) : base(m) { }
}

public static class Assert
{
	public static void True(bool cond, string msg) { if (!cond) throw new AssertFailed(msg); }
	public static void Equal<T>(T expected, T actual, string msg)
	{
		if (!EqualityComparer<T>.Default.Equals(expected, actual))
			throw new AssertFailed($"{msg}: expected {expected}, got {actual}");
	}
}

/// <summary>
/// Headless test runner. Runs every [Test] in this assembly, prints PASS/FAIL lines and quits
/// with exit code 0 (all green) or 1. Run:
/// Godot --headless --path game res://tests/test_runner.tscn
/// Tests needing the scene tree take a Node parameter (the runner).
/// </summary>
public partial class TestRunner : Node
{
	public override void _Ready()
	{
		int pass = 0, fail = 0;
		foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
		foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
		{
			if (m.GetCustomAttribute<TestAttribute>() == null) continue;
			string name = $"{type.Name}.{m.Name}";
			try
			{
				m.Invoke(null, m.GetParameters().Length == 1 ? new object[] { this } : null);
				GD.Print($"PASS {name}");
				pass++;
			}
			catch (TargetInvocationException e)
			{
				GD.PrintErr($"FAIL {name}: {e.InnerException?.Message}");
				fail++;
			}
		}
		GD.Print($"TESTS {pass} passed, {fail} failed");
		GetTree().Quit(fail == 0 && pass > 0 ? 0 : 1);
	}
}
