#!/usr/bin/env python3
"""ElevenLabs sound-effect and music jobs for Yokai Fighters (AM23, sound-smith).

  python -I tools/elevenlabs/el.py balance                        free: tier, credits used / limit, reset date
  python -I tools/elevenlabs/el.py plan JOBS.json [--only a,b]    free: each job, its estimated credits, the total
  python -I tools/elevenlabs/el.py run  JOBS.json --max-credits N [--only a,b] [--takes K]
                                                                  paid: runs the jobs, stops before the cap
  python -I tools/elevenlabs/el.py measure FILE.wav [...]         free: length, peak, RMS, onset, tail, clipping, loop seam
  python -I tools/elevenlabs/el.py normalize SRC.wav DST.wav [--peak -1]
                                                                  free: a copy with its peak at --peak dBFS
  python -I tools/elevenlabs/el.py eq SRC.wav DST.wav [--highpass HZ] [--lowpass HZ] [--low-shelf HZ DB]
                                                                  free: a copy with sub-rumble / top cut, lows lifted
  python -I tools/elevenlabs/el.py bands FILE.wav [...]           free: energy share below 250 Hz and above 2.5 kHz

The key comes from ELEVENLABS_API_KEY in the environment, or on Windows from the user environment in the registry
(so a `setx` made after this shell started still counts). It is sent only to api.elevenlabs.io and never printed
or logged.

Every take is written next to the job's `out` as `<out>_t<k>.<ext>`; an existing take is never overwritten, a
retake gets the next number. Each job appends one line to the run log (`log` in the jobs file): the request (no
key), the format actually used, credits before and after, and the measurement. `run` needs `--max-credits`, the
cap the designer approved: a job whose estimate would cross it is not sent, and the run stops there.
"""
from __future__ import annotations

import argparse
import array
import datetime as dt
import json
import math
import os
import sys
import urllib.error
import urllib.request
import wave
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
API = "https://api.elevenlabs.io"

# Credit estimates, used for the cap before a job is sent; the log records what was actually charged.
# Measured 2026-11-09 (Starter, `character-cost` header): sound effects ~10 credits/s. The help centre gives 20/s
# with a set duration and 100 when the model picks it; those are used, so the cap is never crossed by an estimate
# that was too low. Eleven Music returns no cost header; the balance showed ~12.5 credits/s (~750 a minute) for
# one 90 s track on 2026-11-09, against ~900 a minute on third-party pages. 15/s is kept as the safe estimate.
SFX_CREDITS_PER_SECOND = 20
SFX_AUTO_DURATION_CREDITS = 100
MUSIC_CREDITS_PER_SECOND = 15
MUSIC_AUTO_LENGTH_SECONDS = 60

# Formats tried in order; a later one is used only when the plan tier refuses the earlier (44.1 kHz WAV/PCM needs
# Pro, 192 kbps MP3 needs Creator). SFX stay WAV so a hit's onset lands on its frame with no MP3 decoder delay.
SFX_FORMATS = ["wav_44100", "wav_48000", "wav_32000", "mp3_44100_128"]
MUSIC_FORMATS = ["mp3_44100_192", "mp3_44100_128"]

SFX_FIELDS = {"text", "duration_seconds", "loop", "prompt_influence", "model_id"}
MUSIC_FIELDS = {"prompt", "music_length_ms", "force_instrumental", "model_id"}


class ElevenLabsError(Exception):
    def __init__(self, status: int, detail: str):
        super().__init__(f"HTTP {status}: {detail}")
        self.status = status
        self.detail = detail


# ---------------------------------------------------------------- key and HTTP

def api_key() -> str | None:
    key = os.environ.get("ELEVENLABS_API_KEY", "").strip()
    if key:
        return key
    if sys.platform == "win32":
        try:
            import winreg
            with winreg.OpenKey(winreg.HKEY_CURRENT_USER, "Environment") as handle:
                value, _ = winreg.QueryValueEx(handle, "ELEVENLABS_API_KEY")
                return str(value).strip() or None
        except OSError:
            return None
    return None


