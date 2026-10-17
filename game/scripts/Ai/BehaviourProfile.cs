using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;

namespace YokaiFighters.Ai;

/// <summary>On-screen observations a profile's "when" can name (data/schema/profile.schema.json, P6).</summary>
public enum Observation
{
	Neutral, AtCloseRange, AtMidRange, AtFullScreen,
	OpponentJumping, OpponentKnockedDown, SelfKnockedDown, SelfGotUp,
	ProjectileOnScreen, BlockedProjectile, BlockedTwoHitsInRow,
	OpponentWhiffed, OpponentBlocking, OpponentInRecovery, OpponentAttacking,
	OpponentUsedSameSpecialTwice,
}

public enum ActionKind { WalkForward, WalkBack, DashForward, DashBack, Jump, Block, CrouchBlock, Throw, Special, Normal }

/// <summary>A profile action: generic, or a move id ("special:foxfire", "normal:heavy-punch").</summary>
public readonly record struct AiAction(ActionKind Kind, string? MoveId = null)
{
	public bool Offensive => Kind is ActionKind.WalkForward or ActionKind.DashForward or ActionKind.Jump
		or ActionKind.Throw or ActionKind.Special or ActionKind.Normal;
	public override string ToString() => MoveId is null ? BehaviourProfile.ActionName(Kind) : $"{BehaviourProfile.ActionName(Kind)}:{MoveId}";
}

/// <summary>The one learnable habit (Y2): when the trigger shows, do the action with chance_pct.</summary>
public sealed record Habit(string Id, string Description, Observation When, AiAction Do, int ChancePct);

/// <summary>
/// "when → do" with a weight. Event observations (see <see cref="BehaviourProfile.IsEvent"/>) use the weight
/// as a percent rate per occurrence (an anti-air of 70 = 70% of seen jumps); standing observations
/// (neutral, ranges, opponent_blocking) share a weighted pick in neutral.
/// </summary>
public sealed record Behaviour(Observation When, AiAction Do, int Weight);

/// <summary>
/// YOK-57: which kind of fight the AI is in. E22: "The standard-tier AI doesn't use EX or burst yet", so a profile's
/// <see cref="ExUse"/> / <see cref="BurstUse"/> apply only in elder and Tanuki fights; elder fights reuse yokai-tier
/// profiles, so the gate is the fight (the node), not the profile.
/// </summary>
public enum AiFightTier { Standard, Elder, Tanuki }

/// <summary>
/// YOK-57 (E16, C5): when the AI picks a special and sees on its own meter at least the special's EX cost plus
/// <see cref="KeepMeter"/>, it asks for the EX version with <see cref="ChancePct"/>. Absent = never (E22 standard tier).
/// </summary>
public sealed record ExUse(int ChancePct, int KeepMeter = 0);

/// <summary>
/// YOK-57 (C6, E13): each hit the AI sees itself take, once it has taken <see cref="MinComboHits"/> in a row, its
/// health shows at most <see cref="MaxHealthPct"/>% and its burst seal is still lit, it bursts with
/// <see cref="ChancePct"/>, if the hitstun it sees will still be running when the press lands. Absent = never.
/// </summary>
public sealed record BurstUse(int ChancePct, int MinComboHits = 1, int MaxHealthPct = 100);

/// <summary>One yokai temperament (Y1, Y2) as loaded from kind "profile" JSON. Difficulty = ReactionFrames (Y4)
/// and TellCoverPct (how often a rolled habit is held back).</summary>
public sealed record BehaviourProfile
{
	public required string Id { get; init; }
	public required string Yokai { get; init; }
	public required string Temperament { get; init; }
	public required string ReactionTier { get; init; }
	public required int ReactionFrames { get; init; }
	public required int AggressionBias { get; init; }
	public required Observation PreferredRange { get; init; }
	public required Habit Habit { get; init; }
	public required IReadOnlyList<Behaviour> Behaviours { get; init; }
	/// <summary>0–100: chance a habit that rolled true is suppressed anyway (harder opponents hide the tell).</summary>
	public int TellCoverPct { get; init; }
	/// <summary>YOK-57: EX use (E16); null = the AI never asks for EX. Applied only in elder and Tanuki fights (E22).</summary>
	[YokaiFighters.Replay.FingerprintOmitWhenNull] public ExUse? ExUse { get; init; }
	/// <summary>YOK-57: burst use (C6); null = the AI never bursts. Applied only in elder and Tanuki fights (E22).</summary>
	[YokaiFighters.Replay.FingerprintOmitWhenNull] public BurstUse? BurstUse { get; init; }

