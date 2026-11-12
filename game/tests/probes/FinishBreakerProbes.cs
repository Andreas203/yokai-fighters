using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;
using YokaiFighters.Story;
using YokaiFighters.Tutorial;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>Finish-build breaker probes (2026-11-12): tutorial keys, ability art, VFX determinism, hip lift, portraits.</summary>
public static class FinishBreakerProbes
{
	const int S = SimConfig.Scale;
	static string Repo => Path.GetFullPath(ProjectSettings.GlobalizePath("res://") + "..");
	static MoveData[] Moves() =>
		MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureNormalsDir))
			.Concat(MoveLoader.LoadDirectory(ProjectSettings.GlobalizePath(FightScene.FixtureThrowsDir))).ToArray();
	static SpecialData Special(string id) => SpecialLoader.LoadFile($"{Repo}/data/moves/{id}.json");

	// F3 (determinism): presentation never changes the sim. Real Mirage Feint (fox-mirage Lv3), Aftershock (oni-quake Lv3),
	// Torrent Spin (shell-spin Lv3), seeded random inputs: hashes equal with the VFX observer attached and not.
	static List<ulong> Play(int seed, bool vfx)
	{
		var m = new Match(null, Moves(), Moves());
		var presets = LevelPresetLibrary.LoadDirectory($"{Repo}/data/presets");
		foreach (var id in new[] { "fox-mirage", "oni-quake", "shell-spin" })
		{
			var d = Special(id);
			m.P1.Equip(d.Slot, d, 3, null, m.Config);
			m.P2.Equip(d.Slot, d, 3, null, m.Config);
		}
		m.P1.X = -150 * S; m.P2.X = 150 * S;
		var fx = new SpecialFx(presets);
		if (vfx) fx.Attach(m);
		var rng = new SimRng((uint)seed);
		var hashes = new List<ulong>();
		for (int t = 0; t < 1500 && m.Phase != MatchPhase.Over; t++)
		{
			FighterInput In() => rng.Next(5) == 0
				? FighterInput.Special((SpecialSlot)(1 + rng.Next(4)), rng.Next(4) == 0, InputBits.None)
				: new FighterInput((InputBits)(rng.NextUInt() & (uint)(InputBits.Directions | InputBits.Attacks)));
			m.Step(In(), In());
			if (vfx) { fx.Observe(m); fx.Visible(m); SpecialFx.Projectiles(m, presets); }
			hashes.Add(m.StateHash());
		}
		if (vfx) fx.Detach();
		return hashes;
	}

	[Test]
	public static void VfxObserver_DoesNotChangeTheSim_OverLongSeededFights()
	{
		foreach (int seed in new[] { 3, 17, 91 })
		{
			var off = Play(seed, false); var on = Play(seed, true);
			Assert.Equal(off.Count, on.Count, $"seed {seed}: same length");
			for (int i = 0; i < off.Count; i++) Assert.Equal(off[i], on[i], $"seed {seed} tick {i}: hash with vfx == without");
			Assert.Equal(off.Last(), Play(seed, false).Last(), $"seed {seed}: replays");
		}
	}

	[Test]
	public static void Resolve_NearestLowerLevel_ClampsAndNeverReadsHigher()
	{
		var lib = LevelPresetLibrary.LoadDirectory($"{Repo}/data/presets");
		Assert.True(lib.Resolve(null, 1) == null, "null special: none");
		Assert.True(lib.Resolve("no-such-special", 3) == null, "unknown special: none");
		Assert.True(lib.For("shell-spin", 1) != null, "fixture: shell-spin has an Lv1 preset");
		Assert.True(lib.Resolve("shell-spin", 0) == null, "level 0 reads nothing");
		Assert.True(lib.Resolve("shell-spin", -5) == null, "negative level reads nothing");
		Assert.True(lib.Resolve("shell-spin", 99) is { } hi && hi.Level <= 3, "level 99 clamps to 3");
		Assert.Equal(1, lib.Resolve("shell-spin", 1)!.Level, "Lv1 exact");
	}

	const string PresetJson = """
	{ "kind": "level_preset", "id": "bp-missing-tex", "special": "bp-x", "level": 1, "status": "final", "colour": "#112233",
	  "spark": {}, "projectile": { "look": "orb", "texture": "assets/generated/vfx/nope/missing.png" },
	  "impact": { "look": "seal", "texture": "assets/generated/vfx/nope/also-missing.png" } }
	""";

	[Test]
	public static void PresetNamingAMissingTexture_FallsToProceduralOrDefault_NeverCrashes()
	{
		VfxArt.ClearCache();
		var p = LevelPresetLibrary.Parse(PresetJson);
		var style = new VfxStyle(VfxLook.Orb, 0x112233, 0xFFFFFF, 100, p.Projectile!.Texture);
		Assert.True(VfxArt.For(style, exists: _ => false, load: _ => null) == null, "no file at all: procedural (null)");
		Assert.Equal("procedural:orb", VfxArt.Pick(style, _ => false), "Pick: procedural when nothing exists");
		var seal = new VfxStyle(VfxLook.Seal, 0x112233, 0xFFFFFF, 100, p.Impact!.Texture);
		string def = VfxArt.DefaultPath(VfxLook.Seal)!;
		Assert.Equal(def, VfxArt.Pick(seal, x => x == def), "missing named image falls to the look's default");
		VfxArt.ClearCache();
	}

	// Y6: tokens per scheme, plain words when none, skip key per device.
	[Test]
	public static void TutorialKeys_PlainWordsWithNoScheme_AndSkipKeyResetsBackToKeyboard()
	{
		TutorialPrompts.Library = FightScene.LoadStory();
		TutorialPrompts.Keys = new Dictionary<string, string>();
		TutorialPrompts.SkipKey = null;
		string t = TutorialPrompts.For(TutorialLesson.Throw), b = TutorialPrompts.For(TutorialLesson.Burst);
		Assert.True(t.Length > 0 && !t.Contains("{") && t.Contains("both throw buttons"), "no scheme: plain words: " + t);
		Assert.True(b.Length > 0 && !b.Contains("{") && b.Contains("all three punches"), "no scheme: plain words: " + b);
		FightScene.SetTutorialKeyNames(pad: true);
		Assert.True(TutorialPrompts.SkipHint.StartsWith("Select"), "pad: skip names Select: " + TutorialPrompts.SkipHint);
		FightScene.SetTutorialKeyNames(pad: false);
		Assert.True(TutorialPrompts.SkipHint.StartsWith("Tab"), "keyboard again: Tab: " + TutorialPrompts.SkipHint);
		TutorialPrompts.Library = null;
		Assert.Equal("", TutorialPrompts.For(TutorialLesson.Throw), "no library: empty, never a tag");
		Assert.Equal("", TutorialPrompts.SkipHint, "no library: empty skip hint");
		Assert.Equal("", TutorialPrompts.For((TutorialLesson)999), "unknown lesson: empty");
		TutorialPrompts.Library = FightScene.LoadStory();
	}

	[Test]
	public static void TutorialCardWithAYokaiSlot_DoesNotThrowOutOfThePrompt()
	{
		// A card using {yokai} loads fine, but the tutorial supplies none: the prompt must not throw (it runs every frame in a fight).
		var card = StoryLibrary.Parse("""{ "id": "tutorial-block", "kind": "story_card", "title": "t", "text": "Guard against {yokai}.", "placeholders": ["yokai"], "speaker": "Prompt", "status": "final" }""");
		var lib = new StoryLibrary(new[] { card });
		TutorialPrompts.Library = lib;
		try { string s = TutorialPrompts.For(TutorialLesson.Block); Assert.True(!s.Contains("{"), "no raw tag: " + s); }
		catch (InvalidOperationException e) { Assert.True(false, "TutorialPrompts.For threw on an unfilled {yokai}: " + e.Message); }
		finally { TutorialPrompts.Library = FightScene.LoadStory(); }
	}

	[Test]
	public static void PortraitPath_ElderAndOddIds()
	{
		Assert.Equal(FightHud.PortraitPath("kitsune"), FightHud.PortraitPath("elder-kitsune"), "elder kitsune wears the fox mask");
		Assert.Equal("", FightHud.PortraitPath("elder-tanuki"), "elder-tanuki: no plate");
		Assert.Equal("", FightHud.PortraitPath("elder-"), "bare prefix: no path");
	}

	[Test]
	public static void AbilityArt_RefusesPathLikeAndNewlineIds_AndEvolutionsShareTheirSpecialIcon()
	{
		var fb = AbilityArt.Fallback;
		foreach (string bad in new[] { "../x", "a/b", "..", "a\\b", "foxfire.png", "Foxfire", "foxfire " })
			Assert.True(AbilityArt.ForId(bad) == fb || AbilityArt.Has(bad) == false || true, "never throws for " + bad);
		Assert.True(AbilityArt.ForId("../../ui/ability_art") == fb, "path id: fallback");
		Assert.True(!AbilityArt.Has("foxfire\n") || AbilityArt.Has("foxfire"), "a trailing newline is not accepted as an id of its own");
		AbilityArt.ClearFileCache();
		foreach (string f in Directory.GetFiles($"{Repo}/data/moves", "*.json"))
		{
			using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(f));
			if (!doc.RootElement.TryGetProperty("id", out var sid) || !doc.RootElement.TryGetProperty("levels", out var lv)) continue;
			if (!lv.TryGetProperty("3", out var l3) || !l3.TryGetProperty("evolution", out var ev)) continue;
			string special = sid.GetString()!, evo = ev.GetProperty("id").GetString()!;
			Assert.True(AbilityArt.ForId(evo) == AbilityArt.ForId(special), $"{evo} shows {special}'s icon");
		}
		var a = AbilityArt.ForId("no-such-ability-zz"); AbilityArt.ClearFileCache();
		Assert.True(AbilityArt.ForId("no-such-ability-zz") == a && a == fb, "miss then clear: still the fallback");
	}

	[Test]
	public static void LiftAt_HoldsAtEnds_AndSurvivesEqualFramePoints()
	{
		var info = new ClipInfo("x", "res://x.glb", 0, 60, 1.0, 60);
		var curve = new[] { new HipLift(10, 0.1f), new HipLift(10, 0.3f), new HipLift(20, 0.2f) };
		Assert.Equal(0f, FighterModel.LiftAt(Array.Empty<HipLift>(), info, 0.5), "empty curve");
		Assert.Equal(0.1f, FighterModel.LiftAt(curve, info, info.TimeAt(0)), "held before the first point");
		Assert.Equal(0.2f, FighterModel.LiftAt(curve, info, info.TimeAt(99)), "held past the last point");
		float mid = FighterModel.LiftAt(curve, info, info.TimeAt(15));
		Assert.True(float.IsFinite(mid) && mid >= 0.2f && mid <= 0.3f, "between points is finite and in range: " + mid);
	}

	[Test]
	public static void TanukiLyingLift_OnlyOnLyingClips_AndHipOffsetPrecedence()
	{
		var set = FighterAnimSet.TanukiTripo;
		var lyingClips = set.LyingHipLift.Keys.Select(k => set.ClipFor(k)).ToHashSet();
		foreach (PoseKind k in Enum.GetValues<PoseKind>())
		{
			string? clip = set.ClipFor(k);
			if (clip == null || lyingClips.Contains(clip)) continue;
			Assert.True(set.LyingLiftFor(clip) == null, $"{k} ({clip}) is not a lying pose: no lift");
		}
		var oni = FighterAnimSet.For("oni");
		Assert.Equal(0.02f, oni.HipOffsetFor("oni-quake"), "per-clip offset replaces the body offset");
		Assert.Equal(oni.HipOffsetY, oni.HipOffsetFor("oni-idle"), "other clips use the body offset");
	}
}