class Client:
    """The three endpoints the crew uses. Tests pass a fake with the same methods."""

    def __init__(self, key: str, timeout: float = 600):
        self._key = key
        self._timeout = timeout

    def _request(self, method: str, path: str, body: dict | None = None) -> tuple[bytes, dict]:
        data = json.dumps(body).encode() if body is not None else None
        req = urllib.request.Request(API + path, data=data, method=method)
        req.add_header("xi-api-key", self._key)
        req.add_header("Accept", "*/*")
        if data is not None:
            req.add_header("Content-Type", "application/json")
        try:
            with urllib.request.urlopen(req, timeout=self._timeout) as resp:
                return resp.read(), {k.lower(): v for k, v in resp.headers.items()}
        except urllib.error.HTTPError as err:
            raw = err.read().decode("utf-8", "replace")
            try:
                detail = json.loads(raw).get("detail", raw)
            except (ValueError, AttributeError):
                detail = raw
            raise ElevenLabsError(err.code, json.dumps(detail) if not isinstance(detail, str) else detail) from None

    def subscription(self) -> dict:
        raw, _ = self._request("GET", "/v1/user/subscription")
        return json.loads(raw)

    def sound(self, body: dict, output_format: str) -> tuple[bytes, dict]:
        return self._request("POST", f"/v1/sound-generation?output_format={output_format}", body)

    def music(self, body: dict, output_format: str) -> tuple[bytes, dict]:
        return self._request("POST", f"/v1/music?output_format={output_format}", body)


# ---------------------------------------------------------------- jobs

def load_jobs(path: Path) -> dict:
    spec = json.loads(path.read_text(encoding="utf-8"))
    problems = validate_jobs(spec)
    if problems:
        raise SystemExit("jobs file refused:\n  " + "\n  ".join(problems))
    return spec


def validate_jobs(spec: dict) -> list[str]:
    problems = []
    jobs = spec.get("jobs")
    if not isinstance(jobs, list) or not jobs:
        return ["no `jobs` list"]
    if not spec.get("log"):
        problems.append("no `log` path for the run log")
    seen = set()
    for i, job in enumerate(jobs):
        jid = job.get("id") or f"#{i}"
        if jid in seen:
            problems.append(f"{jid}: duplicate id")
        seen.add(jid)
        kind = job.get("kind")
        out = job.get("out", "")
        if kind not in ("sfx", "music"):
            problems.append(f"{jid}: kind must be sfx or music")
            continue
        if not out.startswith("game/sound/") or Path(out).suffix:
            problems.append(f"{jid}: `out` must be under game/sound/ with no extension")
        stem = Path(out).name
        if not stem or not all(c.isalnum() or c in "_-" for c in stem):
            problems.append(f"{jid}: `out` name may hold only letters, digits, _ and - (sound-cue schema)")
        if kind == "sfx":
            if not job.get("text"):
                problems.append(f"{jid}: sfx needs `text`")
            d = job.get("duration_seconds")
            if d is not None and not 0.5 <= d <= 30:
                problems.append(f"{jid}: duration_seconds must be 0.5-30")
            p = job.get("prompt_influence")
            if p is not None and not 0 <= p <= 1:
                problems.append(f"{jid}: prompt_influence must be 0-1")
        else:
            if not job.get("prompt"):
                problems.append(f"{jid}: music needs `prompt`")
            ms = job.get("music_length_ms")
            if ms is not None and not 3000 <= ms <= 600000:
                problems.append(f"{jid}: music_length_ms must be 3000-600000")
    return problems


def select(spec: dict, only: str | None) -> list[dict]:
    jobs = spec["jobs"]
    if not only:
        return list(jobs)
    wanted = [s.strip() for s in only.split(",") if s.strip()]
    known = {j["id"] for j in jobs}
    missing = [w for w in wanted if w not in known]
    if missing:
        raise SystemExit("unknown job id: " + ", ".join(missing))
    return [j for j in jobs if j["id"] in wanted]


def estimate(job: dict) -> int:
    if job["kind"] == "sfx":
        d = job.get("duration_seconds")
        return SFX_AUTO_DURATION_CREDITS if d is None else math.ceil(d * SFX_CREDITS_PER_SECOND)
    ms = job.get("music_length_ms")
    seconds = MUSIC_AUTO_LENGTH_SECONDS if ms is None else ms / 1000
    return math.ceil(seconds * MUSIC_CREDITS_PER_SECOND)


