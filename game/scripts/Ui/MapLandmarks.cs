using System.Collections.Generic;
using System.Linq;
using Godot;

namespace YokaiFighters.Ui;

/// <summary>YOK-238: the scenery the map puts along its routes.</summary>
public enum MapLandmarkKind { PinesWide, PineTall, Bridge, Torii, Rocks }

/// <summary>
/// One landmark: what it is and where (board coordinates). A bridge carries the <see cref="Stream"/> it spans (a polyline across the route at
/// <see cref="OnPath"/>); a torii stands on the route at <see cref="OnPath"/>; pines and rocks sit at the route's edge.
/// </summary>
public sealed record MapLandmark(MapLandmarkKind Kind, Rect2 Rect)
{
	public Vector2[]? Stream { get; init; }
	public Vector2 OnPath { get; init; }
	/// <summary>Index of the route (in the list given to <see cref="MapLandmarkPlan.Compute"/>) the landmark belongs to; it is shown while that route is.</summary>
	public int Route { get; init; }
	/// <summary>True for the landmarks drawn over the route stroke (the deck of a bridge, the posts of a torii).</summary>
	public bool InFront => Kind is MapLandmarkKind.Bridge or MapLandmarkKind.Torii;
}

/// <summary>
/// YOK-238 (designer review 2026-11-09): the plan for the landmarks, pure and deterministic. They are part of the path, not scatter: along each route
/// (in the order given) a landmark of the next kind is placed where it fits. A footbridge sits on a flat stretch with a short stream under it that the
/// route crosses, a torii stands on the route, pines and rocks hug it at its edge. Never on a node box or its tag, on another landmark, or on another
/// route's stroke. The plan reads only node boxes and route centre lines, never reveal state, so scenery does not change as nodes are revealed.
/// </summary>
public static class MapLandmarkPlan
{
	static readonly MapLandmarkKind[] Cycle = { MapLandmarkKind.Bridge, MapLandmarkKind.PineTall, MapLandmarkKind.Torii, MapLandmarkKind.Rocks, MapLandmarkKind.PinesWide,
		MapLandmarkKind.Rocks, MapLandmarkKind.PineTall, MapLandmarkKind.Bridge, MapLandmarkKind.PinesWide, MapLandmarkKind.Rocks, MapLandmarkKind.Torii, MapLandmarkKind.PineTall };

