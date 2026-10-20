using System;
using System.Linq;
using Godot;
using YokaiFighters.Flow;
using YokaiFighters.Sim;
using YokaiFighters.Ui.Kit;

namespace YokaiFighters.Ui;

/// <summary>
/// The rest stop (YOK-96, GDD 6.3, R6, R7, T6; docs/design/screens/rough/rest-stop.png and rest-stop-dojo.png):
/// a merchant's or a dojo's stop with its one choice and the Tanuki forecast. Layout is <c>scenes/ui/rest_stop.tscn</c>;
/// this script binds <see cref="RestStopScreenModel"/> (which asks <see cref="Merchant"/> / <see cref="Dojo"/> /
/// <see cref="CopyRule"/>) and handles input; it holds no rule of its own.
/// <para>
/// It is the node scene of a merchant or dojo node (<c>data/map/node-scenes.json</c>): <see cref="INodeScene.BindRun"/>
/// hands it the session, Continue calls <see cref="RunSession.CompleteNode"/> and the flow shows the map. Nothing is
/// written to the save inside the stop (the run is saved on entering the node), so quitting and choosing Continue
/// on the title builds this screen again from the entry state with no choice taken (M5).
/// </para>
/// Input: Up / Down move between the two cards, Enter / pad A takes the focused one, Tab / pad Y toggles the
/// forecast detail, Esc / pad B asks for the title (the run waits in the save file).
/// </summary>
public partial class RestStopScreen : Control, INodeScene
{
	/// <summary>Esc / pad B: leave for the title. The stop is not completed; Continue brings it back untouched.</summary>
	public event Action? TitleRequested;
	/// <summary>The stop was completed and the map is next (the flow reacts to the session's own event; tests listen here).</summary>
	public event Action? Left;

	/// <summary>Where the story line comes from; null = <c>data/story/</c> for the merchant, a labelled placeholder for the dojo.</summary>
	public Func<RestStopScreenModel, string>? LineSource { get; set; }

	public RestStopScreenModel? Model { get; private set; }
	public bool DetailVisible { get; private set; }

	public KitCard FreeCard { get; private set; } = null!;
	public KitCard SecondCard { get; private set; } = null!;
	public Button ContinueButton { get; private set; } = null!;

	private RunSession? _session;
	private bool _ready;

	public void BindRun(RunSession session, ControlScheme scheme)
	{
		_session = session;
		Model = RestStopScreenModel.Create(session);
		if (Model == null) GD.PushWarning($"RestStopScreen: node '{session.Node?.NodeId}' is not a merchant or a dojo");
		if (_ready) Refresh();
	}

	public override void _Ready()
	{
		_ready = true;
		MenuInput.EnsurePadConfirm();
		FreeCard = GetNode<KitCard>("FreeCard");
		SecondCard = GetNode<KitCard>("SecondCard");
		ContinueButton = GetNode<Button>("ContinueButton");
		FreeCard.Activated += () => TakeFree();
		SecondCard.Activated += () => OpenSecond(); // the shop (YOK-104); the trial (YOK-98) wires in here too
		ContinueButton.Pressed += () => Leave();
		// The scene's slim brush_pad margins carry no border, so keyboard focus needs its own mark: an ink outline drawn
		// around the brush stroke (this screen only; the shared kit button is untouched).
		var focusMark = new StyleBoxFlat { DrawCenter = false, BorderColor = new Color("1D1B21"), ExpandMarginLeft = 18, ExpandMarginRight = 18, ExpandMarginTop = 6, ExpandMarginBottom = 6 };
		focusMark.SetBorderWidthAll(3);
		focusMark.SetCornerRadiusAll(4);
		ContinueButton.AddThemeStyleboxOverride("focus", focusMark);
		foreach (var card in new Control[] { FreeCard, SecondCard, GetNode<Control>("LineBox") }) PadPaper(card);
		GetNode<KeyChipFooter>("Footer").Alignment = BoxContainer.AlignmentMode.Begin;
		Refresh();
	}

