"""Close-up orthographic renders of a rigged GLB in rest pose, flat unlit, to look at texture seams (YOK-146).

  blender -b --python tools/blender/texture_closeup.py -- --in <rigged.glb> --out <prefix> [--centre 0 0.95] [--scale 0.7]
Writes <prefix>-front.png and <prefix>-back.png (1024 px, white background). --centre is X and height in metres.
"""
import os
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import clip_common as cc  # noqa: E402

a = cc.script_args("texture_closeup")
inp, out = a["--in"][0], os.path.abspath(a["--out"][0])
cx, cz = (float(v) for v in a.get("--centre", ["0", "0.95"]))
scale = float(a.get("--scale", ["0.7"])[0])
bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
bpy.ops.import_scene.gltf(filepath=os.path.abspath(inp))
for o in bpy.data.objects:
    if o.type == "MESH" and not any(m.type == "ARMATURE" for m in o.modifiers):
        o.hide_render = True
for ar in bpy.data.armatures:
    ar.pose_position = "REST"
sc.render.engine = "BLENDER_WORKBENCH"
sc.render.resolution_x = sc.render.resolution_y = 1024
sc.display.render_aa = "OFF"
sc.view_settings.view_transform = "Standard"
w = bpy.data.worlds.new("w")
w.color = (1, 1, 1)
sc.world = w
sh = sc.display.shading
sh.light, sh.color_type = "FLAT", "TEXTURE"
cd = bpy.data.cameras.new("c")
cd.type, cd.ortho_scale = "ORTHO", scale
cam = bpy.data.objects.new("c", cd)
sc.collection.objects.link(cam)
sc.camera = cam
for name, d in (("front", Vector((0, -1, 0))), ("back", Vector((0, 1, 0)))):
    t = Vector((cx, 0, cz))
    cam.location = t + d * 10
    cam.rotation_euler = (t - cam.location).to_track_quat("-Z", "Y").to_euler()
    sc.render.filepath = "%s-%s.png" % (out, name)
    bpy.ops.render.render(write_still=True)
cc.finish(0)
