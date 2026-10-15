using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace YokaiFighters.Sim;

/// <summary>What a trial step watches for. Each is an event the sim already raises (YOK-105).</summary>
public enum TrialEvent
{
	/// <summary>
	/// The player's strike or projectile connects unblocked (<see cref="Match.Hit"/>, Blocked false). A landed
	/// throw also raises <see cref="Match.Hit"/> but does NOT count here: throws are <see cref="Throw"/> steps.
	/// </summary>
	Hit,
	/// <summary>
	/// <c>Count</c> unblocked strike or projectile hits (throws do not count) with the dummy held between them:
	/// on every tick from one hit to the next its state is Hitstun, Knockdown or Thrown
	/// (<see cref="TrialStep.ComboHolds"/>). A dummy that recovered and then jumped, dashed or attacked on its
	/// own has left the combo even though <see cref="Fighter.Actionable"/> is false in those states. This is the
	/// defender's side; X3's "once per combo" is the attacker's (its cancel is given back when the attacker
	/// returns to Idle, Match.SetState), so the two need not end on the same tick.
	/// </summary>
	Combo,
	/// <summary>
	/// The player's throw lands (<see cref="Match.Throw"/>, <see cref="ThrowOutcome.Land"/>). Its dummy state
	/// and distance are read on the GRAB tick of that throw (<see cref="TrialSample.Grab"/>).
	/// </summary>
	Throw,
	/// <summary>The player blocks the dummy's hits (<see cref="Match.Hit"/> from the dummy, Blocked true). Needs an attacking dummy.</summary>
	Block,
}

/// <summary>The dummy's state when a step is sampled (<see cref="TrialStep.Sample"/>). Any = not checked.</summary>
public enum TrialDummyState
{
	Any,
	/// <summary><see cref="Fighter.Airborne"/>.</summary>
	Airborne,
	/// <summary>Not airborne.</summary>
	Grounded,
	/// <summary>Not airborne now, and airborne on one of the <c>LandedWithin</c> ticks before the sampled tick.</summary>
	Landed,
}

/// <summary>The tick a step's dummy state and distance are read on.</summary>
public enum TrialSample
{
	/// <summary>The tick of the step's own event (the <see cref="Match.Hit"/>).</summary>
	Event,
	/// <summary>
	/// Throw steps: the tick the grab connected (<see cref="ThrowOutcome.Grab"/>) for the throw that then lands.
	/// The land comes BreakWindow + 1 ticks later (C4), after the dummy has been held on the ground and before
	/// the land's slide, so "as it lands" and the throw's range can only be read at the grab.
	/// </summary>
	Grab,
}

public enum TrialDummyAction { Idle, Crouch, Guard, CrouchGuard, Jump, Attack }

/// <summary>
/// The dummy's behaviour: a held stance, a jump every <c>Every</c> ticks, or a string of <c>Moves</c> every
/// <c>Every</c> ticks. The first jump or string starts on tick 1 of each attempt, the next on tick 1 + Every.
/// </summary>
public sealed record TrialDummy(TrialDummyAction Action, IReadOnlyList<string> Moves, int Every);

/// <summary>
/// One goal step. Distances are gameplay units (multiply by <see cref="SimConfig.Scale"/> for sim positions);
/// <c>MinDistance</c>/<c>MaxDistance</c> null = not checked.
/// </summary>
public sealed record TrialStep(
	TrialEvent Event, string? Move, int Count, bool Counter, TrialDummyState DummyState, int LandedWithin,
	int? MinDistance, int? MaxDistance)
{
	/// <summary>When <see cref="DummyState"/> and the distance are read: the grab for a throw, the event otherwise.</summary>
	public TrialSample Sample => Event == TrialEvent.Throw ? TrialSample.Grab : TrialSample.Event;

	/// <summary>
	/// The dummy-state condition on the sampled tick. <paramref name="ticksSinceAirborne"/> = sampled tick minus
	/// the dummy's last airborne tick (anything above <see cref="TrialLoader.MaxLandedWithin"/> when it never jumped).
	/// </summary>
	public bool DummyStateMet(bool airborne, int ticksSinceAirborne) => DummyState switch
	{
		TrialDummyState.Airborne => airborne,
		TrialDummyState.Grounded => !airborne,
		TrialDummyState.Landed => !airborne && ticksSinceAirborne >= 1 && ticksSinceAirborne <= LandedWithin,
		_ => true,
	};

	/// <summary>The distance condition on the sampled tick; <paramref name="units"/> = |X1 - X2| / <see cref="SimConfig.Scale"/>.</summary>
	public bool DistanceMet(int units) => (MinDistance is null || units >= MinDistance) && (MaxDistance is null || units <= MaxDistance);

	/// <summary>True while a combo is still going: the dummy is held by the player's last hit, not acting on its own.</summary>
	public static bool ComboHolds(FighterState dummy) =>
		dummy is FighterState.Hitstun or FighterState.Knockdown or FighterState.Thrown;
}

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

	/// <summary>
	/// Steps the named moves can never meet, for the caller that has the kit (the loader does not):
	/// a hit or combo step naming a throw (throws only meet throw steps) and a throw step naming a move that
	/// is not a throw. <paramref name="isThrow"/> returns null for a move it does not know (reported too).
	/// Same check as tools/validate_data.py makes against data/moves/.
	/// </summary>
	public IReadOnlyList<string> MoveProblems(Func<string, bool?> isThrow)
	{
		var problems = new List<string>();
		for (int i = 0; i < Steps.Count; i++)
		{
			var st = Steps[i];
			if (st.Move is null) continue;
			bool? t = isThrow(st.Move);
			if (t is null) problems.Add($"{Id}: goal.steps.{i}.move: unknown move '{st.Move}'");
			else if (t == true && st.Event != TrialEvent.Throw)
				problems.Add($"{Id}: goal.steps.{i}.move: '{st.Move}' is a throw; throws only meet a throw step");
			else if (t == false && st.Event == TrialEvent.Throw)
				problems.Add($"{Id}: goal.steps.{i}.move: '{st.Move}' is not a throw");
		}
		return problems;
	}
}

