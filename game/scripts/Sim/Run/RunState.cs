using System;
using System.Collections.Generic;

namespace YokaiFighters.Sim;

/// <summary>
/// A special Ryo owns this run: its level (A3) and its one modifier (A2). <paramref name="Borrowed"/> (YOK-95, R6):
/// bought at the merchant, shown in grey "borrowed ink" and never carried over to the next run (M3).
/// </summary>
public sealed record OwnedSpecial(SpecialData Data, int Level, ModifierData? Modifier, bool Borrowed = false);

/// <summary>
/// YOK-47: Ryo's run state between duels (pure C#, deterministic): health (C1 carries across the run),
/// slots A-D and, since YOK-76, the run map (<see cref="Map"/>). The next fight reads it through <see cref="MatchConfig"/> and <see cref="ApplyTo"/>.
/// </summary>
public sealed class RunState
{
	public const int MaxHealth = 1000; // C1
	public const int BindHeal = 50;    // R3

	readonly OwnedSpecial?[] _slots = new OwnedSpecial?[5];

	/// <summary>
	/// T3: per slot, the rank of its special's latest level-up among the specials now in a slot (0 = never
	/// levelled, 1 = earliest, higher = more recent). Kept compact by <see cref="CompactLevelled"/> after
	/// every change, so a value never exceeds the number of slots and no arithmetic on it can overflow.
	/// </summary>
	readonly int[] _levelledAt = new int[5];

	/// <summary>
	/// T3: how recently the special now in <paramref name="slot"/> gained a level, as a rank among the
	/// specials currently in a slot: 1 = the earliest of them, higher = more recent, 0 = it has not levelled
	/// this run (or the slot is empty). It belongs to the special, not the slot: a replaced special's level-up
	/// is dropped with it (T2). Part of <see cref="StateHash"/>; restored by <see cref="RestoreLevelledOrder"/>.
	/// </summary>
	public int LevelledOrder(SpecialSlot slot)
	{
		int at = _slots[(int)slot] is null ? 0 : _levelledAt[(int)slot];
		if (at == 0) return 0;
		int rank = 1;
		for (int i = 1; i < _slots.Length; i++)
			if (_slots[i] is not null && _levelledAt[i] > 0 && _levelledAt[i] < at) rank++;
		return rank;
	}

	/// <summary>T3: rewrites the stamps as their ranks (1..4, 0 for never levelled or empty); the order is unchanged.</summary>
	void CompactLevelled()
	{
		var ranks = new int[_levelledAt.Length];
		for (int i = 1; i < ranks.Length; i++) ranks[i] = LevelledOrder((SpecialSlot)i);
		for (int i = 1; i < ranks.Length; i++) _levelledAt[i] = ranks[i];
	}

	/// <summary>
	/// T3: puts back the level-up order of a saved run, slots A-D, as <see cref="LevelledOrder"/> reported it.
	/// Only the relative order of the non-zero values matters: they are stored as ranks, never as given, so
	/// any positive magnitude is safe. Call it after the slots and levels are back, so a restored run does not
	/// depend on replaying its picks in the original sequence. Throws, changing nothing, when the values can't
	/// describe these slots: negative, two slots sharing a non-zero value, non-zero for an empty slot or a
	/// Lv 1 special, or 0 for a special above Lv 1.
	/// </summary>
	public void RestoreLevelledOrder(int a, int b, int c, int d)
	{
		var orders = new[] { 0, a, b, c, d };
		for (int i = 1; i < _slots.Length; i++)
		{
			int o = orders[i];
			if (o < 0) throw new ArgumentException($"slot {(SpecialSlot)i}: level-up order can't be negative");
			bool levelled = _slots[i] is { } s && s.Level > 1;
			if ((o > 0) != levelled)
				throw new ArgumentException($"slot {(SpecialSlot)i}: level-up order {o} doesn't match what the slot holds");
			for (int j = 1; j < i; j++)
				if (o > 0 && orders[j] == o)
					throw new ArgumentException($"slots {(SpecialSlot)j} and {(SpecialSlot)i} share level-up order {o}");
		}
		for (int i = 1; i < _slots.Length; i++) _levelledAt[i] = orders[i];
		CompactLevelled(); // only compared, never added to, before this
	}

	public int Health { get; private set; } = MaxHealth;

	/// <summary>YOK-76: where Ryo is on the run map (null = a run without a map, e.g. the two-duel demo).</summary>
	public RunMap? Map { get; private set; }

