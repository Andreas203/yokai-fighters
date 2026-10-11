using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace YokaiFighters.Ui;

/// <summary>Where each node sits on the board and how big it is.</summary>
public sealed record MapLayoutResult(IReadOnlyDictionary<string, Rect2> Rects, Vector2 NodeSize);

/// <summary>
/// YOK-78: pure layout from graph data. Rows run left to right (one column per distinct row value, ascending); nodes within a row
/// stack top to bottom in the view's order. Cells are an even split of the board, node size shrinks with the busiest row and column
/// count, so any row or node count fits. Nothing here knows the default map.
/// </summary>
public static class MapLayout
{
	public static readonly Vector2 MaxNode = new(200, 130);
	public static readonly Vector2 MinNode = new(48, 40);

	public static MapLayoutResult Compute(IReadOnlyList<MapNodeView> nodes, Vector2 board)
	{
		var rects = new Dictionary<string, Rect2>();
		if (nodes.Count == 0) return new(rects, MinNode);
		var rows = nodes.GroupBy(n => n.Row).OrderBy(g => g.Key).Select(g => g.ToList()).ToList();
		int maxPerRow = rows.Max(r => r.Count);
		float colW = board.X / rows.Count, cellH = board.Y / maxPerRow;
		var size = new Vector2(Math.Clamp(colW * 0.8f, MinNode.X, MaxNode.X), Math.Clamp(cellH * 0.85f, MinNode.Y, MaxNode.Y));
		for (int c = 0; c < rows.Count; c++)
		{
			float y0 = (board.Y - rows[c].Count * cellH) / 2;
			for (int j = 0; j < rows[c].Count; j++)
			{
				var centre = new Vector2((c + 0.5f) * colW, y0 + (j + 0.5f) * cellH);
				rects[rows[c][j].Id] = new Rect2(centre - size / 2, size);
			}
		}
		return new(rects, size);
	}
}
