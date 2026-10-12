using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.Ai;
using YokaiFighters.Fight;
using YokaiFighters.Replay;
using YokaiFighters.SaveGame;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>Test Breaker probes for YOK-94 (T0-T5, C1). Expected values come from rules.md, not from the code.</summary>
public static class TanukiProbes
{
	static string Root => Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "..");
	static KitSources Fixtures => KitSources.Repo(Root).FixturesOnly();
	static AbilityPool Pool() => AbilityPool.LoadFixtures(Root);
	static BehaviourProfile Boss() => BehaviourProfile.LoadFile(Path.Combine(Root, "data", "profiles", "tanuki-boss.json"));
	static ReplayCatalog? _cat;
	static ReplayCatalog Cat => _cat ??= ReplayCatalog.FromRepo(Root);

	static SpecialData Make(AbilityPool pool, string id, string slot)
	{
		string fd = pool.Special("test-spirit-wave")!.Level1Json;
		string levels = "\"1\":{\"frame_data\":" + fd + "},\"2\":{\"tuning\":{\"target\":\"recovery\",\"op\":\"add\",\"value\":-4}},\"3\":{\"evolution\":{\"note\":\"x\"}}";
		return SpecialLoader.Parse("{\"kind\":\"special\",\"id\":\"" + id + "\",\"input\":{\"kata\":{\"slot\":\"" + slot + "\"}},\"levels\":{" + levels + "}}");
	}

	static (Match M, TanukiKit Kit, Kit Ryo) Fight(CopyTarget? copy, RunState? run = null, KitSources? src = null)
	{
		var ryo = Kit.Load(Kit.Ryo, src ?? Fixtures);
		var kit = TanukiKit.Build(ryo, copy);
		var cfg = run?.MatchConfig();
		var m = new Match(cfg, ryo.Moves, kit.Moves);
		ryo.Equip(m.P1);
		kit.Equip(m.P2);
		run?.ApplyTo(m.P1, cfg);
		return (m, kit, ryo);
	}

	static string Specials(Fighter f) => string.Join(",", Enumerable.Range(1, 4).Select(i => f.Specials[i] is { } e ? $"{(SpecialSlot)i}:{e.Data.Id}@{e.Level}{(e.Modifier is null ? "" : "+" + e.Modifier.Id)}" : $"{(SpecialSlot)i}:-"));

	// T0/T4: every special x Lv 1-3, with Ryo carrying a modifier: exactly one copy, no modifier, level capped at 2.
	[Test]
	public static void EveryCopy_ExactlyOneCopiedSpecial_NoModifier_CappedAtLv2()
	{
		var pool = Pool();
		var all = new List<SpecialData>(pool.Specials) { Make(pool, "p-c", "C"), Make(pool, "p-d", "D"), Make(pool, "p-gale", "A") };
		var real = AbilityPool.Load(KitSources.Repo(Root), Path.Combine(Root, "data", "modifiers"));
		var mods = real.Modifiers.ToList();
		int checkedN = 0;
		foreach (var data in all)
			for (int lv = 1; lv <= Math.Min(3, data.MaxLevel); lv++)
			{
				var copy = new CopyTarget(data.Slot, data, lv);
				var (m, kit, _) = Fight(copy);
				// Ryo's side with a drafted special and his own modifier, if one fits, must not leak.
				var ryoRun = RunState.NewRun(pool);
				ryoRun.Equip(Make(pool, "p-ryo-draft", "C"));
				var mod = mods.FirstOrDefault(x => ryoRun.CanAttach(SpecialSlot.C, x));
				if (mod is not null) ryoRun.Attach(SpecialSlot.C, mod);
				ryoRun.ApplyTo(m.P1);
				string what = $"{data.Id} Lv{lv}: {Specials(m.P2)}";
				int n = 0;
				for (int s = 1; s <= 4; s++)
				{
					if (m.P2.Specials[s] is not { } e) continue;
					n++;
					Assert.True(e.Modifier is null, "T4 no modifier: " + what);
					Assert.True(e.Data.Id != "p-ryo-draft", "Ryo's drafted special leaked: " + what);
				}
				var c = m.P2.Specials[(int)kit.CopiedSlot]!;
				Assert.True(c.Data.Id == data.Id && c.Level == Math.Min(lv, 2), "T4 level min(Lv,2): " + what);
				Assert.Equal(1, m.P2.Specials.Count(e => e is { } x && x.Data.Id == data.Id), "no second copy: " + what);
				int expect = data.Slot is SpecialSlot.A or SpecialSlot.B ? 2 : 3;
				Assert.Equal(expect, n, "special count: " + what);
				checkedN++;
			}
		Assert.True(checkedN >= 8, "swept " + checkedN);
	}

	// Mirror ruling: the copy replaces what sits in Ryo's slot; the other starter stays at Lv 1; slots C/D keep both starters.
	[Test]
	public static void Mirror_EverySlot_NeverTwoInASlot_OtherStartersStay()
	{
		var pool = Pool();
		var cases = new[] { ("test-spirit-wave", SpecialSlot.A), ("test-rising-talisman", SpecialSlot.B), ("p-c", SpecialSlot.C), ("p-d", SpecialSlot.D), ("p-gale", SpecialSlot.A) };
		foreach (var (id, slot) in cases)
		{
			var data = pool.Special(id) ?? Make(pool, id, id == "p-gale" ? "A" : slot.ToString());
			var (m, kit, _) = Fight(new CopyTarget(slot, data, 2));
			Assert.Equal(slot, m.P2.CopiedSlot, id + " copied slot is Ryo's slot");
			Assert.True(m.P2.Specials[(int)slot] is { Level: 2 } c && c.Data.Id == data.Id, id + " holds the copy at Lv 2: " + Specials(m.P2));
			foreach (var st in pool.Starters.Where(x => x.Slot != slot))
				Assert.True(m.P2.Specials[(int)st.Slot] is { Level: 1 } o && o.Data.Id == st.Id, $"{id}: starter {st.Id} stays Lv 1: {Specials(m.P2)}");
			Assert.True(kit.Starters.All(x => x.Slot != slot), id + " kit.Starters has none in the copy's slot");
		}
		var (n, nk, _) = Fight(null);
		Assert.True(nk.CopiedSlot == SpecialSlot.None && n.P2.CopiedSlot == SpecialSlot.None, "null copy: no slot");
		Assert.True(n.P2.Specials[(int)SpecialSlot.A] is { Level: 1 } && n.P2.Specials[(int)SpecialSlot.B] is { Level: 1 }, "null copy keeps both starters");
	}

	// Equip twice on the same fighter (e.g. a re-used kit) must not stack: building and equipping again gives the same specials.
	[Test]
	public static void KitEquippedTwice_SameSpecials()
	{
		var pool = Pool();
		var run = RunState.NewRun(pool);
		run.LevelUp(SpecialSlot.B);
		var (m, kit, _) = Fight(CopyRule.Target(run));
		string once = Specials(m.P2);
		kit.Equip(m.P2);
		Assert.Equal(once, Specials(m.P2), "T0: equipping again changes nothing");
	}

	const string BossMap = """
	{ "kind": "run_map", "id": "tanuki-probe-map", "schema_version": 1,
	  "nodes": [ {"id":"boss","row":1,"type":"boss","yokai":"tanuki"} ], "routes": [] }
	""";

	static void OnFixtures(Action body)
	{
		var so = FightScene.Opponent; var ss = FightScene.Sources; var sr = FightScene.Run;
		FightScene.Sources = Fixtures; FightScene.Run = null;
		try { body(); } finally { FightScene.Opponent = so; FightScene.Sources = ss; FightScene.Run = sr; }
	}

	static NodeSceneTable Table() => NodeSceneTable.Default(Path.Combine(Root, "data", "map"));

	static RunSession ToBoss(AbilityPool pool)
	{
		return RunSession.Start(pool, MapLoader.Parse(BossMap), Table(), 7);
	}

	// T1: after the lock, level / replace / sell nothing: the locked copy is the kit, whatever Ryo does next.
	[Test]
	public static void LockedCopy_SurvivesLevelAndReplace_ThenBossFight() => OnFixtures(() =>
	{
		var pool = Pool();
		var s = ToBoss(pool);
		s.Run.LevelUp(SpecialSlot.A);
		Assert.True(s.Enter("boss").Ok && s.CopyLocked, "locked");
		var locked = s.LockedCopy!;
		Assert.True(locked.Data.Id == "test-spirit-wave" && locked.Level == 2, "target is Spirit Wave Lv 2");
		s.Run.LevelUp(SpecialSlot.A);                                    // the locked special itself, Lv 3
		s.Run.LevelUp(SpecialSlot.B); s.Run.LevelUp(SpecialSlot.B);       // another to Lv 3
		s.Run.Equip(Make(pool, "p-late", "A"));                          // replaces the locked special in Ryo's A
		FightScene.Opponent = s.Node!.Fighter;
		var m = FightScene.NewMatch(s.Run, s);
		Assert.True(m.P2.CopiedSlot == SpecialSlot.A && m.P2.Specials[1] is { Level: 2 } a && a.Data.Id == "test-spirit-wave", "T1: the locked Spirit Wave Lv 2 in A, got " + Specials(m.P2));
		Assert.True(m.P2.Specials[2] is { Level: 1 } b && b.Data.Id == "test-rising-talisman", "Talisman stays a Lv 1 starter: " + Specials(m.P2));
		Assert.True(m.P2.Specials[3] is null && m.P2.Specials[4] is null, "nothing else: " + Specials(m.P2));
		Assert.True(m.P1.Specials[1]!.Data.Id == "p-late", "Ryo has the replacement");
	});

	// T1 + save: a run saved inside the boss node restores to the same lock and the same Tanuki kit.
	[Test]
	public static void SaveInsideBossNode_RestoresSameCopyAndKit() => OnFixtures(() =>
	{
		var pool = Pool();
		var graph = MapLoader.Parse(BossMap);
		var s = ToBoss(pool);
		s.Run.LevelUp(SpecialSlot.B);
		Assert.True(s.Enter("boss").Ok, "enter boss");
		// (the file round trip itself is SaveProbes.V2_LockInsideTheTanukiNode_RoundTrip_SameHash_StableBytes; test files may not name the store)
		var back = RunSave.Capture(s).Restore(pool, graph, Table());
		Assert.True(back.CopyLocked && back.LockedCopy is { } lc && lc.Data.Id == s.LockedCopy!.Data.Id && lc.Slot == s.LockedCopy.Slot && lc.Level == s.LockedCopy.Level,
			$"same lock: {back.LockedCopy} vs {s.LockedCopy}");
		FightScene.Opponent = "tanuki";
		var live = FightScene.NewMatch(s.Run, s);
		var rest = FightScene.NewMatch(back.Run, back);
		Assert.Equal(Specials(live.P2), Specials(rest.P2), "same Tanuki kit after restore");
		Assert.True(rest.P2.CopiedSlot == live.P2.CopiedSlot, "same copied slot");
	});

	// Shared state: Ryo's move array is the Tanuki's; a long fight changes neither side's moves or Ryo's specials; deterministic twice.
	[Test]
	public static void LongFight_SharesNothingMutable_AndIsDeterministic()
	{
		var pool = Pool();
		var run = RunState.NewRun(pool);
		run.LevelUp(run.Equip(Make(pool, "p-c", "C")));
		List<ulong> Go(out string moveFp, out string ryoSpecials)
		{
			var (m, kit, ryo) = Fight(CopyRule.Target(run), run);
			Assert.True(ReferenceEquals(kit.Moves, ryo.Moves), "Tanuki holds Ryo's move array");
			Assert.True(!ReferenceEquals(m.P1.Specials[3], m.P2.Specials[3]) || m.P1.Specials[3] is null, "no shared equipped-special object");
			string before = string.Join("|", ryo.Moves.Select(x => Fingerprint.Of(x)));
			string spBefore = Specials(m.P1);
			var ai = new ProfileAi(Boss(), 1, AiLoadout.From(m.P2), 4242);
			var rng = new Random(11);
			var hashes = new List<ulong>();
			for (int t = 0; t < 4000; t++)
			{
				var bits = (InputBits)0;
				if (rng.Next(3) == 0) bits |= rng.Next(2) == 0 ? InputBits.Right : InputBits.Left;
				if (rng.Next(5) == 0) bits |= (InputBits)(1 << rng.Next(6));
				var p1 = rng.Next(60) == 0 ? FighterInput.Special((SpecialSlot)(1 + rng.Next(4)), false, bits) : new FighterInput(bits);
				m.Step(p1, ai.Next(AiView.Capture(m)));
				hashes.Add(m.StateHash());
				if (m.Phase == MatchPhase.Over) m.Reset();
			}
			Assert.Equal(before, string.Join("|", ryo.Moves.Select(x => Fingerprint.Of(x))), "moves unchanged by the fight");
			Assert.Equal(spBefore, Specials(m.P1), "Ryo's specials unchanged by the fight");
			moveFp = before; ryoSpecials = spBefore;
			return hashes;
		}
		var a = Go(out _, out _);
		var b = Go(out _, out _);
		Assert.True(a.SequenceEqual(b), "same seed and inputs: identical StateHash sequence");
	}

	// special:copied on other fighters resolves to nothing; same AI seed, same first decision roll with and without a copy.
	[Test]
	public static void CopiedToken_OnRyoAndKitsune_ResolvesToNothing_SameRoll()
	{
		var tok = BehaviourProfile.ParseAction("special:copied");
		var ryo = Kit.Load(Kit.Ryo, Fixtures);
		var kit = Kit.Load("kitsune", Fixtures);
		var m = new Match(null, ryo.Moves, kit.Moves);
		ryo.Equip(m.P1); kit.Equip(m.P2);
		Assert.True(!AiLoadout.From(m.P1).TryGet(tok, out _, out _) && !AiLoadout.From(m.P2).TryGet(tok, out _, out _), "nothing on Ryo or the Kitsune");
		// Even a kit whose special has the id "copied" must not make the token press something on a non-Tanuki.
		List<AiDecision> Log(Match mm, int self)
		{
			var ai = new ProfileAi(Boss(), self, AiLoadout.From(mm.Fighters[self]), 99) { Log = new() };
			for (int t = 0; t < 900; t++) mm.Step(FighterInput.None, ai.Next(AiView.Capture(mm)));
			return ai.Log;
		}
		var l1 = Log(m, 1);
		var (m2, _, _) = Fight(null);
		var l2 = Log(m2, 1);
		Assert.True(l1.Count > 0 && l2.Count > 0, "decisions logged");
		Assert.Equal(l1[0].Roll, l2[0].Roll, "same seed, same first draw whether or not the copy resolves");
		var (m3, _, _) = Fight(null);
		var l3 = Log(m3, 1);
		Assert.Equal(l2.Count, l3.Count, "deterministic with no copy");
	}

	static ReplayRecording Record(CopyTarget? copy, uint seed, int steps = 1500)
	{
		var (m, _, _) = Fight(copy);
		var ai = new ProfileAi(Boss(), 1, AiLoadout.From(m.P2), seed);
		var rec = ReplayRecorder.Begin(m, ai, seed);
		var rng = new Random((int)seed);
		for (int t = 0; t < steps && m.Phase != MatchPhase.Over; t++)
		{
			var a = new FighterInput(rng.Next(4) == 0 ? InputBits.Right : InputBits.None);
			var b = ai.Next(AiView.Capture(m));
			m.Step(a, b);
			rec.Record(a, b, m.StateHash());
		}
		return rec.Finish(m);
	}

	// Replays: every copy shape and several seeds replay; tampered slots are reported, never a silent pass.
	[Test]
	public static void BossFightReplays_ForEachCopy_AndSeed()
	{
		var pool = Pool();
		var copies = new List<CopyTarget?>
		{
			null,
			new CopyTarget(SpecialSlot.A, pool.Special("test-spirit-wave")!, 1),
			new CopyTarget(SpecialSlot.B, pool.Special("test-rising-talisman")!, 2),
			new CopyTarget(SpecialSlot.C, pool.Special("test-foxfire")!, 3),
			new CopyTarget(SpecialSlot.A, SpecialLoader.LoadDirectory(Path.Combine(Root, "data", "moves")).First(x => x.Id == "spirit-wave"), 2),
		};
		foreach (uint seed in new uint[] { 1, 94, 777 })
			foreach (var c in copies)
			{
				var rec = ReplayRecording.FromBytes(Record(c, seed).ToBytes());
				var r = Replayer.Run(rec, Cat);
				Assert.True(r.Ok, $"seed {seed} copy {c?.Data.Id} {c?.Slot} Lv{c?.Level}: {r}");
			}
	}

	[Test]
	public static void TamperedCopiedSlot_IsReported_NeverSilentlyPasses()
	{
		var pool = Pool();
		var rec0 = Record(new CopyTarget(SpecialSlot.A, pool.Special("test-spirit-wave")!, 1), 94, 1800);
		foreach (int bad in new[] { 0, 5, -1, 4, 2 }) // 0 none, 5/-1 out of range, 4 empty (D), 2 = Talisman in B (a different special)
		{
			var rec = ReplayRecording.FromBytes(rec0.ToBytes());
			rec.Header.Fighters[1].CopiedSlot = bad;
			bool refused = false, ok = false;
			try { ok = Replayer.Run(rec, Cat).Ok; } catch (ReplaySetupException) { refused = true; }
			Assert.True(refused || !ok, $"tampered copied slot {bad} must not replay OK");
		}
	}

	static string Table(string types, string opponents) => "{\"kind\":\"node_scenes\",\"id\":\"t\",\"schema_version\":1,"
		+ "\"default_scene\":\"res://scenes/fight.tscn\",\"fallback_fighter\":\"kitsune\",\"tiers\":{\"yokai\":{\"reaction_frames\":22}},"
		+ "\"types\":{\"boss\":{" + types + "}},\"nodes\":{" + "NODES" + "},\"opponents\":{\"tanuki\":{" + opponents + "}}}";

	[Test]
	public static void KitShapes_AllMalformedOnesFailTheLoad()
	{
		const string tier = "\"tier\":\"yokai\"";
		string Build(string types, string opp, string nodes = "") => Table(types, opp).Replace("NODES", nodes);
		var bad = new Dictionary<string, string>
		{
			["number"] = Build(tier, "\"fighter\":\"tanuki\",\"kit\":1"),
			["empty"] = Build(tier, "\"fighter\":\"tanuki\",\"kit\":\"\""),
			["wrong case"] = Build(tier, "\"fighter\":\"tanuki\",\"kit\":\"Copy_Of_Ryo\""),
			["on a type"] = Build(tier + ",\"kit\":\"copy_of_ryo\"", "\"fighter\":\"tanuki\""),
			["on a node"] = Build(tier, "\"fighter\":\"tanuki\"", "\"boss\":{\"kit\":\"copy_of_ryo\"}"),
			["no fighter"] = Build(tier, "\"kit\":\"copy_of_ryo\""),
			["unknown"] = Build(tier, "\"fighter\":\"tanuki\",\"kit\":\"mirror\""),
			["bool"] = Build(tier, "\"fighter\":\"tanuki\",\"kit\":true"),
			["object"] = Build(tier, "\"fighter\":\"tanuki\",\"kit\":{}"),
			["array"] = Build(tier, "\"fighter\":\"tanuki\",\"kit\":[\"copy_of_ryo\"]"),
			["trailing space"] = Build(tier, "\"fighter\":\"tanuki\",\"kit\":\"copy_of_ryo \""),
		};
		var accepted = new List<string>();
		foreach (var (name, json) in bad)
		{
			try { NodeSceneTable.Parse(json); accepted.Add(name); }
			catch (FormatException) { }
		}
		Assert.True(accepted.Count == 0, "loader accepted: " + string.Join(", ", accepted));
	}

	// KnownOpen: tools/validate_data.py rejects "kit": null (schema enum) but the loader reads null as absent, like every
	// other optional text field. Harmless (the Tanuki then fights as Kitsune-files "tanuki" fighter). Flip to Throws<FormatException> if the loader is made strict.
	[Test]
	public static void KnownOpen_KitNull_LoaderAcceptsWhatValidatorRejects()
	{
		var json = Table("\"tier\":\"yokai\"", "\"fighter\":\"tanuki\",\"kit\":null").Replace("NODES", "");
		var node = MapLoader.Parse("{\"kind\":\"run_map\",\"id\":\"m\",\"schema_version\":1,\"nodes\":[{\"id\":\"n\",\"row\":1,\"type\":\"boss\",\"yokai\":\"tanuki\"}],\"routes\":[]}").Node("n");
		Assert.True(NodeSceneTable.Parse(json).Resolve(node).Context.Kit is null, "today: null kit = no kit named");
	}

	// No other node gets the kit: walk every node of the default map.
	[Test]
	public static void OnlyTheBossNodeBuildsTheTanukiKit_BossStillUsesRyosAnimationSet() => OnFixtures(() =>
	{
		var map = Path.Combine(Root, "data", "map", "default-map.json");
		var graph = MapLoader.Parse(File.ReadAllText(map));
		var table = Table();
		int kits = 0;
		foreach (var n in graph.Nodes)
		{
			var ctx = table.Resolve(n).Context;
			bool tanukiKit = ctx.Kit == TanukiKit.KitId;
			if (tanukiKit) kits++;
			Assert.Equal(n.Type == "boss" && n.Yokai == "tanuki", tanukiKit, $"node {n.Id} ({n.Type}/{n.Yokai}) kit {ctx.Kit}");
		}
		Assert.Equal(1, kits, "exactly one node builds the Tanuki kit");
	});

	// Profile: every action resolves on every possible kit that has a copy; with no copy only special:copied may fall away.
	[Test]
	public static void ProfileActionsResolveOnEveryKit()
	{
		var p = Boss();
		var pool = Pool();
		var copies = new List<CopyTarget?> { null };
		foreach (var d in pool.Specials.Append(Make(pool, "p-c", "C")).Append(Make(pool, "p-d", "D")).Append(Make(pool, "p-gale", "A")))
			foreach (int lv in new[] { 1, 2 }) copies.Add(new CopyTarget(d.Slot, d, lv));
		foreach (var c in copies)
		{
			var (m, _, _) = Fight(c, null, KitSources.Repo(Root));
			var l = AiLoadout.From(m.P2);
			foreach (var act in p.Behaviours.Select(b => b.Do).Append(p.Habit.Do))
			{
				if (act.Kind == ActionKind.Throw) { Assert.True(l.ThrowButtons != InputBits.None, "throw"); continue; }
				if (act.Kind is not (ActionKind.Special or ActionKind.Normal)) continue;
				bool copiedTok = act.Kind == ActionKind.Special;
				Assert.Equal(!(copiedTok && c is null), l.TryGet(act, out _, out _), $"{act} on copy {c?.Data.Id} {c?.Slot}");
			}
		}
	}
}
