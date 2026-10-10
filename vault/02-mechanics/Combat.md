---
title: Combat
status: draft
source: GDD §3.1, §3.2, §3.4, §3.5, §10.1
updated: 2026-10-10
---

# Combat

## Fight basics

- A fight is one round at 60 frames per second, and the loop is deterministic (C1).
- Ryo has 1,000 health, which carries across the whole run (C1). Meter does not carry over.
- Gameplay happens on a flat 2D plane; 3D is presentation only (F4).

| System | Rule |
|---|---|
| Movement | Walk crosses the screen in about 2.5 seconds; dash 18 frames; jump 40 frames of airtime |
| Blocking | Hold back. Lows must be blocked crouching (the Oni Quake hits low) |
| Throws | Startup 5, break window 7 frames by pressing throw, 120 damage |
| Meter | Three bars of 100. +6 per hit landed, +3 per hit blocked or taken. An EX special costs 1 bar |
| Burst | Once per fight. Breaks a combo with 20 invulnerable frames; costs all current meter |
| Counterhits | Hitting an opponent mid-attack deals +20% damage and +6 frames of hitstun |

## Ryo's normals

| Normal | Startup / active / recovery | Damage | Use |
|---|---|---|---|
| Light punch | 4 / 2 / 7 | 30 | Fastest button; interrupts pressure |
| Medium punch | 6 / 3 / 12 | 50 | Mid-range poke |
| Heavy punch | 10 / 4 / 20 | 80 | Big punish; anti-air if timed early |
| Light kick | 5 / 2 / 9 | 30 | Quick low |
| Medium kick | 7 / 3 / 14 | 55 | Longest-range poke |
| Heavy kick | 12 / 4 / 22 | 90 | Knockdown |

## Ryo's starter specials

Ryo starts every run with two specials in his four slots.

| Special | Startup / active / recovery | Damage | What it does |
|---|---|---|---|
| Spirit Wave | 13 / — / 30 | 60 | Projectile |
| Rising Talisman | 5 / 8 / 28 | 90 | Anti-air; invulnerable to air attacks on frames 1–5 |

## Throws are generic

The attacker plays a grab clip and the defender plays a standard knockdown (C4). There are no paired throw animations anywhere, so any special, throws included, can be copied by the Tanuki.

## Frame data

- Every move has startup (before it can hit), active (when it can hit) and recovery (when the fighter is stuck finishing it) (F1).
- Clip first: the move's clip is generated and measured, and only then is frame data written from that clip's real timing (F2).
- Animations are stepped by exact frames with `AnimationPlayer.Seek()`, and hitboxes are 2D rectangles stored in move data, not physics shapes (F3).
- A debug overlay draws hitboxes for the daily spot-check.

## Cancels

A cancel skips a move's recovery by starting another the instant the first hits, so the two combo. Ryo cannot cancel anything at the start of a run. The cancel rules themselves are in [[Abilities#Cancel rules]].

## Open questions

- None on this note. Engine defaults the GDD leaves open are recorded in `docs/design/rules.md` section E.
