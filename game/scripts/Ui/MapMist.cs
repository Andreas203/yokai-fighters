using Godot;

namespace YokaiFighters.Ui;

/// <summary>
/// YOK-238 (AM19), YOK-240: the cloud bank at the far end of what Ryo can see. From <see cref="From"/> (just past the last drawn node) the paper is open: the
/// dashed route sketches run over it and stay clear. From <see cref="BankStart"/> a few wisps appear, and within about half a column they build into
/// flat-bottomed grey-white cumulus layers (paper-toned, a thin ink edge) that are densest against the paper's right end (<see cref="Right"/>) and swallow the sketches there.
/// Drawn in code from deterministic noise, no texture; it carries no information: nothing here knows a node, a type or a row, so nothing of the rows ahead can show
/// through it.
/// </summary>
public partial class MapMist : Control
{
	float _from, _right, _bank, _ramp = 420f;
	/// <summary>Where the mist's reach starts (board x): just right of the last drawn node.</summary>
	public float From { get => _from; set { _from = value; QueueRedraw(); } }
	/// <summary>Where it ends (board x), the paper's right end.</summary>
	public float Right { get => _right; set { _right = value; QueueRedraw(); } }
	/// <summary>Where the first wisps of cloud appear (board x, at least <see cref="From"/>); open paper lies between.</summary>
	public float BankStart { get => Mathf.Max(_bank, _from); set { _bank = value; QueueRedraw(); } }
	/// <summary>How far past <see cref="BankStart"/> the clouds take to reach full body (px).</summary>
	public float Ramp { get => _ramp; set { _ramp = Mathf.Max(60f, value); QueueRedraw(); } }
	/// <summary>True while there is any mist to draw (some row is still ahead).</summary>
	public bool Active => _right - _from > 40f && Visible;

	/// <summary>How thick the bank is at board x: 0 on the open paper, building to 1 within <see cref="Ramp"/> of <see cref="BankStart"/>. A function of x only.</summary>
	public float Density(float x) { float k = Mathf.Clamp((x - BankStart) / _ramp, 0f, 1f); return k * k * (3f - 2f * k); }

	public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;
	public override void _Notification(int what) { if (what == NotificationResized) QueueRedraw(); }

	public override void _Draw()
	{
		if (!Active) return;
		float h = Mathf.Max(60f, Size.Y);
		float x0 = BankStart - 60f;
		const float step = 130f;
		for (int i = 0; ; i++)
		{
			float x = x0 + i * step;
			if (x > _right + 120f) break;
			float k = Density(x + 60f);
			for (int band = 0; band < 5; band++)
			{
				uint seed = (uint)(i * 17 + band * 211);
				if (MapLayout.Unit(seed, 6) > 0.12f + 0.95f * k) continue;           // gaps: the bank closes up to the right
				float w = (190f + 230f * MapLayout.Unit(seed, 3)) * (0.55f + 0.45f * k);
				float baseY = h * (0.16f + 0.17f * band + 0.08f * (MapLayout.Unit(seed, 2) - 0.5f));
				float cx = x + (MapLayout.Unit(seed, 1) - 0.5f) * step;
				Cloud(seed, cx, baseY, w, h * (0.06f + 0.05f * MapLayout.Unit(seed, 4)), 0.28f + 0.62f * k);
			}
		}
	}

	/// <summary>One flat-bottomed cumulus: a skyline of overlapping round puffs over a level base, a shaded underside, and a thin ink edge on top.</summary>
	void Cloud(uint seed, float cx, float baseY, float width, float height, float alpha)
	{
		int puffs = 4 + (int)(MapLayout.Unit(seed, 7) * 4f);
		var pcx = new float[puffs]; var pr = new float[puffs];
		for (int p = 0; p < puffs; p++)
		{
			pcx[p] = cx + (p / (float)(puffs - 1) - 0.5f) * width * 0.72f + (MapLayout.Unit(seed, 20 + p) - 0.5f) * width * 0.06f;
			float mid = 1f - Mathf.Abs(p / (float)(puffs - 1) - 0.5f) * 1.2f;       // the middle puffs are the tallest
			pr[p] = width * (0.13f + 0.10f * MapLayout.Unit(seed, 30 + p)) * (0.7f + 0.5f * mid) * (height / 40f > 1.4f ? 1.4f : Mathf.Max(0.8f, height / 40f));
		}
		float left = cx - width * 0.5f, right = cx + width * 0.5f;
		int n = Mathf.Max(12, (int)(width / 5f));
		var top = new Vector2[n + 1];
		for (int s = 0; s <= n; s++)
		{
			float x = Mathf.Lerp(left, right, s / (float)n), best = 0f;
			for (int p = 0; p < puffs; p++)
			{
				float dx = x - pcx[p];
				if (Mathf.Abs(dx) < pr[p]) best = Mathf.Max(best, Mathf.Sqrt(pr[p] * pr[p] - dx * dx) * 0.85f);
			}
			float taper = Mathf.Min(1f, Mathf.Min(s, n - s) / 4f);                    // the ends of the layer come down to the base
			top[s] = new Vector2(x, baseY - Mathf.Max(best, 3f) * taper);
		}
		var body = new Vector2[n + 3];
		body[0] = new Vector2(left, baseY);
		for (int s = 0; s <= n; s++) body[s + 1] = top[s];
		body[n + 2] = new Vector2(right, baseY);
		DrawColoredPolygon(body, new Color(0.89f, 0.88f, 0.85f, alpha));          // the paper-toned body
		var shade = new Vector2[n + 3];
		for (int s = 0; s < body.Length; s++) shade[s] = new Vector2(body[s].X, Mathf.Lerp(body[s].Y, baseY, 0.62f));
		shade[0] = body[0]; shade[^1] = body[^1];
		DrawColoredPolygon(shade, new Color(0.74f, 0.74f, 0.73f, alpha * 0.55f)); // its shaded underside
		DrawPolyline(top, new Color(0.30f, 0.29f, 0.31f, alpha * 0.5f), 1.6f, true); // the thin ink edge
	}
}
