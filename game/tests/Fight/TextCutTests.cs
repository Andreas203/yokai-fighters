using System.Globalization;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-162: the copy talisman cuts names only between text elements. Before the fix it measured and showed halves of
/// surrogate pairs, which Godot 4.7.2 decodes into garbage code points; CopyHudProbes.NameFit_NeverCutsASurrogatePair
/// then segfaulted in about one run in four.
/// </summary>
public static class TextCutTests
{
	static readonly string[] Samples =
	{
		"", "A", "Spirit Wave", string.Concat(System.Linq.Enumerable.Repeat("\U0001D504", 30)),
		string.Concat(System.Linq.Enumerable.Repeat("é", 30)), "\U0001D504b́\U0001F98A-\U0001D505",
	};

	[Test]
	public static void Cuts_AreTextElementBoundaries_NeverHalfAPairOrABareMark()
	{
		foreach (string s in Samples)
		{
			int[] cuts = CopyTalisman.Cuts(s);
			Assert.Equal(0, cuts[0], $"first cut of '{s}'");
			Assert.Equal(s.Length, cuts[^1], $"last cut of '{s}'");
			Assert.Equal(new StringInfo(s).LengthInTextElements + 1, cuts.Length, $"one cut per text element of '{s}'");
			foreach (int c in cuts)
			{
				if (c == 0 || c == s.Length) continue;
				Assert.True(!char.IsLowSurrogate(s[c]), $"'{s}' cut at {c} splits a surrogate pair");
				Assert.True(CharUnicodeInfo.GetUnicodeCategory(s, c) != UnicodeCategory.NonSpacingMark, $"'{s}' cut at {c} strands a combining mark");
			}
		}
	}

	sealed class View : IHudRunView
	{
		public bool CopyLocked { get; set; }
		public CopyTarget? Copy { get; set; }
		public bool InTanukiFight { get; set; }
		public bool ElderFight { get; set; }
	}

	static string Root => System.IO.Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "..");

	[Test]
	public static void LetterBadge_IsTheWholeFirstLetter_ForAnAstralName(Node runner)
	{
		var hud = GD.Load<PackedScene>("res://scenes/ui/hud.tscn").Instantiate<FightHud>();
		runner.AddChild(hud);
		try
		{
			string fd = AbilityPool.LoadFixtures(Root).Special("test-spirit-wave")!.Level1Json;
			var data = SpecialLoader.Parse("{\"kind\":\"special\",\"id\":\"hud-astral\",\"name\":\"\U0001D504bc\",\"input\":{\"kata\":{\"slot\":\"A\"}},\"levels\":{\"1\":{\"frame_data\":" + fd + "}}}");
			hud.RunView = new View { InTanukiFight = true, Copy = new CopyTarget(SpecialSlot.A, data, 1) };
			hud.Refresh(new Match());
			// Name[..1] was a lone high surrogate; Godot read it back as U+FFFD or garbage.
			Assert.Equal("\U0001D504", UiFind.Get<Label>(hud.CopyTarget, "IconLetter").Text, "letter badge keeps the whole pair");
			Assert.Equal("\U0001D504BC", hud.CopyTarget.MoveName, "short astral name shown unchanged");
		}
		finally { hud.Free(); }
	}
}
