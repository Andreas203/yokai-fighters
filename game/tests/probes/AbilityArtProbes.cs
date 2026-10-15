using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Godot;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>Test Breaker probes for YOK-79 (shared AbilityArt lookup). Rule basis: the lookup contract in AbilityArt.cs
/// (ids are exact; null/empty/unknown give the fallback; never null) and the AbilityArt ticket.</summary>
public static class AbilityArtProbes
{
	static readonly string[] Mapped = { "foxfire", "spirit-wave", "rising-talisman" };

	// Contract: ids are exact; case and whitespace variants are unknown ids and get the fallback.
	[Test]
	public static void CaseAndWhitespaceVariantsFallBack(Node host)
	{
		foreach (var id in Mapped)
		{
			foreach (var v in new[] { id.ToUpperInvariant(), " " + id, id + "\t", id + "\n", id.Replace('-', '_'), id.Replace('-', ' '), "​" + id, " ", "\t" })
			{
				if (v == id) continue; // replace was a no-op
				Assert.True(!AbilityArt.Has(v), "no art for variant '" + v + "'");
				Assert.True(AbilityArt.ForId(v) == AbilityArt.Fallback, "fallback for variant '" + v + "'");
			}
		}
	}

	// Contract: Has and ForId agree for every probe id; ForId never null; Fallback stable.
	[Test]
	public static void HasAndForIdAgreeAndNeverNull(Node host)
	{
		var ids = new List<string?> { null, "", " ", "x", "res://ui/ability_art.tres", "../data/moves/foxfire", "foxfire/", new string('a', 5000) };
		ids.AddRange(Mapped);
		foreach (var id in ids)
		{
			var t = AbilityArt.ForId(id);
			Assert.True(t != null, "never null");
			Assert.True(AbilityArt.Has(id) == (t != AbilityArt.Fallback), "Has agrees with ForId");
			Assert.True(t == AbilityArt.ForId(id), "stable instance");
		}
		Assert.True(AbilityArt.Fallback == AbilityArt.Fallback, "fallback stable");
	}

	// Contract: every mapped icon and the fallback are real, loaded textures (rename + edited .import files), distinct from each other.
	[Test]
	public static void RenamedTexturesLoadViaResourceLoader(Node host)
	{
		var seen = new HashSet<Texture2D>();
		foreach (var id in Mapped)
		{
			string path = "res://assets/generated/ui/ability-icon-" + id + ".png";
			Assert.True(ResourceLoader.Exists(path), path + " exists as an imported resource");
			var tex = ResourceLoader.Load<Texture2D>(path);
			Assert.True(tex != null && tex.GetWidth() > 0 && tex.GetHeight() > 0, path + " loads with a size");
			Assert.True(tex == AbilityArt.ForId(id), id + " lookup returns the resource at the renamed path");
			Assert.True(seen.Add(AbilityArt.ForId(id)), id + " art distinct");
		}
		var fb = ResourceLoader.Load<Texture2D>("res://assets/generated/ui/ability-icon-fallback.png");
		Assert.True(fb != null && fb == AbilityArt.Fallback, "fallback path loads and is the Fallback");
		Assert.True(!seen.Contains(fb!), "fallback distinct from real art");
		Assert.True(fb!.GetWidth() > 0, "fallback has size");
	}

	// Contract: old pre-rename file names are gone (a stale name would mean the rename left orphans).
	[Test]
	public static void NoOrphanedOldArtOrImportFiles(Node host)
	{
		string dir = ProjectSettings.GlobalizePath("res://assets/generated/ui/");
		foreach (var f in Directory.GetFiles(dir, "*.png"))
			Assert.True(File.Exists(f + ".import"), Path.GetFileName(f) + " has an .import file");
		foreach (var f in Directory.GetFiles(dir, "*.import"))
			Assert.True(File.Exists(f.Substring(0, f.Length - ".import".Length)), Path.GetFileName(f) + " has its png");
		Assert.True(!File.Exists(ProjectSettings.GlobalizePath("res://scripts/Ui/PauseMoveArt.cs")), "PauseMoveArt removed");
	}

	// Contract: ids from every data dir resolve non-null; every mapped key is a real ability id in data (no dead keys).
	[Test]
	public static void EveryDataDirIdResolvesAndMappedKeysExistInData(Node host)
	{
		string root = ProjectSettings.GlobalizePath("res://") + "../data/";
		var all = new HashSet<string>();
		foreach (var sub in Directory.GetDirectories(root))
			foreach (var file in Directory.GetFiles(sub, "*.json", SearchOption.AllDirectories))
			{
				try
				{
					using var doc = JsonDocument.Parse(File.ReadAllText(file));
					if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("id", out var e) && e.ValueKind == JsonValueKind.String)
					{
						var id = e.GetString()!;
						all.Add(id);
						Assert.True(AbilityArt.ForId(id) != null, "texture for " + id);
					}
				}
				catch (JsonException) { }
			}
		Assert.True(all.Count > 0, "found ids");
		foreach (var id in Mapped) Assert.True(all.Contains(id), "mapped key '" + id + "' exists in data");
	}

	// Contract: pause menu with an unknown / null / empty move id shows the fallback, a known id its art, an empty slot hides it.
	[Test]
	public static void PauseMenuUnknownSpecialIdShowsFallback(Node host)
	{
		var m = GD.Load<PackedScene>("res://scenes/ui/pause_menu.tscn").Instantiate<PauseMenu>();
		m.FreezeTree = false;
		host.AddChild(m);
		var slots = new List<PauseSlotSummary>
		{
			new("A", "Ghost", 1, "", "no-such-special"),
			new("B", "Fox", 2, "", "foxfire"),
			new("C", "Nul", 1, "", null),
			new("D", "Empty", 0, "", "spirit-wave"),
		};
		m.SetRunSummary(new PauseRunSummary(new("Ryo", 500, 1000), new("Foe", 1, 1000), "Row 1", slots));
		Texture2D Icon(string s)
		{
			var n = m.FindChild("Slot" + s, true, false);
			Assert.True(n != null, "slot " + s + " built");
			return n!.FindChild("MoveIcon", true, false) is TextureRect r ? (r.Texture!) : null!;
		}
		Assert.True(Icon("A") == AbilityArt.Fallback, "unknown id shows fallback");
		Assert.True(Icon("B") == AbilityArt.ForId("foxfire"), "known id shows own art");
		Assert.True(Icon("C") == AbilityArt.Fallback, "null id shows fallback");
		var d = m.FindChild("SlotD", true, false)!.FindChild("MoveIcon", true, false) as TextureRect;
		Assert.True(d != null && !d.Visible, "empty slot icon hidden");
		m.QueueFree();
	}
}
