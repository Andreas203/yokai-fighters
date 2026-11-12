"""YOK-trailer: cuts trailer/yokai-fighters-trailer.mp4 from the Movie Maker takes in trailer/raw/ (sh trailer/record.sh).

Run from anywhere: python -I trailer/cut.py. Needs ffmpeg 9 (path below). Every segment is re-encoded to the same
1080p60 / 48 kHz stereo format, the segments are joined with hard cuts, then a music bed (the game's own ElevenLabs
stage track, AM23) is faded in and out, ducked under the game's hit sounds, and mixed over them.
"""
import os
import subprocess
import sys

FF = r"C:/Users/Andreas/AppData/Local/Microsoft/WinGet/Packages/Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe/ffmpeg-9.0.2-full_build/bin/ffmpeg.exe"
HERE = os.path.dirname(os.path.abspath(__file__))
RAW, SEG, ART = (os.path.join(HERE, d) for d in ("raw", "raw/seg", "art"))
MUSIC = os.path.join(HERE, "..", "game", "sound", "music", "battle_shrine_t1.mp3")
BANNER = os.path.join(SEG, "banner-no-tanuki.png")  # designer 2026-11-12: no Tanuki anywhere in the trailer
FONT = "art/NotoSerif-Bold.ttf"  # relative to HERE: ffmpeg filter strings and Windows drive colons do not mix
OUT = os.path.join(HERE, "yokai-fighters-trailer.mp4")
V = ["-c:v", "libx264", "-preset", "medium", "-crf", "12", "-pix_fmt", "yuv420p", "-r", "60"]
A = ["-c:a", "pcm_s16le", "-ar", "48000", "-ac", "2"]

# (kind, source, start s, length s): clip = a Movie Maker take; card = a text card; title / end = the itch art cards.
EDIT = [
    ("clip", "oni-quake-3", 0.4, 3.0),  # cold open: the Elder Oni's quake
    ("title", None, 0, 4.0),
    ("card", "Defeat the yokai.", 0, 2.2),
    ("clip", "kitsune-clash-2", 0.0, 5.0),  # Foxfire against Spirit Wave in the grove
    ("clip", "oni-quake-4", 3.6, 2.4),  # Iron-Club Charge
    ("clip", "kappa-spin-3", 0.3, 4.4),  # Shell Spin, then River Grab, at the night swamp
    ("clip", "guard-k1", 0.6, 2.6),  # low guard and blocks
    ("clip", "combo-kd-k1", 0.5, 2.6),  # a combo into a knockdown
    ("card", "Bind their powers.", 0, 2.2),
    ("clip", "map", 0.3, 2.2),
    ("clip", "ko-kitsune-1", 1.2, 1.45),  # win over the Kitsune...
    ("clip", "draft-foxfire", 0.4, 2.27),  # ...draft Foxfire (cursor walks to it, pick taken)...
    ("clip", "use-foxfire", 0.4, 2.0),  # ...and Ryo throws it at the Oni
    ("clip", "ko-oni-3", 1.0, 1.35),  # win over the Oni...
    ("clip", "draft-club", 0.4, 2.93),  # ...Iron-Club Charge, frame data shown for a beat...
    ("clip", "use-club-kappa", 0.4, 1.6),  # ...Ryo charges the Kappa
    ("clip", "draft-grab", 0.4, 2.27),  # the Kappa's River Grab...
    ("clip", "use-grab", 0.4, 1.7),  # ...Ryo grabs the Kappa
    ("clip", "oni-quake-4b", 0.6, 1.2),  # montage of quick cuts
    ("clip", "kitsune-fox-3", 1.0, 1.2),
    ("clip", "use-grab-oni", 0.4, 1.2),
    ("clip", "combo-kd-oni2", 0.8, 1.2),
    ("clip", "kappa-grab-2", 0.4, 1.2),
    ("clip", "kitsune-clash-1", 0.6, 1.2),
    ("clip", "ko-kappa-3", 0.0, 2.75),  # the KO finish
    ("end", None, 0, 5.5),
]


def run(args):
    r = subprocess.run([FF, "-v", "error", "-y", *args], cwd=HERE)
    if r.returncode:
        sys.exit(f"ffmpeg failed: {args}")


