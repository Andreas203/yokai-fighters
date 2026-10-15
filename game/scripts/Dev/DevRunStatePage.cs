using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Flow;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Dev;

/// <summary>
/// YOK-126: the dev menu's Run state page. Thin: every control writes into a <see cref="DevRunBuild"/>, every answer
/// (refusal, copy target) comes from <see cref="DevRunEditor.Check"/>, presets from <see cref="DevPresetStore"/>.
/// Apply goes through <see cref="DevContext.TryChangeRun"/> and <see cref="GameFlow.AdoptDevSession"/>, so it is
/// refused inside a fight. Opened on the title it edits a new run (Apply shows it on the map).
/// </summary>
public partial class DevRunStatePage : VBoxContainer
{
	public const string EmptyItem = "(empty)", NoModifier = "(no modifier)";

	readonly DevContext _ctx;
	public DevRunEditor? Editor { get; }
	public DevPresetStore? Presets { get; }
	public DevRunCheck? LastCheck { get; private set; }
	bool _loading;

	public Label CopyLabel { get; } = DevMenu.PageLabel("", 24, "CopyTarget");
	public Label StatusLabel { get; } = DevMenu.PageLabel("", 20, "Status");
	public Button ApplyButton { get; } = new() { Name = "Apply", Text = "Apply to run" };
	public SpinBox Coins { get; } = Spin("Coins", 0, 99_999);
	public SpinBox Health { get; } = Spin("Health", 1, RunState.MaxHealth);
	public SpinBox Depth { get; } = Spin("Depth", 0, 20);
	public SpinBox Seed { get; } = Spin("Seed", 0, uint.MaxValue);
	public LineEdit PresetName { get; } = new() { Name = "PresetName", PlaceholderText = "preset name", CustomMinimumSize = new Vector2(260, 0) };
	public Button SavePresetButton { get; } = new() { Name = "SavePreset", Text = "Save preset" };
	public OptionButton PresetList { get; } = new() { Name = "PresetList", CustomMinimumSize = new Vector2(260, 0) };
	public Button LoadPresetButton { get; } = new() { Name = "LoadPreset", Text = "Load" };
	public Button DeletePresetButton { get; } = new() { Name = "DeletePreset", Text = "Delete" };
	public IReadOnlyDictionary<SpecialSlot, SlotRow> Rows => _rows;
	readonly Dictionary<SpecialSlot, SlotRow> _rows = new();
	IReadOnlyList<DevPreset> _shownPresets = Array.Empty<DevPreset>();

	/// <summary>One slot's controls.</summary>
	public sealed class SlotRow
	{
		public OptionButton Special = new(), Modifier = new();
		public SpinBox Level = Spin("Level", 1, 3), Order = Spin("Order", 0, 4);
		public CheckBox Borrowed = new() { Text = "borrowed" }, ModBorrowed = new() { Text = "mod borrowed" };
	}

	/// <summary>The menu's builder: the editor and presets come from the context (the flow's session, or a new run on the title).</summary>
	public DevRunStatePage(DevContext ctx) : this(ctx, MakeEditor(ctx, out var start), start, new DevPresetStore(DevPresetStore.PathFor(ctx.Flow?.SaveFile ?? GameFlow.UserSavePath))) { }

