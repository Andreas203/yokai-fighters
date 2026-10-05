# Demo Asset Shortlist (YOK-29, scoped to YOK-39: Ryo vs Kitsune, bamboo grove at dusk)

Nothing has been bought. Spending is the designer's call. Rules: F2, F5 (no bespoke animation), G4, C4 (no paired throws), V9. UI art is out of scope.

Verification key: **V** = read on the store/licence page during this pass. **U** = unverified (from memory, search snippet, or the page did not say). Mixamo's site is a JS app and its help page returned 403, so its clip names are **U**. Open mixamo.com and confirm each name before relying on it.

## 1. Characters

| Option | Store / URL | Price | Licence | Format / rig | Retarget risk | Toon + ink fit |
|---|---|---|---|---|---|---|
| Kitsune - Fox Girl Fantasy (REC for Kitsune) | Superhive, https://superhivemarket.com/products/kitsune---fox-girl-fantasy---anime-character---game-ready | $35 (V) | "Royalty Free" (V). Exact redistribution wording U | FBX (V). 60,848 tris, 16 x 2K stylized PBR textures (V). Fully rigged (V). Humanoid / Mixamo bone names U. Tail bones U. Ships 12 animations (V) | High until tested. Tails are the G4 risk. Check that the rig maps to Godot SkeletonProfileHumanoid, and keep the tail as separate non-humanoid bones | Stylized anime look, so good. 60k tris is heavy; PBR textures must be swapped for the toon material |
| Stylized Anthropomorphic Fox (alt Kitsune) | itch, noskillmodelling | $15+ (V, via search snippet; page returned 404 on fetch) | U | FBX, GLB, BLEND (snippet). 7.7k tris. Rigged. Humanoid mapping U | Medium. Outfit is overalls and sneakers, so it reads as a modern mascot, not a kitsune. Poor fit | Good, low poly |
| Mixamo stock character, or an auto-rigged upload (REC for Ryo and for the human-guise fallback) | https://www.mixamo.com | Free | Royalty-free for commercial and non-profit games, no attribution. You may not redistribute raw character or animation files, so exported assets must ship only inside the game (V via Adobe community FAQ threads, search snippet; Adobe help page 403) | FBX, Mixamo humanoid skeleton. Auto-rigger accepts any humanoid mesh upload | Low, since the animations are authored for this skeleton | Neutral. Stock characters are realistic, so they need the toon shader. Ryo's exorcist look would need a retexture or a cheap outfit swap |
| LOWPO Samurai Character Pack (Ryo candidate) | Fab, https://www.fab.com/listings/3bb4ea45-8719-48c2-bbd3-42c543b04cf4 (page 403) | U | U | glTF, GLB, Blend; 5 rigged characters (snippet only) | Medium | Low poly suits toon, but it is a samurai, not an exorcist |
| Voxel Ronin Samurai (not recommended) | itch, mrmgames | $5.99 (snippet) | U | glTF, DAE, humanoid | Low | Voxel style clashes with the Kitsune |

Kitsune human-guise fallback (G4): a Mixamo stock or auto-rigged human with a fox-ear and tail mesh attached to a bone. Tail-free, so it cannot clip. Cost $0 on top of Mixamo.

## 2. Animation

