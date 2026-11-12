"""Add a closed-fist morph target to the open-hand rig (designer 2026-11-09, "fist as a blend shape"; AM26, E15, F5).

Plain Python (numpy), no Blender. Run after the rig pipeline has made both rigs from the same take:
  open:  canon_tripo_rig.py          -> repair_weights.py -> apply_texture.py  (--base)
  fist:  canon_tripo_rig.py --fist   -> repair_weights.py -> apply_texture.py  (--fist)
The two must differ only in POSITION/NORMAL rows of the hand vertices, same vertex order (rig_identity_check.py).

Writes --out = the --base GLB byte for byte (every existing bufferView, accessor and JSON entry unchanged), plus
one glTF morph target on meshes[0].primitives[0]:
  - targets[0] = {POSITION: fist - base, NORMAL: fist - base}, two new dense float32 accessors appended to the
    binary chunk; rows are zero except on the vertices the fist moved;
  - meshes[0].extras.targetNames = [--name] (default "fist"), the name Blender and Godot give the blend shape;
  - meshes[0].weights = [--weight] (default 1.0: closed fist by default; a clip opens the hand through clip data).
Deterministic: same inputs give the same bytes.

  python tools/blender/fist_morph.py --base <open.glb> --fist <fist.glb> --out <out.glb> [--name fist]
      [--weight 1.0] [--log <out.json>]
"""
import argparse
import json
import struct
import sys

import numpy as np


def load(path):
    b = open(path, "rb").read()
    if b[:4] != b"glTF" or struct.unpack("<I", b[16:20])[0] != 0x4E4F534A:
        sys.exit("fist_morph: %s is not a GLB with a JSON first chunk" % path)
    ln = struct.unpack("<I", b[12:16])[0]
    j = json.loads(b[20:20 + ln])
    bl = struct.unpack("<I", b[20 + ln:24 + ln])[0]
    return j, bytearray(b[28 + ln:28 + ln + bl])


def acc(g, i):
    j, buf = g
    a = j["accessors"][i]
    if a["componentType"] != 5126 or a["type"] != "VEC3" or "sparse" in a:
        sys.exit("fist_morph: accessor %d is not a dense float32 VEC3" % i)
    bv = j["bufferViews"][a["bufferView"]]
    if bv.get("byteStride", 12) != 12:
        sys.exit("fist_morph: accessor %d is interleaved" % i)
    return np.frombuffer(bytes(buf), np.float32, a["count"] * 3,
                         bv.get("byteOffset", 0) + a.get("byteOffset", 0)).reshape(-1, 3)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--base", required=True)
    ap.add_argument("--fist", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--name", default="fist")
    ap.add_argument("--weight", type=float, default=1.0)
    ap.add_argument("--log")
    a = ap.parse_args()
    B, F = load(a.base), load(a.fist)
    mesh = B[0]["meshes"][0]
    if len(B[0]["meshes"]) != 1 or len(mesh["primitives"]) != 1 or "targets" in mesh["primitives"][0]:
        sys.exit("fist_morph: expected one mesh, one primitive, no morph targets in --base")
    pa, fa = mesh["primitives"][0]["attributes"], F[0]["meshes"][0]["primitives"][0]["attributes"]
    Pb, Nb, Pf, Nf = acc(B, pa["POSITION"]), acc(B, pa["NORMAL"]), acc(F, fa["POSITION"]), acc(F, fa["NORMAL"])
    if Pb.shape != Pf.shape:
        sys.exit("fist_morph: vertex counts differ (%d vs %d); not the same take" % (len(Pb), len(Pf)))
    dP = (Pf.astype(np.float64) - Pb).astype(np.float32)
    dN = (Nf.astype(np.float64) - Nb).astype(np.float32)
    moved = np.any(dP != 0, axis=1) | np.any(dN != 0, axis=1)
    j, buf = B
    pad = (-len(buf)) % 4
    buf += b"\0" * pad
    new_acc = {}
    for key, d in (("POSITION", dP), ("NORMAL", dN)):
        off = len(buf)
        raw = d.tobytes()
        buf += raw
        j["bufferViews"].append({"buffer": 0, "byteLength": len(raw), "byteOffset": off, "target": 34962})
        ad = {"bufferView": len(j["bufferViews"]) - 1, "componentType": 5126, "count": len(d), "type": "VEC3"}
        if key == "POSITION":  # required on morph POSITION accessors
            ad["max"] = [float(x) for x in d.max(axis=0)]
            ad["min"] = [float(x) for x in d.min(axis=0)]
        j["accessors"].append(ad)
        new_acc[key] = len(j["accessors"]) - 1
    mesh["primitives"][0]["targets"] = [new_acc]
    mesh["weights"] = [a.weight]
    mesh.setdefault("extras", {})["targetNames"] = [a.name]
    buf += b"\0" * ((-len(buf)) % 4)
    j["buffers"][0]["byteLength"] = len(buf)
    js = json.dumps(j, separators=(",", ":")).encode("utf-8")
    js += b" " * ((-len(js)) % 4)
    out = (b"glTF" + struct.pack("<II", 2, 12 + 8 + len(js) + 8 + len(buf))
           + struct.pack("<I", len(js)) + b"JSON" + js + struct.pack("<I", len(buf)) + b"BIN\0" + bytes(buf))
    open(a.out, "wb").write(out)
    rep = {"base": a.base, "fist": a.fist, "out": a.out, "target_name": a.name, "default_weight": a.weight,
           "verts": int(len(Pb)), "verts_with_nonzero_delta": int(moved.sum()),
           "max_position_delta_m": round(float(np.abs(dP).max()), 5),
           "max_recon_error_position": float(np.abs((Pb + dP) - Pf).max()),
           "max_recon_error_normal": float(np.abs((Nb + dN) - Nf).max()),
           "accessors_added": [new_acc["POSITION"], new_acc["NORMAL"]], "bytes": len(out)}
    print("fist_morph:", json.dumps(rep))
    if a.log:
        with open(a.log, "w", encoding="utf-8") as f:
            json.dump(rep, f, indent=1)


main()
