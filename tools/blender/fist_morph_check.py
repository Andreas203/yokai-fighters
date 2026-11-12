"""Check the open-hand rig with its "fist" morph target in Blender (fist as a blend shape, designer 2026-11-09;
fist_morph.py; rules F5, F6, E15).

Run (Blender 4.1.1, headless; nothing is written but the report):
  blender -b --python tools/blender/fist_morph_check.py -- --morph <rig-with-fist-morph.glb> \
      --fist <baked-fist.glb> --open <open-hand.glb> --out <report.json> [--name fist] [--pose-deg 40]

Imports the three files with the same glTF importer settings and checks:
  - the morph rig has exactly one shape key besides Basis, named --name, and the value the importer gave it
    (the glTF mesh default weight);
  - shape key at 1: evaluated vertex positions equal the baked fist; at 0: equal the open hand. Measured at the
    rest pose and again in a stress pose (both Hands and ForeArms rotated --pose-deg about the bone X axis,
    Spine twisted), so the skinning is compared too, not only the shape data;
  - vertex counts and bone names equal across the three.
"""
import json
import math
import sys

import bpy
import numpy as np


def args():
    a = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    d = {"--name": "fist", "--pose-deg": "40"}
    for i in range(0, len(a), 2):
        d[a[i]] = a[i + 1]
    return d


def load(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    mesh = [o for o in new if o.type == "MESH" and any(m.type == "ARMATURE" for m in o.modifiers)]
    arm = [o for o in new if o.type == "ARMATURE"]
    if len(mesh) != 1 or len(arm) != 1:
        raise SystemExit("fist_morph_check: %s: expected one mesh and one armature, got %d/%d"
                         % (path, len(mesh), len(arm)))
    return mesh[0], arm[0]


def coords(obj):
    dg = bpy.context.evaluated_depsgraph_get()
    ev = obj.evaluated_get(dg)
    me = ev.to_mesh()
    c = np.empty(len(me.vertices) * 3, np.float64)
    me.vertices.foreach_get("co", c)
    ev.to_mesh_clear()
    return (np.array(obj.matrix_world) @ np.c_[c.reshape(-1, 3), np.ones(len(c) // 3)].T).T[:, :3]


def pose(arm, deg):
    r = math.radians(deg)
    for pb in arm.pose.bones:
        pb.rotation_mode = "XYZ"
        n = pb.name
        if n.endswith("Hand") or n.endswith("ForeArm"):
            pb.rotation_euler = (r, 0.0, 0.0)
        elif n in ("Spine", "Spine1", "Spine01", "Spine02"):
            pb.rotation_euler = (0.0, r / 2, 0.0)
        else:
            pb.rotation_euler = (0.0, 0.0, 0.0)


def rest(arm):
    for pb in arm.pose.bones:
        pb.rotation_mode = "XYZ"
        pb.rotation_euler = (0.0, 0.0, 0.0)


def main():
    a = args()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    mM, aM = load(a["--morph"])
    mF, aF = load(a["--fist"])
    mO, aO = load(a["--open"])
    keys = mM.data.shape_keys.key_blocks if mM.data.shape_keys else []
    names = [k.name for k in keys]
    rep = {"morph": a["--morph"], "fist": a["--fist"], "open": a["--open"], "blender": bpy.app.version_string,
           "shape_keys": names, "fails": []}
    if len(keys) != 2 or names[1] != a["--name"]:
        rep["fails"].append("shape keys %s, want [Basis, %s]" % (names, a["--name"]))
        print("fist_morph_check:", json.dumps(rep))
        json.dump(rep, open(a["--out"], "w"), indent=1)
        sys.exit(2)
    key = keys[1]
    rep["imported_default_value"] = round(key.value, 6)
    rep["verts"] = [len(m.data.vertices) for m in (mM, mF, mO)]
    rep["bones_equal"] = ([b.name for b in aM.data.bones] == [b.name for b in aF.data.bones]
                          == [b.name for b in aO.data.bones])
    if len(set(rep["verts"])) != 1:
        rep["fails"].append("vertex counts differ %s" % rep["verts"])
    if not rep["bones_equal"]:
        rep["fails"].append("bone names differ")
    # put the three side by side at the origin (the importer places each at its own root)
    deg = float(a["--pose-deg"])
    for label, setp in (("rest", rest), ("stress", lambda arm: pose(arm, deg))):
        for arm in (aM, aF, aO):
            setp(arm)
        bpy.context.view_layer.update()
        res = {}
        for v, ref, tag in ((1.0, mF, "w1_vs_baked_fist"), (0.0, mO, "w0_vs_open")):
            key.value = v
            bpy.context.view_layer.update()
            d = np.abs(coords(mM) - coords(ref))
            res[tag + "_max_m"] = float(d.max())
            res[tag + "_verts_over_1e-5"] = int((d.max(axis=1) > 1e-5).sum())
            if d.max() > 1e-5:
                rep["fails"].append("%s %s max %.3g m" % (label, tag, d.max()))
        dh = np.abs(coords(mF) - coords(mO)).max(axis=1)
        res["baked_fist_vs_open_verts_moved"] = int((dh > 1e-5).sum())
        res["baked_fist_vs_open_max_m"] = float(dh.max())
        rep[label] = res
    key.value = rep["imported_default_value"]
    rep["verdict"] = "FAIL" if rep["fails"] else "PASS"
    print("fist_morph_check:", json.dumps(rep))
    json.dump(rep, open(a["--out"], "w"), indent=1)
    sys.exit(2 if rep["fails"] else 0)


main()
