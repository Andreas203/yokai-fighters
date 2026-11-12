# Sound batch 1: ElevenLabs music and sound effects (YOK-54, AM22)

Author: sound-smith. Jobs: [`elevenlabs-batch-1.json`](elevenlabs-batch-1.json) holds every prompt, length and output path; run it with `tools/elevenlabs/el.py` (see [`.claude/agents/sound-smith.md`](../../../.claude/agents/sound-smith.md)). Run log: `logs/elevenlabs-runs.jsonl`.

**Status: sampler approved, 1,800-credit cap (designer, 2026-11-09).** Jobs `hit-light`, `hit-heavy`, `hit-block`, `sting-ryo`, `sting-kitsune`, `foxfire`, `ui-seal`, `music-battle-grove`, one take each (~1,580 credits estimated). The other 28 jobs wait for a new approval after the sampler is heard. Sampler run on 2026-11-09; see Acceptance.

## What it covers

| Need (rule) | Jobs | Count |
|---|---|---|
| Impact thuds: light, medium, heavy, block, throw, knockdown (V6) | `hit-*`, `throw`, `knockdown` | 6 |
| The ink-stroke binding (V4) | `binding` | 1 |
| Yokai stings, one per source: Ryo paper, Kitsune bells and fire, Oni iron and growl, Kappa water and shell, Tanuki leaf-poof and detuned woodblock (V6) | `sting-*` | 5 |
| Move sounds: whooshes, Spirit Wave, Rising Talisman, Foxfire, ground pound, teleport | `whoosh-*`, `spirit-wave`, `rising-talisman`, `foxfire`, `ground-pound`, `teleport` | 7 |
| UI: card, brush, seal stamp, menu tick, confirm clack | `ui-*` | 5 |
| Round start (taiko + clapper) and round end (temple bell) | `round-*` | 2 |
| Stage ambience loops, one per stage (V9) | `amb-*` (20 s, `loop`) | 4 |
| Music: title, a battle theme per row that rises in intensity, the merchant melody, the Tanuki theme that detunes it (V9) | `music-*` (instrumental) | 6 |

Battle-theme intensity by row: bamboo grove ~128 BPM, moderate → shrine gate ~140 BPM, heavier taiko → lantern river ~148 BPM, high → Tanuki ~150 BPM, highest, with the merchant's shamisen tune drifting out of tune. A prompt can only describe the detune; if the Tanuki take does not quote the merchant melody, the fallback is a pitch-bent copy of the accepted merchant track, made in the engine or by script (no credits).

## Cost

`el.py plan` total: **~11,100 credits for one take of each of the 36 jobs.** That figure uses the higher published rates: 40 credits per second for sound effects (the help centre gives 20 for the API) and ~900 a minute for music (third-party figure). The run log records what was actually charged, so the first run corrects the estimate. Retakes (up to three per sound) need a new approval.

## Formats

- Sound effects: WAV, 44.1 kHz; the tool falls back to 48 or 32 kHz, then MP3, if the plan refuses it (the docs say Pro, but Starter was accepted on 2026-11-09). WAV keeps an impact's first sample on its frame, with no MP3 decoder delay.
- Music: MP3 192 kbps (the docs say Creator; Starter was accepted), else 128 kbps. Godot imports MP3 and loops it.

## Acceptance

Criteria per take are in the agent definition (onset under 15 ms for impacts, seam jump under −30 dBFS for loops, no clipping, rising intensity by row). Verdicts are recorded here after the run.

Sampler run 2026-11-09, Starter plan. All takes start on their first sample (onset 0 ms). The verdicts below come from the measurements; the designer's listen decides.

| Job | Take | Measured | Verdict | Notes |
|---|---|---|---|---|
| `hit-light` | t1 | 0.48 s, peak 0 dBFS, 336 clipped samples (flat runs up to 15) | REJECT (designer) | Clipped; file deleted. |
| `hit-light` | t2 | 0.48 s, peak −0.3 dBFS, 0 clipped | listen | Prompt rewritten for a deeper thud (designer: "more thud sounds"). Trim −1 dB before use. |
| `hit-heavy` | t1 | 0.88 s, peak 0 dBFS, 273 clipped (runs up to 21) | REJECT (designer) | Clipped; file deleted. |
| `hit-heavy` | t2 | 0.88 s, peak −1.6 dBFS, 0 clipped | REJECT (designer) | "Sounds hollow" (the prompt compared it to a drum). Kept until t3 is heard. |
| `hit-heavy` | t3 | 0.68 s, peak 0 dBFS, RMS −7.3, 2,055 clipped (flat runs up to 16) | listen | "Impact" prompt: sharp smack + dense low punch, no drum or resonance, influence 0.7. Measures clipped; the third and last take allowed before a substitute. |
| `hit-block` | t1 | 0.48 s, peak −49 dBFS | REJECT (designer) | Nearly silent; file deleted. |
| `hit-block` | t2 | 0.48 s, peak −30 dBFS, RMS −46 | listen | Deeper-thud prompt; 19 dB louder than t1 but still quiet: needs a free scripted gain or `volume_db` +12 in the cue table. |
| `sting-ryo` | t1 | 0.6 s, peak −11 dBFS, RMS −38 | ACCEPT (designer) | Quiet; the cue table can raise `volume_db`. |
| `sting-kitsune` | t1 | 0.68 s, peak −4.5 dBFS | ACCEPT (designer) | |
| `foxfire` | t1 | 1.48 s, peak 0 dBFS, 6 samples touching full scale (runs of 2) | ACCEPT (designer) | Trim −1 dB by script before use. |
| `ui-seal` | t1 | 0.48 s, peak −10 dBFS | ACCEPT (designer) | |
| `music-battle-grove` | t1 | MP3 192 kbps, 2.16 MB (~90 s) | ACCEPT (designer) | |