	/// <summary>
	/// The rice-card texture draws its ink frame about 20 px inside its edge, so the kit's 28 px margins let text touch it
	/// (and the speaker label cross the top border). Cards on this screen get margins that clear the frame; the style is
	/// a copy, so the kit's own card is untouched.
	/// </summary>
	private static void PadPaper(Control card)
	{
		if (card.GetThemeStylebox("panel") is not StyleBoxTexture kit) return;
		var padded = (StyleBoxTexture)kit.Duplicate();
		padded.ContentMarginLeft = 46; padded.ContentMarginRight = 46;
		padded.ContentMarginTop = 24; padded.ContentMarginBottom = 30;
		card.AddThemeStyleboxOverride("panel", padded);
	}

	/// <summary>The free heal or the rest. A refused take (already chosen) changes nothing and the screen just redraws.</summary>
	public bool TakeFree()
	{
		if (Model == null) return false;
		bool ok = Model.TakeFree().Ok;
		Refresh();
		if (ok) ContinueButton.GrabFocus();
		return ok;
	}

	/// <summary>
	/// The merchant's second card: spends the stop's choice on the shop (which closes the free heal, R6) and asks the flow
	/// to show the shop screen (<see cref="ShopRequested"/>, routed by GameFlow/ScreenRouter). Refused, changing nothing, when the heal was taken.
	/// </summary>
	public bool OpenSecond()
	{
		if (Model is { Kind: RestStopKind.Dojo } dm && _session != null) return OpenTrial(dm);
		if (Model is not { Kind: RestStopKind.Merchant } m || _session == null) return false;
		if (!m.OpenShop().Ok) { Refresh(); return false; }
		Refresh(); // the used state first, so nothing under the shop changes while it is open
		ShopRequested?.Invoke();
		return true;
	}

	/// <summary>
	/// YOK-98, R7: the dojo's second card: takes the offered trial as the stop's one choice (closing the rest) and
	/// asks the flow to play it (<see cref="TrialRequested"/>). Refused, changing nothing, when the rest was taken,
	/// no trial is offered or the trial is already over.
	/// </summary>
	private bool OpenTrial(RestStopScreenModel m)
	{
		if (!m.TrialRunning && !m.ChooseTrial().Ok) { Refresh(); return false; }
		Refresh();
		TrialRequested?.Invoke();
		return true;
	}

	/// <summary>YOK-98: raised once the trial is chosen: the flow plays it on the fight scene and brings this stop back after.</summary>
	public event Action? TrialRequested;

	/// <summary>The flow calls this when the trial fight ended and the stop shows again: the result, focus on Continue.</summary>
	public void TrialClosed()
	{
		Refresh();
		if (ContinueButton.Visible) ContinueButton.GrabFocus();
	}

	/// <summary>Raised after the shop was chosen: the flow shows the shop over this stop (and hides it).</summary>
	public event Action? ShopRequested;

	/// <summary>The line the merchant is speaking, for the shop screen to repeat.</summary>
	public string MerchantLine => L("LineBox/Box/Line").Text;

	/// <summary>The flow calls this when the shop closed and the stop shows again: the used state, focus on Continue.</summary>
	public void ShopClosed()
	{
		Refresh();
		if (ContinueButton.Visible) ContinueButton.GrabFocus();
	}

	/// <summary>Continue: completes the node. Refused before a choice is made.</summary>
	public bool Leave()
	{
		if (Model == null || !Model.Leave().Ok) return false;
		Left?.Invoke();
		return true;
	}

	public void ToggleDetail()
	{
		DetailVisible = !DetailVisible;
		Refresh();
	}

