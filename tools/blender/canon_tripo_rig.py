"""Make the canonical shared-skeleton rig from a Tripo auto-rig GLB, by script (rules F5, F6, E15; AM5; YOK-146).

Why this exists. Tripo's Mixamo-preset GLB (`Ryo.glb`, generator "Tripo") has two faults that make it tear in
Godot and in Blender as delivered:
  1. No joint node carries a translation or rotation: the bind pose lives only in the inverse bind matrices.
     A glTF viewer skins with joint world = identity, so every vertex is pushed by its own IBM and the body
     flies apart (stills: assets/specs/rigs/stills/ryo-tripo/raw_import_*.png).
  2. The IBMs are in a different frame from the mesh: the bind skeleton is Z-up and centred on the body
     (an FBX frame), while POSITION is Y-up, feet at Y=0 and recentred on X/Z.
The weights themselves are clean (max 4 influences, normalised). So the rig is rebuilt from the file's own data,
nothing placed by hand: the rigid frame change T (skeleton frame -> mesh frame) is found by fitting the bind
skeleton to the weights (each vertex near the bone that owns it), the bind joints are mapped through T, and a new
armature is built at those joints with the mesh skinned by the file's own JOINTS_0/WEIGHTS_0.

What the output is (the Option A shared skeleton, assets/specs/rigs/skeleton.md):
  - bone names without the `mixamorig:` prefix (Godot's glTF import turns `:` into `_`, and FighterModel.cs,
    the fighter scenes and these scripts look bones up by bare name: `Hips`, `RightHand`);
  - E15 mitt hands: the 40 finger joints are removed and their weights added to `LeftHand` / `RightHand`
    (per vertex, then renormalised); no clip can curl a finger;
  - scaled to --height (default 1.75 m) about the origin, feet at Y=0, centred on the ankles in X/Z,
    Y-up, facing glTF +Z, character's left = +X; bones in metres under an unscaled `Armature` node;
  - the T-pose is the file's bind pose (no re-posing); no animation.

Run (Blender 4.1.1, headless; no generation job, no credits):
  blender -b --python tools/blender/canon_tripo_rig.py -- --in <tripo.glb> --out <rig.glb> \
      [--height 1.75] [--log <log.json>] [--prefix mixamorig:] [--mesh-name ryo]
"""
import itertools
import json
import os
import struct
import sys

import bpy
import numpy as np
from mathutils import Matrix, Vector

FINGERS = ("Thumb", "Index", "Middle", "Ring", "Pinky")


def args():
    a = sys.argv[sys.argv.index("--") + 1:]
    get = lambda k, d=None: a[a.index(k) + 1] if k in a else d
    return {"inp": get("--in"), "out": get("--out"), "height": float(get("--height", 1.75)),
            "log": get("--log"), "prefix": get("--prefix", "mixamorig:"), "mesh_name": get("--mesh-name", "ryo")}


def read_glb(path):
    b = open(path, "rb").read()
    ln, _ = struct.unpack("<II", b[12:20])
    j = json.loads(b[20:20 + ln])
    off0 = 20 + ln + 8

    def acc(i):
        a = j["accessors"][i]
        bv = j["bufferViews"][a["bufferView"]]
        n = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}[a["type"]]
        dt = {5126: np.float32, 5123: np.uint16, 5121: np.uint8, 5125: np.uint32}[a["componentType"]]
        if bv.get("byteStride") not in (None, n * np.dtype(dt).itemsize):
            sys.exit("canon_tripo_rig: interleaved accessors are not supported")
        x = np.frombuffer(b, dt, a["count"] * n, off0 + bv.get("byteOffset", 0) + a.get("byteOffset", 0))
        x = x.reshape(a["count"], n).astype(np.float64)
        if a.get("normalized"):
            x /= float(np.iinfo(dt).max)
        return x
    if len(j["meshes"]) != 1 or len(j["meshes"][0]["primitives"]) != 1 or len(j["skins"]) != 1:
        sys.exit("canon_tripo_rig: expected one mesh with one primitive and one skin")
    prim = j["meshes"][0]["primitives"][0]
    sk = j["skins"][0]
    joints = sk["joints"]
    names = [j["nodes"][n]["name"] for n in joints]
    parent = {}
    for i, n in enumerate(j["nodes"]):
        for c in n.get("children", []):
            parent[c] = i
    jpar = [names[joints.index(parent[n])] if parent.get(n) in joints else None for n in joints]
    trs = [k for n in joints for k in ("translation", "rotation", "scale", "matrix") if k in j["nodes"][n]]
    M = acc(sk["inverseBindMatrices"]).reshape(-1, 4, 4).transpose(0, 2, 1)  # column-major -> row-major
    return {"gltf": j, "P": acc(prim["attributes"]["POSITION"]), "J": acc(prim["attributes"]["JOINTS_0"]).astype(int),
            "W": acc(prim["attributes"]["WEIGHTS_0"]), "bind": np.linalg.inv(M), "names": names, "parents": jpar,
            "joint_trs_keys": trs}


