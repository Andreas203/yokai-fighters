using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YokaiFighters.Ai;
using YokaiFighters.Sim;

namespace YokaiFighters.Harness;

/// <summary>
/// YOK-26: everything a whole run is played from, loaded once per batch. Profiles are looked up the way the fight
/// scene does (the node fighter's profiles in <c>data/profiles/</c>, else the Kitsune TEST FIXTURE profiles).
/// </summary>
public sealed record HarnessContent(KitSources Sources, AbilityPool Pool, MapGraph Map, INodeSceneResolver Scenes,
	Func<string, BehaviourProfile[]> Profiles)
{
	/// <summary>Every profile in data/profiles/, where a node's named profile is looked up (<see cref="ProfilesForNode"/>).</summary>
	public BehaviourProfile[] AllProfiles { get; init; } = Array.Empty<BehaviourProfile>();
	/// <summary>YOK-177: the AI rolls standing-trigger habits (<see cref="ProfileAi.StandingHabits"/>); false = the rule
	/// before YOK-177, for tests pinning older whole-run hashes (AiGuardTests.LegacyOpponent).</summary>
	public bool StandingHabits { get; init; } = true;

	/// <summary>
	/// YOK-174: the profiles <paramref name="node"/> fights with: its plays-as yokai's (<see cref="Profiles"/>) plus the
	/// node's named profile from all of data/profiles/ when it is not one of them. Throws when it is found nowhere.
	/// </summary>
	public BehaviourProfile[] ProfilesForNode(NodeContext node) =>
		NodeProfiles.WithNamed(node.NodeId, Profiles(node.MovesAndProfilesOf), node.Profile, AllProfiles);

	/// <summary>YOK-157: tally each fight's hits, throws and habit rolls into <see cref="FightRecord.Log"/> (listening only; on by default).</summary>
	public bool HitLog { get; init; } = true;
	/// <summary>YOK-157: also keep every hit as a raw line (<c>hit_log</c> in the results; large, off by default). Needs <see cref="HitLog"/>.</summary>
	public bool RawHitLog { get; init; }
	/// <summary>YOK-179 (P5, Y3): every elder's twist (<c>data/twists/</c>, as <c>FightScene.Twists</c>); an elder node's fight gets its own.</summary>
	public ElderTwists Twists { get; init; } = ElderTwists.None;

	/// <summary>The game's content: <c>data/</c> (moves, modifiers, map, node scenes, profiles) with the TEST FIXTURE fallbacks.</summary>
	public static HarnessContent Load(string dataDir, string fixturesDir, string mapFile = "default-map.json")
	{
		var sources = KitSources.At(dataDir, fixturesDir);
		string mapDir = Path.Combine(dataDir, "map");
		var all = BehaviourProfile.LoadDirectory(Path.Combine(dataDir, "profiles"));
		var fixtures = BehaviourProfile.LoadDirectory(Path.Combine(fixturesDir, "profiles"));
		var content = new HarnessContent(sources, AbilityPool.Load(sources, Path.Combine(dataDir, "modifiers")),
			MapLoader.LoadFile(Path.Combine(mapDir, mapFile)), NodeSceneTable.Default(mapDir),
			fighter => ProfilesFor(fighter, all, fixtures))
		{ AllProfiles = all, Twists = ElderTwists.LoadDir(Path.Combine(dataDir, ElderTwists.Folder)) };
		content.CheckProfiles();
		return content;
	}

	/// <summary>
	/// YOK-174: every fight node of the map, resolved: each behaviour profile of the yokai it plays as names only
	/// moves its kit (<see cref="Kit.LoadAs"/>) has. Throws naming the node, profile and actions otherwise, so a
	/// profile never silently skips a move. The Tanuki's kit is built at the copy lock, so its node is checked when
	/// it is fought (<see cref="RunHarness.FightNode"/>).
	/// </summary>
	public void CheckProfiles()
	{
		var problems = new List<string>();
		foreach (var mapNode in Map.Nodes)
		{
			if (mapNode.Type is NodeTypeRegistry.Merchant or NodeTypeRegistry.Dojo) continue;
			var node = Scenes.Resolve(mapNode).Context;
			if (node.Kit is not null) continue;
			var kit = Kit.LoadAs(node.MovesAndProfilesOf, node.Fighter, Sources);
			var m = new Match(null, null, kit.Moves);
			kit.Equip(m.P2);
			var loadout = AiLoadout.From(m.P2);
			BehaviourProfile[] profiles;
			try { profiles = ProfilesForNode(node); } // the named profile (the apprentice's) is checked against this kit too
			catch (InvalidOperationException e) { problems.Add(e.Message); continue; }
			if (profiles.Length == 0) problems.Add($"node '{node.NodeId}': no behaviour profile for '{node.MovesAndProfilesOf}'");
			foreach (var p in profiles)
				if (loadout.Missing(p) is { Count: > 0 } missing)
					problems.Add($"node '{node.NodeId}' ({node.MovesAndProfilesOf} on {node.Fighter}'s kit): profile '{p.Id}' names {string.Join(", ", missing)}, which the kit does not have");
		}
		if (problems.Count > 0) throw new InvalidOperationException("YOK-174 profile check: " + string.Join("; ", problems.Distinct()));
	}

	/// <summary>Same lookup as <c>FightScene.LoadProfiles</c>: own profiles, else the fixtures; sorted by temperament (aggressive first).</summary>
	public static BehaviourProfile[] ProfilesFor(string fighter, BehaviourProfile[] all, BehaviourProfile[] fixtures) =>
		NodeProfiles.Of(fighter, all, fixtures);
}

