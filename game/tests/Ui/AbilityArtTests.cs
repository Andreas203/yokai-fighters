using System.IO;
using System.Text.Json;
using Godot;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>YOK-79: the shared ability-art lookup. Missing art is legal, so no test demands an icon per id.</summary>
public static class AbilityArtTests
{
	[Test]
	public static void KnownIdResolvesToItsOwnArt(Node host)
	{
		foreach (var id in new[] { "foxfire", "spirit-wave", "rising-talisman" })
		{
			Assert.True(AbilityArt.Has(id), id + " has art");
			Assert.True(AbilityArt.ForId(id) != AbilityArt.Fallback, id + " is not the fallback");
		}
		Assert.True(AbilityArt.ForId("foxfire") != AbilityArt.ForId("spirit-wave"), "distinct icons per id");
	}

	[Test]
	public static void NullEmptyAndUnknownFallBack(Node host)
	{
		Assert.True(AbilityArt.Fallback != null, "fallback exists");
		foreach (var id in new string?[] { null, "", "no-such-ability", "Foxfire", "foxfire " })
		{
			Assert.True(!AbilityArt.Has(id), "no art for '" + id + "'");
			Assert.True(AbilityArt.ForId(id) == AbilityArt.Fallback, "fallback for '" + id + "'");
		}
	}

	[Test]
	public static void EveryDataIdResolvesOrFallsBack(Node host)
	{
		string root = ProjectSettings.GlobalizePath("res://") + "../data/";
		int seen = 0;
		foreach (var sub in new[] { "moves", "modifiers", "cancels" })
		{
			string dir = root + sub;
			if (!Directory.Exists(dir)) continue;
			foreach (var file in Directory.GetFiles(dir, "*.json"))
			{
				using var doc = JsonDocument.Parse(File.ReadAllText(file));
				if (!doc.RootElement.TryGetProperty("id", out var idEl)) continue;
				string id = idEl.GetString() ?? "";
				var tex = AbilityArt.ForId(id);
				Assert.True(tex != null, "texture for " + file);
				if (!AbilityArt.Has(id)) Assert.True(tex == AbilityArt.Fallback, "unmapped " + id + " falls back");
				seen++;
			}
		}
		Assert.True(seen > 0, "found ability data files");
	}

	[Test]
	public static void LookupIsCached(Node host)
	{
		Assert.True(AbilityArt.ForId("foxfire") == AbilityArt.ForId("foxfire"), "same instance each call");
	}
}
