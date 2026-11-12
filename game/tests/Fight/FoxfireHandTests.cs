using System;
using System.Collections.Generic;
using Godot;
using YokaiFighters.Dev;
using YokaiFighters.Fight;
using YokaiFighters.Flow;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>
/// E15 (AM26) end to end: a drafted Foxfire, set on the run through the dev run-state path and fought through a
/// dev Jump (GameFlow, the real data, the fight scene's own presenter and model), opens the Tripo Ryo's hand while it
/// is cast and closes it again after, at every level the dev menu can set; the Tanuki's copy of it does the same on
/// his body (T0). The unit tests call <see cref="FighterModel.Apply"/> with hand-made samples; this one never does.
/// </summary>
public static class FoxfireHandTests
{
	const string Fox = "foxfire";

	/// <summary>A dev run state: the default run (starters) with <see cref="Fox"/> drafted to <c>level</c>.</summary>
	sealed class FoxRun(int level) : IDevRunStateSource
	{
		public RunState Build(AbilityPool pool)
		{
			var run = RunState.NewRun(pool);
			var fox = pool.Special(Fox) ?? throw new AssertFailed("foxfire is in the real ability pool");
			for (int i = 0; i < level; i++) run.Equip(fox); // A3: drafting it again levels it up
			return run;
		}
		public string Describe => $"foxfire Lv {level}";
	}

	static void WithBoot(Node runner, Action<GameFlow> body)
	{
		var savedRun = FightScene.Run;
		var savedFoe = FightScene.Opponent;
		bool savedTripo = FighterAnimSet.TripoRyo;
		DevMode.Configure(new DevModeState(true, true));
		FighterAnimSet.TripoRyo = true;
		FightScene.Run = null;
		MenuSettings.Reset();
		var flow = GD.Load<PackedScene>("res://scenes/boot.tscn").Instantiate<GameFlow>();
		flow.QuitAction = () => { };
		flow.FixedRunSeed = 91;
		flow.SaveFile = null;
		flow.ConfigureFight = f => { f.ExternalDrive = true; f.HeldBitsSource = () => InputBits.None; };
		var stage = new SubViewport { Size = new Vector2I(640, 360), Name = "FoxfireHandStage" };
		runner.AddChild(stage);
		try { stage.AddChild(flow); body(flow); }
		finally
		{
			runner.RemoveChild(stage);
			stage.QueueFree();
			MenuSettings.Reset();
			DevMode.ResetToLaunch();
			DevRunStates.Reset();
			FighterAnimSet.TripoRyo = savedTripo;
			FightScene.Run = savedRun;
			FightScene.Opponent = savedFoe;
		}
	}

	/// <summary>Casts <paramref name="slot"/> for player <paramref name="who"/> and returns the hand weight on every tick of the
	/// cast (the fight scene's own sample and model, after its render), stepping until the caster is back in idle.</summary>
	static List<(int MoveFrame, string? Clip, float Fist)> Cast(FightScene f, int who, SpecialSlot slot, string label)
	{
		var model = f.ModelOf(who) ?? throw new AssertFailed(label + ": the fight built a model");
		Assert.True(model.HasFist, $"{label}: the {model.AnimSet.ModelPath} body has the fist blend shape");
		var me = f.Match.Fighters[who];
		var cast = FighterInput.Special(slot);
		var log = new List<(int, string?, float)>();
		for (int t = 0; t < 30 && me.State != FighterState.Idle; t++) f.Step(FighterInput.None, FighterInput.None);
		f.Step(who == 0 ? cast : FighterInput.None, who == 1 ? cast : FighterInput.None);
		Assert.True(me.State == FighterState.Attack && me.CurrentMove?.Id == Fox, $"{label}: Foxfire started ({me.State} {me.CurrentMove?.Id})");
		for (int t = 0; t < 120 && me.State == FighterState.Attack; t++)
		{
			var s = f.PresenterOf(who)!.Sample;
			log.Add((me.MoveFrame, s.Clip, model.FistValue));
			f.Step(FighterInput.None, FighterInput.None);
		}
		for (int t = 0; t < 20; t++) f.Step(FighterInput.None, FighterInput.None);
		Assert.Equal(1f, model.FistValue, $"{label}: the fist closes again once the cast has blended out");
		return log;
	}

	static void CheckOpen(List<(int MoveFrame, string? Clip, float Fist)> log, string label)
	{
		Assert.True(log.Count > 20, $"{label}: a whole cast was logged ({log.Count} ticks)");
		int open = 0;
		foreach (var (frame, clip, fist) in log)
		{
			// The cast's clip is the move's, mapped by the body's anim set; its hand is data (clip field "hand").
			var info = FightScene.Catalog[clip];
			Assert.True(info is { Hand: ClipHand.Open }, $"{label} frame {frame}: plays {clip} with an open hand in its data");
			if (fist == 0f) open++;
			else Assert.True(fist < 1f, $"{label} frame {frame}: hand weight {fist} is closing only during the blend in");
		}
		Assert.True(open >= log.Count - FighterPresenter.DefaultBlend, $"{label}: the hand is fully open for the cast ({open} of {log.Count} ticks)");
	}

	[Test]
	public static void DraftedFoxfire_OpensRyosHand_AtEveryLevel_ThroughADevJumpFight(Node runner) => WithBoot(runner, flow =>
	{
		var pool = FightScene.LoadAbilityPool();
		var fox = pool.Special(Fox)!;
		for (int lv = 1; lv <= Math.Min(3, fox.MaxLevel); lv++)
		{
			DevRunStates.Source = new FoxRun(lv);
			var setup = new DevFightSetup("kitsune", "patient");
			var jump = DevJump.BuildFight(setup, pool);
			Assert.True(flow.StartDevRun(jump.Session, jump.NodeId, setup.Scheme).Ok, $"Lv {lv}: the jump fight starts");
			var f = flow.Fight!;
			if (f.P2Ai != null) f.ToggleP2Ai();
			Assert.Equal(lv, f.Match.P1.Specials[(int)fox.Slot]?.Level ?? 0, $"Lv {lv}: Ryo fights with it");
			CheckOpen(Cast(f, 0, fox.Slot, $"Ryo Lv {lv}"), $"Ryo Lv {lv}");
			flow.ReturnToTitle();
		}
	});

	[Test]
	public static void TanukisCopiedFoxfire_OpensTheHandOnRyosBody(Node runner) => WithBoot(runner, flow =>
	{
		var pool = FightScene.LoadAbilityPool();
		var fox = pool.Special(Fox)!;
		for (int lv = 2; lv <= Math.Min(3, fox.MaxLevel); lv++) // Lv 1 is never the copy over the starters (T3 keeps the lower slot)
		{
			DevRunStates.Source = new FoxRun(lv);
			var setup = new DevFightSetup(DevJump.TanukiId, CopySpecial: Fox, CopyLevel: lv);
			var jump = DevJump.BuildFight(setup, pool);
			Assert.True(flow.StartDevRun(jump.Session, jump.NodeId, setup.Scheme).Ok, $"Lv {lv}: the Tanuki fight starts");
			var f = flow.Fight!;
			if (f.P2Ai != null) f.ToggleP2Ai();
			Assert.True(f.Match.P2.Specials[(int)fox.Slot]?.Data.Id == Fox, $"Lv {lv}: the Tanuki copied Foxfire (T2)");
			CheckOpen(Cast(f, 1, fox.Slot, $"Tanuki Lv {lv}"), $"Tanuki Lv {lv}");
			flow.ReturnToTitle();
		}
	});
}
