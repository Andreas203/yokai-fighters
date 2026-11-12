"""One-shot clip pipeline (YOK-246, rules F2, F5, F6, G4, T0): per entry, clean -> retarget -> measure -> prop check -> data/clips.

Run from the repo root with the system Python (it drives Blender 4.1 headless, one process per step):
  python tools/finish_clips.py --manifest tools/finish_clips_manifest.json [--only ID ...] [--steps clean,retarget,measure,prop,data]
      [--dry-run] [--blender <blender.exe>]
Writes assets/specs/clips/finish-pipeline-summary.md (one row per entry) and per-step logs under
assets/specs/clips/finish-logs/. Nothing is hand-keyed: clean_clip.py filters and trims existing keys, retarget_bvh.py maps
rotations by chain position, measure_clip.py reports, and the retime curve is computed from the measured hit window.

Manifest entry (JSON, one object per output clip):
  id            clip id, also the data/clips/<id>.json name and the file name
  body          kitsune | oni | kappa | ryo        (ryo = Ryo's Tripo skeleton, also his Tanuki twins)
  src           a GLB on the Tripo skeleton (an existing clips/ryo-tripo file) ... or
  preset        a Tripo preset name; the raw take is found under game/assets/generated/clips/tripo-takes/*/ (file name contains it)
  src_skeleton  "tripo" (default; the Tripo API rig and clips/ryo-tripo share names)
  clean         true to run clean_clip.py on src first (raw Tripo takes); false for files already clean (default false)
  copy_from     data/clips id whose trim, speed_scale, retime and frames_total this entry keeps (fallback poses: same preset, same ticks)
  target        {"startup": S, "active": A, "total": T}  hit moves of a take: the retime curve is built from the measured hit window
  movement      true for walks, dashes, get-ups and hit reactions (slide is reported, not judged)
  prop          "shell" (Kappa) or "club" (Oni), the clearance check to run
  source/clip   data/clips "source" (default tripo-library) and "clip" (default preset name)
"""
import argparse
import glob
import json
import os
import shutil
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
GAME = os.path.join(ROOT, "game")
RIGS = {
    "kitsune": "assets/generated/characters/kitsune/kitsune-rigged.glb",
    "oni": "assets/generated/characters/oni/oni-rigged.glb",
    "kappa": "assets/generated/characters/kappa/kappa-rigged.glb",
    "ryo": "assets/generated/characters/ryo/ryo-tripo-rigged.glb",
}
RYO_MESHY_RIG = "assets/generated/characters/ryo/ryo-rigged.glb"  # catalog convention: a Ryo twin's data/clips file is the Meshy-skeleton file in clips/ryo/, the Tripo Ryo plays clips/ryo-tripo/ by file name
CLIP_DIR = {"kitsune": "kitsune", "oni": "oni", "kappa": "kappa", "ryo": "ryo-tripo"}
PROPS = {  # prop_clearance.py arguments: prop GLB, its prep_prop log, grip radius
    "shell": ("assets/generated/characters/kappa/shell.glb", "assets/specs/rigs/logs/kappa-shell-fit.json", "0.0"),
    "club": ("assets/generated/characters/oni/club-grip.glb", "assets/specs/rigs/logs/oni-club-grip.json", "0.12"),
}
LOGS = os.path.join(ROOT, "assets/specs/clips/finish-logs")


def find_blender(given):
    for c in [given, os.environ.get("BLENDER"), "C:/Program Files/Blender Foundation/Blender 4.1/blender.exe",
              "/Applications/Blender.app/Contents/MacOS/Blender"]:
        if c and os.path.exists(c):
            return c
    sys.exit("finish_clips: Blender not found (pass --blender)")


def run(cmd, log, dry):
    os.makedirs(LOGS, exist_ok=True)
    if dry:
        print("DRY", " ".join(cmd))
        return 0, ""
    p = subprocess.run(cmd, cwd=ROOT, capture_output=True, text=True)
    open(os.path.join(LOGS, log), "w", encoding="utf8").write(p.stdout + p.stderr)
    return p.returncode, p.stdout + p.stderr


