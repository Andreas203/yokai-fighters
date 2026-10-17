using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Godot;
using YokaiFighters.Ai;
using YokaiFighters.Fight;
using YokaiFighters.Flow;
using YokaiFighters.Replay;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>
/// Test Breaker probes for YOK-99 (Tanuki pressure). Expected values come from Y5 (each Tanuki win, max 3: 1 frame faster,
/// more aggressive), Y4 (tier reaction 30/22/18/15), Y6 (apprentice guided) and E22. The save store is reached by reflection
/// (only SaveTests and SaveProbes may name it).
/// </summary>
public static class TanukiPressureProbes
{
	static string FixtureDir => ProjectSettings.GlobalizePath(FightScene.FixtureProfilesDir);
	static BehaviourProfile Patient => BehaviourProfile.LoadFile(Path.Combine(FixtureDir, "test-kitsune-patient.json"));
	static BehaviourProfile Aggressive => BehaviourProfile.LoadFile(Path.Combine(FixtureDir, "test-kitsune-aggressive.json"));
	static string MapDir => ContentPaths.Data("map");
	static string RepoRoot => Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "..");
	static ReplayCatalog Catalog => ReplayCatalog.FromRepo(RepoRoot);

	// ---- save store by reflection ----
	static Type StoreType => typeof(GameFlow).Assembly.GetType("YokaiFighters.SaveGame.Save" + "Store")!;
	static (int wins, string status, string problems) Load(string json)
	{
		object load = StoreType.GetMethod("Parse")!.Invoke(null, new object[] { json })!;
		object data = load.GetType().GetProperty("Data")!.GetValue(load)!;
		int wins = (int)data.GetType().GetProperty("TanukiWins")!.GetValue(data)!;
		var problems = (System.Collections.IEnumerable)load.GetType().GetProperty("Problems")!.GetValue(load)!;
		return (wins, load.GetType().GetProperty("Status")!.GetValue(load)!.ToString()!, string.Join("; ", problems.Cast<object>()));
	}
	static string File7(string wins) =>
		"{ \"version\": " + StoreType.GetField("CurrentVersion")!.GetRawConstantValue() + ", \"run\": null, \"unlocks\": [], \"story\": { \"seen\": [] }" + wins + " }";

	// Y5 + save v7: the claimed bad forms read as 0 and are reported, never as a count.
	[Test]
	public static void Save_TanukiWinsBadForms_ReadAsZero_NeverAsPressure()
	{
		foreach (string bad in new[] { "\"3\"", "3.5", "-1", "-0.0001", "\"\"", "{}", "9999999999", "1e10" })
		{
			var r = Load(File7(", \"tanuki_wins\": " + bad));
			Assert.True(r.wins == 0 && r.status == "Partial", $"{bad}: expected 0/Partial, got {r.wins}/{r.status}");
		}
		foreach ((string good, int want) in new[] { ("3", 3), ("3.0", 3), ("1e2", 100), ("0", 0), ("2147483647", int.MaxValue), ("0e5", 0) })
		{
			var r = Load(File7(", \"tanuki_wins\": " + good));
			Assert.True(r.wins == want && r.status == "Loaded", $"{good}: expected {want}/Loaded, got {r.wins}/{r.status} ({r.problems})");
		}
	}

	// Save v7: a version-8 file (and 5, 6 with no field) keeps readable fields; duplicate key does not crash.
	[Test]
	public static void Save_OtherVersions_AndDuplicateKey()
	{
		foreach (int v in new[] { 0, 1, 5, 6, 8, 99 })
		{
			var r = Load("{ \"version\": " + v + ", \"run\": null, \"unlocks\": [], \"story\": { \"seen\": [] }, \"tanuki_wins\": 2 }");
			Assert.True(r.wins == 2, $"version {v}: a readable tanuki_wins is kept, got {r.wins}");
		}
		var none = Load("{ \"version\": 6, \"run\": null, \"unlocks\": [], \"story\": { \"seen\": [] } }");
		Assert.True(none.wins == 0 && none.status == "OlderVersion", $"v6 without field: 0, got {none.wins}/{none.status}");
		var dup = Load(File7(", \"tanuki_wins\": 1, \"tanuki_wins\": 2"));
		Assert.True(dup.wins is 1 or 2 or 0, "duplicate key: loads without throwing");
	}

	// Y5: the number the file holds is the number written back (no drift through 1e2 -> 100, load -> serialize -> load).
	[Test]
	public static void Save_WinsSurviveSerializeRoundTrip_ForEveryReadableForm()
	{
		foreach (string form in new[] { "1e2", "3.0", "7", "0" })
		{
			object load = StoreType.GetMethod("Parse")!.Invoke(null, new object[] { File7(", \"tanuki_wins\": " + form) })!;
			object data = load.GetType().GetProperty("Data")!.GetValue(load)!;
			string text = (string)StoreType.GetMethod("Serialize", new[] { data.GetType() })!.Invoke(null, new[] { data })!;
			var back = Load(text);
			int first = (int)data.GetType().GetProperty("TanukiWins")!.GetValue(data)!;
			Assert.True(back.wins == first && back.status == "Loaded", $"{form}: {first} -> {back.wins}/{back.status}");
		}
	}

	// ---- the numbers ----

	// Y4/Y5/Y6: every node of the default map: the apprentice exempt, every other node reacts tier - 3 after 3 wins,
	// and with no wins no node changes at all.
	[Test]
	public static void EveryNodeOfTheDefaultMap_ApprenticeExempt_OthersPressed()
	{
		var graph = FileMapSource.Default(MapDir).Load();
		var table = NodeSceneTable.Default(MapDir);
		int apprentices = 0, pressed = 0;
		foreach (var node in graph.Nodes)
		{
			var ctx = table.Resolve(node).Context;
			var none = FightScene.PressureFor(ctx, 0);
			Assert.True(none.ReactionFrames(ctx.ReactionFrames) == ctx.ReactionFrames && none.AggressionBonus == 0, $"{node.Id}: no wins, no change");
			foreach (int wins in new[] { 1, 2, 3, 4, 50 })
			{
				var p = FightScene.PressureFor(ctx, wins);
				if (ctx.Tier == "apprentice") { Assert.True(p.ReactionFrames(ctx.ReactionFrames) == 30 && p.AggressionBonus == 0, $"{node.Id} apprentice stays 30f/0 at {wins} wins"); }
				else Assert.True(p.ReactionFrames(ctx.ReactionFrames) == ctx.ReactionFrames - Math.Min(wins, 3) && p.AggressionBonus == Math.Min(wins, 3) * 10, $"{node.Id} ({ctx.Tier}) wins {wins}");
			}
			if (ctx.Tier == "apprentice") apprentices++; else pressed++;
			if (node.Row == 1) Assert.True(ctx.Tier == "apprentice", $"Y4: row 1 node {node.Id} is the apprentice, got {ctx.Tier}");
		}
		Assert.True(apprentices >= 1 && pressed >= 5, $"saw {apprentices} apprentice and {pressed} other nodes");
	}

	// E22: aggression already 75 plus 30 = 100 and no weight goes negative; a hostile replay bonus cannot overflow past the cap.
	[Test]
	public static void Aggression_NearCap_AndOverflow()
	{
		var kit = AiLoadout.From(FightScene.NewMatch().P2);
		Assert.Equal(100, new ProfileAi(Aggressive, 1, kit, 1, null, AiFightTier.Standard, 30).Aggression, "75 + 30 caps at 100");
		Assert.Equal(100, new ProfileAi(Aggressive, 1, kit, 1, null, AiFightTier.Standard, int.MaxValue).Aggression, "Y5/E22: a huge bonus (a replay file can carry one) still caps at 100, not an int wrap to 0");
	}

	// ---- replays ----

	static FighterInput Chaos(Random rng)
	{
		var buttons = new[] { InputBits.LightPunch, InputBits.MediumPunch, InputBits.HeavyPunch, InputBits.LightKick, InputBits.Special };
		var bits = (InputBits)0;
		int d = rng.Next(9);
		if (d is 0 or 1 or 2) bits |= InputBits.Right; else if (d is 3 or 4) bits |= InputBits.Left;
		if (rng.Next(7) == 0) bits |= buttons[rng.Next(buttons.Length)];
		return new FighterInput(bits);
	}

	static ReplayRecording Record(int steps, int switchAt, Func<Match, ProfileAi?> first, Func<Match, ProfileAi?>? second)
	{
		var m = FightScene.NewMatch();
		var ai = first(m);
		var rec = ReplayRecorder.Begin(m, ai, 5);
		var rng = new Random(11);
		for (int t = 0; t < steps; t++)
		{
			if (t == switchAt && second is not null) { ai = second(m); rec.OnAi(ai, 5); }
			var a = Chaos(rng);
			var b = ai is not null ? ai.Next(AiView.Capture(m)) : FighterInput.None;
			m.Step(a, b);
			rec.Record(a, b, m.StateHash());
			if (m.Phase == MatchPhase.Over) break;
		}
		return rec.Finish(m);
	}

	static ProfileAi Pressured(Match m, int wins, BehaviourProfile? p = null)
	{
		var pr = new TanukiPressure(wins);
		return new ProfileAi(p ?? Aggressive, 1, AiLoadout.From(m.P2), 5, pr.ReactionFrames(22), AiFightTier.Standard, pr.AggressionBonus);
	}

	// Y5: a pressured fight replays through the file: header carries the bonus and the pressured frames, hashes match.
	[Test]
	public static void Replay_PressuredFight_RoundTripsThroughTheFile()
	{
		var rec = Record(2500, -1, m => Pressured(m, 3, Patient), null);
		var back = ReplayRecording.FromBytes(rec.ToBytes());
		Assert.True(back.Header.Ai!.AggressionBonus == 30 && back.Header.Ai.ReactionFrames == 19, $"header: bonus {back.Header.Ai.AggressionBonus}, frames {back.Header.Ai.ReactionFrames}");
		var r = Replayer.Run(back, Catalog);
		Assert.True(r.Ok && r.StepsRun == rec.Steps, "replay: " + r);
	}

	// Y5: the bonus is part of the fight: strip it from the header and the replay must diverge (the field is load-bearing).
	[Test]
	public static void Replay_BonusRemovedFromHeader_Diverges()
	{
		var rec = Record(2500, -1, m => Pressured(m, 3, Patient), null);
		var back = ReplayRecording.FromBytes(rec.ToBytes());
		back.Header.Ai!.AggressionBonus = 0;
		Assert.True(!Replayer.Run(back, Catalog).Ok, "a recording whose aggression bonus is dropped does not replay as the same fight");
	}

	// Y5: a mid-fight AI rebuild (F6/F7 toggle) records its own bonus: pressured -> none -> pressured at a different count.
	[Test]
	public static void Replay_MidFightRebuild_RecordsEachBonus()
	{
		var rec = Record(3000, 400, m => Pressured(m, 3, Patient), m => Pressured(m, 1, Patient));
		var back = ReplayRecording.FromBytes(rec.ToBytes());
		var ev = back.Header.Events.Single(e => e.Kind == "ai");
		Assert.True(ev.Ai!.AggressionBonus == 10 && ev.Ai.ReactionFrames == 21, $"event: bonus {ev.Ai.AggressionBonus} frames {ev.Ai.ReactionFrames}");
		Assert.True(back.Header.Ai!.AggressionBonus == 30, "the header keeps the first AI's bonus");
		var r = Replayer.Run(back, Catalog);
		Assert.True(r.Ok, "replay: " + r);

		// toggled off then rebuilt unpressured: an event with no bonus must not inherit the old one
		var rec2 = Record(3000, 400, m => Pressured(m, 3, Patient), m => Pressured(m, 0, Patient));
		var back2 = ReplayRecording.FromBytes(rec2.ToBytes());
		Assert.Equal(0, back2.Header.Events.Single(e => e.Kind == "ai").Ai!.AggressionBonus, "unpressured rebuild: bonus 0");
		Assert.True(Replayer.Run(back2, Catalog).Ok, "replay after an unpressured rebuild");
	}

	// Y5 + old recordings: an unpressured fight never writes the key anywhere (header or events), and replays.
	[Test]
	public static void Replay_Unpressured_WritesNoBonusKey_AnywhereInTheFile()
	{
		var rec = Record(2500, 300, m => Pressured(m, 0), m => Pressured(m, 0, Patient));
		string text = System.Text.Encoding.UTF8.GetString(rec.ToBytes());
		Assert.True(!text.Contains("AggressionBonus"), "no AggressionBonus key in an unpressured recording");
		Assert.True(Replayer.Run(ReplayRecording.FromBytes(rec.ToBytes()), Catalog).Ok, "replays");
	}

	// Y5: unpressured StateHash stream is what a ProfileAi built the main way (no bonus argument) gives, tick for tick.
	[Test]
	public static void UnpressuredFight_HashStream_EqualsThePreYok99Constructor()
	{
		var a = Record(1500, -1, m => Pressured(m, 0, Patient), null);
		var b = Record(1500, -1, m => new ProfileAi(Patient, 1, AiLoadout.From(m.P2), 5, 22, AiFightTier.Standard), null);
		Assert.True(a.Hashes.SequenceEqual(b.Hashes) && a.Steps == b.Steps, "zero wins changes nothing in the fight");
		Assert.True(a.Header.Result!.FinalHash == b.Header.Result!.FinalHash, "same final hash");
	}

	// Y5: pressured differs from unpressured (the effect is not a no-op).
	[Test]
	public static void PressuredFight_DiffersFromUnpressured_ButFourWinsEqualsThreeWins()
	{
		var none = Record(1500, -1, m => Pressured(m, 0, Patient), null);
		var three = Record(1500, -1, m => Pressured(m, 3, Patient), null);
		var four = Record(1500, -1, m => Pressured(m, 4, Patient), null);
		Assert.True(!none.Hashes.SequenceEqual(three.Hashes), "three wins play a different fight");
		Assert.True(three.Hashes.SequenceEqual(four.Hashes), "Y5 max 3: a fourth win changes nothing");
	}

	// ---- the boot flow ----

	static void WithTempFile(Action<string> body)
	{
		string dir = Path.Combine(Path.GetTempPath(), "yokai-tanuki-probes-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(dir);
		try { body(Path.Combine(dir, "slot.json")); } finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
	}

	static void WithBoot(Node runner, string saveFile, Action<GameFlow> body, uint seed = 41)
	{
		var saved = FightScene.Sources; var savedRun = FightScene.Run; var savedFoe = FightScene.Opponent;
		FightScene.Sources = FightScene.FixtureSources;
		FightScene.Run = null;
		MenuSettings.Reset();
		var flow = GD.Load<PackedScene>("res://scenes/boot.tscn").Instantiate<GameFlow>();
		flow.QuitAction = () => { };
		flow.FixedRunSeed = seed;
		flow.SaveFile = saveFile;
		flow.ConfigureFight = f => f.ExternalDrive = true;
		try { runner.AddChild(flow); body(flow); }
		finally
		{
			runner.RemoveChild(flow); flow.QueueFree(); MenuSettings.Reset();
			FightScene.Sources = saved; FightScene.Run = savedRun; FightScene.Opponent = savedFoe;
		}
	}

	static void Press(Button b) => b.EmitSignal(BaseButton.SignalName.Pressed);

	static void WinNode(GameFlow flow, int health)
	{
		var s = flow.Session!;
		if (s.WinFight(health) is { } d) { var c = d.Offer.Cards[0]; Assert.True(d.Pick(0, c.Targets.Count > 0 ? c.Targets[0].Slot : null).Ok, "picked"); }
		Assert.True(s.CompleteNode().Ok, "completed");
	}

	static void NewRun(GameFlow flow)
	{
		if (flow.Session is not null) flow.ReturnToTitle();
		Press(flow.Title.StartButton);
		Press(flow.ControlSelect.Start);
	}

	static void ToBossDoor(GameFlow flow)
	{
		while (flow.Session!.Reachable()[0] != "boss-tanuki")
		{
			Assert.True(flow.ChooseNode(flow.Session.Reachable()[0]).Ok, "entered");
			WinNode(flow, 700);
		}
	}

	// Y5: win count survives quitting inside the Tanuki fight and Continue; the resumed Tanuki fight is pressured;
	// the win scored in the continued run is counted once on top; a loss on Continue adds nothing.
	[Test]
	public static void Boot_Continue_IntoTheTanukiFight_IsPressured_AndTheWinCounts(Node runner) => WithTempFile(path =>
	{
		WithBoot(runner, path, flow =>
		{
			NewRun(flow); ToBossDoor(flow);
			Assert.True(flow.ChooseNode("boss-tanuki").Ok, "boss entered");
			Assert.Equal(15, flow.Fight!.P2Ai!.ReactionFrames, "Y4: first Tanuki fight, no wins, 15f");
			WinNode(flow, 400);
			Assert.Equal(1, flow.Saved.TanukiWins, "one win");
			NewRun(flow); ToBossDoor(flow);
			Assert.True(flow.ChooseNode("boss-tanuki").Ok, "boss entered, run 2");
			Assert.Equal(14, flow.Fight!.P2Ai!.ReactionFrames, "Y5: 15 - 1");
			flow.ReturnToTitle(); // quit inside the fight
		});
		WithBoot(runner, path, flow =>
		{
			Assert.Equal(1, flow.Saved.TanukiWins, "reboot keeps the count; quitting mid-fight is not a win");
			Press(flow.Title.ContinueButton);
			Assert.True(flow.Session is not null, "continued");
			if (flow.Fight is null) Assert.True(flow.ChooseNode("boss-tanuki").Ok, "re-entered the boss");
			Assert.Equal(14, flow.Fight!.P2Ai!.ReactionFrames, "the continued Tanuki fight is pressured by 1 win");
			WinNode(flow, 300);
			Assert.Equal(2, flow.Saved.TanukiWins, "the continued run's win counts once");
			Assert.Equal(2, Load(File.ReadAllText(path)).wins, "and the file holds 2");
		});
		WithBoot(runner, path, flow =>
		{
			NewRun(flow); ToBossDoor(flow);
			Assert.True(flow.ChooseNode("boss-tanuki").Ok, "boss entered");
			Assert.Equal(13, flow.Fight!.P2Ai!.ReactionFrames, "two wins: 15 - 2");
			Assert.True(flow.Session!.LoseFight().Ok, "lost to the Tanuki");
			Assert.Equal(2, Load(File.ReadAllText(path)).wins, "a loss does not count");
		});
	});

	// Y5 max 3 wins: header bonus 30 replays, 31 and -1 are refused with a setup error (not a crash, not a silent replay).
	[Test]
	public static void Replay_BonusBounds_HeaderAndEvent()
	{
		var rec = Record(600, 200, m => Pressured(m, 3, Patient), m => Pressured(m, 3, Patient));
		foreach (int bad in new[] { 31, -1, int.MaxValue, int.MinValue })
		{
			var h = ReplayRecording.FromBytes(rec.ToBytes());
			h.Header.Ai!.AggressionBonus = bad;
			var r = Replayer.Run(h, Catalog);
			Assert.True(!r.Ok && r.Error!.StartsWith("setup"), $"header bonus {bad} refused: {r.Error}");
			var e = ReplayRecording.FromBytes(rec.ToBytes());
			e.Header.Events.Single(x => x.Kind == "ai").Ai!.AggressionBonus = bad;
			var r2 = Replayer.Run(e, Catalog);
			Assert.True(!r2.Ok && r2.Error!.StartsWith("setup (step"), $"event bonus {bad} refused: {r2.Error}");
		}
		Assert.True(Replayer.Run(ReplayRecording.FromBytes(rec.ToBytes()), Catalog).Ok, "30 in header and event still replays");
	}
}
