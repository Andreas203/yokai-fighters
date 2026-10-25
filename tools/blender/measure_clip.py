"""Measure a clip on the shared skeleton (rules F2, F5, F6, G4). YOK-110.

Run (Blender 4.1.1):
  /Applications/Blender.app/Contents/MacOS/Blender -b [<take.blend>] --python tools/blender/measure_clip.py -- \
      --clip <clip.glb>            # clip to measure (omit to measure the opened .blend's action)
      [--data data/clips/<id>.json]  # take trim_start, trim_end, speed_scale, hit_start, hit_end from a clip match
      [--trim A B] [--speed S]     # raw ticks (60/s, inclusive) and raw ticks per game frame; override --data
      [--hit A B]                  # game frames (1-based, as retimed) of the visible hit; override --data
      [--id <name>]                # report/still name (default: --data id, else the file name)
      [--out <dir>]                # report.json goes here (default ./clip_measure_out)
      [--stills-dir <dir>]         # default assets/specs/clips/stills
      [--no-render]                # skip the stills
      [--as-stripped]              # measure feet as if the root's ground travel were removed (what the game
                                   #   shows, since movement is move data); use on raw takes with root motion
      [--plant-height 0.03] [--plant-min-frames 4]
      [--slide-reject 2.0] [--slide-warn 1.0]       # worst planted-run mean slide, cm per frame
      [--jitter-reject 40] [--jitter-warn 20]       # joint angular acceleration, deg per frame^2
      [--movement-clip]            # walks, dashes, hit reactions, get-ups, knockdowns: the feet are meant to
                                   #   travel, so slide is reported but not judged

Game frame n shows raw tick A + (n - 1) * S, so a take is measured exactly as the engine plays it
(`trim_start`, `trim_end`, `speed_scale`); frames of the report are game frames (1-based, 60 per second).
Without --trim the whole clip is measured at 1x, which is what a cleaned clip (clean_clip.py) should be.

report.json:
  frames_total        game frames at 60 fps after trim and speed
  planted_feet        per foot: planted runs (frames, net and per-frame ground movement of the ankle, cm per frame);
                      "planted" = ankle within --plant-height of the clip's lowest ankle for >= --plant-min-frames
  joint_jitter        angular acceleration of every bone's local rotation per frame (deg/frame^2): worst frame,
                      top spikes, p99, and sign reversals of angular velocity (the zig-zag that reads as shake)
  hit                 the hit frames used, joint rates there, and (always) a suggested hit window from the striking
                      end effector's reach (first/last frame at >= 85% of peak reach), to check against the stills
  root                ground travel and height change of the root bone (cm)
  verdict             PASS / WARN / FAIL on slide and joint acceleration against the thresholds above (first-pass,
                      proposed from Ryo's accepted clips); velocity reversals are INFO only
  stills              one PNG per hit frame: front view above, side view below, skeleton drawn as tubes
                      (left red, right blue, centre grey), named <id>_f<NN>.png
The numbers are evidence. The ACCEPT/RETAKE call on a smeared hit pose is made from the stills (F5).
"""
import json
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy
import numpy as np

import clip_common as cc

TOOL = "measure_clip"
VERSION = 1
DEFAULTS = {"plant_height_m": 0.03, "plant_min_frames": 4, "slide_warn_cm_per_frame": 1.0,
            "slide_reject_cm_per_frame": 2.0, "jitter_warn_deg_per_frame2": 20.0,
            "jitter_reject_deg_per_frame2": 40.0, "reversal_min_rate_deg_per_frame": 1.0,
            "strike_fraction": 0.85}
# Shared names; resolved per rig by cc.resolve_bone (YOK-146: `mixamorig:` prefixes, Tripo names). `headfront`
# exists only on the Meshy skeleton and is skipped on Tripo rigs.
TRACK = ["Hips", "Head", "headfront", "LeftHand", "RightHand", "LeftFoot", "RightFoot", "LeftToeBase", "RightToeBase"]


# ---------------------------------------------------------------- math

def qmul(a, b):
    w1, x1, y1, z1 = a[..., 0], a[..., 1], a[..., 2], a[..., 3]
    w2, x2, y2, z2 = b[..., 0], b[..., 1], b[..., 2], b[..., 3]
    return np.stack([w1 * w2 - x1 * x2 - y1 * y2 - z1 * z2, w1 * x2 + x1 * w2 + y1 * z2 - z1 * y2,
                     w1 * y2 - x1 * z2 + y1 * w2 + z1 * x2, w1 * z2 + x1 * y2 - y1 * x2 + z1 * w2], -1)


