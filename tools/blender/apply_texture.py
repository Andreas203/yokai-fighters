"""Write a base-colour PNG into a rigged GLB and set baseColorFactor to 1.0, at glTF level (YOK-146, phase B).

Plain Python, no Blender. Only the material's `baseColorFactor` and the image data change: nodes, skins, bones,
weights, UVs, accessors and every other bufferView are copied byte for byte. The old image bytes are dropped, so
the file gets smaller. Use it after any weight or rig repair overwrites `ryo-tripo-rigged.glb`:

  python tools/blender/apply_texture.py --glb game/assets/generated/characters/ryo/ryo-tripo-rigged.glb \
      --png game/assets/generated/characters/ryo/ryo-tripo-basecolor.png [--out <other.glb>]

Without --out the GLB is rewritten in place. Needs the GLB's material to have a baseColorTexture (Tripo's does).
"""
import argparse
import json
import struct
import sys


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--glb", required=True)
    ap.add_argument("--png", required=True)
    ap.add_argument("--out")
    ap.add_argument("--factor", type=float, default=1.0)
    a = ap.parse_args()
    b = open(a.glb, "rb").read()
    magic, ver, _ = struct.unpack("<4sII", b[:12])
    if magic != b"glTF" or ver != 2:
        sys.exit("apply_texture: not a GLB 2.0 file")
    jl, jt = struct.unpack("<I4s", b[12:20])
    j = json.loads(b[20:20 + jl])
    bl, bt = struct.unpack("<I4s", b[20 + jl:28 + jl])
    if bt != b"BIN\x00":
        sys.exit("apply_texture: no BIN chunk")
    binb = b[28 + jl:28 + jl + bl]
    png = open(a.png, "rb").read()
    if png[:8] != b"\x89PNG\r\n\x1a\n":
        sys.exit("apply_texture: %s is not a PNG" % a.png)

    mats = j["materials"]
    if len(mats) != 1:
        sys.exit("apply_texture: expected one material, found %d" % len(mats))
    pbr = mats[0]["pbrMetallicRoughness"]
    tex = j["textures"][pbr["baseColorTexture"]["index"]]
    img = j["images"][tex["source"]]
    iv = img["bufferView"]
    pbr["baseColorFactor"] = [a.factor, a.factor, a.factor, 1.0]
    img["mimeType"] = "image/png"

    # rebuild the BIN chunk: same bytes for every view, in order, 4-byte aligned, new image bytes for the image view
    out = bytearray()
    for i, v in enumerate(j["bufferViews"]):
        data = png if i == iv else binb[v.get("byteOffset", 0):v.get("byteOffset", 0) + v["byteLength"]]
        while len(out) % 4:
            out.append(0)
        v["byteOffset"] = len(out)
        v["byteLength"] = len(data)
        out += data
    while len(out) % 4:
        out.append(0)
    j["buffers"][0]["byteLength"] = len(out)
    js = json.dumps(j, separators=(",", ":")).encode("utf-8")
    js += b" " * (-len(js) % 4)
    total = 12 + 8 + len(js) + 8 + len(out)
    with open(a.out or a.glb, "wb") as f:
        f.write(struct.pack("<4sII", b"glTF", 2, total))
        f.write(struct.pack("<I4s", len(js), b"JSON") + js)
        f.write(struct.pack("<I4s", len(out), b"BIN\x00") + bytes(out))
    print("apply_texture: wrote %s (%d bytes, image view %d, baseColorFactor %.2f)" % (a.out or a.glb, total, iv, a.factor))


main()
