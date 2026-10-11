using System;
using System.Collections.Generic;

namespace YokaiFighters.Sim;

public enum RunOutcome { Won, Lost }

/// <summary>How a run ended: the outcome, the row Ryo reached and the node it ended on.</summary>
public sealed record RunEnd(RunOutcome Outcome, int RowReached, string NodeId)
{
	public bool Won => Outcome == RunOutcome.Won;
}

/// <summary>
/// YOK-77: one run from the first node to its end (pure C#, deterministic, no Godot). Owns the
/// <see cref="RunState"/> (health C1, slots) and its <see cref="RunMap"/>; scenes only call it:
/// <see cref="Enter"/> a reachable node → the scene named by <see cref="Entered"/> runs it →
/// <see cref="WinFight"/> (R3 heal, the A7 draft when the node binds) then <see cref="CompleteNode"/>, or
/// <see cref="LoseFight"/>. The run is won when the completed node has no route out, lost on the first lost fight.
/// <para>
/// All of its state is plain data: <see cref="Seed"/>, the run state, <see cref="RunMap.Snapshot"/>,
/// <see cref="Ended"/> and, while a dojo node is entered, its <see cref="Sim.Dojo.Choice"/> (a later save /
/// Continue, YOK-73, writes those; a session rebuilt inside a dojo opens the stop unspent until the saved
/// choice is put back with <c>Dojo.Restore</c>, R7). The object must outlive scene changes:
/// the boot flow holds it, never a node's scene.
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
	public RunEnd? Ended { get; private set; }
	public bool IsOver => Ended is not null;
	/// <summary>1 for the first node of the walk, 2 for the second... (completed nodes + the one entered).</summary>
	public int Step => Map.Visited.Count + (Map.InNode ? 1 : 0);
	/// <summary>Nodes in a whole walk (one per row, R1).</summary>
	public int Steps => Map.Graph.Rows.Count;

	/// <summary>A node was entered. <b>T1 hook</b>: the Tanuki's lock point is this event for its boss node.</summary>
	public event Action<NodeContext>? NodeEntered;
	/// <summary>A node was completed (raised before <see cref="RunEnded"/> on the last one; <see cref="IsOver"/> is already true then).</summary>
	public event Action<NodeContext>? NodeCompleted;
	public event Action<RunEnd>? RunEnded;

	public RunSession(RunState run, AbilityPool pool, INodeSceneResolver scenes, uint seed)
	{
		Run = run;
		Map = run.Map ?? throw new ArgumentException("the run has no map (RunState.StartMap)", nameof(run));
		Pool = pool;
		Scenes = scenes;
		Seed = seed;
		foreach (var n in Map.Graph.Nodes) scenes.Resolve(n); // a node the resolver cannot place fails here, not mid-run
		if (Map.InNode) Entered = scenes.Resolve(Map.CurrentNode!); // a restored walk
		if (Entered?.Context.Type == NodeTypeRegistry.Dojo) _dojo = new Dojo(Run, Entered.Context.NodeId, IsOpenDojo); // its saved choice: Dojo.Restore
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
		_dojo = Entered.Context.Type == NodeTypeRegistry.Dojo ? new Dojo(Run, nodeId, IsOpenDojo) : null; // R7
		NodeEntered?.Invoke(Entered.Context);
		return step;
	}

	/// <summary>
	/// Ryo won the entered node's fight with <paramref name="healthLeft"/>. Health carries (C1); a node that binds
	/// heals 50 (R3) and, unless it is the last node, offers its cards (A7). Returns the offer, or null for none.
	/// </summary>
	public RewardDraft? WinFight(int healthLeft)
	{
		var node = Node ?? throw new InvalidOperationException("no node is entered");
		if (IsOver) throw new InvalidOperationException("the run is over");
		if (Draft is not null) throw new InvalidOperationException($"node '{node.NodeId}' is already won");
		Run.AfterDuel(healthLeft, node.Binds);
		if (node.Binds && Map.Graph.Next(node.NodeId).Count > 0)
			Draft = RewardDraft.Build(Run, Pool, node.Fighter, Seed + (uint)Step, node.Elder);
		return Draft;
	}

	/// <summary>The entered node is done: its routes open, or the run is won if it has none. Refused while its offer is unpicked.</summary>
	public MapResult CompleteNode()
	{
		if (IsOver) return MapResult.Fail("the run is over");
		if (Node is not { } node) return MapResult.Fail("no node is entered");
		if (Draft is { Picked: false }) return MapResult.Fail($"node '{node.NodeId}': the reward is not picked yet (A7)");
		var step = Map.Complete(node.NodeId);
		if (!step.Ok) return step;
		Entered = null;
		Draft = null;
		_dojo = null;
		if (Map.IsFinished) Ended = new RunEnd(RunOutcome.Won, node.Row, node.NodeId);
		NodeCompleted?.Invoke(node);
		if (Ended is { } end) RunEnded?.Invoke(end);
		return step;
	}

	/// <summary>Ryo lost the entered node's fight: the run ends there.</summary>
	public MapResult LoseFight()
	{
		if (IsOver) return MapResult.Fail("the run is over");
		if (Node is not { } node) return MapResult.Fail("no node is entered");
		Ended = new RunEnd(RunOutcome.Lost, node.Row, node.NodeId);
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
		// R7: a dojo choice already made is run state; a run with none keeps the hash it had before YOK-84.
		if (_dojo is { Choice: not DojoChoice.None } dojo) h = Fnv.Mix(Fnv.Mix(h, Sim.Dojo.HashTag), (int)dojo.Choice);
		return h;
	}
}
