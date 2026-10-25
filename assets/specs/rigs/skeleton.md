# Shared skeleton (AM5, F6)

Tickets: YOK-111 (Meshy spec), **YOK-146 (Option A: Tripo skeleton becomes the shared skeleton)**. Owner: rig-specialist. Rules: F6, G4, T0, E15, AM5.
Status: **SPEC (Option A), designer decision 2026-10-19 on YOK-146.** Read from the Tripo auto-rig of Ryo (`Ryo.glb`, Mixamo preset) after the scripted rebuild below. No generation job was run and no credits were spent. `rig_check.py` run against this file: see "Check run".

Every clip in the game plays on this one skeleton (F6). T0 depends on it: the Tanuki fights with Ryo's model and clips, and every yokai's clips are shared presets. A rig from another tool joins only through a bone map below. Changing this skeleton is the designer's call.

**Transition.** Kitsune, Oni and Kappa are still Meshy rigs until their redo tickets (YOK-147/148/149). Their spec is kept below under "Previous shared skeleton (Meshy)" and as `assets/specs/rigs/skeleton-meshy.json` (check them with `rig_check.py --skeleton assets/specs/rigs/skeleton-meshy.json`). Meshy clips do **not** play on this skeleton by name (`Spine` is the chest on Meshy and the lowest spine bone here); they must be retargeted through the Meshy → Tripo bone map (`tools/blender/retarget_bvh.py --clip`). The old Meshy Ryo files (`ryo-rigged.glb`, its clips) stay until the Tripo set passes G4.

## Source files
| Fighter | File | Source | Height |
|---|---|---|---|
| Ryo | `game/assets/generated/characters/ryo/ryo-tripo-rigged.glb` | Tripo auto-rig `game/assets/generated/characters/ryo/Ryo.glb` (designer drop 2026-10-19, untracked in the main checkout), rebuilt by `tools/blender/canon_tripo_rig.py`; log `assets/specs/rigs/logs/ryo-tripo-canon.json` | 1.75 m |

### Why the Tripo file is rebuilt, not used as delivered
`Ryo.glb` tears in Godot and in Blender as delivered (stills `assets/specs/rigs/stills/ryo-tripo/raw_import_*.png`):
1. **No joint node has a translation or rotation.** The bind pose exists only in the inverse bind matrices (IBMs). A glTF importer skins with joint world = identity at rest, so every vertex is moved by its own IBM and the body flies apart.
2. **The IBMs are in another frame from the mesh.** The bind skeleton is Z-up and centred on the body (an FBX frame: arms along ±Y, legs down −Z), while `POSITION` is glTF Y-up with the feet at Y = 0 and the bounding box centred on X/Z.

The weights are clean (max 4 influences, all normalised, no unweighted vertex), so `canon_tripo_rig.py` keeps them and rebuilds the rest from the file's own data: it fits the rigid frame change T (bind-skeleton frame → mesh frame) to the weights, maps every bind joint through T, and builds a new armature there. Nothing is placed by hand (F5). Fit result: mesh = s·R·skel + t with R = (mesh X = skel Y, mesh Y = skel Z, mesh Z = skel X), t = (−0.002, 0.497, 0.005) m, s = 1.013. The mean vertex-to-own-bone distance is 4.6 cm; the next-best axis rotation is 6.6 cm, and every finger bone fits its own vertices within 0.6 to 1.5 cm, which a wrong frame could not do. Rest == bind after the rebuild: max offset 0.0 m.

The same two faults should be expected on every Tripo GLB (YOK-147/148/149 and any Tripo clip export). Run `canon_tripo_rig.py` on each, and do not import a Tripo rig GLB into Godot raw.

