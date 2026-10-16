using System;
using YokaiFighters.Sim;

namespace YokaiFighters.Fight;

/// <summary>
/// YOK-129 / AM9: the Arc System Works-style fight camera. Presentation only: it reads the sim after each
/// tick and nothing in Sim/, Ai/ or the replay reads it back. Pure C# (no Godot types), so tests do the camera maths
/// without booting a scene.
///
/// Each sim tick (<see cref="Step"/>, never per render or delta time, so replays and captures reproduce):
/// 1. the view width eases toward <see cref="TargetWidth"/> (close: <see cref="CameraConfig.CloseViewWidth"/>,
///    at the screen walls: <see cref="SimConfig.ViewWidth"/>, a jump adds a little), and the height eases toward chest
///    height plus part of the higher fighter's jump;
/// 2. hard floors that override the easing: the width never drops below what keeps both models in frame
///    (<see cref="RequiredWidth"/>), the height never drops below what keeps the higher head under the HUD
///    (<see cref="MinHeight"/>);
/// 3. the centre follows the fighters' midpoint (V7: steady, not eased), clamped at the stage edge, then into the
///    range that keeps both models' extents in frame (in-frame wins over the stage clamp by at most
///    <see cref="CameraConfig.StageEdgePadUnits"/>).
/// Widths are units at the Z = 0 plane at the camera's axis depth; heights metres.
/// </summary>
public sealed class DynamicCamera
{
	public const float UnitsPerMeter = 200f; // E1

	/// <summary>One fighter as the camera sees it: position, jump height and model extent (units).</summary>
	public readonly record struct Body(float X, float Y, float Left, float Right);

	/// <summary>What the camera frames this tick. KoProgress 0..1 over the KO slow-down; PushIn 0..1 for scripted push-ins (V7 Tanuki reveal).</summary>
	public readonly record struct Subject(Body A, Body B, float KoProgress, float PushIn)
	{
		public float Separation => MathF.Abs(B.X - A.X);
		public float MaxY => MathF.Max(A.Y, B.Y);
	}

	public CameraConfig Config { get; }
	public SimConfig Sim { get; }
	/// <summary>Visible width at the plane on the camera axis, in units (V3 shake converts px through this).</summary>
	public float Width { get; private set; }
	public float HeightMeters { get; private set; }
	public float CenterX { get; private set; }
	public bool Ready { get; private set; }

	private readonly float _tanV, _topNdcTan;
	private float _pitch, _cos, _sin;

	/// <summary>Current downward pitch in degrees (negative = looking up): lerped with the zoom from <see cref="CameraConfig.PitchDegrees"/> to <see cref="CameraConfig.WidePitchDegrees"/>.</summary>
	public float PitchDegrees => _pitch * 180f / MathF.PI;

	private float Zoom(float width) => MaxWidth > MinWidth ? Math.Clamp((width - MinWidth) / (MaxWidth - MinWidth), 0f, 1f) : 0f;

	/// <summary>Sets the pitch for a view width (a fixed function of the eased width, so deterministic per tick).</summary>
	private void SetPitch(float width)
	{
		float deg = Config.PitchDegrees + (Config.WidePitchDegrees - Config.PitchDegrees) * Zoom(width);
		_pitch = deg * MathF.PI / 180f;
		_cos = MathF.Cos(_pitch);
		_sin = MathF.Sin(_pitch);
	}

	public DynamicCamera(SimConfig sim, CameraConfig? config = null)
	{
		Sim = sim;
		Config = config ?? CameraConfig.Default;
		_tanV = MathF.Tan(Config.FovDegrees * MathF.PI / 360f);
		float topNdc = 1f - 2f * Config.HudClearPx / 1080f;
		_topNdcTan = topNdc * _tanV;
		SetPitch(MinWidth);
	}

	public static Subject SubjectOf(Match m, float pushIn = 0f)
	{
		Body B(Fighter f)
		{
			var (l, r) = FightCamera.VisualExtent(m.Config, f);
			return new Body(f.X / (float)SimConfig.Scale, f.Y / (float)SimConfig.Scale, l / (float)SimConfig.Scale, r / (float)SimConfig.Scale);
		}
		float ko = m.KoFrame > 0 && m.Config.KoSlowFrames > 0 ? Math.Min(1f, m.KoFrame / (float)m.Config.KoSlowFrames) : 0f;
		return new Subject(B(m.P1), B(m.P2), ko, pushIn);
	}

