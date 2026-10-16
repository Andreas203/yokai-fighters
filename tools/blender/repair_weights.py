"""Scripted skin-weight repair for the pelvis of a Meshy-rigged fighter (rule F5; YOK-116).

Rebuilds a repaired rig from the raw rigged GLB. Only the skin weights (JOINTS_0 / WEIGHTS_0)
change: the script patches those two buffer views in place in a byte copy of the input GLB, so
mesh, UVs, normals, texture, skeleton, skin order, inverse bind matrices and the bundled clip stay
byte-identical. The input file is never written. No pose is keyed and no action is touched: this
is a weight edit, not animation.

Run (Blender 4.x, headless):
  /Applications/Blender.app/Contents/MacOS/Blender -b --python tools/blender/repair_weights.py -- \
      --in  game/assets/generated/characters/ryo/ryo-rigged.glb \
      --out <dir>/ryo-rigged-repaired.glb \
      [--log <dir>/repair_log.json] [--set key=value ...]   # override a PARAMS entry below

Why (assets/specs/ryo-regen.md): the take-2 T-pose has the legs ~4 deg apart, so the auto-skin
splits crotch, seat and lower-belly verts between Hips and both UpLeg bones. In a deep crouch
the belly/sash verts above the hip joint still carry 0.2-0.3 UpLeg weight and get dragged into
the body (Hips volume ratio 0.686).

Steps, all inside the pelvis band (Leg head z + region_bottom_m up to Hips head z +
region_top_m; Blender world axes: +Z up, character faces -Y, character's left is +X). Verts
outside the band keep their exact original joint/weight slots.
  0. weld    - verts at the same position (UV seams) are joined for smoothing and end with
               identical weights, so a seam cannot open.
  1. smooth  - Laplacian smoothing of the Hips / LeftUpLeg / RightUpLeg columns (their per-vert
               total is kept, other bones untouched): takes the blockiness out of the auto-skin.
  2. side    - inner-thigh separation: a vert left of the midline gives its RightUpLeg /
               RightLeg weight to LeftUpLeg / LeftLeg, and the reverse. Inside +-side_band_m
               the pair total is split by a smoothstep in x, so the fused crotch seam blends
               over ~10 cm instead of tearing when one leg kicks.
  3. falloff - UpLeg weight fades out above the hip joint (front y < 0: upleg_z + front_lo to
               + front_hi; back/seat: back_lo to back_hi); the removed weight goes to Hips, so
               the belly, sash and coat panel stop being dragged by the thighs.
  4. props   - OFF by default (prop_bind=0). When on, the charm, pouch and rope sash (painted parts of the one body mesh, found by
               base colour in the band: light paper/pouch, persimmon rope; patches of at least
               prop_min_verts) are set rigid to Hips 1.0 and blended into the body over
               prop_rings edge rings, as the rig plan bakes them (YOK-110 cloth pass found them
               weighted to the thighs). On Ryo every variant tore the deep crouch (87-198
               failing edges): the props are painted onto the body surface across the hip
               crease, so a rigid patch there must tear or clip. Kept for a separate-mesh take.
  5. tidy    - drop influences under min_weight, keep the 4 largest, normalise to 1.
Tried and dropped (YOK-116 runs): a narrow side band (1.2 cm) tore the crotch in the kick (10x
stretch); giving the crotch midline to Hips tore it in the crouch; smoothing again after the
falloff re-spread the UpLeg weight and tore the hip crease.
Every step is logged (verts changed, weight moved per bone) to --log.

Per-fighter settings (--fighter, default ryo; YOK-117): FIGHTERS below. "ryo" runs exactly the
steps above (output byte-identical to YOK-116). "kitsune" adds torso steps T1-T5 for the back
hair, coat back and sash bell; see the comment on FIGHTERS["kitsune"]. "oni" and "kappa" (YOK-112,
YOK-102) switch the pelvis steps off and use the optional steps T6 (trunk by height), C (crotch),
K (knee split, shin clean) and S (band smoothing, unused); every new step defaults to off, so
"ryo" and "kitsune" give the same bytes as before. Run:
  ... repair_weights.py -- --fighter kitsune \
      --in game/assets/generated/characters/kitsune/kitsune-rigged.glb --out <dir>/kitsune-rigged-repaired.glb
"""
import bpy
import hashlib
import json
import os
import struct
import sys

import numpy as np

