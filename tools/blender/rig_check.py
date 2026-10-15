"""Headless rig check for the shared skeleton (rules F5, F6, G4; amendment AM5). YOK-110.

Run (Blender 4.1.1):
  /Applications/Blender.app/Contents/MacOS/Blender -b [<rig.blend>] --python tools/blender/rig_check.py -- \
      --rig <rig.glb>              # import a glTF/GLB into an empty scene (omit to check the opened .blend)
      --skeleton <skeleton.md|.json>   # shared skeleton bone list (format below)
      [--bone-map <map.md|.json|NAME>] # rig bone -> shared bone; NAME picks skeleton["bone_maps"][NAME]
      [--out <dir>]                # report.json and stills go here (default: ./rig_check_out)
      [--height <m>]               # expected world height (overrides the skeleton file)
      [--no-render]                # skip the stills
      [--dump-skeleton <path.json>]   # write this rig's bone list in the format below and exit

What it checks, and writes to <out>/report.json:
  skeleton   bone names and parents vs the shared skeleton (after the bone map); extra bones are listed
  rest_pose  T-pose (arms along +-X, legs along -Z), and rest == bind (no mesh offset with the pose cleared)
  scale      world height of the skinned mesh vs skeleton height_m
  up_axis    head above hips, spine within tolerance of world up, facing glTF +Z (Blender -Y)
  origin     feet on the ground (min Z ~ 0) and centred on X/Y
  weights    verts with > max_influences groups, unnormalised sums, unweighted verts,
             cross-side verts: a vert off the midline whose Right* weight beats its Left* weight
             on the left side, or the reverse (FAIL); seam blends past a smaller weight are INFO
  poses      per stress pose: stretched/torn edges (posed/rest edge length) and per-bone volume
             collapse (mean distance of a bone's verts from its bone segment, posed/rest), which is
             how candy-wrapper wrists/forearms and collapsing shoulders/hips show up
  stills     front and side Workbench renders of rest + each stress pose

Bone list formats. The shared skeleton is assets/specs/rigs/skeleton.md (YOK-111), read as-is:
  - bones, parents and skin order: the "| # | Bone | Parent | ..." table (first three columns;
    names in backticks, "(root)" for no parent; "#" is the skin joint index);
  - expected height: the "Source files" table row whose File cell ends in the --rig file name
    (or pass --height);
  - bone maps: each "### Bone map: <rigger> (<fighter>)" heading followed by a
    "| # | Shared bone | Source bone | ..." table. Rows with an empty Source cell are skipped;
    rows naming several bones ("`LeftShoulder`, `LeftArm`") pair them with an equal-length
    comma list in the Source cell. --bone-map NAME picks the heading containing NAME.
  Map by chain position, never by name: Meshy's spine counts DOWN (Spine02 sits on Hips), so a
  name-matched Mixamo/CC spine shows up as wrong_parent here.
Alternatively a .json file, or a Markdown file with one fenced `skeleton-json` block:

  ```skeleton-json
  {"skeleton": "meshy-humanoid-24", "height_m": 1.75, "up_axis": "Y", "rest_pose": "T",
   "bones": [{"name": "Hips", "parent": null}, {"name": "Spine02", "parent": "Hips"}, ...],
   "bone_maps": {"accurig2": {"CC_Base_Hip": "Hips", ...}}}
  ```
height_m is optional (falls back to --height, then the Source files table; without any of them the
scale check is info-only). Skin order is checked only when --rig is a .glb (read from its skin).
A bone map is the same JSON object of rig-name -> shared-name, either inline under bone_maps, a
.json file, or a Markdown file with one fenced `bone-map-json` block. Bones not in the map keep
their own name. --dump-skeleton writes this format from an accepted rig.

F5: the stress poses below are transient check poses, set by script on the pose bones, evaluated
and thrown away. Nothing is keyframed, no action is written, nothing here is a clip, and the rig
file is never saved. Shipped animation comes only from generated, captured or bought clips.

Thresholds are first-pass values (DEFAULTS below); the skeleton file may override any of them
under a "thresholds" object. The report is evidence; the ACCEPT/RETAKE verdict is the Rig
Specialist's, made on the stills.
"""
import bpy
import json
import math
import os
import re
import sys