## Frame, scale, origin
| Property | Value |
|---|---|
| Up axis | **+Y** (glTF native). Blender shows it as +Z after import |
| Facing | **+Z** in glTF (toes in front of the ankles on +Z). Character's left = +X. The Godot fighter scenes turn the model with `Mirror/Yaw` in `BaseFighter.tscn` (+90° about Y; YOK-145), unchanged |
| Units | **Metres.** Bones are in metres under an unscaled `Armature` node (the Meshy rigs used cm bones under a 0.01 node). Bone translations in the file are metres (Hips at Y 0.998). A clip on this skeleton must be in metres too: `FighterModel.HipScale` warns on a 0.01 ratio |
| Origin | At the feet: mesh bbox min Y = 0.000; X/Z centred on the midpoint of the two ankle joints (`LeftFoot`, `RightFoot`); root `Armature` at (0, 0, 0) |
| Rest pose | **T-pose = Tripo's bind pose** (not re-posed). Arms 7.7° / 7.6° below horizontal (upper arm to hand, left / right), legs 9.1° / 9.5° off vertical (hip to ankle). Figures from `rig_check.py` under "Check run" |
| Root bone | `Hips` (no root bone above it). Root motion lives only on `Hips` translation and is stripped to in place (F6); `FighterModel.cs` keeps only the `Hips` Y channel |
| Scene tree | `Armature` (no scale) → `Hips` … and the skinned mesh `ryo` as a child of `Armature`. In Godot: `Model/Armature/Skeleton3D` (same path as before) |
| Skin | One skin named `Armature`, 25 joints. Max 4 influences per vertex, all normalised |
| Bone axes | Each bone's local +Y points down the bone to its child (Mixamo convention; child translations are (0, length, 0)). Rolls are Tripo's |
| Fingers | **None.** E15 mitt hands: the 40 Tripo finger joints were removed and their weights added to `LeftHand` / `RightHand` (see "Fingers") |
| Twist bones | None |
| Embedded clip | None (the rig GLB carries no animation) |
| Mesh | One mesh, 53,785 triangles, 31,156 vertices, one material (Tripo base colour JPEG). Over the 20k budget in `assets/specs/characters/ryo-regen.md`: not decimated here, it's the designer's call |

## Bones and hierarchy
The skin order is the joint index order, as written by the Blender 4.1.1 glTF exporter. It is Tripo's own order with the fingers removed. Bone names are Mixamo names **without** the `mixamorig:` prefix (see "Prefix").

```
Hips
├── LeftUpLeg -> LeftLeg -> LeftFoot -> LeftToeBase -> LeftToe_End (leaf)
├── RightUpLeg -> RightLeg -> RightFoot -> RightToeBase -> RightToe_End (leaf)
└── Spine                       (lowest spine bone; Tripo/Mixamo counts UP: Spine -> Spine1 -> Spine2)
    └── Spine1
        └── Spine2              (chest; parent of neck and both shoulders)
            ├── LeftShoulder -> LeftArm -> LeftForeArm -> LeftHand
            ├── Neck
            │   └── Head
            │       └── HeadTop_End (top of skull, leaf)
            └── RightShoulder -> RightArm -> RightForeArm -> RightHand
```

Rest-pose joint heads, world metres, glTF axes (X left+, Y up, Z forward), from `ryo-tripo-rigged.glb`:

| # | Bone | Parent | Ryo (x, y, z) |
|---|---|---|---|
| 0 | `Hips` | (root) | 0.003, 0.998, 0.055 |
| 1 | `LeftUpLeg` | `Hips` | 0.094, 0.998, 0.055 |
| 2 | `LeftLeg` | `LeftUpLeg` | 0.156, 0.562, 0.055 |
| 3 | `LeftFoot` | `LeftLeg` | 0.225, 0.105, -0.000 |
| 4 | `LeftToeBase` | `LeftFoot` | 0.267, 0.021, 0.097 |
| 5 | `LeftToe_End` | `LeftToeBase` | 0.295, 0.021, 0.162 |
| 6 | `RightUpLeg` | `Hips` | -0.087, 0.998, 0.055 |
| 7 | `RightLeg` | `RightUpLeg` | -0.135, 0.562, 0.055 |
| 8 | `RightFoot` | `RightLeg` | -0.225, 0.105, 0.000 |
| 9 | `RightToeBase` | `RightFoot` | -0.267, 0.021, 0.097 |
| 10 | `RightToe_End` | `RightToeBase` | -0.295, 0.021, 0.162 |
| 11 | `Spine` | `Hips` | 0.003, 1.081, 0.055 |
| 12 | `Spine1` | `Spine` | 0.003, 1.192, 0.048 |
| 13 | `Spine2` | `Spine1` | 0.003, 1.296, 0.035 |
| 14 | `LeftShoulder` | `Spine2` | 0.052, 1.407, 0.007 |
| 15 | `LeftArm` | `LeftShoulder` | 0.170, 1.414, 0.007 |
| 16 | `LeftForeArm` | `LeftArm` | 0.384, 1.365, -0.000 |
| 17 | `LeftHand` | `LeftForeArm` | 0.580, 1.362, -0.012 |
| 18 | `Neck` | `Spine2` | 0.003, 1.428, 0.007 |
| 19 | `Head` | `Neck` | 0.010, 1.525, 0.021 |
| 20 | `HeadTop_End` | `Head` | 0.027, 1.756, 0.054 |
| 21 | `RightShoulder` | `Spine2` | -0.038, 1.407, 0.007 |
| 22 | `RightArm` | `RightShoulder` | -0.156, 1.414, 0.007 |
| 23 | `RightForeArm` | `RightArm` | -0.371, 1.365, 0.000 |
| 24 | `RightHand` | `RightForeArm` | -0.545, 1.366, -0.012 |

