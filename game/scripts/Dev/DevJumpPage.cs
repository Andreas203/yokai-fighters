using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Flow;
using YokaiFighters.Sim;
using YokaiFighters.Story;
using YokaiFighters.Ui;

namespace YokaiFighters.Dev;

/// <summary>The reward-draft jump's setup: whose draft, seeded, and the rarity spread (A7-A9).</summary>
public sealed record DevDraftSetup(string Yokai = "kitsune", bool Rares = false, uint Seed = DevJump.DefaultSeed);

/// <summary>
/// YOK-125: the dev menu's Jump page. Fight setup (opponent, temperament, elder and its twist, stage, scheme, the
/// Tanuki's copy and its level), the reward draft's setup, then one button per jump target; targets not on main yet
/// are greyed with the reason. Every jump goes through <see cref="DevContext.TryChangeRun"/> (refused mid-fight) and
/// builds its run state from <see cref="DevRunStates.Source"/>. Fights and rest stops run through the normal flow
/// (<see cref="GameFlow.StartDevRun"/>); the other screens open in the dev viewer (<see cref="GameFlow.OpenDevScreen"/>).
/// </summary>
public partial class DevJumpPage : VBoxContainer
{
	public const string ReasonNoFlow = "No game flow here (a scene opened on its own): jumps need the boot scene.";

	public DevContext Context { get; }
	public DevFightSetup Fight { get; set; } = new();
	public DevDraftSetup Draft { get; set; } = new();
	/// <summary>The last jump's result line (shown on the page).</summary>
	public string Status { get; private set; } = "";
	public IReadOnlyList<DevJumpTarget> Targets { get; }
	public IReadOnlyDictionary<string, Button> TargetButtons => _buttons;

	private readonly Dictionary<string, Button> _buttons = new();
	private readonly List<Control> _focus = new();
	private readonly AbilityPool _pool;
	private readonly IReadOnlyList<DevOpponent> _opponents;
	private Label _status = null!, _copyLine = null!, _twistLine = null!;
	private OptionButton _opponent = null!, _temperament = null!, _stage = null!, _scheme = null!, _copy = null!, _copyLevel = null!;
	private OptionButton _draftYokai = null!, _rarity = null!;
	private CheckButton _elder = null!;
	private SpinBox _seed = null!;

	public DevJumpPage(DevContext ctx)
	{
		Context = ctx;
		Name = "DevJumpPage";
		AddThemeConstantOverride("separation", 8);
		_pool = FightScene.LoadAbilityPool();
		_opponents = DevJump.Opponents();
		Targets = DevJump.Targets();
		Fight = Fight with { Temperament = DevJump.Temperaments(DevJump.PlaysAsOf(Fight)).FirstOrDefault() };
		Draft = Draft with { Yokai = DevJump.DraftYokai(_pool).FirstOrDefault() ?? "kitsune" };
		Build();
		Ready += () => MenuInput.WrapVertical(_focus.Where(c => c.FocusMode != FocusModeEnum.None).ToArray());
	}

	// ---- layout ----