import numpy as np
from mathutils import Matrix, Vector

DEFAULTS = {
    "max_influences": 4,
    "influence_epsilon": 1e-4,         # a group weight below this does not count as an influence
    "weight_sum_tolerance": 0.01,
    "cross_side_min_x_m": 0.06,        # only verts this far off the midline are judged
    "cross_side_blend_weight": 0.20,   # opposite-side weight reported as a seam blend (info only)
    "rest_arm_max_deg": 20.0,          # arm (upper arm -> hand) vs +-X
    "rest_leg_max_deg": 15.0,          # leg (upper leg -> foot) vs -Z
    "rest_bind_max_offset_m": 0.001,
    "up_max_deg": 15.0,                # hips -> head vs +Z
    "height_tolerance": 0.05,          # fraction of height_m
    "origin_max_ground_m": 0.01,       # |min Z|
    "origin_max_xy_m": 0.08,           # feet centre from the origin (Kitsune sits ~5 cm forward)
    "edge_stretch_warn": 1.8,
    "edge_stretch_fail": 3.0,          # posed/rest edge length = a tear
    "edge_squash_fail": 0.25,
    "edge_fail_max_frac": 0.002,       # pose FAILs above this fraction of edges torn; any = WARN
    "collapse_mean_fail": 0.75,        # posed/rest mean radius of a bone's verts
    "collapse_p5_fail": 0.40,          # 5th percentile of the per-vert ratio
    "collapse_min_verts": 30,
}

# Shared-skeleton names (Meshy humanoid). Axes are Blender world after glTF import:
# +Z up, character faces -Y, character's left is +X. Angles in degrees, applied in order,
# rotating each bone about a world axis through its posed head. "ROLL" rotates about the bone's
# own current direction (head -> mean child head), which is how wrist/forearm twist is tested.
STRESS_POSES = {
    "high_kick": [
        ("Spine02", "X", -12), ("RightUpLeg", "X", -105), ("RightLeg", "X", 10),
        ("LeftUpLeg", "X", 8), ("LeftArm", "Y", -40), ("RightArm", "Y", 40),
    ],
    "deep_crouch": [
        ("LeftUpLeg", "X", -110), ("RightUpLeg", "X", -110),
        ("LeftLeg", "X", 135), ("RightLeg", "X", 135),
        ("LeftFoot", "X", -25), ("RightFoot", "X", -25),
        ("Spine02", "X", 25), ("LeftArm", "Y", 60), ("RightArm", "Y", -60),
    ],
    "arm_raise": [
        ("LeftShoulder", "Y", -20), ("RightShoulder", "Y", 20),
        ("LeftArm", "Y", -70), ("RightArm", "Y", 70),
    ],
    "torso_twist": [
        ("Spine02", "Z", 20), ("Spine01", "Z", 20), ("Spine", "Z", 20),
        ("LeftForeArm", "ROLL", 70), ("LeftHand", "ROLL", 50),
        ("RightForeArm", "ROLL", -70), ("RightHand", "ROLL", -50),
    ],
    "grab": [
        ("Spine02", "X", 15), ("LeftArm", "Z", -80), ("RightArm", "Z", 80),
        ("LeftForeArm", "Z", -30), ("RightForeArm", "Z", 30),
        ("LeftHand", "ROLL", 60), ("RightHand", "ROLL", -60),
    ],
}
AXES = {"X": Vector((1, 0, 0)), "Y": Vector((0, 1, 0)), "Z": Vector((0, 0, 1))}


# ---------------------------------------------------------------- input

