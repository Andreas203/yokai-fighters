"""Re-pose a rigged fighter's legs to the shared skeleton's stance and rebind (rules F5, F6, G4; YOK-102 Kappa).

Why: the Kappa was generated in a squat, splayed stance (hip-to-foot 18-19 deg off vertical; rig_check's limit is
15, the shared skeleton's rigs sit at 2-5). The shared clips set every bone's local rotation outright, so in game
his legs already take Ryo's angles: the skin is wrenched from the splayed bind every frame, and because a
straightened leg is longer than the splayed rest leg is tall, his feet go through the floor while his rest hips
(which YOK-130 scales the clip hips by) still assume the squat.

What it does, once, by script (Blender world: +Z up, the character faces -Y, its left is +X):
  1. for each side, rotate UpLeg about the hip joint so hip -> knee points along the reference rig's hip -> knee
     direction, then Leg about the knee so knee -> ankle points along the reference's knee -> ankle; the Foot is
     turned back to its original world orientation, so the sole stays as modelled (flat);
  2. bake that pose into the mesh (armature modifier applied on a copy of the data; vertex groups are kept)
     and make it the new rest pose (pose -> rest), so rest == bind again;
  3. lift the whole rig and mesh so the lowest vertex is on the ground again (origin at the feet);
  4. export the skinned GLB (armature + mesh + texture, no animation).
Bone names, parents, skin order and bone lengths do not change; only the leg bones' rest orientation and the
height of everything. The pose is computed from the two rigs' joint positions: no key is placed (F5), no action
is written. Weights are untouched (run repair_weights.py afterwards).

Run (Blender 4.x, headless):
  Blender -b --python tools/blender/repose_legs.py -- --in <rig.glb> --ref game/assets/generated/characters/ryo/ryo-rigged.glb \
      --out <rig-reposed.glb> [--log <log.json>] [--blend 1.0]
  --blend 0..1: how far to turn toward the reference directions (1 = all the way).
"""

import argparse
import hashlib
import json
import sys

import bpy
from mathutils import Matrix, Vector