def request_body(job: dict, defaults: dict) -> dict:
    fields = SFX_FIELDS if job["kind"] == "sfx" else MUSIC_FIELDS
    merged = {k: v for k, v in defaults.get(job["kind"], {}).items() if k in fields}
    merged.update({k: v for k, v in job.items() if k in fields})
    return merged


def logged_takes(log_path: Path, job_id: str) -> int:
    """Highest take number the run log records for this job (rejected takes are deleted, their lines stay)."""
    if not log_path.exists():
        return 0
    best = 0
    for line in log_path.read_text(encoding="utf-8").splitlines():
        try:
            entry = json.loads(line)
        except ValueError:
            continue
        if entry.get("id") == job_id and isinstance(entry.get("take"), int):
            best = max(best, entry["take"])
    return best


def next_take_path(out: str, ext: str, root: Path = ROOT, after: int = 0) -> tuple[int, Path]:
    """The first take number above `after` whose file does not exist, so a deleted take's number is never reused."""
    k = after + 1
    while True:
        p = root / f"{out}_t{k}.{ext}"
        if not p.exists():
            return k, p
        k += 1


def ext_for(fmt: str) -> str:
    return fmt.split("_", 1)[0]


def tier_refusal(err: ElevenLabsError) -> bool:
    text = err.detail.lower()
    return err.status in (400, 403, 422) and any(w in text for w in ("output_format", "tier", "subscription", "plan"))


def generate(client, job: dict, body: dict, formats: list[str]) -> tuple[bytes, dict, str]:
    call = client.sound if job["kind"] == "sfx" else client.music
    last: ElevenLabsError | None = None
    for fmt in formats:
        try:
            audio, headers = call(body, fmt)
            return audio, headers, fmt
        except ElevenLabsError as err:
            if not tier_refusal(err):
                raise
            last = err
    assert last is not None
    raise last


def remaining(sub: dict) -> int:
    return int(sub.get("character_limit", 0)) - int(sub.get("character_count", 0))


def run(client, spec: dict, jobs: list[dict], max_credits: int, takes: int = 1, root: Path = ROOT,
        out=sys.stdout) -> int:
    """Run each job `takes` times; stop before any job whose estimate would cross the cap. Returns credits used.

    `used` is the larger of the account's own movement since the run began and the run's ledger (header charges, plus
    the estimate for each call without a header). The counter lags and can catch up several calls at once (seen
    2026-11-10: one music call moved it by five tracks' worth), so a counter delta is never booked to a single call.
    """
    log_path = root / spec["log"]
    log_path.parent.mkdir(parents=True, exist_ok=True)
    defaults = spec.get("defaults", {})
    used = 0
    ledger = 0
    sub = client.subscription()
    start = int(sub.get("character_count", 0))
    for job in jobs:
        for _ in range(takes):
            est = estimate(job)
            if used + est > max_credits:
                print(f"STOP before {job['id']}: {used} used + {est} estimated > cap {max_credits}", file=out)
                return used
            if remaining(sub) < est:
                print(f"STOP before {job['id']}: account has {remaining(sub)} credits left, job needs ~{est}",
                      file=out)
                return used
            body = request_body(job, defaults)
            formats = [job["output_format"]] if job.get("output_format") else (
                SFX_FORMATS if job["kind"] == "sfx" else MUSIC_FORMATS)
            before = int(sub.get("character_count", 0))
            entry = {"time": dt.datetime.now().astimezone().isoformat(timespec="seconds"), "id": job["id"],
                     "kind": job["kind"], "request": body, "estimate": est, "credits_before": before}
            try:
                audio, headers, fmt = generate(client, job, body, formats)
            except ElevenLabsError as err:
                entry.update(status="error", error=str(err))
                append_log(log_path, entry)
                print(f"FAIL {job['id']}: {err}", file=out)
                if err.status == 401:
                    return used
                sub = client.subscription()
                continue
            take, path = next_take_path(job["out"], ext_for(fmt), root, logged_takes(log_path, job["id"]))
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(audio)
            sub = client.subscription()
            # The `character-cost` header is the call's own charge; without it (music) the estimate is booked.
            header_cost = headers.get("character-cost") or headers.get("x-character-count")
            if header_cost and str(header_cost).isdigit():
                charged, source = int(header_cost), "header"
            else:
                charged, source = est, "estimate"
            after = int(sub.get("character_count", 0))
            ledger += charged
            used = max(ledger, after - start)
            entry.update(status="ok", take=take, file=path.relative_to(root).as_posix(), format=fmt,
                         bytes=len(audio), credits_after=after, charged=charged,
                         charged_from=source, run_account_delta=after - start,
                         song_id=headers.get("song-id"), request_id=headers.get("request-id"),
                         measure=measure(path) if path.suffix == ".wav" else None)
            append_log(log_path, entry)
            print(f"OK   {job['id']} t{take} -> {entry['file']} ({fmt}, {charged} credits, total {used})", file=out)
    return used