public static class TrialLoader
{
	/// <summary>R7: the special levels if the trial is landed within 3 attempts.</summary>
	public const int Attempts = 3;
	/// <summary>Step keys only Kata can produce (K1); same list as tools/validate_data.py TRIAL_KATA_ONLY.</summary>
	public static readonly string[] KataOnlyKeys = { "motion", "precision", "kata" };

	// Bounds mirror trial.schema.json: distance = SimConfig body width .. max separation (gameplay units, AM7).
	public const int MinStartDistance = 120, MaxStartDistance = 1040;
	public const int MinAttemptFrames = 60, MaxAttemptFrames = 3600;
	public const int MaxSteps = 4, MaxCount = 10, MaxLandedWithin = 60, MaxEvery = 600, MaxDummyMoves = 4, MaxPrompt = 160;

	static readonly string[] RootKeys = { "kind", "id", "status", "special", "prompt", "attempts", "setup", "goal", "rules", "notes" };
	static readonly string[] SetupKeys = { "distance", "attempt_frames", "dummy" };
	static readonly string[] DummyKeys = { "action", "moves", "every" };
	static readonly string[] StepKeys = { "event", "move", "count", "counter", "dummy_state", "landed_within", "distance" };
	// common.schema.json $defs id and ruleIds.
	static readonly Regex IdPattern = new("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.CultureInvariant);
	static readonly Regex RulePattern = new("^[PCKFEXAYTRMSVG][0-9]+$", RegexOptions.CultureInvariant);

	/// <summary>
	/// Reads one trial; throws <see cref="FormatException"/> naming the trial and field on anything
	/// trial.schema.json or tools/validate_data.py rejects (except: rule ids are checked for shape, not against
	/// docs/design/rules.md, and move ids are not looked up; see <see cref="TrialData.MoveProblems"/>).
	/// </summary>
	public static TrialData Parse(string json, string? fallbackId = null)
	{
		// A JsonObject reports a field written twice only when it is first read (ArgumentException).
		try { return ParseObject(json, fallbackId); }
		catch (ArgumentException e) { throw new FormatException($"{fallbackId ?? "trial"}: {e.Message}"); }
	}

	static TrialData ParseObject(string json, string? fallbackId)
	{
		JsonObject root;
		try { root = JsonNode.Parse(json) as JsonObject ?? throw new FormatException("top level is not an object"); }
		catch (JsonException e) { throw new FormatException($"{fallbackId ?? "trial"}: not JSON ({e.Message})"); }
		catch (FormatException e) { throw new FormatException($"{fallbackId ?? "trial"}: {e.Message}"); }

		string id = root["id"] is JsonValue iv && iv.TryGetValue(out string? s) ? s : fallbackId ?? "unnamed";
		FormatException Bad(string field, string msg) => new($"{id}: {field}: {msg}");

		if (Str(root, "kind", id) != "trial") throw Bad("kind", "not a kind \"trial\" file");
		Only(root, RootKeys, "", id);
		Id(root, "id", id);
		string status = Str(root, "status", id);
		if (status is not ("final" or "proposed")) throw Bad("status", $"'{status}' is not final or proposed");
		string special = Id(root, "special", id);
		string prompt = Str(root, "prompt", id);
		if (prompt.Length == 0) throw Bad("prompt", "empty");
		int chars = prompt.EnumerateRunes().Count(); // code points, as JSON Schema maxLength counts
		if (chars > MaxPrompt) throw Bad("prompt", $"longer than {MaxPrompt} characters ({chars})");
		int attempts = Int(root, "attempts", id);
		if (attempts != Attempts) throw Bad("attempts", $"must be {Attempts} (R7), got {attempts}");

		var setup = Obj(root, "setup", id);
		Only(setup, SetupKeys, "setup.", id);
		int distance = Range(Int(setup, "distance", id, "setup."), MinStartDistance, MaxStartDistance, "setup.distance", id);
		int frames = Range(Int(setup, "attempt_frames", id, "setup."), MinAttemptFrames, MaxAttemptFrames, "setup.attempt_frames", id);
		var dummy = ParseDummy(Obj(setup, "dummy", id, "setup."), id);
		if (dummy.Every > frames)
			throw Bad("setup.dummy.every", $"{dummy.Every} is longer than the attempt ({frames} ticks)");

		var goal = Obj(root, "goal", id);
		Only(goal, new[] { "steps" }, "goal.", id);
		if (goal["steps"] is not JsonArray arr || arr.Count == 0) throw Bad("goal.steps", "needs at least one step");
		if (arr.Count > MaxSteps) throw Bad("goal.steps", $"at most {MaxSteps} steps, got {arr.Count}");
		var steps = new List<TrialStep>();
		for (int i = 0; i < arr.Count; i++)
		{
			var st = ParseStep(arr[i] as JsonObject ?? throw Bad($"goal.steps.{i}", "not an object"), $"goal.steps.{i}.", id);
			// The sim can only raise these against a dummy that attacks: a blocked Match.Hit from the dummy,
			// and HitEvent.Counter (C7: the defender was in a move).
			if (dummy.Action != TrialDummyAction.Attack)
			{
				if (st.Event == TrialEvent.Block) throw Bad($"goal.steps.{i}.event", "block needs a dummy with action 'attack' (nothing to block otherwise)");
				if (st.Counter) throw Bad($"goal.steps.{i}.counter", "a counterhit needs a dummy with action 'attack' (C7: the dummy must be in a move)");
			}
			steps.Add(st);
		}
		if (!steps.Any(st => st.Move == special))
			throw Bad("goal.steps", $"no step names the trial's special '{special}' as its move (R7)");

		if (root["rules"] is not JsonArray ra) throw Bad("rules", "required list of rule ids");
		var rules = new List<string>();
		for (int i = 0; i < ra.Count; i++)
		{
			if (ra[i] is not JsonValue rv || !rv.TryGetValue(out string? rule) || !RulePattern.IsMatch(rule))
				throw Bad($"rules.{i}", "not a rule id (a letter and a number, e.g. R7)");
			if (rules.Contains(rule)) throw Bad($"rules.{i}", $"'{rule}' is listed twice");
			rules.Add(rule);
		}
		if (root.ContainsKey("notes")) Str(root, "notes", id);

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
			moves = ma.Select((n, i) => n is JsonValue v && v.TryGetValue(out string? m) && IdPattern.IsMatch(m) ? m
				: throw new FormatException($"{id}: setup.dummy.moves.{i}: not a lowercase kebab-case move id")).ToArray();
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

		string? move = o.ContainsKey("move") ? Id(o, "move", id, at) : null;
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

	/// <summary>Reads a file; its id must equal the file name (data convention, as the validator checks).</summary>
	public static TrialData LoadFile(string path)
	{
		string stem = Path.GetFileNameWithoutExtension(path);
		try
		{
			var t = Parse(File.ReadAllText(path), stem);
			if (t.Id != stem) throw new FormatException($"{t.Id}: id: must equal the file name '{stem}'");
			return t;
		}
		catch (FormatException e) { throw new FormatException($"{path}: {e.Message}", e); }
	}

	/// <summary>Every kind "trial" *.json in the folder, sorted by file name (absent folder = none).</summary>
	public static TrialData[] LoadDirectory(string dir) =>
		Directory.Exists(dir)
			? Directory.GetFiles(dir, "*.json").OrderBy(f => Path.GetFileName(f), StringComparer.Ordinal)
				.Where(f => MoveLoader.KindOf(f) == "trial").Select(LoadFile).ToArray()
			: Array.Empty<TrialData>();

	static void Only(JsonObject o, string[] keys, string at, string id)
	{
		foreach (var (k, _) in o)
			if (!keys.Contains(k)) throw new FormatException($"{id}: {at}{k}: unknown field");
	}

	static JsonObject Obj(JsonObject o, string key, string id, string at = "") =>
		o[key] as JsonObject ?? throw new FormatException($"{id}: {at}{key}: required object");

	static string Str(JsonObject o, string key, string id, string at = "") =>
		o[key] is JsonValue v && v.TryGetValue(out string? s) ? s : throw new FormatException($"{id}: {at}{key}: required text");

	static string Id(JsonObject o, string key, string id, string at = "")
	{
		string s = Str(o, key, id, at);
		return IdPattern.IsMatch(s) ? s : throw new FormatException($"{id}: {at}{key}: '{s}' is not a lowercase kebab-case id");
	}

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
