using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using YokaiFighters.Sim;

namespace YokaiFighters.Ui;

/// <summary>One base-kit row: what it is called, the input, a plain-language line, and the frame line (behind the toggle).</summary>
public sealed record MoveRow(string Id, string Name, string Input, string Plain, string Frames);

/// <summary>One special slot A-D. Empty = nothing owned there yet. Both schemes' inputs are kept; the panel shows the active one.</summary>
public sealed record SlotRow(string Slot, bool Empty, string Name, int Level, int MaxLevel, string KataInput, string KihonInput, string Plain, string Frames);

public sealed record MoveListView(IReadOnlyList<MoveRow> Base, IReadOnlyList<SlotRow> Slots);

/// <summary>
/// YOK-58 MOCKUP: builds the move-list screen's content. Names, frame data and special text come from
/// <c>data/moves</c> (via <see cref="MoveLoader"/>, <see cref="SpecialData"/>) and the run state; nothing is
/// duplicated from the rules. Two things are UI copy, not data: the one-line plain descriptions of the base kit
/// (<see cref="PlainCopy"/>, placeholder text for movesmith to replace) and the slot input labels (K1 / K2 as written).
/// </summary>
public static class MoveListSource
{
	/// <summary>Order of the six normals as the move list shows them.</summary>
	public static readonly string[] NormalIds =
	{
		"ryo-light-punch", "ryo-medium-punch", "ryo-heavy-punch", "ryo-light-kick", "ryo-medium-kick", "ryo-heavy-kick",
	};
	public const string ThrowId = "ryo-generic-throw";

	/// <summary>Base-kit rows after which the panel draws a group divider (movement | normals | throw and burst).</summary>
	public static readonly int[] GroupBreaksAfter = { 3, 9 };

	// PLACEHOLDER COPY (designer / movesmith to replace): A12 wants plain language first; normals have no card text in data yet.
	private static readonly Dictionary<string, string> PlainCopy = new()
	{
		["walk"] = "Step toward or away from the foe.",
		["dash"] = "A quick hop forward or back.",
		["jump"] = "Leap over attacks, or dive in with an air attack.",
		["block"] = "Hold back to block. Crouch to block low hits.",
		["ryo-light-punch"] = "Quick jab. Starts combos, hard to punish.",
		["ryo-medium-punch"] = "Steady punch with good reach.",
		["ryo-heavy-punch"] = "Slow, heavy punch. Big damage if it lands.",
		["ryo-light-kick"] = "Fast, short kick. Safe when blocked.",
		["ryo-medium-kick"] = "Mid-range kick for keeping your space.",
		["ryo-heavy-kick"] = "Wide sweeping kick. Slow, but it hits hard.",
		[ThrowId] = "Grabs a guarding foe. They can break it.",
		["burst"] = "Break a combo once per fight. Costs all meter.",
	};

	/// <summary>K1 motions and K2 directions per slot, as written in the rules (display only; parsing lives in the input layer).</summary>
	private static readonly Dictionary<string, (string Kata, string Kihon)> SlotInputs = new()
	{
		["A"] = ("Down, down-forward, forward", "Special + neutral"),
		["B"] = ("Forward, down, down-forward", "Special + forward"),
		["C"] = ("Down, down-back, back", "Special + back"),
		["D"] = ("Down, down", "Special + down"),
	};

	public static string Plain(string id) => PlainCopy.TryGetValue(id, out var p) ? p : "";

	public static MoveListView Build(RunState run, string movesDir, SimConfig? cfg = null)
	{
		cfg ??= SimConfig.Default;
		var rows = new List<MoveRow>
		{
			new("walk", "Walk", "Hold back / forward", Plain("walk"), $"Crosses the screen in about {WalkSeconds(cfg):0.0} seconds"),
			new("dash", "Dash", "Double-tap back / forward", Plain("dash"), $"{cfg.DashFrames} frames"),
			new("jump", "Jump", "Up, up-forward, up-back", Plain("jump"), $"{cfg.JumpFrames} frames in the air"),
			new("block", "Block", "Hold back (crouch: lows)", Plain("block"), "No chip damage"),
		};
		foreach (string id in NormalIds) rows.Add(FromFile(movesDir, id, ButtonLabel));
		rows.Add(FromFile(movesDir, ThrowId, ButtonLabel));
		rows.Add(new("burst", "Burst", "LP + MP + HP while hit", Plain("burst"), $"{cfg.BurstFrames} invulnerable frames, once per fight"));

		var slots = new List<SlotRow>();
		foreach (var slot in new[] { SpecialSlot.A, SpecialSlot.B, SpecialSlot.C, SpecialSlot.D })
		{
			string letter = slot.ToString();
			var (kata, kihon) = SlotInputs[letter];
			if (run[slot] is not { } owned)
			{
				slots.Add(new(letter, true, "Empty", 0, 3, kata, kihon, "Win a duel and draft a power from the yokai you bind.", ""));
				continue;
			}
			var m = owned.Data.Build(owned.Level, false);
			string ex = owned.Data.HasEx ? $"\nEX costs {owned.Data.ExCost / 100} bar" : "";
			slots.Add(new(letter, false, owned.Data.Name, owned.Level, owned.Data.MaxLevel, kata, kihon, owned.Data.CardPlain, FrameLine(m) + ex));
		}
		return new MoveListView(rows, slots);
	}

	private static MoveRow FromFile(string dir, string id, Func<string, string> input)
	{
		string path = Path.Combine(dir, id + ".json");
		using var doc = JsonDocument.Parse(File.ReadAllText(path));
		var root = doc.RootElement;
		string name = root.GetProperty("name").GetString() ?? id;
		var inp = root.GetProperty("input");
		string label;
		if (inp.TryGetProperty("buttons", out var bs))
		{
			var parts = new List<string>();
			foreach (var x in bs.EnumerateArray()) parts.Add(input(x.GetString() ?? "?"));
			label = string.Join(" + ", parts);
		}
		else label = input(inp.GetProperty("button").GetString() ?? "?");
		return new MoveRow(id, name, label, Plain(id), FrameLine(MoveLoader.LoadFile(path)));
	}

	/// <summary>Seconds to walk one screen width (C2 says about 2.5).</summary>
	public static double WalkSeconds(SimConfig c) => c.ViewWidth / (double)c.WalkSpeed / 60.0;

	private static string ButtonLabel(string b) => b;

	private static string Signed(int v) => v > 0 ? $"+{v}" : v.ToString();

	/// <summary>"startup / active / recovery frames, damage, advantage": the same shape as the reward card's frame line (A12).</summary>
	public static string FrameLine(MoveData m)
	{
		string active = m.Active > 0 ? m.Active.ToString() : "—";
		string head = $"{m.Startup} / {active} / {m.Recovery} frames, {m.Damage} damage";
		if (m.IsThrow) return head + $", breaks in {m.BreakWindow}";
		if (m.Hitstun == 0 && m.Blockstun == 0) return head;
		return head + $", {Signed(m.AdvantageOnHit)} hit, {Signed(m.AdvantageOnBlock)} block";
	}
}
