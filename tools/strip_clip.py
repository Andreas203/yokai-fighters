#!/usr/bin/env python3
"""Make an animation-only copy of a Meshy clip GLB: skeleton + skin + animation kept, the mesh replaced by one
degenerate (invisible) skinned triangle, materials/textures/images dropped. Poses are unchanged.
Usage: python tools/strip_clip.py in.glb out.glb"""
import json, struct, sys
import numpy as np

NC = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}
SZ = {5120: 1, 5121: 1, 5122: 2, 5123: 2, 5125: 4, 5126: 4}


def read(path):
    b = open(path, "rb").read()
    off, js, binc = 12, None, b""
    while off < len(b):
        ln, ty = struct.unpack("<II", b[off:off + 8])
        d = b[off + 8:off + 8 + ln]
        if ty == 0x4E4F534A:
            js = json.loads(d)
        elif ty == 0x004E4942:
            binc = d
        off += 8 + ln
    return js, binc


def raw(js, binc, i):
    a = js["accessors"][i]
    bv = js["bufferViews"][a["bufferView"]]
    el = SZ[a["componentType"]] * NC[a["type"]]
    start = bv.get("byteOffset", 0) + a.get("byteOffset", 0)
    stride = bv.get("byteStride", el)
    if stride == el:
        return binc[start:start + a["count"] * el]
    return b"".join(binc[start + k * stride:start + k * stride + el] for k in range(a["count"]))


def main(src, dst):
    js, binc = read(src)
    out = {"asset": js["asset"], "scene": js.get("scene", 0), "scenes": js["scenes"], "nodes": json.loads(json.dumps(js["nodes"])),
           "skins": js["skins"], "animations": json.loads(json.dumps(js["animations"])), "accessors": [], "bufferViews": []}
    chunks, pos = [], 0

    def add(data, acc):
        nonlocal pos
        pad = (-pos) % 4
        if pad:
            chunks.append(b"\0" * pad); pos += pad
        out["bufferViews"].append({"buffer": 0, "byteOffset": pos, "byteLength": len(data)})
        chunks.append(data); pos += len(data)
        acc = dict(acc); acc["bufferView"] = len(out["bufferViews"]) - 1
        out["accessors"].append(acc)
        return len(out["accessors"]) - 1

    def clone(i):
        a = js["accessors"][i]
        keep = {k: a[k] for k in ("componentType", "count", "type", "min", "max") if k in a}
        return add(raw(js, binc, i), keep)

    cache = {}
    def cl(i):
        if i not in cache:
            cache[i] = clone(i)
        return cache[i]

    for sk in out["skins"]:
        if "inverseBindMatrices" in sk:
            sk["inverseBindMatrices"] = cl(sk["inverseBindMatrices"])
    for an in out["animations"]:
        for s in an["samplers"]:
            s["input"] = cl(s["input"]); s["output"] = cl(s["output"])
    # one degenerate triangle bound to joint 0
    pos_acc = add(np.zeros((3, 3), "<f4").tobytes(), {"componentType": 5126, "count": 3, "type": "VEC3", "min": [0, 0, 0], "max": [0, 0, 0]})
    j_acc = add(np.zeros((3, 4), "<u2").tobytes(), {"componentType": 5123, "count": 3, "type": "VEC4"})
    w = np.zeros((3, 4), "<f4"); w[:, 0] = 1
    w_acc = add(w.tobytes(), {"componentType": 5126, "count": 3, "type": "VEC4"})
    i_acc = add(np.array([0, 1, 2], "<u2").tobytes(), {"componentType": 5123, "count": 3, "type": "SCALAR"})
    out["meshes"] = [{"name": "stub", "primitives": [{"attributes": {"POSITION": pos_acc, "JOINTS_0": j_acc, "WEIGHTS_0": w_acc}, "indices": i_acc}]}]
    for n in out["nodes"]:
        if "mesh" in n:
            n["mesh"] = 0
    blob = b"".join(chunks)
    blob += b"\0" * ((-len(blob)) % 4)
    out["buffers"] = [{"byteLength": len(blob)}]
    j = json.dumps(out, separators=(",", ":")).encode()
    j += b" " * ((-len(j)) % 4)
    total = 12 + 8 + len(j) + 8 + len(blob)
    open(dst, "wb").write(struct.pack("<III", 0x46546C67, 2, total) + struct.pack("<II", len(j), 0x4E4F534A) + j + struct.pack("<II", len(blob), 0x004E4942) + blob)


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
