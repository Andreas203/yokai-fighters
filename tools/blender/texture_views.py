"""Render the four orthographic paint-over views of a rigged Tripo fighter, by script (YOK-146, texture pass phase A).

Why. The Tripo base colour has shading and cloth folds painted in. The plan is to repaint it as flat cel art:
Blender renders the mesh from four orthographic views, Astra repaints each render inside the exact silhouette,
and phase B projects the paintings back onto the mesh's existing UVs. This script is the first step. It runs
no generation job and spends no credits.

Output, per view V in front, left, right, back (into --out-dir, default assets/specs/texture/ryo-tripo/):
  ryo-albedo-V.png  unlit albedo (the base-colour texture, no lighting) on pure white: the paint-over base
  ryo-mask-V.png    silhouette in pure black on pure white (no anti-aliasing, so the edge is exact)
  views.json        camera parameters shared by all four views, for the projection back (phase B)

Conventions. The glTF faces +Z with the character's left on +X; Blender's importer turns that into
facing -Y, left = +X, up = +Z. "left" shows the character's left side, so he faces image-left; "right"
faces image-right; "back" shows his back. Same orthographic scale, camera distance and square resolution
for all four, camera centred on the same world point, so one pixel is the same size in the mesh in every
view and the camera JSON is enough to project back. Rest T-pose, no action applied.

Run (Blender 4.1, headless):
  blender -b --python tools/blender/texture_views.py -- [--in <rigged.glb>] [--out-dir <dir>] [--res 2048] [--prefix ryo]
"""
import math
import os
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import clip_common as cc  # noqa: E402

DEFAULT_IN = "game/assets/generated/characters/ryo/ryo-tripo-rigged.glb"
DEFAULT_OUT = "assets/specs/texture/ryo-tripo"
PAD = 1.08  # margin around the largest extent
DIST = 10.0  # metres; ortho, so it only has to clear the mesh

# view -> (camera position direction from the target in Blender world, what the image shows)
VIEWS = {
    "front": (Vector((0, -1, 0)), "front, character faces the camera, his left is image-right"),
    "left": (Vector((1, 0, 0)), "the character's left side, he faces image-left"),
    "right": (Vector((-1, 0, 0)), "the character's right side, he faces image-right"),
    "back": (Vector((0, 1, 0)), "back, his left is image-left"),
}


def arg(tool_args, flag, default):
    v = tool_args.get(flag)
    return v[0] if v else default


def mesh_bounds(meshes):
    pts = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return lo, hi


def set_world_white(scene):
    w = bpy.data.worlds.new("white")
    w.use_nodes = False
    w.color = (1, 1, 1)
    scene.world = w


def setup_scene(scene, res):
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.render.resolution_x = scene.render.resolution_y = res
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = False
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGB"
    scene.render.image_settings.color_depth = "8"
    scene.display.render_aa = "OFF"  # hard edges: the mask must be exact and the paint-over keeps the same edge
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"
    scene.view_settings.exposure = 0.0
    scene.view_settings.gamma = 1.0
    scene.display_settings.display_device = "sRGB"
    set_world_white(scene)
    sh = scene.display.shading
    sh.light = "FLAT"  # no lighting at all
    sh.show_shadows = False
    sh.show_cavity = False
    sh.show_object_outline = False
    sh.show_specular_highlight = False
    sh.show_xray = False
    sh.background_type = "WORLD"


def main():
    args = cc.script_args("texture_views")
    inp = arg(args, "--in", DEFAULT_IN)
    out_dir = arg(args, "--out-dir", DEFAULT_OUT)
    res = int(arg(args, "--res", 2048))
    prefix = arg(args, "--prefix", "ryo")
    os.makedirs(out_dir, exist_ok=True)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    bpy.ops.import_scene.gltf(filepath=inp)
    # The glTF importer adds a bone-shape "Icosphere" helper that is not in the file; render only the skinned mesh(es).
    meshes = [o for o in bpy.data.objects if o.type == "MESH" and any(m.type == "ARMATURE" for m in o.modifiers)]
    for o in bpy.data.objects:
        if o.type == "MESH" and o not in meshes:
            o.hide_render = True
            o.hide_viewport = True
    if not meshes:
        sys.exit("texture_views: no mesh in %s" % inp)
    for a in bpy.data.armatures:
        a.pose_position = "REST"  # rest T-pose
    for o in bpy.data.objects:
        if o.type == "ARMATURE":
            o.hide_render = True
    bpy.context.view_layer.update()
    lo, hi = mesh_bounds(meshes)
    centre = (lo + hi) / 2
    size = hi - lo
    ortho = max(size.x, size.y, size.z) * PAD

    setup_scene(scene, res)
    cam_data = bpy.data.cameras.new("texcam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = ortho
    cam_data.clip_start = 0.1
    cam_data.clip_end = 100.0
    cam_data.sensor_fit = "AUTO"
    cam = bpy.data.objects.new("texcam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam

    # materials: albedo = the base-colour image shown flat; mask = pure black. The glTF factor (0.8) is ignored on
    # purpose: the Workbench "texture" colour is the raw image, which is what the UV projection will replace.
    shading = scene.display.shading
    log = {"tool": "texture_views.py", "input": inp, "input_sha256": cc.sha256(inp), "resolution": [res, res],
           "ortho_scale": ortho, "pad": PAD, "distance": DIST, "units": "metres, Blender world (Z up, character faces -Y, left = +X)",
           "target": list(centre), "bounds_min": list(lo), "bounds_max": list(hi), "mesh_objects": [o.name for o in meshes],
           "pixel_size_m": ortho / res, "views": {}}
    for name, (direction, note) in VIEWS.items():
        cam.location = centre + direction * DIST
        # look at the target with world +Z up
        cam.rotation_euler = (centre - cam.location).to_track_quat("-Z", "Y").to_euler()
        bpy.context.view_layer.update()
        mw = cam.matrix_world
        for kind in ("albedo", "mask"):
            if kind == "albedo":
                shading.color_type = "TEXTURE"
            else:
                shading.color_type = "SINGLE"
                shading.single_color = (0.0, 0.0, 0.0)
            path = os.path.abspath(os.path.join(out_dir, "%s-%s-%s.png" % (prefix, kind, name)))
            scene.render.filepath = path
            bpy.ops.render.render(write_still=True)
            print("texture_views: wrote", path)
        log["views"][name] = {"note": note, "location": list(cam.location), "direction_from_target": list(direction),
                              "rotation_euler_xyz": list(cam.rotation_euler),
                              "matrix_world": [list(r) for r in mw],
                              "albedo": "%s-albedo-%s.png" % (prefix, name), "mask": "%s-mask-%s.png" % (prefix, name),
                              "projection": "orthographic; pixel (px, py) from top-left maps to camera-space x = (px+0.5)/res*ortho_scale - ortho_scale/2, "
                                            "y = ortho_scale/2 - (py+0.5)/res*ortho_scale, looking down camera -Z"}
    cc.write_json(os.path.join(out_dir, "views.json"), log)
    cc.finish(0)


main()