def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    a = {"rig": None, "skeleton": None, "bone_map": None, "out": "rig_check_out",
         "render": True, "dump": None, "height": None}
    i = 0
    while i < len(argv):
        k = argv[i]
        if k == "--no-render":
            a["render"] = False
            i += 1
            continue
        if i + 1 >= len(argv):
            sys.exit("rig_check: %s needs a value" % k)
        v = argv[i + 1]
        key = {"--rig": "rig", "--skeleton": "skeleton", "--bone-map": "bone_map",
               "--out": "out", "--dump-skeleton": "dump", "--height": "height"}.get(k)
        if key is None:
            sys.exit("rig_check: unknown argument %s" % k)
        a[key] = v
        i += 2
    if not a["skeleton"] and not a["dump"]:
        sys.exit("rig_check: --skeleton is required (or --dump-skeleton)")
    return a


def load_fenced_json(path, tag):
    text = open(path, encoding="utf-8").read()
    if path.lower().endswith(".json"):
        return json.loads(text)
    blocks = re.findall(r"^```%s[ \t]*\n(.*?)^```" % re.escape(tag), text, re.S | re.M)
    if len(blocks) != 1:
        sys.exit("rig_check: %s needs exactly one ```%s block (found %d)" % (path, tag, len(blocks)))
    return json.loads(blocks[0])


def _cells(line):
    return [c.strip() for c in line.strip().strip("|").split("|")]


def _names(cell):
    return re.findall(r"`([^`]+)`", cell) or ([cell] if cell and cell != "-" else [])


def parse_skeleton_md(path, rig_path):
    """Read skeleton.md's own tables (see header). Returns the same dict as the JSON format."""
    text = open(path, encoding="utf-8").read()
    if re.search(r"^```skeleton-json", text, re.M):
        return load_fenced_json(path, "skeleton-json")
    lines = text.splitlines()
    bones, maps, heights, i = [], {}, {}, 0
    while i < len(lines):
        ln = lines[i]
        m = re.match(r"^#{2,4}\s*Bone map:\s*(.+)$", ln)
        if ln.startswith("|") and i + 1 < len(lines) and re.match(r"^\|[\s:|-]+$", lines[i + 1]):
            head = [c.lower() for c in _cells(ln)]
            rows = []
            i += 2
            while i < len(lines) and lines[i].startswith("|"):
                rows.append(_cells(lines[i]))
                i += 1
            if head[:3] == ["#", "bone", "parent"]:
                for r in rows:
                    n, par = _names(r[1]), _names(r[2])
                    if n and r[0].isdigit():
                        bones.append({"name": n[0], "parent": None if r[2] == "(root)" or not par
                                      else par[0], "index": int(r[0])})
            elif "file" in head and "height" in head:
                fi, hi = head.index("file"), head.index("height")
                for r in rows:
                    f, hm = _names(r[fi]), re.search(r"([\d.]+)\s*m", r[hi])
                    if f and hm:
                        heights[os.path.basename(f[0])] = float(hm.group(1))
            continue
        if m:
            name = m.group(1).strip()
            j = i + 1
            while j < len(lines) and not lines[j].startswith("|"):
                if lines[j].startswith("#"):
                    break
                j += 1
            bm = {}
            if j + 1 < len(lines) and lines[j].startswith("|"):
                head = [c.lower() for c in _cells(lines[j])]
                if "shared bone" in head and "source bone" in head:
                    si, so = head.index("shared bone"), head.index("source bone")
                    k = j + 2
                    while k < len(lines) and lines[k].startswith("|"):
                        r = _cells(lines[k])
                        sh, src = _names(r[si]), [x.strip() for x in r[so].replace("`", "").split(",") if x.strip()]
                        if sh and src:
                            if len(src) != len(sh):
                                sys.exit("rig_check: bone map %r row %r: %d shared vs %d source names"
                                         % (name, r[si], len(sh), len(src)))
                            bm.update(dict(zip(src, sh)))
                        k += 1
            maps[name] = bm
        i += 1
    if not bones:
        sys.exit("rig_check: no '| # | Bone | Parent |' table in %s" % path)
    skel = {"skeleton": os.path.basename(path), "bones": bones, "bone_maps": maps,
            "heights_by_file": heights}
    if rig_path and os.path.basename(rig_path) in heights:
        skel["height_m"] = heights[os.path.basename(rig_path)]
    return skel


