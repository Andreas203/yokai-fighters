using System;
using System.Collections.Generic;

namespace YokaiFighters.Sim;

public enum RunOutcome { Won, Lost }

/// <summary>How a run ended: the outcome, the row Ryo reached and the node it ended on.</summary>
public sealed record RunEnd(RunOutcome Outcome, int RowReached, string NodeId)
{
	public bool Won => Outcome == RunOutcome.Won;
	/// <summary>YOK-99: the run ended on the Tanuki's node (<see cref="RunSession.IsCopyLockNode"/>).</summary>
	public bool AtTanuki { get; init; }
	/// <summary>YOK-99, Y5: a Tanuki win, the one thing that raises the Tanuki pressure on later runs.</summary>
	public bool BeatTanuki => Won && AtTanuki;
}

/// <summary>
/// YOK-77: one run from the first node to its end (pure C#, deterministic, no Godot). Owns the
/// <see cref="RunState"/> (health C1, slots) and its <see cref="RunMap"/>; scenes only call it:
/// <see cref="Enter"/> a reachable node → the scene named by <see cref="Entered"/> runs it →
/// <see cref="WinFight"/> (R3 heal, R8 coins, the A7 draft when the node binds) then <see cref="CompleteNode"/>, or
/// <see cref="LoseFight"/>. The run is won when the completed node has no route out, lost on the first lost fight.
/// <para>
/// All of its state is plain data: <see cref="Seed"/>, the run state, <see cref="RunMap.Snapshot"/>,
/// <see cref="Ended"/>, once Ryo has entered the Tanuki's node the copy lock (<see cref="CopyLocked"/>,
/// <see cref="LockedCopy"/>, T1), while Ryo stands at a merchant that stop's <see cref="Merchant.Choice"/>
/// (R6; the constructor's and <see cref="Restore"/>'s <c>merchantChoice</c> puts it back), and while a dojo node
/// is entered its <see cref="Sim.Dojo.Choice"/> (R7; put back with <c>Dojo.Restore</c>). A later save / Continue
/// (YOK-73) writes those; one that drops a stop's choice hands its heal or rest out twice. The object must
/// outlive scene changes: the boot flow holds it, never a node's scene.
/// </para>
/// </summary>
public sealed class RunSession
{
	public RunState Run { get; }
	public RunMap Map { get; }
	public AbilityPool Pool { get; }
	public INodeSceneResolver Scenes { get; }
	/// <summary>Seeds the reward drafts (node n of the walk uses Seed + n): same seed and picks, same run.</summary>
	public uint Seed { get; }
	/// <summary>
	/// YOK-98, R7: the hand-authored trials the dojo may offer (<see cref="TrialLoader.LoadDirectory"/> of data/trials/).
	/// Empty = no trial is offered anywhere. Set by whoever starts or restores the session; read by each dojo stop.
	/// </summary>
	public IReadOnlyList<TrialData> Trials { get; set; } = Array.Empty<TrialData>();

	/// <summary>The node Ryo is on and the scene that runs it; null on the map (between nodes).</summary>
	public NodeScene? Entered { get; private set; }
	public NodeContext? Node => Entered?.Context;
	/// <summary>The reward offer of the node just won, until the node is completed. Null when it gives none.</summary>
	public RewardDraft? Draft { get; private set; }
	Dojo? _dojo;
	/// <summary>YOK-84, R7: the dojo stop Ryo is at (its rest and its one choice); null off a dojo node or once the run is over.</summary>
	public Dojo? Dojo => IsOver ? null : _dojo;
	/// <summary>R7: a stop takes a choice only while it is the entered node's stop and the run is not over (a kept reference is refused).</summary>
	bool IsOpenDojo(Dojo stop) => !IsOver && ReferenceEquals(stop, _dojo);
	/// <summary>YOK-83: the merchant stop Ryo is at (R6: its one choice, heal or shop); null on any other node.</summary>
	public Merchant? Merchant { get; private set; }
	public RunEnd? Ended { get; private set; }
	/// <summary>R8: the coins the entered node's won fight paid; 0 before the win, on the map and for a node that pays none.</summary>
	public int CoinsWon { get; private set; }
	bool _paid; // R8: a node pays once, however often its win is reported
	/// <summary>
	/// R8, R3: the entered node's fight is won and paid (its coins and bind heal are in the run state). Part of the
	/// run save (YOK-128), so a run saved after the win and continued is not paid or healed again. False on the map.
	/// </summary>
	public bool NodePaid => _paid;
	public bool IsOver => Ended is not null;
	/// <summary>1 for the first node of the walk, 2 for the second... (completed nodes + the one entered).</summary>
	public int Step => Map.Visited.Count + (Map.InNode ? 1 : 0);
	/// <summary>Nodes in a whole walk (one per row, R1).</summary>
	public int Steps => Map.Graph.Rows.Count;

