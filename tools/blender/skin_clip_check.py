"""Play existing clips on one or more skins and compare their deformation (YOK-116).

For each clip GLB, its action is put on each rig's armature (same shared-skeleton bone names), the
clip is stepped frame by frame and every skin edge is measured against its rest length, the
same tear rule as rig_check.py (stretch > 3.0x or squash < 0.25x). Stills of the worst frame
and of any frames asked for are rendered side by side per rig, framed on the pelvis or the body.

Run (Blender 4.x, headless):
  /Applications/Blender.app/Contents/MacOS/Blender -b --python tools/blender/skin_clip_check.py -- \
      --rig before=<a.glb> --rig after=<b.glb> \
      --clip <clip.glb>[:f1,f2,...][@a-b] [--clip ...]  --out <dir> [--no-render] [--fps 60]

  --rotations-only 1 plays only the clip's rotation curves, as the game does on rigs of other proportions
  (default off, so the YOK-116 runs repeat exactly).
  --fps sets the scene frame rate before any import (default: Blender's 24). With --fps 60 a frame is
  one game tick, so the frames and the @a-b range (inclusive) can be the clip data's raw trim ticks:
  only frames a..b are measured (YOK-102 G4: the game plays trim_start..trim_end, not the whole take).

F5: the clips are existing generated clips, played as they are; nothing is keyed or saved, and the
rig files are never written. Writes <out>/clip_report.json and <out>/<clip>_f<frame>_<rig>_<view>.png.
"""
import bpy
import json
import math
import os
import sys

import numpy as np
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import clip_common as cc  # noqa: E402  (YOK-146: bone-name normaliser)

STRETCH_FAIL, SQUASH_FAIL, STRETCH_WARN = 3.0, 0.25, 1.8


def game_tracks(act):
    """YOK-102 G4: the game (FighterModel.BuildLibrary) keeps a clip's rotations only on the shared skeleton:
    per-bone translation and scale are dropped (bones keep the rig's own lengths) and the hips only carry a
    height, scaled per rig (YOK-130). A whole-body offset changes neither skin stretch nor prop clearance, so
    for these checks every location and scale curve is removed from a copy of the action."""
    cp = act.copy()
    cp.use_fake_user = True
    for fc in list(cp.fcurves):
        if fc.data_path.endswith(".location") or fc.data_path.endswith(".scale"):
            cp.fcurves.remove(fc)
    return cp


def args():
    argv = sys.argv[sys.argv.index("--") + 1:]
    a = {"rig": [], "clip": [], "out": "skin_clip_out", "render": True}
    i = 0
    while i < len(argv):
        if argv[i] == "--no-render":
            a["render"] = False
            i += 1
            continue
        k, v = argv[i].lstrip("-"), argv[i + 1]
        if k in ("rig", "clip"):
            a[k].append(v)
        else:
            a[k] = v
        i += 2
    return a


def import_glb(path, tag):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.abspath(path))
    new = [o for o in bpy.data.objects if o not in before]
    for o in new:
        o.name = "%s__%s" % (tag, o.name)
    arm = [o for o in new if o.type == "ARMATURE"][0]
    mesh = [o for o in new if o.type == "MESH" and o.vertex_groups]
    for o in new:
        if o.type == "MESH" and o not in mesh:
            bpy.data.objects.remove(o, do_unlink=True)
    return arm, mesh[0] if mesh else None


def coords(obj):
    dg = bpy.context.evaluated_depsgraph_get()
    oe = obj.evaluated_get(dg)
    me = oe.to_mesh()
    co = np.empty(len(me.vertices) * 3)
    me.vertices.foreach_get("co", co)
    oe.to_mesh_clear()
    m = np.array(obj.matrix_world)
    return co.reshape(-1, 3) @ m[:3, :3].T + m[:3, 3]


def edges(obj):
    e = np.empty(len(obj.data.edges) * 2, np.int64)
    obj.data.edges.foreach_get("vertices", e)
    return e.reshape(-1, 2)


def owner(obj):
    names = [g.name for g in obj.vertex_groups]
    out = []
    for v in obj.data.vertices:
        best = max(v.groups, key=lambda g: g.weight, default=None)
        out.append(names[best.group] if best and best.weight > 0.5 else None)
    return np.array(out, dtype=object)


