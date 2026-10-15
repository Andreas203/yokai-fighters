using System;
using System.Collections.Generic;
using Godot;
using YokaiFighters.Ui;

namespace YokaiFighters.Dev;

/// <summary>
/// YOK-123 dev menu shell (<c>scenes/ui/dev_menu.tscn</c>, inside the <see cref="DevShell"/> autoload). Opened with
/// <see cref="DevMode.MenuKey"/> (F11) or the Dev menu button on the title and the map (<c>GameFlow</c>), and from the
/// pause menu (<c>FightScene.Pause</c>). Left: one button per registered section; right: the open section's page,
/// built fresh from a <see cref="DevContext"/> each time the menu opens. Esc / pad B / F11 closes it.
/// <para>
/// <b>Adding a page (YOK-124/125/126):</b> <c>DevMenu.AddSection(id, title, ctx =&gt; page)</c>. An id already
/// registered is replaced in place (same position), so the Map / Jump / Run state stubs in
/// <c>DevMenu.Sections.cs</c> are swapped for the real pages by changing their one line there. A page that changes the
/// run must go through <see cref="DevContext.TryChangeRun"/> (refused inside a fight).
/// </para>
/// </summary>
public partial class DevMenu : Control
{
	/// <summary>One page of the menu. <paramref name="NeedsDevMode"/>: the page's button is greyed while dev mode is off.</summary>
	public sealed record Section(string Id, string Title, Func<DevContext, Control> Build, bool NeedsDevMode = true);

	private static readonly List<Section> _sections = new();
	/// <summary>Registered sections, in menu order.</summary>
	public static IReadOnlyList<Section> Sections => _sections;

	/// <summary>Registers a page: replaces the section with the same <paramref name="id"/> in place, else appends it.</summary>
	public static void AddSection(string id, string title, Func<DevContext, Control> build, bool needsDevMode = true)
	{
		if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("section id is empty", nameof(id));
		ArgumentNullException.ThrowIfNull(build);
		var section = new Section(id, title, build, needsDevMode);
		int at = _sections.FindIndex(s => s.Id == id);
		if (at >= 0) _sections[at] = section; else _sections.Add(section);
	}

	/// <summary>Removes a page; false when no section has that id.</summary>
	public static bool RemoveSection(string id) => _sections.RemoveAll(s => s.Id == id) > 0;

	public static Section? FindSection(string id) => _sections.Find(s => s.Id == id);

	public event Action? Closed;
	public DevContext? Context { get; private set; }
	public string? CurrentSection { get; private set; }
	public bool IsOpen => Visible;
	public Button ModeToggle { get; private set; } = null!;
	public Button CloseButton { get; private set; } = null!;
	/// <summary>The open page (a child of the page holder); null while closed.</summary>
	public Control? Page { get; private set; }

	private Label _place = null!, _modeNote = null!;
	private VBoxContainer _list = null!, _holder = null!;
	private readonly Dictionary<string, Button> _buttons = new();

	public override void _Ready()
	{
		MenuInput.EnsurePadConfirm();
		ProcessMode = ProcessModeEnum.Always;
		ModeToggle = UiFind.Get<Button>(this, "ModeToggle");
		CloseButton = UiFind.Get<Button>(this, "Close");
		_place = UiFind.Get<Label>(this, "Place");
		_modeNote = UiFind.Get<Label>(this, "ModeNote");
		_list = UiFind.Get<VBoxContainer>(this, "SectionList");
		_holder = UiFind.Get<VBoxContainer>(this, "PageHolder");
		ModeToggle.Pressed += ToggleMode;
		CloseButton.Pressed += Close;
		Visible = false;
	}

	/// <summary>Opens on <paramref name="context"/>, showing the last section opened (or the first).</summary>
	public void Open(DevContext context)
	{
		Context = context;
		Visible = true;
		Rebuild(CurrentSection);
		(ModeToggle.Disabled ? CloseButton : ModeToggle).GrabFocus();
	}

	public void Close()
	{
		if (!Visible) return;
		Visible = false;
		ClearPage();
		ClearButtons();
		Context = null;
		Closed?.Invoke();
	}

	/// <summary>Shows the page of section <paramref name="id"/>. False (nothing changes) when closed, unknown, or greyed while dev mode is off.</summary>
	public bool ShowSection(string id)
	{
		if (Context is null || FindSection(id) is not { } section) return false;
		if (section.NeedsDevMode && !DevMode.On) return false;
		ClearPage();
		CurrentSection = id;
		Page = section.Build(Context);
		Page.Name = "Page_" + id;
		_holder.AddChild(Page);
		foreach (var (sid, b) in _buttons) b.ButtonPressed = sid == id;
		return true;
	}

	private void Rebuild(string? want)
	{
		ClearPage();
		ClearButtons();
		foreach (var s in _sections)
		{
			var b = new Button { Name = "Section_" + s.Id, Text = s.Title, ToggleMode = true, CustomMinimumSize = new Vector2(260, 56),
				Disabled = s.NeedsDevMode && !DevMode.On };
			b.AddThemeFontSizeOverride("font_size", 26);
			string id = s.Id;
			b.Pressed += () => ShowSection(id);
			_list.AddChild(b);
			_buttons[id] = b;
		}
		RefreshHeader();
		// Focus stays inside the menu: an explicit ring, so arrows and Tab cannot reach the screen underneath.
		var ring = new List<Control> { ModeToggle };
		foreach (var s in _sections) ring.Add(_buttons[s.Id]);
		ring.Add(CloseButton);
		MenuInput.WrapVertical(ring.ToArray());		if (want != null && ShowSection(want)) return;
		foreach (var s in _sections) if (ShowSection(s.Id)) return;
	}

	private void ClearButtons()
	{
		foreach (var b in _buttons.Values) { _list.RemoveChild(b); b.QueueFree(); }
		_buttons.Clear();
	}

	private void ClearPage()
	{
		if (Page != null && IsInstanceValid(Page)) { _holder.RemoveChild(Page); Page.QueueFree(); }
		Page = null;
	}

	/// <summary>Why the on/off toggle is greyed; null when it can be pressed (available, and on the title).</summary>
	public string? ModeToggleRefusal => !DevMode.Available ? "Dev mode is not available in this build (launch with --dev)."
		: Context?.Place != DevPlace.Title ? "Switch dev mode on or off from the title screen (it changes the save slot)." : null;

	private void ToggleMode()
	{
		if (ModeToggleRefusal != null) return;
		DevMode.SetOn(!DevMode.On);
		Rebuild(CurrentSection);
	}

	private void RefreshHeader()
	{
		ModeToggle.Text = DevMode.On ? "Dev mode: ON" : "Dev mode: OFF";
		string? refusal = ModeToggleRefusal;
		ModeToggle.Disabled = refusal != null;
		_modeNote.Text = refusal ?? (DevMode.On ? "Saving to the dev slot; the player's save is not read or written." : "Off: the game saves to the player's save as usual.");
		_place.Text = Context switch
		{
			null => "",
			{ Place: DevPlace.Fight } => "In a fight (paused): view only. " + DevContext.MidFightReason,
			{ Place: DevPlace.Map } => "On the map, between nodes: run changes allowed.",
			_ => "On the title: no run in progress.",
		};
	}

	public override void _Input(InputEvent e)
	{
		if (!Visible) return;
		bool menuKey = e is InputEventKey { Pressed: true, Echo: false } k && k.PhysicalKeycode == DevMode.MenuKey;
		if (menuKey || MenuInput.Back(e) || MenuInput.Pause(e)) { Close(); GetViewport().SetInputAsHandled(); }
	}
}