def load_skeleton(path, rig_path):
    if path.lower().endswith(".json"):
        return json.loads(open(path, encoding="utf-8").read())
    return parse_skeleton_md(path, rig_path)


def glb_skin_joints(path):
    """Joint names in skin order, read straight from a .glb (None for other inputs)."""
    if not path or not path.lower().endswith(".glb"):
        return None
    import struct
    b = open(path, "rb").read()
    ln, ty = struct.unpack("<II", b[12:20])
    js = json.loads(b[20:20 + ln])
    if not js.get("skins"):
        return None
    return [js["nodes"][j].get("name") for j in js["skins"][0]["joints"]]


def load_bone_map(arg, skel):
    if not arg:
        return {}
    if os.path.exists(arg):
        return load_fenced_json(arg, "bone-map-json")
    maps = (skel or {}).get("bone_maps", {})
    hits = [k for k in maps if arg == k or arg.lower() in k.lower()]
    if len(hits) == 1:
        return maps[hits[0]]
    if arg not in maps:
        sys.exit("rig_check: bone map %r not found as a file or in skeleton bone_maps" % arg)
    return maps[arg]


# ---------------------------------------------------------------- scene

def load_scene(rig_path):
    if rig_path:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.gltf(filepath=os.path.abspath(rig_path))
    arms = [o for o in bpy.data.objects if o.type == "ARMATURE"]
    if len(arms) != 1:
        sys.exit("rig_check: expected one armature, found %d" % len(arms))
    arm = arms[0]
    meshes = [o for o in bpy.data.objects if o.type == "MESH" and o.vertex_groups
              and any(m.type == "ARMATURE" and m.object == arm for m in o.modifiers)]
    # Drop unskinned helpers (the glTF importer adds an Icosphere bone shape).
    for o in list(bpy.data.objects):
        if o.type == "MESH" and o not in meshes:
            bpy.data.objects.remove(o, do_unlink=True)
    if not meshes:
        sys.exit("rig_check: no mesh skinned to %s" % arm.name)
    if arm.animation_data:
        arm.animation_data.action = None  # check the bare rig, not its baked clip
    return arm, meshes


def reset_pose(arm):
    for pb in arm.pose.bones:
        pb.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()


def world_coords(obj, evaluated):
    if evaluated:
        dg = bpy.context.evaluated_depsgraph_get()
        oe = obj.evaluated_get(dg)
        me = oe.to_mesh()
        co = np.empty(len(me.vertices) * 3, np.float64)
        me.vertices.foreach_get("co", co)
        oe.to_mesh_clear()
    else:
        me = obj.data
        co = np.empty(len(me.vertices) * 3, np.float64)
        me.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3)
    m = np.array(obj.matrix_world)
    return co @ m[:3, :3].T + m[:3, 3]


def all_coords(meshes, evaluated):
    return np.concatenate([world_coords(o, evaluated) for o in meshes])


def bone_world_heads(arm):
    mw = arm.matrix_world
    return {pb.name: mw @ pb.head for pb in arm.pose.bones}


# ---------------------------------------------------------------- checks

def angle_deg(v, ref):
    if v.length < 1e-9:
        return 180.0
    return math.degrees(v.angle(ref))


def status(ok, warn=False):
    return "PASS" if ok else ("WARN" if warn else "FAIL")


def check_skeleton(arm, skel, bmap, skin):
    rig = {b.name: (b.parent.name if b.parent else None) for b in arm.data.bones}
    to_shared = lambda n: bmap.get(n, n) if n else None
    mapped = {to_shared(n): to_shared(p) for n, p in rig.items()}
    want = {b["name"]: b.get("parent") for b in skel["bones"]}
    missing = sorted(n for n in want if n not in mapped)
    extra = sorted(n for n in mapped if n not in want)
    wrong_parent = sorted("%s: parent %s, want %s" % (n, mapped[n], p)
                          for n, p in want.items() if n in mapped and mapped[n] != p)
    out = {"skeleton": skel.get("skeleton"), "rig_bones": len(rig), "shared_bones": len(want),
           "missing": missing, "wrong_parent": wrong_parent, "extra_bones": extra}
    ok = not missing and not wrong_parent
    idx = {b["name"]: b["index"] for b in skel["bones"] if "index" in b}
    if skin is not None and idx:
        got = {to_shared(n): i for i, n in enumerate(skin)}
        bad = sorted("%s: joint %s, want %d" % (n, got.get(n), i) for n, i in idx.items()
                     if got.get(n) != i)
        out["skin_order_wrong"] = bad
        ok &= not bad
    else:
        out["skin_order_wrong"] = None  # not checked (no .glb or no index column)
    out["status"] = status(ok)
    return out


