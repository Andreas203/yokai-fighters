using Godot;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>Icons are data-driven: <c>ability-icon-&lt;id&gt;.png</c> beside the others shows with no code change; evolutions wear their special's.</summary>
public static class AbilityArtFileTests
{
	[Test]
	public static void AFileNamedForTheId_IsShownWithNoMapping_AndAMissingOneFallsBack()
	{
		const string id = "zz-test-icon";
		string disk = ProjectSettings.GlobalizePath(AbilityArt.FilePath(id));
		Assert.True(!AbilityArt.Has(id) && AbilityArt.ForId(id) == AbilityArt.Fallback, "no file: the generic swirl");
		var img = Image.CreateEmpty(8, 8, false, Image.Format.Rgba8);
		img.Fill(Colors.Red);
		img.SavePng(disk);
		try
		{
			Assert.True(AbilityArt.Has(id) && AbilityArt.ForId(id) != AbilityArt.Fallback, "the dropped file shows");
		}
		finally { System.IO.File.Delete(disk); }
	}

	[Test]
	public static void AnEvolution_WearsItsSpecialsIcon()
	{
		Assert.True(AbilityArt.ForId("piercing-foxfire") == AbilityArt.ForId("foxfire") || AbilityArt.Has("piercing-foxfire"), "piercing-foxfire shows foxfire's icon");
		Assert.True(AbilityArt.Has("piercing-foxfire"), "an evolution of a special with an icon has art");
	}

	[Test]
	public static void IdsAreNeverPaths()
	{
		foreach (string id in new[] { "../data/moves/foxfire", "foxfire/", "a b", new string('a', 500) })
			Assert.True(AbilityArt.ForId(id) == AbilityArt.Fallback, "'" + id.Substring(0, System.Math.Min(20, id.Length)) + "' gets the fallback");
	}
}
