"""Cut a baked-on part off a rigged fighter and cap the hole (rules F5, F6; YOK-102, designer 2026-10-16).

Built for the Kappa: Meshy fused the shell to the back of the body mesh, and the auto-skin weights it to
the arms and legs, so it crumples in the crouch and kick. The designer chose to cut it off and mount the
separate rigid `shell.glb` on a spine BoneAttachment3D instead (fitted by prep_prop.py).

What it does (Blender world axes after glTF import: +Z up, the character faces -Y, its left is +X):
  0. import with merge_vertices and weld verts within --weld m, so UV-seam splits are joined and the
     islands and holes are real (the exporter splits seams again);
  1. select every vert of the skinned mesh inside the box: depth y > --y-min (behind the back),
     --z-min < z < --z-max, |x| < --x-max (metres, rest pose);
  2. delete them, then delete loose islands under --min-island verts that lie wholly within 0.10 m of the box
     (inner shell bits; islands elsewhere, such as eyes, are kept);
  3. fill each open boundary loop next to the box (both edge verts within --cap-margin of it; other
     open edges of the model, such as a hem, are left alone) with faces (no new verts, so every cap vert keeps its own skin weights)
     and triangulate the fill;
  4. give the cap's face corners the UV of one body-skin sample point (--skin-sample, default the back of the
     left upper arm), so the cap reads as plain skin under the toon shader;
  5. export the skinned GLB (armature + mesh + texture, no animation).
The skeleton, rest pose and the remaining verts' weights are not changed. Nothing is keyed (F5).
The log records the box, the verts removed, the cut part's rest bounding box (input for prep_prop.py
`--mode fit`), the cap faces and both sha256s.

Run (Blender 4.x, headless):
  /Applications/Blender.app/Contents/MacOS/Blender -b --python tools/blender/cut_shell.py -- \
      --in game/assets/generated/characters/kappa/kappa-rigged.glb \
      --out game/assets/generated/characters/kappa/kappa-rigged-shellcut.glb --log <log.json> \
      [--y-min 0.17] [--z-min 0.58] [--z-max 1.29] [--x-max 0.37] [--min-island 200] \
      [--skin-sample 0.45,0.10,1.10]
"""

import argparse
import hashlib
import json
import sys

import bmesh
import bpy
from mathutils import Vector


def args():
    ap = argparse.ArgumentParser()
    ap.add_argument("--in", dest="inp", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--log")
    ap.add_argument("--y-min", type=float, default=0.17)
    ap.add_argument("--z-min", type=float, default=0.58)
    ap.add_argument("--z-max", type=float, default=1.29)
    ap.add_argument("--x-max", type=float, default=0.37)
    ap.add_argument("--min-island", type=int, default=200)
    ap.add_argument("--cap-margin", type=float, default=0.04)
    ap.add_argument("--weld", type=float, default=1e-5)
    ap.add_argument("--skin-sample", default="0.45,0.10,1.10")
    return ap.parse_args(sys.argv[sys.argv.index("--") + 1:])


def sha(p):
    return hashlib.sha256(open(p, "rb").read()).hexdigest()


def islands(bm):
    seen, out = set(), []
    for v in bm.verts:
        if v.index in seen:
            continue
        stack, comp = [v], []
        seen.add(v.index)
        while stack:
            c = stack.pop()
            comp.append(c)
            for e in c.link_edges:
                o = e.other_vert(c)
                if o.index not in seen:
                    seen.add(o.index)
                    stack.append(o)
        out.append(comp)
    return out


def main():
    a = args()
    if a.inp == a.out:
        raise SystemExit("refusing to overwrite the input")
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=a.inp, merge_vertices=True)  # weld UV-seam splits so islands and holes are real
    meshes = [o for o in bpy.context.scene.objects
              if o.type == "MESH" and any(m.type == "ARMATURE" for m in o.modifiers)]
    if len(meshes) != 1:
        raise SystemExit(f"expected one skinned mesh, found {len(meshes)}")
    obj = meshes[0]
    mw = obj.matrix_world
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    verts_before = len(bm.verts)
    open_before = sum(1 for e in bm.edges if e.is_boundary)
    welded = bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=a.weld)
    bm.verts.ensure_lookup_table()
    open_welded = sum(1 for e in bm.edges if e.is_boundary)

    def near_box(v, m=a.cap_margin):
        p = mw @ v.co
        return p.y > a.y_min - m and a.z_min - m < p.z < a.z_max + m and abs(p.x) < a.x_max + m

    def inside(v):
        p = mw @ v.co
        return p.y > a.y_min and a.z_min < p.z < a.z_max and abs(p.x) < a.x_max

    cut = [v for v in bm.verts if inside(v)]
    cut_world = [mw @ v.co for v in cut]
    if not cut:
        raise SystemExit("nothing inside the box")
    bb_min = [min(p[i] for p in cut_world) for i in range(3)]
    bb_max = [max(p[i] for p in cut_world) for i in range(3)]
    bmesh.ops.delete(bm, geom=cut, context="VERTS")

    bm.verts.index_update()
    small = [c for c in islands(bm) if len(c) < a.min_island and all(near_box(v, 0.10) for v in c)]
    loose = [v for c in small for v in c]
    if loose:
        bmesh.ops.delete(bm, geom=loose, context="VERTS")

    boundary = [e for e in bm.edges if e.is_boundary and all(near_box(v) for v in e.verts)]
    other_open = sum(1 for e in bm.edges if e.is_boundary) - len(boundary)
    res = bmesh.ops.holes_fill(bm, edges=boundary, sides=0)
    cap = res["faces"]
    tri = bmesh.ops.triangulate(bm, faces=cap, quad_method="BEAUTY", ngon_method="BEAUTY")
    cap = tri["faces"]

    # Cap UVs: the UV of the body corner nearest the skin sample point.
    uv = bm.loops.layers.uv.active
    sample = Vector([float(s) for s in a.skin_sample.split(",")])
    cap_set = set(cap)
    best, best_d = None, 1e9
    for f in bm.faces:
        if f in cap_set:
            continue
        for lp in f.loops:
            d = ((mw @ lp.vert.co) - sample).length
            if d < best_d:
                best, best_d = lp[uv].uv.copy(), d
    for f in cap:
        for lp in f.loops:
            lp[uv].uv = best

    bm.to_mesh(obj.data)
    verts_after = len(bm.verts)
    bm.free()
    obj.data.update()

    bpy.ops.export_scene.gltf(filepath=a.out, export_format="GLB", export_animations=False,
                              export_skins=True, export_yup=True)
    log = {
        "tool": "cut_shell.py", "in": a.inp, "out": a.out,
        "box": {"y_min": a.y_min, "z_min": a.z_min, "z_max": a.z_max, "x_max": a.x_max},
        "verts_before": verts_before, "open_edges_before": open_before, "open_edges_after_weld": open_welded, "verts_cut": len(cut), "loose_island_verts": len(loose),
        "verts_after": verts_after, "cap_faces": len(cap), "boundary_edges_filled": len(boundary), "open_edges_left_elsewhere": other_open,
        "cap_uv": list(best), "cap_uv_sample_dist_m": round(best_d, 4),
        "cut_bbox_blender_m": {"min": [round(x, 4) for x in bb_min], "max": [round(x, 4) for x in bb_max]},
        "sha256_in": sha(a.inp), "sha256_out": sha(a.out),
    }
    print("cut_shell:", json.dumps(log))
    if a.log:
        json.dump(log, open(a.log, "w"), indent=1)


if __name__ == "__main__":
    main()