def append_log(path: Path, entry: dict) -> None:
    with path.open("a", encoding="utf-8") as fh:
        fh.write(json.dumps(entry, ensure_ascii=False) + "\n")


# ---------------------------------------------------------------- measure

def _db(x: float) -> float:
    return round(20 * math.log10(x), 1) if x > 0 else -120.0


def measure(path: Path | str) -> dict:
    """Length, peak and RMS (dBFS), onset (first sample above -40 dBFS), trailing silence (below -60 dBFS),
    clipped samples, and for loops the jump at the seam (last frame to first, dBFS of the difference)."""
    with wave.open(str(path), "rb") as w:
        rate, ch, width, n = w.getframerate(), w.getnchannels(), w.getsampwidth(), w.getnframes()
        raw = w.readframes(n)
    info = {"rate": rate, "channels": ch, "bits": width * 8, "seconds": round(n / rate, 3) if rate else 0}
    if width != 2 or n == 0:
        info["note"] = "only 16-bit PCM is measured"
        return info
    s = array.array("h")
    s.frombytes(raw)
    if sys.byteorder == "big":
        s.byteswap()
    full = 32768.0
    frames = [max(abs(s[i * ch + c]) for c in range(ch)) for i in range(n)]
    peak = max(frames)
    rms = math.sqrt(sum(v * v for v in s) / len(s))
    onset_thr, tail_thr = full * 10 ** (-40 / 20), full * 10 ** (-60 / 20)
    onset = next((i for i, v in enumerate(frames) if v > onset_thr), None)
    last = next((i for i in range(n - 1, -1, -1) if frames[i] > tail_thr), None)
    seam = max(abs(s[(n - 1) * ch + c] - s[c]) for c in range(ch))
    info.update(peak_dbfs=_db(peak / full), rms_dbfs=_db(rms / full),
                onset_ms=None if onset is None else round(onset * 1000 / rate, 1),
                tail_silence_ms=round((n - 1 - (last if last is not None else -1)) * 1000 / rate, 1),
                clipped=sum(1 for v in s if v >= 32767 or v <= -32768),
                seam_jump_dbfs=_db(seam / full))
    return info


def _read16(path: Path | str):
    with wave.open(str(path), "rb") as w:
        params, raw = w.getparams(), w.readframes(w.getnframes())
    if params.sampwidth != 2:
        raise SystemExit(f"{path}: only 16-bit PCM can be processed")
    s = array.array("h")
    s.frombytes(raw)
    if sys.byteorder == "big":
        s.byteswap()
    return params, s


