"""Split a Tripo retarget batch GLB (one model, several preset animations) into one GLB per preset (YOK-246).
python tools/split_tripo_batch.py <model.glb> <outdir>   -> <outdir>/<preset>.glb
Edits only the glTF JSON chunk (keeps one animation, renames it); the binary chunk and the rig are untouched."""
import json, os, struct, sys
src, out = sys.argv[1], sys.argv[2]
os.makedirs(out, exist_ok=True)
b = open(src, "rb").read()
jl, _ = struct.unpack("<II", b[12:20])
j = json.loads(b[20:20 + jl])
rest = b[20 + jl:]
for a in j["animations"]:
    name = a["name"]
    k = dict(j)
    k["animations"] = [dict(a, name=name)]
    js = json.dumps(k, separators=(",", ":")).encode()
    js += b" " * (-len(js) % 4)
    blob = b"glTF" + struct.pack("<II", 2, 12 + 8 + len(js) + len(rest)) + struct.pack("<I", len(js)) + b"JSON" + js + rest
    open(os.path.join(out, name + ".glb"), "wb").write(blob)
    print("split", name)
