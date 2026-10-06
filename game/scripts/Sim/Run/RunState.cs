using System;
using System.Collections.Generic;

namespace YokaiFighters.Sim;

/// <summary>A special Ryo owns this run: its level (A3) and its one modifier (A2).</summary>
public sealed record OwnedSpecial(SpecialData Data, int Level, ModifierData? Modifier);

/// <summary>
/// YOK-47: Ryo's run state between duels (pure C#, deterministic): health (C1 carries across the run) and
/// slots A-D. The next fight reads it through <see cref="MatchConfig"/> and <see cref="ApplyTo"/>.
/// </summary>
public sealed class RunState
{
	public const int MaxHealth = 1000; // C1
	public const int BindHeal = 50;    // R3

	readonly OwnedSpecial?[] _slots = new OwnedSpecial?[5];

	public int Health { get; private set; } = MaxHealth;

	public OwnedSpecial? this[SpecialSlot slot] => _slots[(int)slot];

	/// <summary>Owned specials in slot order A-D.</summary>
	public IEnumerable<(SpecialSlot Slot, OwnedSpecial Special)> Owned
	{
		get
		{
			for (int i = 1; i < _slots.Length; i++)
				if (_slots[i] is { } s) yield return ((SpecialSlot)i, s);
		}
	}

	public bool Owns(string specialId)
	{
		foreach (var (_, s) in Owned) if (s.Data.Id == specialId) return true;
		return false;
	}

	/// <summary>A1: a new run = the starters at Lv 1 in their own slots, full health.</summary>
	public static RunState NewRun(AbilityPool pool)
	{
		var run = new RunState();
		foreach (var s in pool.Starters)
			if (run._slots[(int)s.Slot] is null) run._slots[(int)s.Slot] = new OwnedSpecial(s, 1, null);
		return run;
	}

	/// <summary>
	/// A3: a newly drafted special goes in its own slot at Lv 1; drafting one already owned levels it up.
	/// A different special in that slot is replaced and its levels and modifier are lost (A6). Returns the slot.
	/// </summary>
	public SpecialSlot Equip(SpecialData data)
	{
		if (_slots[(int)data.Slot] is { } cur && cur.Data.Id == data.Id) { LevelUp(data.Slot); return data.Slot; }
		_slots[(int)data.Slot] = new OwnedSpecial(data, 1, null);
		return data.Slot;
	}

	public bool CanLevelUp(SpecialSlot slot) => this[slot] is { } s && s.Level < Math.Min(3, s.Data.MaxLevel);

	/// <summary>A3/A4: one level up, the step from the special's data (Lv 2 tuning, Lv 3 evolution). Returns the new level.</summary>
	public int LevelUp(SpecialSlot slot)
	{
		var s = this[slot] ?? throw new InvalidOperationException($"slot {slot} is empty");
		if (!CanLevelUp(slot)) throw new InvalidOperationException($"{s.Data.Id} has no level above {s.Level}");
		_slots[(int)slot] = s with { Level = s.Level + 1 };
		return s.Level + 1;
	}

	/// <summary>A2: one modifier per special; the special must meet the modifier's applies_to.</summary>
	public bool CanAttach(SpecialSlot slot, ModifierData modifier) =>
		this[slot] is { } s && s.Modifier is null && modifier.AppliesTo(s.Data);

	public void Attach(SpecialSlot slot, ModifierData modifier)
	{
		if (!CanAttach(slot, modifier)) throw new InvalidOperationException($"{modifier.Id} can't attach to slot {slot}");
		_slots[(int)slot] = this[slot]! with { Modifier = modifier };
	}

	/// <summary>R3: after a won duel the yokai is bound: health left + 50, capped at 1,000.</summary>
	public void AfterDuel(int healthLeft, bool bound = true) =>
		Health = Math.Min(MaxHealth, Math.Max(0, healthLeft) + (bound ? BindHeal : 0));

	/// <summary>Merchant / dojo heals and other run events (clamped 0..1,000).</summary>
	public void SetHealth(int health) => Health = Math.Clamp(health, 0, MaxHealth);

	/// <summary>The next fight's config: Ryo (P1) starts with the carried health.</summary>
	public SimConfig MatchConfig(SimConfig? baseConfig = null) =>
		(baseConfig ?? SimConfig.Default) with { P1MaxHealth = MaxHealth, P1StartHealth = Health };

	/// <summary>Puts the run's specials (levels, modifiers) on the fighter, replacing whatever it had.</summary>
	public void ApplyTo(Fighter f, SimConfig? config = null)
	{
		for (int i = 1; i < _slots.Length; i++)
		{
			if (_slots[i] is { } s) f.Equip((SpecialSlot)i, s.Data, s.Level, s.Modifier, config);
			else f.Unequip((SpecialSlot)i);
		}
	}

	public ulong StateHash()
	{
		ulong h = Fnv.Mix(Fnv.Offset, Health);
		for (int i = 1; i < _slots.Length; i++)
		{
			h = Fnv.MixString(h, _slots[i]?.Data.Id ?? "");
			h = Fnv.Mix(h, _slots[i]?.Level ?? 0);
			h = Fnv.MixString(h, _slots[i]?.Modifier?.Id ?? "");
		}
		return h;
	}
}