def _write_scaled(params, samples, dst: Path | str, peak_dbfs: float, fade_ms: float, src="") -> float:
    """Scale interleaved float samples so the peak sits at `peak_dbfs`, fade the last `fade_ms`, write 16-bit."""
    peak = max((abs(v) for v in samples), default=0)
    if peak == 0:
        raise SystemExit(f"{src}: silent, nothing to normalize")
    gain = 32767 * 10 ** (peak_dbfs / 20) / peak
    ch, n = params.nchannels, len(samples) // params.nchannels
    fade = min(n, int(params.framerate * fade_ms / 1000))
    out = array.array("h", (
        max(-32768, min(32767, round(v * gain * (min(1.0, (n - 1 - i // ch) / fade) if fade else 1.0))))
        for i, v in enumerate(samples)))
    if sys.byteorder == "big":
        out.byteswap()
    with wave.open(str(dst), "wb") as w:
        w.setparams(params)
        w.writeframes(out.tobytes())
    return round(20 * math.log10(gain), 1)


def normalize(src: Path | str, dst: Path | str, peak_dbfs: float = -1.0, fade_ms: float = 10.0) -> float:
    """Write `src` scaled so its peak sits at `peak_dbfs` (16-bit WAV only), with a linear fade over the last
    `fade_ms` so a take cut off at its set length does not click. Returns the gain applied, in dB.
    It cannot repair a take that was clipped when generated."""
    params, s = _read16(src)
    return _write_scaled(params, s, dst, peak_dbfs, fade_ms, src)


def _biquad(kind: str, f0: float, rate: int, gain_db: float = 0.0, q: float = 0.7071):
    """RBJ audio-EQ-cookbook coefficients (b, a), normalised by a0."""
    amp = 10 ** (gain_db / 40)
    w0 = 2 * math.pi * f0 / rate
    cw, alpha = math.cos(w0), math.sin(w0) / (2 * q)
    if kind == "lowpass":
        b, a = [(1 - cw) / 2, 1 - cw, (1 - cw) / 2], [1 + alpha, -2 * cw, 1 - alpha]
    elif kind == "highpass":
        b, a = [(1 + cw) / 2, -(1 + cw), (1 + cw) / 2], [1 + alpha, -2 * cw, 1 - alpha]
    elif kind == "lowshelf":
        sq = 2 * math.sqrt(amp) * alpha
        b = [amp * ((amp + 1) - (amp - 1) * cw + sq), 2 * amp * ((amp - 1) - (amp + 1) * cw),
             amp * ((amp + 1) - (amp - 1) * cw - sq)]
        a = [(amp + 1) + (amp - 1) * cw + sq, -2 * ((amp - 1) + (amp + 1) * cw), (amp + 1) + (amp - 1) * cw - sq]
    else:
        raise ValueError(kind)
    return [x / a[0] for x in b], [x / a[0] for x in a]


def _filter(x, coeffs) -> list[float]:
    (b0, b1, b2), (_, a1, a2) = coeffs
    y, x1, x2, y1, y2 = [], 0.0, 0.0, 0.0, 0.0
    for v in x:
        o = b0 * v + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2
        x2, x1, y2, y1 = x1, v, y1, o
        y.append(o)
    return y


def bands(path: Path | str, low_hz: float = 250, high_hz: float = 2500) -> dict:
    """Share of the energy below `low_hz` and above `high_hz` (two cascaded 2nd-order filters each, on the
    mono mix). A "tinny" hit has a small low share and a large high share; a boomy one the reverse."""
    params, s = _read16(path)
    ch, rate = params.nchannels, params.framerate
    mono = [sum(s[i:i + ch]) / ch for i in range(0, len(s), ch)]
    total = sum(v * v for v in mono) or 1.0
    low = _filter(_filter(mono, _biquad("lowpass", low_hz, rate)), _biquad("lowpass", low_hz, rate))
    high = _filter(_filter(mono, _biquad("highpass", high_hz, rate)), _biquad("highpass", high_hz, rate))
    return {f"low_below_{int(low_hz)}hz_pct": round(100 * sum(v * v for v in low) / total, 1),
            f"high_above_{int(high_hz)}hz_pct": round(100 * sum(v * v for v in high) / total, 1)}


def eq(src: Path | str, dst: Path | str, lowpass_hz: float | None = None, shelf_hz: float | None = None,
       shelf_db: float = 0.0, peak_dbfs: float = -1.0, fade_ms: float = 10.0,
       highpass_hz: float | None = None) -> float:
    """Free cleanup per channel: optional low-shelf boost, high-pass (24 dB/octave: cuts sub-rumble that
    speakers cannot play but that eats the headroom) and low-pass (12 dB/octave: cuts a tinny top), then
    normalize and fade. Returns the final gain in dB."""
    params, s = _read16(src)
    ch, rate = params.nchannels, params.framerate
    chans = [list(s[c::ch]) for c in range(ch)]
    for c in range(ch):
        if shelf_hz and shelf_db:
            chans[c] = _filter(chans[c], _biquad("lowshelf", shelf_hz, rate, shelf_db))
        if highpass_hz:
            for _ in range(2):
                chans[c] = _filter(chans[c], _biquad("highpass", highpass_hz, rate))
        if lowpass_hz:
            chans[c] = _filter(chans[c], _biquad("lowpass", lowpass_hz, rate))
    mixed = [chans[c][i] for i in range(len(chans[0])) for c in range(ch)]
    return _write_scaled(params, mixed, dst, peak_dbfs, fade_ms, src)


# ---------------------------------------------------------------- CLI

def _client() -> Client:
    key = api_key()
    if not key:
        raise SystemExit("ELEVENLABS_API_KEY is not set. Set it in your own terminal (setx ELEVENLABS_API_KEY ...);"
                         " never paste it into a chat or a file in the repo.")
    return Client(key)


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    sub.add_parser("balance")
    p_plan = sub.add_parser("plan")
    p_plan.add_argument("jobs", type=Path)
    p_plan.add_argument("--only")
    p_run = sub.add_parser("run")
    p_run.add_argument("jobs", type=Path)
    p_run.add_argument("--max-credits", type=int, required=True)
    p_run.add_argument("--only")
    p_run.add_argument("--takes", type=int, default=1)
    p_measure = sub.add_parser("measure")
    p_measure.add_argument("files", nargs="+", type=Path)
    p_norm = sub.add_parser("normalize")
    p_norm.add_argument("src", type=Path)
    p_norm.add_argument("dst", type=Path)
    p_norm.add_argument("--peak", type=float, default=-1.0, help="target peak in dBFS (default -1)")
    p_norm.add_argument("--fade-ms", type=float, default=10.0, help="fade-out at the end (default 10 ms, 0 = none)")
    p_eq = sub.add_parser("eq")
    p_eq.add_argument("src", type=Path)
    p_eq.add_argument("dst", type=Path)
    p_eq.add_argument("--lowpass", type=float, help="low-pass cutoff in Hz (cuts tinny top end)")
    p_eq.add_argument("--highpass", type=float, help="high-pass cutoff in Hz (cuts inaudible sub-rumble)")
    p_eq.add_argument("--low-shelf", nargs=2, type=float, metavar=("HZ", "DB"), help="low-shelf boost, e.g. 150 6")
    p_eq.add_argument("--peak", type=float, default=-1.0)
    p_eq.add_argument("--fade-ms", type=float, default=10.0)
    p_bands = sub.add_parser("bands")
    p_bands.add_argument("files", nargs="+", type=Path)
    args = ap.parse_args(argv)

    if args.cmd == "bands":
        for f in args.files:
            print(f.as_posix(), json.dumps(bands(f)))
        return 0
    if args.cmd == "eq":
        if args.dst.resolve() == args.src.resolve():
            raise SystemExit("write the EQ'd copy to a new file; the take stays as generated")
        hz, db = args.low_shelf if args.low_shelf else (None, 0.0)
        g = eq(args.src, args.dst, args.lowpass, hz, db, args.peak, args.fade_ms, args.highpass)
        print(f"{args.dst.as_posix()}: gain {g:+} dB", json.dumps({**measure(args.dst), **bands(args.dst)}))
        return 0

    if args.cmd == "normalize":
        if args.dst.resolve() == args.src.resolve():
            raise SystemExit("write the normalized copy to a new file; the take stays as generated")
        print(f"{args.dst.as_posix()}: gain {normalize(args.src, args.dst, args.peak, args.fade_ms):+} dB",
              json.dumps(measure(args.dst)))
        return 0

    if args.cmd == "balance":
        s = _client().subscription()
        reset = s.get("next_character_count_reset_unix")
        print(json.dumps({"tier": s.get("tier"), "status": s.get("status"), "used": s.get("character_count"),
                          "limit": s.get("character_limit"), "left": remaining(s),
                          "resets": dt.datetime.fromtimestamp(reset).date().isoformat() if reset else None},
                         indent=2))
        return 0
    if args.cmd == "measure":
        for f in args.files:
            print(f.as_posix(), json.dumps(measure(f)))
        return 0
    spec = load_jobs(args.jobs)
    jobs = select(spec, args.only)
    if args.cmd == "plan":
        total = 0
        for j in jobs:
            e = estimate(j)
            total += e
            length = j.get("duration_seconds") or (j.get("music_length_ms") or 0) / 1000 or "auto"
            print(f"{j['id']:<24} {j['kind']:<5} {str(length):>6} s  ~{e:>5} credits  -> {j['out']}")
        print(f"{len(jobs)} jobs, ~{total} credits for one take each (estimate; the log records the real charge)")
        return 0
    if args.takes < 1:
        raise SystemExit("--takes must be at least 1")
    used = run(_client(), spec, jobs, args.max_credits, args.takes)
    print(f"credits used this run: {used} of cap {args.max_credits}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
