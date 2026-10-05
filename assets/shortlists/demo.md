# Demo Asset Shortlist (YOK-29, scoped to YOK-39: Ryo vs Kitsune, bamboo grove at dusk)

Nothing has been bought. Spending is the designer's call. Rules: F2, F5 (no bespoke animation), G4, C4 (no paired throws), V9. UI art is out of scope.

Verification key: **V** = read on the store/licence page during this pass. **U** = unverified (from memory, search snippet, or the page did not say). Mixamo's site is a JS app and its help page returned 403, so its clip names are **U**. Open mixamo.com and confirm each name before relying on it. Where a row says "V, snippet" the fact came from a search result snippet, not the page itself.

## Changes since first pass (second pass, 2026-10-05)

- Read the Ryo and Kitsune sketches. The old Ryo pick (stock Mixamo human) does not look like the sketch. The old Kitsune pick (Superhive, $35) cannot be shown to match: its page does not state the outfit, mask or tail count (V, see 1c).
- No purchasable model found that matches either sketch. The closest stock hits for Ryo are generic anime or ninja boys (1b).
- New recommended route for both characters: **generate from the sketch PNGs with Meshy, rig with Mixamo's auto-rigger, and test on the free tier first ($0)**. Paid fallback is Meshy Pro for one month ($20). Superhive Kitsune stays as the bought fallback.
- VRoid Studio (free) is the second route. It has the best toon fit and the easiest recolour, but it needs Blender kitbashing for the coat tail and the mask.
- Determinism: no model on any list has coat-tail or tail bones that we can verify. Mixamo's auto-rigger writes a fixed humanoid skeleton and would drop extra bones. They must be added afterwards in Blender and driven from the tick count (1e).
- Totals and Open items are rewritten. Animation, stage and audio sections are unchanged. The coverage table is unchanged (the sketches add no move).

## 1. Characters

First-pass table, edited in place. New material is in 1b to 1f.

| Option | Store / URL | Price | Licence | Format / rig | Retarget risk | Toon + ink fit |
|---|---|---|---|---|---|---|
| Kitsune - Fox Girl Fantasy (FALLBACK for Kitsune; no longer REC, fit to sketch not shown) | Superhive, https://superhivemarket.com/products/kitsune---fox-girl-fantasy---anime-character---game-ready | $35 (V) | "Royalty Free" (V). Exact redistribution wording U | FBX (V). 60,848 tris, 16 x 2K stylized PBR textures (V). Fully rigged (V). Humanoid / Mixamo bone names U. Tail bones U (page does not state them, V). Ships 12 animations (V) | High until tested. Tails are the G4 risk. Check that the rig maps to Godot SkeletonProfileHumanoid, and keep the tail as separate non-humanoid bones | Stylized anime look, so good. 60k tris is heavy; PBR textures must be swapped for the toon material |
| Stylized Anthropomorphic Fox (alt Kitsune) | itch, noskillmodelling | $15+ (V, via search snippet; page returned 404 on fetch) | U | FBX, GLB, BLEND (snippet). 7.7k tris. Rigged. Humanoid mapping U | Medium. Outfit is overalls and sneakers, so it reads as a modern mascot, not a kitsune. Poor fit | Good, low poly |
| Mixamo stock character, or an auto-rigged upload (REC as the rig and clip source; a stock character is now only the Ryo fallback; also the human-guise fallback) | https://www.mixamo.com | Free | Royalty-free for commercial and non-profit games, no attribution. You may not redistribute raw character or animation files, so exported assets must ship only inside the game (V via Adobe community FAQ threads, search snippet; Adobe help page 403) | FBX, Mixamo humanoid skeleton. Auto-rigger accepts any humanoid mesh upload | Low, since the animations are authored for this skeleton | Neutral. Stock characters are realistic, so they need the toon shader. Ryo's exorcist look would need a retexture or a cheap outfit swap |
| LOWPO Samurai Character Pack (Ryo candidate) | Fab, https://www.fab.com/listings/3bb4ea45-8719-48c2-bbd3-42c543b04cf4 (page 403) | U | U | glTF, GLB, Blend; 5 rigged characters (snippet only) | Medium | Low poly suits toon, but it is a samurai, not an exorcist |
| Voxel Ronin Samurai (not recommended) | itch, mrmgames | $5.99 (snippet) | U | glTF, DAE, humanoid | Low | Voxel style clashes with the Kitsune |