PARAMS = {
    "region_bottom_m": 0.15,    # pelvis band: from Leg (knee) z + this ...
    "region_top_m": 0.14,       # ... to Hips head z + this
    "smooth_pre_iters": 20,
    "smooth_lambda": 0.5,
    "side_band_m": 0.05,        # half-width of the left/right blend at the midline
    "falloff_front_lo_m": 0.00,  # UpLeg weight fades from upleg_z + lo ...
    "falloff_front_hi_m": 0.08,  # ... to zero at upleg_z + hi (front, y < 0)
    "falloff_back_lo_m": 0.03,
    "falloff_back_hi_m": 0.12,
    "prop_bind": 0,              # 1 = run step 4 (off: it fails the crouch on Ryo, see note)
    "prop_light_min": 0.55,      # charm / pouch: base colour brighter than this ...
    "prop_light_max_chroma": 0.25,  # ... and nearly grey
    "prop_z_lo_m": -0.03,        # props searched from upleg_z + this up to the band top
    "prop_min_verts": 20,
    "prop_rings": 6,
    "min_weight": 0.01,
    "max_influences": 4,
    "torso_fix": 0,             # 1 = run the torso steps T1-T5 (off for Ryo)
    "side_on": 1,               # 0 = skip step 2 (side separation)       (YOK-112)
    "falloff_on": 1,            # 0 = skip step 3 (UpLeg falloff to Hips) (YOK-112)
    "legstrip_all": 0,          # 1 = step T2 strips leg weight from the whole torso above
                                #     hairleg_z0..z1, not only the hair layer (YOK-102 Kappa)
    "pure_on": 0,               # 1 = run step T6 (back zone follows the trunk alone)
    "pure_y0_m": 0.08, "pure_y1_m": 0.14,      # T6: behind the Hips head by this much ...
    "pure_x0_m": 0.40, "pure_x1_m": 0.46,      # ... within this half-width ...
    "pure_zbot0_m": 0.70, "pure_zbot1_m": 0.78,  # ... above this ...
    "pure_ztop_band_m": 0.05,   # T6: half-width of the blend at ztop
    "pure_ztop_m": 1.25, "pure_xflat_m": 0.20, "pure_zslope": 1.8,  # ... below ztop, lowered
                                #     by zslope per metre of |x| past xflat (keeps the armpits)
    "band_smooth_iters": 0,     # > 0 = run step S (final smoothing of every bone column) ...
    "band_z0_m": 0.0, "band_z1_m": 0.0,  # ... for verts between these heights (YOK-102 Kappa)
    "band_x_m": 9.0,            # ... and within this half-width (keeps the arms out)
    "knee_iters": 0,            # > 0 = run step K (knee smoothing), this many iterations
    "knee_band_m": 0.10,        # step K: verts within this height of the knee heads
    "knee_radius_m": 0.0,       # step K: > 0 = select verts within this distance of the knee head
                                #     (not a height band), fading the change out over the outer
                                #     knee_fade_m; keeps the crotch out when knees and crotch share
                                #     a height (Kappa)
    "knee_fade_m": 0.06,
    "shin_clean_m": 0.0,        # step K: > 0 = on each leg's side, verts more than this below the
                                #     knee head give their UpLeg weight to Leg, blended over
    "shin_blend_m": 0.04,       #     shin_blend_m above that line
    "crotch_x_m": 0.0,          # step C: > 0 = verts within this |x| of the midline and below the
                                #     hip joint give their Leg weight to the same side's UpLeg
    "knee_split": 0,            # step K mode: 1 = after smoothing, split the [UpLeg, Leg] pair by
                                #     height across the knee (UpLeg above, Leg below, smoothstep
                                #     over +-knee_band_m); 0 = smoothing only
}

