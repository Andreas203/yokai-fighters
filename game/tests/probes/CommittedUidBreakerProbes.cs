using Godot;

namespace YokaiFighters.Tests;

/// <summary>YOK-214 probes: each committed .uid file must name a uid that Godot resolves back to its own script path,
/// and no two scripts may share a uid. Rule basis: Godot 4.4+ .cs.uid sidecar contract (one uid://... line per script).</summary>
public static class CommittedUidBreakerProbes
{
	static readonly string[] Scripts =
	{
		"res://tests/Ai/AiGuardHitstopTests.cs", "res://tests/Fight/TextCutTests.cs", "res://tests/captures/PracticeCapture.cs",
		"res://tests/probes/AiGuardHitstopBreakerProbes.cs", "res://tests/probes/ClipRetimeBreakerProbes.cs",
		"res://tests/probes/HitSparkBreakerProbes.cs", "res://tests/probes/PracticeBreakerProbes.cs",
		"res://tests/probes/TextCutBreakerProbes.cs", "res://tests/probes/TextElementBadgeBreakerProbes.cs",
		"res://tests/probes/YokaiNameBreakerProbes.cs",
	};

	[Test]
	public static void EachUidResolvesToItsOwnScript(Node host)
	{
		foreach (var s in Scripts)
		{
			string text = FileAccess.GetFileAsString(s + ".uid").Trim();
			long id = ResourceUid.TextToId(text);
			Assert.True(id != ResourceUid.InvalidId, $"{s}: uid '{text}' does not parse");
			Assert.True(ResourceUid.HasId(id), $"{s}: uid {text} is not registered");
			Assert.Equal(s, ResourceUid.GetIdPath(id), $"{s}: uid {text} resolves to the wrong path");
		}
	}

	[Test]
	public static void UidsAreUnique(Node host)
	{
		var seen = new System.Collections.Generic.HashSet<string>();
		foreach (var s in Scripts)
			Assert.True(seen.Add(FileAccess.GetFileAsString(s + ".uid").Trim()), $"{s}: duplicate uid");
	}
}
