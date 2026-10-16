"""Prepare a rigid prop for a fighter: scale, orient, re-pivot, and compute its bone mount (F5, F6; YOK-102).

Props are separate rigid meshes on a Godot BoneAttachment3D (no extra bones, no weights): the Oni's
kanabo on RightHand, the Kappa's shell on Spine01. This script rebuilds the prop GLB with its origin at
the mount pivot and writes the child transform for the BoneAttachment3D, read from the rig's rest pose,
so nobody places the prop by hand. It is a mesh edit of the prop, not animation: nothing is keyed and the
rig file is never written.

Modes (Blender world axes after glTF import: +Z up, the character faces -Y, its left is +X):
  club  The long axis is the prop's longest extent; the head is the end with the larger outer radius.
        Uniform scale to --length. The pivot is the grip: --grip-frac of the length from the handle end.
        The prop is turned so the handle -> head direction is --axis (glTF axes; default 0,0,1 = forward),
        or, with --axis-bones A,B, the rig's own rest direction from bone A to bone B (the forearm:
        RightForeArm,RightHand) tilted by --tilt-deg toward --tilt-toward (forward), so the club runs on
        from the forearm in every pose and its tip clears the floor with the arm hanging.
        Mount point: the hand centre, the mean rest position of the verts whose top weight is --bone.
  fit   The prop is turned so its longest extent is up (Z), the middle one across (X) and the thinnest is
        depth (Y), with the dome (the side whose extreme verts bunch near the axis) facing +Y, behind
        the character. Scale per axis to the --target-min/--target-max box (Blender world m, rest pose;
        pass negative numbers as --target-min=-0.39,...), so the rim lands where the baked part's rim was,
        or uniform with --uniform (smallest of the X/Z ratios), or --scale s. The dome apex is placed on
        the box's back face, centred on the box in X and Z. The pivot is the centre of the flat side.
        Mount point: that pivot.

Output: <out>.glb (the prop alone, origin at the pivot, metres, glTF Y-up) and a log with the
BoneAttachment3D child transform. The skeleton in Godot is `Model/Armature/Skeleton3D` with bone units in
cm under the 0.01 Armature node, so the child carries scale 100 (as the Kitsune tails) and keeps the prop
upright in the rig's rest pose. A uniform size change of the fighter on the Armature node (scale_rig.py)
scales the prop with him; the child transform does not change.

Run (Blender 4.x, headless):
  Blender -b --python tools/blender/prep_prop.py -- --mode club --in game/assets/generated/characters/oni/club.glb \
      --out game/assets/generated/characters/oni/club-grip.glb --length 1.2 --grip-frac 0.13 \
      --rig game/assets/generated/characters/oni/oni-rigged.glb --bone RightHand --log <log.json>
  Blender -b --python tools/blender/prep_prop.py -- --mode fit --in game/assets/generated/characters/kappa/shell.glb \
      --out game/assets/generated/characters/kappa/shell-fit.glb \
      --target-min=-0.39,0.10,0.63 --target-max=0.39,0.37,1.36 \
      --rig game/assets/generated/characters/kappa/kappa-rigged-shellcut.glb --bone Spine01 --log <log.json>
"""

import argparse
import hashlib
import json
import math
import struct
import sys

import bpy
from mathutils import Matrix, Quaternion, Vector


