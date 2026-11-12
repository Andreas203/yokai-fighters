using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using YokaiFighters.Fight;

namespace YokaiFighters.Tests;

/// <summary>
/// YOK-finish-qa measurement: CPU skinning of the sampled body vertices from the skeleton's global poses (the game's own
/// pose after <see cref="FighterModel.Apply"/>), foot height and contact slip, and self-intersection against rigid core
/// capsules (one per bone or prop, fitted to its rest-pose vertices; core = 0.6 of the median radius). A vertex of a
/// non-adjacent part inside a core is a clip; depth = median radius - distance to the core axis.
/// </summary>
public partial class FinishQaCapture
{
	public sealed class FrameM
	{
		public int Frame;
		public float FootL, FootR, BodyMin;
		public float SlipL, SlipR; // cm this frame: median horizontal motion of sole verts on the floor in both frames
		public Vector2 PosL, PosR;  // foot vertex centroid (x, z), m
		public List<(string Probe, string Target, int Count, float Depth)> Hits = new();
		public Vector3[]? Sole; // world positions of the sampled verts (for slip), not serialised
		public double Badness;

		public Dictionary<string, object> ToJson() => new()
		{
			["f"] = Frame,
			["fl"] = MathF.Round(FootL * 100f, 2), ["fr"] = MathF.Round(FootR * 100f, 2), ["min"] = MathF.Round(BodyMin * 100f, 2),
			["sl"] = MathF.Round(SlipL, 2), ["sr"] = MathF.Round(SlipR, 2),
			["pl"] = new[] { MathF.Round(PosL.X * 100f, 2), MathF.Round(PosL.Y * 100f, 2) },
			["pr"] = new[] { MathF.Round(PosR.X * 100f, 2), MathF.Round(PosR.Y * 100f, 2) },
			["hits"] = Hits.Select(h => new object[] { h.Probe, h.Target, h.Count, MathF.Round(h.Depth * 100f, 1) }).ToList(),
		};
	}

	private sealed class Part
	{
		public MeshInstance3D Mi = null!;
		public bool Skinned;
		public Transform3D[] Bind = Array.Empty<Transform3D>();
		public int[] BindBone = Array.Empty<int>();
		public Node3D? Attach;   // prop: its BoneAttachment3D
		public int AttachBone = -1;
		public string PropName = "";
		public List<Vector3> P = new();
		public List<int[]> Bi = new(); public List<float[]> Bw = new();
	}

	/// <summary>A rigid core capsule (segment A-B) in its owner's frame (bone global pose, or the prop mesh's transform).</summary>
	private sealed class Cap
	{
		public string Name = "";
		public int Bone = -1;
		public Part? Prop;
		public Vector3 A, B;
		public float R, Core;
		public Vector3 WA, WB;
	}

	private static readonly string[] TorsoBones = { "hips", "spine", "spine01", "spine02", "spine1", "spine2", "neck", "head", "leftshoulder", "rightshoulder", "headtop_end", "head_end", "headfront" };
	private static bool IsTorso(string n) => TorsoBones.Contains(n.ToLowerInvariant());

	private sealed class Body
	{
		private readonly Skeleton3D _sk;
		private readonly List<Part> _parts = new();
		private readonly List<(Part Part, int I)> _v = new();
		private int[] _vBone = Array.Empty<int>();
		private string[] _vGroup = Array.Empty<string>();
		private float[] _vDom = Array.Empty<float>();
		private bool[] _footL = Array.Empty<bool>(), _footR = Array.Empty<bool>();
		private Vector3[] _wp = Array.Empty<Vector3>();
		private Transform3D[] _bonePose = Array.Empty<Transform3D>();
		private readonly List<Cap> _caps = new();
		private readonly HashSet<(string, string)> _skip = new();
		public float Height;
		public float RestFootMin;