Kitsune human-guise fallback (G4): a Mixamo stock or auto-rigged human with a fox-ear and tail mesh attached to a bone. Tail-free, so it cannot clip. Cost $0 on top of Mixamo.

### 1b. Ryo: routes closer to the sketch

Sketch key points: indigo coat closed left over right with a long asymmetric tail and persimmon lining, high collar over the mouth, undercut with a lock over the right eye, shimenawa sash, kyahan, jika-tabi, ofuda on the right forearm. Palette: indigo #2D3A5E, ink #1D1B21, persimmon #D8632C.

| Option | Store / URL | Price | Licence | Format / rig | How close to the sketch | Retarget risk | Recolour / toon fit |
|---|---|---|---|---|---|---|---|
| **Meshy image-to-3D from `ryo-meshy-front.png`** (REC, test first) | https://www.meshy.ai | Free $0; Pro $20/mo (V, docs and snippet) | See 1d | GLB/FBX out (V, snippet). Rig: Mixamo upload or Meshy rig (1d) | Highest on paper, because the mesh comes from the sketch. The back of the coat, collar, hair and wraps are invented by the model (U until tried). Small parts (buttons, shide paper, ofuda) will blur | Medium. The coat tail is fused to the body and will stretch on kicks unless cut off in Blender (1e) | Albedo has lit shading baked in (U). Use albedo only. Flat colour regions in the sketch help |
| **VRoid Studio base + kitbash** (ALT) | https://vroid.com/en/studio | Free (V, snippet) | You set the terms for models you make (V, snippet). Third-party preset or shop items follow their own terms (V, snippet). VRoid's own preset-item terms for a commercial game: U (conditions page returned 403) | VRM (glTF 2.0 based). Godot 4 has a VRM importer addon with MToon shader (V, Godot asset library title and godot-vrm repo, snippet). Humanoid skeleton, bone names differ from Mixamo, so retarget with a Godot BoneMap | Medium. Hair with a lock over one eye, jacket and trousers by sliders and texture. The sash, kyahan, collar and long coat tail need Blender edits or a Booth clothing item | Medium. Needs a BoneMap to Mixamo names. **Do not enable VRM spring bones**: they are physics, not tick-driven | Best of all options: flat MToon textures, anime proportions, recolour by swapping a colour |
| Booth.pm VRM bases and clothing (ALT for VRoid) | https://booth.pm (e.g. https://booth.pm/en/items/4698286, not opened) | From ~2,500 JPY for a full kimono avatar (V, snippet) | Per seller. Some allow game production (V, snippet). Check each page; none verified for a male kimono coat | VRM | Unknown. No male closed-coat exorcist item was found (one search) | Same as VRoid | Same as VRoid |
| Stylized Toon Character "Bart" (not a match) | Fab, https://www.fab.com/listings/14b8f74d-eef1-48a4-96d4-9640640670cd | U | U | Fully rigged; Godot listed as supported (V, snippet). Rig type U | Low. Dark hero or demon archetype, not an exorcist | Medium | Made for cel shading (V, snippet) |
| Anime Demon Man (not a match) | itch, https://3dstudios.itch.io/anime-demon-man | $3 (V, 50% off from $6) | U | FBX and .blend (V). Rigged: not stated (V) | Low. Demon theme | U | Unknown |
| Stylized ninja boy rigged low poly | CGTrader, https://www.cgtrader.com/3d-models/character/fantasy-character/stylized-ninja-boy-rigged-low-poly-character | U (page returned no content) | U | FBX, hand-painted PBR (snippet only) | Low to medium. Ninja outfit, not a coat | U | Hand-painted textures recolour easily |
| Mixamo stock human + retexture | https://www.mixamo.com | Free | As above | Mixamo skeleton | Low. Gets only the palette, not the silhouette | Lowest | Realistic textures: needs a flat-colour retexture |

Not found in this pass (a few searches, so not proof): an onmyoji or exorcist coat character on Fab, Sketchfab store, Superhive, the Unity store or CGTrader. Fab and Unity pages return 403 to my fetch tool, so a browser search by the designer could still find one. The Unity store licence for non-Unity use was not checked (U).

### 1c. Kitsune: re-check and alternatives