	/// <summary>Observations that happen at a moment (rolled once each time they start), not standing conditions.</summary>
	public static bool IsEvent(Observation o) => o is not (Observation.Neutral or Observation.AtCloseRange
		or Observation.AtMidRange or Observation.AtFullScreen or Observation.OpponentBlocking);

	public static readonly IReadOnlyDictionary<string, Observation> Observations = new Dictionary<string, Observation>
	{
		["neutral"] = Observation.Neutral, ["at_close_range"] = Observation.AtCloseRange,
		["at_mid_range"] = Observation.AtMidRange, ["at_full_screen"] = Observation.AtFullScreen,
		["opponent_jumping"] = Observation.OpponentJumping, ["opponent_knocked_down"] = Observation.OpponentKnockedDown,
		["self_knocked_down"] = Observation.SelfKnockedDown, ["self_got_up"] = Observation.SelfGotUp,
		["projectile_on_screen"] = Observation.ProjectileOnScreen, ["blocked_projectile"] = Observation.BlockedProjectile,
		["blocked_two_hits_in_row"] = Observation.BlockedTwoHitsInRow, ["opponent_whiffed"] = Observation.OpponentWhiffed,
		["opponent_blocking"] = Observation.OpponentBlocking, ["opponent_in_recovery"] = Observation.OpponentInRecovery,
		["opponent_attacking"] = Observation.OpponentAttacking,
		["opponent_used_same_special_twice"] = Observation.OpponentUsedSameSpecialTwice,
	};

	private static readonly string[] ActionNames =
		{ "walk_forward", "walk_back", "dash_forward", "dash_back", "jump", "block", "crouch_block", "throw", "special", "normal" };

	public static string ActionName(ActionKind k) => ActionNames[(int)k];

	public static string ObservationName(Observation o)
	{
		foreach (var kv in Observations) if (kv.Value == o) return kv.Key;
		return o.ToString();
	}

	/// <summary>
	/// YOK-94, T0: <c>special:copied</c> names the Tanuki's copied special, whose id is known only at the copy lock
	/// (T1). It parses like any special id and resolves in <see cref="AiLoadout"/> to the fighter's
	/// <see cref="YokaiFighters.Sim.Fighter.CopiedSlot"/>; a literal <c>special:&lt;id&gt;</c> is unchanged. Reserved:
	/// no special may have this id (tools/validate_data.py).
	/// </summary>
	public const string CopiedSpecial = "copied";

	/// <summary>Parses "walk_forward", "normal:&lt;id&gt;", "special:&lt;id&gt;" or "special:copied" (<see cref="CopiedSpecial"/>).</summary>
	public static AiAction ParseAction(string s)
	{
		int colon = s.IndexOf(':');
		string head = colon < 0 ? s : s[..colon];
		int k = Array.IndexOf(ActionNames, head);
		if (k < 0) throw new FormatException($"unknown action '{s}'");
		var kind = (ActionKind)k;
		bool needsId = kind is ActionKind.Special or ActionKind.Normal;
		if (needsId != (colon >= 0) || (needsId && colon == s.Length - 1)) throw new FormatException($"bad action '{s}'");
		return new AiAction(kind, needsId ? s[(colon + 1)..] : null);
	}

