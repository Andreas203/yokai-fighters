using Godot;
using YokaiFighters.Audio;
using YokaiFighters.Fight;
using YokaiFighters.Sim;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-54 (V9, AM22): each stage's battle track, one for the whole fight (the tea house too: one track, not one per
/// season, designer 2026-11-12), and a check that every track the table names is in the project. No audio device is needed.
/// </summary>
public static class StageMusicTests
{
	[Test]
	public static void EachStageHasItsOwnTrack()
	{
		string grove = MusicPlayer.StageTrack("bamboo-grove");
		string shrine = MusicPlayer.StageTrack("shrine-gate");
		string swamp = MusicPlayer.StageTrack("riverbank");
		string tea = MusicPlayer.StageTrack("tea-house");
		Assert.Equal(MusicPlayer.BattleTrackPath, grove, "the grove plays the battle theme");
		Assert.True(grove != shrine && shrine != swamp && swamp != tea && grove != swamp && grove != tea && shrine != tea,
			"four stages, four tracks");
		Assert.True(tea.Contains("tanuki_spring"), $"the tea house plays the first Tanuki track throughout: {tea}");
		Assert.Equal(grove, MusicPlayer.StageTrack("placeholder"), "the placeholder stage plays the grove's theme");
		Assert.Equal(grove, MusicPlayer.StageTrack(null), "no stage yet: the grove's theme");
		Assert.Equal(grove, MusicPlayer.StageTrack("no-such-stage"), "an unknown stage: the grove's theme");
	}

	[Test]
	public static void EveryTrackNamedIsInTheProject()
	{
		foreach (string t in new[] { MusicPlayer.MenuTrackPath, MusicPlayer.FallbackBattleTrackPath, MusicPlayer.StageTrack("bamboo-grove"),
			MusicPlayer.StageTrack("shrine-gate"), MusicPlayer.StageTrack("riverbank"), MusicPlayer.StageTrack("tea-house") })
			Assert.True(ResourceLoader.Exists(t), $"{t} is imported");
	}

	[Test]
	public static void HitSounds_LightHeavyAndBlock()
	{
		HitEvent Hit(string move = "ryo-light-punch", bool blocked = false, bool counter = false, int damage = 30) => new(0, move, 10, blocked, counter, damage);
		Assert.Equal(FightScene.LightHitPath, FightScene.HitSoundFor(Hit()), "a light punch: light");
		Assert.Equal(FightScene.HeavyHitPath, FightScene.HitSoundFor(Hit("ryo-heavy-punch", damage: 80)), "a heavy punch: heavy");
		Assert.Equal(FightScene.HeavyHitPath, FightScene.HitSoundFor(Hit(counter: true)), "a counter-hit: heavy");
		Assert.Equal(FightScene.HeavyHitPath, FightScene.HitSoundFor(Hit("foxfire", damage: 90)), "80+ damage: heavy");
		Assert.Equal(FightScene.BlockHitPath, FightScene.HitSoundFor(Hit("ryo-heavy-punch", blocked: true, damage: 80)), "any blocked hit: block");
		foreach (string p in new[] { FightScene.LightHitPath, FightScene.HeavyHitPath, FightScene.BlockHitPath })
			Assert.True(ResourceLoader.Exists(p), $"{p} is imported");
	}
}
