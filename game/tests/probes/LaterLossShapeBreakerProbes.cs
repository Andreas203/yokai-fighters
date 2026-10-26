using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.Dev;
using YokaiFighters.Fight;
using YokaiFighters.Sim;
using YokaiFighters.Story;

namespace YokaiFighters.Tests;

/// <summary>
/// Test Breaker round 2 for YOK-212 (9b831a2). Differential: many later_loss card shapes go through the real validator
/// (tools/validate_data.py, one run over a folder) and the real loader; the validator's own contract is that a file it passes
/// loads, and the lose screen (S3, S4) must then fill it without throwing. Plus the dev jump preview against a real loss.
/// </summary>
public static class LaterLossShapeBreakerProbes
{
	static string Raw(string id, string later, string text = "{yokai} stands.", string ph = "[\"yokai\"]", string? title = null, string cardTail = "") =>
		"{ \"kind\": \"story_card\", \"id\": \"" + id + "\", \"status\": \"proposed\", \"trigger\": \"TEST FIXTURE\", \"speaker\": \"narrator\", "
		+ "\"text\": \"" + text + "\", \"placeholders\": " + ph + ", \"rules\": [\"S3\"], " + (title is null ? "" : "\"title\": " + title + ", ")
		+ "\"later_loss\": " + later + cardTail + " }";

	static IEnumerable<(string What, string Json)> Shapes()
	{
		int n = 0;
		string Id() => "shape-" + (n++).ToString("D3");
		string[] numbers =
		{
			"4", "4.0", "4.5", "4e0", "4E0", "4e+0", "4E-0", "0.4e1", "40e-1", "1e1", "-4", "0", "-0", "0.0", "2147483647", "2147483648", "-2147483648",
			"4294967296", "1e10", "1e400", "NaN", "Infinity", "-Infinity", "\"4\"", "true", "false", "null", "[]", "{}", "[4]",
		};
		foreach (string v in numbers)
		{
			yield return ("row_max " + v, Raw(Id(), "{ \"row_min\": 1, \"row_max\": " + v + " }"));
			yield return ("row_min " + v, Raw(Id(), "{ \"row_min\": " + v + ", \"row_max\": 2147483647 }"));
		}
		string[] yokai = { "\"kitsune\"", "\"oni\"", "\"kappa\"", "\"tanuki\"", "\"elder-oni\"", "\"elder-kappa\"", "\"apprentice\"", "\"nine-tailed-kitsune\"", "\"Oni\"",
			"\"\"", "null", "1", "[]", "\"oni \"", "\" oni\"", "\"oni\\n\"", "\"\\u006fni\"", "\"o\\u006Eni\"", "\"human-guises\"", "\"fox\"" };
		foreach (string y in yokai)
			yield return ("yokai " + y, Raw(Id(), "{ \"yokai\": " + y + ", \"row_min\": 1, \"row_max\": 9 }"));
		string ok = "{ \"row_min\": 1, \"row_max\": 3 }";
		foreach (var (what, later) in new[]
		{
			("duplicate row_min, bad first", "{ \"row_min\": 0, \"row_min\": 1, \"row_max\": 3 }"),
			("duplicate row_min, bad last", "{ \"row_min\": 1, \"row_min\": 0, \"row_max\": 3 }"),
			("duplicate row_max, inverted last", "{ \"row_min\": 2, \"row_max\": 3, \"row_max\": 1 }"),
			("duplicate row_max, inverted first", "{ \"row_min\": 2, \"row_max\": 1, \"row_max\": 3 }"),
			("duplicate yokai, elder last", "{ \"yokai\": \"oni\", \"yokai\": \"elder-oni\", \"row_min\": 1, \"row_max\": 3 }"),
			("duplicate yokai, elder first", "{ \"yokai\": \"elder-oni\", \"yokai\": \"oni\", \"row_min\": 1, \"row_max\": 3 }"),
			("float first, int last", "{ \"row_min\": 1.5, \"row_min\": 1, \"row_max\": 3 }"),
			("int first, float last", "{ \"row_min\": 1, \"row_min\": 1.5, \"row_max\": 3 }"),
			("unknown key", "{ \"rows\": 3, \"row_min\": 1, \"row_max\": 3 }"),
			("empty object", "{}"), ("row_min == row_max", "{ \"row_min\": 3, \"row_max\": 3 }"),
			("max rows", "{ \"row_min\": 2147483647, \"row_max\": 2147483647 }"),
			("1 to max", "{ \"row_min\": 1, \"row_max\": 2147483647 }"),
		})
			yield return (what, Raw(Id(), later));
		foreach (var (what, text, ph, title) in new (string, string, string, string?)[]
		{
			("plain text, no placeholder", "Plain.", "[]", null), ("plain text, yokai listed", "Plain.", "[\"yokai\"]", null),
			("spaced slot", "{ yokai } x", "[\"yokai\"]", null), ("capital slot", "{Yokai} x", "[\"yokai\"]", null), ("empty slot", "{} x", "[]", null),
			("double braces", "{{yokai}} x", "[\"yokai\"]", null), ("open brace", "{yokai x", "[]", null), ("close brace", "yokai} x", "[]", null),
			("slot twice", "{yokai} and {yokai}", "[\"yokai\"]", null), ("move, listed", "{move} x", "[\"move\"]", null), ("move, both listed", "{yokai} {move}", "[\"yokai\", \"move\"]", null),
			("move, unlisted", "{move} x", "[]", null), ("yokai text, move listed", "{yokai} x", "[\"yokai\", \"move\"]", null),
			("escaped braces", "\\u007byokai\\u007d x", "[\"yokai\"]", null), ("escaped move", "\\u007bmove\\u007d x", "[]", null),
			("duplicate listing", "{yokai} x", "[\"yokai\", \"yokai\"]", null), ("title yokai", "{yokai} x", "[\"yokai\"]", "\"{yokai} falls\""),
			("title move", "{yokai} x", "[\"yokai\"]", "\"{move} falls\""), ("title only yokai", "Plain.", "[\"yokai\"]", "\"{yokai} falls\""),
			("title capital", "Plain.", "[]", "\"{Yokai} falls\""), ("title empty slot", "Plain.", "[]", "\"{} falls\""),
			("unknown placeholder", "{yokai} {tanuki}", "[\"yokai\", \"tanuki\"]", null), ("slot with newline", "{yo\\nkai} x", "[]", null),
		})
			yield return (what, Raw(Id(), ok, text, ph, title));
		yield return ("later_loss duplicated at card level, bad first", Raw(Id(), "{ \"row_min\": 0, \"row_max\": 1 }", cardTail: ", \"later_loss\": { \"row_min\": 1, \"row_max\": 3 }"));
		yield return ("later_loss duplicated at card level, bad last", Raw(Id(), "{ \"row_min\": 1, \"row_max\": 3 }", cardTail: ", \"later_loss\": { \"row_min\": 3, \"row_max\": 1 }"));
	}

