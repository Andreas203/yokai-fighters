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


	/// <summary>The width of one screen of the map as the plan reckons it: about three columns (three board heights).</summary>
	public static float ScreenWidth(Vector2 board) => board.Y * 3f;

	/// <summary>At most this many pines and rocks per screen width, and never one kind twice within it.</summary>
	public const int MaxSmallPerScreen = 4;

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
		float screen = ScreenWidth(board);
		int next = 0;
		for (int ri = 0; ri < paths.Count; ri++)
		{
			var path = paths[ri];
			if (Length(path) < 150f) continue;
			bool placed = false;
			for (int tryKind = 0; tryKind < Cycle.Length && !placed; tryKind++)
			{
				var kind = Cycle[(next + tryKind) % Cycle.Length];
				// never the same kind twice within a screen width, and a limited number of small items per screen
				uint h = MapLayout.Hash("lm" + ri + ":" + (int)kind);
				for (int attempt = 0; attempt < 40 && !placed; attempt++)
				{
					float f = 0.12f + 0.76f * MapLayout.Unit(h, 5 + attempt);
					var lm = Place(kind, path, f, ri, h, attempt, paths, boxes, taken, bounds);
					if (lm == null) continue;
					float cx = lm.Rect.GetCenter().X;
					var near = result.Where(o => Mathf.Abs(o.Rect.GetCenter().X - cx) < screen).ToList();
					if (near.Any(o => o.Kind == kind)) continue;
					if (!lm.InFront && kind != MapLandmarkKind.Bridge && near.Count(o => !o.InFront) >= MaxSmallPerScreen) continue;
					result.Add(lm); taken.Add(lm.Rect.Grow(8f));
					if (lm.Stream != null) taken.Add(Bounds(lm.Stream).Grow(14f));
					placed = true; next = (next + tryKind + 1) % Cycle.Length;
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

	/// <summary>The part of the sprite's height at which the route passes: a bridge's deck, the lower third of a torii's posts.</summary>
	public const float BridgeDeck = 0.44f, ToriiCrossing = 0.80f;
	/// <summary>The torii's posts stand this share of its width in from each side (the inner faces of the gate).</summary>
	public const float ToriiPostInset = 0.22f;
	/// <summary>How far a stream reaches past the bridge's rect above and below (px at 1080p, scaled by board height).</summary>
	static readonly float[] StreamReach = { 0.09f, 0.075f, 0.065f };

	/// <summary>Degrees the route turns between the stretch before and the stretch after index <paramref name="i"/>, over <paramref name="k"/> samples each side.</summary>
	static float Bend(Vector2[] p, int i, int k)
	{
		int a = Mathf.Max(0, i - k), b = Mathf.Min(p.Length - 1, i + k);
		var d1 = p[i] - p[a]; var d2 = p[b] - p[i];
		return d1.Length() < 1f || d2.Length() < 1f ? 0f : Mathf.RadToDeg(Mathf.Abs(d1.AngleTo(d2)));
	}

	static MapLandmark? Place(MapLandmarkKind kind, Vector2[] path, float f, int ri, uint h, int attempt, IReadOnlyList<Vector2[]> paths, List<Rect2> boxes, List<Rect2> taken, Rect2 bounds)
	{
		int i = IndexAt(path, f);
		int a = Mathf.Max(0, i - 5), b = Mathf.Min(path.Length - 1, i + 5);
		var d = path[b] - path[a];
		if (d.X <= 1f) return null;
		float slope = Mathf.Abs(d.Y / d.X);
		var p = kind == MapLandmarkKind.Torii ? (path[i] + path[Mathf.Min(path.Length - 1, i + 1)]) / 2f : path[i]; // a torii is centred between two samples so the route crosses its post line cleanly
		float ht = bounds.Size.Y * HeightShare(kind) * (0.9f + 0.2f * MapLayout.Unit(h, 2)), wd = ht * Aspect(kind);
		Rect2 rect; Vector2[]? stream = null;
		bool onPath = kind is MapLandmarkKind.Bridge or MapLandmarkKind.Torii;
		if (onPath)
		{
			// a fairly straight stretch: the bridge on a level one, the torii on a gently sloping one, so the route crosses the line between its posts
			if (Bend(path, i, 5) > 9f) return null;
			if (kind == MapLandmarkKind.Bridge ? slope > 0.2f : slope < 0.12f || slope > 0.9f) return null;
			float level = kind == MapLandmarkKind.Bridge ? BridgeDeck : ToriiCrossing;
			rect = new Rect2(p.X - wd / 2f, p.Y - level * ht, wd, ht);
			if (kind == MapLandmarkKind.Bridge)
			{
				foreach (float reach in StreamReach)
				{
					float up = BridgeDeck * ht + reach * bounds.Size.Y, down = (1f - BridgeDeck) * ht + reach * bounds.Size.Y;
					var pts = new Vector2[11];
					float sgn = MapLayout.Unit(h, 3) > 0.5f ? 1f : -1f;
					for (int k = 0; k < pts.Length; k++)
					{
						float t = k / (float)(pts.Length - 1);
						pts[k] = new Vector2(p.X + 4f + Mathf.Sin(t * Mathf.Tau) * 10f * sgn, p.Y - up + t * (up + down));
					}
					var sb = Bounds(pts).Grow(20f);
					if (!bounds.Encloses(sb) || boxes.Any(x => x.Intersects(sb)) || taken.Any(x => x.Intersects(sb))) continue;
					if (HitsStroke(sb, paths, ri, false)) continue;
					stream = pts; break;
				}
				if (stream == null) return null;
			}
		}
		else
		{
			// beside a bend, its nearest edge about 18 px off the stroke, to the side the hash picks
			if (Bend(path, i, 6) < 10f) return null;
			var n = new Vector2(-d.Y, d.X).Normalized() * (MapLayout.Unit(h, 7 + attempt) > 0.5f ? 1f : -1f);
			float reach = Mathf.Abs(n.X) * wd / 2f + Mathf.Abs(n.Y) * ht / 2f + 18f;
			var c = p + n * reach;
			rect = new Rect2(c.X - wd / 2f, c.Y - ht / 2f, wd, ht);
		}
		// a landmark belongs to one route: never at a spot another stroke also runs through (a shared trunk), so it cannot outlive its route on a trunk still drawn
		for (int k = 0; k < paths.Count; k++) if (k != ri && (NearPath(paths[k], p, 16f) || NearPath(paths[k], rect.GetCenter(), 16f))) return null;
		if (!bounds.Encloses(rect)) return null;
		if (boxes.Any(x => x.Intersects(rect)) || taken.Any(x => x.Intersects(rect))) return null;
		if (HitsStroke(rect.Grow(onPath ? 0f : 10f), paths, ri, !onPath)) return null;
		if (!onPath && DistanceToStroke(rect, paths) > 30f) return null;
		return new MapLandmark(kind, rect) { Stream = stream, OnPath = p, Route = ri };
	}

	/// <summary>The distance from <paramref name="r"/> to the nearest sample of any route's stroke.</summary>
	public static float DistanceToStroke(Rect2 r, IEnumerable<Vector2[]> paths)
	{
		float best = float.MaxValue;
		foreach (var path in paths)
			foreach (var q in path)
			{
				float dx = Mathf.Max(Mathf.Max(r.Position.X - q.X, 0f), q.X - r.End.X), dy = Mathf.Max(Mathf.Max(r.Position.Y - q.Y, 0f), q.Y - r.End.Y);
				best = Mathf.Min(best, Mathf.Sqrt(dx * dx + dy * dy));
			}
		return best;
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
	/// <summary>The landmarks drawn now: those of routes that are on the paper, plus those still fading out with a route that was just left behind.</summary>
	public IEnumerable<MapLandmark> Shown => _plan.Where(l => Alpha(l.Route) > 0f);
	HashSet<int>? _routes;
	readonly Dictionary<int, float> _fading = new(); // route -> alpha while its scenery fades out (as the nodes beside it do)

	float Alpha(int route) => _routes == null || _routes.Contains(route) ? 1f : _fading.GetValueOrDefault(route, 0f);

	public override void _Ready() { MouseFilter = MouseFilterEnum.Ignore; SetProcess(false); }
	public override void _Notification(int what) { if (what == NotificationResized) QueueRedraw(); }

	public void SetPlan(IEnumerable<MapLandmark> plan, Vector2 origin) { _plan = plan.ToList(); _origin = origin; _fading.Clear(); QueueRedraw(); }

	/// <summary>
	/// Show only the landmarks of these routes (null = all). Scenery of a route that is left behind fades out over a quarter second, like the nodes next to it,
	/// when <paramref name="animate"/>; else it goes at once.
	/// </summary>
	public void SetRoutes(IEnumerable<int>? routes, bool animate = false)
	{
		var set = routes?.ToHashSet();
		if (set == null && _routes == null || set != null && _routes != null && set.SetEquals(_routes)) return;
		if (animate && _routes != null && set != null)
			foreach (int r in _routes.Where(r => !set.Contains(r)).ToList()) if (_plan.Any(l => l.Route == r)) _fading[r] = 1f;
		else _fading.Clear();
		_routes = set; SetProcess(_fading.Count > 0); QueueRedraw();
	}

	/// <summary>Ends every fade at once (a key press does this, with the other presentation).</summary>
	public void FinishFade() { if (_fading.Count == 0) return; _fading.Clear(); SetProcess(false); QueueRedraw(); }
	public bool IsFading => _fading.Count > 0;

	public override void _Process(double delta)
	{
		foreach (int r in _fading.Keys.ToList()) { float a = _fading[r] - (float)delta / 0.25f; if (a <= 0f) _fading.Remove(r); else _fading[r] = a; }
		if (_fading.Count == 0) SetProcess(false);
		QueueRedraw();
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
			foreach (var l in Shown.Where(l => l.Stream != null)) DrawStream(l.Stream!, Alpha(l.Route));
		}
		foreach (var l in Shown.Where(l => l.InFront == Front).OrderBy(l => l.Rect.End.Y))
			if (Tex(l.Kind) is { } tex) DrawTextureRect(tex, new Rect2(l.Rect.Position + _origin, l.Rect.Size), false, new Color(1f, 1f, 1f, 0.95f * Alpha(l.Route)));
	}

	/// <summary>
	/// A short stream under a bridge: a soft continuous band of muted indigo-blue wash with a thin ink edge on both banks, curving gently, running across the
	/// route and fading out at both ends. The bridge deck is drawn over its middle, so nothing pokes through it.
	/// </summary>
	void DrawStream(Vector2[] pts, float alpha)
	{
		const int steps = 40;
		var line = new List<Vector2>();
		for (int i = 0; i <= steps; i++)
		{
			float t = i / (float)steps * (pts.Length - 1); int k = Mathf.Min(pts.Length - 2, (int)t);
			line.Add(pts[k].Lerp(pts[k + 1], t - k) + _origin);
		}
		float width = 17f;
		Vector2 N(int i) { var d = line[Mathf.Min(line.Count - 1, i + 1)] - line[Mathf.Max(0, i - 1)]; return new Vector2(-d.Y, d.X).Normalized(); }
		float Fade(int i) { float t = i / (float)steps; return Mathf.Clamp(Mathf.Min(t, 1f - t) / 0.28f, 0f, 1f) * alpha; }
		// wide round-capped lines, not polygons: a bend cannot twist them into a shape the engine cannot triangulate
		for (int i = 0; i < line.Count - 1; i++)
		{
			float f = (Fade(i) + Fade(i + 1)) / 2f, w = width * (0.55f + 0.45f * f / Mathf.Max(alpha, 0.001f));
			DrawLine(line[i], line[i + 1], new Color(0.42f, 0.54f, 0.68f, 0.62f * f), w * 2f, true);
			var ink = new Color(0.17f, 0.20f, 0.30f, 0.55f * f);
			DrawLine(line[i] + N(i) * w, line[i + 1] + N(i + 1) * w, ink, 1.6f, true);
			DrawLine(line[i] - N(i) * w, line[i + 1] - N(i + 1) * w, ink, 1.6f, true);
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
