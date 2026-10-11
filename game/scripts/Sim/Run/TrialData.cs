using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace YokaiFighters.Sim;

/// <summary>What a trial step watches for. Each is an event the sim already raises (YOK-105).</summary>
public enum TrialEvent
{
	/// <summary>The player's strike or projectile connects unblocked (<see cref="Match.Hit"/>, Blocked false).</summary>
	Hit,
	/// <summary><c>Count</c> unblocked player hits with the dummy never <see cref="Fighter.Actionable"/> between them.</summary>
	Combo,
	/// <summary>The player's throw lands (<see cref="Match.Throw"/>, <see cref="ThrowOutcome.Land"/>).</summary>
	Throw,
	/// <summary>The player blocks the dummy's hits (<see cref="Match.Hit"/> from the dummy, Blocked true).</summary>
	Block,
}

/// <summary>The dummy's state when a step's event happens. Any = not checked.</summary>
public enum TrialDummyState
{
	Any,
	/// <summary><see cref="Fighter.Airborne"/>.</summary>
	Airborne,
	/// <summary>Not airborne.</summary>
	Grounded,
	/// <summary>Grounded, and airborne on one of the last <c>LandedWithin</c> ticks.</summary>
	Landed,
}

public enum TrialDummyAction { Idle, Crouch, Guard, CrouchGuard, Jump, Attack }

/// <summary>The dummy's behaviour: a held stance, a jump every <c>Every</c> ticks, or a string of <c>Moves</c> every <c>Every</c> ticks.</summary>
public sealed record TrialDummy(TrialDummyAction Action, IReadOnlyList<string> Moves, int Every);

/// <summary>
/// One goal step. Distances are gameplay units (multiply by <see cref="SimConfig.Scale"/> for sim positions);
/// <c>MinDistance</c>/<c>MaxDistance</c> null = not checked.
/// </summary>
public sealed record TrialStep(
	TrialEvent Event, string? Move, int Count, bool Counter, TrialDummyState DummyState, int LandedWithin,
	int? MinDistance, int? MaxDistance);

/// <summary>
/// YOK-105: one kind "trial" file (data/schema/trial.schema.json), a hand-authored dojo trial (R7). Data only:
/// running it in the dojo (and the 15% heal, the level-up) is YOK-98. The goal is an ordered list of
/// <see cref="TrialStep"/>s, each an observation of the fight, never a button, so every trial is
/// Kihon-completable (K2).
/// </summary>
public sealed class TrialData
{
	public required string Id { get; init; }
	public string Status { get; init; } = "proposed";
	/// <summary>Id of the special the trial levels (R7).</summary>
	public required string Special { get; init; }
	public required string Prompt { get; init; }
	/// <summary>Always <see cref="TrialLoader.Attempts"/> (R7).</summary>
	public int Attempts { get; init; } = TrialLoader.Attempts;
	/// <summary>Start distance between the fighters' centres, gameplay units.</summary>
	public int Distance { get; init; }
	/// <summary>Ticks one attempt lasts before it fails.</summary>
	public int AttemptFrames { get; init; }
	public required TrialDummy Dummy { get; init; }
	public required IReadOnlyList<TrialStep> Steps { get; init; }
	public IReadOnlyList<string> Rules { get; init; } = Array.Empty<string>();
}

public static class TrialLoader
{
	/// <summary>R7: the special levels if the trial is landed within 3 attempts.</summary>
	public const int Attempts = 3;
	/// <summary>Step keys only Kata can produce (K1); same list as tools/validate_data.py TRIAL_KATA_ONLY.</summary>
	public static readonly string[] KataOnlyKeys = { "motion", "precision", "kata" };

	// Bounds mirror trial.schema.json: distance = SimConfig body width .. max separation (gameplay units).
	public const int MinStartDistance = 120, MaxStartDistance = 1520;
	public const int MinAttemptFrames = 60, MaxAttemptFrames = 3600;
	public const int MaxSteps = 4, MaxCount = 10, MaxLandedWithin = 60, MaxEvery = 600, MaxDummyMoves = 4;

	static readonly string[] RootKeys = { "kind", "id", "status", "special", "prompt", "attempts", "setup", "goal", "rules", "notes" };
	static readonly string[] SetupKeys = { "distance", "attempt_frames", "dummy" };
	static readonly string[] DummyKeys = { "action", "moves", "every" };
	static readonly string[] StepKeys = { "event", "move", "count", "counter", "dummy_state", "landed_within", "distance" };

