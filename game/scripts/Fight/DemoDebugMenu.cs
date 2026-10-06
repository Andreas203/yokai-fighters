using System;
using Godot;
using YokaiFighters.Ui;

namespace YokaiFighters.Fight;

/// <summary>
/// YOK-48 debug menu (<c>scenes/ui/debug_menu.tscn</c>, debug builds only, F9): pick the Kitsune's temperament (one
/// button per loaded profile, copied from the scene's template button), restart the run from the first fight, or
/// resume. The flow pauses the fight while it is open.
/// </summary>
public partial class DemoDebugMenu : Control
{
	public FightScene Scene { get; private set; } = null!;
	private Label _current = null!;
	private Button _resume = null!;

	public override void _Ready() => Visible = false;

	/// <summary>Wires the menu to the fight: one temperament button per loaded profile, Restart run, Resume.</summary>
	public void Bind(FightScene scene)
	{
		Scene = scene;
		_current = UiFind.Get<Label>(this, "Current");
		_resume = UiFind.Get<Button>(this, "Resume");
		var template = UiFind.Get<Button>(this, "TemperamentTemplate");
		foreach (var p in scene.Profiles)
		{
			string t = p.Temperament;
			var b = (Button)template.Duplicate();
			b.Name = "Temperament_" + t;
			b.Text = $"Kitsune: {t}";
			b.Visible = true;
			b.Pressed += () => { Scene.SetTemperament(t); Refresh(); };
			template.GetParent().AddChild(b);
			template.GetParent().MoveChild(b, template.GetIndex());
		}
		template.QueueFree();
		UiFind.Get<Button>(this, "RestartRun").Pressed += Scene.RestartRun; // also closes the menu
		_resume.Pressed += () => { if (Visible) Scene.ToggleDebugMenu(); };
	}

	public void Open() { Refresh(); Visible = true; _resume.GrabFocus(); }
	public void Close() => Visible = false;

	private void Refresh() => _current.Text = $"Kitsune temperament: {Scene.Temperament}";
}
