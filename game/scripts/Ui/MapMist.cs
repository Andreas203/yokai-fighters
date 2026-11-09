using Godot;

namespace YokaiFighters.Ui;

/// <summary>
/// YOK-238 (AM19): the soft grey-white mist that trails along the paper past the nodes Ryo can choose. Drawn in code from deterministic noise
/// (stacked, widely stretched translucent ellipses), no texture; it builds up from nothing at <see cref="From"/> to a bank further on. It carries
/// no information: nothing here knows a node, a type or a row, so nothing of the rows ahead can show through it. The dashed route sketches
/// (<see cref="MapRoutes.SetMistLines"/>) run under it and fade into it.
/// </summary>
public partial class MapMist : Control
{
	float _from, _right;
	/// <summary>Where the mist starts (board x): it is thin here and thickens to the right.</summary>
	public float From { get => _from; set { _from = value; QueueRedraw(); } }
	/// <summary>Where it ends (board x), the paper's right end.</summary>
	public float Right { get => _right; set { _right = value; QueueRedraw(); } }
	/// <summary>True while there is any mist to draw (some row is still ahead).</summary>
	public bool Active => _right - _from > 40f && Visible;

	public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;
	public override void _Notification(int what) { if (what == NotificationResized) QueueRedraw(); }

	public override void _Draw()
	{
		if (!Active) return;
		float h = Mathf.Max(60f, Size.Y);
		float ramp = Mathf.Clamp((_right - _from) * 0.5f, 260f, 1400f);
		const int rings = 7;
		for (int i = 0; ; i++)
		{
			float x = _from + 40f + i * 150f;
			if (x > _right + 80f) break;
			float k = Mathf.Clamp((x - _from) / ramp, 0f, 1f);
			k = k * k * (3f - 2f * k);                                // thin at the start, a bank further on
			for (int j = 0; j < 3; j++)
			{
				uint seed = (uint)(i * 7 + j * 131);
				var c = new Vector2(x + (MapLayout.Unit(seed, 1) - 0.5f) * 120f, h * (0.08f + 0.84f * MapLayout.Unit(seed, 2)));
				float rx = 220f + 220f * MapLayout.Unit(seed, 3), ry = h * (0.07f + 0.08f * MapLayout.Unit(seed, 4));
				float a = (0.09f + 0.10f * MapLayout.Unit(seed, 5)) * (0.15f + 0.85f * k);
				DrawSetTransform(c, 0f, new Vector2(rx / ry, 1f));
				for (int r = rings; r >= 1; r--) DrawCircle(Vector2.Zero, ry * r / rings, new Color(0.80f, 0.83f, 0.87f, a));
			}
		}
		DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
	}
}
