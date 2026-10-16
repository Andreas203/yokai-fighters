"""Clearance of rigid props mounted on a fighter, through the stress poses (rules F5, F6, G4; YOK-102).

A prop from prep_prop.py (club, shell) rides a bone rigidly, as the Godot BoneAttachment3D does: its
rest placement is the rig's Armature node x the bone's rest x the BoneAttachment3D child transform from
the prep_prop log (exactly what Godot builds, so a fighter scaled on its Armature node scales the prop),
and in a pose the bone's rest-to-pose change is applied on top. The rig is put through rig_check.py's stress poses (same
definitions, imported from it) plus an arms-down pose (the G4 risk for a back shell), and for each pose:
  inside   prop verts more than --depth m inside the body surface (nearest body face, against its normal),
           ignoring prop verts within --ignore-radius m of the mount point (the grip inside the fist)
  new      of those, the verts that were not inside at rest (a shell's underside sits against the back by
           design, so the change from rest is the clipping the pose causes); the verdict reads this one
  floor    the prop's lowest point (the poses put the body's lowest point on the ground, z 0)
  contact  prop verts within --contact m (default 0.03) of the body at rest; per pose their mean and max
           distance from the body (how far a shell stands off the back)
  stills   front, side and back Workbench renders, <out>/<tag>-<pose>-<view>.png
F5: the poses are transient check poses from rig_check.py; nothing is keyed and no file is saved but the
stills and the report.

Run (Blender 4.x, headless):
  Blender -b --python tools/blender/prop_clearance.py -- --rig <rig.glb> --prop <prop.glb> --log <prep_prop log.json> \
      --out <dir> --tag kappa-shell [--ignore-radius 0.0] [--depth 0.01] [--prop ... --log ... (repeat)]
"""

import json
import math
import os
import sys

import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import rig_check  # noqa: E402  (STRESS_POSES, apply_pose, reset_pose)
import prep_prop  # noqa: E402  (read_gltf_json, bone_rest_in_skeleton)

