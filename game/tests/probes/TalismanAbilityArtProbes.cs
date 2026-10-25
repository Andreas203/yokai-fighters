using Godot;
using YokaiFighters.Fight;
using YokaiFighters.Sim;
using YokaiFighters.Ui;

namespace YokaiFighters.Tests;

/// <summary>YOK-140 test-breaker probes: the HUD copy talisman shows AbilityArt.ForId(special id).</summary>
public static class TalismanAbilityArtProbes
{
	sealed class View : IHudRunView
	{
		public bool CopyLocked { get; set; }
		public CopyTarget? Copy { get; set; }
		public bool InTanukiFight { get; set; } = true;
		public bool ElderFight { get; set; }
	}
	static FightHud Load(Node runner)
	{
		var hud = GD.Load<PackedScene>("res://scenes/ui/hud.tscn").Instantiate<FightHud>();
		runner.AddChild(hud);
		return hud;
	}
	static CopyTarget T(string id, string name, int lv) =>
		new(SpecialSlot.B, new SpecialData { Id = id, Name = name, Slot = SpecialSlot.B, Level1Json = "{}" }, lv);
	static TextureRect Icon(FightHud h) => UiFind.Get<TextureRect>(h.CopyTarget, "Icon");

	// YOK-79 AbilityArt contract: unknown and empty ids get the fallback, no crash; the talisman never shows a blank icon for a target.
	[Test]
	public static void UnknownAndEmptyId_ShowFallbackIcon(Node runner)
	{
		var hud = Load(runner);
		try
		{
			var v = new View(); hud.RunView = v;
			foreach (var id in new[] { "no-such-move", "" })
			{
				v.Copy = T(id, "Mystery", 1); hud.Refresh(new Match());
				Assert.True(Icon(hud).Visible, "icon visible for id '" + id + "'");
				Assert.True(Icon(hud).Texture == AbilityArt.Fallback, "fallback for id '" + id + "'");
			}
		}
		finally { hud.Free(); }
	}

	// T4/A4: the copy shows the special's base icon at Lv 1, 2 and 3 (art is per id, never per level).
	[Test]
	public static void AllLevels_ShowSameBaseIcon(Node runner)
	{
		var hud = Load(runner);
		try
		{
			var v = new View(); hud.RunView = v;
			for (int lv = 1; lv <= 3; lv++)
			{
				v.Copy = T("foxfire", "Foxfire", lv); hud.Refresh(new Match());
				Assert.True(Icon(hud).Texture == AbilityArt.ForId("foxfire"), "Lv " + lv);
				Assert.True(Icon(hud).Texture != AbilityArt.Fallback, "real art at Lv " + lv);
			}
		}
		finally { hud.Free(); }
	}

	// T6: the target changing (different special) updates the icon; id not display name is the key.
	[Test]
	public static void TargetChange_UpdatesIcon_KeyedById(Node runner)
	{
		var hud = Load(runner);
		try
		{
			var v = new View(); hud.RunView = v;
			v.Copy = T("spirit-wave", "Foxfire", 1); hud.Refresh(new Match()); // name belongs to another move
			Assert.True(Icon(hud).Texture == AbilityArt.ForId("spirit-wave"), "id wins over display name");
			v.Copy = T("foxfire", "Spirit Wave", 1); hud.Refresh(new Match());
			Assert.True(Icon(hud).Texture == AbilityArt.ForId("foxfire"), "icon follows the new id");
			Assert.True(AbilityArt.ForId("foxfire") != AbilityArt.ForId("spirit-wave"), "fixture sanity: two arts differ");
			v.Copy = T("spirit-wave", "X", 2); v.CopyLocked = true; hud.Refresh(new Match());
			Assert.True(Icon(hud).Texture == AbilityArt.ForId("spirit-wave"), "locked change updates icon");
		}
		finally { hud.Free(); }
	}

	// AM6/T6: hidden in yokai and elder fights; no icon leaks when it comes back with a null target; null target shows no icon.
	[Test]
	public static void HiddenStates_AndNullTarget(Node runner)
	{
		var hud = Load(runner);
		try
		{
			var v = new View { Copy = null }; hud.RunView = v; hud.Refresh(new Match());
			Assert.True(hud.CopyTarget.Visible, "tanuki fight node visible");
			Assert.True(!Icon(hud).Visible && Icon(hud).Texture == null, "null target: no icon");
			v.Copy = T("foxfire", "Foxfire", 1); hud.Refresh(new Match());
			Assert.True(Icon(hud).Texture == AbilityArt.ForId("foxfire"), "shown");
			v.InTanukiFight = false; hud.Refresh(new Match());
			Assert.True(!hud.CopyTarget.Visible, "yokai: hidden");
			v.ElderFight = true; hud.Refresh(new Match());
			Assert.True(!hud.CopyTarget.Visible, "elder: hidden");
			v.ElderFight = false; v.InTanukiFight = true; v.Copy = null; hud.Refresh(new Match());
			Assert.True(Icon(hud).Texture == null && !Icon(hud).Visible, "no stale icon after re-entry with null target");
		}
		finally { hud.Free(); }
	}
}