	private void Build()
	{
		AddChild(DevMenu.PageLabel("Jump to any fight or screen", 28));
		AddChild(DevMenu.PageLabel(DevRunStates.Source.Describe, 18, "RunStateLine"));
		if (Refusal() is { } r) AddChild(DevMenu.PageLabel(r, 20, "Refusal"));

		var grid = new GridContainer { Name = "FightSetup", Columns = 2 };
		grid.AddThemeConstantOverride("h_separation", 16);
		grid.AddThemeConstantOverride("v_separation", 4);
		_opponent = Options(grid, "Opponent", _opponents.Select(o => (o.Label, o.Built)));
		_opponent.Selected = _opponents.ToList().FindIndex(o => o.Yokai == Fight.Opponent);
		_opponent.ItemSelected += i => { Fight = Fight with { Opponent = _opponents[(int)i].Yokai, Elder = false }; RefreshFight(); };
		_temperament = Options(grid, "Temperament", Array.Empty<(string, bool)>());
		_temperament.ItemSelected += i => Fight = Fight with { Temperament = _temperament.GetItemText((int)i) };
		_elder = new CheckButton { Name = "Elder", Text = "Elder" };
		_elder.Toggled += on => { Fight = Fight with { Elder = on }; RefreshFight(); };
		grid.AddChild(DevMenu.PageLabel("Elder / twist", 20));
		var elderRow = new HBoxContainer();
		elderRow.AddChild(_elder);
		elderRow.AddChild(_twistLine = DevMenu.PageLabel("", 18, "TwistLine"));
		grid.AddChild(elderRow);
		_focus.Add(_elder);
		_stage = Options(grid, "Stage", new[] { ("Bamboo grove", true), ("Placeholder stage", true), ("Level presets (" + DevJump.NotBuiltYet + ": data/presets)", false) });
		_stage.ItemSelected += i => Fight = Fight with { Stage = (DevStage)(int)i };
		_scheme = Options(grid, "Control scheme", new[] { ("Kihon", true), ("Kata", true) });
		_scheme.ItemSelected += i => Fight = Fight with { Scheme = i == 1 ? ControlScheme.Kata : ControlScheme.Kihon };
		var specials = _pool.Specials.ToArray();
		_copy = Options(grid, "Tanuki copies", new[] { ("Ryo's run state (T2/T3)", true) }.Concat(specials.Select(s => ($"{s.Name} (slot {s.Slot})", true))));
		_copy.ItemSelected += i => { Fight = Fight with { CopySpecial = i == 0 ? null : specials[i - 1].Id, CopyLevel = 1 }; RefreshFight(); };
		_copyLevel = Options(grid, "Copy level", Array.Empty<(string, bool)>());
		_copyLevel.ItemSelected += i => { Fight = Fight with { CopyLevel = (int)i + 1 }; RefreshFight(); };
		AddChild(grid);
		AddChild(_copyLine = DevMenu.PageLabel("", 18, "CopyLine"));

		var draftGrid = new GridContainer { Name = "DraftSetup", Columns = 2 };
		draftGrid.AddThemeConstantOverride("h_separation", 16);
		var yokai = DevJump.DraftYokai(_pool);
		_draftYokai = Options(draftGrid, "Draft from", yokai.Select(y => (StoryLibrary.DisplayName(y), true)));
		_draftYokai.ItemSelected += i => Draft = Draft with { Yokai = yokai[(int)i] };
		_rarity = Options(draftGrid, "Rarity spread", new[] { ("Commons only (a duel's draft)", true), ("Commons + rares (an elder's draft, A9)", true) });
		_rarity.ItemSelected += i => Draft = Draft with { Rares = i == 1 };
		draftGrid.AddChild(DevMenu.PageLabel("Seed (draft and fight)", 20));
		_seed = new SpinBox { Name = "Seed", MinValue = 0, MaxValue = int.MaxValue, Step = 1, Value = Draft.Seed };
		_seed.ValueChanged += v => { Draft = Draft with { Seed = (uint)v }; Fight = Fight with { Seed = (uint)v }; };
		draftGrid.AddChild(_seed);
		_focus.Add(_seed.GetLineEdit());
		AddChild(draftGrid);

		var scroll = new ScrollContainer { Name = "TargetScroll", CustomMinimumSize = new Vector2(0, 300), SizeFlagsVertical = SizeFlags.ExpandFill };
		var list = new VBoxContainer { Name = "Targets", SizeFlagsHorizontal = SizeFlags.ExpandFill };
		string? group = null;
		foreach (var t in Targets)
		{
			if (t.Group != group) { list.AddChild(DevMenu.PageLabel(t.Group, 20)); group = t.Group; }
			var b = new Button
			{
				Name = "Jump_" + t.Id.Replace(':', '_'),
				Text = t.Built ? t.Title : $"{t.Title}: {t.NotBuilt}",
				Disabled = !t.Built,
				Alignment = HorizontalAlignment.Left,
				TooltipText = t.NotBuilt ?? "",
			};
			b.AddThemeFontSizeOverride("font_size", 18);
			var target = t;
			b.Pressed += () => Jump(target.Id);
			_buttons[t.Id] = b;
			list.AddChild(b);
			if (t.Built) _focus.Add(b);
		}
		scroll.AddChild(list);
		AddChild(scroll);
		AddChild(_status = DevMenu.PageLabel("", 18, "Status"));
		RefreshFight();
	}

	private OptionButton Options(GridContainer grid, string label, IEnumerable<(string Text, bool Enabled)> items)
	{
		grid.AddChild(DevMenu.PageLabel(label, 20));
		var o = new OptionButton { Name = label.Replace(" ", "").Replace("/", "") };
		o.AddThemeFontSizeOverride("font_size", 18);
		Fill(o, items);
		grid.AddChild(o);
		_focus.Add(o);
		return o;
	}

	private static void Fill(OptionButton o, IEnumerable<(string Text, bool Enabled)> items)
	{
		o.Clear();
		foreach (var (text, enabled) in items)
		{
			o.AddItem(text);
			o.SetItemDisabled(o.ItemCount - 1, !enabled);
		}
	}