	public DevRunStatePage(DevContext ctx, DevRunEditor? editor, DevRunBuild? start, DevPresetStore? presets)
	{
		_ctx = ctx;
		Editor = editor;
		Presets = presets;
		Name = "DevRunStatePage";
		AddThemeConstantOverride("separation", 8);
		AddChild(DevMenu.PageLabel("Run state", 30));
		if (editor is null || start is null)
		{
			AddChild(DevMenu.PageLabel("No run to edit here (open it from the title or the map).", 22, "NoRun"));
			return;
		}
		CopyLabel.AutowrapMode = StatusLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		AddChild(CopyLabel);
		AddChild(StatusLabel);

		var grid = new GridContainer { Name = "Slots", Columns = 7 };
		grid.AddThemeConstantOverride("h_separation", 10);
		foreach (string h in new[] { "Slot", "Special", "Level", "Modifier", "T3 order", "", "" }) grid.AddChild(DevMenu.PageLabel(h, 18));
		foreach (var slot in new[] { SpecialSlot.A, SpecialSlot.B, SpecialSlot.C, SpecialSlot.D })
		{
			var row = new SlotRow();
			row.Special.Name = "Special" + slot;
			row.Modifier.Name = "Modifier" + slot;
			row.Level.Name = "Level" + slot;
			row.Order.Name = "Order" + slot;
			row.Borrowed.Name = "Borrowed" + slot;
			row.ModBorrowed.Name = "ModBorrowed" + slot;
			row.Special.AddItem(EmptyItem);
			foreach (var s in editor.SpecialsFor(slot)) row.Special.AddItem(s.Id);
			row.Special.CustomMinimumSize = row.Modifier.CustomMinimumSize = new Vector2(230, 0);
			row.Special.ItemSelected += _ => { FillModifiers(row, null); Changed(); };
			row.Modifier.ItemSelected += _ => Changed();
			row.Level.ValueChanged += _ => Changed();
			row.Order.ValueChanged += _ => Changed();
			row.Borrowed.Toggled += _ => Changed();
			row.ModBorrowed.Toggled += _ => Changed();
			grid.AddChild(DevMenu.PageLabel(slot.ToString(), 22));
			foreach (Control c in new Control[] { row.Special, row.Level, row.Modifier, row.Order, row.Borrowed, row.ModBorrowed }) grid.AddChild(c);
			_rows[slot] = row;
		}
		AddChild(grid);

		var nums = new HBoxContainer { Name = "Numbers" };
		nums.AddThemeConstantOverride("separation", 10);
		foreach (var (label, box) in new[] { ("Coins", Coins), ("Health", Health), ("Depth (rows done)", Depth), ("Draft seed", Seed) })
		{
			nums.AddChild(DevMenu.PageLabel(label, 18));
			nums.AddChild(box);
			box.ValueChanged += _ => Changed();
		}
		Depth.MaxValue = editor.MaxDepth;
		AddChild(nums);

		ApplyButton.Pressed += () => Apply();
		AddChild(ApplyButton);

		var presetsRow = new HBoxContainer { Name = "Presets" };
		presetsRow.AddThemeConstantOverride("separation", 10);
		foreach (Control c in new Control[] { PresetName, SavePresetButton, PresetList, LoadPresetButton, DeletePresetButton }) presetsRow.AddChild(c);
		SavePresetButton.Pressed += () => SavePreset(PresetName.Text);
		LoadPresetButton.Pressed += () => LoadPreset(PresetList.Selected);
		DeletePresetButton.Pressed += () => DeletePreset(PresetList.Selected);
		AddChild(presetsRow);

		RefreshPresets();
		Show(start);
	}

	public override void _Ready()
	{
		if (Editor is null) return;
		// Pages wire their own focus: a ring through the page, entered with Right from this section's button.
		var ring = new List<Control>();
		foreach (var r in _rows.Values) ring.AddRange(new Control[] { r.Special, r.Level.GetLineEdit(), r.Modifier, r.Order.GetLineEdit(), r.Borrowed, r.ModBorrowed });
		ring.AddRange(new Control[] { Coins.GetLineEdit(), Health.GetLineEdit(), Depth.GetLineEdit(), Seed.GetLineEdit(), ApplyButton,
			PresetName, SavePresetButton, PresetList, LoadPresetButton, DeletePresetButton });
		MenuInput.WrapVertical(ring.ToArray());
		Node? menu = GetParent();
		while (menu != null && menu is not DevMenu) menu = menu.GetParent();
		if (menu != null && menu.FindChild("Section_" + DevMenu.RunId, true, false) is Control button)
		{
			button.FocusNeighborRight = button.GetPathTo(ring[0]);
			foreach (var c in ring) c.FocusNeighborLeft = c.GetPathTo(button);
		}
	}

	static DevRunEditor? MakeEditor(DevContext ctx, out DevRunBuild? start)
	{
		start = null;
		RunSession? s = ctx.Flow?.Session;
		if (s is null && ctx.Place == DevPlace.Title && ctx.Flow is { } flow)
		{
			try { s = FightScene.NewSession(flow.MapFile, flow.FixedRunSeed ?? 1); }
			catch (Exception e) when (e is FormatException or System.IO.IOException) { GD.PushWarning($"DevRunStatePage: no map: {e.Message}"); }
		}
		if (s is null) return null;
		start = DevRunEditor.Capture(s);
		return DevRunEditor.For(s);
	}

	/// <summary>The build the controls describe now.</summary>
	public DevRunBuild CurrentBuild()
	{
		var slots = new List<DevSlotEdit>();
		foreach (var (slot, r) in _rows)
		{
			if (r.Special.Selected <= 0) continue;
			string? mod = r.Modifier.Selected > 0 ? r.Modifier.GetItemText(r.Modifier.Selected) : null;
			slots.Add(new DevSlotEdit(slot, r.Special.GetItemText(r.Special.Selected), (int)r.Level.Value, mod, (int)r.Order.Value,
				r.Borrowed.ButtonPressed, r.ModBorrowed.ButtonPressed));
		}
		return new DevRunBuild(slots, (int)Coins.Value, (int)Health.Value, (int)Depth.Value, (uint)Seed.Value);
	}