	/// <summary>Widest view (the sim's screen walls are derived from it) and the closest one, in units.</summary>
	public float MaxWidth => Sim.ViewWidth / (float)SimConfig.Scale;
	public float MinWidth => MathF.Min(Config.CloseViewWidth, MaxWidth);

	/// <summary>Half the stage the view may show: the sim's visible half plus the pitch pad (units).</summary>
	public float StageHalf => FightCamera.VisibleHalfWidth(Sim) / (float)SimConfig.Scale + Config.StageEdgePadUnits;

	/// <summary>Where the eased width heads: monotonic in separation, plus a little for a jump, minus the V7 push-ins.</summary>
	public float TargetWidth(in Subject s)
	{
		float maxSep = Sim.MaxSeparation / (float)SimConfig.Scale;
		float span = MathF.Max(1f, maxSep - Config.NearSeparation);
		float f = Math.Clamp((s.Separation - Config.NearSeparation) / span, 0f, 1f);
		float w = MinWidth + (MaxWidth - MinWidth) * f + Config.JumpZoomPerUnit * MathF.Max(0f, s.MaxY);
		w = MathF.Min(w, MaxWidth);
		return w * (1f - Config.KoPushIn * s.KoProgress) * (1f - Math.Clamp(s.PushIn, 0f, 0.5f));
	}

	/// <summary>Where the eased height heads: chest height (1.2 m at every zoom by default; lerped from <see cref="CameraConfig.BaseHeightMeters"/> to <see cref="CameraConfig.WideHeightMeters"/> with the zoom) plus part of the jump.</summary>
	public float TargetHeight(in Subject s, float width)
	{
		float zoom = Zoom(width);
		return Config.BaseHeightMeters + (Config.WideHeightMeters - Config.BaseHeightMeters) * zoom
			+ Config.JumpFollow * MathF.Max(0f, s.MaxY) / UnitsPerMeter;
	}

	/// <summary>Camera distance from the Z = 0 plane (m) for a visible width (units) at the FOV and aspect.</summary>
	public float DistanceFor(float width) => width / UnitsPerMeter / (2f * _tanV * Config.Aspect);
	public float WidthFor(float distance) => distance * 2f * _tanV * Config.Aspect * UnitsPerMeter;
	public float Distance => DistanceFor(Width);

	/// <summary>Lowest camera height (m) that keeps the higher fighter's model top under the HUD line at this width.</summary>
	public float MinHeight(float width, in Subject s)
	{
		float top = MathF.Max(0f, s.MaxY) / UnitsPerMeter + Config.ModelTopMeters;
		float pitch = (Config.PitchDegrees + (Config.WidePitchDegrees - Config.PitchDegrees) * Zoom(width)) * MathF.PI / 180f; // the pitch at this width
		return top - DistanceFor(width) * MathF.Tan(MathF.Atan(_topNdcTan) - pitch);
	}


	private float TopOf(in Body b) => MathF.Max(0f, b.Y) / UnitsPerMeter + Config.ModelTopMeters;
	private float FeetOf(in Body b) => MathF.Max(0f, b.Y) / UnitsPerMeter;

	/// <summary>How much nearer than the axis depth (m) the nearest of a model's feet and top is: looking down that is the top, looking up the feet.</summary>
	private float Nearer(in Body b, float cy) => MathF.Max((TopOf(b) - cy) * _sin, (FeetOf(b) - cy) * _sin);

	/// <summary>The narrowest half-width (units) over a model's height: at its nearest point.</summary>
	private float HalfFor(float d, float cy, in Body b) => (d * _cos - Nearer(b, cy)) * _tanV * Config.Aspect * UnitsPerMeter;

	/// <summary>Narrowest width (units) at camera height cy that fits both models' extents side by side (the pitched view is narrowest at their tops).</summary>
	public float RequiredWidth(float cy, in Subject s)
	{
		float scale = _tanV * Config.Aspect * UnitsPerMeter;
		float ka = Nearer(s.A, cy), kb = Nearer(s.B, cy);
		float d = 0f;
		void Pair(float right, float ki, float left, float kj) => d = MathF.Max(d, ((right - left) / scale + ki + kj) / (2f * _cos));
		Pair(s.A.Right, ka, s.A.Left, ka);
		Pair(s.B.Right, kb, s.B.Left, kb);
		Pair(s.A.Right, ka, s.B.Left, kb);
		Pair(s.B.Right, kb, s.A.Left, ka);
		return WidthFor(d);
	}

