"""Tests for tools/elevenlabs/el.py (AM23). No network: a fake client stands in for ElevenLabs.
Run: python -I -m unittest discover -s tools -p 'test_elevenlabs.py'"""
import array
import io
import json
import math
import sys
import tempfile
import unittest
import wave
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "elevenlabs"))
import el  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]


def wav_bytes(samples, rate=44100, channels=1):
    buf = io.BytesIO()
    with wave.open(buf, "wb") as w:
        w.setnchannels(channels)
        w.setsampwidth(2)
        w.setframerate(rate)
        w.writeframes(array.array("h", samples).tobytes())
    return buf.getvalue()


class FakeClient:
    """Charges `cost` credits per call; refuses the formats in `refuse` the way a plan tier does."""

    def __init__(self, used=0, limit=10_000, cost=30, refuse=(), fail_ids=()):
        self.used, self.limit, self.cost = used, limit, cost
        self.refuse, self.fail_ids = set(refuse), set(fail_ids)
        self.calls = []

    def subscription(self):
        return {"character_count": self.used, "character_limit": self.limit, "tier": "creator"}

    def _gen(self, body, fmt):
        self.calls.append((body, fmt))
        if fmt in self.refuse:
            raise el.ElevenLabsError(403, f"output_format {fmt} requires a higher subscription tier")
        if body.get("text") in self.fail_ids or body.get("prompt") in self.fail_ids:
            raise el.ElevenLabsError(422, "text is not allowed")
        self.used += self.cost
        return wav_bytes([0, 1000, -1000, 0] * 100), {}

    sound = _gen
    music = _gen


def spec(*jobs):
    return {"log": "logs/run.jsonl", "defaults": {"sfx": {"prompt_influence": 0.5, "loop": False}},
            "jobs": list(jobs)}


SFX = {"id": "hit", "kind": "sfx", "out": "game/sound/sfx/hit", "duration_seconds": 0.5, "text": "punch"}
MUSIC = {"id": "theme", "kind": "music", "out": "game/sound/music/theme", "music_length_ms": 60000, "prompt": "taiko"}


class Validate(unittest.TestCase):
    def test_batch_1_is_valid(self):
        data = json.loads((ROOT / "assets/specs/sound/elevenlabs-batch-1.json").read_text(encoding="utf-8"))
        self.assertEqual(el.validate_jobs(data), [])

    def test_refuses_bad_jobs(self):
        bad = spec({"id": "a", "kind": "sfx", "out": "sound/a", "text": "x"},
                   {"id": "a", "kind": "sfx", "out": "game/sound/b.wav", "text": "x"},
                   {"id": "c", "kind": "sfx", "out": "game/sound/c d", "duration_seconds": 40},
                   {"id": "d", "kind": "music", "out": "game/sound/d", "music_length_ms": 1000},
                   {"id": "e", "kind": "voice", "out": "game/sound/e"})
        problems = "\n".join(el.validate_jobs(bad))
        self.assertEqual(problems.count("a: `out` must be under game/sound/"), 2)  # sound/a, and b.wav
        for needle in ("a: duplicate id", "c: sfx needs `text`",
                       "c: duration_seconds", "c: `out` name", "d: music needs `prompt`", "d: music_length_ms",
                       "e: kind"):
            self.assertIn(needle, problems)


class Estimate(unittest.TestCase):
    def test_sfx_and_music(self):
        self.assertEqual(el.estimate(SFX), math.ceil(0.5 * el.SFX_CREDITS_PER_SECOND))
        self.assertEqual(el.estimate({**SFX, "duration_seconds": None}), el.SFX_AUTO_DURATION_CREDITS)
        self.assertEqual(el.estimate(MUSIC), 60 * el.MUSIC_CREDITS_PER_SECOND)

    def test_body_takes_defaults_then_job_and_drops_bookkeeping(self):
        body = el.request_body({**SFX, "loop": True, "group": "V6"}, spec()["defaults"])
        self.assertEqual(body, {"text": "punch", "duration_seconds": 0.5, "loop": True, "prompt_influence": 0.5})