	/// <summary>
	/// T1: true from the moment Ryo enters the Tanuki's node (a node of type <see cref="NodeTypeRegistry.Boss"/>)
	/// to the end of the run. False before: nothing is locked until then.
	/// </summary>
	public bool CopyLocked { get; private set; }
	/// <summary>
	/// T1: the Tanuki's copy, fixed when Ryo entered its node: <see cref="CopyRule.Target"/> (T2-T4) as the run
	/// stood at that moment. It is a copy of the answer, not a view of the slots: levelling, replacing or
	/// attaching afterwards does not change it. Null before the lock, and after it only when Ryo held no
	/// special at all (T5 keeps that from happening in a real run). Plain data: slot, special and level.
	/// </summary>
	public CopyTarget? LockedCopy { get; private set; }

	/// <summary>
	/// A node was entered. For the Tanuki's node the copy is already locked when this is raised (T1), so a
	/// listener reads <see cref="LockedCopy"/>.
	/// </summary>
	public event Action<NodeContext>? NodeEntered;
	/// <summary>A node was completed (raised before <see cref="RunEnded"/> on the last one; <see cref="IsOver"/> is already true then).</summary>
	public event Action<NodeContext>? NodeCompleted;
	public event Action<RunEnd>? RunEnded;

	/// <summary>
	/// A session on <paramref name="run"/> and its map. Refused (ArgumentException) for a walk that has already
	/// entered the Tanuki's node: its copy was locked at that entry (T1) and can't be worked out from the slots
	/// as they stand now, so such a walk is put back with <see cref="Restore"/> and its saved lock.
	/// <paramref name="merchantChoice"/> puts back a saved merchant stop's choice (R6).
	/// </summary>
	public RunSession(RunState run, AbilityPool pool, INodeSceneResolver scenes, uint seed, MerchantChoice merchantChoice = MerchantChoice.None)
		: this(run, pool, scenes, seed, false, null, merchantChoice) { }