def rotvec_deg(q):
    """Quaternion (w,x,y,z) -> rotation vector in degrees, shortest way round."""
    q = np.where(q[..., :1] < 0, -q, q)
    s = np.linalg.norm(q[..., 1:], axis=-1, keepdims=True)
    ang = 2 * np.arctan2(s, q[..., :1])
    axis = np.where(s > 1e-12, q[..., 1:] / np.maximum(s, 1e-12), 0.0)
    return np.degrees(ang) * axis


# ---------------------------------------------------------------- sampling

def sample_ticks(t0, t1, speed):
    n = int(math.floor((t1 - t0) / speed + 1e-9)) + 1
    return [t0 + k * speed for k in range(n)]


def sample_positions(arm, ticks):
    """World positions (m) of TRACK bones for each tick -> (n, len(TRACK), 3). Meshes are hidden so the
    depsgraph only evaluates the armature."""
    sc = bpy.context.scene
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    for o in meshes:
        o.hide_viewport = True
    names = [b for b in TRACK if cc.resolve_bone(arm, b)]
    out = np.zeros((len(ticks), len(names), 3))
    for i, t in enumerate(ticks):
        f = int(math.floor(t))
        sc.frame_set(f, subframe=t - f)
        out[i] = cc.bone_world_heads(arm, names)
    for o in meshes:
        o.hide_viewport = False
    return names, out


def sample_rotations(act, ticks):
    """Local rotation quaternions per tick for every animated bone -> (names, (n, bones, 4))."""
    chans = cc.pose_channels(act)
    names = sorted(b for b, p in chans.items() if "rotation_quaternion" in p and len(p["rotation_quaternion"]) == 4)
    q = np.zeros((len(ticks), len(names), 4))
    for j, b in enumerate(names):
        v = cc.eval_channel(chans[b]["rotation_quaternion"], ticks)
        v /= np.linalg.norm(v, axis=1, keepdims=True)
        q[:, j] = cc.fix_quat_signs(v)
    return names, q


# ---------------------------------------------------------------- measurements

def measure_feet(P, names, T, as_stripped):
    idx = {n: i for i, n in enumerate(names)}
    pos = P.copy()
    root = P[:, idx["Hips"], :2]
    if as_stripped:
        pos[:, :, :2] -= (root - root[0])[:, None, :]
    ank = {s: pos[:, idx[s + "Foot"]] for s in ("Left", "Right")}
    floor = min(a[:, 2].min() for a in ank.values())
    res = {"floor_ankle_height_cm": round(float(floor * 100), 1), "as_stripped": as_stripped, "feet": {}}
    worst = 0.0
    for side, a in ank.items():
        planted = a[:, 2] < floor + T["plant_height_m"]
        runs, start = [], None
        for k, m in enumerate(list(planted) + [False]):
            if m and start is None:
                start = k
            if not m and start is not None:
                if k - start >= T["plant_min_frames"]:
                    seg = a[start:k, :2]
                    step = np.linalg.norm(np.diff(seg, axis=0), axis=1) * 100
                    runs.append({"frames": [start + 1, k], "length": k - start,
                                 "net_cm": round(float(np.linalg.norm(seg[-1] - seg[0]) * 100), 2),
                                 "mean_cm_per_frame": round(float(step.mean()), 3),
                                 "peak_cm_per_frame": round(float(step.max()), 3)})
                start = None
        w = max([r["mean_cm_per_frame"] for r in runs], default=0.0)
        worst = max(worst, w)
        res["feet"][side + "Foot"] = {
            "planted_frames": int(planted.sum()), "runs": runs,
            "worst_run_mean_cm_per_frame": w,
            "worst_run_peak_cm_per_frame": max([r["peak_cm_per_frame"] for r in runs], default=0.0),
            "max_net_cm": max([r["net_cm"] for r in runs], default=0.0)}
    res["worst_cm_per_frame"] = round(worst, 3)
    return res


