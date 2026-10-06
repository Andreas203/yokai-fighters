#!/usr/bin/env python3
"""Measure a Meshy animation GLB for the clip gate (F2, G4). numpy only.

Usage: python tools/measure_clip.py <clip.glb> [--no-mesh] [--json out.json]

Resamples the clip to 60 ticks (linear / slerp), runs FK and linear-blend skinning
and reports: ticks, root drift/sway, planted-foot shift, max joint rotation per tick,
end-effector reach/speed (hands, feet), stretched edges (sleeve vs hakama), arm vertices
inside the torso core, hair-to-hakama distance and a proxy tail fan (9 capsules on Hips).
Method limits are the same as docs/assets/retarget_gate.md: proxies, not the real verdict;
rendered stills are the verdict.
"""
import json, struct, sys
import numpy as np

CT = {5120: np.int8, 5121: np.uint8, 5122: np.int16, 5123: np.uint16, 5125: np.uint32, 5126: np.float32}
NC = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}


def load_glb(path):
    b = open(path, "rb").read()
    _, _, _ = struct.unpack("<III", b[:12])
    off, js, binc = 12, None, None
    while off < len(b):
        ln, ty = struct.unpack("<II", b[off:off + 8])
        d = b[off + 8:off + 8 + ln]
        if ty == 0x4E4F534A:
            js = json.loads(d)
        elif ty == 0x004E4942:
            binc = d
        off += 8 + ln
    return js, binc


def acc(js, binc, i):
    a = js["accessors"][i]
    bv = js["bufferViews"][a["bufferView"]]
    n, c = a["count"], NC[a["type"]]
    dt = np.dtype(CT[a["componentType"]])
    start = bv.get("byteOffset", 0) + a.get("byteOffset", 0)
    stride = bv.get("byteStride", 0)
    if stride and stride != dt.itemsize * c:
        raw = np.frombuffer(binc, np.uint8, count=stride * n, offset=start).reshape(n, stride)
        out = raw[:, :dt.itemsize * c].copy().view(dt).reshape(n, c)
    else:
        out = np.frombuffer(binc, dt, count=n * c, offset=start).reshape(n, c)
    if a.get("normalized"):
        out = out.astype(np.float32) / np.iinfo(dt).max
    return out.astype(np.float64) if out.dtype.kind == "f" else out


def qmul(a, b):
    x1, y1, z1, w1 = a[..., 0], a[..., 1], a[..., 2], a[..., 3]
    x2, y2, z2, w2 = b[..., 0], b[..., 1], b[..., 2], b[..., 3]
    return np.stack([w1 * x2 + x1 * w2 + y1 * z2 - z1 * y2, w1 * y2 - x1 * z2 + y1 * w2 + z1 * x2,
                     w1 * z2 + x1 * y2 - y1 * x2 + z1 * w2, w1 * w2 - x1 * x2 - y1 * y2 - z1 * z2], -1)


def qmat(q):
    x, y, z, w = q
    return np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
                     [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
                     [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])


def slerp(q0, q1, t):
    d = np.dot(q0, q1)
    if d < 0:
        q1, d = -q1, -d
    if d > 0.9995:
        q = q0 + t * (q1 - q0)
    else:
        th = np.arccos(d)
        q = (np.sin((1 - t) * th) * q0 + np.sin(t * th) * q1) / np.sin(th)
    return q / np.linalg.norm(q)


def sample(times, vals, t, rot):
    if t <= times[0]:
        return vals[0]
    if t >= times[-1]:
        return vals[-1]
    i = np.searchsorted(times, t, side="right") - 1
    f = (t - times[i]) / (times[i + 1] - times[i])
    return slerp(vals[i], vals[i + 1], f) if rot else vals[i] * (1 - f) + vals[i + 1] * f