	public override void _Input(InputEvent e)
	{
		if (!IsVisibleInTree()) return; // hidden while the shop is open: the shop answers its own keys
		if (MenuInput.FrameData(e)) { ToggleDetail(); GetViewport().SetInputAsHandled(); }
		else if (SelectorStep(e) is int step && step != 0 && Model is { CanSelect: true } sel)
		{
			if (sel.SelectTrial(step)) Refresh();
			GetViewport().SetInputAsHandled();
		}
		else if (MenuInput.Back(e)) { TitleRequested?.Invoke(); GetViewport().SetInputAsHandled(); }
	}

	/// <summary>YOK-109: the dojo's special selector: Left / Right, A / D, the bumpers or the D-pad. 0 = not a selector key.</summary>
	public static int SelectorStep(InputEvent e) => e switch
	{
		InputEventKey { Pressed: true, Echo: false, Keycode: Key.Left or Key.A } => -1,
		InputEventKey { Pressed: true, Echo: false, Keycode: Key.Right or Key.D } => 1,
		InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.LeftShoulder or JoyButton.DpadLeft } => -1,
		InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.RightShoulder or JoyButton.DpadRight } => 1,
		_ => 0,
	};

	private Label L(string path) => GetNode<Label>(path);

	private static void Tag(Control tag, string text, Color? fill = null)
	{
		tag.GetNode<Label>("TagLabel").Text = text;
		if (fill is { } c)
			tag.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = c });
	}

	/// <summary>Redraws everything from the model; safe to call at any time.</summary>
	public void Refresh()
	{
		if (!_ready || Model is not { } m) return;
		bool dojo = m.Kind == RestStopKind.Dojo;
		L("RowLine").Text = m.RowLine;
		L("HealthLabel").Text = $"RYO · {m.Health} / {m.MaxHealth}";
		GetNode<ProgressBar>("HealthBar").Value = m.Health;
		L("CoinChip/CoinValue").Text = m.Coins.ToString();

		string speaker = dojo ? "Dojo master" : "Merchant";
		L("LineBox/Box/Speaker").Text = speaker;
		L("LineBox/Box/Line").Text = (LineSource ?? DefaultLine)(m);
		L("Portrait/PortraitLabel").Text = dojo ? "dojo master (art to come)" : "merchant portrait (art to come)";

		BindFree(m, dojo);
		BindSecond(m, dojo);
		BindForecast(m);

		bool done = m.ChoiceMade;
		var pick = L("PickLine");
		pick.Text = dojo ? "Rest or train: pick one. The other closes when you leave." : "Heal or shop: pick one. The other closes when you leave.";
		pick.Visible = !done;
		ContinueButton.Visible = done;
		var footer = GetNode<KeyChipFooter>("Footer");
		// Esc / B goes to the title today (GameFlow: Router.ReturnToTitle), so the label says so; the mockup's "Map" needs a leave-the-node API (reported on YOK-109).
		if (done) footer.SetHints(new("confirm", "Continue"), new("frames", "Forecast detail"), new("back", "Title"));
		else if (m.CanSelect) footer.SetHints(new("move", "Choose"), new("choose", "Special"), new("confirm", "Take"), new("frames", "Detail"), new("back", "Title"));
		else footer.SetHints(new("move", "Choose"), new("confirm", "Take"), new("frames", "Forecast detail"), new("back", "Title"));
	}

	/// <summary>The largest font size (46 down to 20) at which <paramref name="text"/> wraps to fit a column of this size.</summary>
	public static int NameFontSize(Font font, string text, float width, float height)
	{
		int size = 46;
		while (size > 20 && font.GetMultilineStringSize(text, HorizontalAlignment.Left, width, size, 2).Y > height) size -= 2;
		return size;
	}

	/// <summary>True when the name fits its column at the size <see cref="NameFontSize"/> picked (two lines at most).</summary>
	public static bool NameFits(Font font, string text, float width, float height) =>
		font.GetMultilineStringSize(text, HorizontalAlignment.Left, width, NameFontSize(font, text, width, height), 2).Y <= height;

	private string DefaultLine(RestStopScreenModel m)
	{
		if (m.Kind == RestStopKind.Dojo) return "Dojo master's line (habit-writer: not written yet).";
		if (m.Row != 4) return "Merchant's line for this stop (habit-writer: S3 schedules only row 4's).";
		try
		{
			var story = Fight.FightScene.LoadStory();
			if (story.Has("merchant-row4")) return story["merchant-row4"].FillText(move: m.Forecast?.Name ?? "special");
		}
		catch (Exception e) when (e is System.IO.IOException or FormatException) { GD.PushWarning($"RestStopScreen: no merchant line: {e.Message}"); }
		return "Merchant's line (habit-writer).";
	}

	private void BindFree(RestStopScreenModel m, bool dojo)
	{
		var card = "FreeCard/Box/";
		L(card + "Top/Icon/Glyph").Text = dojo ? "R" : "+";
		L(card + "Top/Titles/Title").Text = dojo ? "Rest" : "Free heal";
		L(card + "Top/Titles/Sub").Text = dojo ? "Quiet night in the dojo" : "Merchant's gift · costs nothing";
		Tag(FreeCard.GetNode<Control>("Box/Top/Tag"), dojo ? "REST" : "FREE");
		var now = FreeCard.GetNode<ProgressBar>("Box/Preview/Now");
		var after = FreeCard.GetNode<ProgressBar>("Box/Preview/After");
		now.Value = m.Health;
		var desc = L(card + "Desc");
		switch (m.Free)
		{
			case RestStopOption.Ready:
				desc.Text = $"Restores {m.FreePercent}% of max health:\n+{m.FreeGain} → {m.HealthAfterFree} / {m.MaxHealth}";
				after.Value = m.HealthAfterFree;
				break;
			case RestStopOption.Taken:
				desc.Text = $"Done: +{m.FreeGain} → {m.Health} / {m.MaxHealth}";
				after.Value = m.Health;
				break;
			default:
				desc.Text = "Closed: the other option was taken.";
				after.Value = m.Health;
				break;
		}
		FreeCard.CardDisabled = m.Free != RestStopOption.Ready;
		FreeCard.Selected = m.Free == RestStopOption.Taken;
		if (m.Free == RestStopOption.Ready && IsInsideTree() && !ContinueButton.HasFocus() && GetViewport().GuiGetFocusOwner() == null) FreeCard.GrabFocus();
	}

	private void BindSecond(RestStopScreenModel m, bool dojo)
	{
		var card = "SecondCard/Box/";
		L(card + "Top/Icon/Glyph").Text = dojo ? "T" : "¥";
		L(card + "Top/Titles/Title").Text = dojo ? "Training trial" : "Browse the shop";
		L(card + "Top/Titles/Sub").Text = dojo ? "A drill for one special" : "Spend coins instead";
		Tag(SecondCard.GetNode<Control>("Box/Top/Tag"), dojo ? "TRIAL" : "SHOP");
		L(card + "Desc").Text = dojo
			? (m.ChosenTrial is { } t
				? $"{t.Prompt}\nHeals {Dojo.TrialHealPercent}% either way; land it within {t.Attempts} tries to level {m.TrialSpecialName}."
				: "No special of yours has a trial that can level it: each is missing a drill or is at its top level.")
			: m.Second == RestStopOption.Ready ? $"Buy an ability or release a binding.\nYou have {m.Coins} coins." : "Buy an ability or release a binding."; // the coin chip shows the coins once the card is closed
		if (dojo) L(card + "Desc").AddThemeFontSizeOverride("font_size", 20); // the dojo card carries the picker row too: one size down keeps it inside its scroll
		var picker = SecondCard.GetNode<Control>("Box/Picker");
		picker.Visible = dojo && m.Second != RestStopOption.NoTrial;
		if (picker.Visible)
		{
			string lv = $"{m.TrialSpecialName} Lv {m.TrialSpecialLevel}";
			L(card + "Picker/Special").Text = m.CanSelect ? $"Special: < {lv} >" : $"Special: {lv}";
			int used = m.AttemptsUsed;
			L(card + "Picker/Pips").Text = "Tries " + string.Join(" ", Enumerable.Range(0, RestStopScreenModel.MaxAttempts).Select(i => i < used ? "●" : "○"));
		}
		L(card + "Coming").Text = m.Second switch
		{
			RestStopOption.NoTrial => "Trial unavailable: no drill for your specials.",
			RestStopOption.Closed => "Closed: the other option was taken.",
			RestStopOption.Taken => dojo ? DojoTaken() : "Chosen · Enter to browse again",
			_ => "",
		};
		L(card + "Coming").Visible = L(card + "Coming").Text.Length > 0; // no empty line eating the card's height
		// The shop stays open for the stop (R6: buying more is allowed): once chosen the card goes back in, as "Chosen".
		SecondCard.CardDisabled = !(m.Second == RestStopOption.Ready || (m.Second == RestStopOption.Taken && !dojo));
	}

	private string DojoTaken() => _session?.Dojo switch
	{
		{ TrialResult: DojoTrialResult.Landed } d => $"Landed · +{d.Healed} health · Lv {d.LevelBefore} → {d.LevelAfter}",
		{ TrialResult: DojoTrialResult.Failed } d => $"Missed · +{d.Healed} health",
		_ => "Chosen",
	};

	private void BindForecast(RestStopScreenModel m)
	{
		var f = m.Forecast;
		var f0 = "Forecast/";
		// Dojo only (T2, A4): training the selected special raises the level the copy runs at.
		L(f0 + "TrainNote").Text = m.CopyRaisedTo() is { } raised ? $"Training {m.TrialSpecialName} to Lv {raised} raises this copy." : "";
		GetNode<TextureRect>(f0 + "IconGlow/IconBox/Icon").Texture = f == null ? null : AbilityArt.ForId(f.SpecialId);
		var nameLabel = L(f0 + "SpecialName");
		nameLabel.Text = f?.Name ?? "Nothing yet";
		nameLabel.AddThemeFontSizeOverride("font_size", NameFontSize(nameLabel.GetThemeFont("font"), nameLabel.Text, nameLabel.Size.X, nameLabel.Size.Y));
		GetNode<Control>(f0 + "LevelTag").Visible = f != null;
		if (f == null)
		{
			// A lock that found no special (or none owned): nothing to forecast, so no level, cost, note or lock line either.
			GetNode<ProgressBar>(f0 + "CostBar/Fill").Value = 0;
			L(f0 + "CopyNote").Text = "The Tanuki has nothing of yours to copy.";
			L(f0 + "CostValue").Text = "";
			L(f0 + "CostMeaning").Text = "";
			GetNode<Control>(f0 + "Placeholder").Visible = false;
			L(f0 + "LockNote").Text = _session is { CopyLocked: true } ? "Locked for this run: no copy." : "";
			return;
		}
		L(f0 + "LevelTag/LevelLabel").Text = $"Lv {f.Level}";
		L(f0 + "CopyNote").Text = f.RyoLevel > f.Level
			? $"Ryo's Lv {f.RyoLevel} evolution is not copied: Lv {f.Level}."
			: "Copied at your level, never your modifier or evolution.";
		L(f0 + "CostValue").Text = f.Cost is { } c ? $"~{c} health" : "Not enough fights yet";
		GetNode<ProgressBar>(f0 + "CostBar/Fill").Value = f.Cost ?? 0;
		GetNode<Control>(f0 + "Placeholder").Visible = f.CostIsPlaceholder;
		L(f0 + "CostMeaning").Text = DetailVisible
			? $"Detail: slot {f.Slot}; Ryo has it at Lv {f.RyoLevel}, the copy runs Lv {f.Level}."
			: "Average health you lose per fight to this move, from your own fights so far.";
		L(f0 + "LockNote").Text = f.Locked
			? "Locked for this run: this is the move the Tanuki copies."
			: "Locks when you enter the Tanuki's node.";
	}
}
