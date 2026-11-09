using System.Collections.Generic;
using System.Linq;
using Godot;

namespace YokaiFighters.Ui;

/// <summary>YOK-238: the scenery the map scatters on the paper.</summary>
public enum MapLandmarkKind { PinesWide, PineTall, Bridge, Torii, Rocks }

/// <summary>One scattered landmark: what it is and where (board coordinates).</summary>
public sealed record MapLandmark(MapLandmarkKind Kind, Rect2 Rect);

/// <summary>
/// YOK-238 (designer, 2026-11-09): the pure plan for the landmarks. Pines, rocks, a footbridge and a torii are laid along the board in a fixed
/// order of kinds; each takes a hashed height and keeps the first spot that overlaps no node box (drawn or not yet drawn, the tag and the
/// ring clearance included) and no earlier landmark. Same board and nodes, same scenery, no random state.
/// </summary>
public static class MapLandmarkPlan
{
	static readonly MapLandmarkKind[] Cycle = { MapLandmarkKind.PineTall, MapLandmarkKind.Rocks, MapLandmarkKind.PinesWide, MapLandmarkKind.Bridge, MapLandmarkKind.PineTall,
		MapLandmarkKind.Rocks, MapLandmarkKind.Torii, MapLandmarkKind.PinesWide, MapLandmarkKind.Rocks, MapLandmarkKind.PineTall, MapLandmarkKind.Bridge, MapLandmarkKind.PinesWide };

	/// <summary>Height of each kind as a share of the board height.</summary>
	public static float HeightShare(MapLandmarkKind k) => k switch
	{
		MapLandmarkKind.PineTall => 0.30f, MapLandmarkKind.PinesWide => 0.23f, MapLandmarkKind.Bridge => 0.13f, MapLandmarkKind.Torii => 0.19f, _ => 0.10f,
	};

	public static string TexturePath(MapLandmarkKind k) => "res://assets/generated/ui/map/" + k switch
	{
		MapLandmarkKind.PineTall => "landmark-pine-tall.png", MapLandmarkKind.PinesWide => "landmark-pines-wide.png", MapLandmarkKind.Bridge => "landmark-bridge.png",
		MapLandmarkKind.Torii => "landmark-torii.png", _ => "landmark-rocks.png",
	};

	/// <summary>The sprite width / height, from the plates' pixel sizes (kept here so the plan needs no texture).</summary>
	public static float Aspect(MapLandmarkKind k) => k switch
	{
		MapLandmarkKind.PineTall => 186f / 248f, MapLandmarkKind.PinesWide => 364f / 190f, MapLandmarkKind.Bridge => 228f / 116f, MapLandmarkKind.Torii => 176f / 164f, _ => 192f / 106f,
	};

	/// <param name="board">The board size.</param>
	/// <param name="avoid">Every node box (board coordinates).</param>
	/// <param name="reserve">Extra height under each box that must stay clear (the hanging "YOU ARE HERE" line).</param>
	public static List<MapLandmark> Compute(Vector2 board, IEnumerable<Rect2> avoid, float reserve = 30f)
	{
		var result = new List<MapLandmark>();
		if (board.X < 200f || board.Y < 120f) return result;
		var boxes = avoid.Select(r => new Rect2(r.Position, r.Size + new Vector2(0, reserve)).Grow(18f)).ToList();
		float x = 20f;
		for (int i = 0; x < board.X - 100f; i++)
		{
			uint h = MapLayout.Hash("lm" + i);
			var kind = Cycle[i % Cycle.Length];
			float ht = board.Y * HeightShare(kind) * (0.85f + 0.3f * MapLayout.Unit(h, 1)), wd = ht * Aspect(kind);
			for (int attempt = 0; attempt < 40; attempt++)
			{
				float bottom = board.Y * (0.30f + 0.68f * MapLayout.Unit(h, 10 + attempt));
				float left = x + (MapLayout.Unit(h, 60 + attempt) - 0.25f) * 320f;
				var r = new Rect2(left, bottom - ht, wd, ht);
				if (r.Position.X < 0 || r.End.X > board.X || r.Position.Y < 0 || r.End.Y > board.Y) continue;
				if (boxes.Any(b => b.Intersects(r)) || result.Any(l => l.Rect.Grow(6f).Intersects(r))) continue;
				result.Add(new MapLandmark(kind, r));
				break;
			}
			x += 110f + 140f * MapLayout.Unit(h, 2);
		}
		return result;
	}
}

