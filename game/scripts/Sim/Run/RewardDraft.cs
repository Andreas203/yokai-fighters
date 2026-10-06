using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace YokaiFighters.Sim;

/// <summary>
/// YOK-47: the reward draft after a duel (A7): up to three cards from the beaten yokai, built from data with
/// a seeded <see cref="SimRng"/> (same run + pool + seed = same offer). Card order: NEW, UPGRADE, MODIFIER.
/// <list type="bullet">
/// <item>NEW: an unlocked special sourced from the yokai (A8; rare only from elders, A9) Ryo doesn't own;
/// it goes to its own slot at Lv 1 (A3).</item>
/// <item>UPGRADE (always offered while an owned special can level, A7): one owned special +1 level, the step
/// from its data (A4). One seeded target by default; <c>upgradeChoice</c> lets the player pick any.</item>
/// <item>MODIFIER: an unlocked modifier of the yokai with at least one eligible owned special (applies_to,
/// no modifier yet: A2); the player picks the special.</item>
/// </list>
/// A missing card is replaced by another NEW or MODIFIER card from the pool when one exists, else the offer
/// has fewer cards. <see cref="Pick"/> applies the card to the <see cref="RunState"/>; the next match built
/// with <see cref="RunState.ApplyTo"/> uses it.
/// </summary>
public sealed class RewardDraft : IRewardDraft
{
	readonly RunState _run;
	readonly List<(CardView View, SpecialData? Special, ModifierData? Modifier)> _cards = new();

	public RewardOffer Offer { get; }
	public bool Picked { get; private set; }

	RewardDraft(RunState run, string yokai, List<(CardView, SpecialData?, ModifierData?)> cards)
	{
		_run = run;
		_cards = cards;
		Offer = new RewardOffer(yokai, cards.Select(c => c.Item1).ToArray());
	}

	public static RewardDraft Build(RunState run, AbilityPool pool, string yokai, uint seed, bool elder = false, bool upgradeChoice = false)
	{
		var rng = new SimRng(seed);
		bool Offered(string source, string rarity, bool locked) => source == yokai && !locked && (rarity == "common" || elder);

		var newPool = pool.Specials.Where(s => !s.Starter && Offered(s.Source, s.Rarity, s.Locked) && !run.Owns(s.Id)).ToList();
		var modPool = pool.Modifiers.Where(m => Offered(m.Source, m.Rarity, m.Locked) && run.Owned.Any(o => run.CanAttach(o.Slot, m))).ToList();
		var upgradable = run.Owned.Where(o => run.CanLevelUp(o.Slot)).ToList();

		var cards = new List<(CardView, SpecialData?, ModifierData?)>();
		void AddNew()
		{
			var s = Take(newPool, rng);
			cards.Add((NewCard(cards.Count, run, s), s, null));
		}
		void AddModifier()
		{
			var m = Take(modPool, rng);
			cards.Add((ModifierCard(cards.Count, run, m), null, m));
		}

		if (newPool.Count > 0) AddNew();
		if (upgradable.Count > 0)
		{
			var targets = upgradeChoice ? upgradable : new() { upgradable[rng.Next(upgradable.Count)] };
			cards.Add((UpgradeCard(cards.Count, run, targets.Select(t => t.Slot).ToList()), null, null));
		}
		if (modPool.Count > 0) AddModifier();
		while (cards.Count < 3 && (modPool.Count > 0 || newPool.Count > 0))
		{
			if (modPool.Count > 0) AddModifier(); else AddNew();
		}
		return new RewardDraft(run, yokai, cards);
	}

	static T Take<T>(List<T> list, SimRng rng)
	{
		int i = rng.Next(list.Count);
		var x = list[i];
		list.RemoveAt(i);
		return x;
	}

	public PickResult Pick(int cardIndex, SpecialSlot? target = null)
	{
		if (Picked) return PickResult.Fail("a card was already picked");
		if (cardIndex < 0 || cardIndex >= _cards.Count) return PickResult.Fail($"no card {cardIndex}");
		var (view, special, modifier) = _cards[cardIndex];
		SpecialSlot slot;
		if (target is SpecialSlot t)
		{
			if (!view.Targets.Any(x => x.Slot == t)) return PickResult.Fail($"{view.Name} can't go to slot {t}");
			slot = t;
		}
		else if (view.Targets.Count == 1) slot = view.Targets[0].Slot;
		else return PickResult.Fail($"{view.Name}: pick a special");

		int level;
		switch (view.Kind)
		{
			case CardKind.NewSpecial:
				_run.Equip(special!);
				level = _run[slot]!.Level;
				break;
			case CardKind.Upgrade:
				level = _run.LevelUp(slot);
				break;
			default:
				_run.Attach(slot, modifier!);
				level = _run[slot]!.Level;
				break;
		}
		Picked = true;
		return new PickResult(true, "", view, slot, level);
	}

	// ---- card text (A12: plain language first, frame data behind the toggle) ----