	RunSession(RunState run, AbilityPool pool, INodeSceneResolver scenes, uint seed, bool copyLocked, CopyTarget? lockedCopy, MerchantChoice merchantChoice,
		bool nodePaid = false)
	{
		Run = run;
		Map = run.Map ?? throw new ArgumentException("the run has no map (RunState.StartMap)", nameof(run));
		Pool = pool;
		Scenes = scenes;
		Seed = seed;
		// T1: the lock is taken by Enter and by nothing else, so a walk and its lock must agree from the start.
		bool passed = PassedLockPoint(Map);
		if (passed && !copyLocked)
			throw new ArgumentException("the walk has entered the Tanuki's node: its copy lock must be given (RunSession.Restore, T1)", nameof(run));
		if (!passed && (copyLocked || lockedCopy is not null))
			throw new ArgumentException("the walk has not entered the Tanuki's node: nothing is locked before it (T1)", nameof(lockedCopy));
		if (lockedCopy is not null)
		{
			if (lockedCopy.Data is null) throw new ArgumentException("the locked copy names no special", nameof(lockedCopy));
			if (!ReferenceEquals(pool.Special(lockedCopy.Data.Id), lockedCopy.Data))
				throw new ArgumentException($"{lockedCopy.Data.Id} is not a special of this run's pool", nameof(lockedCopy));
			if (lockedCopy.Slot == SpecialSlot.None || lockedCopy.Slot != lockedCopy.Data.Slot)
				throw new ArgumentException($"{lockedCopy.Data.Id} does not sit in slot {lockedCopy.Slot} (T2)", nameof(lockedCopy));
			if (lockedCopy.Level < 1 || lockedCopy.Level > lockedCopy.Data.MaxLevel)
				throw new ArgumentException($"{lockedCopy.Data.Id} has no level {lockedCopy.Level} (T4)", nameof(lockedCopy));
		}
		CopyLocked = copyLocked;
		LockedCopy = lockedCopy;
		foreach (var n in Map.Graph.Nodes) scenes.Resolve(n); // a node the resolver cannot place fails here, not mid-run
		if (Map.InNode) Entered = scenes.Resolve(Map.CurrentNode!); // a restored walk
		if (Entered?.Context.Type == NodeTypeRegistry.Dojo) _dojo = new Dojo(Run, Entered.Context.NodeId, IsOpenDojo, () => Trials); // its saved choice: Dojo.Restore
		Merchant = MerchantAt(Node);
		if (merchantChoice != MerchantChoice.None && Merchant?.Restore(merchantChoice) is not { Ok: true })
			throw new ArgumentException($"merchant choice '{merchantChoice}' does not fit this walk (R6)", nameof(merchantChoice));
		if (nodePaid)
		{
			if (Entered is null) throw new ArgumentException("a paid node with no node entered (R8)", nameof(nodePaid));
			// A7: a won node that binds and has a next row owes a draft, which a save never holds (Capture refuses an
			// open offer), so a paid flag there would skip the offer: refused.
			if (Entered.Context.Binds && Map.Graph.Next(Entered.Context.NodeId).Count > 0)
				throw new ArgumentException($"node '{Entered.Context.NodeId}' offers a draft when won: it can't be saved paid (A7)", nameof(nodePaid));
			(_paid, CoinsWon) = (true, Entered.Context.Coins); // YOK-128: the saved win, already in health and coins
		}
	}

	/// <summary>
	/// Puts back a saved session (the later save, YOK-73): the run with its restored map, plus the copy lock as
	/// it was saved (<see cref="CopyLocked"/>, <see cref="LockedCopy"/>; null copy = a lock that found no
	/// special). The lock is never worked out again from the slots. Throws ArgumentException when the lock and
	/// the walk disagree (locked before the walk entered the Tanuki's node, or not locked after it, T1), or the
	/// copy can't be a target: a special that is not this pool's, in a slot not its own, at a level it lacks.
	/// <paramref name="nodePaid"/> (YOK-128, R8): the entered node's fight was won and paid before the save
	/// (<see cref="NodePaid"/>); refused when no node is entered.
	/// </summary>
	public static RunSession Restore(RunState run, AbilityPool pool, INodeSceneResolver scenes, uint seed, bool copyLocked, CopyTarget? lockedCopy,
		MerchantChoice merchantChoice = MerchantChoice.None, bool nodePaid = false) =>
		new(run, pool, scenes, seed, copyLocked, lockedCopy, merchantChoice, nodePaid);

	/// <summary>T1: is this the Tanuki's node? Decided by the node's type from the map data, never by a yokai or node id.</summary>
	public static bool IsCopyLockNode(NodeContext node) => node.Type == NodeTypeRegistry.Boss;

	/// <summary>
	/// T1: locks the copy if <paramref name="node"/> is the Tanuki's node and nothing is locked yet. Called by
	/// <see cref="Enter"/> only. Once per run: a session whose walk is past the lock point is always built
	/// locked, so a later boss-type node can never lock late.
	/// </summary>
	void LockCopyOnEntering(NodeContext node)
	{
		if (CopyLocked || !IsCopyLockNode(node)) return;
		LockedCopy = CopyRule.Target(Run);
		CopyLocked = true;
	}

