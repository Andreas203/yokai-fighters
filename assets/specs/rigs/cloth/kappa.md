# Kappa cloth regions (rig_check.py --cloth)

Owner: rig-specialist. Ticket: YOK-102. The YOK-110 designer rule applies: cloth verts are exempt from the stress-pose edge-tear and volume checks, and their stretch is still reported as info. Same rule as `cloth/kitsune.md`. `Hips` and the spine and head chain are never cloth.

Frame: the game file `kappa-rigged.glb` (1.551 m after the leg re-pose, the shell cut off; Blender world metres, +Z up).

- **loincloth**: the rice-paper flaps hang from the rope at the waist, in front of and behind the crotch. Their verts sit well off the thigh bone, at least 0.17 m from the `UpLeg` segment, while the bare thigh skin is inside that radius.
  - The thigh skin stays checked.
  - The rope and waist band above 0.71 m stay checked (the band moved up 5.1 cm with the leg re-pose, 2026-10-16).

```cloth-regions-json
{
 "fighter": "kappa",
 "regions": [
  {"name": "loincloth", "bones": ["LeftUpLeg", "RightUpLeg"],
   "min_radius_m": 0.17, "z_band_m": [0.43, 0.71]}
 ]
}
```
