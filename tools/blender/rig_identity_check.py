"""Compare two rig GLBs and prove they differ only in the hands (closed-fist bake, 2026-11-08; rules F5, F6).

Plain Python (numpy), no Blender. Checks, and writes to --log:
  - every glTF JSON entry other than accessors is equal (nodes, skins, meshes, materials, images, ...);
  - every accessor is equal in metadata (POSITION min/max excepted) and in data, except POSITION and NORMAL;
  - JOINTS_0 / WEIGHTS_0 (after the weight repair), TEXCOORD_0 and the inverse bind matrices are byte-equal;
  - the vertices whose POSITION or NORMAL differ all carry weight on --hand-bones in the reference rig and lie
    within --radius of a hand joint, and every vertex outside that set is bit-identical.
With --morph <baked-fist.glb> (fist as a blend shape, designer 2026-11-09; fist_morph.py) the candidate must instead
be the reference plus one morph target and nothing else:
  - every glTF JSON entry is equal except meshes[0], which may only add primitives[0].targets, weights and
    extras.targetNames (= [--morph-name]);
  - the reference's accessors and bufferViews are the candidate's first ones, equal in metadata and bytes
    (POSITION and NORMAL included); exactly the two target accessors are appended;
  - base + target equals the baked fist (POSITION and NORMAL) within --tol, the nonzero target rows are exactly the
    rows the baked fist moved, and they all lie in the hand region.
Exit 0 PASS, 2 FAIL.

  python tools/blender/rig_identity_check.py --ref <canonical.glb> --cand <candidate.glb> [--log <out.json>]
      [--hand-bones LeftHand,RightHand] [--radius 0.20] [--morph <baked-fist.glb> [--morph-name fist] [--tol 1e-6]]
"""
import argparse
import json
import struct
import sys

import numpy as np


def load(path):
    b = open(path, "rb").read()
    ln = struct.unpack("<I", b[12:16])[0]
    j = json.loads(b[20:20 + ln])
    return b, j, 20 + ln + 8


def acc(g, i):
    b, j, off = g
    a = j["accessors"][i]
    bv = j["bufferViews"][a["bufferView"]]
    n = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}[a["type"]]
    dt = {5126: np.float32, 5123: np.uint16, 5121: np.uint8, 5125: np.uint32}[a["componentType"]]
    return np.frombuffer(b, dt, a["count"] * n, off + bv.get("byteOffset", 0) + a.get("byteOffset", 0)).reshape(
        a["count"], n)


def hand_set(R, a):
    """Vertices with any weight on the hand bones in the reference, within --radius of a hand joint."""
    prim = R[1]["meshes"][0]["primitives"][0]["attributes"]
    sk = R[1]["skins"][0]
    jn = [R[1]["nodes"][n]["name"] for n in sk["joints"]]
    hand_ids = [jn.index(h) for h in a.hand_bones.split(",")]
    J, W = acc(R, prim["JOINTS_0"]), acc(R, prim["WEIGHTS_0"]).astype(float)
    if R[1]["accessors"][prim["WEIGHTS_0"]].get("normalized"):
        W = W / float(np.iinfo(W.dtype).max if W.dtype != np.float32 else 1)
    on_hand = np.any(np.isin(J, hand_ids) & (W > 0), axis=1)
    P = acc(R, prim["POSITION"]).astype(float)
    ibm = acc(R, sk["inverseBindMatrices"]).reshape(-1, 4, 4).transpose(0, 2, 1).astype(float)
    heads = np.linalg.inv(ibm)[:, :3, 3]
    dist = np.min(np.linalg.norm(P[:, None, :] - heads[hand_ids][None], axis=2), axis=1)
    return on_hand & (dist <= a.radius), dist


