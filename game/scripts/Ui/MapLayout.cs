using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace YokaiFighters.Ui;

/// <summary>Where each node sits on the board and how big it is.</summary>
public sealed record MapLayoutResult(IReadOnlyDictionary<string, Rect2> Rects, Vector2 NodeSize)
{
	/// <summary>YOK-108: the row value of each column and the x of its centre, left to right (for the row headers).</summary>
	public IReadOnlyList<(int Row, float X)> Columns { get; init; } = new List<(int, float)>();
	public float ColumnWidth { get; init; }
}

/// <summary>
/// YOK-109 (designer, 2026-10-25): which nodes the map draws. A view filter over <see cref="MapNodeView.State"/>, not a change to the route
/// graph: the nodes Ryo has cleared, the one he is on and the ones he can pick now. Later nodes (the Tanuki's too) and rows he has left
/// without taking a node are not drawn at all, so they show no name, icon, twist seal or row header.
/// </summary>
public static class MapReveal
{
	public static bool IsRevealed(MapNodeState s) => s is MapNodeState.Visited or MapNodeState.Current or MapNodeState.Reachable;
}

/// <summary>
/// YOK-78: pure layout from graph data. Rows run left to right (one column per distinct row value, ascending); nodes within a row
/// stack top to bottom in the view's order. Cells are an even split of the board (YOK-109: the board is the whole scroll, one column of <c>board.X / rows</c> per row, nodes of every row get a rect whether or not they are drawn), node size comes from a typical row of three (not the busiest row, which would hint at hidden rows) and from the column
/// count, so any row or node count fits. Nothing here knows the default map.
/// </summary>
public static class MapLayout
{
	public static readonly Vector2 MaxNode = new(230, 150);
	public static readonly Vector2 MinNode = new(48, 40);

	/// <summary>
	/// The row size the card height is based on. A fixed number, not the busiest row of the map: with nodes hidden until their row is the choice,
	/// the card size must not hint at how many nodes a later row holds. A row with more nodes than this shrinks its own cards only.
	/// </summary>
	public const int TypicalRow = 3;

	public static MapLayoutResult Compute(IReadOnlyList<MapNodeView> nodes, Vector2 board)
	{
		var rects = new Dictionary<string, Rect2>();
		if (nodes.Count == 0) return new(rects, MinNode);
		var rows = nodes.GroupBy(n => n.Row).OrderBy(g => g.Key).Select(g => g.ToList()).ToList();
		float colW = board.X / rows.Count, cellH = board.Y / TypicalRow;
		var size = new Vector2(Math.Clamp(colW * 0.8f, MinNode.X, MaxNode.X), Math.Clamp(cellH * 0.85f, MinNode.Y, MaxNode.Y));
		for (int c = 0; c < rows.Count; c++)
		{
			int cap = Math.Max(TypicalRow, rows[c].Count);
			float rowCell = board.Y / cap, y0 = (board.Y - rows[c].Count * rowCell) / 2;
			var rowSize = new Vector2(size.X, Math.Min(size.Y, Math.Clamp(rowCell * 0.85f, MinNode.Y, MaxNode.Y)));
			for (int j = 0; j < rows[c].Count; j++)
			{
				var centre = new Vector2((c + 0.5f) * colW, y0 + (j + 0.5f) * rowCell);
				rects[rows[c][j].Id] = new Rect2(centre - rowSize / 2, rowSize);
			}
		}
		return new(rects, size) { Columns = rows.Select((r, c) => (r[0].Row, (c + 0.5f) * colW)).ToList(), ColumnWidth = colW };
	}
}
