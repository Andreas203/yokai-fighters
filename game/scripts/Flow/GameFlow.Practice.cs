using Godot;
using YokaiFighters.Audio;
using YokaiFighters.Fight;

namespace YokaiFighters.Flow;

/// <summary>
/// YOK-197, GDD 8: Practice on the title opens the fight scene in practice mode (<see cref="FightScene.PracticeMode"/>:
/// Ryo against the dojo's training dummy). The scene is given no run session, story flags or Tanuki wins, and nothing
/// here listens for a node or a tutorial, so practice reaches no save point and never reads or writes the save file
/// (M5, AM3). The pause menu's Quit to title brings the title back.
/// </summary>
public partial class GameFlow
{
	/// <summary>The practice fight; null off <see cref="Screen.Practice"/> (it is also <see cref="Fight"/> while it runs).</summary>
	public FightScene? Practice => Router.Current == Screen.Practice ? Fight : null;

	private void BeginPractice()
	{
		var fight = GD.Load<PackedScene>(FightScenePath).Instantiate<FightScene>();
		fight.PracticeMode = true;
		fight.PlayerScheme = Router.Scheme; // the saved setting; F4 on the overlay flips it in the fight
		fight.TitleRequested += () => Router.ReturnToTitle();
		ConfigureFight?.Invoke(fight);
		Fight = fight;
		NodeScene = fight;
		MusicPlayer.Instance?.PlayBattleMusic();
		AddChild(fight);
		MoveChild(fight, 0); // under the menu layer
	}
}