| Option | Store / URL | Price | Licence | Format / rig | Content (relevant) | Retarget risk | Notes |
|---|---|---|---|---|---|---|---|
| Mixamo animation library (REC) | https://www.mixamo.com | Free | As above (V via snippet) | FBX, "with skin" or without, "in place" toggle exists (U) | Punches, kicks, hit reactions, block, knockdown, get-up, death, victory, magic cast: all expected in the library, exact names U | Low on humanoid, medium on Kitsune tails | Mixamo is the only free source of all six normals plus specials. No grab clip is known to exist (GAP, see section 5) |
| RPG Animations GLB FREE (BACKUP and gap filler) | https://store.godotengine.org/asset/explosive-llc/rpg-character-animations-pack-free/ | Free | MIT (V) | GLB, humanoid (V). v0.1.0, marked unstable by the publisher (V) | 64 unarmed clips (V): walk, run, punches, kicks, chained attacks, blocks, dodges, hit reactions, stunned, knocked down, getup, death, revive. Per-clip names U. Root motion vs in-place U | Low (built for Godot 4) | Directly usable in Godot. Probably lacks a special-style cast or uppercut |
| Melee Combat Strikes (BACKUP, paid) | https://rapamotion.itch.io/meleestrikes | $39.99+ (V) | Not stated on page (U). Ask the author | FBX only; Mixamo, UE4/5 and Unity Dude skeletons (V). 116 clips (V) | Jabs, hooks, uppercuts, haymakers, front kicks, side kicks, spinning backfists, a hit reaction per strike (V). No block, knockdown or wake-up listed (U) | Low (Mixamo skeleton) | Best normals and uppercut coverage, but weak on reactions. Combine with Mixamo |
| Human Melee Animations (rejected) | https://kevdev.itch.io/human-melee-animations | $23+ (V) | U | Godot retarget tutorial included (V) | Weapon-based (swords, shields, polearms) | Low | No unarmed kicks or punches, so it does not fit |
| Fighting Animset Pro (Unity) | https://marketplace.unity.com/packages/3d/animations/fighting-animset-pro-64666 | U (page not fetched) | Unity Asset Store EULA; use outside Unity is U | FBX | 230+ unarmed clips: punches, kicks, specials, hits, knockouts, get-ups, blocks, dodges (snippet) | U | Strongest single match on paper, but the licence for Godot use is unclear. Check before any consideration |

## 3. Stage: bamboo grove at dusk

| Option | Store / URL | Price | Licence | Format | Notes |
|---|---|---|---|---|---|
| Bambo Tree 3D (REC) | https://kag3d.itch.io/bambo-tree-3d | $1+ (V) | U (page doesn't state; ask author / check download README) | GLB and FBX (V) | A single bamboo model. Build the grove with MultiMesh, add ground and sky in Godot. Dusk comes from light, fog and a gradient sky, so no pack is needed. Dusk look and toon match depend on our shader |
| NinjaFroggy Stylized Bamboo Pack (alt) | Fab, https://www.fab.com/listings/3e4c58a9-6f07-4dfc-9523-deb2ca91daa8 | U | U | FBX + PNG (V). 470 tris per shoot, 3 clump variants, demo scene (V) | Stylized and lower poly. Price and licence unknown. Fab pages return 403 to my fetch tool, so confirm in a browser |
| Japanese Bamboo Path 3D Scene | https://superhivemarket.com/products/japanese-bamboo-path-3d-scene | U | U | blend, fbx, obj (snippet) | Bamboo, pines, lanterns. A fuller scene, but the style is probably realistic; price U |
| Kenney Nature Kit (ground and rocks filler) | https://kenney.nl | Free | CC0 (known; not re-fetched this pass, **U**) | glTF / FBX | Optional ground props |

Camera: the stage is a flat floor with bamboo columns along a back plane; the side-view needs only a ~10 m wide strip, so a single MultiMesh scatter is enough.

## 4. Audio (low priority)

| Option | URL | Price | Licence | Content |
|---|---|---|---|---|
| Kenney Impact Sounds (REC) | https://kenney.nl/assets/impact-sounds | Free | CC0 (V) | 130 files, ZIP. Impacts and foley: use for hit thuds and block sounds. Whooshes are not confirmed in this pack (U), so listen first |
| Kenney Voiceover Pack: Fighter (optional) | kenney.nl | Free | CC0 (snippet) | Fighting game voice lines; not needed for the demo |
| Taiko drums (seamless loop) (REC) | https://opengameart.org/content/taiko-drums-seamless-loop | Free | CC-BY 3.0, attribution required (V) | OGG, 10.6 MB, loops, taiko with bells and shouts (V). Credit jobro in the credits |
| Whooshes | none found | - | - | Cover with Kenney Impact Sounds or generate in Audacity. GAP, low priority |

Bonus: no pack found that covers Oni, Kappa or Tanuki at no extra cost. The RPG Animations pack (MIT) and Mixamo clips would retarget to them, but the models are not covered.

## 5. Coverage: every `D` row in clip_needs.md

Recommended set = Mixamo clips (names are expected and **U** until browsed), with the free RPG Animations GLB pack as the in-engine backup. "Gap" means no clip is confirmed.