Sketch key points: full white fox mask worn for the whole build, white hair tipped orange, fox ears, three tails (nine for the elder), cropped white haori with wide sleeves and red trim, indigo high-neck top, shrine-red hakama to the knee, suzu bell. Palette: fur white #FBF6EC, foxfire #F2A23A, shrine red #B5332B.

Re-check of Superhive "Kitsune - Fox Girl Fantasy" (page re-read, V): $35, FBX, 60,848 tris in 2 meshes, 16 stylized PBR textures at 2048, "Royalty Free", rigged, 12 animations (2 idle, jump, strafe walks, run, turns, 2 walks). The page does **not** state the outfit, a mask, the tail count, tail bones or the rig type. So haori, hakama, mask and three tails are all U. The 12 clips are locomotion only and add nothing to the fighting needs. A mask would need a separate prop.

| Option | Store / URL | Price | Licence | Format / rig | Fit to sketch | Tail / extra bones | Recolour / toon fit |
|---|---|---|---|---|---|---|---|
| **Meshy from `kitsune-meshy-front-notails.png`, tails and mask handled separately** (REC, test first) | https://www.meshy.ai | Free $0 / Pro $20 (shared with Ryo) | See 1d | GLB/FBX; rig through Mixamo | High for body, haori, hakama and mask (taken from the sketch) | None on the body mesh. Add 3 tails as separate meshes with chain bones (1e) | White, red and orange are flat colours, so easy |
| Meshy from `kitsune-meshy-front.png` (with tails) | same | same | same | same | Highest in one pass, if it works | Tails will probably come out fused to the back and skinned to the hips, so they stretch (U until tried). The Kitsune README already names the no-tails image as the fallback | Same |
| Superhive Fox Girl Fantasy (FALLBACK, bought) | link in table above | $35 | "Royalty Free" (V) | FBX, rig type U | Unknown (see above) | Not stated (V). Ask the seller | PBR with 16 maps: needs a toon material that ignores them. Recolour is harder |
| Kitsune Fox Mask (prop, optional) | RenderHub, https://www.renderhub.com/zelad/kitsune-fox-mask | U | U | FBX, 2,188 polys, PBR (snippet) | Mask only; attach to the head bone | None | Retexture to white with red marks |
| Low Poly Casual Fox Mask (prop, optional) | RenderHub, https://www.renderhub.com/revereel-studio/low-poly-casual-fox-mask | U | U | 526 polys, "rigged" (snippet) | Mask only | None | Low poly suits toon |
| Stylized Anthropomorphic Fox (itch) | in table above | $15+ | U | 7.7k tris | Poor (overalls) | U | Good |
| Nine-tailed fox statues (CGTrader) | https://www.cgtrader.com/3d-print-models/miniatures/figurines/nine-tailed-kitsune-3d-print-model | U | U | Print models, not rigged (snippet) | Reference for the elder only | None | Not for animation. Not recommended |

No miko-outfit pack or fox-tail-and-ear accessory pack with a verified licence was found (one search). VRoid Studio can make a miko-style body. Whether it has fox ears and tail options is U. A VRoid Kitsune needs the same Blender work as Ryo, so Meshy is ranked first.

### 1d. Meshy route (and two alternatives)

