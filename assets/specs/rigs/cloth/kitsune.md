# Kitsune cloth regions (rig_check.py --cloth)

Owner: rig-specialist. Ticket: YOK-110. Designer decision on YOK-110: cloth verts are exempt from the stress-pose edge-tear and volume checks, and their stretch is still reported as info.

Rule (see the header of `tools/blender/rig_check.py`): a vert is cloth when its highest-weight bone is a region bone and, in the rest pose, it sits at least `min_radius_m` from that bone's segment, inside `z_band_m` and inside the `axial_min`/`axial_max` span. No vertex indices. `Hips` and the spine and head chain are never cloth.

- **sleeves_upper / sleeves_lower**: the wide furisode sleeves. Body arm skin is within about 0.06 m of the bone (upper arm p10 0.048 m, forearm 0.028 m), and sleeve cloth reaches 0.31 m.
  - Two limits keep out the long hair locks that hang beside and behind the shoulder: `axial_min: 0.5` (the outer half of the upper arm) and `depth_band_m` (no further than 0.12 m behind the origin; the hair hangs 0.15 to 0.21 m back). The hair carries `LeftArm`/`RightArm` weight in this rig, and that is a weight fault, not cloth, so it stays checked.
  - The sleeve seam at the shoulder stays checked too.
- **hakama / hakama_hem**: the generated mesh is a single shell with no legs modelled under the hakama, so every `UpLeg`/`Leg` vert from 0.30 to 0.85 m is hakama cloth. Between 0.08 and 0.30 m only the hem flare counts: at least 0.07 m from the shin bone. The socks, inside that radius, stay checked. Both ends stay checked:
  - **Below 0.08 m** (feet).
  - **Above 0.85 m**: the waist and crotch band, where the hakama meets `Hips`.

Audit: `cloth_mask_front.png` and `cloth_mask_side.png` show cloth in red.

```cloth-regions-json
{
 "fighter": "kitsune",
 "regions": [
  {"name": "sleeves_upper", "bones": ["LeftArm", "RightArm"],
   "min_radius_m": 0.09, "axial_min": 0.5, "depth_band_m": [-0.12, 0.15]},
  {"name": "sleeves_lower", "bones": ["LeftForeArm", "RightForeArm"],
   "min_radius_m": 0.09, "axial_min": 0.0},
  {"name": "hakama", "bones": ["LeftUpLeg", "RightUpLeg", "LeftLeg", "RightLeg"],
   "min_radius_m": 0.0, "z_band_m": [0.30, 0.85]},
  {"name": "hakama_hem", "bones": ["LeftLeg", "RightLeg"],
   "min_radius_m": 0.07, "z_band_m": [0.08, 0.30]}
 ]
}
```
