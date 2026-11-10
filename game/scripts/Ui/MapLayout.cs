using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace YokaiFighters.Ui;

/// <summary>Where each node sits on the board and how big it is.</summary>
public sealed record MapLayoutResult(IReadOnlyDictionary<string, Rect2> Rects, Vector2 NodeSize)
{
	/// <summary>YOK-108: the row value of each column and the x of its centre, left to right.</summary>
	public IReadOnlyList<(int Row, float X)> Columns { get; init; } = new List<(int, float)>();
	public float ColumnWidth { get; init; }
	/// <summary>YOK-238: the illustration's own rect inside each node's rect (board coordinates): routes end on it, rings go round it.</summary>
	public IReadOnlyDictionary<string, Rect2> Sprites { get; init; } = new Dictionary<string, Rect2>();
	/// <summary>YOK-238: height of one tag line per node (the tag and its text scale with the node).</summary>
	public IReadOnlyDictionary<string, float> LineHeights { get; init; } = new Dictionary<string, float>();
}

/// <summary>
/// YOK-109 (designer, 2026-10-25): which nodes the map draws. A view filter over <see cref="MapNodeView.State"/>, not a change to the route
/// graph: the nodes Ryo has cleared, the one he is on and the ones he can pick now. Later nodes (the Tanuki's too) and rows he has left
/// without taking a node are not drawn at all, so they show no name, icon or twist seal. AM19: their routes still show, as sketches in mist.
/// </summary>
public static class MapReveal
{
	public static bool IsRevealed(MapNodeState s) => s is MapNodeState.Visited or MapNodeState.Current or MapNodeState.Reachable;
	/// <summary>AM19: a route that is still ahead (its far end is not drawn and not a row Ryo has left): drawn as a sketch in the mist.</summary>
	public static bool IsAhead(MapNodeState from, MapNodeState to) => to == MapNodeState.Locked && from != MapNodeState.PassedOver;
}

/// <summary>YOK-238: how a node's illustration shapes its box: the sprite's width / height, its size against the standard one, and extra tag lines (the boss's two).</summary>
public readonly record struct MapNodeShape(float Aspect, float Scale, int ExtraLines)
{
	/// <summary>A plain mask: (<c>new MapNodeShape()</c> would be all zeros, a struct ignores the defaults of a constructor).</summary>
	public static readonly MapNodeShape Standard = new(1.0f, 1f, 0);
}

/// <summary>
/// YOK-238: the proportions of one node, all from the sprite height S. A node is its illustration with a ring clearance around it and a
/// torn name tag overlapping the sprite's foot; the box is the hit area. Layout and <see cref="MapNode"/> both read these, so they agree.
/// </summary>
public static class MapNodeBox
{
	public static float Pad(float s) => 0.14f * s;
	/// <summary>The largest <c>SpriteScale</c> a node's art is drawn at (YOK-240); <see cref="MapNode"/> clamps to it, so the room reserved below is enough for every type.</summary>
	public const float MaxArtScale = 1.3f;
	/// <summary>How far a node's drawn art and ring can reach above its box (a 1.3x building with its foot on the sprite rect's foot, plus the ring's clearance): reserved for every node, whatever it is.</summary>
	public static float OverflowTop(float s) => (MaxArtScale - 1f) * s - 0.28f * Pad(s) + 2f;
	/// <summary>How far the ring can reach past the box's sides.</summary>
	public static float OverflowSide(float s) => 0.08f * s;
	/// <summary>The box grown by the art's and ring's overflow (what the layout keeps on the paper and the landmarks keep clear of); <paramref name="s"/> is the node's sprite height.</summary>
	public static Rect2 Grown(Rect2 box, float s) => new(box.Position - new Vector2(OverflowSide(s), OverflowTop(s)), box.Size + new Vector2(2f * OverflowSide(s), OverflowTop(s)));
	public static float LineHeight(float s) => Mathf.Clamp(s * 0.2f, 20f, 34f);
	/// <summary>Where the tag's top edge is, from the box top: it overlaps the sprite's lowest tenth.</summary>
	public static float TagTop(float s) => Pad(s) + 0.9f * s;
	public static float TagHeight(float s, int extra) => LineHeight(s) * (1 + extra) + 8f;

	public static Vector2 Size(float s, MapNodeShape shape)
	{
		float w = Mathf.Max(s * shape.Aspect, s * 1.08f) + 2f * Pad(s);
		return new Vector2(w, TagTop(s) + TagHeight(s, shape.ExtraLines));
	}

	/// <summary>The sprite's rect inside a box of <paramref name="size"/> (box coordinates).</summary>
	public static Rect2 Sprite(Vector2 size, float s, MapNodeShape shape)
	{
		var sz = new Vector2(s * shape.Aspect, s);
		return new Rect2(new Vector2((size.X - sz.X) / 2f, Pad(s)), sz);
	}
}

/// <summary>
/// YOK-78, YOK-238: pure layout from graph data. Rows run left to right (one column per distinct row value, ascending); within a row the
/// nodes keep their view order top to bottom. Map v2: the nodes are not on a line. Each column's group of nodes wanders up or down along the
/// paper, and each node leans a little off its slot, by a deterministic hash of the node ids (same map, same layout, no random state).
/// The node size comes from the board height and a typical row of three (not the busiest row, which would hint at hidden rows); a row of
/// more nodes shrinks its own nodes only. Nothing here knows the default map.
/// </summary>
public static class MapLayout
{
	public static readonly Vector2 MinNode = new(40, 40);
	/// <summary>
	/// The row size the node height is based on. A fixed number, not the busiest row of the map: with nodes hidden until their row is the choice,
	/// the node size must not hint at how many nodes a later row holds. A row with more nodes than this shrinks its own nodes only.
	/// </summary>
	public const int TypicalRow = 3;
	/// <summary>Tag lines a lone node's box reserves beyond the name (the boss's two).</summary>
	public const int LoneExtraLines = 2;
	/// <summary>The standard sprite height as a share of the board's height.</summary>
	public const float SpriteShare = 0.225f;