	/// <summary>Reads one trial; throws <see cref="FormatException"/> naming the trial and field on anything the schema rejects.</summary>
	public static TrialData Parse(string json, string? fallbackId = null)
	{
		JsonObject root;
		try { root = JsonNode.Parse(json)?.AsObject() ?? throw new FormatException("empty"); }
		catch (Exception e) when (e is JsonException or InvalidOperationException) { throw new FormatException($"{fallbackId ?? "trial"}: not a JSON object ({e.Message})"); }

		string id = root["id"] is JsonValue iv && iv.TryGetValue(out string? s) ? s : fallbackId ?? "unnamed";
		FormatException Bad(string field, string msg) => new($"{id}: {field}: {msg}");

		if (Str(root, "kind", id) != "trial") throw Bad("kind", "not a kind \"trial\" file");
		Only(root, RootKeys, "", id);
		Str(root, "id", id);
		string status = Str(root, "status", id);
		if (status is not ("final" or "proposed")) throw Bad("status", $"'{status}' is not final or proposed");
		string special = Str(root, "special", id);
		string prompt = Str(root, "prompt", id);
		if (prompt.Length == 0) throw Bad("prompt", "empty");
		int attempts = Int(root, "attempts", id);
		if (attempts != Attempts) throw Bad("attempts", $"must be {Attempts} (R7), got {attempts}");

		var setup = Obj(root, "setup", id);
		Only(setup, SetupKeys, "setup.", id);
		int distance = Range(Int(setup, "distance", id, "setup."), MinStartDistance, MaxStartDistance, "setup.distance", id);
		int frames = Range(Int(setup, "attempt_frames", id, "setup."), MinAttemptFrames, MaxAttemptFrames, "setup.attempt_frames", id);
		var dummy = ParseDummy(Obj(setup, "dummy", id, "setup."), id);

		var goal = Obj(root, "goal", id);
		Only(goal, new[] { "steps" }, "goal.", id);
		if (goal["steps"] is not JsonArray arr || arr.Count == 0) throw Bad("goal.steps", "needs at least one step");
		if (arr.Count > MaxSteps) throw Bad("goal.steps", $"at most {MaxSteps} steps, got {arr.Count}");
		var steps = new List<TrialStep>();
		for (int i = 0; i < arr.Count; i++)
			steps.Add(ParseStep(arr[i] as JsonObject ?? throw Bad($"goal.steps.{i}", "not an object"), $"goal.steps.{i}.", id));
		if (!steps.Any(st => st.Move == special))
			throw Bad("goal.steps", $"no step names the trial's special '{special}' as its move (R7)");

		var rules = root["rules"] is JsonArray ra ? ra.Select(n => n!.GetValue<string>()).ToArray()
			: throw Bad("rules", "required list of rule ids");

		return new TrialData
		{
			Id = id, Status = status, Special = special, Prompt = prompt, Attempts = attempts,
			Distance = distance, AttemptFrames = frames, Dummy = dummy, Steps = steps, Rules = rules,
		};
	}

	static TrialDummy ParseDummy(JsonObject o, string id)
	{
		Only(o, DummyKeys, "setup.dummy.", id);
		string a = Str(o, "action", id, "setup.dummy.");
		var action = a switch
		{
			"idle" => TrialDummyAction.Idle, "crouch" => TrialDummyAction.Crouch, "guard" => TrialDummyAction.Guard,
			"crouch_guard" => TrialDummyAction.CrouchGuard, "jump" => TrialDummyAction.Jump, "attack" => TrialDummyAction.Attack,
			_ => throw new FormatException($"{id}: setup.dummy.action: unknown action '{a}'"),
		};
		bool repeats = action is TrialDummyAction.Jump or TrialDummyAction.Attack;
		int every = 0;
		if (repeats) every = Range(Int(o, "every", id, "setup.dummy."), 1, MaxEvery, "setup.dummy.every", id);
		else if (o.ContainsKey("every")) throw new FormatException($"{id}: setup.dummy.every: only for jump and attack");
		string[] moves = Array.Empty<string>();
		if (action == TrialDummyAction.Attack)
		{
			if (o["moves"] is not JsonArray ma || ma.Count == 0 || ma.Count > MaxDummyMoves)
				throw new FormatException($"{id}: setup.dummy.moves: attack needs 1-{MaxDummyMoves} move ids");
			moves = ma.Select(n => n is JsonValue v && v.TryGetValue(out string? m) && m.Length > 0 ? m
				: throw new FormatException($"{id}: setup.dummy.moves: not a move id")).ToArray();
		}
		else if (o.ContainsKey("moves")) throw new FormatException($"{id}: setup.dummy.moves: only for attack");
		return new TrialDummy(action, moves, every);
	}

