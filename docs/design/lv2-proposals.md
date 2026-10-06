# Lv 2 proposals and Foxfire card (YOK-43)

Status: the Spirit Wave and Rising Talisman Lv 2 steps are **final** (designer decision YOK-41, 2026-10-06, rule E21) as relative steps on whatever the clip-derived Lv 1 recovery is; the frames text updates when Lv 1 is re-derived. Foxfire's Lv 2 is still `proposed` (YOK-45). One tuning step per move (A4); Lv 3 keeps it (E17). Card text lives in each special's `card` block (E20). The Lv 1 files are YOK-33's (Ryo) and YOK-45's (Kitsune), so these blocks are for them to take once decided.

## Spirit Wave: recover 4 frames faster

Reasoning: the Lv 1 wave leaves 30 frames of recovery, so Ryo can't follow it up. At 26 he can walk in behind it or throw a second one the moment the first leaves the screen. It is the A4 example step and shows in the frame data (30 -> 26).

```json
"2": {
  "status": "final",
  "tuning": { "target": "recovery", "op": "add", "value": -4 },
  "card": {
    "plain": "Lv 1 → Lv 2: Spirit Wave recovers 4 frames faster, so you can follow it in.",
    "frames": "13 / — / 26 frames (recovery -4), 60 damage, projectile"
  }
}
```

## Rising Talisman: recover 6 frames faster

Reasoning: at 28 recovery a whiffed or blocked anti-air is a free punish, so players hold it back. At 22 it is a read they can risk, which changes when they press it rather than only how hard it hits. Damage (90) and the air-invulnerable frames 1-5 stay as in the specials table.

```json
"2": {
  "status": "final",
  "tuning": { "target": "recovery", "op": "add", "value": -6 },
  "card": {
    "plain": "Lv 1 → Lv 2: Rising Talisman recovers 6 frames faster, so a missed anti-air costs less.",
    "frames": "5 / 8 / 22 frames (recovery -6), 90 damage, air-invulnerable frames 1-5"
  }
}
```

Alternative if the designer prefers damage: `{ "target": "damage", "op": "mul_pct", "value": 110 }` (90 -> 99). It changes no decision, so it is not recommended (P1).

## Foxfire (YOK-45's file) `card` block

```json
"card": {
  "plain": "Launch a slow fox flame that drifts across the screen.",
  "frames": "15 / 4 / 30 frames, 70 damage, slow projectile"
}
```

Lv 3 evolution card (from rules.md): plain "Your foxfire passes through projectiles.", frames "15 / 4 / 30 frames, 70 damage, passes through projectiles".
Foxfire's Lv 2 step is not in this ticket's scope.

## Modifier cards

| Modifier | plain | frames |
|---|---|---|
| Will-o'-wisp | Wisp-light lends your projectile wings: it flies faster across the screen. | Projectile speed +30% (x1.3, rounded to nearest) |
| Fox's Patience | The fox waits and learns: this special builds more spirit meter when the foe blocks it. | Meter gain on block +25% (C5 default 3 becomes 4, rounded to nearest) |
