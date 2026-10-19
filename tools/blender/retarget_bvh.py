"""Retarget a take onto a rig on the shared skeleton, by script (rules F5, F6; YOK-102, YOK-146).

Two sources:
  --bvh <take.bvh>   Meshy text_to_motion BVH (77-joint skeleton: root `Root`, `Hips`, `Spine1`, `Spine2`, `Chest`,
                     `Neck1`, `Neck2`, fingers, `LeftLeg`=thigh, `LeftShin` ...). Applying it through meshy_animate
                     costs 3 more credits; this does the same job for free (YOK-102).
  --clip <clip.glb>  an existing clip GLB on another skeleton (YOK-146: the Meshy clips onto the Tripo shared
                     skeleton, mapped by chain position through MESHY_TO_TRIPO; Meshy's spine counts down). Its
                     frame numbers are kept (scene at 60 fps: frame N = raw tick N), so data/clips trims still hold.

For every mapped bone the source's world-space rotation change from its rest pose is applied to the target's rest
pose, so no pose is placed by hand. With --rest-align 1 (default for --clip) each target bone's rest is first turned
onto the source bone's rest direction (shortest arc), so rest-pose differences (Tripo's arms 7 deg below horizontal,
legs splayed 9 deg; Meshy's near-flat T) do not carry into the clip. Unmapped bones (fingers, `Root`, `Neck2`,
eyes/jaw, leaf ends) stay at rest. `Hips` gets the source's translation change scaled by the ratio of the two rigs'
hip heights; --in-place 1 (default for --clip) keeps only its vertical part (F6: root motion stripped; the game keeps
only the hips height, FighterModel.BuildLibrary). Target bone names are normalised (`mixamorig:Hips` reads `Hips`).

Run (Blender 4.x, headless):
  Blender -b --python tools/blender/retarget_bvh.py -- --bvh <take.bvh> --rig <rig.glb> --out <retargeted.glb> [--fps 30]
  Blender -b --python tools/blender/retarget_bvh.py -- --clip <meshy-clip.glb> --rig <tripo-rig.glb> --out <clip.glb>
      [--rest-align 0|1] [--in-place 0|1] [--proxy-mesh 0|1] [--log <log.json>]
BVH mode writes a GLB (rig, mesh and one action at 60 fps scene rate, keys every other frame since a text_to_motion
take is 30 fps); follow with clean_clip.py. Clip mode keys every source frame at the same frame number and, with
--proxy-mesh 1 (default for --clip), replaces the rig's mesh by a 25-triangle proxy skinned one triangle per bone:
the game only reads a clip GLB's skeleton and animation, so a clip file stays ~100 KB instead of the rig's ~9 MB.
"""
import json
import os
import sys

import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import clip_common as cc  # noqa: E402

MAP = {  # Meshy shared-skeleton target bone: source (BVH) bone
    "Hips": "Hips", "Spine02": "Spine1", "Spine01": "Spine2", "Spine": "Chest", "neck": "Neck1", "Head": "Head",
    "LeftShoulder": "LeftShoulder", "LeftArm": "LeftArm", "LeftForeArm": "LeftForeArm", "LeftHand": "LeftHand",
    "RightShoulder": "RightShoulder", "RightArm": "RightArm", "RightForeArm": "RightForeArm", "RightHand": "RightHand",
    "LeftUpLeg": "LeftLeg", "LeftLeg": "LeftShin", "LeftFoot": "LeftFoot", "LeftToeBase": "LeftToeBase",
    "RightUpLeg": "RightLeg", "RightLeg": "RightShin", "RightFoot": "RightFoot", "RightToeBase": "RightToeBase",
}
LIMBS = [s + n for s in ("Left", "Right") for n in ("Shoulder", "Arm", "ForeArm", "Hand", "UpLeg", "Leg", "Foot", "ToeBase")]
# Meshy clip -> Tripo shared skeleton (skeleton.md "Bone map: Meshy"): target bone: source bone, by chain position.
MESHY_CLIP_MAP = dict({"Hips": "Hips", "Spine": "Spine02", "Spine1": "Spine01", "Spine2": "Spine", "Neck": "neck",
                       "Head": "Head"}, **{n: n for n in LIMBS})