	/// <summary>Starts this run's map from the first row. Health and slots are untouched.</summary>
	public RunMap StartMap(MapGraph graph) => Map = new RunMap(graph);

	/// <summary>Puts back a walk in progress (<see cref="RunMap.Restore"/>), or removes the map with null.</summary>
	public void SetMap(RunMap? map) => Map = map;

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
	public static RunState NewRun(AbilityPool pool, MapGraph? map = null)
	{
		var run = new RunState();
		if (map is not null) run.StartMap(map);
		foreach (var s in pool.Starters)
			if (run._slots[(int)s.Slot] is null) run._slots[(int)s.Slot] = new OwnedSpecial(s, 1, null);
		return run;
	}

	/// <summary>
	/// YOK-87: a saved run's health and slots put back as they were (no map, no level-up order: follow with
	/// <see cref="SetMap"/> and <see cref="RestoreLevelledOrder"/>). Throws, building nothing, when the values are
	/// not a state a run can reach: health outside 0-1,000, a special outside its own slot (A3), two in one slot,
	/// a level outside 1..its top level (A3/A4), or a modifier the special does not meet (A2).
	/// </summary>
	public static RunState Restore(int health, IEnumerable<(SpecialSlot Slot, OwnedSpecial Special)> slots)
	{
		if (health < 0 || health > MaxHealth) throw new ArgumentException($"health {health} is outside 0-{MaxHealth}");
		var run = new RunState { Health = health };
		foreach (var (slot, s) in slots)
		{
			if (slot is < SpecialSlot.A or > SpecialSlot.D) throw new ArgumentException($"{s.Data.Id}: '{slot}' is not a slot");
			if (s.Data.Slot != slot) throw new ArgumentException($"{s.Data.Id} belongs in slot {s.Data.Slot}, not {slot}");
			if (run._slots[(int)slot] is not null) throw new ArgumentException($"slot {slot} is listed twice");
			if (s.Level < 1 || s.Level > Math.Min(3, s.Data.MaxLevel)) throw new ArgumentException($"{s.Data.Id} has no level {s.Level}");
			if (s.Modifier is { } m && !m.AppliesTo(s.Data)) throw new ArgumentException($"{m.Id} can't attach to {s.Data.Id}");
			if (s.Borrowed && s.Data.Starter) throw new ArgumentException($"{s.Data.Id} is a starter: the merchant never sells it (R6, A1)");
			run._slots[(int)slot] = s;
		}
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
		_levelledAt[(int)data.Slot] = 0; // T2/T3: the replaced special's level-up no longer counts
		CompactLevelled();
		return data.Slot;
	}

	/// <summary>
	/// YOK-95, R6: a special bought at the merchant goes in its own (empty) slot at Lv 1 (A3), in borrowed ink.
	/// Throws, changing nothing, when that slot holds a special: the shop sells into a free slot only (release first).
	/// </summary>
	public SpecialSlot Borrow(SpecialData data)
	{
		if (data.Slot is < SpecialSlot.A or > SpecialSlot.D) throw new ArgumentException($"{data.Id}: '{data.Slot}' is not a slot");
		if (_slots[(int)data.Slot] is { } cur) throw new InvalidOperationException($"slot {data.Slot} holds {cur.Data.Id}: release it first (R6)");
		_slots[(int)data.Slot] = new OwnedSpecial(data, 1, null, Borrowed: true);
		_levelledAt[(int)data.Slot] = 0;
		CompactLevelled();
		return data.Slot;
	}

	/// <summary>
	/// YOK-95, R6: empties <paramref name="slot"/> (the merchant's release). The special goes with its levels and
	/// modifier (A6) and its level-up no longer counts for the copy target (T2). Throws, changing nothing, on an
	/// empty slot or when it holds Ryo's last special (T5).
	/// </summary>
	public void Release(SpecialSlot slot)
	{
		var s = this[slot] ?? throw new InvalidOperationException($"slot {slot} is empty");
		if (SpecialCount == 1) throw new InvalidOperationException($"{s.Data.Id} is Ryo's last special (T5)");
		_slots[(int)slot] = null;
		_levelledAt[(int)slot] = 0; // T2: a released move's level-up no longer counts
		CompactLevelled();
	}