class Clip:
    def __init__(self, path):
        js, binc = load_glb(path)
        self.js = js
        nodes = js["nodes"]
        self.names = [n.get("name", str(i)) for i, n in enumerate(nodes)]
        self.parent = {}
        for i, n in enumerate(nodes):
            for c in n.get("children", []):
                self.parent[c] = i
        sk = js["skins"][0]
        self.joints = sk["joints"]
        self.jidx = {j: k for k, j in enumerate(self.joints)}
        self.ibm = acc(js, binc, sk["inverseBindMatrices"]).reshape(-1, 4, 4).transpose(0, 2, 1)
        # rest TRS
        self.rest_t = np.array([n.get("translation", [0, 0, 0]) for n in nodes], float)
        self.rest_r = np.array([n.get("rotation", [0, 0, 0, 1]) for n in nodes], float)
        self.rest_s = np.array([n.get("scale", [1, 1, 1]) for n in nodes], float)
        an = js["animations"][0]
        self.chan = {}
        tmax = 0
        for ch in an["channels"]:
            s = an["samplers"][ch["sampler"]]
            tt, vv = acc(js, binc, s["input"]).ravel(), acc(js, binc, s["output"])
            self.chan[(ch["target"]["node"], ch["target"]["path"])] = (tt, vv)
            tmax = max(tmax, tt[-1])
        self.tmax = float(tmax)
        keys = [len(v[0]) for v in self.chan.values()]
        self.nkeys = max(keys)
        self.key_dt = self.tmax / max(1, self.nkeys - 1)
        self.ticks = int(round(self.tmax * 60))
        # mesh
        m = js["meshes"][[n["mesh"] for n in nodes if "mesh" in n][0]]
        pos, jj, ww, idx = [], [], [], []
        base = 0
        for p in m["primitives"]:
            a = p["attributes"]
            P = acc(js, binc, a["POSITION"])
            pos.append(P)
            jj.append(acc(js, binc, a["JOINTS_0"]).astype(int))
            ww.append(acc(js, binc, a["WEIGHTS_0"]))
            idx.append(acc(js, binc, p["indices"]).ravel().astype(int) + base)
            base += len(P)
        self.P = np.concatenate(pos)
        self.J = np.concatenate(jj)
        self.W = np.concatenate(ww)
        tri = np.concatenate(idx).reshape(-1, 3)
        e = np.concatenate([tri[:, [0, 1]], tri[:, [1, 2]], tri[:, [2, 0]]])
        e.sort(1)
        self.E = np.unique(e, axis=0)
        self.dom = self.J[np.arange(len(self.J)), self.W.argmax(1)]
        self.jname = [self.names[j] for j in self.joints]

    def pose_world(self, t):
        n = len(self.names)
        T, R, S = self.rest_t.copy(), self.rest_r.copy(), self.rest_s.copy()
        for (node, path), (tt, vv) in self.chan.items():
            v = sample(tt, vv, t, path == "rotation")
            if path == "translation":
                T[node] = v
            elif path == "rotation":
                R[node] = v
            else:
                S[node] = v
        world = [None] * n

        def get(i):
            if world[i] is not None:
                return world[i]
            M = np.eye(4)
            M[:3, :3] = qmat(R[i] / np.linalg.norm(R[i])) * S[i]
            M[:3, 3] = T[i]
            if i in self.parent:
                M = get(self.parent[i]) @ M
            world[i] = M
            return M

        for i in range(n):
            get(i)
        return np.array(world), R

    def run(self, mesh_step=2):
        self.world, self.rot = [], []
        for k in range(self.ticks + 1):
            w, r = self.pose_world(k / 60.0)
            self.world.append(w)
            self.rot.append(r)
        self.world = np.array(self.world)
        self.rot = np.array(self.rot)
        self.jw = self.world[:, self.joints]            # (ticks, J, 4,4)
        self.jp = self.jw[:, :, :3, 3]                  # joint world positions
        self.skin_mats = self.jw @ self.ibm[None]
        return self

    def verts(self, k):
        M = self.skin_mats[k]
        p4 = np.concatenate([self.P, np.ones((len(self.P), 1))], 1)
        out = np.zeros((len(self.P), 3))
        for c in range(4):
            out += self.W[:, c:c + 1] * np.einsum("nij,nj->ni", M[self.J[:, c]], p4)[:, :3]
        return out

    def jn(self, name):
        return self.jname.index(name)


def seg_dist(P, a, b):
    """distance from points P to segment a-b"""
    ab = b - a
    t = np.clip(((P - a) @ ab) / (ab @ ab + 1e-12), 0, 1)
    return np.linalg.norm(P - (a + t[:, None] * ab), axis=1)


