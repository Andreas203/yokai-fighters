# Finish pipeline summary (tools/finish_clips.py)

| clip | body | clean | retarget | verdict | worst slide cm/f | frames | hit frames | prop check | notes |
|---|---|---|---|---|---|---|---|---|---|
| kappa-light-punch | kappa | ok | ok | PASS | 0.38 | 13 (trim 32-56) | 5-6 | - | jitter 5.91 |
| kappa-medium-punch | kappa | ok | ok | WARN | 1.25 | 21 (trim 79-119) | 7-9 | - | jitter 17.81 |
| kappa-heavy-punch | kappa | ok | ok | PASS | 0.81 | 34 (trim 26-92) | 11-14 | - | jitter 10.2 |
| kappa-light-kick | kappa | ok | ok | PASS | 0.51 | 16 (trim 58-88) | 6-7 | - | jitter 12.06 |
| kappa-medium-kick | kappa | ok | ok | WARN | 1.94 | 24 (trim 13-59) | 8-10 | - | jitter 10.19 |
| kappa-heavy-kick | kappa | ok | ok | WARN | 1.94 | 38 (trim 4-78) | 12-15 | - | jitter 10.19 |
| kappa-throw | kappa | ok | ok | PASS | 0.89 | 30 (trim 21-79) | 6-12 | - | jitter 4.58 |
| kappa-river-grab | kappa | ok | ok | PASS | 0.28 | 38 (trim 1133-1172) | 7-8 | - | jitter 1.91 |
| kappa-shell-spin | kappa | ok | ok | PASS | 0.88 | 48 (trim 69-146) | 11-28 | - | jitter 6.08 |
| oni-light-punch | oni | ok | ok | PASS | 0.37 | 13 (trim 188-212) | 5-6 | - | jitter 2.38 |
| oni-heavy-kick | oni | ok | ok | PASS | 0.30 | 38 (trim 172-246) | 12-15 | - | jitter 2.99 |
| oni-throw | oni | ok | ok | PASS | 0.67 | 30 (trim 137-195) | 6-12 | - | jitter 2.53 |
| oni-quake | oni | ok | ok | PASS | 0.15 | 56 (trim 140-250) | 28-31 | - | jitter 2.99 |
| oni-iron-club-charge | oni | ok | ok | PASS | 0.26 | 32 (trim 168-230) | 13-18 | - | jitter 2.99 |
| kitsune-light-punch | kitsune | ok | ok | PASS | 0.49 | 13 (trim 34-58) | 5-6 | - | jitter 5.91 |
| kitsune-fox-mirage | kitsune | ok | ok | PASS | 1.17 | 28 (trim 128-182) | 19-19 | - | jitter 6.94 |
| ryo-fox-mirage | ryo | ok | ok | PASS | 1.34 | 28 (trim 128-182) | 19-19 | - | jitter 6.94 |
| ryo-river-grab | ryo | ok | ok | PASS | 0.38 | 38 (trim 1128-1172) | 7-8 | - | jitter 1.91 |
| ryo-shell-spin | ryo | ok | ok | PASS | 0.90 | 48 (trim 72-149) | 11-28 | - | jitter 6.08 |
| ryo-oni-quake | ryo | ok | ok | PASS | 0.16 | 56 (trim 140-250) | 28-31 | - | jitter 2.99 |
| ryo-iron-club-charge | ryo | ok | ok | PASS | 0.25 | 32 (trim 168-230) | 13-18 | - | jitter 2.99 |
| kappa-hit-high | kappa | ok | ok | PASS | 0.45 | 33 (trim 0-64 of 112) | 59-78 | - |  |
| kappa-hit-low | kappa | ok | ok | PASS | 0.15 | 37 (trim 0-72 of 94) | 19-27 | - |  |
| kappa-knockdown | kappa | ok | ok | PASS | 0.07 | 49 (trim 0-96 of 508) | 145-163 | - |  |
| kappa-jump | kappa | ok | ok | WARN | 0.98 | 49 (trim 0-96 of 134) | 61-97 | - |  |
| oni-hit-high | oni | ok | ok | PASS | 0.73 | 33 (trim 0-64 of 112) | 59-79 | - |  |
| oni-hit-low | oni | ok | ok | PASS | 0.28 | 33 (trim 0-64 of 94) | 19-27 | - |  |
| oni-knockdown | oni | ok | ok | PASS | 0.12 | 49 (trim 0-96 of 508) | 145-164 | - |  |
| oni-jump | oni | ok | ok | WARN | 1.45 | 49 (trim 0-96 of 134) | 62-97 | - |  |

## Acceptance by the in-game QA (tools/rig_qa, YOK-246 Tripo takes)
Rule: after the hips fix, floats and sinks at most 4 cm, no body pair through each other 12.5 cm or deeper on 4 frames or more (the shipped Meshy takes run 8 to 12 cm, sleeves, fur and belly volume), and the total intersection score not far above the clip it replaces. The Blender `prop_clearance.py` numbers in the table above are not used (they read 40+ cm on the shipped Kappa idle).

| Result | Clips |
|---|---|
| Accepted (all fixed by a per-clip hips offset, FighterAnimSet) | kappa-light-punch (box_01), kappa-heavy-punch (box_03), kappa-light-kick (front_kick_01), kitsune-light-punch (box_01), kitsune-fox-mirage (flip), ryo-fox-mirage (flip), ryo-river-grab (lift_heavy), ryo-shell-spin (turn), ryo-oni-quake (chop), ryo-iron-club-charge (slash) |
| Rejected: the previous clip stays (data/clips and FighterAnimSet unchanged); manifest entries carry `rejected` with the reason | kappa-medium-punch, kappa-medium-kick, kappa-heavy-kick, kappa-throw, kappa-river-grab, kappa-shell-spin, kappa-hit-high, kappa-hit-low, kappa-knockdown, kappa-jump; oni-light-punch, oni-heavy-kick, oni-throw, oni-quake, oni-iron-club-charge, oni-hit-high, oni-hit-low, oni-knockdown, oni-jump |