	private static Observation ParseObservation(JsonNode? n, string where) =>
		Observations.TryGetValue(n?.GetValue<string>() ?? "", out var o) ? o : throw new FormatException($"{where}: unknown observation '{n}'");

	private static int Int(JsonNode? n, string where, int min, int max)
	{
		int v = n?.GetValue<int>() ?? throw new FormatException($"{where} is missing");
		if (v < min || v > max) throw new FormatException($"{where} {v} outside {min}..{max}");
		return v;
	}

	public static BehaviourProfile Parse(string json)
	{
		var o = JsonNode.Parse(json)!.AsObject();
		if ((string?)o["kind"] != "profile") throw new FormatException("kind is not 'profile'");
		string id = (string?)o["id"] ?? throw new FormatException("id is missing");
		var h = o["habit"] ?? throw new FormatException($"{id}: habit is missing");
		string range = (string?)o["preferred_range"] ?? "";
		var behaviours = new List<Behaviour>();
		foreach (var b in o["behaviours"]?.AsArray() ?? throw new FormatException($"{id}: behaviours missing"))
			behaviours.Add(new Behaviour(ParseObservation(b!["when"], $"{id} behaviour"), ParseAction((string)b["do"]!),
				Int(b["weight"], $"{id} behaviour weight", 1, 100)));
		return new BehaviourProfile
		{
			Id = id,
			Yokai = (string?)o["yokai"] ?? throw new FormatException($"{id}: yokai missing"),
			Temperament = (string?)o["temperament"] ?? throw new FormatException($"{id}: temperament missing"),
			ReactionTier = (string?)o["reaction"]?["tier"] ?? throw new FormatException($"{id}: reaction.tier missing"),
			ReactionFrames = Int(o["reaction"]?["frames"], $"{id} reaction.frames", 1, 120),
			AggressionBias = Int(o["aggression_bias"], $"{id} aggression_bias", 0, 100),
			PreferredRange = range switch
			{
				"close" => Observation.AtCloseRange, "mid" => Observation.AtMidRange, "full_screen" => Observation.AtFullScreen,
				_ => throw new FormatException($"{id}: preferred_range '{range}'"),
			},
			Habit = new Habit((string)h["id"]!, (string?)h["description"] ?? "", ParseObservation(h["when"], $"{id} habit"),
				ParseAction((string)h["do"]!), Int(h["chance_pct"], $"{id} habit.chance_pct", 1, 100)),
			Behaviours = behaviours,
			TellCoverPct = o["tell_cover_pct"] is null ? 0 : Int(o["tell_cover_pct"], $"{id} tell_cover_pct", 0, 100),
			ExUse = o["ex_use"] is { } ex
				? new ExUse(Int(ex["chance_pct"], $"{id} ex_use.chance_pct", 0, 100),
					ex["keep_meter"] is null ? 0 : Int(ex["keep_meter"], $"{id} ex_use.keep_meter", 0, 300))
				: null,
			BurstUse = o["burst_use"] is { } bu
				? new BurstUse(Int(bu["chance_pct"], $"{id} burst_use.chance_pct", 0, 100),
					bu["min_combo_hits"] is null ? 1 : Int(bu["min_combo_hits"], $"{id} burst_use.min_combo_hits", 1, 20),
					bu["max_health_pct"] is null ? 100 : Int(bu["max_health_pct"], $"{id} burst_use.max_health_pct", 1, 100))
				: null,
		};
	}

	public static BehaviourProfile LoadFile(string path) => Parse(File.ReadAllText(path));

	/// <summary>Every kind "profile" file in a folder, sorted by file name; a missing folder gives none.</summary>
	public static BehaviourProfile[] LoadDirectory(string dir)
	{
		if (!Directory.Exists(dir)) return Array.Empty<BehaviourProfile>();
		var files = Directory.GetFiles(dir, "*.json");
		Array.Sort(files, StringComparer.Ordinal);
		var list = new List<BehaviourProfile>();
		foreach (var f in files) list.Add(LoadFile(f));
		return list.ToArray();
	}
}