| Item | Meshy | Tripo | Rodin (Hyper3D) |
|---|---|---|---|
| Plans | Free $0 (100 credits/mo, no card), Pro $20/mo, Studio $60/mo, Enterprise (V, docs page and snippets) | Free, Pro $19.9/mo, Max $89.9, Team $109.9 (V, third-party price site, snippet). A price change in Aug 2026 is mentioned there (U) | Free (pay per download), Creator $24/mo, Business $120/mo (V, snippet) |
| Credit cost | Image-to-3D 25 credits (Meshy 7) or 20 (Meshy 6). Texturing 10. Remesh, rigging and animation are free (V, docs page). The free tier gives about 4 Meshy 7 generations a month. Pro credit count: U | Free: 200 credits, about 8 models (V, third-party site) | U |
| Output licence | Free plan: **CC BY 4.0, attribution needed, commercial use allowed**. Pro and Enterprise: private licence, full commercial use, no attribution. You own the output on every tier (V, docs page and snippets). Studio terms: U | Free: public models, CC BY 4.0. Commercial use needs a paid plan (V, third-party site; not checked against Tripo's own terms, U) | Commercial rights listed on Business and above (snippet). Creator terms: U |
| Student project + public showcase | The free tier is allowed with a credit line in the game credits. Pro for one month ($20) removes the credit. The input is the designer's own sketch. The legal status of AI-output copyright is not settled (U) | Same shape | Higher cost |
| Format | GLB and FBX (V, snippet) | U | U |
| Rigging | Meshy auto-rig: humanoid and quadruped, input should be close to T-pose or A-pose, GLB or FBX out (V, docs snippet). Not for props. Non-standard capes, tails or skirts can confuse body detection (V, snippet). The sketch PNGs are A-pose, so that is fine. Alternative: upload the unrigged model to Mixamo's auto-rigger (65-bone skeleton, V, snippet) | Has a rig feature (U) | U |
| Topology | Remesh presets 3K, 10K, 30K and 100K, quad or triangle (V, docs snippet). Use quads at 10K to 30K for animation | U | U |

Likely problems for toon shading (all U until tried): baked lighting in the albedo, noisy small details (buttons, shide), melted hands, a flat or wrong back of the coat, and uneven UV density. Fixes: albedo-only material, a 3-step ramp, and an outline from a back-face hull (needs clean normals, so remesh first).

How the sketch parts are handled:
- **Coat tail:** fused to the legs. Cut it off in Blender and bind it to 3 added chain bones (1e). If the designer skips Blender, it stays skinned to hips and thighs and will stretch on kicks.
- **Sleeves:** baked into the arms. They follow the arm bones and will not sway. That is acceptable for a toon look. Extra sleeve bones are optional.
- **Tails (Kitsune):** use the no-tails PNG and add three separate tail meshes with chain bones. Or generate one tail with Meshy and duplicate it (U).
- **Collar over the mouth, ofuda wraps:** come from the PNG. Shide and the held ofuda are not in the PNG, so add them as small props on bones if wanted.

Recommendation: spend the free tier first. About 4 generations cover Ryo and Kitsune (no tails) plus one retry each. If the result passes in Godot with the toon shader, decide between shipping with a CC BY credit and paying $20 for one Pro month.

### 1e. Determinism: bones for coat tail, tails and sleeves

Rule from both READMEs: no physics. Motion must be baked into clips or driven from the tick count.

Facts: Mixamo's auto-rigger writes a fixed 65-bone humanoid skeleton (V, snippet). Its guidance says to remove extra bones before upload (V, snippet), so extra bones are dropped. Mixamo clips animate only that skeleton. Extra bones therefore get no motion from any pack, and baking motion for them would be bespoke animation (F5, not allowed).

Suggested method (for gameplay-programmer, not tested): rig through Mixamo, open the rigged FBX in Blender, add chain bones (coat tail 3 bones, each Kitsune tail 3 bones, optional sleeve bones) parented to Hips or Spine, and export glTF. In Godot, set those bones each tick from a function of tick count, fighter velocity and facing (a sine sway plus lag). No physics node, no spring bones. Clip retargeting by bone name is not affected, since the extra bones are not in the clips.

| Model | Extra bones for coat tail / tails / sleeves | Does Mixamo's auto-rigger drop them? |
|---|---|---|
| Meshy Ryo | None. Add 3 coat-tail bones in Blender | Yes if added before upload. Add them after |
| Meshy Kitsune | None. Add 3 bones x 3 tails = 9 chain bones in Blender (nine tails for the elder = 27) | Yes, same |
| VRoid Ryo / Kitsune | VRM can carry spring bones for hair and clothes (physics). Which are set: U | Not applicable to VRM. Strip or disable the springs. Coat tail and tails still need adding |
| Superhive Kitsune | Not stated (V). Ask the seller | Rig already authored. Mixamo's rigger is not needed if it maps to humanoid |
| Mixamo stock human | None | n/a |

### 1f. Palette and recolour

| Option | Texture type | Recolour to Ryo #2D3A5E / ink / persimmon #D8632C and Kitsune white / #F2A23A / #B5332B |
|---|---|---|
| VRoid / VRM | Flat MToon textures (V, godot-vrm imports MToon, snippet) | Easiest. Change the colour in VRoid or in the texture |
| Meshy output | Baked PBR albedo, shading baked in (U) | Medium. Use albedo only and hue-shift masks. The sketch already carries the palette, so little recolour should be needed |
| Superhive Kitsune | 16 stylized PBR maps (V) | Harder. Needs a toon material that ignores the PBR set and a repaint of the albedo |
| Mixamo stock | Realistic PBR | Needs a full flat retexture |
| itch / CGTrader low poly | Hand-painted (snippet) | Easy |

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

Recommended set = Mixamo clips (names are expected and **U** until browsed), with the free RPG Animations GLB pack as the in-engine backup. "Gap" means no clip is confirmed. The sketches add no move, so this table is unchanged. One note: the Kitsune sketch holds foxfire palm-up and casts with a fox hand sign (S03). Mixamo has no finger-specific cast clip (U), so the hand sign is VFX only.

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
| N06 | Heavy kick | "Roundhouse Kick" or "Hurricane Kick" | Expected; largest tail risk, and now also the coat-tail risk |
| S01 | Spirit Wave | "Standing 2H Magic Attack" or "Cast Spell" | Expected, U |
| S02 | Rising Talisman | "Uppercut" | Expected, U. If used for N03, use "Mma Uppercut" or RapaMotion's uppercut as the gap filler |
| S03 | Foxfire | Magic one-hand cast, e.g. "Standing 1H Magic Attack" | Expected, U |

Summary: 3 true gaps (R16, T01, T02) all have fallbacks in clip_needs.md; R08 and N04-N06 depend on the exact Mixamo names. Spend nothing before someone browses Mixamo for these names (about 15 minutes).

## 6. Totals (options, not a decision)

| Set | Contents | Estimated spend |
|---|---|---|
| **Recommended (test first)** | Mixamo (animations, rig, human-guise fallback, free) + Meshy free tier for Ryo and Kitsune (CC BY credit needed) + Bambo Tree $1 + Kenney Impact Sounds (free, CC0) + OGA taiko loop (free, CC-BY) | **~$1** |
| Recommended, clean licence | Same, with Meshy Pro for one month. Pro credit allowance not checked (U); 2 characters plus retries is about 100 to 150 credits at 20 to 25 per image-to-3D (V arithmetic) | **~$21** |
| Optional add-on | Fox mask prop (RenderHub, price U) | U |
| Fallback A (bought Kitsune) | Mixamo stock Ryo retextured + Superhive Kitsune $35 + Bambo Tree $1 + free audio | **~$36** |
| Fallback B (VRoid) | VRoid Studio (free) for both + Booth clothing if wanted (~2,500 JPY a full avatar, U for single items) + Bambo $1 + free audio | **~$1 to $20** |
| Backup (animation) | Mixamo (free) + RPG Animations GLB (free, MIT) + RapaMotion Melee Strikes $39.99 + itch fox $15 + Bambo Tree $1 + same free audio | **~$56** |
| Zero-spend | Mixamo (stock human wearing fox ears and tail attachment as Kitsune) + free bamboo from Godot-built primitives + free audio | $0 |

Non-money cost: the Blender work for the coat tail, tail meshes and chain bones (1e) is a few hours per character (U). It is needed for every route except a stock Mixamo human.

## 7. Open items for the designer

1. Confirm Mixamo's exact clip names by browsing (U above), especially crouch block and the magic cast clips.
2. Run the free Meshy test: upload `ryo-meshy-front.png` and `kitsune-meshy-front-notails.png`, then judge the result in Godot with the toon shader. Decide: ship free output with a CC BY credit, or pay $20 for one Pro month, or fall back to Superhive / VRoid.
3. Decide who does the Blender work: adding coat-tail and tail chain bones and cutting the tail meshes off the body (1e). Gameplay-programmer then drives them from the tick count.
4. Decide the Kitsune tail count (README question 1). It sets 9 chain bones (3 tails) or 27 (nine tails).
5. Decide whether the Ryo collar stays over the mouth (README question 1). Meshy and VRoid both follow the PNG as drawn.
6. If keeping the Superhive Kitsune as a fallback: ask the seller about outfit, mask, tail count, tail bones and rig type before paying $35. This is the largest risk (G4).
7. Confirm the Bambo Tree licence text before the public showcase.
8. Check that Mixamo's "no redistribution of raw files" clause is met: ship the Godot export only, and keep raw FBX out of any public repo. The same applies to any Meshy free-tier output that is public.
