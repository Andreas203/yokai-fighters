"""Compose a crouching guard from two existing takes of the same skeleton, by script (YOK-246; rules F5, F6).
The legs and hips of <legs.glb> (frame 0: the accepted, slide-free crouch pose) replace the legs and hips of <base.glb>, eased in
over --ease ticks from the base take's own standing legs; the base take's spine, arms and head play unchanged. Nothing is hand-keyed:
every value is the take's own or a blend of two takes' own values.
Blender -b --python tools/blender/compose_guard.py -- --base <in.glb> --legs <crouch.glb> --out <out.glb> [--ease 8] [--until TICK]
"""
import sys
import bpy
from mathutils import Quaternion, Vector
a = sys.argv[sys.argv.index("--") + 1:]
g = lambda k, d=None: a[a.index(k) + 1] if k in a else d
base, legs, out, ease = g("--base"), g("--legs"), g("--out"), float(g("--ease", 8))
until = g("--until")
LEG = ("Hips", "LeftUpLeg", "LeftLeg", "LeftFoot", "LeftToeBase", "RightUpLeg", "RightLeg", "RightFoot", "RightToeBase")

def load(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    return next(o for o in new if o.type == "ARMATURE"), new

def bname(pb):
    return pb.name.split(":")[-1]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.scene.render.fps = 60  # keys are 60 per second
carm, cobjs = load(legs)
act = carm.animation_data.action
bpy.context.scene.frame_set(int(act.frame_range[0]))
crouch = {bname(pb): (pb.rotation_quaternion.copy(), pb.location.copy()) for pb in carm.pose.bones if bname(pb) in LEG}
for o in cobjs:
    bpy.data.objects.remove(o, do_unlink=True)
arm, _ = load(base)
act = arm.animation_data.action
lo, hi = int(act.frame_range[0]), int(act.frame_range[1])
if until: hi = min(hi, lo + int(until))
frames = {}
for f in range(lo, hi + 1):
    bpy.context.scene.frame_set(f)
    frames[f] = {bname(pb): (pb.rotation_quaternion.copy(), pb.location.copy()) for pb in arm.pose.bones if bname(pb) in LEG}
for f in range(lo, hi + 1):
    t = (f - lo + 1) / ease  # ease 1: the crouch legs from the first key
    w = 1.0 if t >= 1 else t * t * (3 - 2 * t)
    for pb in arm.pose.bones:
        n = bname(pb)
        if n not in LEG: continue
        qo, lo_ = frames[f][n]; qc, lc = crouch[n]
        q = qo.slerp(qc, w)
        pb.rotation_quaternion = q
        if n == "Hips": pb.location = lo_.lerp(lc, w)
        pb.keyframe_insert("rotation_quaternion", frame=f)
        if n == "Hips": pb.keyframe_insert("location", frame=f)
bpy.context.scene.frame_start, bpy.context.scene.frame_end = lo, hi
bpy.ops.export_scene.gltf(filepath=out, export_format="GLB", export_animations=True, export_force_sampling=True, export_frame_range=True)
print("compose_guard: ok frames", lo, hi)