def measure_jitter(act, ticks, T, hit):
    names, q = sample_rotations(act, ticks)
    n = len(ticks)
    out = {"bones": len(names), "fps": cc.FPS, "unit": "deg per frame^2 (angular acceleration), deg per frame (rate)"}
    if n < 3:
        out.update({"max_accel": 0.0, "note": "fewer than 3 frames"})
        return out, np.zeros(n)
    w = rotvec_deg(qmul(q[1:], q[:-1] * np.array([1, -1, -1, -1])))     # (n-1, bones, 3): frame i -> i+1
    acc = np.linalg.norm(w[1:] - w[:-1], axis=2)                          # (n-2, bones): centred on frame i+1 (0-based)
    rate = np.linalg.norm(w, axis=2)
    per_frame = np.zeros(n)
    per_frame[1:-1] = acc.max(1)
    f, b = np.unravel_index(acc.argmax(), acc.shape)
    flat = acc.ravel()
    order = np.argsort(flat)[::-1]
    spikes, seen = [], set()
    for o in order:
        fi, bi = divmod(int(o), acc.shape[1])
        if (fi, names[bi]) in seen:
            continue
        seen.add((fi, names[bi]))
        spikes.append({"frame": fi + 2, "bone": names[bi], "accel": round(float(acc[fi, bi]), 2)})
        if len(spikes) == 6:
            break
    dots = (w[1:] * w[:-1]).sum(2)
    mag = rate[1:] * rate[:-1]
    thr = T["reversal_min_rate_deg_per_frame"]
    rev = (dots < 0) & (rate[1:] > thr) & (rate[:-1] > thr)
    rev_by = {names[j]: int(rev[:, j].sum()) for j in range(len(names)) if rev[:, j].sum()}
    out.update({
        "max_accel": round(float(acc.max()), 2), "max_accel_frame": int(f + 2), "max_accel_bone": names[b],
        "p99_accel": round(float(np.percentile(flat, 99)), 2), "median_accel": round(float(np.median(flat)), 3),
        "top_spikes": spikes,
        "max_rate_deg_per_frame": round(float(rate.max()), 2),
        "max_rate_bone": names[int(np.unravel_index(rate.argmax(), rate.shape)[1])],
        "max_rate_frame": int(np.unravel_index(rate.argmax(), rate.shape)[0] + 1),
        "velocity_reversals": int(rev.sum()), "velocity_reversals_by_bone": rev_by,
        "per_frame_max_accel": [round(float(x), 2) for x in per_frame],
    })
    if hit:
        sl = per_frame[hit[0] - 1:hit[1]]
        out["hit_frames_max_accel"] = round(float(sl.max()), 2)
        mask = np.ones(n, bool)
        mask[hit[0] - 1:hit[1]] = False
        out["max_accel_outside_hit"] = round(float(per_frame[mask].max()), 2) if mask.any() else 0.0
    return out, per_frame


def facing_xy(P, names):
    """The shared skeleton faces glTF +Z, which is Blender -Y after import (skeleton.md); clips are in place
    and not yawed, so this is fixed rather than read from a pose."""
    return np.array([0.0, -1.0])


def suggest_hit(P, names, T):
    i = {n: k for k, n in enumerate(names)}
    fwd = facing_xy(P, names)
    best, per = None, {}
    for e in ("LeftHand", "RightHand", "LeftFoot", "RightFoot"):
        if e not in i:
            continue
        rel = ((P[:, i[e], :2] - P[:, i["Hips"], :2]) @ fwd)
        pk = int(rel.argmax())
        guard = float(rel[:pk + 1].min())
        reach = float(rel[pk] - guard)
        fr = (rel - guard) / max(reach, 1e-6)
        hs = int(np.argmax(fr >= T["strike_fraction"]))
        he = hs
        for t in range(hs, len(fr)):
            if fr[t] >= T["strike_fraction"]:
                he = t
            elif t > pk:
                break
        per[e] = {"peak_frame": pk + 1, "peak_reach_cm": round(reach * 100, 1), "hit_start": hs + 1, "hit_end": he + 1}
        if best is None or reach > best["reach"]:
            best = {"effector": e, "reach": reach, **per[e]}
    return {"effector": best["effector"], "peak_frame": best["peak_frame"], "peak_reach_cm": best["peak_reach_cm"],
            "hit_start": best["hit_start"], "hit_end": best["hit_end"], "per_effector": per,
            "fraction": T["strike_fraction"],
            "note": "first/last frame with the effector at >= %d%% of its reach from guard; a suggestion to check against the stills"
                    % round(T["strike_fraction"] * 100)}


# ---------------------------------------------------------------- stills (skeleton tubes, Workbench)

