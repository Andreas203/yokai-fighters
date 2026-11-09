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
		// take 1: a few wisps trailing off to the right over the routes still to come, not a bank. Thin where it starts, a little thicker over the next
		// column or two, then drifting wisps (sparser and fainter every step) out to the paper's end; same shape whoever waits ahead.
		const int rings = 6;
		for (int i = 0; ; i++)
		{
			float x = _from + 40f + i * 170f;
			if (x > _right + 80f) break;
			float d = (x - _from) / 1000f;
			float k = Mathf.Clamp(d / 0.5f, 0f, 1f); k = k * k * (3f - 2f * k);          // builds up over the first half screen
			k *= 1f - 0.45f * Mathf.Clamp((d - 0.9f) / 1.6f, 0f, 1f);                    // and thins again further out
			for (int j = 0; j < 2; j++)
			{
				uint seed = (uint)(i * 7 + j * 131);
				if (MapLayout.Unit(seed, 6) > 0.35f + 0.65f * k) continue;               // gaps between the wisps
				var c = new Vector2(x + (MapLayout.Unit(seed, 1) - 0.5f) * 150f, h * (0.12f + 0.76f * MapLayout.Unit(seed, 2)));
				float rx = 170f + 190f * MapLayout.Unit(seed, 3), ry = h * (0.035f + 0.05f * MapLayout.Unit(seed, 4));
				float a = (0.10f + 0.07f * MapLayout.Unit(seed, 5)) * (0.2f + 0.8f * k);
				DrawSetTransform(c, 0f, new Vector2(rx / ry, 1f));
				for (int r = rings; r >= 1; r--) DrawCircle(Vector2.Zero, ry * r / rings, new Color(0.80f, 0.83f, 0.87f, a));
			}
		}
		DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
	}
}