		public Body(FighterModel m)
		{
			_sk = m.Skeleton;
			foreach (var mi in m.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
			{
				if (mi.Mesh is null) continue;
				var part = new Part { Mi = mi };
				if (mi.Skin is { } skin && mi.GetNodeOrNull<Skeleton3D>(mi.Skeleton) is not null)
				{
					part.Skinned = true;
					int nb = skin.GetBindCount();
					part.Bind = new Transform3D[nb]; part.BindBone = new int[nb];
					for (int i = 0; i < nb; i++)
					{
						part.Bind[i] = skin.GetBindPose(i);
						string nm = skin.GetBindName(i);
						part.BindBone[i] = !string.IsNullOrEmpty(nm) ? _sk.FindBone(nm) : skin.GetBindBone(i);
					}
				}
				else
				{
					Node? n = mi.GetParent();
					while (n != null && n is not BoneAttachment3D && n != m) n = n.GetParent();
					if (n is not BoneAttachment3D ba) continue;
					part.AttachBone = _sk.FindBone(ba.BoneName);
					part.Attach = ba;
					string kind = ba.Name.ToString().ToLowerInvariant() == "tails" ? "tail" : ba.Name.ToString().ToLowerInvariant();
					part.PropName = kind + _parts.Count(p => !p.Skinned);
				}
				LoadVerts(part);
				_parts.Add(part);
			}
			_sk.ResetBonePoses();
			_bonePose = new Transform3D[_sk.GetBoneCount()];
			Skin();
			var all = new List<(Part, int)>();
			foreach (var p in _parts) for (int i = 0; i < p.P.Count; i++) all.Add((p, i));
			var restP = all.Select(v => Pos(v.Item1, v.Item2)).ToArray();
			var bodyY = Enumerable.Range(0, all.Count).Where(k => all[k].Item1.Skinned).Select(k => restP[k].Y).ToList();
			Height = bodyY.Max() - bodyY.Min();
			float s = Height / 1.75f;
			int hips = _sk.FindBone("Hips");
			Vector3 hipsHead = _sk.GlobalTransform * _sk.GetBoneGlobalRest(hips).Origin;
			float cell = 0.025f * s;
			var seen = new HashSet<(int, int, int, string)>();
			var vb = new List<int>(); var vg = new List<string>(); var vd = new List<float>(); var vRest = new List<Vector3>();
			for (int k = 0; k < all.Count; k++)
			{
				var (p, i) = all[k];
				int bone = -1; float dom = 1f; string g;
				if (p.Skinned)
				{
					(bone, dom) = Dominant(p, i);
					if (bone < 0) continue;
					g = _sk.GetBoneName(bone);
					// a rigid tail weighted to the hips (Tanuki true form) is its own volume, not the pelvis
					if (bone == hips && (restP[k] - hipsHead).Length() > 0.28f * s) g = "tail";
				}
				else g = "prop:" + p.PropName;
				var c = (Mathf.FloorToInt(restP[k].X / cell), Mathf.FloorToInt(restP[k].Y / cell), Mathf.FloorToInt(restP[k].Z / cell), g);
				if (!seen.Add(c)) continue;
				_v.Add(all[k]); vb.Add(bone); vg.Add(g); vd.Add(dom); vRest.Add(restP[k]);
			}
			_vBone = vb.ToArray(); _vGroup = vg.ToArray(); _vDom = vd.ToArray();
			int nv = _v.Count;
			_footL = new bool[nv]; _footR = new bool[nv]; _wp = new Vector3[nv];
			for (int k = 0; k < nv; k++)
			{
				string g = _vGroup[k].ToLowerInvariant();
				if (g.Contains("foot") || g.Contains("toe"))
				{
					if (g.Contains("left")) _footL[k] = true;
					else if (g.Contains("right")) _footR[k] = true;
				}
			}
			RestFootMin = Enumerable.Range(0, nv).Where(k => _footL[k] || _footR[k]).Select(k => vRest[k].Y).DefaultIfEmpty(0f).Min();
			foreach (var grp in Enumerable.Range(0, nv).GroupBy(k => _vGroup[k]))
			{
				var idx = grp.ToList();
				if (idx.Count < 12) continue;
				var cap = FitCapsule(grp.Key, idx.Select(k => vRest[k]).ToList());
				Transform3D frame;
				if (grp.Key.StartsWith("prop:"))
				{
					cap.Prop = _v[idx[0]].Part;
					frame = PropXform(cap.Prop);
				}
				else
				{
					cap.Bone = grp.Key == "tail" ? hips : _sk.FindBone(grp.Key);
					frame = _sk.GlobalTransform * _bonePose[cap.Bone];
				}
				var inv = frame.AffineInverse();
				cap.A = inv * cap.A; cap.B = inv * cap.B;
				_caps.Add(cap);
			}
			foreach (var a in _caps)
				foreach (var g in _vGroup.Distinct())
					if (Adjacent(g, a.Name)) _skip.Add((g, a.Name));
			// rest-pose check: a pair already inside in the T-pose is a fit artefact, not a pose problem
			for (int k = 0; k < nv; k++) _wp[k] = vRest[k];
			UpdateCaps();
			for (int k = 0; k < nv; k++)
				foreach (var c in _caps)
					if (!_skip.Contains((_vGroup[k], c.Name)) && SegDist(_wp[k], c.WA, c.WB) < c.Core)
						_skip.Add((_vGroup[k], c.Name));
		}

		/// <summary>Parts allowed to touch: same part, parent/child, the torso chain among itself, upper arm vs the
		/// chest, the club in its hand, the shell on the back, the tails on the hips and among themselves.</summary>
		private bool Adjacent(string v, string cap)
		{
			if (v == cap) return true;
			bool vp = v.StartsWith("prop:"), cp = cap.StartsWith("prop:");
			if (vp && cp) return true;
			string vl = v.ToLowerInvariant(), cl = cap.ToLowerInvariant();
			if (vp || cp)
			{
				string prop = vp ? vl : cl, other = vp ? cap : v, ol = other.ToLowerInvariant();
				if (prop.StartsWith("prop:club")) return ol is "righthand" or "rightforearm";
				if (prop.StartsWith("prop:shell")) return IsTorso(other) && !ol.Contains("head") && !ol.Contains("neck") || ol == "tail";
				if (prop.StartsWith("prop:tail")) return ol == "hips" || ol.StartsWith("spine") || ol == "tail";
				return false;
			}
			if (vl == "tail" || cl == "tail")
			{
				string other = vl == "tail" ? cl : vl;
				return other == "hips" || other.StartsWith("spine");
			}
			int vb = _sk.FindBone(v), cb = _sk.FindBone(cap);
			if (vb < 0 || cb < 0) return false;
			if (_sk.GetBoneParent(vb) == cb || _sk.GetBoneParent(cb) == vb) return true;
			if (IsTorso(v) && IsTorso(cap)) return true;
			bool UpperArm(string n) => n.EndsWith("arm") && !n.Contains("fore");
			if ((UpperArm(vl) && IsTorso(cap) && !cl.Contains("head")) || (UpperArm(cl) && IsTorso(v) && !vl.Contains("head"))) return true;
			// the thigh against the pelvis and the lowest spine bone (the hip joint)
			if ((vl.EndsWith("upleg") && (cl == "hips" || cl == "spine" || cl == "spine02")) || (cl.EndsWith("upleg") && (vl == "hips" || vl == "spine" || vl == "spine02"))) return true;
			return false;
		}

		private static Cap FitCapsule(string name, List<Vector3> pts)
		{
			var mean = pts.Aggregate(Vector3.Zero, (a, b) => a + b) / pts.Count;
			float xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0;
			foreach (var p in pts) { var d = p - mean; xx += d.X * d.X; xy += d.X * d.Y; xz += d.X * d.Z; yy += d.Y * d.Y; yz += d.Y * d.Z; zz += d.Z * d.Z; }
			var ax = new Vector3(0.3f, 1f, 0.2f).Normalized();
			for (int it = 0; it < 50; it++)
				ax = new Vector3(xx * ax.X + xy * ax.Y + xz * ax.Z, xy * ax.X + yy * ax.Y + yz * ax.Z, xz * ax.X + yz * ax.Y + zz * ax.Z).Normalized();
			var t = pts.Select(p => (p - mean).Dot(ax)).OrderBy(x => x).ToList();
			float t0 = t[(int)(t.Count * 0.08)], t1 = t[(int)(t.Count * 0.92)];
			var cap = new Cap { Name = name, A = mean + ax * t0, B = mean + ax * t1 };
			var r = pts.Select(p => SegDist(p, cap.A, cap.B)).OrderBy(x => x).ToList();
			cap.R = r[r.Count / 2];
			cap.Core = 0.6f * cap.R;
			var dir = cap.B - cap.A; float len = dir.Length();
			if (len > 2f * cap.Core) { dir /= len; cap.A += dir * cap.Core; cap.B -= dir * cap.Core; }
			return cap;
		}

		private static float SegDist(Vector3 p, Vector3 a, Vector3 b)
		{
			var ab = b - a; float l2 = ab.LengthSquared();
			float t = l2 > 1e-9f ? Mathf.Clamp((p - a).Dot(ab) / l2, 0f, 1f) : 0f;
			return p.DistanceTo(a + ab * t);
		}

		/// <summary>BoneAttachment3D global = skeleton global * bone global pose; below it only local transforms (tail sway).</summary>
		private Transform3D PropXform(Part p) =>
			_sk.GlobalTransform * _bonePose[p.AttachBone] * (p.Attach!.GlobalTransform.AffineInverse() * p.Mi.GlobalTransform);

		private void UpdateCaps()
		{
			foreach (var c in _caps)
			{
				var t = c.Prop is not null ? PropXform(c.Prop) : _sk.GlobalTransform * _bonePose[c.Bone];
				c.WA = t * c.A; c.WB = t * c.B;
			}
		}

		private (int Bone, float W) Dominant(Part p, int i)
		{
			var w = p.Bw[i]; int best = 0;
			for (int j = 1; j < w.Length; j++) if (w[j] > w[best]) best = j;
			return (p.BindBone[p.Bi[i][best]], w[best]);
		}

		private static void LoadVerts(Part p)
		{
			var mesh = p.Mi.Mesh;
			for (int s = 0; s < mesh.GetSurfaceCount(); s++)
			{
				var arr = mesh.SurfaceGetArrays(s);
				var pos = arr[(int)Mesh.ArrayType.Vertex].AsVector3Array();
				int[] bones = p.Skinned ? arr[(int)Mesh.ArrayType.Bones].AsInt32Array() : Array.Empty<int>();
				float[] wts = p.Skinned ? arr[(int)Mesh.ArrayType.Weights].AsFloat32Array() : Array.Empty<float>();
				int per = p.Skinned && pos.Length > 0 ? bones.Length / pos.Length : 0;
				for (int i = 0; i < pos.Length; i++)
				{
					p.P.Add(pos[i]);
					if (p.Skinned)
					{
						var bi = new int[per]; var bw = new float[per];
						Array.Copy(bones, i * per, bi, 0, per); Array.Copy(wts, i * per, bw, 0, per);
						p.Bi.Add(bi); p.Bw.Add(bw);
					}
				}
			}
		}

		private void Skin()
		{
			for (int b = 0; b < _bonePose.Length; b++) _bonePose[b] = _sk.GetBoneGlobalPose(b);
		}

		private Vector3 Pos(Part p, int i)
		{
			if (!p.Skinned) return PropXform(p) * p.P[i];
			var bi = p.Bi[i]; var bw = p.Bw[i];
			Vector3 acc = Vector3.Zero;
			for (int j = 0; j < bi.Length; j++)
			{
				float w = bw[j];
				if (w <= 0f) continue;
				int bone = p.BindBone[bi[j]];
				if (bone < 0) continue;
				acc += w * ((_bonePose[bone] * p.Bind[bi[j]]) * p.P[i]);
			}
			return _sk.GlobalTransform * acc;
		}

		public string Summary() =>
			$"height {Height:0.000} m, rest foot min {RestFootMin * 100f:0.0} cm, sampled {_v.Count} verts, capsules {_caps.Count} (" +
			string.Join(", ", _caps.Select(c => $"{c.Name} r{c.R * 100f:0.0}")) + $"), skip pairs {_skip.Count}, feet {_footL.Count(x => x)}/{_footR.Count(x => x)}";

		public FrameM Measure(int frame, FrameM? prev)
		{
			Skin();
			int nv = _v.Count;
			for (int k = 0; k < nv; k++) { var (p, i) = _v[k]; _wp[k] = Pos(p, i); }
			UpdateCaps();
			var fm = new FrameM { Frame = frame, FootL = float.MaxValue, FootR = float.MaxValue, BodyMin = float.MaxValue };
			Vector2 sl = Vector2.Zero, sr = Vector2.Zero; int nl = 0, nr = 0;
			for (int k = 0; k < nv; k++)
			{
				if (_vBone[k] < 0) continue;
				float y = _wp[k].Y;
				if (y < fm.BodyMin) fm.BodyMin = y;
				if (_footL[k]) { if (y < fm.FootL) fm.FootL = y; sl += new Vector2(_wp[k].X, _wp[k].Z); nl++; }
				if (_footR[k]) { if (y < fm.FootR) fm.FootR = y; sr += new Vector2(_wp[k].X, _wp[k].Z); nr++; }
			}
			fm.PosL = nl > 0 ? sl / nl : Vector2.Zero; fm.PosR = nr > 0 ? sr / nr : Vector2.Zero;
			const float contact = 0.015f;
			fm.SlipL = Slip(_footL, prev?.Sole, contact);
			fm.SlipR = Slip(_footR, prev?.Sole, contact);
			fm.Sole = (Vector3[])_wp.Clone();

			var agg = new Dictionary<(string, string), (int Count, float Depth)>();
			for (int k = 0; k < nv; k++)
			{
				if (_vBone[k] >= 0 && _vDom[k] < 0.6f) continue; // seam vertices blend two bones
				string g = _vGroup[k];
				foreach (var c in _caps)
				{
					if (_skip.Contains((g, c.Name))) continue;
					float d = SegDist(_wp[k], c.WA, c.WB);
					if (d >= c.Core) continue;
					var key = (Coarse(g), Coarse(c.Name));
					agg.TryGetValue(key, out var a);
					agg[key] = (a.Count + 1, Math.Max(a.Depth, c.R - d));
				}
			}
			foreach (var ((pr, tg), a) in agg)
				if (a.Count >= 3) fm.Hits.Add((pr, tg, a.Count, a.Depth));
			float sink = Math.Max(0f, -Math.Min(fm.FootL, fm.FootR)) * 100f;
			float flt = Math.Max(0f, Math.Min(fm.FootL, fm.FootR)) * 100f;
			double inter = fm.Hits.Sum(h => h.Depth * 100f * Math.Min(1.0, h.Count / 8.0));
			fm.Badness = sink + 0.5 * flt + Math.Max(fm.SlipL, fm.SlipR) + 2 * inter;
			return fm;
		}

		/// <summary>Bone or prop name for the report (LeftForeArm -> L.forearm, prop:club2 -> club).</summary>
		private static string Coarse(string n)
		{
			string l = n.ToLowerInvariant();
			if (l.StartsWith("prop:")) return new string(l[5..].TakeWhile(char.IsLetter).ToArray());
			string side = l.StartsWith("left") ? "L." : l.StartsWith("right") ? "R." : "";
			return side + l.Replace("left", "").Replace("right", "");
		}

		private float Slip(bool[] foot, Vector3[]? prev, float contact)
		{
			if (prev is null) return 0f;
			var d = new List<float>();
			for (int k = 0; k < _wp.Length; k++)
			{
				if (!foot[k]) continue;
				if (_wp[k].Y > contact || prev[k].Y > contact) continue;
				d.Add(new Vector2(_wp[k].X - prev[k].X, _wp[k].Z - prev[k].Z).Length() * 100f);
			}
			if (d.Count < 3) return 0f;
			d.Sort();
			return d[d.Count / 2];
		}
	}
}
