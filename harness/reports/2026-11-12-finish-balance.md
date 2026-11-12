# YOK-185 finish balance report: whole-run harness (2026-11-12)

**Sources.** Two 200-run batches, both from `YOK-finish-playable` at **d03c70bd** with no build step (the project was already built), Godot 4.7.2 mono console, headless, `--audio-driver Dummy`, `--runs 200 --seed 1 --hit-log`. Both ran in parallel and finished in about 20 s.

| Key | File (`harness/results/whole-run/finish-2026-11-12/`) | `--level` |
|---|---|---|
| NC | `newcomer-kihon-bot-n200-s1.json` | newcomer |
| SK | `skilled-kihon-bot-n200-s1.json` | skilled (also the proxy for Y4's "average" player) |

**Compared with:** `2026-10-21-yok167-balance-batch-3.md` (NC/SK at 566ee05) and the YOK-158 calibration note (11-02). Every number below was computed by script from the raw runs. The 95% intervals are Wilson intervals. **Calibration:** `production/playtests/` still does not exist, so the bot has never been calibrated against a real player (R5).

## 1. Summary

| Target | Target value | NC | SK | Verdict | Change since 10-21 |
|---|---|---|---|---|---|
| R5 reach Tanuki | 50–70% | **0/200** (CI 0–1.9) | 3/200, 1.5% (CI 0.5–4.3) | **FAIL** | NC 0.5% → 0%. SK 5% → 1.5% |
| R5 beat, of those who reach | 40–60% | n/a | 0/3 | Not measurable | SK 3/10 → 0/3 |
| Duel win rate | (≈90% needed) | 58.5% | 67.7% | | NC 61.6 → 58.5. SK 69.3 → 67.7 |
| Exchange ratio (hits) | ≈4.5 implied by Y4 | 1.38 | 1.87 | FAIL | Flat |
| R4 heal@4 + rest@7, at the Tanuki | ≈520 | none arrive | mean 349, median 424 (n=3, range 85–537) | FAIL | SK 397 → 349 |
| R4 row-4 arrival (implied ≈600) | | mean 434, median 448 (n=26 survivors, sd 216) | 473 (n=63) | FAIL (survivors only) | NC 389 → 434 |
| R4 other three routes | | untested: the bot always heals and rests | | No data | |
| Y4 apprentice (taken) | 110 / 80 | 346 (median 332, sd 147, n=200) | 275 (n=200) | FAIL ×3.1 / ×3.4 | ≈ same |
| Y4 yokai | 220 / 160 | all 522, wins 304 (n=256/81) | all 393, wins 322 (n=348/202) | FAIL ×2.4 / ×2.5 | NC worse (487 → 522) |
| Y4 elder | 280 / 200 | 671 (n=26, 1 win) | 651 (n=68) | FAIL ×2.4 / ×3.3 | SK worse (471 → 651) |
| Y4 Tanuki | 350 / 250 | n=0 | 349 (n=3) | Not measurable | |
| R9 run length | 25–40 min | none reach the Tanuki | 11.9 min (n=3, 11.0–12.8, with 45 s per menu node) | FAIL ×2–3 | Unchanged |
| R8 coins | 75 at row 4, +75 by row 7 | 75 (n=26, sd 0) | 75 (n=63). 150 at row 7 (n=5) | PASS by construction (shop untested) | |
| T2/T3 copy rule | correct target | n/a | **3/3 correct** (Spirit Wave Lv3 slot A, a tie broken by most recent levelling) | PASS (small n) | |
| Stalls | 0 | 0 | 0 | PASS | SK 25 timeouts → 0 (10-21 F6 resolved) |

## 2. Findings, ranked by impact on a first-time player

**F1. The run ends at row 2, and it is worse than on 10-21.**
- NC row-2 win rate: 28% (55/200, CI 22–34), against 37.5% on 10-21. 145 of 200 runs end there.
- Row 5 (elder): NC 1/26, SK 14/63 (22%, against 46% on 10-21).

| Yokai-tier profile (rows 2, 3, 6) | NC win (n) | SK win (n) | NC on 10-21 | Opponent's top damage source (NC) |
|---|---|---|---|---|
| kitsune-aggressive | **5% (2/41)** | 40% (17/43) | 11% | Foxfire 55% (about 436 per fight) |
| kitsune-patient | **0% (0/26)** | 44% (14/32) | 3% | Foxfire 83%. Blocks 57% of Ryo's Spirit Waves (86/151) |
| oni-aggressive | **7% (3/42)** | 26% (18/68) | 36–38% | **Iron-Club Charge 29%** (new kit, YOK-227/226) |
| oni-patient | 29% (12/41) | 50% (30/60) | 36–38% | Normals. Charge 18% |
| kappa-aggressive | 32% (18/57) | 74% (58/78) | 51–57% | Throw 70%, River Grab 30% |
| kappa-patient | 94% (46/49) | 97% (65/67) | 92–95% | |

- **Oni-aggressive collapsed** once the Oni got his specials: he went from 36% to 7% for the newcomer. This is the one regression since the last report with a clear cause.
- **The Kitsune is still a wall.** Neither of last report's Kitsune recommendations (#4, #5) has landed: `projectile_on_screen → block` is still 60 and mid-range Foxfire is still 40.

**F2. The gap is still global (as on 10-21).**
- Even the skilled bot's exchange ratio (1.87) is under half of what Y4 assumes for a newcomer.
- No data-only tuning of a few weights brings R5 into band with this bot. Last report's replay put all per-profile fixes together at about 2% newcomer reach.
- Whether the bot is too weak or the yokai too strong can only be settled by a playtest. **This is bot calibration first, then tuning.**

**F3. Elders.**
- **Nine-Tails:** NC 0/12, SK 2/32. Her reflect fires in every SK fight (125 reflects in 32 fights).
  - The bot spams Spirit Wave: it is 43% (NC) and 47% (SK) of Ryo's damage in non-apprentice wins.
  - So the twist does break the targeted habit. The bot never adapts to it, which is a **bot artefact**.
- **Elder Oni armour now fires:** in 5 of 14 NC fights and 15 of 34 SK fights. Win rates: NC 1/14, SK 13/34.
- **Elder Kappa:** 2/2 (n too small).

**F4. Habits (P6/Y2).**
- Now fire and are punished:
  - The apprentice: 3,322 fired, 56% punished. The 10-21 standing-trigger bug is fixed.
  - The Tanuki: 3 of 11 rolls fired in SK.
  - The Kitsune: 63–95% punished.
  - Kappa-patient: 100% punished.
- **Still dead:**
  - kappa-aggressive: 0 rolls in 57 / 78 fights.
  - Oni, both temperaments: 0–2 rolls in 41–86 fights.
- Fixing these means behaviour additions, not a single number (10-21 recommendations #6 and #7). That is **not for today**.

**F5. P5 single-move dominance (watch).**
- In won fights other than the apprentice, one special makes up more than 50% of Ryo's damage in 35% (NC) and 46% (SK) of them.
- The bot picks upgrades only (0 modifier picks), so this measures move efficiency, not a player's build.

**F6. R9.** Runs that reach the Tanuki take about 12 min, against 25–40. This is unchanged and needs the playtest's real fight and menu times. Do not tune it from bot data.

## 3. Recommendations: at most 3, data-only, ranked

Each one changes one weight in one profile file. The profile files are owned by habit-writer. None touches a shared move file: Foxfire serves Ryo, the Kitsune and the Tanuki copy (K3). Each also applies at the elder of the same yokai and temperament. The expected effects are **upper bounds**: they replay the recorded hit logs with that move's damage cut and no substitute action.

| # | File | Field | Old → new | Expected effect | Rule |
|---|---|---|---|---|---|
| 1 | `data/profiles/kitsune-aggressive.json` | `behaviours` `at_mid_range → special:foxfire` `weight` | **40 → 20** | Foxfire's share of her mid-range picks falls from 57% to 40%, so Foxfire damage is about ×0.7. Rows 2–3 win: NC 5% → ≤27% (11/41), SK 40% → ≤60%. Also eases the aggressive Nine-Tails (row 5). Same as 10-21 #5 | Y4, P6 |
| 2 | `data/profiles/oni-aggressive.json` | `behaviours` `at_mid_range → special:iron-club-charge` `weight` | **20 → 10** | The charge's share of his mid-range picks falls from 20% to 11%, so charge damage is about ×0.55. NC 7% → ≤17%, SK 26% → ≤38%. Partly undoes the post-YOK-227 regression | Y4 |
| 3 | `data/profiles/kitsune-patient.json` | `behaviours` `projectile_on_screen → block` `weight` | **60 → 30** | Spirit Waves blocked: about 57% → about 35%. Ryo's damage against her +25–40% (10-21 estimate; the hit log cannot replay it). Shorter fights (SK median 13,793 ticks, about 3.8 min). Same as 10-21 #4 | Y4, P6, R9 |

- **Verification is cheap.** Re-run both commands above after landing (about 20 s each).
- **Accept if:** NC row-2 win rises above 28%, and no single profile's NC win rate falls.
- **Expected result for R5 even with all three:** newcomer reach still in low single digits. These three make the build fairer; they do not make R5 pass.

**Needs the designer or code, not the next 30 minutes:**
- **Designer decision (Y4 design change, data-only but global).**
  - The knob: `data/map/node-scenes.json` `tiers.yokai` / `tiers.elder` `damage_dealt_pct` (absent = 100).
  - It is the only lever that moves R5 materially. The 10-21 replay: 50 → NC about 7% / SK about 35%; 30 → NC about 30% / SK about 57%. These figures come from an older build.
  - I do not recommend landing it before a playtest calibration.
- **Habit-writer, after the export.** Habit triggers that never fire: Oni `projectile_on_screen → block`, Kappa-aggressive `guard_pct` (10-21 #6, #7). These are new behaviour, not a single number.
- **Designer.** An outside playtest (R5 calibration) is still missing. It decides whether the bot (`data/harness/bot-levels.json`, gameplay-programmer) or the fights get tuned.

## 4. Balance gate (G3: modifiers 14 → 8)

No data to support either choice. Neither bot picked a modifier (reward order is upgrade first), and modifier drafting showed no outcome effect on 10-21 (MOD vs NC, paired). The harness cannot inform this cut. It stays the designer's call.