def _side(b):
    return "L" if b.startswith("Left") else "R" if b.startswith("Right") else "C"


def build_figure(arm, sc):
    """Bone segments as bevelled poly curves, one object per side so each gets a colour."""
    objs = {}
    cols = {"L": (0.85, 0.2, 0.2, 1), "R": (0.2, 0.35, 0.9, 1), "C": (0.35, 0.35, 0.35, 1)}
    bones = [b for b in arm.pose.bones if b.parent is not None]
    for s, col in cols.items():
        cu = bpy.data.curves.new("fig_" + s, "CURVE")
        cu.dimensions = "3D"
        cu.bevel_depth = 0.018
        cu.bevel_resolution = 2
        mat = bpy.data.materials.new("fig_" + s)
        mat.diffuse_color = col
        cu.materials.append(mat)
        ob = bpy.data.objects.new("fig_" + s, cu)
        sc.collection.objects.link(ob)
        objs[s] = (cu, ob, [b for b in bones if _side(b.name) == s])
        for _ in objs[s][2]:
            sp = cu.splines.new("POLY")
            sp.points.add(1)
    gc = bpy.data.curves.new("fig_floor", "CURVE")
    gc.dimensions = "3D"
    gc.bevel_depth = 0.006
    gm = bpy.data.materials.new("fig_floor")
    gm.diffuse_color = (0.1, 0.1, 0.1, 1)
    gc.materials.append(gm)
    for a, b in (((-1.5, 0, 0), (1.5, 0, 0)), ((0, -1.5, 0), (0, 1.5, 0))):
        sp = gc.splines.new("POLY")
        sp.points.add(1)
        sp.points[0].co = (*a, 1)
        sp.points[1].co = (*b, 1)
    sc.collection.objects.link(bpy.data.objects.new("fig_floor", gc))   # ground cross at the origin
    return objs


def pose_figure(arm, objs):
    mw = arm.matrix_world
    for s, (cu, ob, bones) in objs.items():
        for sp, b in zip(cu.splines, bones):
            h, p = mw @ b.parent.head, mw @ b.head
            sp.points[0].co = (h.x, h.y, h.z, 1)
            sp.points[1].co = (p.x, p.y, p.z, 1)


def setup_render(sc):
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.render.resolution_x = sc.render.resolution_y = 480
    sc.render.film_transparent = False
    sh = sc.display.shading
    sh.light = "STUDIO"
    sh.color_type = "MATERIAL"
    sh.show_object_outline = True
    if sc.world is None:
        sc.world = bpy.data.worlds.new("measure_clip")
    sc.world.color = (0.9, 0.9, 0.9)
    cd = bpy.data.cameras.new("measure_cam")
    cd.type = "ORTHO"
    cam = bpy.data.objects.new("measure_cam", cd)
    sc.collection.objects.link(cam)
    sc.camera = cam
    return cam, cd


def render_frame(arm, objs, cam, cd, sc, tick, height, path):
    import tempfile
    f = int(math.floor(tick))
    sc.frame_set(f, subframe=tick - f)
    pose_figure(arm, objs)
    hips = arm.matrix_world @ arm.pose.bones[cc.resolve_bone(arm, "Hips")].head
    cd.ortho_scale = height * 1.35
    shots = []
    for view, (off, rot) in {"front": ((0, -10, 0), (math.radians(90), 0, 0)),
                             "side": ((10, 0, 0), (math.radians(90), 0, math.radians(90)))}.items():
        cam.location = (hips.x + off[0], hips.y + off[1], height * 0.5)
        cam.rotation_euler = rot
        tmp = os.path.join(tempfile.mkdtemp(prefix="measure_clip_"), view + ".png")
        sc.render.filepath = tmp
        bpy.ops.render.render(write_still=True)
        shots.append(tmp)
    imgs = []
    for p in shots:
        im = bpy.data.images.load(p)
        w, h = im.size
        imgs.append(np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4))
        bpy.data.images.remove(im)
        os.remove(p)
    comp = np.concatenate([imgs[1], imgs[0]], axis=0)  # pixel rows run bottom-up: side first puts the front view on top
    out = bpy.data.images.new("comp", comp.shape[1], comp.shape[0], alpha=True)
    out.pixels = comp.ravel().tolist()
    out.filepath_raw = path
    out.file_format = "PNG"
    out.save()
    bpy.data.images.remove(out)