def bvh_map_for(target_names):
    """MAP in the target's naming: on a Tripo-named rig the Meshy keys move by chain position."""
    if "Spine02" in target_names:
        return dict(MAP)
    return {cc.MESHY_TO_TRIPO.get(k, k): v for k, v in MAP.items()}


def make_proxy(tg, names):
    """One small triangle per bone at its rest head, weight 1.0 to that bone, so the exporter writes a skin."""
    verts, faces = [], []
    for i, n in enumerate(names):
        h = tg.data.bones[n].head_local
        verts += [h, h + Vector((0.01, 0, 0)), h + Vector((0, 0, 0.01))]
        faces.append((3 * i, 3 * i + 1, 3 * i + 2))
    me = bpy.data.meshes.new("clip_proxy")
    me.from_pydata([tuple(v) for v in verts], [], faces)
    ob = bpy.data.objects.new("clip_proxy", me)
    bpy.context.scene.collection.objects.link(ob)
    ob.parent = tg
    for i, n in enumerate(names):
        ob.vertex_groups.new(name=n).add([3 * i, 3 * i + 1, 3 * i + 2], 1.0, "REPLACE")
    ob.modifiers.new("Armature", "ARMATURE").object = tg
    return ob


def main():
    a = sys.argv[sys.argv.index("--") + 1:]
    arg = lambda k, d=None: a[a.index(k) + 1] if k in a else d
    bvh, clip, rig, out = arg("--bvh"), arg("--clip"), arg("--rig"), arg("--out")
    if bool(bvh) == bool(clip) or not rig or not out:
        sys.exit("retarget_bvh: give --rig, --out and exactly one of --bvh / --clip")
    clip_mode = bool(clip)
    rest_align = arg("--rest-align", "1" if clip_mode else "0") == "1"
    in_place = arg("--in-place", "1" if clip_mode else "0") == "1"
    proxy = arg("--proxy-mesh", "1" if clip_mode else "0") == "1"
    src_fps = float(arg("--fps", 60 if clip_mode else 30))
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.fps, scene.render.fps_base = 60, 1.0
    bpy.ops.import_scene.gltf(filepath=os.path.abspath(rig))
    tg = [o for o in bpy.data.objects if o.type == "ARMATURE"][0]
    tg_objs = set(bpy.data.objects)
    tnames = cc.bone_lookup(tg)  # normalised -> actual
    if clip_mode:
        bpy.ops.import_scene.gltf(filepath=os.path.abspath(clip))
        sr = [o for o in bpy.data.objects if o.type == "ARMATURE" and o not in tg_objs][0]
        snames = cc.bone_lookup(sr)
        bmap = {tnames[t]: snames[s] for t, s in MESHY_CLIP_MAP.items() if t in tnames and s in snames}
    else:
        bpy.ops.import_anim.bvh(filepath=os.path.abspath(bvh), axis_forward="-Z", axis_up="Y", global_scale=0.01,
                                rotate_mode="QUATERNION", update_scene_fps=False, use_fps_scale=False)
        sr = [o for o in bpy.data.objects if o.type == "ARMATURE" and o not in tg_objs][0]
        bmap = {tnames[t]: s for t, s in bvh_map_for(tnames).items() if t in tnames}
    act = sr.animation_data.action
    if act is None:
        sys.exit("retarget_bvh: source has no action")
    f0, f1 = int(round(act.frame_range[0])), int(round(act.frame_range[1]))
    sw, tw = sr.matrix_world.to_3x3(), tg.matrix_world.to_3x3()
    rest_s = {b.name: sw @ b.matrix_local.to_3x3() for b in sr.data.bones}
    rest_t = {b.name: tw @ b.matrix_local.to_3x3() for b in tg.data.bones}
    align = {}
    for t, s in bmap.items():  # shortest arc from the target bone's rest direction (bone Y) to the source's
        align[t] = (rest_t[t].col[1].rotation_difference(rest_s[s].col[1]).to_matrix() if rest_align
                    else Matrix.Identity(3))
    s_hips, t_hips = bmap.get(tnames.get("Hips")), tnames.get("Hips")
    src_hip0 = sr.matrix_world @ sr.data.bones[s_hips].head_local
    tgt_hip0 = tg.matrix_world @ tg.data.bones[t_hips].head_local
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
    step = int(round(60.0 / src_fps))
    for f in range(f0, f1 + 1):
        scene.frame_set(f)
        tf = f if clip_mode else (f - f0) * step + 1
        world = {}
        for b in order:
            parent_w = world[b.parent.name] if b.parent else Matrix.Identity(3)
            prest = rest_t[b.parent.name] if b.parent else Matrix.Identity(3)
            restlocal = prest.inverted() @ rest_t[b.name]
            sname = bmap.get(b.name)
            pb = tg.pose.bones[b.name]
            if sname and sname in sr.pose.bones:
                rp = sw @ sr.pose.bones[sname].matrix.to_3x3()
                d = rp @ rest_s[sname].inverted()
                desired = d @ align[b.name] @ rest_t[b.name]
                q = ((parent_w @ restlocal).inverted() @ desired)
                pb.rotation_quaternion = q.to_quaternion().normalized()
                world[b.name] = desired
            else:
                pb.rotation_quaternion = (1, 0, 0, 0)
                world[b.name] = parent_w @ restlocal
            pb.keyframe_insert("rotation_quaternion", frame=tf)
            if b.name == t_hips:
                dw = (sr.matrix_world @ sr.pose.bones[s_hips].head) - src_hip0
                if in_place:
                    dw = Vector((0, 0, dw.z))
                darm = (tg.matrix_world.to_3x3().inverted() @ (dw * ratio))
                pb.location = b.matrix_local.to_3x3().inverted() @ darm
                pb.keyframe_insert("location", frame=tf)
    first = f0 if clip_mode else 1
    last = f1 if clip_mode else (f1 - f0) * step + 1
    for o in list(bpy.data.objects):  # keep only the target armature (and, in clip mode, a proxy skin)
        if o is tg or (o in tg_objs and o.type == "MESH" and o.vertex_groups and not proxy):
            continue
        bpy.data.objects.remove(o, do_unlink=True)
    if proxy:
        make_proxy(tg, [b.name for b in tg.data.bones])
    for ac in list(bpy.data.actions):  # the exporter writes every action: keep only the retargeted one
        if ac is not tg.animation_data.action:
            bpy.data.actions.remove(ac)
    scene.frame_start, scene.frame_end = first, last
    os.makedirs(os.path.dirname(os.path.abspath(out)), exist_ok=True)
    bpy.ops.export_scene.gltf(filepath=os.path.abspath(out), export_format="GLB", export_animations=True,
                              export_force_sampling=True, export_frame_range=True, export_optimize_animation_size=False,
                              export_nla_strips=False, export_apply=False)
    info = {"tool": "tools/blender/retarget_bvh.py", "mode": "clip" if clip_mode else "bvh", "source": clip or bvh,
            "rig": rig, "out": out, "source_frames": [f0, f1], "out_frames": [first, last],
            "rest_align": rest_align, "in_place": in_place, "proxy_mesh": proxy, "hip_ratio": round(ratio, 5),
            "map": bmap, "unmapped_target_bones": sorted(b.name for b in tg.data.bones if b.name not in bmap)}
    print("retarget_bvh:", json.dumps(info))
    if arg("--log"):
        cc.write_json(arg("--log"), info)


main()
cc.finish(0)
