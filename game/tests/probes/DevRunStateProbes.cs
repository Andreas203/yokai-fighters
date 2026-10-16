using System;
using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.Dev;
using YokaiFighters.Sim;
using YokaiFighters.SaveGame;

namespace YokaiFighters.Tests;

/// <summary>YOK-126 probes: DevRunEditor / DevPresetStore edges (pure, no scene).</summary>
public static class DevRunStateProbes
{
	const string Sw = "test-spirit-wave", Rt = "test-rising-talisman", Fox = "test-foxfire", Wisp = "test-will-o-wisp", Patience = "test-fox-patience";
	static string RepoRoot => Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "..");
	static DevRunEditor Editor()
	{
		var pool = AbilityPool.LoadFixtures(RepoRoot);
		var graph = FileMapSource.Default(ContentPaths.Data("map")).Load();
		return new DevRunEditor(pool, graph, NodeSceneTable.Default(ContentPaths.Data("map")));
	}
	static DevRunBuild B(params DevSlotEdit[] s) => new(s, 0, 1000, 0, 7);
	static void WithTempDir(Action<string> body)
	{
		string dir = Path.Combine(Path.GetTempPath(), "yokai-devrunprobe-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(dir);
		try { body(dir); } finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
	}

	// A3/T5: a malformed build is refused with a reason, never an exception.
	[Test]
	public static void NullSpecialId_IsRefusedNotThrown()
	{
		var c = Editor().Check(B(new DevSlotEdit(SpecialSlot.A, null!)));
		Assert.True(!c.Ok && c.Refusal != null, "null special id refused");
	}

	[Test]
	public static void NonSlotValues_AreRefusedNotThrown()
	{
		var ed = Editor();
		foreach (var slot in new[] { (SpecialSlot)0, (SpecialSlot)9, unchecked((SpecialSlot)(-1)) })
		{
			var c = ed.Check(B(new DevSlotEdit(slot, Sw)));
			Assert.True(!c.Ok, $"slot {(int)slot} refused");
		}
		Assert.True(!ed.Check(B(new DevSlotEdit(SpecialSlot.A, Sw), new DevSlotEdit(SpecialSlot.A, Sw))).Ok, "slot listed twice refused");
		Assert.True(!ed.Check(B(new DevSlotEdit(SpecialSlot.A, Sw, 0))).Ok && !ed.Check(B(new DevSlotEdit(SpecialSlot.A, Sw, 4))).Ok, "levels 0 and 4 refused (A3)");
		Assert.True(!ed.Check(B(new DevSlotEdit(SpecialSlot.A, Sw, 1, null, 3))).Ok, "a Lv1 slot with a level-up rank refused (T3)");
	}

	// Health boundaries (C1): 1 and 1000 ok, 0 and 1001 refused.
	[Test]
	public static void HealthBoundaries()
	{
		var ed = Editor();
		var b = B(new DevSlotEdit(SpecialSlot.A, Sw));
		Assert.True(ed.Check(b with { Health = 1 }).Ok && ed.Check(b with { Health = 1000 }).Ok, "1 and 1000 ok");
		Assert.True(!ed.Check(b with { Health = 0 }).Ok && !ed.Check(b with { Health = 1001 }).Ok && !ed.Check(b with { Health = int.MinValue }).Ok, "0, 1001 refused");
		Assert.True(ed.Check(b with { Coins = int.MaxValue }).Ok && !ed.Check(b with { Coins = -1 }).Ok, "R8: no cap, no negative");
	}

	// Anything Check accepts must be a run the real game can Continue (RunSave round trip, same hash), for every depth.
	[Test]
	public static void EveryAcceptedBuild_SurvivesSaveAndContinue()
	{
		var ed = Editor();
		var builds = new[]
		{
			B(new DevSlotEdit(SpecialSlot.A, Sw), new DevSlotEdit(SpecialSlot.B, Rt)),
			B(new DevSlotEdit(SpecialSlot.A, Sw, 2, Wisp, 2), new DevSlotEdit(SpecialSlot.B, Rt, 2, Patience, 1), new DevSlotEdit(SpecialSlot.C, Fox, 3)),
			B(new DevSlotEdit(SpecialSlot.C, Fox, 2, Patience, 0, true, true)),
			B(new DevSlotEdit(SpecialSlot.A, Sw, 1, Wisp, 0, false, true), new DevSlotEdit(SpecialSlot.C, Fox, 3, null, 0, true)),
		};
		foreach (var b0 in builds)
			for (int d = 0; d <= ed.MaxDepth; d++)
			{
				var b = b0 with { Depth = d, Coins = 33, Health = 1 };
				var c = ed.Check(b);
				Assert.True(c.Ok, $"depth {d}: {c.Refusal}");
				var s = c.Session!;
				var save = RunSave.Capture(s);
				RunSession back;
				try { back = save.Restore(ed.Pool, ed.Graph, ed.Scenes); }
				catch (FormatException e) { throw new AssertFailed($"depth {d}: Continue rejects an accepted build: {e.Message}"); }
				Assert.Equal(s.StateHash(), back.StateHash(), $"depth {d}: save round trip keeps the hash");
				Assert.Equal(s.StateHash(), ed.Check(DevRunEditor.Capture(s)).Session!.StateHash(), $"depth {d}: Capture -> Check");
				var tc = c.Copy; var tb = CopyRule.Target(back.Run);
				Assert.True((tc is null) == (tb is null) && (tc is null || (tc.Slot == tb!.Slot && tc.Level == tb.Level)), $"depth {d}: copy target same after Continue");
			}
	}

	// T1: no accepted depth has the copy already locked or a Tanuki node walked; one past MaxDepth refused.
	[Test]
	public static void Depth_NeverPassesTheLockPoint()
	{
		var ed = Editor();
		var b = B(new DevSlotEdit(SpecialSlot.A, Sw));
		Assert.True(!ed.Check(b with { Depth = ed.MaxDepth + 1 }).Ok, "past the Tanuki refused");
		Assert.True(!ed.Check(b with { Depth = ed.Graph.Rows.Count }).Ok && !ed.Check(b with { Depth = int.MaxValue }).Ok && !ed.Check(b with { Depth = -1 }).Ok, "huge/negative depth refused");
		for (int d = 0; d <= ed.MaxDepth; d++)
		{
			var s = ed.Check(b with { Depth = d }).Session!;
			Assert.True(!s.CopyLocked && s.LockedCopy == null && !s.Map.InNode, $"depth {d}: copy not locked, between nodes (T1)");
		}
		// A preferred walk naming nodes that are not on the map or not in row order must not leak into the walk.
		var junk = new DevRunEditor(ed.Pool, ed.Graph, ed.Scenes, new[] { "nope", ed.Graph.Rows.Count > 0 ? "zzz" : "" });
		Assert.True(junk.Check(b with { Depth = 3 }).Ok, "junk preferred walk falls back to the first route");
	}

	// T2-T4: Check's copy equals an independent reading of the rule for every level / order combination.
	[Test]
	public static void CopyTarget_MatchesTheRuleForEveryLevelAndOrder()
	{
		var ed = Editor();
		var ids = new[] { (SpecialSlot.A, Sw), (SpecialSlot.B, Rt), (SpecialSlot.C, Fox) };
		for (int la = 1; la <= 2; la++) for (int lb = 1; lb <= 2; lb++) for (int lc = 1; lc <= 3; lc++)
		for (int perm = 0; perm < 6; perm++)
		{
			int[] lv = { la, lb, lc };
			int[] perms = perm switch { 0 => new[] { 1, 2, 3 }, 1 => new[] { 1, 3, 2 }, 2 => new[] { 2, 1, 3 }, 3 => new[] { 2, 3, 1 }, 4 => new[] { 3, 1, 2 }, _ => new[] { 3, 2, 1 } };
			// ranks only among levelled slots, relative order from perms
			var levelled = Enumerable.Range(0, 3).Where(i => lv[i] > 1).OrderBy(i => perms[i]).ToArray();
			var rank = new int[3]; for (int r = 0; r < levelled.Length; r++) rank[levelled[r]] = r + 1;
			var slots = ids.Select((x, i) => new DevSlotEdit(x.Item1, x.Item2, lv[i], null, rank[i])).ToArray();
			var c = ed.Check(B(slots));
			Assert.True(c.Ok, "accepted: " + c.Refusal);
			int best = lv.Max();
			var tied = Enumerable.Range(0, 3).Where(i => lv[i] == best).ToArray();
			int want = best == 1 ? 0 : tied.OrderByDescending(i => rank[i]).First(); // T3: most recent, (T3) then lower slot
			Assert.True(c.Copy!.Slot == ids[want].Item1 && c.Copy.Level == best, $"lv {la}{lb}{lc} rank {rank[0]}{rank[1]}{rank[2]}: expected slot {ids[want].Item1}, got {c.Copy.Slot}");
		}
	}

	// T3 auto rank: a levelled slot left at 0 next to a ranked one must not collide or throw.
	[Test]
	public static void AutoRank_MixedWithGivenRanks_IsAccepted()
	{
		var ed = Editor();
		var c = ed.Check(B(new DevSlotEdit(SpecialSlot.A, Sw, 2, null, 0), new DevSlotEdit(SpecialSlot.B, Rt, 2, null, 5), new DevSlotEdit(SpecialSlot.C, Fox, 2, null, 0)));
		Assert.True(c.Ok, "mixed ranks: " + c.Refusal);
		Assert.True(c.Copy!.Slot == SpecialSlot.C, "auto ranks count after given ones, slot order: C most recent");
		var big = ed.Check(B(new DevSlotEdit(SpecialSlot.A, Sw, 2, null, int.MaxValue), new DevSlotEdit(SpecialSlot.B, Rt, 2, null, 0)));
		Assert.True(big.Ok || big.Refusal != null, "int.MaxValue rank does not overflow into an exception");
	}

	// ---- presets ----
	[Test]
	public static void Presets_OddNames_RoundTrip() => WithTempDir(dir =>
	{
		var store = new DevPresetStore(Path.Combine(dir, DevPresetStore.FileName));
		var b = B(new DevSlotEdit(SpecialSlot.A, Sw));
		string longName = new string('x', 20000);
		var names = new[] { "狐火 \U0001F98A \"quote\" \\ back", longName, "a\nb\t\0c", "Dup", "dup", " lead", "trail " };
		foreach (var n in names) store.Save(new DevPreset(n, b));
		store.Save(new DevPreset("Dup", b with { Coins = 99 })); // replace, case-sensitive
		var back = store.Load();
		Assert.True(store.Problem == null, "no problem: " + store.Problem);
		Assert.Equal(names.Length, back.Count, "one per distinct name; duplicate replaced");
		foreach (var n in names) Assert.True(back.Any(p => p.Name == n), "name kept: " + (n.Length > 20 ? "long" : n));
		Assert.True(back.First(p => p.Name == "Dup").Build.Coins == 99 && back.First(p => p.Name == "dup").Build.Coins == 0, "replaced only the exact name");
		foreach (var bad in new[] { "", " ", "\t\n" })
			Assert.True(Throws(() => store.Save(new DevPreset(bad, b))), "blank name refused");
		Assert.True(!store.Delete("nope") && store.Delete("Dup") && store.Load().Count == names.Length - 1, "delete exact");
	});

	[Test]
	public static void Presets_BuiltInFlag_NeverWritten() => WithTempDir(dir =>
	{
		var store = new DevPresetStore(Path.Combine(dir, DevPresetStore.FileName));
		var ed = Editor();
		foreach (var p in ed.BuiltInPresets(5)) store.Save(p);
		Assert.True(store.Load().All(p => !p.BuiltIn), "saved copies are user presets");
		Assert.True(ed.BuiltInPresets(5).All(p => ed.Check(p.Build).Ok), "every built-in is accepted by Check");
	});

	[Test]
	public static void Presets_CorruptFiles_GiveNoPresetsAndAProblem_NeverThrow() => WithTempDir(dir =>
	{
		string path = Path.Combine(dir, DevPresetStore.FileName);
		var store = new DevPresetStore(path);
		string ok = "{\"version\":1,\"presets\":[{\"name\":\"n\",\"slots\":[{\"slot\":\"A\",\"special\":\"" + Sw + "\"}]}]}";
		var bad = new[]
		{
			"", "null", "[]", "{", "\u0000\u0001", "{\"version\":2,\"presets\":[]}", "{\"version\":\"1\",\"presets\":[]}", "{\"presets\":[]}",
			"{\"version\":1,\"presets\":{}}", "{\"version\":1,\"presets\":[1]}", "{\"version\":1,\"presets\":[{\"name\":5,\"slots\":[]}]}",
			"{\"version\":1,\"presets\":[{\"name\":\"n\",\"slots\":[{\"slot\":\"Q\",\"special\":\"x\"}]}]}",
			ok.Replace("\"slots\"", "\"coins\":\"many\",\"slots\""), ok.Replace("\"slots\"", "\"seed\":-1,\"slots\""),
			ok.Replace("\"slots\"", "\"seed\":4294967296,\"slots\""), ok.Replace("\"slots\"", "\"health\":1.5,\"slots\""),
			ok.Replace("\"slots\"", "\"depth\":99999999999,\"slots\""), ok.Replace("\"A\"", "null"), ok.Replace("\"" + Sw + "\"", "7"),
		};
		foreach (var text in bad)
		{
			File.WriteAllText(path, text);
			try { var l = store.Load(); Assert.True(l.Count == 0 && store.Problem != null, "unusable file: no presets, Problem set: " + text); }
			catch (AssertFailed) { throw; }
			catch (Exception e) { throw new AssertFailed($"Load threw {e.GetType().Name} on: {text}"); }
		}
		File.WriteAllText(path, ok);
		Assert.True(store.Load().Count == 1 && store.Problem == null, "valid file loads and clears Problem");
	});

	// A slot name must be A-D exactly (the saved-run parser is strict); Enum.TryParse would also take "2" or "A,B".
	[Test]
	public static void Presets_SlotNameIsStrict() => WithTempDir(dir =>
	{
		string path = Path.Combine(dir, DevPresetStore.FileName);
		var store = new DevPresetStore(path);
		foreach (var s in new[] { "a", "2", "A,B", " A", "1" })
		{
			File.WriteAllText(path, "{\"version\":1,\"presets\":[{\"name\":\"n\",\"slots\":[{\"slot\":\"" + s + "\",\"special\":\"" + Sw + "\"}]}]}");
			Assert.True(store.Load().Count == 0, $"slot '{s}' is not A-D");
		}
	});

	[Test]
	public static void Presets_OversizedFile_NotRead_BoundaryAtCap() => WithTempDir(dir =>
	{
		string path = Path.Combine(dir, DevPresetStore.FileName);
		var store = new DevPresetStore(path);
		string head = "{\"version\":1,\"presets\":[],\"pad\":\"";
		string tail = "\"}";
		void Fill(long total) => File.WriteAllText(path, head + new string('p', (int)(total - head.Length - tail.Length)) + tail);
		Fill(DevPresetStore.MaxBytes);
		Assert.True(store.Problem == null && store.Load().Count == 0 && store.Problem == null, "exactly at the cap is read");
		Fill(DevPresetStore.MaxBytes + 1);
		store.Load();
		Assert.True(store.Problem != null, "one byte over the cap is not read");
	});

	// The dev preset file and the player's save: different files, in the dev folder, whatever the save path looks like.
	[Test]
	public static void PresetPath_NeverTheSave_AndPresetsNeverTouchIt() => WithTempDir(dir =>
	{
		foreach (var p in new[] { Path.Combine(dir, "player."+"json"), "player."+"json", Path.Combine(dir, "sub", "x") })
		{
			string pp = DevPresetStore.PathFor(p);
			Assert.True(pp != p && Path.GetFileName(pp) == DevPresetStore.FileName, "preset path differs from the save: " + p);
		}
		string save = Path.Combine(dir, "player."+"json");
		File.WriteAllText(save, "PLAYER");
		var st = new DevPresetStore(DevPresetStore.PathFor(save));
		st.Save(new DevPreset("x", B(new DevSlotEdit(SpecialSlot.A, Sw))));
		st.Delete("x"); st.Load();
		Assert.True(File.ReadAllText(save) == "PLAYER", "player's save untouched");
		Assert.True(!File.Exists(st.Path + ".tmp"), "no temp file left");
		Assert.True(Throws(() => new DevPresetStore(" ")), "blank path refused");
	});

	static bool Throws(Action a) { try { a(); return false; } catch (Exception) { return true; } }
}