/// <summary>
/// YOK-26 (G1, R5): the headless whole-run harness. Plays first runs through <see cref="RunSession"/> exactly as the
/// game walks them: the <see cref="RunBot"/> picks the route, each fight node is a <see cref="Match"/> built by
/// <see cref="NodeFight"/> (so the Tanuki fights with <see cref="TanukiKit"/> and the copy locked on entry, T0/T1)
/// between the <see cref="NewcomerBot"/> (Ryo, Kihon) and the node's <see cref="ProfileAi"/> at the node's Y4
/// reaction, wins go through <see cref="RunSession.WinFight"/> and the draft, stops take the heal or rest.
/// No Godot, no rendering, no wall clock: the same seed gives the same <see cref="RunRecord"/>.
/// </summary>
public static class RunHarness
{
	/// <summary>Plays one first run from <paramref name="seed"/>.</summary>
	/// <remarks><paramref name="fight"/> replaces the fight (tests of the run plumbing); null = <see cref="FightNode"/>.</remarks>
	public static RunRecord PlayRun(HarnessContent content, BotConfig bot, uint seed, FightRunner? fight = null)
	{
		fight ??= FightNode;
		var session = RunSession.Start(content.Pool, content.Map, content.Scenes, seed);
		var runBot = new RunBot(bot.Run, Mix(seed, 0x524F5554u));
		var rec = new RunRecord { Seed = seed, Level = bot.Level };
		while (!session.IsOver)
		{
			string id = runBot.ChooseRoute(session, session.Reachable());
			var entered = session.Enter(id);
			if (!entered.Ok) throw new InvalidOperationException($"seed {seed}: enter {id}: {entered.Error}");
			var node = session.Node!;
			var n = new NodeRecord
			{
				Id = node.NodeId, Row = node.Row, Type = node.Type, Yokai = node.Yokai, Fighter = node.Fighter, PlaysAs = node.MovesAndProfilesOf, Tier = node.Tier,
				StandIn = node.StandIn, Elder = node.Elder, ArrivalHealth = session.Run.Health, ArrivalCoins = session.Run.Coins,
			};
			rec.Nodes.Add(n);
			if (node.Type == NodeTypeRegistry.Boss && session.CopyLocked) rec.CopyLocked = session.LockedCopy;

			if (node.Type == NodeTypeRegistry.Merchant) n.Action = runBot.AtMerchant(session, n.Bought);
			else if (node.Type == NodeTypeRegistry.Dojo) n.Action = runBot.AtDojo(session);
			else
			{
				n.Action = "fight";
				n.Fight = fight(content, bot, session, seed);
				rec.FightTicks += n.Fight.Ticks;
				if (!n.Fight.Won)
				{
					// YOK-179: a stall (no KO by the safety cap) is not a loss; the walk still has to end, so the session
					// closes it the way a loss does, and the record says stalled.
					rec.Stalled = n.Fight.Stalled;
					session.LoseFight();
					break;
				}
				var draft = session.WinFight(n.Fight.HealthLeft);
				n.CoinsWon = session.CoinsWon;
				if (draft is not null)
				{
					var (card, target) = runBot.ChooseCard(draft.Offer);
					var pick = draft.Pick(card, target);
					if (!pick.Ok) throw new InvalidOperationException($"seed {seed}: node {node.NodeId}: pick refused: {pick.Error}");
					n.Card = new CardRecord(pick.Card!.Badge, pick.Card.AbilityId, pick.Slot, pick.Level,
						draft.Offer.Cards.Select(x => $"{x.Badge} {x.AbilityId}").ToArray());
				}
			}
			n.LeaveHealth = session.Run.Health;
			var done = session.CompleteNode();
			if (!done.Ok) throw new InvalidOperationException($"seed {seed}: complete {node.NodeId}: {done.Error}");
		}
		var end = session.Ended ?? throw new InvalidOperationException($"seed {seed}: the run did not end");
		rec.Won = end.Won;
		rec.RowReached = end.RowReached;
		rec.EndNode = end.NodeId;
		rec.FinalHealth = session.Run.Health;
		rec.Coins = session.Run.Coins;
		rec.MenuSeconds = rec.Nodes.Count * bot.Run.MenuSecondsPerNode;
		rec.SessionHash = session.StateHash();
		return rec;
	}