	static CardView NewCard(int index, RunState run, SpecialData s)
	{
		var replaced = run[s.Slot];
		string where = replaced is null ? $"Goes in slot {s.Slot}." : $"Replaces {replaced.Data.Name} in slot {s.Slot} (its levels are lost).";
		var target = new CardTarget(s.Slot, s.Id, s.Name, 1, where, FrameSummary(s.Build(1, false)));
		return new CardView(index, CardKind.NewSpecial, "NEW", s.Id, s.Name, s.CardPlain, s.CardFrames, s.Source, new[] { target });
	}

	static CardView UpgradeCard(int index, RunState run, List<SpecialSlot> slots)
	{
		var targets = slots.Select(slot =>
		{
			var o = run[slot]!;
			int next = o.Level + 1;
			string plain = next == 3 && o.Data.EvolutionName != ""
				? $"{o.Data.Name} evolves into {o.Data.EvolutionName}. {o.Data.EvolutionPlain}".Trim()
				: $"{o.Data.Name} Lv {o.Level} → Lv {next}: {Describe(o.Data.Level2Step)}.";
			string frames = $"Lv {o.Level} → Lv {next}\n" + FrameDiff(o.Data.Build(o.Level, false, o.Modifier, null), o.Data.Build(next, false, o.Modifier, null));
			return new CardTarget(slot, o.Data.Id, o.Data.Name, o.Level, plain, frames);
		}).ToArray();
		if (targets.Length == 1)
		{
			var t = targets[0];
			return new CardView(index, CardKind.Upgrade, "UPGRADE", t.SpecialId, t.SpecialName, t.Plain, t.Frames, "ryo", targets);
		}
		return new CardView(index, CardKind.Upgrade, "UPGRADE", "upgrade", "Upgrade", "Raise one of your specials a level.",
			string.Join("\n", targets.Select(t => $"{t.SpecialName}: {t.Frames.Replace('\n', ' ')}")), "ryo", targets);
	}

	static CardView ModifierCard(int index, RunState run, ModifierData m)
	{
		var targets = run.Owned.Where(o => run.CanAttach(o.Slot, m)).Select(o =>
		{
			var before = o.Special.Data.Build(o.Special.Level, false, null, null);
			var after = o.Special.Data.Build(o.Special.Level, false, m, null);
			return new CardTarget(o.Slot, o.Special.Data.Id, o.Special.Data.Name, o.Special.Level,
				$"{m.Name} on {o.Special.Data.Name}.", FrameDiff(before, after));
		}).ToArray();
		return new CardView(index, CardKind.Modifier, "MODIFIER", m.Id, m.Name, m.CardPlain, m.CardFrames, m.Source, targets);
	}

	/// <summary>Plain wording of one effect, from data only ("recovery -4", "damage +10%").</summary>
	public static string Describe(JsonNode? effect)
	{
		if (effect is null) return "no change";
		string target = (effect["target"]?.GetValue<string>() ?? "").Replace('_', ' ').Replace('.', ' ');
		string op = effect["op"]?.GetValue<string>() ?? "";
		var v = effect["value"];
		return op switch
		{
			"add" => $"{target} {v!.GetValue<int>():+0;-0}",
			"mul_pct" => $"{target} {v!.GetValue<int>() - 100:+0;-0}%",
			_ => $"{target} = {v?.ToJsonString()}",
		};
	}

	/// <summary>"15 / 4 / 30, 70 dmg, projectile speed 6".</summary>
	public static string FrameSummary(MoveData m)
	{
		string active = m.Projectile != null && m.Active == 0 ? "—" : m.Active.ToString();
		string s = $"{m.Startup} / {active} / {m.Recovery}, {m.Damage} dmg";
		if (m.Projectile is { } p) s += $", projectile speed {p.Speed}";
		return s;
	}

	/// <summary>Only what changed, e.g. "recovery 30 → 26, projectile speed 12 → 16".</summary>
	public static string FrameDiff(MoveData a, MoveData b, SimConfig? cfg = null)
	{
		cfg ??= SimConfig.Default;
		var parts = new List<string>();
		void D(string name, int x, int y) { if (x != y) parts.Add($"{name} {x} → {y}"); }
		D("startup", a.Startup, b.Startup);
		D("active", a.Active, b.Active);
		D("recovery", a.Recovery, b.Recovery);
		D("damage", a.Damage, b.Damage);
		D("hitstun", a.Hitstun, b.Hitstun);
		D("blockstun", a.Blockstun, b.Blockstun);
		D("meter on hit", a.MeterOnHit ?? cfg.MeterOnHit, b.MeterOnHit ?? cfg.MeterOnHit);
		D("meter on block", a.MeterOnBlock ?? cfg.MeterOnBlock, b.MeterOnBlock ?? cfg.MeterOnBlock);
		if (a.Projectile is { } pa && b.Projectile is { } pb)
		{
			D("projectile speed", pa.Speed, pb.Speed);
			D("projectile hits", pa.Hits, pb.Hits);
			D("absorbs projectiles", pa.AbsorbsProjectiles, pb.AbsorbsProjectiles);
		}
		return parts.Count > 0 ? string.Join(", ", parts) : "no frame-data change (" + FrameSummary(b) + ")";
	}
}