	/// <summary>Jump straight to the target framing (fight start, reset, a new duel).</summary>
	public void Snap(in Subject s)
	{
		Width = TargetWidth(s);
		HeightMeters = TargetHeight(s, Width);
		Constrain(s);
		Ready = true;
	}

	/// <summary>One sim tick of easing (call once per sim tick, after the sim stepped).</summary>
	public void Step(in Subject s)
	{
		if (!Ready) { Snap(s); return; }
		Width += (TargetWidth(s) - Width) * Config.ZoomEase;
		HeightMeters += (TargetHeight(s, Width) - HeightMeters) * Config.HeightEase;
		Constrain(s);
	}

	private void Constrain(in Subject s)
	{
		// The pitch follows the width; re-set it after every width floor so the rendered pitch is the one MinHeight
		// assumed for that width (MinHeight falls as the width grows, so a later floor keeps the height valid).
		SetPitch(Width);
		Width = MathF.Max(Width, RequiredWidth(HeightMeters, s));
		SetPitch(Width);
		HeightMeters = MathF.Max(HeightMeters, MinHeight(Width, s));
		Width = MathF.Max(Width, RequiredWidth(HeightMeters, s)); // a higher camera only lowers this; kept for safety
		SetPitch(Width);

		float lim = MathF.Max(0f, StageHalf - Width / 2f);
		float cx = Math.Clamp((s.A.X + s.B.X) / 2f, -lim, lim);
		float d = Distance;
		float ha = HalfFor(d, HeightMeters, s.A), hb = HalfFor(d, HeightMeters, s.B);
		float lo = MathF.Max(s.A.Right - ha, s.B.Right - hb), hi = MathF.Min(s.A.Left + ha, s.B.Left + hb);
		CenterX = lo <= hi ? Math.Clamp(cx, lo, hi) : (lo + hi) / 2f;
	}

	/// <summary>Camera position (m) and pitch (radians, negative = looking down) for the rig.</summary>
	/// <summary>Puts the camera at a given framing (stage-coverage tests and captures); the next <see cref="Step"/> eases on from it.</summary>
	public void Place(float width, float heightMeters, float centerX)
	{
		Width = width; HeightMeters = heightMeters; CenterX = centerX; Ready = true;
		SetPitch(width);
	}

	/// <summary>World ray (m) through a normalised screen point (-1..1, y up): origin and unnormalised direction.</summary>
	public ((float X, float Y, float Z) Origin, (float X, float Y, float Z) Dir) ScreenRay(float ndcX, float ndcY)
	{
		float sx = ndcX * _tanV * Config.Aspect, sy = ndcY * _tanV;
		return ((CenterX / UnitsPerMeter, HeightMeters, Distance), (sx, -_sin + sy * _cos, -_cos - sy * _sin));
	}

	public (float X, float Y, float Z, float Pitch) Transform =>(CenterX / UnitsPerMeter, HeightMeters, Distance, -_pitch);

	/// <summary>Normalised screen position (-1..1, y up) of a point at x (units) and height h (m) on the Z = 0 plane.</summary>
	public (float X, float Y) Project(float x, float h)
	{
		float rx = (x - CenterX) / UnitsPerMeter, ry = h - HeightMeters, rz = -Distance;
		float yc = ry * _cos - rz * _sin;   // up = (0, cos p, -sin p)
		float depth = -ry * _sin - rz * _cos; // forward = (0, -sin p, -cos p)
		return (rx / (depth * _tanV * Config.Aspect), yc / (depth * _tanV));
	}

	/// <summary>Screen y in px on the 1080 canvas (0 = top) of a point at height h (m).</summary>
	public float ScreenYPx(float x, float h) => (1f - Project(x, h).Y) * 540f;

	/// <summary>True if both models (extent x feet-to-top) are on screen horizontally and both tops sit under the HUD line.</summary>
	public bool InFrame(in Subject s, float eps = 1e-4f)
	{
		foreach (var b in new[] { s.A, s.B })
		{
			float feet = MathF.Max(0f, b.Y) / UnitsPerMeter, top = TopOf(b);
			foreach (float h in new[] { feet, top })
				foreach (float x in new[] { b.Left, b.Right })
					if (MathF.Abs(Project(x, h).X) > 1f + eps) return false;
			if (ScreenYPx(b.X, top) < Config.HudClearPx - 0.01f) return false;
		}
		return true;
	}

	/// <summary>True if the view at the plane stays inside the stage the art covers (<see cref="StageHalf"/>).</summary>
	public bool OnStage(float eps = 0.01f) => MathF.Abs(CenterX) + Width / 2f <= StageHalf + eps;
}