	/// <summary>One node's fight: Ryo (bot) vs the node's opponent (profile AI at the node's reaction frames), to a KO or the safety cap.</summary>
	public static FightRecord FightNode(HarnessContent content, BotConfig bot, RunSession session, uint runSeed) =>
		PlayFight(content, bot, session, runSeed, NewNodeMatch(content, bot, session));

	/// <summary>
	/// The entered node's match as the fight scene builds it (<c>FightScene.NewDuel</c>): <see cref="NodeFight.NewMatch"/>,
	/// then the node's elder twist on P2 through <see cref="ElderTwists.Apply"/> (YOK-179; YOK-90: from the node's elder,
	/// whoever stands in; cleared off an elder node).
	/// </summary>
	public static Match NewNodeMatch(HarnessContent content, BotConfig bot, RunSession session)
	{
		var m = NodeFight.NewMatch(content.Sources, session.Run, session, session.Node!.Fighter, bot.Scheme);
		content.Twists.Apply(m, session.Node);
		return m;
	}

	/// <summary>Plays <paramref name="m"/> (the entered node's match, <see cref="NewNodeMatch"/> or the fight scene's) to a KO or <see cref="BotFightConfig.MaxFightTicks"/>.</summary>
	public static FightRecord PlayFight(HarnessContent content, BotConfig bot, RunSession session, uint runSeed, Match m)
	{
		var node = session.Node!;
		var profiles = content.ProfilesForNode(node); // YOK-174: the yokai's own profiles (even on a stand-in kit) plus the node's named one
		if (profiles.Length == 0) throw new InvalidOperationException($"no behaviour profile for '{node.MovesAndProfilesOf}'");
		int pi = Math.Max(0, NodeTemperament.Pick(profiles, node, session.Seed)); // YOK-174, Y1/Y2: named profile, else the seeded 50/50 temperament roll
		uint step = (uint)session.Step;
		var loadout = AiLoadout.From(m.P2);
		if (loadout.Missing(profiles[pi]) is { Count: > 0 } missing) // YOK-174: never skip a named move silently
			throw new InvalidOperationException($"node '{node.NodeId}': profile '{profiles[pi].Id}' names {string.Join(", ", missing)}, which {node.MovesAndProfilesOf} on {node.Fighter}'s kit does not have");
		var ai = new ProfileAi(profiles[pi], 1, loadout, Mix(runSeed, 0xA1000000u + step), node.ReactionFrames,
			AiFightTiers.Of(node), standingHabits: content.StandingHabits); // YOK-57, E22: EX/burst only in elder and Tanuki fights, as in the fight scene
		var ryo = new NewcomerBot(bot.Fight, BotKit.From(m.P1), Mix(runSeed, 0xB0700000u + step));
		FightLog? log = null;
		if (content.HitLog) { log = new FightLog(content.RawHitLog); log.Attach(m, ai); }
		int ryoStart = m.P1.Health, foeStart = m.P2.Health;
		int twistFired = 0;
		m.TwistTriggered += (_, _) => twistFired++; // YOK-179: listening only
		int t = 0;
		while (m.Phase != MatchPhase.Over && t < bot.Fight.MaxFightTicks)
		{
			var view = AiView.Capture(m);
			var ryoIn = ryo.Next(view);
			var aiIn = ai.Next(view);
			log?.AfterAi(ai, m.Tick);
			m.Step(ryoIn, aiIn);
			t++;
		}
		bool stalled = m.Phase != MatchPhase.Over; // YOK-179: no round timer (designer 2026-10-21); the cap only stops a stall
		return new FightRecord
		{
			Profile = profiles[pi].Id,
			ReactionFrames = ai.ReactionFrames,
			Won = !stalled && m.Winner == 0, // a double KO is a loss, as in the fight scene
			Stalled = stalled,
			Twist = m.P2.Twist?.Id,
			TwistFired = twistFired,
			Ticks = t,
			DamageTaken = ryoStart - m.P1.Health,
			DamageDealt = foeStart - m.P2.Health,
			HealthLeft = m.P1.Health,
			Bot = ryo.Stats,
			OpponentCopiedSlot = m.P2.CopiedSlot,
			MatchHash = m.StateHash(),
			Log = log,
		};
	}