# Per-fighter settings. "ryo" is PARAMS as it stands (YOK-116; its output must stay
# byte-identical). Others start from PARAMS and add or override keys.
FIGHTERS = {
    "ryo": {},
    # YOK-117. Kitsune's Meshy auto-skin hangs the long back hair (a sheet from the head to the
    # waist, 0.15-0.21 m behind the spine) on LeftArm AND RightArm, and the hair tips and coat
    # back also on Hips + both UpLegs: 886 arm-and-leg bleed verts, and the waist tears in every
    # stress pose. Torso steps (Blender world axes as above; y is measured from the Hips head,
    # + is behind the character):
    #   T1 arm strip - inside the torso column (|x| < trunk_x0..x1, below trunk_z0..z1) and the
    #                  hair layer behind the back (y > hair_y0..y1: wider, |x| < hair_x0..x1,
    #                  up to hair_z0..z1) arm weight goes to the spine chain, split by height
    #                  between the Hips/Spine02/Spine01/Spine/neck heads. Smoothstep edges, so
    #                  the hair-to-sleeve seam blends instead of tearing. The sleeves (|x| beyond
    #                  hair_x1, or in front of the hair layer above trunk_z1) keep their weights.
    #   T2 leg strip - leg weight in the hair layer above the seat (hairleg_z0..z1) -> trunk.
    #   T3 smooth    - Laplacian smoothing of [arm bones, trunk total] over the T1/T2 verts
    #                  (per-vert total kept); the trunk total is split back as the vert's own
    #                  original trunk weights first and the height split for any gain.
    #   then steps 1-3 (pelvis), then
    #   T4 bleed     - a vert still holding both arm and leg weight loses the smaller group to
    #                  the trunk (height split).
    #   T5 rigid     - a small prop inside a box (rigid_*) is bound to Hips alone.
    "kitsune": {
        "torso_fix": 1,
        "hair_y0_m": 0.05, "hair_y1_m": 0.09,
        "trunk_x0_m": 0.12, "trunk_x1_m": 0.18, "trunk_z0_m": 1.00, "trunk_z1_m": 1.10,
        "hair_x0_m": 0.10, "hair_x1_m": 0.40, "hair_z0_m": 1.24, "hair_z1_m": 1.34,
        "hairleg_z0_m": 0.86, "hairleg_z1_m": 0.93,
        "torso_smooth_iters": 60,
        # bell on the sash front: |x| < 4.5 cm, more than 11 cm in front of the Hips head,
        # z 0.87-0.97 m -> Hips only
        "rigid_x_m": 0.045, "rigid_y_max_m": -0.11, "rigid_z0_m": 0.87, "rigid_z1_m": 0.97,
    },
    # YOK-112. Oni, on the 2.2 m rig (scale_rig.py x1.1 first; heights below are in that frame:
    # hip joints 1.128 m, knees 0.678 m, arm heads 1.755 m at x 0.295). Ryo's pelvis rules made
    # him worse (the baggy trousers are cloth, exempt by cloth/oni.md, and the seat needs a much
    # wider blend), so they are off. Instead, the whole torso between the seat and the armpits
    # follows the trunk by height (T6 over the full depth), and the leg weight fades out over
    # a 0.38 m band from just below the hip joint (T2 over the whole torso). The armpit line
    # drops 1.6 m per metre of |x| past 0.12 m, blended over +-0.16 m. Removes the 189 mane and
    # upper-back bleed verts and the seat sheet in the crouch.
    "oni": {
        "torso_fix": 1, "side_on": 0, "falloff_on": 0, "smooth_pre_iters": 0,
        # pelvis band (bounds the verts the tidy step may touch; its rules are off)
        "region_bottom_m": 0.18, "region_top_m": 0.17, "side_band_m": 0.06,
        "falloff_front_hi_m": 0.10, "falloff_back_lo_m": 0.04, "falloff_back_hi_m": 0.15,
        # T1 arm strip and T3 off (zero-width strip); T2 leg strip over the whole torso
        "trunk_x0_m": 0.0, "trunk_x1_m": 0.001, "hair_x0_m": 0.0, "hair_x1_m": 0.001,
        "torso_smooth_iters": 0, "legstrip_all": 1, "hairleg_z0_m": 1.04, "hairleg_z1_m": 1.42,
        # T1 limits as tuned (with the zero-width strip they reach only verts on the midline)
        "hair_y0_m": 0.14, "hair_y1_m": 0.20, "trunk_z0_m": 1.25, "trunk_z1_m": 1.38,
        "hair_z0_m": 1.53, "hair_z1_m": 1.65,
        "rigid_x_m": 0.0, "rigid_y_max_m": -9.0, "rigid_z0_m": 0.0, "rigid_z1_m": 0.0,
        "pure_on": 1, "pure_y0_m": -1.0, "pure_y1_m": -0.9, "pure_x0_m": 0.45, "pure_x1_m": 0.52,
        "pure_zbot0_m": 1.04, "pure_zbot1_m": 1.42, "pure_ztop_m": 1.82, "pure_xflat_m": 0.12,
        "pure_zslope": 1.6, "pure_ztop_band_m": 0.16,
    },
    # YOK-102. Kappa, after cut_shell.py and repose_legs.py (heights below are in the re-posed, re-grounded
    # 1.551 m frame: everything 5.1 cm above the take-1 figures in this comment; 1.5 m take: hip joints 0.672 m, knees 0.419 m, arm heads
    # 1.198 m at x 0.268). The auto-skin gave the short torso, the capped back and the shell rim
    # to the arms and legs at once (1,243 bleed verts), the flanks 0.5 UpLeg 20 cm above the hip
    # joint, the crotch to the shins (the splayed knees sit at crotch height) and the calf backs
    # 0.4-0.5 UpLeg, so the shins collapsed in the crouch. Ryo's pelvis rules off (they made it
    # worse). Steps: T2 leg strip over the whole torso and T6 trunk-by-height over the full
    # depth, both from 0.63 to 0.97 m (re-posed frame); C crotch (|x| < 0.12 m: Leg -> UpLeg); K knee: within
    # 0.20 m of each knee head (0.08 m fade) the UpLeg/Leg pair splits by height over +-0.05 m,
    # and below the knee line - 0.02 m the leg's own UpLeg weight goes to Leg, blended over 0.14 m.
    "kappa": {
        "torso_fix": 1, "side_on": 0, "falloff_on": 0, "smooth_pre_iters": 0,
        "region_bottom_m": 0.12, "region_top_m": 0.12, "side_band_m": 0.05,
        "falloff_front_hi_m": 0.07, "falloff_back_lo_m": 0.02, "falloff_back_hi_m": 0.10,
        "trunk_x0_m": 0.0, "trunk_x1_m": 0.001, "hair_x0_m": 0.0, "hair_x1_m": 0.001,
        "torso_smooth_iters": 0, "legstrip_all": 1, "hairleg_z0_m": 0.63, "hairleg_z1_m": 0.97,
        "hair_y0_m": 0.17, "hair_y1_m": 0.22, "trunk_z0_m": 0.89, "trunk_z1_m": 0.99,
        "hair_z0_m": 1.15, "hair_z1_m": 1.25,
        "rigid_x_m": 0.0, "rigid_y_max_m": -9.0, "rigid_z0_m": 0.0, "rigid_z1_m": 0.0,
        "pure_on": 1, "pure_y0_m": -1.0, "pure_y1_m": -0.9,
        "pure_zbot0_m": 0.63, "pure_zbot1_m": 0.97, "pure_ztop_m": 1.30, "pure_xflat_m": 0.15,
        "pure_zslope": 1.4, "pure_ztop_band_m": 0.10,
        "crotch_x_m": 0.12,
        "knee_split": 1, "knee_band_m": 0.05, "knee_radius_m": 0.20, "knee_fade_m": 0.08,
        "shin_clean_m": 0.02, "shin_blend_m": 0.14,
    },
}
TRUNK = ("Hips", "Spine02", "Spine01", "Spine", "neck")
ARM_BONES = tuple(s + n for s in ("Left", "Right") for n in ("Shoulder", "Arm", "ForeArm", "Hand"))
LEG_BONES = tuple(s + n for s in ("Left", "Right") for n in ("UpLeg", "Leg", "Foot", "ToeBase"))


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    a = {"in": None, "out": None, "log": None, "fighter": "ryo", "set": []}
    i = 0
    while i < len(argv):
        k = argv[i].lstrip("-")
        if k not in a or i + 1 >= len(argv):
            sys.exit("repair_weights: bad argument %s" % argv[i])
        if k == "set":
            a["set"].append(argv[i + 1])
        else:
            a[k] = argv[i + 1]
        i += 2
    if not a["in"] or not a["out"]:
        sys.exit("repair_weights: --in and --out are required")
    if os.path.abspath(a["in"]) == os.path.abspath(a["out"]):
        sys.exit("repair_weights: refusing to overwrite the input rig")
    if a["fighter"] not in FIGHTERS:
        sys.exit("repair_weights: unknown fighter %s (have %s)" % (a["fighter"], sorted(FIGHTERS)))
    p = dict(PARAMS, **FIGHTERS[a["fighter"]])
    for s in a["set"]:
        k, v = s.split("=", 1)
        if k not in p:
            sys.exit("repair_weights: unknown param %s" % k)
        p[k] = type(p[k])(float(v)) if isinstance(p[k], float) else int(v)
    return a, p