def args():
    ap = argparse.ArgumentParser()
    ap.add_argument("--in", dest="inp", required=True)
    ap.add_argument("--ref", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--log")
    ap.add_argument("--blend", type=float, default=1.0)
    return ap.parse_args(sys.argv[sys.argv.index("--") + 1:])


def sha(p):
    return hashlib.sha256(open(p, "rb").read()).hexdigest()


def import_rig(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    arm = [o for o in new if o.type == "ARMATURE"][0]
    if arm.animation_data:
        arm.animation_data.action = None
    for pb in arm.pose.bones:
        pb.matrix_basis.identity()
    bpy.context.view_layer.update()
    return arm, new


def heads(arm):
    return {b.name: arm.matrix_world @ b.head_local for b in arm.data.bones}


def leg_angle(h, side):
    d = h[side + "Foot"] - h[side + "UpLeg"]
    return round(d.angle(Vector((0, 0, -1))) * 57.29578, 2)


def turn(arm, pb, pivot_world, src_dir, dst_dir, blend):
    """Rotate pose bone pb (and its children) about a world pivot so src_dir turns toward dst_dir."""
    q = src_dir.normalized().rotation_difference(dst_dir.normalized())
    q = Quaternion_slerp(q, blend)
    R = q.to_matrix().to_4x4()
    mw = arm.matrix_world
    piv = mw.inverted() @ pivot_world
    Ra = mw.to_3x3().inverted().to_4x4() @ R @ mw.to_3x3().to_4x4()  # world rotation in armature space
    pb.matrix = Matrix.Translation(piv) @ Ra @ Matrix.Translation(-piv) @ pb.matrix
    bpy.context.view_layer.update()
    return q.angle * 57.29578


def Quaternion_slerp(q, t):
    from mathutils import Quaternion
    return Quaternion().slerp(q, t)


def main():
    a = args()
    if a.inp == a.out:
        raise SystemExit("refusing to overwrite the input")
    bpy.ops.wm.read_factory_settings(use_empty=True)
    ref_arm, ref_objs = import_rig(a.ref)
    rh = heads(ref_arm)
    for o in ref_objs:
        bpy.data.objects.remove(o, do_unlink=True)

    arm, objs = import_rig(a.inp)
    mesh = [o for o in objs if o.type == "MESH" and any(m.type == "ARMATURE" for m in o.modifiers)][0]
    h0 = heads(arm)
    log = {"tool": "repose_legs.py", "in": a.inp, "ref": a.ref, "out": a.out, "blend": a.blend,
           "leg_deg_before": {s: leg_angle(h0, s) for s in ("Left", "Right")},
           "ref_leg_deg": {s: leg_angle(rh, s) for s in ("Left", "Right")}, "turns_deg": {}}

    for side in ("Left", "Right"):
        up, lo, ft = (arm.pose.bones[side + n] for n in ("UpLeg", "Leg", "Foot"))
        foot_rot0 = (arm.matrix_world @ ft.matrix).to_3x3().normalized()
        h = heads_posed(arm)
        t1 = turn(arm, up, h[side + "UpLeg"], h[side + "Leg"] - h[side + "UpLeg"],
                  rh[side + "Leg"] - rh[side + "UpLeg"], a.blend)
        h = heads_posed(arm)
        t2 = turn(arm, lo, h[side + "Leg"], h[side + "Foot"] - h[side + "Leg"],
                  rh[side + "Foot"] - rh[side + "Leg"], a.blend)
        # foot back to its modelled world orientation (sole flat), about the ankle
        h = heads_posed(arm)
        cur = (arm.matrix_world @ ft.matrix).to_3x3().normalized()
        Rw = (foot_rot0 @ cur.inverted()).to_4x4()
        mw = arm.matrix_world
        piv = mw.inverted() @ h[side + "Foot"]
        Ra = mw.to_3x3().inverted().to_4x4() @ Rw @ mw.to_3x3().to_4x4()
        ft.matrix = Matrix.Translation(piv) @ Ra @ Matrix.Translation(-piv) @ ft.matrix
        bpy.context.view_layer.update()
        log["turns_deg"][side] = {"UpLeg": round(t1, 2), "Leg": round(t2, 2)}

    # bake the pose into the mesh and make it the rest pose
    bpy.context.view_layer.objects.active = mesh
    mod = [m for m in mesh.modifiers if m.type == "ARMATURE"][0]
    name = mod.name
    bpy.ops.object.select_all(action="DESELECT")
    mesh.select_set(True)
    bpy.ops.object.modifier_copy(modifier=name)
    bpy.ops.object.modifier_apply(modifier=name)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="POSE")
    bpy.ops.pose.select_all(action="SELECT")
    bpy.ops.pose.armature_apply(selected=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.context.view_layer.update()

    # ground: lowest vertex back to z 0 (move the bones and the mesh data, not the objects)
    dg = bpy.context.evaluated_depsgraph_get()
    ev = mesh.evaluated_get(dg)
    zmin = min((mesh.matrix_world @ v.co).z for v in ev.data.vertices)
    dz = -zmin
    sw = arm.matrix_world.to_scale().z
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    for eb in arm.data.edit_bones:
        eb.head.z += dz / sw
        eb.tail.z += dz / sw
    bpy.ops.object.mode_set(mode="OBJECT")
    mw_m = mesh.matrix_world
    shift = mw_m.to_3x3().inverted() @ Vector((0, 0, dz))
    for v in mesh.data.vertices:
        v.co += shift
    mesh.data.update()
    bpy.context.view_layer.update()

    h1 = heads(arm)
    ev = mesh.evaluated_get(bpy.context.evaluated_depsgraph_get())
    zs = [(mesh.matrix_world @ v.co).z for v in ev.data.vertices]
    log.update({"leg_deg_after": {s: leg_angle(h1, s) for s in ("Left", "Right")}, "lift_m": round(dz, 4),
                "height_before_m": None, "height_after_m": round(max(zs) - min(zs), 4),
                "hips_z_before_m": round(h0["Hips"].z, 4), "hips_z_after_m": round(h1["Hips"].z, 4),
                "feet_x_before_m": [round(h0[s + "Foot"].x, 4) for s in ("Left", "Right")],
                "feet_x_after_m": [round(h1[s + "Foot"].x, 4) for s in ("Left", "Right")]})
    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True)
    mesh.select_set(True)
    bpy.ops.export_scene.gltf(filepath=a.out, export_format="GLB", use_selection=True, export_animations=False,
                              export_skins=True, export_yup=True)
    log["sha256_in"], log["sha256_out"] = sha(a.inp), sha(a.out)
    print("repose_legs:", json.dumps(log))
    if a.log:
        json.dump(log, open(a.log, "w"), indent=1)


def heads_posed(arm):
    return {pb.name: arm.matrix_world @ pb.head for pb in arm.pose.bones}


if __name__ == "__main__":
    main()