	/// <summary>Puts <paramref name="build"/> in the controls (unknown ids show as empty and the check says so) and re-checks.</summary>
	public void Show(DevRunBuild build)
	{
		_loading = true;
		foreach (var (slot, r) in _rows)
		{
			var e = build[slot];
			r.Special.Selected = 0;
			for (int i = 1; e != null && i < r.Special.ItemCount; i++) if (r.Special.GetItemText(i) == e.Special) r.Special.Selected = i;
			FillModifiers(r, e?.Modifier);
			r.Level.Value = e?.Level ?? 1;
			r.Order.Value = e?.LevelledOrder ?? 0;
			r.Borrowed.ButtonPressed = e?.Borrowed ?? false;
			r.ModBorrowed.ButtonPressed = e?.ModifierBorrowed ?? false;
		}
		Coins.Value = build.Coins;
		Health.Value = build.Health;
		Depth.Value = build.Depth;
		Seed.Value = build.Seed;
		_loading = false;
		Changed();
	}

	void FillModifiers(SlotRow r, string? select)
	{
		r.Modifier.Clear();
		r.Modifier.AddItem(NoModifier);
		if (r.Special.Selected > 0)
			foreach (var m in Editor!.ModifiersFor(r.Special.GetItemText(r.Special.Selected))) r.Modifier.AddItem(m.Id);
		r.Modifier.Selected = 0;
		for (int i = 1; select != null && i < r.Modifier.ItemCount; i++) if (r.Modifier.GetItemText(i) == select) r.Modifier.Selected = i;
	}

	void Changed()
	{
		if (_loading || Editor is null) return;
		LastCheck = Editor.Check(CurrentBuild());
		CopyLabel.Text = LastCheck.Copy is { } c
			? $"Tanuki copy target: {c.Data.Id} (slot {c.Slot}, Ryo Lv {c.Level}; the copy runs Lv {c.MoveLevel}, no modifier, T4)"
			: "Tanuki copy target: none (build refused)";
		string? gate = _ctx.RunChangeRefusal;
		StatusLabel.Text = !LastCheck.Ok ? "Refused: " + LastCheck.Refusal : gate != null ? "Valid build. " + gate : "Valid build.";
		ApplyButton.Disabled = !LastCheck.Ok || gate != null || _ctx.Flow is null;
	}

	/// <summary>Adopts the checked build as the run (title or map only). False, with the reason in the status line, otherwise.</summary>
	public bool Apply()
	{
		if (Editor is null) return false;
		Changed();
		if (LastCheck is not { Ok: true, Session: { } session }) return false;
		if (_ctx.Flow is not { } flow) { StatusLabel.Text = "Refused: no boot flow to hand the run to."; return false; }
		string? inner = null;
		bool ok = _ctx.TryChangeRun(() => flow.AdoptDevSession(session, out inner), out string? refusal) && inner == null;
		StatusLabel.Text = ok ? "Applied." : "Refused: " + (refusal ?? inner);
		if (ok) LastCheck = Editor.Check(CurrentBuild()); // a fresh session for any later Apply; the adopted one is the run now
		return ok;
	}

	void RefreshPresets()
	{
		if (Presets is null || Editor is null) return;
		var saved = Presets.Load();
		_shownPresets = Editor.BuiltInPresets((uint)Seed.Value).Where(b => saved.All(p => p.Name != b.Name)).Concat(saved).ToArray();
		PresetList.Clear();
		foreach (var p in _shownPresets) PresetList.AddItem(p.BuiltIn ? p.Name + " (built-in)" : p.Name);
		if (Presets.Problem != null) StatusLabel.Text = Presets.Problem;
	}

	public IReadOnlyList<DevPreset> ShownPresets => _shownPresets;

	public bool SavePreset(string name)
	{
		if (Presets is null || string.IsNullOrWhiteSpace(name)) { StatusLabel.Text = "A preset needs a name."; return false; }
		Presets.Save(new DevPreset(name.Trim(), CurrentBuild()));
		RefreshPresets();
		StatusLabel.Text = $"Saved preset '{name.Trim()}'.";
		return true;
	}

	public bool LoadPreset(int index)
	{
		if (index < 0 || index >= _shownPresets.Count) return false;
		Show(_shownPresets[index].Build);
		return true;
	}

	public bool DeletePreset(int index)
	{
		if (Presets is null || index < 0 || index >= _shownPresets.Count || _shownPresets[index].BuiltIn) return false;
		bool ok = Presets.Delete(_shownPresets[index].Name);
		RefreshPresets();
		return ok;
	}

	static SpinBox Spin(string name, double min, double max) => new() { Name = name, MinValue = min, MaxValue = max, Step = 1, Rounded = true, AllowGreater = false };
}
