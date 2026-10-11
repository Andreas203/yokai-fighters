using Godot;

namespace YokaiFighters.Ui;

/// <summary>Presentation-only id → plate manifest. Unknown abilities keep their live letter badge.</summary>
public static class RewardArt
{
	public static Texture2D? ForId(string id)
	{
		var plates = GD.Load<Resource>("res://ui/reward_art.tres").GetMeta("plates").AsGodotDictionary<string, Texture2D>();
		return plates.TryGetValue(id, out var texture) ? texture : null;
	}
}
