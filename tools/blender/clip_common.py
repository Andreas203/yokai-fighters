"""Shared helpers for clean_clip.py and measure_clip.py (YOK-110). Imported by them, not run on its own.

Conventions (all measured on the accepted Ryo/Kitsune clips):
- A clip GLB is imported with the scene at 60 fps, so Blender frame N == raw tick N (60 per second); the
  glTF importer maps seconds to frames with the scene fps. Frame numbers in `data/clips/*.json`
  (`trim_start`, `trim_end`) are these raw ticks.
- After import: Blender world is Z up, the character faces -Y (glTF +Z), its left is +X. The armature object
  carries scale 0.01, so world units are metres and armature-space units are centimetres.
- Game frames are 1-based: game frame n shows raw tick trim_start + (n - 1) * speed_scale.
"""
import hashlib
import json
import math
import os
import sys

import bpy
import numpy as np

FPS = 60


def script_args(tool):
    """Arguments after the first `--` as {flag: [values]}; flags without values map to []."""
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out, cur = {}, None
    for tok in argv:
        if tok.startswith("--") and not _is_number(tok):
            cur = tok
            out.setdefault(cur, [])
        elif cur is None:
            sys.exit("%s: unexpected argument %r (flags start with --)" % (tool, tok))
        else:
            out[cur].append(tok)
    return out


def _is_number(s):
    try:
        float(s)
        return True
    except ValueError:
        return False


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def load_clip(path):
    """Import a clip GLB into an empty scene at 60 fps, or use the opened .blend when path is None.
    Returns (armature object, action)."""
    sc = bpy.context.scene
    if path:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        sc = bpy.context.scene
        sc.render.fps = FPS
        sc.render.fps_base = 1.0
        bpy.ops.import_scene.gltf(filepath=path)
    arms = [o for o in bpy.data.objects if o.type == "ARMATURE"]
    if len(arms) != 1:
        sys.exit("expected exactly one armature, found %d" % len(arms))
    arm = arms[0]
    act = arm.animation_data.action if arm.animation_data else None
    if act is None:
        sys.exit("armature %s has no action" % arm.name)
    if abs(sc.render.fps / sc.render.fps_base - FPS) > 1e-6 and not path:
        sys.exit("opened .blend runs at %s fps; clips must be imported from the GLB (60 fps) or saved at 60 fps"
                 % (sc.render.fps / sc.render.fps_base))
    arm.rotation_mode = "XYZ"
    return arm, act


def pose_channels(act):
    """{bone: {'location': [3 fcurves], 'rotation_quaternion': [4], 'scale': [3]}} (missing entries absent)."""
    out = {}
    for fc in act.fcurves:
        dp = fc.data_path
        if not dp.startswith('pose.bones["'):
            continue
        bone = dp[len('pose.bones["'):dp.index('"]')]
        prop = dp[dp.index('"].') + 3:]
        out.setdefault(bone, {}).setdefault(prop, {})[fc.array_index] = fc
    res = {}
    for bone, props in out.items():
        res[bone] = {p: [d[i] for i in sorted(d)] for p, d in props.items()}
    return res


def eval_channel(fcs, frames):
    """Evaluate a list of fcurves at fractional frames -> (len(frames), len(fcs)). Uses the curve's own
    interpolation and constant extrapolation (hold first/last key)."""
    return np.array([[fc.evaluate(f) for fc in fcs] for f in frames], dtype=np.float64)


def fix_quat_signs(q):
    """Flip quaternions in place so consecutive samples share a hemisphere (no 360 deg wraps)."""
    for i in range(1, len(q)):
        if np.dot(q[i], q[i - 1]) < 0:
            q[i] = -q[i]
    return q


def action_last_tick(act):
    return int(round(act.frame_range[1]))


def bone_world_heads(arm, names):
    """World-space head positions (m) of the named pose bones at the current frame."""
    mw = arm.matrix_world
    return np.array([np.array(mw @ arm.pose.bones[n].head) for n in names])


def write_json(path, data):
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        json.dump(data, f, indent=1)
        f.write("\n")


def finish(code):
    """Blender ignores a script's exit status; make the gate scriptable (same as rig_check.py)."""
    sys.stdout.flush()
    sys.stderr.flush()
    os._exit(code)