	/// <summary><paramref name="runs"/> first runs, run i on seed <paramref name="baseSeed"/> + i.</summary>
	public static BatchResult PlayBatch(HarnessContent content, BotConfig bot, uint baseSeed, int runs)
	{
		var batch = new BatchResult(bot, baseSeed);
		for (int i = 0; i < runs; i++) batch.Runs.Add(PlayRun(content, bot, baseSeed + (uint)i));
		return batch;
	}

	/// <summary>
	/// YOK-170: a mixed batch. Run i is played on seed <paramref name="baseSeed"/> + i by level i mod the level count (file
	/// order), so it is the same run as run i of that level's own batch from the same seed.
	/// </summary>
	public static BatchResult PlayMixed(HarnessContent content, BotLevels levels, uint baseSeed, int runs)
	{
		var batch = new BatchResult(levels.Levels[0], baseSeed, $"{levels.Id}-mixed");
		for (int i = 0; i < runs; i++) batch.Runs.Add(PlayRun(content, levels.Levels[i % levels.Levels.Count], baseSeed + (uint)i));
		return batch;
	}

	/// <summary>A seed for one consumer of a run (route, AI, bot), so changing one never shifts another's rolls.</summary>
	public static uint Mix(uint seed, uint tag)
	{
		ulong h = Fnv.Mix(Fnv.Mix(Fnv.Offset, unchecked((int)seed)), unchecked((int)tag));
		uint v = (uint)(h ^ (h >> 32));
		return v == 0 ? 1u : v;
	}
}

/// <summary>Plays the entered node's fight and reports it (the harness calls <see cref="RunHarness.FightNode"/>).</summary>
public delegate FightRecord FightRunner(HarnessContent content, BotConfig bot, RunSession session, uint runSeed);

/// <summary>The card picked and, in offer order, every card offered ("BADGE ability-id").</summary>
public sealed record CardRecord(string Badge, string AbilityId, SpecialSlot Slot, int Level, IReadOnlyList<string> Offered);