	/// <summary>Re-reads the setup into the dependent controls: temperaments, elder twist, copy level, the copy line.</summary>
	private void RefreshFight()
	{
		bool tanuki = Fight.Opponent == DevJump.TanukiId;
		var elder = DevJump.ElderOf(Fight.Opponent);
		_elder.Disabled = tanuki || elder is null;
		_elder.SetPressedNoSignal(Fight.Elder);
		_twistLine.Text = elder is null || tanuki ? "no elder" : $"{elder.Name}: {elder.Twist} ({DevJump.TwistNotBuilt})";

		string fighter = DevJump.PlaysAsOf(Fight); // YOK-174: whose profiles
		var temps = DevJump.Temperaments(fighter);
		Fill(_temperament, temps.Select(t => (t, true)));
		int ti = Fight.Temperament is { } want ? temps.ToList().IndexOf(want) : -1;
		if (ti < 0 && temps.Count > 0) { ti = 0; Fight = Fight with { Temperament = temps[0] }; }
		_temperament.Selected = ti;

		_copy.Disabled = _copyLevel.Disabled = !tanuki;
		var data = Fight.CopySpecial is { } id ? _pool.Special(id) : null;
		int top = data is null ? 1 : Math.Min(3, data.MaxLevel);
		Fill(_copyLevel, Enumerable.Range(1, top).Select(l => (l == 3 ? "Lv 3 (the copy runs Lv 2 tuning, T4)" : $"Lv {l}", data != null)));
		_copyLevel.Selected = Math.Clamp(Fight.CopyLevel, 1, top) - 1;
		_copyLine.Text = !tanuki ? "Tanuki copy: only for the Tanuki fight (T1, AM6)." : CopyPreview();
	}

	/// <summary>What the Tanuki will copy with this setup, locked on entering its node (T1).</summary>
	public string CopyPreview()
	{
		try
		{
			var run = DevRunStates.Source.Build(_pool);
			var target = Fight.CopySpecial is { } id ? DevJump.ShapeCopy(run, _pool, id, Fight.CopyLevel) : CopyRule.Target(run);
			return target is null
				? "Tanuki copy: none (Ryo holds no special)."
				: $"Tanuki copy, locked on entering its node (T1, AM6): {target.Data.Name} Lv {target.Level}, runs Lv {target.MoveLevel}, no modifier (T4).";
		}
		catch (ArgumentException e) { return "Tanuki copy: " + e.Message; }
	}

	private string? Refusal() => Context.RunChangeRefusal ?? (Context.Flow is null ? ReasonNoFlow : null);

	// ---- jumping ----

	/// <summary>
	/// Jumps to target <paramref name="id"/>. False, with <see cref="Status"/> saying why, when the target is greyed,
	/// the context refuses (dev mode off, mid-fight), there is no flow, or the jump could not be built.
	/// </summary>
	public bool Jump(string id)
	{
		bool ok = false;
		string? why = Targets.FirstOrDefault(t => t.Id == id) is not { } target ? $"no jump target '{id}'"
			: !target.Built ? target.NotBuilt
			: Context.Flow is null ? ReasonNoFlow
			: null;
		if (why == null && !Context.TryChangeRun(() => ok = Perform(Context.Flow!, Targets.First(t => t.Id == id), Fight, Draft, CloseMenu, out why), out var refusal))
			why = refusal;
		SetStatus(ok ? $"Jumped: {id}" : $"Refused: {why}");
		return ok;
	}

	private void SetStatus(string text)
	{
		Status = text;
		if (IsInstanceValid(_status)) _status.Text = text;
		GD.Print("DevJumpPage: " + text);
	}

	private void CloseMenu()
	{
		for (Node? n = GetParent(); n != null; n = n.GetParent())
			if (n is DevMenu m) { m.Close(); return; }
	}

