using System.Collections.Generic;
using Godot;
using YokaiFighters.Fight;

namespace YokaiFighters.Dev;

/// <summary>One existing debug key, listed on the dev menu's Debug tools page.</summary>
public sealed record DebugTool(Key Key, string Does, string Where);

/// <summary>YOK-123: the built-in sections. Follow-up tickets replace a stub by changing its line to their page.</summary>
public partial class DevMenu
{
	public const string ToolsId = "tools", MapId = "map", JumpId = "jump", RunId = "run";

	/// <summary>The debug keys that already exist (debug builds), read from their owners' constants.</summary>
	public static readonly IReadOnlyList<DebugTool> DebugTools = new DebugTool[]
	{
		new(DebugOverlay.ToggleKey, "Hitbox overlay on / off (red hit, green hurt, yellow throw, blue push)", "Fight · YOK-22"),
		new(DebugOverlay.PauseKey, "Frame-step: pause / resume the sim", "Fight · YOK-22"),
		new(DebugOverlay.StepKey, "Frame-step: one tick", "Fight · YOK-22"),
		new(DebugOverlay.SchemeKey, "P1 control scheme: Kihon / Kata", "Fight · YOK-23"),
		new(FightScene.SwapTemperamentKey, "Swap the Kitsune's temperament", "Fight · YOK-27"),
		new(FightScene.ToggleAiKey, "P2 AI on / off", "Fight · YOK-27"),
		new(FightScene.SaveReplayKey, "Save the fight's replay to user://replays (play it with tests/replay.tscn)", "Fight · YOK-49"),
		new(FightScene.DebugMenuKey, "Debug menu: Kitsune temperament, restart run", "Fight · YOK-48"),
		new(FightScene.ModelToggleKey, "Rigged models / capsules", "Fight · YOK-53"),
		new(Key.R, "Replay the fight from the start after a KO (reset)", "Fight · YOK-48"),
		new(DevMode.MenuKey, "This dev menu", "Title, map, pause"),
	};

	static DevMenu()
	{
		AddSection(ToolsId, "Debug tools", _ => ToolsPage(), needsDevMode: false);
		AddSection(MapId, "Map", ctx => new DevMapPage(ctx));
		AddSection(JumpId, "Jump", _ => StubPage("Jump", "YOK-125: jump straight to any fight or screen."));
		AddSection(RunId, "Run state", ctx => new DevRunStatePage(ctx));
	}

	/// <summary>Rows of key, what it does, where. The keys work in debug builds only, as before.</summary>
	public static Control ToolsPage()
	{
		var page = new VBoxContainer();
		page.AddThemeConstantOverride("separation", 10);
		page.AddChild(PageLabel("Existing debug keys (debug builds):", 26));
		var grid = new GridContainer { Name = "Keys", Columns = 3 };
		grid.AddThemeConstantOverride("h_separation", 24);
		grid.AddThemeConstantOverride("v_separation", 4);
		foreach (var t in DebugTools)
		{
			grid.AddChild(PageLabel(OS.GetKeycodeString(t.Key), 22, "Key_" + OS.GetKeycodeString(t.Key)));
			var does = PageLabel(t.Does, 20);
			does.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			does.CustomMinimumSize = new Vector2(480, 0);
			grid.AddChild(does);
			grid.AddChild(PageLabel(t.Where, 18));
		}
		page.AddChild(grid);
		return page;
	}

	/// <summary>A placeholder page naming the ticket that fills it.</summary>
	public static Control StubPage(string title, string text)
	{
		var page = new VBoxContainer();
		page.AddThemeConstantOverride("separation", 12);
		page.AddChild(PageLabel(title, 30));
		var body = PageLabel(text, 24, "Stub");
		body.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		page.AddChild(body);
		page.AddChild(PageLabel("Not built yet.", 22));
		return page;
	}

	public static Label PageLabel(string text, int size, string? name = null)
	{
		var l = new Label { Text = text, ThemeTypeVariation = "PaperLabel" };
		if (name != null) l.Name = name;
		l.AddThemeFontSizeOverride("font_size", size);
		return l;
	}
}