def morph_check(a, R, C, F):
    rep, fails = {"ref": a.ref, "cand": a.cand, "morph_fist": a.morph, "mode": "morph"}, []
    rj, cj = R[1], C[1]
    rep["json_keys_differing"] = [k for k in sorted(set(rj) | set(cj))
                                  if k not in ("accessors", "bufferViews", "buffers", "meshes")
                                  and rj.get(k) != cj.get(k)]
    if rep["json_keys_differing"]:
        fails.append("glTF JSON differs: %s" % rep["json_keys_differing"])
    cm = json.loads(json.dumps(cj["meshes"]))
    targets = cm[0]["primitives"][0].pop("targets", None)
    weights = cm[0].pop("weights", None)
    names = cm[0].get("extras", {}).pop("targetNames", None)
    if cm[0].get("extras") == {}:
        cm[0].pop("extras")
    if cm != rj["meshes"]:
        fails.append("meshes differ beyond targets/weights/targetNames")
    if not targets or len(targets) != 1 or names != [a.morph_name] or weights is None or len(weights) != 1:
        fails.append("expected one target named %r with one default weight" % a.morph_name)
        targets = targets or [{}]
    rep.update({"target_names": names, "default_weights": weights, "target": targets[0]})
    n_acc, n_bv = len(rj["accessors"]), len(rj["bufferViews"])
    if cj["accessors"][:n_acc] != rj["accessors"] or len(cj["accessors"]) != n_acc + 2:
        fails.append("reference accessors not kept as the first ones, or not exactly two appended")
    if cj["bufferViews"][:n_bv] != rj["bufferViews"]:
        fails.append("reference bufferViews not kept as the first ones")
    else:
        for vi, bv in enumerate(rj["bufferViews"]):
            o, n = bv.get("byteOffset", 0), bv["byteLength"]
            if R[0][R[2] + o:R[2] + o + n] != C[0][C[2] + o:C[2] + o + n]:
                fails.append("bufferView %d bytes differ" % vi)
    rep["reference_bytes_identical"] = not any("bufferView" in f or "accessors" in f for f in fails)
    if set(targets[0]) != {"POSITION", "NORMAL"} or sorted(targets[0].values()) != [n_acc, n_acc + 1]:
        fails.append("target must be POSITION and NORMAL on the two appended accessors")
        return finish(a, rep, fails)
    prim = rj["meshes"][0]["primitives"][0]["attributes"]
    fprim = F[1]["meshes"][0]["primitives"][0]["attributes"]
    hand_region, dist = hand_set(R, a)
    moved = np.zeros(len(hand_region), bool)
    for key in ("POSITION", "NORMAL"):
        base, d = acc(C, prim[key]).astype(np.float64), acc(C, targets[0][key]).astype(np.float64)
        fist, ref = acc(F, fprim[key]).astype(np.float64), acc(R, prim[key]).astype(np.float64)
        nz, fmoved = np.any(d != 0, axis=1), np.any(fist != ref, axis=1)
        moved |= nz
        rep[key.lower()] = {
            "rows_nonzero": int(nz.sum()), "rows_baked_fist_moved": int(fmoved.sum()),
            "max_err_weight1_vs_baked_fist": float(np.abs(base + d - fist).max()),
            "max_err_weight0_vs_reference": float(np.abs(base - ref).max()),
            "max_abs_delta": round(float(np.abs(d).max()), 5)}
        if rep[key.lower()]["max_err_weight1_vs_baked_fist"] > a.tol:
            fails.append("%s at weight 1 differs from the baked fist" % key)
        if (nz & ~fmoved).any():
            fails.append("%s target nonzero on rows the baked fist did not move" % key)
    rep.update({"rows_nonzero_any": int(moved.sum()), "hand_region_verts": int(hand_region.sum()),
                "nonzero_rows_outside_hand_region": int((moved & ~hand_region).sum()),
                "nonzero_max_dist_from_hand_joint_m": round(float(dist[moved].max()), 4) if moved.any() else 0.0})
    if rep["nonzero_rows_outside_hand_region"]:
        fails.append("%d target rows lie outside the hand region" % rep["nonzero_rows_outside_hand_region"])
    return finish(a, rep, fails)


