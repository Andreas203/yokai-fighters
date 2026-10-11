using System.Collections.Generic;
using System.Linq;

namespace YokaiFighters.Sim;

/// <summary>
/// YOK-76: what the map needs to know about a node type. Behaviour (which scene runs, what the node gives) lives
/// with the systems that own it and is looked up by <see cref="Id"/>; the map only validates and walks.
/// </summary>
public interface INodeType
{
	string Id { get; }
	/// <summary>A fight happens on this node, so its data must name the yokai (R2).</summary>
	bool Fight { get; }
	/// <summary>Registered so maps load, but its behaviour is not built yet.</summary>
	bool Placeholder { get; }
	/// <summary>Load-time check of one node of this type: null = fine, else what is wrong with it.</summary>
	string? Check(MapNodeData node);
}

/// <summary>Resolves a node's open <c>type</c> string. A map with a type that is not registered fails to load.</summary>
public interface INodeTypeRegistry
{
	INodeType? Find(string type);
	/// <summary>Registered type ids, in registration order.</summary>
	IReadOnlyList<string> Types { get; }
}

/// <summary>The plain node type: an id plus the flags above. <c>elder</c> must match <see cref="Elder"/> when set.</summary>
public sealed record NodeType(string Id, bool Fight = false, bool Placeholder = false, bool? Elder = null) : INodeType
{
	public string? Check(MapNodeData node)
	{
		if (Fight && string.IsNullOrEmpty(node.Yokai)) return $"a '{Id}' node needs a yokai (R2)";
		if (Elder is { } e && node.Elder != e) return $"a '{Id}' node must have elder = {(e ? "true" : "false")} (Y3)";
		return null;
	}
}

/// <summary>
/// YOK-76: the node types the game knows. Adding one = <see cref="Register"/> (no edit to <see cref="RunMap"/>
/// or <see cref="MapGraph"/>). <see cref="Default"/> holds duel, elder and boss, plus merchant and dojo as
/// placeholders so the R1 shape loads before their behaviour exists.
/// </summary>
public sealed class NodeTypeRegistry : INodeTypeRegistry
{
	public const string Duel = "duel", ElderDuel = "elder", Boss = "boss", Merchant = "merchant", Dojo = "dojo";

	readonly List<INodeType> _types = new();

	public IReadOnlyList<string> Types => _types.Select(t => t.Id).ToArray();

	public INodeType? Find(string type) => _types.FirstOrDefault(t => t.Id == type);

	/// <summary>Adds a type, or replaces the one registered under the same id. Returns this, for chaining.</summary>
	public NodeTypeRegistry Register(INodeType type)
	{
		int i = _types.FindIndex(t => t.Id == type.Id);
		if (i >= 0) _types[i] = type; else _types.Add(type);
		return this;
	}

	/// <summary>A fresh registry with the built-in types (each caller may add its own).</summary>
	public static NodeTypeRegistry Default() => new NodeTypeRegistry()
		.Register(new NodeType(Duel, Fight: true, Elder: false))
		.Register(new NodeType(ElderDuel, Fight: true, Elder: true))
		.Register(new NodeType(Boss, Fight: true, Elder: false))
		.Register(new NodeType(Merchant, Placeholder: true, Elder: false))
		.Register(new NodeType(Dojo, Placeholder: true, Elder: false));
}
