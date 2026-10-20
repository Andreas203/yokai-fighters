"""Project the four Astra paintings onto a mesh's existing UVs and bake a clean base colour (YOK-146, phase B).

Inputs: the rigged GLB (rest T-pose), `views.json` from texture_views.py and the paintings
`ryo-paint-{front,left,right,back}.png`. Output: one 2048 px sRGB PNG on the mesh's own UV layout, a coverage map.
No bones, weights or UVs are changed (this script only reads the GLB). Apply the PNG with apply_texture.py.

Per texel (3D position and normal from the UV rasterisation):
  - each view contributes only if the texel faces it (normal . direction to camera >= --min-cos), is not occluded
    (a z-buffer of the mesh in that view, so the back view never paints the front) and lies inside that view's mask
    eroded --erode px (drops the edge mismatch between paint and mesh);
  - views are blended with weight (normal . direction)^--power;
  - texels no view sees are filled from the median of the nearest painted texels in 3D (armpits, sleeve insides, soles, under the chin);
  - every UV island is padded --pad px by dilation (clean bleed), the rest takes the mean colour.

Run (Blender 4.1.1, headless; no credits):
  blender -b --python tools/blender/texture_project.py -- [--in <rigged.glb>] [--views <views.json>]
      [--paint-dir <dir>] [--out <png>] [--res 2048] [--power 3] [--erode 3] [--pad 16] [--min-cos 0.25]
"""
import json
import os
import struct
import sys
import zlib

import bpy
import numpy as np
from mathutils import Matrix, kdtree

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import clip_common as cc  # noqa: E402

TEX_DIR = "assets/specs/texture/ryo-tripo"
FILL_K = 15  # neighbours whose median colour fills an unseen texel
VIEW_NAMES = ("front", "left", "right", "back")


def arg(a, k, d):
    return a[k][0] if a.get(k) else d


def write_png(path, rgb):
    """RGB uint8 (H, W, 3), top row first -> PNG, no dependencies."""
    h, w, _ = rgb.shape
    raw = b"".join(b"\x00" + rgb[y].tobytes() for y in range(h))

    def chunk(t, d):
        c = struct.pack(">I", len(d)) + t + d
        return c + struct.pack(">I", zlib.crc32(t + d) & 0xFFFFFFFF)
    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0))
                + chunk(b"IDAT", zlib.compress(raw, 6)) + chunk(b"IEND", b""))


def read_img(path):
    """RGB float (H, W, 3) top row first, raw values (no colour management)."""
    im = bpy.data.images.load(os.path.abspath(path))
    im.colorspace_settings.name = "Non-Color"
    w, h = im.size
    px = np.empty(w * h * 4, np.float32)
    im.pixels.foreach_get(px)
    bpy.data.images.remove(im)
    return px.reshape(h, w, 4)[::-1, :, :3].copy()


def raster(tri_xy, size, per_corner):
    """Yield (ys, xs, bary(n,3)) for texel centres inside one triangle. tri_xy (3,2) in pixel units."""
    x0 = max(int(np.floor(tri_xy[:, 0].min() - 0.5)), 0)
    x1 = min(int(np.ceil(tri_xy[:, 0].max() + 0.5)), size[0] - 1)
    y0 = max(int(np.floor(tri_xy[:, 1].min() - 0.5)), 0)
    y1 = min(int(np.ceil(tri_xy[:, 1].max() + 0.5)), size[1] - 1)
    if x1 < x0 or y1 < y0:
        return None
    a, b, c = tri_xy
    den = (b[1] - c[1]) * (a[0] - c[0]) + (c[0] - b[0]) * (a[1] - c[1])
    if abs(den) < 1e-12:
        return None
    ys, xs = np.mgrid[y0:y1 + 1, x0:x1 + 1]
    px, py = xs + 0.5, ys + 0.5
    l0 = ((b[1] - c[1]) * (px - c[0]) + (c[0] - b[0]) * (py - c[1])) / den
    l1 = ((c[1] - a[1]) * (px - c[0]) + (a[0] - c[0]) * (py - c[1])) / den
    l2 = 1 - l0 - l1
    eps = -1e-4
    ins = (l0 >= eps) & (l1 >= eps) & (l2 >= eps)
    if not ins.any():
        return None
    return ys[ins], xs[ins], np.stack([l0[ins], l1[ins], l2[ins]], 1)


def erode(m, n):
    m = m.copy()
    for _ in range(n):
        p = np.pad(m, 1, constant_values=False)
        m = (p[1:-1, 1:-1] & p[:-2, 1:-1] & p[2:, 1:-1] & p[1:-1, :-2] & p[1:-1, 2:]
             & p[:-2, :-2] & p[:-2, 2:] & p[2:, :-2] & p[2:, 2:])
    return m


