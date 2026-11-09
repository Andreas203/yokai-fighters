using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace YokaiFighters.Sim;

/// <summary>
/// YOK-47: every ability the draft can offer, read through the YOK-12 loaders: specials (kind "special",
/// <c>data/moves/</c>), modifiers (kind "modifier", <c>data/modifiers/</c>) and, since YOK-186, cancel rules (kind
/// "cancel_rule", <c>data/cancels/</c>: offered by elders only, X1, A9). Card text is each file's <c>card</c> block (A12).
/// Lists are sorted by file name so seeded draws are deterministic.
/// </summary>
public sealed class AbilityPool
{
	public IReadOnlyList<SpecialData> Specials { get; }
	public IReadOnlyList<ModifierData> Modifiers { get; }
	/// <summary>YOK-186: the cancel rules an elder's draft may offer (X1, X4); empty when the pool was loaded without a cancels folder.</summary>
	public IReadOnlyList<CancelRuleData> CancelRules { get; }
	/// <summary>Which folders fed the pool (fixture fallbacks are listed here for the log).</summary>
	public IReadOnlyList<string> Notes { get; }
	/// <summary>
	/// YOK-153 (M1, A11): the ids the save has unlocked, handed in by the caller (<see cref="WithUnlocks"/>); Sim code
	/// never reads the save. Empty by default: only abilities whose data says <c>locked: false</c> are available.
	/// </summary>
	public IReadOnlySet<string> Unlocked { get; private init; } = EmptyIds;

	static readonly IReadOnlySet<string> EmptyIds = new HashSet<string>(StringComparer.Ordinal);

	public AbilityPool(IEnumerable<SpecialData> specials, IEnumerable<ModifierData> modifiers, IEnumerable<string>? notes = null,
		IEnumerable<CancelRuleData>? cancelRules = null)
	{
		Specials = specials.ToArray();
		Modifiers = modifiers.ToArray();
		CancelRules = cancelRules?.ToArray() ?? Array.Empty<CancelRuleData>();
		Notes = notes?.ToArray() ?? Array.Empty<string>();
		// One id space: the save's unlock list (M1) names specials, modifiers and cancel rules alike.
		var dup = Specials.Select(s => s.Id).Concat(Modifiers.Select(m => m.Id)).Concat(CancelRules.Select(c => c.Id))
			.GroupBy(x => x).FirstOrDefault(g => g.Count() > 1);
		if (dup != null) throw new FormatException($"ability id '{dup.Key}' is defined twice");
		// Fail at load, not at the reward screen: every modifier builds on every special it applies to.
		foreach (var m in Modifiers)
			foreach (var s in Specials.Where(m.AppliesTo))
				for (int level = 1; level <= s.MaxLevel; level++)
				{
					s.Build(level, false, m, null);
					if (s.HasEx) s.Build(level, true, m, null);
				}
	}