def check_rest(arm, meshes, H, T):
    out = {}
    h = bone_world_heads(arm)
    pairs = {"left_arm": ("LeftArm", "LeftHand", Vector((1, 0, 0)), T["rest_arm_max_deg"]),
             "right_arm": ("RightArm", "RightHand", Vector((-1, 0, 0)), T["rest_arm_max_deg"]),
             "left_leg": ("LeftUpLeg", "LeftFoot", Vector((0, 0, -1)), T["rest_leg_max_deg"]),
             "right_leg": ("RightUpLeg", "RightFoot", Vector((0, 0, -1)), T["rest_leg_max_deg"])}
    ok = True
    for k, (a, b, ref, lim) in pairs.items():
        if H(a) in h and H(b) in h:
            d = angle_deg(h[H(b)] - h[H(a)], ref)
            out[k + "_deg"] = round(d, 2)
            ok &= d <= lim
        else:
            out[k + "_deg"] = None
            ok = False
    base = all_coords(meshes, False)
    posed = all_coords(meshes, True)
    off = float(np.linalg.norm(posed - base, axis=1).max())
    out["rest_vs_bind_max_offset_m"] = round(off, 5)
    ok &= off <= T["rest_bind_max_offset_m"]
    out["status"] = status(ok)
    return out, posed


def check_frame(arm, rest_co, H, skel, T):
    h = bone_world_heads(arm)
    lo, hi = rest_co.min(0), rest_co.max(0)
    height = float(hi[2] - lo[2])
    want = skel.get("height_m")
    scale = {"height_m": round(height, 4), "want_height_m": want,
             "armature_object_scale": [round(s, 5) for s in arm.scale]}
    if want:
        scale["status"] = status(abs(height - want) <= T["height_tolerance"] * want)
    else:
        scale["status"] = "INFO"
    up = {}
    if all(H(n) in h for n in ("Hips", "Head")):
        spine = h[H("Head")] - h[H("Hips")]
        up["hips_to_head_vs_up_deg"] = round(angle_deg(spine, Vector((0, 0, 1))), 2)
        # Facing: toes point forward of the ankles. glTF +Z forward == Blender -Y.
        fwd = Vector((0, 0, 0))
        for side in ("Left", "Right"):
            if H(side + "Foot") in h and H(side + "ToeBase") in h:
                fwd += h[H(side + "ToeBase")] - h[H(side + "Foot")]
        fwd.z = 0
        up["facing"] = "gltf+Z" if fwd.y < 0 else "gltf-Z"
        up["status"] = status(up["hips_to_head_vs_up_deg"] <= T["up_max_deg"]
                              and up["facing"] == "gltf+Z")
    else:
        up["status"] = "FAIL"
    feet = [h[H(n)] for n in ("LeftFoot", "RightFoot") if H(n) in h]
    fc = sum(feet, Vector((0, 0, 0))) / len(feet) if feet else Vector((0, 0, 0))
    origin = {"min_z_m": round(float(lo[2]), 4),
              "feet_centre_xy_m": [round(fc.x, 4), round(fc.y, 4)]}
    origin["status"] = status(abs(lo[2]) <= T["origin_max_ground_m"]
                              and math.hypot(fc.x, fc.y) <= T["origin_max_xy_m"])
    return scale, up, origin


def weight_table(obj):
    """Dense (verts x groups) weight matrix for one mesh."""
    W = np.zeros((len(obj.data.vertices), len(obj.vertex_groups)), np.float64)
    for v in obj.data.vertices:
        for g in v.groups:
            W[v.index, g.group] = g.weight
    return W, [g.name for g in obj.vertex_groups]


