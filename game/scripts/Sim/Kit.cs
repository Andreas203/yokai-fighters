using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace YokaiFighters.Sim;

/// <summary>
/// YOK-56: where kits load from. <see cref="MovesDir"/> is the real content (<c>data/moves/</c>); the rest are the
/// labelled TEST FIXTURE folders used per fighter and per move kind while the real content has none of that kind.
/// </summary>
public sealed record KitSources(
	string MovesDir,
	string FixtureNormalsDir,
	string FixtureAirNormalsDir,
	string FixtureThrowsDir,
	string FixtureSpecialsDir,
	string FixtureRewardsDir)
{
	/// <summary>The repo layout: the folder holding <c>data/</c> and <c>game/</c>.</summary>
	public static KitSources Repo(string repoRoot)
	{
		string fx = Path.Combine(repoRoot, "game", "tests", "fixtures");
		return new(Path.Combine(repoRoot, "data", "moves"),
			Path.Combine(fx, "ryo-normals"), Path.Combine(fx, "ryo-air-normals"), Path.Combine(fx, "throws"),
			Path.Combine(fx, "specials"), Path.Combine(fx, "rewards"));
	}

	/// <summary>Same fixtures, no real content: tests that must not depend on what is in <c>data/</c>.</summary>
	public KitSources FixturesOnly() => this with { MovesDir = "" };
}

/// <summary>
/// YOK-56: one fighter's own kit. A file belongs to the fighter named by its <c>source</c> (required on kinds
/// normal, throw and special). Ryo = his normals and throw + his starters (A1: <c>source</c> "ryo",
/// <c>starter</c> true); a yokai = its normals and throw + every special it sources (Kitsune: Foxfire).
/// Drafted specials are not part of a kit: they come from <see cref="AbilityPool"/> through the run.
/// Fallback is per fighter and per move kind (ground normals, air normals, throw, specials): while the fighter
/// has no real file of a kind, the TEST FIXTURES of that kind stand in, the fighter's own first and, for moves
/// only, Ryo's fixture moves when the fighter has none (a yokai never borrows Ryo's specials).
/// </summary>
public sealed class Kit
{
	public const string Ryo = "ryo";

	public string Fighter { get; }
	/// <summary>Ground normals, throw, air normals (the pre-YOK-56 fixture order), each sorted by file name.</summary>
	public MoveData[] Moves { get; }
	/// <summary>Specials equipped at fight start, each into its own slot at Lv 1.</summary>
	public SpecialData[] Specials { get; }
	/// <summary>Which kinds fell back to TEST FIXTURES (for the log).</summary>
	public IReadOnlyList<string> Notes { get; }

	private Kit(string fighter, MoveData[] moves, SpecialData[] specials, List<string> notes)
	{
		Fighter = fighter; Moves = moves; Specials = specials; Notes = notes;
	}

	private enum MoveKind { Ground, Air, Throw, Other }

	private static MoveKind KindOf(MoveData m) =>
		m.IsThrow ? MoveKind.Throw : m.IsNormal ? (m.Air ? MoveKind.Air : MoveKind.Ground) : MoveKind.Other;

	public static Kit Load(string fighter, KitSources src)
	{
		var notes = new List<string>();
		var real = MoveLoader.LoadDirectory(src.MovesDir, includeSpecials: false).Where(m => m.Source == fighter).ToArray();
		var moves = new List<MoveData>(real.Where(m => KindOf(m) == MoveKind.Other));
		foreach (var (kind, dir, label) in new[]
		{
			(MoveKind.Ground, src.FixtureNormalsDir, "normals"),
			(MoveKind.Throw, src.FixtureThrowsDir, "throw"),
			(MoveKind.Air, src.FixtureAirNormalsDir, "air normals"),
		})
		{
			var mine = real.Where(m => KindOf(m) == kind).ToArray();
			if (mine.Length > 0) { moves.AddRange(mine); continue; }
			var fixtures = MoveLoader.LoadDirectory(dir).Where(m => KindOf(m) == kind).ToArray();
			var own = fixtures.Where(m => m.Source == fighter).ToArray();
			var standIn = fixtures.Where(m => m.Source == Ryo).ToArray();
			moves.AddRange(own.Length > 0 ? own : standIn);
			notes.Add(own.Length > 0 || fighter == Ryo
				? $"{fighter}: no {label} in data, TEST FIXTURE {label}"
				: $"{fighter}: no {label} in data, Ryo's TEST FIXTURE {label} stand in");
		}
		return new Kit(fighter, moves.ToArray(), LoadSpecials(fighter, src, notes), notes);
	}

	/// <summary>
	/// The fighter's own specials (Ryo: starters only, A1), or the fighter's TEST FIXTURE specials while
	/// <see cref="KitSources.MovesDir"/> has none. Also feeds <see cref="AbilityPool"/>'s starters.
	/// </summary>
	public static SpecialData[] LoadSpecials(string fighter, KitSources src, List<string>? notes = null)
	{
		bool Owns(SpecialData s) => s.Source == fighter && (fighter != Ryo || s.Starter);
		var real = SpecialLoader.LoadDirectory(src.MovesDir).Where(Owns).ToArray();
		if (real.Length > 0) return real;
		notes?.Add($"{fighter}: no specials in data, TEST FIXTURE specials");
		return SpecialLoader.LoadDirectory(src.FixtureSpecialsDir).Concat(SpecialLoader.LoadDirectory(src.FixtureRewardsDir))
			.Where(Owns).ToArray();
	}

	/// <summary>A1: each special into its own slot at Lv 1 (first file wins a slot).</summary>
	public void Equip(Fighter f) => EquipSpecials(f, Specials);

	public static void EquipSpecials(Fighter f, IEnumerable<SpecialData> specials)
	{
		foreach (var s in specials)
			if (f.Specials[(int)s.Slot] is null) f.Equip(s.Slot, s, 1);
	}
}
