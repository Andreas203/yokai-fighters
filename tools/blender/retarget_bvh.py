"""Retarget a Meshy text-to-motion BVH onto a rig on the shared skeleton, by script (rules F5, F6; YOK-102).

Meshy's text_to_motion returns a BVH on its own 77-joint skeleton (root `Root`, `Hips`, `Spine1`, `Spine2`, `Chest`,
`Neck1`, `Neck2`, fingers, `LeftLeg`=thigh, `LeftShin` ...). Applying it through meshy_animate costs 3 more credits; this
script does the same job for free. For every mapped bone the source's world-space rotation change from its rest pose is
applied to the target's rest pose (rest poses differ, both are T-poses facing the same way after Blender's import),
so no pose is placed by hand. Fingers, `Root`, `Neck2` and the eyes/jaw are not mapped. Hips gets the source's
translation change scaled by the ratio of the two rigs' hip heights.

Run (Blender 4.x, headless):
  Blender -b --python tools/blender/retarget_bvh.py -- --bvh <take.bvh> --rig <rig.glb> --out <retargeted.glb> [--fps 30]
Writes a GLB (rig, mesh and one action at 60 fps scene rate, keys every other frame since a text_to_motion take is 30 fps).
Follow with clean_clip.py (resample to 60 ticks, trim, strip root).
"""
import os
import sys

import bpy
from mathutils import Matrix, Vector

MAP = {  # target bone: source (BVH) bone
    "Hips": "Hips", "Spine02": "Spine1", "Spine01": "Spine2", "Spine": "Chest", "neck": "Neck1", "Head": "Head",
    "LeftShoulder": "LeftShoulder", "LeftArm": "LeftArm", "LeftForeArm": "LeftForeArm", "LeftHand": "LeftHand",
    "RightShoulder": "RightShoulder", "RightArm": "RightArm", "RightForeArm": "RightForeArm", "RightHand": "RightHand",
    "LeftUpLeg": "LeftLeg", "LeftLeg": "LeftShin", "LeftFoot": "LeftFoot", "LeftToeBase": "LeftToeBase",
    "RightUpLeg": "RightLeg", "RightLeg": "RightShin", "RightFoot": "RightFoot", "RightToeBase": "RightToeBase",
}


def main():
    a = sys.argv[sys.argv.index("--") + 1:]
    arg = lambda k, d=None: a[a.index(k) + 1] if k in a else d
    bvh, rig, out = arg("--bvh"), arg("--rig"), arg("--out")
    src_fps = float(arg("--fps", 30))
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o)
    bpy.context.scene.render.fps = 60
    bpy.ops.import_scene.gltf(filepath=os.path.abspath(rig))
    tg = [o for o in bpy.data.objects if o.type == "ARMATURE"][0]
    before = set(bpy.data.objects)
    bpy.ops.import_anim.bvh(filepath=os.path.abspath(bvh), axis_forward="-Z", axis_up="Y", global_scale=0.01,
                            rotate_mode="QUATERNION", update_scene_fps=False, use_fps_scale=False)
    sr = [o for o in bpy.data.objects if o.type == "ARMATURE" and o not in before][0]
    act = sr.animation_data.action
    f0, f1 = int(act.frame_range[0]), int(act.frame_range[1])
    sw, tw = sr.matrix_world.to_3x3(), tg.matrix_world.to_3x3()
    rest_s = {b.name: sw @ b.matrix_local.to_3x3() for b in sr.data.bones}
    rest_t = {b.name: tw @ b.matrix_local.to_3x3() for b in tg.data.bones}
    src_hip0 = (sr.matrix_world @ sr.data.bones["Hips"].head_local)
    tgt_hip0 = (tg.matrix_world @ tg.data.bones["Hips"].head_local)
    ratio = tgt_hip0.z / src_hip0.z
    order = []

    def walk(b):
        order.append(b)
        for c in b.children:
            walk(c)
    for b in tg.data.bones:
        if b.parent is None:
            walk(b)
    for pb in tg.pose.bones:
        pb.rotation_mode = "QUATERNION"
    tg.animation_data_create()
    for tr in list(tg.animation_data.nla_tracks):  # the rig GLB's one-frame bind clip
        tg.animation_data.nla_tracks.remove(tr)
    tg.animation_data.action = bpy.data.actions.new("retargeted")
    scene = bpy.context.scene
    step = int(round(60.0 / src_fps))
    for f in range(f0, f1 + 1):
        scene.frame_set(f)
        tf = (f - f0) * step + 1
        world = {}
        for b in order:
            parent_w = world[b.parent.name] if b.parent else Matrix.Identity(3)
            prest = rest_t[b.parent.name] if b.parent else Matrix.Identity(3)
            restlocal = prest.inverted() @ rest_t[b.name]
            sname = MAP.get(b.name)
            pb = tg.pose.bones[b.name]
            if sname and sname in sr.pose.bones:
                rp = sw @ sr.pose.bones[sname].matrix.to_3x3()
                d = rp @ rest_s[sname].inverted()
                desired = d @ rest_t[b.name]
                q = ((parent_w @ restlocal).inverted() @ desired)
                pb.rotation_quaternion = q.to_quaternion().normalized()
                world[b.name] = desired
            else:
                pb.rotation_quaternion = (1, 0, 0, 0)
                world[b.name] = parent_w @ restlocal
            pb.keyframe_insert("rotation_quaternion", frame=tf)
            if b.name == "Hips":
                dw = (sr.matrix_world @ sr.pose.bones["Hips"].head) - src_hip0
                darm = (tg.matrix_world.to_3x3().inverted() @ (dw * ratio))
                pb.location = b.matrix_local.to_3x3().inverted() @ darm
                pb.keyframe_insert("location", frame=tf)
    last = (f1 - f0) * step + 1
    print("retarget_bvh: action", tg.animation_data.action.name, len(tg.animation_data.action.fcurves),
          "nla", [t.name for t in tg.animation_data.nla_tracks], [(x.name, x.users) for x in bpy.data.actions])
    bpy.data.objects.remove(sr)
    for ac in list(bpy.data.actions):  # the exporter writes every action: keep only the retargeted one
        if ac is not tg.animation_data.action:
            bpy.data.actions.remove(ac)
    scene.frame_start, scene.frame_end = 1, last
    os.makedirs(os.path.dirname(os.path.abspath(out)), exist_ok=True)
    bpy.ops.export_scene.gltf(filepath=os.path.abspath(out), export_format="GLB", export_animations=True,
                              export_force_sampling=True, export_frame_range=True, export_optimize_animation_size=False,
                              export_nla_strips=False, export_apply=False)
    print("retarget_bvh: %d source frames -> %d scene frames at 60 fps -> %s" % (f1 - f0 + 1, last, out))


main()