public sealed class FightRecord
{
	public required string Profile { get; init; }
	public int ReactionFrames { get; init; }
	public bool Won { get; init; }
	/// <summary>YOK-179: no KO by the safety cap (<c>max_fight_ticks</c>). Not a loss: fights go to KO (no round timer, designer 2026-10-21).</summary>
	public bool Stalled { get; init; }
	/// <summary>YOK-179: the elder twist the opponent fought with (P5, Y3), null off an elder node.</summary>
	public string? Twist { get; init; }
	/// <summary>YOK-179: how many times the twist changed what happened (<see cref="Match.TwistTriggered"/>).</summary>
	public int TwistFired { get; init; }
	public int Ticks { get; init; }
	public int DamageTaken { get; init; }
	public int DamageDealt { get; init; }
	public int HealthLeft { get; init; }
	public required BotStats Bot { get; init; }
	/// <summary>T0: the slot of the Tanuki's copied special (its <see cref="TanukiKit"/>); None for every other opponent.</summary>
	public SpecialSlot OpponentCopiedSlot { get; init; }
	public ulong MatchHash { get; init; }
	/// <summary>YOK-157: hits, throws and habit rolls of this fight; null when <see cref="HarnessContent.HitLog"/> is off.</summary>
	public FightLog? Log { get; init; }
}

public sealed class NodeRecord
{
	public required string Id { get; init; }
	public int Row { get; init; }
	public required string Type { get; init; }
	public string? Yokai { get; init; }
	public required string Fighter { get; init; }
	/// <summary>YOK-174: the yokai whose move files and profiles the opponent fights with (<see cref="NodeContext.PlaysAs"/>).</summary>
	public string PlaysAs { get; init; } = "";
	public required string Tier { get; init; }
	public bool StandIn { get; init; }
	/// <summary>YOK-179: the map node's elder flag (<see cref="NodeContext.Elder"/>), which decides its twist (<see cref="ElderTwists.For"/>).</summary>
	public bool Elder { get; init; }
	public int ArrivalHealth { get; init; }
	public int ArrivalCoins { get; init; }
	public int LeaveHealth { get; set; }
	public int CoinsWon { get; set; }
	/// <summary>fight / heal / rest / skip.</summary>
	public string Action { get; set; } = "";
	/// <summary>Shop purchases at a merchant stop (special or modifier ids, in order).</summary>
	public List<string> Bought { get; } = new();
	public FightRecord? Fight { get; set; }
	public CardRecord? Card { get; set; }
}

public sealed class RunRecord
{
	public uint Seed { get; init; }
	/// <summary>YOK-170: the bot's skill level for this run (null: a bot file played on its own).</summary>
	public string? Level { get; init; }
	public bool Won { get; set; }
	/// <summary>YOK-179: the run ended on a stalled fight (no KO by the safety cap), not a loss.</summary>
	public bool Stalled { get; set; }
	public int RowReached { get; set; }
	public string EndNode { get; set; } = "";
	public int FinalHealth { get; set; }
	public int Coins { get; set; }
	public int FightTicks { get; set; }
	public int MenuSeconds { get; set; }
	public CopyTarget? CopyLocked { get; set; }
	public ulong SessionHash { get; set; }
	public List<NodeRecord> Nodes { get; } = new();

	public NodeRecord? Boss => Nodes.Find(n => n.Type == NodeTypeRegistry.Boss);
	public bool ReachedTanuki => Boss is not null;
	public bool BeatTanuki => Boss?.Fight is { Won: true };
	/// <summary>R9 estimate: fight time + the configured time per node outside fights.</summary>
	public double EstimatedMinutes => FightTicks / 3600.0 + MenuSeconds / 60.0;
}

public sealed class BatchResult
{
	/// <summary>The bot (a mixed batch: its first level; each run records its own <see cref="RunRecord.Level"/>).</summary>
	public BotConfig Bot { get; }
	/// <summary>The results file's <c>bot</c> and file-name stem: the bot id, or <c>&lt;levels id&gt;-mixed</c> (YOK-170).</summary>
	public string Name { get; }
	public uint BaseSeed { get; }
	public List<RunRecord> Runs { get; } = new();
	public BatchResult(BotConfig bot, uint baseSeed, string? name = null) { Bot = bot; BaseSeed = baseSeed; Name = name ?? bot.Id; }
}
