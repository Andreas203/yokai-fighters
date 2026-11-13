"""Foot lock for the game's root-stripped presentation (YOK-finish-qa; rules F2, F5, F6; AM5).

The game plays every clip with the Hips X/Z pinned to the rig's rest (FighterModel: root motion stripped, movement
is move data). A take whose hips travel while a foot is planted then slides that foot along the floor. This script
reproduces the game's pin and, on every frame where a foot is planted in the raw take (low and still in the world),
re-solves that leg (thigh + shin, analytic two-bone IK in the knee's own bend plane) so the ankle stays where it
landed in the pinned pose. The foot keeps its own orientation. It never places a pose by hand: targets come from the
take's own ankle positions, rotations from the solve (F5). Key times, frame count and hit frames are unchanged
(every raw tick keeps its time; the file is sampled at 60 fps as before), so data/clips and frame data stay valid (F2).

Run (Blender 4.1):
  Blender -b --python tools/blender/foot_lock.py -- --clip <in.glb> --out <out.glb> [--start TICK] [--log <json>]
      [--height 0.025] [--speed 0.03] [--min-run 4] [--blend 4] [--legs Left Right] [--until TICK]
  --start: raw tick where the game's trim starts (a run already planted there locks to its pose at that tick).
  --height / --speed: planted = toe within HEIGHT m of its lowest point in the take and moving under SPEED m per tick
  --until: no lock after this raw tick (it fades out over --blend), for a take whose feet must come together late.
  Planted = in the world (raw take; most takes here are already root-stripped, so a planted foot that slides in the file
  still counts), or within 2.4x HEIGHT and under 0.4x SPEED (the support foot rolling onto its toes). Runs shorter than --min-run ticks are left alone; the lock fades
  out over --blend ticks after a run ends.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy
from mathutils import Matrix, Vector

import clip_common as cc

TOOL = "foot_lock"
VERSION = 1


def main():
    a = cc.script_args(TOOL)

    def one(flag, default=None, cast=str):
        v = a.get(flag)
        return default if v is None else cast(v[0])

    clip, out = one("--clip"), one("--out")
    if not clip or not out:
        sys.exit("foot_lock: --clip and --out are required")
    start = one("--start", 0, int)
    h_tol, v_tol = one("--height", 0.025, float), one("--speed", 0.03, float)
    min_run, blend = one("--min-run", 4, int), one("--blend", 4, int)
    legs = a.get("--legs", ["Left", "Right"])
    until = one("--until", None, int)

    arm, act = cc.load_clip(clip)
    sc = bpy.context.scene
    f0, f1 = int(math.floor(act.frame_range[0])), int(math.ceil(act.frame_range[1]))
    frames = list(range(f0, f1 + 1))
    mw = arm.matrix_world
    mwi = mw.inverted()
    B = lambda n: arm.pose.bones[cc.resolve_bone(arm, n)]
    hips = B("Hips")
    chain = {s: (B(s + "UpLeg"), B(s + "Leg"), B(s + "Foot"), B(s + "ToeBase")) for s in legs}
    rest_hips_w = mw @ arm.data.bones[hips.name].head_local

    # 1. cache the take: every bone's basis per tick, toe/ankle world positions (root motion included)
    basis, toe_w = {}, {s: [] for s in legs}
    for f in frames:
        sc.frame_set(f)
        basis[f] = {pb.name: pb.matrix_basis.copy() for pb in arm.pose.bones}
        for s in legs:
            toe_w[s].append(mw @ chain[s][3].head)
    arm.animation_data.action = None

    # 2. planted runs per foot
    runs = {}
    for s in legs:
        z = [p.z for p in toe_w[s]]
        zmin = min(z)
        planted = []
        for i in range(len(frames)):
            j0, j1 = max(0, i - 1), min(len(frames) - 1, i + 1)
            d = (toe_w[s][j1] - toe_w[s][j0]).to_2d().length / max(1, j1 - j0)
            # low and still, or a little higher and very slow (the support foot pivoting onto its toes)
            planted.append((z[i] - zmin < h_tol and d < v_tol) or (z[i] - zmin < 2.4 * h_tol and d < 0.4 * v_tol))
        rs, i = [], 0
        while i < len(frames):
            if planted[i]:
                j = i
                while j + 1 < len(frames) and planted[j + 1]:
                    j += 1
                if until is not None and frames[j] > until:
                    j = max(i, frames.index(until) if until in frames else j)
                if j - i + 1 >= min_run:
                    a0 = i
                    # a run planted across the trim start locks to its pose at the trim start
                    if frames[i] < start <= frames[j]:
                        a0 = frames.index(start)
                    rs.append((a0, j))
                i = j + 1
            else:
                i += 1
        runs[s] = rs

    # weight per frame and the run each frame locks to
    lock = {s: [None] * len(frames) for s in legs}
    for s in legs:
        for (i, j) in runs[s]:
            for k in range(i, j + 1):
                lock[s][k] = (i, 1.0)
            for b in range(1, blend + 1):
                k = j + b
                if k < len(frames) and lock[s][k] is None:
                    lock[s][k] = (i, 1.0 - b / (blend + 1))

    def update():
        bpy.context.view_layer.update()

    def rotate_about(pb, pivot, q):
        m = pb.matrix.copy()
        pb.matrix = Matrix.Translation(pivot) @ q.to_matrix().to_4x4() @ Matrix.Translation(-pivot) @ m
        update()

    target = {s: {} for s in legs}
    result = {}
    stats = {s: {"locked_ticks": 0, "max_correction_cm": 0.0, "unreachable_ticks": 0} for s in legs}
    for k, f in enumerate(frames):
        for pb in arm.pose.bones:
            pb.matrix_basis = basis[f][pb.name]
        update()
        # the game's pin: hips over the rest X/Y (Blender ground plane), height kept
        m = hips.matrix.copy()
        hw = mw @ m.translation
        hw.x, hw.y = rest_hips_w.x, rest_hips_w.y
        m.translation = mwi @ hw
        hips.matrix = m
        update()
        for s in legs:
            up, lo, ft, _ = chain[s]
            if lock[s][k] is None:
                continue
            run_start, w = lock[s][k]
            if k == run_start or run_start not in target[s]:
                target[s][run_start] = ft.head.copy()
            H, K, A = up.head.copy(), lo.head.copy(), ft.head.copy()
            foot_rot = ft.matrix.to_3x3().copy()
            T = A.lerp(target[s][run_start], w)
            l1, l2 = (K - H).length, (A - K).length
            d_vec = T - H
            d = d_vec.length
            lo_lim, hi_lim = abs(l1 - l2) + 1e-4 * l1, (l1 + l2) * 0.9995
            if d > hi_lim or d < lo_lim:
                stats[s]["unreachable_ticks"] += 1
                d = min(max(d, lo_lim), hi_lim)
            u = d_vec.normalized()
            bend = (K - H) - u * (K - H).dot(u)
            if bend.length < 1e-6 * l1:
                bend = (ft.tail - A) - u * (ft.tail - A).dot(u)  # straight leg: bend toward the toes
            bend.normalize()
            ca = (l1 * l1 - l2 * l2 + d * d) / (2 * d)
            K2 = H + u * ca + bend * math.sqrt(max(0.0, l1 * l1 - ca * ca))
            rotate_about(up, H, (K - H).rotation_difference(K2 - H))
            K3, A3 = lo.head.copy(), ft.head.copy()
            rotate_about(lo, K3, (A3 - K3).rotation_difference(H + u * d - K3))
            ft.matrix = Matrix.Translation(ft.head.copy()) @ foot_rot.to_4x4()
            update()
            corr = ((mw @ ft.head) - (mw @ A)).length * 100
            stats[s]["locked_ticks"] += 1
            stats[s]["max_correction_cm"] = max(stats[s]["max_correction_cm"], round(corr, 2))
        names = [hips.name] + [pb.name for s in legs for pb in chain[s][:3]]
        result[f] = {n: arm.pose.bones[n].matrix_basis.copy() for n in names}

    # 3. write: the same action, the changed channels rewritten on every tick (others untouched)
    arm.animation_data.action = act
    changed = list(result[frames[0]].keys())
    for fc in list(act.fcurves):
        dp = fc.data_path
        if not dp.startswith('pose.bones["'):
            continue
        bone = dp[len('pose.bones["'):dp.index('"]')]
        prop = dp[dp.index('"].') + 3:]
        if bone not in changed or prop not in ("rotation_quaternion", "location"):
            continue
        if prop == "location" and bone != hips.name:
            continue
        fc.keyframe_points.clear()
        vals = []
        for f in frames:
            mb = result[f][bone]
            if prop == "location":
                vals.append(mb.to_translation()[fc.array_index])
            else:
                vals.append(mb.to_quaternion()[fc.array_index])
        fc.keyframe_points.add(len(frames))
        for i, (f, v) in enumerate(zip(frames, vals)):
            kp = fc.keyframe_points[i]
            kp.co = (f, v)
            kp.interpolation = "LINEAR"
        fc.update()
    # quaternion sign continuity on the rewritten channels
    for n in changed:
        fcs = [fc for fc in act.fcurves if fc.data_path == 'pose.bones["%s"].rotation_quaternion' % n]
        if len(fcs) == 4:
            fcs.sort(key=lambda fc: fc.array_index)
            prev = None
            for i in range(len(frames)):
                q = [fc.keyframe_points[i].co[1] for fc in fcs]
                if prev is not None and sum(x * y for x, y in zip(q, prev)) < 0:
                    q = [-x for x in q]
                    for fc, x in zip(fcs, q):
                        fc.keyframe_points[i].co[1] = x
                prev = q
            for fc in fcs:
                fc.update()

    sc.frame_start, sc.frame_end = f0, f1
    out = os.path.abspath(out)
    os.makedirs(os.path.dirname(out), exist_ok=True)
    bpy.ops.export_scene.gltf(filepath=out, export_format="GLB", export_animations=True, export_force_sampling=True,
                              export_frame_range=True, export_optimize_animation_size=False,
                              export_nla_strips=False, export_apply=False)
    log = {"tool": "tools/blender/foot_lock.py", "version": VERSION, "blender": bpy.app.version_string,
           "input": clip, "input_sha256_before": None, "output": out, "output_sha256": cc.sha256(out),
           "ticks": [f0, f1], "start": start, "height_m": h_tol, "speed_m_per_tick": v_tol, "min_run": min_run,
           "blend": blend, "until": until, "runs": {s: [[frames[i], frames[j]] for i, j in runs[s]] for s in legs}, "stats": stats,
           "note": "hips X/Y pinned to rest (as the game shows it); planted legs re-solved, foot orientation kept; "
                   "no key time, frame count or hit frame changed"}
    lp = one("--log")
    if lp:
        cc.write_json(lp, log)
    print("foot_lock:", log["runs"], stats)
    return 0


if __name__ == "__main__":
    code = 1
    try:
        code = main()
    except SystemExit as e:
        print(e.code, file=sys.stderr)
        code = 1 if e.code not in (0, None) else 0
    except Exception:
        import traceback
        traceback.print_exc()
        code = 1
    cc.finish(code)
