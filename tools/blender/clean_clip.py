"""Scripted clip cleanup on the shared skeleton (rules F2, F5, F6; amendment AM5). YOK-110.

Run (Blender 4.1.1):
  /Applications/Blender.app/Contents/MacOS/Blender -b [<take.blend>] --python tools/blender/clean_clip.py -- \
      --clip <raw.glb>             # raw take (omit to clean the action of the opened .blend)
      --out <clean.glb>            # cleaned GLB (60 fps, animation + armature + mesh, as the other clips)
      [--trim A B]                 # raw ticks, inclusive, 60 per second (data/clips trim_start/trim_end);
                                   #   default: tick 0 to the last key. Output frame 1 = raw tick A.
      [--strip-root horizontal|all|none]   # default horizontal: remove the root bone's travel in the ground
                                   #   plane (it stays where the trim starts); `all` also freezes height.
      [--root-bone Hips]
      [--smooth-bones PATTERN ...] # fnmatch patterns on bone names, e.g. LeftHand RightHand "*Spine*"; default none
      [--smooth-paths rotation_quaternion location]   # default rotation_quaternion
      [--cutoff 10] [--order 4]    # Butterworth low-pass cutoff in Hz at 60 fps; order is per pass (see below)
      [--hit A B]                  # OUTPUT frames (1-based, after the trim) whose key poses are never smoothed
      [--hit-margin 1] [--taper 2] # frames kept raw either side of the hit, then a linear ramp back to smoothed
      [--blend <path.blend>]       # also save the cleaned scene
      [--log <path.json>]          # operations log (default: <out>.clean.json)

Order of operations (each one is recorded in the log with its settings, so the take rebuilds from the raw
file by running the `rebuild` command stored in it):
  1. resample + trim: every pose-bone channel is evaluated from the raw keys at raw ticks A..B (60 Hz) with the
     curve's own interpolation (quaternions: components, then renormalised and sign-continuous) and written back
     as linear keys on ticks 0..N-1 (the glTF exporter writes absolute time = tick / 60,
     so the GLB's first key is at time 0 and data/clips trim_start is 0). This is a resample of existing keys, never new poses.
  2. strip root motion: the root bone's location is turned into armature space with the bone's rest matrix, the
     ground-plane components are frozen at their first value (and height too with `all`), and turned back.
  3. Butterworth smoothing: zero-phase (forward + backward, so the effective order is twice --order), odd-reflected
     edges, on the chosen bones/paths only. Hit-frame key poses are restored to their unsmoothed values and the
     filter is faded back in over --taper frames, so the strike pose is never blurred.
  4. export GLB at scene fps 60 (glTF exporter, sampled each frame).

F5: this script only filters, resamples and trims keys that already exist in the take. It never places a pose.
It does not retarget: bone names must already be the shared skeleton's (assets/specs/rigs/skeleton.md).
"""
import fnmatch
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy
import numpy as np
from mathutils import Matrix

import clip_common as cc

TOOL = "clean_clip"
VERSION = 1


# ---------------------------------------------------------------- Butterworth (numpy only; Blender ships no scipy)