/// <summary>
/// YOK-238: draws the landmarks and the soft sumi hills along the paper's foot, behind the routes and the nodes. It is as big as the whole
/// scroll (<see cref="Origin"/> is where the board starts inside it); it carries no information and takes no input.
/// </summary>
public partial class MapLandmarks : Control
{
	List<MapLandmark> _plan = new();
	Vector2 _origin;
	static readonly Dictionary<MapLandmarkKind, Texture2D?> Textures = new();

	/// <summary>The scenery now on the paper, in board coordinates.</summary>
	public IReadOnlyList<MapLandmark> Plan => _plan;
	public Vector2 Origin => _origin;

	public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;
	public override void _Notification(int what) { if (what == NotificationResized) QueueRedraw(); }

	public void SetPlan(IEnumerable<MapLandmark> plan, Vector2 origin) { _plan = plan.ToList(); _origin = origin; QueueRedraw(); }

	static Texture2D? Tex(MapLandmarkKind k)
	{
		if (!Textures.TryGetValue(k, out var t) || t == null) Textures[k] = t = GD.Load<Texture2D>(MapLandmarkPlan.TexturePath(k));
		return t;
	}

	public override void _Draw()
	{
		DrawHills();
		foreach (var l in _plan.OrderBy(l => l.Rect.End.Y))
			if (Tex(l.Kind) is { } tex) DrawTextureRect(tex, new Rect2(l.Rect.Position + _origin, l.Rect.Size), false, new Color(1f, 1f, 1f, 0.92f));
	}

	/// <summary>Three layers of soft sumi hills along the paper's foot, low contrast, behind the scenery.</summary>
	void DrawHills()
	{
		float x0 = 72f, x1 = Size.X - 72f, floor = Size.Y - 52f, h = Size.Y - 104f;
		if (x1 - x0 < 100f || h < 60f) return;
		var tints = new[] { new Color(0.25f, 0.33f, 0.34f, 0.12f), new Color(0.20f, 0.27f, 0.28f, 0.10f), new Color(0.15f, 0.22f, 0.22f, 0.10f) };
		for (int layer = 0; layer < 3; layer++)
		{
			float baseH = h * (0.07f + 0.04f * (2 - layer)), amp = h * (0.08f + 0.03f * layer);
			var pts = new List<Vector2>();
			for (float x = x0; x < x1 + 40f; x += 40f)
			{
				float xx = Mathf.Min(x, x1 - 1f);
				if (pts.Count > 0 && xx <= pts[^1].X) break;
				pts.Add(new Vector2(xx, floor - baseH - amp * Smooth(layer * 101 + 3, xx - x0, 260f + 120f * layer) * (0.5f + 0.5f * Smooth(layer * 53 + 1, xx, 900f))));
			}
			pts.Add(new Vector2(pts[^1].X, floor)); pts.Add(new Vector2(x0, floor));
			DrawColoredPolygon(pts.ToArray(), tints[layer]);
		}
	}

	static float Smooth(int seed, float x, float period)
	{
		float f = x / period; int i = Mathf.FloorToInt(f); float t = f - i; t = t * t * (3 - 2 * t);
		return Mathf.Lerp(MapLayout.Unit((uint)(seed + i * 977), 1), MapLayout.Unit((uint)(seed + (i + 1) * 977), 1), t);
	}
}
