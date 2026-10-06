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
	/// Real data first; while it is missing, the TEST FIXTURES (movesmith delivers real files after the Rules
	/// Lawyer gate). YOK-56: starters are Ryo's kit (<see cref="Kit.LoadSpecials"/>, A1), with its own fixture
	/// fallback. Drafted specials are every non-starter special in <paramref name="movesDir"/> whatever its
	/// source (the draft filters by the beaten yokai, A8; the Tanuki copy rule sees all of them), and modifiers
	/// every file in <paramref name="modifiersDir"/>; the fixtures in <paramref name="fixtureRewardsDir"/> fill in
	/// per source yokai, only for a yokai the data has none of.
	/// </summary>
	public static AbilityPool Load(string movesDir, string modifiersDir, string fixtureSpecialsDir, string fixtureRewardsDir) =>
		Load(new KitSources(movesDir, "", "", "", fixtureSpecialsDir, fixtureRewardsDir), modifiersDir);

	public static AbilityPool Load(KitSources src, string modifiersDir)
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
		return new AbilityPool(specials, modifiers, notes);
	}

	/// <summary>Loads from the repo root (the folder holding <c>data/</c> and <c>game/</c>).</summary>
	public static AbilityPool LoadRepo(string repoRoot) =>
		Load(KitSources.Repo(repoRoot), Path.Combine(repoRoot, "data", "modifiers"));

	/// <summary>The TEST FIXTURE pool only: tests that must not depend on what is in <c>data/</c>.</summary>
	public static AbilityPool LoadFixtures(string repoRoot) =>
		Load(KitSources.Repo(repoRoot).FixturesOnly(), "");
}
