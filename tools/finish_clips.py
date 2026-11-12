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


def build_retime(report, target, trim_n):
    """Segments mapping game frames onto cleaned ticks 0..trim_n so startup snaps, the hit holds and recovery eases out.
    Each segment plays within 0.5x-2x, a held tick shows at most 4 frames; returns (frames_total, retime, flags)."""
    S, A, T = target["startup"], target["active"], target["total"]
    h = report.get("hit", {}).get("suggested") or {}
    h0, h1 = (h["hit_start"] - 1, h["hit_end"] - 1) if h.get("hit_start") else (None, None)  # cleaned clip at 1x: tick = frame - 1
    flags = []
    if h0 is None or h1 is None:
        return T, [{"frames": [1, T], "ticks": [0, trim_n], "note": "one segment, hit window not measured"}], ["no hit window"]
    h1 = max(h1, h0)
    clamp = lambda dt, df: max(df // 2 if df % 2 == 0 else (df + 1) // 2, min(2 * df, dt))  # ticks within 0.5x-2x of df frames
    segs, f, t = [], 1, 0
    sf = S + 1
    t2 = t + clamp(h0 - t, sf - f)
    segs.append({"frames": [f, sf], "ticks": [t, t2], "note": "startup snaps"})
    if abs(t2 - h0) > 1:
        flags.append("startup reaches tick %d, hit at %d" % (t2, h0))
    f, t = sf, t2
    af = f + max(A, 1)
    dt_active = min(max(h1 - h0, 0), 2 * (af - f))
    segs.append({"frames": [f, af], "ticks": [t, t + dt_active], "note": "strike pose through the active frames"})
    f, t = af, t + dt_active
    rec = T - f
    dt_rec = clamp(trim_n - t, rec) if rec > 0 else 0
    if rec > 0:
        segs.append({"frames": [f, T], "ticks": [t, t + dt_rec], "note": "recovery eases out"})
    if t + dt_rec < trim_n - 1:
        flags.append("clip tail beyond tick %d unused" % (t + dt_rec))
    return T, segs, flags


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
        # data template
        template = {}
        if e.get("copy_from"):
            template = json.load(open(os.path.join(ROOT, "data/clips", e["copy_from"] + ".json"), encoding="utf8"))
        data_path = os.path.join(ROOT, "data/clips", cid + ".json")
        base = dict(template)
        base.update({"kind": "clip", "id": cid, "status": "proposed", "file": dest, "hit_start": template.get("hit_start"),
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
            if e.get("target") and rep:  # a take: build the retime from the measured hit window
                n = rep.get("frames_total", 1) - 1
                T, segs, flags = build_retime(rep, e["target"], n)
                base.update({"trim_start": 0, "trim_end": n, "frames_total": T, "retime": segs})
                base.pop("speed_scale", None)
                row["notes"] += flags
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
