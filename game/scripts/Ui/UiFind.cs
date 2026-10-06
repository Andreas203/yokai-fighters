using Godot;

namespace YokaiFighters.Ui;

/// <summary>Finds a scene's nodes by name wherever the designer moved them in the tree (names are the contract, not paths).</summary>
public static class UiFind
{
	public static T Get<T>(Node root, string name) where T : Node =>
		root.FindChild(name, true, false) as T ?? throw new System.InvalidOperationException($"{root.Name}: no {typeof(T).Name} named '{name}' in the scene");
}