	/// <summary>Deterministic hash of a string (not <c>string.GetHashCode</c>, which differs per process).</summary>
	public static uint Hash(string s)
	{
		uint h = 2166136261u;
		foreach (char c in s) { h ^= c; h *= 16777619u; }
		h ^= h >> 15; h *= 2246822519u; h ^= h >> 13; h *= 3266489917u; h ^= h >> 16;
		return h;
	}

	/// <summary>Deterministic 0..1 from a hash and a salt.</summary>
	public static float Unit(uint h, int salt)
	{
		uint x = h + (uint)salt * 0x9E3779B9u; x ^= x >> 16; x *= 0x85EBCA6Bu; x ^= x >> 13; x *= 0xC2B2AE35u; x ^= x >> 16;
		return (x & 0xFFFFFF) / (float)0xFFFFFF;
	}

	public static MapLayoutResult Compute(IReadOnlyList<MapNodeView> nodes, Vector2 board, Func<MapNodeView, MapNodeShape>? shapeOf = null)
	{
		var rects = new Dictionary<string, Rect2>();
		var sprites = new Dictionary<string, Rect2>();
		var lines = new Dictionary<string, float>();
		if (nodes.Count == 0) return new(rects, MinNode);
		shapeOf ??= _ => MapNodeShape.Standard;
		var rows = nodes.GroupBy(n => n.Row).OrderBy(g => g.Key).Select(g => g.ToList()).ToList();
		uint seed = Hash(string.Join("|", nodes.Select(n => n.Id)));
		float colW = board.X / rows.Count;
		float s0 = Mathf.Max(24f, Mathf.Min(board.Y * SpriteShare, colW * 0.42f)); // never wider than its column leaves room for routes between
		float reserve = 2f * MapNodeBox.LineHeight(s0);      // the "YOU ARE HERE" line (and the boss's two tag lines) hang below the lowest node: keep them on the board
		float usable = Mathf.Max(60f, board.Y - reserve);
		var std = MapNodeBox.Size(s0, MapNodeShape.Standard);
		for (int c = 0; c < rows.Count; c++)
		{
			var row = rows[c];
			int n = row.Count, cap = Math.Max(TypicalRow, n);
			float cell = usable / cap, free = usable - n * cell;
			// the group wanders: a slow wave along the rows plus a hashed nudge, up to 85% of the free height either way
			float wave = 0.75f * Mathf.Sin(c * 2.05f + Unit(seed, 1) * Mathf.Tau) + 0.25f * (2f * Unit(seed, 100 + c) - 1f);
			float y0 = free / 2f + wave * free / 2f * 0.85f;
			for (int j = 0; j < n; j++)
			{
				var v = row[j];
				uint h = Hash(v.Id);
				var shape = shapeOf(v);
				// a lone node (the first row, the boss) has the column to itself: its box keeps room for a tag of three lines (the boss's name and two more), whoever waits there (AM19: by row size, never by type)
				if (n == 1) shape = shape with { ExtraLines = Math.Max(shape.ExtraLines, LoneExtraLines) };
				float s = s0 * shape.Scale;
				var size = MapNodeBox.Size(s, shape);
				// a crowded row shrinks its own nodes only; a lone node may use the column's whole height (the boss's two tag lines make it taller)
				float maxH = n == 1 ? usable * 0.98f : cell * 0.96f;
				if (size.Y + MapNodeBox.OverflowTop(s) > maxH) // (the art and ring that rise above the box count: they must not poke into the next node) the tag text has a floor, so solve for the sprite height that fits
				{
					float lo = 4f, hi = s;
					for (int it = 0; it < 16; it++) { float mid = (lo + hi) / 2f; if (MapNodeBox.Size(mid, shape).Y + MapNodeBox.OverflowTop(mid) > maxH) hi = mid; else lo = mid; }
					s = lo; size = MapNodeBox.Size(s, shape);
				}
				// the box AND the art and ring rising above it must stay inside this node's own cell, so the grown boxes of a column never overlap:
				// the nudge is taken from the room left after the overflow, and the box sits under it (by the node's size alone, never its type)
				float ov = MapNodeBox.OverflowTop(s);
				float slack = Mathf.Max(0f, cell - size.Y - ov) / 2f;
				float cy = y0 + (j + 0.5f) * cell + slack * (2f * Unit(h, 2) - 1f) * 0.9f + ov / 2f;
				float lean = n > 1 ? (j % 2 == 0 ? -1f : 1f) * 0.03f + (Unit(h, 3) - 0.5f) * 0.02f : (Unit(h, 3) - 0.5f) * 0.08f;
				float cx = (c + 0.5f) * colW + lean * colW;
				var pos = new Vector2(Mathf.Clamp(cx - size.X / 2f, 0f, Mathf.Max(0f, board.X - size.X)), Mathf.Max(MapNodeBox.OverflowTop(s), Mathf.Min(cy - size.Y / 2f, Mathf.Max(0f, board.Y - reserve - size.Y))));
				rects[v.Id] = new Rect2(pos, size);
				var sp = MapNodeBox.Sprite(size, s, shape);
				sprites[v.Id] = new Rect2(pos + sp.Position, sp.Size);
				lines[v.Id] = MapNodeBox.LineHeight(s);
			}
		}
		return new(rects, std) { Columns = rows.Select((r, c) => (r[0].Row, (c + 0.5f) * colW)).ToList(), ColumnWidth = colW, Sprites = sprites, LineHeights = lines };
	}
}