def segment(i, kind, src, start, length):
    out = os.path.join(SEG, f"{i:02d}.mkv")
    fade = f"fade=t=in:st=0:d=0.35,fade=t=out:st={length - 0.35:.2f}:d=0.35"
    silent = ["-f", "lavfi", "-t", f"{length}", "-i", "anullsrc=r=48000:cl=stereo"]
    if kind == "clip":
        run(["-ss", f"{start}", "-t", f"{length}", "-i", os.path.join(RAW, src + ".avi"),
             "-vf", "scale=1920:1080:in_range=pc:out_range=tv,format=yuv420p,fps=60,setsar=1", "-af", "aresample=48000,aformat=channel_layouts=stereo",
             *V, *A, out])
        return out
    bg = ["-loop", "1", "-t", f"{length}", "-i", os.path.join(ART, "background-1920x1080.png")]
    if kind == "card":
        with open(os.path.join(SEG, f"{i:02d}.txt"), "w", encoding="utf-8") as f:
            f.write(src)
        txt = f"raw/seg/{i:02d}.txt"
        vf = (f"[0]drawtext=fontfile={FONT}:textfile={txt}:fontsize=76:fontcolor=white:"
              f"x=(w-text_w)/2:y=(h-text_h)/2,{fade}[v]")
        run([*bg, *silent, "-filter_complex", vf, "-map", "[v]", "-map", "1:a", "-t", f"{length}", *V, *A, out])
    elif kind == "title":
        vf = f"[1]scale=1368:540[b];[0][b]overlay=(W-w)/2:(H-h)/2,{fade}[v]"
        run([*bg, "-i", BANNER, *silent, "-filter_complex", vf,
             "-map", "[v]", "-map", "2:a", "-t", f"{length}", *V, *A, out])
    elif kind == "end":
        vf = (f"[1]scale=1197:472[c];[0][c]overlay=(W-w)/2:150[b];"
              f"[b]drawtext=fontfile={FONT}:text='Free on itch.io':fontsize=64:fontcolor=white:x=(w-text_w)/2:y=715,"
              f"drawtext=fontfile={FONT}:text='shnabin.itch.io/yokai-fighters':fontsize=48:fontcolor=0xE8C9A0:"
              f"x=(w-text_w)/2:y=815,fade=t=in:st=0:d=0.5,fade=t=out:st={length - 0.8:.2f}:d=0.8[v]")
        run([*bg, "-i", BANNER, *silent, "-filter_complex", vf,
             "-map", "[v]", "-map", "2:a", "-t", f"{length}", *V, *A, out])
    return out


def main():
    os.makedirs(SEG, exist_ok=True)
    # The banner without the Tanuki (right edge, from x 760): crop, then feather both sides into the background.
    run(["-i", os.path.join(ART, "banner-960x300.png"), "-vf",
         "crop=760:300:0:0,format=rgba,geq=r='r(X,Y)':g='g(X,Y)':b='b(X,Y)':a='255*min(1,min(X,W-1-X)/40)'",
         "-frames:v", "1", BANNER])
    segs = [segment(i, *e) for i, e in enumerate(EDIT)]
    total = sum(e[3] for e in EDIT)
    lst = os.path.join(SEG, "list.txt")
    with open(lst, "w") as f:
        f.writelines(f"file '{os.path.basename(s)}'\n" for s in segs)
    joined = os.path.join(SEG, "joined.mkv")
    run(["-f", "concat", "-safe", "0", "-i", lst, "-c", "copy", joined])
    # Game sound (hits) stays on top; the music bed fades in, ducks under the hits (sidechain) and fades out with the end card.
    fc = (f"[0:a]volume=1.6,asplit=2[game][key];"
          f"[1:a]atrim=0:{total},asetpts=N/SR/TB,volume=0.55,afade=t=in:st=0:d=1.5,afade=t=out:st={total - 3.5:.2f}:d=3.5[mus];"
          f"[mus][key]sidechaincompress=threshold=0.05:ratio=4:attack=5:release=250[duck];"
          f"[duck][game]amix=inputs=2:duration=first:normalize=0,alimiter=limit=0.95[a]")
    run(["-i", joined, "-i", MUSIC, "-filter_complex", fc, "-map", "0:v", "-map", "[a]",
         "-c:v", "libx264", "-profile:v", "high", "-preset", "slow", "-crf", "18", "-pix_fmt", "yuv420p", "-color_range", "tv", "-r", "60",
         "-c:a", "aac", "-b:a", "192k", "-ar", "48000", "-movflags", "+faststart", OUT])
    print(f"{OUT}: {total:.1f} s planned")


if __name__ == "__main__":
    main()