def build_retime(report, target, n):
    """Retime curve (F2, AM5) from the measured strike, in ticks of the cleaned take (tick = frame - 1, 60 per second).
    The active segment is centred on the strike peak, startup snaps (up to 2x) into it, recovery eases out (up to 2x).
    Returns (frames_total, retime, flags, (first_tick, last_tick))."""
    S, A, T = target["startup"], target["active"], target["total"]
    h = report.get("hit", {}).get("suggested") or {}
    flags = []
    if not h.get("peak_frame"):
        return T, [{"frames": [1, T], "ticks": [0, n], "note": "one segment, strike not measured"}], ["no strike measured"], (0, n)
    p = h["peak_frame"] - 1
    L = h["hit_end"] - h["hit_start"] + 1
    W = max((A + 1) // 2, min(2 * A, L))
    a0 = max(0, p - W // 2)
    a1 = a0 + W
    s0 = max(0, a0 - 2 * S)
    if a0 - s0 < (S + 1) // 2:
        flags.append("strike at tick %d leaves only %d startup ticks" % (a0, a0 - s0))
    R = T - (S + A + 1)  # frames in the recovery segment
    r = min(n - a1, 2 * R) if R > 0 else 0
    if R > 0 and r < (R + 1) // 2:
        flags.append("only %d ticks after the strike for %d recovery frames" % (r, R))
    segs = [{"frames": [1, S + 1], "ticks": [s0, a0], "note": "startup snaps into the strike"},
            {"frames": [S + 1, S + A + 1], "ticks": [a0, a1], "note": "strike pose through the active frames"}]
    if R > 0:
        segs.append({"frames": [S + A + 1, T], "ticks": [a1, a1 + r], "note": "recovery eases out"})
    return T, segs, flags, (s0, segs[-1]["ticks"][1])


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--manifest", default="tools/finish_clips_manifest.json")
    ap.add_argument("--only", nargs="*")
    ap.add_argument("--steps", default="clean,retarget,measure,prop,data")
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--blender")
    a = ap.parse_args()
    steps = set(a.steps.split(","))
    blender = find_blender(a.blender)
    entries = json.load(open(os.path.join(ROOT, a.manifest), encoding="utf8"))["entries"]
    rows = []
    for e in entries:
        if a.only and e["id"] not in a.only:
            continue
        cid, body = e["id"], e["body"]
        row = {"id": cid, "body": body, "clean": "-", "retarget": "-", "verdict": "-", "slide": "-", "frames": "-", "hit": "-", "prop": "-", "notes": []}
        rows.append(row)
        src = e.get("src")
        if not src:
            hits = [p for p in glob.glob(os.path.join(GAME, "assets/generated/clips/tripo-takes/**/*.glb"), recursive=True)
                    if e["preset"] in os.path.basename(p) and "rig" not in os.path.basename(p).lower().replace("rigged", "")]
            if not hits:
                row["notes"].append("take not landed")
                continue
            src = os.path.relpath(hits[0], GAME).replace("\\", "/")
        work = os.path.join(ROOT, "tmp-finish", cid)
        os.makedirs(work, exist_ok=True)
        cur = src
        trim_n = None
        if e.get("clean") and "clean" in steps:
            out = "tmp-finish/%s/clean.glb" % cid
            cmd = [blender, "-b", "--python", "tools/blender/clean_clip.py", "--", "--clip", os.path.join("game", cur),
                   "--out", out, "--strip-root", "horizontal"] + (["--trim", *map(str, e["trim"])] if e.get("trim") else [])
            rc, _ = run(cmd, cid + ".clean.log", a.dry_run)
            row["clean"] = "ok" if rc == 0 else "FAIL"
            cur = "../" + out  # relative to game/
        # retarget onto the body's skeleton
        dest = "assets/generated/clips/%s/%s.glb" % (CLIP_DIR[body], cid)
        if "retarget" in steps:
            cmd = [blender, "-b", "--python", "tools/blender/retarget_bvh.py", "--", "--clip",
                   os.path.normpath(os.path.join("game", cur)), "--src-skeleton", e.get("src_skeleton", "tripo"),
                   "--rig", "game/" + RIGS[body], "--out", "game/" + dest, "--log", "tmp-finish/%s/retarget.json" % cid]
            rc, _ = run(cmd, cid + ".retarget.log", a.dry_run)
            row["retarget"] = "ok" if rc == 0 else "FAIL"
            if body == "ryo":
                meshy_dest = "assets/generated/clips/ryo/%s.glb" % cid
                cmd2 = cmd[:cmd.index("--rig")] + ["--rig", "game/" + RYO_MESHY_RIG, "--out", "game/" + meshy_dest, "--log", "tmp-finish/%s/retarget-meshy.json" % cid]
                rc2, _ = run(cmd2, cid + ".retarget-meshy.log", a.dry_run)
                row["retarget"] += " + meshy %s" % ("ok" if rc2 == 0 else "FAIL")
        # data template
        template = {}
        if e.get("copy_from"):
            template = json.load(open(os.path.join(ROOT, "data/clips", e["copy_from"] + ".json"), encoding="utf8"))
        data_path = os.path.join(ROOT, "data/clips", cid + ".json")
        base = dict(template)
        base.update({"kind": "clip", "id": cid, "status": "proposed", "file": ("assets/generated/clips/ryo/%s.glb" % cid) if body == "ryo" else dest, "hit_start": template.get("hit_start"),
                     "hit_end": template.get("hit_end"), "rules": ["F2", "F5", "F6"]})
        base.setdefault("source", e.get("source", "tripo-library"))
        base["source"] = e.get("source", "tripo-library" if not e.get("copy_from") else "meshy-library")
        base["clip"] = e.get("clip", e.get("preset", template.get("clip", cid)))
        base.setdefault("frames_total", 1)
        base.setdefault("retarget_notes", "")
        base.setdefault("notes", "")
        if "data" in steps or "measure" in steps:
            if not a.dry_run:
                json.dump(base, open(data_path, "w", encoding="utf8"), indent=2)
                open(data_path, "a").write("\n")
        rep = {}
        if "measure" in steps:
            outdir = "tmp-finish/%s/measure" % cid
            cmd = [blender, "-b", "--python", "tools/blender/measure_clip.py", "--", "--clip", "game/" + dest,
                   "--data", "data/clips/%s.json" % cid, "--id", cid, "--out", outdir,
                   "--stills-dir", "assets/specs/clips/stills/finish"]
            if e.get("movement"):
                cmd.append("--movement-clip")
            rc, _ = run(cmd, cid + ".measure.log", a.dry_run)
            rp = os.path.join(ROOT, outdir, cid + ".report.json")
            if rc == 0 and os.path.exists(rp):
                rep = json.load(open(rp, encoding="utf8"))
                row["verdict"] = rep.get("verdict", {}).get("state", "?")
                slides = [f.get("worst_run_mean_cm_per_frame", 0) for f in rep.get("planted_feet", {}).get("feet", {}).values()]
                row["slide"] = "%.2f" % max(slides) if slides else "-"
                row["frames"] = rep.get("frames_total", "-")
                h = rep.get("hit", {}).get("suggested")
                row["hit"] = "%s-%s" % (h.get("hit_start"), h.get("hit_end")) if h else "-"
            else:
                row["verdict"] = "FAIL" if not a.dry_run else "-"
        if "data" in steps and not a.dry_run:
            if e.get("pose_frames") and rep:  # a pose clip: the take from its start over the pose's frame count, at most 2x (the tail is cut)
                n = rep.get("frames_total", 1) - 1
                F = e["pose_frames"]
                sp = min(2.0, n / (F - 1))
                t1 = min(n, int(round(sp * (F - 1))))
                base.update({"trim_start": 0, "trim_end": t1, "frames_total": F, "hit_start": None, "hit_end": None,
                             "retime": [{"frames": [1, F], "ticks": [0, t1], "note": "one segment at %.2fx from the take's start" % (t1 / (F - 1))}]})
                base.pop("speed_scale", None)
                row["frames"] = "%d (trim 0-%d of %d)" % (F, t1, n)
                if t1 / (F - 1) < 0.5:
                    row["notes"].append("pose speed %.2fx below 0.5x" % (t1 / (F - 1)))
            elif e.get("target") and rep:
                n = rep.get("frames_total", 1) - 1
                T, segs, flags, (t0, t1) = build_retime(rep, e["target"], n)
                if e.get("retime_override"):  # chosen window (e.g. the Fox Mirage's flip, away from the upright stand that tops the camera framing)
                    segs = e["retime_override"]
                    t0, t1 = segs[0]["ticks"][0], segs[-1]["ticks"][1]
                S, A = e["target"]["startup"], e["target"]["active"]
                base.update({"trim_start": t0, "trim_end": t1, "frames_total": T, "retime": segs,
                             "hit_start": None if e.get("nohit") else S + 1, "hit_end": None if e.get("nohit") else S + A})
                base.pop("speed_scale", None)
                row["notes"] += flags
                # judge the used window only: feet and jitter over trim_start..trim_end
                wd = {k: v for k, v in base.items() if k != "retime"}
                wd.update({"hit_start": None, "hit_end": None})
                json.dump(wd, open(os.path.join(ROOT, "tmp-finish", cid, "windata.json"), "w", encoding="utf8"), indent=1)
                cmd = [blender, "-b", "--python", "tools/blender/measure_clip.py", "--", "--clip", "game/" + dest, "--data",
                       "tmp-finish/%s/windata.json" % cid, "--id", cid + "-win", "--out", "tmp-finish/%s/win" % cid]
                if e.get("movement"):
                    cmd.append("--movement-clip")
                rc2, _ = run(cmd, cid + ".window.log", False)
                rp2 = os.path.join(ROOT, "tmp-finish", cid, "win", cid + "-win.report.json")
                if rc2 == 0 and os.path.exists(rp2):
                    w = json.load(open(rp2, encoding="utf8"))
                    row["verdict"] = w.get("verdict", {}).get("state", "?")
                    slides = [f.get("worst_run_mean_cm_per_frame", 0) for f in w.get("planted_feet", {}).get("feet", {}).values()]
                    row["slide"] = "%.2f" % max(slides) if slides else "-"
                    row["frames"] = "%d (trim %d-%d)" % (T, t0, t1)
                    row["hit"] = "%d-%d" % (S + 1, S + A)
                    row["notes"].append("jitter %s" % w.get("joint_jitter", {}).get("max_accel"))
            elif rep and not e.get("copy_from"):
                base["frames_total"] = rep.get("frames_total", base["frames_total"])
            base["retarget_notes"] = "Tripo take %s retargeted onto %s by retarget_bvh.py --src-skeleton %s; measured by tools/finish_clips.py (%s, slide %s cm/frame)." % (
                e.get("preset") or os.path.basename(src), body, e.get("src_skeleton", "tripo"), row["verdict"], row["slide"])
            base["notes"] = e.get("notes", "Built by tools/finish_clips.py (YOK-246); no hand-keyed pose (F5). Rebuild: python tools/finish_clips.py --only %s." % cid)
            json.dump(base, open(data_path, "w", encoding="utf8"), indent=2)
            open(data_path, "a").write("\n")
        if e.get("prop") and "prop" in steps:
            prop, plog, ign = PROPS[e["prop"]]
            outdir = "tmp-finish/%s/prop" % cid
            d0 = json.load(open(data_path, encoding="utf8")) if os.path.exists(data_path) else {}
            lo, hi = d0.get("trim_start", 0), d0.get("trim_end", 60)
            frames = ",".join(str(f) for f in range(lo, hi + 1, 2))
            cmd = [blender, "-b", "--python", "tools/blender/prop_clearance.py", "--", "--rig", "game/" + RIGS[body],
                   "--prop", "game/" + prop, "--log", plog, "--out", outdir, "--tag", cid, "--ignore-radius", ign,
                   "--clip", "game/" + dest, "--frames", frames]
            rc, out = run(cmd, cid + ".prop.log", a.dry_run)
            row["prop"] = "ok" if rc == 0 else "FAIL"
            if rc == 0 and not a.dry_run:
                worst = 0.0
                for ln in out.splitlines():
                    if ln.startswith("prop_clearance: ") and "_f" in ln.split()[1] and "{" in ln:
                        try:
                            blob = json.loads(ln[ln.index("{"):])
                            worst = max(worst, max(v.get("new_worst_depth_m", 0) for v in blob.values()))
                        except ValueError:
                            pass
                row["prop"] = "worst new depth %.1f cm (limit 2)" % (worst * 100)
    # summary table
    os.makedirs(os.path.dirname(os.path.join(ROOT, "assets/specs/clips/finish-pipeline-summary.md")), exist_ok=True)
    lines = ["# Finish pipeline summary (tools/finish_clips.py)", "",
             "| clip | body | clean | retarget | verdict | worst slide cm/f | frames | hit frames | prop check | notes |", "|---|---|---|---|---|---|---|---|---|---|"]
    for r in rows:
        lines.append("| %s | %s | %s | %s | %s | %s | %s | %s | %s | %s |" % (r["id"], r["body"], r["clean"], r["retarget"], r["verdict"],
                     r["slide"], r["frames"], r["hit"], r["prop"], "; ".join(r["notes"])))
    if not a.dry_run:
        open(os.path.join(ROOT, "assets/specs/clips/finish-pipeline-summary.md"), "w", encoding="utf8").write("\n".join(lines) + "\n")
    print("\n".join(lines))


if __name__ == "__main__":
    main()
