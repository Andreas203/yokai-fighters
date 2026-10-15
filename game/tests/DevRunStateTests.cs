using System;
using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.Dev;
using YokaiFighters.Fight;
using YokaiFighters.Flow;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-126: the dev menu's run-state editor. <see cref="DevRunEditor"/> on the fixture pool and the default map (invalid
/// builds refused with a reason, the copy target following the build, depth / seed / coins / health), the preset file
/// (round trip, built-ins, a broken file), and the page in the boot scene (apply on the title and the map in the dev
/// slot with the player's file untouched; refused mid-fight and with dev mode off).
/// </summary>
public static class DevRunStateTests
{
	static readonly DevModeState Debug = new(true, false), DevOn = new(true, true);
	const string Sw = "test-spirit-wave", Rt = "test-rising-talisman", Fox = "test-foxfire", Wisp = "test-will-o-wisp", Patience = "test-fox-patience";

	static string RepoRoot => Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "..");
	static string MapDir => ContentPaths.Data("map");

	static DevRunEditor Editor()
	{
		var pool = AbilityPool.LoadFixtures(RepoRoot);
		var graph = FileMapSource.Default(MapDir).Load();
		return new DevRunEditor(pool, graph, NodeSceneTable.Default(MapDir));
	}

	static DevRunBuild Starters(uint seed = 7) =>
		new(new[] { new DevSlotEdit(SpecialSlot.A, Sw), new DevSlotEdit(SpecialSlot.B, Rt) }, 0, RunState.MaxHealth, 0, seed);

	static void Refused(DevRunEditor ed, DevRunBuild b, string needle, string what)
	{
		var c = ed.Check(b);
		Assert.True(!c.Ok && c.Session == null && c.Copy == null, what + ": refused, nothing built");
		Assert.True(c.Refusal!.Contains(needle, StringComparison.OrdinalIgnoreCase), $"{what}: the reason names it ('{needle}'): {c.Refusal}");
	}

	static void WithMode(DevModeState state, Action body)
	{
		DevMode.Configure(state);
		try { body(); }
		finally { DevMode.ResetToLaunch(); }
	}

	static void WithTempDir(Action<string> body)
	{
		string dir = Path.Combine(Path.GetTempPath(), "yokai-devrun-tests-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(dir);
		try { body(dir); }
		finally { try { Directory.Delete(dir, recursive: true); } catch (IOException) { } }
	}

	// ---- the editor (pure) ----

	[Test]
	public static void InvalidBuilds_AreRefusedWithAReason()
	{
		var ed = Editor();
		Assert.True(ed.Check(Starters()).Ok, "the starters at Lv 1 are a valid build: " + ed.Check(Starters()).Refusal);
		Refused(ed, Starters() with { Slots = Array.Empty<DevSlotEdit>() }, "T5", "no special at all");
		Refused(ed, Starters().WithSlot(SpecialSlot.C, new DevSlotEdit(SpecialSlot.C, "no-such-move")), "no special 'no-such-move'", "a special not in the data");
		Refused(ed, Starters().WithSlot(SpecialSlot.D, new DevSlotEdit(SpecialSlot.D, Fox)), "belongs in slot C", "a special outside its own slot (A3)");
		Refused(ed, Starters().WithSlot(SpecialSlot.A, new DevSlotEdit(SpecialSlot.A, Sw, 4)), "no level 4", "level 4 (A3/A4)");
		Refused(ed, Starters().WithSlot(SpecialSlot.A, new DevSlotEdit(SpecialSlot.A, Sw, 0)), "no level 0", "level 0");
		Refused(ed, Starters().WithSlot(SpecialSlot.B, new DevSlotEdit(SpecialSlot.B, Rt, 1, Wisp)), "can't attach", "a projectile modifier on a non-projectile (A2)");
		Refused(ed, Starters().WithSlot(SpecialSlot.B, new DevSlotEdit(SpecialSlot.B, Rt, 1, "nope")), "no modifier 'nope'", "a modifier not in the data");
		Refused(ed, Starters().WithSlot(SpecialSlot.A, new DevSlotEdit(SpecialSlot.A, Sw, 1, null, 0, Borrowed: true)), "R6", "a borrowed starter");
		Refused(ed, Starters().WithSlot(SpecialSlot.A, new DevSlotEdit(SpecialSlot.A, Sw, 1, null, 0, false, ModifierBorrowed: true)), "no modifier to be borrowed", "a borrowed modifier that isn't there");
		Refused(ed, Starters() with { Coins = -1 }, "R8", "negative coins");
		Refused(ed, Starters() with { Health = 0 }, "outside 1-1000", "health 0");
		Refused(ed, Starters() with { Health = 1001 }, "outside 1-1000", "health over the cap");
		Refused(ed, Starters() with { Depth = -1 }, "negative", "negative depth");
		Refused(ed, Starters() with { Depth = ed.MaxDepth + 1 }, "T1", "a depth past the Tanuki's node");
		var tied = Starters().WithSlot(SpecialSlot.A, new DevSlotEdit(SpecialSlot.A, Sw, 2, null, 1)).WithSlot(SpecialSlot.B, new DevSlotEdit(SpecialSlot.B, Rt, 2, null, 1));
		Refused(ed, tied, "share level-up order", "two slots with one T3 rank");
		Refused(ed, Starters().WithSlot(SpecialSlot.A, new DevSlotEdit(SpecialSlot.A, Sw, 1, null, 2)), "doesn't match", "a T3 rank on a Lv 1 special");
	}

	[Test]
	public static void ValidBuild_MakesTheRunItDescribes()
	{
		var ed = Editor();
		var b = new DevRunBuild(new[]
		{
			new DevSlotEdit(SpecialSlot.A, Sw, 2, Wisp), new DevSlotEdit(SpecialSlot.B, Rt, 1, Patience),
			new DevSlotEdit(SpecialSlot.C, Fox, 3),
		}, 175, 420, 3, 12345);
		var c = ed.Check(b);
		Assert.True(c.Ok, "valid: " + c.Refusal);
		var s = c.Session!;
		Assert.True(s.Run[SpecialSlot.A] is { Level: 2, Modifier.Id: Wisp } && s.Run[SpecialSlot.B] is { Level: 1, Modifier.Id: Patience }
			&& s.Run[SpecialSlot.C] is { Level: 3, Modifier: null } && s.Run[SpecialSlot.D] is null, "slots, levels, modifiers");
		Assert.True(s.Run.Coins == 175 && s.Run.Health == 420 && s.Seed == 12345u, "coins, health, draft seed");
		Assert.True(s.Map.Visited.Count == 3 && !s.Map.InNode && s.Reachable().All(id => s.Map.Graph.Node(id).Row == s.Map.Graph.Rows[3]), "three rows done, row 4 open");
		Assert.True(s.Run.MatchConfig().P1StartHealth == 420, "the next fight starts on the set health (C1)");
		// The same build again = the same run; Capture gives back what was set.
		Assert.Equal(s.StateHash(), ed.Check(b).Session!.StateHash(), "deterministic");
		var back = DevRunEditor.Capture(s);
		Assert.Equal(s.StateHash(), ed.Check(back).Session!.StateHash(), "Capture → Check is the same run");
		Assert.True(ed.Check(b with { Seed = 1 }).Session!.StateHash() != s.StateHash(), "the seed is run state");
		// Depth keeps the editor's preferred walk while it fits.
		var keep = DevRunEditor.For(s);
		var deeper = keep.Check(b with { Depth = 5 }).Session!;
		Assert.True(deeper.Map.Visited.Take(3).SequenceEqual(s.Map.Visited), "a deeper walk extends the current one");
		Assert.True(ed.MaxDepth == ed.Graph.Rows.Count - 1, $"max depth stops before the Tanuki's row: {ed.MaxDepth} of {ed.Graph.Rows.Count}");
		Assert.True(ed.Check(b with { Depth = ed.MaxDepth }).Ok, "the deepest depth is valid");
	}

	[Test]
	public static void CopyTarget_FollowsTheBuild()
	{
		var ed = Editor();
		CopyTarget Copy(DevRunBuild b) => ed.Check(b).Copy ?? throw new AssertFailed("refused: " + ed.Check(b).Refusal);
		Assert.True(Copy(Starters()) is { Slot: SpecialSlot.A, Level: 1 } c0 && c0.Data.Id == Sw, "all Lv 1: the lower slot (T3)");
		var rt2 = Starters().WithSlot(SpecialSlot.B, new DevSlotEdit(SpecialSlot.B, Rt, 2));
		Assert.True(Copy(rt2).Data.Id == Rt && Copy(rt2).Level == 2, "the highest level (T2)");
		var fox3 = rt2.WithSlot(SpecialSlot.C, new DevSlotEdit(SpecialSlot.C, Fox, 3, Patience));
		var f = Copy(fox3);
		Assert.True(f.Data.Id == Fox && f.Level == 3 && f.MoveLevel == 2, "Lv 3 Foxfire: copied at Ryo's level, running the Lv 2 tuning (T4)");
		var bothTwo = Starters().WithSlot(SpecialSlot.A, new DevSlotEdit(SpecialSlot.A, Sw, 2, null, 2)).WithSlot(SpecialSlot.B, new DevSlotEdit(SpecialSlot.B, Rt, 2, null, 1));
		Assert.True(Copy(bothTwo).Data.Id == Sw, "equal levels: the one levelled most recently (T3)");
		var swapped = bothTwo.WithSlot(SpecialSlot.A, new DevSlotEdit(SpecialSlot.A, Sw, 2, null, 1)).WithSlot(SpecialSlot.B, new DevSlotEdit(SpecialSlot.B, Rt, 2, null, 2));
		Assert.True(Copy(swapped).Data.Id == Rt, "swap the order, the target follows");
		var auto = Starters().WithSlot(SpecialSlot.A, new DevSlotEdit(SpecialSlot.A, Sw, 2)).WithSlot(SpecialSlot.B, new DevSlotEdit(SpecialSlot.B, Rt, 2));
		Assert.True(Copy(auto).Data.Id == Rt, "ranks left at 0 count in slot order: B levelled last");
		var released = new DevRunBuild(new[] { new DevSlotEdit(SpecialSlot.B, Rt) }, 0, 1000, 0, 1);
		Assert.True(Copy(released).Data.Id == Rt, "a slot emptied by hand drops out (T2)");
	}

	// ---- presets ----

	[Test]
	public static void Presets_RoundTrip_NextToTheDevSlot() => WithTempDir(dir =>
	{
		string player = Path.Combine(dir, "progress.json");
		string path = DevPresetStore.PathFor(player);
		Assert.Equal(Path.Combine(dir, DevPresetStore.FileName), path, "the preset file sits next to the dev slot");
		Assert.True(path != player && path != DevMode.SavePathFor(player), "and is neither save file");

		var ed = Editor();
		var store = new DevPresetStore(path);
		Assert.True(store.Load().Count == 0 && store.Problem == null, "no file = no presets");
		var built = ed.BuiltInPresets(9);
		Assert.True(built.Select(p => p.Name).SequenceEqual(new[] { "all starters Lv1", "Lv3 Foxfire evolved" }), "the two built-ins");
		foreach (var p in built) Assert.True(ed.Check(p.Build).Ok, p.Name + " is valid: " + ed.Check(p.Build).Refusal);
		Assert.True(ed.Check(built[1].Build).Session!.Run[SpecialSlot.C] is { Level: 3 } fx && fx.Data.Id == Fox, "Lv3 Foxfire in slot C");

		var odd = new DevRunBuild(new[]
		{
			new DevSlotEdit(SpecialSlot.A, Sw, 2, Wisp, 2), new DevSlotEdit(SpecialSlot.B, Rt, 2, null, 1),
			new DevSlotEdit(SpecialSlot.C, Fox, 1, Patience, 0, Borrowed: true, ModifierBorrowed: true),
		}, 999, 333, 4, 4_000_000_000);
		store.Save(new DevPreset("shop edge", odd));
		store.Save(built[1]);
		store.Save(new DevPreset("shop edge", odd with { Coins = 50 })); // replaces by name
		var back = store.Load();
		Assert.True(back.Select(p => p.Name).SequenceEqual(new[] { "Lv3 Foxfire evolved", "shop edge" }) && back.All(p => !p.BuiltIn), "names, replaced in place: " + string.Join(", ", back.Select(p => p.Name)));
		Assert.Equal(DevPresetStore.Serialize(new[] { built[1] with { BuiltIn = false }, new DevPreset("shop edge", odd with { Coins = 50 }) }), File.ReadAllText(path), "the file holds what was saved");
		Assert.Equal(ed.Check(odd with { Coins = 50 }).Session!.StateHash(), ed.Check(back[1].Build).Session!.StateHash(), "a loaded preset makes the same run");
		Assert.True(store.Delete("shop edge") && !store.Delete("shop edge") && store.Load().Count == 1, "delete");

		File.WriteAllText(path, "{ not json");
		Assert.True(store.Load().Count == 0 && store.Problem != null, "a broken file reads as none, with the problem");
		var only = Directory.GetFiles(dir).Select(Path.GetFileName).ToArray();
		Assert.True(only.SequenceEqual(new[] { DevPresetStore.FileName }), "nothing else written: " + string.Join(", ", only));
	});

	// ---- the page in the boot scene ----

	static void WithBoot(Node runner, string saveFile, Action<GameFlow> body)
	{
		var saved = FightScene.Sources;
		var savedRun = FightScene.Run;
		var savedFoe = FightScene.Opponent;
		FightScene.Sources = FightScene.FixtureSources;
		FightScene.Run = null;
		MenuSettings.Reset();
		var flow = GD.Load<PackedScene>("res://scenes/boot.tscn").Instantiate<GameFlow>();
		flow.QuitAction = () => { };
		flow.FixedRunSeed = 91;
		flow.SaveFile = saveFile;
		flow.ConfigureFight = f => { f.ExternalDrive = true; f.PauseEnabled = true; f.HeldBitsSource = () => InputBits.None; };
		var stage = new SubViewport { Size = new Vector2I(1920, 1080), Name = "DevRunTestStage" };
		runner.AddChild(stage);
		try { stage.AddChild(flow); body(flow); }
		finally
		{
			DevShell.Instance?.Menu.Close();
			runner.RemoveChild(stage);
			stage.QueueFree();
			MenuSettings.Reset();
			FightScene.Sources = saved;
			FightScene.Run = savedRun;
			FightScene.Opponent = savedFoe;
		}
	}

	static DevRunStatePage OpenPage(GameFlow flow)
	{
		var shell = DevShell.Instance ?? throw new AssertFailed("no DevShell autoload");
		Assert.True(flow.OpenDevMenu() && shell.Menu.ShowSection(DevMenu.RunId), "the dev menu opens on the Run state page");
		return shell.Menu.Page as DevRunStatePage ?? throw new AssertFailed("the run section is the run-state page, not " + shell.Menu.Page?.GetType().Name);
	}

	const string PlayerFile = "{ \"version\": 3, \"run\": null, \"unlocks\": [\"player-only-unlock\"], \"story\": { \"seen\": [] } }";

	[Test]
	public static void Page_AppliesOnTitleAndMap_InTheDevSlot_PlayerSaveUntouched(Node runner) => WithTempDir(dir =>
	{
		string player = Path.Combine(dir, "progress.json"), dev = DevMode.SavePathFor(player);
		File.WriteAllText(player, PlayerFile);
		DateTime stamp = File.GetLastWriteTimeUtc(player);
		WithMode(DevOn, () => WithBoot(runner, player, flow =>
		{
			var page = OpenPage(flow);
			Assert.True(page.Editor != null && page.Presets!.Path == DevPresetStore.PathFor(player), "title: a new run to edit, presets next to the dev slot");
			Assert.True(page.CopyLabel.Text.Contains(Sw), "the copy target shows on open: " + page.CopyLabel.Text);

			int fox = page.ShownPresets.ToList().FindIndex(p => p.Name == "Lv3 Foxfire evolved");
			Assert.True(page.LoadPreset(fox), "the built-in preset loads into the controls");
			Assert.True(page.CopyLabel.Text.Contains(Fox) && page.CopyLabel.Text.Contains("Lv 3"), "copy target follows the preset: " + page.CopyLabel.Text);
			page.Rows[SpecialSlot.C].Special.Select(0);
			page.Rows[SpecialSlot.C].Special.EmitSignal(OptionButton.SignalName.ItemSelected, 0L);
			Assert.True(page.CopyLabel.Text.Contains(Sw), "emptying slot C by hand moves the target back: " + page.CopyLabel.Text);
			page.LoadPreset(fox);
			page.Coins.Value = 120;
			page.Health.Value = 640;
			page.Depth.Value = 2;
			Assert.True(page.SavePreset("my foxfire") && File.Exists(DevPresetStore.PathFor(player)), "saved as a preset");

			page.Rows[SpecialSlot.A].Level.Value = 3;
			page.Rows[SpecialSlot.A].Order.Value = 0;
			Assert.True(page.LastCheck is { Ok: true }, "Spirit Wave Lv 3 is a valid build");
			page.Health.Value = 1;
			page.Rows[SpecialSlot.A].Borrowed.ButtonPressed = true;
			Assert.True(page.LastCheck is { Ok: false } && page.StatusLabel.Text.Contains("R6") && page.ApplyButton.Disabled, "a borrowed starter: refused, Apply greyed: " + page.StatusLabel.Text);
			Assert.True(!page.Apply() && flow.Session == null, "Apply refuses an invalid build");

			Assert.True(page.LoadPreset(page.ShownPresets.ToList().FindIndex(p => p.Name == "my foxfire")), "the saved preset loads");
			Assert.True(page.Apply() && flow.Router.Current == Screen.Map, "applied on the title: the run shows on the map");
			var s = flow.Session!;
			Assert.True(s.Run[SpecialSlot.C] is { Level: 3 } c && c.Data.Id == Fox && s.Run.Coins == 120 && s.Run.Health == 640 && s.Map.Visited.Count == 2, "the run is the preset");
			Assert.True(File.ReadAllText(dev).Contains(Fox) && File.ReadAllText(dev).Contains("\"coins\": 120"), "saved at once in the dev slot");

			DevShell.Instance!.Menu.Close();
			page = OpenPage(flow);
			Assert.True(page.CurrentBuild().Coins == 120 && page.CurrentBuild().Depth == 2, "on the map the page opens on the current run");
			page.Coins.Value = 5;
			Assert.True(page.Apply() && flow.Session != s && flow.Session!.Run.Coins == 5 && flow.Session.Map.Visited.SequenceEqual(s.Map.Visited), "applied on the map: same walk, new purse");
			Assert.True(flow.Map.Visible && flow.Router.Current == Screen.Map, "still on the map");
			string reach = flow.Session.Reachable()[0];
			Assert.True(flow.ChooseNode(reach).Ok && flow.Router.Current == Screen.Fight, "the edited run plays its next node");
			Assert.True(flow.Fight!.Match.P1.Health == flow.Session.Run.Health, "the fight starts on the edited health (C1)");
		}));
		Assert.Equal(PlayerFile, File.ReadAllText(player), "the player's file is byte-for-byte untouched");
		Assert.Equal(stamp, File.GetLastWriteTimeUtc(player), "and not rewritten");
		var left = Directory.GetFiles(dir).Select(Path.GetFileName).OrderBy(n => n).ToArray();
		Assert.True(left.SequenceEqual(new[] { "progress.dev.json", "progress.json", DevPresetStore.FileName }), "only the dev slot and the preset file were written: " + string.Join(", ", left));
	});

	[Test]
	public static void Page_RefusesMidFight_AndWithDevModeOff(Node runner) => WithTempDir(dir =>
	{
		string player = Path.Combine(dir, "progress.json");
		WithMode(DevOn, () => WithBoot(runner, player, flow =>
		{
			flow.Router.Select(TitleEntry.Start);
			flow.Router.Confirm();
			Assert.True(flow.Router.Current == Screen.Map && flow.ChooseNode("r1-apprentice").Ok, "in the row-1 fight");
			var session = flow.Session!;
			ulong before = session.StateHash();
			var fight = flow.Fight!;
			ulong fightHash = fight.Match.StateHash();
			var ctx = new DevContext(DevPlace.Fight, flow, fight);
			var page = new DevRunStatePage(ctx);
			runner.AddChild(page);
			try
			{
				Assert.True(page.CopyLabel.Text.Contains(Sw), "mid-fight the page still reads the run");
				page.Coins.Value = 777;
				Assert.True(page.LastCheck is { Ok: true } && page.ApplyButton.Disabled && page.StatusLabel.Text.Contains(DevContext.MidFightReason), "a valid build, but Apply greyed with the reason");
				Assert.True(!page.Apply() && page.StatusLabel.Text.Contains(DevContext.MidFightReason), "Apply refused mid-fight");
				Assert.True(!flow.AdoptDevSession(page.LastCheck!.Session!, out string? why) && why == DevContext.MidFightReason, "and the flow refuses it too");
				Assert.True(flow.Session == session && session.StateHash() == before && session.Run.Coins == 0, "the run is unchanged");
				Assert.True(fight.Match.StateHash() == fightHash, "the fight is unchanged");
			}
			finally { runner.RemoveChild(page); page.QueueFree(); }
			WithMode(Debug, () =>
			{
				var b = DevRunEditor.Capture(session);
				var other = DevRunEditor.For(session).Check(b with { Coins = 1 }).Session!;
				Assert.True(!flow.AdoptDevSession(other, out string? why) && why == DevContext.DevOffReason, "dev mode off: refused");
			});
		}));
	});
}