What the shared skeleton fixes and what it does not:
- **Fixed:** the 25 names (case-sensitive), the parent of each, the skin order, T-pose rest, Y-up, +Z facing, metre bones under an unscaled `Armature` node, origin at the feet, no fingers, no prefix.
- **Per rig:** bone positions (proportions). Clips are rotations per bone (F6, in place), so they carry across proportions; `Hips` translation is the only positional channel and is stripped or scaled per rig (YOK-130).

Proportion notes on this Ryo (Tripo's bind, kept as delivered):
- `LeftUpLeg`/`RightUpLeg` sit level with `Hips` (no drop to the hip sockets), and the legs splay about 8° outward at rest. Ankles are ±0.225 m apart, against ±0.13 m on the Meshy Ryo.
- The forearms differ by side: left 0.196 m, right 0.175 m. The left hand joint reaches 0.580 m and the right 0.545 m. This comes from the IBMs and is kept: correcting it would mean re-binding by hand.
- `Head` → `HeadTop_End` is 0.23 m, so the leaf sits at the top of the hair, at 1.756 m.

Weight notes (for the stress-pose checks):
- `Head` owns about 14k of the 31k vertices: the head, hair and face are dense. `LeftUpLeg` owns far more vertices than `RightUpLeg` (about 3.7k against 0.9k at >0.5); this asymmetry is Tripo's, the `ryo-tripo` pelvis repair (`repair_weights.py --fighter ryo-tripo`) moves most of the excess to `Hips`; see `assets/specs/rigs/ryo-tripo.md`.
- `Spine2` (the chest) carries little weight (12 verts > 0.5); chest skin is on `Spine`, `Spine1`, `Neck` and the shoulders.
- `HeadTop_End`, `LeftToe_End` and `RightToe_End` carry no weight. They are leaves kept for anchors (head VFX, ground contact), and stay in the skin.

## Prefix (YOK-146 decision)
**Stripped.** The bones are `Hips`, not `mixamorig:Hips`, because:
1. Godot's glTF import does not keep `:` in a bone name: `Skeleton3D` bone names and animation track paths use `:` as the subname separator, so the importer turns `mixamorig:Hips` into `mixamorig_Hips`. This was verified in Godot 4.7.2 on the raw `Ryo.glb`; the result is under "Check run". Keeping the prefix would give names that match neither the source file nor any clip exported with `:`.
2. `FighterModel.cs` looks the root up as `FindBone("Hips")` and keeps only the `Hips` position track. `kitsune.tscn` mounts on `Hips`, `oni.tscn` on `RightHand` and `kappa.tscn` on `Spine01`. With bare names, `Hips` and `RightHand` keep working unchanged; only the Meshy spine names differ (`Spine01` has no Tripo equivalent by name, see the bone map).
3. The Blender scripts strip the prefix on input (`clip_common.normalise_bone_name`), so a raw Tripo, Mixamo or mocap file with `mixamorig:` or `mixamorig_` names reads the same as a canonical one.

## Fingers (YOK-146 decision)
**Removed; their weights are merged into the hand.** E15 asks for mitt hands, so no clip may curl a finger and no code poses a fist. Keeping 40 finger joints held at rest would cost 40 joints per fighter on every pose, and any Tripo or mocap clip with finger keys would curl them unless every importer stripped them. So `canon_tripo_rig.py` adds each finger joint's weight to the same-side `Hand`, per vertex, and renormalises. On Ryo, 3115 vertex influences moved; max influences stayed at 4. Finger tracks in a source clip are dropped by the retarget (`retarget_bvh.py`, unmapped) and ignored by Godot (no such bone).

## Attachment points
Godot attaches with `BoneAttachment3D` on `Model/Armature/Skeleton3D`. Bones are in metres now, so a child needs **no** ×100 scale; Meshy rigs still need it.

| Use | Bone | Notes |
|---|---|---|
| Hand props (talisman, club) | `LeftHand`, `RightHand` | Mitt: place the prop with an offset along the bone's +Y (the old finger direction) |
| Hit sparks, chest VFX | `Spine2` (chest) or `Spine1` (mid chest) | `Spine2` holds little weight but sits at the chest |
| Head VFX, head hurt anchor | `Head`, `HeadTop_End` | No face leaf (Meshy's `headfront`): use `Head` with a +Z offset of about 0.12 m |
| Ground contact (clip measuring) | `LeftFoot`, `RightFoot`, `LeftToeBase`, `RightToeBase`, `LeftToe_End`, `RightToe_End` | Toe heads at Y 0.021, ankles at 0.105 |
| Tails, coat tail, shell | `Hips` | |

Extra bones (tails, sleeves, coat tail, shell, club) are **added per fighter by Blender script** as children of these bones, named `X_<part>_<n>` (e.g. `X_Tail_0`). They are not part of the shared 25: clips never key them, their sway comes from the tick (deterministic, never physics), and a rig with extras still plays every shared clip because the 25 are untouched.

## Bone maps

### Bone map: Meshy (previous shared skeleton)
Mapped **by chain position, not by name**: Meshy's spine counts down (`Spine02` sits on `Hips`, `Spine` is the chest), Tripo's counts up (`Spine` sits on `Hips`, `Spine2` is the chest). Used by `tools/blender/retarget_bvh.py --clip` (its `MESHY_CLIP_MAP`) to move the Meshy clips onto this skeleton. `rig_check.py --skeleton skeleton.md --bone-map Meshy` reads it too. On the Meshy Ryo it reports the two `Toe_End` leaves missing, `headfront` extra and a different skin order (skeleton FAIL, as expected), and its poses match the Meshy-file check. To check a Meshy rig on its own terms, use `skeleton-meshy.json`; on the Meshy Ryo that reproduces the YOK-116 figures exactly.

| # | Shared bone | Source bone | Notes |
|---|---|---|---|
| 0 | `Hips` | Hips | translation channel; in place |
| 11 | `Spine` | Spine02 | lowest spine (name trap: Meshy `Spine` is the chest) |
| 12 | `Spine1` | Spine01 | |
| 13 | `Spine2` | Spine | chest |
| 18 | `Neck` | neck | |
| 19 | `Head` | Head | |
| 20 | `HeadTop_End` | head_end | leaf |
| 14-17 | `LeftShoulder`, `LeftArm`, `LeftForeArm`, `LeftHand` | LeftShoulder, LeftArm, LeftForeArm, LeftHand | |
| 21-24 | `RightShoulder`, `RightArm`, `RightForeArm`, `RightHand` | RightShoulder, RightArm, RightForeArm, RightHand | |
| 1-4 | `LeftUpLeg`, `LeftLeg`, `LeftFoot`, `LeftToeBase` | LeftUpLeg, LeftLeg, LeftFoot, LeftToeBase | |
| 6-9 | `RightUpLeg`, `RightLeg`, `RightFoot`, `RightToeBase` | RightUpLeg, RightLeg, RightFoot, RightToeBase | |
| 5, 10 | `LeftToe_End`, `RightToe_End` | | no Meshy bone: held at rest |
| - | - | headfront | face leaf; no Tripo bone; its weight belongs to `Head` |

### Rules for other riggers (AccuRIG 2, CC, mocap BVH)
A rig from another tool joins only through a filled bone map under its own `### Bone map: <rigger> (<fighter>)` heading. It must play the walk, heavy and throw gate clips on this skeleton (`clips/ryo-tripo/ryo-walk.glb`, `clips/ryo-tripo/ryo-heavy.glb`, `clips/ryo-tripo/ryo-grab-2.glb`: stopgaps until the Tripo/mocap set lands, see "Check run") before any new clip is made for it.
1. **Spine order.** Mixamo/Tripo/CC rigs count the spine upwards; Meshy counts down. Map by position in the chain, never by name.
2. **More source bones than shared bones** (extra spine or neck bones, a separate pelvis): map the nearest one and keep the others at rest; record which.
3. **Twist bones**: merge their weights into the parent by script (default), or drive them from the parent's twist with a baked constraint. Record which.
4. **Fingers**: unmapped. Merge their weights into the hand (as on Ryo) or hold them at rest; no clip keys them.
5. **Rest-pose delta**: the retarget is rest-relative (world rotation change from each rig's own rest), so A-pose sources work, but prefer re-posing to this T-pose by script before export.
6. **Units and axes**: Y-up, +Z facing, origin at the feet, metre bones.
7. **Root bones above the hips**: keep one as a non-deforming parent at the origin, or remove it. It must never carry root motion.
8. **Prefixes**: `mixamorig:` / `mixamorig_` are stripped on input (`normalise_bone_name`). Other namespaces need a map.

Known source names for the next riggers (to verify on the first real file):

| Shared bone | Mixamo / Tripo (`mixamorig:` stripped) | AccuRIG 2 / CC3-CC4 (`CC_Base_`) |
|---|---|---|
| `Hips` | `Hips` | `CC_Base_Hip` (`CC_Base_Pelvis` at rest) |
| `Spine` / `Spine1` / `Spine2` | `Spine` / `Spine1` / `Spine2` | `CC_Base_Waist` / `CC_Base_Spine01` / `CC_Base_Spine02` |
| `Neck` / `Head` / `HeadTop_End` | `Neck` / `Head` / `HeadTop_End` | `CC_Base_NeckTwist01` / `CC_Base_Head` / (add by script) |
| arms | same names | `CC_Base_L_Clavicle` / `_Upperarm` / `_Forearm` / `_Hand` |
| legs | same names; `LeftToe_End` leaf | `CC_Base_L_Thigh` / `_Calf` / `_Foot` / `_ToeBase` |

## Check run
`tools/blender/rig_check.py` (Blender 4.1.1) on `ryo-tripo-rigged.glb` against this file, 2026-10-19 (YOK-146). Report `assets/specs/rigs/logs/ryo-tripo-rig-check.json`, stills `assets/specs/rigs/stills/ryo-tripo/`:
- **PASS:** skeleton (25 names, parents, skin order), rest pose (T, rest == bind), scale (1.750 m), up axis and facing (+Y, glTF +Z), origin, weights (max 4, normalised, no cross-side, no arm-leg bleed).
- **WARN:** high kick 44, arm raise 2, torso twist 7, grab 8 failing edges (limit 169).
- **FAIL:** deep crouch, on `LeftUpLeg` volume only (mean 0.749, p5 0.29; 55 edges). That's the paper charm and pouch painted on the left hip, as on the Meshy Ryo; the stills are clean. Verdict and details in `assets/specs/rigs/ryo-tripo.md`.
- Godot 4.7.2: imports as `Armature/Skeleton3D`, 25 bones, `Hips` rest Y 0.998. The raw Tripo file imports with `mixamorig_` names and every rest at the origin.

Tool support for these names (YOK-146): `clip_common.normalise_bone_name` / `resolve_bone` (strips `mixamorig:` and `mixamorig_`, and maps Meshy names by chain position when the rig lacks them), used by `measure_clip.py`, `clean_clip.py`, `skin_clip_check.py` and `retarget_bvh.py`. `rig_check.py` normalises prefixes and renames its stress poses to the Tripo spine. `repair_weights.py` picks the Tripo trunk chain.

The gate clips for G4 on this skeleton are the stopgap retargets `game/assets/generated/clips/ryo-tripo/ryo-walk.glb`, `ryo-heavy.glb` and `ryo-grab-2.glb` until the Tripo exports and mocap takes land (`assets/specs/clips/retarget_gate.md`, YOK-146).

---

# Previous shared skeleton (Meshy)

Kept for Kitsune, Oni and Kappa until YOK-147/148/149, and for reading the Meshy clips. Machine-readable copy: `assets/specs/rigs/skeleton-meshy.json` (the table below is retitled "Meshy bone" so `rig_check.py` reads only the Tripo table above from this file). Text as written in YOK-111:

Ticket: YOK-111. Owner: rig-specialist. Rules: F6, G4, T0, E15, AM5.
Status: **SPEC.** Read from the accepted Meshy rigs of Ryo and Kitsune (YOK-30 rebuild, YOK-31 gate). No generation job was run and no credits were spent. The scripted rig check (`tools/blender/rig_check.py`, YOK-110) does not exist yet, so its run against this spec is **pending** (see the end).

Every clip in the game plays on this one skeleton (F6). T0 depends on it: the Tanuki fights with Ryo's model and clips, and every yokai's clips are shared presets. A rig from another tool joins only through a bone map below. Changing this skeleton is the designer's call.

### Source files
| Fighter | File | Rig task id | Height |
|---|---|---|---|
| Ryo | `game/assets/generated/characters/ryo/ryo-rigged.glb` | `01a10e47-857e-73a0-9f7a-cd74795198fb` | 1.75 m |
| Kitsune | `game/assets/generated/characters/kitsune/kitsune-rigged.glb` | `01a10e47-91f6-709b-bd34-8884f8cbabbb` | 1.65 m |

How it was read (2026-10-15): the glTF JSON chunk parsed directly, and Blender 4.1.1 headless (`-b`, glTF import, read only) for world-space bone heads, vertex groups and weights. The joint list was then compared across every skinned GLB under `game/assets/generated/` (both rigs, their walk/run GLBs, all clip GLBs in `clips/`, `clips/ryo/`, `clips/kitsune/`): **all of them have the same 24 joints in the same skin order.**

### Frame, scale, origin
| Property | Value |
|---|---|
| Up axis | **+Y** (glTF native). Blender shows it as +Z after import |
| Facing | **+Z** in glTF (the `headfront` joint sits 12.7 cm (Ryo) / 16.5 cm (Kitsune) in front of `Head` on +Z). Character's left = +X. The Godot fighter scenes turn the model with the `Mirror/Yaw` node of `game/scenes/fighters/BaseFighter.tscn` (+90° about Y: rig +Z → world +X = sim facing right; YOK-145), shared by every fighter, not by changing the rig or a per-fighter rotation |
| Units | Bones in **centimetres**: the `Armature` node carries scale 0.01, so world units are metres. Bone translations in the files are in cm (Hips at Y about 95) |
| Origin | At the feet: mesh bbox min Y = 0.000 on both rigs; root at world (0, 0, 0) |
| Rest pose | **T-pose**: arms horizontal (hand head 3.8 cm (Ryo) / 6.0 cm (Kitsune) below the shoulder head), legs straight, feet slightly apart (ankles ±0.13 m) |
| Root bone | `Hips` (no separate root bone above it; root motion, if any, lives on `Hips` translation and is stripped to in-place clips per F6) |
| Scene tree | `Armature` (scale 0.01) → `Hips` … and the skinned mesh `char1` as a child of `Armature`. In Godot: `Model/Armature/Skeleton3D` |
| Skin | One skin named `Armature`, 24 joints. Max 4 influences per vertex, all weights normalised (both rigs) |
| Fingers | **None.** Hands are single mitts (E15: open-hand strikes, no code-posed fists) |
| Twist bones | None |
| Embedded clip | Each rig GLB carries one single-frame clip `Armature|clip0|baselayer` (the bind pose); ignore it on import |

### Bones and hierarchy
Skin order is the joint index order used by every clip GLB. All 24 joints carry weights on both rigs, including the two head leaves.

```
Hips
├── Spine02                      (lowest spine bone; Meshy counts DOWN: Spine02 -> Spine01 -> Spine)
│   └── Spine01
│       └── Spine                (chest; parent of neck and both shoulders)
│           ├── neck
│           │   └── Head
│           │       ├── head_end   (top of skull, leaf)
│           │       └── headfront  (face, leaf)
│           ├── LeftShoulder -> LeftArm -> LeftForeArm -> LeftHand
│           └── RightShoulder -> RightArm -> RightForeArm -> RightHand
├── LeftUpLeg -> LeftLeg -> LeftFoot -> LeftToeBase
└── RightUpLeg -> RightLeg -> RightFoot -> RightToeBase
```

Rest-pose joint heads, world metres, glTF axes (X left+, Y up, Z forward):

| # | Meshy bone | Parent | Ryo (x, y, z) | Kitsune (x, y, z) |
|---|---|---|---|---|
| 0 | `Hips` | (root) | 0.008, 0.949, -0.004 | 0.000, 0.959, 0.048 |
| 1 | `Spine02` | `Hips` | 0.006, 1.066, -0.008 | 0.001, 1.058, 0.046 |
| 2 | `Spine01` | `Spine02` | 0.005, 1.183, -0.011 | 0.001, 1.156, 0.043 |
| 3 | `Spine` | `Spine01` | 0.004, 1.303, -0.014 | 0.002, 1.258, 0.040 |
| 4 | `neck` | `Spine` | 0.002, 1.385, -0.017 | 0.003, 1.320, 0.038 |
| 5 | `Head` | `neck` | 0.000, 1.455, -0.018 | 0.004, 1.386, 0.037 |
| 6 | `head_end` | `Head` | 0.008, 1.702, -0.003 | 0.020, 1.580, 0.068 |
| 7 | `headfront` | `Head` | 0.000, 1.455, 0.127 | 0.004, 1.386, 0.202 |
| 8 | `LeftShoulder` | `Spine` | 0.034, 1.347, -0.015 | 0.035, 1.309, 0.039 |
| 9 | `LeftArm` | `LeftShoulder` | 0.162, 1.347, -0.026 | 0.164, 1.309, 0.022 |
| 10 | `LeftForeArm` | `LeftArm` | 0.366, 1.313, -0.021 | 0.331, 1.252, 0.030 |
| 11 | `LeftHand` | `LeftForeArm` | 0.578, 1.309, -0.013 | 0.545, 1.249, 0.039 |
| 12 | `RightShoulder` | `Spine` | -0.033, 1.347, -0.016 | -0.029, 1.306, 0.039 |
| 13 | `RightArm` | `RightShoulder` | -0.170, 1.347, -0.027 | -0.153, 1.306, 0.021 |
| 14 | `RightForeArm` | `RightArm` | -0.355, 1.313, -0.022 | -0.306, 1.252, 0.029 |
| 15 | `RightHand` | `RightForeArm` | -0.570, 1.313, -0.017 | -0.534, 1.256, 0.037 |
| 16 | `LeftUpLeg` | `Hips` | 0.084, 0.863, -0.004 | 0.084, 0.868, 0.048 |
| 17 | `LeftLeg` | `LeftUpLeg` | 0.117, 0.516, 0.031 | 0.121, 0.505, 0.099 |
| 18 | `LeftFoot` | `LeftLeg` | 0.137, 0.121, -0.003 | 0.114, 0.141, 0.058 |
| 19 | `LeftToeBase` | `LeftFoot` | 0.173, 0.031, 0.075 | 0.139, 0.049, 0.118 |
| 20 | `RightUpLeg` | `Hips` | -0.076, 0.863, -0.004 | -0.079, 0.873, 0.048 |
| 21 | `RightLeg` | `RightUpLeg` | -0.113, 0.520, 0.031 | -0.114, 0.529, 0.099 |
| 22 | `RightFoot` | `RightLeg` | -0.132, 0.123, -0.004 | -0.110, 0.141, 0.047 |
| 23 | `RightToeBase` | `RightFoot` | -0.169, 0.032, 0.073 | -0.138, 0.050, 0.114 |

What the shared skeleton fixes and what it does not:
- **Fixed:** the 24 names (case-sensitive, note lower-case `neck`, `head_end`, `headfront`), the parent of each, the skin order, T-pose rest, Y-up, +Z facing, cm bones under a 0.01 node, origin at the feet.
- **Per rig:** bone positions (proportions). The two rigs differ by height (1.75 / 1.65 m) and Kitsune's whole column sits about 5 cm forward of the origin. Clips are rotations per bone (F6, in place), so they carry across proportions; `Hips` translation is the only positional channel and is stripped or kept small in place.

Weight notes (both rigs, for the stress-pose checks):
- `Spine` (the chest bone) carries almost no weight (Ryo 7 verts, Kitsune 50); chest skin is on `Spine01`, `neck` and the shoulders. Torso twist reads mostly through `Spine02`/`Spine01`.
- `head_end` and `headfront` are weighted (hair, mask, collar), so they are deform bones and must stay in the skin.

### Attachment points (no new bones needed for Ryo and Kitsune)
Godot attaches with `BoneAttachment3D` on `Model/Armature/Skeleton3D` (positions in the bone's cm space, so children carry scale 100 to work in metres, as the Kitsune tails do).

| Use | Bone | Notes |
|---|---|---|
| Hand props (talisman, club) | `LeftHand`, `RightHand` | No fingers: the prop is placed on the mitt with an offset |
| Hit sparks, chest VFX | `Spine01` (mid chest) | `Spine` is the chest joint but nearly unweighted; either works for a VFX anchor |
| Head VFX, head hurt anchor | `Head`, `headfront` | `headfront` gives the face point directly |
| Ground contact (clip measuring) | `LeftToeBase`, `RightToeBase`, `LeftFoot`, `RightFoot` | Toe heads sit 3-5 cm above Y=0 at rest; foot plant = toe or foot height minimum |
| Tails, coat tail, shell | `Hips` | Kitsune's nine tails ride `Hips` today |

Extra bones (tails, sleeves, coat tail, shell, club) are **added per fighter by Blender script** as children of these bones, named `X_<part>_<n>` (e.g. `X_Tail_0`). They are not part of the shared 24: clips never key them, their sway comes from the tick (deterministic, never physics), and a rig with extras still plays every shared clip because the 24 are untouched.

### Bone map for other riggers
A rig from Tripo auto-rig or AccuRIG 2 joins only through a filled bone map, stored in this file under its own heading, and must play the existing walk, heavy and throw clips (`characters/ryo/ryo-walk.glb`, `clips/ryo-heavy.glb`, and the E15 throw, preset 259 `Step_Forward_and_Push`: `clips/ryo-grab-2.glb`, head-trimmed from tick 30) before any new clip is made for it.

Template (one row per shared bone; copy it under `### Bone map: <rigger> (<fighter>)`):

| # | Shared bone | Source bone | Rest delta (deg, source vs shared) | Notes |
|---|---|---|---|---|
| 0 | `Hips` | | | translation channel; strip to in place |
| 1 | `Spine02` | | | lowest spine |
| 2 | `Spine01` | | | |
| 3 | `Spine` | | | chest, parent of neck and shoulders |
| 4 | `neck` | | | |
| 5 | `Head` | | | |
| 6 | `head_end` | | | if the source has no head-top leaf, add it by script at the skull top |
| 7 | `headfront` | | | if absent, add by script at the face, +Z of `Head` |
| 8-11 | `LeftShoulder`, `LeftArm`, `LeftForeArm`, `LeftHand` | | | |
| 12-15 | `RightShoulder`, `RightArm`, `RightForeArm`, `RightHand` | | | |
| 16-19 | `LeftUpLeg`, `LeftLeg`, `LeftFoot`, `LeftToeBase` | | | |
| 20-23 | `RightUpLeg`, `RightLeg`, `RightFoot`, `RightToeBase` | | | |
| - | (unmapped source bones) | | | list each: fingers, twist, extra spine/neck, roots |

Rules for filling it:
1. **Spine order trap.** Meshy counts the spine downwards (`Spine02` is just above the hips, `Spine` is the chest). Mixamo-style and CC rigs count upwards (`Spine` or `Spine01` lowest). Map by position in the chain, never by name.
2. **More source bones than shared bones** (extra spine/neck, a separate pelvis): map the nearest one and keep the others at their rest pose; record which stay at rest. Their weights stay as made, but the rig check must show the stress poses still bend cleanly with those bones frozen.
3. **Twist bones** (CC and some Tripo rigs): shared clips never key them, so a forearm or upper-arm twist would sit at rest and candy-wrap. Either merge their weights into the parent by script (default) or drive them from the parent's twist with a scripted constraint baked at export. Record which.
4. **Fingers**: unmapped, held at rest (open hand per E15). Remove or keep them, but no clip keys them.
5. **Rest-pose delta**: if the source rest is an A-pose or its bone rolls differ, the retarget is rest-relative and the per-bone delta goes in the table. Preferred: re-pose the source to the shared T-pose and re-apply rest by script before export, so the delta is zero.
6. **Units and axes**: convert to Y-up, +Z facing, origin at the feet; bone units may be cm or m as long as the world height is right. The scripted check verifies after conversion.
7. **Root bones above the hips** (a `Root`/`CC_Base_BoneRoot` node): keep it as a non-deforming parent at the origin or remove it; never let it carry root motion.

Known source names (to verify on the first real file, not yet seen in this repo):

| Shared bone | AccuRIG 2 / CC3-CC4 (`CC_Base_` prefix) | Tripo auto-rig |
|---|---|---|
| `Hips` | `CC_Base_Hip` (with `CC_Base_Pelvis` under it holding the thighs: keep Pelvis at rest) | fill from the first Tripo file |
| `Spine02` / `Spine01` / `Spine` | `CC_Base_Waist` / `CC_Base_Spine01` / `CC_Base_Spine02` | |
| `neck` / `Head` | `CC_Base_NeckTwist01` (`NeckTwist02` at rest) / `CC_Base_Head` | |
| `LeftShoulder` / `LeftArm` / `LeftForeArm` / `LeftHand` | `CC_Base_L_Clavicle` / `CC_Base_L_Upperarm` / `CC_Base_L_Forearm` / `CC_Base_L_Hand` | |
| `LeftUpLeg` / `LeftLeg` / `LeftFoot` / `LeftToeBase` | `CC_Base_L_Thigh` / `CC_Base_L_Calf` / `CC_Base_L_Foot` / `CC_Base_L_ToeBase` | |
| right side | same with `_R_` | |
| twist bones (rule 3) | `CC_Base_L_UpperarmTwist01/02`, `CC_Base_L_ForearmTwist01/02`, `CC_Base_L_ThighTwist01/02`, `CC_Base_L_CalfTwist01/02` (and `_R_`) | |
| fingers (rule 4) | `CC_Base_L_Thumb1..3`, `CC_Base_L_Index1..3`, … | |

### Check run
- **Pending:** `tools/blender/rig_check.py` (YOK-110) does not exist yet. When it lands, run it on both rig GLBs against this file (names, hierarchy, skin order, rest pose, Y-up, scale, origin, 4 influences, normalised weights) and record the result here and in `ryo.md` / `kitsune.md`.
- The figures above were read by a one-off read-only Blender import and a glTF parse during YOK-111; they are the expected values for that check.