def main():
    a = args()
    out = os.path.abspath(a["out"])
    os.makedirs(out, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if a.get("fps"):
        bpy.context.scene.render.fps = int(a["fps"])
    rigs = {}
    for spec in a["rig"]:
        tag, path = spec.split("=", 1)
        arm, mesh = import_glb(path, tag)
        if arm.animation_data:
            arm.animation_data.action = None
        for pb in arm.pose.bones:
            pb.matrix_basis.identity()
        bpy.context.view_layer.update()
        E = edges(mesh)
        rest = coords(mesh)
        rigs[tag] = {"arm": arm, "mesh": mesh, "E": E, "own": owner(mesh),
                     "L0": np.linalg.norm(rest[E[:, 0]] - rest[E[:, 1]], axis=1),
                     }
    tags = list(rigs)

    sc = bpy.context.scene
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.render.resolution_x, sc.render.resolution_y = 560, 700
    sh = sc.display.shading
    sh.light, sh.color_type, sh.show_object_outline, sh.show_cavity = "STUDIO", "TEXTURE", True, True
    sc.world = sc.world or bpy.data.worlds.new("w")
    sc.world.color = (0.85, 0.85, 0.85)
    cd = bpy.data.cameras.new("cam")
    cd.type = "ORTHO"
    cam = bpy.data.objects.new("cam", cd)
    sc.collection.objects.link(cam)
    sc.camera = cam

    report = {"tool": "tools/blender/skin_clip_check.py", "rigs": {t: s.split("=", 1)[1] for t, s in
                                                                  zip(tags, a["rig"])}, "clips": {}}
    for cspec in a["clip"]:
        path, _, fr = cspec.partition(":")
        fr, _, rng = fr.partition("@")
        want = [int(f) for f in fr.split(",") if f]
        name = os.path.splitext(os.path.basename(path))[0]
        carm, cmesh = import_glb(path, "clip")
        if cmesh:
            cmesh.hide_render = True
        act = carm.animation_data.action if carm.animation_data else None
        if act is None:
            print("skin_clip_check: %s has no action" % name)
            continue
        ra = rigs[tags[0]]["arm"]
        rest_d = max((carm.matrix_world @ b.head_local - ra.matrix_world @ ra.data.bones[b.name].head_local).length
                     for b in carm.data.bones if b.name in ra.data.bones)
        f0, f1 = (int(round(x)) for x in act.frame_range)
        if rng:
            r0, r1 = (int(x) for x in rng.split("-"))
            f0, f1 = max(f0, r0), min(f1, r1)
        res = {"action": act.name, "frames": [f0, f1], "clip_rest_vs_rig_max_m": round(rest_d, 4),
               "per_rig": {}}
        per_frame = {t: [] for t in tags}
        if a.get("rotations-only") == "1":
            act = game_tracks(act)
        for t in tags:
            rigs[t]["arm"].animation_data_create()
            rigs[t]["arm"].animation_data.action = act
        for f in range(f0, f1 + 1):
            sc.frame_set(f)
            for t in tags:
                r = rigs[t]
                co = coords(r["mesh"])
                ok = r["L0"] > 1e-6
                ratio = np.ones(len(r["E"]))
                ratio[ok] = np.linalg.norm(co[r["E"][ok, 0]] - co[r["E"][ok, 1]], axis=1) / r["L0"][ok]
                bad = np.where((ratio > STRETCH_FAIL) | (ratio < SQUASH_FAIL))[0]
                bones = {}
                for e in bad:
                    for v in r["E"][e]:
                        if r["own"][v]:
                            bones[r["own"][v]] = bones.get(r["own"][v], 0) + 1
                per_frame[t].append({"frame": f, "failing": len(bad), "max": float(ratio.max()),
                                     "over_warn": int((ratio > STRETCH_WARN).sum()), "bones": bones})
        for t in tags:
            pf = per_frame[t]
            worst = max(pf, key=lambda d: (d["failing"], d["max"]))
            res["per_rig"][t] = {
                "max_failing_edges": worst["failing"], "worst_frame": worst["frame"],
                "worst_frame_bones": dict(sorted(worst["bones"].items(), key=lambda kv: -kv[1])[:6]),
                "max_stretch": round(max(d["max"] for d in pf), 3),
                "frames_with_failing_edges": sum(1 for d in pf if d["failing"]),
                "mean_edges_over_1_8x": round(float(np.mean([d["over_warn"] for d in pf])), 1),
            }
        frames = sorted(set(want + [res["per_rig"][t]["worst_frame"] for t in tags]))
        res["stills"] = []
        if a["render"]:
            for f in frames:
                sc.frame_set(f)
                for t in tags:
                    for o in (rigs[u]["mesh"] for u in tags):
                        o.hide_render = o is not rigs[t]["mesh"]
                    co = coords(rigs[t]["mesh"])
                    ra_t = rigs[t]["arm"]
                    hips = ra_t.matrix_world @ ra_t.pose.bones[cc.resolve_bone(ra_t, "Hips")].head
                    lo, hi = co.min(0), co.max(0)
                    for view, rot, off in (("front", (math.radians(90), 0, 0), Vector((0, -10, 0))),
                                           ("side", (math.radians(90), 0, math.radians(90)), Vector((10, 0, 0))),
                                           ("pelvis", (math.radians(75), 0, math.radians(35)),
                                            Vector((5.7, -8.2, 2.6)))):
                        if view == "pelvis":
                            cd.ortho_scale = 0.75
                            cam.location = hips + off
                        else:
                            cd.ortho_scale = float(max(hi[2] - lo[2], hi[0] - lo[0], hi[1] - lo[1])) * 1.25
                            c = Vector(((lo[0] + hi[0]) / 2, (lo[1] + hi[1]) / 2, (lo[2] + hi[2]) / 2))
                            cam.location = c + off
                        cam.rotation_euler = rot
                        p = os.path.join(out, "%s_f%03d_%s_%s.png" % (name, f, t, view))
                        sc.render.filepath = p
                        bpy.ops.render.render(write_still=True)
                        res["stills"].append(p)
            for t in tags:
                rigs[t]["mesh"].hide_render = False
        for t in tags:
            rigs[t]["arm"].animation_data.action = None
        bpy.data.objects.remove(carm, do_unlink=True)
        if cmesh:
            bpy.data.objects.remove(cmesh, do_unlink=True)
        report["clips"][name] = res
        print("skin_clip_check:", name, json.dumps(res["per_rig"]))
    with open(os.path.join(out, "clip_report.json"), "w", encoding="utf-8") as f:
        json.dump(report, f, indent=1)
    return 0


if __name__ == "__main__":
    code = main()
    sys.stdout.flush()
    os._exit(code)
