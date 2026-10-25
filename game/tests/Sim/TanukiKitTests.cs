using System;
using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.Ai;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-94, T0: the Tanuki fights with Ryo's normals, throw and starters + the one special copied at the strength
/// T2-T4 set, from the copy locked at T1. Kits and pool are the TEST FIXTURES (Spirit Wave A, Rising Talisman B,
/// Foxfire C) unless a test names the real data (the Tanuki's profile).
/// </summary>
public static class TanukiKitTests
{
	static string RepoRoot => Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "..");
	static KitSources Fixtures => KitSources.Repo(RepoRoot).FixturesOnly();
	static AbilityPool Pool() => AbilityPool.LoadFixtures(RepoRoot);
	static Kit Ryo() => Kit.Load(Kit.Ryo, Fixtures);
	static SpecialData Foxfire(AbilityPool pool) => pool.Special("test-foxfire") ?? throw new AssertFailed("fixture Foxfire missing");

	static bool IsCopied(AiAction a) => a.Kind == ActionKind.Special && a.MoveId == BehaviourProfile.CopiedSpecial;

	/// <summary>A Lv 1-2 special for <paramref name="slot"/> made from the Spirit Wave fixture's frame data.</summary>
	static SpecialData Make(AbilityPool pool, string id, string slot)
	{
		string fd = pool.Special("test-spirit-wave")!.Level1Json;
		string levels = "\"1\":{\"frame_data\":" + fd + "},\"2\":{\"tuning\":{\"target\":\"recovery\",\"op\":\"add\",\"value\":-4}}";
		return SpecialLoader.Parse("{\"kind\":\"special\",\"id\":\"" + id + "\",\"input\":{\"kata\":{\"slot\":\"" + slot + "\"}},\"levels\":{" + levels + "}}");
	}

	/// <summary>The Tanuki as P2 of a fresh match with Ryo's fixture kit.</summary>
	static (Match M, TanukiKit Kit) Fight(CopyTarget? copy, KitSources? src = null)
	{
		var ryo = Kit.Load(Kit.Ryo, src ?? Fixtures);
		var kit = TanukiKit.Build(ryo, copy);
		var m = new Match(null, ryo.Moves, kit.Moves);
		ryo.Equip(m.P1);
		kit.Equip(m.P2);
		return (m, kit);
	}

	static void Throws<T>(Action body, string what) where T : Exception
	{
		try { body(); }
		catch (T) { return; }
		throw new AssertFailed($"{what}: expected {typeof(T).Name}");
	}

	[Test]
	public static void KitIsRyosNormalsThrowAndStarters()
	{
		var ryo = Ryo();
		var (m, kit) = Fight(null);
		Assert.True(kit.Moves.Select(x => x.Id).SequenceEqual(ryo.Moves.Select(x => x.Id)), "T0: the Tanuki's moves are Ryo's own, in his order");
		Assert.True(ReferenceEquals(TanukiKit.Build(ryo, null).Moves, ryo.Moves), "built from Ryo's kit, not a copy of it");
		Assert.True(kit.Moves.Any(x => x.IsThrow) && kit.Moves.Any(x => x.IsNormal && !x.Air), "Ryo's throw and ground normals are in");
		Assert.True(m.P2.Moves.SequenceEqual(m.P1.Moves), "both fighters hold the same normals and throw");
		Assert.Equal("test-spirit-wave", m.P2.Specials[(int)SpecialSlot.A]?.Data.Id, "A1: Spirit Wave in A");
		Assert.Equal("test-rising-talisman", m.P2.Specials[(int)SpecialSlot.B]?.Data.Id, "A1: Rising Talisman in B");
		Assert.True(m.P2.Specials[(int)SpecialSlot.A]!.Level == 1 && m.P2.Specials[(int)SpecialSlot.B]!.Level == 1, "starters at Lv 1");
		Assert.True(m.P2.Specials[(int)SpecialSlot.C] is null && m.P2.Specials[(int)SpecialSlot.D] is null, "nothing else");
		Throws<ArgumentException>(() => TanukiKit.Build(Kit.Load("kitsune", Fixtures), null), "T0: only Ryo's kit");
	}

	[Test]
	public static void CopiedSpecialIsAtRyosLevelWithoutHisModifier()
	{
		var pool = Pool();
		var run = RunState.NewRun(pool);
		var c = run.Equip(Foxfire(pool));
		run.LevelUp(c);
		var mod = pool.Modifiers.FirstOrDefault(x => run.CanAttach(c, x));
		if (mod is not null) run.Attach(c, mod);
		var copy = CopyRule.Target(run)!;
		Assert.True(copy.Data.Id == "test-foxfire" && copy.Level == 2, $"T2: Foxfire Lv 2 is the target ({copy.Data.Id} Lv {copy.Level})");

		var (m, kit) = Fight(copy);
		var eq = m.P2.Specials[(int)SpecialSlot.C]!;
		Assert.True(kit.CopiedSlot == SpecialSlot.C && m.P2.CopiedSlot == SpecialSlot.C, "the copy keeps Ryo's slot when it is free");
		Assert.True(eq.Data.Id == "test-foxfire" && eq.Level == 2, "T4: at Ryo's level");
		Assert.True(eq.Modifier is null, $"T4: never Ryo's modifier ({(mod is null ? "the fixture pool has none that fits" : mod.Id)})");
		var built = copy.Build();
		Assert.True(eq.Move.TotalFrames == built.TotalFrames && eq.Move.Id == built.Id, "the equipped move is CopyTarget.Build()");
		Assert.True(m.P2.Specials[(int)SpecialSlot.A]!.Level == 1 && m.P2.Specials[(int)SpecialSlot.B]!.Level == 1, "starters stay Lv 1 beside the copy");
	}

	[Test]
	public static void Lv3CopyRunsTheLv2Tuning()
	{
		var pool = Pool();
		var run = RunState.NewRun(pool);
		var c = run.Equip(Foxfire(pool));
		run.LevelUp(c);
		run.LevelUp(c);
		var copy = CopyRule.Target(run)!;
		Assert.Equal(3, copy.Level, "Ryo's Foxfire is Lv 3");
		var (m, _) = Fight(copy);
		var eq = m.P2.Specials[(int)SpecialSlot.C]!;
		Assert.Equal(2, eq.Level, "T4: an evolved Lv 3 move is copied at its Lv 2 tuning");
		Assert.Equal(Foxfire(pool).Build(2, false, null, null).TotalFrames, eq.Move.TotalFrames, "Lv 2 frame data");
	}

	[Test]
	public static void LevelledStarterReplacesItsLv1Self()
	{
		var pool = Pool();
		var run = RunState.NewRun(pool);
		run.LevelUp(SpecialSlot.B);
		var (m, kit) = Fight(CopyRule.Target(run));
		Assert.Equal(SpecialSlot.B, kit.CopiedSlot, "Rising Talisman copied in B");
		Assert.True(m.P2.Specials[(int)SpecialSlot.B] is { Level: 2 } b && b.Data.Id == "test-rising-talisman", "B holds the Lv 2 copy, not a second Talisman");
		Assert.True(m.P2.Specials[(int)SpecialSlot.A] is { Level: 1 }, "Spirit Wave Lv 1 still in A");
		Assert.True(m.P2.Specials[(int)SpecialSlot.C] is null && m.P2.Specials[(int)SpecialSlot.D] is null, "one copied special, no extra");
	}

	[Test]
	public static void CopyOfASpecialThatTookAStarterSlotReplacesThatStarter()
	{
		var pool = Pool();
		var run = RunState.NewRun(pool);
		var gale = Make(pool, "test-gale", "A");
		run.Equip(gale); // replaces Spirit Wave on Ryo's side
		run.LevelUp(SpecialSlot.A);
		var copy = CopyRule.Target(run)!;
		Assert.True(copy.Data.Id == "test-gale" && copy.Slot == SpecialSlot.A, "target: the drafted special in A");
		var (m, kit) = Fight(copy);
		Assert.Equal(SpecialSlot.A, kit.CopiedSlot, "designer ruling 2026-10-11: the copy sits in Ryo's slot for it");
		Assert.True(m.P2.CopiedSlot == SpecialSlot.A && m.P2.Specials[(int)SpecialSlot.A] is { Level: 2 } a && a.Data.Id == "test-gale", "A holds the copy at Lv 2, not Spirit Wave");
		Assert.True(kit.Starters.Select(s => s.Id).SequenceEqual(new[] { "test-rising-talisman" }), "the starter Ryo replaced is gone on the Tanuki's side too");
		Assert.True(m.P2.Specials[(int)SpecialSlot.B] is { Level: 1 } b && b.Data.Id == "test-rising-talisman", "the other starter stays in its own slot");
		Assert.True(m.P2.Specials[(int)SpecialSlot.C] is null && m.P2.Specials[(int)SpecialSlot.D] is null, "nothing in C or D");
		var l = AiLoadout.From(m.P2);
		Assert.True(l.TryGet(BehaviourProfile.ParseAction("special:copied"), out var input, out _) && input == FighterInput.Special(SpecialSlot.A), "special:copied presses slot A");
		Assert.True(!l.TryGet(BehaviourProfile.ParseAction("special:test-spirit-wave"), out _, out _), "the replaced starter cannot be pressed");
	}

	[Test]
	public static void CopyAtFightStart_LockWinsAndNoLockIsDefined()
	{
		var pool = Pool();
		var starters = pool.Starters.ToArray();
		var run = RunState.NewRun(pool);
		run.LevelUp(SpecialSlot.B);
		var locked = new CopyTarget(SpecialSlot.A, pool.Special("test-spirit-wave")!, 1);

		Assert.True(ReferenceEquals(locked, TanukiKit.CopyAtFightStart(true, locked, run, starters)), "T1: the locked copy, not the slots now");
		Assert.True(TanukiKit.CopyAtFightStart(true, null, run, starters) is null, "locked on no special: stays none");
		var now = TanukiKit.CopyAtFightStart(false, null, run, starters);
		Assert.True(now is { Slot: SpecialSlot.B, Level: 2 }, "no lock: the copy rule on the run as it stands");
		var fresh = TanukiKit.CopyAtFightStart(false, null, null, starters);
		Assert.True(fresh is { Slot: SpecialSlot.A, Level: 1 } && fresh.Data.Id == "test-spirit-wave", "no run: a new run's copy (T3 lower slot)");
		Assert.True(TanukiKit.CopyAtFightStart(false, null, null, Array.Empty<SpecialData>()) is null, "no starters either: none, no throw");
	}

	[Test]
	public static void NoCopyIsADefinedKit()
	{
		var (m, kit) = Fight(null);
		Assert.True(kit.Copy is null && kit.CopiedSlot == SpecialSlot.None && m.P2.CopiedSlot == SpecialSlot.None, "no copy, no copied slot");
		Assert.True(kit.Notes.Any(n => n.Contains("no copied special")), "logged");
		var l = AiLoadout.From(m.P2);
		Assert.True(!l.TryGet(BehaviourProfile.ParseAction("special:copied"), out _, out _), "special:copied presses nothing");
		Assert.True(l.TryGet(BehaviourProfile.ParseAction("special:test-spirit-wave"), out _, out _), "the starters still do");
		for (int t = 0; t < 600; t++) m.Step(FighterInput.None, FighterInput.None);
		Assert.True(m.P2.Health > 0, "the fight runs");
	}

	[Test]
	public static void CopiedTokenResolvesToTheCopySlot_AndLiteralIdsStillWork()
	{
		var a = BehaviourProfile.ParseAction("special:copied");
		Assert.True(a.Kind == ActionKind.Special && IsCopied(a) && a.ToString() == "special:copied", "parses as the copied special");
		Assert.True(!IsCopied(BehaviourProfile.ParseAction("special:test-foxfire")), "a literal id is not the token");

		var pool = Pool();
		var run = RunState.NewRun(pool);
		run.LevelUp(run.Equip(Foxfire(pool)));
		var (m, kit) = Fight(CopyRule.Target(run));
		var l = AiLoadout.From(m.P2);
		Assert.True(l.TryGet(a, out var input, out int frames), "special:copied resolves on the Tanuki");
		Assert.Equal(FighterInput.Special(kit.CopiedSlot), input, "to the copy's slot");
		Assert.Equal(m.P2.Specials[(int)kit.CopiedSlot]!.Move.TotalFrames, frames, "with the copy's frames");
		Assert.True(l.TryGet(BehaviourProfile.ParseAction("special:test-foxfire"), out var lit, out _) && lit == input, "the literal id of the copy still works");
		Assert.True(l.TryGet(BehaviourProfile.ParseAction("special:test-rising-talisman"), out var rt, out _) && rt == FighterInput.Special(SpecialSlot.B), "and a starter's literal id");

		var kitsune = Kit.Load("kitsune", Fixtures);
		var km = new Match(null, Ryo().Moves, kitsune.Moves);
		kitsune.Equip(km.P2);
		Assert.True(!AiLoadout.From(km.P2).TryGet(a, out _, out _), "no other fighter has a copied special");
	}

	[Test]
	public static void TanukiProfileUsesTheCopyAndEveryActionResolves()
	{
		var p = BehaviourProfile.LoadFile(Path.Combine(RepoRoot, "data", "profiles", "tanuki-boss.json"));
		Assert.True(p.Yokai == "tanuki" && p.Behaviours.Any(b => IsCopied(b.Do)), "tanuki-boss uses special:copied");
		Assert.True(!p.Behaviours.Any(b => b.Do.Kind == ActionKind.Special && !IsCopied(b.Do)), "and names no special id of its own");

		var src = KitSources.Repo(RepoRoot);
		var run = RunState.NewRun(AbilityPool.Load(src, Path.Combine(RepoRoot, "data", "modifiers")));
		var (m, _) = Fight(CopyRule.Target(run), src);
		var l = AiLoadout.From(m.P2);
		foreach (var act in p.Behaviours.Select(b => b.Do).Append(p.Habit.Do))
			if (act.Kind is ActionKind.Special or ActionKind.Normal)
				Assert.True(l.TryGet(act, out _, out _), $"{act} resolves on the Tanuki's kit");
			else if (act.Kind == ActionKind.Throw)
				Assert.True(l.ThrowButtons != InputBits.None, "the Tanuki has Ryo's throw");
	}

	[Test]
	public static void TanukiAiThrowsTheCopiedSpecial()
	{
		var p = BehaviourProfile.LoadFile(Path.Combine(RepoRoot, "data", "profiles", "tanuki-boss.json"));
		var pool = Pool();
		var run = RunState.NewRun(pool);
		run.LevelUp(run.Equip(Foxfire(pool)));
		int Count(uint seed)
		{
			var (m, kit) = Fight(CopyRule.Target(run));
			int used = 0;
			m.SpecialStarted += (_, e) => { if (e.Fighter == 1 && e.Slot == kit.CopiedSlot) used++; };
			var ai = new ProfileAi(p, 1, AiLoadout.From(m.P2), seed);
			for (int t = 0; t < 1800 && m.Phase != MatchPhase.Over; t++) m.Step(FighterInput.None, ai.Next(AiView.Capture(m)));
			return used;
		}
		int a = Count(94), b = Count(94);
		Assert.True(a > 0, "T0: in 30 s the Tanuki throws the copied special at least once");
		Assert.Equal(a, b, "deterministic: same seed, same count");
	}

	[Test]
	public static void BossNodeFightUsesTheLockedCopy()
	{
		var table = NodeSceneTable.Default(Path.Combine(RepoRoot, "data", "map"));
		var graph = MapLoader.Parse("""
		{ "kind": "run_map", "id": "tanuki-kit-map", "schema_version": 1,
		  "nodes": [ {"id": "boss-tanuki", "row": 1, "type": "boss", "yokai": "tanuki"} ], "routes": [] }
		""");
		var session = RunSession.Start(Pool(), graph, table, 94);
		session.Run.LevelUp(SpecialSlot.A); // before the lock: Spirit Wave Lv 2 is the target
		Assert.True(session.Enter("boss-tanuki").Ok && session.CopyLocked, "T1: locked on entering the boss node");
		Assert.True(session.Node!.Fighter == "tanuki" && session.Node.Kit == TanukiKit.KitId && !session.Node.StandIn, "node-scenes.json: the Tanuki fights as itself");

		string savedOpp = FightScene.Opponent;
		var savedSrc = FightScene.Sources;
		try
		{
			FightScene.Opponent = session.Node.Fighter;
			FightScene.Sources = Fixtures;
			session.Run.LevelUp(SpecialSlot.B);
			session.Run.LevelUp(SpecialSlot.B); // after the lock: must not reach the copy
			var m = FightScene.NewMatch(session.Run, session);
			Assert.Equal(SpecialSlot.A, m.P2.CopiedSlot, "the locked Spirit Wave");
			Assert.True(m.P2.Specials[(int)SpecialSlot.A] is { Level: 2 } && m.P2.Specials[(int)SpecialSlot.B] is { Level: 1 }, "Lv 2 copy, Talisman a Lv 1 starter");
			Assert.True(m.P2.Moves.SequenceEqual(m.P1.Moves), "Ryo's normals and throw");
		}
		finally { FightScene.Opponent = savedOpp; FightScene.Sources = savedSrc; }
	}

	[Test]
	public static void KitInTheNodeTable_OnlyOnAnOpponentWithAFighter()
	{
		static string Table(string types, string opponents) => "{\"kind\":\"node_scenes\",\"id\":\"t\",\"schema_version\":1,"
			+ "\"default_scene\":\"res://scenes/fight.tscn\",\"fallback_fighter\":\"kitsune\",\"tiers\":{\"yokai\":{\"reaction_frames\":22}},"
			+ "\"types\":{\"boss\":{" + types + "}},\"opponents\":{\"tanuki\":{" + opponents + "}}}";
		const string tier = "\"tier\":\"yokai\"", kit = "\"kit\":\"copy_of_ryo\"";
		var node = MapLoader.Parse("{\"kind\":\"run_map\",\"id\":\"m\",\"schema_version\":1,\"nodes\":[{\"id\":\"n\",\"row\":1,\"type\":\"boss\",\"yokai\":\"tanuki\"}],\"routes\":[]}").Node("n");

		var ok = NodeSceneTable.Parse(Table(tier, "\"fighter\":\"tanuki\"," + kit)).Resolve(node).Context;
		Assert.True(ok.Kit == TanukiKit.KitId && ok.Fighter == "tanuki" && !ok.StandIn, "an opponent with a fighter may name the kit");
		Assert.True(NodeSceneTable.Parse(Table(tier, "\"fighter\":\"tanuki\"")).Resolve(node).Context.Kit is null, "no kit named: the fighter's own files");
		Throws<FormatException>(() => NodeSceneTable.Parse(Table(tier, kit)), "kit without fighter (it would fight under the fallback's model)");
		Throws<FormatException>(() => NodeSceneTable.Parse(Table(tier + "," + kit, "\"fighter\":\"tanuki\"")), "kit on a type");
		Throws<FormatException>(() => NodeSceneTable.Parse(Table(tier, "\"fighter\":\"tanuki\",\"kit\":\"mirror\"")), "an unknown kit");
	}
}