class Run(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)

    def tearDown(self):
        self.tmp.cleanup()

    def log(self):
        return [json.loads(line) for line in (self.root / "logs/run.jsonl").read_text().splitlines()]

    def run_jobs(self, client, jobs, cap=1000, takes=1):
        return el.run(client, spec(*jobs), jobs, cap, takes, root=self.root, out=io.StringIO())

    def test_writes_takes_without_overwriting(self):
        client = FakeClient()
        self.assertEqual(self.run_jobs(client, [SFX], takes=2), 60)
        self.run_jobs(client, [SFX])
        names = sorted(p.name for p in (self.root / "game/sound/sfx").iterdir())
        self.assertEqual(names, ["hit_t1.wav", "hit_t2.wav", "hit_t3.wav"])
        entry = self.log()[0]
        self.assertEqual((entry["status"], entry["charged"], entry["format"]), ("ok", el.estimate(SFX), "wav_44100"))
        self.assertEqual(entry["measure"]["rate"], 44100)

    def test_a_deleted_take_number_is_not_reused(self):
        client = FakeClient()
        self.run_jobs(client, [SFX], takes=2)
        (self.root / "game/sound/sfx/hit_t1.wav").unlink()  # rejected take deleted, its log line stays
        self.run_jobs(client, [SFX])
        self.assertEqual(sorted(p.name for p in (self.root / "game/sound/sfx").iterdir()),
                         ["hit_t2.wav", "hit_t3.wav"])
        self.assertEqual([e["take"] for e in self.log()], [1, 2, 3])

    def test_stops_before_the_cap(self):
        est = el.estimate(SFX)
        client = FakeClient(cost=est)
        # a cap of two and a half estimates allows two jobs; the third would cross it
        used = self.run_jobs(client, [SFX, {**SFX, "id": "b", "out": "game/sound/sfx/b"},
                                      {**SFX, "id": "c", "out": "game/sound/sfx/c"}], cap=2 * est + est // 2)
        self.assertEqual(used, 2 * est)
        self.assertEqual(len(client.calls), 2)

    def test_stops_when_account_is_short(self):
        client = FakeClient(used=10_000 - (el.estimate(SFX) - 1), limit=10_000)
        self.assertEqual(self.run_jobs(client, [SFX]), 0)
        self.assertEqual(client.calls, [])

    def test_falls_back_when_the_tier_refuses_a_format(self):
        client = FakeClient(refuse={"wav_44100", "wav_48000"})
        self.run_jobs(client, [SFX])
        self.assertEqual([fmt for _, fmt in client.calls], ["wav_44100", "wav_48000", "wav_32000"])
        self.assertEqual(self.log()[0]["format"], "wav_32000")

    def test_logs_a_refused_prompt_and_goes_on(self):
        client = FakeClient(fail_ids={"punch"})
        used = self.run_jobs(client, [SFX, MUSIC])
        statuses = [e["status"] for e in self.log()]
        self.assertEqual(statuses, ["error", "ok"])
        self.assertEqual(used, el.estimate(MUSIC))  # music has no cost header: its estimate is booked

    def test_counts_the_estimate_when_the_counter_has_not_moved(self):
        client = FakeClient(cost=0)
        self.assertEqual(self.run_jobs(client, [MUSIC]), el.estimate(MUSIC))
        self.assertEqual(self.log()[0]["charged_from"], "estimate")

    def test_cost_header_beats_the_lagging_counter(self):
        client = FakeClient(cost=0)
        sound = client.sound
        client.sound = lambda body, fmt: (sound(body, fmt)[0], {"character-cost": "6"})
        self.assertEqual(self.run_jobs(client, [SFX]), 6)
        self.assertEqual(self.log()[0]["charged_from"], "header")

    def test_a_counter_catching_up_is_not_booked_twice(self):
        # Seen 2026-11-10: the counter sat still for four music calls, then moved by all five at once.
        client = FakeClient(cost=0)
        real = 750
        calls = {"n": 0}
        music = client.music

        def lagging(body, fmt):
            calls["n"] += 1
            result = music(body, fmt)
            if calls["n"] == 3:
                client.used += 3 * real
            return result
        client.music = lagging
        jobs = [dict(MUSIC, id=f"m{i}", out=f"game/sound/music/m{i}") for i in range(4)]
        used = self.run_jobs(client, jobs, cap=4 * el.estimate(MUSIC))
        self.assertEqual(len(self.log()), 4, "the catch-up does not stop the run early")
        self.assertEqual([e["charged"] for e in self.log()], [el.estimate(MUSIC)] * 4)
        self.assertEqual(used, 4 * el.estimate(MUSIC))

    def test_account_movement_wins_when_it_is_larger(self):
        client = FakeClient(cost=5000)
        self.assertEqual(self.run_jobs(client, [MUSIC], cap=10_000), 5000)

    def test_log_never_holds_the_key(self):
        self.run_jobs(FakeClient(), [SFX])
        self.assertNotIn("xi-api-key", (self.root / "logs/run.jsonl").read_text())


class Measure(unittest.TestCase):
    def test_onset_tail_and_seam(self):
        rate = 1000
        samples = [0] * 100 + [16384] * 50 + [0] * 850  # 100 ms silence, 50 ms at -6 dBFS, 850 ms silence
        with tempfile.TemporaryDirectory() as d:
            p = Path(d) / "t.wav"
            p.write_bytes(wav_bytes(samples, rate=rate))
            m = el.measure(p)
        self.assertEqual(m["seconds"], 1.0)
        self.assertEqual(m["onset_ms"], 100.0)
        self.assertEqual(m["tail_silence_ms"], 850.0)
        self.assertAlmostEqual(m["peak_dbfs"], -6.0, delta=0.1)
        self.assertEqual(m["clipped"], 0)
        self.assertEqual(m["seam_jump_dbfs"], -120.0)


class Normalize(unittest.TestCase):
    def test_raises_a_quiet_take_to_the_target_peak_without_clipping(self):
        with tempfile.TemporaryDirectory() as d:
            src, dst = Path(d) / "q.wav", Path(d) / "n.wav"
            src.write_bytes(wav_bytes([0, 1000, -2000, 500] * 50, channels=2))
            gain = el.normalize(src, dst, -1.0, fade_ms=0)  # the 4.5 ms sample is shorter than a default fade
            m = el.measure(dst)
        self.assertAlmostEqual(gain, 20 * math.log10(32767 * 10 ** (-1 / 20) / 2000), delta=0.1)
        self.assertAlmostEqual(m["peak_dbfs"], -1.0, delta=0.1)
        self.assertEqual((m["clipped"], m["channels"]), (0, 2))

    def test_fades_the_cut_off_end_to_silence(self):
        with tempfile.TemporaryDirectory() as d:
            src, dst = Path(d) / "c.wav", Path(d) / "n.wav"
            src.write_bytes(wav_bytes([8000] * 1000, rate=1000))  # ends at full level, as a cut-off take does
            el.normalize(src, dst, -1.0, fade_ms=50)
            with wave.open(str(dst)) as w:
                s = array.array("h", w.readframes(w.getnframes()))
        self.assertEqual(s[-1], 0)
        self.assertGreater(s[-25], 0)
        self.assertEqual(s[0], s[949])  # untouched before the fade

    def test_refuses_silence(self):
        with tempfile.TemporaryDirectory() as d:
            src = Path(d) / "s.wav"
            src.write_bytes(wav_bytes([0] * 100))
            with self.assertRaises(SystemExit):
                el.normalize(src, Path(d) / "n.wav")


def tone(freq, rate=8000, seconds=0.25, amp=8000):
    return [round(amp * math.sin(2 * math.pi * freq * i / rate)) for i in range(int(rate * seconds))]


class Bands(unittest.TestCase):
    def write(self, d, name, samples, rate=8000):
        p = Path(d) / name
        p.write_bytes(wav_bytes(samples, rate=rate))
        return p

    def test_low_and_high_tones_land_in_their_bands(self):
        with tempfile.TemporaryDirectory() as d:
            low = el.bands(self.write(d, "low.wav", tone(80)))
            high = el.bands(self.write(d, "high.wav", tone(3500)))
        self.assertGreater(low["low_below_250hz_pct"], 80)
        self.assertLess(low["high_above_2500hz_pct"], 1)
        self.assertGreater(high["high_above_2500hz_pct"], 80)
        self.assertLess(high["low_below_250hz_pct"], 1)

    def test_eq_moves_energy_from_the_top_to_the_bottom(self):
        mixed = [a + b for a, b in zip(tone(80, amp=4000), tone(3500, amp=4000))]
        with tempfile.TemporaryDirectory() as d:
            src = self.write(d, "mix.wav", mixed)
            before = el.bands(src)
            el.eq(src, Path(d) / "eq.wav", lowpass_hz=1000, shelf_hz=150, shelf_db=6)
            after = el.bands(Path(d) / "eq.wav")
            m = el.measure(Path(d) / "eq.wav")
        self.assertGreater(after["low_below_250hz_pct"], before["low_below_250hz_pct"] + 30)
        self.assertLess(after["high_above_2500hz_pct"], before["high_above_2500hz_pct"] / 10)
        self.assertEqual(m["clipped"], 0)


    def test_highpass_removes_sub_rumble(self):
        mixed = [a + b for a, b in zip(tone(25, amp=6000), tone(150, amp=2000))]
        with tempfile.TemporaryDirectory() as d:
            src = self.write(d, "rumble.wav", mixed)
            el.eq(src, Path(d) / "hp.wav", highpass_hz=60)
            params, s = el._read16(Path(d) / "hp.wav")
        sub = el._filter(el._filter(list(s), el._biquad("lowpass", 50, 8000)), el._biquad("lowpass", 50, 8000))
        self.assertLess(sum(v * v for v in sub) / sum(v * v for v in s), 0.05)

if __name__ == "__main__":
    unittest.main()