	/// <summary>Runs validate_data.py once over a folder of cards; returns the card ids it names in an error line, or null if it cannot run.</summary>
	static HashSet<string>? ValidatorRefuses(string storyDir)
	{
		foreach (string exe in new[] { "python", "python3" })
		{
			try
			{
				var psi = new ProcessStartInfo(exe) { WorkingDirectory = ContentPaths.Current.Root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
				psi.ArgumentList.Add(Path.Combine(ContentPaths.Current.Root, "tools", "validate_data.py")); psi.ArgumentList.Add(storyDir);
				using var p = Process.Start(psi)!;
				var err = p.StandardError.ReadToEndAsync();
				string output = p.StandardOutput.ReadToEnd();
				p.WaitForExit();
				if (p.ExitCode > 1) { GD.Print("LaterLossShapeBreakerProbes: validator did not run: " + err.Result); continue; }
				return output.Split('\n').Where(l => l.Contains(".json:")).Select(l => Path.GetFileName(l[..l.IndexOf(".json:", StringComparison.Ordinal)] + ".json")).ToHashSet();
			}
			catch (System.ComponentModel.Win32Exception) { }
		}
		return null;
	}

	// The validator passes the file, so the game must load it and the lose screen must fill it (S3/S4); a card the validator
	// refuses and the loader takes is only the validator being stricter, not a finding.
	[Test]
	public static void EveryShapeTheValidatorPasses_LoadsAndFillsAtTheLoss()
	{
		string tmp = Path.Combine(Path.GetTempPath(), "yok212-shapes-" + Guid.NewGuid().ToString("N"));
		string dir = Path.Combine(tmp, "data", "story");
		Directory.CreateDirectory(dir);
		var bad = new List<string>();
		int passed = 0, strictOnly = 0;
		try
		{
			var shapes = Shapes().ToList();
			foreach (var (_, json) in shapes)
				File.WriteAllText(Path.Combine(dir, System.Text.RegularExpressions.Regex.Match(json, "\"id\": \"([^\"]+)\"").Groups[1].Value + ".json"), json);
			var refused = ValidatorRefuses(dir);
			if (refused is null) { GD.Print("LaterLossShapeBreakerProbes: no python, skipped"); return; }
			var screen = FightScene.LoadStory()[StoryLibrary.LoseScreenId];
			foreach (var (what, json) in shapes)
			{
				string id = System.Text.RegularExpressions.Regex.Match(json, "\"id\": \"([^\"]+)\"").Groups[1].Value;
				StoryCardData? card = null; string? loadError = null;
				try { card = StoryLibrary.Parse(json); } catch (Exception e) { loadError = e.GetType().Name + ": " + e.Message; }
				if (refused.Contains(id + ".json"))
				{
					if (card is not null) strictOnly++;
					continue;
				}
				passed++;
				if (card is null) { bad.Add($"[{what}] validate_data passes, Parse fails: {loadError}"); continue; }
				if (card.LaterLoss is not { } l) { bad.Add($"[{what}] validate_data passes a later_loss card the loader reads as none"); continue; }
				try
				{
					var lib = new StoryLibrary(new[] { screen, card });
					var (title, text) = lib.LoseScreen("Elder Oni", l.Yokai ?? "kitsune", l.RowMin);
					if (lib.LaterLossCard(l.Yokai ?? "kitsune", l.RowMin).Id != id) bad.Add($"[{what}] passes, loads, is not chosen on its own first row {l.RowMin} for '{l.Yokai ?? "kitsune"}'");
					if (text.Contains("{yokai}") || text.Contains("{move}") || (title ?? "").Contains("{yokai}") || (title ?? "").Contains("{move}"))
						bad.Add($"[{what}] raw slot on the screen: {title} / {text}");
				}
				catch (Exception e) { bad.Add($"[{what}] passes and loads, the loss throws {e.GetType().Name}: {e.Message}"); }
			}
			GD.Print($"LaterLossShapeBreakerProbes: {shapes.Count} shapes, {passed} passed the validator, {strictOnly} refused by it but taken by the loader");
			Assert.True(passed > 10, "the validator passed fewer shapes than the controls: the run was not read");
		}
		finally { try { Directory.Delete(tmp, true); } catch (IOException) { } }
		Assert.True(bad.Count == 0, "\n" + string.Join("\n", bad));
	}

	static void LoseTo(FightScene s)
	{
		s.TwistCard?.Continue();
		for (int t = 0; t < 900 && s.Match.Phase != MatchPhase.Over; t++) s.Step(FighterInput.None, new FighterInput(t % 2 == 0 ? InputBits.DebugStrike : InputBits.None));
		Assert.Equal(1, s.Match.Winner, "the opponent won");
	}

	// The dev jump page's lose screen is what a real loss in the same dev fight shows (same yokai, same row), for every setup that can be built.
	[Test]
	public static void DevJumpLosePreview_IsWhatARealLossInTheSameDevFightShows(Godot.Node runner)
	{
		var pool = FightScene.LoadAbilityPool();
		var story = FightScene.LoadStory();
		var bad = new List<string>();
		foreach (string opp in DevJump.OpponentIds)
			foreach (bool elder in new[] { false, true })
			{
				var setup = new DevFightSetup(opp, Elder: elder);
				var preview = DevJump.LoseCardOf(setup, story); // must never throw, even for a combination the fight cannot build (the page holds any setup)
				if (opp == DevJump.TanukiId && elder) continue;
				if (elder && DevJump.ElderOf(opp) is null) continue;
				var jump = DevJump.BuildFight(setup, pool);
				Assert.True(jump.Session.Enter(jump.NodeId).Ok, "enter the dev fight");
				var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
				scene.ExternalDrive = true; scene.Session = jump.Session; scene.FirstMeeting = true;
				runner.AddChild(scene);
				try
				{
					var hud = scene.GetNode<FightHud>("Hud");
					hud.SetLoseText(null, "stale");
					LoseTo(scene);
					if (hud.LoseText != preview.Text) bad.Add($"{opp}{(elder ? " elder" : "")} (row {scene.Node.Row}): preview '{preview.Text}' vs loss '{hud.LoseText}'");
					if (preview.Title is not null && hud.LoseTitle != preview.Title) bad.Add($"{opp}{(elder ? " elder" : "")}: title '{preview.Title}' vs '{hud.LoseTitle}'");
				}
				finally { scene.QueueFree(); }
			}
		Assert.True(bad.Count == 0, "\n" + string.Join("\n", bad));
	}

	// The preview's yokai name and band for each setup, written out from S3's bands (rows 1-3, 4-6, 7-9) and R6's names, not read back from the code.
	[Test]
	public static void DevJumpLosePreview_NamesTheYokaiAndTheBandOfItsRow()
	{
		var story = FightScene.LoadStory();
		foreach (var (opp, elder, name, band) in new[]
		{
			("kitsune", false, "Kitsune", "lose-early"), ("oni", false, "Oni", "lose-early"), ("kappa", false, "Kappa", "lose-early"),
			("kitsune", true, "Nine Tailed Kitsune", "lose-mid"), ("oni", true, "Elder Oni", "lose-mid"), ("kappa", true, "Elder Kappa", "lose-mid"),
			("tanuki", false, "Tanuki", "lose-late"), ("tanuki", true, "Tanuki", "lose-late"),
		})
		{
			string shown = DevJump.LoseCardOf(new DevFightSetup(opp, Elder: elder), story).Text;
			Assert.True(shown.Contains(name) || (name == "Nine Tailed Kitsune" && shown.Contains("Nine")), $"{opp} elder={elder}: names '{name}': {shown}");
			string template = story[band].Text.Replace("{yokai}", "");
			Assert.True(shown.Replace(name, "").Contains(template.Trim()) || shown.Contains(template.Trim().Split("{")[0]), $"{opp} elder={elder}: the {band} text: {shown}");
		}
	}
}