**Formats on Starter:** 44.1 kHz WAV and 192 kbps MP3 were both accepted, so no fallback was needed.

**Cost:** each sound effect returned its own `character-cost`: 5, 9, 5, ~6, 7, 15 and 5, about **52 credits** in all (~10 per second). The account counter lags about a call behind, so `el.py` now reads the header first. The music call returns no cost header; its 1,350 is the tool's estimate until the balance catches up (account at 1,892 used after the run, from 1,867 before). The cap counted 1,421 of 1,800.

## Licence

Account tier on 2026-11-09: **Starter** (active, 38,200 credits a month, resets 2026-11-20). Third-party summaries say Starter allows commercial use, but not distribution of music to streaming services; self-serve music terms may exclude studio games. The designer confirms ElevenLabs' current terms before release. Commercial use in a shipped game needs a paid plan (AM1 licence clause, kept by AM22).

**Retake 2026-11-09** (designer: "all is good other than the hits, do a retake with more thud sounds"): `hit-light`, `hit-heavy`, `hit-block` t2, 19 credits from the cost header, under a 100-credit cap. `hit-medium`'s prompt got the same change; it is not run yet.

**Take 3, 2026-11-09** (designer: "heavy hit sounds hollow, let's get some impact sounding sfx"): `hit-heavy` t3, 7 credits (header), cap 50. `el.py` first wrote it as `t1`, because take numbers came only from the files and the rejected t1 had been deleted. Fixed: numbers now also come from the run log; the file was renamed and the log line corrected.

**Heavy variants, 2026-11-09** (designer lifted the 3-take limit for the hits; cap 60, 24 credits by header). Energy is "90% of the sound's energy has landed by":

| Job | Take | Measured | Verdict | Notes |
|---|---|---|---|---|
| `hit-heavy-a` | t1 | peak −22.7 dBFS, 0 clipped, 90% by 36 ms | listen: `hit_heavy_a_t1_norm.wav` | The tightest impact so far. Copy raised +21.7 dB to −1 dBFS with a 10 ms fade-out (`el.py normalize`, free). |
| `hit-heavy-b` | t1 | peak 0, 1,135 clipped (runs up to 24), 90% by 132 ms | listen | Meaty, but clipped as generated. |
| `hit-heavy-c` | t1 | 90% by 599 ms | REJECT | Its weight comes at the end, so it would land late on the impact frame. |
| `hit-heavy-d` | t1 | peak 0, 2,581 clipped (runs up to 23), 90% by 142 ms | listen | Crunchy, but clipped as generated. |

For comparison: t2 ("hollow") reaches 90% by 107 ms; t3 by 238 ms, and it is clipped. ElevenLabs output that comes back loud tends to come back clipped whatever the prompt says, so quiet, clean takes raised by `normalize` are the safer route.

**Less tinny, 2026-11-09** (designer: "raise the quiet block and try give me a heavy punch that is less tinny sounding"). New free tools in `el.py`: `bands`, and `eq` (high-pass, low-pass, low-shelf, then normalize). "Body" is the RMS from 80 to 250 Hz, the thump you hear; "sub" is the RMS below 50 Hz, which most speakers barely play.

- **Why a sounds tinny:** 69% of its energy is below 50 Hz and only about 6% is at 100–250 Hz, so speakers play mostly its click.
- **Block:** `block_t2_norm.wav`, raised +29.3 dB to −1 dBFS with a 10 ms fade (free). Its background hiss is raised by the same amount.

| File | Made by | Body | Sub | Verdict |
|---|---|---|---|---|
| `hit_heavy_a_t1_eq.wav` | `eq --highpass 55 --low-shelf 180 6 --lowpass 6000` (free) | −30.8 (norm copy −34.6) | −32.9 (was −20.3) | listen |
| `hit_heavy_e_t1_norm.wav` | new take e (heavy leather bag), came back at −36.7 dBFS, raised +35.7 dB | **−23.3** | −30.9 | listen: the strongest clean body; its quietest 50 ms sits at −43 dBFS, so check the tail for hiss |
| `hit_heavy_f_t1.wav` | new take f (fist into a sandbag), as generated | −27.2 | −28.9 | listen: clean, peak −1.5, no clipping, noise floor −81 dBFS |
| `hit_heavy_g_t1` | new take g (kung-fu "whump") | −82.7 | −66.3 | REJECT: came back nearly silent; deleted |