	/// <summary>How many slots hold a special.</summary>
	public int SpecialCount
	{
		get
		{
			int n = 0;
			for (int i = 1; i < _slots.Length; i++) if (_slots[i] is not null) n++;
			return n;
		}
	}

	/// <summary>
	/// M3 / R6: the specials the next run may carry over from, slot order A-D. Borrowed ink never carries over;
	/// choosing the best of these and the row-6 condition is M3's own code (not built yet).
	/// </summary>
	public IEnumerable<(SpecialSlot Slot, OwnedSpecial Special)> CarryOverCandidates
	{
		get
		{
			foreach (var o in Owned) if (!o.Special.Borrowed) yield return o;
		}
	}

	public bool CanLevelUp(SpecialSlot slot) => this[slot] is { } s && s.Level < Math.Min(3, s.Data.MaxLevel);

	/// <summary>A3/A4: one level up, the step from the special's data (Lv 2 tuning, Lv 3 evolution). Returns the new level.</summary>
	public int LevelUp(SpecialSlot slot)
	{
		var s = this[slot] ?? throw new InvalidOperationException($"slot {slot} is empty");
		if (!CanLevelUp(slot)) throw new InvalidOperationException($"{s.Data.Id} has no level above {s.Level}");
		_slots[(int)slot] = s with { Level = s.Level + 1 };
		CompactLevelled(); // stamps are 0..4 from here, so the +1 below can't overflow
		int latest = 0;
		for (int i = 1; i < _levelledAt.Length; i++) latest = Math.Max(latest, _levelledAt[i]);
		_levelledAt[(int)slot] = latest + 1; // T3: this special is now the most recently levelled
		CompactLevelled();
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

	/// <summary>
	/// YOK-85, R8: Ryo's coins this run. A won duel pays them (<see cref="RunSession.WinFight"/>, the amount from
	/// the node-scene table); the merchant's shop (R6) spends them through <see cref="TrySpend"/>.
	/// </summary>
	public int Coins { get; private set; }

	/// <summary>Pays Ryo <paramref name="amount"/> coins (0 or more). The purse stops at <see cref="int.MaxValue"/> rather than wrap.</summary>
	public void AddCoins(int amount)
	{
		if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), amount, "coins paid can't be negative");
		Coins = amount > int.MaxValue - Coins ? int.MaxValue : Coins + amount;
	}

	/// <summary>Ryo has at least <paramref name="cost"/> coins. A negative cost is never affordable.</summary>
	public bool CanAfford(int cost) => cost >= 0 && cost <= Coins;

	/// <summary>Takes <paramref name="cost"/> coins and returns true; when Ryo cannot afford it (or the cost is negative) nothing changes and it returns false.</summary>
	public bool TrySpend(int cost)
	{
		if (!CanAfford(cost)) return false;
		Coins -= cost;
		return true;
	}

	/// <summary>Puts back the purse of a saved run (0 or more).</summary>
	public void RestoreCoins(int coins)
	{
		if (coins < 0) throw new ArgumentOutOfRangeException(nameof(coins), coins, "coins can't be negative");
		Coins = coins;
	}

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
		// T3: the level-up order decides the Tanuki's copy target, so it is run state. A run in which no
		// special in a slot has levelled keeps the hash it had before the copy rule (recorded replays).
		bool anyLevelled = false;
		for (int i = 1; i < _slots.Length; i++) anyLevelled |= LevelledOrder((SpecialSlot)i) > 0;
		if (anyLevelled)
			for (int i = 1; i < _slots.Length; i++) h = Fnv.Mix(h, LevelledOrder((SpecialSlot)i));
		if (Map is not null) // a run without a map keeps the hash it had before YOK-76 (recorded replays)
		{
			ulong m = Map.StateHash();
			h = Fnv.Mix(h, unchecked((int)m));
			h = Fnv.Mix(h, unchecked((int)(m >> 32)));
		}
		if (Coins != 0) h = Fnv.Mix(Fnv.MixString(h, "coins"), Coins); // R8: a run with an empty purse keeps the hash it had before YOK-85
		// R6: borrowed ink is run state (it decides M3's carry-over). A run that never bought keeps the hash it had before YOK-95.
		int borrowed = 0;
		for (int i = 1; i < _slots.Length; i++) if (_slots[i] is { Borrowed: true }) borrowed |= 1 << i;
		if (borrowed != 0) h = Fnv.Mix(Fnv.MixString(h, "borrowed"), borrowed);
		return h;
	}
}