def check_weights(meshes, rest_co, bmap, T):
    out = {"meshes": {}}
    ok_all = True
    start = 0
    for o in meshes:
        W, names = weight_table(o)
        shared = [bmap.get(n, n) for n in names]
        co = rest_co[start:start + len(W)]
        start += len(W)
        infl = (W > T["influence_epsilon"]).sum(1)
        sums = W.sum(1)
        over = np.where(infl > T["max_influences"])[0]
        unnorm = np.where((sums > 0) & (np.abs(sums - 1) > T["weight_sum_tolerance"]))[0]
        zero = np.where(sums <= 0)[0]
        left_cols = [i for i, n in enumerate(shared) if n.startswith("Left")]
        right_cols = [i for i, n in enumerate(shared) if n.startswith("Right")]
        x = co[:, 0]
        m = T["cross_side_min_x_m"]
        wl = W[:, left_cols].sum(1) if left_cols else np.zeros(len(W))
        wr = W[:, right_cols].sum(1) if right_cols else np.zeros(len(W))
        cross = np.where(((x > m) & (wr > wl)) | ((x < -m) & (wl > wr)))[0]
        bw = T["cross_side_blend_weight"]
        blend = np.where(((x > m) & (wr > bw)) | ((x < -m) & (wl > bw)))[0]
        ok = not len(over) and not len(zero) and not len(cross)
        ok_all &= ok
        out["meshes"][o.name] = {
            "verts": len(W), "max_influences_found": int(infl.max()) if len(W) else 0,
            "over_max_influences": len(over), "unnormalised": len(unnorm), "unweighted": len(zero),
            "cross_side": len(cross), "cross_side_blend_info": len(blend),
            "cross_side_sample": [{"vert": int(i), "x_m": round(float(x[i]), 3),
                                   "left_w": round(float(wl[i]), 3), "right_w": round(float(wr[i]), 3)}
                                  for i in cross[:10]],
            "over_max_sample": [int(i) for i in over[:10]],
            "unnormalised_sample": [int(i) for i in unnorm[:10]],
        }
    out["status"] = status(ok_all, warn=False)
    return out


# ---------------------------------------------------------------- poses

def bone_segments(arm):
    """World (head, end) per bone; end = mean of children heads, None for leaves."""
    h = bone_world_heads(arm)
    seg = {}
    for b in arm.data.bones:
        kids = [h[c.name] for c in b.children]
        seg[b.name] = (h[b.name], sum(kids, Vector((0, 0, 0))) / len(kids) if kids else None)
    return seg


def radial(co, a, b):
    a = np.array(a)
    if b is None:
        return np.linalg.norm(co - a, axis=1)
    b = np.array(b)
    d = b - a
    L = float(d @ d)
    if L < 1e-12:
        return np.linalg.norm(co - a, axis=1)
    t = np.clip((co - a) @ d / L, 0, 1)
    return np.linalg.norm(co - (a + t[:, None] * d), axis=1)


def apply_pose(arm, meshes, pose, H):
    reset_pose(arm)
    skipped = []
    for bone, axis, deg in pose:
        pb = arm.pose.bones.get(H(bone))
        if pb is None:
            skipped.append(bone)
            continue
        hd = pb.head.copy()  # armature space; axes match world (uniform object scale, no rotation)
        if axis == "ROLL":
            kids = [c.head for c in pb.children]
            ax = (sum(kids, Vector((0, 0, 0))) / len(kids) - hd) if kids else (pb.tail - hd)
            ax.normalize()
        else:
            ax = AXES[axis]
        R = Matrix.Rotation(math.radians(deg), 4, ax)
        pb.matrix = Matrix.Translation(hd) @ R @ Matrix.Translation(-hd) @ pb.matrix
        bpy.context.view_layer.update()
    # Put the lowest point back on the ground, so crouch and kick read as standing poses.
    root = [pb for pb in arm.pose.bones if pb.parent is None][0]
    co = all_coords(meshes, True)
    dz = float(co[:, 2].min()) / arm.matrix_world.to_scale().z
    root.matrix = Matrix.Translation((0, 0, -dz)) @ root.matrix
    bpy.context.view_layer.update()
    return skipped


