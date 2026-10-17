"""Uniform size change of a rigged fighter GLB, by patching one node scale (rules F5, F6; YOK-112).

The designer sets a fighter's size (Oni: 2.2 m, x1.1 on the 2.0 m Meshy rig, 2026-10-16). This writes a
byte copy of the rig where only the JSON chunk's `Armature` node scale changes (0.01 -> 0.01 * factor).
The bones stay in centimetres with their own rest lengths and the skeleton is untouched (names, parents,
skin order, rest rotations, inverse binds), so every shared clip still plays (F6). Anything mounted under
the skeleton (BoneAttachment3D: the Oni's club) scales with the fighter, so it keeps its size against the
hand. The binary chunk (mesh, skin, texture) is byte-identical; the input file is never written.

Clip note: the game keeps a clip's `Hips` height in bone space (FighterModel.BuildLibrary), so the scale
does not change the hip-track fix: scale Hips Y by this rig's rest Hips Y / the clip's rest Hips Y.

Run (plain Python 3, or Blender's: no bpy needed):
  python3 tools/blender/scale_rig.py --in <rig.glb> --out <rig-scaled.glb> --factor 1.1 [--node Armature] [--log <log.json>]
"""

import argparse
import hashlib
import json
import struct
import sys


def read_glb(path):
    data = open(path, "rb").read()
    magic, version, _length = struct.unpack("<III", data[:12])
    if magic != 0x46546C67 or version != 2:
        raise SystemExit(f"{path}: not a glTF 2 binary")
    jlen, jtype = struct.unpack("<II", data[12:20])
    if jtype != 0x4E4F534A:
        raise SystemExit(f"{path}: first chunk is not JSON")
    gltf = json.loads(data[20:20 + jlen])
    rest = data[20 + jlen:]  # BIN chunk (header + payload), kept byte for byte
    return data, gltf, rest


def write_glb(path, gltf, rest):
    j = json.dumps(gltf, separators=(",", ":")).encode("utf-8")
    j += b" " * ((4 - len(j) % 4) % 4)
    out = struct.pack("<II", len(j), 0x4E4F534A) + j + rest
    out = struct.pack("<III", 0x46546C67, 2, 12 + len(out)) + out
    with open(path, "wb") as f:
        f.write(out)
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--in", dest="inp", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--factor", type=float, required=True)
    ap.add_argument("--node", default="Armature")
    ap.add_argument("--log")
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    a = ap.parse_args(argv)
    if a.inp == a.out:
        raise SystemExit("refusing to overwrite the input")
    data, gltf, rest = read_glb(a.inp)
    nodes = [n for n in gltf["nodes"] if n.get("name") == a.node]
    if len(nodes) != 1:
        raise SystemExit(f"expected one node named {a.node}, found {len(nodes)}")
    node = nodes[0]
    if "matrix" in node:
        raise SystemExit(f"{a.node} uses a matrix; only TRS nodes are supported")
    before = node.get("scale", [1.0, 1.0, 1.0])
    node["scale"] = [s * a.factor for s in before]
    out = write_glb(a.out, gltf, rest)
    log = {
        "tool": "scale_rig.py", "in": a.inp, "out": a.out, "node": a.node, "factor": a.factor,
        "scale_before": before, "scale_after": node["scale"],
        "sha256_in": hashlib.sha256(data).hexdigest(), "sha256_out": hashlib.sha256(out).hexdigest(),
        "bin_chunk_identical": True,
    }
    print("scale_rig:", json.dumps(log))
    if a.log:
        json.dump(log, open(a.log, "w"), indent=1)


if __name__ == "__main__":
    main()