Takes e, f and g cost 18 credits by the cost header, under a 40-credit cap.

**Heavy hit chosen, 2026-11-09** (designer: "hit_heavy_f_t1 is good"): `hit-heavy-f` t1 ACCEPT. The other heavy candidates (t2, t3, a, a-eq, b, d, e) are rejected and their files deleted; the run log keeps their history.

| Hit sound | Accepted file |
|---|---|
| Heavy | `game/sound/sfx/hits/hit_heavy_f_t1.wav` |
| Light | `game/sound/sfx/hits/hit_light_t2.wav` (accepted 2026-11-09; trim −1 dB before use) |
| Block | `game/sound/sfx/hits/block_t2_norm.wav` (accepted 2026-11-09) |

**Light and block accepted, 2026-11-09** (designer, after hearing them in a fight: "sfx are good"). The unused `block_t2.wav` is the raw take that the normalized copy came from.

**Actual spend so far** (balance 2026-11-09: 3,112 used, from 1,867 before the sampler): **1,245 credits.** The sound effects come to 120 by their cost headers (sampler 52, retakes 19, 7, 24 and 18), so the 90 s battle-grove track cost about **1,125** (~12.5 credits/s, ~750 a minute; the estimate was 15/s).

**Stage music, 2026-11-10** (designer: "use appropriate music for each unique stage, that includes the four seasons of the Tanuki"; 6 jobs approved, cap 6,300). One track per stage; the tea house has one per season, played on the Tanuki's health beats like its screens (spring, then summer below 75%, autumn below 50%, winter below 25%) and cross-faded over 2 s. All four season prompts ask for D minor at 150 BPM so the fades line up. `music-battle-river` became `music-battle-swamp` (AM21) and `music-battle-tanuki` became the four season jobs.

| Job | Take | Stage (`FightStage.Kind`) | Verdict |
|---|---|---|---|
| `music-battle-grove` | t1 | `bamboo-grove` (Kitsune, Nine-Tails, apprentice) | ACCEPT (designer, 2026-11-09) |
| `music-battle-shrine` | t1, 90 s | `shrine-gate` (Oni) | ACCEPT (designer, 2026-11-12) |
| `music-battle-swamp` | t1, 90 s | `riverbank`, the night swamp (Kappa) | ACCEPT (designer, 2026-11-12) |
| `music-tanuki-spring` | t1, 60 s | `tea-house`, the whole fight | ACCEPT (designer, 2026-11-12) |
| `music-tanuki-summer` | t1, 60 s | not used | unused (designer, 2026-11-12) |
| `music-tanuki-autumn` | t1, 60 s | not used | unused (designer, 2026-11-12) |
| `music-tanuki-winter` | t1, 60 s | not used | unused (designer, 2026-11-12) |

**Cost:** the account moved 3,112 → 7,612 (**4,500 credits**) for the first five tracks; winter took the account to 8,362, **750** more, so **5,250 credits** of the 6,300 cap (12.5 credits/s, as for the grove). The first run stopped before winter. Music returns no cost header, and the account counter sat still for four calls, then moved by all five during the autumn call; el.py booked that 4,500 to autumn and counted 9,000. Fixed: a call without a header books its estimate, and the cap is checked against the larger of that ledger and the account's own movement since the run began. Winter was then run alone under the 1,800 left of the cap.

**Wiring:** `MusicPlayer.StageTrack` maps each stage to its track (the grove's theme for the placeholder and any unknown stage; the old Spirit Duel loop if a file is missing). The fight scene asks for its stage's track when it builds the stage. Tests: `game/tests/Audio/StageMusicTests.cs`.

**Tea house, one track, 2026-11-12** (designer, after playing the Tanuki fight: "keep the first music playing, the crossfade between them is not good so lets simply and just use the first music"). The tea house plays `tanuki_spring_t1.mp3` for the whole fight; the season change and its 2 s cross-fade are removed from `MusicPlayer` and the fight scene. The screens still change season on the health beats. Summer, autumn and winter are unused.

**Shrine and swamp accepted, 2026-11-12** (designer: "accepted"). **Hit sounds in the fight scene:** while there are no `data/sound/` cue tables, `FightScene.HitSoundFor` plays the accepted thuds: block on a blocked hit; heavy on a heavy move, a counter-hit or 80+ damage; light otherwise. The old nine-sample pack plays only if those files are missing. Not merged: the unused `block_t2.wav` (the raw take of `block_t2_norm.wav`) and the unused tea-house summer, autumn and winter tracks stay out of the repo.
