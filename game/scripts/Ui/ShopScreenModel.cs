using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using YokaiFighters.Sim;

namespace YokaiFighters.Ui;

public enum ShopKind { Special, Modifier }

/// <summary>How a shelf card reads: takeable, refused (with a reason), or already bought on this visit.</summary>
public enum ShopCardState { Buyable, Refused, Bought }

/// <summary>One card on a shelf. Text comes from the ability's card block (A12); price, state and reason from <see cref="MerchantShop"/>.</summary>
public sealed record ShopEntry(ShopKind Kind, string Id, string Name, string Source, string Type, string Plain, string Frames, int Price, ShopCardState State, string? Reason);

/// <summary>One of Ryo's four slots. <c>Locked</c> = refused although Ryo can pay (a starter, his last special, a closed shop).</summary>
public sealed record ShopSlotRow(SpecialSlot Slot, bool Empty, string SpecialId, string Name, int Level, string? ModifierName, bool Borrowed, bool ModifierBorrowed,
	bool IsTanukiTarget, bool CanRelease, bool Locked, string? Reason);

/// <summary>One slot as a modifier's target: offered only when <see cref="MerchantShop.ModifierTargets"/> lists it.</summary>
public sealed record ShopTargetRow(SpecialSlot Slot, string Name, int Level, bool Offered, string? Reason);

/// <summary>
/// What the shop screen shows and does (YOK-104, R6, A2, T2, T5; pure C#, no Godot). Every rule lives in
/// <see cref="MerchantShop"/> (shelves, prices, eligibility, refusals and their reasons) and <see cref="CopyRule"/>;
/// this class only asks them, so a refused action changes nothing here either. The one thing it remembers is which
/// cards were bought on this visit, so a bought card stays on its shelf as "BOUGHT" instead of vanishing.
/// </summary>
public sealed class ShopScreenModel
{
	readonly RunSession _session;
	readonly Merchant _merchant;
	readonly List<string> _specialOrder = new(), _modifierOrder = new();
	readonly HashSet<string> _bought = new(StringComparer.Ordinal);

	ShopScreenModel(RunSession session, Merchant merchant)
	{
		_session = session; _merchant = merchant;
		_specialOrder.AddRange(Shop.Shelf.Select(s => s.Id));
		_modifierOrder.AddRange(Shop.ModifierShelf.Select(m => m.Id));
	}

	/// <summary>The model for the merchant stop Ryo is standing at, or null when he is not at one.</summary>
	public static ShopScreenModel? Create(RunSession session) =>
		session?.Merchant is { } m && session.Node?.Type == NodeTypeRegistry.Merchant ? new ShopScreenModel(session, m) : null;

	MerchantShop Shop => _merchant.Shop;
	RunState Run => _session.Run;

	public int Health => Run.Health;
	public int MaxHealth => RunState.MaxHealth;
	public int Coins => Run.Coins;
	public int BuyPrice => MerchantShop.BuyCost;
	public int ReleasePrice => MerchantShop.ReleaseCost;
	/// <summary>The shop is open: the stop's choice was the shop and Ryo still stands at it.</summary>
	public bool Open => Shop.Open;

	/// <summary>Strips the rule ids the shop's reasons carry for the developer, "(R6)", "(A2: one modifier per special)".</summary>
	public static string Clean(string reason) => Regex.Replace(reason, @"\s*\([A-Z]\d+[^)]*\)", "").Trim();

	public IReadOnlyList<ShopEntry> Specials
	{
		get
		{
			var live = Shop.Shelf;
			Sync(_specialOrder, live.Select(s => s.Id));
			var list = new List<ShopEntry>();
			foreach (string id in _specialOrder)
			{
				if (live.FirstOrDefault(s => s.Id == id) is { } s)
				{
					string? why = Shop.WhyNotBuy(id);
					list.Add(new(ShopKind.Special, id, Name(s.Name, id), Title(s.Source), SlotType(s), s.CardPlain, s.CardFrames, BuyPrice, why is null ? ShopCardState.Buyable : ShopCardState.Refused, why is null ? null : Clean(why)));
				}
				else if (_bought.Contains(id) && Run.Owned.Select(o => o.Special.Data).FirstOrDefault(d => d.Id == id) is { } held)
					list.Add(new(ShopKind.Special, id, Name(held.Name, id), Title(held.Source), SlotType(held), held.CardPlain, held.CardFrames, BuyPrice, ShopCardState.Bought, null));
			}
			return list;
		}
	}