def stripped_planted(c, t0=0, t1=None):
    """Planted-foot slide (cm) if the root's xz travel is removed (what happens in-game if movement is not
    replayed from move data). Planted = foot within 3 cm of its lowest point. Window [t0, t1] in ticks."""
    t1 = c.ticks if t1 is None else t1
    H = c.jn("Hips")
    root = c.jp[:, H][:, [0, 2]]
    out = {}
    for nm in ("LeftFoot", "RightFoot"):
        p = c.jp[:, c.jn(nm)]
        ymin = p[t0:t1 + 1, 1].min()
        xz = p[:, [0, 2]] - (root - root[t0])
        worst = 0.0
        start = None
        for k in range(t0, t1 + 2):
            m = k <= t1 and p[k, 1] < ymin + 0.03
            if m and start is None:
                start = k
            if not m and start is not None:
                if k - start >= 4:
                    worst = max(worst, float(np.linalg.norm(xz[k - 1] - xz[start]) * 100))
                start = None
        out[nm] = round(worst, 1)
    return out


def strike_window(c, name, frac_hi=0.85, frac_lo=0.05):
    """Reach fraction of an end effector forward of the hips (z), between its guard value and its peak.
    Returns trim-relevant ticks: rise_start, hit_start (first >= frac_hi), peak, hit_end (last >= frac_hi), back_to_guard."""
    H = c.jn("Hips")
    rel = (c.jp[:, c.jn(name)] - c.jp[:, H])[:, 2]
    k = int(rel.argmax())
    guard = float(np.median(rel[:max(3, c.ticks // 10)])) if k > 10 else float(rel.min())
    guard = min(guard, float(rel[:k + 1].min()) + 0.05) if k > 0 else guard
    fr = (rel - guard) / max(rel[k] - guard, 1e-6)
    hs = int(np.argmax(fr >= frac_hi))
    he = hs
    for t in range(hs, len(fr)):
        if fr[t] >= frac_hi:
            he = t
        elif t > k:
            break
    rs = hs
    while rs > 0 and fr[rs - 1] < fr[rs] and fr[rs - 1] >= frac_lo:
        rs -= 1
    bg = he
    while bg < len(fr) - 1 and fr[bg] > 0.15:
        bg += 1
    return {"effector": name, "peak_tick": k, "peak_forward_cm": round(float(rel[k] * 100), 1), "guard_forward_cm": round(guard * 100, 1),
            "rise_start": rs, "hit_start": hs, "hit_end": he, "back_to_guard": bg}


def analyze(path, mesh=True, mesh_step=2):
    c = Clip(path).run()
    r = {"file": path, "keys": c.nkeys, "key_fps": round(1 / c.key_dt, 1) if c.key_dt else None,
         "duration_s": round(c.tmax, 4), "ticks": c.ticks, "poses": c.ticks + 1}
    H = c.jn("Hips")
    hp = c.jp[:, H]
    r["root_drift_end_minus_start_cm"] = [round(float(x) * 100, 1) for x in (hp[-1] - hp[0])]
    r["root_range_cm_xyz"] = [round(float(x) * 100, 1) for x in (hp.max(0) - hp.min(0))]
    r["root_travel_z_cm"] = round(float((hp[:, 2].max() - hp[:, 2].min()) * 100), 1)
    # joint rotation per tick (local quats)
    dq = np.abs((c.rot[1:] * c.rot[:-1]).sum(-1)).clip(0, 1)
    ang = np.degrees(2 * np.arccos(dq))
    jang = ang[:, c.joints]
    mx = np.unravel_index(jang.argmax(), jang.shape)
    r["max_joint_deg_per_tick"] = [round(float(jang.max()), 1), c.jname[mx[1]], int(mx[0])]
    # end effectors
    ee = {}
    for nm in ["LeftHand", "RightHand", "LeftFoot", "RightFoot", "LeftToeBase", "RightToeBase", "Head"]:
        if nm in c.jname:
            p = c.jp[:, c.jn(nm)]
            rel = p - hp
            sp = np.linalg.norm(np.diff(p, axis=0), axis=1) * 60
            ee[nm] = {"peak_speed_mps": round(float(sp.max()), 2) if len(sp) else 0, "peak_speed_tick": int(sp.argmax()) if len(sp) else 0,
                      "max_forward_cm": round(float(rel[:, 2].max() * 100), 1), "max_forward_tick": int(rel[:, 2].argmax()),
                      "max_height_cm": round(float(p[:, 1].max() * 100), 1), "min_height_cm": round(float(p[:, 1].min() * 100), 1)}
            ee[nm]["forward_cm_series_every6"] = [round(float(x) * 100, 0) for x in rel[::6, 2]]
    r["effectors"] = ee
    # planted foot shift
    plant = {}
    for nm in ["LeftFoot", "RightFoot"]:
        p = c.jp[:, c.jn(nm)]
        ymin = p[:, 1].min()
        mask = p[:, 1] < ymin + 0.03
        runs, start = [], None
        for k, m in enumerate(list(mask) + [False]):
            if m and start is None:
                start = k
            if not m and start is not None:
                if k - start >= 4:
                    seg = p[start:k, [0, 2]]
                    sp = np.linalg.norm(np.diff(seg, axis=0), axis=1) * 60
                    runs.append({"ticks": [start, k - 1], "shift_cm": round(float(np.linalg.norm(seg[-1] - seg[0]) * 100), 1),
                                 "mean_speed_mps": round(float(sp.mean()), 2) if len(sp) else 0, "std": round(float(sp.std()), 2) if len(sp) else 0})
                start = None
        plant[nm] = {"min_y_cm": round(float(ymin * 100), 1), "runs": runs[:4],
                     "max_shift_cm": max([x["shift_cm"] for x in runs], default=0)}
    r["planted"] = plant
    r["planted_if_root_stripped"] = stripped_planted(c)
    # first/last ticks pose
    r["head_y_cm_start_end"] = [round(float(c.jp[0, c.jn("Head")][1] * 100), 1), round(float(c.jp[-1, c.jn("Head")][1] * 100), 1)]
    r["hips_y_cm_start_end"] = [round(float(hp[0, 1] * 100), 1), round(float(hp[-1, 1] * 100), 1)]
    r["hips_tilt_deg_start_end"] = [round(float(np.degrees(np.arccos(np.clip(c.jw[k, H, 1, 1], -1, 1)))), 1) for k in (0, -1)]
    # facing: spine up axis (head - hips) angle from vertical
    up = c.jp[:, c.jn("Head")] - hp
    r["torso_from_vertical_deg"] = [round(float(np.degrees(np.arccos(up[k, 1] / np.linalg.norm(up[k])))), 0) for k in (0, len(up) // 2, -1)]
    if mesh:
        ks = list(range(0, c.ticks + 1, mesh_step))
        V0 = c.verts(0)
        # bind lengths from bind pose positions
        L0 = np.linalg.norm(c.P[c.E[:, 0]] - c.P[c.E[:, 1]], axis=1)
        valid = L0 > 1e-5
        dn = np.array(c.jname)[c.dom]
        sleeve = np.isin(dn, [n for n in c.jname if any(s in n for s in ("Shoulder", "Arm", "ForeArm", "Hand"))])
        hak = np.isin(dn, [n for n in c.jname if any(s in n for s in ("UpLeg", "Leg", "Foot", "Toe"))] + ["Hips"])
        e_sleeve = sleeve[c.E[:, 0]] | sleeve[c.E[:, 1]]
        e_hak = hak[c.E[:, 0]] & hak[c.E[:, 1]]
        ever = np.zeros(len(c.E), bool)
        worst = 0.0
        worst_tick = 0
        per_tick_sleeve = []
        torso_core_arm = []
        # torso core capsule
        neck = c.jn("Neck") if "Neck" in c.jname else c.jn("Head")
        torso_v = np.isin(dn, ["Spine", "Spine01", "Spine02", "Hips"])
        bind_ax_a, bind_ax_b = c.jp[0, H], c.jp[0, neck]
        rad = 0.6 * np.percentile(seg_dist(V0[torso_v], bind_ax_a, bind_ax_b), 30)
        arm_idx = np.where(sleeve)[0]
        # hair proxy: verts behind torso (z < -0.08 relative to hips) between hips and neck height, dominated by spine/hips/head
        hair = (~sleeve) & (V0[:, 2] < c.jp[0, H][2] - 0.07) & (V0[:, 1] > c.jp[0, H][1] - 0.2) & (V0[:, 1] < c.jp[0, neck][1] + 0.05)
        hak_v = hak & (V0[:, 1] < c.jp[0, H][1] + 0.02) & ~hair
        hair_hak_min = []
        tails_clear = []
        tails_floor = []
        # proxy tail fan: 9 capsules from hips back/up, +-55 deg fan in hips-local XY plane (fan around local Z, pointing back -Z/up)
        fan = np.radians(np.linspace(-55, 55, 9))
        tlen, trad = 0.5, 0.07
        leg_v = hak & (V0[:, 1] < c.jp[0, H][1])
        for k in ks:
            V = c.verts(k)
            L = np.linalg.norm(V[c.E[:, 0]] - V[c.E[:, 1]], axis=1)
            ratio = np.where(valid, L / np.maximum(L0, 1e-9), 1)
            over = ratio > 2
            ever |= over
            if ratio.max() > worst:
                worst, worst_tick = float(ratio.max()), k
            per_tick_sleeve.append(int((over & e_sleeve).sum()))
            torso_core_arm.append(int((seg_dist(V[arm_idx], c.jp[k, H], c.jp[k, neck]) < rad).sum()))
            if hair.any() and hak_v.any():
                A, B = V[hair], V[hak_v]
                sa = A[::3]
                sb = B[::3]
                d = np.linalg.norm(sa[:, None] - sb[None], axis=2).min()
                hair_hak_min.append(float(d))
            Hm = c.jw[k, c.joints[H]]
            O = Hm[:3, 3] + Hm[:3, :3] @ np.array([0, 0.05, -0.08])
            dmin, ymin = 9.0, 9.0
            for a in fan:
                dl = np.array([np.sin(a), 0.5, -0.85])
                dl = dl / np.linalg.norm(dl)
                tip = O + Hm[:3, :3] @ (dl * tlen)
                ymin = min(ymin, tip[1] - trad)
                dmin = min(dmin, float((seg_dist(V[leg_v][::2], O, tip) - trad).min()))
            tails_clear.append(dmin)
            tails_floor.append(ymin)
        r["mesh"] = {"ticks_sampled_every": mesh_step, "edges_stretched_gt2x_ever": int(ever.sum()),
                     "of_which_sleeve": int((ever & e_sleeve).sum()), "of_which_hakama_hips": int((ever & e_hak).sum()),
                     "max_stretch_ratio": round(worst, 1), "max_stretch_tick": worst_tick,
                     "sleeve_stretched_per_tick_max": max(per_tick_sleeve), "sleeve_stretched_tick_of_max": ks[int(np.argmax(per_tick_sleeve))],
                     "arm_verts_in_torso_core_bind_max": [torso_core_arm[0], max(torso_core_arm), ks[int(np.argmax(torso_core_arm))], int(len(arm_idx))],
                     "hair_to_hakama_min_cm_bind_clip": [round(hair_hak_min[0] * 100, 1), round(min(hair_hak_min) * 100, 1)] if hair_hak_min else None,
                     "tail_proxy_leg_clearance_min_cm": round(min(tails_clear) * 100, 1), "tail_proxy_min_clear_tick": ks[int(np.argmin(tails_clear))],
                     "tail_proxy_lowest_y_cm": round(min(tails_floor) * 100, 1), "tail_proxy_lowest_tick": ks[int(np.argmin(tails_floor))],
                     "tail_clip_floor_ticks": [k for k, y in zip(ks, tails_floor) if y < -0.01][:3], "tail_clip_floor_count": int(sum(y < -0.01 for y in tails_floor))}
    return r


if __name__ == "__main__":
    import argparse
    ap = argparse.ArgumentParser()
    ap.add_argument("glb")
    ap.add_argument("--no-mesh", action="store_true")
    ap.add_argument("--json")
    a = ap.parse_args()
    res = analyze(a.glb, mesh=not a.no_mesh)
    s = json.dumps(res, indent=1)
    if a.json:
        open(a.json, "w").write(s)
    else:
        print(s)
