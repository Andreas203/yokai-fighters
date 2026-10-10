using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using YokaiFighters.Sim;

namespace YokaiFighters.Ui;

/// <summary>One base-kit row: what it is called, the input, a plain-language line, and the frame line (behind the toggle).</summary>
public sealed record MoveRow(string Id, string Name, string Input, string Plain, string Frames, string? KataInput = null)
{
	/// <summary>The input line for the shown scheme: most rows are the same in both; EX differs (E16 / E18).</summary>
	public string InputFor(bool kata) => kata && KataInput != null ? KataInput : Input;
}

/// <summary>One special slot A-D. Empty = nothing owned there yet. Both schemes' inputs are kept; the panel shows the active one.</summary>
public sealed record SlotRow(string Slot, bool Empty, string Name, int Level, int MaxLevel, string KataInput, string KihonInput, string Plain, string Frames,
	string ModifierName = "", string ModifierPlain = "");

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

	/// <summary>Base-kit rows after which the panel draws a group divider (movement, air and block | six normals | throw, burst, EX).</summary>
	public static readonly int[] GroupBreaksAfter = { 6, 12 };
	public const string AirPunchId = "ryo-air-punch", AirKickId = "ryo-air-kick";

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
		[ThrowId] = "Grabs a guarding foe. Break it: press LP + LK.", // GDD 3.2: break by pressing throw; E12 input
		["burst"] = "Break a combo for all your meter. Not from throws.", // E13: a thrown fighter breaks, it cannot burst
		[AirPunchId] = "Dive-in punch. One per jump.",
		[AirKickId] = "Dive-in kick. One per jump.",
		["ex"] = "Stronger special for 1 bar. No bar: plain one.", // E16: without the meter the plain version comes out
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
			FromFile(movesDir, AirPunchId, _ => "Jump + any punch", air: true, landing: cfg.AirLandingRecovery),
			FromFile(movesDir, AirKickId, _ => "Jump + any kick", air: true, landing: cfg.AirLandingRecovery),
			new("block", "Block", "Hold back (crouch: lows)", Plain("block"), "No chip damage"),
		};
		foreach (string id in NormalIds) rows.Add(FromFile(movesDir, id, ButtonLabel));
		rows.Add(FromFile(movesDir, ThrowId, ButtonLabel));
		rows.Add(new("burst", "Burst", "LP + MP + HP while hit", Plain("burst"), $"{cfg.BurstFrames} invulnerable frames, once per fight"));
		// E16 (Kata) / E18 (Kihon). GDD 3.2 only says an EX special costs 1 bar; the input is the designer-proposal in SimConfig.ExPressWindow.
		rows.Add(new("ex", "EX special", "Special + dir + punch / kick", Plain("ex"), $"Costs {cfg.ExCost / cfg.MeterBar} bar, second button within {cfg.ExPressWindow - 1} ticks", KataInput: "Motion + 2 punches / 2 kicks"));

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
			// The move the sim would run: level, then the equipped modifier on top (A2, A10), using the same fight defaults as ApplyTo.
			var m = new EquippedSpecial(owned.Data, owned.Level, owned.Modifier, run.MatchConfig(cfg)).Move;
			string ex = owned.Data.HasEx ? $"\nEX costs {owned.Data.ExCost / cfg.MeterBar} bar" : "";
			var mod = owned.Modifier;
			string frames = FrameLine(m) + (mod is { CardFrames.Length: > 0 } ? $"\n{mod.Name}: {mod.CardFrames}" : "") + ex;
			slots.Add(new(letter, false, owned.Data.Name, owned.Level, owned.Data.MaxLevel, kata, kihon, owned.Data.CardPlain, frames, mod?.Name ?? "", mod?.CardPlain ?? ""));
		}
		return new MoveListView(rows, slots);
	}

	private static MoveRow FromFile(string dir, string id, Func<string, string> input, bool air = false, int landing = 0)
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
		var move = MoveLoader.LoadFile(path);
		string frames = FrameLine(move) + (air ? $", {move.LandingRecovery ?? landing} landing" : "");
		return new MoveRow(id, name, air ? input("") : label, Plain(id), frames);
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