def butter_lowpass_sos(cutoff_hz, fs, order):
    """Digital Butterworth low-pass as second-order sections [(b0, b1, b2, a1, a2)], bilinear transform."""
    if order < 2 or order % 2:
        sys.exit("%s: --order must be an even number >= 2" % TOOL)
    if not 0 < cutoff_hz < fs / 2:
        sys.exit("%s: --cutoff must be between 0 and %.1f Hz" % (TOOL, fs / 2))
    k = math.tan(math.pi * cutoff_hz / fs)
    sos = []
    for i in range(order // 2):
        s = math.sin(math.pi * (2 * i + 1) / (2 * order))
        n = 1 + 2 * k * s + k * k
        sos.append((k * k / n, 2 * k * k / n, k * k / n, 2 * (k * k - 1) / n, (1 - 2 * k * s + k * k) / n))
    return sos


def _biquad(x, c):
    b0, b1, b2, a1, a2 = c
    y = np.empty_like(x)
    z1 = z2 = 0.0
    for i, v in enumerate(x):
        o = b0 * v + z1
        z1 = b1 * v - a1 * o + z2
        z2 = b2 * v - a2 * o
        y[i] = o
    return y


def filtfilt(x, sos):
    """Zero-phase filtering of a 1-D signal with odd-reflected padding; the start is initialised on the padded edge."""
    n = len(x)
    pad = min(n - 1, 3 * 2 * len(sos))
    if n < 4 or pad < 1:
        return x.copy()
    left = 2 * x[0] - x[pad:0:-1]
    right = 2 * x[-1] - x[-2:-pad - 2:-1]
    y = np.concatenate([left, x, right])
    for c in sos:
        y = _biquad(y, c)
    y = y[::-1]
    for c in sos:
        y = _biquad(y, c)
    y = y[::-1]
    return y[pad:pad + n]


# ---------------------------------------------------------------- helpers

def quat_angle_deg(a, b):
    d = np.abs((a * b).sum(-1)).clip(0, 1)
    return np.degrees(2 * np.arccos(d))


def normalise(q):
    return q / np.linalg.norm(q, axis=1, keepdims=True)


def main():
    args = cc.script_args(TOOL)
    known = {"--clip", "--out", "--trim", "--strip-root", "--root-bone", "--smooth-bones", "--smooth-paths",
             "--cutoff", "--order", "--hit", "--hit-margin", "--taper", "--blend", "--log"}
    for k in args:
        if k not in known:
            sys.exit("%s: unknown argument %s" % (TOOL, k))

    def one(flag, default=None, cast=str):
        v = args.get(flag)
        if v is None:
            return default
        if len(v) != 1:
            sys.exit("%s: %s takes one value" % (TOOL, flag))
        return cast(v[0])

    clip = one("--clip")
    out = one("--out")
    if not out:
        sys.exit("%s: --out is required" % TOOL)
    strip = one("--strip-root", "horizontal")
    if strip not in ("horizontal", "all", "none"):
        sys.exit("%s: --strip-root must be horizontal, all or none" % TOOL)
    root = one("--root-bone", "Hips")
    cutoff = one("--cutoff", 10.0, float)
    order = one("--order", 4, int)
    margin = one("--hit-margin", 1, int)
    taper = one("--taper", 2, int)
    patterns = args.get("--smooth-bones", [])
    paths = args.get("--smooth-paths", ["rotation_quaternion"])
    for p in paths:
        if p not in ("rotation_quaternion", "location"):
            sys.exit("%s: --smooth-paths accepts rotation_quaternion and location" % TOOL)

    arm, act = cc.load_clip(clip)
    chans = cc.pose_channels(act)
    last = cc.action_last_tick(act)
    trim = args.get("--trim")
    t0, t1 = (int(trim[0]), int(trim[1])) if trim and len(trim) == 2 else (0, last)
    if trim and len(trim) != 2:
        sys.exit("%s: --trim takes two raw ticks" % TOOL)
    if not 0 <= t0 < t1:
        sys.exit("%s: --trim needs 0 <= A < B (got %d %d)" % (TOOL, t0, t1))
    n = t1 - t0 + 1
    hit = args.get("--hit")
    if hit is not None and (len(hit) != 2 or not 1 <= int(hit[0]) <= int(hit[1]) <= n):
        sys.exit("%s: --hit needs two output frames within 1..%d" % (TOOL, n))
    hit = (int(hit[0]), int(hit[1])) if hit else None

    ops = []
    src_key_ticks = sorted({round(k.co[0], 3) for fc in act.fcurves for k in fc.keyframe_points})
    ops.append({"op": "load", "file": clip or bpy.data.filepath, "sha256": cc.sha256(clip) if clip else None,
                "action": act.name, "bones_animated": len(chans), "channels": len(act.fcurves),
                "raw_ticks_first_last": [src_key_ticks[0], src_key_ticks[-1]], "raw_key_count": len(src_key_ticks),
                "note": "glTF imported at 60 fps: Blender frame == raw tick"})

    # 1. resample + trim -----------------------------------------------------------------------------------------
    frames = [t0 + k for k in range(n)]
    data = {}
    for bone, props in chans.items():
        data[bone] = {}
        for prop, fcs in props.items():
            v = cc.eval_channel(fcs, frames)
            if prop == "rotation_quaternion" and v.shape[1] == 4:
                v = cc.fix_quat_signs(normalise(v))
            data[bone][prop] = v
    ops.append({"op": "trim_resample", "raw_tick_first": t0, "raw_tick_last": t1, "fps": cc.FPS, "frames_out": n,
                "interpolation": "as imported (LINEAR for glTF); quaternions renormalised and sign-continuous",
                "output_frame_1_is_raw_tick": t0,
                "head_tail_dropped_ticks": [t0, max(0, last - t1)]})

    # 2. strip root motion ---------------------------------------------------------------------------------------
    if strip != "none":
        if root not in data or "location" not in data[root]:
            sys.exit("%s: root bone %s has no location channels to strip" % (TOOL, root))
        bone = arm.data.bones[root]
        if bone.parent is not None:
            print("%s: WARNING root bone %s has a parent (%s); stripping its location anyway" % (TOOL, root, bone.parent.name))
        m3 = np.array(bone.matrix_local.to_3x3())
        loc = data[root]["location"]
        off = loc @ m3.T                      # bone rest space -> armature space (Z up, cm)
        before = off.copy()
        first = off[0].copy()
        off[:, 0:2] = first[0:2]
        if strip == "all":
            off[:, 2] = first[2]
        data[root]["location"] = off @ np.linalg.inv(m3).T
        d = before - off
        ops.append({"op": "strip_root_motion", "bone": root, "mode": strip,
                    "removed_horizontal_travel_cm": round(float(np.linalg.norm(d[:, 0:2], axis=1).max()), 2),
                    "removed_height_change_cm": round(float(np.abs(d[:, 2]).max()), 2),
                    "removed_travel_xyz_end_cm": [round(float(x), 2) for x in d[-1]],
                    "start_position_kept": [round(float(x), 2) for x in first],
                    "units": "armature space, cm; Z up, character faces -Y"})
    else:
        ops.append({"op": "strip_root_motion", "mode": "none"})

    # 3. Butterworth smoothing -----------------------------------------------------------------------------------
    if patterns:
        sos = butter_lowpass_sos(cutoff, cc.FPS, order)
        w = np.ones(n)
        prot = None
        if hit:
            lo, hi = hit[0] - 1 - margin, hit[1] - 1 + margin
            prot = [max(0, lo) + 1, min(n - 1, hi) + 1]
            idx = np.arange(n)
            dist = np.where(idx < lo, lo - idx, np.where(idx > hi, idx - hi, 0))
            inside = (idx >= lo) & (idx <= hi)
            w = np.where(inside, 0.0, np.clip(dist / (taper + 1.0), 0, 1))
        smoothed = {}
        for bone in sorted(data):
            if not any(fnmatch.fnmatchcase(bone, p) for p in patterns):
                continue
            for prop in paths:
                if prop not in data[bone]:
                    continue
                raw = data[bone][prop]
                sm = np.stack([filtfilt(raw[:, c], sos) for c in range(raw.shape[1])], 1)
                new = raw + w[:, None] * (sm - raw)
                if prop == "rotation_quaternion":
                    new = cc.fix_quat_signs(normalise(new))
                    dev = quat_angle_deg(raw, new)
                    stat = {"max_deg": round(float(dev.max()), 3), "mean_deg": round(float(dev.mean()), 3)}
                else:
                    dev = np.linalg.norm(raw - new, axis=1)
                    stat = {"max_cm": round(float(dev.max()), 3), "mean_cm": round(float(dev.mean()), 3)}
                data[bone][prop] = new
                smoothed["%s.%s" % (bone, prop)] = stat
        if not smoothed:
            print("%s: WARNING --smooth-bones %s matched no animated bone" % (TOOL, patterns))
        ops.append({"op": "butterworth_lowpass", "cutoff_hz": cutoff, "order_per_pass": order,
                    "passes": "forward+backward (zero-phase)", "fs_hz": cc.FPS, "edges": "odd reflection",
                    "bone_patterns": patterns, "paths": paths,
                    "hit_frames_output": list(hit) if hit else None, "hit_margin_frames": margin if hit else None,
                    "taper_frames": taper if hit else None,
                    "unsmoothed_output_frames": prot, "channels_smoothed": len(smoothed),
                    "change_vs_unsmoothed": smoothed})
    else:
        ops.append({"op": "butterworth_lowpass", "applied": False, "reason": "no --smooth-bones given"})

    # write keys back (same fcurves, same channels; only the sampling changes) -------------------------------
    for bone, props in chans.items():
        for prop, fcs in props.items():
            v = data[bone][prop]
            for c, fc in enumerate(fcs):
                kp = fc.keyframe_points
                kp.clear()
                kp.add(n)
                co = np.empty((n, 2))
                co[:, 0] = np.arange(n)
                co[:, 1] = v[:, c]
                kp.foreach_set("co", co.ravel())
                for k in kp:
                    k.interpolation = "LINEAR"
                fc.update()
    sc = bpy.context.scene
    sc.render.fps, sc.render.fps_base = cc.FPS, 1.0
    sc.frame_start, sc.frame_end = 0, n - 1
    sc.frame_set(0)
    ops.append({"op": "write_keys", "ticks": [0, n - 1], "interpolation": "LINEAR", "fps": cc.FPS,
                "note": "same fcurves and channels as the raw take; keys are resampled values, none hand-placed (F5)"})

    # 4. export --------------------------------------------------------------------------------------------------
    out = os.path.abspath(out)
    os.makedirs(os.path.dirname(out), exist_ok=True)
    bpy.ops.export_scene.gltf(filepath=out, export_format="GLB", export_animations=True, export_force_sampling=True,
                              export_frame_range=True, export_optimize_animation_size=False,
                              export_nla_strips=False, export_apply=False)
    ops.append({"op": "export_glb", "file": out, "sha256": cc.sha256(out), "bytes": os.path.getsize(out),
                "exporter": "Blender %s io_scene_gltf2, sampled every frame at 60 fps" % bpy.app.version_string})
    if "--blend" in args:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(args["--blend"][0]), copy=True)
        ops.append({"op": "save_blend", "file": os.path.abspath(args["--blend"][0])})

    rebuild = ("/Applications/Blender.app/Contents/MacOS/Blender -b --python tools/blender/clean_clip.py -- "
               + " ".join(_q(a) for a in sys.argv[sys.argv.index("--") + 1:]))
    log = {"tool": "tools/blender/clean_clip.py", "version": VERSION, "blender": bpy.app.version_string,
           "raw_file": clip, "raw_sha256": cc.sha256(clip) if clip else None, "output": out,
           "frames_total": n, "output_frame_1_is_raw_tick": t0, "operations": ops,
           "rebuild": rebuild,
           "data_hint": {"file_trim_start": 0, "file_trim_end": n - 1,
                         "note": "this GLB is already trimmed: data/clips trim_start/trim_end index ITS ticks (0..%d), "
                                 "not the raw take's; the raw trim is recorded above" % (n - 1)}}
    lp = one("--log", out[:-4] + ".clean.json" if out.lower().endswith(".glb") else out + ".clean.json")
    cc.write_json(lp, log)
    print("%s: %d frames at 60 fps from raw ticks %d-%d -> %s" % (TOOL, n, t0, t1, out))
    print("%s: log %s" % (TOOL, lp))
    return 0


def _q(a):
    return "'%s'" % a if any(c in a for c in " *?[]") else a


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