# ---------------------------------------------------------------- glb

class Glb:
    def __init__(self, path):
        self.raw = bytearray(open(path, "rb").read())
        magic, ver, total = struct.unpack("<III", self.raw[:12])
        ln, ty = struct.unpack("<II", self.raw[12:20])
        assert magic == 0x46546C67 and ty == 0x4E4F534A, "not a GLB"
        self.js = json.loads(bytes(self.raw[20:20 + ln]))
        bl, bt = struct.unpack("<II", self.raw[20 + ln:28 + ln])
        assert bt == 0x004E4942, "no BIN chunk"
        self.bin0 = 28 + ln

    def span(self, ai):
        a = self.js["accessors"][ai]
        bv = self.js["bufferViews"][a["bufferView"]]
        assert "byteStride" not in bv or bv["byteStride"] in (0, None), "strided accessor"
        n = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4}[a["type"]]
        dt = {5121: np.uint8, 5123: np.uint16, 5125: np.uint32, 5126: np.float32}[a["componentType"]]
        off = self.bin0 + bv.get("byteOffset", 0) + a.get("byteOffset", 0)
        return off, dt, a["count"], n

    def read(self, ai):
        off, dt, c, n = self.span(ai)
        return np.frombuffer(bytes(self.raw), dt, c * n, off).reshape(c, n).copy()

    def write(self, ai, arr):
        off, dt, c, n = self.span(ai)
        data = np.ascontiguousarray(arr, dtype=dt).reshape(c, n).tobytes()
        self.raw[off:off + len(data)] = data


# ---------------------------------------------------------------- helpers