	/// <summary>
	/// Builds the target's run state and opens it on <paramref name="flow"/>. <paramref name="beforeOpen"/> runs once the
	/// build succeeded and before anything opens (the page closes the dev menu there). False with <paramref name="error"/>
	/// when the build or the flow refuses.
	/// </summary>
	public static bool Perform(GameFlow flow, DevJumpTarget target, DevFightSetup fight, DevDraftSetup draft, Action? beforeOpen, out string? error)
	{
		error = null;
		try
		{
			var pool = FightScene.LoadAbilityPool();
			switch (target.Kind)
			{
				case DevJumpKind.Fight:
				{
					var jump = DevJump.BuildFight(fight, pool);
					beforeOpen?.Invoke();
					return Done(flow.StartDevRun(jump.Session, jump.NodeId, fight.Scheme, fight.Stage), out error);
				}
				case DevJumpKind.RestStop:
				{
					var jump = DevJump.BuildRestStop(target.Arg!, pool, seed: fight.Seed);
					beforeOpen?.Invoke();
					return Done(flow.StartDevRun(jump.Session, jump.NodeId, fight.Scheme), out error);
				}
				default:
					return OpenScreen(flow, target, fight, draft, pool, beforeOpen, out error);
			}
		}
		catch (Exception e) when (e is ArgumentException or InvalidOperationException or FormatException or System.IO.IOException or System.Text.Json.JsonException)
		{
			error = e.Message;
			return false;
		}
	}

	private static bool Done(MapResult r, out string? error)
	{
		error = r.Ok ? null : r.Error;
		return r.Ok;
	}

	private static T Load<T>(string scene) where T : Node => GD.Load<PackedScene>(scene).Instantiate<T>();

	/// <summary>A screen in the dev viewer, given a run state from the source (and Ryo's copy target for {move}).</summary>
	private static bool OpenScreen(GameFlow flow, DevJumpTarget target, DevFightSetup fight, DevDraftSetup draft, AbilityPool pool, Action? beforeOpen, out string? error)
	{
		error = null;
		var run = DevRunStates.Source.Build(pool);
		string yokai = StoryLibrary.DisplayName(fight.Opponent == DevJump.TanukiId ? "kitsune" : fight.Opponent);
		string? move = CopyRule.Target(run)?.Data.Name;
		Control screen;
		Action<DevScreenHost> wire;
		switch (target.Kind)
		{
			case DevJumpKind.RewardDraft:
			{
				var (offer, draftRun) = DevJump.BuildDraft(pool, draft.Yokai, draft.Seed, draft.Rares);
				if (offer.Offer.Cards.Count == 0) { error = $"{draft.Yokai} offers nothing to this run state"; return false; }
				var s = Load<RewardScreen>("res://scenes/ui/reward_screen.tscn");
				screen = s;
				wire = h => { s.Present(new DraftRewardSource(offer, draftRun)); s.Picked += (_, _) => h.Finish(); };
				break;
			}
			case DevJumpKind.StoryCard:
			{
				var card = FightScene.LoadStory()[target.Arg!];
				var s = Load<StoryCard>("res://scenes/ui/story_card.tscn");
				screen = s;
				wire = h => { s.Display(card.FillText(yokai, move)); s.Finished += h.Finish; };
				break;
			}
			case DevJumpKind.LoseScreen:
			{
				var (title, text) = FightScene.LoadStory().LoseScreen(yokai);
				var s = Load<LoseScreen>("res://scenes/ui/lose_screen.tscn");
				screen = s;
				wire = h => { s.SetText(title, text); s.Open(); s.RestartRequested += h.Finish; };
				break;
			}
			case DevJumpKind.RunComplete:
			{
				var (title, text) = FightScene.LoadStory().DemoComplete(yokai);
				var s = Load<DemoCompleteScreen>("res://scenes/ui/demo_complete.tscn");
				screen = s;
				wire = h => { s.SetText(title, text); s.Show(); s.RestartRequested += h.Finish; };
				break;
			}
			case DevJumpKind.ControlSelect:
			{
				var s = Load<StartScreen>("res://scenes/ui/start_screen.tscn");
				screen = s;
				var keep = MenuSettings.Scheme; // the real screen sets the scheme; a dev view must not change the player's setting
				wire = h =>
				{
					s.Open();
					s.Started += h.Finish;
					s.BackRequested += h.Finish;
					// TreeExiting: on Close and also when the host leaves the tree any other way (quit with the view open).
					h.TreeExiting += () => { if (MenuSettings.Scheme != keep) MenuSettings.SetScheme(keep); };
				};
				break;
			}
			case DevJumpKind.MoveList:
			{
				var view = MoveListSource.Build(run, ContentPaths.Data("moves"));
				var s = Load<MoveListPanel>("res://scenes/ui/move_list_panel.tscn");
				screen = s;
				wire = h => { s.Bind(view); s.Visible = true; s.BackRequested += h.Finish; };
				break;
			}
			default:
				error = $"{target.Title}: {DevJump.NotBuiltYet}";
				return false;
		}
		beforeOpen?.Invoke();
		if (flow.OpenDevScreen(target.Title, screen, wire)) return true;
		error = "the dev viewer opens only over the title or the map, one screen at a time";
		return false;
	}
}