C = Matrix(((1, 0, 0, 0), (0, 0, -1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))  # glTF -> Blender axes

EXTRA_POSES = {"arms_down": [("LeftArm", "Y", 75), ("RightArm", "Y", -75)]}
VIEWS = {"front": ((0, -10, 0), (90, 0, 0)), "side": ((10, 0, 0), (90, 0, 90)), "back": ((0, 10, 0), (90, 0, 180))}


def parse():
    argv = sys.argv[sys.argv.index("--") + 1:]
    a = {"props": [], "logs": [], "ignore": 0.0, "depth": 0.01, "contact": 0.03, "tag": "prop",
         "out": "prop_clearance_out"}
    i = 0
    while i < len(argv):
        k, v = argv[i].lstrip("-"), argv[i + 1]
        if k == "prop":
            a["props"].append(v)
        elif k == "log":
            a["logs"].append(v)
        elif k == "ignore-radius":
            a["ignore"] = float(v)
        elif k == "depth":
            a["depth"] = float(v)
        elif k == "contact":
            a["contact"] = float(v)
        else:
            a[k] = v
        i += 2
    return a


def gltf_to_blender(v):
    return Vector((v[0], -v[2], v[1]))


def setup_render():
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.display.shading.color_type = "TEXTURE"
    sc.display.shading.light = "FLAT"
    sc.render.resolution_x = sc.render.resolution_y = 640
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    sc.collection.objects.link(cam)
    sc.camera = cam
    cam.data.type = "ORTHO"
    return cam


def main():
    a = parse()
    os.makedirs(a["out"], exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=a["rig"])
    arm = [o for o in bpy.context.scene.objects if o.type == "ARMATURE"][0]
    body = [o for o in bpy.context.scene.objects
            if o.type == "MESH" and any(m.type == "ARMATURE" for m in o.modifiers)]
    height = max((o.matrix_world @ Vector(c)).z for o in body for c in o.bound_box)

    props = []
    for path, logp in zip(a["props"], a["logs"]):
        log = json.load(open(logp))
        bone = log["mount"]["bone"]
        before = set(bpy.data.objects)
        bpy.ops.import_scene.gltf(filepath=path)
        objs = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
        # rest placement exactly as Godot builds it: Armature node x bone rest x the BoneAttachment3D
        # child transform from the prep_prop log (so a fighter scaled on its Armature node scales the
        # prop too)
        gj = prep_prop.read_gltf_json(a["rig"])
        G, A = prep_prop.bone_rest_in_skeleton(gj, bone)
        rows = log["mount"]["child_matrix_rows_bone_space_cm"]
        child = Matrix([rows[0], rows[1], rows[2], [0, 0, 0, 1]])
        rest_world = C @ (A @ G @ child) @ C.inverted()
        mount = rest_world.to_translation()
        for o in objs:
            o.data.transform(o.matrix_world)
            o.matrix_world = Matrix.Identity(4)
        props.append({"path": path, "bone": bone, "objs": objs, "mount": mount, "rest": rest_world,
                      "rest_co": [[v.co.copy() for v in o.data.vertices] for o in objs]})

    cam = setup_render()
    poses = {"rest": []}
    poses.update(rig_check.STRESS_POSES)
    poses.update(EXTRA_POSES)
    report = {"tool": "prop_clearance.py", "rig": a["rig"], "props": a["props"], "depth_m": a["depth"],
              "ignore_radius_m": a["ignore"], "poses": {}}
    for name, pose in poses.items():
        rig_check.apply_pose(arm, body, pose, lambda n: n)
        bpy.context.view_layer.update()
        dg = bpy.context.evaluated_depsgraph_get()
        bvhs = [BVHTree.FromObject(o.evaluated_get(dg), dg, deform=True) for o in body]
        # BVHTree.FromObject works in object space; body objects sit under the armature with a world matrix
        mws = [o.matrix_world.copy() for o in body]
        entry = {}
        for p in props:
            pb = arm.pose.bones[p["bone"]]
            delta = (arm.matrix_world @ pb.matrix) @ (arm.matrix_world @ pb.bone.matrix_local).inverted()
            M = delta @ p["rest"]
            mount_now = M @ Vector((0, 0, 0))
            inside, worst, new, new_worst = 0, 0.0, 0, 0.0
            rest_in = p.setdefault("rest_inside", None)
            now_in = set()
            for oi, (o, rest) in enumerate(zip(p["objs"], p["rest_co"])):
                for vi, (v, co) in enumerate(zip(o.data.vertices, rest)):
                    w = M @ co
                    v.co = w
                    if a["ignore"] and (w - mount_now).length < a["ignore"]:
                        continue
                    for bvh, mw in zip(bvhs, mws):
                        loc, nrm, _i, _d = bvh.find_nearest(mw.inverted() @ w)
                        if loc is None:
                            continue
                        locw = mw @ loc
                        nw = (mw.to_3x3().inverted().transposed() @ nrm).normalized()
                        d = (w - locw).dot(nw)
                        if d < -a["depth"]:
                            inside += 1
                            worst = max(worst, -d)
                            now_in.add((oi, vi))
                            if rest_in is not None and (oi, vi) not in rest_in:
                                new += 1
                                new_worst = max(new_worst, -d)
                            break
                o.data.update()
            if rest_in is None:
                p["rest_inside"] = now_in
            # floor and contact: the prop's lowest point, and how far the verts that touch the body at
            # rest (within --contact m) stand off it now (a shell lifting off the back)
            wv = [M @ co for rest in p["rest_co"] for co in rest]
            zmin = min(w.z for w in wv)
            dists = []
            for i, w in enumerate(wv):
                dmin = 9.0
                for bvh, mw in zip(bvhs, mws):
                    loc, _n, _i, _d = bvh.find_nearest(mw.inverted() @ w)
                    if loc is not None:
                        dmin = min(dmin, (w - mw @ loc).length)
                dists.append(dmin)
            if "contact" not in p:
                p["contact"] = [i for i, d in enumerate(dists) if d < a["contact"]]
            cd = [dists[i] for i in p["contact"]] or [0.0]
            entry_extra = {"prop_min_z_m": round(zmin, 4), "contact_verts": len(p["contact"]),
                           "contact_gap_mean_m": round(sum(cd) / len(cd), 4), "contact_gap_max_m": round(max(cd), 4)}
            entry[os.path.basename(p["path"])] = {"verts_inside": inside, "worst_depth_m": round(worst, 4),
                                                  **entry_extra,
                                                  "new_vs_rest": new, "new_worst_depth_m": round(new_worst, 4),
                                                  "verts_total": sum(len(o.data.vertices) for o in p["objs"])}
        stills = []
        for view, (loc, rot) in VIEWS.items():
            cam.location = Vector(loc) + Vector((0, 0, height * 0.5))
            cam.rotation_euler = [math.radians(x) for x in rot]
            cam.data.ortho_scale = height * 1.25
            f = os.path.join(a["out"], f"{a['tag']}-{name.replace('_', '-')}-{view}.png")
            bpy.context.scene.render.filepath = f
            bpy.ops.render.render(write_still=True)
            stills.append(f)
        report["poses"][name] = {"props": entry, "stills": stills}
        print("prop_clearance:", name, json.dumps(entry))
    json.dump(report, open(os.path.join(a["out"], f"{a['tag']}-clearance.json"), "w"), indent=1)


if __name__ == "__main__":
    main()