def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / np.maximum(e1 - e0, 1e-9), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def world_rest(path):
    """Rest-pose world coords (Blender, metres) and bone heads, via the glTF importer."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=os.path.abspath(path))
    arm = [o for o in bpy.data.objects if o.type == "ARMATURE"][0]
    if arm.animation_data:
        arm.animation_data.action = None
    for pb in arm.pose.bones:
        pb.matrix_basis.identity()
    bpy.context.view_layer.update()
    mesh = [o for o in bpy.data.objects if o.type == "MESH" and o.vertex_groups][0]
    co = np.array([v.co for v in mesh.data.vertices], np.float64)
    m = np.array(mesh.matrix_world)
    co = co @ m[:3, :3].T + m[:3, 3]
    heads = {pb.name: np.array(arm.matrix_world @ pb.head) for pb in arm.pose.bones}
    # base colour per vert (glTF verts are split at UV seams, so each has one UV)
    col = np.zeros((len(co), 3))
    imgs = [n.image for mt in mesh.data.materials if mt and mt.node_tree
            for n in mt.node_tree.nodes if n.type == "TEX_IMAGE" and n.image]
    if imgs:
        w, h = imgs[0].size
        px = np.array(imgs[0].pixels[:]).reshape(h, w, 4)
        uv = np.zeros((len(co), 2))
        uvl = mesh.data.uv_layers.active.data
        for lp in mesh.data.loops:
            uv[lp.vertex_index] = uvl[lp.index].uv
        col = px[np.clip((uv[:, 1] % 1.0 * h).astype(int), 0, h - 1),
                 np.clip((uv[:, 0] % 1.0 * w).astype(int), 0, w - 1), :3]
    return co, heads, col


def weld_groups(P):
    key = np.round(P, 5)
    _, inv = np.unique(key, axis=0, return_inverse=True)
    return inv.reshape(-1)


def adjacency(tris, rep, nv):
    """Neighbour lists over welded verts (rep = weld id per vert)."""
    e = np.concatenate([tris[:, [0, 1]], tris[:, [1, 2]], tris[:, [2, 0]]])
    e = rep[e]
    e = e[e[:, 0] != e[:, 1]]
    e = np.unique(np.sort(e, 1), axis=0)
    return e


def laplacian(Wg, edges, movable, iters, lam):
    n = len(Wg)
    deg = np.bincount(edges.ravel(), minlength=n).astype(np.float64)
    for _ in range(iters):
        acc = np.zeros_like(Wg)
        np.add.at(acc, edges[:, 0], Wg[edges[:, 1]])
        np.add.at(acc, edges[:, 1], Wg[edges[:, 0]])
        avg = np.where(deg[:, None] > 0, acc / np.maximum(deg, 1)[:, None], Wg)
        Wg = np.where(movable[:, None], Wg + lam * (avg - Wg), Wg)
    return Wg


def stats(tag, W0, W1, names, log):
    d = W1 - W0
    changed = int((np.abs(d).max(1) > 1e-4).sum())
    moved = {names[j]: round(float(d[:, j].sum()), 2) for j in range(len(names))
             if np.abs(d[:, j]).sum() > 1e-3}
    log["steps"].append({"step": tag, "verts_changed": changed, "net_weight_by_bone": moved})
    print("repair_weights: %-8s verts changed %5d  %s" % (tag, changed, moved))


# ---------------------------------------------------------------- main

def main():
    a, p = parse_args()
    g = Glb(a["in"])
    prim = g.js["meshes"][0]["primitives"]
    assert len(g.js["meshes"]) == 1 and len(prim) == 1, "expects one mesh, one primitive"
    att = prim[0]["attributes"]
    J = g.read(att["JOINTS_0"]).astype(np.int64)
    Wq = g.read(att["WEIGHTS_0"]).astype(np.float64)
    tris = g.read(prim[0]["indices"]).reshape(-1, 3).astype(np.int64)
    names = [g.js["nodes"][j]["name"] for j in g.js["skins"][0]["joints"]]
    nb, nv = len(names), len(J)
    W = np.zeros((nv, nb))
    for k in range(4):
        np.add.at(W, (np.arange(nv), J[:, k]), Wq[:, k])

    co, heads, col = world_rest(a["in"])
    assert len(co) == nv, "importer vertex count differs from the GLB (%d vs %d)" % (len(co), nv)
    P = g.read(att["POSITION"]).astype(np.float64)
    # index check: the importer keeps glTF vertex order (Blender is Z-up, glTF Y-up)
    pb = np.stack([P[:, 0], -P[:, 2], P[:, 1]], 1)
    ci = np.corrcoef(pb[:, 2], co[:, 2])[0, 1]
    assert ci > 0.9999, "vertex order mismatch between importer and GLB"

    B = {n: i for i, n in enumerate(names)}
    H, L, R = B["Hips"], B["LeftUpLeg"], B["RightUpLeg"]
    LL, RL = B["LeftLeg"], B["RightLeg"]
    hz = heads["Hips"][2]
    uz = 0.5 * (heads["LeftUpLeg"][2] + heads["RightUpLeg"][2])
    kz = 0.5 * (heads["LeftLeg"][2] + heads["RightLeg"][2])
    mid_x = 0.5 * (heads["LeftUpLeg"][0] + heads["RightUpLeg"][0])
    x, y, z = co[:, 0] - mid_x, co[:, 1] - heads["Hips"][1], co[:, 2]
    region = (z > kz + p["region_bottom_m"]) & (z < hz + p["region_top_m"])

    log = {"tool": "tools/blender/repair_weights.py", "fighter": a["fighter"],
           "input": a["in"], "output": a["out"],
           "input_sha256": hashlib.sha256(open(a["in"], "rb").read()).hexdigest(),
           "params": p, "frame": {"hips_z": round(hz, 4), "upleg_z": round(uz, 4),
                                  "leg_z": round(kz, 4), "mid_x": round(mid_x, 4)},
           "region_verts": int(region.sum()), "steps": []}
    print("repair_weights: region verts %d (z %.3f..%.3f)" % (region.sum(), kz + p["region_bottom_m"],
                                                              hz + p["region_top_m"]))

    # 1. weld
    rep = weld_groups(np.round(co, 6))
    nw = rep.max() + 1
    cnt = np.bincount(rep, minlength=nw).astype(np.float64)

    def to_weld(M):
        out = np.zeros((nw, M.shape[1]))
        np.add.at(out, rep, M)
        return out / cnt[:, None]

    seam_mismatch = float(np.abs(W - to_weld(W)[rep]).max())
    log["seam_weight_mismatch_before"] = round(seam_mismatch, 4)
    W0 = W.copy()

    reg_w = np.zeros(nw, bool)
    np.logical_or.at(reg_w, rep, region)
    edges = adjacency(tris, rep, nv)

    def smooth(M, iters):
        """Laplacian smoothing of the Hips / UpLeg columns over the welded mesh, inside the
        region; their per-vert total is kept, so other bones are untouched."""
        Ww = to_weld(M)
        if iters <= 0:
            return Ww[rep]
        cols = [H, L, R]
        sub = Ww[:, cols]
        tot = sub.sum(1, keepdims=True)
        sm = laplacian(sub, edges, reg_w & (tot[:, 0] > 1e-6), iters, p["smooth_lambda"])
        st = sm.sum(1, keepdims=True)
        Ww[:, cols] = np.where(st > 1e-9, sm / np.maximum(st, 1e-9) * tot, sub)
        return Ww[rep]

    # verts the repair may change; Ryo: the pelvis band only
    edit = region.copy()
    TI = [B[n] for n in TRUNK]
    AI = [B[n] for n in ARM_BONES]
    LI = [B[n] for n in LEG_BONES]
    trunk_z = np.array([heads[n][2] for n in TRUNK])

    def trunk_split(zz):
        """Per vert, a weight over TRUNK by height: linear between consecutive joint heads."""
        out = np.zeros((len(zz), len(TRUNK)))
        zc = np.clip(zz, trunk_z[0], trunk_z[-1])
        k = np.clip(np.searchsorted(trunk_z, zc) - 1, 0, len(TRUNK) - 2)
        t = (zc - trunk_z[k]) / (trunk_z[k + 1] - trunk_z[k])
        out[np.arange(len(zz)), k] = 1 - t
        out[np.arange(len(zz)), k + 1] += t
        return out

    Wt = W
    if p["torso_fix"]:
        # T1. arm strip in the torso column and the hair layer behind the back
        hair = smoothstep(p["hair_y0_m"], p["hair_y1_m"], y)        # 0 body .. 1 hair layer
        mix = lambda k: p["trunk_" + k] + hair * (p["hair_" + k] - p["trunk_" + k])
        strip = ((1 - smoothstep(mix("x0_m"), mix("x1_m"), np.abs(x)))
                 * (1 - smoothstep(mix("z0_m"), mix("z1_m"), z)) * (z > uz - 0.1))
        WA = W.copy()
        cut = WA[:, AI] * strip[:, None]
        WA[:, AI] -= cut
        WA[:, TI] += cut.sum(1)[:, None] * trunk_split(z)
        stats("armstrip", W0, WA, names, log)
        # T2. leg strip in the hair layer above the seat
        WB = WA.copy()
        layer = np.ones_like(hair) if p["legstrip_all"] else hair
        lcut = WB[:, LI] * (layer * smoothstep(p["hairleg_z0_m"], p["hairleg_z1_m"], z))[:, None]
        WB[:, LI] -= lcut
        WB[:, TI] += lcut.sum(1)[:, None] * trunk_split(z)
        stats("legstrip", WA, WB, names, log)
        # T3. smooth [arm bones, trunk total] over the T1/T2 verts
        tedit = (strip > 1e-3) | (lcut.sum(1) > 1e-6)
        tw = np.zeros(nw, bool)
        np.logical_or.at(tw, rep, tedit)
        Ww, Wo = to_weld(WB), to_weld(W)
        sub = np.concatenate([Ww[:, AI], Ww[:, TI].sum(1, keepdims=True)], 1)
        tot = sub.sum(1, keepdims=True)
        sm = laplacian(sub, edges, tw & (tot[:, 0] > 1e-6), p["torso_smooth_iters"], p["smooth_lambda"])
        st = sm.sum(1, keepdims=True)
        sm = np.where(st > 1e-9, sm / np.maximum(st, 1e-9) * tot, sub)
        Ww[:, AI] = sm[:, :-1]
        t_new, T0 = sm[:, -1], Wo[:, TI]
        t0 = T0.sum(1)
        t_keep = np.minimum(t_new, t0)
        dist0 = np.where(t0[:, None] > 1e-9, T0 / np.maximum(t0, 1e-9)[:, None], 0)
        Ww[:, TI] = dist0 * t_keep[:, None] + trunk_split(to_weld(z[:, None])[:, 0]) * (t_new - t_keep)[:, None]
        Wt = Ww[rep]
        stats("tsmooth", WB, Wt, names, log)
        edit |= tedit
        log["torso_verts"] = int(tedit.sum())
        print("repair_weights: torso verts %d" % tedit.sum())

    # 1. pre-smooth: take the blockiness out of the auto-skin before the rules below
    Ws = smooth(Wt, p["smooth_pre_iters"])
    stats("smooth1", Wt, Ws, names, log)

    # 2. side separation (UpLeg and Leg pairs): each half of the crotch follows its own leg
    W1 = Ws.copy()
    s_left = smoothstep(-p["side_band_m"], p["side_band_m"], x)
    for lc, rc in (((L, R), (LL, RL)) if p["side_on"] else ()):
        u = W1[:, lc] + W1[:, rc]
        sel = region & (u > 0)
        W1[sel, lc] = u[sel] * s_left[sel]
        W1[sel, rc] = u[sel] * (1 - s_left[sel])
    stats("side", Ws, W1, names, log)

    # 3. falloff of UpLeg weight above the hip joint -> Hips
    W3 = W1.copy()
    lo = np.where(y < 0, p["falloff_front_lo_m"], p["falloff_back_lo_m"])
    hi = np.where(y < 0, p["falloff_front_hi_m"], p["falloff_back_hi_m"])
    keep = 1 - smoothstep(uz + lo, uz + hi, z)
    for c in ((L, R) if p["falloff_on"] else ()):
        cut = W3[:, c] * (1 - keep) * region
        W3[:, c] -= cut
        W3[:, H] += cut
    stats("falloff", W1, W3, names, log)

    if p["torso_fix"]:
        # T4. arm-and-leg bleed: the smaller group goes to the trunk
        W2 = W3.copy()
        aw, lw = W2[:, AI].sum(1), W2[:, LI].sum(1)
        both = (aw > 0) & (lw > 0)
        for msk, grp in ((both & (aw <= lw), AI), (both & (aw > lw), LI)):
            c = W2[:, grp] * msk[:, None]
            W2[:, grp] -= c
            W2[:, TI] += c.sum(1)[:, None] * trunk_split(z)
        stats("bleed", W3, W2, names, log)
        log["bleed_verts"] = int(both.sum())
        edit |= both
        W3 = W2
        # T5. rigid prop: the small part inside the box (Kitsune: the bell on the sash front)
        # is bound to one bone, so the midline leg blend cannot shear it
        rig = ((np.abs(x) < p["rigid_x_m"]) & (y < p["rigid_y_max_m"])
               & (z > p["rigid_z0_m"]) & (z < p["rigid_z1_m"]))
        if rig.any():
            W2 = W3.copy()
            W2[rig] = W3[rig].mean(0)
            stats("rigid", W3, W2, names, log)
            log["rigid_verts"] = int(rig.sum())
            edit |= rig
            W3 = W2

    # C. crotch (off unless crotch_x_m > 0): near the midline below the hip joint the auto-skin can
    # give the crotch to the shins (Leg) when the knees sit at crotch height; that weight goes to
    # the same side's UpLeg.
    if p["crotch_x_m"] > 0:
        WC = W3.copy()
        cz = (np.abs(x) < p["crotch_x_m"]) & (z < uz)
        for up, lo_ in ((L, LL), (R, RL)):
            WC[cz, up] += WC[cz, lo_]
            WC[cz, lo_] = 0
        stats("crotch", W3, WC, names, log)
        edit |= cz
        W3 = WC

    # T6. pure trunk zone (off unless pure_on; YOK-102 Kappa): behind the back, inside the zone,
    # the weights blend to the trunk height split alone (no arms, no legs, no per-vert noise),
    # so folded or thin geometry there (the Kappa's capped back and the shell rim left at its
    # edge, both under the mounted shell) moves as one smooth plate with the spine.
    if p["pure_on"]:
        ztop = p["pure_ztop_m"] - p["pure_zslope"] * np.maximum(np.abs(x) - p["pure_xflat_m"], 0)
        zone = (smoothstep(p["pure_y0_m"], p["pure_y1_m"], y)
                * (1 - smoothstep(p["pure_x0_m"], p["pure_x1_m"], np.abs(x)))
                * smoothstep(p["pure_zbot0_m"], p["pure_zbot1_m"], z)
                * (1 - smoothstep(ztop - p["pure_ztop_band_m"], ztop + p["pure_ztop_band_m"], z)))
        zone = to_weld(zone[:, None])[rep][:, 0]
        WP = W3.copy() * (1 - zone)[:, None]
        WP[:, TI] += zone[:, None] * trunk_split(to_weld(z[:, None])[rep][:, 0])
        stats("pure", W3, WP, names, log)
        log["pure_verts"] = int((zone > 1e-3).sum())
        edit |= zone > 1e-3
        W3 = WP

    # K. knee smoothing (off unless knee_iters > 0; YOK-102 Kappa): the auto-skin splits the knee
    # between UpLeg and Leg in steps, so the back of the knee and the calf top collapse in a deep
    # crouch. Laplacian smoothing of each side's [UpLeg, Leg] pair over the verts within
    # knee_band_m of that knee head (the pair's per-vert total is kept; other bones untouched).
    if p["knee_iters"] > 0 or p["knee_split"]:
        WK = to_weld(W3)
        for up, lo_, kn in ((L, LL, "LeftLeg"), (R, RL, "RightLeg")):
            if p["knee_radius_m"] > 0:
                dk = np.linalg.norm(co - heads[kn][None, :], axis=1)
                kfade = 1 - smoothstep(p["knee_radius_m"] - p["knee_fade_m"], p["knee_radius_m"], dk)
                kb = kfade > 1e-3
            else:
                kfade = (np.abs(z - heads[kn][2]) < p["knee_band_m"]).astype(np.float64)
                kb = kfade > 0
            Wbefore = WK[:, [up, lo_]].copy()
            kw = np.zeros(nw, bool)
            np.logical_or.at(kw, rep, kb)
            sub = WK[:, [up, lo_]]
            tot = sub.sum(1, keepdims=True)
            sm = laplacian(sub, edges, kw & (tot[:, 0] > 1e-6), p["knee_iters"], p["smooth_lambda"])
            st = sm.sum(1, keepdims=True)
            WK[:, [up, lo_]] = np.where(st > 1e-9, sm / np.maximum(st, 1e-9) * tot, sub)
            if p["knee_split"]:
                zw = to_weld(z[:, None])[:, 0]
                kz_ = heads[kn][2]
                share = smoothstep(kz_ - p["knee_band_m"], kz_ + p["knee_band_m"], zw)
                pair = WK[:, up] + WK[:, lo_]
                ksel = kw & (pair > 1e-6)
                WK[ksel, up] = pair[ksel] * share[ksel]
                WK[ksel, lo_] = pair[ksel] * (1 - share[ksel])
            kf = to_weld(kfade[:, None])[:, 0]
            WK[:, [up, lo_]] = kf[:, None] * WK[:, [up, lo_]] + (1 - kf[:, None]) * Wbefore
            edit |= kb
            if p["shin_clean_m"] > 0:
                side = np.sign(heads[kn][0] - mid_x)
                zs = heads[kn][2] - p["shin_clean_m"]
                f = (1 - smoothstep(zs - p["shin_blend_m"], zs, z)) * ((x * side) > 0.03)
                fw = to_weld(f[:, None])[:, 0]
                mv = WK[:, up] * fw
                WK[:, up] -= mv
                WK[:, lo_] += mv
                edit |= f > 1e-3
        stats("knee", W3, WK[rep], names, log)
        W3 = WK[rep]

    # S. band smoothing (off unless band_smooth_iters > 0): Laplacian smoothing of the whole weight
    # vector (every bone; rows stay normalised) over the verts between band_z0_m and band_z1_m.
    # Takes the per-vert noise of the auto-skin out of a transition (the Kappa's seat and back,
    # where neighbours differ by 0.3 UpLeg) after the rules above have set its shape.
    if p["band_smooth_iters"] > 0:
        bsel = (z > p["band_z0_m"]) & (z < p["band_z1_m"]) & (np.abs(x) < p["band_x_m"])
        bw = np.zeros(nw, bool)
        np.logical_or.at(bw, rep, bsel)
        WS = laplacian(to_weld(W3), edges, bw, p["band_smooth_iters"], p["smooth_lambda"])
        stats("bandsm", W3, WS[rep], names, log)
        edit |= bsel
        W3 = WS[rep]

    # 4. props: the charm, pouch and rope sash are baked rigid to Hips (rig plan, ryo.md). They
    # are painted parts of the one body mesh, so they are found by base colour inside the
    # pelvis band (light paper/pouch, persimmon rope), kept when a connected patch has at least
    # prop_min_verts verts, set to Hips 1.0, and blended into the body over prop_rings rings.
    W3b = W3.copy()
    if not p["prop_bind"]:
        W3b = W3  # step off: props keep the falloff weights
    light = (col.mean(1) > p["prop_light_min"]) & (col.max(1) - col.min(1) < p["prop_light_max_chroma"])
    rope = (col[:, 0] > 0.35) & (col[:, 0] > 1.8 * col[:, 2]) & (col[:, 0] > 1.3 * col[:, 1])
    band = (z > uz + p["prop_z_lo_m"]) & (z < hz + p["region_top_m"]) & region
    cand_w = np.zeros(nw, bool)
    np.logical_or.at(cand_w, rep, band & (light | rope))
    ce = edges[cand_w[edges[:, 0]] & cand_w[edges[:, 1]]]
    par = np.arange(nw)

    def find(i):
        while par[i] != i:
            par[i] = par[par[i]]
            i = par[i]
        return i
    for i, j in ce:
        ri, rj = find(i), find(j)
        if ri != rj:
            par[ri] = rj
    roots = np.array([find(i) if cand_w[i] else -1 for i in range(nw)])
    ids, counts = np.unique(roots[roots >= 0], return_counts=True)
    keep_ids = ids[counts >= p["prop_min_verts"]]
    prop_w = np.isin(roots, keep_ids)
    # ring distance from the props over the welded mesh
    ring = np.full(nw, 99)
    ring[prop_w] = 0
    for k in range(1, p["prop_rings"] + 1):
        nxt = np.zeros(nw, bool)
        nxt[edges[ring[edges[:, 0]] == k - 1, 1]] = True
        nxt[edges[ring[edges[:, 1]] == k - 1, 0]] = True
        ring[nxt & (ring == 99)] = k
    t = np.where(ring <= p["prop_rings"], 1 - ring / (p["prop_rings"] + 1.0), 0.0)[rep]
    t = t * region * (1 if p["prop_bind"] else 0)
    # leg influence (UpLeg) goes to Hips; Spine02's share at the waist is left as it is
    for c in (L, R):
        cut = W3b[:, c] * t
        W3b[:, c] -= cut
        W3b[:, H] += cut
    log["props"] = {"patches": [int(c) for c in sorted(counts[counts >= p["prop_min_verts"]], reverse=True)],
                    "rigid_verts": int((t >= 1).sum()), "blend_verts": int(((t > 0) & (t < 1)).sum())}
    print("repair_weights: props    patches %s, rigid %d, blend %d" % (
        log["props"]["patches"], log["props"]["rigid_verts"], log["props"]["blend_verts"]))
    stats("props", W3, W3b, names, log)

    W4 = to_weld(W3b)[rep]

    # 5. tidy (edited verts only: the pelvis band, plus the torso steps when on; the rest of the
    # skin keeps its exact weights): prune, limit, normalise
    W4 = np.where(edit[:, None], W4, W0)
    touched = edit | (np.abs(W4 - W0).max(1) > 0)
    W4[touched[:, None] & (W4 < p["min_weight"])] = 0
    order = np.argsort(-W4, 1, kind="stable")
    J_new = order[:, :p["max_influences"]]
    W_new = np.take_along_axis(W4, J_new, 1)
    W_new /= W_new.sum(1, keepdims=True)
    # untouched verts: write back their original slots exactly
    J_new = np.where(touched[:, None], J_new, J[:, :p["max_influences"]])
    W_new = np.where(touched[:, None], W_new, Wq[:, :p["max_influences"]])
    W5 = np.zeros_like(W)
    for k in range(W_new.shape[1]):
        np.add.at(W5, (np.arange(nv), J_new[:, k]), W_new[:, k])
    stats("tidy", W4, W5, names, log)
    log["verts_touched"] = int(touched.sum())
    stats("total", W0, W5, names, log)
    log["seam_weight_mismatch_after"] = round(float(np.abs(W5 - to_weld(W5)[rep]).max()), 4)

    # write 4 slots, largest first; empty slots point at joint 0 with weight 0
    Jw = np.where(W_new > 0, J_new, np.where(touched[:, None], 0, J_new))
    if p["max_influences"] < 4:
        Jw = np.pad(Jw, ((0, 0), (0, 4 - Jw.shape[1])))
        W_new = np.pad(W_new, ((0, 0), (0, 4 - W_new.shape[1])))
    g.write(att["JOINTS_0"], Jw)
    g.write(att["WEIGHTS_0"], W_new.astype(np.float32))
    os.makedirs(os.path.dirname(os.path.abspath(a["out"])), exist_ok=True)
    with open(a["out"], "wb") as f:
        f.write(g.raw)
    log["output_sha256"] = hashlib.sha256(bytes(g.raw)).hexdigest()
    print("repair_weights: wrote", a["out"], log["output_sha256"][:12])
    if a["log"]:
        with open(a["log"], "w", encoding="utf-8") as f:
            json.dump(log, f, indent=1)
    return 0


if __name__ == "__main__":
    code = main()
    sys.stdout.flush()
    os._exit(code)
