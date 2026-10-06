using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace YokaiFighters.Sim;

/// <summary>
/// YOK-47: every ability the draft can offer, read through the YOK-12 loaders: specials (kind "special",
/// <c>data/moves/</c>) and modifiers (kind "modifier", <c>data/modifiers/</c>). Card text is each file's
/// <c>card</c> block (A12). Lists are sorted by file name so seeded draws are deterministic.
/// </summary>
public sealed class AbilityPool
{
	public IReadOnlyList<SpecialData> Specials { get; }
	public IReadOnlyList<ModifierData> Modifiers { get; }
	/// <summary>Which folders fed the pool (fixture fallbacks are listed here for the log).</summary>
	public IReadOnlyList<string> Notes { get; }

	public AbilityPool(IEnumerable<SpecialData> specials, IEnumerable<ModifierData> modifiers, IEnumerable<string>? notes = null)
	{
		Specials = specials.ToArray();
		Modifiers = modifiers.ToArray();
		Notes = notes?.ToArray() ?? Array.Empty<string>();
		var dup = Specials.Select(s => s.Id).Concat(Modifiers.Select(m => m.Id)).GroupBy(x => x).FirstOrDefault(g => g.Count() > 1);
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

	public SpecialData? Special(string id) => Specials.FirstOrDefault(s => s.Id == id);
	public ModifierData? Modifier(string id) => Modifiers.FirstOrDefault(m => m.Id == id);
	public IEnumerable<SpecialData> Starters => Specials.Where(s => s.Starter);

	/// <summary>
	/// Real data first; while it is missing, the TEST FIXTURES: starters from <paramref name="fixtureSpecialsDir"/>
	/// when <paramref name="movesDir"/> has no starter, drafted specials and modifiers from
	/// <paramref name="fixtureRewardsDir"/> when the data has none (movesmith delivers them after the
	/// Rules Lawyer gate, YOK-43/46).
	/// </summary>
	public static AbilityPool Load(string movesDir, string modifiersDir, string fixtureSpecialsDir, string fixtureRewardsDir)
	{
		var notes = new List<string>();
		var specials = SpecialLoader.LoadDirectory(movesDir).ToList();
		if (!specials.Any(s => s.Starter))
		{
			notes.Add($"no starter specials in {movesDir}: TEST FIXTURE starters");
			specials.AddRange(SpecialLoader.LoadDirectory(fixtureSpecialsDir).Where(s => s.Starter));
		}
		if (!specials.Any(s => !s.Starter))
		{
			notes.Add($"no drafted specials in {movesDir}: TEST FIXTURE rewards");
			specials.AddRange(SpecialLoader.LoadDirectory(fixtureRewardsDir).Where(s => !s.Starter));
		}
		var modifiers = ModifierLoader.LoadDirectory(modifiersDir).ToList();
		if (modifiers.Count == 0)
		{
			notes.Add($"no modifiers in {modifiersDir}: TEST FIXTURE modifiers");
			modifiers.AddRange(ModifierLoader.LoadDirectory(fixtureRewardsDir));
		}
		return new AbilityPool(specials, modifiers, notes);
	}

	/// <summary>Loads from the repo root (the folder holding <c>data/</c> and <c>game/</c>).</summary>
	public static AbilityPool LoadRepo(string repoRoot) => Load(
		Path.Combine(repoRoot, "data", "moves"), Path.Combine(repoRoot, "data", "modifiers"),
		Path.Combine(repoRoot, "game", "tests", "fixtures", "specials"), Path.Combine(repoRoot, "game", "tests", "fixtures", "rewards"));
}