def seg_dist(X, a, e):
    d = e - a
    L = float(d @ d)
    t = np.clip((X - a) @ d / max(L, 1e-12), 0, 1)
    return np.linalg.norm(X - (a + t[:, None] * d), axis=1)


def fit_frame(g):
    """Rigid+uniform-scale T with mesh = s * R @ skel + t, R one of the 24 axis rotations, by the weights."""
    P, J, W, pos = g["P"], g["J"], g["W"], g["bind"][:, :3, 3]
    names = g["names"]
    kids = {i: [k for k, p in enumerate(g["parents"]) if p == names[i]] for i in range(len(names))}
    dom = J[np.arange(len(J)), W.argmax(1)]
    sel = W.max(1) > 0.6
    groups = [(i, np.where(sel & (dom == i))[0]) for i in range(len(names)) if kids[i]]
    groups = [(i, v) for i, v in groups if len(v) >= 20]

    def score(R, s, t, per_bone=False):
        Q = s * pos @ R.T + t
        d = {names[i]: seg_dist(P[v], Q[i], Q[kids[i]].mean(0)) for i, v in groups}
        allv = np.concatenate(list(d.values()))
        val = float(np.mean(np.minimum(allv, 0.10)))
        return (val, d) if per_bone else val

    rots = []
    for perm in itertools.permutations(range(3)):
        for sg in itertools.product((1, -1), repeat=3):
            R = np.zeros((3, 3))
            for r, (c, x) in enumerate(zip(perm, sg)):
                R[r, c] = x
            if np.linalg.det(R) > 0:
                rots.append(R)
    cands = []
    for R in rots:
        Q = pos @ R.T
        mids = np.array([(Q[i] + Q[kids[i]].mean(0)) / 2 for i, _ in groups])
        cents = np.array([P[v].mean(0) for _, v in groups])
        t = np.average(cents - mids, axis=0, weights=[len(v) for _, v in groups])
        cands.append((score(R, 1.0, t), R, t))
    cands.sort(key=lambda c: c[0])
    best, R, t = cands[0]
    s = 1.0
    step = np.array([0.01, 0.01, 0.01, 0.01])  # t (m) and s
    x = np.array([*t, s])
    f = lambda x: score(R, x[3], x[:3])
    fx = f(x)
    for _ in range(60):  # coordinate descent, halving the step when nothing improves
        moved = False
        for k in range(4):
            for sgn in (1, -1):
                y = x.copy()
                y[k] += sgn * step[k]
                fy = f(y)
                if fy < fx:
                    x, fx, moved = y, fy, True
        if not moved:
            step /= 2
            if step.max() < 1e-4:
                break
    _, per = score(R, x[3], x[:3], per_bone=True)
    return {"R": R, "t": x[:3], "s": float(x[3]), "score_m": fx, "runner_up_score_m": cands[1][0],
            "per_bone_median_m": {k: round(float(np.median(v)), 4) for k, v in per.items()}}


def bare(name, prefix):
    return name[len(prefix):] if prefix and name.startswith(prefix) else name


