using System;
using System.Collections.Generic;

namespace YokaiFighters.Sim;

/// <summary>
/// YOK-21: a special in one of the four slots (A1) at its current level (A3). Level is the fighter's run
/// state: <see cref="Fighter.LevelUp"/> (reward draft, YOK-47) rebuilds the moves from data, nothing else.
/// </summary>
public sealed class EquippedSpecial
{
	public SpecialData Data { get; }
	public int Level { get; }
	public MoveData Move { get; }
	/// <summary>The EX version (C5), or null when the data has none.</summary>
	public MoveData? ExMove { get; }
	/// <summary>YOK-47: the attached modifier (A2: at most one), already applied to Move and ExMove.</summary>
	public ModifierData? Modifier { get; }
	/// <summary>YOK-103: the EX cost with the modifier (Ninefold Spark halves it), else the data's <c>ex.meter_cost</c>.</summary>
	public int ExCost { get; }
	/// <summary>The fight defaults the modifier was built against (kept so a level-up rebuilds the same way).</summary>
	public SimConfig? Defaults { get; }

	public EquippedSpecial(SpecialData data, int level, ModifierData? modifier = null, SimConfig? defaults = null)
	{
		Data = data;
		Level = level;
		Modifier = modifier;
		Defaults = defaults;
		Move = data.Build(level, false, modifier, defaults);
		ExMove = data.HasEx ? data.Build(level, true, modifier, defaults) : null;
		ExCost = modifier?.ExCostFor(data.ExCost) ?? data.ExCost;
	}
}

/// <summary>A cancel rule a fighter holds (X1-X4), read from data/cancels/ (cancel-rule.schema.json).</summary>
public sealed record CancelRule(string Id, CancelFrom From, CancelInto Into, bool OnHit, bool OnBlock, bool OnWhiff);

public enum CancelFrom : byte { Specials, HeavyNormals, Normals, SuccessfulThrows }
public enum CancelInto : byte { Dash, Specials, OneSpecial }

public sealed partial class Fighter
{
	/// <summary>Slots A-D, indexed by <see cref="SpecialSlot"/> (index 0 unused). Run state: Reset keeps them.</summary>
	public readonly EquippedSpecial?[] Specials = new EquippedSpecial?[5];

	/// <summary>The move being played: a normal/throw from <see cref="Moves"/> or a special variant.</summary>
	public MoveData? ActiveMove;
	/// <summary>Slot of the special being played; None for normals and throws.</summary>
	public SpecialSlot ActiveSpecial;
	public bool ActiveEx;
	/// <summary>Kata motion input (+10% damage, K1).</summary>
	public bool ActivePrecision;
	/// <summary>Buttons pressed with the special's command (EX upgrade checks them).</summary>
	public InputBits SpecialButtons;
	/// <summary>The current move was blocked (the cancel framework's hit/block/whiff outcome).</summary>
	public bool MoveBlocked;

	/// <summary>Held cancel rules (X1: none at run start; X2: at most two). Run state.</summary>
	public readonly List<CancelRule> CancelRules = new();
	/// <summary>X3: targets already cancelled into this combo (bits 1-4 slots A-D, bit 5 dash); cleared when Idle.</summary>
	public int CancelUsed;

	/// <summary>YOK-103: start conditions held when the current special started (which build plays, kept for the EX upgrade).</summary>
	public StartConditions ActiveConditions;
	/// <summary>YOK-103: hits the current move's armour has taken (properties.armour hits).</summary>
	public int ArmourHitsTaken;
	/// <summary>YOK-103: specials started this round per slot (first_use_each_round); cleared by Reset (a new round).</summary>
	public readonly int[] SpecialUses = new int[5];

	public const int MaxCancelRules = 2;

	public void Equip(SpecialSlot slot, SpecialData data, int level = 1, ModifierData? modifier = null, SimConfig? defaults = null)
	{
		if (slot == SpecialSlot.None) throw new ArgumentException("slot A-D", nameof(slot));
		Specials[(int)slot] = new EquippedSpecial(data, level, modifier, defaults);
	}

	/// <summary>YOK-47, A2: attaches (or replaces) the slot's one modifier; rebuilds the moves from data.</summary>
	public void AttachModifier(SpecialSlot slot, ModifierData modifier, SimConfig? defaults = null)
	{
		var s = Specials[(int)slot] ?? throw new InvalidOperationException($"slot {slot} is empty");
		Specials[(int)slot] = new EquippedSpecial(s.Data, s.Level, modifier, defaults);
	}

	public void Unequip(SpecialSlot slot) => Specials[(int)slot] = null;

	/// <summary>A3: drafting an owned special levels it up (max Lv 3). Returns the new level.</summary>
	public int LevelUp(SpecialSlot slot)
	{
		var s = Specials[(int)slot] ?? throw new InvalidOperationException($"slot {slot} is empty");
		int level = Math.Min(3, s.Level + 1);
		Specials[(int)slot] = new EquippedSpecial(s.Data, level, s.Modifier, s.Defaults);
		return level;
	}

	/// <summary>X2: false when two rules are already held.</summary>
	public bool HoldCancelRule(CancelRule rule)
	{
		if (CancelRules.Count >= MaxCancelRules) return false;
		CancelRules.Add(rule);
		return true;
	}

	internal void ResetSpecialState()
	{
		ResetSpecialMoveState();
		CancelUsed = 0;
		Array.Clear(SpecialUses);
	}

	/// <summary>Clears what belongs to the move being played (on every state change).</summary>
	internal void ResetSpecialMoveState()
	{
		ActiveMove = null;
		ActiveSpecial = SpecialSlot.None;
		ActiveEx = false;
		ActivePrecision = false;
		SpecialButtons = InputBits.None;
		MoveBlocked = false;
		ActiveConditions = StartConditions.None;
		ArmourHitsTaken = 0;
	}

	internal ulong HashSpecialState(ulong h)
	{
		h = Fnv.Mix(h, (int)ActiveSpecial | (ActiveEx ? 16 : 0) | (ActivePrecision ? 32 : 0) | (MoveBlocked ? 64 : 0));
		h = Fnv.Mix(h, (int)SpecialButtons);
		h = Fnv.Mix(h, CancelUsed);
		h = Fnv.Mix(h, (int)ActiveConditions | ArmourHitsTaken << 8);
		foreach (int u in SpecialUses) h = Fnv.Mix(h, u);
		for (int i = 1; i < Specials.Length; i++)
		{
			h = Fnv.Mix(h, Specials[i]?.Level ?? 0);
			h = Fnv.MixString(h, Specials[i]?.Modifier?.Id ?? "");
		}
		return h;
	}
}
