using System.Collections.Generic;
using Godot;

namespace YokaiFighters.Ui;

/// <summary>How bright a route is drawn: walked (both ends done), open (leads out of Ryo to a reachable node) or idle.</summary>
public enum RouteKind { Idle, Open, Walked }

/// <summary>Draws the map's routes as lines between node edges. Lines come from <see cref="MapScreen"/>; this knows no map rules.</summary>
public partial class MapRoutes : Control
{
	readonly List<(Vector2 A, Vector2 B, RouteKind Kind)> _lines = new();
	public IReadOnlyList<(Vector2 A, Vector2 B, RouteKind Kind)> Lines => _lines;

	public void SetLines(IEnumerable<(Vector2 A, Vector2 B, RouteKind Kind)> lines)
	{
		_lines.Clear(); _lines.AddRange(lines); QueueRedraw();
	}

	public override void _Draw()
	{
		foreach (var (a, b, kind) in _lines)
		{
			Color c = kind switch { RouteKind.Walked => new Color(0.89f, 0.42f, 0.24f), RouteKind.Open => new Color(0.945f, 0.91f, 0.831f), _ => new Color(0.945f, 0.91f, 0.831f, 0.4f) };
			DrawLine(a, b, c, kind == RouteKind.Idle ? 2f : 4f, true);
		}
	}
}