def edges_of(meshes):
    out, start = [], 0
    for o in meshes:
        e = np.empty(len(o.data.edges) * 2, np.int64)
        o.data.edges.foreach_get("vertices", e)
        out.append(e.reshape(-1, 2) + start)
        start += len(o.data.vertices)
    return np.concatenate(out)


def owners(meshes, arm):
    """Per vertex: dominant bone name (weight > 0.5) or None."""
    res = []
    for o in meshes:
        W, names = weight_table(o)
        idx = W.argmax(1)
        res += [names[i] if W[v, i] > 0.5 else None for v, i in enumerate(idx)]
    return np.array(res, dtype=object)


def check_poses(arm, meshes, rest_co, H, T, out_dir, render):
    E = edges_of(meshes)
    rest_len = np.linalg.norm(rest_co[E[:, 0]] - rest_co[E[:, 1]], axis=1)
    good = rest_len > 1e-6
    own = owners(meshes, arm)
    rest_seg = bone_segments(arm)
    res = {}
    for name, pose in STRESS_POSES.items():
        skipped = apply_pose(arm, meshes, pose, H)
        co = all_coords(meshes, True)
        seg = bone_segments(arm)
        r = np.ones(len(E))
        r[good] = np.linalg.norm(co[E[good, 0]] - co[E[good, 1]], axis=1) / rest_len[good]
        bad = np.where((r > T["edge_stretch_fail"]) | (r < T["edge_squash_fail"]))[0]
        bad_bones = {}
        for e in bad:
            for v in E[e]:
                if own[v]:
                    bad_bones[own[v]] = bad_bones.get(own[v], 0) + 1
        collapse = {}
        for b in arm.data.bones:
            sel = np.where(own == b.name)[0]
            if len(sel) < T["collapse_min_verts"]:
                continue
            r0 = radial(rest_co[sel], *rest_seg[b.name])
            r1 = radial(co[sel], *seg[b.name])
            keep = r0 > 1e-4
            if not keep.any():
                continue
            ratio = r1[keep] / r0[keep]
            mean = float(r1[keep].mean() / r0[keep].mean())
            p5 = float(np.percentile(ratio, 5))
            if mean < T["collapse_mean_fail"] or p5 < T["collapse_p5_fail"]:
                collapse[b.name] = {"mean_ratio": round(mean, 3), "p5_ratio": round(p5, 3)}
        stills = render_views(name, out_dir) if render else []
        res[name] = {
            "status": ("FAIL" if (len(bad) > T["edge_fail_max_frac"] * len(E) or collapse or skipped)
                       else "WARN" if len(bad) else "PASS"),
            "edge_ratio_max": round(float(r.max()), 3), "edge_ratio_min": round(float(r.min()), 3),
            "edges_over_warn": int((r > T["edge_stretch_warn"]).sum()),
            "edges_total": len(E), "edges_failing": len(bad),
            "failing_edges_by_bone": dict(sorted(bad_bones.items(), key=lambda kv: -kv[1])[:8]),
            "collapsed_bones": collapse, "missing_pose_bones": skipped, "stills": stills,
        }
    reset_pose(arm)
    return res


# ---------------------------------------------------------------- render