	public IReadOnlyList<ShopEntry> Modifiers
	{
		get
		{
			var live = Shop.ModifierShelf;
			Sync(_modifierOrder, live.Select(m => m.Id));
			var list = new List<ShopEntry>();
			foreach (string id in _modifierOrder)
			{
				if (live.FirstOrDefault(m => m.Id == id) is { } m)
				{
					var targets = Shop.ModifierTargets(id);
					// Card-level reason: the shop's own for a slot that could take it; with none, the coin reason if short, else a plain one.
					string? why = targets.Count > 0 ? Shop.WhyNotBuyModifier(id, targets[0])
						: !Shop.Open ? Shop.WhyNotBuyModifier(id, SpecialSlot.A)
						: !Run.CanAfford(BuyPrice) ? $"{BuyPrice} coins needed, Ryo has {Run.Coins}"
						: Run.Owned.Any() ? "no special can carry it" : "Ryo holds no special";
					list.Add(new(ShopKind.Modifier, id, Name(m.Name, id), Title(m.Source), "modifier", m.CardPlain, m.CardFrames, BuyPrice, why is null ? ShopCardState.Buyable : ShopCardState.Refused, why is null ? null : Clean(why)));
				}
				else if (_bought.Contains(id) && Run.Owned.Select(o => o.Special.Modifier).FirstOrDefault(x => x?.Id == id) is { } held)
					list.Add(new(ShopKind.Modifier, id, Name(held.Name, id), Title(held.Source), "modifier", held.CardPlain, held.CardFrames, BuyPrice, ShopCardState.Bought, null));
			}
			return list;
		}
	}

	/// <summary>Keeps the visit's card order: ids no longer live and not bought here drop out, new live ids join at the end.</summary>
	void Sync(List<string> order, IEnumerable<string> live)
	{
		var liveSet = live.ToList();
		order.RemoveAll(id => !liveSet.Contains(id) && !_bought.Contains(id));
		foreach (string id in liveSet) if (!order.Contains(id)) order.Add(id);
	}

	/// <summary>The slot the Tanuki would copy now (T2) or has locked (T1), from the same query the rest stop's forecast uses.</summary>
	public SpecialSlot? TanukiTarget => RestStopScreenForecast.For(_session)?.Slot;

	public IReadOnlyList<ShopSlotRow> Slots
	{
		get
		{
			var rows = new List<ShopSlotRow>();
			var target = TanukiTarget;
			foreach (var slot in new[] { SpecialSlot.A, SpecialSlot.B, SpecialSlot.C, SpecialSlot.D })
			{
				if (Run[slot] is not { } o) { rows.Add(new(slot, true, "", "", 0, null, false, false, false, false, false, null)); continue; }
				string? why = Shop.WhyNotRelease(slot);
				// T5 and the starter rule lock the row whatever the coins; only a plain shortfall reads "Need 40".
				bool locked = why is not null && !why.StartsWith($"{ReleasePrice} coins needed");
				rows.Add(new(slot, false, o.Data.Id, Name(o.Data.Name, o.Data.Id), o.Level, o.Modifier is { } m ? Name(m.Name, m.Id) : null, o.Borrowed, o.ModifierBorrowed,
					target == slot, why is null, locked, why is null ? null : Clean(why)));
			}
			return rows;
		}
	}

	/// <summary>The four slots as targets for <paramref name="modifierId"/>: offered only the ones the shop lists, the rest with the shop's own reason.</summary>
	public IReadOnlyList<ShopTargetRow> TargetsFor(string modifierId)
	{
		var offered = Shop.ModifierTargets(modifierId);
		var rows = new List<ShopTargetRow>();
		foreach (var (slot, o) in Run.Owned)
		{
			bool ok = offered.Contains(slot);
			string? why = Shop.WhyNotBuyModifier(modifierId, slot);
			rows.Add(new(slot, Name(o.Data.Name, o.Data.Id), o.Level, ok && why is null, why is null ? null : Clean(why)));
		}
		return rows;
	}

	public MapResult Buy(string specialId)
	{
		var r = Shop.Buy(specialId);
		if (r.Ok) _bought.Add(specialId);
		return r;
	}

	public MapResult BuyModifier(string modifierId, SpecialSlot slot)
	{
		var r = Shop.BuyModifier(modifierId, slot);
		if (r.Ok) _bought.Add(modifierId);
		return r;
	}

	public MapResult Release(SpecialSlot slot) => Shop.Release(slot);

	static string Name(string name, string id) => name.Length > 0 ? name : id;

	static string Title(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

	static string SlotType(SpecialData s) => "special";
}