# ---------------------------------------------------------------- main

def main():
    args = cc.script_args(TOOL)
    known = {"--clip", "--data", "--trim", "--speed", "--hit", "--id", "--out", "--stills-dir", "--no-render",
             "--as-stripped", "--movement-clip", "--plant-height", "--plant-min-frames", "--slide-reject", "--slide-warn",
             "--jitter-reject", "--jitter-warn"}
    for k in args:
        if k not in known:
            sys.exit("%s: unknown argument %s" % (TOOL, k))
    one = lambda f, d=None, c=str: c(args[f][0]) if f in args and args[f] else d
    T = dict(DEFAULTS)
    for flag, key in (("--plant-height", "plant_height_m"), ("--slide-reject", "slide_reject_cm_per_frame"),
                      ("--slide-warn", "slide_warn_cm_per_frame"), ("--jitter-reject", "jitter_reject_deg_per_frame2"),
                      ("--jitter-warn", "jitter_warn_deg_per_frame2")):
        if flag in args:
            T[key] = float(args[flag][0])
    if "--plant-min-frames" in args:
        T["plant_min_frames"] = int(args["--plant-min-frames"][0])

    data = json.load(open(one("--data"), encoding="utf-8")) if "--data" in args else {}
    clip = one("--clip")
    if not clip and data.get("file"):
        sys.exit("%s: --data gives file %s relative to game/; pass --clip with the GLB path" % (TOOL, data["file"]))
    arm, act = cc.load_clip(clip)
    last = cc.action_last_tick(act)
    t0, t1 = data.get("trim_start", 0), data.get("trim_end", last)
    if "--trim" in args:
        t0, t1 = int(args["--trim"][0]), int(args["--trim"][1])
    speed = one("--speed", data.get("speed_scale", 1.0), float)
    hit = (data.get("hit_start"), data.get("hit_end"))
    if "--hit" in args:
        hit = (int(args["--hit"][0]), int(args["--hit"][1]))
    ticks = sample_ticks(t0, t1, speed)
    n = len(ticks)
    cid = one("--id", data.get("id") or (os.path.splitext(os.path.basename(clip))[0] if clip else "clip"))
    hit = hit if hit[0] and hit[1] and 1 <= hit[0] <= hit[1] <= n else None
    out_dir = os.path.abspath(one("--out", "clip_measure_out"))
    os.makedirs(out_dir, exist_ok=True)

    names, P = sample_positions(arm, ticks)
    idx = {nm: i for i, nm in enumerate(names)}
    for need in ("Hips", "LeftFoot", "RightFoot"):
        if need not in idx:
            sys.exit("%s: bone %s not found; the clip is not on the shared skeleton" % (TOOL, need))
    hips = P[:, idx["Hips"]]
    root = {"ground_travel_cm": round(float(np.linalg.norm(hips[:, :2] - hips[0, :2], axis=1).max() * 100), 1),
            "ground_travel_end_cm": round(float(np.linalg.norm(hips[-1, :2] - hips[0, :2]) * 100), 1),
            "height_range_cm": round(float((hips[:, 2].max() - hips[:, 2].min()) * 100), 1),
            "root_motion_present": bool(np.linalg.norm(hips[:, :2] - hips[0, :2], axis=1).max() > 0.02)}
    feet = measure_feet(P, names, T, "--as-stripped" in args)
    jit, per_frame = measure_jitter(act, ticks, T, hit)
    sug = suggest_hit(P, names, T)
    ee = {}
    fwd = facing_xy(P, names)
    for e in ("LeftHand", "RightHand", "LeftFoot", "RightFoot"):
        if e in idx:
            sp = np.linalg.norm(np.diff(P[:, idx[e]], axis=0), axis=1) * cc.FPS
            ee[e] = {"peak_speed_mps": round(float(sp.max()), 2), "peak_speed_frame": int(sp.argmax()) + 1,
                     "max_forward_of_hips_cm": round(float(((P[:, idx[e], :2] - hips[:, :2]) @ fwd).max() * 100), 1),
                     "max_height_cm": round(float(P[:, idx[e], 2].max() * 100), 1)}

    # verdict
    reasons, state = [], "PASS"
    def raise_to(s, why):
        nonlocal state
        reasons.append(why)
        state = "FAIL" if s == "FAIL" or state == "FAIL" else "WARN"
    slide = feet["worst_cm_per_frame"]
    if "--movement-clip" in args:
        reasons.append("INFO slide not judged (--movement-clip): %.2f cm/frame" % slide)
    elif slide > T["slide_reject_cm_per_frame"]:
        raise_to("FAIL", "foot slide %.2f cm/frame > %.2f" % (slide, T["slide_reject_cm_per_frame"]))
    elif slide > T["slide_warn_cm_per_frame"]:
        raise_to("WARN", "foot slide %.2f cm/frame > %.2f" % (slide, T["slide_warn_cm_per_frame"]))
    ja = jit.get("max_accel", 0)
    if ja > T["jitter_reject_deg_per_frame2"]:
        raise_to("FAIL", "joint acceleration %.1f deg/frame^2 (%s, frame %s) > %.0f"
                 % (ja, jit.get("max_accel_bone"), jit.get("max_accel_frame"), T["jitter_reject_deg_per_frame2"]))
    elif ja > T["jitter_warn_deg_per_frame2"]:
        raise_to("WARN", "joint acceleration %.1f deg/frame^2 (%s, frame %s) > %.0f"
                 % (ja, jit.get("max_accel_bone"), jit.get("max_accel_frame"), T["jitter_warn_deg_per_frame2"]))
    if jit.get("velocity_reversals", 0):
        reasons.append("INFO %d angular-velocity reversals on %s (accepted Ryo clips run 0-12 with real direction changes; "
                       "read them with the stills, not alone)" % (jit["velocity_reversals"], list(jit["velocity_reversals_by_bone"])))
    if root["root_motion_present"] and "--as-stripped" not in args:
        reasons.append("INFO root motion present (%.1f cm); strip it with clean_clip.py or measure with --as-stripped" % root["ground_travel_cm"])

    # stills
    stills = []
    if "--no-render" not in args:
        sc = bpy.context.scene
        for o in bpy.data.objects:
            if o.type == "MESH":
                o.hide_render = True
                o.hide_viewport = True
        objs = build_figure(arm, sc)
        cam, cd = setup_render(sc)
        height = float(P[0, idx["Head"], 2] * 1.25) if "Head" in idx else 1.8
        sdir = os.path.abspath(one("--stills-dir", "assets/specs/clips/stills"))
        os.makedirs(sdir, exist_ok=True)
        pick = [1]
        use = hit if hit else (sug["hit_start"], sug["hit_end"])
        pick += [use[0], (use[0] + use[1]) // 2, use[1]]
        for fr in sorted(set(pick)):
            p = os.path.join(sdir, "%s_f%02d.png" % (cid, fr))
            render_frame(arm, objs, cam, cd, sc, ticks[fr - 1], height, p)
            stills.append(p)

    report = {"tool": "tools/blender/measure_clip.py", "version": VERSION, "blender": bpy.app.version_string,
              "id": cid, "clip": clip or bpy.data.filepath, "sha256": cc.sha256(clip) if clip else None,
              "data": one("--data"), "thresholds": T,
              "window": {"raw_tick_first": t0, "raw_tick_last": t1, "speed_scale": speed,
                         "game_frame_n_shows_raw_tick": "first + (n - 1) * speed_scale"},
              "frames_total": n, "fps": cc.FPS, "duration_s": round(n / cc.FPS, 4),
              "hit": {"given": list(hit) if hit else None, "suggested": sug},
              "root": root, "planted_feet": feet, "joint_jitter": jit, "end_effectors": ee,
              "verdict": {"state": state, "reasons": reasons,
                          "note": "numbers are evidence; a smeared hit pose is judged on the stills (F5)"},
              "stills": stills}
    rp = os.path.join(out_dir, "%s.report.json" % cid)
    cc.write_json(rp, report)
    print("%s: %s %s frames=%d slide=%.2f cm/f accel=%.1f deg/f^2 reversals=%s" %
          (TOOL, cid, state, n, slide, ja, jit.get("velocity_reversals")))
    print("%s: report %s" % (TOOL, rp))
    return 1 if state == "FAIL" else 0


if __name__ == "__main__":
    code = 1
    try:
        code = main()
    except SystemExit as e:
        if e.code not in (0, None):
            print(e.code if isinstance(e.code, str) else "%s: exit %s" % (TOOL, e.code), file=sys.stderr)
            code = 1
        else:
            code = 0
    except Exception:
        import traceback
        traceback.print_exc()
        code = 1
    cc.finish(code)