	/// <summary>True when the walk has entered a node of the Tanuki's type (now or earlier): the lock point is behind Ryo.</summary>
	internal static bool PassedLockPoint(RunMap map) // internal: the save's restore asks the same question (YOK-87)
	{
		if (map.Current is { } cur && map.Graph.Node(cur).Type == NodeTypeRegistry.Boss) return true;
		foreach (string id in map.Visited)
			if (map.Graph.Node(id).Type == NodeTypeRegistry.Boss) return true;
		return false;
	}

	/// <summary>A new run on <paramref name="graph"/>: the starters at Lv 1, full health (A1, C1), no node picked.</summary>
	public static RunSession Start(AbilityPool pool, MapGraph graph, INodeSceneResolver scenes, uint seed) =>
		new(RunState.NewRun(pool, graph), pool, scenes, seed);

	/// <summary>Node ids the player may pick now (none while a node is entered or once the run is over).</summary>
	public IReadOnlyList<string> Reachable() => IsOver ? Array.Empty<string>() : Map.Reachable();

	/// <summary>The player picked a node on the map. A refused pick changes nothing.</summary>
	public MapResult Enter(string nodeId)
	{
		if (IsOver) return MapResult.Fail("the run is over");
		var step = Map.Select(nodeId);
		if (!step.Ok) return step;
		Entered = Scenes.Resolve(Map.Graph.Node(nodeId));
		_dojo = Entered.Context.Type == NodeTypeRegistry.Dojo ? new Dojo(Run, nodeId, IsOpenDojo, () => Trials) : null; // R7
		Merchant = MerchantAt(Node);
		(CoinsWon, _paid) = (0, false);
		LockCopyOnEntering(Entered.Context); // T1, before the event so listeners see the lock
		NodeEntered?.Invoke(Entered.Context);
		return step;
	}

	/// <summary>
	/// Ryo won the entered node's fight with <paramref name="healthLeft"/>. Health carries (C1); a node that binds
	/// heals 50 (R3) and, unless it is the last node, offers its cards (A7). The node's coins are paid once (R8).
	/// Returns the offer, or null for none. A win reported again on a node already paid (<see cref="NodePaid"/>, a
	/// repeat or a run continued after the win, YOK-128) changes nothing: health, heal and coins were counted once.
	/// </summary>
	public RewardDraft? WinFight(int healthLeft)
	{
		var node = Node ?? throw new InvalidOperationException("no node is entered");
		if (IsOver) throw new InvalidOperationException("the run is over");
		if (Draft is not null) throw new InvalidOperationException($"node '{node.NodeId}' is already won");
		if (_paid) return null; // R3, R8: already counted (a draft-less node: a node with a draft threw above)
		Run.AfterDuel(healthLeft, node.Binds);
		_paid = true;
		CoinsWon = node.Coins;
		Run.AddCoins(node.Coins); // R8, the amount from the node-scene table
		if (node.Binds && Map.Graph.Next(node.NodeId).Count > 0)
			Draft = RewardDraft.Build(Run, Pool, node.DraftYokai, Seed + (uint)Step, node.Elder); // YOK-178: the yokai beaten, not the body (A7, A8)
		return Draft;
	}

	/// <summary>The entered node is done: its routes open, or the run is won if it has none. Refused while its offer is unpicked.</summary>
	public MapResult CompleteNode()
	{
		if (IsOver) return MapResult.Fail("the run is over");
		if (Node is not { } node) return MapResult.Fail("no node is entered");
		if (Draft is { Picked: false }) return MapResult.Fail($"node '{node.NodeId}': the reward is not picked yet (A7)");
		if (_dojo is { TrialRunning: true }) return MapResult.Fail($"node '{node.NodeId}': the training trial is not finished (R7)");
		var step = Map.Complete(node.NodeId);
		if (!step.Ok) return step;
		Entered = null;
		Draft = null;
		_dojo = null;
		Merchant = null;
		(CoinsWon, _paid) = (0, false);
		if (Map.IsFinished) Ended = new RunEnd(RunOutcome.Won, node.Row, node.NodeId) { AtTanuki = IsCopyLockNode(node) };
		NodeCompleted?.Invoke(node);
		if (Ended is { } end) RunEnded?.Invoke(end);
		return step;
	}

