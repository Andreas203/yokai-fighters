using System;
using YokaiFighters.Sim;

namespace YokaiFighters.Ui;

public enum RestStopKind { Merchant, Dojo }

/// <summary>How one of the stop's two option cards reads (R6, R7: one choice per stop).</summary>
public enum RestStopOption { Ready, Taken, Closed, ComingSoon }

/// <summary>
/// What the rest-stop screen shows and does, read from the <see cref="RunSession"/> (pure C#, no Godot). Every rule
/// lives in <see cref="Merchant"/> / <see cref="Dojo"/>; this class only asks them and reports what they say, so
/// a refused action (second choice, a stop already left) changes nothing here either.
/// </summary>
public sealed class RestStopScreenModel
{
	readonly RunSession _session;
	int _gain;

	public RestStopKind Kind { get; }
	public int Row { get; }

	RestStopScreenModel(RunSession session, RestStopKind kind, int row) { _session = session; Kind = kind; Row = row; }

	/// <summary>The model for the node Ryo is standing at, or null when it is not a merchant or a dojo.</summary>
	public static RestStopScreenModel? Create(RunSession session)
	{
		if (session?.Node is not { } node) return null;
		return node.Type switch
		{
			NodeTypeRegistry.Merchant when session.Merchant is not null => new(session, RestStopKind.Merchant, node.Row),
			NodeTypeRegistry.Dojo when session.Dojo is not null => new(session, RestStopKind.Dojo, node.Row),
			_ => null,
		};
	}

	public string RowLine => Kind == RestStopKind.Merchant ? $"Row {Row} · the merchant's lantern" : $"Row {Row} · the dojo at the bend";
	public int Health => _session.Run.Health;
	public int MaxHealth => RunState.MaxHealth;
	public int Coins => _session.Run.Coins;

	/// <summary>The percent of full health the free option gives back (R6 25, R7 30), from the rule's own constant.</summary>
	public int FreePercent => Kind == RestStopKind.Merchant ? Merchant.HealPercent : Dojo.RestHealPercent;

	/// <summary>Health after the free option from now (the card's preview), or the health Ryo has once it is taken.</summary>
	public int HealthAfterFree => Kind == RestStopKind.Merchant ? Merchant.Healed(Health) : Math.Min(MaxHealth, Health + Dojo.RestHeal);

	/// <summary>Health the free option adds from now; once it is taken, what it did add.</summary>
	public int FreeGain => ChoiceMade ? _gain : HealthAfterFree - Health;

	/// <summary>Both cards: the stop's one choice is spent, whichever card it was.</summary>
	public bool ChoiceMade => Kind == RestStopKind.Merchant ? _session.Merchant is { Choice: not MerchantChoice.None } : _session.Dojo is { Choice: not DojoChoice.None };

	/// <summary>The stop is still the one Ryo is standing at (it is not after the node is completed or the run ends).</summary>
	public bool Open => Kind == RestStopKind.Merchant ? _session.Merchant is { Open: true } : _session.Dojo is { IsOpen: true };

	public bool CanTakeFree => Kind == RestStopKind.Merchant ? _session.Merchant is { CanHeal: true } : _session.Dojo is { CanRest: true };

	/// <summary>The shop screen is YOK-104 and the trial YOK-98: until they land the second card is shown, not takeable.</summary>
	public static bool SecondCardBuilt { get; set; }

	public RestStopOption Free => !ChoiceMade ? RestStopOption.Ready
		: Kind == RestStopKind.Merchant ? (_session.Merchant!.Choice == MerchantChoice.Heal ? RestStopOption.Taken : RestStopOption.Closed)
		: RestStopOption.Taken;

	public RestStopOption Second => ChoiceMade
		? (Kind == RestStopKind.Merchant && _session.Merchant!.Choice == MerchantChoice.Shop ? RestStopOption.Taken : RestStopOption.Closed)
		: SecondCardBuilt ? RestStopOption.Ready : RestStopOption.ComingSoon;

	/// <summary>R6 / R7: takes the free heal or the rest. Refused, changing nothing, when the choice is already made or the stop is left.</summary>
	public MapResult TakeFree()
	{
		if (!Open) return MapResult.Fail("Ryo is no longer at this stop");
		int before = Health;
		var r = Kind == RestStopKind.Merchant ? _session.Merchant!.TakeHeal() : _session.Dojo!.Rest();
		if (r.Ok) _gain = Health - before;
		return r;
	}

	/// <summary>Leaves the stop for the map. Only once the choice is made: leaving without one is an open designer question, so it is not offered.</summary>
	public MapResult Leave() => ChoiceMade ? _session.CompleteNode() : MapResult.Fail("pick one first: the free option or the second one");

	public RestStopScreenForecast? Forecast => RestStopScreenForecast.For(_session);
}