def setup_render(rest_co):
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.render.resolution_x = sc.render.resolution_y = 640
    sc.render.film_transparent = False
    sh = sc.display.shading
    sh.light = "STUDIO"
    sh.color_type = "TEXTURE"
    sh.show_object_outline = True
    sh.show_cavity = True
    if sc.world is None:
        sc.world = bpy.data.worlds.new("rig_check")
    sc.world.color = (0.85, 0.85, 0.85)
    h = float(rest_co[:, 2].max() - rest_co[:, 2].min())
    cam_data = bpy.data.cameras.new("rig_check_cam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = h * 1.5
    cam = bpy.data.objects.new("rig_check_cam", cam_data)
    sc.collection.objects.link(cam)
    sc.camera = cam
    return cam, h


VIEWS = {"front": ((0, -10, 0), (math.radians(90), 0, 0)),
         "side": ((10, 0, 0), (math.radians(90), 0, math.radians(90)))}
_CAM = {}


def render_views(tag, out_dir):
    cam, h = _CAM["cam"], _CAM["h"]
    paths = []
    for view, (loc, rot) in VIEWS.items():
        cam.location = (loc[0], loc[1], h * 0.6)
        cam.rotation_euler = rot
        p = os.path.join(out_dir, "%s_%s.png" % (tag, view))
        bpy.context.scene.render.filepath = p
        bpy.ops.render.render(write_still=True)
        paths.append(p)
    return paths


# ---------------------------------------------------------------- main

def dump_skeleton(arm, meshes, path, bmap):
    co = all_coords(meshes, True)
    to_shared = lambda n: bmap.get(n, n) if n else None
    data = {"skeleton": "dumped-from-%s" % arm.name,
            "height_m": round(float(co[:, 2].max() - co[:, 2].min()), 3),
            "up_axis": "Y", "rest_pose": "T",
            "bones": [{"name": to_shared(b.name), "parent": to_shared(b.parent.name if b.parent else None)}
                      for b in arm.data.bones]}
    with open(path, "w", encoding="utf-8") as f:
        json.dump(data, f, indent=1)
    print("rig_check: wrote skeleton bone list", path)


def main():
    a = parse_args()
    skel = load_skeleton(a["skeleton"], a["rig"]) if a["skeleton"] else None
    if skel is not None and a["height"]:
        skel["height_m"] = float(a["height"])
    bmap = load_bone_map(a["bone_map"], skel)
    arm, meshes = load_scene(a["rig"])
    reset_pose(arm)
    if a["dump"]:
        dump_skeleton(arm, meshes, a["dump"], bmap)
        return 0
    T = dict(DEFAULTS, **skel.get("thresholds", {}))
    inv = {v: k for k, v in bmap.items()}
    H = lambda shared: inv.get(shared, shared)  # shared name -> rig bone name
    out_dir = os.path.abspath(a["out"])
    os.makedirs(out_dir, exist_ok=True)

    report = {"tool": "tools/blender/rig_check.py", "blender": bpy.app.version_string,
              "rig": a["rig"] or bpy.data.filepath, "skeleton_file": a["skeleton"],
              "bone_map": a["bone_map"], "thresholds": T,
              "armature": arm.name, "meshes": [o.name for o in meshes]}
    report["skeleton"] = check_skeleton(arm, skel, bmap, glb_skin_joints(a["rig"]))
    report["rest_pose"], rest_co = check_rest(arm, meshes, H, T)
    report["scale"], report["up_axis"], report["origin"] = check_frame(arm, rest_co, H, skel, T)
    report["weights"] = check_weights(meshes, rest_co, bmap, T)
    if a["render"]:
        _CAM["cam"], _CAM["h"] = setup_render(rest_co)
        report["rest_stills"] = render_views("rest", out_dir)
    report["poses"] = check_poses(arm, meshes, rest_co, H, T, out_dir, a["render"])

    keys = ["skeleton", "rest_pose", "scale", "up_axis", "origin", "weights"]
    states = [report[k]["status"] for k in keys] + [p["status"] for p in report["poses"].values()]
    report["summary"] = {k: report[k]["status"] for k in keys}
    report["summary"].update({"pose:" + n: p["status"] for n, p in report["poses"].items()})
    report["overall"] = "FAIL" if "FAIL" in states else ("WARN" if "WARN" in states else "PASS")
    rp = os.path.join(out_dir, "report.json")
    with open(rp, "w", encoding="utf-8") as f:
        json.dump(report, f, indent=1)
    print("rig_check:", report["overall"], json.dumps(report["summary"]))
    print("rig_check: report", rp)
    return 1 if report["overall"] == "FAIL" else 0


if __name__ == "__main__":
    code = main()
    sys.stdout.flush()
    os._exit(code)  # Blender ignores a script's exit status; make the gate scriptable