	/// <summary>Height of each kind as a share of the board height.</summary>
	public static float HeightShare(MapLandmarkKind k) => k switch
	{
		MapLandmarkKind.PineTall => 0.27f, MapLandmarkKind.PinesWide => 0.20f, MapLandmarkKind.Bridge => 0.14f, MapLandmarkKind.Torii => 0.19f, _ => 0.095f,
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

	/// <summary>How far down its sprite the route runs: a bridge's deck, a torii's feet.</summary>
	static float PathLevel(MapLandmarkKind k) => k == MapLandmarkKind.Bridge ? 0.44f : 0.86f;

	/// <param name="board">The board size.</param>
	/// <param name="avoid">Every node box (board coordinates).</param>
	/// <param name="paths">The centre line of every route (<see cref="MapRouteGeometry"/>), in board coordinates; null = none, so no landmarks.</param>
	/// <param name="reserve">Extra height under each box that must stay clear (the hanging "YOU ARE HERE" line).</param>
	public static List<MapLandmark> Compute(Vector2 board, IEnumerable<Rect2> avoid, IReadOnlyList<Vector2[]>? paths = null, float reserve = 30f)
	{
		var result = new List<MapLandmark>();
		if (paths == null || board.X < 200f || board.Y < 120f) return result;
		var boxes = avoid.Select(r => new Rect2(r.Position, r.Size + new Vector2(0, reserve)).Grow(16f)).ToList();
		var bounds = new Rect2(Vector2.Zero, board);
		var taken = new List<Rect2>();
		int next = 0;
		for (int ri = 0; ri < paths.Count; ri++)
		{
			var path = paths[ri];
			float len = Length(path);
			if (len < 150f) continue;
			int slots = len > 330f ? 2 : 1;
			for (int slot = 0; slot < slots; slot++)
			{
				bool placed = false;
				for (int tryKind = 0; tryKind < Cycle.Length && !placed; tryKind++)
				{
					var kind = Cycle[(next + tryKind) % Cycle.Length];
					uint h = MapLayout.Hash("lm" + ri + ":" + slot + ":" + (int)kind);
					for (int attempt = 0; attempt < 28 && !placed; attempt++)
					{
						float f = 0.18f + 0.64f * MapLayout.Unit(h, 5 + attempt);
						var lm = Place(kind, path, f, ri, h, attempt, paths, boxes, taken, bounds);
						if (lm == null) continue;
						result.Add(lm); taken.Add(lm.Rect.Grow(8f));
						if (lm.Stream != null) taken.Add(Bounds(lm.Stream).Grow(14f));
						placed = true; next = (next + tryKind + 1) % Cycle.Length;
					}
				}
			}
		}
		return result;
	}

	static float Length(Vector2[] p) { float l = 0; for (int i = 0; i + 1 < p.Length; i++) l += p[i].DistanceTo(p[i + 1]); return l; }

	static Rect2 Bounds(Vector2[] pts)
	{
		float x0 = pts.Min(p => p.X), x1 = pts.Max(p => p.X), y0 = pts.Min(p => p.Y), y1 = pts.Max(p => p.Y);
		return new Rect2(x0, y0, x1 - x0, y1 - y0);
	}

	static int IndexAt(Vector2[] p, float f)
	{
		float want = f * Length(p), acc = 0;
		for (int i = 0; i + 1 < p.Length; i++) { acc += p[i].DistanceTo(p[i + 1]); if (acc >= want) return i; }
		return p.Length - 2;
	}

	static bool NearPath(Vector2[] path, Vector2 q, float d)
	{
		float d2 = d * d;
		for (int i = 0; i < path.Length; i++) if (path[i].DistanceSquaredTo(q) <= d2) return true;
		return false;
	}

	/// <summary>True when some stroke other than route <paramref name="own"/> (and not running together with it) passes through <paramref name="r"/>; with <paramref name="includeOwn"/> its own stroke counts too.</summary>
	static bool HitsStroke(Rect2 r, IReadOnlyList<Vector2[]> paths, int own, bool includeOwn)
	{
		for (int k = 0; k < paths.Count; k++)
		{
			bool mine = k == own;
			if (mine && !includeOwn) continue;
			foreach (var q in paths[k])
			{
				if (!r.HasPoint(q)) continue;
				if (!mine && NearPath(paths[own], q, 10f)) continue; // a shared trunk is the same stroke
				return true;
			}
		}
		return false;
	}

	static MapLandmark? Place(MapLandmarkKind kind, Vector2[] path, float f, int ri, uint h, int attempt, IReadOnlyList<Vector2[]> paths, List<Rect2> boxes, List<Rect2> taken, Rect2 bounds)
	{
		int i = IndexAt(path, f);
		int a = Mathf.Max(0, i - 4), b = Mathf.Min(path.Length - 1, i + 4);
		var d = path[b] - path[a];
		if (d.X <= 1f) return null;
		float slope = Mathf.Abs(d.Y / d.X);
		var p = path[i];
		float ht = bounds.Size.Y * HeightShare(kind) * (0.9f + 0.2f * MapLayout.Unit(h, 2)), wd = ht * Aspect(kind);
		Rect2 rect; Vector2[]? stream = null;
		bool onPath = kind is MapLandmarkKind.Bridge or MapLandmarkKind.Torii;
		if (onPath)
		{
			if (slope > (kind == MapLandmarkKind.Bridge ? 0.20f : 0.32f)) return null;
			rect = new Rect2(p.X - wd / 2f, p.Y - PathLevel(kind) * ht, wd, ht);
			if (kind == MapLandmarkKind.Bridge)
			{
				foreach (float sh in new[] { 0.36f, 0.26f, 0.18f })
				{
					float half = bounds.Size.Y * sh / 2f;
					var pts = new Vector2[10];
					for (int k = 0; k < pts.Length; k++)
					{
						float t = k / (float)(pts.Length - 1);
						pts[k] = new Vector2(p.X + 4f + Mathf.Sin(t * Mathf.Tau) * 9f * (MapLayout.Unit(h, 3) > 0.5f ? 1f : -1f), p.Y - half + t * 2f * half);
					}
					var sb = Bounds(pts).Grow(15f);
					if (!bounds.Encloses(sb) || boxes.Any(x => x.Intersects(sb)) || taken.Any(x => x.Intersects(sb))) continue;
					if (HitsStroke(sb, paths, ri, false)) continue;
					stream = pts; break;
				}
				if (stream == null) return null;
			}
		}
		else
		{
			// at the path's edge: step off along the normal by the stroke's half width plus a gap, to the side the hash picks, hugging bends
			var n = new Vector2(-d.Y, d.X).Normalized() * (MapLayout.Unit(h, 7 + attempt) > 0.5f ? 1f : -1f);
			float reach = Mathf.Abs(n.X) * wd / 2f + Mathf.Abs(n.Y) * ht / 2f + 26f;
			var c = p + n * reach;
			rect = new Rect2(c.X - wd / 2f, c.Y - ht / 2f, wd, ht);
		}
		if (!bounds.Encloses(rect)) return null;
		if (boxes.Any(x => x.Intersects(rect)) || taken.Any(x => x.Intersects(rect))) return null;
		if (HitsStroke(rect.Grow(onPath ? 0f : 10f), paths, ri, !onPath)) return null;
		return new MapLandmark(kind, rect) { Stream = stream, OnPath = p, Route = ri };
	}
}

/// <summary>
/// YOK-238: draws the landmarks and the soft sumi hills. The back layer (behind the routes) holds the hills, the streams, pines and rocks; the front
/// layer (<see cref="Front"/>, above the routes) holds the bridge decks and torii, so the stroke runs under the deck and through the posts.
/// <see cref="Origin"/> is where the board starts inside the control. Carries no information and takes no input.
/// </summary>
public partial class MapLandmarks : Control
{
	List<MapLandmark> _plan = new();
	Vector2 _origin;
	static readonly Dictionary<MapLandmarkKind, Texture2D?> Textures = new();

