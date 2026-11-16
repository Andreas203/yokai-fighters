# AM27 merchant balance report: whole-run harness (2026-11-16)

**Sources.** Four 200-run batches on each side, `--runs 200 --seed 1 --hit-log`, Godot 4.7.2, headless.

| Side | Build | Folder |
|---|---|---|
| AM27 | `YOK-merchant-always-stocked` (PR #285, head ad625d19) | `harness/results/whole-run/am27-2026-11-16/` |
| main | b25858cb, before AM27 | scratch copy, not in the repo |

| Key | Level | Merchant / row 7 |
|---|---|---|
| NC, SK | `newcomer`, `skilled` (`data/harness/bot-levels.json`) | heal at row 4, dojo rest at row 7 |
| NC-shop, SK-shop | same fight numbers, levels file [`shop-levels.json`](../results/whole-run/am27-2026-11-16/shop-levels.json) | shop at every merchant, row-7 merchant over the dojo |

Every number below was computed by script from the node records. The 95% intervals are Wilson intervals. **Calibration:** `production/playtests/` does not exist, so every number here is **bot-uncalibrated** (R5).

**How the empty shelf was measured.** The node record lists `bought` only when the bot bought something. So I rebuilt main's shelf from each run's bindings: the unlocked commons of unbound yokai that Ryo does not hold. The rebuild matches the record exactly. All 27 main shop stops with no purchase were empty shelves by the rebuild, and every rebuilt empty shelf had no purchase.

## 1. Summary

| Target | Target | NC / SK (heal route, both sides) | SK-shop main → AM27 | Verdict |
|---|---|---|---|---|
| Standard batches unchanged | identical | **byte-identical files** on both sides | n/a | PASS |
| R5 reach Tanuki | 50–70% | NC 0/200 (CI 0–1.9). SK 6/200, 3% (CI 1–6) | 3/200 → 4/200 (CI 1–5) | **FAIL**, no change |
| R5 beat, of those who reach | 40–60% | SK 0/6 | 0/3 → 0/4 | Not measurable |
| R4 heal@4 + rest@7, at the Tanuki | ≈520 | SK mean 393, median 419 (n=6). No NC arrives | n/a | FAIL (as on 11-12) |
| R4 shop@4 + rest@7 | ≈270 | no bot plays this route | estimate in §2 F2 | Not measured |
| R4 shop route at the Tanuki (shop@4 + shop@7) | (no target) | n/a | mean 332 → 352, median 178 → 253 (n=3 → 4) | Below the heal route |
| Shelf empty at a shop stop | 0 (AM27) | n/a | row 4: 17/73 → **0/73**. Row 7: 10/12 → **0/13** | **PASS** |
| R8 coins | 75 at row 4, +75 by row 7 | 75 / 150 | 75 / 100, all spendable on AM27 | PASS |
| R9 run length | 25–40 min | SK 10.9–12.8 min (n=6) | 10.6–11.4 min (n=4) | FAIL, unchanged |
| T2/T3 copy rule | correct target | 6/6 | 3/3 → 4/4 correct | PASS |
| Y4 damage taken (NC) | 110 / 220 / 280 apprentice / yokai / elder | 346 / 493 / 638 | n/a | FAIL, unchanged from 11-12 tuned |

## 2. Findings, ranked by impact on a first-time player

**F1. AM27 does what it says, but a newcomer almost never sees it.**
- NC-shop: 26 of 200 runs reach row 4. On main, 1 of those 26 found an empty shelf; on AM27 that run bought Foxfire. No NC-shop run reaches row 7.
- All 200 NC-shop runs are identical on both sides up to row 4, and every run ends on the same row.
- For SK-shop, every AM27 shop stop bought at least one item (86/86). On main, 27 of 85 stops had an empty shelf (row 4: 23%, row 7: 83%). That matches AM27's "11 of 20 walks".
- What AM27 sold in the stops that were empty before:
  - row 4: Foxfire, 17 of 17 (the buyable-now special first);
  - row 7: Fox's Patience + Iron Will, 13 of 13.
- Coins now get spent. On the SK-shop route, coins left at run end fall from a mean of 45 to 36 (main max 200, AM27 max 75).

**F2. Shopping is not a viable route against healing. The cost is attrition, not arrival health.** (SK, AM27)

| | Heal route (heal@4, rest@7) | Shop route (shop@4, shop@7) |
|---|---|---|
| Row-5 arrival | 688 | 452 |
| Row-5 (elder) win | 19/73, 26% (CI 17–37) | 16/73, 22% (CI 14–33) |
| Row-7 arrival (survivors) | 317 | 322 |
| Row-8 arrival | 605 | 322 |
| Row-8 (elder) win | 6/12, 50% (CI 25–75) | 4/13, 31% (CI 13–58) |
| Reach / beat the Tanuki | 6 / 0 | 4 / 0 |
| Tanuki arrival | mean 393, median 419 | mean 352, median 253 |

- The row-4 heal (+236 mean) buys about 3 more row-5 survivors in 73. The survivors then reach row 7 equally healthy (317 against 322), because only healthy runs survive.
- The row-7 rest (+300) is worth far more than two modifiers.
- **R4's shop@4 + rest@7 ≈ 270 cannot be measured: no bot level plays that route.** An estimate takes SK-shop's 13 row-7 arrivals and adds the rest's 300 (capped at 1000). That gives a row-8 arrival of mean 612, about the heal route's 605, because of the same survivor selection. So the ≈270 line describes the run that survives. The cost of the route shows up as fewer runs reaching row 7, not as lower health on arrival.

**F3. The row-7 top-up sells modifiers that change nothing in the bot's fights (bot artefact).**
- The bot buys the first item on the shelf that fits. At row 7 that is Fox's Patience (+25% meter on block) and Iron Will (projectile interrupt immunity).
- Paired by seed, main against AM27, 11 of the 12 row-8 fights have the same ticks, damage dealt and damage taken. In the twelfth (seed 91), the damage taken falls from 120 to 0; that run lost to the Tanuki on both sides.
- So the shop route's row-8 weakness here is the bot's buying rule plus the missing heal. It is not evidence that AM27's stock is too weak or too strong.

**F4. Row-4 Foxfire top-up: no measurable effect.**
- The 17 SK runs whose row-4 shelf was empty on main bought Foxfire Lv 1 on AM27.
- Row-5 win in those 17 runs: 3/17 → 2/17. Damage dealt per fight 417 → 344. Overall: 1 run went deeper (seed 49 reached the Tanuki), 2 ended earlier. That is noise at this n.

**F5. Copy rule (P5, T2/T3).**
- All AM27 Tanuki locks are correct (4/4).
- 3 of the 4 locked a special bought at row 4 and then levelled to Lv 3 by reward cards (Iron-Club Charge ×1, Foxfire ×2). This follows T2 (highest level in a slot), but a player may be surprised by it.
- Single-special dominance in won fights, rows 2–8: shop route 41%, heal route 44%. Spirit Wave leads on both. Unchanged.

**F6. Y4 artefact to ignore.** Damage taken in shop-route elder fights looks lower (362 against 594). The cause: a fight ends at death, so damage taken is capped by the lower arrival health. The elders are not easier.

## 3. Recommendations (designer's call; all bot-uncalibrated)

No game-data tuning is warranted. AM27 is neutral on R4, R5 and R9 in this data. Both changes below are harness levels; they tune no number in the game.

| # | Owner | Change | Expected effect | Rule |
|---|---|---|---|---|
| 1 | gameplay-programmer | Add a `skilled-shop4-rest7` level to `data/harness/bot-levels.json`: `run.merchant: "shop"`, default route (dojo at row 7) | Measures R4's ≈270 line directly, instead of F2's estimate | R4 |
| 2 | gameplay-programmer | Add a mixed level: `merchant: heal_if_hurt_else_shop` (already in `RunBot`) with one number, `heal_below_pct` = 50 | The route a real player is likely to take. Expect R5 between the heal and shop routes | R4, R6 |

- **Not recommended now:** changing the bot's first-that-fits buying rule (F3). It is a code change, and it should wait for a playtest that shows what players actually buy.
- **Untested:** the AM27 side effect of releasing a drafted bound special and buying it back. The bot never releases.

## 4. Balance gate (G3: modifiers 14 → 8)

No data to support either choice.
- On a fresh save only 6 of the 14 modifiers are unlocked, so the harness never sees the other 8.
- AM27 raised the bot's modifier buys from 4 to 26, all at row 7, with no outcome effect (F3).
- The cut stays the designer's call.

Raw runs: `harness/results/whole-run/am27-2026-11-16/` (AM27) and its `main-b25858cb/` (main before AM27). Scripts: `tools/am27_analysis.py` and `tools/am27_paired.py` in that folder (`python <script>`).