def args():
    ap = argparse.ArgumentParser()
    ap.add_argument("--mode", choices=["club", "fit"], required=True)
    ap.add_argument("--in", dest="inp", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--log")
    ap.add_argument("--length", type=float, default=1.2)
    ap.add_argument("--grip-frac", type=float, default=0.13)
    ap.add_argument("--axis", default="0,0,1")
    ap.add_argument("--axis-bones", help="club: A,B = point handle->head along bone A's head -> bone B's head "
                                         "(rest, from --rig), then tilt it by --tilt-deg toward --tilt-toward")
    ap.add_argument("--tilt-deg", type=float, default=0.0)
    ap.add_argument("--tilt-toward", default="0,0,1")
    ap.add_argument("--target-min")
    ap.add_argument("--target-max")
    ap.add_argument("--uniform", action="store_true")
    ap.add_argument("--scale", type=float)
    ap.add_argument("--rig")
    ap.add_argument("--bone")
    return ap.parse_args(sys.argv[sys.argv.index("--") + 1:])


def vec(s):
    return Vector([float(x) for x in s.split(",")])


def sha(p):
    return hashlib.sha256(open(p, "rb").read()).hexdigest()


def gltf_to_blender(v):
    return Vector((v[0], -v[2], v[1]))


def blender_to_gltf(v):
    return Vector((v[0], v[2], -v[1]))


def read_gltf_json(path):
    data = open(path, "rb").read()
    n = struct.unpack("<I", data[12:16])[0]
    return json.loads(data[20:20 + n])


def node_local(n):
    if "matrix" in n:
        m = n["matrix"]
        return Matrix([m[0:4], m[4:8], m[8:12], m[12:16]]).transposed()
    t = Vector(n.get("translation", [0, 0, 0]))
    r = n.get("rotation", [0, 0, 0, 1])
    s = n.get("scale", [1, 1, 1])
    S = Matrix.Diagonal((s[0], s[1], s[2], 1.0))
    return Matrix.Translation(t) @ Quaternion((r[3], r[0], r[1], r[2])).to_matrix().to_4x4() @ S


def bone_rest_in_skeleton(gltf, bone, skeleton_root="Armature"):
    """Bone global rest in the skeleton's space (the space under the Armature node; glTF axes, cm)."""
    nodes = gltf["nodes"]
    parent = {c: i for i, n in enumerate(nodes) for c in n.get("children", [])}
    idx = [i for i, n in enumerate(nodes) if n.get("name") == bone]
    if len(idx) != 1:
        raise SystemExit(f"bone {bone} not found once in the rig")
    chain, i = [], idx[0]
    while i is not None and nodes[i].get("name") != skeleton_root:
        chain.append(i)
        i = parent.get(i)
    if i is None:
        raise SystemExit(f"{bone} is not under a node named {skeleton_root}")
    G = Matrix.Identity(4)
    for j in reversed(chain):
        G = G @ node_local(nodes[j])
    return G, node_local(nodes[i])


def import_meshes(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    return [o for o in bpy.data.objects if o not in before]


def prop_world_verts(objs):
    return [o.matrix_world @ v.co for o in objs if o.type == "MESH" for v in o.data.vertices]


def bake(objs, M):
    """Apply world matrix M on top of each mesh's world transform, into the mesh data; objects at identity."""
    for o in objs:
        if o.type != "MESH":
            continue
        o.data.transform(M @ o.matrix_world)
        o.matrix_world = Matrix.Identity(4)
        o.data.update()


def hand_centre(rig_path, bone):
    objs = import_meshes(rig_path)
    pts = []
    for o in objs:
        if o.type != "MESH" or not o.vertex_groups:
            continue
        gi = o.vertex_groups.get(bone)
        if gi is None:
            continue
        for v in o.data.vertices:
            if not v.groups:
                continue
            top = max(v.groups, key=lambda g: g.weight)
            if top.group == gi.index:
                pts.append(o.matrix_world @ v.co)
    for o in objs:
        bpy.data.objects.remove(o, do_unlink=True)
    if not pts:
        raise SystemExit(f"no verts weighted mostly to {bone}")
    return sum(pts, Vector()) / len(pts), len(pts)


def main():
    a = args()
    if a.inp == a.out:
        raise SystemExit("refusing to overwrite the input")
    bpy.ops.wm.read_factory_settings(use_empty=True)
    objs = import_meshes(a.inp)
    meshes = [o for o in objs if o.type == "MESH"]
    pts = prop_world_verts(meshes)
    lo = Vector([min(p[i] for p in pts) for i in range(3)])
    hi = Vector([max(p[i] for p in pts) for i in range(3)])
    ext = hi - lo
    log = {"tool": "prep_prop.py", "mode": a.mode, "in": a.inp, "out": a.out,
           "in_extent_blender_m": [round(x, 4) for x in ext]}
    mount_world = None

    if a.mode == "club":
        ax = max(range(3), key=lambda i: ext[i])
        L = ext[ax]
        others = [i for i in range(3) if i != ax]
        mid = (lo + hi) / 2

        def outer_r(t0, t1):
            sel = [p for p in pts if t0 <= p[ax] <= t1]
            return max(max(abs(p[others[0]] - mid[others[0]]), abs(p[others[1]] - mid[others[1]])) for p in sel)

        r_lo = outer_r(lo[ax], lo[ax] + 0.2 * L)
        r_hi = outer_r(hi[ax] - 0.2 * L, hi[ax])
        head_sign = 1 if r_hi > r_lo else -1
        s = a.length / L
        handle_end = lo[ax] if head_sign > 0 else hi[ax]
        grip = mid.copy()
        grip[ax] = handle_end + head_sign * a.grip_frac * L
        d_src = Vector((0, 0, 0))
        d_src[ax] = head_sign
        if a.axis_bones:
            if not a.rig:
                raise SystemExit("--axis-bones needs --rig")
            gj = read_gltf_json(a.rig)
            b0, b1 = a.axis_bones.split(",")
            G0, A0 = bone_rest_in_skeleton(gj, b0)
            G1, _ = bone_rest_in_skeleton(gj, b1)
            p0 = A0 @ G0.to_translation()
            p1 = A0 @ G1.to_translation()
            d = (p1 - p0).normalized()
            t = vec(a.tilt_toward)
            t = (t - t.dot(d) * d).normalized()
            th = math.radians(a.tilt_deg)
            d_gltf = (math.cos(th) * d + math.sin(th) * t).normalized()
            log["axis_from_bones"] = {"bones": [b0, b1], "forearm_gltf": [round(x, 4) for x in d],
                                      "tilt_deg": a.tilt_deg, "tilt_toward": a.tilt_toward,
                                      "axis_gltf": [round(x, 4) for x in d_gltf]}
            d_dst = gltf_to_blender(d_gltf).normalized()
        else:
            d_dst = gltf_to_blender(vec(a.axis)).normalized()
        R = d_src.rotation_difference(d_dst).to_matrix().to_4x4()
        M = R @ Matrix.Scale(s, 4) @ Matrix.Translation(-grip)
        bake(meshes, M)
        log.update({"long_axis": "XYZ"[ax], "length_in_m": round(L, 4), "scale": round(s, 5),
                    "length_out_m": round(L * s, 4), "head_end": "+" if head_sign > 0 else "-",
                    "outer_radius_ends_m": [round(r_lo, 4), round(r_hi, 4)],
                    "grip_frac": a.grip_frac, "grip_in_m": [round(x, 4) for x in grip],
                    "axis_gltf": a.axis})
        if a.rig and a.bone:
            mount_world, n = hand_centre(a.rig, a.bone)
            log["hand_centre_verts"] = n
    else:
        order = sorted(range(3), key=lambda i: ext[i])  # thin, mid, long
        thin, midx, long_ = order
        c = (lo + hi) / 2

        def bunch(side):
            # mean distance from the thin axis of the 3% of verts nearest this extreme: small = dome apex
            key = (lambda p: p[thin]) if side < 0 else (lambda p: -p[thin])
            sel = sorted(pts, key=key)[:max(10, len(pts) // 33)]
            return sum(((p[midx] - c[midx]) ** 2 + (p[long_] - c[long_]) ** 2) ** 0.5 for p in sel) / len(sel)

        dome_sign = 1 if bunch(+1) < bunch(-1) else -1
        # rows of R: new X <- mid axis, new Y <- thin axis (dome to +Y), new Z <- long axis
        R = Matrix.Identity(3) * 0
        R[0][midx] = 1
        R[1][thin] = dome_sign
        R[2][long_] = 1
        if R.determinant() < 0:
            R[0][midx] = -1
        Rm = R.to_4x4()
        bake(meshes, Rm @ Matrix.Translation(-c))
        pts = prop_world_verts(meshes)
        lo2 = Vector([min(p[i] for p in pts) for i in range(3)])
        hi2 = Vector([max(p[i] for p in pts) for i in range(3)])
        e2 = hi2 - lo2
        tmin, tmax = vec(a.target_min), vec(a.target_max)
        te = tmax - tmin
        if a.scale:
            sv = Vector((a.scale,) * 3)
        elif a.uniform:
            u = min(te.x / e2.x, te.z / e2.z)
            sv = Vector((u, u, u))
        else:
            sv = Vector((te.x / e2.x, te.y / e2.y, te.z / e2.z))  # per axis: the dome fills the box
        bake(meshes, Matrix.Diagonal((sv.x, sv.y, sv.z, 1.0)))
        pts = prop_world_verts(meshes)
        lo3 = Vector([min(p[i] for p in pts) for i in range(3)])
        hi3 = Vector([max(p[i] for p in pts) for i in range(3)])
        # place: centred in X and Z on the box, dome apex on the box's back face (max Y)
        place = Vector(((tmin.x + tmax.x) / 2 - (lo3.x + hi3.x) / 2, tmax.y - hi3.y, (tmin.z + tmax.z) / 2 - (lo3.z + hi3.z) / 2))
        flat_centre = Vector(((lo3.x + hi3.x) / 2, lo3.y, (lo3.z + hi3.z) / 2)) + place
        bake(meshes, Matrix.Translation(place - flat_centre))  # pivot = flat-side centre at the origin
        mount_world = flat_centre
        log.update({"axes_in_long_mid_thin": ["XYZ"[long_], "XYZ"[midx], "XYZ"[thin]],
                    "dome_side": "+" if dome_sign > 0 else "-", "scale_xyz": [round(x, 4) for x in sv],
                    "size_out_m": [round((hi3[i] - lo3[i]), 4) for i in range(3)],
                    "target_min": list(tmin), "target_max": list(tmax)})

    if mount_world is not None and a.rig and a.bone:
        gltf = read_gltf_json(a.rig)
        G, A = bone_rest_in_skeleton(gltf, a.bone)
        p_world = blender_to_gltf(mount_world)
        p_skel = A.inverted() @ p_world  # metres -> skeleton cm
        child = G.inverted() @ Matrix.Translation(p_skel) @ Matrix.Scale(1.0 / A.to_scale().x, 4)
        rows = [[round(child[r][c], 6) for c in range(4)] for r in range(3)]
        tscn = "Transform3D(%s)" % ", ".join(str(x) for x in
                                             [rows[0][0], rows[0][1], rows[0][2], rows[1][0], rows[1][1], rows[1][2],
                                              rows[2][0], rows[2][1], rows[2][2], rows[0][3], rows[1][3], rows[2][3]])
        log["mount"] = {"rig": a.rig, "bone": a.bone,
                        "point_gltf_m": [round(x, 4) for x in p_world],
                        "child_matrix_rows_bone_space_cm": rows,
                        "godot_child_transform": tscn,
                        "note": "BoneAttachment3D(bone) > child Node3D with this transform > prop scene. "
                                "Basis is written row by row (Godot's Transform3D text order)."}

    for o in list(bpy.data.objects):
        o.select_set(o in meshes)
    bpy.ops.export_scene.gltf(filepath=a.out, export_format="GLB", use_selection=True,
                              export_animations=False, export_yup=True)
    log["sha256_in"], log["sha256_out"] = sha(a.inp), sha(a.out)
    print("prep_prop:", json.dumps(log))
    if a.log:
        json.dump(log, open(a.log, "w"), indent=1)


if __name__ == "__main__":
    main()