	/// <summary>True for the layer above the routes.</summary>
	[Export] public bool Front { get; set; }
	/// <summary>The scenery now on the paper, in board coordinates.</summary>
	public IReadOnlyList<MapLandmark> Plan => _plan;
	/// <summary>The landmarks drawn now: those of routes that are on the paper (not those of a row Ryo has left).</summary>
	public IEnumerable<MapLandmark> Shown => _plan.Where(l => _routes == null || _routes.Contains(l.Route));
	HashSet<int>? _routes;
	public Vector2 Origin => _origin;

	public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;
	public override void _Notification(int what) { if (what == NotificationResized) QueueRedraw(); }

	public void SetPlan(IEnumerable<MapLandmark> plan, Vector2 origin) { _plan = plan.ToList(); _origin = origin; QueueRedraw(); }

	/// <summary>Show only the landmarks of these routes (null = all). Scenery of a route that is no longer on the paper goes with it.</summary>
	public void SetRoutes(IEnumerable<int>? routes)
	{
		var set = routes?.ToHashSet();
		if (set == null && _routes == null || set != null && _routes != null && set.SetEquals(_routes)) return;
		_routes = set; QueueRedraw();
	}

	static Texture2D? Tex(MapLandmarkKind k)
	{
		if (!Textures.TryGetValue(k, out var t) || t == null) Textures[k] = t = GD.Load<Texture2D>(MapLandmarkPlan.TexturePath(k));
		return t;
	}

	public override void _Draw()
	{
		if (!Front)
		{
			DrawHills();
			foreach (var l in Shown.Where(l => l.Stream != null)) DrawStream(l.Stream!);
		}
		foreach (var l in Shown.Where(l => l.InFront == Front).OrderBy(l => l.Rect.End.Y))
			if (Tex(l.Kind) is { } tex) DrawTextureRect(tex, new Rect2(l.Rect.Position + _origin, l.Rect.Size), false, new Color(1f, 1f, 1f, 0.95f));
	}

	/// <summary>A short stream: a tapered blue-grey ribbon with a pale highlight, under a bridge.</summary>
	void DrawStream(Vector2[] pts)
	{
		for (int i = 0; i + 1 < pts.Length; i++)
		{
			float t = (i + 0.5f) / (pts.Length - 1), w = 16f + 28f * Mathf.Sin(Mathf.Pi * t);
			var a = pts[i] + _origin; var b = pts[i + 1] + _origin;
			DrawLine(a, b, new Color(0.40f, 0.52f, 0.60f, 0.55f), w + 5f, true);
			DrawLine(a, b, new Color(0.66f, 0.77f, 0.82f, 0.95f), w, true);
			DrawLine(a + new Vector2(-w * 0.18f, 0), b + new Vector2(-w * 0.18f, 0), new Color(0.92f, 0.96f, 0.97f, 0.7f), Mathf.Max(2f, w * 0.14f), true);
		}
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
