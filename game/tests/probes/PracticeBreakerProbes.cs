using System;
using System.IO;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Flow;
using YokaiFighters.Sim;
using YokaiFighters.Ui;
using YokaiFighters.SaveGame;

namespace YokaiFighters.Tests;

/// <summary>YOK-197 probes: practice mode must never touch the save, leak into a run, desync, or change the scheme. Temp save paths only.</summary>
public static class PracticeBreakerProbes
{
	static void WithTempFile(Action<string> body)
	{
		string dir = Path.Combine(Path.GetTempPath(), "yokai-pbp-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(dir);
		try { body(Path.Combine(dir, SaveStore.FileName)); }
		finally { try { Directory.Delete(dir, recursive: true); } catch (IOException) { } }
	}

	static void WithBoot(Node runner, string saveFile, Action<GameFlow> body, uint seed = 197)
	{
		var saved = FightScene.Sources; var savedRun = FightScene.Run; var savedFoe = FightScene.Opponent;
		FightScene.Sources = FightScene.FixtureSources;
		FightScene.Run = null;
		MenuSettings.Reset();
		var flow = GD.Load<PackedScene>("res://scenes/boot.tscn").Instantiate<GameFlow>();
		flow.QuitAction = () => { };
		flow.FixedRunSeed = seed;
		flow.SaveFile = saveFile;
		flow.ConfigureFight = f => f.ExternalDrive = true;
		try { runner.AddChild(flow); body(flow); }
		finally
		{
			runner.RemoveChild(flow); flow.QueueFree();
			MenuSettings.Reset();
			FightScene.Sources = saved; FightScene.Run = savedRun; FightScene.Opponent = savedFoe;
		}
	}

	static void Press(Button b) => b.EmitSignal(BaseButton.SignalName.Pressed);

	static void WithPractice(Node runner, Action<FightScene> body)
	{
		string foe = FightScene.Opponent; var run = FightScene.Run;
		FightScene.Run = null;
		var scene = GD.Load<PackedScene>("res://scenes/fight.tscn").Instantiate<FightScene>();
		scene.ExternalDrive = true; scene.PracticeMode = true; scene.PauseEnabled = true;
		runner.AddChild(scene);
		try { body(scene); }
		finally { runner.RemoveChild(scene); scene.QueueFree(); FightScene.Opponent = foe; FightScene.Run = run; }
	}

	static string Slots(RunState r) =>
		string.Join(",", r.Owned.Select(o => $"{o.Slot}:{o.Special.Data.Id}:{o.Special.Level}:{o.Special.Modifier?.Id}"));

	static string[] Files(string path) => Directory.GetFiles(Path.GetDirectoryName(path)!).OrderBy(f => f).ToArray();

	// M5 / AM3: practice never writes the save. Corrupt file, quit in the middle of a KO slow-mo, pause then title, many enters.
	[Test]
	public static void CorruptSave_PracticeMidKoQuit_LeavesFileAndDirectoryUntouched(Node runner) => WithTempFile(path =>
	{
		string corrupt = "{ this is not json";
		File.WriteAllText(path, corrupt);
		var before = Files(path);
		WithBoot(runner, path, flow =>
		{
			for (int round = 0; round < 3; round++)
			{
				Press(flow.Title.PracticeButton);
				var f = flow.Practice!;
				for (int t = 0; t < 600 && f.Match.Phase == MatchPhase.Fighting; t++)
					f.Step(new FighterInput(t % 2 == 0 ? InputBits.DebugStrike : InputBits.None), FighterInput.None);
				if (round == 1) f.TryPause();
				Assert.True(f.RequestTitle() && flow.Router.Current == Screen.Title, "quit to title, round " + round);
			}
		});
		Assert.Equal(corrupt, File.ReadAllText(path), "corrupt save text unchanged");
		Assert.True(Files(path).SequenceEqual(before), "no backup, temp or other file written");
	});

	// A1, M5: a save with unlocks and a carried move still gives Ryo only his starters in practice.
	[Test]
	public static void SaveWithUnlocksAndCarryOver_PracticeKitIsOnlyTheStarters_AndFileUntouched(Node runner) => WithTempFile(path =>
	{
		var pool = FightScene.LoadAbilityPool();
		var data = SaveData.Empty;
		foreach (var s in pool.Specials) data = data.WithUnlock(s.Id);
		var carried = pool.Specials.First(s => !s.Starter).Id;
		data = data.WithCarryOver(carried).WithTanukiWin();
		Assert.True(new SaveStore(path).Save(data), "fixture save written");
		byte[] before = File.ReadAllBytes(path);
		var files = Files(path);
		WithBoot(runner, path, flow =>
		{
			Assert.True(flow.Saved.Unlocks.Count > 0, "the boot read the unlocks (probe precondition)");
			Press(flow.Title.PracticeButton);
			var f = flow.Practice!;
			var fresh = RunState.NewRun(FightScene.LoadAbilityPool());
			Assert.Equal(Slots(fresh), Slots(f.DemoRun), "A1: only the starters, whatever the save holds");
			Assert.True(!f.DemoRun.Owns(carried), "the carried move is not in Ryo's practice kit");
			Assert.True(f.Pool.Unlocked.Count == 0, "practice pool has no unlocks read from the save");
			f.RequestTitle();
		});
		Assert.True(File.ReadAllBytes(path).SequenceEqual(before) && Files(path).SequenceEqual(files), "save untouched");
	});

	// M5: nothing from practice reaches the next run. Same seed, same inputs: identical fight, health, coins, slots, scheme.
	static string RunSnapshot(GameFlow flow)
	{
		Press(flow.Title.StartButton);
		Press(flow.ControlSelect.Start);
		var s = flow.Session!;
		Assert.True(flow.ChooseNode(s.Reachable()[0]).Ok, "node entered");
		var f = flow.Fight!;
		for (int t = 0; t < 240; t++)
			f.Step(new FighterInput(t % 9 == 0 ? InputBits.DebugStrike : t % 5 < 2 ? InputBits.Right : InputBits.None), FighterInput.None);
		return $"{f.Match.StateHash():x16}|{FightScene.Opponent}|{f.Match.P1.Health}|{s.Run.Health}|{s.Run.Coins}|{Slots(s.Run)}|{flow.Router.Scheme}|{f.Match.P1.Input.Scheme}|{f.Temperament}";
	}

	[Test]
	public static void RunAfterPractice_IsIdenticalToARunThatNeverVisitedPractice(Node runner)
	{
		string clean = "", dirty = "";
		WithTempFile(p => WithBoot(runner, p, flow => clean = RunSnapshot(flow)));
		WithTempFile(p => WithBoot(runner, p, flow =>
		{
			flow.Router.ChooseScheme(ControlScheme.Kata);
			Press(flow.Title.PracticeButton);
			var f = flow.Practice!;
			f.CyclePracticeStance();
			for (int t = 0; t < 500; t++) f.Step(new FighterInput(t % 2 == 0 ? InputBits.DebugStrike : InputBits.Right), FighterInput.None);
			f.RequestTitle();
			flow.Router.ChooseScheme(ControlScheme.Kihon);
			dirty = RunSnapshot(flow);
		}));
		Assert.Equal(clean, dirty, "run state after a practice visit");
	}

	// Desync: reset in different orders against stance changes; after a reset the sim equals a fresh match.
	[Test]
	public static void ResetThenStance_EqualsStanceThenReset_AndResetEqualsFreshMatch(Node runner)
	{
		ulong fresh = 0;
		WithPractice(runner, s => fresh = s.Match.StateHash());
		ulong Play(bool stanceFirst)
		{
			ulong h = 0;
			WithPractice(runner, s =>
			{
				for (int t = 0; t < 150; t++) s.Step(new FighterInput(t % 3 == 0 ? InputBits.Right : InputBits.DebugStrike), FighterInput.None);
				if (stanceFirst) { s.CyclePracticeStance(); s.CyclePracticeStance(); s.ResetPractice(); }
				else { s.ResetPractice(); s.CyclePracticeStance(); s.CyclePracticeStance(); }
				Assert.Equal(fresh, s.Match.StateHash(), "reset hash == fresh scene hash");
				for (int t = 0; t < 600; t++)
				{
					s.Step(t % 41 == 0 ? FighterInput.Special(SpecialSlot.A) : new FighterInput(t % 7 < 3 ? InputBits.Right : InputBits.None), FighterInput.None);
					h = h * 1099511628211UL + s.Match.StateHash();
				}
			});
			return h;
		}
		Assert.Equal(Play(true), Play(false), "same end state by two orders");
	}

	// Reset on the tick a hit lands (hitstop): the match must equal a fresh one afterwards.
	[Test]
	public static void ResetDuringHitstop_LeavesNoStopOrStateBehind(Node runner)
	{
		foreach (var attack in new[] { FighterInput.Special(SpecialSlot.A), new FighterInput(InputBits.HeavyPunch) })
		{
			ulong fresh = 0, after = 0, idle = 0;
			WithPractice(runner, s => { fresh = s.Match.StateHash(); for (int t = 0; t < 30; t++) s.Step(FighterInput.None, FighterInput.None); idle = s.Match.StateHash(); });
			WithPractice(runner, s =>
			{
				bool hit = false;
				s.Match.Hit += (_, _) => hit = true;
				for (int t = 0; t < 900 && !hit; t++)
					s.Step(t < 80 && attack.Bits != InputBits.Special ? new FighterInput(InputBits.Right) : t % 40 == 0 ? attack : FighterInput.None, FighterInput.None);
				Assert.True(hit, "a hit landed (probe precondition)");
				Assert.True(s.ResetPractice(), "reset");
				Assert.Equal(fresh, s.Match.StateHash(), "reset on a hit tick == fresh match");
				for (int t = 0; t < 30; t++) s.Step(FighterInput.None, FighterInput.None);
				after = s.Match.StateHash();
			});
			Assert.Equal(idle, after, "30 idle ticks after reset == a fresh scene's (no hitstop kept)");
		}
	}

	// R7: the dummy guards away from Ryo. After Ryo ends on the other side, a held Block must face and guard him.
	[Test]
	public static void CrossUp_DummyBlockFacesRyoOnTheNewSide(Node runner) => WithPractice(runner, s =>
	{
		s.Dummy!.Stance = PracticeStance.Block;
		var m = s.Match;
		m.P1.X = m.P2.X + 30000;
		for (int t = 0; t < 30; t++) s.Step(FighterInput.None, FighterInput.None);
		Assert.Equal(Math.Sign(m.P1.X - m.P2.X), m.P2.Facing, "dummy faces Ryo");
		HitEvent? hit = null;
		m.Hit += (_, e) => { if (e.Attacker == 0) hit ??= e; };
		for (int t = 0; t < 600 && hit is null; t++)
			s.Step(t % 40 == 0 ? FighterInput.Special(SpecialSlot.A) : FighterInput.None, FighterInput.None);
		Assert.True(hit is not null, "Ryo's move reached the dummy");
		Assert.True(hit!.Value.Blocked, "the guard holds away from Ryo on the new side");
	});

	// Scheme fixed during practice: the settings route (move list's toggle) must not change it.
	[Test]
	public static void Scheme_MoveListToggleRoute_DoesNotChangeIt(Node runner) => WithTempFile(p => WithBoot(runner, p, flow =>
	{
		flow.Router.ChooseScheme(ControlScheme.Kata);
		Press(flow.Title.PracticeButton);
		var f = flow.Practice!;
		MenuSettings.SetScheme(ControlScheme.Kihon);
		Assert.Equal(ControlScheme.Kata, flow.Router.Scheme, "router scheme");
		Assert.Equal(ControlScheme.Kata, f.Match.P1.Input.Scheme, "Ryo's scheme");
	}));

	// Designer attention: F4 (E18, debug builds) flips P1's scheme in practice; the brief says no route may.
	[Test]
	// Pins the current behaviour: F4 on the overlay flips P1's scheme in practice (E18; the overlay is live in practice, YOK-197).
	// Waiting on the designer (YOK-197 PR): if practice locks the scheme, invert this to Assert.Equal(before, ...).
	public static void Scheme_F4DebugOverlay_InPractice(Node runner) => WithPractice(runner, s =>
	{
		var before = s.Match.P1.Input.Scheme;
		s.ToggleScheme(0);
		Assert.True(before != s.Match.P1.Input.Scheme, "F4 flips P1's scheme in practice (E18); designer to confirm or lock it");
		s.ToggleScheme(0);
		Assert.Equal(before, s.Match.P1.Input.Scheme, "a second F4 flips it back");
	});

	[Test]
	public static void EnterLeaveManyTimes_NoSessionNoFileNoStaticLeak(Node runner) => WithTempFile(path => WithBoot(runner, path, flow =>
	{
		for (int i = 0; i < 5; i++)
		{
			Press(flow.Title.PracticeButton);
			flow.Practice!.Step(FighterInput.None, FighterInput.None);
			flow.Practice.RequestTitle();
			Assert.True(flow.Session is null && flow.Fight is null, "no session or scene left, round " + i);
		}
		Assert.True(!File.Exists(path) && Files(path).Length == 0, "nothing written");
		Assert.True(FightScene.Run is null, "static FightScene.Run not left set by practice");
	}));
}
