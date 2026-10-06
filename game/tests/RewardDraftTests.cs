using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;
using YokaiFighters.Ui;
using SimCardKind = YokaiFighters.Sim.CardKind;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-47: the reward draft and run state. Pool = TEST FIXTURE starters (tests/fixtures/specials/) plus the
/// TEST FIXTURE Foxfire, Will-o'-wisp and Fox's Patience (tests/fixtures/rewards/) while data/ has none.
/// Each card type is picked, the next Match is built from the run, and the change is checked in the fight.
/// </summary>
public static class RewardDraftTests
{
	static readonly FighterInput Idle = FighterInput.None;
	static string RepoRoot => Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "..");
	/// <summary>YOK-56: the fixture pool only, so these tests never depend on what is in data/.</summary>
	static AbilityPool Pool() => AbilityPool.LoadFixtures(RepoRoot);

	static MoveData[] Moves() =>
		MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureNormalsDir))
			.Concat(MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureThrowsDir))).ToArray();

	/// <summary>The next fight as the run sets it up: P1 = Ryo from the run, P2 = starters.</summary>
	static Match NextMatch(RunState run, ControlScheme scheme = ControlScheme.Kata)
	{
		var cfg = run.MatchConfig();
		var m = new Match(cfg, Moves(), Moves());
		run.ApplyTo(m.P1, cfg);
		FightScene.EquipStarters(m.P2, Pool().Starters.ToArray());
		m.P1.Input.Scheme = scheme;
		return m;
	}

	static void Play(Match m, IReadOnlyList<FighterInput> p1, IReadOnlyList<FighterInput>? p2 = null, int total = 0)
	{
		int n = Math.Max(total, Math.Max(p1.Count, p2?.Count ?? 0));
		for (int t = 0; t < n; t++)
			m.Step(t < p1.Count ? p1[t] : Idle, p2 != null && t < p2.Count ? p2[t] : Idle);
	}

	static List<FighterInput> Seq(string s, int facing = 1) => InputParserTests.Record(s, facing);

	/// <summary>First seed whose MODIFIER card is <paramref name="modifierId"/>.</summary>
	static (RewardDraft Draft, int Card) DraftWithModifier(RunState run, AbilityPool pool, string modifierId)
	{
		for (uint seed = 1; seed < 200; seed++)
		{
			var d = RewardDraft.Build(run, pool, "kitsune", seed);
			var c = d.Offer.Cards.FirstOrDefault(c => c.Kind == SimCardKind.Modifier && c.AbilityId == modifierId);
			if (c != null) return (d, c.Index);
		}
		throw new AssertFailed($"no seed offers {modifierId}");
	}

	static int CardOf(RewardDraft d, SimCardKind k) => d.Offer.Cards.Single(c => c.Kind == k).Index;

	// --- Loading through the schema loaders ----------------------------------------------------

	[Test]
	public static void PoolReadsFixturesThroughTheLoaders()
	{
		var pool = Pool();
		var fox = pool.Special("test-foxfire");
		Assert.True(fox != null, "Foxfire fixture loaded");
		Assert.Equal(SpecialSlot.C, fox!.Slot, "Foxfire goes in slot C (214 / back)");
		Assert.Equal("kitsune", fox.Source, "source");
		Assert.True(fox.HasProperty("projectile") && !pool.Special("test-rising-talisman")!.HasProperty("projectile"), "projectile property");
		Assert.True(pool.Modifier("test-will-o-wisp") != null && pool.Modifier("test-fox-patience") != null, "both modifiers");
		Assert.Equal(2, pool.Starters.Count(), "two starters (A1)");
		Assert.Equal("Your projectiles fly 30% faster.", pool.Modifier("test-will-o-wisp")!.CardPlain.Replace("TEST FIXTURE: your", "Your"), "card text from data");
	}

	[Test]
	public static void PoolPrefersDataFoldersWhenPresent()
	{
		string tmp = Path.Combine(Path.GetTempPath(), "yok47-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(Path.Combine(tmp, "modifiers"));
		try
		{
			string fx = ProjectSettings.GlobalizePath("res://tests/fixtures/rewards");
			string json = File.ReadAllText(Path.Combine(fx, "test-will-o-wisp.json")).Replace("test-will-o-wisp", "data-wisp");
			File.WriteAllText(Path.Combine(tmp, "modifiers", "data-wisp.json"), json);
			var pool = AbilityPool.Load(Path.Combine(tmp, "moves"), Path.Combine(tmp, "modifiers"),
				ProjectSettings.GlobalizePath(FightScene.FixtureSpecialsDir), fx);
			Assert.Equal("data-wisp", string.Join(",", pool.Modifiers.Select(m => m.Id)), "data/modifiers wins over the fixtures");
			Assert.True(pool.Special("test-foxfire") != null, "specials still fall back while data/moves has none");
		}
		finally { Directory.Delete(tmp, true); }
	}

	[Test]
	public static void LoaderRejectsBadModifiers()
	{
		void Bad(string json, string why)
		{
			try { ModifierLoader.Parse(json, "x"); }
			catch (FormatException) { return; }
			throw new AssertFailed($"accepted: {why}");
		}
		Bad("{\"kind\":\"special\",\"id\":\"x\",\"effects\":[{\"target\":\"damage\",\"op\":\"add\",\"value\":1}]}", "wrong kind");
		Bad("{\"kind\":\"modifier\",\"id\":\"x\",\"effects\":[]}", "no effects");
		// An effect the sim can't run fails when the pool builds it, not mid-run.
		var bad = ModifierLoader.Parse("{\"kind\":\"modifier\",\"id\":\"x\",\"source\":\"kitsune\",\"applies_to\":{\"requires\":[]},\"effects\":[{\"target\":\"projectile.nonsense\",\"op\":\"add\",\"value\":1}]}");
		try { new AbilityPool(Pool().Specials, new[] { bad }); }
		catch (FormatException) { return; }
		throw new AssertFailed("pool accepted an effect on a missing field");
	}

	// --- The offer (A7, A8) --------------------------------------------------------------------

	[Test]
	public static void KitsuneOfferIsNewUpgradeModifier()
	{
		var pool = Pool();
		var seenMods = new HashSet<string>();
		var seenUps = new HashSet<string>();
		for (uint seed = 1; seed <= 40; seed++)
		{
			var run = RunState.NewRun(pool);
			var d = RewardDraft.Build(run, pool, "kitsune", seed);
			var c = d.Offer.Cards;
			Assert.Equal("NewSpecial,Upgrade,Modifier", string.Join(",", c.Select(x => x.Kind)), "card order");
			Assert.Equal("test-foxfire", c[0].AbilityId, "NEW = Foxfire");
			Assert.Equal(SpecialSlot.C, c[0].Targets.Single().Slot, "into slot C");
			Assert.True(c[0].Frames.Contains("15 / 4 / 30"), "frame text from data");
			Assert.Equal(1, c[1].Targets.Count, "upgrade names one special");
			Assert.True(c[1].Frames.Contains("Lv 1 → Lv 2") && c[1].Frames.Contains("→"), "upgrade frame text");
			seenUps.Add(c[1].AbilityId);
			seenMods.Add(c[2].AbilityId);
			Assert.True(c.All(x => x.Plain.Length > 0 && x.Badge.Length > 0), "plain text and badge on every card (A12)");
		}
		Assert.Equal("test-fox-patience,test-will-o-wisp", string.Join(",", seenMods.OrderBy(x => x)), "both modifiers offered across seeds");
		Assert.Equal("test-rising-talisman,test-spirit-wave", string.Join(",", seenUps.OrderBy(x => x)), "both starters upgradable across seeds");
	}

	[Test]
	public static void OtherYokaiOfferOnlyTheirOwn()
	{
		var pool = Pool();
		var d = RewardDraft.Build(RunState.NewRun(pool), pool, "oni", 7);
		Assert.Equal("Upgrade", string.Join(",", d.Offer.Cards.Select(c => c.Kind)), "A8: no Kitsune abilities from an Oni; the upgrade stays (A7)");
	}

	// --- NEW: Foxfire in slot C, both schemes (A3, K3) -----------------------------------------

	[Test]
	public static void NewFoxfireFiresFromSlotCInKataAndKihon()
	{
		var pool = Pool();
		var run = RunState.NewRun(pool);
		var d = RewardDraft.Build(run, pool, "kitsune", 3);
		var r = d.Pick(CardOf(d, SimCardKind.NewSpecial));
		Assert.True(r.Ok, r.Error);
		Assert.Equal(SpecialSlot.C, r.Slot, "slot C");
		Assert.Equal(1, r.Level, "Lv 1 (A3)");
		Assert.True(!d.Pick(0).Ok, "one pick per draft");

		foreach (var (scheme, seq) in new[] { (ControlScheme.Kata, "2 1 4+LP"), (ControlScheme.Kihon, "4+S") })
		{
			var m = NextMatch(run, scheme);
			var spawned = new List<int>();
			m.ProjectileChanged += (mm, e) => { if (e.Kind == ProjectileEventKind.Spawn && e.MoveId == "test-foxfire") spawned.Add(mm.Tick); };
			var inputs = Seq(seq);
			Play(m, inputs);
			Assert.Equal(SpecialSlot.C, m.P1.ActiveSpecial, $"{scheme}: slot C fires");
			Assert.Equal("test-foxfire", m.P1.CurrentMove?.Id, $"{scheme}: Foxfire plays");
			Play(m, Array.Empty<FighterInput>(), total: 20);
			Assert.Equal(1, spawned.Count, $"{scheme}: Foxfire's projectile spawns");
			var p = m.Projectiles.Single();
			int x = p.X;
			Play(m, Array.Empty<FighterInput>(), total: 1);
			Assert.Equal(6 * SimConfig.Scale, p.X - x, $"{scheme}: slow projectile, 6 units a tick");
		}
	}

	// --- UPGRADE: Lv 2 from data (A4) ----------------------------------------------------------

	[Test]
	public static void UpgradeChangesFrameDataFromLevel2()
	{
		var pool = Pool();
		var run = RunState.NewRun(pool);
		var d = RewardDraft.Build(run, pool, "kitsune", 5, upgradeChoice: true);
		var card = d.Offer.Cards[CardOf(d, SimCardKind.Upgrade)];
		Assert.Equal("A,B", string.Join(",", card.Targets.Select(t => t.Slot)), "player may pick either starter");
		Assert.True(card.Targets[0].Frames.Contains("recovery 30 → 26"), $"Spirit Wave Lv 2 text from data: {card.Targets[0].Frames}");
		Assert.True(!d.Pick(card.Index).Ok, "needs a target when there are several");
		Assert.True(!d.Pick(card.Index, SpecialSlot.C).Ok, "C isn't a target");
		Assert.Equal(1, run[SpecialSlot.A]!.Level, "failed picks change nothing");
		var r = d.Pick(card.Index, SpecialSlot.A);
		Assert.True(r.Ok, r.Error);
		Assert.Equal(2, r.Level, "Lv 2");

		var m = NextMatch(run);
		var before = new Match(null, Moves(), Moves());
		RunState.NewRun(pool).ApplyTo(before.P1);
		Assert.Equal(30, before.P1.Specials[(int)SpecialSlot.A]!.Move.Recovery, "Lv 1 recovery");
		Assert.Equal(26, m.P1.Specials[(int)SpecialSlot.A]!.Move.Recovery, "Lv 2 recovery -4 in the next match");
		// Visible in the fight: the whole move is 4 frames shorter.
		Play(m, new List<FighterInput> { FighterInput.Special(SpecialSlot.A) });
		int ticks = 1;
		while (m.P1.State == FighterState.Attack) { Play(m, Array.Empty<FighterInput>(), total: 1); ticks++; }
		Assert.Equal(13 + 26, ticks - 1, "13 startup + 26 recovery, then idle");

		// Single-target upgrade card (the default) picks with a null target.
		var run2 = RunState.NewRun(pool);
		var d2 = RewardDraft.Build(run2, pool, "kitsune", 5);
		var up = d2.Offer.Cards[CardOf(d2, SimCardKind.Upgrade)];
		Assert.True(d2.Pick(up.Index).Ok, "null target = the only target");
		Assert.Equal(2, run2[up.Targets[0].Slot]!.Level, "levelled");
	}

	// --- MODIFIER: Will-o'-wisp, Fox's Patience (A2, A10) ---------------------------------------

	[Test]
	public static void WillOWispMakesTheProjectileFaster()
	{
		var pool = Pool();
		var run = RunState.NewRun(pool);
		var (d, ci) = DraftWithModifier(run, pool, "test-will-o-wisp");
		Assert.Equal("A", string.Join(",", d.Offer.Cards[ci].Targets.Select(t => t.Slot)), "only projectile specials (applies_to)");
		Assert.True(!d.Pick(ci, SpecialSlot.B).Ok, "Rising Talisman has no projectile");
		Assert.True(d.Pick(ci, SpecialSlot.A).Ok, "attach to Spirit Wave");

		int Speed(RunState r)
		{
			var m = NextMatch(r);
			Play(m, new List<FighterInput> { FighterInput.Special(SpecialSlot.A) }, total: 15);
			var p = m.Projectiles.Single();
			int x = p.X;
			Play(m, Array.Empty<FighterInput>(), total: 1);
			return (p.X - x) / SimConfig.Scale;
		}
		Assert.Equal(12, Speed(RunState.NewRun(pool)), "plain Spirit Wave: 12 units a tick");
		Assert.Equal(16, Speed(run), "Will-o'-wisp: 12 x 1.3 = 15.6 -> 16 units a tick");
		Assert.Equal(21, run[SpecialSlot.A]!.Data.Build(1, true, run[SpecialSlot.A]!.Modifier, null).Projectile!.Speed, "EX too: (12 + 4) x 1.3 = 20.8 -> 21");
	}

	[Test]
	public static void FoxsPatienceGivesMoreMeterOnBlock()
	{
		var pool = Pool();
		int MeterOnBlock(RunState r)
		{
			var m = NextMatch(r);
			var p2 = Enumerable.Repeat(new FighterInput(InputBits.Right), 200).ToList(); // P2 faces left: back = right
			int blocked = 0;
			m.ProjectileChanged += (_, e) => { if (e.Kind == ProjectileEventKind.Blocked) blocked++; };
			Play(m, new List<FighterInput> { FighterInput.Special(SpecialSlot.A) }, p2, total: 200);
			Assert.Equal(1, blocked, "blocked");
			return m.P1.Meter;
		}
		var run = RunState.NewRun(pool);
		var (d, ci) = DraftWithModifier(run, pool, "test-fox-patience");
		Assert.Equal("A,B", string.Join(",", d.Offer.Cards[ci].Targets.Select(t => t.Slot)), "applies to any special");
		Assert.True(d.Pick(ci, SpecialSlot.A).Ok, "attach to Spirit Wave");
		Assert.Equal(3, MeterOnBlock(RunState.NewRun(pool)), "C5: +3 when blocked");
		Assert.Equal(4, MeterOnBlock(run), "Fox's Patience: 3 x 1.25 -> 4");
		Assert.Equal(6, run[SpecialSlot.A]!.Data.Build(1, false, run[SpecialSlot.A]!.Modifier, null).MeterOnHit, "meter on hit unchanged");
	}

	[Test]
	public static void OneModifierPerSpecial()
	{
		var pool = Pool();
		var run = RunState.NewRun(pool);
		var (d, ci) = DraftWithModifier(run, pool, "test-fox-patience");
		Assert.True(d.Pick(ci, SpecialSlot.A).Ok, "first modifier");
		var (d2, ci2) = DraftWithModifier(run, pool, "test-fox-patience");
		Assert.Equal("B", string.Join(",", d2.Offer.Cards[ci2].Targets.Select(t => t.Slot)), "A2: Spirit Wave already carries one");
		Assert.True(!run.CanAttach(SpecialSlot.A, pool.Modifier("test-will-o-wisp")!), "no second modifier on A");
		// Will-o'-wisp has no eligible target left (only A has a projectile): it is not offered.
		for (uint seed = 1; seed < 50; seed++)
			Assert.True(RewardDraft.Build(run, pool, "kitsune", seed).Offer.Cards.All(c => c.AbilityId != "test-will-o-wisp"), "no unattachable card");
	}

	[Test]
	public static void LevelUpKeepsTheModifierAndNewSpecialReplacesSlot()
	{
		var pool = Pool();
		var run = RunState.NewRun(pool);
		run.Attach(SpecialSlot.A, pool.Modifier("test-will-o-wisp")!);
		run.LevelUp(SpecialSlot.A);
		var m = NextMatch(run);
		var sw = m.P1.Specials[(int)SpecialSlot.A]!;
		Assert.Equal(2, sw.Level, "Lv 2");
		Assert.Equal(26, sw.Move.Recovery, "Lv 2 step");
		Assert.Equal(16, sw.Move.Projectile!.Speed, "modifier kept");
		run.Equip(pool.Special("test-foxfire")!);
		run.Equip(pool.Special("test-foxfire")!);
		Assert.Equal(2, run[SpecialSlot.C]!.Level, "A3: drafting an owned special levels it");
	}

	// --- Health (C1, R3) ------------------------------------------------------------------------

	[Test]
	public static void HealthCarriesIntoTheNextMatch()
	{
		var pool = Pool();
		var run = RunState.NewRun(pool);
		Assert.Equal(1000, run.Health, "new run at 1,000 (C1)");
		run.AfterDuel(600);
		Assert.Equal(650, run.Health, "R3: left + 50");
		var m = NextMatch(run);
		Assert.Equal(650, m.P1.Health, "next match starts at 650");
		Assert.Equal(1000, m.P1.MaxHealth, "max stays 1,000");
		Assert.Equal(1000, m.P2.Health, "the yokai is unaffected");
		m.Reset();
		Assert.Equal(650, m.P1.Health, "a reset restarts at the carried health");
		run.AfterDuel(980);
		Assert.Equal(1000, run.Health, "capped at 1,000");
		run.AfterDuel(0, bound: false);
		Assert.Equal(0, run.Health, "no bind, no heal");
	}

	// --- Rounding, determinism, adapter -------------------------------------------------------

	[Test]
	public static void MulPctRoundsToNearest()
	{
		Assert.Equal(4, Effects.MulPct(3, 125), "3 x 1.25");
		Assert.Equal(16, Effects.MulPct(12, 130), "12 x 1.3");
		Assert.Equal(66, Effects.MulPct(60, 110), "60 x 1.1");
		Assert.Equal(99, Effects.MulPct(90, 110), "90 x 1.1");
		Assert.Equal(-4, Effects.MulPct(-3, 125), "symmetric for negatives");
	}

	[Test]
	public static void DraftAndNextFightAreDeterministic()
	{
		(string, ulong, ulong) RunOnce()
		{
			var pool = Pool();
			var run = RunState.NewRun(pool);
			run.AfterDuel(700);
			var d = RewardDraft.Build(run, pool, "kitsune", 42);
			string offer = string.Join("|", d.Offer.Cards.Select(c => $"{c.Kind}:{c.AbilityId}:{string.Join(",", c.Targets.Select(t => t.Slot))}:{c.Plain}:{c.Frames}"));
			var mod = d.Offer.Cards.Single(c => c.Kind == SimCardKind.Modifier);
			d.Pick(mod.Index, mod.Targets[0].Slot);
			var d2 = RewardDraft.Build(run, pool, "kitsune", 43);
			d2.Pick(0);
			var m = NextMatch(run, ControlScheme.Kihon);
			var rng = new SimRng(4747);
			for (int t = 0; t < 2000 && m.Phase != MatchPhase.Over; t++)
			{
				int r = rng.Next(8);
				var in1 = r == 0 ? FighterInput.Special((SpecialSlot)(1 + rng.Next(3)), false)
					: new FighterInput((InputBits)(rng.NextUInt() & (uint)(InputBits.Directions | InputBits.Attacks | InputBits.Special)));
				var in2 = new FighterInput((InputBits)(rng.NextUInt() & (uint)(InputBits.Directions | InputBits.Attacks)));
				m.Step(in1, in2);
			}
			return (offer, run.StateHash(), m.StateHash());
		}
		var a = RunOnce();
		var b = RunOnce();
		Assert.Equal(a.Item1, b.Item1, "same seed, same offer");
		Assert.Equal(a.Item2, b.Item2, "same picks, same run state");
		Assert.Equal(a.Item3, b.Item3, "same inputs, same fight");
	}

	[Test]
	public static void AdapterFeedsTheRewardScreen()
	{
		var pool = Pool();
		var run = RunState.NewRun(pool);
		var (d, ci) = DraftWithModifier(run, pool, "test-will-o-wisp");
		var src = new DraftRewardSource(d, run);
		Assert.Equal("Kitsune", src.Offer.Yokai, "yokai name");
		Assert.Equal("Forgive me, Kitsune. I'll set your spirit free.", ((IStorySource)src).BindingLine(src.Offer.Yokai), "YOK-44: binding line from data/story");
		Assert.Equal("NewMove,Upgrade,Modifier", string.Join(",", src.Offer.Cards.Select(c => c.Kind)), "UI kinds");
		Assert.Equal("NEW - Lv 1", src.Offer.Cards[0].Tag, "tag");
		Assert.True(src.Offer.Cards[ci].TargetPrompt != null, "modifier asks for a target");
		var t = src.Offer.Targets;
		Assert.Equal("A,B,C,D", string.Join(",", t.Select(x => x.Slot)), "four slots");
		Assert.True(t[0].Eligible && !t[1].Eligible && !t[2].Eligible, "only Spirit Wave can carry Will-o'-wisp");
		Assert.Equal("Can't carry this modifier", t[1].WhyNot, "why not");
		src.Pick(ci, "test-spirit-wave");
		Assert.Equal("test-will-o-wisp", run[SpecialSlot.A]!.Modifier?.Id, "attached through the adapter");
	}
}
