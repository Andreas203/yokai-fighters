"""Strip a rigged GLB to its bare standing mesh (no armature, no skin) for a fresh Tripo API rig (free, scripted).
Blender -b --python tools/blender/strip_for_rig.py -- --in <rigged.glb> --out <mesh.glb> [--rot-x-deg 0]
"""
import math, os, sys
import bpy
a = sys.argv[sys.argv.index("--") + 1:]
arg = lambda k, d=None: a[a.index(k) + 1] if k in a else d
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=os.path.abspath(arg("--in")))
meshes = [o for o in bpy.data.objects if o.type == "MESH"]
for o in meshes:
    for m in list(o.modifiers):
        if m.type == "ARMATURE":
            bpy.context.view_layer.objects.active = o
            bpy.ops.object.modifier_apply(modifier=m.name)  # bake the rest pose
    o.vertex_groups.clear()
    o.parent = None
for o in [o for o in bpy.data.objects if o.type == "ARMATURE"]:
    bpy.data.objects.remove(o)
bpy.ops.object.select_all(action="DESELECT")
for o in meshes:
    o.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
if len(meshes) > 1:
    bpy.ops.object.join()
ob = bpy.context.view_layer.objects.active
ob.rotation_euler[0] += math.radians(float(arg("--rot-x-deg", 0)))
bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
bpy.ops.export_scene.gltf(filepath=os.path.abspath(arg("--out")), export_format="GLB", export_animations=False, export_skins=False)
print("strip_for_rig: wrote", arg("--out"), "dims", tuple(ob.dimensions))
