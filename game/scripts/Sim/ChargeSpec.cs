using System;
using System.Linq;
using System.Text.Json.Nodes;

namespace YokaiFighters.Sim;

/// <summary>
/// YOK-171 (AM11, Sumo Stance): a modifier's <c>charge</c> object (modifier.schema.json). The fighter holds the
/// special's trigger button before the special starts (<see cref="FighterState.Bracing"/>); on release the held
/// ticks pick a dash speed that moves the fighter forward on each of the move's startup frames. Integers only (F3):
/// speeds are gameplay units per tick. Data only (A10): nothing here is keyed on a modifier id.
/// </summary>
public sealed record ChargeSpec(int WindowMin, int WindowMax, int StepTicks, int[] UnitsPerTickByStep, int Cap, string HoldPoseClip)
{
	/// <summary>The only trigger the engine reads: whichever button fired the special (Kihon Special, Kata the motion's button, K3).</summary>
	public const string SpecialTrigger = "special_trigger";

	/// <summary>True when a release after <paramref name="held"/> ticks counts as <c>charge_released</c>.</summary>
	public bool InWindow(int held) => held >= WindowMin && held <= WindowMax;

	/// <summary>
	/// Dash speed in gameplay units per tick for a release after <paramref name="held"/> ticks: step = held / step_ticks,
	/// past the last step the last speed holds, never above the cap. 0 outside the window (no dash).
	/// </summary>
	public int SpeedFor(int held)
	{
		if (!InWindow(held)) return 0;
		int step = Math.Min(held / StepTicks, UnitsPerTickByStep.Length - 1);
		return Math.Min(UnitsPerTickByStep[step], Cap);
	}

	/// <summary>Reads the JSON object; <see cref="FormatException"/> names what the fight can't run.</summary>
	public static ChargeSpec Parse(JsonNode? node, string id)
	{
		if (node is not JsonObject o) throw new FormatException($"{id}: charge must be an object");
		string[] allowed = { "button", "window_ticks", "step_ticks", "grace_ticks", "release", "hold_pose_clip", "dash" };
		if (o.FirstOrDefault(kv => !allowed.Contains(kv.Key)) is { Key: { } extra }) throw new FormatException($"{id}: charge.{extra} is not read");
		if (Effects.Text(o, "button", id, "charge.button") != SpecialTrigger)
			throw new FormatException($"{id}: charge.button must be \"{SpecialTrigger}\"");
		if (o["window_ticks"] is not JsonObject w) throw new FormatException($"{id}: charge.window_ticks must be an object");
		int min = Int(w, "min", 0), max = Int(w, "max", 1);
		if (min > max) throw new FormatException($"{id}: charge.window_ticks.min must be <= max");
		int step = Int(o, "step_ticks", 1);
		// Read and pinned until a designer rule gives them a meaning beyond the YOK-142 data (grace 0, dash then move, startup).
		if (o.ContainsKey("grace_ticks") && Int(o, "grace_ticks", 0) != 0) throw new FormatException($"{id}: charge.grace_ticks other than 0 is not read yet");
		if (o.ContainsKey("release") && Effects.Text(o, "release", id, "charge.release") != "dash_then_move")
			throw new FormatException($"{id}: charge.release must be \"dash_then_move\"");
		string pose = Effects.Text(o, "hold_pose_clip", id, "charge.hold_pose_clip") ?? "";
		if (o["dash"] is not JsonObject d) throw new FormatException($"{id}: charge.dash must be an object");
		if (Effects.Text(d, "duration", id, "charge.dash.duration") != "startup")
			throw new FormatException($"{id}: charge.dash.duration must be \"startup\"");
		if (d["units_per_tick_by_step"] is not JsonArray a || a.Count == 0)
			throw new FormatException($"{id}: charge.dash.units_per_tick_by_step must list at least one speed");
		var units = new int[a.Count];
		for (int k = 0; k < a.Count; k++)
			if (!Effects.Whole(a[k], out units[k]) || units[k] < 1) throw new FormatException($"{id}: charge.dash.units_per_tick_by_step must be whole numbers >= 1");
		int cap = Int(d, "cap_units_per_tick", 1);
		return new ChargeSpec(min, max, step, units, cap, pose);

		int Int(JsonObject parent, string key, int least)
		{
			if (!Effects.Whole(parent[key], out int v) || v < least)
				throw new FormatException($"{id}: charge {key} must be a whole number >= {least}");
			return v;
		}
	}
}