def finish(a, rep, fails):
    rep["verdict"] = "FAIL" if fails else "PASS"
    rep["fails"] = fails
    print("rig_identity_check:", json.dumps(rep))
    if a.log:
        with open(a.log, "w", encoding="utf-8") as f:
            json.dump(rep, f, indent=1)
    sys.exit(2 if fails else 0)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--ref", required=True)
    ap.add_argument("--cand", required=True)
    ap.add_argument("--log")
    ap.add_argument("--hand-bones", default="LeftHand,RightHand")
    ap.add_argument("--radius", type=float, default=0.20)
    ap.add_argument("--morph")
    ap.add_argument("--morph-name", default="fist")
    ap.add_argument("--tol", type=float, default=1e-6)
    a = ap.parse_args()
    R, C = load(a.ref), load(a.cand)
    if a.morph:
        return morph_check(a, R, C, load(a.morph))
    rep, fails = {"ref": a.ref, "cand": a.cand}, []
    rep["json_keys_differing"] = [k for k in sorted(set(R[1]) | set(C[1]))
                                  if k not in ("accessors", "bufferViews", "buffers") and R[1].get(k) != C[1].get(k)]
    if rep["json_keys_differing"]:
        fails.append("glTF JSON differs: %s" % rep["json_keys_differing"])
    prim = R[1]["meshes"][0]["primitives"][0]["attributes"]
    pos_i, nrm_i = prim["POSITION"], prim["NORMAL"]
    rows = {}
    for i, (ra, ca) in enumerate(zip(R[1]["accessors"], C[1]["accessors"])):
        meta = {k: v for k, v in ra.items() if not (i == pos_i and k in ("min", "max"))}
        cmeta = {k: v for k, v in ca.items() if not (i == pos_i and k in ("min", "max"))}
        if meta != cmeta:
            fails.append("accessor %d metadata differs" % i)
            continue
        x, y = acc(R, i), acc(C, i)
        diff = np.any(x != y, axis=1)
        if i in (pos_i, nrm_i):
            rows[i] = diff
        elif diff.any():
            fails.append("accessor %d (%s) differs in %d rows" % (i, ra.get("name", ""), int(diff.sum())))
    if len(R[1]["accessors"]) != len(C[1]["accessors"]):
        fails.append("accessor count differs")
    skip_views = {R[1]["accessors"][pos_i]["bufferView"], R[1]["accessors"][nrm_i]["bufferView"]}
    if R[1]["bufferViews"] != C[1]["bufferViews"]:
        fails.append("bufferView layout differs")
    else:
        for vi, bv in enumerate(R[1]["bufferViews"]):
            o, n = bv.get("byteOffset", 0), bv["byteLength"]
            if vi not in skip_views and R[0][R[2] + o:R[2] + o + n] != C[0][C[2] + o:C[2] + o + n]:
                fails.append("bufferView %d bytes differ (texture, weights or skin data)" % vi)
    rep["bufferviews_byte_equal_except_position_normal"] = not any("bufferView" in f for f in fails)
    changed = rows.get(pos_i, 0) | rows.get(nrm_i, 0)
    hand_region, dist = hand_set(R, a)
    P = acc(R, pos_i).astype(float)
    rep.update({
        "position_rows_changed": int(rows.get(pos_i, np.zeros(1, bool)).sum()),
        "normal_rows_changed": int(rows.get(nrm_i, np.zeros(1, bool)).sum()),
        "changed_rows_outside_hand_region": int((changed & ~hand_region).sum()),
        "hand_region_verts": int(hand_region.sum()),
        "changed_max_dist_from_hand_joint_m": round(float(dist[changed].max()), 4) if changed.any() else 0.0,
        "unchanged_rows_bit_identical": int((~changed).sum()),
        "max_position_change_m": round(float(np.abs(acc(C, pos_i).astype(float) - P).max()), 4),
        "position_min_max_ref": [R[1]["accessors"][pos_i]["min"], R[1]["accessors"][pos_i]["max"]],
        "position_min_max_cand": [C[1]["accessors"][pos_i]["min"], C[1]["accessors"][pos_i]["max"]],
    })
    if rep["changed_rows_outside_hand_region"]:
        fails.append("%d changed vertices lie outside the hand region" % rep["changed_rows_outside_hand_region"])
    finish(a, rep, fails)


main()