	/// <summary>
	/// R6: the yokai bound this run, in walk order, once each: for every completed node that binds (R3), the yokai it
	/// binds as (<see cref="NodeContext.BoundAs"/>, YOK-178: the yokai whose abilities its draft offered, A8, never the
	/// stand-in body), then the yokai the map names there when that is another id (the apprentice: Kitsune, then
	/// apprentice). An elder binds its own id as written (<c>elder-oni</c>), not its yokai: open for the designer.
	/// Worked out from the walk, so it needs no saved state of its own.
	/// </summary>
	public IReadOnlyList<string> BoundYokai()
	{
		var bound = new List<string>();
		foreach (string id in Map.Visited)
		{
			var ctx = Scenes.Resolve(Map.Graph.Node(id)).Context;
			if (!ctx.Binds) continue;
			if (!bound.Contains(ctx.BoundAs)) bound.Add(ctx.BoundAs);
			if (ctx.Yokai is { } yokai && !bound.Contains(yokai)) bound.Add(yokai);
		}
		return bound;
	}

	Merchant? MerchantAt(NodeContext? node) =>
		node is { Type: NodeTypeRegistry.Merchant } ? new Merchant(this, node.NodeId) : null;

	/// <summary>Ryo lost the entered node's fight: the run ends there.</summary>
	public MapResult LoseFight()
	{
		if (IsOver) return MapResult.Fail("the run is over");
		if (Node is not { } node) return MapResult.Fail("no node is entered");
		Ended = new RunEnd(RunOutcome.Lost, node.Row, node.NodeId) { AtTanuki = IsCopyLockNode(node) };
		RunEnded?.Invoke(Ended);
		return MapResult.Done;
	}

	public ulong StateHash()
	{
		ulong r = Run.StateHash();
		ulong h = Fnv.Mix(Fnv.Offset, unchecked((int)r));
		h = Fnv.Mix(h, unchecked((int)(r >> 32)));
		h = Fnv.Mix(h, unchecked((int)Seed));
		h = Fnv.Mix(h, Ended is null ? 0 : 1 + (int)Ended.Outcome);
		// T1: the locked copy is run state of its own (the slots may differ from it later). Mixed in only once
		// locked, so a run that has not reached the Tanuki's node keeps the hash it had before the lock existed.
		if (CopyLocked)
		{
			h = Fnv.Mix(h, 1);
			h = Fnv.Mix(h, (int)(LockedCopy?.Slot ?? SpecialSlot.None));
			h = Fnv.MixString(h, LockedCopy?.Data.Id ?? "");
			h = Fnv.Mix(h, LockedCopy?.Level ?? 0);
		}
		// R6: a spent merchant choice is run state; a run with none keeps the hash it had before YOK-83
		if (Merchant is { Choice: not MerchantChoice.None } m) h = Fnv.Mix(Fnv.MixString(h, "merchant"), (int)m.Choice);
		// R7: a dojo choice already made is run state; a run with none keeps the hash it had before YOK-84.
		if (_dojo is { Choice: not DojoChoice.None } dojo)
		{
			h = Fnv.Mix(Fnv.Mix(h, Sim.Dojo.HashTag), (int)dojo.Choice);
			// YOK-98: the chosen trial and its attempt count (a rest hashes as it did before trials existed).
			if (dojo.Choice == DojoChoice.Trial)
				h = Fnv.Mix(Fnv.Mix(Fnv.Mix(Fnv.MixString(h, dojo.TrialId ?? ""), (int)dojo.TrialSlot), dojo.Attempts), (int)dojo.TrialResult);
		}
		return h;
	}
}