def is_finger(name):
    return any(("Hand" + f) in name for f in FINGERS)


def finger_hand(name):
    return ("Left" if name.startswith("Left") else "Right") + "Hand"


G2B = Matrix(((1, 0, 0), (0, 0, -1), (0, 1, 0)))  # glTF (Y-up) -> Blender (Z-up): (x, y, z) -> (x, -z, y)


def main():
    a = args()
    g = read_glb(a["inp"])
    fit = fit_frame(g)
    R, t, s = fit["R"], fit["t"], fit["s"]
    P = g["P"]
    names = [bare(n, a["prefix"]) for n in g["names"]]
    parents = [bare(p, a["prefix"]) if p else None for p in g["parents"]]

    # Bind joints in the mesh frame (glTF axes), then the uniform height scale and the ankle centring.
    jpos = s * g["bind"][:, :3, 3] @ R.T + t
    jrot = np.array([R @ g["bind"][i, :3, :3] for i in range(len(names))])
    jrot /= np.linalg.norm(jrot, axis=1, keepdims=True)  # unit columns (bind matrices carry no scale)
    h0 = float(P[:, 1].max() - P[:, 1].min())
    k = a["height"] / h0
    ank = (jpos[names.index("LeftFoot")] + jpos[names.index("RightFoot")]) / 2
    shift = np.array([-ank[0], -P[:, 1].min(), -ank[2]])
    Pn = (P + shift) * k
    jposn = (jpos + shift) * k

    # Weights: fingers into the hand, prefix stripped, renormalised.
    J, W = g["J"], g["W"].copy()
    keep = [n for n in names if not is_finger(n)]
    col = {n: i for i, n in enumerate(keep)}
    Wd = np.zeros((len(P), len(keep)))
    finger_verts = 0
    for c in range(4):
        tgt = np.array([col[finger_hand(names[j]) if is_finger(names[j]) else names[j]] for j in J[:, c]])
        fw = np.array([is_finger(names[j]) for j in J[:, c]]) & (W[:, c] > 0)
        finger_verts += int(fw.sum())
        np.add.at(Wd, (np.arange(len(P)), tgt), W[:, c])
    sums_before = Wd.sum(1)
    Wd /= np.where(sums_before > 0, sums_before, 1)[:, None]
    infl = (Wd > 1e-4).sum(1)

    # Scene: the imported mesh (faces, UVs, material) with its vertices set from POSITION, a new armature.
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=os.path.abspath(a["inp"]))
    meshes = [o for o in bpy.data.objects if o.type == "MESH" and o.vertex_groups]
    if len(meshes) != 1 or len(meshes[0].data.vertices) != len(P):
        sys.exit("canon_tripo_rig: imported mesh does not match the POSITION accessor (vertex count)")
    me = meshes[0]
    for o in list(bpy.data.objects):
        if o is not me:
            bpy.data.objects.remove(o, do_unlink=True)
    for ar in list(bpy.data.armatures):
        bpy.data.armatures.remove(ar)
    me.modifiers.clear()
    me.parent = None
    me.matrix_world = Matrix.Identity(4)
    bco = Pn @ np.array(G2B).T
    me.data.vertices.foreach_set("co", bco.ravel())
    me.data.update()
    me.vertex_groups.clear()
    vgs = [me.vertex_groups.new(name=n) for n in keep]
    for vi in range(len(P)):
        for ci in np.nonzero(Wd[vi] > 1e-4)[0]:
            vgs[ci].add([vi], float(Wd[vi, ci]), "REPLACE")
    me.name = a["mesh_name"]
    me.data.name = a["mesh_name"]

    ad = bpy.data.armatures.new("Armature")
    arm = bpy.data.objects.new("Armature", ad)
    bpy.context.scene.collection.objects.link(arm)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    child_heads = {}
    for i, n in enumerate(names):
        if parents[i]:
            child_heads.setdefault(parents[i], []).append((n, jposn[i]))
    ebs = {}
    for i, n in enumerate(names):
        if is_finger(n):
            continue
        eb = ad.edit_bones.new(n)
        rot = G2B @ Matrix([list(r) for r in jrot[i]])
        head = G2B @ Vector(jposn[i])
        m = rot.to_4x4()
        m.translation = head
        eb.head = head
        eb.tail = head + Vector((0, 0.1, 0))
        eb.matrix = m
        kids = [p for cn, p in child_heads.get(n, []) if not is_finger(cn)]
        length = (float(np.linalg.norm(np.mean(kids, 0) - jposn[i])) if kids else
                  float(np.linalg.norm(jposn[i] - jposn[names.index(parents[i])])) * 0.5)
        if n.endswith("Hand"):
            length = 0.08 * k  # mitt: no finger children kept
        eb.length = max(length, 0.02)
        ebs[n] = eb
    for i, n in enumerate(names):
        if n in ebs and parents[i]:
            ebs[n].parent = ebs[parents[i]]
            ebs[n].use_connect = False
    bpy.ops.object.mode_set(mode="OBJECT")
    me.parent = arm
    mod = me.modifiers.new("Armature", "ARMATURE")
    mod.object = arm

    # Rest == bind check: the skinned mesh at rest must not move.
    dg = bpy.context.evaluated_depsgraph_get()
    ev = me.evaluated_get(dg).to_mesh()
    evco = np.empty(len(ev.vertices) * 3)
    ev.vertices.foreach_get("co", evco)
    me.evaluated_get(dg).to_mesh_clear()
    rest_off = float(np.abs(evco.reshape(-1, 3) - bco).max())

    os.makedirs(os.path.dirname(os.path.abspath(a["out"])), exist_ok=True)
    for ac in list(bpy.data.actions):
        bpy.data.actions.remove(ac)
    bpy.ops.export_scene.gltf(filepath=os.path.abspath(a["out"]), export_format="GLB", export_animations=False,
                              export_skins=True, export_yup=True, export_apply=False, export_texcoords=True,
                              export_normals=True, export_materials="EXPORT")
    log = {
        "tool": "tools/blender/canon_tripo_rig.py", "blender": bpy.app.version_string, "in": a["inp"],
        "out": a["out"], "input_faults": {
            "joint_nodes_with_trs": len(g["joint_trs_keys"]),
            "note": "no joint node has TRS: the bind pose is only in the IBMs, which are in a Z-up frame"},
        "frame_fit": {"R_skel_to_mesh": R.astype(int).tolist(), "t_m": [round(float(x), 5) for x in t],
                      "s": round(s, 5), "mean_clipped_dist_m": round(fit["score_m"], 5),
                      "runner_up_rotation_dist_m": round(fit["runner_up_score_m"], 5),
                      "per_bone_median_dist_m": fit["per_bone_median_m"]},
        "source_height_m": round(h0, 5), "height_m": a["height"], "scale_factor": round(k, 6),
        "shift_before_scale_m": [round(float(x), 5) for x in shift],
        "bones_in": len(names), "bones_out": len(keep), "bones_out_names": keep,
        "fingers": {"removed": [n for n in names if is_finger(n)], "verts_with_finger_weight": finger_verts,
                    "rule": "finger weight added to the same-side Hand bone, then renormalised (E15 mitts)"},
        "weights": {"max_influences": int(infl.max()), "unweighted": int((sums_before <= 0).sum()),
                    "renormalised_max_change": round(float(np.abs(sums_before - 1).max()), 6)},
        "rest_vs_bind_max_offset_m": round(rest_off, 6),
        "joint_heads_gltf_m": {n: [round(float(x), 4) for x in jposn[i]] for i, n in enumerate(names) if not is_finger(n)},
    }
    print("canon_tripo_rig:", json.dumps({k2: log[k2] for k2 in ("frame_fit", "scale_factor", "bones_out",
                                                                 "weights", "rest_vs_bind_max_offset_m")}))
    if a["log"]:
        with open(a["log"], "w", encoding="utf-8") as f:
            json.dump(log, f, indent=1)


main()
sys.stdout.flush()
os._exit(0)
