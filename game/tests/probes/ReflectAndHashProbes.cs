using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>YOK-103 round 5 probes: holes the reflect fix could open, and the no-modifier hash staying as on main.</summary>
public static class ReflectAndHashProbes
{
	const int S = SimConfig.Scale;
	static string Repo => Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "..");
	static ModifierData Mod(string id) => ModifierLoader.LoadFile($"{Repo}/data/modifiers/{id}.json");
	static SpecialData[] Fixtures() => SpecialLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureSpecialsDir));
	static MoveData[] Moves() =>
		MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureNormalsDir))
			.Concat(MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureThrowsDir))).ToArray();

	const string QuakeJson = """
	{
	  "kind": "special", "id": "test-quake",
	  "name": "TEST FIXTURE: quake", "source": "test-quaker", "starter": false, "rarity": "rare", "locked": true, "clip": "test-quake",
	  "rules": ["A5"],
	  "input": { "kata": { "slot": "D", "motion": "22" }, "kihon": { "slot": "D", "direction": "down" } },
	  "card": { "plain": "TEST FIXTURE", "frames": "9 / 3 / 20" },
	  "levels": {
	    "1": { "frame_data": {
	      "startup": 9, "active": 3, "recovery": 20, "damage": 50, "hitstun": 20, "blockstun": 15,
	      "hitboxes": [ { "frames": [10, 12], "rect": { "x": 0, "y": 0, "w": 300, "h": 110 } } ],
	      "hurtboxes": [ { "frames": [1, 32], "rect": { "x": -45, "y": 0, "w": 90, "h": 320 } } ],
	      "properties": { "low": true } } },
	    "2": { "status": "proposed", "tuning": { "target": "recovery", "op": "add", "value": -2 } },
	    "3": { "evolution": { "id": "test-aftershock", "name": "TEST FIXTURE", "preset": "test-aftershock",
	      "changes": [ { "target": "properties.aftershock_delay", "op": "set", "value": 30 } ] } }
	  }
	}
	""";

	static readonly ElderTwist Reflect = new("t-reflect", "nine-tailed-kitsune", TwistEffect.ReflectRepeatedSpecial, 2, 0, "twist.t", null, null, null);

	static Match FightWith(string[] mods, bool angle, bool quake)
	{
		var m = new Match(null, Moves(), Moves());
		foreach (var f in m.Fighters) FightScene.EquipStarters(f, Fixtures());
		var q = SpecialLoader.Parse(QuakeJson);
		if (quake) m.P1.Equip(SpecialSlot.D, q, 3, null, m.Config);
		var slots = new List<SpecialSlot> { SpecialSlot.A, SpecialSlot.B };
		if (quake) slots.Add(SpecialSlot.D);
		for (int i = 0; i < mods.Length; i++)
		{
			var mod = Mod(mods[i]);
			var slot = slots[i % slots.Count];
			if (m.P1.Specials[(int)slot] is { } s && mod.AppliesTo(s.Data)) m.P1.AttachModifier(slot, mod, m.Config);
		}
		if (angle) { var a = Mod("tricksters-angle"); if (a.AppliesTo(m.P1.Specials[(int)SpecialSlot.A]!.Data)) m.P1.AttachModifier(SpecialSlot.A, a, m.Config); }
		m.P2.Twist = Reflect;
		m.P1.X = -150 * S; m.P2.X = 150 * S;
		return m;
	}

	static (ulong hash, int maxElderGain, int minRyoShove) Play(Match m, int seed, bool upBias)
	{
		var rng = new SimRng((uint)seed);
		ulong h = 0; int gain = 0; int prev = m.P2.Health;
		for (int t = 0; t < 3000 && m.Phase != MatchPhase.Over; t++)
		{
			FighterInput In(bool p1) => rng.Next(5) == 0
				? FighterInput.Special((SpecialSlot)(1 + rng.Next(p1 ? 3 : 2)), false, upBias || rng.Next(2) == 0 ? InputBits.Up : InputBits.None)
				: new FighterInput((InputBits)(rng.NextUInt() & (uint)(InputBits.Directions | InputBits.Attacks)));
			m.Step(In(true), In(false));
			gain = Math.Max(gain, m.P2.Health - prev);
			prev = m.P2.Health;
			h = h * 31 + m.StateHash();
		}
		return (h, gain, 0);
	}

	// T4: the elder holds no healing of her own; with Ryo carrying every modifier (Cucumber Gift among them), reflect and
	// aftershock must never raise her health, and the fight must be deterministic.
	[Test]
	public static void ElderNeverGainsHealthAndFightIsDeterministicUnderReflectWithEveryModifier()
	{
		var all = new[] { "cucumber-gift", "river-pull", "tricksters-angle", "brute-recovery", "club-momentum", "iron-will", "onis-hide", "illusion-cloak" };
		for (int seed = 1; seed <= 12; seed++)
		{
			var mods = all.OrderBy(x => (x.GetHashCode() * 0 + (x[0] * seed) % 7)).ToArray();
			var a = Play(FightWith(mods, true, true), seed, seed % 2 == 0);
			var b = Play(FightWith(mods, true, true), seed, seed % 2 == 0);
			Assert.Equal(a.hash, b.hash, $"seed {seed}: same choices, same hashes");
			Assert.True(a.maxElderGain <= 0, $"seed {seed}: the elder gained health ({a.maxElderGain}) in one tick");
		}
	}

	// T4 + A5: Cucumber Gift on the Lv 3 quake, reflected quake and its aftershock, must not heal the elder.
	[Test]
	public static void ReflectedQuakeAndAftershockDoNotHealTheElder()
	{
		var m = FightWith(new[] { "cucumber-gift", "cucumber-gift", "cucumber-gift" }, false, true);
		m.P2.Health = 500;
		int prev = m.P2.Health, worst = 0;
		for (int use = 0; use < 3; use++)
		{
			for (int t = 0; t < 140; t++)
			{
				m.P1.X = -60 * S; m.P2.X = 60 * S;
				m.Step(t == 0 ? FighterInput.Special(SpecialSlot.D) : FighterInput.None, FighterInput.None);
				worst = Math.Max(worst, m.P2.Health - prev); prev = m.P2.Health;
			}
		}
		Assert.True(worst <= 0, $"elder gained {worst} in a tick");
	}

	// Reflected Trickster's Angle wave: determinism and no exception while it climbs back (rule silent on the angle).
	[Test]
	public static void ReflectedAngleWaveIsDeterministic()
	{
		ulong Run()
		{
			var m = FightWith(new[] { "tricksters-angle" }, true, false);
			ulong h = 0;
			for (int use = 0; use < 3; use++)
				for (int t = 0; t < 140; t++)
				{
					m.Step(t == 0 ? FighterInput.Special(SpecialSlot.A, false, InputBits.Up) : FighterInput.None, FighterInput.None);
					h = h * 31 + m.StateHash();
				}
			return h;
		}
		Assert.Equal(Run(), Run(), "reflected angled wave replays");
	}

	// Rule (decision: a fight with no modifier and no twist hashes as before). Digest recorded from origin/main (see report).
	public const ulong MainDigest = 6359445485853194678UL;
	[Test]
	public static void ModifierFreeTwistFreeFightHashesAsOnMain()
	{
		ulong total = 0;
		for (int seed = 1; seed <= 6; seed++)
		{
			var m = new Match(null, Moves(), Moves());
			foreach (var f in m.Fighters) FightScene.EquipStarters(f, Fixtures());
			m.P1.X = -100 * S; m.P2.X = 100 * S;
			var rng = new SimRng((uint)(seed * 77));
			for (int t = 0; t < 2500 && m.Phase != MatchPhase.Over; t++)
			{
				FighterInput In() => rng.Next(6) == 0
					? FighterInput.Special((SpecialSlot)(1 + rng.Next(2)), rng.Next(2) == 0, InputBits.None)
					: new FighterInput((InputBits)(rng.NextUInt() & (uint)(InputBits.Directions | InputBits.Attacks)));
				m.Step(In(), In());
				total = total * 1000003 + m.StateHash();
			}
		}
		GD.Print($"HASHDIGEST {total}");
		Assert.Equal(MainDigest, total, "digest differs from main's (HASHDIGEST in the log)");
	}
}