| ID | Need | Recommended clip (Mixamo, U) | Status / fallback |
|---|---|---|---|
| R01 | Idle | "Boxing Idle" or "Fighting Idle" | Expected. Seamless loop |
| R02 | Walk fwd | "Walking" with a guarded stance, or "Boxing Walk" | Expected; check for in-place. G4 gate clip |
| R03 | Walk back | Reverse R02 in Godot, or "Walking Backwards" | Expected |
| R04 | Dash | "Running" start, or "Quick Jab Run" style | Expected; trim to 18f |
| R05 | Jump | "Jump" | Expected |
| R06 | Crouch | "Crouch Idle" plus "Crouch Turn" or similar | Expected; in/out transitions must be trimmed |
| R07 | Block high | "Standing Block" or "Boxing Block" style | Expected, U |
| R08 | Block low | "Crouching Block" | **GAP risk.** Fallback: R06 crouch hold plus a flex |
| R09 | Hit high | "Head Hit" or "Receive Punch to the Face" | Expected |
| R10 | Hit low | "Stomach Hit" or "Hit To Body" | Expected |
| R11 | Knockdown | "Falling Back Death" or "Knocked Down" | Expected; prefer the side variant for tails |
| R12 | Wake-up | "Getting Up" | Expected |
| R13 | KO | "Dying" or "Standing React Death" | Expected |
| R14 | Win pose | "Victory Idle" | Expected. Fallback: R01 plus VFX |
| R15 | Binding | Composed from the finisher and R13 | No new clip, VFX only |
| R16 | Burst | None | **GAP.** Fallback per clip_needs: R07 pose plus shockwave VFX |
| T01 | Generic grab | None confirmed | **GAP.** Mixamo has no clear single-fighter grab. Fallback: a lunge-forward arm reach (jab clip trimmed) or the RPG Animations pack; Movesmith flags startup delta (C4 startup 5) |
| T02 | Throw break | None | Fallback per clip_needs: both play R09 pushback |
| N01 | Light punch | "Jab" or "Lead Jab" | Expected |
| N02 | Medium punch | "Cross Punch" | Expected |
| N03 | Heavy punch | "Hook Punch" or "Uppercut" | Expected. G4 gate clip |
| N04 | Light kick | "Low Kick" or a quick "Mma Kick" | Expected, U |
| N05 | Medium kick | "Front Kick" or "Mma Kick" | Expected, U |
| N06 | Heavy kick | "Roundhouse Kick" or "Hurricane Kick" | Expected; largest tail risk |
| S01 | Spirit Wave | "Standing 2H Magic Attack" or "Cast Spell" | Expected, U |
| S02 | Rising Talisman | "Uppercut" | Expected, U. If used for N03, use "Mma Uppercut" or RapaMotion's uppercut as the gap filler |
| S03 | Foxfire | Magic one-hand cast, e.g. "Standing 1H Magic Attack" | Expected, U |

Summary: 3 true gaps (R16, T01, T02) all have fallbacks in clip_needs.md; R08 and N04-N06 depend on the exact Mixamo names. Spend nothing before someone browses Mixamo for these names (about 15 minutes).

## 6. Totals (options, not a decision)

| Set | Contents | Estimated spend |
|---|---|---|
| **Recommended** | Mixamo (animations + Ryo + human-guise fallback, free) + Superhive Kitsune $35 + Bambo Tree $1 + Kenney Impact Sounds (free, CC0) + OGA taiko loop (free, CC-BY) | **~$36** |
| Backup | Mixamo (free) + RPG Animations GLB (free, MIT) + RapaMotion Melee Strikes $39.99 + itch fox $15 + Bambo Tree $1 + same free audio | **~$56** |
| Zero-spend | Mixamo (stock human wearing fox ears and tail attachment as Kitsune) + free bamboo from Godot-built primitives + free audio | $0 |

## 7. Open items for the designer

1. Confirm Mixamo's exact clip names by browsing (U above), especially crouch block and the magic cast clips.
2. Download the Superhive Kitsune's free preview or ask the seller whether the rig is Mixamo/humanoid-compatible and where the tail bones sit, before paying $35. This is the largest risk (G4).
3. Confirm the Bambo Tree licence text before the public showcase.
4. Check that Mixamo's "no redistribution of raw files" clause is met: ship the Godot export only, and keep raw FBX out of any public repo.
