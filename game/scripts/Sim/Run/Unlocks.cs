using System;
using System.Collections.Generic;
using System.Linq;

namespace YokaiFighters.Sim;

/// <summary>
/// YOK-86, M1: how many locked abilities a run unlocks, by the depth it reached, and which ones, in a fixed order.
/// Pure C#, no RNG: the award depends only on how the run ended (<see cref="RunEnd"/>), the order and what the save
/// has already unlocked, so dying on purpose never beats pushing on.
/// <para>
/// PROVISIONAL (designer to confirm; the order of the 12 unlocks is an open question in the decision log):
/// <see cref="FromData"/> derives the order from the data's own <c>locked</c> flags (A11): locked specials, then
/// locked modifiers, then locked cancel rules, each in the loaders' file-name order. An order the designer writes
/// is handed to the constructor instead. Ids already unlocked are skipped, so an award is always the next ones
/// still locked; when every one is unlocked a run awards nothing.
/// </para>
/// </summary>
public sealed class Unlocks
{
	/// <summary>M1: the first row whose run earns an unlock (rows 6-8 → 1; row 5 or earlier → 0).</summary>
	public const int MinRow = 6;
	/// <summary>M1: a run that reached row 6 or further but not the Tanuki.</summary>
	public const int ForRowSix = 1;
	/// <summary>M1: a run that reached the Tanuki and lost there.</summary>
	public const int ForReachingTanuki = 2;
	/// <summary>M1: a Tanuki win.</summary>
	public const int ForTanukiWin = 4;

	/// <summary>The fixed award order: every locked ability id, each once.</summary>
	public IReadOnlyList<string> Order { get; }

	public Unlocks(IEnumerable<string> order)
	{
		if (order is null) throw new ArgumentNullException(nameof(order));
		var list = order.ToArray();
		if (list.Any(string.IsNullOrWhiteSpace)) throw new FormatException("unlock order: an id is blank");
		var dup = list.GroupBy(x => x, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);
		if (dup != null) throw new FormatException($"unlock order: '{dup.Key}' is listed twice");
		Order = list;
	}

	/// <summary>
	/// The provisional order from data: the pool's locked specials, then its locked modifiers (both in pool order,
	/// sorted by file name), then the locked cancel-rule ids in the order given (<see cref="CancelRuleLoader.LockedIds"/>
	/// sorts by file name). Starters are never locked (A1) and are left out even if a file says so.
	/// </summary>
	public static Unlocks FromData(AbilityPool pool, IEnumerable<string>? lockedCancelIds = null) =>
		FromData(pool, lockedCancelIds, out _);

	/// <summary>
	/// As <see cref="FromData(AbilityPool, IEnumerable{string}?)"/>; an id named twice in the data (a modifier and a
	/// cancel rule sharing one, or a blank cancel id) is kept once, first occurrence wins, and listed in
	/// <paramref name="dropped"/> for the caller's log, so a data slip never aborts a run-end save. A designer-written
	/// order given to the constructor stays strict.
	/// </summary>
	public static Unlocks FromData(AbilityPool pool, IEnumerable<string>? lockedCancelIds, out IReadOnlyList<string> dropped)
	{
		if (pool is null) throw new ArgumentNullException(nameof(pool));
		var all = pool.Specials.Where(s => s.Locked && !s.Starter).Select(s => s.Id)
			.Concat(pool.Modifiers.Where(m => m.Locked).Select(m => m.Id))
			.Concat(lockedCancelIds ?? Array.Empty<string>());
		var seen = new HashSet<string>(StringComparer.Ordinal);
		var order = new List<string>();
		var skipped = new List<string>();
		foreach (string id in all)
		{
			if (string.IsNullOrWhiteSpace(id) || !seen.Add(id)) { skipped.Add(id ?? ""); continue; }
			order.Add(id);
		}
		dropped = skipped;
		return new Unlocks(order);
	}

	/// <summary>
	/// YOK-153: the log line for one id <see cref="FromData(AbilityPool, IEnumerable{string}?, out IReadOnlyList{string})"/>
	/// dropped: a blank id says it is blank; any other was named twice.
	/// </summary>
	public static string WhyDropped(string? id) =>
		string.IsNullOrWhiteSpace(id) ? "a blank id in the data was left out" : $"'{id}' is named twice in the data; kept once";

	/// <summary>
	/// M1: unlocks earned by a run that ended this way. A Tanuki win → 4; ended on the Tanuki's node (lost) → 2;
	/// reached row 6 or further otherwise → 1; row 5 or earlier → 0. "Reached the Tanuki" is
	/// <see cref="RunEnd.AtTanuki"/> (the copy-lock node), not a row number, so it holds on any map.
	/// </summary>
	public static int CountFor(RunEnd end)
	{
		if (end is null) throw new ArgumentNullException(nameof(end));
		if (end.BeatTanuki) return ForTanukiWin;
		if (end.AtTanuki) return ForReachingTanuki;
		return end.RowReached >= MinRow ? ForRowSix : 0;
	}

	/// <summary>The ids of <see cref="Order"/> not yet in <paramref name="unlocked"/>, in order.</summary>
	public IReadOnlyList<string> StillLocked(IEnumerable<string> unlocked)
	{
		var have = new HashSet<string>(unlocked ?? throw new ArgumentNullException(nameof(unlocked)), StringComparer.Ordinal);
		return Order.Where(id => !have.Contains(id)).ToArray();
	}

	/// <summary>
	/// M1 at run end: the ids this run unlocks, in award order: the first <see cref="CountFor"/> of
	/// <see cref="StillLocked"/>. Fewer when fewer are left; empty below row 6.
	/// </summary>
	public IReadOnlyList<string> Award(RunEnd end, IEnumerable<string> unlocked)
	{
		int n = CountFor(end);
		return n == 0 ? Array.Empty<string>() : StillLocked(unlocked).Take(n).ToArray();
	}
}
