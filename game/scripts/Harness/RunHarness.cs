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
	/// <summary>The game's content: <c>data/</c> (moves, modifiers, map, node scenes, profiles) with the TEST FIXTURE fallbacks.</summary>
	public static HarnessContent Load(string dataDir, string fixturesDir, string mapFile = "default-map.json")
	{
		var sources = KitSources.At(dataDir, fixturesDir);
		string mapDir = Path.Combine(dataDir, "map");
		var all = BehaviourProfile.LoadDirectory(Path.Combine(dataDir, "profiles"));
		var fixtures = BehaviourProfile.LoadDirectory(Path.Combine(fixturesDir, "profiles"));
		return new HarnessContent(sources, AbilityPool.Load(sources, Path.Combine(dataDir, "modifiers")),
			MapLoader.LoadFile(Path.Combine(mapDir, mapFile)), NodeSceneTable.Default(mapDir),
			fighter => ProfilesFor(fighter, all, fixtures));
	}

	/// <summary>Same lookup as <c>FightScene.LoadProfiles</c>: own profiles, else the fixtures; sorted by temperament (aggressive first).</summary>
	public static BehaviourProfile[] ProfilesFor(string fighter, BehaviourProfile[] all, BehaviourProfile[] fixtures)
	{
		var own = Array.FindAll(all, p => p.Yokai == fighter);
		if (own.Length == 0) own = (BehaviourProfile[])fixtures.Clone();
		Array.Sort(own, (a, b) => string.CompareOrdinal(a.Temperament, b.Temperament));
		return own;
	}
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
		var rec = new RunRecord { Seed = seed };
		while (!session.IsOver)
		{
			string id = runBot.ChooseRoute(session, session.Reachable());
			var entered = session.Enter(id);
			if (!entered.Ok) throw new InvalidOperationException($"seed {seed}: enter {id}: {entered.Error}");
			var node = session.Node!;
			var n = new NodeRecord
			{
				Id = node.NodeId, Row = node.Row, Type = node.Type, Yokai = node.Yokai, Fighter = node.Fighter, Tier = node.Tier,
				StandIn = node.StandIn, ArrivalHealth = session.Run.Health, ArrivalCoins = session.Run.Coins,
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

	/// <summary>One node's fight: Ryo (bot) vs the node's opponent (profile AI at the node's reaction frames), to a KO or the cap.</summary>
	public static FightRecord FightNode(HarnessContent content, BotConfig bot, RunSession session, uint runSeed)
	{
		var node = session.Node!;
		var m = NodeFight.NewMatch(content.Sources, session.Run, session, node.Fighter, bot.Scheme);
		var profiles = content.Profiles(node.Fighter);
		if (profiles.Length == 0) throw new InvalidOperationException($"no behaviour profile for '{node.Fighter}'");
		int pi = node.Profile is { } want ? Math.Max(0, Array.FindIndex(profiles, p => p.Id == want)) : 0;
		uint step = (uint)session.Step;
		var ai = new ProfileAi(profiles[pi], 1, AiLoadout.From(m.P2), Mix(runSeed, 0xA1000000u + step), node.ReactionFrames,
			AiFightTiers.Of(node)); // YOK-57, E22: EX/burst only in elder and Tanuki fights, as in the fight scene
		var ryo = new NewcomerBot(bot.Fight, BotKit.From(m.P1), Mix(runSeed, 0xB0700000u + step));
		int ryoStart = m.P1.Health, foeStart = m.P2.Health;
		int t = 0;
		while (m.Phase != MatchPhase.Over && t < bot.Fight.MaxFightTicks)
		{
			var view = AiView.Capture(m);
			m.Step(ryo.Next(view), ai.Next(view));
			t++;
		}
		bool timedOut = m.Phase != MatchPhase.Over;
		return new FightRecord
		{
			Profile = profiles[pi].Id,
			ReactionFrames = ai.ReactionFrames,
			Won = !timedOut && m.Winner == 0, // a double KO is a loss, as in the fight scene
			TimedOut = timedOut,
			Ticks = t,
			DamageTaken = ryoStart - m.P1.Health,
			DamageDealt = foeStart - m.P2.Health,
			HealthLeft = m.P1.Health,
			Bot = ryo.Stats,
			OpponentCopiedSlot = m.P2.CopiedSlot,
			MatchHash = m.StateHash(),
		};
	}

	/// <summary><paramref name="runs"/> first runs, run i on seed <paramref name="baseSeed"/> + i.</summary>
	public static BatchResult PlayBatch(HarnessContent content, BotConfig bot, uint baseSeed, int runs)
	{
		var batch = new BatchResult(bot, baseSeed);
		for (int i = 0; i < runs; i++) batch.Runs.Add(PlayRun(content, bot, baseSeed + (uint)i));
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
	public bool TimedOut { get; init; }
	public int Ticks { get; init; }
	public int DamageTaken { get; init; }
	public int DamageDealt { get; init; }
	public int HealthLeft { get; init; }
	public required BotStats Bot { get; init; }
	/// <summary>T0: the slot of the Tanuki's copied special (its <see cref="TanukiKit"/>); None for every other opponent.</summary>
	public SpecialSlot OpponentCopiedSlot { get; init; }
	public ulong MatchHash { get; init; }
}

public sealed class NodeRecord
{
	public required string Id { get; init; }
	public int Row { get; init; }
	public required string Type { get; init; }
	public string? Yokai { get; init; }
	public required string Fighter { get; init; }
	public required string Tier { get; init; }
	public bool StandIn { get; init; }
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
	public bool Won { get; set; }
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
	public BotConfig Bot { get; }
	public uint BaseSeed { get; }
	public List<RunRecord> Runs { get; } = new();
	public BatchResult(BotConfig bot, uint baseSeed) { Bot = bot; BaseSeed = baseSeed; }
}