	static TrialStep ParseStep(JsonObject o, string at, string id)
	{
		foreach (var key in KataOnlyKeys)
			if (o.ContainsKey(key)) throw new FormatException($"{id}: {at}{key}: needs a Kata input; trials must be Kihon-completable (R7, K2)");
		Only(o, StepKeys, at, id);
		string e = Str(o, "event", id, at);
		var ev = e switch
		{
			"hit" => TrialEvent.Hit, "combo" => TrialEvent.Combo, "throw" => TrialEvent.Throw, "block" => TrialEvent.Block,
			_ => throw new FormatException($"{id}: {at}event: unknown event '{e}'"),
		};
		void Forbid(string key, string why) { if (o.ContainsKey(key)) throw new FormatException($"{id}: {at}{key}: {why}"); }

		string? move = o.ContainsKey("move") ? Str(o, "move", id, at) : null;
		int count = o.ContainsKey("count") ? Range(Int(o, "count", id, at), 1, MaxCount, at + "count", id) : 1;
		bool counter = o.ContainsKey("counter") && (o["counter"] is JsonValue cv && cv.TryGetValue(out bool c)
			? c : throw new FormatException($"{id}: {at}counter: not true/false"));
		var state = TrialDummyState.Any;
		if (o.ContainsKey("dummy_state"))
		{
			string ds = Str(o, "dummy_state", id, at);
			state = ds switch
			{
				"airborne" => TrialDummyState.Airborne, "grounded" => TrialDummyState.Grounded, "landed" => TrialDummyState.Landed,
				_ => throw new FormatException($"{id}: {at}dummy_state: unknown state '{ds}'"),
			};
		}
		int landed = 0;
		if (state == TrialDummyState.Landed) landed = Range(Int(o, "landed_within", id, at), 1, MaxLandedWithin, at + "landed_within", id);
		else Forbid("landed_within", "only with dummy_state 'landed'");
		int? min = null, max = null;
		if (o.ContainsKey("distance"))
		{
			var d = Obj(o, "distance", id, at);
			Only(d, new[] { "min", "max" }, at + "distance.", id);
			if (d.Count == 0) throw new FormatException($"{id}: {at}distance: give min, max or both");
			if (d.ContainsKey("min")) min = Range(Int(d, "min", id, at + "distance."), 0, MaxStartDistance, at + "distance.min", id);
			if (d.ContainsKey("max")) max = Range(Int(d, "max", id, at + "distance."), 0, MaxStartDistance, at + "distance.max", id);
			if (min > max) throw new FormatException($"{id}: {at}distance: min {min} is greater than max {max}");
		}

		switch (ev)
		{
			case TrialEvent.Combo:
				if (!o.ContainsKey("count") || count < 2) throw new FormatException($"{id}: {at}count: a combo needs count 2 or more");
				Forbid("dummy_state", "not for combo"); Forbid("distance", "not for combo"); Forbid("counter", "not for combo");
				break;
			case TrialEvent.Block:
				Forbid("move", "not for block"); Forbid("dummy_state", "not for block");
				Forbid("distance", "not for block"); Forbid("counter", "not for block");
				break;
			case TrialEvent.Throw:
				Forbid("counter", "a throw is never a counterhit"); Forbid("count", "not for throw");
				if (state == TrialDummyState.Airborne) throw new FormatException($"{id}: {at}dummy_state: an airborne dummy cannot be thrown (C4)");
				break;
		}
		return new TrialStep(ev, move, count, counter, state, landed, min, max);
	}

	public static TrialData LoadFile(string path)
	{
		try { return Parse(File.ReadAllText(path), Path.GetFileNameWithoutExtension(path)); }
		catch (FormatException e) { throw new FormatException($"{path}: {e.Message}", e); }
	}

	/// <summary>Every kind "trial" *.json in the folder, sorted by file name (absent folder = none); duplicate ids are rejected.</summary>
	public static TrialData[] LoadDirectory(string dir)
	{
		if (!Directory.Exists(dir)) return Array.Empty<TrialData>();
		var trials = Directory.GetFiles(dir, "*.json").OrderBy(f => Path.GetFileName(f), StringComparer.Ordinal)
			.Where(f => MoveLoader.KindOf(f) == "trial").Select(LoadFile).ToArray();
		var dup = trials.GroupBy(t => t.Id).FirstOrDefault(g => g.Count() > 1);
		if (dup is not null) throw new FormatException($"{dir}: trial id '{dup.Key}' appears twice");
		return trials;
	}

	static void Only(JsonObject o, string[] keys, string at, string id)
	{
		foreach (var (k, _) in o)
			if (!keys.Contains(k)) throw new FormatException($"{id}: {at}{k}: unknown field");
	}

	static JsonObject Obj(JsonObject o, string key, string id, string at = "") =>
		o[key] as JsonObject ?? throw new FormatException($"{id}: {at}{key}: required object");

	static string Str(JsonObject o, string key, string id, string at = "") =>
		o[key] is JsonValue v && v.TryGetValue(out string? s) ? s : throw new FormatException($"{id}: {at}{key}: required text");

	static int Int(JsonObject o, string key, string id, string at = "")
	{
		// JSON Schema "integer" takes any whole number (3, 3.0, 3e0), as tools/validate_data.py does.
		if (o[key] is JsonValue v && v.GetValueKind() == JsonValueKind.Number && v.GetValue<JsonElement>().TryGetDecimal(out decimal d)
			&& d == decimal.Truncate(d) && d >= int.MinValue && d <= int.MaxValue)
			return (int)d;
		throw new FormatException($"{id}: {at}{key}: required whole number");
	}

	static int Range(int v, int lo, int hi, string field, string id) =>
		v >= lo && v <= hi ? v : throw new FormatException($"{id}: {field}: {v} is outside {lo}..{hi}");
}