def sample(img, fx, fy):
    """Bilinear sample of img (H,W,3) at pixel-centre coordinates fx, fy (pixel units, centres at +0.5)."""
    h, w, _ = img.shape
    x, y = fx - 0.5, fy - 0.5
    x0, y0 = np.floor(x).astype(int), np.floor(y).astype(int)
    tx, ty = (x - x0)[:, None], (y - y0)[:, None]
    x0c, x1c = np.clip(x0, 0, w - 1), np.clip(x0 + 1, 0, w - 1)
    y0c, y1c = np.clip(y0, 0, h - 1), np.clip(y0 + 1, 0, h - 1)
    return (img[y0c, x0c] * (1 - tx) * (1 - ty) + img[y0c, x1c] * tx * (1 - ty)
            + img[y1c, x0c] * (1 - tx) * ty + img[y1c, x1c] * tx * ty)


def main():
    a = cc.script_args("texture_project")
    inp = arg(a, "--in", "game/assets/generated/characters/ryo/ryo-tripo-rigged.glb")
    views_path = arg(a, "--views", TEX_DIR + "/views.json")
    paint_dir = arg(a, "--paint-dir", TEX_DIR)
    out = arg(a, "--out", "game/assets/generated/characters/ryo/ryo-tripo-basecolor.png")
    res = int(arg(a, "--res", 2048))
    power = float(arg(a, "--power", 3))
    er = int(arg(a, "--erode", 3))
    pad = int(arg(a, "--pad", 16))
    min_cos = float(arg(a, "--min-cos", 0.25))
    views = json.load(open(views_path))
    vres, ortho = views["resolution"][0], views["ortho_scale"]

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=os.path.abspath(inp))
    obj = [o for o in bpy.data.objects if o.type == "MESH" and any(m.type == "ARMATURE" for m in o.modifiers)][0]
    for ar in bpy.data.armatures:
        ar.pose_position = "REST"
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    me = obj.evaluated_get(dg).to_mesh()
    nv = len(me.vertices)
    co = np.empty(nv * 3)
    me.vertices.foreach_get("co", co)
    m = np.array(obj.matrix_world)
    P = co.reshape(-1, 3) @ m[:3, :3].T + m[:3, 3]
    me.calc_loop_triangles()
    nt = len(me.loop_triangles)
    T = np.empty(nt * 3, np.int64)
    me.loop_triangles.foreach_get("vertices", T)
    T = T.reshape(-1, 3)
    TL = np.empty(nt * 3, np.int64)
    me.loop_triangles.foreach_get("loops", TL)
    TL = TL.reshape(-1, 3)
    uvl = me.uv_layers.active
    uvs = np.empty(len(me.loops) * 2)
    uvl.data.foreach_get("uv", uvs)
    uvs = uvs.reshape(-1, 2)
    print("texture_project: %d verts, %d triangles" % (nv, nt))

    fn = np.cross(P[T[:, 1]] - P[T[:, 0]], P[T[:, 2]] - P[T[:, 0]])
    N = np.zeros_like(P)
    for k in range(3):
        np.add.at(N, T[:, k], fn)
    N /= np.maximum(np.linalg.norm(N, axis=1, keepdims=True), 1e-12)

    # 1. UV rasterisation: per texel 3D position, normal
    pos = np.zeros((res, res, 3), np.float32)
    nrm = np.zeros((res, res, 3), np.float32)
    have = np.zeros((res, res), bool)
    for t in range(nt):
        uv = uvs[TL[t]] * res  # Blender UV: v up, so row index = v * res (bottom-up array)
        r = raster(uv, (res, res), None)
        if r is None:
            continue
        ys, xs, bc = r
        pos[ys, xs] = bc @ P[T[t]]
        nrm[ys, xs] = bc @ N[T[t]]
        have[ys, xs] = True
    nrm /= np.maximum(np.linalg.norm(nrm, axis=2, keepdims=True), 1e-9)
    ty, tx = np.nonzero(have)
    tp, tn = pos[ty, tx].astype(np.float64), nrm[ty, tx].astype(np.float64)
    print("texture_project: %d texels on islands (%.1f%% of the sheet)" % (len(ty), 100.0 * len(ty) / res / res))

    # 2. views
    acc = np.zeros((len(ty), 3))
    wsum = np.zeros(len(ty))
    stats = {}
    for name in VIEW_NAMES:
        mw = np.array(views["views"][name]["matrix_world"])
        inv = np.linalg.inv(mw)
        back = mw[:3, 2] / np.linalg.norm(mw[:3, 2])  # camera +Z points from the scene to the camera
        paint = read_img(os.path.join(paint_dir, "ryo-paint-%s.png" % name))
        mask = read_img(os.path.join(paint_dir, "ryo-mask-%s.png" % name))[:, :, 0] < 0.5
        mask_e = erode(mask, er)
        ph, pw, _ = paint.shape
        sc = pw / vres  # paint may be a different size from the render; they share the framing

        def to_pix(X):
            c = X @ inv[:3, :3].T + inv[:3, 3]
            return (c[:, 0] / ortho + 0.5) * vres, (0.5 - c[:, 1] / ortho) * vres, -c[:, 2]
        # z-buffer of the mesh in this view
        vx, vy, vz = to_pix(P)
        zb = np.full((vres, vres), np.inf, np.float32)
        for t in range(nt):
            xy = np.stack([vx[T[t]], vy[T[t]]], 1)
            r = raster(xy, (vres, vres), None)
            if r is None:
                continue
            ys, xs, bc = r
            d = (bc @ vz[T[t]]).astype(np.float32)
            zb[ys, xs] = np.minimum(zb[ys, xs], d)
        fx, fy, fz = to_pix(tp)
        ix, iy = np.clip(np.floor(fx).astype(int), 0, vres - 1), np.clip(np.floor(fy).astype(int), 0, vres - 1)
        cosv = tn @ back
        px_m = ortho / vres
        tol = 0.006 + 3 * px_m * np.sqrt(np.clip(1 - cosv ** 2, 0, 1)) / np.maximum(cosv, 0.25)
        vis = (fz <= zb[iy, ix] + tol) & (cosv >= min_cos) & mask_e[np.clip((fy * sc).astype(int), 0, ph - 1),
                                                                        np.clip((fx * sc).astype(int), 0, pw - 1)]
        col = sample(paint, fx * sc, fy * sc)
        vis &= ~(col.min(1) > 0.98)  # background white leaking in at the edge
        w = np.where(vis, np.clip(cosv, 0, 1) ** power, 0.0)
        acc += col * w[:, None]
        wsum += w
        stats[name] = int(vis.sum())
        print("texture_project: view %s paints %d texels" % (name, vis.sum()))
    painted = wsum > 1e-6
    tex = np.zeros((res, res, 3), np.float32)
    kind = np.zeros((res, res), np.uint8)  # 0 outside, 1 painted, 2 filled, 3 padding
    tex[ty[painted], tx[painted]] = acc[painted] / wsum[painted, None]
    kind[ty[painted], tx[painted]] = 1

    # 3. fill texels no view sees from the nearest painted texel in 3D
    un = ~painted
    print("texture_project: %d texels (%.1f%%) seen by no view, filled from the median of the nearest painted texels"
          % (un.sum(), 100.0 * un.sum() / len(ty)))
    if un.any() and painted.any():
        sel = np.nonzero(painted)[0][::3]
        kd = kdtree.KDTree(len(sel))
        for i, s in enumerate(sel):
            kd.insert(tp[s], i)
        kd.balance()
        selc = tex[ty[sel], tx[sel]].copy()  # painted colours only (the fill below never feeds itself)
        for j in np.nonzero(un)[0]:
            near = kd.find_n(tp[j], FILL_K)
            tex[ty[j], tx[j]] = np.median(selc[[i for _, i, _ in near]], axis=0)  # median: a thin rope or wrap next to cloth does not leak in
        kind[ty[un], tx[un]] = 2

    # 4. pad the islands by dilation
    valid = kind > 0
    for _ in range(pad):
        p = np.pad(valid, 1)
        pc = np.pad(tex, ((1, 1), (1, 1), (0, 0)))
        s = np.zeros_like(tex)
        n = np.zeros(valid.shape, np.float32)
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                if dy == 0 and dx == 0:
                    continue
                sv = p[1 + dy:1 + dy + res, 1 + dx:1 + dx + res]
                s += pc[1 + dy:1 + dy + res, 1 + dx:1 + dx + res] * sv[:, :, None]
                n += sv
        new = (~valid) & (n > 0)
        tex[new] = s[new] / n[new, None]
        kind[new] = 3
        valid |= new
    mean = tex[kind > 0].mean(0)
    tex[~valid] = mean
    img8 = np.clip(np.round(tex * 255), 0, 255).astype(np.uint8)[::-1]  # bottom-up -> top-down PNG
    os.makedirs(os.path.dirname(os.path.abspath(out)), exist_ok=True)
    write_png(out, img8)
    cov = np.zeros((res, res, 3), np.uint8)
    cov[kind == 1] = (255, 255, 255)
    cov[kind == 2] = (230, 60, 60)
    cov[kind == 3] = (110, 110, 110)
    cov_path = os.path.join(TEX_DIR, "diagnosis", "projection-coverage.png")
    os.makedirs(os.path.dirname(os.path.abspath(cov_path)), exist_ok=True)
    write_png(cov_path, cov[::-1][::2, ::2])
    cc.write_json(os.path.join(TEX_DIR, "projection.json"), {
        "tool": "texture_project.py", "input": inp, "input_sha256": cc.sha256(inp), "out": out, "out_sha256": cc.sha256(out),
        "resolution": res, "power": power, "erode_px": er, "pad_px": pad, "min_cos": min_cos,
        "island_texels": int(len(ty)), "painted": int((kind == 1).sum()), "filled": int((kind == 2).sum()),
        "view_texels": stats})
    print("texture_project: wrote", out)
    cc.finish(0)


main()