	/// <summary>
	/// The same abilities with <paramref name="unlockedIds"/> (the save's unlocks, M1) as the unlocked set, replacing any
	/// set before. The data is shared, not rebuilt, and its <c>locked</c> flags are untouched (the unlock order is read
	/// from them). An id that names no ability, or a blank one, changes nothing.
	/// </summary>
	public AbilityPool WithUnlocks(IEnumerable<string>? unlockedIds) => new(this)
	{
		Unlocked = new HashSet<string>((unlockedIds ?? Array.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.Ordinal),
	};

	AbilityPool(AbilityPool other)
	{
		Specials = other.Specials;
		Modifiers = other.Modifiers;
		CancelRules = other.CancelRules;
		Notes = other.Notes;
	}

	/// <summary>
	/// YOK-153, A11/M1: the draft (A7) and the shop (R6) may offer this special: its data is not locked, or the save
	/// unlocked its id. Its evolution comes with it (A5: never locked on its own). Source, rarity and ownership are
	/// the caller's filters (A8, A9).
	/// </summary>
	public bool IsAvailable(SpecialData s) => !s.Locked || Unlocked.Contains(s.Id);

	/// <summary>As <see cref="IsAvailable(SpecialData)"/>, for a modifier.</summary>
	public bool IsAvailable(ModifierData m) => !m.Locked || Unlocked.Contains(m.Id);

	/// <summary>YOK-186: as <see cref="IsAvailable(SpecialData)"/>, for a cancel rule (Undertow starts locked, X4 ◆).</summary>
	public bool IsAvailable(CancelRuleData c) => !c.Locked || Unlocked.Contains(c.Id);

	public SpecialData? Special(string id) => Specials.FirstOrDefault(s => s.Id == id);
	public ModifierData? Modifier(string id) => Modifiers.FirstOrDefault(m => m.Id == id);
	public CancelRuleData? CancelRule(string id) => CancelRules.FirstOrDefault(c => c.Id == id);
	public IEnumerable<SpecialData> Starters => Specials.Where(s => s.Starter);

	/// <summary>
	/// Real data first; while it is missing, the TEST FIXTURES (movesmith delivers real files after the Rules
	/// Lawyer gate). YOK-56: starters are Ryo's kit (<see cref="Kit.LoadSpecials"/>, A1), with its own fixture
	/// fallback. Drafted specials are every non-starter special in <paramref name="movesDir"/> whatever its
	/// source (the draft filters by the beaten yokai, A8; the Tanuki copy rule sees all of them), and modifiers
	/// every file in <paramref name="modifiersDir"/>; the fixtures in <paramref name="fixtureRewardsDir"/> fill in
	/// per source yokai, only for a yokai the data has none of.
	/// </summary>
	public static AbilityPool Load(string movesDir, string modifiersDir, string fixtureSpecialsDir, string fixtureRewardsDir) =>
		Load(new KitSources(movesDir, "", "", "", fixtureSpecialsDir, fixtureRewardsDir), modifiersDir);

	/// <param name="cancelsDir">YOK-186: <c>data/cancels/</c>; empty = no cancel rules (fixture pools).</param>
	public static AbilityPool Load(KitSources src, string modifiersDir, string cancelsDir = "")
	{
		var notes = new List<string>();
		var specials = Kit.LoadSpecials(Kit.Ryo, src, notes).ToList();
		var drafted = SpecialLoader.LoadDirectory(src.MovesDir).Where(s => !s.Starter).ToList();
		var fixtureDrafted = SpecialLoader.LoadDirectory(src.FixtureRewardsDir).Where(s => !s.Starter)
			.Where(s => !drafted.Any(d => d.Source == s.Source)).ToList();
		foreach (var y in fixtureDrafted.Select(s => s.Source).Distinct())
			notes.Add($"{y}: no drafted specials in {src.MovesDir}, TEST FIXTURE rewards");
		specials.AddRange(drafted);
		specials.AddRange(fixtureDrafted);

		var modifiers = ModifierLoader.LoadDirectory(modifiersDir).ToList();
		var fixtureMods = ModifierLoader.LoadDirectory(src.FixtureRewardsDir)
			.Where(f => !modifiers.Any(m => m.Source == f.Source)).ToList();
		foreach (var y in fixtureMods.Select(m => m.Source).Distinct())
			notes.Add($"{y}: no modifiers in {modifiersDir}, TEST FIXTURE modifiers");
		modifiers.AddRange(fixtureMods);
		var cancels = cancelsDir.Length > 0 ? CancelRuleLoader.LoadDataDirectory(cancelsDir) : Array.Empty<CancelRuleData>();
		return new AbilityPool(specials, modifiers, notes, cancels);
	}

	/// <summary>Loads from the repo root (the folder holding <c>data/</c> and <c>game/</c>).</summary>
	public static AbilityPool LoadRepo(string repoRoot) =>
		Load(KitSources.Repo(repoRoot), Path.Combine(repoRoot, "data", "modifiers"), Path.Combine(repoRoot, "data", "cancels"));

	/// <summary>The TEST FIXTURE pool only: tests that must not depend on what is in <c>data/</c>.</summary>
	public static AbilityPool LoadFixtures(string repoRoot) =>
		Load(KitSources.Repo(repoRoot).FixturesOnly(), "");
}
